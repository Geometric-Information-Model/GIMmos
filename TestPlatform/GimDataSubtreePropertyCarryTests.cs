using System;
using System.Collections.Generic;
using GIMcompress;
using NUnit.Framework;

namespace TestPlatform
{
    /// <summary>
    /// Verifies property set carry-through behavior during subtree extraction,
    /// secondary GimData addition, and external reference demotion.
    /// </summary>
    [TestFixture]
    public sealed class GimDataSubtreePropertyCarryTests
    {
        /// <summary>
        /// Verifies GetGimElementAsGimData carries referenced property sets,
        /// maintains shared set identity, omits unreferenced orphan sets, and leaves source sets isolated.
        /// </summary>
        [Test]
        public void GetGimElementAsGimData_CarriesReferencedPropertySets_PreservesSharedSetIdentity_AndOmitsOrphanSets()
        {
            GimData source = new();

            // Root element
            GimElement root = new()
            {
                ElementType = GimElementType.D_root,
                Elements = new List<uint> { 1, 4 }
            };

            // Folder 1 under root
            GimElement folder = new()
            {
                ElementType = GimElementType.D_folder,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint> { 2, 3 }
            };

            // Part 1 under folder
            GimElement part1 = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            // Part 2 under folder
            GimElement part2 = new()
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            };

            // Part 3 outside folder under root
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

            // Property sets: 0 is shared by part1 and part2, 1 is orphan, 2 is used outside the subtree
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("LED")),
                ("type", GimPropertyValue.FromString("light"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("Orphan")),
                ("note", GimPropertyValue.FromString("should_not_travel"))));
            source.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("Outside")),
                ("type", GimPropertyValue.FromString("sensor"))));

            source.Objects.Add(GimObjectFactory.Create(0, "RootObject"));
            source.Objects.Add(GimObjectFactory.Create(1, "FolderObject"));
            source.Objects.Add(GimObjectFactory.Create(2, "LED1", 0));
            source.Objects.Add(GimObjectFactory.Create(3, "LED2", 0));
            source.Objects.Add(GimObjectFactory.Create(4, "OutsideSensor", 2));

            GimData subtree = source.GetGimElementAsGimData(source, 1);

            // Only set 0 is referenced in the subtree; orphan set 1 and outside set 2 must be absent
            Assert.That(subtree.PropertySets, Has.Count.EqualTo(1));
            Assert.That(subtree.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("LED"));
            Assert.That(subtree.PropertySets[0].Entries["type"].StringValue, Is.EqualTo("light"));

            // Carried objects for Part 1 and Part 2 must both reference the remapped set at index 0
            GimObject carriedPart1 = subtree.Objects.Find(o => o.Name == "LED1");
            GimObject carriedPart2 = subtree.Objects.Find(o => o.Name == "LED2");
            Assert.That(carriedPart1, Is.Not.Null);
            Assert.That(carriedPart2, Is.Not.Null);
            Assert.That(carriedPart1.PropertySetIndex, Is.EqualTo(0));
            Assert.That(carriedPart2.PropertySetIndex, Is.EqualTo(0));
            Assert.That(carriedPart1.PropertySetIndex, Is.EqualTo(carriedPart2.PropertySetIndex));

            // Folder object had null property set index, which must remain null
            GimObject carriedFolder = subtree.Objects.Find(o => o.Name == "FolderObject");
            Assert.That(carriedFolder, Is.Not.Null);
            Assert.That(carriedFolder.PropertySetIndex, Is.Null);

            // Mutating the carried property set must not mutate the source set
            subtree.PropertySets[0].Entries["name"] = GimPropertyValue.FromString("ModifiedLED");
            Assert.That(source.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("LED"));
        }

        /// <summary>
        /// Verifies GetGimElementAsGimData throws ArgumentOutOfRangeException when a carried object
        /// references an out-of-range property set index per ADR 0003.
        /// </summary>
        [Test]
        public void GetGimElementAsGimData_ThrowsArgumentOutOfRangeException_WhenCarriedObjectReferencesCorruptIndex()
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
            // Corrupt reference: index 99 is beyond PropertySets.Count of 1
            source.Objects.Add(GimObjectFactory.Create(1, "CorruptObject", 99));

            Assert.Throws<ArgumentOutOfRangeException>(() => source.GetGimElementAsGimData(source, 1));
        }

        /// <summary>
        /// Verifies AddSecondaryGimData appends only referenced property sets to the target,
        /// preserves shared set relationships, remaps object indices relative to existing sets,
        /// and omits orphan sets.
        /// </summary>
        [Test]
        public void AddSecondaryGimData_CarriesReferencedPropertySets_PreservesSharedSetIdentity_AndOmitsOrphanSets()
        {
            GimData target = new();
            target.Elements.Add(new GimElement { ElementType = GimElementType.D_root });
            target.PropertySets.Add(GimPropertySet.Create(("name", GimPropertyValue.FromString("TargetOriginal"))));
            target.Objects.Add(GimObjectFactory.Create(0, "TargetRoot", 0));

            GimData secondary = new();
            secondary.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_root,
                Elements = new List<uint> { 1 }
            });
            secondary.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_folder,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint> { 2, 3 }
            });
            secondary.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            });
            secondary.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 1 } },
                Elements = new List<uint>()
            });

            // Set 0 shared by parts, set 1 is unreferenced orphan
            secondary.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SecondaryShared")),
                ("type", GimPropertyValue.FromString("fixture"))));
            secondary.PropertySets.Add(GimPropertySet.Create(
                ("name", GimPropertyValue.FromString("SecondaryOrphan"))));

            secondary.Objects.Add(GimObjectFactory.Create(1, "SecFolder", null));
            secondary.Objects.Add(GimObjectFactory.Create(2, "SecPart1", 0));
            secondary.Objects.Add(GimObjectFactory.Create(3, "SecPart2", 0));

            target.AddSecondaryGimData(secondary, 1, 0, 0, AddObjects: true);

            // Target had 1 set; secondary contributed 1 referenced set. Total must be 2.
            Assert.That(target.PropertySets, Has.Count.EqualTo(2));
            Assert.That(target.PropertySets[0].Entries["name"].StringValue, Is.EqualTo("TargetOriginal"));
            Assert.That(target.PropertySets[1].Entries["name"].StringValue, Is.EqualTo("SecondaryShared"));

            GimObject carriedPart1 = target.Objects.Find(o => o.Name == "SecPart1");
            GimObject carriedPart2 = target.Objects.Find(o => o.Name == "SecPart2");
            Assert.That(carriedPart1, Is.Not.Null);
            Assert.That(carriedPart2, Is.Not.Null);

            // Both carried parts must point to the new index (1)
            Assert.That(carriedPart1.PropertySetIndex, Is.EqualTo(1));
            Assert.That(carriedPart2.PropertySetIndex, Is.EqualTo(1));
            Assert.That(carriedPart1.PropertySetIndex, Is.EqualTo(carriedPart2.PropertySetIndex));

            GimObject carriedFolder = target.Objects.Find(o => o.Name == "SecFolder");
            Assert.That(carriedFolder, Is.Not.Null);
            Assert.That(carriedFolder.PropertySetIndex, Is.Null);
        }

        /// <summary>
        /// Verifies AddSecondaryGimData throws ArgumentOutOfRangeException when a carried object
        /// references a corrupt property set index per ADR 0003.
        /// </summary>
        [Test]
        public void AddSecondaryGimData_ThrowsArgumentOutOfRangeException_WhenCarriedObjectReferencesCorruptIndex()
        {
            GimData target = new();
            target.Elements.Add(new GimElement { ElementType = GimElementType.D_root });

            GimData secondary = new();
            secondary.Elements.Add(new GimElement { ElementType = GimElementType.D_root, Elements = new List<uint> { 1 } });
            secondary.Elements.Add(new GimElement
            {
                ElementType = GimElementType.D_part,
                Locals = new List<GimLocal> { new() { parent = 0 } },
                Elements = new List<uint>()
            });
            // Negative index is invalid
            secondary.Objects.Add(GimObjectFactory.Create(1, "BadPart", -1));

            Assert.Throws<ArgumentOutOfRangeException>(() => target.AddSecondaryGimData(secondary, 1, 0, 0, AddObjects: true));
        }
    }
}
