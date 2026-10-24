using NUnit.Framework;
using GIMcompress;
using GIMcompress.ElementHelpers;
using System;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class EllipseCurveTests
    {
        [Test]
        public void Builder_FullEllipse_WithDirections()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(5, 3).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0)).BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_elipse));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(3));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(2 * Math.PI));
        }

        [Test]
        public void Builder_FullEllipse_WithDirectionAndAxis()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(5, 3).WithDirectionAndAxis(new Vect(1, 0, 0), new Vect(0, 0, 1)).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(3));
        }

        [Test]
        public void Builder_SwapsRadiiWhenInverted()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(3, 5).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0)).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(3));
        }

        [Test]
        public void Builder_AngleRange_EndNormalizedUpward()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(3, 2).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithAngleRange(Math.PI, Math.PI / 2).BuildElement();

            Assert.That(element.ElementDoubles[3], Is.EqualTo(Math.PI / 2 + 2 * Math.PI));
        }

        [Test]
        public void Builder_NullDirections_Throws()
        {
            Assert.That(() => EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(1, 1).WithDirections(new Vect(0, 0, 0), new Vect(1, 0, 0)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_CollinearDirections_Throws()
        {
            Assert.That(() => EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(1, 1).WithDirections(new Vect(1, 0, 0), new Vect(2, 0, 0)).BuildElement(),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_TotalAngle_ProducesCorrectParams()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(2, 1).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithTotalAngle(Math.PI).BuildElement();

            Assert.That(element.ElementDoubles[3], Is.EqualTo(Math.PI));
        }

        [Test]
        public void Adapter_IsClosed_FullEllipse()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(4, 2).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0)).BuildElement();

            var adapter = new GimEllipseCurve(element);
            Assert.That(adapter.IsClosed, Is.True);
            Assert.That(adapter.IsDegenerate(), Is.False);
        }

        [Test]
        public void Adapter_IsNotClosed_Arc()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(3, 2).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0))
                .WithAngleRange(0, Math.PI).BuildElement();

            var adapter = new GimEllipseCurve(element);
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_GetCenter_ReturnsCorrectPoint()
        {
            var element = EllipseBuilder.FromCenter(new Vect(1, 2, 0))
                .WithRadii(2, 1).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0)).BuildElement();

            var adapter = new GimEllipseCurve(element);
            var center = adapter.GetCenter();
            Assert.That(center[0], Is.EqualTo(1).Within(1e-6));
            Assert.That(center[1], Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void Adapter_GetDirectionX_GetDirectionY_GetAxis()
        {
            var element = EllipseBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadii(1, 1).WithDirections(new Vect(1, 0, 0), new Vect(0, 1, 0)).BuildElement();

            var adapter = new GimEllipseCurve(element);
            Assert.That(adapter.GetDirectionX()[0], Is.EqualTo(1).Within(1e-6));
            Assert.That(adapter.GetDirectionY()[1], Is.EqualTo(1).Within(1e-6));
            Assert.That(adapter.GetAxis()[2], Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_circle };
            Assert.That(() => new GimEllipseCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
