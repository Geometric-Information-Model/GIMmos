using GIMcompress;
using GIMocctCLI;
using GIMocctWrapper;
using System;
using System.Collections.Generic;
using TopoDSWrapper_EnumType = GIMocctWrapper.TopoDSWrapper_EnumType;

namespace GIMtransfer.read.GIMocctRead
{
    public static class GIMocctMesher
    {
        public static void MeshBrepElement(GimData gimData, GIMocctElement elementToMesh, uint partIndex,
            float red = 0.5f, float green = 0.5f, float blue = 0.5f, float alpha = 1f,
            double linearDeflection = 1.0, double angularDeflection = 1.0)
        {
            BrepMeshIncrementalWrapper brepmesh = new(elementToMesh.Shape, linearDeflection, false, angularDeflection, true);
            brepmesh.GetStatusFlag();
            brepmesh.Dispose();

            GimElement polygonElement = new GimElement() { elementType = GimElementType.P_polygon };
            List<int> elementInts = new();
            List<double> elementDoubles = new();
            int prevCount = 0;

            foreach (GIMocctElement face in CollectFacesForMeshing(elementToMesh))
                AddMeshFaceToPolygon(face, elementInts, elementDoubles, ref prevCount);

            polygonElement.elementDoubles = elementDoubles.ToArray();
            polygonElement.elementInts = elementInts.ToArray();

            //PolyRep Processing

            GimElement polyRepElement = new()
            {
                ElementType = GimElementType.R_Poly
            };
            uint polyRepIndex = (uint)gimData.Elements.Count;

            gimData.Elements.Add(polyRepElement);
            gimData.AddChildToParent(polyRepIndex, partIndex);

            //Polygon Processing

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);

            //Materials

            GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
            int gimMaterialIndex = gimData.Elements.Count;
            gimData.Elements.Add(gimMaterialElement);
            gimData.AddChildToParent((uint)gimMaterialIndex, (uint)polygonIndex);
            GIMocctReadElements.ApplyOCCTMaterialToGimElement((float)red, green, blue, alpha, gimMaterialElement);


            //var resultingMesh = PolygonRounder.ConstructTriangles(polygonElement);
        }

        /// <summary>Flattens solid / shell / compound-with-meshable-children into face elements (same traversal as previous nested loops).</summary>
        public static IEnumerable<GIMocctElement> CollectFacesForMeshing(GIMocctElement elementToMesh)
        {
            var topo = (TopoDSWrapper_EnumType)elementToMesh.Shape.ShapeType();
            switch (topo)
            {
                case TopoDSWrapper_EnumType.TopoDSWrapper_FACE:
                    yield return elementToMesh;
                    yield break;
                case TopoDSWrapper_EnumType.TopoDSWrapper_SHELL:
                    foreach (GIMocctElement faceChild in elementToMesh.Children)
                        yield return faceChild;
                    yield break;
                case TopoDSWrapper_EnumType.TopoDSWrapper_SOLID:
                case TopoDSWrapper_EnumType.TopoDSWrapper_COMPOUND:
                    foreach (GIMocctElement child in elementToMesh.Children)
                        foreach (GIMocctElement face in CollectFacesForMeshing(child))
                            yield return face;
                    yield break;
            }
        }

        public static void AddMeshFaceToPolygon(GIMocctElement faceChild, List<int> elementInts,
        List<double> elementDoubles, ref int prevCount)
        {
            Matrix4x4Wrapper transformMatrix;
            PolyTriangulationWrapper polyTrigWrapper = BrepToolWrapper.Triangulation((TopoDSFaceWrapper)faceChild.Shape, out transformMatrix);
            if(polyTrigWrapper == null)
            {
                return;
            }
            var nbNodes = polyTrigWrapper.NbNodes();
            var nbTriangles = polyTrigWrapper.NbTriangles();

            Matrix matrix = new(
                new double[] { transformMatrix.M11, transformMatrix.M12, transformMatrix.M13 },
                new double[] { transformMatrix.M21, transformMatrix.M22, transformMatrix.M23 },
                new double[] { transformMatrix.M31, transformMatrix.M32, transformMatrix.M33 }
                );
            Vect offset = new(transformMatrix.M14, transformMatrix.M24, transformMatrix.M34);

            for (int i = 1; i <= nbNodes; i++)
            {
                var occtNode = polyTrigWrapper.Node(i);
                Vect point = new(occtNode.X, occtNode.Y, occtNode.Z);
                Vect newPoint = matrix * point + offset;
                elementDoubles.AddRange((double[])newPoint);


            }
            for (int i = 1; i <= nbTriangles; i++)
            {
                elementInts.Add(polyTrigWrapper.GetTriangleValue(i, 1) - 1 + prevCount);
                elementInts.Add(polyTrigWrapper.GetTriangleValue(i, 2) - 1 + prevCount);
                elementInts.Add(polyTrigWrapper.GetTriangleValue(i, 3) - 1 + prevCount);
            }
            prevCount += nbNodes;

        }
    }
}
