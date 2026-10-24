using GIMcompress;
using GIMocctCLI;
using GIMocctWrapper;
using GIMtransfer.read.GIMocctRead;
using System.Collections.Generic;
using TopoDSWrapper_EnumType = GIMocctWrapper.TopoDSWrapper_EnumType;

namespace GIMtransfer
{
    public partial class GIMocctReadElements
    {
        public GimData ReadPartHierarchyMeshOnly(GIMocctElement occtElement)
        {
            GimData gimData = new();
            gimData.CreateRootIfNotExists();

            var elemType = (TopoDSWrapper_EnumType)occtElement.Shape.ShapeType();
            switch (elemType)
            {
                case TopoDSWrapper_EnumType.TopoDSWrapper_COMPOUND:
                    ReadCompoundMeshOnly(gimData, occtElement);
                    break;
                case TopoDSWrapper_EnumType.TopoDSWrapper_SOLID:
                    ReadMeshPart(gimData, occtElement);
                    break;
                case TopoDSWrapper_EnumType.TopoDSWrapper_SHELL:
                    ReadSheetMesh(gimData, occtElement);
                    break;
            }

            return gimData;
        }

        private int ReadCompoundMeshOnly(GimData gimData, GIMocctElement compound, uint parentIndex = 0)
        {
            if (compound.Children.Count == 0)
                return 0;

            string shapeName;
            try
            {
                shapeName = ReadNames && compound.Shape.InitNames()
                    ? compound.Shape.GetName() : $"{objectCount++}-COMPOUND";
            }
            catch
            {
                shapeName = $"{objectCount++}-COMPOUND";
            }

            uint folderIndex = (uint)gimData.Elements.Count;
            GimElement folderElement = new() { ElementType = GimElementType.D_folder };
            gimData.Elements.Add(folderElement);
            lock (objectLock) gimData.Objects.Add(GimObjectFactory.Create(folderIndex, shapeName));
            lock (ElementIDByName) ElementIDByName[shapeName] = folderIndex;
            gimData.AddChildToParent(folderIndex, parentIndex);

            Matrix4x4Wrapper location = compound.Shape.Location();
            ApplyMatrixWrapperToLocal(location, folderElement.Locals[^1]);

            for (int i = 0; i < compound.Children.Count; i++)
            {
                var childOcctElement = compound.Children[i];
                childOcctElement.Shape.SetDocWrapper(compound.Shape);

                var childType = (TopoDSWrapper_EnumType)childOcctElement.Shape.ShapeType();
                int childDefinitionId = -1;

                switch (childType)
                {
                    case TopoDSWrapper_EnumType.TopoDSWrapper_COMPOUND:
                        childDefinitionId = ReadCompoundMeshOnly(gimData, childOcctElement, folderIndex);
                        break;
                    case TopoDSWrapper_EnumType.TopoDSWrapper_SOLID:
                        childDefinitionId = ReadMeshPart(gimData, childOcctElement, folderIndex);
                        break;
                    case TopoDSWrapper_EnumType.TopoDSWrapper_SHELL:
                        childDefinitionId = ReadSheetMesh(gimData, childOcctElement, folderIndex);
                        break;
                }

                bool namesLoaded = false;
                try { namesLoaded = ReadNames && childOcctElement.Shape.InitNames(); } catch { }
                if (namesLoaded && childDefinitionId > 0)
                {
                    string childName = childOcctElement.Shape.GetName();
                    if (!string.IsNullOrEmpty(childName))
                        lock (ElementIDByName) ElementIDByName[childName] = (uint)childDefinitionId;
                }
            }

            ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
            return (int)folderIndex;
        }

        private int ReadMeshPart(GimData gimData, GIMocctElement solid, uint parentIndex = 0)
        {
            string shapeName;
            try
            {
                shapeName = ReadNames && solid.Shape.InitNames()
                    ? solid.Shape.GetName() : $"{objectCount++}-SOLID";
            }
            catch
            {
                shapeName = $"{objectCount++}-SOLID";
            }

            uint partIndex = (uint)gimData.Elements.Count;
            GimElement partElement = new() { ElementType = GimElementType.D_part };
            gimData.Elements.Add(partElement);
            lock (objectLock) gimData.Objects.Add(GimObjectFactory.Create(partIndex, shapeName));
            lock (ElementIDByName) ElementIDByName[shapeName] = partIndex;
            gimData.AddChildToParent(partIndex, parentIndex);

            Matrix4x4Wrapper location = solid.Shape.Location();
            ApplyMatrixWrapperToLocal(location, partElement.Locals[^1]);

            float red = 0.5f, green = 0.5f, blue = 0.5f, alpha = 1.0f;
            if (ReadColors && solid.Shape.InitColors())
            {
                red = (float)solid.Shape.GetRed();
                green = solid.Shape.GetGreen();
                blue = solid.Shape.GetBlue();
                alpha = solid.Shape.GetAlpha();
            }

            GimElement polyRepElement = new() { ElementType = GimElementType.R_Poly };
            uint polyRepIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polyRepElement);
            gimData.AddChildToParent(polyRepIndex, partIndex);

            GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
            List<int> elementInts = new();
            List<double> elementDoubles = new();
            int prevCount = 0;

            lock (FileAccess.MeshingLock)
            {
                using BrepMeshIncrementalWrapper brepmesh = new(solid.Shape, MeshLinearDeflection, false, MeshAngularDeflection, true);
                brepmesh.GetStatusFlag();
            }

            foreach (GIMocctElement face in GIMocctMesher.CollectFacesForMeshing(solid))
                GIMocctMesher.AddMeshFaceToPolygon(face, elementInts, elementDoubles, ref prevCount);

            if (elementInts.Count == 0 || elementDoubles.Count == 0)
            {
                ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
                return (int)partIndex;
            }

            polygonElement.elementDoubles = elementDoubles.ToArray();
            polygonElement.elementInts = elementInts.ToArray();

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);

            GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
            int gimMaterialIndex = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement);
            gimData.AddChildToParent((uint)gimMaterialIndex, polygonIndex);
            ApplyOCCTMaterialToGimElement(red, green, blue, alpha, gimMaterialElement);

            ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
            return (int)partIndex;
        }

        private int ReadSheetMesh(GimData gimData, GIMocctElement shell, uint parentIndex = 0)
        {
            string shapeName;
            try
            {
                shapeName = ReadNames && shell.Shape.InitNames()
                    ? shell.Shape.GetName() : $"{objectCount++}-SHEET";
            }
            catch
            {
                shapeName = $"{objectCount++}-SHEET";
            }

            uint partIndex = (uint)gimData.Elements.Count;
            GimElement partElement = new() { ElementType = GimElementType.D_part };
            gimData.Elements.Add(partElement);
            lock (objectLock) gimData.Objects.Add(GimObjectFactory.Create(partIndex, shapeName));
            lock (ElementIDByName) ElementIDByName[shapeName] = partIndex;
            gimData.AddChildToParent(partIndex, parentIndex);

            Matrix4x4Wrapper location = shell.Shape.Location();
            ApplyMatrixWrapperToLocal(location, partElement.Locals[^1]);

            float red = 0.5f, green = 0.5f, blue = 0.5f, alpha = 1.0f;
            if (ReadColors && shell.Shape.InitColors())
            {
                red = (float)shell.Shape.GetRed();
                green = shell.Shape.GetGreen();
                blue = shell.Shape.GetBlue();
                alpha = shell.Shape.GetAlpha();
            }

            GimElement polyRepElement = new() { ElementType = GimElementType.R_Poly };
            uint polyRepIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polyRepElement);
            gimData.AddChildToParent(polyRepIndex, partIndex);

            GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
            List<int> elementInts = new();
            List<double> elementDoubles = new();
            int prevCount = 0;

            lock (FileAccess.MeshingLock)
            {
                using BrepMeshIncrementalWrapper brepmesh = new(shell.Shape, MeshLinearDeflection, false, MeshAngularDeflection, true);
                brepmesh.GetStatusFlag();
            }

            foreach (GIMocctElement face in GIMocctMesher.CollectFacesForMeshing(shell))
                GIMocctMesher.AddMeshFaceToPolygon(face, elementInts, elementDoubles, ref prevCount);

            if (elementInts.Count == 0 || elementDoubles.Count == 0)
            {
                ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
                return (int)partIndex;
            }

            polygonElement.elementDoubles = elementDoubles.ToArray();
            polygonElement.elementInts = elementInts.ToArray();

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);

            GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
            int gimMaterialIndex = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement);
            gimData.AddChildToParent((uint)gimMaterialIndex, polygonIndex);
            ApplyOCCTMaterialToGimElement(red, green, blue, alpha, gimMaterialElement);

            ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
            return (int)partIndex;
        }
    }
}
