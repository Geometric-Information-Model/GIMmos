using System;
using System.Collections.Generic;
using GimUtilities;

namespace GIMcompress
{
	public struct LinkTwinsProperties
	{
		public bool doNothing;
		public int twinCount;
		public uint currentElement;
		public List<uint> toRemove;
		public Dictionary<int, List<uint>> uniqueElements;

		public static LinkTwinsProperties StartProperties()
		{
			return new LinkTwinsProperties()
			{
				doNothing = false,
				twinCount = 0,
				currentElement = 0,
				toRemove = new List<uint>(),
				uniqueElements = new Dictionary<int, List<uint>>(),
			};
		}

		public static LinkTwinsProperties DoNothing()
		{
			return new LinkTwinsProperties()
			{
				doNothing = true,
			};
		}
	}

	public static class Twins
	{

		// Link Twins
		public static GimData LinkTwins(GimData gimData)
		{
			LinkTwinsProperties linkTwinsProperties = LinkTwinsProperties.StartProperties();
			gimData = LinkTwinsPartial(gimData, ref linkTwinsProperties);
			gimData = LinkTwinsEnd(gimData, linkTwinsProperties.toRemove);

			GimLogger.Info("Twins combined: " + linkTwinsProperties.twinCount);

			return gimData;
		}

		// Link twins partial
		public static GimData LinkTwinsPartial(GimData gimData, ref LinkTwinsProperties linkTwinsProperties)
		{
			if (linkTwinsProperties.doNothing)
				return gimData;

			gimData = FindAndBindTwinElements(gimData, linkTwinsProperties.currentElement, ref linkTwinsProperties.uniqueElements, gimData.GetElementToObjectDict(), ref linkTwinsProperties.toRemove, ref linkTwinsProperties.twinCount);
			return gimData;
		}

		public static GimData LinkTwinsEnd(GimData gimData, LinkTwinsProperties linkTwinsProperties)
		{
#if DEBUG
			if (linkTwinsProperties.doNothing)
			{

				throw new Exception("Ending LinkTwins for Twins that where never linked!");
			}
#endif
			return LinkTwinsEnd(gimData, linkTwinsProperties.toRemove);
		}

		public static GimData LinkTwinsEnd(GimData gimData, List<uint> toRemove)
		{
			gimData.RemoveElements(toRemove);
			return gimData;
		}

		private static GimData FindAndBindTwinElements(GimData gimData, uint element, ref Dictionary<int, List<uint>> uniqueElements, Dictionary<uint, uint> elementToObject, ref List<uint> toRemove, ref int twinCount)
		{
			// Depth first!
			for (int childIndex = 0; childIndex < gimData.Elements[(int)element].Elements.Count; childIndex++)
				gimData = FindAndBindTwinElements(gimData, gimData.Elements[(int)element].Elements[childIndex], ref uniqueElements, elementToObject, ref toRemove, ref twinCount);

			IGimElement currentElement = gimData.Elements[(int)element];

			if (currentElement.ElementType.IsDocument() || currentElement.ElementType.IsRepresentation())
				return gimData;

			int hashCode = currentElement.GetHashCode(); // Hash doesn't include locals

			if (currentElement.ElementType != GimElementType.P_vertex)
			{
				if (uniqueElements.ContainsKey(hashCode)) // If the hash code exists, possible twin found, closer inspection required (hash codes can be duplicated)
				{
					foreach (uint otherElement in uniqueElements[hashCode])
					{
						if (element == otherElement)
							continue;
						if (/*currentElement.CompareWithoutLocals(gimData.elements[(int)otherElement])*/
							(currentElement.ElementType.IsCurve() && currentElement.Compare(gimData.Elements[(int)otherElement])) // If curve they need to be exactly the same Or do they?!?!?!?!
							 || (!currentElement.ElementType.IsCurve() && currentElement.CompareWithoutLocals(gimData.Elements[(int)otherElement])) // Else everything needs to be the same except for the locals.
							 ) // If the same, twin found
							   //if(currentElement.CompareWithoutLocals(gimData.Elements[(int)otherElement]))
						{
							twinCount++;

							if (elementToObject.ContainsKey(element))
							{
								gimData.Objects[(int)elementToObject[element]].element = otherElement;
							}

							foreach (GimLocal currentLocal in currentElement.Locals)
							{
								//if (!gimData.elements[(int)otherElement].locals.Contains(currentLocal))
								gimData.Elements[(int)otherElement].Locals.Add(currentLocal);
								gimData.SubstituteElementInParent(otherElement, element, currentLocal.parent); // This changes parent hash, however, since we are using DFS the parent is not in dictionary
							}

							foreach (uint child in currentElement.Elements)
							{
								gimData.SubstituteElementInChild(otherElement, element, child);
							}

							toRemove.Add(element);
							return gimData; 
						}
					}
					uniqueElements[hashCode].Add(element);
					//Run through list


					//    if (gimData.elements[(int)element].Compare(gimData.elements[(int)i]))
					//{
					//    twinCount++;
					//    gimData.SubstituteElement(element, i);
					//    gimData.RemoveElement(i);
					//}

					return gimData;
				}
				uniqueElements.Add(hashCode, new List<uint>() { element });
			}
			return gimData;

		}
	}
}
