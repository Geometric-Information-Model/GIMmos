using System;
using System.Globalization;
using System.IO;
using GIMcompress;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GIMtransfer
{
    /// <summary>
    /// Reader for GIM JSON schema 1.3.0, adding optional property sets and links.
    /// </summary>
    internal class JsonReaderV1_3 : JsonReaderV1_2
    {
        /// <inheritdoc />
        public override JsonGimObject ReadJson(string jsonText)
        {
            JsonGimObject result = ReadJsonCore(ParseRoot(jsonText));
            // Assign schema version once after parsing completes.
            result.Version = JsonVersionPolicy.FormatSchemaVersionString(JsonVersionPolicy.V1_3);
            return result;
        }

        private static JObject ParseRoot(string jsonText)
        {
            using var stringReader = new StringReader(jsonText);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None
            };
            return JObject.Load(jsonReader);
        }

        protected override JsonGimObject ReadJsonCore(JObject root)
        {
            JsonGimObject result = base.ReadJsonCore(root);
            result.PropertySetIndex = ReadPropertySetIndex(root, "PropertySetIndex");
            ReadPropertySets(root, result);

            ValidatePropertySetIndex(result.PropertySetIndex, result.PropertySets.Count);
            foreach (JsonGimElement child in result.Children)
            {
                ValidatePropertySetIndex(child.PropertySetIndex, result.PropertySets.Count);
            }

            return result;
        }

        protected virtual void ReadPropertySets(JObject root, JsonGimObject result)
        {
            result.PropertySets = new();
            if (!root.TryGetValue("PropertySets", out JToken setsToken)) return;
            if (setsToken.Type != JTokenType.Array)
                throw new JsonReaderException("PropertySets must be a JSON array.");

            foreach (JToken setToken in (JArray)setsToken)
            {
                if (setToken.Type != JTokenType.Object)
                    throw new JsonReaderException("Each PropertySets entry must be a JSON object.");

                JsonGimPropertySet set = new();
                foreach (JProperty property in ((JObject)setToken).Properties())
                {
                    set[property.Name] = property.Value.ToObject<JsonGimPropertyValue>();
                }

                result.PropertySets.Add(set);
            }
        }

        protected override JsonGimElement ReadJsonChild(JToken childToken)
        {
            JsonGimElement child = base.ReadJsonChild(childToken);
            child.PropertySetIndex = ReadPropertySetIndex(childToken, "PropertySetIndex");
            return child;
        }

        private static int? ReadPropertySetIndex(JToken owner, string propertyName)
        {
            JToken token = owner[propertyName];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.Integer)
                throw new JsonReaderException($"{propertyName} must be an integer or null.");
            // Reject wide integer values that cannot fit the model's index type.
            if (!int.TryParse(token.ToString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int index))
                throw new ArgumentOutOfRangeException(propertyName);
            return index;
        }

        private static void ValidatePropertySetIndex(int? index, int count)
        {
            if (index.HasValue && (index.Value < 0 || index.Value >= count))
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
