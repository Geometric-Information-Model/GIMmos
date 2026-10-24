using ACadSharp.Entities;
using ACadSharp.Objects;
using CSMath;
using GeometryGym.STEP;
using GIMcompress;
using Svg;
using Svg.Transforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using static ACadSharp.Objects.Evaluations.BlockVisibilityParameter;
using static ACadSharp.Objects.MLineStyle;
using static System.Net.WebRequestMethods;

namespace GIMtransfer.write;

//TODO make something that can read the direction of the local of the 2dRep to dicide wich 2dRep to use.

public class SvgWriter
{
    public SvgWriter()
    {
    }
    //Todo: Make dynamic size 
    private SvgDocument svgDoc = new SvgDocument
    {
        Width = 2500,
        Height = 1000
    };

    private SvgGroup mainGroup = new SvgGroup
    {
        Transforms = new SvgTransformCollection
            {
                new SvgScale(1, -1),
                new SvgTranslate(500, -300)
            }
    };
    //tempoary color
    private Color color = Color.White;

    // Thread-safe counter for unique pattern IDs
    private int _patternCounter = 0;

    private HashSet<int> _processedElements;

    // -------------------------------------------------------------
    // Main method to generate the SVG document
    // Skips materials, parts with BRep, and elements whose parent
    // is Wire, Plane, or Hatch.
    // -------------------------------------------------------------
    public string WriteGimDataToSvg(GimData gimData)
    {
        _processedElements = new HashSet<int>(gimData.Elements.Count);

        _processedElements.Clear();

        var defs = new SvgDefinitionList();
        svgDoc.Children.Add(defs);

        for (int elementIndex = 0; elementIndex < gimData.Elements.Count; elementIndex++)
        {
            var element = gimData.Elements[elementIndex];
            if (_processedElements.Contains(elementIndex))
                continue;
            // Skip material elements
            if (element.ElementType == GimElementType.Material)
                continue;

            // Skip elements whose parent is Wire, Plane, or Hatch
            if (ShouldSkipDueToParent(element, gimData))
                continue;

            // Build SVG group for this element and its subtree
            var group = WriteElementToSvgGroup(element, gimData, elementIndex);
            if (group != null)
                mainGroup.Children.Add(group);
        }

        svgDoc.Children.Add(mainGroup);
        return svgDoc.GetXML();
    }

    // -------------------------------------------------------------
    // Recursively creates an SVG group for a single element and its subtree
    // Returns null if the element should be skipped.
    // -------------------------------------------------------------
    private SvgGroup WriteElementToSvgGroup(GimElement element, GimData gimData, int elementIndex)
    {
        _processedElements.Add(elementIndex);

        if (ShouldSkipDueToParent(element, gimData))
            return null;

        var group = new SvgGroup();
        double dimScale = GetDimScale(gimData);

        // Draw geometry for this element (skip materials)
        if (element.ElementType != GimElementType.Material)
        {
            WriteElementGeometry(element, gimData, group, dimScale);
            group.AddStyle(element.ElementType.ToString(), "", 0);
        }

        // Recursively process children
        foreach (uint childIndex in element.Elements)
        {
            if (_processedElements.Contains((int)childIndex))
                continue;
            var child = gimData.Elements[(int)childIndex];

            if (child.ElementType == GimElementType.Material)
                continue;

            if (ShouldSkipDueToParent(child, gimData))
                continue;

            var childGroup = WriteElementToSvgGroup(child, gimData, (int)childIndex);
            if (childGroup != null)
            {
                group.Children.Add(childGroup);
                //group.AddStyle(element.ElementType.ToString(), "", 0);
            }
        }

        return group;
    }

    // -------------------------------------------------------------
    // Determines if an element should be skipped because it belongs
    // to Wire, Plane, or Hatch (checks parent and grandparent).
    // -------------------------------------------------------------
    private bool ShouldSkipDueToParent(GimElement element, GimData gimData)
    {
        if (element.Locals == null || element.Locals.Count == 0)
            return false;

        int parentIndex = (int)element.Locals[0].Parent;
        var parentElement = gimData.Elements[parentIndex];

        // Skip if parent is Wire, Plane, or Hatch
        if (parentElement.ElementType == GimElementType.wire
            || parentElement.ElementType == GimElementType.F_plane
            || parentElement.ElementType == GimElementType.Hatch
            || parentElement.ElementType == GimElementType.R_BRep)
        {
            return true;
        }

        if (parentElement.Locals != null && parentElement.Locals.Count > 0)
        {
            int grandParentIndex = (int)parentElement.Locals[0].Parent;
            var grandParentElement = gimData.Elements[grandParentIndex];

            if (grandParentElement.ElementType == GimElementType.Hatch)
            {
                return true;
            }
        }
        return false;
    }

    private double GetDimScale(GimData gimdata)
    {
        var bRepElement = gimdata.Elements.FirstOrDefault(e => e.ElementType == GimElementType.R_2DRep);

        if (bRepElement != null &&
            bRepElement.ElementDoubles != null &&
            bRepElement.ElementDoubles.Length > 0)
        {
            double dimScale = bRepElement.ElementDoubles[0];
            return dimScale != 0 ? dimScale : 1.0;
        }
        return 1.0;
    }

    // -------------------------------------------------------------
    // Geometry router
    // Decides which drawing method to use based on element type
    // -------------------------------------------------------------
    private void WriteElementGeometry(GimElement element, GimData gimData, SvgGroup group, double dimScale)
    {
        switch (element.ElementType)
        {
            case GimElementType.C_line2D:
                WriteLine(element, gimData, group);
                break;

            case GimElementType.C_circle2D:
                WriteCircle(element, gimData, group);
                break;

            case GimElementType.P_vertex:
                WritePoint(element, gimData,  group);
                break;

            case GimElementType.C_elipse2D:
                WriteEllipse(element, gimData, group);
                break;

            case GimElementType.F_plane:
                WritePlane(element, gimData, group);
                break;

            case GimElementType.F_symbol:
            case GimElementType.D_part:
                WriteSymbolOrPart(element, gimData, group);
                break;

            case GimElementType.wire:
                WriteWire(element, gimData, group);
                break;

            case GimElementType.C_bspline2D:
                WriteBSpline(element, gimData, group);
                break;

            case GimElementType.C_bezier2D:
                WriteBezier2D(element, gimData, group);
                break;

            case GimElementType.Text:
                if (element.elementDoubles != null)
                    WriteText(element, gimData, group, dimScale);
                break;

            case GimElementType.Dimension:
                // Optional: implement dimension drawing if needed
                break;
        }
    }

    // =============================================================
    // WriteWire: Generates an SVG path for a wire element.
    // Supports open and closed wires, recursive child wires,
    // and multiple geometry types (line, circle, ellipse, B-spline).
    // =============================================================
    private void WriteWire(GimElement element, GimData gimData, SvgGroup group)
    {
        var path = new SvgPath
        {
            Stroke = new SvgColourServer(Color.Black),
            Fill = SvgPaintServer.None // Fill can be set if wire is closed
        };

        var children = element.GetAllChildrenIndexesOrdered(
            gimData.Elements.IndexOf(element), gimData);

        if (children.Count == 0)
            return;

        // Apply material if any
        
        AssignMaterialsToSvgGroup(gimData, element, path);

        string fullPath = "";
        bool isFirstSegment = true;

        // Iterate through all child elements
        for (int i = 0; i < children.Count; i++)
        {
            var childEl = gimData.Elements[children[i].Item1];

            if (childEl.ElementType == GimElementType.wire)
            {
                // Recursive call for nested wire
                WriteWire(childEl, gimData, group);
                continue;
            }

            // Build the segment path string based on element type
            string segment = childEl.ElementType switch
            {
                GimElementType.C_line2D => BuildLinePath(childEl, isFirstSegment),
                GimElementType.C_circle2D => BuildCirclePath(childEl, isFirstSegment),
                GimElementType.C_bspline2D => BuildBSplinePath(childEl, isFirstSegment),
                GimElementType.C_elipse2D => BuildEllipsePath(childEl, isFirstSegment),
                _ => ""
            };

            if (!string.IsNullOrWhiteSpace(segment))
            {
                fullPath += segment + " ";
                isFirstSegment = false; // After first segment
            }
        }

        // If wire is closed
        if (element.ElementBools != null && element.ElementBools[0] == 1 && fullPath != string.Empty)
        {
            fullPath += "Z";

            // Fill the closed wire with the same color as the stroke
            if (element.ElementBools[1] == 1 && path.Stroke is SvgColourServer strokeColorServer)
            {
                var color = strokeColorServer.Colour;
                path.Fill = new SvgColourServer(color);
            }
        }
        else
        {
            // Open wire, no fill
            path.Fill = SvgPaintServer.None;
        }

        if (!string.IsNullOrWhiteSpace(fullPath))
        {
            path.PathData = SvgPathBuilder.Parse(fullPath);
            group.Children.Add(path); 
        }
    }

    // -------------------------------------------------------------
    // BuildLinePath: Returns SVG path string for a line segment.
    // Adds 'M' for the first segment, 'L' for continuation.
    // -------------------------------------------------------------
    private string BuildLinePath(GimElement element, bool isFirstSegment)
    {
        GetLinePoints(element, out var x1, out var y1, out var x2, out var y2);
        return isFirstSegment ? $"M{x1} {y1} L{x2} {y2}" : $"L{x2} {y2}";
    }

    private void GetLinePoints(GimElement element, out string x1, out string y1, out string x2, out string y2)
    {
        x1 = element.ElementDoubles[0].ToString(CultureInfo.InvariantCulture);
        y1 = element.ElementDoubles[1].ToString(CultureInfo.InvariantCulture);
        x2 = element.ElementDoubles[3].ToString(CultureInfo.InvariantCulture);
        y2 = element.ElementDoubles[4].ToString(CultureInfo.InvariantCulture);
    }

    // -------------------------------------------------------------
    // BuildCirclePath: Returns SVG path string for a circle or arc.
    // Starts with 'M' if first segment, full circle uses two arcs.
    // -------------------------------------------------------------
    private string BuildCirclePath(GimElement element, bool isFirstSegment)
    {
        GetCircleGeometry(
            element,
            out double startX, out double startY,
            out double radius,
            out double startAngle, out double endAngle,
            out double centerX, out double centerY,
            out double endX, out double endY,
            out int largeArcFlag, out int sweepFlag,
            out bool isFullCircle
        );

        string path;
        if (isFullCircle)
        {
            path = string.Format(CultureInfo.InvariantCulture,
                "M {0:F4} {1:F4} A {2:F4} {2:F4} 0 1 0 {3:F4} {1:F4} " +
                "A {2:F4} {2:F4} 0 1 0 {0:F4} {1:F4}",
                centerX + radius, centerY,
                radius,
                centerX - radius
            );
        }
        else
        {
            path = string.Format(CultureInfo.InvariantCulture,
                "M {0:F4} {1:F4} A {2:F4} {2:F4} 0 {3} {4} {5:F4} {6:F4}",
                startX, startY,
                radius,
                largeArcFlag, sweepFlag,
                endX, endY
            );
        }

        return isFirstSegment ? path : path.TrimStart('M');
    }

    private void GetCircleGeometry(
        GimElement element,
        out double startX, out double startY,
        out double radius,
        out double startAngle, out double endAngle,
        out double centerX, out double centerY,
        out double endX, out double endY,
        out int largeArcFlag, out int sweepFlag,
        out bool isFullCircle)
    {
        startX = element.ElementDoubles[0];
        startY = element.ElementDoubles[1];
        radius = element.ElementDoubles[6];
        startAngle = element.ElementDoubles[7];
        endAngle = element.ElementDoubles[8];

        centerX = startX - radius * Math.Cos(startAngle);
        centerY = startY - radius * Math.Sin(startAngle);

        endX = centerX + radius * Math.Cos(endAngle);
        endY = centerY + radius * Math.Sin(endAngle);

        largeArcFlag = Math.Abs(endAngle - startAngle) > Math.PI ? 1 : 0;
        sweepFlag = endAngle > startAngle ? 1 : 0;

        isFullCircle = Math.Abs(endAngle - startAngle) >= 2 * Math.PI;
    }

    // -------------------------------------------------------------
    // BuildEllipsePath: Returns SVG path string for an ellipse or arc.
    // Handles closed ellipses and arcs, adds 'M' for first segment.
    // -------------------------------------------------------------
    private string BuildEllipsePath(GimElement element, bool isFirstSegment)
    {
        double startX = element.ElementDoubles[0];
        double startY = element.ElementDoubles[1];
        double endX = element.ElementDoubles[3];
        double endY = element.ElementDoubles[4];
        double majorR = element.ElementDoubles[6];
        double minorR = element.ElementDoubles[7];
        double startAngle = element.ElementDoubles[8];
        double endAngle = element.ElementDoubles[9];
        double rotation = element.ElementDoubles[10];

        bool isClosed = Math.Abs(endAngle - startAngle - 2 * Math.PI) < 1e-6;
        double rotationDeg = rotation * 180 / Math.PI;

        string path;
        if (isClosed)
        {
            path =
                $"M {startX - majorR} {startY} " +
                $"A {majorR} {minorR} {rotationDeg} 1 0 {startX + majorR} {startY} " +
                $"A {majorR} {minorR} {rotationDeg} 1 0 {startX - majorR} {startY}";
        }
        else
        {
            double sweep = endAngle - startAngle;
            int largeArc = Math.Abs(sweep) > Math.PI ? 1 : 0;
            int sweepFlag = sweep > 0 ? 1 : 0;

            path = string.Format(
                CultureInfo.InvariantCulture,
                "M {0} {1} A {2} {3} {4} {5} {6} {7} {8}",
                startX, startY,
                majorR, minorR,
                rotationDeg,
                largeArc, sweepFlag,
                endX, endY
            );
        }

        return isFirstSegment ? path : path.TrimStart('M');
    }

    // -------------------------------------------------------------
    // BuildBSplinePath: Returns SVG path string for B-spline.
    // Converts control points into line segments (approximation).
    // Adds 'M' for first segment.
    // -------------------------------------------------------------
    private string BuildBSplinePath(GimElement element, bool isFirstSegment)
    {
        GetBSplineData(
            element,
            out int degree,
            out int totalControlPoints,
            out int segmentCount,
            out List<(double X, double Y)> cps
        );

        if (cps.Count < 2)
            return "";

        string path = isFirstSegment
            ? $"M {cps[0].X:F4} {cps[0].Y:F4} "
            : $"L {cps[0].X:F4} {cps[0].Y:F4} ";

        for (int i = 1; i < cps.Count; i++)
        {
            path += $"L {cps[i].X:F4} {cps[i].Y:F4} ";
        }

        return path.Trim();
    }

    private void GetBSplineData(
        GimElement element,
        out int degree,
        out int totalControlPoints,
        out int segmentCount,
        out List<(double X, double Y)> cps)
    {
        int poleStartIndex = 8;

        degree = element.ElementInts[^1];
        int numKnots = element.ElementInts[0];
        int uniqueKnots = element.ElementInts.Length - 2;

        totalControlPoints = numKnots - degree - 1;
        segmentCount = uniqueKnots - 1;

        cps = new List<(double X, double Y)>();

        for (int i = 0; i < totalControlPoints; i++)
        {
            double x = element.ElementDoubles[poleStartIndex + i * 3 + 0];
            double y = element.ElementDoubles[poleStartIndex + i * 3 + 1];
            cps.Add((x, y));
        }
    }

    private void WriteLine(GimElement element, GimData gimData, SvgGroup group)
    {
        GetLinePoints(element, out var x1, out var y1, out var x2, out var y2);
        var path = new SvgPath
        {
            Stroke = new SvgColourServer(color),
            Fill = SvgPaintServer.None
        };

        if (element.Elements.Count > 0)
        {
            var children = element.GetAllChildrenIndexesOrdered(gimData.Elements.IndexOf(element), gimData);
            foreach (var child in children)
            {
                var childElement = gimData.Elements[child.Item1];
                if (childElement.ElementType == GimElementType.Material)
                {
                    var material = childElement.AsMaterial();
                    if (material != null)
                    {
                        AssignMaterialsToSvgGroup(gimData, element, path);
                    }
                }
            }
        }

        path.PathData = SvgPathBuilder.Parse($"M{x1} {y1} L{x2} {y2}");
        group.Children.Add(path);
    }

    private void WritePoint(GimElement element, GimData gimData, SvgGroup group)
    {
        var circle = new SvgCircle
        {
            CenterX = (float)element.ElementDoubles[0],
            CenterY = (float)element.ElementDoubles[1],
            Radius = new SvgUnit(0),
            //Fill = new SvgColourServer(Color.Black),
        };

        circle.CustomAttributes["data-gim-type"] = "P_vertex";
        AssignMaterialsToSvgGroup(gimData, element, circle);
        group.Children.Add(circle);
        //TODO ask if this is the best way or that it is better to store as strings
    }

    private void WriteEllipse(GimElement element, GimData gimdata, SvgGroup group)
    {
        double startX = element.ElementDoubles[0];
        double startY = element.ElementDoubles[1];
        double endX = element.ElementDoubles[3];
        double endY = element.ElementDoubles[4];
        double majorR = element.ElementDoubles[6];
        double minorR = element.ElementDoubles[7];
        double startAngle = element.ElementDoubles[8];
        double endAngle = element.ElementDoubles[9];
        double rotation = element.ElementDoubles[10];

        double rotationDeg = rotation * 180 / Math.PI;

        // --- Center Calculation ---
        double centerX, centerY;
        if (Math.Abs(rotation) > 1e-6)
        {
            centerX = startX - majorR * Math.Cos(startAngle) * Math.Cos(rotation)
                            + minorR * Math.Sin(startAngle) * Math.Sin(rotation);

            centerY = startY - majorR * Math.Cos(startAngle) * Math.Sin(rotation)
                            - minorR * Math.Sin(startAngle) * Math.Cos(rotation);
        }
        else
        {
            centerX = startX - majorR * Math.Cos(startAngle);
            centerY = startY - minorR * Math.Sin(startAngle);
        }

        // --- Full Ellipse? ---
        bool isClosed = Math.Abs(endAngle - startAngle - 2 * Math.PI) < 1e-6;

        if (isClosed)
        {
            var ellipse = new SvgEllipse
            {
                CenterX = new SvgUnit((float)centerX),
                CenterY = new SvgUnit((float)centerY),
                RadiusX = new SvgUnit((float)majorR),
                RadiusY = new SvgUnit((float)minorR)
            };

            if (Math.Abs(rotation) > 1e-6)
            {
                ellipse.Transforms = new SvgTransformCollection
                {
                    new SvgRotate((float)rotationDeg, (float)centerX, (float)centerY)
                };
            }

            group.Children.Add(ellipse);
            return;
        }

        // --- Arc Path ---
        double sweep = endAngle - startAngle;
        int largeArc = Math.Abs(sweep) > Math.PI ? 1 : 0;
        int sweepFlag = sweep > 0 ? 1 : 0;

        string pathData = string.Format(
            CultureInfo.InvariantCulture,
            "M {0} {1} A {2} {3} {4} {5} {6} {7} {8}",
            startX, startY,
            majorR, minorR,
            rotationDeg,
            largeArc, sweepFlag,
            endX, endY
        );

        var path = new SvgPath
        {
            PathData = SvgPathBuilder.Parse(pathData),
            Fill = SvgPaintServer.None
        };

        AssignMaterialsToSvgGroup(gimdata, element, path);

        group.Children.Add(path);
    }

    //
    // function to convert the universal fromatted text back to normal readeble text
    //

    private string ConvertUniversalToPlain(string universalText)
    {
        if (string.IsNullOrEmpty(universalText))
            return universalText;

        var result = new StringBuilder();
        int i = 0;

        while (i < universalText.Length)
        {
            if (universalText[i] == '§')
            {
                int end = universalText.IndexOf('§', i + 1);
                if (end > i)
                {
                    string code = universalText.Substring(i + 1, end - i - 1);
                    i = end + 1;

                    // Special symbols - render them
                    if (code == "DEG")
                    {
                        result.Append("°");
                    }
                    else if (code == "PM")
                    {
                        result.Append("±");
                    }
                    else if (code == "DIA")
                    {
                        result.Append("Ø");
                    }
                    else if (code == "BULLET")
                    {
                        result.Append("•");
                    }
                    // Stacked text (fractions/tolerances) - render simplified
                    else if (code.StartsWith("STACK:"))
                    {
                        // Extract content after the type (STACK:TOL:, STACK:DIAG:, STACK:HORIZ:)
                        int lastColon = code.LastIndexOf(':');
                        if (lastColon > 0)
                            result.Append(code.Substring(lastColon + 1));
                    }
                    // Unicode character
                    else if (code.StartsWith("UNI:"))
                    {
                        try
                        {
                            int cp = Convert.ToInt32(code.Substring(4), 16);
                            result.Append(char.ConvertFromUtf32(cp));
                        }
                        catch { }
                    }
                    // Line breaks
                    else if (code == "BR" || code == "DIMWRAP")
                    {
                        result.Append('\n');
                    }
                    else if (code == "NEWCOL")
                    {
                        result.Append('\n'); // Or use a different separator like "\t\t"
                    }
                    // Text decorations - could optionally render with unicode
                    else if (code == "U+")
                    {
                        // Optionally: result.Append("[U]"); to show underline started
                    }
                    else if (code == "U-")
                    {
                        // Optionally: result.Append("[/U]"); to show underline ended
                    }
                    else if (code == "U_TOGGLE")
                    {
                        // Toggle from DTEXT format
                    }
                    // All other formatting codes (HEIGHT, FONT, COLOR, WIDTH, etc.) 
                    // are skipped - they don't add visible content
                }
                else
                {
                    result.Append(universalText[i]);
                    i++;
                }
            }
            else
            {
                result.Append(universalText[i]);
                i++;
            }
        }

        return result.ToString();
    }

    private void WriteText(GimElement element, GimData gimData, SvgGroup group, double dimScale)
    {
        if (element.Locals == null || element.Locals.Count == 0)
            return;

        var local = element.Locals[0];

        double insertX = local.Px;
        double insertY = local.Py;
        double Xx = local.Xx;
        double Xy = local.Xy;
        double height = element.ElementDoubles[0];      
        double attachmentPoint = element.ElementDoubles.Length > 1 ? element.ElementDoubles[1] : 0.0;
        double lineSpacing = element.ElementDoubles.Length > 2 ? element.ElementDoubles[2] : 1.0;
        double rectangleWidth = element.ElementDoubles.Length > 3 ? element.ElementDoubles[3] : 0.0;

        double rotationRad = Math.Atan2(Xy, Xx);
        double rotationDeg = rotationRad * (180.0 / Math.PI);

        string universalText = Encoding.UTF8.GetString(element.ElementBytes);
        string content = ConvertUniversalToPlain(universalText);
        if (string.IsNullOrWhiteSpace(content))
            return;

        double actualHeight = height;
        double lineHeight = actualHeight * lineSpacing;
        double fontSize = actualHeight * dimScale; 

        string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string firstLine = lines[0];

        string remainingText = lines.Length > 1
                                ? string.Join("\n", lines.Skip(1))
                                : string.Empty;

        List<string> wrappedLines = rectangleWidth > 0
                                    ? WrapTextToWidth(remainingText, rectangleWidth, fontSize)
                                    : new List<string> { remainingText };
        if (wrappedLines.Count == 0)
            return;
       
        double totalHeight = (lines.Length - 1) * lineHeight;

        var text = new SvgText()
        {
            X = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, 0) },
            Y = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, 0) },
            FontSize = new SvgUnit(SvgUnitType.User, (float)(actualHeight * dimScale)),
            TextAnchor = ConvertToSvgTextAnchor(attachmentPoint),
            Transforms = new SvgTransformCollection(),
            Text = firstLine
        };

        text.CustomAttributes["dominant-baseline"] = ConvertToDominantBaseline(attachmentPoint);

        if (lines.Length > 1)
        {
            foreach (var wrappedLine in wrappedLines)
            {
                string lineContentSpan = string.IsNullOrWhiteSpace(wrappedLine) ? " " : wrappedLine;
                var tspan = new SvgTextSpan()
                {
                    X = new SvgUnitCollection { new SvgUnit(0) },
                    Dy = new SvgUnitCollection { new SvgUnit((float)(lineHeight * dimScale)) },
                    Text = wrappedLine
                };
                text.Nodes.Add(tspan);
            }
        }

        text.Transforms.Add(new SvgTranslate((float)insertX, (float)insertY));

        if (rotationDeg != 0)
        {
            text.Transforms.Add(new SvgRotate((float)rotationDeg));
        }
        text.Transforms.Add(new SvgScale(1, -1));

        AssignMaterialsToSvgGroup(gimData, element, text);

        group.Children.Add(text);     
    }

    private SvgTextAnchor ConvertToSvgTextAnchor(double attachmentPoint)
    {
        int ap = (int)attachmentPoint;

        return ap switch
        {
            1 or 4 or 7 => SvgTextAnchor.Start,  
            2 or 5 or 8 => SvgTextAnchor.Middle, 
            3 or 6 or 9 => SvgTextAnchor.End,    
            _ => SvgTextAnchor.Start
        };
    }
    private string ConvertToDominantBaseline(double attachmentPoint)
    {
        int ap = (int)attachmentPoint;

        return ap switch
        {
            1 or 2 or 3 => "text-before-edge", 
            4 or 5 or 6 => "middle",           
            7 or 8 or 9 => "text-after-edge", 
            _ => "text-before-edge"
        };
    }

    private List<string> WrapTextToWidth(string content, double maxWidth, double fontSize)
    {
        var wrappedLines = new List<string>();

        if (string.IsNullOrEmpty(content))
        {
            wrappedLines.Add("");
            return wrappedLines;
        }

        if (maxWidth <= 0)
        {
            wrappedLines.Add(content);
            return wrappedLines;
        }
        double averageCharWidth = 0.55 * fontSize;

        string[] paragraphs = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (string paragraph in paragraphs)
        {
            if (paragraph.Length == 0)
            {
                wrappedLines.Add("");
                continue;
            }
            string currentLine = "";
            string[] words = paragraph.Split(' ', StringSplitOptions.None);

            foreach (string word in words)
            {
                string potentialLine = currentLine.Length == 0 ? word : currentLine + " " + word;

                if (potentialLine.Length * averageCharWidth <= maxWidth)
                {
                    currentLine = potentialLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        wrappedLines.Add(currentLine);
                    }

                    currentLine = word;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                wrappedLines.Add(currentLine);
            }
        }
        return wrappedLines;
    }

    private void WriteCircle(GimElement element, GimData gimData, SvgGroup group)
    {
        GetCircleGeometry(
            element,
            out double startX, out double startY,
            out double radius,
            out double startAngle, out double endAngle,
            out double centerX, out double centerY,
            out double endX, out double endY,
            out int largeArcFlag, out int sweepFlag,
            out bool isFullCircle
        );

        var path = new SvgPath
        {
            Fill = SvgPaintServer.None,
            Stroke = new SvgColourServer(color)
        };

        if (element.Elements.Count > 0)
        {
            AssignMaterialsToSvgGroup(gimData, element, path);
        }

        string pathData;

        if (isFullCircle)
        {
            pathData = string.Format(CultureInfo.InvariantCulture,
                "M {0:F4} {1:F4} A {2:F4} {2:F4} 0 1 0 {3:F4} {1:F4} " +
                "A {2:F4} {2:F4} 0 1 0 {0:F4} {1:F4}",
                centerX + radius, centerY,
                radius,
                centerX - radius
            );
        }
        else
        {
            pathData = string.Format(CultureInfo.InvariantCulture,
                "M {0:F4} {1:F4} A {2:F4} {2:F4} 0 {3} {4} {5:F4} {6:F4}",
                startX, startY,
                radius,
                largeArcFlag, sweepFlag,
                endX, endY
            );
        }

        path.PathData = SvgPathBuilder.Parse(pathData);
        group.Children.Add(path);
    }


    private void WriteBSpline(GimElement element, GimData gimData, SvgGroup group)
    {
        double startPointX = element.ElementDoubles[0];
        double startPointY = element.ElementDoubles[1];

        GetBSplineData(
            element,
            out int degree,
            out int totalControlPoints,
            out int segmentCount,
            out List<(double X, double Y)> cps
        );

        var path = new SvgPath
        {
            Fill = SvgPaintServer.None,
            Stroke = new SvgColourServer(color)
        };

        if (element.Elements.Count > 0)
        {
            AssignMaterialsToSvgGroup(gimData, element, path);
        }

        // BSpline path
        string pathData = string.Format(
            CultureInfo.InvariantCulture,
            "M {0:F4} {1:F4}",
            startPointX, startPointY
        );

        for (int i = 0; i < segmentCount; i++)
        {
            int idx = i * degree;

            pathData += string.Format(
                CultureInfo.InvariantCulture,
                " C {0:F4} {1:F4} {2:F4} {3:F4} {4:F4} {5:F4}",
                cps[idx + 1].X, cps[idx + 1].Y,
                cps[idx + 2].X, cps[idx + 2].Y,
                cps[idx + 3].X, cps[idx + 3].Y
            );
        }

        path.PathData = SvgPathBuilder.Parse(pathData);
        group.Children.Add(path);
    }


    private void WriteBezier2D(GimElement element, GimData gimData, SvgGroup group)
    {
        double startPointX = element.elementDoubles[0];
        double startPointY = element.elementDoubles[1];
        double endPointX = element.elementDoubles[3];
        double endPointY = element.elementDoubles[4];
        int elementLength = element.elementDoubles.Length;

        int poleStartIndex = 6;
        int poleCount = (elementLength - 8) / 2;
        var controlPointsXY = new List<(double X, double Y)>();
        for (int i = 0; i < poleCount; i++)
        {
            double x = element.elementDoubles[poleStartIndex + i * 2 + 0];
            double y = element.elementDoubles[poleStartIndex + i * 2 + 1];
            controlPointsXY.Add((x, y));
        }

        var path = new SvgPath
        {
            Fill = SvgPaintServer.None,
            Stroke = new SvgColourServer(color)
        };

        if (element.Elements.Count > 0)
        {
            AssignMaterialsToSvgGroup(gimData, element, path);
        }

        string pathData = string.Format(CultureInfo.InvariantCulture, "M {0:F4} {1:F4}", startPointX, startPointY);

        if (poleCount == 4)
        {
            pathData += string.Format(CultureInfo.InvariantCulture,
                "C {0:F4} {1:F4} {2:F4} {3:F4} {4:F4} {5:F4}",
                controlPointsXY[1].X, controlPointsXY[1].Y,
                controlPointsXY[2].X, controlPointsXY[2].Y,
                controlPointsXY[3].X, controlPointsXY[3].Y);
        }
        else if (poleCount == 3)
        {
            pathData += string.Format(CultureInfo.InvariantCulture,
                "Q {0:F4} {1:F4} {2:F4} {3:F4}",
                controlPointsXY[1].X, controlPointsXY[1].Y,
                controlPointsXY[2].X, controlPointsXY[2].Y);
        }

        path.PathData = SvgPathBuilder.Parse(pathData);
        group.Children.Add(path);
    }

    private void WriteSymbolOrPart(GimElement element, GimData gimData, SvgGroup group)
    {
        var doubles = element.ElementDoubles;
        if (doubles != null)
        {
            float scaleX = (float)doubles[0];
            float scaleY = (float)doubles[1];
            GimLocal local = element.Locals[^1];

            float insertX = (float)local.Px;
            float insertY = (float)local.Py;
            
            // Extract matrix components (example: Xx, Xy, Yx, Yy)
            double xx = local.Xx;
            double xy = local.Xy;
            double yx = local.Yx;
            double yy = local.Yy;

            // Calculate rotation in radians and then degrees
            double rotationRad = Math.Atan2(yx, xx);
            double rotationDeg = rotationRad * 180.0 / Math.PI;

            group.Transforms = new SvgTransformCollection {
            new SvgTranslate(insertX, insertY),
            new SvgRotate((float)rotationDeg),
            new SvgScale(scaleX,scaleY)
            };
        }
    }
    private void WritePlane(GimElement element, GimData gimData, SvgGroup group)
    {
        if (element.ElementType != GimElementType.F_plane)
            return;

        var elementIndex = gimData.Elements.IndexOf(element);
        var children = element.GetAllChildrenIndexesOrdered(elementIndex, gimData);
        GimElement hatchElement = null;
        GimElement wireElement = null;
        GimElement materialElement = null;

        foreach (var child in children)
        {
            var childElement = gimData.Elements[child.Item1];

            switch (childElement.ElementType)
            {
                case GimElementType.Material:
                    materialElement = childElement;
                    break;

                case GimElementType.Hatch:
                    hatchElement = childElement;
                    break;

                case GimElementType.wire:
                    wireElement = childElement;
                    break;
            }
        }

        if (hatchElement != null)
        {
            WriteHatch(element, gimData, group);
        }

        else
        {
            var visited = new HashSet<uint>();
            var bbox = ComputeHatchBoundaryBox(wireElement, gimData, visited);
            if (bbox.Extent == BoundingBoxExtent.Null) return;

            double minX = bbox.Min.X;
            double minY = bbox.Min.Y;
            double rectWidth = bbox.Width;
            double rectHeight = bbox.Height;

            string pathData = string.Format(CultureInfo.InvariantCulture,
                                 "M {0:F4} {1:F4} L {2:F4} {3:F4} {4:F4} {5:F4} {6:F4} {7:F4} Z",
                                 minX, minY,
                                 minX + rectWidth, minY,
                                 minX + rectWidth, minY + rectHeight,
                                 minX, minY + rectHeight);
            var path = new SvgPath
            {
                PathData = SvgPathBuilder.Parse(pathData),

            };
            AssignMaterialsToSvgGroup(gimData, element, path);
            group.Children.Add(path);
        }
    }

    private void WriteHatch(GimElement element, GimData gimData, SvgGroup group)
    {
        if (element.ElementType != GimElementType.F_plane || element.Locals == null || element.Locals.Count == 0)
            return;

        var hatchElement = element.Elements
           .Select(idx => gimData.Elements[(int)idx])
           .FirstOrDefault(e => e.ElementType == GimElementType.Hatch);

        if (hatchElement == null) return;

        var wireElement = element.Elements
           .Select(idx => gimData.Elements[(int)idx])
           .FirstOrDefault(e => e.ElementType == GimElementType.wire);

        if (wireElement == null) return;

        var visited = new HashSet<uint>();
        var bbox = ComputeHatchBoundaryBox(wireElement, gimData, visited);
        if (bbox.Extent == BoundingBoxExtent.Null) return;

        double minX = bbox.Min.X;
        double minY = bbox.Min.Y;
        double rectWidth = bbox.Width;
        double rectHeight = bbox.Height;
     
        var local = hatchElement.Locals[0];
        int lineElementCount = hatchElement.Elements.Count;
        double patternWidth = 0.0;
        double patternHeight = 0.0;

        if (lineElementCount == 1)
        {
            var offsetX = hatchElement.ElementDoubles[0];
            var offsetY = hatchElement.ElementDoubles[1];
            patternWidth = Math.Abs(offsetX);
            patternHeight = Math.Abs(offsetY);
        }
        else if (lineElementCount >= 2)
        {
            var baseX1 = hatchElement.Locals[0].Px;
            var baseY1 = hatchElement.Locals[0].Py;
            var baseX2 = hatchElement.Locals[1].Px;
            var baseY2 = hatchElement.Locals[1].Py;

            double deltaX = Math.Abs(baseX2 - baseX1);
            double deltaY = Math.Abs(baseY2 - baseY1);

            patternWidth = deltaX > 0.0001 ? deltaX : Math.Abs(hatchElement.ElementDoubles[0]);
            patternHeight = deltaY > 0.0001 ? deltaY : Math.Abs(hatchElement.ElementDoubles[1]);
        }
        else
        {
            patternWidth = rectWidth / 10.0;
            patternHeight = rectHeight / 10.0;
        }

        double dynamicStrokeWidth = patternWidth / 100.0;
        string patternId = $"hatch_{Interlocked.Increment(ref _patternCounter)}";

        var pattern = new SvgPatternServer
        {
            ID = patternId,
            X = new SvgUnit(0),
            Y = new SvgUnit(0),
            Width = new SvgUnit((float)Math.Round(patternWidth, 4)),
            Height = new SvgUnit((float)Math.Round(patternHeight, 4)),
            PatternUnits = SvgCoordinateUnits.UserSpaceOnUse
        };
       
        for (int i = 0; i < lineElementCount; i++)
        {
            var lineIdx = (int)hatchElement.Elements[i];
            var lineElement = gimData.Elements[lineIdx];

            if (lineElement.ElementType != GimElementType.C_line2D)
                continue;

            local = hatchElement.Locals[i];
            double basePointX = local.Px;
            double basePointY = local.Py;

            double x1 = lineElement.ElementDoubles[0];
            double y1 = lineElement.ElementDoubles[1];
            double x2 = lineElement.ElementDoubles[3];
            double y2 = lineElement.ElementDoubles[4];

            var dashElement = lineElement.Elements
                .Select(idx => gimData.Elements[(int)idx])
                .FirstOrDefault(e => e.ElementType == GimElementType.PatternLine);

            double[] dash = dashElement?.ElementDoubles;
            
            if (dash == null || dash.Length == 0)
            {
                var line = new SvgLine
                {
                    StartX = new SvgUnit(0f),
                    StartY = new SvgUnit(0f),
                    EndX = new SvgUnit((float)Math.Round(patternWidth, 4)),
                    EndY = new SvgUnit((float)Math.Round(patternHeight, 4)),
                    Stroke = new SvgColourServer(color),
                    StrokeWidth = (float)dynamicStrokeWidth
                };
                pattern.Children.Add(line);
            }
            else
            {
                double dx = x2 - x1;
                double dy = y2 - y1;
                double totalLen = Math.Sqrt(dx * dx + dy * dy);

                double ux = dx / totalLen;
                double uy = dy / totalLen;

                double pos = 0.0;

                while (pos < totalLen)
                {
                    for (int d = 0; d < dash.Length; d++)
                    {
                        if (pos >= totalLen) break;

                        double dashValue = dash[d];
                        double segLen = Math.Abs(dashValue);
                        bool isDrawing = dashValue > 0.0001;

                        double segEndPos = Math.Min(pos + segLen, totalLen);

                        if (isDrawing)
                        {
                            var line = new SvgLine
                                {
                                    StartX = new SvgUnit((float)Math.Round(x1 + ux * pos, 4)),
                                    StartY = new SvgUnit((float)Math.Round(y1 + uy * pos, 4)),
                                    EndX = new SvgUnit((float)Math.Round(x1 + ux * segEndPos, 4)),
                                    EndY = new SvgUnit((float)Math.Round(y1 + uy * segEndPos, 4)),
                                    Stroke = new SvgColourServer(color),
                                    StrokeWidth = (float)dynamicStrokeWidth
                                };
                            pattern.Children.Add(line);
                        }
                        pos += segLen;
                    }
                }
            }
        }

        var defs = svgDoc.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (defs != null)
        {
            defs.Children.Add(pattern);
        }
        string pathData = string.Format(CultureInfo.InvariantCulture,
                                  "M {0:F4} {1:F4} L {2:F4} {3:F4} {4:F4} {5:F4} {6:F4} {7:F4} Z",
                                  minX, minY, 
                                  minX + rectWidth, minY, 
                                  minX + rectWidth, minY + rectHeight, 
                                  minX, minY + rectHeight);
        var path = new SvgPath
        {
            PathData = SvgPathBuilder.Parse(pathData),
            Fill = new SvgDeferredPaintServer($"url(#{patternId})"),
            Stroke = new SvgColourServer(color),
            StrokeWidth = (float)dynamicStrokeWidth
        };

        group.Children.Add(path);
    }

    private BoundingBox ComputeHatchBoundaryBox(GimElement element, GimData gimData, HashSet<uint> visited)
    {
        if (visited == null)
            visited = new HashSet<uint>();

        var wireIndex = gimData.Elements.IndexOf(element);

        if (visited.Contains((uint)wireIndex))
            return BoundingBox.Null;
        visited.Add((uint)wireIndex);

        BoundingBox bbox = BoundingBox.Null;
      
        var wireElements = element.GetAllChildrenIndexesOrdered(wireIndex, gimData);

        bool first = true;
        foreach (var wireChild in wireElements)
        {
            var edge = gimData.Elements[wireChild.Item1];
            BoundingBox temp = BoundingBox.Null;

            switch (edge.ElementType)
            {
                case GimElementType.C_line2D:
                    temp = GetBoundingBoxFromLine(edge);
                    break;

                case GimElementType.C_circle2D:
                    temp = GetBoundingBoxFromCircle(edge);
                    break;

                case GimElementType.C_elipse2D:
                    temp = GetBoundingBoxFromEllipse(edge);
                    break;

                case GimElementType.C_bspline2D: 
                    temp = GetBoundingBoxFromSpline(edge);
                    break;

                case GimElementType.wire:
                    temp = ComputeHatchBoundaryBox(edge, gimData, visited);
                    break;

                default:
                    break;
            }
            if (!temp.Equals(BoundingBox.Null))
            {
                if (first)
                {
                    bbox = temp; 
                    first = false;
                }
                else
                {
                    bbox = bbox.Merge(temp); 
                }
            }

        }

        return bbox;
    }

    private BoundingBox GetBoundingBoxFromLine(GimElement line)
    {
        var d = line.ElementDoubles;
        var p1 = new XYZ(d[0], d[1], 0);
        var p2 = new XYZ(d[3], d[4], 0);
        return new BoundingBox(p1, p2);
    }

    private BoundingBox GetBoundingBoxFromCircle(GimElement circle)
    {
        GetCircleGeometry(
            circle,
            out double startX, out double startY,
            out double radius,
            out double startAngle, out double endAngle,
            out double centerX, out double centerY,
            out double endX, out double endY,
            out int largeArcFlag, out int sweepFlag,
            out bool isFullCircle
        );

        return new BoundingBox(
            centerX - radius, centerY - radius, 0,
            centerX + radius, centerY + radius, 0
        );
    }

    private BoundingBox GetBoundingBoxFromEllipse(GimElement ellipse)
    {
        var d = ellipse.ElementDoubles;
        double startX = d[0];
        double startY = d[1];
        double majorR = d[6];
        double minorR = d[7];
        double startAngle = d[8];
        double rotation = d[10];

        double centerX, centerY;
        if (Math.Abs(rotation) > 1e-6)
        {
            centerX = startX - majorR * Math.Cos(startAngle) * Math.Cos(rotation)
                            + minorR * Math.Sin(startAngle) * Math.Sin(rotation);
            centerY = startY - majorR * Math.Cos(startAngle) * Math.Sin(rotation)
                            - minorR * Math.Sin(startAngle) * Math.Cos(rotation);
        }
        else
        {
            centerX = startX - majorR * Math.Cos(startAngle);
            centerY = startY - minorR * Math.Sin(startAngle);
        }

        double maxRadius = Math.Max(majorR, minorR);
        return new BoundingBox(
            centerX - maxRadius, centerY - maxRadius, 0,
            centerX + maxRadius, centerY + maxRadius, 0
        );
    }

    private BoundingBox GetBoundingBoxFromSpline(GimElement spline)
    {
        GetBSplineData(
            spline,
            out int degree,
            out int totalControlPoints,
            out int segmentCount,
            out List<(double X, double Y)> cps
        );

        if (cps.Count == 0)
            return BoundingBox.Null;

        double minX = cps[0].X;
        double maxX = cps[0].X;
        double minY = cps[0].Y;
        double maxY = cps[0].Y;

        foreach (var cp in cps)
        {
            if (cp.X < minX) minX = cp.X;
            if (cp.X > maxX) maxX = cp.X;
            if (cp.Y < minY) minY = cp.Y;
            if (cp.Y > maxY) maxY = cp.Y;
        }

        return new BoundingBox(minX, minY, 0, maxX, maxY, 0);
    }

    private void AssignMaterialsToSvgGroup(GimData gimData, GimElement element, SvgElement svgElement)
    {     
        var children = element.GetAllChildrenIndexesOrdered(gimData.Elements.IndexOf(element), gimData);
        foreach (var child in children)
        {
            var childElement = gimData.Elements[child.Item1];
            if (childElement.ElementType == GimElementType.Material)
            {
                var material = childElement.AsMaterial();
                if (material != null)
                {
                    int argb = material.DiffuseColour != 0 ? material.DiffuseColour : material.BaseColour;
                    color = Color.FromArgb(argb);

                    if (svgElement is SvgPath path)
                    {
                        if (element.ElementType == GimElementType.F_plane)
                        {
                            path.Fill = new SvgColourServer(color);
                        }
                        else
                        {
                            path.Stroke = new SvgColourServer(color);
                            // LineWeight is disabled pending review of how it should be represented.
                            // double lineWidth = material.LineWeight;
                            path.StrokeWidth = 0.10f;
                        }
                    }
                    else if (svgElement is SvgText text)
                    {
                        text.Fill = new SvgColourServer(color);
                    }
                    else if (svgElement is SvgCircle circle)
                    {
                        circle.Fill = new SvgColourServer(color);
                    }
                        break;
                }
            }
        }
    }
}
