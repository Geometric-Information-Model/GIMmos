using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMcompress.Shrinkwrap
{
    public class Segment
    {
        public Vect pointA;
        public Vect pointB;

        public Segment(Vect pointA, Vect pointB)
        {
            this.pointA = pointA;
            this.pointB = pointB;
        }

        public override string ToString()
        {
            return "[" + pointA.ToString() + ", " + pointB.ToString() + "]";
        }

        internal bool SharesPointWith(Segment other)
        {
            if (other == null) return false;
            return other.pointA == pointA || other.pointB == pointB || other.pointA == pointB || other.pointB == pointA;
        }

        public override int GetHashCode()
        {
            return pointA.GetHashCode() + pointB.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if(obj == null) return false;
            if(obj is Segment)
            {
                Segment other = (Segment)obj;

                // can be the same orientation or flipped
                return (((pointA == other.pointA) && (pointB == other.pointB)) || 
                        ((pointA == other.pointB) && (pointB == other.pointA)));
            }

            return false;
        }

        public static bool operator==(Segment segmentA, Segment segmentB)
        {
            return (segmentA.Equals(segmentB));
        }

        public static bool operator !=(Segment segmentA, Segment segmentB)
        {
            return !(segmentA.Equals(segmentB));
        }

    }
}
