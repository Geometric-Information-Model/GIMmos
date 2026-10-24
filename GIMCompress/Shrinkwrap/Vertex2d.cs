using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


[assembly: InternalsVisibleTo("StartCityJson")]
namespace GIMcompress.Shrinkwrap
{
    public class Vertex2d
    {
        private Vect vertex;
        bool isOutside = false;
        HashSet<Vertex2d> connectedVertices = new();

        public Vertex2d(double x, double y, bool isOutside = false)
        {
            vertex = new Vect(x, y);
            this.isOutside = isOutside;
        }

        public Vertex2d(Vect vect)
        {
            vertex = vect;
        }

        public override bool Equals(object obj)
        {
            if(obj is Vertex2d)
            {
                return (this == (Vertex2d)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            // making sure hashcode is at least asymmetric
            int half1 = (vertex[0] + vertex[0] + vertex[0]).GetHashCode();
            int half2 = (vertex[1]).GetHashCode();
            return half1 + half2;
        }

        #region properties

        public double this[int index]
        {
            get { return vertex[index]; }
        }
    public bool IsOutside
        {
            get { return isOutside; }
            set { isOutside = value; }
        }

        public double X
        {
            get
            {
                return vertex[0];
            }
            //set
            //{
            //    vertex[0] = value;
            //}
        }
        public double Y
        {
            get
            {
                return vertex[1];
            }
            //set
            //{
            //    vertex[1] = value;
            //}
        }
        
        public double Arg
        {
            get
            {
                return Math.Atan2(Y, X);
            }

            //set
            //{
            //    X = R * Math.Cos(Arg);
            //    Y = R * Math.Sin(Arg);
            //}
        }

        public double R
        {
            get
            {
                return Math.Sqrt(X * X + Y * Y);
            }

            //set
            //{
            //    double oldR = Math.Sqrt(X * X + Y * Y);
            //    if(oldR == 0)
            //    {
            //        return;
            //    }

            //    X *= value/oldR;
            //    Y *= value/oldR;
            //}
        }

        public HashSet<Vertex2d> ConnectedVertices
        {
            get
            {
                return connectedVertices;
            }
            set
            {
                connectedVertices = value;
            }
        }

    

        #endregion

        public void Round()
        {
            vertex.Round();
        }

        public bool AddConnectedPoint(Vertex2d other)
        {
            if (other == this) return false;
            return connectedVertices.Add(other);
        }

        public bool RemoveConnectedPoint(Vertex2d other)
        {
            if (other == this) return false;
            return connectedVertices.Remove(other);
        }

        public bool IsConnectedTo(Vertex2d other)
        {
            return connectedVertices.Contains(other);
        }

        public Vect to3DPoint(Vect originPoint, Vect uVector, Vect vVector)
        {
            Vect pt = originPoint + X * uVector + Y * vVector;

            pt[0] = Math.Round(pt[0], 2);
            pt[1] = Math.Round(pt[1], 2);
            pt[2] = Math.Round(pt[2], 2);

            return pt;
        }

        public override string ToString()
        {
            return vertex.ToString();
        }

        public static List<Vertex2d> GetContourOf2dMesh(IEnumerable<Vertex2d> vertices)
        {
            List<Vertex2d> profile2D = new();
            Vect averageVertex = new(0, 0);
            foreach (Vertex2d vertex in vertices)
            {
                averageVertex += (Vect)vertex;
            }

            averageVertex /= vertices.Count();

            Vertex2d furthestVertex = vertices.First();
            double longestDistance = 0;
            foreach (Vertex2d vertex in vertices)
            {
                double distance = ((Vect)vertex - averageVertex).Norm;
                if (distance >= longestDistance)
                {
                    longestDistance = distance;
                    furthestVertex = vertex;
                }
            }
            Vect direction = ((Vect)furthestVertex - averageVertex).Direction;
            HashSet<Vertex2d> visitedVertices = new();
            Vertex2d currentVertex = furthestVertex;

            double pi = Math.PI;
            Vertex2d previousVertex = null;

            // safety break
            while (profile2D.Count <= (vertices.Count() * 2))
            {
                Vertex2d nextVertex = null;
                Vect nextDirection = null;
                double smallestAngle = 2 * pi;
                foreach (Vertex2d connectedVertex in currentVertex.ConnectedVertices)
                {
                    if (connectedVertex == previousVertex) continue;
                    Vect differenceDirection = ((Vect)connectedVertex - (Vect)currentVertex).Direction;
                    double dot = direction * differenceDirection;
                    double cross = direction[0] * differenceDirection[1] - direction[1] * differenceDirection[0];
                    double angle = (Math.Atan2(cross, dot) + 2 * pi) % (2 * pi);

                    if (angle < smallestAngle)
                    {
                        smallestAngle = angle;
                        nextVertex = connectedVertex;

                        nextDirection = -differenceDirection;
                    }

                }

                if (visitedVertices.Contains(currentVertex) && visitedVertices.Contains(nextVertex)) break;
                profile2D.Add(new(currentVertex.X, currentVertex.Y));
                visitedVertices.Add(currentVertex);
                if (nextVertex == null) break;
                previousVertex = currentVertex;
                currentVertex = nextVertex;
                direction = nextDirection;

            }
            return profile2D;
        }

        // static operator overrides


        public static Vertex2d operator+(Vertex2d A, Vertex2d B)
        {
            return new Vertex2d(A.vertex + B.vertex);
        }

        public static Vertex2d operator-(Vertex2d A, Vertex2d B)
        {
            return new Vertex2d(A.vertex - B.vertex);
        }

        public static Vertex2d operator*(double coeff, Vertex2d v)
        {
            return new(coeff * v.vertex);
        }

        public static Vertex2d operator /(Vertex2d v, double coeff)
        {
            return new(v.vertex/ coeff);
        }

        public static bool operator==(Vertex2d A, Vertex2d B)
        {
            if (A is null && B is null) return true;
            if (A is null || B is null) return false;
            double x = Math.Abs(A.X - B.X);
            double y = Math.Abs(A.Y - B.Y);
            return (x < Matrix.EQUALITY_MARGIN && y < Matrix.EQUALITY_MARGIN);
        }

        public static bool operator!=(Vertex2d A, Vertex2d B)
        {
            if (A is null && B is null) return false;
            if (A is null || B is null) return true;
            double x = Math.Abs(A.X - B.X);
            double y = Math.Abs(A.Y - B.Y);
            return (x >= Matrix.EQUALITY_MARGIN || y >= Matrix.EQUALITY_MARGIN);
        }


        public static explicit operator Vect(Vertex2d A)
        {
            return A.vertex;
        }

        public static explicit operator Vertex2d(Vect A)
        {
            return new Vertex2d(A);
        }
    }
}
