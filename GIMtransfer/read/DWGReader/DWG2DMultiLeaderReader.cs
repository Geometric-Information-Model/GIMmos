using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Objects;
using CSMath;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GIMtransfer.read.DWGReader;

// Reads MultiLeader DWG entities: text content + leader lines (straight/spline) + arrows.
// Delegates to DWG2DCurveReader for curve segments, DWG2DSurfaceReader for arrow/box solids.
// Text content uses DWG2DTextFormatter for format code conversion.
// ponytail: F_symbol/Text elements use raw construction — add builders when available.

public static class DWG2DMultiLeaderReader
{
    public static void ReadMultiLeader(GimData gimData, uint bRepIndex, MultiLeader multiLeader)
    {
        if (multiLeader == null) return;
        if (HasValidContent(multiLeader))
            ReadMultiLeaderContentType(gimData, bRepIndex, multiLeader);
        if (multiLeader.ContextData.LeaderRoots.Count > 0)
            ReadMultiLeaderLine(gimData, bRepIndex, multiLeader);
    }

    private static bool HasValidContent(MultiLeader multiLeader)
    {
        var content = multiLeader.ContextData;
        if (content == null) return false;
        return (multiLeader.ContentType == LeaderContentType.Block && content.BlockContent?.Entities?.Count > 0)
            || (multiLeader.ContentType == LeaderContentType.MText && !string.IsNullOrEmpty(content.TextLabel))
            || (multiLeader.ContentType == LeaderContentType.Tolerance && !string.IsNullOrEmpty(content.TextLabel));
    }

    private static void ReadMultiLeaderContentType(GimData gimData, uint parentIndex, MultiLeader multiLeader)
    {
        var context = multiLeader.ContextData;

        if (multiLeader.ContentType == LeaderContentType.MText || multiLeader.ContentType == LeaderContentType.Tolerance)
        {
            string universalText = DWG2DTextFormatter.ConvertAutoCADToUniversal(context.TextLabel);
            GimElement gimText = new GimElement()
            {
                ElementType = GimElementType.Text,
                ElementDoubles = new double[] { context.TextHeight, (int)context.TextAttachmentPoint, 1.0, context.BoundaryWidth },
                Locals = BuildMultiLeaderTextGimLocal(multiLeader, parentIndex),
                ElementBytes = Encoding.UTF8.GetBytes(universalText)
            };
            uint textIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(gimText);
            gimData.AddChildToParent(textIndex, parentIndex, localIndex: 0);
            DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, textIndex, multiLeader);
        }
        else if (multiLeader.ContentType == LeaderContentType.Block && context.BlockContent.Entities.Count > 0)
        {
            foreach (var entity in context.BlockContent.Entities)
                DWGReadElement.Dispatch2DEntity(gimData, parentIndex, entity);
        }
    }

    private static List<GimLocal> BuildMultiLeaderTextGimLocal(MultiLeader multiLeader, uint parentIndex)
    {
        var contextData = multiLeader.ContextData;
        double cos = Math.Cos(contextData.TextRotation);
        double sin = Math.Sin(contextData.TextRotation);
        return new List<GimLocal>
        {
            new GimLocal
            {
                Parent = parentIndex,
                Px = contextData.TextLocation.X, Py = contextData.TextLocation.Y, Pz = contextData.TextLocation.Z,
                Xx = cos, Xy = sin, Xz = 0,
                Yx = -sin, Yy = cos, Yz = 0,
                Zx = 0, Zy = 0, Zz = 1,
                RightHanded = true
            }
        };
    }

    private static void ReadMultiLeaderLine(GimData gimData, uint parentIndex, MultiLeader multiLeader)
    {
        GimElement wireElement = new GimElement { ElementType = GimElementType.wire };
        uint wireIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(wireElement);
        gimData.AddChildToParent(wireIndex, parentIndex);

        var contextData = multiLeader.ContextData;
        if (multiLeader.PathType == MultiLeaderPathType.Invisible || contextData?.LeaderRoots == null || contextData.LeaderRoots.Count == 0) return;

        var startPoint = new Vect(); var endPoint = new Vect();
        foreach (var root in contextData.LeaderRoots)
            foreach (var line in root.Lines)
            {
                if (line.Points == null || line.Points.Count == 0) continue;
                startPoint = DWGHelpers.XYZtoVect(line.Points.First());
                endPoint = DWGHelpers.XYZtoVect(root.ConnectionPoint);
                if (multiLeader.PathType == MultiLeaderPathType.StraightLineSegments)
                    HandleLineSegments(gimData, wireIndex, root, line, multiLeader);
                else if (multiLeader.PathType == MultiLeaderPathType.Spline)
                    HandleSplineLines(gimData, wireIndex, root, line, multiLeader);
            }

        if (multiLeader.TextFrame) AddTextFrameAsSolid(gimData, (uint)gimData.Elements.Count - 1, multiLeader);
        DrawMultiLeaderArrow(gimData, (uint)gimData.Elements.Count - 1, startPoint, endPoint, multiLeader.ArrowheadSize);
        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, wireIndex, multiLeader);
    }

    private static void HandleLineSegments(GimData gimData, uint bRepIndex, MultiLeaderObjectContextData.LeaderRoot root, MultiLeaderObjectContextData.LeaderLine line, MultiLeader multiLeader)
    {
        var startEndPairs = line.StartEndPoints?.ToList();
        if (startEndPairs?.Count > 0)
            foreach (var pair in startEndPairs)
                DWG2DCurveReader.ReadLineElement(gimData, bRepIndex, new List<Vect> { DWGHelpers.XYZtoVect(pair.StartPoint), DWGHelpers.XYZtoVect(pair.EndPoint) }, multiLeader, false);
        else
        {
            var verts = new List<Vect> { DWGHelpers.XYZtoVect(line.Points[0]) };
            if (line.Points.Count > 1) verts.AddRange(line.Points.Skip(1).Select(DWGHelpers.XYZtoVect));
            verts.Add(DWGHelpers.XYZtoVect(root.ConnectionPoint));
            if (root.LandingDistance > 0) { var ev = GetLastMultiLeaderEndPoint(root, multiLeader); if (ev.HasValue) verts.Add(DWGHelpers.XYZtoVect(ev.Value)); }
            for (int i = 0; i < verts.Count - 1; i++)
                DWG2DCurveReader.ReadLineElement(gimData, bRepIndex, new List<Vect> { verts[i], verts[i + 1] }, multiLeader, false);
        }
    }

    private static void HandleSplineLines(GimData gimData, uint bRepIndex, MultiLeaderObjectContextData.LeaderRoot root, MultiLeaderObjectContextData.LeaderLine line, MultiLeader multiLeader)
    {
        var fitPoints = new List<XYZ>(line.Points);
        if (!fitPoints.Last().Equals(root.ConnectionPoint)) fitPoints.Add(root.ConnectionPoint);
        if (root.LandingDistance > 0) { var ev = GetLastMultiLeaderEndPoint(root, multiLeader); if (ev.HasValue && !fitPoints.Last().Equals(ev.Value)) fitPoints.Add(ev.Value); }
        if (fitPoints.Count < 2) return;
        var spline = new Spline();
        spline.FitPoints.AddRange(fitPoints);
        spline.UpdateFromFitPoints();
        DWG2DCurveReader.ReadSpline(gimData, bRepIndex, spline, false);
    }

    private static void DrawMultiLeaderArrow(GimData gimData, uint bRepIndex, Vect start, Vect end, double arrowSize)
    {
        GimElement symbolElement = new GimElement() { ElementType = GimElementType.F_symbol };
        uint symbolIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(symbolElement);
        gimData.AddChildToParent(symbolIndex, bRepIndex);

        var dirVec = end - start;
        double dirLength = dirVec.Norm;
        if (dirLength < 1e-8) return;
        var dir = dirVec / dirLength;
        double halfWidth = Math.Tan(Math.PI / 9) * arrowSize;
        var baseCenter = new Vect(start.X + dir.X * arrowSize, start.Y + dir.Y * arrowSize, start.Z + dir.Z * arrowSize);
        var side = new Vect(-dir.Y, dir.X, 0);
        double sideLen = side.Norm;
        side = sideLen < 1e-6 ? new Vect(0, 1, 0) : side / sideLen;
        var left = new Vect(baseCenter.X + side.X * halfWidth, baseCenter.Y + side.Y * halfWidth, baseCenter.Z + side.Z * halfWidth);
        var right = new Vect(baseCenter.X - side.X * halfWidth, baseCenter.Y - side.Y * halfWidth, baseCenter.Z - side.Z * halfWidth);

        var solid = new Solid()
        {
            FirstCorner = new XYZ(start.X, start.Y, start.Z),
            SecondCorner = new XYZ(left.X, left.Y, left.Z),
            ThirdCorner = new XYZ(right.X, right.Y, right.Z),
            FourthCorner = new XYZ(start.X, start.Y, start.Z)
        };
        DWG2DSurfaceReader.ReadSolid(gimData, symbolIndex, solid, true, true);
    }

    private static XYZ? GetLastMultiLeaderEndPoint(MultiLeaderObjectContextData.LeaderRoot root, MultiLeader multiLeader)
    {
        if (root == null || multiLeader == null) return null;
        var endPoint = multiLeader.ContextData.ContentBasePoint;
        double dx = endPoint.X - root.ConnectionPoint.X;
        double dy = endPoint.Y - root.ConnectionPoint.Y;
        return dx * dx + dy * dy < 1e-6 ? null : endPoint;
    }

    private static void AddTextFrameAsSolid(GimData gimData, uint bRepIndex, MultiLeader multiLeader)
    {
        GimElement symbolElement = new GimElement() { ElementType = GimElementType.F_symbol };
        uint symbolIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(symbolElement);
        gimData.AddChildToParent(symbolIndex, bRepIndex);

        var context = multiLeader.ContextData;
        double textWidth = Math.Abs(context.TextLocation.X - context.ContentBasePoint.X);
        if (textWidth <= 0) textWidth = context.TextHeight * 6;
        int lineCount = context.TextLabel.Split(new string[] { "\r\n", "\r", "\n", "\\P" }, StringSplitOptions.None).Length;
        double contentHeight = (context.TextHeight * lineCount) + ((lineCount - 1) * (context.TextHeight / 2));
        double bottomY = context.ContentBasePoint.Y - (context.LandingGap + contentHeight);
        double topY = context.ContentBasePoint.Y + context.LandingGap + (context.TextHeight / 2);
        double leftX, rightX;
        if (context.TextAttachmentPoint == TextAttachmentPointType.Right)
        { leftX = context.ContentBasePoint.X; rightX = context.ContentBasePoint.X + textWidth + context.LandingGap; }
        else
        { rightX = context.ContentBasePoint.X; leftX = context.ContentBasePoint.X - textWidth - context.LandingGap; }

        var corners = new List<XYZ> { new XYZ(leftX, bottomY, context.ContentBasePoint.Z), new XYZ(leftX, topY, context.ContentBasePoint.Z), new XYZ(rightX, topY, context.ContentBasePoint.Z), new XYZ(rightX, bottomY, context.ContentBasePoint.Z) };
        if (context.TextRotation > 1e-6) corners = RotateCornersAroundBase(corners, context.ContentBasePoint, context.BaseDirection, context.BaseVertical, context.TextRotation);
        DWG2DSurfaceReader.ReadSolid(gimData, symbolIndex, new Solid() { FirstCorner = corners[0], SecondCorner = corners[1], ThirdCorner = corners[2], FourthCorner = corners[3] }, true, false);
    }

    private static List<XYZ> RotateCornersAroundBase(List<XYZ> corners, XYZ basePoint, XYZ baseDirection, XYZ baseVertical, double rotation)
    {
        XYZ dir = new XYZ(baseDirection.X, baseDirection.Y, 0).Normalize();
        XYZ vert = new XYZ(baseVertical.X, baseVertical.Y, 0).Normalize();
        double cosA = Math.Cos(rotation); double sinA = Math.Sin(rotation);
        return corners.Select(p =>
        {
            XYZ offset = p - basePoint;
            double rd = offset.Dot(dir) * cosA - offset.Dot(vert) * sinA;
            double rv = offset.Dot(dir) * sinA + offset.Dot(vert) * cosA;
            return new XYZ((basePoint + dir * rd + vert * rv).X, (basePoint + dir * rd + vert * rv).Y, p.Z);
        }).ToList();
    }
}
