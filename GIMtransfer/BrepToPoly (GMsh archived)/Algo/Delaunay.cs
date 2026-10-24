using System;
using System.Collections.Generic;
using System.Linq;
using GimUtilities;

namespace GIMtransfer.BrepToPoly
{
	internal class Sphere
	{
		internal Vector a;
		internal Vector b;
		internal Vector c;
		internal Vector d;

		internal Sphere(Vector a, Vector b, Vector c, Vector d)
		{
			this.a = a;
			this.b = b;
			this.c = c;
			this.d = d;
		}

		internal Sphere(List<Vector> vectors)
		{
			if (vectors.Count != 4)
				throw new ArgumentOutOfRangeException(nameof(vectors) + " is either to long or to short.");

			a = vectors[0];
			b = vectors[1];
			c = vectors[2];
			d = vectors[3];
		}

		internal List<Vector> FaceA { get => new List<Vector>() { a, b, c }; }
		internal List<Vector> FaceB { get => new List<Vector>() { b, c, d }; }
		internal List<Vector> FaceC { get => new List<Vector>() { a, c, d }; }
		internal List<Vector> FaceD { get => new List<Vector>() { a, b, d }; }
	}

	public static class Delaunay
	{

		// Modification from https://github.com/nol1fe/delaunator-sharp
		public static Tuple<int[], int[]> Delaunator(List<Vector> points)
		{
			if (points.Count <= 4)
			{
				throw new ArgumentOutOfRangeException("Need at least 4" + nameof(points));
			}

			int maxTriangles = 2 * points.Count - 5;

			double minX = double.PositiveInfinity;
			double minY = double.PositiveInfinity;
			double minZ = double.PositiveInfinity;
			double maxX = double.NegativeInfinity;
			double maxY = double.NegativeInfinity;
			double maxZ = double.NegativeInfinity;

			List<Tuple<int, double>> idsAndDistance = new List<Tuple<int, double>>(points.Count);

			for (var i = 0; i < points.Count; i++)
			{
				double x = points[i].X;
				double y = points[i].Y;
				double z = points[i].Z;
				if (x < minX) minX = x;
				if (y < minY) minY = y;
				if (z < minZ) minZ = z;
				if (x > maxX) maxX = x;
				if (y > maxY) maxY = y;
				if (z > maxX) maxX = z;
				idsAndDistance.Add(new Tuple<int, double>(i, double.NegativeInfinity));
			}

			double cx = (minX + maxX) / 2;
			double cy = (minY + maxY) / 2;
			double cz = (minZ + maxZ) / 2;

			double minDistatanceSquared = double.PositiveInfinity;
			int id0 = 0;
			int id1 = 0;
			int id2 = 0;
			int id3 = 0;

			// pick a seed point close to the center
			for (int i = 0; i < points.Count; i++)
			{
				double distanceSquared = DistanceSquared(new Vector(cx, cy, cz), points[i]);
				if (distanceSquared < minDistatanceSquared)
				{
					id0 = i;
					minDistatanceSquared = distanceSquared;
				}
			}

			// find the two points closest to the seed
			minDistatanceSquared = double.PositiveInfinity;
			double minDistatanceSquared2 = double.PositiveInfinity;

			for (int i = 0; i < points.Count; i++)
			{
				// Skip self.
				if (i == id0)
					continue;

				double distanceSquared = DistanceSquared(points[id0], points[i]);
				if (distanceSquared < minDistatanceSquared)
				{
					id1 = i;
					minDistatanceSquared = distanceSquared;
				}
				else if (distanceSquared < minDistatanceSquared2)
				{
					id2 = i;
					minDistatanceSquared2 = distanceSquared;
				}
			}

			// find the fourth point which forms the smallest circumcircle with the first two
			double minRadius = double.PositiveInfinity;
			for (int i = 0; i < points.Count; i++)
			{
				// Skip others.
				if (i == id0 || i == id1 || i == id2)
					continue;

				try
				{
					double r = Circumradius(points[id0], points[id1], points[id2], points[i]);
					if (r < minRadius)
					{
						id3 = i;
						minRadius = r;
					}
				}
				catch (ArgumentException)
				{
					// Not a valid sphere, just skip
				}
				catch (Exception)
				{
					GimLogger.Warn("Internal Exception in triangulation. ");
					throw;
				}
			}

			if (minRadius == double.PositiveInfinity)
			{
				throw new ArgumentException("No Delaunay triangulation exists for this input.");
			}

			// TODO FIX ORIENT AND FLIP
			// if 
			if (Orient(points[id0], points[id1], points[id2], points[id3]))
			{
				// flip
				var i = id1;
				id1 = id2;
				id2 = i;
				throw new NotImplementedException();
			}

			Vector center = Circumcenter(points[id0], points[id1], points[id2], points[id3]);

			for (var i = 0; i < points.Count; i++)
			{
				idsAndDistance[i] = new Tuple<int, double>(i, DistanceSquared(center, points[i]));
			}

			// sort the points by distance from the seed triangle circumcenter
			idsAndDistance = idsAndDistance.OrderBy(t => t.Item2).ToList();

			// set up the seed triangle as the starting hull
			int hullStart = id0;
			int hullSize = 4;

			int[] triangles = new int[maxTriangles * 4];

			int[] halfEdges = new int[maxTriangles * 4];
			int hashSize = (int)Math.Ceiling(Math.Sqrt(points.Count));

			int[] hullPrev = new int[points.Count];
			int[] hullMid = new int[points.Count];
			int[] hullNext = new int[points.Count];

			int[] hullTri = new int[points.Count];
			int[] hullHash = new int[hashSize];

			hullNext[id0] = hullPrev[id2] = hullMid[id3] = id1;
			hullNext[id1] = hullPrev[id0] = hullMid[id3] = id2;
			hullNext[id2] = hullPrev[id1] = hullMid[id3] = id0;
			hullMid[id0] = hullMid[id1] = hullMid[id2] = id3;

			hullTri[id0] = 0;
			hullTri[id1] = 1;
			hullTri[id2] = 2;
			hullTri[id3] = 3;

			hullHash[HashKey(points[id0], hashSize)] = id0;
			hullHash[HashKey(points[id1], hashSize)] = id1;
			hullHash[HashKey(points[id2], hashSize)] = id2;
			hullHash[HashKey(points[id3], hashSize)] = id3;

			int trianglesLength = 0;
			AddTriangle(triangles, halfEdges, ref trianglesLength, id0, id1, id2, id3, -1, -1, -1, -1);

			double xp = 0;
			double yp = 0;
			double zp = 0;

			int[] EDGE_STACK = new int[512];

			// Accuracy
			double epsilon = Math.Pow(2, -52);

			for (int k = 0; k < idsAndDistance.Count; k++)
			{
				int i = idsAndDistance[k].Item1;
				Vector currentPoint = points[i];

				// skip near-duplicate points
				if (k > 0 && Math.Abs(currentPoint.X - xp) <= epsilon && Math.Abs(currentPoint.Y - yp) <= epsilon && Math.Abs(currentPoint.Z - zp) <= epsilon)
					continue;

				xp = currentPoint.X;
				yp = currentPoint.Y;
				zp = currentPoint.Z;

				// skip seed triangle points
				if (i == id0 || i == id1 || i == id2 || i == id3)
					continue;

				// find a visible edge on the convex hull using edge hash
				int start = 0;
				for (int j = 0; j < hashSize; j++)
				{
					int key = HashKey(currentPoint, hashSize);
					start = hullHash[(key + j) % hashSize];
					if (start != -1 && start != hullNext[start])
						break;
				}

				start = hullPrev[start];
				int e = start;
				int q = hullNext[e];
				int r = hullMid[e];

				while (!Orient(currentPoint, points[e], points[q], points[q]))
				{
					e = q;
					if (e == start)
					{
						e = int.MaxValue;
						break;
					}

					q = hullNext[e];
					r = hullMid[e];
				}

				if (e == int.MaxValue)
					continue; // likely a near-duplicate point; skip it

				// ???? TODO is this correct?
				// add the first triangle from the point
				int t = AddTriangle(triangles, halfEdges, ref trianglesLength, e, i, hullNext[e], hullMid[e], -1, -1, -1, hullTri[e]);

				// recursively flip triangles from the point until they satisfy the Delaunay condition
				hullTri[i] = Legalize(t + 2, halfEdges, triangles, points, hullStart, hullPrev, hullTri, EDGE_STACK);
				hullTri[e] = t; // keep track of boundary triangles on the hull
				hullSize++;

				// walk forward through the hull, adding more triangles and flipping recursively
				int next = hullNext[e];
				q = hullNext[next];

				while (Orient(currentPoint, points[next], points[q]))
				{
					//t = AddTriangle(triangles, halfEdges, ref trianglesLength, next, i, q, hullTri[i], -1, hullTri[next]);
					//hullTri[i] = Legalize(t + 2, halfEdges, triangles, points, hullStart, hullPrev, hullTri, EDGE_STACK);
					//hullNext[next] = next; // mark as removed
					//hullSize--;
					//next = q;

					//q = hullNext[next];
				}

				// walk backward from the other side, adding more triangles and flipping
				if (e == start)
				{
					q = hullPrev[e];

					while (Orient(currentPoint, points[q], points[e]))
					{
						//t = AddTriangle(triangles, halfEdges, ref trianglesLength, q, i, e, -1, hullTri[e], hullTri[q]);
						//Legalize(t + 2, halfEdges, triangles, points, hullStart, hullPrev, hullTri, EDGE_STACK);
						//hullTri[q] = t;
						//hullNext[e] = e; // mark as removed
						//hullSize--;
						//e = q;

						//q = hullPrev[e];
					}
				}

				// update the hull indices
				hullStart = hullPrev[i] = e;
				hullNext[e] = hullPrev[next] = i;
				hullNext[i] = next;

				// save the two new edges in the hash table
				hullHash[HashKey(currentPoint, hashSize)] = i;
				hullHash[HashKey(points[e], hashSize)] = e;
			}

			//int[] hull = new int[hullSize];
			//var s = hullStart;
			//for (var i = 0; i < hullSize; i++)
			//{
			//	hull[i] = s;
			//	s = hullNext[s];
			//}

			hullPrev = hullNext = hullTri = null; // get rid of temporary arrays

			//// trim typed triangle mesh arrays
			triangles = triangles.Take(trianglesLength).ToArray();
			halfEdges = halfEdges.Take(trianglesLength).ToArray();

			return new Tuple<int[], int[]>(triangles, halfEdges);
		}

		private static int Legalize(int a, int[] halfEdges, int[] triangles, List<Vector> points, int hullStart, int[] hullPrev, int[] hullTri, int[] EDGE_STACK)
		{
			var i = 0;
			int ar;

			// recursion eliminated with a fixed-size stack
			while (true)
			{
				var b = halfEdges[a];

				/* if the pair of triangles doesn't satisfy the Delaunay condition
                 * (p1 is inside the circumcircle of [p0, pl, pr]), flip them,
                 * then do the same check/flip recursively for the new pair of triangles
                 *
                 *           pl                    pl
                 *          /||\                  /  \
                 *       al/ || \bl            al/    \a
                 *        /  ||  \              /      \
                 *       /  a||b  \    flip    /___ar___\
                 *     p0\   ||   /p1   =>   p0\---bl---/p1
                 *        \  ||  /              \      /
                 *       ar\ || /br             b\    /br
                 *          \||/                  \  /
                 *           pr                    pr
                 */
				int a0 = a - a % 4;
				ar = a0 + (a + 2) % 4;

				if (b == -1)
				{ // convex hull edge
					if (i == 0) break;
					a = EDGE_STACK[--i];
					continue;
				}

				int b0 = b - b % 3;
				int al = a0 + (a + 1) % 3;
				int bl = b0 + (b + 2) % 3;

				int p0 = triangles[ar];
				int pr = triangles[a];
				int pl = triangles[al];
				int p1 = triangles[bl];

				bool illegal = InCircle(points[p1], points[p0], points[pr], points[pl]);

				if (illegal)
				{
					triangles[a] = p1;
					triangles[b] = p0;

					int hbl = halfEdges[bl];

					// edge swapped on the other side of the hull (rare); fix the halfedge reference
					if (hbl == -1)
					{
						int e = hullStart;
						do
						{
							if (hullTri[e] == bl)
							{
								hullTri[e] = a;
								break;
							}
							e = hullPrev[e];
						} while (e != hullStart);
					}
					Link(halfEdges, a, hbl);
					Link(halfEdges, b, halfEdges[ar]);
					Link(halfEdges, ar, bl);

					int br = b0 + (b + 1) % 3;

					// don't worry about hitting the cap: it can only happen on extremely degenerate input
					if (i < EDGE_STACK.Length)
					{
						EDGE_STACK[i++] = br;
					}
				}
				else
				{
					if (i == 0)
						break;

					a = EDGE_STACK[--i];
				}
			}

			return ar;
		}

		private static int AddTriangle(int[] triangles, int[] halfEdges, ref int trianglesLength, int id0, int id1, int id2, int id3, int a, int b, int c, int d)
		{
			triangles[trianglesLength] = id0;
			triangles[trianglesLength + 1] = id1;
			triangles[trianglesLength + 2] = id2;
			triangles[trianglesLength + 3] = id3;

			Link(halfEdges, trianglesLength, a);
			Link(halfEdges, trianglesLength + 1, b);
			Link(halfEdges, trianglesLength + 2, c);
			Link(halfEdges, trianglesLength + 3, d);

			int t = trianglesLength;
			trianglesLength += 4;

			return t;
		}

		private static int[] Link(int[] halfEdges, int a, int b)
		{
			halfEdges[a] = b;
			if (b != -1) halfEdges[b] = a;
			return halfEdges;
		}

		private static int HashKey(Vector v, int hashSize)
		{
			return (int)(Math.Floor(PseudoAngle(v) * hashSize) % hashSize);
		}

		private static double PseudoAngle(Vector v)
		{
			double p = v.X / (Math.Abs(v.X) + Math.Abs(v.Y));

			if (double.IsNaN(p))
				return 0;

			return (v.Y > 0 ? 3 - p : 1 + p) / 4; // [0..1]
		}

		private static double Distance(Vector a, Vector b)
		{
			return Math.Sqrt(DistanceSquared(a, b));
		}

		private static double DistanceSquared(Vector a, Vector b)
		{
			return (a - b).LengthSquared();
		}

		private static Vector Circumcenter(Vector a, Vector b, Vector c, Vector d)
		{
			return (a + b + c + d) / 4;
		}

		private static bool Orient(Vector a, Vector b, Vector c)
		{
			return (a.Y - b.Y) * (c.X - a.X) - (a.X - b.X) * (c.Y - a.Y) < 0;
		}

		private static bool Orient(Vector a, Vector b, Vector c, Vector d)
		{
			throw new NotImplementedException();
		}

		private static double Circumradius(Vector a, Vector b, Vector c)
		{
			double dx = b.X - a.X;
			double dy = b.Y - a.Y;
			double ex = c.X - a.X;
			double ey = c.Y - a.Y;
			double bl = dx * dx + dy * dy;
			double cl = ex * ex + ey * ey;
			double d = 0.5 / (dx * ey - dy * ex);
			double x = (ey * bl - dy * cl) * d;
			double y = (dx * cl - ex * bl) * d;
			return x * x + y * y;
		}

		private static double Circumradius(Vector a, Vector b, Vector c, Vector d)
		{
			// (x-a)^2 + (y-b)^2 + (z-c)^2 = R^2
			// 2ax + 2by + 2cz + R^2 - a^2 - b^2 -c^2 = x^2 + y^2 + z^2
			// https://stackoverflow.com/questions/37449046/how-to-calculate-the-sphere-center-with-4-points

			double u = InternalFunctionU(a, b, c, d, b, c, d, a);
			double v = InternalFunctionU(c, d, a, b, d, a, b, c);
			double w = InternalFunctionU(a, c, d, b, b, d, a, c);
			double uvw = 2 * (u + v + w);
			if (uvw == 0.0)
			{
				// Oops.  The points are coplanar.
				throw new ArgumentException();
			}

			double ra = a.LengthSquared();
			double rb = b.LengthSquared();
			double rc = c.LengthSquared();
			double rd = d.LengthSquared();

			Vector origin = new Vector(InternalFunctionEx(ra, rb, rc, rd, uvw, a, b, c, d),
			 InternalFunctionEy(ra, rb, rc, rd, uvw, a, b, c, d),
			 InternalFunctionEz(ra, rb, rc, rd, uvw, a, b, c, d));

			double radius = Distance(origin, a);
			return radius;
		}

		private static double InternalFunctionU(Vector a, Vector b, Vector c, Vector d, Vector e, Vector f, Vector g, Vector h)
		{
			return (a.Z - b.Z) * (c.X * d.Y - d.X * c.Y) - (e.Z - f.Z) * (g.X * h.Y - h.X * g.Y);
		}

		private static double InternalFunctionEx(double ra, double rb, double rc, double rd, double uvw, Vector a, Vector b, Vector c, Vector d)
		{
			return (ra * InternalFunctionDx(b, c, d)
				- rb * InternalFunctionDx(c, d, a)
				+ rc * InternalFunctionDx(d, a, b)
				- rd * InternalFunctionDx(a, b, c))
				/ uvw;
		}

		private static double InternalFunctionEy(double ra, double rb, double rc, double rd, double uvw, Vector a, Vector b, Vector c, Vector d)
		{
			return (ra * InternalFunctionDy(b, c, d)
				- rb * InternalFunctionDy(c, d, a)
				+ rc * InternalFunctionDy(d, a, b)
				- rd * InternalFunctionDy(a, b, c))
				/ uvw;
		}

		private static double InternalFunctionEz(double ra, double rb, double rc, double rd, double uvw, Vector a, Vector b, Vector c, Vector d)
		{
			return (ra * InternalFunctionDz(b, c, d)
				- rb * InternalFunctionDz(c, d, a)
				+ rc * InternalFunctionDz(d, a, b)
				- rd * InternalFunctionDz(a, b, c))
				/ uvw;
		}

		private static double InternalFunctionDx(Vector a, Vector b, Vector c)
		{
			return a.Y * (b.Z - c.Z)
				+ b.Y * (c.Z - a.Z)
				+ c.Y * (a.Z - b.Z);
		}

		private static double InternalFunctionDy(Vector a, Vector b, Vector c)
		{
			return a.Z * (b.X - c.X)
				+ b.Z * (c.X - a.X)
				+ c.Z * (a.X - b.X);
		}

		private static double InternalFunctionDz(Vector a, Vector b, Vector c)
		{
			return a.X * (b.Y - c.Y)
				+ b.X * (c.Y - a.Y)
				+ c.X * (a.Y - b.Y);
		}

		private static List<Sphere> DivideDelaunay(List<Vector> vectors)
		{
			// Divide
			List<Vector> a;
			List<Vector> b;
			(a, b) = SplitVectors(vectors);

			// If one of the 2 sets is to small to create a Delaunay handle them as one set
			if (a.Count <= 5 || b.Count <= 5)
			{
				List<Sphere> Delaunay = new List<Sphere>();
				List<Vector> DelaunayVectors = vectors.GetRange(0, 4);
				vectors.RemoveRange(0, 4);

				Delaunay.Add(new Sphere(DelaunayVectors));

				foreach (Vector v in vectors)
				{
					Delaunay = AddPointToDelaunay(DelaunayVectors, Delaunay, v);
				}
				return Delaunay;
			}

			List<Sphere> solutionA = DivideDelaunay(a);
			List<Sphere> solutionB = DivideDelaunay(b);
			return MergeDelaunay(solutionA, solutionB);
		}

		private static Tuple<List<Vector>, List<Vector>> SplitVectors(List<Vector> vectors)
		{
			throw new NotImplementedException();
		}

		private static List<Sphere> MergeDelaunay(List<Sphere> a, List<Sphere> b)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// This function directly edits the vectors list and the spheres list. 
		/// So both are changed, and it is not necessary to use the returned list.
		/// For clarification what the function does it returns the List of spheres.
		/// </summary>
		/// <param name="vectors"></param>
		/// <param name="spheres"></param>
		/// <param name="newPoint"></param>
		/// <returns></returns>
		private static List<Sphere> AddPointToDelaunay(List<Vector> vectors, List<Sphere> spheres, Vector newPoint)
		{
			List<Sphere> badSpheres = new List<Sphere>();

			foreach (Sphere sphere in spheres)
			{
				if (IsInSphere(newPoint, sphere))
				{
					badSpheres.Add(sphere);
				}
			}

			foreach (Sphere sphere in badSpheres)
			{
				spheres.Remove(sphere);
			}

			if (badSpheres.Count == 0)
			{
				Vector[] newSphereParts = FindClosestsN(newPoint, vectors, 3);
				Sphere newSphere = new Sphere(newSphereParts[0], newSphereParts[1], newSphereParts[2], newPoint);
				List<Vector> pottentialCandidates = new List<Vector>();

				foreach (Vector v in vectors)
				{
					if (IsInSphere(v, newSphere))
					{
						pottentialCandidates.Add(v);
					}
				}

				if (pottentialCandidates.Count > 0)
				{
					// Burger Flip
					throw new NotImplementedException("Can't flip faces yet. ");
				}

				vectors.Add(newPoint);
				spheres.Add(newSphere);
			}
			else
			{
				// Split bad spheres.
				throw new NotImplementedException("Can't reconstruct new Delaunays yet. ");
			}

			return spheres;
		}

		private static Vector[] FindClosestsN(Vector toCheck, List<Vector> vectors, int n)
		{
			Vector[] clossests = new Vector[n];
			double[] clossestsDistanceSquared = new double[n];

			for (int i = 0; i < n; i++)
			{
				clossestsDistanceSquared[i] = double.MaxValue;
			}

			for (int i = 0; i < vectors.Count; i++)
			{
				Vector current = vectors[i];
				double currentDistanceSquared = (toCheck - current).LengthSquared();
				for (int j = 0; j < n; j++)
				{
					if (currentDistanceSquared < clossestsDistanceSquared[j])
					{
						double previousClossestsDistanceSquared = clossestsDistanceSquared[j];
						Vector previousClossests = clossests[j];
						clossests[j] = current;
						clossestsDistanceSquared[j] = currentDistanceSquared;
						currentDistanceSquared = previousClossestsDistanceSquared;
						current = previousClossests;
					}
				}
			}

			return clossests;
		}

		private static bool InCircle(Vector a, Vector b, Vector c, Vector toCheck)
		{
			return InCircle(a.X, a.Y, b.X, b.Y, c.X, c.Y, toCheck.X, toCheck.Y);
		}

		private static bool InCircle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py)
		{
			var dx = ax - px;
			var dy = ay - py;
			var ex = bx - px;
			var ey = by - py;
			var fx = cx - px;
			var fy = cy - py;

			var ap = dx * dx + dy * dy;
			var bp = ex * ex + ey * ey;
			var cp = fx * fx + fy * fy;

			return dx * (ey * cp - bp * fy) -
				   dy * (ex * cp - bp * fx) +
				   ap * (ex * fy - ey * fx) < 0;
		}

		private static bool IsInSphere(Vector toCheck, Sphere sphere)
		{
			return IsInSphere(toCheck, sphere.a, sphere.b, sphere.c, sphere.d);
		}

		/// <summary>
		/// Checks if a vector is in the sphere constructed by the four other vectors.
		/// </summary>
		/// <param name="ToCheck">The vector to check if it is in the sphere constructed with a, b, c, d.</param>
		/// <param name="a">One of the Vectors constructing the sphere.</param>
		/// <param name="b">One of the Vectors constructing the sphere.</param>
		/// <param name="c">One of the Vectors constructing the sphere.</param>
		/// <param name="d">One of the Vectors constructing the sphere.</param>
		private static bool IsInSphere(Vector ToCheck, Vector a, Vector b, Vector c, Vector d)
		{
			// m = midpoint of the sphere.
			Vector m = (a + b + c + d) / 4;

			// distance = Distance from the ToCheck vector to the midpoint of the sphere.
			double distanceSquared = (ToCheck - m).LengthSquared();
			return (a - m).LengthSquared() < distanceSquared &&
				(b - m).LengthSquared() < distanceSquared &&
				(c - m).LengthSquared() < distanceSquared &&
				(d - m).LengthSquared() < distanceSquared;

		}
	}
}
