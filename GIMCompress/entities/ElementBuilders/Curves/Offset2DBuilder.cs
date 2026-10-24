namespace GIMcompress.ElementHelpers
{
    public class Offset2DBuilder : IElementBuilder
    {
        private double _offset;
        private Vect _direction;

        private Offset2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            Vect dir = _direction.Direction;
            GimLocal local = new();
            dir.ApplyToLocal(local);

            return new GimElement
            {
                ElementType = GimElementType.C_offset2D,
                ElementDoubles = [_offset],
                Locals = [local]
            };
        }

        /// <inheritdoc/>
        public GimOffset2DCurve BuildAdapter() => new(BuildElement());

        public static IElementBuilder FromOffsetAndDirection(double offset, double du, double dv) =>
            FromOffsetAndDirection(offset, new Vect(du, dv));

        /// <summary>
        /// Starts offset 2D curve construction.
        /// </summary>
        public static IElementBuilder FromOffsetAndDirection(double offset, Vect direction)
        {
            if (direction.Length != 2)
                throw new InvalidGimElementException("Offset2D direction must be 2D (Length == 2).");
            if (direction.Norm == 0)
                throw new InvalidGimElementException("Offset2D direction cannot be a null vector!");
            return new Offset2DBuilder { _offset = offset, _direction = direction };
        }
    }
}
