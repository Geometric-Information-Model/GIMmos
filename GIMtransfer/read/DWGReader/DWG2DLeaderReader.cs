using ACadSharp;
using ACadSharp.Entities;
using CSMath;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GIMtransfer.read.DWGReader;

// Reads Leader DWG entities: creates wire with line/spline segments + arrow head.
// Delegates to DWG2DSurfaceReader.ReadSolid for arrow triangle geometry.
// Sub-entity dispatch in arrow blocks uses DWGReadElement.Dispatch2DEntity.
// ponytail: F_symbol elements use raw construction — add builder when available.

public static class DWG2DLeaderReader
{
    public static void ReadLeader(GimData gimData, uint bRepIndex, Leader leader)
    {
        if (leader.Vertices == null || leader.Vertices.Count == 0) return;

        var annotation = leader.AssociatedAnnotation;

        if (leader.CreationType == LeaderCreationType.CreatedWithTextAnnotation)
        {
            if (annotation is MText mText)
                DWG2DTextReader.ReadMText(gimData, bRepIndex, mText);
        }
        else if (leader.CreationType == LeaderCreationType.CreatedWithBlockReferenceAnnotation)
        {
            if (annotation is Insert insert && insert.Block.Entities.Count > 0)
                foreach (var entity in insert.Block.Entities)
                    DWGReadElement.Dispatch2DEntity(gimData, bRepIndex, entity);
        }
        else if (leader.CreationType == LeaderCreationType.CreatedWithToleranceAnnotation)
        {
            if (annotation is Tolerance tolerance)
                DWG2DTextReader.ReadTolerance(gimData, bRepIndex, tolerance);
        }
        else
            ReadLeaderLine(gimData, bRepIndex, leader);
    }

    public static void ReadLeaderLine(GimData gimData, uint parentIndex, Leader leader)
    {
        GimElement wireElement = new GimElement { ElementType = GimElementType.wire };
        uint wireIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(wireElement);
        gimData.AddChildToParent(wireIndex, parentIndex);

        uint lastIndex = wireIndex;
        var verts = leader.Vertices.Select(DWGHelpers.XYZtoVect).ToList();
        var lastVertex = GetLastSegmentEndPoint(leader);
        if (lastVertex.HasValue) verts.Add(DWGHelpers.XYZtoVect(lastVertex.Value));

        if (leader.PathType == LeaderPathType.StraightLineSegments)
        {
            for (int i = 0; i < verts.Count - 1; i++)
                DWG2DCurveReader.ReadLineElement(gimData, wireIndex, new List<Vect> { verts[i], verts[i + 1] }, leader, false);
            lastIndex = (uint)gimData.Elements.Count - 1;
        }
        else if (leader.PathType == LeaderPathType.Spline)
        {
            var spline = new Spline();
            foreach (var vertex in leader.Vertices) spline.FitPoints.Add(vertex);
            if (spline.FitPoints.Count >= 2)
            {
                spline.UpdateFromFitPoints();
                DWG2DCurveReader.ReadSpline(gimData, wireIndex, spline, false);
                lastIndex = (uint)gimData.Elements.Count - 1;
            }
        }

        if (leader.ArrowHeadEnabled) ReadLeaderArrow(gimData, lastIndex, leader);
        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, wireIndex, leader);
    }

    private static XYZ? GetLastSegmentEndPoint(Leader leader)
    {
        if (leader.Vertices.Count < 2 || leader.AssociatedAnnotation is not MText mText) return null;
        var startVertex = leader.Vertices.Last();
        double direction = mText.DrawingDirection == DrawingDirectionType.LeftToRight ? 1.0 : -1.0;
        double dx = Math.Cos(mText.Rotation) * leader.TextWidth * direction;
        double dy = Math.Sin(mText.Rotation) * leader.TextWidth * direction;
        return new XYZ(startVertex.X + dx, startVertex.Y + dy, startVertex.Z);
    }

    private static void ReadLeaderArrow(GimData gimData, uint bRepIndex, Leader leader)
    {
        if (!leader.ArrowHeadEnabled) return;

        GimElement symbolElement = new GimElement() { ElementType = GimElementType.F_symbol };
        uint symbolIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(symbolElement);
        gimData.AddChildToParent(symbolIndex, bRepIndex);

        var arrowBlock = leader.Style.ArrowBlock;
        if (arrowBlock?.Entities.Count > 0)
            foreach (var entity in arrowBlock.Entities)
            {
                if (entity is Solid solid)
                    DWG2DSurfaceReader.ReadSolid(gimData, symbolIndex, solid, addMaterial: true, isFilled: true);
                else if (entity is Insert insert)
                    ReadInsertWithChildren(gimData, symbolIndex, insert);
                else
                    DWGReadElement.Dispatch2DEntity(gimData, symbolIndex, entity);
            }
        else
            DrawLeaderArrow(gimData, symbolIndex, leader);
    }

    private static void DrawLeaderArrow(GimData gimData, uint bRepIndex, Leader leader)
    {
        if (leader.Vertices == null || leader.Vertices.Count < 2) return;
        var start = DWGHelpers.XYZtoVect(leader.Vertices[0]);
        var next = DWGHelpers.XYZtoVect(leader.Vertices[1]);
        var dirVec = next - start;
        double dirLength = dirVec.Norm;
        if (dirLength < 1e-8) return;
        var dir = dirVec / dirLength;
        double arrowSize = leader.Style.ArrowSize;
        double halfWidth = Math.Tan(Math.PI / 9) * arrowSize;
        var baseCenter = new Vect(start.X + dir.X * arrowSize, start.Y + dir.Y * arrowSize, start.Z + dir.Z * arrowSize);
        var side = new Vect(-dir.Y, dir.X, 0);
        double sideLen = side.Norm;
        side = sideLen > 1e-8 ? side / sideLen : new Vect(0, 1, 0);
        var left = new Vect(baseCenter.X + side.X * halfWidth, baseCenter.Y + side.Y * halfWidth, baseCenter.Z + side.Z * halfWidth);
        var right = new Vect(baseCenter.X - side.X * halfWidth, baseCenter.Y - side.Y * halfWidth, baseCenter.Z - side.Z * halfWidth);

        var solid = new Solid()
        {
            FirstCorner = new XYZ(start.X, start.Y, start.Z),
            SecondCorner = new XYZ(left.X, left.Y, left.Z),
            ThirdCorner = new XYZ(right.X, right.Y, right.Z),
            FourthCorner = new XYZ(start.X, start.Y, start.Z)
        };
        DWG2DSurfaceReader.ReadSolid(gimData, bRepIndex, solid, true, true);
    }

    private static void ReadInsertWithChildren(GimData gimData, uint parentIndex, Insert insert)
    {
        GimElement symbolElement = new GimElement()
        {
            ElementType = GimElementType.F_symbol,
            ElementDoubles = new double[] { insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z, insert.Rotation }
        };
        uint symbolIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(symbolElement);
        gimData.AddChildToParent(symbolIndex, parentIndex);

        if (insert.Block.Entities.Count > 0)
            foreach (var blockEntity in insert.Block.Entities)
            {
                if (blockEntity is Solid solid)
                    DWG2DSurfaceReader.ReadSolid(gimData, symbolIndex, solid, true, true);
                else
                    DWGReadElement.Dispatch2DEntity(gimData, symbolIndex, blockEntity);
            }
    }
}
