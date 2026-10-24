
using System.Collections.Generic;
using System.Text;

namespace GIMtransfer
{
	/// <summary>
	/// A holder class to share the properties of file types.
	/// </summary>
	public class FileTypeProperties
	{
		internal FileTypeProperties(string icon, string name, string description, string version, bool input, bool output, List<Industry> industries, bool yUp)
		{
			this.icon = icon;
			this.name = name;
			this.description = description;
			this.input = input;
			this.output = output;
			this.industries = industries;
			this.yUp = yUp;

			this.lowerInputVersion = version;
			this.upperInputVersion = version;
			this.lowerOutputVersion = version;
			this.upperOutputVersion = version;
		}

		internal FileTypeProperties(string icon, string name, string description, string lowerVersion, string upperVersion, bool input, bool output, List<Industry> industries, bool yUp)
		{
			this.icon = icon;
			this.name = name;
			this.description = description;
			this.input = input;
			this.output = output;
			this.industries = industries;
            this.yUp = yUp;

            this.lowerInputVersion = lowerVersion;
			this.upperInputVersion = upperVersion;
			this.lowerOutputVersion = lowerVersion;
			this.upperOutputVersion = upperVersion;
		}

		internal FileTypeProperties(string icon , string name, string description, string lowerInputVersion, string upperInputVersion, string lowerOutputVersion, string upperOutputVersion, bool input, bool output, List<Industry> industries, bool yUp)
		{
			this.icon = icon;
			this.name = name;
			this.description = description;
			this.input = input;
			this.output = output;
			this.industries = industries;
            this.yUp = yUp;

            this.lowerInputVersion = lowerInputVersion;
			this.upperInputVersion = upperInputVersion;
			this.lowerOutputVersion = lowerOutputVersion;
			this.upperOutputVersion = upperOutputVersion;
		}

        /// <summary>
        /// The default vertical axis of the format.
        /// </summary>
		/// change needed to YVertical
        public readonly bool yUp;

		/// <summary>
		/// The icon location of the filetype.
		/// </summary>
		public readonly string icon;

		/// <summary>
		/// The full name of the filetype.
		/// </summary>
		public readonly string name;

		/// <summary>
		/// An description for the filetype.
		/// </summary>
		public readonly string description;

		/// <summary>
		/// Can be used as an input file.
		/// True if it can be read by us, otherwise false.
		/// </summary>
		public readonly bool input;

		/// <summary>
		/// Can be used as an output file.
		/// True if it can be written by us, otherwise false.
		/// </summary>
		public readonly bool output;

		/// <summary>
		/// The industries this filetype is used in.
		/// </summary>
        public readonly IReadOnlyList<Industry> industries;

		/// <summary>
		/// Generate a string containing a full description of the filetype.
		/// </summary>
        public string FileDescription
		{
			get
			{
				StringBuilder descriptionConstructor = new StringBuilder();

				// Basic information.
				descriptionConstructor.Append(name);
				descriptionConstructor.Append(": \n");
				descriptionConstructor.AppendLine(description);

				// Read or write
				if (input && output)
				{
					descriptionConstructor.Append("We can read and write all versions");
				}
				else if (input )
				{
                    descriptionConstructor.Append("We can only read " + name + " files");
                    if (!(lowerInputVersion.ToLower() == "all" && upperInputVersion.ToLower() == "all"))
					{
                        descriptionConstructor.Append("From version:(");
                        descriptionConstructor.Append(lowerInputVersion);
						descriptionConstructor.Append(") until version (");
						descriptionConstructor.Append(upperInputVersion);
						descriptionConstructor.AppendLine(")");
					}
				}
				else if (output)
				{
                    descriptionConstructor.Append("We can only write " + name + " files");
                    if (!(lowerOutputVersion.ToLower() == "all" && upperOutputVersion.ToLower() == "all"))
					{
                        descriptionConstructor.Append("From version:(");
                        descriptionConstructor.Append("We can only write from version (");
						descriptionConstructor.Append(lowerOutputVersion);
						descriptionConstructor.Append(") until version (");
						descriptionConstructor.Append(upperOutputVersion);
						descriptionConstructor.AppendLine(")");
					}
				}
				else
				{
					descriptionConstructor.Append("We can not read or write ");
				}

				//descriptionConstructor.Append(name);
				//descriptionConstructor.AppendLine(" files.");

				//if (input)
				//{
				//	descriptionConstructor.Append("We can read from version (");
				//	descriptionConstructor.Append(lowerInputVersion);
				//	descriptionConstructor.Append(") until version (");
				//	descriptionConstructor.Append(upperInputVersion);
				//	descriptionConstructor.AppendLine(")");
				//}

				//if (output)
				//{
				//	descriptionConstructor.Append("We can read from version (");
				//	descriptionConstructor.Append(lowerOutputVersion);
				//	descriptionConstructor.Append(") until version (");
				//	descriptionConstructor.Append(upperOutputVersion);
				//	descriptionConstructor.AppendLine(")");
				//}

				if (industries.Count > 1)
				{
					descriptionConstructor.Append(". This filetype is meant for the following industries: ");
					for (int i = 0; i < industries.Count - 1; i++)
					{
						descriptionConstructor.Append(industries[i].ToString());
						descriptionConstructor.Append(", ");
					}
					descriptionConstructor.Append(" and ");
					descriptionConstructor.Append(industries[^1].ToString());
					descriptionConstructor.AppendLine(". ");
				}
				else
				{
					descriptionConstructor.Append(". This filetype is meant for ");
					descriptionConstructor.Append(industries[0]);
					descriptionConstructor.AppendLine(". ");
				}

				return descriptionConstructor.ToString();
			}
		}

		/// <summary>
		/// The miminum input version of the filetype.
		/// We can only read files with a higher (or equal) version to this.
		/// </summary>
		public readonly string lowerInputVersion;

		/// <summary>
		/// The maximum input version for the filetype.
		/// We can only read files with a lower (or equal) version to this.
		/// </summary>
		public readonly string upperInputVersion;

		/// <summary>
		/// The lowest possible version we can create.
		/// </summary>
		private readonly string lowerOutputVersion;

		/// <summary>
		/// The highest possible version we can create.
		/// </summary>
		private readonly string upperOutputVersion;
	}

	/// <summary>
	/// Industry enum.
	/// An enum that holds the possible industries.
	/// </summary>
	public enum Industry
	{
		/// <summary>
		/// Cartography industrues. (Mapping)
		/// </summary>
		Cartography = 1,

		/// <summary>
		/// Civil Engineering
		/// </summary>
		CivilEngineering = 2,

		/// <summary>
		/// Construction Engineering.
		/// </summary>
		ConstructionEngineering = 3,

		/// <summary>
		/// Supplier for Building Engineering 
		/// </summary>
		SupplierForBuildingEngineering = 4,

		/// <summary>
		/// Marine engineering
		/// </summary>
		MarineEngineering = 5,

		/// <summary>
		/// Automotive industry.
		/// </summary>
		Automotive = 6,

		/// <summary>
		/// Machinery.
		/// </summary>
		Machinery = 7,

		/// <summary>
		/// Supplier for machinery.
		/// </summary>
		SupplierForMachinery = 8,
	};
}
