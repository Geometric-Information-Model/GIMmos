//using GIMcompress;
//using GmshNet;
//using System.Collections.Generic;

//namespace GIMtransfer.BrepToPoly
//{
//	// Open cascade engine (Open Cascade is similar to Cadex)
//	using occ = Gmsh.Model.Occ;

//	internal static partial class GimGMSH
//	{
//		/// <summary>
//		/// Add a wire to gmsh.
//		/// </summary>
//		/// <param name="element">The wire to add.</param>
//		/// <param name="gimData">The GimData to read it's children from.</param>
//		/// <param name="elementToTag">The element to tag lookup table.</param>
//		/// <param name="index">Index of the current element.</param>
//		/// <param name="parent">Parent of the current element.</param>
//		private static void AddWireToGmsh(IReadOnlyGimElement element, IReadOnlyGimData gimData, Dictionary<IReadOnlyGimElement, int> elementToTag, int index, int parent)
//		{
//			int[] curveTags = GetChildrenTags(elementToTag, element, gimData);

//			// GmshNet.GmshException : error code: 1,where: GmshNet.Gmsh + Model + Occ.AddWire
//			// OpenCASCADE exception BRep_API: command not done
//			// BRep_API exception is often a concurrency problem or that tags are incorrect
//			// e.g. tags can't be 0!

//			// A wire can be open or closed in occ.
//			int tag = occ.AddWire(curveTags);

//			elementToTag.Add(element, tag);
//			return;
//		}
//	}
//}
