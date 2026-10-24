using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class BezierCurveTests
    {
        [Test]
        public void Builder_HappyPath_ProducesCorrectElement()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };

            var element = BezierBuilder.FromControlPoints(poles)
                .WithParamRange(0, 1)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(2, 0, 0))
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_bezier));
            Assert.That(element.ElementInts[0], Is.EqualTo(3));
            Assert.That(element.ElementDoubles[^2], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[^1], Is.EqualTo(1));
        }

        [Test]
        public void Builder_WithExplicitEndpoints()
        {
            var poles = new Vect[] { new(0, 0, 0), new(2, 0, 0) };

            var element = BezierBuilder.FromControlPoints(poles)
                .WithParamRange(0, 1)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(5, 0, 0))
                .BuildElement();

            Assert.That(element.ElementDoubles[3], Is.EqualTo(5));
        }

        [Test]
        public void Adapter_Properties()
        {
            var poles = new Vect[] { new(0, 0, 0), new(1, 1, 0), new(2, 0, 0) };
            var element = BezierBuilder.FromControlPoints(poles)
                .WithParamRange(0, 1)
                .WithEndpoints(new Vect(0, 0, 0), new Vect(2, 0, 0))
                .BuildElement();

            var adapter = new GimBezierCurve(element);
            Assert.That(adapter.PoleCount, Is.EqualTo(3));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(1));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line };
            Assert.That(() => new GimBezierCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
