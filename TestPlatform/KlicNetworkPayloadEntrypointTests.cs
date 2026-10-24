using System;
using System.IO;
using System.Threading.Tasks;
using GIMtransfer;
using GIMtransfer.KlicImkl;
using NUnit.Framework;

namespace TestPlatform
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class KlicNetworkPayloadEntrypointTests
    {
        private static string FixturePath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", "Klic", fileName);

        // Removes the per-run payload folder (%TEMP%\GIM\<name>\) written by the entrypoint.
        private static void DeletePayloadFolder(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return;
            string directory = Path.GetDirectoryName(outputPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [Test]
        public async Task FileToNetworkPayload_MiniFixture_EmitsKlicPayloadAndWritesJson()
        {
            // Unique source basename per run: the payload lands in its own %TEMP%\GIM\<name>\ folder,
            // so parallel runs never collide; both the source and that folder are torn down below.
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            File.Copy(FixturePath("mini-delivery.xml"), source, overwrite: true);
            string outputPath = null;
            try
            {
                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    source, null, new GimOptions());
                outputPath = result.OutputPath;

                Assert.That(result.Kind, Is.EqualTo(KlicKind.Klic));
                Assert.That(result.Meldnummer, Is.EqualTo("26O0074042"));
                Assert.That(result.WoonplaatsNaam, Is.EqualTo("Arnhem"));
                Assert.That(result.LineCount, Is.EqualTo(6));
                Assert.That(result.NetworkCount, Is.EqualTo(3));
                Assert.That(result.Json, Is.Not.Null.And.Not.Empty);
                Assert.That(result.OutputPath, Is.Not.Null);
                Assert.That(File.Exists(result.OutputPath), Is.True);

                string written = File.ReadAllText(result.OutputPath);
                Assert.That(written, Is.EqualTo(result.Json));

                // Property names stay PascalCase; the Kind enum serializes as the documented verdict.
                Assert.That(written, Does.Contain("\"Networks\""));
                Assert.That(written, Does.Contain("\"Lines\""));
                Assert.That(written, Does.Contain("\"SelectionArea\""));
                Assert.That(written, Does.Contain("\"Kind\":\"klic\""));
            }
            finally
            {
                File.Delete(source);
                if (!string.IsNullOrEmpty(outputPath))
                {
                    string directory = Path.GetDirectoryName(outputPath);
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task FileToNetworkPayload_NotNetwork_SignalsFallbackWithNoOutput()
        {
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(source,
                @"<core:CityModel xmlns:core=""http://www.opengis.net/citygml/2.0""><core:cityObjectMember/></core:CityModel>");
            try
            {
                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    source, null, new GimOptions());

                Assert.That(result.Kind, Is.EqualTo(KlicKind.NotNetwork));
                Assert.That(result.OutputPath, Is.Null);
                Assert.That(result.Json, Is.Null);
            }
            finally
            {
                File.Delete(source);
            }
        }

        [Test]
        public async Task FileToNetworkPayload_KmlFixture_EmitsNetworkPayload()
        {
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".kml");
            File.Copy(FixturePath("generic-lines.kml"), source, overwrite: true);
            string outputPath = null;
            try
            {
                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    source, null, new GimOptions());
                outputPath = result.OutputPath;

                Assert.That(result.Kind, Is.EqualTo(KlicKind.Network));
                Assert.That(result.LineCount, Is.EqualTo(7));
                Assert.That(result.NetworkCount, Is.EqualTo(3));
                Assert.That(result.Json, Is.Not.Null.And.Not.Empty);
                Assert.That(result.OutputPath, Is.Not.Null);
                Assert.That(File.Exists(result.OutputPath), Is.True);

                // Generic KML rides the same payload shape, with SourceCrs always present.
                Assert.That(result.Json, Does.Contain("\"Kind\":\"network\""));
                Assert.That(result.Json, Does.Contain("\"SourceCrs\":\"urn:ogc:def:crs:EPSG::4326\""));
                Assert.That(result.Json, Does.Contain("\"Name\":\"Kabels\""));
            }
            finally
            {
                File.Delete(source);
                DeletePayloadFolder(outputPath);
            }
        }

        [Test]
        public async Task FileToNetworkPayload_GmlFixture_EmitsNetworkPayload()
        {
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            File.Copy(FixturePath("generic-lines.xml"), source, overwrite: true);
            string outputPath = null;
            try
            {
                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    source, null, new GimOptions());
                outputPath = result.OutputPath;

                Assert.That(result.Kind, Is.EqualTo(KlicKind.Network));
                Assert.That(result.LineCount, Is.EqualTo(4));
                Assert.That(result.NetworkCount, Is.EqualTo(2));
                Assert.That(result.Json, Does.Contain("\"SourceCrs\":\"EPSG:4326\""));
            }
            finally
            {
                File.Delete(source);
                DeletePayloadFolder(outputPath);
            }
        }

        [Test]
        public async Task FileToNetworkPayload_CityGmlBuildingPolygons_SignalsNotNetwork()
        {
            // A CityGML-style building model (core:CityModel root) has no network-line content:
            // the sniff already classifies it notNetwork, so the untouched mesh path is used.
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(source,
                @"<core:CityModel xmlns:core=""http://www.opengis.net/citygml/2.0"" " +
                @"xmlns:gml=""http://www.opengis.net/gml/3.2"" " +
                @"xmlns:bldg=""http://www.opengis.net/citygml/building/2.0"">" +
                @"<core:cityObjectMember><bldg:Building gml:id=""bldg.1""><bldg:lod1Solid><gml:Solid><gml:exterior>" +
                @"<gml:CompositeSurface><gml:surfaceMember><gml:Polygon><gml:exterior><gml:LinearRing>" +
                @"<gml:posList>0 0 10 0 10 10 0 0</gml:posList></gml:LinearRing></gml:exterior></gml:Polygon>" +
                @"</gml:surfaceMember></gml:CompositeSurface></gml:exterior></gml:Solid></bldg:lod1Solid>" +
                @"</bldg:Building></core:cityObjectMember></core:CityModel>");
            try
            {
                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    source, null, new GimOptions());

                Assert.That(result.Kind, Is.EqualTo(KlicKind.NotNetwork));
                Assert.That(result.OutputPath, Is.Null);
                Assert.That(result.Json, Is.Null);
            }
            finally
            {
                File.Delete(source);
            }
        }

        [Test]
        public async Task FileToNetworkPayload_GmlWithoutLinework_DowngradesToNotNetwork()
        {
            // A gml:FeatureCollection sniffs as network, but polygon-only features yield no line:
            // an empty payload is effectively notNetwork (ADR 0004), so fall back to the mesh path.
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(source,
                @"<gml:FeatureCollection xmlns:gml=""http://www.opengis.net/gml/3.2"" xmlns:app=""urn:example:app"">" +
                @"<gml:featureMember><app:Building><app:geometrie><gml:Polygon><gml:exterior><gml:LinearRing>" +
                @"<gml:posList>0 0 1 0 1 1 0 0</gml:posList></gml:LinearRing></gml:exterior></gml:Polygon>" +
                @"</app:geometrie></app:Building></gml:featureMember></gml:FeatureCollection>");
            try
            {
                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    source, null, new GimOptions());

                Assert.That(result.Kind, Is.EqualTo(KlicKind.NotNetwork));
                Assert.That(result.OutputPath, Is.Null);
            }
            finally
            {
                File.Delete(source);
            }
        }

        [Test]
        public async Task FileToNetworkPayload_AanvraagRoot_StillStampsImklSourceCrs()
        {
            // A GebiedsinformatieAanvraag-rooted IMKL file sniffs as klic but never reaches the
            // GebiedsinformatieLevering header branch: the entrypoint must still stamp the IMKL
            // 28992 URN so SourceCrs is present for every klic verdict.
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(source, @"<?xml version=""1.0""?>
<imkl:GebiedsinformatieAanvraag xmlns:imkl=""http://www.imkl.nl/2.0"">
  <imkl:woonplaatsNaam>Arnhem</imkl:woonplaatsNaam>
</imkl:GebiedsinformatieAanvraag>");
            string outputPath = null;
            try
            {
                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    source, null, new GimOptions());
                outputPath = result.OutputPath;

                Assert.That(result.Kind, Is.EqualTo(KlicKind.Klic));
                Assert.That(result.Json, Does.Contain("\"Kind\":\"klic\""));
                Assert.That(result.Json,
                    Does.Contain("\"SourceCrs\":\"urn:ogc:def:crs:EPSG::28992\""));
            }
            finally
            {
                File.Delete(source);
                DeletePayloadFolder(outputPath);
            }
        }

        [Test]
        public void ReadGmlToGimData_SupportsType_IsCaseInsensitive()
        {
            Assert.That(ReadGMLToGimData.SupportsType("GML"), Is.True);
            Assert.That(ReadGMLToGimData.SupportsType("Kml"), Is.True);
            Assert.That(ReadGMLToGimData.SupportsType("dwg"), Is.False);
            Assert.That(ReadGMLToGimData.SupportsType(null), Is.False);
        }
    }
}
