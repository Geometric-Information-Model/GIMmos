using System.Xml.Linq;
using GimUtilities;

namespace GIMtransfer
{
    public class Converter
    {
        //Loads GML File in to XDocument
        public XDocument GetFromXmlFile(string file)
        {
            GimLogger.Info(file);
            return XDocument.Load(file);
        }
    }
}