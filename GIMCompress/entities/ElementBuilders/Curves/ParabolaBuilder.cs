namespace GIMcompress.ElementHelpers
{
    public class ParabolaBuilder : IElementBuilder, IParabolaFocalSet, IParabolaAxesSet
    {
        private Vect _center;
        private double _focal;
        private Vect _directionX;
        private Vect _directionY;
        private double _paramFirst;
        private double _paramLast;

        private ParabolaBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            var matrix = new Matrix(_directionX, _directionY, _directionX ^ _directionY);
            GimLocal local = new();
            _center.SetLocalBasis(local);
            matrix.ApplyToLocal(local);

            return new GimElement
            {
                ElementType = GimElementType.C_parabol,
                ElementDoubles = [_paramFirst, _paramLast, _focal],
                Locals = [local]
            };
        }

        /// <inheritdoc/>
        public GimParabolaCurve BuildAdapter() => new(BuildElement());

        public static IParabolaFocalSet FromCenterAndFocal(Vect center, double focal)
        {
            if (focal == 0)
                throw new InvalidGimElementException("Parabola focal cannot be zero!");
            return new ParabolaBuilder { _center = center, _focal = focal };
        }

        IParabolaAxesSet IParabolaFocalSet.WithDirections(Vect directionX, Vect directionY)
        {
            if (directionX.Norm == 0 || directionY.Norm == 0)
                throw new InvalidGimElementException("Parabola axes must not be null vectors!");
            Vect v1 = directionX.Direction;
            Vect v2 = directionY - (v1 * directionY) * v1;
            if (v2.Norm == 0)
                throw new InvalidGimElementException("Parabola axes must not be collinear!");
            _directionX = v1;
            _directionY = v2.Direction;
            return this;
        }

        IParabolaAxesSet IParabolaFocalSet.WithDirectionAndAxis(Vect directionX, Vect axis)
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

        IElementBuilder IParabolaAxesSet.WithParamRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last;
            return this;
        }
    }

    public interface IParabolaFocalSet
    {
        IParabolaAxesSet WithDirections(Vect directionX, Vect directionY);
        IParabolaAxesSet WithDirectionAndAxis(Vect directionX, Vect axis);
    }
    public interface IParabolaAxesSet { IElementBuilder WithParamRange(double first, double last); }
}
