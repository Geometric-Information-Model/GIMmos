using System;
using System.Collections.Generic;
using System.Linq;

namespace GIMcompress.Shrinkwrap
{
    public class Triangle
    {
        public const int DIMENSION_INDEX_X = 0;
        public const int DIMENSION_INDEX_Y = 1;
        public const int DIMENSION_INDEX_Z = 2;

        public int dimensionIndex = 0;
        public static int CompareTriangles(Triangle a, Triangle b)
        {
            if(a.MinVertexValue > b.MinVertexValue)
                return 1;
            else if (a.MinVertexValue < b.MinVertexValue)
            {
                return -1;
            }
            return 0;
        }

        // static to keep track of all different Triangles

        private Vect vertex1;
        private Vect vertex2;
        private Vect vertex3;

        private Vect calculatedCircleCenter;
        private double calculatedCircleRadius;

        private int id = -1;
        private TriangleMesh mesh;

        private List<Triangle> neighbour1 = null; // neighbours opposite to vertex 1 (on seg 2-3)
        private List<Triangle> neighbour2 = null; // neighbours opposite to vertex 2 (on seg 3-1)
        private List<Triangle> neighbour3 = null; // neighbours opposite to vertex 3 (on seg 1-2)

        public bool is2D = false;

        public bool planarChecked = false;
        public bool planar = false;

        private double minVertexValue = double.MinValue;
        private double maxVertexValue = double.MinValue;


        #region properties
        
        public double MinVertexValue
        {
            get{
                if (minVertexValue != double.MinValue) return minVertexValue;
                if(vertex1[dimensionIndex] <= vertex2[dimensionIndex] && vertex1[dimensionIndex] <= vertex3[dimensionIndex])
                {
                    minVertexValue = vertex1[dimensionIndex];
                }
                else if( vertex2[dimensionIndex] <= vertex1[dimensionIndex] && vertex2[dimensionIndex] <= vertex3[dimensionIndex])
                {
                    minVertexValue = vertex2[dimensionIndex];
                }
                else
                {
                    minVertexValue = vertex3[dimensionIndex];
                }

                return minVertexValue;
            }
        }

        public double MaxVertexValue
        {
            get
            {
                if (maxVertexValue != double.MinValue) return maxVertexValue;
                if (vertex1[dimensionIndex] >= vertex2[dimensionIndex] && vertex1[dimensionIndex] >= vertex3[dimensionIndex])
                {
                    maxVertexValue = vertex1[dimensionIndex];
                }
                else if (vertex2[dimensionIndex] >= vertex1[dimensionIndex] && vertex2[dimensionIndex] >= vertex3[dimensionIndex])
                {
                    maxVertexValue = vertex2[dimensionIndex];
                }
                else
                {
                    maxVertexValue = vertex3[dimensionIndex];
                }

                return maxVertexValue;
            }
        }

        public Vect Center
        {
            get
            {
                if(calculatedCircleCenter == null)
                {
                    CalculateCircleCenterAndRadius();
                }

                return calculatedCircleCenter;
            }
        }

        public double Radius
        {
            get
            {
                if(calculatedCircleRadius == null)
                {
                    CalculateCircleCenterAndRadius();
                }

                return calculatedCircleRadius;
            }
        }

      

        public Vect this[int index]
        {
            get
            {
                switch (index)
                {
                    case 1: return vertex1;
                    case 2: return vertex2;
                    case 3: return vertex3;
                    default: return null;
                }
            }

            set
            {
                switch (index)
                {
                    case 1: vertex1 = value; break;
                    case 2: vertex2 = value; break;
                    case 3: vertex3 = value; break;
                    default:; break;
                }
            }
        }

        public List<Triangle>[] Neighbour
        {
            get
            {
                return new List<Triangle>[] { neighbour1, neighbour2, neighbour3 };
            }
        }

        public int Id
        {
            get { return id; }
            set { if (id == -1) id = value; }
        }

        public Vect Vertex1
        {
            get { return vertex1; }
            set { vertex1 = value; }
        }
        public Vect Vertex2
        {
            get { return vertex2; }
            set { vertex2 = value; }
        }

        public Vect Vertex3
        {
            get { return vertex3; }
            set { vertex3 = value; }
        }

        public List<Triangle> Neighbour1
        {
            set { neighbour1 = value; }
            get { return neighbour1; }
        }

        public List<Triangle> Neighbour2
        {
            set { neighbour2 = value; }
            get { return neighbour2; }
        }

        public List<Triangle> Neighbour3
        {
            set { neighbour3 = value; }
            get { return neighbour3; }
        }

        public TriangleMesh Mesh
        {
            get { return mesh; }
            set { mesh = value; }
        }

        #endregion

        public void CalculateCircleCenterAndRadius()
        {
            Vect pointA = vertex1;
            Vect pointB = vertex2;
            Vect pointC = vertex3;

            // if two of the points are equal, we get the center of two points

            if (pointA == pointB)
            {
                calculatedCircleCenter = (pointA + pointC) / 2;
                calculatedCircleRadius = (pointA - pointC).Norm;
                return;
            }
            if(pointA == pointC)
            {
                calculatedCircleCenter = (pointA + pointB) / 2;
                calculatedCircleRadius = (pointA - pointB).Norm;
                return;
            }
            if(pointB == pointC)
            {
                calculatedCircleCenter = (pointA + pointB) / 2;
                calculatedCircleRadius = (pointA - pointB).Norm;
                return;
            }

            Vect vectAB = (pointB - pointA).Direction;
            Vect vectAC = (pointC - pointA).Direction;

            // else if the three points are aligned, we get the center of the biggest distance

            if (vectAB.IsCollinearTo(vectAC))
            {
                double dist1 = (pointA - pointB).Norm;
                double dist2 = (pointA - pointC).Norm;
                double dist3 = (pointB - pointC).Norm;

                if(dist1 >= dist2 && dist1 >= dist3)
                {
                    calculatedCircleCenter = (pointA + pointB) / 2;
                    calculatedCircleRadius = (pointA - pointB).Norm;
                    return;
                }

                if(dist2 >= dist1 && dist2 >= dist3)
                {
                    calculatedCircleCenter = (pointA + pointC) / 2;
                    calculatedCircleRadius = (pointA - pointC).Norm;
                    return;
                }

                if(dist3 >= dist1 && dist3 >= dist2)
                {
                    calculatedCircleCenter = (pointB + pointC) / 2;
                    calculatedCircleRadius = (pointB - pointC).Norm;
                    return;
                }
                
            }

            calculatedCircleCenter = (pointA + pointB + pointC) / 3;
            double OA = (pointA - calculatedCircleCenter).Norm;
            double OB = (pointB - calculatedCircleCenter).Norm;
            double OC = (pointC - calculatedCircleCenter).Norm;

            calculatedCircleRadius = Math.Max(OA, Math.Max(OB, OC));
        }

        public Triangle(Vect vertex1, Vect vertex2, Vect vertex3, TriangleMesh mesh = null)
        {
            this.vertex1 = vertex1;
            this.vertex2 = vertex2;
            this.vertex3 = vertex3;
            if(mesh != null)
            {
                id = mesh.Counter;
                mesh.Triangles.Add(this);
                mesh.Counter++;
                this.mesh = mesh;
            }
        }

        public int CoincidesWith(Triangle other)
        {
            bool v1 = other.ContainsVertex(vertex1), v2 = other.ContainsVertex(vertex2), v3 = other.ContainsVertex(vertex3);
            if (v1 && v2)
            {
                if (vertex1 != vertex2) return 3;
            };
            if (v2 && v3)
            {
                if (vertex2 != vertex3) return 1;
            }
            if (v3 && v1)
            {
                if (vertex3 != vertex1) return 2;
            }
            return 0;
        }

        public bool IsNeighbour(Triangle other)
        {
            if(neighbour1 == null || neighbour2 == null ||neighbour3 == null)
            {
                neighbour1 = new();
                neighbour2 = new();
                neighbour3 = new();
            }
            if (neighbour1.Contains(other)) return true;
            if (neighbour2.Contains(other)) return true;
            if (neighbour3.Contains(other)) return true;
            return false;
        }

        // returns true if the neighbour was added successfully 
        public bool AddNeighbourIfPossible(Triangle other)
        {
            if (IsNeighbour(other)) return false;
            if (this == other) return false;
            int coincidence = CoincidesWith(other);
            if (coincidence == 1) neighbour1.Add(other);
            if (coincidence == 2) neighbour2.Add(other);
            if (coincidence == 3) neighbour3.Add(other);
            if (coincidence > 0) return true;
            return false;
        }

        public bool RemoveNeighbourIfExists(Triangle other)
        {
            if (neighbour1.Contains(other))
            {
                neighbour1.Remove(other);
                return true;
            }

            if (neighbour2.Contains(other))
            {
                neighbour2.Remove(other);
                return true;
            }

            if (neighbour3.Contains(other))
            {
                neighbour3.Remove(other);
                return true;
            }

            return false;
        }

        public bool ContainsVertex(Vect vertex)
        {
            return (vertex1 == vertex || vertex2 == vertex || vertex3 == vertex);
        }

        // figuring out if a triangle is missing neighbours on at least one side
        public bool MissingNeighboursOnOneSide()
        {
            return neighbour1.Count == 0 || neighbour2.Count == 0 || neighbour3.Count == 0;
        }

        public string ToDesmosString()
        {
            return "triangle(" + vertex1 + ", " + vertex2 + ", " + vertex3 + ")";
        }

        public override string ToString()
        {
            if(neighbour1 == null || neighbour2 == null || neighbour3 == null)
            {
                return "[" +vertex1 + ", " + vertex2 + ", " + vertex3 + "]";
            }
            string str = id + ": [" + vertex1 + ", " + vertex2 + ", " + vertex3 + "], n1: " + neighbour1.Count + ", n2: " + neighbour2.Count +", n3: " + neighbour3.Count;

            if (IsDegenerate()) str = "DEGEENERATE: " + str;
            else if (IsFlattened()) str = "FLATTENED: " + str;
            return str;
        }

        public override int GetHashCode()
        {
            if (!is2D) return base.GetHashCode();
            unchecked
            {
                int v1 = vertex1.GetHashCode();
                int v2 = vertex2.GetHashCode();
                int v3 = vertex3.GetHashCode();
                int hash = v1 + v2 + v3;
                return hash;
            };
        }

        public bool IsEqualTo(Triangle other)
        {
            HashSet<Vect> thisPoints = new() { vertex1, vertex2, vertex3 };
            HashSet<Vect> otherPoints = new() { other.vertex1, other.vertex2, other.vertex3 };

            return (thisPoints.SetEquals(otherPoints));
        }

        public override bool Equals(object obj)
        {
            if (!is2D) return base.Equals(obj);
            if (obj is Triangle)
            {
                HashSet<Vect> vertexes = new HashSet<Vect>() { vertex1, vertex2, vertex3 };

                Triangle otherTriangle = (Triangle)obj;

                if (!vertexes.Contains(otherTriangle.vertex1)) return false;
                if (!vertexes.Contains(otherTriangle.vertex2)) return false;
                if (!vertexes.Contains(otherTriangle.Vertex3)) return false;

                return true;
            }
            return false;
        }

        public string GetFreeCADString()
        {
            return "Part.show(Part.Face(Part.makePolygon(" +
                "[App.Vector" +
                vertex1 +
                "," +
                "App.Vector" +
                vertex2 +
                "," +
                "App.Vector" +
                vertex3 +
                "," +
                "App.Vector" +
                vertex1 +
                "])))";
        }

        public string GetFreeCADMeshString()
        {
            return
                "[" + vertex1[0] + ".0, " + vertex1[1] + ".0, " + vertex1[2] + ".0], " +
                "[" + vertex2[0] + ".0, " + vertex2[1] + ".0, " + vertex2[2] + ".0], " +
                "[" + vertex3[0] + ".0, " + vertex3[1] + ".0, " + vertex3[2] + ".0],";
        }

        public void Round(double precision = 1)
        {

            vertex1 = new Vect(
                Math.Round(vertex1[0] / precision) * precision,
                Math.Round(vertex1[1] / precision) * precision,
                Math.Round(vertex1[2] / precision) * precision
                );

            vertex2 = new Vect(
                Math.Round(vertex2[0] / precision) * precision,
                Math.Round(vertex2[1] / precision) * precision,
                Math.Round(vertex2[2] / precision) * precision
                );

            vertex3 = new Vect(
                Math.Round(vertex3[0] / precision) * precision,
                Math.Round(vertex3[1] / precision) * precision,
                Math.Round(vertex3[2] / precision) * precision
                );

        }

        public bool IsVertex()
        {
            return vertex1 == vertex2 && vertex2 == vertex3;
        }

        /// <summary>
        /// Checks if the triangle is degenerate, which occurs when any two of its vertices are coincident.
        /// </summary>
        /// <returns>Returns true if the triangle is degenerate (any two vertices are the same), otherwise false.</returns>
        /// <remarks>
        /// A triangle is considered degenerate if any of its vertices are the same, resulting in
        /// a triangle with zero area. This method checks for equality between each pair of vertices.
        /// </remarks>
        public bool IsDegenerate()
        {
            return (vertex1 == vertex2 || vertex2 == vertex3 || vertex3 == vertex1);
        }

        /// <summary>
        /// Determines whether the triangle is flattened, i.e., all its vertices lie in a single plane.
        /// </summary>
        /// <returns>Returns true if the triangle is flattened, otherwise false.</returns>
        /// <remarks>
        /// This method calculates the cross product (denoted by ^) of the vectors formed by two
        /// sides of the triangle. If the norm of this cross product is zero, it indicates that
        /// the triangle is flattened as the sides are collinear.
        /// </remarks>
        public bool IsFlattened()
        {
            Vect diff1 = vertex2 - vertex1;
            Vect diff2 = vertex3 - vertex1;

            return ((diff1 ^ diff2).Norm == 0);
        }

        public bool fixNeighbourOverlap(int side)
        {
            List<Triangle> neighbours = null;
            Vect vertexA = null, vertexB = null, vertexC = null;
            switch (side)
            {
                case 1: neighbours = neighbour1; vertexA = vertex2; vertexB = vertex3; vertexC = vertex1; break;
                case 2: neighbours = neighbour2; vertexA = vertex3; vertexB = vertex1; vertexC = vertex2; break;
                case 3: neighbours = neighbour3; vertexA = vertex1; vertexB = vertex2; vertexC = vertex3;  break;
                default: return false;
            }


            foreach(Triangle otherTriangle in neighbours)
            {
                // getting the triangle centers
                Vect center = (vertex1 + vertex2 + vertex3) / 3;
                Vect otherCenter = (otherTriangle.vertex1 + otherTriangle.vertex2 + otherTriangle.vertex3) / 3;

                // getting the center of the shared segment to tell whether triangles are on the same side or not
                Vect segmentCenter = (vertexA + vertexB) / 2;

                // getting the normal of the segment
                Vect segmentDirection = (vertexB - vertexA).Direction;
                Vect segmentNormal = (vertexC - vertexB);
                segmentNormal = segmentNormal - (segmentNormal * segmentDirection) * segmentDirection;
                segmentNormal = segmentNormal.Direction;


                Vect centerDirection = (center - segmentCenter) * (segmentNormal) * segmentNormal;
                Vect otherCenterDirection = (otherCenter - segmentCenter) * (segmentNormal) * segmentNormal;

                if(centerDirection * otherCenterDirection > 0) // both triangles are on the same direction because of positive scalar product
                {
                    // we fix these triangles, and the way we do that is by splitting overlapping intersecting triangles, and removing contained overlapping triangles

                    bool intersectingTriangles = false;

                    Vect vertexP = null;
                    int vertexPIndex = -1;

                    // getting the last point of the other triangle
                    if( (otherTriangle.vertex1 == vertexA || otherTriangle.vertex2 == vertexA) && (otherTriangle.vertex2 == vertexB || otherTriangle.vertex1 == vertexB))
                    {
                        vertexP = otherTriangle.vertex3;
                        vertexPIndex = 3;
                    } 

                    else if ((otherTriangle.vertex1 == vertexA || otherTriangle.vertex3 == vertexA) && (otherTriangle.vertex3 == vertexB || otherTriangle.vertex1 == vertexB))
                    {
                        vertexP = otherTriangle.vertex2;
                        vertexPIndex = 2;
                    }

                    else if ((otherTriangle.vertex2 == vertexA || otherTriangle.vertex3 == vertexA) && (otherTriangle.vertex3 == vertexB || otherTriangle.vertex2 == vertexB))
                    {
                        vertexP = otherTriangle.vertex1;
                        vertexPIndex = 1;
                    }

                    if(vertexPIndex == -1) // these triangles don't share a side, but somehow we got here
                    {
                        otherTriangle.RemoveNeighbourIfExists(this);
                        RemoveNeighbourIfExists(otherTriangle);
                        return false;
                    }

                    double bigArea = ((vertexC - vertexA) ^ (vertexB - vertexA)).Norm;
                    double alpha = ((vertexC - vertexP) ^ (vertexB - vertexP)).Norm / bigArea;
                    double beta = ((vertexC - vertexP) ^ (vertexA - vertexP)).Norm / bigArea;

                    double gamma = 1 - alpha - beta;

                    bool alphaCond = (alpha >= 0) && (alpha <= 1);
                    bool betaCond = (beta >= 0) && (beta <= 1);
                    bool gammaCond = (gamma >= 0) && (gamma <= 1);

                    if(alphaCond && betaCond && gammaCond) // Triangle neighbour is fully contained, this is the easy case
                    {
                        foreach(Triangle overlapNeighbour in otherTriangle.Neighbour[vertexPIndex - 1]) // we remove the neighbours of 
                        {
                            overlapNeighbour.RemoveNeighbourIfExists(otherTriangle);
                            bool success = AddNeighbourIfPossible(overlapNeighbour);
                            if (success)
                            {
                                overlapNeighbour.AddNeighbourIfPossible(this);
                            }
                        }

                        mesh.RemoveTriangle(otherTriangle);
                        

                        return true;
                    }

                    // now the reverse, if THIS triangle is contained in the other (basically switching P with C)

                    bigArea = Math.Abs((vertexP - vertexA) * (vertexB - vertexA));

                    alpha = Math.Abs((vertexP - vertexC) * (vertexB - vertexC)) / bigArea;
                    beta = Math.Abs((vertexP - vertexC) * (vertexA - vertexC)) / bigArea;

                    gamma = 1 - alpha - beta;

                    alphaCond = (alpha >= 0) && (alpha <= 1);
                    betaCond = (beta >= 0) && (beta <= 1);
                    gammaCond = (gamma >= 0) && (gamma <= 1);

                    if (alphaCond && betaCond && gammaCond) // Triangle THIS is fully contained, this is the easy case
                    {
                        foreach (Triangle overlapNeighbour in Neighbour[vertexPIndex - 1]) // we remove the neighbours of 
                        {
                            overlapNeighbour.RemoveNeighbourIfExists(this);
                            bool success = otherTriangle.AddNeighbourIfPossible(overlapNeighbour);
                            if (success)
                            {
                                overlapNeighbour.AddNeighbourIfPossible(otherTriangle);
                            }
                        }

                        mesh.RemoveTriangle(this);



                        return true;
                    }

                    // if both conditions above are not true, then the triangles overlap and intersect, and must be split into three triangles

                    // the triangles here are ABC and ABP
                    // we have to figure out whether AP and BC intersect or BP and AC (no other intersections are possible)


                    //however in order to make this calculation, it's very helpful to bring things to the 2D plane

                    // we will assume the point A to be the origin
                    Vect v1 = segmentDirection;
                    Vect v2 = segmentNormal;

                    Vect a2d = new Vect(0, 0);
                    Vect b2d = new Vect((vertexB - vertexA).Norm, 0);

                    double cX = (vertexC - vertexA) * (v1);
                    double cY = (vertexC - vertexA) * (v2);

                    Vect c2d = new Vect(cX, cY);

                    double pX = (vertexP - vertexA) * (v1);
                    double pY = (vertexP - vertexA) * (v2);

                    Vect p2d = new Vect(pX, pY);

                    Vect u = p2d - a2d;
                    Vect v = c2d - b2d;

                    Vect constantVector = new Vect(b2d[0] - a2d[0], b2d[1] - a2d[1]);
                    Matrix transformMatrix = new Matrix(new Vect(u[0], v[0]), new Vect(u[1], v[1]));

                    double a = -1, b = -1;

                    if(transformMatrix.Determinant > 0) // when the lines are parallel, the matrix has NULL determinant and is thus uninvertible
                    {
                        Matrix inverse = transformMatrix.Inverse;

                        Vect result = inverse * constantVector;

                        a = result[0];
                        
                        b = -result[1];
                    }

                   

                    bool aCond = a > 0 && a < 1;
                    bool bCond = b > 0 && b < 1;

                    Vect intersectionPoint = null;

                    if (aCond && bCond) // the intersection is on the segments AP and BC

                        // in this case, the two triangles ABP and ABC will become 3: AOB, AOC and BOP
                        // with O = intersectionPoint
                    {

                        // this triangle will become AOB or ABO intead of ABC
                        intersectionPoint = vertexA + a * (vertexP - vertexA);

                        this[side] = intersectionPoint;

                        // we remove all the neighbours and reassign them, to make sure every neighbour goes in the right spot
                        List<Triangle>[] allTriangleLists = new List<Triangle>[7];

                        for (int i = 0; i <= 2; i++)
                        {
                            allTriangleLists[i] = new();
                            for (int index = 0; index < Neighbour[i].Count; index++)
                            {
                                // we add the triangle to our new list
                                Triangle triangle = Neighbour[i][index];
                                allTriangleLists[i].Add(triangle);

                                // then we remove it as a neighbour
                                bool success = RemoveNeighbourIfExists(triangle);
                                if (success)
                                {
                                    triangle.RemoveNeighbourIfExists(this);
                                    index--;
                                }
                            }
                        }

                        // the triangle "neighbour" will become BOP or OBP instead of ABP

                        int otherSide = -1;

                        for(int i=1; i<=3; i++)
                        {
                            if(otherTriangle[i] == vertexA)
                            {
                                otherSide = i;
                                break;
                            }
                        }

                        otherTriangle[otherSide] = intersectionPoint;

                        // likewise, we strip the other triangle of all its neighbours

                        for (int i = 0; i <= 2; i++)
                        {
                            allTriangleLists[i + 3] = new();
                            for (int index = 0; index < otherTriangle.Neighbour[i].Count; index++)
                            {
                                // we add the triangle to our new list
                                Triangle triangle = otherTriangle.Neighbour[i][index];
                                allTriangleLists[i + 3].Add(triangle);

                                // then we remove it as a neighbour
                                bool success = otherTriangle.RemoveNeighbourIfExists(triangle);
                                if (success)
                                {
                                    triangle.RemoveNeighbourIfExists(otherTriangle);
                                    index--;
                                }
                            }
                        }

                        // new triangle will be AOC

                        Triangle newTriangle = new Triangle(vertexA, intersectionPoint, vertexC, mesh);

                        allTriangleLists[6] = new List<Triangle>() { this, otherTriangle, newTriangle };

                        foreach (var neighbourList in allTriangleLists)
                        {
                            foreach (var triangle in neighbourList)
                            {
                                bool success;
                                success = this.AddNeighbourIfPossible(triangle);
                                if (success) triangle.AddNeighbourIfPossible(this);

                                success = otherTriangle.AddNeighbourIfPossible(triangle);
                                if (success) triangle.AddNeighbourIfPossible(otherTriangle);

                                success = newTriangle.AddNeighbourIfPossible(triangle);
                                if (success) triangle.AddNeighbourIfPossible(newTriangle);
                            }
                        }


                        return true;
                    }

                    u = p2d - b2d;
                    v = c2d - a2d;

                    constantVector = new Vect(a2d[0] - b2d[0], a2d[1] - b2d[1]);
                    transformMatrix = new Matrix(new Vect(u[0], v[0]), new Vect(u[1], v[1]));

                    a = -1; b = -1;

                    if (transformMatrix.Determinant > 0) // when the determinant is NULL or zero, the lines are parallel and the matrix is not invertible
                    {
                        Matrix inverse = transformMatrix.Inverse;

                        Vect result = inverse * constantVector;

                        a = result[0];

                        b = -result[1];
                    }


                    aCond = a > 0 && a < 1;
                    bCond = b > 0 && b < 1;

                    if(aCond && bCond) // the intersection is on the segments AC and BP
                        // in this case, the two triangles will become AOB, AOP and BOC
                    {
                        intersectionPoint = vertexA + b * (vertexC - vertexA);

                        this[side] = intersectionPoint;

                        // we remove all the neighbours and reassign them, to make sure every neighbour goes in the right spot
                        List<Triangle>[] allTriangleLists = new List<Triangle>[7];

                        for(int i =0; i <=2; i++)
                        {
                            allTriangleLists[i] = new();
                            for (int index = 0; index < Neighbour[i].Count; index++)
                            {
                                // we add the triangle to our new list
                                Triangle triangle = Neighbour[i][index];
                                allTriangleLists[i].Add(triangle);

                                // then we remove it as a neighbour
                                bool success = RemoveNeighbourIfExists(triangle);
                                if (success)
                                {
                                    triangle.RemoveNeighbourIfExists(this);
                                    index--;
                                }
                            }
                        }

                        // the triangle "neighbour" will become AOP or OAP instead of ABP

                        int otherSide = -1;

                        for (int i = 1; i <= 3; i++)
                        {
                            if (otherTriangle[i] == vertexB)
                            {
                                otherSide = i;
                                break;
                            }
                        }

                        otherTriangle[otherSide] = intersectionPoint;

                        // likewise, we strip the other triangle of all its neighbours

                        for (int i = 0; i <= 2; i++)
                        {
                            allTriangleLists[i + 3] = new();
                            for (int index = 0; index < otherTriangle.Neighbour[i].Count; index++)
                            {
                                // we add the triangle to our new list
                                Triangle triangle = otherTriangle.Neighbour[i][index];
                                allTriangleLists[i + 3].Add(triangle);

                                // then we remove it as a neighbour
                                bool success = otherTriangle.RemoveNeighbourIfExists(triangle);
                                if (success)
                                {
                                    triangle.RemoveNeighbourIfExists(otherTriangle);
                                    index--;
                                }
                            }
                        }

                        // new triangle will be BOC

                        Triangle newTriangle = new Triangle(vertexB, intersectionPoint, vertexC, mesh);

                        allTriangleLists[6] = new List<Triangle>() { this, otherTriangle, newTriangle };

                        foreach (var neighbourList in allTriangleLists)
                        {
                            foreach (var triangle in neighbourList)
                            {
                                bool success;
                                success = this.AddNeighbourIfPossible(triangle);
                                if (success) triangle.AddNeighbourIfPossible(this);

                                success = otherTriangle.AddNeighbourIfPossible(triangle);
                                if (success) triangle.AddNeighbourIfPossible(otherTriangle);

                                success = newTriangle.AddNeighbourIfPossible(triangle);
                                if (success) triangle.AddNeighbourIfPossible(newTriangle);
                            }
                        }



                        return true;
                    }


                }
            }

            return false;
        
        }



        // if the triangle is degenerate (two vertices are the same)
        public void FixDegenerateNeighbourhood()
        {
            // we're going to attach these neighbours to each other, and remove this element
            List<Triangle> neighboursLeft = null;
            List<Triangle> neighboursRight = null;

            if(vertex1 == vertex2 && vertex2 == vertex3)
            {
                mesh.Triangles.Remove(this);
                return;
            }

            if (vertex1 == vertex2)
            {
                neighboursLeft = neighbour1;
                neighboursRight = neighbour2;
            }

            if(vertex2 == vertex3)
            {
                neighboursLeft = neighbour2;
                neighboursRight = neighbour3;
            }

            if(vertex3 == vertex1)
            {
                neighboursLeft = neighbour3;
                neighboursRight = neighbour1;
            }

            // not degenerate
            if (neighboursLeft == null || neighboursRight == null) return;

            foreach(Triangle triangle in neighboursLeft)
            {
                triangle.RemoveNeighbourIfExists(this);
            }

            foreach(Triangle triangle in neighboursRight)
            {
                triangle.RemoveNeighbourIfExists(this);
            }

            foreach(Triangle triangleLeft in neighboursLeft)
            {
                foreach(Triangle triangleRight in neighboursRight)
                {
                    triangleLeft.AddNeighbourIfPossible(triangleRight);
                    triangleRight.AddNeighbourIfPossible(triangleLeft);
                }
            }

            if(neighboursLeft.Count > 1)
            {
                for(int i=0; i<neighboursLeft.Count; i++)
                {
                    Triangle triangle = neighboursLeft[i];
                    for(int j = i +1; j < neighboursLeft.Count; j++)
                    {
                        Triangle other = neighboursLeft[j];
                        triangle.AddNeighbourIfPossible(other);
                        other.AddNeighbourIfPossible(triangle);
                    }
                }
            }


            if (neighboursRight.Count > 1)
            {
                for (int i = 0; i < neighboursRight.Count; i++)
                {
                    Triangle triangle = neighboursRight[i];
                    for (int j = i + 1; j < neighboursRight.Count; j++)
                    {
                        Triangle other = neighboursRight[j];
                        triangle.AddNeighbourIfPossible(other);
                        other.AddNeighbourIfPossible(triangle);
                    }
                }
            }

            Neighbour1 = new();
            Neighbour2 = new();
            Neighbour3 = new();

            //mesh.Triangles.Remove(this);
        }

        // triangle is flat but no degenerate (all points are distinct)
        // in this case, split the neighbhours of the big segment into two small triangles connected to the small segments
        // and call it a day
        public void FixFlattenedNeighbourhood()
        {
            if (!IsFlattened()) return;

            List<Triangle> bigNeighbourhood = null;
            List<Triangle> leftNeighbourhood = null;
            List<Triangle> rightNeighbourhood = null;

            double distance1 = (vertex2 - vertex3).Norm;
            double distance2 = (vertex3 - vertex1).Norm;
            double distance3 = (vertex1 - vertex2).Norm;

            Vect dividingPoint = null;
            Vect leftPoint = null;
            Vect rightPoint = null;

            if(distance1 >= distance2 && distance1 >= distance3)
            {
                bigNeighbourhood = neighbour1;
                leftNeighbourhood = neighbour2;
                rightNeighbourhood = neighbour3;
                dividingPoint = vertex1;
                leftPoint = vertex2;
                rightPoint = vertex3;
            }

            if(distance2 >= distance3 && distance2 >= distance1)
            {
                bigNeighbourhood = neighbour2;
                leftNeighbourhood = neighbour3;
                rightNeighbourhood = neighbour1;
                dividingPoint = vertex2;
                leftPoint = vertex3;
                rightPoint = vertex1;
            }

            if(distance3 >= distance1 && distance3 >= distance2)
            {
                bigNeighbourhood = neighbour3;
                leftNeighbourhood = neighbour1;
                rightNeighbourhood = neighbour2;
                dividingPoint = vertex3;
                leftPoint = vertex1;
                rightPoint = vertex2;
            }

            // removing old neighbours
            foreach(Triangle neighbour in bigNeighbourhood)
            {
                neighbour.RemoveNeighbourIfExists(this);
            }

            foreach (Triangle neighbour in leftNeighbourhood)
            {
                neighbour.RemoveNeighbourIfExists(this);
            }

            foreach (Triangle neighbour in rightNeighbourhood)
            {
                neighbour.RemoveNeighbourIfExists(this);
            }

            Neighbour1 = new();
            Neighbour2 = new();
            Neighbour3 = new();
            //mesh.Triangles.Remove(this);

            if(bigNeighbourhood.Count == 0)
            {
                return;
            }

            // splitting triangles into two chunks
            for (int i=0; i<bigNeighbourhood.Count; i++)
            {
                Triangle triangleChunk1 = bigNeighbourhood[i];
                Triangle triangleChunk2 = triangleChunk1.Divide(dividingPoint, leftPoint, rightPoint);


                if (triangleChunk2 == null) continue;

                foreach(Triangle leftNeighbour in leftNeighbourhood)
                {
                    bool success = triangleChunk1.AddNeighbourIfPossible(leftNeighbour);
                    if (success)
                    {
                        leftNeighbour.AddNeighbourIfPossible(triangleChunk1);
                    }

                    success = triangleChunk2.AddNeighbourIfPossible(leftNeighbour);
                    if (success)
                    {
                        leftNeighbour.AddNeighbourIfPossible(triangleChunk2);
                    }
                }

                foreach(Triangle rightNeighbour in rightNeighbourhood)
                {
                    bool success = triangleChunk1.AddNeighbourIfPossible(rightNeighbour);
                    if (success)
                    {
                        rightNeighbour.AddNeighbourIfPossible(triangleChunk1);
                    }

                    success = triangleChunk2.AddNeighbourIfPossible(rightNeighbour);
                    if (success)
                    {
                        rightNeighbour.AddNeighbourIfPossible(triangleChunk2);
                    }
                }

                if (triangleChunk1.IsDegenerate())
                {
                    triangleChunk1.FixDegenerateNeighbourhood();
                }
                if (triangleChunk2.IsDegenerate())
                {
                    triangleChunk2.FixDegenerateNeighbourhood();
                }

            }


        }

        // divides a triangle to two triangles by splitting a segment along a given vertex
        // the first triangle is THIS triangle, the second triangle is returned
        public Triangle Divide(Vect vertex, Vect leftPoint, Vect rightPoint)
        {
            Vect commonVertex = null;
            Vect oldVertex = null;
            Vect newVertex = null;

            // which two points is it between? cross product is needed for this
            //double cross1 = ((vertex - vertex2) ^ (vertex - vertex3)).Norm;
            //double cross2 = ((vertex - vertex3) ^ (vertex - vertex1)).Norm;
            //double cross3 = ((vertex - vertex1) ^ (vertex - vertex2)).Norm;
            // ^this was commented because it doesn't work if the triangle is flat (all cross products are 0)

            Triangle newTriangle = null;

            if(vertex1 != leftPoint && vertex1 != rightPoint)
            {
                commonVertex = vertex1;
                oldVertex = vertex2;
                newVertex = vertex3;

                vertex3 = vertex;

                newTriangle =  new Triangle(commonVertex, vertex, newVertex, mesh);

                foreach (Triangle triangle in neighbour1)
                {
                    triangle.RemoveNeighbourIfExists(this);
                }


                // adding the lost side's neighbour to the triangle, setting neighbour to empty
                // and setting split segment neighbour to empty
                foreach (Triangle triangle in neighbour2)
                {
                    triangle.RemoveNeighbourIfExists(this);

                    bool success = newTriangle.AddNeighbourIfPossible(triangle);
                    if (success)
                    {
                        triangle.AddNeighbourIfPossible(newTriangle);
                    }
                }


                neighbour1 = new();
                neighbour2 = new();




            }

            else if (vertex2 != leftPoint && vertex2 != rightPoint)
            {
                commonVertex = vertex2;
                oldVertex = vertex3;
                newVertex = vertex1;

                vertex1 = vertex;

                newTriangle = new Triangle(commonVertex, vertex, newVertex, mesh);

                foreach(Triangle triangle in neighbour2)
                {
                    triangle.RemoveNeighbourIfExists(this);
                }

                // adding the lost side's neighbour to the triangle, setting neighbour to empty
                // and setting split segment neighbour to empty
                foreach (Triangle triangle in neighbour3)
                {
                    triangle.RemoveNeighbourIfExists(this);

                    bool success = newTriangle.AddNeighbourIfPossible(triangle);
                    if (success)
                    {
                        triangle.AddNeighbourIfPossible(newTriangle);
                    }
                }


                neighbour2 = new();
                neighbour3 = new();

            }

            else if (vertex3 != leftPoint && vertex3 != rightPoint)
            {
                commonVertex = vertex3;
                oldVertex = vertex1;
                newVertex = vertex2;

                vertex2 = vertex;

                newTriangle = new Triangle(commonVertex, vertex, newVertex, mesh);

                foreach (Triangle triangle in neighbour3)
                {
                    triangle.RemoveNeighbourIfExists(this);
                }


                // adding the lost side's neighbour to the triangle, setting neighbour to empty
                // and setting split segment neighbour to empty
                foreach (Triangle triangle in neighbour1)
                {
                    triangle.RemoveNeighbourIfExists(this);

                    bool success = newTriangle.AddNeighbourIfPossible(triangle);
                    if (success)
                    {
                        triangle.AddNeighbourIfPossible(newTriangle);
                    }
                }


                neighbour3 = new();
                neighbour1 = new();
            }


            if(newTriangle != null)
            {
                this.AddNeighbourIfPossible(newTriangle);
                newTriangle.AddNeighbourIfPossible(this);
            }

            return newTriangle;

        }

        public double GetArea3D()
        {
            double a = (vertex2 - vertex1).Norm;
            double b = (vertex3 - vertex2).Norm;
            double c = (vertex1 - vertex3).Norm;
            double s = (a+b+c)/2;
            var result = Math.Sqrt(s * (s - a) * (s - b) * (s - c));
            if (double.IsNaN(result))
            {
                return 0;
            }
            return result ;
        }

        public double GetArea()
        {

            double vertex1X = vertex1[0];
            double vertex1Y = vertex1[1];
            double vertex2X = vertex2[0];
            double vertex2Y = vertex2[1];
            double vertex3X = vertex3[0];
            double vertex3Y = vertex3[1];
            double temp = vertex1X * (vertex2Y - vertex3Y) + vertex2X * (vertex3Y - vertex1Y) + vertex3X * (vertex1Y - vertex2Y);
            return Math.Abs(temp / 2);


        }




        public Vect GetNormal()
        {
            Vect v1 = vertex2 - vertex1;
            Vect v2 = vertex3 - vertex1;

            return v1 ^ v2;
        }

        public void GroupPlanarNeighbours(List<Triangle> triangleGroup, Vect normal)
        {
            // we already checked this triangle before, no need to do it again
            if (planarChecked) return;

            if (!(GetNormal().IsCollinearTo(normal))) // don't share the same plane
            {
                return;
            }

            planarChecked = true;
            triangleGroup.Add(this);

            foreach (Triangle triangle in neighbour1)
            {
                triangle.GroupPlanarNeighbours(triangleGroup, normal);
            }

            foreach (Triangle triangle in neighbour2)
            {
                triangle.GroupPlanarNeighbours(triangleGroup, normal);
            }

            foreach (Triangle triangle in neighbour3)
            {
                triangle.GroupPlanarNeighbours(triangleGroup, normal);
            }

        }

        public static List<Triangle> ExplodeIntersectingTriangles(Triangle triangleA, Triangle triangleB)
        {
            List<Triangle> result = new List<Triangle>();

            //creating plane base vectors from triangleA
            Vect originPoint = triangleA.vertex1;
            Vect u = (new Vect(1,0,0));
            Vect normal = triangleA.GetNormal().Direction.Round();
            
            u = u - (normal * u) * normal;
            u = u.Direction;
            if(u.Norm == 0)
            {
                u = new Vect(0, 1, 0);
                u = u - (normal * u) * normal;
            }
            Vect v = normal ^ u;
            //removing the u component from v, to create an orthogonal base
            //v = v - (v * u) * u;
            v = v.Direction;

            // getting the 2d coordinates of triangle 1 points
            double APoint1X = (triangleA.vertex1 - originPoint) * u;
            double APoint1Y = (triangleA.vertex1 - originPoint) * v;
            APoint1X = ValueRounder.roundDouble(APoint1X, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            APoint1Y = ValueRounder.roundDouble(APoint1Y, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

            double APoint2X = (triangleA.vertex2 - originPoint) * u;
            double APoint2Y = (triangleA.vertex2 - originPoint) * v;
            APoint2X = ValueRounder.roundDouble(APoint2X, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            APoint2Y = ValueRounder.roundDouble(APoint2Y, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

            double APoint3X = (triangleA.vertex3 - originPoint) * u;
            double APoint3Y = (triangleA.vertex3 - originPoint) * v;
            APoint3X = ValueRounder.roundDouble(APoint3X, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            APoint3Y = ValueRounder.roundDouble(APoint3Y, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            // getting the 2d coordintes of triangle 2 points
            double BPoint1X = (triangleB.vertex1 - originPoint) * u;
            double BPoint1Y = (triangleB.vertex1 - originPoint) * v;
            BPoint1X = ValueRounder.roundDouble(BPoint1X, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            BPoint1Y = ValueRounder.roundDouble(BPoint1Y, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

            double BPoint2X = (triangleB.vertex2 - originPoint) * u;
            double BPoint2Y = (triangleB.vertex2 - originPoint) * v;
            BPoint2X = ValueRounder.roundDouble(BPoint2X, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            BPoint2Y = ValueRounder.roundDouble(BPoint2Y, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

            double BPoint3X = (triangleB.vertex3 - originPoint) * u;
            double BPoint3Y = (triangleB.vertex3 - originPoint) * v;
            BPoint3X = ValueRounder.roundDouble(BPoint3X, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            BPoint3Y = ValueRounder.roundDouble(BPoint3Y, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);


            Vertex2d APoint1 = new(APoint1X, APoint1Y);
            Vertex2d APoint2 = new(APoint2X, APoint2Y);
            Vertex2d APoint3 = new(APoint3X, APoint3Y);
            APoint2.Round();
            APoint3.Round();

            Vertex2d BPoint1 = new(BPoint1X, BPoint1Y);
            Vertex2d BPoint2 = new(BPoint2X, BPoint2Y);
            Vertex2d BPoint3 = new(BPoint3X, BPoint3Y);
            BPoint1.Round();
            BPoint2.Round();
            BPoint3.Round();


            // checking if the triangles share any points, therefore setting them to the same reference

            if(BPoint1 == APoint1)
            {
                BPoint1 = APoint1;
            }
            if(BPoint1 == APoint2)
            {
                BPoint1 = APoint2;
            }
            if(BPoint1 == APoint3)
            {
                BPoint1 = APoint3;
            }

            if(BPoint2 == APoint1)
            {
                BPoint2 = APoint1;
            }
            if (BPoint2 == APoint2)
            {
                BPoint2 = APoint2;
            }
            if(BPoint2 == APoint3)
            {
                BPoint2 = APoint3;
            }

            if(BPoint3 == APoint1)
            {
                BPoint3 = APoint1;
            }
            if(BPoint3 == APoint2)
            {
                BPoint3 = APoint2;
            }
            if(BPoint3 == APoint3)
            {
                BPoint3 = APoint3;
            }

            // now creating the segments
            Segment2d ASegment1 = new Segment2d(APoint1, APoint2);
            Segment2d ASegment2 = new Segment2d(APoint2, APoint3);
            Segment2d ASegment3 = new Segment2d(APoint3, APoint1);

            List<Segment2d> ASegments = new List<Segment2d>() {ASegment1, ASegment2, ASegment3};

            Segment2d BSegment1 = new Segment2d(BPoint1, BPoint2);
            Segment2d BSegment2 = new Segment2d(BPoint2, BPoint3);
            Segment2d BSegment3 = new Segment2d(BPoint3, BPoint1);


            List<Segment2d> BSegments = new List<Segment2d> { BSegment1, BSegment2, BSegment3};

            // we're now calculating the intersections of the segments two by two
            bool intersectionFound = false;
            for(int i = 0; i < ASegments.Count; i++)
            {
                Segment2d ASegment = ASegments[i];
                for(int j = 0; j < BSegments.Count; j++)
                {
                    Segment2d BSegment = BSegments[j];

                    Vertex2d intersectionPoint = ASegment.GetIntersection(BSegment);

                    if(intersectionPoint != null)
                    {
                        // split segment ONLY if the point is not on the edge of the segment
                        if (intersectionPoint != ASegment.pointA && intersectionPoint != ASegment.pointB)
                        {
                            // removing connection from AB (because we will make it AO and OB), O = intersectionPoint
                            ASegment.pointB.RemoveConnectedPoint(ASegment.pointA);
                            ASegment.pointA.RemoveConnectedPoint(ASegment.pointB);
                            Vertex2d newASegmentEnd = ASegment.pointB;
                            // setting new extremity to be intersectionPoint
                            ASegment.pointB = intersectionPoint;
                            ASegment.pointB.AddConnectedPoint(ASegment.pointA);
                            ASegment.pointA.AddConnectedPoint(ASegment.pointB);

                            // creating segment OB and adding it to the list (as it can intersect other triangles)
                            Segment2d newASegment = new Segment2d(intersectionPoint, newASegmentEnd);
                            ASegments.Add(newASegment);
                            intersectionFound = true;
                        }

                        // doing the same for CD
                        if (intersectionPoint != BSegment.pointA && intersectionPoint != BSegment.pointB)
                        {
                            // removing connection from CD (because we will make it CO and OD), O = intersectionPoint
                            BSegment.pointB.RemoveConnectedPoint(BSegment.pointA);
                            BSegment.pointA.RemoveConnectedPoint(BSegment.pointB);
                            Vertex2d newBSegmentEnd = BSegment.pointB;
                            // setting new extremity to be intersectionPoint
                            BSegment.pointB = intersectionPoint;
                            BSegment.pointB.AddConnectedPoint(BSegment.pointA);
                            BSegment.pointA.AddConnectedPoint(BSegment.pointB);

                            // creating segment OB and adding it to the list (as it can intersect other triangles)
                            Segment2d newBSegment = new Segment2d(intersectionPoint, newBSegmentEnd);
                            BSegments.Add(newBSegment);
                            intersectionFound = true;
                        }
                    }
                }
            }

            if (!intersectionFound)
            {
                return new List<Triangle>() { triangleA, triangleB };
            }

            // adding the points to a list of unique points
            HashSet<Vertex2d> uniquePoints = new();

            foreach(Segment2d segment in ASegments)
            {
                uniquePoints.Add(segment.pointA);
                uniquePoints.Add(segment.pointB);
            }
            foreach (Segment2d segment in BSegments)
            {
                uniquePoints.Add(segment.pointA);
                uniquePoints.Add(segment.pointB);
            }

            var vertexes =  uniquePoints.ToList();
            List<List<Vertex2d>> polygonGroups = new(); 

            // now we will get the polygon groups that have at least one point on the outside
            // aka one point connected to only 2 other points
            for(int i = 0; i < vertexes.Count; i++)
            {
                var vertex = vertexes[i];
                if(vertex.ConnectedVertices.Count == 2)
                {
                    List<List<Vertex2d>> possiblePaths = new();
                    
                    Vertex2d nextPoint = null;
                    Vertex2d targetPoint = null;
                    foreach(Vertex2d connectedVertex in vertex.ConnectedVertices)
                    {
                        if (nextPoint == null) nextPoint = connectedVertex;
                        else targetPoint = connectedVertex;
                    }
                    possiblePaths.Add(new List<Vertex2d>() { vertex, nextPoint });

                    List<Vertex2d> correctPath = null;
                    while (true)
                    {
                        bool targetFound = false;
                        foreach (var possiblePath in possiblePaths)
                        {
                            var lastVertex = possiblePath.Last();
                            if (lastVertex.ConnectedVertices.Contains(targetPoint))
                            {
                                correctPath = possiblePath;
                                correctPath.Add(targetPoint);
                                targetFound = true;
                                break;
                            }
                        }
                        if (targetFound) break;

                        List<List<Vertex2d>> nextPossiblePaths = new();
                        foreach (var possiblePath in possiblePaths)
                        {
                            var lastVertex = possiblePath[^1];
                            var beforeLastVertex = possiblePath[^2];
                            foreach(Vertex2d nextVertex in lastVertex.ConnectedVertices)
                            {
                                // no going backwards
                                if (nextVertex == beforeLastVertex) continue;
                                // make sure we're not running in a loop (TODO: look into optimizing this maybe)
                                if (possiblePath.Contains(nextVertex)) continue;
                                List<Vertex2d> newPath = new List<Vertex2d>(possiblePath);
                                newPath.Add(nextVertex);
                                nextPossiblePaths.Add(newPath);
                            }
                        }
                        possiblePaths = nextPossiblePaths;
                    }

                    List<Vertex2d> polygonGroup = new();
                    List<Vertex2d> pointsToDelete = new();
                    // marking the points we will delete from the point list
                    bool deleteCheckReachedEnd = true;
                    for (int j = 0; j < correctPath.Count; j++)
                    {
                        Vertex2d checkedVertex = correctPath[j];
                        if(checkedVertex.ConnectedVertices.Count > 2)
                        {
                            deleteCheckReachedEnd = false;
                            break;
                        }
                        pointsToDelete.Add(checkedVertex);
                    }

                    if (!deleteCheckReachedEnd)
                    {
                        for(int j = correctPath.Count - 1; j >= 0; j--)
                        {
                            Vertex2d checkedVertex = correctPath[j];
                            if (checkedVertex.ConnectedVertices.Count > 2)
                            {
                                break;
                            }
                            pointsToDelete.Add(checkedVertex);
                        }
                    }

                    for (int j = 0; j < correctPath.Count; j++)
                    {
                        Vertex2d currentVertex = correctPath[j];
                        //if (currentVertex.ConnectedVertices.Count <= 2)
                        //{
                        //    pointsToDelete.Add(currentVertex);
                        //}
                        polygonGroup.Add(new Vertex2d(currentVertex.X, currentVertex.Y));
                    }

                    foreach (Vertex2d pointToDelete in pointsToDelete)
                    {
                        foreach(var connectedPoint in pointToDelete.ConnectedVertices)
                        {
                            connectedPoint.RemoveConnectedPoint(pointToDelete);
                        }
                        vertexes.Remove(pointToDelete);
                    }


                    polygonGroups.Add(polygonGroup);
                    i = -1;
                }
            }

            // sometimes, some polygons will land inside of other polygons. we need to test this out
            // and remove the redundant polygons

            for (int i = 0; i < polygonGroups.Count; i++)
            {
                List<Vertex2d> currentPolygon = polygonGroups[i];
                for(int j = 0; j < polygonGroups.Count; j++)
                {
                    if (i == j) continue;
                    List<Vertex2d> otherPolygon = polygonGroups[j];
                    // we're going to try to find out if j=other is containted in i=current
                    // if even one point of other is not in current, then we abandon the search
                    bool uncontainedFound = false;
                    for(int k = 0; k < otherPolygon.Count; k++)
                    {
                        Vertex2d otherVertex = otherPolygon[k];
                        bool shared = false;
                        // we're getting the max distance between the point and other points 
                        // to construct a segment of appropriate length
                        double maxDistance = 0;
                        foreach(Vertex2d currentVertex in currentPolygon)
                        {
                            if (currentVertex == otherVertex)
                            {
                                shared = true;
                                break;
                            }
                            double distance = ((Vect)currentVertex - (Vect)otherVertex).Norm;
                            if(distance > maxDistance)
                            {
                                maxDistance = distance;
                            }
                        }
                        // if the point is shared between the two polygons, it's not a suitable test candidate
                        // because it will pass both the outside and inside test
                        if (shared) continue;
                        int intMaxDistance = (int)Math.Ceiling(maxDistance);
                        // creating a segment with double the length
                        // this specific number is used because it's very unlikely that it will line up with any other segment
                        // and we want to avoid segments being parallel
                        Vertex2d segmentEnd = new Vertex2d((Vect)otherVertex + 2 * intMaxDistance * (new Vect(1, 0.618)));
                        Segment2d ray = new Segment2d(otherVertex, segmentEnd, false);
                        int intersectionCount = 0;
                        for(int vertexIndex = 0; vertexIndex < currentPolygon.Count; vertexIndex++)
                        {
                            Vertex2d currentVertex = currentPolygon[vertexIndex];
                            int nextVertexIndex = (vertexIndex + 1) % currentPolygon.Count;
                            Vertex2d nextVertex = currentPolygon[nextVertexIndex];

                            // creating a segment out of the original polygon's points
                            Segment2d segment = new Segment2d(currentVertex, nextVertex, false);
                            Vertex2d intersection = segment.GetIntersection(ray);
                            if(intersection != null)
                            {
                                intersectionCount++;
                            }
                        }
                        // if the number of intersections is even, the point must be on the outside
                        if(intersectionCount % 2 == 0)
                        {
                            uncontainedFound = true;
                            break;
                        }

                    }
                    // if no outside point was found, we remove this polygon because it's fully contained within the other
                    if (!uncontainedFound)
                    {
                        polygonGroups.RemoveAt(j);
                        j--;
                    }
                }
            }

            for (int i = 0; i < polygonGroups.Count; i++)
            {
                List<Vertex2d> polygon = polygonGroups[i];
                // should never happen, but just to be safe
                if(polygon.Count < 3)
                {
                    polygonGroups.RemoveAt(i);
                    i--;
                    continue;
                }
                // 3 triangles, easy case
                if(polygon.Count == 3)
                {
                    Triangle newTriangle = new Triangle(
                        polygon[0].to3DPoint(originPoint, u, v),
                        polygon[1].to3DPoint(originPoint, u, v),
                        polygon[2].to3DPoint(originPoint, u, v));
                    newTriangle.dimensionIndex = triangleA.dimensionIndex;
                    // no point adding the triangle if its flat or degenerate
                    if(newTriangle.IsDegenerate() || newTriangle.IsFlattened())
                    {

                    }
                    else
                    {
                        result.Add(newTriangle);
                    }
                    // we dealt with this group, so we remove it.
                    polygonGroups.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            // we now only have have the 4+ sided polygons, and we need to line them up clockwise
            // this will help a lot with checks for convexity

            for (int i = 0; i < polygonGroups.Count; i++)
            {
                List<Vertex2d> polygon = polygonGroups[i];
                int minPointIndex = 0;
                for(int j = 1; j < polygon.Count; j++)
                {
                    Vertex2d point = polygon[j];
                    if(point.X < polygon[minPointIndex].X)
                    {
                        minPointIndex = j;
                    }
                    if(point.X == polygon[minPointIndex].X)
                    {
                        if(point.Y < polygon[minPointIndex].Y)
                        {
                            minPointIndex = j;
                        }
                    }
                }

                double dot = 0;
                {
                    int previoussPointIndex = (minPointIndex - 1 + polygon.Count) % polygon.Count;
                    int nextPointIndex = (minPointIndex + 1) % polygon.Count;

                    Vertex2d A = polygon[previoussPointIndex];
                    Vertex2d B = polygon[minPointIndex];
                    Vertex2d C = polygon[nextPointIndex];

                    dot = (B.X - A.X) * (C.Y - A.Y) - (C.X - A.X) * (B.Y - A.Y);

                }

                if(dot < 0) // if oriented left-handed/clock-wise/"negatively"/"indirectly", we reverse the order
                {
                    polygonGroups[i].Reverse();
                }

                int concaveCount = 0;
                int startPointIndex = 0;



                for (int j = 0; j < polygon.Count; j++)
                {
                    int previousIndex = (j - 1 + polygon.Count) % polygon.Count;
                    int nextIndex = (j + 1) % polygon.Count;

                    Vertex2d A = polygon[previousIndex];
                    Vertex2d B = polygon[j];
                    Vertex2d C = polygon[nextIndex];

                    dot = (B.X - A.X) * (C.Y - A.Y) - (C.X - A.X) * (B.Y - A.Y);
                    if(dot < 0)
                    {
                        concaveCount++;
                        startPointIndex = j;
                        if (concaveCount > 1) break;
                    }
                }

                if(concaveCount == 0) // we're going to calculate the optimal point to split this polygon
                {
                    // we're calculating the point that will have the maximum smallest angle of a resulting triangle
                    double biggestSmallAngle = 0;
                    int biggestSmallAngleIndex = 0;
                    for (int ptIndex = 0; ptIndex < polygon.Count; ptIndex++)
                    {
                        Vect B = (Vect)polygon[ptIndex];

                        // high dot product = small angle. we're looking for the smallest resulting angle
                        double smallestAngle = 1000;//arbtirary starting value
                        for (int j = 0; j < polygon.Count; j++)
                        {
                            if (j == ptIndex) continue;
                            int CIndex = (j + 1) % polygon.Count;
                            if (CIndex == ptIndex) continue;
                            Vect A = (Vect)polygon[j];
                            Vect C = (Vect)polygon[CIndex];
                            // calculating all three angles
                            Vect AB = A - B;
                            Vect AC = A - C;
                            Vect BA = -AB;
                            Vect BC = B - C;
                            Vect CA = -AC;
                            Vect CB = -BC;

                            double dot1 = (AB * AC) / (AB.Norm * AC.Norm);
                            double dot2 = (BA * BC) / (BA.Norm * BC.Norm);
                            double dot3 = (CA * CB) / (CA.Norm * CB.Norm);

                            double maxDot = Math.Max(dot1, dot2);
                            maxDot = Math.Max(maxDot, dot3);

                            double angle = Math.Acos(maxDot);
                            if(angle < smallestAngle)
                            {
                                smallestAngle = angle;
                            }
                        }

                        if(smallestAngle > biggestSmallAngle)
                        {
                            biggestSmallAngle = smallestAngle;
                            biggestSmallAngleIndex = ptIndex;
                        }
                    }

                    startPointIndex = biggestSmallAngleIndex;
                }


                // if the polygon is convex or has only one concave point, we can use the fanning method to turn it
                // into a list of triangles, by drawing lines from one point (the concave point) to all other points
                if(concaveCount <= 1)
                {
                    Vertex2d B = polygon[startPointIndex];
                    for (int j=0; j < polygon.Count; j++)
                    {
                        if(j == startPointIndex) continue;
                        int CIndex = (j + 1) % polygon.Count;
                        if (CIndex == startPointIndex) continue;
                        Vertex2d A = polygon[j];
                        Vertex2d C = polygon[CIndex];
                        // adding the triangle into our result list
                        Triangle triangle = new Triangle(A.to3DPoint(originPoint, u, v), B.to3DPoint(originPoint, u, v), C.to3DPoint(originPoint, u, v));
                        triangle.dimensionIndex = triangleA.dimensionIndex;
                        if(!triangle.IsFlattened() && !triangle.IsDegenerate()) result.Add(triangle);
                    }

                    polygonGroups.RemoveAt(i);
                    i--;
                }
            }

            return result;
        }

    }
}
