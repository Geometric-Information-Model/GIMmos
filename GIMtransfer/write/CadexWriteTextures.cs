using cadex;
using GIMcompress;
using System;
using System.IO;
using GimUtilities;

namespace GIMtransfer
{
	static partial class CadexWriteElements
	{
//		private static ModelData_FileTexture GetTexture(string fileDestination, IReadOnlyGimElement gimElement, bool doNotUseOriginalFile = false)
//		{
//			return GetTexture(fileDestination, new GimTexture(gimElement), doNotUseOriginalFile);
//		}

//		/// <summary>
//		/// Gets a ModelData_texture for the gimTexture.
//		/// It also writes the texture file the the file destination if it doesn't exists already on the computer.
//		/// </summary>
//		/// <param name="fileDestination">The folder where the texture file should be written to, is only used if the file does not
//		/// exist on this pc.</param>
//		/// <param name="gimTexture">The GimTexture (GimElement) to create a file for and a ModelData_Texture.</param>
//		/// <param name="doNotUseOriginalFile"> If set to true we will not use the original file texture but generate new ones.</param>
//		/// <returns></returns>
//		private static ModelData_FileTexture GetTexture(string fileDestination, GimTexture gimTexture, bool doNotUseOriginalFile = false)
//		{
//			//ModelData_Texture texture = new ModelData_FileTexture();

//			//ModelData_PixMap texture2 = new ModelData_PixMap();
//			Base_UTF16String theFilePath;

//			// If the file already exists and is the same, we just use that file
//			if (!doNotUseOriginalFile && File.Exists(gimTexture.FilePath) && FileAccess.CompareFile(File.OpenRead(gimTexture.FilePath), gimTexture.TextureFile))
//			{
//#if DEBUG
//				GimLogger.Info("We found the file and it is the same! (:");
//#endif
//				theFilePath = new Base_UTF16String(gimTexture.FilePath);
//			}
//			// Otherwise we try to create a new file in the fileDestination folder.
//			else
//			{
//				string fileName = Path.GetFileName(gimTexture.FilePath);
//				// Create file and set storage location.
//				// New location to write the file to.
//				string newFilePath = fileDestination + fileName;

//				// Does the file already exists?
//				if (File.Exists(newFilePath))
//				{
//					if (FileAccess.CompareFile(File.OpenRead(newFilePath), gimTexture.TextureFile))
//					{
//						// The file does exist and is the same so we used use that.
//						GimLogger.Info("The file " + fileName + " already exists.");
//					}
//					else
//					{
//						// The file does exist but it is not the same.
//						// I do not want to fuck around in someone elses file system..
//						// So we just pretend that that is the correct file.
//						GimLogger.Warn("The file " + fileName + " already exists. We are not overwriting the file.\n But it is different.");
//					}
//				}
//				else
//				{
//					// The file that we want to create does not already exist.
//					// So we write it to the location!
//					GimLogger.Info("Writing texture file to location: " + newFilePath);
//					FileStream newTextureFile = File.Create(newFilePath, gimTexture.TextureFile.Length);
//					newTextureFile.Write(gimTexture.TextureFile);
//					newTextureFile.Close();
//					newTextureFile.Dispose();
//				}

//				// The file path we tell to Cadex is the filePath of the file we just created (or reused).
//				theFilePath = new Base_UTF16String(newFilePath);
//			}

//			ModelData_TextureType theTextureType = GetTextureType(gimTexture.TextureType);
//			ModelData_TextureParameters theTextureParameters = GetTextureParameters(gimTexture);

//			ModelData_FileTexture fileTexture = new ModelData_FileTexture(theFilePath, theTextureType, theTextureParameters);
//			return fileTexture;
//		}

//		private static ModelData_TextureParameters GetTextureParameters(GimTexture gimTexture)
//		{
//			ModelData_TextureParameters theTextureParameters = new ModelData_TextureParameters();
//			theTextureParameters.SetRotation((float)gimTexture.Rotation);
//			theTextureParameters.SetScale(gimTexture.ScaleU, gimTexture.ScaleV);
//			theTextureParameters.SetTranslation(gimTexture.TranslationU, gimTexture.TranslationV);
			
//			return theTextureParameters;
//		}

//		private static ModelData_TextureType GetTextureType(GimTextureType textureType)
//		{
//			return textureType switch
//			{
//				GimTextureType.Unkown => ModelData_TextureType.ModelData_TT_Unknown,
//				GimTextureType.DiffuseMap => ModelData_TextureType.ModelData_TT_DiffuseMap,
//				GimTextureType.SpecularMap => ModelData_TextureType.ModelData_TT_SpecularMap,
//				GimTextureType.EmissiveMap => ModelData_TextureType.ModelData_TT_EmissiveMap,
//				GimTextureType.BumpMap => ModelData_TextureType.ModelData_TT_BumpMap,
//				GimTextureType.NormalMap => ModelData_TextureType.ModelData_TT_NormalMap,
//				GimTextureType.DisplacementMap => ModelData_TextureType.ModelData_TT_DisplacementMap,
//				GimTextureType.SphereEnviromentMap => ModelData_TextureType.ModelData_TT_SphereEnvironmentMap,
//				_ => throw new NotImplementedException(textureType.ToString()),
//			};
//		}
	}
}
