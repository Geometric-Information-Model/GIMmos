using System;
using System.Collections.Generic;

namespace GIMtransfer.BrepToPoly
{
	public class Vertex
	{
		public double x, y, z;

		// TODO: fix magic number programaticly
		public const double maximumDelta = 1.0E-10;

		public Vertex(double x, double y, double z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public Vertex(Vector vector)
		{
			x = vector.X;
			y = vector.Y;
			z = vector.Z;
		}

		public Vertex(double[] array)
		{
			x = array[0];
			y = array[1];
			z = array[2];
		}

		public Vector ToVector()
		{
			return new Vector(x, y, z);
		}

		public override string ToString()
		{
			return "(" + x + ", " + y + ", " + z + ")";
		}

		public bool Equals(Vertex other)
		{
            return AreProbablySame(x, other.x) && AreProbablySame(y, other.y) && AreProbablySame(z, other.z);
		}

        public static bool AreProbablySame(double expected, double actual, double deltaFraction = 0.01)
        {
            if (Math.Abs(expected) > Math.Abs(actual))
                return Math.Abs(expected - actual) < Math.Abs(deltaFraction * expected);
            else
                return Math.Abs(expected - actual) < Math.Abs(deltaFraction * actual);
        }

		public static string GenerateStringRepresentation(Vertex vertex)
		{
			return vertex.x + " " + vertex.y + " " + vertex.z;
		}

		public static string GenerateStringRepresentation(List<Vertex> vertices)
		{
			string lineOutput = "";
			foreach (Vertex vertex in vertices)
				lineOutput += GenerateStringRepresentation(vertex) + " ";
			return lineOutput;
		}
    }
}
