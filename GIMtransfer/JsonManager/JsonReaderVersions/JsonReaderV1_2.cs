using GIMcompress;
using Newtonsoft.Json.Linq;

namespace GIMtransfer
{
    /// <summary>
    /// Reader for GIM JSON schema 1.2.0. See the folder <c>README.md</c> for the
    /// full dispatch table and feature thresholds.
    /// </summary>
    internal class JsonReaderV1_2 : JsonReaderV1_1_2_Older
    {
        protected override JsonGimObject ReadJsonCore(JObject root)
        {
            JsonGimObject finalJsonObj = base.ReadJsonCore(root);
            finalJsonObj.Version = JsonVersionPolicy.FormatSchemaVersionString(JsonVersionPolicy.V1_2);

            ReadMaterials(root, finalJsonObj);

            return finalJsonObj;
        }

        protected virtual void ReadMaterials(JObject root, JsonGimObject finalJsonObj)
        {
            if (!root.TryGetValue("Materials", out JToken materialsToken)) return;

            finalJsonObj.Materials = new();
            foreach (var materialToken in materialsToken.Values<JToken>())
            {
                finalJsonObj.Materials.Add(ReadJsonMaterial(materialToken));
            }
        }

        protected override JsonGimElement ReadJsonChild(JToken childToken)
        {
            JsonGimElement child = base.ReadJsonChild(childToken);

            if (childToken["MaterialID"] != null)
                child.MaterialID = childToken["MaterialID"].Value<int?>();

            return child;
        }

        protected virtual JsonGimMaterial ReadJsonMaterial(JToken materialToken)
        {
            JsonGimMaterial material = new();

            if (materialToken["BaseColour"] != null)
                material.BaseColour = materialToken["BaseColour"].ToString();

            if (materialToken["AmbientColour"] != null)
                material.AmbientColour = materialToken["AmbientColour"].ToString();

            if (materialToken["DiffuseColour"] != null)
                material.DiffuseColour = materialToken["DiffuseColour"].ToString();

            if (materialToken["EmissionColour"] != null)
                material.EmissionColour = materialToken["EmissionColour"].ToString();

            if (materialToken["SpecularColour"] != null)
                material.SpecularColour = materialToken["SpecularColour"].ToString();

            if (materialToken["Shininess"] != null)
                material.Shininess = (float)materialToken["Shininess"].Value<double>();

            if (materialToken["Crud"] != null)
                material.Crud = (Crud)materialToken["Crud"].Value<int>();

            return material;
        }
    }
}
