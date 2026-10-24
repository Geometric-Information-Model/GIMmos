using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMcompress.Shrinkwrap
{
    public class Triangle2d
    {
        private Vertex2d vertex1;
        private Vertex2d vertex2;
        private Vertex2d vertex3;

        private List<Triangle2d> neighbour1 = null;
        private List<Triangle2d> neighbour2 = null;
        private List<Triangle2d> neighbour3 = null;

        #region properties

        public Vertex2d Vertex1
        {
            get
            {
                return vertex1;
            }
            set
            {
                vertex1 = value;
            }
        }
        public Vertex2d Vertex2
        {
            get
            {
                return vertex2;
            }
            set
            {
                vertex2 = value;
            }
        }
        
        public Vertex2d Vertex3
        {
            get
            {
                return vertex3;
            }
            set
            {
                vertex3 = value;
            }
        }


        public List<Triangle2d> Neighbour1
        {
            get
            {
                return neighbour1;
            }
            set
            {
                neighbour1 = value;
            }
        }
        public List<Triangle2d> Neighbour2
        {
            get
            {
                return neighbour2;
            }
            set
            {
                neighbour2 = value;
            }
        }
        public List<Triangle2d> Neighbour3
        {
            get
            {
                return neighbour3;
            }
            set
            {
                neighbour3 = value;
            }
        }

        #endregion

        public int CoincidesWith(Triangle2d other)
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

        public bool RemoveNeighbourIfExists(Triangle2d other)
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

        public bool ContainsVertex(Vertex2d vertex)
        {
            return (vertex1 == vertex || vertex2 == vertex || vertex3 == vertex);
        }

        // figuring out if a triangle is missing neighbours on at least one side
        public bool MissingNeighboursOnOneSide()
        {
            return neighbour1.Count == 0 || neighbour2.Count == 0 || neighbour3.Count == 0;
        }


        public bool IsNeighbour(Triangle2d other)
        {
            if (neighbour1 == null || neighbour2 == null || neighbour3 == null)
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
        public bool AddNeighbourIfPossible(Triangle2d other)
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

        public Triangle2d(Vertex2d vertex1, Vertex2d vertex2, Vertex2d vertex3)
        {
            Vertex1 = vertex1;
            Vertex2 = vertex2;
            Vertex3 = vertex3;
        }

        public override int GetHashCode()
        {
            return Vertex1.GetHashCode() + Vertex2.GetHashCode() + Vertex3.GetHashCode();
        }

        public bool ContainsPoint(Vertex2d point)
        {
            return (Vertex1 == point) && (Vertex2 == point) && (Vertex3 == point);
        }

        public override bool Equals(object obj)
        {
            if(obj is Triangle2d)
            {
                Triangle2d other = (Triangle2d)obj;

                if(other.Vertex1 == Vertex1)
                {
                    if(other.Vertex2 == Vertex2)
                    {
                        if(other.Vertex3 == Vertex3)
                        {
                            return true;
                        }
                        return false;
                    }

                    if(other.Vertex2 == Vertex3)
                    {
                        if (other.Vertex3 == Vertex2)
                        {
                            return true;
                        }
                        return false;
                    }
                }

                if (other.Vertex1 == Vertex2)
                {
                    if (other.Vertex2 == Vertex1)
                    {
                        if (other.Vertex3 == Vertex3)
                        {
                            return true;
                        }
                        return false;
                    }

                    if (other.Vertex2 == Vertex3)
                    {
                        if (other.Vertex3 == Vertex1)
                        {
                            return true;
                        }
                        return false;
                    }
                }

                if (other.Vertex1 == Vertex3)
                {
                    if (other.Vertex2 == Vertex2)
                    {
                        if (other.Vertex3 == Vertex1)
                        {
                            return true;
                        }
                        return false;
                    }

                    if (other.Vertex2 == Vertex1)
                    {
                        if (other.Vertex3 == Vertex2)
                        {
                            return true;
                        }
                        return false;
                    }
                }

            }

            return false;
        }

        public static void ConstructNeighborhouds(List<Triangle2d> triangles)
        {
            Dictionary<Vertex2d, List<Triangle2d>> vertexDictionary = new();
            foreach (Triangle2d triangle in triangles)
            {
                Vertex2d vertex1 = triangle.Vertex1;
                Vertex2d vertex2 = triangle.Vertex2;
                Vertex2d vertex3 = triangle.Vertex3;

                triangle.Neighbour1 = new();
                triangle.Neighbour2 = new();
                triangle.Neighbour3 = new();

                if (!vertexDictionary.ContainsKey(vertex1))
                {
                    vertexDictionary.Add(vertex1, new List<Triangle2d>());
                }
                if (!vertexDictionary.ContainsKey(vertex2))
                {
                    vertexDictionary.Add(vertex2, new List<Triangle2d>());
                }
                if (!vertexDictionary.ContainsKey(vertex3))
                {
                    vertexDictionary.Add(vertex3, new List<Triangle2d>());
                }

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

        public static string Triangles2dToString(List<Triangle2d> triangles, string prefix = "polygon(", string suffix = ")")
        {
            StringBuilder stringBuilder = new("[");
            for (int i = 0; i < triangles.Count; i++) 
            {
                var triangle = triangles[i];
                string triangleString = $"{prefix}{triangle.Vertex1}, {triangle.Vertex2}, {triangle.Vertex3}{suffix}";
                if(i < (triangles.Count - 1))
                {
                    triangleString += ", ";
                }
                stringBuilder.Append(triangleString);
            }
            stringBuilder.Append("]");
            return stringBuilder.ToString();
        }

        public static List<Triangle2d> From3dTrianglesWithVertexDictionary(List<Triangle> triangles3d, Dictionary<Vertex2d, Vect> point2Dto3D)
        {
            List<Triangle2d> triangles2d = new();

            foreach (Triangle roadTriangle in triangles3d)
            {
                Vect[] vertices = new Vect[] { roadTriangle.Vertex1, roadTriangle.Vertex2, roadTriangle.Vertex3 };
                Vertex2d[] vertices2d = new Vertex2d[3];
                int index = 0;
                foreach (var vertex in vertices)
                {
                    Vertex2d vertex2d = new Vertex2d(vertex[0], vertex[1]);
                    vertices2d[index] = vertex2d;
                    if (point2Dto3D.ContainsKey(vertex2d))
                    {
                        point2Dto3D.Add(vertex2d, vertex);
                    }
                    index++;
                }
                triangles2d.Add(new Triangle2d(vertices2d[0], vertices2d[1], vertices2d[2]));
            }
            return triangles2d;
        }

        public static void debugPrintTrianglesDesmos(List<Triangle2d> triangles, string fileNameKeywords = "")
        {

            Vect averagePoint = new Vect(0, 0, 0);

            HashSet<Vect> uniquePoints = new();

            StringBuilder desmosHtmlString = new("// START" + "\n");
            int graphId = 0;
            foreach (Triangle2d triangle in triangles)
            {
                graphId++;
                Vertex2d vertex1 = triangle.Vertex1;
                Vertex2d vertex2 = triangle.Vertex2;
                Vertex2d vertex3 = triangle.Vertex3;

                double x1 = vertex1.X, y1 = vertex1.Y;
                double x2 = vertex2.X, y2 = vertex2.Y;
                double x3 = vertex3.X, y3 = vertex3.Y;

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
