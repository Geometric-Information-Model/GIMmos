using GimUtilities;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Intermediate base class for all 3D curve adapters.
    /// Provides a uniform contract for parametric range, start/end points, and closed/degenerate checks.
    /// </summary>
    public abstract class AbstractCurveAdapter : AbstractElementAdapter
    {
        /// <summary>
        /// Wraps the given curve element.
        /// </summary>
        /// <param name="element">The curve GimElement to wrap.</param>
        protected AbstractCurveAdapter(GimElement element) : base(element) { }

        /// <summary>
        /// The first parameter of the curve's parametric domain.
        /// </summary>
        public abstract double ParamFirst { get; }

        /// <summary>
        /// The last parameter of the curve's parametric domain.
        /// </summary>
        public abstract double ParamLast { get; }

        /// <summary>
        /// The 3D start point of the curve for the given instance.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>The 3D start point.</returns>
        public abstract Vect GetStartPoint(int localIndex = 0);

        /// <summary>
        /// The 3D end point of the curve for the given instance.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>The 3D end point.</returns>
        public abstract Vect GetEndPoint(int localIndex = 0);

        /// <summary>
        /// Whether the curve is closed (start connects to end).
        /// Types that can never be closed return false.
        /// </summary>
        public abstract bool IsClosed { get; }

        /// <summary>
        /// Returns both start and end points as a tuple.
        /// Replaces the legacy CurveHelpers.getStartAndEndPointsOfCurve.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>A tuple (Start, End) of the two 3D points.</returns>
        public (Vect Start, Vect End) GetStartAndEndPoints(int localIndex = 0) =>
            (GetStartPoint(localIndex), GetEndPoint(localIndex));

        /// <summary>
        /// Whether the curve is degenerate (zero-length).
        /// For open curves: start == end within Vect tolerance (EQUALITY_MARGIN 1e-5).
        /// For closed curves: override to additionally check control point coincidence.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>True if the curve has no meaningful extent.</returns>
        public virtual bool IsDegenerate(int localIndex = 0) =>
            !IsClosed && GetStartPoint(localIndex) == GetEndPoint(localIndex);
    }
}
