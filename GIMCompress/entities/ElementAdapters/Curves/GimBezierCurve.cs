using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_bezier elements.
    /// layout: elementDoubles[0..5]=start/end XYZ, [6..]=pole coords (x3 each), [..]=paramFirst, [..]=paramLast.
    /// elementInts[0]=poleCount.
    /// </summary>
    public class GimBezierCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_bezier;

        /// <summary>
        /// Number of control points (poles).
        /// </summary>
        public int PoleCount
        {
            get => element.ElementInts[0];
        }

        /// <summary>
        /// Start point as a 3-double ArraySegment.
        /// </summary>
        public readonly ArraySegment<double> StartPoint;
        /// <summary>
        /// End point as a 3-double ArraySegment.
        /// </summary>
        public readonly ArraySegment<double> EndPoint;
        /// <summary>
        /// All pole coordinates (x, y, z interleaved), starting at index 6.
        /// </summary>
        public readonly ArraySegment<double> PoleCoords;

        /// <inheritdoc/>
        public override double ParamFirst
        {
            get => element.ElementDoubles[^2];
        }

        /// <inheritdoc/>
        public override double ParamLast
        {
            get => element.ElementDoubles[^1];
        }

        /// <inheritdoc/>
        /// <remarks>
        /// A bezier is closed when start and end points coincide AND at least one control point differs from them.
        /// A closed bezier is NOT degenerate. A bezier where all points coincide is degenerate.
        /// </remarks>
        public override bool IsClosed
        {
            get
            {
                Vect s = GetStartPoint();
                Vect e = GetEndPoint();
                if (s != e) return false;
                // Check if any pole differs from start/end
                for (int i = 0; i < PoleCount; i++)
                {
                    var p = new Vect(PoleCoords[i * 3], PoleCoords[i * 3 + 1], PoleCoords[i * 3 + 2]);
                    if (p != s) return true;
                }
                return false; // All points coincide — not closed, just degenerate
            }
        }

        /// <inheritdoc/>
        /// <remarks>A bezier is degenerate when all control points coincide with start/end.</remarks>
        public override bool IsDegenerate(int localIndex = 0)
        {
            Vect s = GetStartPoint(localIndex);
            for (int i = 0; i < PoleCount; i++)
            {
                var p = new Vect(PoleCoords[i * 3], PoleCoords[i * 3 + 1], PoleCoords[i * 3 + 2]);
                if (p != s) return false;
            }
            return true;
        }

        /// <summary>
        /// Wraps the given bezier element.
        /// </summary>
        public GimBezierCurve(GimElement bezierElement) : base(bezierElement)
        {
            int poleEnd = 6 + PoleCount * 3;
            PoleCoords = new(element.ElementDoubles, 6, PoleCount * 3);
            StartPoint = new(element.ElementDoubles, 0, 3);
            EndPoint = new(element.ElementDoubles, 3, 3);
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            return new Vect(StartPoint[0], StartPoint[1], StartPoint[2]);
        }

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0)
        {
            return new Vect(EndPoint[0], EndPoint[1], EndPoint[2]);
        }
    }
}
