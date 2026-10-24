using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GimUtilities;

[assembly: InternalsVisibleTo("GIMtransfer")]

namespace GIMcompress
{
	public class Axis
    {
        public Vect basePoint;
        public Vect direction;

        //public sbyte type = 0; // 0 -> unknown, -1 -> internal, 1 -> external circle: used to flag 
        public double circleRadius;

        public Axis(Vect basePoint, Vect direction)
        {
            this.basePoint = basePoint;
            this.direction = direction;
            //this.type = type;
            //this.circleRadius = circleRadius;
        }

        public bool CompareAxis(Axis otherAxis)
        {
            Vect offset = otherAxis.basePoint - basePoint;
            return offset.IsCollinearTo(direction) && direction.IsCollinearTo(otherAxis.direction);
        }

        public static bool operator==(Axis axisA, Axis axisB)
        {
            if (axisA is null || axisB is null) return false;
            return axisA.CompareAxis(axisB);
        }
        public static bool operator !=(Axis axisA, Axis axisB)
        {
            if (axisA is null || axisB is null) return true;
            return !axisA.CompareAxis(axisB);
        }
    }

    public static class JointDetector
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectElementId"> the ID of the object in the GimData</param>
        /// <param name="localId"> the ID of the local we're going to use </param>
        /// <param name="objectIndex"> the index of the object as it will apear in the JSON children array</param>
        /// <param name="gimData">the GimData to use</param>
        /// <returns></returns>
        internal static List<GimJoint> GetObjectJoints(GimData gimData, uint objectElementId, int localId, int objectIndex)
        {
            List<GimJoint> result = new();

            GimElement objectElement = gimData.Elements[(int)objectElementId];
            GimLocal objectLocal = objectElement.Locals[localId];

            Matrix transformationMatrix = new Matrix(objectLocal);
            Vect transformationOffset = new Vect(objectLocal.Px, objectLocal.Py, objectLocal.Pz);

            var totalJoints = new List<GimJoint>();
            foreach(uint childId in objectElement.Elements) // we extract the joints in a recusive tree search manner (depth first)
            {
                GimElement child = gimData.Elements[(int)childId];
                List<GimJoint> childJoints = GetObjectJointsRecursive(gimData, childId, objectElementId, objectIndex);

                if(childJoints != null)
                {

                    totalJoints.AddRange(childJoints);
                }

            }

            foreach (GimJoint joint in totalJoints) // we apply the correct transformation to the joints
            {
                Vect jointStart = joint.Origin[0];
                Vect jointEnd = joint.Origin[1];

                jointStart = transformationMatrix * jointStart + transformationOffset; // we transform (rotate + translate) both points
                jointEnd = transformationMatrix * jointEnd + transformationOffset;

                joint.Origin[0] = jointStart;
                joint.Origin[1] = jointEnd;
            }

            List<Tuple<GimJoint, List<GimJoint>>> jointTuples = new();

            foreach (GimJoint joint in totalJoints) // we are going to group the joints in type by their axis
                // an axis is defined by a point and a vector, but for the sake of simplicity were using the joint itself as the tuple's ID 
            {
                bool found = false;
                foreach (var jointTuple in jointTuples) // we check all the existing groups

                {
                    Vect tupleDirection = (jointTuple.Item1.Origin[1] - jointTuple.Item1.Origin[0]).Direction;
                    Vect jointDirection = (joint.Origin[1] - joint.Origin[0]).Direction;
                    Vect offset = joint.Origin[0] - jointTuple.Item1.Origin[0];

                    // if the joint is lined up with the joint from this group (having same direction) we add it to it
                    if (jointDirection.IsCollinearTo(tupleDirection) && offset.IsCollinearTo(jointDirection))
                    {
                        found = true;
                        jointTuple.Item2.Add(joint);
                        break;
                    }
                }
                if (!found) // we didn't find the directional group, we just create one
                {
                    var jointTuple = new Tuple<GimJoint, List<GimJoint>>(joint, new List<GimJoint>() { joint });
                    jointTuples.Add(jointTuple);
                }
            }

            // in this next section, we group the joint that have the same axis and overlap
            // this is when there are multiple cylinders but only one "joint hole" or "joint pole"

            foreach (var jointTuple in jointTuples) // we check groups within themselves
            {
                List<GimJoint> jointGroup = jointTuple.Item2;

                for (int i = 0; i < jointGroup.Count - 1; i++)
                {
                    GimJoint currentJoint = jointGroup[i];

                    for (int j = i + 1; j < jointGroup.Count; j++)
                    {
                        GimJoint otherJoint = jointGroup[j];

                        if (currentJoint.OverlapsWith(otherJoint)) // they have the same direction and overlap
                        {
                            Vect basepoint = currentJoint.Origin[0];

                            Vect currentStart = currentJoint.Origin[0];
                            Vect currentEnd = currentJoint.Origin[1];

                            Vect otherStart = otherJoint.Origin[0];
                            Vect otherEnd = otherJoint.Origin[1];



                            Vect direction = (currentEnd - currentStart).Direction;

                            // here we're going to combine the two joints into one continous one, that goes from minparam to maxparam

                            double param1 = (currentStart - basepoint) * direction;
                            double param2 = (currentEnd - basepoint) * direction;
                            double param3 = (otherStart - basepoint) * direction;
                            double param4 = (otherEnd - basepoint) * direction;

                            double minParam = 300000, maxParam = 0;

                            if (param1 < minParam) minParam = param1;
                            if (param2 < minParam) minParam = param2;
                            if (param3 < minParam) minParam = param3;
                            if (param4 < minParam) minParam = param4;

                            if (param1 > maxParam) maxParam = param1;
                            if (param2 > maxParam) maxParam = param2;
                            if (param3 > maxParam) maxParam = param3;
                            if (param4 > maxParam) maxParam = param4;

                            Vect newStart = basepoint + minParam * direction;
                            Vect newEnd = basepoint + maxParam * direction;

                            currentJoint.Origin[0] = newStart;
                            currentJoint.Origin[1] = newEnd;

                            // we remove the joint after absorbing it into this one
                            jointGroup.RemoveAt(j);
                            j--;
                        }
                    }
                }

                // we only add the remaining joints
                for (int i = 0; i < jointGroup.Count; i++)
                {
                    result.Add(jointGroup[i]);
                }

            }

            return result;

        }

        internal static List<GimJoint> GetObjectJointsRecursive(GimData gimData, uint elementIndex, uint parentIndex, int objectIndex)
        {
            GimElement element = gimData.Elements[(int)elementIndex];

            if(element.ElementType == GimElementType.R_Poly || element.ElementType == GimElementType.wire || element.ElementType.IsCurve()) 
                // if we encounter any of these elements, we just leave because it's an inapproprite branch
            {
                return null;
            }

            // we only care about the local for these elements

            bool localMatters = /*element.ElementType == GimElementType.D_link ||*/ element.ElementType == GimElementType.D_part
                             || element.ElementType == GimElementType.D_folder
                             || element.ElementType.IsBody();

            List<GimJoint> totalJoints = new();

            foreach (GimLocal local in element.Locals)
            {
                if (local.parent != parentIndex) continue; // we only use the relevant locals

                foreach (uint childId in element.Elements) // we keep branching recursively
                {

                    GimElement childElement = gimData.Elements[(int)childId];
                    List<GimJoint> childJoints = null;

                    if(childElement.ElementType == GimElementType.F_cilinder) // if we find a cylinder element, we begin extracting the joints
                    {
                        childJoints = GetCylinderAxis(gimData, childId, elementIndex, objectIndex);
                    }

                    else if (childElement.ElementType.IsFace()) // we are not interested in other face elements, so we save time
                    {
                        continue;
                    }

                    else // otherwise, we keep searching recursively
                    {
                        childJoints = GetObjectJointsRecursive(gimData, childId, elementIndex, objectIndex);
                    }

                    if (localMatters && (childJoints != null)) // after extracting joints with the two previous methods, we apply the necessary transformations to them, IF the local matters
                    {
                        Matrix localMatrix = new Matrix(local);
                        Vect localOffset = new Vect(local.Px, local.Py, local.Pz);
                        foreach (GimJoint joint in childJoints)
                        {
                            Vect jointStart = joint.Origin[0];
                            Vect jointEnd = joint.Origin[1];

                            jointStart = localMatrix * jointStart + localOffset; // we transform (rotate + translate) both points
                            jointEnd = localMatrix * jointEnd + localOffset; 

                            joint.Origin[0] = jointStart;
                            joint.Origin[1] = jointEnd;
                        }
                    }


                    if (childJoints != null) // we add the joints to the total joints array
                    {
                        totalJoints.AddRange(childJoints);
                    }
                }

            }


            return totalJoints;
            

        }

        internal static List<GimJoint> DetectJoints(GimData gimData, List<int> objects)
        {


            gimData.joints = new();

            Dictionary<int, List<GimJoint>> objectJoints = new(); // the joints, ordereed by each objectId
            Dictionary<int, Tuple<Vect, double>> objectJointBoundaries = new(); // the boundaries of every object, used to see if it's useful to check distance objects or not
            Dictionary<int, int> objectOccurence = new(); // used to get which occurence of the object we're at (aka local ID)

            foreach(int objectId in objects) // initializing the last dict
            {
                if (!objectOccurence.ContainsKey(objectId))
                {
                    objectOccurence.Add(objectId, 0);
                }
            }

            int objectIndex = 0;
            foreach (int objectId in objects)
            {


                var joints = JointDetector.GetObjectJoints(gimData, (uint)objectId, objectOccurence[objectId], objectIndex);
                objectOccurence[objectId]++; // we increment this to indicate that on the next iteration, we're taking the second local

                //string s = "";
                //foreach (Axis axis in axes)
                //{
                //	s += "(";
                //	s += axis.basePoint[0];
                //	s += ", ";
                //	s += axis.basePoint[1];
                //	s += ")\n";
                //}


                //List<GimJoint> joints = JointDetector.CreateJointsFromAxes(axisTuples, linkElements[0]);






                // we're now trying to construct a "sphere" around our joints that contains all the joint points
                // the reason for this, is that we use this to see if two objects are too distant from one another to have joints or not

                Vect centerPoint = new Vect(0, 0, 0);
                int count = 0;
                foreach (var joint in joints)
                {
                    Vect p1 = joint.Origin[0];
                    Vect p2 = joint.Origin[1];

                    centerPoint += p1;
                    centerPoint += p2;

                    count += 2;
                }

                centerPoint /= count;
                double maxDist = 0;

                foreach (var joint in joints)
                {
                    Vect p1 = joint.Origin[0];
                    Vect p2 = joint.Origin[1];

                    double dist1 = (p1 - centerPoint).Norm;
                    double dist2 = (p2 - centerPoint).Norm;

                    if (dist1 > maxDist)
                    {
                        maxDist = dist1;
                    }
                    if (dist2 > maxDist)
                    {
                        maxDist = dist2;
                    }
                }

                // we add the joints we found
                objectJoints.Add(objectIndex, joints);
                // we add the boundary we found
                objectJointBoundaries.Add(objectIndex, new Tuple<Vect, double>(centerPoint, maxDist));

                objectIndex++;
            }

            double margin = Matrix.EQUALITY_MARGIN;

            List<GimJoint> confirmedJoints = new();

            for (int i = 0; i < objectJoints.Count; i++)
            {
                int linkId = i;
                Vect currentCenter = objectJointBoundaries[linkId].Item1;
                double currentRadius = objectJointBoundaries[linkId].Item2;

                for (int j = i + 1; j < objectJoints.Count; j++)
                {
                    int otherLinkId = j;
                    Vect otherCenter = objectJointBoundaries[otherLinkId].Item1;
                    double otherRadius = objectJointBoundaries[otherLinkId].Item2;

                    double distance = (currentCenter - otherCenter).Norm;
                    if (distance > currentRadius + otherRadius + margin)
                    {
                        continue;
                    }


                    List<GimJoint> currentJoints = objectJoints[linkId];
                    List<GimJoint> otherJoints = objectJoints[otherLinkId];

                    foreach (GimJoint currentJoint in currentJoints)
                    {
                        foreach (GimJoint otherJoint in otherJoints)
                        {
                            if (currentJoint.OverlapsWith(otherJoint))
                            {
                                currentJoint.OverlapsWith(otherJoint);
                                Vect currentP1 = currentJoint.Origin[0];
                                Vect currentP2 = currentJoint.Origin[1];

                                Vect otherP1 = otherJoint.Origin[0];
                                Vect otherP2 = otherJoint.Origin[1];

                                Vect basePoint = currentP1;
                                Vect direction = (currentP2 - currentP1).Direction;

                                double t1 = (currentP1 - basePoint) * direction;
                                double t2 = (currentP2 - basePoint) * direction;
                                if (t1 > t2)
                                {
                                    double temp = t1;
                                    t1 = t2;
                                    t2 = temp;
                                }
                                double t3 = (otherP1 - basePoint) * direction;
                                double t4 = (otherP2 - basePoint) * direction;
                                if (t3 > t4)
                                {
                                    double temp = t3;
                                    t3 = t4;
                                    t4 = temp;
                                }

                                double minParam = 0, maxParam = 0;

                                if (t1 > maxParam) maxParam = t1;
                                if (t1 < minParam) minParam = t1;

                                if (t2 > maxParam) maxParam = t2;
                                if (t2 < minParam) minParam = t2;

                                if (t3 > maxParam) maxParam = t3;
                                if (t3 < minParam) minParam = t3;

                                if (t4 > maxParam) maxParam = t4;
                                if (t4 < minParam) minParam = t4;

                                Vect startPoint = basePoint + minParam * direction;
                                Vect endPoint = basePoint + maxParam * direction;


                                GimJoint overlapJoint = new GimJoint(currentJoint.Parent, otherJoint.Parent, GimJointType.Continouous, startPoint, endPoint);

                                if (((t1 > t3 + margin) && (t1 < t4 - margin)) || ((t2 > t3 + margin) && (t2 < t4 - margin)) ||
                                     ((t3 > t1 + margin) && (t3 < t2 - margin)) || ((t4 > t1 + margin) && (t4 < t2 - margin)))
                                {
                                    overlapJoint.Status = GimJointStatus.New_HighQuality;
                                }

                                if (t1 == t3 && t2 == t4)
                                {
                                    overlapJoint.Status = GimJointStatus.New_HighQuality;
                                }

                                confirmedJoints.Add(overlapJoint);
                            }
                        }
                    }
                }
            }


            for (int i = 0; i < confirmedJoints.Count; i++)
            {

                GimJoint currentJoint = confirmedJoints[i];
                for (int j = i + 1; j < confirmedJoints.Count; j++)
                {
                    GimJoint otherJoint = confirmedJoints[j];

                    if (currentJoint.Parent == otherJoint.Parent && currentJoint.Child == otherJoint.Child) // are between the same pieces
                    {
                        Vect currentDirection = (currentJoint.Origin[1] - currentJoint.Origin[0]).Direction;
                        Vect otherDirection = (otherJoint.Origin[1] - otherJoint.Origin[0]).Direction;
                        Vect offset = currentJoint.Origin[0] - otherJoint.Origin[0];

                        if (otherDirection.IsCollinearTo(currentDirection) && offset.IsCollinearTo(currentDirection)) // have the exact axis
                                                                                                                      // will be simplified into one joint
                        {
                            Vect basepoint = currentJoint.Origin[0];

                            Vect currentStart = currentJoint.Origin[0];
                            Vect currentEnd = currentJoint.Origin[1];

                            Vect otherStart = otherJoint.Origin[0];
                            Vect otherEnd = otherJoint.Origin[1];


                            Vect direction = (currentEnd - currentStart).Direction;

                            double param1 = (currentStart - basepoint) * direction;
                            double param2 = (currentEnd - basepoint) * direction;
                            double param3 = (otherStart - basepoint) * direction;
                            double param4 = (otherEnd - basepoint) * direction;

                            double minParam = 500000, maxParam = 0;

                            if (param1 < minParam) minParam = param1;
                            if (param2 < minParam) minParam = param2;
                            if (param3 < minParam) minParam = param3;
                            if (param4 < minParam) minParam = param4;

                            if (param1 > maxParam) maxParam = param1;
                            if (param2 > maxParam) maxParam = param2;
                            if (param3 > maxParam) maxParam = param3;
                            if (param4 > maxParam) maxParam = param4;

                            Vect newStart = basepoint + minParam * direction;
                            Vect newEnd = basepoint + maxParam * direction;

                            currentJoint.Origin[0] = newStart;
                            currentJoint.Origin[1] = newEnd;

                            confirmedJoints.RemoveAt(j);
                            j--;
                        }
                    }
                }
            }

            gimData.joints = confirmedJoints;

            return confirmedJoints;
        }

        internal static string DebugJoints(List<GimJoint> confirmedJoints)
        {
            string p = "";
            string freeCAD = "";
            int counter = 0;
            int index = 0;

            foreach (GimJoint joint in confirmedJoints)
            {

                //if (joint.status != GimJointStatus.New_HighQuality) continue;
                if (counter == 0)
                {
                    p += "Execute[{";
                }
                p += "\"Vector(";
                p += joint.Origin[0];
                p += ", ";
                p += joint.Origin[1];
                p += ")\"";


                Vect direction1 = joint.Origin[1] - joint.Origin[0];
                Vect end1 = joint.Origin[1] + direction1;
                Vect direction2 = joint.Origin[0] - joint.Origin[1];
                Vect end2 = joint.Origin[0] + direction2;

                Vect directionVector = -(end2 - end1).Direction;
                double height = (joint.Origin[0] - joint.Origin[1]).Norm;

                freeCAD += "cyl = Part.makeCylinder(2, " + height + ", FreeCAD.Base.Vector" + joint.Origin[0].ToString() + ", FreeCAD.Base.Vector" + directionVector.ToString() + ")\n";
                freeCAD += "cylObj = App.ActiveDocument.addObject(\"Part::Feature\",\"cyl" + index + "\")\n";
                freeCAD += "cylObj.Shape = cyl\n";

                freeCAD += "cylObj.ViewObject.ShapeColor  = (" + (new Random()).NextDouble().ToString("F1") + ", "
                    + (new Random()).NextDouble().ToString("F1") + ", " + (new Random()).NextDouble().ToString("F1") + ")\n\n\n";


                index++;
                counter++;

                if (counter == 25)
                {
                    counter = 0;
                    p += "}]\n\n\n";
                }
                else
                {
                    p += ", ";
                }
            }

            p += "}]\n";

            return freeCAD;
        }

        internal static List<int> GetLinkElements(GimData gimData, int linkIndex) 
        {
            List<int> shellElements = new List<int>();
            bool found = false;
            int index = 0;

            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement element = gimData.Elements[i];
                //if (element.ElementType == GimElementType.D_link)
                //{
                //    if (index < linkIndex)
                //    {
                //        index++;
                //        continue;
                //    }
                //    // at the beginning of each list is the parent part.
                //    shellElements.Add(i);
                //    found = true;
                //    break;
                //}
            }

            if (!found) return new List<int>();

            List<int> elementsToCheck = new List<int>() { shellElements[0] }; // we begin with the parent part
                                                                              // and we later expand 

            for (int i = 0; i < shellElements.Count; i++)
            {


                for (int el = 0; el < elementsToCheck.Count; el++)
                {
                    int elId = elementsToCheck[el];
                    GimElement element = gimData.Elements[elId];

                    if (element.Elements == null) continue;
                    foreach (int childId in element.Elements)
                    {
                        elementsToCheck.Add(childId);
                    }
                }
            }


            return elementsToCheck;
        }

        public static List<GimJoint> GetCylinderAxis(GimData gimData, uint cylinderId, uint parentId, int objectIndex)
        {
            GimElement cylinderElement = gimData.Elements[(int)cylinderId];
            if (cylinderElement.ElementType != GimElementType.F_cilinder) return null; // safety check

            List<GimJoint> joints = new();

            foreach (GimLocal local in cylinderElement.Locals)
            {
                if (local.parent != parentId) continue;

                GimLocal circleLocal = local;

                Vect basePoint = new Vect(circleLocal.Px, circleLocal.Py, circleLocal.Pz);
                Vect vector1 = new Vect(circleLocal, 0);
                Vect vector2 = new Vect(circleLocal, 1);

                Vect axisDirection = vector1 ^ vector2;

                uint cylinderWireIndex = cylinderElement.Elements[0];
                GimElement cylinderWire = gimData.Elements[(int)cylinderWireIndex];

                double minParam = 1000000, maxParam = 0;

                foreach (uint childId in cylinderWire.Elements)
                {
                    GimElement child = gimData.Elements[(int)childId];
                    if (child.ElementType != GimElementType.C_line && child.ElementType != GimElementType.C_bspline) continue;

                    Vect start, end;

                    double[] extremities = CurveHelpers.getStartAndEndPointsOfCurve(child, gimData);
                    start = extremities[0..3];
                    end = extremities[3..6];

                    double startParam = (start - basePoint) * axisDirection;
                    double endParam = (end - basePoint) * axisDirection;

                    if (startParam < minParam) minParam = startParam;
                    if (endParam < minParam) minParam = endParam;
                    if (startParam > maxParam) maxParam = startParam;
                    if (endParam > maxParam) maxParam = endParam;

                }

                if (minParam > maxParam)
                {
                    continue;
                }

                Vect startPoint = basePoint + minParam * axisDirection;
                Vect endPoint = basePoint + maxParam * axisDirection;


                joints.Add(new GimJoint(objectIndex, -1, GimJointType.Continouous, startPoint, endPoint));


            }

            return joints;
        }
        internal static List<GimJoint> FindCylinderAxis(GimData gimData, List<int> elementsToCheck, Matrix linkMatrix, Vect linkOrigin, int linkIndex)
        {
            List<GimJoint> joints = new();

            foreach(int i in elementsToCheck)
            {
                GimElement cylinderElement = gimData.Elements[i];
                if(cylinderElement.ElementType == GimElementType.F_cilinder)
                {
                    if(cylinderElement.Locals.Count > 1)
                    {
                        GimLogger.Error(new NotImplementedException("not implemented for multiple locals!"));
                    }

                    if(cylinderElement.Elements.Count < 1)
                    {
                        continue;
                    }

                    else
                    {
                        GimLocal circleLocal = cylinderElement.Locals[0];
                        Vect basePoint = new Vect(circleLocal.Px, circleLocal.Py, circleLocal.Pz);
                        Vect vector1 = new Vect(circleLocal, 0);
                        Vect vector2 = new Vect(circleLocal, 1);
                        sbyte type = 0;
                        double circleRadius = cylinderElement.ElementDoubles[0];

                        //uint parentWireIndex = childElement.Locals[0].Parent;
                        //GimElement parentWire = gimData.elements[(int)parentWireIndex];

                        //uint parentElementIndex = parentWire.Locals[0].Parent;
                        //GimElement parentElement = gimData.elements[(int)parentElementIndex]; //cylinder or the plane



                        
                        Vect axisDirection = vector1 ^ vector2;

                        uint cylinderWireIndex = cylinderElement.Elements[0];
                        GimElement cylinderWire = gimData.Elements[(int)cylinderWireIndex];

                        double minParam = 1000000, maxParam = 0;

                        foreach (uint childId in cylinderWire.Elements)
                        {
                            GimElement child = gimData.Elements[(int)childId];
                            if (child.ElementType != GimElementType.C_line && child.ElementType != GimElementType.C_bspline) continue;

                            Vect start, end;

                            double[] extremities = CurveHelpers.getStartAndEndPointsOfCurve(child, gimData);
                            start = extremities[0..3];
                            end = extremities[3..6];

                            double startParam = (start - basePoint) * axisDirection;
                            double endParam = (end - basePoint) * axisDirection;

                            if (startParam < minParam) minParam = startParam;
                            if (endParam < minParam) minParam = endParam;
                            if (startParam > maxParam) maxParam = startParam;
                            if (endParam > maxParam) maxParam = endParam;

                        }

                        if(minParam > maxParam)
                        {
                            continue;
                        }

                        Vect startPoint = basePoint + minParam * axisDirection;
                        Vect endPoint = basePoint + maxParam * axisDirection;


                        basePoint = linkMatrix * basePoint + linkOrigin;
                        axisDirection = linkMatrix * axisDirection;

                        startPoint = linkMatrix * startPoint + linkOrigin;
                        endPoint = linkMatrix * endPoint + linkOrigin;  

                        joints.Add(new GimJoint(linkIndex, -1, GimJointType.Continouous, startPoint, endPoint));
                    }
                }
            }
            //for (int i = 0; i < result.Count-1; i++) 
            //{
            //    Axis axe = result[i];
            //    for (int j = i + 1; j < result.Count; j++) 
            //    {
            //        Axis nextAxe = result[j];
            //        if (axe.basePoint == nextAxe.basePoint && axe.circleRadius == nextAxe.circleRadius && axe.direction.IsCollinearTo(nextAxe.direction)) 
            //        {
            //            if (axe.type == 0)
            //            {
            //                axe.type = nextAxe.type; 
            //            } 
            //            else if(nextAxe.type == 0)
            //            {
            //                nextAxe.type = axe.type;
            //            }
            //        }    
            //    }
            //}

            List<Tuple<GimJoint, List < GimJoint >>> jointTuples = new();
      
            foreach(GimJoint joint in joints)
            {
                bool found = false;
                foreach(var jointTuple in jointTuples)
                {
                    Vect tupleDirection = (jointTuple.Item1.Origin[1] - jointTuple.Item1.Origin[0]).Direction;
                    Vect jointDirection = (joint.Origin[1] - joint.Origin[0]).Direction;
                    Vect offset = joint.Origin[0] - jointTuple.Item1.Origin[0];

                    if (jointDirection.IsCollinearTo(tupleDirection) && offset.IsCollinearTo(jointDirection))
                    {
                        found = true;
                        jointTuple.Item2.Add(joint);
                        break;
                    }
                }
                if (!found)
                {
                    var jointTuple = new Tuple<GimJoint, List<GimJoint>> (joint, new List<GimJoint>() { joint });
                    jointTuples.Add(jointTuple);
                }
            }

            List<GimJoint> result = new();

            foreach (var jointTuple in jointTuples)
            {
                List<GimJoint> jointGroup = jointTuple.Item2;

                for (int i = 0; i < jointGroup.Count - 1; i++)
                {
                    GimJoint currentJoint = jointGroup[i];

                    for (int j = i + 1; j < jointGroup.Count; j++)
                    {
                        GimJoint otherJoint = jointGroup[j];

                        if (currentJoint.OverlapsWith(otherJoint))
                        {
                            Vect basepoint = currentJoint.Origin[0];

                            Vect currentStart = currentJoint.Origin[0];
                            Vect currentEnd = currentJoint.Origin[1];

                            Vect otherStart = otherJoint.Origin[0];
                            Vect otherEnd = otherJoint.Origin[1];


                            Vect direction = (currentEnd - currentStart).Direction;

                            double param1 = (currentStart - basepoint) * direction;
                            double param2 = (currentEnd - basepoint) * direction;
                            double param3 = (otherStart - basepoint) * direction;
                            double param4 = (otherEnd - basepoint) * direction;

                            double minParam = 300000, maxParam = 0;

                            if (param1 < minParam) minParam = param1;
                            if (param2 < minParam) minParam = param2;
                            if (param3 < minParam) minParam = param3;
                            if (param4 < minParam) minParam = param4;

                            if (param1 > maxParam) maxParam = param1;
                            if (param2 > maxParam) maxParam = param2;
                            if (param3 > maxParam) maxParam = param3;
                            if (param4 > maxParam) maxParam = param4;

                            Vect newStart = basepoint + minParam * direction;
                            Vect newEnd = basepoint + maxParam * direction;

                            currentJoint.Origin[0] = newStart;
                            currentJoint.Origin[1] = newEnd;

                            jointGroup.RemoveAt(j);
                            j--;
                        }
                    }
                }

                for (int i = 0; i < jointGroup.Count; i++)
                {
                    result.Add(jointGroup[i]);
                }

            }

            return result;
        }

        internal static List<GimJoint> CreateJointsFromAxes(List<Tuple<Axis, List<Axis>>> axisTuples, int linkId)
        {
            List<GimJoint> joints = new();

            foreach(var axisTuple in axisTuples)
            {
                List<Axis> axisGroup = axisTuple.Item2;
                if (axisGroup.Count == 0) continue;
                Vect basePoint = axisGroup[0].basePoint;
                Vect direction = axisGroup[0].direction;
                double minParam = 0, maxParam = 0;
                //bool internalAxis = false;
                //bool externalAxis = false;
                foreach(Axis axis in axisGroup)
                {
                    Vect offset = axis.basePoint - basePoint;
                    double parameter = offset * direction;
                    if(parameter < minParam) minParam = parameter;
                    if(parameter > maxParam) maxParam = parameter;

                    //if (axis.type == 1) { externalAxis = true; }
                    //else if (axis.type == -1) { internalAxis = true; }
                }

                Vect minPoint = basePoint + minParam * direction;
                Vect maxPoint = basePoint + maxParam * direction;

                // leave child to -1 for now because we only checked one piece.
                // When we compare the different pieces, we will set parent and child. 
                //if (internalAxis) {
                //    GimJoint joint = new GimJoint(linkId, -1, GimJointType.Axe, minPoint, maxPoint);
                //    joint.external = false;
                //    joints.Add(joint);

                //}
                //if (externalAxis) {
                //    GimJoint joint = new GimJoint(linkId, -1, GimJointType.Axe, minPoint, maxPoint);
                //    joint.external = true;
                //    joints.Add(joint);
                //}

                GimJoint joint = new GimJoint(linkId, -1, GimJointType.Continouous, minPoint, maxPoint);
                joints.Add(joint);
            }

            List<GimJoint> result = new();
            foreach(var joint in joints)
            {
                double length = (joint.Origin[0] - joint.Origin[1]).Norm;
                if(length > 0)
                {
                    result.Add(joint);
                }
            }

            return result;
        }

        internal static void AddJoints(GimData gimData, GimData elemgd)
        {
            foreach (GimJoint joint in gimData.Joints)
            {
                GimElement newJoint = new GimElement();
                newJoint.elementType = GimElementType.Gim_Joint;
                int[] elementIntsToAdd = new int[3];
                newJoint.ElementInts = elementIntsToAdd;
                newJoint.elementInts[0] = 1;
                newJoint.elementInts[1] = joint.Parent;
                newJoint.elementInts[2] = joint.Child;
                double[] elementDoublesToAdd = new double[17];
                newJoint.elementDoubles = elementDoublesToAdd;
                newJoint.elementDoubles[0] = joint.Origin[0][0];
                newJoint.elementDoubles[1] = joint.Origin[0][1];
                newJoint.elementDoubles[2] = joint.Origin[0][2];
                newJoint.elementDoubles[3] = joint.Origin[1][0];
                newJoint.elementDoubles[4] = joint.Origin[1][1];
                newJoint.elementDoubles[5] = joint.Origin[1][2];
                newJoint.elementDoubles[6] = 0;
                newJoint.elementDoubles[7] = 0;
                newJoint.elementDoubles[8] = 0;
                newJoint.elementDoubles[9] = 0;
                newJoint.elementDoubles[10] = 0;
                newJoint.elementDoubles[11] = 0;
                newJoint.elementDoubles[12] = 0;
                newJoint.elementDoubles[13] = 0;
                newJoint.elementDoubles[14] = 0;
                newJoint.elementDoubles[15] = 0;
                newJoint.elementDoubles[16] = 0;
                int jointID = elemgd.Elements.Count;
                elemgd.Elements.Add(newJoint);
            }
        }

    }
}
