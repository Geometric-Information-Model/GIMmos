using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeometryGym.Ifc;
using GIMcompress;

namespace GIMtransfer.read.IFCReader
{
    public class IFC2Reader
    {

        public static bool FacetedBrepReader(IfcRepresentationItem item, GimData gimData, int solidBodyElementIndex, Vect generalLocationGIM, Matrix transform)
        {
            bool removed = false;
            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);
            IfcFacetedBrep facetedBrep = (IfcFacetedBrep)item;
            var closedShell = facetedBrep.Outer;
            var faces = closedShell.CfsFaces;
            if( faces.Count == 0 )
            {
                removed = true;
                return removed;
            }
            foreach (var face in faces)
            {
                var faceBounds = face.Bounds.ToList();
                var faceOuterBounds = faceBounds[0];
                IfcPolyLoop polyLoop = (IfcPolyLoop)faceOuterBounds.Bound;
                IFCReadElements.CreatePlane(polyLoop, gimData, shellElementIndex, generalLocationGIM, transform);
            }
            return removed;
        }
        public static void ShellBasedSurfaceModelReader(IfcRepresentationItem insideItem, GimData gimData, int solidBodyElementIndex, Vect generalLocationGIM, Matrix transform)
        {


            GimElement shellElement = new() { elementType = GimElementType.shell };
            int shellElementIndex = gimData.elements.Count;
            gimData.elements.Add(shellElement);

            gimData.AddChildToParent((uint)shellElementIndex, (uint)solidBodyElementIndex);
            IfcShellBasedSurfaceModel shellItem = (IfcShellBasedSurfaceModel)insideItem;
            var openShells = shellItem.SbsmBoundary.ToList();
            foreach (var openShell in openShells)
            {

                var faces = openShell.CfsFaces;
                foreach (var face in faces)
                {
                    var faceBounds = face.Bounds.ToList();
                    var faceOuterBounds = faceBounds[0];
                    IfcPolyLoop polyLoop = (IfcPolyLoop)faceOuterBounds.Bound;
                    IFCReadElements.CreatePlane(polyLoop, gimData, shellElementIndex, generalLocationGIM, transform);
                }
            }
        }

        public static bool ExtrudedFaceReader(IfcRepresentationItem item, GimData gimData, int solidBodyElementIndex, Vect generalLocationGIM, Matrix transform, bool hasHoles
            , List<List<Vect>> holes, int baseAxis)
        {
            bool removed = false;
            removed =IFCExtrusionReader.ReadExtursion(item, gimData, solidBodyElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
            return removed;
        }


        public static bool MappedItemReader(IfcRepresentationItem item, GimData gimData, int solidBodyElementIndex, Vect generalLocationGIM, Matrix transform, bool hasHoles
            , List<List<Vect>> holes, int baseAxis)
        {
            bool removed = false;
            IfcMappedItem mappedItem = (IfcMappedItem)item;
            var mappingSource = mappedItem.MappingSource;
            var mappingTarget = mappedItem.MappingTarget;
            var localOrigin = mappingTarget.LocalOrigin;
            Vect localOriginGIM = new(localOrigin.CoordinateX, localOrigin.CoordinateY, localOrigin.CoordinateZ);
            localOriginGIM = localOriginGIM * transform;
            generalLocationGIM[0] += localOriginGIM[0];
            generalLocationGIM[1] += localOriginGIM[1];
            generalLocationGIM[2] += localOriginGIM[2];
            if (mappingSource is IfcRepresentationMap)
            {
                if (mappingSource.MappedRepresentation is IfcShapeRepresentation)
                {
                    var rep = mappingSource.MappedRepresentation;
                    var items = rep.Items;
                    foreach (var insideItem in items)
                    {

                        if (insideItem is IfcFacetedBrep)
                        {
                            removed = FacetedBrepReader(insideItem, gimData, solidBodyElementIndex, generalLocationGIM, transform);
                        }
                        else if (insideItem is IfcShellBasedSurfaceModel)
                        {

                            ShellBasedSurfaceModelReader(insideItem, gimData, solidBodyElementIndex, generalLocationGIM, transform);
                        }
                        else if (insideItem is IfcExtrudedAreaSolid)
                        {
                            removed = ExtrudedFaceReader(insideItem, gimData, solidBodyElementIndex, generalLocationGIM, transform, hasHoles, holes, baseAxis);
                            if (removed)
                                return removed;
                        }
                        else if (!(insideItem is IfcShellBasedSurfaceModel) && !(insideItem is IfcFacetedBrep) && !(insideItem is IfcExtrudedAreaSolid))
                        {
                            
                            string debug = "debug";
                            removed = true;
                            if (removed)
                                return removed;

                        }
                    }

                }



            }
            return removed;
        }
    }
}
