using GIMcompress.ElementHelpers;

namespace GIMcompress
{
    public class ScaleChanger
    {
        public double Scale;

        public ScaleChanger(double scale)
        {
            Scale = scale;
        }

        /// <summary>
        /// Scales a GimData by a given scale factor. (for now, only modifies the polygon)
        /// </summary>
        /// <param name="scale">The scale to apply to GimData (1 for same scale)</param>
        public void ScaleGimData(GimData gimData)
        {
            for (int i = 0; i < gimData.Elements.Count; i++)
            {
                GimElement element = gimData.Elements[i];
                ScaleElement(element);

                foreach (GimLocal local in element.Locals)
                    ScaleLocal(local);
            }
        }

        /// <summary>
        /// Scales a gimElement.
        /// </summary>
        /// <param name="element">The element to scale.</param>
        public void ScaleElement(GimElement element)
        {
            if (element.ElementType.IsCurve())
            {
                ScaleCurve(element);
                return;
            }

            if (element.ElementType.IsFace())
            {
                ScaleFace(element);
                return;
            }

            if (element.ElementType == GimElementType.P_polygon)
            {
                ScalePolygon(element);
                return;
            }

            if (element.ElementType == GimElementType.Gim_Joint)
            {
                for (int i = 0; i < 6; i++)
                    element.ElementDoubles[i] *= Scale;
                return;
            }
        }

        /// <summary>
        /// Scales a face/surface element by the set scale.
        /// </summary>
        /// <param name="faceElement">the face element to scale</param>
        public void ScaleFace(GimElement faceElement)
        {
            switch (faceElement.ElementType)
            {
                case GimElementType.F_toroid:
                    faceElement.ElementDoubles[0] *= Scale;
                    faceElement.ElementDoubles[1] *= Scale;
                    break;
                case GimElementType.F_sphere:
                case GimElementType.F_offset:
                case GimElementType.F_cilinder:
                case GimElementType.F_cone:
                    faceElement.ElementDoubles[0] *= Scale; break;
                case GimElementType.F_bspline:
                    ScaleBSplineFace(faceElement); break;
                case GimElementType.F_trimmed:
                    for (int i = 0; i < faceElement.ElementDoubles.Length; i++)
                        faceElement.ElementDoubles[i] *= Scale;
                    break;
            }
        }

        /// <summary>
        /// Scales a curve element by the set scale.
        /// </summary>
        /// <param name="curveElement">the curve element to scale</param>
        public void ScaleCurve(GimElement curveElement)
        {
            switch (curveElement.ElementType)
            {
                case GimElementType.C_offset:
                case GimElementType.C_offset2D:
                case GimElementType.C_circle:
                case GimElementType.C_circle2D:
                    curveElement.ElementDoubles[0] *= Scale;
                    return;
                case GimElementType.C_trimmed:
                case GimElementType.C_trimmed2D:
                case GimElementType.C_line:
                case GimElementType.C_line2D:
                case GimElementType.C_elipse:
                case GimElementType.C_elipse2D:
                    curveElement.ElementDoubles[0] *= Scale;
                    curveElement.ElementDoubles[1] *= Scale;
                    return;
                case GimElementType.C_hyperbol:
                case GimElementType.C_hyperbol2D:
                    curveElement.ElementDoubles[2] *= Scale;
                    curveElement.ElementDoubles[3] *= Scale;
                    return;
                case GimElementType.C_parabol:
                case GimElementType.C_parabol2D:
                    curveElement.ElementDoubles[0] *= Scale;
                    curveElement.ElementDoubles[1] *= Scale;
                    curveElement.ElementDoubles[2] *= Scale;
                    return;
                case GimElementType.C_bspline:
                    ScaleBSplineCurve(curveElement);
                    return;
                case GimElementType.C_bspline2D:
                    ScaleBSplineCurve2D(curveElement);
                    return;
                case GimElementType.C_bezier2D:
                    ScaleBezierCurve2D(curveElement);
                    return;
            }
        }
        /// <summary>
        /// Scales a bspline Face by the set scale.
        /// </summary>
        /// <param name="bsplineFaceElement">The bspline face element to scale</param>
        public void ScaleBSplineFace(GimElement bsplineFaceElement)
        {
            GimBSplineFace bsplineFace = new(bsplineFaceElement);
            for (int i = 0; i < bsplineFace.PointCoords.Count; i++)
                bsplineFace.PointCoords[i] *= Scale;
        }

        public void ScaleBSplineCurve(GimElement bsplineCurveElement)
        {
            GimBSplineCurve bsplineCurve = new(bsplineCurveElement);
            for (int i = 0; i < bsplineCurve.PointCoords.Count; i++)
                bsplineCurve.PointCoords[i] *= Scale;
        }

        public void ScaleBezierCurve2D(GimElement bezierCurveElement)
        {
            GimBezier2DCurve bezierCurve = new(bezierCurveElement);
            for (int i = 0; i < bezierCurve.StartPoint.Count; i++)
            {
                bezierCurve.StartPoint[i] *= Scale;
                bezierCurve.EndPoint[i] *= Scale;
            }
            for (int i = 0; i < bezierCurve.PointCoords.Count; i++)
                bezierCurve.PointCoords[i] *= Scale;
        }

        public void ScaleBSplineCurve2D(GimElement bsplineCurveElement)
        {
            GimBSpline2DCurve bsplineCurve = new(bsplineCurveElement);
            for (int i = 0; i < bsplineCurve.StartPoint.Count; i++)
            {
                bsplineCurve.StartPoint[i] *= Scale;
                bsplineCurve.EndPoint[i] *= Scale;
            }
            for (int i = 0; i < bsplineCurve.PointCoords.Count; i++)
                bsplineCurve.PointCoords[i] *= Scale;
        }

        /// <summary>
        /// Scales a polygon by the set scale.
        /// </summary>
        /// <param name="polygonElement">the polygon element to scale</param>
        public void ScalePolygon(GimElement polygonElement)
        {
            for (int i = 0; i < polygonElement.ElementDoubles.Length; i++)
                polygonElement.ElementDoubles[i] *= Scale;
        }

        /// <summary>
        /// Scales a local
        /// </summary>
        /// <param name="local">The GimLocal to scale</param>
        public void ScaleLocal(GimLocal local)
        {
            local.Px *= Scale;
            local.Py *= Scale;
            local.Pz *= Scale;
        }

    }
}
