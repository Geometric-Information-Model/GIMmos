using System.Linq;

namespace GIMcompress.ElementHelpers
{
    public class BSpline2DBuilder : IElementBuilder, IBSpline2DPolesSet, IBSpline2DWeightsSet, IBSpline2DKnotsSet, IBSpline2DMultsSet, IBSpline2DDegreeSet
    {
        private Vect[] _poles;
        private double[] _weights;
        private double[] _knots;
        private int[] _multiplicities;
        private int _degree;
        private Vect _startPoint;
        private Vect _endPoint;

        private BSpline2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            int knotCount = _knots.Length;
            int poleCount = _poles.Length;
            int totalDoubleCount = 4 + poleCount * 2 + knotCount + poleCount;

            double[] doubles = new double[totalDoubleCount];
            doubles[0] = _startPoint[0];
            doubles[1] = _startPoint[1];
            doubles[2] = _endPoint[0];
            doubles[3] = _endPoint[1];

            int idx = 4;
            for (int i = 0; i < poleCount; i++)
            {
                doubles[idx++] = _poles[i][0];
                doubles[idx++] = _poles[i][1];
            }
            for (int i = 0; i < knotCount; i++)
                doubles[idx++] = _knots[i];
            for (int i = 0; i < poleCount; i++)
                doubles[idx++] = _weights[i];

            int[] ints = new int[knotCount + 2];
            ints[0] = knotCount;
            for (int i = 0; i < knotCount; i++)
                ints[i + 1] = _multiplicities[i];
            ints[^1] = _degree;

            return new GimElement
            {
                ElementType = GimElementType.C_bspline2D,
                ElementDoubles = doubles,
                ElementInts = ints,
                Locals = [new GimLocal()]
            };
        }

        /// <inheritdoc/>
        public GimBSpline2DCurve BuildAdapter() => new(BuildElement());

        public static IBSpline2DPolesSet FromPoles(Vect[] poles)
        {
            if (poles == null || poles.Length == 0)
                throw new InvalidGimElementException("BSpline2D must have at least one pole!");
            for (int i = 0; i < poles.Length; i++)
            {
                if (poles[i] is null || poles[i].Length != 2)
                    throw new InvalidGimElementException("BSpline2D poles must be 2D (Length == 2).");
            }
            return new BSpline2DBuilder { _poles = poles };
        }

        IBSpline2DWeightsSet IBSpline2DPolesSet.WithWeights(double[] weights)
        {
            if (weights == null || weights.Length == 0)
                throw new InvalidGimElementException("BSpline2D must have weights!");
            if (weights.Length != _poles.Length)
                throw new InvalidGimElementException("BSpline2D weight count must equal pole count!");
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0)
                    throw new InvalidGimElementException("BSpline2D weights must be positive!");
            }
            _weights = (double[])weights.Clone();
            return this;
        }

        IBSpline2DWeightsSet IBSpline2DPolesSet.WithUniformWeights()
        {
            double[] uniform = new double[_poles.Length];
            System.Array.Fill(uniform, 1.0);
            return ((IBSpline2DPolesSet)this).WithWeights(uniform);
        }

        IBSpline2DKnotsSet IBSpline2DWeightsSet.WithKnots(double[] knots)
        {
            if (knots == null || knots.Length == 0)
                throw new InvalidGimElementException("BSpline2D must have knots!");
            bool needsSort = false;
            for (int i = 1; i < knots.Length; i++)
                if (knots[i] < knots[i - 1]) { needsSort = true; break; }

            _knots = needsSort ? [.. knots.OrderBy(k => k)] : knots;
            return this;
        }

        IBSpline2DMultsSet IBSpline2DKnotsSet.WithMultiplicities(int[] multiplicities)
        {
            if (multiplicities == null || multiplicities.Length == 0)
                throw new InvalidGimElementException("BSpline2D must have multiplicities!");
            if (multiplicities.Length != _knots.Length)
                throw new InvalidGimElementException("BSpline2D knot count must equal multiplicity count!");
            _multiplicities = multiplicities;
            return this;
        }

        IBSpline2DDegreeSet IBSpline2DMultsSet.WithDegree(int degree)
        {
            if (degree < 1)
                throw new InvalidGimElementException("BSpline2D degree must be at least 1!");
            if (_poles.Length <= degree)
                throw new InvalidGimElementException("BSpline2D pole count must be greater than degree!");
            _degree = degree;
            return this;
        }

        // TODO: Support full-range BSpline2D construction without caller-supplied endpoints.
        // The builder should infer start/end from first/last poles and use the full knot-derived param range.
        IElementBuilder IBSpline2DDegreeSet.WithEndpoints(double startU, double startV, double endU, double endV) =>
            ((IBSpline2DDegreeSet)this).WithEndpoints(new Vect(startU, startV), new Vect(endU, endV));

        IElementBuilder IBSpline2DDegreeSet.WithEndpoints(Vect start, Vect end)
        {
            if (start is null || start.Length != 2 || end is null || end.Length != 2)
                throw new InvalidGimElementException("BSpline2D endpoints must be 2D (Length == 2).");
            _startPoint = start;
            _endPoint = end;
            return this;
        }
    }

    public interface IBSpline2DPolesSet
    {
        IBSpline2DWeightsSet WithWeights(double[] weights);
        IBSpline2DWeightsSet WithUniformWeights();
    }

    public interface IBSpline2DWeightsSet { IBSpline2DKnotsSet WithKnots(double[] knots); }

    public interface IBSpline2DKnotsSet { IBSpline2DMultsSet WithMultiplicities(int[] multiplicities); }

    public interface IBSpline2DMultsSet { IBSpline2DDegreeSet WithDegree(int degree); }

    public interface IBSpline2DDegreeSet
    {
        IElementBuilder WithEndpoints(Vect start, Vect end);
        IElementBuilder WithEndpoints(double startU, double startV, double endU, double endV);
    }
}
