using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GIMcompress.Shrinkwrap
{
    public class GimPolygon
    {
        private List<List<Vect>> innerBoundaries = new();
        private List<Vect> outerBoundary = new();

        private Vect calculatedCenter = null;
        private Vect calculatedNormal = null;

        #region properties

        public List<List<Vect>> InnerBoundaries
        {
            get { return innerBoundaries; }
            set { innerBoundaries = value; }
        }

        public List<Vect> OuterBoundary
        {
            get { return outerBoundary;}
            set { outerBoundary = value; }
        }

        public Vect Center
        {
            get
            {
                if(calculatedCenter == null)
                {
                    calculatedCenter = GetCenter();
                }

                return calculatedCenter;
            }
        }

        public Vect Normal
        {
            get
            {
                if(calculatedNormal == null)
                {
                    calculatedNormal = GetNormal();
                }
                return calculatedNormal;
            }
        }

        #endregion

        public GimPolygon(List<Vect> outerBoundary, List<List<Vect>> innerBoundaries)
        {
            this.outerBoundary = outerBoundary;
            this.innerBoundaries = innerBoundaries;
        }

        // first boundary is outerBoundary, the rest are innerBoundaries
        public GimPolygon(List<List<Vect>> allBoundaries)
        {
            foreach(Vect point in allBoundaries[0])
            {
                outerBoundary.Add(point);
            }

            for(int i=1; i<allBoundaries.Count; i++)
            {
                List<Vect> innerBoundary = new List<Vect>();
                foreach(Vect point in allBoundaries[i])
                {
                    innerBoundary.Add(point);
                }
                innerBoundaries.Add(innerBoundary);
            }

        }

        public Tuple<Vect,Vect,Vect> GetCenterAndMoments()
        {
            Tuple<Vect, Vect, Vect> result = null;

            Vect center = Center;

            int pointCount = outerBoundary.Count;

            foreach(var innerBoundary in innerBoundaries)
            {
                pointCount += innerBoundary.Count;
            }


            Vect moment = new(0, 0, 0);
            Vect momentTwo = new(0, 0, 0);

            foreach (Vect point in outerBoundary)
            {
                Vect vect = point - center;
                moment += vect.Norm * vect;
                momentTwo += (vect.Norm * vect.Norm) * vect;
            }

            foreach (var innerBoundary in innerBoundaries)
            {
                foreach (Vect point in innerBoundary)
                {
                    Vect vect = point - center;
                    moment += vect.Norm * vect;
                    momentTwo += (vect.Norm * vect.Norm) * vect;
                }
            }

            if(moment.Norm != 0)
            {
                moment = moment.Direction;
            }
            if(momentTwo.Norm != 0)
            {
                momentTwo = momentTwo.Direction;
            }

            result = new(center, moment, momentTwo);
            return result;
        }

        private Vect GetCenter()
        {
            Vect center = new(0, 0, 0);

            foreach (Vect point in outerBoundary)
            {
                center += point;
            }
            int pointCount = outerBoundary.Count;
            foreach (var innerBoundary in innerBoundaries)
            {
                foreach (Vect point in innerBoundary)
                {
                    center += point;
                }
                pointCount += innerBoundary.Count;
            }

            center /= pointCount;


            return center;
        }
        private Vect GetNormal()
        {
            Vect normal = new(0, 0, 0);
            Vect pointA = outerBoundary[0];
            Vect pointB = outerBoundary[1];

            Vect vectA = (pointB - pointA);

            for(int i = 2; i < outerBoundary.Count; i++)
            {
                Vect vectB = outerBoundary[i] - pointA;

                normal = vectA ^ vectB;
                if (normal.Norm != 0) break;
            }

            return normal;
        }

        // returns the normal vect that points AWAY from this point
        public Vect GetOrientedNormal(Vect awayFromPoint)
        {
            Vect normal = GetNormal();

            Vect orientation = Center - awayFromPoint;

            if(orientation * normal >= 0)
            {
                return normal;
            }

            return -normal;
        }

        // returns the longest and shortest length in a gimpolygon

        public Tuple<double, double> GetLongestAndShortestLengths()
        {
            Tuple<double, double> result = null;

            double max = -1, min = -1;

            for(int i=0; i < outerBoundary.Count; i++)
            {
                Vect currentPoint = outerBoundary[i];
                Vect nextPoint = outerBoundary[ (i + 1) % outerBoundary.Count];

                double distance = (nextPoint - currentPoint).Norm;
                
                if(max == -1)
                {
                    max = distance;
                }
                else if (distance > max)
                {
                    max = distance;
                }
                if(min == -1)
                {
                    min = distance;
                } else if(distance < min) 
                {
                    min = distance;
                }

            }

            foreach(var innerBoundary in innerBoundaries)
            {
                for (int i = 0; i < innerBoundary.Count; i++)
                {
                    Vect currentPoint = innerBoundary[i];
                    Vect nextPoint = innerBoundary[(i + 1) % innerBoundary.Count];

                    double distance = (nextPoint - currentPoint).Norm;

                    if (max == -1)
                    {
                        max = distance;
                    }
                    else if (distance > max)
                    {
                        max = distance;
                    }
                    if (min == -1)
                    {
                        min = distance;
                    }
                    else if (distance < min)
                    {
                        min = distance;
                    }

                }
            }

            result = new(max, min);
            return result;
        }

        public void SimplifyBoundaries()
        {
            outerBoundary = SimplifyBoundary(outerBoundary);

            for(int i=0; i<innerBoundaries.Count; i++)
            {
                innerBoundaries[i] = SimplifyBoundary(innerBoundaries[i]);
            }
        }

        public bool IsDegenerate()
        {
            bool result = true;

            Vect startPoint = outerBoundary[0];
            Vect direction = new(0,0,0);
            for (int i = 1; i < outerBoundary.Count; i++)
            {
                Vect point = outerBoundary[i];
                direction = point - startPoint;
                if(direction.Norm > Vect.EQUALITY_MARGIN * 1000) { // not null
                    direction = direction.Direction;
                    break;
                }
            }

            if(direction.Norm <= Vect.EQUALITY_MARGIN * 1000) // all points are basically the same
            {
                return true;
            }

            for(int i = 1; i < outerBoundary.Count; i++)
            {
                Vect point = outerBoundary[i];
                Vect direction2 = point - startPoint;
                if (direction2.Norm > Vect.EQUALITY_MARGIN * 1000)
                { // not null
                    direction2 = direction2.Direction;
                    if (!direction2.IsCollinearTo(direction)) // checking if aligned with starting 2 points
                    {
                        result = false;
                        break;
                    }
                }

            }

            return result;
        }


        /// <summary>
        /// Given a polygon represented as a List of points (Vect), this function simplifies said polygon by removing redundant points
        /// and points that are on 180 degree angles.
        /// </summary>
        /// <param name="boundary">The List of points of the edges of the polygon</param>
        /// <returns></returns>
        public static List<Vect> SimplifyBoundary(List<Vect> boundary)
        {
            if (boundary.Count <= 3) return boundary;
            List<Vect> newBoundary = new();

            Vect center = new(0,0,0);
            foreach(Vect point in boundary)
            {
                center += point;
            }

            center /= boundary.Count;

            int furthestIndex = 0;
            double maxDistance = 0;

            for (int i = 0; i < boundary.Count; i++)
            {
                double distance = (boundary[i] - center).Norm;
                if(distance > maxDistance)
                {
                    maxDistance = distance;
                    furthestIndex = i;
                }
            }

            int startIndex = furthestIndex;
            int currentIndex = startIndex;
            int pointCount = 0;
            int previousIndex = (currentIndex - 1 + boundary.Count) % boundary.Count;

            do
            {
                int nextIndex = (currentIndex + 1) % boundary.Count;


                Vect current = boundary[currentIndex];
                Vect next = boundary[nextIndex];
                Vect previous = boundary[previousIndex];

                Vect v1 = (next - current).Direction;
                Vect v2 = (previous - current).Direction;

                double reverseScalar = 1 + v1 * v2;

                if(reverseScalar >= Vect.EQUALITY_MARGIN * 100)
                {
                    newBoundary.Add(current);
                    previousIndex = currentIndex;
                }

                currentIndex = nextIndex;


            } while (currentIndex != startIndex);


            return newBoundary;
        }

        // return gimpolygon that is identical in shape but oriented by applying orientation * (point - center)
        public GimPolygon Orient(Vect center, Matrix orientation)
        {
            List<Vect> newOuterBoundary = new();
            foreach(Vect point in outerBoundary)
            {
                newOuterBoundary.Add(
                    orientation * (point - center)
                    );
            }

            List<List<Vect>> newBoundaries = new() { newOuterBoundary };
            
            foreach(var innerBoundary in innerBoundaries)
            {
                List<Vect> newInnerBoundary = new();
                foreach(Vect point in innerBoundary)
                {
                    newInnerBoundary.Add(
                        orientation * (point - center)
                        );
                }
                newBoundaries.Add(newInnerBoundary);

            }

            return new GimPolygon(newBoundaries);
        }

        // convert to gimDataPartial to be included in a gimData
        public GimDataPartial ToGimDataPartial(int parent = 0)
        {
            GimDataPartial gimData = new(parent);

            GimElement planeElement = new() { ElementType = GimElementType.F_plane};

            // first point is the plane origin
            Vect planeBasePoint = outerBoundary[0];

            GimLocal planeLocal = new() { parent = (uint)parent,
                Px = planeBasePoint[0], Py = planeBasePoint[1], pz = planeBasePoint[2] };

            // first direction is the first line's direction
            Vect planeDirectionA = outerBoundary[1] - outerBoundary[0];
            int index = 2;

            Vect nullVect = new Vect(0, 0, 0);
            // we go through the points until we find a non-null first vector
            while(planeDirectionA == nullVect && index < outerBoundary.Count)
            {
                planeDirectionA = outerBoundary[index] - outerBoundary[0];
                index++;
            }

            if(planeDirectionA == nullVect)
            {
                // flat gimpoly
                throw GimUtilities.GimLogger.Error("GimPoly was entirely flat, basically a line!");
            }


            Vect planeNormal = new Vect(0, 0, 0);
            if (index == outerBoundary.Count) index--;
            Vect planeDirectionB = null;

            // we go through the points until we find a non-null second vector that doesn't align with the first

            while (planeNormal == nullVect && index < outerBoundary.Count)
            {
                planeDirectionB = outerBoundary[index] - outerBoundary[0];
                planeNormal = planeDirectionA ^ planeDirectionB;
                index++;
            }

            if(planeNormal == null)
            {
                throw GimUtilities.GimLogger.Error("GimPoly was entirely flat, basically a line!");
            }

            planeNormal = planeNormal.Direction;

            Vect vect1 = new Vect(1, 0, 0);
            vect1 = vect1 - (vect1 * planeNormal) * planeNormal;

            if(vect1.Norm == 0)
            {
                vect1 = new Vect(0, 1, 0);
                vect1 = vect1 - (vect1 * planeNormal) * planeNormal;
            }

            vect1 = vect1.Direction;
            Vect vect2 = (planeNormal ^ vect1).Direction;

            Matrix planeMatrix = new(vect1, vect2, vect1 ^ vect2);
            planeMatrix.ApplyToLocal(planeLocal);

            planeElement.Locals.Add(planeLocal);
            int planeIndex = gimData.Elements.Count;
            gimData.Elements.Add(planeElement);

            GimElement outerWireElement = new() { ElementType = GimElementType.wire };
            outerWireElement.Locals.Add(new() { parent = (uint)planeIndex });
            int outerWireIndex = gimData.Elements.Count;
            planeElement.Elements.Add((uint)outerWireIndex);
            gimData.Elements.Add(outerWireElement);

            for(int i=0; i< outerBoundary.Count; i++)
            {
                Vect pointA = outerBoundary[i];
                Vect pointB = outerBoundary[(i + 1) % outerBoundary.Count];

                Vect direction = (pointB - pointA).Direction;
                double length = (pointB - pointA).Norm;

                GimElement lineElement = new GimElement()
                {
                    ElementDoubles = new double[] { 0, length },
                    ElementType = GimElementType.C_line,
                };

                GimLocal lineLocal = new()
                {
                    px = pointA[0],
                    py = pointA[1],
                    pz = pointA[2],
                    parent = (uint)outerWireIndex,

                };

                direction.ApplyToLocal(lineLocal);
                lineElement.Locals.Add(lineLocal);

                int lineElementIndex = gimData.Elements.Count;
                gimData.Elements.Add(lineElement);
                outerWireElement.Elements.Add((uint)lineElementIndex);
                
            }

            foreach(var innerBoundary in innerBoundaries)
            {
                GimElement innerWireElement = new() { ElementType = GimElementType.wire };
                innerWireElement.Locals.Add(new() { parent = (uint)planeIndex });
                int innerWireIndex = gimData.Elements.Count;
                planeElement.Elements.Add((uint)innerWireIndex);
                gimData.Elements.Add(innerWireElement);


                for (int i = 0; i < innerBoundary.Count; i++)
                {
                    Vect pointA = innerBoundary[i];
                    Vect pointB = innerBoundary[(i + 1) % innerBoundary.Count];

                    Vect direction = (pointB - pointA).Direction;
                    double length = (pointB - pointA).Norm;

                    GimElement lineElement = new GimElement()
                    {
                        ElementDoubles = new double[] { 0, length },
                        ElementType = GimElementType.C_line,
                    };

                    GimLocal lineLocal = new()
                    {
                        px = pointA[0],
                        py = pointA[1],
                        pz = pointA[2],
                        parent = (uint)innerWireIndex,

                    };

                    direction.ApplyToLocal(lineLocal);
                    lineElement.Locals.Add(lineLocal);

                    int lineElementIndex = gimData.Elements.Count;
                    gimData.Elements.Add(lineElement);
                    innerWireElement.Elements.Add((uint)lineElementIndex);

                }

            }


            return gimData;
        }

        /// <summary>
        /// calculates the area of a polygon perpendicular or normal to the Z-direction
        /// </summary>
        /// <param name="boundary">The boundary of the polygon</param>
        /// <returns></returns>
        public static double ZPolygonArea(List<Vect> boundary)
        {
            double area = 0;

            for (int i = 0; i < boundary.Count; i++)
            {
                int index = i;
                int nextIndex = (i + 1) % boundary.Count;

                Vect current = boundary[index];
                Vect next = boundary[nextIndex];

                area += (0.5) * (current[1] + next[1]) * (current[0] - next[0]) ; //:((((
            }

            return Math.Abs(area);
        }

        public static string PointListToString(List<Vect> points)
        {
            StringBuilder builder = new();

            builder.Append("Polygon(");

            for(int i=0; i<points.Count; i++)
            {
                builder.Append(points[i].ToString());
                if (i != points.Count - 1)
                {
                    builder.Append(", ");
                }
            }

            builder.Append(")");

            return builder.ToString();
        }

        public static string Vertex2dListToString(IEnumerable<Vertex2d> points, string prefix = "polygon(", string suffix = ")")
        {
            StringBuilder builder = new();

            builder.Append(prefix);
            var last = points.Last();
            foreach (Vertex2d point in points)
            {
                builder.Append(point.ToString());
                if (point != last)
                {
                    builder.Append(", ");
                }
            }

            builder.Append(suffix);

            return builder.ToString();
        }

    }

    public class GimPolygonMesh
    {
        List<GimPolygon> gimPolygons = new();
        List<Triangle> triangles = new();

        private GimPolyMeshProperties? properties = null;

        public GimPolyMeshProperties Properties
        {
            get
            {
                if(properties == null) {
                    properties = CalculateProperties();
                }

                return (GimPolyMeshProperties)properties;
            }
        }

        public List<GimPolygon> GimPolygons
        {
            get
            {
                return gimPolygons;
            }
            set
            {
                gimPolygons = value;
            }
        }

        public List<Triangle> Triangles
        {
            get
            {
                return triangles;
            }
            set
            {
                triangles = value;
            }
        }
        
        public GimPolygonMesh(List<GimPolygon> gimPolygons, List<Triangle> triangles)
        {
            this.gimPolygons = gimPolygons;
            this.triangles = triangles;
        }

        public Vect GetCenter()
        {
            Vect center = new(0, 0, 0);
            int pointCount = 0;

            foreach(GimPolygon polygon in gimPolygons)
            {
                pointCount += polygon.OuterBoundary.Count;
                foreach(Vect point in polygon.OuterBoundary)
                {
                    center += point;
                }

                foreach(var boundary in polygon.InnerBoundaries)
                {
                    pointCount += boundary.Count;
                    foreach(Vect point in boundary)
                    {
                        center += point;
                    }
                }
            }

            center /= pointCount;

            return center;
        }

        public Vect GetMoment()
        {
            Vect center = GetCenter();
            Vect vectorSum = new(0, 0, 0);

            foreach(GimPolygon face in gimPolygons)
            {
                foreach(Vect point in face.OuterBoundary)
                {
                    Vect vector = (point - center);
                    vectorSum += vector.Norm * vector;
                }

                foreach(var innerBoundary in face.InnerBoundaries)
                {
                    foreach (Vect point in innerBoundary)
                    {
                        Vect vector = (point - center);
                        vectorSum += vector.Norm * vector;
                    }
                }
            }

            return vectorSum;
        }

        public GimPolygonMesh GetOrientedPolyMesh()
        {
            GimPolygonMesh orientedMesh = null;

            var props = Properties;
            Vect zVect = new(0, 0, 1);
            Vect vect1 = props.moment;
            Vect center = props.center;
            if (vect1.Norm == 0)
            {
                Vect longestVector = new(0, 0, 0);
                foreach(GimPolygon polygon in GimPolygons)
                {
                    foreach(Vect point in polygon.OuterBoundary)
                    {
                        Vect vector = (point - center);
                        if(vector.Norm > longestVector.Norm)
                        {
                            longestVector = vector;
                        }
                    }
                }
                vect1 = longestVector;
            }

            vect1 = vect1 - (vect1 * zVect) * zVect;
            vect1 = vect1.Direction;

            Vect vect2 = zVect ^ vect1;
            Vect vect3 = zVect;

#if DEBUG
            
            {
                string freeCAD = "";

                freeCAD += "cyl = Part.makeCylinder(2, " + 15 + ", FreeCAD.Base.Vector" + center.ToString() + ", FreeCAD.Base.Vector" + vect1.ToString() + ")\n";
                freeCAD += "cylObj = App.ActiveDocument.addObject(\"Part::Feature\",\"cyl" + 1 + "\")\n";
                freeCAD += "cylObj.Shape = cyl\n";

                freeCAD += "cylObj.ViewObject.ShapeColor  = (" + (new Random()).NextDouble().ToString("F1") + ", "
                    + (new Random()).NextDouble().ToString("F1") + ", " + (new Random()).NextDouble().ToString("F1") + ")\n\n\n";

                freeCAD += "cyl = Part.makeCylinder(2, " + 15 + ", FreeCAD.Base.Vector" + center.ToString() + ", FreeCAD.Base.Vector" + vect2.ToString() + ")\n";
                freeCAD += "cylObj = App.ActiveDocument.addObject(\"Part::Feature\",\"cyl" + 2 + "\")\n";
                freeCAD += "cylObj.Shape = cyl\n";

                freeCAD += "cylObj.ViewObject.ShapeColor  = (" + (new Random()).NextDouble().ToString("F1") + ", "
                    + (new Random()).NextDouble().ToString("F1") + ", " + (new Random()).NextDouble().ToString("F1") + ")\n\n\n";


                freeCAD += "cyl = Part.makeCylinder(2, " + 15 + ", FreeCAD.Base.Vector" + center.ToString() + ", FreeCAD.Base.Vector" + vect3.ToString() + ")\n";
                freeCAD += "cylObj = App.ActiveDocument.addObject(\"Part::Feature\",\"cyl" + 3 + "\")\n";
                freeCAD += "cylObj.Shape = cyl\n";

                freeCAD += "cylObj.ViewObject.ShapeColor  = (" + (new Random()).NextDouble().ToString("F1") + ", "
                    + (new Random()).NextDouble().ToString("F1") + ", " + (new Random()).NextDouble().ToString("F1") + ")\n\n\n";


            }
#endif

            Matrix transformation = (new Matrix(vect1, vect2, vect3));
            List<GimPolygon> newGimPolygons = new();
            
            foreach(var gimpoly in gimPolygons)
            {
                newGimPolygons.Add(gimpoly.Orient(center, transformation));
            }

            List<Triangle> newTriangles = new();
            foreach(var triangle in newTriangles)
            {
                Vect newPoint1 = transformation * (triangle.Vertex1 - center);
                Vect newPoint2 = transformation * (triangle.Vertex2 - center);
                Vect newPoint3 = transformation * (triangle.Vertex3 - center);

                newTriangles.Add(new(newPoint1, newPoint2, newPoint3));
            }

            orientedMesh = new(newGimPolygons, newTriangles);
            return orientedMesh;
        }

        public HashSet<Segment> GetUniqueSegments()
        {
            HashSet<Segment> result = new();
            foreach (GimPolygon polygon in gimPolygons)
            {
                for(int i=0; i<polygon.OuterBoundary.Count; i++)
                {
                    Vect currentPoint = polygon.OuterBoundary[i].Round(1);
                    int nextPointIndex = (i + 1) % polygon.OuterBoundary.Count;
                    Vect nextPoint = polygon.OuterBoundary[nextPointIndex].Round(1);

                    result.Add(new(currentPoint, nextPoint));
                }

                foreach(var innerBoundary in polygon.InnerBoundaries)
                {
                    for (int i = 0; i < innerBoundary.Count; i++)
                    {
                        Vect currentPoint = innerBoundary[i].Round(1);
                        int nextPointIndex = (i + 1) % innerBoundary.Count;
                        Vect nextPoint = innerBoundary[nextPointIndex].Round(1);

                        result.Add(new(currentPoint, nextPoint));
                    }
                }
            }

            foreach (var triangle in triangles)
            {
                result.Add(new(triangle.Vertex1.Round(1), triangle.Vertex2.Round(1)));
                result.Add(new(triangle.Vertex2.Round(1), triangle.Vertex3.Round(1)));
                result.Add(new(triangle.Vertex3.Round(1), triangle.Vertex1.Round(1)));
            }


            return result;
        }

        public HashSet<Vect> GetUniquePoints()
        {
            HashSet<Vect> result = new();

            foreach(GimPolygon polygon in gimPolygons)
            {
                foreach(Vect point in polygon.OuterBoundary)
                {
                    result.Add(point.Round(1));
                }

                foreach(var innerBoundary in polygon.InnerBoundaries)
                {
                    foreach (Vect point in innerBoundary)
                    {
                        result.Add(point.Round(1));
                    }
                }
            }

            foreach (var triangle in triangles)
            {
                result.Add(triangle.Vertex1.Round(1));
                result.Add(triangle.Vertex2.Round(1));
                result.Add(triangle.Vertex3.Round(1));
            }


            return result;
        }

        public override int GetHashCode()
        {
            int hashCode = 0;

            var prop = Properties;
            hashCode += prop.pointCount.GetHashCode();
            hashCode += prop.faceCount.GetHashCode();
            hashCode += prop.holeCount.GetHashCode();

            hashCode += Math.Round(prop.vectorSum.Norm,1).GetHashCode();
            hashCode += Math.Round(prop.moment.Norm,1).GetHashCode();

            hashCode += Math.Round(prop.longestLength,1).GetHashCode();
            hashCode += Math.Round(prop.shortestLength,1).GetHashCode();

            return hashCode;
        }

        private GimPolyMeshProperties CalculateProperties()
        {
            int pointCount = 0;
            double longestLength = 0;
            double shortestLength = int.MaxValue;

            foreach (var polygon in gimPolygons)
            {
                pointCount += polygon.OuterBoundary.Count;

                foreach(var boundary in polygon.InnerBoundaries)
                {
                    pointCount += boundary.Count;
                }

                var longestShortest = polygon.GetLongestAndShortestLengths();
                if(longestShortest.Item1 > longestLength) longestLength = longestShortest.Item1;
                if(longestShortest.Item2 < shortestLength) shortestLength = longestShortest.Item2;
            }

            Vect vectorSum = new Vect(0, 0, 0);

            Vect center = GetCenter();
            foreach (var polygon in gimPolygons)
            {
                vectorSum += polygon.GetOrientedNormal(center);
            }

            int faceCount = gimPolygons.Count;
            int holeCount = 0;

            foreach (var polygon in gimPolygons)
            {
                holeCount += polygon.InnerBoundaries.Count;
            }

            GimPolyMeshProperties properties = new()
            {
                pointCount = pointCount,
                faceCount = faceCount,
                holeCount = holeCount,

                shortestLength = shortestLength,
                longestLength = longestLength,

                vectorSum = vectorSum,
                moment = GetMoment() / pointCount,
                center = center
            };
            return properties;
        }

    }

    public struct GimPolyMeshProperties
    {
        public int pointCount;
        public int faceCount;
        public int holeCount;

        // vectors, don't compare directly
        public Vect vectorSum;
        public Vect moment;
        public Vect center;

        public double longestLength;
        public double shortestLength;

    }
}
