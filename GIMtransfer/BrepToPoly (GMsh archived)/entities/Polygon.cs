using GIMcompress;
using System.Collections.Generic;
using System.Linq;

namespace GIMtransfer.BrepToPoly
{
	public class Polygon
	{
		public List<Face> faces;
		public List<Vertex> vertices;

		public void Merge(Polygon other)
		{
			if (faces == null && vertices == null)
			{
				faces = other.faces;
				vertices = other.vertices;
				return;
			}

			for (int i = 0; i < other.faces.Count; i++)
			{
				Face face = other.faces[i];
				for (int j = 0; j < face.vertices.Length; j++)
				{
					Vertex foundVertex = FindVertex(face.vertices[j]);
					if (foundVertex != null)
					{
						other.vertices.Remove(face.vertices[j]);
						face.vertices[j] = foundVertex;
					}
				}
			}

			vertices.AddRange(other.vertices);
			faces.AddRange(other.faces);
		}

		/// <summary>
		/// Looks for the first vertex that is equal to the given vertex
		/// </summary>
		/// <param name="toFind"> The vertex to look for.</param>
		/// <returns> Null in none exist otherwise the vertex. </returns>
		private Vertex FindVertex(Vertex toFind)
		{
			return vertices.FirstOrDefault((vertex) => vertex.Equals(toFind));
		}

		public GimElement ToGimElement()
		{
			Dictionary<Vertex, int> vertexPointers = new Dictionary<Vertex, int>(vertices.Count);
			int[] indexTriangleList = new int[faces.Count * 3];
			int pointerCounter = 0;
			double[] verticesAsArray = new double[vertices.Count * 3];

			for (int i = 0; i < faces.Count; i++)
			{
				for (int j = 0; j < faces[i].vertices.Length; j++)
				{
					
					if (!vertexPointers.TryGetValue(faces[i].vertices[j], out int verticePointer))
					{
						vertexPointers.Add(faces[i].vertices[j], pointerCounter);
						verticePointer = pointerCounter;
						verticesAsArray[pointerCounter] = faces[i].vertices[j].x;
						verticesAsArray[pointerCounter + 1] = faces[i].vertices[j].y;
						verticesAsArray[pointerCounter + 2] = faces[i].vertices[j].z;
						pointerCounter++;
					}
					indexTriangleList[i * 3 + j] = verticePointer;
				}
			}

			return new GimElement()
			{
				elementDoubles = verticesAsArray,
				elementInts = indexTriangleList,
				elementType = GimElementType.P_polygon,
			};
		}
	}
}
