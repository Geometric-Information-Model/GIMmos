using System.Collections.Generic;

namespace GIMcompress
{
	public interface IGimElement : IReadOnlyGimElement
	{

		#region properties
		public new List<GimLocal> Locals
		{
			get;
			set;
		}

		public new GimElementType ElementType
		{
			get;
			set;
		}

		public new double[] ElementDoubles
		{
			get;
			set;
		}

		public new int[] ElementInts
		{
			get;
			set;
		}

		public new byte[] ElementBytes
		{
			get;
			set;
		}

		public new List<uint> Elements
		{
			get;
			set;
		}

		public new bool Orientation
		{
			get;
			set;
		}
		#endregion

	}
}
