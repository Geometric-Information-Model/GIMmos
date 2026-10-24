namespace GIMcompress
{
	/// <summary>Creates GimObject instances with normalized names.</summary>
	public static class GimObjectFactory
	{
		/// <summary>Creates a GimObject and converts a null name to an empty string.</summary>
		/// <param name="element">Target element index.</param>
		/// <param name="name">Object name, or null for an unnamed object.</param>
		/// <param name="propertySetIndex">Optional property-set index.</param>
		/// <returns>Created object with a non-null name.</returns>
		public static GimObject Create(uint element, string name, int? propertySetIndex = null)
		{
			return new GimObject
			{
				element = element,
				// Empty names keep unnamed objects valid for consumers that require strings.
				name = name ?? string.Empty,
				propertySetIndex = propertySetIndex
			};
		}
	}
}
