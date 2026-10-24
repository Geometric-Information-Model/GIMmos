//using CityJson;
//using GIMcompress;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using static StartCityJson.GimProperties;
////using static StartCityJson.LanguageList;
//using FileAccess = GIMtransfer.FileAccess;
//namespace StartCityJson
//{
//    internal class ReadCityJsonOld
//    {
//        internal static List<CityJsonPlot> Plots = new List<CityJsonPlot>();
//        internal static List<CityJsonPlotGroup> PlotGroups = new List<CityJsonPlotGroup>();
//        internal static List<CityJsonObject> Objects = new List<CityJsonObject>();
//        //internal static CityJsonDatabaseLanguages[] Languages = StartCityJson.LanguageList.Languages.CityJsonDatabaseLanguages();
//        internal static List<GimProperty> GimProperties = StartCityJson.GimProperties.Properties.GimProperties();
        //private static void getCityObjectsOld(JProperty jObject, List<double[]> vertices)
        //{
        //    int id = 0;
        //    foreach (JProperty jFragmentA in jObject.Values())
        //    {
        //        GimData gimData = new GimData();
        //        //List<Tuple<string, string>> properties = new List<Tuple<string, string>>();
        //        List<CityJsonProperty> objectProperties = new List<CityJsonProperty>();
        //        int minX = 0, maxX = 0, minY = 0, maxY = 0, minZ = 0;
        //        string objectType = "";
        //        foreach (JProperty jObjectOrType in jFragmentA.Values())
        //        {
        //            if (jObjectOrType.Name.Equals("geometry"))
        //            {
        //                gimData.elements.Add(new GimElement() { ElementType = GimElementType.D_root });

        //                foreach (JContainer jFragmentC in jObjectOrType.Children())
        //                {
        //                    foreach (JProperty jBoundary in jFragmentC.Values())
        //                    {
        //                        if (jBoundary.Name.Equals("boundaries"))
        //                        {
        //                            int e = 0;
        //                            List<int> list = new List<int>();
        //                            List<int> elementInts = new List<int>();
        //                            List<double> elementDoubles = new List<double>();
        //                            foreach (JArray jPolyMesh in jBoundary.Values())
        //                            {
        //                                if (jPolyMesh.Children().Count() > 1) // building
        //                                {
        //                                    foreach (JArray jTriagle in jPolyMesh.Values())
        //                                    {
        //                                        foreach (JValue jVertice in jTriagle.Values())
        //                                        {
        //                                            SetValues(vertices, ref minX, ref maxX, ref minY, ref maxY, ref minZ, list, elementInts, elementDoubles, jVertice);
        //                                        }
        //                                    }
        //                                }
        //                                else // land surface
        //                                {
        //                                    foreach (JValue jVertice in jPolyMesh.Values())
        //                                    {
        //                                        SetValues(vertices, ref minX, ref maxX, ref minY, ref maxY, ref minZ, list, elementInts, elementDoubles, jVertice);
        //                                    }
        //                                }
        //                            }
        //                            if (minX == 0 || minY == 0)
        //                            {
        //                                minX = 0;
        //                            }
        //                            // calculate the centre and move all points
        //                            double cenX = (minX + maxX) / 2;
        //                            double cenY = (minY + maxY) / 2;
        //                            // TODO: rotate to make the longest horizontal line pure-X
        //                            List<double> points = new List<double>();
        //                            int i = 0;
        //                            foreach (double d in elementDoubles)
        //                            {
        //                                // multiply by scale
        //                                if (i == 0) points.Add(Math.Round(d - cenX, 3));
        //                                if (i == 1) points.Add(Math.Round(d - cenY, 3));
        //                                if (i == 2) { points.Add(Math.Round(d, 3)); i = -1; }
        //                                i++;
        //                            }
        //                            // add the part with the correct local at the centre of the object
        //                            e = gimData.elements.Count();
        //                            gimData.Elements.Add(new GimElement // add new part
        //                            {
        //                                ElementType = GimElementType.D_part,
        //                                Elements = new List<uint> { (uint)(e + 1) },
        //                                Locals = new List<GimLocal> {
        //                                    new GimLocal
        //                                    {
        //                                        parent = (uint)(e - 1),
        //                                        Px = 0,
        //                                        Py = 0,
        //                                        Pz = 0
        //                                    }
        //                                }
        //                            });
        //                            // convert RD to Latitude and Longitude
        //                            int GimLong = ReadCityJson.RdToLong(cenX, cenY);
        //                            int GimLat = ReadCityJson.RdToLat(cenX, cenY);

        //                            gimData.Elements[0].elements.Add((uint)e); // add child to the root
        //                            gimData.Objects.Add(new GimObject // add part as new object
        //                            {
        //                                element = (uint)e,
        //                                name = jFragmentA.Name
        //                            });
        //                            // add the polymesh as child of the root
        //                            e++;
        //                            gimData.Elements.Add(new GimElement
        //                            {
        //                                ElementType = GimElementType.R_Poly,
        //                                Elements = new List<uint> { (uint)(e + 1) },
        //                                Locals = new List<GimLocal> { new GimLocal
        //                                {
        //                                    parent = (uint)(e - 1)
        //                                } }
        //                            });
        //                            // add new polygon as child of the polymesh
        //                            e++;
        //                            gimData.Elements.Add(new GimElement
        //                            {
        //                                ElementType = GimElementType.P_polygon,
        //                                ElementDoubles = points.ToArray(),
        //                                ElementInts = elementInts.ToArray(),
        //                                Locals = new List<GimLocal> { new GimLocal
        //                                {
        //                                    parent = (uint)(e - 1)
        //                                } }
        //                            });
        //                            //FileAccess.GimDataToJson(gimData, "0-" + id.ToString()); // save all GimData as .json
        //                            id++;
        //                        }
        //                    }
        //                }
        //            }
        //            else if (jObjectOrType.Name.ToString() == "type")
        //            {
        //                objectType = jObjectOrType.Value.ToString().ToLower();
        //            }
        //            else if (jObjectOrType.Name.ToString() == "attributes")
        //            {
        //                foreach (JProperty jFragmentE in jObjectOrType.Values())
        //                {
        //                    int propId = 0;
        //                    string name = jFragmentE.Name.ToString();
        //                    string value = jFragmentE.Value.ToString();
        //                    GimProperty item = GimProperties.FirstOrDefault(o => o.Name == name);
        //                    if (item == null) // new item
        //                    {
        //                        propId = GimProperties.Min(p => p.ID) - 1;
        //                        GimProperties.Add(new GimProperty { ID = propId, Name = name, Language = 38, Type = "CityJson_" + propId });
        //                    }
        //                    else { propId = item.ID; } // excisting item
        //                    objectProperties.Add(new CityJsonProperty
        //                    {
        //                        Property = propId,
        //                        ValueString = value.ToString()
        //                    });

        //                    //Tuple<ushort, byte[]> ByteValue = GimProperties(jFragmentE.Name.ToString(), jFragmentE.Value.ToString());
        //                    //properties.Add(new CityJsonProperty { Type = (ushort)ByteValue.Item1, Value = ByteValue.Item2 });
        //                }
        //            }
        //        }
        //        if (objectType == "building" || objectType == "genericcityobject" || objectType == "bridge")
        //        {
        //            int cenX = (int)Math.Floor((double)(minX + maxX) / 2);
        //            int cenY = (int)Math.Floor((double)(minY + maxY) / 2);
        //            JsonGimObject GIMjson = FileAccess.GimDataToJson(gimData, "null");
        //            string GIMbinary = Convert.ToBase64String(GIMjson.GIMbin);
        //            GIMjson.GIMbin = null;
        //            string GIMjsonString = JsonConvert.SerializeObject(GIMjson, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        //            //string GIMjsonString = Convert.ToBase64String(GIMjson);

        //            Objects.Add(new CityJsonObject
        //            {
        //                Name = gimData.Objects[0].name,
        //                //Plots = new List<int>(),
        //                Type = objectType,
        //                GimData = gimData,
        //                GimBin = GIMbinary,
        //                GimJson = GIMjsonString,
        //                Px = cenX,
        //                Py = cenY,
        //                Pz = 0,
        //                Properties = objectProperties.ToArray()
        //            });
        //        }
        //        else if (objectType == "road" || objectType == "waterbody" || objectType == "landuse" || objectType == "plantcover")
        //        {
        //            int cenX = (int)Math.Floor((double)(minX + maxX) / 2);
        //            int cenY = (int)Math.Floor((double)(minY + maxY) / 2);

        //            Plots.Add(new CityJsonPlot
        //            {
        //                Name = gimData.Objects[0].name,
        //                Objects = new List<int>(),
        //                Type = objectType,
        //                GimData = gimData,
        //                //GimBin = GimBin(gimData),
        //                //GimJson = JsonSerializer.Serialize(FileAccess.GimDataToJson(gimData, "null")),
        //                Px = cenX,
        //                Py = cenY,
        //                MinGimLat = ReadCityJson.RdToLat(minX, minY),
        //                MaxGimLat = ReadCityJson.RdToLat(maxX, maxY),
        //                MinGimLong = ReadCityJson.RdToLong(minX, minY),
        //                MaxGimLong = ReadCityJson.RdToLong(maxX, maxY),
        //                Properties = objectProperties.ToArray()
        //            });
        //        }
        //        else
        //        {
        //            int tt = 0;
        //        }
        //    }
        //    return;

        //    static void SetValues(List<double[]> vertices, ref int minX, ref int maxX, ref int minY, ref int maxY,
        //        ref int minZ, List<int> list, List<int> elementInts, List<double> elementDoubles, JValue jVertice)
        //    {
        //        int i = list.IndexOf((int)jVertice);
        //        if (i == -1) // new local vertice
        //        {
        //            elementDoubles.AddRange(vertices[(int)jVertice]);
        //            i = list.Count;
        //            list.Add((int)jVertice);
        //        }
        //        if (minX == 0 && maxX == 0)
        //        {
        //            minX = (int)Math.Floor(vertices[(int)jVertice][0]);
        //            maxX = (int)Math.Ceiling(vertices[(int)jVertice][0]);
        //            minY = (int)Math.Floor(vertices[(int)jVertice][1]);
        //            maxY = (int)Math.Ceiling(vertices[(int)jVertice][1]);
        //            minZ = (int)Math.Floor(vertices[(int)jVertice][2]);
        //        }
        //        else
        //        {
        //            minX = (int)Math.Floor(Math.Min(minX, vertices[(int)jVertice][0]));
        //            maxX = (int)Math.Ceiling(Math.Max(maxX, vertices[(int)jVertice][0]));
        //            minY = (int)Math.Floor(Math.Min(minY, vertices[(int)jVertice][1]));
        //            maxY = (int)Math.Ceiling(Math.Max(maxY, vertices[(int)jVertice][1]));
        //            minZ = (int)Math.Floor(Math.Min(minZ, vertices[(int)jVertice][2]));
        //        }
        //        elementInts.Add((int)i);
        //    }
        //}
        //internal static void FindObjectsPerPlot()
        //{
        //    //int objectID = 0;
        //    //foreach (CityJsonObject cityObject in Objects)
        //    //{
        //    //    int GimObjectLong = RdToLong(cityObject.Px, cityObject.Py);
        //    //    int GimObjectLat = RdToLat(cityObject.Px, cityObject.Py);
        //    //    int plotID = 0;
        //    //    List<int> checkPlots= new List<int>();
        //    //    foreach (CityJsonPlot cityPlot in Plots)
        //    //    {
        //    //        if (GimObjectLat > cityPlot.MinGimLat && GimObjectLat < cityPlot.MaxGimLat && GimObjectLong > cityPlot.MinGimLong && GimObjectLong < cityPlot.MaxGimLong)
        //    //        {
        //    //            // TODO @Anis: check if the Object is at the Plot
        //    //            // TODO @Anis: test this
        //    //            if (PlotContainsObject(cityPlot, cityObject))
        //    //            {
        //    //                AddObjectToPlot(cityObject, cityPlot, objectID);
        //    //                break; // add to only one plot
        //    //            }
        //    //            else checkPlots.Add(plotID); // don't add to this plot
        //    //        }
        //    //        plotID++;
        //    //    }
        //    //    if (plotID == Plots.Count) { string error = ""; }
        //    //    cityObject.GimBin = GimBin(cityObject.GimData); // the cityObject is done
        //    //    objectID++;
        //    //}
        //    foreach (CityJsonPlot cityPlot in Plots)
        //    {
        //        JsonGimObject GIMjson = FileAccess.GimDataToJson(cityPlot.GimData, "null");
        //        string GIMbinary = Convert.ToBase64String(GIMjson.GIMbin);
        //        GIMjson.GIMbin = null;
        //        string GIMjsonString = JsonConvert.SerializeObject(GIMjson, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        //        cityPlot.GimBin = GIMbinary; // the cityPlot is done
        //        cityPlot.GimJson = GIMjsonString;
        //    }
        //}
        //private static bool PlotContainsObject(CityJsonPlot cityPlot, CityJsonObject cityObject)
        //{
        //    // center the object around the plot's center
        //    Vect objectCenter = new Vect(cityObject.Px - cityPlot.Px, cityObject.Py - cityPlot.Py);

        //    List<Vect> plotPolygon = new();

        //    // build the plot polygon
        //    for (int i = 0; i < cityPlot.GimData.Elements.Count; i++)
        //    {
        //        GimElement elem = cityPlot.GimData.Elements[i];
        //        if (elem.ElementType == GimElementType.P_polygon)
        //        {
        //            foreach (int pointIndex in elem.ElementInts)
        //            {
        //                Vect point = new Vect(
        //                    elem.ElementDoubles[pointIndex * 3],
        //                    elem.ElementDoubles[pointIndex * 3 + 1],
        //                    elem.ElementDoubles[pointIndex * 3 + 2]
        //                    );
        //                plotPolygon.Add(point);
        //            }
        //        }
        //    }
        //    bool result = false;

        //    // counting the intersections of the point with the edges of the polygon, if the count is odd the point is inside
        //    int j = plotPolygon.Count() - 1;
        //    for (int i = 0; i < plotPolygon.Count(); i++)
        //    {
        //        if (plotPolygon[i][1] < objectCenter[1] && plotPolygon[j][1] >= objectCenter[1] || plotPolygon[j][1] < objectCenter[1] && plotPolygon[i][1] >= objectCenter[1])
        //        {
        //            if (plotPolygon[i][0] + (objectCenter[1] - plotPolygon[i][1]) / (plotPolygon[j][1] - plotPolygon[i][1]) * (plotPolygon[j][0] - plotPolygon[i][0]) < objectCenter[0])
        //            {
        //                result = !result;
        //            }
        //        }
        //        j = i;
        //    }
        //    List<Vect[]> triangles = new();

        //    for (int i = 0; i < cityPlot.GimData.Elements.Count; i++)
        //    {
        //        GimElement elem = cityPlot.GimData.Elements[i];
        //        if (elem.ElementType == GimElementType.P_polygon)
        //        {
        //            for (int triangleIndex = 0; triangleIndex < elem.ElementInts.Length / 3; triangleIndex++)
        //            {
        //                int pt1Index = elem.ElementInts[triangleIndex * 3];
        //                int pt2Index = elem.ElementInts[triangleIndex * 3 + 1];
        //                int pt3Index = elem.ElementInts[triangleIndex * 3 + 2];

        //                Vect point1 = new Vect(
        //                    elem.ElementDoubles[pt1Index * 3],
        //                    elem.ElementDoubles[pt1Index * 3 + 1]
        //                    );

        //                Vect point2 = new Vect(
        //                   elem.ElementDoubles[pt2Index * 3],
        //                   elem.ElementDoubles[pt2Index * 3 + 1]
        //                   );

        //                Vect point3 = new Vect(
        //                   elem.ElementDoubles[pt3Index * 3],
        //                   elem.ElementDoubles[pt3Index * 3 + 1]
        //                   );

        //                triangles.Add(new Vect[] { point1, point2, point3 });
        //            }
        //        }
        //    }

        //    foreach (Vect[] triangle in triangles)
        //    {
        //        double sign1, sign2, sign3;

        //        sign1 = pointSign(objectCenter, triangle[0], triangle[1]);
        //        sign2 = pointSign(objectCenter, triangle[1], triangle[2]);
        //        sign3 = pointSign(objectCenter, triangle[2], triangle[0]);

        //        bool hasPositive = (sign1 >= 0) || (sign2 >= 0) || (sign3 >= 0);
        //        bool hasNegative = (sign1 <= 0) || (sign2 <= 0) || (sign3 <= 0);

        //        // if the below condition is true, the point is contained in one of the triangles
        //        if (!(hasPositive && hasNegative))
        //        {
        //            return true;
        //        }

        //    }

        //    return false;
        //    return result;
        //}
        //private static double pointSign(Vect a, Vect b, Vect c)
        //{
        //    return (a[0] - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (a[1] - c[1]);
        //}
        //private static void AddObjectToPlot(CityJsonObject cityObject, CityJsonPlot cityPlot, int objectID)
        //{
        //    //GimReader reader = new GimReader();
        //    //GimData gdObject = reader.BytesToGimData(Convert.FromBase64String(cityObject.GimBin));
        //    //reader = new GimReader();
        //    //GimData gdPlot = reader.BytesToGimData(Convert.FromBase64String(cityPlot.GimBin));
        //    // todo: add cityObject to cityPlot via id-reference at correct location :)
        //    uint elementIndex = (uint)cityPlot.GimData.Elements.Count;
        //    GimElement gimElement = new GimElement()
        //    {
        //        elementType = GimElementType.ExternalReference,
        //        elementInts = new int[] { objectID },
        //        locals = new List<GimLocal>() { new GimLocal() {
        //            px = cityObject.Px - cityPlot.Px,
        //            py = cityObject.Py - cityPlot.Py,
        //            pz = cityObject.Pz
        //        } }
        //    };
        //    cityPlot.GimData.Elements.Add(gimElement);
        //    cityPlot.GimData.AddChildToParent(elementIndex, 0, 0);
        //    //cityObject.Px = 0; cityObject.Py = 0; // move out of the country
        //}

//    }
//}
