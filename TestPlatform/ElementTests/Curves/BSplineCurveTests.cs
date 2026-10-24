using NUnit.Framework;
using GIMcompress;
using GIMcompress.ElementHelpers;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class BSplineCurveTests
    {
        [Test]
        public void Builder_HappyPath_ProducesCorrectLayout()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };
            double[] knots = { 0, 0, 0, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };

            var element = BSplineBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(2, 0, 0))
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_bspline));
            Assert.That(element.ElementInts[0], Is.EqualTo(6));
            Assert.That(element.ElementInts[^1], Is.EqualTo(2));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[4], Is.EqualTo(0));
        }

        [Test]
        public void Builder_WithUniformWeights_ProducesUnityWeights()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };
            double[] knots = { 0, 0, 0, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };

            var element = BSplineBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(2, 0, 0))
                .BuildElement();
            var adapter = new GimBSplineCurve(element);

            for (int i = 0; i < adapter.PointCount; i++)
                Assert.That(adapter.Weights[i], Is.EqualTo(1.0));
        }

        [Test]
        public void Builder_WeightsLengthMismatch_ThrowsAtStep()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0) };

            Assert.That(() => BSplineBuilder.FromPoles(poles).WithWeights(new[] { 1.0, 1.0 }),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_NonPositiveWeight_ThrowsAtStep()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0) };

            Assert.That(() => BSplineBuilder.FromPoles(poles).WithWeights(new[] { 1.0, 0.0, 1.0 }),
                Throws.InstanceOf<InvalidGimElementException>());
            Assert.That(() => BSplineBuilder.FromPoles(poles).WithWeights(new[] { 1.0, -1.0, 1.0 }),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_NonUnityWeights_RoundTripsThroughAdapter()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };
            double[] knots = { 0, 0, 0, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };
            double[] weights = { 1.0, 2.0, 1.0 };

            var element = BSplineBuilder.FromPoles(poles).WithWeights(weights).WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(2, 0, 0))
                .BuildElement();
            var adapter = new GimBSplineCurve(element);

            Assert.That(adapter.Weights[0], Is.EqualTo(1.0));
            Assert.That(adapter.Weights[1], Is.EqualTo(2.0));
            Assert.That(adapter.Weights[2], Is.EqualTo(1.0));
        }

        [Test]
        public void Builder_KnotMultiplicityMismatch_ThrowsAtStep()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0) };
            double[] knots = { 0, 0, 1 };

            Assert.That(() => BSplineBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(new int[] { 1, 1, 1, 1 }),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_PoleCountNotGreaterThanDegree_ThrowsAtStep()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 0, 0) };
            double[] knots = { 0, 0, 1, 1 };
            int[] mults = { 1, 1, 1, 1 };

            Assert.That(() => BSplineBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(3),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_EmptyPoles_ThrowsAtStep()
        {
            Assert.That(() => BSplineBuilder.FromPoles(new Vect[0]),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_SortsDescendingKnots()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };
            double[] knots = { 0.5, 0.3, 0.8, 1.0, 0.1 };

            // Should not throw — knots get sorted at WithKnots step
            Assert.DoesNotThrow(() => BSplineBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots));
        }

        [Test]
        public void Adapter_ExtractsCorrectProperties()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };
            double[] knots = { 0, 0, 0, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };

            var element = BSplineBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(2, 0, 0))
                .BuildElement();
            var adapter = new GimBSplineCurve(element);

            Assert.That(adapter.KnotCount, Is.EqualTo(6));
            Assert.That(adapter.Degree, Is.EqualTo(2));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(1));
        }

        [Test]
        public void Adapter_ParamDomain_UsesClampedKnotIndices()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };
            double[] knots = { 0, 0.1, 0.2, 1, 1, 1 };
            int[] mults = { 1, 1, 1, 1, 1, 1 };

            var element = BSplineBuilder.FromPoles(poles).WithUniformWeights().WithKnots(knots)
                .WithMultiplicities(mults).WithDegree(2)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(2, 0, 0))
                .BuildElement();
            var adapter = new GimBSplineCurve(element);

            Assert.That(adapter.ParamFirst, Is.EqualTo(0.2));
            Assert.That(adapter.ParamLast, Is.EqualTo(1));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line };
            Assert.That(() => new GimBSplineCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
