using cadex;
using GimBin;
using GIMcompress;
using GIMcompress.Shrinkwrap;
using GimCrypt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GimUtilities;
using GIMocctCLI;

using JsonSerializer = Newtonsoft.Json.JsonSerializer;
using SharpGLTF.Schema2;
using GIMtransfer.read;
using GIMtransfer.write;
using ACadSharp.Objects;
using System.Text.Json.Nodes;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.InteropServices.JavaScript;
using Assimp.Unmanaged;

[assembly: InternalsVisibleTo("TestPlatform"),
            InternalsVisibleTo("TestUI"),
    InternalsVisibleTo("StartCityJson"),
    InternalsVisibleTo("GIMmapsData")
    ]
namespace GIMtransfer
{
    /// <summary>
    /// The static FileAccess class manages interaction with the functionality of GIM and IO interaction.
    /// This is reading and writing files from and to GimDatas.
    /// </summary>
    /// 

    public class GimOptions
    {
        public int binaryUnitToBe = -1;
        public int visualisationUnitToBe = -1;
        public double rotationYaw = 0;
        public double rotationPitch = 0;
        public double rotationRoll = 0;
        public double translateX = 0;
        public double translateY = 0;
        public double translateZ = 0;
        public double scale = 1; // Attention, default scale must be 1, not 0
        public bool mirror = false;
        public bool isIFCPreview = false; // true = IfcConvert preview mesh, false = full BRep (OCCT)
        public int promoteChildId = 0;
        public double minX = 0;
        public double minY = 0;
        public double maxX = 0;
        public double maxY = 0;
        public GimOptions()
        {

        }
        public GimOptions(double rotationRoll)
        {
            this.rotationRoll = rotationRoll;
        } 

        /// <summary>
        /// Checks if this has any rotation or translation set. (PS: ignores mirror flag)
        /// </summary>
        /// <returns>True if the transformation is the default transformation (no change)</returns>
        public bool IsDefaultTransformation()
        {
            return rotationRoll == 0 && rotationPitch == 0 && rotationYaw == 0
                && translateX == 0 && translateY == 0 && translateZ == 0;
        }

        internal void ApplyToElementLocal(GimElement element, GimLocal local)
        {
            // Rotation matrix
            Matrix matrix = new Matrix(local);
            Matrix transformation = Matrix.FromRPY(rotationRoll, rotationPitch, rotationYaw);
            Matrix finalMatrix = transformation * matrix;
            finalMatrix.ApplyToLocal(local);

            // Translation postion
            Vect position = new Vect(local.px, local.py, local.pz);
            Vect offset = new Vect(translateX, translateY, translateZ);
            Vect finalPosition = transformation * position + offset;

            local.Px = finalPosition[0];
            local.Py = finalPosition[1];
            local.Pz = finalPosition[2];
        }

    }

    /// <summary>
    /// Used for GIMmaps, this holds the basic data of a single GLB object (name, position and glb content)
    /// </summary>

    public class GLBStruct
    {
        public string Name;
        public byte[] Bytes;
        public double[] Rpy;
        public double[] Xyz;
    }

    public static partial class FileAccess
    {
        public static bool DisableAssimpLogging()
        {
            try
            {
                AssimpLibrary.Instance.DetachAllLogStreams();
            }
            catch
            {
                return false;
            }
            return true;
        }

        public static bool FreeAssimpMemory()
        {
            try
            {
            }
            catch
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static GimData SharpGLTFReadGLBToGimData(string path)
        {
            return GLTFReadElements.ReadGLBToGimData(path);
        }

        public static void ForceStartCadex()
        {
            Start();
        }

        /// <summary>
        /// Converts and exports GimData into various formats based on the specified file type.
        /// </summary>
        /// <remarks>
        /// The method handles different export formats:
        /// - For JSON: Serializes the GimData to JSON and writes it to a temporary file.
        /// - For Binary: Converts the GimData to a binary format.
        /// - For GLB: Updates the GimData matrix for GLB compatibility and converts it.
        /// - For other types: Uses Cadex to convert and write the GimData.
        /// In non-DEBUG mode, it wraps the conversion process in a try-catch block and logs any exceptions.
        /// The method also optionally handles compression before conversion.
        /// </remarks>
        /// <param name="gimData">The GimData to be converted and exported.</param>
        /// <param name="fileName">The name of the file to be created.</param>
        /// <param name="fileType">The type of the output file (e.g., 'json', 'bin', 'glb' and more).</param>
        /// <param name="compress">Boolean flag to indicate whether compression should be applied before conversion.</param>
        /// <param name="progressStatus">Optional parameter for tracking the progress of the operation.</param>
        /// <returns>The path to the exported file or the exported data itself, depending on the file type.</returns>
        /// <exception cref="Exception">Throws a logged exception in case of failure in non-DEBUG mode.</exception>
        internal static string WriteFromGimData(GimData gimData, string fileType, string fileName, bool compress = true, GimProgressStatus progressStatus = null)
        {
            string path;

            // TODO create sanitization function for filetype
            fileType = fileType.ToLower();
            if (fileType[0] == '.') fileType = fileType[1..];

            if (compress)
            {
                //gimData = Twins.LinkTwins(gimData);
            }
            if (fileType == "json")
            {
                string json = System.Text.Json.JsonSerializer.Serialize(
                    GimDataToJson(gimData, fileName, -1),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                );

                path = GetTempPath(fileName); // define and create the subfolder

                File.WriteAllText(path + ".json", json);

                return json;
            }
            else if (fileType == "bin")
            {
                return GimDataToBinary(gimData, fileName, progressStatus);
               
            }
            //else if (fileType == "glb") 
            //{
            //    //reverting z vertical gimdata to y vertical gimdata
            //    UpdateChildMatrix(gimData, new GimOptions(((Math.PI) / 2) * -1 ));
            //    var convertedGLB = GetGimDataAsGLB64(gimData);
            //    ModelRoot glb = ModelRoot.ParseGLB(Convert.FromBase64String(convertedGLB));
            //    glb.Save(GetTempPath(fileName) + ".glb");
            //    var path = GetTempPath(fileName) + ".glb";

            //    return path; 
            //}


            Start();
            path = GetTempPath(fileName);
            progressStatus ??= new();

            if (GIMocctWriteElements.SupportsType(fileType))
            {
                GIMocctWriteElements gimOcctWriter = new(progressStatus);
                if (string.Equals(fileType, "ifc", StringComparison.OrdinalIgnoreCase))
                {
                    if (!gimOcctWriter.WriteGimDataToIfc(path, fileType, gimData, out var messages))
                    {
                        string detail = messages is { Count: > 0 } msgs
                            ? string.Join("; ", msgs)
                            : "IFC export failed.";
                        throw new InvalidOperationException(detail);
                    }
                    return path + ".ifc";
                }
                return gimOcctWriter.WriteGimDataToOcct(path, fileType, gimData);
            }

            if (GimdataToGml.SupportsType(fileType))
            {
                GimdataToGml GMLwriter = new();
                return GMLwriter.GimDataToGML(gimData, path);
            }

            AssimpWriteElements assimpWriter = new(progressStatus);
            return assimpWriter.WriteGimDataToAssimp(path, fileType, gimData, fileName);

            // Legacy with Cadex
            //ModelData_Model model = CadexWriteElements.GetCadexModel(gimData, GIMtransfer.FileAccess.GetTempFolder(), progressStatus);

            // TODO: Should converted all breppresentations to a polyrepresentations if we want the export a polyrepresentations
            //if (fileType == "gltf") // fileType == PolyFileType
            //{
            //	CadexModel.CadexModelToPoly(model);
            //}

            //bool success = CadexModel.Write(model, fileType, fileName, ref error, ref path);
            //if (!success)
            //{
            //    throw GimLogger.Error(error);
            //}


            //if (progressStatus != null)
            //{
            //    progressStatus.EntityCompletedCallback("Elements");
            //}
            //return path;
        }
        public static async Task<string> WriteToSvgFileAsync(GimData gimData, string fileName, GimProgressStatus progressStatus = null)
        {
            SvgWriter svgWriter = new SvgWriter();
            string svgContent = svgWriter.WriteGimDataToSvg(gimData);

            // If fileName is just a name, save to temp/output folder; otherwise, use as full path
            string directory = Path.GetDirectoryName(fileName);
            string nameOnly = Path.GetFileNameWithoutExtension(fileName);
            string svgPath;

            if (string.IsNullOrEmpty(directory))
            {
                // Use a temp or output folder if no directory is provided
                string tempFolder = Path.GetTempPath();
                svgPath = Path.Combine(tempFolder, nameOnly + ".svg");
            }
            else
            {
                svgPath = Path.Combine(directory, nameOnly + ".svg");
            }

            await File.WriteAllTextAsync(svgPath, svgContent);
            return svgPath;
        }
        /// <summary>
        /// Reads a file from a specific filetype to a GimData object. Then creates a json string of the read file.
        /// </summary>
        /// <param name="aSource"> The path location of the file. </param>
        /// <param name="fileType"> The filetype of the file.</param>
        /// <param name="fileName"> the name of the file. </param>
        /// <param name="readProgressStatus"> GimProgressStatus that can be passed to the function to update to current reading progress of the task.</param>
        /// <param name="writeProgressStatus"> GimProgressStatus that can be passed to the function to update to current writing progress of the task.</param>
        /// <param name="gimOptions"> Gim options such as unittobe or currentunit</param>
        /// <returns> The written json as a string.</returns>

        public static async Task<string> FileToJsonAsync(string aSource, string fileType, string fileName, GimProgressStatus readProgressStatus,
            GimProgressStatus writeProgressStatus, GimOptions gimOptions)
        {
            int binaryUnitToBe = gimOptions.binaryUnitToBe;
            int visualisationUnitToBe = gimOptions.visualisationUnitToBe;

            GimData gimData = await ReadToGimDataAsync(aSource, fileType, gimOptions.isIFCPreview, readProgressStatus);

            readProgressStatus?.WaitForStart();

            // Modifications to overall gimData happen here, as we combined all demoted gimDatas and
            // we didn't yet export the promoted ones
            // Applying the transformations stored in gimOptions
            ApplyTransformations(gimData, gimOptions);

            if (binaryUnitToBe > 0)
            {
                GimUnitSize unitSize = new GimUnitSize((byte)binaryUnitToBe);
                double roundingStep = unitSize.GetRoundingStep();
                PolygonRounder.RoundGimData(gimData, roundingStep);
                gimData.GimBin = null;
            }

            // Use await to wait for the Task to complete
            JsonGimObject writtenJson = await GimDataToJsonAsync(gimData, fileName, visualisationUnitToBe, writeProgressStatus);
            writeProgressStatus?.WaitForStart();

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            string json = JsonConvert.SerializeObject(writtenJson, settings);
            WriteJsonToTemp(json, Path.GetFileName(fileName));
            return json;
        }

        /// <summary>
        /// Reads GIM Json string and Writes to a file from a GimData object to a specific filetype.
        /// </summary>
        /// <param name="aSource"> The Json String to read from. </param>
        /// <param name="convertToFileType"> The filetype to be written. </param>
        /// <param name="fileName"> The name of the given file (without the path, nor the extension). </param>
        /// <param name="readProgressStatus"> GimProgressStatus for reading that can be passed to the function to update to current progress of the reading task.</param>
        /// <param name="writeProgressStatus"> GimProgressStatus for writing that can be passed to the function to update to current progress of the writing task.</param>
        /// <param name="gimOptions"> Gim options such as unittobe or currentunit </param>
        /// <returns> For json returns the json content as a string and for other formats, The path of the written file </returns>
        public static async Task<string> JsonToFileAsync(string aSource, string convertToFileType, string fileName,
            GimProgressStatus readProgressStatus, GimProgressStatus writeProgressStatus, GimOptions gimOptions)
        {
            int binaryUnitToBe = gimOptions.binaryUnitToBe;
            JsonGimObject jsonObj = JsonManager.ReadGimJson(aSource);

            GimData gimData = GimJsonToGimData(jsonObj);

            // Modifications to overall gimData happen here, as we combined all demoted gimDatas and
            // we didn't yet export the promoted ones
            // Applying the transformations stored in gimOptions
            ApplyTransformations(gimData, gimOptions);

            if (binaryUnitToBe > 0)
            {
                GimUnitSize unitSize = new GimUnitSize((byte)binaryUnitToBe);
                double roundingStep = unitSize.GetRoundingStep();
                PolygonRounder.RoundGimData(gimData, roundingStep);
                gimData.GimBin = null;
            }

            return await WriteFromGimDataAsync(gimData, convertToFileType, fileName, true, writeProgressStatus);
        }
        /// <summary>
        /// Reads a GIM Json file to a GimData object and returns it as a string.
        /// </summary>
        /// <param name="aSource"> Content of the json file in string form. </param>
        /// <param name="readProgressStatus"> GimProgressStatus for reading that can be passed to the function to update to current progress of the reading task.</param>
        /// <param name="writeProgressStatus"> GimProgressStatus for writing that can be passed to the function to update to current progress of the writing task.</param>
        /// <returns> The json file as a string.</returns>

        public static string[] JsonToJson(string[] jsonContents, string fileName,
            GimProgressStatus readProgressStatus, GimProgressStatus writeProgressStatus,
            GimOptions gimOptions)
        {
            return JsonToJsonLogic(jsonContents, fileName, readProgressStatus, writeProgressStatus, gimOptions);
        }

        public static byte[] GetObjectVisualisationFromJson(string jsonContent, GimUnitSize scale)
        {
            JsonGimObject jsonObj = JsonManager.ReadGimJson(jsonContent);

            GimData gimData = GimJsonToGimData(jsonObj);
            if (scale.ToByte() > 0)
            {
                double roundingStep = scale.GetRoundingStep();
                PolygonRounder.RoundGimData(gimData, roundingStep);
            }

            ModelRoot modelRoot = GetGLTF(gimData, 0, useFirstLocal: true);
            return modelRoot.WriteGLB().Array;
        }

        /// <summary>
        /// Converts binary array to JsonGimObject.
        /// </summary>
        /// <param name="gimBin"> The variable which has gimbin. </param>
        /// <param name="fileName"> The name of the file. </param>
        /// <param name="progressStatus"> An optional GimProgressStatus that can be passed to the function to update to current progress of the task.</param>
        /// <returns></returns>
        private static async Task<GimData> GimBinToJson(byte[] gimBin, string fileName, GimProgressStatus progressStatus = null)
        {
            GimReader reader = new GimReader();

            GimData gd = reader.BytesToGimData(gimBin);

            JsonGimObject writtenJson = await GimDataToJsonAsync(gd, fileName);

            return gd;
        }
        /// <summary>
        /// Converts binary array to JsonGimObject.
        /// </summary>
        /// <param name="gimBin"> The string variable which has gimbin. </param>
        /// <param name="fileName"> The name of the file. </param>
        /// <param name="progressStatus"> An optional GimProgressStatus that can be passed to the function to update to current progress of the task.</param>
        /// <returns></returns>
        private static async Task<GimData> GimBinToJson(string gimBin, string fileName, GimProgressStatus progressStatus = null)
        {
            byte[] bin = Convert.FromBase64String(gimBin);

            GimReader reader = new GimReader();

            GimData gd = reader.BytesToGimData(bin);

            JsonGimObject writtenJson = await GimDataToJsonAsync(gd, fileName);

            return gd;
        }

        /// <summary>
        /// Converts GimData into a GLB (GL Transmission Format Binary) file format encoded as a Base64 string.
        /// </summary>
        /// <remarks>
        /// The method involves the following steps:
        /// - Converts GimData to a ModelRoot object, representing a GLTF structure.
        /// - Writes the ModelRoot object to a GLB format.
        /// - Encodes the resulting GLB byte array into a Base64 string for easy transmission or storage.
        /// - The scale parameter is available but currently not used in the conversion process.
        /// </remarks>
        /// <param name="gimData">The GimData to be converted into GLB format.</param>
        /// <param name="scale">An optional scale parameter for the conversion, defaulted to 1. Currently unused.</param>
        /// <returns>A Base64 encoded string representing the GimData in GLB format.</returns>
        public static string GetGimDataAsGLB64(GimData gimData, double scale = 1)
        {
            ModelRoot modelRoot = GetGLTF(gimData, 0); // Convert the subtree to a glb format.
                                                       // The document structure may break as j isn't always a document. This shouldn't break this function though.

            string GLB = Convert.ToBase64String(modelRoot.WriteGLB().Array);


            return GLB;
        }

        /// <summary>
        /// Converts a GimData directly into a GLB Byte Array, with possible scaling
        /// </summary>
        /// <param name="gimData">The GimData to convert</param>
        /// <param name="scale">The scaling factor</param>
        /// <returns></returns>
        public static byte[] GetGimDataAsByteArrayGLB(GimData gimData, double scale = 1)
        {
            ModelRoot modelRoot = GetGLTF(gimData, 0, scale); // Convert the subtree to a glb format. The document structure may break as j isn't always a document. This shouldn't break this function though.
                                                       //System.Text.Encoding.Default.GetString(modelRoot.WriteGLB().Array); // Use this to
                                                       //the modelRoot to a string
                                                       //System.Text.Encoding.Default.GetBytes(string) // Use this to convert string back to byte array (when writing may become a thing).

            return modelRoot.WriteGLB().Array;
        }

        public static string ConvertJSONToGLBArray(string json, string outputFileName, double scale = 1)
        {
            JsonGimObject jsonObj = JsonManager.ReadGimJson(json);
            GimData updatedGimData = GimJsonToGimData(jsonObj);
            if (jsonObj.Children.Count == 1 && updatedGimData.Objects.Count == 1)
            {
                var RPY = jsonObj.Children[0].Local.RPY;
                Matrix jsonMatrix = Matrix.FromRPY(RPY[0], RPY[1], RPY[2]);

                GimElement mainElement = updatedGimData.Elements[(int)updatedGimData.Objects[0].Element];
                if (mainElement.Locals.Count == 0)
                {
                    updatedGimData.CreateRootIfNotExists();
                    jsonMatrix.ApplyToLocal(mainElement.Locals[0]);
                }
                else
                {

                    jsonMatrix.ApplyToLocal(mainElement.Locals[0]);

                }
            }

            var bytes = GetGimDataAsByteArrayGLB(updatedGimData, scale);
            string fullPath = GetTempPath(outputFileName + ".glb");
            File.WriteAllBytes(fullPath, bytes);

            return fullPath;
        }


//        private static ModelData_Model PolyTest(string aSource, string fileTypeIn)
//        {
//            try
//            {
//                // Start Cadex and read the file to the cadex model
//                Start();
//                ModelData_Model aModel = CadexModel.Read(aSource, fileTypeIn); // Read the file based on the fileType
//                aModel = CadexModel.CadexModelToPoly(aModel);
//                return aModel;
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
        /// Compares the hash codes of two files asynchronously, to determine if any changes have occurred.
        /// </summary>
        /// <param name="originalFilePath">The file path to the original file.</param>
        /// <param name="editedFilePath">The file path to the edited file.</param>
        /// <param name="progressStatus">An optional progress status object for tracking progress.</param>
        /// <returns>
        /// A Task containing a boolean value indicating whether the hash codes of the GimData objects (if present) in the files differ, 
        /// which signifies whether the files have changed.
        /// </returns>
        /// <remarks>
        /// This asynchronous method initiates the comparison of hash codes between two files using the 
        /// <see cref="CompareGimDataHashCodeLogic"/> method. It returns a Task representing the result of the comparison. 
        /// Progress can be tracked using the optional progressStatus parameter.
        /// </remarks>
        public static async Task<bool> CompareGimDataHashCode(string originalFilePath, string editedFilePath, GimProgressStatus progressStatus = null)
        {
            try
            {
                return await CompareGimDataHashCodeLogic(originalFilePath, editedFilePath);
            }
            catch(FileNotFoundException ex) 
            {
                throw new Exception("File not found: " + ex.FileName);
            }
            catch (Exception ex)
            {
                throw new Exception("An error occured while comparing the hashcodes: " + ex);
            }

        }
        /// <summary>
        /// Asynchronously converts a list of binary GLB (Graphics Language Binary) files into their string representations.
        /// </summary>
        /// <param name="glbs">A list of byte arrays, each representing a GLB file in binary format.</param>
        /// <returns>
        /// A Task that results in a List of strings, where each string is the converted text representation of a GLB file from the input list.
        /// </returns>
        /// <remarks>
        /// This method acts as an asynchronous wrapper for the `ConvertBinaryGlbToStringLogic` function. It is designed to handle the conversion of multiple binary GLB files to their string equivalents in a non-blocking manner. By processing the files asynchronously, this method is well-suited for scenarios where processing efficiency and responsiveness are crucial, such as in UI applications or when dealing with a large number of files. The core logic of the conversion process is encapsulated in the `ConvertBinaryGlbToStringLogic` method.
        /// </remarks>
        public static async Task<List<string>> ConvertBinaryGlbToString(List<byte[]> glbs)
        {
            return await ConvertBinaryGlbToStringLogic(glbs);
        }

        public static GimData ReadGimDataFromFile(string filePath)
        {
            try
            {
                return ReadToGimData(filePath, filePath.Split(".")[^1]);
            }
            catch (Exception ex)
            {
                throw GimLogger.Error("Invalid file");
            }
        }

        /// <summary>
        /// Asynchronously initiates the download and processing of multiple GLB files with their spatial transformation data to create a combined scene.
        /// </summary>
        /// <param name="glbs">A dictionary with byte arrays representing GLB files as keys, and tuples of double arrays for XYZ coordinates and RPY (roll, pitch, yaw) values as values.</param>
        /// <param name="options">GimOptions for unit conversions</param>
        /// <returns>
        /// A Task containing a JsonGimObject that represents the combined scene created from the processed GLB files and their transformations.
        /// </returns>
        /// <remarks>
        /// This method serves as an asynchronous wrapper for the `GimMapsSceneDownloadLogic` method. It takes a collection of GLB files with their associated spatial transformation data and processes them asynchronously. The method delegates the core logic to `GimMapsSceneDownloadLogic`, which handles the parsing, conversion, and merging of the GLB files into a single JsonGimObject. This approach enables efficient handling of multiple GLB files in an asynchronous manner, suitable for scenarios where non-blocking operations are preferred or required.
        /// </remarks>
        public static string GimMapSceneDownload(List<GLBStruct> glbObjects, string fileType, string fileName, GimOptions options, GimProgressStatus progressStatus)
        {
            return GimMapsSceneDownloadLogic(glbObjects, fileType, fileName, options, progressStatus);
        }

        /// <summary>
        /// Rewrites the GLB at <paramref name="glbPath"/> with a meshopt-compressed payload
        /// when smaller than the original. Use only for consumers that support
        /// <c>EXT_meshopt_compression</c>. Failures are logged and leave the file unchanged.
        /// </summary>
        /// <param name="glbPath">Absolute path to an existing .glb file.</param>
        public static void CompressGlbFileInPlace(string glbPath)
        {
            if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
            {
                return;
            }

            byte[] originalBytes;
            try
            {
                originalBytes = File.ReadAllBytes(glbPath);
            }
            catch (Exception e)
            {
                GimLogger.Warn($"GLB compression [read-failed] ({glbPath}): {e.Message}");
                return;
            }

            byte[] compressedBytes = GetBestCompressedGlbBytes(originalBytes);

            if (!ReferenceEquals(compressedBytes, originalBytes))
            {
                try
                {
                    File.WriteAllBytes(glbPath, compressedBytes);
                }
                catch (Exception e)
                {
                    GimLogger.Warn($"GLB compression [write-failed] ({glbPath}): {e.Message}");
                }
            }
        }

    }



}
        

