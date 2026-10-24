using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using GimUtilities;

namespace GIMtransfer.KlicImkl
{
    /// <summary>
    /// Forward-only reader for KLIC/IMKL 2.0 (WIBON) deliveries. Streams the XML with
    /// <see cref="XmlReader"/> — never a DOM — and emits a <see cref="KlicPayload"/> with the
    /// header fields, the utility networks, the theme-feature + UtilityLink joined lines and the
    /// selection area. See GIMmos ADR 0004.
    /// </summary>
    internal sealed class KlicImklReader
    {
        /// <summary>Progress bucket owned by this reader when the supplied status is not yet started.</summary>
        public const string ProgressTaskName = "KlicImklRead";

        private const string GmlIdNamespaceFragment = "opengis.net/gml";
        private const string XLinkNamespace = "http://www.w3.org/1999/xlink";
        private const int ProgressBlockBytes = 1024 * 1024;

        // Theme features that carry semantics and link to the geometry-bearing UtilityLink.
        private static readonly HashSet<string> ThemeFeatureNames = new(StringComparer.Ordinal)
        {
            "Elektriciteitskabel", // = electricity cable
            "Telecommunicatiekabel", // = telecommunications cable
            "Mantelbuis", // = casing pipe (duct)
            "Waterleiding", // = water pipe
            "Rioolleiding", // = sewer pipe
            "Duct", // = duct
            "OlieGasChemicalienPijpleiding", // = oil/gas/chemicals pipeline
            "ThermischePijpleiding", // = thermal pipeline
            "Kabelbed" // = cable bed
        };

        // Display-support features dropped in phase 1 (and non-operator registries / attachments).
        private static readonly HashSet<string> SkippedFeatureNames = new(StringComparer.Ordinal)
        {
            "Maatvoering", // = dimensioning annotation
            "Annotatie", // = annotation
            "Appurtenance", // = appurtenance
            "Kast", // = cabinet
            "TechnischGebouw", // = technical building
            "ExtraGeometrie", // = extra geometry
            "EigenTopografie", // = base topography
            "ExtraDetailinfo", // = extra detail info
            "DiepteTovMaaiveld", // = depth relative to ground level
            "DiepteNAP", // = depth relative to NAP (national datum)
            "Bijlage", // = attachment
            "Belanghebbende", // = interested party
            "Belang", // = interest
            "AanduidingEisVoorzorgsmaatregel" // = precautionary-measure requirement flag
        };

        private readonly KlicPayload payload = new();
        private readonly List<string> path = new();
        private readonly Dictionary<string, LinkBuilder> links = new(StringComparer.Ordinal);
        private readonly List<ThemeFeatureBuilder> themeFeatures = new();
        private readonly Dictionary<string, string> ownerNames = new(StringComparer.Ordinal);
        private readonly GimProgressStatus progressStatus;

        private bool ownsProgress;
        private long reportedBytes;
        private long pendingReportBytes;

        private bool inRequest; // true while inside imkl:GebiedsinformatieAanvraag (= area information request)
        private bool inOperatorRegistry; // true while inside imkl:Beheerder (= network operator registry)
        private bool inOrientation;
        private int posListDimension = 2;
        private string pendingSourceHolderCode; // bronhoudercode (= source holder code)
        private string pendingOperatorName; // beheerdernaam (= network operator name)
        private LinkBuilder currentLink;
        private ThemeFeatureBuilder currentTheme;
        private KlicNetwork currentNetwork;
        private KlicEnvelope pendingEnvelope;
        private List<(double X, double Y)> orientationCoords;

        private KlicImklReader(GimProgressStatus progressStatus, long expectedBytes)
        {
            this.progressStatus = progressStatus;
            if (progressStatus != null && !progressStatus.IsStarted)
            {
                int total = expectedBytes > 0 ? (int)Math.Min(expectedBytes, int.MaxValue) : 1;
                progressStatus.Create(new Dictionary<string, int> { { ProgressTaskName, total } });
                ownsProgress = true;
            }
        }

        /// <summary>Reads an IMKL delivery from a file path.</summary>
        /// <param name="path">Location of the XML delivery.</param>
        /// <param name="progressStatus">Optional progress status; started by the reader when untouched.</param>
        /// <returns>The parsed payload.</returns>
        public static KlicPayload Read(string path, GimProgressStatus progressStatus = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            using FileStream stream = new(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
            return Read(stream, progressStatus);
        }

        /// <summary>Reads an IMKL delivery from a stream (forward-only; not seeked).</summary>
        /// <param name="stream">Source stream positioned at the start of the document.</param>
        /// <param name="progressStatus">Optional progress status; started by the reader when untouched.</param>
        /// <returns>The parsed payload.</returns>
        public static KlicPayload Read(Stream stream, GimProgressStatus progressStatus = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            long length = stream.CanSeek ? stream.Length : -1;
            KlicImklReader reader = new(progressStatus, length);

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

        /// <summary>
        /// Peeks the head of an XML-family document and classifies it (see GIMmos ADR 0004).
        /// The supplied reader is consumed up to and including the root element's start tag, so
        /// callers re-open their source before parsing. IMKL namespace or <c>imkl-wibon</c>
        /// schemaLocation → <see cref="KlicKind.Klic"/>; KML or generic GML → <see cref="KlicKind.Network"/>;
        /// anything else → <see cref="KlicKind.NotNetwork"/>.
        /// </summary>
        /// <param name="reader">Reader positioned at the document start.</param>
        /// <returns>The classification verdict.</returns>
        public static KlicKind DetectKind(XmlReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            string rootLocalName = null;
            string rootNamespace = null;
            string schemaLocation = null;
            List<string> declaredNamespaces = new();

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                rootLocalName = reader.LocalName;
                rootNamespace = reader.NamespaceURI;

                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        if (reader.LocalName == "schemaLocation")
                            schemaLocation = reader.Value;
                        else if (reader.Prefix == "xmlns" || (reader.Prefix.Length == 0 && reader.LocalName == "xmlns"))
                            declaredNamespaces.Add(reader.Value);
                    }
                    while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }
                break;
            }

            if (rootLocalName == null) return KlicKind.NotNetwork;

            if (ContainsImkl(rootNamespace) || ContainsImkl(schemaLocation)) return KlicKind.Klic;
            foreach (string ns in declaredNamespaces)
                if (ContainsImkl(ns)) return KlicKind.Klic;

            if (string.Equals(rootLocalName, "kml", StringComparison.OrdinalIgnoreCase)) return KlicKind.Network;
            if (string.Equals(rootLocalName, "FeatureCollection", StringComparison.Ordinal)) return KlicKind.Network;
            if (rootNamespace != null && rootNamespace.IndexOf("opengis.net/gml", StringComparison.Ordinal) >= 0)
                return KlicKind.Network;

            return KlicKind.NotNetwork;
        }

        private static bool ContainsImkl(string value) =>
            !string.IsNullOrEmpty(value) && value.IndexOf("imkl", StringComparison.OrdinalIgnoreCase) >= 0;

        // Streams the whole document, keeping only the parse state needed for the join.
        private void Parse(XmlReader reader)
        {
            bool haveNode = reader.Read();
            while (haveNode)
            {
                bool nodeConsumed = false;
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (SkippedFeatureNames.Contains(reader.LocalName))
                        {
                            reader.Skip();
                            nodeConsumed = true;
                            break;
                        }
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

                haveNode = nodeConsumed ? !reader.EOF : reader.Read();
            }

            Join();
        }

        private void HandleElementStart(XmlReader reader)
        {
            string local = reader.LocalName;

            switch (local)
            {
                case "GebiedsinformatieLevering": // = area information delivery
                    payload.Kind = KlicKind.Klic;
                    // IMKL 2.0 delivers RD New (EPSG:28992) coordinates only; stamp it so the
                    // importer can transform to the storage SRID (KML/generic carry their own).
                    payload.SourceCrs = KlicCrs.ImklSourceCrs;
                    return;

                case "GebiedsinformatieAanvraag": // = area information request
                    inRequest = !reader.IsEmptyElement;
                    return;

                case "Beheerder": // = network operator registry
                    inOperatorRegistry = !reader.IsEmptyElement;
                    pendingSourceHolderCode = null;
                    pendingOperatorName = null;
                    return;

                case "UtilityLink":
                    currentLink = new LinkBuilder { SourceId = CanonicalGmlId(reader) };
                    return;

                case "Utiliteitsnet": // = utility network
                    string networkId = CanonicalGmlId(reader);
                    currentNetwork = new KlicNetwork { SourceId = networkId, OwnerCode = KlicText.OwnerCodeFromGmlId(networkId) };
                    return;

                case "Orientatiepolygoon": // = orientation polygon
                    inOrientation = !reader.IsEmptyElement;
                    orientationCoords = null;
                    return;

                case "Envelope":
                    BeginEnvelope(reader);
                    return;

                case "posList":
                    posListDimension = ReadIntAttribute(reader, "srsDimension") ?? 2;
                    return;
            }

            if (ThemeFeatureNames.Contains(local))
            {
                string featureId = CanonicalGmlId(reader);
                currentTheme = new ThemeFeatureBuilder
                {
                    FeatureType = local,
                    SourceId = featureId,
                    OwnerCode = KlicText.OwnerCodeFromGmlId(featureId)
                };
                return;
            }

            // Attribute-carried references and codelists (self-closing variants).
            string href = ReadAttribute(reader, "href", XLinkNamespace);

            if (local == "link")
            {
                if (currentTheme != null && href != null) currentTheme.LinkId = KlicText.NormalizeRef(href);
                return;
            }
            if (local == "inNetwork")
            {
                string target = KlicText.NormalizeRef(href);
                if (currentTheme != null) currentTheme.NetworkId = target;
                else if (currentLink != null) currentLink.NetworkId = target;
                return;
            }
            if (local == "utilityNetworkType" && currentNetwork != null)
            {
                // Theme = the coarse network class, and utilityNetworkType is the authoritative
                // source of that class (the per-cable feature-element names map 1:1 onto its
                // codelist). codeSpace is the vocabulary URI, not a value: its tail
                // ("utilityNetworkType") must never become the theme. With no href, the paired
                // text node supplies it.
                string theme = KlicText.CodelistTail(href);
                if (theme != null) currentNetwork.Theme = theme;
                return;
            }
            if (local == "thema") // = theme
            {
                // Type = the voltage/pressure band, and imkl:thema is the source of that band.
                // Same guard as utilityNetworkType: a bare codeSpace URI must not leak its own
                // tail ("thema") as the band when no href and no paired text arrive. The feature's
                // own theme (currentTheme.Theme) is kept as-is: on a feature it is the display band.
                string band = KlicText.CodelistTail(href);
                if (band == null) return; // paired element: the text node supplies the value
                if (currentNetwork != null) currentNetwork.Type = band;
                else if (currentTheme != null) currentTheme.Theme = band;
                return;
            }
            if (local == "currentStatus" && currentTheme != null)
            {
                currentTheme.Status = KlicText.CodelistTail(href);
                return;
            }
        }

        private void HandleElementEnd(string local)
        {
            switch (local)
            {
                case "GebiedsinformatieAanvraag":
                    inRequest = false;
                    return;

                case "Beheerder":
                    if (inOperatorRegistry)
                    {
                        if (!string.IsNullOrEmpty(pendingSourceHolderCode))
                            ownerNames[pendingSourceHolderCode] = pendingOperatorName ?? pendingSourceHolderCode;
                        inOperatorRegistry = false;
                    }
                    return;

                case "UtilityLink":
                    CompleteLink();
                    return;

                case "Utiliteitsnet":
                    if (currentNetwork != null)
                    {
                        currentNetwork.Index = payload.Networks.Count;
                        payload.Networks.Add(currentNetwork);
                        currentNetwork = null;
                    }
                    return;

                case "Orientatiepolygoon":
                    if (orientationCoords != null && orientationCoords.Count >= 3)
                    {
                        EnsureSelectionArea();
                        payload.SelectionArea.OrientationPolygonWkt = KlicGeometry.ToWktPolygon(orientationCoords);
                    }
                    inOrientation = false;
                    orientationCoords = null;
                    return;

                case "Envelope":
                    if (pendingEnvelope != null)
                    {
                        EnsureSelectionArea();
                        payload.SelectionArea.Envelope = pendingEnvelope;
                        pendingEnvelope = null;
                    }
                    return;
            }

            if (currentTheme != null && string.Equals(currentTheme.FeatureType, local, StringComparison.Ordinal))
            {
                themeFeatures.Add(currentTheme);
                currentTheme = null;
            }
        }

        private void HandleText(string raw)
        {
            if (path.Count == 0 || raw == null) return;
            string local = path[path.Count - 1];
            string value = raw.Trim();
            if (value.Length == 0) return;

            switch (local)
            {
                case "klicMeldnummer": // = KLIC delivery report number
                    payload.Meldnummer ??= value;
                    break;

                case "leveringsvolgnummer": // = re-delivery counter / delivery sequence number
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int deliverySequence))
                        payload.Leveringsvolgnummer = deliverySequence;
                    break;

                case "woonplaatsNaam": // = requester town/city name
                    if (inRequest && payload.WoonplaatsNaam == null) payload.WoonplaatsNaam = value;
                    break;

                case "bronhoudercode": // = source holder (data owner) code
                    if (inOperatorRegistry) pendingSourceHolderCode = value;
                    break;

                case "naam": // = name
                case "organisatienaam": // = organization name
                case "beheerdernaam": // = network operator name
                    if (inOperatorRegistry) pendingOperatorName = value;
                    break;

                case "utilityNetworkType":
                    // Paired-text form: the value is the coarse network class (Theme).
                    if (currentNetwork != null) currentNetwork.Theme = KlicText.CodelistTail(value) ?? value;
                    break;

                case "thema":
                    // On a feature this is the band it is displayed under; on a network it is the
                    // band too (Type) — the coarse class already came from utilityNetworkType.
                    if (currentTheme != null) currentTheme.Theme = KlicText.CodelistTail(value) ?? value;
                    else if (currentNetwork != null) currentNetwork.Type = KlicText.CodelistTail(value) ?? value;
                    break;

                case "currentStatus":
                case "status":
                    if (currentTheme != null) currentTheme.Status = KlicText.CodelistTail(value) ?? value;
                    break;

                case "verticalPosition":
                case "verticalPositionCategory":
                    if (currentTheme != null) currentTheme.VerticalPosition = KlicText.CodelistTail(value) ?? value;
                    break;

                case "validFrom":
                case "beginGeldigheid": // = start of validity
                    if (currentTheme != null)
                    {
                        currentTheme.ValidFromRaw = value;
                        currentTheme.ValidFrom = KlicDate.Parse(value);
                    }
                    break;

                case "validTo":
                case "eindGeldigheid": // = end of validity
                    if (currentTheme != null)
                    {
                        currentTheme.ValidToRaw = value;
                        currentTheme.ValidTo = KlicDate.Parse(value);
                    }
                    break;

                case "posList":
                    if (currentLink != null)
                    {
                        // The enclosing geometry/Curve locks one dimension; a mid-Curve srsDimension
                        // shift would silently re-interpret the already-accumulated segment axes, so
                        // a mismatch is malformed (abort the whole link, same policy as a bad token).
                        int dimension = posListDimension;
                        if (currentLink.GeometryDimension == null)
                        {
                            currentLink.GeometryDimension = dimension;
                        }
                        else if (currentLink.GeometryDimension.Value != dimension)
                        {
                            currentLink.InvalidGeometry = true;
                            GimLogger.Warn("KlicImkl: posList srsDimension changed within one geometry (" +
                                currentLink.GeometryDimension.Value + " then " + dimension +
                                "); aborting this geometry to avoid axis re-interpretation.");
                            break;
                        }

                        // A Curve may carry several LineStringSegments; accumulate them in order.
                        List<(double X, double Y)> segment = KlicGeometry.ParsePosList(value, dimension);
                        if (segment == null)
                        {
                            currentLink.InvalidGeometry = true; // malformed token: abort the whole link
                        }
                        else
                        {
                            currentLink.Coords ??= new List<(double X, double Y)>();
                            currentLink.Coords.AddRange(segment);
                        }
                    }
                    else if (inOrientation)
                    {
                        orientationCoords = KlicGeometry.ParsePosList(value, posListDimension);
                        if (orientationCoords == null)
                        {
                            // Malformed orientation ring: keep the selection area null-safe (no
                            // polygon) rather than silently dropping it with no trace.
                            GimLogger.Warn("KlicImkl: malformed orientatiepolygoon posList; " +
                                "dropping the selection polygon.");
                        }
                    }
                    break;

                case "lowerCorner":
                    if (pendingEnvelope != null && TryParsePair(value, out double lx, out double ly))
                    {
                        pendingEnvelope.MinX = lx;
                        pendingEnvelope.MinY = ly;
                    }
                    break;

                case "upperCorner":
                    if (pendingEnvelope != null && TryParsePair(value, out double ux, out double uy))
                    {
                        pendingEnvelope.MaxX = ux;
                        pendingEnvelope.MaxY = uy;
                    }
                    break;

                default:
                    if (currentTheme != null && KlicWidth.IsWidthElement(local))
                        currentTheme.WidthMm = KlicWidth.Parse(value);
                    break;
            }
        }

        private void BeginEnvelope(XmlReader reader)
        {
            if (payload.SelectionArea?.Envelope != null) return;
            pendingEnvelope = new KlicEnvelope { Crs = KlicCrs.Normalize(ReadAttribute(reader, "srsName")) };
        }

        private void CompleteLink()
        {
            if (currentLink != null)
            {
                if (!currentLink.InvalidGeometry && currentLink.Coords != null && currentLink.Coords.Count >= 2)
                    currentLink.Wkt = KlicGeometry.ToWktLineString(currentLink.Coords);
                // Keep WKT only: Coords is ~2x the geometry bytes across ~32k links and nothing
                // reads it after the WKT build (Join uses link.Wkt).
                currentLink.Coords = null;
                if (!string.IsNullOrEmpty(currentLink.SourceId)) links[currentLink.SourceId] = currentLink;
                currentLink = null;
            }
        }

        // Joins theme features to their UtilityLink geometry and resolves names/networks.
        private void Join()
        {
            foreach (KlicNetwork network in payload.Networks)
                network.OwnerName = ResolveOwnerName(network.OwnerCode);

            Dictionary<string, int> networkIndexById = new(StringComparer.Ordinal);
            foreach (KlicNetwork network in payload.Networks)
                if (!string.IsNullOrEmpty(network.SourceId)) networkIndexById[network.SourceId] = network.Index;

            foreach (ThemeFeatureBuilder feature in themeFeatures)
            {
                links.TryGetValue(feature.LinkId ?? string.Empty, out LinkBuilder link);

                // Theme feature's net:inNetwork wins; the link's own reference is the fallback.
                string networkRef = !string.IsNullOrEmpty(feature.NetworkId) ? feature.NetworkId : link?.NetworkId;
                int networkIndex = -1;
                if (!string.IsNullOrEmpty(networkRef) && networkIndexById.TryGetValue(networkRef, out int index))
                    networkIndex = index;
                KlicNetwork network = networkIndex >= 0 ? payload.Networks[networkIndex] : null;

                string ownerName = ResolveOwnerName(feature.OwnerCode);
                // Display band: the feature's own theme when it carries one, else the network's band
                // (its Type — the network Theme is the coarse class and must not be shown here).
                string theme = feature.Theme ?? network?.Type ?? feature.FeatureType;

                KlicLine line = new()
                {
                    NetworkIndex = networkIndex,
                    Geometry = link?.Wkt,
                    Type = theme,
                    Name = ownerName + " · " + theme + " · " + KlicText.LocalIdTail(feature.SourceId),
                    StartDate = feature.ValidFrom,
                    EndDate = feature.ValidTo
                };

                // network facts resolve via NetworkIndex; no per-line duplication (networkType,
                // theme, ownerCode and ownerName stay on the network the index points at).
                line.Metadata["widthMm"] = feature.WidthMm?.ToString(CultureInfo.InvariantCulture); // width rides metadata like every other per-line fact; DB + renderer read jsonb
                line.Metadata["status"] = feature.Status;
                line.Metadata["verticalPosition"] = feature.VerticalPosition;
                line.Metadata["validFrom"] = feature.ValidFromRaw;
                line.Metadata["validTo"] = feature.ValidToRaw;
                line.Metadata["sourceGmlId"] = feature.SourceId;
                line.Metadata["meldnummer"] = payload.Meldnummer; // = KLIC delivery report number
                line.Metadata["leveringsvolgnummer"] = payload.Leveringsvolgnummer?.ToString(CultureInfo.InvariantCulture); // = re-delivery counter

                payload.Lines.Add(line);
            }
        }

        private string ResolveOwnerName(string ownerCode)
        {
            if (string.IsNullOrEmpty(ownerCode)) return string.Empty;
            return ownerNames.TryGetValue(ownerCode, out string name) ? name : ownerCode;
        }

        private void EnsureSelectionArea() => payload.SelectionArea ??= new KlicSelectionArea();

        // Reports byte progress in ~1 MB blocks, but only for a status this reader started.
        private void OnBytesRead(int count)
        {
            reportedBytes += count;
            pendingReportBytes += count;
            if (!ownsProgress || pendingReportBytes < ProgressBlockBytes) return;
            progressStatus.EntityCompletedCallback(ProgressTaskName, (int)pendingReportBytes);
            pendingReportBytes = 0;
        }

        // Reads a gml:id and canonicalizes the dashed/dotted peer-id styles to one form.
        private static string CanonicalGmlId(XmlReader reader) => KlicText.CanonicalizeId(ReadGmlId(reader));

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

        private static bool TryParsePair(string text, out double first, out double second)
        {
            first = 0;
            second = 0;
            string[] tokens = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) return false;
            if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out first)) return false;
            if (!double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out second)) return false;
            return true;
        }

        private sealed class LinkBuilder
        {
            public string SourceId;
            public string NetworkId;
            public string Wkt;
            public List<(double X, double Y)> Coords;
            public bool InvalidGeometry;
            public int? GeometryDimension; // locked by the first posList of this link's geometry
        }

        private sealed class ThemeFeatureBuilder
        {
            public string FeatureType;
            public string SourceId;
            public string OwnerCode;
            public string LinkId;
            public string NetworkId;
            public string Theme;
            public string Status;
            public string VerticalPosition;
            public string ValidFromRaw;
            public string ValidToRaw;
            public DateTime? ValidFrom;
            public DateTime? ValidTo;
            public double? WidthMm;
        }
    }
}
