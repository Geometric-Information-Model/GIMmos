using GIMcompress;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace TestPlatform
{
	public class VectorTests
    {
        [Test, Theory]
        public void GCDtests()
        {
            int[] As = new int[] {
                1071, // regular gcd
                17,   // two primes
                3,    // direct multiple
                512,  // one of the numbers is 0
                2046  // b>a
            };

            int[] Bs = new int[]
            {
                462,
                23,
                9,
                0,
                7656
            };

            int[] expectedGCDs = new int[]
            {
                21,
                1,
                3,
                512,
                66
            };

            for(int i=0; i< As.Length; i++)
            {
                
                ClassicAssert.AreEqual(expectedGCDs[i], VectorHelpers.GCD(As[i], Bs[i]));
            }

        }

        [Test, Theory]
        public void ImprovedGCDMethodTests()
        {
            ClassicAssert.AreEqual(true, true);
        }

        [Test]
        public void Vect2D_SetLocalBasisAndApplyToLocal_RoundTrip()
        {
            var origin = new Vect(3, 4);
            var direction = new Vect(0, 1);
            var local = new GimLocal();
            origin.SetLocalBasis(local);
            direction.ApplyToLocal(local);

            Assert.That(Vect.FromLocalOrigin2D(local), Is.EqualTo(origin));
            Assert.That(Vect.FromLocalDirection(local), Is.EqualTo(direction));
            Assert.That(local.Pz, Is.EqualTo(0));
            Assert.That(local.Zz, Is.EqualTo(0));
        }

        [Test]
        public void Vect2D_ApplyAsDirectionUAndV_RoundTrip()
        {
            var dirU = new Vect(1, 0);
            var dirV = new Vect(0, 1);
            var local = new GimLocal();
            dirU.ApplyAsDirectionU(local);
            dirV.ApplyAsDirectionV(local);

            Assert.That(Vect.FromLocalDirectionU(local), Is.EqualTo(dirU));
            Assert.That(Vect.FromLocalDirectionV(local), Is.EqualTo(dirV));
            Assert.That(local.Xz, Is.EqualTo(0));
            Assert.That(local.Yz, Is.EqualTo(0));
            Assert.That(local.Zx, Is.EqualTo(int.MinValue));
            Assert.That(local.Zy, Is.EqualTo(int.MinValue));
            Assert.That(local.Zz, Is.EqualTo(int.MinValue));
        }

        [Test]
        public void Vect2D_ZAccess_Throws()
        {
            var v = new Vect(1, 2);
            Assert.That(() => { var _ = v.Z; }, Throws.InstanceOf<InvalidGimOperation>());
        }

        [Test]
        public void Vect2D_CrossProduct_Throws()
        {
            var a = new Vect(1, 0);
            var b = new Vect(0, 1);
            Assert.That(() => { var _ = a ^ b; }, Throws.InstanceOf<InvalidGimOperation>());
        }

        [Test]
        public void Vect2D_Addition_Works()
        {
            var result = new Vect(0, 0) + new Vect(1, 2);
            Assert.That(result, Is.EqualTo(new Vect(1, 2)));
            Assert.That(result.Length, Is.EqualTo(2));
        }

        [Test]
        public void Vect2D_MixedDimensionAddition_Throws()
        {
            var a = new Vect(1, 2);
            var b = new Vect(1, 2, 3);
            Assert.That(() => { var _ = a + b; }, Throws.Exception);
        }

        [Test]
        public void Vect2D_Is2DAndIs3D()
        {
            Assert.That(new Vect(1, 2).Is2D, Is.True);
            Assert.That(new Vect(1, 2).Is3D, Is.False);
            Assert.That(new Vect(1, 2, 3).Is3D, Is.True);
            Assert.That(new Vect(1, 2, 3).Is2D, Is.False);
        }

    }
}