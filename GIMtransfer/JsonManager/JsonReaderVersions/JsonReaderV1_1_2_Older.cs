using GIMcompress;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GIMtransfer
{
    /// <summary>
    /// Reader for the legacy GIM JSON baseline schema. Serves effective versions
    /// strictly below <see cref="JsonVersionPolicy.V1_2"/> and the missing /
    /// unparsable <c>Version</c> fallback. See the folder <c>README.md</c> for
    /// the dispatch table and the reader extension pattern (override
    /// <see cref="ReadJsonCore(JObject)"/>, chain to <c>base</c>, then layer
    /// added shape).
    /// </summary>
    internal class JsonReaderV1_1_2_Older : JsonReader
    {
        /// <inheritdoc />
        public override JsonGimObject ReadJson(string jsonText)
        {
            JObject root = JObject.Parse(jsonText);
            JsonGimObject gimJson = ReadJsonCore(root);
            // Keep a canonical version assigned by a derived reader during core dispatch.
            if (gimJson.Version == null)
                gimJson.Version = JsonVersionPolicy.FormatSchemaVersionString(JsonVersionPolicy.V1_1_2);
            return gimJson;
        }

        /// <summary>
        /// Materializes a <see cref="JsonGimObject"/> from an already-parsed root.
        /// Derived readers extend this by overriding and calling
        /// <c>base.ReadJsonCore(root)</c> before layering their additions.
        /// </summary>
        protected virtual JsonGimObject ReadJsonCore(JObject root)
        {
            JsonGimObject finalJsonObj = new();

            ReadRootAttributes(root, finalJsonObj);
            ReadGLBs(root, finalJsonObj);
            ReadChildren(root, finalJsonObj);
            ReadJoints(root, finalJsonObj);

            return finalJsonObj;
        }

        protected virtual void ReadRootAttributes(JObject root, JsonGimObject finalJsonObj)
        {

            if (root.TryGetValue("Name", out JToken nameToken))
                finalJsonObj.Name = nameToken.ToString();

            if (root.TryGetValue("GIMbin", out JToken gimBinToken))
                finalJsonObj.GIMbin = Convert.FromBase64String(gimBinToken.ToString());

            if (root.TryGetValue("ObjectID", out JToken objectIdToken))
                finalJsonObj.ObjectID = objectIdToken.ToString();

            if (root.TryGetValue("ProjectID", out JToken projectIdToken))
                finalJsonObj.ProjectID = projectIdToken.ToString();

            if (root.TryGetValue("GIMUnitv", out JToken gimUnitvToken))
                finalJsonObj.GIMUnitv = gimUnitvToken.ToString();

            if (root.TryGetValue("GIMUnitToBe", out JToken gimUnitToBeToken))
                finalJsonObj.GIMUnitToBe = gimUnitToBeToken.ToString();

            if (root.TryGetValue("GIMUnits", out JToken gimUnitsToken))
                finalJsonObj.GIMUnits = gimUnitsToken.Values<string>().ToArray();
        }

        protected virtual void ReadGLBs(JObject root, JsonGimObject finalJsonObj)
        {
            if (!root.TryGetValue("GLBs", out JToken glbsToken)) return;

            var glbTokens = glbsToken.Values();
            finalJsonObj.GLBs = new string[glbTokens.Count()];
            int index = 0;
            foreach (var glbToken in glbTokens)
            {
                finalJsonObj.GLBs[index++] = glbToken.ToString();
            }
        }

        protected virtual void ReadChildren(JObject root, JsonGimObject finalJsonObj)
        {
            if (!root.TryGetValue("Children", out JToken childrenToken)) return;

            finalJsonObj.Children = new();
            foreach (var childToken in childrenToken.Values<JToken>())
            {
                finalJsonObj.Children.Add(ReadJsonChild(childToken));
            }
        }

        protected virtual void ReadJoints(JObject root, JsonGimObject finalJsonObj)
        {
            if (!root.TryGetValue("Joints", out JToken jointsToken)) return;

            finalJsonObj.Joints = new();
            foreach (var jointToken in jointsToken.Values<JToken>())
            {
                finalJsonObj.Joints.Add(ReadJsonJoint(jointToken));
            }
        }

        protected virtual JsonGimElement ReadJsonChild(JToken childToken)
        {
            JsonGimElement child = new();

            if (childToken["Name"] != null)
                child.Name = childToken["Name"].ToString();

            if (childToken["ID"] != null)
                child.ID = childToken["ID"].Value<int>();

            if (childToken["Crud"] != null)
                child.Crud = (Crud)childToken["Crud"].Value<int>();

            if (childToken["ObjectID"] != null)
                child.ObjectID = childToken["ObjectID"].Value<int?>();

            if (childToken["ProjectID"] != null)
                child.ProjectID = childToken["ProjectID"].Value<int?>();

            if (childToken["Local"] != null)
            {
                var jLocal = childToken["Local"];
                child.Local = new JsonGimLocal
                {
                    XYZ = ((JArray)jLocal["XYZ"]).ToObject<double[]>(),
                    RPY = ((JArray)jLocal["RPY"]).ToObject<double[]>(),
                    M = (byte)jLocal["M"].Value<int>(),
                };
            }

            return child;
        }

        protected virtual JsonGimJoint ReadJsonJoint(JToken jointToken)
        {
            JsonGimJoint joint = new();

            if (jointToken["Name"] != null)
                joint.Name = jointToken["Name"].ToString();

            if (jointToken["Type"] != null)
                joint.Type = jointToken["Type"].ToString();

            if (jointToken["Parent"] != null)
                joint.Parent = jointToken["Parent"].Value<int>();

            if (jointToken["Child"] != null)
                joint.Child = jointToken["Child"].Value<int>();

            if (jointToken["Origin"] != null)
                joint.Origin = ((JArray)jointToken["Origin"]).ToObject<double[][]>().ToList();

            if (jointToken["Crud"] != null)
                joint.Crud = (Crud)jointToken["Crud"].Value<int>();

            if (jointToken["Limit"] != null)
                joint.Limit = ((JArray)jointToken["Limit"]).ToObject<double[]>();

            if (jointToken["Calibration"] != null)
                joint.Calibration = ((JArray)jointToken["Calibration"]).ToObject<double[]>();

            if (jointToken["Dynamics"] != null)
                joint.Dynamics = ((JArray)jointToken["Dynamics"]).ToObject<double[]>();

            if (jointToken["Mimic_ID"] != null)
                joint.Mimic_ID = jointToken["Mimic_ID"].Value<uint?>();

            if (jointToken["Mimic_Multiplier"] != null)
                joint.Mimic_Multiplier = jointToken["Mimic_Multiplier"].Value<double?>();

            if (jointToken["Mimic_Offset"] != null)
                joint.Mimic_Offset = jointToken["Mimic_Offset"].Value<double?>();

            return joint;
        }
    }
}
