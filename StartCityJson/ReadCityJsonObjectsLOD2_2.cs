using GIMcompress;
using GIMcompress.Shrinkwrap;
using GIMtransfer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Drawing.Printing;
using TriangleNet.Geometry;
using TriangleNet.Rendering.Text;
using TriangleNet.Meshing;
using System.Text;
using CityJson;
using System.Drawing;
using System.Runtime.Intrinsics.X86;
using System.Linq;

namespace StartCityJson
{
    public static class ReadCityJsonObjectsLOD2_2
    {

        public struct Read2_2Options 
        {
            public Read2_2Options() { }
            public bool Write1_2 { get; set; } = true;
            public bool Write2_2 { get; set; } = true;
        }

        #region faceTypes

        public const string FACE_TYPE_GROUND_SURFACE = "GroundSurface";
        public const string FACE_TYPE_WALL_SURFACE = "WallSurface";
        public const string FACE_TYPE_ROOF_SURFACE = "RoofSurface";

        #endregion

        public static Dictionary<string, int> NumericStats = new();
        public static Dictionary<string, string> ObjectSerialNameToID = new();
        public static Dictionary<string, string> UnmarkedObjects = new();
        // ↓ dictionary for: x, y, z, rotation yaw 
        public static Dictionary<string, Tuple<int, int, int, int>> ObjectInstanceRotationAndPosition = new();

        // how much difference between instance ID and definition (def > instance because of signs)
        const int InstanceToDefIDOffset = 154;

        internal static int[] ObjectIDGaps; 
        internal static int currentObjectCount = 0;
        internal static List<List<RegionBoundaries>> regionBoundariesMatrix;

        static int NotFoundCount = 0;
        public static void InitStats()
        {
            NumericStats["Building"] = 0;
            NumericStats["BuildingPart"] = 0;

            NumericStats["has parents"] = 0;
            NumericStats["has children"] = 0;

            NumericStats["lod0"] = 0;
            NumericStats["lod1.2"] = 0;
            NumericStats["lod1.3"] = 0;
            NumericStats["lod2.2"] = 0;

            NumericStats["total"] = 0;
        }
        internal static void ReadObjects(string fileName, bool createFiles, ref int startID, Read2_2Options readOptions)
        {
            using (StreamReader file = File.OpenText(fileName))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JObject jCityJson = (JObject)JToken.ReadFrom(reader);

                double[] transform = GetTransform(jCityJson);
                double[] scale = GetScale(jCityJson);

                var metadata = jCityJson[ReadJsonStream.ATTRIBUTE_METADATA];

                // reading the verticies from the file
                List<double[]> vertices = GetVertices(jCityJson, scale, transform);

                // reading the cityobjects from the file
                GetObjects(jCityJson, vertices, createFiles, ref startID, readOptions);

            }
        }

        internal static void GetObjectIDsAndOrientation(string fileName, CityJsonDBContext db)
        {
            using StreamReader file = File.OpenText(fileName);
            using JsonTextReader reader = new JsonTextReader(file);
            
            JObject jCityJson = (JObject)JToken.ReadFrom(reader);

            double[] transform = GetTransform(jCityJson);
            double[] scale = GetScale(jCityJson);

            var metadata = jCityJson[ReadJsonStream.ATTRIBUTE_METADATA];

            List<double[]> vertices = GetVertices(jCityJson, scale, transform);

            JToken cityObjects = jCityJson[ReadJsonStream.ATTRIBUTE_CITY_OBJECTS];
            foreach (JProperty jsonChildObjectProperty in cityObjects)
            {
                var jsonChildObject = jsonChildObjectProperty.Value;
                string objectName = jsonChildObjectProperty.Name;

                string type = jsonChildObject["type"].Value<string>();
                if (type == "Building")
                {
                    // we add the current building to the completion dictionary
                    string objectSerialName = objectName.Split(".")[^1];

                    var result = db.ObjectDefinitions.Where(x => x.Name == objectSerialName).FirstOrDefault();
                    if (result != null)
                    {
                        string ID = result.ID.ToString();
                        ObjectSerialNameToID[objectSerialName] = ID;

                        var result2 = db.ObjectInstances.Where(x => x.DefID == result.ID).FirstOrDefault();
                        if (result2 != null)
                        {
                            int x = result2.Px;
                            int y = result2.Py;
                            int z = result2.Pz;
                            int rotation = result2.Ry;

                            ObjectInstanceRotationAndPosition[objectSerialName] = new(x, y, z, rotation);
                        }
                        else
                        {
                            ObjectInstanceRotationAndPosition[objectSerialName] = new(0, 0, 0, 0);
                        }
                    }
                    else
                    {
                        ObjectSerialNameToID[objectSerialName] = "NOT FOUND";
                        ObjectInstanceRotationAndPosition[objectSerialName] = new(0, 0, 0, 0);

                    }

                }
            }
        }

        internal static void ImportObjectNamesAndRotations(string importPath)
        {
            Dictionary<string, string> nameToId = new();
            Dictionary<string, Tuple<int, int, int, int>> nameToPositions = new();
            string[] lines = File.ReadAllLines(importPath);
            for (int i = 1; i < lines.Length; i++) 
            {
                string line = lines[i];
                string[] keywords = line.Split(';');
                string name = keywords[0];
                string id = keywords[1];
                if(id.ToLower() == "not found")
                {
                    continue;
                }
                int x = int.Parse(keywords[2]);
                int y = int.Parse(keywords[3]);
                int z = int.Parse(keywords[4]);
                int ry = int.Parse(keywords[5]);
                nameToId[name] = id;
                nameToPositions[name] = new(x, y, z, ry);
            }

            ObjectSerialNameToID = nameToId;
            ObjectInstanceRotationAndPosition = nameToPositions;
        }

        internal static void ImportIDGaps(string importPath)
        {
            List<int> IDs = new List<int>(11000000);
            string[] lines = File.ReadAllLines(importPath);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int ID = int.Parse(line.Replace("\"", ""));
            }

            ObjectIDGaps = IDs.ToArray();
        }



        internal static void ImportMissingObjectNames(string importPath)
        {
            Dictionary<string, string> nameToId = new();
            string[] lines = File.ReadAllLines(importPath);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] keywords = line.Split(';');
                string name = keywords[0];
                string id = keywords[1];
                if (id.ToLower() == "not found")
                {
                    nameToId[name] = name;
                }
            }

            UnmarkedObjects = nameToId;
        }

        internal static void ExportObjectNamesAndRotations(string exportPath)
        {
            StringBuilder result = new();
            result.AppendLine("\"Serial\";\"Name\";\"x\";\"y\";\"z\";\"ry\"");

            static string Stringify(object obj)
            {
                return obj.ToString();
            }

            foreach (var keyval in ObjectSerialNameToID)
            {
                string serial = keyval.Key;
                string ID = keyval.Value;

                var tuple = ObjectInstanceRotationAndPosition[serial];
                int x = tuple.Item1;
                int y = tuple.Item2;
                int z = tuple.Item3;
                int ry = tuple.Item4;

                string[] arrValues = new string[]
                {
                    Stringify(serial),
                    Stringify(ID),
                    Stringify(x),
                    Stringify(y),
                    Stringify(z),
                    Stringify(ry)
                };


                result.AppendLine(string.Join(";", arrValues));
            }

            string finalString = result.ToString();

            File.WriteAllText(exportPath, finalString);
        }


        internal static void GetObjects(JObject jCityJson, List<double[]> vertices, bool createFiles,
            ref int startingInstanceID, Read2_2Options readOptions)
        {
            JToken cityObjects = jCityJson[ReadJsonStream.ATTRIBUTE_CITY_OBJECTS];

            //GetObjectStats(cityObjects);

            // this dictionary will track if a single building is complete:
            // accessing the dict by building name returns a subdictionary with all the children of the building
            // and whether they've been read yet or not (true = read). If a building has been read successfully
            // in its entirety, that building will be removed from the dictionary

            Dictionary<string, Dictionary<string, bool>> buildingCompletion = new();
            Dictionary<string, GimData> buildingGimDatasLOD22 = new();
            Dictionary<string, GimData> buildingGimDatasLOD12 = new();
            Dictionary<string, Tuple<int, Vect>> buildingTransformations = new();

            int instanceID = startingInstanceID;
            int definitionID = instanceID + InstanceToDefIDOffset;

            foreach(JProperty jsonChildObjectProperty in cityObjects)
            {
                var jsonChildObject = jsonChildObjectProperty.Value;
                string objectName = jsonChildObjectProperty.Name;

                string type = jsonChildObject["type"].Value<string>();
                if (type == "Building")
                {
                    // we add the current building to the completion dictionary
                    buildingCompletion[objectName] = new();
                    string objectSerialName = objectName.Split(".")[^1];
                    GimData buildingGimData = new()
                    {
                        elements = new()
                        {
                            new()
                            {
                                elementType = GimElementType.D_root,
                                elements = new(){ 1 }
                            },
                            new()
                            {
                                elementType = GimElementType.D_folder,
                                locals = new(){ new GimLocal() { parent = 0 } }
                            }
                        },
                        objects = new()
                        {
                            new()
                            {
                                name = objectName,
                                element = 0,
                            }
                        }
                    };

                    var jChildren = jsonChildObject["children"];

                    buildingGimDatasLOD22[objectName] = buildingGimData;
                    buildingGimDatasLOD12[objectName] = buildingGimData.Clone();

                    if (jChildren != null)
                    {
                        var children = jChildren.Values<string>();
                        foreach (string childID in children)
                        {
                            buildingCompletion[objectName][childID] = false;
                        }
                    }
                }
                else if (type == "BuildingPart")
                {
                    var jParents = jsonChildObject["parents"];
                    if (jParents == null) continue;

                    GimData buildingPartGimData22 = new()
                    {
                        elements = new()
                            {
                                new()
                                {
                                    elementType = GimElementType.D_root,
                                    elements = new(){ 1 }
                                },
                                new()
                                {
                                    elementType = GimElementType.D_part,
                                    elements = new() { 2 },
                                    locals = new(){ new() { parent = 0 } }
                                },
                                new()
                                {
                                    elementType = GimElementType.R_Poly,
                                    locals = new() { new() { parent = 1 } }
                                }
                            },

                        objects = new()
                            {
                                new()
                                {
                                    element = 0,
                                    name = objectName,
                                },
                                new()
                                {
                                    element = 1,
                                    name = objectName,
                                }
                            }
                    };

                    GimData buildingPartGimData12 = buildingPartGimData22.Clone();

                    // populate gimData
                    var jGeometry = jsonChildObject["geometry"];
                    if (objectName == "NL.IMBAG.Pand.0202100000211842-0")
                    {
                        string debug = "debust";
                    }
                    GetObjectGemetry(jGeometry, vertices, buildingPartGimData22, buildingPartGimData12);

                    var parents = jParents.Values<string>();

                    // look for parent/parents
                    foreach (string parentName in parents)
                    {
                        // mark this child as read in the parent
                        buildingCompletion[parentName][objectName] = true;
                        bool parentComplete = true;

                        // we update the overall parent gimData
                        GimData parentGimData22 = buildingGimDatasLOD22[parentName];
                        parentGimData22.AddSecondaryGimData(buildingPartGimData22, 1, 1, 0);

                        GimData parentGimData12 = buildingGimDatasLOD12[parentName];
                        parentGimData12.AddSecondaryGimData(buildingPartGimData12, 1, 1, 0);

                        // we check if, after reading this child, we completed the parent
                        foreach (var value in buildingCompletion[parentName].Values)
                        {
                            if (!value)
                            {
                                // one child is still incomplete = parent is still incomplete
                                parentComplete = false;
                                break;
                            }
                        }

                        if (parentComplete)
                        {
                            string folderID;
                            string objectSerialName = parentName.Split(".")[^1];

                            string ID = definitionID.ToString();
                            int folderNumber = int.Parse(ID) / 10000;
                            folderID = folderNumber.ToString();

                            string exportName22 = ID + "_22";
                            string exportName12 = ID + "_12";
                            GIMcityjsonPosition positionData;
                            CenterObjectAndGetPosition(parentGimData12, parentGimData22, out positionData);
                            int minX = (int)positionData.MinX;
                            int minY = (int)positionData.MinY;
                            int maxX = (int)positionData.MaxX;
                            int maxY = (int)positionData.MaxY;

                            double minZ = positionData.MinZ;
                            double ry = positionData.Ry;
                            double centerX = positionData.CenterX;
                            double centerY = positionData.CenterY;


                            (GimUnitSize nearestMinUnit, GimUnitSize nearestMaxUnit) = GetMinMaxGimUnit(minX, minY, maxX, maxY);


                            var objDef = new ObjectDefinition()
                            {
                                ID = definitionID,
                                Name = parentName,
                                Type = "building",
                                OID = null,
                                PID = null,
                                minGIMunit = nearestMinUnit.ToByte(),
                                maxGIMunit = nearestMaxUnit.ToByte(),

                            };

                            ReadCityJson.objectDefinitions.Add(objDef
                            );

                            int GIMminlat = ReadCityJson.RdToLat(minX, minY),
                                GIMmaxlat = ReadCityJson.RdToLat(maxX, maxY),
                                GIMminlong = ReadCityJson.RdToLong(minX, minY),
                                GIMmaxlong = ReadCityJson.RdToLong(maxX, maxY);

                            int GIMcenterLong = (GIMminlong + GIMmaxlong) / 2;
                            int GIMcenterLat = (GIMminlat + GIMmaxlat) / 2;

                            string RegionID =
                               GeographicalHelpers.GetRegionIDContainingObjectXY(regionBoundariesMatrix, centerX, centerY);

                            ObjectInstance objInst = new()
                            {
                                Name = parentName,
                                RegionID = RegionID, // code of the file in 6 chars
                                ID = instanceID,
                                DefID = definitionID,
                                Px = (int)(centerX * 1000),
                                Py = (int)(centerY * 1000),
                                Pz = (int)(minZ * 1000),
                                Ry = (int)Math.Round(ry * 572957795), // value to be expected between -1.800.000.000 and 1.800.000.000
                                GIMminlat = GIMminlat,
                                GIMmaxlat = GIMmaxlat,
                                GIMminlong = GIMminlong,
                                GIMmaxlong = GIMmaxlong,
                                M = false,
                            };
                            ReadCityJson.objectInstances.Add(objInst);

                            if (createFiles)
                            {
                                string fullDirectory = GIMtransfer.FileAccess.GetTempPath(folderID);
                                var dirInfo = Directory.CreateDirectory(fullDirectory);
                                string dirName = dirInfo.FullName;

                                if (readOptions.Write1_2)
                                {
                                    string fileName12 = dirName + "\\" + exportName12;
                                    byte[] bytes12 = GIMtransfer.FileAccess.GetGimDataAsByteArrayGLB(parentGimData12);
                                    File.WriteAllBytes(fileName12 + ".glb", bytes12);
                                }

                                if (readOptions.Write2_2)
                                {
                                    string fileName22 = dirName + "\\" + exportName22;
                                    byte[] bytes22 = GIMtransfer.FileAccess.GetGimDataAsByteArrayGLB(parentGimData22);
                                    File.WriteAllBytes(fileName22 + ".glb", bytes22);
                                }
                            }

                            buildingCompletion.Remove(parentName);
                            buildingGimDatasLOD22.Remove(parentName);
                            buildingGimDatasLOD12.Remove(parentName);

                            instanceID++;
                            definitionID++;
                        }
                    }
                
                }
            }
            startingInstanceID = instanceID;
        }

        internal static void GroupRegionBoundaries(List<RegionBoundaries> regionBoundaries)
        {
            //int minLong = int.MaxValue, minLat = int.MaxValue, maxLong = int.MinValue, maxLat = int.MinValue;

            Dictionary<int, List<RegionBoundaries>> xToRegionsDict = new();
            foreach (RegionBoundaries regionBoundary in regionBoundaries)
            {

                int x = (regionBoundary.minX + regionBoundary.maxX) / 2;
                int y = (regionBoundary.minY + regionBoundary.maxY) / 2;
                if (!xToRegionsDict.ContainsKey(x))
                {
                    xToRegionsDict[x] = new();
                }
                xToRegionsDict[x].Add(regionBoundary);

                regionBoundary.centerX = x;
                regionBoundary.centerY = y;
                //if (x < minLong) minLong = x;
                //if (y < minLat) minLat = y;
                //if (x > maxLong) maxLong = x;
                //if (y > maxLat) maxLat = y;

            }

            List<List<RegionBoundaries>> regionsByX = new();
            foreach(var xRegionSet in xToRegionsDict)
            {
                regionsByX.Add(xRegionSet.Value);
            }

            // we sort the big list by X
            regionsByX.Sort((List<RegionBoundaries> a, List<RegionBoundaries> b) =>
            {
                if (a[0].centerX > b[0].centerX) return 1;
                if (a[0].centerX < b[0].centerX) return -1;
                return 0;
            });

            // we sort the smaller inner lists by Y
            foreach(var regionList in regionsByX)
            {
                regionList.Sort((RegionBoundaries a, RegionBoundaries b) =>
                {
                    if (a.centerY > b.centerY) return 1;
                    if (a.centerY < b.centerY) return -1;
                    return 0;
                });
            }


            int count = 0;

            regionBoundariesMatrix= regionsByX;
            foreach (var regionArr in regionBoundariesMatrix)
                foreach (var region in regionArr)
                    if (region != null)
                        count++;

            return;

        }


        public static void GetObjectGemetry(JToken jGeometry, List<double[]> vertices, 
            GimData buildingPartGimData22, GimData buildingPartGimData12)
        {

            foreach (var jGeometryLOD in jGeometry)
            {
                //JToken jGeometryLOD = jGeometryLODProp.Value;
                string lodLevel = jGeometryLOD["lod"].Value<string>();
                if (lodLevel == "2.2")
                {
                    var jBoundaries = jGeometryLOD["boundaries"];
                    if (jBoundaries != null)
                    {
                        var jSemantics = jGeometryLOD["semantics"];

                        List<string> faceToType = new();
                        if(jSemantics != null)
                        {
                            List<string> uniqueFaceTypes = new();
                            var jSurfaces = jSemantics["surfaces"];
                            foreach(var jSurface in jSurfaces)
                            {
                                uniqueFaceTypes.Add(jSurface["type"].ToString());
                            }

                            var jValues = jSemantics["values"][0].Values<int>();
                            foreach(int faceValue in jValues)
                            {
                                faceToType.Add(uniqueFaceTypes[faceValue]);
                            }
                        }


                        double lowestZ = 10000000;
                        double highestZ = -10000000;

                        List<List<Vect>> allPolygonEdges = new();

                        // list of boundaries per building
                        foreach (JArray jBoundary in jBoundaries)
                        {
                            int faceID = 0;
                            // list of faces per boundary
                            foreach (JArray jFace in jBoundary)
                            {
                                List<Vect> facePoints = new();
                                List<Vect> outerPolygon = new();
                                List<List<Vect>> innerPolygons = new();

                                bool isOuterPoly = true;
                                // list of polygons per face
                                foreach (JArray jPolygon in jFace)
                                {
                                    if (!isOuterPoly)
                                    // we read the outer polygon, all the other polygons
                                    // are inner polygons
                                    {
                                        innerPolygons.Add(new());
                                    }
                                    // reading all points and adding them to either outer or inner polys
                                    foreach (int pointIndex in jPolygon)
                                    {
                                        Vect point = (Vect)vertices[pointIndex];
                                        point = point.Round(3);
                                        facePoints.Add(point);
                                        // belongs to the outer polygon
                                        if (isOuterPoly)
                                        {
                                            outerPolygon.Add(point);
                                        }
                                        else
                                        {
                                            innerPolygons[^1].Add(point);
                                        }
                                    }
                                    // after reading the first polygon, all the others are internal
                                    isOuterPoly = false;
                                }

                                allPolygonEdges.Add(outerPolygon);

                                if (facePoints.Count < 3) continue; // degenerate face

                                List<List<Vect>> nonDegenerateInnerPolygons = new();

                                // weeding out degenerate holes in the polygon, these break trianglenet
                                foreach(var innerBoundary in innerPolygons)
                                {
                                    double area = GimPolygon.ZPolygonArea(innerBoundary);
                                    if (area < Matrix.EQUALITY_MARGIN * 100) continue;
                                    nonDegenerateInnerPolygons.Add(innerBoundary);
                                }

                                innerPolygons = nonDegenerateInnerPolygons;

                                // we're going to calculate the planes direction vectors to flatten the points to 2D
                                Vect origin = facePoints[0];
                                // for the first direction, we will pick the longest length
                                Vect directionA = new Vect(0, 0, 0);
                                double maxLen = 0; 
                                for (int i = 0; i < facePoints.Count; i++)
                                {
                                    int firstIndex = (i - 1 + facePoints.Count) % facePoints.Count;
                                    int lastIndex = i;
                                    Vect segment = facePoints[lastIndex] - facePoints[firstIndex];
                                    // logging highest Z value
                                    if (facePoints[i][2] >= highestZ)
                                    {
                                        highestZ = facePoints[i][2];
                                    }

                                    if(maxLen < segment.Norm)
                                    {
                                        directionA = segment;
                                        maxLen = segment.Norm;
                                    }
                                }

                                directionA = directionA.Direction;

                                if (directionA.Norm == 0) continue; // degenerate polygon

                                // we want to calculate the vector B: the vector most perpendicular to direction A
                                Vect directionB = new Vect(0, 0, 0);
                                bool degenerate = true;
                                // maximum perpendicularity coefficient: we want directionB to be as less parallel with A, to guarantee the most precise normal 
                                double maxCoeff = 0;
                                for (int i = 0; i < facePoints.Count; i++)
                                {
                                    int firstIndex = (i - 1 + facePoints.Count) % facePoints.Count;
                                    int lastIndex = i;
                                    Vect segment = facePoints[lastIndex] - facePoints[firstIndex];
                                    double coeff = (segment ^ directionA).Norm;
                                    if(coeff > maxCoeff)
                                    {
                                        maxCoeff = coeff;
                                        directionB = segment;
                                    }
                                }

                                if (!directionB.IsCollinearTo(directionA))
                                {
                                    degenerate = false;
                                }

                                directionB = (directionB - (directionA * directionB) * directionA).Direction;

                                if (directionB.Norm == 0 || degenerate) continue; // degenerate polygon

                                if (outerPolygon.Count > 3) // don't simplify triangles
                                {
                                    GimPolygon gimPoly = new(outerPolygon, innerPolygons);
                                    if (gimPoly.IsDegenerate()) 
                                        continue; // degenerate polygon
                                    gimPoly.SimplifyBoundaries();
                                    outerPolygon = gimPoly.OuterBoundary;
                                    innerPolygons = gimPoly.InnerBoundaries;
                                }
                                else if (outerPolygon.Count == 3) // if the triangle is very thin, best to get rid of it
                                {
                                    Vect AB = outerPolygon[1] - outerPolygon[0];
                                    Vect AC = outerPolygon[2] - outerPolygon[0];
                                    Vect BC = outerPolygon[2] - outerPolygon[1];

                                    double area = 0.5 * (AB ^ AC).Norm;
                                    double minEdge = Math.Min(AB.Norm, Math.Min(AC.Norm, BC.Norm));
                                    if(area < 0.03 || minEdge <= 0.01) // basically a degenerate triangle
                                    {
                                        continue;
                                    }
                                }
                            
                                if (outerPolygon.Count < 3) continue; //degenerate polygon

                                // checking if this is the lowest face

                                double avgZ = 0;

                                foreach(Vect point in outerPolygon)
                                {
                                    avgZ += point[2];

                                }

                                avgZ /= outerPolygon.Count;
                                bool isLowerPolygon = false;
                                if(avgZ < lowestZ)
                                {
                                    lowestZ = avgZ;
                                    isLowerPolygon = true;
                                }


                                // constructing faces for triangle library

                                List<Vertex2d> outerVertices = new();
                                List<List<Vertex2d>> innerVertices = new();

                                Dictionary<Vertex2d, Vect> vertex2dTo3d = new();
                                Dictionary<Vect, Vertex2d> vect3dTo2d = new();

                                foreach (Vect outerPoint in outerPolygon)
                                {
                                    double x = (outerPoint - origin) * directionA;
                                    double y = (outerPoint - origin) * directionB;
                                    Vertex2d vertex = new(x, y);
                                    outerVertices.Add(vertex);

                                    vertex2dTo3d[vertex] = outerPoint;
                                    vect3dTo2d[outerPoint] = vertex;
                                }
                                foreach (var innerPolygon in innerPolygons)
                                {
                                    List<Vertex2d> innerVertexList = new();
                                    foreach (Vect innerPoint in innerPolygon)
                                    {
                                        double x = (innerPoint - origin) * directionA;
                                        double y = (innerPoint - origin) * directionB;
                                        Vertex2d vertex = new(x, y);
                                        innerVertexList.Add(vertex);
                                        vertex2dTo3d[vertex] = innerPoint;
                                        vect3dTo2d[innerPoint] = vertex;
                                    }
                                    innerVertices.Add(innerVertexList);
                                }

                                bool print = false;
                                if(innerVertices.Count > 0)
                                {
                                    string breakpoint = "debug";
                                    print = false;
                                }

                                var mesh = ConvertPolygonToTriangles(outerVertices, innerVertices, print);
                                GimElement polyElement = Convert2DMeshTo3D(mesh, origin, directionA, directionB);

                                uint polyIndex = (uint)buildingPartGimData22.Elements.Count;
                                buildingPartGimData22.Elements.Add(polyElement);
                                buildingPartGimData22.AddChildToParent(polyIndex, 2); // 2 is the r_poly element

                                if(faceToType.Count > faceID)
                                {
                                    string faceType = faceToType[faceID];
                                    string faceColorHex = "";

                                    switch (faceType)
                                    {
                                        case FACE_TYPE_GROUND_SURFACE: faceColorHex = "808080";break;
                                        case FACE_TYPE_ROOF_SURFACE: faceColorHex = "c70000"; break;
                                        case FACE_TYPE_WALL_SURFACE: faceColorHex = "a0a0a0";break;
                                        default: faceColorHex = "A0A0A0"; break;
                                    }


                                    int color = int.Parse(faceColorHex, System.Globalization.NumberStyles.HexNumber);
                                    //int color = GimColour.FromHexString(faceColorHex).ToInt();

                                    GimMaterial gimMaterial = new()
                                    {
                                        BaseColour = color,
                                        DiffuseColour = color,
                                        SpecularColour = 255 << (6 * 4),
                                        EmissionColour = 0,
                                        AmbientColour = new GimColour(1, 0, 0, 0).ToInt(),
                                        ElementType = GimElementType.Material
                                    };

                                    uint gimMaterialIndex = (uint)buildingPartGimData22.Elements.Count;
                                    buildingPartGimData22.Elements.Add(gimMaterial);
                                    buildingPartGimData22.AddChildToParent(gimMaterialIndex, polyIndex);

                                }

                                faceID++;
                            }

                        }

                        List<Vect> lowerFacePoints = new();
                        GimElement lowerFaceElement = null;

                        lowerFacePoints = Get2DProfileOfBuilding(allPolygonEdges, lowestZ);
                        lowerFacePoints = GimPolygon.SimplifyBoundary(lowerFacePoints);
                        List<Vertex2d> lowerFace2DVertices = new();
                        foreach(Vect point in lowerFacePoints)
                        {
                            lowerFace2DVertices.Add(new(point[0], point[1]));
                        }

                        var lowerFaceMesh = ConvertPolygonToTriangles(lowerFace2DVertices, new(), false);
                        lowerFaceElement = Convert2DMeshTo3D(lowerFaceMesh, new(0, 0, lowestZ), new(1, 0, 0), new(0, 1, 0));

                        // after finding the lowest face for LOD 1.2, we will now construct the walls
                        // the way we do this is for every segment in this face, we project it up to the highest Z
                        // value.

                        if (lowerFaceElement == null) continue;
                        lowerFaceElement.Locals = new();
                        lowerFaceElement.Elements = new();

                        GimElement wallsElement = new() { ElementType = GimElementType.P_polygon };
                        Dictionary<Vect, int> pointToIndex = new();
                        List<double> elementDoubles = new();
                        List<int> elementInts = new();
                        // here we are constructiong the walls based on the lowest face
                        for (int i = 0; i < lowerFacePoints.Count; i++)
                        {
                            int currentIndex = i;
                            int nextIndex = (i + 1) % lowerFacePoints.Count;

                            Vect currentPoint = lowerFacePoints[currentIndex];
                            Vect nextPoint = lowerFacePoints[nextIndex];

                            Vect A = currentPoint;
                            Vect B = nextPoint;

                            // this is the structure of the wall, given A and B belong to the lower face:
                            // C ==> D
                            // ^     v
                            // ^     v
                            // B <== A

                            Vect C = new(
                                B[0],
                                B[1],
                                highestZ
                                );

                            Vect D = new(
                                A[0],
                                A[1],
                                highestZ
                                );

                            List<Vect> points = new() { A, B, C, D };
                            List<int> pointIndices = new();

                            //constructing elementDoubles 
                            foreach (Vect point in points)
                            {
                                int pointIndex;
                                if (!pointToIndex.ContainsKey(point))
                                {
                                    pointIndex = pointToIndex.Count;
                                    elementDoubles.Add(point[0]);
                                    elementDoubles.Add(point[1]);
                                    elementDoubles.Add(point[2]);

                                    pointToIndex[point] = pointIndex;
                                }
                                else
                                {
                                    pointIndex = pointToIndex[point];
                                }
                                pointIndices.Add(pointIndex);
                            }

                            int aIndex = pointIndices[0], bIndex = pointIndices[1],
                                cIndex = pointIndices[2], dIndex = pointIndices[3];

                            // constructing elementInts
                            elementInts.Add(aIndex);
                            elementInts.Add(bIndex);
                            elementInts.Add(dIndex);
                            elementInts.Add(cIndex);
                            elementInts.Add(dIndex);
                            elementInts.Add(bIndex);

                        }

                        wallsElement.ElementDoubles = elementDoubles.ToArray();
                        wallsElement.ElementInts = elementInts.ToArray();

                        // now we add the roof. We do this by copying the floor and setting all Z-values to be
                        // the highest value
                        GimElement roofElement = lowerFaceElement.Clone();
                        for (int i = 0; (i + 2) < roofElement.ElementDoubles.Length; i += 3)
                        {
                            roofElement.ElementDoubles[i + 2] = highestZ;
                        }

                        GimElement[] gimElements = new[] { lowerFaceElement, wallsElement, roofElement };

                        List<uint> polyIndices = new();

                        // we add the materials to the gimData, as children of the polyrep r_poly element
                        foreach (GimElement element in gimElements)
                        {
                            uint polyIndex = (uint)buildingPartGimData12.Elements.Count;
                            buildingPartGimData12.Elements.Add(element);
                            buildingPartGimData12.AddChildToParent(polyIndex, 2); // 2 is the r_poly element
                            polyIndices.Add(polyIndex);
                        }

                        // we add the material at the part level
                        {
                            string faceColorHex = "7f0000";
                            int color = int.Parse(faceColorHex, System.Globalization.NumberStyles.HexNumber);
                            //int color = GimColour.FromHexString(faceColorHex).ToInt();

                            GimMaterial gimMaterial = new()
                            {
                                BaseColour = color,
                                DiffuseColour = color,
                                SpecularColour = 255 << (6 * 4),
                                EmissionColour = 0,
                                AmbientColour = new GimColour(1, 0, 0, 0).ToInt(),
                                ElementType = GimElementType.Material
                            };

                            uint gimMaterialIndex = (uint)buildingPartGimData12.Elements.Count;
                            buildingPartGimData12.Elements.Add(gimMaterial);
                            buildingPartGimData12.AddChildToParent(gimMaterialIndex, 1);
                        }
                    }
                }
            }
        }

        private static List<Vect> Get2DProfileOfBuilding(List<List<Vect>> allFaces, double lowestZ)
        {
            List<Vect> profile2D = new();

            HashSet<Vertex2d> vertices = new();
            foreach (var face in allFaces)
            {
                for (int i = 0; i < face.Count; i++) 
                {
                    int index = i;
                    Vect point = face[index];
                    Vertex2d vertex = new(Math.Round(point[0], 4), Math.Round(point[1], 4));
                    vertices.Add(vertex);
                }
            }

            Vect averageVertex = new(0, 0);
            foreach(Vertex2d vertex in vertices)
            {
                averageVertex += (Vect)vertex;
            }

            averageVertex /= vertices.Count;

            if (true)
            {
                string debug = "DEBUG";
                string pointString = GimPolygon.Vertex2dListToString(vertices, "L=[", "]");
            }

            foreach (var face in allFaces)
            {
                for (int i = 0; i < face.Count; i++)
                {
                    int index = i;
                    int nextIndex = (i + 1) % face.Count;
                    Vect point = face[index];
                    Vect nextPoint = face[nextIndex];
                    Vertex2d point2D = new(point[0], point[1]);
                    Vertex2d vertex;
                    vertices.TryGetValue(point2D, out vertex);

                    Vertex2d nextPoint2D = new(nextPoint[0], nextPoint[1]);
                    Vertex2d nextVertex;
                    vertices.TryGetValue(nextPoint2D, out nextVertex);

                    vertex.AddConnectedPoint(nextVertex);
                    nextVertex.AddConnectedPoint(vertex);
                }
            }
            Vertex2d furthestVertex = vertices.First();
            double longestDistance = 0;
            foreach(Vertex2d vertex in vertices)
            {
                double distance = ((Vect)vertex - averageVertex).Norm;
                if (distance >= longestDistance)
                {
                    longestDistance = distance;
                    furthestVertex = vertex;
                }
            }
            Vect direction = ((Vect)furthestVertex - averageVertex).Direction;
            HashSet<Vertex2d> visitedVertices = new();
            Vertex2d currentVertex = furthestVertex;

            double pi = Math.PI;
            Vertex2d previousVertex = null;

            while (!visitedVertices.Contains(currentVertex))
            {
                Vertex2d nextVertex = null;
                Vect nextDirection = null;
                double smallestAngle = 2 * pi;
                foreach(Vertex2d connectedVertex in currentVertex.ConnectedVertices)
                {
                    if (connectedVertex == previousVertex) continue;
                    Vect differenceDirection = ((Vect)connectedVertex - (Vect)currentVertex).Direction;
                    double dot = direction * differenceDirection;
                    double cross = direction[0] * differenceDirection[1] - direction[1] * differenceDirection[0];
                    double angle = (Math.Atan2(cross, dot) + 2 * pi) % (2 * pi);

                    if(angle < smallestAngle)
                    {
                        smallestAngle = angle;
                        nextVertex = connectedVertex;

                        nextDirection = -differenceDirection;
                    }

                }

                if (visitedVertices.Contains(currentVertex) && visitedVertices.Contains(nextVertex)) break;
                profile2D.Add(new(currentVertex.X, currentVertex.Y, lowestZ));
                visitedVertices.Add(currentVertex);
                if (nextVertex == null) break;
                previousVertex = currentVertex;
                currentVertex = nextVertex;
                direction = nextDirection;

            }


            return profile2D;
        }


        // converting a mesh of 2D points to a 3D mesh (gimElement)
        private static GimElement Convert2DMeshTo3D(IMesh mesh, Vect origin, Vect directionA, Vect directionB)
        {
            GimElement polyElement = new() { ElementType = GimElementType.P_polygon };

            List<int> elementInts = new();
            List<double> elementDoubles = new();
            Dictionary<Vect, int> pointToIndex = new();
            int pointCount = 0;
            foreach(var triangle in mesh.Triangles)
            {
                for (int i = 0; i < 3; i++)
                {
                    var vertex = triangle.GetVertex(i);
                    double x = vertex.X;
                    double y = vertex.Y;

                    Vect point = origin + x * directionA + y * directionB;
                    
                    if (!pointToIndex.ContainsKey(point))
                    {
                        pointToIndex[point] = pointCount;
                        pointCount++;
                        elementDoubles.Add(point[0]);
                        elementDoubles.Add(point[1]);
                        elementDoubles.Add(point[2]);
                    }

                    int index = pointToIndex[point];

                    elementInts.Add(index);
                }
            }

            polyElement.ElementDoubles = elementDoubles.ToArray();
            polyElement.ElementInts = elementInts.ToArray();

            return polyElement;
        }

        private static IMesh ConvertPolygonToTriangles(List<Vertex2d> outerPoints, List<List<Vertex2d>> innerPoints, bool print = false)
        {
            Contour outerContour = null;
            List<Contour> innerContours = new();

            List<Vertex> outerContourVertices = new();
            foreach (Vertex2d outerPoint in outerPoints)
            {
                Vertex newVertex = new(outerPoint.X, outerPoint.Y);
                outerContourVertices.Add(newVertex);
            }
            outerContour = new(outerContourVertices);

            foreach (List<Vertex2d> innerPointList in innerPoints)
            {
                if (innerPointList.Count < 3) continue;
                List<Vertex> innerContourVertices = new();
                foreach (Vertex2d innerPoint in innerPointList)
                {
                    Vertex newVertex = new(innerPoint.X, innerPoint.Y);
                    innerContourVertices.Add(newVertex);
                }
                innerContours.Add(new(innerContourVertices));
            }

            Polygon polygon = new Polygon();
            polygon.Add(outerContour, false);
            foreach (var contour in innerContours)
            {
                polygon.Add(contour, true);
            }

            var mesh = polygon.Triangulate();

            if (print)
            {
                string path = GIMtransfer.FileAccess.GetTempPath("test.svg");
                SvgImage.Save(mesh, path, 400);
            }

            return mesh;
        }

        private static Tuple<GimUnitSize, GimUnitSize> GetMinMaxGimUnit(double minX, double minY, double maxX, double maxY)
        {
            // calculating bounding box

            double longestX = maxX - minX;
            double longestY = maxY - minY;

            double longestDim = longestX * 1000;
            if (longestY > longestDim) longestDim = longestY * 1000;

            if (longestDim == 0) return new(new(0), new(0));

            double dimFraction = longestDim / 100000;
            GimUnitSize nearestMinUnit = new();
            GimUnitSize nearestMaxUnit = new();

            for (byte b = 0; b <= 127; b++)
            {
                GimUnitSize currentUnit = new(b);

                if (currentUnit.GimUnit.IsMetric())
                {
                    if (currentUnit.GetRoundingStep() >= dimFraction)
                    {
                        nearestMinUnit = currentUnit;
                        break;
                    }
                }
            }

            for (byte b = 127; b >= 0 && b < 128; b--)
            {
                GimUnitSize currentUnit = new(b);

                if (currentUnit.GimUnit.IsMetric())
                {
                    if (currentUnit.GetRoundingStep() <= longestDim / 2)
                    {
                        nearestMaxUnit = currentUnit;
                        break;
                    }
                }

            }

            return new(nearestMinUnit, nearestMaxUnit);

        }
        private static void CenterObjectAndGetPosition(GimData gimData12, GimData gimData22, out GIMcityjsonPosition position)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity,
                maxX = double.NegativeInfinity, maxY = double.NegativeInfinity,
                minZ = double.PositiveInfinity;

            int pointCount = 0;
            double avgX = 0;
            double avgY = 0;
            for (int elemIndex = 0; elemIndex < gimData12.Elements.Count; elemIndex++) 
            {
                GimElement element = gimData12.Elements[elemIndex];
                if(element.ElementType == GimElementType.P_polygon)
                {
                    for (int i = 0; i + 2 < element.ElementDoubles.Length; i+=3)
                    {
                        double x = element.ElementDoubles[i];
                        double y = element.ElementDoubles[i + 1];
                        double z = element.ElementDoubles[i + 2];

                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (z < minZ) minZ = z;

                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;

                        avgX += x;
                        avgY += y;

                        pointCount++;
                    }
                }
            }

            if (pointCount != 0)
            {
                avgX /= pointCount;
                avgY /= pointCount;
            }
            // calculate the centre and move all points
            double centerX = avgX;
            double centerY = avgY;

            for (int elemIndex = 0; elemIndex < gimData12.Elements.Count; elemIndex++)
            {
                GimElement element = gimData12.Elements[elemIndex];
                if (element.ElementType == GimElementType.P_polygon)
                {
                    for (int i = 0; i + 2 < element.ElementDoubles.Length; i += 3)
                    {
                        element.ElementDoubles[i] -= avgX;
                        element.ElementDoubles[i + 1] -= avgY;
                        element.ElementDoubles[i + 2] -= minZ;
                    }
                }
            }

            for (int elemIndex = 0; elemIndex < gimData22.Elements.Count; elemIndex++)
            {
                GimElement element = gimData22.Elements[elemIndex];
                if (element.ElementType == GimElementType.P_polygon)
                {
                    for (int i = 0; i + 2 < element.ElementDoubles.Length; i += 3)
                    {
                        element.ElementDoubles[i] -= centerX;
                        element.ElementDoubles[i + 1] -= centerY;
                        element.ElementDoubles[i + 2] -= minZ;
                    }
                }
            }

            double yaw = ReadCityJson.GetPolyRepYawRotation(gimData12);
            ReadCityJson.unrotateGimDataByYaw(gimData12, yaw);
            ReadCityJson.unrotateGimDataByYaw(gimData22, yaw);

            position = new()
            {
                CenterX = centerX,
                CenterY = centerY,
                MinX = minX,
                MinY = minY,
                MaxX = maxX,
                MaxY = maxY,
                MinZ = minZ,
                Ry = yaw
            };

        }

        public static void GetObjectStats(JToken cityObjects)
        {


            foreach (JProperty jsonChildObjectProperty in cityObjects)
            {
                var jsonChildObject = jsonChildObjectProperty.Value;
                NumericStats["total"]++;

                var type = jsonChildObject["type"].Value<String>();

                if(type == "Building")
                {
                    NumericStats["Building"]++;
                }
                else if (type == "BuildingPart")
                {
                    NumericStats["BuildingPart"]++;
                }
                else
                {
                    string otherTypeKey = "other type:" + type;
                    if (!NumericStats.ContainsKey(otherTypeKey)) {
                        NumericStats[otherTypeKey] = 0;
                    }
                    NumericStats[otherTypeKey]++;
                }

                var children = jsonChildObject["children"];
                if(children != null)
                {
                    var childrenArray = children.ToArray();
                    if(childrenArray.Length > 0)
                    {
                        NumericStats["has children"]++;
                        string childCountKey = "has " + childrenArray.Length + " children";
                        if (!NumericStats.ContainsKey(childCountKey))
                        {
                            NumericStats[childCountKey] = 0;
                        }
                        NumericStats[childCountKey]++;
                    }
                }

                var parents = jsonChildObject["parents"];
                if(parents != null)
                {
                    var parentsArray = parents.ToArray();
                    if(parentsArray.Length > 0)
                    {
                        NumericStats["has parents"]++;
                        string parentCountKey = "has " + parentsArray.Length + " children";
                        if (!NumericStats.ContainsKey(parentCountKey))
                        {
                            NumericStats[parentCountKey] = 0;
                        }
                        NumericStats[parentCountKey]++;
                    }
                }
                
                var geometry = jsonChildObject["geometry"];
                if(geometry != null) {
                    var geometryArray = geometry.ToArray();
                    foreach(var geom in geometryArray)
                    {
                        string lod = geom["lod"].Value<string>();
                        switch (lod)
                        {
                            case "0": 
                                NumericStats["lod0"]++;break;
                            case "1.2":
                                NumericStats["lod1.2"]++;break;
                            case "1.3":
                                NumericStats["lod1.3"]++;break;
                            case "2.2":
                                NumericStats["lod2.2"]++;break;

                        }
                    }
                }

            }
        }

        public static double[] GetTransform(JObject jCityJson)
        {
            JObject jTransform = jCityJson["transform"].Value<JObject>();
            return jTransform["translate"].Value<JArray>().ToObject<double[]>();
        }

        public static double[] GetScale(JObject jCityJson)
        {
            JObject jTransform = jCityJson["transform"].Value<JObject>();
            return jTransform["scale"].Value<JArray>().ToObject<double[]>();
        }

        public static List<double[]> GetVertices(JObject jCityJson, double[] scale, double[] transform)
        {
            List<double[]> vertices = new();
            JArray[] jVertices = jCityJson["vertices"].Value<JArray>().ToObject<JArray[]>();
            int index = 0;
            foreach (JArray jVertice in jVertices)
            {
                double[] vertice = jVertice.ToObject<double[]>();
                vertice[0] = vertice[0] * scale[0] + transform[0]; // X
                vertice[1] = vertice[1] * scale[1] + transform[1]; // Y
                vertice[2] = vertice[2] * scale[2] + transform[2]; // Z
                vertices.Add(vertice);

                index++;
            } // calculate the correct value of the vertices

            return vertices;
        }
    }

    internal class GIMcityjsonPosition
    {
        // bounding box
        public double MinX =0, MinY =0, MaxX =0, MaxY =0;

        // minimum Z
        public double MinZ = 0;

        // center
        public double CenterX = 0, CenterY = 0;

        // yaw rotation
        public double Ry = 0;

        public GIMcityjsonPosition() { }
    }
}
