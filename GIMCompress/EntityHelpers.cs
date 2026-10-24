using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestPlatform")]
namespace GIMcompress
{
	/// <summary>
	/// This static class holds all static helper function from GIMCompress.
	/// </summary>
	public static class Helpers
	{
		public static int GetIntCount(this GimElementType type)
		{
			switch (type)
			{
				case GimElementType.Material:
					return 5;

                case GimElementType.ExternalReference:
					return 2;
                case GimElementType.Texture:
				case GimElementType.P_polyline:
				case GimElementType.P_polygon:
                    return -1;
				default:
					if (type.IsBezierType())
					{
						return -1;
					}
					if(type == GimElementType.Gim_Joint)
                    {
						return 3;
                    }
					return 0;
			}
		}

		/// <summary>
		/// This function returns the amount of doubles for the type this is called on. 
		/// If the function returns -1 this means the amount of doubles is variable.
		/// </summary>
		/// <param name="type"> The type to return the double count for.</param>
		/// <returns>The amount of doubles the type should have. If the function returns -1 this means the amount of doubles is variable.</returns>
		public static int GetDoubleCount(this GimElementType type)
		{

			switch (type)
			{
				case GimElementType.F_cone:
					return 2;
				case GimElementType.F_cilinder:
					return 1;
				case GimElementType.F_sphere:
					return 1;
				case GimElementType.F_toroid:
					return 2;
				case GimElementType.F_trimmed:
					return 4;
				case GimElementType.C_circle:
					return 3;
				case GimElementType.C_elipse:
					return 4;
				case GimElementType.C_hyperbol:
					return 4;
				case GimElementType.C_line:
					return 2;
				case GimElementType.C_parabol:
					return 3;
				case GimElementType.C_circle2D:
					return 3;
				case GimElementType.C_elipse2D:
					return 4;
				case GimElementType.C_hyperbol2D:
					return 4;
				case GimElementType.C_line2D:
					return 2;
				case GimElementType.C_parabol2D:
					return 3;
				case GimElementType.C_offset2D:
					return 1;
				case GimElementType.C_trimmed2D:
					return 2;
				case GimElementType.C_trimmed:
					return 2;
				case GimElementType.F_offset:
				case GimElementType.Material:
					return 1;
				case GimElementType.B_sheet:
				case GimElementType.B_solid:
				case GimElementType.shell:
				case GimElementType.undefined:
				case GimElementType.F_plane:
				case GimElementType.F_revolution:
				case GimElementType.F_extrusion:
				case GimElementType.wire:
				case GimElementType.Seam:
				case GimElementType.ExternalReference:
					return 0;
				default:
					if (type.IsBezierType() || type == GimElementType.P_polygon || type == GimElementType.P_polyline)
					{
						return -1;
					}

					if (type.IsTexture())
					{
						return 5;
					}

					if (type.IsDocument() || type.IsRepresentation())
					{
						return 0;
					}

					if(type == GimElementType.Gim_Joint)
                    {
						return 17;
                    }
#if DEBUG
					throw new NotImplementedException(type.ToString());
#else
						return 0;
#endif
			}
		}

		/// <summary>
		/// Returns the amount of bytes a certain GimElementType should have.
		/// Bytes are only used for GimElements containing files.
		/// </summary>
		/// <param name="type">The type to get the amount of bytes for.</param>
		/// <returns>Returns -1 (variable amount) if the Type is a texture (contains a file) and 0 otherwise. </returns>
		public static int GetByteCount(this GimElementType type)
		{
			if (type.IsTexture())
				return -1;
			return 0;
		}

		/// <summary>
		/// Checks if the GimElementType is a document.
		/// </summary>
		/// <param name="type"></param>
		/// <returns>Returns true if the GimElementType is a document, otherwise it returns false.</returns>
		public static bool IsDocument(this GimElementType type)
		{
			return type == GimElementType.D_root /*|| type == GimElementType.D_link*/ || type == GimElementType.D_folder || type == GimElementType.D_part;
		}

		/// <summary>
		/// Checks if the GimElementType is a 3D type i.e. some face, poly or curve.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool Is3D(this GimElementType type)
		{
			return !type.IsDocument() && !type.IsRepresentation() && !type.IsBody() && !type.IsAppearance();
		}

		/// <summary>
		/// Checks if the GimElementType is a body.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool IsBody(this GimElementType type)
		{
			return type == GimElementType.B_sheet || type == GimElementType.B_solid;
		}

		/// <summary>
		/// Checks if the GimElementType is a representation. (Either a basis-representation or a polygon representation)
		/// </summary>
		/// <param name="type"></param>
		/// <returns>Returns true if the GimElementTYpe is a representation, otherwise it returns false.</returns>
		public static bool IsRepresentation(this GimElementType type)
		{
			return type == GimElementType.R_BRep || type == GimElementType.R_Poly;
		}

		/// <summary>
		/// Checks if the GimElementType is a seam marker (topological metadata child of a 3D curve).
		/// </summary>
		public static bool IsSeam(this GimElementType type)
		{
			return type == GimElementType.Seam;
		}

		/// <summary>
		/// Checks if the GimElementType is a 2 dimensional curve.
		/// </summary>
		/// <param name="type"></param>
		/// <returns>Returns true if the GimElementType is a 2 dimensional curve, false otherwise.</returns>
		public static bool Is2DType(this GimElementType type)
		{
			return type == GimElementType.C_bezier2D || type == GimElementType.C_bspline2D 
				|| type == GimElementType.C_circle2D || type == GimElementType.C_elipse2D 
				|| type == GimElementType.C_hyperbol2D || type == GimElementType.C_line2D 
				|| type == GimElementType.C_offset2D || type == GimElementType.C_parabol2D 
				|| type == GimElementType.C_trimmed2D;
		}

		/// <summary>
		/// Checks if the GimElementType is a face.
		/// </summary>
		/// <param name="type"></param>
		/// <returns>Returns true if the GimElementType is a face, otherwise false.</returns>
		public static bool IsFace(this GimElementType type)
		{
			return type == GimElementType.F_bezier || type == GimElementType.F_bspline 
				|| type == GimElementType.F_cilinder || type == GimElementType.F_cone 
				|| type == GimElementType.F_extrusion || type == GimElementType.F_offset 
				|| type == GimElementType.F_revolution || type == GimElementType.F_sphere 
				|| type == GimElementType.F_toroid || type == GimElementType.F_trimmed;
		}

		/// <summary>
		/// Checks if the GimElementType is a Bezier type or b-spline.
		/// </summary>
		/// <param name="type"></param>
		/// <returns>returns true if the GimElementType is a Bezier face/curve type or b-spline face/curve, otherwise true.</returns>
		public static bool IsBezierType(this GimElementType type)
		{
			return type == GimElementType.F_bezier || type == GimElementType.F_bspline 
				|| type == GimElementType.C_bezier || type == GimElementType.C_bezier2D 
				|| type == GimElementType.C_bspline || type == GimElementType.C_bspline2D;
		}

		/// <summary>
		/// Checks if the GimElementType is a polygon type.
		/// </summary>
		/// <param name="type"></param>
		/// <returns>Returns true if the GimElementType is a polygon representation or a type that the polygon representation can contain.</returns>
		public static bool IsPoly(this GimElementType type)
		{
			return type == GimElementType.R_Poly || type == GimElementType.P_polygon
				|| type == GimElementType.P_polyline || type == GimElementType.P_vertex;
		}

		/// <summary>
		/// Checks if the GimElementType is a boundary-representation. (I.e. a b-representation or a element the b-representation can contain.)
		/// <c>!type.IsPoly() && !type.IsDocument();</c>
		/// </summary>
		/// <param name="type"></param>
		/// <returns>Returns false if the GimElementType is a polygon type or a document type. Otherwise false.</returns>
		public static bool IsBrep(this GimElementType type)
		{
			return !type.IsPoly() && !type.IsDocument() && !type.IsAppearance();
		}

		/// <summary>
		/// Checks if the GimElementType is a curve.
		/// </summary>
		/// <param name="type"></param>
		/// <returns>Returns true if the GimElementType is a curve, otherwise false.</returns>
		public static bool IsCurve(this GimElementType type)
		{
			return type == GimElementType.C_bezier || type == GimElementType.C_bezier2D 
				|| type == GimElementType.C_bspline || type == GimElementType.C_bspline2D 
				|| type == GimElementType.C_circle || type == GimElementType.C_circle2D 
				|| type == GimElementType.C_elipse || type == GimElementType.C_elipse2D 
				|| type == GimElementType.C_hyperbol || type == GimElementType.C_hyperbol2D 
				|| type == GimElementType.C_line || type == GimElementType.C_line2D 
				|| type == GimElementType.C_offset || type == GimElementType.C_offset2D 
				|| type == GimElementType.C_parabol || type == GimElementType.C_parabol2D 
				|| type == GimElementType.C_trimmed || type == GimElementType.C_trimmed2D;
		}

		/// <summary>
		/// Checks if the GimElementType is a material or a Texture.
		/// </summary>
		/// <param name="type">The GimElementType to check.</param>
		/// <returns>Returns true if the GimElementType is a material or a Texture, otherwise false.</returns>
		public static bool IsAppearance(this GimElementType type)
		{
			return type.IsMaterial() || type.IsTexture();
		}

		/// <summary>
		/// Checks if the GimElementType is a material.
		/// </summary>
		/// <param name="type">The GimElementType to check.</param>
		/// <returns>Returns true if the GimElementType is a material, otherwise false.</returns>
		public static bool IsMaterial(this GimElementType type)
		{
			return type == GimElementType.Material;
		}

		/// <summary>
		/// Checks if the GimElementType is a Texture.
		/// </summary>
		/// <param name="type">The GimElementType to check.</param>
		/// <returns>Returns true if the GimElementType a Texture, otherwise false.</returns>
		public static bool IsTexture(this GimElementType type)
		{
			return type == GimElementType.Texture;
		}

		/// <summary>
		/// Calculates a hashcode for a list.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sequence">The generic collection to calculate a hashcode for.</param>
		/// <returns>The hashcode of the list.</returns>
		internal static int GetSequenceHashCode<T>(this IList<T> sequence)
		{
			const int seed = 487;
			const int modifier = 31;

			unchecked
			{
				return sequence.Aggregate(seed, (current, item) =>
					(current * modifier) + item.GetHashCode());
			}
		}

		public static double UnitToDouble(this GimUnit unit)
		{
			return unit switch
			{
				GimUnit.nm => 0.000001,
				GimUnit.µm => 0.001,
				GimUnit.mm => 1,
				GimUnit.m => 1000,
				GimUnit.km => 1000000,
				GimUnit.Mm => 1000000000,
				GimUnit.inch => 25.4,
				GimUnit.ft => 304.8,
				GimUnit.ml => 1609344,
				_ => throw new ArgumentOutOfRangeException(nameof(unit)),
			};
		}

		public static double SizeToDouble(this GimSize size)
		{
			return size switch
			{
				GimSize.m_0_125 => 0.125,
				GimSize.m_0_25 => 0.25,
				GimSize.m_0_5 => 0.5,
				GimSize.m_1 => 1,
				GimSize.m_1_25 => 1.25,
				GimSize.m_2_5 => 2.5,
				GimSize.m_5 => 5,
				GimSize.m_10 => 10,
				GimSize.m_12_5 => 12.5,
				GimSize.m_25 => 25,
				GimSize.m_50 => 50,
				GimSize.m_100 => 100,
				GimSize.Id_512 => 0.001953125,
				GimSize.Id_256 => 0.00390625,
				GimSize.Id_128 => 0.0078125,
				GimSize.Id_64 => 0.015625,
				GimSize.Id_32 => 0.03125,
				GimSize.Id_16 => 0.0625,
				GimSize.Id_8 => 0.125,
				GimSize.Id_4 => 0.25,
				GimSize.Id_2 => 0.5,
				GimSize.I_1 => 1,
				GimSize.I_1_25 => 1.25,
				GimSize.I_2_5 => 2.5,
				GimSize.I_5 => 5,
				GimSize.I_10 => 10,
				GimSize.FM_1 => 1,
				GimSize.FM_1_25 => 1.25,
				GimSize.FM_2_5 => 2.5,
				GimSize.FM_5 => 5,
				GimSize.FM_10 => 10,
				GimSize.FM_12_5 => 12.5,
				GimSize.FM_25 => 25,
				GimSize.FM_50 => 50,
				GimSize.FM_100 => 100,
				GimSize.FM_125 => 125,
				GimSize.FM_250 => 250,
				GimSize.FM_500 => 500,
				GimSize.FM_1000 => 1000,
				GimSize.FM_1250 => 1250,
				GimSize.FM_2500 => 2500,
				GimSize.FM_5000 => 5000,
				GimSize.FM_10000 => 10000,
				GimSize.FM_12500 => 12500,
				GimSize.FM_25000 => 25000,
				GimSize.FM_50000 => 50000,
				GimSize.FM_100000 => 100000,
				_ => throw new ArgumentOutOfRangeException(nameof(size)),
			};
		}

		public static bool IsMetric(this GimUnit unit)
		{
			return unit == GimUnit.nm || unit == GimUnit.µm || unit == GimUnit.mm || unit == GimUnit.m || unit == GimUnit.km || unit == GimUnit.Mm;
		}

		public static bool IsInchSize(this GimSize size)
		{
			return size == GimSize.Id_512 || size == GimSize.Id_256 || size == GimSize.Id_128 || size == GimSize.Id_64 || size == GimSize.Id_32 || size == GimSize.Id_16 || size == GimSize.Id_8 || size == GimSize.Id_4 || size == GimSize.Id_2 || size == GimSize.I_1 || size == GimSize.I_1_25 || size == GimSize.I_2_5 || size == GimSize.I_5 || size == GimSize.I_10;
		}

		public static bool IsMetric(this GimSize size)
		{
			return size == GimSize.m_0_125 || size == GimSize.m_0_25 || size == GimSize.m_0_5 || size == GimSize.m_1 || size == GimSize.m_1_25 || size == GimSize.m_2_5 || size == GimSize.m_5 || size == GimSize.m_10 || size == GimSize.m_12_5 || size == GimSize.m_25 || size == GimSize.m_50 || size == GimSize.m_100;
		}
	}
}
