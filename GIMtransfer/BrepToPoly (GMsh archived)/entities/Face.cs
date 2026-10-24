namespace GIMtransfer.BrepToPoly
{
	public class Face
	{
		public Vertex A
		{
			get { return vertices[0]; }
			set { vertices[0] = value; }
		}

		public Vertex B
		{
			get { return vertices[1]; }
			set { vertices[1] = value; }
		}

		public Vertex C
		{
			get { return vertices[2]; }
			set { vertices[2] = value; }
		}

		public Vertex[] vertices = new Vertex[3];

		public Face(Vertex a, Vertex b, Vertex c)
		{
			A = a;
			B = b;
			C = c;
		}
	}
}
