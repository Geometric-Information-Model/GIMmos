using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestPlatform")]
namespace GIMcompress
{
	public static class ProfileDecompressor
    {

        public static void BuildProfileWalls(uint profileId, GimData gimData)
        {
            if (profileId >= gimData.Elements.Count)
            {
                return;
            }


            GimElement profileElement = gimData.Elements[(int)profileId];

            if (profileElement.ElementType != GimElementType.Gim_ProfileInstance)
            {
                return;
            }

            uint profileParentId = profileElement.locals[0].parent;
            GimElement profileParent = gimData.Elements[(int)profileParentId];

            uint cutSurfaceId = profileElement.Elements[0];
            GimElement cutSurfaceElement = gimData.Elements[(int)cutSurfaceId];
            uint polylineId = profileElement.Elements[1];
            GimElement polylineElement = gimData.Elements[(int)polylineId];

            uint profileLineId = polylineElement.Elements[0];
            GimElement profileLine = gimData.Elements[(int)profileLineId];

            GimLocal lineLocal = null;

            foreach (GimLocal local in profileLine.Locals)
            {
                if (local.parent == polylineId)
                {
                    lineLocal = local;
                }
            }

            if (lineLocal == null) return;

            Dictionary<uint, List<GimLocal>> processedElementLocals = new Dictionary<uint, List<GimLocal>>();

            Vect profileDirection = new Vect(lineLocal);
            Vect profileBasePoint = new Vect(lineLocal.Px, lineLocal.Py, lineLocal.Pz);
            double faceAParam = profileLine.ElementDoubles[0];
            double faceBParam = profileLine.ElementDoubles[1];
            GimElement faceASurface = null;
            GimElement faceBSurface = null;
            uint faceASurfaceId = 0;
            uint faceBSurfaceId = 0;

            List<Tuple<GimElement, GimElement>> faceACounterparts = new List<Tuple<GimElement, GimElement>>();

            bool faceAIsPlane = false; //we'll be checking if we're constructiong from a planar face, or another complex for
            bool faceAomitted = (profileElement.ElementInts[0] % 2) == 1; // 1 signifies that there's 1 in the first bit, which signifies that face is omitted

            if (faceAomitted)
            {
                faceAIsPlane = true;
            }
            else
            {
                faceASurfaceId = profileElement.Elements[2];
                faceASurface = gimData.Elements[(int)faceASurfaceId];

                if (faceASurface.Elements.Count != 1)
                {
                    faceAIsPlane = false;
                }
                else
                {
                    uint childId = faceASurface.Elements[0];
                    GimElement child = gimData.Elements[(int)childId];

                    if (child.Elements.Count == 0)
                    {
                        faceAIsPlane = true;
                    }
                }
            }

            List<Tuple<GimElement, GimElement>> faceBCounterparts = new List<Tuple<GimElement, GimElement>>();

            bool faceBIsPlane = false;
            bool faceBomitted = ((profileElement.ElementInts[0] / 2) % 2) == 1; // 1 signifies that there's 1 in the second bit, which signifies that face is omitted

            if (faceBomitted)
            {
                faceBIsPlane = true;
            }
            else
            {
                int faceBindex = 3;
                if (faceAomitted) faceBindex = 2;
                faceBSurfaceId = profileElement.Elements[faceBindex];
                faceBSurface = gimData.Elements[(int)faceBSurfaceId];

                if (faceBSurface.Elements.Count != 1)
                {
                    faceBIsPlane = false;
                }
                else
                {
                    uint childId = faceBSurface.Elements[0];
                    GimElement child = gimData.Elements[(int)childId];

                    if (child.Elements.Count == 0)
                    {
                        faceBIsPlane = true;
                    }
                }
            }


            Vect faceAVect1 = null, faceAVect2 = null;
            Vect faceABasePoint = null;
            GimLocal faceAlocal = null;

            if (faceAomitted)
            {
                faceABasePoint = profileBasePoint + faceAParam * profileDirection;
                faceAVect1 = profileDirection ^ new Vect(1, 0, 0);
                if (faceAVect1.Norm < PolygonRepresentation.EQUALITY_MARGIN)
                {
                    faceAVect1 = profileDirection ^ new Vect(0, 1, 0);
                }
                faceAVect1 = faceAVect1.Direction;
                faceAVect2 = profileDirection ^ faceAVect1;

            }
            else
            {
                foreach (GimLocal local in faceASurface.Locals)
                {
                    if (local.parent == profileId)
                    {
                        faceAlocal = local;
                        break;
                    }
                }

                uint faceElId = faceASurface.Elements[0];
                GimElement faceElement = gimData.Elements[(int)faceElId];
                GimLocal faceLocal = null;
                foreach (GimLocal local in faceElement.Locals)
                {
                    if (local.parent == faceASurfaceId)
                    {
                        faceLocal = local;
                        break;
                    }
                }



                faceABasePoint = new Vect(faceLocal.Px, faceLocal.Py, faceLocal.Pz);
                if (faceAIsPlane)
                {

                    faceAVect1 = new Vect(faceLocal, 0);
                    faceAVect2 = new Vect(faceLocal, 1);
                }

            }


            Vect faceBVect1 = null, faceBVect2 = null;
            Vect faceBBasePoint = null;
            GimLocal faceBlocal = null;

            if (faceBomitted)
            {
                faceBBasePoint = profileBasePoint + faceBParam * profileDirection;
                faceBVect1 = profileDirection ^ new Vect(1, 0, 0);
                if (faceBVect1.Norm < PolygonRepresentation.EQUALITY_MARGIN)
                {
                    faceBVect1 = profileDirection ^ new Vect(0, 1, 0);
                }
                faceBVect1 = faceBVect1.Direction;
                faceBVect2 = profileDirection ^ faceBVect1;

            }
            else
            {
                foreach (GimLocal local in faceBSurface.Locals)
                {
                    if (local.parent == profileId)
                    {
                        faceBlocal = local;
                        break;
                    }
                }

                uint faceElId = faceBSurface.Elements[0];
                GimElement faceElement = gimData.Elements[(int)faceElId];
                GimLocal faceLocal = null;
                foreach (GimLocal local in faceElement.Locals)
                {
                    if (local.parent == faceBSurfaceId)
                    {
                        faceLocal = local;
                        break;
                    }
                }



                faceBBasePoint = new Vect(faceLocal.Px, faceLocal.Py, faceLocal.Pz);
                if (faceBIsPlane)
                {
                    faceBVect1 = new Vect(faceLocal, 0);
                    faceBVect2 = new Vect(faceLocal, 1);
                }
            }

            Vect centerPoint = (faceABasePoint + faceBBasePoint) / 2;

            foreach (uint polyPolygonId in cutSurfaceElement.Elements)
            {

                GimElement faceAElement = new GimElement();
                uint faceAId = (uint)gimData.Elements.Count;
                if (faceAIsPlane)
                {
                    faceAElement.elementType = GimElementType.F_plane;
                    gimData.Elements.Add(faceAElement);
                    faceAElement.Locals.Add(new GimLocal());
                    faceAElement.Locals[0].parent = profileParentId;
                    profileParent.Elements.Add(faceAId);

                    Vect directionFromCenter = (faceABasePoint - centerPoint).Direction;
                    Matrix localMatrix;
                    if ((faceAVect1 ^ faceAVect2) * directionFromCenter > 0)
                    {
                        localMatrix = new Matrix(faceAVect1, faceAVect2, faceAVect1 ^ faceAVect2);
                    }
                    else
                    {
                        localMatrix = new Matrix(faceAVect2, faceAVect1, faceAVect2 ^ faceAVect1);
                    }

                    localMatrix.ApplyToLocal(faceAElement.Locals[0]);
                }

                GimElement faceBElement = new GimElement();
                uint faceBId = (uint)gimData.Elements.Count;
                if (faceBIsPlane)
                {
                    faceBElement.elementType = GimElementType.F_plane;
                    gimData.Elements.Add(faceBElement);
                    faceBElement.Locals.Add(new GimLocal());
                    faceBElement.Locals[0].parent = profileParentId;
                    profileParent.Elements.Add(faceBId);

                    Vect directionFromCenter = (faceBBasePoint - centerPoint).Direction;
                    Matrix localMatrix;
                    if ((faceBVect1 ^ faceBVect2) * directionFromCenter > 0)
                    {
                        localMatrix = new Matrix(faceBVect1, faceBVect2, faceBVect1 ^ faceBVect2);
                    }
                    else
                    {
                        localMatrix = new Matrix(faceBVect2, faceBVect1, faceBVect2 ^ faceBVect1);
                    }

                    localMatrix.ApplyToLocal(faceBElement.Locals[0]);
                }

                GimElement polyPolygonElement = gimData.Elements[(int)polyPolygonId];
                int singlePolygonIndex = 0;
                foreach (uint singlePolygonId in polyPolygonElement.Elements)
                {
                    GimElement singlePolygonElement = gimData.Elements[(int)singlePolygonId];
                    bool reverse = (singlePolygonIndex % 2) == 1; // reversing element orientation

                    GimElement wireAElement = new GimElement();
                    uint wireAId = (uint)gimData.Elements.Count;
                    if (faceAIsPlane)
                    {
                        wireAElement.elementType = GimElementType.wire;
                        gimData.Elements.Add(wireAElement);
                        wireAElement.Locals.Add(new GimLocal());
                        wireAElement.Locals[0].parent = faceAId;
                        faceAElement.Elements.Add(wireAId);
                    }

                    GimElement wireBElement = new GimElement();
                    uint wireBId = (uint)gimData.Elements.Count;
                    if (faceBIsPlane)
                    {
                        wireBElement.elementType = GimElementType.wire;
                        gimData.Elements.Add(wireBElement);
                        wireBElement.Locals.Add(new GimLocal());
                        wireBElement.Locals[0].parent = faceBId;
                        faceBElement.Elements.Add(wireBId);
                    }

                    Vect previousEnd = null;
                    Vect previousStart = null;
                    int index = 0;

                    List<GimElement> wireAChildElements = new List<GimElement>();
                    List<GimElement> wireBChildElements = new List<GimElement>();

                    foreach (uint polygonSegmentId in singlePolygonElement.Elements)
                    {
                        GimElement polygonSegmentElement = gimData.Elements[(int)polygonSegmentId];
                        GimLocal polygonSegmentLocal = null;
                        Vect startingPoint = null;
                        Vect endingPoint = null;
                        Vect[] IntermediaryPoints = null;
                        int localIndex = 0;

                        foreach (GimLocal local in polygonSegmentElement.Locals)
                        {
                            if (local.Parent == singlePolygonId)
                            {
                                List<GimLocal> localList;
                                if (processedElementLocals.ContainsKey(polygonSegmentId))
                                {
                                    localList = processedElementLocals[polygonSegmentId];
                                    if (localList.Contains(local))
                                    {
                                        localIndex++;
                                        continue;
                                    }
                                }
                                else
                                {
                                    processedElementLocals.Add(polygonSegmentId, new List<GimLocal>());
                                    localList = processedElementLocals[polygonSegmentId];
                                }
                                Vect startEnd = CurveHelpers.getStartAndEndPointsOfCurve(polygonSegmentElement, gimData, localIndex);
                                startingPoint = startEnd[0..3];
                                endingPoint = startEnd[3..6];

                                polygonSegmentLocal = local;
                                localList.Add(polygonSegmentLocal);
                                break;
                            }
                            localIndex++;
                        }

                        previousStart = startingPoint;
                        previousEnd = endingPoint;

                        GimElement faceACounterPart;
                        GimElement faceBCounterPart;


                        if (faceAIsPlane)
                        {
                            faceACounterPart = GetFaceCounterpartFromPlane(polygonSegmentElement, polygonSegmentLocal, profileDirection,
                                faceABasePoint, faceAVect1, faceAVect2);
                        }
                        else
                        {

                            faceACounterPart = GetFaceCounterpartFromElements(polygonSegmentElement, polygonSegmentLocal, profileDirection,
                          faceASurface, faceASurfaceId, singlePolygonId, gimData);

                        }


                        if (faceBIsPlane)
                        {
                            faceBCounterPart = GetFaceCounterpartFromPlane(polygonSegmentElement, polygonSegmentLocal, profileDirection,
                                faceBBasePoint, faceBVect1, faceBVect2);
                        }
                        else
                        {

                            faceBCounterPart = GetFaceCounterpartFromElements(polygonSegmentElement, polygonSegmentLocal, profileDirection,
                            faceBSurface, faceBSurfaceId, singlePolygonId, gimData);
                        }

                        // TODO: optimize and rewrite

                        double[] faceAExtremes = CurveHelpers.getStartAndEndPointsOfCurve(faceACounterPart, gimData);
                        if(faceACounterPart.ElementType == GimElementType.Gim_ComplexSegment && faceAIsPlane)
                        {
                            GimElement newLineElement = new GimElement();
                            newLineElement.ElementType = GimElementType.C_line;
                            GimLocal newLineLocal = new GimLocal();

                            Vect start = faceAExtremes[0..3];
                            newLineLocal.px = start[0];
                            newLineLocal.py = start[1];
                            newLineLocal.pz = start[2];

                            Vect end = faceAExtremes[3..6];

                            double lineLength = (end - start).Norm;
                            Vect lineVect = (end - start).Direction;
                            lineVect.ApplyToLocal(newLineLocal);
                            newLineElement.ElementDoubles = new double[] { 0, lineLength };
                            newLineElement.Locals.Add(newLineLocal);

                            GimElement projectedLine = GetFaceCounterpartFromPlane(newLineElement, newLineLocal, profileDirection, faceABasePoint, faceAVect1, faceAVect2);
                            projectedLine.ElementType = GimElementType.C_line;
                            faceAExtremes = CurveHelpers.getStartAndEndPointsOfCurve(projectedLine, gimData);
                        }
                        double[] faceBExtremes = CurveHelpers.getStartAndEndPointsOfCurve(faceBCounterPart, gimData);

                        if (faceBCounterPart.ElementType == GimElementType.Gim_ComplexSegment && faceBIsPlane)
                        {
                            GimElement newLineElement = new GimElement();
                            newLineElement.ElementType = GimElementType.C_line;
                            GimLocal newLineLocal = new GimLocal();

                            Vect start = faceBExtremes[0..3];
                            newLineLocal.px = start[0];
                            newLineLocal.py = start[1];
                            newLineLocal.pz = start[2];

                            Vect end = faceBExtremes[3..6];

                            double lineLength = (end - start).Norm;
                            Vect lineVect = (end - start).Direction;
                            lineVect.ApplyToLocal(newLineLocal);
                            newLineElement.ElementDoubles = new double[] { 0, lineLength };
                            newLineElement.Locals.Add(newLineLocal);

                            GimElement projectedLine = GetFaceCounterpartFromPlane(newLineElement, newLineLocal, profileDirection, faceBBasePoint, faceBVect1, faceBVect2);

                            projectedLine.ElementType = GimElementType.C_line;
                            faceBExtremes = CurveHelpers.getStartAndEndPointsOfCurve(projectedLine, gimData);
                        }

                        Vect startA = faceAExtremes[0..3];
                        Vect startB = faceBExtremes[0..3];

                        Vect endA = faceAExtremes[3..6];
                        Vect endB = faceBExtremes[3..6];

                        Vect line1 = startB - startA;
                        double length1 = line1.Norm;

                        Vect line2 = endB - endA;
                        double length2 = line2.Norm;

                        if (length1 == 0 || length2 == 0)
                        {
                            string x = "DEBUG";
                        }

                        Vect direction1 = line1.Direction;
                        Vect direction2 = line2.Direction;

                        GimElement line1Element = new();
                        line1Element.ElementType = GimElementType.C_line;
                        GimLocal line1ElementLocal = new GimLocal();

                        line1Element.ElementDoubles = new double[2] { 0, length1 };
                        line1ElementLocal.Px = startA[0];
                        line1ElementLocal.Py = startA[1];
                        line1ElementLocal.Pz = startA[2];
                        direction1.ApplyToLocal(line1ElementLocal);

                        GimElement line2Element = line1Element.Clone();
                        GimLocal line2ElementLocal = line1ElementLocal.Clone();

                        line2Element.ElementDoubles = new double[2] { 0, length2 };
                        line2ElementLocal.Px = endA[0];
                        line2ElementLocal.Py = endA[1];
                        line2ElementLocal.Pz = endA[2];
                        direction2.ApplyToLocal(line2ElementLocal);

                        line1Element.Locals.Add(line1ElementLocal);
                        line2Element.Locals.Add(line2ElementLocal);

                        GimElement wallPieceElement = new GimElement();
                        GimLocal wallPieceElementLocal = new GimLocal();

                        if (faceACounterPart.ElementType == GimElementType.Gim_ComplexSegment
                            || faceBCounterPart.ElementType == GimElementType.Gim_ComplexSegment)
                        {
                            GimElement complexSegment;
                            if (faceACounterPart.ElementType == GimElementType.Gim_ComplexSegment)
                                complexSegment = faceACounterPart;
                            else
                                complexSegment = faceBCounterPart;

                            wallPieceElement.ElementDoubles = complexSegment.ElementDoubles;
                            wallPieceElement.ElementInts = complexSegment.ElementInts;
                            wallPieceElementLocal = complexSegment.Locals[localIndex].Clone();

                            if (complexSegment.ElementDoubles.Length == 1 && complexSegment.ElementInts == null)
                            // cylinder, it's the only face element with 1 double
                            {
                                wallPieceElement.ElementType = GimElementType.F_cilinder;
                            }
                            else // bspline face
                            {
                                wallPieceElement.ElementType = GimElementType.F_bspline;
                            }

                        }
                        else if (faceACounterPart.ElementType == GimElementType.C_circle && faceBCounterPart.elementType == GimElementType.C_circle)
                        {
                            wallPieceElement.ElementType = GimElementType.F_cilinder;
                            wallPieceElement.ElementDoubles = new double[] { faceACounterPart.ElementDoubles[0] };
                            wallPieceElementLocal = faceACounterPart.Locals[0].Clone();
                        }
                        else if (faceACounterPart.ElementType == GimElementType.C_line)
                        {
                            wallPieceElement.ElementType = GimElementType.F_plane;
                            Vect lineVect = new Vect(faceACounterPart.Locals[0]);
                            Matrix localMatrix = new Matrix(lineVect, profileDirection, lineVect ^ profileDirection);
                            localMatrix.ApplyToLocal(wallPieceElementLocal);

                        }
                        else if (faceACounterPart.ElementType == GimElementType.C_bspline || faceBCounterPart.ElementType == GimElementType.C_bspline)
                        {
                            wallPieceElement.ElementType = GimElementType.F_bspline;
                            GetBsplineDoublesAndIntsFromFaces(faceACounterPart, faceBCounterPart, wallPieceElement, profileDirection, wallPieceElementLocal);
                        }
                        else if (faceACounterPart.ElementType == GimElementType.C_bezier)
                        {
                            wallPieceElement.ElementType = GimElementType.F_bezier;
                        }

                        wallPieceElement.locals.Add(wallPieceElementLocal);

                        uint wallPieceElementId = (uint)gimData.Elements.Count;
                        gimData.Elements.Add(wallPieceElement);

                        wallPieceElementLocal.parent = profileParentId;
                        profileParent.Elements.Add(wallPieceElementId);

                        GimElement wireElement = new GimElement();
                        wireElement.ElementType = GimElementType.wire;
                        wireElement.Locals.Add(new GimLocal());

                        uint wireElementId = (uint)gimData.Elements.Count;
                        gimData.Elements.Add(wireElement);

                        wireElement.Locals[0].parent = wallPieceElementId;
                        wallPieceElement.Elements.Add(wireElementId);


                        // we will be adding the elements to the wire of the wall pieces, and the face if it exists

                        List<GimElement> wireElementChildren = new List<GimElement>();

                        if (faceACounterPart.ElementType == GimElementType.Gim_ComplexSegment)
                        {
                            if (faceAIsPlane)
                                foreach (uint childId in polygonSegmentElement.Elements)
                                {
                                    GimElement child = gimData.Elements[(int)childId];
                                    GimElement childClone = GetFaceCounterpartFromPlane(child, child.Locals[0], profileDirection, faceABasePoint, faceAVect1, faceAVect2);
                                    wireElementChildren.Add(childClone);
                                    wireAChildElements.Add(childClone); // adding it to the wire of the face
                                }
                            else foreach (uint childId in faceACounterPart.Elements)
                                {
                                    GimElement child = gimData.Elements[(int)childId];
                                    wireElementChildren.Add(child.Clone());
                                    wireAChildElements.Add(child.Clone()); // adding it to the wire of the face
                                }
                        }
                        else
                        {
                            wireElementChildren.Add(faceACounterPart);
                            wireAChildElements.Add(faceACounterPart); // adding it to the wire of the face
                        }

                        wireElementChildren.Add(line1Element);

                        if (faceBCounterPart.ElementType == GimElementType.Gim_ComplexSegment)
                        {
                            if (faceBIsPlane)
                                foreach (uint childId in polygonSegmentElement.Elements)
                                {
                                    GimElement child = gimData.Elements[(int)childId];
                                    GimElement childClone = GetFaceCounterpartFromPlane(child, child.Locals[0], profileDirection, faceBBasePoint, faceBVect1, faceBVect2);
                                    wireElementChildren.Add(childClone);
                                    wireBChildElements.Add(childClone); // adding it to the wire of the face

                                }
                            else foreach (uint childId in faceBCounterPart.Elements)
                                {
                                    GimElement child = gimData.Elements[(int)childId];
                                    wireElementChildren.Add(child.Clone());
                                    wireBChildElements.Add(faceBCounterPart); // adding it to the wire of the face
                                }
                        }
                        else
                        {
                            wireElementChildren.Add(faceBCounterPart);
                            wireBChildElements.Add(faceBCounterPart); // adding it to the wire of the face
                        }

                        wireElementChildren.Add(line2Element);



                        foreach (GimElement childElement in wireElementChildren)
                        {
                            uint childId = (uint)gimData.Elements.Count;
                            gimData.Elements.Add(childElement);

                            childElement.Locals[0].parent = wireElementId;
                            wireElement.Elements.Add(childId);
                        }



                        index++;

                    }

                    if (faceAIsPlane) // currently we only construct the faces if they are planar
                    {

                        for (int i = 0; i < wireAChildElements.Count; i++)
                        {
                            GimElement wireAChild = wireAChildElements[i].Clone();

                            uint wireAChildId = (uint)gimData.Elements.Count;
                            gimData.Elements.Add(wireAChild);

                            wireAChild.Locals[0].parent = wireAId;
                            wireAElement.Elements.Add(wireAChildId);
                        }

                        for (int i = 0; i < wireAElement.Elements.Count - 1; i++) // correcting order
                        {
                            uint currentPieceId = wireAElement.Elements[i];
                            GimElement currentPiece = gimData.Elements[(int)currentPieceId];
                            double[] extremities = CurveHelpers.getStartAndEndPointsOfCurve(currentPiece, gimData);
                            Vect start = extremities[0..3];
                            Vect end = extremities[3..6];
                            for (int j = i + 1; j < wireAElement.Elements.Count; j++)
                            {
                                uint otherPieceId = wireAElement.Elements[j];
                                GimElement otherPiece = gimData.Elements[(int)otherPieceId];
                                double[] otherExtremities = CurveHelpers.getStartAndEndPointsOfCurve(otherPiece, gimData);
                                Vect otherStart = otherExtremities[0..3];
                                Vect otherEnd = otherExtremities[3..6];

                                if (start == otherStart || start == otherEnd || end == otherStart || end == otherEnd)
                                {
                                    if (j == i + 1) continue;
                                    uint temp = wireAElement.Elements[i + 1];
                                    wireAElement.Elements[i + 1] = wireAElement.Elements[j];
                                    wireAElement.Elements[j] = temp;
                                }
                            }
                        }
                    }

                    if (faceBIsPlane) // currently we only construct the faces if they are planar
                    {

                        for (int i = 0; i < wireBChildElements.Count; i++)
                        {
                            GimElement wireBChild = wireBChildElements[i].Clone();

                            uint wireBChildId = (uint)gimData.Elements.Count;
                            gimData.Elements.Add(wireBChild);

                            wireBChild.Locals[0].parent = wireBId;
                            wireBElement.Elements.Add(wireBChildId  );
                        }
                        for (int i = 0; i < wireBElement.Elements.Count - 1; i++) // correcting order
                        {
                            uint currentPieceId = wireBElement.Elements[i];
                            GimElement currentPiece = gimData.Elements[(int)currentPieceId];
                            double[] extremities = CurveHelpers.getStartAndEndPointsOfCurve(currentPiece, gimData);
                            Vect start = extremities[0..3];
                            Vect end = extremities[3..6];
                            for (int j = i + 1; j < wireBElement.Elements.Count; j++)
                            {
                                uint otherPieceId = wireBElement.Elements[j];
                                GimElement otherPiece = gimData.Elements[(int)otherPieceId];
                                double[] otherExtremities = CurveHelpers.getStartAndEndPointsOfCurve(otherPiece, gimData);
                                Vect otherStart = otherExtremities[0..3];
                                Vect otherEnd = otherExtremities[3..6];

                                if (start == otherStart || start == otherEnd || end == otherStart || end == otherEnd)
                                {
                                    if (j == i + 1) continue;
                                    uint temp = wireBElement.Elements[i + 1];
                                    wireBElement.Elements[i + 1] = wireBElement.Elements[j];
                                    wireBElement.Elements[j] = temp;
                                }
                            }
                        }
                    }


                    if (faceAIsPlane || faceBIsPlane)
                    {
                        singlePolygonIndex++;
                    }

                }
            }

        }

        public static void RemoveProfilePieces(uint profileId, GimData gimData)
        {
            List<uint> elementsToRemove = new List<uint>() { profileId };

            gimData.RemoveElementsAndTheirChildren(elementsToRemove);
        }

        // HELPER FUNCTIONS BELOW


        public static GimElement GetFaceCounterpartFromPlane(GimElement polygonSegment, GimLocal polygonSegmentLocal, Vect profileDirection,
            Vect faceBasePoint, Vect faceVect1, Vect faceVect2)
        {
            GimElement counterpart = polygonSegment.Clone();
            counterpart.Locals = new();
            counterpart.ElementType = polygonSegment.ElementType;

            Vect polygonSegmentBasepoint = new Vect(polygonSegmentLocal.Px, polygonSegmentLocal.Py, polygonSegmentLocal.Pz);
            Vect planeNormal = faceVect1 ^ faceVect2;
            double distanceFromPlane = (polygonSegmentBasepoint - faceBasePoint) * planeNormal;
            Vect polygonSegmentBasepointProjected = polygonSegmentBasepoint - distanceFromPlane * planeNormal;

            GimLocal counterpartLocal = polygonSegmentLocal.Clone();
            counterpartLocal.Px = polygonSegmentBasepointProjected[0];
            counterpartLocal.Py = polygonSegmentBasepointProjected[1];
            counterpartLocal.Pz = polygonSegmentBasepointProjected[2];

            if (polygonSegment.ElementType == GimElementType.C_line)
            {
                Vect lineVect = new Vect(counterpartLocal);
                Vect vectStart = faceBasePoint;
                Vect endVect = vectStart + lineVect;

                double distFromPlane = (endVect - faceBasePoint) * planeNormal;
                Vect endVectProjected = endVect - distFromPlane * planeNormal;

                Vect projectedVect = endVectProjected - vectStart;

                double multiplier = projectedVect.Norm;
                counterpart.ElementDoubles[0] *= multiplier;
                counterpart.ElementDoubles[1] *= multiplier;

                projectedVect.Direction.ApplyToLocal(counterpartLocal);

            }
            else if (polygonSegment.ElementType == GimElementType.Gim_ComplexSegment)
            {
                if(polygonSegment.ElementDoubles != null)counterpart.ElementDoubles = (double[]) polygonSegment.ElementDoubles.Clone();
                if (polygonSegment.ElementInts != null) counterpart.ElementInts = (int[]) polygonSegment.ElementInts.Clone();
                counterpart.locals = new List<GimLocal>();
                foreach (GimLocal l in polygonSegment.Locals)
                    counterpart.locals.Add(l.Clone());
            }
            else
            {
                Matrix localMatrix = new Matrix(polygonSegmentLocal);
                Vect lineVect1 = localMatrix[0];
                Vect vectStart = faceBasePoint;
                Vect endVect1 = vectStart + lineVect1;

                double distFromPlane1 = (endVect1 - faceBasePoint) * planeNormal;
                Vect endVect1Projected = endVect1 - distFromPlane1 * planeNormal;

                Vect projectedVect1 = endVect1Projected - vectStart;

                Vect lineVect2 = localMatrix[1];
                Vect endVect2 = vectStart + lineVect2;

                double distFromPlane2 = (endVect2 - faceBasePoint) * planeNormal;
                Vect endVect2Projected = endVect2 - distFromPlane2 * planeNormal;

                Vect projectedVect2 = endVect2Projected - vectStart;

                double multiplier1 = projectedVect1.Norm;
                double multiplier2 = projectedVect2.Norm;

                Matrix counterPartMatrixLocal = new Matrix(projectedVect1.Direction, projectedVect2.Direction, localMatrix[2]);

                counterPartMatrixLocal.ApplyToLocal(counterpartLocal);

                if (polygonSegment.ElementType == GimElementType.C_circle)
                {

                    if (multiplier1 != 1 || multiplier2 != 1) // ellipse
                    {
                        counterpart.ElementDoubles = new double[4];
                        counterpart.ElementDoubles[0] = 1/multiplier1;
                        counterpart.ElementDoubles[1] = 1/multiplier2;
                        counterpart.ElementDoubles[2] = polygonSegment.ElementDoubles[1];
                        counterpart.ElementDoubles[3] = polygonSegment.ElementDoubles[2];
                        counterpart.ElementType = GimElementType.C_elipse;
                    }
                    else // circle
                    {

                    }
                }
                else if (polygonSegment.ElementType == GimElementType.C_bspline)
                {
                    int firstPoleIndex = 6;

                    int knotsCount = polygonSegment.ElementInts[0];
                    int polesCount = (polygonSegment.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                    int lastPoleIndex = firstPoleIndex + (polesCount) * 3; // 3 coordinates for every point. 

                    // indexes of starting and ending points, in case the data structure changes later on.
                    int firstPointIndex = 0;
                    int lastPointIndex = 3;

                    for (int i = 0; i < lastPoleIndex; i += 3)
                    {
                        Vect point = new Vect( // our point to check, let's call it P
                            polygonSegment.ElementDoubles[i],
                            polygonSegment.ElementDoubles[i + 1],
                            polygonSegment.ElementDoubles[i + 2]
                        );

                        double pointDistFromPlane = (point - faceBasePoint) * planeNormal;
                        Vect pointProjectedToPlane = point - pointDistFromPlane * planeNormal;

                        counterpart.ElementDoubles[i] = pointProjectedToPlane[0];
                        counterpart.ElementDoubles[i + 1] = pointProjectedToPlane[1];
                        counterpart.ElementDoubles[i + 2] = pointProjectedToPlane[2];

                    }
                }
            }

            counterpart.Locals.Add(counterpartLocal);


            return counterpart;
        }

        public static GimElement GetFaceCounterpartFromElements(GimElement polygonSegment, GimLocal polygonSegmentLocal, Vect profileDirection,
            GimElement cutSurface, uint cutSurfaceId, uint singlePolygonId, GimData gimData)
        {
            GimElement counterpart = new GimElement();

            int localIndex = polygonSegment.Locals.IndexOf(polygonSegmentLocal);
            double[] polygonSegmentExtremities = CurveHelpers.getStartAndEndPointsOfCurve(polygonSegment, gimData, localIndex);

            Vect start = polygonSegmentExtremities[0..3];
            Vect end = polygonSegmentExtremities[3..6];

            // each element and their localIndex, and whether they're reversed or not
            List<Tuple<GimElement, int, bool>> matchingElements = new();

            GimElement faceElement = gimData.Elements[(int)cutSurface.Elements[0]];

            if(singlePolygonId == 4017)
            {
                int x = 9;
            }

            foreach (uint wireId in faceElement.Elements)
            {
                GimElement wireElement = gimData.Elements[(int)wireId];
                foreach (uint childId in wireElement.Elements)
                {
                    GimElement childElement = gimData.Elements[(int)childId];
                    for (int i = 0; i < childElement.Locals.Count; i++)
                    {
                        double[] curveExtremities = CurveHelpers.getStartAndEndPointsOfCurve(childElement, gimData, i);
                        Vect curveStart = curveExtremities[0..3];
                        Vect curveEnd = curveExtremities[3..6];

                        Vect startToStartDifference = curveStart - start;
                        Vect startToEndDifference = curveStart - end;

                        if (startToStartDifference.IsCollinearTo(profileDirection)) // probably the same direction
                        {
                            Vect endDifference = curveEnd - end;
                            if (endDifference.IsCollinearTo(profileDirection)) // both ends match, add to match list.
                            {
                                matchingElements.Add(new Tuple<GimElement, int, bool>(childElement, i, false));
                            }
                        }
                        else if (startToEndDifference.IsCollinearTo(profileDirection))
                        // we check if the piece is the equivalent, but in a reverse order
                        {
                            Vect endDifference = curveEnd - end;
                            if (endDifference.IsCollinearTo(profileDirection)) // both ends match, add to match list, mark it as reversed
                            {
                                matchingElements.Add(new Tuple<GimElement, int, bool>(childElement, i, true));
                            }
                        }
                    }
                }
            }

            // sometimes we get multiple matches. this is the case in shapes composed of just two elements, as they share both extremities
            // for this particular case, we compare the "middle" of the curves and choose the one with the least difference

            Vect middlePoint;
            Vect[] intermediaryPoints = CurveHelpers.getIntermediaryPointsOfCurve(polygonSegment, localIndex);
            if(polygonSegment.ElementType == GimElementType.Gim_ComplexSegment)
            {
                List<Vect> points = new();
                foreach(uint childId in polygonSegment.Elements)
                {
                    GimElement childCurve = gimData.Elements[(int)childId];
                    Vect[] curveInterm = CurveHelpers.getIntermediaryPointsOfCurve(childCurve);
                    double[] curveExtremes = CurveHelpers.getStartAndEndPointsOfCurve(childCurve, gimData);
                    foreach (Vect intermPoint in curveInterm)
                    {
                        points.Add(intermPoint);
                    }
                    points.Add(curveExtremes[0..3]);
                    points.Add(curveExtremes[3..6]);
                }

                intermediaryPoints = points.ToArray();
            }
            
            if (intermediaryPoints.Length == 0)
            {
                middlePoint = (start + end) / 2; // only in the case of a line.
            }
            else
            {
                middlePoint = Vect.GetAveragePoint(intermediaryPoints);
            }

            List<double> distances = new();
            foreach (var match in matchingElements)
            {
                GimElement element = match.Item1;
                int matchLocalIndex = match.Item2;

                Vect curveMiddlePoint;
                Vect[] curveIntermediaries = CurveHelpers.getIntermediaryPointsOfCurve(element, matchLocalIndex);

                if (element.ElementType == GimElementType.Gim_ComplexSegment)
                {
                    List<Vect> points = new();
                    foreach (uint childId in element.Elements)
                    {
                        GimElement childCurve = gimData.Elements[(int)childId];
                        Vect[] curveInterm = CurveHelpers.getIntermediaryPointsOfCurve(childCurve);
                        double[] curveExtremes = CurveHelpers.getStartAndEndPointsOfCurve(childCurve, gimData);
                        foreach (Vect intermPoint in curveInterm)
                        {
                            points.Add(intermPoint);
                        }
                        points.Add(curveExtremes[0..3]);
                        points.Add(curveExtremes[3..6]);
                    }

                    curveIntermediaries = points.ToArray();
                }

                if (curveIntermediaries.Length == 0)
                {
                    double[] curveExtremities = CurveHelpers.getStartAndEndPointsOfCurve(element, gimData, matchLocalIndex);
                    curveMiddlePoint = (((Vect)curveExtremities[0..3]) + ((Vect)curveExtremities[3..6])) / 2;
                }
                else
                {
                    curveMiddlePoint = Vect.GetAveragePoint(curveIntermediaries);
                }

                // the deciding factor is how much the middle points deviate from the profileDirection line.
                Vect differenceVect = curveMiddlePoint - middlePoint;

                // we remove the profileDirection component from the vector, and we measure how "off" the points are.
                double differenceFactor = (differenceVect - (differenceVect * profileDirection) * profileDirection).Norm;
                distances.Add(differenceFactor);

            }
            // just a rough figure
            double min = 10000000;
            int minIndex = 0;
            for (int i = 0; i < distances.Count; i++)
            {
                if (distances[i] < min)
                {
                    min = distances[i];
                    minIndex = i;
                }
            }

            GimElement matchingElement = matchingElements[minIndex].Item1;
            int matchingLocalIndex = matchingElements[minIndex].Item2;
            bool inversed = matchingElements[minIndex].Item3;
            if (inversed)
            {
                return ProfileDetector.ReverseCurveElement(matchingElement, matchingLocalIndex);
            }

            counterpart = matchingElement.Clone();
            counterpart.Locals = new();
            counterpart.Locals.Add(matchingElement.Locals[matchingLocalIndex].Clone());

            return counterpart;
        }


        public static void
            GetBsplineDoublesAndIntsFromFaces(GimElement faceACounterPart, GimElement faceBCounterPart,
            GimElement wallPieceElement, Vect profileDirection, GimLocal wallPieceLocal)
        {
            // we check if the points lie on another type of shape (e.g.Cylinder)

            // curves could be both bsplines, or bspline and cylinder etc
            AbstractCurveConstructor faceAConstructor = AbstractCurveConstructor.GetCurveConstructor(faceACounterPart, false, 500);
            AbstractCurveConstructor faceBConstructor = AbstractCurveConstructor.GetCurveConstructor(faceBCounterPart, false, 500);

            faceAConstructor.constructCurve();
            Vect[] faceAPoints = faceAConstructor.Segments;
            int faceAlen = faceAPoints.Length;
            faceBConstructor.constructCurve();
            Vect[] faceBPoints = faceBConstructor.Segments;
            int faceBlen = faceBPoints.Length;

            bool faceACylindrical = true;
            bool faceBCylindrical = true;

            // points will be projected to the plane of this point.
            Vect basePoint = faceAPoints[0];
            Vect planeNormal = profileDirection.Direction;

            // faceACheck

            // normalizing the points by projecting them onto the same plane
            Vect point1 = basePoint;
            Vect point2 = faceAPoints[faceAlen / 3] - ((faceAPoints[faceAlen / 3] - basePoint) * planeNormal) * planeNormal;
            Vect point3 = faceAPoints[2 * faceAlen / 3] - ((faceAPoints[2 * faceAlen / 3] - basePoint) * planeNormal) * planeNormal;

            Vect pointCenter = Vect.GetCircleCenter(point1, point2, point3);
            double radius = (basePoint - pointCenter).Norm;
            double averagedRadius = 0;
            for (int i = 0; i < faceAlen; i++)
            {
                Vect point = faceAPoints[i];
                Vect pointProjected = point - ((point - basePoint) * planeNormal) * planeNormal;

                double distance = (pointProjected - pointCenter).Norm;
                double difference = Math.Abs(distance - radius) / radius;
                if (difference > 0.01) // 1% tolerance. this is due to the imprecision of splines and doubles
                {
                    // too different from the radius, we discard
                    faceACylindrical = false;
                    break;
                }
                averagedRadius += distance;
            }
            averagedRadius /= faceAlen;

            if (faceACylindrical) // if face A checks out, we continue and check faceB along the same circle
            {
                for (int i = 0; i < faceBlen; i++)
                {
                    Vect point = faceBPoints[i];
                    Vect pointProjected = point - ((point - basePoint) * planeNormal) * planeNormal;

                    double distance = (pointProjected - pointCenter).Norm;
                    double difference = Math.Abs(distance - radius) / radius;
                    if (difference > 0.01) // %1 tolerance
                    {
                        // too different from the radius, we discard
                        faceACylindrical = false;
                        break;
                    }
                }

                if (faceBCylindrical) // both faces are cylindrical, we change curve type to cylinder and calculate its properties.
                {

                    Vect vect1 = (basePoint - pointCenter).Direction;
                    Vect vect2 = (profileDirection ^ vect1).Direction;

                    Matrix cylinderMatrix = new Matrix(vect1, vect2, profileDirection);
                    cylinderMatrix.ApplyToLocal(wallPieceLocal);
                    wallPieceLocal.Px = pointCenter[0];
                    wallPieceLocal.Py = pointCenter[1];
                    wallPieceLocal.Pz = pointCenter[2];

                    wallPieceElement.ElementDoubles = new double[] { averagedRadius };
                    wallPieceElement.ElementType = GimElementType.F_cilinder;


                    return;
                }
            }

            //TODO: future surfaces

            //if it's not a cylinder, we just create a bspline surface
            if (faceACounterPart.ElementDoubles.Length == faceBCounterPart.ElementDoubles.Length
            && faceACounterPart.ElementType == GimElementType.C_bspline && faceBCounterPart.ElementType == GimElementType.C_bspline)
            {



                int degree = faceACounterPart.ElementInts[^1];
                int knotsCount = faceACounterPart.ElementInts[0];
                int multiplicitiesCount = faceACounterPart.ElementInts.Length - 2;
                int polesCount = (faceACounterPart.ElementDoubles.Length - (/*2 +*/6) - knotsCount) / 4;

                // each control point from each curve. if they have the same length, that's 2x control point count
                // + 2x weights + 1x knots, and finally 2 knots for the other direction (0,1).
                int doublesLength = polesCount * 3 * 2 + 2 * polesCount + multiplicitiesCount + 2;

                // there always must be 6 starting elements in F_Bspline. then we add the multiplicites
                int intsLength = 6 + multiplicitiesCount + 2;

                wallPieceElement.ElementDoubles = new double[doublesLength];
                wallPieceElement.ElementInts = new int[intsLength];

                // =====================
                // Element Doubles 
                // =====================

                // we will now copy the control points from c_bsplines.
                int index = 6; // control points start at position 6 in C_Bsplines

                // the point order is: A1 -> B1 -> A2 -> B2 -> ....
                int controlPointStartIndex = 0;
                int controlPointEndIndex = controlPointStartIndex + polesCount * 2 * 3;

                for (int i = controlPointStartIndex; i < controlPointEndIndex; i += 6)
                {
                    wallPieceElement.ElementDoubles[i] = faceACounterPart.ElementDoubles[index];
                    wallPieceElement.ElementDoubles[i + 1] = faceACounterPart.ElementDoubles[index + 1];
                    wallPieceElement.ElementDoubles[i + 2] = faceACounterPart.ElementDoubles[index + 2];


                    wallPieceElement.ElementDoubles[i + 3] = faceBCounterPart.ElementDoubles[index];
                    wallPieceElement.ElementDoubles[i + 4] = faceBCounterPart.ElementDoubles[index + 1];
                    wallPieceElement.ElementDoubles[i + 5] = faceBCounterPart.ElementDoubles[index + 2];

                    index += 3;
                }

                // now the weights. same A1 -> B1 -> A2 -> B2...
                index = 6 + polesCount * 3 + knotsCount;

                int weightStartIndex = polesCount * 2 * 3;
                int weightEndIndex = weightStartIndex + polesCount * 2;

                for (int i = weightStartIndex; i < weightEndIndex; i += 2)
                {
                    wallPieceElement.ElementDoubles[i] = faceACounterPart.ElementDoubles[index];
                    wallPieceElement.ElementDoubles[i + 1] = faceBCounterPart.ElementDoubles[index];
                    index++;
                }

                // the final part is the knots, but we must normalize them from 0 -> 1.
                // unlike control points and weights, knots are only inserted once
                double smallestKnot = faceACounterPart.ElementDoubles[polesCount * 3 + 6];
                double biggestKnot = faceACounterPart.ElementDoubles[polesCount * 3 + 6 + knotsCount - 1];

                // we will use the formula newKnot = (oldknot - minknot) / (maxknot - minknot)
                double divider = (biggestKnot - smallestKnot);

                index = 6 + polesCount * 3;

                int knotStartIndex = polesCount * 3 * 2 + polesCount * 2;
                int knotEndIndex = knotStartIndex + knotsCount;

                for (int i = knotStartIndex; i < knotEndIndex; i++)
                {
                    double knot = faceACounterPart.ElementDoubles[index];
                    wallPieceElement.ElementDoubles[i] = (knot - smallestKnot) / divider;
                    index++;
                }

                // finally, the last two knots of the other direction which are 0 - 1

                wallPieceElement.ElementDoubles[^2] = 0;
                wallPieceElement.ElementDoubles[^1] = 1;

                // =============
                // Element Ints
                // =============

                // poles count = c_bspline poles x 2
                wallPieceElement.ElementInts[0] = polesCount * 2;
                // u degree = C_bspline degree
                wallPieceElement.ElementInts[1] = degree;
                // v degree = 1 => straight line
                wallPieceElement.ElementInts[2] = 1;
                // u knots count  =  C_bspline knot count
                wallPieceElement.ElementInts[3] = knotsCount;
                // u poles count = C_bspline pole count
                wallPieceElement.ElementInts[4] = polesCount;
                // multiplicity count = C_bspline multiplicities + 2 for V direction
                wallPieceElement.ElementInts[5] = multiplicitiesCount + 2;

                // now we insert the multiplicites of the c_bspline
                int multiplicityStart = 6;
                int multiplicityEnd = 6 + multiplicitiesCount;

                index = 1;
                for (int i = multiplicityStart; i < multiplicityEnd; i++)
                {
                    wallPieceElement.ElementInts[i] = faceACounterPart.ElementInts[index];
                    index++;
                }

                // final two multiplicities for the v direction. since it's a clamped line => 2 - 2
                wallPieceElement.ElementInts[^2] = 2;
                wallPieceElement.ElementInts[^1] = 2;
                return;
            }



        }

    }
}