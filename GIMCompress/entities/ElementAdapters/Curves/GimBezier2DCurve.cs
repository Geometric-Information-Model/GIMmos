using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_bezier2D elements.
    /// layout: elementDoubles[0..3]=start/end UV, [4..]=pole coords (x2 each), [..]=paramFirst, [..]=paramLast.
    /// elementInts[0]=poleCount.
    /// </summary>
    public class GimBezier2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_bezier2D;

        public int PoleCount => element.ElementInts[0];

        public readonly ArraySegment<double> StartPoint;
        public readonly ArraySegment<double> EndPoint;
        public readonly ArraySegment<double> PointCoords;

        /// <inheritdoc/>
        public override double ParamFirst => element.ElementDoubles[^2];

        /// <inheritdoc/>
        public override double ParamLast => element.ElementDoubles[^1];

        /// <inheritdoc/>
        public override bool IsClosed =>
            GetStartPoint() == GetEndPoint() && !IsDegenerate();

        /// <inheritdoc/>
        public override bool IsDegenerate(int localIndex = 0)
        {
            Vect s = GetStartPoint(localIndex);
            for (int i = 0; i < PoleCount; i++)
            {
                var p = new Vect(PointCoords[i * 2], PointCoords[i * 2 + 1]);
                if (p != s) return false;
            }
            return true;
        }

        public GimBezier2DCurve(GimElement bezierElement) : base(bezierElement)
        {
            PointCoords = new(element.ElementDoubles, 4, PoleCount * 2);
            StartPoint = new(element.ElementDoubles, 0, 2);
            EndPoint = new(element.ElementDoubles, 2, 2);
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) =>
            new(StartPoint[0], StartPoint[1]);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) =>
            new(EndPoint[0], EndPoint[1]);
    }
}
