namespace GIMcompress
{
    public class GimLocal : IReadOnlyGimLocal
    {
        public uint parent;

        public double px, py, pz;

        // xx, xy, xz
        // yx, yy, yz
        // zx, zy, zz

        public double xx;
        public double xy;
        public double xz;
        public double yx;
        public double yy;
        public double yz;
        public double zx;
        public double zy;
        public double zz;
        public bool rightHanded = true;

        #region properties

        public uint Parent
        {
            get => parent;
            set => parent = value;
        }

        public double Px
        {
            get => px;
            set => px = value;
        }

        public double Py
        {
            get => py;
            set => py = value;
        }

        public double Pz
        {
            get => pz;
            set => pz = value;
        }

        public Vect BasisPoint
        {
            get => new Vect(Px, Py, Pz);
        }

        public double Xx
        {
            get => xx;
            set => xx = value;
        }

        public double Xy
        {
            get => xy;
            set => xy = value;
        }

        public double Xz
        {
            get => xz;
            set => xz = value;
        }

        public double Yx
        {
            get => yx;
            set => yx = value;
        }

        public double Yy
        {
            get => yy;
            set => yy = value;
        }

        public double Yz
        {
            get => yz;
            set => yz = value;
        }

        public double Zx
        {
            get => zx;
            set => zx = value;
        }

        public double Zy
        {
            get => zy;
            set => zy = value;
        }

        public double Zz
        {
            get => zz;
            set => zz = value;
        }

        public bool RightHanded
        {
            get => rightHanded;
            set => rightHanded = value;
        }
        #endregion

        public GimLocal()
        {
            xx = 1.0f;
            yy = 1.0f;
            zz = 1.0f;
            rightHanded = true;
        }

        public GimLocal(double px, double py, double pz, double xx, double xy, double xz, double yx, double yy, double yz, double zx, double zy, double zz, bool rightHanded = true)
        {
            this.px = px;
            this.py = py;
            this.pz = pz;
            this.xx = xx;
            this.xy = xy;
            this.xz = xz;
            this.yx = yx;
            this.yy = yy;
            this.yz = yz;
            this.zx = zx;
            this.zy = zy;
            this.zz = zz;

            this.rightHanded = rightHanded;
        }

        public GimLocal(double px, double py, double pz, Matrix m, bool rightHanded = true)
        {
            this.px = px;
            this.py = py;
            this.pz = pz;
            m.ApplyToLocal(this);

            this.rightHanded = rightHanded;
        }
        /// <summary>
        /// Checks if everything is equal except for the parent
        /// </summary>
        /// <param name="l">the other local to check.</param>
        /// <returns>true if the representation of the local is equal, otherwise false</returns>
        public bool Compare(IReadOnlyGimLocal l)
        {
            if (l == null)
                return false;
            return
                VectorHelpers.CompareLocalDouble(px, l.Px) &&
                VectorHelpers.CompareLocalDouble(py, l.Py) &&
                VectorHelpers.CompareLocalDouble(pz, l.Pz) &&

                VectorHelpers.CompareLocalDouble(xx, l.Xx) &&
                VectorHelpers.CompareLocalDouble(xy, l.Xy) &&
                VectorHelpers.CompareLocalDouble(xz, l.Xz) &&

                VectorHelpers.CompareLocalDouble(yx, l.Yx) &&
                VectorHelpers.CompareLocalDouble(yy, l.Yy) &&
                VectorHelpers.CompareLocalDouble(yz, l.Yz) &&

                // Is not correct? What if the z-axis is int.MinValue() == null?
                ((VectorHelpers.CompareLocalDouble(zx, l.Zx) &&
                VectorHelpers.CompareLocalDouble(zy, l.Zy) &&
                VectorHelpers.CompareLocalDouble(zz, l.Zz)) || rightHanded == l.RightHanded);
        }

        public bool DirectionOnly()
        {
            return xx == int.MinValue && yx == int.MinValue && xx == xy && xy == xz && yx == yy && yy == yz;
        }

        public bool ZAxisIsNull()
        {
            return /*zx == int.MinValue &&*/ zy == zx && zz == zy;

        }
        internal GimLocal Clone()
        {
            GimLocal clone = new GimLocal
            {
                parent = parent,
                px = px,
                py = py,
                pz = pz,
                xx = xx,
                xy = xy,
                xz = xz,
                yx = yx,
                yy = yy,
                yz = yz,
                zx = zx,
                zy = zy,
                zz = zz,
                rightHanded = rightHanded

            };
            return clone;
        }

        public void RoundMatrix()
        {
            xx = ValueRounder.roundDouble(xx, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            xy = ValueRounder.roundDouble(xy, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            xz = ValueRounder.roundDouble(xz, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

            yx = ValueRounder.roundDouble(yx, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            yy = ValueRounder.roundDouble(yy, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            yz = ValueRounder.roundDouble(yz, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);

            zx = ValueRounder.roundDouble(zx, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            zy = ValueRounder.roundDouble(zy, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
            zz = ValueRounder.roundDouble(zz, Matrix.EQUALITY_ROUND_MARGIN, Matrix.EQUALITY_ROUND_PRECISION);
        }
    }
}
