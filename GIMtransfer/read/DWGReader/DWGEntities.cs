using ACadSharp.Entities;
using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using System.Linq;


namespace GIMtransfer.read.DWGReader;

public static class DWGEntities
{
    public class DWGEntityInfo
    {
        public Entity Entity { get; set; }
        public bool Is2D { get; set; }
    }

    public static List<DWGEntityInfo> CollectEntities2D(BlockRecord record)
    {
        var visitedHandles = new HashSet<ulong>();

        var allEntities = CollectEntitiesRecursive2D(record, visitedHandles);

        return allEntities;
    }

    private static List<DWGEntityInfo> CollectEntitiesRecursive2D(BlockRecord record, HashSet<ulong> visitedHandles)
    {
        var entities = new List<DWGEntityInfo>();

        foreach (var entity in record.Entities)
        {
            if (entity == null || !visitedHandles.Add(entity.Handle))
                continue;

            bool is2D = IsEntity2D(entity);

            if (is2D)
            {
                entities.Add(new DWGEntityInfo
                {
                    Entity = entity,
                    Is2D = is2D
                });
            }

            BlockRecord chilBlock = null;

            if (entity is Insert insert)
            {
                chilBlock = insert.Block;
            }

            if (chilBlock != null && chilBlock.Entities.Count > 0)
            {
                var subEntities = CollectEntitiesRecursive2D(chilBlock, visitedHandles);
                entities.AddRange(subEntities);
            }
        }
        return entities;
    }

    public static bool IsEntity2D(Entity entity)
    {
        const double Tolerance = 1e-6;
        bool IsZero(double value) => Math.Abs(value) < Tolerance;

        switch (entity)
        {
            case Insert insert:
                return true;
            case Line line:
                return IsZero(line.StartPoint.Z) && IsZero(line.EndPoint.Z);

            case Arc arc:
                return IsZero(arc.Center.Z);

            case Circle circle:
                return IsZero(circle.Center.Z);

            case Polyline2D pl2d:
                return pl2d.Vertices.All(v => IsZero(v.Location.Z));

            case Spline spline:
                if (spline.ControlPoints.Count > 0)
                    return spline.ControlPoints.All(p => IsZero(p.Z));
                return spline.FitPoints.All(p => IsZero(p.Z));

            case Face3D face3d:
                return IsZero(face3d.FirstCorner.Z) && IsZero(face3d.SecondCorner.Z) &&
                       IsZero(face3d.ThirdCorner.Z) &&
                       IsZero(face3d.FourthCorner.Z);

            case Solid solid:
                return IsZero(solid.FirstCorner.Z) && IsZero(solid.SecondCorner.Z) &&
                       IsZero(solid.ThirdCorner.Z) &&
                       IsZero(solid.FourthCorner.Z);

            case PolyfaceMesh pfm:
                return pfm.Vertices.All(v => IsZero(v.Location.Z));

            case Polyline3D polyline:
                return polyline.Vertices.All(v => IsZero(v.Location.Z));

            case Wipeout wipe:
                return IsZero(wipe.InsertPoint.Z);

            case MText mtext:
                return IsZero(mtext.InsertPoint.Z);

            case TextEntity text:
                return IsZero(text.InsertPoint.Z);

            case Leader leader:
                return leader.Vertices.All(v => IsZero(v.Z));

            case MultiLeader multiLeader:
                return multiLeader.ContextData.LeaderRoots.All(root => 
                IsZero(root.ConnectionPoint.Z) && 
                root.Lines.All(line => line.Points.All(p => IsZero(p.Z))));
                      
            case Ellipse ellipse:
                return IsZero(ellipse.Center.Z) &&
                       IsZero(ellipse.MajorAxisEndPoint.Z);

            case Hatch hatch:
                var normal = hatch.Normal;
                return IsZero(hatch.Elevation) &&
                       IsZero(normal.X) &&
                       IsZero(normal.Y) &&
                       IsZero(normal.Z - 1);

            case Point point:
                return IsZero(point.Location.Z);

            case LwPolyline lwPolyline:
                return true; // Always 2D

            case Ray ray:
                return IsZero(ray.StartPoint.Z) &&
                       IsZero(ray.Direction.Z);

            case XLine xLine:
                return IsZero(xLine.FirstPoint.Z);

            case MLine mLine:
                return mLine.Vertices.All(v => IsZero(v.Position.Z));

            case Dimension dimension:
                return true; // Dimensions are typically 2D

            case Tolerance tolerance:
                return IsZero(tolerance.InsertionPoint.Z); // Tolerances are typically 2D

            case Viewport viewport:
                return true;

            default:
                return false; // Other entities are 3D
        }
    }
    
}

