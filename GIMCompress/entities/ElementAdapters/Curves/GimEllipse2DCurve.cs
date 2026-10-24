using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_elipse2D elements.
    /// layout: elementDoubles[0]=majorRadius, [1]=minorRadius, [2]=paramFirst, [3]=paramLast.
    /// </summary>
    public class GimEllipse2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_elipse2D;

        public double MajorRadius => element.ElementDoubles[0];

        public double MinorRadius => element.ElementDoubles[1];

        /// <inheritdoc/>
        public override double ParamFirst => element.ElementDoubles[2];

        /// <inheritdoc/>
        public override double ParamLast => element.ElementDoubles[3];

        /// <inheritdoc/>
        public override bool IsClosed =>
            VectorHelpers.CompareDouble(ParamLast - ParamFirst, 2 * Math.PI);

        public GimEllipse2DCurve(GimElement ellipseElement) : base(ellipseElement) { }

        public Vect PointAt(double angleParam, int localIndex = 0)
        {
            Vect center = GetCenter(localIndex);
            Vect dirU = GetDirectionU(localIndex);
            Vect dirV = GetDirectionV(localIndex);
            return center + MajorRadius * Math.Cos(angleParam) * dirU + MinorRadius * Math.Sin(angleParam) * dirV;
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
            return VectorHelpers.CompareDouble(MajorRadius, 0) && VectorHelpers.CompareDouble(MinorRadius, 0);
        }
    }
}
