using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Assimp;
using GimUtilities;
using Assimp.Configs;
using Assimp.Unmanaged;

namespace GIMtransfer.write
{
    //TODO: rplace all Assimp.Type with Assimp as it's imported in the current file 
    public class AssimpWriteElements
    {
        GimProgressStatus WriteStatus;
        public const string ASSIMP_DISK_WRITE = "AssimpDiskWrite";
        public const string ASSIMP_NODE_WRITE = "AssimpNodeWrite";
        Dictionary<int, int> ElementMeshesByID = new();
        Dictionary<int, int> MaterialsByID = new();
        public AssimpWriteElements(GimProgressStatus writeStatus)
        {
            WriteStatus = writeStatus;
        }
        public string WriteGimDataToAssimp(string path, string fileType, GimData gimData, string fileName)
        {
            ElementMeshesByID = new();
            MaterialsByID = new();
            Scene newScene = new();
            Node node = new();
            var exportIndex = GetExportElementIndex(gimData);
            GimElement bigGimFolder = gimData.Elements[exportIndex];

            int exportElementCount = bigGimFolder.Elements.Count;

            WriteStatus?.Create(new()
            {
                { ASSIMP_DISK_WRITE, 100 },
                { ASSIMP_NODE_WRITE, exportElementCount * 100 }
            });

            newScene = GetSceneFromGimData(gimData,exportIndex, fileName);

            WriteStatus?.ForceCompleteTask(ASSIMP_NODE_WRITE);

            string targetPath = path + "." + fileType;
            string exportFileType = GetProperAssimpExportFormatID(fileType);
            using AssimpContext exporter = new AssimpContext();

            // todo: add try/catch in case the user inputs an invalid output type
            var outScene = exporter.ExportFile(newScene, targetPath, exportFileType);
            WriteStatus?.ForceCompleteTask(ASSIMP_DISK_WRITE);
            return targetPath;

        }

        public ExportDataBlob WriteGimDataToBlob(GimData gimData, uint objectID = 0, string objectName = "", string fileType = "glb", bool useTopTransform = false)
        {
            ElementMeshesByID = new();
            MaterialsByID = new();
            Scene newScene = new();
            Node node = new();
            var exportIndex = objectID > 0 ? (int)objectID : GetExportElementIndex(gimData);
            GimElement bigGimFolder = gimData.Elements[exportIndex];

            int exportElementCount = bigGimFolder.Elements.Count;

            WriteStatus?.Create(new()
            {
                { ASSIMP_DISK_WRITE, 100 },
                { ASSIMP_NODE_WRITE, exportElementCount * 100 }
            });

            newScene = GetSceneFromGimData(gimData, exportIndex, objectName, useTopTransform);

            WriteStatus?.ForceCompleteTask(ASSIMP_NODE_WRITE);

            string exportFileType = GetProperAssimpExportFormatID(fileType);
            using AssimpContext exporter = new AssimpContext();

            PostProcessSteps postProcessSteps = PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.RemoveRedundantMaterials | PostProcessSteps.OptimizeMeshes | PostProcessSteps.OptimizeGraph;
            //PostProcessSteps postProcessSteps = PostProcessPreset.TargetRealTimeFast;

            // todo: add try/catch in case the user inputs an invalid output type
            var blob = exporter.ExportToBlob(newScene, exportFileType, postProcessSteps);
            WriteStatus?.ForceCompleteTask(ASSIMP_DISK_WRITE);
            return blob;
        }

        private static int GetExportElementIndex(GimData gimData)
        {
            int index = 0;
            for ( int i = 0; i < gimData.Elements.Count; i++ )
            {
                var currentElement = gimData.Elements[i];
                if( currentElement.ElementType == GimElementType.D_root || currentElement.ElementType == GimElementType.D_folder )
                {
                    if (currentElement.Elements.Count == 1)
                        continue;
                    else if (currentElement.Elements.Count  > 1)
                    {
                        index = i;
                        break;
                    }
                }
                if(currentElement.ElementType == GimElementType.D_part)
                {
                    var currentElementLocals = currentElement.Locals;
                    if (currentElementLocals.Count == 0) // in this case the part is the parent itself
                        return i;
                    int parentIndex = (int)currentElementLocals[0].Parent;
                    index = parentIndex;
                    break;
                }
            }
            return index;
        }

        private static string GetProperAssimpExportFormatID(string fileType)
        {
            string fileTypeLower =fileType.ToLower();
            string exportID = "";
            // todo: make this a switch case and replace the ones that didn't change with a simple exportID = fileType
            if (fileTypeLower == "dae")
                exportID = "collada";
            if (fileTypeLower == "x")
                exportID = "x";
            if (fileTypeLower == "stp")
                exportID = "stp";
            if (fileTypeLower == "obj")
                exportID = "obj";
            if (fileTypeLower == "stl")
                exportID = "stl";
            if (fileTypeLower == "ply")
                exportID = "ply";
            if (fileTypeLower == "3ds")
                exportID = "3ds";
            if (fileTypeLower == "gltf")
                exportID = "gltf2";
            if (fileTypeLower == "glb")
                exportID = "glb2";
            if (fileTypeLower == "assbin")
                exportID = "assbin";
            if (fileTypeLower == "assxml")
                exportID = "assxml";
            if (fileTypeLower == "x3d")
                exportID = "x3d";
            if (fileTypeLower == "fbx")
                exportID = "fbx";
            if (fileTypeLower == "3mf")
                exportID = "3mf";
            if (fileTypeLower == "json")
                exportID = "assjson";
            return exportID;
        }
        private Scene GetSceneFromGimData(GimData gimData, int rootIndex, string fileName, bool useTopTransform = true)
        {
            GimElement rootElement = gimData.Elements[rootIndex];
            Scene newScene = new();
            newScene.RootNode = new Node(fileName);
            var assimpRootNode = newScene.RootNode;

            var defaultMaterial = GetAssimpMaterialFromGimMaterial(GetDefaultGIMmaterial());
            newScene.Materials.Add(defaultMaterial);
            MaterialsByID[0] = 0;

            // Setting the main transform
            assimpRootNode.Transform = rootElement.ElementType == GimElementType.D_folder && useTopTransform? 
                GetTransformFromLocal(rootElement.Locals[0]): // Get the folder's first local
                (new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)); // Use a default local

            //
            // if we have a single part that we wish to export, we just export it directly
            //

            if(rootElement.ElementType == GimElementType.D_part || (rootElement.ElementType == GimElementType.ExternalReference && rootElement.Elements.Count > 0))
            {
                Node node = GetNodeStructure(gimData, rootIndex, -1, gimData.GetElementToObjectDict());
                PopulateNodeFromPart(gimData, newScene, node, rootElement);
                assimpRootNode.Children.Add(node);
                return newScene;
            }

            //
            // in the case of a folder, we loop over the children
            //

            var childrenAssimp = assimpRootNode.Children;

            var childrenAndLocals = rootElement.GetAllChildrenIndexesOrdered(rootIndex, gimData);
            var elemToObjDict = gimData.GetElementToObjectDict();

            for (int i = 0; i < childrenAndLocals.Count; i++)
            {
                (int childIndex, int childLocalIndex) = childrenAndLocals[i];
                var childNode = ConvertGimFoldersToAssimpNodes(gimData, childIndex, childLocalIndex, newScene, elemToObjDict);
                if(childNode != null) childrenAssimp.Add(childNode);
                WriteStatus?.EntityCompletedCallback(ASSIMP_NODE_WRITE, 100);
            }

            return newScene;
        }

        private Node ConvertGimFoldersToAssimpNodes(GimData gimData, int elementIndex, int localIndex, Scene newScene, Dictionary<uint, uint> elemToObj)
        {
            GimElement gimElement = gimData.Elements[elementIndex];

            // only accept document elements
            if (gimElement.ElementType != GimElementType.D_folder &&
                gimElement.ElementType != GimElementType.D_root &&
                gimElement.ElementType != GimElementType.D_part &&
                gimElement.ElementType != GimElementType.ExternalReference)
                return null;

            // only accept elements that have children
            if(gimElement.Elements.Count == 0)
                return null;

            Node node = GetNodeStructure(gimData, elementIndex, localIndex, elemToObj);

            //
            // Element is a folder: we simply add an empty node that will have child nodes later
            //

            if (gimElement.ElementType == GimElementType.D_folder || gimElement.ElementType == GimElementType.D_root)
            {
                var childrenAndLocals = gimElement.GetAllChildrenIndexesOrdered(elementIndex, gimData);
                for (int i = 0; i < gimElement.Elements.Count; i++)
                {
                    (int childIndex, int childLocalIndex) = childrenAndLocals[i];

                    var childNode = ConvertGimFoldersToAssimpNodes(gimData, childIndex, childLocalIndex, newScene, elemToObj);
                    if (childNode != null) node.Children.Add(childNode);
                }
                return node;
            }

            //
            // Element is a part: we add a node and create a mesh for it, or add an existing mesh
            //

            else if (gimElement.ElementType == GimElementType.D_part || gimElement.ElementType == GimElementType.ExternalReference)
            {
                PopulateNodeFromPart(gimData, newScene, node, gimElement);
            }

            return node;
        }

        private static Node GetNodeStructure(GimData gimData, int elementIndex, int localIndex, Dictionary<uint, uint> elemToObj)
        {
            GimElement gimElement = gimData.Elements[elementIndex];

            Node node = new();
            // safety check, as root elements dont have locals. Additionally if no local is selected (localindex = -1), a default local is used
            GimLocal local = gimElement.ElementType != GimElementType.D_root && localIndex != -1 ?
                gimElement.Locals[localIndex] : new();

            // Checking if the node has an associated GimObject for name and metadata
            bool hasObj = elemToObj.TryGetValue((uint)elementIndex, out uint objectIndex);
            string nodeName = hasObj ? gimData.Objects[(int)objectIndex].Name : "";
            node.Name = nodeName;

            node.Transform = GetTransformFromLocal(local);
            return node;
        }

        private void PopulateNodeFromPart(GimData gimData, Scene newScene, Node node, GimElement gimElement)
        {
            for (int i = 0; i < gimElement.Elements.Count; i++)
            {
                var childElementIndex = gimElement.Elements[i];
                var childElement = gimData.Elements[(int)childElementIndex];
                if (childElement.ElementType != GimElementType.R_Poly)
                    continue;

                var gimPolyMesh = childElement;

                foreach (int gimPolygonIndex in gimPolyMesh.Elements)
                {
                    var gimPolygon = gimData.Elements[(int)gimPolygonIndex];

                    // mesh has been created before, we simply add the index of the mesh
                    if (ElementMeshesByID.TryGetValue(gimPolygonIndex, out int assimpMeshIndex))
                    {
                        node.MeshIndices.Add(assimpMeshIndex);
                        continue;
                    }

                    int assimpMaterialIndex;
                    // Checking if the mesh has a material
                    if (gimPolygon.Elements.Count != 0)
                    {
                        int gimMaterialIndex = (int)gimPolygon.Elements[0];
                        // If the material has been inserted before, we just reference its index
                        if (MaterialsByID.TryGetValue(gimMaterialIndex, out assimpMaterialIndex))
                        {
                        }
                        // else, we create a new material and save its index for future use
                        else
                        {
                            GimMaterial gimMaterial = gimData.Elements[gimMaterialIndex].AsMaterial();
                            var assimpMaterial = GetAssimpMaterialFromGimMaterial(gimMaterial);
                            assimpMaterialIndex = newScene.Materials.Count;
                            newScene.Materials.Add(assimpMaterial);

                            MaterialsByID[gimMaterialIndex] = assimpMaterialIndex;
                        }
                    }
                    // In the case of no pre-existing material, we just reference the default material
                    else
                        assimpMaterialIndex = MaterialsByID[0];

                    // We create the new mesh
                    var createdMesh = CreateAssimpMeshFromGimPoly(gimData, gimPolygonIndex, assimpMaterialIndex);

                    // We save the mesh and its index
                    assimpMeshIndex = newScene.Meshes.Count;
                    newScene.Meshes.Add(createdMesh);
                    ElementMeshesByID[gimPolygonIndex] = assimpMeshIndex;

                    // Add the mesh to node
                    node.MeshIndices.Add(assimpMeshIndex);
                }
            }
        }

        // TODO: pass the local as parameter (or the element + (int) local index) instead of passing the element alone
        private static Matrix4x4 GetTransformFromLocal(GimLocal local)
        {
            Matrix matrix = new(local);
            Matrix leftMatrix = new(
                new double[] { 1, 0, 0 },
                new double[] { 0, 0, 1 },
                new double[] { 0, -1, 0 }
                );
            Matrix rightMatrix = new(
                new double[] { 1, 0, 0 },
                new double[] { 0, 0, -1 },
                new double[] { 0, 1, 0 }
                );
            Matrix finalTransform = leftMatrix * matrix * rightMatrix;
            var gimElementLocals = local;
            Vect offset = new Vect(gimElementLocals.Px, gimElementLocals.Py, gimElementLocals.Pz);
            Vect finalOffset = offset;


            (double r, double p, double y) = matrix.GetRPY();
            Matrix4x4 transform = Matrix4x4.FromEulerAnglesXYZ((float)(r),(float)(p),(float)(y));

            //Applying Offset First
            transform.A4 = (float)finalOffset[0];
            transform.B4 = (float)finalOffset[1];
            transform.C4 = (float)finalOffset[2];
            //Applying Locals to the X axis
            //transform.A1 = (float)finalTransform[0][0];
            //transform.A2 = (float)finalTransform[0][1];
            //transform.A3 = (float)finalTransform[0][2];
            ////Applying Locals to the Y axis
            //transform.B1 = (float)finalTransform[1][0];
            //transform.B2 = (float)finalTransform[1][1];
            //transform.B3 = (float)finalTransform[1][2];
            ////Applying Locals to the Z axis
            //transform.C1 = (float)finalTransform[2][0];
            //transform.C2 = (float)finalTransform[2][1];
            //transform.C3 = (float)finalTransform[2][2];   


            ////Applying Offset First
            transform.A4 = (float)gimElementLocals.px;
            transform.B4 = (float)gimElementLocals.py;
            transform.C4 = (float)gimElementLocals.pz;
            //Applying Locals to the X axis
            transform.A1 = (float)gimElementLocals.xx;
            transform.A2 = (float)gimElementLocals.xy;
            transform.A3 = (float)gimElementLocals.xz;
            //Applying Locals to the Y axis
            transform.B1 = (float)gimElementLocals.yx;
            transform.B2 = (float)gimElementLocals.yy;
            transform.B3 = (float)gimElementLocals.yz;
            //Applying Locals to the Z axis
            transform.C1 = (float)gimElementLocals.zx;
            transform.C2 = (float)gimElementLocals.zy;
            transform.C3 = (float)(gimElementLocals.zz);
            transform.D4 = 1f;
            return transform;
        }

        private Mesh CreateAssimpMeshFromGimPoly(GimData gimData, int elementIndex, 
            int assimpMaterialIndex)
        {
            GimElement gimElement = gimData.Elements[elementIndex];
            var currentMesh = new Mesh();
            currentMesh.SetIndices(gimElement.ElementInts, 3);

            for (int i = 0; i < gimElement.ElementDoubles.Length; i += 3)
            {
                Vector3D currentVect = new(
                    (float)gimElement.ElementDoubles[i], 
                    (float)gimElement.ElementDoubles[i + 1], 
                    (float)gimElement.ElementDoubles[i + 2]);
                currentMesh.Vertices.Add(currentVect);
            }

            currentMesh.MaterialIndex = assimpMaterialIndex;
            return currentMesh;
        }

        private static Material GetAssimpMaterialFromGimMaterial(GimMaterial gimMaterialElement)
        {
            Material assimpMaterial = new();
            //Todo: create function that returns a assimp colour from a gimmaterial

            //Ambient colors
            var argbGimColorAmbient = GimColour.FromInt(gimMaterialElement.AmbientColour);
            var assimpColorAmbient = assimpMaterial.ColorAmbient;
            assimpColorAmbient.A = argbGimColorAmbient.alpha;
            assimpColorAmbient.B = argbGimColorAmbient.blue;
            assimpColorAmbient.G = argbGimColorAmbient.green;
            assimpColorAmbient.R = argbGimColorAmbient.red;
            assimpMaterial.ColorAmbient = assimpColorAmbient;
            //Diffuse colors && Transparency Factor && Opacity

            var argbGimColorDiffuse = GimColour.FromInt(gimMaterialElement.DiffuseColour);
            var assimpColorDiffuse = assimpMaterial.ColorDiffuse;
            assimpColorDiffuse.A = argbGimColorDiffuse.alpha;
            assimpColorDiffuse.B = argbGimColorDiffuse.blue;
            assimpColorDiffuse.G = argbGimColorDiffuse.green;
            assimpColorDiffuse.R = argbGimColorDiffuse.red;
            assimpMaterial.ColorDiffuse = assimpColorDiffuse;
            assimpMaterial.Opacity = argbGimColorDiffuse.alpha;
            assimpMaterial.TransparencyFactor = 1 - argbGimColorDiffuse.alpha;
            //Specular colors

            var argbGimColorSpecular = GimColour.FromInt(gimMaterialElement.SpecularColour);
            var assimpColorSpecular = assimpMaterial.ColorSpecular;
            assimpColorSpecular.A = argbGimColorSpecular.alpha;
            assimpColorSpecular.B = argbGimColorSpecular.blue;
            assimpColorSpecular.G = argbGimColorSpecular.green;
            assimpColorSpecular.R = argbGimColorSpecular.red;
            assimpMaterial.ColorSpecular = assimpColorSpecular;

            //Emission colors

            var argbGimColorEmission = GimColour.FromInt(gimMaterialElement.EmissionColour);
            var assimpColorEmission = assimpMaterial.ColorEmissive;
            assimpColorEmission.A = argbGimColorEmission.alpha;
            assimpColorEmission.B = argbGimColorEmission.blue;
            assimpColorEmission.G = argbGimColorEmission.green;
            assimpColorEmission.R = argbGimColorEmission.red;
            assimpMaterial.ColorEmissive = assimpColorEmission;

            //Shininess

            assimpMaterial.Shininess = gimMaterialElement.Shininess;
            assimpMaterial.IsTwoSided = true;
            //assimpMaterial.TransparencyFactor = argbGimColorDiffuse.alpha; 

            return assimpMaterial;
        }

        private static GimMaterial GetDefaultGIMmaterial()
        {
            GimMaterial gimMaterial = new();

            GimColour diffuseRGB = new(1f, 0.8f, 0.8f, 0.8f);
            int diffuseINT = diffuseRGB.ToInt();
            gimMaterial.DiffuseColour = diffuseINT;
            GimColour baseRGB = new(1f, 0.8f, 0.8f, 0.8f);
            int baseINT = baseRGB.ToInt();
            gimMaterial.BaseColour = baseINT;

            return gimMaterial;
        }

    }
}
