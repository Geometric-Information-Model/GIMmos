namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_offset2D elements.
    /// layout: elementDoubles[0]=offset value.
    /// Direction from local Zx, Zy.
    /// </summary>
    public class GimOffset2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_offset2D;

        public double Offset => element.ElementDoubles[0];

        /// <inheritdoc/>
        public override double ParamFirst => 0;

        /// <inheritdoc/>
        public override double ParamLast => 0;

        /// <inheritdoc/>
        public override bool IsClosed => false;

        public GimOffset2DCurve(GimElement offsetElement) : base(offsetElement) { }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            AssertElementHasLocals();
            return Vect.FromLocalOrigin2D(element.Locals[localIndex]);
        }

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0)
        {
            AssertElementHasLocals();
            return GetStartPoint(localIndex) + Offset * GetDirection(localIndex);
        }

        public Vect GetDirection(int localIndex = 0)
        {
            AssertElementHasLocals();
            return Vect.FromLocalDirection(element.Locals[localIndex]);
        }
    }
}
