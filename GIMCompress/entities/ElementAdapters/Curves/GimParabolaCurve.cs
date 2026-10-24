namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_parabol elements.
    /// layout: elementDoubles[0]=paramFirst, [1]=paramLast, [2]=focal.
    /// CAD definition: C(t) = P + t^2/(4F)*X + t*Y, where X = symmetry axis, Y = parallel direction.
    /// </summary>
    public class GimParabolaCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_parabol;

        /// <summary>
        /// The focal length of the parabola.
        /// </summary>
        public double Focal
        {
            get => element.ElementDoubles[2];
        }

        /// <inheritdoc/>
        public override double ParamFirst
        {
            get => element.ElementDoubles[0];
        }

        /// <inheritdoc/>
        public override double ParamLast
        {
            get => element.ElementDoubles[1];
        }

        /// <inheritdoc/>
        public override bool IsClosed => false;

        /// <summary>
        /// Wraps the given parabola element.
        /// </summary>
        public GimParabolaCurve(GimElement parabolaElement) : base(parabolaElement) { }

        public Vect GetCenter(int localIndex = 0)
        {
            AssertElementHasLocals();
            return element.Locals[localIndex].BasisPoint;
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            return EvaluateAt(ParamFirst, localIndex);
        }

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0)
        {
            return EvaluateAt(ParamLast, localIndex);
        }

        /// <summary>
        /// Evaluates the parabola at parameter t.
        /// C(t) = P + t^2/(4F)*X + t*Y
        /// </summary>
        public Vect EvaluateAt(double t, int localIndex = 0)
        {
            Vect P = GetCenter(localIndex);
            Vect X = GetSymmetryAxis(localIndex);
            Vect Y = GetParallelDirection(localIndex);
            return P + (t * t / (4 * Focal)) * X + t * Y;
        }

        /// <summary>
        /// Gets the symmetry axis of the parabola. First row of the local matrix (X direction).
        /// </summary>
        public Vect GetSymmetryAxis(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[0];
        }

        /// <summary>
        /// Gets the parallel direction of the parabola. Second row of the local matrix (Y direction).
        /// </summary>
        public Vect GetParallelDirection(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[1];
        }
    }
}
