namespace GIMcompress
{
	// Real data is inconsistent: the same key can arrive as text or a number, so keys alone cannot identify value type.
	// A bare index is ambiguous across string and number values; each kind tag lets its value decode without consulting keys first.
	// Future kinds such as Int or Bool can extend GimPropertyKind without restructuring entries.
	/// <summary>Value kinds currently supported by GimData property sets.</summary>
	public enum GimPropertyKind
	{
		/// <summary>String property value.</summary>
		String = 0,
		/// <summary>Numeric property value.</summary>
		Double = 1
	}

	/// <summary>Typed property value stored in a GimData property set.</summary>
	public readonly struct GimPropertyValue
	{
		/// <summary>Gets value kind.</summary>
		public GimPropertyKind Kind { get; }
		/// <summary>Gets string value when <see cref="Kind"/> is <see cref="GimPropertyKind.String"/>.</summary>
		public string StringValue { get; }
		/// <summary>Gets numeric value when <see cref="Kind"/> is <see cref="GimPropertyKind.Double"/>.</summary>
		public double DoubleValue { get; }

		private GimPropertyValue(GimPropertyKind kind, string stringValue, double doubleValue)
		{
			Kind = kind;
			StringValue = stringValue;
			DoubleValue = doubleValue;
		}

		/// <summary>Creates a string property value.</summary>
		/// <param name="v">String value.</param>
		/// <returns>Created string property value.</returns>
		public static GimPropertyValue FromString(string v) => new GimPropertyValue(GimPropertyKind.String, v, 0);
		/// <summary>Creates a numeric property value.</summary>
		/// <param name="v">Numeric value.</param>
		/// <returns>Created numeric property value.</returns>
		public static GimPropertyValue FromDouble(double v) => new GimPropertyValue(GimPropertyKind.Double, null, v);
	}
}
