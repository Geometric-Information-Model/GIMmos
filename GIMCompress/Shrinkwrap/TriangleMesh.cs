using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace GIMcompress.Shrinkwrap
{
    // used to group different Triangle Meshes. Static variables were avoided in order to make this parallelizable
    [Serializable]
    public class TriangleMesh
    {
        private List<Triangle> triangles = new();
        int counter = 0;

        #region properties

        public List<Triangle> Triangles
        {
            get { return triangles; }
            set 
            { 
                triangles = value;
                foreach(Triangle triangle in triangles)
                {
                    triangle.Mesh = this;
                }
            }
        }

        public int Counter
        {
            get { return counter; }
            set { counter = value; }
        }

        #endregion

        public TriangleMesh()
        {

        }

        public void RemoveTriangle(Triangle triangle)
        {
            triangles.Remove(triangle);
            List<Triangle> n1 = triangle.Neighbour1;
            List<Triangle> n2 = triangle.Neighbour2;
            List<Triangle> n3 = triangle.Neighbour3;

            foreach(Triangle n in n1)
            {
                n.RemoveNeighbourIfExists(triangle);
            }
            foreach(Triangle n in n2)
            {
                n.RemoveNeighbourIfExists(triangle);
            }
            foreach (Triangle n in n3)
            {
                n.RemoveNeighbourIfExists(triangle);
            }
        }

        /// <summary>
        /// Transforms a TriangleMesh into a GimElement representation for polygons. It extracts unique vertices 
        /// from the mesh, assigns indices to them, and populates the GimElement with these vertices and their indices.
        /// </summary>
        /// <param name="polygonElement">The GimElement to be populated with mesh data.</param>
        /// <remarks>
        /// Process:
        /// 1. Extracts unique vertices from the mesh's triangles.
        /// 2. Assigns a sequential index to each unique vertex and stores them in a dictionary.
        /// 3. Initializes arrays for vertex coordinates (elementDoubles) and vertex indices (elementInts).
        /// 4. Populates these arrays with the mesh's vertex data.
        /// Assumes vertices are represented as Vect objects and that the mesh's triangles are stored in the 'triangles' collection.
        /// </remarks>
        public void ApplyToGimDataPolygon(GimElement polygonElement)
        {
            Dictionary<Vect, int> pointIndexDict = new();
            int pointCount = 0;
            int i = 0;

            // counting how many unique points there are
            foreach (Triangle triangle in triangles)
            {
                Vect vertex1 = triangle.Vertex1;
                Vect vertex2 = triangle.Vertex2;
                Vect vertex3 = triangle.Vertex3;

                Vect[] vertices = new Vect[] { vertex1, vertex2, vertex3 };

                foreach (Vect vertex in vertices)
                {
                    if (pointIndexDict.ContainsKey(vertex))
                    {
                        continue;
                    }

                    int vertexIndex = pointCount;
                    pointIndexDict.Add(vertex, vertexIndex);
                    pointCount++;
                }
            }

            // every point has 3 coordinates
            double[] elementDoubles = new double[pointCount * 3];
            // every triangle has 3 points so 3 point indices
            int[] elementInts = new int[triangles.Count * 3];

            // filling in the points in the corresponding arrays.
            foreach (Triangle triangle in triangles)
            {
                Vect vertex1 = triangle.Vertex1;
                Vect vertex2 = triangle.Vertex2;
                Vect vertex3 = triangle.Vertex3;

                Vect[] vertices = new Vect[] { vertex1, vertex2, vertex3 };

                foreach (Vect vertex in vertices)
                {

                    int vertexIndex = pointIndexDict[vertex];
                    
                    //else
                    //{
                    //    vertexIndex = pointCount;
                    //    elementDoublesList.Add(vertex[0]);
                    //    elementDoublesList.Add(vertex[1]);
                    //    elementDoublesList.Add(vertex[2]);
                    //    pointIndexDict.Add(vertex, vertexIndex);
                    //    pointCount++;
                    //}

                    elementInts[i] = vertexIndex;
                    i++;
                }
            }

            foreach(var vertexTuple in pointIndexDict)
            {
                int index = vertexTuple.Value;
                Vect uniqueVertex = vertexTuple.Key;

                elementDoubles[index * 3] = uniqueVertex[0];
                elementDoubles[index * 3 + 1] = uniqueVertex[1];
                elementDoubles[index * 3 + 2] = uniqueVertex[2];
            }

            polygonElement.ElementDoubles = elementDoubles;
            polygonElement.ElementInts = elementInts;
        }

        public static TriangleMesh ConstructMesh(List<Triangle> triangleList, bool is2d = false)
        {
            TriangleMesh mesh = new TriangleMesh();
            Dictionary<Vect, List<Triangle>> triangleVertex = new();

            HashSet<Triangle> addedTriangles = new();

            int count = 0;

            // constructing new triangles out of the old ones

            foreach(Triangle oldTriangle in triangleList)
            {
                Vect vertex1 = oldTriangle.Vertex1;
                Vect vertex2 = oldTriangle.Vertex2;
                Vect vertex3 = oldTriangle.Vertex3;

                if ((vertex1 == vertex2) || (vertex2 == vertex3) || (vertex3 == vertex1))
                {
                    continue;
                }

                Triangle triangle = new Triangle(vertex1, vertex2, vertex3);
                triangle.is2D = is2d;
                if (is2d)
                {
                    triangle.Neighbour1 = new();
                    triangle.Neighbour2 = new();
                    triangle.Neighbour3 = new();
                }
                if (addedTriangles.Contains(triangle))
                    continue;

                triangle.Id = mesh.Counter;
                mesh.Triangles.Add(triangle);
                mesh.Counter++;
                triangle.Mesh = mesh; 
                addedTriangles.Add(triangle);

            }

            

            //// sorting triangles

            //double[] minCoordinate = new double[3] { int.MaxValue, int.MaxValue, int.MaxValue };
            //double[] maxCoordinate = new double[3] { int.MinValue, int.MinValue, int.MinValue };

            //foreach (Triangle triangle in mesh.Triangles)
            //{
            //    Vect[] verticies = new Vect[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 };

            //    foreach (Vect vertex in verticies)
            //    {
            //        for (int i = 0; i < 3; i++)
            //        {
            //            if (vertex[i] < minCoordinate[i])
            //            {
            //                minCoordinate[i] = vertex[i];
            //            }

            //            if (vertex[i] > maxCoordinate[i])
            //            {
            //                maxCoordinate[i] = vertex[i];
            //            }
            //        }
            //    }
            //}

            //double[] differences = new double[] {
            //    Math.Abs(maxCoordinate[0] - minCoordinate[0]),
            //    Math.Abs(maxCoordinate[1] - minCoordinate[1]),
            //    Math.Abs(maxCoordinate[2] - minCoordinate[2])
            //};

            //int dimensionIndex = 0;
            //double maxDifference = 0;

            //for (int i = 0; i < differences.Length; i++)
            //{
            //    if (differences[i] > maxDifference)
            //    {
            //        maxDifference = differences[i];
            //        dimensionIndex = i;
            //    }
            //}

            //foreach()
            //Triangle.dimensionIndex = dimensionIndex;

            //mesh.Triangles.Sort(Triangle.CompareTriangles);

            // we will construct the neighbours here

            foreach (Triangle triangle in addedTriangles)
            {
                count++;
                Vect vertex1 = triangle.Vertex1;
                Vect vertex2 = triangle.Vertex2;
                Vect vertex3 = triangle.Vertex3;

                //if ((vertex1 == vertex2) || (vertex2 == vertex3) || (vertex3 == vertex1))
                //{
                //    continue;
                //}


                //Triangle triangle = new Triangle(vertex1, vertex2, vertex3);
                //triangle.is2D = is2d;
                //if (is2d)
                //{
                //    triangle.Neighbour1 = new();
                //    triangle.Neighbour2 = new();
                //    triangle.Neighbour3 = new();
                //}

                //if (addedTriangles.Contains(triangle))
                //    continue;

                //triangle.Id = mesh.Counter;
                //mesh.Triangles.Add(triangle);
                //mesh.Counter++;
                //triangle.Mesh = mesh;

                //addedTriangles.Add(triangle);

                if (!triangleVertex.ContainsKey(vertex1))
                {
                    triangleVertex.Add(vertex1, new List<Triangle>());
                }
                if (!triangleVertex.ContainsKey(vertex2))
                {
                    triangleVertex.Add(vertex2, new List<Triangle>());
                }
                if (!triangleVertex.ContainsKey(vertex3))
                {
                    triangleVertex.Add(vertex3, new List<Triangle>());
                }

                foreach (Triangle other in triangleVertex[vertex1])
                {
                    bool success = other.AddNeighbourIfPossible(triangle);
                    if (success)
                    {
                        triangle.AddNeighbourIfPossible(other);
                    }
                }
                foreach (Triangle other in triangleVertex[vertex2])
                {
                    bool success = other.AddNeighbourIfPossible(triangle);
                    if (success)
                    {
                        triangle.AddNeighbourIfPossible(other);
                    }
                }
                foreach (Triangle other in triangleVertex[vertex3])
                {
                    bool success = other.AddNeighbourIfPossible(triangle);
                    if (success)
                    {
                        triangle.AddNeighbourIfPossible(other);
                    }
                }

                triangleVertex[vertex1].Add(triangle);
                triangleVertex[vertex2].Add(triangle);
                triangleVertex[vertex3].Add(triangle);

            }

            return mesh;
        }
        public string ToDesmosString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var triangle in triangles)
            {
                sb.Append(triangle.ToDesmosString());
                if (triangle != triangles.Last())
                    sb.Append(", ");  
            }
            return sb.ToString();
        }
    }
}
