using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using GimUtilities;
using GIMtransfer.KlicImkl;

namespace GIMtransfer
{
    /// <summary>
    /// Result of <see cref="FileAccess.FileToNetworkPayloadAsync"/>: the classification verdict of
    /// the sniffed file plus, for <see cref="KlicKind.Klic"/>/<see cref="KlicKind.Network"/>, the
    /// emitted payload JSON and its temp output path. A <see cref="KlicKind.NotNetwork"/> verdict
    /// carries no path/JSON and tells the caller to fall back to
    /// <see cref="FileAccess.FileToJsonAsync"/> unchanged. See GIMmos ADR 0004.
    /// </summary>
    public sealed class NetworkPayloadResult
    {
        /// <summary>Classification verdict: <c>klic</c>, <c>network</c> or <c>notNetwork</c>.</summary>
        public KlicKind Kind { get; init; }

        /// <summary>Temp path of the written payload JSON; null for <see cref="KlicKind.NotNetwork"/>.</summary>
        public string OutputPath { get; init; }

        /// <summary>The payload JSON as a string; null for <see cref="KlicKind.NotNetwork"/>.</summary>
        public string Json { get; init; }

        /// <summary>KLIC delivery report number (meldnummer); null when absent/not a network file.</summary>
        public string Meldnummer { get; init; }

        /// <summary>Requester town/city (woonplaatsNaam); null when absent.</summary>
        public string WoonplaatsNaam { get; init; }

        /// <summary>Number of joined network lines in the payload.</summary>
        public int LineCount { get; init; }

        /// <summary>Number of utility networks in the payload.</summary>
        public int NetworkCount { get; init; }
    }

    public static partial class FileAccess
    {
        // Property names stay PascalCase (no naming policy, per the A2 contract); the enum value
        // serializes as the documented lowercase string ("klic"/"network"/"notNetwork").
        private static readonly JsonSerializerOptions NetworkPayloadJsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>
        /// Reads an XML-family file (xml/gml/kml lane) as a network-line payload instead of a
        /// GimData mesh. A first-KB content sniff (IMKL namespace/schemaLocation, KML or generic
        /// GML root) classifies the file and the streaming <see cref="KlicImklReader"/> emits the
        /// payload DTOs as JSON to the temp output path. <see cref="KlicKind.NotNetwork"/> is
        /// returned untouched for the caller to route to <see cref="FileToJsonAsync"/>. Never buffers
        /// the whole input document. See GIMmos ADR 0004.
        /// </summary>
        /// <param name="aSource">The path location of the file to read.</param>
        /// <param name="readStatus">Optional GimProgressStatus that tracks reading progress.</param>
        /// <param name="gimOptions">Gim options (kept for signature parity with the mesh read path).</param>
        /// <returns>A task producing the classification verdict and, for klic/network, the emitted payload.</returns>
        public static async Task<NetworkPayloadResult> FileToNetworkPayloadAsync(
            string aSource, GimProgressStatus readStatus, GimOptions gimOptions)
        {
            Task<NetworkPayloadResult> task = Task.Run(() => FileToNetworkPayload(aSource, readStatus, gimOptions));
            return await task;
        }

        internal static NetworkPayloadResult FileToNetworkPayload(
            string aSource, GimProgressStatus readStatus, GimOptions gimOptions)
        {
            if (string.IsNullOrEmpty(aSource)) throw new ArgumentException("Source path is required.", nameof(aSource));

            KlicKind kind = SniffNetworkKind(aSource);

            // notNetwork: signal the caller to fall back to the untouched FileToJsonAsync mesh path.
            if (kind == KlicKind.NotNetwork)
                return new NetworkPayloadResult { Kind = KlicKind.NotNetwork };

            // klic → the IMKL delivery reader (authority metadata, 28992); network → the generic
            // KML/GML reader (no authority metadata, per-geometry CRS). Both emit the same payload.
            KlicPayload payload = kind == KlicKind.Klic
                ? KlicImklReader.Read(aSource, readStatus)
                : GenericGmlKmlReader.Read(aSource, readStatus);

            // The sniff already classified the file klic (IMKL namespace present). An
            // Aanvraag-rooted document never reaches the GebiedsinformatieLevering header branch, so
            // stamp the IMKL CRS here at the entrypoint too: SourceCrs must be present for every
            // klic verdict, not only when the delivery header was seen.
            if (kind == KlicKind.Klic)
                payload.SourceCrs ??= KlicCrs.ImklSourceCrs;

            // A network-classified file that yielded no usable line content is effectively
            // notNetwork (ADR 0004: "XML-family file with no network-line content"): signal the
            // untouched mesh fallback and emit nothing rather than an empty payload.
            if (kind == KlicKind.Network && payload.Lines.Count == 0)
                return new NetworkPayloadResult { Kind = KlicKind.NotNetwork };

            payload.Kind = kind; // the sniff is authoritative for the returned classification

            string fileName = NetworkPayloadFileName(aSource);
            string json = JsonSerializer.Serialize(payload, NetworkPayloadJsonOptions);
            // Same per-file subfolder convention as the mesh writes (FileManagerPrivate WriteJsonToTemp
            // callers): "<name>\<name>.json" in the GIM temp root, so payloads from different imports
            // (and the flat mesh "<name>.json") cannot overwrite each other.
            string outputPath = WriteJsonToTemp(json, fileName + "\\" + fileName);

            return new NetworkPayloadResult
            {
                Kind = kind,
                OutputPath = outputPath,
                Json = json,
                Meldnummer = payload.Meldnummer,
                WoonplaatsNaam = payload.WoonplaatsNaam,
                LineCount = payload.Lines.Count,
                NetworkCount = payload.Networks.Count
            };
        }

        // First-KB content sniff: consumes only up to the root element's start tag, so a 200 MB
        // delivery peeks in constant memory.
        private static KlicKind SniffNetworkKind(string aSource)
        {
            using FileStream stream = new(aSource, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 1 << 12, FileOptions.SequentialScan);
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
            using XmlReader reader = XmlReader.Create(stream, settings);
            return KlicImklReader.DetectKind(reader);
        }

        private static string NetworkPayloadFileName(string aSource)
        {
            string name = Path.GetFileNameWithoutExtension(aSource);
            return string.IsNullOrEmpty(name) ? "network-payload" : name;
        }
    }
}
