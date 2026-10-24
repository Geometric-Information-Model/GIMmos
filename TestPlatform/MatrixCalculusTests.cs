using GIMcompress;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace TestPlatform
{
	class MatrixCalculusTests
    {
        [Test, Theory]
        public void VectorAddition()
        {
            Vect A = new double[]{1,2,0};
            Vect B = new Vect(0,0,3);

            Vect Res = A + B;
            Vect Expected = new double[] { 1, 2, 3 };

            ClassicAssert.IsTrue(Expected == Res);
        }

        [Test, Theory]
        public void VectorScalar()
        {
            Vect A = new double[] { 1, 2, 0 };
            Vect B = new Vect(0, 0, 3);

            Vect Res = A + 2*B;
            Vect Expected = new double[] { 1, 2, 6 };

            ClassicAssert.IsTrue(Expected == Res);
        }

        [Test, Theory]
        public void VectorMultipleOperations()
        {
            Vect A = new Vect(1, 0, 0);
            Vect B = new Vect(new double[] { 1, 1, 0 });
            Vect C = A ^ (B-A); // cross prod, should be 0,0,1;

            Vect Res = A + 2 * (B - A) + 3 * C;
            Vect Expected = new double[] { 1, 2, 3 };

            ClassicAssert.IsTrue(Expected == Res);
        }

        [Test, Theory]
        public void VectorInequalityTest()
        {
            Vect A = new double[] { 1, 2, 0 };
            Vect B = new Vect(0, 0, 3);

            Vect Res = A + B;
            Vect NotExpected = new double[] { 2, 2, 3 };

            ClassicAssert.IsTrue(NotExpected != Res);
        }

        [Test, Theory]
        public void MatrixEqualityTest()
        {
            Matrix A = new Matrix(new Vect(1, 0, 0), new Vect(0, 2, 0), new Vect(0, 0, 3));
            Matrix B = new Matrix(new double[][] {
                new double[]{1,0,0},
                new double[]{0,2,0},
                new double[]{0,0,3}
            });

            ClassicAssert.IsTrue(A == B);
        }

        [Test, Theory]
        public void MatrixInequalityTest()
        {
            Matrix A = new Matrix(new Vect(1, 0, 0), new Vect(0, 2, 0), new Vect(0, 0, 3));
            Matrix B = new Matrix(new double[][] {
                new double[]{1,0,0},
                new double[]{0,2,0},
                new double[]{0,0,1}
            });

            ClassicAssert.IsTrue(!(A == B));
            ClassicAssert.IsTrue(A != B);
        }

        [Test, Theory]
        public void MatrixAdditionTest()
        {
            Matrix A = new Matrix(new Vect(1, 1, 3), new Vect(3, 4, 2), new Vect(2, 18, 3));
            Matrix B = new Matrix(new double[][] {
                new double[]{0,1,0},
                new double[]{1,1,4},
                new double[]{5,-10,6}
            });

            Matrix Expected = new Matrix(new double[][] {
                new double[]{1,2,3},
                new double[]{4,5,6},
                new double[]{7,8,9}
            });

            Matrix Res = A + B;

            ClassicAssert.IsTrue(Res == Expected);
        }

        [Test, Theory]
        public void MatrixMultiplicationTest()
        {
            Matrix A = new Matrix(new Vect(1, 0, -1));
            Matrix B = new Matrix(new double[][] {
                new double[]{1},
                new double[]{0},
                new double[]{-1}
            });

            Matrix Expected = new Matrix(new double[][] {
                new double[]{1,0,-1},
                new double[]{0,0,0},
                new double[]{-1,0,1}
            });

            Matrix Res = B * A;

            ClassicAssert.IsTrue(Res == Expected);
        }

        [Test, Theory]
        public void MatrixDeterminantTest1()
        {
            Matrix A = new Matrix(
                new Vect(1, 1, 1),
                new Vect(4, 1, 1),
                new Vect(0, 0, 1)
            );

            double determinant = A.Determinant;

            double expected = -3;

            ClassicAssert.IsTrue(expected == determinant);

        }

        [Test, Theory]
        public void MatrixDeterminantTest2()
        {
            Matrix A = new Matrix(
                new Vect(1, 3, 1),
                new Vect(4, 1, 1),
                new Vect(0, 0, 2)
            );

            double determinant = A.Determinant;

            double expected = -22;

            ClassicAssert.IsTrue(expected == determinant);

        }

        [Test, Theory]
        public void MatrixInverseTest1()
        {
            Matrix A = new Matrix(
                new Vect(1, 1, 1),
                new Vect(0, 1, 1),
                new Vect(0, 0, 1)
            );

            Matrix Inverse = A.Inverse;

            Matrix Expected = new Matrix(
                new Vect(1, -1, 0),
                new Vect(0,  1, -1),
                new Vect(0,  0, 1)
            );

            ClassicAssert.IsTrue(Expected == Inverse);

        }
    }
}
