using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Objects;
using ACadSharp.Tables;
using CSMath;
using GIMcompress;
using GIMcompress.Shrinkwrap;
using GimUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using static ACadSharp.Entities.Hatch.BoundaryPath;

namespace GIMtransfer
{
    public class DWGReadPolyrep
    {
        public const double DEFAULT_HALF_WIDTh = 0.0025;
        public const int FULL_CIRCLE_SEGMENT_COUNT = 32;
        public const double TWOPI = Math.PI * 2;
        public static void ReadPolyrepPart(GimData gimData, uint partIndex, BlockRecord record)
        {
            GimElement polyRepElement = new GimElement()
            {
                ElementType = GimElementType.R_Poly
            };

            uint polyRepIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polyRepElement);
            gimData.AddChildToParent(polyRepIndex, partIndex);

            foreach(var entity in record.Entities)
            {
                ReadPolyrepEntity(gimData, polyRepIndex, entity);
            }
        }

        public static void ReadPolyrepEntity(GimData gimData, uint polyRepIndex, Entity entity)
        {
            switch (entity.ObjectType)
            {
                case ObjectType.INSERT: return;
                    // polygon mesh
                case ObjectType.POLYLINE_PFACE:
                    if (entity is PolyfaceMesh faceMesh)
                        ReadPolyLinePFace(gimData, polyRepIndex, faceMesh);
                    break;
                case ObjectType.LWPOLYLINE:
                    if (entity is LwPolyline lwPolyline)
                        ReadLWPolyLine(gimData, polyRepIndex, lwPolyline);
                    break;
                case ObjectType.POLYLINE_2D:
                    if(entity is Polyline2D polyLine2D)
                        Read2DPolyLine(gimData, polyRepIndex, polyLine2D);
                    break;

                case ObjectType.LINE:
                    if(entity is ACadSharp.Entities.Line line)
                        ReadLine(gimData, polyRepIndex, line);
                    break;

                case ObjectType.SOLID:
                    if (entity is Solid solid)
                        ReadSolid(gimData, polyRepIndex, solid);
                    break;
                case ObjectType.FACE3D:
                        //TODO: ADD SUPPORT FOR 3DFACE
                    break;

                case ObjectType.HATCH:
                    if (entity is Hatch hatch)
                        ReadHatch(gimData, polyRepIndex, hatch);
                    break;
                case ObjectType.CIRCLE:
                    if (entity is Circle circle)
                        ReadCircle(gimData, polyRepIndex, circle);
                    break;
                case ObjectType.ARC:
                    if (entity is ACadSharp.Entities.Arc arc)
                        ReadArc(gimData, polyRepIndex, arc);
                    break;

                case ObjectType.ELLIPSE:
                    if (entity is ACadSharp.Entities.Ellipse ellipse)
                        ReadEllipse(gimData, polyRepIndex, ellipse);
                    break;
            }
        }

        public static void ReadEllipse(GimData gimData, uint polyRepIndex, ACadSharp.Entities.Ellipse ellipse)
        {
            Vect ellipseNormal = DWGHelpers.XYZtoVect(ellipse.Normal);
            double majorRadius = ellipse.MajorAxis / 2;
            double minorRadius = ellipse.MinorAxis / 2;
            Vect center = DWGHelpers.XYZtoVect(ellipse.Center);

            double startAngle = ellipse.StartParameter;
            double endAngle = ellipse.EndParameter;
            if (endAngle == startAngle) endAngle = startAngle + TWOPI;
            var ellipsePolygonElement = MeshCircularOutline(center, startAngle, endAngle, majorRadius, minorRadius, ellipseNormal);
            uint ellipsePolygonIndex = (uint)gimData.Elements.Count;

            gimData.Elements.Add(ellipsePolygonElement);
            gimData.AddChildToParent(ellipsePolygonIndex, polyRepIndex);

            AssignDWGColorsToGimMaterial(gimData, ellipsePolygonIndex, ellipse);
        }
        public static void ReadCircle(GimData gimData, uint polyRepIndex, Circle circle)
        {
            Vect circleNormal = DWGHelpers.XYZtoVect(circle.Normal);
            double radius = circle.Radius;
            Vect center = DWGHelpers.XYZtoVect(circle.Center);

            var circlePolygonElement = MeshCircularOutline(center, 0, TWOPI, radius, circleNormal);
            uint circlePolygonIndex = (uint)gimData.Elements.Count;

            gimData.Elements.Add(circlePolygonElement);
            gimData.AddChildToParent(circlePolygonIndex, polyRepIndex);

            AssignDWGColorsToGimMaterial(gimData, circlePolygonIndex, circle);
        }

        public static void ReadArc(GimData gimData, uint polyRepIndex, ACadSharp.Entities.Arc arc)
        {
            Vect circleNormal = DWGHelpers.XYZtoVect(arc.Normal);
            double radius = arc.Radius;
            Vect center = DWGHelpers.XYZtoVect(arc.Center);
            var startAngle = arc.StartAngle;
            var endAngle = arc.EndAngle;
            if(startAngle > endAngle)
            {
                endAngle += Math.PI * 2;
            }

            var circlePolygonElement = MeshCircularOutline(center, startAngle, endAngle, radius, circleNormal);
            uint circlePolygonIndex = (uint)gimData.Elements.Count;

            gimData.Elements.Add(circlePolygonElement);
            gimData.AddChildToParent(circlePolygonIndex, polyRepIndex);

            AssignDWGColorsToGimMaterial(gimData, circlePolygonIndex, arc);
        }

        public static void ReadLine(GimData gimData, uint polyRepIndex, ACadSharp.Entities.Line line)
        {
            double halfWidth = DEFAULT_HALF_WIDTh;

            Vect polyNormal = DWGHelpers.XYZtoVect(line.Normal);

            Vect lastVertex = DWGHelpers.XYZtoVect(line.EndPoint);
            Vect firstVertex = DWGHelpers.XYZtoVect(line.StartPoint);

            List<Vect> boundA = new() { firstVertex };
            List<Vect> boundB = new() { firstVertex };

            Vect lastPrevDir = (lastVertex - firstVertex).Direction;
            Vect lastNextDir = (firstVertex - lastVertex).Direction;

            Vect firstNextDir = (lastVertex - firstVertex).Direction;
            Vect firstBisector, lastBisector;
            Vect firstPointA, firstPointB, lastPointA, lastPointB;

            firstBisector = polyNormal ^ firstNextDir;
            lastBisector = polyNormal ^ lastPrevDir;

            firstPointA = firstVertex + halfWidth * firstBisector;
            firstPointB = firstVertex - halfWidth * firstBisector;

            lastPointA = lastVertex + halfWidth * lastBisector;
            lastPointB = lastVertex - halfWidth * lastBisector;

            boundA[0] = firstPointA;
            boundB[0] = firstPointB;
            boundA.Add(lastPointA);
            boundB.Add(lastPointB);

            List<Triangle> triangles = new();
            for (int i = 0; i < boundA.Count - 1; i++)
            {
                Vect pointA = boundA[i], pointB = boundB[i], nextA = boundA[i + 1], nextB = boundB[i + 1];
                Triangle triangleA = new(pointA, nextA, nextB),
                    triangleB = new(pointA, nextB, pointB);

                triangles.Add(triangleA); triangles.Add(triangleB);
            }

            TriangleMesh triangleMesh = new() { Triangles = triangles };
            GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
            triangleMesh.ApplyToGimDataPolygon(polygonElement);

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);

            AssignDWGColorsToGimMaterial(gimData, polygonIndex, line);
        }


        public static void Read2DPolyLine(GimData gimData, uint polyRepIndex, Polyline2D polyLine2D)
        {
            bool closed = polyLine2D.IsClosed;
            double halfWidth = DEFAULT_HALF_WIDTh;

            Vect polyNormal = DWGHelpers.XYZtoVect(polyLine2D.Normal);
            var a =polyLine2D.Vertices[0];

            Vect lastVertex = DWGHelpers.Vertex2DtoVect(polyLine2D.Vertices[^1]);
            Vect firstVertex = DWGHelpers.Vertex2DtoVect(polyLine2D.Vertices[0]);

            List<Vect> boundA = new() { firstVertex };
            List<Vect> boundB = new() { firstVertex };

            for (int i = 1; i < polyLine2D.Vertices.Count - 1; i++)
            {
                int prevI = i - 1;
                int nextI = i + 1;

                Vect prevVertex = DWGHelpers.Vertex2DtoVect(polyLine2D.Vertices[prevI]);
                Vect vertex = DWGHelpers.Vertex2DtoVect(polyLine2D.Vertices[i]);
                Vect nextVertex = DWGHelpers.Vertex2DtoVect(polyLine2D.Vertices[nextI]);

                var prevDir = (vertex - prevVertex).Direction;
                var nextDir = (nextVertex - vertex).Direction;

                var bisectDir = (nextDir + prevDir).Direction;
                if (bisectDir.Norm < Matrix.EQUALITY_MARGIN) bisectDir = prevDir;

                var bisector = polyNormal ^ bisectDir;
                var pointA = vertex + halfWidth * bisector;
                var pointB = vertex - halfWidth * bisector;

                boundA.Add(pointA);
                boundB.Add(pointB);
            }

            Vect beforeLastVertex = DWGHelpers.Vertex2DtoVect(polyLine2D.Vertices[^2]);
            Vect secondVertex = DWGHelpers.Vertex2DtoVect(polyLine2D.Vertices[1]);

            Vect lastPrevDir = (lastVertex - beforeLastVertex).Direction;
            Vect lastNextDir = (firstVertex - lastVertex).Direction;

            Vect firstPrevDir = lastNextDir;
            Vect firstNextDir = (secondVertex - firstVertex).Direction;
            Vect firstBisector, lastBisector;
            Vect firstPointA, firstPointB, lastPointA, lastPointB;

            // TODO: maybe check if it's closed but it's less than 3 points?
            if (closed) // if closed, we add the start and end points similar to the last few points
            {
                Vect firstBisectDir = (firstNextDir + firstPrevDir).Direction;
                if (firstBisectDir.Norm < Matrix.EQUALITY_MARGIN) firstBisectDir = firstPrevDir;
                firstBisector = polyNormal ^ firstBisectDir;
                Vect lastBisectDir = (lastNextDir + lastPrevDir).Direction;
                if (lastBisectDir.Norm < Matrix.EQUALITY_MARGIN) lastBisectDir = lastPrevDir;
                lastBisector = polyNormal ^ lastBisectDir;
            }
            else // else the points will simply close off
            {
                firstBisector = polyNormal ^ firstNextDir;
                lastBisector = polyNormal ^ lastPrevDir;
            }

            firstPointA = firstVertex + halfWidth * firstBisector;
            firstPointB = firstVertex - halfWidth * firstBisector;

            lastPointA = lastVertex + halfWidth * lastBisector;
            lastPointB = lastVertex - halfWidth * lastBisector;

            boundA[0] = firstPointA;
            boundB[0] = firstPointB;
            boundA.Add(lastPointA);
            boundB.Add(lastPointB);

            List<Triangle> triangles = new();
            for (int i = 0; i < boundA.Count - 1; i++)
            {
                Vect pointA = boundA[i], pointB = boundB[i], nextA = boundA[i + 1], nextB = boundB[i + 1];
                Triangle triangleA = new(pointA, nextA, nextB),
                    triangleB = new(pointA, nextB, pointB);

                triangles.Add(triangleA); triangles.Add(triangleB);
            }

            if (closed)
            {
                Triangle triangleA = new(firstPointA, firstPointB, lastPointA),
                    triangleB = new(firstPointB, lastPointA, lastPointB);

                triangles.Add(triangleA); triangles.Add(triangleB);
            }

            TriangleMesh triangleMesh = new() { Triangles = triangles };
            GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
            triangleMesh.ApplyToGimDataPolygon(polygonElement);

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);

            AssignDWGColorsToGimMaterial(gimData, polygonIndex, polyLine2D);
        }

        public static void ReadLWPolyLine(GimData gimData, uint polyRepIndex, LwPolyline lwPolyline)
        {
            bool closed = lwPolyline.IsClosed;
            double halfWidth = lwPolyline.ConstantWidth == 0 ? DEFAULT_HALF_WIDTh : lwPolyline.ConstantWidth / 2;

            Vect polyNormal = DWGHelpers.XYZtoVect(lwPolyline.Normal);

            Vect lastVertex = DWGHelpers.Vertex2DtoVect(lwPolyline.Vertices[^1]);
            Vect firstVertex = DWGHelpers.Vertex2DtoVect(lwPolyline.Vertices[0]);

            List<Vect> boundA = new() { firstVertex };
            List<Vect> boundB = new() { firstVertex };

            for (int i=1; i<lwPolyline.Vertices.Count - 1; i++)
            {
                int prevI = i - 1;
                int nextI = i + 1;

                Vect prevVertex = DWGHelpers.Vertex2DtoVect(lwPolyline.Vertices[prevI]);
                Vect vertex = DWGHelpers.Vertex2DtoVect(lwPolyline.Vertices[i]);
                Vect nextVertex = DWGHelpers.Vertex2DtoVect(lwPolyline.Vertices[nextI]);

                var prevDir = (vertex - prevVertex).Direction;
                var nextDir = (nextVertex - vertex).Direction;

                var bisectDir = (nextDir + prevDir).Direction;
                if (bisectDir.Norm < Matrix.EQUALITY_MARGIN) bisectDir = prevDir;

                var bisector = polyNormal ^ bisectDir;
                var pointA = vertex + halfWidth * bisector;
                var pointB = vertex - halfWidth * bisector;

                boundA.Add(pointA);
                boundB.Add(pointB);
            }

            Vect beforeLastVertex = DWGHelpers.Vertex2DtoVect(lwPolyline.Vertices[^2]);
            Vect secondVertex = DWGHelpers.Vertex2DtoVect(lwPolyline.Vertices[1]);

            Vect lastPrevDir = (lastVertex - beforeLastVertex).Direction;
            Vect lastNextDir = (firstVertex - lastVertex).Direction;

            Vect firstPrevDir = lastNextDir;
            Vect firstNextDir = (secondVertex - firstVertex).Direction;
            Vect firstBisector, lastBisector;
            Vect firstPointA, firstPointB, lastPointA, lastPointB;

            // TODO: maybe check if it's closed but it's less than 3 points?
            if (closed) // if closed, we add the start and end points similar to the last few points
            {
                Vect firstBisectDir = (firstNextDir + firstPrevDir).Direction;
                if (firstBisectDir.Norm < Matrix.EQUALITY_MARGIN) firstBisectDir = firstPrevDir;
                firstBisector = polyNormal ^ firstBisectDir;
                Vect lastBisectDir = (lastNextDir + lastPrevDir).Direction;
                if (lastBisectDir.Norm < Matrix.EQUALITY_MARGIN) lastBisectDir = lastPrevDir;
                lastBisector = polyNormal ^ lastBisectDir;
            }
            else // else the points will simply close off
            {
                firstBisector = polyNormal ^ firstNextDir;
                lastBisector = polyNormal ^ lastPrevDir;
            }

            firstPointA = firstVertex + halfWidth * firstBisector;
            firstPointB = firstVertex - halfWidth * firstBisector;

            lastPointA = lastVertex + halfWidth * lastBisector;
            lastPointB = lastVertex - halfWidth * lastBisector;

            boundA[0] = firstPointA;
            boundB[0] = firstPointB;
            boundA.Add(lastPointA);
            boundB.Add(lastPointB);

            List<Triangle> triangles = new();
            for (int i = 0; i < boundA.Count - 1; i++)
            {
                Vect pointA = boundA[i], pointB = boundB[i], nextA = boundA[i + 1], nextB = boundB[i + 1];
                Triangle triangleA = new(pointA, nextA, nextB),
                    triangleB = new(pointA, nextB, pointB);

                triangles.Add(triangleA); triangles.Add(triangleB);
            }

            if (closed)
            {
                Triangle triangleA = new(firstPointA, firstPointB, lastPointA),
                    triangleB = new(firstPointB, lastPointA, lastPointB);

                triangles.Add(triangleA); triangles.Add(triangleB);
            }

            TriangleMesh triangleMesh = new() { Triangles = triangles };
            GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
            triangleMesh.ApplyToGimDataPolygon(polygonElement);

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);

            AssignDWGColorsToGimMaterial(gimData, polygonIndex, lwPolyline);
        }

        private static void ReadPolyLinePFace(GimData gimData, uint polyRepIndex, PolyfaceMesh faceMesh)
        {
            GimElement polygonElement = new()
            {
                ElementType = GimElementType.P_polygon,
            };
            uint polygonElementIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonElementIndex, polyRepIndex);
            
            AssignDWGColorsToGimMaterial(gimData, polygonElementIndex, faceMesh);
            
            List<double> elementDoubles = new();
            foreach (Vertex vertex in faceMesh.Vertices)
            {
                elementDoubles.Add(vertex.Location.X);
                elementDoubles.Add(vertex.Location.Y);
                elementDoubles.Add(vertex.Location.Z);
            }

            List<int> elementInts = new();
            foreach(var face in faceMesh.Faces)
            {
                if (face.Location.X != 0 || face.Location.Y != 0 || face.Location.Z != 0)
                    GimLogger.Warn("Polygon face has non null location");
                elementInts.Add(Math.Abs(face.Index1) - 1);
                elementInts.Add(Math.Abs(face.Index2) - 1);
                elementInts.Add(Math.Abs(face.Index3) - 1);
                // face with 4 points, TODO ANIS: optimize this to reduce shard triangles
                // TODO: maybe pay attention to invisible edges
                if (face.Index4 != 0)
                {
                    elementInts.Add(Math.Abs(face.Index1) - 1);
                    elementInts.Add(Math.Abs(face.Index3) - 1);
                    elementInts.Add(Math.Abs(face.Index4) - 1);
                }
            }

            polygonElement.ElementDoubles = elementDoubles.ToArray();
            polygonElement.ElementInts = elementInts.ToArray();
        }



        public static void ReadSolid(GimData gimData, uint polyRepIndex, Solid solid)
        {
            List<Vect> points = DWGHelpers.SolidPointsToVectList(solid);
            
            if (points.Count == 3)
            {
                List<Triangle> triangles = new();
                Triangle triangle = new(points[0], points[1], points[2]);
                triangles.Add(triangle);
                TriangleMesh triangleMesh = new() { Triangles = triangles };
                GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
                triangleMesh.ApplyToGimDataPolygon(polygonElement);

                uint polygonIndex = (uint)gimData.Elements.Count;
                gimData.Elements.Add(polygonElement);
                gimData.AddChildToParent(polygonIndex, polyRepIndex);

                AssignDWGColorsToGimMaterial(gimData, polygonIndex, solid);
            }
            if (points.Count == 4)
            {
                List<Triangle> triangles = DWGHelpers.GetTwoTrianglesFromFourPoints(points);
                TriangleMesh triangleMesh = new() { Triangles = triangles };
                GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
                triangleMesh.ApplyToGimDataPolygon(polygonElement);

                uint polygonIndex = (uint)gimData.Elements.Count;
                gimData.Elements.Add(polygonElement);
                gimData.AddChildToParent(polygonIndex, polyRepIndex);

                AssignDWGColorsToGimMaterial(gimData, polygonIndex, solid);
            }
        }


        public static void ReadHatch(GimData gimData, uint polyRepIndex, Hatch hatch)
        {
            if (hatch.Paths.Count == 0)
            {
                return;
            }
            for (int i = 0; i < hatch.Paths.Count; i++)
            {
                Hatch.BoundaryPath boundaryPath = hatch.Paths[i];
                if (boundaryPath.Entities.Count == 0)
                {
                    continue;
                }
                //For now only fill Ellipses/Circles and maybe Arcs if I can
                for (int j = 0; j < boundaryPath.Entities.Count; j++)
                {
                    var currentEntity = boundaryPath.Entities[j];
                    switch (currentEntity.ObjectType)
                    {
                        case ObjectType.CIRCLE:
                            if (currentEntity is Circle circle)
                                FillCircle(gimData, polyRepIndex, circle);
                            break;
                        case ObjectType.ELLIPSE:
                            //
                            break;
                        case ObjectType.ARC:
                            if (currentEntity is ACadSharp.Entities.Arc arc)
                                FillArc(gimData, polyRepIndex, arc);
                            break;
                    }

                }
            }
        }

        private static void FillCircle(GimData gimData, uint polyRepIndex, Circle circle)
        {
            var center = DWGHelpers.XYZtoVect(circle.Center);
            var radius = circle.Radius;
            List<Vect> points = new();
            double currentAngle = 0;
            Vect direction1 = new(1, 0, 0);
            Vect direction2 = new(0, 1, 0);
            List<Triangle> triangles = new();

            //var endingPoint = center + radius * Math.Cos(Math.PI * 2) * direction1 + radius * Math.Sin(Math.PI * 2) * direction2;
            while(currentAngle < Math.PI * 2)
            {
                var startingPoint = center + radius * Math.Cos(currentAngle) * direction1 + radius * Math.Sin(currentAngle) * direction2;
                points.Add(startingPoint);
                currentAngle += Math.PI / 16;
            }

            for(int i = 0; i< points.Count;i++)
            {
                 int nextIndex = (i + 1) % points.Count;
                Triangle triangle = new(center, points[i], points[nextIndex]);
                triangles.Add(triangle);
            }
            TriangleMesh triangleMesh = new() { Triangles = triangles };
            GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
            triangleMesh.ApplyToGimDataPolygon(polygonElement);

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);
            AssignDWGColorsToGimMaterial(gimData, polygonIndex, circle);

        }


        private static void FillArc(GimData gimData, uint polyRepIndex, ACadSharp.Entities.Arc arc)
        {

            var center = DWGHelpers.XYZtoVect(arc.Center);
            var radius = arc.Radius;
            List<Vect> points = new();
            double currentAngle = arc.StartAngle;
            Vect direction1 = new(1, 0, 0);
            Vect direction2 = new(0, 1, 0);
            List<Triangle> triangles = new();
            double endAngle = arc.EndAngle;
            if (endAngle < currentAngle)
                endAngle += Math.PI * 2;
            //var endingPoint = center + radius * Math.Cos(Math.PI * 2) * direction1 + radius * Math.Sin(Math.PI * 2) * direction2;
            while (currentAngle < endAngle)
            {
                var startingPoint = center + radius * Math.Cos(currentAngle) * direction1 + radius * Math.Sin(currentAngle) * direction2;
                points.Add(startingPoint);
                currentAngle += Math.PI / 16;
            }
            if (currentAngle != endAngle)
            {
                var endingPoint = center + radius * Math.Cos(endAngle) * direction1 + radius * Math.Sin(endAngle) * direction2;
                points.Add(endingPoint);
            }


            for (int i = 0; i < points.Count; i++)
            {
                int nextIndex = (i + 1) % points.Count;
                Triangle triangle = new(center, points[i], points[nextIndex]);
                triangles.Add(triangle);
            }
            TriangleMesh triangleMesh = new() { Triangles = triangles };
            GimElement polygonElement = new() { ElementType = GimElementType.P_polygon };
            triangleMesh.ApplyToGimDataPolygon(polygonElement);

            uint polygonIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polygonElement);
            gimData.AddChildToParent(polygonIndex, polyRepIndex);
            AssignDWGColorsToGimMaterial(gimData, polygonIndex, arc);

        }


        private static GimElement MeshCircularOutline(Vect center, double startAngle, double endAngle, double radius, Vect normal)
        {
            return MeshCircularOutline(center, startAngle, endAngle, radius, radius, normal);
        }
        private static GimElement MeshCircularOutline(Vect center, double startAngle, double endAngle, double majorRadius, double minorRadius, Vect normal)
        {
            int segmentCount = FULL_CIRCLE_SEGMENT_COUNT;
            double segmentIncrement = TWOPI / segmentCount;
            double halfWidth = DEFAULT_HALF_WIDTh;

            Vect xDirection = (new Vect(0, 1, 0)) ^ normal;
            Vect yDirection = normal ^ xDirection;

            var cos = Math.Cos; var sin = Math.Sin;

            // A is the outside bound, B is in the inner bound.
            double majorRadiusA = majorRadius + halfWidth, minorRadiusA = minorRadius + halfWidth;
            double majorRadiusB = majorRadius - halfWidth, minorRadiusB = minorRadius - halfWidth;

            Vect previousA = center + (majorRadiusA * cos(startAngle) * xDirection + minorRadiusA * sin(startAngle) * yDirection);
            Vect previousB = center + (majorRadiusB * cos(startAngle) * xDirection + minorRadiusB * sin(startAngle) * yDirection);

            List<Triangle> triangles = new();

            for (double angle = startAngle; angle < endAngle; angle += segmentIncrement)
            {
                var pointA = previousA;
                var pointB = previousB;

                var nextAngle = angle + segmentIncrement;
                if(nextAngle > endAngle) nextAngle = endAngle;

                Vect nextA = center + (majorRadiusA * cos(nextAngle) * xDirection + minorRadiusA * sin(nextAngle) * yDirection);
                Vect nextB = center + (majorRadiusB * cos(nextAngle) * xDirection + minorRadiusB * sin(nextAngle) * yDirection);

                triangles.Add(new(pointA, pointB, nextA));
                triangles.Add(new(pointB, nextA, nextB));

                previousA = nextA; previousB = nextB;
            }

            TriangleMesh triangleMesh = new() { Triangles = triangles };

            GimElement polygonElement = new()
            {
                ElementType = GimElementType.P_polygon
            };

            triangleMesh.ApplyToGimDataPolygon(polygonElement);

            return polygonElement;
        }

        private static void AssignDWGColorsToGimMaterial(GimData gimData, uint polygonIndex, Entity faceMesh)
        {
            Color color = faceMesh.Color;
            Color layerColor = faceMesh.Layer.Color;
            if (!color.IsByBlock && !color.IsByLayer)
            {
                GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
                int gimMaterialIndex = gimData.Elements.Count;
                gimData.Elements.Add(gimMaterialElement);
                gimData.AddChildToParent((uint)gimMaterialIndex, (uint)polygonIndex);
                float red = color.R / 255f;
                float blue = color.B / 255f;
                float green = color.G / 255f;
                GimColour gimColourDiffuse = new(1, red, green, blue);

                GimColour gimColourBase = new(1, red, green, blue);
                int resultingDiffuseColor = gimColourDiffuse.ToInt();
                int resultingBaseColor = gimColourBase.ToInt();

                gimMaterialElement.DiffuseColour = resultingDiffuseColor;
                gimMaterialElement.BaseColour = resultingBaseColor;
            }
            if (color.IsByLayer)
            {
                GimMaterial gimMaterialElement = new() { elementType = GimElementType.Material };
                int gimMaterialIndex = gimData.Elements.Count;
                gimData.Elements.Add(gimMaterialElement);
                gimData.AddChildToParent((uint)gimMaterialIndex, (uint)polygonIndex);
                float red = layerColor.R / 255f;
                float blue = layerColor.B / 255f;
                float green = layerColor.G / 255f;
                GimColour gimColourDiffuse = new(1, red, green, blue);

                GimColour gimColourBase = new(1, red, green, blue);
                int resultingDiffuseColor = gimColourDiffuse.ToInt();
                int resultingBaseColor = gimColourBase.ToInt();

                gimMaterialElement.DiffuseColour = resultingDiffuseColor;
                gimMaterialElement.BaseColour = resultingBaseColor;
            }
        }
    }
}
