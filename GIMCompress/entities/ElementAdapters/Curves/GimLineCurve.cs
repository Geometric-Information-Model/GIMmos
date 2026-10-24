using System;

namespace GIMcompress.ElementHelpers
{
    public class GimLineCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_line;

        /// <summary>
        /// The length of the line element. (Does not depend on locals)
        /// </summary>
        public double Length
        {
            get => Math.Abs(element.ElementDoubles[1] - element.ElementDoubles[0]);
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
        /// Builds a GimLineCurve wrapping the lineElement
        /// </summary>
        /// <param name="lineElement">The line Element to wrap</param>
        public GimLineCurve(GimElement lineElement) : base(lineElement)
        {

        }

        /// <summary>
        /// Gets the starting point of the line element. Depends on which local is used.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>The 3D start point.</returns>
        public override Vect GetStartPoint(int localIndex = 0)
        {
            AssertElementHasLocals();
            return element.Locals[localIndex].BasisPoint;
        }

        /// <summary>
        /// Gets the direction of the line element. Depends on which local is used.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>The direction vector.</returns>
        public Vect GetDirection(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Vect(element.Locals[localIndex]);
        }

        /// <summary>
        /// Gets the end point of the line element. Depends on which local is used.
        /// </summary>
        /// <param name="localIndex">Index of the local (instance) to use.</param>
        /// <returns>The 3D end point.</returns>
        public override Vect GetEndPoint(int localIndex = 0) => GetStartPoint(localIndex) + Length * GetDirection(localIndex);

    }
}
