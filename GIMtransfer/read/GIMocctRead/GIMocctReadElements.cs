using ACadSharp.Entities;
using ACadSharp.Objects;
using cadex;
using GIMcompress;
using GIMcompress.Shrinkwrap;
using GIMocctCLI;
using GIMocctWrapper;
using GIMtransfer.read.GIMocctRead;
using GIMtransfer.write;
using GimUtilities;
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ACadSharp.Entities.TableEntity;
using TopoDSWrapper_EnumType = GIMocctWrapper.TopoDSWrapper_EnumType;

namespace GIMtransfer
{
    public partial class GIMocctReadElements
    {
        public GimProgressStatus ReadStatus;
        public const string PROGRESS_ELEMENTS = "ElementsToRead";
        public const string PROGRESS_IFC_BOOTSTRAP = "IfcLoadBootstrap"; // tiny synthetic-only placeholder for the pre-callback window
        public const string PROGRESS_IFC_LOAD = "IfcLoadDoc";            // real, sized to native product count once known
        // Synthetic crawl bucket, ticking from the start of the read until the elements
        // correction (when the real shape count becomes known and real element ticks begin):
        // it covers every stretch of the read that has no real signal - the raw IfcParse::IfcFile
        // parse (no hook exposed), the throttled tail of BuildShapeMap (native callback fires at
        // most ~200 times per load, so the last few intervals can span minutes on slow files),
        // the silent BuildHierarchy/XCAF assembly inside LoadDoc, and the C# tree walk + count.
        // Its fixed share weight caps its whole crawl (~13.6% of the bar at 150/1100) no matter
        // how long the silent stretches run, and it is cancelled the instant real element
        // progress exists, so it can never dominate the bar or compete with real ticks.
        // Total 4800 x 250ms = 20 min of crawl before the ceiling is reached.
        private const int IFC_BOOTSTRAP_NOMINAL_TOTAL = 4800;
        private const int IFC_BOOTSTRAP_TICK_MS = 250;
        // Fixed share weights (relative shares, sum 1100): synthetic crawl ~13.6%, load ~40.9%,
        // elements ~45.5% of the bar. Weights never change once registered - only bucket totals
        // are corrected mid-flight via AddTask - so the reported percentage is monotone by
        // construction: a total correction rescales only that bucket's fraction (0/anything = 0
        // until its phase starts), never the visible boundary. The synthetic crawl can therefore
        // run harmlessly at its own pace without handoff/correction regressions, and the load
        // phase always shows a clearly visible sweep regardless of how big shapeCount turns out.
        private const int IFC_WEIGHT_BOOTSTRAP = 150;
        private const int IFC_WEIGHT_LOAD = 450;
        private const int IFC_WEIGHT_ELEMENTS = 500;
        public int objectCount = 0; // count for the gimobjects encountered
        public Dictionary<string, uint> ElementIDByName = new();

        public bool ReadNames = true;
        public bool ReadColors = true;
        public bool IsIfc = false;
        public bool ForceBrep = true;
        public double MeshLinearDeflection = 1;
        public double MeshAngularDeflection = 1;
        // LOCKS
        private object objectLock = new();

        // Synthetic crawl ticker for the IFC read: runs from Create until the elements
        // correction in ReadXcafDocumentToGimData (or until the read fails), covering every
        // stretch without a real signal (parse, throttled load tail, BuildHierarchy, tree walk).
        private CancellationTokenSource ifcTailCts;

        public GIMocctReadElements(GimProgressStatus readStatus)
        {
            this.ReadStatus = readStatus;
        }




        public GimData  ReadToGimData(string path, string fileType)
        {
            ElementIDByName = new();
            objectCount = 0;
            ReadNames = true;
            ReadColors = true;

            if (string.Equals(fileType, "ifc", StringComparison.OrdinalIgnoreCase) && !ForceBrep)
                return ReadToGimDataFromIFCMeshOnly(path);

            TopoDSWrapper wrapper;

            if (string.Equals(fileType, "ifc", StringComparison.OrdinalIgnoreCase))
            {
                IsIfc = true;
                IFCReaderCLR ifcReader = LoadIfcDocWithProgress(path);
                wrapper = new TopoDSWrapper(ifcReader);
                ifcReader.Dispose();
            }
            else
            {
                IsIfc = false;
                OcctWrapper occtWrapper = new OcctWrapper();
                lock (FileAccess.OcctLock) switch (fileType.ToLower())
                {
                    case "step":
                    case "stp":
                        occtWrapper.ReadStepToMemory(path);
                        break;
                    case "iges":
                    case "igs":
                        occtWrapper.ReadIGESToMemory(path);
                        break;
                    default: throw new FormatException("Invalid filetype " + fileType + ": Unsupported by OCCT!");
                }
                wrapper = new TopoDSWrapper(occtWrapper, fileType.ToLower());
            }

                return ReadXcafDocumentToGimData(
                new GIMocctElement(TopoDSAbstractShapeWrapper.GetWrapper(wrapper)), wrapper);
        }

        public GimData ReadToGimDataFromIFC(string filePath)
            => ReadToGimData(filePath, "ifc");

        public GimData ReadToGimDataFromIFC(TopoDSAbstractShapeWrapper shapeWrapper)
        {
            ElementIDByName = new();
            IsIfc = false;
            objectCount = 0;
            ReadNames = false;
            ReadColors = false;
            OcctWrapper occtWrapper = new OcctWrapper();
            TopoDSWrapper wrapper = new TopoDSWrapper(occtWrapper);
            return ReadXcafDocumentToGimData(new GIMocctElement(shapeWrapper), wrapper);
        }

        private GimData ReadXcafDocumentToGimData(GIMocctElement parentShape, TopoDSWrapper wrapper)
        {
            parentShape.Shape.InitDocWrapper(wrapper);
            GIMocctRead.ReadShapeTree(parentShape);
            int shapeCount = GetOcctCountRecursive(parentShape);
            if (IsIfc)
            {
                ifcTailCts?.Cancel(); // real element progress is measurable from here - the synthetic crawl's job is done
                ReadStatus.AddTask(PROGRESS_ELEMENTS, shapeCount);
            }
            else
                ReadStatus.Create(new() { { PROGRESS_ELEMENTS, shapeCount } });
            GimData gimData = ForceBrep
                ? ReadPartHierarchy(parentShape)
                : ReadPartHierarchyMeshOnly(parentShape);
            ReadStatus.ForceCompleteTask(PROGRESS_ELEMENTS);
            return gimData;
        }

        // Loads an IFC file via the native BREP reader with live progress, using fixed share
        // weights (see IFC_WEIGHT_*): synthetic crawl ~13.6%, load ~40.9%, elements ~45.5% of
        // the bar. All three buckets are registered up front - Load and Elements with
        // placeholder totals that AddTask corrects to the real product count (first callback)
        // and real shapeCount (after the shape tree is walked) without ever moving the visible
        // boundary.
        //
        // The synthetic crawl ticker runs from the very start until the elements correction in
        // ReadXcafDocumentToGimData: it keeps the bar moving through every stretch that has no
        // real signal - the raw IfcParse::IfcFile parse (no hook exposed), the throttled tail of
        // BuildShapeMap (native callback fires at most ~200 times per load, so its last
        // intervals can span minutes on slow files), the silent BuildHierarchy/XCAF assembly,
        // and the C# tree walk + count - then hands off seamlessly to real element ticks, which
        // simply add on top of its position (monotone by construction, never a catch-up stall).
        // Load itself takes over at the first callback, sized to the native product count and
        // ticked with exact per-callback deltas.
        private IFCReaderCLR LoadIfcDocWithProgress(string path)
        {
            ReadStatus.Create(
                new() {
                    { PROGRESS_IFC_BOOTSTRAP, IFC_BOOTSTRAP_NOMINAL_TOTAL },
                    { PROGRESS_IFC_LOAD, 1 },
                    { PROGRESS_ELEMENTS, 1 }
                },
                new() {
                    { PROGRESS_IFC_BOOTSTRAP, IFC_WEIGHT_BOOTSTRAP },
                    { PROGRESS_IFC_LOAD, IFC_WEIGHT_LOAD },
                    { PROGRESS_ELEMENTS, IFC_WEIGHT_ELEMENTS }
                });
            ifcTailCts = new CancellationTokenSource();
            _ = Task.Run(() => RunBootstrapProgress(ReadStatus, ifcTailCts.Token));

            IFCReaderCLR ifcReader = new();
            bool loadBucketStarted = false;
            int lastReportedCurrent = 0;

            try
            {
                ifcReader.LoadDoc(path, new IfcLoadProgressDelegate((current, total) =>
                {
                    if (!loadBucketStarted)
                    {
                        ReadStatus.AddTask(PROGRESS_IFC_LOAD, Math.Max(total, 1));
                        loadBucketStarted = true;
                    }

                    int delta = current - lastReportedCurrent;
                    if (delta > 0)
                        ReadStatus.EntityCompletedCallback(PROGRESS_IFC_LOAD, delta);
                    lastReportedCurrent = current;
                }));
            }
            finally
            {
                if (!loadBucketStarted)
                    ifcTailCts.Cancel(); // LoadDoc failed before we ever learned 'total' - the read will fall back
                else
                    ReadStatus.ForceCompleteTask(PROGRESS_IFC_LOAD); // safety net if the final (current==total) call never arrived; the crawl keeps running until the elements correction
            }

            return ifcReader;
        }

        // Tiny synthetic ticker for the crawl bucket — no real data ever flows into it,
        // exists only so the bar keeps moving through every stretch of the read that has no
        // real signal (raw IfcParse::IfcFile parse, throttled tail of BuildShapeMap, silent
        // BuildHierarchy, C# tree walk + count). Runs until cancelled (the elements correction
        // in ReadXcafDocumentToGimData, or the read failing) rather than a fixed tick count, so
        // it keeps gracefully crawling through arbitrarily long silent stretches - capped just
        // short of its own total so it never looks "done" on its own. If the read fails and the
        // status is Reset for a fallback plan, the first tick after the reset hits a missing
        // bucket and the ticker terminates itself instead of throwing on a threadpool thread.
        private static async Task RunBootstrapProgress(GimProgressStatus status, CancellationToken token)
        {
            try
            {
                int ticked = 0;
                while (!token.IsCancellationRequested && ticked < IFC_BOOTSTRAP_NOMINAL_TOTAL - 1)
                {
                    await Task.Delay(IFC_BOOTSTRAP_TICK_MS, token);
                    try
                    {
                        status.EntityCompletedCallback(PROGRESS_IFC_BOOTSTRAP);
                    }
                    catch (KeyNotFoundException)
                    {
                        break; // the read failed and the status was Reset for a fallback plan - our bucket is gone
                    }
                    ticked++;
                }
            }
            catch (OperationCanceledException) { }
        }

        public static bool SupportsType(string fileType)
        {
            switch (fileType.ToLower())
            {
                case "step":
                case "stp":
                case "iges":
                case "igs":
                case "ifc":
                    return true;
            }
            return false;
        }
        // deprecated.
        public static GimData Test(string path)
        {
            OcctWrapper occtWrapper = new OcctWrapper();
            //string path = "replace";
            //string path = "replace";
            occtWrapper.ReadStepToMemory(path);

            TopoDSWrapper wrapper = new TopoDSWrapper(occtWrapper); 
            var shapeType = wrapper.ShapeType();

            GIMocctElement parentShape = new(TopoDSAbstractShapeWrapper.GetWrapper(wrapper));

            GIMocctRead.ReadShapeTree(parentShape);
            //GimData gimData = ReadPartHierarchy(parentShape);


            return new();

        }

        public GimData ReadToGimDataFromIFCMeshOnly(string filePath)
        {
            ElementIDByName = new();
            objectCount = 0;
            ReadNames = true;
            ReadColors = true;
            IsIfc = true;

            IFCMeshReader meshReader = new();
            List<MeshProductData> products = meshReader.Read(filePath);

            ReadStatus.Create(new()
            {
                { PROGRESS_ELEMENTS, products.Count }
            });

            GimData gimData = new();
            gimData.CreateRootIfNotExists();

            Stack<uint> parentStack = new();
            parentStack.Push(0);
            int currentDepth = 0;

            foreach (MeshProductData product in products)
            {
                while (currentDepth >= product.Depth && parentStack.Count > 1)
                {
                    parentStack.Pop();
                    currentDepth--;
                }
                currentDepth = product.Depth;

                if (product.IsFolder)
                {
                    uint folderIndex = (uint)gimData.Elements.Count;
                    GimElement folderElement = new() { ElementType = GimElementType.D_folder };
                    gimData.Elements.Add(folderElement);
                    gimData.AddChildToParent(folderIndex, parentStack.Peek());
                    gimData.Objects.Add(GimObjectFactory.Create(folderIndex, product.Name));
                    parentStack.Push(folderIndex);
                }
                else
                {
                    uint partIndex = (uint)gimData.Elements.Count;
                    GimElement partElement = new() { ElementType = GimElementType.D_part };
                    gimData.Elements.Add(partElement);
                    gimData.AddChildToParent(partIndex, parentStack.Peek());

                    string typeName = product.IfcType ?? "";
                    string productName = product.Name ?? "";
                    string fullName = string.IsNullOrEmpty(productName)
                        ? typeName.ToUpper()
                        : (typeName.ToUpper() + " " + productName);
                    gimData.Objects.Add(GimObjectFactory.Create(partIndex, fullName));

                    GimElement polyRepElement = new() { ElementType = GimElementType.R_Poly };
                    uint polyRepIndex = (uint)gimData.Elements.Count;
                    gimData.Elements.Add(polyRepElement);
                    gimData.AddChildToParent(polyRepIndex, partIndex);

                    if (product.SubMeshes != null && product.SubMeshes.Count > 0)
                    {
                        foreach (var sub in product.SubMeshes)
                        {
                            GimElement polygonElement = new()
                            {
                                ElementType = GimElementType.P_polygon,
                                elementDoubles = sub.Verts,
                                elementInts = sub.Faces
                            };
                            uint polygonIndex = (uint)gimData.Elements.Count;
                            gimData.Elements.Add(polygonElement);
                            gimData.AddChildToParent(polygonIndex, polyRepIndex);

                            GimMaterial gimMat = new() { elementType = GimElementType.Material };
                            int gimMaterialIndex = gimData.Elements.Count;
                            gimData.Elements.Add(gimMat);
                            gimData.AddChildToParent((uint)gimMaterialIndex, polygonIndex);
                            ApplyOCCTMaterialToGimElement(sub.ColorR, sub.ColorG, sub.ColorB, sub.ColorA, gimMat);
                        }
                    }
                    else
                    {
                        GimElement polygonElement = new()
                        {
                            ElementType = GimElementType.P_polygon,
                            elementDoubles = product.Verts,
                            elementInts = product.Faces
                        };
                        uint polygonIndex = (uint)gimData.Elements.Count;
                        gimData.Elements.Add(polygonElement);
                        gimData.AddChildToParent(polygonIndex, polyRepIndex);

                        GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
                        int gimMaterialIndex = gimData.Elements.Count;
                        gimData.Elements.Add(gimMaterialElement);
                        gimData.AddChildToParent((uint)gimMaterialIndex, polygonIndex);
                        ApplyOCCTMaterialToGimElement(product.ColorR, product.ColorG, product.ColorB, product.ColorA, gimMaterialElement);
                    }
                }
                ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
            }

            ReadStatus.ForceCompleteTask(PROGRESS_ELEMENTS);
            return gimData;
        }

        public static void ApplyMatrixWrapperToLocal(Matrix4x4Wrapper matrix, GimLocal local)
        {
            local.Px = matrix.M14;
            local.Py = matrix.M24;
            local.Pz = matrix.M34;

            local.Xx = matrix.M11;
            local.Xy = matrix.M12;
            local.Xz = matrix.M13;

            local.Yx = matrix.M21;
            local.Yy = matrix.M22;
            local.Yz = matrix.M23;

            local.Zx = matrix.M31;
            local.Zy = matrix.M32;
            local.Zz = matrix.M33;


        }


        public static int GetOcctCountRecursive(GIMocctElement occtElement)
        {
            if (occtElement == null) return 0;
            int totalCount = 1;

            foreach(var child in occtElement.Children)
            {
                var innerShape = child.Shape;
                // for the sake of speed we don't count these elements. TODO ANIS: maybe count these elements?
                if (innerShape is TopoDSFaceWrapper || innerShape is TopoDSWireWrapper || innerShape is TopoDSEdgeWrapper) continue;
                totalCount += GetOcctCountRecursive(child);
            }

            return totalCount;
        }

        public static void ApplyOcctSurfaceToGimElement(GimElement element, GIMocctElement occtElement)
        {

        }

        public static void ApplyOCCTMaterialToGimElement(float red, float green, float blue, float alpha, GimMaterial gimMaterialElement)
        {
            //Diffuse && Base colors

            GimColour gimColourDiffuse = new(alpha, red, green, blue);
            GimColour gimColourBase = new(alpha, red, green, blue);
            int resultingDiffuseColor = gimColourDiffuse.ToInt();
            int resultingBaseColor = gimColourBase.ToInt();
            gimMaterialElement.DiffuseColour = resultingDiffuseColor;
            gimMaterialElement.BaseColour = resultingBaseColor;

            //Ambient colors

            GimColour gimColourAmbient = new GimColour(1, 0.2f, 0.2f, 0.2f);
            int resultingAmbientColor = gimColourAmbient.ToInt();
            gimMaterialElement.AmbientColour = resultingAmbientColor;



            //Specular colors


            GimColour gimColourSpecular = new(1, 0, 0, 0);
            int resultingSpecularColor = gimColourSpecular.ToInt();
            gimMaterialElement.SpecularColour = resultingSpecularColor;

            //Emission Colors

            GimColour gimColourEmissive = new(0, 0, 0, 0);
            int resultingEmissiveColor = gimColourEmissive.ToInt();
            gimMaterialElement.EmissionColour = resultingEmissiveColor;

        }
        

    }
}
