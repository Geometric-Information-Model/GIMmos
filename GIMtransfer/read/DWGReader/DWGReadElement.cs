using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using GIMcompress;
using GIMtransfer.read.DWGReader;
using GimUtilities;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GIMtransfer
{
    public partial class DWGReadElement
    {
        // const values
        public const string DWG_DISK_READ = "DWGDiskRead";
        public const string DWG_BLOCK_READ = "DWGBlockRead";

        GimProgressStatus ReadProgress;
        public bool IsBrep = false;
        Dictionary<string, uint> blockToElementDict = new();
        public DWGReadElement(GimProgressStatus readProgress = null) {
            ReadProgress = readProgress;
        }

        public static bool SupportsType(string fileType)
        {
            return fileType.ToLower() switch
            {
                "dwg" => true,
                "dxf" => true,
                _ => false,
            };
        }
        public GimData ReadDWGFile(CadDocument doc)
        {

            var insUnits = doc.Header.InsUnits;

            var allSpaces = new List<object>();

            // Use reflection to get all spaces
            var docType = doc.GetType();
            var field = docType.GetField("_cadObjects", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
                throw new InvalidOperationException("Field '_cadObjects' not found.");

            var cadObjects = field.GetValue(doc) as IEnumerable;

            if (cadObjects == null)
                throw new InvalidOperationException("_cadObjects is empty or not an IEnumerble");

            // Loop through and find BlockRecords that are spaces
            foreach (var holder in cadObjects)
            {
                if (holder == null) continue;

                var holderType = holder.GetType();

                // Try to get Value as a PROPERTY instead of a field
                var valueProp = holderType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);

                if (valueProp == null) continue;

                var cadObj = valueProp.GetValue(holder);

                if (cadObj == null) continue;

                var cadObjType = cadObj.GetType();

                // Check ObjectName to see if it's a BLOCK_RECORD
                var objectNameProp = cadObjType.GetProperty(
                    "ObjectName",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );
                var objectName = objectNameProp?.GetValue(cadObj)?.ToString();

                if (objectName?.Equals("BLOCK_RECORD", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Get the Name property
                    var nameProp = cadObjType.GetProperty(
                        "Name",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    var name = nameProp?.GetValue(cadObj)?.ToString();

                    // Check if the name indicates it's a space (*Model_Space or *Paper_Space*)
                    if (name != null &&
                        (name.Equals("*Model_Space", StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase)))
                    {
                        allSpaces.Add(cadObj);
                    }
                }
            }

            var modelSpace = allSpaces.FirstOrDefault(br =>
            {
                var nameProp = br.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var brName = nameProp?.GetValue(br)?.ToString();
                return brName?.Equals("*Model_Space", StringComparison.OrdinalIgnoreCase) == true;
            });

            var paperSpaces = allSpaces.Where(br =>
            {
                var nameProp = br.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var brName = nameProp?.GetValue(br)?.ToString();
                return brName?.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase) == true;
            }).Select(br =>
            {
                var nameProp = br.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var brName = nameProp?.GetValue(br)?.ToString();
                return (LayoutName: brName, BlockRecord: br);
            }).ToList();

            // Calculate block count
            int blockCount = 0;
            if (modelSpace != null)
            {
                BlockRecord modelRecord = modelSpace as BlockRecord;
                if (modelRecord != null)
                    blockCount = GetBlockCount(modelRecord);
            }

            ReadProgress?.Create(new Dictionary<string, int>(){
                { DWG_DISK_READ, 100},
                { DWG_BLOCK_READ, blockCount != 0 ? blockCount * 100 : 100 },
            });
            ReadProgress?.ForceCompleteTask(DWG_DISK_READ);

            // Process Model_Space and Paper_Space differently
            GimData gimData = ProcessModelAndPaperSpaces(modelSpace, paperSpaces);

            ReadProgress?.ForceCompleteTask(DWG_BLOCK_READ);

            return gimData;
        }

        private GimData ProcessModelAndPaperSpaces(object modelSpace, List<(string LayoutName, object BlockRecord)> paperSpaces)
        {
            GimData gimData = new();
            gimData.CreateRootIfNotExists();

            uint modelSpaceFolderIndex = 0;

            if (modelSpace != null)
            {
                BlockRecord modelRecord = modelSpace as BlockRecord;
                if (modelRecord != null)
                {
                    bool isModelSpace2D = IsBlockRecord2D(modelRecord);
                    BlockRecordToFolder(modelRecord, gimData, parentIndex: 0, "Model", isRead2D: isModelSpace2D);
                    modelSpaceFolderIndex = blockToElementDict["Model"];
                }
            }

            foreach (var (layoutName, blockRecord) in paperSpaces)
            {
                BlockRecord record = blockRecord as BlockRecord;
                if (record == null) continue;
                AddPaperSpaceAs2DRep(record, gimData, layoutName);
            }

            return gimData;
        }

        private void AddPaperSpaceAs2DRep(BlockRecord record, GimData gimData, string layoutName)
        {
            // Create R_2DRep element
            uint rep2DIndex = (uint)gimData.Elements.Count;

            GimElement rep2DElement = new()
            {
                ElementType = GimElementType.R_2DRep,
            };

            gimData.Elements.Add(rep2DElement);
            gimData.AddChildToParent(rep2DIndex, 0); // Add as child of root

            // Add object name for the representation
            gimData.Objects.Add(GimObjectFactory.Create(rep2DIndex, layoutName));

            // Read the 2D entities from the Paper_Space into this representation
            Read2DParts(gimData, rep2DIndex, record);

            ReadProgress?.EntityCompletedCallback(DWG_BLOCK_READ);
        }

        private static readonly Dictionary<UnitsType, double> Conversions = new()
        {
            // Unitless
            { UnitsType.Unitless, 1 },

            // Metric units
            { UnitsType.Millimeters, 1 },
            { UnitsType.Centimeters, 10 },
            { UnitsType.Decimeters, 100 },
            { UnitsType.Meters, 1000 },
            { UnitsType.Decameters, 10000 },
            { UnitsType.Hectometers, 100000 },
            { UnitsType.Kilometers, 1000000 },
            { UnitsType.Microns, 0.001 },
            { UnitsType.Nanometers, 0.000001 },
            { UnitsType.Angstroms, 0.0000001 },
            { UnitsType.Gigameters, 1000000000000 },

            // Imperial units
            { UnitsType.Inches, 25.4 },
            { UnitsType.Feet, 304.8 },
            { UnitsType.Yards, 914.4 },
            { UnitsType.Miles, 1609344 },
            { UnitsType.Microinches, 0.0000254 },
            { UnitsType.Mils, 0.0254 },

            // Survey units (US Survey)
            { UnitsType.USSurveyFeet, 304.8006096012192 },
            { UnitsType.USSurveyInches, 25.40005080010160 },
            { UnitsType.USSurveyYards, 914.4018288036576 },
            { UnitsType.USSurveyMiles, 1609347.2186944375 },

            // Astronomical units
            { UnitsType.AstronomicalUnits, 149597870700000.0 },
            { UnitsType.LightYears, 9.461e+18 },
            { UnitsType.Parsecs, 3.086e+19 }
        };

        public static double ConvertToMillimeters(double value, UnitsType units)
        {
            if (Conversions.TryGetValue(units, out double factor))
                return value * factor;

            throw new ArgumentException($"Unsupported unit type: {units}");
        }

        public static double GetValueInMillimeters(double value, UnitsType units)
        {
            return ConvertToMillimeters(value, units);
        }

        private int GetTotalBlockCount(List<(string LayoutName, object BlockRecord)> spaces)
        {
            int totalCount = 0;
            foreach (var (layoutName, blockRecord) in spaces)
            {
                BlockRecord record = blockRecord as BlockRecord;
                if (record == null) continue;
                totalCount += GetBlockCount(record);
            }
            return totalCount;
        }

        public int GetBlockCount(BlockRecord blockRecord)
        {
            int totalCount = 0;
            foreach(var entity in blockRecord.Entities)
            {
                if (entity is Insert insert)
                {
                    totalCount++;
                    totalCount += GetBlockCount(insert.Block);
                }                   
            }
            return totalCount;
        }

        public bool DetectBrepOrPolyRep(BlockRecord record)
        {
            foreach(var entity in record.Entities)
            {
                // recursive call, check subfolders
                if(entity is Insert insert && DetectBrepOrPolyRep(insert.Block)) return true;
                // check single entity
                if (IsBrepEntity(entity)) return true;
            }

            return false;
        }

        public static bool IsBrepEntity(Entity entity)
        {
            switch (entity.ObjectType)
            {
                case ObjectType.SOLID:
                case ObjectType.SOLID3D:
                //case ObjectType.LINE: // Line is ambiguous
                case ObjectType.CIRCLE:
                case ObjectType.SPLINE:
                case ObjectType.ELLIPSE:
                case ObjectType.FACE3D:
                case ObjectType.ARC:
                case ObjectType.SHAPE:
                    return true;
            }
            return false;
        }

        //private GimData ModelSpaceToGimData(BlockRecord modelSpace, bool isRead2D)
        //{
        //    GimData gimData = new();
        //    gimData.CreateRootIfNotExists();
        //    BlockRecordToGimData(modelSpace, gimData, 0, isRead2D);
        //    return gimData;
        //}

        private GimData AllSpacesToGimData(List<(string LayoutName, object BlockRecord)> spaces, bool isRead2D)
        {
            GimData gimData = new();
            gimData.CreateRootIfNotExists();

            foreach (var (layoutName, blockRecord) in spaces)
            {
                // Cast the object to BlockRecord (you'll need to do this properly based on your reflection)
                BlockRecord record = blockRecord as BlockRecord;
                if (record == null) continue;

                // Process each space as a folder under root
                BlockRecordToFolder(record, gimData, parentIndex: 0, layoutName, isRead2D);
            }

            return gimData;
        }

        private GimData BlockRecordToGimData(BlockRecord record, GimData gimData, uint parentIndex)
        {
            bool containsSubStructure = false;

            foreach(var entity in record.Entities)
            {
                if (entity is Insert insert)
                {
                    var childBlockRecord = insert.Block;
                    if (childBlockRecord.Entities.Count == 0) continue;

                    containsSubStructure = true;
                    break;
                }
            }

            if (containsSubStructure)
                return BlockRecordToFolder(record, gimData, parentIndex);

            return BlockRecordToPart(record, gimData, parentIndex);
        }

        private GimData BlockRecordToPart(BlockRecord record, GimData gimData, uint parentIndex, bool isSubGeometry = false, bool isRead2D = false)
        {
            string blockName = isSubGeometry? record.Name + "-Geometry" : record.Name;
            uint partIndex;

            if (blockToElementDict.ContainsKey(blockName))
            {
                partIndex = blockToElementDict[blockName];
                gimData.AddChildToParent(partIndex, parentIndex);
                ReadProgress?.EntityCompletedCallback(DWG_BLOCK_READ);
                return gimData;
            }

            partIndex = (uint)gimData.Elements.Count;

            GimElement partElement = new()
            {
                ElementType = GimElementType.D_part,
            };
            if (!isRead2D)
            {
                gimData.AddChildToParent(partIndex, parentIndex);
                gimData.Elements.Add(partElement);
            }

            blockToElementDict[blockName] = partIndex;

            gimData.Objects.Add(GimObjectFactory.Create(partIndex, blockName));

            //var entityInfos = DWGEntities.CollectEntities2D(record);
            //bool has2DEntities = entityInfos.Any(e => e.Is2D);
            
            if (IsBrep)
            {
                return gimData;
            }
            else if (isRead2D)
            {
                Read2DParts(gimData, parentIndex, record);
            }
            else
            {
                DWGReadPolyrep.ReadPolyrepPart(gimData, partIndex, record);
            }

            ReadProgress?.EntityCompletedCallback(DWG_BLOCK_READ);

            return gimData;
        }

        //private GimData BlockRecordToFolder(BlockRecord record, GimData gimData, uint parentIndex)
        //{
        //    string blockName = record.Name;
        //    uint folderIndex;
        //    if (blockToElementDict.ContainsKey(blockName))
        //    {
        //        folderIndex = blockToElementDict[blockName];
        //        gimData.AddChildToParent(folderIndex, parentIndex);
        //        ReadProgress?.EntityCompletedCallback(DWG_BLOCK_READ);
        //        return gimData;
        //    }

        //    folderIndex = (uint)gimData.Elements.Count;
        //    GimElement folderElement = new()
        //    {
        //        ElementType = GimElementType.D_folder,
        //    };

        //    gimData.Elements.Add(folderElement) ;
        //    gimData.AddChildToParent(folderIndex, parentIndex);

        //    blockToElementDict[blockName] = folderIndex;
        //    gimData.Objects.Add(new()
        //    {
        //        Element = folderIndex,
        //        Name = blockName
        //    });

        //    bool containsGeometry = false;

        //    foreach (var entity in record.Entities)
        //    {
        //        if (entity is Insert insert)
        //            continue;

        //        containsGeometry = true;
        //        break;
        //    }

        //    List<Entity> remainingEntities = new();

        //    // reading folders
        //    foreach(var entity in record.Entities)
        //    {
        //        if (entity is not Insert insert)
        //        {
        //            remainingEntities.Add(entity);
        //            continue;
        //        }

        //        var childBlockRecord = insert.Block;
        //        if (childBlockRecord.Entities.Count == 0) continue;

        //        BlockRecordToGimData(childBlockRecord, gimData, folderIndex,isRead2DPart: true);

        //       //gimData.AddSecondaryGimData(childGimData, 1, folderIndex, 0, true);
        //    }

        //    // reading part structure
        //    if (containsGeometry)
        //    {
        //        BlockRecordToPart(record, gimData, parentIndex,true, isRead2D: true);
        //    }

        //    ReadProgress?.EntityCompletedCallback(DWG_BLOCK_READ);
        //    return gimData;
        //}

        private GimData BlockRecordToFolder(BlockRecord record, GimData gimData, uint parentIndex, string customName = null, bool isRead2D = false)
        {
            string blockName = customName ?? record.Name;
            uint folderIndex;

            if (blockToElementDict.ContainsKey(blockName))
            {
                folderIndex = blockToElementDict[blockName];
                gimData.AddChildToParent(folderIndex, parentIndex);
                ReadProgress?.EntityCompletedCallback(DWG_BLOCK_READ);
                return gimData;
            }

            folderIndex = (uint)gimData.Elements.Count;

            bool is2D = record.Name.StartsWith("*Paper_Space") ? true : IsBlockRecord2D(record);

            GimElement folderElement = new()
            {
                ElementType = GimElementType.D_folder,
                ElementBools = new int[] { is2D ? 1 : 0 }  // 1 = true (2D), 0 = false (3D)
            };
            gimData.Elements.Add(folderElement);
            gimData.AddChildToParent(folderIndex, parentIndex);
            blockToElementDict[blockName] = folderIndex;

            gimData.Objects.Add(GimObjectFactory.Create(folderIndex, blockName));

            bool containsGeometry = false;
            foreach (var entity in record.Entities)
            {
                if (entity is Insert insert)
                    continue;
                containsGeometry = true;
                break;
            }

            List<Entity> remainingEntities = new();

            // reading folders (nested blocks)
            if (!isRead2D)
            {
                foreach (var entity in record.Entities)
                {
                    if (entity is not Insert insert)
                    {
                        remainingEntities.Add(entity);
                        continue;
                    }
                    var childBlockRecord = insert.Block;
                    if (childBlockRecord.Entities.Count == 0) continue;
                    BlockRecordToGimData(childBlockRecord, gimData, folderIndex);
                }
            }

            // reading part structure
            if (containsGeometry)
            {
                BlockRecordToPart(record, gimData, folderIndex, true, isRead2D: isRead2D);
            }

            ReadProgress?.EntityCompletedCallback(DWG_BLOCK_READ);
            return gimData;
        }

        private bool IsBlockRecord2D(BlockRecord record)
        {
            List<Entity> entities2D = new();
            List<Entity> entities3D = new();

            foreach (var entity in record.Entities)
            {
                //if (entity is Insert)
                //    continue;
                if (DWGEntities.IsEntity2D(entity))
                    entities2D.Add(entity);
                else
                    entities3D.Add(entity);
            }

            int totalEntities = entities2D.Count + entities3D.Count;

            if (totalEntities == 0) return true; // Empty folder defaults to 2D

            // If 80% or more entities are 2D, consider the folder 2D
            double percentage2D = (double)entities2D.Count / totalEntities;
            return percentage2D >= 0.9;
        }

        private string GetUniqueRecordName(string recordName)
        {
            lock (blockToElementDict)
            {
                string potentialRecordName = recordName;
                int index = 1;
                while (blockToElementDict.ContainsKey(potentialRecordName))
                {
                    GimLogger.Warn("Duplicate entity name, modifying...");
                    potentialRecordName = recordName + "-" + index;
                }

                return potentialRecordName;
            }
        }

        public static void Read2DParts(GimData gimData, uint folderIndex, BlockRecord record)
        {
            CadDocument document = record.Document;
            double scaleValue = document?.Header?.DimensionScaleFactor ?? 1.0;

            List<Entity> entities2D = new List<Entity>();
            List<Entity> entities3D = new List<Entity>();

            foreach (var entity in record.Entities)
            {
                if (entity is Insert)
                    continue;

                if (DWGEntities.IsEntity2D(entity))
                    entities2D.Add(entity);
                else
                    entities3D.Add(entity);
            }

            if (entities2D.Count > 0)
            {
                uint part2DIndex = CreatePart(gimData, folderIndex, scaleValue, true);
                foreach (var entity in entities2D)
                    Dispatch2DEntity(gimData, part2DIndex, entity);
            }

            if (entities3D.Count > 0)
            {
                uint part3DIndex = CreatePart(gimData, folderIndex, scaleValue, false);
                foreach (var entity in entities3D)
                    Dispatch2DEntity(gimData, part3DIndex, entity);
            }

            foreach (var entity in record.Entities.OfType<Insert>())
            {
                uint partIndex = CreatePart(gimData, folderIndex, scaleValue, true);
                Dispatch2DEntity(gimData, partIndex, entity);
            }
        }

        private static uint CreatePart(GimData gimData, uint parentIndex, double scale, bool is2D)
        {
            uint partIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_part });
            gimData.AddChildToParent(partIndex, parentIndex);

            uint repIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(new GimElement
            {
                ElementType = is2D ? GimElementType.R_2DRep : GimElementType.R_BRep,
                ElementDoubles = new[] { scale }
            });
            gimData.AddChildToParent(repIndex, partIndex);
            return repIndex;
        }

        public static void Dispatch2DEntity(GimData gimData, uint bRepIndex, Entity entity)
        {
            switch (entity.ObjectType)
            {
                case ObjectType.INSERT:
                    if (entity is Insert insert)
                        DWG2DBlockReader.ReadInsert(gimData, bRepIndex, insert);
                    return;

                case ObjectType.ARC:
                    if (entity is Arc arc)
                        DWG2DCurveReader.ReadArc(gimData, bRepIndex, arc, true);
                    break;

                case ObjectType.CIRCLE:
                    if (entity is Circle circle)
                        DWG2DCurveReader.ReadCircle(gimData, bRepIndex, circle, true);
                    break;

                case ObjectType.LINE:
                    if (entity is Line line)
                        DWG2DCurveReader.ReadLine(gimData, bRepIndex, line, true);
                    break;

                case ObjectType.POINT:
                    if (entity is Point point)
                        DWG2DBlockReader.ReadPoint(gimData, bRepIndex, point);
                    break;

                case ObjectType.SOLID:
                    if (entity is Solid solid)
                        DWG2DSurfaceReader.ReadSolid(gimData, bRepIndex, solid, addMaterial: true);
                    break;

                case ObjectType.ELLIPSE:
                    if (entity is Ellipse ellipse)
                        DWG2DCurveReader.ReadEllipse(gimData, bRepIndex, ellipse, true);
                    break;

                case ObjectType.SPLINE:
                    if (entity is Spline spline)
                        DWG2DCurveReader.ReadSpline(gimData, bRepIndex, spline, true);
                    break;

                case ObjectType.RAY:
                    if (entity is Ray ray)
                        DWG2DCurveReader.ReadRay(gimData, bRepIndex, ray);
                    break;

                case ObjectType.XLINE:
                    if (entity is XLine xline)
                        DWG2DCurveReader.ReadXLine(gimData, bRepIndex, xline);
                    break;

                case ObjectType.MLINE:
                    if (entity is MLine mline)
                        DWG2DSurfaceReader.ReadMLine(gimData, bRepIndex, mline);
                    break;

                case ObjectType.POLYLINE_2D:
                    if (entity is Polyline2D polyline2D)
                        DWG2DCurveReader.ReadPolyline2D(gimData, bRepIndex, polyline2D, true);
                    break;

                case ObjectType.LWPOLYLINE:
                    if (entity is LwPolyline lwPolyline)
                        DWG2DCurveReader.ReadLwPolyline(gimData, bRepIndex, lwPolyline);
                    break;

                case ObjectType.HATCH:
                    if (entity is Hatch hatch)
                        DWG2DSurfaceReader.ReadHatch(gimData, bRepIndex, hatch);
                    break;

                case ObjectType.VIEWPORT:
                    if (entity is Viewport viewport)
                        DWG2DBlockReader.ReadViewport(gimData, bRepIndex, viewport);
                    break;

                case ObjectType.MTEXT:
                    if (entity is MText mText)
                        DWG2DTextReader.ReadMText(gimData, bRepIndex, mText);
                    break;

                case ObjectType.TEXT:
                    if (entity is TextEntity text)
                        DWG2DTextReader.ReadText(gimData, bRepIndex, text);
                    break;

                case ObjectType.LEADER:
                    if (entity is Leader leader)
                        DWG2DLeaderReader.ReadLeader(gimData, bRepIndex, leader);
                    break;

                case ObjectType.TOLERANCE:
                    if (entity is Tolerance tolerance)
                        DWG2DTextReader.ReadTolerance(gimData, bRepIndex, tolerance);
                    break;

                case ObjectType.UNLISTED:
                    if (entity is MultiLeader multiLeader)
                        DWG2DMultiLeaderReader.ReadMultiLeader(gimData, bRepIndex, multiLeader);
                    break;

                case ObjectType.DIMENSION_ORDINATE:
                case ObjectType.DIMENSION_LINEAR:
                case ObjectType.DIMENSION_ALIGNED:
                case ObjectType.DIMENSION_ANG_2_Ln:
                case ObjectType.DIMENSION_DIAMETER:
                case ObjectType.DIMENSION_RADIUS:
                case ObjectType.DIMENSION_ANG_3_Pt:
                    if (entity is Dimension dimension)
                        DWG2DDimensionReader.ReadDimension(gimData, bRepIndex, dimension);
                    break;
            }
        }
    }
    
}
