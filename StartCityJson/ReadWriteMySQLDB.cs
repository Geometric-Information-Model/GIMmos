using CityJson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StartCityJson
{
    internal class ReadWriteMySQLDB
    {
        //internal static uint WriteDatabasePlots(uint id, DemoDBContext db)
        //{
        //    int counter = 0;
        //    foreach (CityJsonPlot p in ReadCityJsonOld.Plots)
        //    {
        //        counter++;
        //        id++;
        //        PlotType type;
        //        bool success = Enum.TryParse<PlotType>(p.Type, true, out type);
        //        if (!success) throw new Exception("invalid type for plot encountered");
        //        db.Plots.Add(new DemoDBPlot()
        //        {
        //            Id = (int)id,
        //            Name = p.Name,
        //            Type = type.ToString(),
        //            Px = p.Px,
        //            Py = p.Py,
        //            MinGimLat = p.MinGimLat,
        //            MaxGimLat = p.MaxGimLat,
        //            MinGimLong = p.MinGimLong,
        //            MaxGimLong = p.MaxGimLong,
        //            GimBin = p.GimBin,
        //            GimJson = p.GimJson

        //        });

        //        // adding all references to children in the third table
        //        if (p.Objects != null)
        //            foreach (int objectId in p.Objects)
        //            {
        //                db.PlotObjectRelations.Add(new()
        //                {
        //                    PlotId = (int)id,
        //                    ObjectId = objectId
        //                });
        //            }

        //        if (counter > 500)
        //        {
        //            db.SaveChanges();
        //            counter = 0;
        //        }
        //    }

        //    db.SaveChanges();
        //    return id;
        //}
        internal static void WriteDatabaseObjectInstances(CityJsonDBContext db, bool updateMode)
        {
            int counter = 0;
            int lastAddedId = -1;
            int index = 0;

            for(int i = 0; i < ReadCityJson.objectInstances.Count; i+= 10000) 
            {
                int start = i;
                int count = 10000;
                count = Math.Min(count, ReadCityJson.objectInstances.Count - i);
                var subRange = ReadCityJson.objectInstances.GetRange(start, count);
                try
                {
                    if (updateMode) db.ObjectInstances.BulkUpdate(subRange);
                    else db.ObjectInstances.BulkInsert(subRange);
                    db.SaveChanges();
                    int percentage = (int)(((double)(i + count) / ReadCityJson.objectInstances.Count) * 100);
                    Console.WriteLine(percentage + "%: " + (i + count) + " out of " + ReadCityJson.objectInstances.Count + " Instances inserted into DB");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Caught exception: " + ex.Message);
                    Console.WriteLine("Breakpoint should be hit, resume code when internet is back");
                    string breakpoint = "breakpoint";
                    i -= 10000;
                    continue;
                }
            }
            return;

            foreach (var objectInstance in ReadCityJson.objectInstances)
            {
                index++;
                try
                {
                    counter++;
                    db.ObjectInstances.Add(objectInstance);
                    if (counter >= 1000)
                    {
                        try
                        {
                            db.SaveChanges();
                        }catch (Exception ex)
                        {
                            Console.WriteLine("Caught exception: " + ex.Message);
                            Console.WriteLine("Breakpoint should be hit, resume code when internet is back");
                            string breakpoint = "breakpoint";
                            continue;
                        }
                        counter = 0;
                        lastAddedId = objectInstance.ID;
                        int percentage = (int)(((double)index / ReadCityJson.objectInstances.Count) * 100);
                        Console.WriteLine(percentage + "%: " + index + " out of " + ReadCityJson.objectInstances.Count + " Instances inserted into DB");
                    }
                }
                catch 
                {
                    Console.WriteLine("Exception when inserting object Instance, last added ID is " + lastAddedId);
                    throw;
                }
            }

            try
            {
                db.SaveChanges();
            }
            catch
            {
                Console.WriteLine("Exception when inserting object Instance, last added ID is " + lastAddedId);
                Console.WriteLine("Try again by continuing from breakpoint");
                db.SaveChanges();
                throw;
            }
        }
        internal static void WriteDatabaseObjectDefinitions(CityJsonDBContext db, bool updateMode)
        {
            int counter = 0;
            int lastAddedId = -1;
            int index = 0;

            for (int i = 0; i < ReadCityJson.objectDefinitions.Count; i += 10000)
            {
                int start = i;
                int count = 10000;
                count = Math.Min(count, ReadCityJson.objectDefinitions.Count - i);
                var subRange = ReadCityJson.objectDefinitions.GetRange(start, count);
                try
                {
                    if (updateMode) db.ObjectDefinitions.BulkUpdate(subRange);
                    else db.ObjectDefinitions.BulkInsert(subRange);
                    db.SaveChanges();
                    int percentage = (int)(((double)(i + count) / ReadCityJson.objectDefinitions.Count) * 100);
                    Console.WriteLine(percentage + "%: " + (i + count) + " out of " + ReadCityJson.objectDefinitions.Count + " Instances inserted into DB");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Caught exception: " + ex.Message);
                    Console.WriteLine("Breakpoint should be hit, resume code when internet is back");
                    string breakpoint = "breakpoint";
                    i -= 10000;
                    continue;
                }
            }
            return;

            foreach (var o in ReadCityJson.objectDefinitions)
            {
                index++;
                try
                {
                    counter++;
                    db.ObjectDefinitions.Add(o);

                    if (counter >= 1000)
                    {
                        try
                        {
                            db.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Caught exception: " + ex.Message);
                            Console.WriteLine("Breakpoint should be hit, resume code when internet is back");
                            string breakpoint = "breakpoint";
                            continue;
                        }
                        counter = 0;
                        lastAddedId = o.ID;
                        int percentage = (int)(((double)index / ReadCityJson.objectDefinitions.Count) * 100);
                        Console.WriteLine(percentage + "%: " + index + " out of " + ReadCityJson.objectDefinitions.Count + " Definitions inserted into DB");

                    }
                }
                catch
                {
                    Console.WriteLine("Exception when inserting object Definition, last added ID is " + lastAddedId);
                    throw;
                }
            }
            try
            {
                db.SaveChanges();
            }
            catch
            {
                Console.WriteLine("Exception when inserting object Definition, last added ID is " + lastAddedId);
                Console.WriteLine("Breakpoint should be hit, resume when internet connection is back");
                db.SaveChanges();
                throw;
            }
        }
        //internal static void WriteDatabaseObjectVisualizations(CityJsonDBContext db)
        //{
        //    int counter = 0;
        //    int lastAddedId = -1;
        //    int index = 0;
        //    foreach (var o in ReadCityJson.objectVisualisations)
        //    {
        //        index++;
        //        try
        //        {
        //            counter++;
        //            db.ObjectVisualisations.Add(o);

        //            if (counter > 500)
        //            {
        //                db.SaveChanges();
        //                counter = 0;
        //                lastAddedId = o.DefID;
        //                int percentage = (int)(((double)index / ReadCityJson.objectVisualisations.Count) * 100);
        //                Console.WriteLine(percentage + "%: " + index + " out of " + ReadCityJson.objectVisualisations.Count + " Visualisations inserted into DB");

        //            }
        //        }
        //        catch
        //        {
        //            Console.WriteLine("Exception when inserting object Visualisation, last added ID is " + lastAddedId);
        //            throw;
        //        }
        //    }

        //    try
        //    {
        //        db.SaveChanges();
        //    }
        //    catch
        //    {
        //        Console.WriteLine("Exception when inserting object Visualisation, last added ID is " + lastAddedId);
        //        throw;
        //    }

        //}
    }
}
