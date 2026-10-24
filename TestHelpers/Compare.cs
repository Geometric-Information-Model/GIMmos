using GIMcompress;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;

namespace TestHelpers
{
	/// <summary>
	/// Global static class for comparing two gimdata's.
	/// </summary>
	public static class TestComparison
	{
		public const double DoubleDelta = 0.001;

		/// <summary>
		/// Checks if 2 GimDatas are equal. 
		/// I.e. it checks if all objects are equal and if all elements are equal.
		/// </summary>
		/// <param name="a">The first GimData to check. Choose for this the expected GimData.</param>
		/// <param name="b">The second GimData to check. Choose for this the actual GimData you got as a result of your function.</param>
		/// <returns></returns>
		public static bool CompareGimDatas(IReadOnlyGimData a, IReadOnlyGimData b)
		{
			ClassicAssert.IsTrue(CompareObjects(a, b));
			ClassicAssert.IsTrue(CompareElements(a, b));
			ClassicAssert.IsTrue(ComparePropertySets(a, b));
			return true;
		}

		/// <summary>
		/// Compares the objects of 2 GimDatas.
		/// </summary>
		/// <param name="a">The first GimData to check. Choose for this the expected GimData.</param>
		/// <param name="b">The second GimData to check. Choose for this the actual GimData you got as a result of your function.</param>
		/// <returns></returns>
		public static bool CompareObjects(IReadOnlyGimData a, IReadOnlyGimData b)
		{
			for (int i = 0; i < a.Objects.Count; i++)
				CompareObject(a.Objects[i], b.Objects[i]);
			ClassicAssert.AreEqual(a.Objects.Count, b.Objects.Count);
			return true;
		}

		/// <summary>
		/// Compares 2 GimObjects.
		/// </summary>
		/// <param name="a">The first GimObject to check. Choose for this the expected GimObject.</param>
		/// <param name="b">The second GimObject to check. Choose for this the actual GimObject you got as a result of your function.</param>
		/// <returns></returns>
		public static bool CompareObject(IReadOnlyGimObject a, IReadOnlyGimObject b)
		{
			ClassicAssert.AreEqual(a.Name, b.Name);
			ClassicAssert.AreEqual(a.Element, b.Element);
			ClassicAssert.AreEqual(a.PropertySetIndex, b.PropertySetIndex);
			return true;
		}

		/// <summary>
		/// Compares property sets entry-by-entry, independent of dictionary iteration order.
		/// </summary>
		public static bool ComparePropertySets(IReadOnlyGimData a, IReadOnlyGimData b)
		{
			ClassicAssert.AreEqual(a.PropertySets.Count, b.PropertySets.Count);

			for (int i = 0; i < a.PropertySets.Count; i++)
			{
				IReadOnlyDictionary<string, GimPropertyValue> aEntries = a.PropertySets[i].Entries;
				IReadOnlyDictionary<string, GimPropertyValue> bEntries = b.PropertySets[i].Entries;
				ClassicAssert.AreEqual(aEntries.Count, bEntries.Count);

				foreach (KeyValuePair<string, GimPropertyValue> entry in aEntries)
				{
					ClassicAssert.IsTrue(bEntries.TryGetValue(entry.Key, out GimPropertyValue bValue));
					ClassicAssert.AreEqual(entry.Value.Kind, bValue.Kind);
					if (entry.Value.Kind == GimPropertyKind.String)
						ClassicAssert.AreEqual(entry.Value.StringValue, bValue.StringValue);
					else
						ClassicAssert.AreEqual(entry.Value.DoubleValue, bValue.DoubleValue);
				}
			}

			return true;
		}

		/// <summary>
		/// Compares the elements of 2 GimDatas.
		/// </summary>
		/// <param name="a">The first GimData to check. Choose for this the expected GimData.</param>
		/// <param name="b">The second GimData to check. Choose for this the actual GimData you got as a result of your function.</param>
		/// <returns></returns>
		public static bool CompareElements(IReadOnlyGimData a, IReadOnlyGimData b)
		{
			for (int i = 0; i < a.Elements.Count; i++)
				CompareElement(a.Elements[i], b.Elements[i]);

			ClassicAssert.AreEqual(a.Elements.Count, b.Elements.Count);
			return true;
		}

		/// <summary>
		/// Compares 2 GimElements.
		/// </summary>
		/// <param name="a">The first GimElement to check. Choose for this the expected GimElement.</param>
		/// <param name="b">The second GimElement to check. Choose for this the actual GimElement you got as a result of your function.</param>
		/// <returns></returns>
		public static bool CompareElement(IReadOnlyGimElement a, IReadOnlyGimElement b)
		{
			// Compare Locals
			ClassicAssert.IsTrue(CompareLocals(a, b));

			// Type is right
			ClassicAssert.AreEqual(a.ElementType, b.ElementType);

			// Orientation is equal
			ClassicAssert.AreEqual(a.Orientation, b.Orientation);


			// Elements are okay
			if (a.Elements != null)
			{
				ClassicAssert.AreEqual(a.Elements.Count, b.Elements.Count);
				List<uint> aSorted = GetSortedClone(a.Elements);
				List<uint> bSorted = GetSortedClone(b.Elements);

				for (int j = 0; j < a.Elements.Count; j++)
					ClassicAssert.AreEqual(aSorted[j], bSorted[j]);
			}
			else
			{
				ClassicAssert.IsTrue(b.Elements == null || b.Elements.Count == 0);
			}

			// Doubles are okay
			if (a.ElementDoubles != null)
			{
				ClassicAssert.AreEqual(a.ElementDoubles.Count, b.ElementDoubles.Count);

				for (int j = 0; j < a.ElementDoubles.Count; j++)
					if (!CheckDifferenceTwoPi(a.ElementDoubles[j], b.ElementDoubles[j], DoubleDelta))
						AreEqualEnough(a.ElementDoubles[j], b.ElementDoubles[j]);
			}
			else
			{
				ClassicAssert.IsTrue(b.ElementDoubles == null || b.ElementDoubles.Count == 0);
			}

			// Ints are okay
			if (a.ElementInts != null)
			{
				if (a.ElementType.IsTexture())
				{
					// Textures int can change beceause it now points to a new file.
				}
				else
				{
					ClassicAssert.AreEqual(a.ElementInts.Count, b.ElementInts.Count);

					for (int j = 0; j < a.ElementInts.Count; j++)
						ClassicAssert.AreEqual(a.ElementInts[j], b.ElementInts[j], DoubleDelta);
				}
			}
			else
			{
				ClassicAssert.IsTrue(b.ElementInts == null || b.ElementInts.Count == 0);
			}


			return true;
		}

		/// <summary>
		/// Compare 2 GimLocals.
		/// </summary>
		/// <param name="a">A GimLocal. Choose for this the expected GimLocal.</param>
		/// <param name="b">A GimLocal. Choose for this the actual GimLocal.</param>
		/// <returns></returns>
		public static bool CompareLocals(IReadOnlyGimElement a, IReadOnlyGimElement b)
		{
			// Locals 
			if (a.Locals != null)
			{
				// TODO: how to compare indipendilty????
				List<IReadOnlyGimLocal> aSorted = GetSortedClone(a.Locals, (local, other) => (int)(local.Parent - other.Parent));
				List<IReadOnlyGimLocal> bSorted = GetSortedClone(b.Locals, (local, other) => (int)(local.Parent - other.Parent));

				for (int i = 0; i < a.Locals.Count; i++)
				{
					ClassicAssert.AreEqual(a.Locals[i].Parent, b.Locals[i].Parent);

					AreEqualEnough(a.Locals[i].Px, b.Locals[i].Px, DoubleDelta);
					AreEqualEnough(a.Locals[i].Py, b.Locals[i].Py, DoubleDelta);
					AreEqualEnough(a.Locals[i].Pz, b.Locals[i].Pz, DoubleDelta);
					ClassicAssert.AreEqual(a.Locals[i].Parent, b.Locals[i].Parent);
					ClassicAssert.AreEqual(a.Locals[i].RightHanded, b.Locals[i].RightHanded);

					double ayaw = 0.0f;
					double apitch = 0.0f;
					double aroll = 0.0f;
					double byaw = 0.0f;
					double bpitch = 0.0f;
					double broll = 0.0f;
					GetPitchYawRoll(a.Locals[i], ref ayaw, ref apitch, ref aroll);
					GetPitchYawRoll(b.Locals[i], ref byaw, ref bpitch, ref broll);

					CheckDegrees(ayaw, byaw, 0.1);
					CheckDegrees(apitch, bpitch, 0.1);
					CheckDegrees(aroll, broll, 0.1);
				}
			}
			else
				return b.Locals == null;

			// If out of the loop for checking local equality.
			return true;
		}

		/// <summary>
		/// Gets the Pitch, Yaw and Roll of a GimLocal.
		/// </summary>
		/// <param name="local">The local to get the Pitch, Yaw and Roll from.</param>
		/// <param name="Yaw">The refrence variable the Yaw is set to.</param>
		/// <param name="Pitch">The refrence variable the Pitch is set to.</param>
		/// <param name="Roll">The refrence variable the Roll is set to.</param>
		internal static void GetPitchYawRoll(IReadOnlyGimLocal local, ref double Yaw, ref double Pitch, ref double Roll)
		{
			//if (local.ZAxisIsNull())
			GetPitchYawRoll(local.Xx, local.Xy, local.Xz, local.Yx, local.Yy, local.Yz, local.RightHanded, ref Yaw, ref Pitch, ref Roll);
			//else
			//	GetPitchYawRoll(local.xx, local.xy, local.xz, local.yx, local.yy, local.yz, local.zx, local.zy, local.zz, ref Yaw, ref Pitch, ref Roll);

		}

		private static void GetPitchYawRoll(double r11, double r12, double r13, double r21, double r22, double r23, bool rightHanded, ref double Yaw, ref double Pitch, ref double Roll)
		{
			double r31 = Math.Clamp(r12 * r23 - r13 * r22, -1.0, 1.0);
			double r32 = Math.Clamp(r13 * r21 - r23 * r11, -1.0, 1.0);
			double r33 = Math.Clamp(r11 * r22 - r21 * r12, -1.0, 1.0);

			if (!rightHanded)
			{
				r33 *= -1;
				r32 *= -1;
				r31 *= -1;
			}

			GetPitchYawRoll(r11, r12, r13, r21, r22, r23, r31, r32, r33, ref Yaw, ref Pitch, ref Roll);
		}

#pragma warning disable IDE0060 // Remove unused parameter
		private static void GetPitchYawRoll(double r11, double r12, double r13, double r21, double r22, double r23, double r31, double r32, double r33, ref double Yaw, ref double Pitch, ref double Roll)
#pragma warning restore IDE0060 // Remove unused parameter
		{
			Pitch = -Math.Asin(r31);

			//Gymbal lock: pitch = -90
			if (r31 == 1.0)
			{
				Yaw = 0.0;                      //yaw = 0
				Roll = Math.Atan2(-r12, -r13);    //Roll
			}

			//Gymbal lock: pitch = 90
			else if (r31 == -1.0)
			{
				Yaw = 0.0;             //yaw = 0
				Roll = Math.Atan2(r12, r13);    //Roll
			}

			//General solution
			else
			{
				Yaw = Math.Atan2(r21, r11);
				Roll = Math.Atan2(r32, r33);
			}

		}

		/// <summary>
		/// Comparison function that is safe for degrees that are slightly different (a.k.a. loops around PI).
		/// </summary>
		/// <param name="aDegree">The expected degree. </param>
		/// <param name="bDegree">The actual degree. </param>
		/// <param name="delta">The maximum delta of the two degrees. </param>
		/// <returns></returns>
		public static bool CheckDegrees(double aDegree, double bDegree, double delta)
		{
			return Math.Abs(aDegree - bDegree) % Math.PI <= delta || Math.Abs(aDegree - Math.PI - bDegree) % Math.PI <= delta || Math.Abs(aDegree + Math.PI - bDegree) % Math.PI <= delta;
		}

		/// <summary>
		/// Same as CheckDegree. But uses 2 PI instead of 1 PI.
		/// </summary>
		/// <param name="aDegree">The expected degree. </param>
		/// <param name="bDegree">The actual degree. </param>
		/// <param name="delta">The maximum delta of the two degrees. </param>
		/// <returns></returns>
		public static bool CheckDifferenceTwoPi(double aDegree, double bDegree, double delta)
		{
			return Math.Abs(aDegree - bDegree) % (2 * Math.PI) <= delta || Math.Abs(aDegree - (2 * Math.PI) - bDegree) % (2 * Math.PI) <= delta || Math.Abs(aDegree + (2 * Math.PI) - bDegree) % (2 * Math.PI) <= delta;
		}

		public static void AreEqualEnough(double expected, double actual, double deltaFraction = 0.01)
		{
			if (Math.Abs(expected) < deltaFraction)
				ClassicAssert.AreEqual(expected, actual, deltaFraction);
			else
				ClassicAssert.AreEqual(expected, actual, Math.Abs(expected * deltaFraction));
		}

		public static bool AreProbablySame(double expected, double actual, double deltaFraction = 0.01)
		{
			if (Math.Abs(expected) < deltaFraction)
				return expected - actual < deltaFraction;
			else
				return expected - actual < deltaFraction * expected;
		}

		/// <summary>
		/// Creates a clone of a list that is sorted.
		/// </summary>
		/// <typeparam name="T">The type to sort. (The type of the list.)</typeparam>
		/// <param name="readOnlyUnsorted">The (possibly) unsorted IReadOnlyList.</param>
		/// <param name="comparison">The comparison check for sorted the list.</param>
		/// <returns></returns>
		public static List<T> GetSortedClone<T>(IReadOnlyList<T> readOnlyUnsorted, Comparison<T> comparison = null)
		{
			List<T> sortedClone = new List<T>(readOnlyUnsorted.Count);
			foreach (T item in readOnlyUnsorted)
				sortedClone.Add(item);

			if (comparison != null)
				sortedClone.Sort(comparison);
			else
				sortedClone.Sort();

			return sortedClone;
		}
	}
}
