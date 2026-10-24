namespace GIMcompress.ElementHelpers
{
    public class OffsetCurveBuilder : IElementBuilder
    {
        private double _offset;
        private Vect _direction;

        private OffsetCurveBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            Vect dir = _direction.Direction;
            GimLocal local = new();
            dir.ApplyToLocal(local);

            return new GimElement
            {
                ElementType = GimElementType.C_offset,
                ElementDoubles = [_offset],
                Locals = [local]
            };
        }

        /// <inheritdoc/>
        public GimOffsetCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts offset curve construction.
        /// </summary>
        /// <param name="offset">The offset value.</param>
        /// <param name="direction">The direction of the offset (must not be null vector).</param>
        /// <returns>A builder ready to construct.</returns>
        /// <exception cref="InvalidGimElementException">Thrown when direction is a null vector.</exception>
        public static IElementBuilder FromOffsetAndDirection(double offset, Vect direction)
        {
            if (direction.Norm == 0)
                throw new InvalidGimElementException("Offset direction cannot be a null vector!");
            return new OffsetCurveBuilder { _offset = offset, _direction = direction };
        }
    }
}
