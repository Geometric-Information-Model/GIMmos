using System.IO;
using System.Xml;
using GIMtransfer.KlicImkl;
using NUnit.Framework;

namespace TestPlatform
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class KlicKindDetectionTests
    {
        private static KlicKind Detect(string xml)
        {
            using StringReader source = new(xml);
            using XmlReader reader = XmlReader.Create(source);
            return KlicImklReader.DetectKind(reader);
        }

        [Test]
        public void DetectKind_ImklNamespace_ReturnsKlic()
        {
            const string xml =
                @"<imkl:GebiedsinformatieLevering xmlns:imkl=""http://www.imkl.nl/2.0"" id=""x""/>";
            Assert.That(Detect(xml), Is.EqualTo(KlicKind.Klic));
        }

        [Test]
        public void DetectKind_ImklWibonSchemaLocationOnly_ReturnsKlic()
        {
            const string xml =
                @"<Levering xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " +
                @"xsi:schemaLocation=""http://www.imkl.nl/2.0 http://www.kadaster.nl/schemas/imkl-wibon-2.0.xsd""/>";
            Assert.That(Detect(xml), Is.EqualTo(KlicKind.Klic));
        }

        [Test]
        public void DetectKind_Kml_ReturnsNetwork()
        {
            const string xml = @"<kml xmlns=""http://www.opengis.net/kml/2.2""><Document/></kml>";
            Assert.That(Detect(xml), Is.EqualTo(KlicKind.Network));
        }

        [Test]
        public void DetectKind_GmlFeatureCollection_ReturnsNetwork()
        {
            const string xml = @"<gml:FeatureCollection xmlns:gml=""http://www.opengis.net/gml/3.2""/>";
            Assert.That(Detect(xml), Is.EqualTo(KlicKind.Network));
        }

        [Test]
        public void DetectKind_CityGmlModel_ReturnsNotNetwork()
        {
            const string xml = @"<core:CityModel xmlns:core=""http://www.opengis.net/citygml/2.0""/>";
            Assert.That(Detect(xml), Is.EqualTo(KlicKind.NotNetwork));
        }

        [Test]
        public void DetectKind_PlainXml_ReturnsNotNetwork()
        {
            const string xml = @"<root xmlns=""urn:example:whatever""><child/></root>";
            Assert.That(Detect(xml), Is.EqualTo(KlicKind.NotNetwork));
        }
    }
}
