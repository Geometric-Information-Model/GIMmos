using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace GIMcompress.Shrinkwrap
{

    internal class VertexDictionary
    {

        Dictionary<double, Dictionary<double, Dictionary<double, List<Triangle>>>> vertexDictionary = new();
        public List<Triangle> this[double x, double y, double z]
        {
            get
            {
                if (vertexDictionary.ContainsKey(x))
                {
                    var stack1 = vertexDictionary[x];
                    if (stack1.ContainsKey(y))
                    {
                        var stack2 = stack1[y];
                        if (stack2.ContainsKey(z))
                        {
                            var list = stack2[z];
                            if (list == null)
                            {
                                list = new List<Triangle>();
                                stack2[z] = list;
                            }

                            return list;
                        }
                        else
                        {
                            stack2.Add(z, new List<Triangle>());
                            return stack2[z];
                        }
                    }
                    else
                    {
                        stack1.Add(y, new());
                        stack1[y].Add(z, new List<Triangle>());

                        return stack1[y][z];
                    }
                }
                else
                {
                    vertexDictionary.Add(x, new());
                    vertexDictionary[x].Add(y, new());
                    vertexDictionary[x][y].Add(z, new List<Triangle>());

                    return vertexDictionary[x][y][z];
                }
            }
        }

        public List<Triangle> this[Vect vertex]
        {
            get
            {
                return this[vertex[0], vertex[1], vertex[2]];
            }
        }

    }

    public class PolygonRounder
    {
        public static double precision = 1;


        public static void RoundGimData(GimData gimData, double roundingStep)
        {
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement element = gimData.Elements[i];
                if (element.ElementType != GimElementType.P_polygon)
                    continue;
                
                // construct the triangle mesh
                TriangleMesh mesh = ConstructTriangles(element);
                // round the triangles according the the rounding step
                RoundPolygons(mesh, roundingStep);
                // remove triangles that turn into one point
                RemoveVertexTriangles(mesh);
                // remove triangles that turn into lines
                RemoveFlattenedAndDegenerate(mesh);
                // readd the mesh
                mesh.ApplyToGimDataPolygon(element);

            }
        }


        //constructs a triangle mesh using the Triangle class

        public static TriangleMesh ConstructTriangles(GimElement gimElement, bool forceUnique = false)
        {
            Dictionary<int, List<Triangle>> triangleVertex = null;

            if (forceUnique)
            {
                triangleVertex = new(gimElement.ElementInts.Length / 3);
            }

            List<Triangle> triangleList = new(gimElement.ElementInts.Length / 3);

            TriangleMesh mesh = new TriangleMesh();

            for (int i = 0; i < gimElement.ElementInts.Length; i += 3)
            {
                int index1 = gimElement.ElementInts[i];
                int index2 = gimElement.ElementInts[i + 1];
                int index3 = gimElement.ElementInts[i + 2];

                Vect vertex1 = new Vect(
                    gimElement.ElementDoubles[3 * index1],
                    gimElement.ElementDoubles[3 * index1 + 1],
                    gimElement.ElementDoubles[3 * index1 + 2]
                    );

                Vect vertex2 = new Vect(
                    gimElement.ElementDoubles[3 * index2],
                    gimElement.ElementDoubles[3 * index2 + 1],
                    gimElement.ElementDoubles[3 * index2 + 2]
                    );

                Vect vertex3 = new Vect(
                    gimElement.ElementDoubles[3 * index3],
                    gimElement.ElementDoubles[3 * index3 + 1],
                    gimElement.ElementDoubles[3 * index3 + 2]
                    );

                if ((vertex1 == vertex2) || (vertex2 == vertex3) || (vertex3 == vertex1))
                {
                    continue;
                }

                Triangle triangle = new Triangle(vertex1, vertex2, vertex3, mesh);



                if (forceUnique)
                {
                    bool newTriangle = false;
                    if (!triangleVertex.ContainsKey(index1))
                    {
                        newTriangle = true;
                        triangleVertex.Add(index1, new List<Triangle>());
                    }
                    if (!triangleVertex.ContainsKey(index2))
                    {
                        newTriangle = true;
                        triangleVertex.Add(index2, new List<Triangle>());
                    }
                    if (!triangleVertex.ContainsKey(index3))
                    {
                        newTriangle = true;
                        triangleVertex.Add(index3, new List<Triangle>());
                    }

                    if (!newTriangle)
                    {
                        bool identicalFound = false;

                        foreach (Triangle other in triangleVertex[index1])
                        {
                            if (triangle.IsEqualTo(other))
                            {
                                identicalFound = true;
                                break;
                            }
                        }

                        if (identicalFound) continue;

                        foreach (Triangle other in triangleVertex[index2])
                        {
                            if (triangle.IsEqualTo(other))
                            {
                                identicalFound = true;
                                break;
                            }
                        }

                        if (identicalFound) continue;

                        foreach (Triangle other in triangleVertex[index3])
                        {
                            if (triangle.IsEqualTo(other))
                            {
                                identicalFound = true;
                                break;
                            }
                        }

                        if (identicalFound) continue;

                    }

                    triangleVertex[index1].Add(triangle);
                    triangleVertex[index2].Add(triangle);
                    triangleVertex[index3].Add(triangle);

                }
                triangleList.Add(triangle);
            }

            if (forceUnique)
            {
                mesh.Triangles = triangleList;
                mesh.Counter = triangleList.Count;
            }

            return mesh;
        }

        /// <summary>
        /// This function clips a GimData (presumably a scene) by minimum-maximum XY coordinates. 
        /// Only works with PolyMesh representations.
        /// </summary>
        /// <param name="gimData">The GimData to be clipped</param>
        /// <param name="minX">The minimum X value in millimeters</param>
        /// <param name="minY">The maximum X value in millimeters</param>
        /// <param name="maxX">The minimum Y value in millimeters</param>
        /// <param name="maxY">The maximum Y value in millimeters</param>
        /// <returns></returns>
        public static GimData ClipGimDataByXY(GimData gimData, 
            double minX, double minY, double maxX, double maxY)
        {
            if (minX == 0 && minY == 0 && maxX == 0 && maxY == 0) 
            {
                return gimData;
            }

            foreach(int childId in gimData.Elements[0].Elements)
            {
                ClipGimDataRecursive(gimData, childId, Matrix.Ge3DUnitMatrix(),
                    new Vect(0, 0, 0), minX, minY, maxX, maxY);
            }
            return gimData;
        }

        private static void ClipGimDataRecursive(GimData gimData, int elId, Matrix transformation, Vect offset,
            double minX, double minY, double maxX, double maxY)
        {
            GimElement currentElement = gimData.Elements[(int)elId];

            GimElementType eleType = currentElement.ElementType;

            // only valid types worth checking 
            if (eleType != GimElementType.D_root /*&& eleType != GimElementType.D_link*/
                && eleType != GimElementType.D_part && eleType != GimElementType.D_folder
                && eleType != GimElementType.R_Poly && eleType != GimElementType.P_polygon
                && eleType != GimElementType.P_polyline
                )
            {
                return;
            }

            foreach (GimLocal local in currentElement.Locals)
            {

                //if (local.Parent != parentId) continue; // following the correct hierarchy

                Matrix currentMatrix = new Matrix(local);
                Vect currentOffset = new Vect(local.Px, local.Py, local.Pz);

                Matrix nextMatrix = transformation * currentMatrix;
                Vect nextOffset = transformation * currentOffset + offset;
                if (currentElement.ElementType == GimElementType.R_Poly)
                {
                    foreach(int childId in currentElement.Elements)
                    {
                        GimElement polyElement = gimData.Elements[childId];
                        if (polyElement.ElementType == GimElementType.P_polygon)
                        {
                            ClipGimElementByXY(polyElement, nextMatrix, nextOffset, minX, minY, maxX, maxY);
                        }
                    }
                }

                else
                {
                    foreach (int childId in currentElement.Elements)
                    {
                        ClipGimDataRecursive(gimData, childId, nextMatrix, nextOffset, minX, minY, maxX, maxY);
                    }
                }
            }
        }

        public static GimElement ClipGimElementByXY(GimElement gimElement, Matrix transformation, Vect offset,
        double minX, double minY, double maxX, double maxY)
        {
            TriangleMesh mesh = ConstructTriangles(gimElement);

            foreach (var triangle in mesh.Triangles)
            {
                Vect v1 = triangle.Vertex1, v2 = triangle.Vertex2, v3 = triangle.Vertex3;

                triangle.Vertex1 = transformation * v1 + offset;
                triangle.Vertex2 = transformation * v2 + offset;
                triangle.Vertex3 = transformation * v3 + offset;

            }

            List<Triangle> oldTriangles = mesh.Triangles;
            List<Triangle> newTriangles = new(oldTriangles.Count);
            foreach (Triangle triangle in oldTriangles)
            {
                var result = ClipTriangleByDimension(triangle, minX, Triangle.DIMENSION_INDEX_X, false);
                newTriangles.AddRange(result);
            }

            oldTriangles = newTriangles;
            newTriangles = new(oldTriangles.Count);
            foreach (Triangle triangle in oldTriangles)
            {
                var result = ClipTriangleByDimension(triangle, minY, Triangle.DIMENSION_INDEX_Y, false);
                newTriangles.AddRange(result);
            }

            oldTriangles = newTriangles;
            newTriangles = new(oldTriangles.Count);
            foreach (Triangle triangle in oldTriangles)
            {
                var result = ClipTriangleByDimension(triangle, maxX, Triangle.DIMENSION_INDEX_X, true);
                newTriangles.AddRange(result);
            }

            oldTriangles = newTriangles;
            newTriangles = new(oldTriangles.Count);
            foreach (Triangle triangle in oldTriangles)
            {
                var result = ClipTriangleByDimension(triangle, maxY, Triangle.DIMENSION_INDEX_Y, true);
                newTriangles.AddRange(result);
            }

            TriangleMesh newMesh = new TriangleMesh();
            newMesh.Triangles = newTriangles;

            Matrix inverseTransformation = transformation.Inverse;
            Vect inverseOffset = -offset;

            foreach(var triangle in newMesh.Triangles)
            {
                Vect v1 = triangle.Vertex1, v2 = triangle.Vertex2, v3 = triangle.Vertex3;

                triangle.Vertex1 = inverseTransformation * (v1 - offset);
                triangle.Vertex2 = inverseTransformation * (v2 - offset);
                triangle.Vertex3 = inverseTransformation * (v3 - offset);
            }

            newMesh.ApplyToGimDataPolygon(gimElement);

            return gimElement;
        }

        /// <summary>
        /// Clips a triangle by a single dimension and returns the result as a list of triangles.
        /// </summary>
        /// <param name="triangle">The triangle to be clipped</param>
        /// <param name="dimensionValue">The value along the dimension</param>
        /// <param name="dimensionIndex">The index of the dimension: 0=X, 1=Y, 2=Z</param>
        /// <param name="superiorToDimension">Wether to compare if it's superior or inferior to dimension (< or >) </param>
        /// <returns></returns>
        public static List<Triangle> ClipTriangleByDimension(Triangle triangle, 
            double dimensionValue, int dimensionIndex, bool superiorToDimension)
        {
            List<Triangle> result = new();

            int multiplier = -1;
            if (superiorToDimension) multiplier = 1;

            int pointCountOutOfBounds = 0;
            List<Vect> pointsOutOfBounds = new();
            List<Vect> pointsInBounds = new();

            List<Vect> vertices = new() { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };
            foreach(Vect vertex in vertices)
            {
                double diff = vertex[dimensionIndex] - dimensionValue;
                diff *= multiplier;
                if (diff > Matrix.EQUALITY_MARGIN)
                {
                    pointCountOutOfBounds++;
                    pointsOutOfBounds.Add(vertex);
                }
                else
                {
                    pointsInBounds.Add(vertex);
                }
            }

            switch (pointCountOutOfBounds)
            {
                // no points out of bounds, we return the same triangle
                case 0:
                    {
                        result.Add(triangle);
                    }
                    break;
                // one point out of bounds, we intersect the triangle with the plane and return two triangles

                case 1:
                    {
                        List<Vect> intersectionPoints = new();
                        foreach(Vect outPoint in pointsOutOfBounds)
                        {
                            foreach(Vect inPoint in pointsInBounds)
                            {
                                double divisor = (inPoint[dimensionIndex] - outPoint[dimensionIndex]);

                                // calculating the linear function that maps dimensionIndex (could be X/Y/Z) to X
                                // according to the formula X = Ax * dimension + Bx
                                double aX = (inPoint[0] - outPoint[0]) / divisor;
                                double bX = inPoint[0] - inPoint[dimensionIndex] * aX;

                                // same for Y and Z
                                double aY = (inPoint[1] - outPoint[1]) / divisor;
                                double bY = inPoint[1] - inPoint[dimensionIndex] * aY;

                                double aZ = (inPoint[2] - outPoint[2]) / divisor;
                                double bZ = inPoint[2] - inPoint[dimensionIndex] * aZ;

                                double x = aX * dimensionValue + bX;
                                double y = aY * dimensionValue + bY;
                                double z = aZ * dimensionValue + bZ;

                                intersectionPoints.Add(new Vect(x, y, z));

                            }
                        }

                        // we connect each of the intersection Points to the other two points and add to the result
                        foreach(Vect intersectionPoint in intersectionPoints)
                        {
                            Triangle newTriangle = new(intersectionPoint, pointsInBounds[0], pointsInBounds[1]);
                            result.Add(newTriangle);
                        }

                    }
                    break;
                    // if two points are outside bounds, they will be replaced with two points on the edge
                case 2:
                    {
                        List<Vect> intersectionPoints = new();
                        foreach (Vect outPoint in pointsOutOfBounds)
                        {
                            foreach (Vect inPoint in pointsInBounds)
                            {
                                double divisor = (inPoint[dimensionIndex] - outPoint[dimensionIndex]);

                                // calculating the linear function that maps dimensionIndex (could be X/Y/Z) to X
                                // according to the formula X = Ax * dimension + Bx
                                double aX = (inPoint[0] - outPoint[0]) / divisor;
                                double bX = inPoint[0] - inPoint[dimensionIndex] * aX;

                                // same for Y and Z
                                double aY = (inPoint[1] - outPoint[1]) / divisor;
                                double bY = inPoint[1] - inPoint[dimensionIndex] * aY;

                                double aZ = (inPoint[2] - outPoint[2]) / divisor;
                                double bZ = inPoint[2] - inPoint[dimensionIndex] * aZ;

                                double x = aX * dimensionValue + bX;
                                double y = aY * dimensionValue + bY;
                                double z = aZ * dimensionValue + bZ;

                                intersectionPoints.Add(new Vect(x, y, z));

                            }
                        }

                        result.Add(new(intersectionPoints[0], intersectionPoints[1], pointsInBounds[0]));
                    }
                    break;

                    // in the case of 3 points out of bounds, we just return nothing (triangle isn't in bounds)
            }

            return result;
        }

        // rounds all the polygons in the Triangle Mesh
        public static void RoundPolygons(TriangleMesh mesh, double precision = 50)
        {
            foreach (Triangle triangle in mesh.Triangles)
            {
                triangle.Round(precision);
            }
        }

        /// <summary>
        /// Removes triangles from a TriangleMesh object based on a specified condition. 
        /// It iterates through all triangles in the mesh, checks each against the condition,
        /// and retains only those that do not meet the condition.
        /// </summary>
        /// <param name="mesh">The TriangleMesh object from which triangles are to be removed. 
        /// This mesh is directly modified by the method.</param>
        /// <remarks>
        /// This method counts the number of triangles removed but does not return this count.
        /// Instead, it updates the mesh's Triangles property with the remaining triangles.
        /// The method assumes that the Triangle class has an IsVertex method to determine
        /// if a triangle meets the condition for removal. Triangles for which IsVertex returns
        /// true are removed from the mesh.
        /// </remarks>
        public static void RemoveVertexTriangles(TriangleMesh mesh)
        {
            int vertexTriangleCount = 0;
            List<Triangle> newTriangles = new();
            for (int i = 0; i < mesh.Triangles.Count; i++)
            {
                Triangle triangle = mesh.Triangles[i];
                if (triangle.IsVertex())
                {
                    //mesh.RemoveTriangle(triangle);
                    vertexTriangleCount++;
                    //i--;
                }
                else
                {
                    newTriangles.Add(triangle);
                }
            }
            mesh.Triangles = newTriangles;
        }

        public static void FixDegenerate(TriangleMesh mesh)
        {
            int degenerateCount = 0;
            for (int i = 0; i < mesh.Triangles.Count; i++)
            {
                Triangle triangle = mesh.Triangles[i];
                if (triangle.IsDegenerate())
                {
                    triangle.FixDegenerateNeighbourhood();
                    //i--;
                    degenerateCount++;
                }
            }
        }

        public static void FixFlattened(TriangleMesh mesh)
        {
            int flattenedCount = 0;
            for (int i = 0; i < mesh.Triangles.Count; i++)
            {
                Triangle triangle = mesh.Triangles[i];
                if (triangle.IsFlattened())
                {
                    triangle.FixFlattenedNeighbourhood();
                    //i--;
                    flattenedCount++;
                }
            }
        }

        /// <summary>
        /// Removes triangles that are either flattened or degenerate from a given TriangleMesh.
        /// A triangle is considered flattened if it lies in a single plane, and degenerate if
        /// any of its vertices are coincident.
        /// </summary>
        /// <param name="mesh">The TriangleMesh object from which triangles are to be removed.
        /// This mesh is directly modified by the method.</param>
        /// <remarks>
        /// This method iterates through all the triangles in the given mesh. It checks each
        /// triangle to determine if it is flattened or degenerate, and if so, the triangle
        /// is not included in the new list of triangles. The mesh's Triangles property is then
        /// updated with this new list.
        /// </remarks>
        public static void RemoveFlattenedAndDegenerate(TriangleMesh mesh)
        {
            List<Triangle> newList = new();

            for (int i = 0; i < mesh.Triangles.Count; i++)
            {
                Triangle triangle = mesh.Triangles[i];
                if ((triangle.IsFlattened() || triangle.IsDegenerate()))
                {

                }
                else
                {
                    newList.Add(triangle);
                }
            }
            mesh.Triangles = newList;
        }



        public static void ConstructNeighbourhoods(TriangleMesh mesh)
        {
            VertexDictionary vertexDictionary = new();
            foreach (Triangle triangle in mesh.Triangles)
            {
                Vect vertex1 = triangle.Vertex1;
                Vect vertex2 = triangle.Vertex2;
                Vect vertex3 = triangle.Vertex3;

                triangle.Neighbour1 = new();
                triangle.Neighbour2 = new();
                triangle.Neighbour3 = new();

                var neighbours = vertexDictionary[vertex1];

                foreach (var neighbour in neighbours)
                {
                    bool success = triangle.AddNeighbourIfPossible(neighbour);
                    if (success)
                    {
                        neighbour.AddNeighbourIfPossible(triangle);
                    }
                }

                neighbours = vertexDictionary[vertex2];

                foreach (var neighbour in neighbours)
                {
                    bool success = triangle.AddNeighbourIfPossible(neighbour);
                    if (success)
                    {
                        neighbour.AddNeighbourIfPossible(triangle);
                    }
                }


                neighbours = vertexDictionary[vertex3];

                foreach (var neighbour in neighbours)
                {
                    bool success = triangle.AddNeighbourIfPossible(neighbour);
                    if (success)
                    {
                        neighbour.AddNeighbourIfPossible(triangle);
                    }
                }

                vertexDictionary[vertex1].Add(triangle);
                vertexDictionary[vertex2].Add(triangle);
                vertexDictionary[vertex3].Add(triangle);
            }

            return;
        }

        public static List<List<Triangle>> GroupPlanarTriangles(TriangleMesh mesh)
        {
            Dictionary<int, int> encounteredGroups = new();
            List<List<Triangle>> flatTriangleGroups = new List<List<Triangle>>();
            for (int i = 0; i < mesh.Triangles.Count; i++)
            {
                Triangle triangle = mesh.Triangles[i];
                List<Triangle> triangleGroup = new();
                triangle.GroupPlanarNeighbours(triangleGroup, triangle.GetNormal());

                if (triangleGroup.Count > 1) // marking the triangles as flat, so we remove them from the big list
                {
                    foreach (Triangle flatTriangle in triangleGroup)
                    {
                        flatTriangle.planar = true;
                    }
                    flatTriangleGroups.Add(triangleGroup);
                    //DetectAndBreakOverlaps(triangleGroup);
                }

                if (triangleGroup.Count > 1)
                {
                    if (encounteredGroups.ContainsKey(triangleGroup.Count))
                    {
                        encounteredGroups[triangleGroup.Count] += 1;
                    }
                    else
                    {
                        encounteredGroups[triangleGroup.Count] = 1;
                    }

                    //TriangleMesh simpleGroupMesh = TriangleMesh.ConstructMesh(triangleGroup, true) ;
                    //if(triangleGroup.Count>100)
                    //    SimplifyTriangleGroup(simpleGroupMesh);
                }
            }

            List<Triangle> nonFlatTriangles = new();

            foreach (Triangle triangle in mesh.Triangles)
            {
                if (!triangle.planar)
                {
                    nonFlatTriangles.Add(triangle);
                }
            }
            // the non flat triangles are added in the end so we don't lose them
            flatTriangleGroups.Add(nonFlatTriangles);

            return flatTriangleGroups;
        }

        private static void DetectAndBreakOverlaps(List<Triangle> triangles)
        {
            // firstly we detect the dimension with the most variance (x, y or z), to optimize the search
            double[] minCoordinate = new double[3] { int.MaxValue, int.MaxValue, int.MaxValue };
            double[] maxCoordinate = new double[3] { int.MinValue, int.MinValue, int.MinValue };

            foreach (Triangle triangle in triangles)
            {
                Vect[] verticies = new Vect[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };

                foreach (Vect vertex in verticies)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (vertex[i] < minCoordinate[i])
                        {
                            minCoordinate[i] = vertex[i];
                        }

                        if (vertex[i] > maxCoordinate[i])
                        {
                            maxCoordinate[i] = vertex[i];
                        }
                    }
                }
            }

            double[] differences = new double[] {
                Math.Abs(maxCoordinate[0] - minCoordinate[0]),
                Math.Abs(maxCoordinate[1] - minCoordinate[1]),
                Math.Abs(maxCoordinate[2] - minCoordinate[2])
            };

            int dimensionIndex = 0;
            double maxDifference = 0;

            for (int i = 0; i < differences.Length; i++)
            {
                if (differences[i] > maxDifference)
                {
                    maxDifference = differences[i];
                    dimensionIndex = i;
                }
            }

            // setting the triangles to be comparable by this dimension
            foreach (Triangle triangle in triangles)
            {
                triangle.dimensionIndex = dimensionIndex;
            }

            triangles.Sort(Triangle.CompareTriangles);

            StringBuilder Intersections = new();

            for (int i = 0; i < triangles.Count - 1; i++)
            {
                var currentTriangle = triangles[i];

                bool foundAndFixedOverlap = false;
                for (int j = i + 1; j < triangles.Count; j++)
                {
                    var otherTriangle = triangles[j];

                    // the other triangle and the triangles after are too far away
                    if (otherTriangle.MinVertexValue > currentTriangle.MaxVertexValue)
                    {
                        break;
                    }

                    Vect centerDiff = currentTriangle.Center - otherTriangle.Center;
                    double difference = centerDiff.Norm;
                    double radiusSum = currentTriangle.Radius + otherTriangle.Radius;

                    if(triangles.Count > 300)
                    {
                        string intersectText = Intersections.ToString();
                    }

                    // triangles are more likely to overlap
                    if (difference < radiusSum)
                    {
                        
                        List<Triangle> resultingTriangles = Triangle.ExplodeIntersectingTriangles(currentTriangle, otherTriangle);

                        bool overlapFound = resultingTriangles.Count != 2;
                        if (!overlapFound)
                        {
                            overlapFound = resultingTriangles[0] != currentTriangle || resultingTriangles[1] != otherTriangle;
                        }

                        // check for no overlap found
                        if (overlapFound)
                        {
                            Intersections.AppendLine("Checking the intersection of triangles: \n");
                            Intersections.AppendLine(currentTriangle.ToString());
                            Intersections.AppendLine(otherTriangle.ToString());
                            Intersections.AppendLine("Intersection found! resulting triangles: ");

                            // we remove the old triangles
                            triangles.RemoveAt(j);
                            triangles.RemoveAt(i);
                            foundAndFixedOverlap = true;
                            // we're gonna restart i from this value
                            int minInsertPosition = i;

                            for (int k = 0; k < resultingTriangles.Count; k++)
                            {
                                Triangle resultingTriangle = resultingTriangles[k];

                                Intersections.AppendLine(k + ": " + resultingTriangle.ToString());
                                int insertPosition = 0;
                                //int insertPosition = FindInsertPosition(resultingTriangle, triangles, 0, triangles.Count - 1);
                                if (insertPosition < minInsertPosition)
                                {
                                    minInsertPosition = insertPosition;
                                }

                                // we add the triangle in the appropriate spot. if the appropriate spot is bigger than the max,
                                // we put the triangle in the end
                                if (insertPosition < triangles.Count) triangles.Insert(insertPosition, resultingTriangle);
                                else triangles.Add(resultingTriangle);
                            }
                            // we go back and check from the earliest triangle we added
                            i = minInsertPosition - 1;
                            j = i;
                            Intersections.AppendLine("");
                            break;
                        }
                        else {
                            Intersections.AppendLine("No intersection found.\n");
                        }
                    }

                }

            }

        }

        private static int FindInsertPosition(double[] triangle, List<double[]> list, int lowerBound, int upperBound, int comparisonCoordinate)
        {
            if (lowerBound == upperBound - 1)
            {
                return upperBound;
            }
            if (lowerBound == upperBound)
            {
                return lowerBound;
            }

            if (triangle[comparisonCoordinate] < list[lowerBound][comparisonCoordinate])
            {
                return lowerBound;
            }
            if (triangle[comparisonCoordinate] > list[upperBound][comparisonCoordinate])
            {
                return upperBound + 1;
            }

            int middleBound = (upperBound + lowerBound) / 2;

            if (triangle[0] == list[middleBound][comparisonCoordinate])
            {
                return middleBound;
            }

            if (triangle[0] < list[middleBound][comparisonCoordinate])
            {
                return FindInsertPosition(triangle, list, lowerBound, middleBound, comparisonCoordinate);
            }
            else
            {
                return FindInsertPosition(triangle, list, middleBound, upperBound, comparisonCoordinate);
            }

        }

        public static void ReplaceFlatGroupsWithGimPoly(List<List<Triangle>> triangleGroups)
        {
            // important to note that the last group of triangles is NOT flat
            for (int triGrp = 0; triGrp < triangleGroups.Count - 1; triGrp++)
            {
                var triangleGroup = triangleGroups[triGrp];
                SimplifyTriangleGroup(triangleGroup);
            }
        }
        public static List<List<Vertex2d>> GetTriangleGroupBoundaries2d(List<Triangle2d> triangles)
        {
            List<List<Vertex2d>> result = new();

            HashSet<Vertex2d> outerVertices = new HashSet<Vertex2d>();
            HashSet<Vertex2d> uniqueVertices = new HashSet<Vertex2d>(triangles.Count * 3);

            foreach (var triangle in triangles)
            {
                uniqueVertices.Add(triangle.Vertex1);
                uniqueVertices.Add(triangle.Vertex2);
                uniqueVertices.Add(triangle.Vertex3);
                if (triangle.Neighbour1.Count == 0)
                {
                    outerVertices.Add(triangle.Vertex2);
                    outerVertices.Add(triangle.Vertex3);

                    triangle.Vertex2.AddConnectedPoint(triangle.Vertex3);
                    triangle.Vertex3.AddConnectedPoint(triangle.Vertex2);
                }
                if (triangle.Neighbour2.Count == 0)
                {
                    outerVertices.Add(triangle.Vertex1);
                    outerVertices.Add(triangle.Vertex3);

                    triangle.Vertex1.AddConnectedPoint(triangle.Vertex3);
                    triangle.Vertex3.AddConnectedPoint(triangle.Vertex1);
                }
                if (triangle.Neighbour3.Count == 0)
                {
                    outerVertices.Add(triangle.Vertex1);
                    outerVertices.Add(triangle.Vertex2);

                    triangle.Vertex2.AddConnectedPoint(triangle.Vertex1);
                    triangle.Vertex1.AddConnectedPoint(triangle.Vertex2);
                }
            }

            foreach (var outerVertex in outerVertices)
            {
                outerVertex.IsOutside = true;
            }

            HashSet<Vertex2d> checkedVerticies = new();
            List<List<Vertex2d>> vertexChains = new();
            foreach (var outerVertex in outerVertices)
            {
                // already checked this point here and got its chain
                if (checkedVerticies.Contains(outerVertex)) continue;
                checkedVerticies.Add(outerVertex);

                Vertex2d previousVertex = null;
                foreach (Vertex2d connectedVertex in outerVertex.ConnectedVertices)
                {
                    if (connectedVertex.IsOutside)
                    {
                        previousVertex = connectedVertex;
                        break;
                    }
                }

                if (previousVertex == null) continue;

                HashSet<Vertex2d> visitedVerticies = new() { previousVertex };
                List<Vertex2d> vertexChain = new List<Vertex2d>() { previousVertex };

                GetConnnectedVerticesRecursively(outerVertex, vertexChain, visitedVerticies);
                vertexChains.Add(vertexChain);
                checkedVerticies.UnionWith(visitedVerticies);
            }

            double maxChainRadius = -1;
            List<Vertex2d> maxChain = null;

            if (vertexChains.Count > 1)
            {
                string debug = "debug breakpoint";
            }

            // we determine which boundary is the outer one, by the bigger chain

            Vertex2d centerPoint = new(0, 0);
            foreach (var vertex in uniqueVertices)
            {
                centerPoint += vertex;
            }
            centerPoint = new((Vect)centerPoint / uniqueVertices.Count);

            foreach (var vertexChain in vertexChains)
            {
                double maxRadius = 0;
                foreach (var vertex in vertexChain)
                {
                    double radius = (vertex - centerPoint).R;
                    if (radius > maxRadius)
                    {
                        maxRadius = radius;
                    }
                }

                if (maxRadius >= maxChainRadius)
                {
                    maxChainRadius = maxRadius;
                    maxChain = vertexChain;
                }

            }

            List<List<Vertex2d>> sortedChains = new List<List<Vertex2d>>() { maxChain };

            // biggest chain (exterior) first
            foreach (var vertexChain in vertexChains)
            {
                if (vertexChain == maxChain) continue;
                sortedChains.Add(vertexChain);
            }

            foreach (var vertexChain in sortedChains)
            {
                List<Vertex2d> pointChain = new();
                foreach (var vertex in vertexChain)
                {
                    // reconstructing original point
                    //Vect newPoint = origin + vertex.X * vect1 + vertex.Y * vect2;
                    pointChain.Add(vertex);
                }
                result.Add(pointChain);
            }


            return result;

        }
        public static List<List<Vect>> GetTriangleGroupBoundaries(List<Triangle> triangles)
        {
            
            List<List<Vect>> result = new();
            try
            {
                HashSet<Vertex2d> uniqueVerticies = new HashSet<Vertex2d>();

                Triangle firstTriangle = triangles[0];
                Vect origin = firstTriangle.Vertex1;

                Vect planeVectA = (firstTriangle.Vertex2 - firstTriangle.Vertex1).Direction;
                Vect planeVectB = (firstTriangle.Vertex3 - firstTriangle.Vertex2);

                Vect planeNormal = planeVectA ^ planeVectB;

                Vect vect1 = (firstTriangle.Vertex2 - firstTriangle.Vertex1).Direction;
                Vect vect2 = (firstTriangle.Vertex3 - firstTriangle.Vertex2);
                vect2 = vect2 - (vect2 * vect1) * vect1;
                vect2 = vect2.Direction;

                vect1 = new(1, 0, 0);
                vect2 = new(0, 1, 0);
                origin = new(0, 0, 0);


                List<Triangle2d> triangles2d = new();
                Dictionary<Triangle2d, Triangle> triangle2Dto3D = new();
                Dictionary<Triangle, Triangle2d> triangle3Dto2D = new();

                Dictionary<Vertex2d, Vect> vertex2DtoPoint3D = new();

                foreach (Triangle triangle in triangles)
                {
                    List<Vect> trianglePoints = new() { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };
                    List<Vertex2d> triangleVertices = new();

                    foreach (Vect point in trianglePoints)
                    {
                        double x = (point - origin) * vect1;
                        double y = (point - origin) * vect2;

                        Vertex2d vertex = new(x, y);
                        if (uniqueVerticies.Contains(vertex))
                        {
                            uniqueVerticies.TryGetValue(vertex, out vertex);
                        }
                        else
                        {
                            uniqueVerticies.Add(vertex);
                            vertex2DtoPoint3D.Add(vertex, point);
                        }
                        triangleVertices.Add(vertex);
                    }

                    Triangle2d triangle2d = new(triangleVertices[0], triangleVertices[1], triangleVertices[2]);

                    triangles2d.Add(triangle2d);


                    triangle2Dto3D[triangle2d] =  triangle;
                    triangle3Dto2D[triangle] =  triangle2d;
                }
                foreach (Triangle triangle in triangles)
                {
                    Triangle2d triangle2d = triangle3Dto2D[triangle];
                    foreach (var neigh1 in triangle.Neighbour1)
                    {
                        if (!triangle3Dto2D.ContainsKey(neigh1)) continue;
                        var neigh12d = triangle3Dto2D[neigh1];
                        triangle2d.AddNeighbourIfPossible(neigh12d);
                    }

                    foreach (var neigh2 in triangle.Neighbour2)
                    {
                        if (!triangle3Dto2D.ContainsKey(neigh2)) continue;
                        var neigh22d = triangle3Dto2D[neigh2];
                        triangle2d.AddNeighbourIfPossible(neigh22d);
                    }

                    foreach (var neigh3 in triangle.Neighbour3)
                    {
                        if (!triangle3Dto2D.ContainsKey(neigh3)) continue;
                        var neigh32d = triangle3Dto2D[neigh3];
                        triangle2d.AddNeighbourIfPossible(neigh32d);
                    }
                }

                HashSet<Vertex2d> outerVertices = new HashSet<Vertex2d>();
                foreach (var triangle2d in triangles2d)
                {
                    
                        if (triangle2d.Neighbour1.Count == 0)
                        {
                            outerVertices.Add(triangle2d.Vertex2);
                            outerVertices.Add(triangle2d.Vertex3);

                            triangle2d.Vertex2.AddConnectedPoint(triangle2d.Vertex3);
                            triangle2d.Vertex3.AddConnectedPoint(triangle2d.Vertex2);
                        }
                        if (triangle2d.Neighbour2.Count == 0)
                        {
                            outerVertices.Add(triangle2d.Vertex1);
                            outerVertices.Add(triangle2d.Vertex3);

                            triangle2d.Vertex1.AddConnectedPoint(triangle2d.Vertex3);
                            triangle2d.Vertex3.AddConnectedPoint(triangle2d.Vertex1);
                        }
                        if (triangle2d.Neighbour3.Count == 0)
                        {
                            outerVertices.Add(triangle2d.Vertex1);
                            outerVertices.Add(triangle2d.Vertex2);

//<<<<<<< HEAD
//            HashSet<Vertex2d> checkedVerticies = new();
//            List<List<Vertex2d>> vertexChains = new();
//            double pi = Math.PI;
//            foreach(var outerVertex in outerVertices)
//            {
//                // already checked this point here and got its chain
//                if (checkedVerticies.Contains(outerVertex)) continue;
//                checkedVerticies.Add(outerVertex);
//=======
                            triangle2d.Vertex2.AddConnectedPoint(triangle2d.Vertex1);
                            triangle2d.Vertex1.AddConnectedPoint(triangle2d.Vertex2);
                        }
                    
                }
//>>>>>>> 076802becbbadabcab2fb088afa24a55935676dc

                foreach (var outerVertex in outerVertices)
                {
                    outerVertex.IsOutside = true;
                }

                HashSet<Vertex2d> checkedVerticies = new();
                List<List<Vertex2d>> vertexChains = new();
                foreach (var outerVertex in outerVertices)
                {
                    // already checked this point here and got its chain
                    if (checkedVerticies.Contains(outerVertex)) continue;
                    checkedVerticies.Add(outerVertex);

                    Vertex2d previousVertex = null;
                    foreach (Vertex2d connectedVertex in outerVertex.ConnectedVertices)
                    {
                        if (connectedVertex.IsOutside)
                        {
                            previousVertex = connectedVertex;
                            break;
                        }
                    }

                    if (previousVertex == null) continue;

                    HashSet<Vertex2d> visitedVerticies = new() { previousVertex };
                    List<Vertex2d> vertexChain = new List<Vertex2d>() { previousVertex };

                    GetConnnectedVerticesRecursively(outerVertex, vertexChain, visitedVerticies);
                    vertexChains.Add(vertexChain);
                    checkedVerticies.UnionWith(visitedVerticies);
                }

                double maxChainRadius = -1;
                List<Vertex2d> maxChain = null;

                if (vertexChains.Count > 1)
                {
                    string debug = "debug breakpoint";
                }

                // we determine which boundary is the outer one, by the bigger chain

                Vertex2d centerPoint = new(0, 0);
                foreach (var vertex in uniqueVerticies)
                {
                    centerPoint += vertex;
                }
                centerPoint = new((Vect)centerPoint / uniqueVerticies.Count);

                foreach (var vertexChain in vertexChains)
                {
                    double maxRadius = 0;
                    foreach (var vertex in vertexChain)
                    {
                        double radius = (vertex - centerPoint).R;
                        if (radius > maxRadius)
                        {
                            maxRadius = radius;
                        }
                    }

                    if (maxRadius >= maxChainRadius)
                    {
                        maxChainRadius = maxRadius;
                        maxChain = vertexChain;
                    }

                }

                List<List<Vertex2d>> sortedChains = new List<List<Vertex2d>>() { maxChain };

                // biggest chain (exterior) first
                foreach (var vertexChain in vertexChains)
                {
                    if (vertexChain == maxChain) continue;
                    sortedChains.Add(vertexChain);
                }

                foreach (var vertexChain in sortedChains)
                {
                    List<Vect> pointChain = new();
                    foreach (var vertex in vertexChain)
                    {
                        // reconstructing original point
                        //Vect newPoint = origin + vertex.X * vect1 + vertex.Y * vect2;
                        pointChain.Add(vertex2DtoPoint3D[vertex]);
                    }
                    result.Add(pointChain);
                }
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }

            return result;
        } 

        public static List<GimDataPartial> ConvertTrianglesToGimDataPartials(List<Triangle> triangles, int parent = 0)
        {
            List<GimDataPartial> gimDataPartials = new();

            foreach(var triangle in triangles)
            {
                if(triangle.IsDegenerate() || triangle.IsFlattened()) continue;

                Vect pointA = triangle.Vertex1;
                Vect pointB = triangle.Vertex2;
                Vect pointC = triangle.Vertex3;

                Vect directionA = (pointB - pointA);
                Vect directionB = (pointC - pointA);
                Vect planeNormal = (directionA ^ directionB).Direction;
                planeNormal = planeNormal.Round();

                Vect basePoint = pointA;

                Vect vect1 = new Vect(1, 0, 0);
                vect1 = vect1 - (vect1 * planeNormal) * planeNormal;

                if (vect1.Norm == 0)
                {
                    vect1 = new Vect(0, 1, 0);
                    vect1 = vect1 - (vect1 * planeNormal) * planeNormal;
                }

                vect1 = vect1.Direction;
                Vect vect2 = (planeNormal ^ vect1).Direction;

                GimDataPartial gimData = new(parent);

                GimElement planeElement = new GimElement() { ElementType = GimElementType.F_plane };
                int planeIndex = 0;
                gimData.Elements.Add(planeElement);

                GimLocal planeLocal = new()
                {
                    parent = (uint)parent,
                    px = basePoint[0],
                    py = basePoint[1],
                    pz = basePoint[2]
                };
                Matrix planeMatrix = new(vect1, vect2, vect1 ^ vect2);
                planeMatrix.ApplyToLocal(planeLocal);
                planeElement.Locals.Add(planeLocal);

                GimElement wireElement = new GimElement() { ElementType = GimElementType.wire };
                wireElement.Locals.Add(new() { parent = (uint)planeIndex });
                int wireIndex = gimData.Elements.Count;
                gimData.Elements.Add(wireElement);
                planeElement.Elements.Add((uint)wireIndex);

                GimElement lineElement1 = GimElement.GetLineFromTwoPoints(pointA, pointB, wireIndex);
                GimElement lineElement2 = GimElement.GetLineFromTwoPoints(pointB, pointC, wireIndex);
                GimElement lineElement3 = GimElement.GetLineFromTwoPoints(pointC, pointA, wireIndex);

                int lineElement1Index = gimData.Elements.Count;
                gimData.Elements.Add(lineElement1);
                int lineElement2Index = gimData.Elements.Count;
                gimData.Elements.Add(lineElement2);
                int lineElement3Index = gimData.Elements.Count;
                gimData.Elements.Add(lineElement3);

                wireElement.Elements.Add((uint)lineElement1Index);
                wireElement.Elements.Add((uint)lineElement2Index);
                wireElement.Elements.Add((uint)lineElement3Index);

                gimDataPartials.Add(gimData);
            }

            return gimDataPartials;
        }

        private static List<Vertex2d> GetConnnectedVerticesRecursively(Vertex2d vertex, List<Vertex2d> vertexChain,HashSet<Vertex2d> visitedVertices)
        {
            vertexChain.Add(vertex);
            visitedVertices.Add(vertex);
            foreach(Vertex2d connectedVertex in vertex.ConnectedVertices)
            {
                if (!connectedVertex.IsOutside) continue;
                if (visitedVertices.Contains(connectedVertex)) continue;

                GetConnnectedVerticesRecursively(connectedVertex, vertexChain, visitedVertices);
                break;
            }

            return vertexChain;
        }

        private static SimplifiedPolygon SimplifyTriangleGroup(List<Triangle> triangles)
        {


            HashSet<Vect> outsidePoints = new HashSet<Vect>();





            Vect originPoint = triangles[0].Vertex1;
            Vect triangleNormal = triangles[0].GetNormal().Direction.Round();
            Vect u = new Vect(0, 0, 1) ^ triangleNormal;
            if (u.Norm == 0)
            {
                u = new Vect(0, 1, 0) ^ triangleNormal;
            }
            u = u.Direction.Round();
            Vect v = triangleNormal ^ u;
            v = v.Direction.Round();
            // this region is for debugging the triangles manually, exporting as a script for desmos
#if DEBUG
            debugPrintTrianglesDesmos(triangles, "BEFORE SPLITTING");
#endif

            List<double[]> triangles2D = new();


            // converting each triangle into a 2D triangle
            for (int i = 0; i < triangles.Count; i++)
            {
                Triangle triangle = triangles[i];
                Vect vertex1 = triangle.Vertex1;
                double v1x = (vertex1 - originPoint) * u;
                double v1y = (vertex1 - originPoint) * v;
                Vect vertex2 = triangle.Vertex2;
                double v2x = (vertex2 - originPoint) * u;
                double v2y = (vertex2 - originPoint) * v;
                Vect vertex3 = triangle.Vertex3;
                double v3x = (vertex3 - originPoint) * u;
                double v3y = (vertex3 - originPoint) * v;

                triangles2D.Add(new double[] { v1x, v1y, v2x, v2y, v3x, v3y });
            }
            // we're going to be calculating the maximum dimension variance between two given triangles
            // so we can know which dimension we're sorting according to
            double[] minCoordinates = new double[] { double.MaxValue, double.MaxValue };
            double[] maxCoordinates = new double[] { double.MinValue, double.MinValue };

            for (int i = 0; i < triangles2D.Count; i++)
            {
                double[] triangle = triangles2D[i];
                double minX = Math.Min(triangle[0], triangle[2]);
                minX = Math.Min(minX, triangle[4]);

                double maxX = Math.Max(triangle[0], triangle[2]);
                maxX = Math.Max(maxX, triangle[4]);

                double minY = Math.Min(triangle[1], triangle[3]);
                minY = Math.Min(minY, triangle[5]);

                double maxY = Math.Max(triangle[1], triangle[3]);
                maxY = Math.Max(maxY, triangle[5]);

                if (minX < minCoordinates[0])
                {
                    minCoordinates[0] = minX;
                }
                if (minY < minCoordinates[1])
                {
                    minCoordinates[1] = minY;
                }

                if (maxX > maxCoordinates[0])
                {
                    maxCoordinates[0] = maxX;
                }
                if (maxY > maxCoordinates[1])
                {
                    maxCoordinates[1] = maxY;
                }
            }

            double xDifference = maxCoordinates[0] - minCoordinates[0];
            double yDifference = maxCoordinates[1] - minCoordinates[1];

            int dimensionIndex = 0;
            if (yDifference > xDifference)
            {
                dimensionIndex = 1;
            }

            // sorting each triangle so that it's in the form of [minVertex, midVertex, maxVertex] according to whichever sorting dimension we chose
            for (int i = 0; i < triangles2D.Count; i++)
            {
                double[] triangle = triangles2D[i];

                double[] vertex1 = new double[] { triangle[0], triangle[1] };
                double[] vertex2 = new double[] { triangle[2], triangle[3] };
                double[] vertex3 = new double[] { triangle[4], triangle[5] };

                double[] newTriangle = new double[6];

                if (vertex1[dimensionIndex] <= vertex2[dimensionIndex] && vertex1[dimensionIndex] <= vertex3[dimensionIndex])
                {
                    newTriangle[0] = vertex1[0];
                    newTriangle[1] = vertex1[1];

                    if (vertex2[dimensionIndex] <= vertex3[dimensionIndex])
                    {
                        newTriangle[2] = vertex2[0];
                        newTriangle[3] = vertex2[1];

                        newTriangle[4] = vertex3[0];
                        newTriangle[5] = vertex3[1];
                    }
                    else
                    {
                        newTriangle[2] = vertex3[0];
                        newTriangle[3] = vertex3[1];

                        newTriangle[4] = vertex2[0];
                        newTriangle[5] = vertex2[1];
                    }
                }

                else if (vertex2[dimensionIndex] <= vertex1[dimensionIndex] && vertex2[dimensionIndex] <= vertex3[dimensionIndex])
                {
                    newTriangle[0] = vertex2[0];
                    newTriangle[1] = vertex2[1];

                    if (vertex1[dimensionIndex] <= vertex3[dimensionIndex])
                    {
                        newTriangle[2] = vertex1[0];
                        newTriangle[3] = vertex1[1];

                        newTriangle[4] = vertex3[0];
                        newTriangle[5] = vertex3[1];
                    }
                    else
                    {
                        newTriangle[2] = vertex3[0];
                        newTriangle[3] = vertex3[1];

                        newTriangle[4] = vertex1[0];
                        newTriangle[5] = vertex1[1];
                    }
                }
                else
                {
                    newTriangle[0] = vertex3[0];
                    newTriangle[1] = vertex3[1];
                    if (vertex1[dimensionIndex] <= vertex2[dimensionIndex])
                    {
                        newTriangle[2] = vertex1[0];
                        newTriangle[3] = vertex1[1];

                        newTriangle[4] = vertex2[0];
                        newTriangle[5] = vertex2[1];
                    }
                    else
                    {
                        newTriangle[2] = vertex2[0];
                        newTriangle[3] = vertex2[1];

                        newTriangle[4] = vertex1[0];
                        newTriangle[5] = vertex1[1];
                    }
                }

            }

            // after sorting each triangle internally, we sort the whole list of triangles according to this function

            int Compare2DTriangle(double[] triangleA, double[] triangleB)
            {
                if(triangleA[dimensionIndex] > triangleB[dimensionIndex])
                {
                    return 1;
                }
                if(triangleA[dimensionIndex] < triangleB[dimensionIndex])
                {
                    return -1;
                }
                return 0;
            }

            triangles2D.Sort(Compare2DTriangle);
            Dictionary<Vertex2d, Vertex2d> polygonOuterBound = new();
            {
                Vertex2d vertex1 = new Vertex2d(triangles2D[0][0], triangles2D[0][1]);
                Vertex2d vertex2 = new Vertex2d(triangles2D[0][2], triangles2D[0][3]);
                Vertex2d vertex3 = new Vertex2d(triangles2D[0][4], triangles2D[0][5]);
                polygonOuterBound.Add(vertex1, vertex1);
                polygonOuterBound.Add(vertex2, vertex2);
                polygonOuterBound.Add(vertex3, vertex3);
            }

            triangles2D.RemoveAt(0);

            SimplifiedPolygon polygon = new(polygonOuterBound);
            polygon.DimensionIndex = dimensionIndex;

            for(int i = 0; i < triangles2D.Count; i++)
            {

            }

#if DEBUG
            //debugPrintTrianglesDesmos(triangles, "AFTER SPLITTING");
        
#endif

            // we're calculating the outside points
            foreach (Triangle triangle in triangles)
            {
                if (triangle.MissingNeighboursOnOneSide()) // prolly an outside triangle
                {
                    if (triangle.Neighbour1.Count == 0)
                    {
                        outsidePoints.Add(triangle.Vertex2);
                        outsidePoints.Add(triangle.Vertex3);
                    }
                    if (triangle.Neighbour2.Count == 0)
                    {
                        outsidePoints.Add(triangle.Vertex3);
                        outsidePoints.Add(triangle.Vertex1);
                    }
                    if (triangle.Neighbour3.Count == 0)
                    {
                        outsidePoints.Add(triangle.Vertex1);
                        outsidePoints.Add(triangle.Vertex2);
                    }
                }
            }

            // we calculate the average or center of the outside points
            Vect averagePoint = new Vect(0, 0, 0);

            foreach (Vect outsidePoint in outsidePoints)
            {
                averagePoint += outsidePoint;
            }

            averagePoint = averagePoint / outsidePoints.Count;

            Vertex2d furthestPoint = null;
            double maxDistance = 0;

            // building 2D vertexes

            // TODO REMOVE DEBUG STUFF

            string desmosStr = "";

            Vect normal = triangles[0].GetNormal();
            Vect v1 = new Vect(1, 0, 0) ^ normal;
            if (v1.Norm == 0)
            {
                v1 = new Vect(0, 1, 0) ^ normal;
            }

            v1 = v1.Direction;
            Vect v2 = (normal ^ v1).Direction;

            Dictionary<Vect, Vertex2d> map3DTo2D = new();



            foreach (Triangle triangle in triangles)
            {

                Vect vertex1 = triangle.Vertex1;
                Vect vertex2 = triangle.Vertex2;
                Vect vertex3 = triangle.Vertex3;

                Vertex2d vertex2d1 = null;
                Vertex2d vertex2d2 = null;
                Vertex2d vertex2d3 = null;

                if (map3DTo2D.ContainsKey(vertex1))
                {
                    vertex2d1 = map3DTo2D[vertex1];
                }
                else
                {
                    Vect point = vertex1;
                    Vect difference = point - averagePoint;
                    double x = difference * v1;
                    double y = difference * v2;

                    vertex2d1 = new Vertex2d(x, y, outsidePoints.Contains(point));
                    map3DTo2D.Add(vertex1, vertex2d1);
                }

                if (map3DTo2D.ContainsKey(vertex2))
                {
                    vertex2d2 = map3DTo2D[vertex2];
                }
                else
                {
                    Vect point = vertex2;
                    Vect difference = point - averagePoint;
                    double x = difference * v1;
                    double y = difference * v2;

                    vertex2d2 = new Vertex2d(x, y, outsidePoints.Contains(point));
                    map3DTo2D.Add(vertex2, vertex2d2);
                }


                if (map3DTo2D.ContainsKey(vertex3))
                {
                    vertex2d3 = map3DTo2D[vertex3];
                }
                else
                {
                    Vect point = vertex3;
                    Vect difference = point - averagePoint;
                    double x = difference * v1;
                    double y = difference * v2;

                    vertex2d3 = new Vertex2d(x, y, outsidePoints.Contains(point));
                    map3DTo2D.Add(vertex3, vertex2d3);
                }



                if (triangle.Neighbour3.Count == 0)
                {
                    vertex2d1.AddConnectedPoint(vertex2d2);
                    vertex2d2.AddConnectedPoint(vertex2d1);
                }
                if (triangle.Neighbour1.Count == 0)
                {
                    vertex2d2.AddConnectedPoint(vertex2d3);
                    vertex2d3.AddConnectedPoint(vertex2d2);
                }
                if (triangle.Neighbour2.Count == 0)
                {
                    vertex2d3.AddConnectedPoint(vertex2d1);
                    vertex2d1.AddConnectedPoint(vertex2d3);
                }

            }



            desmosStr += "(0, 0)";

            HashSet<Vertex2d> vertices = new();

            foreach (var keyVal in map3DTo2D)
            {
                vertices.Add(keyVal.Value);
            }

            List<Tuple<Vertex2d, Vertex2d>> segments = new();
            HashSet<Vertex2d> checkedVertices = new();

            foreach (Vertex2d vertex2d in vertices)
            {
                if (vertex2d.IsOutside)
                {
                    checkedVertices.Add(vertex2d);
                    foreach (Vertex2d other in vertex2d.ConnectedVertices)
                    {
                        if (checkedVertices.Contains(other)) continue;
                        if (other.IsOutside)
                            segments.Add(new Tuple<Vertex2d, Vertex2d>(vertex2d, other));
                    }
                }
            }

            string desmosSegmentString = "";

            int graphId = 0;
            foreach (var segment in segments)
            {
                graphId++;
                Vertex2d vertex1 = segment.Item1;
                Vertex2d vertex2 = segment.Item2;

                string polygonString =
                   string.Format(" id: 'graph{0}', latex: '({1:0.#}, {2:0.#}), ({3:0.#}, {4:0.#})', lines: true", graphId,
                   vertex1.X, vertex1.Y, vertex2.X, vertex2.Y);
                string codeLine = "calculator.setExpression({" + polygonString + "});";
                desmosSegmentString += codeLine + "\n";
            }

            // we're finding the furthest point from the center,
            // this point must be an outside point, otherwise no other poitns can be further
            foreach (Vertex2d outsidePoint in vertices)
            {
                double distance = ((Vect)(outsidePoint)).Norm;
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    furthestPoint = outsidePoint;
                }
            }


            // now we find the triangle containing the furthest point AND having an edge on the outside

            Triangle furthestTriangle = null;

            //foreach (Triangle triangle in triangles)
            //{
            //    if (triangle.ContainsVertex(furthestPoint))
            //    {
            //        // we check which vertext is the furthest point

            //        int pointIndex = -1;
            //        if (triangle.Vertex1 == furthestPoint)
            //        {
            //            pointIndex = 1;
            //        }
            //        else if (triangle.Vertex2 == furthestPoint)
            //        {
            //            pointIndex = 2;
            //        }
            //        else if (triangle.Vertex3 == furthestPoint)
            //        {
            //            pointIndex = 3;
            //        }
            //        else continue;



            //        // if the empty segment contains the point,  then we found our triangle.
            //        // else if it's a triangle with an empty segment not containing the furthest point,
            //        // then it's simple a triangle touching the edge AND a hole

            //        if (pointIndex == 1 && (triangle.Neighbour2.Count == 0 || triangle.Neighbour3.Count == 0))
            //        {
            //            furthestTriangle = triangle;
            //            break;
            //        }

            //        if (pointIndex == 2 && (triangle.Neighbour3.Count == 0 || triangle.Neighbour1.Count == 0))
            //        {
            //            furthestTriangle = triangle;
            //            break;
            //        }

            //        if (pointIndex == 3 && (triangle.Neighbour1.Count == 0 || triangle.Neighbour2.Count == 0))
            //        {
            //            furthestTriangle = triangle;
            //            break;
            //        }
            //    }
            //}



            // shouldn't be null, but safety just in case
            //if (furthestTriangle == null) return resultingPolygon;
            //Triangle currentTriangle = furthestTriangle;
            //Vect currentSharedPoint = furthestPoint;
            //Vect previousSharedPoint = null;

            //List<Vect> outsidePointList = new List<Vect>() { furthestPoint };
            //bool stillInFirstTriangle = true;

            //do
            //{
            //    break; // TODO REMOVE THIS BREAK

            //    // we get which vertex index it is
            //    int pointIndex = -1;
            //    if (currentTriangle.Vertex1 == currentSharedPoint)
            //    {
            //        pointIndex = 1;
            //    }
            //    else if (currentTriangle.Vertex2 == currentSharedPoint)
            //    {
            //        pointIndex = 2;
            //    }
            //    else
            //    {
            //        pointIndex = 3;
            //    }

            //    // we get which segment is the outside segment
            //    // we can actually obtain two points here, which is problematic as we don't want to cycle backwards
            //    // so therefore we got both points, check which one we already checked and take the other

            //    int otherPointIndex = -1;
            //    int secondOtherPointIndex = -1;

            //    Vect otherPoint = null;
            //    if (currentTriangle.Neighbour1.Count == 0)
            //    {

            //        if (pointIndex == 2)
            //        {
            //            otherPointIndex = 3;
            //        }
            //        else if (pointIndex == 3)
            //        {
            //            otherPointIndex = 2;
            //        }
            //    }
            //    if (currentTriangle.Neighbour2.Count == 0)
            //    {

            //        if (otherPointIndex == -1)
            //        {

            //            if (pointIndex == 1)
            //            {
            //                otherPointIndex = 3;
            //            }
            //            else if (pointIndex == 3)
            //            {
            //                otherPointIndex = 1;
            //            }
            //        }
            //        else
            //        {
            //            if (pointIndex == 1)
            //            {
            //                secondOtherPointIndex = 3;
            //            }
            //            else if (pointIndex == 3)
            //            {
            //                secondOtherPointIndex = 1;
            //            }
            //        }
            //    }
            //    if (currentTriangle.Neighbour3.Count == 0)
            //    {
            //        if (otherPointIndex == -1)
            //        {
            //            if (pointIndex == 1)
            //            {
            //                otherPointIndex = 2;
            //            }
            //            else if (pointIndex == 2)
            //            {
            //                otherPointIndex = 1;
            //            }
            //        }
            //        else
            //        {
            //            if (pointIndex == 1)
            //            {
            //                secondOtherPointIndex = 2;
            //            }
            //            else if (pointIndex == 2)
            //            {
            //                secondOtherPointIndex = 1;
            //            }
            //        }
            //    }

            //    // after getting which segment it is, we get the other point on the segment, since it's the other point on the outside

            //    switch (otherPointIndex)
            //    {
            //        case 1: otherPoint = currentTriangle.Vertex1; break;
            //        case 2: otherPoint = currentTriangle.Vertex2; break;
            //        case 3: otherPoint = currentTriangle.Vertex3; break;
            //        // this shouldn't ever happen, but again safety in case it does
            //        default: return resultingPolygon;
            //    }

            //    if(previousSharedPoint != null) // making sure we don't go back to a point we already used
            //    {
            //        if(previousSharedPoint == otherPoint)
            //        {
            //            switch (secondOtherPointIndex)
            //            {
            //                case 1: otherPoint = currentTriangle.Vertex1; break;
            //                case 2: otherPoint = currentTriangle.Vertex2; break;
            //                case 3: otherPoint = currentTriangle.Vertex3; break;
            //                // this shouldn't ever happen, but again safety in case it does
            //                default: return resultingPolygon;
            //            }
            //        }
            //    }

            //    // we find the next neighbhour sharing that point and lying on the outside (should be only one)
            //    previousSharedPoint = currentSharedPoint;
            //    currentSharedPoint = otherPoint;
            //    if (currentSharedPoint == furthestPoint) break;
            //    outsidePointList.Add(currentSharedPoint);
            //    Triangle nextTriangle = GetNextTriangleSharingPoint(currentTriangle, otherPoint, triangles);

            //    if (nextTriangle == null) return resultingPolygon; // safety
            //    if(currentTriangle != nextTriangle)
            //    {
            //        stillInFirstTriangle = false;
            //    }

            //    currentTriangle = nextTriangle;

            //    // we set the otherpoint we found to be the next point


            //} while (currentTriangle != furthestTriangle || stillInFirstTriangle);

            SimplifiedPolygon resultingPolygon = null;
            return resultingPolygon;
        }


        private static Triangle GetNextTriangleSharingPoint(Triangle triangle, Vect sharedPoint, List<Triangle> triangleList)
        {

            Triangle currentTriangle = triangle;

            do
            {


                Triangle correctNeighbour = null;

                // we check the first neighbours

                foreach (Triangle neighbour in currentTriangle.Neighbour1)
                {
                    if (neighbour.ContainsVertex(sharedPoint))
                    {
                        correctNeighbour = neighbour;
                    }
                }

                // the second neighbours

                if (correctNeighbour == null)
                {
                    foreach (Triangle neighbour in currentTriangle.Neighbour2)
                    {
                        if (neighbour.ContainsVertex(sharedPoint))
                        {
                            correctNeighbour = neighbour;
                        }
                    }
                }

                // the third neighbours

                if (correctNeighbour == null)
                {
                    foreach (Triangle neighbor in currentTriangle.Neighbour3)
                    {
                        if (neighbor.ContainsVertex(sharedPoint))
                        {
                            correctNeighbour = neighbor;
                        }
                    }
                }

                // if we didn't find the point by now, then the point lies on the same triangle

                if (correctNeighbour == null) return currentTriangle;

                // now we check if the neighbour is an edge triangle (has an entire segment on the edge) or simply touches the edge

                int pointIndex = -1;

                if (correctNeighbour.Vertex1 == sharedPoint)
                {
                    pointIndex = 1;
                }
                else if (correctNeighbour.Vertex2 == sharedPoint)
                {
                    pointIndex = 2;
                }
                else if (correctNeighbour.Vertex3 == sharedPoint)
                {
                    pointIndex = 3;
                }
                // safety check
                else return null;

                // if side2/3 has no neighbours, it means it's on an empty edge containing point 1, and therefore is an edge. same for the other checks

                if (pointIndex == 1 && (correctNeighbour.Neighbour2.Count == 0 || correctNeighbour.Neighbour3.Count == 0))
                {
                    return correctNeighbour;
                }

                if (pointIndex == 2 && (correctNeighbour.Neighbour3.Count == 0 || correctNeighbour.Neighbour1.Count == 0))
                {
                    return correctNeighbour;
                }

                if (pointIndex == 3 && (correctNeighbour.Neighbour1.Count == 0 || correctNeighbour.Neighbour2.Count == 0))
                {
                    return correctNeighbour;
                }

                // if none of the checks above passed through, then we found a neighbour that contains the edge point, but is not on the edge itself
                // in this case, we look for neighbours for the current neighbour and repeat until we find one on the edge

                currentTriangle = correctNeighbour;

            } while (true);

        }

        private static void debugPrintTrianglesDesmos(List<Triangle> triangles, string fileNameKeywords = "")
        {
            Vect averagePoint = new Vect(0, 0, 0);

            HashSet<Vect> uniquePoints = new();

            foreach (Triangle triangle in triangles)
            {
                uniquePoints.Add(triangle.Vertex1);
                uniquePoints.Add(triangle.Vertex2);
                uniquePoints.Add(triangle.Vertex3);
            }

            foreach (Vect point in uniquePoints)
            {
                averagePoint += point;
            }

            averagePoint /= uniquePoints.Count;

            Vect normal = triangles[0].GetNormal();
            Vect v1 = new Vect(1, 0, 0) ^ normal;
            if (v1.Norm == 0)
            {
                v1 = new Vect(0, 1, 0) ^ normal;
            }

            v1 = v1.Direction;
            Vect v2 = (normal ^ v1).Direction;

            StringBuilder desmosHtmlString = new("// START" + "\n");
            int graphId = 0;
            foreach (Triangle triangle in triangles)
            {
                graphId++;
                Vect vertex1 = triangle.Vertex1;
                Vect vertex2 = triangle.Vertex2;
                Vect vertex3 = triangle.Vertex3;

                Vect diff1 = (vertex1 - averagePoint);
                Vect diff2 = (vertex2 - averagePoint);
                Vect diff3 = (vertex3 - averagePoint);

                double x1 = diff1 * v1, y1 = diff1 * v2;
                double x2 = diff2 * v1, y2 = diff2 * v2;
                double x3 = diff3 * v1, y3 = diff3 * v2;

                string polygonString =
                    string.Format(" id: 'graph{0}', latex: '({1:0.#}, {2:0.#}), ({3:0.#}, {4:0.#}), ({5:0.#}, {6:0.#}), ({1:0.#}, {2:0.#})', lines: true",
                    graphId, x1, y1, x2, y2, x3, y3);
                string codeLine = "calculator.setExpression({" + polygonString + "});";

                desmosHtmlString.AppendLine(codeLine);


            }

            desmosHtmlString.AppendLine("//END");

            string HtmlFileContent = "<html> <head> <title> Desmos Applet Template </title> </head>"
                + " <script src = \"https://www.desmos.com/api/v1.7/calculator.js?apiKey=dcb31709b452b1cf9dc26972add0fda6\" ></script > "
                + "<body> <div id = \"calculator\" style = \"width: 100%; height: 100%\"> </div>  </body> <script> "

                + "	var elt = document.getElementById('calculator'); var calculator = Desmos.GraphingCalculator(elt); \n"

                + desmosHtmlString.ToString()

                + "</script> </html> "

                ;


            DateTime dateNow = DateTime.Now;
            string fileName = String.Format("Polygon Visualisation " + dateNow.ToString("d-M") + " at " + dateNow.ToString("h-m") + " total {1} triangles", dateNow, triangles.Count);
            fileName += fileNameKeywords + ".Html";
            string filePath = System.IO.Path.GetTempPath() + "GIM\\";
            if (!System.IO.Directory.Exists(filePath))
                System.IO.Directory.CreateDirectory(filePath);

            System.IO.File.WriteAllTextAsync(filePath + fileName, HtmlFileContent);
        }


    }
}
