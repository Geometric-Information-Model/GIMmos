using Assimp.Configs;
using GIMcompress;
using GIMtransfer.BrepToPoly;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUI
{
    internal class BrepToPolyRepGML
    {
        public GimData BrepToPoly(GimData gimdata, List<Vertex> vertexes) {
            List<double> doubles = new List<double>();
            List<int> ints = new List<int>();
            GimElement polyRep = new() { 
                ElementType = GimElementType.R_Poly

            
            };
            GimElement polygon = new()
            {
                ElementType = GimElementType.P_polygon


            };
            GimElement brep = gimdata.Elements[2];
            uint index = 0;
            //looping trough coordinates, not the actual lines in the code

            foreach (GimElement element in gimdata.Elements) {
                if (element.ElementType == GimElementType.C_line) { 
                 doubles.Add(element.ElementDoubles[0]);
                 doubles.Add(element.ElementDoubles[1]);
                 doubles.Add(0);
                }
               

            }
          

            polygon.ElementDoubles = doubles.ToArray();

            for (int i = 0; i < polygon.ElementDoubles.Length; i+=3) {
                ints.Add(i);
            
            }
          
            polygon.ElementInts = ints.ToArray();


            Console.WriteLine(polygon.ElementDoubles.Length);
            uint partIndex = 2;
            uint repIndex = (uint)gimdata.Elements.Count;
            uint polyIndex = (uint)gimdata.Elements.Count +1;
            gimdata.Elements.Add(polyRep);
            gimdata.Elements.Add(polygon);
            gimdata.AddChildToParent(polyIndex, repIndex);
            gimdata.AddChildToParent(repIndex, 2);
            return gimdata;
        }
    }
}
