using System.Collections.Generic;

namespace GIMcompress
{
	/// <summary>Shared accessors for object-linked property values.</summary>
	public static class GimPropertyHelpers
	{
		/// <summary>Resolves object display name using its string-valued property name when present.</summary>
		/// <param name="gimData">Model containing object and property data.</param>
		/// <param name="objectIndex">Index into <see cref="GimData.Objects"/>.</param>
		/// <returns>Property name when it is a string; otherwise object's legacy name.</returns>
		public static string ResolveDisplayName(GimData gimData, uint objectIndex)
		{
			GimObject gimObject = gimData.Objects[(int)objectIndex];
			return TryGetPropertyString(gimData, objectIndex, "name", out string name) ? name : gimObject.name;
		}

		/// <summary>Reads a string-valued entry from an object's linked property set.</summary>
		/// <param name="gimData">Model containing object and property data.</param>
		/// <param name="objectIndex">Index into <see cref="GimData.Objects"/>.</param>
		/// <param name="key">Entry key.</param>
		/// <param name="value">Receives entry value when found.</param>
		/// <returns><see langword="true"/> when linked entry exists and is string-valued.</returns>
		public static bool TryGetPropertyString(GimData gimData, uint objectIndex, string key, out string value)
		{
			value = null;
			if (objectIndex >= (uint)gimData.Objects.Count)
				return false;

			int? setIndex = gimData.Objects[(int)objectIndex].propertySetIndex;
			if (!setIndex.HasValue || setIndex.Value < 0 || setIndex.Value >= gimData.PropertySets.Count)
				return false;

			if (!gimData.PropertySets[setIndex.Value].Entries.TryGetValue(key, out GimPropertyValue property)
				|| property.Kind != GimPropertyKind.String)
				return false;

			value = property.StringValue;
			return true;
		}
	}
}
