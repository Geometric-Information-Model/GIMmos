using GIMcompress;
using GIMcompress.Shrinkwrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMcompress.Shrinkwrap
{
    public class GimPolygon2d
    {
        private List<List<Vertex2d>> innerBoundaries = new();
        private List<Vertex2d> outerBoundary = new();

        private Vertex2d calculatedCenter = null;

        #region properties

        public List<List<Vertex2d>> InnerBoundaries
        {
            get { return innerBoundaries; }
            set { innerBoundaries = value; }
        }

        public List<Vertex2d> OuterBoundary
        {
            get { return outerBoundary; }
            set { outerBoundary = value; }
        }

        public Vertex2d Center
        {
            get
            {
                if (calculatedCenter == null)
                {
                    calculatedCenter = GetCenter();
                }

                return calculatedCenter;
            }
        }

        #endregion

        public GimPolygon2d(List<Vertex2d> outerBoundary, List<List<Vertex2d>> innerBoundaries)
        {
            this.outerBoundary = outerBoundary;
            this.innerBoundaries = innerBoundaries;
        }

        // first boundary is outerBoundary, the rest are innerBoundaries
        public GimPolygon2d(List<List<Vertex2d>> allBoundaries)
        {
            foreach (Vertex2d point in allBoundaries[0])
            {
                outerBoundary.Add(point);
            }

            for (int i = 1; i < allBoundaries.Count; i++)
            {
                List<Vertex2d> innerBoundary = new List<Vertex2d>();
                foreach (Vertex2d point in allBoundaries[i])
                {
                    innerBoundary.Add(point);
                }
                innerBoundaries.Add(innerBoundary);
            }

        }

        private Vertex2d GetCenter()
        {
            Vertex2d center = new(0, 0);

            foreach (Vertex2d point in outerBoundary)
            {
                center += point;
            }
            int pointCount = outerBoundary.Count;
            foreach (var innerBoundary in innerBoundaries)
            {
                foreach (Vertex2d point in innerBoundary)
                {
                    center += point;
                }
                pointCount += innerBoundary.Count;
            }

            center /= pointCount;
            return center;
        }


        public static void ToDesmosFileWithTypes(List<GimPolygon2d> polygons, List<string> types, string fileNameKeywords = "")
        {
            StringBuilder desmosHtmlString = new("// START" + "\n");
            int index = 0;
            foreach (var polygon in polygons)
            {
                index++;
                string xValuesString = "";
                string yValuesString = "";

                for (int i = 0; i < polygon.OuterBoundary.Count; i++)
                {
                    xValuesString += polygon.OuterBoundary[i].X.ToString("0.00");
                    yValuesString += polygon.OuterBoundary[i].Y.ToString("0.00");

                    xValuesString += ",";
                    yValuesString += ",";
                }
                

                xValuesString += polygon.OuterBoundary[0].X;
                yValuesString += polygon.OuterBoundary[0].Y;

                //bool reverse = true;
                //foreach(var innerBoundary in polygon.InnerBoundaries)
                //{
                //    if (reverse) innerBoundary.Reverse();
                //    reverse = !reverse;

                //    for (int i = 0; i < innerBoundary.Count; i++)
                //    {
                //        xValuesString += innerBoundary[i].X;
                //        yValuesString += innerBoundary[i].Y;
                //        if (i < (innerBoundary.Count - 1))
                //        {
                //            xValuesString += ",";
                //            yValuesString += ",";
                //        }
                //    }
                //    xValuesString += innerBoundary[0].X;
                //    yValuesString += innerBoundary[0].Y;
                //    xValuesString += ",\\frac{0}{0}";
                //    yValuesString += ",\\frac{0}{0}";
                //}

                string type = types[index - 1];

                string colorHex;

                switch (type.ToLower())
                {

                    case "bridge": colorHex = "AA4A44"; break;
                    case "building": colorHex = "c16464"; break;
                    case "genericcityobject": colorHex = "A0A0A0"; break;
                    case "landuse": colorHex = "69f7a6"; break;
                    case "plantcover": colorHex = "228b22"; break;
                    case "road": colorHex = "8c8eab"; break;
                    case "waterbody": colorHex = "1ca3ec"; break;
                    default: colorHex = "000000";break;
                }

                string polygonString = $"type: 'table', columns:[" +
                    $"{{ latex: 'x_{{index}}', values: [{xValuesString}], lines: true, points: false, color: '#{colorHex}'}}," +
                    $"{{ latex: 'y_{{index}}', values: [{yValuesString}], lines: true, points: false, color: '#{colorHex}'}}" +
                    $"]";
                string codeLine = "calculator.setExpression({" + polygonString + "});";

                desmosHtmlString.AppendLine(codeLine);

            }

            desmosHtmlString.AppendLine("//END");

            string HtmlFileContent = "<html> <head> <title> Desmos Applet Template </title> </head>"
                + " <script src = \"https://www.desmos.com/api/v1.7/calculator.js?apiKey=dcb31709b452b1cf9dc26972add0fda6\" ></script > "
                + "<body> <div id = \"calculator\" style = \"width: 100%; height: 100%\"> </div>  </body> <script> "

                + "	var elt = document.getElementById('calculator'); var calculator = Desmos.GraphingCalculator(elt); \n"

                + desmosHtmlString.ToString()

                + "</script> </html> "

                ;


            DateTime dateNow = DateTime.Now;
            string fileName = String.Format("Polygon Visualisation " + dateNow.ToString("d-M") + " at " + dateNow.ToString("h-m") + " total {1} polygons", dateNow, polygons.Count);
            fileName += fileNameKeywords + ".Html";
            string filePath = System.IO.Path.GetTempPath() + "GIM\\";
            if (!System.IO.Directory.Exists(filePath))
                System.IO.Directory.CreateDirectory(filePath);

            System.IO.File.WriteAllTextAsync(filePath + fileName, HtmlFileContent);

        }


    }
}
