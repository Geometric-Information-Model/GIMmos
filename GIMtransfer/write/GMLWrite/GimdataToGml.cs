using System.Collections.Generic;
using System;
using System.Xml.Linq;
using GIMcompress;

namespace GIMtransfer
{
    class GimdataToGml
    {   //All the xml namespaces used in our GML files
        XNamespace core = "http://www.opengis.net/citygml/3.0";
        XNamespace con = "http://www.opengis.net/citygml/construction/3.0";
        XNamespace gml = "http://www.opengis.net/gml/3.2";
        XNamespace bldg = "http://www.opengis.net/citygml/building/3.0";

        public static bool SupportsType(string filetype)
        {
            return filetype == "gml";
        }

        private string CutCurveToCoords(GimElement curve, GimData gimdata)
        {
            //Currently used when converting b-rep -> gml. needs improvement.

            double[] points = CurveHelpers.getStartAndEndPointsOfCurve(curve, gimdata);

            Console.WriteLine(points.Length);
            return string.Join(" ", points);

        }
        public GimElement[] FindElementsInGimdata(GimElementType elementType, GimData gimdata)
        {
            //Simple function i made to find all the elements of a certain type in gimdata
            List<GimElement> FoundElements = new();
            foreach (GimElement element in gimdata.Elements)
            {
                if (element.ElementType == elementType)
                {
                    FoundElements.Add(element);
                }

            }
            return FoundElements.ToArray();

        }


        private bool ContainsInterior(GimData gimData, GimElement plane)
        {
            uint wires = 0;
            foreach (uint elementIndex in plane.Elements)
            {
                if (gimData.Elements[(int)elementIndex].ElementType == GimElementType.wire) { wires++; }

            }
            return wires > 1;

        }
        private string WireToCoordString(GimData gimData, GimElement wire)
        {
            //Cuts off curves, only works with step files converted from gml for now.
            string coords = " ";
            foreach (uint lineIndex in wire.Elements)
            {
                if (gimData.Elements[(int)lineIndex].ElementType == GimElementType.C_line)
                {
                    coords += (gimData.Elements[(int)lineIndex].Locals[0].Px).ToString() + " ";
                    coords += (gimData.Elements[(int)lineIndex].Locals[0].Py).ToString() + " ";
                    coords += (gimData.Elements[(int)lineIndex].Locals[0].Pz).ToString() + " ";
                }
                else
                {
                    coords += CutCurveToCoords(gimData.Elements[(int)lineIndex], gimData);

                }


            }

            coords += " ";


            return coords;


        }


        private XElement CreateExteriorOrInterior(string coords, bool ExteriorChosen)
        {
            //This goes in the Polygon. Automatically makes the correct structure
            XNamespace GMLNamespace = "http://www.opengis.net/gml/3.2";
            string name = ExteriorChosen ? "exterior" : "interior";

            XAttribute dimension = new XAttribute("srsDimension", "3");
            XElement ExteriorOrInterior = new XElement(GMLNamespace + name);
            XElement LinearRing = new XElement(GMLNamespace + "LinearRing");
            XElement posList = new XElement(GMLNamespace + "posList");
            posList.Add(dimension);
            posList.SetValue(coords);
            LinearRing.Add(posList);
            ExteriorOrInterior.Add(LinearRing);

            return ExteriorOrInterior;
        }

        private (XElement, bool) PlaneToGMLPolygon(GimData gimData, GimElement Plane)
        {
            //Works only if it contains wires, which contain lines, with coordinates in their px,py, and pz locals.
            //For now it just cuts off curves
            // TODO : This is the main thing to improve to support b-rep -> gml conversion
            XNamespace GMLNamespace = "http://www.opengis.net/gml/3.2";
            List<GimElement> wires = new List<GimElement>();
            foreach (uint elementIndex in Plane.Elements)
            {
                if (gimData.Elements[(int)elementIndex].ElementType == GimElementType.wire)
                {
                    wires.Add(gimData.Elements[(int)elementIndex]);
                }

            }
            XElement xPolygon = new XElement(GMLNamespace + "Polygon");




            for (int i = 0; i < wires.Count; i++)
            {
                if (i == 0)
                {
                    XElement xExterior = CreateExteriorOrInterior(WireToCoordString(gimData, wires[i]), true);
                    xPolygon.Add(xExterior);

                }
                else if (ContainsInterior(gimData, Plane))
                {
                    XElement xInterior = CreateExteriorOrInterior(WireToCoordString(gimData, wires[i]), false);
                    xPolygon.Add(xInterior);
                }
            }

            bool success = wires.Count > 0;
            return (xPolygon, success);
        }


        private XElement SetPolygonStructure(XElement xPoly)
        {
            //gives the polygon the proper structure
            XElement boundary = new XElement(core + "boundary");
            XElement roofSurface = new XElement(con + "RoofSurface");
            XElement lod2MultiSurface = new XElement(core + "lod2MultiSurface");
            XElement MultiSurface = new XElement(gml + "MultiSurface");
            XElement surfaceMember = new XElement(gml + "surfaceMember");

            surfaceMember.Add(xPoly);
            MultiSurface.Add(surfaceMember);
            lod2MultiSurface.Add(MultiSurface);
            roofSurface.Add(lod2MultiSurface);
            boundary.Add(roofSurface);


            return boundary;
        }
        private (XDocument, XElement) InitializeXDoc(XDocument xdoc)
        {
            // Define namespaces

            // Create the GML top structure
            var cityModel = new XElement(core + "CityModel",
                new XAttribute(XNamespace.Xmlns + "core", core),
                new XAttribute(XNamespace.Xmlns + "con", con),
                new XAttribute(XNamespace.Xmlns + "gml", gml),
                new XAttribute(XNamespace.Xmlns + "bldg", bldg),
                new XElement(gml + "boundedBy",
                    new XElement(gml + "Envelope",
                        new XAttribute("srsName", "EPSG:25832"),
                        new XAttribute("srsDimension", "3"),
                        new XElement(gml + "lowerCorner", "458900.0 5438785.0 113.5"),
                        new XElement(gml + "upperCorner", "458912.0 5438795.0 120.017691")
                    )
                )
            );

            // Create an XDocument
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                cityModel
            );



            return (document, cityModel);
        }

        private string FindCorrectPolyCoords(GimElement polygon, int i)
        {
            string coords = "";
            //Gets the correct elementdoubles based on the element ints.
            //Prevents crashing from going out of bounds, just takes the last coordinate. 
            coords = " " + (string.Join(" ", polygon.ElementDoubles[(polygon.ElementInts[i] * 3)..Math.Min(((polygon.ElementInts[i] * 3) + 3), polygon.ElementDoubles.Length)])) + " ";
            coords += (string.Join(" ", polygon.ElementDoubles[(polygon.ElementInts[Math.Min(i + 1, polygon.ElementInts.Length)] * 3)..Math.Min(((polygon.ElementInts[Math.Min(i + 1, polygon.ElementInts.Length)] * 3) + 3), polygon.ElementDoubles.Length)])) + " ";
            coords += (string.Join(" ", polygon.ElementDoubles[(polygon.ElementInts[Math.Min(i + 2, polygon.ElementInts.Length)] * 3)..Math.Min(((polygon.ElementInts[Math.Min(i + 2, polygon.ElementInts.Length)] * 3) + 3), polygon.ElementDoubles.Length)])) + " ";

            return coords;

        }
        private XElement[] PolyrepToPolygons(GimData gimdata)
        {
            List<XElement> Polygons = new List<XElement>();
            string coords = "";
            GimElement[] Polyrep = FindElementsInGimdata(GimElementType.R_Poly, gimdata);

            GimElement polygon = gimdata.Elements[(int)Polyrep[0].Elements[0]];

            //Finds and adds the correct coords to the polygon, also sets the correct structure
            for (int i = 0; i < polygon.ElementInts.Length; i += 3)
            {
                coords = FindCorrectPolyCoords(polygon, i);

                XElement polygonGML = new XElement(gml + "Polygon");
                polygonGML.Add(CreateExteriorOrInterior(coords, true));
                Polygons.Add(SetPolygonStructure(polygonGML));


            }


            return Polygons.ToArray();


        }

        public string GimDataToGML(GimData gimdata, string filePath)
        {
            XNamespace GMLNamespace = "http://www.opengis.net/gml/3.2";

            //Idk how to actually make options for saving files and everything, so just hard coded this for now
            // TODO : Change this to save files correctly

            XDocument xdoc = new XDocument();
            XElement root;
            //Initializes the GML file. 
            (xdoc, root) = InitializeXDoc(xdoc);
            XElement cityObjectMember = new XElement(core + "cityObjectMember");
            XElement building = new XElement(bldg + "Building");
            cityObjectMember.Add(building);
            root.Add(cityObjectMember);


            //If the gimdata contains a polyrep
            if (FindElementsInGimdata(GimElementType.R_Poly, gimdata).Length > 0)
            {
                XElement[] GMLPolygons = PolyrepToPolygons(gimdata);
                foreach (XElement Polygon in GMLPolygons)
                {
                    building.Add(Polygon);

                }
            }
            //If theres no polyrep, it tries to work with b-rep. (Only supported for step files converted from GML)
            // TODO : make b-rep -> gml file conversion possible! 
            //Already have quite alot of functions that may help in achieving this
            else
            {

                for (int i = 0; i < gimdata.Elements.Count; i++)
                {
                    if (gimdata.Elements[i].ElementType == GimElementType.F_plane)
                    {
                        (XElement xPoly, bool success) = PlaneToGMLPolygon(gimdata, gimdata.Elements[i]);
                        if (!success) { continue; }
                        XElement boundary = SetPolygonStructure(xPoly);
                        building.Add(boundary);



                    }

                }
            }

            //Saves the file
            string outputPath = filePath + ".gml";
            xdoc.Save(outputPath);

            //xdoc.Add(root);

            return outputPath;

        }


    }
}
 