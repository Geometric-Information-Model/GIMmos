using ACadSharp;
using ACadSharp.Entities;
using GIMcompress;
using System;

namespace GIMtransfer.read.DWGReader;

// Extracts DWG entity colors into GimElementType.Material children.
// Handles ByLayer/ByBlock fallback. Lineweight mapping is disabled until GimMaterial.LineWeight is restored.

public static class DWG2DMaterialReader
{
    /// <summary>
    /// Creates a Material child for <paramref name="elementIndex"/> from the entity's color (or layer color when ByLayer/ByBlock).
    /// </summary>
    /// <remarks>
    /// Curve readers also call this so stroke color is preserved on import. Output-flow support for
    /// materials on curves, and any GimMaterial refactor, are follow-up work.
    /// </remarks>
    public static void AssignDWGColorsToGimMaterial(GimData gimData, uint elementIndex, Entity entity)
    {
        Color color = entity.Color;
        Color layerColor = entity.Layer.Color;

        GimMaterial gimMaterialElement = new()
        {
            elementType = GimElementType.Material
        };

        int gimMaterialIndex = gimData.Elements.Count;
        gimData.Elements.Add(gimMaterialElement);
        gimData.AddChildToParent((uint)gimMaterialIndex, elementIndex);

        float red, green, blue;

        // Prefer explicit entity color; ByLayer/ByBlock fall back to the layer palette.
        if (!color.IsByBlock && !color.IsByLayer)
        {
            red = color.R / 255f;
            green = color.G / 255f;
            blue = color.B / 255f;
        }
        else
        {
            red = layerColor.R / 255f;
            green = layerColor.G / 255f;
            blue = layerColor.B / 255f;
        }

        GimColour diffuse = new(1, red, green, blue);
        GimColour baseColor = new(1, red, green, blue);

        gimMaterialElement.DiffuseColour = diffuse.ToInt();
        gimMaterialElement.BaseColour = baseColor.ToInt();

        // LineWeight is disabled pending review of how it should be represented on GimMaterial.
    }
}
