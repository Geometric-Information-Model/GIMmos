namespace GIMcompress.ElementHelpers
{
    public class BezierBuilder : IElementBuilder, IBezierPolesSet, IBezierParamsSet
    {
        private Vect[] _poles;
        private Vect _startPoint;
        private Vect _endPoint;
        private double _paramFirst;
        private double _paramLast;

        private BezierBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            Vect start = _startPoint;
            Vect end = _endPoint;

            int totalDoubleCount = 6 + _poles.Length * 3 + 2;
            double[] doubles = new double[totalDoubleCount];
            doubles[0] = start[0]; doubles[1] = start[1]; doubles[2] = start[2];
            doubles[3] = end[0]; doubles[4] = end[1]; doubles[5] = end[2];

            for (int i = 0; i < _poles.Length; i++)
            {
                doubles[6 + i * 3] = _poles[i][0];
                doubles[6 + i * 3 + 1] = _poles[i][1];
                doubles[6 + i * 3 + 2] = _poles[i][2];
            }
            doubles[^2] = _paramFirst;
            doubles[^1] = _paramLast;

            return new GimElement
            {
                ElementType = GimElementType.C_bezier,
                ElementDoubles = doubles,
                ElementInts = [_poles.Length],
                Locals = [new GimLocal()]
            };
        }

        /// <inheritdoc/>
        public GimBezierCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts bezier construction from control points.
        /// </summary>
        /// <exception cref="InvalidGimElementException">Thrown when poles array is null or empty.</exception>
        public static IBezierPolesSet FromControlPoints(Vect[] poles)
        {
            if (poles == null || poles.Length == 0)
                throw new InvalidGimElementException("Bezier must have at least one pole!");
            return new BezierBuilder { _poles = poles };
        }

        IBezierParamsSet IBezierPolesSet.WithParamRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last;
            return this;
        }

        IElementBuilder IBezierParamsSet.WithEndpoints(Vect start, Vect end)
        {
            _startPoint = start;
            _endPoint = end;
            return this;
        }
    }

    public interface IBezierPolesSet
    {
        IBezierParamsSet WithParamRange(double first, double last);
    }
    public interface IBezierParamsSet
    {
        IElementBuilder WithEndpoints(Vect start, Vect end);
    }
}
