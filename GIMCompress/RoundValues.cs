using System;

namespace GIMcompress
{

	public static class ValueRounder
	{
        /// <summary>
        /// Rounds the numerical values in the GimData object to a specified level of precision.
        /// </summary>
        /// <remarks>
        /// This method iterates through each element in the GimData object. For each element, it:
        /// - Rounds the values in 'ElementDoubles' to the specified precision, if they are not null.
        /// - Rounds the values in 'Locals' to the specified precision, if they are not null.
        /// This operation modifies the original GimData object passed to the method.
        /// </remarks>
        /// <param name="gimData">The GimData object whose values need to be rounded.</param>
        /// <param name="precision">The number of decimal places to round to. The default is 5, which means rounding to one part in a hundred thousand.</param>
        /// <returns>The modified GimData object with rounded values.</returns>
        public static GimData RoundValues(GimData gimData, short precision = 5) // default precision is 5, one in a 100k
		{
			foreach (GimElement element in gimData.Elements)//come back R
			{
				// null net
				if (element.ElementDoubles != null)
				{
					RoundElementDoubles(element, precision);
				}

				if (element.Locals != null)
				{
					RoundElementLocals(element, precision);
				}

			}
			return gimData;
		}

		public static GimElement RoundElementDoubles(GimElement element, short precision)
        {
			double margin = Math.Pow(10, -precision-2);

			//for loop to access by reference, not value
			for (int i = 0; i < element.ElementDoubles.Length; i++)
			{
                element.ElementDoubles[i] = roundDouble(element.ElementDoubles[i], margin, precision);
			}

			return element;
		}

		public static GimElement RoundElementLocals(GimElement element, short precision)
        {
			double margin = Math.Pow(10, -precision-2);
			foreach(GimLocal local in element.Locals)
			{
                local.Px = roundDouble(local.Px, margin, precision);
                local.Py = roundDouble(local.Py, margin, precision);
                local.Pz = roundDouble(local.Pz, margin, precision);
                local.Xx = roundDouble(local.Xx, margin, precision);
                local.Xy = roundDouble(local.Xy, margin, precision);
                local.Xz = roundDouble(local.Xz, margin, precision);
                local.Yx = roundDouble(local.Yx, margin, precision);
                local.Yy = roundDouble(local.Yy, margin, precision);
                local.Yz = roundDouble(local.Yz, margin, precision);
                local.Zx = roundDouble(local.Zx, margin, precision);
                local.Zy = roundDouble(local.Zy, margin, precision);
                local.Zz = roundDouble(local.Zz, margin, precision); 
			}
			return element;
        }

        // any value whos round is within one hundredth of the rounded value precision, will be rounded
        // e.g: for precision = 5,
        // x = 2.999999995, margin = 0.0000001, x - round(x) = 0.0000000005 < margin => x = 3
        // x = 3.14156592,  margin = 0.0000001, x - round(x) = 0.000035 > margin => x is not rounded
        //
        // TLDR: if rounding x by precision or precision+2 results in the same number, that number should probably
        // just be rounded (repeating nines/zeroes). 

		// PS: this is set to public for unit tests
        public static double roundDouble(double x, double margin, short precision)
        {
            if (Math.Abs(x - Math.Round(x, precision)) < margin)
            {
                return Math.Round(x, precision);
            }
            return x;
        }

        /// <summary>
        /// Compares two doubles with an equality margin. Said margin can be passed in, but defaults to 10^-6
        /// </summary>
        /// <param name="A">The first double</param>
        /// <param name="B">The second double</param>
        /// <param name="margin">The equality margin</param>
        /// <returns>True if the difference is within the margin</returns>
        public static bool CompareDouble(double A, double B, double margin = -1)
        {
            if (margin == -1) margin = Matrix.EQUALITY_ROUND_MARGIN;
            return Math.Abs(A - B) <= margin;
        }
	}
}