using GIMcompress;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMtransfer.read
{
    static class GLTFReadElements
    {
        /// <summary>
        /// Reads a GLB file to GimData, through the SharpGLTF Library
        /// </summary>
        /// <param name="path">The Path of the glb file</param>
        /// <returns></returns>
        public static GimData ReadGLBToGimData(string path)
        {
            var modelRoot = ReadGLB(path);
            return ConvertModelRootToGimData(modelRoot);
        }

        public static GimData ReadGLBToGimData(byte[] glbBytes)
        {
            var modelRoot = ModelRoot.ParseGLB(glbBytes);
            return ConvertModelRootToGimData(modelRoot);
        }

        /// <summary>
        /// Reads a GLB file into SharpGLTF's ModelRoot format
        /// </summary>
        /// <param name="path">The Path of the glb file</param>
        /// <returns></returns>
        public static ModelRoot ReadGLB(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                ModelRoot model = ModelRoot.ReadGLB(fs);
                return model;
            }
        }

        public static GimData ConvertModelRootToGimData(ModelRoot model)
        {
            GimData gimData = new();

            var meshes = model.LogicalMeshes;
            var nodes = model.LogicalNodes;

            gimData.Elements = new()
            {
                new()
                {
                    ElementType = GimElementType.D_root
                }
            };

            List<int> currentNodes = new();

            foreach (var node in nodes)
            {
                if(node.VisualParent == null)
                {
                    currentNodes.Add(node.LogicalIndex);
                }
            }

            Dictionary<int, int> nodeToElement = new Dictionary<int, int>
            {
                { -1, 0 }
            };

            while (currentNodes.Count > 0)
            {

                List<int> nextNodes = new();
                foreach (var nodeIndex in currentNodes)
                {
                    var node = nodes[nodeIndex];
                    GimData elementGimData = new()
                    {
                        Elements = new()
                        {
                            new()
                            {
                                ElementType = GimElementType.D_root,
                            }
                        }
                    };
                
                    GimElement nodeElement;
                    int elementIndex = 0;
                    if (node.Mesh == null && node.VisualChildren.Count() > 0)
                    {
                        // in this case, we will create a folder
                        nodeElement = new()
                        {
                            ElementType = GimElementType.D_folder
                        };
                        elementIndex = gimData.Elements.Count;
                        elementGimData.Elements.Add(nodeElement);
                        elementGimData.AddChildToParent(1, 0);

                        foreach(var childNode in node.VisualChildren)
                        {
                            nextNodes.Add(childNode.LogicalIndex);
                        }

                    }
                    else if (node.Mesh != null)
                    {
                        // in this case, we have a part
                        nodeElement = new()
                        {
                            ElementType = GimElementType.D_part
                        };
                        elementIndex = gimData.Elements.Count;
                        elementGimData.Elements.Add(nodeElement);
                        elementGimData.AddChildToParent(1, 0);

                        GimData meshData = ConvertGLBMeshToGimDataPolyRep(node.Mesh);
                        elementGimData.AddSecondaryGimData(meshData, 1, 1, 0);
                    }
                    else continue; // a node with no mesh or children is just degenerate


                    Matrix transformationMatrix = Matrix.Ge3DUnitMatrix();
                    Vect positionVect = new(0, 0, 0);

                    transformationMatrix[0][0] = node.LocalMatrix.M11;
                    transformationMatrix[0][1] = node.LocalMatrix.M12;
                    transformationMatrix[0][2] = node.LocalMatrix.M13;

                    transformationMatrix[1][0] = node.LocalMatrix.M21;
                    transformationMatrix[1][1] = node.LocalMatrix.M22;
                    transformationMatrix[1][2] = node.LocalMatrix.M23;

                    transformationMatrix[2][0] = node.LocalMatrix.M31;
                    transformationMatrix[2][1] = node.LocalMatrix.M32;
                    transformationMatrix[2][2] = node.LocalMatrix.M33;

                    positionVect[0] = node.LocalMatrix.M41;
                    positionVect[1] = node.LocalMatrix.M42;
                    positionVect[2] = node.LocalMatrix.M43;

                    transformationMatrix.ApplyToLocal(nodeElement.Locals[0]);
                    nodeElement.Locals[0].Px = positionVect[0];
                    nodeElement.Locals[0].Py = positionVect[1];
                    nodeElement.Locals[0].Pz = positionVect[2];



                    nodeToElement[node.LogicalIndex] = elementIndex;
                    int parentIndex = -1;
                    if (node.VisualParent != null) parentIndex = node.VisualParent.LogicalIndex;
                    int parentElementIndex = nodeToElement[parentIndex];

                    gimData.AddSecondaryGimData(elementGimData, 1, (uint)parentElementIndex, 0);

                }

                currentNodes = nextNodes;
            }

            return gimData;
        }

        public static GimData ConvertGLBMeshToGimDataPolyRep(Mesh mesh)
        {
            GimData gimData = new()
            {
                Elements = new()
                {
                    new()
                    {
                        ElementType = GimElementType.D_root,
                        Elements = new() { 1 }
                    },
                    new()
                    {
                        ElementType = GimElementType.R_Poly,
                        Locals = new() { new() { parent = 0 } }
                    }
                }
            };

            foreach(var primitive in mesh.Primitives)
            {
                // each primitive will be a polygon
                GimData primitiveData = GimData.CreateGimDataWithRoot();
                primitiveData.Elements.Add(new()
                {
                    ElementType = GimElementType.P_polygon,
                    Locals = new() { new() { parent = 0} }
                }) ;

                if (primitive.VertexAccessors.ContainsKey("POSITION"))
                {
                    List<double> elementDoubles = new();
                    var vertices = primitive.VertexAccessors["POSITION"].AsVector3Array();
                    foreach(var vertex in vertices)
                    {
                        elementDoubles.Add(vertex.X);
                        elementDoubles.Add(vertex.Y);
                        elementDoubles.Add(vertex.Z);
                    }

                    var vertexIndices = primitive.IndexAccessor.AsIndicesArray();

                    List<int> elementInts = new();
                    foreach(int index in vertexIndices)
                    {
                        elementInts.Add(index);
                    }

                    primitiveData.Elements[1].ElementDoubles = elementDoubles.ToArray();
                    primitiveData.Elements[1].ElementInts = elementInts.ToArray();

                    if (primitive.Material != null)
                    {
                        var material = primitive.Material;
                        var color = material.GetDiffuseColor(new(0.8f, 0.8f, 0.8f, 0));
                        GimMaterial materialElement = new() { ElementType = GimElementType.Material };
                        var gimColor = new GimColour(color.W, color.X, color.Y, color.Z);
                        materialElement.AmbientColour = new GimColour(1, 0, 0, 0).ToInt();
                        materialElement.BaseColour = gimColor.ToInt();
                        materialElement.DiffuseColour = gimColor.ToInt();
                        materialElement.SpecularColour = 255 << (6 * 4);
                        materialElement.EmissionColour = 0;
                        int materialElementIndex = primitiveData.Elements.Count;
                        primitiveData.Elements.Add(materialElement);
                        primitiveData.AddChildToParent((uint)materialElementIndex, 1);
                    }

                    gimData.AddSecondaryGimData(primitiveData, 1, 1, 0);

                }

            }
        
            return gimData;
        }

    }
}
