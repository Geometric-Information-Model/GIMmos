using System;
using GimUtilities;

namespace GIMcompress
{
	// used primarily in matrix calculus. Still useful by itself occasionally.
	public static class MatrixHelpers
    {

        // inverse of a matrix

        public static double[][] Inverse(double[][] mat)
        {
            int height = GetMatrixHeight(mat);
            int width = GetMatrixWidth(mat);
            if (height != width) throw GimLogger.Error(new ArgumentException("Cannot Calculate Inverse of Non-Square Matrix (" + height + "*" + width + ")!"));
            double determinant = DeterminantRecursive(mat);

            if (determinant == 0) throw GimLogger.Error(new ArgumentException("Cannot calculate Inverse of a matrix whos determinant is null!"));

            // most straightforward formula for an inverse.
            return Scale(1/determinant, Adjugate(mat));

        }

        // adjugate of a matrix, used primarily in calculating the inverse

        public static double[][] Adjugate(double[][] mat)
        {
            return Transpose(Cofactor(mat));
        }

        // cofactor of a matrix, used primarily in calculating the inverse
        public static double[][] Cofactor(double[][] mat)
        {
            int height = GetMatrixHeight(mat);
            int width = GetMatrixWidth(mat);
            if (height != width) throw GimLogger.Error(new ArgumentException("Cannot Calculate Cofactor of Non-Square Matrix (" + height + "*" + width + ")!"));


            double[][] result = new double[height][];

            for(int i=0; i<height; i++)
            {
                result[i] = new double[width];
                for (int j=0; j<width; j++)
                {
                    result[i][j] = Math.Pow(-1, i+j)*DeterminantRecursive(GetSubMatrix(mat, i, j));
                }
            }

            return result;

        }

        // transpose of a matrix, i.e. diagonally flipped
        public static double[][] Transpose(double[][] mat)
        {
            int height = GetMatrixHeight(mat);
            int width = GetMatrixWidth(mat);

            double[][] result = new double[width][];

            // intializing the matrix to avoid null errors
            for (int i = 0; i < width; i++)
            {
                result[i] = new double[height];
            }

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    result[j][i] = mat[i][j];
                }
            }

            return result;
        }

        // determinant calculus:
        //  det(A) = A[0][0] * A[/0,/0] - A[1][0] * A[/1,/0] + ..  + (-1)^n * A[n] * A[/n,/0]
        //  
        //  where A[/n,/m] is the matrix A with line n, column m removed 

        public static double Determinant(double[][] mat)
        {
            int height = GetMatrixHeight(mat);
            int width = GetMatrixWidth(mat);
            if (height != width) throw GimLogger.Error(new ArgumentException("Cannot Calculate Determinant of Non-Square Matrix (" + height + "*" + width + ")!"));

            return DeterminantRecursive(mat);
        }

        // used by the above function. avoids asserting square matrix in each iteration, to optimize time

        private static double DeterminantRecursive(double[][] mat)
        {
            int height = GetMatrixHeight(mat);
            if (height == 1) return mat[0][0]; // matrix with one element
            if (height == 0) return 0;
            double determinant = 0;
            int sign = 1;

            for(int i=0; i<height ; i++) // the formula is det(A) = Sum of (-1^)^(i) * A[i][0] * Det(A[/i][/0])
            {
                if (mat[i][0] == 0)
                {
                    sign = sign * (-1); // the sign flips with each operation
                    continue; // ye olde optimisation,
                              // making sure we don't calculate determinants if they're multiplied by 0
                }
                determinant += sign * mat[i][0] * DeterminantRecursive(GetSubMatrix(mat, i, 0));
                sign = sign * (-1); // the sign flips with each operation
            }

            return determinant;
        }


        // used to return a matrix with 1 row and/or 1 column removed. this is used to calculate the determinant

        public static double[][] GetSubMatrix(double[][] mat, int row=-1, int column=-1)
        {
            // we assume the resulting matrix is the same size as the input one
            int height = GetMatrixHeight(mat);
            int width = GetMatrixWidth(mat);
            int resHeight = height;
            int resWidth = width;
            // row = -1 means we remove no rows. otherwise we remove a row => resulting height = height-1;
            if(row>=0 && row < height)
            {
                resHeight--;
            }
            // same as row, but for columns
            if(column>=0 && column < width)
            {
                resWidth--;
            }

            double[][] result = new double[resHeight][];

            int x = 0; // we used mat[i,j] for input, and result[x,y] for the output, since we don't count some lines/columns in.
            for(int i=0; i< height; i++)
            {
                if (i == row) continue;
                result[x] = new double[resWidth];
                int y = 0;
                for(int j=0; j<width; j++)
                {
                    if (j == column) continue;
                    result[x][y] = mat[i][j];
                    y++;
                }

                x++;
            }

            return result;
        }

        // matrix multiplication
        public static double[][] Multiply(double[][] matA, double[][] matB)
        {
            int heightA = GetMatrixHeight(matA), heightB = GetMatrixHeight(matB);
            int widthA = GetMatrixWidth(matA), widthB = GetMatrixWidth(matB);

            if(widthA != heightB) throw GimLogger.Error(new IndexOutOfRangeException("Cannot Multiply Matrix " + heightA + "*" + widthB + " with Matrix" + heightB + "*" + widthB + "!"));

            double[][] result = new double[heightA][];

            for(int i =0; i< heightA; i++)
            {
                result[i] = new double[widthB];
                for(int j=0; j < widthB; j++)
                {
                    double sum = 0;

                    for(int k =0; k<widthA; k++)
                    {
                        sum += matA[i][k] * matB[k][j];
                    }

                    result[i][j] = sum;

                }
            }

            return result;
        }

        // summing two matrices
        public static double[][] Sum(double[][] matA, double[][] matB)
        {
            AssertMatricesAreSameSize(matA, matB);

            int height = GetMatrixHeight(matA);
            int width = GetMatrixWidth(matA);
            double[][] result = new double[height][];
            for (int i = 0; i < height; i++)
            {
                result[i] = new double[width];
                for (int j = 0; j < width; j++)
                {
                    result[i][j] = matA[i][j] + matB[i][j];
                }
            }

            return result;
        }
        
        // subtracting two matrices
        public static double[][] Subtract(double[][] matA, double[][] matB)
        {
            AssertMatricesAreSameSize(matA, matB);

            int height = GetMatrixHeight(matA);
            int width = GetMatrixWidth(matA);
            double[][] result = new double[height][];
            for (int i = 0; i < height; i++)
            {
                result[i] = new double[width];
                for (int j = 0; j < width; j++)
                {
                    result[i][j] = matA[i][j] - matB[i][j];
                }
            }

            return result;
        }

        // negative matrix
        public static double[][] Neg(double[][] matA)
        {
            int height = GetMatrixHeight(matA);
            int width = GetMatrixWidth(matA);
            double[][] result = new double[height][];
            for (int i = 0; i < height; i++)
            {
                result[i] = new double[width];
                for (int j = 0; j < width; j++)
                {
                    result[i][j] = -matA[i][j];
                }
            }

            return result;
        }


        // scaling a matrix

        public static double[][] Scale(double a, double[][] matA)
        {
            int height = GetMatrixHeight(matA);
            int width = GetMatrixWidth(matA);
            double[][] result = new double[height][];
            for (int i = 0; i < height; i++)
            {
                result[i] = new double[width];
                for (int j = 0; j < width; j++)
                {
                    result[i][j] = a*matA[i][j];
                }
            }

            return result;
        }

        // comparing two matricies and adjusting for double imprecision

        public static bool Compare(double[][] matA, double[][] matB, double margin = 0.000001)
        {

            try
            {
                AssertMatricesAreSameSize(matA, matB);
            }catch(IndexOutOfRangeException) // if they're not the same size => error => not equal
            {
                return false;
            }

            int height = GetMatrixHeight(matA);
            int width = GetMatrixWidth(matA);
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    double diff = Math.Abs(matA[i][j] - matB[i][j]);
                    if (diff > margin) return false;
                }
            }

            return true;
        }

        // asserts two matrices are the same size. TODO: custom Exception for matrix/vector size
        public static void AssertMatricesAreSameSize(double[][] matA, double[][] matB)
        {
            int heightA = GetMatrixHeight(matA), heightB = GetMatrixHeight(matB);
            int widthA = GetMatrixWidth(matA), widthB = GetMatrixWidth(matB);
            if(heightA != heightB)throw GimLogger.Error(new IndexOutOfRangeException("Cannot perform operation on matrix of Height " + heightA + " with matrix of Height " + heightB));
            if (widthA != widthB) throw GimLogger.Error(new IndexOutOfRangeException("Cannot perform operation on matrix of Width " + widthA + " with matrix of Width " + widthB));
        }

        // height, width getters
        public static int GetMatrixWidth(double[][] mat)
        {
            AssertMatrixIsValid(mat);
            return mat[0].Length;
        }

        public static int GetMatrixHeight(double[][] mat)
        {
            AssertMatrixIsValid(mat);
            return mat.Length;
        }

        // a valid matrix must contain at least one element, and all the lines must be the same width
        public static void AssertMatrixIsValid(double[][] mat)
        {
            int width = mat[0].Length;
            for(int i=1; i < mat.Length; i++)
            {
                if(mat[i].Length != width)
                {
                    throw GimLogger.Error(new IndexOutOfRangeException("Invalid Matrix: line " + i + " is of width " + mat[i].Length + ", while previous lines are of width " + width + "!"));
                }
            }
        }

    }
}