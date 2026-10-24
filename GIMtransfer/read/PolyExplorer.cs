using cadex;
using GIMcompress;
using System;
using System.Collections.Generic;
using GimUtilities;

namespace GIMtransfer
{
	internal static class PolyExplorer
	{

		//internal static GimData ExplorePoly(ModelData_PolyRepresentation thePoly, GimData gimData, uint parentOfRepresentation)
		//{
		//	if (thePoly == null)
		//	{
  //              throw GimLogger.Error("Unexpected null poly!");
		//	}

		//	ModelData_PolyShapeList shapeList;
		//	try
		//	{
		//		shapeList = thePoly.Get();
		//	}
		//	catch (Exception e)
		//	{
  //              throw GimLogger.Error(e);
		//	}

  //          if (shapeList == null) // The poly has no shapes. The same thing as a brep having no bodies.
  //              return gimData;

		//	gimData.Elements.Add(new GimElement() { elementType = GimElementType.R_Poly });

		//	// Set my parent as my parent.
		//	gimData.Elements[^1].locals.Add(new GimLocal() { parent = parentOfRepresentation });
		//	// Set me as my parents child
		//	gimData.Elements[(int)parentOfRepresentation].elements.Add((uint)gimData.Elements.Count - 1);

		//	int parent = gimData.Elements.Count - 1;

		//	for (uint j = 0; j < shapeList.Size(); j++)
		//	{
		//		gimData.Elements[parent].elements.Add((uint)gimData.Elements.Count);
		//		ModelData_PolyVertexSet vertexSet = shapeList.Access(j);

		//		int vertexCount = vertexSet.NumberOfVertices();
		//		int parentIndex = gimData.Elements.Count;

		//		if (vertexSet.TypeId() == ModelData_PolyLineSet.GetTypeId())
		//		{
		//			ModelData_PolyLineSet polyLineSet = ModelData_PolyLineSet.Cast(vertexSet);
		//			GimElement polyLineElement = new GimElement() {elementType = GimElementType.P_polyline };

		//			polyLineElement.ElementDoubles = new double[vertexCount * 3];
		//			for (int i = 0; i < vertexCount; i++)
		//			{
		//				// Vertexes are mostly unique (No direct duplicate)
		//				ModelData_Point vertex = polyLineSet.Coordinate(i);

		//				polyLineElement.ElementDoubles[i * 3] = vertex.X();
		//				polyLineElement.ElementDoubles[i * 3 + 1] = vertex.Y();
		//				polyLineElement.ElementDoubles[i * 3 + 2] = vertex.Z();
		//			}
		//			int polyLineCount = polyLineSet.NumberOfPolyLines();
		//			polyLineElement.ElementInts = new int[polyLineCount];
		//			for (int l = 0; l < polyLineCount; l++)
		//			{
		//				polyLineElement.ElementInts[l] = polyLineSet.NumberOfVertices(l);

		//				//for (int v = 0; v < polyLineSet.NumberOfVertices(); v++)
		//				//{
		//				//	polyLineElement.ElementInts[l + v] = polyLineSet.NumberOfVertices(l);
		//				//}

		//			}

		//			int polyIndex = gimData.Elements.Count;
		//			gimData.Elements.Add(polyLineElement);
		//			gimData.Elements[parentIndex].locals.Add(new GimLocal() { parent = (uint)parent });

		//			if (polyLineSet.Appearance() != null) 
		//			{
		//				GimMaterial material = new GimMaterial() { elementType = GimElementType.Material };
		//				ModelData_Appearance theAppearance = polyLineSet.Appearance();

		//				// Save textures to Gim Material.
		//				ModelData_TextureSet theTextureSet = theAppearance.TextureSet();
		//				List<GimTexture> materialTextures = null;
		//				if (theTextureSet != null && !theTextureSet.IsEmpty() && theTextureSet.NumberOfTextures() > 0)
		//				{
		//					// Texture visitor
		//					CadexTextureVisitor textureVisitor = new CadexTextureVisitor();
		//					theTextureSet.Accept(textureVisitor);
		//					//material.textures = textureVisitor.gimTextures;
		//					materialTextures = textureVisitor.gimTextures;
		//				}

		//				// Generic colour not needed? Is either ToColor or DiffuseColor.
		//				// ModelData_ColorObject c = theAppearance.GenericColor();

		//				ModelData_Color tc = new ModelData_Color();
		//				if (theAppearance.ToColor(tc))
		//				{
		//					// ToColor = GenericColor or DiffuseColor!
		//					float a, r, g, b;
		//					a = tc.A(); r = tc.R(); g = tc.G(); b = tc.B();
		//					material.BaseColour = new GimColour(a, r, g, b).ToInt();
		//				}

		//				ModelData_Material theMaterial = new ModelData_Material();
		//				if (theAppearance.ToMaterial(theMaterial))
		//				{
		//					material.AmbientColour = GetColour(theMaterial.AmbientColor()).ToInt();
		//					material.DiffuseColour = GetColour(theMaterial.DiffuseColor()).ToInt();
		//					material.SpecularColour = GetColour(theMaterial.SpecularColor()).ToInt();
		//					material.Shininess = theMaterial.Shininess();
		//				}

		//				uint materialID = (uint)gimData.elements.Count;
		//				gimData.Elements.Add(material);
		//				gimData.AddChildToParent(materialID, (uint)polyIndex);
		//				if (materialTextures != null)
		//				{
		//					for (int i = 0; i < materialTextures.Count; i++)
		//					{
		//						gimData.Elements.Add(materialTextures[i]);
		//						gimData.AddChildToParent((uint)gimData.elements.Count - 1, materialID);
		//					}
		//				}
		//			}


		//		}
		//		else if (vertexSet.TypeId() == ModelData_IndexedTriangleSet.GetTypeId())
		//		{

		//			ModelData_IndexedTriangleSet indexedTriangleSet = ModelData_IndexedTriangleSet.Cast(vertexSet);
		//			GimElement polyElement = new GimElement() { elementType = GimElementType.P_polygon };

		//			polyElement.ElementDoubles = new double[vertexCount * 3];
		//			for (int i = 0; i < vertexCount; i++)
		//			{
		//				// Vertexes are mostly unique (No direct duplicate)
		//				ModelData_Point vertex = indexedTriangleSet.Coordinate(i);

		//				polyElement.ElementDoubles[i * 3] = vertex.X();
		//				polyElement.ElementDoubles[i * 3 + 1] = vertex.Y();
		//				polyElement.ElementDoubles[i * 3 + 2] = vertex.Z();
		//			}

		//			int faceCount = indexedTriangleSet.NumberOfFaces();
		//			polyElement.ElementInts = new int[faceCount * 3];
		//			for (int f = 0; f < faceCount; f++)
		//			{
		//				for (int v = 0; v < 3; v++)
		//				{
		//					polyElement.ElementInts[f * 3 + v] = indexedTriangleSet.CoordinateIndex(f, v);
		//				}

		//			}

		//			int polyIndex = gimData.Elements.Count;
		//			gimData.Elements.Add(polyElement);
		//			gimData.Elements[parentIndex].locals.Add(new GimLocal() { parent = (uint)parent });

		//			if(indexedTriangleSet.Appearance() != null)
  //                  {
		//				GimMaterial material = new GimMaterial() { elementType = GimElementType.Material };
		//				ModelData_Appearance theAppearance = indexedTriangleSet.Appearance();

		//				// Save textures to Gim Material.
		//				ModelData_TextureSet theTextureSet = theAppearance.TextureSet();
		//				List<GimTexture> materialTextures = null;
		//				if (theTextureSet != null && !theTextureSet.IsEmpty() && theTextureSet.NumberOfTextures() > 0)
		//				{
		//					// Texture visitor
		//					CadexTextureVisitor textureVisitor = new CadexTextureVisitor();
		//					theTextureSet.Accept(textureVisitor);
		//					//material.textures = textureVisitor.gimTextures;
		//					materialTextures = textureVisitor.gimTextures;
		//				}

		//				// Generic colour not needed? Is either ToColor or DiffuseColor.
		//				// ModelData_ColorObject c = theAppearance.GenericColor();

		//				ModelData_Color tc = new ModelData_Color();
		//				if (theAppearance.ToColor(tc))
		//				{
		//					// ToColor = GenericColor or DiffuseColor!
		//					float a, r, g, b;
		//					a = tc.A(); r = tc.R(); g = tc.G(); b = tc.B();
		//					material.BaseColour = new GimColour(a, r, g, b).ToInt();
		//				}

		//				ModelData_Material theMaterial = new ModelData_Material();
		//				if (theAppearance.ToMaterial(theMaterial))
		//				{
		//					material.AmbientColour = GetColour(theMaterial.AmbientColor()).ToInt();
		//					material.DiffuseColour = GetColour(theMaterial.DiffuseColor()).ToInt();
		//					material.SpecularColour = GetColour(theMaterial.SpecularColor()).ToInt();
		//					material.Shininess = theMaterial.Shininess();
		//				}

		//				uint materialID = (uint)gimData.elements.Count;
		//				gimData.Elements.Add(material);
		//				gimData.AddChildToParent(materialID, (uint)polyIndex);
		//				if (materialTextures != null)
		//				{
		//					for (int i = 0; i < materialTextures.Count; i++)
		//					{
		//						gimData.Elements.Add(materialTextures[i]);
		//						gimData.AddChildToParent((uint)gimData.elements.Count - 1, materialID);
		//					}
		//				}
		//			}
		//		}
		//		else if (vertexSet.TypeId() == ModelData_PolyPointSet.GetTypeId())
		//		{
		//			ModelData_PolyPointSet polyPointSet = ModelData_PolyPointSet.Cast(vertexSet);
		//			gimData.Elements.Add(new GimElement() { elementType = GimElementType.P_points });
		//		}
		//		else
		//			throw GimLogger.Error(new NotSupportedException());
		//	}

		//	return gimData;
		//}

		//internal static GimColour GetColour(ModelData_Color theColour)
		//{
		//	return new GimColour()
		//	{
		//		alpha = theColour.A(),
		//		red = theColour.R(),
		//		green = theColour.G(),
		//		blue = theColour.B()
		//	};
		//}
	}
}
