using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using GimUtilities;

namespace GIMtransfer.KlicImkl
{
    /// <summary>
    /// Forward-only reader for generic (non-IMKL) KML and GML/XML network files. Streams the XML
    /// with <see cref="XmlReader"/> — never a DOM — and emits the same <see cref="KlicPayload"/>
    /// shape as the IMKL reader, so the API consumes one payload contract. The classification
    /// verdict is <see cref="KlicKind.Network"/>. Grouping: KML → one network per Folder layer
    /// (placemarks outside any Folder share one base-name network); generic GML → one network per
    /// distinct feature element type. See GIMmos ADR 0004.
    /// </summary>
    internal sealed class GenericGmlKmlReader
    {
        /// <summary>Progress bucket owned by this reader when the supplied status is not yet started.</summary>
        public const string ProgressTaskName = "GenericGmlKmlRead";

        private const string GmlIdNamespaceFragment = "opengis.net/gml";
        private const int ProgressBlockBytes = 1024 * 1024;

        // Element names that belong to the GML/KML schema machinery (containers, geometry and
        // envelope bookkeeping) rather than to an application feature type. A generic GML feature
        // is any other element that sits directly inside a feature container.
        private static readonly HashSet<string> StructuralNames = new(StringComparer.Ordinal)
        {
            "FeatureCollection", "featureMember", "featureMembers", "member",
            "boundedBy", "Envelope", "envelope", "lowerCorner", "upperCorner",
            "LineString", "LineStringSegment", "posList", "Point", "pos", "Polygon",
            "exterior", "interior", "LinearRing", "MultiGeometry", "geometry", "geometrie",
            "name", "description", "metaDataProperty",
            "validFrom", "validTo", "TimeSpan", "TimeStamp", "begin", "end", "when"
        };

        private static readonly HashSet<string> FeatureContainers = new(StringComparer.Ordinal)
        {
            "FeatureCollection", "featureMember", "featureMembers", "member"
        };

        private readonly KlicPayload payload = new() { Kind = KlicKind.Network };
        private readonly List<string> path = new();
        private readonly Dictionary<string, int> networkIndexByKey = new(StringComparer.Ordinal);
        private readonly GimProgressStatus progressStatus;

        private bool ownsProgress;
        private long pendingReportBytes;
        private bool isKml;
        private string fileBaseName = "network";

        // Selection extent, accumulated from the coordinates actually emitted (KML and GML share
        // it). Computed rather than read from a declared gml:boundedBy — see Finish().
        private bool haveCoords;
        private double minX, minY, maxX, maxY;

        // posList stride/separators, reset per LineString (see HandleGmlStart) so an earlier 3D or
        // comma-separated geometry never leaks its parsing shape onto a later dimensionless one.
        private int posListDimension = 2;
        private string posListCoordinateSeparator;
        private string posListTupleSeparator;

        // KML state: the open Folder stack and the placemark currently being read.
        private readonly List<FolderFrame> folders = new();
        private PlacemarkBuilder currentPlacemark;
        private StringBuilder coordinateBuffer;

        // Generic GML state: the feature element currently being read and whether its geometry is
        // being collected.
        private FeatureBuilder currentFeature;
        private bool geometryActive;

        private GenericGmlKmlReader(GimProgressStatus progressStatus, long expectedBytes, string baseName)
        {
            this.progressStatus = progressStatus;
            if (!string.IsNullOrEmpty(baseName)) fileBaseName = baseName;
            if (progressStatus != null && !progressStatus.IsStarted)
            {
                int total = expectedBytes > 0 ? (int)Math.Min(expectedBytes, int.MaxValue) : 1;
                progressStatus.Create(new Dictionary<string, int> { { ProgressTaskName, total } });
                ownsProgress = true;
            }
        }

        /// <summary>Reads a generic KML/GML file from a path.</summary>
        /// <param name="path">Location of the KML/GML file.</param>
        /// <param name="progressStatus">Optional progress status; started by the reader when untouched.</param>
        /// <returns>The parsed payload (Kind = Network).</returns>
        public static KlicPayload Read(string path, GimProgressStatus progressStatus = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            string baseName = Path.GetFileNameWithoutExtension(path);
            using FileStream stream = new(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
            return Read(stream, progressStatus, baseName);
        }

        /// <summary>Reads a generic KML/GML file from a stream (forward-only; not seeked).</summary>
        /// <param name="stream">Source stream positioned at the start of the document.</param>
        /// <param name="progressStatus">Optional progress status; started by the reader when untouched.</param>
        /// <param name="baseName">File base name used to name the network for placemarks outside any Folder.</param>
        /// <returns>The parsed payload (Kind = Network).</returns>
        public static KlicPayload Read(Stream stream, GimProgressStatus progressStatus = null, string baseName = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            long length = stream.CanSeek ? stream.Length : -1;
            GenericGmlKmlReader reader = new(progressStatus, length, baseName);

            Stream source = stream;
            if (reader.ownsProgress)
                source = new ProgressReportingStream(stream, reader.OnBytesRead);

            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };

            using XmlReader xmlReader = XmlReader.Create(source, settings);
            reader.Parse(xmlReader);

            if (reader.ownsProgress)
                progressStatus.ForceCompleteTask(ProgressTaskName);
            return reader.payload;
        }

        private void Parse(XmlReader reader)
        {
            bool haveNode = reader.Read();
            while (haveNode)
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        HandleElementStart(reader);
                        if (!reader.IsEmptyElement) path.Add(reader.LocalName);
                        break;

                    case XmlNodeType.EndElement:
                        HandleElementEnd(reader.LocalName);
                        if (path.Count > 0) path.RemoveAt(path.Count - 1);
                        break;

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.SignificantWhitespace:
                        HandleText(reader.Value);
                        break;
                }

                haveNode = reader.Read();
            }

            Finish();
        }

        private void HandleElementStart(XmlReader reader)
        {
            string local = reader.LocalName;

            if (path.Count == 0)
            {
                isKml = string.Equals(local, "kml", StringComparison.OrdinalIgnoreCase);
                // KML is WGS84 (lon,lat) by OGC spec: stamp the constant CRS for the importer.
                if (isKml) payload.SourceCrs = KlicCrs.KmlSourceCrs;
                return;
            }

            if (isKml) HandleKmlStart(reader, local);
            else HandleGmlStart(reader, local);
        }

        private void HandleElementEnd(string local)
        {
            if (isKml) HandleKmlEnd(local);
            else HandleGmlEnd(local);
        }

        private void HandleText(string raw)
        {
            if (path.Count == 0 || raw == null) return;
            string local = path[path.Count - 1];

            // <coordinates> text is buffered raw (untrimmed) across its text/CDATA nodes and parsed
            // once at the end tag: XmlReader does not merge adjacent text and CDATA, and trimming
            // each node would drop the separator between them.
            if (isKml && local == "coordinates")
            {
                coordinateBuffer?.Append(raw);
                return;
            }

            string value = raw.Trim();
            if (value.Length == 0) return;

            if (isKml) HandleKmlText(local, value);
            else HandleGmlText(local, value);
        }

        // ---- KML ------------------------------------------------------------------------------

        private void HandleKmlStart(XmlReader reader, string local)
        {
            switch (local)
            {
                case "Folder":
                    if (!reader.IsEmptyElement) folders.Add(new FolderFrame());
                    return;

                case "Placemark":
                    currentPlacemark = new PlacemarkBuilder { Id = ReadAttribute(reader, "id") };
                    if (reader.IsEmptyElement) CompletePlacemark();
                    return;

                case "coordinates":
                    // Open the per-element buffer; the end tag hands the full text to the parser.
                    coordinateBuffer = new StringBuilder();
                    return;
            }
        }

        private void HandleKmlEnd(string local)
        {
            switch (local)
            {
                case "Folder":
                    if (folders.Count > 0) folders.RemoveAt(folders.Count - 1);
                    return;

                case "Placemark":
                    CompletePlacemark();
                    return;

                case "coordinates":
                    FlushKmlCoordinates();
                    return;
            }
        }

        private void HandleKmlText(string local, string value)
        {
            // The Folder's own <name> names its layer/network. Must be read before the placemark
            // guard below (a Folder <name> precedes any Placemark).
            if (local == "name" && path.Count >= 2 && path[path.Count - 2] == "Folder")
            {
                if (folders.Count > 0 && folders[folders.Count - 1].Name == null)
                    folders[folders.Count - 1].Name = value;
                return;
            }

            if (currentPlacemark == null) return;

            switch (local)
            {
                case "name":
                    if (path.Count >= 2 && path[path.Count - 2] == "Placemark" && currentPlacemark.Name == null)
                        currentPlacemark.Name = value;
                    return;

                case "begin":
                    SetKmlDate(currentPlacemark, value, isEnd: false);
                    return;

                case "end":
                    SetKmlDate(currentPlacemark, value, isEnd: true);
                    return;

                case "when":
                    if (currentPlacemark.StartRaw == null) SetKmlDate(currentPlacemark, value, isEnd: false);
                    return;

                case "validFrom":
                    if (currentPlacemark.StartRaw == null) SetKmlDate(currentPlacemark, value, isEnd: false);
                    return;

                case "validTo":
                    if (currentPlacemark.EndRaw == null) SetKmlDate(currentPlacemark, value, isEnd: true);
                    return;
            }
        }

        // Parses the buffered <coordinates> text as one KML tuple list. Only the first LineString
        // coordinates of a placemark are taken: a MultiGeometry may also carry a Point or Polygon
        // (ignored in phase 1), and a Point/Polygon's own coordinates are never under a LineString,
        // so they can never win. A nested MultiGeometry is still one LineString-first walk, so the
        // first LineString encountered wins regardless of nesting depth.
        private void FlushKmlCoordinates()
        {
            string raw = coordinateBuffer?.ToString();
            coordinateBuffer = null;
            if (raw == null || currentPlacemark == null) return;
            if (currentPlacemark.InvalidGeometry || currentPlacemark.Coords != null) return;
            if (path.Count < 2 || path[path.Count - 2] != "LineString") return;

            List<(double X, double Y)> coords = ParseKmlCoordinates(raw);
            if (coords == null)
            {
                currentPlacemark.InvalidGeometry = true; // malformed token: drop the line
            }
            else
            {
                currentPlacemark.Coords = coords;
                AccumulateExtent(coords);
            }
        }

        private void CompletePlacemark()
        {
            if (currentPlacemark == null) return;

            // A Point/Polygon-only placemark (or one with malformed coordinates) emits no line.
            if (!currentPlacemark.InvalidGeometry &&
                currentPlacemark.Coords != null && currentPlacemark.Coords.Count >= 2)
            {
                // Phase-1 grouping choice: orphan placemarks (outside any Folder) merge into the
                // base-name network; nested folders key on the innermost (top-of-stack) folder.
                string key = folders.Count > 0 && !string.IsNullOrEmpty(folders[folders.Count - 1].Name)
                    ? folders[folders.Count - 1].Name
                    : fileBaseName;
                int networkIndex = GetOrCreateNetwork(key);
                string sourceId = currentPlacemark.Id ?? currentPlacemark.Name;

                KlicLine line = new()
                {
                    NetworkIndex = networkIndex,
                    Geometry = KlicGeometry.ToWktLineString(currentPlacemark.Coords),
                    Type = string.Empty, // generic files carry no voltage/pressure band
                    Name = currentPlacemark.Name ?? sourceId,
                    StartDate = currentPlacemark.Start,
                    EndDate = currentPlacemark.End
                };
                FillGenericMetadata(line, sourceId, currentPlacemark.StartRaw, currentPlacemark.EndRaw);
                payload.Lines.Add(line);
            }

            currentPlacemark = null;
        }

        private static void SetKmlDate(PlacemarkBuilder placemark, string value, bool isEnd)
        {
            if (isEnd)
            {
                placemark.EndRaw = value;
                placemark.End = KlicDate.Parse(value);
            }
            else
            {
                placemark.StartRaw = value;
                placemark.Start = KlicDate.Parse(value);
            }
        }

        // Parses KML coordinate tuples ("lon,lat[,alt]" separated by whitespace) into 2D points.
        // Returns null when any token is unparsable: dropping it silently would shift the tuples,
        // so the whole geometry is aborted instead (logged once), same policy as the IMKL reader.
        private static List<(double X, double Y)> ParseKmlCoordinates(string text)
        {
            List<(double X, double Y)> points = new();
            if (string.IsNullOrWhiteSpace(text)) return points;

            string[] tuples = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string tuple in tuples)
            {
                string[] parts = tuple.Split(',');
                if (parts.Length < 2 ||
                    !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    GimLogger.Warn("GenericGmlKml: unparsable KML coordinate '" + tuple +
                        "'; aborting this geometry to avoid axis shift.");
                    return null;
                }
                points.Add((x, y));
            }
            return points;
        }

        // ---- Generic GML ----------------------------------------------------------------------

        private void HandleGmlStart(XmlReader reader, string local)
        {
            string parent = path[path.Count - 1];

            if (currentFeature == null && IsFeatureElement(local, parent))
            {
                currentFeature = new FeatureBuilder
                {
                    ElementLocal = local,
                    TypeName = SanitizeTypeName(local),
                    SourceId = ReadGmlId(reader)
                };
                if (reader.IsEmptyElement) CompleteFeature();
                return;
            }

            if (local == "LineString" && currentFeature != null)
            {
                geometryActive = true;
                // Each gml:LineString is one geometry: lock its dimension here, not across the
                // whole feature, so independent line properties cannot falsely abort each other.
                currentFeature.GeometryDimension = null;
                // The posList parsing shape is per-geometry too: reset dimension/separators to
                // their defaults so a 3D or comma-separated earlier LineString never leaks its
                // stride onto a later dimensionless LineString.
                posListDimension = 2;
                posListCoordinateSeparator = null;
                posListTupleSeparator = null;
                string srs = KlicCrs.Normalize(ReadAttribute(reader, "srsName"));
                if (srs != null)
                {
                    payload.SourceCrs ??= srs;
                }
                return;
            }

            if (local == "posList")
            {
                posListDimension = ReadIntAttribute(reader, "srsDimension") ?? posListDimension;
                posListCoordinateSeparator = ReadAttribute(reader, "cs");
                posListTupleSeparator = ReadAttribute(reader, "ts");
                return;
            }
        }

        private void HandleGmlEnd(string local)
        {
            if (currentFeature != null &&
                string.Equals(local, currentFeature.ElementLocal, StringComparison.Ordinal))
            {
                CompleteFeature();
                return;
            }
            if (local == "LineString") geometryActive = false;
        }

        private void HandleGmlText(string local, string value)
        {
            if (local == "posList")
            {
                if (currentFeature != null && geometryActive)
                {
                    // A mid-geometry srsDimension shift would silently re-interpret the
                    // already-accumulated axes, so a mismatch aborts the whole geometry.
                    int dimension = posListDimension;
                    if (currentFeature.GeometryDimension == null)
                    {
                        currentFeature.GeometryDimension = dimension;
                    }
                    else if (currentFeature.GeometryDimension.Value != dimension)
                    {
                        currentFeature.InvalidGeometry = true;
                        GimLogger.Warn("GenericGmlKml: posList srsDimension changed within one geometry (" +
                            currentFeature.GeometryDimension.Value + " then " + dimension +
                            "); aborting this geometry to avoid axis re-interpretation.");
                        return;
                    }

                    List<(double X, double Y)> segment = KlicGeometry.ParsePosList(
                        value, dimension, posListCoordinateSeparator, posListTupleSeparator);
                    if (segment == null)
                    {
                        currentFeature.InvalidGeometry = true;
                    }
                    else
                    {
                        currentFeature.Coords ??= new List<(double X, double Y)>();
                        currentFeature.Coords.AddRange(segment);
                        AccumulateExtent(segment);
                    }
                }
                return;
            }

            if (currentFeature == null) return;

            switch (local)
            {
                case "name":
                    if (currentFeature.Name == null) currentFeature.Name = value;
                    break;

                case "validFrom":
                case "begin":
                case "when":
                    if (currentFeature.StartRaw == null)
                    {
                        currentFeature.StartRaw = value;
                        currentFeature.Start = KlicDate.Parse(value);
                    }
                    break;

                case "validTo":
                case "end":
                    if (currentFeature.EndRaw == null)
                    {
                        currentFeature.EndRaw = value;
                        currentFeature.End = KlicDate.Parse(value);
                    }
                    break;
            }
        }

        private void CompleteFeature()
        {
            if (currentFeature == null) return;

            if (!currentFeature.InvalidGeometry &&
                currentFeature.Coords != null && currentFeature.Coords.Count >= 2)
            {
                int networkIndex = GetOrCreateNetwork(currentFeature.TypeName);
                string sourceId = currentFeature.SourceId ?? currentFeature.Name;

                KlicLine line = new()
                {
                    NetworkIndex = networkIndex,
                    Geometry = KlicGeometry.ToWktLineString(currentFeature.Coords),
                    Type = string.Empty, // generic files carry no voltage/pressure band
                    Name = currentFeature.Name ?? sourceId ?? currentFeature.TypeName,
                    StartDate = currentFeature.Start,
                    EndDate = currentFeature.End
                };
                FillGenericMetadata(line, sourceId, currentFeature.StartRaw, currentFeature.EndRaw);
                payload.Lines.Add(line);
            }

            currentFeature = null;
            geometryActive = false;
        }

        private static bool IsFeatureElement(string local, string parent)
        {
            if (!FeatureContainers.Contains(parent)) return false;
            if (StructuralNames.Contains(local)) return false;
            return !string.Equals(local, "kml", StringComparison.OrdinalIgnoreCase);
        }

        // Lowercases and replaces anything that is not a letter/digit/underscore/dash, so an
        // element such as <Road> or <my.pipe> becomes a stable, file-safe network type name.
        private static string SanitizeTypeName(string localName)
        {
            if (string.IsNullOrWhiteSpace(localName)) return "feature";
            StringBuilder builder = new(localName.Length);
            foreach (char c in localName.Trim())
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? char.ToLowerInvariant(c) : '_');
            return builder.Length == 0 ? "feature" : builder.ToString();
        }

        private int GetOrCreateNetwork(string key)
        {
            if (networkIndexByKey.TryGetValue(key, out int index)) return index;

            // Generic files carry no authority metadata: there is no utilityNetworkType band and
            // no owner registry to resolve, so the network gets the coarse catch-all theme
            // "overig" (= "other"), an empty band Type and empty owner fields. The layer/type
            // identity rides Name (generic files have no network gml:id).
            KlicNetwork network = new()
            {
                Index = payload.Networks.Count,
                Name = key,
                Type = string.Empty,
                Theme = "overig",
                OwnerCode = string.Empty,
                OwnerName = string.Empty
            };
            networkIndexByKey[key] = network.Index;
            payload.Networks.Add(network);
            return network.Index;
        }

        // Fills the same per-line contract keys as the IMKL reader so the payload shape stays
        // uniform even where a generic file has no value for a key.
        private static void FillGenericMetadata(KlicLine line, string sourceId, string validFromRaw, string validToRaw)
        {
            line.Metadata["widthMm"] = null; // no width concept in generic KML/GML files
            line.Metadata["status"] = null;
            line.Metadata["verticalPosition"] = null;
            line.Metadata["validFrom"] = validFromRaw;
            line.Metadata["validTo"] = validToRaw;
            line.Metadata["sourceGmlId"] = sourceId;
            line.Metadata["meldnummer"] = null;
            line.Metadata["leveringsvolgnummer"] = null;
        }

        private void AccumulateExtent(IReadOnlyList<(double X, double Y)> points)
        {
            if (points == null) return;
            foreach ((double X, double Y) point in points)
            {
                if (!haveCoords)
                {
                    minX = maxX = point.X;
                    minY = maxY = point.Y;
                    haveCoords = true;
                    continue;
                }
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }
        }

        private void Finish()
        {
            // Selection area = the extent of the lines actually drawn. We deliberately do not
            // trust a declared gml:boundedBy: it can be absent, stale or wider than the content
            // and there is no mesh to fall back on for the extent, so consumers get the real one.
            if (haveCoords)
            {
                payload.SelectionArea = new KlicSelectionArea
                {
                    Envelope = new KlicEnvelope
                    {
                        Crs = KlicCrs.Normalize(payload.SourceCrs),
                        MinX = minX,
                        MinY = minY,
                        MaxX = maxX,
                        MaxY = maxY
                    }
                };
            }
        }

        // Reports byte progress in ~1 MB blocks, but only for a status this reader started.
        private void OnBytesRead(int count)
        {
            pendingReportBytes += count;
            if (!ownsProgress || pendingReportBytes < ProgressBlockBytes) return;
            progressStatus.EntityCompletedCallback(ProgressTaskName, (int)pendingReportBytes);
            pendingReportBytes = 0;
        }

        private static string ReadGmlId(XmlReader reader)
        {
            if (!reader.HasAttributes) return null;
            if (!reader.MoveToFirstAttribute()) return null;
            string id = null;
            do
            {
                if (reader.LocalName == "id" &&
                    reader.NamespaceURI.IndexOf(GmlIdNamespaceFragment, StringComparison.Ordinal) >= 0)
                {
                    id = reader.Value;
                    break;
                }
            }
            while (reader.MoveToNextAttribute());
            reader.MoveToElement();
            return id;
        }

        private static string ReadAttribute(XmlReader reader, string localName, string namespaceUri = null)
        {
            if (!reader.HasAttributes) return null;
            if (!reader.MoveToFirstAttribute()) return null;
            string value = null;
            do
            {
                if (reader.LocalName == localName && (namespaceUri == null || reader.NamespaceURI == namespaceUri))
                {
                    value = reader.Value;
                    break;
                }
            }
            while (reader.MoveToNextAttribute());
            reader.MoveToElement();
            return value;
        }

        private static int? ReadIntAttribute(XmlReader reader, string localName)
        {
            string raw = ReadAttribute(reader, localName);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;
        }

        private sealed class FolderFrame
        {
            public string Name;
        }

        private sealed class PlacemarkBuilder
        {
            public string Id;
            public string Name;
            public List<(double X, double Y)> Coords;
            public bool InvalidGeometry;
            public string StartRaw;
            public string EndRaw;
            public DateTime? Start;
            public DateTime? End;
        }

        private sealed class FeatureBuilder
        {
            public string ElementLocal;
            public string TypeName;
            public string SourceId;
            public string Name;
            public List<(double X, double Y)> Coords;
            public bool InvalidGeometry;
            public int? GeometryDimension; // locked by the first posList of one LineString
            public string StartRaw;
            public string EndRaw;
            public DateTime? Start;
            public DateTime? End;
        }
    }
}
