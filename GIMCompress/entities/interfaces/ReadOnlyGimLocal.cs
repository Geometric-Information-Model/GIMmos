namespace GIMcompress
{
	public interface IReadOnlyGimLocal
	{
		#region properties
		//public bool AlreadyUsed
		//{
		//	get;
		//}

		public uint Parent
		{
			get;
		}

		public double Px
		{
			get;
		}

		public double Py
		{
			get;
		}

		public double Pz
		{
			get;
		}

		public double Xx
		{
			get;
		}

		public double Xy
		{
			get;
		}

		public double Xz
		{
			get;
		}

		public double Yx
		{
			get;
		}

		public double Yy
		{
			get;
		}

		public double Yz
		{
			get;
		}

		public double Zx
		{
			get;
		}

		public double Zy
		{
			get;
		}

		public double Zz
		{
			get;
		}

		public bool RightHanded
		{
			get;
		}
		#endregion

		public bool DirectionOnly();
		public bool ZAxisIsNull();
	}
}
