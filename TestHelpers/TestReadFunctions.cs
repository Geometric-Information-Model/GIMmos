using GIMcompress;
using System;
using System.IO;

namespace TestHelpers
{
	public static class TestReadFunctions
	{
		public static GimData ReadToGimData(string path, bool generatePolygonsOnReading = false)
		{
			// Simple file read test.
			try
			{
				string filetype = Path.GetExtension(path);
				string pathFileName = Path.GetFullPath(path, Directory.GetCurrentDirectory());
				//return GIMtransfer.FileAccess.ReadToGimData(pathFileName, filetype[1..], generatePolygonsOnReading);
				return null;
			}
			catch (Exception e)
			{
				GimUtilities.GimLogger.Error(e);
				return null;
			}

		}
	}
}
