using GIMcompress;
using System;
using System.Collections.Generic;


namespace GIMtransfer.BrepToPoly
{
	public static partial class BrepToPoly
	{
		// TODO: Implement all curves

		// TODO: (???) Don't return a list of vertices but a functions that takes a parameter and returns the coresponding vertice.
		// In this way we only construct a function in this functions and we can do the calculations only when we need the points.

		// TODO: Do we need the faces for the 2d curves to project them on?

		private static List<Vertex> CurveToPoly(IReadOnlyGimData gimData, int curveIndex, bool skipLast)
		{
			IReadOnlyGimElement element = gimData.Elements[curveIndex];

			return element.ElementType switch
			{
				GimElementType.C_line => CurveLineToPoly(element, skipLast),
				GimElementType.C_line2D => CurveLine2DToPoly(element, skipLast),
				GimElementType.C_bspline => CurveBsplineToPoly(element, skipLast),
				GimElementType.C_bspline2D => CurveBspline2DToPoly(element, skipLast),
				GimElementType.C_hyperbol => CurveHyperbolToPoly(element, skipLast),
				GimElementType.C_hyperbol2D => CurveHyperbol2DToPoly(element, skipLast),
				GimElementType.C_circle => CurveCircleToPoly(gimData, element, curveIndex, skipLast),
				GimElementType.C_circle2D => CurveCircle2DToPoly(element, skipLast),
				GimElementType.C_elipse => CurveElipseToPoly(element, skipLast),
				GimElementType.C_elipse2D => CurveElipse2DToPoly(element, skipLast),
				GimElementType.C_parabol => CurveParabolToPoly(element, skipLast),
				GimElementType.C_parabol2D => CurveParabol2DToPoly(element, skipLast),
				GimElementType.C_trimmed => CurveTrimmedToPoly(gimData, element, curveIndex, skipLast),
				GimElementType.C_trimmed2D => CurveTrimmed2DToPoly(gimData, element, curveIndex, skipLast),
				GimElementType.C_offset => CurveOffsetToPoly(gimData, element, curveIndex, skipLast),
				GimElementType.C_offset2D => CurveOffset2DToPoly(gimData, element, curveIndex, skipLast),
				GimElementType.C_bezier => CurveBezierToPoly(element, skipLast),
				GimElementType.C_bezier2D => CurveBezier2DToPoly(element, skipLast),
				_ => throw new ArgumentException("Curve type is not handled: " + element.ElementType),
			};
		}

		private static List<Vertex> CurveTrimmedToPoly(IReadOnlyGimData gimData, IReadOnlyGimElement trimmedElement, int trimmedIndex, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveTrimmed2DToPoly(IReadOnlyGimData gimData, IReadOnlyGimElement trimmedElement, int trimmedIndex, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveOffsetToPoly(IReadOnlyGimData gimData, IReadOnlyGimElement trimmedElement, int trimmedIndex, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveOffset2DToPoly(IReadOnlyGimData gimData, IReadOnlyGimElement trimmedElement, int trimmedIndex, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveBezierToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveBezier2DToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		// TODO: Generalise this number. See top of BrepToPoly.cs.
		private static readonly int numberOfPointsOnACircle = 14;
		private static List<Vertex> CurveCircleToPoly(IReadOnlyGimData gimData, IReadOnlyGimElement element, int curveIndex, bool skipLast)
		{
			List<List<Vertex>> children = new List<List<Vertex>>(element.Elements.Count);
			foreach (uint child in element.Elements)
			{
				children.Add(CurveToPoly(gimData, (int)child, skipLast));
                //TODO do something with these children
			}

			//element.Locals[0].Px;
			double radius = element.ElementDoubles[0];
			double aParamFirst = element.ElementDoubles[1];
			double aParamLast = element.ElementDoubles[2];

			Vector Position = new Vector(
				element.Locals[0].Px,
				element.Locals[0].Py,
				element.Locals[0].Pz
			);

			Vector vectorX = new Vector(
				element.Locals[0].Xx,
				element.Locals[0].Xy,
				element.Locals[0].Xz
			);

			Vector vectorY = new Vector(
				element.Locals[0].Yx,
				element.Locals[0].Yy,
				element.Locals[0].Yz
			);
			double stepsize;
			if (aParamFirst == aParamLast)
				stepsize = ((aParamLast + Math.PI * 2) - aParamFirst) / numberOfPointsOnACircle;
			else
				stepsize = (aParamLast - aParamFirst) / numberOfPointsOnACircle;

			// C(t) = P + R cos(t) X + R sin(t) Y
			List<Vertex> vertices = new List<Vertex>(numberOfPointsOnACircle + 1);
			for (int i = 0; i < numberOfPointsOnACircle; i++)
			{
				double parameter = aParamFirst + i * stepsize;
				vertices.Add(
					new Vertex(Position + (radius * Math.Cos(parameter) * vectorX) + (radius * Math.Sin(parameter) * vectorY))
					);
			}

			if (!skipLast)
				vertices.Add(
					new Vertex(Position + (radius * Math.Cos(aParamLast) * vectorX) + (radius * Math.Sin(aParamLast) * vectorY))
					);

			return vertices;
		}

		private static List<Vertex> CurveCircle2DToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveElipseToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveElipse2DToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveParabolToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveParabol2DToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveHyperbolToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveHyperbol2DToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			throw new NotImplementedException();
		}

		private static List<Vertex> CurveLine2DToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			return new List<Vertex>(){
				new Vertex(
					element.ElementDoubles[0],
					element.ElementDoubles[1],
					element.ElementDoubles[2]),
				new Vertex(
					 (element.ElementDoubles[3] + element.ElementDoubles[0]) / 2,
					 (element.ElementDoubles[4] + element.ElementDoubles[1]) / 2,
					 (element.ElementDoubles[5] + element.ElementDoubles[2]) / 2),
			new Vertex(
					 element.ElementDoubles[3],
					 element.ElementDoubles[4],
					 element.ElementDoubles[5]),
				};
		}

		private static List<Vertex> CurveLineToPoly(IReadOnlyGimElement element, bool skipLast)
		{
			int pointCount = 4;
			double stepsize = ((element.ElementDoubles[0] + Math.PI * 2) - element.ElementDoubles[1]) / pointCount;
			List<Vertex> vertices = new List<Vertex>(pointCount + 1);

			for (int i = 0; i < pointCount; i++)
			{
				double param = stepsize * i + element.ElementDoubles[0];
				vertices.Add(new Vertex(
					element.Locals[0].Px + element.Locals[0].Zx * param,
					element.Locals[0].Py + element.Locals[0].Zy * param,
					element.Locals[0].Pz + element.Locals[0].Zz * param));
			}
			if (!skipLast)
			{
				vertices.Add(new Vertex(
					element.Locals[0].Px + element.Locals[0].Zx * element.ElementDoubles[1],
					element.Locals[0].Py + element.Locals[0].Zy * element.ElementDoubles[1],
					element.Locals[0].Pz + element.Locals[0].Zz * element.ElementDoubles[1]));
			}
			return vertices;
		}
	}
}
