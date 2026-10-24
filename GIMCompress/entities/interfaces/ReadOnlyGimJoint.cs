namespace GIMcompress
{
	public interface IReadOnlyGimJoint
    {

        #region properties
        public int Child { get;  }
        public int Parent { get; }
        public Vect[] Origin { get; }

        #endregion

        public bool OverlapsWith(GimJoint otherJoint);
        public string ToString();
    }
}