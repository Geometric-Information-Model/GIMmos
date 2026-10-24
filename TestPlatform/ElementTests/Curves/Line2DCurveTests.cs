using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Line2DCurveTests
    {
        [Test]
        public void Builder_FromTwoPoints_ProducesCorrectElement()
        {
            var element = Line2DBuilder.From(0, 0).To(3, 4).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_line2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(5));
            Assert.That(element.Locals.Count, Is.EqualTo(1));
            Assert.That(element.Locals[0].Pz, Is.EqualTo(0));
            Assert.That(element.Locals[0].Zz, Is.EqualTo(0));
        }

        [Test]
        public void Builder_FromDirectionAndLength_ProducesCorrectElement()
        {
            var element = Line2DBuilder.From(1, 2).WithDirection(0, 1).WithLength(10).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_line2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(10));
        }

        [Test]
        public void Builder_WithParams_NormalizesToZeroStart()
        {
            var element = Line2DBuilder.From(1, 0).WithDirection(1, 0).WithParams(5, 15).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(10));
            Assert.That(element.Locals[0].Px, Is.EqualTo(6).Within(1e-6));
        }

        [Test]
        public void Builder_WithParams_InvertedFlipsDirection()
        {
            var element = Line2DBuilder.From(0, 0).WithDirection(1, 0).WithParams(10, 4).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(6));
            Assert.That(element.Locals[0].Zx, Is.EqualTo(-1).Within(1e-6));
        }

        [Test]
        public void Builder_FromTwoPoints_ZeroLength_UsesDefaultDirection()
        {
            var element = Line2DBuilder.From(1, 1).To(1, 1).BuildElement();

            Assert.That(element.ElementDoubles[1], Is.EqualTo(0));
            Assert.That(element.Locals[0].Zx, Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Builder_NullDirection_Throws()
        {
            Assert.That(() => Line2DBuilder.From(0, 0).WithDirection(0, 0),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_3DPoint_Throws()
        {
            Assert.That(() => Line2DBuilder.From(new Vect(0, 0, 0)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_WrapsLineCorrectly()
        {
            var element = Line2DBuilder.From(0, 0).To(5, 0).BuildElement();
            var adapter = new GimLine2DCurve(element);

            Assert.That(adapter.Length, Is.EqualTo(5));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(5));
            Assert.That(adapter.IsClosed, Is.False);
            Assert.That(adapter.IsDegenerate(), Is.False);
            Assert.That(adapter.GetStartPoint().Length, Is.EqualTo(2));
            Assert.That(adapter.GetEndPoint(), Is.EqualTo(new Vect(5, 0)));
        }

        [Test]
        public void Adapter_WrongType_ThrowsExpectedTypeException()
        {
            var element = new GimElement { ElementType = GimElementType.C_circle2D };
            Assert.That(() => new GimLine2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_DegenerateLine_IsIdentified()
        {
            var element = Line2DBuilder.From(1, 2).To(1, 2).BuildElement();
            var adapter = new GimLine2DCurve(element);

            Assert.That(adapter.Length, Is.EqualTo(0));
            Assert.That(adapter.IsDegenerate(), Is.True);
        }

        [Test]
        public void Builder_BuildAdapter_Roundtrip()
        {
            var adapter = new GimLine2DCurve(Line2DBuilder.From(0, 0).To(3, 4).BuildElement());
            Assert.That(adapter.Length, Is.EqualTo(5));
            Assert.That(adapter.IsDegenerate(), Is.False);
        }
    }
}
