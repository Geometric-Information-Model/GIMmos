using ACadSharp.Entities;
using GimUtilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GIMcompress
{
    public class GimElement : IGimElement, IReadOnlyGimElement
    {
        public List<GimLocal> locals = new List<GimLocal>();  // xyz-start and xy-directions
                                                              //public uint parent;
        private bool internOnly = false; // if true, the elemenet is not visible from the outside considering all sisters
        public GimElementType elementType = GimElementType.undefined; // based on this type, an array of values can be applicable

        public double[] elementDoubles; // for description, see GimElementType
        public int[] elementInts;       // for description, see GimElementType
        public int[] elementBools;      // Only for GimElements containing Boolean Operations
        private byte[] elementBytes;     // Only for GimElements containing Files (GimTexture)

        public List<uint> elements = new List<uint>(); // pointer to GimData.elements

        public bool orientation = true; // if true a Forward orrientation, if false an reversed orientation. this is used by cadex to identify the start o a curve or a plane. E.G is a curve starts from A to B or B to A. has nothing to do with mirroing

        #region properties
        public List<GimLocal> Locals
        {
            get => locals;
            set => locals = value;
        }

        public bool InternOnly
        {
            get => internOnly;
            set => internOnly = value;
        }

        public GimElementType ElementType
        {
            get => elementType;
            set => elementType = value;
        }

        ReadOnlyCollection<double> IReadOnlyGimElement.ElementDoubles
        {
            get
            {
                if (elementDoubles == null)
                    return null;
                else
                    return Array.AsReadOnly(elementDoubles);
            }
        }

        public double[] ElementDoubles
        {
            get => elementDoubles;
            set => elementDoubles = value;
        }

        public int[] ElementInts
        {
            get => elementInts;
            set => elementInts = value;
        }

        ReadOnlyCollection<int> IReadOnlyGimElement.ElementInts
        {
            get
            {
                if (elementInts == null)
                    return null;
                else
                    return Array.AsReadOnly(elementInts);
            }
        }

        public byte[] ElementBytes
        {
            get => elementBytes;
            set => elementBytes = value;
        }

        ReadOnlyCollection<byte> IReadOnlyGimElement.ElementBytes
        {
            get
            {
                if (ElementBytes == null)
                    return null;
                else
                    return Array.AsReadOnly(ElementBytes);
            }
        }

        public int[] ElementBools
        {
            get => elementBools;
            set => elementBools = value;
        }

        ReadOnlyCollection<int> IReadOnlyGimElement.ElementBools
        {
            get
            {
                if (ElementBools == null)
                    return null;
                else
                    return Array.AsReadOnly(ElementBools);
            }
        }

        public List<uint> Elements
        {
            get => elements;
            set => elements = value;
        }

        public bool Orientation
        {
            get => orientation;
            set => orientation = value;
        }

        IReadOnlyList<IReadOnlyGimLocal> IReadOnlyGimElement.Locals => Locals;
        IReadOnlyList<uint> IReadOnlyGimElement.Elements => Elements;
        #endregion

        // Quick GimElement Compare
        public bool CompareWithoutLocals(IReadOnlyGimElement g)
        {
            if (elementType != g.ElementType) return false;

            if (elements != null && g.Elements != null)
            {
                if (!Enumerable.SequenceEqual(elements, g.Elements))
                    return false;
            }
            else if (!(elements == null && g.Elements == null))
                return false;

            if (Orientation != g.Orientation) return false;

            if (elementDoubles != null && g.ElementDoubles != null)
            {
                if (elementDoubles.Length != g.ElementDoubles.Count) return false; // unequal array size
                for (int i = 0; i < elementDoubles.Length; i++)
                {
                    if (!VectorHelpers.CompareDouble(elementDoubles[i], g.ElementDoubles[i])) return false;
                }
                if (!Enumerable.SequenceEqual(elementDoubles, g.ElementDoubles)) return false;
            }
            else if (!(elementDoubles == null && g.ElementDoubles == null)) return false;

            if (elementInts != null && g.ElementInts != null)
            {
                if (!Enumerable.SequenceEqual(elementInts, g.ElementInts)) return false;
            }
            else if (!(elementInts == null && g.ElementInts == null)) return false;
            return true;
        }

        public GimElement Clone()
        {
            GimElement clone = new GimElement();
            if (locals != null)
            {
                clone.locals = new List<GimLocal>();
                foreach (GimLocal l in locals)
                    clone.locals.Add(l.Clone());
            }
            //clone.parent = parent;
            //clone.gimScale = gimScale;
            clone.internOnly = internOnly;
            clone.elementType = (GimElementType)((int)elementType);
            clone.Orientation = Orientation;
            if (elementDoubles != null) clone.elementDoubles = (double[])elementDoubles.Clone();
            if (elementInts != null) clone.elementInts = (int[])elementInts.Clone();
            if (elementBytes != null) clone.elementBytes = (byte[])elementBytes.Clone();
            if (elements != null) clone.elements = new List<uint>(elements);
            return clone;
        }

        public GimMaterial AsMaterial()
        {
            return new GimMaterial()
            {
                elementInts = elementInts,
                elementDoubles = elementDoubles,
                elementType = elementType,
                elements = elements,
                orientation = orientation,
            };
        }

        public bool Compare(IReadOnlyGimElement g)
        {
            if (!CompareWithoutLocals(g)) return false;

            if (locals != null && g.Locals != null && locals.Count > 0 && g.Locals.Count > 0)
            {
                if (!locals[0].Compare(g.Locals[0])) return false;
            }
            else if (!((locals == null || locals.Count == 0) && (g.Locals == null || g.Locals.Count == 0))) return false;

            return true;
        }

        public GimLocal GetFirstLocalThatBelongsToThisParentOfThisElement(int parent)
        {
            List<int> indices = GetIndexLocalOfParent(parent);
            if (indices.Count == 0)
                throw GimLogger.Error(new IndexOutOfRangeException());
            return locals[indices[0]];
        }

        public List<GimLocal> GetLocalsThatBelongsToThisParentOfThisElement(int parent)
        {
            List<GimLocal> gimLocals = new List<GimLocal>();
            foreach (int i in GetIndexLocalOfParent(parent))
            {
                gimLocals.Add(locals[i]);
            }
            return gimLocals;
        }

        public static List<GimLocal> GetChildElementLocals(int child, int thisIndex, GimData gimData)
        {
            List<GimLocal> gimLocals = new List<GimLocal>();
            GimElement childElement = gimData.Elements[child];
            foreach (GimLocal childLocal in childElement.Locals)
            {
                if (childLocal.Parent == thisIndex) gimLocals.Add(childLocal);
            }
            return gimLocals;
        }

        public static GimLocal GetNthChildElementLocal(int child, int thisIndex, GimData gimData, int localIndex = 0)
        {
            int counter = -1;
            GimElement childElement = gimData.Elements[child];
            foreach (GimLocal childLocal in childElement.Locals)
            {
                if (childLocal.Parent == thisIndex)
                {
                    counter++;
                    if (counter >= localIndex)
                    {
                        break;
                    }
                }
            }

            // if localIndex is too high, this always returns the last child
            if (counter >= 0)
            {
                return childElement.Locals[counter];
            }

            return null;
        }

        public static int GetNthChildElementLocalIndex(int child, int thisIndex, GimData gimData, int localIndex = 0)
        {
            int counter = -1;
            int index = -1;
            GimElement childElement = gimData.Elements[child];
            for (int i = 0; i < childElement.Locals.Count; i++)
            {
                GimLocal childLocal = childElement.Locals[i];
                if (childLocal.Parent == thisIndex)
                {
                    index = i;
                    counter++;
                    if (localIndex == counter) return index;
                }
            }

            // if localIndex is too high, this always returns the last child
            return index;
        }

        public static GimElement GetLineFromTwoPoints(Vect pointA, Vect pointB, int parent = 0)
        {
            Vect direction = (pointB - pointA).Direction;
            Vect basePoint = pointA;
            double length = (pointB - pointA).Norm;

            GimElement lineElement = new()
            {
                ElementType = GimElementType.C_line,
                ElementDoubles = new double[] { 0, length }
            };


            GimLocal lineLocal = new()
            {
                parent = (uint)parent,
                px = basePoint[0],
                py = basePoint[1],
                pz = basePoint[2]
            };
            direction.ApplyToLocal(lineLocal);

            lineElement.Locals.Add(lineLocal);

            return lineElement;
        }

        // returns children and their locals as a list of pairs
        // e.g. if Elements = [2, 3, 3, 4, 2], the return is
        // [element 2, local 1]
        // [element 3, local 1]
        // [element 3, local 2]
        // [element 4, local 1]
        // [element 2, local 2]

        public List<Tuple<GimElement, GimLocal>> GetAllChildrenOrdered(int thisIndex, GimData gimData)
        {
            List<Tuple<GimElement, GimLocal>> children = new();

            Dictionary<int, int> elementOccurenceIndex = new Dictionary<int, int>();

            foreach (int childId in elements)
            {
                if (elementOccurenceIndex.ContainsKey(childId)) continue;
                elementOccurenceIndex.Add(childId, 0);
            }

            foreach (int childId in elements)
            {
                GimElement childElement = gimData.Elements[childId];
                int localIndex = elementOccurenceIndex[childId];
                elementOccurenceIndex[childId] = localIndex + 1;

                GimLocal local = GetNthChildElementLocal(childId, thisIndex, gimData, localIndex);
                children.Add(new Tuple<GimElement, GimLocal>(childElement, local));
            }


            return children;

        }

        // does the same as the above function, but returns:
        // [the element index in gimData, the local index in the element]
        public List<Tuple<int, int>> GetAllChildrenIndexesOrdered(int thisIndex, GimData gimData)
        {
            List<Tuple<int, int>> children = new();

            Dictionary<int, int> elementOccurenceIndex = new Dictionary<int, int>();

            foreach (int childId in elements)
            {
                if (elementOccurenceIndex.ContainsKey(childId)) continue;
                elementOccurenceIndex.Add(childId, 0);
            }

            foreach (int childId in elements)
            {
                int localIndex = elementOccurenceIndex[childId];
                elementOccurenceIndex[childId] = localIndex + 1;

                int localPosition = GetNthChildElementLocalIndex(childId, thisIndex, gimData, localIndex);
                children.Add(new Tuple<int, int>(childId, localPosition));
            }


            return children;

        }

        private List<int> GetIndexLocalOfParent(int parent) // TODO: What to do if parent has multiple locals in this element?
        {
            List<int> localIndices = new List<int>();
            for (int i = 0; i < locals.Count; i++)
            {
                if (locals[i].parent == parent)
                {
                    localIndices.Add(i);
                }
            }
            return localIndices;
        }

        public override int GetHashCode() //Should be fine
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = elementType.GetHashCode();
                foreach (uint i in elements)
                {
                    hash += i.GetHashCode();
                }
                hash += orientation.GetHashCode();
                if (elementDoubles != null)
                    foreach (double d in elementDoubles)
                    {
                        hash += d.GetHashCode();
                    }
                if (elementInts != null)
                    foreach (int i in elementInts)
                    {
                        hash += i.GetHashCode();
                    }
                return hash;
            }
        }

        /// <summary>
        /// The ToString fucntion of GimElement writes the variables of the GimElement in a human readable format.
        /// </summary>
        /// <returns>A summary of the GimElement in a human readable format.</returns>
        public override string ToString()
        {
            string type = TypeToString();
            int elCount = 0;
            if (elements != null)
            {
                elCount = elements.Count;
            }

            int doubleCount = 0;
            if (elementDoubles != null)
            {
                doubleCount = elementDoubles.Length;
            }

            int localCount = 0;
            if (locals != null)
            {
                localCount = locals.Count;
            }

            int intCount = 0;
            if (elementInts != null)
            {
                intCount = elementInts.Length;
            }

            string result = type + ", " + elCount + " Children, " + localCount + " Locals, "
                + doubleCount + " doubles, " + intCount + " Ints";

            return result;
        }
        // used in the typeToString function
        //private static Dictionary<GimElementType, string> elementTypeString = new()
        //{
        //	{ GimElementType.D_root, "Root Element" },
        //	{ GimElementType.D_folder, "Folder" },
        //	{ GimElementType.D_link, "Link" },
        //	{ GimElementType.D_part, "Part" },
        //	{ GimElementType.R_BRep, "B-Representation" },
        //	{ GimElementType.R_Poly, "Poly Mesh Representation" },
        //	{ GimElementType.B_acorn, "Acorn Body" },
        //	{ GimElementType.B_general, "General Body" },
        //	{ GimElementType.B_sheet, "Sheet" },
        //	{ GimElementType.B_solid, "Solid Body" },
        //	{ GimElementType.undefined, "Undefined" },
        //	{ GimElementType.B_wireframe, "WireFrame Body" },
        //	{ GimElementType.solid, "Solid" },
        //	{ GimElementType.shell, "Shell" },
        //	{ GimElementType.wire, "Wire" },
        //	{ GimElementType.F_bezier, "Bezier Face" },
        //	{ GimElementType.F_bspline, "B-Spline Face" },
        //	{ GimElementType.F_cilinder, "Cylinder Face" },
        //	{ GimElementType.F_cone, "Cone Face" },
        //	{ GimElementType.F_extrusion, "Extrusion Face" },
        //	{ GimElementType.F_offset, "Offset Face" },
        //	{ GimElementType.F_plane, "Plane" },
        //	{ GimElementType.F_polygon, "Polygon Face" },
        //	{ GimElementType.F_revolution, "Revolution Face" },
        //	{ GimElementType.F_sphere, "Sphere" },
        //	{ GimElementType.F_toroid, "Torus/Toroid" },
        //	{ GimElementType.F_trimmed, "Trimmed Face" },
        //	{ GimElementType.P_points, "Points" },
        //	{ GimElementType.P_polygon, "Polygon" },
        //	{ GimElementType.P_polyline, "PolyLine" },
        //	{ GimElementType.P_vertex, "Vertex" },
        //	{ GimElementType.C_bezier, "Bezier Curve" },
        //	{ GimElementType.C_bezier2D, "Bezier 2D Curve" },
        //	{ GimElementType.C_bspline, "B-Spline Curve" },
        //	{ GimElementType.C_bspline2D, "B-Spline 2D Curve" },
        //	{ GimElementType.C_circle, "Circle" },
        //	{ GimElementType.C_circle2D, "Circle 2D" },
        //	{ GimElementType.C_elipse, "Ellipse" },
        //	{ GimElementType.C_elipse2D, "Ellipse 2D" },
        //	{ GimElementType.C_hyperbol, "Hyperbola" },
        //	{ GimElementType.C_hyperbol2D, "Hyperbola 2D" },
        //	{ GimElementType.C_line, "Line" },
        //	{ GimElementType.C_line2D, "Line 2D" },
        //	{ GimElementType.C_offset, "Offset Curve" },
        //	{ GimElementType.C_offset2D, "Offset 2D Curve" },
        //	{ GimElementType.C_parabol, "Parabola" },
        //	{ GimElementType.C_parabol2D, "Parabola 2D" },
        //	{ GimElementType.C_trimmed, "Trimmed Curve" },
        //	{ GimElementType.C_trimmed2D, "Trimmed 2D Curve" },
        //	{ GimElementType.Gim_CutSurface, "(Gim) CutSurface" },
        //	{ GimElementType.Gim_PolyPolyGon, "(Gim) Poly-Polygon" },
        //	{ GimElementType.Gim_ProfileDefinition, "(Gim) Profile Definition" },
        //	{ GimElementType.Gim_ProfileInstance, "(Gim) Profile Instance" },
        //	{ GimElementType.Gim_SinglePolygon, "(Gim) Single-Polygon" },
        //	{ GimElementType.Gim_ComplexSegment, "(Gim) Complex Segment" },
        //	{ GimElementType.Material, "Material" },
        //	{ GimElementType.Texture, "Texture" },
        //	{ GimElementType.Gim_Joint, "Joint" },
        //	{ GimElementType.ExternalReference, "External Reference" },
        //};

        /// <summary>
        /// Converts the element type of this GimElement as a string.
        /// </summary>
        /// <returns>The string version of the GimElementType of this GimElement.</returns>

        private string TypeToString()
        {

            string type;
            //         if (elementTypeString.ContainsKey(elementType))
            //         {
            //	return elementTypeString[elementType];
            //         }
            //return "Other";

            switch (elementType)
            {
                case GimElementType.D_root: type = "Root Element"; break;
                case GimElementType.D_folder: type = "Folder"; break;
                case GimElementType.D_part: type = "Part"; break;
                case GimElementType.R_BRep: type = "B-Representation"; break;
                case GimElementType.R_Poly: type = "Poly Mesh Representation"; break;
                case GimElementType.B_sheet: type = "Sheet"; break;
                case GimElementType.B_solid: type = "Solid Body"; break;
                case GimElementType.undefined: type = "Undefined"; break;
                case GimElementType.shell: type = "Shell"; break;
                case GimElementType.wire: type = "Wire"; break;
                case GimElementType.Seam: type = "Seam"; break;
                case GimElementType.F_bezier: type = "Bezier Face"; break;
                case GimElementType.F_bspline: type = "B-Spline Face"; break;
                case GimElementType.F_cilinder: type = "Cylinder Face"; break;
                case GimElementType.F_cone: type = "Cone Face"; break;
                case GimElementType.F_extrusion: type = "Extrusion Face"; break;
                case GimElementType.F_offset: type = "Offset Face"; break;
                case GimElementType.F_plane: type = "Plane"; break;
                case GimElementType.F_revolution: type = "Revolution Face"; break;
                case GimElementType.F_sphere: type = "Sphere"; break;
                case GimElementType.F_toroid: type = "Torus/Toroid"; break;
                case GimElementType.F_trimmed: type = "Trimmed Face"; break;
                case GimElementType.P_polygon: type = "Polygon"; break;
                case GimElementType.P_polyline: type = "PolyLine"; break;
                case GimElementType.P_vertex: type = "Vertex"; break;
                case GimElementType.C_bezier: type = "Bezier Curve"; break;
                case GimElementType.C_bezier2D: type = "Bezier 2D Curve"; break;
                case GimElementType.C_bspline: type = "B-Spline Curve"; break;
                case GimElementType.C_bspline2D: type = "B-Spline 2D Curve"; break;
                case GimElementType.C_circle: type = "Circle"; break;
                case GimElementType.C_circle2D: type = "Circle 2D"; break;
                case GimElementType.C_elipse: type = "Ellipse"; break;
                case GimElementType.C_elipse2D: type = "Ellipse 2D"; break;
                case GimElementType.C_hyperbol: type = "Hyperbola"; break;
                case GimElementType.C_hyperbol2D: type = "Hyperbola 2D"; break;
                case GimElementType.C_line: type = "Line"; break;
                case GimElementType.C_line2D: type = "Line 2D"; break;
                case GimElementType.C_offset: type = "Offset Curve"; break;
                case GimElementType.C_offset2D: type = "Offset 2D Curve"; break;
                case GimElementType.C_parabol: type = "Parabola"; break;
                case GimElementType.C_parabol2D: type = "Parabola 2D"; break;
                case GimElementType.C_trimmed: type = "Trimmed Curve"; break;
                case GimElementType.C_trimmed2D: type = "Trimmed 2D Curve"; break;
                case GimElementType.Dimension: type = "Dimension"; break;
                case GimElementType.Gim_ProfileInstance: type = "(Gim) Profile Instance"; break;
                case GimElementType.Gim_ComplexSegment: type = "(Gim) Complex Segment"; break;
                case GimElementType.Gim_Boolean: type = "(Gim) Boolean operation"; break;
                case GimElementType.Material: type = "Material"; break;
                case GimElementType.Texture: type = "Texture"; break;
                case GimElementType.Gim_Joint: type = "Joint"; break;
                case GimElementType.ExternalReference: type = "External Reference"; break;
                case GimElementType.Text: type = "Text"; break;
                case GimElementType.Hatch: type = "Hatch"; break;
                case GimElementType.PatternLine: type = "PatternLine"; break;
                case GimElementType.F_symbol: type = "Symbol"; break;
                case GimElementType.R_2DRep: type = "2D Representation"; break;
                case GimElementType.Viewport: type = "Viewport"; break;
                default: type = "Other"; break;
            }
            return type;
        }

        /// <summary>
        /// Return whether a double index is a distance value or not.
        /// </summary>
        /// <param name="i"> The index of the doubles to be evaluated. If out of range of the doubles will return false. </param>
        /// <returns> Whether a double index is a distance value or not. </returns>
        public bool IsDoubleDistance(uint i)
        {
            switch (elementType)
            {
                case GimElementType.F_bezier:
                    return i < ElementDoubles.Length / 4 * 3; // Doubles contains x, y and z for poles in the beginning, and a weight for all poles.
                case GimElementType.F_bspline:
                    return i < ElementInts[0] * 3; // x, y and z for poles in the beginning.
                case GimElementType.F_cone:
                    return i == 0; // 0 is radius, otherwise not.
                case GimElementType.F_cilinder:
                    return true; // Only radius.
                case GimElementType.F_offset:
                    return false; // Only offset.
                case GimElementType.F_sphere:
                    return true; // Only radius.
                case GimElementType.F_toroid:
                    return true; // Only two radii.
                case GimElementType.F_trimmed:
                    GimLogger.Warn("Trimmed may be slightly wrong"); // TODO: figure out
                    return false;
                case GimElementType.C_bezier:
                    return i < elementDoubles.Length - 2; // First 6 values vertex coords distance, final two values not distance.
                case GimElementType.C_bspline:
                    return i < (elementDoubles.Length - 6 - elementInts[0] / 4 * 3) + 6; // First 6 values vertex coords distance, next 3 values per pole.
                case GimElementType.C_circle:
                    return i == 0; // First is radius.
                case GimElementType.C_elipse:
                    return i < 2; // First two radii.
                case GimElementType.C_hyperbol:
                    return i == 2 || i == 3; // Last two radii.
                case GimElementType.C_line:
                    return false; // Only params.
                case GimElementType.C_parabol:
                    return i == 2; // Focal is distance from origin.
                case GimElementType.C_offset:
                    return false; // Only offset.
                case GimElementType.C_trimmed:
                    return false; // Only params.
                case GimElementType.C_line2D:
                    return true;
                case GimElementType.C_circle2D:
                    return i == 0;
                case GimElementType.C_offset2D:
                    return true;
                case GimElementType.C_trimmed2D:
                    return false;
                case GimElementType.C_elipse2D:
                    return i < 2;
                case GimElementType.C_hyperbol2D:
                    return i == 2 || i == 3;
                case GimElementType.C_parabol2D:
                    return i == 2;
                case GimElementType.C_bezier2D:
                    return i < elementDoubles.Length - 2;
                case GimElementType.C_bspline2D:
                    return i < 4 + 2 * ((elementDoubles.Length - 4 - elementInts[0]) / 3);
                case GimElementType.Material:
                    return false;
                case GimElementType.P_polygon:
                    return true; // All coordinates.
                case GimElementType.Gim_Joint:
                    return false; // TODO: Check this
                default:
                    if (elementDoubles != null && elementDoubles.Length > 0)
                        GimLogger.Error(new NotImplementedException(elementType + "has doubles but isn't implemented as such."));
                    return false;
            }
        }
    }

	public enum GimElementType // Numbers free: 52, 53, 60 // Can't go higher then 63
	{
		// Document types
		D_root = 0,      // Element 0, the file representation.
		D_folder = 1,    // Contains multiple elements (Windows Explorer folder equivalent)
        D_partScalable = 2, 
        D_part = 3,      // (CADEX part equivalent)

        // Representation elements
        R_BRep = 4,      // Breppresentation
        R_Poly = 5,      // Polyrepresentation
        R_2DRep = 6,     // 2D Representation
        R_space = 7,     // Not in use yet. See FindSpaceStructure.cs

        // Body elements
        B_sheet = 8,
        B_solid = 9,     // elementDoubles: points
        undefined = 10,
        B_spaceDividerLinePoint = 11, // elementDoubles: XYZ,XYZ,XYZ;
                                  // elementInts: R_space.ID,R_space.ID,R_space.ID
        B_spaceDivider = 12,  // 
        shell = 13,
        wire = 14,

        // Face elements
        F_bezier = 15,    // elementDoubles: [0]PoleA.X, [1]PoleA.Y, [2]PolyA.Z, [3]PoleB.X, [4]PoleB.Y, [5]PoleB.Z, [6]..[x], [x+1]PoleA.Weight, [x+2]PoleB.Weight, [x+3]..
        F_bspline = 16,   // elementDoubles: [0]PoleA.X, [1]PoleA.Y, [2]PolyA.Z, [3]PoleB.X, [4]PoleB.Y, [5]PoleB.Z, [6]..[ ]UKnots, [ ]..[ ]VKnots...
                          // elementInts: [0]Poly.Count, [1]UDegree, [2]VDegree, [3]UKnots.Count, [4]UPoles.Count, [5]UMultiplicities.Count, [6]..[ ]UMultiplicities, [ ]..[ ]VMultiplicities...
        F_cone = 17,      // elementDoubles: [0]radius, [1]semi-angle
        F_cilinder = 18,  // elementDoubles: [0]radius
        F_extrusion = 19, //	elements: 1
        F_offset = 20,    // elementDoubles: [0]offsetMultiplication
                          // elements: 1  
        F_plane = 21,
        F_revolution = 22,// elements: [0]curve
        F_sphere = 23,    // elementDoubles: [0]radius
        F_toroid = 24,    // elementDoubles: [0]Major Radius, [1]Minor Radius, ----- previously: [0]cut-radius, [1]Outer-radius
        F_trimmed = 25,   // elementDoubles: [0]UMin, [1]UMax, [2]VMin, [3]VMax
                          // ModelData_RectangularTrimmedSurface
        F_symbol = 28,

        Seam = 29,      // 0 doubles; topological seam marker (child of 3D curve)

        // Poly elements
        P_polygon = 26,   // elements: [0]segment, [1]...
        P_polyline = 27,  // elements: [0]segment, [1]...
                          // <NEW> added for different poly representations
        P_vertex = 30,
        // </NEW>

        // Curve elements
        C_bezier = 31,    // elementDoubles: [0]startX, [1]startY, [2]startZ, [3]endX, [4]endY, [5]endZ, [6]PoleA.X, [7]PoleA.Y, [8]PolyA.Z, [9]PoleB.X, [10]PoleB.Y, [11]PoleB.Z, [12]..[x], [x+1]aParamFirst, [x+2]aParamLast
        C_bspline = 32,   // elementDoubles: [0]startX, [1]startY, [2]startZ, [3]endX, [4]endY, [5]endZ, [6]PoleA.X, [7]PoleA.Y, [8]PolyA.Z, [9]PoleB.X, [10]PoleB.Y, [11]PoleB.Z, [12]..[]Knots.A, []..[]Knots.B, ... []..[]Weights, ...
                          // elementInts: [0]Number of Knots, [1]Multiplicity A, [2...]Multiplicity B, ..., [N] Degree
        C_circle = 33,    // elementDoubles: [0]radius, [1]aParamFirst, [2]aParamLast
        C_elipse = 34,    // elementDoubles: [0]MajorRadius, [1]MinorRadius, [2]aParamFirst, [3]aParamLast
        C_hyperbol = 35,  // elementDoubles: [0]aParamFirst, [1]aParamLast, [2]MajorRadius, [3]MinorRadius
        C_line = 36,      // elementDoubles: [0]aParamFirst, [1]aParamLast,
        C_parabol = 37,   // elementDoubles: [0]aParamFirst, [1]aParamLast, [2]Focal
        C_offset = 38,    // elementDoubles: [0] offset
                          // direction = local.zx, local.zy, local.zz
        C_trimmed = 39,   // elementDoubles: [0] aParamFirst, [1] aParamLast
                          // 2D curves — mirror 3D counterparts; placement via GimLocal (Px/Py=origin, Xx/Xy=U, Yx/Yy=V, Zx/Zy=direction for line)
        C_bezier2D = 40,  // elementDoubles: [0]startU, [1]startV, [2]endU, [3]endV, [4]PoleA.U, [5]PoleA.V, [6]PoleB.U, [7]PoleB.V, [8]..[x], [x+1]aParamFirst, [x+2]aParamLast
        C_bspline2D = 41, // elementDoubles: [0]startU, [1]startV, [2]endU, [3]endV, [4]PoleA.U, [5]PoleA.V, [6]PoleB.U, [7]PoleB.V, [8]..[]Knots, []..[]Weights
                           // elementInts: [0]Number of Knots, [1]Multiplicity A, [2...]Multiplicity B, ..., [N] Degree
                           // start param = knot[degree], end param = knot[n-degree]
        C_circle2D = 42,  // elementDoubles: [0]radius, [1]aParamFirst, [2]aParamLast
        C_elipse2D = 43,  // elementDoubles: [0]MajorRadius, [1]MinorRadius, [2]aParamFirst, [3]aParamLast
        C_hyperbol2D = 44,// elementDoubles: [0]aParamFirst, [1]aParamLast, [2]MajorRadius, [3]MinorRadius
        C_line2D = 45,    // elementDoubles: [0]aParamFirst, [1]aParamLast
        C_parabol2D = 46, // elementDoubles: [0]aParamFirst, [1]aParamLast, [2]Focal
        C_offset2D = 47,  // elementDoubles: [0]offset; direction = local Zx, Zy
        C_trimmed2D = 48, // elementDoubles: [0]aParamFirst, [1]aParamLast


        // Gim Elements
        Gim_ProfileInstance = 49,   // 3 types of child elements: ProfileDefinition, polyline, CutSurfaces (optional)
        Gim_ComplexSegment = 54,    // groups multiple wallpiece segments together.
        Gim_Joint = 55,             // ElementInts: [0]GimJointType, [1]Parent, [2]Child
                                    // ElementDoubles: [0]ax, [1]ay, [2]az, [3]bx, [4]by, [5]bz,
                                    // [6]Calibration.Raising, [7]Calibration.Falling],
                                    // [8]Dynamics.Damping, [9]Dynamics.Friction,
                                    // [10]Limit.Lower, [11]Limit.Upper, [12]Limit.Effort, [13]Limit.Velocity,
                                    // [14]Mimic.Joint, [15]Mimic.Multiplier, [16]Mimic.Offset
        Gim_Boolean = 56,           // ElementBools [0] [1] type 0,1,2(,3) 0=AND 1=OR 2=NOT
                                    // Elements [0]main.local(), [1]sub.local()
                                    // .locals()

        // Texture types
        Material = 57,
        PatternLine = 63, //  elementDoubles: elementDoubles: [DashLength0, DashLength1, ..., DashLengthN-1]
        Texture = 58,
        ExternalReference = 59, // ElementInt: [0] ObjectId, [1] ProjectId (optional), [2] Sub_ProjectId (optional)

        // Extra elements
        Text = 61,		// elementDoubles: [0]InsertPoint.X, [1]InsertPoint.Y, [2]InsertPoint.Z, [3]Height, [4]Rotation, [5]VerticalAlignment, [6]HorizontalAlignment, ([7]LineSpacing) ElementBytes (Text)
        Hatch = 62,   // elementDoubles: [0]Child1.offsetX, [1]Child1.offsetY, [2]Child2.offsetX, [3]Child2.offsetY….)
        Dimension = 51,       // elements: elementInts: [0]: enum_type
        Viewport = 50   // elementDoubles: [0]CenterX, [1]CenterY, [2]CenterZ, [3]ViewCenterX, [4]ViewCenterY, [5]ViewCenterZ,
                        // [6]viewTargetX, [7]viewTargetY, [8]viewTargetZ. [9]viewDirectionX, [10]viewDirectionY, [11]viewDirectionZ,
                        // [12]width, [13]height, [14]viewHeight, [15]twistAngle, [16]lensLength, [17]aspectRatio


    }
}
