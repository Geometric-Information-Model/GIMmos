using ACadSharp.Entities;
using GIMCompress;
using GimUtilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestUI")]
[assembly: InternalsVisibleTo("GIMtransfer")]

namespace GIMcompress
{
	public class GimData : IReadOnlyGimData
	{
		public List<GimObject> objects = new List<GimObject>();
		public List<GimElement> elements = new List<GimElement>();
		public List<GimJoint> joints = new List<GimJoint>();
		// Parallel list like objects/elements/joints; element index graph untouched.
		public List<GimPropertySet> propertySets = new List<GimPropertySet>();
		public string projectID = null;
		public string objectID = null;
		public GimUnitSize unitCurr = null;
		public GimUnitSize unitToBe = null;
		public string[] gimUnits = null;
		public byte[] gimBin = null;

		#region properties

		public string[] GimUnits
        {
			get { return gimUnits; }
			set { gimUnits = value; }
        }
		public byte[] GimBin
        {
			get { return gimBin; }
			set { gimBin = value; }
        }

		public GimUnitSize UnitCurr
        {
			get => unitCurr;
			set => unitCurr = value;
        }

		public GimUnitSize UnitToBe
        {
			get => unitToBe;
			set => unitToBe = value;
        }

		public List<GimObject> Objects
		{
			get => objects;
			set => objects = value;
		}

		public List<GimElement> Elements
		{
			get => elements;
			set => elements = value;
		}

		public List<GimJoint> Joints
		{
			get => joints;
			set => joints = value;
		}

		public List<GimPropertySet> PropertySets
		{
			get => propertySets;
			set => propertySets = value;
		}

		public Dictionary<GimElementType, int> GimElementTypeCounts
		{
			get
			{
				Dictionary<GimElementType, int> gimElementTypeCounts = new Dictionary<GimElementType, int>();

				foreach (GimElement elem in this.Elements)
				{
					if (gimElementTypeCounts.ContainsKey(elem.ElementType))
					{
						gimElementTypeCounts[elem.ElementType] = gimElementTypeCounts[elem.ElementType] + 1;
					}
					else
					{
						gimElementTypeCounts.Add(elem.ElementType, 1);
					}
				}

				return gimElementTypeCounts;
			}
		}

		IReadOnlyList<IReadOnlyGimObject> IReadOnlyGimData.Objects => Objects;
		IReadOnlyList<IReadOnlyGimElement> IReadOnlyGimData.Elements => Elements;
		IReadOnlyList<IReadOnlyGimJoint> IReadOnlyGimData.Joints => Joints;
		IReadOnlyList<IReadOnlyGimPropertySet> IReadOnlyGimData.PropertySets => PropertySets;


		#endregion

		// static methods

		public static GimData CreateGimDataWithRoot()
		{
			GimData gimData = new()
			{
				Elements = new()
				{
					new()
					{
						ElementType = GimElementType.D_root,
					}
				}
			};

			return gimData;
		}

		// instance methods

		/// <summary>
		/// Create a root element at index = 0 if it doesn't exist.
		/// </summary>
		public void CreateRootIfNotExists()
		{
			if(elements.Count == 0)
			{
				elements.Add(new GimElement() { elementType = GimElementType.D_root });
				return;
			}

			if (elements[0].ElementType == GimElementType.D_root) return;
			elements.Insert(0, new GimElement() { elementType = GimElementType.D_root });
			for(int i=1; i<elements.Count; i++)
			{
				GimElement element = elements[i];
				for(int j=0; j<element.Elements.Count; j++)
				{
					element.Elements[j] += 1;
				}

				if(element.Locals.Count == 0)
				{
					element.Locals.Add(new());
					elements[0].Elements.Add((uint)i);
				}
				else{
					foreach(GimLocal local in element.Locals)
					{
						local.parent += 1;
					}
				}
			}
		}

		public void WriteElementTypeCount()
		{
			Dictionary<GimElementType, int> types = new Dictionary<GimElementType, int>();
			foreach (GimElement element in Elements)
			{
				if (types.ContainsKey(element.ElementType))
					types[element.ElementType]++;
				else types.Add(element.ElementType, 1);
			}

			// Print the amount of each type.
			List<KeyValuePair<GimElementType, int>> myList = types.ToList();
			myList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
			foreach (KeyValuePair<GimElementType, int> pair in myList)
				GimLogger.Info(pair.Key + ": " + pair.Value);
		}

		public IReadOnlyGimData AsReadOnly()
		{
			return this;
		}

		public void SubstituteElementInParent(uint substitute, uint old, uint parent)
		{
			bool canQuit = false;
			for (int i = 0; i < Elements[(int)parent].Elements.Count; i++)
            {
                if (Elements[(int)parent].Elements[i] != old)
                    continue;

                Elements[(int)parent].Elements[i] = substitute;
                canQuit = true;
            }
            if (canQuit)
				return;
			if (parent != 0 && Elements[(int)parent].Elements.Exists(x => x > parent) && old > parent && Elements[(int)parent].ElementType != GimElementType.D_folder)
				throw GimLogger.Error("Parent lost it's Child!");
		}

		public void SubstituteElementInChild(uint substitute, uint old, uint child)
		{
			if (Elements[(int)child].Locals == null)
				return;
			//if (old == substitute) { return; }
			if (null != Elements[(int)child].locals.Find((local) => local.parent == substitute))
				Elements[(int)child].locals.RemoveAll((local) => local.parent == old);
			

			for (int i = 0; i < Elements[(int)child].Locals.Count; i++)
			{
				if (Elements[(int)child].Locals[i].parent == old)
					Elements[(int)child].Locals[i].parent = substitute;
			}
		}

		/// <summary>
		/// Removes elements from the gimData.
		/// PS: No elements to be removed may be referenced in the gimData by elements not to be removed.
		/// </summary>
		/// <param name="remove">A list of elements to be removed.</param>
		internal void RemoveElements(List<uint> remove)
		{
			remove.Sort();

			uint[] removeIndices = new uint[Elements.Count];
			uint removeIndex = 0; // Amount of removed elements?
			for (int i = 0; i < Elements.Count; i++)
			{
				if (removeIndex < remove.Count && remove[(int)removeIndex] == i) // Elements to remove & i should be removed
				{
					removeIndex++; // One element removed
				}
				else // i not removed
				{
					removeIndices[i] = removeIndex;
					elements[i - (int)removeIndex] = elements[i]; // Replace element due to shifted elements

				}
			}
			Elements.RemoveRange(Elements.Count - (int)removeIndex, (int)removeIndex); // Remove the last elements which have shifted
			for (int i = 0; i < Elements.Count; i++) // Fix all element and parent references
			{
				for (int j = 0; j < Elements[i].Elements.Count; j++)
				{
					if (remove.Contains(Elements[i].Elements[j])) throw GimLogger.Error("Removed element referenced as child element.");
					Elements[i].Elements[j] -= removeIndices[Elements[i].Elements[j]];
				}
				for (int j = 0; j < Elements[i].Locals.Count; j++)
				{
					if (remove.Contains(Elements[i].Locals[j].parent)) throw GimLogger.Error("Removed element referenced as parent element.");
					Elements[i].Locals[j].parent -= removeIndices[Elements[i].Locals[j].parent];
				}
			}
			for (int j = 0; j < Objects.Count; j++)
			{
				if (remove.Contains(Objects[j].element)) throw GimLogger.Error("Removed element referenced by object.");
				Objects[j].element -= removeIndices[Objects[j].element];
			}
		}

        /// <summary>
        /// Adds a <see cref="GimElementType.Seam"/> child to a 3D curve, marking it as a seam edge.
        /// No-op if the curve already has a seam child.
        /// </summary>
        /// <param name="curveIndex">The index of the curve to mark as a seam.</param>
        /// <returns>The index of the seam element.</returns>
        public uint MarkCurveAsSeam(uint curveIndex)
        {
            GimElement curve = Elements[(int)curveIndex];
            if (!curve.ElementType.IsCurve() || curve.ElementType.Is2DType())
                throw GimLogger.Error(new ArgumentException("MarkCurveAsSeam requires a 3D curve element."));

            foreach (uint childIndex in curve.Elements)
            {
                if (Elements[(int)childIndex].ElementType.IsSeam())
                    return childIndex;
            }

            GimElement seamElement = new()
            {
                ElementType = GimElementType.Seam,
            };
            uint seamIndex = (uint)Elements.Count;
            Elements.Add(seamElement);
            AddChildToParent(seamIndex, curveIndex);
            return seamIndex;
        }

        /// <summary>
        /// Adds childId to parent element, and parentId to the chosen child local (by localIndex).
        /// If localIndex is -1, a new local is created and added to the child.
        /// </summary>
        /// <param name="childId">The index of the child element to add.</param>
        /// <param name="parentId">The index of the parent element to add the child to.</param>
        /// <param name="localIndex">The index of the local to add the child to. If -1, a new local is created and added to the child.</param>
        public void AddChildToParent(uint childId, uint parentId, int localIndex = -1)
        {
            IGimElement parentElement = Elements[(int)parentId];
            IGimElement childElement = Elements[(int)childId];

            GimLocal local;
            // in case no local was chosen
            if (localIndex < 0)
            {
                local = new GimLocal();
                childElement.Locals.Add(local);
            }
            else
            {
                local = childElement.Locals[localIndex];
            }
            parentElement.Elements.Add(childId);
            local.parent = parentId;
        }
        /// <summary>
        /// Removes elements and their locals from the tree.
        /// </summary>
        /// <param name="elementsToDelete"></param>
        public void RemoveElementsAndLocals(List<uint> elementsToDelete)
		{
			//first removing all locals that point to the elements that are to be deleted
			//this needs to be done first because after removal of locals, if locals == 0 -> remove that element aswell
			for (int s = 0; s < elementsToDelete.Count; s++)
			{
				for (int k = 0; k < Elements.Count; k++)
				{
					for (int h = 0; h < Elements[k].Locals.Count; h++)
                    {
                        //removing all locals that point to the elements that are to be deleted
                        if (Elements[k].Locals[h].parent != elementsToDelete[s])
                            continue;
                        
                        Elements[k].Locals.RemoveAt(h);
                        if (Elements[k].Locals.Count == 0) elementsToDelete.Add((uint)k);
                    }
                }
			}
			elementsToDelete.Sort();
			RemoveElements(elementsToDelete);
		}

		/// <summary>
		/// Removes an element and its sub-tree from the gimdata.
		/// </summary>
		/// <param name="treeParentIndex"></param>
		/// <returns></returns>
		public uint RemoveElementSubTree(uint treeParentIndex)
		{
			GimElement treeParent = elements[(int)treeParentIndex];
			HashSet<uint> currentElements = new(treeParent.Elements);
			HashSet<uint> visitedElements = new HashSet<uint>() { treeParentIndex };
			HashSet<uint> elementsToRemove = new();

			var elemToObj = GetElementToObjectDict();
			HashSet<int> objectsToRemove = new();

			if (currentElements.Count == 0) return 0;

			uint elementsBefore = 0;

			while(currentElements.Count > 0)
            {
				HashSet<uint> nextElements = new();
				
				foreach(uint eleIndex in currentElements)
                {
					GimElement element = elements[(int)eleIndex];
					bool allLocalsContained = true;
					for (int i = 0; i < element.Locals.Count; i++) 
                    {
						GimLocal local = element.Locals[i];
						if (!visitedElements.Contains(local.parent))
                        {
							allLocalsContained = false;
                        }
                        else
                        {
							element.Locals.RemoveAt(i);
							i--;
                        }
                    }

                    if (allLocalsContained)
                    {
						foreach(uint childIndex in element.Elements)
                        {
							nextElements.Add(childIndex);
						}
                        if (elemToObj.ContainsKey(eleIndex))
                        {
							objectsToRemove.Add((int)elemToObj[eleIndex]);
                        }
						elementsToRemove.Add(eleIndex);
                    }

					visitedElements.Add(eleIndex);
                }

				currentElements = nextElements;
            }

			List<int> objectsToRemoveSorted = objectsToRemove.ToList();
			objectsToRemoveSorted.Sort();

			// we remove the objects in reverse order so they don't mess up their own indexing
			for(int i = objectsToRemoveSorted.Count - 1; i >= 0; i--)
            {
				int objectIndex = objectsToRemoveSorted[i];
				Objects.RemoveAt(objectIndex);
            }

			elementsBefore = RemoveElementsAndTheirChildren(elementsToRemove.ToList(), treeParentIndex);
			return elementsBefore;
        }

		// removes elements, their children and their references in other elements
		// and returns how many elements were removed before the pivot
		public uint RemoveElementsAndTheirChildren(List<uint> elementsToRemove, uint pivotElement = 0)
        {
			uint beforePivotCount = 0;
			HashSet<uint> uniqueElements = new(elementsToRemove);
            for (int i = 0; i < elementsToRemove.Count; i++)
            {
                uint elId = elementsToRemove[i];
				if (elId < pivotElement) beforePivotCount++; 
                IGimElement element = Elements[(int)elId];
                if (element.Elements.Count == 0) continue;
				// we only add children whose locals all belong to their parent
                foreach (uint childId in element.Elements)
                {
					var childElement = elements[(int)childId];
					bool allLocalsBelongToParent = true;
					for(int localId = 0; localId < childElement.Locals.Count; localId++)
					{
                        GimLocal childLocal = childElement.Locals[localId];

                        if (!uniqueElements.Contains(childLocal.Parent))
						{
							allLocalsBelongToParent = false;
							break;
						}

						childElement.Locals.RemoveAt(localId);
						localId--;
					}

					if (!allLocalsBelongToParent) continue;

                    if (!uniqueElements.Contains(childId))
                    {
                        elementsToRemove.Add(childId);
                        uniqueElements.Add(childId);
                    }
                }
            }
            // sorting seems to be necessary
            elementsToRemove.Sort();
            // removing locals of elements to remove
            for (int i = 0; i < elementsToRemove.Count; i++)
            {
                uint elId = elementsToRemove[i];
                IGimElement element = Elements[(int)elId];
                element.Locals = new List<GimLocal>();
            }

			// removing references of elements to remove
			for (int i = 0; i < Elements.Count; i++)
			{
				IGimElement element = Elements[i];
				if (element.Elements.Count == 0) continue;
				for (int j = 0; j < element.Elements.Count; j++)
				{
					uint childId = element.Elements[j];
					if (elementsToRemove.Contains(childId))
					{
						element.Elements.RemoveAt(j);
						j--;
					}
				}
			}

			// finally, removing the elements
			RemoveElements(elementsToRemove);
			return beforePivotCount;
		}

		/// <summary>
		/// Inverses the array of gimobjects to a dictionary
		/// </summary>
		/// <returns>A dictionary that assigns to each element its object</returns>
		public Dictionary<uint, uint> GetElementToObjectDict()
		{
			Dictionary<uint, uint> elementToObject = new Dictionary<uint, uint>();
			for (uint i = 0; i < Objects.Count; i++)
			{
				elementToObject[Objects[(int)i].element] = i;
			}
			return elementToObject;
		}

		/// <summary>
		/// Clones the GimData.
		/// </summary>
		/// <returns>An Identical copy of the original GimData</returns>
		public GimData Clone()
		{
			GimData output = new GimData
			{
				Objects = new List<GimObject>(Objects.Count),
				Elements = new List<GimElement>(Elements.Count),
			};

			foreach (GimObject obj in Objects)
			{
				output.Objects.Add(obj.Clone());
			}

			foreach (GimElement element in Elements)
			{
				output.Elements.Add(element.Clone());
			}
			return output;
		}

        // TODO
        // TODO
        // TODO FIX THIS FUNCTION AND RE-ENABLE IT 
        // TODO				~ANIS
        // TODO
        public void NormaliseWeights()
		{
			return; 
			foreach (GimElement element in Elements)
			{
				int firstIndex;
				int lastIndex;
				if (element.ElementType == GimElementType.C_bspline)
				{
					firstIndex = element.ElementDoubles.Length - (element.ElementDoubles.Length - 6 - element.ElementInts[0]) / 4;
					lastIndex = element.ElementDoubles.Length;
				}
				else if (element.ElementType == GimElementType.F_bezier)
				{
					GimLogger.Warn("Bezier face untested, contact a developer about this warning!");
					firstIndex = element.elementDoubles.Length - element.elementDoubles.Length / 4;
					lastIndex = element.ElementDoubles.Length;
				}
				else if (element.ElementType == GimElementType.F_bspline)
				{
					firstIndex = element.elementInts[0] * 3;
					lastIndex = element.elementInts[0] * 4;
				}
				else continue;
				double maxWeight = double.MinValue;
				for (int i = firstIndex; i < lastIndex; i++)
				{
					if (maxWeight < element.elementDoubles[i]) maxWeight = element.elementDoubles[i];
				}
				for (int i = firstIndex; i < lastIndex; i++)
				{
					element.elementDoubles[i] = element.elementDoubles[i] / maxWeight;
				}
			}
		}

        // Methods that manipulate the structure of the gimData (merging, demoting, deleting etc...)
        #region structure manipulation

		/// <summary>
		/// Carries a referenced property set from the source to the target GimData when an object is transferred.
		/// Preserves shared set relationships and remaps the object's PropertySetIndex.
		/// Unreferenced sets do not travel.
		/// Out-of-range property set references throw per ADR 0003.
		/// </summary>
		/// <param name="source">Source GimData containing the original property sets.</param>
		/// <param name="target">Target GimData receiving the cloned property sets.</param>
		/// <param name="gimObject">The transferred GimObject whose PropertySetIndex is to be remapped.</param>
		/// <param name="oldToNewPropertySets">Map tracking source set index to target set index.</param>
		internal static void CarryPropertySet(GimData source, GimData target, GimObject gimObject, Dictionary<int, int> oldToNewPropertySets)
		{
			if (gimObject == null || !gimObject.PropertySetIndex.HasValue)
				return;

			int oldIndex = gimObject.PropertySetIndex.Value;
			if (oldToNewPropertySets.TryGetValue(oldIndex, out int newIndex))
			{
				gimObject.PropertySetIndex = newIndex;
				return;
			}

			if (source.PropertySets == null || oldIndex < 0 || oldIndex >= source.PropertySets.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(gimObject.PropertySetIndex),
					$"Carried object '{gimObject.Name}' references property set index {oldIndex}, which is out of range [0, {source.PropertySets?.Count ?? 0}).");
			}

			GimPropertySet sourceSet = source.PropertySets[oldIndex];
			if (sourceSet == null)
			{
				throw new InvalidOperationException(
					$"Carried object '{gimObject.Name}' references null property set at index {oldIndex}.");
			}

			GimPropertySet clonedSet = new()
			{
				Entries = sourceSet.Entries != null
					? new Dictionary<string, GimPropertyValue>(sourceSet.Entries)
					: new Dictionary<string, GimPropertyValue>()
			};

			target.PropertySets ??= new List<GimPropertySet>();
			newIndex = target.PropertySets.Count;
			target.PropertySets.Add(clonedSet);
			oldToNewPropertySets[oldIndex] = newIndex;
			gimObject.PropertySetIndex = newIndex;
		}

        public GimData GetGimElementAsGimData(GimData gimData, uint elementIndex, Dictionary<int, int> oldToNewPropertySets = null)
        {
            if (elementIndex == 0) return gimData;
            Dictionary<uint, uint> oldToNew = new Dictionary<uint, uint>();
            GimData newGimData = new GimData();
            HashSet<uint> elementsToAdd = new();
            List<uint> currentChildren = new() { elementIndex };

            // getting a list of the elements that we will keep references to
            while (currentChildren.Count > 0)
            {
                List<uint> newChildren = new();
                foreach (var child in currentChildren)
                {
                    if (elementsToAdd.Contains(child)) continue;
                    elementsToAdd.Add(child);
                    GimElement childElement = gimData.Elements[(int)child];
                    foreach (var grandChild in childElement.Elements)
                    {
                        newChildren.Add(grandChild);
                    }
                }
                currentChildren = newChildren;
            }

            oldToNewPropertySets ??= new Dictionary<int, int>();
            newGimData = GetGimElementsRecursively(gimData, newGimData, gimData.GetElementToObjectDict(), elementIndex, 0, oldToNew, 0, elementsToAdd, oldToNewPropertySets);

            // Remove parents for the new highest object
            //for (int i = 0; i < newGimData.Elements[0].locals.Count; i++)
            //	newGimData.Elements[0].locals[i].parent = 0;

            return newGimData;
        }



        private static GimData GetGimElementsRecursively(GimData oldGimData, GimData newGimData, Dictionary<uint, uint> elementToObject,
            uint currentElement, uint parent, Dictionary<uint, uint> oldToNew, uint oldParent, HashSet<uint> elementsToAdd,
            Dictionary<int, int> oldToNewPropertySets = null)
        {
            oldToNewPropertySets ??= new Dictionary<int, int>();

            newGimData.Elements.Add(oldGimData.Elements[(int)currentElement].Clone()); // Add the new element
            uint currentIndex = (uint)newGimData.Elements.Count - 1; // The current index in the newGimData
            oldToNew.Add(currentElement, currentIndex); // Add the index conversion

            GimElement newElement = newGimData.Elements[(int)currentIndex];
            for (int i = 0; i < newElement.Locals.Count; i++)
            {
                GimLocal local = newElement.Locals[i];
                // element referenced is not going to be added
                if (!elementsToAdd.Contains(local.parent))
                {
                    newElement.Locals.RemoveAt(i);
                    i--;
                }

            }

            // Fix the indices in the new gimdata
            newGimData.SubstituteElementInParent(currentIndex, currentElement, parent);
            newGimData.SubstituteElementInChild(parent, oldParent, currentIndex);

            if (elementToObject.ContainsKey(currentElement)) // If this is an object, add it
            {
                GimObject gimObject = oldGimData.Objects[(int)elementToObject[currentElement]].Clone();
                gimObject.element = currentIndex;
                CarryPropertySet(oldGimData, newGimData, gimObject, oldToNewPropertySets);
                newGimData.Objects.Add(gimObject);
            }
            //TODO: Remove locals outside of the subtree

            for (int i = 0; i < oldGimData.Elements[(int)currentElement].elements.Count; i++) // Loop over every child
            {
                uint element = oldGimData.Elements[(int)currentElement].elements[i];

                if (oldToNew.ContainsKey(element)) // If the current child is a twin already added in the newGimData
                {
                    if (oldGimData.Elements[(int)element].GetLocalsThatBelongsToThisParentOfThisElement((int)currentElement).Count == newGimData.Elements[(int)oldToNew[element]].GetLocalsThatBelongsToThisParentOfThisElement((int)currentElement).Count)
                    {
                        newGimData.SubstituteElementInChild(currentIndex, currentElement, oldToNew[element]); // Fix the indices in the new gimdata
                        newGimData.SubstituteElementInParent(oldToNew[element], element, oldToNew[currentElement]);
                    }
                }
                else // Not added yet, do so now
                {
                    newGimData = GetGimElementsRecursively(oldGimData, newGimData, elementToObject, element, currentIndex, oldToNew, currentElement, elementsToAdd, oldToNewPropertySets); // Recurse over the current child
                }
            }

            return newGimData;
        }


		

        public uint AddPartialGimDataToThis(IReadOnlyGimData otherGimData, uint elementIndex, uint newParent, uint oldParent)
		{
			// The gimElement to copy
			IReadOnlyGimElement otherGimElement = otherGimData.Elements[(int)elementIndex];

			// Create copy of the gimElement.
			GimElement newElement = otherGimElement.Clone();

			// Remove every local that is not for this parent.
			newElement.locals.RemoveAll((local) => local.parent != oldParent);

			// Replace the parent in the local with the new parent.
			newElement.locals.ForEach((local) => local.parent = newParent);

			// Empty the element elements
			newElement.Elements = new List<uint>();

			uint newGimElementIndex = (uint)elements.Count;

			elements.Add(newElement);
			elements[(int)newParent].Elements.Add(newGimElementIndex);

			for (int i = 0; i < otherGimElement.Elements.Count; i++)
			{
				AddPartialGimDataToThis(otherGimData, otherGimElement.Elements[i], newGimElementIndex, elementIndex);
			}

			return newGimElementIndex;
		}

        /// <summary>
        /// Demotes an object which is an external reference, by re-integrating its GimData in the main GimData (this).
        /// </summary>
        /// <param name="secondaryGimData">The secondary gimData to be demoted</param>
        /// <param name="elementIndex">The elementindex in <b>this</b> where the GimData belongs</param>
        public void DemoteExternalReference(GimData secondaryGimData, uint elementIndex)
		{
			GimElement objectElement = Elements[(int)elementIndex];

			int correctParentIndex = 0;
			if (secondaryGimData.Elements[0].ElementType == GimElementType.D_root)
				correctParentIndex = 1;
			
            objectElement.ElementDoubles = secondaryGimData.Elements[correctParentIndex].ElementDoubles;
            objectElement.ElementInts = secondaryGimData.Elements[correctParentIndex].ElementInts;
            objectElement.ElementBytes = secondaryGimData.Elements[correctParentIndex].ElementBytes;
            objectElement.ElementType = secondaryGimData.Elements[correctParentIndex].ElementType;

			Dictionary<uint, uint> oldToNew = new();

            foreach (uint childId in secondaryGimData.Elements[correctParentIndex].Elements)
            {
                uint newChildIndex = AddSecondaryGimDataRecursive(secondaryGimData, childId, elementIndex, (uint)correctParentIndex, oldToNew);
            }

			// adding the objects and preserving their indexes
			foreach(GimObject obj in secondaryGimData.Objects)
			{
				GimObject newObj = obj.Clone();
				if (obj.Element == correctParentIndex) continue; // we don't add the element that's the parent of the tree again
				newObj.Element = oldToNew[obj.Element];
				Objects.Add(newObj);
			}

        }
		
		/// <summary>
		/// Adds a secondary GimData to this one with the specified parent element.
		/// </summary>
		/// <param name="secondaryGimData">The secondary gimdata</param>
		/// <param name="elementIndex">The element index in the secondary gimData where to start</param>
		/// <param name="newParent">New parent of the secondary gimData's main element</param>
		/// <param name="oldParent">Old parent of the secondary gimData's main element 
		/// (to avoid adding unnecessary locals)</param>
		/// <param name="AddObjects">Whether to transfer objects from the secondary GimData</param>
		/// <param name="oldToNewPropertySets">Optional map tracking source property set index to target index.</param>
		/// <returns>New index of the inserted element</returns>
		internal uint AddSecondaryGimData(GimData secondaryGimData, uint elementIndex, uint newParent, uint oldParent, bool AddObjects = true, Dictionary<int, int> oldToNewPropertySets = null)
		{
			Dictionary<uint, uint> oldToNew = new();
			var result = AddSecondaryGimDataRecursive(secondaryGimData, elementIndex, newParent, oldParent, oldToNew);

			if (AddObjects)
			{
                // adding the objects and preserving their indexes
                oldToNewPropertySets ??= new Dictionary<int, int>();
                foreach (GimObject obj in secondaryGimData.Objects)
                {
                    if (obj.Element == oldParent) continue; // we don't add the element that's the parent of the tree
                    if (!oldToNew.TryGetValue(obj.Element, out uint newElement)) continue;
                    GimObject newObj = obj.Clone();
                    newObj.Element = newElement;
                    CarryPropertySet(secondaryGimData, this, newObj, oldToNewPropertySets);
                    Objects.Add(newObj);
                }
            }

			return result;	
		}

		private uint AddSecondaryGimDataRecursive(GimData secondaryGimData, uint elementIndex, uint newParent, uint oldParent, Dictionary<uint, uint> oldToNewDictionary)
		{
            // The gimElement to copy
            GimElement otherGimElement = secondaryGimData.Elements[(int)elementIndex];

            // Create copy of the gimElement.
            GimElement newElement = otherGimElement.Clone();

            // Remove every local that is not for this parent.
            newElement.locals.RemoveAll((local) => local.parent != oldParent);

            // Replace the parent in the local with the new parent.
            newElement.locals.ForEach((local) => local.parent = newParent);

            // Empty the element elements
            newElement.Elements = new List<uint>();

			uint newGimElementIndex;
			bool alreadyAdded = false;
			// get the id for this child, if we inserted it before then don't insert again
			if (oldToNewDictionary.ContainsKey(elementIndex))
			{
				// if we already added this element, no need to add it again and its sub-hierarchy
				newGimElementIndex = oldToNewDictionary[elementIndex];
				alreadyAdded = true;
			}
			else
			{
                newGimElementIndex = (uint)elements.Count;
                oldToNewDictionary.Add(elementIndex, newGimElementIndex);
            }

            // we add the element to its parent
            elements[(int)newParent].Elements.Add(newGimElementIndex);
            if (alreadyAdded) return newGimElementIndex;

            // adding the children of this element
            elements.Add(newElement);

            for (int i = 0; i < otherGimElement.Elements.Count; i++)
            {
                AddSecondaryGimDataRecursive(secondaryGimData, otherGimElement.Elements[i], newGimElementIndex, elementIndex, oldToNewDictionary);
            }

			return newGimElementIndex;
        }

        /// <summary>
        /// Returns a part of the branch's definition as a flat (one polyrep) poly mesh definition.
		/// Only works for Parts, Folders and Root elements
        /// </summary>
        /// <param name="elementIndex">The index of the Part/Folder/Root we want returned as Mesh</param>
        /// <returns>a GimData containing only the polymesh of this one</returns>
        public GimData GetElementSubTreeAsMesh(uint elementIndex)
        {
            // this is the GimData to be returned, that has a Mesh representation of the element and its subtree
            GimData resultingGimData = new();
            resultingGimData.Elements.Add(
                new GimElement()
                {
                    elementType = GimElementType.R_Poly,
                }
            );

            GimElement selectedElement = elements[(int)elementIndex];
            // doesn't make sense to run this for elements that are not documents (parts/folders)
            if (!selectedElement.ElementType.IsDocument())
                return resultingGimData;
            

            // trying to find if this element has a general material
            int defaultMaterialIndex = -1;
            foreach (uint child in selectedElement.Elements)
            {
                GimElement element = Elements[(int)child];
                // material element found, we apply it by default to the polygons that don't have a material
                if (element.ElementType != GimElementType.Material)
                    continue;
                
                defaultMaterialIndex = 1;
                GimElement materialCopy = element.Clone();
                materialCopy.Locals = new();
                materialCopy.Elements = new();
                resultingGimData.Elements.Add(materialCopy);
            }

            // we recurse over the tree and flatten it into polygons
            foreach (uint child in selectedElement.Elements)
            {
                GetPolygonsRecursively(resultingGimData, (int)child,
					Matrix.Ge3DUnitMatrix(), new Vect(0, 0, 0), defaultMaterialIndex, (int)elementIndex);
            }

            return resultingGimData;
        }

        /// <summary>
        /// Returns a part of the branch's definition as a flat (one polyrep) poly mesh definition recursively.
        /// Called by GetElementSubTreeAsMesh.
        /// </summary>
        /// <param name="meshData">The resulting Mesh GimData</param>
        /// <param name="selectedElementIndex">The index of the next element we're checking</param>
        /// <param name="cumulativeTransformation">The total transformation matrix so far</param>
        /// <param name="cumulativeOffset">The total transformation offset so far</param>
        private void GetPolygonsRecursively(GimData meshData, int selectedElementIndex,
			Matrix cumulativeTransformation, Vect cumulativeOffset, int materialIndex =-1, int callerParentElementId = -1)
		{
			GimElement selectedElement = Elements[selectedElementIndex]; 
			if(selectedElement.ElementType == GimElementType.D_part)
			{ // if it's a part, we take the polygons for every local, and then stop the recursion
				foreach(GimLocal partLocal in selectedElement.Locals)
				{
					if (callerParentElementId >= 0 && (int)partLocal.parent != callerParentElementId)
						continue;

					// calculating the total transformation up to here
					Matrix partTransformation = new Matrix(partLocal);
					Vect partOffset = new Vect(
						partLocal.Px,
						partLocal.Py,
						partLocal.Pz
						);

					Matrix totalTransformation = cumulativeTransformation * partTransformation;
					Vect totalOffset = cumulativeTransformation * partOffset + cumulativeOffset;


                    GimElement defaultPartMaterial = null;
                    GimElement polymeshElement = null;
					int polymeshIndex = -1;

                    // we find the polymesh of the part and the default material
                    foreach (uint childIndex in selectedElement.Elements)
                    {
                        GimElement child = elements[(int)childIndex];
                        if (child.ElementType.IsAppearance())
                        { // default material found
                            defaultPartMaterial = child.Clone();
                        }
                        if (child.ElementType == GimElementType.R_Poly)
                        { // polymesh found
                            polymeshElement = child;
							polymeshIndex = (int)childIndex;
                        }
                    }

                    int defaultPartIndex = materialIndex;
                    // if the default material is not null, we will add it to polygons that don't have a material
                    if (defaultPartMaterial != null)
                    {
                        defaultPartMaterial.Locals = new();
                        defaultPartMaterial.Elements = new();
                        defaultPartIndex = meshData.Elements.Count;
                        meshData.Elements.Add(defaultPartMaterial);
                    }

					// if it has a polymesh, we will find and get the polygons
					if(polymeshIndex > -1)
					{
						GetPolygonsRecursively(meshData, polymeshIndex, totalTransformation, totalOffset, defaultPartIndex, -1);
                    }

                }
            }
			// if it's a folder, it doesn't have immediate available information. therefore we have to
			// visit the children as well
			else if(selectedElement.ElementType == GimElementType.D_folder)
			{
				foreach (GimLocal local in selectedElement.Locals)
				{
					foreach (int childIndex in selectedElement.Elements)
					{
						Matrix folderTransform = new Matrix(local);
						Vect folderOffset = new Vect(
							local.Px,
							local.Py,
							local.Pz
							);
						// we take the transformation of the folder into account
						Matrix nextTransformation = cumulativeTransformation * folderTransform;
						Vect nextOffset = cumulativeTransformation * folderOffset + cumulativeOffset;
						GetPolygonsRecursively(meshData, childIndex, nextTransformation, nextOffset, materialIndex, selectedElementIndex);
                    }
				}
			}
			// if it's a root element, it has no local, so it's a special case
			else if(selectedElement.ElementType == GimElementType.D_root)
			{
				Matrix startTransformation = Matrix.Ge3DUnitMatrix();
				Vect offset = new(0, 0, 0);
				foreach(int childIndex in selectedElement.Elements)
				{
					GetPolygonsRecursively(meshData, childIndex, startTransformation, offset, materialIndex, 0);
				}
			}
			// this is the meat of the algorithm, where we're gonna find the polygons
			else if(selectedElement.ElementType == GimElementType.R_Poly)
			{
				GimElement polymeshElement = selectedElement;
                // we loop over all the polygons in the polymesh
                foreach (uint polygonIndex in polymeshElement.Elements)
                {
                    GimElement polygonElement = elements[(int)polygonIndex].Clone();
                    polygonElement.Locals = new();
                    List<uint> polygonChildren = polygonElement.Elements;
                    polygonElement.Elements = new();
                    int newPolygonIndexInMesh = meshData.Elements.Count;
                    meshData.Elements.Add(polygonElement);
                    meshData.AddChildToParent((uint)newPolygonIndexInMesh, 0);

                    for (int i = 0; i + 2 < polygonElement.ElementDoubles.Length; i += 3)
                    {
                        Vect point = new Vect(
                            polygonElement.ElementDoubles[i],
                            polygonElement.ElementDoubles[i + 1],
                            polygonElement.ElementDoubles[i + 2]
                            );

						// we apply the total transformation to the point
                        Vect newPoint = cumulativeTransformation * point + cumulativeOffset;

                        polygonElement.ElementDoubles[i] = newPoint[0];
                        polygonElement.ElementDoubles[i + 1] = newPoint[1];
                        polygonElement.ElementDoubles[i + 2] = newPoint[2];

                    }
					// if the polygon has a child, it's a material
                    if (polygonChildren.Count == 1)
                    {
                        GimElement polygonMaterialElement = elements[(int)polygonChildren[0]].Clone();
                        polygonMaterialElement.Locals = new();
                        polygonMaterialElement.Elements = new();
                        uint polygonMaterialIndex = (uint)meshData.Elements.Count;
                        meshData.Elements.Add(polygonMaterialElement);
                        meshData.AddChildToParent(polygonMaterialIndex, (uint)newPolygonIndexInMesh);
                    }
					// we apply the default material Index, if it exists
                    else if (materialIndex > 0)
                    {
                        meshData.AddChildToParent((uint)materialIndex, (uint)newPolygonIndexInMesh);
                    }
                }
            }
		}

        #endregion

        public void LogElementCounts()
		{
			Dictionary<GimElementType, uint> elemCounts = new Dictionary<GimElementType, uint>();
			foreach (GimElement elem in elements)
			{
				GimElementType elemType = elem.elementType;

				elemCounts.TryGetValue(elemType, out uint count);
				elemCounts[elemType] = count + 1;
			}

			var elemCountsSorted = from entry in elemCounts orderby entry.Value descending select entry;
			foreach (KeyValuePair<GimElementType, uint> kvp in elemCountsSorted)
			{
				GimLogger.Info("Element type " + kvp.Key + " has " + kvp.Value + " occurrences.");
			}
		}
        public bool ElementContainsChildType(uint element, GimElementType type)
        {
            return elements[(int)element].elements.Exists(x => elements[(int)x].elementType == type);
        }

        public List<GimElement> GetElementsWithType(GimElementType type)
        {
            return Elements.Where(x => x.elementType == type).ToList();   
        }

        public double GetLargestDistance()
        {
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            double minZ = double.MaxValue;
            double maxZ = double.MinValue;
            List<GimElement> polygons = GetElementsWithType(GimElementType.P_polygon);
            foreach (GimElement polygon in polygons)
            {
                for (int i = 0; i < polygon.elementDoubles.Length; i++)
                {
                    if (i % 3 == 0) // X
                    {
                        if (polygon.ElementDoubles[i] < minX)
                        {
                            minX = polygon.ElementDoubles[i];
                        }
                        if (polygon.ElementDoubles[i] > maxX)
                        {
                            maxX = polygon.ElementDoubles[i];
                        }
                    }
                    else if (i % 3 == 1) // Y
                    {
                        if (polygon.ElementDoubles[i] < minY)
                        {
                            minY = polygon.ElementDoubles[i];
                        }
                        if (polygon.ElementDoubles[i] > maxY)
                        {
                            maxY = polygon.ElementDoubles[i];
                        }
                    }
                    else // Z
                    {
                        if (polygon.ElementDoubles[i] < minZ)
                        {
                            minZ = polygon.ElementDoubles[i];
                        }
                        if (polygon.ElementDoubles[i] > maxZ)
                        {
                            maxZ = polygon.ElementDoubles[i];
                        }
                    }
                }
            }
            return Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2) + Math.Pow(maxZ - minZ, 2));
        }
        /// <summary>
        /// Computes the hash code for the GimData object based on its constituent GimElements.
        /// </summary>
        /// <returns>
        /// An integer hash code representing the GimData object, calculated by combining the hash codes of its GimElements.
        /// </returns>
        /// <remarks>
        /// This method iterates through all the GimElements contained within the GimData object and calculates a hash code
        /// by summing the hash codes of each individual GimElement. The resulting hash code uniquely represents the GimData object
        /// based on the content and structure of its GimElements.
        /// </remarks>
        internal int GetGimDataHashCode() 
		{

            int hash = new int();

            foreach (GimElement elem in this.Elements)
            {
                hash += elem.GetHashCode();
            }

            return hash;
		}
        internal uint AddSecondaryGimData(GimData secondaryGimData, uint elementIndex, uint newParent, uint oldParent, (double[], double[]) transformation, bool AddObjects = false)
        {
            Dictionary<uint, uint> oldToNew = new();
            uint result = AddSecondaryGimDataRecursive(secondaryGimData, elementIndex, newParent, oldParent, oldToNew, transformation);
            if (AddObjects)
            {
                Dictionary<int, int> oldToNewPropertySets = new();
                foreach (GimObject obj in secondaryGimData.Objects)
                {
                    if (obj.Element == oldParent) continue;
                    if (!oldToNew.TryGetValue(obj.Element, out uint newElement)) continue;
                    GimObject newObj = obj.Clone();
                    newObj.Element = newElement;
                    CarryPropertySet(secondaryGimData, this, newObj, oldToNewPropertySets);
                    Objects.Add(newObj);
                }
            }
            return result;
        }

        private uint AddSecondaryGimDataRecursive(GimData secondaryGimData, uint elementIndex, uint newParent, uint oldParent, Dictionary<uint, uint> oldToNewDictionary, (double[], double[]) transformation)
        {
            // The gimElement to copy
            GimElement otherGimElement = secondaryGimData.Elements[(int)elementIndex];

            // Create copy of the gimElement.
            GimElement newElement = otherGimElement.Clone();

            // Remove every local that is not for this parent.
            newElement.locals.RemoveAll((local) => local.parent != oldParent);

            // Replace the parent in the local with the new parent.
            newElement.locals.ForEach((local) => local.parent = newParent);

			// Set transform for local if element type is part
			if (newElement.ElementType == GimElementType.D_part) 
			{
                Matrix newMatrix = Matrix.FromRPY(transformation.Item1[0], transformation.Item1[1], transformation.Item1[2]);

                newElement.locals.ForEach(local =>
                {
					newMatrix.ApplyToLocal(local);
                    local.px = transformation.Item1[0];
                    local.py = transformation.Item1[1];
                    local.pz = transformation.Item1[2];
                });
            }
            

            // Empty the element elements
            newElement.Elements = new List<uint>();

            uint newGimElementIndex;
            bool alreadyAdded = false;
            // get the id for this child, if we inserted it before then don't insert again
            if (oldToNewDictionary.ContainsKey(elementIndex))
            {
                // if we already added this element, no need to add it again and its sub-hierarchy
                newGimElementIndex = oldToNewDictionary[elementIndex];
                alreadyAdded = true;
            }
            else
            {
                newGimElementIndex = (uint)elements.Count;
                oldToNewDictionary.Add(elementIndex, newGimElementIndex);
            }

            // we add the element to its parent
            elements[(int)newParent].Elements.Add(newGimElementIndex);
            if (alreadyAdded) return newGimElementIndex;

            // adding the children of this element
            elements.Add(newElement);

            for (int i = 0; i < otherGimElement.Elements.Count; i++)
            {
                AddSecondaryGimDataRecursive(secondaryGimData, otherGimElement.Elements[i], newGimElementIndex, elementIndex, oldToNewDictionary);
            }

            return newGimElementIndex;

        }

        internal uint AddGimMapsSecondaryGimData(GimData secondaryGimData, uint elementIndex, uint newParent, uint oldParent, (double[], double[]) transformation, int visualizationUnitToBe, bool AddObjects = false)
        {
            Dictionary<uint, uint> oldToNew = new();
            uint result = AddGimMapsSecondaryGimDataRecursive(secondaryGimData, elementIndex, newParent, oldParent, oldToNew, transformation, visualizationUnitToBe);
            if (AddObjects)
            {
                Dictionary<int, int> oldToNewPropertySets = new();
                foreach (GimObject obj in secondaryGimData.Objects)
                {
                    if (obj.Element == oldParent) continue;
                    if (!oldToNew.TryGetValue(obj.Element, out uint newElement)) continue;
                    GimObject newObj = obj.Clone();
                    newObj.Element = newElement;
                    CarryPropertySet(secondaryGimData, this, newObj, oldToNewPropertySets);
                    Objects.Add(newObj);
                }
            }
            return result;
        }

        private uint AddGimMapsSecondaryGimDataRecursive(GimData secondaryGimData, uint elementIndex, uint newParent, uint oldParent, Dictionary<uint, uint> oldToNewDictionary, (double[], double[]) transformation, int visualizationUnitToBe)
        {
            var gimUnitProcessor = new GimUnitProcessor();

			//GimUnitSize gimUnitSize = GimUnitSize.FromByte(((byte)visualizationUnitToBe));

            // The gimElement to copy
            GimElement otherGimElement = secondaryGimData.Elements[(int)elementIndex];

            // Create copy of the gimElement.
            GimElement newElement = otherGimElement.Clone();

            // Remove every local that is not for this parent.
            newElement.locals.RemoveAll((local) => local.parent != oldParent);

            // Replace the parent in the local with the new parent.
            newElement.locals.ForEach((local) => local.parent = newParent);

            // Set transform for local if element type is part
            if (newElement.ElementType == GimElementType.D_part)
            {
                Matrix newMatrix = Matrix.FromRPY(transformation.Item2[0], transformation.Item2[1], transformation.Item2[2]);
                newElement.locals.ForEach(local =>
                {
                    local.px = transformation.Item1[0] * 1000;
                    local.py = transformation.Item1[1] * 1000;
                    local.pz = transformation.Item1[2] * 1000;
                    newMatrix.ApplyToLocal(local);
                });
            }

            // Empty the element elements
            newElement.Elements = new List<uint>();

            uint newGimElementIndex;
            bool alreadyAdded = false;
            // get the id for this child, if we inserted it before then don't insert again
            if (oldToNewDictionary.ContainsKey(elementIndex))
            {
                // if we already added this element, no need to add it again and its sub-hierarchy
                newGimElementIndex = oldToNewDictionary[elementIndex];
                alreadyAdded = true;
            }
            else
            {
                newGimElementIndex = (uint)elements.Count;
                oldToNewDictionary.Add(elementIndex, newGimElementIndex);
            }

            // we add the element to its parent
            elements[(int)newParent].Elements.Add(newGimElementIndex);
            if (alreadyAdded) return newGimElementIndex;

            // adding the children of this element
            elements.Add(newElement);

            for (int i = 0; i < otherGimElement.Elements.Count; i++)
            {
                AddGimMapsSecondaryGimDataRecursive(secondaryGimData, otherGimElement.Elements[i], newGimElementIndex, elementIndex, oldToNewDictionary, transformation, visualizationUnitToBe);
            }

            return newGimElementIndex;

        }

    }
}
