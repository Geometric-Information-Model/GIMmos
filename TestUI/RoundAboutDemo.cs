using GIMcompress;
using GIMcompress.Shrinkwrap;
using GIMtransfer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TestUI
{
    public class CityPlotCSV
    {
        public int Id;
        public double Px;
        public double Py;
        public string GimBinBase64;
        public GimData gimData;
        public JsonGimObject gimJson;

        public static CityPlotCSV FromCSV(string csvLine)
        {
            string[] values = csvLine.Replace("\"","").Split(',');
            CityPlotCSV plot = new();
            plot.Id = int.Parse(values[0]);
            plot.Px = double.Parse(values[1]);
            plot.Py = double.Parse(values[2]);
            plot.GimBinBase64 = values[3];

            return plot;
        }
    }

    public static class RoundAboutDemo
    {
       

        public static List<GimData> BatchProcessDXFGimDatas(List<GimData> gimDatas)
        {
            List<GimData> newGimDatas = new();

            foreach (var gimData in gimDatas)
            {
                GimData newGimData = new();
                Dictionary<int, List<GimElement>> elementsGroupedByMaterial = new();
                int baseElementIndex = 0;
                if (gimData.Elements[0].Elements.Count == 1)
                {
                    if (gimData.Elements[(int)gimData.Elements[0].Elements[0]].Elements.Count == 1)
                        baseElementIndex = (int)gimData.Elements[(int)gimData.Elements[0].Elements[0]].Elements[0];
                }

                for (int i = 0; i < gimData.Elements[baseElementIndex].Elements.Count; i++)
                {
                    uint childId = gimData.Elements[baseElementIndex].Elements[i];
                    GimElement partElement = gimData.Elements[(int)childId];
                    if (partElement.ElementType == GimElementType.D_part)
                    {
                        int materialIndex = -1;
                        if (partElement.Elements.Count == 0) continue;

                        uint firstChildIndex = partElement.Elements[0];
                        GimElement firstChild = gimData.Elements[(int)firstChildIndex];
                        if (firstChild.ElementType == GimElementType.Material)
                        {
                            materialIndex = (int)firstChildIndex;
                        }

                        if (!elementsGroupedByMaterial.ContainsKey(materialIndex))
                        {
                            elementsGroupedByMaterial.Add(materialIndex, new List<GimElement>());
                        }

                        var polygonList = elementsGroupedByMaterial[materialIndex];

                        for (int j = 0; j < partElement.Elements.Count; j++)
                        {
                            uint polyRepId = partElement.Elements[j];
                            GimElement polyRep = gimData.Elements[(int)polyRepId];
                            if (polyRep.ElementType == GimElementType.R_Poly)
                            {
                                for (int k = 0; k < polyRep.Elements.Count; k++)
                                {
                                    uint polygonId = polyRep.Elements[k];
                                    GimElement polygon = gimData.Elements[(int)polygonId];
                                    polygonList.Add(polygon);
                                }
                            }
                        }
                    }
                }

                newGimData.Elements.Add(new() { ElementType = GimElementType.D_root });
                int index = 1;

                foreach (var keyVal in elementsGroupedByMaterial)
                {
                    GimElement partElement = new() { ElementType = GimElementType.D_part };
                    GimElement polyRepElement = new() { ElementType = GimElementType.R_Poly };
                    GimElement material = new() { ElementType = GimElementType.Material };

                    int materialIndex = keyVal.Key;
                    if (materialIndex != -1)
                    {
                        material = gimData.Elements[materialIndex].Clone();
                        material.Locals = new();
                        material.Elements = new();
                    }

                    int partIndex = newGimData.Elements.Count;
                    newGimData.Elements.Add(partElement);
                    newGimData.Elements.Add(material);
                    newGimData.Elements.Add(polyRepElement);

                    partElement.Elements.Add((uint)partIndex + 1);
                    partElement.Elements.Add((uint)partIndex + 2);

                    polyRepElement.Locals.Add(new() { parent = (uint)partIndex });


                    material.Locals.Add(new() { parent = (uint)partIndex });

                    GimElement combinedPolygon = GIMtransfer.FileAccess.CombineMultiplePolygonsIntoOne(keyVal.Value);
                    combinedPolygon.Locals.Add(new() { parent = (uint)partIndex });
                    polyRepElement.Elements.Add((uint)partIndex + 3);
                    newGimData.Elements.Add(combinedPolygon);

                    newGimData.Objects.Add(new() { name = "Layer " + index, element = (uint)partIndex });
                    index++;

                    partElement.Locals.Add(new());
                    newGimData.Elements[0].Elements.Add((uint)partIndex);
                }

                if (newGimData.Elements.Count == 1)
                {
                    string debug = "bad gimdata!";
                }
                else
                {

                    newGimDatas.Add(newGimData);
                }

            }

            return newGimDatas;
        }

        public static List<GimData> ExtractRoundAboutsGimData(GimData roadGimData)
        {
            List<GimData> result = new();

            double radius = 65;

            List<Vect> roundAboutCoordinates = new() {
                new Vect(174429.391, 403013.031),
                new Vect(173502.922, 401316.750),
                new Vect(173833, 402288),
                new Vect(175120, 400077)
            };

            List<GimData> roundAboutGimDatas = new()
            {
                new GimData(),
                new GimData(),
                new GimData(),
                new GimData()
            };

            GimData leftOverGimData = roadGimData.Clone();

            for (int i = 0; i < roundAboutGimDatas.Count; i++)
            {
                roundAboutGimDatas[i] = roadGimData.Clone();
                foreach (GimElement element in roundAboutGimDatas[i].Elements)
                {
                    if (element.ElementType == GimElementType.P_polygon)
                    {
                        element.ElementDoubles = null;
                        element.ElementInts = null;
                    }
                }
            }

            bool TriangleWithin2DPoint(Triangle triangle, Vect point2D, double radius)
            {
                Vect vertex1 = triangle.Vertex1;
                Vect vertex2 = triangle.Vertex2;
                Vect vertex3 = triangle.Vertex3;

                double x = (vertex1[0] - point2D[0]);
                double y = (vertex1[1] - point2D[1]);

                double radiusSq = radius * radius;

                double distSq = x * x + y * y;
                if (distSq > radiusSq) return false;

                x = (vertex2[0] - point2D[0]);
                y = (vertex2[1] - point2D[1]);

                distSq = x * x + y * y;
                if (distSq > radiusSq) return false;

                x = (vertex3[0] - point2D[0]);
                y = (vertex3[1] - point2D[1]);

                distSq = x * x + y * y;

                if (distSq > radiusSq) return false;

                return true;
            }

            var totalTriangles = new List<Dictionary<uint, List<Triangle>>>();

            for (int i = 0; i < roundAboutCoordinates.Count; i++)
            {
                Vect roundAboutCenter = roundAboutCoordinates[i];
                var roundAboutTriangles = new Dictionary<uint, List<Triangle>>();
                totalTriangles.Add(roundAboutTriangles);
                for (uint elId = 0; elId < roadGimData.Elements.Count; elId++)
                {
                    GimElement element = roadGimData.Elements[(int)elId];
                    if (element.ElementType == GimElementType.P_polygon)
                    {
                        roundAboutTriangles.Add(elId, new());
                        var mesh = PolygonRounder.ConstructTriangles(element);
                        List<Triangle> triangles = mesh.Triangles;
                        foreach (Triangle triangle in triangles)
                        {

                            if (TriangleWithin2DPoint(triangle, roundAboutCenter, radius))
                            {
                                roundAboutTriangles[elId].Add(triangle);
                            }
                        }
                    }
                }

                foreach (var keypair in roundAboutTriangles)
                {
                    uint elId = keypair.Key;
                    var triangles = keypair.Value;
                    GimElement element = roundAboutGimDatas[i].Elements[(int)elId];
                    var mesh = new TriangleMesh();
                    mesh.Triangles = triangles;
                    mesh.ApplyToGimDataPolygon(element);
                }

            }

            for (uint elId = 0; elId < leftOverGimData.Elements.Count; elId++)
            {
                GimElement element = leftOverGimData.Elements[(int)elId];
                if (element.ElementType == GimElementType.P_polygon)
                {
                    List<Triangle> leftOverTriangles = new();
                    var mesh = PolygonRounder.ConstructTriangles(element);
                    List<Triangle> triangles = mesh.Triangles;
                    foreach (Triangle triangle in triangles)
                    {
                        bool leftOver = true;
                        for (int i = 0; i < roundAboutCoordinates.Count; i++)
                        {
                            var roundAboutCenter = roundAboutCoordinates[i];
                            if (TriangleWithin2DPoint(triangle, roundAboutCenter, radius))
                            {
                                leftOver = false; break;
                            }
                        }

                        if (leftOver)
                        {
                            leftOverTriangles.Add(triangle);
                        }

                    }

                    mesh.Triangles = leftOverTriangles;
                    mesh.ApplyToGimDataPolygon(element);
                }
            }



            result.AddRange(roundAboutGimDatas);
            result.Add(leftOverGimData);


            return result;
        }

        public static List<GimData> ExtractRoadSectionsHorizontal(GimData roadWithoutRoundAboutGimData)
        {

            double[] ySplits = new double[]
            {
                1000000,
                403063,
                403008,
                402314,
                402232,
                401288,
                400070,
                0
            };


            List<GimData> roadSections = new List<GimData>()
            {
                roadWithoutRoundAboutGimData.Clone(),
                roadWithoutRoundAboutGimData.Clone(),
                roadWithoutRoundAboutGimData.Clone(),
                roadWithoutRoundAboutGimData.Clone(),
                roadWithoutRoundAboutGimData.Clone(),
                roadWithoutRoundAboutGimData.Clone(),
                roadWithoutRoundAboutGimData.Clone(),
            };


            for (int i = 0; i < ySplits.Length - 1; i++)
            {
                var upperBound = ySplits[i];
                var lowerBound = ySplits[i + 1];
                var sectionGimData = roadSections[i];

                for(int elId = 0; elId < sectionGimData.Elements.Count; elId++)
                {
                    GimElement element = sectionGimData.Elements[elId];
                    if(element.ElementType == GimElementType.P_polygon)
                    {
                        var mesh = PolygonRounder.ConstructTriangles(element);
                        List<Triangle> trianglesInSection = new();
                        foreach(Triangle triangle in mesh.Triangles)
                        {
                            if (triangle.Vertex1[1] < upperBound && triangle.Vertex1[1] > lowerBound)
                            {
                                trianglesInSection.Add(triangle);
                            }
                        }
                        mesh.Triangles = trianglesInSection;
                        mesh.ApplyToGimDataPolygon(element);
                    }
                }
            }

            return roadSections;
        }

        public static List<GimData> ExtractRoadSectionVertical(GimData roadSectionGimData)
        {
            double[] xSplits = new double[]
            {
                1000000,
                175108,
                0
            };

            List<GimData> roadSections = new()
            {
                roadSectionGimData.Clone(),
                roadSectionGimData.Clone()
            };

            for (int i = 0; i < xSplits.Length - 1; i++)
            {
                var upperBound = xSplits[i];
                var lowerBound = xSplits[i + 1];
                var sectionGimData = roadSections[i];

                for (int elId = 0; elId < sectionGimData.Elements.Count; elId++)
                {
                    GimElement element = sectionGimData.Elements[elId];
                    if (element.ElementType == GimElementType.P_polygon)
                    {
                        var mesh = PolygonRounder.ConstructTriangles(element);
                        List<Triangle> trianglesInSection = new();
                        foreach (Triangle triangle in mesh.Triangles)
                        {
                            if (triangle.Vertex1[0] < upperBound && triangle.Vertex1[0] > lowerBound)
                            {
                                trianglesInSection.Add(triangle);
                            }
                        }
                        mesh.Triangles = trianglesInSection;
                        mesh.ApplyToGimDataPolygon(element);
                    }
                }
            }

            return roadSections;
        }

        public static void IntersectRoadWithPlot(List<CityPlotCSV> plots, GimData roadData, int startIndex, string objectName = "Layer 14")
        {
            var debug = false;
            List<Triangle> roadTriangles;
            List<List<Triangle>> elementTriangles = new();

            int elementId = 0;

            foreach(GimObject obj in roadData.Objects)
            {
                if(obj.Name == objectName)
                {
                    elementId = (int)obj.element;
                }
            }

            foreach (GimElement element in roadData.Elements)
            {
                if (element.ElementType == GimElementType.P_polygon)
                {
                    int parentPolyRep = (int)element.Locals[0].Parent;
                    int parentPart = (int)roadData.Elements[parentPolyRep].Locals[0].Parent;
                    if (parentPart != elementId) continue;

                    var mesh = PolygonRounder.ConstructTriangles(element);
                    elementTriangles.Add(mesh.Triangles);
                }
            }

            int triangleCount = 0;

            foreach(var triangleGroup in elementTriangles)
            {
                triangleCount += triangleGroup.Count;
            }

            roadTriangles = new(triangleCount);

            foreach (var triangleGroup in elementTriangles)
            {
                foreach(var triangle in triangleGroup)
                {
                    roadTriangles.Add(triangle);
                }
            }

            //List<Triangle> plotTriangles = new();

            //foreach (GimElement element in plotData.Elements)
            //{
            //    if (element.ElementType == GimElementType.P_polygon)
            //    {
            //        var mesh = PolygonRounder.ConstructTriangles(element);
            //        plotTriangles = mesh.Triangles;
            //    }
            //}

            Dictionary<Vertex2d, Vect> point2Dto3D = new();
            //List<Triangle2d> plot2dTriangles = Triangle2d.From3dTrianglesWithVertexDictionary(plotTriangles, point2Dto3D);
            List<Triangle2d> road2dTriangles = Triangle2d.From3dTrianglesWithVertexDictionary(roadTriangles, point2Dto3D);

            //Triangle2d.ConstructNeighborhouds(road2dTriangles);
            //Triangle2d.ConstructNeighborhouds(plot2dTriangles);
            Triangle2d.ConstructNeighborhouds(road2dTriangles);

            //var outsideTrianglesSegmentResult = GetOutsidePointsAndSegments(plot2dTriangles);

            //List<Segment2d> plotOutsideSegments = outsideTrianglesSegmentResult.Item1;
            //Dictionary<Vertex2d, Vertex2d> plotOutsidePoints = outsideTrianglesSegmentResult.Item2;

            var outsideTrianglesSegmentResult = GetOutsidePointsAndSegments(road2dTriangles);

            List<Segment2d> roadOutsideSegments = outsideTrianglesSegmentResult.Item1;
            Dictionary<Vertex2d, Vertex2d> roadOutsidePoints = outsideTrianglesSegmentResult.Item2;

            List<List<Vertex2d>> roadBorderGroups = new();

            foreach(var keypair in roadOutsidePoints)
            {
                var startingPoint = keypair.Value;
                if (startingPoint.IsOutside == true) continue;
                keypair.Key.IsOutside = true;

                startingPoint.IsOutside = true;

                List<Vertex2d> vertices = new();

                Vertex2d currentPoint = null;
                foreach(Vertex2d vertex in startingPoint.ConnectedVertices)
                {
                    currentPoint = vertex;
                    break;
                }
                if (currentPoint.IsOutside) continue;
                var previousPoint = startingPoint;
                vertices.Add(startingPoint);
                startingPoint.IsOutside = true;
                HashSet<Vertex2d> visitedVertices = new() { startingPoint };
                do
                {
                    vertices.Add(currentPoint);
                    visitedVertices.Add(currentPoint);
                    Vertex2d nextPoint = null;
                    foreach(Vertex2d vertex in currentPoint.ConnectedVertices)
                    {
                        if (vertex == previousPoint) continue;
                        nextPoint = vertex;
                        break;
                    }

                    currentPoint.IsOutside = true;
                    previousPoint = currentPoint;
                    currentPoint = nextPoint;
                    if (nextPoint == null) break;

                } while (currentPoint != startingPoint && !visitedVertices.Contains(currentPoint));

                roadBorderGroups.Add(vertices);
            }

            //Segment2d.debugPrintSegmentsDesmos(outsideSegments);

            //List<Segment2d> simplifiedPlotSegments = SimplifyOuterBorder(plotOutsideSegments);

            //Segment2d.debugPrintSegmentsDesmos(plotOutsideSegments, "", "-plot-full border");
            //Segment2d.debugPrintSegmentsDesmos(simplifiedPlotSegments, "" , "-plot-simplified border");

            int index = 0;
            List<Segment2d> allRoadSimplifiedSegments = new();
            List<List<Segment2d>> roadBorderSegmentGroups = new();
            List<Tuple<double, double, double, double>> roadBorderBoundaries = new();
            foreach(var roadBorderGroup in roadBorderGroups)
            {
                List<Segment2d> segments = new();
                segments.Add(new(roadBorderGroup[0], roadBorderGroup[^1], false));
                var previousPoint = roadBorderGroup[0];
                double minY = previousPoint.Y;
                double maxY = previousPoint.Y;
                double minX = previousPoint.X;
                double maxX = previousPoint.X;
                for (int i = 1; i < roadBorderGroup.Count; i++) 
                {
                    if (roadBorderGroup[i].X > maxX)
                    {
                        maxX = roadBorderGroup[i].X;
                    }
                    if (roadBorderGroup[i].X < minX)
                    {
                        minX = roadBorderGroup[i].X;
                    }
                    if (roadBorderGroup[i].Y > maxY)
                    {
                        maxY = roadBorderGroup[i].Y;
                    }
                    if (roadBorderGroup[i].Y < minY)
                    {
                        minY = roadBorderGroup[i].Y;
                    }
                    segments.Add(new(roadBorderGroup[i-1], roadBorderGroup[i], false));
                }
                List<Segment2d> SimplifiedRoadSegments = SimplifyOuterBorder(segments);

                allRoadSimplifiedSegments.AddRange(SimplifiedRoadSegments);
                roadBorderSegmentGroups.Add(segments);
                roadBorderBoundaries.Add(new(minX, minY, maxX, maxY));

                if (debug) Segment2d.debugPrintSegmentsDesmos(segments, index + "-", "-road-full border");
                if (debug) Segment2d.debugPrintSegmentsDesmos(SimplifiedRoadSegments, index + "-", "-road-simplified border");
                index++;
            }


            if(debug)Segment2d.debugPrintSegmentsDesmos(roadOutsideSegments, "", "ALL-road-full border-");
            if(debug)Segment2d.debugPrintSegmentsDesmos(allRoadSimplifiedSegments, "" ,"ALL-road-simplified border-");



            var offsetPoint = new Vertex2d(10000, 0);
            var pushDownVect = new Vect(0, 0, -1);
            for (int plotIndex = startIndex; plotIndex < plots.Count; plotIndex++)
            {
                var plot = plots[plotIndex];
                List<Triangle> plotTriangles = new();
                var plotData = plot.gimData;
                var plotOffsetVector = new Vect(plot.Px, plot.Py, 0);

                for (int elementIndex = 0; elementIndex < plotData.Elements.Count; elementIndex++)
                {
                    GimElement element = plotData.Elements[elementIndex];
                    if (element.ElementType == GimElementType.P_polygon)
                    {
                        var mesh = PolygonRounder.ConstructTriangles(element);
                        plotTriangles = mesh.Triangles;

                        foreach(var triangle in plotTriangles)
                        {
                            triangle.Vertex1 += plotOffsetVector;
                            triangle.Vertex2 += plotOffsetVector;
                            triangle.Vertex3 += plotOffsetVector;
                        }

                        bool changed = false;
                        double divisionThreshold = 2;

                        // subdividing triangles to make removed triangles as small as possible
                        for(int i=0; i< plotTriangles.Count; i++)
                        {
                            var triangle = plotTriangles[i];
                            bool splitTriangle = false;
                            Vect point1 = triangle.Vertex1;
                            Vect point2 = triangle.Vertex2;
                            Vect point3 = triangle.Vertex3;

                            for (int borderIndex = 0; borderIndex < roadBorderSegmentGroups.Count; borderIndex++)
                            {

                                bool noPointIn = true;

                                var roadBorderBoundary = roadBorderBoundaries[borderIndex];
                                var minX = roadBorderBoundary.Item1;
                                var minY = roadBorderBoundary.Item2;
                                var maxX = roadBorderBoundary.Item3;
                                var maxY = roadBorderBoundary.Item4;

                                if ((point1[0] > minX && point1[0] < maxX) && (point1[1] > minY && point1[1] < maxY))
                                {
                                    noPointIn = false;
                                    splitTriangle = true;
                                }
                                if (noPointIn)
                                {
                                    if ((point2[0] > minX && point2[0] < maxX) && (point2[1] < minY && point2[1] > maxY))
                                    {
                                        noPointIn = false;
                                        splitTriangle = true;
                                    }
                                }
                                if (noPointIn)
                                {
                                    if ((point3[0] > minX && point3[0] < maxX) && (point3[1] < minY && point3[1] > maxY))
                                    {
                                        noPointIn = false;
                                        splitTriangle = true;
                                    }
                                }

                                if (splitTriangle)
                                {
                                    break;
                                }
                            }

                            if (!splitTriangle) continue;
                            double len1 = (triangle.Vertex3 - triangle.Vertex2).Norm;
                            double len2 = (triangle.Vertex1 - triangle.Vertex3).Norm;
                            double len3 = (triangle.Vertex2 - triangle.Vertex1).Norm;
                            if(len1 > divisionThreshold || len2 > divisionThreshold || len3 > divisionThreshold)
                            {
                                Vect center1 = (triangle.Vertex3 + triangle.Vertex2) / 2;
                                Vect center2 = (triangle.Vertex1 + triangle.Vertex3) / 2;
                                Vect center3 = (triangle.Vertex1 + triangle.Vertex2) / 2;
                                //          1
                                //         / \
                                //       c3___c2
                                //       /\   /\
                                //       2__c1__3

                                Triangle triangle1 = new(point1, center3, center2);
                                Triangle triangle2 = new(point2, center1, center3);
                                Triangle triangle3 = new(point3, center2, center1);
                                Triangle triangle4 = new(center1, center2, center3);

                                plotTriangles.RemoveAt(i);
                                i--;
                                plotTriangles.Add(triangle1);
                                plotTriangles.Add(triangle2);
                                plotTriangles.Add(triangle3);
                                plotTriangles.Add(triangle4);

                            }
                        }

                        for(int i=0; i < plotTriangles.Count; i++)
                        {
                            var triangle = plotTriangles[i];
                            var point1 = new Vertex2d(triangle.Vertex1[0], triangle.Vertex1[1]);
                            var point2 = new Vertex2d(triangle.Vertex2[0], triangle.Vertex2[1]);
                            var point3 = new Vertex2d(triangle.Vertex3[0], triangle.Vertex3[1]);

                            var segment1 = new Segment2d(point1, point1 + offsetPoint);
                            var segment2 = new Segment2d(point2, point2 + offsetPoint);
                            var segment3 = new Segment2d(point3, point3 + offsetPoint);

                            bool remove = false;

                            for (int borderIndex = 0; borderIndex < roadBorderSegmentGroups.Count; borderIndex++)
                            {
                                var roadSegmentGroup = roadBorderSegmentGroups[borderIndex];
                                int intersectionCountP1 = 0;
                                int intersectionCountP2 = 0;
                                int intersectionCountP3 = 0;

                                bool noPointIn = true;

                                var roadBorderBoundary = roadBorderBoundaries[borderIndex];
                                var minX = roadBorderBoundary.Item1;
                                var minY = roadBorderBoundary.Item2;
                                var maxX = roadBorderBoundary.Item3;
                                var maxY = roadBorderBoundary.Item4;

                                if( (point1.X > minX && point1.X < maxX) && (point1.Y > minY && point1.Y < maxY) ) {
                                    noPointIn = false;
                                }
                                if (noPointIn)
                                {
                                    if ((point2.X > minX && point2.X < maxX) && (point2.Y < minY && point2.Y > maxY))
                                    {
                                        noPointIn = false;
                                    }
                                }
                                if (noPointIn)
                                {
                                    if ((point3.X > minX && point3.X < maxX) && (point3.Y < minY && point3.Y > maxY))
                                    {
                                        noPointIn = false;
                                    }
                                }

                                if (noPointIn)
                                {
                                    continue;
                                }

                                foreach (var roadSegment in roadSegmentGroup)
                                {
                                    if (segment1.Intersects(roadSegment))
                                    {
                                        intersectionCountP1++;
                                    }
                                    if (segment2.Intersects(roadSegment))
                                    {
                                        intersectionCountP2++;
                                    }
                                    if (segment3.Intersects(roadSegment))
                                    {
                                        intersectionCountP3++;
                                    }
                                }

                                int pointsIn = 0;

                                if(intersectionCountP1 % 2 == 1)
                                {
                                    pointsIn++;
                                }
                                if(intersectionCountP2 % 2 == 1)
                                {
                                    pointsIn++;
                                }
                                if(intersectionCountP3 % 2 == 1)
                                {
                                    pointsIn++;
                                }


                                if(pointsIn > 2)
                                {
                                    remove = true;
                                    changed = true;
                                    break;
                                }

                                else if(pointsIn > 1)
                                {
                                    changed = true;


                                    if (intersectionCountP1 % 2 == 1)
                                    {
                                        triangle.Vertex1 += pushDownVect;
                                    }
                                    else if (intersectionCountP2 % 2 == 1)
                                    {
                                        triangle.Vertex2 += pushDownVect;
                                    }
                                    else
                                    {
                                        triangle.Vertex3 += pushDownVect;
                                    }

                                    break;
                                }

                            }

                            if (remove)
                            {
                                plotTriangles.RemoveAt(i);
                                i--;
                            }
                        }

                        if (changed)
                        {

                            foreach (var triangle in plotTriangles)
                            {
                                triangle.Vertex1 -= plotOffsetVector;
                                triangle.Vertex2 -= plotOffsetVector;
                                triangle.Vertex3 -= plotOffsetVector;
                            }

                            mesh.Triangles = plotTriangles;
                            mesh.ApplyToGimDataPolygon(element);
                        }

                    }
                }

                FileAccess.WriteFromGimData(plot.gimData, "obj", "plots\\plot-after-cut-" + plot.Id, true, null);

            }
        }

        private static Tuple<List<Segment2d>, Dictionary<Vertex2d, Vertex2d>> GetOutsidePointsAndSegments(List<Triangle2d> triangles2d)
        {
            List<Triangle2d> outsideTriangles = new();
            foreach (Triangle2d triangle in triangles2d)
            {
                if (triangle.MissingNeighboursOnOneSide())
                {
                    outsideTriangles.Add(triangle);
                }
            }

            //Triangle2d.debugPrintTrianglesDesmos(plotOutsideTriangles);

            Dictionary<Vertex2d, Vertex2d> outsidePoints = new();
            List<Segment2d> outsideSegments = new();

            for (int i = 0; i < outsideTriangles.Count; i++)
            {
                Triangle2d triangle = outsideTriangles[i];

                if (triangle.Neighbour1.Count == 0)
                {
                    Vertex2d pointA = triangle.Vertex2;
                    Vertex2d pointB = triangle.Vertex3;
                    if (outsidePoints.ContainsKey(pointA))
                    {
                        pointA = outsidePoints[pointA];
                    }
                    else
                    {
                        outsidePoints.Add(pointA, pointA);
                    }

                    if (outsidePoints.ContainsKey(pointB))
                    {
                        pointB = outsidePoints[pointB];
                    }
                    else
                    {
                        outsidePoints.Add(pointB, pointB);
                    }

                    outsideSegments.Add(new Segment2d(pointA, pointB));
                }

                if (triangle.Neighbour2.Count == 0)
                {
                    Vertex2d pointA = triangle.Vertex3;
                    Vertex2d pointB = triangle.Vertex1;
                    if (outsidePoints.ContainsKey(pointA))
                    {
                        pointA = outsidePoints[pointA];
                    }
                    else
                    {
                        outsidePoints.Add(pointA, pointA);
                    }

                    if (outsidePoints.ContainsKey(pointB))
                    {
                        pointB = outsidePoints[pointB];
                    }
                    else
                    {
                        outsidePoints.Add(pointB, pointB);
                    }

                    outsideSegments.Add(new Segment2d(pointA, pointB));
                }

                if (triangle.Neighbour3.Count == 0)
                {
                    Vertex2d pointA = triangle.Vertex1;
                    Vertex2d pointB = triangle.Vertex2;
                    if (outsidePoints.ContainsKey(pointA))
                    {
                        pointA = outsidePoints[pointA];
                    }
                    else
                    {
                        outsidePoints.Add(pointA, pointA);
                    }

                    if (outsidePoints.ContainsKey(pointB))
                    {
                        pointB = outsidePoints[pointB];
                    }
                    else
                    {
                        outsidePoints.Add(pointB, pointB);
                    }

                    outsideSegments.Add(new Segment2d(pointA, pointB));
                }

            }

            return new(outsideSegments, outsidePoints);
        }

        private static List<Segment2d> SimplifyOuterBorder(List<Segment2d> outsideSegments, double threshold = -0.996)
        {
            Vertex2d startingPoint = outsideSegments[0].pointA;
            Vertex2d previousPoint = startingPoint;
            Vertex2d previousAnchorPoint = startingPoint;
            Vertex2d currentPoint = outsideSegments[0].pointB;

            List<Vertex2d> simplifiedBorder = new();
            simplifiedBorder.Add(new Vertex2d(previousPoint.X, previousPoint.Y));
            HashSet<Vertex2d> visitedVertices = new() { startingPoint };

            do
            {
                Vertex2d nextPoint = null;
                visitedVertices.Add(currentPoint);
                foreach (Vertex2d vertex in currentPoint.ConnectedVertices)
                {
                    if (vertex == previousPoint) continue;
                    nextPoint = vertex;
                    break;
                }

                if (nextPoint == null)
                {
                    string debugErr = "err";
                    break;
                }

                Vect vectA = (Vect)currentPoint - (Vect)previousAnchorPoint;
                Vect vectB = (Vect)currentPoint - (Vect)nextPoint;

                double dot = vectA * vectB / (vectA.Norm * vectB.Norm);

                if (dot > threshold)
                {
                    simplifiedBorder.Add(new Vertex2d(currentPoint.X, currentPoint.Y));
                    previousAnchorPoint = currentPoint;
                }

                previousPoint = currentPoint;
                currentPoint = nextPoint;

            } while (currentPoint != startingPoint && !visitedVertices.Contains(currentPoint));

            //Segment2d.debugPrintSegmentsDesmos(outsideSegments);

            List<Segment2d> simplifiedBorderSegments = new();

            Vertex2d previousVertex = simplifiedBorder[^1];
            foreach (Vertex2d simplifiedBorderVertex in simplifiedBorder)
            {
                simplifiedBorderSegments.Add(new Segment2d(previousVertex, simplifiedBorderVertex));
                previousVertex = simplifiedBorderVertex;
            }

            return simplifiedBorderSegments;
        }

        public static GimData CenterPolygonsAroundZero(GimData gimData, Vect offset)
        {
            int vertexCount = 0;

            foreach(GimElement element in gimData.Elements)
            {
                if(element.ElementType == GimElementType.P_polygon)
                {
                    vertexCount += element.ElementDoubles.Length;
                }
            }

            List<Vect> vertices = new(vertexCount);

            foreach (GimElement element in gimData.Elements)
            {
                if (element.ElementType == GimElementType.P_polygon)
                {
                    for (int i = 0; i + 2 < element.ElementDoubles.Length; i +=3)
                    {
                        vertices.Add(new Vect(
                            element.ElementDoubles[i],
                            element.ElementDoubles[i + 1],
                            element.ElementDoubles[i + 2]
                            ));
                    }
                }
            }

            Vect averageVertex = Vect.GetAveragePoint(vertices) + offset;


            foreach (GimElement element in gimData.Elements)
            {
                if (element.ElementType == GimElementType.P_polygon)
                {
                    for (int i = 0; i + 2 < element.ElementDoubles.Length; i += 3)
                    {
                        element.ElementDoubles[i] -= averageVertex[0];
                        element.ElementDoubles[i + 1] -= averageVertex[1];
                    }
                }
            }


            return gimData;
        }

    }
}