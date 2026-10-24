using GIMcompress;
using System;


namespace GIMtransfer
{
    internal class AddKmlZValuesFromGimMaps
    {
        public static double[] getCenterKMLCoords(GimData kml)
        {
            double highestX = double.MinValue;
            double highestY = double.MinValue;
            double LowestX = double.MaxValue;
            double LowestY = double.MaxValue;

            foreach (GimElement element in kml.Elements)
            {
                try
                {
                    for (int i = 0; i < element.ElementDoubles.Length; i++)
                    {

                        //This is an X Value 
                        if (i % 3 == 0 || i == 0)
                        {

                            if (element.ElementDoubles[i] >= highestX)
                            {
                                highestX = element.ElementDoubles[i];
                            }
                            if (element.ElementDoubles[i] <= LowestX)
                            {
                                LowestX = element.ElementDoubles[i];
                            }

                        }
                        //This is an Y Value
                        else if (i % 3 == 1 || i == 1)
                        {

                            if (element.ElementDoubles[i] >= highestY)
                            {
                                   
                                highestY = element.ElementDoubles[i];
                                Console.WriteLine(highestY);
                            }
                            if (element.ElementDoubles[i] <= LowestY)
                            {
                                   
                                LowestY = element.ElementDoubles[i];
                                Console.WriteLine(LowestY);
                            }

                        }


                       
                    }
                }
                catch { }
            }
            
            double[] offset = { (LowestX+highestX)/2,(LowestY+highestY)/2};
            return offset;
        }
            public bool ContainsKmlElement(GimElement kmlElement, GimElement gimMapsElement) {
            //Initializing the variables
            double highestX = double.MinValue;
            double highestY = double.MinValue;
            double LowestX = double.MaxValue;
            double LowestY = double.MaxValue;

            //Going trough every element double to find the highest and lowest ones
            for (int i = 0; i < gimMapsElement.ElementDoubles.Length; i++)
            {

                //This is an X Value
                if (i % 3 == 1)
                {
                    
                    if (gimMapsElement.ElementDoubles[i] > highestX) {
                        highestX = gimMapsElement.ElementDoubles[i];
                    }
                    if (gimMapsElement.ElementDoubles[i] < LowestX)
                    {
                        LowestX = gimMapsElement.ElementDoubles[i];
                    }

                }
                //This is an Y Value
                if (i % 3 == 2)
                {
                    
                    if (gimMapsElement.ElementDoubles[i] > highestY)
                    {
                        highestY = gimMapsElement.ElementDoubles[i];
                    }
                    if (gimMapsElement.ElementDoubles[i] < LowestY)
                    {
                        LowestY = gimMapsElement.ElementDoubles[i];
                    }

                }

            }
            //If the polygon contains the kml element, give it the z value
        if (
            kmlElement.ElementDoubles[0] <= highestX &&
            kmlElement.ElementDoubles[0] >= LowestX &&
            kmlElement.ElementDoubles[1] <= highestY &&
            kmlElement.ElementDoubles[1] >= LowestY
            )
            {
                Console.WriteLine("Polygon contains the kml element!");
                return true;
            }
        else{
            
                return false;
            }
            
        }
        public static GimData ApplyKmlOffsets(GimData kmlGimdata) {

            double offsetX = kmlGimdata.Elements[1].Locals[1].Px;
            double offsetY = kmlGimdata.Elements[1].Locals[1].Py;

            foreach (GimElement element in kmlGimdata.Elements)
            {
                try
                {
                    if (element.ElementDoubles.Length > 0) { 
                        element.ElementDoubles[0] -=offsetX;
                        element.elementDoubles[1] -= offsetY;
                    
                    }
                }
                catch { 
                
                }
            
            }
            return kmlGimdata;
        }
        public static GimData AddKmlZValues(GimData kmlGimData, GimData GimMaps) {
            AddKmlZValuesFromGimMaps kmlGimMaps = new();
            

            foreach (GimElement element in kmlGimData.Elements) {
            
                try
                {
                    //Checks if the element has element doubles, and if so, if the Z is not already set
                    if (element.ElementDoubles.Length > 0 && element.ElementDoubles[2] == 0)
                    {
                        Console.WriteLine("Element doubles found");
                        //Goes trough the gimMaps polygons to figure out what the z value is
                        foreach (GimElement gimMapsElement in GimMaps.Elements)
                        {
                            //Checks if its a polygon
                            if (gimMapsElement.ElementType == GimElementType.P_polygon)
                            {
                                if(kmlGimMaps.ContainsKmlElement(element, gimMapsElement))
                                {
                                    //Gets the Z value of the polyon
                                    double z = gimMapsElement.ElementDoubles[2];
                                    if (element.ElementDoubles[2] == 0) { 
                                        element.elementDoubles[2] = z;
                                    
                                    }
                                    
                                    else if(z > element.ElementDoubles[2]){
                                        element.ElementDoubles[2] = z;
                                    }
                                    
                                }
                            }

                        }

                    }
                }
                catch {
                    Console.WriteLine("Element doubles not found");
                
                }
            }
            return kmlGimData;
          }





    }
}
