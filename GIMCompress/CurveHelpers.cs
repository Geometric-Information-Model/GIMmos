using System;
using System.Collections.Generic;
using GimUtilities;

namespace GIMcompress
{

	public static class CurveHelpers   
    {
        public static Vect GetSplineVect(GimElement splineElement, GimData gimData)
        {
            GimLocal splineLocal = splineElement.Locals[0];

            // indexes of starting and ending points, in case the data structure changes later on.
            int firstPointIndex = 0;
            int lastPointIndex = 3;

            Vect firstPoint = new Vect(
                splineElement.ElementDoubles[firstPointIndex], //  X
                splineElement.ElementDoubles[firstPointIndex + 1],// Y
                splineElement.ElementDoubles[firstPointIndex + 2] // Z
            );

            Vect lastPoint = new Vect(
                splineElement.ElementDoubles[lastPointIndex], //  X
                splineElement.ElementDoubles[lastPointIndex + 1],// Y
                splineElement.ElementDoubles[lastPointIndex + 2] // Z
            );

            Vect splineVector = lastPoint - firstPoint; // direction of matrix
            splineVector = splineVector.Direction;

            Vect resultingVect = splineVector;
            return resultingVect;

        }

        public static bool checkSplineIsFlat(GimElement splineElement)
        {
            // we double check it's a spline
            if (splineElement.ElementType != GimElementType.C_bspline)
                throw GimLogger.Error(new ArgumentException("Object Passed is not a 3D spline element!"));
            // current assumptions, number of points to check = number of poles + start + end;
            int firstPoleIndex = 6;
            int degree = splineElement.ElementInts[^1];
            int knotsCount = splineElement.ElementInts[0];
            int multiplicitiesCount = splineElement.ElementInts.Length - 2;
            int polesCount = (splineElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;
            int lastPoleIndex = firstPoleIndex + (polesCount - 1) * 3; // 3 coordinates for every point. 

            // indexes of starting and ending points, in case the data structure changes later on.
            int firstPointIndex = 0;
            int lastPointIndex = 3;

            Vect firstPoint = new Vect(
                splineElement.ElementDoubles[firstPointIndex], //  X
                splineElement.ElementDoubles[firstPointIndex + 1],// Y
                splineElement.ElementDoubles[firstPointIndex + 2] // Z
            );

            Vect lastPoint = new Vect(
                splineElement.ElementDoubles[lastPointIndex], //  X
                splineElement.ElementDoubles[lastPointIndex + 1],// Y
                splineElement.ElementDoubles[lastPointIndex + 2] // Z
            );

            Vect directionVector = lastPoint - firstPoint; // guiding vector of the spline


            double directionVectorNorm = directionVector.Norm;     // calculating the vector norm, to get normalized vector


            if (directionVectorNorm == 0) return true; // if the spline has no length, it is by definition flat

            double margin = Math.Pow(10, -6);
            short precision = 4; // values used to determine the precision of the flatness

            // we will now check the flatness of this spline. If it is indeed flat, then all the controlpoints or 
            // poles will lie aligned with the start and end points
            for (int i = firstPoleIndex; i <= lastPoleIndex && i + 2 < splineElement.ElementDoubles.Length; i += 3)
            // we increment by 3 cause each point has 3 coords
            {
                Vect point = new Vect( // our point to check, let's call it P
                    splineElement.ElementDoubles[i],
                    splineElement.ElementDoubles[i + 1],
                    splineElement.ElementDoubles[i + 2]
                );

                Vect PA = firstPoint - point; // vector to first point, A 


                Vect PB = lastPoint - point; // vector to last point, B

                // now, if P belongs to the segment, the cross product PA x PB = null Vector (or almost so),
                // as PA and PB would be co-linear. If otherwise, the point P does not belong to the segment
                // and therefore, the spline is NOT a flat line.

                Vect cross = PA ^ PB;

                double norm = cross.Norm;

                norm = ValueRounder.roundDouble(norm, margin, precision);

                if (norm != 0) return false;
            }

            // if we checked all points and none of them were off the line, the curb is flat

            return true;
        }

        public static Vect[] getIntermediaryPointsOfCurve(GimElement curve, int localIndex = 0)
        {
            // lines have no intermediary points
            if (curve.ElementType == GimElementType.C_line)
            {
                return (new Vect[] { });
            }

            if (curve.ElementType == GimElementType.C_bspline)
            {
                // we don't take control points of flat beziers
                if (checkSplineIsFlat(curve))
                {
                    return (new Vect[] { });
                }


                int degree = curve.ElementInts[^1];
                int knotsCount = curve.ElementInts[0];
                int multiplicitiesCount = curve.ElementInts.Length - 2;
                int polesCount = (curve.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                int firstPoleIndex = 6;

                int lastPoleIndex = firstPoleIndex + (polesCount - 1) * 3; // 3 coordinates for every point.
                int pointCount = (lastPoleIndex - firstPoleIndex + 1) / 3;

                Vect[] intermediaryPoints = new Vect[polesCount];
                int index = 0;

                for (int i = firstPoleIndex; i <= lastPoleIndex && i + 2 < curve.ElementDoubles.Length; i += 3)
                // we increment by 3 cause each point has 3 coords
                {
                    Vect point = new Vect( // point to add
                       curve.ElementDoubles[i],
                       curve.ElementDoubles[i + 1],
                       curve.ElementDoubles[i + 2]
                   );

                    intermediaryPoints[index] = point;
                    index++;
                }

                return intermediaryPoints;

            }

            if (curve.ElementType == GimElementType.C_circle)
            {
                // a circle has one intermediary point, which is the midpoint of the two endpoints of the circle.
                // this way, it will be easy to reconstruct a circle since the center can be determined from
                // the three points of the arc.
                Vect basePoint = new Vect(
                    curve.locals[localIndex].Px,
                    curve.locals[localIndex].Py,
                    curve.locals[localIndex].Pz
                );

                Matrix elementMatrix = new Matrix(curve.locals[localIndex]);

                // currently, the first line is the first vector, and the second line is the second
                Vect firstVector = elementMatrix[0];
                Vect secondVector = elementMatrix[1];

                double radius = curve.ElementDoubles[0];
                double t1 = curve.ElementDoubles[1];
                double t2 = curve.ElementDoubles[2];

                if (t2 < t1) // if t2 is less than t1, this means the we start from t1, we go all the way to 2pi
                             // then from 0 to t2. this is different than from t2-> t1.
                             //if they are equal, it's a full circle (can't be a single point)
                {
                    t2 = t2 + 2 * Math.PI;
                }
                double diff = Math.Abs(t2 - t1);

                double midT;
                Vect point;

                if (diff < Matrix.EQUALITY_MARGIN) // full circle, we need 2 points
                {

                    midT = (t1 + t2) / 2; // from first point to second, this is an important distinction.
                    point = basePoint + radius * (Math.Cos(midT) * firstVector + Math.Sin(midT) * secondVector);

                    double quarterT = (t1 + midT) / 2;
                    Vect quarterPoint = basePoint + radius * (Math.Cos(quarterT) * firstVector + Math.Sin(quarterT) * secondVector);

                    return (new Vect[] { quarterPoint, point });
                }

                // TODO: unique case of a full circle.

                midT = (t1 + t2) / 2; // from first point to second, this is an important distinction.
                point = basePoint + radius * (Math.Cos(midT) * firstVector + Math.Sin(midT) * secondVector);


                return (new Vect[] { point });
            }

            // TODO: bezier

            return (new Vect[] { });
        }

        public static double[] getStartAndEndPointsOfCurve(GimElement curve, GimData gimdata, int localIndex = 0)
        {
            // for a line we simply get p0 = b + t0*v and p1 = b+t1*v, parametric equations of the points along [t0,1];
            if (curve.ElementType == GimElementType.C_line)
            {
                Vect lineBasisPoint = new double[]
                {
                    curve.Locals[localIndex].Px,
                    curve.Locals[localIndex].Py,
                    curve.Locals[localIndex].Pz
                };

                Vect lineDirectionVector = new double[]
                   {
                    curve.Locals[localIndex].Zx,
                    curve.Locals[localIndex].Zy,
                    curve.Locals[localIndex].Zz
                   };

                double t1 = curve.ElementDoubles[0];
                // p1 = b + t1*v, b = basis point, t = elementDouble[0], v = direction vector
                Vect point1 = lineBasisPoint + t1 * lineDirectionVector;

                double t2 = curve.ElementDoubles[1];
                // same as above point
                Vect point2 = lineBasisPoint + t2 * lineDirectionVector;

                return (double[])point1.Concat(point2);

            }

            if (curve.ElementType == GimElementType.C_bspline)
            {
                Vect point1 = new Vect( // in a c_spline, the first 3 values are coordinates for the first point
                    curve.ElementDoubles[0],
                    curve.ElementDoubles[1],
                    curve.ElementDoubles[2]
                );

                Vect point2 = new Vect( // in a c_spline, the values 3-5 are coordinates for the final point
                    curve.ElementDoubles[3],
                    curve.ElementDoubles[4],
                    curve.ElementDoubles[5]
                );

                return (double[])point1.Concat(point2);
            }

            if (curve.ElementType == GimElementType.C_bezier)
            {
                // TODO: check if bezier is the same as bspline, assuming it is for now

                Vect point1 = new Vect(
                    curve.ElementDoubles[0],
                    curve.ElementDoubles[1],
                    curve.ElementDoubles[2]
                );

                Vect point2 = new Vect(
                    curve.ElementDoubles[3],
                    curve.ElementDoubles[4],
                    curve.ElementDoubles[5]
                );

                return (double[])point1.Concat(point2);
            }

            if (curve.ElementType == GimElementType.C_circle)
            {
                Vect basePoint = new Vect(
                    curve.locals[localIndex].Px,
                    curve.locals[localIndex].Py,
                    curve.locals[localIndex].Pz
                );

                Matrix elementMatrix = new Matrix(curve.locals[localIndex]);

                // currently, the first line is the first vector, and the second line is the second
                Vect firstVector = elementMatrix[0];
                Vect secondVector = elementMatrix[1];

                double radius = curve.ElementDoubles[0];
                double t1 = curve.ElementDoubles[1];
                double t2 = curve.ElementDoubles[2];

                // definition of a circle, spinning two vectors around an origin
                Vect point1 = basePoint + radius * (Math.Cos(t1) * firstVector + Math.Sin(t1) * secondVector);
                Vect point2 = basePoint + radius * (Math.Cos(t2) * firstVector + Math.Sin(t2) * secondVector);


                return (double[])point1.Concat(point2);
            }

            if (curve.ElementType == GimElementType.C_elipse)
            {
                Vect basePoint = new Vect(
                    curve.locals[localIndex].Px,
                    curve.locals[localIndex].Py,
                    curve.locals[localIndex].Pz
                );

                Matrix elementMatrix = new Matrix(curve.locals[localIndex]);

                // currently, the first line is the first vector, and the second line is the second
                Vect firstVector = elementMatrix[0];
                Vect secondVector = elementMatrix[1];

                double radius1 = curve.ElementDoubles[0];
                double radius2 = curve.ElementDoubles[0];
                double t1 = curve.ElementDoubles[1];
                double t2 = curve.ElementDoubles[2];

                // definition of a ellipse, spinning two vectors around an origin with different Radii
                Vect point1 = basePoint + radius1 * (Math.Cos(t1) * firstVector) + radius2 * (Math.Sin(t1) * secondVector);
                Vect point2 = basePoint + radius1 * (Math.Cos(t2) * firstVector) + radius2 * (Math.Sin(t2) * secondVector);


                return (double[])point1.Concat(point2);
            }

            if (curve.ElementType == GimElementType.Gim_ComplexSegment)
            {
                // the extremities of a complex segment are the only points that don't repeat twice (not connection points)
                // if all the points repeat twice => fully connected polygon => any point can count as the start or end
                List<Vect> points = new List<Vect>();
                foreach (uint childId in curve.Elements)
                {
                    GimElement childPolygon = gimdata.Elements[(int)childId];
                    double[] extremities = getStartAndEndPointsOfCurve(childPolygon, gimdata, 0);
                    Vect Start = extremities[0..3];
                    Vect End = extremities[3..6];
                    points.Add(Start);
                    points.Add(End);
                }

                int startIndex = 0, endIndex = 0; // default to 0 so if the polygon is fully connected, we take the first point as start + end
                bool firstFound = false, secondFound = false;

                for (int i = 0; i < points.Count; i++)
                {
                    Vect point = points[i];
                    int occurences = 0;
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (points[j] == point)
                        {
                            occurences++;
                            if (occurences == 2) break;
                        }
                    }
                    if (occurences == 1)
                    {
                        if (!firstFound)
                        {
                            firstFound = true;
                            startIndex = i;
                        }
                        else
                        {
                            secondFound = true;
                            endIndex = i;
                            break;
                        }
                    }
                }

                Vect start = points[startIndex];
                Vect end = points[endIndex];

                return (double[])start.Concat(end);

            }

            return null;

        }

    }

}