using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_hyperbol2D elements.
    /// layout: elementDoubles[0]=paramFirst, [1]=paramLast, [2]=majorRadius, [3]=minorRadius.
    /// </summary>
    public class GimHyperbola2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_hyperbol2D;

        public double MajorRadius => element.ElementDoubles[2];

        public double MinorRadius => element.ElementDoubles[3];

        /// <inheritdoc/>
        public override double ParamFirst => element.ElementDoubles[0];

        /// <inheritdoc/>
        public override double ParamLast => element.ElementDoubles[1];

        /// <inheritdoc/>
        public override bool IsClosed => false;

        public GimHyperbola2DCurve(GimElement hyperbolaElement) : base(hyperbolaElement) { }

        public Vect EvaluateAt(double t, int localIndex = 0)
        {
            Vect center = GetCenter(localIndex);
            Vect dirU = GetDirectionU(localIndex);
            Vect dirV = GetDirectionV(localIndex);
            return center + MajorRadius * Math.Cosh(t) * dirU + MinorRadius * Math.Sinh(t) * dirV;
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) => EvaluateAt(ParamFirst, localIndex);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) => EvaluateAt(ParamLast, localIndex);
    }
}
