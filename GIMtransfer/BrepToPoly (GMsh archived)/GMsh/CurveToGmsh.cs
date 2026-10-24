//using GIMcompress;
//using GmshNet;
//using System;
//using System.Collections.Generic;

//namespace GIMtransfer.BrepToPoly
//{
//	// Open cascade engine (Open Cascade is similar to Cadex)
//	using occ = Gmsh.Model.Occ;

//	internal static partial class GimGMSH
//	{
//		/// <summary>
//		/// Add a curve to gmsh.
//		/// This also adds the tag of the curve to the elementToTag lookup table.
//		/// </summary>
//		/// <param name="element">The curve to add.</param>
//		/// <param name="gimData">The GimData to read it's children from. (trimmed curves and offset curves have children.)</param>
//		/// <param name="elementToTag">The element to tag lookup table.</param>
//		/// <param name="index">Index of the current element.</param>
//		/// <param name="parent">Parent of the current element.</param>
//		private static void AddCurveToGmsh(IReadOnlyGimElement element, IReadOnlyGimData gimData, Dictionary<IReadOnlyGimElement, int> elementToTag, int index, int parent)
//		{
//			int tag;
//		IReadOnlyGimLocal local = element.Locals[0]; //.GetLocalThatBelongsToThisParentOfThisElement(parent);

//			// Add element to occ.
//			switch (element.ElementType)
//			{
//				case GimElementType.C_circle:
//					if (element.ElementDoubles[1] == element.ElementDoubles[2])
//					{
//						tag = occ.AddCircle(local.Px, local.Py, local.Pz, element.ElementDoubles[0], -1);
//						break;
//					}
//					tag = occ.AddCircle(local.Px, local.Py, local.Pz, element.ElementDoubles[0], -1, element.ElementDoubles[1], element.ElementDoubles[2]);
//					break;
//				case GimElementType.C_line:
//					int pointStart = occ.AddPoint(
//						local.Px + local.Zx * element.ElementDoubles[0],
//						local.Py + local.Zy * element.ElementDoubles[0],
//						local.Pz + local.Zz * element.ElementDoubles[0],
//						meshingConstraint, -1);
//					int pointEnd = occ.AddPoint(
//						local.Px + local.Zx * element.ElementDoubles[1],
//						local.Py + local.Zy * element.ElementDoubles[1],
//						local.Pz + local.Zz * element.ElementDoubles[1],
//						meshingConstraint, -1);
//					tag = occ.AddLine(pointStart, pointEnd, -1);
//					break;
//				case GimElementType.C_bezier:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.C_bspline:
//					tag = AddC_bsplineToGmsh(element);
//					break;
//				case GimElementType.C_elipse:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.C_hyperbol:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.C_offset:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.C_parabol:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.C_trimmed:
//					throw new NotImplementedException(element.ElementType.ToString());
//				default:
//					throw new ArgumentException(element.ToString());
//			}

//			elementToTag.Add(element, tag);
//			return;
//		}

//		/// <summary>
//		/// Add a bspline curve to occ gmsh directly.
//		/// </summary>
//		/// <param name="element">The c_bspline element.</param>
//		/// <returns>The tag of the bspline curve that is added to occ gmsh. </returns>
//		private static int AddC_bsplineToGmsh(IReadOnlyGimElement element)
//		{
//			// Read specials (set location)
//			int degree = element.ElementInts[^1];
//			int knotsCount = element.ElementInts[0];
//			int multiplicitiesCount = element.ElementInts.Count - 2;
//			int polesCount = (element.ElementDoubles.Count - (/*2 +*/6) - knotsCount) / 4;


//			// Read all ints
//			double[] knots = new double[knotsCount];
//			for (int i = 0; i < knotsCount; i++)
//			{
//				knots[i - 0] = element.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)];
//			}
//			//knots = NormaliseKnots(knots, new double[2] { element.elementDoubles[element.elementDoubles.Length - 2], element.elementDoubles[element.elementDoubles.Length - 1] });

//			double[] weights = new double[polesCount];
//			for (int i = 0; i < polesCount; i++)
//			{
//				weights[i] = element.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount];
//			}

//			int[] multiplicities = new int[multiplicitiesCount];
//			for (int i = 0; i < multiplicitiesCount; i++)
//			{
//				multiplicities[i] = element.ElementInts[i + 1];
//			}

//			// TODO: Replace magic number by variable.
//			for (int i = 1; i < knots.Length; i++)
//			{
//				if (knots[i - 1] >= knots[i])
//					knots[i] = knots[i - 1] + 3.675086918879E-08;
//			}

//			int[] pointTags = AddPoles(polesCount, element.ElementDoubles, 6);

//			return occ.AddBSpline(pointTags, -1, degree, weights, knots, multiplicities);
//		}

//		/// <summary>
//		/// Directly add the poles to occ. 
//		/// </summary>
//		/// <param name="polesCount">Amount of poles to add.</param>
//		/// <param name="doubles">List of doubles containing the poles.</param>
//		/// <param name="offset">amount of dubbles to skip. e.g. special doubles for an element.</param>
//		/// <returns>The tags of the poles. In the same order they where added.</returns>
//		private static int[] AddPoles(int polesCount, IReadOnlyList<double> doubles, int offset = 0)
//		{
//			int[] poleTags = new int[polesCount];
//			for (int i = 0; i < polesCount; i++)
//			{
//				poleTags[i] = occ.AddPoint(doubles[offset + i * 3], doubles[offset + i * 3 + 1], doubles[offset + i * 3 + 2]);
//			}
//			return poleTags;
//		}
//	}
//}
