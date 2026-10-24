using GIMcompress;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using GimUtilities;
using GIMtransfer.BrepToPoly;

namespace GIMtransfer
{
    public class GMLGimData
    {
        private XDocToVectList xElementsToVertList = new XDocToVectList();
        public (GimData, List<Vertex>) XElementToLines(XElement exterior, GimData gimData, string dimension, List<Vertex> allVertexes, uint mainFaceIndex)
        {
            
            List<Vect> vectListAll = new();
            XDocToVectList xDocToVectList = new XDocToVectList();
            List<Vertex> vertexes = new();
            vertexes = (xDocToVectList.ConvertXElementsToVertList(exterior, dimension));
            List<Vect> vectList = new List<Vect>();
            foreach (Vertex vert in vertexes)
            {
                Vect vect = new(vert.x, vert.y, vert.z);
                vectList.Add(vect);

            }
            uint parentPolyIndex = (uint)gimData.Elements.Count;
            GimElement exteriorParentPolygon = new()
            {
                ElementType = GimElementType.wire,
            };
            gimData.Elements.Add(exteriorParentPolygon);
            gimData.AddChildToParent(parentPolyIndex, mainFaceIndex);



            foreach (Vertex x in vertexes)
            {
                allVertexes.Add(x);

            }
            int counter = 0;
            
            double[] Origin = new double[3];
            List<Vect> vectListTemp = new List<Vect>();
            foreach (Vertex vertex in vertexes)
            {
              
                if (vectListTemp.Count == 1) {
                    Origin[0] = vectListTemp[0][0];
                    Origin[1] = vectListTemp[0][1];
                    Origin[2] = vectListTemp[0][2];
                }
                counter++;

                Vect point = new(vertex.x, vertex.y, vertex.z);
               


                vectListTemp.Add(point);
                vectListAll.Add(point);
                uint lineIndex = (uint)gimData.Elements.Count;
                double[] doubles = { vertex.x, vertex.y, vertex.z };
                allVertexes.Add(vertex);
                GimLogger.Info(vectListTemp.Count);
                if (counter == vertexes.Count && vectListTemp.Count == 1) {
                    Vect point2 = new(vertexes[0].x, vertexes[0].y, vertexes[0].z);
                    vectListTemp.Add(point2);
                
                }
                if (vectListTemp.Count == 2)
                {

                   
                    GimLogger.Info("2 vects added, adding line...");
                    
                    Vect direction = (vectListTemp[1] - vectListTemp[0]).Direction;


                    double xDif = (vectListTemp[0][0] - vectListTemp[1][0]) * (vectListTemp[0][0] - vectListTemp[1][0]);
                    double yDif = (vectListTemp[0][1] - vectListTemp[1][1]) * (vectListTemp[0][1] - vectListTemp[1][1]);
                    double zDif = (vectListTemp[0][2] - vectListTemp[1][2]) * (vectListTemp[0][2] - vectListTemp[1][2]);

                    double length = Math.Sqrt(xDif + yDif + zDif);
                    // This is a better way to calculate the distance of the vector.
                    //double length = direction.Norm;
                    double[] ElementDoubles = new double[2];

                    //0
                    ElementDoubles[0] = 0; // setting the start param   
                                           //length
                    ElementDoubles[1] = length; // setting the end param
                  
                    lineIndex = (uint)gimData.Elements.Count;
                    GimElement line = new()
                    {
                        ElementType = GimElementType.C_line,
                        ElementDoubles = ElementDoubles


                    };

                    gimData.Elements.Add(line);
                    gimData.AddChildToParent(lineIndex, parentPolyIndex);
                    line.Locals[0].Px = vectListTemp[0][0];
                    line.Locals[0].Py = vectListTemp[0][1];
                    line.Locals[0].Pz = vectListTemp[0][2];


                    direction.ApplyToLocal(line.Locals[0]);
                    // applying the direction

                    vectListTemp.RemoveAt(0); // Remove the oldest vector
               

                }
                
                    //Origin[0] = vertex.x;
                    //Origin[1] = vertex.y;
                    //Origin[2] = vertex.z;

            }

            //GimLogger.Info("String ding: ", GimPolygon.PointListToString(vectListAll));
            return (gimData, allVertexes);
        }
        public GimData SetupGMLGimdataStructure()
        {
            GimData gimData = new GimData();

            //Creates the gim elements and gives them the right type
            gimData.CreateRootIfNotExists();
            GimElement gimPart = new();
            GimElement gimFolder = new();
            GimElement gimBrep = new();
            GimElement gimSolid = new();
            GimElement gimShell = new();
          

            //Adds the element type.
            gimFolder.ElementType = GimElementType.D_folder;
            gimPart.ElementType = GimElementType.D_part;
            gimBrep.ElementType = GimElementType.R_BRep;
            gimSolid.ElementType = GimElementType.B_solid;
            gimShell.ElementType = GimElementType.shell;

            //Adds the elements to gimData
            uint folderIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(gimFolder);
            gimData.AddChildToParent(folderIndex, 0);

            uint partIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(gimPart);
            gimData.AddChildToParent(partIndex, folderIndex);

            uint polyIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(gimBrep);
            gimData.AddChildToParent(polyIndex, partIndex);


            uint solidIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(gimSolid);
            gimData.AddChildToParent(solidIndex, polyIndex);
            uint shellIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(gimShell);
            gimData.AddChildToParent(shellIndex, solidIndex);

            return gimData;
        }
        public string GetGMLDimension(List<XElement> allXElements)
        {
            string value = "0";
            foreach (XElement x in allXElements)
            {
                try
                {
                    if (x.Attribute("srsDimension") != null && x.Attribute("srsDimension").Value != null)
                    {
                        value = x.Attribute("srsDimension").Value;
                        break;
                    }
                }
                catch
                {

                }
            }
            return value;
        }

        public GimData AddAllPointsToGimData(List<XElement> points, GimData gimData, string dimension)
        {

            uint pointCloudIndex = (uint)gimData.Elements.Count;
            GimElement pointCloud = new()
            {
                ElementType = GimElementType.P_vertex


            };
            gimData.Elements.Add(pointCloud);

            foreach (XElement point in points)
            {
                List<Vertex> vertices = xElementsToVertList.ConvertXElementsToVertList(point, dimension);

                double[] pointDoubles = new double[]
                {
                    vertices[0].x,
                    vertices[0].y,
                    vertices[0].z
                };

                GimElement gimPoint = new()
                {
                    ElementType = GimElementType.P_vertex,
                    ElementDoubles = pointDoubles
                };
                uint pointIndex = (uint)gimData.Elements.Count;
                gimData.Elements.Add(gimPoint);
                gimData.AddChildToParent(pointIndex, pointCloudIndex);
                GimLogger.Info("Point added");

            }

            return gimData;
        }
        public GimData ExteriorInteriorToGimData(XDocument xdoc, GimData gimData, string dimension)
        {
            int MultiplySize = 1;
            uint partIndex = 5;
            XDocToVectList xDocToVectList = new XDocToVectList();
            XNamespace GMLNamespace = "http://www.opengis.net/gml/3.2";
            GMLReader gmlReader = new GMLReader();
            List<XElement> polygons = gmlReader.GetPolygons(xdoc, GMLNamespace);
            List<XElement> KMLcoordinates = gmlReader.GetKMLCoordinates(xdoc);
            List<Vertex> allVertexes = new();
          
            GimLogger.Info(polygons.Count + " polygons found!");
            double[] defaultDoubles = { 0, 0, 0 };
            int[] defaultInts = { 0, 0, 0, 0, 0, 0, 0 };
            uint mainFaceIndex = 0;
            //Runs the amount of times there are polygons, so it adds all the exteriors and interiors

            //For KML, not fully supported
            if (KMLcoordinates.Count > 0)
            {

                foreach (XElement coordinate in KMLcoordinates)
                {
                    mainFaceIndex = (uint)gimData.Elements.Count;
                    GimElement mainFace = new()
                    {
                        ElementType = GimElementType.F_plane
                    };
                    gimData.Elements.Add(mainFace);
                    gimData.AddChildToParent(mainFaceIndex, partIndex);
                    uint WireIndex = (uint)gimData.Elements.Count;
                    GimElement wire = new()
                    {
                        ElementType = GimElementType.wire
                    };
                    gimData.Elements.Add(wire);
                    gimData.AddChildToParent(WireIndex, mainFaceIndex);

                    List<Vertex> vertexes = new();

                    vertexes = (xDocToVectList.ConvertXElementsToVertList(coordinate, dimension));
                    int counter = 0;
                    foreach (Vertex vertex in vertexes)
                    {
                        double[] Origin = new double[2];
                        counter++;
                        List<Vect> vectListTemp = new List<Vect>();
                        Vect point = new(vertex.x, vertex.y, vertex.z);


                        vectListTemp.Add(point);
                        uint lineIndex = (uint)gimData.Elements.Count;
                        double[] doubles = { vertex.x, vertex.y, vertex.z };
                        allVertexes.Add(vertex);
                        GimLogger.Info("Vertex added");

                        if (counter % 2 == 0)
                        {
                            counter = 0;

                            Vect direction = (vectListTemp[0] - vectListTemp[1]).Direction;

                            double xDif = (vectListTemp[0][0] + vectListTemp[1][0]) * (vectListTemp[0][0] + vectListTemp[1][0]);
                            double yDif = (vectListTemp[0][1] + vectListTemp[1][1]) * (vectListTemp[0][1] + vectListTemp[1][1]);
                            double zDif = (vectListTemp[0][2] + vectListTemp[1][2]) * (vectListTemp[0][2] + vectListTemp[1][2]);

                            double length = Math.Sqrt(xDif + yDif + zDif);
                            double[] ElementDoubles = new double[2];
                            ElementDoubles[0] = 0; // setting the start param
                            ElementDoubles[1] = length;// setting the end param

                            GimElement line = new()
                            {
                                ElementType = GimElementType.C_line,
                                elementDoubles = ElementDoubles


                            };
                            line.Locals.Add(new());
                            line.Locals[0].Px = Origin[0]; // setting the origin point
                            line.Locals[0].Py = Origin[1];
                            line.Locals[0].Pz = Origin[2];
                            lineIndex = (uint)gimData.Elements.Count;
                            gimData.Elements.Add(line);
                            direction.ApplyToLocal(line.Locals[0]); // applying the direction
                            gimData.AddChildToParent(lineIndex, WireIndex);
                            vectListTemp.Clear();

                        }
                        else
                        {
                            Origin[0] = vertex.x;
                            Origin[1] = vertex.y;
                            Origin[2] = vertex.z;

                        }

                    }


                }
            }
            //For normal gml files
          
            else
            {



                for (int i = 0; i < polygons.Count; i++)
                {
                    //if (i < 44) { continue; }
                    //makes the polygon its gonna add the exteriors/interiors to
                    mainFaceIndex = (uint)gimData.Elements.Count;
                    GimElement mainFace = new()
                    {
                        ElementType = GimElementType.F_plane
                    };

                    gimData.Elements.Add(mainFace);
                    gimData.AddChildToParent(mainFaceIndex, partIndex);

                    //gets the interiors and exteriors
                    List<XElement> exteriors = new(gmlReader.GetExterior(polygons[i], GMLNamespace));
                    List<XElement> interiors = new(gmlReader.GetInterior(polygons[i], GMLNamespace));

                    //goes trough the exteriors and adds them to gimdata
                    foreach (XElement exterior in exteriors)
                    {

                        (gimData, allVertexes) = XElementToLines(exterior, gimData, dimension, allVertexes, mainFaceIndex);




                    }
                    //Goes trough the interiors and adds them to gimdata
                    foreach (XElement interior in interiors)
                    {

                        (gimData, allVertexes) = XElementToLines(interior, gimData, dimension, allVertexes, mainFaceIndex);




                    }
                    //Removes empty faces
                    if (mainFace.Elements.Count > 0)
                    {
                        int wireIndex = (int)mainFace.Elements[0];
                        List<uint> wireIndexes = new List<uint>();
                        foreach (uint element in mainFace.Elements)
                        {
                            if (gimData.Elements[(int)element].ElementType == GimElementType.wire && gimData.Elements[(int)element].Elements.Count < 3)
                            {
                                wireIndexes.Add(element);

                            }
                        }
                        if (wireIndexes.Count > 0)
                        {
                            //gimData.RemoveElementsAndTheirChildren(wireIndexes);
                            foreach (uint wire in wireIndexes)
                            {
                                uint newWireindex = (uint)gimData.elements.Count;
                                GimElement firstWire = gimData.Elements[(int)gimData.Elements[(int)wire].Elements[0]];
                                gimData.AddChildToParent(newWireindex, wire);
                                GimLogger.Info("Face rounded");
                            }

                        }
                        if (mainFace.Elements.Count == 0) { gimData.RemoveElementsAndTheirChildren(new() { (uint)gimData.Elements.IndexOf(mainFace) }); }
                    }
                    else
                    {
                        gimData.RemoveElementsAndTheirChildren(new() { (uint)gimData.Elements.IndexOf(mainFace) });

                    }
                }



                double[] offsets = xElementsToVertList.FindOffset(allVertexes, dimension);

                GimLocal offset1 = new()
                {
                    px = offsets[0],
                    py = offsets[1],
                    pz = offsets[2]
                };
                GimLogger.Info(offsets[0]);
                gimData.Elements[1].Locals.Add(offset1);
                if (gimData.Elements[1].Locals.Contains(offset1))
                {
                    GimLogger.Info("Offset Toegevoegd");
                }
                //Applies offsets
                foreach (GimElement element in gimData.Elements)
                {

                    if (element.elementType == GimElementType.C_line)
                    {
                        element.Locals[0].Px -= offsets[0];


                        element.Locals[0].Py -= offsets[1];

                        element.Locals[0].Pz -= offsets[2];

                        element.Locals[0].Px *= MultiplySize;
                        element.Locals[0].Py *= MultiplySize;
                        element.Locals[0].Pz *= MultiplySize;
                        element.ElementDoubles[1] *= MultiplySize;


                    }

                }



            }
         
            return gimData;
        }
    }
}