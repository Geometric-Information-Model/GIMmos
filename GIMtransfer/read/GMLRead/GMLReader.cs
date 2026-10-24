using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using GimUtilities;

namespace GIMtransfer
{
    public class GMLReader
    {
        bool isOld;
        public List<XElement> GetGMLElementCoords(XDocument xdoc, XNamespace GMLnamespace, string Element)
        {//Gets gml coords from a gml file based on the element.
            List<XElement> XElementListPoly = new();
            XNamespace namespace2 = "http://www.opengis.net/gml";
            XNamespace kml = "http://www.opengis.net/kml/2.2";
            List<XElement> coordinates = xdoc.Descendants(namespace2 + "coordinates").ToList();
            List<XElement> KMLcoordinates = xdoc.Descendants(kml + "coordinates").ToList();
            List<XElement> pos = xdoc.Descendants(GMLnamespace + "pos").ToList();
            List<XElement> element = xdoc.Descendants(GMLnamespace + Element).ToList();
            KMLGimData kmlGimData = new KMLGimData();

            //Checks what the Element variable is and gets the corresponding coordinates
            if (Element == "Point" && element.Count > 0)
            {
                XElementListPoly = pos;
             

            }
            else if (element.Count > 0)
            {
                XElementListPoly = element;
              
            }
            else if (KMLcoordinates.Count > 0 && Element != "Point")
            {
                XElementListPoly = KMLcoordinates;
              
            }
            //Checks if the Namespace is an older version
            else if (coordinates.Count > 0 && Element == "posList" || pos.Count > 0 && Element == "posList")
            {
                if (coordinates.Count > 0)
                {
                    XElementListPoly = coordinates;
                   
                    isOldGML();
                }
                else
                {
                    XElementListPoly = pos;
                   
                  
                }
            }
            else
            {
                GimLogger.Info(Element + " Not found");
            }

            return XElementListPoly;
        }
        public List<XElement> GetKMLCoordinates(XDocument xdoc)
        {
            XNamespace kmlNamespace = "http://www.opengis.net/kml/2.2";
            List<XElement> Coordinates = xdoc.Descendants(kmlNamespace + "coordinates").ToList();
            return Coordinates;
        }
        public List<XElement> GetPolygons(XDocument xdoc, XNamespace GMLnamespace)
        {
            List<XElement> exterior = xdoc.Descendants(GMLnamespace + "exterior").ToList();
            List<XElement> polygons = new List<XElement>();
            foreach (XElement x in exterior) { 
                polygons.Add(x.Parent);
            }
            return polygons;
        }
        public List<XElement> GetExterior(XElement polygon, XNamespace GMLnamespace) {
            List<XElement> exterior = polygon.Descendants(GMLnamespace+"exterior").Descendants(GMLnamespace + "posList").ToList();
            return exterior;
        }
        public List<XElement> GetCoordinates(XDocument xdoc)
        {
            XNamespace gmlNamespace = "http://www.opengis.net/gml";
            List<XElement> Coordinates = xdoc.Descendants(gmlNamespace + "coordinates").ToList();
            return Coordinates;
        }
        public List<XElement> GetInterior(XElement polygon, XNamespace GMLnamespace)
        {
            List<XElement> interior = polygon.Descendants(GMLnamespace + "interior").Descendants(GMLnamespace+"posList").ToList();
            
            return interior;
        }
        public List<XElement> GetLineString(XDocument xdoc, XNamespace GMLnamespace) { 
            
            List<XElement> lineStrings = new List<XElement>();
            return xdoc.Descendants(GMLnamespace + "LineString").ToList();
        
        }
        public void isOldGML() //If the namespace is old, it's true
        {
            isOld = true;
        }
        public bool isItOldGML()
        {
            return isOld;
        }

        //Get's all the elements from a gml file
        public List<XElement> getAllElements(XDocument xdoc, XNamespace GMLNamespace)
        {
            List<XElement> XElementListPoly = new();
            XElementListPoly = xdoc.Descendants().ToList();

            return XElementListPoly;
        }
        
    }

}