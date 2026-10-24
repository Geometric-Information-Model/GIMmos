using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GimUtilities;

namespace GIMtransfer.KlicImkl
{
    /// <summary>
    /// Culture-invariant text helpers for the IMKL reader: codelist tails, reference ids,
    /// owner-code extraction and local-id tails. Precedent to avoid: the locale-dependent
    /// <c>double.Parse</c> calls in the legacy GML path (<c>XDocToVectList.cs</c>).
    /// </summary>
    internal static class KlicText
    {
        // Strips everything up to the last URI path/urn separator, leaving the codelist value.
        public static string CodelistTail(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string value = raw.Trim();
            int cut = Math.Max(value.LastIndexOf('/'), Math.Max(value.LastIndexOf('#'), value.LastIndexOf(':')));
            return cut >= 0 && cut < value.Length - 1 ? value.Substring(cut + 1) : value;
        }

        // Turns an xlink href like "#nl.imkl-KL1040.link.1" into a canonical bare gml:id.
        public static string NormalizeRef(string href) => CanonicalizeId(href);

        // Canonicalizes the two IMKL peer-id styles seen in real files — dashed
        // "nl.imkl-<owner>.…" and dotted "nl.imkl.<owner>.…" — to the dashed form, so gml:id
        // dictionary keys and net:link/net:inNetwork hrefs join regardless of which style the
        // file mixed. A leading "#" is stripped. The rewrite is anchored to the "nl.imkl."
        // peer prefix (never an unanchored "imkl." match, which could hit a value that merely
        // contains that token); ids without that prefix pass through.
        public static string CanonicalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            string value = id.Trim();
            if (value.StartsWith("#", StringComparison.Ordinal)) value = value.Substring(1);

            const string DottedPrefix = "nl.imkl.";
            if (value.StartsWith(DottedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // "nl.imkl" (7 chars) + "-" + the remainder after the dotted separator.
                value = value.Substring(0, DottedPrefix.Length - 1) + "-" + value.Substring(DottedPrefix.Length);
            }
            return value;
        }

        // Extracts the owner code embedded after "imkl-" in a gml:id, e.g. KL1040 or GM0202.
        public static string OwnerCodeFromGmlId(string gmlId)
        {
            if (string.IsNullOrWhiteSpace(gmlId)) return null;
            int marker = gmlId.LastIndexOf("imkl-", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return null;
            string remainder = gmlId.Substring(marker + "imkl-".Length);
            int end = remainder.IndexOfAny(new[] { '.', '_', '-' });
            return end >= 0 ? remainder.Substring(0, end) : remainder;
        }

        // Returns the final dot-separated segment of a gml:id, e.g. 12406439.
        public static string LocalIdTail(string gmlId)
        {
            if (string.IsNullOrWhiteSpace(gmlId)) return null;
            int dot = gmlId.LastIndexOf('.');
            return dot >= 0 && dot < gmlId.Length - 1 ? gmlId.Substring(dot + 1) : gmlId;
        }
    }

    /// <summary>Normalizes the three CRS spellings observed in IMKL files to a single identifier.</summary>
    internal static class KlicCrs
    {
        /// <summary>Canonical SourceCrs of an IMKL delivery: RD New (EPSG:28992) in URN form.</summary>
        public const string ImklSourceCrs = "urn:ogc:def:crs:EPSG::28992";

        /// <summary>Canonical SourceCrs of KML: WGS84 (EPSG:4326) in URN form — KML is WGS84 by OGC spec.</summary>
        public const string KmlSourceCrs = "urn:ogc:def:crs:EPSG::4326";

        // urn:ogc:def:crs:EPSG::28992, EPSG:28992 and epsg:28992 all normalize to EPSG:28992.
        public static string Normalize(string srsName)
        {
            if (string.IsNullOrWhiteSpace(srsName)) return null;
            string[] parts = srsName.Trim().Split(':');
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string part = parts[i].Trim();
                if (part.Length > 0 && part.All(char.IsDigit)) return "EPSG:" + part;
            }
            return srsName.Trim();
        }
    }

    /// <summary>Nil/sentinel-safe date parsing for per-line validFrom/validTo values.</summary>
    internal static class KlicDate
    {
        private static readonly string[] Sentinels = { "1900-01-01", "1968-01-01" };

        // Returns null for empty/nil/sentinel (1900-01-01, 1968-01-01) values; 9999-12-31 is kept.
        public static DateTime? Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string value = raw.Trim();
            foreach (string sentinel in Sentinels)
                if (value.StartsWith(sentinel, StringComparison.Ordinal)) return null;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
                return parsed;

            // A bare date with a trailing Z ("2010-05-05Z") is not accepted by every parse mode.
            if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            {
                string trimmed = value.Substring(0, value.Length - 1);
                if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                    return parsed;
            }
            return null;
        }
    }

    /// <summary>Detects and reads width/diameter values (millimetres) from feature properties.</summary>
    internal static class KlicWidth
    {
        // ductWidth, buisdiameter (= pipe diameter), breedte (= width)... any width-ish property
        // qualifies; unit/uom properties and reference/attribute-carrying elements do not (they are
        // not values).
        public static bool IsWidthElement(string localName)
        {
            if (string.IsNullOrEmpty(localName)) return false;
            string name = localName.ToLowerInvariant();
            if (!(name.Contains("diameter") || name.Contains("breedte") /* = width */ || name.Contains("width"))) return false;
            if (name.Contains("unit") || name.Contains("uom")) return false;
            if (name.Contains("attribute") || name.EndsWith("ref", StringComparison.Ordinal)) return false;
            return true;
        }

        public static double? Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
        }
    }

    /// <summary>posList parsing and WKT rendering for the 2D IMKL geometries.</summary>
    internal static class KlicGeometry
    {
        // Parses a GML posList into 2D points. The optional cs/ts separators mirror the posList's
        // own attributes: cs separates the coordinates inside one tuple, ts separates tuples.
        // Both default to any GML whitespace run when absent (so a bare space-separated posList is
        // unchanged) and the default cs also supports the "," seen in real files.
        // Returns null when any token is unparsable: dropping the token silently would shift the
        // stride and mis-map axes, so the whole geometry is aborted instead (logged once). A token
        // count that is not a whole multiple of the stride is aborted for the same reason (a
        // trailing remainder would otherwise be dropped or pair lon/lat wrongly).
        // dimension defaults to 2; higher dimensions keep x/y and drop the rest.
        public static List<(double X, double Y)> ParsePosList(
            string text, int dimension, string coordinateSeparator = null, string tupleSeparator = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<(double X, double Y)>();

            int stride = dimension >= 2 ? dimension : 2;
            string[] tuples = SplitTokens(text, tupleSeparator);
            List<double> values = new(Math.Max(tuples.Length * 2, 2));
            foreach (string tuple in tuples)
            {
                foreach (string token in SplitTokens(tuple, coordinateSeparator))
                {
                    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    {
                        values.Add(value);
                        continue;
                    }
                    GimLogger.Warn("KlicImkl: unparsable posList token '" + token +
                        "'; aborting this geometry to avoid axis shift.");
                    return null;
                }
            }

            if (values.Count == 0) return new List<(double X, double Y)>();

            if (values.Count % stride != 0)
            {
                GimLogger.Warn("KlicImkl: posList token count " + values.Count + " is not a multiple of stride " +
                    stride + "; aborting this geometry to avoid axis mispairing.");
                return null;
            }

            List<(double X, double Y)> points = new();
            for (int i = 0; i + stride <= values.Count; i += stride)
                points.Add((values[i], values[i + 1]));
            return points;
        }

        // Splits on an explicit separator when given, otherwise on any GML whitespace run.
        private static string[] SplitTokens(string text, string separator) =>
            string.IsNullOrEmpty(separator)
                ? text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                : text.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

        public static string ToWktLineString(IReadOnlyList<(double X, double Y)> points)
        {
            if (points == null || points.Count < 2) return null;
            StringBuilder builder = new("LINESTRING(");
            AppendPoints(builder, points);
            builder.Append(')');
            return builder.ToString();
        }

        public static string ToWktPolygon(IReadOnlyList<(double X, double Y)> ring)
        {
            if (ring == null || ring.Count < 3) return null;
            StringBuilder builder = new("POLYGON((");
            AppendPoints(builder, ring);
            if (!IsClosed(ring))
            {
                builder.Append(", ");
                AppendPoint(builder, ring[0]);
            }
            builder.Append("))");
            return builder.ToString();
        }

        private static bool IsClosed(IReadOnlyList<(double X, double Y)> ring)
        {
            (double X, double Y) first = ring[0];
            (double X, double Y) last = ring[ring.Count - 1];
            return first.X == last.X && first.Y == last.Y;
        }

        private static void AppendPoints(StringBuilder builder, IReadOnlyList<(double X, double Y)> points)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                AppendPoint(builder, points[i]);
            }
        }

        private static void AppendPoint(StringBuilder builder, (double X, double Y) point)
        {
            builder.Append(point.X.ToString(CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(point.Y.ToString(CultureInfo.InvariantCulture));
        }
    }
}
