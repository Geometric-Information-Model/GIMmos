//using GIMcompress;
//using GmshNet;
//using System;
//using System.Collections.Generic;
//using GimUtilities;

//namespace GIMtransfer.BrepToPoly
//{
//	// Open cascade engine (Open Cascade is similar to Cadex)
//	using occ = Gmsh.Model.Occ;

//	internal static partial class GimGMSH
//	{
//		/// <summary>
//		/// Add a face to gmsh.
//		/// This also adds the tag of the curve to the elementToTag lookup table.
//		/// </summary>
//		/// <param name="element">The face to add.</param>
//		/// <param name="gimData">The GimData to read it's children from.</param>
//		/// <param name="elementToTag">The element to tag lookup table.</param>
//		/// <param name="index">Index of the current element.</param>
//		/// <param name="parent">Parent of the current element.</param>
//		private static void AddFaceToGmsh(IReadOnlyGimElement element, IReadOnlyGimData gimData, Dictionary<IReadOnlyGimElement, int> elementToTag, int index, int parent)
//		{
//			// Always fill in the tag!
//			// And at it to the lookup table.
//			int tag;

//			IReadOnlyGimLocal local = element.Locals[0];//.GetLocalThatBelongsToThisParentOfThisElement(parent);

//			// Add element to occ.
//			switch (element.ElementType)
//			{
//				case GimElementType.F_plane:
//					int[] curveTags = GetChildrenTags(elementToTag, element, gimData);
//					tag = occ.AddPlaneSurface(curveTags, -1);
//					break;
//				case GimElementType.F_bezier:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_bspline:
//					tag = AddF_bsplineToGmsh(elementToTag, element, gimData);
//					break;
//				case GimElementType.F_cilinder:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_cone:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_extrusion:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_offset:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_polygon:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_revolution:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_sphere:
//					GimLogger.Warn("Set angle1, angle2, angle3.");
//					tag = occ.AddSphere(local.Px, local.Py, local.Pz, element.ElementDoubles[0], -1);
//					break;
//				case GimElementType.F_toroid:
//					throw new NotImplementedException(element.ElementType.ToString());
//				case GimElementType.F_trimmed:
//					throw new NotImplementedException(element.ElementType.ToString());
//				default:
//					throw new ArgumentException(element.ToString());
//			}

//			elementToTag.Add(element, tag);
//		}

//		/// <summary>
//		/// Add a bspline face to occ gmsh directly.
//		/// </summary>
//		/// <param name="elementToTag">The element lookup table.</param>
//		/// <param name="element">The f_bspline element.</param>
//		/// <param name="gimData">the gimData to work with.</param>
//		/// <returns></returns>
//		private static int AddF_bsplineToGmsh(Dictionary<IReadOnlyGimElement, int> elementToTag, IReadOnlyGimElement element, IReadOnlyGimData gimData)
//		{
//			// Read specials (set location)
//			int NumberOfSpecials = 6;
//			int polesCount = element.ElementInts[0];
//			int uDegree = element.ElementInts[1];
//			int vDegree = element.ElementInts[2];
//			int uKnotsCount = element.ElementInts[3];
//			int uPolesCount = element.ElementInts[4];
//			int multiplicitiesCount = element.ElementInts[5];
//			int vPolesCount = polesCount / uPolesCount;

//			// Read all poles
//			int[] polesTags = AddPoles(polesCount, element.ElementDoubles);

//			double[] weights = new double[polesCount];
//			int startNEW = polesCount * 3;
//			for (int iNEW = 0; iNEW < polesCount; iNEW++)
//			{
//				weights[iNEW] = element.ElementDoubles[startNEW + iNEW];
//			}

//			// Read all ints
//			int[] multiplicities = new int[multiplicitiesCount];
//			int functionIndexCounter = NumberOfSpecials;
//			for (; functionIndexCounter < multiplicitiesCount + NumberOfSpecials; functionIndexCounter++)
//			{
//				multiplicities[functionIndexCounter - NumberOfSpecials] = element.ElementInts[functionIndexCounter];
//			}

//			int uMultiplicitiesCount = GetUMultiplicitieCount(uPolesCount, uDegree, multiplicities, multiplicitiesCount);
//			int vMultiplicitiesCount = multiplicities.Length - uMultiplicitiesCount;
//			int[] uMultiplicities = new int[uMultiplicitiesCount];
//			int[] vMultiplicities = new int[vMultiplicitiesCount];

//			for (int j = 0; j < uMultiplicitiesCount; j++)
//			{
//				uMultiplicities[j] = multiplicities[j];
//			}

//			for (int j = 0; j < vMultiplicitiesCount; j++)
//			{
//				vMultiplicities[j] = multiplicities[j + uMultiplicitiesCount];
//			}

//			int start = polesCount * 4;
//			functionIndexCounter = 0;
//			double[] uKnots = new double[uKnotsCount];
//			for (; functionIndexCounter < uKnotsCount; functionIndexCounter++)
//			{
//				uKnots[functionIndexCounter] = element.ElementDoubles[start + functionIndexCounter];
//			}

//			start += functionIndexCounter;
//			int vKnotsCount = element.ElementDoubles.Count - (polesCount * 4 + uKnotsCount);
//			functionIndexCounter = 0;
//			double[] vKnots = new double[vKnotsCount];
//			for (; functionIndexCounter < vKnotsCount; functionIndexCounter++)
//			{
//				vKnots[functionIndexCounter] = element.ElementDoubles[start + functionIndexCounter];
//			}

//			// TODO: Replace magic number by variable.
//			// 3.675086918879E-08
//			for (int i = 1; i < uKnots.Length; i++)
//			{
//				if (uKnots[i - 1] >= uKnots[i])
//					uKnots[i] = uKnots[i - 1] + 3.675086918879E-08;
//			}

//			// TODO: Replace magic number by variable.
//			// 3.675086918879E-08
//			for (int i = 1; i < vKnots.Length; i++)
//			{
//				if (vKnots[i - 1] >= vKnots[i])
//					vKnots[i] = vKnots[i - 1] + 3.675086918879E-08;
//			}

//			int[] wireTags = GetChildrenTags(elementToTag, element, gimData);

//			// TODO: This error!!!
//			// GmshNet.GmshException : error code: 1,where: GmshNet.Gmsh+Model+Occ.AddBSplineSurface
//			// OpenCASCADE exception Geom_BSplineSurface: # V Poles and degree mismatch
//			// I think this is because Cadex calculates the degree and multiplicities weirdly.
//			// A possible fix for this is only adding the poles, or keeping some values to a default value.
//			// It is possible to fill the values with an array of length 0. This wil make occ calculate possible values self.

//			return occ.AddBSplineSurface(polesTags, uPolesCount, -1, uDegree, vDegree, weights, uKnots, vKnots, uMultiplicities, vMultiplicities, wireTags, true);
//		}

//		/// <summary>
//		/// 
//		/// </summary>
//		/// <param name="uPolesCount"></param>
//		/// <param name="uDegree"></param>
//		/// <param name="multiplicities"></param>
//		/// <param name="multiplicitiesCount"></param>
//		/// <returns></returns>
//		private static int GetUMultiplicitieCount(int uPolesCount, int uDegree, int[] multiplicities, int multiplicitiesCount)
//		{
//			bool periodic = true;
//			int ifNonPeriodic = uPolesCount + uDegree + 1;
//			int ifPeriodic = uPolesCount + multiplicities[0];
//			if (ifNonPeriodic == ifPeriodic)
//			{
//				periodic = false;
//			}

//			for (int j = 1; j < multiplicitiesCount; j++)
//			{
//				int MultiSum = 0;
//				for (int k = 0; k < j; k++)
//				{
//					MultiSum += multiplicities[k];
//				}
//				if (periodic && MultiSum == ifPeriodic)
//				{
//					return j;
//				}
//				else if (MultiSum == ifNonPeriodic)
//				{
//					return j;
//				}
//			}
//			throw GimLogger.Error("UMultiplicities not found in BSPline.");
//		}
//	}
//}
