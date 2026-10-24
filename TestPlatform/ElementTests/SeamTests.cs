using GIMcompress;
using NUnit.Framework;

namespace TestPlatform.ElementTests
{
    [TestFixture]
    public sealed class SeamTests
    {
        [Test]
        public void MarkCurveAsSeam_AddsSeamChild()
        {
            var gimData = new GimData();
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root });
            gimData.Elements.Add(new GimElement
            {
                ElementType = GimElementType.C_line,
                ElementDoubles = [0, 1],
                Locals = [new GimLocal()]
            });

            gimData.MarkCurveAsSeam(1);

            Assert.That(gimData.Elements.Count, Is.EqualTo(3));
            GimElement seam = gimData.Elements[2];
            Assert.That(seam.ElementType, Is.EqualTo(GimElementType.Seam));
            Assert.That(seam.ElementDoubles, Is.Null.Or.Empty);
            Assert.That(gimData.Elements[1].Elements, Does.Contain(2u));
            Assert.That(seam.Locals[0].parent, Is.EqualTo(1u));
        }

        [Test]
        public void MarkCurveAsSeam_IsIdempotent()
        {
            var gimData = new GimData();
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root });
            gimData.Elements.Add(new GimElement
            {
                ElementType = GimElementType.C_line,
                ElementDoubles = [0, 1],
                Locals = [new GimLocal()]
            });

            gimData.MarkCurveAsSeam(1);
            gimData.MarkCurveAsSeam(1);

            Assert.That(gimData.Elements.Count, Is.EqualTo(3));
            Assert.That(gimData.Elements[1].Elements.Count, Is.EqualTo(1));
        }

        [Test]
        public void Seam_TypeContracts()
        {
            Assert.That(GimElementType.Seam.IsSeam(), Is.True);
            Assert.That(GimElementType.Seam.GetDoubleCount(), Is.EqualTo(0));
            Assert.That(GimElementType.Seam.IsCurve(), Is.False);
            Assert.That(GimElementType.Seam.Is2DType(), Is.False);
            Assert.That(GimElementType.C_line2D.IsSeam(), Is.False);
        }
    }
}
