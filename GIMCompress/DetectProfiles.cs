using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GimUtilities;

[assembly: InternalsVisibleTo("TestPlatform")]
namespace GIMcompress
{
	// all the faces found will be constructed as polygons, which have a few key informations to them.
	public class PolygonRepresentation
    {
        // stored point types

        // for most points, e.g. rectangle corners
        public const int POINT_BASEPOINT = 0;
        // control points, for non-flat curves:
        // bspline
        public const int POINT_CONTROL_BSPLINE = 1;
        // bezier
        public const int POINT_CONTROL_BEZIER = 2;
        // cylinder
        public const int POINT_CONTROL_CYLINDER = 3;
        // stored polygon type

        // for most quadrilaterals (flat planes)
        public const int POLYGON_QUADRILATERAL = 0;
        // for bsplines
        public const int POLYGON_BSPLINE = 1;
        // for beziers
        public const int POLYGON_BEZIER = 2;
        // for cylindrical polygons (cut sections of a cylinder)
        public const int POLYGON_CYLINDER = 3;


        // margin of equality of two points

        public static readonly double EQUALITY_MARGIN = 0.000001;

        // threshold of profile: if a polygon chain is longer than this number, it's a profile

        public const int chainLengthThreshold = 5;

        // attributes

        List<Vect> points; // the list of points of the polygon
        List<int> pointTypes; // the respective type of each point as defined above in the constants

        int polygonType;

        Vect direction;
        public int originalElement;

        // the list of polygons this polygon is attacked to, i.e. other faces.
        // the first element in this linked list is linked to at least 2 polygons (at most 2?).
        // if it is linked to less than 2, then we have an open surface. If it is linked to 2 chains,
        // and both chains lead to the same element, then it's a closed surface. Otherwise it's open
        List<PolygonRepresentation> next;
        List<PolygonRepresentation> prev;
        // if the polygon is linked to another or not. used to distinguish between different links of polygons
        // as the first element is always linked from but not linked to
        bool linked = false;
        bool isStart = true;

        // if true, GetBothFacePoints returns B and A instead of A and B
        public bool inversed = false;

        // strictly for bsplines
        public List<double[]> bsplineDoubles = new();
        public List<int[]> bsplineInts = new();

        // wall piece properties
        public double[] faceDoubles = null;
        public int[] faceInts = null;
        public GimLocal faceLocal = null;

        #region Properties

        public int PolygonType
        {
            get => polygonType;
            set => polygonType = value;
        }

        public List<Vect> Points
        {
            get => points;
            set => points = value;
        }

        public List<int> PointTypes
        {
            get => pointTypes;
            set => pointTypes = value;
        }

        public Vect Direction
        {
            get => direction;
            set => direction = value;
        }

        public bool Linked
        {
            get => linked;
            set => linked = value;
        }
        public bool IsStart
        {
            get => isStart;
            set => isStart = value;
        }

        public List<PolygonRepresentation> Next
        {
            get => next;
            set => next = value;
        }

        public List<PolygonRepresentation> Prev
        {
            get => prev;
            set => prev = value;
        }

        public string Name = ""; //temporary for debugging

        #endregion

        public PolygonRepresentation()
        {
            points = new();
            pointTypes = new();
            next = new();
            prev = new();
            direction = null;
            polygonType = POLYGON_QUADRILATERAL;
        }

        public void AddPoint(Vect point, int pointType)
        {
            point = point.Round(); // rounding imprecisions.
            points.Add(point);
            pointTypes.Add(pointType);
        }

        public void LinkPolygon(PolygonRepresentation polygon)
        {
            next.Add(polygon);
            polygon.prev.Add(this);
            polygon.linked = true;
            polygon.isStart = false;
        }

        // checks if a polygon shares an edge with another.
        // to share an edge, both polygons must share 2 points
        public bool SharesEdgeWith(PolygonRepresentation polygon)
        {
            int pointAindex = -1;
            bool breakLoop = false;
            // finding the first point
            for (int i = 0; i < points.Count; i++)
            {
                Vect thisPoint = points[i];
                // we do not check edge equality along non-base points (aka control points);
                if (pointTypes[i] != POINT_BASEPOINT) continue;
                for (int j = 0; j < polygon.Points.Count; j++)
                {

                    if (polygon.PointTypes[j] != POINT_BASEPOINT) continue;

                    Vect otherpoint = polygon.Points[j];

                    // comparing that the points are the same
                    if (thisPoint == otherpoint)
                    {
                        pointAindex = i;
                        breakLoop = true;
                        break;
                    }

                }
                if (breakLoop) break;
            }

            // if we didn't even find a point, the polygons don't share an edge
            if (pointAindex == -1) return false;

            // finding the second point;
            breakLoop = false;
            int pointBindex = -1;

            for (int i = 0; i < points.Count; i++)
            {
                // we avoid rechecking the same point
                if (i == pointAindex) continue;
                Vect thisPoint = points[i];
                // we do not check edge equality along non-base points (aka control points);
                if (pointTypes[i] != POINT_BASEPOINT) continue;

                for (int j = 0; j < polygon.Points.Count; j++)
                {

                    if (polygon.PointTypes[j] != POINT_BASEPOINT) continue;

                    Vect otherpoint = polygon.Points[j];

                    // comparing that the points are the same
                    if (thisPoint == otherpoint)
                    {
                        pointBindex = i;
                        breakLoop = true;
                        break;
                    }

                }
                if (breakLoop) break;
            }

            // if the polygons share only 1 point somehow, they don't share an edge
            if (pointBindex == -1) return false;

            return true;

        }
        public bool SharesBothEdgesWith(PolygonRepresentation polygon)
        {
            int pointAindex = -1;
            bool breakLoop = false;
            // finding the first point
            for (int i = 0; i < points.Count; i++)
            {
                Vect thisPoint = points[i];
                // we do not check edge equality along non-base points (aka control points);
                if (pointTypes[i] != POINT_BASEPOINT) continue;
                for (int j = 0; j < polygon.Points.Count; j++)
                {

                    if (polygon.PointTypes[j] != POINT_BASEPOINT) continue;

                    Vect otherpoint = polygon.Points[j];

                    // comparing that the points are the same
                    if (thisPoint == otherpoint)
                    {
                        pointAindex = i;
                        breakLoop = true;
                        break;
                    }

                }
                if (breakLoop) break;
            }

            // if we didn't even find a point, the polygons don't share an edge
            if (pointAindex == -1) return false;

            // finding the second point;
            breakLoop = false;
            int pointBindex = -1;

            for (int i = 0; i < points.Count; i++)
            {
                // we avoid rechecking the same point
                if (i == pointAindex) continue;
                Vect thisPoint = points[i];
                // we do not check edge equality along non-base points (aka control points);
                if (pointTypes[i] != POINT_BASEPOINT) continue;

                for (int j = 0; j < polygon.Points.Count; j++)
                {

                    if (polygon.PointTypes[j] != POINT_BASEPOINT) continue;

                    Vect otherpoint = polygon.Points[j];

                    // comparing that the points are the same
                    if (thisPoint == otherpoint)
                    {
                        pointBindex = i;
                        breakLoop = true;
                        break;
                    }

                }
                if (breakLoop) break;
            }

            // if the polygons share only 1 point somehow, they don't share an edge
            if (pointBindex == -1) return false;

            // finding the third point;
            breakLoop = false;
            int pointCindex = -1;

            for (int i = 0; i < points.Count; i++)
            {
                // we avoid rechecking the same points
                if (i == pointAindex || i == pointBindex) continue;
                Vect thisPoint = points[i];
                // we do not check edge equality along non-base points (aka control points);
                if (pointTypes[i] != POINT_BASEPOINT) continue;

                for (int j = 0; j < polygon.Points.Count; j++)
                {

                    if (polygon.PointTypes[j] != POINT_BASEPOINT) continue;

                    Vect otherpoint = polygon.Points[j];

                    // comparing that the points are the same
                    if (thisPoint == otherpoint)
                    {
                        pointCindex = i;
                        breakLoop = true;
                        break;
                    }

                }
                if (breakLoop) break;
            }

            // polygons don't share two edges, just one
            if (pointCindex == -1) return false;

            // finding the third point;
            breakLoop = false;
            int pointDindex = -1;

            for (int i = 0; i < points.Count; i++)
            {
                // we avoid rechecking the same points
                if (i == pointAindex || i == pointBindex || i == pointCindex) continue;
                Vect thisPoint = points[i];
                // we do not check edge equality along non-base points (aka control points);
                if (pointTypes[i] != POINT_BASEPOINT) continue;

                for (int j = 0; j < polygon.Points.Count; j++)
                {

                    if (polygon.PointTypes[j] != POINT_BASEPOINT) continue;

                    Vect otherpoint = polygon.Points[j];

                    // comparing that the points are the same
                    if (thisPoint == otherpoint)
                    {
                        pointDindex = i;
                        breakLoop = true;
                        break;
                    }

                }
                if (breakLoop) break;
            }

            // polygons don't share two edges, just one
            if (pointDindex == -1) return false;

            return true;

        }

            // in some cases, due to the haphazard ordering of the elements, lists of this form will be created:
            //        .------ A -------.
            //        v                v
            //        B                C                         // A points into B and C, but so does D
            //        ^                ^
            //        '-------D--------'
            //
            // i.e. one given link is detected as two starts, and two ends. these are detected as two different polygon links.
            // This next functions travels along one path, completely flipping any wrong directions, turning the linked list to the form:
            //
            //        .------ A <------.
            //        v                |
            //        B                C                         // A points into B, into C, into D
            //        |                ^
            //        '-----> D -------'
            public void UnwindLinks()
        {
            PolygonRepresentation startElement = this;
            if (next.Count == 0) return;
            PolygonRepresentation endElement = this;
            PolygonRepresentation currentElement = this;
            bool knotEncountered = false; // a knot is an element being pointed at by 2 elements.
                                          // This flips to false again when we find an "anti-knot"
                                          // aka an element pointing at 2 elements 

            bool closedCircuit = false;
            int count = 0;

            while (currentElement != null)
            {
                // we iterate normally until we find a knot

                // dead end
                if (currentElement.Next.Count == 0)
                {
                    count++;
                    endElement = currentElement;
                    break;
                }
                if (currentElement.Next[0].Next.Count == 0) // potential knot in the next element
                {
                    PolygonRepresentation nextElement = currentElement.Next[0];

                    if (nextElement.Prev.Count == 2) // knot found
                    {
                        // important: we have to differentiate where we came from, from where we're going
                        int currentIndex = nextElement.Prev.IndexOf(currentElement);
                        int continueIndex; // the true "next" element, this is where the flipping of the arrows begins.

                        if (currentIndex == 0) continueIndex = 1;
                        else continueIndex = 0;

                        // the element we're going to move from "prev" to "next" to unwind the loop
                        // A -> B <- C     =>     A -> B -> C
                        PolygonRepresentation elementToBeShifted = nextElement.Prev[continueIndex];

                        nextElement.Prev.RemoveAt(continueIndex);
                        nextElement.LinkPolygon(elementToBeShifted);
                        elementToBeShifted.Next.Remove(nextElement);

                        currentElement = nextElement;

                        knotEncountered = true;
                    }
                    else // false positive, next element is a dead end
                    {
                        count ++; // accounting for current and Next
                        endElement = currentElement.Next[0];
                        currentElement = currentElement.Next[0];
                        continue;
                    }
                }
                else // no knot encountered
                {
                    endElement = currentElement;
                    currentElement = currentElement.Next[0];
                    if (currentElement == startElement) // we stumbled upon the start again! we leave and mark it as a closed circuit
                    {
                        closedCircuit = true;
                        count++;
                        break;
                    }
                }
                count++;
            }

            // sometimes full chains aren't detected (for cylinders), this fixes that
            if(count == 2) // if we only have two polygons, they must share TWO edges to be a full link
            {
                bool link = startElement.SharesBothEdgesWith(endElement);
                if (link) // polygons share two edges, we close off the circuit
                {
                    if(endElement.Next.Count == 0)
                    {
                        endElement.LinkPolygon(startElement);
                        startElement.isStart = true;
                        startElement.Linked = true;
                    }
                    else
                    {
                        if(endElement.Next[0] == startElement)
                        {
                            startElement.isStart = true;
                            startElement.Linked = true;
                        }
                    }
                }
            }
            else // in the case of more than 2 polygons, the end polygon must share one edge with the start polygon
            {
                if (startElement.SharesEdgeWith(endElement))
                {
                    if (endElement.Next.Count == 0)
                    {
                        endElement.LinkPolygon(startElement);
                        startElement.isStart = true;
                        startElement.Linked = true;
                    }
                    else
                    {
                        if (endElement.Next[0] == startElement)
                        {
                            startElement.isStart = true;
                            startElement.Linked = true;
                        }
                    }
                }
                return;
            }

            if (closedCircuit)
            {
                startElement.IsStart = true;
            }
            //else
            //{
            //    if (startElement.Prev.Count == 0)
            //        startElement.Linked = true;
            //}



        }

        public Tuple<List<Vect>, List<int>, List<int[]>, List<double[]>> GetSideAPoints() // we get the points of the first SIDE
        {
            int firstPointIndex = 0;
            List<Vect> facePoints = new();
            List<int> facePointTypes = new();
            List<int[]> faceBsplineInts = new();
            List<double[]> faceBsplineDoubles = new();
            // we find the first edge that is colinear to the direction,
            // and return all points between that and the next edge
            int basePointIndex = 0;
            for (int i = 0; true; i++)
            {
                int currentIndex = i % points.Count;
                int nextIndex = (i + 1) % points.Count;
                Vect currentPoint = points[currentIndex];
                Vect nextPoint = points[nextIndex];

                int currentType = pointTypes[currentIndex];
                if (currentType != POINT_BASEPOINT) continue;

                Vect vector = nextPoint - currentPoint;
                basePointIndex++;
                basePointIndex %= bsplineDoubles.Count; 
                if (direction.IsCollinearTo(vector) && vector.Norm != 0) // we found said edge, we return all points after it
                {
                    firstPointIndex = nextIndex;
                    break;
                }
            }

            for (int i = firstPointIndex; true; i++)
            {
                int currentIndex = i % points.Count;
                int nextIndex = (i + 1) % points.Count;
                Vect currentPoint = points[currentIndex];
                Vect nextPoint = points[nextIndex];

                int currentPointType = pointTypes[currentIndex];
                int nextPointType = pointTypes[nextIndex];

                facePoints.Add(currentPoint);
                facePointTypes.Add(pointTypes[currentIndex]);


                Vect vector = nextPoint - currentPoint;

                if (direction.IsCollinearTo(vector) && vector.Norm != 0 && (currentPointType == nextPointType) && (currentPointType == POINT_BASEPOINT)) // we found the next edge, we stop and return.
                {
                    break;
                }


                if (currentPointType == POINT_BASEPOINT)
                {
                    faceBsplineInts.Add(bsplineInts[basePointIndex]);
                    faceBsplineDoubles.Add(bsplineDoubles[basePointIndex]);
                    basePointIndex++;
                    basePointIndex %= bsplineDoubles.Count;
                }

            }

            return new Tuple<List<Vect>, List<int>, List<int[]>, List<double[]>>(facePoints, facePointTypes, faceBsplineInts, faceBsplineDoubles);
        }

        public Tuple<List<Vect>, List<int>, List<int[]>, List<double[]>> GetSideBPoints() // we get the points of the second face
        {
            int firstPointIndex;
            List<Vect> facePoints = new();
            List<int> facePointTypes = new();
            List<int[]> faceBsplineInts = new();
            List<double[]> faceBsplineDoubles = new();
            // we find the first edge that is colinear to the direction,
            // and return all points between that and the next edge, from the OTHER direction
            int basePointIndex = bsplineDoubles.Count - 2;
            for (int i = 0; true; i++)
            {
                int currentIndex = i % points.Count;
                int nextIndex = (i + 1) % points.Count;
                Vect currentPoint = points[currentIndex];
                Vect nextPoint = points[nextIndex];


                int currentType = pointTypes[currentIndex];
                if (currentType != POINT_BASEPOINT) continue;

                Vect vector = nextPoint - currentPoint;
                basePointIndex++;
                basePointIndex %= bsplineDoubles.Count;

                if (direction.IsCollinearTo(vector) && vector.Norm != 0) // we found said edge, we return all points BEFORE it
                {
                    firstPointIndex = currentIndex;
                    break;
                }
            }
            // this is the same as the above function, however we cycle in the opposite order, and append the points
            // to the start of the list instead of to the end of it, to preserve cyclicity.
            for (int i = firstPointIndex; true; i--)
            {
                int currentIndex = i % points.Count;
                if (currentIndex < 0) currentIndex += Points.Count;
                int nextIndex = (i - 1) % points.Count;
                if (nextIndex < 0) nextIndex += Points.Count;
                Vect currentPoint = points[currentIndex];
                Vect nextPoint = points[nextIndex];


                int currentPointType = pointTypes[currentIndex];
                int nextPointType = pointTypes[nextIndex];

                facePoints.Add(currentPoint);
                facePointTypes.Add(pointTypes[currentIndex]);


                Vect vector = nextPoint - currentPoint;

                if (direction.IsCollinearTo(vector) && vector.Norm != 0 && (currentPointType == nextPointType) && (currentPointType == POINT_BASEPOINT)) // we found the next edge, we stop and return.
                {
                    break;
                }

                if (currentPointType == POINT_BASEPOINT)
                {
                    faceBsplineInts.Add(bsplineInts[basePointIndex]);
                    faceBsplineDoubles.Add(bsplineDoubles[basePointIndex]);
                    basePointIndex--;
                    if (basePointIndex < 0) basePointIndex += bsplineDoubles.Count;
                    basePointIndex %= bsplineDoubles.Count;
                }
            }

            return new Tuple<List<Vect>, List<int>, List<int[]>, List<double[]>>(facePoints, facePointTypes, faceBsplineInts, faceBsplineDoubles);
        }

        public PolygonRepresentation GetSideAPolygon() // returns the first side as a polygon
        {
            //int firstPointIndex;
            PolygonRepresentation sideAPolygon = new();

            var sideAPointData = GetSideAPoints();
            List<Vect> sideAPoints = sideAPointData.Item1;
            List<int> sideAPointTypes = sideAPointData.Item2;
            List<int[]> sideABsplineInts = sideAPointData.Item3;
            List<double[]> sideABsplineDoubles = sideAPointData.Item4;

            for (int i = 0; i < sideAPoints.Count; i++) 
            {
                sideAPolygon.AddPoint(sideAPoints[i], sideAPointTypes[i]);
            }

            for(int i=0; i< sideABsplineInts.Count; i++)
            {
                sideAPolygon.bsplineInts.Add(sideABsplineInts[i].Clone() as int[]);
                sideAPolygon.bsplineDoubles.Add(sideABsplineDoubles[i].Clone() as double[]);
            }


            sideAPolygon.polygonType = polygonType;
            sideAPolygon.faceDoubles = faceDoubles;
            sideAPolygon.faceInts = faceInts;
            sideAPolygon.faceLocal = faceLocal;
            return sideAPolygon;

            //// we find the first edge that is colinear to the direction,
            //// and return all points between that and the next edge
            //for (int i = 0; true; i++)
            //{
            //    int currentIndex = i % points.Count;
            //    int nextIndex = (i + 1) % points.Count;
            //    Vect currentPoint = points[currentIndex];
            //    Vect nextPoint = points[nextIndex];

            //    Vect vector = nextPoint - currentPoint;
            //    if (direction.IsCollinearTo(vector)) // we found said edge, we return all points after it
            //    {
            //        firstPointIndex = nextIndex;
            //        break;
            //    }
            //}

            //for (int i = firstPointIndex; true; i++)
            //{
            //    int currentIndex = i % points.Count;
            //    int nextIndex = (i + 1) % points.Count;
            //    Vect currentPoint = points[currentIndex];
            //    Vect nextPoint = points[nextIndex];

            //    sideAPolygon.AddPoint(currentPoint, pointTypes[currentIndex]);

            //    Vect vector = nextPoint - currentPoint;
            //    if (direction.IsCollinearTo(vector)) // we found the next edge, we stop and return.
            //    {
            //        break;
            //    }
            //}

        }


        public PolygonRepresentation GetSideBPolygon() // returns the second side as a polygon
        {
            //int firstPointIndex;
            PolygonRepresentation sideBPolygon = new();

            var sideBPointData = GetSideBPoints();
            List<Vect> sideBPoints = sideBPointData.Item1;
            List<int> sideBPointTypes = sideBPointData.Item2;
            List<int[]> sideBBsplineInts = sideBPointData.Item3;
            List<double[]> sideBBsplineDoubles = sideBPointData.Item4;

            for (int i = 0; i < sideBPoints.Count; i++)
            {
                sideBPolygon.AddPoint(sideBPoints[i], sideBPointTypes[i]);
            }

            for (int i = 0; i < sideBBsplineInts.Count; i++)
            {
                sideBPolygon.bsplineInts.Add(sideBBsplineInts[i].Clone() as int[]);
                sideBPolygon.bsplineDoubles.Add(sideBBsplineDoubles[i].Clone() as double[]);
            }


            sideBPolygon.polygonType = polygonType;

            sideBPolygon.polygonType = polygonType;
            sideBPolygon.faceDoubles = faceDoubles;
            sideBPolygon.faceInts = faceInts;
            sideBPolygon.faceLocal = faceLocal;

            return sideBPolygon;

            //// we find the first edge that is colinear to the direction,
            //// and return all points between that and the next edge, from the OTHER direction
            //for (int i = 0; true; i++)
            //{
            //    int currentIndex = i % points.Count;
            //    int nextIndex = (i + 1) % points.Count;
            //    Vect currentPoint = points[currentIndex];
            //    Vect nextPoint = points[nextIndex];

            //    Vect vector = nextPoint - currentPoint;
            //    if (direction.IsCollinearTo(vector)) // we found said edge, we return all points EBFORE it
            //    {
            //        firstPointIndex = currentIndex;
            //        break;
            //    }
            //}
            //// this is the same as the above function, however we cycle in the opposite order, and append the points
            //// to the start of the list instead of to the end of it, to preserve cyclicity.
            //for (int i = firstPointIndex; true; i--)
            //{
            //    int currentIndex = i % points.Count;
            //    if (currentIndex < 0) currentIndex += Points.Count;
            //    int nextIndex = (i - 1) % points.Count;
            //    if (nextIndex < 0) nextIndex += Points.Count;
            //    Vect currentPoint = points[currentIndex];
            //    Vect nextPoint = points[nextIndex];

            //    sideBPolygon.AddPoint(currentPoint, pointTypes[currentIndex]);

            //    Vect vector = nextPoint - currentPoint;
            //    if (direction.IsCollinearTo(vector)) // we found the next edge, we stop and return.
            //    {
            //        break;
            //    }
            //}

            //sideBPolygon.polygonType = polygonType;
            //return sideBPolygon;
        }

        // used for simple polygons (sideA, sideB, cutsection).
        public List<GimElement> GetSimplePolygonAsCurveElements()
        {
            if (PolygonType == POLYGON_BSPLINE)
            {
                int x = 3;
            }
            return ConvertPointsToCurves(points, pointTypes, bsplineInts, bsplineDoubles);
        }

        // return sideA as curve elements
        public List<GimElement> GetSideAAsCurveElements()
        {

            var pointData = GetSideAPoints();
            List<Vect> pointList = pointData.Item1;
            List<int> pointTypes = pointData.Item2; // doesn't matter in case of a line
            List<int[]> curveInts = pointData.Item3;
            List<double[]> curveDoubles = pointData.Item4;

            return ConvertPointsToCurves(pointList, pointTypes, curveInts, curveDoubles);
        }

        // return sideB as curve Elements

        public List<GimElement> GetSideBAsCurveElements()
        {
            var pointData = GetSideBPoints();
            List<Vect> pointList = pointData.Item1;
            List<int> pointTypes = pointData.Item2; // doesn't matter in case of a line
            List<int[]> curveInts = pointData.Item3;
            List<double[]> curveDoubles = pointData.Item4;

            return ConvertPointsToCurves(pointList, pointTypes, curveInts, curveDoubles);
        }

        // returns faceA and faceB as a list of points.
        public Tuple<List<Vect>[], List<int>[], List<int[]>[], List<double[]>[]> GetBothFacePoints()
        {
            PolygonRepresentation currentElement = this;
            List<Vect> faceA = new();
            List<int> faceATypes = new();
            List<int[]> faceAInts = new();
            List<double[]> faceADoubles = new();

            List<Vect> faceB = new();
            List<int> faceBTypes = new();
            List<int[]> faceBInts = new();
            List<double[]> faceBDoubles = new();

            Vect faceALastPoint = points[0]; // will determine which face points go to, faceALastPoint is part of faceA
            int polyIndex = 0;
            do
            {
                var TA = currentElement.GetSideAPoints();
                List<Vect> faceAPoints = TA.Item1;
                List<int> faceAPointTypes = TA.Item2;
                List<int[]> faceABsplineInts = TA.Item3;
                List<double[]> faceABsplineDoubles = TA.Item4;
                var TB = currentElement.GetSideBPoints();
                List<Vect> faceBPoints = TB.Item1;
                List<int> faceBPointTypes = TB.Item2;
                List<int[]> faceBBsplineInts = TB.Item3;
                List<double[]> faceBBsplineDoubles = TB.Item4;

                double distanceA = 0;
                double distanceB = 0;

                for (int i = 0; i < faceAPoints.Count; i++) // we calculate the average distance to each face. the closer face to last point
                                                            // will be added to faceA, the other points will be added to faceB
                {
                    distanceA += (faceAPoints[i] - faceALastPoint).Norm;
                }

                for(int i=0; i < faceBPoints.Count; i++)
                {
                    distanceB += (faceBPoints[i] - faceALastPoint).Norm;
                }

                List<Vect> closerFacePoints = null;
                List<int> closerFacePointTypes = null;
                List<Vect> furtherFacePoints = null;
                List<int> furtherFacePointTypes = null;

                if (distanceA <= distanceB) // face A is closer => is part of the same face
                {
                    closerFacePoints = faceAPoints;
                    closerFacePointTypes = faceAPointTypes;
                    furtherFacePoints = faceBPoints;
                    furtherFacePointTypes = faceBPointTypes;
                }
                else // face B is closer => is part of the same face
                {
                    closerFacePoints = faceBPoints;
                    closerFacePointTypes = faceBPointTypes;
                    furtherFacePoints = faceAPoints;
                    furtherFacePointTypes = faceAPointTypes;
                }

                // now we determine if we got the points in the right order, or backwards (in which case we flip them)

                Vect firstPoint = closerFacePoints[0];
                Vect lastPoint = closerFacePoints[^1];

                double firstDistance = (firstPoint - faceALastPoint).Norm;
                double lastDistance = (lastPoint - faceALastPoint).Norm;

                if (firstDistance <= lastDistance) // the points are in the right order, we place them as they are.
                {
                    int start = 1;
                    if (polyIndex == 0) start = 0;
                    // A points, we start from 1 to not add the previous point
                    for (int i = start; i < closerFacePoints.Count; i++) 
                    {
                        //if (closerFacePoints[i] == faceALastPoint) continue; // make sure we don't add duplicate points
                        faceA.Add(closerFacePoints[i]);
                        faceATypes.Add(closerFacePointTypes[i]);
                    }

                    // A ints and doubles
                    for(int i = 0; i < faceABsplineInts.Count; i++)
                    {
                        faceAInts.Add(faceABsplineInts[i]);
                        faceADoubles.Add(faceABsplineDoubles[i]);
                    }

                    // B points, we start from 1 to not add the previous point

                    for (int i = start; i < furtherFacePoints.Count; i++) 
                    {
                        faceB.Add(furtherFacePoints[i]);
                        faceBTypes.Add(furtherFacePointTypes[i]);
                    }

                    // B ints and doubles
                    for (int i = 0; i < faceBBsplineInts.Count; i++)
                    {
                        faceBInts.Add(faceBBsplineInts[i]);
                        faceBDoubles.Add(faceBBsplineDoubles[i]);
                    }
                }
                else // the points are backwards, we insert them backwards.
                {
                    // A points
                    for (int i = closerFacePoints.Count - 2; i >= 0; i--)
                    {
                        //if (closerFacePoints[i] == faceALastPoint) continue; // make sure we don't add duplicate points
                        faceA.Add(closerFacePoints[i]);
                        faceATypes.Add(closerFacePointTypes[i]);
                    }

                    // A ints and doubles
                    for (int i = faceABsplineInts.Count - 1; i >= 0; i--) 
                    {
                        faceAInts.Add(faceABsplineInts[i]);
                        // we reverse the bspline doubles by reversing the knots and then reversing the weights
                        faceADoubles.Add(ReverseBsplineDoubles(faceABsplineInts[i], faceABsplineDoubles[i]));
                    }

                    // B points
                    for (int i = furtherFacePoints.Count - 2; i >= 0; i--) 
                    {
                        faceB.Add(furtherFacePoints[i]);
                        faceBTypes.Add(furtherFacePointTypes[i]);
                    }

                    // B ints and doubles
                    for (int i = faceBBsplineInts.Count - 1; i >= 0; i--)
                    {
                        faceBInts.Add(faceBBsplineInts[i]);
                        // we reverse the bspline doubles by reversing the knots and then reversing the weights
                        faceBDoubles.Add(ReverseBsplineDoubles(faceBBsplineInts[i], faceBBsplineDoubles[i]));
                    }
                }

                faceALastPoint = faceA[^1]; // we use the last point of faceA as reference to construction of the next face.
                if (currentElement.next.Count == 0) break;
                currentElement = currentElement.next[0];

                polyIndex++;

            } while (currentElement != this && !currentElement.IsStart);



            List<Vect>[] faces;
            List<int>[] faceTypes;
            List<int[]>[] faceInts;
            List<double[]>[] faceDoubles;

            if (inversed) // certain polygons are inversed. this helps for profiles that are detected on opposite ways
            {
                faces = new List<Vect>[] { faceB, faceA };
                faceTypes = new List<int>[] { faceBTypes, faceATypes };
                faceInts = new List<int[]>[] { faceBInts, faceAInts };
                faceDoubles = new List<double[]>[] { faceBDoubles, faceADoubles };
            }
            else
            {
                faces = new List<Vect>[] { faceA, faceB };
                faceTypes = new List<int>[] { faceATypes, faceBTypes };
                faceInts = new List<int[]>[] { faceAInts, faceBInts };
                faceDoubles = new List<double[]>[] { faceADoubles, faceBDoubles };
            }
            return new Tuple<List<Vect>[], List<int>[], List<int[]>[], List<double[]>[]>(faces, faceTypes, faceInts, faceDoubles);

        }

        public List<PolygonRepresentation>[] GetFacesAsPolygons()
        {
            PolygonRepresentation currentElement = this;
            List<PolygonRepresentation> faceA = new();
            List<PolygonRepresentation> faceB = new();

            Vect faceALastPoint = points[0]; // will determine which face points go to, faceALastPoint is part of faceA
            do
            {
                var TA = currentElement.GetSideAPoints();
                PolygonRepresentation sideA = currentElement.GetSideAPolygon();
                var TB = currentElement.GetSideBPoints();
                PolygonRepresentation sideB = currentElement.GetSideBPolygon();

                double distanceA = 0;
                double distanceB = 0;

                for (int i = 0; i < sideA.Points.Count; i++) // we calculate the average distance to each face. the closer face to last point
                                                            // will be added to faceA, the other points will be added to faceB
                {
                    distanceA += (sideA.Points[i] - faceALastPoint).Norm;
                }

                distanceA /= sideA.Points.Count;

                for (int i = 0; i < sideB.Points.Count; i++) 
                {
                    distanceB += (sideB.Points[i] - faceALastPoint).Norm;
                }

                distanceB /= sideB.Points.Count;

                PolygonRepresentation closerPolygon = null;
                PolygonRepresentation furtherPolygon = null;

                if (distanceA <= distanceB) // face A is closer => is part of the same face
                {
                    closerPolygon = sideA;
                    furtherPolygon = sideB;
                }
                else // face B is closer => is part of the same face
                {
                    closerPolygon = sideB;
                    furtherPolygon = sideA;
                }

                faceA.Add(closerPolygon);
                faceB.Add(furtherPolygon);

                faceALastPoint = closerPolygon.Points[^1]; // we use the last point of faceA as reference to construction of the next face.
                if (currentElement.next.Count == 0) break;
                currentElement = currentElement.next[0];
                if(currentElement == null)
                {
                    break;
                }

            } while (currentElement != this && !currentElement.IsStart);

            if (inversed) // polygon is marked as inversed
            {
                return new List<PolygonRepresentation>[] { faceB, faceA };
            }
            else
            {
                return new List<PolygonRepresentation>[] { faceA, faceB };
            }
        }

        public List<PolygonRepresentation> GetCutSectionAsPolygons(bool projectOnFaceB = false) // projecting the cut section as a
                                                                     // perpendicular face to the profile direction
        {
            var faces = GetFacesAsPolygons();
            List<PolygonRepresentation> faceA;
            if (projectOnFaceB)
            {
                faceA = faces[1];
            }
            else
            {
                faceA = faces[0];
            }

            List<PolygonRepresentation> cutSectionPolygons = new();
            Vect planeNormal = Direction;
            Vect planeBasePoint = faceA[0].Points[0];

            // we now get the two plane vectors
            Vect planeVectorA = (new Vect(1, 0, 0)) ^ planeNormal;
            if(planeVectorA.Norm == 0)
            {
                planeVectorA = (new Vect(0, 1, 0) ^ planeNormal); // if ux is colinear to planeNormal, then uy is definitely not.
            }

            planeVectorA = planeVectorA.Direction;

            Vect planeVectorB = planeNormal ^ planeVectorA; // we get an orthogonal, right handed base for our plane
            planeVectorB = planeVectorB.Direction;

            // our plane is now defined as basePoint + t1*planeVectorA + t2*planeVectorB

            for(int i=0; i<faceA.Count; i++)
            {
                PolygonRepresentation polygon = faceA[i];
                PolygonRepresentation projectedPolygon = new();
                projectedPolygon.PolygonType = polygon.PolygonType;
                projectedPolygon.Direction = polygon.Direction;
                projectedPolygon.faceDoubles = polygon.faceDoubles;
                projectedPolygon.faceInts = polygon.faceInts;
                projectedPolygon.faceLocal = polygon.faceLocal;
                projectedPolygon.bsplineInts = new();
                projectedPolygon.bsplineDoubles = new();
                // copying spline ints and doubles
                for(int j=0; j<polygon.bsplineInts.Count; j++)
                {
                    projectedPolygon.bsplineInts.Add(polygon.bsplineInts[j].Clone() as int[]);
                    projectedPolygon.bsplineDoubles.Add(polygon.bsplineDoubles[j].Clone() as double[]);
                }

                for(int j=0; j<polygon.Points.Count; j++)
                {
                    Vect point = polygon.Points[j];
                    int pointType = polygon.PointTypes[j];

                    // projection onto plane along planeVectorA
                    Vect dislocationVector = point - planeBasePoint;
                    Vect dislocationVectorProjection =
                        (dislocationVector * planeVectorA) * planeVectorA + (dislocationVector * planeVectorB) * planeVectorB;

                    Vect projectedPoint = planeBasePoint + dislocationVectorProjection;

                    projectedPolygon.AddPoint(projectedPoint, pointType);
                }

                cutSectionPolygons.Add(projectedPolygon);
            }

            return cutSectionPolygons;
        }

        public override string ToString() // prints polygon in an understandable manner
        {
            string s = "";
            /* TODO UNCOMMENT
            switch (polygonType)
            {
                case POLYGON_QUADRILATERAL: s += "Quadrilateral: "; break;
                case POLYGON_BSPLINE: s += "B-Spline: "; break;
                case POLYGON_BEZIER: s += "Bezier: "; break;
                case POLYGON_CYLINDER: s += "Cylinder/circle: ";break;
                default: s+= "Polygon: ";break;
            }
            s += "{ "; */

            for(int i=0; i<points.Count; i++)
            {
                Vect point = points[i];
                double pointType = pointTypes[i];

                if(pointType != POINT_BASEPOINT)
                {
                    //s += "C[";
                }
                s += point;

                if(pointType != POINT_BASEPOINT)
                {
                   // s += "]";
                }

                if (i == points.Count - 1)
                {
                    break;
                }
                s += " , ";
            }

            //s += " }";
            return s;
        }

        // get point type from curve type
        public static int GetPointType(GimElementType type)
        {
            switch (type)
            {
                case GimElementType.C_bezier: return POINT_CONTROL_BEZIER;
                case GimElementType.C_bspline: return POINT_CONTROL_BSPLINE;
                case GimElementType.C_circle: return POINT_CONTROL_CYLINDER;
                default: return POINT_CONTROL_BSPLINE;
            }
        }

        // inverts Bspline doubles in this fashion:
        // [knot1, ..., knotN, weight1, ..., weightN] => [knotN, ..., knot1, weightN, ..., weight1]
        public static double[] ReverseBsplineDoubles(int[] bsplineInts, double[] bsplineDoubles)
        {
            if (bsplineInts.Length == 0 ||bsplineDoubles.Length == 0) return bsplineDoubles;

            int degree = bsplineInts[^1];
            int knotsCount = bsplineInts[0];
            int multiplicitiesCount = bsplineInts.Length - 2;
            int polesCount = (bsplineDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

            double[] doubles = new double[knotsCount + polesCount];
            for (int i = 0; i < knotsCount; i++) // inversing the doubles of knots
            {
                doubles[i] = bsplineDoubles[knotsCount - 1 - i ];
            }

            for (int i = 0; i < polesCount; i++) // inversing the doubles of poles
            {
                doubles[i + knotsCount] = bsplineDoubles[polesCount - 1 - i + knotsCount];
            }

            return doubles;
        }

        // converts a list of points to the respective curves:
        public static List<GimElement> ConvertPointsToCurves(List<Vect> points, List<int> pointTypes, 
            List<int[]> bsplineInts, List<double[]> bsplineDoubles)
        {
            Vect previousBasePoint = points[0];
            int basePointIndex = 0;
            int i = 0;

            List<GimElement> curveElements = new();
            do
            {
                int pointType = POINT_BASEPOINT; // default assumed case is a line
                List<Vect> curvePoints = new List<Vect>() { previousBasePoint };
                List<int> curvePointTypes = new List<int>() { POINT_BASEPOINT };
                int[] curveInts = null;
                double[] curveDoubles = null;

                Vect nextBasePoint = null;
                for (i += 1; i < points.Count; i++) 
                {
                    curvePoints.Add(points[i]);
                    curvePointTypes.Add(pointTypes[i]);

                    if (pointTypes[i] == POINT_BASEPOINT)
                    {
                        nextBasePoint = points[i];
                        break;
                    }
                    pointType = pointTypes[i];
                }
                curveInts = bsplineInts[basePointIndex];
                curveDoubles = bsplineDoubles[basePointIndex];

                GimElement curveElement = null;

                switch (pointType)
                {
                    case POINT_CONTROL_BSPLINE: curveElement = BsplineToCurve(curvePoints, curvePointTypes, curveInts, curveDoubles); break;
                    case POINT_CONTROL_CYLINDER: curveElement = CylinderToCurve(curvePoints, curvePointTypes); break;
                    case POINT_BASEPOINT: curveElement = QuadrilateralToCurve(curvePoints, curvePointTypes); break;
                }

                curveElements.Add(curveElement);
                previousBasePoint = nextBasePoint;
                basePointIndex++;

            } while (i < points.Count - 1);

            return curveElements;
        }

        // static conversion methods, to convert a polygon to a given shape.
        public static GimElement QuadrilateralToCurve(List<Vect> pointList, List<int> pointTypes)
        {
            Vect firstPoint = pointList[0];
            Vect previousPoint = firstPoint;
            List<GimElement> curveElement = new();

            GimElement lineElement = new GimElement();
            GimLocal local = new GimLocal();
            lineElement.ElementType = GimElementType.C_line;

            Vect basisPoint = previousPoint;
            Vect lastPoint = pointList[^1]; // the last point is the end of the line

            Vect directionVector = (lastPoint - basisPoint).Direction;
            double param1 = 0;
            double param2 = (lastPoint - basisPoint) * directionVector; // scalar product

            lineElement.ElementDoubles = new double[] { param1, param2 };
            lineElement.ElementInts = Array.Empty<int>();
            directionVector.ApplyToLocal(local);

            local.Px = basisPoint[0];
            local.Py = basisPoint[1];
            local.Pz = basisPoint[2];

            lineElement.Locals = new() { local };


            ValueRounder.RoundElementDoubles(lineElement, Matrix.EQUALITY_ROUND_PRECISION);
            ValueRounder.RoundElementLocals(lineElement, Matrix.EQUALITY_ROUND_PRECISION);


            return lineElement;
        }

        public static GimElement BsplineToCurve(List<Vect> pointList, List<int> pointTypes, int[] bsplineInts, double[] bsplineDoubles)
        {

            // we construct the spline this way: first point -> control points -> end point.
            // If there are more splines, end point = start point of next spline
            Vect firstPoint = pointList[0];
            Vect lastPoint = pointList[^1];

            GimElement splineElement = new GimElement();
            GimLocal local = new GimLocal();
            splineElement.ElementType = GimElementType.C_bspline;

            // elementDoubles: [0]startX, [1]startY, [2]startZ, [3]endX, [4]endY, [5]endZ, [6]PoleA.X, [7]aParamFirst, [8]aParamLast [9]PoleA.Y, [10]PolyA.Z, [11]PoleB.X, [12]PoleB.Y, [13]PoleB.Z, [14]..[]KnotsU.A, []..[]KnotsU.B, ... []..[]KnotsV.A, []..[]KnotsV.B, ... 
            // elementInts: [0]NumberOfPoles, [1]Degree, [2]Number of Knots, [3]Number of Poles [4] Totoal number of multiplicities.... [5]Multiplicitie, [6...]Multiplicitie.B,  [m+1]Knots.A, [m+2]Knots.B, ... 

            splineElement.ElementDoubles = Array.Empty<double>();
            List<double> elementDoubles = new();
            splineElement.ElementInts = Array.Empty<int>();
            List<int> elementInts = new();

            // first point
            elementDoubles.Add(firstPoint[0]);
            elementDoubles.Add(firstPoint[1]);
            elementDoubles.Add(firstPoint[2]);

            // last point
            elementDoubles.Add(lastPoint[0]);
            elementDoubles.Add(lastPoint[1]);
            elementDoubles.Add(lastPoint[2]);


            for (int j = 1; j < pointList.Count - 1; j++)
            {
                Vect point = pointList[j];
                elementDoubles.Add(point[0]);
                elementDoubles.Add(point[1]);
                elementDoubles.Add(point[2]);

            }



            for(int i=0; i<bsplineDoubles.Length; i++)
            {
                elementDoubles.Add(bsplineDoubles[i]);
            }

            for(int i=0; i<bsplineInts.Length; i++)
            {
                elementInts.Add(bsplineInts[i]);
            }



            //Matrix localMatrix = new Matrix(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
            //localMatrix.ApplyToLocal(local);

            //local.Px = 0;
            //local.Py = 0;
            //local.Pz = 0;


            splineElement.Locals = new() { new GimLocal() };
            splineElement.ElementDoubles = elementDoubles.ToArray();
            splineElement.ElementInts = elementInts.ToArray();


            ValueRounder.RoundElementDoubles(splineElement, Matrix.EQUALITY_ROUND_PRECISION);
            ValueRounder.RoundElementLocals(splineElement, Matrix.EQUALITY_ROUND_PRECISION);


            return splineElement;
        }

        public static GimElement CylinderToCurve(List<Vect> pointList, List<int> pointTypes)
        {
            // a cylinder only has 3 points: Arc start, arc center, Arc end. these are used to construct the original arc.
            // a cylinder with a full circle has 4 points: Arc start, two intermediary Arc center points, arc end.
            int i = 0;
            Vect pointA = pointList[i];
            Vect arcCenterPoint = pointList[i + 1];
            Vect extraPoint = pointList[i + 2], pointB;

            if (pointTypes[i + 2] == POINT_CONTROL_BSPLINE)
            {
                pointB = pointList[i + 3];
                i = i + 3;
            }
            else
            {
                pointB = pointList[i + 2];
                i = i + 2;
            }



            Vect bisector1Normal = (arcCenterPoint - pointA).Direction;
            Vect bisector2Normal = (arcCenterPoint - pointB).Direction;

            Vect planeNormal = (bisector1Normal ^ bisector2Normal).Direction;

            Vect pointCenter = null;

            double radius = 0;

            if (planeNormal.Norm == 0) // the points are all aligned => circle is on the center of AB and.
            {
                pointCenter = (arcCenterPoint + pointA) / 2;
                radius = (pointA - pointCenter).Norm;
                Vect extraVect = (extraPoint - pointA).Direction;
                planeNormal = (bisector1Normal ^ extraVect).Direction;
            }
            else // points are not aligned, we have to find the center of the three points
            {
                Vect center1 = (arcCenterPoint + pointA) / 2;
                Vect center2 = (arcCenterPoint + pointB) / 2;

                // we attempt to find the plane bissectors, that cut the lines A-C and B-C in half. The circle lies on
                // their intersection.

                // if each plane is characterized by a normal vector (a0,b0,c0) and a point (x0,y0,z0) then its equation is

                // a0(x-x0) + b0(y-y0) + c0(z-z0) = 0

                // if we assume our point P = (x,y,z) is the center of the circle, then it belong to all three planes.
                // it then verifies the equation:
                //

                // a1(x-x1) + b1(y-y1) + c1(z-y1) = 0       a1*x + b1*y + c1*z = a1*x1 + b1*y1 + c1*z1
                // a2(x-x2) + b2(y-y2) + c2(z-z2) = 0  =>   a2*x + b2*y + c2*z = a2*x1 + b2*y1 + c2*z1
                // a3(x-x3) + b3(y-y3) + c3(z-z3) = 0       a3*x + b3*y + c3*z = a3*x1 + b3*y1 + c3*z1   

                // that resulting equation is of the form Matrix * Point = Point, so to find P = XYZ
                // we have to do Point = Matrix^(-1) * Vector

                Matrix intersectionMatrix = new Matrix(planeNormal, bisector1Normal, bisector2Normal);
                Vect offsetVect = new Vect(
                    planeNormal * arcCenterPoint,
                    bisector1Normal * center1,
                    bisector2Normal * center2);


                if (intersectionMatrix.Determinant == 0)
                {
                    throw GimLogger.Error(new ArgumentException("Illegal point coordinates in circle, are you sure this is a circle?"));
                }
                Matrix intersectionInverse = intersectionMatrix.Inverse;

                pointCenter = intersectionInverse * offsetVect;
                pointCenter = pointCenter.Round();

                radius = (pointA - pointCenter).Norm;
            }

            // now we have the center point and the radius, however we still need to get the two guiding vectors of the circle
            // for the circle's locals. 

            // TODO: take care of non righthanded stuff.

            // setting up the first orthogal base in the plane.
            Vect PlaneVectorA = (pointA - pointCenter).Direction;
            Vect PlaneVectorB = (planeNormal ^ PlaneVectorA).Direction;

            // Point 1 always has parameter 0.
            Vect pointAInPlane = pointA - pointCenter;
            Vect pointBInPlane = pointB - pointCenter;
            Vect arcPointInPlane = arcCenterPoint - pointCenter;

            // all these next steps are documented in TODO: document name

            double paramB = Math.Acos(pointBInPlane.Direction * PlaneVectorA);
            double Bside = Math.Abs(Math.Acos(pointBInPlane.Direction * PlaneVectorB));
            if (Bside > Math.PI / 2)
            {
                paramB = 2 * Math.PI - paramB;
            }

            double paramC = Math.Acos(arcPointInPlane.Direction * PlaneVectorA);
            double Cside = Math.Abs(Math.Acos(arcPointInPlane.Direction * PlaneVectorB));
            if (Cside > Math.PI / 2)
            {
                paramC = 2 * Math.PI - paramC;
            }

            // measuring ARC lengths to determine on which side the arc point is
            // this makes a big difference.
            double pi = Math.PI;

            double AB = Math.Acos(pointAInPlane.Direction * pointBInPlane.Direction);
            double AC = Math.Acos(arcPointInPlane.Direction * pointBInPlane.Direction);
            double BC = Math.Acos(arcPointInPlane.Direction * pointAInPlane.Direction);

            double paramStart, paramEnd;

            Vect localVectorA = null, localVectorB = null;

            if (AC + BC <= AB + EQUALITY_MARGIN) // short side of the arc
            {
                double arcLength = AB;
                if (paramB > pi) // B is on the lower half of the circle => arc is B->A
                {
                    localVectorA = pointBInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }
                else // B is n the upper half of the circle => arc is A=>B
                {
                    localVectorA = pointAInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }

                paramStart = 0;
                paramEnd = arcLength;
            }
            else // long side of the arc
            {
                double arcLength = (2 * pi - AB) % (2 * pi);
                if (paramB > pi) // B is on the lower half of the circle => arc is A->B
                {
                    localVectorA = pointAInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }
                else // B is n the upper half of the circle => arc is B->A
                {
                    localVectorA = pointBInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }
                paramStart = 0;
                paramEnd = arcLength;
            }

            if (paramEnd == paramStart) // full circle
            {
                paramEnd = 2 * pi;
            }

            GimLocal curveLocal = new GimLocal();
            Matrix circleMatrix = new Matrix(localVectorA, localVectorB, planeNormal);
            curveLocal.Px = pointCenter[0];
            curveLocal.Py = pointCenter[1];
            curveLocal.Pz = pointCenter[2];

            circleMatrix.ApplyToLocal(curveLocal);

            GimElement circleElement = new GimElement();
            circleElement.ElementType = GimElementType.C_circle;
            circleElement.ElementDoubles = new double[] { radius, paramStart, paramEnd };
            circleElement.ElementInts = Array.Empty<int>();
            circleElement.locals = new List<GimLocal> { curveLocal };

            ValueRounder.RoundElementDoubles(circleElement, Matrix.EQUALITY_ROUND_PRECISION);
            ValueRounder.RoundElementLocals(circleElement, Matrix.EQUALITY_ROUND_PRECISION);

            return circleElement;

        }
    }

    public static class ProfileDetector
    {
        // the basic idea, is to find all the elements with co-linear locals, group them in a list and assign 
        // the tuple Index as that local, then put all the tuples in one list for easy iteration later.

        private static List<int> checkedElements = new();

        // function call order
        // 1- DetectParallels
        // 2- Construct Polygons
        // 3- LinkPolygons
        // 4- FilterProfileCandidates
        // 5- EliminateConflictingProfiles
        // 5.5-? ELiminateProfilesWithUnmatchingFaces
        // 6- ConvertCandidatesIntoProfiles
        // 7- RemoveOriginalProfilePieces
        // 8- SimplifyProfileFaces
        // 9- OmitPerpendicularFaces

        public static List<int> GetShellElementsInList(GimData gimData, int shellIndex = 0) //divide the elements into parts and their subchildren
        {
            List<int> shellElements = new List<int>();
            bool found = false;
            int index = 0;

            for(int i=0; i<gimData.Elements.Count; i++)
            {
                GimElement element = gimData.Elements[i];
                if(element.ElementType == GimElementType.shell)
                {
                    if(index < shellIndex)
                    {
                        index++;
                        continue;
                    }
                    // at the beginning of each list is the parent part.
                    shellElements.Add(i);
                    found = true;
                    break;
                }
            }

            if(!found) return new List<int>();

            List<int> elementsToCheck = new List<int>() { shellElements[0] }; // we begin with the parent part
                                                                              // and we later expand 

            for (int i=0; i<shellElements.Count; i++)
            {
                

                for(int el=0; el<elementsToCheck.Count; el++)
                {
                    int elId = elementsToCheck[el];
                    GimElement element = gimData.Elements[elId];

                    if (element.Elements == null) continue;
                    foreach(int childId in element.Elements)
                    {
                        elementsToCheck.Add(childId);
                    }
                }
            }


            return elementsToCheck;
        }
        // this executes on the elements of the same part
        public static List<Tuple<Vect, List<GimElement> >> DetectParallels(GimData gimData, List<int> PartElements)
        {
            List<Tuple<Vect, List<GimElement>>> ParallelLines = new();

            foreach (int partElId in PartElements)
            {
                GimElement element = gimData.Elements[partElId];
                // if it's a plane, we looking for the directional vectors of said plane;
                if (element.ElementType == GimElementType.F_plane)
                {
                    // planes have an intermediary element which contains all the edges
                    GimElement planeMetaData = gimData.Elements[(int)element.Elements[0]];

                    // we iterate over the edges of the plane
                    foreach (uint elId in planeMetaData.Elements)
                    {
                        GimElement lineElement = gimData.Elements[(int)elId];
                        // skip non-lines, for now
                        if (lineElement.ElementType != GimElementType.C_line && lineElement.ElementType != GimElementType.C_line2D)
                            continue;

                        GimLocal lineLocal = lineElement.locals[0];
                        Vect lineVect = new Vect(lineLocal);

                        bool tupleFound = false;
                        // we check if a local with said coordinates exists
                        foreach (Tuple<Vect, List<GimElement>> T in ParallelLines)
                        {
                            // one of the tuples is coolinear to this line, we shove the line in there
                            if (T.Item1.IsCollinearTo(lineVect))
                            {
                                T.Item2.Add(lineElement);
                                tupleFound = true;
                                break;
                            }
                        }
                        // we found no tuple, we create one for futur use
                        if (!tupleFound)
                        {
                            Tuple<Vect, List<GimElement>> T = new(lineVect, new List<GimElement>() { lineElement });
                            ParallelLines.Add(T);
                        }

                    }

                }

                // if it's a Bspline, we are looking if said Bspline has lines that are perfectly linear
                else if (element.ElementType == GimElementType.F_bspline)
                {
                    // F_bsplines have an intermediary element which contains all the edges
                    GimElement splineMetaData = gimData.Elements[(int)element.Elements[0]];
                    foreach (uint elId in splineMetaData.Elements)
                    {
                        // we try to find perfectly (or VERY nearly* perfect) straight splines
                        // 
                        // *: this is necessary because of the imprecision of doubles, a spline can be flagged as
                        // 99.99997% flat
                        GimElement childElement = gimData.Elements[(int)elId];
                        if (childElement.ElementType != GimElementType.C_bspline && childElement.ElementType != GimElementType.C_line)
                            continue;

                        // first preliminary test: straightness or flatness of the curve
                        Vect directionVector;
                        bool tupleFound = false;

                        if (childElement.ElementType == GimElementType.C_bspline)
                        {
                            if (!CurveHelpers.checkSplineIsFlat(childElement)) continue;

                            // now, we check the alignment of the spline to see with which direction it is aligned
                            //
                            // to begin, we construct a line local from a spline. Since line locals only populate the
                            // ZX, ZY and ZZ values, and the spline local is off the form XX = 1, YY = 1, ZZ = 1,
                            // we have to construct a line local from a given spline. This is simply done by checking
                            // the direction of the spline (end - start), normalizing it and creating an equivalent
                            // line local.
                            directionVector = CurveHelpers.GetSplineVect(childElement, gimData);

                            // we check if a local with said coordinates exists
                            foreach (Tuple<Vect, List<GimElement>> T in ParallelLines)
                            {
                                // one of the tuples is coolinear to this line, we shove the line in there
                                if (T.Item1.IsCollinearTo(directionVector))
                                {
                                    // inserting the spline element at the start to see if our logic works
                                    T.Item2.Add(childElement);
                                    tupleFound = true;
                                    break;
                                }
                            }
                            // we found no tuple, we create one for futur use
                            if (!tupleFound)
                            {
                                Tuple<Vect, List<GimElement>> T = new(directionVector, new List<GimElement>() { childElement });
                                ParallelLines.Add(T);
                            }
                        }
                        if (childElement.ElementType == GimElementType.C_line) // in case of a line
                        {
                            GimLocal lineLocal = childElement.locals[0];
                            Vect lineVect = new Vect(lineLocal);
                            // we check if a local with said coordinates exists
                            foreach (Tuple<Vect, List<GimElement>> T in ParallelLines)
                            {
                                // one of the tuples is coolinear to this line, we shove the line in there
                                if (T.Item1.IsCollinearTo(lineVect))
                                {
                                    T.Item2.Add(childElement);
                                    tupleFound = true;
                                    break;
                                }
                            }
                            // we found no tuple, we create one for futur use
                            if (!tupleFound)
                            {
                                Tuple<Vect, List<GimElement>> T = new(lineVect, new List<GimElement>() { childElement });
                                ParallelLines.Add(T);
                            }

                        }

                    }
                }

                else if (element.ElementType == GimElementType.F_cilinder)
                {
                    // F_cylinders have an intermediary element which contains all the edges
                    GimElement cylinderWireElement = gimData.Elements[(int)element.Elements[0]];
                    foreach (uint elId in cylinderWireElement.Elements)
                    {
                        GimElement childElement = gimData.Elements[(int)elId];
                        if(childElement.ElementType == GimElementType.C_line) // in case of a line
                        {
                            GimLocal lineLocal = childElement.locals[0];
                            Vect lineVect = new Vect(lineLocal);

                            bool tupleFound = false;
                            // we check if a local with said coordinates exists
                            foreach (Tuple<Vect, List<GimElement>> T in ParallelLines)
                            {
                                // one of the tuples is coolinear to this line, we shove the line in there
                                if (T.Item1.IsCollinearTo(lineVect))
                                {
                                    T.Item2.Add(childElement);
                                    tupleFound = true;
                                    break;
                                }
                            }
                            // we found no tuple, we create one for futur use
                            if (!tupleFound)
                            {
                                Tuple<Vect, List<GimElement>> T = new(lineVect, new List<GimElement>() { childElement });
                                ParallelLines.Add(T);
                            }

                        }
                        else if (childElement.ElementType == GimElementType.C_bspline)
                        {
                            // first preliminary test: straightness or flatness of the curve

                            if (!CurveHelpers.checkSplineIsFlat(childElement)) continue;

                            // now, we check the alignment of the spline to see with which direction it is aligned

                            Vect splineVector = CurveHelpers.GetSplineVect(childElement, gimData);

                            bool tupleFound = false;
                            // we check if a local with said coordinates exists
                            foreach (Tuple<Vect, List<GimElement>> T in ParallelLines)
                            {
                                // one of the tuples is coolinear to this line, we shove the line in there
                                if (T.Item1.IsCollinearTo(splineVector))
                                {
                                    // inserting the spline element at the start to see if our logic works
                                    T.Item2.Add(childElement);
                                    tupleFound = true;
                                    break;
                                }
                            }
                            // we found no tuple, we create one for futur use
                            if (!tupleFound)
                            {
                                Tuple<Vect, List<GimElement>> T = new(splineVector, new List<GimElement>() { childElement });
                                ParallelLines.Add(T);
                            }
                        }
                    }
                }
            }

            return ParallelLines;
        }

        // after grouping all the shapes into Tuples
        // it's important to call this function after the above one

        public static List<Tuple<Vect, List<PolygonRepresentation>>> ConstructPolygons(GimData gimData, List<Tuple<Vect, List<GimElement>>> ParallelLines)
        {
            List<Tuple<Vect, List<PolygonRepresentation>>> PolygonTuples = new();
            char letterCounter = 'A';
            // we iterate the tuples
            foreach (Tuple<Vect, List<GimElement>> tuple in ParallelLines)
            {
                List<GimElement> elementList = tuple.Item2;
                Vect direction = tuple.Item1;

                // will contain the list of elements we checked.
                // we can check a face multiple times along different orientations, but once per orientation
                checkedElements = new();

                List<PolygonRepresentation> polygonList = new();
                foreach(GimElement element in elementList)
                {
                    // this is the "undefined" element that contains the sub elements
                    GimElement containerElement = gimData.Elements[(int)element.Locals[0].Parent];
                    // this is the actual parent, that contains the elementType
                    GimElement elementParent = gimData.Elements[(int)containerElement.Locals[0].Parent];
                    int parentId = (int)containerElement.Locals[0].Parent;

                    if (checkedElements.Contains(parentId)) // we already checked this element, jump to the next
                    {
                        continue;
                    }
                    else // we didn't check this element, but mark it as checked
                    {
                        checkedElements.Add(parentId);
                    }

                    PolygonRepresentation polygon = null;

                    switch(elementParent.ElementType) // if the parent is a plane
                    {
                        case GimElementType.F_plane: polygon = planeToPolygon(gimData, polygon, containerElement, direction); break;
                        case GimElementType.F_bspline: polygon = BsplineToPolygon(gimData, polygon, containerElement, direction);break;
                        case GimElementType.F_cilinder: polygon = CylinderToPolygon(gimData, polygon, containerElement, direction);break;
                    }

                    if (polygon != null) // if the polygon is not null, it's valid
                    {
                        polygonList.Add(polygon);
                        polygon.Name = (letterCounter++).ToString(); // temoprary, for debugging
                    }

                }
                // we group all polygons by their direction
                Tuple<Vect, List<PolygonRepresentation>> T = new(direction, polygonList);
                PolygonTuples.Add(T);
            }

            return PolygonTuples;
        }

        // the polygons constructed above are going to get linked
        public static List<Tuple<Vect, List<PolygonRepresentation>>> LinkPolygons(List<Tuple<Vect, List<PolygonRepresentation>>> PolygonTuples)
        {
            List<Tuple<Vect, List<PolygonRepresentation>>> LinkedPolygonTuples = new();
            foreach(Tuple<Vect, List<PolygonRepresentation>> tuple in PolygonTuples)
            {
                List<PolygonRepresentation> polygonList = tuple.Item2;
                Vect direction = tuple.Item1;
                if (polygonList.Count == 0) continue;

                for(int i=0; i<polygonList.Count; i++)
                {
                    PolygonRepresentation polygonA = polygonList[i];
                    // we check all polygons that are connected into this one
                    for(int j = i+1; j < polygonList.Count; j++)
                    {
                        PolygonRepresentation polygonB = polygonList[j];
                        if (polygonA.SharesEdgeWith(polygonB))
                        {
                            polygonA.LinkPolygon(polygonB);
                        }
                    }

                }
                List<PolygonRepresentation> linkedPolygonList = new();
                for (int i = 0; i < polygonList.Count; i++)
                {
                    PolygonRepresentation polygon = polygonList[i];
                    // if the polygon is not backwards-linked (no element points to it), it's the start of a chain.
                    if (!polygon.Linked || polygon.IsStart)
                    {
                        // this function makes sure all linked polygons are either circular, or have one start and one end.
                        polygon.UnwindLinks();
                        linkedPolygonList.Add(polygon);
                    }
                }

                Tuple<Vect, List<PolygonRepresentation>> T = new(direction, linkedPolygonList);
                LinkedPolygonTuples.Add(T);

            }

            return LinkedPolygonTuples;
        }

        // this functions filters out false-profiles
        public static List<PolygonRepresentation> FilterProfileCandidates
            (List<Tuple<Vect, List<PolygonRepresentation>>> PolygonTuples)
        {
            List<PolygonRepresentation> ProfileCandidates = new();
            foreach(var polygonGroup in PolygonTuples)
            {
                List<PolygonRepresentation> polygonLinks = polygonGroup.Item2;
                foreach(PolygonRepresentation polygonLink in polygonLinks)
                {
                    if(polygonLink.IsStart && polygonLink.Linked) //fully linked polygonlist => profile candidate
                    {
                        ProfileCandidates.Add(polygonLink);
                    }

                }
            }
            return ProfileCandidates;
        }

        public static List<PolygonRepresentation> EliminateConflictingProfiles(List<PolygonRepresentation> ProfileCandidates)
        {
            for(int i = 0; i < ProfileCandidates.Count-1; i++) // we can't construct profiles when the profile candidates share pieces
                                                             // mostly a major concern because of cubes and cubic/rectangular shapes.
            {
                var currentProfile = ProfileCandidates[i];

                for(int j= i+1; j< ProfileCandidates.Count; j++) // we check all the profiles after our profile
                {

                    var otherProfile = ProfileCandidates[j];
                    bool sharesPieces = false;

                    PolygonRepresentation currentPolygon = currentProfile;

                    do
                    {
                        PolygonRepresentation currentOtherPolygon = otherProfile;
                        do
                        {
                            if(currentOtherPolygon.originalElement == currentPolygon.originalElement) // we check if they share pieces
                            {
                                sharesPieces = true;
                                break;
                            }
                            if (currentOtherPolygon.Next.Count == 0) break; // next = null break
                            if (currentOtherPolygon.Next[0] == currentOtherPolygon) break; // loop break
                            currentOtherPolygon = currentOtherPolygon.Next[0];
                            if (currentOtherPolygon == otherProfile) break; // next = start break

                        } while (currentOtherPolygon.Next != null && currentOtherPolygon.Next.Count > 0);

                        if (sharesPieces)
                        {
                            break;
                        }
                        if (currentPolygon.Next.Count == 0) break; // next = null break
                        if (currentPolygon.Next[0] == currentPolygon) break; // loop break
                        currentPolygon = currentPolygon.Next[0];
                        if (currentPolygon == currentProfile) break; // next = start break
                    } while (currentPolygon.Next != null && currentPolygon.Next.Count > 0);

                    if (sharesPieces)
                    {
                        ProfileCandidates.RemoveAt(j); // we remove the offending profile
                        j--;
                    }

                }

            }

            return ProfileCandidates;

        }

        // this function eliminates profiles whos faces don't have matching pieces. These profiles are complicated to rebuild back.
        // probably temporary.
        public static List<PolygonRepresentation> EliminateProfilesWithUnmatchingFaces(List<PolygonRepresentation> ProfileCandidates)
        {

            for(int profileIndex=0; profileIndex<ProfileCandidates.Count; profileIndex++)
            {
                PolygonRepresentation profile = ProfileCandidates[profileIndex];
                var profileFaces = profile.GetBothFacePoints();
                List<Vect> faceAPoints = profileFaces.Item1[0];
                List<Vect> faceBPoints = profileFaces.Item1[1];
                List<int> faceATypes = profileFaces.Item2[0];
                List<int> faceBTypes = profileFaces.Item2[1];

                Vect profileDirection = profile.Direction;

                bool match = true;

                int j = 0; // optimization trick. reduces average complexity to o(n) rather o(n^2)
                int lastChecked = 0;
                for(int i=0; i<faceAPoints.Count; i++) // every base point in A has to have an equivalent in B
                {
                    Vect point = faceAPoints[i];
                    int type = faceATypes[i];
                    if (type != PolygonRepresentation.POINT_BASEPOINT) continue;

                    bool hasMatch = false;
                    do
                    {
                        Vect otherPoint = faceBPoints[j];
                        int otherType = faceBTypes[j];

                        if (otherType == PolygonRepresentation.POINT_BASEPOINT)
                        {
                            Vect line = otherPoint - point;
                            if (line.IsCollinearTo(profileDirection))
                            { // we found a match for the point
                                hasMatch = true;
                                break;
                            }
                        }

                        j++;
                        j = j % faceBPoints.Count;
                    } while (j != lastChecked);

                    if (!hasMatch)
                    { // we didn't find a match for the point => problematic profile, best to remove it.
                        match = false;
                        break;
                    }

                    lastChecked = j; // the purpose of this is that points are most likely close to each other, no need to iterate
                                    // from the start every time. but rather, continue from where we found the last match
                }

                // one or more points in face A didn't have a match
                if (!match)
                {
                    ProfileCandidates.RemoveAt(profileIndex);
                    profileIndex--;
                    continue;
                }

                // now we check face B

                match = true;

                j = 0; // optimization trick. reduces average complexity to o(n) rather o(n^2)
                lastChecked = 0;
                for (int i = 0; i < faceBPoints.Count; i++) // every base point in B has to have an equivalent in A
                {
                    Vect point = faceBPoints[i];
                    int type = faceBTypes[i];
                    if (type != PolygonRepresentation.POINT_BASEPOINT) continue;

                    bool hasMatch = false;
                    do
                    {
                        Vect otherPoint = faceAPoints[j];
                        int otherType = faceATypes[j];

                        if (otherType == PolygonRepresentation.POINT_BASEPOINT)
                        {
                            Vect line = otherPoint - point;
                            if (line.IsCollinearTo(profileDirection))
                            { // we found a match for the point
                                hasMatch = true;
                                break;
                            }
                        }

                        j++;
                        j = j % faceAPoints.Count;
                    } while (j != lastChecked);

                    if (!hasMatch)
                    { // we didn't find a match for the point => problematic profile, best to remove it.
                        match = false;
                        break;
                    }

                    lastChecked = j;
                }

                // one or more points in face B didn't have a match
                if (!match)
                {
                    ProfileCandidates.RemoveAt(profileIndex);
                    profileIndex--;
                    continue;
                }


            }

            return ProfileCandidates;
        }

        // this is where candidates start getting converted into profiles.
        public static List<Tuple<List<PolygonRepresentation>, int>> ConvertCandidatesIntoProfiles(List<PolygonRepresentation> ProfileCandidates, GimData gimData)
        {

            List<Tuple<List<PolygonRepresentation>, int>> ProfileTuples = new List<Tuple<List<PolygonRepresentation>, int>>();

            for(int i=0; i<ProfileCandidates.Count; i++)
            {



                // the profile element, parent to all the other elements
                PolygonRepresentation profilePolygons = ProfileCandidates[i];
                GimElement profileElement = new GimElement();
                profileElement.elementType = GimElementType.Gim_ProfileInstance;
                int profileElementIndex = gimData.Elements.Count;
                gimData.Elements.Add(profileElement);

                // cut section element
                GimElement cutSectionElement = new GimElement();
                //cutSectionElement.elementType = GimElementType.Gim_ProfileDefinition;
                int cutSectionElementIndex = gimData.Elements.Count;
                GimLocal cutSectionLocal = new GimLocal();
                cutSectionLocal.parent = (uint)profileElementIndex;
                cutSectionElement.locals.Add(cutSectionLocal);
                gimData.Elements.Add(cutSectionElement);

                // poly line element
                GimElement polyLineElement = new GimElement();
                polyLineElement.elementType = GimElementType.P_polyline;
                int polyLineElementIndex = gimData.Elements.Count;
                GimLocal polyLineLocal = new GimLocal();
                polyLineLocal.parent = (uint)profileElementIndex;
                polyLineElement.locals.Add(polyLineLocal);
                gimData.Elements.Add(polyLineElement);
                
                // cut surface A ( face A ), needed later
                GimElement cutSurfaceElementA = new GimElement();
                //cutSurfaceElementA.ElementType = GimElementType.Gim_CutSurface;
                int cutSurfaceElementAIndex = gimData.Elements.Count;
                GimLocal cutSurfaceALocal = new GimLocal();
                cutSurfaceALocal.parent = (uint) profileElementIndex;
                cutSurfaceElementA.locals.Add(cutSurfaceALocal);
                gimData.Elements.Add(cutSurfaceElementA);

                // cut surface A child, type will be specified later
                GimElement cutSurfaceAContainerElement = new GimElement();
                int cutSurfaceAContainerIndex = gimData.Elements.Count;
                GimLocal cutSurfaceAContainerLocal = new GimLocal();
                cutSurfaceAContainerLocal.parent = (uint)cutSurfaceElementAIndex;
                cutSurfaceAContainerElement.Locals.Add(cutSurfaceAContainerLocal);
                gimData.Elements.Add(cutSurfaceAContainerElement);
                cutSurfaceElementA.Elements.Add((uint)cutSurfaceAContainerIndex);

                // cut surface A wire
                GimElement cutSurfaceAWire = new GimElement();
                cutSurfaceAWire.ElementType = GimElementType.wire;
                int cutSurfaceAWireIndex = gimData.Elements.Count;
                GimLocal cutSurfaceAWireLocal = new GimLocal();
                cutSurfaceAWireLocal.parent = (uint)cutSurfaceAContainerIndex;
                cutSurfaceAWire.Locals.Add(cutSurfaceAWireLocal);
                gimData.Elements.Add(cutSurfaceAWire);
                cutSurfaceAContainerElement.Elements.Add((uint)cutSurfaceAWireIndex);

                // cut surface B ( face B ), needed later
                GimElement cutSurfaceElementB = new GimElement();
                //cutSurfaceElementB.elementType = GimElementType.Gim_CutSurface;
                int cutSurfaceElementBIndex = gimData.Elements.Count;
                GimLocal cutSurfaceBLocal = new GimLocal();
                cutSurfaceBLocal.parent = (uint)profileElementIndex;
                cutSurfaceElementB.locals.Add(cutSurfaceBLocal);
                gimData.Elements.Add(cutSurfaceElementB);

                // cut surface B child, type will be specified later
                GimElement cutSurfaceBContainerElement = new GimElement();
                int cutSurfaceBContainerIndex = gimData.Elements.Count;
                GimLocal cutSurfaceBContainerLocal = new GimLocal();
                cutSurfaceBContainerLocal.parent = (uint)cutSurfaceElementBIndex;
                cutSurfaceBContainerElement.Locals.Add(cutSurfaceBContainerLocal);
                gimData.Elements.Add(cutSurfaceBContainerElement);
                cutSurfaceElementB.Elements.Add((uint)cutSurfaceBContainerIndex);

                // cut surface B wire
                GimElement cutSurfaceBWire = new GimElement();
                cutSurfaceBWire.ElementType = GimElementType.wire;
                int cutSurfaceBWireIndex = gimData.Elements.Count;
                GimLocal cutSurfaceBWireLocal = new GimLocal();
                cutSurfaceBWireLocal.parent = (uint)cutSurfaceBContainerIndex;
                cutSurfaceBWire.Locals.Add(cutSurfaceBWireLocal);
                gimData.Elements.Add(cutSurfaceBWire);
                cutSurfaceBContainerElement.Elements.Add((uint)cutSurfaceBWireIndex);

                // we append the children of profile to it
                profileElement.Elements.Add((uint)cutSectionElementIndex);
                profileElement.Elements.Add((uint)polyLineElementIndex);
                profileElement.Elements.Add((uint)cutSurfaceElementAIndex);
                profileElement.Elements.Add((uint)cutSurfaceElementBIndex);

                // element ints of profile, the first int signifies whether face elements are omitted or taken into account
                profileElement.ElementInts = new int[] { 0 };

                // polypolygonelement
                GimElement polyPolygonElement = new GimElement();
                // This used to be elementtype PolyPolyGon
                polyPolygonElement.elementType = GimElementType.Dimension;
                int polyPolygonElementIndex = gimData.Elements.Count;
                GimLocal polyPolygonLocal = new GimLocal();
                polyPolygonLocal.parent = (uint)cutSectionElementIndex;
                polyPolygonElement.locals.Add(polyPolygonLocal);
                gimData.Elements.Add(polyPolygonElement);

                cutSectionElement.Elements.Add((uint)polyPolygonElementIndex);

                //singlePolygonElement
                GimElement singlePolygonElement = new GimElement();
                //singlePolygonElement.elementType = GimElementType.Gim_SinglePolygon;
                int singlePolygonElementIndex = gimData.Elements.Count;
                GimLocal singlePolygonLocal = new GimLocal();
                singlePolygonLocal.parent = (uint)polyPolygonElementIndex;
                singlePolygonElement.locals.Add(singlePolygonLocal);
                gimData.Elements.Add(singlePolygonElement);

                polyPolygonElement.Elements.Add((uint)singlePolygonElementIndex);
                
                // we will now convert the cutsection of the shape into curve elements
                List<PolygonRepresentation> cutSection = profilePolygons.GetCutSectionAsPolygons();


                for (int x=0; x<cutSection.Count; x++)
                {
                    var polygon = cutSection[x];
                    var polygonElements = polygon.GetSimplePolygonAsCurveElements();

                    if (polygonElements.Count == 0) 
                        continue;

                    if(polygonElements.Count > 1) // if we have a complex polygon for a single face, we create a complexpolygon grouping for it
                    {
                        GimElement polygonSegmentElement = new GimElement();
                        polygonSegmentElement.ElementType = GimElementType.Gim_ComplexSegment;
                        int polygonSegmentElementIndex = gimData.Elements.Count;
                        GimLocal polygonSegmentLocal = polygon.faceLocal.Clone();
                        polygonSegmentLocal.parent = (uint)singlePolygonElementIndex;
                        polygonSegmentElement.locals.Add(polygonSegmentLocal);
                        gimData.Elements.Add(polygonSegmentElement);

                        singlePolygonElement.Elements.Add((uint)polygonSegmentElementIndex);

                        foreach (var polygonElement in polygonElements)
                        {
                            int polygonElementIndex = gimData.Elements.Count;
                            gimData.Elements.Add(polygonElement);
                            polygonSegmentElement.Elements.Add((uint)polygonElementIndex);
                            polygonElement.Locals[0].Parent = (uint)polygonSegmentElementIndex;
                        }

                        polygonSegmentElement.ElementDoubles = polygon.faceDoubles;
                        polygonSegmentElement.ElementInts = polygon.faceInts;
                    }   
                    else // else if it's a simple element, we just add it directly.
                    {
                        GimElement polygonElement = polygonElements[0];

                        int polygonElementIndex = gimData.Elements.Count;
                        gimData.Elements.Add(polygonElement);
                        singlePolygonElement.Elements.Add((uint)polygonElementIndex);
                        polygonElement.Locals[0].Parent = (uint)singlePolygonElementIndex;
                    }

                }

                // after saving the cutsection we will now save the face A and face B

                var facePolys = profilePolygons.GetFacesAsPolygons();
                List<PolygonRepresentation> faceAPolys = facePolys[0];
                List<PolygonRepresentation> faceBPolys = facePolys[1];

                // saving Face A elements. If these are simple, they will be simplified later
                for (int x = 0; x < faceAPolys.Count; x++)
                {
                    var polygon = faceAPolys[x];
                    var polygonElements = polygon.GetSimplePolygonAsCurveElements();

                    //foreach (var polygonElement in polygonElements)
                    //{
                    //    int polygonElementIndex = gimData.Elements.Count;
                    //    gimData.Elements.Add(polygonElement);
                    //    cutSurfaceAWire.Elements.Add((uint)polygonElementIndex);
                    //    polygonElement.Locals[0].Parent = (uint)cutSurfaceAWireIndex;
                    //}

                    if (polygonElements.Count == 0)
                        continue;

                    if (polygonElements.Count > 1) // if we have a complex polygon for a single face, we create a complexpolygon grouping for it
                    {
                        GimElement polygonSegmentElement = new GimElement();
                        polygonSegmentElement.ElementType = GimElementType.Gim_ComplexSegment;
                        int polygonSegmentElementIndex = gimData.Elements.Count;
                        GimLocal polygonSegmentLocal = polygon.faceLocal.Clone();
                        polygonSegmentLocal.parent = (uint)cutSurfaceAWireIndex;
                        polygonSegmentElement.locals.Add(polygonSegmentLocal);
                        gimData.Elements.Add(polygonSegmentElement);

                        cutSurfaceAWire.Elements.Add((uint)polygonSegmentElementIndex);

                        foreach (var polygonElement in polygonElements)
                        {
                            int polygonElementIndex = gimData.Elements.Count;
                            gimData.Elements.Add(polygonElement);
                            polygonSegmentElement.Elements.Add((uint)polygonElementIndex);
                            polygonElement.Locals[0].Parent = (uint)polygonSegmentElementIndex;
                        }

                        polygonSegmentElement.ElementDoubles = polygon.faceDoubles;
                        polygonSegmentElement.ElementInts = polygon.faceInts;
                    }
                    else // else if it's a simple element, we just add it directly.
                    {
                        GimElement polygonElement = polygonElements[0];

                        int polygonElementIndex = gimData.Elements.Count;
                        gimData.Elements.Add(polygonElement);
                        cutSurfaceAWire.Elements.Add((uint)polygonElementIndex);
                        polygonElement.Locals[0].Parent = (uint)cutSurfaceAWireIndex;
                    }
                }

                // saving Face B elements. If these are simple, they will be simplified later
                for (int x = 0; x < faceBPolys.Count; x++)
                {
                    var polygon = faceBPolys[x];
                    var polygonElements = polygon.GetSimplePolygonAsCurveElements();

                    //foreach (var polygonElement in polygonElements)
                    //{
                    //    int polygonElementIndex = gimData.Elements.Count;
                    //    gimData.Elements.Add(polygonElement);
                    //    cutSurfaceBWire.Elements.Add((uint)polygonElementIndex);
                    //    polygonElement.Locals[0].Parent = (uint)cutSurfaceBWireIndex;
                    //}

                    if (polygonElements.Count == 0)
                        continue;

                    if (polygonElements.Count > 1) // if we have a complex polygon for a single face, we create a complexpolygon grouping for it
                    {
                        GimElement polygonSegmentElement = new GimElement();
                        polygonSegmentElement.ElementType = GimElementType.Gim_ComplexSegment;
                        int polygonSegmentElementIndex = gimData.Elements.Count;
                        GimLocal polygonSegmentLocal = polygon.faceLocal.Clone();
                        polygonSegmentLocal.parent = (uint)cutSurfaceBWireIndex;
                        polygonSegmentElement.locals.Add(polygonSegmentLocal);
                        gimData.Elements.Add(polygonSegmentElement);

                        cutSurfaceBWire.Elements.Add((uint)polygonSegmentElementIndex);

                        foreach (var polygonElement in polygonElements)
                        {
                            int polygonElementIndex = gimData.Elements.Count;
                            gimData.Elements.Add(polygonElement);
                            polygonSegmentElement.Elements.Add((uint)polygonElementIndex);
                            polygonElement.Locals[0].Parent = (uint)polygonSegmentElementIndex;
                        }

                        polygonSegmentElement.ElementDoubles = polygon.faceDoubles;
                        polygonSegmentElement.ElementInts = polygon.faceInts;
                    }
                    else // else if it's a simple element, we just add it directly.
                    {
                        GimElement polygonElement = polygonElements[0];

                        int polygonElementIndex = gimData.Elements.Count;
                        gimData.Elements.Add(polygonElement);
                        cutSurfaceBWire.Elements.Add((uint)polygonElementIndex);
                        polygonElement.Locals[0].Parent = (uint)cutSurfaceBWireIndex;
                    }
                }


                // now we determine if the start and end surface are slanted or perpendicular to the profile direction
                Vect profileDirection = profilePolygons.Direction.Direction; // the direction of the profile, perpendicular to the cutsection.
                // guiding vectors of the cutsection plane
                Vect cutSectionVector1 = profileDirection ^ (new Vect(1, 0, 0));
                if (cutSectionVector1.Norm < PolygonRepresentation.EQUALITY_MARGIN)
                {
                    cutSectionVector1 = profileDirection ^ (new Vect(0, 1, 0));
                    cutSectionVector1 = cutSectionVector1.Direction;
                }
                Vect cutSectionVector2 = profileDirection ^ cutSectionVector1;
                cutSectionVector2 = cutSectionVector2.Direction;
                // a point that lies on the cutsection
                Vect cutSectionPoint = cutSection[0].Points[0];

                var facesData = profilePolygons.GetBothFacePoints();
                List<Vect> faceAPoints = facesData.Item1[0];
                List<Vect> faceBPoints = facesData.Item1[1];

                bool faceAPerpendicular = true, faceBPerpendicular = true;
                bool faceAPlanar = true, faceBPlanar = true;

                // determining whether faceA is perpendicular to the profile or not
                Vect faceACenterPoint = Vect.GetAveragePoint(faceAPoints);
                for(int x=1; x<faceAPoints.Count; x++)
                {
                    Vect point = faceAPoints[x];
                    Vect direction = point - faceACenterPoint;
                    if( Math.Abs(direction * profileDirection) > PolygonRepresentation.EQUALITY_MARGIN)
                    {
                        faceAPerpendicular = false;
                        break;
                    }
                }

                // checking if face A is planar

                // calculating the vectors
                int ind = 1;
                Vect faceAVect1;
                do
                {
                    faceAVect1 = faceAPoints[0] - faceAPoints[ind];
                    ind++;
                } while (ind < faceAPoints.Count && faceAVect1.Norm < PolygonRepresentation.EQUALITY_MARGIN * 100);

                faceAVect1 = faceAVect1.Direction;

                Vect faceANormal = null;
                int j = 2;
                Vect faceAVect2;
                do
                {
                    faceAVect2 = faceAPoints[0] - faceAPoints[j];
                    faceANormal = faceAVect1 ^ faceAVect2;
                    j++;
                } while (j < faceAPoints.Count && faceANormal.Norm == 0);

                faceAVect2 = faceAVect2.Direction;
                faceANormal = faceANormal.Direction;

                // checking flatness

            for (int x = 1; x < faceAPoints.Count; x++)
                {
                    Vect point = faceAPoints[x];
                    Vect direction = point - faceACenterPoint;
                    if (Math.Abs(direction * faceANormal) > PolygonRepresentation.EQUALITY_MARGIN)
                    {
                        faceAPlanar = false;
                        break;
                    }
                }


                if (!faceAPlanar) // TODO: more complicated cutsections (cylinders, spheres etc)
                {
                    cutSurfaceAContainerElement.ElementType = GimElementType.F_bspline;
                }
                else
                {
                    cutSurfaceAContainerElement.ElementType = GimElementType.F_plane;
                    faceAVect1 = faceAVect1.Direction;
                    faceAVect2 = faceAVect2 - (faceAVect2 * faceAVect1) * faceAVect1;
                    Matrix planeMatrix = new Matrix(faceAVect1.Direction, faceAVect2.Direction, faceANormal.Direction);
                    planeMatrix.ApplyToLocal(cutSurfaceAContainerElement.Locals[0]);
                }
                // distance definition using scalars => results in negative distances
                double faceADistance = (faceACenterPoint - cutSectionPoint) * profileDirection;
                cutSurfaceAContainerElement.Locals[0].Px = faceACenterPoint[0];
                cutSurfaceAContainerElement.Locals[0].Py = faceACenterPoint[1];
                cutSurfaceAContainerElement.Locals[0].Pz = faceACenterPoint[2];

                Vect faceBCenterPoint = Vect.GetAveragePoint(faceBPoints);
                for (int x = 1; x < faceBPoints.Count; x++)
                {
                    Vect point = faceBPoints[x];
                    Vect direction = point - faceBCenterPoint;
                    if (Math.Abs(direction * profileDirection) > PolygonRepresentation.EQUALITY_MARGIN)
                    {
                        faceBPerpendicular = false;
                        break;
                    }
                }

                ind = 1;
                Vect faceBVect1;
                do
                {
                    faceBVect1 = faceBPoints[0] - faceBPoints[ind];
                    ind++;
                } while (ind < faceBPoints.Count && faceBVect1.Norm == 0);

                faceBVect1 = faceAVect1.Direction;

                Vect faceBNormal = null;
                j = 2;
                Vect faceBVect2;
                do
                {
                    faceBVect2 = faceBPoints[0] - faceBPoints[j];
                    faceBNormal = faceBVect1 ^ faceBVect2;
                    j++;
                } while (j < faceBPoints.Count && faceBNormal.Norm == 0);


                faceBVect2 = faceAVect2.Direction;
                faceBNormal = faceANormal.Direction;

                // checking flatness

                for (int x = 1; x < faceBPoints.Count; x++)
                {
                    Vect point = faceBPoints[x];
                    Vect direction = point - faceBCenterPoint;
                    if (Math.Abs(direction * faceBNormal) > PolygonRepresentation.EQUALITY_MARGIN)
                    {
                        faceBPlanar = false;
                        break;
                    }
                }

                if (!faceBPlanar) // TODO: more complicated cutsections (cylinders, spheres etc)
                {
                    cutSurfaceBContainerElement.ElementType = GimElementType.F_bspline;
                }
                else
                {
                    cutSurfaceBContainerElement.ElementType = GimElementType.F_plane;
                    faceBVect1 = faceBVect1.Direction;
                    faceBVect2 = faceBVect2 - (faceBVect2 * faceBVect1) * faceBVect1;
                    Matrix planeMatrix = new Matrix(faceBVect1.Direction, faceBVect2.Direction, faceBNormal.Direction);
                    planeMatrix.ApplyToLocal(cutSurfaceBContainerElement.Locals[0]);
                }
                // distance definition using scalars => results in negative distances
                double faceBDistance = (faceBCenterPoint - cutSectionPoint) * profileDirection;
                cutSurfaceBContainerElement.Locals[0].Px = faceBCenterPoint[0];
                cutSurfaceBContainerElement.Locals[0].Py = faceBCenterPoint[1];
                cutSurfaceBContainerElement.Locals[0].Pz = faceBCenterPoint[2];

                // lineElement for the direction of the profile
                GimElement profileDirectionLineElement = new GimElement();
                profileDirectionLineElement.ElementType = GimElementType.C_line;
                GimLocal lineLocal = new GimLocal();
                profileDirectionLineElement.Locals.Add(lineLocal);
                lineLocal.parent = (uint)polyLineElementIndex;
                int profileDirectionLineElementIndex = gimData.Elements.Count;
                gimData.Elements.Add(profileDirectionLineElement);
                polyLineElement.Elements.Add((uint)profileDirectionLineElementIndex);
                profileDirection.ApplyToLocal(lineLocal);
                profileDirectionLineElement.ElementDoubles = new double[] { faceADistance, faceBDistance };

                profileDirectionLineElement.Locals[0].Px = cutSectionPoint[0];
                profileDirectionLineElement.Locals[0].Py = cutSectionPoint[1];
                profileDirectionLineElement.Locals[0].Pz = cutSectionPoint[2];

                // setting the parent of the profile to the original parent of the pieces.
                GimElement sourceElement = gimData.Elements[profilePolygons.originalElement];
                GimLocal profileElementLocal = new GimLocal();
                profileElementLocal.parent = sourceElement.locals[0].parent;
                GimElement parentElement = gimData.Elements[(int)sourceElement.locals[0].parent];
                parentElement.Elements.Add((uint)profileElementIndex);

                // the list of polygon chains used for this profile.
                List<PolygonRepresentation> profilePolygonList = new List<PolygonRepresentation>() { profilePolygons };

                for (j = i + 1; j < ProfileCandidates.Count; j++) // we are now verifying if the other profiles are a part of this profile
                {
                    PolygonRepresentation otherProfile = ProfileCandidates[j];
                    // if the profile direction is different no need to check.
                    if (!otherProfile.Direction.IsCollinearTo(profileDirection)) continue;
                    var otherProfileFaces = otherProfile.GetBothFacePoints();
                    List<Vect> otherFaceAPoints = otherProfileFaces.Item1[0];
                    List<Vect> otherFaceBPoints = otherProfileFaces.Item1[1];

                    var result = CheckProfilesOverlap(faceAPoints, faceBPoints, otherFaceAPoints, otherFaceBPoints, profileDirection);

                    bool contained = result.Item1;
                    bool firstProfileIsBigger = result.Item2;
                    bool inversed = result.Item3;

                   

                    if(contained) //AinA and BinB means the profile is contained within the bigger one
                                                        // and in the same order. AinB and BinA means the profile is contained within
                                                        // the bigger one and in an oppositeOrder (=> we project the cutsection onto faceB)
                    {
                        // we add the cutsection of this element as an additional SinglePolygon in the polypolygon cutsection
                        GimElement additionalSinglePolygonalElement = new GimElement();
                        //additionalSinglePolygonalElement.ElementType = GimElementType.Gim_SinglePolygon;
                        int additionalSinglePolygonalIndex = gimData.Elements.Count;
                        gimData.Elements.Add(additionalSinglePolygonalElement);

                        // if we found a new element, 
                        if (firstProfileIsBigger) polyPolygonElement.Elements.Add((uint)additionalSinglePolygonalIndex);
                        else polyPolygonElement.Elements.Insert(0, (uint)additionalSinglePolygonalIndex);
                        GimLocal local = new GimLocal();
                        local.parent = (uint)polyPolygonElementIndex;
                        additionalSinglePolygonalElement.Locals.Add(local);

                        // we get the cutsection of the other polygon. if AinB && BinA, the cutsection must be projected onto the other face.
                        List<PolygonRepresentation> otherProfileCutSection = otherProfile.GetCutSectionAsPolygons((inversed));
                        otherProfile.inversed = (inversed);
                        for (int x = 0; x < otherProfileCutSection.Count; x++) // we convert the polygons of the cutsection to elements.
                        {
                            var polygon = otherProfileCutSection[x];
                            var polygonElements = polygon.GetSimplePolygonAsCurveElements();

                            if (polygonElements.Count == 0)
                                continue;

                            if (polygonElements.Count > 1) // if we have a complex polygon for a single face, we create a complexpolygon grouping for it
                            {
                                GimElement polygonSegmentElement = new GimElement();
                                polygonSegmentElement.ElementType = GimElementType.Gim_ComplexSegment;
                                int polygonSegmentElementIndex = gimData.Elements.Count;
                                GimLocal polygonSegmentLocal = polygon.faceLocal.Clone();
                                polygonSegmentLocal.parent = (uint)additionalSinglePolygonalIndex;
                                polygonSegmentElement.locals.Add(polygonSegmentLocal);
                                gimData.Elements.Add(polygonSegmentElement);

                                additionalSinglePolygonalElement.Elements.Add((uint)polygonSegmentElementIndex);

                                foreach (var polygonElement in polygonElements)
                                {
                                    int polygonElementIndex = gimData.Elements.Count;
                                    gimData.Elements.Add(polygonElement);
                                    polygonSegmentElement.Elements.Add((uint)polygonElementIndex);
                                    polygonElement.Locals[0].Parent = (uint)polygonSegmentElementIndex;
                                }

                                polygonSegmentElement.ElementDoubles = polygon.faceDoubles;
                                polygonSegmentElement.ElementInts = polygon.faceInts;
                            }
                            else // else if it's a simple element, we just add it directly.
                            {
                                GimElement polygonElement = polygonElements[0];

                                int polygonElementIndex = gimData.Elements.Count;
                                gimData.Elements.Add(polygonElement);
                                additionalSinglePolygonalElement.Elements.Add((uint)polygonElementIndex);
                                polygonElement.Locals[0].Parent = (uint)additionalSinglePolygonalIndex;
                            }
                        }

                        // cut surface A wire
                        GimElement otherCutSurfaceAWire = new GimElement();
                        otherCutSurfaceAWire.ElementType = GimElementType.wire;
                        int otherCutSurfaceAWireIndex = gimData.Elements.Count;
                        GimLocal otherCutSurfaceAWireLocal = new GimLocal();
                        otherCutSurfaceAWireLocal.parent = (uint)cutSurfaceAContainerIndex;
                        otherCutSurfaceAWire.Locals.Add(otherCutSurfaceAWireLocal);
                        gimData.Elements.Add(otherCutSurfaceAWire);
                        if (firstProfileIsBigger) cutSurfaceAContainerElement.Elements.Add((uint)otherCutSurfaceAWireIndex);
                        else cutSurfaceAContainerElement.Elements.Insert(0, (uint)otherCutSurfaceAWireIndex);

                        // cut surface B wire
                        GimElement otherCutSurfaceBWire = new GimElement();
                        otherCutSurfaceBWire.ElementType = GimElementType.wire;
                        int otherCutSurfaceBWireIndex = gimData.Elements.Count;
                        GimLocal otherCutSurfaceBWireLocal = new GimLocal();
                        otherCutSurfaceBWireLocal.parent = (uint)cutSurfaceBContainerIndex;
                        otherCutSurfaceBWire.Locals.Add(otherCutSurfaceBWireLocal);
                        gimData.Elements.Add(otherCutSurfaceBWire);
                        if (firstProfileIsBigger) cutSurfaceBContainerElement.Elements.Add((uint)otherCutSurfaceBWireIndex);
                        else cutSurfaceBContainerElement.Elements.Insert(0, (uint)otherCutSurfaceBWireIndex);

                        // after saving the cutsection we will now save the face A and face B

                        var otherFacePolys = otherProfile.GetFacesAsPolygons();
                        List<PolygonRepresentation> otherFaceAPolys = otherFacePolys[0];
                        List<PolygonRepresentation> otherFaceBPolys = otherFacePolys[1];

                        // saving Face A elements. If these are simple, they will be simplified later
                        for (int x = 0; x < otherFaceAPolys.Count; x++)
                        {
                            var polygon = otherFaceAPolys[x];
                            var polygonElements = polygon.GetSimplePolygonAsCurveElements();

                            //foreach (var polygonElement in polygonElements)
                            //{
                            //    int polygonElementIndex = gimData.Elements.Count;
                            //    gimData.Elements.Add(polygonElement);
                            //    otherCutSurfaceAWire.Elements.Add((uint)polygonElementIndex);
                            //    polygonElement.Locals[0].Parent = (uint)otherCutSurfaceAWireIndex;
                            //}

                            if (polygonElements.Count == 0)
                                continue;

                            if (polygonElements.Count > 1) // if we have a complex polygon for a single face, we create a complexpolygon grouping for it
                            {
                                GimElement polygonSegmentElement = new GimElement();
                                polygonSegmentElement.ElementType = GimElementType.Gim_ComplexSegment;
                                int polygonSegmentElementIndex = gimData.Elements.Count;
                                GimLocal polygonSegmentLocal = polygon.faceLocal.Clone();
                                polygonSegmentLocal.parent = (uint)otherCutSurfaceAWireIndex;
                                polygonSegmentElement.locals.Add(polygonSegmentLocal);
                                gimData.Elements.Add(polygonSegmentElement);

                                otherCutSurfaceAWire.Elements.Add((uint)polygonSegmentElementIndex);

                                foreach (var polygonElement in polygonElements)
                                {
                                    int polygonElementIndex = gimData.Elements.Count;
                                    gimData.Elements.Add(polygonElement);
                                    polygonSegmentElement.Elements.Add((uint)polygonElementIndex);
                                    polygonElement.Locals[0].Parent = (uint)polygonSegmentElementIndex;
                                }

                                polygonSegmentElement.ElementDoubles = polygon.faceDoubles;
                                polygonSegmentElement.ElementInts = polygon.faceInts;
                            }
                            else // else if it's a simple element, we just add it directly.
                            {
                                GimElement polygonElement = polygonElements[0];

                                int polygonElementIndex = gimData.Elements.Count;
                                gimData.Elements.Add(polygonElement);
                                otherCutSurfaceAWire.Elements.Add((uint)polygonElementIndex);
                                polygonElement.Locals[0].Parent = (uint)otherCutSurfaceAWireIndex;
                            }
                        }

                        // saving Face B elements. If these are simple, they will be simplified later
                        for (int x = 0; x < otherFaceBPolys.Count; x++)
                        {
                            var polygon = otherFaceBPolys[x];
                            var polygonElements = polygon.GetSimplePolygonAsCurveElements();

                            //foreach (var polygonElement in polygonElements)
                            //{
                            //    int polygonElementIndex = gimData.Elements.Count;
                            //    gimData.Elements.Add(polygonElement);
                            //    otherCutSurfaceBWire.Elements.Add((uint)polygonElementIndex);
                            //    polygonElement.Locals[0].Parent = (uint)otherCutSurfaceBWireIndex;
                            //}

                            if (polygonElements.Count == 0)
                                continue;

                            if (polygonElements.Count > 1) // if we have a complex polygon for a single face, we create a complexpolygon grouping for it
                            {
                                GimElement polygonSegmentElement = new GimElement();
                                polygonSegmentElement.ElementType = GimElementType.Gim_ComplexSegment;
                                int polygonSegmentElementIndex = gimData.Elements.Count;
                                GimLocal polygonSegmentLocal = polygon.faceLocal.Clone();
                                polygonSegmentLocal.parent = (uint)otherCutSurfaceBWireIndex;
                                polygonSegmentElement.locals.Add(polygonSegmentLocal);
                                gimData.Elements.Add(polygonSegmentElement);

                                otherCutSurfaceBWire.Elements.Add((uint)polygonSegmentElementIndex);

                                foreach (var polygonElement in polygonElements)
                                {
                                    int polygonElementIndex = gimData.Elements.Count;
                                    gimData.Elements.Add(polygonElement);
                                    polygonSegmentElement.Elements.Add((uint)polygonElementIndex);
                                    polygonElement.Locals[0].Parent = (uint)polygonSegmentElementIndex;
                                }

                                polygonSegmentElement.ElementDoubles = polygon.faceDoubles;
                                polygonSegmentElement.ElementInts = polygon.faceInts;
                            }
                            else // else if it's a simple element, we just add it directly.
                            {
                                GimElement polygonElement = polygonElements[0];

                                int polygonElementIndex = gimData.Elements.Count;
                                gimData.Elements.Add(polygonElement);
                                otherCutSurfaceBWire.Elements.Add((uint)polygonElementIndex);
                                polygonElement.Locals[0].Parent = (uint)otherCutSurfaceBWireIndex;
                            }
                        }



                        ProfileCandidates.RemoveAt(j); // we remove the profile
                        j--;
                        if (firstProfileIsBigger) profilePolygonList.Add(otherProfile);
                        else profilePolygonList.Insert(0, otherProfile);

                        if (!firstProfileIsBigger) // we found a profile that contains the current profile
                        {
                            // we restart our search cause we could have missed some other profiles that are contained in the new big profile
                            j = i;
                            faceAPoints = otherFaceAPoints;
                            faceBPoints = otherFaceBPoints;
                        }

                    }



                }

                GimLocal profileLocal = new GimLocal();
                // we get an original element of the profile so that we set that parent of that element to the parent of the profile
                GimElement AProfileElement = gimData.Elements[profilePolygons.originalElement];
                profileLocal.parent = AProfileElement.locals[0].parent;
                profileElement.locals.Add(profileLocal);


                Tuple<List<PolygonRepresentation>, int> T = 
                    new Tuple<List<PolygonRepresentation>, int>(profilePolygonList, profileElementIndex);
                ProfileTuples.Add(T);

            }
            return ProfileTuples;
        }

        // after constructing all the profile pieces, we remove the original profile pieces now.
        // this is done after adding because it's easier with the element references
        public static void RemoveOriginalProfilePieces(List<Tuple<List<PolygonRepresentation>, int>> ProfileTuples, GimData gimData)
        {
            // keeping track of the id of profile instances
            // old is used to indicate the id before removal
            List<int> oldProfilePieceId = new List<int>();
            // new is the id after removal
            List<int> newProfilePieceId = new List<int>();

            // using a dict to access the new id via the old id
            Dictionary<int, int> newProfileIds = new Dictionary<int, int>();

            for (int i = 0; i < gimData.Elements.Count; i++) // we mark the old profile IDs in order
            {
                GimElement element = gimData.Elements[i];
                if (element.ElementType == GimElementType.Gim_ProfileInstance)
                {
                    oldProfilePieceId.Add(i);
                }
            }


            // list of elements to remove, will be expanded to include the element children later
            List<uint> elementsToRemove = new List<uint>();
            foreach (Tuple<List<PolygonRepresentation>, int> tuple in ProfileTuples) // foreach profile
            {
                List<PolygonRepresentation> profilePolygons = tuple.Item1;

                foreach (PolygonRepresentation profilePolygon in profilePolygons) // for each polygon chain in the profile
                {
                    PolygonRepresentation currentPolygon = profilePolygon;
                    do // for each polygon piece
                    {
                        int elementId = currentPolygon.originalElement;
                        if (!elementsToRemove.Contains((uint)elementId))
                        {
                            elementsToRemove.Add((uint)elementId);
                        }

                        if (currentPolygon.Next == null) break; // next null break
                        if (currentPolygon.Next.Count == 0) break; // next null break
                        currentPolygon = currentPolygon.Next[0];
                        if (currentPolygon == profilePolygon) break; // next = start break

                    } while (currentPolygon != null);

                }

            }

            // we expand on the children of the elements to remove.
            // Each element to remove is checked, and we add its children to the list

            gimData.RemoveElementsAndTheirChildren(elementsToRemove);

            // now we update the profile Ids
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement element = gimData.Elements[i];
                if (element.ElementType == GimElementType.Gim_ProfileInstance)
                {
                    newProfilePieceId.Add(i);
                }
            }

            // convert it into a dictionary for easy access
            for (int i = 0; i < oldProfilePieceId.Count; i++)
            {
                int oldId = oldProfilePieceId[i];
                int newId = newProfilePieceId[i];
                newProfileIds.Add(oldId, newId);
            }

            // and finally we update the position of each profile
            for (int i=0; i < ProfileTuples.Count; i++)
            {
                Tuple<List<PolygonRepresentation>, int> tuple = ProfileTuples[i]; 
                int oldProfileID = tuple.Item2;
                int newProfileId = newProfileIds[oldProfileID];
                ProfileTuples[i] = new Tuple<List<PolygonRepresentation>, int>(tuple.Item1, newProfileId);
            }
        }

        // now we look for the faces of the profile and we remove them from gimData
        public static List<GimElement> FindOriginalProfileFaces
            (List<Tuple<List<PolygonRepresentation>, int>> ProfileTuples, GimData gimData, int partIndex)
        {

            GimElement partElement = null;
            int checkedElements = 0;
            int partId = 0;

            foreach(GimElement element in gimData.Elements) // we find our part in gimData
            {
                if(element.ElementType == GimElementType.shell)
                {
                    if(checkedElements < partIndex)
                    {
                        checkedElements++;
                    }
                    else
                    {
                        partElement = element;
                        break;
                    }
                }
                partId++;
            }

            if (partElement == null) return new List<GimElement>();

            // we now build a list of the child elements of the part, we will check all these child elements.
            List<int> elementsToCheck = new List<int> { partId };

            // each element we check, we check the children of
            for(int i=0; i<elementsToCheck.Count; i++)
            {
                int elId = elementsToCheck[i];
                if (elId > gimData.Elements.Count) // safety check
                {
                    elementsToCheck.RemoveAt(i);
                    i--;
                    continue;
                }
                GimElement element = gimData.Elements[elId];

                if (element.Elements == null) continue;
                foreach(uint childId in element.Elements) // adding the children of the element
                {
                    if (elementsToCheck.Contains((int)childId)) continue;
                    elementsToCheck.Add((int)childId);
                }
            }

            List<uint> elementsToRemove = new List<uint>();

            List<GimElement> profileElements = new List<GimElement>();


            foreach (Tuple<List<PolygonRepresentation>, int> profileTuple in ProfileTuples)
            {
                List<PolygonRepresentation> polygons = profileTuple.Item1;
                // list of tuples of faceA, faceB.
                List<Tuple<List<Vect>, List<Vect>>> polygonFaces = new();

                foreach(PolygonRepresentation profilePolygon in polygons) // building a list of faceA and faceB points
                {
                    var facesData = profilePolygon.GetBothFacePoints();
                    List<Vect> faceAPoints = facesData.Item1[0];
                    List<Vect> faceBPoints = facesData.Item1[1];
                    polygonFaces.Add(new Tuple<List<Vect>, List<Vect>>(faceAPoints, faceBPoints));
                    
                }


                int lastCheckIndex = 0; // elements after this are profiles so they will not be checked

                
                foreach(GimElement element in gimData.Elements)
                {
                    if(element.ElementType == GimElementType.Gim_ProfileInstance)
                    {
                        break;
                    }
                    lastCheckIndex++;
                }

                // adding the profile to the simplified list of profiles
                int profileId = profileTuple.Item2;
                GimElement profileElement = gimData.Elements[profileId];
                profileElements.Add(profileElement);
           
                // getting faceA and faceB elements
                uint cutSurfaceAId = profileElement.Elements[2];
                GimElement cutSurfaceA = gimData.Elements[(int)cutSurfaceAId];
                uint cutSurfaceBId = profileElement.Elements[3];
                GimElement cutSurfaceB = gimData.Elements[(int)cutSurfaceBId];


                // we now check the elements one by one
                for (int i = 0; i < elementsToCheck.Count; i++)
                {
                    int parentId = elementsToCheck[i];
                    if (parentId >= lastCheckIndex) continue; // only check elements before
                    GimElement element = gimData.Elements[parentId];
                    // face elements are our primary targets
                    if (element.ElementType == GimElementType.F_plane ||
                        element.ElementType == GimElementType.F_cilinder)
                    {
                        bool allContained = true;
                        // we assume they're all contained, and then we single out which face they belong to, if any
                        bool inFaceA = true, inFaceB = true;

                        // faceElements have wires as children. we check all these wires and their children.
                        for (int el = 0; el < element.Elements.Count; el++)
                        {
                            uint wireId = element.Elements[el];
                            GimElement wireElement = gimData.Elements[(int)wireId];
                            // safeguard against weird cases
                            if (wireElement.elementType != GimElementType.wire) continue;
                            for (int j = 0; j < wireElement.Elements.Count; j++)
                            {
                                uint childId = wireElement.Elements[j];
                                GimElement childElement = gimData.Elements[((int)childId)];

                                // these are the planar elements we will check,
                                // as they're the elements we check at the start of profileDetection
                                if (childElement.ElementType == GimElementType.C_line ||
                                    childElement.ElementType == GimElementType.C_circle ||
                                    childElement.ElementType == GimElementType.C_bspline ||
                                    childElement.elementType == GimElementType.C_bezier)
                                {
                                    // we build a list of curve points using the start, end and the intermediary points
                                    Vect EdgePoints = CurveHelpers.getStartAndEndPointsOfCurve(childElement, gimData);
                                    Vect StartPoint = EdgePoints[0..3];
                                    Vect EndPoint = EdgePoints[3..6];
									List<Vect> curvePoints = new List<Vect>
									{
										StartPoint,
										EndPoint
									};

									Vect[] IntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(childElement);
                                    // adding the intermediary points
                                    foreach (Vect InterPoint in IntermediaryPoints)
                                    {
                                        curvePoints.Add(InterPoint);
                                    }

                                    // we now check if each point connects with our original profile faces
                                    foreach (Vect curvePoint in curvePoints)
                                    {
                                        bool found = false;
                                        // we look for a profile polygon that connects with our point list
                                        foreach (var profilePolygon in polygonFaces)
                                        {

                                            List<Vect> faceA = profilePolygon.Item1;
                                            List<Vect> faceB = profilePolygon.Item2;

                                            // if we're just searching for faceA
                                            if (inFaceA)
                                            {
                                                foreach (Vect faceAPoint in faceA)
                                                {
                                                    if (faceAPoint == curvePoint)
                                                    {
                                                        found = true;
                                                        break;
                                                    }
                                                }

                                                if (found) // if we find it in faceA, we stop searching in B
                                                {
                                                    inFaceB = false;
                                                    break;
                                                }
                                            }

                                            // if we're just searching in faceB
                                            if (inFaceB)
                                            {
                                                foreach (Vect faceBPoint in faceB)
                                                {
                                                    if (faceBPoint == curvePoint)
                                                    {
                                                        found = true;
                                                        break;
                                                    }
                                                }

                                                if (found) // if we find it in faceB, we stop searching in A
                                                {
                                                    inFaceA = false;
                                                    break;
                                                }
                                            }

                                        }

                                        if (!found) // if we didn't find it, we eliminate the piece as a candidate
                                        {
                                            allContained = false;
                                            break;
                                        }

                                    }

                                    if (!allContained) // if not all piece elements are contained, we stop searching
                                                       // in the current face and start looking in the next
                                    {
                                        break;
                                    }

                                }

                            }

                            if (allContained) // if all the pieces of the face are contained
                            {
                                if (inFaceA) // contained in face A
                                {
                                    elementsToRemove.Add((uint)parentId);
                                }
                                else if (inFaceB) // contained in face B
                                {
                                    elementsToRemove.Add((uint)parentId);
                                }
                            }
                        }
                    }
                }

                // distinctifying because of multiple children in one parent
                elementsToRemove = elementsToRemove.Distinct().ToList();

                // we are going to duplicate the elements are their children, to not mess with the references
                // for this, we expand this list of elementsA
                for (int i = 0; i < elementsToRemove.Count; i++)
                {
                    uint elementId = elementsToRemove[i];
                    GimElement element = gimData.Elements[(int)elementId];
                    if (element.Elements == null) continue;
                    foreach(uint childId in element.Elements)
                    {
                        if (elementsToRemove.Contains(childId)) continue;
                        elementsToRemove.Add(childId);
                    }
                }

            }

            // sorting the elements to remove
            elementsToRemove.Sort();
            // removing locals of elements to remove

            for (int i = 0; i < elementsToRemove.Count; i++)
            {
                uint elId = elementsToRemove[i];
                GimElement element = gimData.Elements[(int)elId];
                element.locals = new List<GimLocal>();
            }

            // removing references of elements to remove

            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement element = gimData.Elements[i];
                if (element.Elements.Count == 0) continue;
                for (int j = 0; j < element.Elements.Count; j++)
                {
                    uint childId = element.Elements[j];
                    if (elementsToRemove.Contains(childId))
                    {
                        element.Elements.RemoveAt(j);
                        j--;
                    }
                }
            }

            // we remove the old elements
            gimData.RemoveElements(elementsToRemove);

            return profileElements;

        }

        // we now simplify the profile faces
        public static void SimplifyProfileFaces(List<GimElement> ProfileTuples, GimData gimData)
        {
            // in the case of simplified pieces, we remove them
            List<uint> elementsToRemove = new List<uint>();

            // we iterate each profile element, and we check their face parts
            foreach(GimElement profileElement in ProfileTuples)
            {
                uint cutSurfaceAId = profileElement.Elements[2]; 
                uint cutSurfaceBId = profileElement.Elements[3];
                GimElement cutSurfaceA = gimData.Elements[(int)cutSurfaceAId];
                GimElement cutSurfaceB = gimData.Elements[(int)cutSurfaceBId];

                // iterating over both cutSurfaces to avoid rewriting code
                GimElement[] cutSurfaces = new GimElement[] { cutSurfaceA, cutSurfaceB };
                foreach(GimElement cutSurface in cutSurfaces)
                {
                    int childCount = cutSurface.Elements.Count;
                    int childIndex = 0;
                    // we check if the cut surface is planar or not
                    foreach(uint faceElementId in cutSurface.Elements)
                    {
                        bool planar = true;
                        GimElement faceElement = gimData.Elements[(int)faceElementId];
                        // elements other than these are not planar
                        if(faceElement.ElementType != GimElementType.F_plane)
                        {
                            planar = false;
                        }
                        else
                        {
                            int wireCount = faceElement.Elements.Count;
                            Vect[] normals = new Vect[wireCount]; // normal vectors
                            Vect[] firstPoints = new Vect[wireCount]; // starting points
                            Vect[] averagePoints = new Vect[wireCount]; // average point

                            // we check all the wire children. They must all be planar
                            for (int i = 0; i < wireCount; i++)
                            {
                                uint wireId = faceElement.Elements[i];
                                GimElement wireElement = gimData.Elements[(int)wireId];

                                uint firstCurveId = wireElement.Elements[0];
                                GimElement firstCurve = gimData.Elements[(int)firstCurveId];

                                Vect curveStartEnd = CurveHelpers.getStartAndEndPointsOfCurve(firstCurve, gimData);
                                // we need a starting point, cause we will use it with the other points to check for 
                                // planarity
                                Vect firstPoint = curveStartEnd[0..3];
                                firstPoints[i] = firstPoint;
                                Vect[] curveIntermediaries = CurveHelpers.getIntermediaryPointsOfCurve(firstCurve);
                                Vect secondPoint;



                                Vect averagePoint = new Vect(0, 0, 0);
                                int pointCount = 0;

                                if (curveIntermediaries.Length > 0) // case of a line
                                {
                                    secondPoint = curveIntermediaries[0];
                                }
                                else
                                {
                                    secondPoint = curveStartEnd[3..6];
                                }

                                // first vector, needed in the calculation of the normal
                                Vect firstVector = secondPoint - firstPoint;
                                Vect normal = new Vect(0, 0, 0);
                                // we attempt to calculate the normal of the plane
                                // for this, we need 3 distinct, non-aligned points.
                                // Since we can't always get the points from one element,
                                // we iterate the elements until we find a non-zero normal
                                for (int j = 0; j < wireElement.Elements.Count; j++)
                                {
                                    uint curveId = wireElement.Elements[j];
                                    GimElement curve = gimData.Elements[(int)curveId];

                                    List<Vect> curvePoints = new List<Vect>();
                                    // we'll be using all these points to calculate the normal
                                    Vect curveEdges = CurveHelpers.getStartAndEndPointsOfCurve(curve, gimData);
                                    Vect curveStart = curveEdges[0..3];
                                    Vect curveEnd = curveEdges[3..6];
                                    curvePoints.Add(curveStart);
                                    curvePoints.Add(curveEnd);

                                    Vect[] curveMidPoints = CurveHelpers.getIntermediaryPointsOfCurve(curve);

                                    foreach (Vect curveMidPoint in curveMidPoints)
                                    {
                                        curvePoints.Add(curveMidPoint);
                                    }

                                    foreach (Vect curvePoint in curvePoints)
                                    {
                                        Vect secondVector = curvePoint - firstPoint;
                                        normal = firstVector ^ secondVector;
                                        if (normal.Norm > 0) // normal calculated successfully 
                                        {
                                            break;
                                        }
                                    }

                                    if (normal.Norm > 0)// normal calculated successfully 
                                    {
                                        break;
                                    }

                                }

                                normals[i] = normal.Direction; // normalizing the plane normal

                                for (int j = 0; j < wireElement.Elements.Count; j++) 
                                    // now we check using the starting point + the normal, that all points lie on the same plane
                                {
                                    uint curveId = wireElement.Elements[j];
                                    GimElement curve = gimData.Elements[(int)curveId];

                                    List<Vect> curvePoints = new List<Vect>();

                                    // we'll be checking all these points
                                    Vect curveEdges = CurveHelpers.getStartAndEndPointsOfCurve(curve, gimData);
                                    Vect curveStart = curveEdges[0..3];
                                    Vect curveEnd = curveEdges[3..6];
                                    curvePoints.Add(curveStart);
                                    curvePoints.Add(curveEnd);

                                    // building a list of the points
                                    Vect[] curveMidPoints = CurveHelpers.getIntermediaryPointsOfCurve(curve);

                                    foreach (Vect curveMidPoint in curveMidPoints)
                                    {
                                        curvePoints.Add(curveMidPoint);
                                    }

                                    // now we check if the points are on the plane
                                    foreach (Vect curvePoint in curvePoints)
                                    {
                                        // the way this is done, is that if P is our point to check,
                                        // and F is the first point on the plane, the vector FP must belong to the plane
                                        // and so therefore it must be perpendicular to the normal vector
                                        // hence the check of the dot product of the vectors

                                        Vect difference = curvePoint - firstPoint;
                                        // dot product check. equals 0 (less than equality margin) means we have perpendicularity
                                        if( Math.Abs(difference * normal) > PolygonRepresentation.EQUALITY_MARGIN)
                                        {
                                            planar = false;
                                            break;
                                        }

                                        // just calculating the average point
                                        averagePoint += curvePoint;
                                        pointCount++;
                                    }
                                    if (!planar) // face is not planar, stop
                                    {
                                        break;
                                    }
                                }

                                averagePoint /= pointCount; // averaging
                                averagePoints[i] = averagePoint;


                                if (!planar)
                                {
                                    break;
                                }



                            }

                            if (planar) // if all the wires are planar, that's not enough
                            {
                                bool parallel = true;
                                // we need to check if the wires are parallel to each other 
                                for(int i =0; i < normals.Length-1; i++)
                                {
                                    if (!normals[i].IsCollinearTo(normals[i + 1]))
                                    {
                                        parallel = false;
                                        break;
                                    }
                                }

                                if (parallel) // if the wires are parallel to each other, that's still not enough
                                {
                                    // we need to check if they reside on the same plane or not
                                    bool aligned = true;


                                    Vect normal = normals[0];

                                    for (int i = 0; i < firstPoints.Length - 1; i++)
                                    {
                                        // same planarity check as above.
                                        Vect difference = firstPoints[i] - firstPoints[i + 1];
                                        if (Math.Abs(difference * normal) > PolygonRepresentation.EQUALITY_MARGIN)
                                        {
                                            aligned = false;
                                            break;
                                        }
                                    }


                                    if (aligned) // if the wires are planar, parallel and aligned, then we have a planar face
                                    {
                                        // constructing a perpendicular base
                                        Vect firstVector = normal ^ (new Vect(1, 0, 0));
                                        if (firstVector.Norm < PolygonRepresentation.EQUALITY_MARGIN)
                                        {
                                            firstVector = normal ^ (new Vect(0, 1, 0));
                                        }
                                        firstVector = firstVector.Direction;

                                        Vect secondVector = normal ^ firstVector;
                                        secondVector = secondVector.Direction;
                                        Vect averagePoint = averagePoints[0];

                                        //Matrix planeMatrix = new Matrix(firstVector, secondVector, firstVector ^ secondVector);
                                        //planeMatrix.ApplyToLocal(faceElement.locals[0]);

                                        //// setting the plane's origin to the average point
                                        //faceElement.locals[0].Px = averagePoint[0];
                                        //faceElement.locals[0].Py = averagePoint[1];
                                        //faceElement.locals[0].Pz = averagePoint[2];

                                        for(int i =0; i < faceElement.Elements.Count; i++)
                                        {
                                            // we're marking the face's wires for removal, this will leave just a plane
                                            elementsToRemove.Add(faceElement.Elements[i]);
                                        }

                                        

                                    }

                                }

                            }

                        }


                        childIndex++;
                    }

                }
            }
            

            gimData.RemoveElementsAndTheirChildren(elementsToRemove);

        }

        // faces that are perpendicular are automatically omitted
        public static void OmitPerpendicularFaces(List<GimElement> profileElements, GimData gimData)
        {
            List<uint> elementsToRemove = new List<uint>();

            foreach (GimElement profileElement in profileElements)
            {
                // removing them in inverse order so they don't affect each other
                uint[] cutSurfaceIds = new uint[] { profileElement.Elements[3], profileElement.Elements[2] };
                int[] cutSurfacesIndexs = new int[] { 3, 2 };


                // getting the profile direction

                uint profilePolyLineId = profileElement.Elements[1];
                GimElement profilePolyLineElement = gimData.Elements[(int)profilePolyLineId];

                uint profileDirectionLineId = profilePolyLineElement.Elements[0];
                GimElement profileDirectionLineElement = gimData.Elements[(int) profileDirectionLineId];

                Vect direction = new Vect(profileDirectionLineElement.Locals[0]);

                int index = 0;
                // now we check each of the cutsections
                foreach(uint cutSurfaceId in cutSurfaceIds)
                {
                    GimElement cutSurfaceElement = gimData.Elements[(int)cutSurfaceId];

                    // has more than one plane child => not just one perpendicular face
                    if (cutSurfaceElement.Elements.Count != 1)
                    {
                        index++;
                        continue;
                    }

                    uint planeId = cutSurfaceElement.Elements[0];
                    GimElement planeElement = gimData.Elements[(int)planeId];

                    // plane has child elements, or isn't plane => not a perpendicular face
                    if(planeElement.Elements.Count > 0 || planeElement.ElementType != GimElementType.F_plane)
                    {
                        index++;
                        continue;
                    }

                    // getting the plane normal
                    Matrix planeMatrix = new Matrix(planeElement.Locals[0]);
                    Vect normal = planeMatrix[2];

                    //  plane is perpendicular to profile direction, we simplify it by omitting it
                    if (normal.IsCollinearTo(direction))
                    {
                        int pow = cutSurfacesIndexs[index]-2;
                        profileElement.ElementInts[0] += (int)Math.Pow(2, pow);
                        elementsToRemove.Add(cutSurfaceId);
                        profileElement.Elements.RemoveAt(cutSurfacesIndexs[index]);
                    }

                    index++;
                }

            }

            gimData.RemoveElementsAndTheirChildren(elementsToRemove);
        }


        // DIVIDER BETWEEN HELPER FUNCTIONS (BELOW) AND MAIN LOGIC FUNCTIONS (ABOVE)
        

  



        // below are helper functions used in the above functions


        // check if two profiles overlap. this is done by:
        // 1- projecting them onto the same direction line and seeing if their intervals line up. If then:
        // 2- project them onto the plane that is perpendicular to the profile direction. if the points land within each other
        // (profile A in profile B  or vice versa), then they are a profile
        // 3- check if A is inside B or B is inside A
        // 4- check if the profiles are aligned along the same direction (if they're not, mark them as opposites)

        // the returned result is Tuple< Contained, FirstProfileIsBigger, sidesAreInversed>

        public static Tuple<bool, bool, bool> 
            CheckProfilesOverlap(List<Vect> profileAFaceA,List<Vect> profileAFaceB,
            List<Vect> profileBFaceA, List<Vect> profileBFaceB, Vect profileDirection)
        {

            // we calculate the average point of face A and face B of profile A
            Vect ProfileAFaceACenter = new Vect(0, 0, 0);
            Vect ProfileAFaceBCenter = new Vect(0, 0, 0);

            foreach(Vect point in profileAFaceA)
            {
                ProfileAFaceACenter += point;
            }
            ProfileAFaceACenter /= profileAFaceA.Count;

            foreach(Vect point in profileAFaceB)
            {
                ProfileAFaceBCenter += point;
            }
            ProfileAFaceBCenter /= profileAFaceB.Count;

            // we calculate the average point of face A and face B of profile B
            Vect ProfileBFaceACenter = new Vect(0, 0, 0);
            Vect ProfileBFaceBCenter = new Vect(0, 0, 0);

            foreach (Vect point in profileBFaceA)
            {
                ProfileBFaceACenter += point;
            }
            ProfileBFaceACenter /= profileBFaceA.Count;

            foreach (Vect point in profileBFaceB)
            {
                ProfileBFaceBCenter += point;
            }
            ProfileBFaceBCenter /= profileBFaceB.Count;

            // now we will project the face center points onto the directional line, and get the parameter of the points on the line
            // by default, ProfileAFaceA will have a parameter 0, and profileAFaceB param > 0.

            Vect faceDirection = ProfileAFaceBCenter - ProfileAFaceACenter;

            if(faceDirection * profileDirection < 0)
            {
                profileDirection = -profileDirection;
            }

            double proAFaceAParam = 0;
            double proAFaceBParam = (ProfileAFaceBCenter - ProfileAFaceACenter) * profileDirection;

            // these could be reversed. for that, we check and reverse them, and mark B as Inversed.
            double proBFaceAParam = (ProfileBFaceACenter - ProfileAFaceACenter) * profileDirection;
            double proBFaceBParam = (ProfileBFaceBCenter - ProfileAFaceACenter) * profileDirection;

            bool inversed = false;
            if(proBFaceAParam > proBFaceBParam)
            {
                inversed = true;
                double temp = proBFaceBParam;
                proBFaceBParam = proBFaceAParam;
                proBFaceAParam = temp;
            }

            // equality margin for the case of B == A
            double equalityMargin = PolygonRepresentation.EQUALITY_MARGIN;

            // 4 types of overlap:
            // === : A boundary
            // --- : B boundary

            // B inside A
            // ==================
            //    ------------
            //    ------------
            // ==================
            //
            bool BinA = (proBFaceAParam + equalityMargin >= proAFaceAParam ) && (proBFaceBParam <= proAFaceBParam + equalityMargin);
            // B then A
            //     ==================
            // ---------------
            // ---------------
            //     ==================
            //
            bool BthenA = (proBFaceAParam <= proAFaceAParam + equalityMargin) && (proBFaceBParam <= proAFaceBParam + equalityMargin);
            // A then B
            // ==================
            //    ---------------------
            //    ---------------------
            // ==================
            //
            bool AthenB = (proBFaceAParam + equalityMargin >= proAFaceAParam) && (proBFaceBParam + equalityMargin >= proAFaceBParam);
            // A inside B
            //    ==================
            // ------------------------
            // ------------------------
            //    ==================
            //
            bool AinB = (proAFaceAParam + equalityMargin >= proBFaceAParam) && (proAFaceBParam <= proBFaceBParam + equalityMargin);

            bool contained;
            // any one of these cases is an overlap
            if(BinA || AinB || BthenA || AthenB)
            {
                contained = true;
            }
            else // check 1 failed
            {
                return new Tuple<bool, bool, bool>(false, false, false);
            }

            // we move onto check 2: we projected the points onto a plane and we see if one is contained in the other.
            // to do this, we simplify the profiles cutsection into two circles, one for profileA and one for profileB
            // then, if one circle is fully contained within the other one, we conclude that that profile is in the other profile.

            // the point profileAFaceA is the basis of the plane. since it's also the center of face A, projected it yields the projected face.

            // we will be dealing with vectors of length 2, since we're projecting onto a flat plane
            Matrix planeMatrix = profileDirection.GetNormalPlane();
            Vect vect1 = planeMatrix[0];
            Vect vect2 = planeMatrix[1];

            Vect profileAProjectedCenter = new Vect(0, 0);

            Vect profileBProjectedCenter;
            double centerX = (ProfileBFaceACenter - ProfileAFaceACenter) * vect1;
            double centerY = (ProfileBFaceACenter - ProfileAFaceACenter) * vect2;
            profileBProjectedCenter = new Vect(centerX, centerY);

            double faceARadius = 0;
            foreach(Vect point in profileAFaceA)
            {
                double x = (point - ProfileAFaceACenter) * vect1;
                double y = (point - ProfileAFaceACenter) * vect2;

                Vect projectedPoint = new Vect(x, y);
                double distance = (projectedPoint - profileAProjectedCenter).Norm;
                faceARadius = Math.Max(distance, faceARadius);
            }
            //faceARadius /= profileAFaceA.Count;


            double faceBRadius = 0;
            foreach (Vect point in profileBFaceA)
            {
                double x = (point - ProfileAFaceACenter) * vect1;
                double y = (point - ProfileAFaceACenter) * vect2;

                Vect projectedPoint = new Vect(x, y);
                double distance = (projectedPoint - profileBProjectedCenter).Norm;
                faceBRadius = Math.Max(distance,faceBRadius);
            }
            //faceBRadius /= profileBFaceA.Count;

            // the furthest point of B is still inside A
            bool BContainedInA = (faceBRadius + (profileBProjectedCenter - profileAProjectedCenter).Norm) <= faceARadius + equalityMargin;
            bool AContainedInB = (faceARadius + (profileBProjectedCenter - profileAProjectedCenter).Norm) <= faceBRadius + equalityMargin;

            bool firstProfileIsBigger;
            if(BContainedInA || AContainedInB)
            {
                firstProfileIsBigger = BContainedInA;
            }
            else // not contained within one another => not an inner profile
            {
                return new Tuple<bool, bool, bool>(false, false, false);
            }




            Tuple<bool, bool, bool> result = new Tuple<bool, bool, bool>(contained, firstProfileIsBigger, inversed);
            return result;
        }

        // this function returns the reverse of a curve. Useful later in profile decompression and in bsplines in profile detection
        // PS: the curve is still the same, it's just that the start and end are reversed.
        public static GimElement ReverseCurveElement(GimElement curveElement, int localIndex = 0)
        {

            GimElement newCurveElement = new GimElement();
            newCurveElement.ElementType = curveElement.ElementType;
            if (curveElement.ElementType == GimElementType.F_bspline)
            {
                double[] curveExtremities = CurveHelpers.getStartAndEndPointsOfCurve(curveElement, null, localIndex);

                // doubles
                Vect start = curveExtremities[0..3];
                Vect end = curveExtremities[3..6];
                Vect[] controlPoints = CurveHelpers.getIntermediaryPointsOfCurve(curveElement, localIndex);

                double[] newSplineDoubles = new double[curveElement.ElementDoubles.Length];
                int[] newSplineInts = new int[curveElement.ElementInts.Length];

                newSplineDoubles[0] = end[0];
                newSplineDoubles[1] = end[1];
                newSplineDoubles[2] = end[2];

                newSplineDoubles[3] = start[0];
                newSplineDoubles[4] = start[1];
                newSplineDoubles[5] = start[2];



                int degree = curveElement.ElementInts[^1];
                int knotsCount = curveElement.ElementInts[0];
                int multiplicitiesCount = curveElement.ElementInts.Length - 2;
                int polesCount = (curveElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                double[] weights = new double[polesCount];
                for (int i = 0; i < polesCount; i++)
                {
                    weights[i] = curveElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount];
                }

                List<double> knotList = new();

                double[] knotValues = new double[knotsCount];
                for (int i = 0; i < knotsCount; i++)
                {
                    knotValues[i] = curveElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
                }


                // adding all doubles in reverse
                int index = 6;
                for (int i = controlPoints.Length - 1; i >= 0; i--)
                {
                    Vect point = controlPoints[i];
                    newSplineDoubles[index] = point[0];
                    newSplineDoubles[index + 1] = point[1];
                    newSplineDoubles[index + 2] = point[2];

                    index += 3;
                }

                for (int i = knotValues.Length - 1; i >= 0; i--) 
                {
                    newSplineDoubles[index] = knotValues[i];
                    index++;
                }

                for (int i = weights.Length - 1; i >= 0; i--)
                {
                    newSplineDoubles[index] = weights[i];
                    index++;
                }

                // ints

                int[] multiplicities = new int[multiplicitiesCount];

                for (int i = 0; i < multiplicitiesCount; i++)
                {
                    int multiplicity = curveElement.ElementInts[i + 1];
                    multiplicities[i] = multiplicity;
                }

                newSplineInts[0] = knotsCount;
                newSplineInts[^1] = degree;

                index = 1;
                // inserting ints in reverse (multiplicities)
                for (int i = multiplicitiesCount - 1; i >= 0; i--) 
                {
                    newSplineInts[index] = multiplicities[i];
                    index++;
                }

                newCurveElement.Locals.Add(curveElement.Locals[localIndex].Clone());
                newCurveElement.ElementDoubles = newSplineDoubles;
                newCurveElement.ElementInts = newSplineInts;
            }

            if(curveElement.ElementType == GimElementType.C_line)
            {
                double[] curveExtremities = CurveHelpers.getStartAndEndPointsOfCurve(curveElement, null, localIndex);

                Vect start = curveExtremities[0..3];
                Vect end = curveExtremities[3..6];

                Vect lineDirection = (start - end).Direction;
                double startParam = 0;
                double endParam = (end - start).Norm;

                GimLocal curveLocal = curveElement.Locals[localIndex].Clone();
                lineDirection.ApplyToLocal(curveLocal);

                curveLocal.Px = end[0];
                curveLocal.Py = end[1];
                curveLocal.Pz = end[2];

                newCurveElement.ElementDoubles = new double[] {startParam, endParam};
                newCurveElement.Locals.Add(curveLocal);

            }

            // TODO: Circle and bezier

            return curveElement.Clone();
        }

        // this function turns a plane into a polygon representation
        public static PolygonRepresentation planeToPolygon(GimData gimData, PolygonRepresentation polygon,
            GimElement containerElement, Vect directionVector)
        {

            // for our polygon to count, it has to have two lines parallel to the direction we're checking
            // if not, it cannot be a profile
            GimElement line1 = null;
            GimElement line2 = null;
            bool firstElFound = false;
            bool secondElFound = false;

            if(containerElement.Elements.Count != 4) // we presuppose that there are only 4 elements for it
                // to be a quadrigon
            {
                return null;
            }

            // we iterate all the lines
            foreach (int childId in containerElement.Elements)
            {
                GimElement child = gimData.Elements[childId];
                Matrix childMatrix = GetLocalMatrix(child, gimData);
                Vect childVector = null;
                try
                {
                    childVector = (Vect)childMatrix;
                }
                catch(InvalidCastException) // if we couldn't cast the matrix, then it's not a vector, thus it doesn't equal a vector.
                {
                    continue;
                }
                if(directionVector.IsCollinearTo(childVector)) // one of the lines is parallel
                {

                    if (!firstElFound) // this is important, we need to find two lines of the plane that are parallel
                        // to our detected profile detection.
                    {
                        line1 = child;
                        firstElFound = true;
                    }

                    else
                    {
                        line2 = child;
                        secondElFound = true;
                        break;
                    }

                }

            }

            // plane only has one face or no faces parallel to the direction => not usable in a profile
            if(!firstElFound || !secondElFound)
            {
                return null;
            }


            double[] line1Points = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData);

            Vect point1 = line1Points[0..3];
            Vect point2 = line1Points[3..6];

            // now the below part assumes that line1 and line2 have the same direction.
            // if line1 was A---->B
            // and line2:   C---->D
            // then the polygon would be ABDC in cyclic order. This is important to not confuse
            // therefore the next two points are the same as above, only inverted

            double[] line2Points = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData);

            Vect point3 = line2Points[0..3];
            Vect point4 = line2Points[3..6];

            // if the lines are the same, this is a misleading erroneous element
            // same if one of the lines is null.

            if((point1 == point3 && point2 == point4) || (point1 == point4 && point2 == point3))
            {
                return null;
            }

            if (point1 == point2 || point3 == point4) return null;

            // another check to see if the polygon doesn't have colinear lines that actually coincide
            Vect vect1 = point2 - point1;
            Vect vect2 = point3 - point1;

            if (vect1.IsCollinearTo(vect2)) // in this case all the points are aligned => not a profile
            {
                return null;
            }

            // flipping wrong lines:

            if( (point4 - point3) * (point2 - point1) < 0)
            {
                Vect temp = point4;
                point4 = point3;
                point3 = temp;
            }

            // we add all the points in the above order to construct a polygon ABCD
            polygon = new();
            polygon.AddPoint(point1, PolygonRepresentation.POINT_BASEPOINT);
            polygon.AddPoint(point2, PolygonRepresentation.POINT_BASEPOINT);
            polygon.AddPoint(point4, PolygonRepresentation.POINT_BASEPOINT);
            polygon.AddPoint(point3, PolygonRepresentation.POINT_BASEPOINT);


            polygon.bsplineInts = new List<int[]> { Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>() };
            polygon.bsplineDoubles = new List<double[]> { Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>() };

            polygon.Direction = directionVector;
            polygon.PolygonType = PolygonRepresentation.POLYGON_QUADRILATERAL;
            polygon.originalElement = (int)containerElement.Locals[0].parent;

            GimElement originalElement = gimData.Elements[polygon.originalElement];
            polygon.faceLocal = originalElement.Locals[0];

            return polygon;
        }

        public static PolygonRepresentation BsplineToPolygon(GimData gimData, PolygonRepresentation polygon,
            GimElement containerElement, Vect directionVector)
        {
            // for our polygon to count, it has to have two lines parallel to the direction we're checking
            // if not, it cannot be a profile
            GimElement line1 = null;
            GimElement line2 = null;
            bool firstElFound = false;
            bool secondElFound = false;

            if (containerElement.Elements.Count < 4) // we presuppose that there are at least 4 elements for it
                                                      // to be a quadrigon
            {
                return null;
            }

            foreach (int childId in containerElement.Elements)
            {
                GimElement child = gimData.Elements[childId];
                // enforcing that the element is a spline;

                // todo: add potential beziers
                if(child.ElementType == GimElementType.C_line)
                {
                    Matrix childMatrix = GetLocalMatrix(child, gimData);
                    Vect childVector = null;
                    try
                    {
                        childVector = (Vect)childMatrix;
                    }catch(InvalidCastException)
                    {
                        continue; // line has nonlinear matrix, cannot be used as direction
                    }

                    if (childVector.IsCollinearTo(directionVector)) // one of the lines is parallel
                    {

                        if (!firstElFound) // this is important, we need to find two lines of the plane that are parallel
                                           // to our detected profile detection.
                        {
                            line1 = child;
                            firstElFound = true;
                        }

                        else
                        {
                            line2 = child;
                            secondElFound = true;
                            break;
                        }

                    }
                }

                if (child.ElementType == GimElementType.C_bspline || child.ElementType == GimElementType.C_bspline2D)
                {
                    if (!CurveHelpers.checkSplineIsFlat(child)) continue; // if the spline is not flat, its not a profile candidate.
                    Vect splineDirection = CurveHelpers.GetSplineVect(child, gimData);
                    if (splineDirection.IsCollinearTo(directionVector))
                    {

                        if (!firstElFound) // this is important, we need to find two splines of the plane that are parallel
                                           // to our detected profile detection.
                        {
                            line1 = child;
                            firstElFound = true;
                        }

                        else
                        {
                            line2 = child;
                            secondElFound = true;
                            break;
                        }

                    }
                }
            }

            if (!firstElFound || !secondElFound)
            {
                return null;
            }

            double[] line1Points = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData);

            Vect point1 = line1Points[0..3];
            Vect point2 = line1Points[3..6];

            double[] line2Points = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData);

            Vect point3 = line2Points[0..3];
            Vect point4 = line2Points[3..6];

            // making sure the lines have the same direction
            if( (point4 - point3) * (point2 - point1) < 0)
            {
                Vect temp = point4;
                point4 = point3;
                point3 = temp;
            }


            // if the lines are the same, this is a misleading erroneous element
            // same if one of the lines is null.

            if ((point1 == point3 && point2 == point4) || (point1 == point4 && point2 == point3))
            {
                return null;
            }

            if (point1 == point2 || point3 == point4) return null;

            // another check to see if the polygon doesn't have colinear lines that actually coincide
            Vect vect1 = point2 - point1;
            Vect vect2 = point3 - point1;

            if (vect1.IsCollinearTo(vect2)) // in this case all the points are aligned => not a profile
            {
                return null;
            }

            // in the above part we got the AB and CD of the curve ABCD.
            // so our curve lookes like
            //  A-------------> B
            //
            //  (some inbetween points)
            //
            //  C-------------> D
            //
            // the below part finds the inbetween points and links them in order.
            // This is done in two parts: between B and D, and between C and A. This is to maintain the order of points

            polygon = new();
            polygon.AddPoint(point1, PolygonRepresentation.POINT_BASEPOINT);


            polygon.bsplineInts.Add(Array.Empty<int>());
            polygon.bsplineDoubles.Add(Array.Empty<double>());

            Vect currentPoint = point2;



            // we iterate in a cylic order to find the inner points of the bezier in order
            // (these are mostly going to be control points, but we take into account other points JUST in case)

            List<GimElement> checkedLines = new List<GimElement> {line1}; // the elements are in random order, so we store which elements
                                                                          // we don't need to check again

            List<GimElement> faceAElements = new();
            List<GimElement> faceBElements = new();

            bool currentFaceIsA = true;

            while (!(currentPoint == point1)) // we stop when do a full loop
            {

                polygon.AddPoint(currentPoint, PolygonRepresentation.POINT_BASEPOINT);
                // the way we iterate is we find an element that starts with point X(n),
                // get the intermediary points, and jump the counter to the end point X(n+1)
                // i.e. A_start --> A_end = B_start -> B_end = C_start -> C_end....

                GimElement currentElement = null;
                Vect startPoint = null;
                Vect endPoint = null;
                bool inversed = false;

                foreach (int childId in containerElement.Elements)
                {
                    GimElement child = gimData.Elements[childId];
                    if (checkedLines.Contains(child)) continue; // we already checked this element.

                    double[] curvePoints = CurveHelpers.getStartAndEndPointsOfCurve(child, gimData);
                    Vect firstPoint = curvePoints[0..3];
                    Vect lastPoint = curvePoints[3..6];


                    if (currentPoint == firstPoint) // we found the next tail in our cycle
                    {
                        currentElement = child;
                        startPoint = firstPoint;
                        endPoint = lastPoint;
                        inversed = true; // we have to take the intermediary points in OPPOSITE ORDER
                        break;
                    }
                    if (currentPoint == lastPoint)
                    {
                        // sometimes the lines don't have the same order of first-last
                        // e.g. A--->B and D--->C but the polygon is ABCD
                        currentElement = child;
                        startPoint = firstPoint;
                        endPoint = lastPoint;
                        break;
                    }

                }

                if (currentElement == null) break;
                checkedLines.Add(currentElement);
                
                if(currentElement == line1 || currentElement == line2)
                {
                    currentFaceIsA = !currentFaceIsA;
                }
                else if (currentFaceIsA)
                {
                    faceAElements.Add(currentElement);
                }
                else
                { // making sure both arrays are oriented similarily
                    faceBElements.Insert(0, currentElement);
                }

                Vect[] curveIntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(currentElement);
                

                if (inversed) // we add the intermediary points in the reversed order
                {
                    for (int i = curveIntermediaryPoints.Length - 1; i >= 0; i--)
                    {
                        polygon.AddPoint(curveIntermediaryPoints[i], PolygonRepresentation.GetPointType(currentElement.ElementType));
                    }

                    if (currentElement.ElementType == GimElementType.C_bspline) // saving the bspline doubles
                    {


                        int degree = currentElement.ElementInts[^1];
                        int knotsCount = currentElement.ElementInts[0];
                        int multiplicitiesCount = currentElement.ElementInts.Length - 2;
                        int polesCount = (currentElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                        double[] doubles = new double[knotsCount + polesCount];
                        for (int i = 0; i < knotsCount; i++) // inversing the doubles of knots
                        {
                            doubles[i] = currentElement.ElementDoubles[knotsCount - 1 - i + polesCount * 3 + (/*2 +*/6)];
                        }

                        for (int i = 0; i < polesCount; i++) // inversing the doubles of poles
                        {
                            doubles[i + knotsCount] = currentElement.ElementDoubles[polesCount - 1 - i + polesCount * 3 + (/*2 +*/6) + knotsCount];
                        }

                        polygon.bsplineDoubles.Add(doubles);
                        polygon.bsplineInts.Add(currentElement.ElementInts);

                    }
                    else
                    {
                        polygon.bsplineInts.Add(Array.Empty<int>());
                        polygon.bsplineDoubles.Add(Array.Empty<double>());
                    }

                    currentPoint = endPoint;  // we advance to the start, cause the segment is inverted
                }
                else
                {
                    for (int i = 0; i < curveIntermediaryPoints.Length; i++)
                    {
                        polygon.AddPoint(curveIntermediaryPoints[i], PolygonRepresentation.GetPointType(currentElement.ElementType));
                    }

                    if (currentElement.ElementType == GimElementType.C_bspline)
                    {
                        polygon.bsplineInts.Add(currentElement.ElementInts);

                        int degree = currentElement.ElementInts[^1];
                        int knotsCount = currentElement.ElementInts[0];
                        int multiplicitiesCount = currentElement.ElementInts.Length - 2;
                        int polesCount = (currentElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                        double[] doubles = new double[knotsCount + polesCount];
                        for (int i = 0; i < knotsCount; i++)
                        {
                            doubles[i] = currentElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
                        }

                        for (int i = 0; i < polesCount; i++)
                        {
                            doubles[i + knotsCount] = currentElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount];
                        }

                        polygon.bsplineDoubles.Add(doubles);

                    }
                    else
                    {
                        polygon.bsplineInts.Add(Array.Empty<int>());
                        polygon.bsplineDoubles.Add(Array.Empty<double>());
                    }

                    currentPoint = startPoint; // we advance to the end, the next point
                }

            }

            if (faceAElements.Count != faceBElements.Count)
            {
                int faceACount = faceAElements.Count;
                int faceBCount = faceBElements.Count;
                int totalCount = (faceACount * faceBCount) / VectorHelpers.GCD(faceACount, faceBCount);

                int sideASegmentFactor = totalCount / faceACount;
                int sideBSegmentFactor = totalCount / faceBCount;

                int sideASegmentCount = 100 * sideASegmentFactor;
                int sideBSegmentCount = 100 * sideBSegmentFactor;


                Vect previousPoint = point2; // this where we started adding FaceAElements
                List<Vect> sideAPoints = new() { point2 };
                double curveAlength = 0;
                for (int i = 0; i < faceAElements.Count; i++)
                {
                    GimElement element = faceAElements[i];
                    bool inversed = false;
                    double[] elementExtremities = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData);
                    Vect start = elementExtremities[0..3];
                    Vect end = elementExtremities[3..6];

                    if (start == previousPoint)
                    {
                        inversed = false;
                    }
                    else if (end == previousPoint)
                    {
                        inversed = true;
                    }

                    AbstractCurveConstructor curveConstructor;
                    switch (element.ElementType)
                    {
                        case GimElementType.C_bspline: curveConstructor = new BsplineConstructor(element, inversed, sideASegmentCount); break;
                        default: curveConstructor = new BsplineConstructor(element, inversed, sideASegmentCount); break;
                    }

                    Vect[] points = curveConstructor.getApproximativePoints();
                    if (points[0] != previousPoint) return null; // better dismiss the profile than break everything

                    previousPoint = points[^1];
                    curveAlength += curveConstructor.curveLength;
                    /*for (int j = 1; j < points.Length; j++) // we don't add the shared point, cause we added it in the previous iteration.
                    {
                        sideAPoints.Add(points[j]);
                    }*/

                }
                previousPoint = point1;
                List<Vect> sideBPoints = new() { point1 };
                double curveBlength = 0;
                for (int i = 0; i < faceBElements.Count; i++)
                {
                    GimElement element = faceBElements[i];
                    bool inversed = false;
                    double[] elementExtremities = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData);
                    Vect start = elementExtremities[0..3];
                    Vect end = elementExtremities[3..6];

                    if (start == previousPoint)
                    {
                        inversed = false;
                    }
                    else if (end == previousPoint)
                    {
                        inversed = true;
                    }

                    AbstractCurveConstructor curveConstructor;
                    switch (element.ElementType)
                    {
                        case GimElementType.C_bspline: curveConstructor = new BsplineConstructor(element, inversed, sideBSegmentCount); break;
                        default: curveConstructor = new BsplineConstructor(element, inversed, sideBSegmentCount); break;
                    }

                    Vect[] points = curveConstructor.getApproximativePoints();
                    if (points[0] != previousPoint) return null; // better dismiss the profile than break everything

                    previousPoint = points[^1];
                    curveBlength += curveConstructor.curveLength;

                    for (int j = 1; j < points.Length; j++) // we don't add the shared point, cause we added it in the previous iteration.
                    {
                        sideBPoints.Add(points[j]);
                    }
                }

                int countA = sideAPoints.Count;
                int countB = sideBPoints.Count;

                double difference = (Math.Abs(curveAlength - curveBlength)/curveAlength);

                double differenceThreshold = 0.001;
                if (difference > differenceThreshold) return null; // curves are too different, better not risk it
                                                                   
                // making sure that the lines are equidistant. If they're not, not a profile.
                double dist1 = (point2 - point1).Norm;
                double dist2 = (point4 - point3).Norm;
                double distanceDifference = Math.Abs(dist2 - dist1);
                if (distanceDifference > Matrix.EQUALITY_MARGIN * 10) return null;

                if (faceACount <= faceBCount) // face A is simpler, use face A
                {
                    // order will be: point 1 -> point 2 -> faceA -> point 4 -> point 3 -> face B
                    polygon = new();
                    polygon.AddPoint(point1, PolygonRepresentation.POINT_BASEPOINT);

                    polygon.bsplineDoubles.Add(Array.Empty<double>());
                    polygon.bsplineInts.Add(Array.Empty<int>());

                    previousPoint = point2;
                    for(int i=0; i<faceAElements.Count; i++)
                    {
                        GimElement element = faceAElements[i];
                        double[] curveEndpoints = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData);
                        bool inversed = false;
                        Vect start = curveEndpoints[0..3];
                        Vect end = curveEndpoints[3..6];

                        if(end == previousPoint)
                        {
                            inversed = true; 
                        }
                        if (inversed)
                        {
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                        }
                        else
                        {
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                        }

                        Vect[] intermediaries = CurveHelpers.getIntermediaryPointsOfCurve(element);

                        if (inversed)
                        {
                            for(int j = intermediaries.Length-1; j >= 0 ; j--)
                            {
                                Vect point = intermediaries[j];
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }
                        else
                        {
                            for (int j = 0; j < intermediaries.Length; j++)
                            {
                                Vect point = intermediaries[j];
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }

                        if (inversed)
                        {
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = start;
                        }
                        else
                        {
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = end;
                        }
                        if(element.ElementType == GimElementType.C_bspline)
                        {
                            if (inversed)
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++) // inversing the doubles of knots
                                {
                                    doubles[x] = element.ElementDoubles[knotsCount - 1 - x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++) // inversing the doubles of poles
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[polesCount - 1 - x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                            else
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++)
                                {
                                    doubles[x] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++)
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                        }
                        else
                        {
                            polygon.bsplineDoubles.Add(Array.Empty<double>());
                            polygon.bsplineInts.Add(Array.Empty<int>());
                        }
                    }

                    Vect projectionVector = point1 - point2; // we will add this to all the points to project them to the other face
                    previousPoint = point3;

                    polygon.bsplineDoubles.Add(Array.Empty<double>());
                    polygon.bsplineInts.Add(Array.Empty<int>());

                    for (int i = faceAElements.Count - 1; i >= 0; i--)
                    {
                        GimElement element = faceAElements[i];
                        double[] curveEndpoints = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData);
                        bool inversed = false;
                        Vect start = curveEndpoints[0..3];
                        Vect end = curveEndpoints[3..6];

                        start += projectionVector;
                        end += projectionVector;

                        if (end == previousPoint)
                        {
                            inversed = true;
                        }
                        if (inversed)
                        {
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                        }
                        else
                        {
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                        }

                        Vect[] intermediaries = CurveHelpers.getIntermediaryPointsOfCurve(element);
                        if (inversed)
                        {
                            for (int j = intermediaries.Length - 1; j >= 0; j--)
                            {
                                Vect point = intermediaries[j] + projectionVector;
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }
                        else
                        {
                            for (int j = 0; j < intermediaries.Length; j++)
                            {
                                Vect point = intermediaries[j] + projectionVector;
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }

                        if (inversed)
                        {
                            if(start != point1) // don't add the start twice
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = start;
                        }
                        else
                        {
                            if(end != point1) // don't add the start twice
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = end;
                        }
                        if (element.ElementType == GimElementType.C_bspline)
                        {
                            if (inversed)
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++) // inversing the doubles of knots
                                {
                                    doubles[x] = element.ElementDoubles[knotsCount - 1 - x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++) // inversing the doubles of poles
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[polesCount - 1 - x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                            else
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++)
                                {
                                    doubles[x] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++)
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                        }
                        else
                        {
                            polygon.bsplineDoubles.Add(Array.Empty<double>());
                            polygon.bsplineInts.Add(Array.Empty<int>());
                        }
                    }
                    
                }
                else // face B is simpler, use face B
                {
                    // order will be: point 2 -> point 1 -> face B -> point 3 -> point 4 -> face A
                    polygon = new();
                    polygon.AddPoint(point2, PolygonRepresentation.POINT_BASEPOINT);

                    polygon.bsplineDoubles.Add(Array.Empty<double>());
                    polygon.bsplineInts.Add(Array.Empty<int>());

                    previousPoint = point1;
                    for (int i = 0; i < faceBElements.Count; i++)
                    {
                        GimElement element = faceBElements[i];
                        double[] curveEndpoints = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData);
                        bool inversed = false;
                        Vect start = curveEndpoints[0..3];
                        Vect end = curveEndpoints[3..6];

                        if (end == previousPoint)
                        {
                            inversed = true;
                        }
                        if (inversed)
                        {
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                        }
                        else
                        {
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                        }

                        Vect[] intermediaries = CurveHelpers.getIntermediaryPointsOfCurve(element);
                        if (inversed)
                        {
                            for (int j = intermediaries.Length - 1; j >= 0; j--)
                            {
                                Vect point = intermediaries[j];
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }
                        else
                        {
                            for (int j = 0; j < intermediaries.Length; j++)
                            {
                                Vect point = intermediaries[j];
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }

                        if (inversed)
                        {
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = start;
                        }
                        else
                        {
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = end;
                        }
                        if (element.ElementType == GimElementType.C_bspline)
                        {
                            if (inversed)
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++) // inversing the doubles of knots
                                {
                                    doubles[x] = element.ElementDoubles[knotsCount - 1 - x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++) // inversing the doubles of poles
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[polesCount - 1 - x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                            else
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++)
                                {
                                    doubles[x] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++)
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                        }
                        else
                        {
                            polygon.bsplineDoubles.Add(Array.Empty<double>());
                            polygon.bsplineInts.Add(Array.Empty<int>());
                        }
                    }

                    Vect projectionVector = point2 - point1; // we will add this to all the points to project them to the other face
                    previousPoint = point3;

                    polygon.bsplineDoubles.Add(Array.Empty<double>());
                    polygon.bsplineInts.Add(Array.Empty<int>());

                    for (int i = faceBElements.Count - 1; i >= 0; i--)
                    {
                        GimElement element = faceBElements[i];
                        double[] curveEndpoints = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData);
                        bool inversed = false;
                        Vect start = curveEndpoints[0..3];
                        Vect end = curveEndpoints[3..6];

                        start += projectionVector;
                        end += projectionVector;

                        if (end == previousPoint)
                        {
                            inversed = true;
                        }
                        if (inversed)
                        {
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                        }
                        else
                        {
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                        }

                        Vect[] intermediaries = CurveHelpers.getIntermediaryPointsOfCurve(element);

                        if (inversed)
                        {
                            for (int j = intermediaries.Length - 1; j >= 0; j--)
                            {
                                Vect point = intermediaries[j] + projectionVector;
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }
                        else
                        {
                            for (int j = 0; j < intermediaries.Length; j++)
                            {
                                Vect point = intermediaries[j] + projectionVector;
                                polygon.AddPoint(point, PolygonRepresentation.GetPointType(element.elementType));
                            }
                        }

                        if (inversed)
                        {
                            if(start != point2) // don't add the start twice
                            polygon.AddPoint(start, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = start;
                        }
                        else
                        {
                            if(end != point2) // don't add the start twice
                            polygon.AddPoint(end, PolygonRepresentation.POINT_BASEPOINT);
                            previousPoint = end;
                        }
                        if (element.ElementType == GimElementType.C_bspline)
                        {
                            if (inversed)
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++) // inversing the doubles of knots
                                {
                                    doubles[x] = element.ElementDoubles[knotsCount - 1 - x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++) // inversing the doubles of poles
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[polesCount - 1 - x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                            else
                            {

                                int degree = element.ElementInts[^1];
                                int knotsCount = element.ElementInts[0];
                                int multiplicitiesCount = element.ElementInts.Length - 2;
                                int polesCount = (element.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                                double[] doubles = new double[knotsCount + polesCount];
                                for (int x = 0; x < knotsCount; x++)
                                {
                                    doubles[x] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6)];
                                }

                                for (int x = 0; x < polesCount; x++)
                                {
                                    doubles[x + knotsCount] = element.ElementDoubles[x + polesCount * 3 + (/*2 +*/6) + knotsCount];
                                }

                                polygon.bsplineDoubles.Add(doubles);
                                polygon.bsplineInts.Add(element.ElementInts);
                            }
                        }
                        else
                        {
                            polygon.bsplineDoubles.Add(Array.Empty<double>());
                            polygon.bsplineInts.Add(Array.Empty<int>());
                        }
                    }
                }
            }

            polygon.PolygonType = PolygonRepresentation.POLYGON_BSPLINE;
            polygon.Direction = directionVector;
            polygon.originalElement = (int)containerElement.Locals[0].parent;

            GimElement originalElement = gimData.Elements[polygon.originalElement];
            if(originalElement.ElementDoubles != null) polygon.faceDoubles = (double[])originalElement.ElementDoubles.Clone();
            if(originalElement.ElementInts != null) polygon.faceInts = (int[])originalElement.ElementInts.Clone();
            polygon.faceLocal = originalElement.Locals[0];

            return polygon;
        }

        public static PolygonRepresentation CylinderToPolygon(GimData gimData, PolygonRepresentation polygon,
            GimElement containerElement, Vect directionVector)
        {
            List<GimElement> uniqueChildren = new();
            GimElement line1 = null;
            GimElement line2 = null;
            bool firstElFound = false;
            bool secondElFound = false;

            // checking whether has 2 lines, if not no profile

            int lineCount = 0;
            foreach (int childId in containerElement.Elements)
            {
                GimElement child = gimData.Elements[childId];
                if(child.ElementType == GimElementType.C_line)
                {
                    lineCount++;
                    continue;
                }
                else if (child.ElementType == GimElementType.C_bspline)
                {
                    if (CurveHelpers.checkSplineIsFlat(child)) lineCount++;
                }
            }
            if (lineCount != 2) return null;


            foreach (int childId in containerElement.Elements)
            {
                GimElement child = gimData.Elements[childId];
                bool duplicate = false;
                foreach (GimElement uniqueChild in uniqueChildren) // eliminating repeating children/duped elements
                {
                    if (uniqueChild.Compare(child))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    uniqueChildren.Add(child);
                }
            }

            bool multipleCircles = false;
            int circleCount = 0;
            foreach(GimElement child in uniqueChildren)
            {
                if(child.ElementType == GimElementType.C_circle)
                {
                    circleCount++;
                }
            }

            if(circleCount > 2 ) multipleCircles = true;

            foreach(GimElement child in uniqueChildren){
                Vect childVector = null;
                if (child.ElementType == GimElementType.C_circle) continue;
                if (child.ElementType == GimElementType.C_line)
                {
                    childVector = new Vect(child.locals[0]);
                }
                else if(child.ElementType == GimElementType.C_bspline)
                {
                    if (!CurveHelpers.checkSplineIsFlat(child)) continue;
                    childVector = CurveHelpers.GetSplineVect(child, gimData); // getting the direction of the spline
                }
                if (childVector is null) continue;

                if(childVector.IsCollinearTo(directionVector))
                {
                    if (!firstElFound)
                    {
                        firstElFound = true;
                        line1 = child;
                    }
                    else
                    {
                        secondElFound = true;
                        line2 = child;
                        break;
                    }
                }

            }


            if (!firstElFound || !secondElFound)
            {
                return null;
            }

            double[] line1Points = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData);
            double[] line2Points = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData);

            Vect point1 = line1Points[0..3];
            Vect point2 = line1Points[3..6];

            Vect point3 = line2Points[0..3];
            Vect point4 = line2Points[3..6];


            if ((point4 - point3) * (point2 - point1) < 0)
            {
                Vect temp = point4;
                point4 = point3;
                point3 = temp;
            }

            // if the lines are the same, this is a misleading erroneous element
            //// same if one of the lines is null.

            //if ((point1 == point3 && point2 == point4) || (point1 == point4 && point2 == point3))
            //{
            //    return null;
            //}

            //if (point1 == point2 || point3 == point4) return null;

            // another check to see if the polygon doesn't have colinear lines that actually coincide
            //Vect vect1 = point2 - point1;
            //Vect vect2 = point3 - point1;

            //if (vect1.IsCollinearTo(vect2)) // in this case all the points are aligned => not a profile
            //{
            //    return null;
            //}

            // in the above part we got the AB and CD of the curve ABCD.
            // so our curve lookes like
            //  A-------------> B
            //
            //  (some inbetween points)
            //
            //  C-------------> D
            //
            // the below part finds the inbetween points and links them in order.
            // This is done in two parts: between B and D, and between C and A. This is to maintain the order of points

            polygon = new();
            polygon.AddPoint(point1, PolygonRepresentation.POINT_BASEPOINT);
            Vect currentPoint = point2;


            polygon.bsplineInts.Add(Array.Empty<int>());
            polygon.bsplineDoubles.Add(Array.Empty<double>());

            // we iterate in a cylic order to find the inner points of the curve in order
            // (these are mostly going to be control points, but we take into account other points JUST in case)

            List<GimElement> checkedLines = new List<GimElement> { line1 }; // the elements are in random order, so we store which elements
                                                                            // we don't need to check again

            while ((checkedLines.Count != uniqueChildren.Count)) 
                // we stop when do a full loop or when we check all the elements
            {

                polygon.AddPoint(currentPoint, PolygonRepresentation.POINT_BASEPOINT);
                // the way we iterate is we find an element that starts with point X(n),
                // get the intermediary points, and jump the counter to the end point X(n+1)
                // i.e. A_start --> A_end = B_start -> B_end = C_start -> C_end....

                GimElement currentElement = null;
                Vect startPoint = null;
                Vect endPoint = null;
                bool inversed = false;

                foreach (GimElement child in uniqueChildren)
                {
                    if (checkedLines.Contains(child)) continue; // we already checked this element.

                    double[] curvePoints = CurveHelpers.getStartAndEndPointsOfCurve(child, gimData);
                    Vect firstPoint = curvePoints[0..3];
                    Vect lastPoint = curvePoints[3..6];


                    if (currentPoint == firstPoint) // we found the next tail in our cycle
                    {
                        currentElement = child;
                        startPoint = firstPoint;
                        endPoint = lastPoint;
                        break;
                    }
                    if (currentPoint == lastPoint)
                    {
                        // sometimes the lines don't have the same order of first-last
                        // e.g. A--->B and D--->C but the polygon is ABCD
                        currentElement = child;
                        startPoint = firstPoint;
                        endPoint = lastPoint;
                        inversed = true; // we have to take the intermediary points in OPPOSITE ORDER
                        break;
                    }

                }

                if (currentElement is null) break; // next element is null => we checked all elements.
                checkedLines.Add(currentElement);

                Vect[] curveIntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(currentElement);

                if (inversed) // we add the intermediary points in the reversed order
                {
                    for (int i = curveIntermediaryPoints.Length - 1; i >= 0; i--)
                    {
                        polygon.AddPoint(curveIntermediaryPoints[i], PolygonRepresentation.GetPointType(currentElement.elementType));
                    }

                    if (currentElement.ElementType == GimElementType.C_bspline)
                    {
                        polygon.bsplineInts.Add(currentElement.ElementInts);

                        int degree = currentElement.ElementInts[^1];
                        int knotsCount = currentElement.ElementInts[0];
                        int multiplicitiesCount = currentElement.ElementInts.Length - 2;
                        int polesCount = (currentElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                        double[] doubles = new double[knotsCount + polesCount];
                        for (int i = 0; i < knotsCount; i++)
                        {
                            doubles[i] = currentElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
                        }

                        for (int i = 0; i < polesCount; i++)
                        {
                            doubles[i + knotsCount] = currentElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount];
                        }

                        polygon.bsplineDoubles.Add(doubles);

                    }
                    else
                    {
                        polygon.bsplineInts.Add(Array.Empty<int>());
                        polygon.bsplineDoubles.Add(Array.Empty<double>());
                    }

                    currentPoint = startPoint;  // we advance to the start, cause the segment is inverted
                }
                else
                {
                    for (int i = 0; i < curveIntermediaryPoints.Length; i++)
                    {
                        polygon.AddPoint(curveIntermediaryPoints[i], PolygonRepresentation.GetPointType(currentElement.elementType));
                    }

                    if (currentElement.ElementType == GimElementType.C_bspline)
                    {
                        polygon.bsplineInts.Add(currentElement.ElementInts);

                        int degree = currentElement.ElementInts[^1];
                        int knotsCount = currentElement.ElementInts[0];
                        int multiplicitiesCount = currentElement.ElementInts.Length - 2;
                        int polesCount = (currentElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                        double[] doubles = new double[knotsCount + polesCount];
                        for (int i = 0; i < knotsCount; i++)
                        {
                            doubles[i] = currentElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
                        }

                        for (int i = 0; i < polesCount; i++)
                        {
                            doubles[i + knotsCount] = currentElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount];
                        }

                        polygon.bsplineDoubles.Add(doubles);

                    }
                    else
                    {
                        polygon.bsplineInts.Add(Array.Empty<int>());
                        polygon.bsplineDoubles.Add(Array.Empty<double>());
                    }

                    currentPoint = endPoint; // we advance to the end, the next point
                }

            }

            if (multipleCircles) // we're going to check if this cylinder is a full circle
                                // in that case, we mark it as "linked" as it is linked to itself
            {
                // the way we check for a full circle, is by checking that all points on circle elements appear twice
                // aka all circles are connected, as they share the same point repeating twice

                List<Tuple<Vect, int>> pointOccurrences = new();

                foreach(GimElement uniqueChild in uniqueChildren)
                {
                    if(uniqueChild.ElementType == GimElementType.C_circle)
                    {
                        Vect startEnd = CurveHelpers.getStartAndEndPointsOfCurve(uniqueChild, gimData);
                        Vect start = startEnd[0..3];
                        Vect end = startEnd[3..6];
                        List<Vect> pointList = new List<Vect>() { start, end };

                        foreach (Vect point in pointList)
                        {
                            bool pointFound = false;
                            for (int i = 0; i < pointOccurrences.Count; i++)
                            {
                                var pointOccurence = pointOccurrences[i];
                                if (pointOccurence.Item1 == point)
                                {
                                    pointFound = true;
                                    pointOccurrences[i] = new Tuple<Vect, int>(pointOccurence.Item1, pointOccurence.Item2 + 1);
                                }
                            }
                            if (!pointFound)
                            {
                                pointOccurrences.Add(new Tuple<Vect, int>(point, 1));
                            }
                        }
                    }
                }

                bool fullCircle = true;
                foreach(var pointOccurence in pointOccurrences)
                {
                    if(pointOccurence.Item2 != 2)
                    {
                        fullCircle = false;
                    }
                }
                if (fullCircle)
                {
                    polygon.IsStart = true;
                    polygon.Linked = true;
                }

            }
            polygon.PolygonType = PolygonRepresentation.POLYGON_CYLINDER;
            polygon.Direction = directionVector;
            polygon.originalElement = (int)containerElement.Locals[0].parent;

            GimElement originalElement = gimData.Elements[polygon.originalElement];
            if (originalElement.ElementDoubles != null) polygon.faceDoubles = (double[])originalElement.ElementDoubles.Clone();
            if (originalElement.ElementInts != null) polygon.faceInts = (int[])originalElement.ElementInts.Clone();
            polygon.faceLocal = originalElement.Locals[0];

            return polygon;

        }



        // for a curve AB, returns an array which is [A,B] => [Ax, Ay, Az, Bx, By, Bz]
        // using a single double[2*n] array is slightly more optimal than using a double[2][n] array


        // checks if a spline is a flat line




        // gets local matrix from an element and all subsequent parents
        public static Matrix GetLocalMatrix(GimElement element, GimData gimData)
        {
            if (element.locals.Count == 0) return new Matrix(new Vect(0, 0, 0), new Vect(0, 0, 0), new Vect(0, 0, 0));
            Matrix mat = new Matrix(element.locals[0]);
            GimElement currentEl = element;

            //do
            //{
            //    int parentId = (int)currentEl.locals[0].Parent;
            //    GimElement parent = gimData.Elements[parentId];
            //    if(parent.locals.Count == 0)
            //    {
            //        return mat;
            //    }

            //    Matrix parentMat = new Matrix(parent.locals[0]);

            //    // avoiding compound *= to preserve order
            //    mat = mat * parentMat;
            //    currentEl = parent;

            //} while (true);

            return mat;
        }

        public static bool CompareLocalMatrix(GimLocal localA, GimLocal localB)
        {
            // checking if the pieces have the same direction vector
            bool equal = true;
            // comparing X
            if (!CompareLocalDouble(localA.Xx, localB.Xx)) equal = false;
            if (!CompareLocalDouble(localA.Xy, localB.Xy)) equal = false;
            if (!CompareLocalDouble(localA.Xz, localB.Xz)) equal = false;
            // comparing Y
            if (!CompareLocalDouble(localA.Yx, localB.Yx)) equal = false;
            if (!CompareLocalDouble(localA.Yy, localB.Yy)) equal = false;
            if (!CompareLocalDouble(localA.Yz, localB.Yz)) equal = false;
            // comparing z
            if (!CompareLocalDouble(localA.Zx, localB.Zx)) equal = false;
            if (!CompareLocalDouble(localA.Zy, localB.Zy)) equal = false;
            if (!CompareLocalDouble(localA.Zz, localB.Zz)) equal = false;

            // equally important: checking if the pieces have directly opposing vectors
            bool opposites = true;

            // comparing X
            if (!CompareLocalDouble(-localA.Xx, localB.Xx)) opposites = false;
            if (!CompareLocalDouble(-localA.Xy, localB.Xy)) opposites = false;
            if (!CompareLocalDouble(-localA.Xz, localB.Xz)) opposites = false;
            // comparing Y
            if (!CompareLocalDouble(-localA.Yx, localB.Yx)) opposites = false;
            if (!CompareLocalDouble(-localA.Yy, localB.Yy)) opposites = false;
            if (!CompareLocalDouble(-localA.Yz, localB.Yz)) opposites = false;
            // comparing z
            if (!CompareLocalDouble(-localA.Zx, localB.Zx)) opposites = false;
            if (!CompareLocalDouble(-localA.Zy, localB.Zy)) opposites = false;
            if (!CompareLocalDouble(-localA.Zz, localB.Zz)) opposites = false;

            return opposites || equal;
        }

        public static bool CompareLocalDouble(double valA, double valB, double margin = 0.000001)
        {
            // both values are either max or min ==> unused
            // 
            // this next part is necessary evil; we use this function to compare directions A and B, and A and -B
            // however the annoying part is that if B = minInt, B = -2147483648, so -B = 2147483648, which is one more
            // than max Int. This results in multiple comparison cases, cause the same happens with MaxInt.
            // 
            // so we normalized both (absolute value) and check if they equal 2147483648 or 2147483647

            double valANormalized = Math.Abs(valA);
            double valBNormalized = Math.Abs(valB);
            double minInt = Math.Abs((double)int.MinValue);
            double maxInt = (double)int.MaxValue;

            if((valANormalized == minInt || valANormalized == maxInt) 
                &&(valBNormalized == minInt || valBNormalized == maxInt))
            {
                return true;
            }

            // this is making sure that stuff like 0.70710606 and 0.70710607 register as the same value

            double difference = Math.Abs(valA - valB);
            // if the difference between these values is less than 10^-7, they are probably just the same value
            return (difference < margin);

            
        }

    }



}