using cadex;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.IO;
using GimUtilities;

namespace GIMtransfer
{
	//internal class CadexTextureVisitor : ModelData_TextureVisitor
	//{
	//	GimTexture currentTexture;
	//	public List<GimTexture> gimTextures = new List<GimTexture>();

	//	// TODO: Are both applies called for every PixMapTexture?

	//	public override void Apply(ModelData_FileTexture theTexture)
	//	{
	//		currentTexture = new GimTexture();

	//		ReadTextureType(theTexture.Type());
	//		ReadParameters(theTexture.Parameters());

	//		// Use this to embed the texture into the GimData.
	//		currentTexture.FilePath = theTexture.FilePath().ToString();
	//		ReadTextureFile(currentTexture);

	//		gimTextures.Add(currentTexture);
	//		currentTexture = null;
	//	}


	//	public override void Apply(ModelData_PixMapTexture theTexture)
	//	{
	//		currentTexture = new GimTexture();

	//		ReadTextureType(theTexture.Type());
	//		ReadParameters(theTexture.Parameters());
	//		ReadPixMap(theTexture.PixMap());

	//		gimTextures.Add(currentTexture);
	//		currentTexture = null;
	//	}

	//	private void ReadTextureFile(GimTexture gimTexture)
	//	{
	//		if (!File.Exists(gimTexture.FilePath))
	//			throw new Exception("File does not exist: " + gimTexture.FilePath);

	//		FileStream textureFileStream = new FileStream(gimTexture.FilePath, FileMode.Open, System.IO.FileAccess.Read);

	//		if (textureFileStream.Length > int.MaxValue)
	//		{
	//			textureFileStream.Close();
	//			throw new Exception("Texture file " + gimTexture.FilePath + "is to large. Size is: " + textureFileStream.Length + " bytes. ");
	//		}

	//		byte[] textureFile;
	//		textureFile = new byte[textureFileStream.Length];
	//		textureFileStream.Read(textureFile, 0, (int)textureFileStream.Length);
	//		currentTexture.TextureFile = textureFile;

	//		textureFileStream.Close();
	//	}

	//	private void ReadTextureType(ModelData_TextureType textureType)
	//	{
	//		switch (textureType)
	//		{
	//			case ModelData_TextureType.ModelData_TT_DiffuseMap:
	//				currentTexture.TextureType = GimTextureType.DiffuseMap;
	//				return;
	//			case ModelData_TextureType.ModelData_TT_SpecularMap:
	//				currentTexture.TextureType = GimTextureType.SpecularMap;
	//				return;
	//			case ModelData_TextureType.ModelData_TT_EmissiveMap:
	//				currentTexture.TextureType = GimTextureType.EmissiveMap;
	//				return;
	//			case ModelData_TextureType.ModelData_TT_BumpMap:
	//				currentTexture.TextureType = GimTextureType.BumpMap;
	//				return;
	//			case ModelData_TextureType.ModelData_TT_NormalMap:
	//				currentTexture.TextureType = GimTextureType.NormalMap;
	//				return;
	//			case ModelData_TextureType.ModelData_TT_DisplacementMap:
	//				currentTexture.TextureType = GimTextureType.DisplacementMap;
	//				return;
	//			case ModelData_TextureType.ModelData_TT_SphereEnvironmentMap:
	//				currentTexture.TextureType = GimTextureType.SphereEnviromentMap;
	//				return;
	//			case ModelData_TextureType.ModelData_TT_Unknown:
	//			default:
	//				currentTexture.TextureType = GimTextureType.Unkown;
	//				return;
	//		}
	//	}

	//	private void ReadParameters(ModelData_TextureParameters parameters)
	//	{
	//		if (parameters == null)
	//			return;

	//		// TODO: GenerateMipMaps, MagnificationFilter, MinificationFilter, SetWrapMode U and V, BlendMode, MappingMode.
	//		currentTexture.ScaleU = parameters.ScaleU();
	//		currentTexture.ScaleV = parameters.ScaleV();
	//		currentTexture.Rotation = parameters.Rotation();
	//		currentTexture.TranslationU = parameters.TranslationU();
	//		currentTexture.TranslationV = parameters.TranslationV();
	//	}

	//	private void ReadPixMap(ModelData_PixMap pixMap)
	//	{
	//		uint pixelDataSize = pixMap.PixelDataSize();

	//		currentTexture.pixelFormat = (int)pixMap.PixelFormat();
	//		currentTexture.bytesPerPixel = BytesPerPixel(pixMap.Width(), pixMap.Height(), pixelDataSize);

	//		GimLogger.Warn("Pixmap textures can not be read. ");

	//		// Important: PixelData() is not supported in C# contact Cadex?
	//		//currentTexture.TexturePixelBuffer = new byte[(int)pixelDataSize];
	//		//for (int i = 0; i < pixelDataSize; i++)
	//		//{
	//		//	currentTexture.TexturePixelBuffer[i] = pixMap.PixelData()[i];
	//		//}
	//	}

	//	// Do this using ModelData_PixelFormat Enum?
	//	private static uint BytesPerPixel(uint width, uint height, uint datasize)
	//	{
	//		uint bytesCount = datasize / (width * height);
	//		if (bytesCount * width * height != datasize)
	//			throw new InvalidOperationException();
	//		return bytesCount;
	//	}
	//}
}
