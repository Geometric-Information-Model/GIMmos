using System.Collections.Generic;

namespace GIMcompress
{
	/// <summary>
	/// A readonly interface for GimDatas so that we can be sure the GimData is not edited.
	/// </summary>
	public interface IReadOnlyGimData
	{
		public IReadOnlyList<IReadOnlyGimObject> Objects
		{
			get;
		}

		public IReadOnlyList<IReadOnlyGimElement> Elements
		{
			get;
		}

		public IReadOnlyList<IReadOnlyGimJoint> Joints
		{
			get;
		}

		// Exposes additive set store to readonly consumers.
		public IReadOnlyList<IReadOnlyGimPropertySet> PropertySets
		{
			get;
		}

		public GimData Clone();
		public void WriteElementTypeCount();
        public bool ElementContainsChildType(uint element, GimElementType type);

    }
}
