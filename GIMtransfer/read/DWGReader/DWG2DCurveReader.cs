using ACadSharp.Entities;
using ACadSharp.Objects;
using CSMath;
using GIMcompress;
using GIMcompress.ElementHelpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GIMtransfer.read.DWGReader;

// Reads 2D/3D curve DWG entities using ElementBuilders (Line2DBuilder, CircleBuilder, etc.).
// Detects R_2DRep vs R_BRep parent to choose between 2D (C_*2D) and 3D (C_*) element types.
// Uses Is2DRep recursive check to handle wire-parented curves (polylines/hatches dispatch).

public static class DWG2DCurveReader
{
    /// <summary>Fallback half-length (model units) when clamping Ray/XLine to a finite segment.</summary>
    public const double DefaultXLineLength = 1000.0;

    /// <summary>
    /// Walks parent locals to decide whether geometry should use 2D builders (<see cref="GimElementType.R_2DRep"/>)
    /// or 3D builders (<see cref="GimElementType.R_BRep"/>).
    /// </summary>
    private static bool Is2DRep(GimData gimData, uint elementIndex)
    {
        var element = gimData.Elements[(int)elementIndex];
        if (element.ElementType == GimElementType.R_2DRep) return true;
        if (element.ElementType == GimElementType.R_BRep) return false;
        if (element.Locals != null && element.Locals.Count > 0)
            return Is2DRep(gimData, element.Locals[0].Parent);
        return false;
    }

    /// <summary>
    /// Builds a circular or elliptical arc under <paramref name="bRepIndex"/>.
    /// When <paramref name="addMaterial"/> is true, attaches stroke color via <see cref="DWG2DMaterialReader"/>.
    /// </summary>
    public static void ReadArc(GimData gimData, uint bRepIndex, Arc arc, bool addMaterial)
    {
        double radius = arc.Radius;
        double radiusRatio = arc.RadiusRatio;
        Vect center = DWGHelpers.XYZtoVect(arc.Center);

        double startAngle = arc.StartAngle;
        double endAngle = arc.EndAngle;
        // ACadSharp can return start > end for arcs that cross 0; unwrap into a continuous range.
        if (startAngle > endAngle)
            endAngle += 2 * Math.PI;

        GimElement gimArc;
        bool is2D = Is2DRep(gimData, bRepIndex);

        if (is2D)
        {
            if (radiusRatio == 1)
            {
                gimArc = Circle2DBuilder.FromCenter(center.X, center.Y)
                    .WithRadius(radius)
                    .WithDirectionU(1, 0)
                    .WithDirectionV(0, 1)
                    .WithAngleRange(startAngle, endAngle)
                    .BuildElement();
            }
            else
            {
                double radiusX = radius;
                double radiusY = radius * radiusRatio;
                gimArc = Ellipse2DBuilder.FromCenter(center.X, center.Y)
                    .WithRadii(radiusX, radiusY)
                    .WithDirectionU(1, 0)
                    .WithDirectionV(0, 1)
                    .WithAngleRange(startAngle, endAngle)
                    .BuildElement();
            }
        }
        else
        {
            // 3D path: ACadSharp exposes only the extrusion Normal, not a stable OCS X axis.
            // Build an orthonormal frame by crossing Normal with a reference axis that is not
            // nearly parallel to it. This is intentional (not AAA); rotating the Normal near the
            // |Nz|=0.9 threshold can flip the chosen refDir and jump the arc start point.
            Vect normal = DWGHelpers.XYZtoVect(arc.Normal).Direction;
            Vect refDir = Math.Abs(normal.Z) < 0.9 ? new Vect(0, 0, 1) : new Vect(1, 0, 0);
            Vect dirX = (normal ^ refDir).Direction;
            Vect dirY = normal ^ dirX;

            if (radiusRatio == 1)
            {
                gimArc = CircleBuilder.FromCenter(center)
                    .WithRadius(radius)
                    .WithDirectionX(dirX)
                    .WithDirectionY(dirY)
                    .WithAngleRange(startAngle, endAngle)
                    .BuildElement();
            }
            else
            {
                double radiusX = radius;
                double radiusY = radius * radiusRatio;
                gimArc = EllipseBuilder.FromCenter(center)
                    .WithRadii(radiusX, radiusY)
                    .WithDirections(dirX, dirY)
                    .WithAngleRange(startAngle, endAngle)
                    .BuildElement();
            }
        }

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimArc);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
        // Materials on curves are provisional: preserved for import; full output-flow support TBD.
        if (addMaterial)
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, arc);
    }

    /// <summary>
    /// Builds a full circle under <paramref name="bRepIndex"/> (2D or 3D builders based on parent rep).
    /// </summary>
    public static void ReadCircle(GimData gimData, uint bRepIndex, Circle circle, bool addMaterial)
    {
        double radius = circle.Radius;
        Vect center = DWGHelpers.XYZtoVect(circle.Center);

        GimElement gimCircle;
        bool is2D = Is2DRep(gimData, bRepIndex);

        if (is2D)
        {
            gimCircle = Circle2DBuilder.FromCenter(center.X, center.Y)
                .WithRadius(radius)
                .WithDirectionU(1, 0)
                .WithDirectionV(0, 1)
                .WithTotalAngle(2 * Math.PI)
                .BuildElement();
        }
        else
        {
            // Same Normal-only orthonormal frame as ReadArc (see comment there).
            Vect normal = DWGHelpers.XYZtoVect(circle.Normal).Direction;
            Vect refDir = Math.Abs(normal.Z) < 0.9 ? new Vect(0, 0, 1) : new Vect(1, 0, 0);
            Vect dirX = (normal ^ refDir).Direction;
            Vect dirY = normal ^ dirX;

            gimCircle = CircleBuilder.FromCenter(center)
                .WithRadius(radius)
                .WithDirectionX(dirX)
                .WithDirectionY(dirY)
                .WithTotalAngle(2 * Math.PI)
                .BuildElement();
        }

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimCircle);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
        if (addMaterial)
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, circle);
    }

    /// <summary>
    /// Builds a single line segment from the first and last points of <paramref name="verts"/>.
    /// </summary>
    public static void ReadLineElement(GimData gimData, uint bRepIndex, List<Vect> verts, Entity sourceEntity, bool addMaterial)
    {
        if (verts == null || verts.Count < 2)
            return;

        Vect start = verts[0];
        Vect end = verts[^1];

        GimElement element;
        bool is2D = Is2DRep(gimData, bRepIndex);

        if (is2D)
            element = Line2DBuilder.From(start.X, start.Y).To(end.X, end.Y).BuildElement();
        else
            element = LineBuilder.From(start).To(end).BuildElement();

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(element);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
        if (addMaterial)
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, sourceEntity);
    }

    /// <summary>Reads a DWG <see cref="Line"/> into a GIM line element.</summary>
    public static void ReadLine(GimData gimData, uint bRepIndex, Line line, bool addMaterial)
    {
        var verts = new List<Vect>
        {
            DWGHelpers.XYZtoVect(line.StartPoint),
            DWGHelpers.XYZtoVect(line.EndPoint)
        };

        ReadLineElement(gimData, bRepIndex, verts, line, addMaterial);
    }

    /// <summary>
    /// Builds a wire of line/arc segments from polyline vertices.
    /// Vertex Z encodes LWPOLYLINE bulge (0 = straight); non-zero Z triggers bulge-to-arc conversion.
    /// </summary>
    /// <remarks>
    /// Wire <c>ElementBools</c>: [0]=isClosed (SVG Z), [1]=isFilled — consumed by SvgWriter.
    /// </remarks>
    public static void ReadPolylineElement(
       GimData gimData,
       uint bRepIndex,
       List<Vect> verts,
       Entity sourceEntity,
       bool isClosed = false,
       bool addMaterial = true,
       bool isFilled = false)
    {
        if (verts == null || verts.Count < 2)
            return;

        GimElement wireElement = new()
        {
            ElementType = GimElementType.wire,
            // [0]=closed, [1]=filled — see SvgWriter wire path handling.
            ElementBools = new int[2] { isClosed ? 1 : 0, isFilled ? 1 : 0 }
        };

        uint wireIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(wireElement);
        gimData.AddChildToParent(wireIndex, bRepIndex);

        for (int i = 0; i < verts.Count - 1; i++)
        {
            int nextIndex = i + 1;
            Vect startPt = verts[i];
            Vect endPt = verts[nextIndex];

            if (startPt.Z == 0)
            {
                ReadLineElement(gimData, wireIndex, new List<Vect> { startPt, endPt }, sourceEntity, false);
                continue;
            }

            // Z holds LWPOLYLINE bulge; convert chord+bulge into a circular arc segment.
            double bulge = startPt.Z;
            double dx = endPt.X - startPt.X;
            double dy = endPt.Y - startPt.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);

            if (chord == 0)
                continue;

            double includedAngle = 4 * Math.Atan(bulge);
            double radius = chord / (2.0 * Math.Sin(Math.Abs(includedAngle) / 2.0));

            if (Math.Abs(radius) < 1e-12)
                continue;

            double midX = (startPt.X + endPt.X) / 2.0;
            double midY = (startPt.Y + endPt.Y) / 2.0;

            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length == 0)
                continue;

            double nx = -dy / length;
            double ny = dx / length;

            if (bulge < 0)
            {
                nx = -nx;
                ny = -ny;
            }

            double offset = Math.Sqrt(radius * radius - (chord * chord) / 4.0);

            Vect center = new(midX + nx * offset, midY + ny * offset, 0);

            double startAngle = Math.Atan2(startPt.Y - center.Y, startPt.X - center.X);
            double endAngle = Math.Atan2(endPt.Y - center.Y, endPt.X - center.X);

            if (bulge > 0 && endAngle < startAngle)
                endAngle += 2 * Math.PI;
            else if (bulge < 0 && startAngle < endAngle)
                startAngle += 2 * Math.PI;

            bool is2D = Is2DRep(gimData, bRepIndex);
            GimElement arcElement;

            if (is2D)
            {
                arcElement = Circle2DBuilder.FromCenter(center.X, center.Y)
                    .WithRadius(Math.Abs(radius))
                    .WithDirectionU(1, 0)
                    .WithDirectionV(0, 1)
                    .WithAngleRange(startAngle, endAngle)
                    .BuildElement();
            }
            else
            {
                // Bulge sign chooses arc Normal (±Z); frame is otherwise fixed in XY.
                Vect arcNormal = new(0, 0, bulge > 0 ? 1 : -1);
                Vect refDir = new(1, 0, 0);
                Vect dirX = (arcNormal ^ refDir).Direction;
                Vect dirY = arcNormal ^ dirX;

                arcElement = CircleBuilder.FromCenter(center)
                    .WithRadius(Math.Abs(radius))
                    .WithDirectionX(dirX)
                    .WithDirectionY(dirY)
                    .WithAngleRange(startAngle, endAngle)
                    .BuildElement();
            }

            uint arcElementIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(arcElement);
            gimData.AddChildToParent(arcElementIndex, wireIndex);
        }

        if (addMaterial)
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, wireIndex, sourceEntity);
    }

    /// <summary>Reads a <see cref="Polyline2D"/> (bulge-aware vertices) into a wire.</summary>
    public static void ReadPolyline2D(GimData gimData, uint bRepIndex, Polyline2D polyline2D, bool addMaterial)
    {
        var verts = new List<Vect>(polyline2D.Vertices.Count);
        foreach (Vertex2D v in polyline2D.Vertices)
        {
            if (v.Bulge == 0)
                verts.Add(DWGHelpers.XYZtoVect(v.Location));
            else
                verts.Add(DWGHelpers.XYZWithBulgeToVect(v));
        }
        ReadPolylineElement(gimData, bRepIndex, verts, polyline2D, isClosed: polyline2D.IsClosed, addMaterial: addMaterial);
    }

    /// <summary>Reads an <see cref="LwPolyline"/> into a wire (not filled).</summary>
    public static void ReadLwPolyline(GimData gimData, uint bRepIndex, LwPolyline lwPolyline)
    {
        var verts = new List<Vect>(lwPolyline.Vertices.Count);
        foreach (var v in lwPolyline.Vertices)
            verts.Add(DWGHelpers.XYZWithBulgeToVect(v));

        ReadPolylineElement(gimData, bRepIndex, verts, lwPolyline, isClosed: lwPolyline.IsClosed, isFilled: false, addMaterial: true);
    }

    /// <summary>
    /// Reads an <see cref="Ellipse"/> (full or partial) using 2D axes from Rotation or a 3D Normal-derived frame.
    /// </summary>
    public static void ReadEllipse(GimData gimData, uint bRepIndex, Ellipse ellipse, bool addMaterial)
    {
        bool isClosed = ellipse.IsFullEllipse;
        Vect center = DWGHelpers.XYZtoVect(ellipse.Center);

        double majorRadius = ellipse.MajorAxis;
        double minorRadius = ellipse.MinorAxis;
        double rotation = ellipse.Rotation;

        double startParam = ellipse.StartParameter;
        double endParam = ellipse.EndParameter;

        double startAngle = isClosed ? 0.0 : startParam;
        double endAngle = isClosed ? 2 * Math.PI : endParam;
        if (startAngle > endAngle)
            endAngle += 2 * Math.PI;

        GimElement gimEllipse;
        bool is2D = Is2DRep(gimData, bRepIndex);

        if (is2D)
        {
            double cosR = Math.Cos(rotation);
            double sinR = Math.Sin(rotation);
            gimEllipse = Ellipse2DBuilder.FromCenter(center.X, center.Y)
                .WithRadii(majorRadius, minorRadius)
                .WithDirectionU(cosR, sinR)
                .WithDirectionV(-sinR, cosR)
                .WithAngleRange(startAngle, endAngle)
                .BuildElement();
        }
        else
        {
            // Same Normal-only orthonormal frame as ReadArc (see comment there).
            Vect normal = DWGHelpers.XYZtoVect(ellipse.Normal).Direction;
            Vect refDir = Math.Abs(normal.Z) < 0.9 ? new Vect(0, 0, 1) : new Vect(1, 0, 0);
            Vect dirX = (normal ^ refDir).Direction;
            Vect dirY = normal ^ dirX;

            gimEllipse = EllipseBuilder.FromCenter(center)
                .WithRadii(majorRadius, minorRadius)
                .WithDirections(dirX, dirY)
                .WithAngleRange(startAngle, endAngle)
                .BuildElement();
        }

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimEllipse);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
        if (addMaterial)
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, ellipse);
    }

    /// <summary>
    /// Dispatches a DWG <see cref="Spline"/> to Bezier segments (no knots) or a B-spline (control points + knots).
    /// </summary>
    public static void ReadSpline(GimData gimData, uint bRepIndex, Spline spline, bool addMaterial)
    {
        if (spline.ControlPoints.Count == 0 && spline.FitPoints.Count == 0 && spline.Knots.Count == 0)
            return;

        int degree = spline.Degree;

        if (degree < 1 || degree > 20)
            return;

        if (spline.Knots.Count == 0 && spline.FitPoints.Count == 0 && spline.ControlPoints.Count >= degree + 1)
        {
            ReadBezierSpline(gimData, bRepIndex, spline, addMaterial);
            return;
        }

        if (spline.ControlPoints.Count == 0 && spline.Knots.Count == 0 && spline.FitPoints.Count >= 2)
            spline.UpdateFromFitPoints();

        ReadBSpline(gimData, bRepIndex, spline, addMaterial);
    }

    private static void ReadBezierSpline(GimData gimData, uint bRepIndex, Spline spline, bool addMaterial)
    {
        int degree = spline.Degree;
        if (spline.ControlPoints.Count < degree + 1)
            return;

        bool is2D = Is2DRep(gimData, bRepIndex);
        int numSegments = (spline.ControlPoints.Count - 1) / degree;

        for (int seg = 0; seg < numSegments; seg++)
        {
            int startIdx = seg * degree;
            int endIdx = startIdx + degree;
            Vect startPoint = DWGHelpers.XYZtoVect(spline.ControlPoints[startIdx]);
            Vect endPoint = DWGHelpers.XYZtoVect(spline.ControlPoints[endIdx]);

            Vect[] poles = new Vect[degree + 1];
            for (int i = 0; i <= degree; i++)
            {
                var cp = spline.ControlPoints[startIdx + i];
                if (is2D)
                    poles[i] = new Vect(cp.X, cp.Y);
                else
                    poles[i] = new Vect(cp.X, cp.Y, cp.Z);
            }

            GimElement gimBezier;
            if (is2D)
            {
                gimBezier = Bezier2DBuilder.FromControlPoints(poles)
                    .WithParamRange(0, 1)
                    .WithEndpoints(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y)
                    .BuildElement();
            }
            else
            {
                gimBezier = BezierBuilder.FromControlPoints(poles)
                    .WithParamRange(0, 1)
                    .WithEndpoints(startPoint, endPoint)
                    .BuildElement();
            }

            uint gimElementIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(gimBezier);
            gimData.AddChildToParent(gimElementIndex, bRepIndex);

            if (addMaterial)
                DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, spline);
        }
    }

    private static void ReadBSpline(GimData gimData, uint bRepIndex, Spline spline, bool addMaterial)
    {
        int degree = spline.Degree;
        if (spline.ControlPoints.Count < 2 || spline.Knots.Count < spline.ControlPoints.Count + degree + 1)
            return;

        bool is2D = Is2DRep(gimData, bRepIndex);
        int poleCount = spline.ControlPoints.Count;

        Vect[] poles = new Vect[poleCount];
        for (int i = 0; i < poleCount; i++)
        {
            var cp = spline.ControlPoints[i];
            if (is2D)
                poles[i] = new Vect(cp.X, cp.Y);
            else
                poles[i] = new Vect(cp.X, cp.Y, cp.Z);
        }

        double[] weights;
        if (spline.Weights.Count > 0)
        {
            weights = spline.Weights.ToArray();
            if (weights.Length != poleCount)
                weights = Enumerable.Repeat(1.0, poleCount).ToArray();
        }
        else
        {
            weights = Enumerable.Repeat(1.0, poleCount).ToArray();
        }

        double[] knots = spline.Knots.OrderBy(k => k).ToArray();
        double knotTolerance = spline.KnotTolerance;
        var uniqueKnots = new List<double>();
        var multiplicities = new List<int>();

        foreach (var k in knots)
        {
            if (uniqueKnots.Count == 0 || Math.Abs(uniqueKnots[^1] - k) > knotTolerance)
            {
                uniqueKnots.Add(k);
                multiplicities.Add(1);
            }
            else
            {
                multiplicities[^1]++;
            }
        }

        Vect startPoint = DWGHelpers.XYZtoVect(spline.ControlPoints[0]);
        Vect endPoint = DWGHelpers.XYZtoVect(spline.ControlPoints[^1]);

        GimElement gimBSpline;
        if (is2D)
        {
            gimBSpline = BSpline2DBuilder.FromPoles(poles)
                .WithWeights(weights)
                .WithKnots(uniqueKnots.ToArray())
                .WithMultiplicities(multiplicities.ToArray())
                .WithDegree(degree)
                .WithEndpoints(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y)
                .BuildElement();
        }
        else
        {
            gimBSpline = BSplineBuilder.FromPoles(poles)
                .WithWeights(weights)
                .WithKnots(uniqueKnots.ToArray())
                .WithMultiplicities(multiplicities.ToArray())
                .WithDegree(degree)
                .WithEndpoints(startPoint, endPoint)
                .BuildElement();
        }

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimBSpline);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
        if (addMaterial)
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, spline);
    }

    /// <summary>
    /// Clamps a DWG <see cref="Ray"/> to a finite line segment for GIM (which has no infinite geometry).
    /// Picks the smallest positive intersection scale of the ray with its ACadSharp bounding box;
    /// falls back to <see cref="DefaultXLineLength"/> when the box yields no usable scale.
    /// True infinite rays remain a follow-up.
    /// </summary>
    public static void ReadRay(GimData gimData, uint bRepIndex, Ray ray)
    {
        Vect start = DWGHelpers.XYZtoVect(ray.StartPoint);
        Vect direction = DWGHelpers.XYZtoVect(ray.Direction);

        var bbox = ray.GetBoundingBox();
        List<double> scaleFactors = new();

        if (IsNonZero(direction.X))
        {
            if (!double.IsInfinity(bbox.Min.X) && !double.IsNaN(bbox.Min.X))
                scaleFactors.Add((bbox.Min.X - start.X) / direction.X);
            if (!double.IsInfinity(bbox.Max.X) && !double.IsNaN(bbox.Max.X))
                scaleFactors.Add((bbox.Max.X - start.X) / direction.X);
        }

        if (IsNonZero(direction.Y))
        {
            if (!double.IsInfinity(bbox.Min.Y) && !double.IsNaN(bbox.Min.Y))
                scaleFactors.Add((bbox.Min.Y - start.Y) / direction.Y);
            if (!double.IsInfinity(bbox.Max.Y) && !double.IsNaN(bbox.Max.Y))
                scaleFactors.Add((bbox.Max.Y - start.Y) / direction.Y);
        }

        if (IsNonZero(direction.Z))
        {
            if (!double.IsInfinity(bbox.Min.Z) && !double.IsNaN(bbox.Min.Z))
                scaleFactors.Add((bbox.Min.Z - start.Z) / direction.Z);
            if (!double.IsInfinity(bbox.Max.Z) && !double.IsNaN(bbox.Max.Z))
                scaleFactors.Add((bbox.Max.Z - start.Z) / direction.Z);
        }

        // Shortest positive hit keeps the finite segment as small as the bbox allows.
        double selectedScale = scaleFactors
            .Where(val => val > 0 && !double.IsInfinity(val) && !double.IsNaN(val))
            .DefaultIfEmpty(DefaultXLineLength)
            .Min();

        var end = new Vect(
            start.X + direction.X * selectedScale,
            start.Y + direction.Y * selectedScale,
            start.Z + direction.Z * selectedScale
        );

        var verts = new List<Vect> { start, end };

        bool is2D = Is2DRep(gimData, bRepIndex);
        GimElement gimRay =
            is2D
                ? Line2DBuilder.From(start.X, start.Y).To(end.X, end.Y).BuildElement()
                : LineBuilder.From(start).To(end).BuildElement();

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimRay);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, ray);
    }

    /// <summary>
    /// Approximates a DWG construction line (<see cref="XLine"/>) as a finite segment of total length
    /// <c>2 * <paramref name="length"/></c> centered on the base point. Skips degenerate directions.
    /// </summary>
    public static void ReadXLine(GimData gimData, uint bRepIndex, XLine xline, double length = DefaultXLineLength)
    {
        Vect basePt = DWGHelpers.XYZtoVect(xline.FirstPoint);
        Vect dir = DWGHelpers.XYZtoVect(xline.Direction);

        double mag = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
        // Ignore near-zero direction vectors (degenerate XLine) — no usable segment to emit.
        if (mag < 1e-8) return;

        dir = new Vect(dir.X / mag, dir.Y / mag, dir.Z / mag);

        Vect p1 = new(basePt.X - dir.X * length,
                       basePt.Y - dir.Y * length,
                       basePt.Z - dir.Z * length);

        Vect p2 = new(basePt.X + dir.X * length,
                       basePt.Y + dir.Y * length,
                       basePt.Z + dir.Z * length);

        var verts = new List<Vect> { p1, p2 };

        bool is2D = Is2DRep(gimData, bRepIndex);
        GimElement gimXLine =
            is2D
                ? Line2DBuilder.From(p1.X, p1.Y).To(p2.X, p2.Y).BuildElement()
                : LineBuilder.From(p1).To(p2).BuildElement();

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimXLine);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, gimElementIndex, xline);
    }

    /// <summary>
    /// True when <paramref name="value"/> is a finite magnitude above <paramref name="tolerance"/>.
    /// NaN and ±Infinity count as "zero" here so Ray bbox scaling never divides by unusable components.
    /// </summary>
    private static bool IsNonZero(double value, double tolerance = 1e-6)
    {
        return !double.IsNaN(value) &&
               !double.IsInfinity(value) &&
               Math.Abs(value) > tolerance;
    }
}
