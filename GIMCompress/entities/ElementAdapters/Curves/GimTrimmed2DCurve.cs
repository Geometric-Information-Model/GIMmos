namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_trimmed2D elements.
    /// layout: elementDoubles[0]=paramFirst, [1]=paramLast.
    /// </summary>
    public class GimTrimmed2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_trimmed2D;

        /// <inheritdoc/>
        public override double ParamFirst => element.ElementDoubles[0];

        /// <inheritdoc/>
        public override double ParamLast => element.ElementDoubles[1];

        /// <inheritdoc/>
        public override bool IsClosed => false;

        public GimTrimmed2DCurve(GimElement trimmedElement) : base(trimmedElement) { }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            throw new InvalidGimOperation("Trimmed2D curve delegates to its child basis curve for point evaluation.");
        }

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0)
        {
            throw new InvalidGimOperation("Trimmed2D curve delegates to its child basis curve for point evaluation.");
        }
    }
}
