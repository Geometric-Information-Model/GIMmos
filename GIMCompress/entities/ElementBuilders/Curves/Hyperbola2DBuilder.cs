namespace GIMcompress.ElementHelpers
{
    public class Hyperbola2DBuilder : IElementBuilder, IHyperbola2DRadiiSet, IHyperbola2DAxeChoice, IHyperbola2DWithAxesSet
    {
        private Vect _center;
        private double _majorRadius;
        private double _minorRadius;
        private Vect _dirU;
        private Vect _dirV;
        private double _paramFirst;
        private double _paramLast;

        private Hyperbola2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            Vect dirU = _dirU.Direction;
            Vect dirV = _dirV.Direction;

            GimLocal local = new();
            _center.SetLocalBasis(local);
            dirU.ApplyAsDirectionU(local);
            dirV.ApplyAsDirectionV(local);

            return new GimElement
            {
                ElementType = GimElementType.C_hyperbol2D,
                ElementDoubles = [_paramFirst, _paramLast, _majorRadius, _minorRadius],
                Locals = [local]
            };
        }

        /// <inheritdoc/>
        public GimHyperbola2DCurve BuildAdapter() => new(BuildElement());

        public static IHyperbola2DRadiiSet FromCenterAndRadii(double u, double v, double majorRadius, double minorRadius) =>
            FromCenterAndRadii(new Vect(u, v), majorRadius, minorRadius);

        public static IHyperbola2DRadiiSet FromCenterAndRadii(Vect center, double majorRadius, double minorRadius)
        {
            if (center.Length != 2)
                throw new InvalidGimElementException("Hyperbola2D center must be 2D (Length == 2).");
            return new Hyperbola2DBuilder { _center = center, _majorRadius = majorRadius, _minorRadius = minorRadius };
        }

        IHyperbola2DAxeChoice IHyperbola2DRadiiSet.WithDirectionU(double du, double dv) =>
            ((IHyperbola2DRadiiSet)this).WithDirectionU(new Vect(du, dv));

        IHyperbola2DAxeChoice IHyperbola2DRadiiSet.WithDirectionU(Vect dirU)
        {
            if (dirU is null || dirU.Length != 2)
                throw new InvalidGimElementException("Hyperbola2D direction U must be 2D (Length == 2).");
            if (dirU.Norm == 0)
                throw new InvalidGimElementException("Hyperbola2D direction U cannot be a null vector!");
            _dirU = dirU.Direction;
            return this;
        }

        IHyperbola2DWithAxesSet IHyperbola2DAxeChoice.WithDirectionV(double du, double dv) =>
            ((IHyperbola2DAxeChoice)this).WithDirectionV(new Vect(du, dv));

        IHyperbola2DWithAxesSet IHyperbola2DAxeChoice.WithDirectionV(Vect dirV)
        {
            if (dirV is null || dirV.Length != 2)
                throw new InvalidGimElementException("Hyperbola2D direction V must be 2D (Length == 2).");
            if (dirV.Norm == 0)
                throw new InvalidGimElementException("Hyperbola2D direction V cannot be a null vector!");
            if (dirV.IsCollinearTo(_dirU))
                throw new InvalidGimElementException("Hyperbola2D axes cannot be colinear!");
            _dirV = (dirV - (dirV * _dirU) * _dirU).Direction;
            return this;
        }

        IElementBuilder IHyperbola2DWithAxesSet.WithParamRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last;
            return this;
        }
    }

    public interface IHyperbola2DRadiiSet
    {
        IHyperbola2DAxeChoice WithDirectionU(Vect dirU);
        IHyperbola2DAxeChoice WithDirectionU(double du, double dv);
    }

    public interface IHyperbola2DAxeChoice
    {
        IHyperbola2DWithAxesSet WithDirectionV(Vect dirV);
        IHyperbola2DWithAxesSet WithDirectionV(double du, double dv);
    }

    public interface IHyperbola2DWithAxesSet
    {
        IElementBuilder WithParamRange(double first, double last);
    }
}
