namespace GIMcompress
{
	public class GimObject : IReadOnlyGimObject
	{
		public string name;              //   public name
		public uint id;                  //   public uint id; // Database ID, not in use.
										 //   public string id; // Base64. For new use: "=" + i
		public GimUnitSize gimUnitSize;  //   
		public uint element;             //   pointer to GimData.elements
		// Nullable link into PropertySets, many-to-one for dedup; null = no properties; independent of element so element remaps never shift it.
		public int? propertySetIndex;    //   pointer to GimData.propertySets, null = none

		#region properties
		public string Name
		{
			get => name;
			set => name = value;
		}

		public uint Id
		{
			get => id;
			set => id = value;
		}

		public GimUnitSize GimUnitSize
		{
			get => gimUnitSize;
			set => gimUnitSize = value;
		}

		public uint Element
		{
			get => element;
			set => element = value;
		}

		public int? PropertySetIndex
		{
			get => propertySetIndex;
			set => propertySetIndex = value;
		}
		#endregion

		public GimObject Clone()
		{
			return new GimObject
			{
				name = name,
				id = id,
				GimUnitSize = gimUnitSize?.Clone(),
				element = element,
				propertySetIndex = propertySetIndex
			};
		}

        public override string ToString()
        {
			return element + ": \"" + name + "\"";  
        }
    }
}
