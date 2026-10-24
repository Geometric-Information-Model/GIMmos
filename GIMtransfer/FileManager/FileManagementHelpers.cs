using System.IO;

namespace GIMtransfer
{
	public static partial class FileAccess
	{
		/// <summary>
		/// Gets the path to the fileName in the GIM folder in the Temp folder.
		/// It also creates the GIM folder if it doesn't exist in the Temp folder.
		/// </summary>
		/// <param name="fileName">The fileName to get the Path to.</param>
		/// <returns>Returns the full path to the file in the temp folder.</returns>
		public static string GetTempPath(string fileName)
		{
			string path = GetTempFolder() + fileName;
			return path;
		}
		public static string GetTempPath(string fileName, string fileType)
		{
			string path = null;
			if (fileType.Equals("gltf"))
			{
				path = GetGltfTempFolder() + fileName;
			}
			else if (fileType.Equals("obj"))
			{
				path = GetObjTempFolder() + fileName;
			}
			else 
			{
				path = GetTempFolder() + fileName;
			}
			return path;
		}

		/// <summary>
		/// Gets the path to the GIM folder in the Temp folder.
		/// It also creates the GIM folder if it doesn't exist in the Temp folder.
		/// </summary>
		/// <returns>Returns the full path to the GIM folder in the temp folder.</returns>
		public static string GetTempFolder()
		{
			string path = Path.GetTempPath() + "GIM\\";
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			return path;
		}
		public static string GetGltfTempFolder()
		{
			string path = Path.GetTempPath() + "GIM\\GLTF\\";
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			return path;
		}
		public static string GetObjTempFolder()
		{
			string path = Path.GetTempPath() + "GIM\\OBJ\\";
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			return path;
		}


		//public static string GetGltfObjFolder() 
		//{
		//	Directory.CreateDirectory();
		//}

		const int amountOfBytesToReadPerTime = 1024;
		public static bool CompareFile(FileStream otherFileStream, byte[] file)
		{
			// If we have more bytes in the file they can never be the same file.
			if (otherFileStream.Length != file.Length)
				return false;

			// We now know otherFile and file have the same length
			byte[] otherFile = new byte[file.Length];

			for (int i = 0; i < file.Length; i += amountOfBytesToReadPerTime)
			{
				// Clamp the amount of bytes to read to the file length.
				int amountOfBytesToRead = amountOfBytesToReadPerTime;
				if (i + amountOfBytesToRead > file.Length)
					amountOfBytesToRead = file.Length - i;

				// Read amountOfBytesToRead amount of bytes and compare them.
				otherFileStream.Read(otherFile, i, amountOfBytesToRead);
				for (int j = i; j < i + amountOfBytesToRead; j++)
					if (otherFile[j] != file[j])
						return false;
			}

			// We never found a different byte so they must be the same.
			return true;
		}
	}
}
