using ACadSharp.Entities;
using ACadSharp.Objects;
using ACadSharp.Tables;
using ACadSharp.Header;
using CSMath;
using GIMcompress;
using System;
using System.Collections.Generic;

namespace GIMtransfer.read.DWGReader;

/// <summary>
/// Reads Insert (block references), Point, and Viewport DWG entities.
/// Entity dispatch formerly in the deleted DWGRead2D.cs now lives in <see cref="DWGReadElement"/>.
/// </summary>
/// <remarks>
/// Insert applies X/Y/Z scale + rotation to the parent D_part, then dispatches block children.
/// Point creates P_vertex at XYZ; Viewport creates a Viewport element with UCS orientation.
/// ponytail: raw GimElement construction — add builders for F_symbol/Viewport/P_vertex when available.
/// </remarks>
public static class DWG2DBlockReader
{
    /// <summary>
    /// Applies Insert scale/position/rotation onto the parent part and dispatches 2D/3D block children
    /// via <see cref="DWGReadElement.Dispatch2DEntity"/>.
    /// </summary>
    public static void ReadInsert(GimData gimData, uint bRepIndex, Insert insert)
    {
        int partIndex = (int)bRepIndex - 1;

        Vect position = DWGHelpers.XYZtoVect(insert.InsertPoint);
        gimData.Elements[partIndex].ElementDoubles = new double[]
        {
           insert.XScale,
           insert.YScale,
           insert.ZScale
        };

        gimData.Elements[partIndex].Locals[^1].Px = position.X;
        gimData.Elements[partIndex].Locals[^1].Py = position.Y;
        gimData.Elements[partIndex].Locals[^1].Pz = position.Z;

        Matrix newMatrix = Matrix.FromRPY(0, 0, insert.Rotation);
        newMatrix.ApplyToLocal(gimData.Elements[partIndex].Locals[^1]);

        var childBlock = insert.Block;
        if (childBlock != null)
        {
            List<Entity> entities2D = new List<Entity>();
            List<Entity> entities3D = new List<Entity>();

            foreach (var childEntity in childBlock.Entities)
            {
                if (DWGEntities.IsEntity2D(childEntity))
                    entities2D.Add(childEntity);
                else
                    entities3D.Add(childEntity);
            }

            foreach (var entity in entities2D)
                DWGReadElement.Dispatch2DEntity(gimData, bRepIndex, entity);

            foreach (var entity in entities3D)
            {
                gimData.Elements[(int)bRepIndex].ElementType = GimElementType.R_BRep;
                DWGReadElement.Dispatch2DEntity(gimData, bRepIndex, entity);
            }
        }
    }

    /// <summary>Creates a <see cref="GimElementType.P_vertex"/> at the Point location.</summary>
    public static void ReadPoint(GimData gimData, uint bRepIndex, Point point)
    {
        GimElement gimPoint = new GimElement()
        {
            ElementType = GimElementType.P_vertex
        };

        Vect position = DWGHelpers.XYZtoVect(point.Location);

        gimPoint.ElementDoubles = new double[]
        {
            position.X,
            position.Y,
            position.Z
        };
        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimPoint);
        gimData.AddChildToParent(gimElementIndex, bRepIndex);
    }

    /// <summary>
    /// Creates a Viewport element with focus/size doubles and a local frame from the viewport UCS axes.
    /// </summary>
    public static void ReadViewport(GimData gimData, uint bRepIndex, Viewport viewport)
    {
        GimElement gimViewport = new GimElement()
        {
            ElementType = GimElementType.Viewport
        };

        Vect center = DWGHelpers.XYZtoVect(viewport.Center);
        double width = viewport.Width;
        double height = viewport.Height;

        Vect viewTarget = DWGHelpers.XYZtoVect(viewport.ViewTarget);
        Vect viewCenter = new Vect(viewport.ViewCenter.X, viewport.ViewCenter.Y, 0.0);

        Vect focusPoint = new Vect(
        viewTarget.X + viewCenter.X,
        viewTarget.Y + viewCenter.Y,
        viewTarget.Z
        );

        double viewHeight = viewport.ViewHeight;

        gimViewport.ElementDoubles = new double[]
        {
            focusPoint.X, focusPoint.Y, focusPoint.Z,
            width, height,
            viewHeight,
        };

        gimViewport.Locals = BuildViewportGimLocal(viewport, bRepIndex);

        uint gimElementIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimViewport);
        gimData.AddChildToParent(gimElementIndex, bRepIndex, localIndex: 0);
        gimData.AddChildToParent(1, gimElementIndex);
    }

    private static List<GimLocal> BuildViewportGimLocal(Viewport viewport, uint parentIndex)
    {
        Vect center = DWGHelpers.XYZtoVect(viewport.Center);

        // Z axis = UCS X × UCS Y (right-handed viewport frame).
        double zx = viewport.UcsXAxis.Y * viewport.UcsYAxis.Z - viewport.UcsXAxis.Z * viewport.UcsYAxis.Y;
        double zy = viewport.UcsXAxis.Z * viewport.UcsYAxis.X - viewport.UcsXAxis.X * viewport.UcsYAxis.Z;
        double zz = viewport.UcsXAxis.X * viewport.UcsYAxis.Y - viewport.UcsXAxis.Y * viewport.UcsYAxis.X;

        return new List<GimLocal>
        {
            new GimLocal
            {
                Parent = parentIndex,
                Px = center.X,
                Py = center.Y,
                Pz = center.Z,
                Xx = viewport.UcsXAxis.X,
                Xy = viewport.UcsXAxis.Y,
                Xz = viewport.UcsXAxis.Z,
                Yx = viewport.UcsYAxis.X,
                Yy = viewport.UcsYAxis.Y,
                Yz = viewport.UcsYAxis.Z,
                Zx = zx,
                Zy = zy,
                Zz = zz,
                RightHanded = true
            }
        };
    }
}
