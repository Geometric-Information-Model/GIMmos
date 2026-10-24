using System;
using GIMtransfer;
using NUnit.Framework;

namespace TestPlatform
{
    [TestFixture]
    public sealed class JsonVersionPolicyTests
    {
        [TestCase("1", 1, 0, 0)]
        [TestCase("1.2", 1, 2, 0)]
        [TestCase("1.2.3", 1, 2, 3)]
        public void TryParseSchemaVersion_AcceptsValidThreePartForms(string text, int major, int minor, int build)
        {
            Assert.That(JsonVersionPolicy.TryParseSchemaVersion(text, out Version parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(new Version(major, minor, build, 0)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("1.0.0.0")]
        [TestCase("1.0.0.0.1")]
        [TestCase("a.b.c")]
        [TestCase("1.x.0")]
        [TestCase("-1.0.0")]
        [TestCase("1.-1.0")]
        [TestCase("1.0.-1")]
        public void TryParseSchemaVersion_RejectsInvalid(string text)
        {
            Assert.That(JsonVersionPolicy.TryParseSchemaVersion(text, out _), Is.False);
        }

        [Test]
        public void NormalizeRevision_NullsThrough()
        {
            Assert.That(JsonVersionPolicy.NormalizeRevision(null), Is.Null);
        }

        [Test]
        public void NormalizeRevision_ForcesRevisionZeroAndPreservesBuild()
        {
            Version input = new Version(1, 2, 3, 4);
            Version normalized = JsonVersionPolicy.NormalizeRevision(input);
            Assert.That(normalized, Is.EqualTo(new Version(1, 2, 3, 0)));
        }

        [Test]
        public void IsUnsupportedFuture_NullIsFalse()
        {
            Assert.That(JsonVersionPolicy.IsUnsupportedFuture(null), Is.False);
        }

        [Test]
        public void IsUnsupportedFuture_LatestSupportedIsFalse()
        {
            Assert.That(JsonVersionPolicy.LATEST_SUPPORTED_JSON_VERSION, Is.EqualTo(JsonVersionPolicy.V1_3));
            Assert.That(
                JsonVersionPolicy.IsUnsupportedFuture(JsonVersionPolicy.LATEST_SUPPORTED_JSON_VERSION),
                Is.False);
        }

        [Test]
        public void IsUnsupportedFuture_StrictlyGreaterIsTrue()
        {
            Assert.That(JsonVersionPolicy.IsUnsupportedFuture(new Version(999, 0, 0, 0)), Is.True);
        }

        [Test]
        public void UnsupportedVersionMessage_ContainsExpectedSubstrings()
        {
            Version fileVersion = new Version(999, 0, 0, 0);
            string message = JsonVersionPolicy.UnsupportedVersionMessage(fileVersion);

            Assert.That(message, Does.Contain("999"));
            Assert.That(message, Does.Contain("not supported").IgnoreCase);
            Assert.That(message, Does.Contain(JsonVersionPolicy.LATEST_SUPPORTED_JSON_VERSION.Major.ToString()));
            Assert.That(message, Does.Contain(JsonVersionPolicy.LATEST_SUPPORTED_JSON_VERSION.Minor.ToString()));
        }
    }
}
