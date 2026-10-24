using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Bezier2DCurveTests
    {
        [Test]
        public void Builder_HappyPath_ProducesCorrectElement()
        {
            var poles = new Vect[] { new(0, 0), new(1, 1), new(2, 0) };

            var element = Bezier2DBuilder.FromControlPoints(poles)
                .WithParamRange(0, 1)
                .WithEndpoints(0, 0, 2, 0)
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_bezier2D));
            Assert.That(element.ElementInts[0], Is.EqualTo(3));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[^2], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[^1], Is.EqualTo(1));
        }

        [Test]
        public void Adapter_Properties()
        {
            var poles = new Vect[] { new(0, 0), new(1, 1), new(2, 0) };
            var element = Bezier2DBuilder.FromControlPoints(poles)
                .WithParamRange(0, 1)
                .WithEndpoints(0, 0, 2, 0)
                .BuildElement();
            var adapter = new GimBezier2DCurve(element);

            Assert.That(adapter.PoleCount, Is.EqualTo(3));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(1));
            Assert.That(adapter.GetStartPoint(), Is.EqualTo(new Vect(0, 0)));
            Assert.That(adapter.GetEndPoint(), Is.EqualTo(new Vect(2, 0)));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Builder_EmptyPoles_Throws()
        {
            Assert.That(() => Bezier2DBuilder.FromControlPoints(new Vect[0]),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimBezier2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
