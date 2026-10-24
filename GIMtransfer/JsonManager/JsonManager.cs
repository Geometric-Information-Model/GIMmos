using GIMcompress;
using Newtonsoft.Json;
using System;
using System.IO;

namespace GIMtransfer
{
    /// <summary>
    /// Public entry point for reading GIM JSON documents. Dispatches to the
    /// version-specific <see cref="JsonReader"/> after a streaming-only sniff
    /// of the root <c>Version</c> property; see this folder's <c>README.md</c>
    /// for the full dispatch table, policy, and extension procedure.
    /// </summary>
    public static class JsonManager
    {
        /// <summary>
        /// Reads a GIM JSON document string into a <see cref="JsonGimObject"/>
        /// via the version-appropriate reader.
        /// </summary>
        /// <exception cref="NotImplementedException">
        /// Thrown when the file's schema version is strictly greater than
        /// <see cref="JsonVersionPolicy.LATEST_SUPPORTED_JSON_VERSION"/>. The
        /// throw happens after the streaming sniff and before any full parse.
        /// </exception>
        public static JsonGimObject ReadGimJson(string jsonText)
        {
            Version effectiveVersion = SniffEffectiveVersion(jsonText);

            if (JsonVersionPolicy.IsUnsupportedFuture(effectiveVersion))
            {
                throw new NotImplementedException(
                    JsonVersionPolicy.UnsupportedVersionMessage(effectiveVersion));
            }

            JsonReader reader = SelectReader(effectiveVersion);
            return reader.ReadJson(jsonText);
        }

        /// <summary>
        /// Picks the reader for <paramref name="effectiveVersion"/>. A
        /// <c>null</c> value means the sniff found no parsable root
        /// <c>Version</c> and routes to the legacy baseline. Callers must have
        /// already rejected unsupported-future versions via
        /// <see cref="JsonVersionPolicy.IsUnsupportedFuture"/>.
        /// </summary>
        private static JsonReader SelectReader(Version effectiveVersion)
        {
            switch (effectiveVersion)
            {
                case null:
                    return new JsonReaderV1_1_2_Older();
                case Version version when version < JsonVersionPolicy.V1_1_2:
                    return new JsonReaderV1_1_2_Older();
                case Version version when version < JsonVersionPolicy.V1_2:
                    return new JsonReaderV1_1_2_Older();
                case Version version when version < JsonVersionPolicy.V1_3:
                    return new JsonReaderV1_2();
                default:
                    return new JsonReaderV1_3();
            }
        }

        /// <summary>
        /// Streams <paramref name="jsonText"/> just far enough to read the
        /// root-level <c>Version</c> property without building a DOM. Returns
        /// <c>null</c> when the property is missing, null, or unparsable.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonTextReader.Depth"/> is used to distinguish root-level
        /// property names (<c>Depth == 1</c>) from nested <c>"Version"</c> keys
        /// deeper in the document, which must be ignored.
        /// </remarks>
        private static Version SniffEffectiveVersion(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;

            using var stringReader = new StringReader(jsonText);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                // Schema versions are plain strings; disable date parsing so a
                // "Version" literal that happens to look like a date is not
                // coerced into a DateTime token.
                DateParseHandling = DateParseHandling.None,
            };

            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonToken.StartObject) break;
            }

            if (jsonReader.TokenType != JsonToken.StartObject) return null;

            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonToken.EndObject && jsonReader.Depth == 0)
                {
                    return null;
                }

                if (jsonReader.TokenType != JsonToken.PropertyName) continue;

                bool isRootLevel = jsonReader.Depth == 1;
                string propertyName = jsonReader.Value as string;

                if (!isRootLevel || !string.Equals(propertyName, "Version", StringComparison.Ordinal))
                {
                    jsonReader.Skip();
                    continue;
                }

                if (!jsonReader.Read()) return null;

                string versionText = jsonReader.Value?.ToString();
                return JsonVersionPolicy.TryParseSchemaVersion(versionText, out Version parsed)
                    ? parsed
                    : null;
            }

            return null;
        }
    }
}
