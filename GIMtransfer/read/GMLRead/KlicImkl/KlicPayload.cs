using System;
using System.Collections.Generic;

namespace GIMtransfer.KlicImkl
{
    /// <summary>
    /// Classification verdict for an XML-family upload: <c>Klic</c> = official KLIC/IMKL
    /// delivery, <c>Network</c> = generic networked GML/KML/XML, <c>NotNetwork</c> = XML-family
    /// file without network-line content (falls back to the mesh path). See GIMmos ADR 0004.
    /// </summary>
    public enum KlicKind
    {
        Klic,
        Network,
        NotNetwork
    }

    /// <summary>
    /// The result of parsing one IMKL delivery: header fields, networks, joined lines and the
    /// selection area. Emitted as JSON by the A2 entrypoint (see GIMmos ADR 0004).
    /// </summary>
    internal sealed class KlicPayload
    {
        /// <summary>
        /// Classification verdict of the parsed file. Defaults to <see cref="KlicKind.NotNetwork"/>
        /// so a parse that never sees a positive IMKL header stays safe standalone; only an
        /// <c>imkl:GebiedsinformatieLevering</c> header promotes it to <see cref="KlicKind.Klic"/>.
        /// </summary>
        public KlicKind Kind { get; set; } = KlicKind.NotNetwork;

        /// <summary>
        /// Source coordinate reference system of the file's 2D geometry, so the importer can
        /// ST_Transform into the storage SRID. URN form for IMKL (RD New, EPSG:28992) and KML
        /// (WGS84, EPSG:4326 — KML is WGS84 by OGC spec); "EPSG:NNNN" for a generic GML geometry
        /// that declares <c>srsName</c>; null when unknown. Always present as a JSON property so
        /// the API consumes one payload shape.
        /// </summary>
        public string SourceCrs { get; set; }

        /// <summary>The delivery report number (meldnummer) stamped on every feature.</summary>
        public string Meldnummer { get; set; }

        /// <summary>Re-delivery version counter for <see cref="Meldnummer"/>; null when absent.</summary>
        public int? Leveringsvolgnummer { get; set; }

        /// <summary>Requester town/city (woonplaatsNaam) from the request block.</summary>
        public string WoonplaatsNaam { get; set; }

        /// <summary>Utility networks declared in the delivery, in encounter order.</summary>
        public List<KlicNetwork> Networks { get; } = new();

        /// <summary>Joined theme-feature + UtilityLink lines, in theme-feature encounter order.</summary>
        public List<KlicLine> Lines { get; } = new();

        /// <summary>File-level selection geometry (envelope + orientation polygon); may be null.</summary>
        public KlicSelectionArea SelectionArea { get; set; }
    }

    /// <summary>One <c>imkl:Utiliteitsnet</c> declaration (type, theme and owning operator).</summary>
    internal sealed class KlicNetwork
    {
        /// <summary>Zero-based position in <see cref="KlicPayload.Networks"/>; referenced by lines.</summary>
        public int Index { get; set; }

        /// <summary>
        /// Network display/identity name. Null for IMKL deliveries (their identity is the gml:id
        /// in <see cref="SourceId"/> and the coarse class in <see cref="Theme"/>); for generic
        /// KML/GML files it is the KML Folder layer name or the sanitized feature element type.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Voltage/pressure band, codelist tail of <c>imkl:thema</c> (e.g. <c>middenspanning</c>,
        /// <c>laagspanning</c>, <c>gasLageDruk</c>, <c>datatransport</c>). Type = the band.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Coarse network class, codelist tail of <c>us-net-common:utilityNetworkType</c> (e.g.
        /// <c>elektriciteit</c>, <c>telecommunicatie</c>, <c>riolering</c>). Theme = the coarse
        /// network class: the per-cable feature-element names (Elektriciteitskabel, Mantelbuis, …)
        /// map 1:1 onto these codelists, and the network's utilityNetworkType is the authoritative
        /// version of the same fact.
        /// </summary>
        public string Theme { get; set; }

        /// <summary>Owner code parsed from the gml:id prefix, e.g. <c>KL1040</c>.</summary>
        public string OwnerCode { get; set; }

        /// <summary>Organization name resolved from the file's Beheerder (network-operator registry) map; falls back to code.</summary>
        public string OwnerName { get; set; }

        /// <summary>Full gml:id of the network declaration.</summary>
        public string SourceId { get; set; }
    }

    /// <summary>
    /// One merged network line: a theme feature (cable/pipe/duct) joined to its geometry-bearing
    /// <c>us-net-common:UtilityLink</c>.
    /// </summary>
    internal sealed class KlicLine
    {
        /// <summary>Index into <see cref="KlicPayload.Networks"/>; <c>-1</c> when unresolved.</summary>
        public int NetworkIndex { get; set; } = -1;

        /// <summary>Geometry as WKT (LINESTRING); null when the link or geometry was missing.</summary>
        public string Geometry { get; set; }

        /// <summary>Minted display name <c>&lt;owner&gt; · &lt;band&gt; · &lt;local id tail&gt;</c>.</summary>
        public string Name { get; set; }

        /// <summary>Type = the voltage/pressure band (middenspanning/laagspanning/...), aligns with <see cref="KlicNetwork.Type"/>; used only for the minted display name.</summary>
        public string Type { get; set; }

        /// <summary>Parsed validFrom; null when absent/nil/sentinel.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Parsed validTo; null when absent/nil/sentinel.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Per-line metadata keys mirroring the plan's payload contract. Width/diameter rides here as
        /// <c>widthMm</c> (nullable) like every other per-line fact, and network facts are deliberately
        /// absent: resolve those via <see cref="NetworkIndex"/>, the single source (no per-line duplication).
        /// </summary>
        public Dictionary<string, string> Metadata { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>File-level selection geometry: the top-level envelope plus the dig-request polygon.</summary>
    internal sealed class KlicSelectionArea
    {
        /// <summary>Top-level <c>gml:Envelope</c>; null when the file carried none.</summary>
        public KlicEnvelope Envelope { get; set; }

        /// <summary><c>imkl:Orientatiepolygoon</c> polygon as WKT; null when absent.</summary>
        public string OrientationPolygonWkt { get; set; }
    }

    /// <summary>2D file-level bounding box with a normalized CRS identifier.</summary>
    internal sealed class KlicEnvelope
    {
        /// <summary>Normalized CRS, e.g. <c>EPSG:28992</c>.</summary>
        public string Crs { get; set; }

        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }
}
