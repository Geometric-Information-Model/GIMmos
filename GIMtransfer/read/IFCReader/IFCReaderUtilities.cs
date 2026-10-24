using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeometryGym.Ifc;
using GIMcompress;
using GIMcompress.Shrinkwrap;

namespace GIMtransfer.read.IFCReader
{
    public class IFCReaderUtilities
    {

        public static List<Triangle> CreateTwoTrianglesFromExtrudedFace(List<Vect> points)
        {
            List<Triangle> resultingTriangles = new();
            Triangle triangle1 = new(points[0], points[1], points[2]);
            resultingTriangles.Add(triangle1);
            Triangle triangle2 = new(points[2], points[3], points[0]);
            resultingTriangles.Add(triangle2);
            return resultingTriangles;
        }

        public static int CheckIfPointInsideTriangles(List<Vect> points, List<Triangle> triangles)
        {
            int count = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Vect currentPoint = points[i];
                foreach (var triangle in triangles)
                {

                    Triangle temp1 = new(currentPoint, triangle.Vertex1, triangle.Vertex2);
                    Triangle temp2 = new(currentPoint, triangle.Vertex1, triangle.Vertex3);
                    Triangle temp3 = new(currentPoint, triangle.Vertex2, triangle.Vertex3);

                    //getting the area of the original triangle and then compare it to the ones we just created
                    double area1 = triangle.GetArea3D();
                    double area2 = temp1.GetArea3D();
                    double area3 = temp2.GetArea3D();
                    double area4 = temp3.GetArea3D();
                    int margin = 3;
                    double subAreasSum = area2 + area3 + area4;

                    double difference = Math.Round(area1 - subAreasSum, margin);

                    if (difference == 0)
                    {
                        count++;
                        break;
                    }

                }
            }
            return count;
        }

        public static List<List<Vect>> RemoveDuplicates(List<List<Vect>> list)
        {
            List<List<Vect>> uniqueLists = new List<List<Vect>>();

            foreach (var currentList in list)
            {
                bool isDuplicate = false;

                foreach (var uniqueList in uniqueLists)
                {
                    if (CheckIfListIsEqual(currentList, uniqueList))
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    uniqueLists.Add(currentList);
                }
            }

            return uniqueLists;

        }

        public static Matrix Transform2DCoordsInto3D(IfcAxis2Placement2D position)
        {
            
            Vect xAxis2D = new(position.RefDirection.DirectionRatioX, position.RefDirection.DirectionRatioY);
            Vect yAxis2D = new(-xAxis2D[1], xAxis2D[0]);
            Vect xAxis3D = new(xAxis2D[0], xAxis2D[1], 0);
            Vect yAxis3D = new(yAxis2D[0], yAxis2D[1],0);
            Vect zAxis3D = xAxis3D ^ yAxis3D;
            Matrix direction = new(xAxis3D, yAxis3D, zAxis3D);
            return direction;
        }



        private static bool CheckIfListIsEqual(List<Vect> list1, List<Vect> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            // Check if all elements in list1 have a matching element in list2 (ignoring order)
            foreach (var v1 in list1)
            {
                if (!list2.Any(v2 => v2[0] == v1[0] && v2[1] == v1[1] && v2[2] == v1[2]))
                    return false;
            }

            // Also check the reverse to ensure all elements in list2 are in list1
            foreach (var v2 in list2)
            {
                if (!list1.Any(v1 => v1[0] == v2[0] && v1[1] == v2[1] && v1[2] == v2[2]))
                    return false;
            }

            return true;
        }

        public static List<Vect> CreateListOfPointsFromRectangle(IfcRectangleProfileDef rectangleDef, Matrix resultingTransform)
        {
            List<Vect> vectList = new();
            //Prepare the variables needed: center of rectangle and the 2 halves needed for the movements towards the end points
            double xTransition = rectangleDef.XDim / 2;
            double yTransition = rectangleDef.YDim / 2;
            IfcAxis2Placement2D position = rectangleDef.Position;
            Vect center = new Vect(position.Location.CoordinateX, position.Location.CoordinateY, 0);
            center = center * resultingTransform;
            Vect firstPoint = new Vect(center[0] - xTransition, center[1] + yTransition, 0);
            vectList.Add(firstPoint);
            Vect secondPoint = new Vect(center[0] + xTransition, center[1] + yTransition, 0);
            vectList.Add(secondPoint);
            Vect thirdPoint = new Vect(center[0] + xTransition, center[1] - yTransition, 0);
            vectList.Add(thirdPoint);
            Vect finalPoint = new Vect(center[0] - xTransition, center[1] - yTransition, 0);
            vectList.Add(finalPoint);
            return vectList;
        }

        public static List<Vect> CreateListOfPointsFromRectangle(IfcRectangleProfileDef rectangleDef, Matrix finalTransform, Vect localPosition)
        {
            List<Vect> vectList = new();
            //Prepare the variables needed: center of rectangle and the 2 halves needed for the movements towards the end points
            double xTransition = rectangleDef.XDim / 2;
            double yTransition = rectangleDef.YDim / 2;
            IfcAxis2Placement2D position = rectangleDef.Position;
            Vect center = new Vect(position.Location.CoordinateX, position.Location.CoordinateY, 0);
            Vect firstPoint = new Vect(center[0] - xTransition, center[1] + yTransition, 0);
            var temp1 = firstPoint * finalTransform;
            Vect finalFirstPoint = new(temp1[0] + localPosition[0], temp1[1] + localPosition[1], temp1[2] + localPosition[2]);
            vectList.Add(finalFirstPoint);
            Vect secondPoint = new Vect(center[0] + xTransition, center[1] + yTransition, 0);
            var temp2 = secondPoint * finalTransform;
            Vect finalSecondtPoint = new(temp2[0] + localPosition[0], temp2[1] + localPosition[1], temp2[2] + localPosition[2]);
            vectList.Add(finalSecondtPoint);
            Vect thirdPoint = new Vect(center[0] + xTransition, center[1] - yTransition, 0);
            var temp3 = thirdPoint * finalTransform;
            Vect finalThirdtPoint = new(temp3[0] + localPosition[0], temp3[1] + localPosition[1], temp3[2] + localPosition[2]);
            vectList.Add(finalThirdtPoint);
            Vect finalPoint = new Vect(center[0] - xTransition, center[1] - yTransition, 0);
            var temp4 = finalPoint * finalTransform;
            Vect finalFinalPoint = new(temp4[0] + localPosition[0], temp4[1] + localPosition[1], temp4[2] + localPosition[2]);
            vectList.Add(finalFinalPoint);
            return vectList;

        }

        public static Matrix GetObjectTransformation(IfcAxis2Placement3D relativePlacement)
        {
            Vect zAxisGIM = new(0, 0, 0);
            Vect xAxisGIM = new(0, 0, 0);
            var zAxis = relativePlacement.Axis;
            var xAxis = relativePlacement.RefDirection;
            if (zAxis == null)
                zAxisGIM = new(0, 0, 1);
            else
                zAxisGIM = new(zAxis.DirectionRatioX, zAxis.DirectionRatioY, zAxis.DirectionRatioZ);
            if (xAxis == null)
                xAxisGIM = new(1, 0, 0);
            else
                xAxisGIM = new(xAxis.DirectionRatioX, xAxis.DirectionRatioY, xAxis.DirectionRatioZ);
            Vect yAxisGIM = zAxisGIM ^ xAxisGIM;
            var normA = xAxisGIM.Norm;
            var normB = yAxisGIM.Norm;
            var normC = zAxisGIM.Norm;
            var finalNorm = 1 - normA * normB * normC;
            Matrix transform = new(xAxisGIM, yAxisGIM, zAxisGIM);
            return transform;
        }

        public static void GetObjectTransformation(IfcLocalPlacement ifcLocalPlacement, ref Matrix matrix)
        {
            var relativePlacement = (IfcAxis2Placement3D)ifcLocalPlacement.RelativePlacement;
            var zAxis = relativePlacement.Axis;
            var xAxis = relativePlacement.RefDirection;
            Vect zAxisGIM = new(0, 0, 0);
            Vect xAxisGIM = new(0, 0, 0);
            if (zAxis == null)
                zAxisGIM = new(0, 0, 1);
            else
                zAxisGIM = new(zAxis.DirectionRatioX, zAxis.DirectionRatioY, zAxis.DirectionRatioZ);
            if (xAxis == null)
                xAxisGIM = new(1, 0, 0);
            else
                xAxisGIM = new(xAxis.DirectionRatioX, xAxis.DirectionRatioY, xAxis.DirectionRatioZ);
            Vect yAxisGIM = zAxisGIM ^ xAxisGIM;
            Matrix transform = new(xAxisGIM, yAxisGIM, zAxisGIM);
            matrix = matrix * transform;
            if (ifcLocalPlacement.PlacementRelTo != null)
            {
                GetObjectTransformation((IfcLocalPlacement)ifcLocalPlacement.PlacementRelTo, ref matrix);
            }

        }


        public static Vect GetLocalObjectPlacement(IfcAxis2Placement3D ifcLocalPlacement)
        {
            // Get the current placement

            var locationIFC = ifcLocalPlacement.Location;
            Vect locationGIM = new(locationIFC.CoordinateX, locationIFC.CoordinateY, locationIFC.CoordinateZ);
            return locationGIM;
            // Add current placement to the point



        }


        public static void GetGlobalObjectPlacement(ref Vect point, IfcLocalPlacement ifcLocalPlacement, ref Matrix rotation)
        {
            if (ifcLocalPlacement.PlacementRelTo != null)
            {
                GetGlobalObjectPlacement(ref point, (IfcLocalPlacement)ifcLocalPlacement.PlacementRelTo, ref rotation);
            }
            // Get the current placement
            Vect zAxisGIM = new(0, 0, 0);
            Vect xAxisGIM = new(0, 0, 0);
            var relativePlacement = (IfcAxis2Placement3D)ifcLocalPlacement.RelativePlacement;
            var locationIFC = relativePlacement.Location;
            Vect locationGIM = new(locationIFC.CoordinateX, locationIFC.CoordinateY, locationIFC.CoordinateZ);
            locationGIM = locationGIM * rotation;
            var zAxis = relativePlacement.Axis;
            var xAxis = relativePlacement.RefDirection;
            if (zAxis != null)
                zAxisGIM = new(zAxis.DirectionRatioX, zAxis.DirectionRatioY, zAxis.DirectionRatioZ);
            else
                zAxisGIM = new(0, 0, 1);

            if (xAxis != null)
                xAxisGIM = new(xAxis.DirectionRatioX, xAxis.DirectionRatioY, xAxis.DirectionRatioZ);
            else
                xAxisGIM = new(1, 0, 0);

            Vect yAxisGIM = zAxisGIM ^ xAxisGIM;
            Matrix transform = new(xAxisGIM, yAxisGIM, zAxisGIM);
            rotation = transform * rotation;
            // Add current placement to the point
            point += locationGIM;


        }

        public static void GetGlobalObjectPlacement(ref Vect point, IfcLocalPlacement ifcLocalPlacement)
        {
            // Get the current placement
            var relativePlacement = (IfcAxis2Placement3D)ifcLocalPlacement.RelativePlacement;
            var locationIFC = relativePlacement.Location;
            Vect locationGIM = new(locationIFC.CoordinateX, locationIFC.CoordinateY, locationIFC.CoordinateZ);

            // Add current placement to the point
            point += locationGIM;

            // Recursively check if there's a parent placement
            if (ifcLocalPlacement.PlacementRelTo != null)
            {
                GetGlobalObjectPlacement(ref point, (IfcLocalPlacement)ifcLocalPlacement.PlacementRelTo);
            }
        }


        public static List<Vect> GetPointsFromIShape(Vect center, double filletRadius, double flangeThickness, double overallDepth, double overallWidth, double webThickness)
        {
            List<Vect> points = new List<Vect>();
            Vect point1 = new(overallWidth / 2, overallDepth / 2, 0);
            points.Add(point1);
            Vect point2 = new(overallWidth / 2, (overallDepth / 2 - flangeThickness), 0);
            points.Add(point2);
            Vect point3 = new((webThickness / 2 + filletRadius), (overallDepth / 2 - flangeThickness), 0);
            points.Add(point3);
            Vect point4 = new(webThickness / 2, (overallDepth / 2 - filletRadius - flangeThickness), 0);
            points.Add(point4);
            Vect point5 = new(point4[0], -point4[1], 0);
            points.Add(point5);
            Vect point6 = new(point3[0], -point3[1], 0);
            points.Add(point6);
            Vect point7 = new(point2[0], -point2[1], 0);
            points.Add(point7);
            Vect point8 = new(point1[0], -point1[1], 0);
            points.Add(point8);
            Vect point9 = -point1;
            points.Add(point9);
            Vect point10 = -point2;
            points.Add(point10);
            Vect point11 = -point3;
            points.Add(point11);
            Vect point12 = -point4;
            points.Add(point12);
            Vect point13 = -point5;
            points.Add(point13);
            Vect point14 = -point6;
            points.Add(point14);
            Vect point15 = -point7;
            points.Add(point15);
            Vect point16 = -point8;
            points.Add(point16);




            return points;
        }

        public static List<Vect> CreatePointsFromPolyBound(IfcPolyline polyLine)
        {
            List<Vect> points = new List<Vect>();
            for (int i = 0; i < polyLine.Points.Count; i++)
            {
                var currentPoint = polyLine.Points[i];
                points.Add(new(currentPoint.CoordinateX, currentPoint.CoordinateY, 0));
            }

            List<Vect> uniquePoints = new List<Vect>();
            HashSet<string> seen = new HashSet<string>();

            foreach (var point in points)
            {
                string key = $"{point[0]},{point[1]},{point[2]}"; // Unique key for each point

                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniquePoints.Add(point);
                }
            }

            points = uniquePoints;


            return points;
        }


        public static List<Vect> CreatePointsForHalfSpace(Vect center, Matrix planeOrientation)
        {
            List<Vect> points = new();
            Vect xBasisVector = planeOrientation[0];
            Vect yBasisVector = planeOrientation[1];
            int distance = 500;
            Vect point1 = center + distance * xBasisVector + distance * yBasisVector;
            points.Add(point1);
            Vect point2 = center - distance * xBasisVector + distance * yBasisVector;
            points.Add(point2);
            Vect point3 = center - distance * xBasisVector - distance * yBasisVector;
            points.Add(point3);
            Vect point4 = center + distance * xBasisVector - distance * yBasisVector;
            points.Add(point4);
            return points;
        }


        public static List<Vect> CreatePointsForHalfSpace(Vect center, Matrix planeOrientation, List<Vect> boundingBox)
        {
            double sumX=0;
            double sumY = 0;
            for(int i = 0; i < boundingBox.Count;i++)
            {
                Vect currentPoint = boundingBox[i];
                sumX += currentPoint[0];
                sumY += currentPoint[1];
            }
            Vect temp = new(sumX/4, sumY / 4);
            
            double finalDist = (temp - boundingBox[0]).Norm;
            Vect newCenter = new(temp[0], temp[1], center[2]);
            List<Vect> points = new();
            Vect xBasisVector = planeOrientation[0];
            Vect yBasisVector = planeOrientation[1];
            double halfX = (boundingBox[1][0] - boundingBox[0][0]) / 2; 
            double halfY = (boundingBox[2][1] - boundingBox[1][1]) / 2;

            Vect point1 = newCenter + (halfY * xBasisVector) + (halfX * yBasisVector);
            points.Add(point1);
            Vect point2 = newCenter - (halfY * xBasisVector) + (halfX * yBasisVector);
            points.Add(point2);
            Vect point3 = newCenter - (halfY * xBasisVector) - (halfX * yBasisVector);
            points.Add(point3);
            Vect point4 = newCenter + (halfY * xBasisVector) - (halfX * yBasisVector);
            points.Add(point4);


            return points;

        }

        public static List<Vect> GetPointsFromExtrusion(IfcArbitraryClosedProfileDef closedProfileDef, double depth, Vect extrudedDirection)
        {

            List<Vect> originalSetOfPoints = new();

            var pointsList = closedProfileDef.OuterCurve;
            if (pointsList is IfcPolyline)
            {
                var polyLine = (IfcPolyline)pointsList;
                var polyLineList = polyLine.Points.ToList();
                if (polyLineList.First() == polyLineList.Last())
                {
                    polyLineList.Remove(polyLineList.Last());
                }
                //After having our list of unique points, we start prepping up the new points by converting them into a list of Vects


                for (int i = 0; i < polyLineList.Count; i++)
                {
                    var currentPoint = polyLineList[i];
                    if (currentPoint.Coordinates.Count < 3)
                        originalSetOfPoints.Add(new(currentPoint.CoordinateX, currentPoint.CoordinateY, 0));
                    else
                        originalSetOfPoints.Add(new(currentPoint.CoordinateX, currentPoint.CoordinateY, currentPoint.CoordinateZ));

                }
                var norm = extrudedDirection.Norm;
                Vect temp = new(extrudedDirection[0] / norm, extrudedDirection[1] / norm, extrudedDirection[2] / norm);
                Vect finalDirection = new(0, 0, 0);
                finalDirection[0] = temp[0] * depth;
                finalDirection[1] = temp[1] * depth;
                finalDirection[2] = temp[2] * depth;
                //And then preparing another list with the modified values thanks to the depth + extruded direction
                for (int i = 0; i < polyLineList.Count; i++)
                {
                    var currentPoint = polyLineList[i];
                    Vect extrudedPoint = new(0, 0, 0);
                    if (currentPoint.Coordinates.Count == 3)
                    {
                        extrudedPoint[0] = currentPoint.CoordinateX + finalDirection[0];
                        extrudedPoint[1] = currentPoint.CoordinateY + finalDirection[1];
                        extrudedPoint[2] = currentPoint.CoordinateZ + finalDirection[2];
                    }
                    else
                    {
                        extrudedPoint[0] = currentPoint.CoordinateX + finalDirection[0];
                        extrudedPoint[1] = currentPoint.CoordinateY + finalDirection[1];
                        extrudedPoint[2] = finalDirection[2];
                    }
                    originalSetOfPoints.Add(extrudedPoint);
                }
            }

            return originalSetOfPoints;

        }

        public static List<Vect> Get2DBoundingBox(List<Vect> points, Matrix transform)
        {
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                Vect currentPoint = points[i] * transform;
                double currentX = currentPoint[0];
                double currentY = currentPoint[1];
                if (currentX < minX) minX = currentX;
                if (currentX > maxX) maxX = currentX;
                if (currentY < minY) minY = currentY;
                if (currentY > maxY) maxY = currentY;
            }
            List<Vect> boundingBox = new List<Vect>
                {    
                    new Vect(minX-1, maxY+1), // Top-left corner
                    new Vect(maxX+1, maxY+1), // Top-right corner
                    new Vect(maxX+1, minY-1), // Bottom-right corner
                    new Vect(minX-1, minY-1) // Bottom-left corner     
                };
            return boundingBox;
        }


        public static int GetConstantAxis(List<Vect> hole)
        {
            if (hole.Count == 0)
                return -1; 

            bool sameX = hole.All(v => v[0] == hole[0][0]);
            bool sameY = hole.All(v => v[1] == hole[0][1]);
            bool sameZ = hole.All(v => v[2] == hole[0][2]);

            if (sameX) return 0;
            if (sameY) return 1;
            if (sameZ) return 2;

            return -1;
        }


        public static void CreateHoleFromIFCOpening(List<List<Vect>> holes, List<Vect> points, int baseAxis, GimData gimData, int parentID, Vect generalLocation, Matrix localTransform)
        {
            int planeBaseAxis = GetConstantAxis(points);
            if (planeBaseAxis != baseAxis)
                return;
            
            if (baseAxis == 0)
            {
                for (int i = 0; i < holes.Count; i = i + 2)
                {
                    List<Vect> firstHole = holes[i];
                    Vect firstHolePoint = firstHole[0];
                    List<Vect> secondHole = holes[i + 1];
                    Vect secondHolePoint = secondHole[0];
                    Vect point = points[0];
                    if ((point[0] > firstHolePoint[0] && point[0] < secondHolePoint[0]) || (point[0] < firstHolePoint[0] && point[0] > secondHolePoint[0]))
                    {
                        List<Vect> newPoints = new();
                        for (int j = 0; j < firstHole.Count; j++)
                        {
                            Vect currentPoint = firstHole[j];
                            Vect v = new(point[0], currentPoint[1], currentPoint[2]);
                            newPoints.Add(v);
                        }
                        IFCReadHoles.CreateHole(newPoints, gimData, parentID, generalLocation, localTransform);
                    }

                }
            }
            else if(baseAxis == 1)
            {
                for (int i = 0; i < holes.Count; i=i+2)
                {
                    List<Vect> firstHole = holes[i];
                    Vect firstHolePoint = firstHole[0];
                    List<Vect> secondHole = holes[i + 1];
                    Vect secondHolePoint = secondHole[0];
                    Vect point = points[0];
                    if ((point[1] > firstHolePoint[1] && point[1] < secondHolePoint[1]) || (point[1] < firstHolePoint[1] && point[1] > secondHolePoint[1]))
                    {
                        List<Vect> newPoints = new();
                        for (int j = 0; j< firstHole.Count; j++)
                        {
                            Vect currentPoint = firstHole[j];
                            Vect v = new(currentPoint[0], point[1], currentPoint[2]);
                            newPoints.Add(v);
                        }
                        IFCReadHoles.CreateHole(newPoints, gimData, parentID, generalLocation, localTransform);
                    }

                }
            }
            else if(baseAxis == 2)
            {
                for (int i = 0; i < holes.Count; i = i + 2)
                {
                    List<Vect> firstHole = holes[i];
                    Vect firstHolePoint = firstHole[0];
                    List<Vect> secondHole = holes[i + 1];
                    Vect secondHolePoint = secondHole[0];
                    Vect point = points[0];
                    if ((point[2] > firstHolePoint[2] && point[2] < secondHolePoint[2]) || (point[2] < firstHolePoint[2] && point[2] > secondHolePoint[2]))
                    {
                        List<Vect> newPoints = new();
                        for (int j = 0; j < firstHole.Count; j++)
                        {
                            Vect currentPoint = firstHole[j];
                            Vect v = new(currentPoint[0], currentPoint[1], point[2]);
                            newPoints.Add(v);
                        }
                        IFCReadHoles.CreateHole(newPoints, gimData, parentID, generalLocation, localTransform);
                    }

                }
            }
            
        }


    } 
}
