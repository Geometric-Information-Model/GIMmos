namespace GIMcompress.ElementHelpers
{
    public class Bezier2DBuilder : IElementBuilder, IBezier2DPolesSet, IBezier2DParamsSet
    {
        private Vect[] _poles;
        private Vect _startPoint;
        private Vect _endPoint;
        private double _paramFirst;
        private double _paramLast;

        private Bezier2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            int totalDoubleCount = 4 + _poles.Length * 2 + 2;
            double[] doubles = new double[totalDoubleCount];
            doubles[0] = _startPoint[0];
            doubles[1] = _startPoint[1];
            doubles[2] = _endPoint[0];
            doubles[3] = _endPoint[1];

            for (int i = 0; i < _poles.Length; i++)
            {
                doubles[4 + i * 2] = _poles[i][0];
                doubles[4 + i * 2 + 1] = _poles[i][1];
            }
            doubles[^2] = _paramFirst;
            doubles[^1] = _paramLast;

            return new GimElement
            {
                ElementType = GimElementType.C_bezier2D,
                ElementDoubles = doubles,
                ElementInts = [_poles.Length],
                Locals = [new GimLocal()]
            };
        }

        /// <inheritdoc/>
        public GimBezier2DCurve BuildAdapter() => new(BuildElement());

        public static IBezier2DPolesSet FromControlPoints(Vect[] poles)
        {
            if (poles == null || poles.Length == 0)
                throw new InvalidGimElementException("Bezier2D must have at least one pole!");
            for (int i = 0; i < poles.Length; i++)
            {
                if (poles[i] is null || poles[i].Length != 2)
                    throw new InvalidGimElementException("Bezier2D poles must be 2D (Length == 2).");
            }
            return new Bezier2DBuilder { _poles = poles };
        }

        IBezier2DParamsSet IBezier2DPolesSet.WithParamRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last;
            return this;
        }

        // TODO: Support full Bezier2D construction without caller-supplied params or endpoints.
        // Full range is expected to be 0 -> curve length; confirm length calculation before implementing.
        IElementBuilder IBezier2DParamsSet.WithEndpoints(double startU, double startV, double endU, double endV) =>
            ((IBezier2DParamsSet)this).WithEndpoints(new Vect(startU, startV), new Vect(endU, endV));

        IElementBuilder IBezier2DParamsSet.WithEndpoints(Vect start, Vect end)
        {
            if (start is null || start.Length != 2 || end is null || end.Length != 2)
                throw new InvalidGimElementException("Bezier2D endpoints must be 2D (Length == 2).");
            _startPoint = start;
            _endPoint = end;
            return this;
        }
    }

    public interface IBezier2DPolesSet
    {
        IBezier2DParamsSet WithParamRange(double first, double last);
    }

    public interface IBezier2DParamsSet
    {
        IElementBuilder WithEndpoints(Vect start, Vect end);
        IElementBuilder WithEndpoints(double startU, double startV, double endU, double endV);
    }
}
