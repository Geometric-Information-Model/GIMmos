namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Intermediate base class for all 2D curve adapters.
    /// Provides a uniform contract for parametric range, start/end UV points, and closed/degenerate checks.
    /// </summary>
    public abstract class AbstractCurve2DAdapter : AbstractElementAdapter
    {
        /// <summary>
        /// Wraps the given 2D curve element.
        /// </summary>
        /// <param name="element">The curve GimElement to wrap.</param>
        protected AbstractCurve2DAdapter(GimElement element) : base(element) { }

        /// <summary>
        /// The first parameter of the curve's parametric domain.
        /// </summary>
        public abstract double ParamFirst { get; }

        /// <summary>
        /// The last parameter of the curve's parametric domain.
        /// </summary>
        public abstract double ParamLast { get; }

        /// <summary>
        /// The 2D start point of the curve for the given instance.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>The 2D start point (Length == 2).</returns>
        public abstract Vect GetStartPoint(int localIndex = 0);

        /// <summary>
        /// The 2D end point of the curve for the given instance.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>The 2D end point (Length == 2).</returns>
        public abstract Vect GetEndPoint(int localIndex = 0);

        /// <summary>
        /// Whether the curve is closed (start connects to end).
        /// </summary>
        public abstract bool IsClosed { get; }

        /// <summary>
        /// Returns both start and end points as a tuple.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>A tuple (Start, End) of the two 2D points.</returns>
        public (Vect Start, Vect End) GetStartAndEndPoints(int localIndex = 0) =>
            (GetStartPoint(localIndex), GetEndPoint(localIndex));

        public Vect GetCenter(int localIndex = 0)
        {
            AssertElementHasLocals();
            return Vect.FromLocalOrigin2D(element.Locals[localIndex]);
        }

        public Vect GetDirectionU(int localIndex = 0)
        {
            AssertElementHasLocals();
            return Vect.FromLocalDirectionU(element.Locals[localIndex]);
        }

        public Vect GetDirectionV(int localIndex = 0)
        {
            AssertElementHasLocals();
            return Vect.FromLocalDirectionV(element.Locals[localIndex]);
        }

        /// <summary>
        /// Whether the curve is degenerate (zero-length).
        /// For open curves: start == end within Vect tolerance.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>True if the curve has no meaningful extent.</returns>
        public virtual bool IsDegenerate(int localIndex = 0) =>
            !IsClosed && GetStartPoint(localIndex) == GetEndPoint(localIndex);
    }
}
