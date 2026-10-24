using GimUtilities;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_offset elements.
    /// layout: elementDoubles[0]=offset value.
    /// Direction from local Z axis.
    /// </summary>
    public class GimOffsetCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_offset;

        /// <summary>
        /// The offset value.
        /// </summary>
        public double Offset
        {
            get => element.ElementDoubles[0];
        }

        /// <inheritdoc/>
        public override double ParamFirst
        {
            get => 0; // Offset curve inherits param range from basis curve
        }

        /// <inheritdoc/>
        public override double ParamLast
        {
            get => 0; // Offset curve inherits param range from basis curve
        }

        /// <inheritdoc/>
        public override bool IsClosed => false;

        /// <summary>
        /// Wraps the given offset element.
        /// </summary>
        public GimOffsetCurve(GimElement offsetElement) : base(offsetElement) { }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            AssertElementHasLocals();
            return element.Locals[localIndex].BasisPoint;
        }

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0)
        {
            AssertElementHasLocals();
            var local = element.Locals[localIndex];
            Vect dir = new Vect(local.Zx, local.Zy, local.Zz);
            return local.BasisPoint + Offset * dir;
        }

        /// <summary>
        /// Gets the direction of the offset.
        /// </summary>
        public Vect GetDirection(int localIndex = 0)
        {
            AssertElementHasLocals();
            var local = element.Locals[localIndex];
            return new Vect(local.Zx, local.Zy, local.Zz);
        }
    }
}
