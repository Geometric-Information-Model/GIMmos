using GIMcompress;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;

namespace TestPlatform
{
	public class RoundingTests
    {
        [Test, Theory]
        public void RoundNearZeros()
        {
            double infinitesimal = Math.Pow(10, -17);

            short precision = 5;
            double margin = Math.Pow(10, -precision - 2);
            double result = GIMcompress.ValueRounder.roundDouble(infinitesimal, margin, precision);
            // should get rounded to 0
            
            ClassicAssert.AreEqual(0, result);
        }

        [Test, Theory]
        public void RoundNearIntegers()
        {
            double almostInt = (double)3.999999999;

            short precision = 5;
            double margin = Math.Pow(10, -precision - 2);
            double result = GIMcompress.ValueRounder.roundDouble(almostInt, margin, precision);
            // should equal 4
            ClassicAssert.AreEqual(4, result);
        }

        [Test, Theory]
        public void RoundNearFractions()
        {
            double almostFraction = (double)8.49999999;

            short precision = 4;
            double margin = Math.Pow(10, -precision - 2);
            double result = GIMcompress.ValueRounder.roundDouble(almostFraction, margin, precision);
            // should equal 8.5
            ClassicAssert.AreEqual(8.5, result);
        }

        [Test, Theory]
        public void DontRoundIrrationals()
        {
            double sqrt2 = Math.Sqrt(2);

            short precision = 4;
            double margin = Math.Pow(10, -precision - 2);
            double result = GIMcompress.ValueRounder.roundDouble(sqrt2, margin, precision);
            // should equal sqrt(2)
            ClassicAssert.AreEqual(Math.Sqrt(2), result);
        }

        [Test, Theory]
        public void DontRoundTranscendental()
        {
            double multipleOfPi = Math.PI * 7;

            short precision = 4;
            double margin = Math.Pow(10, -precision - 2);
            double result = GIMcompress.ValueRounder.roundDouble(multipleOfPi, margin, precision);
            // should equal sqrt(2)
            ClassicAssert.AreEqual(Math.PI * 7, result);
        }
        [Test, Theory]
        public void RoundUpFullElement()
        {
            GimElement element = new GimElement();
            GimLocal local = new GimLocal();

            // these values may be nonsensical and have no meaning, but this is purely to test that rounding works fine
            local.Px = Math.Pow(10, -10);
            local.Py = 3;
            local.Pz = 3.123456789;

            local.Xy = int.MinValue;
            local.Xz = int.MinValue;

            local.Yx = Math.PI;
            local.Yz = Math.Pow(10, -10);

            local.Zx = 8.49999999;
            local.Zy = 8.49999999;

            element.ElementDoubles = new double[] { 2.9999999, 3 };
            element.locals = new List<GimLocal>() { local };

            element = ValueRounder.RoundElementDoubles(element, 5);
            element = ValueRounder.RoundElementLocals(element, 5);

            // expected result

            GimElement expectedElement = new();
            GimLocal expectedLocal = new();

            expectedLocal.Px = 0;
            expectedLocal.Py = 3;
            expectedLocal.Pz = 3.123456789;

            expectedLocal.Xy = int.MinValue;
            expectedLocal.Xz = int.MinValue;

            expectedLocal.Yx = Math.PI;
            expectedLocal.Yz = 0;

            expectedLocal.Zx = 8.5;
            expectedLocal.Zy = 8.5;

            expectedElement.locals = new List<GimLocal>() { expectedLocal };

            expectedElement.ElementDoubles = new double[] { 3, 3 };

            ClassicAssert.IsTrue(element.Compare(expectedElement));
        }
    }
}