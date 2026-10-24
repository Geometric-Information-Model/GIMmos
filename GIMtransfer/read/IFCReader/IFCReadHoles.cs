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
    public class IFCReadHoles
    {

        public static void CreateHole(List<Vect> listOfPoints, GimData gimData, int parentID, Vect generalLocation, Matrix localTransform)
        {
            if (listOfPoints.First() == listOfPoints.Last())
            {
                listOfPoints.Remove(listOfPoints.Last());
            }
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)parentID);
            Vect sumOfPoints = new(0, 0, 0);
            for (int i = 0; i < listOfPoints.Count; i++)
            {
                GimElement line = new() { elementType = GimElementType.C_line };
                int lineIndex = gimData.elements.Count;
                gimData.elements.Add(line);
                gimData.AddChildToParent((uint)lineIndex, (uint)wireIndex);
                Vect nextPointGIM = new(0, 0, 0);
                Vect currentPointGIM = listOfPoints[i];
                //currentPointGIM = currentPointGIM * localTransform;
                sumOfPoints += currentPointGIM;
                if (i == listOfPoints.Count - 1)
                    nextPointGIM = listOfPoints[0];
                else
                    nextPointGIM = listOfPoints[i + 1];
                //nextPointGIM = nextPointGIM * localTransform;
                var norm = (nextPointGIM - currentPointGIM).Norm;
                var direction = (nextPointGIM - currentPointGIM).Direction;
                double[] startEndNorms = new double[2];
                startEndNorms[0] = 0;
                startEndNorms[1] = norm;
                //var test = transform * direction;
                direction.ApplyToLocal(line.Locals[0]);
                line.Locals[0].px = currentPointGIM[0];// + generalLocation[0];
                line.Locals[0].py = currentPointGIM[1];// + generalLocation[1];
                line.Locals[0].pz = currentPointGIM[2];// + generalLocation[2];
                line.elementDoubles = startEndNorms;
                //gimData.elements.Add(line);

            }
        }
        public static List<List<Vect>> PrepUpHoles(IfcElement elementWithOpening)
        {
            List<List<Vect>> holes = new();
            var openings = elementWithOpening.HasOpenings.ToList();
            try
            {
                foreach (var opening in openings)
                {
                    var openingIFC = opening.RelatedOpeningElement;
                    var representation = openingIFC.Representation.Representations[0].Items;
                    IfcLocalPlacement objectOverallPlacement = new(new IfcAxis2Placement3D(new(new(), 0, 0)));
                    //if (openingIFC.ObjectPlacement.ReferencedByPlacements.Count > 0)
                    //    objectOverallPlacement = (IfcLocalPlacement)openingIFC.ObjectPlacement.ReferencedByPlacements.ElementAt(0);
                    //else
                    objectOverallPlacement = (IfcLocalPlacement)openingIFC.ObjectPlacement;
                    Vect generalLocationGIM = new Vect(0, 0, 0);
                    Matrix transformLocation = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                    IFCReaderUtilities.GetGlobalObjectPlacement(ref generalLocationGIM, objectOverallPlacement, ref transformLocation);

                    for (int i = 0; i < representation.Count; i++)
                    {
                        var extrudedArea = (IfcExtrudedAreaSolid)representation.ElementAt(i);
                        double depth = extrudedArea.Depth;
                        var extrudedDirection = extrudedArea.ExtrudedDirection;

                        Vect extrudedDirectionGIM = new(extrudedDirection.DirectionRatioX, extrudedDirection.DirectionRatioY, extrudedDirection.DirectionRatioZ);
                        var sweptArea = extrudedArea.SweptArea;
                        var positionIFC = extrudedArea.Position;
                        var localPosition = IFCReaderUtilities.GetLocalObjectPlacement(positionIFC);
                        localPosition = localPosition * transformLocation;
                        //if (localPosition[0] < 0)
                        //{
                        //    localPosition[0] = 0;
                        //}
                        //if (localPosition[1] < 0)
                        //{
                        //    localPosition[1] = 0;
                        //}
                        //if (localPosition[2] < 0)
                        //{
                        //    localPosition[2] = 0;
                        //}
                        var localTransform = IFCReaderUtilities.GetObjectTransformation(positionIFC);
                        var finalTransform = localTransform * transformLocation;
                        Vect resultingLocation = new(generalLocationGIM[0] + localPosition[0], generalLocationGIM[1] + localPosition[1], generalLocationGIM[2] + localPosition[2]);
                        ////if (i == 0)
                        ////{
                        //    generalLocationGIM[0] += localPosition[0];
                        //    generalLocationGIM[1] += localPosition[1];
                        //    generalLocationGIM[2] += localPosition[2];
                        ////}
                        if (sweptArea is IfcArbitraryClosedProfileDef)
                        {

                            var arbitraryClosedProfileDef = (IfcArbitraryClosedProfileDef)sweptArea;
                            var tempList = GetHolesFromPoints(arbitraryClosedProfileDef, depth, resultingLocation, extrudedDirectionGIM, finalTransform);
                            if (i == 1)
                            {
                                tempList[0].Reverse();
                                tempList[1].Reverse();
                            }
                            holes.AddRange(tempList);

                        }
                        else if (sweptArea is IfcRectangleProfileDef)
                        {
                            List<Vect> points = new();
                            var rectangleDef = (IfcRectangleProfileDef)sweptArea;
                            var innerTransform = IFCReaderUtilities.Transform2DCoordsInto3D(rectangleDef.Position);
                            Matrix identity = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                            //if (innerTransform != identity && localTransform != identity)
                            //{
                            //    innerTransform = innerTransform * localTransform;
                            //    points = IFCReaderUtilities.CreateListOfPointsFromRectangle(rectangleDef, innerTransform, resultingLocation);
                            //    for (int j = 0; j < points.Count; j++)
                            //    {
                            //        Vect currentPoint = points[j];
                            //        currentPoint = currentPoint * finalTransform;
                            //        points[j] = currentPoint;
                            //    }
                            //}
                            //else
                            var resultingTransform = innerTransform * finalTransform;
                            points = IFCReaderUtilities.CreateListOfPointsFromRectangle(rectangleDef, resultingTransform, resultingLocation);
                            
                            
                            var tempList = GetHolesFromPoints(points, depth, extrudedDirectionGIM, resultingTransform);
                            if (i == 1)
                            {
                                tempList[0].Reverse();
                                tempList[1].Reverse();
                            }
                            holes.AddRange(tempList);

                        }
                        else
                        {
                            string debug = "debug";
                        }

                    }

                }
                //var finalHoles = IFCReaderUtilities.RemoveDuplicates(holes);
                return holes;
            }
            catch
            {
                return null;
            }

            
        }

        public static List<List<Vect>> GetHolesFromPoints(IfcArbitraryClosedProfileDef closedProfileDef, double depth,Vect generalLocationGIM, Vect extrudedDirection, Matrix finalTransform)
        {

            List<List<Vect>> holes = new();
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
                for(int i =0; i < originalSetOfPoints.Count; i++)
                {
                    var currentPoint = originalSetOfPoints[i];
                    currentPoint[0] += generalLocationGIM[0];
                    currentPoint[1] += generalLocationGIM[1];
                    currentPoint[2] += generalLocationGIM[2];
                }
                holes.Add(originalSetOfPoints);
                extrudedDirection = extrudedDirection * finalTransform;
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
                for (int i = 0; i < extrudedSetOfPoints.Count; i++)
                {
                    var currentPoint = extrudedSetOfPoints[i];
                    currentPoint[0] += generalLocationGIM[0];
                    currentPoint[1] += generalLocationGIM[1];
                    currentPoint[2] += generalLocationGIM[2];
                }
                holes.Add(extrudedSetOfPoints);
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
                holes.Add(originalSetOfPoints);
                extrudedDirection = extrudedDirection * finalTransform;
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
                holes.Add(extrudedSetOfPoints);
            }
            else
            {
                string debug = "debug";
            }

                //We add the final two planes now
                
            return holes;
        }


        public static List<List<Vect>> GetHolesFromPoints(List<Vect> points, double depth, Vect extrudedDirection, Matrix finalTransform)
        {
            List<List<Vect>> holes = new List<List<Vect>>();
            //First make sure that all of the points are unique, if the last point equals the first point, then it's a closed loop and thus we can remove it safely

            List<Vect> originalSetOfPoints = new();
            List<Vect> extrudedSetOfPoints = new();

            //After having our list of unique points, we start prepping up the new points by converting them into a list of Vects
            //for(int i = 0; i < points.Count; i++)
            //{
            //    Vect currentPoint = points[i];
            //    currentPoint = currentPoint * finalTransform;
            //    points[i] = currentPoint;
            //}

            for (int i = 0; i < points.Count; i++)
            {
                var currentPoint = points[i];
                originalSetOfPoints.Add(currentPoint);

            }
            holes.Add(originalSetOfPoints);
            extrudedDirection = extrudedDirection * finalTransform;

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

            //We add the final two planes now
            holes.Add(extrudedSetOfPoints);
            return holes;
        }

        public static void ExtractPointsAndCreatePlane(List<Vect> points, List<List<Vect>> holes, double depth, Vect extrudedDirection, Vect location, Matrix localTransform, GimData gimData, int parentID, int baseAxis)
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
                //if (entry.Key == count1 || entry.Key == count2)
                
                    CreatePlaneWithHole(currentList, holes, gimData, parentID, location, localTransform, baseAxis);
                
                //IFCReadElements.CreatePlane(currentList, gimData, parentID, location, localTransform);

            }

        }

        public static void CreatePlaneWithHole(List<Vect> listOfPoints, List<List<Vect>> holes, GimData gimData, int parentID, Vect generalLocation, Matrix localTransform,
            int baseAxis)
        {
            bool superImposed = false;
            GimElement planeElement = new() { elementType = GimElementType.F_plane };
            int planeIndex = gimData.elements.Count;
            gimData.elements.Add(planeElement);
            gimData.AddChildToParent((uint)planeIndex, (uint)parentID);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)planeIndex);
            Vect sumOfPoints = new(0, 0, 0);
            for (int i = 0; i < listOfPoints.Count; i++)
            {
                GimElement line = new() { elementType = GimElementType.C_line };
                int lineIndex = gimData.elements.Count;
                gimData.elements.Add(line);
                gimData.AddChildToParent((uint)lineIndex, (uint)wireIndex);
                Vect nextPointGIM = new(0, 0, 0);
                Vect currentPointGIM = listOfPoints[i];
                currentPointGIM = currentPointGIM * localTransform;
                sumOfPoints += currentPointGIM;
                if (i == listOfPoints.Count - 1)
                    nextPointGIM = listOfPoints[0];
                else
                    nextPointGIM = listOfPoints[i + 1];
                nextPointGIM = nextPointGIM * localTransform;
                var norm = (nextPointGIM - currentPointGIM).Norm;
                var direction = (nextPointGIM - currentPointGIM).Direction;
                double[] startEndNorms = new double[2];
                startEndNorms[0] = 0;
                startEndNorms[1] = norm;
                //var test = transform * direction;
                direction.ApplyToLocal(line.Locals[0]);
                line.Locals[0].px = currentPointGIM[0] + generalLocation[0];
                line.Locals[0].py = currentPointGIM[1] + generalLocation[1];
                line.Locals[0].pz = currentPointGIM[2] + generalLocation[2];
                line.elementDoubles = startEndNorms;
                //gimData.elements.Add(line);



            }
            List<Vect> tempList = new();
            for(int i = 0; i < listOfPoints.Count; i++)
            {
                Vect currentPoint = new(0,0,0);
                var temp = listOfPoints[i];
                var temp2 = temp * localTransform;
                currentPoint[0] = temp2[0] +generalLocation[0];
                currentPoint[1] = temp2[1] +generalLocation[1];
                currentPoint[2] = temp2[2] +generalLocation[2];
                tempList.Add(currentPoint); 
            }
            var triangles = IFCReaderUtilities.CreateTwoTrianglesFromExtrudedFace(tempList);
            foreach (var pointsInside in holes)
            {
                
                var count = IFCReaderUtilities.CheckIfPointInsideTriangles(pointsInside, triangles);
                if (count == pointsInside.Count)
                {
                    superImposed = true;
                    CreateHole(pointsInside, gimData, planeIndex, generalLocation, localTransform);
                }
            }
            if(!superImposed)
                IFCReaderUtilities.CreateHoleFromIFCOpening(holes, tempList, baseAxis, gimData, planeIndex, generalLocation, localTransform);


            Vect centerPoint = sumOfPoints / listOfPoints.Count;
            Vect directionA = new(0, 0, 0);
            double maxLen = 0;

            for (int i = 0; i < listOfPoints.Count; i++)
            {
                int firstIndex = (i - 1 + listOfPoints.Count) % listOfPoints.Count;
                int lastIndex = i;

                Vect lastPointGIM = listOfPoints[lastIndex];
                Vect firstPointGIM = listOfPoints[firstIndex];
                Vect segment = lastPointGIM - firstPointGIM;
                // logging highest Z value


                if (maxLen < segment.Norm)
                {
                    directionA = segment;
                    maxLen = segment.Norm;
                }
            }

            directionA = directionA.Direction;

            //if (directionA.Norm == 0) continue; // degenerate polygon

            // we want to calculate the vector B: the vector most perpendicular to direction A
            Vect directionB = new Vect(0, 0, 0);

            // maximum perpendicularity coefficient: we want directionB to be as less parallel with A, to guarantee the most precise normal 
            double maxCoeff = 0;
            bool degenerate = true;
            for (int i = 0; i < listOfPoints.Count; i++)
            {
                int firstIndex = (i - 1 + listOfPoints.Count) % listOfPoints.Count;
                int lastIndex = i;

                Vect lastPointGIM = listOfPoints[lastIndex];
                Vect firstPointGIM = listOfPoints[firstIndex];
                Vect segment = lastPointGIM - firstPointGIM;
                double coeff = (segment ^ directionA).Norm;
                if (coeff > maxCoeff)
                {
                    maxCoeff = coeff;
                    directionB = segment;
                }
            }

            if (!directionB.IsCollinearTo(directionA))
            {
                degenerate = false;
            }

            if (!degenerate)
            {
                directionB = (directionB - directionA * directionB * directionA).Direction;

                var directionC = directionB ^ directionA;
                Matrix matrix = new(directionA, directionB, directionC);
                (matrix * localTransform).ApplyToLocal(planeElement.Locals[0]);
                planeElement.locals[0].px = centerPoint[0] + generalLocation[0];
                planeElement.locals[0].py = centerPoint[1] + generalLocation[1];
                planeElement.locals[0].pz = centerPoint[2] + generalLocation[2];
            }
            else
            {
                List<uint> planeIndexes = new();
                planeIndexes.Add((uint)planeIndex);
                gimData.RemoveElementsAndTheirChildren(planeIndexes);
            }

            //if (directionB.Norm == 0 || degenerate) continue;


        }

        public static bool ExtractPointsFromProfileDefAndCreatePlane(IfcArbitraryClosedProfileDef closedProfileDef, List<List<Vect>> holes ,double depth, Vect extrudedDirection, GimData gimData, int parentID, Vect location, Matrix localTransform, int baseAxis)
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
                    
                        CreatePlaneWithHole(currentList, holes, gimData, parentID, location, localTransform, baseAxis);
                    
                    //IFCReadElements.CreatePlane(currentList, gimData, parentID, location, localTransform);

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
                    if (entry.Key == count1 || entry.Key == count2)
                    {
                        CreatePlaneWithHole(currentList, holes, gimData, parentID, location, localTransform, baseAxis);
                    }
                    IFCReadElements.CreatePlane(currentList, gimData, parentID, location, localTransform);

                }
            }
            else
            {
                string debug = "debug";
                removed = true;
            }


            return removed;
        }


    }
}
