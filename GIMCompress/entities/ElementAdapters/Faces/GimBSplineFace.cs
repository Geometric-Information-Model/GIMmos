using GimUtilities;
using System;

namespace GIMcompress.ElementHelpers
{
    /// <summary>
    /// Serves as a wrapper to facilitiate and standarize BSpline processing.
    /// </summary>
    public class GimBSplineFace : AbstractElementAdapter
    {
        /// <inheritdoc/>
        protected override GimElementType ExpectedType => GimElementType.F_bspline;

        #region single properties
        public int PoleCount
        {
            get { return element.elementInts[0]; }
        }
        public int UDegree
        {
            get { return element.elementInts[1]; }
        }
        public int VDegree
        {
            get { return element.elementInts[2]; }
        }
        public int UKnotCount
        {
            get { return element.elementInts[3]; }
        }
        public int VKnotCount
        {
            get { return KnotCount - UKnotCount; }
        }
        public int UPoleCount
        {
            get { return element.elementInts[4]; }
        }
        public int VPoleCount
        {
            get { return PoleCount / UPoleCount; }
        }
        public int KnotCount
        {
            get { return element.elementInts[5]; }
        }

        public bool Periodic
        {
            get; private set;
        }

        public bool UPeriodic
        {
            get; private set;
        }

        public bool VPeriodic
        {
            get; private set;
        }

        #endregion

        #region double ranges

        public readonly ArraySegment<double> PointCoords;
        public readonly ArraySegment<double> Weights;
        public readonly ArraySegment<double> UKnots;
        public readonly ArraySegment<double> VKnots;
        #endregion

        #region integer ranges
        public readonly ArraySegment<int> UMultiplicities;
        public readonly ArraySegment<int> VMultiplicities;
        #endregion
        public GimBSplineFace(GimElement bsplineFaceElement) : base(bsplineFaceElement)
        {
            // Double ranges
            int pointsEnd = 3 * PoleCount;
            PointCoords = new(element.ElementDoubles, 0, pointsEnd);
            int weightsEnd = pointsEnd + PoleCount;
            Weights = new(element.ElementDoubles, pointsEnd, weightsEnd);

            // Int ranges
            int multiplicityEnd = KnotCount + 6;

            int nonperiodicSum = UPoleCount + UDegree + 1;
            int periodicSum = UPoleCount + element.ElementInts[6];
            Periodic = (periodicSum != nonperiodicSum);

            ArraySegment<int> multiplicities = new(element.ElementInts, 6, multiplicityEnd);
            int sum = Periodic ? periodicSum : nonperiodicSum;

            int uMultiplicitiesCount = 0;

            for (int j = 1; j < multiplicities.Count; j++)
            {
                int MultiSum = 0;
                for (int k = 0; k < j; k++)
                {
                    MultiSum += multiplicities[k];
                }
                if (MultiSum == sum)
                {
                    uMultiplicitiesCount = j; break;
                }
            }

            int uMultiplicitiesEnd = 6 + uMultiplicitiesCount;
            UMultiplicities = new(element.ElementInts, 6, uMultiplicitiesEnd);
            VMultiplicities = new(element.ElementInts, uMultiplicitiesEnd, multiplicityEnd);

            int uKnotEnd = weightsEnd + UKnotCount;
            UKnots = new(element.ElementDoubles, weightsEnd, uKnotEnd);
            int vKnotEnd = uKnotEnd + VKnotCount;
            VKnots = new(element.ElementDoubles, uKnotEnd, vKnotEnd);

            int uMultSum = 0;
            for (int i = 0; i < UMultiplicities.Count; i++)
                uMultSum += UMultiplicities[i];

            int uAperiodicSum = uMultSum - UDegree - 1;
            int uPeriodicSum = uMultSum - UMultiplicities[0];

            bool uAperiodic = (uAperiodicSum == UPoleCount);
            if (uAperiodic)
                UPeriodic = false;
            else
                UPeriodic = (uPeriodicSum == UPoleCount);

            if (UPeriodic == uAperiodic)
                throw GimLogger.Error(new InvalidGimElementException("Bspline surface is neither periodic nor aperiodic!"));

            int vMultSum = 0;
            for (int i = 0; i < VMultiplicities.Count; i++)
                vMultSum += VMultiplicities[i];
            int vAperiodicSum = vMultSum - VDegree - 1;
            int vPeriodicSum = vMultSum - VMultiplicities[0];

            bool vAperiodic = (vAperiodicSum == VPoleCount);
            if (vAperiodic)
                VPeriodic = false;
            else
                VPeriodic = (vPeriodicSum == VPoleCount);

            if (VPeriodic == vAperiodic)
                throw GimLogger.Error(new InvalidGimElementException("Bspline surface is neither periodic nor aperiodic!"));

        }
    }
}
