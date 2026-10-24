namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_trimmed elements.
    /// layout: elementDoubles[0]=paramFirst, [1]=paramLast.
    /// </summary>
    public class GimTrimmedCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_trimmed;

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
        /// Wraps the given trimmed curve element.
        /// </summary>
        public GimTrimmedCurve(GimElement trimmedElement) : base(trimmedElement) { }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            // Trimmed curve relies on its child basis curve for geometry
            throw new InvalidGimOperation("Trimmed curve delegates to its child basis curve for point evaluation.");
        }

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0)
        {
            throw new InvalidGimOperation("Trimmed curve delegates to its child basis curve for point evaluation.");
        }
    }
}
