using ACadSharp;
using ACadSharp.Entities;
using CSMath;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GIMtransfer.read.DWGReader;

// Reads Text, Tolerance, and MText DWG entities into GimElementType.Text elements.
// Calls DWG2DTextFormatter for AutoCAD formatting code conversion.
// Calls DWG2DLeaderReader.ReadLeaderLine for MText reactor-linked leaders.
// ponytail: uses raw GimElement construction — add builders for Text/F_symbol when available.

public static class DWG2DTextReader
{
    public static void ReadText(GimData gimData, uint bRepIndex, TextEntity text)
    {
        string universalText = DWG2DTextFormatter.ConvertAutoCADToUniversal(text.Value);
        byte[] bytes = Encoding.UTF8.GetBytes(universalText);

        GimElement gimText = new GimElement()
        {
            ElementType = GimElementType.Text,
            ElementDoubles = new double[] { text.Height, (double)text.VerticalAlignment, (double)text.HorizontalAlignment, 0.0 },
            Locals = BuildTextEntityGimLocal(text, bRepIndex),
            ElementBytes = bytes
        };

        uint textIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimText);
        gimData.AddChildToParent(textIndex, bRepIndex, localIndex: 0);
        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, textIndex, text);
    }

    public static void ReadTolerance(GimData gimData, uint bRepIndex, Tolerance tolerance)
    {
        string universalText = DWG2DTextFormatter.ConvertAutoCADToUniversal(tolerance.Text);
        byte[] bytes = Encoding.UTF8.GetBytes(universalText);

        GimElement symbolTolerance = new GimElement() { ElementType = GimElementType.F_symbol };
        uint symbolIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(symbolTolerance);
        gimData.AddChildToParent(symbolIndex, bRepIndex);

        GimElement gimTolerance = new GimElement()
        {
            ElementType = GimElementType.Text,
            ElementDoubles = new double[] { tolerance.Style.TextHeight, 0.0, 0.0, 0.0 },
            Locals = BuildToleranceGimLocal(tolerance, symbolIndex),
            ElementBytes = bytes
        };

        uint toleranceIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimTolerance);
        gimData.AddChildToParent(toleranceIndex, symbolIndex, localIndex: 0);
        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, toleranceIndex, tolerance);
    }

    public static void ReadMText(GimData gimData, uint bRepIndex, MText mtext)
    {
        string universalText = DWG2DTextFormatter.ConvertAutoCADToUniversal(mtext.Value);
        byte[] bytes = Encoding.UTF8.GetBytes(universalText);
        double attachmentPoint = ConvertMTextAttachmentPoint(mtext.AttachmentPoint);

        GimElement gimMText = new GimElement()
        {
            ElementType = GimElementType.Text,
            ElementDoubles = new double[] { mtext.Height, attachmentPoint, mtext.LineSpacing, mtext.RectangleWidth },
            Locals = BuildMTextGimLocal(mtext, bRepIndex),
            ElementBytes = bytes
        };

        uint mTextIndex = (uint)gimData.Elements.Count;
        gimData.Elements.Add(gimMText);
        gimData.AddChildToParent(mTextIndex, bRepIndex, localIndex: 0);

        if (mtext.Reactors?.OfType<Leader>().Any() == true)
            foreach (var leader in mtext.Reactors.OfType<Leader>().ToList())
                DWG2DLeaderReader.ReadLeaderLine(gimData, mTextIndex, leader);

        DWG2DMaterialReader.AssignDWGColorsToGimMaterial(gimData, mTextIndex, mtext);
    }

    private static double ConvertMTextAttachmentPoint(AttachmentPointType ap) => ap switch
    {
        AttachmentPointType.TopLeft => 1,
        AttachmentPointType.TopCenter => 2,
        AttachmentPointType.TopRight => 3,
        AttachmentPointType.MiddleLeft => 4,
        AttachmentPointType.MiddleCenter => 5,
        AttachmentPointType.MiddleRight => 6,
        AttachmentPointType.BottomLeft => 7,
        AttachmentPointType.BottomCenter => 8,
        AttachmentPointType.BottomRight => 9,
        _ => 0
    };

    private static List<GimLocal> BuildMTextGimLocal(MText mtext, uint parentIndex) => BuildRotationGimLocal(
        mtext.InsertPoint.X, mtext.InsertPoint.Y, mtext.InsertPoint.Z,
        mtext.Rotation, parentIndex);

    private static List<GimLocal> BuildTextEntityGimLocal(TextEntity text, uint parentIndex) => BuildRotationGimLocal(
        text.InsertPoint.X, text.InsertPoint.Y, text.InsertPoint.Z,
        text.Rotation, parentIndex);

    private static List<GimLocal> BuildToleranceGimLocal(Tolerance tolerance, uint parentIndex) => BuildRotationGimLocal(
        tolerance.InsertionPoint.X, tolerance.InsertionPoint.Y, tolerance.InsertionPoint.Z,
        0.0, parentIndex);

    private static List<GimLocal> BuildRotationGimLocal(double px, double py, double pz, double angle, uint parentIndex)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new List<GimLocal>
        {
            new GimLocal
            {
                Parent = parentIndex,
                Px = px, Py = py, Pz = pz,
                Xx = cos, Xy = sin, Xz = 0,
                Yx = -sin, Yy = cos, Yz = 0,
                Zx = 0, Zy = 0, Zz = 1,
                RightHanded = true
            }
        };
    }
}
