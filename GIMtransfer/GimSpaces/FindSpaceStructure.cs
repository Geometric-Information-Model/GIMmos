using GeometryGym.STEP;
using GIMcompress;
using GIMtransfer.BrepToPoly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMtransfer.GimSpaces
{
    internal class FindSpaceStructure
    {
        private static void AnalyseSpaceStructureFromGimBim(GimData gimData)
        {
            int accuracy = 500; // used to decide when something is big enough for a space or small enough for a divider
            gimData.elements.Add(new GimElement { ElementType = GimElementType.R_space }); // add main element for spaces
            uint spaceRootID = (uint)gimData.elements.Count; // good to know where we started this
            uint lastElement = (uint)gimData.elements.Count;

            // create a list containing outerspace and all innerspaces. elementType = solid
            List<Tuple<GimElement, uint>> spaces = FindAllspaces(gimData, spaceRootID, accuracy, lastElement); 
            lastElement = (uint)gimData.elements.Count;

            // create a list containing all cornerpoints of all SpaceDividers
            List<List<SpaceDividerLinePoint>> spaceDividerLinePoints = FindAllspaceDividerLinePoints(gimData, spaces);
            lastElement = (uint)gimData.elements.Count;

            // create a list of all SpaceDividers including SpaceDividerLinePoint 
            List<GimElement> spaceDividers = FindAllSpaceDividers(gimData, spaces, spaceDividerLinePoints, lastElement);
            lastElement = (uint)gimData.elements.Count;

            return;
        }
        private static List<Tuple<GimElement, uint>> FindAllspaces(GimData gimData, uint spaceRootID, int accuracy, uint lastElement)
        {
            List<Tuple<GimElement,uint>> allSpaces = new List<Tuple<GimElement, uint>>();
            gimData.elements.Add(new GimElement { ElementType = GimElementType.D_folder }); // the space is a folder containing the boundries and optional internal elements
            gimData.AddChildToParent(lastElement, spaceRootID);
            GimObject buitenruimte = GimObjectFactory.Create(0, "buitenruimte");
            buitenruimte.id = lastElement++;
            gimData.objects.Add(buitenruimte);
            gimData.elements.Add(new GimElement { ElementType = GimElementType.D_part });
            gimData.AddChildToParent(lastElement++, lastElement);
            GimElement gimElement = new GimElement { ElementType = GimElementType.B_solid };
            allSpaces.Add(new Tuple<GimElement, uint>(gimElement, lastElement));
            gimData.elements.Add(gimElement);
            gimData.AddChildToParent(lastElement++, lastElement);

            //TODO: copy all faces which are fysically touching the outerspace to the B_solid.
            //      Lower elements like wire and curve should not be copied but remain linked.

            //TODO: create list of new D_folder > D_part > B_solid elements of all inner spaces of the building wherein a sphere of 'accuracy' fits

            //TODO: cut the left-overs from the Faces to create a proper B_solid.
            //      if needed for gimData, we should copy any effected wire.
            //    
            //    ┌───┤        ┌───┐
            //    │  ─┤        │   │
            //  ──┴───┴────    └───┘

            return allSpaces;
        }
        private static List<List<SpaceDividerLinePoint>> FindAllspaceDividerLinePoints(GimData gimData, List<Tuple<GimElement, uint>> spaces)
        {
            //create a list of all cornerpoints of all spaces.
            List<SpaceDividerLinePoint> spaceVertex = new List<SpaceDividerLinePoint>();
            foreach (Tuple<GimElement, uint> space in spaces) { GetAllVertex(gimData, space, spaceVertex); }

            List<List<SpaceDividerLinePoint>> allSpaceDividerLinePointsGrouped = new List<List<SpaceDividerLinePoint>>();
            //TODO: Group all points in one GimElement when all are in a maximized boundingbox of 'accuracy'
            //
            //   ┌──       ───       ──┐
            //   │ ┌       ┐ ┌       ┐ │
            // 
            //         1         2
            //
            //   │ └       ┘ └       ┘ │
            //   └──       ───       ──┘
            //

            //TODO: a lot of complex edge-cases will occur. spaceDividers will come together in:
            //      - clear L
            //      - clear | where the angle is 180deg
            //      - unclear L where the angle is near to 180deg. 
            //      - clear T
            //      - clear X
            //      - or just end in the void

            //List<GimElement> allSpacesDividerPoints = new List<GimElement>(); // ElementType = B_spaceDividerLinePoint
            //foreach (List<SpaceDividerLinePoint> spaceDividerLinePoint in allSpaceDividerLinePointsGrouped)
            //{
            //    gimData.elements.Add(new GimElement { ElementType = GimElementType.B_spaceDividerLinePoint }); 
            //    gimData.AddChildToParent(lastElement, spaceRootID);
            //}
            //TODO: write List<SpaceDividerLinePoint> as GimElement and add to allSpacesDividerPoints
            return allSpaceDividerLinePointsGrouped;
        }
        private static List<GimElement> FindAllSpaceDividers(GimData gimData, List<Tuple<GimElement, uint>> spaces, List<List<SpaceDividerLinePoint>> spaceDividerLinePoints, uint lastElement)
        {
            List<GimElement> spaceDividers = new List<GimElement>();
            // foreach space in spaces  
            // foreach Face in space
            // foreach wire in Face
            // find SpaceDividerLinePoint 
            // connect via wire(s) to next SpaceDividerLinePoint
            // create Profile via wire(s)
            // create D_folder with:
                // space plane
                // opposite plane
                // SpaceDividerLinePoints
                // profiles
            return spaceDividers;
        }
        internal class SpaceDividerLinePoint
        {
            internal double x;
            internal double y;
            internal double z;
            internal uint space;
        }
        private static List<SpaceDividerLinePoint> GetAllVertex(GimData gimData, Tuple<GimElement, uint> space, List<SpaceDividerLinePoint> vertexes)
        {
            uint solid = 0;
            foreach(int shellID in space.Item1.elements)
            {
                GimElement shell = gimData.elements[shellID];
                foreach(int wireID in  shell.elements)
                {
                    GimElement wire = gimData.elements[wireID];
                    foreach(int lineID  in wire.elements)
                    {
                        GimElement line = gimData.elements[lineID];
                        foreach (GimLocal local in line.Locals)
                        {
                            if(local.parent == lineID)
                            {
                                // TODO: correct to global values
                                SpaceDividerLinePoint spaceDividerPoint = (new SpaceDividerLinePoint
                                {
                                    x = local.px,
                                    y = local.py,
                                    z = local.pz,
                                    space = space.Item2
                                });
                                if (!vertexes.Contains(spaceDividerPoint))
                                {
                                    vertexes.Add(spaceDividerPoint);
                                }
                            }
                        }
                    }
                }
            }



            foreach (int shellID in space.Item1.elements)
            {

                if (gimData.elements[shellID].elementType == GimElementType.shell)
                {
                    GimElement shell = gimData.elements[shellID];
                    foreach (GimLocal gimLocal in gimData.elements[shellID].locals)
                    {

                    }
                }
            }
            return vertexes;
        }
        private static bool elementTypeIsFace(GimElementType elementType)
        {
            bool face = false;
            switch (elementType)
            {
                case GimElementType.F_bezier:
                case GimElementType.F_bspline:
                case GimElementType.F_cone:
                case GimElementType.F_cilinder:
                case GimElementType.F_extrusion:
                case GimElementType.F_offset:
                case GimElementType.F_plane:
                case GimElementType.F_revolution:
                case GimElementType.F_sphere:
                case GimElementType.F_toroid:
                case GimElementType.F_trimmed:
                    face = true;
                    break;
            }
            return face;
        }
    }
}
