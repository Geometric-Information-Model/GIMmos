using System;

namespace GIMcompress.ElementHelpers
{
    public class EllipseBuilder : IElementBuilder, IEllipseCenterSet, IEllipseRadiiSet, IEllipseAxesSet
    {
        private Vect _center;
        private double _majorRadius;
        private double _minorRadius;
        private Vect _directionX;
        private Vect _directionY;
        private double _paramFirst = 0;
        private double _paramLast = 2 * Math.PI;

        private EllipseBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            var matrix = new Matrix(_directionX, _directionY, _directionX ^ _directionY);

            GimLocal ellipseLocal = new();
            _center.SetLocalBasis(ellipseLocal);
            matrix.ApplyToLocal(ellipseLocal);

            return new GimElement
            {
                ElementType = GimElementType.C_elipse,
                ElementDoubles = [_majorRadius, _minorRadius, _paramFirst, _paramLast],
                Locals = [ellipseLocal]
            };
        }

        /// <inheritdoc/>
        public GimEllipseCurve BuildAdapter() => new(BuildElement());

        public static IEllipseCenterSet FromCenter(Vect center) => new EllipseBuilder { _center = center };

        IEllipseRadiiSet IEllipseCenterSet.WithRadii(double majorRadius, double minorRadius)
        {
            _majorRadius = Math.Max(majorRadius, minorRadius);
            _minorRadius = Math.Min(majorRadius, minorRadius);
            return this;
        }

        IEllipseAxesSet IEllipseRadiiSet.WithDirections(Vect directionX, Vect directionY)
        {
            if (directionX.Norm == 0 || directionY.Norm == 0)
                throw new InvalidGimElementException("Ellipse axes must not be null vectors!");

            Vect v1 = directionX.Direction;
            Vect v2 = directionY - (v1 * directionY) * v1;
            if (v2.Norm == 0)
                throw new InvalidGimElementException("Ellipse axes must not be collinear!");
            _directionX = v1;
            _directionY = v2.Direction;
            return this;
        }

        IEllipseAxesSet IEllipseRadiiSet.WithDirectionAndAxis(Vect directionX, Vect axis)
        {
            if (directionX.Norm == 0 || axis.Norm == 0)
                throw new InvalidGimElementException("Ellipse axes must not be null vectors!");

            Vect v1 = directionX.Direction;
            if (axis.IsCollinearTo(v1))
                throw new InvalidGimElementException("Ellipse axes must not be collinear!");
            _directionX = v1;
            _directionY = (axis ^ v1).Direction;
            return this;
        }


        IElementBuilder IEllipseAxesSet.WithAngleRange(double first, double last)
        {
            _paramFirst = first;
            _paramLast = last < first ? last + 2 * Math.PI : last;
            return this;
        }

        IElementBuilder IEllipseAxesSet.WithTotalAngle(double totalAngle)
        {
            _paramFirst = 0;
            _paramLast = totalAngle;
            return this;
        }
    }

    public interface IEllipseCenterSet { IEllipseRadiiSet WithRadii(double majorRadius, double minorRadius); }
    public interface IEllipseRadiiSet
    {
        IEllipseAxesSet WithDirections(Vect directionX, Vect directionY);
        IEllipseAxesSet WithDirectionAndAxis(Vect directionX, Vect Axis);
    }
    public interface IEllipseAxesSet : IElementBuilder
    {
        IElementBuilder WithAngleRange(double first, double last);
        IElementBuilder WithTotalAngle(double totalAngle);
    }
}
