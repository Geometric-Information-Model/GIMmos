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
//		/// Add a volume to gmsh.
//		/// </summary>
//		/// <param name="element">The volume to add.</param>
//		/// <param name="gimData">The GimData to read it's children from.</param>
//		/// <param name="elementToTag">The element to tag lookup table.</param>
//		/// <param name="index">Index of the current element.</param>
//		/// <param name="parent">Parent of the current element.</param>
//		private static void AddVolumeToGmsh(IReadOnlyGimElement element, IReadOnlyGimData gimData, Dictionary<IReadOnlyGimElement, int> elementToTag, int index, int parent)
//		{
//			int tag;

//			switch (element.ElementType)
//			{
//				case GimElementType.shell:
//					int[] surfaceTags = GetChildrenTags(elementToTag, element, gimData);
//					tag = occ.AddSurfaceLoop(surfaceTags, -1, false);

//					elementToTag.Add(element, tag);
//					return;
//				case GimElementType.solid:
//					int[] shellTags = GetChildrenTags(elementToTag, element, gimData);
//					tag = occ.AddVolume(shellTags, -1);

//					elementToTag.Add(element, tag);
//					return;
//				default:
//					throw new ArgumentException(element.ToString());
//			}

//		}
//	}
//}
