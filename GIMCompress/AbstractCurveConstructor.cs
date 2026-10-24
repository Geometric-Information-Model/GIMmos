using System;
using System.Collections.Generic;
using GimUtilities;

namespace GIMcompress
{
	public abstract class AbstractCurveConstructor
    {
        protected const int defaultSegmentNumber = 100; // this is the number of segments we will construct using a paramater t:
                                                           // t will increment along 1/value 

        protected const double defaultEquidistantPointPercentage = 0.2; // because parametric curves aren't evaluated equi-distantly,
                                                                        // it doesn't make sense to compare points that are arbitrary
                                                                        // (some curves construct "faster" and "slower" than other curves at
                                                                        // different parts).
                                                                        // After constructing the segments, we will move along the segments,
                                                                        // and take a point after we have crossed a certain "distance" from
                                                                        // the last crossed point, therefore ensuring that we leap
                                                                        // an equal amount along the curve


        public Vect[] Segments;

        protected bool inversed = false;
        protected int segmentNumber;
        protected double equidistantPointPercentage;
        public double curveLength;


        public static AbstractCurveConstructor GetCurveConstructor(GimElement curveElement, bool inversed = false, int segmentNumber = defaultSegmentNumber,
            double pointPercentage = defaultEquidistantPointPercentage)
        {
            switch (curveElement.ElementType)
            {
                case GimElementType.C_bspline: return new BsplineConstructor(curveElement, inversed, segmentNumber, pointPercentage);
                case GimElementType.C_circle: return new CircleConstructor(curveElement, inversed, segmentNumber, pointPercentage);
                default: GimLogger.Error(new NotImplementedException("Curve Constructor for type " + curveElement.ElementType.ToString() + " not implemented!"));break;
            }

            
            return new BsplineConstructor(curveElement, inversed, segmentNumber, pointPercentage);
        }

        public abstract void constructCurve(); // this is the part to implement, it constructs the curve out of its parameters


       
        public double calculateCurveLength() {

            curveLength = 0;

            for (int i = 0; i < Segments.Length - 1; i++)
            {
                double length = (Segments[i + 1] - Segments[i]).Norm;
                curveLength += length;
            }
            return curveLength;
        }
        public Vect[] getApproximativePoints() // this part constructs the approximative polygon, independant of the curve type
        {
            constructCurve();
            int pointCount = (int)(equidistantPointPercentage * segmentNumber);
            Vect[] points = new Vect[pointCount + 1];

            calculateCurveLength();

            double desiredLength = curveLength / pointCount;

            double currentDistance = 0;
            Vect previousPoint;


            previousPoint = Segments[0];
            points[0] = previousPoint;
            int index = 1;
            for (int i = 0; i < Segments.Length - 1; i += 1)
            {
                double length = (Segments[i + 1] - Segments[i]).Norm;
                currentDistance += length;
                if (currentDistance >= desiredLength - desiredLength*0.001) // checking if we got the right amount (with 0.1% tolerance)
                {
                    points[index] = Segments[i + 1];
                    previousPoint = Segments[i + 1];
                    index++;
                    currentDistance = currentDistance - desiredLength;
                }
            }

            if (points[^1] != Segments[^1])
            {
                points[^1] = Segments[^1];
            }



            string str = "[ ";

            for (int i = 0; i < points.Length; i++)
            {

                str += "[" + points[i][0] + ", " + points[i][1] + ", " + points[i][2] + " ]";

                if (i != points.Length - 1)
                {
                    str += ", ";
                }
            }

            str += " ]";

            int x = 9; //debug line TODO: REMOVE


            return points;
        }
    }

    public class BsplineConstructor: AbstractCurveConstructor
    {
		readonly GimElement bsplineElement;


        public BsplineConstructor(GimElement bsplineElement, bool inversed, int segmentNumber = defaultSegmentNumber,
            double pointPercentage = defaultEquidistantPointPercentage)
        {
            this.segmentNumber = segmentNumber;
            this.equidistantPointPercentage = pointPercentage;
            this.bsplineElement = bsplineElement;
            this.inversed = inversed;
        }

        public override void constructCurve()
        {
            CorrectKnotsIfBsplineInverted();
            int degree = bsplineElement.ElementInts[^1];
            int knotsCount = bsplineElement.ElementInts[0];
            int multiplicitiesCount = bsplineElement.ElementInts.Length - 2;
            int polesCount = (bsplineElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

            int firstPoleIndex = 6;
            Vect[] poles = new Vect[polesCount];
            for(int i = 0; i < polesCount; i++)
            {
                int index = firstPoleIndex + i * 3;

                poles[i] = new Vect(
                    bsplineElement.ElementDoubles[index],
                    bsplineElement.ElementDoubles[index + 1],
                    bsplineElement.ElementDoubles[index + 2]
                );

            }
            double[] weights = new double[polesCount];
            for (int i = 0; i < polesCount; i++)
            {
                weights[i] = bsplineElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount];
            }

            List<double> knotList = new();

            double[] knotValues = new double[knotsCount];
            for (int i = 0; i < knotsCount; i++)
            {
                knotValues[i] = bsplineElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
            }
            double paramStart = knotValues[0];
            double paramEnd = knotValues[0];

            for (int i = 0;i < multiplicitiesCount; i++)
            {
                int multiplicity = bsplineElement.ElementInts[i + 1];

                for(int j=0;j < multiplicity; j++)
                {

                    double knotParam = knotValues[i];
                    knotList.Add(knotParam);
                    paramEnd = knotParam;
                }
            }

            double[] knots = knotList.ToArray();

            double increment;
            int segmentIndex = 0;
            Segments = new Vect[segmentNumber + 1];

            
            if(!inversed){
                increment = (paramEnd - paramStart) / segmentNumber;
                for (double t = paramStart; t < paramEnd + increment; t += increment)
                {
                    if (segmentIndex >= Segments.Length) break;
                    Vect point = new Vect(0, 0, 0);
                    double denominator = 0;
                    if (t > paramEnd && t < paramEnd + increment) // rounding issues
                    {
                        t = paramEnd;
                    }
                    for (int i = 0; i < polesCount; i++)
                    {
                        Vect pole = poles[i];
                        double basisValue = BasisFunction(degree, i, knots, t);
                        double weight = weights[i];
                        point += basisValue * weight * pole;
                        denominator += basisValue * weight;
                    }
                    point /= denominator;
                    point[0] = ValueRounder.roundDouble(point[0], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[1] = ValueRounder.roundDouble(point[1], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[2] = ValueRounder.roundDouble(point[2], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    Segments[segmentIndex] = point;
                    segmentIndex++;
                    if (t == paramEnd) break;
                }
            }
            else
            {
                increment = (paramStart - paramEnd) / segmentNumber;
                for (double t = paramEnd; t > paramStart + increment; t += increment)
                {
                    if (segmentIndex >= Segments.Length) break;
                    Vect point = new Vect(0, 0, 0);
                    double denominator = 0;
                    if (t < paramStart && t > paramStart + increment) // rounding issues
                    {
                        t = paramStart;
                    }
                    for (int i = 0; i < polesCount; i++)
                    {
                        Vect pole = poles[i];
                        double basisValue = BasisFunction(degree, i, knots, t);
                        double weight = weights[i];
                        point += basisValue * weight * pole;
                        denominator += basisValue * weight;
                    }
                    point /= denominator;

                    point[0] = ValueRounder.roundDouble(point[0], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[1] = ValueRounder.roundDouble(point[1], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[2] = ValueRounder.roundDouble(point[2], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

                    Segments[segmentIndex] = point;
                    segmentIndex++;
                    if (t == paramStart) break;
                }
            }


        }

        private void CorrectKnotsIfBsplineInverted()
        {
            int degree = bsplineElement.ElementInts[^1];
            int knotsCount = bsplineElement.ElementInts[0];
            int multiplicitiesCount = bsplineElement.ElementInts.Length - 2;
            int polesCount = (bsplineElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

            double[] knotValues = new double[knotsCount];

            double maxKnot = -10;
            for (int i = 0; i < knotsCount; i++)
            {
                knotValues[i] = bsplineElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
                if (knotValues[i] > maxKnot)
                {
                    maxKnot = knotValues[i];
                }
            }

            // order is normal, no need to invert
            if (knotValues[^1] >= knotValues[0]) return;

            // order is backwards, we invert the knots while keeping the same interval between them
            // to do this, we make them all negative and add the biggest knot. this way we preserve the interval lengths:
            // ex:

            // 3, 2, 2.5, 1 => negative => -3 , -2, -2.5, -1 => +3 => 0, 1, 1.5, 2
            int x = 9; // debug line TODO REMOVE
            for (int i = 0; i < knotsCount; i++)
            {
                // this keeps the same intervals of values, but ensures the order flips around
                knotValues[i] = -knotValues[i] + maxKnot;

            }


            for (int i = 0; i < knotsCount; i++)
            {
                // we insert the corrected knots back in
                bsplineElement.ElementDoubles[i + polesCount*3 + ( 6)] = knotValues[i];
            }



        }

        private double BasisFunction(int k, int i, double[] knots, double t)
        {
            if (k == 0)
            {
                if ((knots[i] <= t) && (t <= knots[i + 1]))
                    return 1;
                else
                    return 0;
            }
            else
            {
                double memb1, memb2;
                if (knots[i + k] == knots[i])
                    memb1 = 0;
                else
                    memb1 = ((t - knots[i]) / (knots[i + k] - knots[i])) * BasisFunction(k - 1, i, knots, t);
                if (knots[i + k + 1] == knots[i + 1])
                    memb2 = 0;
                else
                    memb2 = ((knots[i + k + 1] - t) / (knots[i + k + 1] - knots[i + 1])) * BasisFunction(k - 1, i + 1, knots, t);
                return memb1 + memb2;
            }
        }
    }

    public class CircleConstructor : AbstractCurveConstructor
    {
		readonly GimElement circleElement;
        public CircleConstructor(GimElement circleElement, bool inversed, int segmentNumber = defaultSegmentNumber,
            double pointPercentage = defaultEquidistantPointPercentage)
        {
            this.segmentNumber = segmentNumber;
            this.equidistantPointPercentage = pointPercentage;
            this.circleElement = circleElement;
            this.inversed = inversed;
        }

        public override void constructCurve()
        {

            double radius = circleElement.ElementDoubles[0];
            double paramStart = circleElement.ElementDoubles[1];
            double paramEnd = circleElement.ElementDoubles[2];
            double increment;

            Matrix circleMatrix = new Matrix(circleElement.Locals[0]);
            Vect vect1 = circleMatrix[0];
            Vect vect2 = circleMatrix[1];

            Vect centerPoint = new Vect(circleElement.Locals[0].Px,
                circleElement.Locals[0].Py,
                circleElement.Locals[0].Pz
            );

            int segmentIndex = 0;
            if (!inversed)
            {
                increment = (paramEnd - paramStart) / segmentNumber;
                for (double t = paramStart; t < paramEnd + increment; t += increment)
                {
                    if (segmentIndex >= Segments.Length) break;
                    Vect point = new Vect(0, 0, 0);
                    if (t > paramEnd && t < paramEnd + increment) // rounding issues
                    {
                        t = paramEnd;
                    }

                    point = centerPoint + radius * (Math.Cos(t) * vect1 + Math.Sin(t) * vect2);
                    
                    point[0] = ValueRounder.roundDouble(point[0], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[1] = ValueRounder.roundDouble(point[1], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[2] = ValueRounder.roundDouble(point[2], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    Segments[segmentIndex] = point;
                    segmentIndex++;
                    if (t == paramEnd) break;
                }
            }
            else
            {
                increment = (paramStart - paramEnd) / segmentNumber;
                for (double t = paramEnd; t > paramStart + increment; t += increment)
                {
                    if (segmentIndex >= Segments.Length) break;
                    Vect point = new Vect(0, 0, 0);
                    if (t < paramStart && t > paramStart + increment) // rounding issues
                    {
                        t = paramStart;
                    }

                    point = centerPoint + radius * (Math.Cos(t) * vect1 + Math.Sin(t) * vect2);

                    point[0] = ValueRounder.roundDouble(point[0], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[1] = ValueRounder.roundDouble(point[1], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    point[2] = ValueRounder.roundDouble(point[2], Matrix.EQUALITY_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
                    Segments[segmentIndex] = point;
                    segmentIndex++;
                    if (t == paramEnd) break;
                }
            }
        }
    }
}
