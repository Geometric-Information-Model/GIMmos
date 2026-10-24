using GIMcompress;
using System;

namespace GIMtransfer
{
	public class Vector
	{
		private readonly double[] internalArray;

		public double X { get => internalArray[0]; }
		public double Y { get => internalArray[1]; }
		public double Z { get => internalArray[2]; }


		public Vector(double x, double y, double z)
		{
			internalArray = new double[3] { x, y, z };
		}

		public override string ToString()
		{
			return "(" + X + ", " + Y + ", " + Z + ")";
		}

		public Vector(double[] newArray)
		{
			internalArray = newArray;
		}

		/// <summary>
		/// LengthSquared is way faster than length. So if you can just use the square length use this.
		/// </summary>
		/// <returns>|v|^2 The squared length of the vector.</returns>
		public double LengthSquared()
		{
			return Math.Pow(X, 2) + Math.Pow(Y, 2) + Math.Pow(Z, 2);
		}

		/// <summary>
		/// Length is way slower than LengthSquared. So if you can just use the square length use that.
		/// </summary>
		/// <returns>|v| The length of the vector.</returns>
		public double Length()
		{
			return Math.Sqrt(LengthSquared());
		}

		public static Vector operator *(double left, Vector right)
		{
			return new Vector(VectorHelpers.ScaleVector(left, right.internalArray));
		}

		public static Vector operator /(Vector left, double right)
		{
			return new Vector(VectorHelpers.ScaleVector(1 / right, left.internalArray));
		}

		public static Vector operator +(Vector left, Vector right)
		{
			return new Vector(VectorHelpers.Sum(left.internalArray, right.internalArray));
		}


		public static Vector operator -(Vector left, Vector right)
		{
			return new Vector(VectorHelpers.Subtract(left.internalArray, right.internalArray));
		}
	}
}
