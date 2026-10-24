using GIMcompress;
using GIMocctCLI;
using GimUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GIMocctWrapper;
namespace GIMtransfer
{
    public static class GIMocctCurveWriter
    {
        public static TopoDSEdgeWrapper WriteCurveElement(GimData gimData, int elementIndex, int localIndex)
        {
            TopoDSEdgeWrapper finalEdge = null;
            GimElement element = gimData.Elements[elementIndex];
            GimLocal local = element.Locals[localIndex];

            switch (element.ElementType)
            {
                case GimElementType.C_line: finalEdge = WriteLine(element, local); break;
                case GimElementType.C_circle: finalEdge = WriteCircle(element, local); break;
                case GimElementType.C_elipse: finalEdge = WriteEllipse(element, local); break;
                case GimElementType.C_parabol: finalEdge = WriteParabola(element, local); break;
                case GimElementType.C_hyperbol: finalEdge = WriteHyperbola(element, local); break;
                case GimElementType.C_bspline: finalEdge = WriteBsplineCurve(element, local); break;
            }
            // orienting the edge correctly
            if(finalEdge != null) finalEdge.SetOrientation((int)(element.Orientation ? 
                TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD : 
                TopAbsWrapper_Orientation.TopAbsWrapper_REVERSED));
            return finalEdge;
        }

        public static TopoDSBSplineCurveWrapper WriteBsplineCurve(GimElement bsplineElement, GimLocal local)
        {
            CurveBuilder curveBuilder = new CurveBuilder();
            //(Vector3Wrapper center, Matrix4x4Wrapper matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            int degree = bsplineElement.ElementInts[^1];
            int knotsCount = bsplineElement.ElementInts[0];
            int multiplicitiesCount = bsplineElement.ElementInts.Length - 2;
            int polesCount = (bsplineElement.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

            int firstPoleIndex = 6;
            List<Vector3Wrapper> poles = new List<Vector3Wrapper>(polesCount);
            for (int i = 0; i < polesCount; i++)
            {
                int index = firstPoleIndex + i * 3;

                poles.Add(new Vector3Wrapper(
                    bsplineElement.ElementDoubles[index],
                    bsplineElement.ElementDoubles[index + 1],
                    bsplineElement.ElementDoubles[index + 2]
                ));

            }
            List<double> weights = new List<double>(polesCount);
            for (int i = 0; i < polesCount; i++)
            {
                weights.Add(bsplineElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6) + knotsCount]);
            }

            List<double> knotList = new(knotsCount);

            for (int i = 0; i < knotsCount; i++)
            {
                knotList.Add(bsplineElement.ElementDoubles[i + polesCount * 3 + (/*2 +*/6)]);
            }
            double paramStart = knotList[0];
            double paramEnd = knotList[0];

            List<int> multiplicities = new List<int>(multiplicitiesCount);
            for (int i = 0; i < multiplicitiesCount; i++)
            {
                multiplicities.Add(bsplineElement.ElementInts[i + 1]);
            }

            double start = knotList[0];
            double end = knotList[^1];

            int multSum = multiplicities.Sum();
            int aperiodicSum = multSum - degree - 1;
            int periodicSum = multSum - multiplicities[0];

            bool aperiodic = (aperiodicSum == polesCount);
            bool periodic;
            if (aperiodic)
            {
                periodic = false;
            }
            else
            {
                periodic = (periodicSum == polesCount);
            }

            if(periodic == aperiodic)
            {
                throw GimLogger.Error(new FormatException("Bspline curve is neither periodic nor aperiodic!"));
            }

            return curveBuilder.CreateBSplineCurve(poles, knotList, multiplicities, degree, start, end, periodic);

        }

        public static TopoDSHyperbolaWrapper WriteHyperbola(GimElement hyperbolaElement, GimLocal local)
        {
            CurveBuilder curveBuilder = new CurveBuilder();
            (Vector3Wrapper center, Matrix4x4Wrapper matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            (double start, double end) = (hyperbolaElement.ElementDoubles[0], hyperbolaElement.ElementDoubles[1]);
            (double majorRadius, double minorRadius) = (hyperbolaElement.ElementDoubles[2], hyperbolaElement.ElementDoubles[3]);
            return curveBuilder.CreateHyperbola(majorRadius, minorRadius, start, end, center, matrix);
        }

        public static TopoDSParabolaWrapper WriteParabola(GimElement parabolaElement, GimLocal local)
        {
            CurveBuilder curveBuilder = new CurveBuilder();
            (Vector3Wrapper center, Matrix4x4Wrapper matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double focal = parabolaElement.ElementDoubles[2];
            (double start, double end) = (parabolaElement.ElementDoubles[0], parabolaElement.ElementDoubles[1]);
            return curveBuilder.CreateParabola(focal, start, end, center, matrix);
        }

        public static TopoDSEllipseWrapper WriteEllipse(GimElement ellipseElement, GimLocal local)
        {
            CurveBuilder curveBuilder = new CurveBuilder();
            (Vector3Wrapper center, Matrix4x4Wrapper matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            (double start, double end) = (ellipseElement.ElementDoubles[2], ellipseElement.ElementDoubles[3]);
            (double majorRadius, double minorRadius) = (ellipseElement.ElementDoubles[0], ellipseElement.ElementDoubles[1]);
            return curveBuilder.CreateEllipse(majorRadius, minorRadius, start, end, center, matrix);
        }
        public static TopoDSCircleWrapper WriteCircle(GimElement circleElement, GimLocal local)
        {
            CurveBuilder curveBuilder = new CurveBuilder();
            (Vector3Wrapper center, Matrix4x4Wrapper matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double radius = circleElement.elementDoubles[0];
            (double start, double end) = (circleElement.ElementDoubles[1], circleElement.ElementDoubles[2]);
            return curveBuilder.CreateCircle(radius, start, end, center, matrix);
        }

        public static TopoDSLineWrapper WriteLine(GimElement lineElement, GimLocal local)
        {
            CurveBuilder curveBuilder = new CurveBuilder();
            (Vector3Wrapper center, Matrix4x4Wrapper matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            Vector3Wrapper direction = new Vector3Wrapper(local.Zx, local.Zy, local.Zz);
            (double start, double end) = (lineElement.ElementDoubles[0], lineElement.ElementDoubles[1]);
            return curveBuilder.CreateLine(start, end, center, direction);
        }

    }
}
