using System;
using System.Collections.Generic;
using System.Linq;
using GimUtilities;

namespace GIMcompress
{
	public class Matrix
    {
        public static readonly double EQUALITY_MARGIN = 0.0001; // to account for double imprecision
        public static readonly short EQUALITY_ROUND_PRECISION = 4; // used for ValueRounder
        public static readonly double EQUALITY_ROUND_MARGIN = Math.Pow(10, -EQUALITY_ROUND_PRECISION-2); // used for ValueRounder

        private readonly double[][] values;

        #region properties

        public int Width
        {
            get => values[0].Length;
        }

        public int Height
        {
            get => values.Length;
        }

        public double Determinant
        {
            get => ValueRounder.roundDouble(MatrixHelpers.Determinant(values), EQUALITY_ROUND_MARGIN, EQUALITY_ROUND_PRECISION);
        }

        public Matrix Inverse
        {
            get => (Matrix) MatrixHelpers.Inverse(values);
        }

        // has the added benefit of being able to to call Matrix[i][j] as a getter+setter
        public double[] this[int i]
        {
            get => values[i];
            set => values[i] = (double[])value;
        }

        public double[][] this[Range r]
        {
            get => values[r];
        }
        #endregion

        // constructed from an array of lines or a bunch of line parameters.
        public Matrix(params double[][] lines)
        {
            MatrixHelpers.AssertMatrixIsValid(lines);
            values = new double[lines.Length][];
            for(int i=0; i < lines.Length;i++)
            {
                values[i] = (double[])lines[i].Clone();
            }
       
        }

        // Matrix from vectors
        public Matrix(params Vect[] lines)
        {
            values = new double[lines.Length][];
            for (int i = 0; i < lines.Length; i++)
            {
                // vectors can be cast as double []
                values[i] = (double[])lines[i];
            }

            MatrixHelpers.AssertMatrixIsValid(values);
        }

        public Matrix(IReadOnlyGimLocal local, bool ignoreUnused = true) : this((GimLocal) local, ignoreUnused)
        {
           
        }

        // Matrix from Local. IgnoreUnused = true, signifies ignoring lines equal to int.minValue and taking only the rest.
        public Matrix(GimLocal local, bool ignoreUnused = true)
        {
            if (ignoreUnused)
            {
                int lineIndex = 0;
                List<double[]> lines = new();
                if(local.Xx != int.MaxValue && local.Xx != int.MinValue)
                {
                    lines.Add(new double[] { local.Xx, local.Xy, local.Xz });
                }

                if(local.Yx != int.MaxValue && local.Yx != int.MinValue)
                {
                    lines.Add(new double[] { local.Yx, local.Yy, local.Yz });
                }

                if (local.Zx != int.MaxValue && local.Zx != int.MinValue)
                {
                    lines.Add(new double[] { local.Zx, local.Zy, local.Zz });
                }

                values = lines.ToArray();
                return;
            }
            values = new double[3][];
            values[0] = new double[] { local.Xx, local.Xy, local.Xz };
            values[1] = new double[] { local.Yx, local.Yy, local.Yz };
            values[2] = new double[] { local.Zx, local.Zy, local.Zz };
        }
        public void ApplyToLocal(GimLocal localElement)
        {
            localElement.Xx = this[0][0];
            localElement.Xy = this[0][1];
            localElement.Xz = this[0][2];

            localElement.Yx = this[1][0];
            localElement.Yy = this[1][1];
            localElement.Yz = this[1][2];

            localElement.Zx = this[2][0];
            localElement.Zy = this[2][1];
            localElement.Zz = this[2][2];
        }

        public Tuple<double, double, double> GetRPY()
        {
            double yaw = Math.Atan2(values[1][0], values[0][0]);
            double pitch = Math.Atan2(-values[2][0], Math.Sqrt(values[2][1] * values[2][1] + values[2][2] * values[2][2]));
            double roll = Math.Atan2(values[2][1], values[2][2]);

            if (yaw == 0) yaw = 0;
            if (pitch == 0) pitch = 0;
            if (roll == 0) roll = 0;

            if (VectorHelpers.CompareDouble(Math.Abs(pitch), Math.PI / 2))
            // pitch is maximal/minimal, in this case yaw and roll degenerate into being the same angle
            // therefore we set roll to be zero, and simply just take the value of the yaw
            {
                double sign = Math.Sin(pitch);

                yaw = Math.Atan2(sign * values[1][2], values[1][1]);
                roll = 0;
            }

            return new(roll, pitch, yaw);
        }

        public static Matrix FromRPY(double r, double p, double y) 
        {
            Matrix matrix = new Matrix(new Vect(0,0,0), new Vect(0, 0, 0), new Vect(0, 0, 0));

            matrix[0][0] = (Math.Cos(y) * Math.Cos(p));
            matrix[0][1] = (Math.Cos(y) * Math.Sin(p) * Math.Sin(r) - Math.Sin(y) * Math.Cos(r));
            matrix[0][2] = (Math.Cos(y) * Math.Sin(p) * Math.Cos(r) + Math.Sin(y) * Math.Sin(r));
            matrix[1][0] = Math.Sin(y) * Math.Cos(p);
            matrix[1][1] = Math.Sin(y) * Math.Sin(p) * Math.Sin(r) + Math.Cos(y) * Math.Cos(r);
            matrix[1][2] = Math.Sin(y) * Math.Sin(p) * Math.Cos(r) - Math.Cos(y) * Math.Sin(r);
            matrix[2][0] = -Math.Sin(p);
            matrix[2][1] = Math.Cos(p) * Math.Sin(r);
            matrix[2][2] = Math.Cos(p) * Math.Cos(r);

            return matrix;
        }


        public override string ToString()
        {
            string s = "{ ";
            for (int i = 0; i < Height; i++)
            {
                s += "[";
                for (int j = 0; j < Width; j++)
                {
                    s += Math.Round(values[i][j], EQUALITY_ROUND_PRECISION);
                    if (j == Width - 1) break;
                    s += ", ";
                }
                s += "]";

                if (i == Width - 1) break;
                s += ", ";
            }
            s += " }";

            return s;
        }

        // sum
        public static Matrix operator+(Matrix A, Matrix B)
        {
            return new Matrix(MatrixHelpers.Sum(A.values, B.values));
        }

        // diff
        public static Matrix operator-(Matrix A, Matrix B)
        {
            return new Matrix(MatrixHelpers.Subtract(A.values, B.values));
        }

        // multiplication: not commutative! (order matters)
        public static Matrix operator*(Matrix A, Matrix B)
        {
            return new Matrix(MatrixHelpers.Multiply(A.values, B.values));
        }

        // negative of a matrix
        public static Matrix operator-(Matrix A)
        {
            return new Matrix(MatrixHelpers.Neg(A.values));
        }

        // real * Matrix = Matrix
        public static Matrix operator*(double x, Matrix A)
        {
            return new Matrix(MatrixHelpers.Scale(x, A.values));
        }

        // Vector * Matrix = Vector (we assume the vector is oriented like a line, i.e. the vector is a one row matrix)
        public static Vect operator*(Vect v, Matrix A)
        {
            return (Vect) ((Matrix)v * A);
        }

        // Matrix * Vector = Matrix: we apply the matrix A to the vector V to transform it in space, the vector is vertical
        // n*m x m*1 => n*1
        public static Vect operator*(Matrix A, Vect v)
        {
            double[][] vectValues = new double[v.Length][];
            for(int i=0; i<v.Length; i++)
            {
                vectValues[i] = new double[] { v[i]};
            }
            Matrix vectMatrix = new Matrix(vectValues);

            Matrix result = A * vectMatrix;

            double[] resultVectValues = new double[A.Height];
            for (int i = 0; i< A.Height; i++){
                resultVectValues[i] = result[i][0];
            }

            return (Vect)resultVectValues;
        }

        // Matrix/real = Matrix
        public static Matrix operator/(Matrix A, double x)
        {
            return new Matrix(MatrixHelpers.Scale(1/x, A.values));
        }

        // real/Matrix = real * Matrix^(-1) = real * inverse
        public static Matrix operator/(double x, Matrix A)
        {
            return new Matrix(MatrixHelpers.Scale(x, A.Inverse.values));
        }

        // comparison operators

        public override bool Equals(object obj)
        {
            if(! (obj is Matrix))
            {
                return false;
            }
            Matrix A = this;
            Matrix B = (Matrix)obj;
            if (A is null && B is null) return true;
            if (A is null || B is null) return false;
            return MatrixHelpers.Compare(A.values, B.values, EQUALITY_MARGIN);
        }

        public static bool operator==(Matrix A, Matrix B)
        {   
            if (A is null && B is null) return true;
            if (A is null || B is null) return false;
            return MatrixHelpers.Compare(A.values, B.values, EQUALITY_MARGIN);
        }
        public static bool operator!=(Matrix A, Matrix B)
        {
            return !MatrixHelpers.Compare(A.values, B.values, EQUALITY_MARGIN);
        }
        // conversion to and from double[][]

        public static explicit operator double[][](Matrix A)
        {
            double[][] result = new double[A.values.Length][];
            for(int i=0; i< A.values.Length; i++)
            {
                result[i] = (double[])A[i].Clone();
            }
            return result;
        }
        public static explicit operator Matrix(double[][] values) => new Matrix(values);

        // conversion to and from Vector

        public static explicit operator Vect(Matrix A)
        {
            if (A.Height > 1) throw new InvalidCastException("Cannot convert Matrix of Size " + A.Height + "x" + A.Width +
                   " To vector: only matrices of size 1xn can be converted!");

            return (Vect)A.values[0];
        }

        public static explicit operator Matrix(Vect V)
        {
            return new Matrix(V);
        }

		public override int GetHashCode()
		{
            unchecked // Overflow is fine, just wrap
            {
                int hash = 12;
                for (int i = 0; i < values.Length; i++)
                {
                    for (int j = 0; j < values[i].Length; j++)
                        hash ^= values[i][j].GetHashCode();

                    hash = hash << 7;
                }
                return hash;
            }
        }

        public static Matrix Ge3DUnitMatrix()
        {
            return new Matrix(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
        }
	}
    [Serializable]
    public class Vect
    {

        public static readonly double EQUALITY_MARGIN = 0.00001; // to account for double imprecision

        private readonly double[] values;
        #region properties
        public double X
        {
            get => values[0];
        }

        public double Y
        {
            get => values[1];
        }

        public double Z
        {
            get
            {
                AssertDimension(3, "Z component access");
                return values[2];
            }
        }

        public bool Is2D => values.Length == 2;

        public bool Is3D => values.Length == 3;

        /// <summary>
        /// WARNING: this is NOT the length of the line, this is the amount of coordinates is in the vect.
        /// </summary>
        public int Length
        {
            get => values.Length;
        }

        private void AssertDimension(int required, string operation)
        {
            if (values.Length < required)
                throw new InvalidGimOperation($"{operation} requires a {required}D vector but got {values.Length}D.");
        }

        public double this[int i]
        {
            get => values[i];
            set => values[i] = value;
        }

        public double[] this[Range r]
        {
            get => values[r];
        }
        // returns the norm of the vector
        public double Norm
        {
            get => ValueRounder.roundDouble(VectorHelpers.Norm(values), Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
        }

        // returns the unitary vector with the direction of the current vector
        public Vect Direction
        {
            get => new Vect(VectorHelpers.Normalize(values));
        }

        #endregion
        // construct from multiple Parameters or single Array
        public Vect(params double[] values)
        {
            this.values = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                this.values[i] = values[i];
            }
        }

        // construction from a local matrix. index = 0 for line 0 = X, 1 for line 1 = Y, 2 for line 2 = Z
        // defaults to index = 2 because this is mostly used for lineLocals, which only populate the Z value
        public Vect(GimLocal local, int index=2)
        {
            switch (index)
            {
                case 0: values = VectorHelpers.ToVectorArray(local.Xx, local.Xy, local.Xz);break;
                case 1: values = VectorHelpers.ToVectorArray(local.Yx, local.Yy, local.Yz);break;
                case 2: values = VectorHelpers.ToVectorArray(local.Zx, local.Zy, local.Zz);break;
                default: values = VectorHelpers.ToVectorArray(local.Zx, local.Zy, local.Zz);break;
            }
        }

        public void ApplyToLocal(GimLocal localElement)
        {
            localElement.Xx = int.MinValue;
            localElement.Xy = int.MinValue;
            localElement.Xz = int.MinValue;

            localElement.Yx = int.MinValue;
            localElement.Yy = int.MinValue;
            localElement.Yz = int.MinValue;

            if (Length == 2)
            {
                localElement.Zx = this[0];
                localElement.Zy = this[1];
                localElement.Zz = 0;
            }
            else
            {
                localElement.Zx = this[0];
                localElement.Zy = this[1];
                localElement.Zz = this[2];
            }
        }

        public void ApplyAsDirectionU(GimLocal localElement)
        {
            AssertDimension(2, "ApplyAsDirectionU");
            localElement.Xx = this[0];
            localElement.Xy = this[1];
            localElement.Xz = 0;
            localElement.Zx = int.MinValue;
            localElement.Zy = int.MinValue;
            localElement.Zz = int.MinValue;
        }

        public void ApplyAsDirectionV(GimLocal localElement)
        {
            AssertDimension(2, "ApplyAsDirectionV");
            localElement.Yx = this[0];
            localElement.Yy = this[1];
            localElement.Yz = 0;
            localElement.Zx = int.MinValue;
            localElement.Zy = int.MinValue;
            localElement.Zz = int.MinValue;
        }

        public static Vect FromLocalOrigin2D(GimLocal local)
        {
            return new Vect(local.Px, local.Py);
        }

        public static Vect FromLocalDirectionU(GimLocal local)
        {
            return new Vect(local.Xx, local.Xy);
        }

        public static Vect FromLocalDirectionV(GimLocal local)
        {
            return new Vect(local.Yx, local.Yy);
        }

        public static Vect FromLocalDirection(GimLocal local)
        {
            return new Vect(local.Zx, local.Zy);
        }
        
        public void SetLocalBasis(GimLocal localElement)
        {
            localElement.Px = this[0];
            localElement.Py = this[1];
            localElement.Pz = Length == 2 ? 0 : this[2];
        }

        public Vect Concat(Vect v) // concatenating two vectors (a,b,c).concat(x,y,z) => (a,b,c,x,y,z);
        {
            int total = Length + v.Length;
            double[] result = new double[total];
            for(int i=0; i<Length; i++)
            {
                result[i] = this[i];
            }
            for(int i=0; i<v.Length; i++)
            {
                result[i + Length] = v[i];
            }
            return new Vect(result);
        }

        // rounds the vector's imprecisions
        public Vect Round() 
        {
            Vect res = new Vect(values);
            for(int i=0; i<res.Length; i++)
            {
                res[i] = ValueRounder.roundDouble(res[i], Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            }
            return res;
        }

        // rounds the vector to a certain degree of precision

        public Vect Round(int prec)
        {
            Vect res = new Vect(values);
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = Math.Round(res[i], prec);
            }
            return res;
        }

        // checking for collinearity of two vectors i.e. true if same direction/ opposite, false if otherwise

        public bool IsCollinearTo(Vect v)
        {
            Vect A = this;
            Vect B = v;

            // without cross product, testing if A[i] / B[i] is constant
            if (A.Length != 3 && A.Length == B.Length)
            {
                if (B.Norm == 0) return true;
                double constant = 0;
                for(int i = 0; i < A.Length; i++)
                {
                    if (B[i] != 0)
                    {
                        constant = A[i] / B[i];
                        break;
                    }
                    
                }

                for (int i = 0; i < A.Length; i++)
                {
                    // different to our constant
                    if ( !VectorHelpers.CompareDouble(A[i], B[i] * constant)) return false;
                }
                // all A[i] = B[i] * constant;
                return true;
            }



            // normalizing because of imprecisions
            if (Norm > 1) A = Direction;
            if (v.Norm > 1) B = v.Direction;
            return ((A ^ B).Norm <= EQUALITY_MARGIN); // collinearity is checked with the cross product
        }

        // returns two vectors of the plane perpendicular to this vector. returned in the form of a matrix:
        // vect1
        // vect2
        // vect1^vect2 = this

        public Matrix GetNormalPlane()
        {
            AssertDimension(3, "GetNormalPlane");
            // null vector.
            if (Norm <= EQUALITY_MARGIN) return new Matrix(new Vect(0, 0, 0), new Vect(0, 0, 0), new Vect(0, 0, 0));
            Matrix result;

            Vect vect1 = new Vect(1, 0, 0) ^ this;
            if(vect1.Norm <= EQUALITY_MARGIN)
            {
                vect1 = new Vect(0, 1, 0) ^ this;
            }
            vect1 = vect1.Direction;

            Vect vect2 = this ^ vect1;
            vect2 = vect2.Direction;

            result = new Matrix(vect1, vect2, this);

            return result;
        }

        // returns the center of the circle that passes through 3 points

        public static Vect GetCircleCenter(Vect point1, Vect point2, Vect point3)
        {
            Vect bisector1Normal = (point2 - point1).Direction;
            Vect bisector2Normal = (point2 - point3).Direction;

            Vect planeNormal = (bisector1Normal ^ bisector2Normal).Direction;

            Vect pointCenter = null;

            double radius = 0;

            if (planeNormal.Norm == 0) // the points are all aligned => circle is on the center of AB and.
            {
                throw GimLogger.Error(new ArgumentException("points are linear, cannot extrapolate circle"));
            }
            else // points are not aligned, we have to find the center of the three points
            {
                Vect center1 = (point2 + point1) / 2;
                Vect center2 = (point2 + point3) / 2;

                // we attempt to find the plane bissectors, that cut the lines A-C and B-C in half. The circle lies on
                // their intersection.

                // if each plane is characterized by a normal vector (a0,b0,c0) and a point (x0,y0,z0) then its equation is

                // a0(x-x0) + b0(y-y0) + c0(z-z0) = 0

                // if we assume our point P = (x,y,z) is the center of the circle, then it belong to all three planes.
                // it then verifies the equation:
                //

                // a1(x-x1) + b1(y-y1) + c1(z-y1) = 0       a1*x + b1*y + c1*z = a1*x1 + b1*y1 + c1*z1
                // a2(x-x2) + b2(y-y2) + c2(z-z2) = 0  =>   a2*x + b2*y + c2*z = a2*x1 + b2*y1 + c2*z1
                // a3(x-x3) + b3(y-y3) + c3(z-z3) = 0       a3*x + b3*y + c3*z = a3*x1 + b3*y1 + c3*z1   

                // that resulting equation is of the form Matrix * Point = Point, so to find P = XYZ
                // we have to do Point = Matrix^(-1) * Vector

                Matrix intersectionMatrix = new Matrix(planeNormal, bisector1Normal, bisector2Normal);
                Vect offsetVect = new Vect(
                    planeNormal * point2,
                    bisector1Normal * center1,
                    bisector2Normal * center2);


                if (intersectionMatrix.Determinant == 0)
                {
                    throw GimLogger.Error(new ArgumentException("Illegal point coordinates in circle, are you sure this is a circle?"));
                }
                Matrix intersectionInverse = intersectionMatrix.Inverse;

                pointCenter = intersectionInverse * offsetVect;
                pointCenter = pointCenter.Round();

                radius = (point1 - pointCenter).Norm;
                return pointCenter;
            }
        }

        /// <summary>
        /// Checks if a group of points lies on a plane
        /// </summary>
        /// <param name="points">the points to check</param>
        /// <returns></returns>
        public static bool PointsAreCoPlanar(IEnumerable<Vect> points)
        {
            if (points.Count() <= 3) return true;

            Vect basePoint = points.ElementAt(0);
            Vect secondPoint = points.ElementAt(1);
            Vect directionA = secondPoint - basePoint;

            int i;
            for (i = 1; i < points.Count(); i++)
            {
                secondPoint = points.ElementAt(i); 
                directionA = secondPoint - basePoint;
                if (directionA.Norm > Matrix.EQUALITY_MARGIN)
                    break;
            }

            if (directionA.Norm < Matrix.EQUALITY_MARGIN || i >= points.Count())
                return true;

            Vect thirdPoint = points.ElementAt(i);
            Vect directionB = thirdPoint - basePoint;
            Vect norm = directionA ^ directionB;

            for(; i < points.Count(); i++)
            {
                thirdPoint = points.ElementAt(i);
                directionB = thirdPoint - basePoint; 
                norm = directionA ^ directionB;
                if (norm.Norm > Matrix.EQUALITY_MARGIN)
                    break;
            }

            if (norm.Norm < Matrix.EQUALITY_MARGIN)
                return true;

            for(int index = 0; index < points.Count(); index++)
            {
                Vect point = points.ElementAt(index);
                Vect offsetVect = point - basePoint;

                double offset = offsetVect * norm;
                if (offset > EQUALITY_MARGIN)
                    return false;
            }
            return true;
        }

        public override bool Equals(Object B)
        {
            return this == (Vect)B;
        }


        // hash code, used mostly for triangles so values are truncated to int.

        public override int GetHashCode()
        {
            int sum = 0;

            for(int i=0; i < values.Length; i++)
            {
                sum += values[i].GetHashCode();
            }

            return sum;
        }

        // (a, b, c) formatting
        public override string ToString()
        {
            string s = "(";
            for(int i=0; i<values.Length; i++)
            {
                s += Math.Round(values[i], Matrix.EQUALITY_ROUND_PRECISION);
                if(i!= values.Length - 1)
                {
                    s += ", ";
                }
            }
            s += ")";

            return s;
        }


        // returns the average of an Arraylist of points

        public static Vect GetAveragePoint(List<Vect> points)
        {
            if (points.Count == 0) return new Vect(0, 0, 0); // assumed length is 3
            Vect average = GetNullVector(points[0].Length);

            foreach(Vect point in points)
            {
                average += point;
            }
            average /= points.Count;
            return average;
        }

        // returns the average of an Array of points

        public static Vect GetAveragePoint(Vect[] points)
        {
            if (points.Length == 0) return new Vect(0, 0, 0); // assumed length is 3
            Vect average = GetNullVector(points[0].Length);

            foreach (Vect point in points)
            {
                average += point;
            }
            average /= points.Length;
            return average;
        }


        // constructs a null vector of size N

        public static Vect GetNullVector(int dimension = 3)
        {
            double[] resultArray = new double[dimension];
            return new Vect(resultArray);
        }

        // constructs a unit vector of size N and with a 1 in position P
        public static Vect GetUnitVector(int dimension = 3, int position = 0)
        {
            Vect unit = GetNullVector(dimension);
            unit[position] = 1;
            return unit;
        }

        // vector addition
        public static Vect operator+(Vect A, Vect B)
        {
            return new Vect(VectorHelpers.Sum(A.values, B.values));
        }

        // vector subtraction
        public static Vect operator-(Vect A, Vect B)
        {
            return new Vect(VectorHelpers.Subtract(A.values, B.values));
        }

        // vector Cross Product: A x B = Vector
        public static Vect operator^(Vect A, Vect B)
        {
            A.AssertDimension(3, "Cross product");
            B.AssertDimension(3, "Cross product");
            return new Vect(VectorHelpers.CrossProduct(A.values, B.values));
        }

        // vector Dot product: A*B = real
        public static double operator*(Vect A, Vect B)
        {
            return VectorHelpers.DotProduct(A.values, B.values);
        }

        // scalar x vector:  xV = vector
        public static Vect operator*(double x, Vect A)
        {
            return new Vect(VectorHelpers.ScaleVector(x, A.values));
        }

        // vector/scalar: V/x = vector. Optional, could be represented as 1/x * V, but simplifies and prettifies the code
        
        public static Vect operator/(Vect A, double x)
        {
            return (1 / x) * A;
        }

        // negative of a vector
        public static Vect operator-(Vect A)
        {
            return new Vect(VectorHelpers.Neg(A.values));
        }

        // +A, redefined just in case
        public static Vect operator+(Vect A)
        {
            return new Vect(VectorHelpers.ScaleVector(1, A.values));
        }

        // vector comparison


        public static bool operator==(Vect A, Vect B)
        {
            if (A is null && B is null) return true;
            if (A is null || B is null) return false;
            return VectorHelpers.Compare(A.values, B.values, EQUALITY_MARGIN);
        }
        
        // obligatory definition after defining ==
        public static bool operator !=(Vect A, Vect B)
        {
            if (A is null && B is null) return false;
            if (A is null || B is null) return true;
            return !VectorHelpers.Compare(A.values, B.values, EQUALITY_MARGIN);
        }

        // casting vect to double[] and vice versa
        public static explicit operator double[](Vect V) => (double[])V.values.Clone();
        public static implicit operator Vect(double[] values) => new Vect(values);

    }


}