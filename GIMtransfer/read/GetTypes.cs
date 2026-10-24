using cadex;
using GIMcompress;
using System;
using System.Collections.Generic;
using GimUtilities;

namespace GIMtransfer
{
	internal partial class Cadex
	{
		internal partial class BRepExplorer
		{
			/// <summary>
			/// Creates a GimElement from the ModelData_Surface. For some surface types this also creates a basisElement. 
			/// </summary>
			/// <param name="theSurface"> This is the surface to read. </param>
			/// <param name="gimData"> This is the gimdata the values are created relativly to.</param>
			/// <param name="gimElement"> This is the GimElement that is changed according to the values of theSurface.</param>
			/// <returns>The element to add to the GimData. </returns>
//			private List<GimElement> GetSurfaceType(ModelData_Surface theSurface, GimData gimData, GimElement gimElement)
//			{
//				List<GimElement> gimElements = new List<GimElement>();
//				// documentation https://cadexchanger.com/download/sdk/doc/dev/html/sdk_data_model_geometry_topology.html#sdk_data_model_geometry_surfaces
//				switch (theSurface.Type())
//				{
//					// <Elementary>
//					case ModelData_SurfaceType.ModelData_ST_Plane:
//					case ModelData_SurfaceType.ModelData_ST_Cone:
//					case ModelData_SurfaceType.ModelData_ST_Cylinder:
//					case ModelData_SurfaceType.ModelData_ST_Sphere:
//					case ModelData_SurfaceType.ModelData_ST_Torus:
//						gimElements.Add(GetElementarySurface(ModelData_ElementarySurface.Cast(theSurface), gimElement));
//						return gimElements;
//					// <\Elementary>
//					// <Swept>
//					case ModelData_SurfaceType.ModelData_ST_LinearExtrusion:
//					case ModelData_SurfaceType.ModelData_ST_Revolution:
//						return GetSweptSurface(ModelData_SweptSurface.Cast(theSurface), gimData, gimElement);
//					// <\Swept>
//					// <Bezier>
//					case ModelData_SurfaceType.ModelData_ST_Bezier:
//					case ModelData_SurfaceType.ModelData_ST_BSpline:
//						gimElements.Add(GetBezierTypes(theSurface, gimElement));
//						return gimElements;
//					// <\Bezier>
//					case ModelData_SurfaceType.ModelData_ST_Offset:
//					case ModelData_SurfaceType.ModelData_ST_Trimmed:
//						return GetOffsetOrTrimmedSurface(theSurface, gimData, gimElement);
//					default: 
//                        throw GimLogger.Error("Unexpected surface type!");
//				}
//			}

//			/// <summary>
//			/// Creates a elementary surface (plane, cone, toroid or sphere).
//			/// </summary>
//			/// <param name="theSurface"> The ModelData_ElementarySurface to read the values from to the GimElement.</param>
//			/// <param name="gimElement"> The element to add the values from the elementary surface to. </param>
//			/// <returns>The elementary surface as a GimElement.</returns>
//			private static GimElement GetElementarySurface(ModelData_ElementarySurface theSurface, GimElement gimElement)
//			{
//				gimElement.locals.Add(NewGimPlane(theSurface.Position()));
//				switch (theSurface.Type())
//				{
//					case ModelData_SurfaceType.ModelData_ST_Plane:
//						gimElement.elementType = GimElementType.F_plane;
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Cone:
//						gimElement.elementType = GimElementType.F_cone;
//						ModelData_ConicalSurface aCone = ModelData_ConicalSurface.Cast(theSurface);
//						gimElement.elementDoubles = new double[2] { aCone.Radius(), aCone.SemiAngle() };
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Cylinder:
//						gimElement.elementType = GimElementType.F_cilinder;
//						ModelData_CylindricalSurface aCylinder = ModelData_CylindricalSurface.Cast(theSurface);
//						gimElement.elementDoubles = new double[1] { MultipleOf(aCylinder.Radius()) };
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Sphere:
//						gimElement.elementType = GimElementType.F_sphere;
//						ModelData_SphericalSurface aSphere = ModelData_SphericalSurface.Cast(theSurface);
//						gimElement.elementDoubles = new double[1] { MultipleOf(aSphere.Radius()) };
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Torus:
//						gimElement.elementType = GimElementType.F_toroid;
//						ModelData_ToroidalSurface aTorus = ModelData_ToroidalSurface.Cast(theSurface);
//						gimElement.elementDoubles = new double[2] { aTorus.MajorRadius(), aTorus.MinorRadius() };
//						break;
//					default: 
//                        throw GimLogger.Error("Expected ElementarySurface. Wrong surface type!");
//				}
//				return gimElement;
//			}

//			/// <summary>
//			/// This generates a GimElement from a ModelData_SweptSurface. (Either LinearExtrusion or Revolution)
//			/// </summary>
//			/// <param name="theSurface"> The ModelData_SweptSurface to read the values from to the GimElement.</param>
//			/// <param name="gimData"> The GimData to create the GimElement values relatively to. </param>
//			/// <param name="gimElement"> The element to add the values from the swept surface to. </param>
//			/// <returns>The swept surface as a GimElement.</returns>
//			private static List<GimElement> GetSweptSurface(ModelData_SweptSurface theSurface, GimData gimData, GimElement gimElement)
//			{
//				// Basis curve
//				ModelData_Curve aCurve = theSurface.BasisCurve();
//				List<GimElement> gimElementBasis = GetCurveType(aCurve, theSurface.UMin(), theSurface.UMax(), new GimElement());

//				// Set basis element parent and element child.
//				if (gimElementBasis[0].locals != null && gimElementBasis[0].locals.Count != 0)
//				{
//					gimElement.elements.Add((uint)gimData.Elements.Count + 1);
//					gimElementBasis[0].locals[0].parent = (uint)gimData.Elements.Count;
//				}

//				// Swept Surface
//				switch (theSurface.Type())
//				{
//					case ModelData_SurfaceType.ModelData_ST_LinearExtrusion:
//						gimElement.elementType = GimElementType.F_extrusion;
//						ModelData_SurfaceOfLinearExtrusion aLinEx = ModelData_SurfaceOfLinearExtrusion.Cast(theSurface);
//						gimElement.locals.Add(NewGimPlane(aLinEx.Direction()));
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Revolution:
//						gimElement.elementType = GimElementType.F_revolution;
//						ModelData_SurfaceOfRevolution aRevolution = ModelData_SurfaceOfRevolution.Cast(theSurface);
//						gimElement.locals.Add(NewGimPlane(aRevolution.Location(), aRevolution.Direction()));
//						break;
//					default: 
//                        throw GimLogger.Error("Expected SweptSurface. Wrong surface type!");
//				}

//				List<GimElement> gimElements = new List<GimElement>(1 + gimElementBasis.Count)
//				{
//					gimElement
//				};
//				foreach (GimElement basisElement in gimElementBasis)
//				{
//					gimElements.Add(basisElement);
//				}

//				return gimElements;
//			}

//			/// <summary>
//			/// Creates a GimElement for a Bezier type (either Bezier or BSpline)
//			/// </summary>
//			/// <param name="theSurface"> The ModelData_Surface that is either Bezier or a BSpline</param>
//			/// <param name="gimElement"> The GimElement to add the values of the Beziertype to.</param>
//			/// <returns>The GimElement with the values of the bezier type added.</returns>
//			private static GimElement GetBezierTypes(ModelData_Surface theSurface, GimElement gimElement)
//			{
//				int i; // Counter
//				ModelData_Point[] poleArray;
//				double[] weightArray;

//				switch (theSurface.Type())
//				{
//					case ModelData_SurfaceType.ModelData_ST_Bezier: // TODO: Clean this?
//						gimElement.elementType = GimElementType.F_bezier;
//						ModelData_BezierSurface aBezier = ModelData_BezierSurface.Cast(theSurface);
//						gimElement.elementInts = new int[1] { aBezier.NumberOfUPoles() };
//						// create new values for XYZ-values of poles
//						poleArray = aBezier.Poles().ToArray();
//						weightArray = aBezier.Weights().ToArray();
//						gimElement.elementDoubles = new double[poleArray.Length * 4];
//						i = 0;
//						foreach (ModelData_Point p in poleArray)
//						{
//							gimElement.elementDoubles[i] = MultipleOf(p.X()); i++;
//							gimElement.elementDoubles[i] = MultipleOf(p.Y()); i++;
//							gimElement.elementDoubles[i] = MultipleOf(p.Z()); i++;
//						}
//						foreach (double w in weightArray)
//						{
//							gimElement.elementDoubles[i] = w; i++;
//						}
//						break;
//					case ModelData_SurfaceType.ModelData_ST_BSpline:
//						gimElement = GetBSplineSurface(ModelData_BSplineSurface.Cast(theSurface), gimElement);
//						break;
//					default: 
//                        throw GimLogger.Error("Expected either Bezier or B-Spline. Wrong surface type!");
//				}
//				return gimElement;
//			}

//			/// <summary>
//			/// Adds the values of the ModelData_BSplineSurface to the GimElement.
//			/// </summary>
//			/// <param name="aBSpline"> The BSpline surface. </param>
//			/// <param name="gimElement"> The GimElement to add the values to. </param>
//			/// <returns> The GimElement with the values of the ModelData_BSplineSurface added. </returns>
//			private static GimElement GetBSplineSurface(ModelData_BSplineSurface aBSpline, GimElement gimElement)
//			{
//				ModelData_Point[] poleArray = aBSpline.Poles().ToArray();
//				double[] weightArray = aBSpline.Weights().ToArray();

//				gimElement.elementType = GimElementType.F_bspline;

//				// Create element ints array
//				gimElement.elementInts = new int[
//					  6
//					+ aBSpline.UMultiplicities().Count
//					+ aBSpline.VMultiplicities().Count];

//				// Set aBSpline constants
//				gimElement.elementInts[0] = aBSpline.Poles().Count;
//				gimElement.elementInts[1] = aBSpline.UDegree();
//				gimElement.elementInts[2] = aBSpline.VDegree();
//				gimElement.elementInts[3] = aBSpline.UKnots().Count;
//				gimElement.elementInts[4] = aBSpline.NumberOfUPoles();
//				gimElement.elementInts[5] = aBSpline.UMultiplicities().Count + aBSpline.VMultiplicities().Count;

//				// Create element doubles array
//				gimElement.elementDoubles = new double[
//					  poleArray.Length * 3
//					+ weightArray.Length
//					+ aBSpline.UKnots().Count
//					+ aBSpline.VKnots().Count];

//				int i; // Counter
//				i = 6; // number of specials
//				foreach (int d in aBSpline.UMultiplicities().ToArray())
//				{
//					gimElement.elementInts[i] = d;
//					i++;
//				}
//				foreach (int d in aBSpline.VMultiplicities().ToArray())
//				{
//					gimElement.elementInts[i] = d;
//					i++;
//				}

//				i = 0;
//				// create new values for XYZ-values of poles
//				foreach (ModelData_Point p in poleArray)
//				{
//					gimElement.elementDoubles[i] = MultipleOf(p.X()); i++;
//					gimElement.elementDoubles[i] = MultipleOf(p.Y()); i++;
//					gimElement.elementDoubles[i] = MultipleOf(p.Z()); i++;
//				}
//				foreach (double w in weightArray)
//				{
//					gimElement.elementDoubles[i] = w; i++;
//				}
//				foreach (double k in aBSpline.UKnots().ToArray())
//				{
//					gimElement.elementDoubles[i] = k; i++;
//				}
//				foreach (double k in aBSpline.VKnots().ToArray())
//				{
//					gimElement.elementDoubles[i] = k; i++;
//				}
//				return gimElement;
//			}

//			/// <summary>
//			/// Adds the values of the Offset or Trimmed surface to the GimElement.
//			/// </summary>
//			/// <param name="theSurface"> The ModelData_Surface to read the values from. </param>
//			/// <param name="gimData"> The GimData to create the GimElement values relatively to.  </param>
//			/// <param name="gimElement"> The GimElement to add the values to. </param>
//			/// <returns> The GimElement with the values of the Offset or trimmed surface added. (Also creates a basisElement.)</returns>
//			private List<GimElement> GetOffsetOrTrimmedSurface(ModelData_Surface theSurface, GimData gimData, GimElement gimElement)
//			{
//				// Basis curve
//				ModelData_Surface basisSurface;

//				// Swept Surface
//				switch (theSurface.Type())
//				{
//					case ModelData_SurfaceType.ModelData_ST_Offset:
//						gimElement.elementType = GimElementType.F_offset;
//						ModelData_OffsetSurface aOffset = ModelData_OffsetSurface.Cast(theSurface);
//						gimElement.elementDoubles = new double[1] { aOffset.Offset() };
//						basisSurface = aOffset.BasisSurface();
//						break;
//					case ModelData_SurfaceType.ModelData_ST_Trimmed:
//						gimElement.elementType = GimElementType.F_trimmed;
//						ModelData_RectangularTrimmedSurface aTrimmed = ModelData_RectangularTrimmedSurface.Cast(theSurface);
//						gimElement.elementDoubles = new double[4] { aTrimmed.UMin(), aTrimmed.UMax(), aTrimmed.VMin(), aTrimmed.VMax() };
//						basisSurface = aTrimmed.BasisSurface();
//						break;
//					default: 
//                        throw GimLogger.Error("Expected Offset Or Trimmed. Wrong surface type!");
//				}

//				gimElement.elements.Add((uint)gimData.Elements.Count + 1);
//				List<GimElement> gimElementBasis = GetSurfaceType(basisSurface, gimData, new GimElement());

//				if (gimElementBasis[0].locals.Count == 0)
//					gimElementBasis[0].locals.Add(new GimLocal());
//				gimElementBasis[0].locals[0].parent = (uint)gimData.Elements.Count;

//				List<GimElement> gimElements = new List<GimElement>(1 + gimElementBasis.Count)
//				{
//					gimElement
//				};
//				foreach (GimElement basisElement in gimElementBasis)
//				{
//					gimElements.Add(basisElement);
//				}

//				return gimElements;
//			}

//			/// <summary>
//			/// Adds the values of a curve to the GimElement.
//			/// </summary>
//			/// <param name="theCurve"> The curve to read the values from. </param>
//			/// <param name="aParamFirst"> The start parameter of the edge</param>
//			/// <param name="aParamLast"> The end parameter of the edge.</param>
//			/// <param name="gimElement"> The GimElement to add the values to.</param>
//			/// <returns> The GimElement with the values of the curve added to it.</returns>
//			private static List<GimElement> GetCurveType(ModelData_Curve theCurve, double aParamFirst, double aParamLast, GimElement gimElement, bool basis = false)
//			{
//				 List<GimElement> gimElements = new List<GimElement>();
//				if (theCurve == null)
//				{
//                    throw GimLogger.Error("Null curve");
//				}
//				switch (theCurve.Type())
//				{
//					case ModelData_CurveType.ModelData_CT_Line:
                        
//						gimElement.elementType = GimElementType.C_line;
//						ModelData_Line theLine = ModelData_Line.Cast(theCurve);

//						double newParamFirst = 0, newParamSecond =  aParamLast - aParamFirst;

//						GimLocal lineLocal = NewGimPlane(theLine.Location(), theLine.Direction());


//						if (basis)
//						{
//							newParamFirst = aParamFirst;
//							newParamSecond = aParamLast;
//						}
//                        else
//                        {

//							Vect basePoint = new Vect(lineLocal.Px, lineLocal.Py, lineLocal.Pz);
//							Vect direction = new Vect(lineLocal);

//							Vect startPoint = basePoint + aParamFirst * direction;

//							lineLocal.Px = startPoint[0];
//							lineLocal.Py = startPoint[1];
//							lineLocal.Pz = startPoint[2];
//							}


//						gimElement.locals.Add(lineLocal);

//#if DEBUG
//						if(newParamSecond == 0)
//						{
//							string debug = "found!";
//						}
//#endif

//						gimElement.elementDoubles = new double[2] { newParamFirst, newParamSecond };
//						gimElements.Add(gimElement);
//						return gimElements;
//					case ModelData_CurveType.ModelData_CT_Circle:
//					case ModelData_CurveType.ModelData_CT_Ellipse:
//					case ModelData_CurveType.ModelData_CT_Hyperbola:
//					case ModelData_CurveType.ModelData_CT_Parabola:
//						gimElement = GetConicType(ModelData_Conic.Cast(theCurve), gimElement, aParamFirst, aParamLast);
//						gimElements.Add(gimElement);
//						return gimElements;
//					case ModelData_CurveType.ModelData_CT_Bezier:
//					case ModelData_CurveType.ModelData_CT_BSpline:
//						gimElement = GetBezierCurveTypes(theCurve, gimElement, aParamFirst, aParamLast);
//						gimElements.Add(gimElement);
//						return gimElements;
//					case ModelData_CurveType.ModelData_CT_Offset:
//					case ModelData_CurveType.ModelData_CT_Trimmed:
//						return GetOffsetOrTrimmedCurve(theCurve, gimElement, aParamFirst, aParamLast);
//					default:
//						break;
//				}
//				return gimElements;
//			}

//			/// <summary>
//			/// Adds the values of a conic curve to the GimElement. (Either circle, ellipse, hyperbola or parabola.)
//			/// </summary>
//			/// <param name="theCurve"> The curve to read the values from. (Either circle, ellipse, hyperbola or parabola.) </param>
//			/// <param name="aParamFirst"> The start parameter of the edge</param>
//			/// <param name="aParamLast"> The end parameter of the edge.</param>
//			/// <param name="gimElement"> The GimElement to add the values to.</param>
//			/// <returns> The GimElement with the values of the curve added to it.</returns>
//			private static GimElement GetConicType(ModelData_Conic theCurve, GimElement gimElement, double aParamFirst, double aParamLast)
//			{
//				if (theCurve.IsPeriodic())
//				{
//					aParamLast %= theCurve.UMax();
//				}

//				GimLocal elementLocal = NewGimPlane(theCurve.Position());

//				gimElement.locals.Add(elementLocal);
//				switch (theCurve.Type())
//				{
//					case ModelData_CurveType.ModelData_CT_Circle:
//						gimElement.elementType = GimElementType.C_circle;
//						var theCircle = ModelData_Circle.Cast(theCurve);
//						// we only need the radius
//						// How do we remove this radius and use the one from the parent?
//						double newParamFirst = 0, newParamSecond = aParamLast - aParamFirst;

//						Vect v1 = new Vect(elementLocal, 0);
//						Vect v2 = new Vect(elementLocal, 1);
//						Vect v3 = (v1 ^ v2).Direction;

//						Vect newV1, newV2, newV3;

//						double pi = Math.PI;

//						newV1 = Math.Cos(aParamFirst) * v1 + Math.Sin(aParamFirst) * v2;
//						newV2 = Math.Cos(aParamFirst + pi/2) * v1 + Math.Sin(aParamFirst + pi/2) * v2;

//						newV3 = v3;

//						Matrix newMatrix = new Matrix(newV1, newV2, newV3);
//						newMatrix.ApplyToLocal(elementLocal);

//						// ensuring second param is positive
//						newParamSecond = (newParamSecond + 2 * pi) % (2 * pi);
						
//						gimElement.elementDoubles = new double[3] { theCircle.Radius(), newParamFirst, newParamSecond };


//						return gimElement;
//					case ModelData_CurveType.ModelData_CT_Ellipse:
//						gimElement.elementType = GimElementType.C_elipse;
//						var theEllipse = ModelData_Ellipse.Cast(theCurve);
//						gimElement.elementDoubles = new double[4] { theEllipse.MajorRadius(), theEllipse.MinorRadius(), aParamFirst, aParamLast }; // we only need the 2 radiuses
//						return gimElement;
//					case ModelData_CurveType.ModelData_CT_Hyperbola:
//						ModelData_Hyperbola theHyperbola = ModelData_Hyperbola.Cast(theCurve);
//						gimElement.elementType = GimElementType.C_hyperbol;
//						gimElement.elementDoubles = new double[4] { aParamFirst, aParamLast, theHyperbola.MajorRadius(), theHyperbola.MinorRadius() };
//						return gimElement;
//					case ModelData_CurveType.ModelData_CT_Parabola:
//						gimElement.elementType = GimElementType.C_parabol;
//						ModelData_Parabola theParabola = ModelData_Parabola.Cast(theCurve);
//						gimElement.elementDoubles = new double[3] { aParamFirst, aParamLast, theParabola.Focal() };
//						return gimElement;
//					default: 
//                        throw GimLogger.Error("Expected circle, ellipse, hyperbola, or parabola. Wrong curve type!");
//				}
//			}

//			private static GimElement GetBezierCurveTypes(ModelData_Curve theCurve, GimElement gimElement, double aParamFirst, double aParamLast)
//			{
//				int i; // Counter
//				ModelData_Point[] poleArray;
//				//Console.WriteLine("Check BezierCurve implementation");
//				switch (theCurve.Type())
//				{
//					case ModelData_CurveType.ModelData_CT_Bezier:
//						gimElement.elementType = GimElementType.C_bezier;
//						ModelData_BezierCurve aBezier = ModelData_BezierCurve.Cast(theCurve);
//						gimElement.elementInts = new int[1] { aBezier.NumberOfPoles() };
//						poleArray = aBezier.Poles().ToArray();
//						i = 6;
//						gimElement.elementDoubles = new double[poleArray.Length * 3 + 8];
//						foreach (ModelData_Point p in poleArray)
//						{
//							gimElement.elementDoubles[i] = MultipleOf(p.X()); i++;
//							gimElement.elementDoubles[i] = MultipleOf(p.Y()); i++;
//							gimElement.elementDoubles[i] = MultipleOf(p.Z()); i++;
//						}
//						gimElement.elementDoubles[i] = aParamFirst;
//						gimElement.elementDoubles[i + 1] = aParamLast;
//						break;
//					case ModelData_CurveType.ModelData_CT_BSpline:
//						ModelData_BSplineCurve theBSpline = ModelData_BSplineCurve.Cast(theCurve);
//						gimElement = GetBSplineCurve(theBSpline, gimElement);
//						break;
//					default: 
//                        throw GimLogger.Error("Expected either Bezier or B-Spline. Wrong curve type!");
//				}


//				return gimElement;
//			}

//			private static GimElement GetBSplineCurve(ModelData_BSplineCurve theBSpline, GimElement gimElement)
//			{
//				gimElement.elementType = GimElementType.C_bspline;

//				ModelData_Point[] poleArray = theBSpline.Poles().ToArray();
//				double[] knots = theBSpline.Knots().ToArray();

//				int[] multiplicities = theBSpline.Multiplicities().ToArray();

//				gimElement.elementInts = new int[/*knots.Length +*/ multiplicities.Length + 2];
//				gimElement.elementInts[0] = knots.Length;

//				int j = 1;

//				for (int k = 0; k < multiplicities.Length; k++)
//				{
//					gimElement.elementInts[j] = multiplicities[k]; j++;
//				}


//				gimElement.elementInts[^1] = theBSpline.Degree();

//				gimElement.elementDoubles = new double[poleArray.Length * (3 + 1) + knots.Length /*+ 2*/ + 6];

//				// 0 til 6 are vertices coordinates
//				int i = 6; // 8;

//				foreach (ModelData_Point p in poleArray)
//				{
//					gimElement.elementDoubles[i] = MultipleOf(p.X()); i++;
//					gimElement.elementDoubles[i] = MultipleOf(p.Y()); i++;
//					gimElement.elementDoubles[i] = MultipleOf(p.Z()); i++;
//				}

//				foreach (double k in knots)
//				{
//					gimElement.elementDoubles[i] = k; i++;
//				}

//				double[] weights = theBSpline.Weights().ToArray();
//				foreach (double k in weights)
//				{
//					gimElement.elementDoubles[i] = k; i++;
//				}

//				return gimElement;
//			}

//			private static List<GimElement> GetOffsetOrTrimmedCurve(ModelData_Curve theCurve, GimElement gimElement, double aParamFirst, double aParamLast)
//			{
//                switch (theCurve.Type())
//                {
//					case ModelData_CurveType.ModelData_CT_Offset: // offset curve
//                        {

//							gimElement.ElementType = GimElementType.C_offset;

//							ModelData_OffsetCurve offsetCurve = ModelData_OffsetCurve.Cast(theCurve);
//							// the offset value of the curve
//							double offsetValue = offsetCurve.Offset();
//							// offset direction is saved like a line vector is saved: zx, zy, zz
//							ModelData_Direction offsetDirection = offsetCurve.Direction();
//							Vect offsetVector = new Vect(offsetDirection.X(), offsetDirection.Y(), offsetDirection.Z());

//							GimLocal offsetLocal = new GimLocal();
//							offsetVector.ApplyToLocal(offsetLocal);

//							gimElement.ElementDoubles = new double[] { offsetValue };

//							// Basis curve of the offset, added as a child of it outside this function
//							ModelData_Curve basisCurve = offsetCurve.BasisCurve();
//							GimElement basisCurveElement = new GimElement();
							
//							basisCurveElement = GetCurveType(basisCurve, basisCurve.UMin(), basisCurve.UMax(), basisCurveElement, true)[0];

//							return new List<GimElement>() { gimElement, basisCurveElement };
//						}

//					case ModelData_CurveType.ModelData_CT_Trimmed: // trimmed curve
//                        {
//							gimElement.ElementType = GimElementType.C_trimmed;

//							ModelData_TrimmedCurve trimmedCurve = ModelData_TrimmedCurve.Cast(theCurve);
//							// trim start and trim end
//							double firstParam = trimmedCurve.UMin();
//							double secondParam = trimmedCurve.UMax();
//							gimElement.ElementDoubles = new double[] { firstParam, secondParam };

//							gimElement.Locals.Add(new GimLocal());

//							// Basis curve of the trimmed, added as a child of it outside this function
//							ModelData_Curve basisCurve = trimmedCurve.BasisCurve();
//							GimElement basisCurveElement = new GimElement();

//							basisCurveElement = GetCurveType(basisCurve, basisCurve.UMin(), basisCurve.UMax(), basisCurveElement, true)[0];

//							return new List<GimElement>() { gimElement, basisCurveElement };
//						}
//                }
				
//                throw GimLogger.Error("Check OffsetOrTreimmedCurve implementation");
//			}

//			private static List<GimElement> GetCurve2DType(ModelData_Curve2d theCurve, double aParamFirst, double aParamLast, GimElement gimElement)
//			{
//				List<GimElement> gimElements = new List<GimElement>();

//				switch (theCurve.Type())
//				{
//					case ModelData_CurveType.ModelData_CT_Line:
//						gimElement.elementType = GimElementType.C_line2D;
//						ModelData_Line2d theLine = ModelData_Line2d.Cast(theCurve);
//						theLine.Direction().X();
//						theLine.Direction().Y();

//						theLine.Location().X();
//						theLine.Location().Y();

//						gimElement.locals.Add(NewGimPlane(theLine.Location(), theLine.Direction()));
//						gimElement.elementDoubles = new double[8] { 0, 0, 0, 0, 0, 0, aParamFirst, aParamLast };
//						break;
//					case ModelData_CurveType.ModelData_CT_Circle:
//					case ModelData_CurveType.ModelData_CT_Ellipse:
//					case ModelData_CurveType.ModelData_CT_Hyperbola:
//					case ModelData_CurveType.ModelData_CT_Parabola:
//						gimElement = GetConic2DType(ModelData_Conic2d.Cast(theCurve), gimElement, aParamFirst, aParamLast);
//						break;
//					case ModelData_CurveType.ModelData_CT_Bezier:
//					case ModelData_CurveType.ModelData_CT_BSpline:
//						gimElement = GetBezierCurve2DTypes(theCurve, gimElement, aParamFirst, aParamLast);
//						break;
//					case ModelData_CurveType.ModelData_CT_Offset:
//					case ModelData_CurveType.ModelData_CT_Trimmed:
//						gimElements = GetOffsetOrTrimmedCurve2D();
//						break;
//					default:
//						break;
//				}

//				if (gimElements.Count == 0)
//				{
//					if (gimElement == null)
//					{
//						GimLogger.Warn("Null element in " + nameof(GetCurve2DType));
//					}
//					gimElements.Add(gimElement);
//				}

//				return gimElements;
//			}

//			private static GimElement GetConic2DType(ModelData_Conic2d theCurve, GimElement gimElement, double aParamFirst, double aParamLast)
//			{
//				gimElement.locals.Add(NewGimPlane(theCurve.Position()));
//				switch (theCurve.Type())
//				{
//					case ModelData_CurveType.ModelData_CT_Circle:
//						gimElement.elementType = GimElementType.C_circle2D;
//						ModelData_Circle2d theCircle = ModelData_Circle2d.Cast(theCurve);
//						// we only need the radius
//						// How do we remove this radius and use the one from the parent?
//						gimElement.elementDoubles = new double[9] { 0, 0, 0, 0, 0, 0, theCircle.Radius(), aParamFirst, aParamFirst };
//						gimElement.locals.Add(NewGimPlane(theCircle.Position()));
//						return gimElement;
//					case ModelData_CurveType.ModelData_CT_Ellipse:
//						gimElement.elementType = GimElementType.C_elipse2D;
//						ModelData_Ellipse2d theEllipse = ModelData_Ellipse2d.Cast(theCurve);
//						gimElement.elementDoubles = new double[10] { 0, 0, 0, 0, 0, 0, theEllipse.MajorRadius(), theEllipse.MinorRadius(), aParamFirst, aParamLast }; // we only need the 2 radiuses
//						gimElement.locals.Add(NewGimPlane(theEllipse.Position()));
//						return gimElement;
//					case ModelData_CurveType.ModelData_CT_Hyperbola:
//						ModelData_Hyperbola2d theHyperbola = ModelData_Hyperbola2d.Cast(theCurve);
//						gimElement.elementType = GimElementType.C_hyperbol2D;
//						gimElement.elementDoubles = new double[10] { 0, 0, 0, 0, 0, 0, aParamFirst, aParamLast, theHyperbola.MajorRadius(), theHyperbola.MinorRadius() };
//						return gimElement;
//					case ModelData_CurveType.ModelData_CT_Parabola:
//						gimElement.elementType = GimElementType.C_parabol2D;
//						ModelData_Parabola2d theParabola = ModelData_Parabola2d.Cast(theCurve);
//						gimElement.elementDoubles = new double[9] { 0, 0, 0, 0, 0, 0, aParamFirst, aParamLast, theParabola.Focal() };
//						return gimElement;
//					default: 
//                        throw GimLogger.Error("Expected circle, ellipse, hyperbola, or parabola. Wrong curve type!");
//				}
//			}

//			private static GimElement GetBezierCurve2DTypes(ModelData_Curve2d theCurve, GimElement gimElement, double aParamFirst, double aParamLast)
//			{
//				int i; // Counter
//				ModelData_Point2d[] poleArray;

//				switch (theCurve.Type())
//				{
//					case ModelData_CurveType.ModelData_CT_Bezier:
//						gimElement.elementType = GimElementType.C_bezier2D;
//						ModelData_BezierCurve2d aBezier = ModelData_BezierCurve2d.Cast(theCurve);
//						gimElement.elementInts = new int[1] { aBezier.NumberOfPoles() };
//						poleArray = aBezier.Poles().ToArray();

//						i = 6;
//						gimElement.elementDoubles = new double[poleArray.Length * 3 + 2 + 6]; //TODO: change * 3 to * 2, test this
//						foreach (ModelData_Point2d p in poleArray)
//						{
//							gimElement.elementDoubles[i] = MultipleOf(p.X()); i++;
//							gimElement.elementDoubles[i] = MultipleOf(p.Y()); i++;
//						}
//						gimElement.elementDoubles[i] = aParamFirst;
//						gimElement.elementDoubles[i + 1] = aParamLast;

//						return gimElement;
//					case ModelData_CurveType.ModelData_CT_BSpline:
//						return GetBSpline2D(theCurve, gimElement, aParamFirst, aParamLast);
//					default: 
//                        throw GimLogger.Error("Expected either Bezier or B-Spline. Wrong curve type!");
//				}
//			}

//			internal static GimElement GetBSpline2D(ModelData_Curve2d theCurve, GimElement gimElement, double aParamFirst, double aParamLast)
//			{
//				//int i; // Counter
//				gimElement.elementType = GimElementType.C_bspline2D;

//				ModelData_BSplineCurve2d theBSpline = ModelData_BSplineCurve2d.Cast(theCurve);
//				ModelData_Point2d[] poleArray = theBSpline.Poles().ToArray();
//				ModelData_DoubleList knots = theBSpline.Knots();
//				ModelData_IntList multiplicities = theBSpline.Multiplicities();

//				// store Knots.count, degree and, all multiplicities
//				gimElement.elementInts = new int[multiplicities.Count + 2];
//				// Store the Knots, poles, First parameter and second parameter, and the 6 coördinates needed for 2d eges.
//				gimElement.elementDoubles = new double[knots.Count + poleArray.Length * 2 + 2 + 6];
//				// If we know the amount of knots we can calculate the amount of poles.
//				gimElement.elementInts[0] = knots.Count;
//				// Store the degree of the Bspline
//				gimElement.elementInts[1] = theBSpline.Degree();

//				// Set first and last parameter.
//				gimElement.elementDoubles[6] = aParamFirst;
//				gimElement.elementDoubles[7] = aParamLast;

//				// Add multiplicites to the elment ints.
//				for (int i = 0; i < multiplicities.Count; i++)
//				{
//					gimElement.elementInts[i + 2] = multiplicities[i];
//				}

//				// Add knots to the element doubles.
//				for (int i = 0; i < knots.Count; i++)
//				{
//					gimElement.elementDoubles[i + 8] = knots[i];
//				}
//				// Add poles to the element doubles.
//				int counter = 8 + knots.Count;
//				for (int i = 0; i < poleArray.Length; i++)
//				{
//					gimElement.elementDoubles[counter] = poleArray[i].X();
//					counter++;
//					gimElement.elementDoubles[counter] = poleArray[i].Y();
//					counter++;
//				}

//				return gimElement;
//			}

//			private static List<GimElement> GetOffsetOrTrimmedCurve2D()
//			{
//                throw GimLogger.Error("Error: Check GetOffsetOrTrimmedCurve2D implementation");
//			}
		}
	}
}
