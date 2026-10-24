using System;

namespace GIMtransfer.BrepToPoly
{
	/// <summary>
	/// Curve is a class for a curve in the range [paramFirst, paramLast].
	/// </summary>
	class Curve
	{
		Func<double, Vertex> internalCurveFunction;
		double paramFirst;
		double paramLast;
		int steps;
		double stepsize;

		public int Count { get => steps; }

		public double ParamFirst { get => paramFirst; }
		public double ParamLast { get => paramLast; }

		private Vertex[] internalStorageArray;

		// Indexer declaration
		public Vertex this[int index]
		{
			get
			{
				return GetVertexOrCalculateIfNotStored(index);
			}
		}

		private Vertex GetVertexOrCalculateIfNotStored(int index)
		{
			if (internalStorageArray == null)
			{
				internalStorageArray = new Vertex[Count];
			}
			if (internalStorageArray[index] == null)
			{
				internalStorageArray[index] = internalCurveFunction(stepsize * index);
			}
			return internalStorageArray[index];
		}

		public Curve(Func<double, Vertex> internalCurveFunction, double paramFirst, double paramLast, int steps)
		{
			this.internalCurveFunction = internalCurveFunction;
			this.paramFirst = paramFirst;
			this.paramLast = paramLast;
			this.steps = steps;
			stepsize = (paramFirst - paramLast) / steps;

			if (stepsize == 0)
				throw new ArgumentException();
		}
	}
}
