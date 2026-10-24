using GIMcompress;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using TestHelpers;

namespace TestPlatform
{
	[TestFixture]
	public sealed class PropertyHelpersTests
	{
		[Test]
		public void ResolveDisplayName_UsesStringNameOrObjectFallback()
		{
			GimData gimData = CreateData();
			gimData.Objects.Add(new GimObject { name = "LED-legacy", propertySetIndex = 0 });
			gimData.Objects.Add(new GimObject { name = "door-17" });

			Assert.That(GimPropertyHelpers.ResolveDisplayName(gimData, 0), Is.EqualTo("LED"));
			Assert.That(GimPropertyHelpers.ResolveDisplayName(gimData, 1), Is.EqualTo("door-17"));
		}

		[Test]
		public void ResolveDisplayName_IgnoresNonStringName()
		{
			GimData gimData = new GimData();
			GimPropertySet set = new GimPropertySet();
			set.AddDouble("name", 42);
			gimData.PropertySets.Add(set);
			gimData.Objects.Add(new GimObject { name = "door-17", propertySetIndex = 0 });

			Assert.That(GimPropertyHelpers.ResolveDisplayName(gimData, 0), Is.EqualTo("door-17"));
		}

		[Test]
		public void TryGetPropertyString_ReadsTypeString()
		{
			GimData gimData = CreateData();
			gimData.Objects.Add(new GimObject { name = "lamp", propertySetIndex = 0 });

			Assert.That(GimPropertyHelpers.TryGetPropertyString(gimData, 0, "type", out string type), Is.True);
			Assert.That(type, Is.EqualTo("electrical_component"));
			Assert.That(GimPropertyHelpers.TryGetPropertyString(gimData, 0, "Geolocation_X", out _), Is.False);
		}

		[Test]
		public void PropertySetToString_RendersTaggedValues()
		{
			GimPropertySet set = CreateData().PropertySets[0];

			// ponytail: Dictionary iteration order is not guaranteed, so assert contains
			string s = set.ToString();
			Assert.That(s, Does.Contain("name:String(\"LED\")"));
			Assert.That(s, Does.Contain("type:String(\"electrical_component\")"));
			Assert.That(s, Does.Contain("LED_TYPE:String(\"COB LED\")"));
			Assert.That(s, Does.Contain("Geolocation_X:Double(190000)"));
		}

		[Test]
		public void CompareGimDatas_ComparisonsIncludePropertySetsAndObjectLinks()
		{
			GimData expected = CreateData();
			expected.Objects.Add(new GimObject { name = "lamp", propertySetIndex = 0 });
			GimData actual = CreateData();
			actual.Objects.Add(new GimObject { name = "lamp", propertySetIndex = 0 });

			Assert.That(TestComparison.CompareGimDatas(expected, actual), Is.True);
		}

		[Test]
		public void CompareGimDatas_FailsWhenPropertyValuesDiffer()
		{
			GimData expected = CreateData();
			GimData actual = CreateData();
			actual.PropertySets[0].AddDouble("Geolocation_X", 190001);

			Assert.Throws<AssertionException>(() => TestComparison.CompareGimDatas(expected, actual));
		}

		[Test]
		public void CheckCorrectnessPropertySets_ValidatesEntriesAndObjectLinks()
		{
			GimData gimData = CreateData();
			gimData.Elements.Add(new GimElement { elementType = GimElementType.D_root });
			gimData.Objects.Add(new GimObject { propertySetIndex = 0 });

			Assert.That(CorrectnessCheck.CheckCorrectnessObjects(gimData), Is.True);
			Assert.That(CorrectnessCheck.CheckCorrectnessPropertySets(gimData), Is.True);
		}

		[Test]
		public void CheckCorrectnessObjects_FailsForOutOfRangePropertySetLink()
		{
			GimData gimData = CreateData();
			gimData.Elements.Add(new GimElement { elementType = GimElementType.D_root });
			gimData.Objects.Add(new GimObject { propertySetIndex = 1 });

			Assert.Throws<AssertionException>(() => CorrectnessCheck.CheckCorrectnessObjects(gimData));
		}

		private static GimData CreateData()
		{
			GimPropertySet set = new GimPropertySet();
			set.AddString("name", "LED");
			set.AddString("type", "electrical_component");
			set.AddString("LED_TYPE", "COB LED");
			set.AddDouble("Geolocation_X", 190000);
			GimData gimData = new GimData();
			gimData.PropertySets.Add(set);
			return gimData;
		}
	}
}
