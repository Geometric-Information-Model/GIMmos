using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
// Newtonsoft.Json and System.Text.Json both serialize these DTOs, so each needs its own attributes.
using StjIgnore = System.Text.Json.Serialization.JsonIgnoreCondition;
using StjJsonIgnore = System.Text.Json.Serialization.JsonIgnoreAttribute;
using StjJsonInclude = System.Text.Json.Serialization.JsonIncludeAttribute;
using StjName = System.Text.Json.Serialization.JsonPropertyNameAttribute;

namespace GIMcompress
{
    public static class JsonSettings
    {
        /// <summary>
        /// Retrieves a pre-configured JsonSerializerSettings instance.
        /// </summary>
        /// <remarks>
        /// This method returns JsonSerializerSettings with GIM Json specific settings:
        /// - NullValueHandling is set to Ignore, so properties with null values are ignored.
        /// - Formatting is set to Indented, which formats the JSON output for readability.
        /// 
        /// Use this method to get a consisten tGIM Json JsonSerializerSettings configuration 
        /// throughout the application.
        /// </remarks>
        /// <returns>
        /// A GIM Json JsonSerializerSettings instance with pre-defined settings.
        /// </returns>
        public static JsonSerializerSettings GetSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Newtonsoft.Json.Formatting.Indented
            };
        }
    }

	/// <summary>Root JSON DTO; property sets belong to root and children reference them by index.</summary>
    public class JsonGimObject
	{
		private string version = "1.1.2"; //change whenever new json verison is updated
		private string name;
		private string projectID;
        private string objectID; 
		private uint[] gimBin;
        private string gimUnitv;
		private string gimUnitToBe;
        private string[] gimUnits;
        private GimUnitSize gimScale = null;
        private string glb;
		private List<JsonGimElement> children = new List<JsonGimElement>(); // Generated from element.elements
		private List<JsonGimJoint> joints;
		private List<JsonGimMaterial> materials;
        private List<JsonGimPropertySet> propertySets = new();
        //HashSet<string> glbs = new HashSet<string>();

		private string[]? glbs;
        private int? propertySetIndex;

		/// <summary>Creates a JSON object with embedded model payload and display name.</summary>
		/// <param name="gimBin">Model payload.</param>
		/// <param name="name">Display name.</param>
		/// <param name="glb">Optional visualization payload.</param>
		public JsonGimObject(uint[] gimBin, string name = "", string glb = "")
		{
			this.glb = glb;
			this.name = name;
			this.gimBin = gimBin;
		}

		/// <summary>Creates an empty root JSON object.</summary>
		public JsonGimObject() { }

		#region properties


		/// <summary>
		/// Schema version string for the materialized document. The setter is <c>internal</c>
		/// so version-specific readers in <c>GIMtransfer</c> can assign the canonical value
		/// for their tier (see <c>JsonVersionPolicy</c> and the JSON reader README).
		/// </summary>
		public string Version { get => version; internal set => version = value; }

		public string Name
		{
			get => name;
			set => name = value;
		}
		public string ProjectID
		{
			get => projectID;
			set => projectID = value;
		}
		public byte[] GIMbin
		{
			get
			{
				if (gimBin != null)
				{
					//if (BitConverter.IsLittleEndian)
					//	return gimBin.SelectMany((x) => (BitConverter.GetBytes(x).Reverse())).ToArray();
					//else
					//	return gimBin.SelectMany(BitConverter.GetBytes).ToArray();

					byte[] byteArray = new byte[4*gimBin.Length];

					for (int i = 0; i < gimBin.Length; i++)
                    {
						uint val = gimBin[i];

						byte byte1 = (byte)( (val & 0xFF000000) >> 24);
						byte byte2 = (byte)( (val & 0x00FF0000) >> 16);
						byte byte3 = (byte)( (val & 0x0000FF00) >> 8);
						byte byte4 = (byte)( (val & 0x000000FF));

						byteArray[4*i] = byte1;
						byteArray[4*i+1] = byte2;
						byteArray[4*i+2] = byte3;
						byteArray[4*i+3] = byte4;
                    }

					return byteArray;
				}
				return null;
			}
			set
			{
				if (value != null)
				{

					byte[] temp = (byte[])value.Clone();
					//Array.Reverse(temp);
					gimBin = new uint[temp.Length / 4];
					for (int i = 0; i < temp.Length; i += 4)
					{
						byte byte1;
						byte byte2;
						byte byte3;
						byte byte4;
						byte1 = temp[i];
						byte2 = temp[i + 1];
						byte3 = temp[i + 2];
						byte4 = temp[i + 3];
						uint result = ((uint)byte1 << 24) + ((uint)byte2 << 16) + ((uint)byte3 << 8) + (uint)byte4;
						gimBin[i / 4] = result;
					}

				}
				else { gimBin = null; }
			}
		}

        public string GIMUnitv
        {
            get => gimUnitv;
            set => gimUnitv = value;
        }

        public string GIMUnitToBe
        {
            get => gimUnitToBe;
            set => gimUnitToBe = value;
        }

        public string[] GIMUnits
        {
            get => gimUnits;
            set => gimUnits = value;
        }

        public GimUnitSize GIMscale
		{
			get => gimScale;
			set => gimScale = value;
		}
		public string GLB
		{
			get => glb;
			set => glb = value;
		}
		public List<JsonGimElement> Children
		{
			get => children;
			set => children = value;
		}


		public List<JsonGimJoint> Joints
		{
			get => joints;
			set => joints = value;
		}

		public string[] GLBs
		{
			get => glbs;
			set => glbs = value;
		}

        public List<JsonGimMaterial> Materials
        {
            get => materials;
            set => materials = value;
        }

        /// <summary>Gets or sets root-owned property sets referenced by child links.</summary>
        [JsonIgnore]
        [StjJsonIgnore]
        public List<JsonGimPropertySet> PropertySets
        {
            get => propertySets;
            set => propertySets = value ?? new();
        }

        // Keep public model access non-null while omitting empty root sets from JSON output.
        [JsonProperty("PropertySets", NullValueHandling = NullValueHandling.Ignore)]
        [StjJsonInclude]
        [StjName("PropertySets")]
        [StjJsonIgnore(Condition = StjIgnore.WhenWritingNull)]
        private List<JsonGimPropertySet> SerializedPropertySets
        {
            get => propertySets.Count == 0 ? null : propertySets;
            set => propertySets = value ?? new();
        }

        /// <summary>Gets or sets root object's nullable property-set link.</summary>
        [StjJsonIgnore(Condition = StjIgnore.WhenWritingNull)]
        public int? PropertySetIndex
        {
            get => propertySetIndex;
            set => propertySetIndex = value;
        }

        public string ObjectID { get => objectID; set => objectID = value; }
        #endregion
    }

	public class JsonGimElement
	{
        private JsonGimLocal local;
        private int id;
        private int? projectID;
		private int? objectID;
		private string name;
		private Crud crud;
		private int? materialID;
        private int? propertySetIndex;

		public JsonGimElement(string name, JsonGimLocal local, int id, int? objectID = null, int? projectID = null)
		{
			this.local = local;
			this.name = name;
			this.id = id;
			this.projectID = projectID;
			this.objectID = objectID;
			crud = Crud.Read;
		}

		public JsonGimElement() { }

		#region Properties

		public int? MaterialID
		{
			get
			{
				return materialID;
			}
			set
			{
				materialID = value;
			}
		}

        /// <summary>Gets or sets child's nullable index into root-owned property sets.</summary>
        [StjJsonIgnore(Condition = StjIgnore.WhenWritingNull)]
        public int? PropertySetIndex
        {
            get
            {
                return propertySetIndex;
            }
            set
            {
                propertySetIndex = value;
            }
        }

		public string Name
		{
			get => name;
			set => name = value;
		}
		public int ID
		{
			get => id;
			set => id = value;
		}

		public int? ObjectID
        {
			get => objectID;
			set => objectID = value;
        }

		public int? ProjectID
		{
			get => projectID;
			set => projectID = value;
		}
		public JsonGimLocal Local
		{
			get => local;
			set => local = value;
		}
		public Crud Crud
		{
			get => crud;
			set => crud = value;
		}
		#endregion
	}

	public class JsonGimLocal
	{
		double[][] matrix; // 4 lines for the transformation matrix and 1 for the basis point
		bool rightHanded;
		public JsonGimLocal(double px, double py, double pz, double xx, double xy, double xz, double yx, double yy, double yz,
			double zx, double zy, double zz, bool rightHanded = true)
		{
			matrix = new double[4][];

			// basis point is the last line
			matrix[3] = new double[3];
			matrix[3][0] = px;
			matrix[3][1] = py;
			matrix[3][2] = pz;

			// rest of the matrix
			matrix[0] = new double[3];
			matrix[0][0] = xx;
			matrix[0][1] = xy;
			matrix[0][2] = xz;

			matrix[1] = new double[3];
			matrix[1][0] = yx;
			matrix[1][1] = yy;
			matrix[1][2] = yz;

			matrix[2] = new double[3];
			matrix[2][0] = zx;
			matrix[2][1] = zy;
			matrix[2][2] = zz;

			this.rightHanded = rightHanded;
		}

		public JsonGimLocal(GimLocal local)
		{
			matrix = new double[4][];

			// basis point is the last line
			matrix[3] = new double[3];
			matrix[3][0] = local.px;
			matrix[3][1] = local.py;
			matrix[3][2] = local.pz;

			// rest of the matrix
			matrix[0] = new double[3];
			matrix[0][0] = local.xx;
			matrix[0][1] = local.xy;
			matrix[0][2] = local.xz;

			matrix[1] = new double[3];
			matrix[1][0] = local.yx;
			matrix[1][1] = local.yy;
			matrix[1][2] = local.yz;

			matrix[2] = new double[3];
			matrix[2][0] = local.zx;
			matrix[2][1] = local.zy;
			matrix[2][2] = local.zz;

			rightHanded = local.rightHanded;
		}

		public JsonGimLocal() { }

		#region Properties

		// Vestigial 
		private double[][] Matrix
		{
			get => matrix;
			set => matrix = value;
		}

		public double[] XYZ
		{
			get
			{
                if (matrix == null)
                {
                    matrix = new double[4][];
                    matrix[0] = new double[3];
                    matrix[1] = new double[3];
                    matrix[2] = new double[3];
                    matrix[3] = new double[3];
                }
                return new double[]
				{
					matrix[3][0],
					matrix[3][1],
					matrix[3][2]
				};
			}
			set 
			{
				if (matrix == null) 
				{
					matrix = new double[4][];
					matrix[0] = new double[3];
					matrix[1] = new double[3];
					matrix[2] = new double[3];
					matrix[3] = new double[3];
				}
				matrix[3][0] = value[0];
				matrix[3][1] = value[1];
				matrix[3][2] = value[2];
			}
		}

		public double[] RPY
		{
			get
			{
                if (matrix == null)
                {
                    matrix = new double[4][];
                    matrix[0] = new double[3];
                    matrix[1] = new double[3];
                    matrix[2] = new double[3];
                    matrix[3] = new double[3];
                }
                double yaw = Math.Atan2(matrix[1][0], matrix[0][0]);
				double pitch = Math.Atan2(-matrix[2][0], Math.Sqrt(matrix[2][1] * matrix[2][1] + matrix[2][2] * matrix[2][2]));
				double roll = Math.Atan2(matrix[2][1], matrix[2][2]);

				//if (!rightHanded) pitch = -pitch;

				// eliminating negative zeroes
				if (yaw == 0) yaw = 0;
				if (pitch == 0) pitch = 0;
				if (roll == 0) roll = 0;

				if (VectorHelpers.CompareDouble(Math.Abs(pitch), Math.PI / 2))
				// pitch is maximal/minimal, in this case yaw and roll degenerate into being the same angle
				// therefore we set roll to be zero, and simply just take the value of the yaw
				{
					double sign = Math.Sin(pitch);

					yaw = Math.Atan2(sign * matrix[1][2], matrix[1][1]);
					roll = 0;
				}
				return new double[] { roll, pitch, yaw };
			}
			set 
			{
				if (matrix == null)
				{
					matrix = new double[4][];
					matrix[0] = new double[3];
					matrix[1] = new double[3];
					matrix[2] = new double[3];
					matrix[3] = new double[3];
				}
				matrix[0][0] = (Math.Cos(value[2]) * Math.Cos(value[1]));
				matrix[0][1] = (Math.Cos(value[2]) * Math.Sin(value[1]) * Math.Sin(value[0]) - Math.Sin(value[2]) * Math.Cos(value[0]));
				matrix[0][2] = (Math.Cos(value[2]) * Math.Sin(value[1]) * Math.Cos(value[0]) + Math.Sin(value[2]) * Math.Sin(value[0]));
				matrix[1][0] = Math.Sin(value[2]) * Math.Cos(value[1]);
				matrix[1][1] = Math.Sin(value[2]) * Math.Sin(value[1]) * Math.Sin(value[0]) + Math.Cos(value[2]) * Math.Cos(value[0]);
				matrix[1][2] = Math.Sin(value[2]) * Math.Sin(value[1]) * Math.Cos(value[0]) - Math.Cos(value[2]) * Math.Sin(value[0]);
				matrix[2][0] = -Math.Sin(value[1]);
				matrix[2][1] = Math.Cos(value[1]) * Math.Sin(value[0]);
				matrix[2][2] = Math.Cos(value[1]) * Math.Cos(value[0]);
			}
		}

		public byte M
		{
			get
			{
				if (rightHanded) return 0; return 1;
			}
			set
			{
				rightHanded = (value == 0);
			}
		}

		#endregion
	}

	public class JsonGimJoint
	{
        private string name;
        private string type;
        private int parent;
        private int child;
        private List<double[]> origin = new List<double[]>();
        private double[] limit = new double[] { 0, 0, 0, 0 };
        private double[] calibration = new double[2];
        private double[] dynamics = new double[2] { 0, 0 };
        private uint? mimic_ID;
        private double? mimic_Multiplier;
        private double? mimic_Offset = 0.0;
        private Crud crud = Crud.Read;

        public JsonGimJoint(string name, GimJointType type, Vect[] origin, int parent, int child, double[] limit, GimJointStatus status, double[] calibration, double[] dynamics, string mimic_Name, double mimic_Multiplier, double mimic_Offset)
        {
            this.name = name;
            Type = Enum.GetName(type.GetType(), type);
            this.parent = parent;
            this.child = child;
            this.origin = new();
            foreach (Vect vectPoint in origin)
            {
                this.origin.Add((double[])vectPoint);
            }
            this.limit = limit;
            this.calibration = calibration;
            this.dynamics = dynamics;
            this.mimic_ID = mimic_ID;
			this.mimic_Multiplier = mimic_Multiplier;
			this.mimic_Offset = mimic_Offset;
        }
        public JsonGimJoint(GimJoint joint) : this(joint.Name, joint.Type, joint.Origin, joint.Parent, joint.Child, joint.Limit, joint.Status, joint.Calibration, joint.Dynamics, joint.Mimic_ID, joint.Mimic_Multiplier, joint.Mimic_Offset)
		{ }

		public JsonGimJoint() { }

		#region Properties
		public string Name
		{
			get { return name; }
			set { name = value; }
		}
		public string Type
		{
			get { return type; }
			set { type = value; }
		}
		public int Parent
		{
			get { return (int)parent; }
			set { parent = value; }
		}
		public int Child
		{
			get { return (int)child; }
			set { child = value; }
		}
		public List<double[]> Origin
		{
			get { return origin; }
			set { origin = value; }
		}
		public double[] Limit
		{
			get { return limit; }
			set { limit = value; }
		}
		public Crud Crud
		{
			get { return crud; }
			set { crud = value; }
		}

        public double[] Calibration 
		{ 
			get => calibration; 
			set => calibration = value; 
		}
        public double[] Dynamics { get => dynamics; set => dynamics = value; }
        public uint? Mimic_ID { get => mimic_ID; set => mimic_ID = value; }
        public double? Mimic_Multiplier { get => mimic_Multiplier; set => mimic_Multiplier = value; }
        public double? Mimic_Offset { get => mimic_Offset; set => mimic_Offset = value; }

        #endregion
    }

    /// <summary>Named property values included in the root JSON object's property-set list.</summary>
    public class JsonGimPropertySet : Dictionary<string, JsonGimPropertyValue>
    {
    }

    /// <summary>JSON representation of one kind-tagged property value.</summary>
    [JsonConverter(typeof(JsonGimPropertyValueNewtonsoftConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonGimPropertyValueSystemTextJsonConverter))]
    public class JsonGimPropertyValue
    {
        /// <summary>Gets or sets value kind.</summary>
        [JsonProperty("kind")]
        [StjName("kind")]
        public string Kind { get; set; }

        /// <summary>Gets or sets value payload.</summary>
        [JsonProperty("value")]
        [StjName("value")]
        public object Value { get; set; }

        /// <summary>Creates string-kind value from its primitive JSON representation.</summary>
        public static JsonGimPropertyValue FromWireString(string value) =>
            new() { Kind = "string", Value = value ?? throw new JsonSerializationException("Property string value cannot be null.") };

        /// <summary>Creates double-kind value from its primitive JSON representation.</summary>
        public static JsonGimPropertyValue FromWireNumber(double value) =>
            new() { Kind = "double", Value = value };

        /// <summary>Gets primitive JSON representation for this property value.</summary>
        public object ToWireValue() => Kind switch
        {
            "string" when Value is string value => value,
            "double" when Value is double value => value,
            _ => throw new JsonSerializationException("Property value must be string or double.")
        };
    }

    /// <summary>Converts property values to and from primitive Newtonsoft JSON values.</summary>
    public sealed class JsonGimPropertyValueNewtonsoftConverter : JsonConverter<JsonGimPropertyValue>
    {
        public override JsonGimPropertyValue ReadJson(
            JsonReader reader,
            Type objectType,
            JsonGimPropertyValue existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                return JsonGimPropertyValue.FromWireString((string)reader.Value);
            if (reader.TokenType == JsonToken.Date)
                return JsonGimPropertyValue.FromWireString(FormatDateValue(reader.Value));
            if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
                return JsonGimPropertyValue.FromWireNumber(Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture));
            throw new JsonReaderException("Property values must be JSON strings or numbers.");
        }

        public override void WriteJson(JsonWriter writer, JsonGimPropertyValue value, JsonSerializer serializer) =>
            writer.WriteValue(value.ToWireValue());

        private static string FormatDateValue(object value) => value switch
        {
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Converts property values to and from primitive System.Text.Json values.</summary>
    public sealed class JsonGimPropertyValueSystemTextJsonConverter :
        System.Text.Json.Serialization.JsonConverter<JsonGimPropertyValue>
    {
        public override JsonGimPropertyValue Read(
            ref System.Text.Json.Utf8JsonReader reader,
            Type typeToConvert,
            System.Text.Json.JsonSerializerOptions options)
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.String)
                return JsonGimPropertyValue.FromWireString(reader.GetString());
            if (reader.TokenType == System.Text.Json.JsonTokenType.Number &&
                reader.TryGetDouble(out double number))
                return JsonGimPropertyValue.FromWireNumber(number);
            throw new System.Text.Json.JsonException("Property values must be JSON strings or numbers.");
        }

        public override void Write(
            System.Text.Json.Utf8JsonWriter writer,
            JsonGimPropertyValue value,
            System.Text.Json.JsonSerializerOptions options) =>
            System.Text.Json.JsonSerializer.Serialize(writer, value.ToWireValue(), options);
    }

	public class JsonGimMaterial
	{
		private GimMaterial material = new();
        private Crud crud = Crud.Read;

        #region properties
        public JsonGimMaterial(GimMaterial material)
		{
			this.material = material;
		}

		public JsonGimMaterial()
		{
			material = new GimMaterial();
		}

		public string BaseColour
		{
			get
			{
				var gimCol = GimColour.FromInt(material.BaseColour);
				return gimCol.ToHexString();
			}

			set
			{
				// Hex code material
				if (value[0] == '#')
				{
					var gimCol = GimColour.FromHexString(value);
                    material.BaseColour = gimCol.ToInt();
				}
				else
				{
					throw new NotImplementedException("Only HexCode Colours are supported for materials currently!");
				}
			}

		}

        public string SpecularColour
        {
            get
            {
                var gimCol = GimColour.FromInt(material.SpecularColour);
                return gimCol.ToHexString();
            }

            set
            {
                // Hex code material
                if (value[0] == '#')
                {
                    var gimCol = GimColour.FromHexString(value);
                    material.SpecularColour = gimCol.ToInt();
                }
                else
                {
                    throw new NotImplementedException("Only HexCode Colours are supported for materials currently!");
                }
            }

        }


        public string DiffuseColour
        {
            get
            {
                var gimCol = GimColour.FromInt(material.DiffuseColour);
                return gimCol.ToHexString();
            }

            set
            {
                // Hex code material
                if (value[0] == '#')
                {
                    var gimCol = GimColour.FromHexString(value);
                    material.DiffuseColour = gimCol.ToInt();
                }
                else
                {
                    throw new NotImplementedException("Only HexCode Colours are supported for materials currently!");
                }
            }

        }


        public string EmissionColour
        {
            get
            {
                var gimCol = GimColour.FromInt(material.EmissionColour);
                return gimCol.ToHexString();
            }

            set
            {
                // Hex code material
                if (value[0] == '#')
                {
                    var gimCol = GimColour.FromHexString(value);
                    material.EmissionColour = gimCol.ToInt();
                }
                else
                {
                    throw new NotImplementedException("Only HexCode Colours are supported for materials currently!");
                }
            }

        }


        public string AmbientColour
        {
            get
            {
                var gimCol = GimColour.FromInt(material.AmbientColour);
                return gimCol.ToHexString();
            }

            set
            {
                // Hex code material
                if (value[0] == '#')
                {
                    var gimCol = GimColour.FromHexString(value);
                    material.AmbientColour = gimCol.ToInt();
                }
                else
                {
                    throw new NotImplementedException("Only HexCode Colours are supported for materials currently!");
                }
            }

        }

		public float Shininess
		{
			get
			{
				return material.Shininess;
			}
			set
			{
				material.Shininess = value;
			}
		}

        #endregion

		public Crud Crud
		{
			get { return crud; }
			set { crud = value; }
		}

    }

    public enum Crud { Create, Read, Update, Delete }
}
