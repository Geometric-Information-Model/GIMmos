using GIMcompress;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using GimUtilities;

namespace TestHelpers
{
	/// <summary>
	/// Static class that holds the correctness check functions.
	/// </summary>
	public static class CorrectnessCheck
	{
		/// <summary>
		/// Checks if all assumption we do with GimDatas hold.
		/// We assume that the first element of a GimData is a root element.
		/// And we check the assumption we do for Objects and Elements.
		/// </summary>
		/// <param name="gimData">The GimData to check these assumption on.</param>
		/// <returns></returns>
		public static bool CheckCorrectnessGimData(IReadOnlyGimData gimData)
		{
			ClassicAssert.IsTrue(gimData.Elements[0].ElementType == GimElementType.D_root);
			ClassicAssert.IsTrue(CheckCorrectnessObjects(gimData));
			ClassicAssert.IsTrue(CheckCorrectnessElements(gimData));
			ClassicAssert.IsTrue(CheckCorrectnessPropertySets(gimData));
			return true;
		}

		public static object CheckCorrectnessGimData(object input)
		{
			IReadOnlyGimData gimData = (IReadOnlyGimData)input;
			return CheckCorrectnessGimData(gimData);
		}

		/// <summary>
		/// Checks if all assumption we do with GimObjects hold.
		/// We assume that every element of a GimObject is in the GimData.
		/// </summary>
		/// <param name="gimData">The gimData to check the objects for.</param>
		/// <returns></returns>
		public static bool CheckCorrectnessObjects(IReadOnlyGimData gimData)
		{
			foreach (IReadOnlyGimObject gimObject in gimData.Objects)
			{
				ClassicAssert.IsTrue(gimObject.Element < gimData.Elements.Count);
				ClassicAssert.IsTrue(!gimObject.PropertySetIndex.HasValue
					|| (gimObject.PropertySetIndex.Value >= 0 && gimObject.PropertySetIndex.Value < gimData.PropertySets.Count));
			}
			return true;
		}

		/// <summary>
		/// Checks that every property set has non-empty keys and values matching their declared kind.
		/// </summary>
		public static bool CheckCorrectnessPropertySets(IReadOnlyGimData gimData)
		{
			foreach (IReadOnlyGimPropertySet propertySet in gimData.PropertySets)
			{
				foreach (KeyValuePair<string, GimPropertyValue> entry in propertySet.Entries)
				{
					ClassicAssert.IsFalse(string.IsNullOrEmpty(entry.Key));
					switch (entry.Value.Kind)
					{
						case GimPropertyKind.String:
							ClassicAssert.IsNotNull(entry.Value.StringValue);
							break;
						case GimPropertyKind.Double:
							ClassicAssert.IsTrue(double.IsFinite(entry.Value.DoubleValue));
							break;
						default:
							ClassicAssert.Fail($"Unsupported property kind: {entry.Value.Kind}");
							break;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Check if all assumption we do with GimElements hold.
		/// We check the correctness for every individual element with CheckCorrectnessElement(IReadOnlyGimElement gimElement).
		/// And we assert that every child has as many parents as elements it is a child off.
		/// </summary>
		/// <param name="gimData"></param>
		/// <returns></returns>
		public static bool CheckCorrectnessElements(IReadOnlyGimData gimData)
		{
			for (int i = 0; i < gimData.Elements.Count; i++)
			{
				ClassicAssert.Multiple(() =>
				{
					ClassicAssert.IsTrue(CheckCorrectnessElement(gimData.Elements[i]));

					/// We check for the element that every child has them as many times as parent as we have them as a child.
					foreach (uint child in gimData.Elements[i].Elements)
					{
						ClassicAssert.IsNotNull(gimData.Elements[(int)child].Locals.First((local) => local.Parent == i));
						int sameChildsCount = gimData.Elements[i].Elements.Count((elem) => elem == child);
						int childWithThisParent = gimData.Elements[(int)child].Locals.Count((local) => local.Parent == i);

						if (sameChildsCount != childWithThisParent)
						{
							GimLogger.Warn(gimData.Elements[i].ElementType + " has " + sameChildsCount + " Children");
                            GimLogger.Warn(gimData.Elements[(int)child].ElementType + " has " + childWithThisParent + " times this as parent.");
							ClassicAssert.AreEqual(sameChildsCount, childWithThisParent);

						}
					}
				});
			}
			return true;
		}

		/// <summary>
		/// Checks the corectness of a GimElement.
		/// We check if every element has the correct amount of doubles and ints.
		/// </summary>
		/// <param name="gimElement"></param>
		/// <returns></returns>
		public static bool CheckCorrectnessElement(IReadOnlyGimElement gimElement)
		{
			ClassicAssert.AreNotEqual(gimElement.ElementType, GimElementType.undefined);

			if (gimElement.ElementType == GimElementType.C_bspline)
			{
				ClassicAssert.IsTrue(CheckCorrectnessElement_C_bspline(gimElement));
			}

			CheckCorrectnessDoubles(gimElement);
			CheckCorrectnessInts(gimElement);

			return true;
		}

		public static bool CheckCorrectnessInts(IReadOnlyGimElement gimElement)
		{

			int gimElmentExpectedIntCount = gimElement.ElementType.GetIntCount();
			if (gimElmentExpectedIntCount != -1)
			{
				if (gimElmentExpectedIntCount == 0)
				{
					if (gimElement.ElementInts != null)
						ClassicAssert.AreEqual(gimElement.ElementInts.Count, gimElmentExpectedIntCount);
				}
				else
				{
					ClassicAssert.AreEqual(gimElement.ElementInts.Count, gimElmentExpectedIntCount);
				}
			}
			else
			{
				ClassicAssert.IsTrue(gimElement.ElementType.IsTexture() || gimElement.ElementType.IsBezierType() || gimElement.ElementType.IsPoly());
			}

			return true;
		}

		public static bool CheckCorrectnessBytes(IReadOnlyGimElement gimElement)
		{
			int gimElementExpectedByteCount = gimElement.ElementType.GetByteCount();
			if (gimElementExpectedByteCount != -1)
			{
				if (gimElementExpectedByteCount == 0)
				{
					if (gimElement.ElementBytes != null)
						ClassicAssert.AreEqual(gimElementExpectedByteCount, gimElement.ElementBytes.Count);
				}
				else
				{
					ClassicAssert.AreEqual(gimElementExpectedByteCount, gimElement.ElementBytes.Count);
				}
			}
			else
			{
				ClassicAssert.IsTrue(gimElement.ElementType.IsTexture());
			}

			return true;
		}

		public static bool CheckCorrectnessDoubles(IReadOnlyGimElement gimElement)
		{
			int gimElmentExpectedDoubleCount = gimElement.ElementType.GetDoubleCount();
			if (gimElmentExpectedDoubleCount != -1)
			{
				if (gimElmentExpectedDoubleCount == 0)
				{
					if (gimElement.ElementDoubles != null)
						ClassicAssert.Equals(gimElement.ElementDoubles.Count, gimElmentExpectedDoubleCount);
				}
				else
				{
					ClassicAssert.AreEqual(gimElement.ElementDoubles.Count, gimElmentExpectedDoubleCount);
				}
			}
			else
			{
				ClassicAssert.IsTrue(gimElement.ElementType.IsBezierType() || gimElement.ElementType == GimElementType.P_polygon);
			}

			return true;
		}

		/// <summary>
		/// Checks if the assumptions we make about Bspline curves hold.
		/// </summary>
		/// <param name="element">The bspline to check this for. </param>
		/// <returns></returns>
		public static bool CheckCorrectnessElement_C_bspline(IReadOnlyGimElement element)
		{
			// https://en.wikipedia.org/wiki/Non-uniform_rational_B-spline
			ClassicAssert.AreEqual(element.ElementType, GimElementType.C_bspline);

			// Read specials (set location)
			int degree = element.ElementInts[^1];
			int knotsCount = element.ElementInts[0];
			int multiplicitiesCount = element.ElementInts.Count - 2;
			int polesCount = (element.ElementDoubles.Count - (/*2 +*/6) - knotsCount) / 4;


			// Read all poles
			double[] poles = MakePoles(polesCount, element.ElementDoubles, (/*2 +*/6));

			// Read all ints
			double[] knots = new double[knotsCount];
			for (int i = 0; i < knotsCount; i++)
			{
				knots[i] = element.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
			}

			double[] weights = new double[polesCount];
			for (int i = 0; i < polesCount; i++)
			{
				weights[i] = element.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount];
			}

			int[] multiplicities = new int[multiplicitiesCount];
			for (int i = 0; i < multiplicitiesCount; i++)
			{
				multiplicities[i] = element.ElementInts[i + 1];
			}

			ClassicAssert.IsTrue(polesCount == multiplicities.Sum() - multiplicities[0] || polesCount == multiplicities.Sum() - degree - 1);

			// It follows that the values in the knot vector should be in nondecreasing order, so (0, 0, 1, 2, 3, 3) is valid while (0, 0, 2, 1, 3, 3) is not.
			for (int i = 1; i < knotsCount; i++)
			{
				ClassicAssert.IsTrue(knots[i - 1] <= knots[i]);
			}

			// theNumberOfPoles defines the length of thePoles and must be >= 2;
			ClassicAssert.IsTrue(polesCount >= 2);

			// theNumberOfKnots defines the length of theKnots and theMultiplicities and must be >= 2;
			ClassicAssert.IsTrue(multiplicitiesCount >= 2);
			ClassicAssert.IsTrue(knotsCount >= 2);
			ClassicAssert.AreEqual(multiplicitiesCount, knotsCount);

			// theDegree must belong to range[1, 25];
			ClassicAssert.IsTrue(degree <= 25);
			ClassicAssert.IsTrue(degree >= 1);

			// The number of poles must equal the number of weights.
			ClassicAssert.AreEqual(polesCount, weights.Length);

			// https://mathworld.wolfram.com/B-Spline.html
			// p == m - n - 1
			// degree == knotsCount - polesCount - 1
			if (degree == knotsCount - polesCount - 1 + degree + degree)
			{
				ClassicAssert.IsTrue(multiplicities[0] == degree + 1 && multiplicities[^1] == degree + 1);
				if (TestComparison.AreProbablySame(element.ElementDoubles[0], poles[0], 0.01)
				&& TestComparison.AreProbablySame(element.ElementDoubles[1], poles[1], 0.01)
				&& TestComparison.AreProbablySame(element.ElementDoubles[2], poles[2], 0.01)
				&& TestComparison.AreProbablySame(element.ElementDoubles[3], poles[^3], 0.01)
				&& TestComparison.AreProbablySame(element.ElementDoubles[4], poles[^2], 0.01)
				&& TestComparison.AreProbablySame(element.ElementDoubles[5], poles[^1], 0.0))
				{
					TestComparison.AreEqualEnough(element.ElementDoubles[0], poles[0]);
					TestComparison.AreEqualEnough(element.ElementDoubles[1], poles[1]);
					TestComparison.AreEqualEnough(element.ElementDoubles[2], poles[2]);
					TestComparison.AreEqualEnough(element.ElementDoubles[3], poles[^3]);
					TestComparison.AreEqualEnough(element.ElementDoubles[4], poles[^2]);
					TestComparison.AreEqualEnough(element.ElementDoubles[5], poles[^1]);
				}
			}

			//double[] knotsAndExtension = new double[degree + knots.Length + degree];
			//for (int i = 0; i < knotsAndExtension.Length; i++)
			//{
			//	if (i < degree)
			//		knotsAndExtension[i] = knots[0];
			//	else if (i < degree + knots.Length)
			//		knotsAndExtension[i] = knots[i - degree];
			//	else
			//		knotsAndExtension[i] = knots[^1];
			//}

			//double[] N = new double[knots.Length];
			//for (int i = 0; i < knots.Length - 1; i++)
			//{
			//	N[i] = InternalFunction(i + degree, degree, knotsAndExtension, knots[i]);
			//}

			//for (int i = 1; i < knotsCount; i++)
			//{
			//	ClassicAssert.IsTrue(N[i - 1] <= N[i]);
			//}

			// element.elementDoubles.Length = polesCount * 3 + weightCount + multiplicitiesCount + knots.Length + vertexA.xyz + vertexB.xyz
			ClassicAssert.AreEqual(element.ElementDoubles.Count, polesCount * 3 + polesCount + knots.Length + 6);
			ClassicAssert.AreEqual(element.ElementInts.Count, multiplicitiesCount + 2);

			return true;
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
