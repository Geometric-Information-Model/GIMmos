using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static StartCityJson.LanguageList;

namespace StartCityJson
{
    public class GimProperties
    {
        public class GimProperty
        {
            public int ID { get; set; }
            public byte Language { get; set; }
            public string? Name { get; set; }
            public string? Type { get; set; }
        }
        public static class Properties
        {
            internal static List<GimProperty> GimProperties()
            {
                List<GimProperty> list = new List<GimProperty>();
                string marcelFolder = "replace"; //Assuming this is your Folder
                string gimFolder = "replace";
                string PropertiesFile = File.ReadAllText(gimFolder + "ETIMFEATURE.csv");
                int i = 0;
                using (var reader = new StringReader(PropertiesFile))
                {
                    for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        string[] lineItems = line.Split(';');
                        list.Add(new GimProperty { ID = i, Language = 40, Name = lineItems[1], Type = lineItems[0] });
                        i++;
                    }
                }
                return list;
            }
        }
    }
}
