using System;
using System.IO;
using GIMcompress;
using GIMtransfer;
using Newtonsoft.Json;
using NUnit.Framework;

namespace TestPlatform
{
    [TestFixture]
    public sealed class JsonManagerReadGimJsonTests
    {
        private static string FixturePath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", "JsonManager", fileName);

        private static string LoadFixture(string fileName) =>
            File.ReadAllText(FixturePath(fileName));

        [Test]
        public void ReadGimJson_UnsupportedFuture_ThrowsNotImplementedExceptionWithExpectedSubstrings()
        {
            string json = LoadFixture("unsupported_future.json");

            NotImplementedException ex = Assert.Throws<NotImplementedException>(() => JsonManager.ReadGimJson(json));

            Assert.That(ex.Message, Does.Contain("999"));
            Assert.That(ex.Message, Does.Contain("not supported").IgnoreCase);
            Assert.That(ex.Message, Does.Contain(JsonVersionPolicy.LATEST_SUPPORTED_JSON_VERSION.Major.ToString()));
        }

        [Test]
        public void ReadGimJson_Supported12_Minimal_AssignsCanonicalVersionAndMaterials()
        {
            JsonGimObject obj = JsonManager.ReadGimJson(LoadFixture("supported_1_2_minimal.json"));

            Assert.That(obj.Version, Is.EqualTo(JsonVersionExpectedStrings.V1_2Line));
            Assert.That(obj.Materials, Is.Not.Null);
            Assert.That(obj.Materials.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReadGimJson_Supported112_Minimal_AssignsCanonicalVersion()
        {
            JsonGimObject obj = JsonManager.ReadGimJson(LoadFixture("supported_1_1_2_minimal.json"));

            Assert.That(obj.Version, Is.EqualTo(JsonVersionExpectedStrings.SchemaLineFor(JsonVersionPolicy.V1_1_2)));
        }

        [Test]
        public void ReadGimJson_MissingVersion_AssignsLegacyCanonicalBaseline()
        {
            JsonGimObject obj = JsonManager.ReadGimJson(LoadFixture("missing_version.json"));

            Assert.That(obj.Version, Is.EqualTo(JsonVersionExpectedStrings.LegacyBaseline));
        }

        [Test]
        public void ReadGimJson_InvalidVersionToken_AssignsLegacyCanonicalBaseline()
        {
            JsonGimObject obj = JsonManager.ReadGimJson(LoadFixture("invalid_version_token.json"));

            Assert.That(obj.Version, Is.EqualTo(JsonVersionExpectedStrings.LegacyBaseline));
            Assert.That(obj.Version, Does.Not.Contain("not-a-version"));
        }

        [Test]
        public void ReadGimJson_NestedVersionInChild_SucceedsWithRootSniffedVersion()
        {
            JsonGimObject obj = JsonManager.ReadGimJson(LoadFixture("nested_version_in_child.json"));

            Assert.That(obj.Version, Is.EqualTo(JsonVersionExpectedStrings.V1_2Line));
            Assert.That(obj.Children, Is.Not.Null);
            Assert.That(obj.Children.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReadGimJson_EmptyString_ThrowsJsonReaderException()
        {
            Assert.Throws<JsonReaderException>(() => JsonManager.ReadGimJson(string.Empty));
        }
    }
}
