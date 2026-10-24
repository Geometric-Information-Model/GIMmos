namespace GIMcompress
{
	public class GimMaterial : GimElement, IGimElement
	{
		public GimMaterial()
		{
			ElementInts = new int[5];
			ElementDoubles = new double[1];
		}

		public int BaseColour
		{
			get => ElementInts[0];
			set => ElementInts[0] = value;
		}

		public int AmbientColour
		{
			get => ElementInts[1];
			set => ElementInts[1] = value;
		}

		public int DiffuseColour
		{
			get => ElementInts[2];
			set => ElementInts[2] = value;
		}

		public int SpecularColour
		{
			get => ElementInts[3];
			set => ElementInts[3] = value;
		}

		public int EmissionColour
		{
			get => ElementInts[4];
			set => ElementInts[4] = value;
		}

		public float Shininess
		{
			get => (float)ElementDoubles[0];
			set => ElementDoubles[0] = value;
		}

		// LineWeight is disabled pending review of how it should be represented.
		// public double LineWeight
		// {
		// 	get => ElementDoubles.Length > 1 ? ElementDoubles[1] : 0;
		// 	set => ElementDoubles[1] = value;
		// }

		public new GimMaterial AsMaterial()
		{
			return this;
		}
	}
}
