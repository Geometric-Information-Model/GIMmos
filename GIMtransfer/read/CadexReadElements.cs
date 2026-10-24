using cadex;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GimUtilities;

namespace GIMtransfer
{
	internal partial class Cadex
	{
		/// <summary>
		/// Defines a visitor of the scene graph elements.
		/// 
		/// Visits directly each part and calls B-Rep representation and polygon representation exploring if a part has one
		///
		///	The Visitor follows a hierarchical visitor pattern(see http://c2.com/cgi/wiki?HierarchicalVisitorPattern) 
		///	what enables to track entering and leaving composite entities - assemblies and instances.
		///
		/// Methods VisitEnter() returns true if the child elements of the assembly or a referred element of the instance should be visited. If the method returns false then they will not be visited. The method VisitLeave() will always be called.
		/// </summary>
//		internal class PartBRepVisitor : ModelData_Model.VoidElementVisitor
//		{	
//			// Set to collect unique shapes from model
//			protected GimData gimData;
//            protected readonly List<Task<GimDataPartial>> partialGimDatas;
//            protected readonly Dictionary<ModelData_SceneGraphElement, uint> refrencesDictionary;
//            protected readonly bool generatePolygonsOnReading;
//            protected readonly bool doAsyncBrepExploring;
//            protected LinkTwinsProperties linkTwinsProperties;
//            protected Dictionary<uint, uint> elemToObject = new Dictionary<uint, uint>();
//            protected HashSet<string> addedObjectNames = new HashSet<string>();
//            /// <summary>
//            /// If set to true the PartBRepVisitor will write the scenegraph elements strucutre to the console.
//            /// </summary>
//            protected readonly bool writeDocumentStructureInformation;

//			public PartBRepVisitor(bool generatePolygonsOnReading, bool writeDocumentStructureInformation, bool doAsyncBrepExploring = true, GimProgressStatus progressStatus = null)
//			{
//				this.generatePolygonsOnReading = generatePolygonsOnReading;
//				this.writeDocumentStructureInformation = writeDocumentStructureInformation;
//				this.doAsyncBrepExploring = doAsyncBrepExploring;
//				gimData = new GimData();
//				gimData.Elements.Add(new GimElement());
//				gimData.Elements[0].elementType = GimElementType.D_root;
//				refrencesDictionary = new Dictionary<ModelData_SceneGraphElement, uint>();

//				if (doAsyncBrepExploring)
//				{
//					partialGimDatas = new List<Task<GimDataPartial>>();
//					linkTwinsProperties = LinkTwinsProperties.DoNothing();
//				}
//				else
//				{
//					linkTwinsProperties = LinkTwinsProperties.StartProperties();
//				}
//			}

//			public GimData GimData
//			{
//				get
//				{
//					// Handle finishing all async tasks if we where async exploring.
//					if (doAsyncBrepExploring)
//					{
//						int busyThreadCount = 0;
//						while (partialGimDatas.Any((t) => t.Status == TaskStatus.Created))
//						{
//							if (partialGimDatas.Any((t) => t.IsFaulted))
//								throw new Exception();
//							if (partialGimDatas.Any((t) => t.Status == TaskStatus.Created))
//							{
//								Task newTask = partialGimDatas.First((t) => t.Status == TaskStatus.Created);
//								newTask.Start();
//							}
//							busyThreadCount = partialGimDatas.Count((t) => t.Status == TaskStatus.Running) + partialGimDatas.Count((t) => t.Status == TaskStatus.WaitingToRun);
//						}

//						for (int i = 0; i < partialGimDatas.Count; i++)
//						{
//							Task<GimDataPartial> partialGimDataTask = partialGimDatas[i];
//							GimDataPartial partialGimData = partialGimDataTask.Result;

//							uint oldParentInPartialGimData;

//							if(gimData.Elements.Count > 117600)	
//							{
//								string debug = "t";
//							} 

//							try
//							{
//								oldParentInPartialGimData = partialGimData.Elements[0].locals[0].parent;
//							}
//							catch
//							{
//								// If the root of the partial does not have a correct local pointing to the parent, we add it.
//								oldParentInPartialGimData = (uint)partialGimData.parent;
//								partialGimData.Elements[0].Locals.Add(new() { parent = oldParentInPartialGimData});
//							}

//							try
//							{
//								gimData.AddPartialGimDataToThis(partialGimData, 0, (uint)partialGimData.parent, oldParentInPartialGimData);
//							}
//							catch
//							{
//								if (partialGimData.Elements.Count == 0)
//									GimLogger.Warn("Empty partial GimData. This PartialGimData didn't store anything or it has failed to read in a earlier stage.");
//								else
//									throw;
//							}

//							// Remove refrence so C# will know that the memory isn't referenced anymore and the garbage collector will do it's work.
//							partialGimDatas[i] = null;
//						}
//					}

//					// If we did not link the twins while reading we need to do that now.
//					if (linkTwinsProperties.doNothing)
//					{
//						// Still need to link the twins if we read them async.
//						// Otherwise it's done while reading.
//						//gimData = Twins.LinkTwins(gimData);
//					}
//					// Otherwise if we did TwinLinking we need to end it.
//					else
//					{
//						// End the linking of twins.
//						gimData = Twins.LinkTwinsEnd(gimData, linkTwinsProperties);
//					}

//					return gimData;
//				}
//			}

//			/// <summary>
//			/// Gets called by cadex for every part (first element of a physical thing)
//			/// </summary>
//			/// <param name="thePart">The part to explore.</param>
//			public override void Apply(ModelData_Part thePart)
//			{
//                GimLocal local;
//                if (lastLinks == null || lastLinks.Count == 0 || documentOrder.Last() != lastLinks.Last().parent) // Link no longer applicable for the current part
//                {
//                    local = new GimLocal() { parent = documentOrder.Last() };
//                }
//                else
//                {
//                    local = lastLinks.Last();
//                }
//				gimData.Elements[(int)documentOrder.Last()].elements.Add((uint)gimData.Elements.Count); // Add self to parent

//				if (!refrencesDictionary.ContainsKey(thePart))
//					refrencesDictionary.Add(thePart, (uint)gimData.Elements.Count);

//				// Add self to document order
//				documentOrder.Add((uint)gimData.Elements.Count);

//				uint parent = (uint)gimData.Elements.Count;
//				gimData.Elements.Add(new GimElement()
//				{
//					elementType = GimElementType.D_part,
//					locals = new List<GimLocal>() { local }
//				});

//				string partName = thePart.Name().ToString();

//                if (partName != "" && partName.ToLower() != "body")
//                {
//                    if (addedObjectNames.Contains(partName))
//                    {
//                        LoopTillParent(partName, (uint)gimData.Elements.Count - 1);
//                    }
//                    else
//                    {
//                        addedObjectNames.Add(partName);
//                    }
//                    GimObject gimObject = new GimObject
//					{
//						// the next element is the definition of this object
//						element = (uint)gimData.Elements.Count - 1,
//						name = partName
//                    };
//					gimData.Objects.Add(gimObject); // add new object to gimData
//					elemToObject.Add((uint)gimData.Elements.Count - 1, (uint)gimData.Objects.Count - 1);
//				}

				


//				// Explore Colours and/or textures
//				// TODO: Implement this.
//				gimData = BRepExplorer.ExploreAppearance(thePart.Appearance(), gimData, parent);

//				//WriteNameToConsole(thePart, "part");

//				// Read BRep+
//				ModelData_BRepRepresentation aBRep = thePart.BRepRepresentation(); // Get tree structure of part
				
//				// TODO: Is this correct? / What is this?
//				ModelData_PolyRepresentation aPoly = thePart.PolyRepresentation(ModelData_RepresentationMask.ModelData_RM_Poly); // Get tree structure of part
				
//				if (aBRep != null)
//				{
//					BRepExplorer explorer = new BRepExplorer();

//					if (doAsyncBrepExploring)
//						partialGimDatas.Add(explorer.ExploreBRepAsync(aBRep, new GimDataPartial((int)parent), parent));
//					else
//						gimData = explorer.ExploreBRep(aBRep, gimData, parent, ref linkTwinsProperties);
//				}

//				if (aPoly != null)
//				{
//					gimData = PolyExplorer.ExplorePoly(aPoly, gimData, parent);
//				}
//				else if (aBRep != null && generatePolygonsOnReading) // Ideally a brep null check shouldn't be needed here, however sometimes we find parts without brep or poly representations. This should be investigated further.
//				{
//					// Do mesher magic.
//					ModelAlgo_BRepMesherParameters parameters = new ModelAlgo_BRepMesherParameters(ModelAlgo_BRepMesherParameters.Granularity.Fine);
//					ModelAlgo_BRepMesher bRepMesher = new ModelAlgo_BRepMesher(parameters);
//					ModelData_PolyRepresentation aPolyRep = bRepMesher.Compute(aBRep);
//					gimData = PolyExplorer.ExplorePoly(aPolyRep, gimData, parent);
//					bRepMesher.Dispose();
//					parameters.Dispose();

//				}

//				if (aPoly == null && aBRep == null)
//				{
//					GimLogger.Warn("Found part without representation!");
//				}

//				documentOrder.RemoveAt(documentOrder.Count - 1);
//				// If the brep is an root instance.
//				//if (documentOrder.Last() == 0)
//				//{
//				//	GimLocal l = new GimLocal();
//				//	l.parent = 0;
//				//	gimData.elements[(int)gimData.objects.Last().element].locals.Add(l);
//				//}
//			}

//			// Part hierarchy.
//			// Used to check the hierarchy of the parts.
//			ModelData_Transformation currentOffset;
//			internal readonly List<uint> documentOrder = new List<uint>() { 0 };
//            internal List<GimLocal> lastLinks = new List<GimLocal>();
//			/// <summary>
//			/// Called by Cadex for hierarchical elements (parts, folders etc...)
//			/// 
//			/// </summary>
//			/// <param name="theAssembly">The Hierarchical element</param>
//			/// <returns></returns>
//			public override bool VisitEnter(ModelData_Assembly theAssembly) // Called by cadex
//			{
//                GimLocal local;
//                if (lastLinks == null || lastLinks.Count == 0 || documentOrder.Last() != lastLinks.Last().parent) // Link no longer applicable for the current part
//                {
//                    local = new GimLocal() { parent = documentOrder.Last() };
//                }
//                else
//                {
//                    local = lastLinks.Last();
//                }
//                gimData.Elements[(int)documentOrder.Last()].elements.Add((uint)gimData.Elements.Count); // Add self to parent
//				refrencesDictionary.Add(theAssembly, (uint)gimData.Elements.Count);

//				// Add self to document order
//				documentOrder.Add((uint)gimData.Elements.Count);

//				//WriteNameToConsole(theAssembly, "assembly");

//				// Object with name
//				if (theAssembly.Name().ToString() != "" && theAssembly.Name().ToString().ToLower() != "body")
//				{
//					GimObject gimObject = new GimObject()
//					{
//						name = theAssembly.Name().ToString(),
//						element = (uint)gimData.Elements.Count
//					};
//					gimData.Objects.Add(gimObject);
//					elemToObject.Add((uint)gimData.Elements.Count,(uint)gimData.Objects.Count-1);
//				}


//				// Make folder (assembly) element
//				GimElement element = new GimElement()
//				{
//					elementType = GimElementType.D_folder,
//					locals = new List<GimLocal>() { local }
//				};
//				gimData.Elements.Add(element);

//				return true;
//			}

//			/// <summary>
//			/// Called by cadex.
//			/// </summary>
//			/// <param name="theAssembly"> The assembly we exit.</param>
//			public override void VisitLeave(ModelData_Assembly theAssembly)
//			{
//                // Leaving SceneGraphElement, remove self from document order
//                documentOrder.RemoveAt(documentOrder.Count - 1);

//#if DEBUG
//                if (documentOrder.Count == 1)
//                {
//                    if (documentOrder.Last() != 0)
//                    {
//                        throw new Exception("Root element is not root!");
//                    }
//                }
//#endif
//				base.VisitLeave(theAssembly);
//			}

//			/// <summary>
//			/// Called by cadex. 
//			/// </summary>
//			/// <param name="theInstance">The instance we enter.</param>
//			/// <returns></returns>
//			public override bool VisitEnter(ModelData_Instance theInstance)
//			{
//				// Create local with offset and correct parent.
//				currentOffset = theInstance.Transformation(); // read offset
//				lastLinks.Add(BRepExplorer.NewGimPlane(currentOffset));
//                lastLinks.Last().parent = documentOrder.Last();
				
//				if (theInstance.Reference() != null && refrencesDictionary.ContainsKey(theInstance.Reference()))
//				{
//                    // Add self to child
//                    var reference = theInstance.Reference();

//                    gimData.Elements[(int)refrencesDictionary[reference]].locals.Add(lastLinks.Last());

//                    // Add child to parent (since self doesn't exist)
//                    gimData.Elements[(int)lastLinks.Last().parent].elements.Add(refrencesDictionary[reference]);

//                    return false;
//				}
//				return true;
//			}

//            public override void VisitLeave(ModelData_Instance theInstance) // Called by cadex
//            {
//                lastLinks.RemoveAt(lastLinks.Count - 1);
//                base.VisitLeave(theInstance);
//            }

//            //public override void VisitLeave(ModelData_Instance theInstance) // Called by cadex
//            //{
//            //	VisitLeaveActions();
//            //	base.VisitLeave(theInstance);
//            //}

//			/// Writes the name of a scengraphelement to the console if writeDocumentStructureInformation is set to true.
//			/// </summary>
//			/// <param name="theAssembly">The SceneGraphElement to write the name of.</param>
//			/// <param name="s">The string that tells us if it is either a "part", "assembly" or "instance".</param>
//			private void WriteNameToConsole(ModelData_SceneGraphElement theAssembly, string s)
//			{
//				if (writeDocumentStructureInformation)
//				{
//					string tabular = "";
//					for (int i = 0; i < documentOrder.Count; i++)
//						tabular += "-";
//					tabular += " " + documentOrder.Last() + ": ";
//					if (theAssembly.Name() == null)
//						tabular += "NoName";
//					tabular += theAssembly.Name().ToString();
//					Console.WriteLine(tabular + " " + s);
//				}
//			}
//            protected void LoopTillParent(string objectName, uint elemIndex) 
//			{
//				//first two if statement check 1-1 relation
//				if (gimData.Elements[(int)elemIndex].Locals.Count != 1) 
//				{
//					return;
//				}
//				uint parentIndex = gimData.Elements[(int)elemIndex].Locals[0].Parent;
//				if (gimData.Elements[(int)parentIndex].Elements.Count != 1)
//				{
//					return;
//				}
//				if (elemToObject.ContainsKey(parentIndex))
//				{
//					if (gimData.Objects[(int)elemToObject[(uint)parentIndex]].Name.Equals(objectName))
//					{
//						gimData.Objects.RemoveAt((int)elemToObject[(uint)parentIndex]);
//						elemToObject.Remove(parentIndex);
//					}
//					return;
//				}
//				LoopTillParent(objectName, parentIndex);
//				return;
//			}
//		}

//		//internal class StructureVisitor : PartBRepVisitor
//		//{


//  //          struct gimNode
//  //          {
//		//		string type;
//		//		List<gimNode> children;
//  //          }
//  //          public StructureVisitor() : base(false, true, false)
//		//	{
				
//		//	}

//  //          public override void Apply(ModelData_Part thePart)
//  //          {
//  //              if (lastLinks == null || lastLinks.Count == 0 || documentOrder.Last() != lastLinks.Last().parent) // Link no longer applicable for the current part
//  //              {
//  //                  local = new GimLocal() { parent = documentOrder.Last() };
//  //              }
//  //              gimData.Elements[(int)documentOrder.Last()].elements.Add((uint)gimData.Elements.Count); // Add self to parent

//  //              if (!refrencesDictionary.ContainsKey(thePart))
//  //                  refrencesDictionary.Add(thePart, (uint)gimData.Elements.Count);

//  //              // Add self to document order
//  //              documentOrder.Add((uint)gimData.Elements.Count);

//  //              uint parent = (uint)gimData.Elements.Count;
//  //              gimData.Elements.Add(new GimElement()
//  //              {
//  //                  elementType = GimElementType.D_part,
//  //                  locals = new List<GimLocal>() { lastLink }
//  //              });

//		//		String partName = thePart.Name().ToString();

//  //              if (partName != "" && partName.ToLower() != "body")
//  //              {
//  //                  if (addedObjectNames.Contains(partName))
//  //                  {
//  //                      LoopTillParent(partName, (uint)gimData.Elements.Count - 1);
//  //                  }
//		//			else
//		//			{
//		//				addedObjectNames.Add(partName);
//		//			}
//  //                  GimObject gimObject = new GimObject
//  //                  {
//  //                      // the next element is the definition of this object
//  //                      element = (uint)gimData.Elements.Count - 1,
//  //                      name = partName
//  //                  };
//  //                  gimData.Objects.Add(gimObject); // add new object to gimData
//  //                  elemToObject.Add((uint)gimData.Elements.Count - 1, (uint)gimData.Objects.Count - 1);
//  //              }




//  //              // Explore Colours and/or textures
//  //              // TODO: Implement this.
//  //             // gimData = BRepExplorer.ExploreAppearance(thePart.Appearance(), gimData, parent);

//  //              //WriteNameToConsole(thePart, "part");

//  //              // Read BRep+
//  //              //ModelData_BRepRepresentation aBRep = thePart.BRepRepresentation(); // Get tree structure of part

//  //              // TODO: Is this correct? / What is this?
//  //              //ModelData_PolyRepresentation aPoly = thePart.PolyRepresentation(ModelData_RepresentationMask.ModelData_RM_Poly); // Get tree structure of part

//  //              //if (aBRep != null)
//  //              //{
//  //              //    BRepExplorer explorer = new BRepExplorer();

//  //              //    if (doAsyncBrepExploring)
//  //              //        partialGimDatas.Add(explorer.ExploreBRepAsync(aBRep, new GimDataPartial((int)parent), parent));
//  //              //    else
//  //              //        gimData = explorer.ExploreBRep(aBRep, gimData, parent, ref linkTwinsProperties);
//  //              //}

//  //              //if (aPoly != null)
//  //              //{
//  //              //    gimData = PolyExplorer.ExplorePoly(aPoly, gimData, parent);
//  //              //}
//  //              //else if (aBRep != null && generatePolygonsOnReading) // Ideally a brep null check shouldn't be needed here, however sometimes we find parts without brep or poly representations. This should be investigated further.
//  //              //{
//  //              //    // Do mesher magic.
//  //              //    ModelAlgo_BRepMesherParameters parameters = new ModelAlgo_BRepMesherParameters(ModelAlgo_BRepMesherParameters.Granularity.Fine);
//  //              //    ModelAlgo_BRepMesher bRepMesher = new ModelAlgo_BRepMesher(parameters);
//  //              //    ModelData_PolyRepresentation aPolyRep = bRepMesher.Compute(aBRep);
//  //              //    gimData = PolyExplorer.ExplorePoly(aPolyRep, gimData, parent);
//  //              //    bRepMesher.Dispose();
//  //              //    parameters.Dispose();

//  //              //}

//  //              //if (aPoly == null && aBRep == null)
//  //              //{
//  //              //    GimLogger.Warn("Found part without representation!");
//  //              //}

//  //              documentOrder.RemoveAt(documentOrder.Count - 1);
//  //              // If the brep is an root instance.
//  //              //if (documentOrder.Last() == 0)
//  //              //{
//  //              //	GimLocal l = new GimLocal();
//  //              //	l.parent = 0;
//  //              //	gimData.elements[(int)gimData.objects.Last().element].locals.Add(l);
//  //              //}
//  //          }

//  //      }

//		internal partial class BRepExplorer
//		{
//			int orientationTracker;
//			//private GimElement gimElement;
//			//private GimElement gimElementBasis;

//			private GimDataPartial gimDataAsync;
//			private int parentAsync;
//			private uint parentOfRepresentationAsync;
//			private ModelData_BRepRepresentation theBRepAsync;

//			/// <summary>
//			/// Explores the boundary representation. It adds the boundary representation to the gimData.
//			/// </summary>
//			/// <param name="theBRep">The boundary representation to explore</param>
//			/// <param name="gimData">The gimData to add the boundary representation too.</param>
//			/// <param name="parentOfRepresentation">The element index that is the parent of the representation.</param>
//			/// <returns></returns>
//			internal Task<GimDataPartial> ExploreBRepAsync(ModelData_BRepRepresentation theBRep, GimDataPartial gimData, uint parentOfRepresentation)
//			{
//				// Get() method retrieves bodies listed in B-Rep representation by calling data providers flushing
//				// Flushing isn't an elementary process so it can take a significant time (seconds, minutes depending on a model structure)
//				theBRepAsync = theBRep;
//				gimDataAsync = gimData;
//				parentAsync = gimData.parent;
//				parentOfRepresentationAsync = parentOfRepresentation;

//				Task<GimDataPartial> GimDataPartialTask = new Task<GimDataPartial>(ExploreBrepBodiesAsync);
//				return GimDataPartialTask;
//			}

//			internal GimDataPartial ExploreBrepBodiesAsync()
//			{
//				ModelData_BodyList aBodyList = theBRepAsync.Get();

//				gimDataAsync.Elements.Add(new GimElement() { elementType = GimElementType.R_BRep });

//				if (aBodyList == null) return gimDataAsync; // If the body has no b-representation.

//				// Set my parent as my parent.
//				gimDataAsync.Elements[^1].locals.Add(new GimLocal() { parent = parentOfRepresentationAsync });

//				uint bodyParent = (uint)gimDataAsync.Elements.Count - 1;

//				LinkTwinsProperties linkTwinsProperties = LinkTwinsProperties.DoNothing();

//				gimDataAsync = (GimDataPartial)ExploreBrepBodies(gimDataAsync, theBRepAsync, aBodyList, bodyParent, ref linkTwinsProperties);
//				gimDataAsync.parent = parentAsync;

//				return gimDataAsync;
//			}

//			/// <summary>
//			/// Explores the boundary representation. It adds the boundary representation to the gimData.
//			/// </summary>
//			/// <param name="theBRep">The boundary representation to explore</param>
//			/// <param name="gimData">The gimData to add the boundary representation too.</param>
//			/// <param name="parentOfRepresentation">The element index that is the parent of the representation.</param>
//			/// <returns></returns>
//			internal GimData ExploreBRep(ModelData_BRepRepresentation theBRep, GimData gimData, uint parentOfRepresentation, ref LinkTwinsProperties linkTwinsProperties)
//			{
//				// Get() method retrieves bodies listed in B-Rep representation by calling data providers flushing
//				// Flushing isn't an elementary process so it can take a significant time (seconds, minutes depending on a model structure)
//				Task<ModelData_BodyList> aBodyList = new Task<ModelData_BodyList>( () => theBRep.Get());
//				aBodyList.Start();

//				gimData.Elements.Add(new GimElement() { elementType = GimElementType.R_BRep });

//				// Set my parent as my parent.
//				gimData.Elements[^1].locals.Add(new GimLocal() { parent = parentOfRepresentation });
//				// Set me as my parents child
//				gimData.Elements[(int)parentOfRepresentation].elements.Add((uint)gimData.Elements.Count - 1);

//				uint bodyParent = (uint)gimData.Elements.Count - 1;

//				if (aBodyList.Result == null)
//				{
//					// If the b-representation has no bodies.
//					GimLogger.Warn("b-representation without bodies. ");
//					return gimData;
//				}

//				return ExploreBrepBodies(gimData, theBRep, aBodyList.Result, bodyParent, ref linkTwinsProperties);
//			}

//			internal GimData ExploreBrepBodies(GimData gimData, ModelData_BRepRepresentation theBRep, ModelData_BodyList aBodyList, uint bodyParent, ref LinkTwinsProperties linkTwinsProperties)
//			{
//				for (uint i = 0; i < aBodyList.Size(); ++i)
//				{
//					ModelData_Body aBody = aBodyList.Access(i);

//					// add new element to gimData
//					gimData.Elements.Add(new GimElement());
//					gimData.Elements[^1].elementType = aBody.BodyType() switch
//					{
//						ModelData_BodyType.ModelData_BT_Acorn => GimElementType.B_acorn,
//						ModelData_BodyType.ModelData_BT_General => GimElementType.B_general,
//						ModelData_BodyType.ModelData_BT_Sheet => GimElementType.B_sheet,
//						ModelData_BodyType.ModelData_BT_Solid => GimElementType.B_solid,
//						ModelData_BodyType.ModelData_BT_Undefined => GimElementType.undefined,
//						ModelData_BodyType.ModelData_BT_Wireframe => GimElementType.B_wireframe,
//						_ => throw GimLogger.Error(new ArgumentException("Unknown type: " + aBody.BodyType().ToString())),
//					};

//					// Add the body to the gimdata. 
//					gimData.Elements[^1].locals.Add(new GimLocal() { parent = bodyParent });

//					// Store the index of the body. 
//					uint bodyIndex = (uint)gimData.Elements.Count - 1;

//					// Add the body to it's parent.
//					gimData.Elements[(int)bodyParent].elements.Add(bodyIndex);

//					// Explore the shape of the body.
//					gimData = GetOrientationInfo(aBody, gimData);
//					gimData = ExploreShape(theBRep, aBody, gimData, (int)bodyIndex); // look into the shapes of the body

//					// Explore Colours and/or textures
//					// For Body specific appearences
//					gimData = ExploreAppearance(theBRep.Appearance(aBody), gimData, bodyIndex);
//				}

//				// Start linking from the bodyparent element. 
//				linkTwinsProperties.currentElement = bodyParent;
//				gimData = Twins.LinkTwinsPartial(gimData, ref linkTwinsProperties); //twin detection is now done on reading layer; whenever a part is added, twins detection is applied 

//				return gimData;
//			}

//			internal static GimData ExploreAppearance(ModelData_Appearance theAppearance, GimData gimData, uint parent)
//			{
//				// Add a gim Material for each body (?)
//				// TODO: Check if this is correct.
//				// TODO: Compress GimMaterial

//				if (theAppearance != null)
//				{
//					GimMaterial material = new GimMaterial() { elementType = GimElementType.Material };

//					// Save textures to Gim Material.
//					ModelData_TextureSet theTextureSet = theAppearance.TextureSet();
//					List<GimTexture> materialTextures = null;
//					if (theTextureSet != null && !theTextureSet.IsEmpty() && theTextureSet.NumberOfTextures() > 0)
//					{
//						// Texture visitor
//						CadexTextureVisitor textureVisitor = new CadexTextureVisitor();
//						theTextureSet.Accept(textureVisitor);
//						//material.textures = textureVisitor.gimTextures;	
//						materialTextures = textureVisitor.gimTextures;
//					}

//					// Generic colour not needed? Is either ToColor or DiffuseColor.
//					// ModelData_ColorObject c = theAppearance.GenericColor();

//					ModelData_Color tc = new ModelData_Color();
//					if (theAppearance.ToColor(tc))
//					{
//						// ToColor = GenericColor or DiffuseColor!
//						float a, r, g, b;
//						a = tc.A(); r = tc.R(); g = tc.G(); b = tc.B();
//						material.BaseColour = new GimColour(a, r, g, b).ToInt();
//					}

//					ModelData_Material theMaterial = new ModelData_Material();
//					if (theAppearance.ToMaterial(theMaterial))
//					{
//						material.AmbientColour = GetColour(theMaterial.AmbientColor()).ToInt();
//						material.DiffuseColour = GetColour(theMaterial.DiffuseColor()).ToInt();
//						material.SpecularColour = GetColour(theMaterial.SpecularColor()).ToInt();
//						material.Shininess = theMaterial.Shininess();
//					}

//					uint materialID = (uint)gimData.elements.Count;
//					gimData.Elements.Add(material);
//					gimData.AddChildToParent(materialID, parent);
//					if (materialTextures != null)
//					{
//						for (int i = 0; i < materialTextures.Count; i++)
//						{
//							gimData.Elements.Add(materialTextures[i]);
//							gimData.AddChildToParent((uint)gimData.elements.Count - 1, materialID);
//						}
//					}
//				}

//				return gimData;
//			}

//			internal static GimColour GetColour(ModelData_Color theColour)
//			{
//				return new GimColour()
//				{
//					alpha = theColour.A(),
//					red = theColour.R(),
//					green = theColour.G(),
//					blue = theColour.B()
//				};
//			}

//			/// <summary>
//			/// This recursive iterates over the Shape until it reaches vertices.
//			/// </summary>
//			/// <param name="theShape">The shape to explore</param>
//			/// <param name="gimData">The GimData to add the shape and it's children to.</param>
//			/// <param name="theFace">An optional reference to a face if a element has a link to it.</param>
//			/// <returns></returns>
//			private GimData ExploreShape(ModelData_BRepRepresentation theBRep, ModelData_Shape theShape, GimData gimData, int currentId, ModelData_Face theFace = null)
//			{

//				int currentOrientation;
//				if (theShape.Orientation() == ModelData_ShapeOrientation.ModelData_SO_Reversed) // If reversed
//					currentOrientation = -1;
//				else
//					currentOrientation = 1;
//				orientationTracker *= currentOrientation;

//				ModelData_Shape.Iterator aShapeIt = new ModelData_Shape.Iterator(theShape);
//				//int currentId = gimData.Elements.Count - 1;

//				if (theShape.Type() == ModelData_ShapeType.ModelData_ST_Face)
//					theFace = ModelData_Face.Cast(theShape);

//				while (aShapeIt.HasNext())
//				{
//                    // Marcel 27-1-2023
//                    // suggestion to change here:
//                    // make a list of all children on the current level
//                    // create per child a list of elements and a list of object
//                    // loop per child: call this same function for grandchildren (later: multithreading)
//					   // during this, create hashcode for each element
//                    // end loop: find twins comparing the new created list with the current GimData using the hashcode
//                    // add new lists to GimData

//                    ModelData_Shape aShape = aShapeIt.Next();
//					uint shapeIndex = (uint)gimData.elements.Count;

//					// Get shape info and add it to the gimdata. 
//					gimData = GetShapeInfo(aShape, currentId, gimData, theFace);

//					// Explore the shape appearance  and add it to the gimdata.
//					gimData = ExploreAppearance(theBRep.Appearance(aShape), gimData, shapeIndex);

//					// Explore the shape
//					gimData = ExploreShape(theBRep, aShape, gimData, (int)shapeIndex, theFace);
//				}
//				// Division and multiplication with -1 and 1 is the same, however multiplication in code is much faster, changed to *=
//				orientationTracker *= currentOrientation;
//				return gimData;
//			}

//			/// <summary>
//			/// Adds shape type name and adds shape info to the GimData.
//			/// </summary>
//			/// <param name="theShape">The shape that we are exploring.</param>
//			/// <param name="parent">The index of the parent of this shape</param>
//			/// <param name="gimData">The GimData to add the shape information to.</param>
//			/// <param name="theFace">An optional reference to a face if the element needs that.</param>
//			/// <returns></returns>
//			private GimData GetShapeInfo(ModelData_Shape theShape, int parent, GimData gimData, ModelData_Face theFace = null)
//			{
//				//if (theShape.Type() == ModelData_ShapeType.ModelData_ST_Vertex) 
//				//    Console.WriteLine("Vertex, possible issue");
//				ModelData_ShapeType theShapeType = theShape.Type();


//				if (theShapeType != ModelData_ShapeType.ModelData_ST_Vertex)
//				{
//					List<GimElement> gimElements = new List<GimElement>();
//					switch (theShapeType)
//					{
//						case ModelData_ShapeType.ModelData_ST_Body:
//							break;
//						case ModelData_ShapeType.ModelData_ST_Solid:
//							gimElements.Add(new GimElement());
//							gimElements[0].elementType = GimElementType.solid;
//							break;
//						case ModelData_ShapeType.ModelData_ST_Shell:
//							gimElements.Add(new GimElement());
//							gimElements[0].elementType = GimElementType.shell;
//							break;
//						case ModelData_ShapeType.ModelData_ST_Face:
//							gimElements = GetFaceInfo(ModelData_Face.Cast(theShape), gimData, new GimElement()); break;
//						case ModelData_ShapeType.ModelData_ST_Edge:
//							gimElements = GetEdgeInfo(ModelData_Edge.Cast(theShape), theFace, new GimElement()); break;
//						case ModelData_ShapeType.ModelData_ST_Wire:
//							gimElements.Add(new GimElement());
//							gimElements[0].elementType = GimElementType.wire;
//							break;
//						default: break;
//					}

//					if (gimElements.Count > 0)
//					{
//						gimElements[0].orientation = GetOrientationInfoGimElement(theShape);

//						// Set correct parent
//						if (gimElements[0].locals == null || gimElements[0].locals.Count <= 0)
//							gimElements[0].locals = new List<GimLocal>() { new GimLocal() };
//						gimElements[0].locals[0].parent = (uint)parent;

//						gimData.Elements.Add(gimElements[0]);
//						gimData.Elements[parent].elements.Add((uint)gimData.Elements.Count - 1);
//						for (int i = 1; i < gimElements.Count; i++)
//						{
//							GimElement gimElementBasis = gimElements[i];
//							// Set correct parent for gimElementBasis
//							if (gimElementBasis.locals == null || gimElementBasis.locals.Count <= 0)
//							{
//								//TODO: Make sure this is always already set!
//								//GimLogger.Warn("Improper basis element parent set!");
//								gimElementBasis.locals = new List<GimLocal>() { new GimLocal() };
//								gimElementBasis.locals[0].parent = (uint)gimData.Elements.Count - 1;
//								gimData.Elements[^1].elements.Add((uint)gimData.Elements.Count);
//							}
//							if (gimElementBasis.locals[0].parent == 0)
//							{
//								//TODO: Make sure this is always already set!
//								//GimLogger.Warn("Improper basis element parent set!");
//								gimElementBasis.locals[0].parent = (uint)gimData.Elements.Count - 1;
//								gimData.Elements[^1].elements.Add((uint)gimData.Elements.Count);
//							}

//							gimData.Elements.Add(gimElementBasis);
//						}
//					}
//				}
//				return gimData;
//			}

//			private static GimData GetOrientationInfo(ModelData_Shape theShape, GimData gimData)
//			{
//				switch (theShape.Orientation())
//				{
//					case ModelData_ShapeOrientation.ModelData_SO_Forward:
//						gimData.Elements[^1].Orientation = true;
//						//if (orientationTracker == 1)
//						//	gimData.elements[^1].Orientation = true;
//						//else
//						//	gimData.elements[^1].Orientation = false;
//						break;
//					case ModelData_ShapeOrientation.ModelData_SO_Reversed:
//						gimData.Elements[^1].Orientation = false;
//						//if (orientationTracker == 1)
//						//	gimData.elements[^1].Orientation = false;
//						//else
//						//	gimData.elements[^1].Orientation = true;
//						break;
//					default:
//						break;
//				}
//				return gimData;
//			}

//			private static bool GetOrientationInfoGimElement(ModelData_Shape theShape)
//			{
//				return theShape.Orientation() switch
//				{
//					ModelData_ShapeOrientation.ModelData_SO_Forward => true,
//					ModelData_ShapeOrientation.ModelData_SO_Reversed => false, // Why does everything brake when this is false?
//					_ => true,
//				};
//			}

//			private List<GimElement> GetFaceInfo(ModelData_Face theFace, GimData gimData, GimElement gimElement)
//			{
//				ModelData_Surface aSurface = theFace.Surface();
//				List<GimElement> gimElements = GetSurfaceType(aSurface, gimData, gimElement); // get details of aSurface
//				return gimElements;
//			}

//			private static List<GimElement> GetEdgeInfo(ModelData_Edge theEdge, ModelData_Face theFace, GimElement gimElement)
//			{
//				List<GimElement> gimElements = new List<GimElement>();
//				double aParamFirst = 0.0, aParamLast = 0.0;
//				ModelData_Curve aCurve = null;

//				if (theEdge.IsDegenerated())
//				{
//					if (theFace == null)
//						GimLogger.Warn("The face of a degenerated edge is null!");

//					ModelData_Curve2d aPCurve = theEdge.PCurve(theFace, ref aParamFirst, ref aParamLast);
//					gimElements = GetCurve2DType(aPCurve, aParamFirst, aParamLast, gimElement);
//				}
//				else if (theFace != null && theEdge.IsSeam(theFace))
//				{
//					// TODO: Fix seam edges!
//					aCurve = theEdge.Curve(ref aParamFirst, ref aParamLast);
//					List<GimElement> curveElements = GetCurveType(aCurve, aParamFirst, aParamLast, gimElement);
//					ModelData_Curve2d aSeamCurve = theEdge.PCurve(theFace, ref aParamFirst, ref aParamLast);

//					// Make sure we have the not reversed PCurve by reversing the PCurve if the edge is reversed.
//					//if (theEdge.Orientation() == ModelData_ShapeOrientation.ModelData_SO_Reversed)
//					//	aSeamCurve = aSeamCurve.Reversed();

//					List<GimElement> curve2DElements = GetCurve2DType(aSeamCurve, aParamFirst, aParamLast, new GimElement());
//					ModelData_Vertex start = theEdge.StartVertex();
//					ModelData_Vertex end = theEdge.EndVertex();
//					curve2DElements[0].elementDoubles[0] = start.Point().X();
//					curve2DElements[0].elementDoubles[1] = start.Point().Y();
//					curve2DElements[0].elementDoubles[2] = start.Point().Z();
//					curve2DElements[0].elementDoubles[3] = end.Point().X();
//					curve2DElements[0].elementDoubles[4] = end.Point().Y();
//					curve2DElements[0].elementDoubles[5] = end.Point().Z();

//					curve2DElements[0].Orientation = theEdge.Orientation() != ModelData_ShapeOrientation.ModelData_SO_Reversed;

//					foreach (GimElement curve2DElement in curve2DElements)
//					{
//						curveElements.Add(curve2DElement);
//					}

//					gimElements = curveElements;
//				}
//				else
//				{

//					aCurve = theEdge.Curve(ref aParamFirst, ref aParamLast);
//					if (aCurve == null)
//					{
//						// GimLogger.Warn("Non-degenerated PCurve!");
//						// TODO: A non degenerated curve should never have only aPCurve, why is this so?
//						ModelData_Curve2d aPCurve = theEdge.PCurve(theFace, ref aParamFirst, ref aParamLast);
//						if (aPCurve == null)
//						{
//							GimLogger.Warn("Empty Curve and PCurve!");
//							gimElements.Add(new GimElement() { elementType = GimElementType.undefined });
//						}
//						else
//						{
//							gimElements = GetCurve2DType(aPCurve, aParamFirst, aParamLast, gimElement);
//						}
//					}
//					else
//					{
//						gimElements = GetCurveType(aCurve, aParamFirst, aParamLast, gimElement); // get the specific values
//					}
//				}

//				if (theEdge.IsDegenerated() || aCurve == null || (aCurve != null && (aCurve.Type() == ModelData_CurveType.ModelData_CT_BSpline || aCurve.Type() == ModelData_CurveType.ModelData_CT_Bezier)))
//				{
//					if (gimElements[0].elementDoubles != null)
//					{
//						ModelData_Vertex start = theEdge.StartVertex();
//						ModelData_Vertex end = theEdge.EndVertex();
//						gimElements[0].elementDoubles[0] = start.Point().X();
//						gimElements[0].elementDoubles[1] = start.Point().Y();
//						gimElements[0].elementDoubles[2] = start.Point().Z();
//						gimElements[0].elementDoubles[3] = end.Point().X();
//						gimElements[0].elementDoubles[4] = end.Point().Y();
//						gimElements[0].elementDoubles[5] = end.Point().Z();
//					}
//				}

//				foreach (GimElement curveElement in gimElements)
//				{
//					curveElement.Orientation = theEdge.Orientation() != ModelData_ShapeOrientation.ModelData_SO_Reversed;
//				}


//				return gimElements;
//			}

//			private static double MultipleOf(double value, double step = new double())
//			{
//				if (step == new double()) // remove double-trash
//					value = (double)((decimal)value);
//				else // Round using step size
//				{
//					//double d = ((int)Math.Round(value / step)) * step;
//					string s = step.ToString();
//					int i = s.Length - s.IndexOf(',') - 1;
//					if (i < 0) i = 0;
//					if (i > 15) i = 15; // Larger or smaller value's than 0 and 15 cause errors.
//					value = Math.Round(value, i);
//				}
//				return value;
//			}

//			internal static GimLocal NewGimPlane(ModelData_Axis2Placement aLocSystem)
//			{
//				return NewGimPlane(new ModelData_Axis3Placement(aLocSystem));
//			}
//			internal static GimLocal NewGimPlane(ModelData_Axis3Placement aLocSystem)
//			{
//				Vect xVector = new Vect(aLocSystem.XDirection().X(),
//					aLocSystem.XDirection().Y(),
//					aLocSystem.XDirection().Z());
//				Vect yVector = new Vect(aLocSystem.YDirection().X(),
//                    aLocSystem.YDirection().Y(),
//                    aLocSystem.YDirection().Z());
//				// making sure the matrix is rank 3, aka a correct transformation matrix
//				Vect zVector = xVector ^ yVector;
//                GimLocal local = new GimLocal
//				{
//					px = aLocSystem.Location().X(),
//					py = aLocSystem.Location().Y(),
//					pz = aLocSystem.Location().Z(),
//					xx = xVector[0], 
//					xy = xVector[1],
//					xz = xVector[2],
//					yx = yVector[0],
//					yy = yVector[1],
//					yz = yVector[2],
//					zx = zVector[0],
//					zy = zVector[1],
//					zz = zVector[2],
//					rightHanded = aLocSystem.IsRightHanded()
//				};

//				local.RoundMatrix();

//				return local;
//			}
//			internal static GimLocal NewGimPlane(ModelData_Axis2Placement2d aLocSystem)
//			{

//				var t = new GimLocal
//				{
//					px = aLocSystem.Location().X(),
//					py = aLocSystem.Location().Y(),
//					pz = -2147483648,
//					xx = aLocSystem.XDirection().X(), // convert double to int with highest precision
//					xy = aLocSystem.XDirection().Y(),
//					xz = -2147483648,
//					yx = aLocSystem.YDirection().X(),
//					yy = aLocSystem.YDirection().Y(),
//					yz = -2147483648,
//					zx = -2147483648, // null
//					zy = -2147483648,
//					zz = -2147483648,
//					rightHanded = true
//				};

//				t.RoundMatrix();

//				return t;
//			}
//			internal static GimLocal NewGimPlane(ModelData_Point point, ModelData_Direction zDirection)
//			{
//				GimLocal gimLocal = NewGimPlane(zDirection);
//				gimLocal.px = point.X();
//				gimLocal.py = point.Y();
//				gimLocal.pz = point.Z();
//				return gimLocal;
//			}
//			internal static GimLocal NewGimPlane(ModelData_Point2d point, ModelData_Direction2d zDirection)
//			{
//				GimLocal gimLocal = NewGimPlane(zDirection);
//				gimLocal.px = point.X();
//				gimLocal.py = point.Y();
//				return gimLocal;
//			}
//			internal static GimLocal NewGimPlane(ModelData_Direction zDirection)
//			{
//				var t  = new GimLocal
//				{
//					xx = -2147483648, // null
//					xy = -2147483648,
//					xz = -2147483648,
//					yx = -2147483648,
//					yy = -2147483648,
//					yz = -2147483648,
//					zx = zDirection.X(), // convert double to int with highest precision
//					zy = zDirection.Y(),
//					zz = zDirection.Z(),
//					rightHanded = true
//				};

//				t.RoundMatrix();

//				return t;
//			}
//			internal static GimLocal NewGimPlane(ModelData_Direction2d zDirection)
//			{
				
//				var t = new GimLocal
//				{
//					xx = -2147483648, // null
//					xy = -2147483648,
//					xz = -2147483648,
//					yx = -2147483648,
//					yy = -2147483648,
//					yz = -2147483648,
//					zx = (zDirection.X()), // convert double to int with highest precision
//					zy = (zDirection.Y()),
//					zz = -2147483648,
//					rightHanded = true
//				};

//				t.RoundMatrix();

//				return t;
//			}
//			internal static GimLocal NewGimPlane(ModelData_Transformation transform)
//			{
//				GimLocal l = new GimLocal();
//				transform.TranslationPart(ref l.px, ref l.py, ref l.pz);
//				double xx = 0, xy = 0, xz = 0, yx = 0, yy = 0, yz = 0, zx = 0, zy = 0, zz = 0;
//				transform.RotationPart(ref xx, ref xy, ref xz, ref yx, ref yy, ref yz, ref zx, ref zy, ref zz);
//				l.xx = (xx); l.yx = (yx); l.zx = zx;
//				l.xy = (xy); l.yy = (yy); l.zy = zy;
//				l.xz = (xz); l.yz = (yz); l.zz = zz;
//				l.rightHanded = true; // IsRightHanded(xx, xy, xz, yx, yy, yz, zx, zy, zz) is not needed, since rightHanded is never called for a transformation;
				
//				l.RoundMatrix();
                
//				return l;
//			}
//		}
	}
}