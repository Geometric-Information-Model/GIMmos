//using GIMcompress;
//using GmshNet;
//using System.Collections.Generic;
//using GimUtilities;

//namespace GIMtransfer.BrepToPoly
//{
//	// Gmsh internal cad engine
//	// This cad engine has a lot less functions than occ.
//	// using geo = Gmsh.Model.Geo;

//	// Open cascade engine (Open Cascade is similar to Cadex)
//	using occ = Gmsh.Model.Occ;

//	/// <summary>
//	/// The static class that handles calls to the GMsh.net repository
//	/// https://github.com/Sansoeko/Gmsh.Net
//	/// </summary>
//	internal static partial class GimGMSH
//	{
//		/// <summary>
//		/// One gmsh function should run at the same time.
//		/// </summary>
//		private static object gmshLock = new object();


//		/// <summary>
//		/// The amount of detail / maximum differences.
//		/// Smaller values take longer and uses (a lot) more memory.
//		/// Larger values make more inprecise meshes.
//		/// </summary>
//		private const double meshingConstraint = 1e0; // 1e0 = 1.0 = 1mm

//		/// <summary>
//		/// Adds a polygon representation to each object that has a boundary representation.
//		/// </summary>
//		/// <param name="gimData">The gimData to add polygon representations to.</param>
//		/// <returns></returns>
//		internal static GimData GimDataAddPolygonRepresentation(GimData gimData)
//		{
//			for (int i = 0; i < gimData.elements.Count; i++)
//			{
//				// If we find a b-representation create a polygon representation for it's parent.
//				if (gimData.elements[i].ElementType.IsRepresentation() && gimData.elements[i].ElementType == GimElementType.R_BRep)
//				{
//					// TODO: Don't add if it already has a polygon representation (GimElementType.R_Poly).
//					if (gimData.Elements[(int)gimData.elements[i].locals[0].parent].ElementType != GimElementType.D_part)
//						GimLogger.Warn("Not a part! Should be a part");

//					PartToGmsh(gimData, (int)gimData.elements[i].locals[0].parent);
//				}
//			}
//			// Been there, done that.
//			return gimData;
//		}

//		/// <summary>
//		/// Add the part to the occ engine in GMsh.
//		/// </summary>
//		/// <param name="gimData">The gimData to work with. </param>
//		/// <param name="index">The index of the part. </param>
//		/// <returns></returns>
//		private static GimData PartToGmsh(GimData gimData, int index)
//		{
//			// Lookup table for getting the tags that relate to the gimElement. 
//			// This is needed to lookup internal GMsh tags for the children of complexer tags.
//			// tag a tag value should be between 1 and int.MaxValue.
//			// There're 4 types of tags: pointTag, curveTag, surfaceTag & volumeTag.
//			// For every type of tag every number should occur once.
//			// For different types of tags numbers can overlap. 
//			// Which kind of tag type you use should be clear from context
//			Dictionary<IReadOnlyGimElement, int> elementToTag = new Dictionary<IReadOnlyGimElement, int>();

//			// The polygon element.
//			GimElement mesh;

//			// Lock the GmshLock.
//			lock (gmshLock)
//			{
//				// Initialize the Gmsh functions.
//				Initialize();

//				Gmsh.Option.SetNumber("General.Terminal", 1);

//				// Create model
//				Gmsh.Model.Add("FileTest");

//				// Add as Open Cascade (occ).
//				AddPartToGmsh(gimData, elementToTag, index, -1);

//				// Synchronize occ to GMsh.
//				occ.Synchronize();

//				// Convert to Mesh.
//				Gmsh.Model.Mesh.Generate(2);

//				// Read mesh back to a new GimData.
//				mesh = ReadPolygonToGimData();

//				Finalize();
//			}

//			GimElement polygonRepresentation = new GimElement() { elementType = GimElementType.R_Poly };
//			int polygonRepresentationIndex = gimData.elements.Count;
//			gimData.elements.Add(polygonRepresentation);
//			gimData.elements[index].elements.Add((uint)polygonRepresentationIndex);
//			gimData.elements[polygonRepresentationIndex].Locals.Add(new GimLocal() { parent = (uint)index });

//			gimData.elements.Add(mesh);
//			gimData.elements[polygonRepresentationIndex].elements.Add((uint)gimData.elements.Count - 1);
//			gimData.elements[^1].Locals.Add(new GimLocal() { parent = (uint)polygonRepresentationIndex });

//			return gimData;
//		}

//		private static GimElement ReadPolygonToGimData()
//		{
//			// elementType 2 = 3-node triangle.
//			int elementType = 2;

//			// faceType 3 = triangular faces
//			int faceType = 3;

//			// Remove nodes that are duplicates.
//			Gmsh.Model.Mesh.RemoveDuplicateNodes();

//			// Renumber nodes so we get a correct index triangle list.
//			Gmsh.Model.Mesh.RenumberNodes();

//			// Get the nodeTags (Indexes for every node.)
//			long[] nodeTags = Gmsh.Model.Mesh.GetElementFaceNodes(elementType, faceType);

//			GimElement polyElement = new GimElement() { elementType = GimElementType.P_polygon };

//			// maxNodeTag is also equal to the amount of nodes because we renumbered them.
//			int maxNodeTag = -1;
//			polyElement.elementInts = new int[nodeTags.Length];
//			for (int i = 0; i < nodeTags.Length; i++)
//			{
//				polyElement.elementInts[i] = (int)nodeTags[i];
//				if (polyElement.elementInts[i] > maxNodeTag)
//					maxNodeTag = polyElement.elementInts[i];
//			}

//			polyElement.elementDoubles = new double[(maxNodeTag + 1) * 3];
//			for (int i = 1; i <= maxNodeTag; i++)
//			{
//				double[] newVector;
//				double[] newVectorParametric;
//				Gmsh.Model.Mesh.GetNode(i, out newVector, out newVectorParametric);
//				polyElement.elementDoubles[i * 3] = newVector[0];
//				polyElement.elementDoubles[i * 3 + 1] = newVector[1];
//				polyElement.elementDoubles[i * 3 + 2] = newVector[2];
//			}

//			return polyElement;
//		}

//		/// <summary>
//		/// Directly open a file in the Gmsh engine.
//		/// WARNING: This does nothing but reading the file and writing the vectors to the GimLogger.
//		/// </summary>
//		/// <param name="fullPath">The path to the file.</param>
//		internal static void OpenWithGmsh(string fullPath)
//		{
//			lock (gmshLock)
//			{
//				Initialize();

//				// Read file.
//				Gmsh.Open(fullPath);

//				// Generate model
//				Gmsh.Model.Mesh.Generate(2);

//				// TODO: Do something with the meshdata!

//				int elementType = 2;
//				int faceType = 3;

//				long[] elementFaceNodes = Gmsh.Model.Mesh.GetElementFaceNodes(elementType, faceType);
//				GimLogger.Info("GetElementFaceNodes");
//				GimLogger.Info(elementFaceNodes.Length.ToString());
//				foreach (int e in elementFaceNodes)
//				{
//					GimLogger.Info(e.ToString());
//				}

//				List<Vector> nodes = new List<Vector>(elementFaceNodes.Length);
//				foreach (long nodeTag in elementFaceNodes)
//				{
//					double[] newVector;
//					double[] newVectorParametric;
//					Gmsh.Model.Mesh.GetNode(nodeTag, out newVector, out newVectorParametric);
//					nodes.Add(new Vector(newVector));
//				}

//				foreach (Vector v in nodes)
//				{
//					GimLogger.Info(v);
//				}

//				Finalize();
//			}
//		}

//		/// <summary>
//		/// Finalize working with Gmsh.
//		/// </summary>
//#pragma warning disable CS0465 // Introducing a 'Finalize' method can interfere with destructor invocation
//		private static void Finalize()
//#pragma warning restore CS0465 // Introducing a 'Finalize' method can interfere with destructor invocation
//		{
//			// TODO: Making this a direct function for this class could make it possible to finalize and initialize independent terminals.
//			// Finalize Gmsh functions.
//			Gmsh.Clear();
//			Gmsh.Finalize();
//		}

//		/// <summary>
//		/// Initialize working with Gmsh.
//		/// </summary>
//		private static void Initialize()
//		{
//			// TODO: Making this a direct function for this class could make it possible to finalize and initialize independent terminals.
//			Gmsh.Initialize();
//		}
//	}
//}
