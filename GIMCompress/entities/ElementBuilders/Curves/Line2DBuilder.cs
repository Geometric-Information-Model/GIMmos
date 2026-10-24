namespace GIMcompress.ElementHelpers
{
    public class Line2DBuilder : IElementBuilder, ILine2DBuildTypeChoice, ILine2DWithDirection
    {
        private Vect _start;
        private Vect _dir;
        private double _length;

        private Line2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            Vect direction = _dir;
            direction = direction.Direction;

            GimElement lineElement = new()
            {
                ElementType = GimElementType.C_line2D,
                ElementDoubles = [0, _length]
            };

            GimLocal lineLocal = new();
            _start.SetLocalBasis(lineLocal);
            direction.ApplyToLocal(lineLocal);

            lineElement.Locals.Add(lineLocal);
            return lineElement;
        }

        /// <inheritdoc/>
        public GimLine2DCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts the line building operation with a 2D starting point.
        /// </summary>
        public static ILine2DBuildTypeChoice From(double u, double v) => From(new Vect(u, v));

        /// <summary>
        /// Starts the line building operation with a 2D starting point.
        /// </summary>
        public static ILine2DBuildTypeChoice From(Vect uv)
        {
            if (uv.Length != 2)
                throw new InvalidGimElementException("Line2D requires a 2D point (Length == 2).");
            return new Line2DBuilder() { _start = uv };
        }

        IElementBuilder ILine2DBuildTypeChoice.To(double u, double v) => ((ILine2DBuildTypeChoice)this).To(new Vect(u, v));

        IElementBuilder ILine2DBuildTypeChoice.To(Vect end)
        {
            if (end.Length != 2)
                throw new InvalidGimElementException("Line2D requires a 2D point (Length == 2).");
            var diff = (end - _start);
            _length = diff.Norm;
            if (_length == 0)
                _dir = new Vect(1, 0);
            else
                _dir = diff.Direction;
            return this;
        }

        ILine2DWithDirection ILine2DBuildTypeChoice.WithDirection(double du, double dv) =>
            ((ILine2DBuildTypeChoice)this).WithDirection(new Vect(du, dv));

        ILine2DWithDirection ILine2DBuildTypeChoice.WithDirection(Vect direction)
        {
            if (direction.Length != 2)
                throw new InvalidGimElementException("Line2D direction must be 2D (Length == 2).");
            if (direction.Norm == 0)
                throw new InvalidGimElementException("Line2D direction cannot be a null vector!");
            _dir = direction.Direction;
            return this;
        }

        IElementBuilder ILine2DWithDirection.WithLength(double length)
        {
            _length = length;
            return this;
        }

        IElementBuilder ILine2DWithDirection.WithParams(double first, double last)
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

    public interface ILine2DBuildTypeChoice
    {
        IElementBuilder To(Vect end);
        IElementBuilder To(double u, double v);
        ILine2DWithDirection WithDirection(Vect direction);
        ILine2DWithDirection WithDirection(double du, double dv);
    }

    public interface ILine2DWithDirection
    {
        IElementBuilder WithLength(double length);
        IElementBuilder WithParams(double first, double last);
    }
}
