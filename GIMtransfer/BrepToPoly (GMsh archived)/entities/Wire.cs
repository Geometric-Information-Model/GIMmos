using System.Collections.Generic;

namespace GIMtransfer.BrepToPoly
{
	class Wire
	{
		public List<Vertex> vertices;
		public bool orientation;

		public Wire(List<Vertex> vertices, bool orientation)
		{
			this.vertices = vertices;
			this.orientation = orientation;
		}

		public override string ToString()
		{
			return Vertex.GenerateStringRepresentation(vertices);
		}

		public static string GenerateStringRepresentation(List<Wire> wires)
		{
			string lineOutput = "";
			foreach (Wire wire in wires)
				lineOutput += wire.ToString();
			return lineOutput;
		}
	}
}
