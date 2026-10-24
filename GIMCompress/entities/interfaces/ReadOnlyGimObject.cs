

namespace GIMcompress
{
	public interface IReadOnlyGimObject
	{
		#region properties
		public string Name
		{
			get;
		}

		public uint Id
		{
			get;
		}

		public GimUnitSize GimUnitSize
		{
			get;
		}

		public uint Element
		{
			get;
		}

		// Exposes additive set link to readonly consumers.
		public int? PropertySetIndex
		{
			get;
		}
		#endregion

		public GimObject Clone();
	}
}
