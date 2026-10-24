using System;

namespace GIMcompress.ElementHelpers
{
    public class Ellipse2DBuilder : IElementBuilder, IEllipse2DCenterSet, IEllipse2DRadiiSet, IEllipse2DAxeChoice, IEllipse2DWithAxesSet
    {
        private Vect _center;
        private double _majorRadius;
        private double _minorRadius;
        private Vect _dirU;
        private Vect _dirV;
        private double _paramFirst = 0;
        private double _paramLast = 2 * Math.PI;

        private Ellipse2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            Vect dirU = _dirU.Direction;
            Vect dirV = _dirV.Direction;

            GimLocal ellipseLocal = new();
            _center.SetLocalBasis(ellipseLocal);
            dirU.ApplyAsDirectionU(ellipseLocal);
            dirV.ApplyAsDirectionV(ellipseLocal);

            return new GimElement
            {
                ElementType = GimElementType.C_elipse2D,
                ElementDoubles = [_majorRadius, _minorRadius, _paramFirst, _paramLast],
                Locals = [ellipseLocal]
            };
        }

        /// <inheritdoc/>
        public GimEllipse2DCurve BuildAdapter() => new(BuildElement());

        public static IEllipse2DCenterSet FromCenter(double u, double v) => FromCenter(new Vect(u, v));

        public static IEllipse2DCenterSet FromCenter(Vect center)
        {
            if (center.Length != 2)
                throw new InvalidGimElementException("Ellipse2D center must be 2D (Length == 2).");
            return new Ellipse2DBuilder { _center = center };
        }

        IEllipse2DRadiiSet IEllipse2DCenterSet.WithRadii(double majorRadius, double minorRadius)
        {
            _majorRadius = Math.Max(majorRadius, minorRadius);
            _minorRadius = Math.Min(majorRadius, minorRadius);
            return this;
        }

        IEllipse2DAxeChoice IEllipse2DRadiiSet.WithDirectionU(double du, double dv) =>
            ((IEllipse2DRadiiSet)this).WithDirectionU(new Vect(du, dv));

        IEllipse2DAxeChoice IEllipse2DRadiiSet.WithDirectionU(Vect dirU)
        {
            if (dirU is null || dirU.Length != 2)
                throw new InvalidGimElementException("Ellipse2D direction U must be 2D (Length == 2).");
            if (dirU.Norm == 0)
                throw new InvalidGimElementException("Ellipse2D direction U cannot be a null vector!");
            _dirU = dirU.Direction;
            return this;
        }

        IEllipse2DWithAxesSet IEllipse2DAxeChoice.WithDirectionV(double du, double dv) =>
            ((IEllipse2DAxeChoice)this).WithDirectionV(new Vect(du, dv));

        IEllipse2DWithAxesSet IEllipse2DAxeChoice.WithDirectionV(Vect dirV)
        {
            if (dirV is null || dirV.Length != 2)
                throw new InvalidGimElementException("Ellipse2D direction V must be 2D (Length == 2).");
            if (dirV.Norm == 0)
                throw new InvalidGimElementException("Ellipse2D direction V cannot be a null vector!");
            if (dirV.IsCollinearTo(_dirU))
                throw new InvalidGimElementException("Ellipse2D axes cannot be colinear!");
            _dirV = (dirV - (dirV * _dirU) * _dirU).Direction;
            return this;
        }

        IElementBuilder IEllipse2DWithAxesSet.WithAngleRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last;
            return this;
        }

        IElementBuilder IEllipse2DWithAxesSet.WithTotalAngle(double totalAngle)
        {
            _paramFirst = 0;
            _paramLast = totalAngle;
            return this;
        }
    }

    public interface IEllipse2DCenterSet
    {
        IEllipse2DRadiiSet WithRadii(double majorRadius, double minorRadius);
    }

    public interface IEllipse2DRadiiSet
    {
        IEllipse2DAxeChoice WithDirectionU(Vect dirU);
        IEllipse2DAxeChoice WithDirectionU(double du, double dv);
    }

    public interface IEllipse2DAxeChoice
    {
        IEllipse2DWithAxesSet WithDirectionV(Vect dirV);
        IEllipse2DWithAxesSet WithDirectionV(double du, double dv);
    }

    public interface IEllipse2DWithAxesSet : IElementBuilder
    {
        IElementBuilder WithAngleRange(double first, double last);
        IElementBuilder WithTotalAngle(double totalAngle);
    }
}
