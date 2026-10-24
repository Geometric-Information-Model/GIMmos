using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class LineCurveTests
    {
        [Test]
        public void Builder_FromTwoPoints_ProducesCorrectElement()
        {
            var start = new Vect(0, 0, 0);
            var end = new Vect(3, 4, 0);
            var element = LineBuilder.From(start).To(end).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_line));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(5)); // length = 5
            Assert.That(element.Locals.Count, Is.EqualTo(1));
        }

        [Test]
        public void Builder_FromDirectionAndLength_ProducesCorrectElement()
        {
            var start = new Vect(1, 2, 3);
            var dir = new Vect(0, 0, 1);
            var element = LineBuilder.From(start).WithDirection(dir).WithLength(10).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_line));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(10));
        }

        [Test]
        public void Builder_WithParams_NormalizesToZeroStart()
        {
            var start = new Vect(1, 0, 0);
            var dir = new Vect(1, 0, 0);
            var element = LineBuilder.From(start).WithDirection(dir).WithParams(5, 15).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(10));
            // Basis point should be shifted: 1 + 5*1 = 6
            Assert.That(element.Locals[0].Px, Is.EqualTo(6).Within(1e-6));
        }

        [Test]
        public void Builder_WithParams_InvertedFlipsDirection()
        {
            var start = new Vect(0, 0, 0);
            var dir = new Vect(1, 0, 0);
            var element = LineBuilder.From(start).WithDirection(dir).WithParams(10, 4).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(6));
            // Direction flipped
            Assert.That(element.Locals[0].Zx, Is.EqualTo(-1).Within(1e-6));
        }

        [Test]
        public void Builder_FromTwoPoints_ZeroLength_UsesDefaultDirection()
        {
            var start = new Vect(1, 1, 1);
            var element = LineBuilder.From(start).To(start).BuildElement();

            Assert.That(element.ElementDoubles[1], Is.EqualTo(0));
            // Falls back to (1,0,0) direction
            Assert.That(element.Locals[0].Zx, Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Builder_NullDirection_Throws()
        {
            var start = new Vect(0, 0, 0);
            Assert.That(() => LineBuilder.From(start).WithDirection(new Vect(0, 0, 0)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_WrapsLineCorrectly()
        {
            var element = LineBuilder.From(new Vect(0, 0, 0)).To(new Vect(5, 0, 0)).BuildElement();
            var adapter = new GimLineCurve(element);

            Assert.That(adapter.Length, Is.EqualTo(5));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(5));
            Assert.That(adapter.IsClosed, Is.False);
            Assert.That(adapter.IsDegenerate(), Is.False);
        }

        [Test]
        public void Adapter_WrongType_ThrowsExpectedTypeException()
        {
            var element = new GimElement { ElementType = GimElementType.C_circle };
            Assert.That(() => new GimLineCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_DegenerateLine_IsIdentified()
        {
            var element = LineBuilder.From(new Vect(1, 2, 3)).To(new Vect(1, 2, 3)).BuildElement();
            var adapter = new GimLineCurve(element);

            Assert.That(adapter.Length, Is.EqualTo(0));
            Assert.That(adapter.IsDegenerate(), Is.True);
        }

        [Test]
        public void Builder_BuildAdapter_Roundtrip()
        {
            var adapter = new GimLineCurve(LineBuilder.From(new Vect(0, 0, 0)).To(new Vect(3, 4, 0)).BuildElement());
            Assert.That(adapter.Length, Is.EqualTo(5));
            Assert.That(adapter.IsDegenerate(), Is.False);
        }
    }
}
