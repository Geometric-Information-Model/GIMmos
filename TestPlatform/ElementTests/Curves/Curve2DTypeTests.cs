using GIMcompress;
using NUnit.Framework;
using System;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Curve2DTypeTests
    {
        [TestCase(GimElementType.C_line2D, 2)]
        [TestCase(GimElementType.C_circle2D, 3)]
        [TestCase(GimElementType.C_elipse2D, 4)]
        [TestCase(GimElementType.C_hyperbol2D, 4)]
        [TestCase(GimElementType.C_parabol2D, 3)]
        [TestCase(GimElementType.C_offset2D, 1)]
        [TestCase(GimElementType.C_trimmed2D, 2)]
        public void GetDoubleCount_2DTypes_MirrorTheirCounterparts(GimElementType type, int expected)
        {
            Assert.That(type.GetDoubleCount(), Is.EqualTo(expected));
        }

        [TestCase(GimElementType.C_bezier2D)]
        [TestCase(GimElementType.C_bspline2D)]
        public void GetDoubleCount_VariableLength2DTypes_ReturnMinusOne(GimElementType type)
        {
            Assert.That(type.GetDoubleCount(), Is.EqualTo(-1));
        }

        [Test]
        public void ScaleCurve_Line2D_ScalesParams()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_line2D,
                ElementDoubles = [0, 10],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(2.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(20));
        }

        [Test]
        public void ScaleCurve_Circle2D_ScalesRadiusOnly()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_circle2D,
                ElementDoubles = [5.0, 0, Math.PI],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(3.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(15.0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(Math.PI));
        }

        [Test]
        public void ScaleCurve_Elipse2D_ScalesRadii()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_elipse2D,
                ElementDoubles = [4.0, 2.0, 0, Math.PI],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(2.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(8.0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(4.0));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(Math.PI));
        }

        [Test]
        public void ScaleCurve_Hyperbol2D_ScalesRadiiAtCorrectIndices()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_hyperbol2D,
                ElementDoubles = [0.5, 1.5, 3.0, 6.0],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(2.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(0.5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(1.5));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(6.0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(12.0));
        }

        [Test]
        public void ScaleCurve_Parabol2D_ScalesAllThreeDoubles()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_parabol2D,
                ElementDoubles = [1.0, 2.0, 5.0],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(4.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(4.0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(8.0));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(20.0));
        }

        [Test]
        public void ScaleCurve_Offset2D_ScalesOffset()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_offset2D,
                ElementDoubles = [7.0],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(0.5);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(3.5));
        }

        [Test]
        public void ScaleCurve_Trimmed2D_ScalesParams()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_trimmed2D,
                ElementDoubles = [2.0, 8.0],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(3.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(6.0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(24.0));
        }

        [Test]
        public void ScaleCurve_BSpline2D_ScalesHeaderAndPoles()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_bspline2D,
                ElementDoubles = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 0.0, 0.0, 1.0, 1.0],
                ElementInts = [2, 1, 1, 2],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(10.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(10.0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(20.0));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(30.0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(40.0));
            Assert.That(element.ElementDoubles[4], Is.EqualTo(50.0));
            Assert.That(element.ElementDoubles[5], Is.EqualTo(60.0));
            Assert.That(element.ElementDoubles[6], Is.EqualTo(0.0));
            Assert.That(element.ElementDoubles[7], Is.EqualTo(0.0));
            Assert.That(element.ElementDoubles[8], Is.EqualTo(1.0));
            Assert.That(element.ElementDoubles[9], Is.EqualTo(1.0));
        }

        [Test]
        public void ScaleCurve_Bezier2D_ScalesHeaderAndPoles()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_bezier2D,
                ElementDoubles = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 0.0, 1.0],
                ElementInts = [1],
                Locals = [new GimLocal()]
            };
            var scaler = new ScaleChanger(10.0);
            scaler.ScaleCurve(element);

            Assert.That(element.ElementDoubles[0], Is.EqualTo(10.0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(20.0));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(30.0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(40.0));
            Assert.That(element.ElementDoubles[4], Is.EqualTo(50.0));
            Assert.That(element.ElementDoubles[5], Is.EqualTo(60.0));
            Assert.That(element.ElementDoubles[6], Is.EqualTo(0.0));
            Assert.That(element.ElementDoubles[7], Is.EqualTo(1.0));
        }

        [Test]
        public void CheckSplineIsFlat_RejectsBSpline2D()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_bspline2D,
                ElementDoubles = [0, 0, 1, 0],
                Locals = [new GimLocal()]
            };

            Assert.That(() => CurveHelpers.checkSplineIsFlat(element),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void All2DTypes_AreCurves()
        {
            Assert.That(GimElementType.C_line2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_circle2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_elipse2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_hyperbol2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_parabol2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_bezier2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_bspline2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_offset2D.IsCurve(), Is.True);
            Assert.That(GimElementType.C_trimmed2D.IsCurve(), Is.True);
        }

        [TestCase(GimElementType.C_line2D, 0, true)]
        [TestCase(GimElementType.C_line2D, 1, true)]
        [TestCase(GimElementType.C_circle2D, 0, true)]
        [TestCase(GimElementType.C_circle2D, 1, false)]
        [TestCase(GimElementType.C_circle2D, 2, false)]
        [TestCase(GimElementType.C_offset2D, 0, true)]
        [TestCase(GimElementType.C_trimmed2D, 0, false)]
        [TestCase(GimElementType.C_trimmed2D, 1, false)]
        [TestCase(GimElementType.C_elipse2D, 0, true)]
        [TestCase(GimElementType.C_elipse2D, 1, true)]
        [TestCase(GimElementType.C_elipse2D, 2, false)]
        [TestCase(GimElementType.C_hyperbol2D, 2, true)]
        [TestCase(GimElementType.C_hyperbol2D, 3, true)]
        [TestCase(GimElementType.C_hyperbol2D, 0, false)]
        [TestCase(GimElementType.C_parabol2D, 2, true)]
        [TestCase(GimElementType.C_parabol2D, 0, false)]
        public void IsDoubleDistance_PrBBTypes(GimElementType type, int index, bool expected)
        {
            var element = new GimElement
            {
                ElementType = type,
                ElementDoubles = type switch
                {
                    GimElementType.C_line2D => [0.0, 10.0],
                    GimElementType.C_circle2D => [5.0, 0.0, Math.PI],
                    GimElementType.C_offset2D => [7.0],
                    GimElementType.C_trimmed2D => [1.0, 5.0],
                    GimElementType.C_elipse2D => [4.0, 2.0, 0.0, Math.PI],
                    GimElementType.C_hyperbol2D => [0.5, 1.5, 3.0, 6.0],
                    GimElementType.C_parabol2D => [1.0, 2.0, 5.0],
                    _ => []
                },
                Locals = [new GimLocal()]
            };

            Assert.That(element.IsDoubleDistance((uint)index), Is.EqualTo(expected));
        }

        [Test]
        public void IsDoubleDistance_Bezier2D_PolesYesParamsNo()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_bezier2D,
                ElementDoubles = [0, 0, 1, 0, 0.5, 0.5, 0, 1],
                ElementInts = [1],
                Locals = [new GimLocal()]
            };

            Assert.That(element.IsDoubleDistance(0), Is.True);
            Assert.That(element.IsDoubleDistance(5), Is.True);
            Assert.That(element.IsDoubleDistance(6), Is.False);
            Assert.That(element.IsDoubleDistance(7), Is.False);
        }

        [Test]
        public void IsDoubleDistance_BSpline2D_PolesYesKnotsNo()
        {
            var element = new GimElement
            {
                ElementType = GimElementType.C_bspline2D,
                ElementDoubles = [0, 0, 1, 0, 0.5, 0.5, 0, 0, 1, 1, 1.0],
                ElementInts = [2, 1, 1, 2],
                Locals = [new GimLocal()]
            };

            Assert.That(element.IsDoubleDistance(0), Is.True);
            Assert.That(element.IsDoubleDistance(5), Is.True);
            Assert.That(element.IsDoubleDistance(6), Is.False);
            Assert.That(element.IsDoubleDistance(9), Is.False);
        }

        [Test]
        public void All2DTypes_AreIs2DType()
        {
            Assert.That(GimElementType.C_line2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_circle2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_elipse2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_hyperbol2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_parabol2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_bezier2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_bspline2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_offset2D.Is2DType(), Is.True);
            Assert.That(GimElementType.C_trimmed2D.Is2DType(), Is.True);
        }
    }
}
