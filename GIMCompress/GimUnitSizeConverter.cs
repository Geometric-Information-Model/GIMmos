using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMCompress
{
    public interface IUnitConversionStrategy
    {
        void ConvertUnits(GimLocal local, double[] transformation);
    }

    public class MetersToMillimetersConversion : IUnitConversionStrategy
    {
        public void ConvertUnits(GimLocal local, double[] transformation)
        {
            local.px = transformation[0] * 1000; // Example conversion
            local.py = transformation[1] * 1000;
            local.pz = transformation[2] * 1000;
        }
    }
    public class MetersToHundredMetersConversion : IUnitConversionStrategy
    {
        /// <summary>
        /// Converts unit measurements from meters to hundred meters scale, effectively to millimeters for 100m scale.
        /// </summary>
        /// <param name="local">The GimLocal object containing the positions (px, py, pz) in meters.</param>
        /// <param name="transformation">An array representing the transformation matrix or values.</param>
        /// <remarks>
        /// This method applies a conversion factor of 100000 (100 meters in millimeters) to each position component (px, py, pz) 
        /// of the GimLocal object, effectively converting the units from meters to millimeters for a 100 meter scale.
        /// </remarks>
        public void ConvertUnits(GimLocal local, double[] transformation)
        {
            local.px = transformation[0] * 100000; // Converting from meters to millimeters for 100m scale
            local.py = transformation[1] * 100000;
            local.pz = transformation[2] * 100000;
        }
    }
    public class MetersToFiveMetersConversion : IUnitConversionStrategy
    {
        /// <summary>
        /// Converts unit measurements from meters to a scale of 5 meters, effectively to millimeters.
        /// </summary>
        /// <param name="local">The GimLocal object containing the positions (px, py, pz) in meters.</param>
        /// <param name="transformation">An array representing the transformation matrix or values.</param>
        /// <remarks>
        /// This method applies a conversion factor of 5000 (5 meters in millimeters) to each position component (px, py, pz) 
        /// of the GimLocal object, effectively converting the units from meters to millimeters for a 5 meter scale.
        /// </remarks>
        public void ConvertUnits(GimLocal local, double[] transformation)
        {
            local.px = transformation[0] * 5000; // Converting from meters to millimeters for 5 meter scale
            local.py = transformation[1] * 5000;
            local.pz = transformation[2] * 5000;
        }
    }

    public class GimUnitProcessor
    {
        private IUnitConversionStrategy conversionStrategy;

        public void SetConversionStrategy(IUnitConversionStrategy strategy)
        {
            conversionStrategy = strategy;
        }

        public void ConvertUnits(GimLocal local, double[] transformation)
        {
            conversionStrategy?.ConvertUnits(local, transformation);
        }
    }
}
