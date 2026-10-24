using NUnit.Framework;
using GIMcompress;
using GIMcompress.ElementHelpers;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class ParabolaCurveTests
    {
        [Test]
        public void Builder_WithDirections_ProducesCorrectElement()
        {
            var element = ParabolaBuilder.FromCenterAndFocal(new Vect(0, 0, 0), 2)
                .WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithParamRange(0, 10).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_parabol));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(10));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(2));
        }

        [Test]
        public void Builder_WithDirectionAndAxis_ProducesCorrectElement()
        {
            var element = ParabolaBuilder.FromCenterAndFocal(new Vect(0, 0, 0), 2)
                .WithDirectionAndAxis(new Vect(1, 0, 0), new Vect(0, 0, 1))
                .WithParamRange(0, 10).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_parabol));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(2));
        }

        [Test]
        public void Builder_ZeroFocal_Throws()
        {
            Assert.That(() => ParabolaBuilder.FromCenterAndFocal(new Vect(0, 0, 0), 0),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_IsNotClosed()
        {
            var element = ParabolaBuilder.FromCenterAndFocal(new Vect(0, 0, 0), 1)
                .WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithParamRange(0, 5).BuildElement();

            var adapter = new GimParabolaCurve(element);
            Assert.That(adapter.Focal, Is.EqualTo(1));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_GetSymmetryAxis_GetParallelDirection()
        {
            var element = ParabolaBuilder.FromCenterAndFocal(new Vect(0, 0, 0), 2)
                .WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithParamRange(0, 5).BuildElement();

            var adapter = new GimParabolaCurve(element);
            Assert.That(adapter.GetSymmetryAxis()[0], Is.EqualTo(1).Within(1e-6));
            Assert.That(adapter.GetParallelDirection()[1], Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line };
            Assert.That(() => new GimParabolaCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
