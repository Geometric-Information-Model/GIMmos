using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_elipse elements.
    /// layout: elementDoubles[0]=majorRadius, [1]=minorRadius, [2]=paramFirst, [3]=paramLast.
    /// Params stored as-given (no normalisation).
    /// </summary>
    public class GimEllipseCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_elipse;

        /// <summary>
        /// Major radius of the ellipse.
        /// </summary>
        public double MajorRadius
        {
            get => element.ElementDoubles[0];
        }

        /// <summary>
        /// Minor radius of the ellipse.
        /// </summary>
        public double MinorRadius
        {
            get => element.ElementDoubles[1];
        }

        /// <inheritdoc/>
        public override double ParamFirst
        {
            get => element.ElementDoubles[2];
        }

        /// <inheritdoc/>
        public override double ParamLast
        {
            get => element.ElementDoubles[3];
        }

        /// <inheritdoc/>
        public override bool IsClosed =>
            VectorHelpers.CompareDouble(ParamLast - ParamFirst, 2 * System.Math.PI);

        /// <summary>
        /// Wraps the given ellipse element.
        /// </summary>
        /// <param name="ellipseElement">The ellipse GimElement to wrap.</param>
        public GimEllipseCurve(GimElement ellipseElement) : base(ellipseElement) { }

        public Vect PointAt(double angleParam, int localIndex = 0)
        {
            Vect center = GetCenter(localIndex);
            Vect firstVector = GetDirectionX(localIndex);
            Vect secondVector = GetDirectionY(localIndex);
            return center + MajorRadius * Math.Cos(angleParam) * firstVector + MinorRadius * Math.Sin(angleParam) * secondVector;
        }

        public Vect GetCenter(int localIndex = 0)
        {
            AssertElementHasLocals();
            return element.Locals[localIndex].BasisPoint;
        }
        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) => PointAt(ParamFirst, localIndex);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) => PointAt(ParamLast, localIndex);

        /// <inheritdoc/>
        /// <remarks>An ellipse is degenerate only if both radii are effectively zero.</remarks>
        public override bool IsDegenerate(int localIndex = 0)
        {
            if (!IsClosed)
                return base.IsDegenerate(localIndex);
            return VectorHelpers.CompareDouble(MajorRadius, 0) && VectorHelpers.CompareDouble(MinorRadius, 0);
        }

        /// <summary>
        /// Gets the axis (normal) of the ellipse plane. Z axis of the local matrix.
        /// </summary>
        public Vect GetAxis(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[2];
        }

        /// <summary>
        /// Gets the first direction vector (X) of the ellipse plane. First row of the local matrix.
        /// </summary>
        public Vect GetDirectionX(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[0];
        }

        /// <summary>
        /// Gets the second direction vector (Y) of the ellipse plane. Second row of the local matrix.
        /// </summary>
        public Vect GetDirectionY(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[1];
        }
    }
}
