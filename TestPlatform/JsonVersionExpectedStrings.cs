using System;
using GIMtransfer;

namespace TestPlatform
{
    /// <summary>
    /// Expected <see cref="GIMcompress.JsonGimObject.Version"/> strings for JSON reader tests
    /// (aligned with <see cref="GIMtransfer.JsonVersionPolicy.FormatSchemaVersionString"/>).
    /// </summary>
    internal static class JsonVersionExpectedStrings
    {
        public const string V1_2Line = "1.2.0";
        public const string LegacyBaseline = "1.1.2";

        public static string SchemaLineFor(Version v) => JsonVersionPolicy.FormatSchemaVersionString(v);
    }
}
