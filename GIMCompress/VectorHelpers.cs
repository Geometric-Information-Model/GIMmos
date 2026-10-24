using System;
using System.Collections.Generic;
using System.Linq;
using GimUtilities;

namespace GIMcompress
{

	public static class VectorHelpers
    {
        // for Arrays, all the methods here have one array equivalent and one list equivalent.
        //
        // the list equivalent calls the existing Array equivalent. This is to avoid having to
        // write each function twice. If a modification is needed, only the array function would
        // be changed.

        public static double[] Sum(double[] vectA, double[] vectB)
        {
            // Asserting vectA and vectB are the same length
            AssertArraySameLength(vectA, vectB);

            double[] result = new double[vectA.Length];
            for(int i=0; i< vectA.Length; i++)
            {
                result[i] = vectA[i] + vectB[i];
            }

            return result;

        }
        // for Lists
        public static List<double> Sum(List<double> vectA, List<double> vectB)
        {
            return Sum(vectA.ToArray(), vectB.ToArray()).ToList();
        }

        // Subtracts B from A, result = A - B
        public static double[] Subtract(double[] vectA, double[] vectB)
        {
            // Asserting vectA and vectB are the same length
            AssertArraySameLength(vectA, vectB);

            double[] result = new double[vectA.Length];
            for (int i = 0; i < vectA.Length; i++)
            {
                result[i] = vectA[i] - vectB[i];
            }

            return result;

        }
        public static List<double> Subtract(List<double> vectA, List<double> vectB)
        {
            return Subtract(vectA.ToArray(), vectB.ToArray()).ToList();
        }

        // result = mul * Vector
        public static double[] ScaleVector(double multiplier, double[] vect)
        {
            return vect.Select((val) => (multiplier * val)).ToArray();
        }

        public static List<double> ScaleVector(double multiplier, List<double> vect)
        {
            return ScaleVector(multiplier, vect.ToArray()).ToList();
        }

        //calculating the norm of a vector
        public static double Norm(double[] vect)
        {
            double norm = 0;
            for(int i =0; i < vect.Length; i++)
            {
                norm += vect[i] * vect[i];
            }

            return Math.Sqrt(norm);
        }

        public static double Norm(List<double> vect)
        {
            return Norm(vect.ToArray());
        }

        // normalize a vector to have norm = 1. Throws an exception if the vector's norm is 0

        public static double[] Normalize(double[] vect)
        {
            double norm = Norm(vect);
            if (norm == 0) return (double[])vect.Clone(); // null vector returns the null vector

            return ScaleVector(1/norm, vect);
        }
        public static List<double> Normalize(List<double> vect)
        {
            return Normalize(vect.ToArray()).ToList();
        }

        // denormalizing a vector to have a nearly precise integer representation

        public static int[] Denormalize(double[] vect)
        {
            short precision = 6;
            vect = Normalize(vect);
            int[] denormalizedVector = new int[vect.Length];

            for(int i =0; i<vect.Length; i++)
            {
                // we take the first 6 digits of the number, and we multiply it by 10^6 (precision = 6 by default)
                denormalizedVector[i] = (int)(Math.Round(vect[i], precision) * Math.Pow(10, precision));

            }

            int greatestDivisor = denormalizedVector[0];

            for(int i=1; i< vect.Length; i++)
            {
                greatestDivisor = GCD(denormalizedVector[i], greatestDivisor);
            }

            double[] denormalizedVectorDouble = Array.ConvertAll<int, double>(denormalizedVector, x => x);

            double norm = VectorHelpers.Norm(denormalizedVectorDouble);

            if(norm <= Math.Pow(10, 4)) // if the values are small enough, adjustments will cause problems
            { // so we just return the denormalized vector divided by its GCD
                for(int i=0; i<denormalizedVector.Length; i++)
                {
                    denormalizedVector[i] /= greatestDivisor;
                }

                return denormalizedVector;
            }

            // we find a series of adjustments to the numbers that give them better GCDs, while preserving
            // an accurate enough precision
            //
            // e.g. 3999 and 8000 have gcd = 1, but with a +1, it becomes 4000 and 8000 => gcd = 4000.
            //
            // worst case scenario, this function finds a GCD of 3, which is roughly 1~1.5 bits saved

            Tuple<int, int[]>[] T1arr = AlterVectorAndGetGCD(denormalizedVector, -1, 0);
            Tuple<int, int[]>[] T2arr = AlterVectorAndGetGCD(denormalizedVector,  0, 0);
            Tuple<int, int[]>[] T3arr = AlterVectorAndGetGCD(denormalizedVector,  1, 0);

            int[] adjustments = new int[vect.Length];

            for (int i = 0; i < T1arr.Length; i++)
            {
                Tuple<int, int[]> T1 = T1arr[i];
                Tuple<int, int[]> T2 = T2arr[i];
                Tuple<int, int[]> T3 = T3arr[i];

                // we iterate all the list to find the best adjustments that result in the greatest GCD

                if(T1.Item1 > greatestDivisor)
                {
                    greatestDivisor = T1.Item1;
                    adjustments = T1.Item2;
                }
                if (T2.Item1 > greatestDivisor)
                {
                    greatestDivisor = T2.Item1;
                    adjustments = T2.Item2;
                }
                if (T3.Item1 > greatestDivisor)
                {
                    greatestDivisor = T3.Item1;
                    adjustments = T3.Item2;
                }
            }

            // after finding the good adjustments, we adjust the values of our vector, and divide by the GCD

            for(int i=0; i< vect.Length; i++)
            {
                denormalizedVector[i] += adjustments[i];
                denormalizedVector[i] /= greatestDivisor;
            }


            return denormalizedVector;  

        }

        // this is used on a de-normalized vector to return a GCD, and slight offsets to the original coords 
        // to result in the biggest GCD while reducing the precision loss as much as possible
        //
        // an example case of this method is 3999 and 8000 => gcd = 1
        // but with +1, it becomes 4000 and 8000 => gcd = 4000 (optimistic scenario)

        // the result is represented as a tuple: { gcd, adjustments to get that gcd} 
        // meaning the first value is an int, and the second value is an array of value € {-1, 0, +1}

        public static Tuple<int, int[]>[] AlterVectorAndGetGCD(int[] vect, int adjustment, int index=0)
        {
            // if the vector is a single element, that element is the GCD of itself.
            if (index == vect.Length - 1)
            {
                int[] adjustmentList = new int[] { adjustment };
                int gcd = vect[index] + adjustment;
                Tuple<int, int[]> T = new(gcd, adjustmentList);
                return new Tuple<int, int[]>[]{ T };

            }

            // we try all possible combinations of +1, 0, -1
            // this results in always finding a gcd of at least 3
            

            Tuple<int, int[]>[] T1arr = AlterVectorAndGetGCD(vect, -1, index + 1);
            Tuple<int, int[]>[] T2arr = AlterVectorAndGetGCD(vect, 0, index + 1);
            Tuple<int, int[]>[] T3arr = AlterVectorAndGetGCD(vect, 1, index + 1);

            // triple array concatenation
            Tuple<int, int[]>[] TotalTuples = T1arr.Concat(T2arr).ToArray().Concat(T3arr).ToArray();

            int totalLen = T1arr.Length + T2arr.Length + T3arr.Length;

            Tuple<int, int[]>[] resultTupleArray = new Tuple<int, int[]>[totalLen];
            int i = 0;

            // we build our resulting tuple in this way:
            // 1- we iterate all the tuples generated by the recursive calls
            // 2- we check the current GCD with that tuple + the new element (current element)
            // 3- we create a new tuple: { new gcd, new adjustment + old adjustments }
            foreach (Tuple<int, int[]> tuple in TotalTuples)
            {
                int greatestDivisor = tuple.Item1;
                int[] adjustmentArray = tuple.Item2;

                int[] resultingAdjustmentArray = new int[adjustmentArray.Length + 1];
                adjustmentArray.CopyTo(resultingAdjustmentArray, 1);
                resultingAdjustmentArray[0] = adjustment;

                int currentItem = vect[index]+adjustment;

                greatestDivisor = GCD(greatestDivisor, currentItem);

                Tuple<int, int[]> resultingTuple = new Tuple<int, int[]>(greatestDivisor, resultingAdjustmentArray);
                resultTupleArray[i] = resultingTuple;
                i++;
            }

            return resultTupleArray;

        }

        // method to find the greatest common denominator of a number

        public static int GCD(int a, int b) // a > b, if not we flip
        {
            if (b > a)
            {
                return GCD(b, a);
            }
            // GCD doesn't care about the sign of a number
            if (a < 0 || b < 0)
            {
                return GCD(Math.Abs(a), Math.Abs(b));
            }

            if (b == 0) return a;
            if (a == b) return a;
            if (b == 1) return b;

            return GCD(b, a % b);
        }

        // dot product of 2 vects is: x1*x2 + y1*y2 + z1*z2 + ....

        public static double DotProduct(double[] vectA, double[] vectB)
        {
            AssertArraySameLength(vectA, vectB);
            double dot = 0;
            for(int i=0; i < vectA.Length; i++)
            {
                dot += vectA[i] * vectB[i];
            }

            return dot;
        }

        public static double DotProduct(List<double> vectA, List<double> vectB)
        {
            return DotProduct(vectA.ToArray(), vectB.ToArray());
        }


        // this is only implemented for arrays of size 3. cross products of higher dimensions
        // are more complex and require (dimension-1) inputs. So this is just a static function

        public static double[] CrossProduct(double[] vectA, double[] vectB)
        {
            if (vectA.Length != 3) throw GimLogger.Error(new IndexOutOfRangeException("Cross product can only be computed for vectcors of size 3"));
            AssertArraySameLength(vectA, vectB);

            double[] result = new double[3];
            result[0] = vectA[1] * vectB[2] - vectA[2] * vectB[1];
            result[1] = vectA[2] * vectB[0] - vectA[0] * vectB[2];
            result[2] = vectA[0] * vectB[1] - vectA[1] * vectB[0];
            return result;
        }

        public static List<double> CrossProduct(List<double> vectA, List<double> vectB)
        {
            return CrossProduct(vectA.ToArray(), vectB.ToArray()).ToList();
        }

        // Negative of an array

        public static double[] Neg(double[] vectA)
        {
            return vectA.Select((val) => (-val)).ToArray();
        }

        public static List<double> Neg(List<double> vectA)
        {
            return Neg(vectA.ToArray()).ToList();
        }

        // checking if two vectors are the same

        public static bool Compare(double[] vectA, double[] vectB, double margin = 0.000001)
        {
            if (vectA.Length != vectB.Length) return false;
            for(int i=0; i < vectA.Length; i++)
            {
                double diff = Math.Abs(vectA[i] - vectB[i]);
                if (diff > margin * 10)
                {
                    return false;
                }
            }

            return true;

            //double difference = Norm(Subtract(vectA, vectB));
            //return (difference <= margin*10);
        }


        public static bool Compare(List<double> vectA, List<double> vectB, double margin = 0.000001)
        {
            return Compare(vectA.ToArray(), vectB.ToArray(), margin);
        }

        // creating a vector array
        public static double[] ToVectorArray(params double[] values)
        {
            return values;
        }

        // creating a vector list

        public static List<double> ToVectorList(params double[] values)
        {
            return values.ToList();
        }

        // check to see if two vectors have the same size

        public static void AssertArraySameLength(double[] vectA, double[] vectB)
        {
            if (vectA.Length != vectB.Length) throw GimLogger.Error(new IndexOutOfRangeException("The arrays to sum must be the same length"));
        }

        public static void AssertListSameLength(List<double> vectA, List<double> vectB)
        {
            if (vectA.Count != vectB.Count) throw GimLogger.Error(new IndexOutOfRangeException("The arrays to sum must be the same length"));
        }


        // comparing two local doubles
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

            if ((valANormalized == minInt || valANormalized == maxInt)
                && (valBNormalized == minInt || valBNormalized == maxInt))
            {
                return true;
            }

            // this is making sure that stuff like 0.70710606 and 0.70710607 register as the same value

            double difference = Math.Abs(valA - valB);
            // if the difference between these values is less than 10^-7, they are probably just the same value
            return (difference < margin);


        }

        // comparing two doubles
        public static bool CompareDouble(double valA, double valB, double margin = 0.000001)
        {
            // this is making sure that stuff like 0.70710606 and 0.70710607 register as the same value
            double difference = Math.Abs(valA - valB);
            // if the difference between these values is less than 10^-7, they are probably just the same value
            return (difference < margin);
        }

    }

}