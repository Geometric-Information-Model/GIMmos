using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Parabola2DCurveTests
    {
        [Test]
        public void Builder_WithDirectionUAndV_ProducesCorrectElement()
        {
            var element = Parabola2DBuilder.FromCenterAndFocal(0, 0, 2)
                .WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithParamRange(0, 10).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_parabol2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(10));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(2));
        }

        [Test]
        public void Builder_ZeroFocal_Throws()
        {
            Assert.That(() => Parabola2DBuilder.FromCenterAndFocal(0, 0, 0),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_NegativeFocal_Allowed()
        {
            var element = Parabola2DBuilder.FromCenterAndFocal(0, 0, -2)
                .WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithParamRange(0, 1).BuildElement();

            Assert.That(element.ElementDoubles[2], Is.EqualTo(-2));
        }

        [Test]
        public void Adapter_IsNotClosed()
        {
            var adapter = new GimParabola2DCurve(Parabola2DBuilder.FromCenterAndFocal(0, 0, 1)
                .WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithParamRange(0, 5).BuildElement());

            Assert.That(adapter.Focal, Is.EqualTo(1));
            Assert.That(adapter.IsClosed, Is.False);
            Assert.That(adapter.GetStartPoint(), Is.EqualTo(new Vect(0, 0)));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimParabola2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
