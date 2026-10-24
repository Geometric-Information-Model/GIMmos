using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GIMcompress
{
	/// <summary>Read-only view of a property set.</summary>
	public interface IReadOnlyGimPropertySet
	{
		/// <summary>Gets the property's kind-tagged values.</summary>
		IReadOnlyDictionary<string, GimPropertyValue> Entries { get; }
	}

	/// <summary>Collection of named, typed property values.</summary>
	public class GimPropertySet : IReadOnlyGimPropertySet
	{
		/// <summary>Gets or sets property entries; shared sets should remain unchanged after linking.</summary>
		public Dictionary<string, GimPropertyValue> Entries = new();

		/// <inheritdoc />
		IReadOnlyDictionary<string, GimPropertyValue> IReadOnlyGimPropertySet.Entries => Entries;

		/// <summary>Creates a property set from the supplied entries.</summary>
		/// <param name="entries">Key-value pairs to add.</param>
		/// <returns>Created property set.</returns>
		public static GimPropertySet Create(params (string key, GimPropertyValue value)[] entries)
		{
			GimPropertySet set = new();
			foreach ((string key, GimPropertyValue value) in entries)
				set.Entries[key] = value;
			return set;
		}

		/// <summary>Adds or replaces a string entry.</summary>
		/// <param name="key">Entry key.</param>
		/// <param name="value">String value.</param>
		public void AddString(string key, string value) => Entries[key] = GimPropertyValue.FromString(value);
		/// <summary>Adds or replaces a numeric entry.</summary>
		/// <param name="key">Entry key.</param>
		/// <param name="value">Numeric value.</param>
		public void AddDouble(string key, double value) => Entries[key] = GimPropertyValue.FromDouble(value);

		/// <summary>Returns a readable representation of the property's entries.</summary>
		/// <returns>Formatted entries.</returns>
		public override string ToString()
		{
			StringBuilder result = new StringBuilder("{");
			foreach (KeyValuePair<string, GimPropertyValue> entry in Entries)
			{
				if (result.Length > 1)
					result.Append(", ");

				result.Append(entry.Key).Append(':').Append(entry.Value.Kind).Append('(');
				result.Append(entry.Value.Kind == GimPropertyKind.String
					? "\"" + entry.Value.StringValue + "\""
					: entry.Value.DoubleValue.ToString(CultureInfo.InvariantCulture));
				result.Append(')');
			}

			return result.Append('}').ToString();
		}
	}
}
