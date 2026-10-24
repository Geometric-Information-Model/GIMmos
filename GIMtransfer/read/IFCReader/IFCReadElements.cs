using GeometryGym.Ifc;
using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static SharpGLTF.Scenes.LightBuilder;

namespace GIMtransfer.read.IFCReader
{
    public class IFCReadElements
    {

        public IFCReadElements() { }

        public static GimData ReadIFCToGimData(string path)
        {
            DatabaseIfc db = new DatabaseIfc(path);
            GimData gimData = new GimData();
            gimData.CreateRootIfNotExists();
            GimElement folderElement = new() { elementType = GimElementType.D_folder };
            gimData.elements.Add(folderElement);
            gimData.AddChildToParent(1, 0);
            List<int> uniqueIFCID = new();
            var test123 = db.ToList();
           
            uint childFolderElementIndex = 0;
            foreach (var element in db.OfType<IfcElement>())
            {

                //if (element.GlobalId != "00YOps6Br6NOi0c5LTe35$")
                //{
                //    continue;
                //}
                if (element.Representation != null)
                {
                   
                    //if (element.ContainedInStructure != null)
                    {

                        bool hasHoles = false;
                        int baseAxis = -1;
                        List<List<Vect>> holes = new();
                        foreach (var representation in element.Representation.Representations)
                        {
                            if (representation is IfcShapeRepresentation shapeRep)
                            {
                                if (shapeRep.RepresentationType != "Brep" && shapeRep.RepresentationType != "MappedRepresentation" && shapeRep.RepresentationType != "SweptSolid"  && shapeRep.RepresentationType !="Clipping")
                                    
                                    continue;
                                if (shapeRep.RepresentationIdentifier == "Axis")
                                    continue;
                                if (element.ObjectType == "Opening" || element.StepClassName == "IfcOpeningElement")
                                    continue;

                                if (element.HasOpenings.Count > 0)
                                {
                                    holes = IFCReadHoles.PrepUpHoles(element);
                                    if (holes != null)
                                    {
                                        if (holes.Count > 0)
                                        {
                                            hasHoles = true;
                                            //-1 if none, 0 if X, 1 if Y, 2 if Z
                                            baseAxis = IFCReaderUtilities.GetConstantAxis(holes[0]);
                                        }
                                    }
                                }



                                if (shapeRep.Items.Count == 1)
                                {
                                    IfcLocalPlacement objectPlacement = (IfcLocalPlacement)element.ObjectPlacement;
                                    Vect generalLocationGIM = new Vect(0, 0, 0);
                                    Matrix transformLocation = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                                    IFCReaderUtilities.GetGlobalObjectPlacement(ref generalLocationGIM, objectPlacement, ref transformLocation);
                                    GimElement partElement = new() { elementType = GimElementType.D_part };
                                    int partIndex = gimData.elements.Count;
 
                                    gimData.elements.Add(partElement);
                                    if(partIndex == 5939)
                                    {

                                    }
                                    gimData.AddChildToParent((uint)partIndex, 1);
                                    var partRelativePlacement = (IfcAxis2Placement3D)objectPlacement.RelativePlacement;
                                    Vect temp1 = new(0.766, -0.643, 0);
                                    Vect temp2 = new(0.643, 0.766, 0);
                                    Vect temp3 = new(0, 0, 1);
                                    Matrix temp4 = new(temp1, temp2, temp3);
                                    Matrix transform = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                                    IFCReaderUtilities.GetObjectTransformation(objectPlacement, ref transform);
                                    GimElement bRepElement = new() { elementType = GimElementType.R_BRep };
                                    int bRepElementIndex = gimData.Elements.Count;
                                    gimData.elements.Add(bRepElement);

                                    gimData.AddChildToParent((uint)bRepElementIndex, (uint)partIndex);
                                    GimElement solidBodyElement = new() { elementType = GimElementType.B_solid };
                                    int solidBodyElementIndex = gimData.elements.Count;
                                    gimData.elements.Add(solidBodyElement);
                                    gimData.AddChildToParent((uint)solidBodyElementIndex, (uint)bRepElementIndex);
                                    bool found = false;
                                    bool removed = false;
                                    foreach (var item in shapeRep.Items)
                                    {


                                        if (item is IfcMappedItem)
                                        {
                                            removed = IFC2Reader.MappedItemReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
                                            if (removed)
                                                break;
                                        }

                                        else if (item is IfcFacetedBrep)
                                        {
                                            removed = IFC2Reader.FacetedBrepReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform);
                                            if (removed)
                                                break;
                                        }
                                        else if (item is IfcShellBasedSurfaceModel)
                                        {

                                            IFC2Reader.ShellBasedSurfaceModelReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform);
                                        }
                                        else if (item is IfcAdvancedBrep)
                                        {

                                        }
                                        else if (item is IfcExtrudedAreaSolid)
                                        {
                                            removed = IFC2Reader.ExtrudedFaceReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
                                            if (removed)
                                                break;
                                        }
                                        else if(item is IfcBooleanClippingResult)
                                        {
                                           removed = IFCBooleanReader.IFCBooleanClipping(item, gimData, solidBodyElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
                                            if (removed)
                                                break;
                                        }
                                        else
                                        {
                                            //IFCSURVACECURVESWEPTSOLID
                                            string ack = "debug";
                                        }
                                        


                                    }
                                    if(removed)
                                    {
                                        List<uint> partsToGo = new();
                                        partsToGo.Add((uint)partIndex);
                                        gimData.RemoveElementsAndTheirChildren(partsToGo);
                                    }
                                    if (!found && !removed)
                                    {
                                        GimElement polyRepElement = new() { elementType = GimElementType.R_Poly };
                                        int polyRepElementIndex = gimData.elements.Count;
                                        gimData.elements.Add(polyRepElement);
                                        gimData.AddChildToParent((uint)polyRepElementIndex, (uint)partIndex);
                                    }
                                }
                                else if (shapeRep.Items.Count > 1)
                                {
                                    //continue;
                                    bool removed = false;
                                    IfcLocalPlacement objectPlacement = (IfcLocalPlacement)element.ObjectPlacement;
                                    Vect generalLocationGIM = new Vect(0, 0, 0);
                                    Matrix transformLocation = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                                    IFCReaderUtilities.GetGlobalObjectPlacement(ref generalLocationGIM, objectPlacement, ref transformLocation);
                                    GimElement partElement = new() { elementType = GimElementType.D_part };
                                    int partIndex = gimData.elements.Count;
                                    if (partIndex == 1579)
                                    {
                                        string debug = "debug";
                                    }
                                    gimData.elements.Add(partElement);
                                    gimData.AddChildToParent((uint)partIndex, 1);

                                    Matrix transform = new(new Vect(1, 0, 0), new Vect(0, 1, 0), new Vect(0, 0, 1));
                                    IFCReaderUtilities.GetObjectTransformation(objectPlacement, ref transform);
                                    GimElement bRepElement = new() { elementType = GimElementType.R_BRep };
                                    int bRepElementIndex = gimData.Elements.Count;
                                    gimData.elements.Add(bRepElement);
                                    if (partIndex == 20158)
                                    {

                                    }
                                    gimData.AddChildToParent((uint)bRepElementIndex, (uint)partIndex);
                                    GimElement solidBodyElement = new() { elementType = GimElementType.B_solid };
                                    int solidBodyElementIndex = gimData.elements.Count;
                                    gimData.elements.Add(solidBodyElement);
                                    gimData.AddChildToParent((uint)solidBodyElementIndex, (uint)bRepElementIndex);
                                    

                                   
                                    foreach (var item in shapeRep.Items)
                                    {
                                        
                                        if (item is IfcMappedItem)
                                        {
                                            removed = IFC2Reader.MappedItemReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
                                            if (removed)
                                                break;
                                        }
                                        if (item is IfcFacetedBrep)
                                        {
                                            removed = IFC2Reader.FacetedBrepReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform);
                                            if (removed)
                                                break;
                                        }
                                        else if (item is IfcShellBasedSurfaceModel)
                                        {

                                            IFC2Reader.ShellBasedSurfaceModelReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform);
                                        }
                                        else if (item is IfcExtrudedAreaSolid)
                                        {
                                            removed =IFC2Reader.ExtrudedFaceReader(item, gimData, solidBodyElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
                                            if (removed)
                                                break;
                                        }

                                    }
                                    GimElement polyRepElement = new() { elementType = GimElementType.R_Poly };
                                    int polyRepElementIndex = gimData.elements.Count;
                                    gimData.elements.Add(polyRepElement);
                                    gimData.AddChildToParent((uint)polyRepElementIndex, (uint)partIndex);
                                    if (removed)
                                    {
                                        List<uint> partsToGo = new();
                                        partsToGo.Add((uint)partIndex);
                                        gimData.RemoveElementsAndTheirChildren(partsToGo);
                                    }
                                    
                                }
                            }
                        }
                    }
                }
            }
            GimBooleanConvertion.TransformBooleanToElement(gimData);
            return gimData;
        }



        



        

       

       


        public static void CreatePlane(List<Vect> listOfPoints, GimData gimData, int parentID, Vect generalLocation, Matrix localTransform)
        {
            GimElement planeElement = new() { elementType = GimElementType.F_plane };
            int planeIndex = gimData.elements.Count;
            gimData.elements.Add(planeElement);
            gimData.AddChildToParent((uint)planeIndex, (uint)parentID);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)planeIndex);
            Vect sumOfPoints = new(0, 0, 0);
            for (int i = 0; i < listOfPoints.Count; i++)
            {
                GimElement line = new() { elementType = GimElementType.C_line };
                int lineIndex = gimData.elements.Count;
                gimData.elements.Add(line);
                gimData.AddChildToParent((uint)lineIndex, (uint)wireIndex);
                Vect nextPointGIM = new(0, 0, 0);
                Vect currentPointGIM = listOfPoints[i];
                currentPointGIM = currentPointGIM * localTransform;
                sumOfPoints += currentPointGIM;
                if (i == listOfPoints.Count - 1)
                    nextPointGIM = listOfPoints[0];
                else
                    nextPointGIM = listOfPoints[i + 1];
                nextPointGIM = nextPointGIM * localTransform;
                var norm = (nextPointGIM - currentPointGIM).Norm;
                var direction = (nextPointGIM - currentPointGIM).Direction;
                double[] startEndNorms = new double[2];
                startEndNorms[0] = 0;
                startEndNorms[1] = norm;
                //var test = transform * direction;
                direction.ApplyToLocal(line.Locals[0]);
                line.Locals[0].px = currentPointGIM[0] + generalLocation[0];
                line.Locals[0].py = currentPointGIM[1] + generalLocation[1];
                line.Locals[0].pz = currentPointGIM[2] + generalLocation[2];
                line.elementDoubles = startEndNorms;
                //gimData.elements.Add(line);



            }
            Vect centerPoint = sumOfPoints / listOfPoints.Count;
            Vect directionA = new(0, 0, 0);
            double maxLen = 0;

            for (int i = 0; i < listOfPoints.Count; i++)
            {
                int firstIndex = (i - 1 + listOfPoints.Count) % listOfPoints.Count;
                int lastIndex = i;

                Vect lastPointGIM = listOfPoints[lastIndex];
                Vect firstPointGIM = listOfPoints[firstIndex];
                Vect segment = lastPointGIM - firstPointGIM;
                // logging highest Z value


                if (maxLen < segment.Norm)
                {
                    directionA = segment;
                    maxLen = segment.Norm;
                }
            }

            directionA = directionA.Direction;

            //if (directionA.Norm == 0) continue; // degenerate polygon

            // we want to calculate the vector B: the vector most perpendicular to direction A
            Vect directionB = new Vect(0, 0, 0);

            // maximum perpendicularity coefficient: we want directionB to be as less parallel with A, to guarantee the most precise normal 
            double maxCoeff = 0;
            bool degenerate = true;
            for (int i = 0; i < listOfPoints.Count; i++)
            {
                int firstIndex = (i - 1 + listOfPoints.Count) % listOfPoints.Count;
                int lastIndex = i;

                Vect lastPointGIM = listOfPoints[lastIndex];
                Vect firstPointGIM = listOfPoints[firstIndex];
                Vect segment = lastPointGIM - firstPointGIM;
                double coeff = (segment ^ directionA).Norm;
                if (coeff > maxCoeff)
                {
                    maxCoeff = coeff;
                    directionB = segment;
                }
            }

            if (!directionB.IsCollinearTo(directionA))
            {
                degenerate = false;
            }

            if (!degenerate)
            {
                directionB = (directionB - directionA * directionB * directionA).Direction;

                var directionC = directionB ^ directionA;
                Matrix matrix = new(directionA, directionB, directionC);
                (matrix * localTransform).ApplyToLocal(planeElement.Locals[0]);
                planeElement.locals[0].px = centerPoint[0] + generalLocation[0];
                planeElement.locals[0].py = centerPoint[1] + generalLocation[1];
                planeElement.locals[0].pz = centerPoint[2] + generalLocation[2];
            }
            else
            {
                List<uint> planeIndexes = new();
                planeIndexes.Add((uint)planeIndex);
                gimData.RemoveElementsAndTheirChildren(planeIndexes);
            }

            //if (directionB.Norm == 0 || degenerate) continue;

        }


        public static void CreatePlane(IfcPolyLoop polyLoop, GimData gimData, int parentID, Vect generalLocation, Matrix transform)
        {
            GimElement planeElement = new() { elementType = GimElementType.F_plane };
            int planeIndex = gimData.elements.Count;
            gimData.elements.Add(planeElement);
            gimData.AddChildToParent((uint)planeIndex, (uint)parentID);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)planeIndex);
            Vect sumOfPoints = new(0, 0, 0);
            for (int i = 0; i < polyLoop.Polygon.Count; i++)
            {
                GimElement line = new() { elementType = GimElementType.C_line };
                int lineIndex = gimData.elements.Count;
                gimData.elements.Add(line);
                gimData.AddChildToParent((uint)lineIndex, (uint)wireIndex);
                var currentPointIFC = polyLoop.Polygon[i];
                IfcCartesianPoint nextPointIFC = new(new(), 0, 0, 0);
                Vect currentPointGIM = new(currentPointIFC.CoordinateX, currentPointIFC.CoordinateY, currentPointIFC.CoordinateZ);
                currentPointGIM = currentPointGIM * transform;
                sumOfPoints += currentPointGIM;
                if (i == polyLoop.Polygon.Count - 1)
                    nextPointIFC = polyLoop.Polygon[0];
                else
                    nextPointIFC = polyLoop.Polygon[i + 1];
                Vect nextPointGIM = new(nextPointIFC.CoordinateX, nextPointIFC.CoordinateY, nextPointIFC.CoordinateZ);
                nextPointGIM = nextPointGIM * transform;
                var norm = (nextPointGIM - currentPointGIM).Norm;
                var direction = (nextPointGIM - currentPointGIM).Direction;
                double[] startEndNorms = new double[2];
                startEndNorms[0] = 0;
                startEndNorms[1] = norm;
                //var test = transform * direction;
                direction.ApplyToLocal(line.Locals[0]);
                line.Locals[0].px = currentPointGIM[0] + generalLocation[0];
                line.Locals[0].py = currentPointGIM[1] + generalLocation[1];
                line.Locals[0].pz = currentPointGIM[2] + generalLocation[2];
                line.elementDoubles = startEndNorms;
                //gimData.elements.Add(line);



            }
            Vect centerPoint = sumOfPoints / polyLoop.Polygon.Count;
            var originIFC = polyLoop.Polygon[0];
            Vect origin = new Vect(originIFC.CoordinateX, originIFC.CoordinateY, originIFC.CoordinateZ);
            Vect directionA = new(0, 0, 0);
            double maxLen = 0;

            for (int i = 0; i < polyLoop.Polygon.Count; i++)
            {
                int firstIndex = (i - 1 + polyLoop.Polygon.Count) % polyLoop.Polygon.Count;
                int lastIndex = i;
                var lastPointIFC = polyLoop.Polygon[lastIndex];
                var firstPointIFC = polyLoop.Polygon[firstIndex];
                Vect lastPointGIM = new(lastPointIFC.CoordinateX, lastPointIFC.CoordinateY, lastPointIFC.CoordinateZ);
                Vect firstPointGIM = new(firstPointIFC.CoordinateX, firstPointIFC.CoordinateY, firstPointIFC.CoordinateZ);
                Vect segment = lastPointGIM - firstPointGIM;
                // logging highest Z value


                if (maxLen < segment.Norm)
                {
                    directionA = segment;
                    maxLen = segment.Norm;
                }
            }

            directionA = directionA.Direction;

            //if (directionA.Norm == 0) continue; // degenerate polygon

            // we want to calculate the vector B: the vector most perpendicular to direction A
            Vect directionB = new Vect(0, 0, 0);

            // maximum perpendicularity coefficient: we want directionB to be as less parallel with A, to guarantee the most precise normal 
            double maxCoeff = 0;
            bool degenerate = true;
            for (int i = 0; i < polyLoop.Polygon.Count; i++)
            {
                int firstIndex = (i - 1 + polyLoop.Polygon.Count) % polyLoop.Polygon.Count;
                int lastIndex = i;
                var lastPointIFC = polyLoop.Polygon[lastIndex];
                var firstPointIFC = polyLoop.Polygon[firstIndex];
                Vect lastPointGIM = new(lastPointIFC.CoordinateX, lastPointIFC.CoordinateY, lastPointIFC.CoordinateZ);
                Vect firstPointGIM = new(firstPointIFC.CoordinateX, firstPointIFC.CoordinateY, firstPointIFC.CoordinateZ);
                Vect segment = lastPointGIM - firstPointGIM;
                double coeff = (segment ^ directionA).Norm;
                if (coeff > maxCoeff)
                {
                    maxCoeff = coeff;
                    directionB = segment;
                }
            }

            if (!directionB.IsCollinearTo(directionA))
            {
                degenerate = false;
            }

            if (!degenerate)
            {
                directionB = (directionB - directionA * directionB * directionA).Direction;

                var directionC = directionB ^ directionA;
                Matrix matrix = new(directionA, directionB, directionC);
                (matrix * transform).ApplyToLocal(planeElement.Locals[0]);
                planeElement.locals[0].px = centerPoint[0] + generalLocation[0];
                planeElement.locals[0].py = centerPoint[1] + generalLocation[1];
                planeElement.locals[0].pz = centerPoint[2] + generalLocation[2];
            }
            else
            {
                List<uint> planeIndexes = new();
                planeIndexes.Add((uint)planeIndex);
                gimData.RemoveElementsAndTheirChildren(planeIndexes);
            }

            //if (directionB.Norm == 0 || degenerate) continue;

        }

        //For the list of points, first and second points are the original center and the extruded one
        //Third and fourth are the start and end point of the line


        public static void CreateCylinderComposite(List<Vect> listOfPoints, GimData gimData, int parentID, Vect generalLocation, Matrix transform, Matrix finalTransform, double radius, double startAngle = 0, double endAngle = Math.PI * 2)
        {

            Vect transformedCircleCenter1 = listOfPoints[0] * finalTransform;
            Vect transformedCircleCenter2 = listOfPoints[1] * finalTransform;


            GimElement cylinderElement = new() { elementType = GimElementType.F_cilinder };
            int cylinderIndex = gimData.elements.Count;
            gimData.elements.Add(cylinderElement);
            gimData.AddChildToParent((uint)cylinderIndex, (uint)parentID);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)cylinderIndex);
            double[] cylinderRadius = new double[1];
            cylinderRadius[0] = radius;
            cylinderElement.elementDoubles = cylinderRadius;
            Vect cylinderCenter = listOfPoints[0];
            transform.ApplyToLocal(cylinderElement.Locals[0]);
            cylinderElement.Locals[0].px = transformedCircleCenter1[0] + generalLocation[0];
            cylinderElement.Locals[0].py = transformedCircleCenter1[1] + generalLocation[1];
            cylinderElement.Locals[0].pz = transformedCircleCenter1[2] + generalLocation[2];

            Vect direction1 = new(cylinderElement.locals[0].Xx, cylinderElement.locals[0].Xy, cylinderElement.locals[0].Xz);
            Vect direction2 = new(cylinderElement.locals[0].Yx, cylinderElement.locals[0].Yy, cylinderElement.locals[0].Yz);

            Vect startingPoint = new(0, 0, 0);
            Vect endingPoint = new(0, 0, 0);





            //LINES + CIRCLES ELEMENTS TREATMENT
            Vect circle1Center = listOfPoints[0];
            Vect circle2Center = listOfPoints[1];
            //Circle1
            GimElement circle1Element = new() { elementType = GimElementType.C_circle };
            int circle1Index = gimData.elements.Count;
            gimData.elements.Add(circle1Element);
            gimData.AddChildToParent((uint)circle1Index, (uint)wireIndex);
            
            circle1Element.Locals[0].px = transformedCircleCenter1[0] + generalLocation[0];
            circle1Element.Locals[0].py = transformedCircleCenter1[1] + generalLocation[1];
            circle1Element.Locals[0].pz = transformedCircleCenter1[2] + generalLocation[2];
            transform.ApplyToLocal(circle1Element.Locals[0]);
            double[] circle1RadiusAndAngle = new double[3];
            circle1RadiusAndAngle[0] = radius;
            circle1RadiusAndAngle[1] = startAngle;
            circle1RadiusAndAngle[2] = endAngle;
            circle1Element.elementDoubles = circle1RadiusAndAngle;


            startingPoint = circle1Center + radius * Math.Cos(startAngle) * direction1 + radius * Math.Sin(startAngle) * direction2;
            endingPoint = circle1Center + radius * Math.Cos(endAngle) * direction1 + radius * Math.Sin(endAngle) * direction2;

            startingPoint = startingPoint * finalTransform + generalLocation;
            endingPoint = endingPoint * finalTransform + generalLocation;

            Vect startingPoint2 = new(0, 0, 0);
            Vect endingPoint2 = new(0, 0, 0);

            startingPoint2 = circle2Center + radius * Math.Cos(startAngle) * direction1 + radius * Math.Sin(startAngle) * direction2;
            endingPoint2 = circle2Center + radius * Math.Cos(endAngle) * direction1 + radius * Math.Sin(endAngle) * direction2;

            startingPoint2 = (startingPoint2 * finalTransform) + generalLocation;
            endingPoint2 = endingPoint2 * finalTransform + generalLocation;


            //Line1
            GimElement line1 = new() { elementType = GimElementType.C_line };
            int line1Index = gimData.elements.Count;
            gimData.elements.Add(line1);
            gimData.AddChildToParent((uint)line1Index, (uint)wireIndex);
            var normLine1 = (endingPoint2 - startingPoint2).Norm;
            var direction = (endingPoint2 - startingPoint2).Direction;
            double[] startEndNormsLine1 = new double[2];
            startEndNormsLine1[0] = 0;
            startEndNormsLine1[1] = normLine1;
            //var test = transform * direction;
            direction.ApplyToLocal(line1.Locals[0]);
            line1.Locals[0].px = startingPoint2[0];
            line1.Locals[0].py = startingPoint2[1];
            line1.Locals[0].pz = startingPoint2[2];
            line1.elementDoubles = startEndNormsLine1;


            //Circle2
            GimElement circle2Element = new() { elementType = GimElementType.C_circle };
            int circle2Index = gimData.elements.Count;
            gimData.elements.Add(circle2Element);
            gimData.AddChildToParent((uint)circle2Index, (uint)wireIndex);
            
            circle2Element.Locals[0].px = transformedCircleCenter2[0] + generalLocation[0];
            circle2Element.Locals[0].py = transformedCircleCenter2[1] + generalLocation[1];
            circle2Element.Locals[0].pz = transformedCircleCenter2[2] + generalLocation[2];
            transform.ApplyToLocal(circle2Element.Locals[0]);
            double[] circle2RadiusAndAngle = new double[3];
            circle2RadiusAndAngle[0] = radius;
            circle2RadiusAndAngle[1] = startAngle;
            circle2RadiusAndAngle[2] = endAngle;
            circle2Element.elementDoubles = circle2RadiusAndAngle;
            circle2Element.orientation = false;

            



            //Line2
            GimElement line2 = new() { elementType = GimElementType.C_line };
            int line2Index = gimData.elements.Count;
            gimData.elements.Add(line2);
            gimData.AddChildToParent((uint)line2Index, (uint)wireIndex);
            var norm2 = (endingPoint - startingPoint).Norm;
            var direction3 = (endingPoint - startingPoint).Direction;
            double[] startEndNorms2 = new double[2];
            startEndNorms2[0] = 0;
            startEndNorms2[1] = norm2;
            //var test = transform * direction;
            line2.orientation = false;
            direction3.ApplyToLocal(line2.Locals[0]);
            line2.Locals[0].px = startingPoint[0];
            line2.Locals[0].py = startingPoint[1];
            line2.Locals[0].pz = startingPoint[2];
            line2.elementDoubles = startEndNorms2;




        }



        public static void CreateCylinder(List<Vect> listOfPoints, GimData gimData, int parentID, Vect generalLocation, Matrix transform, double radius,  double startAngle = 0, double endAngle = Math.PI * 2)
        {
            //CYLINDER ELEMENT TREATMENT

            GimElement cylinderElement = new() { elementType = GimElementType.F_cilinder };
            int cylinderIndex = gimData.elements.Count;
            gimData.elements.Add(cylinderElement);
            gimData.AddChildToParent((uint)cylinderIndex, (uint)parentID);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)cylinderIndex);
            double[] cylinderRadius = new double[1];
            cylinderRadius[0] = radius;
            cylinderElement.elementDoubles = cylinderRadius;
            Vect cylinderCenter = listOfPoints[0];
            transform.ApplyToLocal(cylinderElement.Locals[0]);
            cylinderElement.Locals[0].px = cylinderCenter[0];
            cylinderElement.Locals[0].py = cylinderCenter[1];
            cylinderElement.Locals[0].pz = cylinderCenter[2];

            //LINES + CIRCLES ELEMENTS TREATMENT

            //Circle1
            GimElement circle1Element = new() { elementType = GimElementType.C_circle };
            int circle1Index = gimData.elements.Count;
            gimData.elements.Add(circle1Element);
            gimData.AddChildToParent((uint)circle1Index, (uint)wireIndex);
            Vect circle1Center = listOfPoints[0];
            circle1Element.Locals[0].px = circle1Center[0];
            circle1Element.Locals[0].py = circle1Center[1];
            circle1Element.Locals[0].pz = circle1Center[2];
            transform.ApplyToLocal(circle1Element.Locals[0]);
            double[] circle1RadiusAndAngle = new double[3];
            circle1RadiusAndAngle[0] = radius;
            circle1RadiusAndAngle[1] = startAngle;
            circle1RadiusAndAngle[2] = endAngle;
            circle1Element.elementDoubles = circle1RadiusAndAngle;


            //Line Points

            Vect lineStartPoint = listOfPoints[2];
            Vect lineEndPoint = listOfPoints[3];


            //Line1
            GimElement line1 = new() { elementType = GimElementType.C_line };
            int line1Index = gimData.elements.Count;
            gimData.elements.Add(line1);
            gimData.AddChildToParent((uint)line1Index, (uint)wireIndex);
            var normLine1 = (lineEndPoint - lineStartPoint).Norm;
            var direction = (lineEndPoint - lineStartPoint).Direction;
            double[] startEndNormsLine1 = new double[2];
            startEndNormsLine1[0] = 0;
            startEndNormsLine1[1] = normLine1;
            //var test = transform * direction;
            direction.ApplyToLocal(line1.Locals[0]);
            line1.Locals[0].px = lineStartPoint[0];
            line1.Locals[0].py = lineStartPoint[1];
            line1.Locals[0].pz = lineStartPoint[2];
            line1.elementDoubles = startEndNormsLine1;

            //LINE2D1
            GimElement line2D1 = new() { elementType = GimElementType.C_line2D };
            int line2D1Index = gimData.elements.Count;
            gimData.elements.Add(line2D1);
            gimData.AddChildToParent((uint)line2D1Index, (uint)line1Index);
            double[] startEndNormsLine2D1 = new double[8];
            startEndNormsLine2D1[0] = lineStartPoint[0];
            startEndNormsLine2D1[1] = lineStartPoint[1];
            startEndNormsLine2D1[2] = lineStartPoint[2];
            startEndNormsLine2D1[3] = lineEndPoint[0];
            startEndNormsLine2D1[4] = lineEndPoint[1];
            startEndNormsLine2D1[5] = lineEndPoint[2];
            startEndNormsLine2D1[6] = 0;
            startEndNormsLine2D1[7] = normLine1;

            line2D1.elementDoubles = startEndNormsLine2D1;

            //Circle2
            GimElement circle2Element = new() { elementType = GimElementType.C_circle };
            int circle2Index = gimData.elements.Count;
            gimData.elements.Add(circle2Element);
            gimData.AddChildToParent((uint)circle2Index, (uint)wireIndex);
            Vect circle2Center = listOfPoints[1];
            circle2Element.Locals[0].px = circle2Center[0];
            circle2Element.Locals[0].py = circle2Center[1];
            circle2Element.Locals[0].pz = circle2Center[2];
            transform.ApplyToLocal(circle2Element.Locals[0]);
            double[] circle2RadiusAndAngle = new double[3];
            circle2RadiusAndAngle[0] = radius;
            circle2RadiusAndAngle[1] = startAngle;
            circle2RadiusAndAngle[2] = endAngle;
            circle2Element.elementDoubles = circle2RadiusAndAngle;
            circle2Element.orientation = false;


            

            //Line2
            GimElement line2 = new() { elementType = GimElementType.C_line };
            int line2Index = gimData.elements.Count;
            gimData.elements.Add(line2);
            gimData.AddChildToParent((uint)line2Index, (uint)wireIndex);
            var norm2 = (lineEndPoint - lineStartPoint).Norm;
            var direction2 = (lineEndPoint - lineStartPoint).Direction;
            double[] startEndNorms2 = new double[2];
            startEndNorms2[0] = 0;
            startEndNorms2[1] = norm2;
            //var test = transform * direction;
            line2.orientation = false;
            direction2.ApplyToLocal(line2.Locals[0]);
            line2.Locals[0].px = lineStartPoint[0];
            line2.Locals[0].py = lineStartPoint[1];
            line2.Locals[0].pz = lineStartPoint[2];
            line2.elementDoubles = startEndNormsLine1;


            //LINE2D2
            GimElement line2D2 = new() { elementType = GimElementType.C_line2D };
            int line2D2Index = gimData.elements.Count;
            gimData.elements.Add(line2D2);
            gimData.AddChildToParent((uint)line2D2Index, (uint)line2Index);
            double[] startEndNormsLine2D2 = new double[8];
            startEndNormsLine2D2[0] = lineStartPoint[0];
            startEndNormsLine2D2[1] = lineStartPoint[1];
            startEndNormsLine2D2[2] = lineStartPoint[2];
            startEndNormsLine2D2[3] = lineEndPoint[0];
            startEndNormsLine2D2[4] = lineEndPoint[1];
            startEndNormsLine2D2[5] = lineEndPoint[2];
            startEndNormsLine2D2[6] = normLine1;
            startEndNormsLine2D2[7] = 0;

            //var test = transform * direction;

            line2D2.elementDoubles = startEndNormsLine2D2;




        }

        public static void CreateCirclePlane(Vect center, GimData gimData, int parentID, Vect generalLocation, Matrix transform, double radius, double startAngle = 0, double endAngle = Math.PI*2)
        {

            //Plane1
            GimElement planeElement = new() { elementType = GimElementType.F_plane };
            int planeIndex = gimData.elements.Count;
            gimData.elements.Add(planeElement);
            gimData.AddChildToParent((uint)planeIndex, (uint)parentID);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)planeIndex);
            planeElement.Locals[0].px = center[0];
            planeElement.Locals[0].py = center[1];
            planeElement.Locals[0].pz = center[2];
            transform.ApplyToLocal(planeElement.Locals[0]);
            

            //Circle1
            GimElement circle1Element = new() { elementType = GimElementType.C_circle };
            int circle1Index = gimData.elements.Count;
            gimData.elements.Add(circle1Element);
            gimData.AddChildToParent((uint)circle1Index, (uint)wireIndex);
            
            circle1Element.Locals[0].px = center[0];
            circle1Element.Locals[0].py = center[1];
            circle1Element.Locals[0].pz = center[2];
            transform.ApplyToLocal(circle1Element.Locals[0]);
            double[] circle1RadiusAndAngle = new double[3];
            circle1RadiusAndAngle[0] = radius;
            circle1RadiusAndAngle[1] = 0;
            circle1RadiusAndAngle[2] = Math.PI * 2;
            circle1Element.elementDoubles = circle1RadiusAndAngle;


        }

    }
}
