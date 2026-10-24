using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Trimmed2DCurveTests
    {
        [Test]
        public void Builder_ProducesCorrectElement()
        {
            var element = Trimmed2DBuilder.FromParams(1, 5).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_trimmed2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(1));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(5));
        }

        [Test]
        public void Builder_InvertedParams_Throws()
        {
            Assert.That(() => Trimmed2DBuilder.FromParams(5, 1),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_EqualParams_Throws()
        {
            Assert.That(() => Trimmed2DBuilder.FromParams(3, 3),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_Properties()
        {
            var element = Trimmed2DBuilder.FromParams(2, 10).BuildElement();
            var adapter = new GimTrimmed2DCurve(element);

            Assert.That(adapter.ParamFirst, Is.EqualTo(2));
            Assert.That(adapter.ParamLast, Is.EqualTo(10));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_GetStartPoint_Throws()
        {
            var adapter = Trimmed2DBuilder.FromParams(0, 1).BuildAdapter();
            Assert.That(() => adapter.GetStartPoint(),
                Throws.InstanceOf<InvalidGimOperation>());
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimTrimmed2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
