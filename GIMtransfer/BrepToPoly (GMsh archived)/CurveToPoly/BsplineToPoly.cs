using GIMcompress;
using System.Collections.Generic;
using GimUtilities;

namespace GIMtransfer.BrepToPoly
{
	public static partial class BrepToPoly
	{
		private static List<Vertex> CurveBsplineToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			GimLogger.Warn("Local position is not implemented");
			// https://en.wikipedia.org/wiki/Non-uniform_rational_B-spline
			//Assert.AreEqual(element.ElementType, GimElementType.C_bspline);

			// Read specials (set location)
			int degree = element.ElementInts[^1];
			int knotsCount = element.ElementInts[0];
			int multiplicitiesCount = element.ElementInts.Count - 2;
			int polesCount = (element.ElementDoubles.Count - 6 - knotsCount) / 4;


			// Read all poles
			double[] poles = MakePoles(polesCount, element.ElementDoubles, 6);

			// Read all ints
			double[] knots = new double[knotsCount];
			for (int i = 0; i < knotsCount; i++)
			{
				knots[i] = element.ElementDoubles[i + polesCount * 3 + 6];
			}

			double[] weights = new double[polesCount];
			for (int i = 0; i < polesCount; i++)
			{
				weights[i] = element.ElementDoubles[i + polesCount * 3 + 6 + knotsCount];
			}

			int[] multiplicities = new int[multiplicitiesCount];
			for (int i = 0; i < multiplicitiesCount; i++)
			{
				multiplicities[i] = element.ElementInts[i + 1];
			}

			//Assert.IsTrue(polesCount == multiplicities.Sum() - multiplicities[0] || polesCount == multiplicities.Sum() - degree - 1);

			// It follows that the values in the knot vector should be in nondecreasing order, so (0, 0, 1, 2, 3, 3) is valid while (0, 0, 2, 1, 3, 3) is not.
			//for (int i = 1; i < knotsCount; i++)
			//{
			//	Assert.IsTrue(knots[i - 1] <= knots[i]);
			//}

			// theNumberOfPoles defines the length of thePoles and must be >= 2;
			//Assert.IsTrue(polesCount >= 2);

			// theNumberOfKnots defines the length of theKnots and theMultiplicities and must be >= 2;
			//Assert.IsTrue(multiplicitiesCount >= 2);
			//Assert.IsTrue(knotsCount >= 2);
			//Assert.AreEqual(multiplicitiesCount, knotsCount);

			// theDegree must belong to range[1, 25];
			//Assert.IsTrue(degree <= 25);
			//Assert.IsTrue(degree >= 1);

			// The number of poles must equal the number of weights.
			//Assert.AreEqual(polesCount, weights.Length);

			double[] knotsAndExtension = new double[degree + knots.Length + degree];
			for (int i = 0; i < knotsAndExtension.Length; i++)
			{
				if (i < degree)
					knotsAndExtension[i] = knots[0];
				else if (i < degree + knots.Length)
					knotsAndExtension[i] = knots[i - degree];
				else
					knotsAndExtension[i] = knots[^1];
			}

			double[] N = new double[knots.Length];
			for (int i = 0; i < knots.Length - 1; i++)
			{
				N[i] = InternalFunction(i + degree, degree, knotsAndExtension, knots[i]); 
			}

			Vertex firstPoint = new Vertex(element.ElementDoubles[0], element.ElementDoubles[1], element.ElementDoubles[2]);
			Vertex LastPoint = new Vertex(element.ElementDoubles[3], element.ElementDoubles[4], element.ElementDoubles[5]);

			//for (int i = 1; i < knotsCount; i++)
			//{
			//	Assert.IsTrue(N[i - 1] <= N[i]);
			//}

			// TODO: Use actual internal function.
			List<Vertex> vertices = new List<Vertex>(polesCount + 2)
			{
				firstPoint
			};
			for (int i = 0; i < polesCount; i += 3)
			{
				vertices.Add(new Vertex(poles[i], poles[i + 1], poles[i + 2]));
			}
			vertices.Add(LastPoint);

			return vertices;
		}

		private static List<Vertex> CurveBspline2DToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			GimLogger.Warn("Local position is not implemented");
			// Read specials (set location)
			int degree = element.ElementInts[^1];
			int knotsCount = element.ElementInts[0];
			int multiplicitiesCount = element.ElementInts.Count - 2;
			int polesCount = (element.ElementDoubles.Count - 6 - knotsCount) / 4;

			// Read all poles
			// THIS IS WRONG
			// double[] poles = MakePoles(polesCount, element.ElementDoubles, 6);

			Vertex firstPoint = new Vertex(element.ElementDoubles[0], element.ElementDoubles[1], element.ElementDoubles[2]);
			Vertex LastPoint = new Vertex(element.ElementDoubles[3], element.ElementDoubles[4], element.ElementDoubles[5]);
			List<Vertex> vertices = new List<Vertex>(polesCount + 2)
			{
				firstPoint,
				// THIS IS WRONG
				//for (int i = 0; i < polesCount; i++)
				//{
				//	vertices.Add(new Vertex(poles[i], poles[i + 1], poles[i + 2]));
				//}
				LastPoint
			};
			return vertices;
		}

		// https://mathworld.wolfram.com/B-Spline.html
		private static double InternalFunction(int i, int j, double[] t, double current_t)
		{
			if (j == 0)
			{
				if (t[i] <= current_t && current_t < t[i + 1] && t[i] < t[i + 1])
					return 1f;
				else
					return 0.0f;
			}
			else
			{
				double a, b;

				double atop = (current_t - t[i]);
				double abottom = (t[i + j] - t[i]);
				if (atop == 0 && abottom == 0)
					a = 0;
				else
					a = atop / abottom * InternalFunction(i, j - 1, t, current_t);

				double btop = (t[i + j + 1] - current_t);
				double bbottom = (t[i + j + 1] - t[i + 1]);
				if (btop == 0 && bbottom == 0)
					b = 0;
				else
					b = btop / bbottom * InternalFunction(i + 1, j - 1, t, current_t);

				double retval = a + b;

				return retval;
			}
		}

		private static double[] MakePoles(int polesCount, IReadOnlyList<double> doubles, int offset = 0)
		{
			double[] poles = new double[polesCount * 3];

			for (int i = 0; i < polesCount * 3; i += 3)
			{
				poles[i] = doubles[offset + i];
				poles[i + 1] = doubles[offset + i + 1];
				poles[i + 2] = doubles[offset + i + 2];
			}
			return poles;
		}
	}
}
