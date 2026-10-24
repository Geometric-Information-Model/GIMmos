using GIMcompress;
using GIMtransfer;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text.Json;

namespace TestPlatform
{
    /// <summary>Checks reuse of visualization payloads and property output.</summary>
    public class PromotionGlbReuseTests
    {
        /// <summary>Verifies a supplied visualization payload is reused for an external child.</summary>
        [Test]
        public void GetJsonGimData_ReusesProvidedGlb_ForExternalReferenceChild()
        {
            GimData gimData = new();

            // Root element
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root, Elements = new List<uint> { 1 } });

            // External reference element (no geometry), with a single local under root.
            gimData.Elements.Add(new GimElement
            {
                ElementType = GimElementType.ExternalReference,
                Locals = new List<GimLocal> { new GimLocal { parent = 0 } },
                Elements = new List<uint>(),
                ElementInts = new[] { 123, -1 }
            });

            // One object pointing to the external reference element.
            gimData.Objects.Add(GimObjectFactory.Create(1, "Child"));

            string cachedGlb = "cached-glb-base64";
            var reuse = new Dictionary<int, string>
            {
                [0] = cachedGlb
            };

            JsonGimObject json = GIMtransfer.FileAccess.GetJsonGimData(gimData, visualisationUnitToBe: -1, progressStatus: null, reuseGlbsByChildId: reuse);

            Assert.That(json.GLBs, Is.Not.Null);
            Assert.That(json.GLBs.Length, Is.EqualTo(1));
            Assert.That(json.GLBs[0], Is.EqualTo(cachedGlb));
            Assert.That(json.Children, Has.Count.EqualTo(1));
            Assert.That(json.Children[0].ID, Is.EqualTo(0));
        }

        /// <summary>Verifies empty property data stays absent in JSON while DTO access remains non-null.</summary>
        [Test]
        public void GetJsonGimData_OmitsPropertyKeys_WhenNoPropertySetsExist()
        {
            GimData gimData = CreateExternalReferenceData("Child");

            JsonGimObject json = GIMtransfer.FileAccess.GetJsonGimData(
                gimData,
                visualisationUnitToBe: -1,
                progressStatus: null,
                reuseGlbsByChildId: new Dictionary<int, string> { [0] = "cached-glb" });

            Assert.That(json.PropertySets, Is.Empty);
            Assert.That(json.Children[0].PropertySetIndex, Is.Null);
            Assert.That(json.Children[0].Name, Is.EqualTo("Child"));

            string serialized = JsonSerializer.Serialize(json);
            Assert.That(serialized, Does.Not.Contain("PropertySets"));
            Assert.That(serialized, Does.Not.Contain("PropertySetIndex"));
        }

        /// <summary>Verifies property sets and child links are emitted with name precedence.</summary>
        [Test]
        public void GetJsonGimData_EmitsPropertySetsAndLink_WithSetNamePrecedence()
        {
            GimData gimData = CreateExternalReferenceData("door-17");
            gimData.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("LED")),
                ("type", GimPropertyValue.FromString("electrical_component")),
                ("LED_TYPE", GimPropertyValue.FromString("COB LED")),
                ("Geolocation_X", GimPropertyValue.FromDouble(190000)),
                ("Geolocation_Y", GimPropertyValue.FromDouble(400000)),
                ("coordinate_type", GimPropertyValue.FromString("RD"))));
            gimData.Objects[0].PropertySetIndex = 0;

            JsonGimObject json = GIMtransfer.FileAccess.GetJsonGimData(
                gimData,
                visualisationUnitToBe: -1,
                progressStatus: null,
                reuseGlbsByChildId: new Dictionary<int, string> { [0] = "cached-glb" });

            Assert.That(json.PropertySets, Has.Count.EqualTo(1));
            Assert.That(json.PropertySets[0]["name"].Kind, Is.EqualTo("string"));
            Assert.That(json.PropertySets[0]["name"].Value, Is.EqualTo("LED"));
            Assert.That(json.PropertySets[0]["Geolocation_X"].Kind, Is.EqualTo("double"));
            Assert.That(json.PropertySets[0]["Geolocation_X"].Value, Is.EqualTo(190000d));
            Assert.That(json.Children[0].PropertySetIndex, Is.EqualTo(0));
            Assert.That(json.Children[0].Name, Is.EqualTo("LED"));

            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(json));
            JsonElement propertySets = document.RootElement.GetProperty("PropertySets");
            Assert.That(propertySets[0].GetProperty("Geolocation_Y").GetDouble(), Is.EqualTo(400000d));
            Assert.That(document.RootElement.GetProperty("Children")[0].GetProperty("PropertySetIndex").GetInt32(), Is.EqualTo(0));
        }

        private static GimData CreateExternalReferenceData(string objectName)
        {
            GimData gimData = new();
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root, Elements = new List<uint> { 1 } });
            gimData.Elements.Add(new GimElement
            {
                ElementType = GimElementType.ExternalReference,
                Locals = new List<GimLocal> { new GimLocal { parent = 0 } },
                Elements = new List<uint>(),
                ElementInts = new[] { 123, -1 }
            });
            gimData.Objects.Add(GimObjectFactory.Create(1, objectName));
            return gimData;
        }
    }
}

