using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace StartCityJson
{
    public static class GeographicalHelpers
    {
        public static (int, int) RdToGIMLatLong(double X, double Y)
        {
            return (RdToGIMlat(X, Y), RdToGIMlong(X, Y));
        }
        /// <summary>
        /// Returns GIMlong value from XY in Meters
        /// </summary>
        /// <param name="X">X in meters</param>
        /// <param name="Y">Y in meters</param>
        /// <returns></returns>
        public static int RdToGIMlong(double X, double Y)
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
        /// <summary>
        /// Returns GIMlat value from XY in Meters
        /// </summary>
        /// <param name="X">X in meters</param>
        /// <param name="Y">Y in meters</param>
        /// <returns></returns>
        public static int RdToGIMlat(double X, double Y)
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

        /// <summary>
        /// Returns the RD X Value (in meters) from a given Long Lat
        /// </summary>
        /// <param name="Lat">Latitude in Degrees</param>
        /// <param name="Long">Longitude in Degrees</param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the RD Y Value (in meters) from a given Long Lat
        /// </summary>
        /// <param name="Lat">Latitude in Degrees</param>
        /// <param name="Long">Longitude in Degrees</param>
        /// <returns></returns>
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

        public static double GIMLongLatToX(double GIMlat, double GIMlong)
        {
            double Lat = (GIMlat / 40075000) * 360;
            double Long = (GIMlong / 40075000) * 360;

            return LongLatToX(Lat, Long);
        }

        public static double GIMLongLatToY(double GIMlat, double GIMlong)
        {
            double Lat = (GIMlat / 40075000) * 360;
            double Long = (GIMlong / 40075000) * 360;

            return LongLatToY(Lat, Long);
        }

        internal static string GetRegionIDContainingObjectXY(
            List<List<RegionBoundaries>> regionBoundariesMatrix, Vect point)
        {
            return GetRegionIDContainingObjectXY(regionBoundariesMatrix, point[0], point[1]);
        }
        internal static string GetRegionIDContainingObjectXY(
            List<List<RegionBoundaries>> regionBoundariesMatrix, double x, double y)

        {
            string regionID = "";

            int smallestX = regionBoundariesMatrix[0][0].centerX;
            int biggestX = regionBoundariesMatrix[^1][0].centerX;

            int xIndex;
            //x *= 1000;
            //y *= 1000;
            //gimlong *= 1000;
            //gimlat *= 1000;
            // we're going to binary search the region By X first
            if (x <= smallestX)
            {
                xIndex = 0;
            }
            else if (x >= biggestX)
            {
                xIndex = regionBoundariesMatrix.Count - 1;
            }

            else xIndex = GetRegionListByXRecursively(x, 0, regionBoundariesMatrix.Count, regionBoundariesMatrix);



            // binary search doesn't find the closest element, it finds the proper index slot
            // so both situations below will return the same index, even though it's wrong:
            //  
            // Region A <=============== obj ==> Region B
            // Region A <==== obj =============> Region B

            int xAtIndex = regionBoundariesMatrix[xIndex][0].centerX;
            int xDiff = (int)Math.Abs(x - xAtIndex);
            bool beforeIsSmaller = false;
            if (xIndex > 0)
            {
                int xBefore = regionBoundariesMatrix[xIndex - 1][0].centerX;
                int xDiffBefore = (int)Math.Abs(x - xBefore);
                if (xDiffBefore < xDiff)
                {
                    xAtIndex = xBefore;
                    xIndex = xIndex - 1;
                    beforeIsSmaller = true;
                }
            }

            if (xIndex < regionBoundariesMatrix.Count - 1 && !beforeIsSmaller)
            {
                int xAfter = regionBoundariesMatrix[xIndex + 1][0].centerX;
                int xDiffAfter = (int)Math.Abs(x - xAfter);
                if (xDiffAfter < xDiff)
                {
                    xAtIndex = xAfter;
                    xIndex = xIndex + 1;
                }
            }

            // after finding the sublist by index X, we do the same algorithm for Y

            var xRegionList = regionBoundariesMatrix[xIndex];

            int minGimLatDifference = int.MaxValue;
            //RegionBoundaries minRegion = null;
            //foreach(var region in xRegionList)
            //{
            //    int regionCenterLat = (region.GIMminlat + region.GIMmaxlat) / 2;
            //    int latDifference = (int)Math.Abs(gimLat - regionCenterLat);

            //    if(latDifference< minGimLatDifference)
            //    {
            //        minGimLatDifference = latDifference;
            //        minRegion = region;
            //    }
            //}

            //return minRegion.ID;

            int smallestY = xRegionList[0].centerY;
            int biggestY = xRegionList[^1].centerY;

            int yIndex;
            if (y <= smallestY)
            {
                yIndex = 0;
            }
            else if (y >= biggestY)
            {
                yIndex = xRegionList.Count - 1;
            }

            else yIndex = GetRegionByYRecursively(y, 0, xRegionList.Count, xRegionList);

            beforeIsSmaller = false;
            int yAtIndex = xRegionList[yIndex].centerY;
            int yDiff = (int)Math.Abs(y - yAtIndex);

            if (yIndex > 0)
            {
                int yBefore = xRegionList[yIndex - 1].centerY;
                int yDiffBefore = (int)Math.Abs(y - yBefore);
                if (yDiffBefore < yDiff)
                {
                    yAtIndex = yBefore;
                    yIndex = yIndex - 1;
                    beforeIsSmaller = true;
                }
            }

            if (yIndex < xRegionList.Count - 1 && !beforeIsSmaller)
            {
                int yAfter = xRegionList[yIndex + 1].centerY;
                int yDiffAfter = (int)Math.Abs(y - yAfter);
                if (yDiffAfter < yDiff)
                {
                    yAtIndex = yAfter;
                    yIndex = yIndex + 1;
                }
            }

            regionID = xRegionList[yIndex].ID;

            return regionID;
        }


        internal static List<List<RegionBoundaries>> GroupRegionBoundariesYX(List<RegionBoundaries> regionBoundaries)
        {
            Dictionary<int, List<RegionBoundaries>> yToRegionsDict = new();
            foreach (RegionBoundaries regionBoundary in regionBoundaries)
            {

                int x = (regionBoundary.minX + regionBoundary.maxX) / 2;
                int y = (regionBoundary.minY + regionBoundary.maxY) / 2;
                if (!yToRegionsDict.ContainsKey(y))
                {
                    yToRegionsDict[y] = new();
                }
                yToRegionsDict[y].Add(regionBoundary);

                regionBoundary.centerX = x;
                regionBoundary.centerY = y;
            }

            List<List<RegionBoundaries>> regionsByY = new();
            foreach (var yRegionSet in yToRegionsDict)
            {
                regionsByY.Add(yRegionSet.Value);
            }

            // we sort the big list by Y
            regionsByY.Sort((List<RegionBoundaries> a, List<RegionBoundaries> b) =>
            {
                if (a[0].centerY > b[0].centerY) return 1;
                if (a[0].centerY < b[0].centerY) return -1;
                return 0;
            });
        
            // we sort the smaller inner lists by X
            foreach (var regionList in regionsByY)
            {
                regionList.Sort((RegionBoundaries a, RegionBoundaries b) =>
                {
                    if (a.centerX > b.centerX) return 1;
                    if (a.centerX < b.centerX) return -1;
                    return 0;
                });
            }


            int count = 0;

            List<List<RegionBoundaries>> regionBoundariesMatrix = new();

            regionBoundariesMatrix = regionsByY;
            foreach (var regionArr in regionBoundariesMatrix)
                foreach (var region in regionArr)
                    if (region != null)
                        count++;

            return regionBoundariesMatrix;

        }


        internal static List<List<RegionBoundaries>> GroupRegionBoundariesXY(List<RegionBoundaries> regionBoundaries)
        {
            //int minLong = int.MaxValue, minLat = int.MaxValue, maxLong = int.MinValue, maxLat = int.MinValue;

            Dictionary<int, List<RegionBoundaries>> xToRegionsDict = new();
            foreach (RegionBoundaries regionBoundary in regionBoundaries)
            {

                int x = (regionBoundary.minX + regionBoundary.maxX) / 2;
                int y = (regionBoundary.minY + regionBoundary.maxY) / 2;
                if (!xToRegionsDict.ContainsKey(x))
                {
                    xToRegionsDict[x] = new();
                }
                xToRegionsDict[x].Add(regionBoundary);

                regionBoundary.centerX = x;
                regionBoundary.centerY = y;
                //if (x < minLong) minLong = x;
                //if (y < minLat) minLat = y;
                //if (x > maxLong) maxLong = x;
                //if (y > maxLat) maxLat = y;

            }

            List<List<RegionBoundaries>> regionsByX = new();
            foreach (var xRegionSet in xToRegionsDict)
            {
                regionsByX.Add(xRegionSet.Value);
            }

            // we sort the big list by X
            regionsByX.Sort((List<RegionBoundaries> a, List<RegionBoundaries> b) =>
            {
                if (a[0].centerX > b[0].centerX) return 1;
                if (a[0].centerX < b[0].centerX) return -1;
                return 0;
            });

            // we sort the smaller inner lists by Y
            foreach (var regionList in regionsByX)
            {
                regionList.Sort((RegionBoundaries a, RegionBoundaries b) =>
                {
                    if (a.centerY > b.centerY) return 1;
                    if (a.centerY < b.centerY) return -1;
                    return 0;
                });
            }


            int count = 0;

            List<List<RegionBoundaries>> regionBoundariesMatrix = new();

            regionBoundariesMatrix = regionsByX;
            foreach (var regionArr in regionBoundariesMatrix)
                foreach (var region in regionArr)
                    if (region != null)
                        count++;

            return regionBoundariesMatrix;

        }

        // ============================ Private Functions

        private static int GetRegionListByXRecursively(double x, int startIndex, int endIndex, List<List<RegionBoundaries>> regionBoundariesMatrix)
        {
            if (startIndex == endIndex - 1) return startIndex;
            if (startIndex == endIndex) return startIndex;

            int middleIndex = (startIndex + endIndex) / 2;

            var middleList = regionBoundariesMatrix[middleIndex];

            if (middleList[0].centerX == x) return middleIndex;
            if (middleList[0].centerX < x)
            {
                return GetRegionListByXRecursively(x, middleIndex + 1, endIndex, regionBoundariesMatrix);
            }

            return GetRegionListByXRecursively(x, startIndex, middleIndex, regionBoundariesMatrix);

        }

        private static int GetRegionByYRecursively(double y, int startIndex, int endIndex, List<RegionBoundaries> list)
        {
            if (startIndex == endIndex - 1) return startIndex;
            if (startIndex == endIndex) return startIndex;
            int middleIndex = (startIndex + endIndex) / 2;

            var middleRegion = list[middleIndex];

            if (middleRegion.centerY == y) return middleIndex;
            if (middleRegion.centerY < y)
            {
                return GetRegionByYRecursively(y, middleIndex + 1, endIndex, list);
            }

            return GetRegionByYRecursively(y, startIndex, middleIndex, list);
        }
    }
}
