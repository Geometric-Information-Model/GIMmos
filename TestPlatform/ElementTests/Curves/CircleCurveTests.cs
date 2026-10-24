using NUnit.Framework;
using GIMcompress;
using GIMcompress.ElementHelpers;
using System;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class CircleCurveTests
    {
        [Test]
        public void Builder_FullCircle_WithDirectionXAndY()
        {
            var element = CircleBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadius(5).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_circle));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(2 * Math.PI));
        }

        [Test]
        public void Builder_FullCircle_WithDirectionXAndAxis()
        {
            var element = CircleBuilder.FromCenter(new Vect(1, 2, 3))
                .WithRadius(3).WithDirectionX(new Vect(1, 0, 0)).WithAxis(new Vect(0, 0, 1))
                .BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(3));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(2 * Math.PI));
        }

        [Test]
        public void Builder_ArcByAngleRange()
        {
            var element = CircleBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadius(4).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .WithAngleRange(0, Math.PI).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(4));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(Math.PI));
        }

        [Test]
        public void Builder_TotalAngle_NormalizedTo2Pi()
        {
            var element = CircleBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadius(1).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .WithTotalAngle(4 * Math.PI).BuildElement();

            Assert.That(element.ElementDoubles[2], Is.EqualTo(2 * Math.PI));
        }

        [Test]
        public void Builder_NegativeRadius_Throws()
        {
            Assert.That(() => CircleBuilder.FromCenter(new Vect(0, 0, 0)).WithRadius(-1),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_NullDirectionX_Throws()
        {
            Assert.That(() => CircleBuilder.FromCenter(new Vect(0, 0, 0)).WithRadius(1).WithDirectionX(new Vect(0, 0, 0)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_From3Points_ProducesCorrectCircle()
        {
            var element = CircleBuilder.From3Points(
                new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(-1, 0, 0))
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_circle));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(1).Within(1e-4));
        }

        [Test]
        public void Builder_From3Points_Collinear_Throws()
        {
            Assert.That(() => CircleBuilder.From3Points(
                new Vect(1, 0, 0), new Vect(2, 0, 0), new Vect(3, 0, 0)),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_WrapsCircleCorrectly()
        {
            var element = CircleBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadius(3).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .WithAngleRange(0, Math.PI / 2).BuildElement();

            var adapter = new GimCircleCurve(element);
            Assert.That(adapter.Radius, Is.EqualTo(3));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(Math.PI / 2));
            Assert.That(adapter.IsClosed, Is.False);
        }

        [Test]
        public void Adapter_FullCircle_IsClosed()
        {
            var element = CircleBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadius(5).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .BuildElement();

            var adapter = new GimCircleCurve(element);
            Assert.That(adapter.IsClosed, Is.True);
            Assert.That(adapter.IsDegenerate(), Is.False);
        }

        [Test]
        public void Adapter_GetCenter_ReturnsCorrectPoint()
        {
            var element = CircleBuilder.FromCenter(new Vect(1, 2, 0))
                .WithRadius(1).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .BuildElement();

            var adapter = new GimCircleCurve(element);
            var center = adapter.GetCenter();
            Assert.That(center[0], Is.EqualTo(1).Within(1e-6));
            Assert.That(center[1], Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void Adapter_PointAt_EvaluatesCorrectly()
        {
            var element = CircleBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadius(2).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .WithAngleRange(0, Math.PI).BuildElement();

            var adapter = new GimCircleCurve(element);
            var pt = adapter.PointAt(Math.PI / 2);
            Assert.That(pt[0], Is.EqualTo(0).Within(1e-6));
            Assert.That(pt[1], Is.EqualTo(2).Within(1e-6));
        }

        [Test]
        public void Adapter_GetDirectionX_GetDirectionY_GetAxis()
        {
            var element = CircleBuilder.FromCenter(new Vect(0, 0, 0))
                .WithRadius(1).WithDirectionX(new Vect(1, 0, 0)).WithDirectionY(new Vect(0, 1, 0))
                .BuildElement();

            var adapter = new GimCircleCurve(element);
            Assert.That(adapter.GetDirectionX()[0], Is.EqualTo(1).Within(1e-6));
            Assert.That(adapter.GetDirectionY()[1], Is.EqualTo(1).Within(1e-6));
            Assert.That(adapter.GetAxis()[2], Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Adapter_WrongType_ThrowsExpectedTypeException()
        {
            var element = new GimElement { ElementType = GimElementType.C_line };
            Assert.That(() => new GimCircleCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
