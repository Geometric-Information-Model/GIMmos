// using ACadSharp;
// using ACadSharp.IO;
using ACadSharp;
using ACadSharp.IO;
using GimBin;
using GIMcompress;
using GIMtransfer.read;
using GimUtilities;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static GIMtransfer.Cadex;

[assembly: InternalsVisibleTo("TestPlatform")]
[assembly: InternalsVisibleTo("GimHeightMapData")]

namespace GIMtransfer
{
	public static partial class FileAccess
	{
        private const string IFC_CONVERT_PROGRESS = "IfcConvert";
        private const int IFC_CONVERT_PROGRESS_UNITS = 70;
        private const int ASSIMP_IMPORT_PROGRESS_UNITS = 20;
        private const int ASSIMP_NODE_PROGRESS_UNITS = 10;
        private const string GML_READ_PROGRESS = "GmlRead";

        /// <summary>
        /// Reads data from a specified source and converts it into GimData format based on the file type. 
        /// Supports reading from different file types including JSON and binary.
        /// </summary>
        /// <remarks>
        /// The method performs the following steps:
        /// 1. Handles progress status updates if provided.
        /// 2. Reads and processes JSON files, converting them into GimData.
        /// 3. Reads and processes binary files into GimData.
        /// 4. For other file types, uses CadexModel for reading and then converts the model into GimData.
        /// 5. In release mode, it includes additional error handling.
        /// </remarks>
        /// <param name="aSource">The path or source from which to read the data.</param>
        /// <param name="fileType">The type of the file (e.g., 'json', 'bin').</param>
        /// <param name="isIFCPreview">When true for IFC, forces the IfcConvert.exe preview/fallback path instead of the OCCT BRep reader.</param>
        /// <param name="progressStatus">Optional parameter for tracking the progress of the reading process.</param>
        /// <returns>The generated GimData after processing the input data. Returns null in case of failure in release mode.</returns>
        internal static GimData ReadToGimData(string aSource, string fileType, bool isIFCPreview = false, GimProgressStatus progressStatus = null)

        {
            //
            // Reading a GIM Json file
            //

            if (fileType == "json")
            {
                progressStatus?.Create(new Dictionary<string, int>() { { "todo", 1 } });
                string text = File.ReadAllText(aSource); //4

                {
                    JsonGimObject jsonObj = JsonManager.ReadGimJson(text);
                    GimData updatedGimData = GimJsonToGimData(jsonObj);

                    progressStatus?.EntityCompletedCallback("todo");

                    return updatedGimData;
                }
            }

            //
            // Reading a GIM Bin file
            //

            if (fileType == "bin")
            {
                return BinaryToGimData(aSource, progressStatus);
            }
            // Setting environment parameters for reading/writing (legacy cadex)
            Start();
            
            //
            // Reading a GML file
            //

            if (ReadGMLToGimData.SupportsType(fileType))
            {
                progressStatus ??= new();
                progressStatus.Create(new Dictionary<string, int>() { { GML_READ_PROGRESS, 1 } });
                GimData gmlGimData = ReadGMLToGimData.GMLToGimData(aSource);
                progressStatus.EntityCompletedCallback(GML_READ_PROGRESS);
                return gmlGimData;
            }

            //
            // Reading a DWG/DXF
            //
            if (DWGReadElement.SupportsType(fileType))
            {
                progressStatus ??= new();
                DWGReadElement dwgReader = new(progressStatus);
                CadDocument doc = new CadDocument();
                if (fileType.ToLower() == "dwg")
                {
                    doc = DwgReader.Read(aSource);
                }
                else if (fileType.ToLower() == "dxf")
                {
                    doc = DxfReader.Read(aSource);
                }
                return dwgReader.ReadDWGFile(doc);
            }

            //
            // Reading a file supported by OCCT (currently IGES, STEP, IFC)
            //
            // TODO Phase 2: ReadToGimDataFromIFCMeshOnly when forceBrep advanced option returns

            if (GIMocctReadElements.SupportsType(fileType))
            {
                try
                {

                    if (fileType == "ifc" && isIFCPreview)
                        return ReadIfcViaIfcConvertPreview(aSource, progressStatus);
                    progressStatus ??= new();
                    GIMocctReadElements occtReader = new(progressStatus);
                    occtReader.ForceBrep = true;
                    return occtReader.ReadToGimData(aSource, fileType);
                }
                catch (Exception)
                {
                    if(fileType == "ifc")
                        return ReadIfcViaIfcConvertPreview(aSource, progressStatus);
                    throw new InvalidOperationException($"Failed to read file {aSource} with OCCT reader.");

                }
            }

            //
            // Reading filetypes supported by ASSIMP (mesh files and IFC)
            //


            //
            // Seperated IFC from ASSIMP, now uses IFCConvert from IFCOpenShell
            //
 

            else
            {
                AssimpReadElements assimpReader = new(progressStatus);

                GimData gimData = assimpReader.ReadAssimpToGimData(aSource);
                return gimData;
            }

            //
            // Reading an IFC file through GeometryGym
            //

            // legacy cadex code

            // ModelData_Model aModel = CadexModel.Read(aSource, fileType); // Read the file based on the fileType

            //if (progressStatus != null)
            //{
            //    progressStatus.EntityCompletedCallback("todo");
            //} 

            //return ReadToGimData(aModel, generatePolygonsOnReading, true, fileType);
        }

        // TODO: Move this IfcConvert-preview block (~ReadIfcViaIfcConvertPreview through
        // IfcConvertProgressReporter, ~268 lines) into its own partial FileAccess file
        // (same pattern as FileManagerAsync.cs / FileManagerPrivate.cs) once there is room
        // for a dedicated reviewable change. Kept here for now to avoid a larger split.
        private static GimData ReadIfcViaIfcConvertPreview(string aSource, GimProgressStatus progressStatus)
        {
            string fileName = Path.GetFileNameWithoutExtension(aSource);
            string tempGimPath = Path.Combine(Path.GetTempPath(), "GIM");
            Directory.CreateDirectory(tempGimPath);
            string outputPath = Path.Combine(tempGimPath, fileName + ".dae");
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            string exeName = "IfcConvert.exe";
            string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string exePath = programFilesPath + @"\Programs\GIM\GIMIfc";
            progressStatus ??= new();
            CreateIfcFallbackProgress(progressStatus);

            GimData gimData;
            try
            {
                RunIfcConvertToDae(
                    exePath,
                    exeName,
                    $"\"{aSource}\" \"{outputPath}\"  --use-element-names --no-normals --element-hierarchy --center-model-geometry --surface-colour -j 7 --stderr-progress",
                    progressStatus);

                AssimpReadElements assimpReader = new(progressStatus);
                gimData = assimpReader.ReadAssimpToGimData(outputPath, true);
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }

            Matrix folderMatrix = new Matrix(gimData.Elements[1].Locals[0]);
            Matrix rotationMatrix = Matrix.FromRPY(Math.PI / 2, 0, 0);
            Matrix newFolderMatrix = rotationMatrix * folderMatrix;

            double factor = (new Vect(newFolderMatrix[0]).Norm) * 1000;
            newFolderMatrix.ApplyToLocal(gimData.Elements[1].Locals[0]);
            gimData.Elements[1].Locals[0].Px = 0;
            gimData.Elements[1].Locals[0].Py = 0;
            gimData.Elements[1].Locals[0].Pz = 0;

            if (!ValueRounder.CompareDouble(factor, 1))
                foreach (GimElement element in gimData.Elements)
                {
                    element.Locals.ForEach((local) =>
                    {
                        local.Px *= factor;
                        local.Py *= factor;
                        local.Pz *= factor;
                    });
                    if (element.ElementType != GimElementType.P_polygon) continue;
                    for (int i = 0; i < element.ElementDoubles.Length; i++)
                    {
                        element.elementDoubles[i] *= factor;
                    }
                }

            return gimData;
        }

        private static void CreateIfcFallbackProgress(GimProgressStatus progressStatus)
        {
            if (progressStatus == null) return;

            if (progressStatus.IsStarted)
                progressStatus.Reset();

            progressStatus.Create(new Dictionary<string, int>
            {
                { IFC_CONVERT_PROGRESS, IFC_CONVERT_PROGRESS_UNITS },
                { AssimpReadElements.ASSIMP_DISK_READ, ASSIMP_IMPORT_PROGRESS_UNITS },
                { AssimpReadElements.ASSIMP_NODE_READ, ASSIMP_NODE_PROGRESS_UNITS },
            });
        }

        private static void RunIfcConvertToDae(string exePath, string exeName, string arguments, GimProgressStatus progressStatus)
        {
            using Process process = new();
            process.StartInfo.FileName = Path.Combine(exePath, exeName);
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.WorkingDirectory = exePath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            IfcConvertProgressReporter progressReporter = new(progressStatus);
            using CancellationTokenSource syntheticProgressTokenSource = new();

            process.Start();

            Task stderrTask = Task.Run(() => ReadIfcConvertProgressStream(process.StandardError, progressReporter));
            Task stdoutTask = Task.Run(() => DrainProcessStream(process.StandardOutput));
            _ = Task.Run(() => ReportSyntheticIfcConvertProgress(progressReporter, syntheticProgressTokenSource.Token));

            process.WaitForExit();
            syntheticProgressTokenSource.Cancel();

            try
            {
                Task.WaitAll(new[] { stderrTask, stdoutTask }, TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Process output is best-effort progress reporting; conversion result handling stays unchanged.
            }

            progressReporter.ForceComplete();
        }

        private static void ReadIfcConvertProgressStream(StreamReader reader, IfcConvertProgressReporter progressReporter)
        {
            StringBuilder buffer = new();
            int value;
            while ((value = reader.Read()) >= 0)
            {
                char current = (char)value;
                if (current == '\r' || current == '\n')
                {
                    ReportIfcConvertProgressFromBuffer(buffer, progressReporter);
                    buffer.Clear();
                    continue;
                }

                buffer.Append(current);
                ReportIfcConvertProgressFromBuffer(buffer, progressReporter);

                if (buffer.Length > 512)
                    buffer.Remove(0, buffer.Length - 256);
            }

            ReportIfcConvertProgressFromBuffer(buffer, progressReporter);
        }

        private static void ReportIfcConvertProgressFromBuffer(StringBuilder buffer, IfcConvertProgressReporter progressReporter)
        {
            if (TryParseIfcConvertProgressBar(buffer.ToString(), out double percent))
                progressReporter.ReportRealProgress(percent);
        }

        private static bool TryParseIfcConvertProgressBar(string text, out double percent)
        {
            percent = 0;
            int start = text.LastIndexOf('[');
            if (start < 0) return false;

            int end = text.IndexOf(']', start + 1);
            if (end <= start) return false;

            string bar = text[(start + 1)..end];
            if (bar.Length < 10) return false;

            int completed = 0;
            int total = 0;
            foreach (char character in bar)
            {
                if (character == '#' || character == '=')
                {
                    completed++;
                    total++;
                }
                else if (character == ' ' || character == '-')
                {
                    total++;
                }
                else
                {
                    return false;
                }
            }

            if (total == 0) return false;

            percent = completed * 100.0 / total;
            return true;
        }

        private static void DrainProcessStream(StreamReader reader)
        {
            reader.ReadToEnd();
        }

        private static async Task ReportSyntheticIfcConvertProgress(
            IfcConvertProgressReporter progressReporter,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                double syntheticPercent = 0;
                while (!cancellationToken.IsCancellationRequested && !progressReporter.HasRealProgress)
                {
                    syntheticPercent = Math.Min(syntheticPercent + 2, 95);
                    progressReporter.ReportSyntheticProgress(syntheticPercent);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private sealed class IfcConvertProgressReporter
        {
            private readonly GimProgressStatus progressStatus;
            private readonly object progressLock = new();
            private int reportedUnits;
            private bool hasRealProgress;

            public IfcConvertProgressReporter(GimProgressStatus progressStatus)
            {
                this.progressStatus = progressStatus;
            }

            public bool HasRealProgress
            {
                get
                {
                    lock (progressLock)
                        return hasRealProgress;
                }
            }

            public void ReportRealProgress(double percent)
            {
                lock (progressLock)
                {
                    hasRealProgress = true;
                    ReportProgressLocked(percent);
                }
            }

            public void ReportSyntheticProgress(double percent)
            {
                lock (progressLock)
                {
                    if (hasRealProgress) return;
                    ReportProgressLocked(percent);
                }
            }

            public void ForceComplete()
            {
                lock (progressLock)
                {
                    if (reportedUnits >= IFC_CONVERT_PROGRESS_UNITS) return;

                    progressStatus?.EntityCompletedCallback(
                        IFC_CONVERT_PROGRESS,
                        IFC_CONVERT_PROGRESS_UNITS - reportedUnits);
                    reportedUnits = IFC_CONVERT_PROGRESS_UNITS;
                }
            }

            private void ReportProgressLocked(double percent)
            {
                int currentUnits = (int)Math.Round(
                    Math.Clamp(percent, 0, 100) / 100 * IFC_CONVERT_PROGRESS_UNITS);
                int delta = currentUnits - reportedUnits;
                if (delta <= 0) return;

                progressStatus?.EntityCompletedCallback(IFC_CONVERT_PROGRESS, delta);
                reportedUnits = currentUnits;
            }
        }

        /// <summary>
        /// Writes a file from a GimData object to a specific filetype.
        /// </summary>
        /// <param name="gimData"> The gimData that should be written. </param>
        /// <param name="fileType"> The filetype to be written. </param>
        /// <param name="fileName"> The name the file should be given. </param>
        /// <param name="jsonObj"> In case of json conversion the object for which the json should be generated. </param>
        /// <param name="compress"> Whether the file should use advanced compression. </param>
        /// <returns></returns>
#if DEBUG
        internal static string WriteFromGimData(GimData gimData, string fileType, string fileName, int jsonObj, bool compress = true, GimProgressStatus progressStatus = null) // Helps with better overloading
#else
        private static string WriteFromGimData(GimData gimData, string fileType, string fileName, int jsonObj, bool compress = true, GimProgressStatus progressStatus = null) // Helps with better overloading
#endif
        {
            return WriteFromGimData(gimData, fileType, fileName, compress, jsonObj, progressStatus);
        }
        /// <summary>
        /// Reads a model file of a specified type and converts it to another specified file type.
        /// </summary>
        /// <remarks>
        /// The method carries out the following tasks:
        /// - Optionally updates a progress status if provided.
        /// - Initializes and reads the model using Cadex, based on the input file type.
        /// - Attempts to write the read model to a new format as specified by the output file type.
        /// - Reports progress upon completion.
        /// - In the case of failure, logs and throws an error with details.
        /// - The behavior in case of an exception differs based on the build configuration:
        ///   - In DEBUG mode, rethrows the exception for further analysis.
        ///   - In RELEASE mode, suppresses the exception and returns null.
        /// </remarks>
        /// <param name="aSource">The source path of the input model file.</param>
        /// <param name="fileTypeIn">The type of the input file.</param>
        /// <param name="fileTypeOut">The type of the output file.</param>
        /// <param name="output">The path where the output file will be saved.</param>
        /// <param name="progressStatus">Optional parameter for tracking the progress of the operation.</param>
        /// <returns>The path to the successfully written output file or null in case of failure in RELEASE mode.</returns>
        //        internal static string ConvertFile(string aSource, string fileTypeIn, string output, string fileTypeOut, GimProgressStatus progressStatus = null)
        //        {
        //            try
        //            {
        //                // TODO: Implement progressStatus!
        //                if (progressStatus != null)
        //                {
        //                    progressStatus.Create(new Dictionary<string, int>() { { "todo", 1 } });
        //                }

        //                // Start Cadex and read the file to the cadex model
        //                Start();
        //                ModelData_Model aModel = CadexModel.Read(aSource, fileTypeIn); // Read the file based on the fileType

        //                if (progressStatus != null)
        //                {
        //                    progressStatus.EntityCompletedCallback("todo");
        //                }

        //                string err = "";
        //                string path = "";

        //                bool success = CadexModel.Write(aModel, fileTypeOut, output, ref err, ref path);
        //                if (!success)
        //                {
        //                    throw GimLogger.Error(err);
        //                }
        //                return path;
        //            }
        //            catch
        //            {
        //#if DEBUG
        //                throw;
        //#elif RELEASE
        //                return null;
        //#endif
        //            }
        //        }


        /// <summary>
        /// Creates a GLTF (GL Transmission Format) model from GimData.
        /// </summary>
        /// <remarks>
        /// This method generates a GLTF model by calling GLTFWriteElements.CreateGLTF, passing the entire GimData or a subset based on the elementId. The method appears to have provisions for handling subsets of GimData, but this functionality is currently commented out, indicating potential future development or modification.
        /// The scale parameter allows for scaling the generated GLTF model, but its actual implementation and effect depend on GLTFWriteElements.CreateGLTF.
        /// </remarks>
        /// <param name="gimData">The GimData to be converted into a GLTF model.</param>
        /// <param name="elementId">An identifier for a specific element within the GimData, potentially representing a subset for conversion. Current implementation may not use this for subsetting.</param>
        /// <param name="scale">An optional scale parameter for the conversion, defaulted to 1. Its application is dependent on the GLTFWriteElements.CreateGLTF implementation.</param>
        /// <returns>A ModelRoot object representing the generated GLTF model.</returns>
        internal static ModelRoot GetGLTF(GimData gimData, uint elementId, double scale = 1, bool useFirstLocal = false)

        {

            //GimData subGimData = GetGimElementAsGimData(gimData, elementId);
            // TODO: This now only works if the gimObject already contains a brepresentation.
            //GimData objectAsPoly = GimDataToPoly(subGimData);
            //string objName = null;
            return GLTFWriteElements.CreateGLTF(gimData, elementId, scale, useFirstLocal);
		}


        /// <summary>
        /// Reads binary data from a file and converts it into GimData format.
        /// </summary>
        /// <remarks>
        /// The method performs the following steps:
        /// 1. Opens a file stream for the given source.
        /// 2. Uses a binary reader to read the file's contents.
        /// 3. Utilizes a GimReader to convert the binary data into GimData.
        /// 4. Closes the file stream upon completion.
        /// 5. In debug mode, rethrows exceptions for further investigation, whereas in release mode, it suppresses exceptions and returns null in case of failure.
        /// </remarks>
        /// <param name="aSource">The file path from which to read the binary data.</param>
        /// <param name="progressStatus">Optional parameter for tracking the progress of the reading process.</param>
        /// <returns>The generated GimData after processing the binary data. In case of an exception, rethrows in debug mode and returns null in release mode.</returns>
        internal static GimData BinaryToGimData(string aSource, GimProgressStatus progressStatus)

        {
            try
            {
                using FileStream stream = File.Open(aSource, FileMode.Open);
                BinaryReader binReader = new BinaryReader(stream);
                GimReader reader = new GimReader(binReader);

                GimData gd = reader.Read(progressStatus);
                return gd;
            }
            catch
            {
#if DEBUG
                throw;
#elif RELEASE
                return null;
#endif
            }
        }

        /// <summary>
        /// Loads the list of readable/supported files in a given directory. 
        /// </summary>
        /// <param name="fileDirectory"> The directory to check the files in.</param>
        /// <param name="acceptedFormats"> The file formats that we will accept. </param>
        internal static List<string> GetReadableFilesInDirectory(string fileDirectory, string[] acceptedFormats)
        {
            List<string> files = new();

            if (Directory.Exists(fileDirectory))
            {
                string[] filesInDirectory = Directory.GetFiles(fileDirectory);
                foreach (string fileName in filesInDirectory)
                {
                    string fileType = Path.GetExtension(fileName);
                    if (acceptedFormats.Contains(fileType[1..].ToLower()))
                    {
                        files.Add(fileName);
                    }
                }
            }
            return files;
        }

//#if DEBUG
//        internal static GimData GimDataToPoly(GimData gimData)
//#else
//		private static GimData GimDataToPoly(GimData gimData)
//#endif
//		{
//			//bool containsBrep = false;
//			//foreach (GimElement element in gimData.elements)
//			//	if (element.elementType == GimElementType.R_BRep)
//			//		containsBrep = true;

//			//GimData returnGimData;
//			//if (containsBrep)
//			//{
//			ModelData_Model theModel = ReadFromGimData(gimData, GetTempFolder());
//			theModel = CadexModel.CadexModelToPoly(theModel);
//			/*returnGimData =*/
//			return ReadToGimData(theModel, false);
//			//	}
//			//	else
//			//	{
//			//		returnGimData = gimData;
//			//	}
//			//	return returnGimData;
//		}

        // Call this before calling cadex functions.
        internal static void Init()
        {
            Start();
        }
#if DEBUG
        internal static void Start()
#else
        private static void Start()
#endif
        {
            Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";" +
			Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + ("\\bin"));
			//string aKey = LicenseKey.Value(); // Activate the license(aKey must be defined in cadex_license.cs)
			//if (!LicenseManager.Activate(aKey)) { throw GimLogger.Error("Failed to activate CAD Exchanger license."); }
			
			//Base_Settings.Default().SetValue(Base_Settings.Id.UseExceptions, true);
		}



        static internal GimElement CombineMultiplePolygonsIntoOne(List<uint> polygonIndexes, GimData gimData)
        {
            List<GimElement> polygonGroup = new();
            foreach (int polygonIndex in polygonIndexes)
            {
                GimElement polygon = gimData.Elements[polygonIndex];
                polygonGroup.Add(polygon);
            }
            return CombineMultiplePolygonsIntoOne(polygonGroup);
        }

        /// <summary>
        /// Combines multiple P_polygon elements into one, Warning: assumes default local for all polygons
        /// </summary>
        /// <param name="polygonGroup"></param>
        /// <returns></returns>
        static internal GimElement CombineMultiplePolygonsIntoOne(List<GimElement> polygonGroup)
        {
            GimElement resultingElement = new() { ElementType = GimElementType.P_polygon };

            // the final int/double count
            int intCount = 0;
            int doubleCount = 0;

            foreach (GimElement polygon in polygonGroup)
            {
                intCount += polygon.ElementInts.Length;
                doubleCount += polygon.ElementDoubles.Length;
            }

            int[] ints = new int[intCount];
            double[] doubles = new double[doubleCount];


            int cumulativeIntIndex = 0;
            int cumulativeDoubleIndex = 0;

            foreach (GimElement polygon in polygonGroup)
            {
                polygon.Elements = new();
                for (int j = 0; j < polygon.ElementDoubles.Length; j++)
                {
                    doubles[cumulativeDoubleIndex + j] = polygon.ElementDoubles[j];
                }

                for (int j = 0; j < polygon.ElementInts.Length; j++)
                {
                    ints[cumulativeIntIndex + j] = polygon.ElementInts[j] + cumulativeDoubleIndex / 3;
                }

                cumulativeIntIndex += polygon.ElementInts.Length;
                cumulativeDoubleIndex += polygon.ElementDoubles.Length;
            }

            resultingElement.elementInts = ints;
            resultingElement.ElementDoubles = doubles;

            return resultingElement;
        }

        static internal void CombinePolygonsWithSameMaterial(GimData gimData)
        {
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement materialElement = gimData.Elements[i];
                List<uint> polygonIndexes = new();
                if (materialElement.ElementType == GimElementType.Material)
                {
                    foreach (GimLocal local in materialElement.Locals)
                    {
                        uint parent = local.parent;
                        GimElement parentElement = gimData.Elements[(int)parent];
                        if (parentElement.ElementType == GimElementType.P_polygon)
                        {
                            polygonIndexes.Add(parent);
                        }
                    }

                    materialElement.Locals = new();

                    GimElement newPolygon = CombineMultiplePolygonsIntoOne(polygonIndexes, gimData);
                    newPolygon.Elements.Add((uint)i);

                    //

                    int polygonParentIndex = (int)gimData.Elements[(int)polygonIndexes[0]].Locals[0].parent;
                    GimElement polygonParent = gimData.Elements[polygonParentIndex];

                    foreach (uint polygonIndex in polygonIndexes)
                    {
                        polygonParent.Elements.Remove(polygonIndex);
                    }

                    int newPolygonIndex = gimData.Elements.Count;
                    gimData.Elements.Add(newPolygon);
                    polygonParent.Elements.Add((uint)newPolygonIndex);
                    materialElement.Locals = new() { new() { parent = (uint)newPolygonIndex } };
                    newPolygon.Locals.Add(new GimLocal() { parent = (uint)polygonParentIndex });

                    gimData.RemoveElements(polygonIndexes);

                }
            }
        }

        /// <summary>
        /// Convert a GimData to a JSON file(s). It convertes each object to an individual JSON. 
        /// </summary>
        /// <param name="gimData"> The GimData to convert.</param>
        /// <param name="fileName"> The main name of the JSON files that are generated</param>
        /// <param name="visualisationUnitToBe"></param>
        /// <param name="elemIndex"> The element index to get the JSON for.</param>
        /// <param name="progressStatus"></param>
        internal static JsonGimObject GimDataToJson(
            GimData gimData,
            string fileName,
            int visualisationUnitToBe = -1,
            GimProgressStatus progressStatus = null,
            IReadOnlyDictionary<int, string> reuseGlbsByChildId = null)
        {

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            JsonGimObject jsonGimData = GetJsonGimData(gimData, visualisationUnitToBe, progressStatus, reuseGlbsByChildId);

            if (progressStatus != null) progressStatus.EntityCompletedCallback(GimProgressStatus.WRITE_TO_DISK);

            return jsonGimData;
        }


    }
}
