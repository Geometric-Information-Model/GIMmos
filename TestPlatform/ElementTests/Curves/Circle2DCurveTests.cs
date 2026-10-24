using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;
using System;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Circle2DCurveTests
    {
        [Test]
        public void Builder_FullCircle_WithDirectionUAndV()
        {
            var element = Circle2DBuilder.FromCenter(0, 0)
                .WithRadius(5).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_circle2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(2 * Math.PI));
            Assert.That(element.Locals[0].Pz, Is.EqualTo(0));
            Assert.That(element.Locals[0].Xz, Is.EqualTo(0));
            Assert.That(element.Locals[0].Yz, Is.EqualTo(0));
            Assert.That(element.Locals[0].Zx, Is.EqualTo(int.MinValue));
            Assert.That(element.Locals[0].Zy, Is.EqualTo(int.MinValue));
            Assert.That(element.Locals[0].Zz, Is.EqualTo(int.MinValue));
        }

        [Test]
        public void Builder_ArcByAngleRange()
        {
            var element = Circle2DBuilder.FromCenter(0, 0)
                .WithRadius(4).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithAngleRange(0, Math.PI).BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(4));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(Math.PI));
        }

        [Test]
        public void Builder_TotalAngle_NormalizedTo2Pi()
        {
            var element = Circle2DBuilder.FromCenter(0, 0)
                .WithRadius(1).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithTotalAngle(4 * Math.PI).BuildElement();

            Assert.That(element.ElementDoubles[2], Is.EqualTo(2 * Math.PI));
        }

        [Test]
        public void Builder_NegativeRadius_Throws()
        {
            Assert.That(() => Circle2DBuilder.FromCenter(0, 0).WithRadius(-1),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_NullDirectionU_Throws()
        {
            Assert.That(() => Circle2DBuilder.FromCenter(0, 0).WithRadius(1).WithDirectionU(0, 0),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_ColinearAxes_Throws()
        {
            Assert.That(() => Circle2DBuilder.FromCenter(0, 0).WithRadius(1)
                .WithDirectionU(1, 0).WithDirectionV(2, 0),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_Properties()
        {
            var element = Circle2DBuilder.FromCenter(1, 2)
                .WithRadius(3).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithAngleRange(0, Math.PI / 2).BuildElement();
            var adapter = new GimCircle2DCurve(element);

            Assert.That(adapter.Radius, Is.EqualTo(3));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(Math.PI / 2));
            Assert.That(adapter.IsClosed, Is.False);
            Assert.That(adapter.GetCenter(), Is.EqualTo(new Vect(1, 2)));
            Assert.That(adapter.GetDirectionU(), Is.EqualTo(new Vect(1, 0)));
            Assert.That(adapter.GetStartPoint(), Is.EqualTo(new Vect(4, 2)));
            Assert.That(adapter.GetEndPoint().X, Is.EqualTo(1).Within(1e-5));
            Assert.That(adapter.GetEndPoint().Y, Is.EqualTo(5).Within(1e-5));
        }

        [Test]
        public void Adapter_FullCircle_IsClosed()
        {
            var element = Circle2DBuilder.FromCenter(0, 0)
                .WithRadius(2).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .BuildElement();
            var adapter = new GimCircle2DCurve(element);

            Assert.That(adapter.IsClosed, Is.True);
            Assert.That(adapter.IsDegenerate(), Is.False);
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimCircle2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Builder_BuildAdapter_Roundtrip()
        {
            var adapter = new GimCircle2DCurve(Circle2DBuilder.FromCenter(0, 0)
                .WithRadius(5).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .BuildElement());
            Assert.That(adapter.Radius, Is.EqualTo(5));
            Assert.That(adapter.IsClosed, Is.True);
        }
    }
}
