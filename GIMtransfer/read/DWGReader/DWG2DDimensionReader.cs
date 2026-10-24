using ACadSharp;
using ACadSharp.Entities;
using CSMath;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GIMtransfer.read.DWGReader;

// Reads Dimension DWG entities: creates Dimension element with block sub-entities
// (symbols, solids, points) dispatched via DWGReadElement.Dispatch2DEntity.
// ponytail: Dimension/F_symbol/P_vertex use raw construction — add builders when available.

public static class DWG2DDimensionReader
{
    public static void ReadDimension(GimData gimData, uint bRepIndex, Dimension dimension)
    {
        if (dimension?.Block?.Entities.Count > 0)
        {
            GimElement dimensionElement = new GimElement() { ElementType = GimElementType.Dimension, ElementInts = new int[] { (int)dimension.ObjectType } };
            uint dimensionIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(dimensionElement);
            gimData.AddChildToParent(dimensionIndex, bRepIndex);

            uint symbolParentIndex = 0;
            var points = new List<double>();
            foreach (var entity in dimension.Block.Entities)
            {
                if (entity is Insert insert)
                {
                    GimElement symbolElement = new GimElement() { ElementType = GimElementType.F_symbol, ElementDoubles = new double[] { insert.XScale, insert.YScale, insert.ZScale } };
                    Vect position = DWGHelpers.XYZtoVect(insert.InsertPoint);
                    uint symbolIndex = (uint)gimData.Elements.Count;
                    gimData.Elements.Add(symbolElement);
                    gimData.AddChildToParent(symbolIndex, symbolParentIndex);
                    gimData.Elements[(int)symbolIndex].Locals[0].Px = position.X;
                    gimData.Elements[(int)symbolIndex].Locals[0].Py = position.Y;
                    gimData.Elements[(int)symbolIndex].Locals[0].Pz = position.Z;
                    Matrix.FromRPY(0, 0, insert.Rotation).ApplyToLocal(gimData.Elements[(int)symbolIndex].Locals[0]);
                    if (insert.Block.Entities.Count > 0)
                        foreach (var be in insert.Block.Entities)
                            DWGReadElement.Dispatch2DEntity(gimData, symbolIndex, be);
                }
                else if (entity is Solid solid)
                {
                    GimElement symbolElement = new GimElement() { ElementType = GimElementType.F_symbol };
                    uint symbolIndex = (uint)gimData.Elements.Count;
                    gimData.Elements.Add(symbolElement);
                    gimData.AddChildToParent(symbolIndex, symbolParentIndex);
                    DWG2DSurfaceReader.ReadSolid(gimData, symbolIndex, solid, true, true);
                }
                else if (entity is Point point)
                {
                    Vect loc = DWGHelpers.XYZtoVect(point.Location);
                    points.AddRange(new[] { loc.X, loc.Y, loc.Z });
                }
                else
                {
                    DWGReadElement.Dispatch2DEntity(gimData, dimensionIndex, entity);
                    if (entity is Line || entity is Arc)
                        symbolParentIndex = (uint)gimData.Elements.Count - 2;
                }
            }
            if (points.Count > 0)
            {
                GimElement pointsElement = new GimElement() { ElementType = GimElementType.P_vertex, ElementDoubles = points.ToArray() };
                gimData.Elements.Add(pointsElement);
                gimData.AddChildToParent((uint)gimData.Elements.Count - 1, dimensionIndex);
            }
        }
    }
}
