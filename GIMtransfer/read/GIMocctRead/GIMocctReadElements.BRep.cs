using GIMcompress;
using GIMocctCLI;
using GIMocctWrapper;
using GIMtransfer.read.GIMocctRead;
using GimUtilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TopoDSWrapper_EnumType = GIMocctWrapper.TopoDSWrapper_EnumType;

namespace GIMtransfer
{
    public partial class GIMocctReadElements
    {
        public GimData ReadPartHierarchy(GIMocctElement occtElement)
        {
            GimData gimData = new();
            gimData.CreateRootIfNotExists();
            
            var elemType = (TopoDSWrapper_EnumType)occtElement.Shape.ShapeType();

            switch (elemType)
            {
                case TopoDSWrapper_EnumType.TopoDSWrapper_COMPOUND: ReadCompound(gimData,occtElement);break;
                case TopoDSWrapper_EnumType.TopoDSWrapper_SOLID: ReadSolidPart(gimData, occtElement);break;
            }
            //ReadShapeRecursive(gimData, occtElement, 0);

            return gimData;
        }

        public int ReadCompound(GimData gimData, GIMocctElement compound, uint parentIndex = 0)
        {
            bool childrenAreOnlyShells = true;
            if (compound.Children.Count == 0)
                return 0;

            // Get shape name early for logging
            string shapeName;
            try
            {
                shapeName = ReadNames && compound.Shape.InitNames() ?
                compound.Shape.GetName() : $"{objectCount++}-COMPOUND";
            } catch
            {
                shapeName = $"{objectCount++}-COMPOUND";
            }

            foreach (var childOcctElement in compound.Children)
            {
                var childTape = (TopoDSWrapper_EnumType)childOcctElement.Shape.ShapeType();
                if (childTape != TopoDSWrapper_EnumType.TopoDSWrapper_SHELL)
                {
                    childrenAreOnlyShells = false;
                    break;
                }
            }

            if (childrenAreOnlyShells)
            {
                int finalIndex = ReadSolidPart(gimData, compound, parentIndex);
                ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
                return finalIndex;
            }


            GimElement folderElement;

            bool isInstance = false;

            // If the instance already exists, we just add a local. Otherwise we add the whole tree
            uint folderIndex;

            if (isInstance) // adding an instance that already exists
            {
                folderIndex = ElementIDByName[shapeName];
                folderElement = gimData.Elements[(int)folderIndex];
            }
            else // creating and adding a new folder
            {
                folderIndex = (uint)gimData.Elements.Count;
                folderElement = new()
                {
                    ElementType = GimElementType.D_folder
                };
                gimData.Elements.Add(folderElement);

                lock (objectLock) gimData.Objects.Add(GimObjectFactory.Create(folderIndex, shapeName));

                lock (ElementIDByName) ElementIDByName[shapeName] = folderIndex;
            }

            gimData.AddChildToParent(folderIndex, parentIndex);

            Matrix4x4Wrapper location = compound.Shape.Location();
            ApplyMatrixWrapperToLocal(location, folderElement.Locals[^1]); // always apply to the last/most recent local

            if (isInstance)
            {
                ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
                return (int)folderIndex;
            }

            bool hasShapeChildren = false;
            for (int i = 0; i < compound.Children.Count; i++)
            {
                var childOcctElement = compound.Children[i];
                childOcctElement.Shape.SetDocWrapper(compound.Shape);

                bool namesLoaded = false;
                try
                {
                    namesLoaded = ReadNames && childOcctElement.Shape.InitNames();
                }
                catch { };

                string childName = "";
                if (namesLoaded)
                    childName = childOcctElement.Shape.GetName();

                // IFC uses distinct IFCTYPE NAME strings per product; reusing STEP instance folding would collapse types.
                if (ElementIDByName.TryGetValue(childName, out uint instanceElementId) && !IsIfc
                    && !string.IsNullOrEmpty(childName))
                {
                    GimElement instanceElement = gimData.Elements[(int)instanceElementId];
                    gimData.AddChildToParent(instanceElementId, folderIndex);

                    Matrix4x4Wrapper childLocation = childOcctElement.Shape.Location();
                    ApplyMatrixWrapperToLocal(childLocation, instanceElement.Locals[^1]); // always apply to the last/most recent local

                    continue;
                }

                // new element, we read the whole subtree.

                int childDefinitionId = -1;
                switch ((TopoDSWrapper_EnumType)childOcctElement.Shape.ShapeType())
                {
                    case TopoDSWrapper_EnumType.TopoDSWrapper_COMPOUND: childDefinitionId = ReadCompound(gimData, childOcctElement, folderIndex); break;
                    case TopoDSWrapper_EnumType.TopoDSWrapper_COMPSOLID: childDefinitionId = ReadCompSolid(gimData, childOcctElement, folderIndex); break;
                    case TopoDSWrapper_EnumType.TopoDSWrapper_SOLID: childDefinitionId = ReadSolidPart(gimData, childOcctElement, folderIndex); break;
                    case TopoDSWrapper_EnumType.TopoDSWrapper_SHELL: childDefinitionId = ReadSheetPart(gimData, childOcctElement, folderIndex); break;
                    default: hasShapeChildren = true; break;
                }

                // Register child definition by name for instance reuse (must use child name, not folder name)
                if (childDefinitionId > 0 && !string.IsNullOrEmpty(childName))
                    lock (ElementIDByName) ElementIDByName[childName] = (uint)childDefinitionId;
            }

            ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
            if (!hasShapeChildren)
                return (int)folderIndex;

            uint partIndex = (uint)gimData.Elements.Count;
            GimElement partElement = new()
            {
                ElementType = GimElementType.D_part
            };

            gimData.Elements.Add(partElement);
            gimData.Objects.Add(GimObjectFactory.Create(partIndex, shapeName + "_GEOMETRY"));

            gimData.AddChildToParent(partIndex, folderIndex);
            // Adding colors to the resulting part here as well
            float red = 0.5f;
            float green = 0.5f;
            float blue = 0.5f;
            float alpha = 1.0f;

            if (ReadColors && compound.Shape.InitColors())
            {
                red = (float)compound.Shape.GetRed();
                green = compound.Shape.GetGreen();
                blue = compound.Shape.GetBlue();
                alpha = compound.Shape.GetAlpha();
            }
            //Materials

            GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
            int gimMaterialIndex = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement);
            gimData.AddChildToParent((uint)gimMaterialIndex, (uint)partIndex);
            ApplyOCCTMaterialToGimElement((float)red, green, blue, alpha, gimMaterialElement);

            GimElement brepElement = new()
            {
                ElementType = GimElementType.R_BRep
            };
            uint brepIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(brepElement);
            gimData.AddChildToParent(brepIndex, partIndex);

            GimMaterial gimMaterialElement2 = new() { elementType = GimElementType.Material };
            int gimMaterialIndex2 = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement2);
            gimData.AddChildToParent((uint)gimMaterialIndex2, (uint)brepIndex);
            ApplyOCCTMaterialToGimElement((float)red, green, blue, alpha, gimMaterialElement2);

            GimElement solidElement = new()
            {
                ElementType = GimElementType.B_solid
            };
            uint solidIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(solidElement);
            gimData.AddChildToParent(solidIndex, brepIndex);

            GimElement shellElement = new()
            {
                ElementType = GimElementType.shell
            };

            int shellIndex = gimData.Elements.Count;
            gimData.Elements.Add(shellElement);
            gimData.AddChildToParent((uint)shellIndex, solidIndex);
            foreach (var child in compound.Children)
            {
                var shapeType = (TopoDSWrapper_EnumType)child.Shape.ShapeType();
                if (shapeType == TopoDSWrapper_EnumType.TopoDSWrapper_FACE)
                    ReadShapeRecursive(gimData, child, shellIndex);
            }

            lock (FileAccess.MeshingLock) GIMocctMesher.MeshBrepElement(gimData, compound, partIndex, red, green, blue, alpha,
                MeshLinearDeflection, MeshAngularDeflection);
            return (int)folderIndex;
        }

        /// <summary>
        /// Reading a part represented only by a shell. This will be a sheet.
        /// </summary>
        public int ReadSheetPart(GimData gimData, GIMocctElement shell, uint parentIndex = 0)
        {

            // trying to get the shape name if it exists
            string shapeName;
            try {
                shapeName = ReadNames && shell.Shape.InitNames() ?
                shell.Shape.GetName() : $"{objectCount++}-SHEET";
            } catch
            {
                shapeName = $"{objectCount++}-SHEET";
            } 

            bool isInstance = false;
            
            uint partIndex;
            GimElement partElement;

            if (isInstance) // in the case of an instance, we add a reference to it
            {
                partIndex = ElementIDByName[shapeName];
                partElement = gimData.Elements[(int)partIndex];
            }
            else // otherwise, we add the new structure
            {
                partIndex = (uint)gimData.Elements.Count;
                partElement = new()
                {
                    ElementType = GimElementType.D_part
                };

                gimData.Elements.Add(partElement);
                gimData.Objects.Add(GimObjectFactory.Create(partIndex, shapeName));

                lock (ElementIDByName) 
                    ElementIDByName[shapeName] = partIndex; 
            }

            gimData.AddChildToParent(partIndex, parentIndex);

            Matrix4x4Wrapper location = shell.Shape.Location();
            ApplyMatrixWrapperToLocal(location, partElement.Locals[^1]);

            if (isInstance)
            {
                ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
                return (int)partIndex;
            }

            float red = 0.5f;
            float green = 0.5f;
            float blue = 0.5f;
            float alpha = 1.0f;

        
            if (ReadColors && shell.Shape.InitColors())
            {
                red = (float)shell.Shape.GetRed();
                green = shell.Shape.GetGreen();
                blue = shell.Shape.GetBlue();
                alpha = shell.Shape.GetAlpha();
            }
            //Materials

            GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
            int gimMaterialIndex = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement);
            gimData.AddChildToParent((uint)gimMaterialIndex, (uint)partIndex);
            ApplyOCCTMaterialToGimElement((float)red, green, blue, alpha, gimMaterialElement);

            GimElement brepElement = new()
            {
                ElementType = GimElementType.R_BRep
            };
            uint brepIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(brepElement);
            gimData.AddChildToParent(brepIndex, partIndex);

            GimMaterial gimMaterialElement2 = new() { elementType = GimElementType.Material };
            int gimMaterialIndex2 = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement2);
            gimData.AddChildToParent((uint)gimMaterialIndex2, (uint)brepIndex);
            ApplyOCCTMaterialToGimElement((float)red, green, blue, alpha, gimMaterialElement2);

            GimElement sheetElement = new()
            {
                ElementType = GimElementType.B_sheet
            };
            uint sheetIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(sheetElement);
            gimData.AddChildToParent(sheetIndex, brepIndex);

            ReadShell(gimData, shell, sheetIndex);

            // later on for meshing
            lock (FileAccess.MeshingLock) GIMocctMesher.MeshBrepElement(gimData, shell, partIndex,red,green,blue,alpha,
                MeshLinearDeflection, MeshAngularDeflection);

            ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
            return (int)partIndex;
        }

        public int ReadSolidPart(GimData gimData, GIMocctElement solid, uint parentIndex = 0)
        {
            // trying to get the shape name if it exists
            string shapeName;
            try
            {
                shapeName = ReadNames && solid.Shape.InitNames() ?
                solid.Shape.GetName() : $"{objectCount++}-SOLID";
            }
            catch
            {
                shapeName = $"{objectCount++}-SOLID";
            }

            

            bool isInstance = false;

            string outPath = FileAccess.GetTempPath("testOCCTMESH");
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            GimElement partElement;
            uint partIndex;

            if (isInstance)
            {
                partIndex = ElementIDByName[shapeName];
                partElement = gimData.Elements[(int)partIndex];
            }
            else
            {
                partIndex = (uint)gimData.Elements.Count;
                partElement = new()
                {
                    ElementType = GimElementType.D_part
                };

                gimData.Elements.Add(partElement);
                gimData.Objects.Add(GimObjectFactory.Create(partIndex, shapeName));

                lock (ElementIDByName)
                    ElementIDByName[shapeName] = partIndex;
            }

            if (shapeName.Contains("Int"))
            {
                int debug = 0;
            }

            if (isInstance)
            {
                ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
                return (int)partIndex;
            }
            gimData.AddChildToParent(partIndex, parentIndex);
            float red = 0.5f;
            float green = 0.5f;
            float blue = 0.5f;
            float alpha = 1.0f;

            if (ReadColors && solid.Shape.InitColors())
            {
                red = (float)solid.Shape.GetRed();
                green = solid.Shape.GetGreen();
                blue = solid.Shape.GetBlue();
                alpha = solid.Shape.GetAlpha();
            }
            //Materials

            GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
            int gimMaterialIndex = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement);
            gimData.AddChildToParent((uint)gimMaterialIndex, (uint)partIndex);
            ApplyOCCTMaterialToGimElement((float)red, green, blue, alpha, gimMaterialElement);
           

            Matrix4x4Wrapper location = solid.Shape.Location();
            ApplyMatrixWrapperToLocal(location, partElement.Locals[^1]);

            GimElement brepElement = new()
            {
                ElementType = GimElementType.R_BRep
            };
            uint brepIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(brepElement);
            gimData.AddChildToParent(brepIndex, partIndex);

            GimMaterial gimMaterialElement2 = new() { elementType = GimElementType.Material };
            int gimMaterialIndex2 = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement2);
            gimData.AddChildToParent((uint)gimMaterialIndex2, (uint)brepIndex);
            ApplyOCCTMaterialToGimElement((float)red, green, blue, alpha, gimMaterialElement2);

            GimElement solidElement = new()
            {
                ElementType = GimElementType.B_solid
            };
            uint solidIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(solidElement);
            gimData.AddChildToParent(solidIndex, brepIndex);
            if(partIndex == 1633)
            {
                int debug = 0;
            }
            foreach (var child in solid.Children)
            {
                if (child.Shape is TopoDSShellWrapper)
                {
                    ReadShell(gimData, child, solidIndex);
                }
                else
                {
                    throw GimLogger.Error("Expecting A shell as a child of a solid, yet received an element of type: " + child.Shape.GetType());
                }
            }

            // later on for meshing
            lock (FileAccess.MeshingLock) GIMocctMesher.MeshBrepElement(gimData, solid, partIndex, (float)red, green, blue, alpha,
                MeshLinearDeflection, MeshAngularDeflection);

            ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);

            return (int)partIndex;
        }

        public void ReadShell(GimData gimData, GIMocctElement shell, uint parentIndex = 0)
        {
            GimElement shellElement = new()
            {
                ElementType = GimElementType.shell
            };

            int shellIndex = gimData.Elements.Count;
            gimData.Elements.Add(shellElement);
            gimData.AddChildToParent((uint)shellIndex, parentIndex);
            foreach (var child in shell.Children) {
                ReadShapeRecursive(gimData, child, shellIndex);
            }

            ReadStatus.EntityCompletedCallback(PROGRESS_ELEMENTS);
        }


        // unimplemented
        public int ReadCompSolid(GimData gimData, GIMocctElement compsolid, uint parentIndex = 0)
        {
            return -1;
        }

        public GimData ReadShapeRecursive(GimData gimData, GIMocctElement occtElement, int parentIndex)
        {
            GimElement element = GetOcctShapeAsElement(occtElement); // getting the appropriate element: note, this adds a local
            if (element == null) return gimData;
            int elementIndex = gimData.Elements.Count;

            gimData.Elements.Add(element);
            gimData.AddChildToParent((uint)elementIndex, (uint)parentIndex, 0); // because we added a local already,
                                                                                // we mark the local to change as local 0

            foreach(var child in occtElement.Children)
            {
                ReadShapeRecursive(gimData, child, elementIndex);
            }

            if (occtElement.Shape is TopoDSEdgeWrapper)
            {
                TopoDSEdgeWrapper edgeShape = (TopoDSEdgeWrapper)occtElement.Shape;
                if (edgeShape.IsSeam())
                {
                    // Reserved for future complex seams (optional C_*2D child under Seam):
                    // double start, end;
                    // edgeShape.GetSeamStartAndEndParam(out start, out end);
                    // Vector3Wrapper startVertex = edgeShape.FirstVertex();
                    // Vector3Wrapper endVertex = edgeShape.LastVertex();

                    gimData.MarkCurveAsSeam((uint)elementIndex);
                }
            }

            return gimData;
        }
        public GimElement GetOcctShapeAsElement(GIMocctElement occtElement)
        {
            GimElement element = new() { Locals = new() { new GimLocal() } };

            var innerShape = occtElement.Shape;

            if(innerShape is TopoDSEdgeWrapper)
            {
                TopoDSEdgeWrapper innerEdge = (TopoDSEdgeWrapper)innerShape;
                if (innerEdge.IsDegenerate()) return null;
                GIMocctCurveReader.ApplyOcctCurveToGimElement(element, occtElement);
            }
            else if(innerShape is TopoDSWireWrapper)
            {
                element.ElementType = GimElementType.wire;
                element.Orientation = (TopAbsWrapper_Orientation)innerShape.GetOrientation() == TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD; 
            }
            else if (innerShape is TopoDSFaceWrapper)
            {
                //innerShape.InitColors();
                GIMocctSurfaceReader.ApplyOcctSurfaceToGimElement(element, occtElement);
            }
            else if(innerShape is TopoDSShellWrapper)
            {
                element.ElementType = GimElementType.shell;
            }
            else if(innerShape is TopoDSSolidWrapper)
            {
                element.ElementType = GimElementType.D_part;
            }
            else if (innerShape is TopoDSCompoundWrapper)
            {
                element.ElementType = GimElementType.D_folder;
            }

            return element;
        }
    }
}
