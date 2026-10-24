using System;
using GimUtilities;

namespace GIMcompress
{
	// TODO: this should probably be a struct.
	public class GimUnitSize
	{
		GimUnit gimUnit;
		GimSize gimSize;

		#region properties
		public GimUnit GimUnit
		{
			get => gimUnit;
			set => gimUnit = value;
		}

		public GimSize GimSize
		{
			get => gimSize;
			set => gimSize = value;
		}
		#endregion

		public GimUnitSize(GimUnit gimUnit, GimSize gimSize)
		{
			this.gimUnit = gimUnit;
			this.gimSize = gimSize;
		}

        public GimUnitSize(byte b)
        {
            if (b >= 107)
            {
                b -= 107;
                gimUnit = GimUnit.ml;
            }
            else if (b >= 86)
            {
                b -= 86;
                gimUnit = GimUnit.ft;
            }
            else
            {
                int multiple = (int)Math.Floor((double)(b / 12));
                gimUnit = (GimUnit)multiple;
                b -= (byte)(12 * multiple);
            }
            if (gimUnit == GimUnit.inch) b += 12;
            else if (gimUnit == GimUnit.ft || gimUnit == GimUnit.ml) b += 26;
            gimSize = (GimSize)b;
        }

        public GimUnitSize(string s)
        {
            string[] subStrings = s.Split(' ');
            if (subStrings.Length != 2) GimLogger.Error("Input string " + s + " is not in the correct format for UnitSize conversion.");
            gimSize = (GimSize)Enum.Parse(GimSize.GetType(), subStrings[0]);
            gimUnit = (GimUnit)Enum.Parse(GimUnit.GetType(), subStrings[1]);
            if (gimSize.IsMetric() != gimUnit.IsMetric() || gimUnit == GimUnit.inch != gimSize.IsInchSize() || (gimUnit == GimUnit.ft || gimUnit == GimUnit.ml) != (!gimSize.IsMetric() && !gimSize.IsInchSize()))
                throw new Exception("string " + s + " is not a valid combination of units and sizes.");
        }

        public GimUnitSize() { }

		public GimUnitSize Clone()
		{
			return new GimUnitSize((GimUnit)((int)GimUnit), (GimSize)((int)GimSize));
		}

		public double GetRoundingStep()
        {
			double value = 0;
            switch (gimUnit)
            {
				case GimUnit.nm:
					value = 0.000001;
					break;
				case GimUnit.µm:
					value = 0.001;
					break;
				case GimUnit.mm:
					value = 1;
					break;
				case GimUnit.m:
					value = 1000;
					break;
				case GimUnit.km:
					value = 1000000;
					break;
				case GimUnit.Mm:
					value = 1000000000;
					break;
                case GimUnit.inch:
                    value += 25.4;
                    break;
                case GimUnit.ft:
                    value += 304.8;
                    break;
                case GimUnit.ml:
                    value += 1609344;
                    break;
                default:
					throw new ArgumentOutOfRangeException("Unkonwn Value");
			}
            switch (gimSize)
            {
				case GimSize.m_0_125:
					value *= 0.125;
					break;
				case GimSize.m_0_25:
					value *= 0.25;
					break;
				case GimSize.m_0_5:
					value *= 0.5;
					break;
				case GimSize.m_1:
					break;
				case GimSize.m_1_25:
					value *= 1.25;
					break;
				case GimSize.m_2_5:
					value *= 2.5;
					break;
				case GimSize.m_5:
					value *= 5;
					break;
				case GimSize.m_10:
					value *= 10;
					break;
				case GimSize.m_12_5:
					value *= 12.5;
					break;
				case GimSize.m_25:
					value *= 25;
					break;
				case GimSize.m_50:
					value *= 50;
					break;
				case GimSize.m_100:
					value *= 100;
					break;
				case GimSize.Id_512:
					value *= 0.001953125;
                    break;
				case GimSize.Id_256:
					value *= 0.00390625;
					break;
				case GimSize.Id_128:
					value *= 0.0078125;
					break;
				case GimSize.Id_64:
					value *= 0.015625;
					break;
				case GimSize.Id_32:
					value *= 0.03125;
					break;
				case GimSize.Id_16:
					value *= 0.0625;
					break;
				case GimSize.Id_8:
					value *= 0.125;
					break;
				case GimSize.Id_4:
					value *= 0.25;
					break;
				case GimSize.Id_2:
					value *= 0.5;
					break;
				case GimSize.I_1:
					value *= 1;
					break;
				case GimSize.I_1_25:
					value *= 1.25;
					break;
				case GimSize.I_2_5:
					value *= 2.5;
					break;
				case GimSize.I_5:
					value *= 5;
					break;
				case GimSize.I_10:
					value *= 10;
					break;
                case GimSize.FM_1: value *= 1; break;
                case GimSize.FM_1_25: value *= 1.25; break;
                case GimSize.FM_2_5: value *= 2.5; break;
                case GimSize.FM_5: value *= 5; break;
                case GimSize.FM_10: value *= 10; break;
                case GimSize.FM_12_5: value *= 12.5; break;
                case GimSize.FM_25: value *= 25; break;
                case GimSize.FM_50: value *= 50; break;
                case GimSize.FM_100: value *= 100; break;
                case GimSize.FM_125: value *= 125; break;
                case GimSize.FM_250: value *= 250; break;
                case GimSize.FM_500: value *= 500; break;
                case GimSize.FM_1000: value *= 1000; break;
                case GimSize.FM_1250: value *= 1250; break;
                case GimSize.FM_2500: value *= 2500; break;
                case GimSize.FM_5000: value *= 5000; break;
                case GimSize.FM_10000: value *= 10000; break;
                case GimSize.FM_12500: value *= 12500; break;
                case GimSize.FM_25000: value *= 25000; break;
                case GimSize.FM_50000: value *= 50000; break;
				case GimSize.FM_100000: value *= 100000; break;


                default:
					throw new ArgumentOutOfRangeException("Unkonwn Value");
			}

			return value;
        }

		public byte ToByte() 
		{
			byte value = 0;
			switch (gimUnit)
			{
				case GimUnit.nm:
					break;
				case GimUnit.µm:
					value += 12;
					break;
				case GimUnit.mm:
					value += 24;
					break;
				case GimUnit.m:
					value += 36;
					break;
				case GimUnit.km:
					value += 48;
					break;
				case GimUnit.Mm:
					value += 60;
					break;
                case GimUnit.inch:
                    value += 72;
                    break;
                case GimUnit.ft:
                    value += 86;
                    break;
                case GimUnit.ml:
                    value += 107;
                    break;
				default:
					throw new ArgumentOutOfRangeException("Unkonwn Value");
			}
			switch (gimSize)
			{
				case GimSize.m_0_125:
                case GimSize.Id_512:
                case GimSize.FM_1:
					break;
				case GimSize.m_0_25:
                case GimSize.Id_256:
                case GimSize.FM_1_25:
					value += 1;
					break;
				case GimSize.m_0_5:
                case GimSize.Id_128:
                case GimSize.FM_2_5:
					value += 2;
					break;
				case GimSize.m_1:
                case GimSize.Id_64:
                case GimSize.FM_5:
					value += 3;
					break;
				case GimSize.m_1_25:
                case GimSize.Id_32:
                case GimSize.FM_10:
					value += 4;
					break;
				case GimSize.m_2_5:
                case GimSize.Id_16:
                case GimSize.FM_12_5:
					value += 5;
					break;
				case GimSize.m_5:
                case GimSize.Id_8:
                case GimSize.FM_25:
					value += 6;
					break;
				case GimSize.m_10:
                case GimSize.Id_4:
                case GimSize.FM_50:
					value += 7;
					break;
				case GimSize.m_12_5:
                case GimSize.Id_2:
                case GimSize.FM_100:
					value += 8;
					break;
				case GimSize.m_25:
                case GimSize.I_1:
                case GimSize.FM_125:
					value += 9;
					break;
				case GimSize.m_50:
                case GimSize.I_1_25:
                case GimSize.FM_250:
					value += 10;
					break;
				case GimSize.m_100:
                case GimSize.I_2_5:
                case GimSize.FM_500:
					value += 11;
					break;
                case GimSize.I_5:
                case GimSize.FM_1000:
                    value += 12;
                    break;
                case GimSize.I_10:
                case GimSize.FM_1250:
                    value += 13;
                    break;
                case GimSize.FM_2500:
                    value += 14;
                    break;
                case GimSize.FM_5000:
                    value += 15;
                    break;
                case GimSize.FM_10000:
                    value += 16;
                    break;
                case GimSize.FM_12500:
                    value += 17;
                    break;
                case GimSize.FM_25000:
                    value += 18;
                    break;
                case GimSize.FM_50000:
                    value += 19;
                    break;
                case GimSize.FM_100000:
                    value += 20;
                    break;
				default:
					throw new ArgumentOutOfRangeException("Unkonwn Value");
			}
			return value;
		}

        public override string ToString()
        {
            return gimSize.ToString() + " " + gimUnit.ToString();
        }

        public override bool Equals(object obj)
        {
			if(obj is GimUnitSize)
            {
				GimUnitSize otherUnitScale = (GimUnitSize)obj;
				return (otherUnitScale.GimUnit == GimUnit && otherUnitScale.GimSize == GimSize);
            }
            return base.Equals(obj);
        }

		public static bool operator==(GimUnitSize A, GimUnitSize B)
        {
			if (A is null && B is null) return true;
			if (A is null || B is null) return false;

			return A.Equals(B); 
        }

		public static bool operator!=(GimUnitSize A, GimUnitSize B)
		{
			if (A is null && B is null) return false;
			if (A is null || B is null) return true;

			return !A.Equals(B);
		}

        public static GimUnitSize FromByte(byte value)
        {
            GimUnit unit;
            GimSize size;

            // Determine GimUnit
            if (value >= 107)
            {
                unit = GimUnit.ml;
                value -= 107;
                size = GetMileSize(value);
            }
            else if (value >= 86)
            {
                unit = GimUnit.ft;
                value -= 86;
                size = GetImperialSize(unit, value);
            }
            else if (value >= 72)
            {
                unit = GimUnit.inch;
                value -= 72;
                size = GetImperialSize(unit, value);
            }
            else if (value >= 60)
            {
                unit = GimUnit.Mm; // Megameters
                value -= 60;
                size = GetMetricSize(unit, value);
            }
            else if (value >= 48)
            {
                unit = GimUnit.km;
                value -= 48;
                size = GetMetricSize(unit, value);
            }
            else if (value >= 36)
            {
                unit = GimUnit.m;
                value -= 36;
                size = GetMetricSize(unit, value);
            }
            else if (value >= 24)
            {
                unit = GimUnit.mm;
                value -= 24;
                size = GetMetricSize(unit, value);
            }
            else if (value >= 12)
            {
                unit = GimUnit.µm;
                value -= 12;
                size = GetMetricSize(unit, value);
            }
            else
            {
                unit = GimUnit.nm;
                size = GetMetricSize(unit, value); // Assuming nanometers also use metric sizes
            }

            return new GimUnitSize(unit, size);
        }
        private static GimSize GetMetricSize(GimUnit unit, byte value)
        {
            // Array of sizes for metric units
            GimSize[] metricSizes = {
        GimSize.m_0_125, GimSize.m_0_25, GimSize.m_0_5, GimSize.m_1, GimSize.m_1_25, GimSize.m_2_5,
        GimSize.m_5, GimSize.m_10, GimSize.m_12_5, GimSize.m_25, GimSize.m_50, GimSize.m_100
    };

            if (value < metricSizes.Length)
                return metricSizes[value];
            else
                throw new ArgumentOutOfRangeException("Unknown Value for Metric Size");
        }

        private static GimSize GetImperialSize(GimUnit unit, byte value)
        {
            // Array of sizes for imperial units
            GimSize[] imperialSizes = unit == GimUnit.inch ?
                new GimSize[] {
            GimSize.Id_512, GimSize.Id_256, GimSize.Id_128, GimSize.Id_64, GimSize.Id_32, GimSize.Id_16,
            GimSize.Id_8, GimSize.Id_4, GimSize.Id_2, GimSize.I_1, GimSize.I_1_25, GimSize.I_2_5, GimSize.I_5, GimSize.I_10
                } :
                new GimSize[] {
            GimSize.FM_1, GimSize.FM_1_25, GimSize.FM_2_5, GimSize.FM_5, GimSize.FM_10, GimSize.FM_12_5,
            GimSize.FM_25, GimSize.FM_50, GimSize.FM_100, GimSize.FM_125, GimSize.FM_250, GimSize.FM_500,
            GimSize.FM_1000, GimSize.FM_1250, GimSize.FM_2500, GimSize.FM_5000, GimSize.FM_10000,
            GimSize.FM_12500, GimSize.FM_25000, GimSize.FM_50000, GimSize.FM_100000
                };

            if (value < imperialSizes.Length)
                return imperialSizes[value];
            else
                throw new ArgumentOutOfRangeException("Unknown Value for Imperial Size");
        }

        private static GimSize GetMileSize(byte value)
        {
            // Array of sizes for mile units
            GimSize[] mileSizes = {
            GimSize.FM_1, GimSize.FM_1_25, GimSize.FM_2_5, GimSize.FM_5, GimSize.FM_10, GimSize.FM_12_5,
            GimSize.FM_25, GimSize.FM_50, GimSize.FM_100, GimSize.FM_125, GimSize.FM_250, GimSize.FM_500,
            GimSize.FM_1000, GimSize.FM_1250, GimSize.FM_2500, GimSize.FM_5000, GimSize.FM_10000,
            GimSize.FM_12500, GimSize.FM_25000, GimSize.FM_50000, GimSize.FM_100000
        };

            if (value < mileSizes.Length)
                return mileSizes[value];
            else
                throw new ArgumentOutOfRangeException("Unknown Value for Mile Size");
        }



    }

    //public enum GimUnitSize
    //{
    //    nm_0_125, nm_0_25, nm_0_5, nm_1, nm_1_25, nm_2_5, nm_5, nm_10, nm_12_5, nm_25, nm_50, nm_100,
    //    µm_0_125, µm_0_25, µm_0_5, µm_1, µm_1_25, µm_2_5, µm_5, µm_10, µm_12_5, µm_25, µm_50, µm_100,
    //    mm_0_125, mm_0_25, mm_0_5, mm_1, mm_1_25, mm_2_5, mm_5, mm_10, mm_12_5, mm_25, mm_50, mm_100,
    //    m_0_125, m_0_25, m_0_5, m_1, m_1_25, m_2_5, m_5, m_10, m_12_5, m_25, m_50, m_100,
    //    km_0_125, km_0_25, km_0_5, km_1, km_1_25, km_2_5, km_5, km_10, km_12_5, km_25, km_50, km_100,
    //    Mm_0_125, Mm_0_25, Mm_0_5, Mm_1, Mm_1_25, Mm_2_5, Mm_5, Mm_10, Mm_12_5, Mm_25, Mm_50, Mm_100, (megameters)
    //    in_d_512, in_d_256, in_d_128, in_d_64, in_d_32, in_d_16, in_d_8, in_d_4, in_d_2, in_1, in_1_25, in_2_5, in_5, in_10,
    //    ft_1, ft_1_25, ft_2_5, ft_5, ft_10, ft_12_5, ft_25, ft_50, ft_100, ft_125, ft_250, ft_500,
    //    ft_1000, ft_1250, ft_2500, ft_5000, ft_10000, ft_12500, ft_25000, ft_50000, ft_100000,
    //    ml_1, ml_1_25, ml_2_5, ml_5, ml_10, ml_12_5, ml_25, ml_50, ml_100, ml_125, ml_250, ml_500,(miles)
    //    ml_1000, ml_1250, ml_2500, ml_5000, ml_10000, ml_12500, ml_25000, ml_50000, ml_100000 (miles)
    //}

    public enum GimUnit { nm, µm, mm, m, km, Mm, inch, ft, ml }

	public enum GimSize
	{
		m_0_125, m_0_25, m_0_5, m_1, m_1_25, m_2_5, m_5, m_10, m_12_5, m_25, m_50, m_100,
		Id_512, Id_256, Id_128, Id_64, Id_32, Id_16, Id_8, Id_4, Id_2, I_1, I_1_25, I_2_5, I_5, I_10,
		FM_1, FM_1_25, FM_2_5, FM_5, FM_10, FM_12_5, FM_25, FM_50, FM_100, FM_125, FM_250, FM_500, FM_1000, FM_1250, FM_2500, FM_5000, FM_10000, FM_12500, FM_25000, FM_50000, FM_100000
	}

}
