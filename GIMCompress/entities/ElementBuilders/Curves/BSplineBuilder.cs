using System.Linq;

namespace GIMcompress.ElementHelpers
{
    public class BSplineBuilder : IElementBuilder, IBSplinePolesSet, IBSplineWeightsSet, IBSplineKnotsSet, IBSplineMultsSet, IBSplineDegreeSet
    {
        private Vect[] _poles;
        private double[] _weights;
        private double[] _knots;
        private int[] _multiplicities;
        private int _degree;
        private Vect _startPoint;
        private Vect _endPoint;

        private BSplineBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            int knotCount = _knots.Length;
            int poleCount = _poles.Length;
            int totalDoubleCount = 6 + poleCount * 3 + knotCount + poleCount;

            double[] doubles = new double[totalDoubleCount];
            doubles[0] = _startPoint[0]; doubles[1] = _startPoint[1]; doubles[2] = _startPoint[2];
            doubles[3] = _endPoint[0]; doubles[4] = _endPoint[1]; doubles[5] = _endPoint[2];

            int idx = 6;
            for (int i = 0; i < poleCount; i++)
            {
                doubles[idx++] = _poles[i][0];
                doubles[idx++] = _poles[i][1];
                doubles[idx++] = _poles[i][2];
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
                ElementType = GimElementType.C_bspline,
                ElementDoubles = doubles,
                ElementInts = ints,
                Locals = [new GimLocal()]
            };
        }

        /// <inheritdoc/>
        public GimBSplineCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts BSpline construction from control points.
        /// </summary>
        public static IBSplinePolesSet FromPoles(Vect[] poles)
        {
            if (poles == null || poles.Length == 0)
                throw new InvalidGimElementException("BSpline must have at least one pole!");
            return new BSplineBuilder { _poles = poles };
        }

        IBSplineWeightsSet IBSplinePolesSet.WithWeights(double[] weights)
        {
            if (weights == null || weights.Length == 0)
                throw new InvalidGimElementException("BSpline must have weights!");
            if (weights.Length != _poles.Length)
                throw new InvalidGimElementException("BSpline weight count must equal pole count!");
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0)
                    throw new InvalidGimElementException("BSpline weights must be positive!");
            }
            _weights = (double[])weights.Clone();
            return this;
        }

        IBSplineWeightsSet IBSplinePolesSet.WithUniformWeights()
        {
            double[] uniform = new double[_poles.Length];
            System.Array.Fill(uniform, 1.0);
            return ((IBSplinePolesSet)this).WithWeights(uniform);
        }

        IBSplineKnotsSet IBSplineWeightsSet.WithKnots(double[] knots)
        {
            if (knots == null || knots.Length == 0)
                throw new InvalidGimElementException("BSpline must have knots!");
            // Verify array is sorted, otherwise sort it
            bool needsSort = false;
            for (int i = 1; i < knots.Length; i++)
                if (knots[i] < knots[i - 1]) { needsSort = true; break; }

            if (needsSort)
                _knots = [.. knots.OrderBy(k => k)];
            else
                _knots = knots;
            return this;
        }

        IBSplineMultsSet IBSplineKnotsSet.WithMultiplicities(int[] multiplicities)
        {
            if (multiplicities == null || multiplicities.Length == 0)
                throw new InvalidGimElementException("BSpline must have multiplicities!");
            if (multiplicities.Length != _knots.Length)
                throw new InvalidGimElementException("BSpline knot count must equal multiplicity count!");
            _multiplicities = multiplicities;
            return this;
        }

        IBSplineDegreeSet IBSplineMultsSet.WithDegree(int degree)
        {
            if (degree < 1)
                throw new InvalidGimElementException("BSpline degree must be at least 1!");
            if (_poles.Length <= degree)
                throw new InvalidGimElementException("BSpline pole count must be greater than degree!");
            _degree = degree;
            return this;
        }

        IElementBuilder IBSplineDegreeSet.WithEndpoints(Vect start, Vect end)
        {
            _startPoint = start;
            _endPoint = end;
            return this;
        }

    }

    public interface IBSplinePolesSet
    {
        IBSplineWeightsSet WithWeights(double[] weights);
        IBSplineWeightsSet WithUniformWeights();
    }
    public interface IBSplineWeightsSet { IBSplineKnotsSet WithKnots(double[] knots); }
    public interface IBSplineKnotsSet { IBSplineMultsSet WithMultiplicities(int[] multiplicities); }
    public interface IBSplineMultsSet { IBSplineDegreeSet WithDegree(int degree); }
    public interface IBSplineDegreeSet { IElementBuilder WithEndpoints(Vect start, Vect end); }
}
