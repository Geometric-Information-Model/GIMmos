namespace GIMcompress.ElementHelpers
{
    public class HyperbolaBuilder : IElementBuilder, IHyperbolaRadiiSet, IHyperbolaAxesSet
    {
        private Vect _center;
        private double _majorRadius;
        private double _minorRadius;
        private Vect _directionX;
        private Vect _directionY;
        private double _paramFirst;
        private double _paramLast;

        private HyperbolaBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            var matrix = new Matrix(_directionX, _directionY, _directionX ^ _directionY);
            GimLocal local = new();
            _center.SetLocalBasis(local);
            matrix.ApplyToLocal(local);

            return new GimElement
            {
                ElementType = GimElementType.C_hyperbol,
                ElementDoubles = [_paramFirst, _paramLast, _majorRadius, _minorRadius],
                Locals = [local]
            };
        }

        /// <inheritdoc/>
        public GimHyperbolaCurve BuildAdapter() => new(BuildElement());

        public static IHyperbolaRadiiSet FromCenterAndRadii(Vect center, double majorRadius, double minorRadius)
        {
            return new HyperbolaBuilder { _center = center, _majorRadius = majorRadius, _minorRadius = minorRadius };
        }

        IHyperbolaAxesSet IHyperbolaRadiiSet.WithDirections(Vect directionX, Vect directionY)
        {
            if (directionX.Norm == 0 || directionY.Norm == 0)
                throw new InvalidGimElementException("Hyperbola axes must not be null vectors!");
            Vect v1 = directionX.Direction;
            Vect v2 = directionY - (v1 * directionY) * v1;
            if (v2.Norm == 0)
                throw new InvalidGimElementException("Hyperbola axes must not be collinear!");
            _directionX = v1;
            _directionY = v2.Direction;
            return this;
        }

        IHyperbolaAxesSet IHyperbolaRadiiSet.WithDirectionAndAxis(Vect directionX, Vect axis)
        {
            if (directionX.Norm == 0 || axis.Norm == 0)
                throw new InvalidGimElementException("Ellipse axes must not be null vectors!");

            Vect v1 = directionX.Direction;
            if (axis.IsCollinearTo(v1))
                throw new InvalidGimElementException("Ellipse axes must not be collinear!");
            _directionX = v1;
            _directionY = (axis ^ v1).Direction;
            return this;
        }

        IElementBuilder IHyperbolaAxesSet.WithParamRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last;
            return this;
        }
    }

    public interface IHyperbolaRadiiSet
    {
        IHyperbolaAxesSet WithDirections(Vect directionX, Vect directionY);
        IHyperbolaAxesSet WithDirectionAndAxis(Vect directionX, Vect axis);
    }
    public interface IHyperbolaAxesSet { IElementBuilder WithParamRange(double first, double last); }
}
