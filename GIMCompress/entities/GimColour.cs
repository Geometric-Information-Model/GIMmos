namespace GIMcompress
{
	public class GimColour
	{
		public float alpha, red, green, blue;

		public GimColour() { }

		public GimColour(float a, float r, float g, float b)
		{
			alpha = a;
			red = r;
			green = g;
			blue = b;
		}

		public int ToInt()
		{
			int a = (int)(alpha * 255) << 24;
			int r = (int)(red * 255) << 16;
			int g = (int)(green * 255) << 8;
			int b = (int)(blue * 255);
			return a + r + g + b;
		}

		public string ToHexString()
		{
            string redHex = ((int)(red * 255)).ToString("X2");
            string greenHex = ((int)(green * 255)).ToString("X2");
            string blueHex = ((int)(blue * 255)).ToString("X2");
            string alphaHex = ((int)(alpha * 255)).ToString("X2");

            return "#" + redHex + greenHex + blueHex + alphaHex;
        }

		public static GimColour FromHexString(string value)
		{
            string redHex = value.Substring(1, 2);
            string greenHex = value.Substring(3, 2);
            string blueHex = value.Substring(5, 2);

            GimColour gimCol = new();

            gimCol.red = (float)int.Parse(redHex, System.Globalization.NumberStyles.HexNumber) / 255;
            gimCol.green = (float)int.Parse(greenHex, System.Globalization.NumberStyles.HexNumber) / 255;
            gimCol.blue = (float)int.Parse(blueHex, System.Globalization.NumberStyles.HexNumber) / 255;

			// if the hex string has an alpha value
            if (value.Length > 7)
            {
                string alphaHex = value.Substring(7, 2);
                gimCol.alpha = (float)int.Parse(alphaHex, System.Globalization.NumberStyles.HexNumber) / 255;
            }
            else
            {
                gimCol.alpha = 1;
            }

			return gimCol;
        }

		public static GimColour FromInt(int colorValue)
        {
			uint unsignedColorValue = (uint)colorValue;
			float a = (float)(unsignedColorValue >> 24) / 255;
			float r = (float)((unsignedColorValue << 8) >> 24) / 255;
			float g = (float)((unsignedColorValue << 16) >> 24) / 255;
			float b = (float)((unsignedColorValue << 24) >> 24) /255;

			return new GimColour(a, r, g, b);
        }

	}
}
