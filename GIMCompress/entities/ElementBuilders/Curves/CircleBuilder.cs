using System;

namespace GIMcompress.ElementHelpers
{
    public class CircleBuilder : IElementBuilder, ICircleCenterSet, ICircleRadiusSet, ICircleAxeChoice, ICircleWithAxesSet
    {
        private Vect _center;
        private double _radius;
        private Vect _dirX;
        private Vect _dirY;
        private double _angle = 2 * Math.PI;

        private CircleBuilder() { }

        /// <summary>
        /// Builds a C_circle GimElement from the canonical 5 fields.
        /// Params are [0, absAngle]. Matrix X=dirX, Y=dirY, Z=cross product.
        /// </summary>
        public GimElement BuildElement()
        {
            Vect v1 = _dirX.Direction;
            Vect v2 = _dirY.Direction;
            var matrix = new Matrix(v1, v2, v1 ^ v2);

            GimLocal circleLocal = new();
            _center.SetLocalBasis(circleLocal);
            matrix.ApplyToLocal(circleLocal);

            return new GimElement
            {
                ElementType = GimElementType.C_circle,
                ElementDoubles = [_radius, 0, Math.Abs(_angle)],
                Locals = [circleLocal]
            };
        }

        /// <inheritdoc/>
        public GimCircleCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts circle construction with a center point.
        /// </summary>
        public static ICircleCenterSet FromCenter(Vect center) => new CircleBuilder { _center = center };

        ICircleRadiusSet ICircleCenterSet.WithRadius(double radius)
        {
            if (radius < 0)
                throw new InvalidGimElementException("Circle radius cannot be negative!");
            _radius = radius;
            return this;
        }

        public static IElementBuilder From3Points(Vect arcStart, Vect arcCenter, Vect arcEnd)
        {
            if (arcStart == arcCenter || arcStart == arcEnd || arcCenter == arcEnd)
                throw new InvalidGimElementException("Circle points must not coincide!");

            var builder = new CircleBuilder();

            // constructing bisector plane normals, as the circle center lines on these planes
            Vect bisector1Normal = (arcCenter - arcStart).Direction;
            Vect bisector2Normal = (arcCenter - arcEnd).Direction;
            Vect planeNormal = (bisector1Normal ^ bisector2Normal).Direction;

            if (VectorHelpers.CompareDouble(planeNormal.Norm, 0))
                throw new InvalidGimElementException("Circle points are collinear!");

            // getting one point to define each plane
            Vect center1 = (arcCenter + arcStart) / 2;
            Vect center2 = (arcCenter + arcEnd) / 2;


            // we attempt to find the plane bissectors, that cut the lines A-C and B-C in half. The circle lies on
            // their intersection.

            // if each plane is characterized by a normal vector (a0,b0,c0) and a point (x0,y0,z0) then its equation is

            // a0(x-x0) + b0(y-y0) + c0(z-z0) = 0

            // if we assume our point P = (x,y,z) is the center of the circle, then it belong to all three planes.
            // it then verifies the equation:
            //

            // a1(x-x1) + b1(y-y1) + c1(z-y1) = 0       a1*x + b1*y + c1*z = a1*x1 + b1*y1 + c1*z1
            // a2(x-x2) + b2(y-y2) + c2(z-z2) = 0  =>   a2*x + b2*y + c2*z = a2*x1 + b2*y1 + c2*z1
            // a3(x-x3) + b3(y-y3) + c3(z-z3) = 0       a3*x + b3*y + c3*z = a3*x1 + b3*y1 + c3*z1   

            // that resulting equation is of the form Matrix * Unknown Point = Matrix * Known Point, so to find P = XYZ
            // we have to do Point = Matrix^(-1) * Vector, with Vector = Matrix * Known Point

            var intersectionMatrix = new Matrix(planeNormal, bisector1Normal, bisector2Normal);
            Vect offsetVect = new Vect(planeNormal * arcCenter, bisector1Normal * center1, bisector2Normal * center2);

            if (VectorHelpers.CompareDouble(intersectionMatrix.Determinant, 0))
                throw new InvalidGimElementException("Could not determine circle center!");

            var _center = (intersectionMatrix.Inverse * offsetVect).Round();

            Vect pointAInPlane = arcStart - _center;
            Vect pointBInPlane = arcEnd - _center;
            Vect arcPointInPlane = arcCenter - _center;

            // we calculate the radius using any point-to-center distance
            var _radius = pointAInPlane.Norm;

            // The first direction points at the starting point
            var _dirX = pointAInPlane.Direction;

            // some sort of Atan logic is happening here
            double paramB = Math.Acos(pointBInPlane.Direction * _dirX);
            double bSide = Math.Abs(Math.Acos(pointBInPlane.Direction * (planeNormal ^ _dirX).Direction));
            if (bSide > Math.PI / 2)
                paramB = 2 * Math.PI - paramB;

            // measuring ARC lengths to determine on which side the arc point is
            // this makes a big difference.
            double pi = Math.PI;

            double AB = Math.Acos(pointAInPlane.Direction * pointBInPlane.Direction);
            double AC = Math.Acos(arcPointInPlane.Direction * pointBInPlane.Direction);
            double BC = Math.Acos(arcPointInPlane.Direction * pointAInPlane.Direction);

            double paramStart, paramEnd;

            Vect localVectorA = null, localVectorB = null;

            // For the next part, we have to imagine that point A is fixed to (0,0)
            // and we will decide the relevant arc based on
            // 1. Whether we're on the short/long arc defined by AB
            // 2. Whether B is on the upper or lower side of the circle

            paramStart = 0;
            if (AC + BC < AB + Matrix.EQUALITY_MARGIN) // short side of the arc
            {
                double arcLength = AB;
                if (paramB > pi) // B is on the lower half of the circle => arc is B->A
                {
                    localVectorA = pointBInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }
                else // B is n the upper half of the circle => arc is A=>B
                {
                    localVectorA = pointAInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }

                paramEnd = arcLength;
            }
            else // long side of the arc
            {
                double arcLength = (2 * pi - AB) % (2 * pi);
                if (paramB > pi) // B is on the lower half of the circle => arc is A->B
                {
                    localVectorA = pointAInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }
                else // B is n the upper half of the circle => arc is B->A
                {
                    localVectorA = pointBInPlane.Direction;
                    localVectorB = planeNormal ^ localVectorA;
                }
                paramEnd = arcLength;
            }

            var _dirY = (planeNormal ^ _dirX).Direction;
            var _angle = paramEnd;

            return new CircleBuilder()
            {
                _angle = _angle,
                _center = _center,
                _radius = _radius,
                _dirX = _dirX,
                _dirY = _dirY,
            };
        }

        ICircleAxeChoice ICircleRadiusSet.WithDirectionX(Vect dirX)
        {
            if (dirX is null || dirX.Norm == 0)
                throw new InvalidGimElementException("Circle direction X cannot be a null vector!");
            _dirX = dirX.Direction;
            return this;
        }

        ICircleWithAxesSet ICircleAxeChoice.WithDirectionY(Vect dirY)
        {
            if (dirY is null || dirY.Norm == 0)
                throw new InvalidGimElementException("Circle direction Y cannot be a null vector!");
            if (dirY.IsCollinearTo(_dirX))
                throw new InvalidGimElementException("Circle Axes cannot be colinear!");
            _dirY = dirY;
            // removing X component from Y
            _dirY = (_dirY - _dirX * _dirY * _dirX).Direction;
            return this;
        }

        ICircleWithAxesSet ICircleAxeChoice.WithAxis(Vect axis)
        {
            if (axis is null || axis.Norm == 0)
                throw new InvalidGimElementException("Circle Axis cannot be a null vector!");
            if (axis.IsCollinearTo(_dirX))
                throw new InvalidGimElementException("Circle Axis cannot be colinear to its X direction");
            _dirY = (axis ^ _dirX).Direction;
            return this;
        }



        IElementBuilder ICircleWithAxesSet.WithAngleRange(double first, double last)
        {
            _angle = last - first;
            if (first != 0)
            {
                // Recompute dirX/dirY so params become [0, absAngle]
                Vect zAxis = (_dirX ^ _dirY).Direction;
                Vect adjDirX = Math.Cos(first) * _dirX + Math.Sin(first) * _dirY;
                Vect adjDirY = zAxis ^ adjDirX;
                _dirX = adjDirX.Direction;
                _dirY = adjDirY.Direction;
            }
            return this;
        }

        IElementBuilder ICircleWithAxesSet.WithTotalAngle(double totalAngle)
        {
            if (_angle < 0)
                throw new InvalidGimElementException("Total circle angle cannot be negative!");
            _angle = Math.Min(totalAngle, 2 * Math.PI);
            return this;
        }
    }

    public interface ICircleCenterSet
    {
        ICircleRadiusSet WithRadius(double radius);
    }

    public interface ICircleRadiusSet
    {
        ICircleAxeChoice WithDirectionX(Vect dirX);
    }

    public interface ICircleAxeChoice
    {
        ICircleWithAxesSet WithDirectionY(Vect dirY);
        ICircleWithAxesSet WithAxis(Vect axis);
    }

    public interface ICircleWithAxesSet : IElementBuilder
    {
        IElementBuilder WithAngleRange(double first, double last);
        IElementBuilder WithTotalAngle(double totalAngle);
    }
}
