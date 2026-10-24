using GIMcompress;
using System;
using System.Collections.Generic;
using GIMtransfer;
using System.Xml.Linq;
using GIMtransfer.BrepToPoly;
using System.IO;

namespace GIMtransfer
{
    internal class ReadGMLToGimData
    {
        // Ali's review: 
        //1. this method is too large can be broken down into reusable components, please identify where the code can be modularized
        //2. the method is doing too many things, please identify where the code can be simplified
        //3. Use LINQ for Data Manipulation
        //4. XML Documentation: Use XML comments for public methods and classes
        //5. Future-Proofing: Instead of returning null for 3D files, consider throwing a NotSupportedException or handling it in a way that informs the user appropriately
        //6. Use PascalCase for method and class names, camelCase for local variables, and avoid abbreviations
        //7. Make GMLToGimData method async

        public static GimData GMLToGimData(string fileName)//Gml to gimdata 
        {
            //Defining classes 
            List<Vertex> vertList = new List<Vertex>();
            XNamespace GMLNamespace = "http://www.opengis.net/gml/3.2"; 
            XNamespace GMLNamespace2 = "http://www.opengis.net/gml";
            GMLReader gmlreader = new();
            Converter converter = new Converter();
            XDocument xdoc = converter.GetFromXmlFile(fileName);
            List<XElement> xElements = new(gmlreader.GetGMLElementCoords(xdoc, GMLNamespace, "posList"));
            List<XElement> points = new(gmlreader.GetGMLElementCoords(xdoc, GMLNamespace, "Point"));
            List<XElement> allxElements = new(gmlreader.getAllElements(xdoc, GMLNamespace));
            XDocToVectList xElementsToVertList = new XDocToVectList();

            GMLGimData GmlGimData = new GMLGimData();
            GimData gimData = GmlGimData.SetupGMLGimdataStructure();

            string dimension = "0";

            //Checks the dimension of the GML file (2d or 3d)
            if (!gmlreader.isItOldGML())
            {
                dimension = GmlGimData.GetGMLDimension(allxElements);
            }
            if (dimension != "0") {
                Console.WriteLine("Dimension is set to: " + dimension);
            }
            
         

            //For points

            //if (points.Count > 0)
            //{
            //    gimData = GmlGimData.AddAllPointsToGimData(points, gimData, dimension);
            //}

            //For polylines
            gimData = GmlGimData.ExteriorInteriorToGimData(xdoc, gimData, dimension);

            

            return gimData;
        }

        public static bool SupportsType(string filetype)
        {
            if (string.IsNullOrEmpty(filetype)) return false;
            // FileManagerInternal normalizes the dispatch fileType to lowercase; compare the same
            // way so a differently-cased extension in a direct call still routes.
            string type = filetype.ToLowerInvariant();
            return (type == "gml" || type == "kml");
        }
    }
}
