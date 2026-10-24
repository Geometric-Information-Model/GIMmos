using NUnit.Framework;
using GIMcompress;
using GIMcompress.ElementHelpers;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class TrimmedCurveTests
    {
        [Test]
        public void Builder_ProducesCorrectElement()
        {
            var element = TrimmedCurveBuilder.FromParams(1, 5).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_trimmed));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(1));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(5));
        }

        [Test]
        public void Builder_InvertedParams_Throws()
        {
            Assert.That(() => TrimmedCurveBuilder.FromParams(5, 1),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_EqualParams_Throws()
        {
            Assert.That(() => TrimmedCurveBuilder.FromParams(3, 3),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_Properties()
        {
            var element = TrimmedCurveBuilder.FromParams(2, 10).BuildElement();
            var adapter = new GimTrimmedCurve(element);

            Assert.That(adapter.ParamFirst, Is.EqualTo(2));
            Assert.That(adapter.ParamLast, Is.EqualTo(10));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line };
            Assert.That(() => new GimTrimmedCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
