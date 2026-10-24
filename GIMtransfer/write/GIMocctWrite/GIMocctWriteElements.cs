using GIMcompress;
using GIMocctCLI;
using GIMocctWrapper;
using GimUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace GIMtransfer
{
    public class GIMocctWriteElements
    {
        static int faceNumber = 0;
        public GimProgressStatus WriteStatus;
        public const string PROGRESS_WRITE = "ElementsToWrite";
        private Dictionary<int, TDFLabelWrapper> LabelsByElementId = new();
        internal Dictionary<uint, uint> elemToObj = new();
        private bool dataHasBRep;

        public GIMocctWriteElements(GimProgressStatus writeStatus)
        {
            WriteStatus = writeStatus;
        }

        public string WriteGimDataToOcct(string filePath, string fileType, GimData gimData)
        {
            var writer = BuildOcctWriter(gimData);

            switch (fileType.ToLower())
            {
                case "step":
                case "stp": writer.AdvancedWriteStepFromMemory(filePath + "." + fileType); break;
                case "iges":
                case "igs": writer.WriteIGESFromMemory(filePath + "." + fileType); break;
                default: throw new InvalidOperationException("Unsupported file type for OCCT!");
            }

            WriteStatus?.ForceCompleteTask(PROGRESS_WRITE);
            return filePath + "." + fileType;
        }

        public bool WriteGimDataToIfc(string filePath, string fileType, GimData gimData, out List<string> messages)
        {
            messages = null;
            if (!string.Equals(fileType, "ifc", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("IFC export requires fileType 'ifc'.", nameof(fileType));

            var writer = BuildOcctWriter(gimData);
            writer.IsMeshOnly = !dataHasBRep;
            string outputPath = filePath + "." + fileType;
            List<string> msgs = null;
            bool ok = writer.AdvancedWriteIfcFromMemory(outputPath, out msgs);
            messages = msgs;

            WriteStatus?.ForceCompleteTask(PROGRESS_WRITE);
            return ok;
        }

        private OcctWrapper BuildOcctWriter(GimData gimData)
        {
            LabelsByElementId = new();
            int metaElementCount = 0;
            foreach (GimElement element in gimData.Elements)
            {
                if (element.ElementType.IsDocument() || element.ElementType == GimElementType.shell)
                    metaElementCount++;
            }
            WriteStatus?.Create(new() { { PROGRESS_WRITE, metaElementCount } });
            var occtShape = GetAdvancedOcctShape(gimData);
            if (occtShape == null)
                throw new Exception("shape is null!");
            return occtShape.GetAdvancedWriter();
        }


        public TopoDSAbstractShapeWrapper WriteGimDataToOcctIFC(GimData gimData)
        {
            int metaElementCount = 0;

            foreach (GimElement element in gimData.Elements)
            {
                if (element.ElementType.IsDocument() || element.ElementType == GimElementType.shell)
                {
                    metaElementCount++;
                }
            }

            WriteStatus?.Create(new(){
                { PROGRESS_WRITE, metaElementCount}
            });

            var occtShape = GetOcctShape(gimData);
            if (occtShape == null) throw new Exception("shape is null!");
            //TopoDSWrapper wrapper = new TopoDSWrapper(writer, fileType.ToLower());
            //GIMocctElement parentShape = new(TopoDSAbstractShapeWrapper.GetWrapper(wrapper));

            //GIMocctRead.ReadShapeTree(parentShape);
            //GimData newGimData = GIMoctReadElements.ReadPartHierarchy(parentShape);

            return occtShape;
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

        public TopoDSAbstractShapeWrapper GetOcctShape(GimData gimData)
        {
            TopoDSAbstractShapeWrapper finalShape;
            elemToObj = gimData.GetElementToObjectDict();
            DocHelperWrapper docHelperWrapper = new();
            docHelperWrapper.InitializeDoc();
            dataHasBRep = HasAnyBRepElement(gimData);

            GimElement rootElement = gimData.Elements[0];
            var rootChildren = rootElement.GetAllChildrenIndexesOrdered(0, gimData);
            if(rootChildren.Count > 1)
            {
                return WriteCompound(gimData, 0, -1, docHelperWrapper);
            }
            
            if(rootChildren.Count == 1)
            {
                GimElement onlyChild = gimData.Elements[(int)rootElement.Elements[0]];
                if(onlyChild.ElementType == GimElementType.D_folder)
                {
                    return WriteCompound(gimData, (int)rootElement.Elements[0], 0, docHelperWrapper);
                }
                else if (onlyChild.ElementType == GimElementType.D_part)
                {
                    return dataHasBRep
                        ? WriteSolidBRep(gimData, (int)rootElement.Elements[0], 0, docHelperWrapper)
                        : WriteSolidMesh(gimData, (int)rootElement.Elements[0], 0, docHelperWrapper);
                }
            }
            return null;
        }

        public TopoDSAbstractShapeWrapper GetAdvancedOcctShape(GimData gimData)
        {
            TopoDSAbstractShapeWrapper finalShape;
            elemToObj = gimData.GetElementToObjectDict();
            DocHelperWrapper docHelperWrapper = new();
            docHelperWrapper.InitializeDoc();
            dataHasBRep = HasAnyBRepElement(gimData);

            GimElement rootElement = gimData.Elements[0];
            var rootChildren = rootElement.GetAllChildrenIndexesOrdered(0, gimData);
            if (rootChildren.Count > 1)
            {
                finalShape = WriteCompound(gimData, 0, -1, docHelperWrapper);
                if (finalShape == null) return null;
                finalShape._docWrapper = docHelperWrapper;
                return finalShape;
            }

            if (rootChildren.Count == 1)
            {
                GimElement onlyChild = gimData.Elements[(int)rootElement.Elements[0]];
                if (onlyChild.ElementType == GimElementType.D_folder)
                {
                    finalShape = WriteCompound(gimData, (int)rootElement.Elements[0], 0, docHelperWrapper);
                    if (finalShape == null) return null;
                    finalShape._docWrapper = docHelperWrapper;
                    return finalShape;
                }
                else if (onlyChild.ElementType == GimElementType.D_part)
                {
                    finalShape = dataHasBRep
                        ? WriteSolidBRep(gimData, (int)rootElement.Elements[0], 0, docHelperWrapper)
                        : WriteSolidMesh(gimData, (int)rootElement.Elements[0], 0, docHelperWrapper);
                    finalShape._docWrapper = docHelperWrapper;
                    return finalShape;
                }
            }
            return null; 
           
        }


        public TopoDSCompoundWrapper WriteCompound(GimData gimData, int folderIndex, int folderLocalIndex, DocHelperWrapper docHelperWrapper)
        {
            GimElement folderElement = gimData.Elements[folderIndex];
            GimLocal folderLocal = null;
            if(folderLocalIndex >= 0) folderLocal = folderElement.Locals[folderLocalIndex];

            var childrenAndLocals = folderElement.GetAllChildrenIndexesOrdered(folderIndex, gimData);
            var childWrappers = new List<TopoDSAbstractShapeWrapper>(childrenAndLocals.Count);

            var childLabels = new List<TDFLabelWrapper>(childrenAndLocals.Count);
            var childTransforms = new List<Matrix4x4Wrapper>(childrenAndLocals.Count);
            var childOffsets = new List<Vector3Wrapper>(childrenAndLocals.Count);

            // Build unique children single-threaded: one shared OCCT document (docHelperWrapper).
            // Concurrent shape construction against that document is unsafe even with C#-side
            // dictionary locks — the old Parallel.ForEach only locked LabelsByElementId /
            // uniqueChildren, not the native OCCT calls. Re-parallelizing may be worth revisiting
            // now that major memory leaks are fixed, but only with per-thread OCCT documents or
            // confirmed thread-safety; do not reintroduce Parallel.* here without that.
            Dictionary<int, TopoDSAbstractShapeWrapper> uniqueChildren = new();
            var orderedUniqueChildIndices = new List<int>();
            var seenChild = new HashSet<int>();
            foreach (var tuple in childrenAndLocals)
            {
                if (seenChild.Add(tuple.Item1))
                    orderedUniqueChildIndices.Add(tuple.Item1);
            }

            foreach (int childIndex in orderedUniqueChildIndices)
            {
                if (LabelsByElementId.ContainsKey(childIndex))
                    continue;

                GimElement childElement = gimData.Elements[childIndex];
                TopoDSAbstractShapeWrapper newChild = null;

                switch (childElement.ElementType)
                {
                    case GimElementType.D_folder: newChild = WriteCompound(gimData, childIndex, 0, docHelperWrapper); break;
                    case GimElementType.D_part: newChild = dataHasBRep
                        ? WriteSolidBRep(gimData, childIndex, 0, docHelperWrapper)
                        : WriteSolidMesh(gimData, childIndex, 0, docHelperWrapper);
                        break;
                }

                if (newChild != null)
                    uniqueChildren[childIndex] = newChild;
            }

            // Collect labels/transforms single-threaded for the same shared OCCT document reason
            // as above (GetLabel() is a native OCCT call). Same caveat on future re-parallelization.
            for (int i = 0; i < childrenAndLocals.Count; i++)
            {
                var childTuple = childrenAndLocals[i];
                var childIndex = childTuple.Item1;
                var childLocalIndex = childTuple.Item2;

                GimElement childElement = gimData.Elements[childIndex];
                GimLocal childLocal = childElement.Locals[childLocalIndex];

                var (offset, transform) = GetLocalAsWrapper(childLocal);

                if (LabelsByElementId.TryGetValue(childIndex, out var childLabel))
                {
                    childWrappers.Add(null);
                    childLabels.Add(childLabel);
                    childTransforms.Add(transform);
                    childOffsets.Add(offset);
                    continue;
                }
                if (!uniqueChildren.TryGetValue(childIndex, out var newChild) || newChild == null)
                {
                    // Child element type not supported for compound composition (or failed to build).
                    // Skip instead of throwing to keep recursive export robust.
                    continue;
                }

                childWrappers.Add(newChild);
                var builtLabel = newChild.GetLabel();
                childLabels.Add(builtLabel);
                childTransforms.Add(transform);
                childOffsets.Add(offset);
                LabelsByElementId[childIndex] = builtLabel;
            }

            CompoundBuilder compoundBuilder = new();

            if (folderLocal != null)
            {
                (var center, var matrix) = GetLocalAsWrapper(folderLocal);
                compoundBuilder.SetLocation(center, matrix);
            }

            string shapeName = ResolvePartName(gimData, folderIndex);

            if (childLabels.Count == 0)
            {
                // This folder contains no OCCT-composable descendants (e.g., metadata-only branches).
                // Returning null lets parent compounds skip this branch instead of hard-failing export.
                return null;
            }

            var resultingShape = compoundBuilder.GetCompound(docHelperWrapper, shapeName,
                childWrappers, childLabels, childTransforms, childOffsets);

            WriteStatus?.EntityCompletedCallback(PROGRESS_WRITE);

#if FALSE
            resultingShape._docWrapper = docHelperWrapper;
            var debugWriter = resultingShape.GetAdvancedWriter();
            Directory.CreateDirectory(FileAccess.GetTempPath("compounds"));
            debugWriter.AdvancedWriteStepFromMemory(FileAccess.GetTempPath("compounds\\compound-" + folderIndex + ".step"));

#endif
            return resultingShape;
        }

        private static bool HasAnyBRepElement(GimData gimData)
        {
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                if (gimData.Elements[i].ElementType == GimElementType.R_BRep)
                    return true;
            }
            return false;
        }

        public TopoDSAbstractShapeWrapper WriteSolidBRep(GimData gimData, int partIndex, int partLocalIndex, DocHelperWrapper docHelperWrapper)
        {
            List<TopoDSShellWrapper> shellWrappers = new();
            GimElement partElement = gimData.Elements[partIndex];

            GimElement brepElement = null;
            foreach(int childIndex in partElement.Elements)
            {
                if (gimData.Elements[childIndex].ElementType == GimElementType.R_BRep)
                {
                    brepElement = gimData.Elements[childIndex];
                    break;
                }
            }
            if (brepElement == null) throw new InvalidOperationException($"Part {partIndex} has no R_BRep element in BRep mode");

            int solidIndex = -1;
            foreach (int childIdx in brepElement.Elements)
            {
                if (gimData.Elements[childIdx].ElementType == GimElementType.B_solid)
                {
                    solidIndex = childIdx;
                    break;
                }
            }
            if (solidIndex < 0) throw new InvalidOperationException($"Part {partIndex} R_BRep has no B_solid child");

            GimElement bSolidElement = gimData.Elements[solidIndex];

            var shellsAndLocals = bSolidElement.GetAllChildrenIndexesOrdered(solidIndex, gimData);
            foreach(var shellTuple in shellsAndLocals)
            {
                shellWrappers.Add(WriteShell(gimData, shellTuple.Item1, shellTuple.Item2));
            }
            if (shellWrappers.Count == 0) throw new InvalidOperationException($"Part {partIndex} has no shells");
            SolidBuilder solidBuilder = new(shellWrappers[0]);
            foreach(var shellWrapper in shellWrappers)
            {
                if (shellWrapper == shellWrappers.First()) continue;
                solidBuilder.Add(shellWrapper);
            }

            GimLocal partLocal = partElement.Locals[partLocalIndex];
            (var center, var matrix) = GetLocalAsWrapper(partLocal);

            string shapeName = ResolvePartName(gimData, partIndex);
            float[] argbValues = new float[] { 1.0f, 0.5f, 0.5f, 0.5f };
            foreach (int matIdx in partElement.Elements)
            {
                if (gimData.Elements[matIdx].ElementType != GimElementType.Material) continue;
                GimElement mat = gimData.Elements[matIdx];
                if (mat.ElementInts != null && mat.ElementInts.Length > 0)
                {
                    GimColour temp = GimColour.FromInt(mat.ElementInts[0]);
                    argbValues = new float[] { temp.alpha, temp.red, temp.green, temp.blue };
                }
                break;
            }

            WriteStatus?.EntityCompletedCallback(PROGRESS_WRITE);
            return solidBuilder.GetSolid(center, matrix, docHelperWrapper, argbValues, shapeName);
        }

        public TopoDSAbstractShapeWrapper WriteSolidMesh(GimData gimData, int partIndex, int partLocalIndex, DocHelperWrapper docHelperWrapper)
        {
            GimElement partElement = gimData.Elements[partIndex];

            int polyRepIndex = -1;
            foreach(int childIndex in partElement.Elements)
            {
                if (gimData.Elements[childIndex].ElementType == GimElementType.R_Poly)
                {
                    polyRepIndex = childIndex;
                    break;
                }
            }
            if (polyRepIndex < 0) throw new InvalidOperationException($"Part {partIndex} has no R_Poly element in mesh mode");

            GimElement polyRepElement = gimData.Elements[polyRepIndex];
            if (polyRepElement.Elements.Count == 0) throw new InvalidOperationException($"Part {partIndex} R_Poly has no children");

            List<double> allVertices = new();
            List<int> allTriangles = new();
            int vertexOffset = 0;
            float[] argbValues = new float[] { 1.0f, 0.5f, 0.5f, 0.5f };

            foreach (uint polyChild in polyRepElement.Elements)
            {
                int polyIdx = (int)polyChild;
                GimElement polyElement = gimData.Elements[polyIdx];
                if (polyElement.ElementType != GimElementType.P_polygon) continue;
                if (polyElement.ElementDoubles == null || polyElement.ElementInts == null) continue;

                allVertices.AddRange(polyElement.ElementDoubles);
                for (int t = 0; t < polyElement.ElementInts.Length; t++)
                    allTriangles.Add(polyElement.ElementInts[t] + vertexOffset);
                vertexOffset += polyElement.ElementDoubles.Length / 3;

                foreach (uint matChild in polyElement.Elements)
                {
                    GimElement mat = gimData.Elements[(int)matChild];
                    if (mat.ElementType != GimElementType.Material) continue;
                    if (mat.ElementInts != null && mat.ElementInts.Length > 0)
                    {
                        GimColour temp = GimColour.FromInt(mat.ElementInts[0]);
                        argbValues = new float[] { temp.alpha, temp.red, temp.green, temp.blue };
                    }
                    break;
                }
            }
            

            if (allVertices.Count == 0 || allTriangles.Count == 0)
                throw new InvalidOperationException($"Part {partIndex} has no valid triangulation data");

            WriteStatus?.EntityCompletedCallback(PROGRESS_WRITE);

            return MeshShapeBuilder.BuildFromTriangulation(
                allVertices.ToArray(),
                allTriangles.ToArray(),
                argbValues,
                ResolvePartName(gimData, partIndex),
                docHelperWrapper);
        }

        private string ResolvePartName(GimData gimData, int partIndex)
        {
            if (elemToObj.TryGetValue((uint)partIndex, out uint objIdx))
                return gimData.Objects[(int)objIdx].Name;
            return "";
        }

        public TopoDSShellWrapper WriteShell(GimData gimData, int elementIndex, int localIndex)
        {
            GimElement shellElement = gimData.Elements[elementIndex];
            var facesAndLocals = shellElement.GetAllChildrenIndexesOrdered(elementIndex, gimData);
            TopoDSFaceWrapper[] faces = new TopoDSFaceWrapper[facesAndLocals.Count];
            // Face BRep construction single-threaded: WriteSurfaceElement builds real OCCT geometry
            // (curves/wires/surfaces). The old Parallel.For had no locks at all around those native
            // calls. Re-parallelizing may be worth revisiting after the memory-leak fixes, but only
            // with per-thread OCCT documents or confirmed thread-safety.
            for (int i = 0; i < facesAndLocals.Count; i++)
            {
                var faceTuple = facesAndLocals[i];
                var faceIndex = faceTuple.Item1;
                var faceLocalIndex = faceTuple.Item2;
                var resultingFace = GIMocctSurfaceWriter.WriteSurfaceElement(gimData, faceIndex, faceLocalIndex);

                resultingFace.GetChildren()[0].GetChildren();
                faces[i] = resultingFace;
#if FALSE 
                {
                    var faceWriter = resultingFace.GetWriter();
                    Directory.CreateDirectory(FileAccess.GetTempPath("faces"));
                    faceWriter.WriteStepFromMemory(FileAccess.GetTempPath("faces\\face" + faceNumber + ".step"));
                    faceNumber++;
                }
#endif
            }

            WriteStatus?.EntityCompletedCallback(PROGRESS_WRITE);
            ShellBuilder shellBuilder = new(faces.ToList());
            return shellBuilder.GetShell();
        }

        public static (Vector3Wrapper, Matrix4x4Wrapper) GetLocalAsWrapper(GimLocal local)
        {
            Vector3Wrapper vector = new Vector3Wrapper(local.Px, local.Py, local.Pz);

            Matrix4x4Wrapper matrix = new Matrix4x4Wrapper();
            matrix.SetRow(0, local.Xx, local.Xy, local.Xz);
            matrix.SetRow(1, local.Yx, local.Yy, local.Yz);
            matrix.SetRow(2, local.Zx, local.Zy, local.Zz);
            matrix.SetPosition(local.Px, local.Py, local.Pz);
            return (vector, matrix);
        }
    }
}
