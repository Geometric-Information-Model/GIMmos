using System;

namespace GIMtransfer
{
    /// <summary>
    /// Central policy for GIM JSON schema version handling (version constants,
    /// parsing, and revision normalization).
    /// </summary>
    /// <remarks>
    /// See <c>README.md</c> in this folder for the full dispatch table, feature
    /// thresholds, parsing rules, null-safety contract, and the procedure for
    /// adding a new schema version. All <see cref="System.Version"/> values
    /// produced or compared here use <c>major.minor.build</c> with
    /// <see cref="System.Version.Revision"/> normalized to <c>0</c>.
    /// </remarks>
    internal static class JsonVersionPolicy
    {
        /// <summary>
        /// Maximum GIM JSON schema version this codebase implements.
        /// Versions strictly greater than this must cause the dispatcher to
        /// throw <see cref="NotImplementedException"/> before any full parse.
        /// See <c>README.md</c> for the dispatch table.
        /// </summary>
        public static readonly Version LATEST_SUPPORTED_JSON_VERSION = new Version(1, 3, 0, 0);

        /// <summary>
        /// Lowest schema version handled by a non-legacy reader.
        /// Files below this (and missing / unparsable versions) route to
        /// <c>JsonReaderV1_1_2_Older</c>.
        /// </summary>
        public static readonly Version V1_1_2 = new Version(1, 1, 2, 0);

        /// <summary>
        /// Feature threshold for Materials + per-child <c>MaterialID</c>
        /// (<c>JsonReaderV1_2</c>).
        /// </summary>
        public static readonly Version V1_2 = new Version(1, 2, 0, 0);

        /// <summary>
        /// Feature threshold for root property sets and nullable object links.
        /// </summary>
        public static readonly Version V1_3 = new Version(1, 3, 0, 0);

        /// <summary>
        /// Returns a copy of <paramref name="v"/> with
        /// <see cref="System.Version.Revision"/> forced to <c>0</c> and any
        /// missing <c>Build</c> coerced to <c>0</c>. Safe on <c>null</c>.
        /// </summary>
        public static Version NormalizeRevision(Version v)
        {
            if (v == null) return null;
            int build = v.Build < 0 ? 0 : v.Build;
            return new Version(v.Major, v.Minor, build, 0);
        }

        /// <summary>
        /// Normalized <c>major.minor.build</c> text for a schema
        /// <see cref="System.Version"/> (revision ignored).
        /// </summary>
        public static string FormatSchemaVersionString(Version v)
        {
            ArgumentNullException.ThrowIfNull(v);
            v = NormalizeRevision(v);
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }

        /// <summary>
        /// Parses a GIM JSON schema version string
        /// (<c>"major"</c>, <c>"major.minor"</c>, or <c>"major.minor.build"</c>)
        /// into a normalized <see cref="System.Version"/>. Rejects null,
        /// whitespace, or malformed input. See <c>README.md</c> for the exact
        /// acceptance rules.
        /// </summary>
        /// <returns><c>true</c> if <paramref name="versionText"/> parsed successfully.</returns>
        public static bool TryParseSchemaVersion(string versionText, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(versionText)) return false;

            string[] parts = versionText.Split('.');
            if (parts.Length < 1 || parts.Length > 3) return false;

            int major = 0, minor = 0, build = 0;

            if (!int.TryParse(parts[0], out major) || major < 0) return false;
            if (parts.Length >= 2 && (!int.TryParse(parts[1], out minor) || minor < 0)) return false;
            if (parts.Length >= 3 && (!int.TryParse(parts[2], out build) || build < 0)) return false;

            version = new Version(major, minor, build, 0);
            return true;
        }

        /// <summary>
        /// <c>true</c> if <paramref name="version"/> is strictly greater than
        /// <see cref="LATEST_SUPPORTED_JSON_VERSION"/>. Caller must have
        /// normalized <paramref name="version"/> beforehand.
        /// </summary>
        public static bool IsUnsupportedFuture(Version version)
        {
            if (version == null) return false;
            return version > LATEST_SUPPORTED_JSON_VERSION;
        }

        /// <summary>
        /// Canonical message for the <see cref="NotImplementedException"/>
        /// thrown when a file's schema version exceeds
        /// <see cref="LATEST_SUPPORTED_JSON_VERSION"/>.
        /// </summary>
        public static string UnsupportedVersionMessage(Version fileVersion)
        {
            return $"GIM JSON schema version {fileVersion} is not supported by this build " +
                   $"(latest supported: {LATEST_SUPPORTED_JSON_VERSION}).";
        }
    }
}
