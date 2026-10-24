using System;
using System.Text;

namespace GIMcompress
{
	public class GimTexture : GimElement, IGimElement
	{
		// Texture file path. Save the file in the GimData/GimTexture object?
		private string filePath;
		private GimTextureType textureType = GimTextureType.Unkown;

		public string FilePath
		{
			get
			{
				if (filePath == null)
					SyncronizeIntsToValues();

				return filePath;
			}
			set
			{
				if (filePath == null)
				{
					filePath = value;
					SyncronizeValuesToInts();
				}
				else
					throw new Exception("File path is already set!");
			}
		}

		public GimTexture() : base()
		{
			ElementDoubles = new double[5];
		}

		public GimTexture(IReadOnlyGimElement gimElementReadOnly) : base()
		{
			GimElement gimElement = gimElementReadOnly.Clone();
			ReadFromElement(gimElement);
		}

		public GimTexture(GimElement gimElement) : base()
		{
			ReadFromElement(gimElement);
		}

		private void ReadFromElement(GimElement element)
		{
			ElementDoubles = element.ElementDoubles;
			ElementInts = element.ElementInts;
			TextureFile = element.ElementBytes;
			SyncronizeIntsToValues();
		}

		// Parameters (Cadex)
		// TODO: GenerateMipMaps, MagnificationFilter, MinificationFilter, SetWrapMode U and V, BlendMode, MappingMode.
		public double Rotation
		{
			get
			{
				return ElementDoubles[0];
			}

			set
			{
				ElementDoubles[0] = value;
			}
		}

		public double ScaleU
		{
			get
			{
				return ElementDoubles[1];
			}

			set
			{
				ElementDoubles[1] = value;
			}
		}

		public double ScaleV
		{
			get
			{
				return ElementDoubles[2];
			}

			set
			{
				ElementDoubles[2] = value;
			}
		}

		public double TranslationU
		{
			get
			{
				return ElementDoubles[3];
			}

			set
			{
				ElementDoubles[3] = value;
			}
		}

		public double TranslationV
		{
			get
			{
				return ElementDoubles[4];
			}

			set
			{
				ElementDoubles[4] = value;
			}
		}

		public byte[] TextureFile
		{
			get
			{
				return ElementBytes;
			}
			set
			{
				ElementBytes = value;
			}
		}

		private void SyncronizeIntsToValues()
		{
			textureType = TextureType;
			int filePathBytesLength = ElementInts[1];

			byte[] filePathBytes = new byte[filePathBytesLength];

			// Copy integers to file path bytes and convert to a string.
			for (int i = 0; i < filePathBytesLength; i += 4)
			{
				try
				{
					filePathBytes[i] = (byte)ElementInts[(i / 4) + 2];
					filePathBytes[i + 1] = (byte)(ElementInts[(i / 4) + 2] >> 8);
					filePathBytes[i + 2] = (byte)(ElementInts[(i / 4) + 2] >> 16);
					filePathBytes[i + 3] = (byte)(ElementInts[(i / 4) + 2] >> 24);
				}
				catch (IndexOutOfRangeException)
				{
					if (i + 4 < filePathBytes.Length)
						throw;
					// If we reached the end of the filePathBytes array we can just skip filling in the rest.
					break;
				}
			}

			filePath = Encoding.UTF8.GetString(filePathBytes);
		}

		private void SyncronizeValuesToInts()
		{
			// Set file and properties in integers.
			byte[] filePathBytes = Encoding.UTF8.GetBytes(filePath);

			int filePathInts = filePathBytes.Length / 4;
			if (filePathInts * 4 < filePathBytes.Length)
				filePathInts++;

			ElementInts = new int[2 + filePathInts];

			TextureType = textureType;
			ElementInts[1] = filePathBytes.Length;

			// Write the file path to the integers.
			int i;
			for (i = 0; i < filePathBytes.Length; i += 4)
			{
				int intIndex = (i / 4) + 2;
				try
				{
					ElementInts[intIndex] = filePathBytes[i];
					ElementInts[intIndex] += filePathBytes[i + 1] << 8;
					ElementInts[intIndex] += filePathBytes[i + 2] << 16;
					ElementInts[intIndex] += filePathBytes[i + 3] << 24;
				}
				catch (IndexOutOfRangeException)
				{
					if (intIndex > elementInts.Length)
						throw;
					if (i + 4 < filePathBytes.Length)
						throw;
					// If we reached the end of the filePathBytes array we can just skip filling in the rest.
					break;
				}
			}
		}

		// For Pixmap only
		public uint bytesPerPixel;
		public int pixelFormat; // Direct cast for ModelData_PixelFormat

		public byte[] TexturePixelBuffer
		{
			get;
			set;
		}

		public new GimElementType ElementType
		{
			get
			{
				return GimElementType.Texture;
			}
		}

		public GimTextureType TextureType
		{
			set
			{
				switch (value)
				{
					case GimTextureType.Unkown:
						elementType = GimElementType.undefined;
						break;
					case GimTextureType.DiffuseMap:
					case GimTextureType.SpecularMap:
					case GimTextureType.EmissiveMap:
					case GimTextureType.BumpMap:
					case GimTextureType.NormalMap:
					case GimTextureType.DisplacementMap:
					case GimTextureType.SphereEnviromentMap:
						elementType = GimElementType.Texture;
						break;
					default: throw new Exception("Texture is unexpected type!");
				}

				if (ElementInts != null)
				{
					ElementInts[0] = (int)value;
				}

				textureType = value;
			}
			get
			{
				if (textureType == GimTextureType.Unkown)
					textureType = (GimTextureType)ElementInts[0];

				if (ElementInts != null && ElementInts[0] != (int)textureType)
					throw new Exception("Element integers not synchronized with actual values");

				return textureType;
			}
		}

		public new GimTexture Clone()
		{
			throw new System.NotImplementedException();
		}
	}

	public enum GimTextureType
	{
		Unkown,
		DiffuseMap,
		SpecularMap,
		EmissiveMap,
		BumpMap,
		NormalMap,
		DisplacementMap,
		SphereEnviromentMap, //Spherical cubemap?
	}
}
