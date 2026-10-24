using cadex;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GimUtilities;

namespace GIMtransfer
{
	static partial class CadexWriteElements
	{

//#pragma warning disable IDE0060 // Remove unused parameter
//		private static void UpdateProgressStatus(IReadOnlyGimData gimData, GimProgressStatus progressStatus)
//#pragma warning restore IDE0060 // Remove unused parameter
//		{
//			if (progressStatus != null)
//			{
//				progressStatus.EntityCompletedCallback(nameof(gimData.Elements));
//			}
//		}

//		private static void UpdateProgressStatus(GimProgressStatus progressStatus)
//		{
//			if (progressStatus != null)
//			{
//				progressStatus.EntityCompletedCallback(nameof(IReadOnlyGimData.Elements));
//			}
//		}

//		private static int CalculateModelDataElementCount(IReadOnlyGimData gimData, uint currentElement = 0)
//		{
//			if (gimData.Elements[(int)currentElement].ElementType.IsAppearance())
//				return 0;
//			int count = 1;
//			return gimData.Elements.Count;
//			//foreach (uint gimElement in gimData.Elements[(int)currentElement].Elements) 
//			//{
//			//	count += CalculateModelDataElementCount(gimData, gimElement);
//			//	//if (count == gimData.Elements.Count) return count;
//			//}
//			//return count;
//		}

//        /// <summary>
//        /// Converts GimData into a Cadex ModelData_Model, constructing the model based on the scene graph elements and appearances defined in GimData.
//        /// </summary>
//        /// <remarks>
//        /// The method performs the following steps:
//        /// 
//        /// 1. Initializes progress tracking if a progressStatus object is provided, estimating the workload based on the number of elements in GimData.
//        /// 
//        /// 2. Constructs a dictionary to map IDs to ModelData_SceneGraphElement objects, which will represent the document structure of the model.
//        /// 
//        /// 3. Calls GenerateDocumentStructure to populate the document structure dictionary based on GimData's elements and their relationships.
//        /// 
//        /// 4. Calls GenerateAppearances to create and assign appearances (such as materials and textures) to the elements in the document structure, using additional data from the file destination.
//        /// 
//        /// 5. Initializes a new ModelData_Model object, naming it after the first object in GimData if available; otherwise, it defaults to an unnamed root.
//        /// 
//        /// 6. Checks if the document structure has been successfully created. If it's null, throws an error indicating that no SceneGraphElements were generated.
//        /// 
//        /// 7. Calls GenerateModel to finalize the Cadex model using the constructed document structure and GimData.
//        /// 
//        /// This method is essential for translating GimData's representation of a model into a format that Cadex can use, ensuring all necessary elements and appearances are appropriately represented.
//        /// </remarks>
//        /// <param name="gimData">The GimData to be converted into a Cadex model.</param>
//        /// <param name="fileDestination">The destination path for any files related to appearances.</param>
//        /// <param name="progressStatus">Optional parameter for tracking the progress of the model generation.</param>
//        /// <returns>A ModelData_Model representing the converted GimData.</returns>
//        /// <exception cref="Exception">Throws an error if the document structure is null, indicating a failure in generating SceneGraphElements.</exception>
//        internal static ModelData_Model GetCadexModel(IReadOnlyGimData gimData, string fileDestination, GimProgressStatus progressStatus = null)
//		{
//			if (progressStatus != null)
//			{
//				progressStatus.Create(new Dictionary<string, int>
//				{
//					{ nameof(gimData.Elements), CalculateModelDataElementCount(gimData)}, 
//					//{ nameof(gimData.Objects), gimData.Elements.Count },
//				}
//					);
//			}

//			Dictionary<uint, ModelData_SceneGraphElement> documentStructure = new Dictionary<uint, ModelData_SceneGraphElement>();
//			documentStructure = GenerateDocumentStructure(gimData, documentStructure, progressStatus);
//			documentStructure = GenerateAppearances(gimData, fileDestination, documentStructure, progressStatus);

//			ModelData_Model model;
//			if (gimData.Objects.Count >= 1)
//				model = new ModelData_Model(GetNameSafe(gimData.Objects[0].Name)); // Root
//			else
//				model = new ModelData_Model(); // Unkown root.

//			if (documentStructure == null)
//			{
//				throw GimLogger.Error("(Error: DS) documentStructure is null, no SceneGraphElements are generated.");
//			}

//			return GenerateModel(gimData, documentStructure, model);
//		}

//		private static Dictionary<uint, ModelData_SceneGraphElement> GenerateDocumentStructure(
//			IReadOnlyGimData gimData,
//			Dictionary<uint, ModelData_SceneGraphElement> documentStructure,
//			GimProgressStatus progressStatus,
//			uint element = 0,
//			int parentId = -1)
//		{
//			// This loops over the elements that form the document structure.
//			// This way objects can refrence only Folders and Parts! (Every single occurence)

//			GimElementType objectType = gimData.Elements[(int)element].ElementType;

//			if (!objectType.IsDocument() && !objectType.IsRepresentation())
//				return documentStructure;


//			if (objectType.IsDocument())
//			{

//				IReadOnlyGimObject gimObject;
//				switch (objectType)
//				{
//					// D_root is always element 0 and the first object.
//					case GimElementType.D_root:
//						// element should be 0
//						if (element != 0)
//							throw new Exception("Invalid root element location");
//						break;
//					case GimElementType.D_folder:
//						ModelData_Assembly folder;
//						if (gimData.Objects.Any(o => o.Element == element))
//						{
//							gimObject = gimData.Objects.Single(o => o.Element == element);
//							folder = new ModelData_Assembly(GetNameSafe(gimObject.Name));
//						}
//						else
//						{
//							folder = new ModelData_Assembly();
//						}
//						if (!documentStructure.ContainsKey(element))
//							documentStructure.Add(element, folder);
//						break;
//					case GimElementType.D_link: // This should never be reached
//                        GimLogger.Warn("D_LINK where it should not exist");
//						ModelData_Instance link = new ModelData_Instance();
//						if (!documentStructure.ContainsKey(element))
//							documentStructure.Add(element, link);
//						break;
//					case GimElementType.D_part:
//						ModelData_Part part;
//						if (gimData.Objects.Any(o => o.Element == element))
//						{
//							gimObject = gimData.Objects.Single(o => o.Element == element);
//							part = new ModelData_Part(GetNameSafe(gimObject.Name));
//						}
//						else
//						{
//							part = new ModelData_Part();
//						}
//						if (!documentStructure.ContainsKey(element))
//							documentStructure.Add(element, part);
//						break;
//				}
//			}
//			else if (objectType.IsRepresentation())
//			{

//				// If it's a Brep create brep else poly set.
//				if (gimData.Elements[(int)element].ElementType.IsBrep())
//				{
//					ModelData_BRepRepresentation brep = GetObjectShape(gimData, (int)element, progressStatus);

//					foreach (IReadOnlyGimLocal local in gimData.Elements[(int)element].Locals)
//					{

//						int parent = (int)local.Parent;
//						if (parentId > -1)
//						{
//							if (parent != parentId) continue;
//						}
//						ModelData_Part part = ModelData_Part.Cast(documentStructure[(uint)parent]);
//						part.AddRepresentation(brep);
//						//documentStructure[(uint)paren] = part; // TODO Check if this can be removed <---
//					}
//				}
//				else
//				{
//					ModelData_PolyRepresentation polyRepresentation = new ModelData_PolyRepresentation();
//					foreach (uint polyRepresentationElement in gimData.Elements[(int)element].Elements)
//					{
//						polyRepresentation.Add(GetObjectPoly(gimData, (int)polyRepresentationElement, progressStatus));
//					}

//					foreach (IReadOnlyGimLocal local in gimData.Elements[(int)element].Locals)
//					{

//						int parent = (int)local.Parent;

//						ModelData_Part part = ModelData_Part.Cast(documentStructure[(uint)parent]);
//						part.AddRepresentation(polyRepresentation);
//					}
//				}
//			}
//			else
//			{
//				throw new Exception("Unexpected type: " + objectType);
//			}



//			foreach (uint child in gimData.Elements[(int)element].Elements)
//			{
//				if (documentStructure.ContainsKey(child)) continue;
//				documentStructure = GenerateDocumentStructure(gimData, documentStructure, progressStatus, child, (int)element);
//			}

//			// We added the root, a representation or a document, so update the progress status.
//			UpdateProgressStatus(gimData, progressStatus);

//			return documentStructure;
//		}
//        /// <summary>
//        /// Populates the document structure of a Cadex model with appearances based on GimData, including materials and their assignments to scene graph elements.
//        /// </summary>
//        /// <remarks>
//        /// The method performs the following operations:
//        /// 
//        /// 1. Iterates over the elements in GimData that form the document structure, focusing on materials and their application.
//        /// 
//        /// 2. Determines the type of each element. If the element is not a document, representation, or material, it returns the current document structure without modification.
//        /// 
//        /// 3. If the element is a material:
//        ///    - Creates a ModelData_Appearance using the CreateAppearance method.
//        ///    - Assigns this appearance to all relevant scene graph elements in the document structure. This assignment is based on the parent-child relationships defined in GimData.
//        /// 
//        /// 4. If the element is not a material (e.g., a document or representation element):
//        ///    - Recursively processes each child of the current element to handle nested structures.
//        /// 
//        /// The method ensures that all materials defined in GimData are appropriately created and linked to the corresponding elements within the Cadex model's scene graph.
//        /// </remarks>
//        /// <param name="gimData">The GimData containing elements and their material information.</param>
//        /// <param name="fileDestination">The file destination path for any related files (e.g., textures).</param>
//        /// <param name="documentStructure">The current document structure of the Cadex model to be updated with appearances.</param>
//        /// <param name="progressStatus">Progress tracking object for the appearance generation process.</param>
//        /// <param name="element">The current element being processed, defaulting to 0 (root).</param>
//        /// <returns>The updated document structure with appearances applied.</returns>
//        public static Dictionary<uint, ModelData_SceneGraphElement> GenerateAppearances(
//			IReadOnlyGimData gimData,
//			string fileDestination,
//			Dictionary<uint, ModelData_SceneGraphElement> documentStructure,
//			GimProgressStatus progressStatus,
//			uint element = 0)
//		{
//			// This loops over the elements that form the document structure.
//			// This way objects can refrence only Folders and Parts! (Every single occurence)

//			GimElementType objectType = gimData.Elements[(int)element].ElementType;

//            if (!objectType.IsDocument() && !objectType.IsRepresentation() && !objectType.IsMaterial())
//                return documentStructure;

//            if (objectType.IsMaterial())
//			{
//				ModelData_Appearance appearance = CreateAppearance(gimData, element, progressStatus, fileDestination);


//				foreach (IReadOnlyGimLocal local in gimData.Elements[(int)element].Locals)
//				{
//					uint parent = local.Parent;

//					if (!gimData.Elements[(int)parent].ElementType.IsDocument())
//						continue;

//					ModelData_SceneGraphElement sceneGraphElement = (documentStructure[parent]);
//					sceneGraphElement.SetAppearance(appearance);
//				}
//			}
//			else
//			{
//				foreach (uint child in gimData.Elements[(int)element].Elements)
//				{
//					documentStructure = GenerateAppearances(gimData, fileDestination, documentStructure, progressStatus, child);
//				}
//			}

//			return documentStructure;
//		}

//		private static ModelData_Appearance CreateAppearance(IReadOnlyGimData gimData, uint element, GimProgressStatus progressStatus, string fileDestination)
//		{
//			GimMaterial gimMaterial = gimData.Elements[(int)element].AsMaterial();

//			ModelData_Material material = new ModelData_Material();


//			// Constructe Material.
//			material.SetAmbientColor(new ModelData_Color(gimMaterial.AmbientColour));
//			material.SetDiffuseColor(new ModelData_Color(gimMaterial.DiffuseColour));
//			material.SetEmissionColor(new ModelData_Color(gimMaterial.EmissionColour));
//			material.SetSpecularColor(new ModelData_Color(gimMaterial.SpecularColour));
//			material.SetShininess(gimMaterial.Shininess);

//			ModelData_Appearance appearance = new ModelData_Appearance(material);

//			// We added a material
//			UpdateProgressStatus(gimData, progressStatus);

//			ModelData_TextureSet textureSet = new ModelData_TextureSet();
//			// TODO: Loop and read over Textures.
//			foreach (uint child in gimMaterial.Elements)
//			{
//				// Read texture to cadex.
//#if DEBUG
//				ModelData_FileTexture texture = GetTexture(fileDestination, gimData.Elements[(int)child], true);
//#else
//				ModelData_FileTexture texture = GetTexture(fileDestination, gimData.Elements[(int)child]);
//#endif
//				textureSet.Add(texture);
//				//appearance.Add(texture);
//				// We added a Texture
//				UpdateProgressStatus(gimData, progressStatus);
//			}
//			appearance.SetTextureSet(textureSet);

//			return appearance;
//		}
//        /// <summary>
//        /// Builds a Cadex ModelData_Model from GimData, incorporating the document structure and elements' representations.
//        /// </summary>
//        /// <remarks>
//        /// The method processes GimData to construct a ModelData_Model with the following steps:
//        ///
//        /// 1. Checks the type of the current element. If it's neither a document nor a representation, it returns the model as is.
//        ///
//        /// 2. Initializes a dictionary to track elements that have already been added to the model, if not already provided.
//        ///
//        /// 3. Recursively processes each child of the current element (depth-first approach), skipping already processed children.
//        ///
//        /// 4. If the current element is the root, it returns the model as no further processing is needed for the root.
//        ///
//        /// 5. If the current element is a document type (folder or part):
//        ///    - Iterates over each local transformation (GimLocal) of the element.
//        ///    - Creates a SceneGraphElement based on the current element type:
//        ///      - For folders and parts, retrieves the corresponding SceneGraphElement from the document structure.
//        ///      - Applies transformations and sets references as needed.
//        ///    - Depending on the parent element's type, adds the SceneGraphElement to the model. This could be as a root element, within a folder, or as a reference in a link or part.
//        ///
//        /// 6. Throws errors for unexpected cases, such as a part being encountered where it's not expected.
//        ///
//        /// This method is central to translating the hierarchical and relational structure of GimData into a usable Cadex model, ensuring all elements are correctly positioned and related according to their definitions in GimData.
//        /// </remarks>
//        /// <param name="gimData">The GimData containing the elements and their relationships.</param>
//        /// <param name="documentStructure">A dictionary mapping element IDs to their corresponding SceneGraphElements.</param>
//        /// <param name="model">The ModelData_Model to be built and returned.</param>
//        /// <param name="element">The current element being processed, defaulting to 0 (root).</param>
//        /// <param name="addedElements">A dictionary to track elements already added to the model, used in recursive calls.</param>
//        /// <returns>The constructed ModelData_Model based on the GimData.</returns>
//        private static ModelData_Model GenerateModel(IReadOnlyGimData gimData, Dictionary<uint, ModelData_SceneGraphElement> documentStructure, ModelData_Model model, uint element = 0, Dictionary<uint, ModelData_Model> addedElements = null)
//		{
//			GimElementType currentElementType = gimData.Elements[(int)element].ElementType;
//			if (!currentElementType.IsDocument() && !currentElementType.IsRepresentation())
//				return model;

//			if(addedElements == null)
//            {
//				addedElements = new();
//            }

//			// Depth first
//			foreach (uint child in gimData.Elements[(int)element].Elements)
//			{
//				if (addedElements.ContainsKey(child)) continue;
//				model = GenerateModel(gimData, documentStructure, model, child, addedElements);
//				addedElements.Add(child, model);
//			}

//			if (currentElementType == GimElementType.D_root)
//				return model;

//			if (currentElementType.IsDocument())
//			{
//				foreach (IReadOnlyGimLocal local in gimData.Elements[(int)element].Locals)
//				{
//					ModelData_SceneGraphElement sceneGraphElement;
//					switch (currentElementType)
//					{
//						//case GimElementType.D_link:
//							// If this is an instance add the transformation for the instance.
//							//ModelData_Instance tmp = new ModelData_Instance(documentStructure[element]);
//							//tmp.SetTransformation(ModelData_TransformationFromLocal(local));
//							//sceneGraphElement = tmp;
//							//break;
//						case GimElementType.D_folder:
//						case GimElementType.D_part:
//							sceneGraphElement = documentStructure[element];
//                            ModelData_Instance tmp = new ModelData_Instance(documentStructure[element]);
//                            tmp.SetTransformation(ModelData_TransformationFromLocal(local));
//                            tmp.SetReference(sceneGraphElement); // This edits sceneGraphElement

//                            sceneGraphElement = tmp;
//                            break;
//						default:
//							throw GimLogger.Error("(Error: GM-WSGE) unexpected Scne Graph GimElementType! The element type should be a folder or a part" + currentElementType);
//					}

//					int parent = (int)local.Parent;

//					switch (gimData.Elements[parent].ElementType)
//					{
//						case GimElementType.D_root:
//							model.AddRoot(sceneGraphElement);
//							break;
//						case GimElementType.D_folder:
//							ModelData_Assembly folder = ModelData_Assembly.Cast(documentStructure[(uint)parent]);
//							folder.AddInstance(sceneGraphElement); // This edits sceneGraphElement
//							break;
//						case GimElementType.D_link:
//							ModelData_Instance linkParent = ModelData_Instance.Cast(documentStructure[(uint)parent]);
//							linkParent.SetReference(sceneGraphElement);
//							break;
//						case GimElementType.D_part:
//							throw GimLogger.Error("(Error: GM-WI) A part is not expected here.");
//					}
//				}
//			}
//			return model;
//		}

//		private static ModelData_PolyShape GetObjectPoly(IReadOnlyGimData gimData, int element, GimProgressStatus progressStatus) 
//		{
//			int vertexCount = gimData.Elements[element].ElementDoubles.Count / 3;
//			ReadOnlyCollection<double> vertexArray = gimData.Elements[element].ElementDoubles;
//			List<ModelData_Point> verticesList = new List<ModelData_Point>();
//			for (int i = 0; i < vertexCount; i++)
//			{
//				verticesList.Add(new ModelData_Point(vertexArray[i * 3], vertexArray[i * 3 + 1], vertexArray[i * 3 + 2]));
//			}

//			ModelData_PointList vertices = new ModelData_PointList(verticesList);

//			switch (gimData.Elements[(int)element].ElementType)
//			{
//				case GimElementType.P_polygon:
//					ModelData_IndexedTriangleSet polygon = new ModelData_IndexedTriangleSet();
//					polygon.AddCoordinates(vertices, new ModelData_IntList(gimData.Elements[element].ElementInts));
//					UpdateProgressStatus(gimData, progressStatus);

//					if(gimData.Elements[element].Elements.Count > 0)
//                    {
//						uint materialIndex = gimData.Elements[element].Elements[0];
//						ModelData_Appearance appearance = CreateAppearance(gimData, materialIndex, progressStatus, FileAccess.GetTempFolder());
//						polygon.SetAppearance(appearance);
//                    }
//					return polygon;
//				case GimElementType.P_polyline:
//					ModelData_PolyLineSet polyLineSet = new ModelData_PolyLineSet();
//					polyLineSet.AddPolyline(vertices);
//					UpdateProgressStatus(gimData, progressStatus);

//					if (gimData.Elements[element].Elements.Count > 0)
//					{
//						uint materialIndex = gimData.Elements[element].Elements[0];
//						ModelData_Appearance appearance = CreateAppearance(gimData, materialIndex, progressStatus, FileAccess.GetTempFolder());
//						polyLineSet.SetAppearance(appearance);
//					}
//					return polyLineSet;
//				case GimElementType.P_points:
//					throw GimLogger.Error("Error: NI-PP " +new NotImplementedException());
//				default:
//					throw GimLogger.Error(new ArgumentException("Did not expect: " + gimData.Elements[(int)element].ElementType + ". But a P_ type."));
//			}
//		}

//		internal static ModelData_BRepRepresentation GetObjectShape(IReadOnlyGimData gimData, int element, GimProgressStatus progressStatus)
//		{
//			List<ModelData_Shape> shapes = new List<ModelData_Shape>(gimData.Elements[element].Elements.Count);
//			ModelData_BRepRepresentation brep = new ModelData_BRepRepresentation();

//			for (int i = 0; i < gimData.Elements[element].Elements.Count; i++)
//			{
//				switch (gimData.Elements[(int)gimData.Elements[element].Elements[i]].ElementType)
//				{
//					case GimElementType.B_acorn:
//						break;
//					case GimElementType.B_general:
//						break;
//					case GimElementType.B_sheet:
//						shapes.Add(GetSheet(brep, gimData, (int)gimData.Elements[element].Elements[i], progressStatus));
//						break;
//					case GimElementType.B_solid:
//						shapes.Add(GetSolidBody(brep, gimData, (int)gimData.Elements[element].Elements[i], progressStatus));
//						break;
//					case GimElementType.undefined:
//						break;
//					case GimElementType.B_wireframe:
//						shapes.Add(GetWireframe(brep, gimData, (int)gimData.Elements[element].Elements[i], progressStatus));
//						break;
//				}
//			}

//			for (int i = 0; i < shapes.Count; i++)
//			{
//				brep.Add(shapes[i]);
//			}

//			// We created the b-representation, don't update progress status. This is already done in generate document structure.

//			return brep;
//		}

//		private static Base_UTF16String GetNameSafe(string name)
//		{
//			if (name == null)
//			{
//				return new Base_UTF16String("Unnamed");
//			}
//			return new Base_UTF16String(name);
//		}

//		private static ModelData_Body GetSolidBody(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData, int element, GimProgressStatus progressStatus)
//		{
//			// Set the first element as the default solid child element.
//			uint notAMaterialChild = gimData.Elements[element].Elements[0];

//			// Edge case when the solid body has materials and we need to pick the material.
//			if (gimData.Elements[element].Elements.Count > 1)
//			{
//				int notAMaterialChildCount = 0;

//				foreach (uint childOfBody in gimData.Elements[element].Elements)
//				{
//					if (!gimData.Elements[(int)childOfBody].ElementType.IsMaterial())
//					{
//						notAMaterialChild = childOfBody;
//						notAMaterialChildCount++;
//					}
//				}

//				// If there are multiple elements that are not a material there's a bug.
//				if (notAMaterialChildCount != 1)
//					throw GimLogger.Error("solid - contains a single solid https://docs.cadexchanger.com/sdk/classcadex_1_1_model_data___body.html");
//			}

//			// Update the progress status.
//			UpdateProgressStatus(gimData, progressStatus);

//			// Create and return the ModelData_Body
//			return ModelData_Body.Create(GetSolid(theBRep, gimData, (int)notAMaterialChild, progressStatus));

//		}

//		// wireframe - contains a single edge or a single wire or a flat group of edge(s) and/or wire(s)
//		private static ModelData_Body GetWireframe(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData, int element, GimProgressStatus progressStatus)
//		{
//			ModelData_Body theWireframe;

//			try
//			{
//				theWireframe = ModelData_Body.Create(GetWireOrEdge(theBRep, gimData, (int)gimData.Elements[element].Elements[0], element, progressStatus));
//			}
//			catch
//			{
//				return null;
//			}

//			for (int i = 1; i < gimData.Elements[element].Elements.Count; i++)
//			{
//				try
//				{
//					theWireframe.Append(GetWireOrEdge(theBRep, gimData, (int)gimData.Elements[element].Elements[i], element, progressStatus));
//				}
//				catch (Exception e)
//				{
//					throw GimLogger.Error(e);
//				}
//			}

//			UpdateProgressStatus(gimData, progressStatus);

//			return theWireframe;
//		}

//		private static ModelData_Shape GetWireOrEdge(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData, int elementIndex, int localIndex, GimProgressStatus progressStatus)
//		{
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];
//			if (element.ElementType == GimElementType.wire)
//				return GetWire(theBRep, gimData, elementIndex, localIndex, 
//					Matrix.Ge3DUnitMatrix(), new Vect(0,0,0), progressStatus);
//			else if (element.ElementType.IsCurve())
//				return GetEdge(elementIndex, localIndex, new Dictionary<int, List<ModelData_Vertex>>(),
//					gimData, Matrix.Ge3DUnitMatrix(), new Vect(0, 0, 0), progressStatus);
//			else
//			{
//				throw GimLogger.Error(new ArgumentException("Invalid child " + element.ElementType + ". Should be wire or edge."));
//			}
//		}

//		private static ModelData_Solid GetSolid(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData, int elementIndex, GimProgressStatus progressStatus)
//		{
//			//if (gimData.elements[element].elements.Count > 0 && gimData.elements[(int)gimData.elements[element].elements[0]].elementType == GimElementType.solid)
//			//{
//			//    return GetSolid(gimData, (int)gimData.elements[element].elements[0]);
//			//}

//			// Solid consists of 1 or more shells.
//			IReadOnlyGimElement solidElement = gimData.Elements[elementIndex];
//			List<ModelData_Shell> shells = new List<ModelData_Shell>(solidElement.Elements.Count);
//			var children = solidElement.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);
//			foreach (var childAndLocal in children)
//			{
//				int shellIndex = childAndLocal.Item1;
//				int shellLocalIndex = childAndLocal.Item2;
//				IReadOnlyGimElement shellElement = gimData.Elements[shellIndex];
//				if (!shellElement.ElementType.IsMaterial())

//				{
//					ModelData_Shell shell = GetShell(theBRep, gimData, shellIndex, shellLocalIndex, progressStatus);
//					if (shell != null)
//						shells.Add(shell);
//					else
//					{
//						throw GimLogger.Error("Shell is null");
//					}
//				}
//			}

//			if (shells.Count == 0)
//				return null;

//			ModelData_Solid solid;
//			solid = new ModelData_Solid(shells[0]);

//			for (int i = 1; i < shells.Count; i++)
//			{
//				solid.Append(shells[i]);
//			}

//			UpdateProgressStatus(gimData, progressStatus);

//			return ModelData_Solid.Cast(SetProperties(theBRep, solid, gimData, solidElement));
//		}

//		private static ModelData_Shape SetProperties(ModelData_BRepRepresentation theBrep, ModelData_Shape theShape, IReadOnlyGimData gimData, IReadOnlyGimElement element)
//		{
//			if (theShape == null)
//			{
//				throw GimLogger.Error("Can not generate shape for element " + element.ElementType);
//			}

//			theShape = SetOrientation(theShape, element);
//			SetShapeAppearance(theBrep, theShape, gimData, element);

//			return theShape;
//		}

//		/// <summary>
//		/// Adds an appearance to the ModelData_BRepRepresentation for the ModelData_Shape from the IReadOnlyGimElement. 
//		/// </summary>
//		/// <param name="theBRep">The brep to add the appearance to. </param>
//		/// <param name="theShape">The shape to add the appearance for. </param>
//		/// <param name="element">The element to add the appearance from. </param>
//		private static void SetShapeAppearance(ModelData_BRepRepresentation theBRep, ModelData_Shape theShape, IReadOnlyGimData gimData, IReadOnlyGimElement element)
//		{
//			int materialIndex = CheckIfElementHasAnAppearance(gimData, element);
//			if (materialIndex != -1)
//			{
//				ModelData_Appearance theAppearance = CreateAppearance(gimData, (uint)materialIndex, null, FileAccess.GetTempFolder());
//				theBRep.SetAppearance(theShape, theAppearance);
//			}
//		}

//		private static int CheckIfElementHasAnAppearance(IReadOnlyGimData gimData, IReadOnlyGimElement element)
//		{
//			foreach (uint i in element.Elements)
//			{
//				if (gimData.Elements[(int)i].ElementType.IsMaterial())
//					return (int)i;
//			}

//			return -1;
//		}

//		/// <summary>
//		/// Sets the correct orientation to the ModelData_Shape.
//		/// </summary>
//		/// <param name="shape">The shape to set the orientation for. </param>
//		/// <param name="element">The element to set the orientation from. </param>
//		/// <returns>The shape with the updated orientation. </returns>
//		private static ModelData_Shape SetOrientation(ModelData_Shape shape, IReadOnlyGimElement element)
//		{
//			shape = shape.Oriented(GetOrientation(element));
//			return shape;
//		}

//		private static ModelData_ShapeOrientation GetOrientation(IReadOnlyGimElement element)
//		{
//			if (element.Orientation) { return ModelData_ShapeOrientation.ModelData_SO_Forward; }
//			else { return ModelData_ShapeOrientation.ModelData_SO_Reversed; }
//		}

//		private static ModelData_Shell GetShell(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData, int elementIndex, int localIndex, GimProgressStatus progressStatus)
//		{
//			// Shell consists of 1 or more faces.

//			IReadOnlyGimElement shellElement = gimData.Elements[elementIndex];
//            List<ModelData_Face> faces = new List<ModelData_Face>(shellElement.Elements.Count);

//			var children = shellElement.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);

//            foreach (var childAndLocal in children)
//			{
//				var faceIndex = childAndLocal.Item1;
//				var faceLocalIndex = childAndLocal.Item2;

//				IReadOnlyGimElement faceElement = gimData.Elements[faceIndex];
//				if (!faceElement.ElementType.IsMaterial())
//				{
//					ModelData_Face aFace = GetFace(theBRep, gimData, faceIndex, faceLocalIndex, progressStatus);
//					if (aFace != null)
//					{
//						faces.Add(aFace);
//					}
//					else
//					{
//						throw GimLogger.Error("Face is null");
//					}
//				}
//			}
//			if (faces.Count == 0)
//				return null;

//			// Set shell orientation
//			ModelData_Shell shell;
//			//if (gimData.elements[element].Orientation)
//			//{
//			shell = new ModelData_Shell(faces[0]);
//			//}
//			//else
//			//{
//			//    shell = ModelData_Shell.Cast(new ModelData_Shell(ModelData_Face.Cast(faces[0].Reversed())).Reversed());
//			//}

//			for (int i = 1; i < faces.Count; i++)
//			{
//				shell.Append(faces[i]);
//			}

//			UpdateProgressStatus(gimData, progressStatus);

//			return ModelData_Shell.Cast(SetProperties(theBRep, shell, gimData, shellElement));//shell; // Orientation is set earlier.
//		}

//		private static ModelData_Shape GetSheet(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData,
//			int elementIndex, GimProgressStatus progressStatus)
//		{
//			// Sheet consists of 1 or more faces, and/or 1 or more shells.
//			List<ModelData_Shell> shells = new List<ModelData_Shell>(gimData.Elements[elementIndex].Elements.Count);
//			List<ModelData_Face> faces = new List<ModelData_Face>(gimData.Elements[elementIndex].Elements.Count);

//			IReadOnlyGimElement sheetElement = gimData.Elements[elementIndex];
//			var sheetChildren = sheetElement.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);

//			foreach (var childAndLocal in sheetChildren)
//			{
//				int childElementIndex = childAndLocal.Item1;
//				int childElementLocalIndex = childAndLocal.Item2;

//				IReadOnlyGimElement childElement = gimData.Elements[childElementIndex];
				

//				if (childElement.ElementType == GimElementType.shell)
//				{
//					ModelData_Shell aShell = GetShell(theBRep, gimData, childElementIndex, childElementLocalIndex, progressStatus);
//					if (aShell != null)
//					{
//						shells.Add(aShell);
//					}
//					else
//					{
//						throw GimLogger.Error("Shell is null.");
//					}
//				}
//				else if (childElement.ElementType.IsFace())
//				{
//					ModelData_Face aFace = GetFace(theBRep, gimData, childElementIndex, childElementLocalIndex, progressStatus);
//					if (aFace != null)
//					{
//						faces.Add(aFace);
//					}
//					else
//					{
//						throw GimLogger.Error("Face is null");
//					}
//				}
//			}
//			if (faces.Count == 0 && shells.Count == 0)
//			{
//				GimLogger.Warn("Sheet " + elementIndex + " has no faces and no shells.");
//				return null;
//			}

//			ModelData_Body sheet;
//			int i = 0; int j = 0;

//			// Set sheet orientation
//			//if (gimData.elements[element].Orientation)
//			//{
//			if (shells.Count != 0)
//			{
//				sheet = ModelData_Body.Create(shells[0]);
//				i++;
//			}
//			else
//			{
//				sheet = ModelData_Body.Create(faces[0]);
//				j++;
//			}
//			//}
//			//else
//			//{
//			//    if (shells.Count != 0)
//			//    {
//			//        sheet = ModelData_Body.Cast(ModelData_Body.Create(ModelData_Shell.Cast(shells[0].Reversed())).Reversed());
//			//        i++;
//			//    }
//			//    else
//			//    {
//			//        sheet = ModelData_Body.Cast(ModelData_Body.Create(ModelData_Face.Cast(faces[0].Reversed())).Reversed());
//			//        j++;
//			//    }
//			//}

//			while (i < shells.Count)
//			{
//				sheet.Append(shells[i]);
//				i++;
//			}
//			while (j < faces.Count)
//			{
//				sheet.Append(faces[j]);
//				j++;
//			}

//			UpdateProgressStatus(gimData, progressStatus);

//			return ModelData_Body.Cast(SetProperties(theBRep, sheet, gimData, gimData.Elements[elementIndex]));
//		}

//		private static ModelData_Face GetFace(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData, int elementIndex, int localIndex, GimProgressStatus progressStatus)
//		{
//			// Face consists of 1 surface and 0 or more wires.
//			ModelData_Face face;
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];
//			IReadOnlyGimLocal local = element.Locals[localIndex];
//			int i = 0;
//			if (gimData.Elements[elementIndex].ElementType == GimElementType.Material) { i++; }
//			switch (gimData.Elements[elementIndex].ElementType)
//			{
//				case GimElementType.F_plane:
//					face = new ModelData_Face(MakePlaneSurface(gimData, elementIndex, localIndex));
//					break;
//				case GimElementType.F_cone:
//					face = new ModelData_Face(MakeConicalSurface(gimData, elementIndex, localIndex));
//					break;
//				case GimElementType.F_cilinder:
//					face = new ModelData_Face(MakeCylindricalSurface(gimData, elementIndex, localIndex));
//					break;
//				case GimElementType.F_sphere:
//					face = new ModelData_Face(MakeSphericalSurface(gimData, elementIndex, localIndex));
//					break;
//				case GimElementType.F_toroid:
//					face = new ModelData_Face(MakeToroidalSurface(gimData, elementIndex, localIndex));
//					break;
//				case GimElementType.F_extrusion:
//					{
//						var children = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);
//						int extrusionCurveIndex = children[0].Item1;
//						int extrusionCurveLocalIndex = children[0].Item2;
//						//int extrusionCurve = (int)gimData.Elements[elementIndex].Elements[0];

//						face = new ModelData_Face(
//							MakeLinearExtrusionSurface(gimData, elementIndex, extrusionCurveIndex,
//							localIndex, extrusionCurveLocalIndex, progressStatus));

//						IReadOnlyGimElement extrusionCurve = gimData.Elements[extrusionCurveIndex];
//						var extrusionCurveChildren = extrusionCurve.GetAllChildrenIndexesOrdered(extrusionCurveIndex, (GimData)gimData);


//						//foreach (uint e in gimData.Elements[(int)extrusionCurveIndex].Elements)
//						foreach (var extrusionChildAndLocal in extrusionCurveChildren)
//						{
//							int extrusionChildIndex = extrusionChildAndLocal.Item1;
//							int extrusionChildLocalIndex = extrusionChildAndLocal.Item2;

//							IReadOnlyGimElement extrusionChild = gimData.Elements[extrusionChildIndex];
//							if (extrusionChild.ElementType == GimElementType.wire)
//							{
//								ModelData_Wire wire = GetWire(theBRep, gimData, extrusionChildIndex, extrusionChildLocalIndex,
//                                     Matrix.Ge3DUnitMatrix(), new Vect(0, 0, 0), progressStatus, face);
//								face.Append(wire);
//							}
//						}
//						i++;
//					}
//					break;
//				case GimElementType.F_revolution:
//					{
//						var children = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);
//						int revolutionCurveIndex = children[0].Item1;
//						int revolutionCurveLocalIndex = children[0].Item2;

//						//int revolutionCurveIndex = (int)gimData.Elements[elementIndex].Elements[0];
//						face = new ModelData_Face(MakeRevolutionSurface(gimData, elementIndex,
//							revolutionCurveIndex, localIndex, revolutionCurveLocalIndex, progressStatus));

//                        IReadOnlyGimElement revolutionCurve = gimData.Elements[revolutionCurveIndex];
//                        var revolutionCurveChildren = revolutionCurve.GetAllChildrenIndexesOrdered(revolutionCurveIndex, (GimData)gimData);


//                        foreach (var revolutionCurveChildAndLocal in revolutionCurveChildren)
//                        {
//                            int revolutionCurveChildIndex = revolutionCurveChildAndLocal.Item1;
//                            int revolutionCurveChildLocalIndex = revolutionCurveChildAndLocal.Item2;

//                            IReadOnlyGimElement revolutionChild = gimData.Elements[revolutionCurveChildIndex];
//                            if (revolutionChild.ElementType == GimElementType.wire)
//                            {
//                                ModelData_Wire wire = GetWire(theBRep, gimData, revolutionCurveChildIndex, revolutionCurveChildLocalIndex,
//                                     Matrix.Ge3DUnitMatrix(), new Vect(0, 0, 0), progressStatus, face);
//                                face.Append(wire);
//                            }
//                        }

//      //                  foreach (uint e in gimData.Elements[(int)gimData.Elements[elementIndex].Elements[0]].Elements)
//						//{
//						//	if (gimData.Elements[(int)e].ElementType == GimElementType.wire)
//						//	{
//						//		ModelData_Wire wire = GetWire(theBRep, gimData, (int)e, progressStatus, face);
//						//		face.Append(wire);
//						//	}
//						//}
//						i++;
//					}
//					break;
//				case GimElementType.F_bezier:
//					face = new ModelData_Face(MakeBezierSurface(gimData, elementIndex, localIndex, Matrix.Ge3DUnitMatrix(), new Vect(0,0,0)));
//					break;
//				case GimElementType.F_bspline:
//					ModelData_Surface bSplineSurface = MakeBsplineSurface(gimData.Elements[elementIndex], Matrix.Ge3DUnitMatrix(), new Vect(0,0,0));

//					if (bSplineSurface == null)
//						return null;

//					face = new ModelData_Face(bSplineSurface);
//					break;
//				case GimElementType.F_offset:
//					face = new ModelData_Face(MakeOffsetSurface(gimData, elementIndex, localIndex, progressStatus));
//					i++;
//					break;
//				case GimElementType.F_trimmed:
//					{
//						var children = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);
//						int trimmedCurveIndex = children[0].Item1;
//						int trimmedCurveCurveLocalIndex = children[0].Item2;

//						face = new ModelData_Face(MakeTrimmedSurface(gimData, elementIndex, localIndex,
//							progressStatus));

//						// not to be confused with the type trimmed curve: this is the curve that is a child of F_trimmed
//						IReadOnlyGimElement trimmedCurve = gimData.Elements[trimmedCurveIndex];
//						var trimmedCurveChildren = trimmedCurve.GetAllChildrenIndexesOrdered(trimmedCurveIndex, (GimData)gimData);

//						foreach (var trimmedCurveChildAndLocal in trimmedCurveChildren)
//						{
//							int trimmedCurveChildIndex = trimmedCurveChildAndLocal.Item1;
//							int trimmedCurveChildLocalIndex = trimmedCurveChildAndLocal.Item2;

//							IReadOnlyGimElement trimmedChild = gimData.Elements[trimmedCurveChildIndex];

//							if (trimmedChild.ElementType == GimElementType.wire)
//							{
//								ModelData_Wire wire = GetWire(theBRep, gimData, trimmedCurveChildIndex, trimmedCurveChildLocalIndex,
//                                     Matrix.Ge3DUnitMatrix(), new Vect(0, 0, 0), progressStatus, face);
//								face.Append(wire);
//							}
//						}
//					}
//					i++;
//					break;
//				default:
//					throw GimLogger.Error("(Error: FT): Face type not handled: " + gimData.Elements[elementIndex].ElementType);
//			}

//			if (face != null)
//			{
//				var children = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);
//				foreach(var childAndLocal in children)
//				{
//					int childIndex = childAndLocal.Item1;
//					int childLocalIndex = childAndLocal.Item2;

//					IReadOnlyGimElement childElement = gimData.Elements[childIndex];

//					if (childElement.ElementType == GimElementType.wire)
//					{
//						ModelData_Wire wire = 
//							GetWire(theBRep, gimData, childIndex, childLocalIndex,
//							Matrix.Ge3DUnitMatrix(), new Vect(0, 0, 0), progressStatus, face);
//						face.Append(wire);
//					}
//				}
//			}
//			else
//				return null;

//			UpdateProgressStatus(gimData, progressStatus);

//			return ModelData_Face.Cast(SetProperties(theBRep, face, gimData, gimData.Elements[elementIndex]));
//		}

//		private static ModelData_Wire GetWire(ModelData_BRepRepresentation theBRep, IReadOnlyGimData gimData, 
//			int wireIndex, int wireLocalIndex, Matrix cumulativeTransform,
//			Vect cumulativeOffset, GimProgressStatus progressStatus, ModelData_Face face = null)
//		{
//			Dictionary<int, List<ModelData_Vertex>> vertexLibrary = new Dictionary<int, List<ModelData_Vertex>>();

//            // Wire consists of 1 or more edges.
//            // First 3d edges need to be added.
//            // Then 2d edges, otherwise cadex wil brake the face.
//            IReadOnlyGimElement wireElement = gimData.Elements[wireIndex];
//			IReadOnlyGimLocal wireLocal = wireElement.Locals[wireLocalIndex];

//			Matrix wireMatrix = new((GimLocal)wireLocal);

//			Vect wireOffset = new Vect(
//				wireLocal.Px,
//				wireLocal.Py,
//				wireLocal.Pz
//			);

//			Matrix totalTransform = cumulativeTransform * wireMatrix;
//			Vect totalOffset = cumulativeTransform * wireOffset + cumulativeOffset;

//            List<ModelData_Edge> edges = new List<ModelData_Edge>(wireElement.Elements.Count);

//			var children = wireElement.GetAllChildrenIndexesOrdered(wireIndex, (GimData)gimData);

//			foreach (var childEdgeAndLocal in children)
//			{
//				int edgeIndex = childEdgeAndLocal.Item1;
//				int edgeLocalIndex = childEdgeAndLocal.Item2;

//				IReadOnlyGimElement edgeElement = gimData.Elements[edgeIndex];

//				ModelData_Edge aEdge = null;
//				bool simpleEdge = true;

//				if (edgeElement.Elements.Count != 0)
//				{
//					int seamElementIndex = 0;
//					int seamElementLocalIndex = 0;
//					var edgeChildren = edgeElement.GetAllChildrenIndexesOrdered(edgeIndex, (GimData)gimData);
//					foreach (var edgeChild in edgeChildren)
//					{
//						int elementInElement = edgeChild.Item1;
//						if (gimData.Elements[(int)elementInElement].ElementType.Is2DType())
//						{
//							seamElementIndex = elementInElement;
//							seamElementLocalIndex = edgeChild.Item2;
//                            break;
//						}
//					}
//					if (seamElementIndex != 0)
//					{
//						simpleEdge = false;
//						IReadOnlyGimElement gimElementSeam = gimData.Elements[(int)seamElementIndex];
//						if (gimElementSeam.ElementType == GimElementType.C_bspline2D)
//						{
//							ModelData_Vertex vertexC = MakeVertex(gimElementSeam.ElementDoubles[0], gimElementSeam.ElementDoubles[1], gimElementSeam.ElementDoubles[2], vertexLibrary);
//							ModelData_Vertex vertexD = MakeVertex(gimElementSeam.ElementDoubles[3], gimElementSeam.ElementDoubles[4], gimElementSeam.ElementDoubles[5], vertexLibrary);
//							aEdge = new ModelData_Edge(vertexC, vertexD);
//							var tmp = GetCurve(edgeIndex, edgeLocalIndex, gimData, 
//								totalTransform, totalOffset,  progressStatus);
//							Tuple<double, double> FirstAndLastParam = GetParamFirstAndParamLast(edgeElement);
//							if (FirstAndLastParam != null)
//								aEdge.SetCurve(tmp, FirstAndLastParam.Item1, FirstAndLastParam.Item2);
//							else
//								aEdge.SetCurve(tmp);
//						}
//						else
//						{
//							aEdge = GetEdge(edgeIndex, edgeLocalIndex, vertexLibrary, gimData,
//								totalTransform, totalOffset, progressStatus, face);
//						}

//						ModelData_Curve2d seamCurve = GetCurve2D(gimData, seamElementIndex, seamElementLocalIndex, progressStatus);
//						ModelData_Curve2d seamCurveReversed = seamCurve.Reversed();
//						aEdge.SetPCurve(seamCurve, seamCurveReversed, face, gimElementSeam.ElementDoubles[6], gimElementSeam.ElementDoubles[7]);
//					}
//				}

//				if (simpleEdge)
//					aEdge = GetEdge(edgeIndex, edgeLocalIndex, vertexLibrary, gimData,
//						totalTransform, totalOffset, progressStatus, face);

//				edges.Add(aEdge);
//			}

//			// Add 3d and then 2d edges.
//			ModelData_Wire wire = new ModelData_Wire(edges[0]);
//			for (int i = 1; i < edges.Count; i++)
//				wire.Append(edges[i]);

//			// This function must be called after the creation of an entire wire!!
//			// Else we can get an memory overload after extended usage.
//			vertexLibrary.Clear();

//			//CheckWire(wire, ref gimData.elements[element + 1].elementDoubles[gimData.elements[element + 1].elementDoubles.Length - 2], ref gimData.elements[element + 1].elementDoubles[gimData.elements[element + 1].elementDoubles.Length - 1], ref gimData.elements[element + 2].elementInts[0], ref gimData.elements[element + 2].elementInts[1], ref gimData.elements[element + 3].elementDoubles[gimData.elements[element + 3].elementDoubles.Length - 2], ref gimData.elements[element + 3].elementDoubles[gimData.elements[element + 3].elementDoubles.Length - 1], ref gimData.elements[element + 4].elementInts[0], ref gimData.elements[element + 4].elementInts[1], face);

//			UpdateProgressStatus(gimData, progressStatus);

//			return ModelData_Wire.Cast(SetProperties(theBRep, wire, gimData, gimData.Elements[wireIndex])); // Set orientation.
//		}

//		private static ModelData_Edge GetEdge(int elementIndex, int localIndex,
//			Dictionary<int, List<ModelData_Vertex>> vertexLibrary, IReadOnlyGimData gimData,
//			Matrix cumulativeTransformation, Vect cumulativeOffset, GimProgressStatus progressStatus,
//			ModelData_Face face = null)
//		{
//			ModelData_Edge edge;
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];

//			switch (element.ElementType)
//			{
//				case GimElementType.C_line:
//					double startParam = element.ElementDoubles[0];
//					double endParam = element.ElementDoubles[1];
//					if(startParam == endParam)
//                    {
//                        GimLogger.Warn("Null Edge!");
//                        return null;
//					}
//					ModelData_Curve line = GetCurve(elementIndex, localIndex, gimData, cumulativeTransformation, cumulativeOffset,
//						progressStatus);
//					edge = new ModelData_Edge(line, element.ElementDoubles[0], element.ElementDoubles[1]);
//					break;
//				case GimElementType.C_circle:
//					ModelData_Curve circle = GetCurve(elementIndex, localIndex, gimData, cumulativeTransformation, cumulativeOffset, progressStatus);
//					if (element.ElementDoubles[1] < 0 && element.ElementDoubles[1] > -0.0001)
//						edge = new ModelData_Edge(circle, 0, element.ElementDoubles[2]);
//					else
//						edge = new ModelData_Edge(circle, element.ElementDoubles[1], element.ElementDoubles[2]);
//					break;
//				case GimElementType.C_elipse:
//					ModelData_Curve elipse = GetCurve(elementIndex, localIndex, gimData, cumulativeTransformation, cumulativeOffset, progressStatus);
//					edge = new ModelData_Edge(elipse, element.ElementDoubles[2], element.ElementDoubles[3]);
//					break;
//				case GimElementType.C_bspline:
//					edge = new ModelData_Edge(
//						MakeVertex(element.ElementDoubles[0], element.ElementDoubles[1], element.ElementDoubles[2], vertexLibrary),
//						MakeVertex(element.ElementDoubles[3], element.ElementDoubles[4], element.ElementDoubles[5], vertexLibrary));
//					edge.SetCurve(GetCurve(elementIndex, localIndex, gimData, cumulativeTransformation, cumulativeOffset, progressStatus));
//					break;
//				case GimElementType.C_bezier:
//					edge = new ModelData_Edge(
//                        MakeVertex(element.ElementDoubles[0], element.ElementDoubles[1], element.ElementDoubles[2], vertexLibrary),
//                        MakeVertex(element.ElementDoubles[^5], element.ElementDoubles[^4], element.ElementDoubles[^3], vertexLibrary));
//					edge.SetCurve(GetCurve(elementIndex, localIndex, gimData, cumulativeTransformation, cumulativeOffset, progressStatus));
//					break;
//				case GimElementType.C_parabol:
//				case GimElementType.C_hyperbol:
//					edge = new ModelData_Edge(GetCurve(elementIndex, localIndex, gimData, cumulativeTransformation, cumulativeOffset, progressStatus), element.ElementDoubles[0], element.ElementDoubles[1]);
//					break;
//				case GimElementType.C_line2D:
//					ModelData_Curve2d line2d = GetCurve2D(gimData, elementIndex, localIndex, progressStatus);
//					ModelData_Vertex vertexA = MakeVertex(element.ElementDoubles[0], element.ElementDoubles[1], element.ElementDoubles[2], vertexLibrary);
//					ModelData_Vertex vertexB = MakeVertex(element.ElementDoubles[3], element.ElementDoubles[4], element.ElementDoubles[5], vertexLibrary);
//					edge = new ModelData_Edge(vertexA, vertexB);
//					edge.SetPCurve(line2d, face, element.ElementDoubles[6], element.ElementDoubles[7]);
//					//edge.SetPCurve(line2d, face, double.MinValue, double.MaxValue);
//					break;
//				case GimElementType.C_circle2D:
//					ModelData_Curve2d circle2d = GetCurve2D(gimData, elementIndex, localIndex, progressStatus);
//					edge = new ModelData_Edge(new ModelData_Ellipse(new ModelData_Axis2Placement(), 0, 0), element.ElementDoubles[1], element.ElementDoubles[2]);
//					edge.SetPCurve(circle2d, face);
//					break;
//				case GimElementType.C_elipse2D:
//					ModelData_Curve2d elipse2D = GetCurve2D(gimData, elementIndex, localIndex, progressStatus);
//					edge = new ModelData_Edge(new ModelData_Ellipse(new ModelData_Axis2Placement(), 0, 0), element.ElementDoubles[2], element.ElementDoubles[3]);
//					edge.SetPCurve(elipse2D, face);
//					break;
//				case GimElementType.C_bspline2D:
//					ModelData_Curve2d bspline2d = GetCurve2D(gimData, elementIndex, localIndex, progressStatus);
//					ModelData_Vertex vertexC = MakeVertex(element.ElementDoubles[0], element.ElementDoubles[1], element.ElementDoubles[2], vertexLibrary);
//					ModelData_Vertex vertexD = MakeVertex(element.ElementDoubles[3], element.ElementDoubles[4], element.ElementDoubles[5], vertexLibrary);
//					edge = new ModelData_Edge(vertexC, vertexD);
//					edge.SetPCurve(bspline2d, face);
//					break;
//				case GimElementType.C_parabol2D:
//				case GimElementType.C_hyperbol2D:
//					edge = new ModelData_Edge(null, element.ElementDoubles[0], element.ElementDoubles[1]);
//					edge.SetPCurve(GetCurve2D(gimData, elementIndex, localIndex, progressStatus), face);
//					break;
//				case GimElementType.undefined:
//                    GimLogger.Warn("Null Edge!");
//                    return null;
//				default:
//					throw GimLogger.Error("(Error: EDT): Edge type is not handled: " + element.ElementType);
//			}

//            // Dont update progress beceause edges aren't gim elements.

//            double start = 0, end = 0;
//            var curve = edge.Curve(ref start, ref end);
//			if(curve == null)
//			{
//				var curve2d = edge.PCurve(face, ref start, ref end);
//				if(curve2d == null)
//				{
//                    GimLogger.Warn("Null curve on Edge!");
//                }
//			}

//			double length = ValueRounder.roundDouble(end - start, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
//			if(length == 0)
//			{
//                GimLogger.Warn("Degenerate Edge created!");
//            }

//            return ModelData_Edge.Cast(edge.Oriented(GetOrientation(element)));
//		}

//		private static Tuple<double, double> GetParamFirstAndParamLast(IReadOnlyGimElement element)
//		{
//			return element.ElementType switch
//			{
//				GimElementType.C_line or GimElementType.C_parabol or GimElementType.C_hyperbol or GimElementType.C_parabol2D or GimElementType.C_hyperbol2D
//				=> new Tuple<double, double>(element.ElementDoubles[0], element.ElementDoubles[1]),

//				GimElementType.C_circle => new Tuple<double, double>(element.ElementDoubles[1], element.ElementDoubles[2]),
//				GimElementType.C_elipse => new Tuple<double, double>(element.ElementDoubles[2], element.ElementDoubles[3]),
//				GimElementType.C_line2D or GimElementType.C_circle2D or GimElementType.C_elipse2D or GimElementType.C_bspline2D => new Tuple<double, double>(element.ElementDoubles[6], element.ElementDoubles[7]),
//				GimElementType.undefined or GimElementType.C_bspline or GimElementType.C_bezier => null,
//				_ => throw GimLogger.Error("Edge type not handled: " + element.ElementType),
//			};
//		}

//		private static ModelData_Curve GetCurve(int elementIndex, int localIndex, IReadOnlyGimData gimData,
//            Matrix cumulativeTransformation, Vect cumulativeOffset,
//            GimProgressStatus progressStatus)
//		{
//			//IReadOnlyGimLocal local = null;
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];
//			IReadOnlyGimLocal local = element.Locals[localIndex];

//            // We are creating a curve, but it will be returned immediately. Because it doesn't take long, Update the progress already.
//            UpdateProgressStatus(gimData, progressStatus);

//			switch (element.ElementType)
//			{
//				case GimElementType.C_line:
//					{
//						if (local == null)
//							throw new NullReferenceException();

//						Vect lineBasis = new(local.Px, local.Py, local.Pz);
//						Vect lineDirection = new(local.Zx, local.Zy, local.Zz);

//						Vect finalLineBasis = cumulativeTransformation * lineBasis + cumulativeOffset;
//						Vect finalLineDirection = cumulativeTransformation * lineDirection;

//						ModelData_Point location =
//							new ModelData_Point(finalLineBasis[0], finalLineBasis[1], finalLineBasis[2]); ;
//						ModelData_Direction direction =
//							new ModelData_Direction(finalLineDirection[0], finalLineDirection[1], finalLineDirection[2]);

//						return new ModelData_Line(location, direction);
//					}
//				case GimElementType.C_circle:
//					{
//						Matrix circleMatrix = new(local);
//						Matrix finalCircleMatrix = cumulativeTransformation * circleMatrix;

//                        Vect circleBasis = new(local.Px, local.Py, local.Pz);
//                        Vect finalCircleBasis = cumulativeTransformation * circleBasis + cumulativeOffset;

//                        GimLocal newLocal = new GimLocal();
//						finalCircleMatrix.ApplyToLocal(newLocal);
//						finalCircleBasis.SetLocalBasis(newLocal);

//                        return new ModelData_Circle(Axis2PlacementMakeLocal(newLocal), element.ElementDoubles[0]);
//					}
//				case GimElementType.C_elipse:
//					{
//                        Matrix ellipseMatrix = new(local);
//                        Matrix finalEllipseMatrix = cumulativeTransformation * ellipseMatrix;

//                        Vect ellipseBasis = new(local.Px, local.Py, local.Pz);
//                        Vect finalEllipseBasis = cumulativeTransformation * ellipseBasis + cumulativeOffset;

//                        GimLocal newLocal = new GimLocal();
//                        finalEllipseMatrix.ApplyToLocal(newLocal);
//                        finalEllipseBasis.SetLocalBasis(newLocal);

//                        return new ModelData_Ellipse(Axis2PlacementMakeLocal(local), element.ElementDoubles[0], element.ElementDoubles[1]);
//					}
//				case GimElementType.C_bspline:
//					return MakeBsplineCurve(element, cumulativeTransformation, cumulativeOffset);
//				case GimElementType.C_bezier:
//					return MakeBezierCurve(element, cumulativeTransformation, cumulativeOffset);
//				case GimElementType.C_parabol:
//					{
//                        Matrix parabolaMatrix = new(local);
//                        Matrix finalParabolaMatrix = cumulativeTransformation * parabolaMatrix;
						
//                        Vect parabolaBasis = new(local.Px, local.Py, local.Pz);
//                        Vect finalParabolaBasis = cumulativeTransformation * parabolaBasis + cumulativeOffset;

//                        GimLocal newLocal = new GimLocal();
//                        finalParabolaMatrix.ApplyToLocal(newLocal);
//                        finalParabolaBasis.SetLocalBasis(newLocal);

//                        return new ModelData_Parabola(Axis2PlacementMakeLocal(local), element.ElementDoubles[1]);
//					}
//				case GimElementType.C_hyperbol:
//					{
//                        Matrix hyperBolaMatrix = new(local);
//                        Matrix finalHyperBolaMatrix = cumulativeTransformation * hyperBolaMatrix;

//                        Vect hyperBolaBasis = new(local.Px, local.Py, local.Pz);
//                        Vect finalHyperBolaBasis = cumulativeTransformation * hyperBolaBasis + cumulativeOffset;

//                        GimLocal newLocal = new GimLocal();
//                        finalHyperBolaMatrix.ApplyToLocal(newLocal);
//                        finalHyperBolaBasis.SetLocalBasis(newLocal);

//                        return new ModelData_Hyperbola(Axis2PlacementMakeLocal(local), element.ElementDoubles[2], element.ElementDoubles[3]);
//					}
//				case GimElementType.C_offset:
//					{

//						// TODO ANIS: if offset has significant local, make changes to transformation of basis
//						var children = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);
//						int basisCurveIndex = children[0].Item1;
//						int basisCurveLocalIndex = children[0].Item2;

//						IReadOnlyGimElement basisCurveElement = gimData.Elements[basisCurveIndex];
//                        IReadOnlyGimLocal basisLocal = basisCurveElement.Locals[basisCurveLocalIndex];
						

//						ModelData_Curve basisCurve = GetCurve(basisCurveIndex, basisCurveLocalIndex, gimData,
//							cumulativeTransformation, cumulativeOffset, progressStatus);

//                        Vect lineDirection = new(local.Zx, local.Zy, local.Zz);
//                        Vect finalLineDirection = cumulativeTransformation * lineDirection;

//                        // direction of the offset
//                        ModelData_Direction offsetDirection = 
//                            new ModelData_Direction(finalLineDirection[0], finalLineDirection[1], finalLineDirection[2]);
//                        // value of the offset
//                        double offsetValue = element.ElementDoubles[0];
//						// constructing the offset
//						ModelData_OffsetCurve offsetCurve = new ModelData_OffsetCurve(basisCurve, offsetValue, offsetDirection);

//						return offsetCurve;

//					}
//				case GimElementType.C_trimmed:
//					{
//						var children = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);
//                        int basisCurveIndex = children[0].Item1;
//                        int basisCurveLocalIndex = children[0].Item2;

//                        IReadOnlyGimElement basisCurveElement = gimData.Elements[basisCurveIndex];
//                        IReadOnlyGimLocal basisLocal = basisCurveElement.Locals[basisCurveLocalIndex];

//						ModelData_Curve basisCurve = GetCurve(basisCurveIndex, basisCurveLocalIndex, gimData,
//                            cumulativeTransformation, cumulativeOffset,
//                            progressStatus);
//						// constructing trimmed curve
//						return new ModelData_TrimmedCurve(basisCurve, element.ElementDoubles[0], element.ElementDoubles[1]);

//					}
//				case GimElementType.undefined:
//					return null;
//				default:
//					throw GimLogger.Error("Curve type not handled: " + element.ElementType);
//			}
//		}

//		private static ModelData_Curve2d GetCurve2D(IReadOnlyGimData gimData, int elementIndex, int localIndex, 
//			GimProgressStatus progressStatus) // TODO: What if paren has multiple locals in this element?
//		{
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];
//			IReadOnlyGimLocal local = element.Locals[localIndex];
//            //IReadOnlyGimLocal local = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);

//            // We are creating a 2d curve, but it will be returned immediately, beceause it doesn't take long, do it already.
//            UpdateProgressStatus(progressStatus);

//			switch (element.ElementType)
//			{
//				case GimElementType.C_line2D:
//					ModelData_Point2d location = new ModelData_Point2d(local.Px, local.Py);
//					ModelData_Direction2d direction = new ModelData_Direction2d(local.Zx, local.Zy);
//					return new ModelData_Line2d(location, direction);
//				case GimElementType.C_circle2D:
//					return new ModelData_Circle2d(Axis2Placement2DMakeLocal(local), element.ElementDoubles[0]);
//				case GimElementType.C_elipse2D:
//					return new ModelData_Ellipse2d(Axis2Placement2DMakeLocal(local), element.ElementDoubles[0], element.ElementDoubles[1]);
//				case GimElementType.C_bspline2D:
//					return MakeBSLPlineCurve2d(gimData, elementIndex, localIndex);
//				case GimElementType.C_parabol2D:
//					return new ModelData_Parabola2d(Axis2Placement2DMakeLocal(local), element.ElementDoubles[1]);
//				case GimElementType.C_hyperbol2D:
//					return new ModelData_Hyperbola2d(Axis2Placement2DMakeLocal(local), element.ElementDoubles[2], element.ElementDoubles[3]);
//				case GimElementType.undefined:
//					return null;
//				default:
//					throw GimLogger.Error("Curve type not handled: " + element.ElementType);
//			}
//		}

//		private static ModelData_BSplineCurve2d MakeBSLPlineCurve2d(IReadOnlyGimData gimData, int elementIndex,
//            int localIndex)
//		{

//            IReadOnlyGimElement element = gimData.Elements[elementIndex];
//            IReadOnlyGimLocal gimLocal = element.Locals[localIndex];
//            int multiplicitiesCount = element.ElementInts.Count - 2;
//			ModelData_IntList multiplicities = new ModelData_IntList(multiplicitiesCount);
//			for (int i = 2; i < element.ElementInts.Count; i++)
//			{
//				multiplicities.Add(element.ElementInts[i]);
//			}

//			int knotsCount = element.ElementInts[0];
//			ModelData_DoubleList knots = new ModelData_DoubleList(knotsCount);
//			for (int i = 0; i < knotsCount; i++)
//			{
//				knots.Add(element.ElementDoubles[i + 8]);
//			}

//			int poleCount = (element.ElementDoubles.Count - knotsCount - 8) / 2;
//			ModelData_Point2dList poleArray = new ModelData_Point2dList(poleCount);
//			int counter = 8 + knotsCount;
//			for (int i = 0; i < poleCount; i++)
//			{
//				double x = element.ElementDoubles[counter];
//				counter++;
//				double y = element.ElementDoubles[counter];
//				counter++;
//				poleArray.Add(new ModelData_Point2d(x, y));
//			}

//			int degree = element.ElementInts[1];
//			/*var tmp =*/
//			return new ModelData_BSplineCurve2d(poleArray, knots, multiplicities, degree, CheckIfPeriodic(poleCount, multiplicities.ToArray(), degree));
//			//GimElement tmp2 = Cadex.BRepExplorer.GetBSpline2D(tmp, new GimElement(), 0, 1);
//			//return tmp;

//		}

//		private static ModelData_Surface MakeCylindricalSurface(IReadOnlyGimData gimData, int elementIndex,
//            int localIndex) 
//		{
//            IReadOnlyGimElement element = gimData.Elements[elementIndex];
//            IReadOnlyGimLocal gimLocal = element.Locals[localIndex];
//            //IReadOnlyGimLocal gimLocal = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);
//            double radius = element.ElementDoubles[0];
//			ModelData_Axis3Placement axisPlacement = Axis3PlacementMakeLocal(gimLocal);
//			ModelData_CylindricalSurface aSurface = new ModelData_CylindricalSurface(axisPlacement, radius);
//			return aSurface;
//		}

//		private static ModelData_Surface MakePlaneSurface(IReadOnlyGimData gimData, int elementIndex,
//            int localIndex)
//        {
//            IReadOnlyGimElement element = gimData.Elements[elementIndex];
//            IReadOnlyGimLocal gimLocal = element.Locals[localIndex];
//            //IReadOnlyGimLocal gimLocal = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);
//            //ModelData_Plane aSurface = new ModelData_Plane(PointFromLocal(element.local), DirectionFromLocal(element.local));
//            ModelData_Plane aSurface = new ModelData_Plane(Axis3PlacementMakeLocal(gimLocal));
//			return aSurface;
//		}

//		private static ModelData_Surface MakeConicalSurface(IReadOnlyGimData gimData, int elementIndex,
//            int localIndex)
//        {
//            IReadOnlyGimElement element = gimData.Elements[elementIndex];
//            IReadOnlyGimLocal gimLocal = element.Locals[localIndex];
//            //IReadOnlyGimLocal gimLocal = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);
//            ModelData_Axis3Placement local = Axis3PlacementMakeLocal(gimLocal);
//			ModelData_ConicalSurface aSurface = new ModelData_ConicalSurface(local, element.ElementDoubles[1], element.ElementDoubles[0]);
//			return aSurface;
//		}

//		private static ModelData_Surface MakeToroidalSurface(IReadOnlyGimData gimData, int elementIndex,
//			int localIndex)
//        {
//            IReadOnlyGimElement element = gimData.Elements[elementIndex];
//            IReadOnlyGimLocal gimLocal = element.Locals[localIndex];
//            //IReadOnlyGimLocal gimLocal = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);
//            ModelData_Axis3Placement axisPlacement = Axis3PlacementMakeLocal(gimLocal);
//			ModelData_ToroidalSurface aSurface = new ModelData_ToroidalSurface(axisPlacement, element.ElementDoubles[0], element.ElementDoubles[1]);
//			return aSurface;
//		}

//		private static ModelData_Surface MakeSphericalSurface(IReadOnlyGimData gimData, int elementIndex,
//			int localIndex)
//		{
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];
//			IReadOnlyGimLocal gimLocal = element.Locals[localIndex];
//			//IReadOnlyGimLocal gimLocal = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);
//			ModelData_Axis3Placement axisPlacement = Axis3PlacementMakeLocal(gimLocal);
//			ModelData_SphericalSurface aSurface = new ModelData_SphericalSurface(axisPlacement, element.ElementDoubles[0]);
//			return aSurface;
//		}

//		private static ModelData_Surface MakeLinearExtrusionSurface(IReadOnlyGimData gimData, int elementIndex, 
//			int curveElementIndex, int elementLocalIndex, int curveLocalIndex, 
//			GimProgressStatus progressStatus)
//        {
//            IReadOnlyGimElement element = gimData.Elements[(int)elementIndex];
//            IReadOnlyGimElement curveElement = gimData.Elements[(int)curveElementIndex];

//            IReadOnlyGimLocal gimLocal = element.Locals[elementLocalIndex];
//            IReadOnlyGimLocal curveLocal = curveElement.Locals[curveLocalIndex];
//            //IReadOnlyGimLocal gimLocal = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);
//            ModelData_Curve curve = GetCurve(curveElementIndex, curveLocalIndex, gimData,
//				Matrix.Ge3DUnitMatrix(), new Vect(0,0,0), progressStatus);

//			ModelData_Direction direction = DirectionFromLocal(gimLocal);
//			ModelData_SurfaceOfLinearExtrusion aSurface = new ModelData_SurfaceOfLinearExtrusion(curve, direction);
//			return aSurface;
//		}

//		private static ModelData_Surface MakeRevolutionSurface(IReadOnlyGimData gimData, int elementIndex, 
//			int curveElementIndex, int elementLocalIndex, int curveLocalIndex,
//			GimProgressStatus progressStatus)
//		{
//			IReadOnlyGimElement element = gimData.Elements[(int)elementIndex];
//			IReadOnlyGimElement curveElement = gimData.Elements[(int)curveElementIndex];

//			IReadOnlyGimLocal gimLocal = element.Locals[elementLocalIndex];
//            IReadOnlyGimLocal curveLocal = curveElement.Locals[curveLocalIndex];
//            //IReadOnlyGimLocal gimLocal = element.GetFirstLocalThatBelongsToThisParenOfThisElement(paren);
//            ModelData_Curve curve = GetCurve(curveElementIndex, curveLocalIndex, gimData,
//                Matrix.Ge3DUnitMatrix(), new Vect(0, 0, 0), progressStatus);
//			ModelData_Point point = PointFromLocal(gimLocal);
//			ModelData_Direction direction = DirectionFromLocal(gimLocal);
//			ModelData_SurfaceOfRevolution aSurface = new ModelData_SurfaceOfRevolution(curve, point, direction);
//			return aSurface;
//		}

//		private static ModelData_Surface MakeBezierSurface(IReadOnlyGimData gimData, int elementIndex, int localIndex, Matrix totalTransformation, Vect totalOffset)
//		{
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];
//			int polesCount = element.ElementDoubles.Count / 3;
//			int uPolesCount = element.ElementInts[0];
//			int vPolesCount = polesCount / uPolesCount;
//			ModelData_Point[] poles = MakePoles(polesCount, element.ElementDoubles, totalTransformation, totalOffset);
//			ModelData_BezierSurface aSurface = new ModelData_BezierSurface(poles, uPolesCount, vPolesCount);
//			return aSurface;
//		}

//		private static ModelData_Surface MakeTrimmedSurface(IReadOnlyGimData gimData, int elementIndex, int localIndex,
//			GimProgressStatus progressStatus)
//		{
//			IReadOnlyGimElement element = gimData.Elements[elementIndex];
//			int surfaceIndex = 0;
//			int surfaceLocalIndex = 0;

//			var elementChildren = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);

//			foreach(var childAndLocal in elementChildren)
//			{
//				int childIndex = childAndLocal.Item1;
//				IReadOnlyGimElement childElement = gimData.Elements[(int)childIndex];
//				if (childElement.ElementType.IsFace())
//				{
//					surfaceIndex = childIndex;
//					surfaceLocalIndex = childAndLocal.Item2;
//					break;
//				}
//			}

//			ModelData_Face noWireFace = GetFace(null, gimData, surfaceIndex, surfaceLocalIndex, progressStatus);
//			ModelData_Surface surface = new ModelData_RectangularTrimmedSurface(noWireFace.Surface(), element.ElementDoubles[0],
//				element.ElementDoubles[1], element.ElementDoubles[2], element.ElementDoubles[3]);
//			return surface;
//		}

//		private static ModelData_Surface MakeOffsetSurface(IReadOnlyGimData gimData, int elementIndex, int localIndex,
//			GimProgressStatus progressStatus)
//		{
//            IReadOnlyGimElement element = gimData.Elements[elementIndex];
//            int surfaceIndex = 0;
//            int surfaceLocalIndex = 0;

//            var elementChildren = element.GetAllChildrenIndexesOrdered(elementIndex, (GimData)gimData);

//            foreach (var childAndLocal in elementChildren)
//            {
//                int childIndex = childAndLocal.Item1;
//                IReadOnlyGimElement childElement = gimData.Elements[(int)childIndex];
//                if (childElement.ElementType.IsFace())
//                {
//                    surfaceIndex = childIndex;
//                    surfaceLocalIndex = childAndLocal.Item2;
//                    break;
//                }
//            }
//            ModelData_Face noWireFace = GetFace(null, gimData, surfaceIndex, surfaceLocalIndex, progressStatus);
//			ModelData_Surface surface = new ModelData_OffsetSurface(noWireFace.Surface(), element.ElementDoubles[0]);
//			return surface;
//		}

//		private static ModelData_Point[] MakePoles(int polesCount, IReadOnlyList<double> doubles, 
//			Matrix totalTransformation, Vect totalOffset, int offset = 0)
//		{
//			ModelData_Point[] poles = new ModelData_Point[polesCount];

//			// TODO ANIS: transform points before applying
//			for (int i = 0; i < polesCount; i++)
//			{
//				poles[i] = new ModelData_Point(doubles[offset + i * 3], doubles[offset + i * 3 + 1], doubles[offset + i * 3 + 2]);
//			}
//			return poles;
//		}

//		private static ModelData_Surface MakeBsplineSurface(IReadOnlyGimElement element,
//			Matrix totalTransformation, Vect totalOffset)
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
//			ModelData_Point[] poles = MakePoles(polesCount, element.ElementDoubles, totalTransformation, totalOffset);

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

//			try
//			{
//				ModelData_BSplineSurface surface = new ModelData_BSplineSurface(poles, weights, uPolesCount, vPolesCount, uKnots, vKnots, uMultiplicities, vMultiplicities, uDegree, vDegree, CheckIfPeriodic(uPolesCount, uMultiplicities, uDegree), CheckIfPeriodic(vPolesCount, vMultiplicities, vDegree));
//				return surface;
//			}
//			catch (Exception e)
//			{
//				throw GimLogger.Error("(Error: BSSM)  "+e);
//			}
//		}

//		private static ModelData_BezierCurve MakeBezierCurve(IReadOnlyGimElement element, Matrix totalTransformation, Vect totalOffset)
//		{
//			/*gimElement.elementType = GimElementType.C_bezier;
//						ModelData_BezierCurve aBezier = ModelData_BezierCurve.Cast(theCurve);
//						gimElement.elementInts = new int[1] { aBezier.NumberOfPoles() };
//						poleArray = aBezier.Poles().ToArray();
//						i = 6;
//						gimElement.elementDoubles = new double[poleArray.Length * 3 + 8];
//						foreach (ModelData_Point p in poleArray)
//						{
//							gimElement.elementDoubles[i] = MultipleOf(p.X()); i++;
//							gimElement.elementDoubles[i] = MultipleOf(p.Y()); i++;
//							gimElement.elementDoubles[i] = MultipleOf(p.Z()); i++;
//						}
//						gimElement.elementDoubles[i] = aParamFirst;
//						gimElement.elementDoubles[i + 1] = aParamLast;
//						break; */

//			int degree = element.ElementInts[0]; // can also be calculated from length of double array
//			int poleCount = (element.ElementDoubles.Count - 2) / 3;

//            ModelData_Point[] poles = new ModelData_Point[poleCount];
//            for (int i=0; i < poleCount; i++)
//			{
//				poles[i] = new(
//					element.ElementDoubles[3 * i],
//					element.ElementDoubles[3 * i + 1],
//					element.ElementDoubles[3 * i + 2]);
//			}
//			return new ModelData_BezierCurve(poles);
//		}

//		private static ModelData_BSplineCurve MakeBsplineCurve(IReadOnlyGimElement element, Matrix totalTransformation, Vect totalOffset)
//		{
//			// Read specials (set location)
//			int degree = element.ElementInts[^1];
//			int knotsCount = element.ElementInts[0];
//			int multiplicitiesCount = element.ElementInts.Count - 2;
//			int polesCount = (element.ElementDoubles.Count - (/*2 +*/
//            6) - knotsCount) / 4;

//			// Read all poles
//			ModelData_Point[] poles = MakePoles(polesCount, element.ElementDoubles,totalTransformation, totalOffset, (/*2 +*/6));

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

//			bool isPeriodic = CheckIfPeriodic(polesCount, multiplicities, degree);

//			// TODO: Replace magic number by variable.
//			// 3.675086918879E-08
//			for (int i = 1; i < knots.Length; i++)
//			{
//				if (knots[i - 1] >= knots[i])
//					knots[i] = knots[i - 1] + 3.675086918879E-08;
//			}

//			try
//			{
//				ModelData_BSplineCurve curve = new ModelData_BSplineCurve(poles, weights, knots, multiplicities, degree, isPeriodic);
//				return curve;
//			}
//			catch (Exception e)
//			{
//				throw GimLogger.Error(e);
//			}
//		}

//		private static bool CheckIfPeriodic(int poles, int[] mult, int degree)
//		{
//			if (poles == mult.Sum() - degree - 1)
//				return false;

//			if (poles == mult.Sum() - mult[0])
//				return true;

//			throw GimLogger.Error("Cannot find if it's periodic or not.");
//		}

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

//		private static ModelData_Point PointFromLocal(IReadOnlyGimLocal local)
//		{
//			return new ModelData_Point(local.Px, local.Py, local.Pz);
//		}

//		private static ModelData_Direction DirectionFromLocal(IReadOnlyGimLocal local)
//		{
//			return new ModelData_Direction(local.Zx, local.Zy, local.Zz);
//		}

//		private static ModelData_Axis3Placement Axis3PlacementMakeLocal(IReadOnlyGimLocal local) //TODO this does not take correct direction every time.
//		{
//			return new ModelData_Axis3Placement(Axis2PlacementMakeLocal(local));
//			//ModelData_Direction xDirection = new ModelData_Direction(local.xx / 2147483647.0, local.xy / 2147483647.0, local.xz / 2147483647.0);
//			//ModelData_Direction yDirection = new ModelData_Direction(local.yx / 2147483647.0, local.yy / 2147483647.0, local.yz / 2147483647.0);
//			////ModelData_Direction zDirection = new ModelData_Direction(local.zx / 2147483647.0, local.zy / 2147483647.0, local.zz / 2147483647.0);

//			//ModelData_Point point = new ModelData_Point(local.px, local.py, local.pz);
//			//ModelData_Direction axis = CrossProduct(xDirection, yDirection);
//			//ModelData_Axis3Placement axis3Placement = new ModelData_Axis3Placement(point, axis, xDirection);

//			//if (axis3Placement.IsRightHanded() != local.rightHanded)
//			//    Console.WriteLine("Not same: " + local.rightHanded);
//			//return axis3Placement;


//			//return axis3Placement; // TODO check if the correct directions are read. 
//		}

//		/// <summary>
//		/// Creates a ModelData_Axis2Placement from a IReadOnlyGimLocal.
//		/// </summary>
//		/// <param name="local">The local to create the Axis2Placement from.</param>
//		/// <returns>The Axis2Placement that is equal to the local.</returns>
//		private static ModelData_Axis2Placement Axis2PlacementMakeLocal(IReadOnlyGimLocal local)
//		{
//			ModelData_Direction xDirection = new ModelData_Direction(local.Xx, local.Xy, local.Xz);
//			ModelData_Direction yDirection = new ModelData_Direction(local.Yx, local.Yy, local.Yz);

//			ModelData_Point point = new ModelData_Point(local.Px, local.Py, local.Pz);
//			ModelData_Direction axis;
//			//if (local.zz == int.MinValue)
//			axis = CrossProduct(xDirection, yDirection);
//			//else
//			//	axis = new ModelData_Direction(local.zx / 2147483647.0, local.zy / 2147483647.0, local.zz / 2147483647.0);
//			return new ModelData_Axis2Placement(point, axis, xDirection);
//		}

//		/// <summary>
//		/// Creates a ModelData_Axis2Placement2d from a IReadOnlyGimLocal.
//		/// </summary>
//		/// <param name="local">The local to create the Axis2Placement2d from.</param>
//		/// <returns>The Axis2Placement2d that is equal to the local.</returns>
//		private static ModelData_Axis2Placement2d Axis2Placement2DMakeLocal(IReadOnlyGimLocal local)
//		{
//			ModelData_Direction2d zDirection = new ModelData_Direction2d(local.Zx, local.Zy);

//			ModelData_Point2d point = new ModelData_Point2d(local.Px, local.Py);
//			return new ModelData_Axis2Placement2d(point, zDirection);
//		}

//		private static ModelData_Transformation ModelData_TransformationFromLocal(IReadOnlyGimLocal local)
//		{
//			ModelData_Transformation transform = new ModelData_Transformation();
//			if (local == null)
//				return transform;
//			transform.SetTranslationPart(local.Px, local.Py, local.Pz);

//			if (local.RightHanded)
//			{
//                transform.SetRotationPart(local.Xx, local.Xy, local.Xz,
//					local.Yx, local.Yy, local.Yz,
//					local.Zx, local.Zy, local.Zz);
//            }
//			else
//			{
//                transform.SetRotationPart(local.Xx, local.Xy, local.Xz,
//					local.Yx, local.Yy, local.Yz,
//					local.Zx, local.Zy, local.Zz);
//				transform.SetScaleFactor(-1);
//            }
//			return transform;
//		}

//		private static ModelData_Vertex MakeVertex(double x, double y, double z, Dictionary<int, List<ModelData_Vertex>> vertexLibrary)
//		{
//			int hashcode;
//			unchecked // Overflow is fine, just wrap
//			{
//				hashcode = x.GetHashCode() * 71 + y.GetHashCode() * 23 + z.GetHashCode();
//			}
//			if (vertexLibrary.ContainsKey(hashcode))
//			{
//				List<ModelData_Vertex> vertices = vertexLibrary[hashcode];
//				foreach (ModelData_Vertex v in vertices)
//					if (v.Point().X() == x && v.Point().Y() == y && v.Point().Z() == z)
//						return v;
//			}
//			// If the foreach is escaped or the if statement is false, add new vertex
//			ModelData_Vertex newVertex = new ModelData_Vertex(new ModelData_Point(x, y, z));

//			// Add vertex to library.
//			if (vertexLibrary.ContainsKey(hashcode))
//				vertexLibrary[hashcode].Add(newVertex);
//			else
//				vertexLibrary.Add(hashcode, new List<ModelData_Vertex>() { newVertex });

//			return newVertex;
//		}

//		private static ModelData_Direction CrossProduct(ModelData_Direction a, ModelData_Direction b)
//		{
//			return new ModelData_Direction(a.Y() * b.Z() - a.Z() * b.Y(), a.Z() * b.X() - a.X() * b.Z(), a.X() * b.Y() - a.Y() * b.X());
//		}
	}
}
