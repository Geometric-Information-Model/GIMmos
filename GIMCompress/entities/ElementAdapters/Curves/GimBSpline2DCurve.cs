using GimUtilities;
using System;

namespace GIMcompress.ElementHelpers
{
    public class GimBSpline2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_bspline2D;

        public int PointCount { get; private set; }

        public int KnotCount => element.ElementInts[0];

        public int Degree => element.ElementInts[^1];

        public bool Periodic { get; private set; }

        public readonly ArraySegment<double> StartPoint;
        public readonly ArraySegment<double> EndPoint;
        public readonly ArraySegment<double> PointCoords;
        public readonly ArraySegment<double> Weights;
        public readonly ArraySegment<double> Knots;
        public readonly ArraySegment<int> Multiplicities;

        /// <inheritdoc/>
        public override double ParamFirst => Knots[Degree];

        /// <inheritdoc/>
        public override double ParamLast => Knots[KnotCount - Degree - 1];

        /// <inheritdoc/>
        public override bool IsClosed =>
            Periodic || (GetStartPoint() == GetEndPoint() && !IsDegenerate());

        /// <inheritdoc/>
        public override bool IsDegenerate(int localIndex = 0)
        {
            Vect s = GetStartPoint(localIndex);
            for (int i = 0; i < PointCount; i++)
            {
                var p = new Vect(PointCoords[i * 2], PointCoords[i * 2 + 1]);
                if (p != s) return false;
            }
            return true;
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) =>
            new(StartPoint[0], StartPoint[1]);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) =>
            new(EndPoint[0], EndPoint[1]);

        public GimBSpline2DCurve(GimElement bsplineElement) : base(bsplineElement)
        {
            int multiplicitiesCount = element.ElementInts.Length - 2;
            PointCount = (element.ElementDoubles.Length - 4 - KnotCount) / 3;

            int pointsEnd = PointCount * 2 + 4;
            PointCoords = new(element.ElementDoubles, 4, PointCount * 2);

            int knotsEnd = pointsEnd + KnotCount;
            Knots = new(element.ElementDoubles, pointsEnd, KnotCount);

            int weightsEnd = knotsEnd + PointCount;
            Weights = new(element.ElementDoubles, knotsEnd, PointCount);

            StartPoint = new(element.ElementDoubles, 0, 2);
            EndPoint = new(element.ElementDoubles, 2, 2);

            Multiplicities = new(element.ElementInts, 1, multiplicitiesCount);

            int multSum = 0;
            for (int i = 0; i < Multiplicities.Count; i++)
                multSum += Multiplicities[i];

            int aperiodicSum = multSum - Degree - 1;
            int periodicSum = multSum - Multiplicities[0];

            bool aperiodic = aperiodicSum == PointCount;
            Periodic = aperiodic ? false : periodicSum == PointCount;

            if (Periodic == aperiodic)
                throw GimLogger.Error(new InvalidGimElementException("Bspline2D curve is neither periodic nor aperiodic!"));
        }
    }
}
