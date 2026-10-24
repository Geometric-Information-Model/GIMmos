using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Offset2DCurveTests
    {
        [Test]
        public void Builder_ProducesCorrectElement()
        {
            var element = Offset2DBuilder.FromOffsetAndDirection(5, 0, 1).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_offset2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.Locals.Count, Is.EqualTo(1));
            Assert.That(element.Locals[0].Zz, Is.EqualTo(0));
        }

        [Test]
        public void Builder_NullDirection_Throws()
        {
            Assert.That(() => Offset2DBuilder.FromOffsetAndDirection(5, 0, 0),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_3DDirection_Throws()
        {
            Assert.That(() => Offset2DBuilder.FromOffsetAndDirection(5, new Vect(0, 0, 1)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_Properties()
        {
            var local = new GimLocal { Px = 1, Py = 2, Zx = 0, Zy = 1 };
            var element = new GimElement
            {
                ElementType = GimElementType.C_offset2D,
                ElementDoubles = [3],
                Locals = [local]
            };
            var adapter = new GimOffset2DCurve(element);

            Assert.That(adapter.Offset, Is.EqualTo(3));
            Assert.That(adapter.IsClosed, Is.False);
            Assert.That(adapter.GetStartPoint(), Is.EqualTo(new Vect(1, 2)));
            Assert.That(adapter.GetEndPoint(), Is.EqualTo(new Vect(1, 5)));
            Assert.That(adapter.GetDirection(), Is.EqualTo(new Vect(0, 1)));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimOffset2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_BuildAdapter_Roundtrip()
        {
            var adapter = new GimOffset2DCurve(
                Offset2DBuilder.FromOffsetAndDirection(2, 1, 0).BuildElement());
            Assert.That(adapter.Offset, Is.EqualTo(2));
            Assert.That(adapter.GetDirection(), Is.EqualTo(new Vect(1, 0)));
        }
    }
}
