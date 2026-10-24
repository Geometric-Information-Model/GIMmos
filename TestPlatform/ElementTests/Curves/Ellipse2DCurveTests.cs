using GIMcompress;
using GIMcompress.ElementHelpers;
using NUnit.Framework;
using System;

namespace TestPlatform.ElementTests.Curves
{
    [TestFixture]
    public sealed class Ellipse2DCurveTests
    {
        [Test]
        public void Builder_FullEllipse_WithDirectionUAndV()
        {
            var element = Ellipse2DBuilder.FromCenter(0, 0)
                .WithRadii(5, 3).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .BuildElement();

            Assert.That(element.ElementType, Is.EqualTo(GimElementType.C_elipse2D));
            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(3));
            Assert.That(element.ElementDoubles[2], Is.EqualTo(0));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(2 * Math.PI));
        }

        [Test]
        public void Builder_SwapsRadiiWhenInverted()
        {
            var element = Ellipse2DBuilder.FromCenter(0, 0)
                .WithRadii(3, 5).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .BuildElement();

            Assert.That(element.ElementDoubles[0], Is.EqualTo(5));
            Assert.That(element.ElementDoubles[1], Is.EqualTo(3));
        }

        [Test]
        public void Builder_AngleRange_StoredAsGiven()
        {
            var element = Ellipse2DBuilder.FromCenter(0, 0)
                .WithRadii(3, 2).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithAngleRange(Math.PI, Math.PI / 2).BuildElement();

            Assert.That(element.ElementDoubles[2], Is.EqualTo(Math.PI));
            Assert.That(element.ElementDoubles[3], Is.EqualTo(Math.PI / 2));
        }

        [Test]
        public void Builder_ColinearAxes_Throws()
        {
            Assert.That(() => Ellipse2DBuilder.FromCenter(0, 0).WithRadii(1, 1)
                .WithDirectionU(1, 0).WithDirectionV(2, 0).BuildElement(),
                Throws.InstanceOf<InvalidGimElementException>());
        }

        [Test]
        public void Adapter_Properties()
        {
            var element = Ellipse2DBuilder.FromCenter(1, 2)
                .WithRadii(5, 3).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .WithAngleRange(0, Math.PI / 2).BuildElement();
            var adapter = new GimEllipse2DCurve(element);

            Assert.That(adapter.MajorRadius, Is.EqualTo(5));
            Assert.That(adapter.MinorRadius, Is.EqualTo(3));
            Assert.That(adapter.ParamFirst, Is.EqualTo(0));
            Assert.That(adapter.ParamLast, Is.EqualTo(Math.PI / 2));
            Assert.That(adapter.IsClosed, Is.False);
            Assert.That(adapter.GetCenter(), Is.EqualTo(new Vect(1, 2)));
            Assert.That(adapter.GetDirectionU(), Is.EqualTo(new Vect(1, 0)));
            Assert.That(adapter.GetStartPoint(), Is.EqualTo(new Vect(6, 2)));
        }

        [Test]
        public void Adapter_FullEllipse_IsClosed()
        {
            var adapter = new GimEllipse2DCurve(Ellipse2DBuilder.FromCenter(0, 0)
                .WithRadii(2, 1).WithDirectionU(1, 0).WithDirectionV(0, 1)
                .BuildElement());

            Assert.That(adapter.IsClosed, Is.True);
            Assert.That(adapter.IsDegenerate(), Is.False);
        }

        [Test]
        public void Adapter_WrongType_Throws()
        {
            var element = new GimElement { ElementType = GimElementType.C_line2D };
            Assert.That(() => new GimEllipse2DCurve(element),
                Throws.InstanceOf<InvalidGimElementException>());
        }
    }
}
