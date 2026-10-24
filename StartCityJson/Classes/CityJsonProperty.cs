using GimBin;
using GIMcompress;
using System;
using GIMtransfer;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CityJson
{
    //public class CityJsonPlot
    //{
    //    public string? Name { get; set; }
    //    public string? Type { get; set; }
    //    public List<int>? Objects { get; set; }
    //    public double Px { get; set; }
    //    public double Py { get; set; }
    //    public int MinGimLat { get; set; } // Latitude / 360 * 40075
    //    public int MaxGimLat { get; set; } // Latitude / 360 * 40075
    //    public int MinGimLong { get; set; } // Longitude / 360 * 40075
    //    public int MaxGimLong { get; set; } // Longitude / 360 * 40075
    //    public GimData? GimData { get; set; } // shape of the surface
    //    public string? GimBin { get; set; }
    //    public string? GimJson { get; set; } // maybe not needed
    //    public CityJsonProperty[]? Properties { get; set; }
    //}
    //public class CityJsonPlotGroup
    //{
    //    public string? Name { get; set; }
    //    public string? Type { get; set; }
    //    public int[]? Plots { get; set; }
    //    public string? GimBin { get; set; } // PolyLine of outerbound
    //    public string? GimJson { get; set; } // Including all references to the plots
    //    public CityJsonProperty[]? Properties { get; set; }
    //}
    //public class CityJsonObject
    //{
    //    //public List<int> Plots { get; set; }
    //    public string? Name { get; set; }
    //    public string? Type { get; set; }
    //    public double Px { get; set; }
    //    public double Py { get; set; }
    //    public double Pz { get; set; }
    //    public double Rp { get; set; }
    //    public double Ry { get; set; }
    //    public double Rr { get; set; }
    //    public GimData? GimData { get; set; } // shape of the surface
    //    public string? GimBin { get; set; }
    //    public string GimJson { get; set; } // maybe not needed
    //    public CityJsonProperty[]? Properties { get; set; } 
    //}
    public class CityJsonProperty
    {
        public int Property { get; set; } 
        public string ValueString { get; set; }
        public ValueType? ValueType { get; set; }
        public byte[]? Value { get; set; }
    }
    public enum ValueType
    {
        Tstring, Tdate, Tint, Tint32, Tint64
    }
    //public class objectsInACity
    //{
    //    public string type { get; set; }
    //    public List<Geometry> geometry { get; set; }

    //    public Attributes attributes;
    //}

    //public class Attributes
    //{
    //    public string threedf_id { get; set; }
    //    public string threedf_class { get; set; }
    //    public int objectid { get; set; }
    //    public string hoortbijtypeoverbrugging { get; set; }
    //    public string bgt_status { get; set; }
    //    public string bronhouder { get; set; }
    //    public string lokaalid { get; set; }
    //    public string eindregistratie { get; set; }
    //    public string identificatienamespace { get; set; }
    //    public string lv_publicatiedatum { get; set; }
    //    public string objectbegintijd { get; set; }
    //    public string objecteindtijd { get; set; }

    //    public string typeoverbruggingsdeel { get; set; }
    //    public string overbruggingisbeweegbaar { get; set; }
    //    public string plus_status { get; set; }
    //    public string relatievehoogteligging { get; set; }
    //    public string tijdstipregistratie { get; set; }

    //}

    //public class CityObjects
    //{
    //    [System.Text.Json.Serialization.JsonPropertyName("geometry")]
    //    public List<Geometry> geometry { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("attributes")]
    //    public Attributes attributes { get; set; }

    //}

    //public class Geometry
    //{
    //    //public List<GeometrySpecifics> geometrySpecifics { get; set; }
    //    //public List<GeometryAttributes> geometryAtttibutes { get; set; }
    //    public string type { get; set; }
    //    public int lod { get; set; }
    //    public List<List<List<int>>> boundaries { get; set; }
    //}

    //public class Attributes
    //{
    //    //public string 3DfID{get;set;}
    //    [System.Text.Json.Serialization.JsonPropertyName("3df_id")]
    //    public string threedf_id { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("3df_class")]
    //    public string threedf_class { get; set; }
    //    public int baseheight { get; set; }
    //    public int roofheight { get; set; }
    //    public string objectid { get; set; }
    //    public string aanduidingrecordinactief { get; set; }
    //    public string aanduidingrecordcorrectie { get; set; }
    //    public string inonderzoek { get; set; }
    //    public string officieel { get; set; }
    //    public string bagpandid { get; set; }
    //    public string documentnummer { get; set; }
    //    public string pandstatus { get; set; }
    //    public string bouwjaar { get; set; }
    //    public string begindatumtijdvakgeldigheid { get; set; }
    //    public string einddatumtijdvakgeldigheid { get; set; }
    //    public string shape_length { get; set; }
    //    public string shape_area { get; set; }

    //}


    //public class Metadata
    //{
    //    public List<double> geographicalExtent { get; set; }
    //    public string citymodelIdentifier { get; set; }
    //    public string datasetReferenceDate { get; set; }
    //    public string datasetCharacterSet { get; set; }
    //    public string datasetTopicCategory { get; set; }
    //    public string distributionFormatVersion { get; set; }
    //    public string spatialRepresentationType { get; set; }
    //    public string metadataStandard { get; set; }

    //    public string metadataStandardVersion { get; set; }
    //    public string metadataCharacterSet { get; set; }
    //    public string metadataDateStamp { get; set; }
    //    public string textures { get; set; }
    //    public string materials { get; set; }
    //    public cityfeatureMetadata cityfeatureMetadata { get; set; }

    //    public List<int> metadataCharpresentLoDsacterSet { get; set; }
    //    public List<string> thematicModels { get; set; }
    //    public string referenceSystem { get; set; }
    //    public string fileIdentifier { get; set; }

    //}
    //public class cityfeatureMetadata 
    //{
    //    [System.Text.Json.Serialization.JsonPropertyName("PlantCover")]
    //    public PlantCover PlantCover { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("LandUse")]
    //    public LandUse LandUse { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("Bridge")]
    //    public Bridge Bridge { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("Road")]
    //    public Road Road { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("Building")]
    //    public Building Building { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("GenericCityObject")]
    //    public GenericCityObject GenericCityObject { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("WaterBody")]
    //    public WaterBody WaterBody { get; set; }


    //}
    //public class WaterBody 
    //{
    //    public int uniqueFeatureCount { get; set; }
    //    public int aggregateFeatureCount { get; set; }
    //    public Dictionary<double, int> presentLoDs { get; set; }
    //}
    //public class GenericCityObject 
    //{
    //    public int uniqueFeatureCount { get; set; }
    //    public int aggregateFeatureCount { get; set; }
    //    public Dictionary<double, int> presentLoDs { get; set; }
    //}
    //public class Building 
    //{
    //    public int uniqueFeatureCount { get; set; }
    //    public int aggregateFeatureCount { get; set; }
    //    public Dictionary<double, int> presentLoDs { get; set; }
    //}
    //public class Road 
    //{
    //    public int uniqueFeatureCount { get; set; }
    //    public int aggregateFeatureCount { get; set; }
    //    public Dictionary<double, int> presentLoDs { get; set; }
    //}
    //public class Bridge 
    //{
    //    public int uniqueFeatureCount { get; set; }
    //    public int aggregateFeatureCount { get; set; }
    //    public Dictionary<double, int> presentLoDs { get; set; }
    //}
    //public class LandUse 
    //{
    //    public int uniqueFeatureCount { get; set; }
    //    public int aggregateFeatureCount { get; set; }
    //    public Dictionary<double, int> presentLoDs { get; set; }
    //}
    //public class PlantCover 
    //{
    //    public int uniqueFeatureCount { get; set; }
    //    public int aggregateFeatureCount { get; set; }
    //    public Dictionary<double, int> presentLoDs { get; set; }

    //}
    //public class RhinoTexturing
    //{
    //    public long[][][] Values { get; set; }
    //}

    //public class Root
    //{
    //    public string type { get; set; }
    //    public string version { get; set; }
    //    public Metadata metadata { get; set; }
    //    [System.Text.Json.Serialization.JsonPropertyName("CityObjects")]
    //    public dynamic CityObjects { get; set; }
    //    public List<List<int>> vertices { get; set; }
    //    public Transform transform { get; set; }
    //}

    //public class Surface
    //{
    //    public string type { get; set; }
    //}

    //public class Texture
    //{
    //    //public List<RhinoTexturing> RhinoTexturing { get; set; }
    //}

    //public class Transform
    //{
    //    public List<double> scale { get; set; }
    //    public List<double> translate { get; set; }
    //}

}
