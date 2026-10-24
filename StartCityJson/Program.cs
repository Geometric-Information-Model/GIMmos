// See https://aka.ms/new-console-template for more information
using GimBin;
using GIMcompress;
using Microsoft.EntityFrameworkCore;
using NLog.Fluent;
using StartCityJson;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using FileAccess = GIMtransfer.FileAccess;
[assembly: InternalsVisibleTo("GIMmapsData"), InternalsVisibleTo("GIMnetwork"), InternalsVisibleTo("GimHeightMapData")]
namespace CityJson
{
    public class Programm
    {
        public static void Main(string[] args)
        {
            ReadCityJson1_2(args);
        }

        public static void FixRegionsFlippedByY()
        {
            bool update = true;
            var connectionString = DemoDBContext.GetBackendConnectionString();

            HashSet<string> incorrectRegionIDs = new();

            using (var db = new CityJsonDBContext(connectionString))
            {

                Dictionary<string, RegionBoundaries> regionsById = new();

                foreach (var region in db.RegionBoundaries)
                {
                    regionsById.Add(region.ID, region);
                }
                foreach(var region in regionsById.Values) 
                { 

                    string regionID = region.ID;
                    int suffix = int.Parse(regionID[^1].ToString());

                    int pairSuffix = suffix + 2;
                    if (pairSuffix > 4) pairSuffix -= 4;

                    string pairRegionID = regionID[0..^1] + pairSuffix;

                    var pairRegion = regionsById[pairRegionID];

                    int minY = region.minY/1000;
                    int maxY = region.maxY/1000;

                    int pairMinY = pairRegion.minY/1000;
                    int pairMaxY = pairRegion.maxY/1000;

                    int calculatedMinY = (int)GeographicalHelpers.GIMLongLatToY(region.GIMminlat, region.GIMminlong);
                    int calculatedMaxY = (int)GeographicalHelpers.GIMLongLatToY(region.GIMmaxlat, region.GIMmaxlong);

                    int pairDiffMinY = Math.Abs(calculatedMinY - pairMinY);
                    int pairDiffMaxY = Math.Abs(calculatedMaxY - pairMaxY);

                    if(pairDiffMinY < 10 && pairDiffMaxY < 10)
                    {
                        incorrectRegionIDs.Add(regionID);
                    }

                }
                HashSet<string> fixedRegions = new();
                foreach(var regionID in incorrectRegionIDs)
                {
                    if (fixedRegions.Contains(regionID)) continue;

                    var region = db.RegionBoundaries.Find(regionID);
                    int suffix = int.Parse(regionID[^1].ToString());
                    int pairSuffix = suffix + 2;
                    if (pairSuffix > 4) pairSuffix -= 4;
                    string pairRegionID = regionID[0..^1] + pairSuffix;
                    var pairRegion = db.RegionBoundaries.Find(pairRegionID);

                    fixedRegions.Add(regionID);
                    fixedRegions.Add(pairRegionID);

                    int tempMinY = region.minY;
                    int tempMaxY = region.maxY;
                    region.minY = pairRegion.minY;
                    region.maxY = pairRegion.maxY;
                    pairRegion.minY = tempMinY;
                    pairRegion.maxY = tempMaxY;

                    if (update)
                    {
                        db.SaveChanges();
                    }
                }

            }


        }

        public static void DeleteMatchedBuildings()
        {
            string fileLocation =
                @"D:\GIMmaps\cityjson 2.2 statS IDs and positions\LOD 1.2 and 2.2 matching\OldToNewIDsByArea - 2.csv";
            string[] lines = File.ReadAllLines(fileLocation);
            HashSet<int> definitionIDsToMarkForDeletion = new();
            HashSet<int> instanceIDsToMarkForDeletion = new();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                string[] parts = line.Split(";");
                int newID = int.Parse(parts[0]);
                int oldID = int.Parse(parts[1]);

                definitionIDsToMarkForDeletion.Add(oldID+154);
                instanceIDsToMarkForDeletion.Add(oldID);
            }

            Console.WriteLine("Finished reading IDs from file");

            var connectionString = DemoDBContext.GetBackendConnectionString();
            Console.Write("Fetching relevant instances from DB...");
            List<ObjectInstance> instancesToModify = new(6000000);
            int count = 0;

            using (var db = new CityJsonDBContext(connectionString))
            {
                foreach (var objInst in db.ObjectInstances)
                {
                    count++;
                    if (count % 1000000 == 0) Console.WriteLine("Instance " + count + " out of ~50000000");
                    if (objInst.ID > 50000000) break;
                    if (instanceIDsToMarkForDeletion.Contains(objInst.ID))
                    {
                        instancesToModify.Add(objInst);
                    }
                }
            }

            Console.WriteLine("Done");
            Console.Write("Fetching relevant definitions from DB...");
            count = 0;
            List<ObjectDefinition> definitionsToModify = new();
            using (var db = new CityJsonDBContext(connectionString))
            {
                foreach (var objDef in db.ObjectDefinitions)
                {
                    count++;
                    if (count % 1000000 == 0) Console.WriteLine("Definition " + count + " out of ~50000000");
                    if (objDef.ID > 50000000) break;
                    if (definitionIDsToMarkForDeletion.Contains(objDef.ID))
                    {
                        definitionsToModify.Add(objDef);
                    }
                }
            }
            Console.WriteLine("Done");
            instancesToModify.ForEach(instance => instance.DefID = -1);

            bool error = definitionsToModify.Any(definition => definition.Type != "building");
            if (error)
            {
                string debug = "cancel";
            }

            definitionsToModify.ForEach(definition => definition.Type = "delete");
            Console.WriteLine("Modified objectDefs and Instances in memory");
            Console.Write("Uploading changes to DB...");
            using (var db = new CityJsonDBContext(connectionString))
            {
                db.ObjectDefinitions.BulkUpdate(definitionsToModify);
                Console.Write("Definitions Done...");
                db.ObjectInstances.BulkUpdate(instancesToModify);
                Console.WriteLine("Instances Done!");
            }

        }
        public static void MatchLOD1_2And2_2Buildings(string[] args)
        {
            List<RegionBoundaries> regions = new List<RegionBoundaries>();

            var connectionString = DemoDBContext.GetBackendConnectionString();
            using(var db = new CityJsonDBContext(connectionString))
            {
                regions = db.RegionBoundaries.ToList();
            }

            string outputPath = @"D:\GIMmaps\cityjson 2.2 statS IDs and positions\LOD 1.2 and 2.2 matching\";
            int index = 0;
            int IDThreshold = 50000000;

            bool getMatchesByArea = true;
            bool getMatchesByName = false;
            bool debugStats = false;

            HashSet<int> alreadyMatched = new();
            string[] matchedLines = File.ReadAllLines(outputPath + "OldToNewIDsByName.csv");

            for (int i = 1; i < matchedLines.Length; i++) 
            {
                var line = matchedLines[i];
                var idString = line.Split(';')[0];
                int matchedID = int.Parse(idString);
                alreadyMatched.Add(matchedID);
            }

            StringBuilder oldIDsToNewIds = new();
            oldIDsToNewIds.AppendLine("newID:oldID:oldName:newID;Confidence");
            foreach (RegionBoundaries region in regions)
            {
                string regionID = region.ID;
                StringBuilder newToOldStats = new();

                index++;
                Console.WriteLine($"processing region {regionID} (index {index} out of {regions.Count})");
                if (debugStats) newToOldStats.AppendLine("NewID;Name;Px;Py;Pz;Ry;Width;Length;OldID;Name;Px;Py;Pz;Ry;Width;Length;confidence");
                int matchCount = 0;
                using (var db = new CityJsonDBContext(connectionString))
                {
                    var queryRes = db.ObjectInstances.FromSql($"SELECT * FROM `ObjectInstance` a WHERE a.regionID = {regionID} And exists (Select b.type from ObjectDefinition b where b.ID = a.DefID and b.type = \"building\");");

                    List<ObjectInstance> oldObjectInstances = new();
                    List<ObjectInstance> newObjectInstances = new();

                    Dictionary<string, ObjectInstance> oldObjectInstancesByName = new();

                    var debug = queryRes.ToList();
                    Dictionary<int, int> newToOldInstances = new();
                    Dictionary<int, int> matchConfidence = new();

                    foreach (var objectInstance in queryRes)
                    {
                        if (objectInstance.ID < IDThreshold)
                        {
                            oldObjectInstances.Add(objectInstance);
                            oldObjectInstancesByName[objectInstance.Name] = objectInstance;
                        }
                        else
                        {
                            newObjectInstances.Add(objectInstance);
                        }
                    }

                    if (getMatchesByName)
                    {
                        if (newObjectInstances.Count == 0) continue;

                        // first part of matching old files and new files is simply by name
                        List<ObjectInstance> remainingNewObjectInstances = new();


                        foreach (var newInst in newObjectInstances)
                        {
                            // the objects have a name that is NL.IMBAG.<numberSerial>, we only need the last part.
                            string objectSerialName = newInst.Name.Split(".")[^1];

                            if (oldObjectInstancesByName.ContainsKey(objectSerialName))
                            { // found an object by the same name
                                var oldInst = oldObjectInstancesByName[objectSerialName];
                                newToOldInstances.Add(newInst.ID, oldInst.ID);
                                if (debugStats) matchConfidence.Add(newInst.ID, 100);

                                //"newID:oldID:oldName:newID"
                                oldIDsToNewIds.AppendLine(newInst.ID + ";" + oldInst.ID + ";"
                                    + newInst.Name + ";" + oldInst.Name);
                                matchCount++;
                                //"NewID;Name;Px;Py;Pz;Ry;Width;Length;OldID;Name;Px;Py;Pz;Ry;Width;Length;confidence"
                                if (debugStats) newToOldStats.Append($"{newInst.ID};{newInst.Name};" +
                                    $"{newInst.Px};{newInst.Py};{newInst.Pz};{newInst.Ry};-;-;");
                                if (debugStats) newToOldStats.AppendLine($"{oldInst.ID};{oldInst.Name};" +
                                    $"{oldInst.Px};{oldInst.Py};{oldInst.Pz};{oldInst.Ry};-;-;100%");
                            }
                            else
                            { // found no object by the same name
                                remainingNewObjectInstances.Add(newInst);
                            }
                        }
                    }

                    // the second part of matching is done by location
                    // we first organize the original buildings in an easily accessible grid
                    if (getMatchesByArea)
                    {
                        int regionMinX = region.minX, regionMinY = region.minY, regionMaxX = region.maxX, regionMaxY = region.maxY;

                        int regionSizeX = regionMaxX - regionMinX;
                        int regionSizeY = regionMaxY - regionMinY;

                        List<ObjectInstance>[][] oldObjectInstanceGrid = new List<ObjectInstance>[8][];
                        for (int i = 0; i < 8; i++)
                        {
                            oldObjectInstanceGrid[i] = new List<ObjectInstance>[8];

                            for (int j = 0; j < 8; j++)
                            {
                                oldObjectInstanceGrid[i][j] = new();
                            }
                        }

                        foreach (var oldInstance in oldObjectInstances)
                        {
                            int x = oldInstance.Px;
                            int y = oldInstance.Py;

                            // we get which index in the grid this objectInstance will belong to
                            int xIndex = ((x - regionMinX) * 8) / regionSizeX;
                            int yIndex = ((y - regionMinY) * 8) / regionSizeY;

                            // making sure indexes are between 0 and 7
                            xIndex = Math.Min(xIndex, 7);
                            xIndex = Math.Max(xIndex, 0);

                            yIndex = Math.Min(yIndex, 7);
                            yIndex = Math.Max(yIndex, 0);

                            oldObjectInstanceGrid[xIndex][yIndex].Add(oldInstance);
                        }

                        foreach (var newInst in newObjectInstances)
                        {
                            if (alreadyMatched.Contains(newInst.ID)) continue;
                            if (newInst.ID == 50501905 || newInst.ID == 50502324) 
                            {
                                string breakpoint = "debug";
                            }
                            int x = newInst.Px;
                            int y = newInst.Py;

                            int maxLat = newInst.GIMmaxlat, maxLong = newInst.GIMmaxlong;
                            int minLat = newInst.GIMminlat, minLong = newInst.GIMminlong;

                            // we calculate the min/max because an object could belong to two instances (on the edge)
                            int maxX = (int)GeographicalHelpers.GIMLongLatToX(maxLat, maxLong) * 1000;
                            int maxY = (int)GeographicalHelpers.GIMLongLatToY(maxLat, maxLong) * 1000;

                            int minX = (int)GeographicalHelpers.GIMLongLatToX(minLat, minLong) * 1000;
                            int minY = (int)GeographicalHelpers.GIMLongLatToY(minLat, minLong) * 1000;

                            int xIndex = ((x - regionMinX) * 8) / regionSizeX;
                            int yIndex = ((y - regionMinY) * 8) / regionSizeY;

                            xIndex = Math.Min(xIndex, 7);
                            xIndex = Math.Max(xIndex, 0);

                            yIndex = Math.Min(yIndex, 7);
                            yIndex = Math.Max(yIndex, 0);

                            int maxXIndex = ((maxX - regionMinX) * 8) / regionSizeX;
                            int maxYIndex = ((maxY - regionMinY) * 8) / regionSizeY;

                            maxXIndex = Math.Min(maxXIndex + 1, 7);
                            maxXIndex = Math.Max(maxXIndex, 0);

                            maxYIndex = Math.Min(maxYIndex + 1, 7);
                            maxYIndex = Math.Max(maxYIndex, 0);

                            int minXIndex = ((minX - regionMinX) * 8) / regionSizeX;
                            int minYIndex = ((minY - regionMinY) * 8) / regionSizeY;

                            minXIndex = Math.Min(minXIndex, 7);
                            minXIndex = Math.Max(minXIndex - 1, 0);

                            minYIndex = Math.Min(minYIndex, 7);
                            minYIndex = Math.Max(minYIndex - 1, 0);

                            double minDistance = double.PositiveInfinity;
                            ObjectInstance closestInst = null;
                            for (int i = minXIndex; i <= maxXIndex; i++)
                            {
                                for (int j = minYIndex; j <= maxYIndex; j++)
                                {
                                    
                                    var objList = oldObjectInstanceGrid[i][j];
                                    foreach (var oldInstance in objList)
                                    {
                                        if (oldInstance.Name.Length < 20) 
                                            // has a short ID = building ID. In this case,
                                            // it probably matches another building with the same name
                                            continue;
                                        double xDiff = Math.Abs(x - oldInstance.Px);
                                        double yDiff = Math.Abs(y - oldInstance.Py);

                                        double distance = xDiff * xDiff + yDiff * yDiff;

                                        if (distance < minDistance)
                                        {
                                            minDistance = distance;
                                            closestInst = oldInstance;
                                        }
                                    }
                                }
                            }

                            if (closestInst == null)
                            {
                                //"NewID;Name;Px;Py;Pz;Ry;Width;Length;OldID;Name;Px;Py;Pz;Ry;Width;Length;confidence"
                                if (debugStats) newToOldStats.Append($"{newInst.ID};{newInst.Name};" +
                                    $"{newInst.Px};{newInst.Py};{newInst.Pz};{newInst.Ry};-;-;");
                                if (debugStats) newToOldStats.AppendLine($"-;-;" +
                               $"-;-;-;-;-;-;0%");
                                continue;
                            }

                            newToOldInstances[newInst.ID] = closestInst.ID;

                            if (maxX == minX) maxX += 1;
                            if (maxY == minY) maxY += 1;
                            int height = maxY - minY;
                            int width = maxX - minX;


                            int closeInstMaxX = (int)GeographicalHelpers.GIMLongLatToX(closestInst.GIMmaxlat, closestInst.GIMmaxlong) * 1000;
                            int closeInstMaxY = (int)GeographicalHelpers.GIMLongLatToY(closestInst.GIMmaxlat, closestInst.GIMmaxlong) * 1000;

                            int closeInstMinX = (int)GeographicalHelpers.GIMLongLatToX(closestInst.GIMminlat, closestInst.GIMminlong) * 1000;
                            int closeInstMinY = (int)GeographicalHelpers.GIMLongLatToY(closestInst.GIMminlat, closestInst.GIMminlong) * 1000;

                            int closeHeight = closeInstMaxX - closeInstMinX;
                            int closeWidth = closeInstMaxY - closeInstMinY;


                            double newBorderArea = height * (double)width;

                            double dx = Math.Min(maxX, closeInstMaxX) - Math.Max(minX, closeInstMinX);
                            double dy = Math.Min(maxY, closeInstMaxY) - Math.Max(minY, closeInstMinY);
                            int confidence = 0;
                            if ((dx >= 0) && (dy >= 0))
                                confidence = (int)(((dx * dy) / newBorderArea) * 100);

                            if (confidence > 100 || confidence < 0)
                            {
                                string breakpoint = "breakpoint debug";
                            }

                            matchConfidence[newInst.ID] = confidence;
                            if(confidence > 80)
                            {
                                oldIDsToNewIds.AppendLine(newInst.ID + ";" + closestInst.ID + ";"
                                    + newInst.Name + ";" + closestInst.Name + ";" + confidence);
                                matchCount++;
                            }

                            //"NewID;Name;Px;Py;Pz;Ry;Width;Length;OldID;Name;Px;Py;Pz;Ry;Width;Length;confidence"
                            if (debugStats) newToOldStats.Append($"{newInst.ID};{newInst.Name};" +
                                $"{newInst.Px};{newInst.Py};{newInst.Pz};{newInst.Ry};{width};{height};");
                            if (debugStats) newToOldStats.AppendLine($"{closestInst.ID};{closestInst.Name};" +
                                $"{closestInst.Px};{closestInst.Py};{closestInst.Pz};{closestInst.Ry};{closeHeight};{closeWidth};{confidence}%");
                        }
                    }

                    if (debugStats)
                    {
                        string fileExportName = outputPath + regionID + "-Instance-Matches.csv";
                        File.WriteAllText(fileExportName, newToOldStats.ToString());
                    }
                    List<int> instanceIDsToRemove = new();
                    List<int> definitionIDsToRemove = new();
                }

                Console.WriteLine($"Region {regionID} (index {index} out of {regions.Count}), matched {matchCount} buildings");
            }

            string oldToNewFileName = outputPath + "OldToNewIDsByArea - 2.csv";
            File.WriteAllText(oldToNewFileName, oldIDsToNewIds.ToString());
        }

        public static void FindMissingIDFileRegions(string[] args)
        {
            string connectionString = DemoDBContext.GetBackendConnectionString();

            List<RegionBoundaries> allRegionBoundaries;

            using(CityJsonDBContext db = new CityJsonDBContext(connectionString))
            {
                allRegionBoundaries = db.RegionBoundaries.ToList();
            }

            string lod2_2Directory = @"D:\GIMmaps\GLB LOD 2_2 Zip";
            DirectoryInfo lod2Directory = new DirectoryInfo(lod2_2Directory); //Assuming This is your Folder
            DirectoryInfo[] lod2Folders = lod2Directory.GetDirectories();

            List<FileInfo> lod2FilesList = new List<FileInfo>();
            foreach (DirectoryInfo Folder in lod2Folders)
            {
                lod2FilesList.AddRange(Folder.GetFiles());
            }
            FileInfo[] lod2Files = lod2FilesList.ToArray();

            StringBuilder stringBuilder = new StringBuilder("File;Region\n");

            int count = 0;
            foreach(var lod2File in lod2Files)
            {
                count++;

                if(count % 10000 == 1)
                {
                    System.Console.WriteLine("Processing file " + count + " out of " + lod2Files.Length);
                }

                string fullName = lod2File.FullName;
                string extension = lod2File.Extension;

                GimData gimData = GIMtransfer.FileAccess.ReadToGimData(fullName, extension, true);


                double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity,
                    maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

                foreach (GimElement element in gimData.Elements)
                {
                    if (element.ElementType == GimElementType.P_polygon)
                    {
                        for (int i = 0; i + 2 < element.ElementDoubles.Length; i += 3)
                        {
                            double x = element.ElementDoubles[i];
                            double y = element.ElementDoubles[i + 1];
                            double z = element.ElementDoubles[i + 2];

                            minX = Math.Min(minX, x);
                            minY = Math.Min(minY, y);
                            minZ = Math.Min(minZ, z);

                            maxX = Math.Max(maxX, x);
                            maxY = Math.Max(maxY, y);
                        }
                    }
                }

                double centerX = (minX + maxX) / 2;
                double centerY = (minY + maxY) / 2;

                List<RegionBoundaries> fittingRegions = new();

                foreach(var regionBoundaries in allRegionBoundaries)
                {
                    if(centerX <= regionBoundaries.maxX && centerX >= regionBoundaries.minX)
                    {
                        if(centerY <= regionBoundaries.maxY && centerY >= regionBoundaries.minY)
                        {
                            fittingRegions.Add(regionBoundaries);
                        }
                    }
                }

                string correctRegionID = "";

                if(fittingRegions.Count > 1)
                {
                    double minDistance = double.PositiveInfinity;
                    foreach(var fittingRegion in fittingRegions)
                    {
                        double x = (fittingRegion.minX + fittingRegion.maxX) / 2;
                        double y = (fittingRegion.minY + fittingRegion.maxY) / 2;

                        double distance = (centerX - x) * (centerX - x) + (centerY - y) * (centerY - y);
                        if(distance < minDistance)
                        {
                            minDistance = distance;
                            correctRegionID = fittingRegion.ID;
                        }
                    }
                }
                else if(fittingRegions.Count == 1)
                {
                    correctRegionID = fittingRegions[0].ID;
                }

                string line = lod2File.Name + ";" + correctRegionID;

                stringBuilder.AppendLine(line);
            }

            string outputLocation = @"D:\GIMmaps\cityjson 2.2 statS IDs and positions\";
            File.WriteAllText(outputLocation + "missing files Regions.csv", stringBuilder.ToString());

        }

        public static void CheckMissingLODFiles(string[] args)
        {
            string lod1_2Directory = @"D:\GIMmaps\GLB LOD 1_2 Zip";
            DirectoryInfo lod1Directory = new DirectoryInfo(lod1_2Directory); //Assuming This is your Folder
            DirectoryInfo[] lod1Folders = lod1Directory.GetDirectories();

            List<FileInfo> lod1FilesList = new List<FileInfo>();
            foreach (DirectoryInfo Folder in lod1Folders)
            {
                lod1FilesList.AddRange(Folder.GetFiles());
            }
            FileInfo[] lod1Files = lod1FilesList.ToArray();



            string lod2_2Directory = @"D:\GIMmaps\GLB LOD 2_2 Zip";
            DirectoryInfo lod2Directory = new DirectoryInfo(lod2_2Directory); //Assuming This is your Folder
            DirectoryInfo[] lod2Folders = lod2Directory.GetDirectories();

            List<FileInfo> lod2FilesList = new List<FileInfo>();
            foreach (DirectoryInfo Folder in lod2Folders)
            {
                lod2FilesList.AddRange(Folder.GetFiles());
            }
            FileInfo[] lod2Files = lod2FilesList.ToArray();

            // dictionary to check whether file exists in lod 1 AND 2
            // if it exists in lod 1, +1.
            // if it exists in lod 2, +10
            // => score of 11 = exists in both
            Dictionary<int, int> IDtoExistenceCount = new();

            foreach (FileInfo lod1File in lod1Files)
            {
                string fileName = lod1File.Name;
                string nameEnd = fileName[^7..];
                if (nameEnd == "_12.glb")
                {
                    int fileID = int.Parse(fileName.Split("_")[0]);
                    IDtoExistenceCount[fileID] = 1;
                }
            }

            foreach (FileInfo lod2File in lod2Files)
            {
                string fileName = lod2File.Name;
                string nameEnd = fileName[^7..];
                if (nameEnd == "_22.glb")
                {
                    int fileID = int.Parse(fileName.Split("_")[0]);
                    if (!IDtoExistenceCount.ContainsKey(fileID))
                    {
                        IDtoExistenceCount[fileID] = 0;
                    }
                    IDtoExistenceCount[fileID] += 10;
                }
            }

            string outputLocation = @"D:\GIMmaps\cityjson 2.2 statS IDs and positions\";
            StringBuilder lod1Missing = new();
            StringBuilder lod2Missing = new();

            foreach (var keyval in IDtoExistenceCount)
            {
                int ID = keyval.Key;
                int value = keyval.Value;

                if (value % 2 == 0)
                {
                    lod1Missing.AppendLine(ID.ToString());
                }
                if (value < 10)
                {
                    lod2Missing.AppendLine(ID.ToString());
                }

            }

            File.AppendAllText(outputLocation + "lod_12_missing.txt", lod1Missing.ToString());
            File.AppendAllText(outputLocation + "lod_22_missing.txt", lod2Missing.ToString());

            //ReadCityJson2_2(args);
        }

        public static void TryAndFindMissingObjectNames2_2(string[] args)
        {
            string directoryPath = @"D:\GIMmaps\CityJson2.2";

            // test
            // directoryPath = @"D:\GIMmaps\cityjson 2.2 test";
            string connectionString = DemoDBContext.GetBackendConnectionString();

            DirectoryInfo directory = new DirectoryInfo(directoryPath);
            FileInfo[] allFiles = directory.GetFiles();

            ReadCityJsonObjectsLOD2_2.InitStats();
            string IDsAndPositionsPath = @"D:\GIMmaps\cityjson 2.2 statS IDs and positions\object names and positions.csv";

            int fileStartIndex = 2528; // the file we start from

            if (args.Length > 1)
            {
                fileStartIndex = int.Parse(args[1]);
            }


            Console.Write("Importing ^missing object IDs and Positions...");
            ReadCityJsonObjectsLOD2_2.ImportMissingObjectNames(IDsAndPositionsPath);
            Console.WriteLine(" Done");
            int index = 0;

            double totalFileSize = 0;
            Console.Write("Calculating total file size...");
            for (int i = fileStartIndex; i < allFiles.Length; i++)
            {
                FileInfo file = allFiles[i];
                double fileSizeMb = (double)file.Length / 1048576;
                totalFileSize += fileSizeMb;
            }
            Console.WriteLine("Done");

            Console.WriteLine("Total file size is :" + totalFileSize.ToString("0.##") + "MB");

            Console.Write("Fetching region boundaries from DB...");
            List<RegionBoundaries> regions = null;
            using (CityJsonDBContext? db = new CityJsonDBContext(connectionString))
            {
                regions = db.RegionBoundaries.ToList();
            }
            Console.WriteLine(" Done");

            double cumulativeFileSize = 0;
            Stopwatch totalTime = Stopwatch.StartNew();
            for (int i = fileStartIndex; i < allFiles.Length; i++)
            {
                FileInfo file = allFiles[i];
                double fileSizeMb = (double)file.Length / 1048576;
                cumulativeFileSize += fileSizeMb;

                string fileName = file.FullName;
                //ReadCityJsonObjectsLOD2_2.ReadObjects(fileName);
                Console.WriteLine("File " + fileName + " (" + index + " out of " + allFiles.Length + ")  finished!");
                long timeSoFar = totalTime.ElapsedMilliseconds;
                double timePerMb = (double)timeSoFar / cumulativeFileSize;
                long timeLeft = (long)((totalFileSize - cumulativeFileSize) * timePerMb);

                Console.WriteLine("Estimated Time Left is " + (timeLeft / 60000) + " minutes, or " + (timeLeft / 3600000) + " hours");

                Console.WriteLine("Total Elapsed Time is " + (timeSoFar / 60000) + " minutes, " + (timeSoFar / 3600000) + " hours");


                index++;
            }


            //var finalStats = ReadCityJsonObjectsLOD2_2.NumericStats;

            //ReadCityJsonObjectsLOD2_2.ExportObjectNamesAndRotations(exportPath);
            return;

        }

        public static void ReadCityJson2_2(string[] args)
        {
            string directoryPath = @"D:\GIMmaps\CityJson2.2";

            // test
            // directoryPath = @"D:\GIMmaps\cityjson 2.2 test";
            string connectionString = DemoDBContext.GetBackendConnectionString();

            DirectoryInfo directory = new DirectoryInfo(directoryPath);
            FileInfo[] allFiles = directory.GetFiles();

            /*
             * 
             *  <============================= CONFIGURATION =================================>
             * 
             */

            bool upload = false; // whether or not to upload to DB
            bool createFiles = true; // whether or not to create GLB files
            int startID = 50000000; // the starting instanceID for inserting in DB

            int fileStartIndex = 0; // the file we start from
            bool findStartFileByName = false; // start file by name rather than by index
            string fileStartName = "8-712-536.city.json"; // name of the starting file

            ReadCityJsonObjectsLOD2_2.Read2_2Options readOptions = new()
            {
                Write1_2 = false, // whether or not to write 1.2 glb files
                Write2_2 = true // whether or not to write 2.2 glb files
            };

            // ================================================================================

            if (args.Length > 1)
            {
                fileStartIndex = int.Parse(args[1]);
            }

            if (findStartFileByName)
            {
                int i = 0;
                foreach(var file in allFiles)
                {
                    if(file.Name == fileStartName)
                    {
                        fileStartIndex = i;
                        break;
                    }
                    i++;
                }
            }



            int index = 0;
            double totalFileSize = 0;

            // fetching region boundary data and preparing it

            using (CityJsonDBContext? db1 = new(connectionString))
            {

                Console.Write("Calculating total file size...");
                for (int i = fileStartIndex; i < allFiles.Length; i++)
                {
                    FileInfo file = allFiles[i];
                    double fileSizeMb = (double)file.Length / 1048576;
                    totalFileSize += fileSizeMb;
                }
                Console.WriteLine("Done");
                Console.Write("Reading region boundaries and preparing matrix...");
                var regionBoundaries = db1.RegionBoundaries.ToList();
                Console.Write("Fetching Done...");
                ReadCityJsonObjectsLOD2_2.GroupRegionBoundaries(regionBoundaries);
                Console.WriteLine("Done");
            }
            Console.WriteLine("Total file size is :" + totalFileSize.ToString("0.##") + "MB");

            double cumulativeFileSize = 0;
            Stopwatch totalTime = Stopwatch.StartNew();

            var db = new CityJsonDBContext(connectionString);

            for (int i = fileStartIndex; i < allFiles.Length; i++)
            {
                ReadCityJson.objectDefinitions = new();
                ReadCityJson.objectInstances = new();

                FileInfo file = allFiles[i];
                double fileSizeMb = (double)file.Length / 1048576;
                cumulativeFileSize += fileSizeMb;

                string fileName = file.FullName;
                ReadCityJsonObjectsLOD2_2.ReadObjects(fileName, createFiles, ref startID, readOptions);

                Console.WriteLine("Done reading Objects! Uploading To Db...");

                if (upload)
                {
                    if (index % 10 == 0) // reinstantiating db every 10 files
                                         // because if we reinstiate every file, it will take a long time
                                         // and if we keep one instance, it piles up and slows down
                    {
                        db = new CityJsonDBContext(connectionString);
                    }
                    try
                    {
                        Console.Write("Inserting Instances from  " + fileName + " ( index =  " + i + ") to DB...");
                        //ReadWriteMySQLDB.WriteDatabaseObjectInstances(db);
                        db.ObjectInstances.BulkInsert(ReadCityJson.objectInstances,
                            options => { options.ColumnPrimaryKeyNames = new() { "ID" }; });
                        Console.WriteLine("DONE!");
                    }
                    catch
                    {
                        Console.WriteLine("ERROR! Failed to insert Object Instances for region " + fileName + " ( index =  " + i + ")!");
                        throw;
                    }
                    Console.WriteLine("Object Instances for region " + fileName + " ( index =  " + i + " ) added successfully!");

                    try
                    {
                        Console.Write("Inserting Definitions from  " + fileName + " ( index =  " + i + ") to DB...");
                        //ReadWriteMySQLDB.WriteDatabaseObjectDefinitions(db); 
                        db.ObjectDefinitions.BulkInsert(ReadCityJson.objectDefinitions,
                            options => { options.ColumnPrimaryKeyNames = new() { "ID" }; });
                        Console.WriteLine("DONE!");
                    }
                    catch
                    {
                        Console.WriteLine("ERROR! Failed to insert Object Definitions for region " + fileName + " ( index =  " + i + ")!");
                        throw;
                    }
                    Console.WriteLine("Object Definitions for region " + fileName + " ( index =  " + i + " ) added successfully!");

                }

                Console.WriteLine("File " + fileName + " (" + i + " out of " + allFiles.Length + ")  finished!");
                long timeSoFar = totalTime.ElapsedMilliseconds;
                double timePerMb = (double)timeSoFar / cumulativeFileSize;
                long timeLeft = (long)((totalFileSize - cumulativeFileSize) * timePerMb);

                Console.WriteLine("Estimated Time Left is " + (timeLeft / 60000) + " minutes, or " + (timeLeft / 3600000) + " hours");

                Console.WriteLine("Total Elapsed Time is " + (timeSoFar / 60000) + " minutes, " + (timeSoFar / 3600000) + " hours");


                index++;

            }

            //var finalStats = ReadCityJsonObjectsLOD2_2.NumericStats;

            //ReadCityJsonObjectsLOD2_2.ExportObjectNamesAndRotations(exportPath);
            return;


        
            foreach (FileInfo file in allFiles)
            {
                string fileName = file.FullName;
                //ReadCityJsonObjectsLOD2_2.ReadObjects(fileName);
            }


        }

        /// <summary>
        /// 0- Folder location of cityjsons
        /// 1- File Start index (ignored if autoFindStart = true) 
        /// 2- Definition start ID 
        /// 3- Instance start ID
        /// 4- Local only (dont upload to DB)
        /// 5- Skip Sign Definitions
        /// 6- Skip Sign Instances
        /// 7- Automatically find file start index
        /// 8- Start Region ID
        /// </summary>
        /// <param name="args">
        /// the arguments of this function, input as a string
        /// </param>
        public static void ReadCityJson1_2(string[] args)
        {
            string connectionString = DemoDBContext.GetBackendConnectionString();

            Console.WriteLine("Connecting to database...");
            using var db = new CityJsonDBContext(connectionString);
            ServerVersion.AutoDetect(connectionString);
            Console.WriteLine("Connected to database successfully");
            Debug.WriteLine("tested database successfully!");

            string environment;
            //environment = "MarcelTest";
            //environment = "AnisTest";
            //environment = "MarcelDef";
            //environment = "GimTest";
            environment = "Fluffy";

            string DirectoryPath = "";
            bool unzip = true;

            DirectoryPath = environment switch
            {
                "MarcelObjects" => "replace",
                "MarcelTest" => "replace",
                "AnisTest" => "replace",
                "MarcelDef" => @"C:\Data\MAPFILES",
                "GimTest" => @"C:\GIMdata",
                _ => @"D:\GIMmaps\CityJson1.2",
            };

            if (args.Length > 0)
            {
                environment = args[0];
            }

            DirectoryInfo d = new DirectoryInfo(DirectoryPath); //Assuming This is your Folder

            FileInfo[] Files = new FileInfo[1];
            DirectoryInfo[] Folders = new DirectoryInfo[1];
            if (unzip)                           // Getting the folders with zip-files with files
            {
                Folders = d.GetDirectories();
                List<FileInfo> ZipFiles = new List<FileInfo>();
                foreach (DirectoryInfo Folder in Folders)
                {
                    ZipFiles.AddRange(Folder.GetFiles());
                }
                Files = ZipFiles.ToArray();
            }
            else Files = d.GetFiles("*.json");   // Getting Text files

            /*
             * 
             * This is where we modify the starting indices in case the program stops midway
             * <============================== CONFIG ================================>
             * 
             */

            int fileStartIndex = 0; // the file we start from
            int fileEndIndex = Files.Length; // file to end at
            int fileProcessCount = Files.Length; // how many files to process.
                                                 // Stops at either this or the last file index above
            int fileStartUploadIndex = 0; // the file from which we start uploading to DB
            int instanceStartID = 0; // where instances start
            int definitionStartID = 154 + instanceStartID; // where definitions start
                                                           // MOST IMPORTANT VARIABLE V
                                                           // MOST IMPORTANT VARIABLE V
                                                           // MOST IMPORTANT VARIABLE V
                                                           // MOST IMPORTANT VARIABLE V
            bool offlineMode = true; // do not upload the data to DB. PLEASE DOUBLE CHECK THIS VARIABLE
            bool createFiles = false; // do not create glb files. PLEASE DOUBLE CHECK THIS VARIABLE
                                      // MOST IMPORTANT VARIABLE ^
                                      // MOST IMPORTANT VARIABLE ^
                                      // MOST IMPORTANT VARIABLE ^
                                      // MOST IMPORTANT VARIABLE ^
            bool skipSignDefinitions = true; // skip creating sign definitions
            bool skipSignInstances = true; // skip creating sign instances
            bool autoFindStartIndex = true; // automatically find the file starting index by file name
            string startingRegionName = "31cn1"; // starting region name
            bool updateMode = true; // updates in DB instead of attempting to insert
            bool useRegionIDIntervals = true; // Read pre-defined region ID intervals for instance/definition.
                                              // If false will just increment the previous value
            string regionIDIntervalsFilePath =
                "GIMmap region ID boundaries PROD.csv"; // name of the file containing these IDs

            // <=====================================================================>

            Dictionary<string, (int, int)> regionInstanceIDs = new();
            Dictionary<string, (int, int)> regionDefinitionIDs = new();
            Dictionary<string, int> regionObjectCount = new();

            // loading region ID interval information
            if (useRegionIDIntervals)
            {
                Console.WriteLine("Beginning reading region ID Intervals...");
                string[] fileLines = File.ReadAllLines(regionIDIntervalsFilePath);
                for (int i = 1; i < fileLines.Length; i++)
                {
                    string line = fileLines[i];
                    string[] keywords = line.Split(",");
                    string regionID = keywords[0];
                    int minInstanceID = int.Parse(keywords[1]);
                    int maxInstanceID = int.Parse(keywords[2]);
                    int minDefID = int.Parse(keywords[3]);
                    int maxDefID = int.Parse(keywords[4]);
                    int objectCount = int.Parse(keywords[5]);

                    int expectedMinDefID = minInstanceID + 154;
                    // some regionIDs were nullified, so minDefID might be inconsistent. This deals with that
                    if (expectedMinDefID != minDefID)
                    {
                        string debug = "unexpected difference in minimum definition ID" +
                            $" (={minDefID}) in region {regionID}, rectifying to {expectedMinDefID}...";
                        minDefID = expectedMinDefID;
                        Console.WriteLine(debug);
                    }
                    regionInstanceIDs[regionID] = (minInstanceID, maxInstanceID);
                    regionDefinitionIDs[regionID] = (minDefID, maxDefID);
                    regionObjectCount[regionID] = objectCount;
                }
                Console.WriteLine($"Finished reading {regionObjectCount.Count} region ID Intervals!");
            }

            ReadCityJson.createFiles = createFiles;

            // checking ARGS 
            //fileStartIndex = args.Length > 1? int.Parse(args[1]) : fileStartIndex;
            //definitionStartID = args.Length > 2? int.Parse(args[2]) : definitionStartID;
            //instanceStartID = args.Length > 3 ? int.Parse(args[3]) : instanceStartID;
            //offlineMode = args.Length > 4? bool.Parse(args[4]): offlineMode;
            //skipSignDefinitions = args.Length > 5 ? bool.Parse(args[5]) : skipSignDefinitions;
            //skipSignInstances = args.Length > 6 ? bool.Parse(args[6]) : skipSignInstances;
            //autoFindStartIndex = args.Length > 7 ? bool.Parse(args[7]) : autoFindStartIndex;
            //startingRegionName = args.Length > 8 ? args[8] : startingRegionName;

            if (autoFindStartIndex)
                for (int i = 0; i < Files.Length; i++)
                    if (Files[i].Name[0..startingRegionName.Length].ToLower() == startingRegionName)
                    {
                        fileStartIndex = i;
                        break;
                    }
        
        
            int instanceID = instanceStartID;
            int definitionID = definitionStartID;
            definitionID += ReadCityJson.CreateStreetSignObjects(skipSignDefinitions); // create streetsign definitions

            try
            {
                ReadWriteMySQLDB.WriteDatabaseObjectDefinitions(db, updateMode);
            }
            catch
            {
                Console.WriteLine("ERROR! Failed to insert Object Definitions for signs!");
                throw;
            }

            Stopwatch totalStopWatch = new();
            Stopwatch currentFileStopWatch = new();

            DirectoryInfo RunDirectory = Directory.CreateDirectory(DirectoryPath + "\\_run");
            RunDirectory.Delete(true);

            // calculating total file size, helps with estimating time left
            double totalFileSize = 0;
            for (int i = fileStartIndex; i < Files.Length; i++)
            {
                FileInfo file = Files[i];
                double fileSizeMb = (double)file.Length / 1048576;
                totalFileSize += fileSizeMb;
            }

            int index = 0;
            double cumulativeFileSizeMb = 0;

            for (int fileIndex = fileStartIndex; fileIndex < Files.Length && index < fileProcessCount; fileIndex++)
            {
                index++;
                totalStopWatch.Start();
                currentFileStopWatch.Reset();
                currentFileStopWatch.Start();

                FileInfo file = Files[fileIndex];
                FileInfo[] JsonFiles;
                DirectoryInfo directory = Directory.CreateDirectory(DirectoryPath + "\\_run");

                double fileSizeMb = (double)file.Length / 1048576;
                cumulativeFileSizeMb += fileSizeMb;
                if (unzip)
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(file.ToString(), directory.ToString());
                    JsonFiles = directory.GetFiles("*.json").ToArray();
                }
                else JsonFiles = new FileInfo[1] { file };

                foreach (FileInfo jsonfile in JsonFiles)
                {
                    ReadCityJson.objectDefinitions = new();
                    ReadCityJson.objectInstances = new();

                    string filename = jsonfile.Name[..^5];
                    string regionID = filename[0..5] + filename[7];
                    if (useRegionIDIntervals)
                    {
                        // regions that don't have values in this dict simply have no objects, so we skip them
                        if (!regionDefinitionIDs.ContainsKey(regionID)) continue;
                        definitionID = regionDefinitionIDs[regionID].Item1;
                        instanceID = regionInstanceIDs[regionID].Item1;
                    }
                    ReadCityJson.ReadCityJsonStream(jsonfile.FullName, ref definitionID, ref instanceID, regionID);
                    if (offlineMode) continue; // if dontupload flag is true, don't upload to DB
                    if (fileIndex < fileStartUploadIndex) continue; // don't upload DB records for files
                                                                    // before this index

                    if (useRegionIDIntervals)
                    {
                        int expectedCount = regionObjectCount[regionID];
                        int actualCount = ReadCityJson.objectDefinitions.Count;
                        int actualInstCount = ReadCityJson.objectInstances.Count;
                        int absoluteDiff = Math.Abs(actualCount - expectedCount);

                        // difference of one is expected
                        if(absoluteDiff > 1)
                        {
                            string debug = "breakpoint";
                            Console.WriteLine($"WARNING: Different object count (={actualCount})" +
                                $" encountered than expected! (={expectedCount}) ");
                        }
                    }

                    try
                    {
                        ReadWriteMySQLDB.WriteDatabaseObjectInstances(db, updateMode);
                    }
                    catch
                    {
                        Console.WriteLine("ERROR! Failed to insert Object Instances for region " + regionID + " ( index =  " + fileIndex + ")!");
                        throw;
                    }
                    Console.WriteLine("Object Instances for region " + regionID + " ( index =  " + fileIndex + " ) added successfully!");

                    try
                    {
                        ReadWriteMySQLDB.WriteDatabaseObjectDefinitions(db, updateMode);
                    }
                    catch
                    {
                        Console.WriteLine("ERROR! Failed to insert Object Definitions for region " + regionID + " ( index =  " + fileIndex + ")!");
                        throw;
                    }
                    Console.WriteLine("Object Definitions for region " + regionID + " ( index =  " + fileIndex + " ) added successfully!");
                }

                // this region is used to estimate the amount of time left for the run.
                #region time estimate
                totalStopWatch.Stop();

                Console.WriteLine("File " + file.Name + " (" + fileIndex + " out of " + Files.Length + ")  finished!");
                long timeSoFar = totalStopWatch.ElapsedMilliseconds;
                double timePerMb = (double)timeSoFar / cumulativeFileSizeMb;
                long timeLeft = (long)((totalFileSize - cumulativeFileSizeMb) * timePerMb);

                long totalElapsedTime = totalStopWatch.ElapsedMilliseconds;

                long currentElapsedTime = currentFileStopWatch.ElapsedMilliseconds;
                long currentElapsedTimeSec = currentElapsedTime / 1000;
                long currentElapsedTimeMin = currentElapsedTime / 60000;

                double totalFileCount = Math.Min(fileProcessCount, Files.Length - fileStartIndex);
                int fileDonePercentage = (int)((fileIndex + 1) * 100 / totalFileCount);
                Console.WriteLine($"File {fileIndex} out of  {totalFileCount}({fileDonePercentage}%), finished after" +
                    $"{currentElapsedTimeSec}s = {currentElapsedTimeMin} minutes");

                Console.WriteLine("Estimated Time Left is " + (timeLeft / 60000) + " minutes, or " + (timeLeft / 3600000) + " hours");
                Console.WriteLine("Total Elapsed Time is " + (timeSoFar / 60000) + " minutes, " + (timeSoFar / 3600000) + " hours");


                long totalElapsedTimeMin = totalElapsedTime / 60000;
                long totalElapsedTimeHour = totalElapsedTime / 3600000;
                //int filesDone = (fileIndex + 1) - fileStartIndex;
                //long averageTime = totalElapsedTime / filesDone;
                //long averageTimeSec = averageTime / 1000;
                //long averageTimeMin = averageTime / 60000;

                //Console.WriteLine($"Average file duration is {averageTimeSec}s = {averageTimeMin} minutes");

                //int filesLeft = Files.Length - fileStartIndex - filesDone;
                //filesLeft = Math.Min(filesLeft, fileProcessCount - index);
                //long timeLeft = averageTime * filesLeft;
                //long timeLeftMin = timeLeft / 60000;
                //long timeLeftHour = timeLeft / 3600000;

                //Console.WriteLine($"Estimated Time Left is {timeLeftMin} minutes, or {timeLeftHour} hours");
                //Console.WriteLine($"Total Elapsed Time is {totalElapsedTimeMin} minutes, {totalElapsedTimeHour} hours");

                #endregion

                GC.Collect();
                directory.Delete(true);
            }

            totalStopWatch.Stop();

            //foreach (var keyval in ReadCityJson.gridCoordinatesToAltitude)
            //{
            //    db.altitudes.Add(
            //        new()
            //        {
            //            X = (int)keyval.Key[0],
            //            Y = (int)keyval.Key[1],
            //            Z = (int)keyval.Value
            //        }
            //        );
            //}

            db.SaveChanges();

            if (!skipSignInstances)
            {
                ReadCityJson.ReadSigns(instanceID, ReadCityJson.rvvCodes); // create streetsign instances
                try
                {
                    ReadWriteMySQLDB.WriteDatabaseObjectInstances(db, false);
                }
                catch
                {
                    Console.WriteLine("ERROR! Failed to insert Object Instances for signs!");
                    throw;
                }
                Console.WriteLine("Object Instances for signs added successfully!");
            }
        }
    }
}
