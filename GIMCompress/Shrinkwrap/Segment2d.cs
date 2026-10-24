using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMcompress.Shrinkwrap
{
    public class Segment2d
    {
        public Vertex2d pointA;
        public Vertex2d pointB;

        public Segment2d(Vertex2d pointA, Vertex2d pointB, bool connectPoints = true)
        {
            this.pointA = pointA;
            this.pointB = pointB;

            if (connectPoints)
            {
                this.pointA.AddConnectedPoint(pointB);
                this.pointB.AddConnectedPoint(pointA);
            }
        }

        public override string ToString()
        {
            return  "[" + pointA.ToString() + ", " + pointB.ToString() + "]";
        }

        public bool SharesPointWith(Segment2d other)
        {
            if (other == null) return false;
            return other.pointA == pointA || other.pointB == pointB || other.pointA == pointB || other.pointB == pointA;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if(obj is Segment2d)
            {
                Segment2d other = (Segment2d)obj;
                return (other.pointA == pointA && other.pointB == pointB) || (other.pointA == pointB && other.pointB == pointA);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return pointA.GetHashCode() + pointB.GetHashCode();
        }

        public bool Intersects(Segment2d other)
        {
            double aX = pointA.X;
            double aY = pointA.Y;
            double bX = pointB.X;
            double bY = pointB.Y;

            double oaX = other.pointA.X;
            double oaY = other.pointA.Y;
            double obX = other.pointB.X;
            double obY = other.pointB.Y;

            double minX = Math.Min(aX, bX);
            double otherMaxX = Math.Max(oaX, obX);

            if(minX > otherMaxX) return false;

            double maxX = Math.Max(aX, bX);
            double otherMinX = Math.Min(oaX, obX);

            if(maxX < otherMinX) return false;

            double minY = Math.Min(aY, bY);
            double otherMaxY = Math.Max(oaY, obY);

            if(minY > otherMaxY) return false;

            double maxY = Math.Max(aY, bY);
            double otherMinY = Math.Min(oaY, obY);

            if(maxY < otherMinY) return false;

            Vect thisA = (Vect)pointA;
            Vect thisB = (Vect)pointB;
            Vect otherA = (Vect)other.pointA;
            Vect otherB = (Vect)other.pointB;

            Vect AB = thisB - thisA;
            Vect AAprime = otherA - thisA;
            Vect ABPrime = otherB - thisB; 

            if(AB.IsCollinearTo(AAprime) && AB.IsCollinearTo(ABPrime))
            {
                if (other.pointA.X < pointB.X && other.pointA.X > pointA.X &&
                    other.pointA.Y < pointB.Y && other.pointA.Y > pointA.Y)
                    return true;

                if (other.pointB.X < pointB.X && other.pointB.X > pointA.X &&
                    other.pointB.Y < pointB.Y && other.pointB.Y > pointA.Y)
                    return true;

                if (other.pointA.X > pointB.X && other.pointA.X < pointA.X &&
                    other.pointA.Y > pointB.Y && other.pointA.Y < pointA.Y)
                    return true;

                if (other.pointB.X > pointB.X && other.pointB.X < pointA.X &&
                    other.pointB.Y > pointB.Y && other.pointB.Y < pointA.Y)
                    return true;

                return false;
            }

            Vertex2d A = pointA;
            Vertex2d B = pointB;
            Vertex2d C = other.pointA;
            Vertex2d D = other.pointB;

            // optimisation, to reduce the number of operations necessary

            double dot1 = (B.X - A.X) * (C.Y - A.Y) - (C.X - A.X) * (B.Y - A.Y);
            double dot2 = (B.X - A.X) * (D.Y - A.Y) - (D.X - A.X) * (B.Y - A.Y);

            double dotASame = dot1 * dot2;

            double dot3 = (D.X - C.X) * (A.Y - C.Y) - (A.X - C.X) * (D.Y - C.Y);
            double dot4 = (D.X - C.X) * (B.Y - C.Y) - (B.X - C.X) * (D.Y - C.Y);

            double dotBSame = dot3 * dot4;

            return (dotASame <= 0 && dotBSame <=0);

        }

        public Vertex2d GetIntersection(Segment2d other)
        {
            Vertex2d intersectionPoint = null;

            // segments are equal
            if(((pointA == other.pointA) && (pointB == other.pointB)) || ((pointA == other.pointB && pointB == other.pointA))){
                return null;
            }

            double x1 = pointA.X;
            double y1 = pointA.Y;
            double x2 = pointB.X;
            double y2 = pointB.Y;

            double x3 = other.pointA.X;
            double y3 = other.pointA.Y;
            double x4 = other.pointB.X;
            double y4 = other.pointB.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            denom = ValueRounder.roundDouble(denom, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            if (denom == 0) // segments are parallel, we need to check if they are aligned and share a section
                // if they do share a section, return the first point in this section
            {
                Vect direction1 = ((Vect)pointB - (Vect)pointA);
                Vect direction2 = ((Vect)other.pointB - (Vect)pointA);

                if (!direction1.IsCollinearTo(direction2)) // points are not aligned because AB is not parallel to AB'
                {
                    return null;
                }

                direction1 = direction1.Direction;

                x1 = 0;
                x2 = ((Vect)pointB - (Vect)pointA) * direction1;
                x3 = ((Vect)other.pointA - (Vect)pointA) * direction1;
                x4 = ((Vect)other.pointB - (Vect)pointA) * direction1;

                // point A' of the second segment is between AB of the first segment
                if((x3 > x1 && x3 < x2) || (x3 < x1 && x3 > x2)) 
                {
                    direction1.IsCollinearTo(direction2);
                    return other.pointA;
                }
                // point B' of the second segment is between AB of the first segment
                if((x4 > x1 && x4 < x2) || (x4 < x1 && x4 > x2))
                {
                    direction1.IsCollinearTo(direction2);
                    return other.pointB;
                }
                // point A of the first segment is between A'B' of the second segment
                if((x1 > x3 && x1 < x4) || (x1 < x3 && x1 > x4))
                {
                    direction1.IsCollinearTo(direction2);
                    return pointA;
                }
                // point B of the first segment is between A'B' of the second segment
                if((x2 > x3 && x2 < x4) || (x2 < x3 && x2 > x4))
                {
                    direction1.IsCollinearTo(direction2);
                    return pointB;
                }
                // none of the above is true, both segments are parallel and aligned but don't intersect
                return null;
            }
            double tTop = (x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4);
            double uTop = (x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2);

            double t = tTop / denom;
            double u = uTop / denom;

            t = ValueRounder.roundDouble(t, Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            u = ValueRounder.roundDouble(u, Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

            bool tCond = (t < 1) && (t > 0);
            bool uCond = (u < 1) && (u > 0);
            bool tIsEdge = (t == 0) && (t == 1);
            bool uIsEdge = (u == 0) && (u == 1);

            // segments intersect at their ends, doesn't count
            if(tIsEdge && uIsEdge)
            {
                return null;
            }
            // on the edge of first segment, we return the appropriate edge point
            if (tIsEdge)
            {
                if(t == 0)
                {
                    return pointA;
                }
                else
                {
                    return pointB;
                }
            }
            // the on the edge of the second segment, we return the appropriate edge point
            if (uIsEdge)
            {
                if(u == 0)
                {
                    return other.pointA;
                }
                else
                {
                    return other.pointB;
                }
            }



            // segments intersect in the middle, or only one of them on the edge

            if (tCond && uCond)
            {
                double intersectionX = x1 + t * (x2 - x1);
                intersectionX = RoundIntersectionCoordinate(intersectionX);
                double intersectionY = y1 + t * (y2 - y1);
                intersectionY = RoundIntersectionCoordinate(intersectionY);

                if(intersectionX == pointA.X && intersectionY == pointA.Y)
                {
                    return pointA;
                }

                if(intersectionX == pointB.X && intersectionY == pointB.Y)
                {
                    return pointB;
                }

                if(intersectionX == other.pointA.X && intersectionY == other.pointA.Y)
                {
                    return other.pointA;
                }

                if(intersectionX == other.pointB.X && intersectionY == other.pointB.Y)
                {
                    return other.pointB;
                }

                intersectionPoint = new(intersectionX, intersectionY);
            }

            return intersectionPoint;
        }

        public static double RoundIntersectionCoordinate(double intersectionCoordinate)
        {
            double roundFirstDigit = Math.Round(intersectionCoordinate, 1);
            double roundSecondDigit = Math.Round(intersectionCoordinate, 2);

            if(roundFirstDigit == roundSecondDigit)
            {
                return roundFirstDigit;
            }

            double roundThirdDigit = Math.Round(intersectionCoordinate, 3);

            if(roundSecondDigit == roundThirdDigit)
            {
                return roundSecondDigit;
            }

            return intersectionCoordinate;
        }

        public static string debugPrintSegmentsDesmos(List<Segment2d> segments, string prefixKeyword = "", string suffixKeyWord = "")
        {
            StringBuilder finalString = new("// START" + "\n");
            int graphId = 0;
            foreach (Segment2d segment in segments)
            {
                graphId++;
                Vertex2d vertex1 = segment.pointA;
                Vertex2d vertex2 = segment.pointB;

                double x1 = vertex1.X, y1 = vertex1.Y;
                double x2 = vertex2.X, y2 = vertex2.Y;

                string polygonString =
                    string.Format(" id: 'graph{0}', latex: '({1:0.#}, {2:0.#}), ({3:0.#}, {4:0.#})', lines: true, points: false",
                    graphId, x1, y1, x2, y2);
                string codeLine = "calculator.setExpression({" + polygonString + "});";

                finalString.AppendLine(codeLine);
            }


            finalString.AppendLine("//END");

            string HtmlFileContent = "<html> <head> <title> Desmos Applet Template </title> </head>"
                + " <script src = \"https://www.desmos.com/api/v1.7/calculator.js?apiKey=dcb31709b452b1cf9dc26972add0fda6\" ></script > "
                + "<body> <div id = \"calculator\" style = \"width: 100%; height: 100%\"> </div>  </body> <script> "

                + "	var elt = document.getElementById('calculator'); var calculator = Desmos.GraphingCalculator(elt); \n"

                + finalString.ToString()

                + "</script> </html> "

                ;

            
            DateTime dateNow = DateTime.Now;
            string fileName = prefixKeyword + String.Format("Segment Visualisation " + dateNow.ToString("d-M") + " at " + dateNow.ToString("h-m") + " total {1} segments", dateNow, segments.Count);
            fileName += suffixKeyWord + ".Html";
            string filePath = System.IO.Path.GetTempPath() + "GIM\\segments\\";
            if (!System.IO.Directory.Exists(filePath))
                System.IO.Directory.CreateDirectory(filePath);

            System.IO.File.WriteAllTextAsync(filePath + fileName, HtmlFileContent);

            return finalString.ToString();
        }

    }
}
