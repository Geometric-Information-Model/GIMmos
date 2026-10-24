using GimUtilities;
using System;

namespace GIMcompress.ElementHelpers
{
    public class GimBSplineCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_bspline;

        #region single properties
        /// <summary>
        /// Number of control points (poles).
        /// </summary>
        public int PointCount
        {
            get; private set;
        }

        /// <summary>
        /// Number of knots.
        /// </summary>
        public int KnotCount
        {
            get { return element.ElementInts[0]; }
        }

        /// <summary>
        /// Degree of the bspline.
        /// </summary>
        public int Degree
        {
            get { return element.ElementInts[^1]; }
        }

        /// <summary>
        /// Whether the bspline is periodic.
        /// </summary>
        public bool Periodic
        {
            get; private set;
        }

        #endregion

        /// <summary>
        /// Start point as a 3-double ArraySegment.
        /// </summary>
        public readonly ArraySegment<double> StartPoint;
        /// <summary>
        /// End point as a 3-double ArraySegment.
        /// </summary>
        public readonly ArraySegment<double> EndPoint;
        /// <summary>
        /// All pole coordinates (x, y, z interleaved).
        /// </summary>
        public readonly ArraySegment<double> PointCoords;
        /// <summary>
        /// Pole weights.
        /// </summary>
        public readonly ArraySegment<double> Weights;
        /// <summary>
        /// Knot values.
        /// </summary>
        public readonly ArraySegment<double> Knots;
        /// <summary>
        /// Knot multiplicities.
        /// </summary>
        public readonly ArraySegment<int> Multiplicities;

        /// <inheritdoc/>
        public override double ParamFirst => Knots[Degree];

        /// <inheritdoc/>
        public override double ParamLast => Knots[KnotCount - Degree - 1];

        /// <inheritdoc/>
        /// <remarks>
        /// A bspline is closed when it is periodic, or when start and end points coincide
        /// AND at least one control point differs from them.
        /// A closed bspline is NOT degenerate. A bspline where all points coincide is degenerate.
        /// </remarks>
        public override bool IsClosed
        {
            get
            {
                if (Periodic) return true;
                Vect s = GetStartPoint();
                Vect e = GetEndPoint();
                if (s != e) return false;
                // Check if any pole differs from start/end
                for (int i = 0; i < PointCount; i++)
                {
                    var p = new Vect(PointCoords[i * 3], PointCoords[i * 3 + 1], PointCoords[i * 3 + 2]);
                    if (p != s) return true;
                }
                return false;
            }
        }

        /// <inheritdoc/>
        /// <remarks>A bspline is degenerate when all control points coincide with start/end.</remarks>
        public override bool IsDegenerate(int localIndex = 0)
        {
            Vect s = GetStartPoint(localIndex);
            for (int i = 0; i < PointCount; i++)
            {
                var p = new Vect(PointCoords[i * 3], PointCoords[i * 3 + 1], PointCoords[i * 3 + 2]);
                if (p != s) return false;
            }
            return true;
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

        /// <summary>
        /// Wraps the given bspline curve element.
        /// </summary>
        /// <param name="bsplineElement">The bspline GimElement to wrap.</param>
        public GimBSplineCurve(GimElement bsplineElement) : base(bsplineElement)
        {
            int multiplicitiesCount = element.ElementInts.Length - 2;
            PointCount = (element.ElementDoubles.Length - 6 - KnotCount) / 4;

            // Fetching points
            int pointsEnd = PointCount * 3 + 6;
            PointCoords = new(element.ElementDoubles, 6, PointCount * 3);
            // Fetching knots
            int knotsEnd = pointsEnd + KnotCount;
            Knots = new(element.ElementDoubles, pointsEnd, KnotCount);
            // Fetching weights
            int weightsEnd = knotsEnd + PointCount;
            Weights = new(element.ElementDoubles, knotsEnd, PointCount);

            // Start and End points
            StartPoint = new(element.ElementDoubles, 0, 3);
            EndPoint = new(element.ElementDoubles, 3, 3);

            // Fetching multiplicities
            Multiplicities = new(element.ElementInts, 1, multiplicitiesCount);

            // calculating periodicity
            int multSum = 0;
            for (int i = 0; i < Multiplicities.Count; i++)
                multSum += Multiplicities[i];

            int aperiodicSum = multSum - Degree - 1;
            int periodicSum = multSum - Multiplicities[0];

            bool aperiodic = (aperiodicSum == PointCount);
            if (aperiodic)
                Periodic = false;

            else
                Periodic = (periodicSum == PointCount);


            if (Periodic == aperiodic)
            {
                throw GimLogger.Error(new InvalidGimElementException("Bspline curve is neither periodic nor aperiodic!"));
            }
        }
    }
}
