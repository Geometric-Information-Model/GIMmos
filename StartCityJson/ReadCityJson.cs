using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GIMcompress;
using FileAccess = GIMtransfer.FileAccess;
using GimBin;
//using GimCrypt;
//using System.Text.Json;
//using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Text;
using static StartCityJson.LanguageList;
using static StartCityJson.GimProperties;
using System.Linq.Expressions;
using StartCityJson;
using GIMcompress.Shrinkwrap;
using GimUtilities;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Drawing;
using GIMtransfer.BrepToPoly;
//using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CityJson
{
    public static class ReadCityJson
    {
        internal static List<ObjectDefinition> objectDefinitions = new List<ObjectDefinition>();
        internal static List<ObjectInstance> objectInstances = new List<ObjectInstance>();
        internal static object writeAccess = new object();

        public static bool createFiles = true;
        
        //internal static List<ObjectVisualisation> objectVisualisations = new List<ObjectVisualisation>();
        public static Dictionary<Vect, double> gridCoordinatesToAltitude = new();
        public static List<string> rvvCodes = new List<string>();

        public static int THREAD_COUNT = 20;
        public static int CONCURRENT_OBJECT_COUNT = 10;

        #region streaming
        public static void ReadCityJsonStream(string file, ref int definitionID, ref int instanceID, string regionID)
        {
            Stopwatch readSpeed = new();
            readSpeed.Start();

            using (FileStream fileStream = new FileStream(file, FileMode.Open, System.IO.FileAccess.Read))
            {
                string transformJson = ReadJsonStream.ReadJsonAttributeFromEndOfFileBuffered(fileStream, ReadJsonStream.ATTRIBUTE_TRANSFORM);
                Console.Write("reading verticies from file " + regionID + "... ");
                string verticesAndMetaDataJson = ReadJsonStream.ReadJsonAttributeFromEndOfFileBuffered(fileStream, ReadJsonStream.ATTRIBUTE_VERTICES);

                if (verticesAndMetaDataJson.Length <= 10)
                {
                    string somethingWrong = "debug";
                }

                // constructing vertices
                double[] transform;
                double[] scale;

                List<double[]> vertices = new List<double[]>();
                {
                    ParseVerticesAndTransform(verticesAndMetaDataJson, transformJson, out transform, out scale, vertices);
                }
                Console.WriteLine("Done!");

                verticesAndMetaDataJson = null;
                int cityObjectStartPosition = ReadJsonStream.FindAttributeStart(fileStream, ReadJsonStream.ATTRIBUTE_CITY_OBJECTS);

                byte[] buffer = new byte[1];
                int objectPosition = cityObjectStartPosition;

                bool moreObjects = true;
                bool objectNotEmpty = true;

                List<Task> tasks = new List<Task>();
                Console.WriteLine("Commencing reading objects and plots from file " + regionID + "... ");

                List<(string, int, int)> objectInfoHeap = new();
                do
                {
                    string objectJsonText = ReadJsonStream.ReadSingleJsonAttribute(fileStream, ref objectPosition);
                    if(objectJsonText != "")
                    {
                        objectNotEmpty = true;
                        // read async here
                        int currentInstanceID = instanceID;
                        int currentDefinitionID = definitionID;
                        if(tasks.Count > THREAD_COUNT)
                        {
                            Task.WaitAll(tasks.ToArray());
                            tasks = new();
                        }
                        objectInfoHeap.Add((objectJsonText, currentInstanceID, currentDefinitionID));
                        if(objectInfoHeap.Count > CONCURRENT_OBJECT_COUNT)
                        {
                            var currentHeap = objectInfoHeap;
                            Task task = Task.Run(() => {
                                for (int i = 0; i < currentHeap.Count; i++)
                                {
                                    string objTxt = currentHeap[i].Item1;
                                    int instID = currentHeap[i].Item2;
                                    int defID = currentHeap[i].Item3;
                                    ParseSingleObjectOrPlotFromText(objTxt, vertices, instID,
                                        defID, regionID);
                                }
                            });
                            tasks.Add(task);
                            objectInfoHeap = new();
                        }
                        instanceID++;
                        definitionID++;
                        objectPosition++;
                        fileStream.Seek(objectPosition, SeekOrigin.Begin);
                        fileStream.Read(buffer, 0, 1);

                        if ((char)buffer[0] == ',')
                        {
                            moreObjects = true;
                            objectPosition++;
                        }
                        else
                        {
                            moreObjects = false;
                        }
                    }
                    else
                    {
                        objectNotEmpty = false;
                    }

                } while (objectNotEmpty && moreObjects);

                // process residual files
                if (objectInfoHeap.Count > 0)
                {
                    var currentHeap = objectInfoHeap;
                    Task task = Task.Run(() => {
                        for (int i = 0; i < currentHeap.Count; i++)
                        {
                            string objTxt = currentHeap[i].Item1;
                            int instID = currentHeap[i].Item2;
                            int defID = currentHeap[i].Item3;
                            ParseSingleObjectOrPlotFromText(objTxt, vertices, instID,
                                defID, regionID);
                        }
                    });
                    tasks.Add(task);
                    objectInfoHeap = new();
                }

                Task.WaitAll(tasks.ToArray());
            }
            Console.WriteLine("Done!");
            
            readSpeed.Stop();
            long elapsed = readSpeed.ElapsedMilliseconds;
        }

        private static void ParseVerticesAndTransform(string verticesAndMetaDataJson, string transformJson,
            out double[] transform, out double[] scale, List<double[]> vertices)
        {

            var jVerticesAndMetaData = JsonConvert.DeserializeObject<JToken>(verticesAndMetaDataJson);
            var jTransformObject = JsonConvert.DeserializeObject<JToken>(transformJson);
            JObject jTransform = jTransformObject["transform"].Value<JObject>();

            transform = jTransform["translate"].Value<JArray>().ToObject<double[]>();
            scale = jTransform["scale"].Value<JArray>().ToObject<double[]>();

            JArray[] jVertices = jVerticesAndMetaData["vertices"].Value<JArray>().ToObject<JArray[]>();
            int index = 0;
            foreach (JArray jVertice in jVertices)
            {
                double[] vertice = jVertice.ToObject<double[]>();
                vertice[0] = vertice[0] * scale[0] + transform[0]; // X
                vertice[1] = vertice[1] * scale[1] + transform[1]; // Y
                vertice[2] = vertice[2] * scale[2] + transform[2]; // Z
                vertices.Add(vertice);

                index++;
            } // calculate the correct value of the vertices
        }

        private static void ParseSingleObjectOrPlotFromText(string objectJsonText, List<double[]> vertices,
            int currentInstanceID, int currentDefinitionID, string regionID)
        {
            JObject jObject = JObject.Parse(objectJsonText);
            GetSingleObjectOrPlot(jObject, vertices, currentInstanceID,
                currentDefinitionID, regionID);
        }

        private static void GetSingleObjectOrPlot(JObject jObject, List<double[]> vertices,
            int instanceID, int definitionID, string RegionID)
        {
            foreach (JProperty jFragmentA in jObject.Properties())
            {
                GimData gimData = new GimData();
                //List<Tuple<string, string>> properties = new List<Tuple<string, string>>();
                List<CityJsonProperty> objectProperties = new List<CityJsonProperty>();
                int minX = 0, maxX = 0, minY = 0, maxY = 0;
                double minZ = 0;
                double avgX = 0, avgY = 0;
                string objectType = "";
                string type = jFragmentA.Value["type"].ToString().ToLower();
                //if ((type != "building" && type != "genericcityobject" && type != "bridge"))
                //{
                //    continue;
                //}
                if (type == "waterbody")
                {
                    string breakpoint = "breakpoint";
                }
                foreach (JProperty jObjectOrType in jFragmentA.Values())
                {
                    if (jObjectOrType.Name.Equals("geometry"))
                    {
                        gimData.elements.Add(new GimElement() { ElementType = GimElementType.D_root });

                        foreach (JContainer jFragmentC in jObjectOrType.Children())
                        {
                            foreach (JProperty jBoundary in jFragmentC.Values())
                            {
                                if (jBoundary.Name.Equals("boundaries"))
                                {
                                    int e = 0;
                                    List<int> list = new List<int>();
                                    Dictionary<int, int> inverseList = new();
                                    List<int> elementInts = new List<int>();
                                    List<double> elementDoubles = new List<double>();
                                    avgX = 0; avgY = 0;
                                    int count = 0;
                                    foreach (JArray jPolyMesh in jBoundary.Values())
                                    {
                                        if (jPolyMesh.Children().Count() > 1) // building
                                        {
                                            foreach (JArray jTriagle in jPolyMesh.Values())
                                            {
                                                foreach (JValue jVertice in jTriagle.Values())
                                                {
                                                    SetValues(vertices, inverseList, ref minX, ref maxX, ref minY, ref maxY, ref minZ, list, elementInts, elementDoubles, jVertice);
                                                    avgX += vertices[(int)jVertice][0];
                                                    avgY += vertices[(int)jVertice][1];
                                                    count++;
                                                }
                                            }
                                        }
                                        else // land surface
                                        {
                                            foreach (JValue jVertice in jPolyMesh.Values())
                                            {
                                                SetValues(vertices, inverseList, ref minX, ref maxX, ref minY, ref maxY, ref minZ, list, elementInts, elementDoubles, jVertice);
                                                avgX += vertices[(int)jVertice][0];
                                                avgY += vertices[(int)jVertice][1];
                                                count++;
                                            }
                                        }
                                    }
                                    if (minX == 0 || minY == 0)
                                    {
                                        minX = 0;
                                    }
                                    if (count != 0)
                                    {
                                        avgX /= count;
                                        avgY /= count;
                                    }
                                    // calculate the centre and move all points
                                    double centerX = avgX;
                                    double centerY = avgY;
                                    // TODO: rotate to make the longest horizontal line pure-X
                                    List<double> points = new List<double>();
                                    int i = 0;
                                    foreach (double d in elementDoubles)
                                    {
                                        // multiply by scale
                                        if (i == 0) points.Add(d - centerX);
                                        if (i == 1) points.Add(d - centerY);
                                        if (i == 2) { points.Add(d - minZ); i = -1; }
                                        i++;
                                    }
                                    // add the part with the correct local at the centre of the object
                                    e = gimData.elements.Count();
                                    gimData.Elements.Add(new GimElement // add new part
                                    {
                                        ElementType = GimElementType.D_part,
                                        Elements = new List<uint> { (uint)(e + 1) },
                                        Locals = new List<GimLocal> {
                                            new GimLocal
                                            {
                                                parent = (uint)(e - 1),
                                                Px = 0,
                                                Py = 0,
                                                Pz = 0
                                            }
                                        }
                                    });
                                    // convert RD to Latitude and Longitude
                                    int GimLong = RdToLong(centerX, centerY);
                                    int GimLat = RdToLat(centerX, centerY);

                                    gimData.Elements[0].elements.Add((uint)e); // add child to the root
                                    gimData.Objects.Add(GimObjectFactory.Create((uint)e, jFragmentA.Name)); // add part as new object
                                    // add the polymesh as child of the root
                                    e++;
                                    gimData.Elements.Add(new GimElement
                                    {
                                        ElementType = GimElementType.R_Poly,
                                        Elements = new List<uint> { (uint)(e + 1) },
                                        Locals = new List<GimLocal> {
                                            new GimLocal
                                            {
                                                parent = (uint)(e - 1)
                                            }
                                        }
                                    });
                                    // add new polygon as child of the polymesh
                                    e++;
                                    gimData.Elements.Add(new GimElement
                                    {
                                        ElementType = GimElementType.P_polygon,
                                        ElementDoubles = points.ToArray(),
                                        ElementInts = elementInts.ToArray(),
                                        Locals = new List<GimLocal> { new GimLocal
                                        {
                                            parent = (uint)(e - 1)
                                        } }
                                    });
                                    //FileAccess.GimDataToJson(gimData, "0-" + id.ToString()); // save all GimData as .json
                                }
                            }
                        }
                    }
                    else if (jObjectOrType.Name.ToString() == "type")
                    {
                        objectType = jObjectOrType.Value.ToString().ToLower();
                    }
                    else if (jObjectOrType.Name.ToString() == "attributes")
                    {

                    }
                }
                //int cenX = (int)Math.Floor((double)(minX + maxX) / 2);
                //int cenY = (int)Math.Floor((double)(minY + maxY) / 2);
                //int cenZ = minZ;
                //string GIMjsonString = Convert.ToBase64String(GIMjson);


                double ry = 0;

                // we get the most probable rotation of the building here
                // only do this for buildings, not plots
                if ((type == "building" || type == "genericcityobject" || type == "bridge"))
                {
                    ry = GetPolyRepYawRotation(gimData);

                    // we apply the inverse of the rotation matrix to make the object align with the grid by default
                    Matrix reverseTransformation = Matrix.FromRPY(0, 0, -ry);

                    // we unrotate each vertex, one by one, to reset the object to a more neutral definition
                    foreach (GimElement element in gimData.Elements)
                    {
                        if (element.ElementType == GimElementType.P_polygon)
                        {
                            for (int i = 0; i + 2 < element.ElementDoubles.Length; i += 3)
                            {
                                Vect vertex = new Vect(
                                    element.ElementDoubles[i],
                                    element.ElementDoubles[i + 1],
                                    element.ElementDoubles[i + 2]
                                    );

                                Vect newVertex = reverseTransformation * vertex;

                                element.ElementDoubles[i] = Math.Round(newVertex[0], 3);
                                element.ElementDoubles[i + 1] = Math.Round(newVertex[1], 3);
                                element.ElementDoubles[i + 2] = Math.Round(newVertex[2], 3);

                            }
                        }
                    }
                }

                else // we look for grid points contained in this plot
                {
                    // we round up the min to nearest multiple of 100, e.g. 285 => 300
                    //double minGridX = Math.Ceiling((double)minX / 100) * 100;
                    //double minGridY = Math.Ceiling((double)minY / 100) * 100;

                    //double maxGridX = Math.Floor((double)maxX / 100) * 100;
                    //double maxGridY = Math.Floor((double)maxY / 100) * 100;

                    //List<Vect> gridPoints = new();

                    //for(double x = minGridX; x <= maxGridX; x+= 100)
                    //{
                    //    for(double y = minGridY; y <= maxGridY; y += 100)
                    //    {
                    //        gridPoints.Add(new(x, y));
                    //    }
                    //}

                    //List<Vect> gridPointsWithAltitude = GetGridPointZValuesFromPlot(gimData, avgX, avgY, minZ, gridPoints);

                    //foreach(Vect gridPoint in gridPointsWithAltitude)
                    //{
                    //    Vect key = new Vect(gridPoint[0], gridPoint[1]);
                    //    gridCoordinatesToAltitude[key] = gridPoint[2];
                    //}
                }


                int partIndex = 0;
                for (int i = 0; i < gimData.Elements.Count; i++)
                {
                    GimElement element = gimData.Elements[i];
                    if (element.ElementType == GimElementType.D_part)
                    {
                        partIndex = i;
                        break;
                    }
                }

                // adding color to definition based on type

                int materialIndex = gimData.Elements.Count;

                string materialColorHex = "";
                //            string waterHex = "1ca3ec";
                //            string landUseHex = "69f7a6";
                //            string roadHex = "8c8eab";
                //            string plantcoverHex = "228b22";

                //            string bridgeHex = "AA4A44";
                //            string buildingHex = "c16464";
                //            string genericcityobjectHex = "A0A0A0";

                switch (type.ToLower())
                {

                    case "bridge": materialColorHex = "AA4A44"; break;
                    case "building": materialColorHex = "c16464"; break;
                    case "genericcityobject": materialColorHex = "A0A0A0"; break;
                    case "landuse": materialColorHex = "69f7a6"; break;
                    case "plantcover": materialColorHex = "228b22"; break;
                    case "road": materialColorHex = "8c8eab"; break;
                    case "waterbody": materialColorHex = "1ca3ec"; break;

                }

                int color = int.Parse(materialColorHex, System.Globalization.NumberStyles.HexNumber);

                GimMaterial gimMaterial = new()
                {
                    BaseColour = color,
                    DiffuseColour = color,
                    SpecularColour = color,
                    AmbientColour = color,
                    ElementType = GimElementType.Material
                };

                gimData.Elements.Add(gimMaterial);
                gimMaterial.Locals.Add(new() { parent = (uint)partIndex });

                GimElement partElement = gimData.Elements[partIndex];
                partElement.Elements.Insert(0, (uint)materialIndex);

                // calculating bounding box

                double longestX = maxX - minX;
                double longestY = maxY - minY;

                double longestDim = longestX * 1000;
                if (longestY > longestDim) longestDim = longestY * 1000;

                double dimFraction = longestDim / 100000;

                GimUnitSize nearestMinUnit = new();
                GimUnitSize nearestMaxUnit = new();

                for (byte b = 0; b <= 127; b++)
                {
                    GimUnitSize currentUnit = new(b);

                    if (currentUnit.GimUnit.IsMetric())
                    {
                        if (currentUnit.GetRoundingStep() >= dimFraction)
                        {
                            nearestMinUnit = currentUnit;
                            break;
                        }
                    }
                }

                for (byte b = 127; b >= 0; b--)
                {
                    GimUnitSize currentUnit = new(b);

                    if (currentUnit.GimUnit.IsMetric())
                    {
                        if (currentUnit.GetRoundingStep() <= longestDim / 2)
                        {
                            nearestMaxUnit = currentUnit;
                            break;
                        }
                    }
                }

                var objInst = new ObjectInstance()
                {
                    Name = gimData.Objects[0].Name,
                    RegionID = RegionID, // code of the file in 6 chars
                    ID = instanceID,
                    DefID = definitionID,
                    Px = (int)(avgX * 1000),
                    Py = (int)(avgY * 1000),
                    Pz = (int)(minZ * 1000),
                    Ry = (int)Math.Round(ry * 572957795), // value to be expected between -1.800.000.000 and 1.800.000.000
                    GIMminlat = RdToLat(minX, minY),
                    GIMmaxlat = RdToLat(maxX, maxY),
                    GIMminlong = RdToLong(minX, minY),
                    GIMmaxlong = RdToLong(maxX, maxY),
                    M = false,
                };
                lock (objectInstances)
                    objectInstances.Add(objInst);

                //instanceID++;
                bool newDefinition = true;
                // TODO: add TwinDetection
                if (!newDefinition) return;

                // we do not care about buildings from these files, we just mark them as delete to maintain the ID index
                if (type.ToLower() == "building") type = "delete";
                var objDef = new ObjectDefinition()
                {
                    ID = definitionID,
                    Name = gimData.Objects[0].Name,
                    Type = type,
                    OID = null,
                    PID = null,
                    minGIMunit = nearestMinUnit.ToByte(),
                    maxGIMunit = nearestMaxUnit.ToByte(),
                };

                GimWriter writer = new();
                //string gimBin64 = GimBin(gimData);
                //byte[] gimBinBytes = Convert.FromBase64String(gimBin64);

                int lowerIDbound = (int)Math.Floor((double)definitionID / 10000);
                string directoryName = lowerIDbound.ToString();
                lock (writeAccess)
                {
                    var dirInfo = Directory.CreateDirectory(FileAccess.GetTempPath(directoryName));
                    string dirName = dirInfo.FullName;
                    string fileName = dirName + "\\" + definitionID;
                    if (type == "building")
                    {
                        fileName += "_12";
                    }
                    byte[] bytes = FileAccess.GetGimDataAsByteArrayGLB(gimData);
                    if(createFiles) File.WriteAllBytes(fileName + ".glb", bytes);
                    //GIMtransfer.FileAccess.WriteFromGimData(gimData, "bin", fileName, false);

                    //var binUpload = UploadGLBFile(gimBinBytes, definitionID.ToString(), "bin");
                }
                bool binSuccess = true;

                if (!binSuccess)
                {
                    Console.WriteLine("Warning! file " + (definitionID + ".bin") + " Failed to upload to server!");
                    // throw an error?
                    throw new Exception("Could not upload file " + (definitionID + ".bin") + " to server, stopping.");
                }

                lock (objectDefinitions) objectDefinitions.Add(objDef);

            }
            return;

            static void SetValues(List<double[]> vertices, Dictionary<int, int> inverseList, ref int minX, ref int maxX, ref int minY, ref int maxY,
                ref double minZ, List<int> list, List<int> elementInts, List<double> elementDoubles, JValue jVertice)
            {
                int jVerticeIndex = (int)jVertice;
                var vertex = vertices[jVerticeIndex];
                //int i = list.IndexOf((int)jVertice);
                int i;
                if (inverseList.ContainsKey(jVerticeIndex))
                {
                    i = inverseList[jVerticeIndex];
                }

                else // new local vertice
                {
                    elementDoubles.AddRange(vertices[jVerticeIndex]);
                    i = list.Count;
                    list.Add(jVerticeIndex);
                    inverseList.Add(jVerticeIndex, i);
                }
                if (minX == 0 && maxX == 0)
                {
                    minX = (int)Math.Floor(vertex[0]);
                    maxX = (int)Math.Ceiling(vertex[0]);
                    minY = (int)Math.Floor(vertex[1]);
                    maxY = (int)Math.Ceiling(vertex[1]);
                    minZ = vertex[2];
                }
                else
                {
                    minX = (int)Math.Floor(Math.Min(minX, vertex[0]));
                    maxX = (int)Math.Ceiling(Math.Max(maxX, vertex[0]));
                    minY = (int)Math.Floor(Math.Min(minY, vertex[1]));
                    maxY = (int)Math.Ceiling(Math.Max(maxY, vertex[1]));
                    minZ = Math.Min(minZ, vertex[2]);
                }
                elementInts.Add(i);
            }
        }


        #endregion streaming
        public static void CityJsonToGimData(ref string json, ref int instanceID, ref int definitionID, string RegionID) 
        { 
            //JObject jObject = JObject.Parse(json);
            JObject jObject = null;
            //string JsonType = jObject["type"].Value<string>();
            string JsonType = "CityJSON";

            //if (JsonType == "FeatureCollection")
            //if (false)
            //{
            //    StringBuilder ObjectsCsv = new StringBuilder();
            //    ObjectsCsv.AppendLine("Id;Name;Px;Py;Pz;Rp;Rr;Ry;GimBin;GimJson");

            //    JArray jFeatures = jObject["features"].Value<JArray>();
            //    foreach (JObject jFeature in jFeatures)
            //    {
            //        // TODO: create new GimData incl Root.
            //        JObject jGeometry = jFeature["geometry"].Value<JObject>();
            //        GimData gimData = createGimDataFromJson(jGeometry);
                    
            //        JObject jProperties = jFeature["properties"].Value<JObject>();
            //        int objectcode = jProperties["OBJECTCODE"].Value<int>();
            //        //string objectDescription = jProperties["OBJCOD_OMS"].Value<string>();
            //        string objectguid = jProperties["GUID"].Value<string>();
            //        byte[] startDate = DateTimeToBytes(jProperties["OPNAMEDATUM"].Value<string>());
            //        double yaw = jProperties["HOEK"].Value<double>(); // TODO: calculate deg to rad.

            //        string objectDescription = "";
            //        int oid = 0, pid = 0;
            //        switch (objectcode)
            //        {
            //            case 912: oid = 0; pid = 0; objectDescription = "SNS Detectielus Telinrichting"; break;
            //            case 1321: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl rechtdoor"; break;
            //            case 1322: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl rechtsaf"; break;
            //            case 1323: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl linksaf"; break;
            //            case 1324: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl rechtdoor rechtsaf"; break;
            //            case 1325: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl rechtdoor linksaf"; break;
            //            case 1326: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl rechtdoor rechtsaf linksaf"; break;
            //            case 1327: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl hamerkop rechtaf"; break;
            //            case 1328: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl hamerkop linksaf"; break;
            //            case 1329: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl verdrijfpijl naar rechts"; break;
            //            case 1330: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Pijl verdrijfpijl naar links"; break;
            //            case 1341: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Rotonde pijl rechtsaf"; break;
            //            case 1342: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Rotonde pijl rechtdoor"; break;
            //            case 1343: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Rotonde pijl linksaf"; break;
            //            case 1344: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Rotonde pijl rechtdoor rechtsaf"; break;
            //            case 1345: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Rotonde pijl rechtdoor linksaf"; break;
            //            case 1346: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Rotonde pijl rechtsaf linksaf"; break;
            //            case 1347: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Rotonde pijl rechtdoor rechtsaf linksaf"; break;
            //            case 1350: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst BUS"; break;
            //            case 1351: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst 30"; break;
            //            case 1352: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst 50"; break;
            //            case 1353: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst 60"; break;
            //            case 1354: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst 70"; break;
            //            case 1355: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst 80"; break;
            //            case 1359: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst STOP"; break;
            //            case 1360: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Tekst NP"; break;
            //            case 1361: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Fiets symbool"; break;
            //            case 1362: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Invalide symbool"; break;
            //            case 1363: oid = 0; pid = 0; objectDescription = "PNB WGI Markering Voorwaarschuwingsdriehoek"; break;
            //            default:
            //                string new_case = "case " + objectcode + ": oid = 0; pid = 0; objectDescription = \"" + objectDescription + "\"; break;";
            //                break; // we need to add a new Case based on this string.

            //        }

            //        double X = 0, Y = 0, Z = 0; // TODO: find plot and calculate XYZ
            //        string GimBin = ""; // TODO: create GimData with reference to PID & OID
            //        string GimJson = "";

            //        ObjectsCsv.AppendLine(instanceID + ";" + objectguid + ";" +
            //            X + ";" + Y + ";" + Z + ";0;0;" + yaw + ";" + GimBin + ";" + GimJson);

            //        instanceID++;
            //    }
            //}
            /*else*/ if(JsonType == "CityJSON")
            {
                var jFile = JsonConvert.DeserializeObject<JToken>(json);
                // constructing vertices

                JObject jTransform = jFile["transform"].Value<JObject>();
                double[] transform = jTransform["translate"].Value<JArray>().ToObject<double[]>();
                double[] scale = jTransform["scale"].Value<JArray>().ToObject<double[]>();

                JArray[] jVertices = jFile["vertices"].Value<JArray>().ToObject<JArray[]>();
                List<double[]> vertices = new List<double[]>();
                int index = 0;
                foreach (JArray jVertice in jVertices)
                {
                    double[] vertice = jVertice.ToObject<double[]>();
                    vertice[0] = vertice[0] * scale[0] + transform[0]; // X
                    vertice[1] = vertice[1] * scale[1] + transform[1]; // Y
                    vertice[2] = vertice[2] * scale[2] + transform[2]; // Z
                    vertices.Add(vertice);

                    index++;
                } // calculate the correct value of the vertices

                json = null;

                GC.Collect();
                foreach (JProperty jObjectA in jFile)
                {
                    if (jObjectA.Name.Equals("CityObjects"))
                    {
                        GetCityObjectsAndPlots(jObjectA, vertices, ref instanceID, ref definitionID, RegionID);
                    }
                } // read the plots and objects
                
                //GC.Collect();
            }
        }

        private static GimData createGimDataFromJson(JObject? jGeometry)
        {
            GimData gimData = new GimData();
            gimData.elements.Add(new GimElement() { ElementType = GimElementType.D_root });

            string geometryType = jGeometry["type"].Value<string>();
            if (geometryType.ToLower() == "polygon")
            {
                bool hasGimPoly = false;
                int minX = 0, maxX = 0, minY = 0, maxY = 0, minZ = 0;
                JArray jShape = jGeometry["coordinates"].Value<JArray>();
                foreach (JArray jElement in jShape)
                {
                    // TODO: create new GimMesh
                    List<double> ElementDoublesGlobal = new List<double>();
                    List<int> ElementInts = new List<int>();
                    List<double[]> element = new List<double[]>();
                    int vertexCount = 0;
                    foreach (JArray jVertex in jElement)
                    {
                        double[] vertex = jVertex.Value<JToken>().ToObject<double[]>();
                        if (minX == 0 && maxX == 0)
                        {
                            minX = (int)Math.Floor(vertex[0]);
                            maxX = (int)Math.Ceiling(vertex[0]);
                            minY = (int)Math.Floor(vertex[1]);
                            maxY = (int)Math.Ceiling(vertex[1]);
                            minZ = (int)Math.Floor(vertex[2]);
                        }
                        else
                        {
                            minX = (int)Math.Floor(Math.Min(minX, vertex[0]));
                            maxX = (int)Math.Ceiling(Math.Max(maxX, vertex[0]));
                            minY = (int)Math.Floor(Math.Min(minY, vertex[1]));
                            maxY = (int)Math.Ceiling(Math.Max(maxY, vertex[1]));
                            minZ = (int)Math.Floor(Math.Min(minZ, vertex[2]));
                        }
                        int i = element.IndexOf(vertex);
                        if (i == -1) // add the vertex if it's new
                        {
                            element.Add(vertex); 
                            ElementDoublesGlobal.AddRange(vertex);
                            i = element.Count - 1;
                        }
                        ElementInts.Add(i);
                        vertexCount++;
                    }
                    if(vertexCount != 3) hasGimPoly = true;
                    // calculate the centre and move all points
                    double cenX = (minX + maxX) / 2;
                    double cenY = (minY + maxY) / 2;
                    //foreach (double[] vertex in element)
                    //{
                    //    vertex[0] = Math.Round(vertex[0] - cenX, 3);
                    //    vertex[1] = Math.Round(vertex[1] - cenY, 3);
                    //    vertex[2] = Math.Round(vertex[2] - minZ, 3);
                    //}
                    List<double> ElementDoubles = new List<double>();
                    int j = 0;
                    foreach (double d in ElementDoublesGlobal)
                    {
                        // multiply by scale
                        if (j == 0) ElementDoubles.Add(Math.Round(d - cenX, 3));
                        if (j == 1) ElementDoubles.Add(Math.Round(d - cenY, 3));
                        if (j == 2) { ElementDoubles.Add(Math.Round(d - minZ, 3)); j = -1; }
                        j++;
                    }
                    // add the part with the correct local at the centre of the object
                    int e = gimData.elements.Count();
                    gimData.Elements.Add(new GimElement // add new part
                    {
                        ElementType = GimElementType.D_part,
                        Elements = new List<uint> { (uint)(e + 1) },
                        Locals = new List<GimLocal> { new GimLocal
                                        {
                                            parent = (uint)(e - 1),
                                            Px = cenX,
                                            Py = cenY,
                                            Pz = minZ
                                        } }
                    });

                    gimData.Elements[0].elements.Add((uint)e); // add child to the root

                    // add the polymesh as child of the root
                    e++;
                    gimData.Elements.Add(new GimElement
                    {
                        ElementType = GimElementType.R_Poly,
                        Elements = new List<uint> { (uint)(e + 1) },
                        Locals = new List<GimLocal> { new GimLocal
                                        {
                                            parent = (uint)(e - 1)
                                        } }
                    });
                    // add new polygon as child of the polymesh
                    // TODO: if(hasGimPoly) create GimPoly
                    e++;
                    gimData.Elements.Add(new GimElement
                    {
                        ElementType = GimElementType.P_polygon,
                        ElementDoubles = ElementDoubles.ToArray(),
                        ElementInts = ElementInts.ToArray(),
                        Locals = new List<GimLocal> { new GimLocal
                                        {
                                            parent = (uint)(e - 1)
                                        } }
                    }); 
                }
            }
            else if (geometryType.ToLower() == "point")
            {
                // TODO: create new ExternalReferenceObject in GimData
                double[] coordinates = jGeometry["coordinates"].Value<JToken>().ToObject<double[]>();
                int Gimlong = RdToLong(coordinates[0], coordinates[1]);
                int GimLat = RdToLat(coordinates[0], coordinates[1]);
            }
            return gimData;
        }

        private static void GetCityObjectsAndPlots(JProperty jObject, List<double[]> vertices, ref int instanceID, ref int definitionID, string RegionID)
        {
            foreach (JProperty jFragmentA in jObject.Values())
            {
                GimData gimData = new GimData();
                //List<Tuple<string, string>> properties = new List<Tuple<string, string>>();
                List<CityJsonProperty> objectProperties = new List<CityJsonProperty>();
                int minX = 0, maxX = 0, minY = 0, maxY = 0;
                double minZ = 0;
                double avgX = 0, avgY = 0;
                string objectType = "";
                string type = jFragmentA.Value["type"].ToString().ToLower();
                //if ((type != "building" && type != "genericcityobject" && type != "bridge"))
                //{
                //    continue;
                //}
                foreach (JProperty jObjectOrType in jFragmentA.Values())
                {
                    if (jObjectOrType.Name.Equals("geometry"))
                    {
                        gimData.elements.Add(new GimElement() { ElementType = GimElementType.D_root });

                        foreach (JContainer jFragmentC in jObjectOrType.Children())
                        {
                            foreach (JProperty jBoundary in jFragmentC.Values())
                            {
                                if (jBoundary.Name.Equals("boundaries"))
                                {
                                    int e = 0;
                                    List<int> list = new List<int>();
                                    Dictionary<int, int> inverseList = new();
                                    List<int> elementInts = new List<int>();
                                    List<double> elementDoubles = new List<double>();
                                    avgX = 0; avgY = 0;
                                    int count = 0;
                                    foreach (JArray jPolyMesh in jBoundary.Values())
                                    {
                                        if (jPolyMesh.Children().Count() > 1) // building
                                        {
                                            foreach (JArray jTriagle in jPolyMesh.Values())
                                            {
                                                foreach (JValue jVertice in jTriagle.Values())
                                                {
                                                    SetValues(vertices, inverseList, ref minX, ref maxX, ref minY, ref maxY, ref minZ, list, elementInts, elementDoubles, jVertice);
                                                    avgX += vertices[(int)jVertice][0];
                                                    avgY += vertices[(int)jVertice][1];
                                                    count++;
                                                }
                                            }
                                        }
                                        else // land surface
                                        {
                                            foreach (JValue jVertice in jPolyMesh.Values())
                                            {
                                                SetValues(vertices, inverseList, ref minX, ref maxX, ref minY, ref maxY, ref minZ, list, elementInts, elementDoubles, jVertice);
                                                avgX += vertices[(int)jVertice][0];
                                                avgY += vertices[(int)jVertice][1];
                                                count++;
                                            }
                                        }
                                    }
                                    if (minX == 0 || minY == 0)
                                    {
                                        minX = 0;
                                    }
                                    if(count != 0)
                                    {
                                        avgX /= count;
                                        avgY /= count;
                                    }
                                    // calculate the centre and move all points
                                    double centerX = avgX;
                                    double centerY = avgY;
                                    // TODO: rotate to make the longest horizontal line pure-X
                                    List<double> points = new List<double>();
                                    int i = 0;
                                    foreach (double d in elementDoubles)
                                    {
                                        // multiply by scale
                                        if (i == 0) points.Add(d - centerX);
                                        if (i == 1) points.Add(d - centerY);
                                        if (i == 2) { points.Add(d - minZ); i = -1; }
                                        i++;
                                    }
                                    // add the part with the correct local at the centre of the object
                                    e = gimData.elements.Count();
                                    gimData.Elements.Add(new GimElement // add new part
                                    {
                                        ElementType = GimElementType.D_part,
                                        Elements = new List<uint> { (uint)(e + 1) },
                                        Locals = new List<GimLocal> {
                                            new GimLocal
                                            {
                                                parent = (uint)(e - 1),
                                                Px = 0,
                                                Py = 0,
                                                Pz = 0
                                            }
                                        }
                                    });
                                    // convert RD to Latitude and Longitude
                                    int GimLong = RdToLong(centerX, centerY);
                                    int GimLat = RdToLat(centerX, centerY);

                                    gimData.Elements[0].elements.Add((uint)e); // add child to the root
                                    gimData.Objects.Add(GimObjectFactory.Create((uint)e, jFragmentA.Name)); // add part as new object
                                    // add the polymesh as child of the root
                                    e++;
                                    gimData.Elements.Add(new GimElement
                                    {
                                        ElementType = GimElementType.R_Poly,
                                        Elements = new List<uint> { (uint)(e + 1) },
                                        Locals = new List<GimLocal> { 
                                            new GimLocal
                                            {
                                                parent = (uint)(e - 1)
                                            }
                                        }
                                    });
                                    // add new polygon as child of the polymesh
                                    e++;
                                    gimData.Elements.Add(new GimElement
                                    {
                                        ElementType = GimElementType.P_polygon,
                                        ElementDoubles = points.ToArray(),
                                        ElementInts = elementInts.ToArray(),
                                        Locals = new List<GimLocal> { new GimLocal
                                        {
                                            parent = (uint)(e - 1)
                                        } }
                                    });
                                    //FileAccess.GimDataToJson(gimData, "0-" + id.ToString()); // save all GimData as .json
                                }
                            }
                        }
                    }
                    else if (jObjectOrType.Name.ToString() == "type")
                    {
                        objectType = jObjectOrType.Value.ToString().ToLower();
                    }
                    else if (jObjectOrType.Name.ToString() == "attributes")
                    {

                    }
                }
                //int cenX = (int)Math.Floor((double)(minX + maxX) / 2);
                //int cenY = (int)Math.Floor((double)(minY + maxY) / 2);
                //int cenZ = minZ;
                //string GIMjsonString = Convert.ToBase64String(GIMjson);


                double ry = 0;

                // we get the most probable rotation of the building here
                // only do this for buildings, not plots
                if ((type == "building" || type == "genericcityobject" || type == "bridge"))
                {
                    ry = GetPolyRepYawRotation(gimData);

                    // we apply the inverse of the rotation matrix to make the object align with the grid by default
                    Matrix reverseTransformation = Matrix.FromRPY(0, 0, -ry);

                    // we unrotate each vertex, one by one, to reset the object to a more neutral definition
                    foreach (GimElement element in gimData.Elements)
                    {
                        if (element.ElementType == GimElementType.P_polygon)
                        {
                            for (int i = 0; i + 2 < element.ElementDoubles.Length; i += 3)
                            {
                                Vect vertex = new Vect(
                                    element.ElementDoubles[i],
                                    element.ElementDoubles[i + 1],
                                    element.ElementDoubles[i + 2]
                                    );

                                Vect newVertex = reverseTransformation * vertex;

                                element.ElementDoubles[i] = Math.Round(newVertex[0], 3);
                                element.ElementDoubles[i + 1] = Math.Round(newVertex[1], 3);
                                element.ElementDoubles[i + 2] = Math.Round(newVertex[2], 3);

                            }
                        }
                    }
                }

                else // we look for grid points contained in this plot
                {
                    // we round up the min to nearest multiple of 100, e.g. 285 => 300
                    //double minGridX = Math.Ceiling((double)minX / 100) * 100;
                    //double minGridY = Math.Ceiling((double)minY / 100) * 100;

                    //double maxGridX = Math.Floor((double)maxX / 100) * 100;
                    //double maxGridY = Math.Floor((double)maxY / 100) * 100;

                    //List<Vect> gridPoints = new();

                    //for(double x = minGridX; x <= maxGridX; x+= 100)
                    //{
                    //    for(double y = minGridY; y <= maxGridY; y += 100)
                    //    {
                    //        gridPoints.Add(new(x, y));
                    //    }
                    //}

                    //List<Vect> gridPointsWithAltitude = GetGridPointZValuesFromPlot(gimData, avgX, avgY, minZ, gridPoints);

                    //foreach(Vect gridPoint in gridPointsWithAltitude)
                    //{
                    //    Vect key = new Vect(gridPoint[0], gridPoint[1]);
                    //    gridCoordinatesToAltitude[key] = gridPoint[2];
                    //}
                }


                int partIndex = 0;
                for (int i = 0; i < gimData.Elements.Count; i++)
                {
                    GimElement element = gimData.Elements[i];
                    if (element.ElementType == GimElementType.D_part)
                    {
                        partIndex = i;
                        break;
                    }
                }

                // adding color to definition based on type

                int materialIndex = gimData.Elements.Count;

                string materialColorHex = "";
                //            string waterHex = "1ca3ec";
                //            string landUseHex = "69f7a6";
                //            string roadHex = "8c8eab";
                //            string plantcoverHex = "228b22";

                //            string bridgeHex = "AA4A44";
                //            string buildingHex = "c16464";
                //            string genericcityobjectHex = "A0A0A0";

                switch (type.ToLower())
                {
                    case "bridge": materialColorHex = "AA4A44"; break;
                    case "building": materialColorHex = "c16464"; break;
                    case "genericcityobject": materialColorHex = "A0A0A0"; break;
                    case "landuse": materialColorHex = "69f7a6"; break;
                    case "plantcover": materialColorHex = "228b22"; break;
                    case "road": materialColorHex = "8c8eab"; break;
                    case "waterbody": materialColorHex = "1ca3ec"; break;
                }

                int color = int.Parse(materialColorHex, System.Globalization.NumberStyles.HexNumber);

                GimMaterial gimMaterial = new()
                {
                    BaseColour = color,
                    DiffuseColour = color,
                    SpecularColour = color,
                    AmbientColour = color,
                    ElementType = GimElementType.Material
                };

                gimData.Elements.Add(gimMaterial);
                gimMaterial.Locals.Add(new() { parent = (uint)partIndex });

                GimElement partElement = gimData.Elements[partIndex];
                partElement.Elements.Insert(0, (uint)materialIndex);

                // calculating bounding box

                double longestX = maxX - minX;
                double longestY = maxY - minY;

                double longestDim = longestX * 1000;
                if(longestY > longestDim) longestDim = longestY * 1000;

                double dimFraction = longestDim / 100000;

                GimUnitSize nearestMinUnit = new();
                GimUnitSize nearestMaxUnit = new();

                for(byte b =0; b <= 127; b++)
                {
                    GimUnitSize currentUnit = new(b);
                    
                    if (currentUnit.GimUnit.IsMetric())
                    {
                        if (currentUnit.GetRoundingStep() >= dimFraction)
                        {
                            nearestMinUnit = currentUnit;
                            break;
                        }
                    }
                }

                for (byte b = 127; b >=0; b--)
                {
                    GimUnitSize currentUnit = new(b);

                    if (currentUnit.GimUnit.IsMetric())
                    {
                        if (currentUnit.GetRoundingStep() <= longestDim / 2)
                        {
                            nearestMaxUnit = currentUnit;
                            break;
                        }
                    }
                }

                var objInst = new ObjectInstance()
                {
                    Name = gimData.Objects[0].Name,
                    RegionID = RegionID, // code of the file in 6 chars
                    ID = instanceID,
                    DefID = definitionID,
                    Px = (int)(avgX*1000),
                    Py = (int)(avgY*1000),
                    Pz = (int)(minZ*1000),
                    Ry = (int)Math.Round(ry * 572957795), // value to be expected between -1.800.000.000 and 1.800.000.000
                    GIMminlat = RdToLat(minX, minY),
                    GIMmaxlat = RdToLat(maxX, maxY),
                    GIMminlong = RdToLong(minX, minY),
                    GIMmaxlong = RdToLong(maxX, maxY),
                    M = false,
                };

                objectInstances.Add(objInst);
                
                instanceID++;
                bool newDefinition = true;
                // TODO: add TwinDetection
                if (newDefinition)
                {
                    var objDef = new ObjectDefinition()
                    {
                        ID = definitionID,
                        Name = gimData.Objects[0].Name,
                        Type = type,
                        OID = null,
                        PID = null,
                        minGIMunit = nearestMinUnit.ToByte(),
                        maxGIMunit = nearestMaxUnit.ToByte(),
                    };

                    GimWriter writer = new();
                    //string gimBin64 = GimBin(gimData);
                    //byte[] gimBinBytes = Convert.FromBase64String(gimBin64);

                    int lowerIDbound = (int)Math.Floor((double)definitionID / 10000) * 10000;
                    int upperIDbound = lowerIDbound + 9999;
                    string directoryName = lowerIDbound + "-" + upperIDbound;

                    Directory.CreateDirectory(FileAccess.GetTempPath(directoryName));

                    string binFileName = directoryName + "\\" + definitionID;
                    GIMtransfer.FileAccess.WriteFromGimData(gimData, "bin", binFileName , false);

                    //var binUpload = UploadGLBFile(gimBinBytes, definitionID.ToString(), "bin");

                    bool binSuccess = true;

                    if (!binSuccess)
                    {
                        Console.WriteLine("Warning! file " + (definitionID + ".bin") + " Failed to upload to server!");
                        // throw an error?
                        throw new Exception("Could not upload file " + (definitionID + ".bin") + " to server, stopping.");
                    }

                    objectDefinitions.Add(objDef);
                    definitionID++;
                }
            }
            return;

            static void SetValues(List<double[]> vertices, Dictionary<int, int> inverseList, ref int minX, ref int maxX, ref int minY, ref int maxY,
                ref double minZ, List<int> list, List<int> elementInts, List<double> elementDoubles, JValue jVertice)
            {
                int jVerticeIndex = (int)jVertice;
                var vertex = vertices[jVerticeIndex];
                //int i = list.IndexOf((int)jVertice);
                int i;
                if (inverseList.ContainsKey(jVerticeIndex)){
                    i = inverseList[jVerticeIndex];
                }

                else // new local vertice
                {
                    elementDoubles.AddRange(vertices[jVerticeIndex]);
                    i = list.Count;
                    list.Add(jVerticeIndex);
                    inverseList.Add(jVerticeIndex, i);
                }
                if (minX == 0 && maxX == 0)
                {
                    minX = (int)Math.Floor(vertex[0]);
                    maxX = (int)Math.Ceiling(vertex[0]);
                    minY = (int)Math.Floor(vertex[1]);
                    maxY = (int)Math.Ceiling(vertex[1]);
                    minZ = vertex[2];
                }
                else
                {
                    minX = (int)Math.Floor(Math.Min(minX, vertex[0]));
                    maxX = (int)Math.Ceiling(Math.Max(maxX, vertex[0]));
                    minY = (int)Math.Floor(Math.Min(minY, vertex[1]));
                    maxY = (int)Math.Ceiling(Math.Max(maxY, vertex[1]));
                    minZ = Math.Min(minZ, vertex[2]);
                }
                elementInts.Add(i);
            }
        }
        public static List<Vect> GetGridPointZValuesFromPlot(GimData plot, double plotX, double plotY, double plotZ, List<Vect> points)
        {
            List<Vect> resultingPoints = new();

            static double pointSign(Vect a, Vect b, Vect c)
            {
                return (a[0] - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (a[1] - c[1]);
            }

            List<Vect[]> triangles = new();

            for (int i = 0; i < plot.Elements.Count; i++)
            {
                GimElement elem = plot.Elements[i];
                if (elem.ElementType == GimElementType.P_polygon)
                {
                    for (int triangleIndex = 0; triangleIndex < elem.ElementInts.Length / 3; triangleIndex++)
                    {
                        int pt1Index = elem.ElementInts[triangleIndex * 3];
                        int pt2Index = elem.ElementInts[triangleIndex * 3 + 1];
                        int pt3Index = elem.ElementInts[triangleIndex * 3 + 2];

                        Vect point1 = new Vect(
                            elem.ElementDoubles[pt1Index * 3] + plotX,
                            elem.ElementDoubles[pt1Index * 3 + 1] + plotY,
                            elem.ElementDoubles[pt1Index * 3 + 2]
                            );

                        Vect point2 = new Vect(
                           elem.ElementDoubles[pt2Index * 3] + plotX,
                           elem.ElementDoubles[pt2Index * 3 + 1] + plotY,
                           elem.ElementDoubles[pt2Index * 3 + 2]
                           );

                        Vect point3 = new Vect(
                           elem.ElementDoubles[pt3Index * 3] + plotX,
                           elem.ElementDoubles[pt3Index * 3 + 1] + plotY,
                           elem.ElementDoubles[pt3Index * 3 + 2]
                           );

                        triangles.Add(new Vect[] { point1, point2, point3 });
                    }
                }
            }

            foreach (Vect gridPoint in points)
            {
                foreach (Vect[] triangle in triangles)
                {
                    double sign1, sign2, sign3;

                    sign1 = pointSign(gridPoint, triangle[0], triangle[1]);
                    sign2 = pointSign(gridPoint, triangle[1], triangle[2]);
                    sign3 = pointSign(gridPoint, triangle[2], triangle[0]);

                    bool hasPositive = (sign1 >= 0) || (sign2 >= 0) || (sign3 >= 0);
                    bool hasNegative = (sign1 <= 0) || (sign2 <= 0) || (sign3 <= 0);

                    // if the below condition is true, the point is contained in one of the triangles
                    if (!(hasPositive && hasNegative))
                    {
                        // we average the Z values of the triangle and claim that's the altitude
                        double zValue = (triangle[0][2] + triangle[1][2] + triangle[2][2]) / 3 + plotZ;
                        resultingPoints.Add(new(gridPoint[0], gridPoint[1], zValue));
                        break;
                    }

                }
            }


            return resultingPoints;
        }

        static int failedGlbCount = 0;
        public static async Task<bool> UploadGLBFile(byte[] glbBytes, string name, string extension)
        {
            string finalFileName = name + "." + extension;
            string APIUrl = "https://testapi.gimtransfer.online/api/user/UploadGIMMapFiles";

            var client = new HttpClient();
            var multiForm = new MultipartFormDataContent();

            File.WriteAllBytes("replace", glbBytes);

            using(var fileStream = File.OpenRead("replace"))
            {
                //foreach(var b in glbBytes){
                //    memStream.WriteByte(b);
                //}
                multiForm.Add(new StreamContent(fileStream), "file", finalFileName);

                var response = await client.PostAsync(APIUrl, multiForm);
                if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
                {
                    fileStream.Close();
                    return true;
                }
                else
                {
                    fileStream.Close();
                    return false;
                }
            }

            return false;

        }

        internal static void unrotateGimDataByYaw(GimData gimData, double ry)
        {
            Matrix reverseTransformation = Matrix.FromRPY(0, 0, -ry);

            // we unrotate each vertex, one by one, to reset the object to a more neutral definition
            foreach (GimElement element in gimData.Elements)
            {
                if (element.ElementType == GimElementType.P_polygon)
                {
                    for (int i = 0; i + 2 < element.ElementDoubles.Length; i += 3)
                    {
                        Vect vertex = new Vect(
                            element.ElementDoubles[i],
                            element.ElementDoubles[i + 1],
                            element.ElementDoubles[i + 2]
                            );

                        Vect newVertex = reverseTransformation * vertex;

                        element.ElementDoubles[i] = Math.Round(newVertex[0], 3);
                        element.ElementDoubles[i + 1] = Math.Round(newVertex[1], 3);
                        element.ElementDoubles[i + 2] = Math.Round(newVertex[2], 3);

                    }
                }
            }
        }
        internal static double GetPolyRepYawRotation(GimData gimData)
        {
            double yaw = 0;

            Vect longestEdge = new(0, 0, 0);

            for(int elemIndex=0; elemIndex < gimData.Elements.Count; elemIndex++)
            {
                GimElement element = gimData.Elements[elemIndex];
                if(element.ElementType == GimElementType.P_polygon)
                {
                    List<Triangle> triangles = new List<Triangle>();
                    double minZ = 1000000;
                    int minTriangleIndex = 0;
                    int index = 0;
                    for (int i = 0; i + 2 < element.ElementInts.Length; i += 3)
                    {
                        int indexA = element.ElementInts[i];
                        int indexB = element.ElementInts[i + 1];
                        int indexC = element.ElementInts[i + 2];

                        Vect vertexA = new Vect(
                            element.ElementDoubles[indexA * 3],
                            element.ElementDoubles[indexA * 3 + 1],
                            element.ElementDoubles[indexA * 3 + 2]
                            );

                        Vect vertexB = new Vect(
                            element.ElementDoubles[indexB * 3],
                            element.ElementDoubles[indexB * 3 + 1],
                            element.ElementDoubles[indexB * 3 + 2]
                            );

                        Vect vertexC = new Vect(
                            element.ElementDoubles[indexC * 3],
                            element.ElementDoubles[indexC * 3 + 1],
                            element.ElementDoubles[indexC * 3 + 2]
                        );
                        if ((vertexA == vertexB) || (vertexB == vertexC) || (vertexC == vertexA))
                        {
                            continue;
                        }
                        Triangle triangle = new(
                        vertexA,
                        vertexB,
                        vertexC);
                        Vect avgPoint = (vertexA + vertexB + vertexC) / 3;

                        if (avgPoint[2] < minZ)
                        {
                            minZ = avgPoint[2];
                            minTriangleIndex = index;
                        }

                        index++;
                        triangles.Add(triangle);
                    }

                    var mesh = TriangleMesh.ConstructMesh( triangles );
                    PolygonRounder.ConstructNeighbourhoods(mesh);

                    Triangle lowestTriangle = mesh.Triangles[minTriangleIndex];
                    var lowestTriangles = GetNearbyFlatTriangles(lowestTriangle, new() { lowestTriangle });
                    

                    var lowerSideMesh = TriangleMesh.ConstructMesh(lowestTriangles);
                    PolygonRounder.ConstructNeighbourhoods(lowerSideMesh);
                    List<Vect> outerEdges = new List<Vect>();
                    string edgesDebug = "";
                    bool debug = false;
                    foreach(Triangle triangle in lowerSideMesh.Triangles)
                    {
                        if(triangle.Neighbour1.Count == 0)
                        {
                            outerEdges.Add((triangle.Vertex3 - triangle.Vertex2));
                            if(debug)edgesDebug += "polygon(" + "(" + triangle.Vertex3[0] + ", " + triangle.Vertex3[1] 
                                +"), (" + triangle.Vertex2[0] + ", " + triangle.Vertex2[1] + ")" + "), ";
                        }

                        if (triangle.Neighbour2.Count == 0)
                        {
                            outerEdges.Add((triangle.Vertex1 - triangle.Vertex3));
                            if (debug) edgesDebug += "polygon(" + "(" + triangle.Vertex1[0] + ", " + triangle.Vertex1[1]
                                + "), (" + triangle.Vertex3[0] + ", " + triangle.Vertex3[1] + ")" + "), ";
                        }

                        if (triangle.Neighbour3.Count == 0)
                        {
                            outerEdges.Add((triangle.Vertex2 - triangle.Vertex1));
                            if (debug) edgesDebug += "polygon(" + "(" + triangle.Vertex2[0] + ", " + triangle.Vertex2[1]
                                + "), (" + triangle.Vertex1[0] + ", " + triangle.Vertex1[1] + ")" + "), ";
                        }
                    }

                    double longestLength = 0;

                    foreach(Vect outerEdge in outerEdges)
                    {
                        outerEdge[2] = 0;
                        double length = outerEdge.Norm;
                        if(length > longestLength)
                        {
                            longestLength = length;
                            longestEdge = outerEdge;
                        }
                    }


                    break;
                }
            }

            if (longestEdge[0] == 0)
            {
                yaw = Math.PI / 2;
            }
            else
            {
                yaw = Math.Atan(longestEdge[1] / longestEdge[0]);
            }


            return yaw;
        }
        private static List<Triangle> GetNearbyFlatTriangles(Triangle triangle, List<Triangle> visitedTriangles)
        {
            List<Triangle> result = new();

            foreach(List<Triangle> neighbourSide in triangle.Neighbour)
            {
                foreach(Triangle neighbour in neighbourSide)
                {
                    if (visitedTriangles.Contains(neighbour)) continue;
                    visitedTriangles.Add(neighbour);
                    if (neighbour.Vertex1[2] == neighbour.Vertex2[2] && neighbour.Vertex2[2] == neighbour.Vertex3[2])
                    {
                        var neighbourResult = GetNearbyFlatTriangles(neighbour, visitedTriangles);
                        result.AddRange(neighbourResult);
                    }
                }
            }

            result.Add(triangle);

            return result;
        }
        private static string GimBin(GimData gimData)
        {
            // Write to Bin & Json
            var gimWriter = new GimWriter();
            Tuple<uint[], byte> binaryResult = gimWriter.PublicGimDataToBytesWithSmallestScalesIndex(gimData/*, new GimProgressStatus()*/); // TODO: progress bar here
            uint[] arr = binaryResult.Item1;

            byte[] byteArray = new byte[4 * arr.Length];

            for (int k = 0; k < arr.Length; k++)
            {
                uint val = arr[k];

                byte byte1 = (byte)((val & 0xFF000000) >> 24);
                byte byte2 = (byte)((val & 0x00FF0000) >> 16);
                byte byte3 = (byte)((val & 0x0000FF00) >> 8);
                byte byte4 = (byte)((val & 0x000000FF));

                byteArray[4 * k] = byte1;
                byteArray[4 * k + 1] = byte2;
                byteArray[4 * k + 2] = byte3;
                byteArray[4 * k + 3] = byte4;
            }
            return Convert.ToBase64String(byteArray);
        }
        public static int RdToLong(double X, double Y)
        {
            X = X / 100000 - 1.55;
            Y = Y / 100000 - 4.63;
            return (int)Math.Round((5.387206 + (
                5260.52916 * X +
                105.94684 * X * Y +
                2.45656 * X * Math.Pow(Y, 2) +
                -0.81885 * Math.Pow(X, 3) +
                0.05594 * X * Math.Pow(Y, 3) +
                -0.05607 * Math.Pow(X, 3) * Y +
                0.01199 * Y +
                -0.00256 * Math.Pow(X, 3) * Math.Pow(Y, 2) +
                0.00128 * X * Math.Pow(Y, 4) +
                0.00022 * Math.Pow(Y, 2) +
                -0.00022 * Math.Pow(X, 2) +
                0.00026 * Math.Pow(X, 5)
                ) / 3600) / 360 * 40075000);
        }
        public static int RdToLat(double X, double Y)
        {
            X = X / 100000 - 1.55;
            Y = Y / 100000 - 4.63;
            return (int)Math.Round((52.15517 + (
                3235.65389 * Y +
                -32.58297 * Math.Pow(X, 2) +
                -0.2475 * Math.Pow(Y, 2) +
                -0.84978 * Math.Pow(X, 2) * Y +
                -0.0655 * Math.Pow(Y, 3) +
                -0.01709 * Math.Pow(X, 2) * Math.Pow(Y, 2) +
                -0.00738 * X +
                0.0053 * Math.Pow(X, 4) +
                -0.00039 * Math.Pow(X, 2) * Math.Pow(Y, 3) +
                0.00033 * Math.Pow(X, 4) * Y +
                -0.00012 * X * Y
                ) / 3600) / 360 * 40075000);
        }
        public static double LongLatToX(double Lat, double Long)
        {
            return 155000 +
                (190094.945 * (0.36 * (Long - 5.38720621))) +
                (-11832.228 * (0.36 * (Lat - 52.1551744)) * (0.36 * (Long - 5.38720621))) +
                (-114.221 * Math.Pow(0.36 * (Lat - 52.1551744), 2) * (0.36 * (Long - 5.38720621))) +
                (-32.391 * Math.Pow(0.36 * (Long - 5.38720621), 3)) + 
                (-0.705 * (0.36 * (Lat - 52.1551744))) +
                (-2.34 * Math.Pow(0.36 * (Lat - 52.1551744), 3) * (0.36 * (Long - 5.38720621))) +
                (-0.608 * (0.36 * (Lat - 52.1551744)) * Math.Pow(0.36 * (Long - 5.38720621), 3)) +
                (-0.008 * Math.Pow(0.36 * (Long - 5.38720621), 2)) +
                (0.148 * Math.Pow(0.36 * (Lat - 52.1551744), 2) * Math.Pow(0.36 * (Long - 5.38720621), 3));
        }
        public static double LongLatToY(double Lat, double Long)
        {
            return 463000 +
                (309056.544 * (0.36 * (Lat - 52.1551744))) +
                (3638.893 * Math.Pow(0.36 * (Long - 5.38720621), 2)) +
                (73.077 * Math.Pow(0.36 * (Lat - 52.1551744), 2)) +
                (-157.984 * (0.36 * (Lat - 52.1551744)) * Math.Pow(0.36 * (Long - 5.38720621), 2)) +
                (59.788 * Math.Pow(0.36 * (Lat - 52.1551744), 3)) +
                (0.433 * (0.36 * (Long - 5.38720621))) +
                (-6.439 * Math.Pow(0.36 * (Lat - 52.1551744), 2) * Math.Pow(0.36 * (Long - 5.38720621), 2)) +
                (-0.032 * (0.36 * (Lat - 52.1551744)) * (0.36 * (Long - 5.38720621))) +
                (0.092 * Math.Pow(0.36 * (Long - 5.38720621), 4)) +
                (-0.054 * (0.36 * (Lat - 52.1551744)) * Math.Pow(0.36 * (Long - 5.38720621), 4));
        }
        private static byte[] DateTimeToBytes(string Value)
        {
            if (Value == null) return new byte[] { };
            if (Value.Length == 4) // only year. add month = 00
            {
                Value += "00";
            }
            if(Value.Length == 6) // yyyyMM. 2 bytes. max year = 5.041
            {
                string month = Value.Substring(4, 2);
                string year = Value.Substring(0, 4);
                ushort v = (ushort)(Convert.ToUInt16(month) + Convert.ToUInt16(year) * 13); // number of months including 0. This means 13 months per year
                return BitConverter.GetBytes(v);
            }
            else if (Value.Length == 8) // yyyyMMdd. 3 bytes. max date = 2149-06-04
            {
                uint dateTime = (uint)(DateTime.ParseExact(Value, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture).Ticks / 1000);
                byte [] bytes = BitConverter.GetBytes(dateTime - 3049287680); // minus 1970-01-01
                return new byte[3] { bytes[1], bytes[2], bytes[3] };
            }
            else if(Value.Length == 14) // yyyyMMddHHmmss. 7 bytes. 
            {
                ulong dateTime = (ulong)(DateTime.ParseExact(Value, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture).Ticks / 1000);
                byte[] bytes = BitConverter.GetBytes(dateTime - 3049287680); // minus 1970-01-01
                return new byte[7] { bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7] };
            }
            else // yyyyMMdd HH:mm:ss. 7 bytes
            {
                try
                {
                    ulong dateTime = (ulong)(DateTime.Parse(Value).Ticks / 1000);
                    byte[] bytes = BitConverter.GetBytes(dateTime - 3049287680); // minus 1970-01-01
                    return new byte[7] { bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7] };
                }
                catch ( Exception e) { return new byte[] { }; }
            }
        }
        public static int CreateStreetSignObjects(bool skipDefinitions)
        {
            int id = 0;
            if (!skipDefinitions)
            {
                 // empty database
                var objDef = new ObjectDefinition()
                {
                    ID = id,
                    Name = "Verkeersbord Paal", // pole
                    Type = "StreetSign",
                    OID = null,
                    PID = null,
                    minGIMunit = 0,
                    maxGIMunit = 127,
                };
                objectDefinitions.Add(objDef);
                id++;
                objDef = new ObjectDefinition()
                {
                    ID = id,
                    Name = "Verkeersbord Zone", // aditional sign for the zone
                    Type = "StreetSign",
                    OID = null,
                    PID = null,
                    minGIMunit = 0,
                    maxGIMunit = 127,
                };
                objectDefinitions.Add(objDef);
                id++;
            }
            rvvCodes = new List<string>() { "A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4", "B5", "B6", "B7", "C1", "C2", "C3"
, "C4", "C5", "C6", "C7", "C7a", "C7b", "C8", "C9", "C10", "C11", "C12", "C13", "C14", "C15", "C16", "C17", "C18", "C19", "C20", "C21"
, "C22", "C22a", "C22b", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8", "E9", "E10", "E11"
, "E12", "E13", "F1", "F10", "F11", "F12", "F13", "F14", "F15", "F16", "F17", "F2", "F21", "F22", "F3", "F4", "F5", "F6", "F7", "F8"
, "G07", "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "G10", "G11", "G12", "G12a", "G12b", "G13", "G14", "H1", "H2", "J1"
, "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10", "J11", "J12", "J13", "J14", "J15", "J16", "J17", "J18", "J19", "J20", "J21"
, "J22", "J23", "J24", "J25", "J26", "J27", "J28", "J29", "J30", "J31", "J32", "J33", "J34", "J36", "J37", "J38", "J39", "K14", "L1"
, "L2", "L3a", "L3b", "L4", "L5", "L6", "L7", "L8", "L9", "L10", "L11", "L13", "L14", "L16", "L17", "L18", "L19" };

            // if we ran the program before and wish to skip sign definitions

            if (skipDefinitions) return id;

            foreach(string rvvCode in rvvCodes)
            {
                var objSignDef = new ObjectDefinition()
                {
                    ID = id,
                    Name = "Verkeersbord " + rvvCode,
                    Type = "StreetSign",
                    OID = null,
                    PID = null,
                    minGIMunit = 0,
                    maxGIMunit = 127,
                };
                objectDefinitions.Add(objSignDef);
                id++;
            }
            return id;
        }
        public static int ReadSigns(int instanceID, List<string> rvvCodes)
        {
            // read csv for signs
            using (var reader = new StreamReader(@"C:\Data\verkeersborden_actueel_beeld.csv"))
            {
                //id            // 0
                //ndwId         // 1
                //type          // 2
                //schemaVersion // 3
                //validated     // 4
                //validatedOn   // 5
                //userId        // 6
                //organisationId// 7
                //rvvCode       // 8
                //blackCode     // 9
                //textSigns     //10
                //latitude      //11
                //longitude     //12
                //x             //13
                //y             //14
                //placement ;   //15
                //side          //16
                //roadname      //17
                //roadtype      //18
                //roadnumber    //19
                //wvk_id        //20
                //countryname   //21
                //countrycode   //22
                //townname      //23
                //image         //24
                //firstSeen     //25
                //lastSeen      //26
                //removed       //27
                //publicationTimestamp  //28
                List<Tuple<double, double>> rd = new(); // coordinates
                //List<string> rvvCode = new();
                List<ObjectDefinition> objectDefinitions = new List<ObjectDefinition>();
                List<ObjectInstance> objectInstances = new List<ObjectInstance>();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();   // read csv file
                    // fix errors in file
                    StringBuilder builder = new StringBuilder(line);
                    builder.Replace("Z\n", "Z====Z"); // all lines should end with a Z. Mark those as correct
                    builder.Replace("\r", "<br>");    // the rest of the line end isn't correct
                    builder.Replace("\n", "<br>");    // idem
                    builder.Replace("Z====Z", "\n");  // this was the marker for the correct line end
                    line = builder.ToString();

                    var values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                    if (values[0] != "id")
                    {
                        int ry = 0;                     // rotation of vertical axis
                        if (values[16] == "W") ry =  900000000;
                        if (values[16] == "Z") ry = 1800000000;
                        if (values[16] == "O") ry = -900000000;
                        double x = Convert.ToDouble(values[13]); // position in RD in meters
                        double y = Convert.ToDouble(values[14]);
                        int GIMlat = RdToLat(x, y);
                        int GIMlong = RdToLong(x, y);
                        bool addZoneSign = false; // extra sign to indicate that the sign applies for a zone
                        string rvvCode = Convert.ToString(values[8]); // official type indicator for the sign
                        int definitionID = 0; // start on empty database
                        if (rvvCode.ToLower().IndexOf("zone") != -1)
                        {
                            addZoneSign = true;
                            rvvCode.Remove(rvvCode.Length - 6, 6);
                            definitionID = rvvCodes.IndexOf(rvvCode) + 2; // Todo: catch error if needed
                        }

                        if (addZoneSign)
                        {
                            var objZoneSignInst = new ObjectInstance()
                            {
                                Name = "Onderbord Zone", //zone sign
                                RegionID = "", // code of the file in 6 chars
                                ID = instanceID,
                                DefID = 1, // second definition
                                Px = (int)(x * 1000),
                                Py = (int)(y * 1000),
                                Pz = 1400, // TODO: add here the z-value of the surface
                                Ry = ry, // value to be expected between -1.800.000.000 and 1.800.000.000
                                GIMminlat = (int)GIMlat,
                                GIMmaxlat = GIMlat,
                                GIMminlong = GIMlong,
                                GIMmaxlong = GIMlong,
                                M = false,
                            };
                            objectInstances.Add(objZoneSignInst);
                            instanceID++;
                        }

                        var xy = Tuple.Create(x, y); // list of used locations
                        if (rd.IndexOf(xy) == -1) // only add when it's a new location
                        {
                            rd.Add(xy);
                            var objPoleInst = new ObjectInstance()
                            {
                                Name = "Paal voor verkeersbord", //pole
                                RegionID = "", // code of the file in 6 chars
                                ID = instanceID,
                                DefID = 0, // the first object
                                Px = (int)(x * 1000),
                                Py = (int)(y * 1000),
                                Pz = 0, // TODO: add here the z-value of the surface
                                Ry = ry, // value to be expected between -1.800.000.000 and 1.800.000.000
                                GIMminlat = GIMlat,
                                GIMmaxlat = GIMlat,
                                GIMminlong = GIMlong,
                                GIMmaxlong = GIMlong,
                                M = false,
                            };
                            objectInstances.Add(objPoleInst);
                            instanceID++;
                        }

                        var objSignInst = new ObjectInstance()
                        {
                            Name = "Verkeersbord " + rvvCode,
                            RegionID = "Verkeersborden NL", // code of the file
                            ID = instanceID,
                            DefID = definitionID,
                            Px = (int)(x * 1000),
                            Py = (int)(y * 1000),
                            Pz = 1600, // TODO: add here the z-value of the surface
                            Ry = ry, // value to be expected between -1.800.000.000 and 1.800.000.000
                            GIMminlat = GIMlat,
                            GIMmaxlat = GIMlat,
                            GIMminlong = GIMlong,
                            GIMmaxlong = GIMlong,
                            M = false,
                        };
                        objectInstances.Add(objSignInst);
                        instanceID++;
                    }
                }
                return instanceID;
            }
        }
    }
}