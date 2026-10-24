using System;
using System.Collections.Generic;
using System.Text.Json;
using GimBin;
using GIMcompress;
using GIMtransfer;
using NUnit.Framework;

namespace TestPlatform
{
    /// <summary>Checks JSON property links, names, and version output.</summary>
    [TestFixture]
    public sealed class GetJsonGimDataPropertyTests
    {
        /// <summary>Verifies root property-set name takes precedence over legacy name.</summary>
        [Test]
        public void GetJsonGimData_EmitsPropertySetsAndLink_WithSetNamePrecedence()
        {
            GimPropertySet propertySet = new();
            propertySet.AddString("name", "Global display name");

            GimData gimData = new();
            gimData.PropertySets.Add(propertySet);
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root });
            gimData.Objects.Add(new GimObject
            {
                element = 0,
                name = "Legacy root name",
                propertySetIndex = 0
            });

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);

            Assert.That(json.Version, Is.EqualTo("1.3.0"));
            Assert.That(json.Name, Is.EqualTo("Global display name"));
            Assert.That(json.PropertySetIndex, Is.EqualTo(0));
            Assert.That(JsonSerializer.Serialize(json), Does.Contain("\"PropertySetIndex\":0"));
        }

        /// <summary>Verifies property-less output omits property keys but uses current schema version.</summary>
        [Test]
        // The DTO stays non-null for callers while its serializer omits empty property data.
        public void GetJsonGimData_PropertylessPayloadUsesV1_3WithoutPropertyKeys()
        {
            GimData gimData = new();
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root });
            gimData.Objects.Add(new GimObject { element = 0, name = "root" });

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);
            string payload = JsonSerializer.Serialize(json);

            Assert.That(json.Version, Is.EqualTo("1.3.0"));
            Assert.That(json.PropertySets, Is.Empty);
            Assert.That(json.PropertySetIndex, Is.Null);
            Assert.That(payload, Does.Not.Contain("PropertySets"));
            Assert.That(payload, Does.Not.Contain("PropertySetIndex"));
        }

        /// <summary>Verifies empty string property name takes precedence over legacy name.</summary>
        [Test]
        public void GetJsonGimData_EmptyStringRootPropertyNameWinsOverLegacyFallback()
        {
            GimPropertySet propertySet = new();
            propertySet.AddString("name", "");

            GimData gimData = new();
            gimData.PropertySets.Add(propertySet);
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root });
            gimData.Objects.Add(new GimObject
            {
                element = 0,
                name = "Legacy root name",
                propertySetIndex = 0
            });

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);

            Assert.That(json.Name, Is.EqualTo(""));
        }

        /// <summary>Verifies a missing root object falls back to the first object name.</summary>
        [Test]
        public void GetJsonGimData_FallsBackToFirstObjectNameWhenRootHasNoObject()
        {
            GimData gimData = new();
            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);
            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 0 });
            gimData.Elements.Add(root);
            gimData.Elements.Add(child);
            gimData.Objects.Add(new GimObject { element = 1, name = "Legacy child name" });

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);

            Assert.That(json.Name, Is.EqualTo("Legacy child name"));
        }

        /// <summary>Verifies invalid root property-set links fail before JSON output.</summary>
        [TestCase(-1)]
        [TestCase(0)]
        public void GetJsonGimData_ThrowsForOutOfRangeRootPropertySetIndex(int propertySetIndex)
        {
            GimData gimData = new();
            gimData.Elements.Add(new GimElement { ElementType = GimElementType.D_root });
            gimData.Objects.Add(new GimObject
            {
                element = 0,
                name = "root-object",
                propertySetIndex = propertySetIndex
            });

            Assert.Throws<ArgumentOutOfRangeException>(() => FileAccess.GetJsonGimData(gimData, 0));
        }

        /// <summary>Verifies invalid child property-set links fail before JSON output.</summary>
        [Test]
        public void GetJsonGimData_ThrowsForOutOfRangeChildPropertySetIndex()
        {
            GimData gimData = new();
            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);
            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 0 });
            gimData.Elements.Add(root);
            gimData.Elements.Add(child);
            gimData.Objects.Add(new GimObject { element = 0, name = "root-object" });
            gimData.Objects.Add(new GimObject { element = 1, name = "test-object", propertySetIndex = 0 });

            Assert.Throws<ArgumentOutOfRangeException>(() => FileAccess.GetJsonGimData(gimData, 0));
        }

        /// <summary>Verifies child links remain aligned when geometry-only children are filtered.</summary>
        [Test]
        public void GetJsonGimData_EmitsPropertyLinkForChildBesideGeometryFilteredChild()
        {
            GimPropertySet filteredChildSet = new();
            filteredChildSet.AddString("name", "filtered child");
            GimPropertySet emittedChildSet = new();
            emittedChildSet.AddString("name", "emitted child");

            GimData gimData = new();
            gimData.PropertySets.Add(filteredChildSet);
            gimData.PropertySets.Add(emittedChildSet);

            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);
            root.Elements.Add(3);
            GimElement geometryFilteredPart = new() { ElementType = GimElementType.D_part };
            geometryFilteredPart.Elements.Add(2);
            geometryFilteredPart.Locals.Add(new GimLocal { Parent = 0 });
            GimElement emptyBrep = new() { ElementType = GimElementType.R_BRep };
            emptyBrep.Locals.Add(new GimLocal { Parent = 1 });
            GimElement emittedChild = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0 }
            };
            emittedChild.Locals.Add(new GimLocal { Parent = 0 });
            gimData.Elements.Add(root);
            gimData.Elements.Add(geometryFilteredPart);
            gimData.Elements.Add(emptyBrep);
            gimData.Elements.Add(emittedChild);
            gimData.Objects.Add(new GimObject { element = 1, propertySetIndex = 0 });
            gimData.Objects.Add(new GimObject { element = 3, propertySetIndex = 1 });

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);

            Assert.That(json.Children, Has.Count.EqualTo(1));
            Assert.That(json.Children[0].Name, Is.EqualTo("emitted child"));
            Assert.That(json.Children[0].PropertySetIndex, Is.EqualTo(0));
        }

        /// <summary>Verifies subtree export with non-zero common parent aligns JSON and binary set indexes and types.</summary>
        [Test]
        public void GetJsonGimData_SubtreeExportWithNonZeroCommonParent_MatchesBinaryRoundTripSet()
        {
            GimData gimData = new();
            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);

            GimElement folder = new() { ElementType = GimElementType.D_folder };
            folder.Locals.Add(new GimLocal { Parent = 0 });
            folder.Elements.Add(2);

            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0, 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 1 });

            gimData.Elements.Add(root);
            gimData.Elements.Add(folder);
            gimData.Elements.Add(child);

            GimPropertySet rootUnusedSet = new();
            rootUnusedSet.AddString("name", "RootUnused");

            GimPropertySet subtreeSet = new();
            subtreeSet.AddString("name", "SubtreeLED");
            subtreeSet.AddString("quoted_string", "model-42");
            subtreeSet.AddDouble("bare_number", 12.34);

            gimData.PropertySets.Add(rootUnusedSet);
            gimData.PropertySets.Add(subtreeSet);

            gimData.Objects.Add(new GimObject { element = 0, name = "root", propertySetIndex = 0 });
            gimData.Objects.Add(new GimObject { element = 2, name = "child", propertySetIndex = 1 });

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);

            Assert.That(json.PropertySets, Has.Count.EqualTo(1));
            Assert.That(json.Children, Has.Count.EqualTo(1));
            Assert.That(json.Children[0].PropertySetIndex, Is.EqualTo(0));
            Assert.That(json.PropertySets[0]["quoted_string"].Kind, Is.EqualTo("string"));
            Assert.That(json.PropertySets[0]["quoted_string"].Value, Is.EqualTo("model-42"));
            Assert.That(json.PropertySets[0]["bare_number"].Kind, Is.EqualTo("double"));
            Assert.That(json.PropertySets[0]["bare_number"].Value, Is.EqualTo(12.34d));

            GimReader reader = new();
            GimData binaryData = reader.BytesToGimData(json.GIMbin);

            Assert.That(binaryData.PropertySets, Has.Count.EqualTo(1));
            int binaryObjIndex = binaryData.Objects.FindIndex(o => o.PropertySetIndex.HasValue);
            Assert.That(binaryObjIndex, Is.GreaterThanOrEqualTo(0));
            int binarySetIndex = binaryData.Objects[binaryObjIndex].PropertySetIndex.Value;
            Assert.That(binarySetIndex, Is.EqualTo(json.Children[0].PropertySetIndex.Value));

            GimPropertySet roundTripSet = binaryData.PropertySets[binarySetIndex];
            Assert.That(roundTripSet.Entries["name"].StringValue, Is.EqualTo("SubtreeLED"));
            Assert.That(roundTripSet.Entries["quoted_string"].Kind, Is.EqualTo(GimPropertyKind.String));
            Assert.That(roundTripSet.Entries["quoted_string"].StringValue, Is.EqualTo("model-42"));
            Assert.That(roundTripSet.Entries["bare_number"].Kind, Is.EqualTo(GimPropertyKind.Double));
            Assert.That(roundTripSet.Entries["bare_number"].DoubleValue, Is.EqualTo(12.34d));
        }

        /// <summary>Verifies subtree export assigns folder set to JSON and binary root links.</summary>
        [Test]
        public void GetJsonGimData_SubtreeExportWithFolderCommonParent_RootLinkIsFolderSetNotDocumentRootSet()
        {
            GimData gimData = new();
            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);

            GimElement folder = new() { ElementType = GimElementType.D_folder };
            folder.Locals.Add(new GimLocal { Parent = 0 });
            folder.Elements.Add(2);

            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0, 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 1 });

            gimData.Elements.Add(root);
            gimData.Elements.Add(folder);
            gimData.Elements.Add(child);

            GimPropertySet rootSet = new();
            rootSet.AddString("name", "DocumentRootSet");

            GimPropertySet folderSet = new();
            folderSet.AddString("name", "FolderSet");
            folderSet.AddDouble("folder_prop", 10.5);

            GimPropertySet childSet = new();
            childSet.AddString("name", "ChildSet");
            childSet.AddString("child_sku", "SKU-999");

            gimData.PropertySets.Add(rootSet);
            gimData.PropertySets.Add(folderSet);
            gimData.PropertySets.Add(childSet);

            gimData.Objects.Add(new GimObject { element = 0, name = "root", propertySetIndex = 0 });
            gimData.Objects.Add(new GimObject { element = 1, name = "folder", propertySetIndex = 1 });
            gimData.Objects.Add(new GimObject { element = 2, name = "child", propertySetIndex = 2 });

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);

            Assert.That(json.PropertySetIndex, Is.Not.Null);
            Assert.That(json.Children, Has.Count.EqualTo(1));
            Assert.That(json.Children[0].PropertySetIndex, Is.Not.Null);

            GimReader reader = new();
            GimData binaryData = reader.BytesToGimData(json.GIMbin);

            int binaryRootObjIndex = binaryData.Objects.FindIndex(o => o.element == 0);
            Assert.That(binaryRootObjIndex, Is.GreaterThanOrEqualTo(0));
            int? binaryRootSetIndex = binaryData.Objects[binaryRootObjIndex].PropertySetIndex;
            Assert.That(binaryRootSetIndex, Is.Not.Null);
            Assert.That(json.PropertySetIndex, Is.EqualTo(binaryRootSetIndex));

            GimPropertySet roundTripRootSet = binaryData.PropertySets[binaryRootSetIndex.Value];
            Assert.That(roundTripRootSet.Entries["name"].StringValue, Is.EqualTo("FolderSet"));
            Assert.That(roundTripRootSet.Entries["name"].StringValue, Is.Not.EqualTo("DocumentRootSet"));
            Assert.That(roundTripRootSet.Entries["folder_prop"].DoubleValue, Is.EqualTo(10.5d));

            int binaryChildObjIndex = binaryData.Objects.FindIndex(o => o.element != 0 && o.PropertySetIndex.HasValue);
            Assert.That(binaryChildObjIndex, Is.GreaterThanOrEqualTo(0));
            int? binaryChildSetIndex = binaryData.Objects[binaryChildObjIndex].PropertySetIndex;
            Assert.That(binaryChildSetIndex, Is.Not.Null);
            Assert.That(json.Children[0].PropertySetIndex, Is.EqualTo(binaryChildSetIndex));

            GimPropertySet roundTripChildSet = binaryData.PropertySets[binaryChildSetIndex.Value];
            Assert.That(roundTripChildSet.Entries["name"].StringValue, Is.EqualTo("ChildSet"));
            Assert.That(roundTripChildSet.Entries["child_sku"].StringValue, Is.EqualTo("SKU-999"));
        }

        /// <summary>Verifies subtree export with pre-existing GimBin writes fresh binary rather than reusing blob.</summary>
        [Test]
        public void GetJsonGimData_SubtreeExportWithPreExistingGimBin_WritesFreshBinaryAndRoundTripsFolderSet()
        {
            GimData gimData = new();
            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);

            GimElement folder = new() { ElementType = GimElementType.D_folder };
            folder.Locals.Add(new GimLocal { Parent = 0 });
            folder.Elements.Add(2);

            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0, 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 1 });

            gimData.Elements.Add(root);
            gimData.Elements.Add(folder);
            gimData.Elements.Add(child);

            GimPropertySet rootSet = new();
            rootSet.AddString("name", "DocumentRootSet");

            GimPropertySet folderSet = new();
            folderSet.AddString("name", "FolderSet");
            folderSet.AddDouble("folder_prop", 10.5);

            GimPropertySet childSet = new();
            childSet.AddString("name", "ChildSet");

            gimData.PropertySets.Add(rootSet);
            gimData.PropertySets.Add(folderSet);
            gimData.PropertySets.Add(childSet);

            gimData.Objects.Add(new GimObject { element = 0, name = "root", propertySetIndex = 0 });
            gimData.Objects.Add(new GimObject { element = 1, name = "folder", propertySetIndex = 1 });
            gimData.Objects.Add(new GimObject { element = 2, name = "child", propertySetIndex = 2 });

            byte[] dummyBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            gimData.GimBin = dummyBytes;

            JsonGimObject json = FileAccess.GetJsonGimData(gimData, 0);

            Assert.That(json.GIMbin, Is.Not.EqualTo(dummyBytes));

            GimReader reader = new();
            GimData binaryData = reader.BytesToGimData(json.GIMbin);

            int binaryRootObjIndex = binaryData.Objects.FindIndex(o => o.element == 0);
            Assert.That(binaryRootObjIndex, Is.GreaterThanOrEqualTo(0));
            int? binaryRootSetIndex = binaryData.Objects[binaryRootObjIndex].PropertySetIndex;
            Assert.That(binaryRootSetIndex, Is.Not.Null);
            Assert.That(json.PropertySetIndex, Is.EqualTo(binaryRootSetIndex));

            GimPropertySet roundTripRootSet = binaryData.PropertySets[binaryRootSetIndex.Value];
            Assert.That(roundTripRootSet.Entries["name"].StringValue, Is.EqualTo("FolderSet"));
            Assert.That(roundTripRootSet.Entries["folder_prop"].DoubleValue, Is.EqualTo(10.5d));
        }

        /// <summary>Verifies subtree export survives repeated JSON-binary-JSON round trips with consistent property sets.</summary>
        [Test]
        public void GetJsonGimData_FolderSubtree_SurvivesJsonBinaryJsonRoundTrip()
        {
            GimData gimData = new();
            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);

            GimElement folder = new() { ElementType = GimElementType.D_folder };
            folder.Locals.Add(new GimLocal { Parent = 0 });
            folder.Elements.Add(2);

            GimElement child = new()
            {
                ElementType = GimElementType.ExternalReference,
                ElementInts = new[] { 0, 0 }
            };
            child.Locals.Add(new GimLocal { Parent = 1 });

            gimData.Elements.Add(root);
            gimData.Elements.Add(folder);
            gimData.Elements.Add(child);

            GimPropertySet rootSet = new();
            rootSet.AddString("name", "DocumentRootSet");

            GimPropertySet folderSet = new();
            folderSet.AddString("name", "FolderSet");
            folderSet.AddDouble("folder_prop", 10.5);

            GimPropertySet childSet = new();
            childSet.AddString("name", "ChildSet");
            childSet.AddString("child_sku", "SKU-999");

            gimData.PropertySets.Add(rootSet);
            gimData.PropertySets.Add(folderSet);
            gimData.PropertySets.Add(childSet);

            gimData.Objects.Add(new GimObject { element = 0, name = "root", propertySetIndex = 0 });
            gimData.Objects.Add(new GimObject { element = 1, name = "folder", propertySetIndex = 1 });
            gimData.Objects.Add(new GimObject { element = 2, name = "child", propertySetIndex = 2 });

            JsonGimObject firstJson = FileAccess.GetJsonGimData(gimData, 0);
            GimData restored = FileAccess.GimJsonToGimData(firstJson);
            JsonGimObject secondJson = FileAccess.GetJsonGimData(restored, 0);
            GimData restoredAgain = FileAccess.GimJsonToGimData(secondJson);

            int restoredFolderObjIndex = restored.Objects.FindIndex(o =>
                o.PropertySetIndex.HasValue &&
                o.PropertySetIndex.Value >= 0 &&
                o.PropertySetIndex.Value < restored.PropertySets.Count &&
                restored.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("name", out var n) &&
                n.StringValue == "FolderSet" &&
                restored.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("folder_prop", out var fp) &&
                fp.DoubleValue == 10.5d);
            Assert.That(restoredFolderObjIndex, Is.GreaterThanOrEqualTo(0));

            int restoredChildObjIndex = restored.Objects.FindIndex(o =>
                o.PropertySetIndex.HasValue &&
                o.PropertySetIndex.Value >= 0 &&
                o.PropertySetIndex.Value < restored.PropertySets.Count &&
                restored.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("name", out var n) &&
                n.StringValue == "ChildSet" &&
                restored.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("child_sku", out var cs) &&
                cs.StringValue == "SKU-999");
            Assert.That(restoredChildObjIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(restoredChildObjIndex, Is.Not.EqualTo(restoredFolderObjIndex));

            foreach (GimPropertySet propertySet in restored.PropertySets)
            {
                if (propertySet.Entries.TryGetValue("name", out GimPropertyValue nameVal))
                {
                    Assert.That(nameVal.StringValue, Is.Not.EqualTo("DocumentRootSet"));
                }
            }

            int restoredAgainFolderObjIndex = restoredAgain.Objects.FindIndex(o =>
                o.PropertySetIndex.HasValue &&
                o.PropertySetIndex.Value >= 0 &&
                o.PropertySetIndex.Value < restoredAgain.PropertySets.Count &&
                restoredAgain.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("name", out var n) &&
                n.StringValue == "FolderSet" &&
                restoredAgain.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("folder_prop", out var fp) &&
                fp.DoubleValue == 10.5d);
            Assert.That(restoredAgainFolderObjIndex, Is.GreaterThanOrEqualTo(0));

            int restoredAgainChildObjIndex = restoredAgain.Objects.FindIndex(o =>
                o.PropertySetIndex.HasValue &&
                o.PropertySetIndex.Value >= 0 &&
                o.PropertySetIndex.Value < restoredAgain.PropertySets.Count &&
                restoredAgain.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("name", out var n) &&
                n.StringValue == "ChildSet" &&
                restoredAgain.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("child_sku", out var cs) &&
                cs.StringValue == "SKU-999");
            Assert.That(restoredAgainChildObjIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(restoredAgainChildObjIndex, Is.Not.EqualTo(restoredAgainFolderObjIndex));

            foreach (GimPropertySet propertySet in restoredAgain.PropertySets)
            {
                if (propertySet.Entries.TryGetValue("name", out GimPropertyValue nameVal))
                {
                    Assert.That(nameVal.StringValue, Is.Not.EqualTo("DocumentRootSet"));
                }
            }
        }

        /// <summary>Verifies part directly under root survives repeated JSON-binary-JSON round trips.</summary>
        [Test]
        public void GetJsonGimData_PartDirectlyUnderRoot_SurvivesJsonBinaryJsonRoundTrip()
        {
            GimData gimData = new();
            GimElement root = new() { ElementType = GimElementType.D_root };
            root.Elements.Add(1);

            GimElement part = new() { ElementType = GimElementType.D_part };
            part.Locals.Add(new GimLocal { Parent = 0 });
            part.Elements.Add(2);
            part.Elements.Add(3);

            GimElement brep = new() { ElementType = GimElementType.R_BRep };
            brep.Locals.Add(new GimLocal { Parent = 1 });
            brep.Elements.Add(4);

            GimElement dropped = new() { ElementType = GimElementType.D_part };
            dropped.Locals.Add(new GimLocal { Parent = 1 });

            GimElement solid = new() { ElementType = GimElementType.B_solid };
            solid.Locals.Add(new GimLocal { Parent = 2 });

            gimData.Elements.Add(root);    // 0
            gimData.Elements.Add(part);    // 1
            gimData.Elements.Add(brep);    // 2
            gimData.Elements.Add(dropped); // 3
            gimData.Elements.Add(solid);   // 4

            GimPropertySet rootSet = new();
            rootSet.AddString("name", "DocumentRootSet");

            GimPropertySet partSet = new();
            partSet.AddString("name", "PartSet");
            partSet.AddString("part_sku", "SKU-1");

            gimData.PropertySets.Add(rootSet);
            gimData.PropertySets.Add(partSet);

            gimData.Objects.Add(new GimObject { element = 0, name = "root", propertySetIndex = 0 });
            gimData.Objects.Add(new GimObject { element = 1, name = "part", propertySetIndex = 1 });
            gimData.Objects.Add(new GimObject { element = 3, name = "dropped" });

            JsonGimObject firstJson = FileAccess.GetJsonGimData(gimData, 0);
            GimData restored = FileAccess.GimJsonToGimData(firstJson);
            JsonGimObject secondJson = FileAccess.GetJsonGimData(restored, 0);
            GimData restoredAgain = FileAccess.GimJsonToGimData(secondJson);

            int restoredPartObjIndex = restored.Objects.FindIndex(o =>
                o.PropertySetIndex.HasValue &&
                o.PropertySetIndex.Value >= 0 &&
                o.PropertySetIndex.Value < restored.PropertySets.Count &&
                restored.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("name", out var n) &&
                n.StringValue == "PartSet" &&
                restored.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("part_sku", out var ps) &&
                ps.StringValue == "SKU-1");
            Assert.That(restoredPartObjIndex, Is.GreaterThanOrEqualTo(0));

            foreach (GimPropertySet propertySet in restored.PropertySets)
            {
                if (propertySet.Entries.TryGetValue("name", out GimPropertyValue nameVal))
                {
                    Assert.That(nameVal.StringValue, Is.Not.EqualTo("DocumentRootSet"));
                }
            }

            int restoredAgainPartObjIndex = restoredAgain.Objects.FindIndex(o =>
                o.PropertySetIndex.HasValue &&
                o.PropertySetIndex.Value >= 0 &&
                o.PropertySetIndex.Value < restoredAgain.PropertySets.Count &&
                restoredAgain.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("name", out var n) &&
                n.StringValue == "PartSet" &&
                restoredAgain.PropertySets[o.PropertySetIndex.Value].Entries.TryGetValue("part_sku", out var ps) &&
                ps.StringValue == "SKU-1");
            Assert.That(restoredAgainPartObjIndex, Is.GreaterThanOrEqualTo(0));

            foreach (GimPropertySet propertySet in restoredAgain.PropertySets)
            {
                if (propertySet.Entries.TryGetValue("name", out GimPropertyValue nameVal))
                {
                    Assert.That(nameVal.StringValue, Is.Not.EqualTo("DocumentRootSet"));
                }
            }
        }
    }
}
