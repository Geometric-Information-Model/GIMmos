using cadex;
using GIMtransfer.BrepToPoly;
using GimUtilities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using GimUtilities;
using System.Collections;

[assembly: InternalsVisibleTo("TestPlatform")]
namespace GIMtransfer
{
	internal static partial class CadexModel
	{
//		/// <summary>
//		/// Read a files to the cadexes internal ModelData_Model
//		/// </summary>
//		/// <param name="aSource">The full path of the file to read.</param>
//		/// <param name="fileType">The filetype of the file.</param>
//		/// <returns>The ModelData_Model that is read.</returns>
//		/// 
//		internal static ModelData_Model Read(string aSource, string fileType)
//		{
//			fileType = fileType.ToLower();
//			Base_Reader aReader;
//			switch (fileType)
//			{
//				case "3dxml":
//					aReader = new DS3DXML_Reader();
//					break;
//				case "3mf":
//					aReader = new X3MF_Reader();
//					break;
//				case "3ds":
//					aReader = new A3DS_Reader();
//					break;
//				case "sat":
//				case "sab":
//					aReader = new ACIS_Reader();
//					break;
//				case "brep":
//					aReader = new BRep_Reader();
//					break;
//				case "catpart":
//				case "catproduct":
//					aReader = new CAT_Reader();
//					break;
//				case "dae":
//					aReader = new Collada_Reader();
//					break;
//				case "prt.creo":
//				case "asm.creo":
//					aReader = new Creo_Reader();
//					break;
//				case "dxf":
//					aReader = new DXF_Reader();
//					break;
//				case "dwg":
//					aReader = new DWG_Reader();
//					break;
//				case "fbx":
//					aReader = new FBX_Reader();
//					break;
//				case "gltf":
//				case "glb":
//					aReader = new GLTF_Reader(); ;
//					//
//					//"It's not possible to read the file " + aSource);
//					break;
//				case "ifc":
//					aReader = new IFC_Reader();
//					break;
//				case "igs":
//				case "iges":
//					aReader = new IGES_Reader();
//					break;
//				case "jt":
//					aReader = new JT_Reader();
//					break;
//				case "prt.nx":
//					aReader = new NX_Reader();
//					break;
//				case "obj":
//					aReader = new OBJ_Reader();
//					break;
//				case "x_t":
//				case "x_b":
//				case "xmt_txt":
//				case "xmt_bin":
//				case "xmb_txt":
//				case "xmb_bin":
//					aReader = new Para_Reader();
//					break;
//				case "3dm":
//					aReader = new Rhino_Reader();
//					break;
//				case "par":
//				case "psm":
//				case "asm.SLDE":
//					aReader = new SLDE_Reader();
//					break;
//                //case "rvt":
//                //    aReader = new Revit_Reader();
//                //    break;
//                case "sldprt":
//				case "sldasm":
//					aReader = new SLD_Reader();
//					break;
//				case "ply":
//					aReader = new PLY_Reader();
//					break;
//				case "prc":
//					aReader = new PRC_Reader();
//					break;
//				case "step":
//				case "stp":
//                    // TODO: Generate step parameters automaticly based on the file!
//                    GimLogger.Info("Generate step parameters automatically based on the file!");
//					STEP_ReaderParameters x = new STEP_ReaderParameters();
//					x.SetPreferredBRepRepresentationType(STEP_ReaderParameters.BRepRepresentationType.All);
//					STEP_Reader theStepReader = new STEP_Reader();
//					theStepReader.SetParameters(x);
//					aReader = theStepReader;
//					break;
//				case "stl":
//					aReader = new STL_Reader();
//					break;
//				case "u3d":
//					aReader = new U3D_Reader();
//					break;
//				case "wrl":
//					aReader = new VRML_Reader();
//					break;
//				case "x3d":
//					aReader = new X3D_Reader();
//					break;
//				case "pdf":
//					throw GimLogger.Error("(Error: PDF) Failed to read the file " + aSource + " as a type " + fileType);
//				//aReader = new reader();
//				default: throw GimLogger.Error("(Error: FT) Failed to read the file " + aSource + " as a type " + fileType);
//			}
//			ModelData_Model aModel = new ModelData_Model();

//			try
//			{
//				try
//				{
//					if (!aReader.ReadFile(new Base_UTF16String(aSource)))
//						throw GimLogger.Error("Failed to read the file " + aSource + ". This is a cadex error!");
//				} catch (AccessViolationException e)
//                {
//					throw GimLogger.Error("Failed to read the file " + aSource + ", Access Violation error from Cadex!");
//				}
//			}
//			catch
//			{
//				GimLogger.Warn("Internal cadex error with the fileType: " + fileType);
//				GimLogger.Warn(FileAccess.FileTypes[fileType].FileDescription);
//				throw;
//			}

//			try
//			{
//				if (!aReader.Transfer(aModel))
//					throw GimLogger.Error("Failed to transfer the file into inner format. This is a cadex error!");
//			}
//			catch
//			{
//				GimLogger.Warn("Failed to transfer the file into inner format. This is a cadex error!");
//				GimLogger.Warn(FileAccess.FileTypes[fileType].FileDescription);
//				throw;
//			}

//			return aModel;
//		}

//		/// <summary>
//		/// Logs the error from Cadex when transferring the file
//		/// </summary>
//		/// <param name="writer">The writer that was used to attempt to write the file</param>
//		/// <returns></returns>
//		internal static string LogError(Base_Writer writer)
//		{
//			string errorString = "";

//			var writeStats = writer.Statistics();
//			var errIterator = new Base_WriterStatistics.ErrorIterator(writeStats);
//			var shapeLogger = new CadexShapeLogger();
//			while (errIterator.HasNext())
//			{
//				shapeLogger.ResetStack();
//				var error = errIterator.Next();
//				errorString += "<error>\n";
//				errorString += "\t<message>\n\t\t" + error.Message().ToString() + "\n";
//				errorString += "\t</message>\n";
//				errorString += "\t<context>\n\t\t" + error.Context().ToString() + "\n";
//				errorString += "\t</context>\n";
//				errorString += "\t<sourceShape>\n\t\t" + error.Shape().ToString() + "\n";
//				shapeLogger.LogShape(error.Shape(), "\t\t");
//				errorString += shapeLogger.GetResult();
//				errorString += "\t</sourceShape>\n";
//				errorString += "</error>\n";
//			}

//			return errorString;
//		}


//		/// <summary>
//		/// Writes a file from cadexes internal ModelData_Model to the Temp folder. 
//		/// </summary>
//		/// <param name="aModel">The ModelData_Model to write.</param>
//		/// <param name="fileType">The filetype to write the files as.</param>
//		/// <param name="fileName">The name of the file.</param>
//		/// <returns>The filepath the function wrote to or the error as a string.</returns>
//		internal static bool Write(ModelData_Model aModel, string fileType, string fileName, ref string errorsAndWarnings, ref string returnedPath)
//		{
//			string error = ""; //for error messages
//			returnedPath = "";
//			errorsAndWarnings = "";
//			string path = FileAccess.GetTempPath(fileName); // define and create the subfolder
//			fileType = fileType.ToLower();
//			Base_UTF16String theFileName = new Base_UTF16String(path + "." + fileType);
//			Base_Writer aWriter;
//			switch (fileType)
//			{
//				case "sat":
//				case "sab":
//					aWriter = new ACIS_Writer();
//					break;
//				case "brep":
//					aWriter = new BRep_Writer();
//					break;
//				case "dae":
//					aWriter = new Collada_Writer();
//					break;
//				case "dxf":
//					aWriter = new DXF_Writer();
//					break;
//				case "fbx":
//					aWriter = new FBX_Writer();
//					break;
//				case "glb":
//				case "gltf":
//					aWriter = new GLTF_Writer();
//					break;
//				case "ifc":
//					aWriter = new IFC_Writer();
//					break;
//				case "igs":
//				case "iges":
//					aWriter = new IGES_Writer();
//					break;
//				case "jt":
//					aWriter = new JT_Writer();
//					break;
//				case "obj":
//					aWriter = new OBJ_Writer();
//					break;
//				case "x_t":
//				case "x_b":
//				case "xmt_txt":
//				case "xmt_bin":
//				case "xmb_txt":
//				case "xmb_bin":
//					aWriter = new Para_Writer();
//					break;
//				case "3dm":
//					aWriter = new Rhino_Writer();
//					break;
//				case "step":
//				case "stp":
//					aWriter = new STEP_Writer();
//					break;
//				case "stl":
//					aWriter = new STL_Writer();
//					break;
//				case "usd":
//				case "usda":
//				case "usdc":
//				case "usdz":
//					aWriter = new USD_Writer();
//					break;
//				case "wrl":
//					aWriter = new VRML_Writer();
//					break;
//				case "x3d":
//					aWriter = new X3D_Writer();
//					break;
//				default: 
//					error = "(Error: WE) Failed to create writer for the filetype: " + fileType;

//                    GimLogger.Error(error);
//                    errorsAndWarnings = error;
//					return false;
//			}

//			Base_Logger.GlobalInstance().SetLevel(Base_Logger.Level.Error);

//            // Converting model data to a new format
//            if (!aWriter.Transfer(aModel))
//			{
//				error = "(Error: CMT) Failed to transfer model data to specified format. This could be our problem as well as a cadex problem.";
//#if DEBUG
//				error += LogError(aWriter);
//#endif
//				GimLogger.Error(error);
//				errorsAndWarnings = error;
//				return false;
//			}



//            // Writing model data to file
//            if (!aWriter.WriteFile(new Base_UTF16String(theFileName)))
//			{
//				error = "(Error: CWF) Failed to write the file. This could be our problem as well as a cadex problem.";

//                error += LogError(aWriter);

//                GimLogger.Error(error);
//				return false;
//			}


//            error += LogError(aWriter);
//			errorsAndWarnings = error;
//            GimLogger.Info(theFileName.ToString());
//			returnedPath = theFileName.ToString();
//			return true;

//		}

//		internal class CadexShapeLogger
//		{

//			StringBuilder resultStack = new();

//			public void ResetStack()
//			{
//				resultStack = new();
//			}

//			public string GetResult()
//			{
//				return resultStack.ToString();
//			}

//			private static string OpenTag(string tagName)
//			{
//				return "<" + tagName + ">";
//			}
//			private static string CloseTag(string tagName)
//			{
//				return "</" + tagName + ">";
//			}
//			public void LogShape(ModelData_Shape shape, string prefix = "")
//			{

//                resultStack.AppendLine(prefix + OpenTag("Shape"));

//				var type = shape.Type();
//				switch (type)
//				{
//					case ModelData_ShapeType.ModelData_ST_BodyList:
//						{
//							var bodyList = ModelData_BodyList.Cast(shape);
//							LogBodyList(bodyList, prefix + "\t");
//                        }
//                        break;
//					case ModelData_ShapeType.ModelData_ST_Body:
//						{
//							var body = ModelData_Body.Cast(shape);
//							LogBody(body, prefix + "\t");
//						}
//						break;
//					case ModelData_ShapeType.ModelData_ST_Solid:
//						{
//							var solid = ModelData_Solid.Cast(shape);
//							LogSolid(solid, prefix + "\t");
//						}
//						break;
//					case ModelData_ShapeType.ModelData_ST_Shell:
//						{
//							var shell = ModelData_Shell.Cast(shape);
//							LogShell(shell, prefix + "\t");
//						}
//                        break;
//					case ModelData_ShapeType.ModelData_ST_Face:
//						{
//							var face = ModelData_Face.Cast(shape);
//							LogFace(face, prefix + "\t");
//						}
//						break;
//					case ModelData_ShapeType.ModelData_ST_Wire:
//						{
//							var wire = ModelData_Wire.Cast(shape);
//							LogWire(wire, prefix + "\t");
//						}
//						break;
//					case ModelData_ShapeType.ModelData_ST_Edge:
//						{
//							var edge = ModelData_Edge.Cast(shape);
//							LogEdge(edge, prefix + "\t");
//						}
//						break;
//					case ModelData_ShapeType.ModelData_ST_Vertex:
//						{
//							var vertex = ModelData_Vertex.Cast(shape);
//							double x = vertex.Point().X();
//							double y = vertex.Point().Y();
//							double z = vertex.Point().Z();
//                            resultStack.AppendLine(prefix + "\t" + OpenTag("Vertex"));
//                            resultStack.AppendLine(prefix + "\t\t" + "(" + x + ", " + y + ", " + z + ")");
//                            resultStack.AppendLine(prefix + "\t" + CloseTag("Vertex"));

//                        }
//						break;
//                    default:
//						{
//							resultStack.AppendLine(prefix + "\t" + "unknown shape");
//						}break;

//				}


//                resultStack.AppendLine(prefix + CloseTag("Shape"));
//			}

//			public void LogBodyList(ModelData_BodyList bodyList, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Body List"));

//                for (int i = 0; i < bodyList.Size(); i++)
//                {
//                    LogBody(bodyList.Access((uint)i), prefix + "\t");
//                }

//                resultStack.AppendLine(prefix + CloseTag("Body List"));
//            }

//			public void LogBody(ModelData_Body body, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Body"));

//                resultStack.AppendLine(prefix + "\t" + "body type: " + body.Type().ToString());
//                resultStack.AppendLine(prefix  + "\t" + "body orientation: " + body.Orientation().ToString());

//                var shapeIterator = new ModelData_Shape.Iterator(body);

//                while (shapeIterator.HasNext())
//                {
//                    ModelData_Shape shape = shapeIterator.Next();
//                    LogShape(shape, prefix + "\t");
//                }

//                resultStack.AppendLine(prefix + CloseTag("Body"));
//            }

//			public void LogSolid(ModelData_Solid solid, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Solid"));

//                var shapeIterator = new ModelData_Shape.Iterator(solid);

//                while (shapeIterator.HasNext())
//                {
//                    ModelData_Shape shape = shapeIterator.Next();
//                    LogShape(shape, prefix + "\t");
//                }

//                resultStack.AppendLine(prefix + CloseTag("Solid"));
//            }

//			public void LogShell(ModelData_Shell shell, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Shell"));

//                var shapeIterator = new ModelData_Shape.Iterator(shell);

//                while (shapeIterator.HasNext())
//                {
//                    ModelData_Shape shape = shapeIterator.Next();
//                    LogShape(shape, prefix + "\t");
//                }

//                resultStack.AppendLine(prefix + CloseTag("Shell"));
//            }

//			public void LogFace(ModelData_Face face, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Face"));

//				var surface = face.Surface();

//				switch (surface.Type())
//				{
//					case ModelData_SurfaceType.ModelData_ST_Plane:
//						{
//							var plane = ModelData_Plane.Cast(surface);
//							LogPlane(plane, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Cylinder:
//						{
//							var cylinder = ModelData_CylindricalSurface.Cast(surface);
//							LogCylinder(cylinder, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Cone:
//						{
//							var cone = ModelData_ConicalSurface.Cast(surface);
//							LogCone(cone, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Sphere:
//						{
//							var sphere = ModelData_SphericalSurface.Cast(surface);
//							LogSphere(sphere, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Torus:
//						{
//							var torus = ModelData_ToroidalSurface.Cast(surface);
//							LogTorus(torus, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_LinearExtrusion:
//						{
//							var linearExtrusion = ModelData_SurfaceOfLinearExtrusion.Cast(surface);
//							LogExtrusion(linearExtrusion, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Revolution:
//						{
//							var revolution = ModelData_SurfaceOfRevolution.Cast(surface);
//							LogRevolution(revolution, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Bezier:
//						{
//							var bezier = ModelData_BezierSurface.Cast(surface);
//							LogBezier(bezier, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_BSpline:
//						{
//							var bspline = ModelData_BSplineSurface.Cast(surface);
//							LogBSpline(bspline, prefix + "\t");
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Offset:
//						{
//							var offset = ModelData_OffsetSurface.Cast(surface);
//							LogOffset(offset, prefix + "\t");
//						}
//                        break;
//					case ModelData_SurfaceType.ModelData_ST_Trimmed:
//						{
//							var trimmed = ModelData_RectangularTrimmedSurface.Cast(surface);
//							LogTrimmed(trimmed, prefix + "\t");
//						}
//						break;
//					default:
//						{
//                            resultStack.AppendLine(prefix  + "\t" + "unknown face type");
//                        }
//                        break;
//				}

//                var shapeIterator = new ModelData_Shape.Iterator(face);

//                while (shapeIterator.HasNext())
//                {
//                    ModelData_Shape shape = shapeIterator.Next();
//                    LogShape(shape, prefix + "\t");
//                }

//                resultStack.AppendLine(prefix + CloseTag("Face"));
//            }

//			public void LogPlane(ModelData_Plane plane, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Plane"));

//                resultStack.AppendLine(prefix + "\t" + "Position: (" + plane.Position().Location().X() + ", " + plane.Position().Location().Y() + ", " + plane.Position().Location().Z() +")");
//                resultStack.AppendLine(prefix + "\t" + "X-Direction: (" + plane.Position().XDirection().X() + ", " + plane.Position().XDirection().Y() + ", " + plane.Position().XDirection().Z() +")");
//                resultStack.AppendLine(prefix + "\t" + "Y-Direction: (" + plane.Position().YDirection().X() + ", " + plane.Position().YDirection().Y() + ", " + plane.Position().YDirection().Z() +")");
//                resultStack.AppendLine(prefix + "\t" + "Z-Axis: (" + plane.Position().Axis().X() + ", " + plane.Position().Axis().Y() + ", " + plane.Position().Axis().Z() +")");

//                resultStack.AppendLine(prefix + CloseTag("Plane"));
//            }

//			public void LogCylinder(ModelData_CylindricalSurface cylinder, string prefix)
//			{
//				resultStack.AppendLine(prefix + "cylinder Not Implemented");
//			}
//            public void LogCone(ModelData_ConicalSurface cone, string prefix)
//            {
//                resultStack.AppendLine(prefix + "cone Not Implemented");
//            }

//            public void LogSphere(ModelData_SphericalSurface sphere, string prefix)
//            {
//                resultStack.AppendLine(prefix + "sphere Not Implemented");
//            }

//            public void LogTorus(ModelData_ToroidalSurface torus, string prefix)
//            {
//                resultStack.AppendLine(prefix + "torus Not Implemented");
//            }

//            public void LogExtrusion(ModelData_SurfaceOfLinearExtrusion extrusion, string prefix)
//            {
//                resultStack.AppendLine(prefix + "extrusion Not Implemented");
//            }

//            public void LogBezier(ModelData_BezierSurface bezier, string prefix)
//            {
//                resultStack.AppendLine(prefix + "bezier Not Implemented");
//            }

//            public void LogBSpline(ModelData_BSplineSurface bspline, string prefix)
//            {
//                resultStack.AppendLine(prefix + "bspline Not Implemented");
//            }

//            public void LogTrimmed(ModelData_RectangularTrimmedSurface trimmed, string prefix)
//            {
//                resultStack.AppendLine(prefix + "trimmed Not Implemented");
//            }

//            public void LogRevolution(ModelData_SurfaceOfRevolution revolution, string prefix)
//            {
//                resultStack.AppendLine(prefix + "revolution Not Implemented");
//            }

//			public void LogOffset(ModelData_OffsetSurface offset, string prefix)
//			{
//                resultStack.AppendLine(prefix + "offset Not Implemented");
//            }



//            public void LogWire(ModelData_Wire wire, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Wire"));

//                var shapeIterator = new ModelData_Shape.Iterator(wire);

//                while (shapeIterator.HasNext())
//                {
//                    ModelData_Shape shape = shapeIterator.Next();
//                    LogShape(shape, prefix + "\t");
//                }

//                resultStack.AppendLine(prefix + CloseTag("Wire"));
//            }

//			public void LogEdge(ModelData_Edge edge, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Edge"));

//				double start = 0, end = 0;
//                var curve = edge.Curve(ref start, ref end);
//				if (curve == null)
//				{
//                    resultStack.AppendLine(prefix + "\t" + "Null Curve on Edge!");

//                }
//                else
//				{
//					switch (curve.Type())
//					{
//						case ModelData_CurveType.ModelData_CT_Line:
//							{
//								var line = ModelData_Line.Cast(curve);
//								LogLine(line, prefix + "\t");
//							}
//							break;
//						default:
//							{
//								resultStack.AppendLine(prefix + "\t" + "curve " + curve.Type().ToString() + "unimplemented");
//							}
//							break;
//					}
//				}

//                var shapeIterator = new ModelData_Shape.Iterator(edge);

//                while (shapeIterator.HasNext())
//                {
//                    ModelData_Shape shape = shapeIterator.Next();
//                    LogShape(shape, prefix + "\t");
//                }


//                resultStack.AppendLine(prefix + CloseTag("Edge"));
//            }

//			public void LogLine(ModelData_Line line, string prefix)
//			{
//                resultStack.AppendLine(prefix + OpenTag("Line"));

//                resultStack.AppendLine(prefix + "\t" + "Position: (" + line.Location().X() + ", " + line.Location().Y() + ", " + line.Location().Z());
//                resultStack.AppendLine(prefix + "\t" + "Direction: (" + line.Direction().X() + ", " + line.Direction().Y() + ", " + line.Direction().Z());
//                resultStack.AppendLine(prefix + "\t" + "Continuity: (" + line.Continuity().ToString());
//                resultStack.AppendLine(prefix + "\t" + "Period: (" + line.IsPeriodic());

//                resultStack.AppendLine(prefix + CloseTag("Line"));
//            }
//		}

//		internal static ModelData_Model ReadGlbToGimData(string glb) 
//		{
//            Base_Reader aReader = new GLTF_Reader();

//            ModelData_Model aModel = new ModelData_Model();

//            try
//            {
//				try
//				{
//                    //string glb1 = Encoding.Unicode.GetString(glb);
//                    if (!aReader.ReadFile(new Base_UTF16String(glb)))
//                        throw GimLogger.Error("Failed to read the file "  + ". This is a cadex error!");
//                }
//                catch (AccessViolationException e)
//                {
//                    throw GimLogger.Error("Failed to read the file " + ", Access Violation error from Cadex!");
//                }
//            }
//            catch
//            {
//                GimLogger.Warn("Internal cadex error with the fileType: " + "glb");
//                GimLogger.Warn(FileAccess.FileTypes["glb"].FileDescription);
//                throw;
//            }

//            try
//            {
//                if (!aReader.Transfer(aModel))
//                    throw GimLogger.Error("Failed to transfer the file into inner format. This is a cadex error!");
//            }
//            catch
//            {
//                GimLogger.Warn("Failed to transfer the file into inner format. This is a cadex error!");
//                GimLogger.Warn(FileAccess.FileTypes["glb"].FileDescription);
//                throw;
//            }

//			return aModel;

//        }


    }
}
