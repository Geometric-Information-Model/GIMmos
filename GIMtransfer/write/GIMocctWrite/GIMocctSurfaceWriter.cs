using GIMcompress;
using GIMocctCLI;
using GIMocctWrapper;
using GimUtilities;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GIMtransfer
{
    public static class GIMocctSurfaceWriter
    {
        static int curveNumber = 0;
        static int wireNumber = 0;
        private const double LineChainTolerance = 1e-6;
        public static TopoDSFaceWrapper WriteSurfaceElement(GimData gimData, int elementIndex, int localIndex)
        {
            
            TopoDSFaceWrapper finalFace = null;
            GimElement element = gimData.Elements[elementIndex];
            
            GimLocal local = element.Locals[localIndex];

            List<TopoDSEdgeWrapper> edges = new();

            var childrenAndLocals = element.GetAllChildrenIndexesOrdered(elementIndex, gimData);
            (int firstWireIndex, int firstWireLocalIndex) = childrenAndLocals[0];

            GimElement firstWireElement = gimData.Elements[firstWireIndex];
            GimLocal firstWireLocal = firstWireElement.Locals[firstWireLocalIndex];

            var surfaceCurvesAndLocals = firstWireElement.GetAllChildrenIndexesOrdered(firstWireIndex, gimData);

            int status = 0;

            CreateCurves(surfaceCurvesAndLocals, gimData, out var surfaceCurves, out var outerSeamInformation);

            // we force bind the seam edges to each other
            HashSet<int> edgesBound = new();
            for (int i = 0; i < outerSeamInformation.Count - 1; i++)
            {
                // force binding edges to each other
                if (outerSeamInformation[i] && !edgesBound.Contains(i))
                {
                    int curveIndex = surfaceCurvesAndLocals[i].Item1;
                    int curveLocalIndex = surfaceCurvesAndLocals[i].Item2;
                    GimElement curveElement = gimData.Elements[curveIndex];
                    var startEnd = CurveHelpers.getStartAndEndPointsOfCurve(curveElement, gimData, curveLocalIndex);
                    Vect start = startEnd[0..3];
                    Vect end = startEnd[3..6];

                    for (int j = i + 1; j < outerSeamInformation.Count; j++)
                    {
                        if (!outerSeamInformation[j] || edgesBound.Contains(j)) continue;

                        int otherCurveIndex = surfaceCurvesAndLocals[j].Item1;
                        int otherCurveLocalIndex = surfaceCurvesAndLocals[j].Item2;
                        GimElement otherCurve = gimData.Elements[otherCurveIndex];

                        if (otherCurve.ElementType != curveElement.ElementType) continue;

                        var otherStartEnd = CurveHelpers.getStartAndEndPointsOfCurve(otherCurve, gimData, otherCurveLocalIndex);
                        Vect otherStart = otherStartEnd[0..3];
                        Vect otherEnd = otherStartEnd[3..6];

                        if ((start == otherStart && end == otherEnd) || (start == otherEnd && end == otherStart))
                        {
                            surfaceCurves[j].ForceBindToShape(surfaceCurves[i]);
                            //surfaceCurves[j] = surfaceCurves[i];
                            edgesBound.Add(i);
                            edgesBound.Add(j);
                            break;
                        }
                    }

                }
            }

            WireBuilder wireBuilder = new();
            for(int si = 0; si < surfaceCurves.Count; si++)
            {
                var surfaceCurve = surfaceCurves[si];
                wireBuilder.Add(surfaceCurve);
                //var edgeVertices = surfaceCurve.GetVertices();
                //Vector3Wrapper lastVertex = wireBuilder.LastVertex();
                status = wireBuilder.ErrorCode();
                if(status != 0)
                {
                    string warn = "Issue when building one of the wires!";
                    if(si > 0)
                    {
                        var previousCurve = surfaceCurves[si - 1];
                        var previousStart = previousCurve.FirstVertex();
                        var previousEnd = previousCurve.LastVertex();
                        var currentStart = surfaceCurve.FirstVertex();
                        var currentEnd = surfaceCurve.LastVertex();

                        double tolerance = surfaceCurve.Tolerance();
                        Vector3Wrapper commonVertex;
                        bool hasCommon = surfaceCurve.CommonVertex(previousCurve, out commonVertex);
                    }
                }
            }
            bool isDone = wireBuilder.IsDone();
            //TopoDSWireWrapper outerWire = wireBuilder.GetWire();

#if DEBUG
            //var wireWriter = outerWire.GetWriter();
            //Directory.CreateDirectory(FileAccess.GetTempPath("wires"));
            //wireWriter.WriteStepFromMemory(FileAccess.GetTempPath("wires\\wire" + wireNumber + ".step"));
            wireNumber++;
#endif
            SurfaceBuilder surfaceBuilder = new();
            surfaceBuilder.SetSeamInformation(outerSeamInformation);
            TopoDSWireWrapper builtWire;
            switch (element.ElementType)
            {
                case GimElementType.F_cone: builtWire = WriteCone(surfaceBuilder, element, local, surfaceCurves);break;
                case GimElementType.F_plane: builtWire = WritePlane(surfaceBuilder, element, local, surfaceCurves); break;
                case GimElementType.F_toroid: builtWire = WriteTorus(surfaceBuilder, element, local, surfaceCurves); break;
                case GimElementType.F_sphere: builtWire = WriteSphere(surfaceBuilder, element, local, surfaceCurves); break;
                case GimElementType.F_bspline: builtWire = WriteBsplineSurface(surfaceBuilder, element, local, surfaceCurves); break;
                case GimElementType.F_cilinder: builtWire = WriteCylinderSurface(surfaceBuilder, element, local, surfaceCurves); break;
                default: builtWire = null; break;
            }

            if(builtWire!= null)
            {
                builtWire.SetOrientation(
                    firstWireElement.Orientation ?
                    (int)TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD :
                    (int)TopAbsWrapper_Orientation.TopAbsWrapper_REVERSED
                    );
            }

            SurfaceBuilderError faceStatus = surfaceBuilder.Error();


            foreach(var wireAndLocals in childrenAndLocals)
            {
                if (wireAndLocals == childrenAndLocals.First()) continue;

                //var wireCurvesAndLocals = firstWireElement.GetAllChildrenIndexesOrdered(firstWireIndex, gimData);
                int wireId = wireAndLocals.Item1;
                int wireLocalId = wireAndLocals.Item2;

                GimElement wireElement = gimData.Elements[wireId];
                var wireCurvesAndLocals = wireElement.GetAllChildrenIndexesOrdered(wireId, gimData);

                CreateCurves(wireCurvesAndLocals, gimData, out var curves, out var seamInformation);

                //foreach (var curveAndLocal in wireCurvesAndLocals)
                //{
                //    int curveIndex = curveAndLocal.Item1;
                //    int curveLocalIndex = curveAndLocal.Item2;
                //    TopoDSEdgeWrapper resultingCurve = GIMocctCurveWriter.WriteCurveElement(gimData, curveIndex, curveLocalIndex);
                //    if (resultingCurve == null) continue;

                //    curves.Add(resultingCurve);
                //}
                WireBuilder innerWireBuild = new();

                for(int ci =0; ci < curves.Count; ci++)
                {
                    var resultingCurve = curves[ci];
                    innerWireBuild.Add(resultingCurve);
                    status = innerWireBuild.ErrorCode();
                    if (status != 0)
                    {
                        string warn = "Issue when building one of the inner wires!";
                        if (ci > 0)
                        {
                            var previousCurve = curves[ci - 1];
                            var previousStart = previousCurve.FirstVertex();
                            var previousEnd = previousCurve.LastVertex();
                            var currentStart = resultingCurve.FirstVertex();
                            var currentEnd = resultingCurve.LastVertex();

                            double tolerance = resultingCurve.Tolerance();
                            Vector3Wrapper commonVertex;
                            bool hasCommon = resultingCurve.CommonVertex(previousCurve, out commonVertex);
                            previousCurve.ForceBindToEdge(resultingCurve);
                            innerWireBuild.Add(resultingCurve);
                            status = innerWireBuild.ErrorCode();
                        }
                    }
                }

                //TopoDSWireWrapper wire = innerWireBuild.GetWire();

                //wire.SetOrientation(
                //    wireElement.Orientation ?
                //    (int)TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD :
                //    (int)TopAbsWrapper_Orientation.TopAbsWrapper_REVERSED
                //);

                var builtInnerWire = surfaceBuilder.AddWire(curves, seamInformation);

                if (builtInnerWire != null)
                {
                    builtInnerWire.SetOrientation(
                        wireElement.Orientation ?
                        (int)TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD :
                        (int)TopAbsWrapper_Orientation.TopAbsWrapper_REVERSED
                        );
                }

                faceStatus = surfaceBuilder.Error();
            }

            

            switch (element.ElementType)
            {
                
                case GimElementType.F_cone: finalFace = surfaceBuilder.GetCone(); break;
                case GimElementType.F_plane: finalFace = surfaceBuilder.GetPlane(); break;
                case GimElementType.F_toroid: finalFace = surfaceBuilder.GetTorus(); break;
                case GimElementType.F_sphere: finalFace = surfaceBuilder.GetSphere(); break;
                case GimElementType.F_cilinder: finalFace = surfaceBuilder.GetCylinder(); break;
                case GimElementType.F_bspline: finalFace = surfaceBuilder.GetBSplineSurface(); break;
            }
            finalFace.SetOrientation(
                    element.Orientation ?
                    (int)TopAbsWrapper_Orientation.TopAbsWrapper_FORWARD :
                    (int)TopAbsWrapper_Orientation.TopAbsWrapper_REVERSED
                    );
            //finalFace.AddChild(builtWire);
            
            return finalFace;

        }

        /// <summary>
        /// Builds OCCT edges from the given curve element/local pairs, and (when every curve is a
        /// <see cref="GimElementType.C_line"/>) reorders them into a continuous wire chain.
        /// Source data does not guarantee wire-consistent ordering for line sequences; OCCT's
        /// WireBuilder needs a continuous chain, so a seed-and-grow permutation is applied when
        /// a unique chain can be derived (otherwise a warning is logged and the original order is kept).
        /// </summary>
        /// <param name="surfaceCurvesAndLocals">In/out list of (elementIndex, localIndex) pairs; rewritten to match produced curves and any line-chain permutation.</param>
        /// <param name="gimData">Source GimData.</param>
        /// <param name="surfaceCurves">Produced OCCT edges (null writes are skipped).</param>
        /// <param name="seamInformation">Per-curve seam flags aligned with <paramref name="surfaceCurves"/>.</param>
        public static void CreateCurves(List<Tuple<int, int>> surfaceCurvesAndLocals, GimData gimData,
            out List<TopoDSEdgeWrapper> surfaceCurves, out List<bool> seamInformation)
        {
            surfaceCurves = new();
            seamInformation = new();
            List<Tuple<int, int>> producedCurveAndLocals = new();
            foreach (var curveAndLocal in surfaceCurvesAndLocals)
            {
                int curveIndex = curveAndLocal.Item1;
                int curveLocalIndex = curveAndLocal.Item2;
                TopoDSEdgeWrapper resultingCurve = GIMocctCurveWriter.WriteCurveElement(gimData, curveIndex, curveLocalIndex);

                if (resultingCurve == null) continue;

#if FALSE
                {
                    //var writer = resultingCurve.GetWriter();
                    //Directory.CreateDirectory(FileAccess.GetTempPath("curves"));
                    //writer.WriteStepFromMemory(FileAccess.GetTempPath("curves\\curve" + curveNumber + ".step"));
                    curveNumber++;
                }
#endif

                GimElement curveElement = gimData.Elements[curveIndex];
                bool has2DChild = false;
                foreach (int curveChildIndex in curveElement.Elements)
                {
                    GimElement curveChild = gimData.Elements[curveChildIndex];
                    if (curveChild.ElementType.IsSeam())
                    {
                        has2DChild = true;
                        break;
                    }
                }
                seamInformation.Add(has2DChild);
                surfaceCurves.Add(resultingCurve);
                producedCurveAndLocals.Add(curveAndLocal);
            }

            // Keep curve references aligned with the produced curves (null curves are skipped).
            surfaceCurvesAndLocals.Clear();
            surfaceCurvesAndLocals.AddRange(producedCurveAndLocals);

            // Line-chain reordering: only when every produced curve is a C_line.
            // Non-line wires keep their source order.
            if (surfaceCurves.Count <= 1)
            {
                return;
            }

            bool allLines = true;
            List<(Vect Start, Vect End)> lineEndpoints = new(surfaceCurvesAndLocals.Count);
            for (int i = 0; i < surfaceCurvesAndLocals.Count; i++)
            {
                var curveRef = surfaceCurvesAndLocals[i];
                GimElement curveElement = gimData.Elements[curveRef.Item1];
                if (curveElement.ElementType != GimElementType.C_line)
                {
                    allLines = false;
                    break;
                }

                if (!TryGetOrientedLineEndpoints(gimData, curveRef.Item1, curveRef.Item2, out Vect start, out Vect end))
                {
                    allLines = false;
                    break;
                }

                lineEndpoints.Add((start, end));
            }

            if (!allLines)
            {
                return;
            }

            if (!TryBuildLineChainPermutation(lineEndpoints, LineChainTolerance, out var permutation, out string reason))
            {
                GimLogger.Warn($"CreateCurves line-chain ordering fallback: {reason}.");
                return;
            }

            ApplyPermutationInPlace(surfaceCurves, permutation);
            ApplyPermutationInPlace(seamInformation, permutation);
            ApplyPermutationInPlace(surfaceCurvesAndLocals, permutation);
        }

        /// <summary>
        /// Resolves the oriented start/end points of a <see cref="GimElementType.C_line"/> in world space,
        /// flipping endpoints when the curve's orientation flag is reversed.
        /// </summary>
        /// <returns>False if the element is missing, not a line, or has incomplete doubles/locals.</returns>
        private static bool TryGetOrientedLineEndpoints(
            GimData gimData,
            int curveIndex,
            int curveLocalIndex,
            out Vect startPoint,
            out Vect endPoint)
        {
            startPoint = new Vect(0, 0, 0);
            endPoint = new Vect(0, 0, 0);

            if (curveIndex < 0 || curveIndex >= gimData.Elements.Count)
            {
                return false;
            }

            GimElement curveElement = gimData.Elements[curveIndex];
            if (curveElement.ElementType != GimElementType.C_line ||
                curveElement.ElementDoubles == null ||
                curveElement.ElementDoubles.Length < 2 ||
                curveElement.Locals == null ||
                curveLocalIndex < 0 ||
                curveLocalIndex >= curveElement.Locals.Count)
            {
                return false;
            }

            GimLocal local = curveElement.Locals[curveLocalIndex];
            Vect basis = new double[] { local.Px, local.Py, local.Pz };
            Vect direction = new double[] { local.Zx, local.Zy, local.Zz };

            double t0 = curveElement.ElementDoubles[0];
            double t1 = curveElement.ElementDoubles[1];
            startPoint = basis + t0 * direction;
            endPoint = basis + t1 * direction;

            // Orientation controls traversal direction within the parent wire.
            if (!curveElement.Orientation)
            {
                (startPoint, endPoint) = (endPoint, startPoint);
            }

            return true;
        }

        /// <summary>
        /// Finds a permutation of line segments that forms a unique continuous chain (endpoint-to-endpoint
        /// within <paramref name="tolerance"/>). Tries each segment as seed in both orientations and
        /// greedily grows the chain; fails if any step has zero or multiple matching candidates.
        /// </summary>
        private static bool TryBuildLineChainPermutation(
            List<(Vect Start, Vect End)> segments,
            double tolerance,
            out List<int> permutation,
            out string failureReason)
        {
            permutation = new();
            failureReason = string.Empty;

            if (segments.Count <= 1)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    permutation.Add(i);
                }

                return true;
            }

            for (int seed = 0; seed < segments.Count; seed++)
            {
                for (int seedOrientation = 0; seedOrientation < 2; seedOrientation++)
                {
                    bool[] used = new bool[segments.Count];
                    List<int> candidate = new(segments.Count) { seed };
                    used[seed] = true;

                    Vect currentEnd = seedOrientation == 0 ? segments[seed].End : segments[seed].Start;
                    bool success = true;

                    for (int step = 1; step < segments.Count; step++)
                    {
                        int matchingIndex = -1;
                        bool matchedAtStart = false;
                        int matchingCandidates = 0;

                        for (int i = 0; i < segments.Count; i++)
                        {
                            if (used[i]) continue;

                            bool startMatch = ArePointsClose(segments[i].Start, currentEnd, tolerance);
                            bool endMatch = ArePointsClose(segments[i].End, currentEnd, tolerance);
                            if (!startMatch && !endMatch) continue;

                            matchingCandidates++;
                            if (matchingCandidates == 1)
                            {
                                matchingIndex = i;
                                matchedAtStart = startMatch || !endMatch;
                            }
                        }

                        if (matchingCandidates != 1)
                        {
                            success = false;
                            break;
                        }

                        used[matchingIndex] = true;
                        candidate.Add(matchingIndex);
                        currentEnd = matchedAtStart ? segments[matchingIndex].End : segments[matchingIndex].Start;
                    }

                    if (success)
                    {
                        permutation = candidate;
                        return true;
                    }
                }
            }

            failureReason = "could not derive unique continuous chain";
            return false;
        }

        /// <summary>True when the Euclidean distance between the two points is within <paramref name="tolerance"/>.</summary>
        private static bool ArePointsClose(Vect first, Vect second, double tolerance)
        {
            return (first - second).Norm <= tolerance;
        }

        /// <summary>Reorders <paramref name="source"/> in place according to <paramref name="permutation"/> index mapping.</summary>
        private static void ApplyPermutationInPlace<T>(List<T> source, List<int> permutation)
        {
            if (source.Count != permutation.Count)
            {
                return;
            }

            List<T> reordered = new(source.Count);
            for (int i = 0; i < permutation.Count; i++)
            {
                reordered.Add(source[permutation[i]]);
            }

            source.Clear();
            source.AddRange(reordered);
        }

        public static TopoDSWireWrapper WriteSphere(SurfaceBuilder surfaceBuilder, GimElement sphereElement,
            GimLocal local, List<TopoDSEdgeWrapper> wireCurves)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double radius = sphereElement.ElementDoubles[0];
            return surfaceBuilder.CreateSphere(wireCurves, radius, center, matrix);
        }

        public static void WriteCone(SurfaceBuilder surfaceBuilder, GimElement coneElement,
            GimLocal local, TopoDSWireWrapper wire)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double radius = coneElement.ElementDoubles[0];
            double semiAngle = coneElement.ElementDoubles[1];
            surfaceBuilder.CreateCone(wire, radius, semiAngle, center, matrix);
        }
        public static TopoDSWireWrapper WriteCone(SurfaceBuilder surfaceBuilder, GimElement coneElement,
            GimLocal local, List<TopoDSEdgeWrapper> wireCurves)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double radius = coneElement.ElementDoubles[0];
            double semiAngle = coneElement.ElementDoubles[1];
            return surfaceBuilder.CreateCone(wireCurves, radius, semiAngle, center, matrix);
        }

        public static void WriteTorus(SurfaceBuilder surfaceBuilder, GimElement torusElement, 
            GimLocal local, TopoDSWireWrapper wire)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double majorRadius = torusElement.ElementDoubles[0];
            double minorRadius = torusElement.ElementDoubles[1];
            surfaceBuilder.CreateTorus(wire, majorRadius, minorRadius, center, matrix);
        }
        public static TopoDSWireWrapper WriteTorus(SurfaceBuilder surfaceBuilder, GimElement torusElement,
            GimLocal local, List<TopoDSEdgeWrapper> wireCurves)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double majorRadius = torusElement.ElementDoubles[0];
            double minorRadius = torusElement.ElementDoubles[1];
            return surfaceBuilder.CreateTorus(wireCurves, majorRadius, minorRadius, center, matrix);
        }

        public static void WriteCylinderSurface(SurfaceBuilder surfaceBuilder, GimElement cylinderSurface, 
            GimLocal local, TopoDSWireWrapper wire)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double radius = cylinderSurface.ElementDoubles[0];
            surfaceBuilder.CreateCylinder(wire, radius, center, matrix);
        }

        public static TopoDSWireWrapper WriteCylinderSurface(SurfaceBuilder surfaceBuilder, GimElement cylinderSurface,
            GimLocal local, List<TopoDSEdgeWrapper> wireCurves)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            double radius = cylinderSurface.ElementDoubles[0];
            return surfaceBuilder.CreateCylinder(wireCurves, radius, center, matrix);
        }

        public static TopoDSWireWrapper WriteBsplineSurface(SurfaceBuilder surfaceBuilder, GimElement bsplineElement,
            GimLocal local, List<TopoDSEdgeWrapper> wireCurves)
        {
            int NumberOfSpecials = 6;
            int polesCount = bsplineElement.ElementInts[0];
            int uDegree = bsplineElement.ElementInts[1];
            int vDegree = bsplineElement.ElementInts[2];
            int uKnotsCount = bsplineElement.ElementInts[3];
            int uPoleCount = bsplineElement.ElementInts[4];
            int multiplicitiesCount = bsplineElement.ElementInts[5];
            int vPoleCount = polesCount / uPoleCount;

            // Read all poles
            List<List<Vector3Wrapper>> points = new();

            // pole XYZ values
            int i = 0;
            for (int uIndex = 0; uIndex < uPoleCount; uIndex++)
            {
                points.Add(new());
                for (int vIndex = 0; vIndex < vPoleCount; vIndex++)
                {
                    double x = bsplineElement.elementDoubles[i]; i++;
                    double y = bsplineElement.elementDoubles[i]; i++;
                    double z = bsplineElement.elementDoubles[i]; i++;
                    points[uIndex].Add(new Vector3Wrapper(x, y, z));
                }
            }

            List<List<double>> weights = new();
            for (int uIndex = 0; uIndex < uPoleCount; uIndex++) // weights
            {
                weights.Add(new());
                for (int vIndex = 0; vIndex < vPoleCount; vIndex++)
                {
                    double weight = bsplineElement.elementDoubles[i]; i++;
                    weights[uIndex].Add(weight);
                }
            }

            //double[] weights = new double[polesCount];
            //int startNEW = polesCount * 3;
            //for (int iNEW = 0; iNEW < polesCount; iNEW++)
            //{
            //    weights[iNEW] = bsplineElement.ElementDoubles[startNEW + iNEW];
            //}

            // Read all ints
            List<int> multiplicities = new(multiplicitiesCount);
            int knotIndex = NumberOfSpecials;
            for (; knotIndex < multiplicitiesCount + NumberOfSpecials; knotIndex++)
            {
                multiplicities.Add(bsplineElement.ElementInts[knotIndex]);
            }

            bool periodic = true;
            int ifNonPeriodic = uPoleCount + uDegree + 1;
            int ifPeriodic = uPoleCount + multiplicities[0];
            if (ifNonPeriodic == ifPeriodic)
            {
                periodic = false;
            }
            int uMultiplicitiesCount = 0;

            for (int j = 1; j < multiplicitiesCount; j++)
            {
                int MultiSum = 0;
                for (int k = 0; k < j; k++)
                {
                    MultiSum += multiplicities[k];
                }
                if (periodic && MultiSum == ifPeriodic)
                {
                    uMultiplicitiesCount = j; break;
                }
                else if (MultiSum == ifNonPeriodic)
                {
                    uMultiplicitiesCount = j; break;
                }
            }
        
            int vMultiplicitiesCount = multiplicities.Count - uMultiplicitiesCount;
            List<int> uMultiplicities = new(uMultiplicitiesCount);
            List<int> vMultiplicities = new(vMultiplicitiesCount);

            for (int j = 0; j < uMultiplicitiesCount; j++)
            {
                uMultiplicities.Add(multiplicities[j]);
            }

            for (int j = 0; j < vMultiplicitiesCount; j++)
            {
                vMultiplicities.Add(multiplicities[j + uMultiplicitiesCount]);
            }

            int start = polesCount * 4;
            List<double> uKnots = new(uKnotsCount);
            for (knotIndex = 0; knotIndex < uKnotsCount; knotIndex++)
            {
                uKnots.Add(bsplineElement.ElementDoubles[start + knotIndex]);
            }

            start += knotIndex;
            int vKnotsCount = bsplineElement.ElementDoubles.Length - (polesCount * 4 + uKnotsCount);
            
            List<double> vKnots = new(vKnotsCount);
            for (knotIndex = 0; knotIndex < vKnotsCount; knotIndex++)
            {
                vKnots.Add(bsplineElement.ElementDoubles[start + knotIndex]);
            }

            // checking periodicity of bsplines
            int uMultSum = uMultiplicities.Sum();
            int uAperiodicSum = uMultSum - uDegree - 1;
            int uPeriodicSum = uMultSum - uMultiplicities[0];

            bool uAperiodic = (uAperiodicSum == uPoleCount);
            bool uPeriodic;
            if (uAperiodic)
            {
                uPeriodic = false;
            }
            else
            {
                uPeriodic = (uPeriodicSum == uPoleCount);
            }

            if(uPeriodic == uAperiodic)
            {
                throw GimLogger.Error(new FormatException("Bspline surface is neither periodic nor aperiodic!"));
            }

            int vMultSum = vMultiplicities.Sum();
            int vAperiodicSum = vMultSum - vDegree - 1;
            int vPeriodicSum = vMultSum - vMultiplicities[0];

            bool vAperiodic = (vAperiodicSum == vPoleCount);
            bool vPeriodic;
            if (vAperiodic)
            {
                vPeriodic = false;
            }
            else
            {
                vPeriodic = (vPeriodicSum == vPoleCount);

            }

            if (vPeriodic == vAperiodic)
            {
                throw GimLogger.Error(new FormatException("Bspline surface is neither periodic nor aperiodic!"));
            }

            //// TODO: Replace magic number by variable.
            //// 3.675086918879E-08
            //for (int i = 1; i < uKnots.Length; i++)
            //{
            //    if (uKnots[i - 1] >= uKnots[i])
            //        uKnots[i] = uKnots[i - 1] + 3.675086918879E-08;
            //}

            //// TODO: Replace magic number by variable.
            //// 3.675086918879E-08
            //for (int i = 1; i < vKnots.Length; i++)
            //{
            //    if (vKnots[i - 1] >= vKnots[i])
            //        vKnots[i] = vKnots[i - 1] + 3.675086918879E-08;
            //}

            return surfaceBuilder.CreateBSplineSurface(wireCurves, points, weights, uKnots, vKnots, uMultiplicities, 
                vMultiplicities, uDegree, vDegree, uPeriodic, vPeriodic);
        }

        public static void WritePlane(SurfaceBuilder surfaceBuilder, GimElement planeElement,
            GimLocal local, TopoDSWireWrapper wire)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            surfaceBuilder.CreatePlane(wire, center, matrix);
        }
        public static TopoDSWireWrapper WritePlane(SurfaceBuilder surfaceBuilder, GimElement planeElement,
           GimLocal local, List<TopoDSEdgeWrapper> wireCurves)
        {
            (var center, var matrix) = GIMocctWriteElements.GetLocalAsWrapper(local);
            return surfaceBuilder.CreatePlane(wireCurves, center, matrix);
            //surfaceBuilder.CreatePlane(center, matrix);
        }
        public static void WritePlaneFromWire(SurfaceBuilder surfaceBuilder, TopoDSWireWrapper wire)
        {
            surfaceBuilder.CreatePlaneOutOfWire(wire);
        }
    }
}
