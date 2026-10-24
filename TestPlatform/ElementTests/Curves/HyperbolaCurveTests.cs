using NUnit.Framework;
using GIMcompress;
using GIMcompress.ElementHelpers;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class HyperbolaCurveTests
    {
        [Test]
        public void Builder_WithDirections_ProducesCorrectElement()
        {
            var element = HyperbolaBuilder.FromCenterAndRadii(new Vect(0, 0, 0), 3, 2)
                .WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithParamRange(-1, 1).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_hyperbol));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(-1));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(1));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(3));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(2));
        }

        [Test]
        public void Builder_WithDirectionAndAxis_ProducesCorrectElement()
        {
            var element = HyperbolaBuilder.FromCenterAndRadii(new Vect(0, 0, 0), 3, 2)
                .WithDirectionAndAxis(new Vect(1, 0, 0), new Vect(0, 0, 1))
                .WithParamRange(-1, 1).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_hyperbol));
        }

        [Test]
        public void Builder_NullDirections_Throws()
        {
            Assert.That(() => HyperbolaBuilder.FromCenterAndRadii(new Vect(0, 0, 0), 3, 2)
                .WithDirections(new Vect(0, 0, 0), new Vect(1, 0, 0)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_IsNotClosed()
        {
            var element = HyperbolaBuilder.FromCenterAndRadii(new Vect(0, 0, 0), 3, 2)
                .WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithParamRange(-1, 1).BuildElement();

            var adapter = new GimHyperbolaCurve(element);
            Assert.That(adapter.MajorRadius, Is.EqualTo(3));
            Assert.That(adapter.MinorRadius, Is.EqualTo(2));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_GetMajorAxis_GetMinorAxis()
        {
            var element = HyperbolaBuilder.FromCenterAndRadii(new Vect(0, 0, 0), 3, 2)
                .WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithParamRange(-1, 1).BuildElement();

            var adapter = new GimHyperbolaCurve(element);
            Assert.That(adapter.GetMajorAxis()[0], Is.EqualTo(1).Within(1e-6));
            Assert.That(adapter.GetMinorAxis()[1], Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line };
            Assert.That(() => new GimHyperbolaCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
