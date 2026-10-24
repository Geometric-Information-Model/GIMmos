using NUnit.Framework;
using GIMcompress;
using GIMcompress.ElementHelpers;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class OffsetCurveTests
    {
        [Test]
        public void Builder_ProducesCorrectElement()
        {
            var element = OffsetCurveBuilder.FromOffsetAndDirection(5, new Vect(0, 0, 1))
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_offset));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.Locals.Count, Is.EqualTo(1));
        }

        [Test]
        public void Builder_NullDirection_Throws()
        {
            Assert.That(() => OffsetCurveBuilder.FromOffsetAndDirection(5, new Vect(0, 0, 0)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_Properties()
        {
            var element = OffsetCurveBuilder.FromOffsetAndDirection(3, new Vect(0, 0, 1))
                .BuildElement();
            var adapter = new GimOffsetCurve(element);

            Assert.That(adapter.Offset, Is.EqualTo(3));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line };
            Assert.That(() => new GimOffsetCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
