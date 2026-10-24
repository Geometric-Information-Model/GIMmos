using System;
using System.IO;
using GIMtransfer.KlicImkl;
using NUnit.Framework;

namespace TestPlatform
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class KlicGenericReaderTests
    {
        private static string FixturePath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", "Klic", fileName);

        private static KlicPayload ReadKml() => GenericGmlKmlReader.Read(FixturePath("generic-lines.kml"));

        private static KlicPayload ReadGml() => GenericGmlKmlReader.Read(FixturePath("generic-lines.xml"));

        private static KlicPayload ReadInline(string xml) =>
            GenericGmlKmlReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

        private static KlicLine Line(KlicPayload payload, string sourceGmlId) =>
            payload.Lines.Find(line =>
                line.Metadata.TryGetValue("sourceGmlId", out string id) && id == sourceGmlId);

        private static KlicNetwork Network(KlicPayload payload, string name) =>
            payload.Networks.Find(network => network.Name == name);

        // ---- KML ------------------------------------------------------------------------------

        [Test]
        public void Kml_ReadsKindNetwork()
        {
            Assert.That(ReadKml().Kind, Is.EqualTo(KlicKind.Network));
        }

        [Test]
        public void Kml_GroupsOneNetworkPerFolderPlusBaseNameForOrphans()
        {
            KlicPayload payload = ReadKml();

            Assert.That(payload.Networks.Count, Is.EqualTo(3));
            Assert.That(Network(payload, "Kabels").Index, Is.EqualTo(0));
            Assert.That(Network(payload, "Leidingen").Index, Is.EqualTo(1));
            // Orphan placemarks (outside any Folder) share one network named after the file.
            Assert.That(Network(payload, "generic-lines").Index, Is.EqualTo(2));

            // Generic networks carry no authority metadata: catch-all theme, empty band/owner.
            Assert.That(Network(payload, "Kabels").Theme, Is.EqualTo("overig"));
            Assert.That(Network(payload, "Kabels").Type, Is.EqualTo(string.Empty));
            Assert.That(Network(payload, "Kabels").OwnerCode, Is.EqualTo(string.Empty));
            Assert.That(Network(payload, "Kabels").OwnerName, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Kml_PlacemarkLineStrings_BecomeWktLinesWithPlacemarkNames()
        {
            KlicPayload payload = ReadKml();

            Assert.That(payload.Lines.Count, Is.EqualTo(7));

            KlicLine kabelA = Line(payload, "pm.cable.1");
            Assert.That(kabelA, Is.Not.Null);
            Assert.That(kabelA.Name, Is.EqualTo("Kabel A"));
            Assert.That(kabelA.NetworkIndex, Is.EqualTo(0));
            Assert.That(kabelA.Geometry, Is.EqualTo("LINESTRING(4.9 52, 4.95 52.05)"));

            KlicLine leidingA = Line(payload, "pm.pipe.1");
            Assert.That(leidingA.Name, Is.EqualTo("Leiding A"));
            Assert.That(leidingA.NetworkIndex, Is.EqualTo(1));
            Assert.That(leidingA.Geometry, Is.EqualTo("LINESTRING(5 52.1, 5.1 52.2)"));

            KlicLine orphan = Line(payload, "pm.orphan.1");
            Assert.That(orphan.Name, Is.EqualTo("Los"));
            Assert.That(orphan.NetworkIndex, Is.EqualTo(2));
            Assert.That(orphan.Geometry, Is.EqualTo("LINESTRING(5.2 52.3, 5.3 52.4)"));
        }

        [Test]
        public void Kml_MultiGeometry_TakesFirstLineStringAndIgnoresItsPoint()
        {
            KlicPayload payload = ReadKml();

            // Kabel B's MultiGeometry holds a Point then a LineString: only the LineString wins.
            KlicLine kabelB = Line(payload, "pm.cable.2");
            Assert.That(kabelB, Is.Not.Null);
            Assert.That(kabelB.Geometry, Is.EqualTo("LINESTRING(4.91 52.01, 5.05 52.15)"));
        }

        [Test]
        public void Kml_PointAndPolygonPlacemarks_AreIgnored()
        {
            KlicPayload payload = ReadKml();

            Assert.That(Line(payload, "pm.point.1"), Is.Null);
            Assert.That(Line(payload, "pm.poly.1"), Is.Null);
            Assert.That(payload.Lines.Exists(l => l.Name == "Punt"), Is.False);
            Assert.That(payload.Lines.Exists(l => l.Name == "Vlak"), Is.False);
        }

        [Test]
        public void Kml_ThreeTupleCoordinates_DropAltitude()
        {
            // A lon,lat,alt tuple keeps lon/lat only: the altitude is dropped, not mis-paired.
            KlicLine line = Line(ReadKml(), "pm.alt.1");

            Assert.That(line, Is.Not.Null);
            Assert.That(line.Geometry, Is.EqualTo("LINESTRING(4.92 52.02, 4.96 52.06)"));
        }

        [Test]
        public void Kml_GxTrackPlacemark_IsIgnored()
        {
            // gx:Track carries gx:coord, not <coordinates>: phase 1 emits no line for it.
            KlicPayload payload = ReadKml();

            Assert.That(Line(payload, "pm.track.1"), Is.Null);
            Assert.That(payload.Lines.Exists(l => l.Name == "Track"), Is.False);
        }

        [Test]
        public void Kml_NestedMultiGeometryInMultiGeometry_TakesFirstLineString()
        {
            // A MultiGeometry nested in a MultiGeometry is still a single first-LineString-wins
            // walk: the inner LineString is encountered first and the later outer one is ignored.
            KlicLine line = Line(ReadKml(), "pm.nested.1");

            Assert.That(line, Is.Not.Null);
            Assert.That(line.Geometry, Is.EqualTo("LINESTRING(5.02 52.12, 5.06 52.16)"));
        }

        [Test]
        public void Kml_CdataSplitCoordinates_KeepFullGeometry()
        {
            // XmlReader hands text and CDATA as separate nodes: the element's text must be buffered
            // and parsed whole, otherwise the CDATA half is dropped and the tuple list is truncated.
            KlicLine line = Line(ReadKml(), "pm.cdata.1");

            Assert.That(line, Is.Not.Null);
            Assert.That(line.Geometry, Is.EqualTo("LINESTRING(4.93 52.03, 4.97 52.07)"));
        }

        [Test]
        public void Kml_OrphanPlacemark_LandsInBaseNameNetwork()
        {
            // A placemark outside any Folder groups under the file base name.
            KlicPayload payload = ReadKml();
            KlicLine orphan = Line(payload, "pm.orphan.1");

            Assert.That(orphan, Is.Not.Null);
            Assert.That(orphan.NetworkIndex, Is.EqualTo(2));
            Assert.That(payload.Networks[2].Name, Is.EqualTo("generic-lines"));
        }

        [Test]
        public void Kml_TimeSpan_BecomeDatesAndMetadata()
        {
            KlicLine kabelB = Line(ReadKml(), "pm.cable.2");

            Assert.That(kabelB.StartDate.Value.Year, Is.EqualTo(2018));
            Assert.That(kabelB.StartDate.Value.Month, Is.EqualTo(3));
            Assert.That(kabelB.StartDate.Value.Day, Is.EqualTo(1));
            Assert.That(kabelB.EndDate.Value.Year, Is.EqualTo(2020));
            Assert.That(kabelB.Metadata["validFrom"], Is.EqualTo("2018-03-01T00:00:00Z"));
            Assert.That(kabelB.Metadata["validTo"], Is.EqualTo("2020-05-01T00:00:00Z"));

            // A placemark without a time has null dates but the contract keys still present.
            KlicLine kabelA = Line(ReadKml(), "pm.cable.1");
            Assert.That(kabelA.StartDate, Is.Null);
            Assert.That(kabelA.EndDate, Is.Null);
            Assert.That(kabelA.Metadata["validFrom"], Is.Null);
            Assert.That(kabelA.Metadata["validTo"], Is.Null);
        }

        [Test]
        public void Kml_SourceCrsIsTheWgs84ConstantAndMetadataContractIsUniform()
        {
            KlicPayload payload = ReadKml();

            Assert.That(payload.SourceCrs, Is.EqualTo(KlicCrs.KmlSourceCrs));

            KlicLine line = Line(payload, "pm.cable.1");
            // No width concept in generic KML/GML: widthMm is present but null, like every
            // other per-line contract key, so the payload shape stays uniform.
            Assert.That(line.Metadata.ContainsKey("widthMm"), Is.True);
            Assert.That(line.Metadata["widthMm"], Is.Null);
            Assert.That(line.Metadata.ContainsKey("status"), Is.True);
            Assert.That(line.Metadata.ContainsKey("verticalPosition"), Is.True);
            Assert.That(line.Metadata.ContainsKey("meldnummer"), Is.True);
            Assert.That(line.Metadata.ContainsKey("leveringsvolgnummer"), Is.True);
            Assert.That(line.Metadata["sourceGmlId"], Is.EqualTo("pm.cable.1"));
        }

        [Test]
        public void Kml_SelectionEnvelope_IsComputedFromActualLines()
        {
            KlicSelectionArea area = ReadKml().SelectionArea;

            Assert.That(area, Is.Not.Null);
            Assert.That(area.Envelope, Is.Not.Null);
            Assert.That(area.Envelope.Crs, Is.EqualTo("EPSG:4326"));
            Assert.That(area.Envelope.MinX, Is.EqualTo(4.9d));
            Assert.That(area.Envelope.MinY, Is.EqualTo(52.0d));
            Assert.That(area.Envelope.MaxX, Is.EqualTo(5.3d));
            Assert.That(area.Envelope.MaxY, Is.EqualTo(52.4d));
        }

        // ---- Generic GML ----------------------------------------------------------------------

        [Test]
        public void Gml_GroupsOneNetworkPerFeatureElementType()
        {
            KlicPayload payload = ReadGml();

            Assert.That(payload.Kind, Is.EqualTo(KlicKind.Network));
            Assert.That(payload.Networks.Count, Is.EqualTo(2));
            Assert.That(Network(payload, "road").Index, Is.EqualTo(0));
            Assert.That(Network(payload, "utilitypipe").Index, Is.EqualTo(1));
            Assert.That(Network(payload, "road").Theme, Is.EqualTo("overig"));
            Assert.That(Network(payload, "road").Type, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Gml_LineStrings_BecomeWktLinesGroupedByType()
        {
            KlicPayload payload = ReadGml();

            Assert.That(payload.Lines.Count, Is.EqualTo(4));

            KlicLine road1 = Line(payload, "road.1");
            Assert.That(road1.Name, Is.EqualTo("N1"));
            Assert.That(road1.NetworkIndex, Is.EqualTo(0));
            Assert.That(road1.Geometry, Is.EqualTo("LINESTRING(4.9 52, 5.1 52.2)"));

            KlicLine road2 = Line(payload, "road.2");
            Assert.That(road2.NetworkIndex, Is.EqualTo(0));
            Assert.That(road2.Geometry, Is.EqualTo("LINESTRING(5.1 52.2, 5.2 52.3, 5.3 52.4)"));

            KlicLine pipe = Line(payload, "pipe.1");
            Assert.That(pipe.NetworkIndex, Is.EqualTo(1));
            Assert.That(pipe.Geometry, Is.EqualTo("LINESTRING(5 52.1, 5.05 52.15)"));
        }

        [Test]
        public void Gml_InlineFeatureWithoutFeatureMember_IsParsed()
        {
            // road.3 sits directly under the FeatureCollection with no featureMember wrapper: the
            // reader accepts inline children as features and groups it by element type.
            KlicPayload payload = ReadGml();
            KlicLine road3 = Line(payload, "road.3");

            Assert.That(road3, Is.Not.Null);
            Assert.That(road3.NetworkIndex, Is.EqualTo(0));
            Assert.That(road3.Geometry, Is.EqualTo("LINESTRING(5 52.1, 5.2 52.3)"));
        }

        [Test]
        public void Gml_SourceCrs_IsNormalizedFromGeometrySrsName()
        {
            KlicPayload payload = ReadGml();

            // urn:ogc:def:crs:EPSG::4326, EPSG:4326 and epsg:4326 all normalize to EPSG:4326.
            Assert.That(payload.SourceCrs, Is.EqualTo("EPSG:4326"));
            Assert.That(payload.SelectionArea.Envelope.Crs, Is.EqualTo("EPSG:4326"));
        }

        [Test]
        public void Gml_SelectionEnvelope_IsComputedFromActualLines()
        {
            KlicEnvelope envelope = ReadGml().SelectionArea.Envelope;

            Assert.That(envelope.MinX, Is.EqualTo(4.9d));
            Assert.That(envelope.MinY, Is.EqualTo(52.0d));
            Assert.That(envelope.MaxX, Is.EqualTo(5.3d));
            Assert.That(envelope.MaxY, Is.EqualTo(52.4d));
        }

        [Test]
        public void Gml_ValidFromElement_BecomesDateAndMetadata()
        {
            KlicLine road1 = Line(ReadGml(), "road.1");

            Assert.That(road1.StartDate.Value.Date, Is.EqualTo(new DateTime(2005, 3, 1)));
            Assert.That(road1.Metadata["validFrom"], Is.EqualTo("2005-03-01"));
        }

        [Test]
        public void Gml_SecondLineStringWithoutSrsDimension_DoesNotInheritStride()
        {
            // The first LineString declares srsDimension=3; the second declares none. The 3D stride
            // must not leak onto the second posList (which would abort it as a stride mismatch), so
            // both segments accumulate as 2D and the line is complete.
            const string xml = @"<?xml version=""1.0""?>
<gml:FeatureCollection xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:app=""http://example.org/app"">
  <gml:featureMember>
    <app:road gml:id=""road.multi"">
      <app:geometrie>
        <gml:LineString srsName=""EPSG:4326"">
          <gml:posList srsDimension=""3"">4.90 52.00 5 5.10 52.20 6</gml:posList>
        </gml:LineString>
        <gml:LineString srsName=""EPSG:4326"">
          <gml:posList>5.20 52.30 5.30 52.40</gml:posList>
        </gml:LineString>
      </app:geometrie>
    </app:road>
  </gml:featureMember>
</gml:FeatureCollection>";

            KlicPayload payload = ReadInline(xml);

            Assert.That(payload.Lines.Count, Is.EqualTo(1));
            Assert.That(payload.Lines[0].Geometry,
                Is.EqualTo("LINESTRING(4.9 52, 5.1 52.2, 5.2 52.3, 5.3 52.4)"));
        }

        [Test]
        public void Gml_PosListCommaSeparatedTokens_ParseToWkt()
        {
            // cs="," flips the in-tuple separator off the whitespace default; ts stays default.
            const string xml = @"<?xml version=""1.0""?>
<gml:FeatureCollection xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:app=""http://example.org/app"">
  <gml:featureMember>
    <app:road gml:id=""road.csv"">
      <app:geometrie>
        <gml:LineString srsName=""EPSG:4326"">
          <gml:posList cs="","">4.90,52.00 5.10,52.20</gml:posList>
        </gml:LineString>
      </app:geometrie>
    </app:road>
  </gml:featureMember>
</gml:FeatureCollection>";

            KlicPayload payload = ReadInline(xml);

            Assert.That(payload.Lines.Count, Is.EqualTo(1));
            Assert.That(payload.Lines[0].Geometry, Is.EqualTo("LINESTRING(4.9 52, 5.1 52.2)"));
        }

        [Test]
        public void Gml_PosListStrideMismatch_AbortsGeometryWithoutCrashing()
        {
            // Five tokens with stride 2 leave a trailing remainder that would mispair lon/lat, so
            // the whole geometry is aborted (no line) instead of silently dropping the remainder.
            const string xml = @"<?xml version=""1.0""?>
<gml:FeatureCollection xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:app=""http://example.org/app"">
  <gml:featureMember>
    <app:road gml:id=""road.odd"">
      <app:geometrie>
        <gml:LineString srsName=""EPSG:4326"">
          <gml:posList>4.90 52.00 5.10 52.20 5.30</gml:posList>
        </gml:LineString>
      </app:geometrie>
    </app:road>
  </gml:featureMember>
</gml:FeatureCollection>";

            KlicPayload payload = ReadInline(xml);

            Assert.That(payload.Lines, Is.Empty);
        }
    }
}
