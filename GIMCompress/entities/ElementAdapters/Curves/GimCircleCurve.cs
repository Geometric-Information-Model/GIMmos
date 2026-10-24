using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_circle elements.
    /// layout: elementDoubles[0]=radius, [1]=paramFirst, [2]=paramLast.
    /// IsClosed when param range covers 2pi.
    /// </summary>
    public class GimCircleCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_circle;

        /// <summary>
        /// The circle radius.
        /// </summary>
        public double Radius
        {
            get => element.ElementDoubles[0];
        }

        /// <inheritdoc/>
        public override double ParamFirst
        {
            get => element.ElementDoubles[1];
        }

        /// <inheritdoc/>
        public override double ParamLast
        {
            get => element.ElementDoubles[2];
        }

        /// <inheritdoc/>
        public override bool IsClosed =>
            VectorHelpers.CompareDouble(ParamLast - ParamFirst, 2 * Math.PI);

        /// <summary>
        /// Wraps the given circle element.
        /// </summary>
        /// <param name="circleElement">The circle GimElement to wrap.</param>
        public GimCircleCurve(GimElement circleElement) : base(circleElement) { }

        public Vect GetCenter(int localIndex = 0)
        {
            AssertElementHasLocals();
            return element.Locals[localIndex].BasisPoint;
        }

        public Vect PointAt(double angleParam, int localIndex = 0)
        {
            Vect center = GetCenter(localIndex);
            Vect firstVector = GetDirectionX(localIndex);
            Vect secondVector = GetDirectionY(localIndex);
            return center + Radius * (Math.Cos(angleParam) * firstVector + Math.Sin(angleParam) * secondVector);
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) => PointAt(ParamFirst, localIndex);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) => PointAt(ParamLast, localIndex);

        /// <inheritdoc/>
        /// <remarks>A circle is degenerate only if all control points (center+arc points) coincide.
        /// A full circle is NOT degenerate.</remarks>
        public override bool IsDegenerate(int localIndex = 0)
        {
            if (!IsClosed)
                return base.IsDegenerate(localIndex);

            return VectorHelpers.CompareDouble(Radius, 0);
        }

        /// <summary>
        /// Gets the axis (normal) of the circle plane. Z axis of the local matrix.
        /// </summary>
        public Vect GetAxis(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[2];
        }

        /// <summary>
        /// Gets the first direction vector (X) of the circle plane. First row of the local matrix.
        /// </summary>
        public Vect GetDirectionX(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[0];
        }

        /// <summary>
        /// Gets the second direction vector (Y) of the circle plane. Second row of the local matrix.
        /// </summary>
        public Vect GetDirectionY(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[1];
        }
    }
}
