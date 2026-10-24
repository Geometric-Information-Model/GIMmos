using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace GIMcompress
{
    internal static class BRepSimplifier
    {
        public static void ChangeLine(GimData gimData)
        {

            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                GimElement temp;
                Vect startingPoint = new Vect();
                Vect endPoint = new Vect();
                Vect newstartingPoint = new Vect();
                Vect newPoint = new Vect();

                double num;


                if (gimElement.ElementType == GimElementType.C_line)
                {



                    temp = gimElement;

                    #region circlestuff
                    double radius = gimElement.ElementDoubles[0];
                    double t1 = gimElement.ElementDoubles[1];
                    double t2 = gimElement.ElementDoubles[2];
                    Vect origin = new(gimElement.locals[0].Px, gimElement.locals[0].Py, gimElement.locals[0].Pz);
                    Vect direction1 = new(gimElement.locals[0].Xx, gimElement.locals[0].Xy, gimElement.locals[0].Xz);
                    Vect direction2 = new(gimElement.locals[0].Yx, gimElement.locals[0].Yy, gimElement.locals[0].Yz);
                    startingPoint = origin + radius * Math.Cos(t1) * direction1 + radius * Math.Sin(t1) * direction2;
                    endPoint = origin + radius * Math.Cos(t2) * direction1 + radius * Math.Sin(t2) * direction2;
                    Vect newDirection1 = (endPoint - origin).Direction;

                    double newt1 = t2 - (Math.PI / 2);
                    newPoint = origin + radius * Math.Cos(newt1) * direction1 + radius * Math.Sin(newt1) * direction2;
                    Vect newDirection2 = (newPoint - origin).Direction;
                    Matrix test = new(newDirection1, newDirection2, newDirection1 ^ newDirection2);
                    test.ApplyToLocal(gimElement.locals[0]);




                    #endregion

                    #region linestuff
                    //Vect origin = new(gimElement.locals[0].Px, gimElement.locals[0].Py, gimElement.locals[0].Pz );
                    //Vect direction = new(gimElement.locals[0].Zx, gimElement.locals[0].Zy, gimElement.locals[0].Zz);
                    //startingPoint = origin + gimElement.elementDoubles[0] * direction;
                    //endPoint = origin + gimElement.elementDoubles[1] * direction;
                    //temp.locals[0].px = endPoint[0];
                    //temp.locals[0].py = endPoint[1];
                    //temp.locals[0].pz = endPoint[2];
                    //temp.locals[0].zx = -gimElement.locals[0].Zx;
                    //temp.locals[0].zy = -gimElement.locals[0].Zy;
                    //temp.locals[0].zz = -gimElement.locals[0].Zz;
                    //Vect newOrigin = new(temp.locals[0].Px, temp.locals[0].Py, temp.locals[0].Pz);
                    //Vect newDirection = new(temp.locals[0].Zx, temp.locals[0].Zy, temp.locals[0].Zz);
                    //newstartingPoint = newOrigin + temp.elementDoubles[0] * newDirection;
                    //newendPoint = newOrigin + temp.elementDoubles[1] * newDirection;
                    #endregion






                }
            }
        }

        public static Vect getLineStartPoint(GimElement gimElement)
        {

            Vect origin = new(gimElement.locals[0].Px, gimElement.locals[0].Py, gimElement.locals[0].Pz);
            Vect direction = new(gimElement.locals[0].Zx, gimElement.locals[0].Zy, gimElement.locals[0].Zz);
            return origin + gimElement.elementDoubles[0] * direction;
        }


        public static Vect getLineEndPoint(GimElement gimElement)
        {
            Vect origin = new(gimElement.locals[0].Px, gimElement.locals[0].Py, gimElement.locals[0].Pz);
            Vect direction = new(gimElement.locals[0].Zx, gimElement.locals[0].Zy, gimElement.locals[0].Zz);
            return origin + gimElement.elementDoubles[1] * direction;
        }

        public static Vect getCircleStartPoint(GimElement gimElement)
        {
            double radius = gimElement.ElementDoubles[0];
            double t1 = gimElement.ElementDoubles[1];
            double t2 = gimElement.ElementDoubles[2];
            Vect origin = new(gimElement.locals[0].Px, gimElement.locals[0].Py, gimElement.locals[0].Pz);
            Vect direction1 = new(gimElement.locals[0].Xx, gimElement.locals[0].Xy, gimElement.locals[0].Xz);
            Vect direction2 = new(gimElement.locals[0].Yx, gimElement.locals[0].Yy, gimElement.locals[0].Yz);
            return origin + radius * Math.Cos(t1) * direction1 + radius * Math.Sin(t1) * direction2;
        }

        public static Vect getCircleEndPoint(GimElement gimElement)
        {

            double radius = gimElement.ElementDoubles[0];
            double t1 = gimElement.ElementDoubles[1];
            double t2 = gimElement.ElementDoubles[2];
            Vect origin = new(gimElement.locals[0].Px, gimElement.locals[0].Py, gimElement.locals[0].Pz);
            Vect direction1 = new(gimElement.locals[0].Xx, gimElement.locals[0].Xy, gimElement.locals[0].Xz);
            Vect direction2 = new(gimElement.locals[0].Yx, gimElement.locals[0].Yy, gimElement.locals[0].Yz);
            return origin + radius * Math.Cos(t2) * direction1 + radius * Math.Sin(t2) * direction2;
        }



        //this function is used to get the parent of the curves (lines and circles in this case) that are under the threshold along with the shapes adjacents to them
        public static Dictionary<int, List<int>> LineDetection(GimData gimData, double threshold)
        {
            Dictionary<int, List<int>> adjacents = new();

            List<int> indexOfChilds = new();

            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];

                if (gimElement.elementType == GimElementType.C_line)
                {
                    double length = gimElement.elementDoubles[1];
                    if (length <= threshold)
                    {
                        //get the index of the wire of the element here
                        int wireIndex = (int)gimElement.locals[0].Parent;
                        GimElement wire = gimData.elements[wireIndex];
                        //get the index of the parent element here
                        int parentIndex = (int)wire.locals[0].Parent;

                        if (!adjacents.ContainsKey(parentIndex))
                        {
                            adjacents[parentIndex] = new List<int>();
                        }
                        else
                        {
                            adjacents[parentIndex].Clear();
                        }

                        GimElement parentElement = gimData.elements[parentIndex];



                        for (int l = 0; l < parentElement.Elements.Count; l++)
                        {
                            int childWireIndex = (int)parentElement.Elements[l];
                            GimElement wire1 = gimData.elements[childWireIndex];


                            //store the elements of the element inside a list for later comparaison
                            int lengthOfWireOfParent = wire1.elements.Count;
                            for (int j = 0; j < lengthOfWireOfParent; j++)
                            {
                                int wireChildindex = (int)wire1.elements[j];
                                indexOfChilds.Add((int)wire1.elements[j]);

                            }
                            // we have to now go through the elements of the list we created, while comparing them to each element of the object
                            //if they are superposed, we add them to the dictionnary

                            for (int j = 0; j < indexOfChilds.Count; j++)
                            {
                                GimElement currentElement = gimData.elements[indexOfChilds[j]];

                                for (int k = 0; k < gimData.elements.Count; k++)
                                {
                                    GimElement gimElement1 = gimData.elements[k];
                                    if (currentElement.ElementType == GimElementType.C_line && (currentElement.ElementType == gimElement1.ElementType))
                                    {

                                        //getting both starting points and ending points and comparing them to see if it's the same line
                                        double[] StartEndPoints1 = CurveHelpers.getStartAndEndPointsOfCurve(currentElement, gimData, 0);
                                        Vect StartPoint1 = new(StartEndPoints1[0], StartEndPoints1[1], StartEndPoints1[2]);
                                        Vect EndPoint1 = new(StartEndPoints1[3], StartEndPoints1[4], StartEndPoints1[5]);
                                        double[] StartEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                        Vect StartPoint2 = new(StartEndPoints2[0], StartEndPoints2[1], StartEndPoints2[2]);
                                        Vect EndPoint2 = new(StartEndPoints2[3], StartEndPoints2[4], StartEndPoints2[5]);


                                        //if indeed they share both start and end point, we are now putting the parent of its parent (wire's parent)
                                        //in the dictionnary

                                        if ((StartPoint1 == StartPoint2 && EndPoint1 == EndPoint2) || (StartPoint1 == EndPoint2 && StartPoint2 == EndPoint1))
                                        {
                                            int adjacentWireIndex = (int)gimElement1.locals[0].Parent;
                                            GimElement adjacentWire = gimData.elements[adjacentWireIndex];
                                            int adjacentParentIndex = (int)adjacentWire.locals[0].Parent;
                                            GimElement adjacentParent = gimData.elements[adjacentParentIndex];
                                            if (adjacentParentIndex != parentIndex)
                                            {
                                                adjacents[parentIndex].Add(adjacentParentIndex);
                                                break;
                                            }
                                        }

                                    }
                                    else if (currentElement.ElementType == GimElementType.C_circle && (currentElement.ElementType == gimElement1.ElementType))
                                    {
                                        double[] object1StartEndPoints = CurveHelpers.getStartAndEndPointsOfCurve(currentElement, gimData, 0);
                                        Vect object1StartPoint = new(object1StartEndPoints[0], object1StartEndPoints[1], object1StartEndPoints[2]);
                                        Vect object1EndPoint = new(object1StartEndPoints[3], object1StartEndPoints[4], object1StartEndPoints[5]);
                                        double[] object2StartEndPoints = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                        Vect object2StartPoint = new(object2StartEndPoints[0], object2StartEndPoints[1], object2StartEndPoints[2]);
                                        Vect object2EndPoint = new(object2StartEndPoints[3], object2StartEndPoints[4], object2StartEndPoints[5]);
                                        Vect[] object1IntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(currentElement, 0);
                                        Vect[] object2IntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(gimElement1, 0);

                                        if ((object1IntermediaryPoints[0] == object2IntermediaryPoints[0]) && ((object1StartPoint == object2StartPoint && object1EndPoint == object2EndPoint) || (object1StartPoint == object2EndPoint && object1EndPoint == object2StartPoint)))
                                        {
                                            if (object1IntermediaryPoints.Length == 1 && object2IntermediaryPoints.Length == 1)
                                            {
                                                int adjacentWireIndex = (int)gimElement1.locals[0].Parent;
                                                GimElement adjacentWire = gimData.elements[adjacentWireIndex];
                                                int adjacentParentIndex = (int)adjacentWire.locals[0].Parent;
                                                GimElement adjacentParent = gimData.elements[adjacentParentIndex];
                                                if (adjacentParentIndex != parentIndex)
                                                {
                                                    adjacents[parentIndex].Add(adjacentParentIndex);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                if (object1IntermediaryPoints[1] == object2IntermediaryPoints[1])
                                                {
                                                    int adjacentWireIndex = (int)gimElement1.locals[0].Parent;
                                                    GimElement adjacentWire = gimData.elements[adjacentWireIndex];
                                                    int adjacentParentIndex = (int)adjacentWire.locals[0].Parent;
                                                    GimElement adjacentParent = gimData.elements[adjacentParentIndex];
                                                    if (adjacentParentIndex != parentIndex)
                                                    {
                                                        adjacents[parentIndex].Add(adjacentParentIndex);
                                                        break;
                                                    }
                                                }
                                            }



                                        }


                                    }

                                }
                            }
                        }


                        indexOfChilds.Clear();
                    }
                }
                else if (gimElement.elementType == GimElementType.C_circle)
                {
                    double Arclength = (gimElement.ElementDoubles[2] - gimElement.ElementDoubles[1]) * gimElement.ElementDoubles[0];
                    if (Arclength <= threshold)
                    {
                        //get the index of the wire of the element here
                        int wireIndex = (int)gimElement.locals[0].Parent;
                        GimElement wire = gimData.elements[wireIndex];
                        //get the index of the parent element here
                        int parentIndex = (int)wire.locals[0].Parent;

                        if (!adjacents.ContainsKey(parentIndex))
                        {
                            adjacents[parentIndex] = new List<int>();
                        }
                        else
                        {
                            adjacents[parentIndex].Clear();
                        }
                        // store the parent to see if it's a plane or not
                        GimElement parentElement = gimData.elements[parentIndex];

                        for (int l = 0; l < parentElement.Elements.Count; l++)
                        {
                            int childWireIndex = (int)parentElement.Elements[l];
                            GimElement wire1 = gimData.elements[childWireIndex];


                            //store the elements of the plane inside a list for later comparaison
                            int lengthOfWireOfParent = wire1.elements.Count;
                            for (int j = 0; j < lengthOfWireOfParent; j++)
                            {
                                int wireChildindex = (int)wire1.elements[j];
                                indexOfChilds.Add((int)wire1.elements[j]);

                            }
                            // we have to now go through the elements of the list we created, while comparing them to each element of the object
                            //if they are superposed, we add them to the dictionnary

                            for (int j = 0; j < indexOfChilds.Count; j++)
                            {
                                GimElement currentElement = gimData.elements[indexOfChilds[j]];

                                for (int k = 0; k < gimData.elements.Count; k++)
                                {
                                    GimElement gimElement1 = gimData.elements[k];
                                    if (currentElement.ElementType == GimElementType.C_line && (currentElement.ElementType == gimElement1.ElementType))
                                    {

                                        //getting both starting points and ending points and comparing them to see if it's the same line
                                        double[] StartEndPoints1 = CurveHelpers.getStartAndEndPointsOfCurve(currentElement, gimData, 0);
                                        Vect StartPoint1 = new(StartEndPoints1[0], StartEndPoints1[1], StartEndPoints1[2]);
                                        Vect EndPoint1 = new(StartEndPoints1[3], StartEndPoints1[4], StartEndPoints1[5]);
                                        double[] StartEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                        Vect StartPoint2 = new(StartEndPoints2[0], StartEndPoints2[1], StartEndPoints2[2]);
                                        Vect EndPoint2 = new(StartEndPoints2[3], StartEndPoints2[4], StartEndPoints2[5]);


                                        //if indeed they share both start and end point, we are now putting the parent of its parent (wire's parent)
                                        //in the dictionnary

                                        if ((StartPoint1 == StartPoint2 && EndPoint1 == EndPoint2) || (StartPoint1 == EndPoint2 && StartPoint2 == EndPoint1))
                                        {
                                            int adjacentWireIndex = (int)gimElement1.locals[0].Parent;
                                            GimElement adjacentWire = gimData.elements[adjacentWireIndex];
                                            int adjacentParentIndex = (int)adjacentWire.locals[0].Parent;
                                            GimElement adjacentParent = gimData.elements[adjacentParentIndex];
                                            if (adjacentParentIndex != parentIndex)
                                            {
                                                adjacents[parentIndex].Add(adjacentParentIndex);
                                                break;
                                            }
                                        }

                                    }
                                    else if (currentElement.ElementType == GimElementType.C_circle && (currentElement.ElementType == gimElement1.ElementType))
                                    {
                                        double[] object1StartEndPoints = CurveHelpers.getStartAndEndPointsOfCurve(currentElement, gimData, 0);
                                        Vect object1StartPoint = new(object1StartEndPoints[0], object1StartEndPoints[1], object1StartEndPoints[2]);
                                        Vect object1EndPoint = new(object1StartEndPoints[3], object1StartEndPoints[4], object1StartEndPoints[5]);
                                        double[] object2StartEndPoints = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                        Vect object2StartPoint = new(object2StartEndPoints[0], object2StartEndPoints[1], object2StartEndPoints[2]);
                                        Vect object2EndPoint = new(object2StartEndPoints[3], object2StartEndPoints[4], object2StartEndPoints[5]);
                                        Vect[] object1IntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(currentElement, 0);
                                        Vect[] object2IntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(gimElement1, 0);

                                        if ((object1IntermediaryPoints[0] == object2IntermediaryPoints[0]) && ((object1StartPoint == object2StartPoint && object1EndPoint == object2EndPoint) || (object1StartPoint == object2EndPoint && object1EndPoint == object2StartPoint)))
                                        {
                                            if (object1IntermediaryPoints.Length == 1 && object2IntermediaryPoints.Length == 1)
                                            {
                                                int adjacentWireIndex = (int)gimElement1.locals[0].Parent;
                                                GimElement adjacentWire = gimData.elements[adjacentWireIndex];
                                                int adjacentParentIndex = (int)adjacentWire.locals[0].Parent;
                                                GimElement adjacentParent = gimData.elements[adjacentParentIndex];
                                                if (adjacentParentIndex != parentIndex)
                                                {
                                                    adjacents[parentIndex].Add(adjacentParentIndex);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                if (object1IntermediaryPoints[1] == object2IntermediaryPoints[1])
                                                {
                                                    int adjacentWireIndex = (int)gimElement1.locals[0].Parent;
                                                    GimElement adjacentWire = gimData.elements[adjacentWireIndex];
                                                    int adjacentParentIndex = (int)adjacentWire.locals[0].Parent;
                                                    GimElement adjacentParent = gimData.elements[adjacentParentIndex];
                                                    if (adjacentParentIndex != parentIndex)
                                                    {
                                                        adjacents[parentIndex].Add(adjacentParentIndex);
                                                        break;
                                                    }
                                                }
                                            }



                                        }


                                    }

                                }
                            }
                        }

                        indexOfChilds.Clear();
                    }
                }
            }

            //remove all the duplicates
            foreach (KeyValuePair<int, List<int>> entry in adjacents.ToList())
            {
                List<int> uniqueValues = entry.Value
                    .Distinct()
                    .ToList();

                adjacents[entry.Key] = uniqueValues;
            }
            return adjacents;



        }

        //this version detects only the adjacents of planes with lines 
        public static Dictionary<int, List<int>> AdjacentPlaneDetection(GimData gimData)
        {
            Dictionary<int, List<int>> adjacents = new();
            List<int> indexOfChilds = new();
            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];

                if (gimElement.elementType == GimElementType.C_line)
                {
                    double length = gimElement.elementDoubles[1];
                    //get the index of the wire of the element here
                    int wireIndex = (int)gimElement.locals[0].Parent;
                    GimElement wire = gimData.elements[wireIndex];
                    //get the index of the parent element here
                    int parentIndex = (int)wire.locals[0].Parent;
                    GimElement parentElement = gimData.elements[parentIndex];
                    if (parentElement.ElementType == GimElementType.F_plane)
                    {
                        if (!adjacents.ContainsKey(parentIndex))
                        {
                            adjacents[parentIndex] = new List<int>();
                        }
                        else
                        {
                            adjacents[parentIndex].Clear();
                        }
                        for (int l = 0; l < parentElement.Elements.Count; l++)
                        {
                            int childWireIndex = (int)parentElement.Elements[l];
                            GimElement wire1 = gimData.elements[childWireIndex];
                            //store the elements of the element inside a list for later comparaison
                            int lengthOfWireOfParent = wire1.elements.Count;
                            for (int j = 0; j < lengthOfWireOfParent; j++)
                            {
                                int wireChildindex = (int)wire1.elements[j];
                                indexOfChilds.Add((int)wire1.elements[j]);

                            }
                            // we have to now go through the elements of the list we created, while comparing them to each element of the object
                            //if they are superposed, we add them to the dictionnary
                            for (int j = 0; j < indexOfChilds.Count; j++)
                            {
                                GimElement currentElement = gimData.elements[indexOfChilds[j]];

                                for (int k = 0; k < gimData.elements.Count; k++)
                                {
                                    GimElement gimElement1 = gimData.elements[k];
                                    if (currentElement.ElementType == GimElementType.C_line && (currentElement.ElementType == gimElement1.ElementType))
                                    {

                                        //getting both starting points and ending points and comparing them to see if it's the same line
                                        double[] StartEndPoints1 = CurveHelpers.getStartAndEndPointsOfCurve(currentElement, gimData, 0);
                                        Vect StartPoint1 = new(Math.Round(StartEndPoints1[0], 4), Math.Round(StartEndPoints1[1], 4), Math.Round(StartEndPoints1[2], 4));
                                        Vect EndPoint1 = new(Math.Round(StartEndPoints1[3], 4), Math.Round(StartEndPoints1[4], 4), Math.Round(StartEndPoints1[5], 4));
                                        double[] StartEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                        Vect StartPoint2 = new(Math.Round(StartEndPoints2[0], 4), Math.Round(StartEndPoints2[1], 4), Math.Round(StartEndPoints2[2], 4));
                                        Vect EndPoint2 = new(Math.Round(StartEndPoints2[3], 4), Math.Round(StartEndPoints2[4], 4), Math.Round(StartEndPoints2[5], 4));


                                        //if indeed they share both start and end point, we are now putting the parent of its parent (wire's parent)
                                        //in the dictionnary

                                        if ((StartPoint1 == StartPoint2 && EndPoint1 == EndPoint2) || (StartPoint1 == EndPoint2 && StartPoint2 == EndPoint1))
                                        {
                                            int adjacentWireIndex = (int)gimElement1.locals[0].Parent;
                                            GimElement adjacentWire = gimData.elements[adjacentWireIndex];
                                            int adjacentParentIndex = (int)adjacentWire.locals[0].Parent;
                                            GimElement adjacentParent = gimData.elements[adjacentParentIndex];
                                            if (adjacentParentIndex != parentIndex)
                                            {
                                                adjacents[parentIndex].Add(adjacentParentIndex);
                                                break;
                                            }
                                        }

                                    }

                                }
                            }
                        }
                        indexOfChilds.Clear();
                    }

                }

            }
            //remove all the duplicates
            foreach (KeyValuePair<int, List<int>> entry in adjacents.ToList())
            {
                List<int> uniqueValues = entry.Value
                    .Distinct()
                    .ToList();

                adjacents[entry.Key] = uniqueValues;
            }
            return adjacents;

        }


        //function used to see if a point belongs to another plane
        public static bool CheckPoint(GimElement gimElement1, GimElement gimElement2)
        {
            bool belongs = false;

            Vect originPoint1 = new(Math.Round(gimElement1.Locals[0].Px, 4), Math.Round(gimElement1.Locals[0].Py, 4), Math.Round(gimElement1.Locals[0].Pz, 4));
            Vect originPoint2 = new(Math.Round(gimElement2.Locals[0].Px, 4), Math.Round(gimElement2.Locals[0].Py, 4), Math.Round(gimElement2.Locals[0].Pz, 4));
            Vect Dx1 = new(Math.Round(gimElement1.Locals[0].Xx, 4), Math.Round(gimElement1.Locals[0].Xy, 4), Math.Round(gimElement1.Locals[0].Xz, 4));
            Vect Dy1 = new(Math.Round(gimElement1.Locals[0].Yx, 4), Math.Round(gimElement1.Locals[0].Yy, 4), Math.Round(gimElement1.Locals[0].Yz, 4));
            Vect normal1 = Dx1 ^ Dy1;

            Vect newVector = originPoint2 - originPoint1;

            double result = newVector * normal1;

            if (Math.Round(result, 4) == 0)
            {
                belongs = true;
            }


            return belongs;
        }


        //function used to check if the normal of 2 gim elements are collinear
        public static bool CheckCollinearity(GimElement gimElement1, GimElement gimElement2)
        {
            bool collinear = false;

            Vect Dx1 = new(Math.Round(gimElement1.Locals[0].Xx, 4), Math.Round(gimElement1.Locals[0].Xy, 4), Math.Round(gimElement1.Locals[0].Xz, 4));
            Vect Dy1 = new(Math.Round(gimElement1.Locals[0].Yx, 4), Math.Round(gimElement1.Locals[0].Yy, 4), Math.Round(gimElement1.Locals[0].Yz, 4));
            Vect normal1 = Dx1 ^ Dy1;
            Vect Dx2 = new(Math.Round(gimElement2.Locals[0].Xx, 4), Math.Round(gimElement2.Locals[0].Xy, 4), Math.Round(gimElement2.Locals[0].Xz, 4));
            Vect Dy2 = new(Math.Round(gimElement2.Locals[0].Yx, 4), Math.Round(gimElement2.Locals[0].Yy, 4), Math.Round(gimElement2.Locals[0].Yz, 4));
            Vect normal2 = Dx2 ^ Dy2;
            Vect col = normal1 ^ normal2;

            Vect vectNull = new(0, 0, 0);




            if (col == vectNull)
            {
                collinear = true;
            }

            return collinear;
        }



        //this function helps us find planes that can be joined together
        public static Dictionary<int, List<int>> FindJoinablePlanes(GimData gimData)
        {
            Dictionary<int, List<int>> JoinablePlanes = new Dictionary<int, List<int>>();
            //we use the function that detects planes and their adjacents here

            Dictionary<int, List<int>> AdjacentPlanes = AdjacentPlaneDetection(gimData);
            //then after that, we are going to check in each plane which ones can be merged
            foreach (var plane in AdjacentPlanes)
            {
                GimElement plane1 = gimData.elements[(int)plane.Key];
                List<int> mergables = new List<int>();
                for (int i = 0; i < plane.Value.Count; i++)
                {

                    int ind = plane.Value[i];
                    GimElement plane2 = gimData.elements[(int)ind];
                    //the planes can be merged if those 2 conditions are satisfied: their normals are collinear, and the origin of one plane belongs to the other
                    //if those 2 conditions are satisfied, we can proceed
                    if (CheckCollinearity(plane1, plane2) && CheckPoint(plane1, plane2))
                    {
                        mergables.Add(ind);
                        List<int> redundant = new List<int>();
                        redundant = AdjacentPlanes[ind];
                        //the idea here is to merge all the planes directly, meaning instead of merging plane B with C, then merging A with the new BC plane, we merge A with B and C directly
                        for (int j = 0; j < redundant.Count; j++)
                        {
                            if (redundant[j] != plane.Key)
                            {
                                plane.Value.Add(redundant[j]);
                            }
                        }
                        AdjacentPlanes.Remove(ind);
                    }

                }
                if (mergables.Count > 0)
                {
                    JoinablePlanes[plane.Key] = new List<int>();
                    for (int i = 0; i < mergables.Count; i++)
                    {
                        JoinablePlanes[plane.Key].Add(mergables[i]);
                    }

                    mergables.Clear();

                }

            }



            return JoinablePlanes;
        }


        //the joining of the plane is done here
        public static void JoinPlanes(GimData gimData)
        {
            //first get all the joinable planes using the FindJoinablePlanes function
            Dictionary<int, List<int>> JoinablePlanes = FindJoinablePlanes(gimData);
            List<uint> toBeRemoved = new List<uint>();
            //then treating the planes one by one using the TestMergePlanes function
            foreach (var plane in JoinablePlanes)
            {

                for (int i = 0; i < plane.Value.Count; i++)
                {
                    double ind = plane.Value[i];
                    toBeRemoved.AddRange(TestMergePlanes(gimData, (uint)plane.Key, (uint)ind));
                }
            }
            gimData.RemoveElementsAndTheirChildren(toBeRemoved);

        }





        //function used to get the index of all the elements in a wire
        public static List<uint> GetElementIndexes(GimData gimData, uint elementIndex)
        {
            List<uint> indexes = new();
            GimElement gimElement = gimData.elements[(int)elementIndex];
            uint elementWireIndex = gimElement.Elements[0];
            GimElement wire = gimData.elements[(int)elementWireIndex];
            for (int i = 0; i < wire.Elements.Count; i++)
            {
                indexes.Add(wire.Elements[i]);
            }

            return indexes;



        }

        //this function's purpose is to find and delete the common line between two planes
        public static List<uint> DeleteCommonLines(GimData gimData, uint plane1Index, uint plane2Index)
        {
            List<uint> plane1LinesIndexes = new();
            List<uint> plane2LinesIndexes = new();
            List<uint> linesToBeRemovedIndexes = new();
            GimElement plane1 = gimData.elements[(int)plane1Index];
            GimElement plane2 = gimData.elements[(int)plane2Index];
            uint plane1WireIndex = plane1.Elements[0];
            uint plane2WireIndex = plane2.Elements[0];
            GimElement plane1Wire = gimData.elements[(int)plane1WireIndex];
            GimElement plane2Wire = gimData.Elements[(int)plane2WireIndex];
            plane1LinesIndexes = GetElementIndexes(gimData, plane1Index);
            plane2LinesIndexes = GetElementIndexes(gimData, plane2Index);

            for (int i = 0; i < plane1LinesIndexes.Count; ++i)
            {
                //getting the coords of the first line
                uint line1Index = plane1LinesIndexes[i];
                GimElement line1 = gimData.Elements[(int)line1Index];
                double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData, 0);
                Vect line1StartPoint = new(Math.Round(line1Coords[0], 4), Math.Round(line1Coords[1], 4), Math.Round(line1Coords[2], 4));
                Vect line1EndPoint = new(Math.Round(line1Coords[3], 4), Math.Round(line1Coords[4], 4), Math.Round(line1Coords[5], 4));
                for (int j = 0; j < plane2LinesIndexes.Count; j++)
                {
                    //getting coords of second line
                    uint line2Index = plane2LinesIndexes[j];
                    GimElement line2 = gimData.Elements[(int)line2Index];
                    double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData, 0);
                    Vect line2StartPoint = new(Math.Round(line2Coords[0], 4), Math.Round(line2Coords[1], 4), Math.Round(line2Coords[2], 4));
                    Vect line2EndPoint = new(Math.Round(line2Coords[3], 4), Math.Round(line2Coords[4], 4), Math.Round(line2Coords[5], 4));
                    if ((line1StartPoint == line2StartPoint && line1EndPoint == line2EndPoint) || (line1StartPoint == line2EndPoint && line2StartPoint == line1EndPoint))
                    {
                        //the shared line must be removed from the first plane, line musn't have the plane (its wire in this case) as a Parent anymore
                        //and we have to remove the line from the list of elements of the parent as well, two way
                        plane1Wire.elements.Remove(line1Index);
                        plane2Wire.elements.Remove(line2Index);
                        plane2LinesIndexes.Remove(line2Index);
                        linesToBeRemovedIndexes.Add(line1Index);
                        linesToBeRemovedIndexes.Add(line2Index);

                        break;

                    }

                }

            }

            return linesToBeRemovedIndexes;



        }

        //as the name suggests, getting lines
        public static List<uint> GetLines(GimData gimData)
        {
            List<uint> lines = new();
            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                if (gimElement.ElementType == GimElementType.C_line)
                {
                    lines.Add((uint)i);
                }
            }




            return lines;
        }

        //as the name suggests as well, getting circles
        public static List<uint> GetCircles(GimData gimData)
        {
            List<uint> circles = new();
            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                if (gimElement.ElementType == GimElementType.C_circle)
                {
                    circles.Add((uint)i);
                }
            }




            return circles;
        }

        //public static void TestCircles(GimData gimData)
        //{

        //    for (int i = 0; i < gimData.elements.Count; i++)
        //    {
        //        GimElement gimElement = gimData.elements[i];
        //        if (gimElement.ElementType == GimElementType.C_circle)
        //        {
        //            Vect pos = new(gimElement.Locals[0].Px, gimElement.Locals[0].Py, gimElement.Locals[0].Pz);
        //            Vect pos1 = new(0, 0, 0);
        //            Vect pos2 = new(0, -500, 0);
        //            if (pos == pos1 || pos == pos2)
        //            {
        //                double[] temp = CurveHelpers.getStartAndEndPointsOfCurve(gimElement, gimData);
        //                Vect startpoint = temp[0..3];
        //                Vect endpoint = temp[3..6];
        //                int a = 0;
        //            }

        //        }
        //    }
        //}

        public static List<uint> TestMergePlanes(GimData gimData, uint plane1Index, uint plane2Index)
        {
            //declaring variables that we most likely will need
            GimElement plane1 = gimData.elements[(int)plane1Index];
            GimElement plane2 = gimData.elements[(int)plane2Index];
            uint plane1WireIndex = plane1.Elements[0];
            uint plane2WireIndex = plane2.Elements[0];
            GimElement plane1Wire = gimData.elements[(int)plane1WireIndex];
            GimElement plane2Wire = gimData.elements[(int)plane2WireIndex];
            List<uint> result = new();
            List<uint> linesToBeRemoved = new();


            //first delete the common lines
            linesToBeRemoved = DeleteCommonLines(gimData, plane1Index, plane2Index);
            result.AddRange(linesToBeRemoved);
            //now get the lines of the fresh 2 planes
            List<uint> plane1LinesIndexes = GetElementIndexes(gimData, plane1Index);
            List<uint> plane2LinesIndexes = GetElementIndexes(gimData, plane2Index);
            List<uint> newPlaneLines = new();

            plane1LinesIndexes.AddRange(plane2LinesIndexes);
            //change the parent of the second plane lines
            for (int i = 0; i < plane2LinesIndexes.Count; i++)
            {
                GimElement line = gimData.elements[(int)plane2LinesIndexes[i]];
                line.Locals[0].Parent = plane1WireIndex;
            }
            newPlaneLines.Add(plane1LinesIndexes[0]);
            //Idea here is to get all the lines of the new plane in an ordered way thanks to the points
            for (int i = 0; i < newPlaneLines.Count; i++)
            {
                //getting the coords of the first line
                uint line1Index = newPlaneLines[i];
                GimElement line1 = gimData.Elements[(int)line1Index];
                double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData, 0);
                Vect line1StartPoint = new(Math.Round(line1Coords[0], 3), Math.Round(line1Coords[1], 3), Math.Round(line1Coords[2], 3));
                Vect line1EndPoint = new(Math.Round(line1Coords[3], 3), Math.Round(line1Coords[4], 3), Math.Round(line1Coords[5], 3));
                for (int j = 1; j < plane1LinesIndexes.Count; j++)
                {
                    //getting coords of second line
                    uint line2Index = plane1LinesIndexes[j];
                    GimElement line2 = gimData.Elements[(int)line2Index];
                    double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData, 0);
                    Vect line2StartPoint = new(Math.Round(line2Coords[0], 3), Math.Round(line2Coords[1], 3), Math.Round(line2Coords[2], 3));
                    Vect line2EndPoint = new(Math.Round(line2Coords[3], 3), Math.Round(line2Coords[4], 3), Math.Round(line2Coords[5], 3));
                    if ((line1StartPoint == line2StartPoint || line1StartPoint == line2EndPoint || line2StartPoint == line1EndPoint || line1EndPoint == line2EndPoint) && line1Index != line2Index)
                    {
                        //for the first line we simply add it
                        if (i == 0)
                        {
                            newPlaneLines.Add(line2Index);
                            break;
                        }
                        //from the second line and on, we need to have a small check
                        else if (i > 0 && newPlaneLines[i - 1] != line2Index)
                        {
                            newPlaneLines.Add(line2Index);
                            break;
                        }
                    }


                }
            }
            //add the new lines to the plane
            plane1Wire.elements.Clear();
            plane1Wire.elements.AddRange(newPlaneLines);
            //clear the wire and add the plane to a list of planes to be deleted
            plane2Wire.Elements.Clear();
            result.Add(plane2Index);
            return result;


        }


        //here we are going to merge two lines
        public static List<uint> MergeLines(GimData gimData, uint line1Index, uint line2Index)
        {
            //declaring variable that we will most likely need
            List<uint> linesToBeRemoved = new();
            double t1, t2;
            GimElement line1 = gimData.elements[(int)line1Index];
            GimElement line2 = gimData.elements[(int)line2Index];
            uint line1WireIndex = line1.Locals[0].Parent;
            uint line2WireIndex = line2.Locals[0].Parent;
            GimElement line1Wire = gimData.elements[(int)line1WireIndex];
            double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData, 0);
            Vect line1StartPoint = new(Math.Round(line1Coords[0], 4), Math.Round(line1Coords[1], 4), Math.Round(line1Coords[2], 4));
            Vect line1EndPoint = new(Math.Round(line1Coords[3], 4), Math.Round(line1Coords[4], 4), Math.Round(line1Coords[5], 4));
            Vect Dir1 = new(line1.Locals[0].Zx, line1.Locals[0].Zy, line1.Locals[0].Zz);
            double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData, 0);
            Vect line2StartPoint = new(Math.Round(line2Coords[0], 4), Math.Round(line2Coords[1], 4), Math.Round(line2Coords[2], 4));
            Vect line2EndPoint = new(Math.Round(line2Coords[3], 4), Math.Round(line2Coords[4], 4), Math.Round(line2Coords[5], 4));
            Vect Dir2 = new(line2.Locals[0].Zx, line2.Locals[0].Zy, line2.Locals[0].Zz);
            //we check if the lines share the same direction and if they share a point
            if ((line1StartPoint == line2StartPoint || line1EndPoint == line2EndPoint || line1StartPoint == line2EndPoint || line2StartPoint == line1EndPoint) && (Dir1 == Dir2 || Dir1 == -Dir2) && (line1WireIndex == line2WireIndex))
            {
                //if they do, we'll create new points with the extremities, in order to find them, we'll search which points are not the common points between the lines
                //those that aren't the common points are the extremities of our new lines
                Vect point1 = new();
                if (line1StartPoint != line2StartPoint && line1StartPoint != line2EndPoint)
                {
                    point1 = line1StartPoint;
                }
                else
                {
                    point1 = line1EndPoint;
                }
                Vect point2 = new();
                if (line2StartPoint != line1StartPoint && line2StartPoint != line1EndPoint)
                {
                    point2 = line2StartPoint;
                }
                else
                {
                    point2 = line2EndPoint;
                }
                //after finding the extremities, we can do the proper treatment
                t2 = (point1 - point2).Norm;
                Vect direction = (point2 - point1).Direction;
                //t1 = (point1[0] - Basis1[0]) / Dir1[0]; 
                direction.ApplyToLocal(line1.locals[0]);
                line1.locals[0].px = point1[0];
                line1.locals[0].py = point1[1];
                line1.locals[0].pz = point1[2];
                line1.elementDoubles[0] = 0;
                line1.elementDoubles[1] = t2;
                line1Wire.elements.Remove(line2Index);
                linesToBeRemoved.Add(line2Index);

            }
            return linesToBeRemoved;

        }


        //getting the inner circles
        public static List<uint> GetInnerCircles(GimData gimData)
        {
            List<uint> innerCircles = new();
            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                //shapes with more than one wire contain an inner wire, and that's what we want to check in order to know if we should add that shape (circle in this case) to our list
                if ((gimElement.elementType == GimElementType.F_cilinder || gimElement.elementType == GimElementType.F_plane) && gimElement.elements.Count > 1)
                {
                    for (int j = 1; j < gimElement.elements.Count; j++)
                    {
                        uint wireIndex = gimElement.elements[j];
                        GimElement wire = gimData.elements[(int)wireIndex];
                        for (int k = 0; k < wire.elements.Count; k++)
                        {
                            uint currentCircleIndex = wire.elements[k];
                            GimElement currentCircle = gimData.elements[((int)currentCircleIndex)];
                            if (currentCircle.elementType == GimElementType.C_circle)
                            {
                                innerCircles.Add(currentCircleIndex);
                            }
                        }
                    }
                }
            }
            return innerCircles;

        }
        

        

        public static List<uint> GetInnerLines(GimData gimData)
        {
            List<uint> innerLines = new();
            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                //shapes with more than one wire contain an inner wire, and that's what we want to check in order to know if we should add that shape (line in this case) to our list
                if ((gimElement.elementType == GimElementType.F_cilinder || gimElement.elementType == GimElementType.F_plane) && gimElement.elements.Count > 1)
                {
                    for (int j = 1; j < gimElement.elements.Count; j++)
                    {
                        uint wireIndex = gimElement.elements[j];
                        GimElement wire = gimData.elements[(int)wireIndex];
                        for (int k = 0; k < wire.elements.Count; k++)
                        {
                            uint currentLineIndex = wire.elements[k];
                            GimElement currentLine = gimData.elements[((int)currentLineIndex)];
                            if (currentLine.elementType == GimElementType.C_line)
                            {
                                innerLines.Add(currentLineIndex);
                            }
                        }
                    }
                }
            }
            return innerLines;
        }

        //merging all the circles including the inner ones that are superimposed with the current circle we are working on
        public static void MergeCircles(GimData gimData)
        {
            
            List<uint> circlestobeRemoved = new();

            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                //for starters this function will only check for cylinders, it'll be updated soon to work with other shapes
                if (gimElement.elementType == GimElementType.F_cilinder)
                {
                    uint wireIndex = gimElement.Elements[0];
                    GimElement wire = gimData.elements[(int)wireIndex];
                    List<Vect> otherShapesStartEndPoints = new();
                    for (int j = 0; j < wire.Elements.Count; j++)
                    //the idea here since dealing with cylinders is quite delicate, our new merged circle has to start from the point of any other shape within that cylinder (that isn't a circle)
                    //so in this next step, we will be putting all the start and end point of shapes other than circles in a list
                    {
                        GimElement currentCilinderShape = gimData.elements[(int)wire.elements[j]];
                        if (currentCilinderShape.elementType != GimElementType.C_circle)
                        {
                            double[] currentShapePoints = CurveHelpers.getStartAndEndPointsOfCurve(currentCilinderShape, gimData);
                            otherShapesStartEndPoints.Add(currentShapePoints[0..3]);
                            otherShapesStartEndPoints.Add(currentShapePoints[3..6]);
                        }

                    }
                    //after getting the points, we can now start dealing with the circles
                    for (int j = 0; j < wire.Elements.Count; j++)
                    {
                        uint currentCircleIndex = wire.Elements[j];
                        GimElement currentCircle = gimData.elements[(int)currentCircleIndex];
                        if (currentCircle.elementType == GimElementType.C_circle)
                        {
                            double[] currentCirclePoints = CurveHelpers.getStartAndEndPointsOfCurve(currentCircle, gimData);
                            Vect currentCircleStartPoint = currentCirclePoints[0..3];
                            //if the current circle we are sharing shares a point with one of the other non-circle shapes, we can proceed
                            if (otherShapesStartEndPoints.Contains(currentCircleStartPoint) && !circlestobeRemoved.Contains(currentCircleIndex))
                            {
                                //idea here is putting the circles ordered in a new list and then merge them
                                List<uint> newCircles = new();
                                newCircles.Add(currentCircleIndex);
                                for (int k = 0; k < newCircles.Count; k++)
                                {
                                    uint circle1Index = newCircles[k];
                                    GimElement circle1 = gimData.elements[(int)circle1Index];
                                    double[] circle1Coords = CurveHelpers.getStartAndEndPointsOfCurve(circle1, gimData);
                                    Vect circle1EndPoint = circle1Coords[3..6];
                                    for (int m = 0; m < wire.Elements.Count; m++)
                                    {
                                        uint circle2Index = wire.elements[m];
                                        GimElement circle2 = gimData.elements[(int)circle2Index];
                                        if (circle2.elementType == GimElementType.C_circle && circle1Index != circle2Index && !newCircles.Contains(circle2Index))
                                        {

                                            double[] circle2Coords = CurveHelpers.getStartAndEndPointsOfCurve(circle2, gimData);
                                            Vect circle2StartPoint = circle2Coords[0..3];
                                            Vect circle2EndPoint = circle2Coords[3..6];
                                            if (circle1EndPoint == circle2StartPoint || circle1EndPoint == circle2EndPoint)
                                            {
                                                newCircles.Add(circle2Index);
                                                circlestobeRemoved.Add(circle2Index);
                                                wire.elements.Remove(circle2Index);
                                                break;
                                            }
                                        }
                                    }
                                }
                                //this is the function that takes care of merging the circle that coincides with this shape
                                //we pass the list we made of ordered circles as a parameters
                                circlestobeRemoved.AddRange(MergeSuperImposedCircles(newCircles, gimData));
                                //since the circles are ordered here, we just have to increase the value of its angle
                                uint intialCircleIndex = newCircles[0];
                                GimElement initialCircle = gimData.elements[(int)intialCircleIndex];
                                for (int k = 1; k < newCircles.Count; k++)
                                {
                                    uint otherCircleIndex = newCircles[k];
                                    GimElement otherCircle = gimData.elements[(int)otherCircleIndex];
                                    initialCircle.elementDoubles[2] += otherCircle.elementDoubles[2];


                                }

                            }
                        }
                    }

                }
            }
            gimData.RemoveElementsAndTheirChildren(circlestobeRemoved);
        }



        //a simple function that checks if two circles are equal thanks to the start, intermediary and end points
        public static bool CheckIfCirclesEqual(uint circle1Index, uint circle2Index, GimData gimData)
        {
            bool equal = false;
            GimElement circle1 = gimData.Elements[(int)circle1Index];
            GimElement circle2 = gimData.Elements[(int)circle2Index];
            double[] circle1Points = CurveHelpers.getStartAndEndPointsOfCurve(circle1, gimData);
            double[] circle2Points = CurveHelpers.getStartAndEndPointsOfCurve(circle2, gimData);
            Vect[] circle1IntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(circle1);
            Vect[] circle2IntermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(circle2);
            Vect circle1StartPoint = circle1Points[0..3];
            Vect circle1EndPoint = circle1Points[3..6];
            Vect circle2StartPoint = circle2Points[0..3];
            Vect circle2EndPoint = circle2Points[3..6];
            if ((circle1IntermediaryPoints[0] == circle2IntermediaryPoints[0]) && ((circle1StartPoint == circle2StartPoint && circle1EndPoint == circle2EndPoint) || (circle1StartPoint == circle2EndPoint && circle1EndPoint == circle2StartPoint)))
            {
                if (circle1IntermediaryPoints.Length == 1 && circle2IntermediaryPoints.Length == 1)
                {
                    equal = true;
                }
                else
                {
                    if (circle1IntermediaryPoints[1] == circle2IntermediaryPoints[1])
                    {
                        equal = true;
                    }
                }
            }
            return equal;
        }

        //this should merge the inner circles that coincide with the other circles, it takes an ordered list of circles as a parameter, with the first index
        //being the starting point of the eventual new circle
        public static List <uint> MergeSuperImposedCircles(List <uint> circles, GimData gimData)
        {
            List<uint> innerCircles = GetInnerCircles(gimData);
            List<uint> innerCirclesToBeRemoved = new();
            uint newIndex = 0;
            bool found = false;
            uint firstCircleIndex = circles[0];
            GimElement firstCircle = gimData.elements[(int)firstCircleIndex];


            //we check first if the current inner circle is indeed superimposed with the circle on the other shape
            //the inner circle we're looking for has to be superimposed with the first circle we are getting from the list as a parameter since they are ordered there
            for (int i = 0; i < innerCircles.Count; i++)
            {
                uint innerCircleIndex = innerCircles[i];
                GimElement innerCircle = gimData.elements[(int)innerCircleIndex];
                found = CheckIfCirclesEqual(innerCircleIndex, firstCircleIndex, gimData);
                
                if(found == true)
                {
                    newIndex = innerCircleIndex;
                    break;
                }

            }
           
            //if we find that circle, we do the usual treatment, which is adding the next circles in line inside the list
            if (found == true)
            {
                
                List<uint> newInnerCircles = new();
                newInnerCircles.Add(newIndex);
                for (int i = 0; i < newInnerCircles.Count; i++)
                {
                    uint innerCircle1Index = newInnerCircles[i];
                    GimElement innerCircle1 = gimData.elements[(int)innerCircle1Index];
                    double[] innerCircle1Coords = CurveHelpers.getStartAndEndPointsOfCurve(innerCircle1, gimData);
                    Vect innerCircle1EndPoint = innerCircle1Coords[3..6];
                    for (int j = 0; j < innerCircles.Count; j++)
                    {
                        uint innerCircle2Index = innerCircles[j];
                        GimElement innerCircle2 = gimData.elements[(int)innerCircle2Index];
                        uint innerCircle2Wire = innerCircle2.Locals[0].Parent;
                        GimElement innerCircleWire = gimData.elements[(int)innerCircle2Wire];
                        if(innerCircle2Index != innerCircle1Index && !newInnerCircles.Contains(innerCircle2Index))
                        {
                            double[] innerCircle2Points = CurveHelpers.getStartAndEndPointsOfCurve(innerCircle2, gimData);
                            Vect innerCircle2StartPoint = innerCircle2Points[0..3];
                            Vect innerCircle2EndPoint = innerCircle2Points[3..6];
                            if (innerCircle1EndPoint == innerCircle2EndPoint|| innerCircle1EndPoint == innerCircle2StartPoint)
                            {
                                newInnerCircles.Add(innerCircle2Index);
                                innerCirclesToBeRemoved.Add(innerCircle2Index);
                                innerCircleWire.elements.Remove(innerCircle2Index);
                                break;

                            }
                        }
                    }


                }
                //after getting our ordered inner circles, we simply add the angles
                uint intialInnerCircleIndex = newInnerCircles[0];
                GimElement initialInnerCircle = gimData.elements[(int)intialInnerCircleIndex];
                for (int i = 1; i < newInnerCircles.Count; i++)
                {
                    uint otherInnerCircleIndex = newInnerCircles[i];
                    GimElement otherInnerCircle = gimData.elements[(int)otherInnerCircleIndex];
                    initialInnerCircle.elementDoubles[2] += otherInnerCircle.elementDoubles[2];


                }

            }
            //finally we return the indexes that are going to be removed later
            return innerCirclesToBeRemoved;
        }


       
       

        //merging all the lines two by two
        public static void MakeLinesOne(GimData gimData)
        {
            List<uint> curvestoBeRemoved = new();
            List<uint> lines = GetLines(gimData);

            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {

                    curvestoBeRemoved.AddRange(MergeLines(gimData, lines[i], lines[j]));

                }
            }


            gimData.RemoveElementsAndTheirChildren(curvestoBeRemoved);

        }

        //function that calls both the line merging and the circles one
        public static void MergeCurves(GimData gimData)
        {
            MakeLinesOne(gimData);
            MergeCircles(gimData);
        }





        //getting the intersection point of two lines
        public static Vect GetIntersectionPoint(GimElement line1, GimElement line2)
        {

            Vect origin1 = new(line1.Locals[0].Px, line1.Locals[0].Py, line1.Locals[0].Pz);
            Vect direction1 = new(line1.Locals[0].Zx, line1.Locals[0].Zy, line1.Locals[0].Zz);
            Vect origin2 = new(line2.Locals[0].Px, line2.Locals[0].Py, line2.Locals[0].Pz);
            Vect direction2 = new(line2.Locals[0].Zx, line2.Locals[0].Zy, line2.Locals[0].Zz);
            Vect a = direction1 ^ direction2;
            double dot = a * a;
            if (dot == 0)
            {
                return null;
            }
            Vect b = (origin2 - origin1) ^ direction2;
            double t = (b * a) / dot;
            Vect pointA;

            pointA = origin1 + t * direction1;
            return pointA;


            //double t, u, tnum, deno, unum;




            //tnum = ((origin1[0] - origin2[0]) * (-direction2[1])) - ((origin1[1] - origin2[1]) * (-direction2[0]));
            //unum = ((origin1[0] - origin2[0]) * (-direction1[1])) - ((origin1[1] - origin2[1]) * (-direction1[0]));
            //deno = ((-direction1[0]) * (-direction2[1])) - ((-direction1[1]) * (-direction2[0]));


            //t = tnum / deno;
            //u = unum / deno;

            //Vect pointA, pointB;

            //pointA = origin1 + t * direction1;
            //pointB = origin2 + u * direction2;
            //double difference = (pointA - pointB).Norm;
            //if (difference < 0.0001)
            //{
            //    return pointA;
            //}
            //return null;
        }


        //getting rid of the inner shapes that are under the threshold, for now it only deals with circles, lines/planes will be implemented very shortly
        public static void RemoveInnerCircles(GimData gimData, double threshold)
        {
            List<uint> shapesToBeRemoved = new();
            List<uint> innerCircles = GetInnerCircles(gimData);
            for (int i = 0; i < innerCircles.Count; i++)
            {
                uint innerCircleIndex = innerCircles[i];
                GimElement innerCircle = gimData.elements[(int)innerCircleIndex];
                uint innerWireIndex = innerCircle.Locals[0].Parent;
                GimElement innerWire = gimData.elements[(int)innerWireIndex];
                double arclength = (innerCircle.ElementDoubles[2] - innerCircle.ElementDoubles[1]) * innerCircle.ElementDoubles[0];
                if (arclength < threshold)
                {
                    //adding the index of the circle to be removed + removing it from the wire's element list
                    shapesToBeRemoved.Add(innerCircleIndex);
                    innerWire.elements.Remove(innerCircleIndex);
                    double radius = innerCircle.ElementDoubles[0];
                    Vect center = new(innerCircle.Locals[0].Px, innerCircle.Locals[0].Py, innerCircle.Locals[0].Pz);
                    for (int j = 0; j < gimData.elements.Count; j++)
                    {
                        GimElement otherCircle = gimData.elements[j];
                        if (otherCircle.elementType == GimElementType.C_circle)
                        {
                            uint wireIndex = otherCircle.Locals[0].Parent;
                            double arclength1 = (otherCircle.ElementDoubles[2] - otherCircle.ElementDoubles[1]) * otherCircle.ElementDoubles[0];
                            double radius1 = otherCircle.ElementDoubles[0];
                            Vect center1 = new(otherCircle.Locals[0].Px, otherCircle.Locals[0].Py, otherCircle.Locals[0].Pz);
                            
                            if (CheckIfCirclesEqual(innerCircleIndex, (uint)j, gimData) && wireIndex != innerWireIndex)
                            {
                                shapesToBeRemoved.Add(GetParentOfParent(gimData, otherCircle));
                                break;

                            }


                        }
                    }
                }
                //if the wire is empty, we remove it
                if (innerWire.elements.Count == 0)
                {
                    shapesToBeRemoved.Add(innerWireIndex);
                }

            }
            List<uint> finalShapes = shapesToBeRemoved.Distinct().ToList();
            gimData.RemoveElementsAndTheirChildren(finalShapes);

        }

        public static void RemoveInnerLines(GimData gimData, double threshold) 
        {
            List<uint> shapesToBeRemoved = new();
            List<uint> innerLines = GetInnerLines(gimData);
            for (int i = 0; i < innerLines.Count; i++)
            {
                uint innerLineIndex = innerLines[i];
                GimElement innerLine = gimData.elements[(int)innerLineIndex];
                uint innerWireIndex = innerLine.Locals[0].Parent;
                GimElement innerWire = gimData.elements[(int)innerWireIndex];
                
                if (innerLine.ElementDoubles[1] < threshold)
                {
                    //adding the index of the line to be removed + removing it from the wire's element list
                    shapesToBeRemoved.Add(innerLineIndex);
                    innerWire.elements.Remove(innerLineIndex);
                    double[] line1StartEndPoints = CurveHelpers.getStartAndEndPointsOfCurve(innerLine, gimData);
                    Vect line1StartPoint = line1StartEndPoints[0..3];
                    Vect line1EndPoint = line1StartEndPoints[3..6];
                    
                    for (int j = 0; j < gimData.elements.Count; j++)
                    {
                        GimElement otherLine = gimData.elements[j];
                        if (otherLine.elementType == GimElementType.C_line)
                        {
                            uint wire2Index = otherLine.Locals[0].Parent;
                            double[] otherLineStartEndPoints = CurveHelpers.getStartAndEndPointsOfCurve (otherLine, gimData);
                            Vect otherLineStartPoint = otherLineStartEndPoints[0..3];
                            Vect otherLineEndPoint = otherLineStartEndPoints[3..6];
                            //we check if the current inner line shares a 
                            if ((line1StartPoint == otherLineStartPoint && line1EndPoint == otherLineEndPoint ||(line1StartPoint == otherLineEndPoint && otherLineStartPoint == line1EndPoint )) && innerLineIndex != j && innerWireIndex != wire2Index)
                            {
                                shapesToBeRemoved.Add(GetParentOfParent(gimData, otherLine));
                                break;

                            }


                        }
                    }
                }
                //if the wire is empty, we remove it
                if (innerWire.elements.Count == 0)
                {
                    shapesToBeRemoved.Add(innerWireIndex);
                }

            }
            List<uint> finalShapes = shapesToBeRemoved.Distinct().ToList();
            gimData.RemoveElementsAndTheirChildren(finalShapes);
        }

        //this function takes care of removing curves that are under the threshold on the profile face
        public static void BigFaceCurvesRemoval(GimData gimData, double threshold)
        {
            List<uint> linesgone = new();
            for (uint i = 0; i < gimData.elements.Count; i++)
            {

                GimElement gimElement = gimData.elements[(int)i];
                if (gimElement.elementType == GimElementType.C_line)
                {
                    if (gimElement.ElementDoubles[1] < threshold)
                    {

                        Dictionary<uint, List<Vect>> linesToBeTreated = new();

                        //if the length of the line is under the threshold, we get its start and end point
                        uint wireIndex = gimElement.Locals[0].Parent;
                        GimElement wire = gimData.elements[(int)wireIndex];
                        double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(gimElement, gimData, 0);
                        Vect line1StartPoint = new(line1Coords[0], line1Coords[1], line1Coords[2]);
                        Vect line1EndPoint = new(line1Coords[3], line1Coords[4], line1Coords[5]);


                        for (uint j = 0; j < wire.elements.Count; j++)
                        {
                            //we'll know search for the 2 lines that are adjacent to the current line we are dealing with and store their start/end point in a dictionnary
                            uint gimElement1Index = wire.elements[(int)j];
                            GimElement gimElement1 = gimData.elements[(int)gimElement1Index];
                            if (gimElement1.elementType == GimElementType.C_line)
                            {
                                double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                Vect line2StartPoint = new(line2Coords[0], line2Coords[1], line2Coords[2]);
                                Vect line2EndPoint = new(line2Coords[3], line2Coords[4], line2Coords[5]);

                                if ((line1StartPoint == line2StartPoint || line1StartPoint == line2EndPoint || line2StartPoint == line1EndPoint || line1EndPoint == line2EndPoint) && i != gimElement1Index)
                                {
                                    linesToBeTreated[gimElement1Index] = new List<Vect>();
                                    linesToBeTreated[gimElement1Index].Add(line2StartPoint);
                                    linesToBeTreated[gimElement1Index].Add(line2EndPoint);
                                }
                            }

                        }
                        //as a ""safety measure"" we'll only proceed if there's indeed only 2 lines
                        if (linesToBeTreated.Count == 2)
                        {

                            Vect intersection = new();
                            List<uint> lines = new();
                            //we add the 2 lines in a new list
                            foreach (var element in linesToBeTreated)
                            {
                                lines.Add(element.Key);
                            }

                            GimElement line1 = gimData.elements[(int)lines[0]];
                            GimElement line2 = gimData.elements[(int)lines[1]];
                            Vect Dir1 = new(line1.Locals[0].Zx, line1.Locals[0].Zy, line1.Locals[0].Zz);
                            Vect Dir2 = new(line2.Locals[0].Zx, line2.Locals[0].Zy, line2.Locals[0].Zz);
                            Vect pointline1, pointline2;
                            //so far in the treatment, we assumed that the lines under threshold are "edges" meaning the direction of the 2 adjacent lines can't be equal
                            //since we are now increasing the threshold, this will need to be adjusted, it will be as we progress
                            if (Dir1 != Dir2 && -Dir1 != Dir2)
                            {
                                //getting the 2 extremities of our new lines
                                Vect intersectionPoint = GetIntersectionPoint(line1, line2);
                                if (linesToBeTreated[lines[0]][0] != line1StartPoint && linesToBeTreated[lines[0]][0] != line1EndPoint)
                                {
                                    pointline1 = linesToBeTreated[lines[0]][0];
                                }
                                else
                                {
                                    pointline1 = linesToBeTreated[lines[0]][1];

                                }
                                if (linesToBeTreated[lines[1]][0] != line1StartPoint && linesToBeTreated[lines[1]][0] != line1EndPoint)
                                {
                                    pointline2 = linesToBeTreated[lines[1]][0];
                                }
                                else
                                {
                                    pointline2 = linesToBeTreated[lines[1]][1];
                                }

                                //after getting the 2 extremities, we now do the proper treatment
                                double t1 = (pointline1 - intersectionPoint).Norm;
                                Vect direction = (intersectionPoint - pointline1).Direction;
                                direction.ApplyToLocal(line1.locals[0]);
                                line1.locals[0].px = pointline1[0];
                                line1.locals[0].py = pointline1[1];
                                line1.locals[0].pz = pointline1[2];
                                line1.elementDoubles[0] = 0;
                                line1.elementDoubles[1] = t1;
                                double t2 = (pointline2 - intersectionPoint).Norm;
                                Vect direction2 = (intersectionPoint - pointline2).Direction;

                                direction2.ApplyToLocal(line2.locals[0]);
                                line2.locals[0].px = pointline2[0];
                                line2.locals[0].py = pointline2[1];
                                line2.locals[0].pz = pointline2[2];
                                line2.elementDoubles[0] = 0;
                                line2.elementDoubles[1] = t2;
                                linesgone.Add(i);


                            }



                        }


                    }



                }
                //same principle if we are dealing with a circle with 2 adjacent lines to it
                else if (gimElement.elementType == GimElementType.C_circle)
                {
                    double arclength = (gimElement.ElementDoubles[2] - gimElement.ElementDoubles[1]) * gimElement.ElementDoubles[0];
                    if (arclength < threshold)
                    {
                        Dictionary<uint, List<Vect>> linesToBeTreated = new();
                        uint wireIndex = gimElement.Locals[0].Parent;
                        GimElement wire = gimData.elements[(int)wireIndex];
                        double[] circle1Coords = CurveHelpers.getStartAndEndPointsOfCurve(gimElement, gimData, 0);
                        Vect circle1StartPoint = new(circle1Coords[0], circle1Coords[1], circle1Coords[2]);
                        Vect circle1EndPoint = new(circle1Coords[3], circle1Coords[4], circle1Coords[5]);
                        for (uint j = 0; j < wire.elements.Count; j++)
                        {
                            uint gimElement1Index = wire.elements[(int)j];
                            GimElement gimElement1 = gimData.elements[(int)gimElement1Index];
                            if (gimElement1.elementType == GimElementType.C_line)
                            {
                                double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                Vect line2StartPoint = new(line2Coords[0], line2Coords[1], line2Coords[2]);
                                Vect line2EndPoint = new(line2Coords[3], line2Coords[4], line2Coords[5]);
                                if ((circle1StartPoint == line2StartPoint || circle1StartPoint == line2EndPoint || circle1EndPoint == line2StartPoint || circle1EndPoint == line2EndPoint) && i != gimElement1Index)
                                {
                                    linesToBeTreated[gimElement1Index] = new List<Vect>();
                                    linesToBeTreated[gimElement1Index].Add(line2StartPoint);
                                    linesToBeTreated[gimElement1Index].Add(line2EndPoint);
                                }
                            }
                        }
                        if (linesToBeTreated.Count == 2)
                        {
                            List<uint> lines = new();
                            foreach (var element in linesToBeTreated)
                            {
                                lines.Add(element.Key);
                            }
                            GimElement line1 = gimData.elements[(int)lines[0]];
                            GimElement line2 = gimData.elements[(int)lines[1]];
                            Vect Dir1 = new(line1.Locals[0].Zx, line1.Locals[0].Zy, line1.Locals[0].Zz);
                            Vect Dir2 = new(line2.Locals[0].Zx, line2.Locals[0].Zy, line2.Locals[0].Zz);
                            Vect pointline1, pointline2;
                            if (Dir1 != Dir2 && -Dir1 != Dir2)
                            {
                                Vect intersectionPoint = GetIntersectionPoint(line1, line2);
                                if (linesToBeTreated[lines[0]][0] != circle1StartPoint && linesToBeTreated[lines[0]][0] != circle1EndPoint)
                                {
                                    pointline1 = linesToBeTreated[lines[0]][0];
                                }
                                else
                                {
                                    pointline1 = linesToBeTreated[lines[0]][1];

                                }
                                if (linesToBeTreated[lines[1]][0] != circle1StartPoint && linesToBeTreated[lines[1]][0] != circle1EndPoint)
                                {
                                    pointline2 = linesToBeTreated[lines[1]][0];
                                }
                                else
                                {
                                    pointline2 = linesToBeTreated[lines[1]][1];
                                }

                                double t1 = (pointline1 - intersectionPoint).Norm;
                                Vect direction = (intersectionPoint - pointline1).Direction;
                                direction.ApplyToLocal(line1.locals[0]);
                                line1.locals[0].px = pointline1[0];
                                line1.locals[0].py = pointline1[1];
                                line1.locals[0].pz = pointline1[2];
                                line1.elementDoubles[0] = 0;
                                line1.elementDoubles[1] = t1;
                                double t2 = (pointline2 - intersectionPoint).Norm;
                                Vect direction2 = (intersectionPoint - pointline2).Direction;

                                direction2.ApplyToLocal(line2.locals[0]);
                                line2.locals[0].px = pointline2[0];
                                line2.locals[0].py = pointline2[1];
                                line2.locals[0].pz = pointline2[2];
                                line2.elementDoubles[0] = 0;
                                line2.elementDoubles[1] = t2;
                                linesgone.Add(i);
                            }

                        }
                    }


                }



            }
            gimData.RemoveElementsAndTheirChildren(linesgone);
        }


        //to detect common lines between two planes, we use this function
        public static int DetectCommonLines(GimData gimData, uint plane1Index, uint plane2Index)
        {
            List<int> line = new();
            GimElement plane1 = gimData.elements[(int)plane1Index];
            GimElement plane2 = gimData.elements[(int)plane2Index];
            uint wire1Index = plane1.Elements[0];
            uint wire2Index = plane2.Elements[0];
            GimElement wire1 = gimData.elements[(int)wire1Index];
            GimElement wire2 = gimData.elements[(int)wire2Index];


            for (int i = 0; i < wire1.Elements.Count; i++)
            {
                uint line1Index = wire1.Elements[i];
                GimElement line1 = gimData.elements[(int)line1Index];
                double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData, 0);
                Vect line1StartPoint = new(line1Coords[0], line1Coords[1], line1Coords[2]);
                Vect line1EndPoint = new(line1Coords[3], line1Coords[4], line1Coords[5]);
                for (int j = 0; j < wire2.Elements.Count; j++)
                {
                    uint line2Index = wire2.Elements[j];
                    GimElement line2 = gimData.elements[(int)line2Index];
                    double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData, 0);
                    Vect line2StartPoint = new(line2Coords[0], line2Coords[1], line2Coords[2]);
                    Vect line2EndPoint = new(line2Coords[3], line2Coords[4], line2Coords[5]);
                    if ((line1StartPoint == line2StartPoint && line1EndPoint == line2EndPoint) || (line1StartPoint == line2EndPoint && line2StartPoint == line1EndPoint))
                    {
                        line.Add((int)line2Index);

                    }
                }
            }

            //since planes can only have one line in common, we return the first (which is technically the only) value in the list, if there's no value, we return -1
            if (line.Count > 0)
            {
                return line[0];
            }
            else
                return -1;

        }

        //for the side faces, unlike the profile face, the adjacent lines are in other shapes, so we need to get the adjacent lines by looking at the adjacent planes
        //this function takes care of that and returns a dictionnary containing lines/circles under the threshold in the Key part, and the adjacent lines of that line in the Value part
        //the dictionary used here is the return value of the "LineDetection" function
        public static Dictionary<int, List<int>> GetAdjacentLinesofAdjacentPlanes(GimData gimData, double threshold, Dictionary<int, List<int>> adjacentPlanes)
        {
            Dictionary<int, List<int>> adjacentlines = new();
            foreach (KeyValuePair<int, List<int>> entry in adjacentPlanes)
            {
                GimElement plane = gimData.elements[entry.Key];
                uint wireIndex = plane.Elements[0];
                GimElement wire = gimData.elements[(int)wireIndex];
                for (int i = 0; i < wire.elements.Count; i++)
                {
                    uint gimElementIndex = wire.elements[i];
                    GimElement gimElement = gimData.elements[((int)gimElementIndex)];
                    if (gimElement.elementType == GimElementType.C_line)
                    {
                        if (gimElement.elementDoubles[1] < threshold)
                        {
                            adjacentlines[(int)gimElementIndex] = new List<int>();
                        }
                    }
                    else if (gimElement.elementType == GimElementType.C_circle)
                    {
                        double arclength = (gimElement.ElementDoubles[2] - gimElement.ElementDoubles[1]) * gimElement.ElementDoubles[0];
                        if (arclength < threshold)
                        {
                            adjacentlines[(int)gimElementIndex] = new List<int>();
                        }
                    }

                }

            }

            foreach (var entry in adjacentlines)
            {
                GimElement line1 = gimData.elements[entry.Key];
                double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData, 0);
                Vect line1StartPoint = new(line1Coords[0], line1Coords[1], line1Coords[2]);
                Vect line1EndPoint = new(line1Coords[3], line1Coords[4], line1Coords[5]);
                foreach (var entry2 in adjacentPlanes)
                {
                    GimElement plane = gimData.elements[entry2.Key];
                    if (line1.Locals[0].Parent == plane.elements[0])
                    {
                        List<int> planesIndexes = entry2.Value;
                        if (planesIndexes.Count <= 2)
                        {
                            for (int i = 0; i < planesIndexes.Count; i++)
                            {
                                //first we see if they share a common line
                                int commonLineIndex = DetectCommonLines(gimData, (uint)entry2.Key, (uint)planesIndexes[i]);
                                //if there is a common line, the function will return the index of the line, if there isn't, it'll return -1
                                if (commonLineIndex != -1)
                                {

                                    GimElement plane1 = gimData.elements[planesIndexes[i]];
                                    uint wireIndex = plane1.Elements[0];
                                    GimElement wire = gimData.elements[(int)wireIndex];
                                    for (int j = 0; j < wire.elements.Count; j++)
                                    {
                                        uint lineIndex = wire.Elements[j];
                                        if (lineIndex != commonLineIndex)
                                        {
                                            GimElement line2 = gimData.elements[(int)lineIndex];
                                            double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData, 0);
                                            Vect line2StartPoint = new(line2Coords[0], line2Coords[1], line2Coords[2]);
                                            Vect line2EndPoint = new(line2Coords[3], line2Coords[4], line2Coords[5]);
                                            //since in the dictionnary we get the whole adjacent shape, we just have to access that shape and see which lines are adjacent to our current line/circle
                                            if ((line1StartPoint == line2StartPoint || line1StartPoint == line2EndPoint || line2StartPoint == line1EndPoint || line1EndPoint == line2EndPoint))
                                            {
                                                adjacentlines[entry.Key].Add((int)lineIndex);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }





            return adjacentlines;
        }

        //after dealing with the side shapes, we have to fix the lines that weren't moved, this is where it happens
        public static void FixLines(GimData gimData, List<int> listofPlanes)
        {

            for (uint i = 0; i < listofPlanes.Count; i++)
            {
                //getting the wire of the plane
                GimElement plane = gimData.elements[listofPlanes[(int)i]];
                uint bigWireIndex = plane.elements[0];
                GimElement bigWire = gimData.elements[(int)bigWireIndex];
                //we are going to proceed line by line and see which ones we are going to adjust by checking if they have an intersection point or no
                for (int j = 0; j < bigWire.Elements.Count; j++)
                {
                    uint gimElementIndex = bigWire.elements[j];
                    GimElement gimElement = gimData.elements[(int)gimElementIndex];
                    if (gimElement.elementType == GimElementType.C_line)
                    {
                        //getting the coords of the first line
                        double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(gimElement, gimData, 0);
                        Vect line1StartPoint = new(line1Coords[0], line1Coords[1], line1Coords[2]);
                        Vect line1EndPoint = new(line1Coords[3], line1Coords[4], line1Coords[5]);


                        for (int k = 0; k < bigWire.elements.Count; k++)
                        {
                            //coords of the second line
                            uint gimElement1Index = bigWire.elements[k];
                            GimElement gimElement1 = gimData.elements[(int)gimElement1Index];
                            double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                            Vect line2StartPoint = new(line2Coords[0], line2Coords[1], line2Coords[2]);
                            Vect line2EndPoint = new(line2Coords[3], line2Coords[4], line2Coords[5]);
                            //we're getting the intersection points of two lines here
                            Vect pointIntersection = GetIntersectionPoint(gimElement, gimElement1);
                            //if there is indeed an intersection point, we can proceed

                            if (pointIntersection != null && gimElementIndex != gimElement1Index)
                            {
                                //the treatment will be depending on which point is not the intersection point
                                if (line2StartPoint == pointIntersection || line2EndPoint == pointIntersection)
                                {
                                    Vect point1 = new();


                                    if (line2StartPoint != pointIntersection)
                                    {
                                        point1 = line2StartPoint;
                                    }
                                    else
                                    {
                                        point1 = line2EndPoint;
                                    }
                                    //after getting the first point, we calculate the distance between the other poitns and our first point
                                    //logically, the shortest line (since we are dealing with edges) should be the line containing our second point
                                    double distance1 = (line1StartPoint - point1).Norm;
                                    double distance2 = (line1EndPoint - point1).Norm;
                                    Vect point2 = new();
                                    if (distance1 < distance2)
                                    {
                                        point2 = line1StartPoint;
                                    }
                                    else
                                    {
                                        point2 = line1EndPoint;
                                    }
                                    //after getting the 2 points, we do the usual treatment
                                    double t2 = (point1 - point2).Norm;
                                    Vect direction = (point2 - point1).Direction;
                                    direction.ApplyToLocal(gimElement1.locals[0]);
                                    gimElement1.locals[0].px = point1[0];
                                    gimElement1.locals[0].py = point1[1];
                                    gimElement1.locals[0].pz = point1[2];
                                    gimElement1.elementDoubles[0] = 0;
                                    gimElement1.elementDoubles[1] = t2;

                                }
                                //same logic applies here but with the other line
                                else if (line1StartPoint == pointIntersection || line1EndPoint == pointIntersection)
                                {
                                    Vect point1 = new();
                                    if (line1StartPoint != pointIntersection)
                                    {
                                        point1 = line1StartPoint;
                                    }
                                    else
                                    {
                                        point1 = line1EndPoint;
                                    }
                                    double distance1 = (point1 - line2StartPoint).Norm;
                                    double distance2 = (point1 - line2EndPoint).Norm;
                                    Vect point2 = new();
                                    if (distance1 < distance2)
                                    {
                                        point2 = line2StartPoint;
                                    }
                                    else
                                    {
                                        point2 = line2EndPoint;
                                    }

                                    double t2 = (point1 - point2).Norm;
                                    Vect direction = (point2 - point1).Direction;
                                    direction.ApplyToLocal(gimElement.locals[0]);
                                    gimElement.locals[0].px = point1[0];
                                    gimElement.locals[0].py = point1[1];
                                    gimElement.locals[0].pz = point1[2];
                                    gimElement.elementDoubles[0] = 0;
                                    gimElement.elementDoubles[1] = t2;

                                }
                            }


                        }

                    }
                }

            }

        }




        //this takes care of the side faces
        public static void SideFaceCurvesRemoval(GimData gimData, double threshold)
        {
            //first we deal with the profile faces
            BigFaceCurvesRemoval(gimData, threshold);
            //after that, we now use the LineDetection function to get the curves under the threshold, since we used the profile removing function
            //this should only return side profile shapes/curves and so with that we can proceed properly with this function
            Dictionary<int, List<int>> removingstuff = LineDetection(gimData, threshold);
            //this is some kind of "safety measure"
            foreach (var entry in removingstuff)
            {
                if (entry.Value.Count < 2)
                {
                    removingstuff.Remove(entry.Key);
                }
            }
            //we now use the function we created to get the adjacent lines in the adjacent planes
            Dictionary<int, List<int>> adjacentslines = GetAdjacentLinesofAdjacentPlanes(gimData, threshold, removingstuff);

            List<uint> planesToGo = new();
            List<int> temp = new();
            //adding all the planes with curves under threshold to a list so that we can remove them later
            //and adding the adjacent planes to another list so that we can treat them later
            foreach (var entry in removingstuff)
            {
                planesToGo.Add((uint)entry.Key);
                List<int> temp2 = entry.Value;
                temp.AddRange(temp2);

            }
            List<int> planes = temp.Distinct().ToList();

            //same logic as the profile face treatment, we'll be getting the 2 extremities points and do the necessary steps
            foreach (KeyValuePair<int, List<int>> entry in adjacentslines)
            {

                GimElement midline = gimData.elements[entry.Key];
                //getting the coords of the line under the threshold, is it between the two other lines here and thus why it's called "midline"
                double[] midlineCoords = CurveHelpers.getStartAndEndPointsOfCurve(midline, gimData, 0);
                Vect midlineStartPoint = new(midlineCoords[0], midlineCoords[1], midlineCoords[2]);
                Vect midlineEndPoint = new(midlineCoords[3], midlineCoords[4], midlineCoords[5]);
                List<int> otherlines = entry.Value;
                //another safety measure
                if (otherlines.Count > 1)
                {
                    GimElement line1 = gimData.elements[otherlines[0]];
                    GimElement line2 = gimData.elements[otherlines[1]];
                    if (line1.elementType == GimElementType.C_line && line2.elementType == GimElementType.C_line)
                    {
                        Vect intersection = GetIntersectionPoint(line1, line2);
                        //we only proceed if there's an intersection point between the 2 lines that are staying (as in, the 2 other lines that aren't the midline)
                        if (intersection != null)
                        {
                            //getting the coords of the 2 lines if they do indeed intersect
                            double[] line1Coords = CurveHelpers.getStartAndEndPointsOfCurve(line1, gimData, 0);
                            Vect line1StartPoint = new(line1Coords[0], line1Coords[1], line1Coords[2]);
                            Vect line1EndPoint = new(line1Coords[3], line1Coords[4], line1Coords[5]);
                            double[] line2Coords = CurveHelpers.getStartAndEndPointsOfCurve(line2, gimData, 0);
                            Vect line2StartPoint = new(line2Coords[0], line2Coords[1], line2Coords[2]);
                            Vect line2EndPoint = new(line2Coords[3], line2Coords[4], line2Coords[5]);
                            Vect point1 = new();
                            //we get the extremities of our new lines here
                            if (line1StartPoint != midlineStartPoint && line1StartPoint != midlineEndPoint)
                            {
                                point1 = line1StartPoint;
                            }
                            else
                            {
                                point1 = line1EndPoint;
                            }
                            Vect point2 = new();
                            if (line2StartPoint != midlineStartPoint && line2StartPoint != midlineEndPoint)
                            {
                                point2 = line2StartPoint;
                            }
                            else
                            {
                                point2 = line2EndPoint;
                            }
                            //and then finally we do the usual treatment
                            double t1 = (point1 - intersection).Norm;
                            Vect direction = (intersection - point1).Direction;
                            direction.ApplyToLocal(line1.locals[0]);
                            line1.locals[0].px = point1[0];
                            line1.locals[0].py = point1[1];
                            line1.locals[0].pz = point1[2];
                            line1.elementDoubles[0] = 0;
                            line1.elementDoubles[1] = t1;

                            double t2 = (point2 - intersection).Norm;
                            Vect direction1 = (intersection - point2).Direction;
                            direction1.ApplyToLocal(line2.locals[0]);
                            line2.locals[0].px = point2[0];
                            line2.locals[0].py = point2[1];
                            line2.locals[0].pz = point2[2];
                            line2.elementDoubles[0] = 0;
                            line2.elementDoubles[1] = t2;
                        }
                    }
                }

            }

            //finally after modifying the planes, some lines will still need treatment, and that's when this function comes in handy
            FixLines(gimData, planes);
            //last step is to remove all the elements and we are good to go
            gimData.RemoveElementsAndTheirChildren(planesToGo);





        }

        //this takes care of turning cylinders with circles with an arclength less than the threshold into 2 planes

        public static void ArcChange(GimData gimData, double threshold)
        {
            //initializing the list of elements to be removed after we replace them
            List<uint> toBeRemoved = new();
            for (int i = 0; i < gimData.elements.Count; i++)
            {
                GimElement currentElement = gimData.elements[(int)i];

                //we're only treating cylinders in this function
                if (currentElement.elementType == GimElementType.F_cilinder)
                {
                    //we need the shell in order to add elements to it later on
                    uint currentShellidx = currentElement.locals[0].Parent;
                    GimElement currentshell = gimData.elements[(int)currentShellidx];

                    //getting the wire of the cylinder
                    GimElement arcWire = gimData.elements[(int)currentElement.elements[0]];
                    //initializing the arclength
                    double arcLength = 0;

                    //initializing the variables that will contain two points of one of the circles of the cylinder
                    List<Vect> points = new List<Vect>();
                    Vect point1 = new Vect();
                    Vect point2 = new Vect();

                    //getting the arclength of the cylinder as well as two points of one of the circles
                    foreach (var lineidx in arcWire.elements)
                    {
                        GimElement line = gimData.elements[(int)lineidx];
                        if (line.elementType == GimElementType.C_circle)
                        {
                            arcLength = (line.ElementDoubles[2] - line.ElementDoubles[1]) * line.ElementDoubles[0];
                            double[] linePoints = CurveHelpers.getStartAndEndPointsOfCurve(line, gimData);
                            point1 = linePoints[0..3];
                            point2 = linePoints[3..6];
                            break;
                        }
                    }
                    //if it's a full circle and below the threshold then it gets removed entirely without getting replaced
                    if (arcLength <= threshold && point1 == point2)
                    {
                        toBeRemoved.Add((uint)i);
                    }

                    //it needs to be treated if it's no a full circle
                    if (arcLength <= threshold && point1 != point2)
                    {
                        toBeRemoved.Add((uint)i);

                        //getting the different points of the cylinder as well as the new target point
                        //the four points of the cylinder can be obtained from the two circles at the top and bottom

                        foreach (var idx in arcWire.elements)
                        {
                            GimElement line = gimData.elements[(int)idx];
                            int pla = 0;
                            if (line.elementType == GimElementType.C_circle)
                            {
                                //getting the start and end point of each circle
                                double[] linePoints = CurveHelpers.getStartAndEndPointsOfCurve(line, gimData);
                                Vect StartLine = new Vect(linePoints[0..3]);
                                Vect EndLine = new Vect(linePoints[3..6]);
                                pla++;

                                //calculating the midpoint of the two points for later calculation
                                Vect MidPoint = (StartLine + EndLine) / 2;
                                //getting the radius
                                double radius = line.ElementDoubles[0];
                                //getting the center point
                                Vect centerPoint = new Vect(
                                line.locals[0].Px,
                                line.locals[0].Py,
                                line.locals[0].Pz);

                                //the direction between the center point and the mid point is the same as the direction between the center point and the target
                                //finding the direction between the center point and the mid point
                                Vect direction = (MidPoint - centerPoint).Direction;

                                //getting the distance between the mid point and center point for later calculation
                                double smallDistance = (MidPoint - centerPoint).Norm;

                                //calculating the distance between the center point and the target
                                double distance = radius * radius / smallDistance;

                                //finding the target
                                Vect target = centerPoint + (distance * direction);

                                //storing all of the points in an ordered manner 
                                //form a profile view: StartLine is the start of the first line, target is both the end of the first line and the start of the second line, EndLine is the end of the second line
                                points.Add(StartLine);
                                points.Add(target);
                                points.Add(EndLine);
                            }
                        }

                            //if there are more than 6 points then something is wrong                                                                                                                                                                   }
                            if (points.Count > 6) { break; }

                            // getting the number of elements to determin the index of each element that would be added
                            uint nbElements = (uint)gimData.Elements.Count;

                            //creating the lines of the new planes
                            GimElement line1 = new GimElement();
                            line1.locals = new List<GimLocal>();
                            line1.elementType = GimElementType.C_line;

                            GimElement line2 = new GimElement();
                            line2.locals = new List<GimLocal>();
                            line2.elementType = GimElementType.C_line;

                            GimElement line3 = new GimElement();
                            line3.locals = new List<GimLocal>();
                            line3.elementType = GimElementType.C_line;

                            GimElement line4 = new GimElement();
                            line4.locals = new List<GimLocal>();
                            line4.elementType = GimElementType.C_line;

                            GimElement line5 = new GimElement();
                            line5.locals = new List<GimLocal>();
                            line5.elementType = GimElementType.C_line;

                            GimElement line6 = new GimElement();
                            line6.locals = new List<GimLocal>();
                            line6.elementType = GimElementType.C_line;

                            GimElement line7 = new GimElement();
                            line7.locals = new List<GimLocal>();
                            line7.elementType = GimElementType.C_line;

                            GimElement line8 = new GimElement();
                            line8.locals = new List<GimLocal>();
                            line8.elementType = GimElementType.C_line;


                            //creating the new planes
                            GimElement plane1 = new GimElement();
                            plane1.elementType = GimElementType.F_plane;
                            plane1.locals = new List<GimLocal>();
                            //we attribute the locals of the original cylinder and then do the necessary changes
                            plane1.locals.Add(CloneLocal(currentElement.locals[0]));
                            plane1.locals[0].parent = currentShellidx;
                            //adding one of the points to its locals
                            plane1.locals[0].px = Math.Round(points[1][0], 4);
                            plane1.locals[0].py = Math.Round(points[1][1], 4);
                            plane1.locals[0].pz = Math.Round(points[1][2], 4);
                            // adding the index of the wire to its elements
                            plane1.elements.Add(nbElements + 1);
                            //adding its index to the shell elements
                            currentshell.elements.Add(nbElements);


                            //same thing with plane1    
                            GimElement plane2 = new GimElement();
                            plane2.elementType = GimElementType.F_plane;
                            plane2.locals = new List<GimLocal>();
                            plane2.locals.Add(CloneLocal(currentElement.locals[0]));
                            plane2.locals[0].parent = currentShellidx;
                            plane2.locals[0].px = Math.Round(points[1][0], 4);
                            plane2.locals[0].py = Math.Round(points[1][1], 4);
                            plane2.locals[0].pz = Math.Round(points[1][2], 4);
                            plane2.elements.Add(nbElements + 7);
                            currentshell.elements.Add(nbElements + 6);


                            //creating wires
                            GimElement wire1 = new GimElement();
                            wire1.elementType = GimElementType.wire;
                            wire1.locals = new List<GimLocal>();
                            //we attribute the locals of the original wire and then do the necessary changes
                            wire1.locals.Add(CloneLocal(arcWire.locals[0]));
                            //we put the index of plane one as its parent
                            wire1.locals[0].parent = nbElements;

                            //same as wire one
                            GimElement wire2 = new GimElement();
                            wire2.elementType = GimElementType.wire;
                            wire2.locals = new List<GimLocal>();
                            wire2.locals.Add(CloneLocal(arcWire.locals[0]));
                            wire2.locals[0].parent = nbElements + 6;


                            //creating the lines
                            //getting the length of the line
                            double t1 = (points[1] - points[0]).Norm;
                            //getting the direction of the line
                            Vect direction1 = (points[0] - points[1]).Direction;
                            //rounding the points
                            for (int dot = 0; dot < 3; dot++) { direction1[dot] = Math.Round(direction1[dot], 4); }
                            //creating the line's locals
                            GimLocal l = new GimLocal();
                            //creating the line's doubles
                            double[] d = { 0, t1 };
                            direction1.ApplyToLocal(l);
                            //putting the wire's index as the parent
                            l.parent = nbElements + 1;
                            l.px = Math.Round(points[1][0], 4);
                            l.py = Math.Round(points[1][1], 4);
                            l.pz = Math.Round(points[1][2], 4);
                            //adding locals
                            line1.locals.Add(l);
                            //adding doubles
                            line1.elementDoubles = d;
                            //adding the line's index to the wire elements
                            wire1.elements.Add(nbElements + 2);



                            //adding this line's direction to the plane
                            plane1.locals[0].Xx = direction1[0];
                            plane1.locals[0].Xy = direction1[1];
                            plane1.locals[0].Xz = direction1[2];



                            //the rest of the lines are done exactly like the first    
                            double t2 = (points[3] - points[0]).Norm;
                            Vect direction2 = (points[0] - points[3]).Direction;
                            for (int dot = 0; dot < 3; dot++) { direction2[dot] = Math.Round(direction2[dot], 4); }
                            GimLocal l2 = new GimLocal();
                            direction2.ApplyToLocal(l2);
                            l2.parent = nbElements + 1;
                            l2.px = Math.Round(points[3][0], 4);
                            l2.py = Math.Round(points[3][1], 4);
                            l2.pz = Math.Round(points[3][2], 4);
                            double[] d2 = { 0, t2 };
                            line2.locals.Add(l2);
                            line2.elementDoubles = d2;
                            wire1.elements.Add(nbElements + 3);

                            plane1.locals[0].Yx = direction2[0];
                            plane1.locals[0].Yy = direction2[1];
                            plane1.locals[0].Yz = direction2[2];


                            double t3 = (points[4] - points[3]).Norm;
                            Vect direction3 = (points[3] - points[4]).Direction;
                            for (int dot = 0; dot < 3; dot++) { direction3[dot] = Math.Round(direction3[dot], 4); }
                            GimLocal l3 = new GimLocal();
                            direction3.ApplyToLocal(l3);
                            l3.parent = nbElements + 1;
                            l3.px = Math.Round(points[4][0], 4);
                            l3.py = Math.Round(points[4][1], 4);
                            l3.pz = Math.Round(points[4][2], 4);
                            double[] d3 = { 0, t3 };
                            line3.locals.Add(l3);
                            line3.elementDoubles = d3;
                            wire1.elements.Add(nbElements + 4);

                            double t4_5 = (points[4] - points[1]).Norm;
                            Vect direction4_5 = (points[1] - points[4]).Direction;
                            for (int dot = 0; dot < 3; dot++) { direction4_5[dot] = Math.Round(direction4_5[dot], 4); }
                            GimLocal l4 = new GimLocal();
                            direction4_5.ApplyToLocal(l4);
                            l4.parent = nbElements + 1;
                            l4.px = Math.Round(points[4][0], 4);
                            l4.py = Math.Round(points[4][1], 4);
                            l4.pz = Math.Round(points[4][2], 4);
                            double[] d4 = { 0, t4_5 };
                            line4.locals.Add(l4);
                            line4.elementDoubles = d4;
                            wire1.elements.Add(nbElements + 5);

                            GimLocal l5 = new GimLocal();
                            direction4_5.ApplyToLocal(l5);
                            l5.parent = nbElements + 7;
                            l5.px = Math.Round(points[4][0], 4);
                            l5.py = Math.Round(points[4][1], 4);
                            l5.pz = Math.Round(points[4][2], 4);
                            double[] d5 = { 0, t4_5 };
                            line5.locals.Add(l5);
                            line5.elementDoubles = d5;
                            wire2.elements.Add(nbElements + 8);

                            plane2.locals[0].Xx = direction4_5[0];
                            plane2.locals[0].Xy = direction4_5[1];
                            plane2.locals[0].Xz = direction4_5[2];

                            double t6 = (points[2] - points[1]).Norm;
                            Vect direction6 = (points[1] - points[2]).Direction;
                            for (int dot = 0; dot < 3; dot++) { direction6[dot] = Math.Round(direction6[dot], 4); }
                            GimLocal l6 = new GimLocal();
                            direction6.ApplyToLocal(l6);
                            l6.parent = nbElements + 7;
                            l6.px = Math.Round(points[2][0], 4);
                            l6.py = Math.Round(points[2][1], 4);
                            l6.pz = Math.Round(points[2][2], 4);
                            double[] d6 = { 0, t6 };
                            line6.locals.Add(l6);
                            line6.elementDoubles = d6;
                            wire2.elements.Add(nbElements + 9);

                            plane2.locals[0].Yx = direction6[0];
                            plane2.locals[0].Yy = direction6[1];
                            plane2.locals[0].Yz = direction6[2];

                            double t7 = (points[5] - points[2]).Norm;
                            Vect direction7 = (points[2] - points[5]).Direction;
                            for (int dot = 0; dot < 3; dot++) { direction7[dot] = Math.Round(direction7[dot], 4); }
                            GimLocal l7 = new GimLocal();
                            direction7.ApplyToLocal(l7);
                            l7.parent = nbElements + 7;
                            l7.px = Math.Round(points[5][0], 4);
                            l7.py = Math.Round(points[5][1], 4);
                            l7.pz = Math.Round(points[5][2], 4);
                            double[] d7 = { 0, t7 };
                            line7.locals.Add(l7);
                            line7.elementDoubles = d7;
                            wire2.elements.Add(nbElements + 10);

                            double t8 = (points[5] - points[4]).Norm;
                            Vect direction8 = (points[4] - points[5]).Direction;
                            for (int dot = 0; dot < 3; dot++) { direction8[dot] = Math.Round(direction8[dot], 4); }
                            GimLocal l8 = new GimLocal();
                            direction8.ApplyToLocal(l8);
                            l8.parent = nbElements + 7;
                            l8.px = Math.Round(points[5][0], 4);
                            l8.py = Math.Round(points[5][1], 4);
                            l8.pz = Math.Round(points[5][2], 4);
                            double[] d8 = { 0, t8 };
                            line8.locals.Add(l8);
                            line8.elementDoubles = d8;
                            wire2.elements.Add(nbElements + 11);


                            //adding all of the elements to gim data    
                            gimData.Elements.Add(plane1);
                            gimData.Elements.Add(wire1);
                            gimData.Elements.Add(line1);
                            gimData.Elements.Add(line2);
                            gimData.Elements.Add(line3);
                            gimData.Elements.Add(line4);

                            gimData.Elements.Add(plane2);
                            gimData.Elements.Add(wire2);
                            gimData.Elements.Add(line5);
                            gimData.Elements.Add(line6);
                            gimData.Elements.Add(line7);
                            gimData.Elements.Add(line8);





                        }

                    }

                }
                gimData.RemoveElementsAndTheirChildren(toBeRemoved);

            }
        
        public static GimLocal CloneLocal(GimLocal l)
        {
            GimLocal clone = new GimLocal
            {
                parent = l.parent,
                px = l.px,
                py = l.py,
                pz = l.pz,
                xx = l.xx,
                xy = l.xy,
                xz = l.xz,
                yx = l.yx,
                yy = l.yy,
                yz = l.yz,
                zx = l.zx,
                zy = l.zy,
                zz = l.zz,
                rightHanded = l.rightHanded

            };
            return clone;
        }

        //function used to get the parent of a curve
        public static uint GetParentOfParent(GimData gimData, GimElement gimElement)
        {
            uint parentIndex, wireIndex;
            wireIndex = gimElement.Locals[0].Parent;
            GimElement wire = gimData.elements[(int)wireIndex];
            parentIndex = wire.locals[0].Parent;
            return parentIndex;
        }

        //this is a substitute for the profile detection function, after updating the original one, we'll most likely stop using this one
        public static List<uint> DetectingBigFaces(GimData gimData)
        {
            List<uint> temp = new();
            List<uint> bigFaces = new();
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                if (gimElement.elementType == GimElementType.wire)
                {
                    temp.Add((uint)gimElement.elements.Count);
                }
            }
            // this assumes we are working on a "profile" type of shape, another way to determine the big face more precisely was thought of and will be implemented shortly
            uint a = temp.Max();
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                if (gimElement.elementType == GimElementType.wire)
                {
                    if (gimElement.Elements.Count == a)
                    {
                        bigFaces.Add((uint)i);
                    }
                }
            }
            return bigFaces;
        }



        //since we are dealing with vectors and we need the cross product in a future treatment, we have to order all the vectors of the shape
        //in the side shapes, idea is to get the big shape directions, see which lines are superimposed with the side shapes and adjust them correspondly 
        public static Dictionary<uint, List<uint>> GettingOrderedSideProfileLines(GimData gimData)
        {
            //we're getting the profile faces here
            List<uint> bigwires = DetectingBigFaces(gimData);
            Dictionary<uint, List<uint>> orderedBigWiresLines = new();
            Dictionary<uint, List<uint>> orderedSideProfileLines = new();
            for (uint i = 0; i < bigwires.Count; i++)
            {
                if (!orderedBigWiresLines.ContainsKey(i))
                {
                    orderedBigWiresLines[i] = new List<uint>();
                }
                else
                {
                    orderedBigWiresLines[i].Clear();
                }
                //calling the function that fixes the direction of the big faces vectors
                orderedBigWiresLines[i].AddRange(FixPlaneLinesDirection(gimData, (int)bigwires[(int)i]));
            }

            foreach (KeyValuePair<uint, List<uint>> entry in orderedBigWiresLines)
            {
                //creating the new Keys in the dictionary containing the side faces lines
                List<uint> linesIndexes = entry.Value;
                if (!orderedSideProfileLines.ContainsKey(entry.Key))
                {
                    orderedSideProfileLines[entry.Key] = new List<uint>();
                }
                else
                {
                    orderedSideProfileLines[entry.Key].Clear();
                }
                for (int i = 0; i < linesIndexes.Count; i++)
                {
                    //we start doing the comparaison here, we go vector by vector
                    //first we start by getting the points of the current big face element
                    GimElement bigFaceElement = gimData.elements[(int)linesIndexes[i]];
                    double[] bigFaceEelementPoints = CurveHelpers.getStartAndEndPointsOfCurve(bigFaceElement, gimData, 0);
                    Vect bigFaceElementStartPoint = bigFaceEelementPoints[0..3];
                    Vect bigFaceElementEndPoint = bigFaceEelementPoints[3..6];
                    for (int j = 0; j < gimData.elements.Count; j++)
                    {
                        //then we check if the current side face element is a line or a circle 
                        GimElement sideFaceElement = gimData.elements[j];
                        if (sideFaceElement.ElementType == GimElementType.C_line && bigFaceElement.ElementType == GimElementType.C_line)
                        {
                            //if it's a line we get its points and then compare them with the current big face element
                            double[] sideFaceElementPoints = CurveHelpers.getStartAndEndPointsOfCurve(sideFaceElement, gimData, 0);
                            Vect sideFaceElementStartPoint = sideFaceElementPoints[0..3];
                            Vect sideFaceElementEndPoint = sideFaceElementPoints[3..6];
                            //if they have the same start and end point, we simply add them to the list
                            if (bigFaceElementStartPoint == sideFaceElementStartPoint && bigFaceElementEndPoint == sideFaceElementEndPoint && sideFaceElement != bigFaceElement)
                            {
                                orderedSideProfileLines[entry.Key].Add((uint)j);
                                break;
                            }
                            //if the start point of one of the lines equals the end point of the other, it means the direction of the current side face line has to be reversed
                            else if (bigFaceElementStartPoint == sideFaceElementEndPoint && bigFaceElementEndPoint == sideFaceElementStartPoint && sideFaceElement != bigFaceElement)
                            {
                                orderedSideProfileLines[entry.Key].Add((uint)j);
                                Vect newDirection = (bigFaceElementEndPoint - bigFaceElementStartPoint).Direction;
                                newDirection.ApplyToLocal(sideFaceElement.locals[0]);
                                sideFaceElement.locals[0].px = bigFaceElementStartPoint[0];
                                sideFaceElement.locals[0].py = bigFaceElementStartPoint[1];
                                sideFaceElement.locals[0].pz = bigFaceElementStartPoint[2];
                                break;
                            }
                        }

                        else if (sideFaceElement.ElementType == GimElementType.C_circle && bigFaceElement.ElementType == GimElementType.C_circle)
                        {
                            //same principle if it's a circle
                            double[] sideFaceElementPoints = CurveHelpers.getStartAndEndPointsOfCurve(sideFaceElement, gimData, 0);
                            Vect sideFaceElementStartPoint = sideFaceElementPoints[0..3];
                            Vect sideFaceElementEndPoint = sideFaceElementPoints[3..6];
                            //if they share the same start and end point, we just add it
                            if (bigFaceElementStartPoint == sideFaceElementStartPoint && bigFaceElementEndPoint == sideFaceElementEndPoint && sideFaceElement != bigFaceElement)
                            {
                                orderedSideProfileLines[entry.Key].Add((uint)j);
                                break;
                            }
                            //if not we go through the process of reversing the direction of the circle
                            else if (bigFaceElementStartPoint == sideFaceElementEndPoint && bigFaceElementEndPoint == sideFaceElementStartPoint && sideFaceElement != bigFaceElement)
                            {
                                orderedSideProfileLines[entry.Key].Add((uint)j);
                                double radius = sideFaceElement.elementDoubles[0];
                                double t2 = sideFaceElement.elementDoubles[2];
                                Vect direction1 = new(sideFaceElement.Locals[0].Xx, sideFaceElement.Locals[0].Xy, sideFaceElement.Locals[0].Xz);
                                Vect direction2 = new(sideFaceElement.Locals[0].Yx, sideFaceElement.Locals[0].Yy, sideFaceElement.Locals[0].Yz);

                                Vect origin = new(sideFaceElement.Locals[0].Px, sideFaceElement.Locals[0].Py, sideFaceElement.Locals[0].Pz);
                                Vect newDirection1 = (sideFaceElementEndPoint - origin).Direction;
                                double newt1 = t2 - (Math.PI / 2);
                                Vect newPoint = origin + radius * Math.Cos(newt1) * direction1 + radius * Math.Sin(newt1) * direction2;
                                Vect newDirection2 = (newPoint - origin).Direction;
                                Matrix circleDirection = new(newDirection1, newDirection2, newDirection1 ^ newDirection2);
                                circleDirection.ApplyToLocal(sideFaceElement.Locals[0]);
                            }
                        }
                    }
                }
            }

            return orderedSideProfileLines;
        }

        //in this function, we are going to "snap" lines that are under the threshold with the shapes adjacent to it together on the big face
        public static void BigFaceSnap(GimData gimData, double threshold)
        {
            List<uint> linesgone = new();
            List<uint> temp = new();
            List<uint> profileWires = new();
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                if (gimElement.elementType == GimElementType.wire)
                {
                    temp.Add((uint)gimElement.elements.Count);
                }
            }
            // this assumes we are working on a "profile" type of shape, another way to determine the big face more precisely was thought of and will be implemented shortly
            uint a = temp.Max();
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                if (gimElement.elementType == GimElementType.wire)
                {
                    if (gimElement.Elements.Count == a)
                    {
                        profileWires.Add((uint)i);
                    }
                }
            }

            for (int i = 0; i < profileWires.Count(); i++)
            {

                List<uint> linesOrdered = FixPlaneLinesDirection(gimData, (int)profileWires[i]);

                //we'll go through the lines one by one
                for (int j = 0; j < linesOrdered.Count; j++)
                {

                    GimElement currentLine = gimData.elements[(int)linesOrdered[j]];
                    if (currentLine.elementDoubles[1] <= threshold)
                    {
                        GimElement previousLine = new();
                        GimElement nextLine = new();
                        //since we have all the lines ordered in a list, if the current index is 0, the previous line should be the last instance on the list
                        if (j - 1 < 0)
                        {
                            int lastelement = linesOrdered.Count;
                            previousLine = gimData.elements[(int)linesOrdered[lastelement - 1]];
                        }
                        else
                        {
                            previousLine = gimData.elements[(int)linesOrdered[j - 1]];
                        }
                        //same principle here, if it's the last index, the next line should be the first line on the list
                        if (j + 1 >= linesOrdered.Count)
                        {
                            nextLine = gimData.elements[(int)linesOrdered[0]];
                        }
                        else
                        {
                            nextLine = gimData.elements[(int)linesOrdered[j + 1]];
                        }

                        //after dealing with that, we can now start the treatment 
                        if (previousLine.elementType == GimElementType.C_line && nextLine.elementType == GimElementType.C_line)
                        {
                            //we get all the points of the current line, the next one and the previous one, along with their directions
       
                            double[] currentLinePoints = CurveHelpers.getStartAndEndPointsOfCurve(currentLine, gimData, 0);
                            Vect currentLineStartPoint = currentLinePoints[0..3];
                            Vect currentLineEndPoint = currentLinePoints[3..6];
                            Vect currentLineDirection = (currentLineEndPoint - currentLineStartPoint).Direction;

                            double[] previousLinePoints = CurveHelpers.getStartAndEndPointsOfCurve(previousLine, gimData, 0);
                            Vect previousLineStartPoint = previousLinePoints[0..3];
                            Vect previousLineEndPoint = previousLinePoints[3..6];
                            Vect previousLineDirection = (previousLineEndPoint - previousLineStartPoint).Direction;

                            double[] nextLinePoints = CurveHelpers.getStartAndEndPointsOfCurve(nextLine, gimData, 0);
                            Vect nextLineStartPoint = nextLinePoints[0..3];
                            Vect nextLineEndPoint = nextLinePoints[3..6];
                            Vect nextLineDirection = (nextLineEndPoint - nextLineStartPoint).Direction;
                            double dotProduct1 = Math.Round(currentLineDirection * nextLineDirection,4);
                            double dotProduct2 = Math.Round(previousLineDirection * currentLineDirection,4);

                            //then we check if the next and previous line got the same direction, and if the current line is perpendicular to them 
                            if ((nextLineDirection == previousLineDirection || nextLineDirection == -previousLineDirection) && dotProduct2 == 0 && dotProduct1 == 0)
                            {
                                //if all of those are satisfied, we calculate the cross products
                                linesgone.Add(linesOrdered[j]);
                                Vect cross1 = previousLineDirection ^ currentLineDirection;
                                Vect cross2 = currentLineDirection ^ nextLineDirection;


                                if (cross1[0] < 0 || cross1[1] < 0 || cross1[2] < 0)
                                {
                                    //since it's a profile shape, the vector's direction changes considering if it's either the front or the rear, so we made 2 cases for each one of them
                                    //I think surely there's a better way to handle this, but since the lack of time this is what we came up with, if there's a better way it'll be implemented fo sure
                                    //the front and the rear are determined by the Y axis, so if it's negative, we are dealing with the front, that was the case with all the 3d shapes I checked, be it profile or not
                                    //so I think it's safe to assume this should work in most cases which is good for a possible temporary solution
                                    if (currentLineStartPoint[1] < 0 && currentLineEndPoint[1] < 0)
                                    {

                                        //changing the nextline here
                                        double t1 = (nextLineEndPoint - currentLineStartPoint).Norm;
                                        Vect direction = (nextLineEndPoint - currentLineStartPoint).Direction;
                                        direction.ApplyToLocal(nextLine.locals[0]);
                                        nextLine.locals[0].px = currentLine.locals[0].px;
                                        nextLine.locals[0].py = currentLine.locals[0].py;
                                        nextLine.locals[0].pz = currentLine.locals[0].pz;
                                        nextLine.elementDoubles[0] = 0;
                                        nextLine.elementDoubles[1] = t1;
                                    }
                                    else
                                    {
                                        //the previous line gets the treatment here
                                        double t1 = (previousLineStartPoint - currentLineEndPoint).Norm;
                                        Vect direction = (previousLineStartPoint - currentLineEndPoint).Direction;
                                        direction.ApplyToLocal(previousLine.locals[0]);
                                        previousLine.elementDoubles[0] = 0;
                                        previousLine.elementDoubles[1] = t1;
                                    }


                                }
                                //same logic applies here
                                else if (cross2[0] < 0 || cross2[1] < 0 || cross2[2] < 0)
                                {

                                    if (currentLineStartPoint[1] < 0 && currentLineEndPoint[1] < 0)
                                    {
                                        double t1 = (previousLineStartPoint - currentLineEndPoint).Norm;
                                        Vect direction = (previousLineStartPoint - currentLineEndPoint).Direction;
                                        direction.ApplyToLocal(previousLine.locals[0]);
                                        previousLine.elementDoubles[0] = 0;
                                        previousLine.elementDoubles[1] = t1;
                                    }
                                    else
                                    {
                                        double t1 = (nextLineEndPoint - currentLineStartPoint).Norm;
                                        Vect direction = (nextLineEndPoint - currentLineStartPoint).Direction;
                                        direction.ApplyToLocal(nextLine.locals[0]);
                                        nextLine.locals[0].px = currentLine.locals[0].px;
                                        nextLine.locals[0].py = currentLine.locals[0].py;
                                        nextLine.locals[0].pz = currentLine.locals[0].pz;
                                        nextLine.elementDoubles[0] = 0;
                                        nextLine.elementDoubles[1] = t1;
                                    }


                                }
                            }
                        }


                    }
                }
            }
            gimData.RemoveElementsAndTheirChildren(linesgone);

        }

        //the "snapping" of the side lines is being treated here
        public static void SideFacesSnap(GimData gimData, double threshold)
        {
            List<uint> linesGone = new();
            List<uint> planesGone = new();
            //we get the ordered vectors here
            Dictionary<uint, List<uint>> orderedSideFaceLines = GettingOrderedSideProfileLines(gimData);
            //then we reverse the directions instead of adjusting the treatment based on the Y value, again, I do not know if this is a better way
            //i'll try to think of something and get on it as soon as I find something better
            orderedSideFaceLines[1].Reverse();
            ReverseCurvesDirection(gimData, orderedSideFaceLines[1]);
            //same principle as the big face treatment
            foreach (KeyValuePair<uint, List<uint>> entry in orderedSideFaceLines)
            {
                List<uint> lineIndexes = entry.Value;
                for (int i = 0; i < lineIndexes.Count; i++)
                {

                    GimElement currentLine = gimData.elements[(int)lineIndexes[i]];
                    if (currentLine.elementDoubles[1] <= threshold)
                    {
                        GimElement previousLine = new();
                        GimElement nextLine = new();
                        if (i - 1 < 0)
                        {
                            int lastelement = lineIndexes.Count;
                            previousLine = gimData.elements[(int)lineIndexes[lastelement - 1]];
                        }
                        else
                        {
                            previousLine = gimData.elements[(int)lineIndexes[i - 1]];
                        }
                        if (i + 1 >= lineIndexes.Count)
                        {
                            nextLine = gimData.elements[(int)lineIndexes[0]];
                        }
                        else
                        {
                            nextLine = gimData.elements[(int)lineIndexes[i + 1]];
                        }
                        
                        double[] currentLinePoints = CurveHelpers.getStartAndEndPointsOfCurve(currentLine, gimData, 0);
                        Vect currentLineStartPoint = currentLinePoints[0..3];
                        Vect currentLineEndPoint = currentLinePoints[3..6];
                        Vect currentLineDirection = (currentLineEndPoint - currentLineStartPoint).Direction;

                        double[] previousLinePoints = CurveHelpers.getStartAndEndPointsOfCurve(previousLine, gimData, 0);
                        Vect previousLineStartPoint = previousLinePoints[0..3];
                        Vect previousLineEndPoint = previousLinePoints[3..6];
                        Vect previousLineDirection = (previousLineEndPoint - previousLineStartPoint).Direction;

                        double[] nextLinePoints = CurveHelpers.getStartAndEndPointsOfCurve(nextLine, gimData, 0);
                        Vect nextLineStartPoint = nextLinePoints[0..3];
                        Vect nextLineEndPoint = nextLinePoints[3..6];
                        Vect nextLineDirection = (nextLineEndPoint - nextLineStartPoint).Direction;
                        double dotProduct1 = Math.Round(currentLineDirection * nextLineDirection, 4);
                        double dotProduct2 = Math.Round(previousLineDirection * currentLineDirection, 4);
                        //same conditions as in the big face function
                        if ((nextLineDirection == previousLineDirection || nextLineDirection == -previousLineDirection) && dotProduct1 == 0 && dotProduct2 == 0)
                        {
                            linesGone.Add(lineIndexes[i]);
                            Vect cross1 = previousLineDirection ^ currentLineDirection;
                            Vect cross2 = currentLineDirection ^ nextLineDirection;


                            if (cross1[0] < 0 || cross1[1] < 0 || cross1[2] < 0)
                            {


                                double t1 = (previousLineStartPoint - currentLineEndPoint).Norm;
                                Vect direction = (previousLineStartPoint - currentLineEndPoint).Direction;
                                uint wireIndex = nextLine.Locals[0].Parent;
                                GimElement wire = gimData.elements[(int)wireIndex];
                                //since we are not dealing with just lines in this scenario, but planes, there's another line in the plane that needs to be treated and it's done here
                                for (int j = 0; j < wire.elements.Count; j++)
                                {
                                    uint lineIndex = wire.elements[j];
                                    if (lineIndex != lineIndexes[i + 1])
                                    {
                                        GimElement line = gimData.elements[(int)wire.elements[j]];
                                        double[] linePoints = CurveHelpers.getStartAndEndPointsOfCurve(line, gimData, 0);
                                        Vect lineStartPoint = linePoints[0..3];
                                        if (previousLineStartPoint == lineStartPoint)
                                        {
                                            line.locals[0].px = currentLine.locals[0].px;
                                            line.locals[0].py = currentLine.locals[0].py;
                                            line.locals[0].pz = currentLine.locals[0].pz;
                                        }

                                    }
                                }
                                direction.ApplyToLocal(previousLine.locals[0]);
                                previousLine.elementDoubles[0] = 0;
                                previousLine.elementDoubles[1] = t1;



                            }
                            else if (cross2[0] < 0 || cross2[1] < 0 || cross2[2] < 0)
                            {


                                double t1 = (nextLineEndPoint - currentLineStartPoint).Norm;
                                Vect direction = (nextLineEndPoint - currentLineStartPoint).Direction;
                                uint wireIndex = nextLine.Locals[0].Parent;
                                GimElement wire = gimData.elements[(int)wireIndex];
                                //since we are not dealing with just lines in this scenario, but planes, there's another line in the plane that needs to be treated and it's done here
                                for (int j = 0; j < wire.elements.Count; j++)
                                {
                                    uint lineIndex = wire.elements[j];
                                    if (lineIndex != lineIndexes[i + 1])
                                    {
                                        GimElement line = gimData.elements[(int)wire.elements[j]];
                                        double[] linePoints = CurveHelpers.getStartAndEndPointsOfCurve(line, gimData, 0);
                                        Vect lineStartPoint = linePoints[0..3];
                                        if (nextLineStartPoint == lineStartPoint)
                                        {
                                            line.locals[0].px = currentLine.locals[0].px;
                                            line.locals[0].py = currentLine.locals[0].py;
                                            line.locals[0].pz = currentLine.locals[0].pz;
                                        }

                                    }
                                }
                                    direction.ApplyToLocal(nextLine.locals[0]);
                                    nextLine.locals[0].px = currentLine.locals[0].px;
                                    nextLine.locals[0].py = currentLine.locals[0].py;
                                    nextLine.locals[0].pz = currentLine.locals[0].pz;
                                    nextLine.elementDoubles[0] = 0;
                                    nextLine.elementDoubles[1] = t1;
                                
                            }
                        }


                    }

                }

            }
            for (int i = 0; i < linesGone.Count; i++)
            {
                GimElement element = gimData.elements[(int)linesGone[i]];
                planesGone.Add(GetParentOfParent(gimData, element));
            }
            List<uint> test = planesGone.Distinct().ToList();
            gimData.RemoveElementsAndTheirChildren(test);

        }


        //this function takes care of reversing the direction of lines and planes in a given List
        public static void ReverseCurvesDirection(GimData gimData, List<uint> list)
        {

            for (int i = 0; i < list.Count; i++)
            {
                GimElement element = gimData.elements[(int)list[i]];
                if (element.elementType == GimElementType.C_line)
                {
                    double[] elementPoints = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData, 0);
                    Vect startPoint = elementPoints[0..3];
                    Vect endPoint = elementPoints[3..6];
                    Vect newDirection = (startPoint - endPoint).Direction;
                    newDirection.ApplyToLocal(element.locals[0]);
                    element.locals[0].px = endPoint[0];
                    element.locals[0].py = endPoint[1];
                    element.locals[0].pz = endPoint[2];
                }
                else if (element.elementType == GimElementType.C_circle)
                {
                    double[] elementPoints = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData, 0);
                    Vect startPoint = elementPoints[0..3];
                    Vect endPoint = elementPoints[3..6];
                    double radius = element.elementDoubles[0];
                    double t2 = element.elementDoubles[2];
                    Vect direction1 = new(element.Locals[0].Xx, element.Locals[0].Xy, element.Locals[0].Xz);
                    Vect direction2 = new(element.Locals[0].Yx, element.Locals[0].Yy, element.Locals[0].Yz);

                    Vect origin = new(element.Locals[0].Px, element.Locals[0].Py, element.Locals[0].Pz);
                    Vect newDirection1 = (endPoint - origin).Direction;
                    double newt1 = t2 - (Math.PI / 2);
                    Vect newPoint = origin + radius * Math.Cos(newt1) * direction1 + radius * Math.Sin(newt1) * direction2;
                    Vect newDirection2 = (newPoint - origin).Direction;
                    Matrix circleDirection = new(newDirection1, newDirection2, newDirection1 ^ newDirection2);
                    circleDirection.ApplyToLocal(element.Locals[0]);
                }

            }


        }


        //this function "fixes" the direction of the lines on the plane of the big face in a way that the endpoint of a line 
        //is the start point of the line that comes after it
        public static List<uint> FixPlaneLinesDirection(GimData gimData, int wireidx)
        {
            List<uint> result = new List<uint>();
            GimElement wire = gimData.Elements[wireidx];
            uint curve1Index = 0;
            //getting the index of the first line
            for (int k = 0; k < wire.elements.Count; k++)
            {
                GimElement wireElement = gimData.elements[(int)wire.elements[k]];
                if (wireElement.elementType == GimElementType.C_line)
                {
                    curve1Index = wire.elements[k];
                    result.Add(curve1Index);
                    break;
                }
            }


            
            while (result.Count < wire.Elements.Count)
            {
                
                //at first we get the end point of the first line we got
                GimElement currentElement = gimData.Elements[(int)curve1Index];


                double[] curve1Points = CurveHelpers.getStartAndEndPointsOfCurve(currentElement, gimData);
                Vect curve1EndPoint = curve1Points[3..6];


                foreach (var element2index in wire.Elements)
                {
                    //then we start comparing them one by one
                    GimElement otherElement = gimData.Elements[(int)element2index];
                    
                    if (otherElement.elementType == GimElementType.C_line)
                    {
                        //if it's a line, we get its point and check if the end point of the curve we are checking  is the same as the line we are in right now
                        double[] line2Points = CurveHelpers.getStartAndEndPointsOfCurve(otherElement, gimData);
                        Vect lineEndPoint = line2Points[3..6];
                        Vect lineStartPoint = line2Points[0..3];
                        //if it is, then we simply add the element to the list
                        if (lineStartPoint == curve1EndPoint && !result.Contains(element2index))
                        {
                            result.Add(element2index);
                            curve1Index = element2index;
                            break;
                        }
                        //if it's not, we reverse the direction of that line
                        if (lineEndPoint == curve1EndPoint && !result.Contains(element2index))
                        {
                            result.Add(element2index);
                            curve1Index = element2index;
                            Vect lineDirectionVector = (lineStartPoint - curve1EndPoint).Direction;

                            lineDirectionVector.ApplyToLocal(otherElement.locals[0]);
                            otherElement.Locals[0].px = curve1EndPoint[0];
                            otherElement.locals[0].py = curve1EndPoint[1];
                            otherElement.locals[0].pz = curve1EndPoint[2];
                            break;
                        }
                    }
                    //same principle if it's a circle
                    else if (otherElement.elementType == GimElementType.C_circle)
                    {
                        double[] circlePoints = CurveHelpers.getStartAndEndPointsOfCurve(otherElement, gimData);

                        Vect circleStartPoint = circlePoints[0..3];
                        Vect circleEndPoint = circlePoints[3..6];
                        //add the circle directly to the list if end point is the same as the circle's start point
                        if (circleStartPoint == curve1EndPoint && !result.Contains(element2index))
                        {
                            result.Add(element2index);
                            curve1Index = element2index;
                            break;
                        }
                        //if not we simply reverse the direction of the circle
                        if (circleEndPoint == curve1EndPoint && !result.Contains(element2index))
                        {
                            result.Add(element2index);
                            curve1Index = element2index;
                            double radius = otherElement.elementDoubles[0];
                            double t2 = otherElement.elementDoubles[2];
                            Vect direction1 = new(otherElement.Locals[0].Xx, otherElement.Locals[0].Xy, otherElement.Locals[0].Xz);
                            Vect direction2 = new(otherElement.Locals[0].Yx, otherElement.Locals[0].Yy, otherElement.Locals[0].Yz);

                            Vect origin = new(otherElement.Locals[0].Px, otherElement.Locals[0].Py, otherElement.Locals[0].Pz);
                            Vect newDirection1 = (circleEndPoint - origin).Direction;
                            double newt1 = t2 - (Math.PI / 2);
                            Vect newPoint = origin + radius * Math.Cos(newt1) * direction1 + radius * Math.Sin(newt1) * direction2;
                            Vect newDirection2 = (newPoint - origin).Direction;
                            Matrix circleDirection = new(newDirection1, newDirection2, newDirection1 ^ newDirection2);
                            circleDirection.ApplyToLocal(otherElement.Locals[0]);

                        }
                    }

                }


            }

            return result;
        }



        //doodoo
        //there was a misunderstanding and this function was created, we might need it eventually with some tweaks so i'm keeping it here.

        public static void DirectionalRemoval(GimData gimData, double threshold)
        {
            List<uint> planesToBeRemoved = new();
            List<int> test = new();
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement gimElement = gimData.elements[i];
                if (gimElement.elementType == GimElementType.wire)
                {
                    test.Add(gimElement.elements.Count);
                }
            }
            // this assumes we are working on a "profile" type of shape, another way to determine the big face more precisely was thought of and will be implemented shortly
            int a = test.Max();
            double b = gimData.Elements.Count;


            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement gimElement = gimData.Elements[i];
                if (gimElement.elementType == GimElementType.C_line && gimElement.elementDoubles[1] < threshold)
                {
                    double Dy = Math.Round(gimElement.Locals[0].Zy, 6);
                    double Dx = Math.Round(gimElement.Locals[0].Zx, 6);
                    double Dz = Math.Round(gimElement.Locals[0].Zz, 6);
                    if (Dy == 1 || Dy == -1)
                    {
                        double[] startEndPoints1 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement, gimData, 0);
                        double startPointY1 = Math.Round(startEndPoints1[1], 6);
                        double endPointY1 = Math.Round(startEndPoints1[4], 6);
                        double Py = gimElement.Locals[0].Py;
                        uint wireIndex = gimElement.Locals[0].Parent;
                        GimElement wire = gimData.elements[(int)wireIndex];
                        if (wire.Elements.Count != a)
                        {
                            planesToBeRemoved.Add(GetParentOfParent(gimData, gimElement));
                        }
                        for (int j = 0; j < gimData.Elements.Count; j++)
                        {
                            GimElement gimElement1 = gimData.Elements[j];
                            if (gimElement1.elementType == GimElementType.C_line)
                            {
                                uint wireIndex2 = gimElement1.Locals[0].Parent;
                                if (wireIndex != wireIndex2)
                                {

                                    double[] startEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                    double startPointY2 = startEndPoints2[1];
                                    double endPointY2 = startEndPoints2[4];
                                    double Dy1 = gimElement1.Locals[0].Zy;
                                    double Py1 = gimElement1.Locals[0].Py;
                                    if ((startPointY1 >= startPointY2 && endPointY1 >= endPointY2 && startPointY1 >= endPointY2 && endPointY1 >= startPointY2))
                                    {
                                        if (Py1 < 0)
                                        {
                                            gimElement1.locals[0].py = Py1 - (-gimElement.elementDoubles[1]);
                                        }
                                        else
                                        {
                                            gimElement1.locals[0].py = Py1 + gimElement.elementDoubles[1];
                                        }
                                    }
                                    else if ((startPointY1 > startPointY2 && startPointY1 > endPointY2) || (endPointY1 > endPointY2 && endPointY1 > startPointY2))
                                    {
                                        //break;
                                    }
                                    else if (((startPointY1 < startPointY2 && startPointY1 > endPointY2) || (startPointY1 < endPointY2 && startPointY1 > startPointY2)) && ((endPointY1 < startPointY2 && endPointY1 > endPointY2) || (endPointY1 < endPointY2 && endPointY1 > startPointY2)))
                                    {
                                        gimElement1.elementDoubles[1] = gimElement1.elementDoubles[1] - gimElement.elementDoubles[1];
                                    }
                                    else if ((startPointY1 == startPointY2 && endPointY1 == endPointY2) || (startPointY1 == endPointY2 && endPointY1 == startPointY2))
                                    {
                                        gimElement1.elementDoubles[1] = gimElement1.elementDoubles[1] - gimElement.elementDoubles[1];
                                    }
                                }

                            }
                            else if (gimElement1.elementType == GimElementType.C_circle)
                            {
                                //not sure if the circle stuff is 100% accurate on more complex shapes, but on simpler ones it properly does the job
                                uint wireIndex2 = gimElement1.Locals[0].Parent;
                                if (wireIndex != wireIndex2)
                                {
                                    double[] startEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                    double startPointY2 = Math.Round(startEndPoints2[1], 6);
                                    double endPointY2 = Math.Round(startEndPoints2[4], 6);
                                    double xx = Math.Round(gimElement1.Locals[0].Xx, 6);
                                    double xy = Math.Round(gimElement1.Locals[0].Xy, 6);
                                    double xz = Math.Round(gimElement1.Locals[0].Xz, 6);
                                    double yx = Math.Round(gimElement1.Locals[0].Yx, 6);
                                    double yy = Math.Round(gimElement1.Locals[0].Yy, 6);
                                    double yz = Math.Round(gimElement1.Locals[0].Yz, 6);
                                    double Py1 = gimElement1.Locals[0].Py;
                                    if ((startPointY1 >= startPointY2 && endPointY1 >= endPointY2 && startPointY1 >= endPointY2 && endPointY1 >= startPointY2))
                                    {
                                        if (Py1 < 0)
                                        {
                                            gimElement1.locals[0].py = Py1 - (-gimElement.elementDoubles[1]);
                                        }
                                        else
                                        {
                                            gimElement1.locals[0].py = Py1 + gimElement.elementDoubles[1];
                                        }
                                    }
                                    else if ((startPointY1 > startPointY2 && startPointY1 > endPointY2) || (endPointY1 > endPointY2 && endPointY1 > startPointY2))
                                    {
                                        //break;
                                    }
                                    else if (((startPointY1 < startPointY2 && startPointY1 > endPointY2) || (startPointY1 < endPointY2 && startPointY1 > startPointY2)) && ((endPointY1 < startPointY2 && endPointY1 > endPointY2) || (endPointY1 < endPointY2 && endPointY1 > startPointY2)))
                                    {

                                        //defo not sure about this part, still figuring it out 

                                        //double arcLength = (gimElement1.elementDoubles[2] - gimElement1.elementDoubles[1]) * gimElement1.elementDoubles[0];
                                        //arcLength = arcLength - gimElement.elementDoubles[1];
                                        //gimElement1.elementDoubles[2] = (arcLength / gimElement1.elementDoubles[0]) + gimElement1.elementDoubles[1];

                                        //doesn't work, needs more adjustment, will probably go back to the old idea

                                        gimElement1.elementDoubles[0] -= gimElement.elementDoubles[1];
                                        switch (xx)
                                        {
                                            case 1:
                                                gimElement1.locals[0].px += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].px -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (xy)
                                        {
                                            case 1:
                                                gimElement1.locals[0].py += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].py -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (xz)
                                        {
                                            case 1:
                                                gimElement1.locals[0].pz += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].pz -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (yx)
                                        {
                                            case 1:
                                                gimElement1.locals[0].px += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].px -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (yy)
                                        {
                                            case 1:
                                                gimElement1.locals[0].py += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].py -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (yz)
                                        {
                                            case 1:
                                                gimElement1.locals[0].pz += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].pz -= gimElement.elementDoubles[1];
                                                break;
                                        }


                                    }
                                    else if ((startPointY1 == startPointY2 && endPointY1 == endPointY2) || (startPointY1 == endPointY2 && endPointY1 == startPointY2))
                                    {
                                        gimElement1.elementDoubles[0] = 0;
                                    }
                                }
                            }


                        }
                    }
                    else if (Dx == 1 || Dx == -1)
                    {
                        double Px = gimElement.Locals[0].Px;
                        double[] startEndPoints1 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement, gimData, 0);
                        double startPointX1 = Math.Round(startEndPoints1[0], 6);
                        double endPointX1 = startEndPoints1[3];
                        uint wireIndex = gimElement.Locals[0].Parent;
                        GimElement wire = gimData.elements[(int)wireIndex];
                        if (wire.Elements.Count != a)
                        {
                            planesToBeRemoved.Add(GetParentOfParent(gimData, gimElement));
                        }
                        for (int j = 0; j < gimData.Elements.Count; j++)
                        {
                            GimElement gimElement1 = gimData.Elements[j];
                            if (gimElement1.elementType == GimElementType.C_line)
                            {
                                uint wireIndex2 = gimElement1.Locals[0].Parent;
                                if (wireIndex != wireIndex2)
                                {
                                    double[] startEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                    double startPointX2 = startEndPoints2[0];
                                    double endPointX2 = startEndPoints2[3];
                                    double Dx1 = gimElement1.Locals[0].Zx;
                                    double Px1 = gimElement1.Locals[0].Px;
                                    if ((startPointX1 >= startPointX2 && endPointX1 >= endPointX2 && startPointX1 >= endPointX2 && endPointX1 >= startPointX2))
                                    {

                                        if (Px1 < 0)
                                        {
                                            gimElement1.locals[0].px = Px1 - (-gimElement.elementDoubles[1]);
                                        }
                                        else
                                        {
                                            gimElement1.locals[0].px = Px1 + gimElement.elementDoubles[1];
                                        }
                                    }
                                    else if ((startPointX1 > startPointX2 && startPointX1 > endPointX2) || (endPointX1 > endPointX2 && endPointX1 > startPointX2))
                                    {
                                        //break;
                                    }
                                    else if (((startPointX1 < startPointX2 && startPointX1 > endPointX2) || (startPointX1 < endPointX2 && startPointX1 > startPointX2)) && ((endPointX1 < startPointX2 && endPointX1 > endPointX2) || (endPointX1 < endPointX2 && endPointX1 > startPointX2)))
                                    {
                                        gimElement1.elementDoubles[1] = gimElement1.elementDoubles[1] - gimElement.elementDoubles[1];

                                    }
                                    else if ((startPointX1 == startPointX2 && endPointX1 == endPointX2) || (startPointX1 == endPointX2 && endPointX1 == startPointX2))
                                    {
                                        gimElement1.elementDoubles[1] = gimElement1.elementDoubles[1] - gimElement.elementDoubles[1];

                                    }
                                }


                            }
                            else if (gimElement1.elementType == GimElementType.C_circle)
                            {
                                //not sure if the circle stuff is 100% accurate on more complex shapes, but on simpler ones it's fine I assume
                                uint wireIndex2 = gimElement1.Locals[0].Parent;
                                if (wireIndex != wireIndex2)
                                {
                                    double[] startEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                    double startPointX2 = Math.Round(startEndPoints2[0], 6);
                                    double endPointX2 = Math.Round(startEndPoints2[3], 6);
                                    double xx = Math.Round(gimElement1.Locals[0].Xx, 6);
                                    double xy = Math.Round(gimElement1.Locals[0].Xy, 6);
                                    double xz = Math.Round(gimElement1.Locals[0].Xz, 6);
                                    double yx = Math.Round(gimElement1.Locals[0].Yx, 6);
                                    double yy = Math.Round(gimElement1.Locals[0].Yy, 6);
                                    double yz = Math.Round(gimElement1.Locals[0].Yz, 6);
                                    double Px1 = gimElement1.Locals[0].Px;
                                    if ((startPointX1 >= startPointX2 && endPointX1 >= endPointX2 && startPointX1 >= endPointX2 && endPointX1 >= startPointX2))
                                    {

                                        if (Px1 < 0)
                                        {
                                            gimElement1.locals[0].px = Px1 - (-gimElement.elementDoubles[1]);
                                        }
                                        else
                                        {
                                            gimElement1.locals[0].px = Px1 + gimElement.elementDoubles[1];
                                        }
                                    }
                                    else if ((startPointX1 > startPointX2 && startPointX1 > endPointX2) || (endPointX1 > endPointX2 && endPointX1 > startPointX2))
                                    {
                                        //break;
                                    }
                                    else if (((startPointX1 < startPointX2 && startPointX1 > endPointX2) || (startPointX1 < endPointX2 && startPointX1 > startPointX2)) && ((endPointX1 < startPointX2 && endPointX1 > endPointX2) || (endPointX1 < endPointX2 && endPointX1 > startPointX2)))
                                    {
                                        //defo not sure about this part, still figuring it out 

                                        //double arcLength = (gimElement1.elementDoubles[2] - gimElement1.elementDoubles[1]) * gimElement1.elementDoubles[0];
                                        //arcLength = arcLength - gimElement.elementDoubles[1];
                                        //gimElement1.elementDoubles[2] = (arcLength / gimElement1.elementDoubles[0]) + gimElement1.elementDoubles[1];

                                        //doesn't work, needs more adjustment, will probably go back to the old idea
                                        gimElement1.elementDoubles[0] -= gimElement.elementDoubles[1];
                                        switch (xx)
                                        {
                                            case 1:
                                                gimElement1.locals[0].px += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].px -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (xy)
                                        {
                                            case 1:
                                                gimElement1.locals[0].py += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].py -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (xz)
                                        {
                                            case 1:
                                                gimElement1.locals[0].pz += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].pz -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (yx)
                                        {
                                            case 1:
                                                gimElement1.locals[0].px += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].px -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (yy)
                                        {
                                            case 1:
                                                gimElement1.locals[0].py += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].py -= gimElement.elementDoubles[1];
                                                break;
                                        }

                                        switch (yz)
                                        {
                                            case 1:
                                                gimElement1.locals[0].pz += gimElement.elementDoubles[1];
                                                break;
                                            case -1:
                                                gimElement1.locals[0].pz -= gimElement.elementDoubles[1];
                                                break;
                                        }
                                    }
                                    else if ((startPointX1 == startPointX2 && endPointX1 == endPointX2) || (startPointX1 == endPointX2 && endPointX1 == startPointX2))
                                    {


                                        gimElement1.elementDoubles[0] = 0;

                                    }
                                }
                            }


                        }
                    }
                    else if (Dz == 1 || Dz == -1)
                    {
                        double Pz = gimElement.Locals[0].Pz;
                        double[] startEndPoints1 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement, gimData, 0);
                        double startPointZ1 = Math.Round(startEndPoints1[2], 6);
                        double endPointZ1 = Math.Round(startEndPoints1[5], 6);
                        uint wireIndex = gimElement.Locals[0].Parent;
                        GimElement wire = gimData.elements[(int)wireIndex];
                        if (wire.Elements.Count != a)
                        {
                            planesToBeRemoved.Add(GetParentOfParent(gimData, gimElement));
                        }
                        for (int j = 0; j < gimData.Elements.Count; j++)
                        {


                            GimElement gimElement1 = gimData.Elements[j];

                            if (gimElement1.elementType == GimElementType.C_line)
                            {
                                uint wireIndex2 = gimElement1.Locals[0].Parent;
                                if (wireIndex2 != wireIndex)
                                {


                                    double[] startEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                    double startPointZ2 = startEndPoints2[2];
                                    double endPointZ2 = startEndPoints2[5];
                                    double Dz1 = gimElement1.Locals[0].Zz;
                                    double Pz1 = gimElement1.Locals[0].Pz;
                                    if ((startPointZ1 >= startPointZ2 && endPointZ1 >= endPointZ2 && startPointZ1 >= endPointZ2 && endPointZ1 >= startPointZ2))
                                    {

                                        if (Pz1 < 0)
                                        {
                                            gimElement1.locals[0].pz = Pz1 - (-gimElement.elementDoubles[1]);
                                        }
                                        else
                                        {
                                            gimElement1.locals[0].pz = Pz1 + gimElement.elementDoubles[1];
                                        }
                                    }
                                    else if ((startPointZ1 > startPointZ2 && startPointZ1 > endPointZ2) || (endPointZ1 > endPointZ2 && endPointZ1 > startPointZ2))
                                    {
                                        // break;
                                    }
                                    else if (((startPointZ1 < startPointZ2 && startPointZ1 > endPointZ2) || (startPointZ1 < endPointZ2 && startPointZ1 > startPointZ2)) && ((endPointZ1 < startPointZ2 && endPointZ1 > endPointZ2) || (endPointZ1 < endPointZ2 && endPointZ1 > startPointZ2)))
                                    {


                                        gimElement1.elementDoubles[1] = gimElement1.elementDoubles[1] - gimElement.elementDoubles[1];

                                    }
                                    else if ((startPointZ1 == startPointZ2 && endPointZ1 == endPointZ2) || (startPointZ1 == endPointZ2 && endPointZ1 == startPointZ2))
                                    {


                                        gimElement1.elementDoubles[1] = gimElement1.elementDoubles[1] - gimElement.elementDoubles[1];

                                    }
                                }






                            }
                            else if (gimElement1.elementType == GimElementType.C_circle)
                            {
                                //not sure if the circle stuff is 100% accurate on more complex shapes, but on simpler ones it's fine I assume
                                uint wireIndex2 = gimElement1.Locals[0].Parent;
                                if (wireIndex2 != wireIndex)
                                {


                                    double[] startEndPoints2 = CurveHelpers.getStartAndEndPointsOfCurve(gimElement1, gimData, 0);
                                    double startPointZ2 = Math.Round(startEndPoints2[2], 6);
                                    double endPointZ2 = Math.Round(startEndPoints2[5], 6);
                                    double xx = Math.Round(gimElement1.Locals[0].Xx, 6);
                                    double xy = Math.Round(gimElement1.Locals[0].Xy, 6);
                                    double xz = Math.Round(gimElement1.Locals[0].Xz, 6);
                                    double yx = Math.Round(gimElement1.Locals[0].Yx, 6);
                                    double yy = Math.Round(gimElement1.Locals[0].Yy, 6);
                                    double yz = Math.Round(gimElement1.Locals[0].Yz, 6);
                                    double Pz1 = gimElement1.Locals[0].Pz;
                                    if ((startPointZ1 >= startPointZ2 && endPointZ1 >= endPointZ2 && startPointZ1 >= endPointZ2 && endPointZ1 >= startPointZ2))
                                    {
                                        //if ((startPointZ1 == startPointZ2 && endPointZ1 == endPointZ2) || (startPointZ1 == endPointZ2 && endPointZ1 == startPointZ2))
                                        //{
                                        //    gimElement1.elementDoubles[1] = gimElement1.elementDoubles[1] - gimElement.elementDoubles[1];
                                        //}
                                        if (Pz1 < 0)
                                        {
                                            gimElement1.locals[0].pz = Pz1 - (-gimElement.elementDoubles[1]);
                                        }
                                        else
                                        {
                                            gimElement1.locals[0].pz = Pz1 + gimElement.elementDoubles[1];
                                        }
                                    }
                                    else if ((startPointZ1 > startPointZ2 && startPointZ1 > endPointZ2) || (endPointZ1 > endPointZ2 && endPointZ1 > startPointZ2))
                                    {
                                        // break;
                                    }
                                    else if (((startPointZ1 < startPointZ2 && startPointZ1 > endPointZ2) || (startPointZ1 < endPointZ2 && startPointZ1 > startPointZ2)) && ((endPointZ1 < startPointZ2 && endPointZ1 > endPointZ2) || (endPointZ1 < endPointZ2 && endPointZ1 > startPointZ2)))
                                    {
                                        //defo not sure about this part, still figuring it out 


                                        double arcLength = (gimElement1.elementDoubles[2] - gimElement1.elementDoubles[1]) * gimElement1.elementDoubles[0];
                                        arcLength = arcLength - gimElement.elementDoubles[1];
                                        gimElement1.elementDoubles[2] = (arcLength / gimElement1.elementDoubles[0]) + gimElement1.elementDoubles[1];



                                        //gimElement1.elementDoubles[0] -= gimElement.elementDoubles[1];
                                        //some random idea hopefully it works

                                        //nvm doesn't work, needs more adjustment, will probably go back to the old idea
                                        //switch (xx)
                                        //{
                                        //    case 1:
                                        //        gimElement1.locals[0].px += gimElement.elementDoubles[1];
                                        //        break;
                                        //    case -1:
                                        //        gimElement1.locals[0].px -= gimElement.elementDoubles[1];
                                        //        break;
                                        //}

                                        //switch (xy)
                                        //{
                                        //    case 1:
                                        //        gimElement1.locals[0].py += gimElement.elementDoubles[1];
                                        //        break;
                                        //    case -1:
                                        //        gimElement1.locals[0].py -= gimElement.elementDoubles[1];
                                        //        break;
                                        //}

                                        //switch (xz)
                                        //{
                                        //    case 1:
                                        //        gimElement1.locals[0].pz += gimElement.elementDoubles[1];
                                        //        break;
                                        //    case -1:
                                        //        gimElement1.locals[0].pz -= gimElement.elementDoubles[1];
                                        //        break;
                                        //}

                                        //switch (yx)
                                        //{
                                        //    case 1:
                                        //        gimElement1.locals[0].px += gimElement.elementDoubles[1];
                                        //        break;
                                        //    case -1:
                                        //        gimElement1.locals[0].px -= gimElement.elementDoubles[1];
                                        //        break;
                                        //}

                                        //switch (yy)
                                        //{
                                        //    case 1:
                                        //        gimElement1.locals[0].py += gimElement.elementDoubles[1];
                                        //        break;
                                        //    case -1:
                                        //        gimElement1.locals[0].py -= gimElement.elementDoubles[1];
                                        //        break;
                                        //}

                                        //switch (yz)
                                        //{
                                        //    case 1:
                                        //        gimElement1.locals[0].pz += gimElement.elementDoubles[1];
                                        //        break;
                                        //    case -1:
                                        //        gimElement1.locals[0].pz -= gimElement.elementDoubles[1];
                                        //        break;
                                        //}



                                    }
                                    else if ((startPointZ1 == startPointZ2 && endPointZ1 == endPointZ2) || (startPointZ1 == endPointZ2 && endPointZ1 == startPointZ2))
                                    {

                                        gimElement1.elementDoubles[0] = 0;

                                    }
                                }
                            }


                        }
                    }
                }
                //if (i == 42)
                //{
                //    List<uint> testplanes = planesToBeRemoved.Distinct().ToList();
                //    gimData.RemoveElementsAndTheirChildren(testplanes);
                //    return;
                //}
            }

            List<uint> testplanes = planesToBeRemoved.Distinct().ToList();
            gimData.RemoveElementsAndTheirChildren(testplanes);
        }





    }
}