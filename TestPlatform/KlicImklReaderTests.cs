using System;
using System.IO;
using GIMtransfer.KlicImkl;
using NUnit.Framework;

namespace TestPlatform
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class KlicImklReaderTests
    {
        private static string FixturePath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", "Klic", fileName);

        private static KlicPayload ReadMini() => KlicImklReader.Read(FixturePath("mini-delivery.xml"));

        private static KlicLine Line(KlicPayload payload, string sourceGmlId) =>
            payload.Lines.Find(line =>
                line.Metadata.TryGetValue("sourceGmlId", out string id) && id == sourceGmlId);

        private const string CableId = "nl.imkl-KL1040.kabel.12406439"; // kabel = cable
        private const string WaterId = "nl.imkl-KL1040.water.777";
        private const string SewerId = "nl.imkl-GM0202.riool.555"; // riool = sewer
        private const string CasingId = "nl.imkl-GM0202.mantel.888"; // mantel = casing
        private const string DuctId = "nl.imkl-KL1040.duct.999";
        private const string DottedCableId = "nl.imkl-KL1128.kabel.4242";

        [Test]
        public void Read_Header_ParsesDeliveryReportNumberVersionAndTown()
        {
            KlicPayload payload = ReadMini();

            Assert.That(payload.Kind, Is.EqualTo(KlicKind.Klic));
            Assert.That(payload.Meldnummer, Is.EqualTo("26O0074042"));
            Assert.That(payload.Leveringsvolgnummer, Is.EqualTo(2));
            Assert.That(payload.WoonplaatsNaam, Is.EqualTo("Arnhem"));
        }

        [Test]
        public void Read_SourceCrs_IsTheRd28992Constant()
        {
            // A4: the IMKL path now stamps the payload CRS (RD New, EPSG:28992 URN) so the
            // importer can ST_Transform; generic KML/GML carry their own SourceCrs.
            Assert.That(ReadMini().SourceCrs, Is.EqualTo(KlicCrs.ImklSourceCrs));
        }

        [Test]
        public void Read_Networks_ParsesAllNetworksAndResolvesOwnerNames()
        {
            KlicPayload payload = ReadMini();

            Assert.That(payload.Networks.Count, Is.EqualTo(3));

            KlicNetwork first = payload.Networks[0];
            Assert.That(first.Index, Is.EqualTo(0));
            // Theme = coarse class from utilityNetworkType; Type = band from imkl:thema. Both are
            // asserted at once so a re-swap cannot pass.
            Assert.That(first.Theme, Is.EqualTo("elektriciteit"));
            Assert.That(first.Type, Is.EqualTo("middenspanning"));
            Assert.That(first.OwnerCode, Is.EqualTo("KL1040"));
            Assert.That(first.OwnerName, Is.EqualTo("Liander"));
            Assert.That(first.SourceId, Is.EqualTo("nl.imkl-KL1040.net.middenspanning"));

            KlicNetwork second = payload.Networks[1];
            Assert.That(second.Index, Is.EqualTo(1));
            Assert.That(second.Theme, Is.EqualTo("riolering"));
            Assert.That(second.Type, Is.EqualTo("riool"));
            Assert.That(second.OwnerCode, Is.EqualTo("GM0202"));
            Assert.That(second.OwnerName, Is.EqualTo("Gemeente Arnhem"));

            // Third network uses the dotted peer-id style; only its canonical (dashed) key works.
            KlicNetwork third = payload.Networks[2];
            Assert.That(third.Theme, Is.EqualTo("elektriciteit"));
            Assert.That(third.Type, Is.EqualTo("laagspanning"));
            Assert.That(third.OwnerCode, Is.EqualTo("KL1128"));
            Assert.That(third.SourceId, Is.EqualTo("nl.imkl-KL1128.net.dotted"));
        }

        [Test]
        public void Read_OwnerMap_IgnoresNonOperatorRegistryNames()
        {
            KlicPayload payload = ReadMini();

            Assert.That(payload.Networks[0].OwnerName, Is.EqualTo("Liander"));
            Assert.That(payload.Networks[0].OwnerName, Is.Not.EqualTo("Deze naam mag niet winnen")); // = "this name must not win"
        }

        [Test]
        public void Read_Join_LineBeforeItsLink_StillResolvesGeometry()
        {
            // Cable feature appears after its UtilityLink in stream order.
            KlicLine line = Line(ReadMini(), CableId);

            Assert.That(line, Is.Not.Null);
            Assert.That(line.Geometry, Is.EqualTo("LINESTRING(150010.5 450010.25, 150020 450020, 150030 450030)"));
            Assert.That(line.NetworkIndex, Is.EqualTo(0));
        }

        [Test]
        public void Read_Join_LineAfterItsLink_StillResolvesGeometry()
        {
            // Water feature appears before its Curve UtilityLink in stream order.
            KlicLine line = Line(ReadMini(), WaterId);

            Assert.That(line, Is.Not.Null);
            Assert.That(line.Geometry, Is.EqualTo("LINESTRING(150100 450100, 150110 450110)"));
            Assert.That(line.NetworkIndex, Is.EqualTo(0));
        }

        [Test]
        public void Read_Join_SecondOwnerLineMapsToSecondNetwork()
        {
            KlicLine line = Line(ReadMini(), SewerId);

            Assert.That(line, Is.Not.Null);
            Assert.That(line.NetworkIndex, Is.EqualTo(1));
            Assert.That(line.Geometry, Is.EqualTo("LINESTRING(150200 450200, 150210 450210)"));
        }

        [Test]
        public void Read_Join_MultiSegmentCurveAccumulatesAllSegmentsInOrder()
        {
            // Duct's Curve carries two LineStringSegments; both must land in one line.
            KlicLine line = Line(ReadMini(), DuctId);

            Assert.That(line, Is.Not.Null);
            Assert.That(line.Geometry, Is.EqualTo(
                "LINESTRING(150400 450400, 150410 450410, 150420 450420, 150430 450430)"));
            Assert.That(line.NetworkIndex, Is.EqualTo(0));
        }

        [Test]
        public void Read_Join_MixedDashedAndDottedPeerIds_Resolve()
        {
            // Feature id is dotted; the link href is dashed, the network href targets a
            // dotted-declared network: canonicalization must bridge all three.
            KlicPayload payload = ReadMini();
            KlicLine line = Line(payload, DottedCableId);

            Assert.That(line, Is.Not.Null);
            Assert.That(line.Geometry, Is.EqualTo("LINESTRING(150500 450500, 150510 450510)"));
            Assert.That(line.NetworkIndex, Is.EqualTo(2));
            Assert.That(line.Type, Is.EqualTo("laagspanning"));
            Assert.That(line.Name, Is.EqualTo("KL1128 · laagspanning · 4242"));
        }

        [Test]
        public void Read_Names_AreMintedFromOwnerTypeAndLocalIdTail()
        {
            KlicPayload payload = ReadMini();

            Assert.That(Line(payload, CableId).Name, Is.EqualTo("Liander · middenspanning · 12406439"));
            Assert.That(Line(payload, SewerId).Name, Is.EqualTo("Gemeente Arnhem · riool · 555"));
        }

        [Test]
        public void Read_Dates_NilSentinelAndExplicitMaxAreHandled()
        {
            KlicPayload payload = ReadMini();

            KlicLine cable = Line(payload, CableId);
            Assert.That(cable.StartDate, Is.EqualTo(new DateTime(2005, 3, 1)));
            Assert.That(cable.EndDate, Is.Null);
            Assert.That(cable.Metadata["validTo"], Is.Null);

            KlicLine water = Line(payload, WaterId);
            Assert.That(water.StartDate, Is.Null, "1968-01-01 is a sentinel");
            Assert.That(water.EndDate, Is.Null, "1900-01-01 is a sentinel");
            Assert.That(water.Metadata["validFrom"], Is.EqualTo("1968-01-01"));

            KlicLine sewer = Line(payload, SewerId);
            Assert.That(sewer.StartDate, Is.Not.Null);
            Assert.That(sewer.StartDate.Value.Date, Is.EqualTo(new DateTime(2010, 5, 5)));
            Assert.That(sewer.EndDate, Is.EqualTo(new DateTime(9999, 12, 31)));

            KlicLine casing = Line(payload, CasingId);
            Assert.That(casing.StartDate, Is.Not.Null);
            Assert.That(casing.StartDate.Value.Year, Is.EqualTo(2011));
            Assert.That(casing.StartDate.Value.Hour, Is.EqualTo(12));
        }

        [Test]
        public void Read_Width_NilAndRealValuesBothKept()
        {
            KlicPayload payload = ReadMini();

            // Width is metadata-only: the widthMm key is always present, null when the feature has none.
            Assert.That(Line(payload, CableId).Metadata["widthMm"], Is.EqualTo("110"));
            Assert.That(Line(payload, WaterId).Metadata["widthMm"], Is.EqualTo("250"));
            Assert.That(Line(payload, SewerId).Metadata["widthMm"], Is.Null);
            Assert.That(Line(payload, CasingId).Metadata["widthMm"], Is.Null);
        }

        [Test]
        public void Read_Width_FalsePositiveElementsDoNotOverrideRealWidth()
        {
            // widthUnit / widthUom / widthAttributeRef all contain "width" but are not values;
            // a leaking one would overwrite ductWidth with an unparsable token (null).
            KlicPayload payload = ReadMini();

            Assert.That(Line(payload, CableId).Metadata["widthMm"], Is.EqualTo("110"));
        }

        [Test]
        public void Read_SelectionArea_NormalizesCrsAndBuildsPolygonWkt()
        {
            KlicSelectionArea area = ReadMini().SelectionArea;

            Assert.That(area, Is.Not.Null);
            Assert.That(area.Envelope, Is.Not.Null);
            Assert.That(area.Envelope.Crs, Is.EqualTo("EPSG:28992"));
            Assert.That(area.Envelope.MinX, Is.EqualTo(150000d));
            Assert.That(area.Envelope.MinY, Is.EqualTo(450000d));
            Assert.That(area.Envelope.MaxX, Is.EqualTo(151000d));
            Assert.That(area.Envelope.MaxY, Is.EqualTo(451000d));

            Assert.That(area.OrientationPolygonWkt,
                Is.EqualTo("POLYGON((150000 450000, 151000 450000, 151000 451000, 150000 451000, 150000 450000))"));
        }

        [Test]
        public void Read_SupportFeatures_AreIgnored()
        {
            KlicPayload payload = ReadMini();

            Assert.That(payload.Lines.Count, Is.EqualTo(6));
            Assert.That(payload.Lines.Exists(l => l.Geometry == "LINESTRING(1 1, 2 2)"), Is.False);
            Assert.That(payload.Lines.Exists(l => l.Metadata.TryGetValue("sourceGmlId", out string id) && id.Contains("maatvoering")), Is.False);
            Assert.That(payload.Lines.Exists(l => l.Metadata.TryGetValue("sourceGmlId", out string id) && id.Contains("annotatie")), Is.False);
        }

        [Test]
        public void Read_Metadata_CarriesThePerLineContract()
        {
            KlicLine line = Line(ReadMini(), CableId);

            // Network facts are not duplicated per line: they resolve via NetworkIndex against
            // Networks[] (the single source of network facts).
            Assert.That(line.NetworkIndex, Is.EqualTo(0));
            Assert.That(line.Metadata.ContainsKey("networkType"), Is.False);
            Assert.That(line.Metadata.ContainsKey("theme"), Is.False);
            Assert.That(line.Metadata.ContainsKey("ownerCode"), Is.False);
            Assert.That(line.Metadata.ContainsKey("ownerName"), Is.False);
            Assert.That(line.Metadata["widthMm"], Is.EqualTo("110"));
            Assert.That(line.Metadata["status"], Is.EqualTo("inBedrijf"));
            Assert.That(line.Metadata["verticalPosition"], Is.EqualTo("ondergronds"));
            Assert.That(line.Metadata["sourceGmlId"], Is.EqualTo(CableId));
            Assert.That(line.Metadata["meldnummer"], Is.EqualTo("26O0074042"));
            Assert.That(line.Metadata["leveringsvolgnummer"], Is.EqualTo("2"));
        }

        [Test]
        public void Read_UnknownOwnerName_FallsBackToOwnerCode()
        {
            const string xml = @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0""
    xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:net=""http://www.kadaster.nl/net/2.0""
    xmlns:xlink=""http://www.w3.org/1999/xlink"">
  <imkl:Utiliteitsnet gml:id=""nl.imkl-XX9999.net.1"">
    <imkl:thema>laagspanning</imkl:thema>
  </imkl:Utiliteitsnet>
  <us-net-common:UtilityLink xmlns:us-net-common=""http://www.kadaster.nl/us-net-common/2.0"" gml:id=""nl.imkl-XX9999.link.1"">
    <gml:LineString><gml:posList>1 1 2 2</gml:posList></gml:LineString>
  </us-net-common:UtilityLink>
  <imkl:Duct gml:id=""nl.imkl-XX9999.duct.9"">
    <net:link xlink:href=""#nl.imkl-XX9999.link.1""/>
    <net:inNetwork xlink:href=""#nl.imkl-XX9999.net.1""/>
  </imkl:Duct>
</imkl:GebiedsinformatieLevering>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.Lines.Count, Is.EqualTo(1));
            Assert.That(payload.Lines[0].Name, Is.EqualTo("XX9999 · laagspanning · 9"));
            Assert.That(payload.Lines[0].NetworkIndex, Is.EqualTo(0));
        }

        [Test]
        public void Read_PosList_MalformedTokenAbortsGeometryWithoutCrashing()
        {
            const string xml = @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0""
    xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:net=""http://www.kadaster.nl/net/2.0""
    xmlns:xlink=""http://www.w3.org/1999/xlink"">
  <us-net-common:UtilityLink xmlns:us-net-common=""http://www.kadaster.nl/us-net-common/2.0"" gml:id=""nl.imkl-XX1.link.bad"">
    <gml:LineString><gml:posList>1 1 junk 2 3 3</gml:posList></gml:LineString>
  </us-net-common:UtilityLink>
  <imkl:Elektriciteitskabel gml:id=""nl.imkl-XX1.kabel.1"">
    <net:link xlink:href=""#nl.imkl-XX1.link.bad""/>
  </imkl:Elektriciteitskabel>
</imkl:GebiedsinformatieLevering>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.Lines.Count, Is.EqualTo(1));
            Assert.That(payload.Lines[0].Geometry, Is.Null);
        }

        [Test]
        public void Read_GenericGmlWithoutImklHeader_ReportsNotNetworkKind()
        {
            // Parse() must stay safe standalone: no positive IMKL header means notNetwork.
            const string xml =
                @"<gml:FeatureCollection xmlns:gml=""http://www.opengis.net/gml/3.2""><gml:featureMember/></gml:FeatureCollection>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.Kind, Is.EqualTo(KlicKind.NotNetwork));
            Assert.That(payload.Lines, Is.Empty);
        }

        [Test]
        public void CanonicalizeId_AnchorsDottedRewriteToPeerPrefix()
        {
            Assert.That(KlicText.CanonicalizeId("nl.imkl.KL1128.net.dotted"),
                Is.EqualTo("nl.imkl-KL1128.net.dotted"));
            Assert.That(KlicText.CanonicalizeId("#nl.imkl.KL1128.kabel.1"),
                Is.EqualTo("nl.imkl-KL1128.kabel.1"));

            // Already-dashed form passes through untouched.
            Assert.That(KlicText.CanonicalizeId("nl.imkl-KL1128.kabel.1"),
                Is.EqualTo("nl.imkl-KL1128.kabel.1"));

            // A value merely containing "imkl." behind another prefix must not be rewritten.
            Assert.That(KlicText.CanonicalizeId("urn:example:imkl.foo.bar"),
                Is.EqualTo("urn:example:imkl.foo.bar"));
            Assert.That(KlicText.CanonicalizeId("x.nl.imkl.foo"),
                Is.EqualTo("x.nl.imkl.foo"));
        }

        [Test]
        public void Read_Curve_MidGeometryDimensionShiftAbortsGeometry()
        {
            const string xml = @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0""
    xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:net=""http://www.kadaster.nl/net/2.0""
    xmlns:xlink=""http://www.w3.org/1999/xlink"">
  <us-net-common:UtilityLink xmlns:us-net-common=""http://www.kadaster.nl/us-net-common/2.0"" gml:id=""nl.imkl-XX1.link.1"">
    <gml:Curve>
      <gml:segments>
        <gml:LineStringSegment><gml:posList srsDimension=""3"">1 1 0 2 2 0</gml:posList></gml:LineStringSegment>
        <gml:LineStringSegment><gml:posList srsDimension=""2"">3 3 4 4</gml:posList></gml:LineStringSegment>
      </gml:segments>
    </gml:Curve>
  </us-net-common:UtilityLink>
  <imkl:Elektriciteitskabel gml:id=""nl.imkl-XX1.kabel.1"">
    <net:link xlink:href=""#nl.imkl-XX1.link.1""/>
  </imkl:Elektriciteitskabel>
</imkl:GebiedsinformatieLevering>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.Lines.Count, Is.EqualTo(1));
            Assert.That(payload.Lines[0].Geometry, Is.Null);
        }

        [Test]
        public void Read_Curve_ConsistentDimensionKeepsGeometry()
        {
            const string xml = @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0""
    xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:net=""http://www.kadaster.nl/net/2.0""
    xmlns:xlink=""http://www.w3.org/1999/xlink"">
  <us-net-common:UtilityLink xmlns:us-net-common=""http://www.kadaster.nl/us-net-common/2.0"" gml:id=""nl.imkl-XX1.link.1"">
    <gml:Curve>
      <gml:segments>
        <gml:LineStringSegment><gml:posList srsDimension=""3"">1 1 0 2 2 0</gml:posList></gml:LineStringSegment>
        <gml:LineStringSegment><gml:posList srsDimension=""3"">3 3 0 4 4 0</gml:posList></gml:LineStringSegment>
      </gml:segments>
    </gml:Curve>
  </us-net-common:UtilityLink>
  <imkl:Elektriciteitskabel gml:id=""nl.imkl-XX1.kabel.1"">
    <net:link xlink:href=""#nl.imkl-XX1.link.1""/>
  </imkl:Elektriciteitskabel>
</imkl:GebiedsinformatieLevering>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.Lines.Count, Is.EqualTo(1));
            Assert.That(payload.Lines[0].Geometry, Is.EqualTo("LINESTRING(1 1, 2 2, 3 3, 4 4)"));
        }

        [Test]
        public void Read_SelectionArea_MalformedOrientationIsDroppedNullSafe()
        {
            const string xml = @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0""
    xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:xlink=""http://www.w3.org/1999/xlink"">
  <imkl:SelectionArea>
    <imkl:Orientatiepolygoon gml:id=""nl.imkl-XX1.orientatie.1"">
      <imkl:geometrie>
        <gml:Polygon>
          <gml:exterior><gml:LinearRing><gml:posList>1 1 junk 3 3</gml:posList></gml:LinearRing></gml:exterior>
        </gml:Polygon>
      </imkl:geometrie>
    </imkl:Orientatiepolygoon>
  </imkl:SelectionArea>
</imkl:GebiedsinformatieLevering>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.SelectionArea?.OrientationPolygonWkt, Is.Null);
        }

        [Test]
        public void Read_Codelist_BareCodeSpaceUriDoesNotLeakFragmentAsValue()
        {
            const string xml = @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0""
    xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:xlink=""http://www.w3.org/1999/xlink"">
  <imkl:Utiliteitsnet gml:id=""nl.imkl-YY1.net.1"">
    <us-net-common:utilityNetworkType xmlns:us-net-common=""http://www.kadaster.nl/us-net-common/2.0""
        codeSpace=""http://www.kadaster.nl/codes/utilityNetworkType""/>
    <imkl:thema codeSpace=""http://www.kadaster.nl/codes/thema""/>
  </imkl:Utiliteitsnet>
</imkl:GebiedsinformatieLevering>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.Networks.Count, Is.EqualTo(1));
            Assert.That(payload.Networks[0].Type, Is.Null);
            Assert.That(payload.Networks[0].Theme, Is.Null);
        }

        [Test]
        public void Read_Join_FallsBackToLinkInNetworkWhenFeatureLacksOne()
        {
            const string xml = @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0""
    xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:net=""http://www.kadaster.nl/net/2.0""
    xmlns:xlink=""http://www.w3.org/1999/xlink"">
  <imkl:Utiliteitsnet gml:id=""nl.imkl-YY1.net.1""><imkl:thema>laagspanning</imkl:thema></imkl:Utiliteitsnet>
  <us-net-common:UtilityLink xmlns:us-net-common=""http://www.kadaster.nl/us-net-common/2.0"" gml:id=""nl.imkl-YY1.link.1"">
    <net:inNetwork xlink:href=""#nl.imkl-YY1.net.1""/>
    <gml:LineString><gml:posList>1 1 2 2</gml:posList></gml:LineString>
  </us-net-common:UtilityLink>
  <imkl:Elektriciteitskabel gml:id=""nl.imkl-YY1.kabel.1"">
    <net:link xlink:href=""#nl.imkl-YY1.link.1""/>
  </imkl:Elektriciteitskabel>
</imkl:GebiedsinformatieLevering>";

            KlicPayload payload = KlicImklReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

            Assert.That(payload.Lines.Count, Is.EqualTo(1));
            Assert.That(payload.Lines[0].NetworkIndex, Is.EqualTo(0));
            Assert.That(payload.Lines[0].Geometry, Is.EqualTo("LINESTRING(1 1, 2 2)"));
        }
    }
}
