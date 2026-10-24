namespace GIMcompress.ElementHelpers
{
    public class LineBuilder : IElementBuilder, ILineBuildTypeChoice, ILineWithDirection
    {
        private Vect _start;
        private Vect _dir;
        private double _length;

        private LineBuilder() { }

        /// <summary>
        /// Finalizes a Line GimElement.
        /// The direction vector is normalized and stored in the local Z axis.
        /// elementDoubles is [0, _length].
        /// </summary>
        /// <returns>Constructed Line Element.</returns>
        public GimElement BuildElement()
        {
            Vect direction = _dir;
            direction = direction.Direction;

            GimElement lineElement = new()
            {
                ElementType = GimElementType.C_line,
                ElementDoubles = [0, _length]
            };

            GimLocal lineLocal = new();
            _start.SetLocalBasis(lineLocal);
            direction.ApplyToLocal(lineLocal);

            lineElement.Locals.Add(lineLocal);
            return lineElement;
        }

        /// <summary>
        /// Builds the finalized line as an adapter, wrapping the final line element.
        /// </summary>
        /// <returns>The constructed line element adapter.</returns>
        public GimLineCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts the line building operation and specifies the starting point.
        /// </summary>
        /// <param name="start">The starting point.</param>
        /// <returns>A builder with the choice to build from an ending point, or from direction and length.</returns>
        public static ILineBuildTypeChoice From(Vect start) => new LineBuilder() { _start = start };

        /// <summary>
        /// Sets the final point for the line. Finalizes the line.
        /// </summary>
        /// <param name="end">The ending point.</param>
        /// <returns>A builder with a line ready to be constructed.</returns>
        IElementBuilder ILineBuildTypeChoice.To(Vect end)
        {
            var diff = (end - _start);
            _length = diff.Norm;
            if (_length == 0)
                _dir = new Vect(1, 0, 0);
            else
                _dir = diff.Direction;
            return this;
        }

        /// <summary>
        /// Sets the direction of the line.
        /// </summary>
        /// <param name="direction">The line direction (must not be null vector).</param>
        /// <returns>A builder awaiting the length or params of the line.</returns>
        /// <exception cref="InvalidGimElementException">Thrown when direction is a null vector.</exception>
        ILineWithDirection ILineBuildTypeChoice.WithDirection(Vect direction)
        {
            if (direction.Norm == 0)
                throw new InvalidGimElementException("Line direction cannot be a null vector!");
            _dir = direction.Direction;
            return this;
        }

        /// <summary>
        /// Sets the length of the line. Finalizes the line.
        /// </summary>
        /// <param name="length">The length of the line.</param>
        /// <returns>A builder with a line ready to be constructed.</returns>
        IElementBuilder ILineWithDirection.WithLength(double length)
        {
            _length = length;
            return this;
        }

        /// <summary>
        /// Sets explicit parametric range. Normalizes to [0, _length] internally
        /// by shifting the start point along the direction vector.
        /// If last &lt; first (inverted params), the direction is flipped and length is positive.
        /// </summary>
        /// <param name="first">The first parameter.</param>
        /// <param name="last">The last parameter.</param>
        /// <returns>A builder with a line ready to be constructed.</returns>
        IElementBuilder ILineWithDirection.WithParams(double first, double last)
        {
            if (last < first)
            {
                _dir = -_dir;
                _start += last * _dir;
                _length = first - last;
            }
            else
            {
                _start += first * _dir;
                _length = last - first;
            }
            return this;
        }
    }

    /// <summary>
    /// First step of line building: choose between endpoint or direction.
    /// </summary>
    public interface ILineBuildTypeChoice
    {
        /// <summary>Set the end point directly.</summary>
        IElementBuilder To(Vect end);
        /// <summary>Set the direction, then choose length or params.</summary>
        ILineWithDirection WithDirection(Vect direction);
    }

    /// <summary>
    /// Second step after choosing direction: choose length or explicit params.
    /// </summary>
    public interface ILineWithDirection
    {
        /// <summary>Set the length of the line.</summary>
        IElementBuilder WithLength(double length);
        /// <summary>Set explicit parametric range [first, last]. Normalized internally.</summary>
        IElementBuilder WithParams(double first, double last);
    }

}
