using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GIMcompress;
using Assimp;
using GIMcompress.Shrinkwrap;
using GimUtilities;

namespace GIMtransfer.read
{
    public class AssimpReadElements
    {
        public const string ASSIMP_DISK_READ = "AssimpDiskRead";
        public const string ASSIMP_NODE_READ = "AssimpNodeRead";
        private GimProgressStatus ReadProgress;
        Dictionary<HashSet<int>, int> ElementIDByMeshIndices = new Dictionary<HashSet<int>, int>(HashSet<int>.CreateSetComparer());
        public AssimpReadElements(GimProgressStatus readProgress)
        {
            ReadProgress = readProgress;
        }
        public GimData ReadAssimpToGimData(string path, bool useExistingProgressPlan = false)
        {
            GimData gimData = new GimData();
            gimData.CreateRootIfNotExists();
            using AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFile(path, PostProcessPreset.TargetRealTimeMaximumQuality);
            Node rootNode = scene.RootNode;

            if (!useExistingProgressPlan)
            {
                int childNodeCount = rootNode.ChildCount;
                ReadProgress?.Create(new Dictionary<string, int>(){
                    { ASSIMP_DISK_READ, 100},
                    { ASSIMP_NODE_READ, childNodeCount != 0? childNodeCount * 100 : 100 },
                });
            }

            ReadProgress?.ForceCompleteTask(ASSIMP_DISK_READ);

            CreateFolders(rootNode, 0, gimData, scene, !useExistingProgressPlan);
            ReadProgress?.ForceCompleteTask(ASSIMP_NODE_READ);

            return gimData;
        }

        private void CreateFolders(Node child, int parentIndex, GimData gimData, Scene scene, bool reportNodeProgress = true)
        {
            var transformAssimp = child.Transform;
            if(child.Name.Contains("0GkhpndB96r98q9Zpfg0ks"))
            {
                string debug = "test";
            }
            // single folder with single mesh child => simplify into single part rather than folder/part
            if(child.HasChildren && child.Children.Count == 1 && child.Children[0].HasMeshes == true)
            {
                var innerChild = child.Children[0];
                var meshIndices = GetMeshIndices(innerChild);
                GimElement partElement;
                if (ElementIDByMeshIndices.TryGetValue(meshIndices, out int instanceElementID))
                {
                    partElement = gimData.Elements[instanceElementID];
                    gimData.AddChildToParent((uint)instanceElementID, (uint)parentIndex);
                    var transform = innerChild.Transform;
                    ApplyAssimpTransformToGimElement(transform, partElement.Locals[^1]);
                }
                else
                {
                    partElement = new() { elementType = GimElementType.D_part };
                    int partIndex = gimData.Elements.Count;
                    gimData.Elements.Add(partElement);
                    CreateMehes(innerChild, gimData, scene, parentIndex, partIndex);
                    ElementIDByMeshIndices[meshIndices] = partIndex;

                    if (gimData.Objects[^1].Element == partIndex) // to ensure we're renaming the correct object
                        gimData.Objects[^1].Name = child.Name;
                }



                var lastTransform = new Matrix(partElement.Locals[^1]); // modify the last local
                var lastOffset = new Vect(
                    partElement.Locals[^1].Px,
                    partElement.Locals[^1].Py,
                    partElement.Locals[^1].Pz
                    );

                var cumulativeLocal = new GimLocal();
                ApplyAssimpTransformToGimElement(transformAssimp, cumulativeLocal);
                var cumulativeTransform = new Matrix(cumulativeLocal);
                var cumulativeOffset = new Vect(
                    cumulativeLocal.Px,
                    cumulativeLocal.Py,
                    cumulativeLocal.Pz
                    );

                Matrix finalTransform = cumulativeTransform * lastTransform;
                Vect finalOffset = cumulativeTransform * lastOffset + cumulativeOffset;

                finalTransform.ApplyToLocal(partElement.Locals[^1]);
                finalOffset.SetLocalBasis(partElement.Locals[^1]);

                return;
            }

            GimElement folderElement = new() { elementType = GimElementType.D_folder };
            int folderIndex = gimData.Elements.Count;
            gimData.Elements.Add(folderElement);
            GimObject folderObject = GimObjectFactory.Create((uint)folderIndex, child.Name);
            gimData.Objects.Add(folderObject);
            gimData.AddChildToParent((uint)folderIndex, (uint)parentIndex);
            ApplyAssimpTransformToGimElement(transformAssimp, folderElement.Locals[^1]);

            if (child.HasChildren)
            {
                foreach (var innerChild in child.Children)
                {
                    // when something has children, we treat it as a folder
                    if (innerChild.HasChildren)
                        CreateFolders(innerChild, folderIndex, gimData, scene, reportNodeProgress);
                    // else we add the simple meshes
                    else if (innerChild.HasMeshes)
                    {
                        var meshIndices = GetMeshIndices(innerChild);
                        if(ElementIDByMeshIndices.TryGetValue(meshIndices, out int instanceElementID))
                        {
                            GimElement instanceElement = gimData.Elements[instanceElementID];
                            gimData.AddChildToParent((uint)instanceElementID, (uint)folderIndex);
                            var transform = innerChild.Transform;
                            ApplyAssimpTransformToGimElement(transform, instanceElement.Locals[^1]);
                            continue;
                        }
                        GimElement partElement = new() { elementType = GimElementType.D_part };
                        int partIndex = gimData.Elements.Count;
                        gimData.Elements.Add(partElement);
                        CreateMehes(innerChild, gimData, scene, folderIndex,partIndex);
                        ElementIDByMeshIndices[meshIndices] = partIndex;
                    }
                    if(reportNodeProgress && parentIndex == 0) ReadProgress?.EntityCompletedCallback(ASSIMP_NODE_READ, 100);
                }
            }
            if(child.HasMeshes)
            {
                var meshIndices = GetMeshIndices(child);
                if (ElementIDByMeshIndices.TryGetValue(meshIndices, out int instanceElementID))
                {
                    GimElement instanceElement = gimData.Elements[instanceElementID];
                    gimData.AddChildToParent((uint)instanceElementID, (uint)folderIndex);
                    var transform = child.Transform;
                    //ApplyAssimpTransformToGimElement(transform, instanceElement.Locals[^1]);

                }
                else
                {
                    GimElement partElement = new() { elementType = GimElementType.D_part };
                    int partIndex = gimData.Elements.Count;
                    gimData.Elements.Add(partElement);
                    CreateMehes(child, gimData, scene, folderIndex, partIndex, true);
                    ElementIDByMeshIndices[meshIndices] = partIndex;
                    if (reportNodeProgress && parentIndex == 0) ReadProgress?.EntityCompletedCallback(ASSIMP_NODE_READ, 100);
                }
            }
        
            
        }

        private HashSet<int> GetMeshIndices(Node node)
        {
            var meshIndices = new HashSet<int>();
            foreach (var mesh in node.MeshIndices)
            {
                meshIndices.Add(mesh);
            }
            return meshIndices;
        }
        // TODO: pass a local as parameter instead of element; shouldn't access the first local of an element
        private static void ApplyAssimpTransformToGimElement(Assimp.Matrix4x4 transform, GimLocal gimElementLocals)
        {
            //Applying Offset First

            gimElementLocals.px = transform.A4;
            gimElementLocals.py = transform.B4;
            gimElementLocals.pz = transform.C4;
            //Applying transform to the X axis
            gimElementLocals.xx = transform.A1;
            gimElementLocals.xy = transform.A2;
            gimElementLocals.xz = transform.A3;
            //Applying transform to the Y axis
            gimElementLocals.yx = transform.B1;
            gimElementLocals.yy = transform.B2;
            gimElementLocals.yz = transform.B3;
            //Applying transform to the Z axis
            gimElementLocals.zx = transform.C1;
            gimElementLocals.zy = transform.C2;
            gimElementLocals.zz = transform.C3;
        }


        private void CreateMehes(Assimp.Node child, GimData gimData, Assimp.Scene scene, int folderIndex, int partIndex, bool isSubGeom = false)
        {
          
            var transformAssimp = child.Transform;
            // creating part element
            GimElement partElement = gimData.Elements[partIndex];
            GimObject partObject = GimObjectFactory.Create((uint)partIndex, isSubGeom ? child.Name + "_GEOMETRY" : child.Name);
            gimData.Objects.Add(partObject);
            gimData.AddChildToParent((uint)partIndex, (uint)folderIndex);
            if(!isSubGeom)
                ApplyAssimpTransformToGimElement(transformAssimp, partElement.Locals[^1]);
            // creatging polymesh element
            GimElement polyMeshElement = new() { elementType = GimElementType.R_Poly };
            int polyMeshIndex = gimData.Elements.Count;
            gimData.Elements.Add(polyMeshElement);
            gimData.AddChildToParent((uint)polyMeshIndex, (uint)partIndex);

            // reading triangles
            foreach (var mesh in child.MeshIndices)
            {

                var currentMesh = scene.Meshes[mesh];
                var face = currentMesh.Faces[0];
                if (face.Indices.Count < 3)
                    continue;
                GimElement polygonElement = new() { elementType = GimElementType.P_polygon };
                int polygonIndex = gimData.Elements.Count;
                gimData.Elements.Add(polygonElement);
                gimData.AddChildToParent((uint)polygonIndex, (uint)polyMeshIndex);
                List<Vect> points = new();
                List<Triangle> triangles = new();
                if (currentMesh.HasVertices && currentMesh.HasFaces)
                {

                    for (int i = 0; i < currentMesh.Vertices.Count; i++)
                    {
                        var currentVertex = currentMesh.Vertices[i];
                        points.Add(new(currentVertex.X, currentVertex.Y, currentVertex.Z));
                    }

                    for (int i = 0; i < currentMesh.Faces.Count; i++)
                    {
                        var currentTriangle = currentMesh.Faces[i];
                        var v1Indice = currentTriangle.Indices[0];
                        var v2Indice = currentTriangle.Indices[1];
                        var v3Indice = currentTriangle.Indices[2];
                        var v1 = points[v1Indice];
                        var v2 = points[v2Indice];
                        var v3 = points[v3Indice];
                        triangles.Add(new(v1, v2, v3));

                    }

                }

                TriangleMesh triangleMesh = new TriangleMesh();
                triangleMesh.Triangles = triangles;
                triangleMesh.ApplyToGimDataPolygon(polygonElement);
                // creating polygon element
                GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
                int gimMaterialIndex = gimData.Elements.Count;
                gimData.Elements.Add(gimMaterialElement);
                gimData.AddChildToParent((uint)gimMaterialIndex, (uint)polygonIndex);
                int assimpMaterialIndex = currentMesh.MaterialIndex;
                var materialsList = scene.Materials;
                var currentAssimpMaterial = materialsList[assimpMaterialIndex];
                //Applying Materials

                ApplyAssimpMaterialToGimElementMaterial(currentAssimpMaterial, gimMaterialElement);

                //CreateAssimpMeshFromGimPoly(polygonElement, scene, gimMaterialElement);

                
            }
            
        }


        private static void ApplyAssimpMaterialToGimElementMaterial(Assimp.Material assimpMaterial, GimMaterial gimMaterialElement)
        {
            //Ambient colors

            var colorAmbient = assimpMaterial.ColorAmbient;
            var aAmbient = colorAmbient.A;
            var bAmbient = colorAmbient.B;
            var gAmbient = colorAmbient.G;
            var rAmbient = colorAmbient.R;
            GimColour gimColourAmbient = new GimColour(aAmbient, rAmbient, gAmbient, bAmbient);
            int resultingAmbientColor = gimColourAmbient.ToInt();
            gimMaterialElement.AmbientColour = resultingAmbientColor;

            //Diffuse && Base colors

            var colorDiffuse = assimpMaterial.ColorDiffuse;
            float aDiffuse = colorDiffuse.A;
            float aBase = colorDiffuse.A;
            float bDiffuse = colorDiffuse.B;
            float bBase = colorDiffuse.B;
            float gDiffuse = colorDiffuse.G;
            float gBase = colorDiffuse.G;
            float rDiffuse = colorDiffuse.R;
            float rBase = colorDiffuse.R;
            if (aBase == 1 && assimpMaterial.Opacity != 1)
                aBase = assimpMaterial.Opacity;
            if (aDiffuse == 1 && assimpMaterial.Opacity != 1)
                aDiffuse = assimpMaterial.Opacity;
            //if (rBase == 0 && bBase == 0 && gBase == 0)
            //{
            //    rBase = rBase + 0.5f;
            //    bBase = bBase + 0.5f;
            //    gBase = gBase + 0.5f;
            //}
            GimColour gimColourDiffuse = new(aDiffuse, rDiffuse, gDiffuse, bDiffuse);

            GimColour gimColourBase = new(aBase, rBase, gBase, bBase);
            int resultingDiffuseColor = gimColourDiffuse.ToInt();
            int resultingBaseColor = gimColourBase.ToInt();


            gimMaterialElement.DiffuseColour = resultingDiffuseColor;
            gimMaterialElement.BaseColour = resultingBaseColor;

            //Specular colors

            var colorSpecular = assimpMaterial.ColorSpecular;
            var aSpecular = colorSpecular.A;
            var bSpecular = colorSpecular.B;
            var gSpecular = colorSpecular.G;
            var rSpecular = colorSpecular.R;
            GimColour gimColourSpecular = new(aSpecular, rSpecular, gSpecular, bSpecular);
            int resultingSpecularColor = gimColourSpecular.ToInt();
            gimMaterialElement.SpecularColour = resultingSpecularColor;

            //Emission Colors

            var colorEmissive = assimpMaterial.ColorEmissive;
            var aEmissive = colorEmissive.A;
            var bEmissive = colorEmissive.B;
            var gEmissive = colorEmissive.G;
            var rEmissive = colorEmissive.R;
            GimColour gimColourEmissive = new(aEmissive, rEmissive, gEmissive, bEmissive);
            int resultingEmissiveColor = gimColourEmissive.ToInt();
            gimMaterialElement.EmissionColour = resultingEmissiveColor;

            //Shininess

            var shininess = assimpMaterial.Shininess;
            gimMaterialElement.Shininess = shininess;
        }


    }
}
