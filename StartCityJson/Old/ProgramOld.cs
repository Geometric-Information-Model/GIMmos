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
using System.Runtime.Intrinsics.X86;
using FileAccess = GIMtransfer.FileAccess;

namespace CityJson
{
    public class ProgramOld
    {
        static void RenameToMain(string[] args)
        {
            string connectionString = DemoDBContext.GetBackendConnectionString();

            //double lon = 51.9810883, lat = 5.9038773;
            //double px = ReadCityJson.LongLatToX(lon, lat);
            //double py = ReadCityJson.LongLatToY(lon, lat);

            //lon = 51.981462967293425; lat = 5.9046134883699555;
            //px = ReadCityJson.LongLatToX(lon, lat);
            //py = ReadCityJson.LongLatToY(lon, lat);

            //lon = 52.38177384359276; lat = 4.912446890045442;
            //px = ReadCityJson.LongLatToX(lon, lat);
            //py = ReadCityJson.LongLatToY(lon, lat);

            //lon = 52.883794159341726; lat = 4.874529243512215;
            //px = ReadCityJson.LongLatToX(lon, lat);
            //py = ReadCityJson.LongLatToY(lon, lat);



            Console.WriteLine("Connecting to database...");
            using (var db = new CityJsonDBContext(connectionString))
            {
                ServerVersion.AutoDetect(connectionString);
                Console.WriteLine("Connected to database successfully");
                Debug.WriteLine("tested database successfully!");

                string environment;
                //environment = "MarcelTest";
                //environment = "AnisTest";
                environment = "MarcelDef";
                //environment = "GimTest";

                string DirectoryPath = "";
                bool unzip = true;
                if (environment == "MarcelObjects") DirectoryPath = "replace";
                if (environment == "MarcelTest") DirectoryPath = "replace";
                if (environment == "AnisTest") DirectoryPath = "replace";
                if (environment == "MarcelDef") { DirectoryPath = @"C:\Data\MAPFILES"; unzip = true; }
                if (environment == "GimTest") DirectoryPath = @"C:\GIMdata";

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
                 * 
                 */

                int fileStartIndex = 157; // the file we start from
                int fileStartUploadIndex = 0; // the file from which we start uploading to DB
                int definitionStartID = 2532973; // where definitions start
                int instanceStartID = 2532819; // where instances start

                bool dontUpload = false;
                bool skipSignDefinitions = true;
                bool skipSignInstances = true;

                //{
                //    DirectoryInfo Directory = new DirectoryInfo(DirectoryPath + "\\_run");
                //    Directory.Delete(true);
                //}

                /*
                 * 
                 * 
                 * 
                 */


                int instanceID = instanceStartID;
                int definitionID = definitionStartID;
                definitionID += ReadCityJson.CreateStreetSignObjects(skipSignDefinitions); // create streetsign definitions

                try
                {
                    //ReadWriteMySQLDB.WriteDatabaseObjectDefinitions(db, updateMode);
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

                for (int fileIndex = fileStartIndex; fileIndex < Files.Length; fileIndex++)
                {

                    totalStopWatch.Start();
                    currentFileStopWatch.Reset();
                    currentFileStopWatch.Start();

                    FileInfo file = Files[fileIndex];
                    FileInfo[] JsonFiles;
                    DirectoryInfo Directory = new DirectoryInfo(DirectoryPath + "\\_run");
                    if (unzip)
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(file.ToString(), Directory.ToString());
                        JsonFiles = Directory.GetFiles("*.json").ToArray();
                    }
                    else JsonFiles = new FileInfo[1] { file };

                    foreach (FileInfo jsonfile in JsonFiles)
                    {
                        ReadCityJson.objectDefinitions = new();
                        ReadCityJson.objectInstances = new();

                        string filename = jsonfile.Name.Substring(0, jsonfile.Name.Length - 5);
                        string regionID = filename[0..5] + filename[7];
                        string json = File.ReadAllText(jsonfile.FullName);
                        ReadCityJson.CityJsonToGimData(ref json, ref instanceID, ref definitionID, regionID);
                        if (dontUpload) continue; // if dontupload flag is true, don't upload to DB

                        if (fileIndex < fileStartUploadIndex) continue; // don't upload DB records for files
                                                                        // before this index

                        try
                        {
                            //ReadWriteMySQLDB.WriteDatabaseObjectInstances(db);
                        }
                        catch
                        {
                            Console.WriteLine("ERROR! Failed to insert Object Instances for region " + regionID + " ( index =  " + fileIndex + ")!");
                            throw;
                        }
                        Console.WriteLine("Object Instances for region " + regionID + " ( index =  " + fileIndex + " ) added successfully!");

                        try
                        {
                            //ReadWriteMySQLDB.WriteDatabaseObjectDefinitions(db);
                        }
                        catch
                        {
                            Console.WriteLine("ERROR! Failed to insert Object Definitions for region " + regionID + " ( index =  " + fileIndex + ")!");
                            throw;
                        }
                        Console.WriteLine("Object Definitions for region " + regionID + " ( index =  " + fileIndex + " ) added successfully!");

                        //try
                        //{
                        //    ReadWriteMySQLDB.WriteDatabaseObjectVisualizations(db);
                        //}
                        //catch
                        //{
                        //    Console.WriteLine("ERROR! Failed to insert Object Visualisations for region " + regionID + " ( index =  " + fileIndex + ")!");
                        //    throw;
                        //}
                        //Console.WriteLine("Object Visualisations for region " + regionID + " ( index =  " + fileIndex + " ) added successfully!");
                    }
                    totalStopWatch.Stop();

                    long totalElapsedTime = totalStopWatch.ElapsedMilliseconds;
                    long currentElapsedTime = currentFileStopWatch.ElapsedMilliseconds;

                    Console.WriteLine("File " + fileIndex + " finished after "
                        + (currentElapsedTime / 1000) + "s = " + (currentElapsedTime / 60000) + " minutes");

                    int filesDone = (fileIndex + 1) - fileStartIndex;
                    long averageTime = totalElapsedTime / filesDone;

                    Console.WriteLine("Average file duration is " + averageTime / 1000 + "s = "
                        + (averageTime / 60000) + " minutes");

                    int filesLeft = Files.Length - fileStartIndex - filesDone;
                    long timeLeft = averageTime * filesLeft;

                    Console.WriteLine("Estimated Time Left is "
                        + (timeLeft / 60000) + " minutes, or " + (timeLeft / 3600000) + " hours");

                    Console.WriteLine("Total Elapsed Time is " + (totalElapsedTime / 60000) + " minutes, " + (totalElapsedTime / 3600000) + " hours");

                    GC.Collect();
                    Directory.Delete(true);
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
                        //ReadWriteMySQLDB.WriteDatabaseObjectInstances(db);
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
}
