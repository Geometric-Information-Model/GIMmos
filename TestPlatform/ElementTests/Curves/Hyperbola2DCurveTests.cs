using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Hyperbola2DCurveTests
    {
        [Test]
        public void Builder_WithDirectionUAndV_ProducesCorrectElement()
        {
            var element = Hyperbola2DBuilder.FromCenterAndRadii(0, 0, 3, 2)
                .WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithParamRange(-1, 1).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_hyperbol2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(-1));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(1));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(3));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(2));
        }

        [Test]
        public void Builder_StoresRadiiAsGiven()
        {
            var element = Hyperbola2DBuilder.FromCenterAndRadii(0, 0, 2, 5)
                .WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithParamRange(0, 1).BuildElement();

            Assert.That(element.ElementDoubles[2], Is.EqualTo(2));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(5));
        }

        [Test]
        public void Adapter_IsNotClosed()
        {
            var adapter = new GimHyperbola2DCurve(Hyperbola2DBuilder.FromCenterAndRadii(0, 0, 3, 2)
                .WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithParamRange(-1, 1).BuildElement());

            Assert.That(adapter.MajorRadius, Is.EqualTo(3));
            Assert.That(adapter.MinorRadius, Is.EqualTo(2));
            Assert.That(adapter.IsClosed, Is.False);
            Assert.That(adapter.GetDirectionU(), Is.EqualTo(new Vect(1, 0)));
            Assert.That(adapter.GetDirectionV(), Is.EqualTo(new Vect(0, 1)));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimHyperbola2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
