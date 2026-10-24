using cadex;
using GimBin;
using GIMcompress;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static GIMtransfer.Cadex;
using GIMtransfer;
using GIMcompress.Shrinkwrap;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.LayoutRenderers.Wrappers;
using GimUtilities;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using GIMtransfer.read;
using System.Net.Http.Json;
using GIMtransfer.write;
using static ACadSharp.Objects.MLineStyle;
using System.Xml.Linq;

[assembly: InternalsVisibleTo("TestPlatform")]
namespace GIMtransfer
{
    public static partial class FileAccess
    {
        private static readonly JsonSerializerSettings JSON_SETTINGS_INDENTED = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public const string EMPTY_GLB = "Z2xURgIAAADUAAAAwAAAAEpTT057ImFzc2V0Ijp7ImNvcHlyaWdodCI6IiIsImdlbmVyYXRvciI6IlNoYXJwR0xURiAxLjAuMC1hbHBoYTAwMzErNWEzM2Q1NDUyNTI4ZDgyN2FjNTU3MjdmMzAyZDhhOTFhNzVjNDE4NiIsInZlcnNpb24iOiIyLjAifSwibm9kZXMiOlt7Im5hbWUiOiJHSU1vYmplY3QifV0sInNjZW5lIjowLCJzY2VuZXMiOlt7Im5vZGVzIjpbMF19XX0gICAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";
        private const long GLTFPACK_MIN_BLOB_SIZE_BYTES = 128L * 1024L;
        private const int GLTFPACK_TIMEOUT_MS = 60000;
        private const string GLTFPACK_RELATIVE_PATH = @"Programs\GIM\GIMMesh\gltfpack.exe";

        /// <summary>
        /// Writes a file from a GimData object to a specific filetype.
        /// </summary>
        /// <param name="gimData"> The gimData that should be written. </param>
        /// <param name="fileType"> The filetype to be written. </param>
        /// <param name="fileName"> The name the file should be given. </param>
        /// <param name="compress"> Whether the file should use advanced compression. </param>
        /// <param name="jsonElem"> The element to start creating the JSON for.</param>
        /// <param name="progressStatus"> An optional GimProgressStatus that can be passed to the function to update to current progress of the task.</param>
        /// <returns></returns>
        private static string WriteFromGimData(GimData gimData, string fileType, string fileName, bool compress = true, int jsonElem = 0, GimProgressStatus progressStatus = null)
        {
#if !DEBUG
            try
            {
#endif
            Start();
            if (compress)
            {
                //gimData = Twins.LinkTwins(gimData);
            }
            if (fileType == "json")
            {
                return System.Text.Json.JsonSerializer.Serialize(
                    GimDataToJson(gimData, fileName, -1, progressStatus),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                );
            }
            else if (fileType == "bin")
            {
                progressStatus.Create(new Dictionary<string, int>() { { "CreateGIMbin", 100 } });
                return GimDataToBinary(gimData, fileName, progressStatus);
                /*
                // Write with GimZip
                string path = GetTempPath(fileName) + ".gimzip"; // define and create the subfolder

                // Create writer
                BinaryWriter writer;
                if (File.Exists(path))
                    writer = new BinaryWriter(File.Open(path, FileMode.Truncate));
                else
                    writer = new BinaryWriter(File.Open(path, FileMode.Create));

                GimWriter gimWriter = new GimWriter(writer);
                gimWriter.Write(gimData);

                return path;
                */
            }
            else
            {

                // Write with Cadex
                //ModelData_Model model = CadexWriteElements.GetCadexModel(gimData, GIMtransfer.FileAccess.GetTempFolder(), progressStatus);

                //// TODO: Should converted all breppresentations to a polyrepresentations if we want the export a polyrepresentations
                ////if (fileType == "gltf") // fileType == PolyFileType
                ////{
                ////	CadexModel.CadexModelToPoly(model);
                ////}

                string error = "", returnedPath ="";
                //bool success = CadexModel.Write(model, fileType, fileName, ref error, ref returnedPath);
                //if (!success) throw GimLogger.Error(error);
                //if (progressStatus != null)
                //{
                //    progressStatus.EntityCompletedCallback("Elements");
                //}
                return error;
            }

#if !DEBUG
            }
            catch (Exception e)
            {
                throw GimLogger.Error("(Error: GW) " + e);
            }
#endif

        }

        // this function handles the logic for the JsonToJson function
        private static string[] JsonToJsonLogic(string[] jsonContents, string fileName,
            GimProgressStatus readProgressStatus, GimProgressStatus writeProgressStatus,
            GimOptions gimOptions)
        {
            // The units we will use to round the mesh/gimdata
            int binaryUnitToBe = gimOptions.binaryUnitToBe, visualisationUnitToBe = gimOptions.visualisationUnitToBe;

            // Reading the main and secondary jsons (if they exist)
            JsonGimObject mainJsonObj = JsonManager.ReadGimJson(jsonContents[0]);
            (var secondaryGimDatas, var secondaryJsonObjs) = ReadSecondaryJsons(jsonContents);
            GimData mainGimData = GimJsonToGimData(mainJsonObj);

            // Build a reuse map for visual payloads (GLBs) keyed by child definition ID only.
            // In this JSON format, JsonGimElement.ID is the GLB slot index and fully identifies
            // the child's resulting GLB.
            Dictionary<int, string> reuseGlbsByChildId = null;
            try
            {
                if (mainJsonObj != null)
                {
                    reuseGlbsByChildId = new();
                    foreach (var child in mainJsonObj.Children ?? new())
                    {
                        int childId = child.ID;

                        string glb = null;
                        if (mainJsonObj.GLBs != null && childId >= 0 && childId < mainJsonObj.GLBs.Length)
                            glb = mainJsonObj.GLBs[childId];
                        else if (!string.IsNullOrWhiteSpace(mainJsonObj.GLB))
                            glb = mainJsonObj.GLB;

                        if (!string.IsNullOrWhiteSpace(glb))
                            reuseGlbsByChildId[childId] = glb;
                    }
                }
            }
            catch
            {
                // Reuse is best-effort; fall back to legacy behavior on any issue.
                reuseGlbsByChildId = null;
            }

            // Returns a list of unique objects
            var childObjectIDs = GetUniqueChildObjects(mainGimData);

            // Getting the list of objects to promote (and their proj-obj IDs), and objects to demote
            GetObjectsToPromoteOrDemote(mainJsonObj, mainGimData, childObjectIDs,
                out var objectIndexesForPromotion, out var objectIndexesForDemotion, out var objectAndProjectIDs);

            var GLBs = mainJsonObj.GLBs;

            // Invalidate reuse map for operations that can change visuals.
            if (reuseGlbsByChildId != null)
            {
                bool disableReuse =
                    gimOptions == null ||
                    gimOptions.scale != 1 ||
                    gimOptions.mirror ||
                    !gimOptions.IsDefaultTransformation();

                if (!disableReuse && visualisationUnitToBe > 0)
                {
                    try
                    {
                        string targetUnit = new GimUnitSize((byte)visualisationUnitToBe).ToString();
                        if (!string.Equals(mainJsonObj?.GIMUnitv, targetUnit, StringComparison.OrdinalIgnoreCase))
                            disableReuse = true;
                    }
                    catch
                    {
                        disableReuse = true;
                    }
                }

                if (disableReuse)
                {
                    reuseGlbsByChildId = null;
                }
                else
                {
                    void InvalidateChildId(int childId)
                    {
                        reuseGlbsByChildId.Remove(childId);
                    }

                    foreach (var id in objectIndexesForPromotion) InvalidateChildId((int)id);
                    foreach (var id in objectIndexesForDemotion) InvalidateChildId((int)id);
                }
            }

            // Demoting any children to demote
            var objectIdsToBeDemoted = new List<uint>();
            foreach (int objectIndex in objectIndexesForDemotion)
            {
                objectIdsToBeDemoted.Add((uint)childObjectIDs[objectIndex]);
            }
            mainGimData = DemoteJsonChildren(mainGimData, secondaryGimDatas, objectIdsToBeDemoted);

            // Modifications to overall gimData happen here, as we combined all demoted gimDatas and
            // we didn't yet export the promoted ones
            // Applying the transformations stored in gimOptions
            ApplyTransformations(mainGimData, gimOptions);

            // Rounding the polygonal part of the gimData (definitively)
            if (binaryUnitToBe > 0)
            {
                GimUnitSize unitSize = new GimUnitSize((byte)binaryUnitToBe);
                double roundingStep = unitSize.GetRoundingStep();
                PolygonRounder.RoundGimData(mainGimData, roundingStep);
                mainGimData.GimBin = null;
            }

            // Promoting any children to promote
            var objectIdsToBePromoted = new List<uint>();
            foreach (int objectIndex in objectIndexesForPromotion)
            {
                objectIdsToBePromoted.Add((uint)childObjectIDs[objectIndex]);
            }
            GimData[] gimDatas = PromoteJsonChildren(mainGimData, objectIdsToBePromoted, objectAndProjectIDs.ToArray());


            Task<JsonGimObject>[] tasks = new Task<JsonGimObject>[gimDatas.Length + 1];

            tasks[0] = Task.Run(() => GimDataToJsonAsync(mainGimData, fileName, visualisationUnitToBe, writeProgressStatus, reuseGlbsByChildId));
            for (int i = 0; i < gimDatas.Length; i++)
            {
                GimData secondaryGimData = gimDatas[i];
                GimProgressStatus newWriteProgress = new();
                string secondaryFileName = objectAndProjectIDs[i].Item1 + "-" + objectAndProjectIDs[i].Item2;

                // Best-effort reuse for promoted child outputs: if we have the original visual for this promoted child,
                // reuse it for the single-GLB case (keyed as 0 in the child document).
                IReadOnlyDictionary<int, string> childReuse = null;
                if (reuseGlbsByChildId != null && i < objectIndexesForPromotion.Count)
                {
                    int promotedChildId = (int)objectIndexesForPromotion[i];
                    if (reuseGlbsByChildId.TryGetValue(promotedChildId, out string promotedGlb) &&
                        !string.IsNullOrWhiteSpace(promotedGlb))
                    {
                        childReuse = new Dictionary<int, string>
                        {
                            [0] = promotedGlb
                        };
                    }
                }

                tasks[i + 1] = Task.Run(() => GimDataToJsonAsync(secondaryGimData, secondaryFileName, visualisationUnitToBe, newWriteProgress, childReuse));
            }

            //Task<JsonGimObject> writtenJson = Task.Run(() => GimDataToJsonAsync(gimData, fileName, visualisationUnitToBe, 0, writeProgressStatus));
            Task.WaitAll(tasks);
            string[] resultingJsons = new string[gimDatas.Length + 1];

            Task<string>[] jsonWriteTasks = new Task<string>[tasks.Length];

            for (int i = 0; i < tasks.Length; i++)
            {
                var task = tasks[i];
                JsonGimObject jsonResult = task.Result;
                if (i > 0)
                {
                    int projID = objectAndProjectIDs[i - 1].Item2;
                    // The project ID could exist or not
                    jsonResult.ProjectID = projID == -1? null : projID.ToString();
                    jsonResult.ObjectID = objectAndProjectIDs[i - 1].Item1.ToString();
                }

                jsonWriteTasks[i] = Task.Run(() => JsonConvert.SerializeObject(jsonResult, JSON_SETTINGS_INDENTED));
            }

            Task.WaitAll(jsonWriteTasks);

            for (int i = 0; i < tasks.Length; i++)
            {
                resultingJsons[i] = jsonWriteTasks[i].Result;
            }

#if DEBUG
            // GIMtransfer debug only: export promoted files to temp
            WriteJsonToTemp(resultingJsons[0], fileName + "\\" + fileName);
            for (int i = 1; i < resultingJsons.Length; i++)
            {
                string secondaryFileName = objectAndProjectIDs[i - 1].Item2.ToString() + "-" + objectAndProjectIDs[i - 1].Item1.ToString();
                WriteJsonToTemp(resultingJsons[i], fileName + "\\" + secondaryFileName);
            }
#endif
            return resultingJsons;
        }


        private static GimData DemoteJsonChildren(GimData mainGimData, Dictionary<string, GimData> secondaryGimDatas,
            List<uint> objectIDsToDemote)
        {
            foreach (int objectId in objectIDsToDemote)
            {
                uint elId = mainGimData.Objects[objectId].Element;
                GimElement objectElement = mainGimData.Elements[(int)elId];

                if (objectElement.ElementType == GimElementType.ExternalReference)
                {
                    int jsonObjectId = objectElement.ElementInts[0];
                    string identifier = jsonObjectId.ToString();

                    if (objectElement.ElementInts.Length > 1)
                    {
                        // project ID + object ID
                        identifier = objectElement.ElementInts[1] + "-" + identifier;
                    }

                    if (secondaryGimDatas.ContainsKey(identifier))
                    {
                        GimData objectGimData = secondaryGimDatas[identifier];
                        mainGimData.RemoveElementSubTree(elId);
                        mainGimData.DemoteExternalReference(objectGimData, elId);
                        //objectElement.ElementDoubles = objectGimData.Elements[1].ElementDoubles;
                        //objectElement.ElementInts = objectGimData.Elements[1].ElementInts;
                        //objectElement.ElementBytes = objectGimData.Elements[1].ElementBytes;
                        //objectElement.ElementType = objectGimData.Elements[1].ElementType;

                        //foreach(uint childId in objectGimData.Elements[1].Elements)
                        //{
                        //    uint newChildIndex = mainGimData.AddPartialGimDataToThis(objectGimData, childId, elId, 0);
                        //    objectElement.Elements.Add(newChildIndex);
                        //}

                    }
                    else
                    {
                        throw GimLogger.Error(new KeyNotFoundException("Missing demotion dependancy: " + identifier));
                    }

                }
                else
                {
                    throw GimLogger.Error(new Exception("unexpected element type (expected an external reference)"));
                }
            }
            return mainGimData;
        }

        /// <summary>
        /// Promotes the specified objects from the GimData hierarchy to external references.
        /// </summary>
        /// <param name="gimData">The root GimData to promote objects from.</param>
        /// <param name="objectList">List of object indices to promote.</param>
        /// <param name="objectAndProjectIDs">Array of object and project identifier pairs.</param>
        /// <returns>Array of external GimData instances representing promoted objects.</returns>
        internal static GimData[] PromoteJsonChildren(GimData gimData, List<uint> objectList, Tuple<int, int>[] objectAndProjectIDs)
        {
            List<GimData> gimDatas = new();
            objectList.Sort();
            Dictionary<uint, GimData> promotedObjectMeshDatas = new();
            // we traverse the list in reverse order to not mess with our objectIDs
            for (int i = objectList.Count - 1; i >= 0; i--)
            {
                uint correctObjectIndex = objectList[i];
                if (correctObjectIndex >= 0 && correctObjectIndex <= gimData.Objects.Count - 1)
                {
                    uint objectElementIndex = gimData.Objects[(int)correctObjectIndex].element;
                    GimData externalGimData = GetGimElementAsGimData(gimData, objectElementIndex);
                    GimElement elementToPromote = gimData.Elements[(int)objectElementIndex];

                    GimData meshData = gimData.GetElementSubTreeAsMesh(objectElementIndex);
                    uint removeOffset = gimData.RemoveElementSubTree(objectElementIndex);
                    objectElementIndex -= removeOffset;
                    meshData.CreateRootIfNotExists();

                    elementToPromote.ElementType = GimElementType.ExternalReference;
                    elementToPromote.ToString();

                    var objectAndProjectID = objectAndProjectIDs[i];
                    int objectId = objectAndProjectID.Item1;
                    int projectId = objectAndProjectID.Item2;
                    elementToPromote.ElementInts = new int[] { objectId, projectId };
                    elementToPromote.ElementDoubles = null;
                    elementToPromote.ElementBytes = null;
                    elementToPromote.Elements = new();

                    gimData.AddSecondaryGimData(meshData, 1, objectElementIndex, 0);

                    externalGimData.Elements[0].Locals = new();

                    gimDatas.Insert(0, externalGimData);
                }
            }

            //foreach(var keyval in promotedObjectMeshDatas)
            //{

            //    uint objectIndex = keyval.Key;
            //    GimData meshData = promotedObjectMeshDatas[objectIndex];
            //    uint elementIndex = gimData.Objects[(int)objectIndex].Element;
            //    meshData.CreateRootIfNotExists();
            //    gimData.AddSecondaryGimData(meshData, 1, elementIndex, 0);
            //}

            return gimDatas.ToArray();
        }

        private static void GetObjectsToPromoteOrDemote(JsonGimObject mainJsonObj, GimData mainGimData, List<int> childObjectIDs,
            out List<uint> objectIndexesForPromotion, out List<uint> objectIndexesForDemotion, out List<Tuple<int, int>> objectAndProjectIDs
            )
        {
            objectIndexesForPromotion = new();
            objectIndexesForDemotion = new();
            objectAndProjectIDs = new();
            HashSet<int?> checkedObjectIDs = new();
            // we'll be checking the list of children to update
            foreach (var jsonChild in mainJsonObj.Children)
            {
                // no need to bother and recheck elements already checked
                if (checkedObjectIDs.Contains(jsonChild.ID))
                {
                    continue;
                }

                if (jsonChild.Crud != Crud.Update) // only check for children being updated
                {
                    continue;
                }

                int objectIndex = childObjectIDs[(int)jsonChild.ID];
                uint elementIndex = mainGimData.Objects[objectIndex].Element;
                GimElement element = mainGimData.Elements[(int)elementIndex];

                if (jsonChild.ObjectID != null) // maybe getting promoted
                {
                    // we're going to externalize this element
                    if (element.ElementType == GimElementType.ExternalReference) continue;

                    objectIndexesForPromotion.Add((uint)jsonChild.ID);
                    int objectID = (int)jsonChild.ObjectID;
                    int projectID = -1;

                    if (jsonChild.ProjectID != null)
                        projectID = (int)jsonChild.ProjectID;

                    objectAndProjectIDs.Add(new(objectID, projectID)); 
                    // we marked this object for promotion, don't need to check all instances of it
                    checkedObjectIDs.Add(jsonChild.ID);


                }
                else // maybe getting demoted
                {
                    // we're going to import this element back to the gimData
                    if (element.ElementType == GimElementType.ExternalReference)
                    {
                        objectIndexesForDemotion.Add((uint)jsonChild.ID);
                        // we marked this object for demotion, don't need to check all instances of it
                        checkedObjectIDs.Add(jsonChild.ID);
                    }
                }
            }
        }

        private static (Dictionary<string, GimData>, Dictionary<string, JsonGimObject>) ReadSecondaryJsons(string[] jsonContents)
        {
            Dictionary<string, GimData> secondaryGimDatas = new();
            Dictionary<string, JsonGimObject> secondaryJsonObjs = new();

            // checks secondary jsons
            for (int i = 1; i < jsonContents.Length; i++)
            {
                JsonGimObject secondaryObj = JsonManager.ReadGimJson(jsonContents[i]);
                var objectId = secondaryObj.ObjectID;
                string identifier = objectId;
                if (objectId == null) continue;
                if (secondaryObj.ProjectID != null)
                {
                    identifier = secondaryObj.ProjectID + "-" + identifier;
                }
                secondaryJsonObjs.Add(identifier, secondaryObj);

                GimData secondaryGimData = GimJsonToGimData(secondaryObj);

                secondaryGimDatas.Add(identifier, secondaryGimData);
            }

            return (secondaryGimDatas, secondaryJsonObjs);
        }

        /// <summary>
        /// Converts GimData to a binary format and writes it to a file.
        /// </summary>
        /// <remarks>
        /// This method performs the conversion by:
        /// - Opening a file stream to a specified file path.
        /// - Using a binary writer coupled with a custom GimWriter to write the GimData.
        /// - Closing the file stream upon completion.
        /// - In the case of an exception, the method throws a custom error with a descriptive message.
        /// </remarks>
        /// <param name="gimData">The GimData object to be converted to binary format.</param>
        /// <param name="fileName">The name of the file to which the binary data will be written. The '.bin' extension is appended to this name.</param>
        /// <param name="progressStatus">Optional parameter for tracking the progress of the writing process.</param>
        /// <returns>The path to the file where the binary data has been written.</returns>
        /// <exception cref="Exception">Throws a custom error if an issue occurs during the writing process.</exception>
        private static string GimDataToBinary(GimData gimData, string fileName, GimProgressStatus progressStatus = null)
        {
            try
            {
                //string filename = "GIM_COMPRESSED_";
                string filename = fileName + ".bin";

                FileStream stream = File.Open(GetTempPath(filename), FileMode.Create);
                BinaryWriter binWriter = new BinaryWriter(stream);
                GimWriter writer = new GimWriter(binWriter);
                // Send complete model so binary conversion keeps property sets and object links.
                writer.Write(gimData, progressStatus);

                stream.Close();
                return GetTempPath(filename);
            }
            catch(Exception e)
            {
                throw GimLogger.Error("An error occured while reading the binary!");
            }
        }


        private static GimData AddChildRecursive(int parent, int child, GimData gd, GimData gdSource)
        {
            gd.Elements[parent].Elements.Add((uint)gd.Elements.Count());
            gd.elements.Add(new GimElement()
            {
                ElementBytes = gdSource.elements[child].ElementBytes,
                ElementDoubles = gdSource.elements[child].ElementDoubles,
                ElementInts = gdSource.elements[child].ElementInts,
                ElementType = gdSource.Elements[child].ElementType,
                //Elements = gdSource.elements[child].Elements,
                Locals = gdSource.elements[child].Locals
            });
            foreach (GimLocal gl in gdSource.Elements.Last().Locals)
                gl.Parent = (uint)parent;
            foreach (uint grandchild in gdSource.elements[child].Elements)
                gd = AddChildRecursive(child, (int)grandchild, gd, gdSource);

            return gd;
        }

        private static List<uint> GetObjectChildren(List<uint> objects, GimData gimData, uint element)
        {
            GimElement elem = gimData.Elements[(int)element];

            if (!elem.elementType.IsDocument())
                return objects;

            IEnumerable<GimObject> objectsOfThisElement = gimData.Objects.Where(o => o.element == element);
            if (objectsOfThisElement.Any())
            {
                objects.Add(objectsOfThisElement.First().element);
            }

            foreach (uint child in elem.elements)
            {
                objects = GetObjectChildren(objects, gimData, child);
            }
            return objects;
        }

        private static List<Tuple<GimLocal, uint, uint?>> GetObjectTreeRecurse(GimData gimData, Dictionary<uint, uint> elementToObject, List<Tuple<GimLocal, uint, uint?>> output, uint? prevObj, uint element)
        {
            foreach (uint i in gimData.Elements[(int)element].elements)
            {
                if (elementToObject.ContainsKey(i))
                {
                    GimLocal local = gimData.Elements[(int)i].GetFirstLocalThatBelongsToThisParentOfThisElement((int)element);
                    output.Add(new Tuple<GimLocal, uint, uint?>(local, elementToObject[i], prevObj));
                    GetObjectTreeRecurse(gimData, elementToObject, output, elementToObject[i], i);
                }
                else
                {
                    GetObjectTreeRecurse(gimData, elementToObject, output, prevObj, i);
                }
            }
            return output;
        }

        /// <summary>
		/// Creates a new GimData with only a certain element and its subtree.
		/// Delegates to GimData.GetGimElementAsGimData which carries referenced property sets.
		/// </summary>
		/// <param name="gimData"> The GimData from which the element gets split off into a new GimData. </param>
		/// <param name="elementIndex"> The element to split off. </param>
		/// <param name="oldToNewPropertySets"> Optional map tracking source property set index to target index. </param>
		/// <returns>A new GimData containing the element subtree and its referenced property sets.</returns>
		internal static GimData GetGimElementAsGimData(GimData gimData, uint elementIndex, Dictionary<int, int> oldToNewPropertySets = null)
        {
            // The previous body copied every property set. This call copies only the sets the subtree uses.
            return gimData.GetGimElementAsGimData(gimData, elementIndex, oldToNewPropertySets);
        }

        /// <summary>
        /// Validates that all objects in the element subtree have in-range property set references before separation begins.
        /// Throws ArgumentOutOfRangeException if any object references a corrupt property set index per ADR 0003.
        /// </summary>
        private static void ValidateSeparationSubtreePropertySets(GimData gimData, uint rootElement)
        {
            HashSet<uint> visited = new();
            Queue<uint> queue = new();
            queue.Enqueue(rootElement);

            while (queue.Count > 0)
            {
                uint current = queue.Dequeue();
                if (!visited.Add(current))
                    continue;

                if (current < (uint)gimData.Elements.Count)
                {
                    int objIndex = gimData.Objects.FindIndex(o => o.element == current);
                    if (objIndex >= 0)
                    {
                        GimObject obj = gimData.Objects[objIndex];
                        if (obj.PropertySetIndex.HasValue)
                        {
                            int index = obj.PropertySetIndex.Value;
                            if (gimData.PropertySets == null || index < 0 || index >= gimData.PropertySets.Count)
                            {
                                throw new ArgumentOutOfRangeException(
                                    nameof(obj.PropertySetIndex),
                                    $"Carried object '{obj.Name}' references property set index {index}, which is out of range [0, {gimData.PropertySets?.Count ?? 0}).");
                            }
                        }
                    }

                    GimElement currentElement = gimData.Elements[(int)current];
                    if (currentElement.elements != null)
                    {
                        foreach (uint child in currentElement.elements)
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes an element belonging to a parent from the GimData and puts in its separate GimData.
        /// Carries referenced property sets, maintains shared sets, and omits unreferenced sets.
        /// Throws before mutating the source graph if any carried object references a corrupt property set index per ADR 0003.
        /// </summary>
        /// <param name="gimData">The GimData to remove the element from.</param>
        /// <param name="element">The element to remove.</param>
        /// <param name="oldParent">The parent which belongs to the element (in case of twins).</param>
        /// <param name="newParent">The new parent in the new GimData.</param>
        /// <param name="removeElements">The elements to be removed.</param>
        /// <param name="newGimData">The new GimData with only the removed elements.</param>
        /// <param name="oldToNewPropertySets">Map tracking source property set index to target index.</param>
        /// <returns>Tuple containing the modified source GimData and the new separated GimData.</returns>
        internal static Tuple<GimData, GimData> SeperateElementFromGimData(
            GimData gimData,
            uint element,
            uint oldParent,
            uint? newParent = null,
            List<uint> removeElements = null,
            GimData newGimData = null,
            Dictionary<int, int> oldToNewPropertySets = null)
        {
            if (newParent == null)
            {
                ValidateSeparationSubtreePropertySets(gimData, element);
            }

            // Object positions shift once a parent is removed, so each object is found by element id.
            int objIndex = gimData.Objects.FindIndex(o => o.element == element);
            if (objIndex >= 0 && gimData.Objects[objIndex].PropertySetIndex.HasValue)
            {
                int setIdx = gimData.Objects[objIndex].PropertySetIndex.Value;
                if (gimData.PropertySets == null || setIdx < 0 || setIdx >= gimData.PropertySets.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(gimData.Objects),
                        $"Carried object '{gimData.Objects[objIndex].Name}' references property set index {setIdx}, which is out of range [0, {gimData.PropertySets?.Count ?? 0}).");
                }
            }

            removeElements ??= new List<uint>();
            newGimData ??= new GimData();
            oldToNewPropertySets ??= new Dictionary<int, int>();
            gimData.Elements[(int)oldParent].elements.Remove(element); // Remove myself from the old parent in the old gimData
            newGimData.Elements.Add(gimData.Elements[(int)element].Clone()); // Add myself to the new gimdata
            uint newIndex = (uint)newGimData.Elements.Count - 1;

            if (newParent != null)
                newGimData.SubstituteElementInParent(newIndex, element, (uint)newParent); // Set the correct newIndex in my parent

            if (objIndex >= 0) // If element is object, create in new gimData
            {
                GimObject newObj = gimData.Objects[objIndex].Clone();
                newObj.element = newIndex;
                GimData.CarryPropertySet(gimData, newGimData, newObj, oldToNewPropertySets);
                newGimData.Objects.Add(newObj);
            }

            for (int i = 0; i < gimData.Elements[(int)element].locals.Count; i++)
            {
                if (gimData.Elements[(int)element].locals[i].parent == oldParent) // If correct local, remove from old gimData, else remove from newGimData
                    gimData.Elements[(int)element].locals.RemoveAt(i);
                else newGimData.Elements[(int)element].locals.RemoveAt(i);
            }

            if (gimData.Elements[(int)element].locals.Count == 0) // If the old gimData has no locals, no twins so remove entire element and object
            {
                removeElements.Add(element);
                if (objIndex >= 0)
                    gimData.Objects.RemoveAt(objIndex);
            }

            foreach (uint i in gimData.Elements[(int)element].elements.ToList()) // Seperate children
                (gimData, newGimData) = SeperateElementFromGimData(gimData, i, element, newIndex, removeElements, newGimData, oldToNewPropertySets);

            foreach (uint i in newGimData.Elements[(int)newIndex].elements) // Fix the children locals to myself
                newGimData.SubstituteElementInChild(newIndex, element, i);
            return new Tuple<GimData, GimData>(gimData, newGimData);
        }

        private static byte[] GetBestCompressedGlbBytes(byte[] originalGlbBytes)
        {
            if (originalGlbBytes == null || originalGlbBytes.Length == 0 || originalGlbBytes.Length < GLTFPACK_MIN_BLOB_SIZE_BYTES)
            {
                return originalGlbBytes;
            }

            string gltfpackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GLTFPACK_RELATIVE_PATH);
            if (!File.Exists(gltfpackPath))
            {
                GimLogger.Warn($"GLB compression fallback [tool-not-found]: {gltfpackPath}");
                return originalGlbBytes;
            }

            string inputPath = null;
            string outputPath = null;

            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "GIM", "gltfpack");
                Directory.CreateDirectory(tempRoot);
                string tempId = Guid.NewGuid().ToString("N");
                inputPath = Path.Combine(tempRoot, $"{tempId}.input.glb");
                outputPath = Path.Combine(tempRoot, $"{tempId}.output.glb");

                File.WriteAllBytes(inputPath, originalGlbBytes);

                ProcessStartInfo processStartInfo = new()
                {
                    FileName = gltfpackPath,
                    Arguments = $"-cf -i \"{inputPath}\" -o \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using Process process = new()
                {
                    StartInfo = processStartInfo
                };

                process.Start();
                bool didExit = process.WaitForExit(GLTFPACK_TIMEOUT_MS);
                if (!didExit)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch
                    {
                        // process may have exited while kill was attempted
                    }

                    GimLogger.Warn($"GLB compression fallback [timeout]: exceeded {GLTFPACK_TIMEOUT_MS} ms.");
                    return originalGlbBytes;
                }

                string stderr = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                {
                    GimLogger.Warn($"GLB compression fallback [process-failed]: exit-code={process.ExitCode}, stderr={stderr}");
                    return originalGlbBytes;
                }

                if (!File.Exists(outputPath))
                {
                    GimLogger.Warn($"GLB compression fallback [invalid-output]: output file not created.");
                    return originalGlbBytes;
                }

                byte[] compressedBytes = File.ReadAllBytes(outputPath);
                if (compressedBytes == null || compressedBytes.Length == 0)
                {
                    GimLogger.Warn($"GLB compression fallback [invalid-output]: output file is empty.");
                    return originalGlbBytes;
                }

                if (compressedBytes.Length >= originalGlbBytes.Length)
                {
                    GimLogger.Warn($"GLB compression fallback [not-smaller]: original={originalGlbBytes.Length} compressed={compressedBytes.Length}.");
                    return originalGlbBytes;
                }

                return compressedBytes;
            }
            catch (Exception e)
            {
                GimLogger.Warn($"GLB compression fallback [process-failed]: {e.Message}");
                return originalGlbBytes;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(inputPath) && File.Exists(inputPath))
                    {
                        File.Delete(inputPath);
                    }
                }
                catch
                {
                    // ignore cleanup failure
                }

                try
                {
                    if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch
                {
                    // ignore cleanup failure
                }
            }
        }

        private static Dictionary<uint, byte[]> CompressGlbBytesBatch(
            IReadOnlyDictionary<uint, byte[]> rawBlobs,
            string debugPrefix)
        {
            var results = new System.Collections.Concurrent.ConcurrentDictionary<uint, byte[]>();
            if (rawBlobs == null || rawBlobs.Count == 0)
            {
                return new Dictionary<uint, byte[]>();
            }

            int maxDop = Math.Max(1, Environment.ProcessorCount - 1);
            var options = new ParallelOptions { MaxDegreeOfParallelism = maxDop };

            Parallel.ForEach(rawBlobs, options, kv =>
            {
                byte[] compressed = GetBestCompressedGlbBytes(kv.Value);
                results[kv.Key] = compressed;
            });

            return new Dictionary<uint, byte[]>(results);
        }

        /// <summary>Converts GimData into its JSON representation and embedded visual data.</summary>
        /// <param name="gimData">Model to convert.</param>
        /// <param name="visualisationUnitToBe">Optional visualization unit scale.</param>
        /// <param name="progressStatus">Optional progress reporter.</param>
        /// <param name="reuseGlbsByChildId">Optional child visual data cache.</param>
        /// <returns>JSON object containing model metadata and embedded visual data.</returns>
        public static JsonGimObject GetJsonGimData(
            GimData gimData,
            int visualisationUnitToBe,
            GimProgressStatus progressStatus = null,
            IReadOnlyDictionary<int, string> reuseGlbsByChildId = null)

        {
            GimWriter writer = new GimWriter();

            Dictionary<uint, uint> elemToObj = gimData.GetElementToObjectDict(); // Generates an easy way to find out if an element is also an object.
            Dictionary<uint, (string Name, int? PropertySetIndex)> objectDisplayData = new();
            foreach (KeyValuePair<uint, uint> objectEntry in elemToObj)
            {
                GimObject gimObject = gimData.Objects[(int)objectEntry.Value];
                ValidatePropertySetIndex(gimData, gimObject.PropertySetIndex);
                objectDisplayData[objectEntry.Key] =
                    (GimPropertyHelpers.ResolveDisplayName(gimData, objectEntry.Value), gimObject.PropertySetIndex);
            }

            // Resolve names and property links before child geometry filtering can skip GLB work.
            Tuple<List<int>, List<int>> objectChildren = LoopUntilChildrenAreObjects(gimData, 0, gimData.GetElementToObjectDict());

            uint commonParent = GetNearestCommonParent(objectChildren.Item1, gimData);
            string elemName = "";
            int? rootPropertySetIndex = null;
            bool rootHasStringPropertyName = false;
            if (elemToObj.TryGetValue(0, out uint rootObjectIndex))
            {
                elemName = GimPropertyHelpers.ResolveDisplayName(gimData, rootObjectIndex);
                rootHasStringPropertyName = GimPropertyHelpers.TryGetPropertyString(gimData, rootObjectIndex, "name", out _);
                rootPropertySetIndex = gimData.Objects[(int)rootObjectIndex].PropertySetIndex;
                ValidatePropertySetIndex(gimData, rootPropertySetIndex);
            }
            if (string.IsNullOrEmpty(elemName) && !rootHasStringPropertyName && gimData.Objects.Count > 0)
            {
                // Preserve legacy fallback only when set name is absent or non-String; empty String wins.
                elemName = gimData.Objects[0].name;
            }
            GimData elemgd;
            //(_, uint NewElemIndex, string elemName) = LoopUntilMultipleChildren(gimData, (uint)elemIndex, gimData.GetElementToObjectDict());

            Dictionary<int, int> oldToNewPropertySets = new();
            elemgd = GetGimElementAsGimData(gimData, commonParent, oldToNewPropertySets); // Get a new GimData with only the obj and it's subtree.
            bool isSubtreeCopy = !ReferenceEquals(elemgd, gimData);

            if (progressStatus != null)
            {

                if (objectChildren.Item1.Count > 0) progressStatus.Create(new Dictionary<string, int>() {
                        { GimProgressStatus.WRITE_TO_DISK, 1 },
                        { GimProgressStatus.CREATE_GLB, objectChildren.Item1.Count * 100 },
                        { GimProgressStatus.CREATE_GIMBIM, 100 } });
                else
                {
                    progressStatus.Create(new Dictionary<string, int>() {
                        { GimProgressStatus.WRITE_TO_DISK, 1 },
                        { GimProgressStatus.CREATE_GLB, 1 * 100},
                        { GimProgressStatus.CREATE_GIMBIM, 100 } });
                }
            }

            bool detectJoints = false;

            for (int i = (gimData.Elements.Count) - 1; i >= 0; i--)
            {
                if (gimData.Elements[i].ElementType != GimElementType.Gim_Joint)
                    continue;
                
                detectJoints = true;
                JointUpdate(gimData);
                AddJointsToElemGd(gimData, elemgd);
                break;
            }

            if (!detectJoints)
            {
                JointDetector.DetectJoints(gimData, objectChildren.Item1);
                JointDetector.AddJoints(gimData, elemgd);
            }

            JsonGimObject jsonObject; // Create a Json object.
            byte gimUnitCurScalesIndex = 0;
            string[] gimUnits;
            // Stored binary is reused only for a full-document export.
            if (gimData.GimBin != null && !isSubtreeCopy)
            {
                gimUnits = gimData.gimUnits;
                if (gimData.unitCurr != null)
                {
                    gimUnitCurScalesIndex = gimData.unitCurr.ToByte();
                }
                jsonObject = new JsonGimObject();
                jsonObject.Name = elemName;
                jsonObject.GIMbin = gimData.GimBin;

                progressStatus?.ForceCompleteTask(GimProgressStatus.CREATE_GIMBIM);
            }
            // we create the gimbin
            else
            {
                // A subtree copy writes a new binary so its set indexes match the JSON.
                // The read-only view preserves property sets and object links for embedded data.
                Tuple<uint[], byte> binaryResult = writer.PublicGimDataToBytesWithSmallestScalesIndex(elemgd.AsReadOnly(), progressStatus); // Convert the objectgd to binary.
                uint[] gimBin = binaryResult.Item1;
                gimUnitCurScalesIndex = binaryResult.Item2;
                gimUnitCurScalesIndex = Math.Min(gimUnitCurScalesIndex, (byte)127); // capping at 127
                double maxSize = gimData.GetLargestDistance();
                maxSize /= 3;
                byte gimUnitMaxScaleIndex = GimWriter.PublicGetNearestScalesIndex(maxSize, false);
                gimUnitMaxScaleIndex = Math.Min(gimUnitMaxScaleIndex, (byte)127); // capping at 127
                                                                                  //byte gimUnitMaxByte = GimWriter.DoubleToUnitSize(maxSize, null).Item1;
                byte[] gimUnitsByte = Enumerable.Range(gimUnitCurScalesIndex, gimUnitMaxScaleIndex - gimUnitCurScalesIndex + 1).Select(i => GimWriter.ScaleIndexToGimScaleEnum[(byte)i]).ToArray();
                if (gimUnitsByte == null || gimUnitsByte.Length == 0)
                {
                    gimUnitsByte = new byte[1] { GimWriter.ScaleIndexToGimScaleEnum[gimUnitCurScalesIndex] };
                }

                jsonObject = new JsonGimObject(gimBin, elemName); // Create a Json object.
                gimUnits = new string[gimUnitsByte.Length];
                for (int i = 0; i < gimUnitsByte.Length; i++)
                {
                    gimUnits[i] = new GimUnitSize(gimUnitsByte[i]).ToString();
                }

                progressStatus?.ForceCompleteTask(GimProgressStatus.CREATE_GIMBIM);
            }

            jsonObject.GIMUnitv = new GimUnitSize(GimWriter.ScaleIndexToGimScaleEnum[gimUnitCurScalesIndex]).ToString(); // TODO: disparity between toString only handling metric, but GimWriter also handling imperial.
            jsonObject.GIMUnits = gimUnits;
            // Stamp current schema version even when no property data is present.
            jsonObject.Version = JsonVersionPolicy.FormatSchemaVersionString(
                JsonVersionPolicy.LATEST_SUPPORTED_JSON_VERSION);
            // Root object's set carries file-global properties.
            // A subtree copy's element 0 is the exported root, so its set is already in this copy's index space.
            int? effectiveRootPropertySetIndex;
            if (isSubtreeCopy)
            {
                int rootObjIndex = elemgd.Objects.FindIndex(o => o.element == 0);
                effectiveRootPropertySetIndex = rootObjIndex >= 0 ? elemgd.Objects[rootObjIndex].PropertySetIndex : null;
            }
            else
            {
                effectiveRootPropertySetIndex = rootPropertySetIndex;
            }
            jsonObject.PropertySetIndex = ValidatePropertySetIndex(elemgd, effectiveRootPropertySetIndex);
            jsonObject.PropertySets = elemgd.PropertySets.Select(propertySet =>
            {
                JsonGimPropertySet jsonPropertySet = new();
                foreach (KeyValuePair<string, GimPropertyValue> entry in propertySet.Entries)
                {
                    jsonPropertySet[entry.Key] = new JsonGimPropertyValue
                    {
                        Kind = entry.Value.Kind == GimPropertyKind.String ? "string" : "double",
                        Value = entry.Value.Kind == GimPropertyKind.String ? entry.Value.StringValue : entry.Value.DoubleValue
                    };
                }
                return jsonPropertySet;
            }).ToList();

            //Dictionary<string, int> glbs = new Dictionary<string, int>(); // dictionary to allow easy check of existing items + getting their index
            //															  // basically an inverted array: glbs[value] = index

            List<string> glbs = new();
            List<JsonGimMaterial> materials = new();
            if (visualisationUnitToBe > 0)
            {
                GimUnitSize gimUnit = new GimUnitSize((byte)visualisationUnitToBe);
                double roundingStep = gimUnit.GetRoundingStep();
                PolygonRounder.RoundGimData(gimData, roundingStep);
                jsonObject.GIMUnitv = gimUnit.ToString();
            }

            int index = 0;
            int localIndex = 0;
            if (objectChildren.Item1.Count == 0)
            {
                if (reuseGlbsByChildId != null &&
                    reuseGlbsByChildId.TryGetValue(0, out string cachedSingle) &&
                    !string.IsNullOrWhiteSpace(cachedSingle))
                {
                    jsonObject.GLB = cachedSingle;
                }
                else
                {
                    //ModelRoot modelRoot = GetGLTF(gimData, 0, useFirstLocal: true);
                    //string oldGLB = Convert.ToBase64String(modelRoot.WriteGLB().Array);

                    AssimpWriteElements assimpWriter = new(null);
                    var blob = assimpWriter.WriteGimDataToBlob(gimData, 0, elemName, useTopTransform: true);
                    string GLB = blob != null ? Convert.ToBase64String(blob.Data) : EMPTY_GLB;
                    if (blob != null)
                        GLB = Convert.ToBase64String(GetBestCompressedGlbBytes(blob.Data));
                    else
                        GLB = EMPTY_GLB;
                    jsonObject.GLB = GLB;
                }

                progressStatus?.ForceCompleteTask(GimProgressStatus.CREATE_GLB);
            }
            else // we have multiple children, we will add their GLBs one by one
            {
                // this will store all the GLBs we calculated so we don't have to calculate them again
                Dictionary<uint, string> calculatedGLBs = new();
                Dictionary<uint, int> elementToId = new();
                Dictionary<int, int> materialIndexToID = new();

                // Create entries and emit links before geometry filtering; filtered-out children never enter this list.
                List<JsonGimElement> jsonChildEntries = new(objectChildren.Item1.Count);
                for (int childIndex = 0; childIndex < objectChildren.Item1.Count; childIndex++)
                {
                    uint childElementId = (uint)objectChildren.Item1[childIndex];
                    GimElement childElement = gimData.Elements[(int)childElementId];
                    string childName = objectDisplayData[childElementId].Name;
                    JsonGimElement jsonChild = new JsonGimElement(
                        childName,
                        new JsonGimLocal(childElement.Locals[objectChildren.Item2[childIndex]]),
                        0);
                    int? childPropertySetIndex = objectDisplayData[childElementId].PropertySetIndex;
                    if (isSubtreeCopy && childPropertySetIndex.HasValue)
                    {
                        // A carried child must keep its set. A missing map entry means the export is broken, so fail instead of clearing the link.
                        if (!oldToNewPropertySets.TryGetValue(childPropertySetIndex.Value, out int mappedChildIndex))
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(childPropertySetIndex),
                                $"Exported child '{childName}' (element {childElementId}) references property set index {childPropertySetIndex.Value}, which was not carried into the subtree copy.");
                        }
                        childPropertySetIndex = mappedChildIndex;
                    }
                    jsonChild.PropertySetIndex = ValidatePropertySetIndex(
                        elemgd,
                        childPropertySetIndex);
                    jsonChildEntries.Add(jsonChild);
                }

                // Pass 1 (serial): build the raw Assimp blobs for each unique elementID and
                // classify elements that don't need gltfpack (external references without geometry,
                // or elements whose Assimp blob is null).
                Dictionary<uint, byte[]> rawBlobsToCompress = new();
                HashSet<uint> elementIsEmpty = new();
                HashSet<uint> elementIsExternalRef = new();
                HashSet<uint> elementUsesReusedGlb = new();
                Dictionary<uint, int> plannedElementToId = new();
                int plannedIndex = 0;
                foreach (uint elementID in objectChildren.Item1)
                {
                    if (rawBlobsToCompress.ContainsKey(elementID) ||
                        elementIsEmpty.Contains(elementID) ||
                        elementIsExternalRef.Contains(elementID))
                        continue;

                    // Determine this element's GLB slot index (ChildId) in a way that mirrors Pass 3.
                    if (!plannedElementToId.TryGetValue(elementID, out int plannedId))
                    {
                        plannedId = plannedIndex;
                        plannedElementToId[elementID] = plannedIndex;
                        plannedIndex++;
                    }

                    // If we have a cached GLB for this child, skip heavy blob generation entirely.
                    if (reuseGlbsByChildId != null &&
                        reuseGlbsByChildId.TryGetValue(plannedId, out string cachedGlb) &&
                        !string.IsNullOrWhiteSpace(cachedGlb))
                    {
                        elementUsesReusedGlb.Add(elementID);
                        continue;
                    }

                    GimElement gatherElem = gimData.Elements[(int)elementID];
                    if (gatherElem.ElementType == GimElementType.ExternalReference && gatherElem.Elements.Count != 1)
                    {
                        elementIsExternalRef.Add(elementID);
                        continue;
                    }

                    string gatherName = objectDisplayData[elementID].Name;
                    AssimpWriteElements gatherAssimpWriter = new(null);
                    var gatherBlob = gatherAssimpWriter.WriteGimDataToBlob(gimData, elementID, gatherName);
                    if (gatherBlob == null)
                        elementIsEmpty.Add(elementID);
                    else
                        rawBlobsToCompress[elementID] = gatherBlob.Data;
                }

                // Pass 2 (parallel): compress all collected blobs through gltfpack with bounded concurrency.
                Dictionary<uint, byte[]> compressedBlobs = CompressGlbBytesBatch(rawBlobsToCompress, "element");

                // Pass 3 (serial): original loop body, but reading the compressed GLB from the batch result.
                foreach (uint elementID in objectChildren.Item1) // Loop for every child and add it as a JsonElement.
                {
                    //(GimElement elem, uint j, string name) = LoopUntilMultipleChildren(gimData, i, elemToObj);

                    GimElement elem = gimData.Elements[(int)elementID];
                    //Note: j may not have the proper document structure to result in a proper document structure when running GetGimElementAsGimData(). The result may not be able to do cadex conversion.

                    //GimData elementgd = GetGimElementAsGimData(elemgd, j); // Create a seperate gimData containing only the child and it's subtree. For debugging purposes only.

                    // getting the material

                    int? materialIndex = GetObjectMaterialIfExists(gimData, elementID);
                    int? materialID = null;
                    if(materialIndex != null)
                    {
                        if (materialIndexToID.ContainsKey((int)materialIndex))
                        {
                            materialID = materialIndexToID[(int)materialIndex];
                        }
                        else
                        {
                            GimMaterial material = gimData.Elements[(int)materialIndex].AsMaterial();
                            JsonGimMaterial jsonMaterial = new JsonGimMaterial(material);
                            materialID = materials.Count;
                            materials.Add(jsonMaterial);
                            materialIndexToID.Add((int)materialIndex, (int)materialID);
                        }


                    }

                    string GLB = "";
                    int id;

                    if (calculatedGLBs.ContainsKey(elementID))
                    {
                        GLB = calculatedGLBs[elementID];
                        id = elementToId[elementID];
                    }
                    else
                    {
                        // Attempt to reuse a previously computed GLB for this child definition ID.

                        bool reused = false;
                        if (reuseGlbsByChildId != null)
                        {
                            if (reuseGlbsByChildId.TryGetValue(index, out string cachedGlb) && !string.IsNullOrWhiteSpace(cachedGlb))
                            {
                                GLB = cachedGlb;
                                reused = true;
                            }
                        }

                        if (!reused)
                        {
                        if (elementIsExternalRef.Contains(elementID))
                        {
                            GLB = "";
                        }
                        else if (elementIsEmpty.Contains(elementID))
                        {
                            GLB = EMPTY_GLB;
                        }
                        else
                        {
                            GLB = Convert.ToBase64String(compressedBlobs[elementID]);
                        }
                        }

#if DEBUG
                        //Directory.CreateDirectory("replace");
                        //File.WriteAllBytes("replace", Convert.FromBase64String(GLB));
                        //File.WriteAllBytes("replace", Convert.FromBase64String(GLB2));
#endif

                        calculatedGLBs.Add(elementID, GLB);
                        elementToId.Add(elementID, index);
                        glbs.Add(GLB);
                        id = index;
                        index++;
                    }

                    //if (glbs.ContainsKey(GLB)) // checking if glb was added before
                    //{
                    //	id = glbs[GLB];
                    //}
                    //else // glb was not added before, we add it and increment the index
                    //{
                    //	glbs.Add(GLB, index);

                    //	id = index;
                    //	index++;
                    //}
                    JsonGimElement jsonGimElement = jsonChildEntries[localIndex];
                    jsonGimElement.ID = id;
                    if (elem.ElementType == GimElementType.ExternalReference)
                    {
                        jsonGimElement.ObjectID = elem.ElementInts[0];
                        if (elem.ElementInts.Length > 1)
                        {
                            jsonGimElement.ProjectID = elem.ElementInts[1];
                        }
                    }
                    jsonGimElement.MaterialID = materialID;
                    jsonObject.Children.Add(jsonGimElement);
                    localIndex++;


                    progressStatus?.EntityCompletedCallback(GimProgressStatus.CREATE_GLB, 100);
                }
                progressStatus?.ForceCompleteTask(GimProgressStatus.CREATE_GLB);
            }
            jsonObject.Joints = new();
            int jointIndex = 0;
            foreach (GimJoint joint in gimData.Joints)
            {
                joint.Id = jointIndex;
                JsonGimJoint jsonJoint = new JsonGimJoint(joint);
                jsonObject.Joints.Add(jsonJoint);
                jointIndex++;
            }

            jsonObject.GLBs = new string[glbs.Count];
            jsonObject.Materials = materials;

            for (int i = 0; i < glbs.Count; i++)
            {
                jsonObject.GLBs[i] = glbs[i];
            }

            return jsonObject;
        }

        private static void AddJointsToElemGd(GimData gimData, GimData elemgd)
        {
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                if (gimData.Elements[i].elementType == GimElementType.Gim_Joint)
                {
                    elemgd.Elements.Add(gimData.Elements[i]);
                }
            }
        }

        private static uint GetNearestCommonParent(List<int> objectElements, GimData gimData)
        {
            uint parentIndex = 0;
            if (objectElements.Count == 0) return parentIndex;

            //B-1: while getting the common parents, check if part has a folder as its parent, if so return the folder. 
            if (objectElements.Count == 1)
            {
                int parentOfObject = (int)gimData.Elements[objectElements[0]].Locals[0].parent;
                if (gimData.Elements[parentOfObject].ElementType == GimElementType.D_folder)
                {
                    return (uint)parentOfObject;
                }
                return (uint)objectElements[0];
            }
            HashSet<uint> parentList = new();
            foreach (uint objectElement in objectElements)
            {
                GimElement element = gimData.Elements[(int)objectElement];
                foreach (GimLocal local in element.Locals)
                {
                    parentList.Add(local.parent);
                }
            }

            while (parentList.Count > 1)
            {
                HashSet<uint> nextParentList = new();
                foreach (uint parent in parentList)
                {
                    GimElement element = gimData.Elements[(int)parent];
                    foreach (GimLocal local in element.Locals)
                    {
                        nextParentList.Add(local.parent);
                    }
                }

                parentList = nextParentList;
            }

            parentIndex = parentList.ToArray()[0];
            GimElement potentialParentElement = gimData.Elements[(int)parentIndex];
            foreach (GimLocal local in potentialParentElement.Locals)
            {
                GimElement parent = gimData.Elements[(int)local.parent];
                //if (parent.ElementType == GimElementType.D_link)
                //{
                //    parentIndex = local.parent;
                //}
            }

            return parentIndex;
        }

        //TODO REMOVE
        private static Tuple<GimElement, uint, string> GetPolyRepOfObject(GimData gimData, uint elemIndex, Dictionary<uint, uint> elemToObj)
        {
            // here to update changes after kieran as well
            GimElement linkElement = gimData.Elements[(int)elemIndex];

            GimElement partElement = gimData.Elements[(int)linkElement.Elements[0]];


            return null;
        }

        // gets element material if it exists
        private static int? GetObjectMaterialIfExists(GimData gimData, uint elemIndex)
        {
            GimElement element = gimData.Elements[(int)elemIndex];

            if(element.ElementType == GimElementType.D_part)
            {
                for(int i=0; i<element.Elements.Count; i++)
                {
                    uint childId = element.Elements[i];
                    GimElement childElement = gimData.Elements[(int)childId];
                    if(childElement.ElementType == GimElementType.Material)
                    {
                        return (int)childId;
                    }
                }
            }

            return null;
        }

        // gets object name for links or parts
        private static string GetObjectNameIfExists(GimData gimData, uint elemIndex, Dictionary<uint, uint> elemToObj,
            out int? propertySetIndex)
        {
            propertySetIndex = null;
            if (elemToObj.TryGetValue(elemIndex, out uint objectIndex))
            {
                GimObject gimObject = gimData.Objects[(int)objectIndex];
                propertySetIndex = gimObject.PropertySetIndex;
                return GimPropertyHelpers.ResolveDisplayName(gimData, objectIndex);
            }

            GimElement element = gimData.Elements[(int)elemIndex];
            if (element.Elements.Count == 0) return "";
            uint childIndex = element.Elements[0];
            if (elemToObj.TryGetValue(childIndex, out objectIndex))
            {
                GimObject gimObject = gimData.Objects[(int)objectIndex];
                propertySetIndex = gimObject.PropertySetIndex;
                return GimPropertyHelpers.ResolveDisplayName(gimData, objectIndex);
            }

            return "";
        }

        private static int? ValidatePropertySetIndex(GimData gimData, int? propertySetIndex)
        {
            // Reject invalid links before writing so corrupt property references cannot be hidden.
            if (propertySetIndex.HasValue &&
                (propertySetIndex.Value < 0 || propertySetIndex.Value >= gimData.PropertySets.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(propertySetIndex));
            }

            return propertySetIndex;
        }

        internal static List<int> GetUniqueChildObjects(GimData gimData)
        {
            List<int> childObjects = new();
            HashSet<int> addedObjectIndexes = new();

            var elemToObj = gimData.GetElementToObjectDict();

            var res = LoopUntilChildrenAreObjects(gimData, 0, elemToObj);

            foreach (var childId in res.Item1)
            {
                var objId = (int)elemToObj[(uint)childId];

                if (addedObjectIndexes.Contains(objId)) continue;
                childObjects.Add(objId);
                addedObjectIndexes.Add(objId);
            }

            return childObjects;
        }

        private static Tuple<List<int>, List<int>> LoopUntilChildrenAreObjects(GimData gimData, uint elemIndex, Dictionary<uint, uint> elemToObj)
        {
            List<int> children = new();
            List<int> resultingChildren = new();
            List<int> localIndexes = new();

            GimElement elem = gimData.Elements[(int)elemIndex];

            if (elemToObj.Count == 1)
            {
                GimElement el = gimData.Elements[(int)elemToObj.First().Key];
                int x = 0;
                foreach (GimLocal local in el.Locals)
                {
                    children.Add((int)elemToObj.First().Key);
                    localIndexes.Add(x);
                    x++;
                }
                return new Tuple<List<int>, List<int>>(children, localIndexes);
                //return children;
            }

            if (elemToObj.Count == 1)
            {
                return new Tuple<List<int>, List<int>>(children, localIndexes);
            }

            foreach (uint childId in elem.Elements)
            {
                children.Add((int)childId);
            }

            while (true)
            {
                bool objectFound = false;

                foreach (int childId in children)
                {
                    GimElement child = gimData.Elements[(int)childId];
                    if (elemToObj.ContainsKey((uint)childId))
                    {
                        objectFound = true;
                        break;
                    }
                }

                if (objectFound)
                {
                    List<int> potentialResultingChildren = new();
                    foreach (int childId in children)
                    {
                        GimElement child = gimData.Elements[(int)childId];
                        if (!elemToObj.ContainsKey((uint)childId))
                        {
                            continue;
                        }

                        GimElement childElement = gimData.Elements[(int)childId];
                        // if we have an external reference, return it anyways
                        if (childElement.ElementType == GimElementType.ExternalReference)
                        {
                            potentialResultingChildren.Add(childId);
                            continue;
                        }
                        // other elements we have to check if they have subelements and aren't null
                        if (childElement.Elements.Count == 0) continue;

                        GimElement brepElement = gimData.Elements[(int)childElement.Elements[0]];

                        if (brepElement.ElementType != GimElementType.R_BRep)
                        {
                            if (childElement.Elements.Count == 1)
                            {
                                potentialResultingChildren.Add((int)childId);
                                continue;
                            }
                            else
                                brepElement = gimData.Elements[(int)childElement.Elements[1]];
                        }

                        if (brepElement.ElementType == GimElementType.R_BRep && brepElement.Elements.Count == 0) continue;
                        potentialResultingChildren.Add((int)childId);
                    }
                    if (potentialResultingChildren.Count > 0) resultingChildren = potentialResultingChildren;
                }

                if (objectFound && resultingChildren.Count > 1) break;

                HashSet<int> grandChildren = new HashSet<int>();

                bool childrenFound = false;
                foreach (int childId in children)
                {
                    GimElement child = gimData.Elements[childId];
                    foreach (int grandChildId in child.Elements)
                    {
                        grandChildren.Add(grandChildId);
                        childrenFound = true;
                    }
                }

                if (!childrenFound) break;

                children = grandChildren.ToList();
            }

            List<int> newResultingChildren = new();

            // we use this to not add every local for every instance of the same child
            // e.g. array 1, 127, 127, 200. We don't process 127 twice
            HashSet<int> childrenAdded = new();
            foreach (int childId in resultingChildren)
            {
                if (childrenAdded.Contains(childId)) continue;
                GimElement child = gimData.Elements[childId];
                int localIndex = 0;
                foreach (GimLocal local in child.Locals)
                {
                    GimElement parent = gimData.Elements[(int)local.Parent];
                    //if (parent.ElementType == GimElementType.D_link) // add link
                    //{
                    //    newResultingChildren.Add((int)local.parent);
                    //}
                    //else // directly add part
                    //{
                        newResultingChildren.Add(childId);
                        localIndexes.Add(localIndex);
                        localIndex++;
                    //}
                }
                childrenAdded.Add(childId);
                localIndex = 0;
            }

            return new Tuple<List<int>, List<int>>(newResultingChildren, localIndexes);

        }

        private static Tuple<GimElement, uint, string> LoopUntilMultipleChildren(GimData gimData, uint elemIndex, Dictionary<uint, uint> elemToObj)
        {
            string name = "";
            GimElement elem = gimData.Elements[(int)elemIndex];
            if (elem.elementDoubles != null && elem.elementDoubles.Length > 0 || elem.elementInts != null && elem.elementInts.Length > 0) // Check to make sure the child is never a 3d element, if it is, I don't know what will happen
            {
                GimLogger.Warn("This should ideally never happen :). Contact developers about this.");
            }
            while (elem.elements.Count == 1) // Get rid of any elements that have only one child. This is the same as its child, therefore use the child.
            {
                if (name == "" && elemToObj.ContainsKey(elem.Elements[0])) // Only objects have a name, if the current child is an object, set the name to the name of the object.
                    name = gimData.objects[(int)elemToObj[elem.Elements[0]]].name;
                GimElement subElem = gimData.elements[(int)elem.elements[0]]; // Elem has only one child, therefore we can safely index on 0
                if (subElem.elementType.Is3D()) // 3D element found as child, stop here.
                {
                    break;
                }

                List<GimLocal> pLocals = new List<GimLocal>();
                List<GimLocal> cLocals = new List<GimLocal>();
                foreach (GimLocal l in elem.locals)
                {
                    pLocals.Add(l);
                }
                foreach (GimLocal l in subElem.locals)
                {
                    if (l.parent == elemIndex)
                        cLocals.Add(l);
                }
                if (pLocals.Count == 0)
                    pLocals.Add(new GimLocal());
                if (cLocals.Count == 0)
                    cLocals.Add(new GimLocal());
                subElem.locals = new List<GimLocal>();
                foreach (GimLocal pLocal in pLocals) // multiply every combination of matrices together
                    foreach (GimLocal cLocal in cLocals)
                    {
                        GimLocal local = new GimLocal(pLocal.px + cLocal.px, pLocal.py + cLocal.py, pLocal.pz + cLocal.pz, new Matrix(pLocal) * new Matrix(cLocal));
                        subElem.locals.Add(local);
                    }
                elemIndex = elem.elements[0]; // Elem has only one child, therefore we can safely index on 0
                elem = subElem;
                //GimLogger.Info("Child with index: " + i + ". Becomes child with index: " + j + ".");
            }
            return new Tuple<GimElement, uint, string>(elem, elemIndex, name);
        }

        private static string WriteJsonToTemp(string json, string fileName)
        {
            string path = GetTempPath(fileName.Split(".")[0] + ".json");

            FileInfo directory = new FileInfo(path);
            directory.Directory.Create();

            File.WriteAllText(path, json);
            GimLogger.Info("Files written");
            return path;
        }
        private static bool VersionThisOrHigher(string[] checkVersion, string[] currentVersion)
        {
            if (currentVersion == null)
            {
                currentVersion[0] = "1";
                currentVersion[1] = "0";
                currentVersion[2] = "0";
                return false;
            }
            if (Convert.ToUInt32(checkVersion[0]) > Convert.ToUInt32(currentVersion[0])) return false;
            if (Convert.ToUInt32(checkVersion[1]) > Convert.ToUInt32(currentVersion[1])) return false;
            if (Convert.ToUInt32(checkVersion[2]) > Convert.ToUInt32(currentVersion[2])) return false;
            return true;
        }
        private static GimData JsonChildrenUpdate(JsonGimObject jsonGimData, GimData gd)
        {
            string[] currentVersion = jsonGimData.Version.Split('.').ToList().ToArray();

            if (VersionThisOrHigher(new string[] { "1", "1", "0" }, currentVersion))
            {
                gd.objectID = jsonGimData.ObjectID;
                gd.projectID = jsonGimData.ProjectID;
            }

            List<uint> elementsToDelete = new List<uint>();


            // these are the objects that are exported in the list of children
            // this list contains their indexes in GimData objects
            List<int> jsonChildrenObjects = GetUniqueChildObjects(gd);
            List<int> occurrenceOfObjects = new();
            for (int i = 0; i < jsonChildrenObjects.Count; i++)
            {
                occurrenceOfObjects.Add(0);
            }

            //         foreach (GIMcompress.JsonGimElement child in jsonGimData.Children)
            //         {
            //	if (occurrenceOfObjects.ContainsKey(child.ID)) continue;
            //	occurrenceOfObjects.Add(child.ID, 0); //set to 0, it will be incremented as soon as object is found
            //}

            // dictionary to add list of locals to remove from an element, we're going to remove these at the end to not mess with the order
            // e.g.: [5, { 1, 4, 5}]
            Dictionary<int, List<int>> elementLocalsToDelete = new();



            foreach (JsonGimElement child in jsonGimData.Children) //traversing jsonGimData's list of Children(JsonGimElement)
            {
                if (child.Crud == Crud.Delete) //delete
                {
                    gd.gimBin = null;


                    int occurence = occurrenceOfObjects[child.ID];
                    int objectIndex = jsonChildrenObjects[child.ID];
                    int elNumber = (int)gd.Objects[objectIndex].Element;
                    if (!elementLocalsToDelete.ContainsKey(objectIndex))
                    {
                        elementLocalsToDelete.Add(objectIndex, new List<int>());
                    }
                    elementLocalsToDelete[objectIndex].Add(occurence);
                    //if (gd.Elements[elNumber].Locals.Count == 0) { continue; }
                    //if (gd.Elements[(int)gd.Elements[elNumber].Locals[val].Parent].Elements.Contains((uint)elNumber))
                    //{
                    //	gd.elements[(int)gd.Elements[elNumber].Locals[val].Parent].Elements.Remove((uint)elNumber);
                    //}
                    //gd.Elements[elNumber].Locals.RemoveAt(val);


                    //GimLocal objLocal = gd.Elements[(int)gd.Objects[c].Element].Locals[val]; //nth occurrence object -> get nth local
                    //if (val != 0) { val--; }
                    //occurrenceOfObjects[key] = val;
                    //int objLocalParent = (int)objLocal.Parent; //getting the parent element of the current local: nth element
                    ////TO DO: Fix here, we do not need to get the link anymore
                    //int grandParentOfLocal = (int)gd.Elements[objLocalParent].Locals[0].Parent;//getting the grandparent of the current local: nthElement -> nthLocal -> parent -> grandParent
                    //elementsToDelete.Add((uint)objLocalParent);
                    //gd.Elements[(int)gd.Objects[c].Element].Locals.Remove(objLocal);
                    //if (gd.Elements[(int)gd.Objects[c].Element].Locals.Count == 0) {  }
                    //for (int i = 0; i < gd.Elements[grandParentOfLocal].Elements.Count; i++)
                    //{
                    //	if (gd.Elements[grandParentOfLocal].Elements[i] == objLocalParent)
                    //	{

                    //		gd.Elements[grandParentOfLocal].Elements.RemoveAt(i);
                    //		gd.RemoveElementsAndLocals(elementsToDelete);
                    //		break;
                    //	}
                    //}


                    //.Clear();
                    ;
                }
                else if (child.Crud == Crud.Update)
                {
                    gd.gimBin = null;
                    int elementToUpdateIndex;

                    //elementToUpdate = obj.Element;
                    int occurence = occurrenceOfObjects[child.ID];
                    int objectIndex = jsonChildrenObjects[child.ID];

                    var gimObject = gd.Objects[objectIndex];
                    // updating the name
                    gimObject.Name = child.Name;
                    
                    // updating the transform
                    elementToUpdateIndex = (int)gimObject.Element;

                    GimElement elementToUpdate = gd.Elements[elementToUpdateIndex];

                    elementToUpdate.Locals[occurence].Px = child.Local.XYZ[0];
                    elementToUpdate.Locals[occurence].Py = child.Local.XYZ[1];
                    elementToUpdate.Locals[occurence].Pz = child.Local.XYZ[2];

                    Matrix newMatrix = Matrix.FromRPY(child.Local.RPY[0], child.Local.RPY[1], child.Local.RPY[2]);
                    newMatrix.ApplyToLocal(elementToUpdate.Locals[occurence]);
                    if (child.Local.M == 0)
                    {
                        elementToUpdate.Locals[occurence].RightHanded = true;
                    }
                    else
                    {
                        elementToUpdate.Locals[occurence].RightHanded = false;
                    }

                }
                else if (child.Crud == Crud.Create)
                {
                    gd.gimBin = null;

                    //TO DO: check for local.parent it might not always be 0, might be 1
                    int parentIndex = 0;
                    //the new local is added at the end of the parts local 

                    GimLocal localToAdd = new GimLocal();
                    int objectIndex = jsonChildrenObjects[child.ID];
                    var obj = gd.Objects[objectIndex];
                    int partID = (int)obj.Element;
                    if (gd.Elements[partID].Locals.Count > 0)
                    {
                        parentIndex = (int)gd.Elements[partID].Locals[0].parent;
                    }
                    //gd.Elements[(int)obj.Element].Locals.Add(new GimLocal());
                    //GimElement newLink = new GimElement();
                    //newLink.ElementType = GimElementType.D_link;
                    //newLink.Locals.Add(new GimLocal());
                    localToAdd.Px = child.Local.XYZ[0];
                    localToAdd.Py = child.Local.XYZ[1];
                    localToAdd.Pz = child.Local.XYZ[2];

                    //localToAdd.Xx = (Math.Cos(child.Local.RPY[2]) * Math.Cos(child.Local.RPY[1]));
                    //localToAdd.Xy = (Math.Cos(child.Local.RPY[2]) * Math.Sin(child.Local.RPY[1]) * Math.Sin(child.Local.RPY[0]));
                    //localToAdd.Xz = (Math.Cos(child.Local.RPY[2]) * Math.Sin(child.Local.RPY[1]) * Math.Cos(child.Local.RPY[0]) + Math.Sin(child.Local.RPY[2]) * Math.Sin(child.Local.RPY[0]));
                    //localToAdd.Yx = Math.Sin(child.Local.RPY[2]) * Math.Cos(child.Local.RPY[1]);
                    //localToAdd.Yy = Math.Sin(child.Local.RPY[2]) * Math.Sin(child.Local.RPY[1]) * Math.Sin(child.Local.RPY[0]) + Math.Cos(child.Local.RPY[2] * Math.Cos(child.Local.RPY[0]));
                    //localToAdd.Yz = Math.Sin(child.Local.RPY[2]) * Math.Sin(child.Local.RPY[1]) * Math.Cos(child.Local.RPY[0]) - Math.Cos(child.Local.RPY[2]) * Math.Sin(child.Local.RPY[0]);
                    //localToAdd.Zx = -Math.Sin(child.Local.RPY[1]);
                    //localToAdd.Zy = Math.Cos(child.Local.RPY[1]) * Math.Sin(child.Local.RPY[0]);
                    //localToAdd.Zz = Math.Cos(child.Local.RPY[1]) * Math.Cos(child.Local.RPY[0]);

                    Matrix matrix = Matrix.FromRPY(child.Local.RPY[0], child.Local.RPY[1], child.Local.RPY[2]);
                    matrix.ApplyToLocal(localToAdd);

                    localToAdd.parent = (uint)parentIndex;
                    if (child.Local.M == 0)
                    {
                        localToAdd.RightHanded = true;
                    }
                    else
                    {
                        localToAdd.RightHanded = false;
                    }
                    gd.Elements[partID].Locals.Add(localToAdd);
                    //int linkID = gd.Elements.Count;
                    //localToAdd.Parent = (uint)linkID;
                    //gd.Elements[partID].Locals.Add(localToAdd);
                    //newLink.Elements.Add((uint)partID);
                    //gd.Elements.Add(newLink);
                    //TO DO: parent of link can be the grandparent of the part 

                }
                
                occurrenceOfObjects[child.ID]++;
                
            }

            int objectsRemoved = 0;
            foreach (var keypair in elementLocalsToDelete)
            {
                int objIndex = keypair.Key - objectsRemoved;
                int elId = (int)gd.Objects[objIndex].Element;
                GimElement element = gd.Elements[elId];
                List<int> localList = keypair.Value;
                // remove all instances of element = remove element
                if (localList.Count == element.Locals.Count)
                {
                    List<uint> parentList = new();
                    foreach (int localIndex in localList)
                    {
                        parentList.Add(element.Locals[localIndex].parent);
                    }
                    foreach (var parentId in parentList)
                    {
                        GimElement parentElement = gd.Elements[(int)parentId];
                        parentElement.Elements.Remove((uint)elId);
                    }

                    element.Locals = new();
                    gd.objects.RemoveAt(objIndex);
                    gd.RemoveElementsAndTheirChildren(new List<uint>() { (uint)elId });
                    objectsRemoved++;
                    continue;
                }
                localList.Sort();
                for (int i = localList.Count - 1; i >= 0; i--)
                {
                    int localIndex = localList[i];
                    element.Locals.RemoveAt(localIndex);
                }


            }

            return gd;
        }

        internal static GimData GimJsonToGimData(JsonGimObject jsonObj)
        {
            GimReader reader = new GimReader();

            byte[] gimBin = jsonObj.GIMbin;
            GimData gd = reader.BytesToGimData(gimBin);

            // Remember the exported root object before a document root is inserted. That insert shifts every object off element 0, and the JSON root set still belongs to this object.
            GimObject preShiftRootObject = null;
            bool syntheticRootInserted = gd.Elements[0].elementType != GimElementType.D_root;

            if (syntheticRootInserted)
            {
                Dictionary<uint, uint> elementToObject = gd.GetElementToObjectDict();
                if (elementToObject.TryGetValue(0, out uint preShiftRootObjIndex))
                {
                    preShiftRootObject = gd.Objects[(int)preShiftRootObjIndex];
                }

                // we add the root element
                GimElement rootElement = new GimElement { ElementType = GimElementType.D_root };

                // we push all the other element indexes by 1, because the root element will offset everything
                int index = 0;
                foreach (GimElement element in gd.Elements)
                {
                    index++;
                    for (int i = 0; i < element.Elements.Count; i++)
                    {
                        element.Elements[i] += 1;
                    }
                    for (int i = 0; i < element.Locals.Count; i++)
                    {
                        element.Locals[i].parent += 1;
                    }
                    // elements with no parents, previously, now point to the root
                    if (element.Locals.Count == 0 && element.ElementType != GimElementType.Gim_Joint)
                    {
                        element.Locals.Add(new GimLocal { parent = 0 });
                        rootElement.Elements.Add((uint)index);
                    }
                }
                gd.Elements.Insert(0, rootElement);
                gd.objects.ForEach(o => o.element++);
            }

            gd.GimBin = gimBin;

            // Child ids follow the walk from the document root, so take this list after a synthetic root is inserted and before child structure updates.
            List<GimObject> jsonChildObjects = GetUniqueChildObjects(gd)
                .Select(objectIndex => gd.Objects[objectIndex]).ToList();

            // Restore joints before applying child structure updates.
            gd.Joints = new();
            gd.GimUnits = jsonObj.GIMUnits;
            //gd.unitToBe = new GimUnitSize(jsonObj.Gim)

            // Joints and Materials are nullable on JsonGimObject (readers only assign when the
            // corresponding JSON key is present), so guard with null-conditional access before
            // enumerating. Any future Materials consumer added here must follow the same pattern.
            if (jsonObj.Joints?.Count > 0)
            {
                foreach (JsonGimJoint joint in jsonObj.Joints)
                {
                    var type = joint.Type switch
                    {
                        "Continouous" => GimJointType.Continouous,
                        "Revolute" => GimJointType.Revolute,
                        "Prismatic" => GimJointType.Prismatic,
                        "Fixed" => GimJointType.Fixed,
                        "Floating" => GimJointType.Floating,
                        "Planar" => GimJointType.Planar,
                        _ => throw new Exception("Invalid Joint Type"),
                    };
                    Vect[] vect = new Vect[joint.Origin.Count];
                    int i = 0;
                    foreach (double[] val in joint.Origin)
                    {
                        vect[i] = val;
                        i++;
                    }

                    GimJoint newGimJoint = new GimJoint(joint.Parent, joint.Child, type, (int)joint.Crud, joint.Limit, joint.Calibration, joint.Dynamics, joint.Mimic_Multiplier, joint.Mimic_Offset, vect);

                    gd.Joints.Add(newGimJoint);
                }
            }

            // Apply child changes before restoring their property links.
            if (jsonObj.Children.Count > 0)
            {
                gd = JsonChildrenUpdate(jsonObj, gd);
            }

            // Restore root-owned property sets and object links after child objects settle.
            RestoreJsonPropertySetsAndLinks(jsonObj, gd, jsonChildObjects, preShiftRootObject, syntheticRootInserted);

            return gd;
        }

        /// <summary>Restores root property sets and object links after child updates.</summary>
        /// <param name="jsonObj">JSON object containing root-owned property data.</param>
        /// <param name="gd">Model receiving property data.</param>
        /// <param name="jsonChildObjects">Child objects in the JSON child-ID order.</param>
        internal static void RestoreJsonPropertySetsAndLinks(
            JsonGimObject jsonObj,
            GimData gd,
            List<GimObject> jsonChildObjects,
            GimObject targetRootObject = null,
            bool syntheticRootInserted = false)
        {
            if (jsonObj.PropertySets != null && jsonObj.PropertySets.Count > 0)
            {
                List<GimPropertySet> newPropertySets = new(jsonObj.PropertySets.Count);
                foreach (JsonGimPropertySet jsonSet in jsonObj.PropertySets)
                {
                    GimPropertySet propertySet = new();
                    foreach (KeyValuePair<string, JsonGimPropertyValue> entry in jsonSet)
                    {
                        try
                        {
                            switch (entry.Value.Kind)
                            {
                                case "string":
                                    propertySet.Entries[entry.Key] = GimPropertyValue.FromString((string)entry.Value.Value);
                                    break;
                                case "double":
                                    propertySet.Entries[entry.Key] = GimPropertyValue.FromDouble((double)entry.Value.Value);
                                    break;
                                default:
                                    throw new InvalidOperationException($"Invalid property value for '{entry.Key}'.");
                            }
                        }
                        catch (InvalidCastException exception)
                        {
                            throw new InvalidOperationException($"Invalid property value for '{entry.Key}'.", exception);
                        }
                    }

                    newPropertySets.Add(propertySet);
                }

                gd.PropertySets = newPropertySets;
            }

            if ((jsonObj.PropertySets == null || jsonObj.PropertySets.Count == 0) && !jsonObj.PropertySetIndex.HasValue &&
                (jsonObj.Children == null || !jsonObj.Children.Any(child => child.PropertySetIndex.HasValue))) return;

            int? rootPropertySetIndex = ValidatePropertySetIndex(gd, jsonObj.PropertySetIndex);
            // The inserted document root has no object. The JSON root set goes on the object captured before the shift.
            if (syntheticRootInserted)
            {
                if (targetRootObject != null && gd.Objects.Contains(targetRootObject))
                    targetRootObject.PropertySetIndex = rootPropertySetIndex;
                else if (rootPropertySetIndex.HasValue)
                    throw new ArgumentOutOfRangeException(nameof(jsonObj.PropertySetIndex));
            }
            else
            {
                Dictionary<uint, uint> elementToObject = gd.GetElementToObjectDict();
                if (elementToObject.TryGetValue(0, out uint rootObjectIndex))
                    gd.Objects[(int)rootObjectIndex].PropertySetIndex = rootPropertySetIndex;
                else if (rootPropertySetIndex.HasValue)
                    throw new ArgumentOutOfRangeException(nameof(jsonObj.PropertySetIndex));
            }

            foreach (JsonGimElement child in jsonObj.Children)
            {
                if (child.Crud == Crud.Delete) continue;
                if (jsonChildObjects == null ||
                    child.ID < 0 || child.ID >= jsonChildObjects.Count)
                {
                    if (child.PropertySetIndex.HasValue)
                        throw new ArgumentOutOfRangeException(nameof(child.ID));
                    continue;
                }

                GimObject childObject = jsonChildObjects[child.ID];
                if (gd.Objects.Contains(childObject))
                    childObject.PropertySetIndex = ValidatePropertySetIndex(gd, child.PropertySetIndex);
            }
        }

        private static void FixGimJointReferences(int jointIndex, GimData gd)
        {
            for (int i = 0; i < gd.Elements.Count; i++)
            {
                if (gd.Elements[i].Elements.Contains((uint)jointIndex))
                {
                    gd.Elements[i].Elements.Remove((uint)jointIndex);
                }
            }
        }

        private static GimData JointUpdate(GimData gd)
        {
            foreach (GimJoint joint in gd.Joints)
            {
                if (joint.Crud == 3)
                {
                    for (int i = 0; i < gd.Elements.Count; i++)
                    {
                        if (gd.Elements[i].ElementType == GimElementType.Gim_Joint && gd.Elements[i].ElementInts[1] == joint.Parent && gd.Elements[i].ElementInts[2] == joint.Child)
                        {
                            gd.Elements.RemoveAt(i);
                            FixGimJointReferences(i, gd);
                            break;
                        }
                    }
                }
                if (joint.Crud == 2)
                {
                    for (int i = 0; i < gd.Elements.Count; i++)
                    {
                        if (gd.Elements[i].ElementType == GimElementType.Gim_Joint && gd.Elements[i].ElementInts[1] == joint.Parent && gd.Elements[i].ElementInts[2] == joint.Child)
                        {
                            gd.Elements[i].ElementInts[0] = (int)joint.Type;
                            gd.Elements[i].ElementInts[1] = joint.Parent;
                            gd.Elements[i].ElementInts[2] = joint.Child;
                            gd.Elements[i].ElementDoubles[0] = joint.Origin[0][0];
                            gd.Elements[i].ElementDoubles[1] = joint.Origin[0][1];
                            gd.Elements[i].ElementDoubles[2] = joint.Origin[0][2];
                            gd.Elements[i].ElementDoubles[3] = joint.Origin[1][0];
                            gd.Elements[i].ElementDoubles[4] = joint.Origin[1][1];
                            gd.Elements[i].ElementDoubles[5] = joint.Origin[1][2];
                            gd.Elements[i].ElementDoubles[6] = joint.Calibration[0];
                            gd.Elements[i].ElementDoubles[7] = joint.Calibration[1];
                            gd.Elements[i].ElementDoubles[8] = joint.Dynamics[0];
                            gd.Elements[i].ElementDoubles[9] = joint.Dynamics[1];
                            gd.Elements[i].ElementDoubles[10] = joint.Limit[0];
                            gd.Elements[i].ElementDoubles[11] = joint.Limit[1];
                            gd.Elements[i].ElementDoubles[12] = joint.Limit[2];
                            gd.Elements[i].ElementDoubles[13] = joint.Limit[3];
                            gd.Elements[i].ElementDoubles[15] = joint.Mimic_Multiplier;
                            gd.Elements[i].ElementDoubles[16] = joint.Mimic_Offset;
                            break;
                        }

                    }
                }
                if (joint.Crud == 0)
                {
                    GimElement newJoint = new GimElement();
                    newJoint.ElementType = GimElementType.Gim_Joint;
                    newJoint.elementInts = new int[3];
                    newJoint.elementDoubles = new double[17];
                    newJoint.elementInts[0] = (int)joint.Type;
                    newJoint.ElementInts[1] = joint.Parent;
                    newJoint.ElementInts[2] = joint.Child;
                    newJoint.ElementDoubles[0] = joint.Origin[0][0];
                    newJoint.ElementDoubles[1] = joint.Origin[0][1];
                    newJoint.ElementDoubles[2] = joint.Origin[0][2];
                    newJoint.ElementDoubles[3] = joint.Origin[1][0];
                    newJoint.ElementDoubles[4] = joint.Origin[1][1];
                    newJoint.ElementDoubles[5] = joint.Origin[1][2];
                    newJoint.ElementDoubles[6] = joint.Calibration[0];
                    newJoint.ElementDoubles[7] = joint.Calibration[1];
                    newJoint.ElementDoubles[8] = joint.Dynamics[0];
                    newJoint.ElementDoubles[9] = joint.Dynamics[1];
                    newJoint.ElementDoubles[10] = joint.Limit[0];
                    newJoint.ElementDoubles[11] = joint.Limit[1];
                    newJoint.ElementDoubles[12] = joint.Limit[2];
                    newJoint.ElementDoubles[13] = joint.Limit[3];
                    newJoint.ElementDoubles[15] = joint.Mimic_Multiplier;
                    newJoint.ElementDoubles[16] = joint.Mimic_Offset;

                    gd.Elements.Add(newJoint);
                }
            }

            return gd;
        }

        private static void ApplyMirroring(GimData mainGimData, GimOptions gimOptions, Tuple<List<int>, List<int>> childObjectIDs)
        {
            if (!gimOptions.mirror) return;

            for (int i = 0; i < childObjectIDs.Item1.Count; i++)
            {
                int elemID = childObjectIDs.Item1[i];
                int localID = childObjectIDs.Item2[i];

                GimElement elem = mainGimData.Elements[elemID];
                GimLocal local = elem.Locals[localID];

                Matrix originalMatrix = new Matrix(local);

                (double roll, double pitch, double yaw) = originalMatrix.GetRPY();

                double pi = Math.PI;
                double newRoll = (2 * pi - roll) % (2 * pi);
                double newPitch = (2 * pi - pitch) % (2 * pi);


                Matrix newMatrix = Matrix.FromRPY(newRoll, newPitch, yaw);
                newMatrix.ApplyToLocal(local);

                local.Pz = -local.Pz;


                local.rightHanded = !local.rightHanded;

            }
        }

        /// <summary>
        /// Updates the transformation matrix of GIM elements based on the provided GIM options.
        /// Applies scaling, mirroring, Rotation and translation (in that order).
        /// Marks the gimdata as modified if changes occurred (sets gimbin to null).
        /// </summary>
        /// <param name="mainGimData">The primary GIM data containing the elements to be transformed.</param>
        /// <param name="gimOptions">Options specifying the required rotations and translations.</param>
        private static void ApplyTransformations(GimData mainGimData, GimOptions gimOptions)
        {
            // applying the scaling
            ApplyScaling(mainGimData, gimOptions);
            // No need to do scaling and rotation in this case (optimizes run speed)
            if (gimOptions.IsDefaultTransformation() && !gimOptions.mirror) return;
            // Getting the top level objects to apply the transformation to them
            var childObjectIDs = LoopUntilChildrenAreObjects(mainGimData, 0, mainGimData.GetElementToObjectDict());
            // applying the mirroring
            ApplyMirroring(mainGimData, gimOptions, childObjectIDs);
            // applying rotation and translation
            ApplyRotationAndTranslation(mainGimData, gimOptions, childObjectIDs);
        }

        private static void ApplyScaling(GimData mainGimData, GimOptions gimOptions)
        {
            if (gimOptions.scale == 1) return;
            ScaleChanger scaler = new(gimOptions.scale);
            scaler.ScaleGimData(mainGimData);
            mainGimData.GimBin = null;
        }

        private static void ApplyRotationAndTranslation(GimData mainGimData, GimOptions gimOptions,
            Tuple<List<int>, List<int>> childObjectIDs)
        {
            if (gimOptions.IsDefaultTransformation()) return;

            // In this case, we just get the top level part and add it to the list of elements to modify
            if (childObjectIDs.Item1.Count == 0)
            {
                int elementIndex = -1;
                for (int i = 0; i < mainGimData.Elements.Count; i++)
                {
                    var currentElement = mainGimData.Elements[i];
                    if (currentElement.ElementType == GimElementType.D_root || currentElement.ElementType == GimElementType.D_folder)
                    {
                        if (currentElement.Elements.Count == 1)
                            continue;
                        else if (currentElement.Elements.Count > 1)
                        {
                            elementIndex = i;
                            break;
                        }
                    }
                    if (currentElement.ElementType == GimElementType.D_part)
                    {
                        var currentElementLocals = currentElement.Locals;
                        int parentIndex = (int)currentElementLocals[0].Parent;
                        elementIndex = parentIndex;
                        break;
                    }
                }

                if (elementIndex < 0) return;

                GimElement elem = mainGimData.Elements[elementIndex];
                GimLocal local = elem.Locals[0];

                if (local == null) return;

                childObjectIDs.Item1.Add(elementIndex);
                childObjectIDs.Item2.Add(0);
            }

            // marking the gimData as modified
            mainGimData.GimBin = null;

            // we apply the transformation to all the children
            for (int i = 0; i < childObjectIDs.Item1.Count; i++)
            {
                int elemID = childObjectIDs.Item1[i];
                int localID = childObjectIDs.Item2[i];

                GimElement elem = mainGimData.Elements[elemID];
                GimLocal local = elem.Locals[localID];
                gimOptions.ApplyToElementLocal(elem, local);
            }
        }

        /// <summary>
        /// Compares the hash codes of two files, one of which may contain GimData objects, to determine if any changes have occurred.
        /// </summary>
        /// <param name="originalFilePath">The file path to the original file.</param>
        /// <param name="editedFilePath">The file path to the edited file.</param>
        /// <param name="progressStatus">An optional progress status object for tracking progress.</param>
        /// <returns>
        /// A Task containing a boolean value indicating whether the hash codes of the GimData objects (if present) in the files differ, 
        /// which signifies whether the files have changed.
        /// </returns>
        /// <remarks>
        /// This method performs hash code comparison between two files. If both files have the ".json" extension, it deserializes them into 
        /// GimData objects, calculates their hash codes, and compares them. If only one of the files has the ".json" extension, it reads 
        /// the other file and performs the comparison with the GimData object hash code. Progress can be tracked using the optional 
        /// progressStatus parameter.
        /// </remarks>
        private static Task<bool> CompareGimDataHashCodeLogic(string originalFilePath, string editedFilePath, GimProgressStatus progressStatus = null)
        {
            bool isFileChanged = false;

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            string originalFileExtension = Path.GetExtension(originalFilePath);

            string editedFileExtension = Path.GetExtension(editedFilePath);

            if (originalFileExtension == ".json" & editedFileExtension == ".json")
            {
                string originalFile = File.ReadAllText(originalFilePath);

                string editedFile = File.ReadAllText(editedFilePath);

                JsonGimObject originalFileObject = JsonManager.ReadGimJson(originalFile);

                JsonGimObject editedFileObject = JsonManager.ReadGimJson(editedFile);

                GimReader reader = new GimReader();

                GimData originalGimData = reader.BytesToGimData(originalFileObject.GIMbin);

                GimData eitedGimData = reader.BytesToGimData(editedFileObject.GIMbin);

                int originalHashCode = originalGimData.GetGimDataHashCode();

                int editedHashCode = eitedGimData.GetGimDataHashCode();

                if (originalHashCode != editedHashCode)
                {
                    isFileChanged = true;
                }
                else
                {
                    isFileChanged = false;
                }

            } 
            else if (originalFileExtension != ".json" & editedFileExtension == ".json") 
            {
                GimData originalGimData = ReadToGimData(originalFilePath, originalFileExtension.Replace(".", ""));

                int originalHashCode = originalGimData.GetGimDataHashCode();

                string editedFile = File.ReadAllText(editedFilePath);

                JsonGimObject editedFileObject = JsonManager.ReadGimJson(editedFile);

                GimReader reader = new GimReader();

                GimData eitedGimData = reader.BytesToGimData(editedFileObject.GIMbin);

                int editedHashCode = eitedGimData.GetGimDataHashCode();

                if (originalHashCode != editedHashCode)
                {
                    isFileChanged = true;
                }
                else
                {
                    isFileChanged = false;
                }
            }

            else if (originalFileExtension == ".json" & editedFileExtension != ".json")
            {
                GimData editedGimData = ReadToGimData(editedFilePath, editedFileExtension.Replace(".", ""));

                int editedHashCode = editedGimData.GetGimDataHashCode();

                string originalFile = File.ReadAllText(originalFilePath);

                JsonGimObject originalFileObject = JsonManager.ReadGimJson(originalFile);

                GimReader reader = new GimReader();

                GimData originalGimData = reader.BytesToGimData(originalFileObject.GIMbin);

                int originalHashCode = originalGimData.GetGimDataHashCode();

                if (originalHashCode != editedHashCode)
                {
                    isFileChanged = true;
                }
                else
                {
                    isFileChanged = false;
                }
            }

            else if (originalFileExtension != ".json" & editedFileExtension != ".json")
            {
                GimData editedGimData = ReadToGimData(editedFilePath, editedFileExtension.Replace(".", ""));

                int editedHashCode = editedGimData.GetGimDataHashCode();

                GimData originalGimData = ReadToGimData(originalFilePath, originalFileExtension.Replace(".", ""));

                int originalHashCode = originalGimData.GetGimDataHashCode();

                if (originalHashCode != editedHashCode)
                {
                    isFileChanged = true;
                }
                else
                {
                    isFileChanged = false;
                }
            }
            return Task.FromResult(isFileChanged);
        }

        private static Task<List<string>> ConvertBinaryGlbToStringLogic(List<byte[]> glbs)
        {
            List<string> glbStrings = new List<string>();

            foreach (byte[] glb in glbs)
            {
                ModelRoot glbData = ModelRoot.ParseGLB(glb);

                string glbString = glbData.GetJsonPreview();

                glbStrings.Add(glbString);
            }

            return Task.FromResult(glbStrings);
        }

        /// <summary>
        /// Processes a collection of GLB files and their associated transformation data, converts them into GimData objects, and merges them into a single JsonGimObject.
        /// </summary>
        /// <param name="glbObjects">A dictionary with byte arrays representing GLB files as keys, and tuples of double arrays representing XYZ coordinates and RPY (roll, pitch, yaw) values as values.</param>
        /// <param name="fileName">The name of the output file</param>
        /// <param name="fileType">The output file type</param>
        /// <param name="progressStatus">Optional progress bar</param>
        /// <param name="options">GimOptions for unit conversions</param>
        /// <returns>
        /// A Task containing a JsonGimObject which represents a merged scene of all processed GimData objects, incorporating their spatial transformations.
        /// </returns>
        /// <remarks>
        /// This method iterates over each GLB file in the provided dictionary. For each GLB file, it extracts the file data and associated XYZ coordinates and RPY values. Each GLB file is then converted into a ModelData_Model object, which is subsequently read and transformed into a GimData object. These GimData objects are accumulated in a list along with their transformation data. Finally, all GimData objects are merged into a single JsonGimObject, which represents the combined scene. This method is useful for constructing complex scenes from multiple GLB files, each with its unique spatial transformation.
        /// </remarks>
        private static string GimMapsSceneDownloadLogic(List<GLBStruct> glbObjects, string fileType, string fileName,
            GimOptions options = null, GimProgressStatus progressStatus = null)
        {
            GimData[] gimDatasToCombine = new GimData[glbObjects.Count];
            (double[], double[])[] transformations = new (double[], double[])[glbObjects.Count];
            try
            {
                Parallel.For(0, glbObjects.Count, (int i) =>
                {
                    var glbObject = glbObjects[i];

                    byte[] glb = glbObject.Bytes; // byte array (GLB file)
                    double[] xyz = glbObject.Xyz; // XYZ coordinates
                    double[] rpy = glbObject.Rpy; // RPY (roll, pitch, yaw) values
                    string name = glbObject.Name;

                    GimData gd = GLTFReadElements.ReadGLBToGimData(glb);

                    if (gd.Objects.Count == 0)
                    {
                        PrepareMapObject(gd, name);
                    }

                    transformations[i] = (xyz, rpy);

                    gimDatasToCombine[i] = (gd);
                });
            }

            catch (Exception e) 
            {
                throw new Exception("Error in dictionary:" + e);
            }

            GimData sceneDownload;
            
            try
            {
                sceneDownload = MergeAndCreateGIMMapsJson(gimDatasToCombine, transformations, options);
            }
            
            catch(Exception e) 
            {
                throw new Exception("Error in merging: " + e);
            }
            //hardcoded for now
            //sceneDownload.GIMUnitToBe = GimUnit.mm.ToString();

            return WriteFromGimData(sceneDownload, fileType, fileName, true, progressStatus) ;
        }

        internal static async Task<JsonGimObject> MergeJson(List<GimData> promoteGimDatas, JsonGimObject mergedJson)
        {

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            GimData mainGimData = new GimData();
            mainGimData.Elements.Add(new GimElement() { elementType = GimElementType.D_root });
            for (int i = 0; i < promoteGimDatas.Count; i++)
            {
                int folderindex = mainGimData.Elements.Count;
                try
                {
                    mainGimData.Elements.Add(new GimElement()
                    {
                        elementType = GimElementType.D_folder,
                        Locals = new List<GimLocal>() { new GimLocal() }
                    });
                }
                catch (Exception e) 
                {
                    throw new Exception("Error in adding folder: " + e);
                }
                GimObject gimObject;
                try
                {
                    gimObject = GimObjectFactory.Create((uint)folderindex, promoteGimDatas[i].Objects[0].Name); // the next element is the definition of this object
                    mainGimData.Objects.Add(gimObject);
                    mainGimData.Elements[0].Elements.Add((uint)folderindex);
                }
                catch (Exception e) 
                {
                    throw new Exception("Error in adding gim object: "+ e);
                }

                Dictionary<int, int> oldToNewPropertySets = new();
                GimObject rootObj = promoteGimDatas[i].Objects.Find(o => o.Element == 0)
                    ?? (promoteGimDatas[i].Objects.Count > 0 ? promoteGimDatas[i].Objects[0] : null);
                if (rootObj != null && rootObj.PropertySetIndex.HasValue)
                {
                    gimObject.PropertySetIndex = rootObj.PropertySetIndex;
                    GimData.CarryPropertySet(promoteGimDatas[i], mainGimData, gimObject, oldToNewPropertySets);
                }

                HashSet<uint> visitedElements = new HashSet<uint>();

                try
                {
                    foreach (uint elementIndex in promoteGimDatas[i].Elements[0].Elements)
                    {
                        mainGimData.AddSecondaryGimData(promoteGimDatas[i], elementIndex, (uint)folderindex, 0, true, oldToNewPropertySets);

                    }
                }
                catch (Exception e) 
                {
                    throw new Exception("Error in adding secondary GimData:" + e);
                }
                
                //ModelRoot tempModelRoot = GetGLTF(mainGimData, 0);
                //tempModelRoot.SaveGLB(@"E:\\GIMFiles\\beforemergetest.glb");
                //mergedJson.GLBs[i] = Convert.ToBase64String(tempModelRoot.WriteGLB().Array);





                //foreach (GimObject obj in gd.Objects)
                //{
                //    if (obj.Element == 0) { continue; }// skipping root
                //    GimData objGimData = GetGimElementAsGimData(gd, obj.Element);
                //    mainGimData.AddPartialGimDataToThis(objGimData,obj.Element, (uint)mainGimData.Elements.Count, gd.Elements[(int)obj.Element].Locals[0].Parent);
                //    partialGimData.Add(objGimData);
                //}
            }
            //await GimDataToJsonAsync(mainGimData, "mergetest");

            try
            {
                mergedJson = GetJsonGimData(mainGimData, -1);
            }
            catch (Exception e) 
            {
                throw new Exception("Error in creating jsongimdata: "+ e);
            }
            

            //ModelRoot modelRoot = GetGLTF(mainGimData, 0);
            //modelRoot.SaveGLB(@"E:\\GIMFiles\\mergetest.glb");
            //jsonObject.GLB = Convert.ToBase64String(modelRoot.WriteGLB().Array);
            //string json = JsonConvert.SerializeObject(mergedJson, settings);
            //WriteJson(json, Path.GetFileName("mergetest"));

            return mergedJson;
        }


        /// <summary>
        /// Merges a list of <see cref="GimData"/> objects into a single <see cref="JsonGimObject"/>, applying specified transformations to each. This method is designed for combining multiple 3D data structures into a unified format, suitable for further processing or visualization.
        /// </summary>
        /// <param name="promoteGimDatas">A list of <see cref="GimData"/> objects to be merged. Each <see cref="GimData"/> represents a distinct set of 3D data elements.</param>
        /// <param name="transformations">A list of tuples, where each tuple contains two double arrays representing transformation parameters (such as position and rotation) for the corresponding <see cref="GimData"/> object.</param>
        /// <param name="options">GimOptions for unit conversions</param>
        /// <returns>
        /// A <see cref="Task"/> that, when completed, results in a <see cref="GimData"/> representing the merged 3D scene, incorporating the individual transformations of each <see cref="GimData"/>.
        /// </returns>
        /// <exception cref="Exception">Throws an exception if an error occurs during the merging process, including issues in adding folders, gim objects, secondary gim data, or in the final conversion to <see cref="JsonGimObject"/>.</exception>
        /// <remarks>
        /// This method performs a complex merging operation, which is essential in scenarios where multiple 3D data sets need to be combined into a single coherent structure. It handles the intricacies of merging different elements and applying transformations to ensure accurate representation of the combined data.
        /// </remarks>
        private static GimData MergeAndCreateGIMMapsJson(GimData[] promoteGimDatas, (double[], double[])[] transformations, GimOptions options = null)
        {
            GimData mainGimData = new GimData();
            mainGimData.Objects = new List<GimObject>();
            mainGimData.Elements = new List<GimElement>();


            mainGimData.Elements.Add(new GimElement() { elementType = GimElementType.D_root });
            int mainFolderIndex = mainGimData.Elements.Count;
            mainGimData.Elements.Add(new()
            {
                ElementType = GimElementType.D_folder
            });

            mainGimData.AddChildToParent((uint)mainFolderIndex, 0);

            for (int i = 0; i < promoteGimDatas.Length; i++)
            {
                int folderindex = mainGimData.Elements.Count;

                try
                {
                    mainGimData.Elements.Add(new GimElement()
                    {
                        elementType = GimElementType.D_folder,
                    });
                    GimObject gimObject;
                    gimObject = GimObjectFactory.Create((uint)mainGimData.Elements.Count, promoteGimDatas[i].Objects[0].Name); // the next element is the definition of this object
                    mainGimData.Objects.Add(gimObject);
                    //mainGimData.Elements[0].Elements.Add((uint)folderindex);
                    mainGimData.AddChildToParent((uint)folderindex, (uint)mainFolderIndex);
                }
                catch (Exception e)
                {
                    throw new Exception("Error in adding gim object number: " + i + " " + e);
                }

                //HashSet<uint> visitedElements = new HashSet<uint>();

                try
                {
                    foreach (uint elementIndex in promoteGimDatas[i].Elements[0].Elements)
                    {
                        mainGimData.AddGimMapsSecondaryGimData(promoteGimDatas[i], elementIndex, (uint)folderindex, 0, transformations[i], options.visualisationUnitToBe);

                    }
                }
                catch (Exception e) 
                {
                    throw new Exception("Error in adding secondary gimdata: "+e);
                }
            }
            mainGimData = PolygonRounder.ClipGimDataByXY(mainGimData, options.minX, options.minY, options.maxX, options.maxY);
            
            return mainGimData;
        }

        /// <summary>
        /// Adds a new <see cref="GimObject"/> with a specified name to the provided <see cref="GimData"/> instance. This method is primarily used for preparing map objects within a 3D data structure.
        /// </summary>
        /// <param name="gd">The <see cref="GimData"/> instance to which the new <see cref="GimObject"/> will be added. Represents a collection of 3D data elements.</param>
        /// <param name="mapObjectName">The name to be assigned to the new <see cref="GimObject"/>. This name typically represents the identity or role of the map object within the 3D structure.</param>
        /// <remarks>
        /// This method is a utility function for adding new map objects to a <see cref="GimData"/> instance. It simplifies the process of creating and naming <see cref="GimObject"/>s, which are essential components of the 3D data model represented by <see cref="GimData"/>.
        /// </remarks>
        private static void PrepareMapObject(GimData gd, string mapObjectName) 
        {
            GimObject gimObject = GimObjectFactory.Create(0, mapObjectName); // the next element is the definition of this object
            gd.Objects.Add(gimObject);
        }

    }
}
