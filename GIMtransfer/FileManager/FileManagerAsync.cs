using GIMcompress;
using System.Collections.Generic;
using System.Threading.Tasks;
using GimUtilities;
using System;

namespace GIMtransfer
{
	/// <summary>
	/// The static FileAccess class manages interaction with the functionality of GIM and IO interaction.
	/// This is reading and writing files from and to GimDatas.
	/// </summary>
	public static partial class FileAccess
	{
		// async locks for non-multithreadable code
		public static readonly object OcctLock = new();
		public static readonly object MeshingLock = new();

		/// <summary>
		/// Reads asynchronously a file from a specific filetype to a GimData object.
		/// </summary>
		/// <param name="aSource"> The path location to read from. </param>
		/// <param name="fileType"> The filetype of the file.</param>
		/// <param name="isIFCPreview"> When true for IFC, forces the IfcConvert.exe preview/fallback path instead of the OCCT BRep reader.</param>
		/// <param name="progressStatus"> An optional GimProgressStatus that can be passed to the function to update to current progress of the task.</param>
		/// <returns> A task that is creating the file as GimData object.</returns>
		private static async Task<GimData> ReadToGimDataAsync(string aSource, string fileType, bool isIFCPreview = false, GimProgressStatus progressStatus = null)
		{
			// Use Task.Factory.StartNew instead of Task.Run to start a task
			Task<GimData> task = Task.Run(() => ReadToGimData(aSource, fileType, isIFCPreview, progressStatus));
			return await task;
		}

		/// <summary>
		/// Writes a file from a GimData object to a specific filetype.
		/// </summary>
		/// <param name="gimData"> The gimData that should be written. </param>
		/// <param name="fileType"> The filetype to be written. </param>
		/// <param name="fileName"> The name the file should be given. </param>
		/// <param name="compress"> Whether the file should use advanced compression. </param>
		/// <param name="progressStatus"> An optional GimProgressStatus that can be passed to the function to update to current progress of the task.</param>
		/// <returns></returns>
		internal static async Task<string> WriteFromGimDataAsync(GimData gimData, string fileType, string fileName,  bool compress = true, GimProgressStatus progressStatus = null)
		{
			return await Task.Run(() => WriteFromGimData(gimData, fileType, fileName, compress, progressStatus));
		}

		///// <summary>
		///// Writes a file from a GimData object to a specific filetype.
		///// </summary>
		///// <param name="gimData"> The gimData that should be written. </param>
		///// <param name="fileType"> The filetype to be written. </param>
		///// <param name="fileName"> The name the file should be given. </param>
		///// <param name="compress"> Whether the file should use advanced compression. </param>
		///// <param name="progressStatus"> An optional GimProgressStatus that can be passed to the function to update to current progress of the task.</param>
		///// <returns></returns>
		//private static async Task<string> WriteFromGimDataAsync(GimData gimData, string fileType, string fileName, bool compress = true, GimProgressStatus progressStatus = null) // Helps with better overloading
		//{
		//	return await WriteFromGimDataAsync(gimData, fileType, fileName, compress, progressStatus);
		//}

		/// <summary>
		/// Converts the file from a certain filetype directly trough cadex to the an other filetype.
		/// </summary>
		/// <param name="aSource"> The file path to read from.</param>
		/// <param name="fileTypeIn"> The filetype of the source file. </param>
		/// <param name="output"> The file path to write to. </param>
		/// <param name="fileTypeOut"> The file type of the output file. </param>
		/// <param name="progressStatus"> An optional GimProgressStatus that can be passed to the function to update to current progress of the task.</param>
		/// <returns> The location where the file is written to if the operation succeeded. or the error message if the action failed. </returns>
		private static async Task<string> ConvertFileAsync(string aSource, string fileTypeIn, string output, string fileTypeOut, GimProgressStatus progressStatus = null)
		{
			Task<string> task = Task.Run(() => ("nonexistant function")
			//ConvertFile(aSource, fileTypeIn, output, fileTypeOut, progressStatus)
			);
			task.Start();
			return await task;
		}

		/// <summary>
		/// Convert a GimData to a JSON file(s). It convertes each object to an individual JSON. 
		/// </summary>
		/// <param name="gimData"> The GimData to convert.</param>
		/// <param name="fileName"> The main name of the JSON files that are generated</param>
		/// <param name="elemIndex"> The element index to get the JSON for.</param>
		private static async Task<JsonGimObject> GimDataToJsonAsync(
			GimData gimData,
			string fileName,
			int visualisationUnitToBe = -1,
			GimProgressStatus progressStatus = null,
			IReadOnlyDictionary<int, string> reuseGlbsByChildId = null)
		{
			Task<JsonGimObject> task = Task.Run(() => GimDataToJson(gimData, fileName, visualisationUnitToBe, progressStatus, reuseGlbsByChildId));
			return await task;
		}

	}

}