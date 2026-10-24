using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class BSpline2DCurveTests
    {
        [Test]
        public void Builder_HappyPath_ProducesCorrectLayout()
        {
            var poles = new Vect[] { new(0, 0), new(1, 1), new(2, 0) };
            double[] knots = { 0, 0, 0, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };

            var element = BSpline2DBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(0, 0, 2, 0)
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_bspline2D));
            Assert.That(element.ElementInts[0], Is.EqualTo(6));
            Assert.That(element.ElementInts[^1], Is.EqualTo(2));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(0));
        }

        [Test]
        public void Adapter_ExtractsCorrectProperties()
        {
            var poles = new Vect[] { new(0, 0), new(1, 1), new(2, 0) };
            double[] knots = { 0, 0, 0, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };

            var element = BSpline2DBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(0, 0, 2, 0)
                .BuildElement();
            var adapter = new GimBSpline2DCurve(element);

            Assert.That(adapter.PointCount, Is.EqualTo(3));
            Assert.That(adapter.KnotCount, Is.EqualTo(6));
            Assert.That(adapter.Degree, Is.EqualTo(2));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(1));
        }

        [Test]
        public void Adapter_ParamDomain_UsesClampedKnotIndices()
        {
            var poles = new Vect[] { new(0, 0), new(1, 1), new(2, 0) };
            double[] knots = { 0, 0.1, 0.2, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };

            var element = BSpline2DBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(0, 0, 2, 0)
                .BuildElement();
            var adapter = new GimBSpline2DCurve(element);

            Assert.That(adapter.ParamFirst, Is.EqualTo(0.2));
            Assert.That(adapter.ParamLast, Is.EqualTo(1));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimBSpline2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
