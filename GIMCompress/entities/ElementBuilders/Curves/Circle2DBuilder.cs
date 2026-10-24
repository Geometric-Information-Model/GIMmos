using System;

namespace GIMcompress.ElementHelpers
{
    public class Circle2DBuilder : IElementBuilder, ICircle2DCenterSet, ICircle2DRadiusSet, ICircle2DAxeChoice, ICircle2DWithAxesSet
    {
        private Vect _center;
        private double _radius;
        private Vect _dirU;
        private Vect _dirV;
        private double _angle = 2 * Math.PI;

        private Circle2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            Vect dirU = _dirU.Direction;
            Vect dirV = _dirV.Direction;

            GimLocal circleLocal = new();
            _center.SetLocalBasis(circleLocal);
            dirU.ApplyAsDirectionU(circleLocal);
            dirV.ApplyAsDirectionV(circleLocal);

            return new GimElement
            {
                ElementType = GimElementType.C_circle2D,
                ElementDoubles = [_radius, 0, Math.Abs(_angle)],
                Locals = [circleLocal]
            };
        }

        /// <inheritdoc/>
        public GimCircle2DCurve BuildAdapter() => new(BuildElement());

        public static ICircle2DCenterSet FromCenter(double u, double v) => FromCenter(new Vect(u, v));

        public static ICircle2DCenterSet FromCenter(Vect center)
        {
            if (center.Length != 2)
                throw new InvalidGimElementException("Circle2D center must be 2D (Length == 2).");
            return new Circle2DBuilder { _center = center };
        }

        ICircle2DRadiusSet ICircle2DCenterSet.WithRadius(double radius)
        {
            if (radius < 0)
                throw new InvalidGimElementException("Circle2D radius cannot be negative!");
            _radius = radius;
            return this;
        }

        ICircle2DAxeChoice ICircle2DRadiusSet.WithDirectionU(double du, double dv) =>
            ((ICircle2DRadiusSet)this).WithDirectionU(new Vect(du, dv));

        ICircle2DAxeChoice ICircle2DRadiusSet.WithDirectionU(Vect dirU)
        {
            if (dirU is null || dirU.Length != 2)
                throw new InvalidGimElementException("Circle2D direction U must be 2D (Length == 2).");
            if (dirU.Norm == 0)
                throw new InvalidGimElementException("Circle2D direction U cannot be a null vector!");
            _dirU = dirU.Direction;
            return this;
        }

        ICircle2DWithAxesSet ICircle2DAxeChoice.WithDirectionV(double du, double dv) =>
            ((ICircle2DAxeChoice)this).WithDirectionV(new Vect(du, dv));

        ICircle2DWithAxesSet ICircle2DAxeChoice.WithDirectionV(Vect dirV)
        {
            if (dirV is null || dirV.Length != 2)
                throw new InvalidGimElementException("Circle2D direction V must be 2D (Length == 2).");
            if (dirV.Norm == 0)
                throw new InvalidGimElementException("Circle2D direction V cannot be a null vector!");
            if (dirV.IsCollinearTo(_dirU))
                throw new InvalidGimElementException("Circle2D axes cannot be colinear!");
            _dirV = (dirV - (dirV * _dirU) * _dirU).Direction;
            return this;
        }

        IElementBuilder ICircle2DWithAxesSet.WithAngleRange(double first, double last)
        {
            _angle = last - first;
            if (first != 0)
            {
                double handednessSign = Math.Sign(_dirU.X * _dirV.Y - _dirU.Y * _dirV.X);
                if (handednessSign == 0)
                    handednessSign = 1;
                Vect adjDirU = Math.Cos(first) * _dirU + Math.Sin(first) * _dirV;
                _dirU = adjDirU.Direction;
                _dirV = new Vect(-handednessSign * _dirU.Y, handednessSign * _dirU.X).Direction;
            }
            return this;
        }

        IElementBuilder ICircle2DWithAxesSet.WithTotalAngle(double totalAngle)
        {
            if (totalAngle < 0)
                throw new InvalidGimElementException("Total circle2D angle cannot be negative!");
            _angle = Math.Min(totalAngle, 2 * Math.PI);
            return this;
        }
    }

    public interface ICircle2DCenterSet
    {
        ICircle2DRadiusSet WithRadius(double radius);
    }

    public interface ICircle2DRadiusSet
    {
        ICircle2DAxeChoice WithDirectionU(Vect dirU);
        ICircle2DAxeChoice WithDirectionU(double du, double dv);
    }

    public interface ICircle2DAxeChoice
    {
        ICircle2DWithAxesSet WithDirectionV(Vect dirV);
        ICircle2DWithAxesSet WithDirectionV(double du, double dv);
    }

    public interface ICircle2DWithAxesSet : IElementBuilder
    {
        IElementBuilder WithAngleRange(double first, double last);
        IElementBuilder WithTotalAngle(double totalAngle);
    }
}
