using cadex;
using GIMcompress;
using System;
using GimUtilities;

namespace GIMtransfer
{
	internal static partial class CadexModel
	{
		//internal static ModelData_PolyRepresentation GimDataBrepObjectToPoly(GimData gimData, int gimObjectIndex, GimProgressStatus progressStatus)
		//{
		//	//Do mesher magic.
		//	ModelAlgo_BRepMesherParameters parameters = new ModelAlgo_BRepMesherParameters(ModelAlgo_BRepMesherParameters.Granularity.Coarse);
		//	ModelAlgo_BRepMesher bRepMesher = new ModelAlgo_BRepMesher(parameters);
		//	ModelData_BRepRepresentation bReprepresentation = CadexWriteElements.GetObjectShape(gimData, gimObjectIndex, progressStatus);
		//	ModelData_PolyRepresentation polyRepresentation;

		//	try
		//	{
		//		polyRepresentation = bRepMesher.Compute(bReprepresentation);
		//	}
		//	catch
		//	{
		//		throw GimLogger.Error("Transforming BRepRepresentation to PolyRepresentation failed");
		//	}

		//	bRepMesher.Dispose();
		//	parameters.Dispose();
		//	bReprepresentation.Dispose();
		//	return polyRepresentation;
		//}

		//public static ModelData_Model CadexModelToPoly(ModelData_Model model)
		//{
		//	//Do mesher magic.
		//	ModelAlgo_BRepMesherParameters parameters = new ModelAlgo_BRepMesherParameters(ModelAlgo_BRepMesherParameters.Granularity.Coarse);
		//	ModelAlgo_BRepMesher bRepMesher = new ModelAlgo_BRepMesher(parameters);
		//	try
		//	{
		//		bRepMesher.Compute(model, false);
		//	}
		//	catch (Exception e)
		//	{
		//		throw GimLogger.Error(e);
		//	}
		//	finally
		//	{
		//		bRepMesher.Dispose();
		//		parameters.Dispose();
		//	}
		//	return model;
		//}
	}
}
