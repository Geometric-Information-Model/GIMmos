namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_parabol2D elements.
    /// layout: elementDoubles[0]=paramFirst, [1]=paramLast, [2]=focal.
    /// </summary>
    public class GimParabola2DCurve : AbstractCurve2DAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_parabol2D;

        public double Focal => element.ElementDoubles[2];

        /// <inheritdoc/>
        public override double ParamFirst => element.ElementDoubles[0];

        /// <inheritdoc/>
        public override double ParamLast => element.ElementDoubles[1];

        /// <inheritdoc/>
        public override bool IsClosed => false;

        public GimParabola2DCurve(GimElement parabolaElement) : base(parabolaElement) { }

        public Vect EvaluateAt(double t, int localIndex = 0)
        {
            Vect center = GetCenter(localIndex);
            Vect dirU = GetDirectionU(localIndex);
            Vect dirV = GetDirectionV(localIndex);
            return center + (t * t / (4 * Focal)) * dirU + t * dirV;
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) => EvaluateAt(ParamFirst, localIndex);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) => EvaluateAt(ParamLast, localIndex);
    }
}
