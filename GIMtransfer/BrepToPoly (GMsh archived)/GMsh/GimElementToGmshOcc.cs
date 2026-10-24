//using GIMcompress;
//using System.Collections.Generic;
//using GimUtilities;

//namespace GIMtransfer.BrepToPoly
//{
//	// Open cascade engine (Open Cascade is similar to Cadex)

//	internal static partial class GimGMSH
//	{
//		/// <summary>
//		/// Add the b-representation in the part to the GMsh.occ cad engine.
//		/// </summary>
//		/// <param name="gimData">The gimData to read from</param>
//		/// <param name="elementToTag">The lookup table for converting elements to tags.</param>
//		/// <param name="index"> Index to start working from.</param>
//		/// <param name="parent"> The parent of the current element.</param>
//		private static void AddPartToGmsh(IReadOnlyGimData gimData, Dictionary<IReadOnlyGimElement, int> elementToTag, int index, int parent)
//		{
//			// Depth first creation (Beceause we need to create the tags of children first.)
//			foreach (uint child in gimData.Elements[index].Elements)
//			{
//				AddPartToGmsh(gimData, elementToTag, (int)child, index);
//			}
//			AddElementToGmsh(gimData.Elements[index], gimData, elementToTag, index, parent);
//		}

//		/// <summary>
//		/// Add the element to the GMsh.occ cad engine.
//		/// It's children should be handled first.
//		/// This also adds the tag of the curve to the elementToTag lookup table.
//		/// </summary>
//		/// <param name="element">The element to add</param>
//		/// <param name="gimData">The GimData we are working on</param>
//		/// <param name="elementToTag">The element to tag lookup table to find the children of this element in the occ engine.</param>
//		/// <param name="index">The index of the current element we are working on.</param>
//		/// <param name="parent">The parent of the current element we are working on.</param>
//		private static void AddElementToGmsh(IReadOnlyGimElement element, IReadOnlyGimData gimData, Dictionary<IReadOnlyGimElement, int> elementToTag, int index, int parent)
//		{
//			if (elementToTag.ContainsKey(element))
//			{
//				// Element is already added.
//				// TODO: Handle linked-twin gimData's.
//				GimLogger.Warn("Multiple locals not handled!");
//				return;
//			}

//			if (element.ElementType.IsDocument() || element.ElementType.IsRepresentation() || element.ElementType.IsBody())
//			{
//				// Do nothing
//				return;
//			}

//			if (element.ElementType.IsCurve())
//			{
//				AddCurveToGmsh(element, gimData, elementToTag, index, parent);
//				return;
//			}

//			if (element.ElementType == GimElementType.wire)
//			{
//				AddWireToGmsh(element, gimData, elementToTag, index, parent);
//				return;
//			}


//			if (element.ElementType.IsFace())
//			{
//				AddFaceToGmsh(element, gimData, elementToTag, index, parent);
//				return;
//			}

//			AddVolumeToGmsh(element, gimData, elementToTag, index, parent);
//		}

//		/// <summary>
//		/// Get the tags of the children.
//		/// </summary>
//		/// <param name="elementToTag">The lookup table to look in.</param>
//		/// <param name="element">The element to find the kids for.</param>
//		/// <param name="gimData">The GimData to look in.</param>
//		/// <returns></returns>
//		private static int[] GetChildrenTags(Dictionary<IReadOnlyGimElement, int> elementToTag, IReadOnlyGimElement element, IReadOnlyGimData gimData)
//		{
//			int childrenCount = element.Elements.Count;

//			// Tag array.
//			int[] tags = new int[childrenCount];

//			for (int i = 0; i < element.Elements.Count; i++)
//			{
//				// Get the tag of the child.
//				tags[i] = elementToTag[gimData.Elements[(int)element.Elements[i]]];
//			}

//			return tags;
//		}
//	}
//}
