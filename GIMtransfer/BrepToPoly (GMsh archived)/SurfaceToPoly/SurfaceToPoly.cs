//using GIMcompress;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using GimUtilities;


//namespace GIMtransfer.BrepToPoly
//{
//	public static partial class BrepToPoly
//	{
//		// TODO: Implement all surfaces

//		// TODO: should get entire gimdata to handel the trimmed and offset cases.

//		private static Polygon SurfaceToPoly(IReadOnlyGimData gimData, int faceIndex, List<Wire> wires)
//		{
//			string wiresAsString = Wire.GenerateStringRepresentation(wires);
//			Console.WriteLine(wiresAsString);

//			IReadOnlyGimElement readOnlyGimElement = gimData.Elements[faceIndex];
//			// Creates polygon faces from a surface with the wire as boundary.
//			switch (readOnlyGimElement.ElementType)
//			{
//				case GimElementType.F_plane:
//					return SurfacePlaneToPoly(readOnlyGimElement, wires);
//				case GimElementType.F_sphere:
//					return SurfaceSphereToPoly(readOnlyGimElement, wires);
//				case GimElementType.F_cone:
//				case GimElementType.F_cilinder:
//				case GimElementType.F_toroid:
//				case GimElementType.F_extrusion:
//				case GimElementType.F_revolution:
//				case GimElementType.F_bezier:
//				case GimElementType.F_bspline:
//				case GimElementType.F_offset:
//				case GimElementType.F_trimmed:
//					throw new NotImplementedException(nameof(readOnlyGimElement.ElementType) + " is not implmented. " + readOnlyGimElement.ElementType.ToString());
//				default:
//					throw GimLogger.Error("Face type not handled: " + readOnlyGimElement.ElementType);
//			}
//		}

//		private static Polygon SurfacePlaneToPoly(IReadOnlyGimElement readOnlyGimElement, List<Wire> wires)
//		{
//			if (wires.Count == 1)
//			{
//				Wire wire = wires[0];
//				Polygon polygon = new Polygon();
//				polygon.faces = new List<Face>();
//				// TODO: Assert for a Plane, the wire holds the plane.
//				polygon.vertices = wire.vertices;
//				int verticesCount = polygon.vertices.Count;
//				for (int i = 0; i < verticesCount; i += 2)
//				{
//					polygon.faces.Add(new Face(wire.vertices[i % verticesCount], wire.vertices[(i + 1) % verticesCount], wire.vertices[(i + 2) % verticesCount]));
//				}
//				return polygon;
//			}
//			throw new NotImplementedException("Wire count: " + wires.Count);
//		}

//		private static Polygon SurfaceSphereToPoly(IReadOnlyGimElement readOnlyGimElement, List<Wire> wires)
//		{
//			// S(u, v) = P + R cos(v)(cos(u) D_x + sin(u) D_y) + R sin(v) * D_z

//			// TODO: Find out what the correct local is.
//			Vector position = new Vector(readOnlyGimElement.Locals[0].Px, readOnlyGimElement.Locals[0].Py, readOnlyGimElement.Locals[0].Pz);
//			Vector xdirection = new Vector(readOnlyGimElement.Locals[0].Xx, readOnlyGimElement.Locals[0].Xy, readOnlyGimElement.Locals[0].Xz);
//			Vector ydirection = new Vector(readOnlyGimElement.Locals[0].Yx, readOnlyGimElement.Locals[0].Yy, readOnlyGimElement.Locals[0].Yz);
//			Vector zdirection = new Vector(readOnlyGimElement.Locals[0].Zx, readOnlyGimElement.Locals[0].Zy, readOnlyGimElement.Locals[0].Zz);
//			double radius = readOnlyGimElement.ElementDoubles[0];

//			List<Tuple<double, double>> theParameters = new List<Tuple<double, double>>(wires.Sum((wire) => wire.vertices.Count));

//			foreach (Wire wire in wires)
//				foreach (Vertex vertex in wire.vertices)
//					theParameters.Add(SphereInternalFunctionInverse(vertex.ToVector(), position, radius));

//			throw new NotImplementedException();
//		}

//		private static Tuple<double, double> SphereInternalFunctionInverse(Vector resultPoint, Vector position, double radius/*, Vector xDirection, Vector yDirection, Vector zDirection*/)
//		{
//			Vector point = resultPoint - position;
//			double u, v;
//			u = Math.Atan2(Math.Sqrt(Math.Pow(resultPoint.X, 2) + Math.Pow(resultPoint.Y, 2)), resultPoint.Z);
//			if (resultPoint.X > 0)
//				v = Math.Atan2(point.Y, point.X);
//			else if (point.X < 0 && point.Y >= 0)
//				v = Math.Atan2(point.Y, point.X) + Math.PI;
//			else if (point.X < 0 && point.Y < 0)
//				v = Math.Atan2(point.Y, point.X) - Math.PI;
//			else if (point.X == 0 && point.Y > 0)
//				v = Math.PI / 2;
//			else if (point.X == 0 && point.Y < 0)
//				v = -Math.PI / 2;
//			else
//				v = double.NaN;

//			return new Tuple<double, double>(u, v);
//		}

//		private static Vector SphereInternalFunction(double u, double v, Vector position, double radius, Vector xDirection, Vector yDirection, Vector zDirection)
//		{
//			return position + radius * Math.Cos(v) * (Math.Cos(u) * xDirection + Math.Sin(u) * yDirection) + radius * Math.Sin(v) * zDirection;
//		}
//	}
//}
