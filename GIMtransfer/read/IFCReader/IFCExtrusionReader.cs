using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeometryGym.Ifc;
using GIMcompress;

namespace GIMtransfer.read.IFCReader
{
    public class IFCExtrusionReader
    {
        public static bool ReadExtursion(IfcRepresentationItem item, GimData gimData, int solidBodyElementIndex, Vect generalLocationGIM, Matrix transform, bool hasHoles,
            List<List<Vect>> holes, int baseAxis)
        {
            bool removed = false;
            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);
            //Idea is to make a dictionnary, containing each resulting plane from the extursion and their points
            var extrudedArea = (IfcExtrudedAreaSolid)item;
            double depth = extrudedArea.Depth;
            var extrudedDirection = extrudedArea.ExtrudedDirection;
            Vect extrudedDirectionGIM = new(extrudedDirection.DirectionRatioX, extrudedDirection.DirectionRatioY, extrudedDirection.DirectionRatioZ);
            var sweptArea = extrudedArea.SweptArea;
            var positionIFC = extrudedArea.Position;

            var localTransform = IFCReaderUtilities.GetObjectTransformation(positionIFC);
            var finalTransform = localTransform * transform;
            //finalTransform.ApplyToLocal(partElement.Locals[0]);
            var localPosition = IFCReaderUtilities.GetLocalObjectPlacement(positionIFC);
            localPosition = localPosition * transform;
            Vect resultingLocation = new(generalLocationGIM[0] + localPosition[0], generalLocationGIM[1] + localPosition[1], generalLocationGIM[2] + localPosition[2]);
            if (sweptArea is IfcArbitraryClosedProfileDef)
            {
                var closedProfileDef = (IfcArbitraryClosedProfileDef)sweptArea;
                //TODO: Make it so that this function always gets a list of Vect instead and create a function to create said list of points for all cases
                if (!hasHoles)
                    removed = GetPlanesDictionaryFromPoints(closedProfileDef, depth, extrudedDirectionGIM, gimData, shellElementIndex, resultingLocation, finalTransform, localTransform);
                else
                    try
                    {
                        removed = IFCReadHoles.ExtractPointsFromProfileDefAndCreatePlane(closedProfileDef, holes, depth, extrudedDirectionGIM, gimData, shellElementIndex, resultingLocation, finalTransform, baseAxis);
                    }
                    catch
                    {
                        removed = true;
                    }

            }
            else if (sweptArea is IfcRectangleProfileDef)
            {

                var rectangleDef = (IfcRectangleProfileDef)sweptArea;
                var innerTransform = IFCReaderUtilities.Transform2DCoordsInto3D(rectangleDef.Position);
                Matrix identity = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                if(innerTransform != identity && localTransform != identity)
                {
                    innerTransform = innerTransform * localTransform;
                }
                var resultingTransform = innerTransform * finalTransform;
                //var position2DIFC = rectangleDef.Position.Location;
                //Vect position2DGIM = new(position2DIFC.CoordinateX, position2DIFC.CoordinateY, 0);
                //resultingLocation = resultingLocation + position2DGIM;
                List<Vect> points = IFCReaderUtilities.CreateListOfPointsFromRectangle(rectangleDef, innerTransform);
                if (!hasHoles)
                    GetPlanesDictionaryFromPoints(points, depth, extrudedDirectionGIM, gimData, shellElementIndex, resultingLocation, resultingTransform);
                else
                    IFCReadHoles.ExtractPointsAndCreatePlane(points, holes, depth, extrudedDirectionGIM, resultingLocation, resultingTransform, gimData, shellElementIndex, baseAxis);
                

            }
            else if (sweptArea is IfcCircleProfileDef)
            {
               
                var circleDef = (IfcCircleProfileDef)sweptArea;
                CreateCylinderFromExtrusion(circleDef, depth, extrudedDirectionGIM, gimData, shellElementIndex, generalLocationGIM, finalTransform);
              
            }
            else if (sweptArea is IfcIShapeProfileDef)
            {
                string debug = "debug";
                IfcIShapeProfileDef iShape = (IfcIShapeProfileDef)sweptArea;
                double filletRadius = iShape.FilletRadius;
                double flangeThickness = iShape.FlangeThickness;
                double overallDepth = iShape.OverallDepth;
                double overallWidth = iShape.OverallWidth;
                double webThickness = iShape.WebThickness;
                var centerIFC = iShape.Position.Location;
                Vect centerGIM = new(centerIFC.CoordinateX, centerIFC.CoordinateY, 0);
                var innerTransform = IFCReaderUtilities.Transform2DCoordsInto3D(iShape.Position);
                var resultingTransform = innerTransform * finalTransform;
                var iShapePoints = IFCReaderUtilities.GetPointsFromIShape(centerGIM, filletRadius, flangeThickness, overallDepth, overallWidth, webThickness);
                GetPlanesDictionaryFromPoints(iShapePoints, depth, extrudedDirectionGIM, gimData, shellElementIndex, resultingLocation, resultingTransform);


            }
            else
            {
                string debug = "debug";
                removed = true;
            }
            return removed;
        }


        public static void GetPlanesDictionaryFromPoints(List<Vect> points, double depth, Vect extrudedDirection, GimData gimData, int parentID, Vect location, Matrix localTransform)
        {
            //First make sure that all of the points are unique, if the last point equals the first point, then it's a closed loop and thus we can remove it safely
            Dictionary<int, List<Vect>> planesDictionary = new();
            List<Vect> originalSetOfPoints = new();
            List<Vect> extrudedSetOfPoints = new();

            //After having our list of unique points, we start prepping up the new points by converting them into a list of Vects


            for (int i = 0; i < points.Count; i++)
            {
                var currentPoint = points[i];
                originalSetOfPoints.Add(currentPoint);

            }

            List<Vect> uniquePoints = new List<Vect>();
            HashSet<string> seen = new HashSet<string>();

            foreach (var point in originalSetOfPoints)
            {
                string key = $"{point[0]},{point[1]},{point[2]}"; // Unique key for each point

                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniquePoints.Add(point);
                }
            }

            originalSetOfPoints = uniquePoints;
            var norm = extrudedDirection.Norm;
            Vect temp = new(extrudedDirection[0] / norm, extrudedDirection[1] / norm, extrudedDirection[2] / norm);
            Vect finalDirection = new(0, 0, 0);
            finalDirection[0] = temp[0] * depth;
            finalDirection[1] = temp[1] * depth;
            finalDirection[2] = temp[2] * depth;
            //And then preparing another list with the modified values thanks to the depth + extruded direction
            for (int i = 0; i < points.Count; i++)
            {
                var currentPoint = points[i];
                Vect extrudedPoint = new(0, 0, 0);
                extrudedPoint[0] = currentPoint[0] + finalDirection[0];
                extrudedPoint[1] = currentPoint[1] + finalDirection[1];
                extrudedPoint[2] = currentPoint[2] + finalDirection[2];
                extrudedSetOfPoints.Add(extrudedPoint);
            }
            //Now that we have both lists ready, it's time to set up the dictionary, we get each point and its next (so a line basically)
            //Along with their extruded counterpart and put them in the Value of the dictonary
            List<Vect> uniqueExtrudedPoints = new List<Vect>();
            HashSet<string> seenExtruded = new HashSet<string>();

            foreach (var point in extrudedSetOfPoints)
            {
                string key = $"{point[0]},{point[1]},{point[2]}"; // Unique key for each point

                if (!seenExtruded.Contains(key))
                {
                    seenExtruded.Add(key);
                    uniqueExtrudedPoints.Add(point);
                }
            }

            extrudedSetOfPoints = uniqueExtrudedPoints;
            for (int i = 0; i < originalSetOfPoints.Count; i++)
            {
                int firstIndex = i;
                int nextIndex = (i + 1 + originalSetOfPoints.Count) % originalSetOfPoints.Count;
                List<Vect> listOfPointsToAdd = new();
                //We try to add the lines in an ordered manner so that it facilitates our task ahead
                var firstPoint = originalSetOfPoints[firstIndex];
                listOfPointsToAdd.Add(firstPoint);
                var nextPoint = originalSetOfPoints[nextIndex];
                listOfPointsToAdd.Add(nextPoint);
                var nextExtrudedPoint = extrudedSetOfPoints[nextIndex];
                listOfPointsToAdd.Add(nextExtrudedPoint);
                var firstExtrudedPoint = extrudedSetOfPoints[firstIndex];
                listOfPointsToAdd.Add(firstExtrudedPoint);

                //Add the entry in the Dictionary
                if (!planesDictionary.ContainsKey(firstIndex))
                {
                    planesDictionary[firstIndex] = new();
                }
                //Add the corresponding values
                planesDictionary[firstIndex] = listOfPointsToAdd;

            }
            //We add the final two planes now
            var count1 = planesDictionary.Count;
            if (!planesDictionary.ContainsKey(count1))
            {
                planesDictionary[count1] = new();
            }
            planesDictionary[count1] = originalSetOfPoints;
            var count2 = planesDictionary.Count;
            if (!planesDictionary.ContainsKey(count2))
            {
                planesDictionary[count2] = new();
            }
            planesDictionary[count2] = extrudedSetOfPoints;

            //And for the final step, we proceed to create our planes
            foreach (var entry in planesDictionary)
            {

                var currentList = entry.Value;
                IFCReadElements.CreatePlane(currentList, gimData, parentID, location, localTransform);

            }

        }

        public static bool GetPlanesDictionaryFromPoints(IfcArbitraryClosedProfileDef closedProfileDef, double depth, Vect extrudedDirection, GimData gimData, int parentID, Vect location, Matrix finalTransform, Matrix localTransform)
        {
            //First make sure that all of the points are unique, if the last point equals the first point, then it's a closed loop and thus we can remove it safely
            bool removed = false;
            Dictionary<int, List<Vect>> planesDictionary = new();
            List<Vect> originalSetOfPoints = new();
            List<Vect> extrudedSetOfPoints = new();
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
                List<Vect> uniquePoints = new List<Vect>();
                HashSet<string> seen = new HashSet<string>();

                foreach (var point in originalSetOfPoints)
                {
                    string key = $"{point[0]},{point[1]},{point[2]}"; // Unique key for each point

                    if (!seen.Contains(key))
                    {
                        seen.Add(key);
                        uniquePoints.Add(point);
                    }
                }

                originalSetOfPoints = uniquePoints;
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
                    extrudedSetOfPoints.Add(extrudedPoint);
                }
                //Now that we have both lists ready, it's time to set up the dictionary, we get each point and its next (so a line basically)
                //Along with their extruded counterpart and put them in the Value of the dictonary
                List<Vect> uniqueExtrudedPoints = new List<Vect>();
                HashSet<string> seenExtruded = new HashSet<string>();

                foreach (var point in extrudedSetOfPoints)
                {
                    string key = $"{point[0]},{point[1]},{point[2]}"; // Unique key for each point

                    if (!seenExtruded.Contains(key))
                    {
                        seenExtruded.Add(key);
                        uniqueExtrudedPoints.Add(point);
                    }
                }

                extrudedSetOfPoints = uniqueExtrudedPoints;
                for (int i = 0; i < originalSetOfPoints.Count; i++)
                {
                    int firstIndex = i;
                    int nextIndex = (i + 1 + originalSetOfPoints.Count) % originalSetOfPoints.Count;
                    List<Vect> listOfPointsToAdd = new();
                    //We try to add the lines in an ordered manner so that it facilitates our task ahead
                    var firstPoint = originalSetOfPoints[firstIndex];
                    listOfPointsToAdd.Add(firstPoint);
                    var nextPoint = originalSetOfPoints[nextIndex];
                    listOfPointsToAdd.Add(nextPoint);
                    var nextExtrudedPoint = extrudedSetOfPoints[nextIndex];
                    listOfPointsToAdd.Add(nextExtrudedPoint);
                    var firstExtrudedPoint = extrudedSetOfPoints[firstIndex];
                    listOfPointsToAdd.Add(firstExtrudedPoint);

                    //Add the entry in the Dictionary
                    if (!planesDictionary.ContainsKey(firstIndex))
                    {
                        planesDictionary[firstIndex] = new();
                    }
                    //Add the corresponding values
                    planesDictionary[firstIndex] = listOfPointsToAdd;



                }
                //We add the final two planes now
                var count1 = planesDictionary.Count;
                if (!planesDictionary.ContainsKey(count1))
                {
                    planesDictionary[count1] = new();
                }
                planesDictionary[count1] = originalSetOfPoints;
                var count2 = planesDictionary.Count;
                if (!planesDictionary.ContainsKey(count2))
                {
                    planesDictionary[count2] = new();
                }
                planesDictionary[count2] = extrudedSetOfPoints;

                //And for the final step, we proceed to create our planes
                foreach (var entry in planesDictionary)
                {

                    var currentList = entry.Value;
                    IFCReadElements.CreatePlane(currentList, gimData, parentID, location, finalTransform);

                }




            }
            else if (pointsList is IfcIndexedPolyCurve)
            {
                IfcIndexedPolyCurve indexedPolyCurve = (IfcIndexedPolyCurve)pointsList;
                IfcCartesianPointList2D points = (IfcCartesianPointList2D)indexedPolyCurve.Points;
                var pointList = points.CoordList;
                for (int i = 0; i < pointList.Count; i++)
                {
                    var currentPoint = pointList[i];
                    originalSetOfPoints.Add(new(currentPoint.Item1, currentPoint.Item2, 0));
                }
                var norm = extrudedDirection.Norm;
                Vect temp = new(extrudedDirection[0] / norm, extrudedDirection[1] / norm, extrudedDirection[2] / norm);
                Vect finalDirection = new(0, 0, 0);
                finalDirection[0] = temp[0] * depth;
                finalDirection[1] = temp[1] * depth;
                finalDirection[2] = temp[2] * depth;
                string debug = "debug";
                for (int i = 0; i < pointList.Count; i++)
                {
                    var currentPoint = pointList[i];
                    Vect extrudedPoint = new(0, 0, 0);


                    extrudedPoint[0] = currentPoint.Item1 + finalDirection[0];
                    extrudedPoint[1] = currentPoint.Item2 + finalDirection[1];
                    extrudedPoint[2] = finalDirection[2];

                    extrudedSetOfPoints.Add(extrudedPoint);
                }
                //Now that we have both lists ready, it's time to set up the dictionary, we get each point and its next (so a line basically)
                //Along with their extruded counterpart and put them in the Value of the dictonary

                for (int i = 0; i < originalSetOfPoints.Count; i++)
                {
                    int firstIndex = i;
                    int nextIndex = (i + 1 + originalSetOfPoints.Count) % originalSetOfPoints.Count;
                    List<Vect> listOfPointsToAdd = new();
                    //We try to add the lines in an ordered manner so that it facilitates our task ahead
                    var firstPoint = originalSetOfPoints[firstIndex];
                    listOfPointsToAdd.Add(firstPoint);
                    var nextPoint = originalSetOfPoints[nextIndex];
                    listOfPointsToAdd.Add(nextPoint);
                    var nextExtrudedPoint = extrudedSetOfPoints[nextIndex];
                    listOfPointsToAdd.Add(nextExtrudedPoint);
                    var firstExtrudedPoint = extrudedSetOfPoints[firstIndex];
                    listOfPointsToAdd.Add(firstExtrudedPoint);

                    //Add the entry in the Dictionary
                    if (!planesDictionary.ContainsKey(firstIndex))
                    {
                        planesDictionary[firstIndex] = new();
                    }
                    //Add the corresponding values
                    planesDictionary[firstIndex] = listOfPointsToAdd;



                }
                //We add the final two planes now
                var count1 = planesDictionary.Count;
                if (!planesDictionary.ContainsKey(count1))
                {
                    planesDictionary[count1] = new();
                }
                planesDictionary[count1] = originalSetOfPoints;
                var count2 = planesDictionary.Count;
                if (!planesDictionary.ContainsKey(count2))
                {
                    planesDictionary[count2] = new();
                }
                planesDictionary[count2] = extrudedSetOfPoints;

                //And for the final step, we proceed to create our planes
                foreach (var entry in planesDictionary)
                {

                    var currentList = entry.Value;
                    IFCReadElements.CreatePlane(currentList, gimData, parentID, location, finalTransform);

                }
            }
            else if(pointsList is IfcCompositeCurve)
            {
                IfcCompositeCurve compositeCurve = (IfcCompositeCurve)pointsList;
                originalSetOfPoints = GetPointsFromCompositeCurveLines(compositeCurve);
                //var circles = GetCirclesFromCompositeCurveTrimmed(compositeCurve);
                //foreach (var circle in circles)
                //{
                //    var list = circle.Value;
                //    CreateCylinderFromExtrusion(circle.Key, depth, extrudedDirection, gimData, parentID, location, finalTransform, localTransform, list[0], list[1]);
                //}
                if (originalSetOfPoints.Count == 0)
                {
                    removed = true;
                    return removed;

                }
                if (originalSetOfPoints.Count > 0)
                {
                    var norm = extrudedDirection.Norm;
                    Vect temp = new(extrudedDirection[0] / norm, extrudedDirection[1] / norm, extrudedDirection[2] / norm);
                    Vect finalDirection = new(0, 0, 0);
                    finalDirection[0] = temp[0] * depth;
                    finalDirection[1] = temp[1] * depth;
                    finalDirection[2] = temp[2] * depth;
                    string debug = "debug";
                    for (int i = 0; i < originalSetOfPoints.Count; i++)
                    {
                        var currentPoint = originalSetOfPoints[i];
                        Vect extrudedPoint = new(0, 0, 0);


                        extrudedPoint[0] = currentPoint[0] + finalDirection[0];
                        extrudedPoint[1] = currentPoint[1] + finalDirection[1];
                        extrudedPoint[2] = finalDirection[2];

                        extrudedSetOfPoints.Add(extrudedPoint);
                    }
                    //Now that we have both lists ready, it's time to set up the dictionary, we get each point and its next (so a line basically)
                    //Along with their extruded counterpart and put them in the Value of the dictonary

                    for (int i = 0; i < originalSetOfPoints.Count; i++)
                    {
                        int firstIndex = i;
                        int nextIndex = (i + 1 + originalSetOfPoints.Count) % originalSetOfPoints.Count;
                        List<Vect> listOfPointsToAdd = new();
                        //We try to add the lines in an ordered manner so that it facilitates our task ahead
                        var firstPoint = originalSetOfPoints[firstIndex];
                        listOfPointsToAdd.Add(firstPoint);
                        var nextPoint = originalSetOfPoints[nextIndex];
                        listOfPointsToAdd.Add(nextPoint);
                        var nextExtrudedPoint = extrudedSetOfPoints[nextIndex];
                        listOfPointsToAdd.Add(nextExtrudedPoint);
                        var firstExtrudedPoint = extrudedSetOfPoints[firstIndex];
                        listOfPointsToAdd.Add(firstExtrudedPoint);

                        //Add the entry in the Dictionary
                        if (!planesDictionary.ContainsKey(firstIndex))
                        {
                            planesDictionary[firstIndex] = new();
                        }
                        //Add the corresponding values
                        planesDictionary[firstIndex] = listOfPointsToAdd;



                    }
                    //We add the final two planes now
                    var count1 = planesDictionary.Count;
                    if (!planesDictionary.ContainsKey(count1))
                    {
                        planesDictionary[count1] = new();
                    }
                    planesDictionary[count1] = originalSetOfPoints;
                    var count2 = planesDictionary.Count;
                    if (!planesDictionary.ContainsKey(count2))
                    {
                        planesDictionary[count2] = new();
                    }
                    planesDictionary[count2] = extrudedSetOfPoints;

                    //And for the final step, we proceed to create our planes
                    foreach (var entry in planesDictionary)
                    {

                        var currentList = entry.Value;
                        IFCReadElements.CreatePlane(currentList, gimData, parentID, location, finalTransform);

                    }
                }
            }
            else
            {
                string debug = "debug";
                removed = true;
            }

            return removed;

        }



        public static List<Vect> GetPointsFromCompositeCurveLines(IfcCompositeCurve pointsList)
        {
            List<Vect> finalPoints = new();
            var curveSegments = pointsList.Segments;
            for(int i = 0; i < curveSegments.Count; i++)
            {
                IfcCompositeCurveSegment curveSegment = (IfcCompositeCurveSegment)curveSegments[i];
                if(curveSegment.ParentCurve is IfcPolyline)
                {
                    IfcPolyline parentCurve = (IfcPolyline)curveSegment.ParentCurve;
                    for(int j = 0; j < parentCurve.Points.Count;j++)
                    {
                        if(curveSegment.Transition.ToString() == "CONTINUOUS" && i == 0)
                        {
                            var pointIFC = parentCurve.Points[j];
                            Vect pointGIM = new(0, 0, 0);
                            if (pointIFC.Coordinates.Count == 2)
                                pointGIM = new(pointIFC.CoordinateX, pointIFC.CoordinateY, 0);
                            else
                                pointGIM = new(pointIFC.CoordinateX, pointIFC.CoordinateY, pointIFC.CoordinateZ);
                            finalPoints.Add(pointGIM);
                        }
                        else if(curveSegment.Transition.ToString() == "CONTINUOUS"  && (i != curveSegments.Count - 1 && i!=0))
                        {
                            IfcCompositeCurveSegment previousCurveSegment = (IfcCompositeCurveSegment)curveSegments[i-1];
                            if (previousCurveSegment.ParentCurve is IfcTrimmedCurve && j == 0)
                            {
                                var pointIFC = parentCurve.Points[j];
                                Vect pointGIM = new(0, 0, 0);
                                if (pointIFC.Coordinates.Count == 2)
                                    pointGIM = new(pointIFC.CoordinateX, pointIFC.CoordinateY, 0);
                                else
                                    pointGIM = new(pointIFC.CoordinateX, pointIFC.CoordinateY, pointIFC.CoordinateZ);
                                finalPoints.Add(pointGIM);
                            }
                            else if (j != 0)
                            {
                                var pointIFC = parentCurve.Points[j];
                                Vect pointGIM = new(0, 0, 0);
                                if (pointIFC.Coordinates.Count == 2)
                                    pointGIM = new(pointIFC.CoordinateX, pointIFC.CoordinateY, 0);
                                else
                                    pointGIM = new(pointIFC.CoordinateX, pointIFC.CoordinateY, pointIFC.CoordinateZ);
                                finalPoints.Add(pointGIM);
                            }
                        }
                    }
                }

            }
            return finalPoints;
        }

        public static Dictionary<IfcCircle, List<double>> GetCirclesFromCompositeCurveTrimmed(IfcCompositeCurve circleList)
        {
            Dictionary<IfcCircle, List<double>> finalresults = new();
            var curveSegments = circleList.Segments;
            for (int i = 0; i < curveSegments.Count; i++)
            {
                IfcCompositeCurveSegment curveSegment = (IfcCompositeCurveSegment)curveSegments[i];
                if (curveSegment.ParentCurve is IfcTrimmedCurve)
                {
                    IfcTrimmedCurve trimmedCurve = (IfcTrimmedCurve)curveSegment.ParentCurve;
                    IfcCircle circleIFC = (IfcCircle)trimmedCurve.BasisCurve;
                    finalresults[circleIFC] = new();
                    var startAngleDegree = trimmedCurve.Trim1.ParameterValue;
                    var startAngleRadiant = startAngleDegree * (Math.PI / 180);
                    var endAngleDegree = trimmedCurve.Trim2.ParameterValue;
                    var endAngleRadiant = endAngleDegree * (Math.PI / 180);
                    if(!trimmedCurve.SenseAgreement)
                    {
                        var temp = startAngleRadiant;
                        startAngleRadiant = endAngleRadiant;
                        endAngleRadiant = temp;
                    }
                    finalresults[circleIFC].Add(startAngleRadiant);
                    finalresults[circleIFC].Add(endAngleRadiant);

                }
            }

            return finalresults;
        }





        public static void CreateCylinderFromExtrusion(IfcCircle circle, double depth, Vect extrudedDirection, GimData gimData, int parentID, Vect location, Matrix finalTransform,
            Matrix localTransform, double startAngle = 0, double endAngle = Math.PI*2)
        {
            List<Vect> listOfPoints = new();
            var positionIFC = (IfcAxis2Placement2D)circle.Position;
            double radius = circle.Radius;
            var centerIFC = positionIFC.Location;
            Vect centerGIM = new(centerIFC.CoordinateX, centerIFC.CoordinateY, 0);
            var innerTransform = IFCReaderUtilities.Transform2DCoordsInto3D((IfcAxis2Placement2D)circle.Position);
            //centerGIM = centerGIM * finalTransform;
            //var resultingTransform = innerTransform * finalTransform;
            Vect originalCenterGIM = new(0, 0, 0);
            //centerGIM[0] += location[0];
            //centerGIM[1] += location[1];
            //centerGIM[2] += location[2];
            listOfPoints.Add(centerGIM);
            extrudedDirection = localTransform * extrudedDirection;
            var norm = extrudedDirection.Norm;
            Vect temp = new(extrudedDirection[0] / norm, extrudedDirection[1] / norm, extrudedDirection[2] / norm);
            Vect finalDirection = new(0, 0, 0);
            finalDirection[0] = temp[0] * depth;
            finalDirection[1] = temp[1] * depth;
            finalDirection[2] = temp[2] * depth;
            Vect extrudedcenterGIM = new(0, 0, 0);
            extrudedcenterGIM[0] = centerGIM[0] + finalDirection[0];
            extrudedcenterGIM[1] = centerGIM[1] + finalDirection[1];
            extrudedcenterGIM[2] = centerGIM[2] + finalDirection[2];
            listOfPoints.Add(extrudedcenterGIM);
            List<Vect> centers = new();
            IFCReadElements.CreateCylinderComposite(listOfPoints, gimData, parentID, location, innerTransform,finalTransform, radius, startAngle, endAngle);
            //IFCReadElements.CreateCirclePlane(centerGIM, gimData, parentID, location, finalTransform, radius, startAngle, endAngle);
            //IFCReadElements.CreateCirclePlane(extrudedcenterGIM, gimData, parentID, location, finalTransform, radius, startAngle, endAngle);


        }




        public static void CreateCylinderFromExtrusion(IfcCircleProfileDef circle, double depth ,Vect extrudedDirection, GimData gimData, int parentID, Vect location, Matrix localTransform)
        {
            List<Vect> listOfPoints = new();
            var positionIFC = circle.Position;
            double radius = circle.Radius;
            var centerIFC = positionIFC.Location;
            Vect centerGIM = new(centerIFC.CoordinateX, centerIFC.CoordinateY, 0);
            Vect originalCenterGIM = new(0, 0, 0);
            centerGIM[0] += location[0];
            centerGIM[1] += location[1];
            centerGIM[2] += location[2];
            listOfPoints.Add(centerGIM);
            var norm = extrudedDirection.Norm;
            Vect temp = new(extrudedDirection[0] / norm, extrudedDirection[1] / norm, extrudedDirection[2] / norm);
            Vect finalDirection = new(0, 0, 0);
            finalDirection[0] = temp[0] * depth;
            finalDirection[1] = temp[1] * depth;
            finalDirection[2] = temp[2] * depth;
            Vect extrudedcenterGIM = new(0, 0, 0);
            extrudedcenterGIM[0] = centerGIM[0] + finalDirection[0];
            extrudedcenterGIM[1] = centerGIM[1] + finalDirection[1];
            extrudedcenterGIM[2] = centerGIM[2] + finalDirection[2];
            listOfPoints.Add(extrudedcenterGIM);
            Vect line1StartPoint = new(0, 0, 0);
            Vect line1EndPoint = new(0, 0, 0);
            //DO VECTOR CALCULUS DIRECTLY INSTEAD EXAMPLE: OGPOINT + NEW VECT(X,Y,Z)
            line1StartPoint[0] = centerGIM[0] + radius;
            line1StartPoint[1] = centerGIM[1];
            line1StartPoint[2] = centerGIM[2];
            line1EndPoint[0] = extrudedcenterGIM[0] + radius;
            line1EndPoint[1] = extrudedcenterGIM[1];
            line1EndPoint[2] = extrudedcenterGIM[2];
            listOfPoints.Add(line1StartPoint);
            listOfPoints.Add(line1EndPoint);
            List<Vect> centers = new();
            IFCReadElements.CreateCylinder(listOfPoints, gimData, parentID, location, localTransform, radius);
            IFCReadElements.CreateCirclePlane(centerGIM, gimData, parentID, location, localTransform, radius);
            IFCReadElements.CreateCirclePlane(extrudedcenterGIM, gimData, parentID, location, localTransform, radius);

            
        }

    }
}
