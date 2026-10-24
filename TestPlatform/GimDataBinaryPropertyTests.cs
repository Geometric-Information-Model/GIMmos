using System;
using System.IO;
using GIMcompress;
using GIMtransfer;
using NUnit.Framework;

namespace TestPlatform
{
    /// <summary>Checks property preservation through FileAccess binary conversion.</summary>
    [TestFixture]
    public sealed class GimDataBinaryPropertyTests
    {
        /// <summary>Round-trips typed property data and shared object links.</summary>
        [Test]
        // FileAccess must pass complete GimData through binary conversion.
        public void GimDataBinaryRoundTrip_PreservesLedPropertiesAndSharedLinks()
        {
            GimPropertySet ledProperties = GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("LED")),
                ("LED_TYPE", GimPropertyValue.FromString("COB LED")),
                ("Geolocation_X", GimPropertyValue.FromDouble(190000)));
            GimData gimData = new();
            // Valid scale metadata keeps geometry values readable after conversion.
            gimData.UnitCurr = new GimUnitSize((byte)0);
            gimData.PropertySets.Add(ledProperties);

            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);
            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0, 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 0 });
            gimData.Elements.Add(root);
            gimData.Elements.Add(child);
            gimData.Objects.Add(GimObjectFactory.Create(0, "root", 0));
            gimData.Objects.Add(GimObjectFactory.Create(1, "LED", 0));

            string fileName = "property-roundtrip-" + Guid.NewGuid().ToString("N");
            string binaryPath = GIMtransfer.FileAccess.WriteFromGimData(gimData, "bin", fileName, compress: false);
            try
            {
                GimData result = GIMtransfer.FileAccess.ReadToGimData(binaryPath, "bin");

                Assert.That(result.PropertySets, Has.Count.EqualTo(1));
                Assert.That(result.PropertySets[0].Entries["LED_TYPE"].Kind, Is.EqualTo(GimPropertyKind.String));
                Assert.That(result.PropertySets[0].Entries["LED_TYPE"].StringValue, Is.EqualTo("COB LED"));
                Assert.That(result.PropertySets[0].Entries["Geolocation_X"].Kind, Is.EqualTo(GimPropertyKind.Double));
                Assert.That(result.PropertySets[0].Entries["Geolocation_X"].DoubleValue, Is.EqualTo(190000d));
                Assert.That(result.Objects, Has.Count.EqualTo(2));
                Assert.That(result.Objects[0].PropertySetIndex, Is.EqualTo(0));
                Assert.That(result.Objects[1].PropertySetIndex, Is.EqualTo(0));
                Assert.That(result.Objects[0].PropertySetIndex, Is.EqualTo(result.Objects[1].PropertySetIndex));
            }
            finally
            {
                if (File.Exists(binaryPath)) File.Delete(binaryPath);
            }
        }
    }
}
