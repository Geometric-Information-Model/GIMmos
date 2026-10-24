using System;

namespace GIMcompress
{
	public class GimJoint : IReadOnlyGimJoint
    {
        private Vect[] origin;
        private int parent;
        private int child;
        private int id;
        private double[] limit; // double[4] {lower, upper, effort, velocity}
        private double[] calibration; //double[2] {rising, falling}
        private double[] dynamics; //double[2] {damping, friction}
        private string mimic_ID;
        private double mimic_Multiplier;
        private double mimic_Offset = 0.0;
        private GimJointStatus status = GimJointStatus.New_LowQuality;
        private GimJointType type;
        private int crud;

        private static int count = 0;

        //TODO WRITE GETSET
        #region properties 

        public Vect[] Origin
        {
            get => origin;
            set => origin = value;
        }

        public int Parent
        {
            get => parent;
            set => parent = value;
        }

        public int Child
        {
            get => child;
            set => child = value;
        }


        public double[] Calibration
        {
            get => calibration;
            set => calibration = value;
        }
        public double[] Dynamics
        {
            get => dynamics; set => dynamics = value;
        }
        public string Mimic_ID
        {
            get => mimic_ID;
            set => mimic_ID = value;
        }
        public double Mimic_Multiplier
        {
            get => mimic_Multiplier;
            set => mimic_Multiplier = value;
        }
        public double Mimic_Offset
        {
            get => mimic_Offset;
            set => mimic_Offset = value;
        }

        public string Name
        {
            get => Id.ToString() + " " + Status.ToString();
        }
        public int Crud { get => crud; set => crud = value; }
        public double[] Limit { get => limit; set => limit = value; }
        public GimJointType Type { get => type; set => type = value; }
        public int Id { get => id; set => id = value; }
        public GimJointStatus Status { get => status; set => status = value; }

        #endregion
        public GimJoint(int parent, int child, GimJointType type, int crud, double[] limit, double[] calibration, double[] dynamics, double? mimic_Multiplier, double? mimic_Offset, params Vect[] origin)
        {
            this.child = child;
            this.parent = parent;
            this.origin = (Vect[])origin.Clone();
            this.Type = type;
            this.crud = crud;
            this.Limit = limit;
            this.calibration = calibration;
            this.dynamics = dynamics;
            this.mimic_ID = null;
            this.mimic_Multiplier = mimic_Multiplier != null ? (double)mimic_Multiplier : 0;
            this.mimic_Offset = mimic_Offset != null ? (double)mimic_Offset : 0;
            Id = count;
            count++;
        }
        public GimJoint(int parent, int child, GimJointType type, params Vect[] origin)
        {
            this.child = child;
            this.parent = parent;
            this.origin = (Vect[])origin.Clone();
            this.Type = type;
            this.Limit = new double[] { 0, 0, 0, 0 };
            this.calibration = new double[] { 0, 0 };
            this.dynamics = new double[] { 0, 0 };
            this.mimic_ID = null;
            this.mimic_Multiplier = 0;
            this.mimic_Offset = 0;
            Id = count;
            count++;
        }

        public GimJoint(int parent, int child, GimJointType type, double[] limit, double[] calibration, double[] dynamics, string mimic_Name, double? mimic_Multiplier, double? mimic_Offset, params Vect[] origin)
        {
            this.child = child;
            this.parent = parent;
            this.origin = (Vect[])origin.Clone();
            this.Type = type;
            this.Limit = limit;
            this.calibration = calibration;
            this.dynamics = dynamics;
            this.mimic_ID = mimic_Name;
            this.mimic_Multiplier = mimic_Multiplier != null? (double)mimic_Multiplier : 0;
            this.mimic_Offset = mimic_Offset != null? (double) mimic_Offset : 0;
            Id = count;
            count++;
        }

        public bool OverlapsWith(GimJoint otherJoint) // checks if the two overlap, perhaps to create a similar joint
        {
            if (Type == GimJointType.Prismatic)
            {
                if (otherJoint.Type != GimJointType.Prismatic) return false;
            }

            if (Type == GimJointType.Continouous)
            {
                if (otherJoint.Type == GimJointType.Continouous)
                {
                    Vect direction = (origin[1] - origin[0]).Direction;
                    Vect otherDirection = otherJoint.origin[1] - otherJoint.origin[0];
                    if (!direction.IsCollinearTo(otherDirection)) return false;

                    Vect basePoint = origin[0];
                    Vect otherBasePoint = otherJoint.origin[0];
                    Vect differenceDirection = otherBasePoint - basePoint;
                    if (!differenceDirection.IsCollinearTo(direction)) return false;

                    Vect endPoint = origin[1];
                    Vect otherEndPoint = otherJoint.origin[1];

                    double l = (endPoint - basePoint) * direction;

                    double distance1 = (otherBasePoint - basePoint) * direction;
                    double distance2 = (otherEndPoint - basePoint) * direction;

                    double s = Math.Min(distance1, distance2);
                    double e = Math.Max(distance1, distance2);

                    double margin = Matrix.EQUALITY_MARGIN;

                    if (e > l + margin && s > l + margin) return false;
                    if (e < -margin && s < -margin) return false;

                    return true;
                }
                if (otherJoint.Type == GimJointType.Floating)
                {
                    Vect startPoint = origin[0];
                    Vect endPoint = origin[1];

                    Vect otherPoint = otherJoint.origin[0];
                    double distance1 = (otherPoint - startPoint).Norm;
                    double distance2 = (endPoint - otherPoint).Norm;

                    double length = (endPoint - startPoint).Norm;
                    double totalDistance = distance1 + distance2;


                    double margin = Matrix.EQUALITY_MARGIN;

                    if (totalDistance > length + margin || totalDistance < length - margin) return false;

                    return true;
                }
            }

            if (Type == GimJointType.Floating)
            {
                if (otherJoint.Type == GimJointType.Floating)
                {
                    return origin[0] == otherJoint.origin[0];
                }
                if (otherJoint.Type == GimJointType.Continouous)
                {
                    otherJoint.OverlapsWith(this);
                }
            }

            return false;
        }

        public override string ToString()
        {
            string s = "";

            switch (Type)
            {
                case GimJointType.Continouous:
                    {
                        s = "Vector(" + origin[0] + " ," + origin[1] + ")";
                    }
                    break;
            }

            return s;
        }
    }

    public enum GimJointStatus
    {
        New_LowQuality,                    // detected by the program
        New_HighQuality,
    }
    public enum GimJointType
    {
        Revolute,
        Continouous,
        Prismatic,
        Fixed,
        Floating,
        Planar
    }
    
}
