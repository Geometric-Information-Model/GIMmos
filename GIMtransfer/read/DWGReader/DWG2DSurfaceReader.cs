using ACadSharp.Entities;
using ACadSharp.Objects;
using CSMath;
using GIMcompress;
using GIMcompress.ElementHelpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GIMtransfer.read.DWGReader;

/// <summary>
/// Reads DWG surface-like 2D entities (SOLID, MLINE, HATCH) into GIM wires / planes / hatch patterns.
/// Lives beside hatch/solid dispatch even when an entity reuses curve helpers (e.g. MLINE).
/// </summary>
public static class DWG2DSurfaceReader
{
    /// <summary>
    /// Reads a DWG <see cref="Solid"/> as a closed (optionally filled) wire of 2D line segments.
    /// </summary>
    public static void ReadSolid(GimData gimData, uint parentIndex, Solid solid, bool addMaterial = true, bool isFilled = true)
    {
        List<Vect> points = DWGHelpers.SolidPointsToVectList(solid);
        if (points == null || points.Count < 3)
            return;

        // ACadSharp stores a triangle as 4 corners with points[2]==points[3]; otherwise treat as quad.
        bool isTriangle = points.Count == 4 && points[2].Equals(points[3]);
        int segmentCount = isTriangle ? 3 : points.Count;

        if (!isTriangle && points.Count == 4)
        {
            // ACadSharp corner order is not sequential boundary winding; remap to 0→2→3→1 for a closed quad.
            List<Vect> reordered = new() { points[0], points[2], points[3], points[1] };
            points = reordered;
        }

        GimElement wireElement = new()
        {
            ElementType = GimElementType.wire,
            // [0]=closed (always for Solid), [1]=filled — matches SvgWriter wire semantics.
            ElementBools = new int[2] { 1, isFilled ? 1 : 0 }
        };

        uint wireIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(wireElement);
        gimData.AddChildToParent(wireIndex, parentIndex);

        for (int i = 0; i < segmentCount; i++)
        {
            Vect start = points[i];
            Vect end = points[(i + 1) % segmentCount];

            GimElement lineElement = Line2DBuilder.From(start.X, start.Y).To(end.X, end.Y).BuildElement();

            uint lineIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(lineElement);
            gimData.AddChildToParent(lineIndex, wireIndex);
        }

        if (addMaterial)
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, wireIndex, solid);
    }

    /// <summary>
    /// Reads a DWG multiline as an open polyline wire.
    /// Kept here (not in <see cref="DWG2DCurveReader"/>) because MLINE is dispatched with hatch/solid
    /// surface entities; implementation reuses <see cref="DWG2DCurveReader.ReadPolylineElement"/>.
    /// </summary>
    public static void ReadMLine(GimData gimData, uint bRepIndex, MLine mline)
    {
        var verts = new List<Vect>(mline.Vertices.Count);
        foreach (var v in mline.Vertices)
            verts.Add(DWGHelpers.XYZtoVect(v.Position));

        DWG2DCurveReader.ReadPolylineElement(gimData, bRepIndex, verts, mline, isClosed: false, isFilled: false, addMaterial: true);
    }

    /// <summary>
    /// Reads a DWG <see cref="Hatch"/> as an <c>F_plane</c> with optional pattern Hatch child and closed
    /// boundary wires (edges delegated to curve readers). HatchElementBuilder remains a follow-up.
    /// </summary>
    public static void ReadHatch(GimData gimData, uint bRepIndex, Hatch hatch)
    {
        if (hatch.Paths.Count == 0)
            return;

        GimElement planeElement = new()
        {
            ElementType = GimElementType.F_plane
        };

        uint planeIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(planeElement);
        gimData.AddChildToParent(planeIndex, bRepIndex);

        bool hasPattern = !hatch.IsSolid && hatch.Pattern.Lines.Count > 0;

        if (hasPattern)
        {
            // Raw GimElement until a HatchElementBuilder exists.
            GimElement hatchElement = new()
            {
                ElementType = GimElementType.Hatch,
                ElementDoubles = BuildHatchDoubles(hatch),
                Locals = BuildHatchGimLocal(hatch, planeIndex)
            };
            uint hatchIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(hatchElement);
            gimData.AddChildToParent(hatchIndex, planeIndex, localIndex: 0);

            AddHatchPatternMaterial(gimData, hatchIndex, hatch);
        }

        foreach (var path in hatch.Paths)
        {
            GimElement wireElement = new()
            {
                ElementType = GimElementType.wire,
                // Closed boundary path, not filled here (fill comes from solid hatch / pattern).
                ElementBools = new int[2] { 1, 0 }
            };

            uint wireIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(wireElement);
            gimData.AddChildToParent(wireIndex, planeIndex);

            foreach (var edge in path.Edges)
            {
                switch (edge.Type)
                {
                    case Hatch.BoundaryPath.EdgeType.Line:
                        if (edge is Hatch.BoundaryPath.Line lineEdge)
                        {
                            var lineEntity = (Line)lineEdge.ToEntity();
                            DWG2DCurveReader.ReadLine(gimData, wireIndex, lineEntity, false);
                        }
                        break;

                    case Hatch.BoundaryPath.EdgeType.CircularArc:
                        if (edge is Hatch.BoundaryPath.Arc arcEdge)
                        {
                            var arcEntity = (Arc)arcEdge.ToEntity();
                            DWG2DCurveReader.ReadArc(gimData, wireIndex, arcEntity, false);
                        }
                        break;

                    case Hatch.BoundaryPath.EdgeType.EllipticArc:
                        if (edge is Hatch.BoundaryPath.Ellipse ellipseEdge)
                        {
                            var ellipseEntity = (Ellipse)ellipseEdge.ToEntity();
                            DWG2DCurveReader.ReadEllipse(gimData, wireIndex, ellipseEntity, false);
                        }
                        break;

                    case Hatch.BoundaryPath.EdgeType.Spline:
                        if (edge is Hatch.BoundaryPath.Spline splineEdge)
                        {
                            var splineEntity = (Spline)splineEdge.ToEntity();
                            DWG2DCurveReader.ReadSpline(gimData, wireIndex, splineEntity, false);
                        }
                        break;

                    case Hatch.BoundaryPath.EdgeType.Polyline:
                        if (edge is Hatch.BoundaryPath.Polyline polylineEdge)
                        {
                            var polylineEntity = (Polyline2D)polylineEdge.ToEntity();
                            DWG2DCurveReader.ReadPolyline2D(gimData, wireIndex, polylineEntity, false);
                        }
                        break;
                }
            }
        }

        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, planeIndex, hatch);
    }

    private static void AddHatchPatternMaterial(GimData gimData, uint hatchIndex, Hatch hatch)
    {
        var patternLines = hatch.Pattern.Lines;
        if (patternLines == null || patternLines.Count == 0)
            return;

        foreach (var patternLine in hatch.Pattern.Lines)
        {
            double patternScale = hatch.PatternScale;
            double patternAngle = hatch.PatternAngle;

            var basePt = patternLine.BasePoint;
            var offset = patternLine.Offset;

            double lineAngle = patternLine.Angle + patternAngle;
            double length = Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y) * patternScale;

            double startX = basePt.X;
            double startY = basePt.Y;
            double endX = basePt.X + Math.Cos(lineAngle) * length;
            double endY = basePt.Y + Math.Sin(lineAngle) * length;

            GimElement lineElement = Line2DBuilder.From(startX, startY).To(endX, endY).BuildElement();

            uint lineIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(lineElement);
            gimData.AddChildToParent(lineIndex, hatchIndex);

            double[] dashDoubles = BuildHatchPatternLineDoubles(hatch, patternLine);
            GimElement patternLineElement = new()
            {
                ElementType = GimElementType.PatternLine,
                ElementDoubles = dashDoubles
            };

            uint patternLineIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(patternLineElement);
            gimData.AddChildToParent(patternLineIndex, lineIndex);
        }
    }

    private static double[] BuildHatchDoubles(Hatch hatch)
    {
        int patternCount = hatch.Pattern.Lines.Count;
        double[] elementDoubles = new double[patternCount * 2];

        for (int i = 0; i < patternCount; i++)
        {
            var patternLine = hatch.Pattern.Lines[i];
            elementDoubles[i * 2 + 0] = patternLine.Offset.X;
            elementDoubles[i * 2 + 1] = patternLine.Offset.Y;
        }

        return elementDoubles;
    }

    private static List<GimLocal> BuildHatchGimLocal(Hatch hatch, uint parentIndex)
    {
        List<GimLocal> locals = new();

        foreach (var patternLine in hatch.Pattern.Lines)
        {
            double totalAngle = hatch.PatternAngle + patternLine.Angle;
            double cosAngle = Math.Cos(totalAngle) * hatch.PatternScale;
            double sinAngle = Math.Sin(totalAngle) * hatch.PatternScale;

            GimLocal local = new()
            {
                Parent = parentIndex,
                Px = patternLine.BasePoint.X,
                Py = patternLine.BasePoint.Y,
                Pz = 0,
                Xx = cosAngle,
                Xy = sinAngle,
                Xz = 0,
                Yx = -sinAngle,
                Yy = cosAngle,
                Yz = 0,
                Zx = 0,
                Zy = 0,
                Zz = 1,
                RightHanded = true
            };

            locals.Add(local);
        }

        return locals;
    }

    private static double[] BuildHatchPatternLineDoubles(Hatch hatch, HatchPattern.Line patternLine)
    {
        if (hatch == null || patternLine == null)
            return Array.Empty<double>();

        double patternScale = hatch.PatternScale;
        var dashLengthList = patternLine.DashLengths ?? new List<double>();

        if (dashLengthList.Count == 0)
            return Array.Empty<double>();

        double[] elementDoubles = new double[dashLengthList.Count];
        for (int i = 0; i < dashLengthList.Count; i++)
            elementDoubles[i] = dashLengthList[i] * patternScale;

        return elementDoubles;
    }
}
