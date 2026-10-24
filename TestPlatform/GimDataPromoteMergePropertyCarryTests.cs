using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GIMcompress;
using GIMtransfer;
using NUnit.Framework;

namespace TestPlatform
{
    /// <summary>
    /// Verifies property set carry-through behavior during promotion, separation,
    /// and subtree removal operations in FileManagerPrivate and GimData.
    /// </summary>
    [TestFixture]
    public sealed class GimDataPromoteMergePropertyCarryTests
    {
        /// <summary>
        /// Verifies FileAccess.GetGimElementAsGimData carries referenced property sets,
        /// maintains shared set identity, omits orphan sets, and preserves source isolation.
        /// </summary>
        [Test]
        public void FileManagerPrivate_GetGimElementAsGimData_CarriesReferencedPropertySets_AndOmitsOrphans()
        {
            GimData source = new();

            GimElement root = new()
            {
                ElementType = GimElementType.D_root,
                Elements = new List<uint> { 1, 4 }
            };

            GimElement folder = new()
            {
                ElementType = GimElementType.D_folder,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint> { 2, 3 }
            };

            GimElement part1 = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            GimElement part2 = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            GimElement part3 = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            };

            source.Elements.Add(root);
            source.Elements.Add(folder);
            source.Elements.Add(part1);
            source.Elements.Add(part2);
            source.Elements.Add(part3);

            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SharedLED")),
                ("type", GimPropertyValue.FromString("diode"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("OrphanSet"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("OutsideSet"))));

            source.Objects.Add(GimObjectFactory.Create(0, "RootObj"));
            source.Objects.Add(GimObjectFactory.Create(1, "FolderObj"));
            source.Objects.Add(GimObjectFactory.Create(2, "LED1", 0));
            source.Objects.Add(GimObjectFactory.Create(3, "LED2", 0));
            source.Objects.Add(GimObjectFactory.Create(4, "OutsidePart", 2));

            GimData subtree = FileAccess.GetGimElementAsGimData(source, 1);

            Assert.That(subtree.PropertySets, Has.Count.EqualTo(1));
            Assert.That(subtree.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("SharedLED"));

            GimObject carriedPart1 = subtree.Objects.Find(o => o.Name == "LED1");
            GimObject carriedPart2 = subtree.Objects.Find(o => o.Name == "LED2");
            Assert.That(carriedPart1, Is.Not.Null);
            Assert.That(carriedPart2, Is.Not.Null);
            Assert.That(carriedPart1.PropertySetIndex, Is.EqualTo(0));
            Assert.That(carriedPart2.PropertySetIndex, Is.EqualTo(0));

            // Mutating target set does not affect source
            subtree.PropertySets[0].Entries["name"] = GimPropertyValue.FromString("Mutated");
            Assert.That(source.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("SharedLED"));
        }

        /// <summary>
        /// Verifies FileAccess.GetGimElementAsGimData throws ArgumentOutOfRangeException
        /// when an object in the subtree references an out-of-range property set.
        /// </summary>
        [Test]
        public void FileManagerPrivate_GetGimElementAsGimData_ThrowsArgumentOutOfRangeException_WhenObjectReferencesCorruptIndex()
        {
            GimData source = new();
            source.Elements.Add(new GimElement { ElementType = GimElementType.D_root, Elements = new List<uint> { 1 } });
            source.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            });
            source.PropertySets.Add(GimPropertySet.Create(("name", GimPropertyValue.FromString("Valid"))));
            source.Objects.Add(GimObjectFactory.Create(1, "BadPart", 50));

            Assert.Throws<ArgumentOutOfRangeException>(() => FileAccess.GetGimElementAsGimData(source, 1));
        }

        /// <summary>
        /// Verifies FileAccess.PromoteJsonChildren carries referenced property sets to the extracted
        /// external GimData, and surviving objects in the original GimData maintain valid PropertySetIndex links.
        /// </summary>
        [Test]
        public void FileManagerPrivate_PromoteJsonChildren_CarriesReferencedPropertySetsToExtractedGimData_AndSurvivingLinksIntact()
        {
            GimData source = new();

            GimElement root = new()
            {
                ElementType = GimElementType.D_root,
                Elements = new List<uint> { 1, 3 }
            };

            GimElement folder = new()
            {
                ElementType = GimElementType.D_folder,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint> { 2 }
            };

            GimElement partToPromote = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            GimElement survivingPart = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            };

            source.Elements.Add(root);
            source.Elements.Add(folder);
            source.Elements.Add(partToPromote);
            source.Elements.Add(survivingPart);

            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("FirstUnrelatedProp"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SurvivingProp")),
                ("type", GimPropertyValue.FromString("sensor"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("PromotedProp")),
                ("type", GimPropertyValue.FromString("motor"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("OrphanProp"))));

            source.Objects.Add(GimObjectFactory.Create(0, "RootObject"));
            source.Objects.Add(GimObjectFactory.Create(1, "FolderObject"));
            source.Objects.Add(GimObjectFactory.Create(2, "MotorToPromote", 2));
            source.Objects.Add(GimObjectFactory.Create(3, "SensorSurviving", 1));

            List<uint> objectList = new() { 2 };
            Tuple<int, int>[] objectAndProjectIDs = new[] { Tuple.Create(10, 20) };

            GimData[] promotedDatas = FileAccess.PromoteJsonChildren(source, objectList, objectAndProjectIDs);

            Assert.That(promotedDatas, Has.Length.EqualTo(1));
            GimData externalData = promotedDatas[0];

            Assert.That(externalData.PropertySets, Has.Count.EqualTo(1));
            Assert.That(externalData.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("PromotedProp"));

            GimObject promotedObj = externalData.Objects.Find(o => o.Name == "MotorToPromote");
            Assert.That(promotedObj, Is.Not.Null);
            // Source index was 2; destination set list is shorter (length 1) and remapped index must be 0
            Assert.That(promotedObj.PropertySetIndex, Is.EqualTo(0));

            // Surviving object in original model still points to its valid set index 1
            GimObject survivingObj = source.Objects.Find(o => o.Name == "SensorSurviving");
            Assert.That(survivingObj, Is.Not.Null);
            Assert.That(survivingObj.PropertySetIndex, Is.EqualTo(1));
            Assert.That(source.PropertySets[1].Entries["name"].StringValue, Is.EqualTo("SurvivingProp"));
        }

        /// <summary>
        /// Verifies FileAccess.SeperateElementFromGimData carries referenced property sets to the
        /// new separated GimData, preserves shared set relationships, and omits unreferenced orphan sets.
        /// </summary>
        [Test]
        public void FileManagerPrivate_SeperateElementFromGimData_CarriesReferencedPropertySets_PreservesSharedSets_AndOmitsOrphans()
        {
            GimData source = new();

            GimElement root = new()
            {
                ElementType = GimElementType.D_root,
                Elements = new List<uint> { 1, 4 }
            };

            GimElement parentPart = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint> { 2, 3 }
            };

            GimElement childPart1 = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            GimElement childPart2 = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            GimElement outsidePart = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            };

            source.Elements.Add(root);
            source.Elements.Add(parentPart);
            source.Elements.Add(childPart1);
            source.Elements.Add(childPart2);
            source.Elements.Add(outsidePart);

            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("UnrelatedRootOrphan"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SharedValve")),
                ("pressure", GimPropertyValue.FromDouble(10.5))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SecondCarriedProp")),
                ("type", GimPropertyValue.FromString("valve_actuator"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("OutsideProp"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("OrphanProp"))));

            source.Objects.Add(GimObjectFactory.Create(0, "RootObject", 0));
            source.Objects.Add(GimObjectFactory.Create(1, "ValveParent", 1));
            source.Objects.Add(GimObjectFactory.Create(2, "ValveChild1", 1));
            source.Objects.Add(GimObjectFactory.Create(3, "ValveChild2", 2));
            source.Objects.Add(GimObjectFactory.Create(4, "OutsideObject", 3));

            (GimData remainingSource, GimData separatedData) = FileAccess.SeperateElementFromGimData(source, 1, 0);

            Assert.That(separatedData.PropertySets, Has.Count.EqualTo(2));
            Assert.That(separatedData.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("SharedValve"));
            Assert.That(separatedData.PropertySets[1].Entries["name"].StringValue, Is.EqualTo("SecondCarriedProp"));

            GimObject sepParent = separatedData.Objects.Find(o => o.Name == "ValveParent");
            GimObject sepChild1 = separatedData.Objects.Find(o => o.Name == "ValveChild1");
            GimObject sepChild2 = separatedData.Objects.Find(o => o.Name == "ValveChild2");
            Assert.That(sepParent, Is.Not.Null);
            Assert.That(sepChild1, Is.Not.Null);
            Assert.That(sepChild2, Is.Not.Null);

            // Source set 1 remapped to destination set 0 (shared between parent and child 1)
            Assert.That(sepParent.PropertySetIndex, Is.EqualTo(0));
            Assert.That(sepChild1.PropertySetIndex, Is.EqualTo(0));
            Assert.That(sepParent.PropertySetIndex, Is.EqualTo(sepChild1.PropertySetIndex));

            // Source set 2 remapped to destination set 1 (proves destination index is remapped and not left as old number 2)
            Assert.That(sepChild2.PropertySetIndex, Is.EqualTo(1));

            // Remaining source still retains its valid property set reference for the outside object
            GimObject survivingOutside = remainingSource.Objects.Find(o => o.Name == "OutsideObject");
            Assert.That(survivingOutside, Is.Not.Null);
            Assert.That(survivingOutside.PropertySetIndex, Is.EqualTo(3));
            Assert.That(remainingSource.PropertySets[3].Entries["name"].StringValue, Is.EqualTo("OutsideProp"));
        }

        /// <summary>
        /// Verifies FileAccess.SeperateElementFromGimData throws ArgumentOutOfRangeException
        /// when an object being separated references an out-of-range property set index,
        /// and guarantees the source graph is not cut before throwing.
        /// </summary>
        [Test]
        public void FileManagerPrivate_SeperateElementFromGimData_ThrowsArgumentOutOfRangeException_WhenCarriedObjectReferencesCorruptIndex()
        {
            GimData source = new();
            source.Elements.Add(new GimElement { ElementType = GimElementType.D_root, Elements = new List<uint> { 1 } });
            source.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            });
            source.PropertySets.Add(GimPropertySet.Create(("name", GimPropertyValue.FromString("Valid"))));
            source.Objects.Add(GimObjectFactory.Create(1, "CorruptPart", -5));

            Assert.Throws<ArgumentOutOfRangeException>(() => FileAccess.SeperateElementFromGimData(source, 1, 0));

            // Verify source graph was not mutated before the throw (parent still lists element)
            Assert.That(source.Elements[0].Elements, Contains.Item(1u));
            Assert.That(source.Objects.Find(o => o.Name == "CorruptPart"), Is.Not.Null);
        }

        /// <summary>
        /// Verifies FileAccess.SeperateElementFromGimData throws ArgumentOutOfRangeException before cutting
        /// the source graph when a descendant in the separated subtree references a corrupt property set index.
        /// </summary>
        [Test]
        public void FileManagerPrivate_SeperateElementFromGimData_ThrowsArgumentOutOfRangeException_WhenSubtreeChildReferencesCorruptIndex_AndLeavesSourceUnchanged()
        {
            GimData source = new();
            source.Elements.Add(new GimElement { ElementType = GimElementType.D_root, Elements = new List<uint> { 1 } });
            source.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint> { 2 }
            });
            source.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            });
            source.PropertySets.Add(GimPropertySet.Create(("name", GimPropertyValue.FromString("Valid"))));
            source.Objects.Add(GimObjectFactory.Create(1, "ValidPart", 0));
            source.Objects.Add(GimObjectFactory.Create(2, "CorruptChildPart", 99));

            Assert.Throws<ArgumentOutOfRangeException>(() => FileAccess.SeperateElementFromGimData(source, 1, 0));

            // Verify source graph was not cut before the throw (root still has element 1, element 1 still has element 2)
            Assert.That(source.Elements[0].Elements, Contains.Item(1u));
            Assert.That(source.Elements[1].Elements, Contains.Item(2u));
            Assert.That(source.Objects.Find(o => o.Name == "ValidPart"), Is.Not.Null);
            Assert.That(source.Objects.Find(o => o.Name == "CorruptChildPart"), Is.Not.Null);
        }

        /// <summary>
        /// Verifies RemoveElementSubTree removes objects from Objects without shifting or renumbering
        /// surviving objects' PropertySetIndex values, and allows orphan property sets to remain.
        /// </summary>
        [Test]
        public void RemoveElementSubTree_DoesNotChangeSurvivingObjectsPropertySetIndexValues()
        {
            GimData source = new();

            GimElement root = new()
            {
                ElementType = GimElementType.D_root,
                Elements = new List<uint> { 1, 3 }
            };

            GimElement folderToRemove = new()
            {
                ElementType = GimElementType.D_folder,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint> { 2 }
            };

            GimElement partInFolder = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            GimElement survivingPart = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            };

            source.Elements.Add(root);
            source.Elements.Add(folderToRemove);
            source.Elements.Add(partInFolder);
            source.Elements.Add(survivingPart);

            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("RootProp"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("FolderProp"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("PartInFolderProp"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SurvivingProp"))));

            source.Objects.Add(GimObjectFactory.Create(0, "RootObj", 0));
            source.Objects.Add(GimObjectFactory.Create(1, "FolderObj", 1));
            source.Objects.Add(GimObjectFactory.Create(2, "PartInFolderObj", 2));
            source.Objects.Add(GimObjectFactory.Create(3, "SurvivingObj", 3));

            // Remove folder and its child part
            source.RemoveElementSubTree(1);

            // Objects list was compacted: PartInFolderObj (element 2 in subtree) is removed
            Assert.That(source.Objects.Find(o => o.Name == "PartInFolderObj"), Is.Null);

            GimObject survivingRoot = source.Objects.Find(o => o.Name == "RootObj");
            GimObject survivingFolder = source.Objects.Find(o => o.Name == "FolderObj");
            GimObject survivingPartObj = source.Objects.Find(o => o.Name == "SurvivingObj");

            Assert.That(survivingRoot, Is.Not.Null);
            Assert.That(survivingFolder, Is.Not.Null);
            Assert.That(survivingPartObj, Is.Not.Null);

            // PropertySetIndex values on surviving objects must NOT have shifted,
            // even though SurvivingObj shifted position in Objects list.
            Assert.That(survivingRoot.PropertySetIndex, Is.EqualTo(0));
            Assert.That(survivingFolder.PropertySetIndex, Is.EqualTo(1));
            Assert.That(survivingPartObj.PropertySetIndex, Is.EqualTo(3));

            // PropertySets still contains all 4 sets (set 2 is now an unreferenced orphan, which is allowed)
            Assert.That(source.PropertySets, Has.Count.EqualTo(4));
            Assert.That(source.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("RootProp"));
            Assert.That(source.PropertySets[1].Entries["name"].StringValue, Is.EqualTo("FolderProp"));
            Assert.That(source.PropertySets[2].Entries["name"].StringValue, Is.EqualTo("PartInFolderProp"));
            Assert.That(source.PropertySets[3].Entries["name"].StringValue, Is.EqualTo("SurvivingProp"));
        }

        /// <summary>Verifies MergeJson copies the root object's property set onto the new folder object.</summary>
        [Test]
        public async Task FileManagerPrivate_MergeJson_KeepsRootObjectPropertySetOnNewFolderObject()
        {
            GimData promoteData = new();
            GimElement root = new()
            {
                ElementType = GimElementType.D_root,
                Elements = new List<uint> { 1 }
            };
            GimElement part = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            };
            promoteData.Elements.Add(root);
            promoteData.Elements.Add(part);

            GimPropertySet rootSet = GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SourceRootSet")),
                ("layer", GimPropertyValue.FromString("arch")));
            promoteData.PropertySets.Add(rootSet);

            promoteData.Objects.Add(GimObjectFactory.Create(0, "SourceRoot", 0));
            promoteData.Objects.Add(GimObjectFactory.Create(1, "SourcePart", null));

            JsonGimObject mergedJson = await FileAccess.MergeJson(new List<GimData> { promoteData }, new JsonGimObject());

            Assert.That(mergedJson.PropertySets, Has.Count.EqualTo(1));
            Assert.That(mergedJson.PropertySets[0]["name"].Value, Is.EqualTo("SourceRootSet"));
            Assert.That(mergedJson.PropertySets[0]["layer"].Value, Is.EqualTo("arch"));
            Assert.That(mergedJson.Children, Has.Count.EqualTo(1));
            Assert.That(mergedJson.Children[0].Name, Is.EqualTo("SourceRootSet"));
            Assert.That(mergedJson.Children[0].PropertySetIndex, Is.EqualTo(0));
        }
    }
}
