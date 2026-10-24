using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Adapter for C_hyperbol elements.
    /// layout: elementDoubles[0]=paramFirst, [1]=paramLast, [2]=majorRadius, [3]=minorRadius.
    /// CAD definition: C(t) = P + R_major*cosh(t)*X + R_minor*sinh(t)*Y, where X = major axis, Y = minor axis.
    /// </summary>
    public class GimHyperbolaCurve : AbstractCurveAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.C_hyperbol;

        /// <summary>
        /// Major radius of the hyperbola.
        /// </summary>
        public double MajorRadius
        {
            get => element.ElementDoubles[2];
        }

        /// <summary>
        /// Minor radius of the hyperbola.
        /// </summary>
        public double MinorRadius
        {
            get => element.ElementDoubles[3];
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
        /// Wraps the given hyperbola element.
        /// </summary>
        public GimHyperbolaCurve(GimElement hyperbolaElement) : base(hyperbolaElement) { }

        public Vect GetCenter(int localIndex = 0)
        {
            AssertElementHasLocals();
            return element.Locals[localIndex].BasisPoint;
        }

        /// <inheritdoc/>
        public override Vect GetStartPoint(int localIndex = 0) => EvaluateAt(ParamFirst, localIndex);

        /// <inheritdoc/>
        public override Vect GetEndPoint(int localIndex = 0) => EvaluateAt(ParamLast, localIndex);


        /// <summary>
        /// Evaluates the hyperbola at parameter t.
        /// C(t) = P + R_major*cosh(t)*X + R_minor*sinh(t)*Y
        /// </summary>
        public Vect EvaluateAt(double t, int localIndex = 0)
        {
            Vect P = GetCenter(localIndex);
            Vect X = GetMajorAxis(localIndex);
            Vect Y = GetMinorAxis(localIndex);
            return P + MajorRadius * Math.Cosh(t) * X + MinorRadius * Math.Sinh(t) * Y;
        }

        /// <summary>
        /// Gets the major axis of the hyperbola. First row of the local matrix (X direction).
        /// </summary>
        public Vect GetMajorAxis(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[0];
        }

        /// <summary>
        /// Gets the minor axis of the hyperbola. Second row of the local matrix (Y direction).
        /// </summary>
        public Vect GetMinorAxis(int localIndex = 0)
        {
            AssertElementHasLocals();
            return new Matrix(element.Locals[localIndex])[1];
        }
    }
}
