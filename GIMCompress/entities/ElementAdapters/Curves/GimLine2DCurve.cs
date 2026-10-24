using System;

namespace GIMcompress.ElementHelpers
{
    public class GimLine2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_line2D;

        /// <summary>
        /// The length of the line element.
        /// </summary>
        public double Length
        {
            get => Math.Abs(element.ElementDoubles[1] - element.ElementDoubles[0]);
        }

        /// <inheritdoc/>
        public override double ParamFirst => element.ElementDoubles[0];

        /// <inheritdoc/>
        public override double ParamLast => element.ElementDoubles[1];

        /// <inheritdoc/>
        public override bool IsClosed => false;

        public GimLine2DCurve(GimElement lineElement) : base(lineElement) { }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            AssertElementHasLocals();
            return Vect.FromLocalOrigin2D(element.Locals[localIndex]);
        }

        public Vect GetDirection(int localIndex = 0)
        {
            AssertElementHasLocals();
            return Vect.FromLocalDirection(element.Locals[localIndex]);
        }

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) =>
            GetStartPoint(localIndex) + Length * GetDirection(localIndex);
    }
}
