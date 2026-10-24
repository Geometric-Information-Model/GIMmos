//using GIMcompress;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace GIMtransfer.BrepToPoly
//{
//	public static partial class BrepToPoly
//	{
//		// TODO: Add precision size calculations. 
//		// This needs to relate to size and the curvature of an object.

//		public static GimData BrepGimDataToPoly(IReadOnlyGimData gimData)
//		{
//			// Creates polygon representations for all objects in the gimdata.
//			int rootIndex = 0;
//			GimData gimDataPolygon = new GimData();

//			// Copy root element
//			IReadOnlyGimElement currentGimElement = gimData.Elements[rootIndex];
//			GimElementType currentDocumentType = currentGimElement.ElementType;

//			GimElement newElement = currentGimElement.Clone();
//			newElement.Elements = new List<uint>();

//			gimDataPolygon.elements.Add(newElement);

//			int newGimElementIndex = gimDataPolygon.Elements.Count - 1;

//			// Loop over children
//			for (int i = 0; i < gimData.Elements[rootIndex].Elements.Count; i++)
//			{
//				uint objectElementIndex = gimData.Elements[rootIndex].Elements[i];
//				gimDataPolygon = BrepDocumentToPoly(gimData, gimData.Objects.First((gimObject) => gimObject.Element == objectElementIndex), gimDataPolygon, objectElementIndex, rootIndex, rootIndex);
//			}

//			return gimDataPolygon;
//		}

//		private static GimData BrepDocumentToPoly(IReadOnlyGimData gimData, IReadOnlyGimObject gimObject, GimData gimDataPolygon, uint objectElementIndex, int parentInPolygon, int oldParent)
//		{
//			// Copy element
//			IReadOnlyGimElement currentGimElement = gimData.Elements[(int)objectElementIndex];
//			GimElementType currentDocumentType = currentGimElement.ElementType;

//			GimElement newElement = currentGimElement.Clone();
//			newElement.locals.RemoveAll((local) => local.parent != (uint)oldParent);
//			newElement.locals.ForEach((local) => local.parent = (uint)parentInPolygon);
//			newElement.Elements = new List<uint>();
//			gimDataPolygon.elements.Add(newElement);
//			int newGimElementIndex = gimDataPolygon.elements.Count - 1;

//			if (gimObject != null)
//			{
//				gimDataPolygon.objects.Add(gimObject.Clone());
//				gimDataPolygon.objects[^1].element = (uint)newGimElementIndex;
//			}

//			gimDataPolygon.Elements[parentInPolygon].Elements.Add((uint)newGimElementIndex);


//			if (currentDocumentType.IsDocument() && currentDocumentType != GimElementType.D_part)
//			{
//				// Loop over children
//				for (int i = 0; i < currentGimElement.Elements.Count; i++)
//				{
//					gimDataPolygon = BrepDocumentToPoly(gimData, gimData.Objects.FirstOrDefault((gimObject) => gimObject.Element == currentGimElement.Elements[i]), gimDataPolygon, currentGimElement.Elements[i], newGimElementIndex, (int)objectElementIndex);
//				}

//				return gimDataPolygon;
//			}
//			else if (currentDocumentType == GimElementType.D_part)
//			{
//				// Check if part is already poly and otherwise convert.
//				return BrepPartToPoly(gimData, gimObject, gimDataPolygon, objectElementIndex);
//			}

//			throw new IndexOutOfRangeException();
//		}

//		private static GimData BrepPartToPoly(IReadOnlyGimData gimData, IReadOnlyGimObject gimObject, GimData gimDataPolygon, uint objectElementIndex)
//		{
//			if (gimData.Elements[(int)objectElementIndex].ElementType != GimElementType.D_part)
//			{
//				throw new ArgumentException(nameof(objectElementIndex) + " is not a " + nameof(GimElementType.D_part));
//			}

//			// Creates an Polygon representation for a object.
//			// (?) If there is not already a poly representation for the brepresentation.
//			foreach (uint representationIndex in gimData.Elements[(int)objectElementIndex].Elements)
//			{
//				// TODO: Should look for the highest/proper quality polygon.
//				if (gimData.Elements[(int)representationIndex].ElementType == GimElementType.R_Poly)
//				{
//					// If the object has already a polygon add the polygon representation to the object in gimDataPolygon.
//					gimDataPolygon.AddPartialGimDataToThis(gimData, representationIndex, (uint)gimDataPolygon.elements.Count - 1, objectElementIndex);
//					return gimDataPolygon;
//				}
//			}

//			// If the object has no polygon representation we should generate one from the brep representation.
//			// TODO: Can an object have more then one brep-representation?
//			return BrepRepresentationToPoly(gimData, gimDataPolygon, gimData.Elements[(int)objectElementIndex].Elements[0], gimDataPolygon.Elements.Count, (int)objectElementIndex);
//		}

//		private static GimData BrepRepresentationToPoly(IReadOnlyGimData gimData, GimData gimDataPolygon, uint representationIndex, int parentInPolygon, int oldParent)
//		{
//			IReadOnlyGimElement currentGimElement = gimData.Elements[(int)representationIndex];
//			GimElementType currentDocumentType = currentGimElement.ElementType;

//			GimElement newElement = currentGimElement.Clone();
//			newElement.locals.RemoveAll((local) => local.parent != (uint)oldParent);
//			newElement.locals.ForEach((local) => local.parent = (uint)parentInPolygon);
//			newElement.elementType = GimElementType.R_Poly;
//			newElement.Elements = new List<uint>();

//			gimDataPolygon.elements.Add(newElement);

//			int newGimElementIndex = gimDataPolygon.elements.Count - 1;
//			gimDataPolygon.objects[^1].element = (uint)newGimElementIndex;
//			gimDataPolygon.Elements[parentInPolygon].Elements.Add((uint)newGimElementIndex);

//			Polygon newBody = new Polygon();

//			// Loop over children
//			for (int i = 0; i < currentGimElement.Elements.Count; i++)
//			{
//				int childIndex = (int)currentGimElement.Elements[i];
//				IReadOnlyGimElement childElement = gimData.Elements[childIndex];
//				switch (childElement.ElementType)
//				{
//					case GimElementType.B_solid:
//						newBody.Merge(BrepSolidBodyToPoly(gimData, childIndex));
//						break;
//					case GimElementType.B_sheet:
//						newBody.Merge(BrepSheetBodyToPoly(gimData, childIndex));
//						break;
//					case GimElementType.B_acorn:
//					case GimElementType.B_wireframe:
//						throw new NotImplementedException();
//					default:
//						throw new ArgumentException(nameof(childElement.ElementType) + " is not a bodytype.");
//				}
//			}

//			gimDataPolygon.Elements.Add(newBody.ToGimElement());

//			return gimDataPolygon;
//		}

//		private static Polygon BrepSolidBodyToPoly(IReadOnlyGimData gimData, int bodyIndex)
//		{
//			IReadOnlyGimElement currentGimElement = gimData.Elements[(int)bodyIndex];
//			Polygon polygon = new Polygon();

//			// Loop over children
//			for (int i = 0; i < currentGimElement.Elements.Count; i++)
//			{
//				int childIndex = (int)currentGimElement.Elements[i];
//				IReadOnlyGimElement childElement = gimData.Elements[childIndex];
//				if (childElement.ElementType == GimElementType.solid)
//				{
//					polygon.Merge(BrepSolidToPoly(gimData, childIndex));
//				}
//				else
//				{
//					throw new ArgumentException(nameof(childIndex));
//				}
//			}

//			return polygon;
//		}

//		private static Polygon BrepSheetBodyToPoly(IReadOnlyGimData gimData, int sheetBodyIndex)
//		{
//			IReadOnlyGimElement currentGimElement = gimData.Elements[(int)sheetBodyIndex];
//			Polygon polygon = new Polygon();

//			// Loop over children
//			for (int i = 0; i < currentGimElement.Elements.Count; i++)
//			{
//				int childIndex = (int)currentGimElement.Elements[i];
//				IReadOnlyGimElement childElement = gimData.Elements[childIndex];
//				if (childElement.ElementType == GimElementType.shell)
//				{
//					polygon.Merge(BrepShellToPoly(gimData, childIndex));
//				}
//				else if (childElement.ElementType.IsFace())
//				{
//					polygon.Merge(FaceToPolly(gimData, childIndex));
//				}
//				else
//				{
//					throw new ArgumentException(nameof(childIndex));
//				}
//			}

//			return polygon;
//		}

//		private static Polygon BrepSolidToPoly(IReadOnlyGimData gimData, int solidIndex)
//		{
//			Polygon newSolid = new Polygon();
//			for (int i = 0; i < gimData.Elements[solidIndex].Elements.Count; i++)
//			{
//				int childIndex = (int)gimData.Elements[solidIndex].Elements[i];
//				newSolid.Merge(BrepShellToPoly(gimData, childIndex));
//			}
//			return newSolid;
//		}

//		private static Polygon BrepShellToPoly(IReadOnlyGimData gimData, int shellIndex)
//		{
//			// Creates a polygon shell from a shell.
//			Polygon newShell = new Polygon();

//			for (int i = 0; i < gimData.Elements[shellIndex].Elements.Count; i++)
//			{
//				int childIndex = (int)gimData.Elements[shellIndex].Elements[i];
//				newShell.Merge(FaceToPolly(gimData, childIndex));
//			}

//			return newShell;
//		}

//		private static Polygon FaceToPolly(IReadOnlyGimData gimData, int faceIndex)
//		{
//			// Creates a polygon face from a face.
//			List<Wire> wires = new List<Wire>(gimData.Elements[faceIndex].Elements.Count);

//			for (int i = 0; i < gimData.Elements[faceIndex].Elements.Count; i++)
//			{
//				int childIndex = (int)gimData.Elements[faceIndex].Elements[i];
//				if (gimData.Elements[childIndex].ElementType == GimElementType.wire)
//				{
//					wires.Add(WireToPoly(gimData, childIndex));
//				}
//			}

//			return SurfaceToPoly(gimData, faceIndex, wires);
//		}

//		private static Wire WireToPoly(IReadOnlyGimData gimData, int wireIndex)
//		{
//			// Creates a vertex line from a wire.
//			List<List<Vertex>> vertices = new List<List<Vertex>>(gimData.Elements[wireIndex].Elements.Count);

//			for (int i = 0; i < gimData.Elements[wireIndex].Elements.Count; i++)
//			{
//				int childIndex = (int)gimData.Elements[wireIndex].Elements[i];

//				// Skip te last point of the newCurve, beceause this will always be the first point of the next curve.
//				// And for the last curve this will be the first point of the first curve
//				List<Vertex> newCurve = CurveToPoly(gimData, childIndex, false);
//				vertices.Add(newCurve);
//			}

//#if DEBUG
//			// This code is to debug the points we try to match on.
//			List<Vertex> firstAndLastVertices = new List<Vertex>(vertices.Count * 2);
//			foreach (List<Vertex> v in vertices)
//			{
//				firstAndLastVertices.Add(v.First());
//				firstAndLastVertices.Add(v.Last());
//			}
//#endif

//			// See https://docs.cadexchanger.com/sdk/classcadex_1_1_model_data___wire.html
//			// Wire's direct children are edges which must be connected between each other,
//			// i.e. share the same vertices between adjacent edges.
//			// Although there is no strict requirement that edges must be oriented in a tail-to-head order this is strongly recommended.

//			int finalVertexCount = vertices.Sum((list) => list.Count - 1);
//			List<Vertex> newWire = new List<Vertex>(finalVertexCount) { };
//			int timesToVertexLoop = vertices.Count;

//			string outPutWire = "";
//			foreach (List<Vertex> tobewire in vertices)
//				outPutWire += Vertex.GenerateStringRepresentation(tobewire);
//			Console.WriteLine(outPutWire);

//			// For the first edge add all the vertices.
//			newWire.AddRange(vertices[0]);
//			vertices.RemoveAt(0);

//			// Beceause of this we need to wire them up.
//			for (int i = 1; i < timesToVertexLoop; i++)
//			{
//				bool preprend = false;
//				List<Vertex> edgeToAppend;
//				edgeToAppend = vertices.FirstOrDefault((polyCurve) => polyCurve[0].Equals(newWire[^1]));
//				if (edgeToAppend == null)
//				{
//					edgeToAppend = vertices.FirstOrDefault((polyCurve) => polyCurve[^1].Equals(newWire[^1]));
//					if (edgeToAppend == null)
//					{
//						preprend = true;
//						edgeToAppend = vertices.FirstOrDefault((polyCurve) => polyCurve[^1].Equals(newWire[0]));
//						if (edgeToAppend == null)
//						{
//							edgeToAppend = vertices.First((polyCurve) => polyCurve[0].Equals(newWire[0]));
//							edgeToAppend.Reverse();
//						}
//					}
//					edgeToAppend.Reverse();
//				}

//				vertices.Remove(edgeToAppend);

//				// If last don't add last and first vertex
//				if (i == timesToVertexLoop - 1)
//				{
//					if (preprend)
//					{
//						edgeToAppend = edgeToAppend.GetRange(1, edgeToAppend.Count - 2);
//						edgeToAppend.AddRange(newWire);
//						newWire = edgeToAppend;
//					}
//					else
//					{
//						newWire.AddRange(edgeToAppend.GetRange(1, edgeToAppend.Count - 2));
//					}
//				}
//				// if not last add all vertices except for the first.
//				else
//				{
//					if (preprend)
//					{
//						// If were prepending the last is a copy of the first of the new wire.
//						edgeToAppend = edgeToAppend.GetRange(0, edgeToAppend.Count - 1);
//						edgeToAppend.AddRange(newWire);
//						newWire = edgeToAppend;
//					}
//					else
//					{
//						newWire.AddRange(edgeToAppend.GetRange(1, edgeToAppend.Count - 1));
//					}
//				}
//			}

//			return new Wire(newWire, gimData.Elements[wireIndex].Orientation);
//		}
//	}
//}
