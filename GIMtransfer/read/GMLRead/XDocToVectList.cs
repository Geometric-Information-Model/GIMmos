using System;
using System.Collections.Generic;
using System.Xml.Linq;
using GIMtransfer.BrepToPoly;

namespace GIMtransfer
{
    public class XDocToVectList
    {
        private double[] offset = new double[3];
        private bool offsetFound = false;
        public List<Vertex> ConvertXElementsToVertList(XElement Xelement, string dimension)//Can we also use this for KML??
        {
            //Converts an element from gml in to a list of vertexes
           
            List<Vertex> gmlVertlist = new List<Vertex>();
            double value1 = 0; double value2 = 0; double value3 = 0;
            int counter = 1;
          //  string test = Xelement.Value;
            string[] seperatedNumbers = Xelement.Value.ToString().Trim().Split(' ', ',');
            
            foreach (string s in seperatedNumbers)
            {
                if (!string.IsNullOrWhiteSpace(s)) 
                {
                    try
                    {
                        if (counter == 1)
                        {
                            value1 = Double.Parse(s);
                            counter++;
                        }
                        else if (counter == 2)
                        {
                            value2 = Double.Parse(s);
                            counter++;
                            if (dimension != "3")
                            {
                                gmlVertlist.Add(new Vertex(
                               value1,
                               value2,
                               value3
                               ));
                                value1 = 0;
                                value2 = 0;
                                value3 = 0;
                                counter = 1;
                            }
                        }
                        else if (counter == 3)
                        {
                            value3 = Double.Parse(s);
                            counter++;
                            gmlVertlist.Add(new Vertex(
                               value1,
                               value2,
                               value3
                               ));

                            value1 = 0;
                            value2 = 0;
                            value3 = 0;
                            counter = 1;
                        }
                    }
                    catch
                    {
                        
                    }
                }
            }
            
            return gmlVertlist;
        }

        public XElement VertListXElement(List<Vertex> vertList, string elementName) 
        {   //Reconstructs VertexList in to XElement

            XElement xelement = new XElement(elementName);
                        
            foreach (Vertex i in vertList) { 
                xelement.Value += i.x.ToString()+" ";
                xelement.Value += i.y.ToString()+" ";
            }

            
            
            return xelement;
        }

        public double[] FindOffset(List<Vertex> vertList, string dimension)
        {   //finds the offset off a vertex list

            double highestX = 0;
            double highestZ = 0;
            double highestY = 0;
            double lowestX = int.MaxValue;
            double lowestY = int.MaxValue;
            double lowestZ = int.MaxValue;

            if (dimension != "3")
            {
                foreach (Vertex vert in vertList)
                {
                 
                    //Looks for the highest X and Y coordinates.
                    if (vert.x > highestX )
                    {
                        highestX = vert.x;
                    }
                    if (vert.y > highestY)
                    {
                        highestY = vert.y;
                    }//Looks for the lowest X and Y coordinates.
                    if (vert.y < lowestY )
                    {
                        lowestY = vert.y;
                    }
                    if (vert.x < lowestX)
                    {
                        lowestX = vert.x;
                    }
                }

                double[] offsets = { highestX, highestY, lowestX, lowestY };
                double offsetX = (offsets[0] + offsets[2]) / 2;
                double offsetY = (offsets[1] + offsets[3]) / 2;

                double[] calculatedOffsets = { offsetX, offsetY, 0 };

                offsetFound = true;
                offset = calculatedOffsets;

                return calculatedOffsets;
            }
            else
            {
                foreach (Vertex vert in vertList)
                {
                    //Looks for the highest X and Y and Z coordinates.
                    if (vert.x > highestX)
                    {
                        highestX = vert.x;
                    }
                    else if (vert.y > highestY)
                    {
                        highestY = vert.y;
                    }
                    else if (vert.z > highestZ)
                    {
                        lowestZ = vert.z;
                    }
                    //Looks for the lowest X and Y and Z coordinates.
                    else if (vert.x < lowestX)
                    {
                        lowestX = vert.x;
                    }
                    else if (vert.y < lowestY)
                    {
                        lowestY = vert.y;

                    }
                    else if (vert.z < lowestZ )
                    {
                        lowestZ = vert.z;
                    }
                }

                double[] offsets = { highestX, highestY, highestZ, lowestX, lowestY, lowestZ };
                double offsetX = (offsets[0] + offsets[3]) / 2;
                double offsetY = (offsets[1] + offsets[4]) / 2;
                double offsetZ = (offsets[2] + offsets[5]) / 2;

                double[] calculatedOffsets = { offsetX, offsetY, offsetZ };

                offsetFound = true;
                offset = calculatedOffsets;

                return calculatedOffsets;
            }
        }
    }
}