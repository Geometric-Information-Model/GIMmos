using GIMcompress;
using GIMocctCLI;
using GIMocctWrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMtransfer
{
    public static class GIMocctCurveReader
    {
        public static void ApplyOcctCurveToGimElement(GimElement element, GIMocctElement occtElement)
        {

            var innerShape = occtElement.Shape;
            if (innerShape is TopoDSLineWrapper) SetLineCurve(element, innerShape);
            else if (innerShape is TopoDSCircleWrapper) SetCircleCurve(element, innerShape);
            else if (innerShape is TopoDSBSplineCurveWrapper) SetBsplineCurve(element, innerShape);
            else if (innerShape is TopoDSEllipseWrapper) SetEllipseCurve(element, innerShape);
            else if (innerShape is TopoDSParabolaWrapper) SetParabolaCurve(element, innerShape);
            else if (innerShape is TopoDSHyperbolaWrapper) SetHyperbolaCurve(element, innerShape);
            else if (innerShape is TopoDSOffsetCurveWrapper) SetOffsetCurve(element, innerShape);
            else
            {

                var occtWriter = innerShape.GetWriter();
                Directory.CreateDirectory(FileAccess.GetTempPath("curves"));
                occtWriter.WriteStepFromMemory(FileAccess.GetTempPath("curves\\curve-UNKNOWN" + ".step"));
            }
            element.Orientation = (innerShape.GetOrientation() == (int)TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD);
        }

        private static void SetOffsetCurve(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var offsetShape = (TopoDSOffsetCurveWrapper)innerShape;
            element.ElementDoubles = new double[] { offsetShape.Offset()
            };
            element.ElementType = GimElementType.C_offset;

            Vector3Wrapper direction = offsetShape.Direction();
            Vector3Wrapper position = offsetShape.Position();

            // we're reading this shape, so we modify to last existing local
            element.Locals[^1].Px = position.X;
            element.Locals[^1].Py = position.Y;
            element.Locals[^1].Pz = position.Z;

            Vect dirVect = new Vect(direction.X, direction.Y, direction.Z);
            dirVect.ApplyToLocal(element.Locals[^1]);
        }

        private static void SetHyperbolaCurve(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var hyperbola = (TopoDSHyperbolaWrapper)innerShape;
            element.ElementDoubles = new double[] { hyperbola.FirstParameter(), hyperbola.LastParameter(),
                hyperbola.MajorRadius(), hyperbola.MinorRadius()
            };
            element.ElementType = GimElementType.C_hyperbol;

            Matrix4x4Wrapper shapeLocal = hyperbola.Location();
            GIMocctReadElements.ApplyMatrixWrapperToLocal(shapeLocal, element.Locals[^1]);
        }

        private static void SetParabolaCurve(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var parabolaShape = (TopoDSParabolaWrapper)innerShape;
            element.ElementDoubles = new double[] { parabolaShape.FirstParameter(), parabolaShape.LastParameter(), parabolaShape.Focal()
            };
            element.ElementType = GimElementType.C_parabol;

            Matrix4x4Wrapper shapeLocal = parabolaShape.Location();
            GIMocctReadElements.ApplyMatrixWrapperToLocal(shapeLocal, element.Locals[^1]);
        }

        private static void SetEllipseCurve(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var ellipseShape = (TopoDSEllipseWrapper)innerShape;
            element.ElementDoubles = new double[] { ellipseShape
                .MajorRadius(), ellipseShape.MinorRadius(),
                ellipseShape.FirstParameter(), ellipseShape.LastParameter() };
            element.ElementType = GimElementType.C_elipse;

            Matrix4x4Wrapper shapeLocal = ellipseShape.Location();
            GIMocctReadElements.ApplyMatrixWrapperToLocal(shapeLocal, element.Locals[^1]);
        }

        private static void SetCircleCurve(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var circleShape = (TopoDSCircleWrapper)innerShape;
            element.ElementDoubles = new double[] { circleShape.Radius(), circleShape.FirstParameter(), 
                circleShape.LastParameter() };
            element.ElementType = GimElementType.C_circle;

            Matrix4x4Wrapper shapeLocal = circleShape.Location();
            GIMocctReadElements.ApplyMatrixWrapperToLocal(shapeLocal, element.Locals[^1]);

        }
        private static void SetBsplineCurve(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var bsplineShape = (TopoDSBSplineCurveWrapper)innerShape;
            element.ElementType = GimElementType.C_bspline;
            int degree = bsplineShape.Degree();
            int poleCount = bsplineShape.NbPoles();
            int knotCount = bsplineShape.NbKnots();

            element.elementInts = new int[knotCount + 2];
            element.elementInts[0] = knotCount;
            element.elementInts[^1] = degree;

            for (int k = 1; k <= knotCount; k++)
            {
                element.elementInts[k] = bsplineShape.Multiplicity(k);
            }

            element.elementDoubles = new double[poleCount * (3 + 1) + knotCount + 6];

            int i = 6;

            for (int poleIndex = 1; poleIndex <= poleCount; poleIndex++)
            {
                Vector3Wrapper pole = bsplineShape.Pole(poleIndex);
                element.elementDoubles[i] = (pole.X); i++;
                element.elementDoubles[i] = (pole.Y); i++;
                element.elementDoubles[i] = (pole.Z); i++;
            }

            for (int knotIndex = 1; knotIndex <= knotCount; knotIndex++)
            {
                element.elementDoubles[i] = bsplineShape.Knot(knotIndex); i++;
            }



            for (int weightIndex = 1; weightIndex <= poleCount; weightIndex++)
            {
                element.elementDoubles[i] = bsplineShape.Weight(weightIndex); i++;
            }
        }


        private static void SetLineCurve(GimElement element, TopoDSAbstractShapeWrapper innerShape)
        {
            var lineShape = (TopoDSLineWrapper)innerShape;
            element.ElementDoubles = new double[] { lineShape.FirstParameter(), lineShape.LastParameter() };
            element.ElementType = GimElementType.C_line;

            Vector3Wrapper direction = lineShape.Direction();
            Vector3Wrapper position = lineShape.Position();

            // we're reading this shape, so we modify to last existing local
            element.Locals[^1].Px = position.X;
            element.Locals[^1].Py = position.Y;
            element.Locals[^1].Pz = position.Z;

            Vect dirVect = new Vect(direction.X, direction.Y, direction.Z);
            dirVect.ApplyToLocal(element.Locals[^1]);
        }
    }
}
