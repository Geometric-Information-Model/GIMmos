using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
namespace GIMcompress
{
	public interface IReadOnlyGimElement
	{
		#region properties
		public IReadOnlyList<IReadOnlyGimLocal> Locals
		{
			get;
		}

		public GimElementType ElementType
		{
			get;
		}

		public ReadOnlyCollection<double> ElementDoubles
		{
			get;
		}

		public ReadOnlyCollection<int> ElementInts
		{
			get;
		}

		public ReadOnlyCollection<byte> ElementBytes
		{
			get;
		}

		public ReadOnlyCollection<int> ElementBools
		{
			get;
		}

		public IReadOnlyList<uint> Elements
		{
			get;
		}

		public bool Orientation
		{
			get;
		}
		#endregion

		public GimLocal GetFirstLocalThatBelongsToThisParentOfThisElement(int parent);
		public List<GimLocal> GetLocalsThatBelongsToThisParentOfThisElement(int parent);
		public List<Tuple<GimElement, GimLocal>> GetAllChildrenOrdered(int thisIndex, GimData gimData);
		public List<Tuple<int, int>> GetAllChildrenIndexesOrdered(int thisIndex, GimData gimData);
        public GimElement Clone();
		bool Compare(IReadOnlyGimElement gimElement);
		bool CompareWithoutLocals(IReadOnlyGimElement gimElement);
		public string ToString();
		public GimMaterial AsMaterial();
        public bool IsDoubleDistance(uint i);
	}
}
