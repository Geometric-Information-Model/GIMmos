using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GimBin;
using GIMcompress;
using GIMtransfer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TestPlatform
{
    /// <summary>Checks property-aware JSON reading and hydration.</summary>
    [TestFixture]
    public sealed class JsonReaderV1_3Tests
    {
        // Keep writer payload and standalone link targets structurally identical.
        private static GimData CreateData(bool includeProperties)
        {
            GimData data = new();
            if (includeProperties)
            {
                data.PropertySets.Add(GimPropertySet.Create(
                    ("name", GimPropertyValue.FromString("LED")),
                    ("type", GimPropertyValue.FromString("electrical_component")),
                    ("LED_TYPE", GimPropertyValue.FromString("COB LED")),
                    ("Geolocation_X", GimPropertyValue.FromDouble(190000))));
            }

            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);
            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 0 });
            data.Elements.Add(root);
            data.Elements.Add(child);
            data.Objects.Add(GimObjectFactory.Create(0, "legacy root", includeProperties ? 0 : null));
            data.Objects.Add(GimObjectFactory.Create(1, "legacy child", includeProperties ? 0 : null));

            return data;
        }

        // Use actual writer output without unrelated fixture setup.
        private static string CreatePayload()
        {
            return System.Text.Json.JsonSerializer.Serialize(GIMtransfer.FileAccess.GetJsonGimData(CreateData(true), 0));
        }

        /// <summary>Verifies writer output restores values and root/child references.</summary>
        [Test]
        public void ReadGimJsonV1_3_WriterPayloadRoundTripsLedSetsAndSharedLinks()
        {
            string payload = CreatePayload();
            JsonGimObject json = JsonManager.ReadGimJson(payload);
            GimData result = CreateData(false);
            var childObjects = GIMtransfer.FileAccess.GetUniqueChildObjects(result)
                .Select(objectIndex => result.Objects[objectIndex]).ToList();
            GIMtransfer.FileAccess.RestoreJsonPropertySetsAndLinks(json, result, childObjects);

            Assert.That(json.Version, Is.EqualTo("1.3.0"));
            Assert.That(json.PropertySets, Has.Count.EqualTo(1));
            Assert.That(json.PropertySets[0]["LED_TYPE"].Kind, Is.EqualTo("string"));
            Assert.That(json.PropertySets[0]["Geolocation_X"].Kind, Is.EqualTo("double"));
            Assert.That(json.PropertySetIndex, Is.EqualTo(0));
            Assert.That(json.Children[0].PropertySetIndex, Is.EqualTo(0));
            Assert.That(result.PropertySets, Has.Count.EqualTo(1));
            Assert.That(result.PropertySets[0].Entries["LED_TYPE"].StringValue, Is.EqualTo("COB LED"));
            Assert.That(result.PropertySets[0].Entries["Geolocation_X"].DoubleValue, Is.EqualTo(190000d));
            var objectByElement = result.GetElementToObjectDict();
            int rootObjectIndex = (int)objectByElement[0];
            int childObjectIndex = (int)objectByElement[1];
            Assert.That(result.Objects[rootObjectIndex].PropertySetIndex, Is.EqualTo(0));
            Assert.That(result.Objects[childObjectIndex].PropertySetIndex, Is.EqualTo(0));
            Assert.That(result.Objects[rootObjectIndex].PropertySetIndex,
                Is.EqualTo(result.Objects[childObjectIndex].PropertySetIndex));
        }

        /// <summary>Verifies dangling root or child references fail during reading.</summary>
        [Test]
        public void ReadGimJsonV1_3_OutOfRangeRootOrChildLinkThrows()
        {
            JObject rootCorrupt = JObject.Parse(CreatePayload());
            rootCorrupt["PropertySetIndex"] = 1;
            Assert.Throws<ArgumentOutOfRangeException>(() => JsonManager.ReadGimJson(rootCorrupt.ToString()));

            JObject childCorrupt = JObject.Parse(CreatePayload());
            childCorrupt["Children"][0]["PropertySetIndex"] = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => JsonManager.ReadGimJson(childCorrupt.ToString()));

            JObject oversizedCorrupt = JObject.Parse(CreatePayload());
            oversizedCorrupt["PropertySetIndex"] = 99999999999;
            Assert.Throws<ArgumentOutOfRangeException>(() => JsonManager.ReadGimJson(oversizedCorrupt.ToString()));
        }

        /// <summary>Verifies wrapper and unsupported primitive property values fail during reading.</summary>
        [Test]
        public void ReadGimJsonV1_3_UnknownOrMismatchedPropertyKindThrows()
        {
            JObject unknownKind = JObject.Parse(CreatePayload());
            unknownKind["PropertySets"][0]["LED_TYPE"] = new JObject
            {
                ["kind"] = "integer",
                ["value"] = "COB LED"
            };
            Assert.Throws<JsonReaderException>(() => JsonManager.ReadGimJson(unknownKind.ToString()));

            JObject mismatchedKind = JObject.Parse(CreatePayload());
            mismatchedKind["PropertySets"][0]["LED_TYPE"] = true;
            Assert.Throws<JsonReaderException>(() => JsonManager.ReadGimJson(mismatchedKind.ToString()));
        }

        /// <summary>Verifies absent property data preserves legacy geometry and name behavior.</summary>
        [Test]
        public void ReadGimJsonV1_3_MissingPropertyKeysRemainLegacyTolerant()
        {
            JObject payload = JObject.Parse(CreatePayload());
            payload.Remove("PropertySets");
            payload.Remove("PropertySetIndex");
            foreach (JObject child in (JArray)payload["Children"])
                child.Remove("PropertySetIndex");

            JsonGimObject json = JsonManager.ReadGimJson(payload.ToString());
            GimData result = CreateData(false);
            var childObjects = GIMtransfer.FileAccess.GetUniqueChildObjects(result)
                .Select(objectIndex => result.Objects[objectIndex]).ToList();
            GIMtransfer.FileAccess.RestoreJsonPropertySetsAndLinks(json, result, childObjects);

            Assert.That(json.Version, Is.EqualTo("1.3.0"));
            Assert.That(json.GIMbin, Is.Not.Null);
            Assert.That(json.Name, Is.EqualTo("LED"));
            Assert.That(json.Children[0].Name, Is.EqualTo("LED"));
            Assert.That(json.PropertySets, Is.Empty);
            Assert.That(json.PropertySetIndex, Is.Null);
            Assert.That(json.Children[0].PropertySetIndex, Is.Null);
            Assert.That(result.PropertySets, Is.Empty);
            var objectByElement = result.GetElementToObjectDict();
            Assert.That(result.Objects[(int)objectByElement[0]].PropertySetIndex, Is.Null);
            Assert.That(result.Objects[(int)objectByElement[1]].PropertySetIndex, Is.Null);
        }

        /// <summary>Verifies older reader ignores unknown property keys and reads names.</summary>
        [Test]
        public void JsonReaderV1_2_IgnoresUnknownV1_3PropertyKeysAndReadsNames()
        {
            JsonGimObject result = new JsonReaderV1_2().ReadJson(CreatePayload());

            Assert.That(result.Children, Has.Count.EqualTo(1));
            Assert.That(result.GIMbin, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("LED"));
            Assert.That(result.Children[0].Name, Is.EqualTo("LED"));
            Assert.That(result.PropertySets, Is.Empty);
        }

        /// <summary>Verifies property value with ISO date string reads back as string kind and does not throw.</summary>
        [Test]
        public void ReadGimJsonV1_3_DateLikeStringPropertyReadsAsStringKind()
        {
            string payload = CreatePayload();
            JObject jsonObject = JObject.Parse(payload);
            jsonObject["PropertySets"][0]["ManufacturedDate"] = "2024-01-15T00:00:00Z";

            JsonGimObject result = null;
            Assert.DoesNotThrow(() =>
            {
                result = JsonManager.ReadGimJson(jsonObject.ToString());
            });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PropertySets, Has.Count.EqualTo(1));
            Assert.That(result.PropertySets[0]["ManufacturedDate"].Kind, Is.EqualTo("string"));
            Assert.That(result.PropertySets[0]["ManufacturedDate"].Value, Is.EqualTo("2024-01-15T00:00:00Z"));
        }

        /// <summary>Verifies JSON property sets replace binary sets on read rather than appending a duplicate.</summary>
        [Test]
        public void GimJsonToGimData_ReplacesBinaryPropertySetsWithJsonSets()
        {
            GimData binarySource = new();
            GimPropertySet binarySet = new();
            binarySet.AddString("name", "BinarySet");
            binarySet.AddString("origin", "from_binary");
            binarySource.PropertySets.Add(binarySet);

            GimElement root = new() { ElementType = GimElementType.D_root, Elements = new List<uint> { 1 } };
            GimElement child = new()
            {
                Orientation = true,
                ElementType = GimElementType.F_cilinder,
                ElementDoubles = new double[] { 4.25 },
                Locals = new List<GimLocal>
                {
                    new()
                    {
                        Parent = 0,
                        RightHanded = true,
                        Xx = 1, Xy = 0, Xz = 0,
                        Yx = 0, Yy = 1, Yz = 0,
                        Zx = 0, Zy = 0, Zz = 1,
                        Px = 0, Py = 0, Pz = 100
                    }
                }
            };
            binarySource.Elements.Add(root);
            binarySource.Elements.Add(child);
            binarySource.Objects.Add(new GimObject { Element = 0, Name = "root", PropertySetIndex = 0 });
            binarySource.Objects.Add(new GimObject { Element = 1, Name = "child" });

            GimWriter writer = new();
            uint[] binaryWords = writer.GimDataToBytes(binarySource);

            JsonGimObject json = new(binaryWords, "root");

            JsonGimPropertySet jsonSet = new();
            jsonSet["name"] = JsonGimPropertyValue.FromWireString("JsonSet");
            jsonSet["origin"] = JsonGimPropertyValue.FromWireString("from_json");
            json.PropertySets = new System.Collections.Generic.List<JsonGimPropertySet> { jsonSet };
            json.PropertySetIndex = 0;

            GimData restored = FileAccess.GimJsonToGimData(json);

            Assert.That(restored.PropertySets, Has.Count.EqualTo(1));
            var objDict = restored.GetElementToObjectDict();
            int rootObjIndex = (int)objDict[0];
            Assert.That(restored.Objects[rootObjIndex].PropertySetIndex, Is.EqualTo(0));
            GimPropertySet activeSet = restored.PropertySets[restored.Objects[rootObjIndex].PropertySetIndex.Value];
            Assert.That(activeSet.Entries["name"].StringValue, Is.EqualTo("JsonSet"));
            Assert.That(activeSet.Entries["origin"].StringValue, Is.EqualTo("from_json"));
        }
    }
}
