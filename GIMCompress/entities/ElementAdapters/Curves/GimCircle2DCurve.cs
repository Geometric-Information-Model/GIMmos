using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_circle2D elements.
    /// layout: elementDoubles[0]=radius, [1]=paramFirst, [2]=paramLast.
    /// </summary>
    public class GimCircle2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_circle2D;

        public double Radius => element.ElementDoubles[0];

        /// <inheritdoc/>
        public override double ParamFirst => element.ElementDoubles[1];

        /// <inheritdoc/>
        public override double ParamLast => element.ElementDoubles[2];

        /// <inheritdoc/>
        public override bool IsClosed =>
            VectorHelpers.CompareDouble(ParamLast - ParamFirst, 2 * Math.PI);

        public GimCircle2DCurve(GimElement circleElement) : base(circleElement) { }

        public Vect PointAt(double angleParam, int localIndex = 0)
        {
            Vect center = GetCenter(localIndex);
            Vect dirU = GetDirectionU(localIndex);
            Vect dirV = GetDirectionV(localIndex);
            return center + Radius * (Math.Cos(angleParam) * dirU + Math.Sin(angleParam) * dirV);
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) => PointAt(ParamFirst, localIndex);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) => PointAt(ParamLast, localIndex);

        /// <inheritdoc/>
        public override bool IsDegenerate(int localIndex = 0)
        {
            if (!IsClosed)
                return base.IsDegenerate(localIndex);
            return VectorHelpers.CompareDouble(Radius, 0);
        }
    }
}
