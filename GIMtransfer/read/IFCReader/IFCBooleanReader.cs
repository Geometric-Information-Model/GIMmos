using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using GeometryGym.Ifc;
using GIMcompress;

namespace GIMtransfer.read.IFCReader
{
    public class IFCBooleanReader
    {

        public static bool IFCBooleanClipping(IfcRepresentationItem item, GimData gimData, int solidBodyElementIndex, Vect generalLocationGIM, Matrix transform, bool hasHoles,
            List<List<Vect>> holes, int baseAxis)
        {
            bool removed = false;
            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);
            IfcBooleanClippingResult clipping = (IfcBooleanClippingResult)item;
            GimElement gimBooleanElement = new() { ElementType = GimElementType.Gim_Boolean };
            int booleanElementIndex = gimData.elements.Count;
            gimData.elements.Add(gimBooleanElement);
            gimData.AddChildToParent((uint)booleanElementIndex, (uint)shellElementIndex);
            if (clipping.Operator.ToString() == "DIFFERENCE")
            {
                gimBooleanElement.elementBools = new int[1];
                gimBooleanElement.elementBools[0] = 2;
            }
            else
            {
                string debug = "debug";
            }
            removed = CreateGimBoolean(clipping, gimData, booleanElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
            return removed;

        }

        public static bool CreateGimBoolean(IfcBooleanClippingResult clipping, GimData gimData, int parentIndex, Vect generalLocationGIM, Matrix transform, bool hasHoles,
            List<List<Vect>> holes, int baseAxis)
        {

            bool removed = false;
           
            var firstOperand = clipping.FirstOperand;
            if (firstOperand is IfcBooleanClippingResult)
            {
                GimElement gimBooleanElement = new() { ElementType = GimElementType.Gim_Boolean };
                int booleanElementIndex = gimData.elements.Count;
                gimData.elements.Add(gimBooleanElement);
                gimData.AddChildToParent((uint)booleanElementIndex, (uint)parentIndex);
                if (clipping.Operator.ToString() == "DIFFERENCE")
                {
                    gimBooleanElement.elementBools = new int[1];
                    gimBooleanElement.elementBools[0] = 2;
                }
                else
                {
                    string debug = "debug";
                }
                IfcBooleanClippingResult firstOperandClipping = (IfcBooleanClippingResult)firstOperand;
                removed = CreateGimBoolean(firstOperandClipping, gimData, booleanElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
                if (removed)
                    return removed;
                if (clipping.SecondOperand is IfcPolygonalBoundedHalfSpace)
                {
                    IfcPolygonalBoundedHalfSpace secondOperand = (IfcPolygonalBoundedHalfSpace)clipping.SecondOperand;
                    //CreateOpenPlaneFromPolyBoundedHalfSpace(secondOperand, transform, generalLocationGIM, parentIndex, gimData);
                    removed = CreatePlanesFromPolyHalfSpace(secondOperand, transform, generalLocationGIM, parentIndex, gimData);
                    if(removed) return removed;
                    //CreatePlaneFromPolyBoundedHalfSpace(secondOperand, transform, generalLocationGIM, parentIndex, gimData);

                }
                else if(clipping.SecondOperand is IfcHalfSpaceSolid)
                {
                    IfcHalfSpaceSolid halfSpace = (IfcHalfSpaceSolid)clipping.SecondOperand;
                    List<Vect> boundingBox = new();
                    CreatePlaneFromHalfSpaceSolid(halfSpace, boundingBox, generalLocationGIM, gimData, parentIndex);
                    string debug = "debug";
                }
                else
                {
                    string debug = "debug";
                }




            }
            if(firstOperand is IfcExtrudedAreaSolid)
            {
                List<Vect> pointsforPlane = new();
                GimElement partElement = new() { elementType = GimElementType.D_part };
                int partIndex = gimData.elements.Count;

                gimData.elements.Add(partElement);

                gimData.AddChildToParent((uint)partIndex, (uint)parentIndex);
                GimElement bRepElement = new() { elementType = GimElementType.R_BRep };
                int bRepElementIndex = gimData.Elements.Count;
                gimData.elements.Add(bRepElement);

                gimData.AddChildToParent((uint)bRepElementIndex, (uint)partIndex);
                GimElement solidBodyElement = new() { elementType = GimElementType.B_solid };
                int solidBodyElementIndex = gimData.elements.Count;
                gimData.elements.Add(solidBodyElement);
                gimData.AddChildToParent((uint)solidBodyElementIndex, (uint)bRepElementIndex);
                ;
                GimElement shellElement = new() { elementType = GimElementType.shell };
                int shellElementIndex = gimData.elements.Count;
                gimData.elements.Add(shellElement);

                gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);
                var extrudedArea = (IfcExtrudedAreaSolid)firstOperand;
                double depth = extrudedArea.Depth;
                var extrudedDirection = extrudedArea.ExtrudedDirection;
                Vect extrudedDirectionGIM = new(extrudedDirection.DirectionRatioX, extrudedDirection.DirectionRatioY, extrudedDirection.DirectionRatioZ);
                var sweptArea = extrudedArea.SweptArea;
                var positionIFC = extrudedArea.Position;

                var localTransform = IFCReaderUtilities.GetObjectTransformation(positionIFC);
                var finalTransform = localTransform * transform;
                
                //finalTransform.ApplyToLocal(partElement.Locals[0]);
                var temp = IFCReaderUtilities.GetLocalObjectPlacement(positionIFC);
                Vect localPosition = new(generalLocationGIM[0] + temp[0], generalLocationGIM[1] + temp[1], generalLocationGIM[2] + temp[2]);
                //generalLocationGIM[0] += localPosition[0];
                //generalLocationGIM[1] += localPosition[1];
                //generalLocationGIM[2] += localPosition[2];
                //localTransform.ApplyToLocal(partElement.Locals[0]);
                if (sweptArea is IfcArbitraryClosedProfileDef)
                {
                    var closedProfileDef = (IfcArbitraryClosedProfileDef)sweptArea;
                    //TODO: Make it so that this function always gets a list of Vect instead and create a function to create said list of points for all cases
                        pointsforPlane = IFCReaderUtilities.GetPointsFromExtrusion(closedProfileDef, depth, extrudedDirectionGIM);
                    if (!hasHoles)
                        removed = IFCExtrusionReader.GetPlanesDictionaryFromPoints(closedProfileDef, depth, extrudedDirectionGIM, gimData, shellElementIndex, localPosition, finalTransform, localTransform);
                    else
                        try
                        {
                            removed = IFCReadHoles.ExtractPointsFromProfileDefAndCreatePlane(closedProfileDef, holes, depth, extrudedDirectionGIM, gimData, shellElementIndex, localPosition, finalTransform, baseAxis);
                        }
                        catch
                        {
                            removed = true;
                        }
                    if (removed)
                        return removed;


                }
                else if (sweptArea is IfcRectangleProfileDef)
                {

                    var rectangleDef = (IfcRectangleProfileDef)sweptArea;
                    var innerTransform = IFCReaderUtilities.Transform2DCoordsInto3D(rectangleDef.Position);
                    Matrix identity = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                    if (innerTransform != identity && localTransform != identity)
                    {
                        innerTransform = innerTransform * localTransform;
                    }
                    var resultingTransform = innerTransform * finalTransform;
                    //var position2DIFC = rectangleDef.Position.Location;
                    //Vect position2DGIM = new(position2DIFC.CoordinateX, position2DIFC.CoordinateY, 0);
                    //localPosition = localPosition + position2DGIM;
                    List<Vect> points = IFCReaderUtilities.CreateListOfPointsFromRectangle(rectangleDef, innerTransform);
                    if (!hasHoles)
                        IFCExtrusionReader.GetPlanesDictionaryFromPoints(points, depth, extrudedDirectionGIM, gimData, shellElementIndex, localPosition, resultingTransform);
                    else
                        IFCReadHoles.ExtractPointsAndCreatePlane(points, holes, depth, extrudedDirectionGIM, localPosition, resultingTransform, gimData, shellElementIndex, baseAxis);



                }
                else if (sweptArea is IfcIShapeProfileDef)
                {
                    IfcIShapeProfileDef iShape = (IfcIShapeProfileDef)sweptArea;
                    double filletRadius = iShape.FilletRadius;
                    double flangeThickness = iShape.FlangeThickness;
                    double overallDepth = iShape.OverallDepth;
                    double overallWidth = iShape.OverallWidth;
                    double webThickness = iShape.WebThickness;
                    var centerIFC = iShape.Position.Location;
                    Vect centerGIM = new(centerIFC.CoordinateX, centerIFC.CoordinateY, 0);
                    var iShapePoints = IFCReaderUtilities.GetPointsFromIShape(centerGIM, filletRadius, flangeThickness, overallDepth, overallWidth, webThickness);
                    var innerTransform = IFCReaderUtilities.Transform2DCoordsInto3D(iShape.Position);
                    var resultingTransform = innerTransform * finalTransform;
                    IFCExtrusionReader.GetPlanesDictionaryFromPoints(iShapePoints, depth, extrudedDirectionGIM, gimData, shellElementIndex, localPosition, resultingTransform);
                    string debug = "debug";
                }
                else if (sweptArea is IfcCircleProfileDef)
                {

                        var circleDef = (IfcCircleProfileDef)sweptArea;
                    IFCExtrusionReader.CreateCylinderFromExtrusion(circleDef, depth, extrudedDirectionGIM, gimData, shellElementIndex, generalLocationGIM, finalTransform);

                    
                    
                }
                else
                {
                    string debug = "debug";
                }
                if (clipping.SecondOperand is IfcPolygonalBoundedHalfSpace)
                {
                    IfcPolygonalBoundedHalfSpace secondOperand = (IfcPolygonalBoundedHalfSpace)clipping.SecondOperand;
                    removed = CreatePlanesFromPolyHalfSpace(secondOperand, transform, localPosition, parentIndex, gimData);
                    if (removed) return removed;
                    //CreatePlaneFromPolyBoundedHalfSpace(secondOperand, transform, generalLocationGIM, parentIndex, gimData);
                    //CreateOpenPlaneFromPolyBoundedHalfSpace(secondOperand, transform, generalLocationGIM, parentIndex, gimData);
                }
                else if (clipping.SecondOperand is IfcHalfSpaceSolid)
                {
                    IfcHalfSpaceSolid halfSpace = (IfcHalfSpaceSolid)clipping.SecondOperand;
                    var boundingBox = IFCReaderUtilities.Get2DBoundingBox(pointsforPlane, transform);
                    CreatePlaneFromHalfSpaceSolid(halfSpace, boundingBox , localPosition, gimData,parentIndex);
                    string debug = "debug";
                }
                else
                {
                    string debug = "debug";
                }

            }



            return removed;
        }


        public static void CreatePlaneFromHalfSpaceSolid(IfcHalfSpaceSolid halfSpace, List<Vect> boundingBox, Vect generalLocation, GimData gimData, int parentID)
        {
            if (!halfSpace.AgreementFlag)
            {

            }
            GimElement partElement = new() { elementType = GimElementType.D_part };
            int partIndex = gimData.elements.Count;

            gimData.elements.Add(partElement);

            gimData.AddChildToParent((uint)partIndex, (uint)parentID);
            GimElement bRepElement = new() { elementType = GimElementType.R_BRep };
            int bRepElementIndex = gimData.Elements.Count;
            gimData.elements.Add(bRepElement);

            gimData.AddChildToParent((uint)bRepElementIndex, (uint)partIndex);
            GimElement solidBodyElement = new() { elementType = GimElementType.B_solid };
            int solidBodyElementIndex = gimData.elements.Count;
            gimData.elements.Add(solidBodyElement);
            gimData.AddChildToParent((uint)solidBodyElementIndex, (uint)bRepElementIndex);
            ;
            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);

            IfcPlane planeIFC = (IfcPlane)halfSpace.BaseSurface;
            var locationIFC = planeIFC.Position.Location;
            var zAxisIFC = planeIFC.Position.Axis;
            var xAxisIFC = planeIFC.Position.RefDirection;
            Vect locationGIM = new(locationIFC.CoordinateX, locationIFC.CoordinateY, locationIFC.CoordinateZ);
            Vect zAxisGIM = new(zAxisIFC.DirectionRatioX, zAxisIFC.DirectionRatioY, zAxisIFC.DirectionRatioZ);
            Vect xAxisGIM = new(xAxisIFC.DirectionRatioX, xAxisIFC.DirectionRatioY, xAxisIFC.DirectionRatioZ);
            Vect yAxisGIM = zAxisGIM ^ xAxisGIM;
            Matrix planeDirectionGIM = new(xAxisGIM, yAxisGIM, zAxisGIM);
            List<Vect> borders = IFCReaderUtilities.CreatePointsForHalfSpace(locationGIM, planeDirectionGIM);
            Matrix identity = new(new Vect(1,0,0), new Vect(0, 1, 0), new Vect(0, 0, 1));
            Vect extrusionDir = -zAxisGIM;
            if (!halfSpace.AgreementFlag)
                extrusionDir = -extrusionDir;
            IFCExtrusionReader.GetPlanesDictionaryFromPoints(borders, 1000, extrusionDir, gimData, shellElementIndex, generalLocation, identity);
            //IFCReadElements.CreatePlane(borders, gimData, shellElementIndex, generalLocation, identity);
           // GimBooleanConvertion.TransformBooleanToElement(gimData);

        }




        public static void CreatePlaneFromPolyBoundedHalfSpace(IfcPolygonalBoundedHalfSpace polygonalBoundedHalfSpace, Matrix transform, Vect globalPosition, int parentID, GimData gimData)
        {
            GimElement partElement = new() { elementType = GimElementType.D_part };
            int partIndex = gimData.elements.Count;

            gimData.elements.Add(partElement);

            gimData.AddChildToParent((uint)partIndex, (uint)parentID);
            GimElement bRepElement = new() { elementType = GimElementType.R_BRep };
            int bRepElementIndex = gimData.Elements.Count;
            gimData.elements.Add(bRepElement);

            gimData.AddChildToParent((uint)bRepElementIndex, (uint)partIndex);
            GimElement solidBodyElement = new() { elementType = GimElementType.B_solid };
            int solidBodyElementIndex = gimData.elements.Count;
            gimData.elements.Add(solidBodyElement);
            gimData.AddChildToParent((uint)solidBodyElementIndex, (uint)bRepElementIndex);
            ;
            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);
            IfcPlane ifcPlane = (IfcPlane)polygonalBoundedHalfSpace.BaseSurface;
            var zAxisIfcPlane = ifcPlane.Position.Axis;
            var xAxisIfcPlane = ifcPlane.Position.RefDirection;
            Vect zAxisGIMPlane = new(zAxisIfcPlane.DirectionRatioX, zAxisIfcPlane.DirectionRatioY, zAxisIfcPlane.DirectionRatioZ);
            Vect xAxisGIMPlane = new(xAxisIfcPlane.DirectionRatioX, xAxisIfcPlane.DirectionRatioY, xAxisIfcPlane.DirectionRatioZ);
            Vect yAxisGIMPlane = zAxisGIMPlane ^ xAxisGIMPlane;
            Matrix cutOrientation = new(xAxisGIMPlane, yAxisGIMPlane, zAxisGIMPlane);
            var testLoc = ifcPlane.Position.Location;
            Vect testLocGIM = new(testLoc.CoordinateX, testLoc.CoordinateY, testLoc.CoordinateZ);
            
           // testLocGIM = testLocGIM + globalPosition;
            //cutOrientation = cutOrientation * transform;
            IfcPolyline polyLinesIFC = (IfcPolyline)polygonalBoundedHalfSpace.PolygonalBoundary;
            var polyLinesGIM = IFCReaderUtilities.CreatePointsFromPolyBound(polyLinesIFC);
            
                var cutPosition = cutOrientation * globalPosition;
            IFCReadElements.CreatePlane(polyLinesGIM, gimData, shellElementIndex, globalPosition, cutOrientation);

            //Too lazy to redo a whole function for creating the plane, so i'm just going to do this for getting the id
            //of plane and then setting the proper orientation depending on the "agreement flag" will have to fix it later
            int lineID = gimData.Elements.Count-1;
            GimElement lineElement = gimData.Elements[lineID];
            int wireID = (int)lineElement.Locals[0].Parent;
            GimElement wireElement = gimData.Elements[wireID];
            int planeID = (int)wireElement.Locals[0].Parent;
            GimElement planeElement = gimData.Elements[planeID];
            if (!polygonalBoundedHalfSpace.AgreementFlag)
                planeElement.Orientation = false;
            //Again this is the wrong way to do this, has to be redone properly
           
        }


        public static bool CreatePlanesFromPolyHalfSpace(IfcPolygonalBoundedHalfSpace polygonalBoundedHalfSpace, Matrix transform, Vect globalPosition, int parentID, GimData gimData)
        {
            bool removed = false;
            List<Vect> points = new List<Vect>();

            GimElement partElement = new() { elementType = GimElementType.D_part };
            int partIndex = gimData.elements.Count;

            gimData.elements.Add(partElement);

            gimData.AddChildToParent((uint)partIndex, (uint)parentID);
            GimElement bRepElement = new() { elementType = GimElementType.R_BRep };
            int bRepElementIndex = gimData.Elements.Count;
            gimData.elements.Add(bRepElement);

            gimData.AddChildToParent((uint)bRepElementIndex, (uint)partIndex);
            GimElement solidBodyElement = new() { elementType = GimElementType.B_solid };
            int solidBodyElementIndex = gimData.elements.Count;
            gimData.elements.Add(solidBodyElement);
            gimData.AddChildToParent((uint)solidBodyElementIndex, (uint)bRepElementIndex);
            ;
            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);


            IfcPlane ifcPlane = (IfcPlane)polygonalBoundedHalfSpace.BaseSurface;
            var zAxisIfcPlane = ifcPlane.Position.Axis;
            var xAxisIfcPlane = ifcPlane.Position.RefDirection;
            Vect zAxisGIMPlane = new(0, 0, 1);
            Vect xAxisGIMPlane = new(1, 0, 0);
            if (zAxisIfcPlane != null)
                zAxisGIMPlane = new(zAxisIfcPlane.DirectionRatioX, zAxisIfcPlane.DirectionRatioY, zAxisIfcPlane.DirectionRatioZ);
            if (xAxisIfcPlane != null)
                xAxisGIMPlane = new(xAxisIfcPlane.DirectionRatioX, xAxisIfcPlane.DirectionRatioY, xAxisIfcPlane.DirectionRatioZ);
            IfcAxis2Placement3D positionIFC = polygonalBoundedHalfSpace.Position;
            Vect yAxisGIMPlane = zAxisGIMPlane ^ xAxisGIMPlane;
            Matrix cutOrientation = new(xAxisGIMPlane, yAxisGIMPlane, zAxisGIMPlane);
            var testLoc = ifcPlane.Position.Location;
            Vect testLocGIM = new(testLoc.CoordinateX, testLoc.CoordinateY, testLoc.CoordinateZ);
            if(polygonalBoundedHalfSpace.PolygonalBoundary is not IfcPolyline)
            {
                removed = true;
                return removed;
            }
            IfcPolyline polyLinesIFC = (IfcPolyline)polygonalBoundedHalfSpace.PolygonalBoundary;
            var polyLinesGIM = IFCReaderUtilities.CreatePointsFromPolyBound(polyLinesIFC);
            //for(int i = 0; i < polyLinesGIM.Count;i++)
            //{
            //    var currentPoint = polyLinesGIM[i] * transform;
            //    polyLinesGIM[i] = currentPoint;
            //}
            Vect normal = cutOrientation[2];
            Vect extrudedDir = new(0, 0, 1);
            IfcDirection dir = positionIFC.Axis;
            if (dir != null)
                extrudedDir = new(dir.DirectionRatioX, dir.DirectionRatioY, dir.DirectionRatioZ);
            foreach (var linePoint in polyLinesGIM)
            {
                
                var diff = testLocGIM - linePoint;
                double numerator = normal * diff;
                double denominator = normal * extrudedDir;
                if (Math.Abs(denominator) < 1e-6)
                    continue;
                double t = numerator / denominator;
                //if (t < 0)
                //    continue;
                var intersection = linePoint + t * extrudedDir;
                points.Add(intersection);
            }
            
            
            IFCExtrusionReader.GetPlanesDictionaryFromPoints(points, 500,extrudedDir, gimData, shellElementIndex, globalPosition, transform);

            return removed;
            //IFCReadElements.CreatePlane(points, gimData, shellElementIndex, globalPosition, transform);

        }


        public static void CreateOpenPlaneFromPolyBoundedHalfSpace(IfcPolygonalBoundedHalfSpace halfSpace, Matrix transform, Vect globalPosition, int parentID, GimData gimData)
        {
            if (!halfSpace.AgreementFlag)
            {
                
            }
                GimElement partElement = new() { elementType = GimElementType.D_part };
            int partIndex = gimData.elements.Count;

            gimData.elements.Add(partElement);

            gimData.AddChildToParent((uint)partIndex, (uint)parentID);
            GimElement bRepElement = new() { elementType = GimElementType.R_BRep };
            int bRepElementIndex = gimData.Elements.Count;
            gimData.elements.Add(bRepElement);

            gimData.AddChildToParent((uint)bRepElementIndex, (uint)partIndex);
            GimElement solidBodyElement = new() { elementType = GimElementType.B_solid };
            int solidBodyElementIndex = gimData.elements.Count;
            gimData.elements.Add(solidBodyElement);
            gimData.AddChildToParent((uint)solidBodyElementIndex, (uint)bRepElementIndex);
            ;
            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);

            IfcPlane planeIFC = (IfcPlane)halfSpace.BaseSurface;
            var locationIFC = planeIFC.Position.Location;
            var zAxisIFC = planeIFC.Position.Axis;
            var xAxisIFC = planeIFC.Position.RefDirection;
            Vect locationGIM = new(locationIFC.CoordinateX, locationIFC.CoordinateY, locationIFC.CoordinateZ);
            Vect zAxisGIM = new(zAxisIFC.DirectionRatioX, zAxisIFC.DirectionRatioY, zAxisIFC.DirectionRatioZ);
            Vect xAxisGIM = new(xAxisIFC.DirectionRatioX, xAxisIFC.DirectionRatioY, xAxisIFC.DirectionRatioZ);
            Vect yAxisGIM = zAxisGIM ^ xAxisGIM;
            Matrix planeDirectionGIM = new(xAxisGIM, yAxisGIM, zAxisGIM);
            List<Vect> borders = IFCReaderUtilities.CreatePointsForHalfSpace(locationGIM, planeDirectionGIM);
            Matrix identity = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
            Vect extrusionDir = new(0, 0, 1);
            IFCExtrusionReader.GetPlanesDictionaryFromPoints(borders, 200, extrusionDir, gimData, shellElementIndex, globalPosition, transform);
            //IFCReadElements.CreatePlane(borders, gimData, shellElementIndex, generalLocation, identity);
            //GimBooleanConvertion.TransformBooleanToElement(gimData);

        }


    }
}
