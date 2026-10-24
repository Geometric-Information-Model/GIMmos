using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GIMcompress.Shrinkwrap
{
    internal class SimplifiedPolygon
    {
        private List<Dictionary<Vertex2d, Vertex2d>> holes;

        private bool isHole = false;

        private Dictionary<Vertex2d, Vertex2d> points;

        private int dimensionIndex = 0;

        #region properties

        public int DimensionIndex
        {
            get { return dimensionIndex; }
            set { dimensionIndex = value; }
        }

        public Dictionary<Vertex2d, Vertex2d> Points
        {
            get { return points; }
            set { points = value; }
        }

        public List<Dictionary<Vertex2d, Vertex2d>> Holes
        {
            get { return holes; }
            set { holes = value; }
        }

        public bool IsHole
        {
            get { return isHole; }
            set { isHole = value; }
        }

        #endregion

        public SimplifiedPolygon(Dictionary<Vertex2d, Vertex2d> points, List<Dictionary<Vertex2d, Vertex2d>> holes = null, bool connectPoints = false)
        {
            this.points = points;
            this.holes = holes;
            if(this.holes == null)
            {
                this.holes = new();
            }

            if (connectPoints)
            {
                // todo connect the points in the correct order
            }

            int minPointIndex = 0;
            for (int i = 1; i < points.Count; i++)
            {

            }

        }

        public bool MergeWithTriangle(double[] triangle)
        {
            bool result = false;

            bool intersectionFound = false;

            List<Segment2d> polygonSegments = new List<Segment2d>();

            Vertex2d startPoint = null;
            foreach (var keyPair in points)
            {
                startPoint = keyPair.Key;
                break;
            }

            Vertex2d vertex1 = new(triangle[0], triangle[1]);
            Vertex2d vertex2 = new(triangle[2], triangle[3]);
            Vertex2d vertex3 = new(triangle[4], triangle[5]);

            if (points.ContainsKey(vertex1))
            {
                vertex1 = points[vertex1];
            }
            if (points.ContainsKey(vertex2))
            {
                vertex2 = points[vertex2];
            }
            if (points.ContainsKey(vertex3))
            {
                vertex3 = points[vertex3];
            }


            vertex1.AddConnectedPoint(vertex2);
            vertex1.AddConnectedPoint(vertex3);
            vertex2.AddConnectedPoint(vertex3);
            vertex2.AddConnectedPoint(vertex1);
            vertex3.AddConnectedPoint(vertex1);
            vertex3.AddConnectedPoint(vertex2);

            Vertex2d currentPoint = startPoint;
            Vertex2d previousPoint = null;
            do
            {
                Vertex2d nextPoint = null;
                foreach (Vertex2d connectedPoint in currentPoint.ConnectedVertices)
                {
                    if (connectedPoint != previousPoint)
                    {
                        nextPoint = connectedPoint;
                        break;
                    }
                }

                Vertex2d vertexA = currentPoint;
                Vertex2d vertexB = nextPoint;
                // making sure the smaller one is first
                if (vertexA[dimensionIndex] > vertexB[dimensionIndex]) {
                    vertexB = currentPoint;
                    vertexA = nextPoint;
                }
                Segment2d segment = new Segment2d(vertexA, vertexB);

                polygonSegments.Add(segment);

                previousPoint = currentPoint;
                currentPoint = nextPoint;


            } while (currentPoint != startPoint);

            polygonSegments.Sort(CompareSegments);

            List<Segment2d> triangleSegments = new();

            triangleSegments.Add(new Segment2d(vertex1, vertex2));

            triangleSegments.Add(new Segment2d(vertex1, vertex3));

            triangleSegments.Add(new Segment2d(vertex2, vertex3));

            int startPosition = GetSegmentInsertPosition(polygonSegments, triangleSegments[0]);
        
            for(int i = startPosition; i < polygonSegments.Count; i++)
            {
                var polygonSegment = polygonSegments[i];
                
                // segments whose maximum point is still smaller than the minimum point of the triangle are not considered and don't matter
                if(polygonSegment.pointB[dimensionIndex] < vertex1[dimensionIndex])
                {
                    continue;
                }

                // if the minimum point of this current segment is bigger than the triangle's max vertex then we stop
                // because all segments after this are further (bigger minimum points)
                if(polygonSegment.pointA[dimensionIndex] > vertex3[dimensionIndex])
                {
                    break;
                }

                bool segmentsIntersected = false;

                for (int j = 0; j < triangleSegments.Count; j++)
                {
                    var triangleSegment = triangleSegments[j];

                    // no need to checking segments where the max of segmentA is smaller than the min of segment B
                    if(polygonSegment.pointB[dimensionIndex] < triangleSegment.pointA[dimensionIndex])
                    {
                        continue;
                    }

                    if (polygonSegment.Intersects(triangleSegment))
                    {
                        Vertex2d intersectionPoint = polygonSegment.GetIntersection(triangleSegment);
                        polygonSegment.pointA.RemoveConnectedPoint(polygonSegment.pointB);
                        polygonSegment.pointB.RemoveConnectedPoint(polygonSegment.pointA);

                        triangleSegment.pointA.RemoveConnectedPoint(triangleSegment.pointB);
                        triangleSegment.pointB.RemoveConnectedPoint(triangleSegment.pointA);

                        Segment2d segmentA = new Segment2d(polygonSegment.pointA, intersectionPoint);
                        polygonSegments.Insert(i, segmentA);
                        Segment2d segmentB = new Segment2d(intersectionPoint, polygonSegment.pointB);
                        polygonSegments.Insert(GetSegmentInsertPosition(polygonSegments, segmentB), segmentB);

                        Segment2d segmentC = new Segment2d(triangleSegment.pointA, intersectionPoint);
                        triangleSegments.Insert(j, segmentC);

                        Segment2d segmentD = new Segment2d(intersectionPoint, triangleSegment.pointB);
                        triangleSegments.Insert(GetSegmentInsertPosition(triangleSegments, segmentD), segmentD);

                        segmentsIntersected = true;
                        intersectionFound = true;
                        break;
                    }
                    
                }

                if (segmentsIntersected)
                {
                    i--;
                }

            }

            result = intersectionFound;
            return result;
        }

        internal int CompareSegments(Segment2d segmentA, Segment2d segmentB)
        {
            if(segmentA.pointA[dimensionIndex] > segmentB.pointA[dimensionIndex])
            {
                return 1;
            }
            if(segmentA.pointA[dimensionIndex] < segmentB.pointA[dimensionIndex])
            {
                return -1;
            }
            return 0;
        }

        internal int GetSegmentInsertPosition(List<Segment2d> polygonSegments, Segment2d segment)
        {
            int lowerBound = 0;
            int upperBound = polygonSegments.Count;

            int insertPosition = lowerBound;

            while (lowerBound < upperBound)
            {
                if (segment.pointA[dimensionIndex] <= polygonSegments[lowerBound].pointA[dimensionIndex])
                {
                    insertPosition = lowerBound;
                    break;
                }
                if (segment.pointA[dimensionIndex] >= polygonSegments[upperBound - 1].pointA[dimensionIndex])
                {
                    insertPosition = upperBound;
                    break;
                }

                int middleBound = (upperBound + lowerBound) / 2;

                if (segment.pointA[dimensionIndex] == polygonSegments[middleBound].pointA[dimensionIndex])
                {
                    insertPosition = middleBound;
                    break;
                }

                if (segment.pointA[dimensionIndex] > polygonSegments[middleBound].pointA[dimensionIndex])
                {
                    lowerBound = middleBound + 1;
                    continue;
                }
                if (segment.pointA[dimensionIndex] < polygonSegments[middleBound].pointA[dimensionIndex])
                {
                    upperBound = middleBound;
                    continue;
                }
            }

            return insertPosition;
        }
    }
}
