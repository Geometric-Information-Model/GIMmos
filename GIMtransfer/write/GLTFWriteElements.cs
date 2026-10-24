using GIMcompress;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using System;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace GIMtransfer
{
	static class GLTFWriteElements
    {
        //internal static ModelRoot CreateGLTF(GimData gimData, int element)
        //{
        //    // SharpGLTF.Geometry.MeshBuilder
        //    MeshBuilder<VertexPosition> mesh = new MeshBuilder<VertexPosition>("mesh");
        //    var material1 = new MaterialBuilder().WithDoubleSide(true).WithMetallicRoughnessShader().WithChannelParam("BaseColor", new Vector4(1, 1, 1, 1));
        //    var primitive = mesh.UsePrimitive(material1);


        //    for (int i = 0; i < gimData.Elements[element].ElementInts.Length; i += 3)
        //    {
        //        int vertexA = gimData.Elements[element].ElementInts[i] * 3;
        //        int vertexB = gimData.Elements[element].ElementInts[i + 1] * 3;
        //        int vertexC = gimData.Elements[element].ElementInts[i + 2] * 3;

        //        primitive.AddTriangle(new VertexPosition(
        //            (float)gimData.Elements[element].ElementDoubles[vertexA] * 1000,
        //            (float)gimData.Elements[element].ElementDoubles[vertexA + 1] * 1000,
        //            (float)gimData.Elements[element].ElementDoubles[vertexA + 2] * 1000
        //            ),
        //            new VertexPosition(
        //            (float)gimData.Elements[element].ElementDoubles[vertexB] * 1000,
        //            (float)gimData.Elements[element].ElementDoubles[vertexB + 1] * 1000,
        //            (float)gimData.Elements[element].ElementDoubles[vertexB + 2] * 1000
        //            ),
        //            new VertexPosition(
        //            (float)gimData.Elements[element].ElementDoubles[vertexC] * 1000,
        //            (float)gimData.Elements[element].ElementDoubles[vertexC + 1] * 1000,
        //            (float)gimData.Elements[element].ElementDoubles[vertexC + 2] * 1000
        //            ));
        //    }

        //    SceneBuilder scene = new SceneBuilder();
        //    scene.AddRigidMesh(mesh, Matrix4x4.Identity);

        //    return scene.ToGltf2();
        //}
        /// <summary>
        /// Recursively constructs a GLTF structure for a given element in GimData, handling its children, materials, and transformations.
        /// </summary>
        /// <remarks>
        /// The method processes GimData elements, considering only specific types (like D_root, D_link, D_part, etc.). It performs the following steps:
        /// 
        /// 1. Checks the element type of the current GimElement. If the type is not one of the specified types worth processing, the method returns early.
        /// 
        /// 2. Initializes the transformation matrix and offset vector if they are null.
        /// 
        /// 3. Determines the appropriate material for the current element:
        ///    - If the element type is D_part, it searches for a potential material element among its children.
        ///    - It either uses this found material or defaults to the material passed into the method.
        /// 
        /// 4. Iterates through each local transformation (GimLocal) of the current element:
        ///    - Skips locals that do not match the parent hierarchy.
        ///    - Combines the current local transformation with the total transformation passed into the method.
        /// 
        /// 5. For elements of type R_Poly:
        ///    - Creates a new mesh for the polygon data and converts it to GLB format.
        ///    - Adds this mesh to the parent mesh.
        /// 
        /// 6. For other element types:
        ///    - Iterates over the child elements.
        ///    - Recursively calls itself for each child, passing the updated materials, transformations, and mesh.
        /// 
        /// This recursive approach builds the entire GLTF structure, including geometry and material data, following the hierarchy and transformations specified in GimData.
        /// </remarks>
        /// <param name="gimData">The GimData containing the elements to be processed.</param>
        /// <param name="elId">The ID of the current element being processed.</param>
        /// <param name="parentId">The ID of the parent element in the hierarchy.</param>
        /// <param name="mesh">The MeshBuilder instance for constructing mesh data.</param>
        /// <param name="scene">The SceneBuilder instance for building the overall scene.</param>
        /// <param name="currentMaterial">The current MaterialBuilder for applying materials.</param>
        /// <param name="totalMatrix">The cumulative transformation matrix.</param>
        /// <param name="totalOffset">The cumulative offset vector.</param>
        /// <param name="childObj">The name of the child object being processed.</param>
        /// <param name="scale">The scale factor applied to the model.</param>
        private static void CreateGTLFRecursive(GimData gimData, uint parentId, uint elId, 
            MeshBuilder<VertexPosition> mesh, SceneBuilder scene, MaterialBuilder currentMaterial,
            Matrix totalMatrix = null, Vect totalOffset = null, string childObj = "default", double scale = 1)
        {
            GimElement currentElement = gimData.Elements[(int)elId];

            GimElementType eleType = currentElement.ElementType;

            // only valid types worth checking 
            if(eleType != GimElementType.D_root /*&& eleType != GimElementType.D_link*/
                && eleType != GimElementType.D_part && eleType != GimElementType.D_folder
                && eleType != GimElementType.R_Poly && eleType != GimElementType.P_polygon
                && eleType != GimElementType.P_polyline
                )
            {
                return;
            }

            if(totalMatrix == null || totalOffset == null)
            {
                totalMatrix = Matrix.Ge3DUnitMatrix();
                totalOffset = new Vect(0, 0, 0);
            }

            MaterialBuilder nextMaterial = null;

            if (currentElement.ElementType == GimElementType.D_part)
            {
                GimElement potentialMaterialElement = null;
                foreach(var childId in currentElement.Elements)
                {
                    GimElement child = gimData.Elements[(int)childId];
                    if(child.ElementType == GimElementType.Material)
                    {
                        potentialMaterialElement = child;
                        break;
                    }
                }


                if(potentialMaterialElement == null)
                {
                    nextMaterial = currentMaterial;
                }
                else
                {
                    nextMaterial = GetMaterialBuilderFromMaterial(potentialMaterialElement.AsMaterial(), nextMaterial);
                }
            }
            else
            {
                nextMaterial = currentMaterial;
            }

                
            foreach(GimLocal local in currentElement.Locals)
            {

                if (local.Parent != parentId) continue; // following the correct hierarchy

                Matrix currentMatrix = new Matrix(local);
                Vect currentOffset = new Vect(local.Px, local.Py, local.Pz);

                Matrix nextMatrix = totalMatrix * currentMatrix;
                Vect nextVect = totalMatrix * currentOffset + totalOffset;

                if(currentElement.ElementType == GimElementType.R_Poly)
                {
                    MeshBuilder<VertexPosition> childMesh = new MeshBuilder<VertexPosition>(childObj);
                    ConvertPolygonsToGLB(gimData, elId, nextMaterial, childMesh, scene, nextMatrix, nextVect, scale);
                    mesh.AddMesh(childMesh);
                }

                else
                {
                    foreach(uint childId in currentElement.Elements)
                    {
                        foreach (GimObject obj in gimData.Objects)
                        {
                            if (obj.Element == childId)
                            {
                                childObj = obj.Name; break;
                            }
                        }
                         CreateGTLFRecursive(gimData, elId, childId, mesh, scene, nextMaterial, nextMatrix, nextVect, childObj, scale);
                    }
                }

            }

        }

        /// <summary>
        /// Constructs a GLTF (GL Transmission Format) model from given GimData, focusing on a specified element and its children.
        /// </summary>
        /// <remarks>
        /// The method carries out several key steps to construct the GLTF model:
        /// 
        /// 1. Initializes a SceneBuilder to manage the overall scene construction.
        /// 2. Creates a default material using a MaterialBuilder, setting it as double-sided with a metallic-roughness shader and a specific base color.
        /// 3. Identifies the parent element in GimData using the provided element ID.
        /// 4. Determines the name of the GimObject associated with the parent element, if available.
        /// 5. Creates a MeshBuilder to build the mesh for the GLTF model, naming it after the parent object.
        /// 6. Iterates over each child element ID of the parent element:
        ///    - For each child element, finds its corresponding GimObject name.
        ///    - Calls `CreateGTLFRecursive` for each child, passing parameters including the mesh and scene builders, the default material, and the scale factor.
        /// 7. Constructs a NodeBuilder for the parent object and adds the built mesh to the scene under this node.
        /// 
        /// After handling the parent and its children, the method iterates over all elements in the GimData:
        /// 8. For each element of type 'R_Poly', performs conversions of its polygons to GLB format, applying materials as needed.
        ///    - Handles cases where GimElement has locals defined and cases where no locals are present.
        /// 
        /// Finally, converts the built scene to GLTF format (version 2) and returns the resulting ModelRoot object.
        /// 
        /// Note: Some code blocks, including matrix transformations, are commented out, indicating potential functionality not currently in use.
        /// </remarks>
        /// <param name="gimData">The GimData to be used for GLTF model creation.</param>
        /// <param name="elemId">The ID of the element in GimData to focus on, defaulted to 0.</param>
        /// <param name="scale">Scale factor for the model, defaulted to 1.</param>
        /// <returns>A ModelRoot object representing the constructed GLTF model.</returns>
        internal static ModelRoot CreateGLTF(GimData gimData, uint elemId = 0, double scale = 1, bool useFirstLocal = false)
        {
            // SharpGLTF.Geometry.MeshBuilder
            //NodeBuilder node = new NodeBuilder(objName);

           // InstanceBuilder instance = new InstanceBuilder(objName);

            SceneBuilder scene = new SceneBuilder();

            MaterialBuilder defaultMaterial = new MaterialBuilder().WithDoubleSide(true).WithMetallicRoughnessShader().WithChannelParam("BaseColor", new Vector4(1, 1, 1, 1));

            GimElement parentElement = gimData.Elements[(int)elemId];

            string objName = "GIMobject";
            foreach (GimObject obj in gimData.Objects)
            {
                if (obj.Element == elemId)
                {
                    objName = obj.Name; break;
                }
            }

            MeshBuilder<VertexPosition> mesh = new MeshBuilder<VertexPosition>(objName);

            var elemToObj = gimData.GetElementToObjectDict();

            Matrix startingMatrix = Matrix.Ge3DUnitMatrix();
            Vect startingOffset = new(0, 0, 0);
            foreach (uint childId in parentElement.Elements)
            {
                // Getting object's name
                string childObj = "default";
                bool success = elemToObj.TryGetValue(childId, out var respectiveObj);
                if (success) childObj = gimData.Objects[(int)respectiveObj].Name;

                GimElement childElement = gimData.Elements[(int)childId];
                CreateGTLFRecursive(gimData, elemId, childId, mesh, scene, defaultMaterial, startingMatrix, startingOffset, childObj, scale: scale);
            }

            NodeBuilder mainNode = new NodeBuilder
            {
                Name = objName
            };
            scene.AddRigidMesh(mesh, mainNode).WithName(objName);

            //scene.AddRigidMesh(mesh, Matrix4x4.Identity);

            return scene.ToGltf2();
        }

        private static MaterialBuilder GetMaterialBuilderFromMaterial(GimMaterial materialElement, MaterialBuilder material = null)
        {
            material = new MaterialBuilder().WithDoubleSide(true);

            GimColour baseColor = GimColour.FromInt(materialElement.BaseColour);
            material = material.WithBaseColor(
                new Vector4(baseColor.red, baseColor.green, baseColor.blue, baseColor.alpha));

            GimColour diffuseColor = GimColour.FromInt(materialElement.DiffuseColour);
            //partMaterial = partMaterial.WithDiffuse(
            //    new Vector4(diffuseColor.red, diffuseColor.green, diffuseColor.blue, diffuseColor.alpha));

            GimColour specularColor = GimColour.FromInt(materialElement.SpecularColour);
            //partMaterial = partMaterial.WithSpecularGlossiness(
            //    new Vector3(specularColor.red, specularColor.green, specularColor.blue));


            GimColour emissionColor = GimColour.FromInt(materialElement.EmissionColour);

            // For whatever reason, the DLLs sometimes don't see this function and throw a "missingMethodException"
            // I have tried everything in my power to fix this, and yet it refuses to be fixed
            // So for now, I'm calling the body of the function directly, rather than the function itself
            // ~ Anis

            //material = material.WithEmissive(
            //    new Vector3(emissionColor.red, emissionColor.green, emissionColor.blue));
            float strength = 1;
            material = material.WithChannelParam(KnownChannel.Emissive, KnownProperty.EmissiveStrength, strength);
            material = material.WithChannelParam(KnownChannel.Emissive, KnownProperty.RGB,
                new Vector3(emissionColor.red, emissionColor.green, emissionColor.blue));

            return material;

        }

        private static void ConvertPolygonsToGLB(GimData gimData, uint polyRepIndex, MaterialBuilder partMaterialBuilder, MeshBuilder<VertexPosition> mesh, SceneBuilder scene, Matrix transformationMatrix = null, Vect offsetVect = null, double scale = 1)
        {
            
            if (partMaterialBuilder == null)
            {
                partMaterialBuilder = new MaterialBuilder().WithDoubleSide(true).WithMetallicRoughnessShader().WithChannelParam("BaseColor", new Vector4(1, 1, 1, 1));
            }

            GimElement polyRepElement = gimData.Elements[(int)polyRepIndex];

            foreach (int polyChild in polyRepElement.Elements)
            {
                GimElement polyElement = gimData.Elements[polyChild];
                var material = partMaterialBuilder;

                if (polyElement.Elements.Count > 0)
                {
                    GimMaterial polyMaterialElement = gimData.Elements[(int)polyElement.Elements[0]].AsMaterial();
                    material = GetMaterialBuilderFromMaterial(polyMaterialElement, material);
                }

                var primitive = mesh.UsePrimitive(material);

                if (polyElement.ElementType == GimElementType.P_polygon) for (int i = 0; i < polyElement.ElementInts.Length; i += 3)
                    {
                        int vertexA = polyElement.ElementInts[i] * 3;
                        int vertexB = polyElement.ElementInts[i + 1] * 3;
                        int vertexC = polyElement.ElementInts[i + 2] * 3;

                        Vect A = new Vect(
                            polyElement.ElementDoubles[vertexA],
                            polyElement.ElementDoubles[vertexA + 1],
                            polyElement.ElementDoubles[vertexA + 2]
                        );

                        Vect B = new Vect(
                            polyElement.ElementDoubles[vertexB],
                            polyElement.ElementDoubles[vertexB + 1],
                            polyElement.ElementDoubles[vertexB + 2]
                        );

                        Vect C = new Vect(
                            polyElement.ElementDoubles[vertexC],
                            polyElement.ElementDoubles[vertexC + 1],
                            polyElement.ElementDoubles[vertexC + 2]
                        );

                        A = transformationMatrix * A + offsetVect;
                        B = transformationMatrix * B + offsetVect;
                        C = transformationMatrix * C + offsetVect;

                        A = scale * A;
                        B = scale * B;
                        C = scale * C;

                        // Cadex (and we) work with default length unit of milimeters, gltf works with a default length unit of meters!
                        primitive.AddTriangle(new VertexPosition(
                            (float)A[0],
                            (float)A[1],
                            (float)A[2]
                            ),
                            new VertexPosition(
                            (float)B[0],
                            (float)B[1],
                            (float)B[2]
                            ),
                            new VertexPosition(
                            (float)C[0],
                            (float)C[1],
                            (float)C[2]
                            ));

                        //new VertexPosition(
                        //(float)polyElement.ElementDoubles[vertexC] * 1000,
                        //(float)polyElement.ElementDoubles[vertexC + 1] * 1000,
                        //(float)polyElement.ElementDoubles[vertexC + 2] * 1000
                        //));
                    }
                else if (polyElement.ElementType == GimElementType.P_polyline)
                {
                    //int startIndex = 0;
                    //for (int i = 0; i < polyElement.ElementInts.Length; i++)
                    //{
                    //    int vertexCount = polyElement.ElementInts[i];

                    //    int index = startIndex;
                    //    Vect firstPoint = new Vect( 
                    //        polyElement.ElementDoubles[index],
                    //        polyElement.ElementDoubles[index + 1],
                    //        polyElement.ElementDoubles[index + 2]
                    //    );

                    //    Vect A = firstPoint;
                    //    A = transformationMatrix * A + offsetVect;
                    //    for(int j = 3; j < vertexCount * 3; j += 3)
                    //    {
                    //        index = startIndex + j;
                    //        Vect B = new Vect(
                    //            polyElement.ElementDoubles[index],
                    //            polyElement.ElementDoubles[index + 1],
                    //            polyElement.ElementDoubles[index + 2]
                    //            );

                    //        B = transformationMatrix * B + offsetVect;

                    //        primitive.AddLine(
                    //            new VertexPosition(
                    //                (float)A[0] * 1000,
                    //                (float)A[1] * 1000,
                    //                (float)A[2] * 1000
                    //                ),
                    //            new VertexPosition(
                    //                (float)B[0] * 1000,
                    //                (float)B[1] * 1000,
                    //                (float)B[2] * 1000
                    //                )
                    //            ) ;

                    //        A = B;
                    //    }

                    //    startIndex += vertexCount * 3;

                    //}
                }



                if(transformationMatrix == null)
                {
                    transformationMatrix = Matrix.Ge3DUnitMatrix();
                }

                if(offsetVect == null)
                {
                    offsetVect = new Vect(0, 0, 0);
                }

                Matrix4x4 finalTransformation = new Matrix4x4();
                finalTransformation.M11 = (float)transformationMatrix[0][0];
                finalTransformation.M12 = (float)transformationMatrix[0][1];
                finalTransformation.M13 = (float)transformationMatrix[0][2];
                finalTransformation.M14 = 0;

                finalTransformation.M21 = (float)transformationMatrix[1][0];
                finalTransformation.M22 = (float)transformationMatrix[1][1];
                finalTransformation.M23 = (float)transformationMatrix[1][2];
                finalTransformation.M24 = 0;

                finalTransformation.M31 = (float)transformationMatrix[2][0];
                finalTransformation.M32 = (float)transformationMatrix[2][1];
                finalTransformation.M33 = (float)transformationMatrix[2][2];
                finalTransformation.M34 = 0;

                finalTransformation.M41 = (float)offsetVect[0] * 1000;
                finalTransformation.M42 = (float)offsetVect[1] * 1000;
                finalTransformation.M43 = (float)offsetVect[2] * 1000;
                finalTransformation.M44 = 1;

            }

        }

        private static GimMaterial FindPartMaterialElement(GimData gimData, int parentIndex)
        {
            GimElement part = gimData.Elements[parentIndex];

            foreach(uint childId in part.Elements)
            {
                GimElement childElement = gimData.Elements[(int)childId];
                if(childElement.ElementType == GimElementType.Material)
                {
                    return childElement.AsMaterial();
                }
            }

            return null;
        }

        private static VertexPosition GetVertex(GimElement element)
        {
            return new VertexPosition(
                            (float)element.locals[0].px * 1000,
                            (float)element.locals[0].py * 1000,
                            (float)element.locals[0].pz * 1000
                            );
        }
    }
}
