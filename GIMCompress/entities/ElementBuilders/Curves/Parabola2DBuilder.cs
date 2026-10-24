namespace GIMcompress.ElementHelpers
{
    public class Parabola2DBuilder : IElementBuilder, IParabola2DFocalSet, IParabola2DAxeChoice, IParabola2DWithAxesSet
    {
        private Vect _center;
        private double _focal;
        private Vect _dirU;
        private Vect _dirV;
        private double _paramFirst;
        private double _paramLast;

        private Parabola2DBuilder() { }

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
                ElementType = GimElementType.C_parabol2D,
                ElementDoubles = [_paramFirst, _paramLast, _focal],
                Locals = [local]
            };
        }

        /// <inheritdoc/>
        public GimParabola2DCurve BuildAdapter() => new(BuildElement());

        public static IParabola2DFocalSet FromCenterAndFocal(double u, double v, double focal) =>
            FromCenterAndFocal(new Vect(u, v), focal);

        public static IParabola2DFocalSet FromCenterAndFocal(Vect center, double focal)
        {
            if (center.Length != 2)
                throw new InvalidGimElementException("Parabola2D center must be 2D (Length == 2).");
            if (focal == 0)
                throw new InvalidGimElementException("Parabola2D focal cannot be zero!");
            return new Parabola2DBuilder { _center = center, _focal = focal };
        }

        IParabola2DAxeChoice IParabola2DFocalSet.WithDirectionU(double du, double dv) =>
            ((IParabola2DFocalSet)this).WithDirectionU(new Vect(du, dv));

        IParabola2DAxeChoice IParabola2DFocalSet.WithDirectionU(Vect dirU)
        {
            if (dirU is null || dirU.Length != 2)
                throw new InvalidGimElementException("Parabola2D direction U must be 2D (Length == 2).");
            if (dirU.Norm == 0)
                throw new InvalidGimElementException("Parabola2D direction U cannot be a null vector!");
            _dirU = dirU.Direction;
            return this;
        }

        IParabola2DWithAxesSet IParabola2DAxeChoice.WithDirectionV(double du, double dv) =>
            ((IParabola2DAxeChoice)this).WithDirectionV(new Vect(du, dv));

        IParabola2DWithAxesSet IParabola2DAxeChoice.WithDirectionV(Vect dirV)
        {
            if (dirV is null || dirV.Length != 2)
                throw new InvalidGimElementException("Parabola2D direction V must be 2D (Length == 2).");
            if (dirV.Norm == 0)
                throw new InvalidGimElementException("Parabola2D direction V cannot be a null vector!");
            if (dirV.IsCollinearTo(_dirU))
                throw new InvalidGimElementException("Parabola2D axes cannot be colinear!");
            _dirV = (dirV - (dirV * _dirU) * _dirU).Direction;
            return this;
        }

        IElementBuilder IParabola2DWithAxesSet.WithParamRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last;
            return this;
        }
    }

    public interface IParabola2DFocalSet
    {
        IParabola2DAxeChoice WithDirectionU(Vect dirU);
        IParabola2DAxeChoice WithDirectionU(double du, double dv);
    }

    public interface IParabola2DAxeChoice
    {
        IParabola2DWithAxesSet WithDirectionV(Vect dirV);
        IParabola2DWithAxesSet WithDirectionV(double du, double dv);
    }

    public interface IParabola2DWithAxesSet
    {
        IElementBuilder WithParamRange(double first, double last);
    }
}
