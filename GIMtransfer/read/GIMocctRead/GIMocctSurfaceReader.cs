using cadex;
using GIMcompress;
using GIMcompress.Shrinkwrap;
using GIMocctCLI;
using GIMocctWrapper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GIMtransfer
{
    public class GIMocctSurfaceReader
    {
        public static void ApplyOcctSurfaceToGimElement(GimElement element, GIMocctElement occtElement)
        {
            
            var innerShape = occtElement.Shape;
            element.Orientation = (TopAbsWrapper_Orientation)innerShape.GetOrientation() == TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD;
            if (innerShape is TopoDSPlaneWrapper) GetPlane(element, innerShape);
            else if (innerShape is TopoDSCylinderWrapper) GetCylinder(element, innerShape);
            else if (innerShape is TopoDSBsplineSurfaceWrapper) GetBsplineSurface(element, innerShape);
            else if (innerShape is TopoDSSphereWrapper) GetSphere(element, innerShape);
            else if (innerShape is TopoDSConeWrapper) GetCone(element, innerShape);
            else if (innerShape is TopoDSTorusWrapper) GetTorus(element, innerShape);
            else
            {

            }
        }


        private static void GetSphere(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var sphereWrapper = (TopoDSSphereWrapper)innerShape;
            element.ElementType = GimElementType.F_sphere;
            element.ElementDoubles = new double[] { sphereWrapper.Radius() };

            GIMocctReadElements.ApplyMatrixWrapperToLocal(sphereWrapper.Location(), element.Locals[^1]);
        }
        private static void GetTorus(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var torusWrapper = (TopoDSTorusWrapper)innerShape;
            element.ElementType = GimElementType.F_toroid;
            element.ElementDoubles = new double[] { torusWrapper.MajorRadius(), torusWrapper.MinorRadius() };

            GIMocctReadElements.ApplyMatrixWrapperToLocal(torusWrapper.Location(), element.Locals[^1]);
        }
        private static void GetCone(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var coneShape = (TopoDSConeWrapper)innerShape;
            element.ElementType = GimElementType.F_cone;
            element.ElementDoubles = new double[] { coneShape.RefRadius(), coneShape.SemiAngle() };

            GIMocctReadElements.ApplyMatrixWrapperToLocal(coneShape.Location(), element.Locals[^1]);
        }

        private static void GetPlane(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var planeShape = (TopoDSPlaneWrapper)innerShape;
            element.ElementType = GimElementType.F_plane;

            GIMocctReadElements.ApplyMatrixWrapperToLocal(planeShape.Location(), element.Locals[^1]);
        }

        private static void GetCylinder(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var cylinderShape = (TopoDSCylinderWrapper)innerShape;
            element.ElementType = GimElementType.F_cilinder;
            element.ElementDoubles = new double[] { cylinderShape.Radius() };

            GIMocctReadElements.ApplyMatrixWrapperToLocal(cylinderShape.Location(), element.Locals[^1]);
        }

        private static void GetBsplineSurface(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var bsplineShape = (TopoDSBsplineSurfaceWrapper)innerShape;
            element.ElementType = GimElementType.F_bspline;

            int uKnotCount = bsplineShape.NbUKnots();
            int vKnotCount = bsplineShape.NbVKnots();
            int knotCount = uKnotCount + vKnotCount;
            int uDegree = bsplineShape.UDegree();
            int vDegree = bsplineShape.VDegree();

            int vPoleCount = bsplineShape.NbVPoles();
            int uPoleCount = bsplineShape.NbUPoles();
            int poleCount = vPoleCount * uPoleCount;


            // Create element ints array
            element.elementInts = new int[
                  6
                + knotCount];

            // Set aBSpline constants
            element.elementInts[0] = poleCount;
            element.elementInts[1] = uDegree;
            element.elementInts[2] = vDegree;
            element.elementInts[3] = uKnotCount;
            element.elementInts[4] = uPoleCount;
            element.elementInts[5] = knotCount;

            // Create element doubles array
            element.elementDoubles = new double[
                  poleCount * (3 + 1) // 3 for coordinates, and one for weight
                + uKnotCount
                + vKnotCount];

            int i; // Counter
            i = 6; // number of specials
            for (int uIndex = 1; uIndex <= uKnotCount; uIndex++) // U multiplicties
            {
                element.elementInts[i] = bsplineShape.UMultiplicity(uIndex);
                i++;
            }
            for (int vIndex = 1; vIndex <= vKnotCount; vIndex++) // V multiplicties
            {
                element.elementInts[i] = bsplineShape.VMultiplicity(vIndex);
                i++;
            }

            i = 0;
            // pole XYZ values
            for (int uIndex = 1; uIndex <= uPoleCount; uIndex++)
            {
                for(int vIndex = 1; vIndex <= vPoleCount; vIndex++)
                {
                    var p = bsplineShape.Pole(uIndex, vIndex);
                    element.elementDoubles[i] = p.X; i++;
                    element.elementDoubles[i] = p.Y; i++;
                    element.elementDoubles[i] = p.Z; i++;
                }
            }

            for (int uIndex = 1; uIndex <= uPoleCount; uIndex++) // weights
            {
                for (int vIndex = 1; vIndex <= vPoleCount; vIndex++)
                {
                    element.elementDoubles[i] = bsplineShape.Weight(uIndex, vIndex); i++;
                }
            }

            for (int uIndex = 1; uIndex <= uKnotCount; uIndex++) // U knots
            {
                element.elementDoubles[i] = bsplineShape.UKnot(uIndex);
                i++;
            }
            for (int vIndex = 1; vIndex <= vKnotCount; vIndex++) // V knots
            {
                element.elementDoubles[i] = bsplineShape.VKnot(vIndex);
                i++;
            }

        }
    }
}
