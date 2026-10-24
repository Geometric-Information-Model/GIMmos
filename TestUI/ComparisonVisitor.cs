using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using cadex;
using GIMcompress;
using GIMocctCLI;
using GIMocctWrapper;
using GIMtransfer;
using GimUtilities;
using Newtonsoft.Json;

namespace TestUI
{
    public static class VectExtensions
    {
        public static Vect FromWrapper(Vector3Wrapper vectWrapper)
        {
            return new Vect(vectWrapper.X, vectWrapper.Y, vectWrapper.Z);
        }
        
        public static Matrix FromWrapper(Matrix4x4Wrapper matrixWrapper)
        {
            return new Matrix(
                new double[] { matrixWrapper.M11, matrixWrapper.M12, matrixWrapper.M13 },
                new double[] { matrixWrapper.M21, matrixWrapper.M22, matrixWrapper.M23 },
                new double[] { matrixWrapper.M31, matrixWrapper.M32, matrixWrapper.M33 }
                );
        }
    }
    public class PropertyComparison
    {
        public string name;
        public object occtValue;
        public object gimValue;
        public bool similar;

        public PropertyComparison(string name, object occtValue, object gimValue)
        {
            this.name = name;
            this.occtValue = occtValue;
            this.gimValue = gimValue;
            CheckSimilar();
        }

        public void CheckSimilar()
        {
            if(occtValue is null && gimValue is null)
            {
                similar = true;
                return;
            }

            if(occtValue is null || gimValue is null)
            {
                similar = false;
                return;
            }

            if(occtValue.GetType() != gimValue.GetType())
            {
                similar = false;
                return;
            }
            similar = CompareObjects(occtValue, gimValue);
        }

        // assumes the objects have the same type
        public static bool CompareObjects(object occtV, object gimV)
        {
            if(occtV is Vector3Wrapper)
            {
                Vect occtVect = VectExtensions.FromWrapper(occtV as Vector3Wrapper);
                Vect gimVect = VectExtensions.FromWrapper(gimV as Vector3Wrapper);
                return occtVect == gimVect;
            }
            if(occtV is Matrix4x4Wrapper)
            {
                Matrix occtMatrix = VectExtensions.FromWrapper(occtV as Matrix4x4Wrapper);
                Matrix gimMatrix = VectExtensions.FromWrapper(gimV as Matrix4x4Wrapper);
                return occtMatrix == gimMatrix;
            }
            if(occtV is IList)
            {
                IList occtList = (IList) occtV;
                IList gimList = (IList) gimV;
                if(occtList.Count != gimList.Count) { return false; }

                for (int i = 0; i < occtList.Count; i++)
                {
                    if (!CompareObjects(occtList[i], gimList[i])) return false;
                }
                return true;
            }
            if(occtV is double)
            {
                double occtD = (double) occtV;
                double gimD = (double) gimV;
                return VectorHelpers.CompareDouble(gimD, occtD, Matrix.EQUALITY_MARGIN);
            }

            return occtV.Equals(gimV);
        }

        public override string ToString()
        {
            string val = $"{name};{StringifyObj(occtValue)};{StringifyObj(gimValue)};{similar}";
            if (!similar)
            {
                val = "DIFFERENCE: " + val;
            }
            return val;
        }

        public static string StringifyObj(object obj)
        {
            if (obj == null)
            {
                return "null";
            }

            if (obj is Vector3Wrapper)
            {
                Vect vect = VectExtensions.FromWrapper(obj as Vector3Wrapper);
                return vect.ToString();
            }
            if (obj is Matrix4x4Wrapper)
            {
                var matrix = VectExtensions.FromWrapper(obj as Matrix4x4Wrapper);
                return matrix.ToString();
            }

            if (obj is IList)
            {
                if ((obj as IList).Count > 0)
                    return $"{(obj as IList).Count} * {(obj as IList)[0].GetType()}";
                else
                    return $"Empty List";
            }

            return obj.ToString();
        }
    }
    public class ShapeComparison
    {
        public List<PropertyComparison> comparisons = new();
        public List<ShapeComparison> childComparisons = new();
        TopoDSAbstractShapeWrapper occtShape;
        TopoDSAbstractShapeWrapper gimShape;

        public bool ChildDifferent = false;
        public bool TypeDifferent = false;
        public bool PropertyDifferent = false;
        public ShapeComparison(TopoDSAbstractShapeWrapper occtShape, TopoDSAbstractShapeWrapper gimShape)
        {
            this.occtShape = occtShape;
            this.gimShape = gimShape;
            TypeDifferent = (occtShape.GetType() != gimShape.GetType());
        }

        public void CheckProperties()
        {
            PropertyDifferent = comparisons.Any((property) => !property.similar);
        }

        public bool Different()
        {
            return (ChildDifferent || TypeDifferent || PropertyDifferent);
        }

        public void AddChildShapeComparison(ShapeComparison comparison)
        {
            childComparisons.Add(comparison);
            if (!ChildDifferent)
            {
                bool different = comparison.Different();
                ChildDifferent = different;
            }
        }

        public void AddProperty(string name, object occtVal, object gimVal)
        {
            PropertyComparison propertyComparison = new(name, occtVal, gimVal);
            comparisons.Add(propertyComparison);
            if (!PropertyDifferent)
            {
                PropertyDifferent = !propertyComparison.similar;
            }
        }

        public override string ToString()
        {
            string val = "";
            if(TypeDifferent)
            {
                val += "Shapes are of different types! ";
            }

            if(PropertyDifferent)
            {
                val += "Different properties! ";
            }

            if(ChildDifferent)
            {
                val += "Difference in child!";
            }

            return val;
        }
    }

    public class LabelComparisonStructure
    {
        public string Name { get; set; }

        public RGBComparison RGB { get; set; }

        public List<LabelComparisonStructure> ChildLabels { get; set; }
        public List<LabelComparisonStructure> ChildShapes { get; set; }

        public bool IsFree { get; set; }
        public bool IsShape { get; set; }
        public bool IsSimpleShape { get; set; }
        public bool IsReference { get; set; }
        public bool IsAssembly { get; set; }
        public bool IsComponent { get; set; }
        public bool IsCompound { get; set; }
        public bool IsSubShape { get; set; }

        public LabelComparisonStructure(string name, RGBComparison rgb = null)
        {
            Name = name;
            RGB = rgb;
            ChildLabels = new();
            ChildShapes = new();
        }
    }

    public class RGBComparison
    {
        public double Red { get; set; }
        public double Green { get; set; }
        public double Blue { get; set; }
        public double Alpha { get; set; }
    }

    internal class ComparisonVisitor
    {
        TopoDSAbstractShapeWrapper occtShape;
        TopoDSAbstractShapeWrapper gimShape;

        DocHelperWrapper occtDoc;
        DocHelperWrapper gimDoc;

        string fileName;

        public ShapeComparison FinalComparison;
        public ComparisonVisitor(string filePath) 
        {
            OcctWrapper occtWrapper = new OcctWrapper();
            string fileType = filePath.Split('.')[^1];
            fileName = Path.GetFileName(filePath);
            // using the appropriate read function
            switch (fileType.ToLower())
            {
                case "step":
                case "stp":
                    {
                        occtWrapper.ReadStepToMemory(filePath);
                    }
                    break;
                case "iges":
                case "igs":
                    {
                        occtWrapper.ReadIGESToMemory(filePath);
                    }
                    break;
                default: throw new FormatException("Invalid filetype " + fileType + ": Unsupported by OCCT!");
            }

            TopoDSWrapper wrapper = new TopoDSWrapper(occtWrapper, fileType.ToLower()); 
            // transferring shape to specialized shape wrappers
            occtShape = TopoDSAbstractShapeWrapper.GetWrapper(wrapper);
            GIMocctElement parentShape = new(occtShape);

            // getting the document
            parentShape.Shape.InitDocWrapper(wrapper);
            occtDoc = occtWrapper.DocWrapper;

            // loading the subtree of the shape
            GIMocctRead.ReadShapeTree(parentShape);
            // transferring shape hierarchy to gimData
            GimProgressStatus readStatus = new();
            readStatus.Create(new()
            {
                { GIMocctReadElements.PROGRESS_ELEMENTS, 100 }
            });
            GIMocctReadElements gimOcctReader = new(readStatus);
            
            GimData gimData = gimOcctReader.ReadPartHierarchy(parentShape);
            GimProgressStatus writeStatus = new();
            writeStatus.Create(new(){
                { GIMocctWriteElements.PROGRESS_WRITE, 100}
            });
            GIMocctWriteElements gimOcctWriter = new(writeStatus);
            gimOcctWriter.elemToObj = gimData.GetElementToObjectDict();
            gimShape = gimOcctWriter.GetAdvancedOcctShape(gimData);

            FinalComparison = CompareShapes(occtShape, gimShape);

            gimDoc = gimShape._docWrapper;

            CompareStructures();
        }

        public void ExportShapes()
        {
            Directory.CreateDirectory(GIMtransfer.FileAccess.GetTempFolder() + "COMPARE");
            string occtName = "COMPARE\\" + fileName + " OCCT.step";
            string gimName = "COMPARE\\" + fileName + " GIM.step";

            string occtPath = GIMtransfer.FileAccess.GetTempPath(occtName);
            string gimPath = GIMtransfer.FileAccess.GetTempPath(gimName);

            var occtWriter = occtShape.GetWriter();
            occtWriter.WriteStepFromMemory(occtPath);
            var gimWriter = gimShape.GetWriter();
            gimWriter.WriteStepFromMemory(gimPath);
        }

        public void CompareStructures()
        {
            var occtLabels = occtDoc.GetTopLabels();
            var gimLabels = gimDoc.GetTopLabels();

            Directory.CreateDirectory(GIMtransfer.FileAccess.GetTempFolder() + "COMPARE");
            string occtName = "COMPARE\\" + fileName + " OCCT.structure.json";
            string gimName = "COMPARE\\" + fileName + " GIM.structure.json";

            string occtPath = GIMtransfer.FileAccess.GetTempPath(occtName);
            string gimPath = GIMtransfer.FileAccess.GetTempPath(gimName);

            List<LabelComparisonStructure> occtLabelComparisons = new();
            List<LabelComparisonStructure> gimLabelComparisons = new();

            foreach(var occtLabel in occtLabels)
            {
                occtLabelComparisons.Add(GetLabelFromDoc(occtDoc, occtLabel));
            }

            foreach(var gimLabel in gimLabels)
            {
                gimLabelComparisons.Add(GetLabelFromDoc(gimDoc, gimLabel));
            }

            string occtString = JsonConvert.SerializeObject(occtLabelComparisons, Newtonsoft.Json.Formatting.Indented);
            string gimString = JsonConvert.SerializeObject(gimLabelComparisons, Newtonsoft.Json.Formatting.Indented);

            File.WriteAllText(occtPath, occtString);
            File.WriteAllText(gimPath, gimString);
        }

        public LabelComparisonStructure GetLabelFromDoc(DocHelperWrapper doc, TDFLabelWrapper label)
        {
            LabelComparisonStructure resultingLabel;

            var colorWrapper = doc.GetLabelColor(label);

            RGBComparison rgb;

            if (colorWrapper != null) rgb = new()
            {
                Red = colorWrapper.GetRed(),
                Green = colorWrapper.GetGreen(),
                Blue = colorWrapper.GetBlue(),
                Alpha = colorWrapper.GetAlpha(),
            };
            else
                rgb = null;

            string name = doc.GetLabelName(label);
            resultingLabel = new(name, rgb);

            resultingLabel.IsFree = doc.IsFree(label);
            resultingLabel.IsShape= doc.IsShape(label);
            resultingLabel.IsSimpleShape = doc.IsSimpleShape(label);
            resultingLabel.IsReference = doc.IsReference(label);
            resultingLabel.IsAssembly = doc.IsAssembly(label);
            resultingLabel.IsComponent = doc.IsComponent(label);
            resultingLabel.IsCompound = doc.IsCompound(label);
            resultingLabel.IsSubShape = doc.IsSubShape(label);

            var subLabels = doc.GetSubLabels(label);
            foreach(var subLabel in subLabels)
                resultingLabel.ChildLabels.Add(GetLabelFromDoc(doc, subLabel));

            var subShapes = doc.GetSubShapeLabels(label);
            foreach (var subShape in subShapes)
                resultingLabel.ChildShapes.Add(GetLabelFromDoc(doc, subShape));

            return resultingLabel;
        }

        public ShapeComparison CompareShapes(TopoDSAbstractShapeWrapper occtShape, TopoDSAbstractShapeWrapper gimShape)
        {
            if (occtShape is null && gimShape is null) return null;
            occtShape.ResetIterator(); ;
            gimShape.ResetIterator();
            ShapeComparison comparison = new(occtShape, gimShape);
            comparison.AddProperty("Shape", occtShape.GetType(), gimShape.GetType());
            comparison.AddProperty("Orientation", occtShape.GetOrientation(), gimShape.GetOrientation());
            if (!comparison.TypeDifferent)
            {
                if(occtShape is TopoDSFaceWrapper)
                {
                    comparison.AddProperty("Surface", (occtShape as TopoDSFaceWrapper).FaceType(), (gimShape as TopoDSFaceWrapper).FaceType());
                    if(occtShape is TopoDSPlaneWrapper)
                    {
                        ComparePlanes((TopoDSPlaneWrapper)occtShape, (TopoDSPlaneWrapper)gimShape, comparison);
                    }
                    else if (occtShape is TopoDSCylinderWrapper)
                    {
                        CompareCylinders((TopoDSCylinderWrapper)occtShape, (TopoDSCylinderWrapper)gimShape, comparison);
                    }
                }
                else if (occtShape is TopoDSEdgeWrapper)
                {
                    TopoDSEdgeWrapper occtEdge = (TopoDSEdgeWrapper)occtShape;
                    TopoDSEdgeWrapper gimEdge = (TopoDSEdgeWrapper)gimShape;
                    comparison.AddProperty("Curve", occtEdge.EdgeType(), gimEdge.EdgeType());
                    comparison.AddProperty("StartParam", occtEdge.FirstParameter(), gimEdge.FirstParameter());
                    comparison.AddProperty("EndParam", occtEdge.LastParameter(), gimEdge.LastParameter());
                    comparison.AddProperty("FirstVertex", occtEdge.FirstVertex(), gimEdge.FirstVertex());
                    comparison.AddProperty("LastVertex", occtEdge.LastVertex(), gimEdge.LastVertex());
                    comparison.AddProperty("IsDegenerate", occtEdge.IsDegenerate(), gimEdge.IsDegenerate());
                    comparison.AddProperty("IsSeam", occtEdge.IsSeam(), gimEdge.IsSeam());
                    if(occtEdge.IsSeam() && gimEdge.IsSeam())
                    {
                        double occtStart, occtEnd, gimStart, gimEnd;
                        occtEdge.GetSeamStartAndEndParam(out occtStart, out occtEnd);
                        gimEdge.GetSeamStartAndEndParam(out gimStart, out gimEnd);
                        comparison.AddProperty("SeamStart", occtStart, gimStart);
                        comparison.AddProperty("SeamEnd", occtEnd, gimEnd);
                    }
                    
                    if(occtShape is TopoDSLineWrapper)
                    {
                        CompareLines((TopoDSLineWrapper)occtEdge, (TopoDSLineWrapper)gimEdge, comparison);
                    }
                    else if (occtShape is TopoDSCircleWrapper)
                    {
                        CompareCircles((TopoDSCircleWrapper)occtEdge, (TopoDSCircleWrapper)gimEdge, comparison);
                    }
                    else if (occtShape is TopoDSBSplineCurveWrapper)
                    {
                        CompareBSplineCurves((TopoDSBSplineCurveWrapper)occtEdge, (TopoDSBSplineCurveWrapper)gimEdge, comparison);
                    }
                }
                else
                {
                    comparison.AddProperty("Location", occtShape.Location(), gimShape.Location());
                }
                while (occtShape.MoreChildren() && gimShape.MoreChildren())
                {
                    var occtChild = occtShape.GetChild();
                    var gimChild = gimShape.GetChild();

                    // checking rare case where there's a degenerate edge in reading. These are eliminated before writing
                    if(occtChild is TopoDSEdgeWrapper)
                    {
                        var occtEdge = (TopoDSEdgeWrapper)occtChild;
                        if (occtEdge.IsDegenerate())
                        {
                            ShapeComparison degenerateComparison = new(occtEdge, occtEdge);
                            degenerateComparison.AddProperty("DEGENERATE EDGE", null, null);
                            comparison.AddChildShapeComparison(degenerateComparison);
                            occtShape.NextChild();
                            continue;
                        }
                    }
                    var childComp = CompareShapes(occtChild, gimChild);
                    if(childComp != null) comparison.AddChildShapeComparison(childComp);
                    occtShape.NextChild();
                    gimShape.NextChild();
                }

                if(occtShape.MoreChildren() || gimShape.MoreChildren())
                {
                    comparison.AddProperty("CHILD COUNT DIFFERENT", occtShape.MoreChildren(), gimShape.MoreChildren());
                }

            }

            return comparison;
        }
        #region Surfaces

        public void ComparePlanes(TopoDSPlaneWrapper occtPlane, TopoDSPlaneWrapper gimPlane, ShapeComparison comparison) 
        {
            comparison.AddProperty("Location", occtPlane.Location(), gimPlane.Location());
        }

        public void CompareCylinders(TopoDSCylinderWrapper occtCylinder, TopoDSCylinderWrapper gimCylinder, ShapeComparison comparison)
        {
            comparison.AddProperty("Radius", occtCylinder.Radius(), gimCylinder.Radius());
            comparison.AddProperty("Location", occtCylinder.Location(), gimCylinder.Location());
        }

        public void CompareTori(TopoDSTorusWrapper occtTorus, TopoDSTorusWrapper gimTorus, ShapeComparison comparison)
        {
            comparison.AddProperty("Major Radius", occtTorus.MajorRadius(), gimTorus.MinorRadius());
            comparison.AddProperty("Minor Radius", occtTorus.MinorRadius(), gimTorus.MinorRadius());
            comparison.AddProperty("Location", occtTorus.Location(), gimTorus.Location());
        }
        #endregion

        #region curves

        public void CompareLines(TopoDSLineWrapper occtLine, TopoDSLineWrapper gimLine, ShapeComparison comparison)
        {
            comparison.AddProperty("Direction", occtLine.Direction(), gimLine.Direction());
            comparison.AddProperty("Position", occtLine.Position(), gimLine.Position());
        }

        public void CompareCircles(TopoDSCircleWrapper occtCircle, TopoDSCircleWrapper gimCircle, ShapeComparison comparison)
        {
            comparison.AddProperty("Radius", occtCircle.Radius(), gimCircle.Radius());
            Matrix4x4Wrapper occtLocation = occtCircle.Location();
            Matrix4x4Wrapper gimLocation = gimCircle.Location();

            comparison.AddProperty("Location", occtLocation, gimLocation);
            comparison.AddProperty("Position", occtLocation.GetPosition(), gimLocation.GetPosition());
        }

        public void CompareBSplineCurves(TopoDSBSplineCurveWrapper occtSpline, TopoDSBSplineCurveWrapper gimSpline, ShapeComparison comparison)
        {
            comparison.AddProperty("Degree", occtSpline.Degree(), gimSpline.Degree());
            comparison.AddProperty("KnotCount", occtSpline.NbKnots(), gimSpline.NbKnots());

            List<double> occtKnots = new(), gimKnots = new();
            for(int i=1; i<= occtSpline.NbKnots(); i++)
            {
                occtKnots.Add(occtSpline.Knot(i));
            }

            for (int i = 1; i <= gimSpline.NbKnots(); i++)
            {
                gimKnots.Add(gimSpline.Knot(i));
            }
            comparison.AddProperty("Knots", occtKnots, gimKnots);

            comparison.AddProperty("PoleCount", occtSpline.NbPoles(), gimSpline.NbPoles());
            
            List<Vector3Wrapper> occtPoles = new(), gimPoles = new();
            List<double> occtWeights = new(), gimWeights = new();
            for(int i=1; i <= occtSpline.NbPoles(); i++)
            {
                occtPoles.Add(occtSpline.Pole(i));
                occtWeights.Add(occtSpline.Weight(i));
            }

            for (int i = 1; i <= gimSpline.NbPoles(); i++)
            {
                gimPoles.Add(gimSpline.Pole(i));
                gimWeights.Add(gimSpline.Weight(i));
            }

            comparison.AddProperty("Poles", occtPoles, gimPoles);
            comparison.AddProperty("Weights", occtWeights, gimWeights);

        }
        #endregion
    }

    // OLD COMPARISON VISITOR 
    //internal class ComparisonVisitor : ModelData_Model.VoidElementVisitor
    //{
    //public GimData gimData;
    //public ModelData_Model cadexModel;

    //public ModelData_Model gimCadexModel;

    //public List<ModelData_Part> cadexPartList = new();
    //public List<ModelData_Part> gimPartList = new();

    //bool gim = false;

    //StringBuilder debugStack = new();
    //List<string> blockSections = new();
    //int identCount = 0;

    //public ComparisonVisitor(GimData gimData, ModelData_Model cadexModel)
    //{
    //    this.gimData = gimData;
    //    this.cadexModel = cadexModel;
    //    gimCadexModel = CadexWriteElements.GetCadexModel(gimData, GIMtransfer.FileAccess.GetTempFolder(), null);
    //}

    //public void GetBothPartLists()
    //{
    //    gim = false;
    //    cadexModel.Accept(this);
    //    gim = true;
    //    gimCadexModel.Accept(this);
    //    TrySortPartLists();
    //}

    //public override void Apply(ModelData_Part thePart)
    //{
    //    var partList = cadexPartList;
    //    if (gim)
    //    {
    //        partList = gimPartList;
    //    }

    //    partList.Add(thePart);

    //}

    //public void TrySortPartLists() // sorting parts so they match 1 to 1
    //{
    //    for (int i = 0; i < cadexPartList.Count; i++)
    //    {
    //        var part = cadexPartList[i];
    //        string partName = part.Name().ToString();

    //        for (int j = i; j < gimPartList.Count; j++)
    //        {
    //            var otherPart = gimPartList[j];
    //            string otherPartName = otherPart.Name().ToString();

    //            if (otherPartName == partName)
    //            {
    //                if (i == j) break;
    //                var temp = gimPartList[i];
    //                gimPartList[i] = gimPartList[j];
    //                gimPartList[j] = temp;
    //            }
    //        }

    //    }
    //}

    //public void StartRegion(string name)
    //{
    //    Debug("<" + name + ">");
    //    //Debug("{");
    //    blockSections.Add(name);
    //    identCount++;
    //}

    //public void EndRegion()
    //{
    //    string closedSection = blockSections.Last();
    //    blockSections.RemoveAt(blockSections.Count - 1);
    //    identCount--;
    //    Debug("</" + closedSection + ">");
    //}

    //public void Debug(string line)
    //{
    //    string idents = "";
    //    for(int i=0; i<identCount; i++)
    //    {
    //        idents += "\t";
    //    }


    //    debugStack.AppendLine(idents + line);
    //}

    //public void DebugMarker()
    //{
    //    string markerLine = "marker <";

    //    for(int i=0; i<identCount; i++)
    //    {
    //        markerLine += "===";
    //    }

    //    debugStack.AppendLine(markerLine);

    //}

    //public void ResetDebug()
    //{
    //    debugStack = new();
    //}

    //public string GetDebugStack()
    //{
    //    return debugStack.ToString();
    //}

    //public bool CompareModels()
    //{
    //    StartRegion("Models");
    //    bool modelsSimilar = true;
    //    if (cadexPartList.Count != gimPartList.Count)
    //    {
    //        string debug = "ERROR, PART LISTS NOT SIMILAL, IMINENT EXCEPTION";
    //    }

    //    for (int i = 0; i < cadexPartList.Count; i++)
    //    {
    //        StartRegion("Part");
    //        var cadexPart = cadexPartList[i];
    //        var gimPart = gimPartList[i];

    //        var cadexBrep = cadexPart.BRepRepresentation();
    //        var gimBrep = gimPart.BRepRepresentation();

    //        var result = CompareBreps(cadexBrep, gimBrep);
    //        if (!result)
    //        {
    //            string debug = "BREPS NOT SIMILAR";
    //            Debug(debug);
    //            DebugMarker();
    //        }
    //        EndRegion();
    //        modelsSimilar &= result;
    //    }

    //    EndRegion();
    //    return modelsSimilar;
    //}

    //public void ExportBothModels(string selectedFormat, string fileName)
    //{
    //    string folderPrefix = "Comparaison\\";
    //    string cadexName = folderPrefix + fileName + "_CADEXMODEL";
    //    string gimName = folderPrefix + fileName + "_GIMMODEL";

    //    string xmlName = folderPrefix + fileName + "_DIFFERENCES";

    //    string gimCadexErrName = folderPrefix + fileName + "_GIM_ERRORS";
    //    string cadexErrName = folderPrefix + fileName + "_CADEX_ERRORS";

    //    string cadexErr = "", cadexPath = "";
    //    string gimCadexErr = "", gimCadexPath = "";
    //    CadexModel.Write(cadexModel, selectedFormat, cadexName, ref cadexErr, ref cadexPath);
    //    CadexModel.Write(gimCadexModel, selectedFormat, gimName, ref gimCadexErr, ref gimCadexPath);

    //    string debugStack = GetDebugStack();

    //    System.IO.File.WriteAllText(FileAccess.GetTempPath(xmlName + ".xml"), debugStack);
    //    if(cadexErr != "")System.IO.File.WriteAllText(FileAccess.GetTempPath(cadexErrName + ".xml"), cadexErr);
    //    if(gimCadexErr != "")System.IO.File.WriteAllText(FileAccess.GetTempPath(gimCadexErrName + ".xml"), gimCadexErr);

    //}

    //public bool CompareBreps(ModelData_BRepRepresentation cadexBrep, ModelData_BRepRepresentation gimBrep)
    //{
    //    StartRegion("Brep");
    //    var cadexBodyList = cadexBrep.Get();
    //    List<ModelData_Body> cadexBodies = new List<ModelData_Body>();
    //    for(int i = 0; i < cadexBodyList.Size(); i++)
    //    {
    //        cadexBodies.Add(cadexBodyList.Access((uint)i));
    //    }

    //    var gimBodyList = gimBrep.Get();
    //    List<ModelData_Body> gimBodies = new List<ModelData_Body>();
    //    for(int i=0; i < gimBodyList.Size(); i++)
    //    {
    //        gimBodies.Add(gimBodyList.Access((uint)i));
    //    }

    //    bool brepsSimilar = true;
    //    for(int i=0; i< cadexBodies.Count; i++)
    //    {
    //        var cadexBody = cadexBodies[i];
    //        var gimBody = gimBodies[i];

    //        bool result = CompareBodies(cadexBody, gimBody);

    //        if(!result)
    //        {
    //            string debug = "BODIES NOT SIMILAR";
    //            Debug(debug);
    //            DebugMarker();
    //        }
    //        brepsSimilar &= result;
    //    }

    //    EndRegion();
    //    return brepsSimilar;
    //}

    //public bool CompareBodies(ModelData_Body cadexBody, ModelData_Body gimBody)
    //{
    //    StartRegion("Bodies");
    //    bool bodiesSimilar = cadexBody.BodyType() == gimBody.BodyType();

    //    if (!bodiesSimilar)
    //    {
    //        string debug = "BODIES ARE OF DIFFERENT TYPES";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    Debug("Orientation: " + cadexBody.Orientation().ToString() + " vs " + gimBody.Orientation().ToString());
    //    if(cadexBody.Orientation() != gimBody.Orientation())
    //    {
    //        string debug = "BODIES ARE ORIENTED DIFFERENTLY";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    bool result = CompareShapes(cadexBody, gimBody);
    //    bodiesSimilar &= result;
    //    if (!result)
    //    {
    //        string debug = "BODIES HAVE DIFFERENT SHAPES!";
    //        Debug(debug);
    //        DebugMarker();
    //    }
    //    EndRegion();
    //    return bodiesSimilar;
    //}

    //public bool CompareShapes(ModelData_Shape cadexShape, ModelData_Shape gimShape, ModelData_Face cadexSeamFace = null, ModelData_Face gimSeamFace = null)
    //{
    //    StartRegion("Shapes - " + cadexShape.Type() + " - " + gimShape.Type());
    //    bool shapesSimilar = true;

    //    List<ModelData_Shape> cadexShapeList = new();
    //    List<ModelData_Shape> gimShapeList = new();

    //    var cadexShapeIterator = new ModelData_Shape.Iterator(cadexShape);
    //    var gimShapeIterator = new ModelData_Shape.Iterator(gimShape);

    //    while (cadexShapeIterator.HasNext())
    //    {
    //        ModelData_Shape shape = cadexShapeIterator.Next();
    //        cadexShapeList.Add(shape);
    //    }

    //    while (gimShapeIterator.HasNext())
    //    {
    //        ModelData_Shape shape = gimShapeIterator.Next();
    //        gimShapeList.Add(shape);
    //    }

    //    if(cadexShapeList.Count != gimShapeList.Count)
    //    {
    //        shapesSimilar = false;
    //        string debug = "SHAPES HAVE DIFFERENT COUNT OF SUBSHAPES, ERROR IMINENT";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    ModelData_Face cadexFace = cadexSeamFace;
    //    ModelData_Face gimFace = gimSeamFace;

    //    if (cadexShape.Type() == ModelData_ShapeType.ModelData_ST_Face)
    //    {
    //        cadexFace = ModelData_Face.Cast(cadexShape);
    //    }
    //    if (gimShape.Type() == ModelData_ShapeType.ModelData_ST_Face)
    //    {
    //        gimFace = ModelData_Face.Cast(gimShape);
    //    }

    //    for (int i=0; i<Math.Min(cadexShapeList.Count, gimShapeList.Count); i++)
    //    {
    //        var cadexSubShape = cadexShapeList[i];
    //        var gimSubShape = gimShapeList[i];

    //        bool result = CompareShapes(cadexSubShape, gimSubShape, cadexFace, gimFace);
    //        shapesSimilar &= result;
    //        if (!result)
    //        {
    //            string debug = "SUBSHAPES ARE NOT SIMILAR IN STRUCTURE";
    //            Debug(debug);
    //            DebugMarker();
    //        }



    //        result = CompareShapeInfos(cadexSubShape, gimSubShape, cadexFace, gimFace);
    //        shapesSimilar &= result;
    //        if (!result)
    //        {
    //            string debug = "SUBSHAPES ARE NOT SIMILAR IN SHAPE";
    //            Debug(debug);
    //            DebugMarker();
    //        }
    //    }

    //    EndRegion();
    //    return shapesSimilar;
    //}

    //public bool CompareShapeInfos(ModelData_Shape cadexShape, ModelData_Shape gimShape, ModelData_Face cadexFaceToSeam = null, ModelData_Face gimFaceToSeam = null)
    //{
    //    StartRegion("ShapeInfo");
    //    bool shapesSimilar = cadexShape.Type() == gimShape.Type();
    //    if (!shapesSimilar)
    //    {
    //        string debug = "SHAPES DO NOT HAVE THE SAME TYPE!";
    //        Debug(debug);
    //        Debug(cadexShape.Type() + " vs " + gimShape.Type());
    //        DebugMarker();
    //        EndRegion();
    //        return false;
    //    }

    //    switch (cadexShape.Type())
    //    {
    //        case ModelData_ShapeType.ModelData_ST_Body: break;
    //        case ModelData_ShapeType.ModelData_ST_Solid: break;
    //        case ModelData_ShapeType.ModelData_ST_Shell: 
    //            {
    //                var cadexShell = ModelData_Shell.Cast(cadexShape);
    //                var gimShell = ModelData_Shell.Cast(gimShape);
    //            };
    //            break;
    //        case ModelData_ShapeType.ModelData_ST_Face:
    //            {
    //                bool result = CompareFaces(ModelData_Face.Cast(cadexShape), ModelData_Face.Cast(gimShape));
    //                shapesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "SURFACES ARE NOT THE SAME";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;
    //        case ModelData_ShapeType.ModelData_ST_Edge:
    //            {
    //                bool result = CompareEdges(ModelData_Edge.Cast(cadexShape), ModelData_Edge.Cast(gimShape), cadexFaceToSeam, gimFaceToSeam);
    //                shapesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "EDGES ARE NOT THE SAME";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;
    //        case ModelData_ShapeType.ModelData_ST_Vertex:
    //            {
    //                var cadexVertex = ModelData_Vertex.Cast(cadexShape);
    //                var gimVertex = ModelData_Vertex.Cast(gimShape);

    //                Vect cadexVect = new Vect(cadexVertex.Point().X(), cadexVertex.Point().Y(), cadexVertex.Point().Z());
    //                Vect gimVect = new Vect(gimVertex.Point().X(), gimVertex.Point().Y(), gimVertex.Point().Z());

    //                Debug("Vertex: " + cadexVect + " vs " + gimVect);

    //                if(cadexVect != gimVect)
    //                {
    //                    string debug = "VERTICES ARE NOT THE SAME";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;

    //    }

    //    EndRegion();
    //    return shapesSimilar;
    //}

    //public bool CompareFaces(ModelData_Face cadexFace, ModelData_Face gimFace)
    //{
    //    StartRegion("Faces");
    //    bool facesSimilar = true;
    //    var cadexSurface = cadexFace.Surface();
    //    var gimSurface = gimFace.Surface();

    //    if(cadexSurface?.Type() != gimSurface?.Type())
    //    {
    //        facesSimilar = false;
    //        string debug = "SURFACES ARENT THE SAME TYPE";
    //        Debug(debug);
    //        Debug(cadexSurface?.Type() + " vs " + gimSurface?.Type());
    //        DebugMarker();
    //        EndRegion();
    //        return false;
    //    } 

    //    bool result = CompareSurfaces(cadexSurface, gimSurface);
    //    facesSimilar &= result;
    //    if (!result)
    //    {
    //        string debug = "FACES HAVE DIFFERING SURFACES";
    //        Debug(debug);
    //        DebugMarker();
    //    }
    //    EndRegion();
    //    return facesSimilar;
    //}

    //public bool CompareSurfaces(ModelData_Surface cadexSurface, ModelData_Surface gimSurface)
    //{
    //    StartRegion("Surfaces");
    //    bool surfacesSimilar = true;

    //    if(cadexSurface.Type() != gimSurface.Type())
    //    {
    //        string debug = "SURFACES ARE DIFFERENT TYPE! " + cadexSurface.Type() + " vs " + gimSurface.Type();
    //        Debug(debug);
    //        DebugMarker();
    //        return false;
    //    }

    //    switch (cadexSurface.Type()){
    //        case ModelData_SurfaceType.ModelData_ST_Plane:
    //        case ModelData_SurfaceType.ModelData_ST_Cone:
    //        case ModelData_SurfaceType.ModelData_ST_Cylinder:
    //        case ModelData_SurfaceType.ModelData_ST_Sphere:
    //        case ModelData_SurfaceType.ModelData_ST_Torus:
    //            {
    //                bool result = CompareElementarySurface(ModelData_ElementarySurface.Cast(cadexSurface), ModelData_ElementarySurface.Cast(gimSurface));
    //                surfacesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "ELEMENTARY SURFACES ARE NOT SIMILAR";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;

    //        case ModelData_SurfaceType.ModelData_ST_LinearExtrusion:
    //        case ModelData_SurfaceType.ModelData_ST_Revolution:
    //            {
    //                bool result = CompareSweptSurface(ModelData_SweptSurface.Cast(cadexSurface), ModelData_SweptSurface.Cast(gimSurface));
    //                surfacesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "SWEPT SURFACES ARE NOT SIMILAR";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }
    //            break;
    //        case ModelData_SurfaceType.ModelData_ST_Bezier:
    //            {

    //                var cadexBezier = ModelData_BezierSurface.Cast(cadexSurface);
    //                var gimBezier = ModelData_BezierSurface.Cast(gimSurface);

    //                bool result = CompareBezierSurfaces(cadexBezier, gimBezier);
    //                surfacesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "BEZIER SURFACES ARE NOT SIMILAR";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;
    //        case ModelData_SurfaceType.ModelData_ST_BSpline:
    //            {
    //                var cadexBspline = ModelData_BSplineSurface.Cast(cadexSurface);
    //                var gimBspline = ModelData_BSplineSurface.Cast(gimSurface);

    //                bool result = CompareBsplineSurfaces(cadexBspline, gimBspline);
    //                surfacesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "BSPLINE SURFACES ARE NOT SIMILAR";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }
    //            break;

    //        case ModelData_SurfaceType.ModelData_ST_Offset:
    //            {
    //                var cadexBasis = ModelData_OffsetSurface.Cast(cadexSurface).BasisSurface();
    //                var gimBasis = ModelData_OffsetSurface.Cast(gimSurface).BasisSurface();
    //                bool result = CompareSurfaces(cadexBasis, gimBasis);

    //                surfacesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "OFFSET SURFACES DONT HAVE THE SAME BASIS SURFACE";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;
    //        case ModelData_SurfaceType.ModelData_ST_Trimmed:
    //            {
    //                var cadexTrimmed = ModelData_RectangularTrimmedSurface.Cast(cadexSurface);
    //                var gimTrimmed = ModelData_RectangularTrimmedSurface.Cast(gimSurface);

    //                var cadexBasis = cadexTrimmed.BasisSurface();
    //                var gimBasis = gimTrimmed.BasisSurface();

    //                var cadexUMin = cadexTrimmed.UMin();
    //                var gimUMin = gimTrimmed.UMin();

    //                Debug("UMin: " + cadexUMin + " vs " + gimUMin);

    //                if(!VectorHelpers.CompareDouble(cadexUMin, gimUMin))
    //                {
    //                    string debug = "TRIMMED SURFACES HAVE DIFFERENT UMIN!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    surfacesSimilar = false;
    //                }

    //                var cadexUMax = cadexTrimmed.UMax();
    //                var gimUMax = gimTrimmed.UMax();

    //                Debug("UMax: " + cadexUMax + " vs " + gimUMax);

    //                if (!VectorHelpers.CompareDouble(cadexUMax, gimUMax))
    //                {
    //                    string debug = "TRIMMED SURFACES HAVE DIFFERENT UMAX!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    surfacesSimilar = false;
    //                }

    //                var cadexVMin = cadexTrimmed.VMin();
    //                var gimVMin = gimTrimmed.VMin();

    //                Debug("VMin: " + cadexVMin + " vs " + gimVMin);

    //                if (!VectorHelpers.CompareDouble(cadexVMin, gimVMin))
    //                {
    //                    string debug = "TRIMMED SURFACES HAVE DIFFERENT VMIN!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    surfacesSimilar = false;
    //                }

    //                var cadexVMax = cadexTrimmed.VMax();
    //                var gimVMax = gimTrimmed.VMax();

    //                Debug("VMax: " + cadexVMax + " vs " + gimVMax);

    //                if (!VectorHelpers.CompareDouble(cadexVMax, gimVMax))
    //                {
    //                    string debug = "TRIMMED SURFACES HAVE DIFFERENT VMAX!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    surfacesSimilar = false;
    //                }

    //                var cadexUPeriodic = cadexTrimmed.IsUPeriodic();
    //                var gimUPeriodic = gimTrimmed.IsUPeriodic();

    //                Debug("U-Periodic: " + cadexUPeriodic + " vs " + gimUPeriodic);

    //                if(cadexUPeriodic != gimUPeriodic)
    //                {
    //                    string debug = "CURVES DO NOT HAVE SIMILAR U-PERIODICITY";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    surfacesSimilar = false;
    //                }

    //                var cadexVPeriodic = cadexTrimmed.IsUPeriodic();
    //                var gimVPeriodic = gimTrimmed.IsUPeriodic();

    //                Debug("V-Periodic: " + cadexVPeriodic + " vs " + gimVPeriodic);

    //                if (cadexVPeriodic != gimVPeriodic)
    //                {
    //                    string debug = "CURVES DO NOT HAVE SIMILAR V-PERIODICITY";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    surfacesSimilar = false;
    //                }

    //                bool result = CompareSurfaces(cadexBasis, gimBasis);

    //                surfacesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "TRIMMED SURFACES DONT HAVE THE SAME BASIS SURFACE";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }
    //            break;
    //    }

    //    EndRegion();
    //    return surfacesSimilar;
    //}

    //public bool CompareBsplineSurfaces(ModelData_BSplineSurface cadexBspline, ModelData_BSplineSurface gimBspline)
    //{
    //    StartRegion("Bspline Surface");
    //    bool bsplinesSimilar = true;

    //    int cadexUDegree = cadexBspline.UDegree();
    //    int gimUDegree = gimBspline.UDegree();

    //    int cadexVDegree = cadexBspline.VDegree();
    //    int gimVDegree = gimBspline.VDegree();

    //    Debug("U Degree: " + cadexUDegree + " vs " + gimUDegree);
    //    if(cadexUDegree != gimUDegree)
    //    {
    //        string debug = "BSPLINE SURFACES U-DEGREES ARE DIFFERENT!";
    //        Debug(debug);
    //        DebugMarker();
    //        bsplinesSimilar = false;
    //    }

    //    Debug("V Degree: " + cadexVDegree + " vs " + gimVDegree);
    //    if(cadexVDegree != gimVDegree)
    //    {
    //        string debug = "BSPLINE SURFACES V-DEGREES ARE DIFFERENT!";
    //        Debug(debug);
    //        DebugMarker();
    //        bsplinesSimilar = false;
    //    }

    //    var cadexPoles = cadexBspline.Poles().ToArray();
    //    var gimPoles = gimBspline.Poles().ToArray();


    //    Debug("Pole Count: " + cadexPoles.Length + " vs " + gimPoles.Length);
    //    if (cadexPoles.Length != gimPoles.Length)
    //    {
    //        bsplinesSimilar = false;
    //        string debug = "BSPLINE SURFACES HAVE DIFFERENT POLE COUNT!";
    //        Debug(debug);
    //        DebugMarker();

    //    }

    //    var cadexWeights = cadexBspline.Weights().ToArray();
    //    var gimWeights = gimBspline.Weights().ToArray();

    //    Debug("Weight Count: " + cadexWeights.Length + " vs " + gimWeights.Length);
    //    if (cadexWeights.Length != gimWeights.Length)
    //    {
    //        bsplinesSimilar = false;
    //        string debug = "BSPLINE SURFACES HAVE DIFFERENT WEIGHT COUNT";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    var cadexUMultiplicities = cadexBspline.UMultiplicities().ToArray();
    //    var gimUMultiplicities = cadexBspline.UMultiplicities().ToArray();

    //    Debug("U-Multiplicities Count: " + cadexUMultiplicities.Length + " vs " + gimUMultiplicities.Length);
    //    if (cadexUMultiplicities.Length != gimUMultiplicities.Length)
    //    {
    //        bsplinesSimilar = false;
    //        string debug = "BSPLINE SURFACES HAVE DIFFERENT U MULTIPLICITY COUNTS";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    var cadexVMultiplicities = cadexBspline.VMultiplicities().ToArray();
    //    var gimVMultiplicities = cadexBspline.VMultiplicities().ToArray();

    //    Debug("V-Multiplicities Count: " + cadexVMultiplicities.Length + " vs " + gimVMultiplicities.Length);
    //    if (cadexVMultiplicities.Length != gimVMultiplicities.Length)
    //    {
    //        bsplinesSimilar = false;
    //        string debug = "BSPLINE SURFACES HAVE DIFFERENT V MULTIPLICITY COUNTS";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    var cadexUKnots = cadexBspline.UKnots().ToArray();
    //    var gimUKnots = gimBspline.UKnots().ToArray();

    //    Debug("U-Knots Count: " + cadexUKnots.Length + " vs " + gimUKnots.Length);
    //    if (cadexUKnots.Length != gimUKnots.Length)
    //    {
    //        bsplinesSimilar = false;
    //        string debug = "BSPLINE SURFACES HAVE DIFFERENT UKNOT COUNTS";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    var cadexVKnots = cadexBspline.VKnots().ToArray();
    //    var gimVKnots = gimBspline.VKnots().ToArray();


    //    Debug("V-Knots Count: " +cadexVKnots.Length + " vs " + gimVKnots.Length);
    //    if (cadexVKnots.Length!= gimVKnots.Length)
    //    {
    //        bsplinesSimilar = false;
    //        string debug = "BSPLINE SURFACES HAVE DIFFERENT VKNOT COUNTS";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    StartRegion("Bspline Surface Point");

    //    for(int i=0; i<Math.Min(cadexPoles.Length, gimPoles.Length); i++)
    //    {
    //        Vect cadexPoint = new Vect(cadexPoles[i].X(), cadexPoles[i].Y(), cadexPoles[i].Z());
    //        Vect gimPoint = new Vect(gimPoles[i].X(), gimPoles[i].Y(), gimPoles[i].Z());

    //        Debug("Point " + i + ": " + cadexPoint + " vs " + gimPoint);
    //        if(cadexPoint != gimPoint)
    //        {
    //            string debug = "BSPLINE POINTS " + i + " ARE DIFFERENT!";
    //            Debug(debug);
    //            DebugMarker();
    //            bsplinesSimilar = false;
    //        }
    //    }

    //    EndRegion();

    //    StartRegion("Bspline Surface weights");

    //    for (int i = 0; i < Math.Min(cadexWeights.Length, gimWeights.Length); i++)
    //    {
    //        double cadexWeight = cadexWeights[i];
    //        double gimWeight = gimWeights[i];

    //        Debug("weight " + i + ": " + cadexWeight + " vs " + gimWeight);
    //        if (!VectorHelpers.CompareDouble(cadexWeight, gimWeight))
    //        {
    //            string debug = "BSPLINE WEIGHTS " + i + " ARE DIFFERENT!";
    //            Debug(debug);
    //            DebugMarker();
    //            bsplinesSimilar = false;
    //        }
    //    }

    //    EndRegion();

    //    StartRegion("Bspline Surface U-Knots");

    //    for (int i = 0; i < Math.Min(cadexUKnots.Length, gimUKnots.Length); i++)
    //    {
    //        double cadexKnot = cadexUKnots[i];
    //        double gimKnot = gimUKnots[i];

    //        Debug("U-Knot " + i + ": " + cadexKnot + " vs " + gimKnot);
    //        if (!VectorHelpers.CompareDouble(cadexKnot, gimKnot))
    //        {
    //            string debug = "BSPLINE U-KNOTS " + i + " ARE DIFFERENT!";
    //            Debug(debug);
    //            DebugMarker();
    //            bsplinesSimilar = false;
    //        }
    //    }

    //    EndRegion();

    //    StartRegion("Bspline Surface V-Knots");

    //    for (int i = 0; i < Math.Min(cadexVKnots.Length, gimVKnots.Length); i++)
    //    {
    //        double cadexKnot = cadexVKnots[i];
    //        double gimKnot = gimVKnots[i];

    //        Debug("V-Knot " + i + ": " + cadexKnot + " vs " + gimKnot);
    //        if (!VectorHelpers.CompareDouble(cadexKnot, gimKnot))
    //        {
    //            string debug = "BSPLINE V-KNOTS " + i + " ARE DIFFERENT!";
    //            Debug(debug);
    //            DebugMarker();
    //            bsplinesSimilar = false;
    //        }
    //    }

    //    EndRegion();

    //    StartRegion("Bspline Surface U-Multiplicities");

    //    for (int i = 0; i < Math.Min(cadexUMultiplicities.Length, gimUMultiplicities.Length); i++)
    //    {
    //        int cadexMultiplicity = cadexUMultiplicities[i];
    //        int gimMultiplicity = gimUMultiplicities[i];

    //        Debug("U-Multiplicity " + i + ": " + cadexMultiplicity + " vs " + gimMultiplicity);
    //        if (cadexMultiplicity != gimMultiplicity)
    //        {
    //            string debug = "BSPLINE U-MULTIPLICTIES " + i + " ARE DIFFERENT!";
    //            Debug(debug);
    //            DebugMarker();
    //            bsplinesSimilar = false;
    //        }
    //    }

    //    EndRegion();

    //    StartRegion("Bspline Surface V-Multiplicities");

    //    for (int i = 0; i < Math.Min(cadexVMultiplicities.Length, gimVMultiplicities.Length); i++)
    //    {
    //        int cadexMultiplicity = cadexVMultiplicities[i];
    //        int gimMultiplicity = gimVMultiplicities[i];

    //        Debug("V-Multiplicity " + i + ": " + cadexMultiplicity + " vs " + gimMultiplicity);
    //        if (cadexMultiplicity != gimMultiplicity)
    //        {
    //            string debug = "BSPLINE V-MULTIPLICTIES " + i + " ARE DIFFERENT!";
    //            Debug(debug);
    //            DebugMarker();
    //            bsplinesSimilar = false;
    //        }
    //    }

    //    EndRegion();

    //    EndRegion();

    //    return bsplinesSimilar;
    //}

    //public bool CompareBezierSurfaces(ModelData_BezierSurface cadexBezierSurface, ModelData_BezierSurface gimBezierSurface)
    //{
    //    StartRegion("Bezier Surface");
    //    bool bezierSurfacesSimilar = true;

    //    var cadexPoles = cadexBezierSurface.Poles().ToArray();
    //    var gimPoles = cadexBezierSurface.Poles().ToArray();

    //    Debug("Pole Count: " + cadexPoles.Length + " vs " + gimPoles.Length);
    //    if (cadexPoles.Length != gimPoles.Length)
    //    {

    //        bezierSurfacesSimilar = false;
    //        string debug = "BEZIERS SURFACES HAVE DIFFERENT POLE COUNT!";
    //        Debug(debug);
    //        DebugMarker();

    //    }

    //    var cadexWeights = cadexBezierSurface.Weights().ToArray();
    //    var gimWeights = gimBezierSurface.Weights().ToArray();


    //    Debug("Weight Count: " + cadexWeights.Length + " vs " + gimWeights.Length);
    //    if (cadexWeights.Length != gimWeights.Length)
    //    {
    //        bezierSurfacesSimilar = false;
    //        string debug = "BEZIERS SURFACES HAVE DIFFERENT WEIGHT COUNT";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    return bezierSurfacesSimilar;
    //}

    //public bool CompareSweptSurface(ModelData_SweptSurface cadexSweptSurface, ModelData_SweptSurface gimSweptSurface)
    //{
    //    StartRegion("Swept Surface");
    //    bool sweptSurfaceSimilar = true;

    //    switch (cadexSweptSurface.Type())
    //    {
    //        case ModelData_SurfaceType.ModelData_ST_Revolution:
    //            {
    //                StartRegion("Revolute");
    //                var cadexRevolute = ModelData_SurfaceOfRevolution.Cast(cadexSweptSurface);
    //                var gimRevolute = ModelData_SurfaceOfRevolution.Cast(gimSweptSurface);

    //                Vect cadexLocation = new Vect(cadexRevolute.Location().X(), cadexRevolute.Location().Y(), cadexRevolute.Location().Z());
    //                Vect gimLocation = new Vect(gimRevolute.Location().X(), gimRevolute.Location().Y(), gimRevolute.Location().Z());

    //                Debug("Location: " + cadexLocation + " vs " + gimLocation);

    //                if (cadexLocation != gimLocation)
    //                {
    //                    sweptSurfaceSimilar = false;
    //                    string debug = "REVOLUTES HAVE DIFFERENT LOCATION";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //                EndRegion();

    //            }break;
    //    }

    //    Vect cadexVect = new Vect(cadexSweptSurface.Direction().X(), cadexSweptSurface.Direction().Y(), cadexSweptSurface.Direction().Z());
    //    Vect gimVect = new Vect(gimSweptSurface.Direction().X(), gimSweptSurface.Direction().Y(), gimSweptSurface.Direction().Z());


    //    Debug("Direction: " + cadexVect + " vs " + gimVect);
    //    if (cadexVect != gimVect)
    //    {
    //        sweptSurfaceSimilar = false;

    //        string debug = "EXTRUSION/REVOLUTE HAVE DIFFERENT DIRECTIONS";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    var cadexBasis = cadexSweptSurface.BasisCurve();
    //    var gimBasis = gimSweptSurface.BasisCurve();
    //    bool result = CompareCurve(cadexBasis, gimBasis);

    //    sweptSurfaceSimilar &= result;

    //    if (!result)
    //    {
    //        string debug = "EXTRUSION/REVOLUTE HAVE DIFFERENT BASIS CURVES!";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    EndRegion();

    //    return sweptSurfaceSimilar;
    //}

    //public bool CompareElementarySurface(ModelData_ElementarySurface cadexElementarySurface, ModelData_ElementarySurface gimElementarySurface)
    //{
    //    StartRegion("Elementary Surface");
    //    bool elementarySurfacesSimilar = true;

    //    switch (cadexElementarySurface.Type())
    //    {
    //        case ModelData_SurfaceType.ModelData_ST_Plane: break;
    //        case ModelData_SurfaceType.ModelData_ST_Cylinder:
    //            {
    //                StartRegion("Cylinder");
    //                var cadexCylinder = ModelData_CylindricalSurface.Cast(cadexElementarySurface);
    //                var gimCylinder = ModelData_CylindricalSurface.Cast(gimElementarySurface);

    //                var cadexRadius = cadexCylinder.Radius();
    //                var gimRadius = gimCylinder.Radius();

    //                Debug("Radius: " + cadexRadius + " vs " + gimRadius);
    //                if (!VectorHelpers.CompareDouble(cadexRadius, gimRadius))
    //                {
    //                    string debug = "CYLINDERS HAVE DIFFERENT RADII";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    elementarySurfacesSimilar = false;
    //                }
    //                EndRegion();
    //            }
    //            break;

    //        case ModelData_SurfaceType.ModelData_ST_Cone:
    //            {
    //                StartRegion("Cone");
    //                var cadexCone = ModelData_ConicalSurface.Cast(cadexElementarySurface);
    //                var gimCone = ModelData_ConicalSurface.Cast(gimElementarySurface);

    //                var cadexRadius = cadexCone.Radius();
    //                var gimRadius = gimCone.Radius();

    //                Debug("Radius: " + cadexRadius + " vs " + gimRadius);
    //                if (!VectorHelpers.CompareDouble(cadexRadius, gimRadius))
    //                {
    //                    string debug = "CONES HAVE DIFFERENT RADII";
    //                    elementarySurfacesSimilar = false;
    //                    Debug(debug);
    //                    DebugMarker();
    //                }

    //                var cadexAngle = cadexCone.Angle();
    //                var gimAngle = gimCone.Angle();

    //                Debug("Angle: " + cadexAngle + " vs " + gimAngle);
    //                if (!VectorHelpers.CompareDouble(cadexAngle, gimAngle))
    //                {
    //                    string debug = "CONES HAVE DIFFERENT ANGLES";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    elementarySurfacesSimilar = false;
    //                }
    //                EndRegion();
    //            }
    //            break;

    //        case ModelData_SurfaceType.ModelData_ST_Sphere:
    //            {
    //                StartRegion("Sphere");
    //                var cadexSphere = ModelData_SphericalSurface.Cast(cadexElementarySurface);
    //                var gimSphere = ModelData_SphericalSurface.Cast(gimElementarySurface);

    //                var cadexRadius = cadexSphere.Radius();
    //                var gimRadius = gimSphere.Radius();

    //                Debug("Radius:" + cadexRadius + " vs " + gimRadius);
    //                if (!VectorHelpers.CompareDouble(cadexRadius, gimRadius))
    //                {
    //                    string debug = "SPHERES HAVE DIFFERENT RADII";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    elementarySurfacesSimilar = false;
    //                }
    //                EndRegion();
    //            }
    //            break;
    //        case ModelData_SurfaceType.ModelData_ST_Torus:
    //            {
    //                StartRegion("Torus");
    //                var cadexTorus = ModelData_ToroidalSurface.Cast(cadexElementarySurface);
    //                var gimTorus = ModelData_ToroidalSurface.Cast(gimElementarySurface);

    //                var cadexMajorRadius = cadexTorus.MajorRadius();
    //                var gimMajorRadius = gimTorus.MajorRadius();

    //                Debug("Major Radius:" + cadexMajorRadius + " vs " + gimMajorRadius);
    //                if (!VectorHelpers.CompareDouble(cadexMajorRadius, gimMajorRadius))
    //                {
    //                    string debug = "TORII HAVE DIFFERENT MAJOR RADII";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    elementarySurfacesSimilar = false;
    //                }

    //                var cadexMinorRadius = cadexTorus.MinorRadius();
    //                var gimMinorRadius = gimTorus.MinorRadius();


    //                Debug("Minor Radius:" + cadexMinorRadius + " vs " + gimMinorRadius);
    //                if (!VectorHelpers.CompareDouble(cadexMinorRadius, gimMinorRadius))
    //                {
    //                    string debug = "TORII HAVE DIFFERENT MINOR RADII";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    elementarySurfacesSimilar = false;
    //                }
    //                EndRegion();
    //            }
    //            break;

    //    }

    //    var cadexPosition = cadexElementarySurface.Position();
    //    var gimPosition = gimElementarySurface.Position();

    //    Vect cadexLocation = new Vect(cadexPosition.Location().X(), cadexPosition.Location().Y(), cadexPosition.Location().Z());
    //    Vect gimLocation = new Vect(gimPosition.Location().X(), gimPosition.Location().Y(), gimPosition.Location().Z());

    //    Debug("Location: " + cadexLocation + " vs " + gimLocation);
    //    if (cadexLocation != gimLocation)
    //    {
    //        string debug = "ELEMENTARY SURFACES HAVE DIFFERENT LOCATION";
    //        Debug(debug);
    //        DebugMarker();
    //        elementarySurfacesSimilar = false;
    //    }

    //    Vect cadexXDirection = new Vect(cadexPosition.XDirection().X(), cadexPosition.XDirection().Y(), cadexPosition.XDirection().Z());
    //    Vect gimXDirection = new Vect(gimPosition.XDirection().X(), gimPosition.XDirection().Y(), gimPosition.XDirection().Z());

    //    Debug("X-Direction: " + cadexXDirection + " vs " + gimXDirection);
    //    if (cadexXDirection != gimXDirection)
    //    {
    //        string debug = "ELEMENTARY SURFACES HAVE DIFFERENT X DIRECTION";
    //        Debug(debug);
    //        DebugMarker();
    //        elementarySurfacesSimilar = false;
    //    }

    //    Vect cadexYDirection = new Vect(cadexPosition.YDirection().X(), cadexPosition.YDirection().Y(), cadexPosition.YDirection().Z());
    //    Vect gimYDirection = new Vect(gimPosition.YDirection().X(), gimPosition.YDirection().Y(), gimPosition.YDirection().Z());

    //    Debug("Y-Direction: " + cadexXDirection + " vs " + cadexYDirection);
    //    if (cadexYDirection != gimYDirection)
    //    {
    //        string debug = "ELEMENTARY SURFACES HAVE DIFFERENT Y DIRECTION";
    //        Debug(debug);
    //        DebugMarker();

    //        elementarySurfacesSimilar = false;
    //    }

    //    Vect cadexZDirection = new Vect(cadexPosition.Axis().X(), cadexPosition.Axis().Y(), cadexPosition.Axis().Z());
    //    Vect gimZDirection = new Vect(gimPosition.Axis().X(), gimPosition.Axis().Y(), gimPosition.Axis().Z());


    //    Debug("Z-Axis: " + cadexZDirection + " vs " + gimZDirection);
    //    if (cadexZDirection != gimZDirection)
    //    {
    //        string debug = "ELEMENTARY SURFACES HAVE DIFFERENT Z AXIS";
    //        Debug(debug);
    //        DebugMarker();
    //        elementarySurfacesSimilar = false;
    //    }
    //    EndRegion();
    //    return elementarySurfacesSimilar;
    //}

    //public bool CompareEdges(ModelData_Edge cadexEdge, ModelData_Edge gimEdge, ModelData_Face cadexFaceToSeam = null, ModelData_Face gimFaceToSeam = null)
    //{
    //    StartRegion("Edge");

    //    bool edgesSimilar = true;

    //    double cadexParamStart = 0.0, cadexParamEnd = 0.0, gimParamStart = 0.0, gimParamEnd = 0.0;
    //    ModelData_Curve cadexCurve = null, gimCurve;

    //    try
    //    {
    //        gimCurve = gimEdge.Curve(ref gimParamStart, ref gimParamEnd);
    //    }
    //    catch
    //    {
    //        Debug("Exception caught when reading edge curve, aborting...");
    //        DebugMarker();
    //        edgesSimilar = false;
    //        EndRegion();
    //        return false;
    //    }

    //    try
    //    {
    //        cadexCurve = cadexEdge.Curve(ref cadexParamStart, ref cadexParamEnd);
    //    }
    //    catch
    //    {
    //        Debug("Exception caught when reading edge curve, aborting...");
    //        DebugMarker();
    //        edgesSimilar = false;
    //        EndRegion();
    //        return false;
    //    }

    //    Vect cadexStartVertex = new Vect(cadexEdge.StartVertex().Point().X(), cadexEdge.StartVertex().Point().Y(), cadexEdge.StartVertex().Point().Z());
    //    Vect gimStartVertex = new Vect(gimEdge.StartVertex().Point().X(), gimEdge.StartVertex().Point().Y(), gimEdge.StartVertex().Point().Z());

    //    Vect cadexEndVertex = new Vect(cadexEdge.EndVertex().Point().X(), cadexEdge.EndVertex().Point().Y(), cadexEdge.EndVertex().Point().Z());
    //    Vect gimEndVertex = new Vect(gimEdge.EndVertex().Point().X(), gimEdge.EndVertex().Point().Y(), gimEdge.EndVertex().Point().Z());

    //    var cadexOrientation = cadexEdge.Orientation();
    //    var gimOrientation = gimEdge.Orientation();

    //    Debug("Orientation: " + cadexOrientation + " vs " + gimOrientation);

    //    if(cadexOrientation != gimOrientation)
    //    {
    //        string debug = "EDGES HAVE DIFFERENT ORIENTATIONS!";
    //        Debug(debug);
    //        DebugMarker();
    //        edgesSimilar = false;
    //    }

    //    Debug("Edge Start: " + cadexStartVertex + " vs " + gimStartVertex);
    //    if(cadexStartVertex != gimStartVertex)
    //    {
    //        string debug = "EDGES HAVE DIFFERENT START VERTICIES!";
    //        Debug(debug);
    //        DebugMarker();
    //        edgesSimilar = false;
    //    }
    //    Debug("Edge End: " + cadexEndVertex + " vs " + gimEndVertex);
    //    if (cadexEndVertex != gimEndVertex)
    //    {
    //        string debug = "EDGES HAVE DIFFERENT END VERTICIES!";
    //        Debug(debug);
    //        DebugMarker();
    //        edgesSimilar = false;
    //    }

    //    if (cadexFaceToSeam != null && gimFaceToSeam != null)
    //    {
    //        bool cadexSeam = cadexEdge.IsSeam(cadexFaceToSeam);
    //        bool gimSeam = gimEdge.IsSeam(gimFaceToSeam);

    //        Debug("Seam Edge: " + cadexSeam + " vs " + gimSeam);

    //        if(cadexSeam && gimSeam)
    //        {
    //            Debug("Both edges are a steam");

    //            double cadexSeamStartParam = 0.0, cadexSeamEndParam = 0.0;
    //            double gimSeamStartParam = 0.0, gimSeamEndParam = 0.0;
    //            ModelData_Curve2d cadex2DCurve = cadexEdge.PCurve(cadexFaceToSeam, ref cadexSeamStartParam, ref cadexSeamEndParam);
    //            ModelData_Curve2d gim2DCurve = gimEdge.PCurve(gimFaceToSeam, ref gimSeamStartParam,ref gimSeamEndParam);

    //            Debug("Seam Start Param: " + cadexSeamStartParam + " vs " + gimSeamStartParam);
    //            if (!VectorHelpers.CompareDouble(cadexSeamStartParam, gimSeamStartParam))
    //            {
    //                Debug("SEAM EDGES HAVE DIFFERENT START PARAM!");
    //            }
    //            Debug("Seam End Param:" + cadexSeamEndParam + " vs " + gimSeamEndParam);
    //            if (!VectorHelpers.CompareDouble(cadexSeamEndParam, gimSeamEndParam))
    //            {
    //                Debug("SEAM EDGES HAVE DIFFERENT END PARAMS!");
    //            }

    //            double cadexSeamLength = cadexSeamEndParam - cadexSeamStartParam;
    //            double gimSeamLength = gimSeamEndParam - gimSeamStartParam;

    //            Debug("Seam Length: " + cadexSeamLength + " vs " + gimSeamLength);

    //            if(!VectorHelpers.CompareDouble(cadexSeamLength, gimSeamLength))
    //            {
    //                Debug("SEAM EDGES HAVE DIFFERENT LENGTHS!");
    //                DebugMarker();
    //                edgesSimilar = false;
    //            }

    //            bool result2D = Compare2DCurves(cadex2DCurve, gim2DCurve);
    //        }
    //        else if (cadexSeam)
    //        {
    //            Debug("CADEX IS SEAM WHILE GIM IS NOT!");
    //            DebugMarker();
    //            edgesSimilar = false; 

    //            double cadexSeamStartParam = 0.0, cadexSeamEndParam = 0.0;
    //            ModelData_Curve2d cadex2DCurve = cadexEdge.PCurve(cadexFaceToSeam, ref cadexSeamStartParam, ref cadexSeamEndParam);

    //        }
    //        else if (gimSeam)
    //        {
    //            Debug("GIM IS SEAM WHILE CADEX IS NOT!");
    //            DebugMarker();
    //            edgesSimilar = false;

    //            double gimSeamStartParam = 0.0, gimSeamEndParam = 0.0;
    //            ModelData_Curve2d gim2DCurve = gimEdge.PCurve(gimFaceToSeam, ref gimSeamStartParam, ref gimSeamEndParam);

    //        }
    //    }



    //    double startDiff = Math.Abs(cadexParamStart - gimParamStart);
    //    Debug("Start Param: " + cadexParamStart + " vs " + gimParamStart);
    //    Debug("End Param: " + cadexParamEnd + " vs " + gimParamEnd);
    //    double endDiff = Math.Abs(cadexParamEnd - gimParamEnd);

    //    Debug("Cadex same: " + cadexEdge.IsSame(gimEdge));
    //    Debug("Cadex equal: " + cadexEdge.IsEqual(gimEdge));

    //    Debug("Tolerance: " + cadexEdge.Tolerance() + " vs " + gimEdge.Tolerance());

    //    bool startsAreDifferent = false, endsAreDifferent = false;
    //    if(startDiff > Vect.EQUALITY_MARGIN)
    //    {
    //        string debug = "EDGES HAVE DIFFERENT START PARAMETERS!";
    //        Debug(debug);
    //        startsAreDifferent = true;
    //    }

    //    if(endDiff > Vect.EQUALITY_MARGIN)
    //    {
    //        string debug = "EDGES HAVE DIFFERENT END PARAMETERS!";
    //        Debug(debug);
    //        endsAreDifferent = true;
    //    }

    //    if(startsAreDifferent && endsAreDifferent)
    //    {
    //        double cadexLength = cadexParamEnd - cadexParamStart;
    //        double gimLength = gimParamEnd - gimParamStart;


    //        Debug("Edge Legth: " + cadexLength + " vs " + gimLength);

    //        double lengthDiff = Math.Abs(cadexLength - gimLength);

    //        if(lengthDiff > Vect.EQUALITY_MARGIN)
    //        {
    //            string debug = "EDGES ARE DIFFERENT LENGTH";
    //            Debug(debug);
    //            DebugMarker();
    //            edgesSimilar = false;
    //        }
    //        else
    //        {
    //            Debug("edges have same length = " + cadexLength + ", OK");
    //        }
    //    }
    //    else if (startsAreDifferent)
    //    {
    //        edgesSimilar = false;
    //        string debug = "ONLY STARTS ARE DIFFERENT";
    //        Debug(debug);
    //        DebugMarker();
    //    }
    //    else if (endsAreDifferent)
    //    {
    //        edgesSimilar = false;
    //        string debug = "ONLY ENDS ARE DIFFERENT";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    bool result = CompareCurve(cadexCurve, gimCurve);

    //    edgesSimilar &= result;
    //    if (!result)
    //    {
    //        string debug = "CURVES ARE DIFFERENT";
    //        Debug(debug);
    //        DebugMarker();
    //    }
    //    EndRegion();
    //    return edgesSimilar;
    //}

    //public bool CompareCurve(ModelData_Curve cadexCurve, ModelData_Curve gimCurve)
    //{
    //    StartRegion("Curve");
    //    bool curvesSimilar = true;

    //    if(cadexCurve == null && gimCurve == null)
    //    {
    //        Debug("NULL CURVES");
    //        EndRegion();
    //        return true;

    //    }

    //    if(cadexCurve?.Type() != gimCurve?.Type())
    //    {
    //        string debug = "CURVES ARE DIFFERENT TYPES";
    //        Debug(debug);
    //        Debug(cadexCurve?.Type() + " vs " + gimCurve?.Type());
    //        DebugMarker();
    //        EndRegion();
    //        return false;
    //    }

    //    switch (cadexCurve.Type())
    //    {
    //        case ModelData_CurveType.ModelData_CT_Line:
    //            {
    //                bool result = CompareLines(ModelData_Line.Cast(cadexCurve), ModelData_Line.Cast(gimCurve));
    //                curvesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "LINES ARE NOT SIMILAR";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;
    //        case ModelData_CurveType.ModelData_CT_Circle:
    //        case ModelData_CurveType.ModelData_CT_Ellipse:
    //        case ModelData_CurveType.ModelData_CT_Hyperbola:
    //        case ModelData_CurveType.ModelData_CT_Parabola:
    //            {
    //                bool result = CompareConics(cadexCurve, gimCurve);
    //                curvesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "CONICS ARE NOT SIMILAR";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }
    //            break;
    //        case ModelData_CurveType.ModelData_CT_Bezier:
    //        case ModelData_CurveType.ModelData_CT_BSpline:
    //            {
    //                bool result = CompareBeziers(cadexCurve, gimCurve);
    //                curvesSimilar &= result;
    //                if (!result)
    //                {
    //                    string debug = "BEZIERS ARE NOT SIMILAR";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //            }break;
    //        case ModelData_CurveType.ModelData_CT_Offset:
    //        case ModelData_CurveType.ModelData_CT_Trimmed:
    //            {
    //                ModelData_Curve cadexBasisCurve = null;
    //                ModelData_Curve gimBasisCurve = null;

    //                if(cadexCurve.Type() == ModelData_CurveType.ModelData_CT_Offset)
    //                {
    //                    var cadexOffset = ModelData_OffsetCurve.Cast(cadexCurve);
    //                    var gimOffset = ModelData_OffsetCurve.Cast(gimCurve);

    //                    double cadexOffsetValue = cadexOffset.Offset();
    //                    double gimOffsetValue = gimOffset.Offset();
    //                    Debug("Offset Value: " + cadexOffsetValue + " vs " + gimOffsetValue);

    //                    if(!VectorHelpers.CompareDouble(cadexOffsetValue, gimOffsetValue))
    //                    {
    //                        string debug = "OFFSET CURVES HAVE DIFFERENT OFFSET VALUES";
    //                        curvesSimilar = false;
    //                        Debug(debug);
    //                        DebugMarker();
    //                    }

    //                    Vect cadexVect = new Vect(cadexOffset.Direction().X(), cadexOffset.Direction().Y(), cadexOffset.Direction().Z());
    //                    Vect gimVect = new Vect(gimOffset.Direction().X(), gimOffset.Direction().Y(), gimOffset.Direction().Z());


    //                    Debug("Offset Direction: " + cadexVect + " vs " + gimVect);

    //                    if (cadexVect != gimVect)
    //                    {
    //                        string debug = "OFFSET CURVES HAVE DIFFERENT VECTORS";
    //                        curvesSimilar = false;
    //                        DebugMarker();
    //                    }

    //                    cadexBasisCurve = cadexOffset.BasisCurve();
    //                    gimBasisCurve = gimOffset.BasisCurve();

    //                }

    //                if(cadexCurve.Type() == ModelData_CurveType.ModelData_CT_Trimmed)
    //                {
    //                    var cadexTrimmed = ModelData_TrimmedCurve.Cast(cadexCurve);
    //                    var gimTrimmed = ModelData_TrimmedCurve.Cast(gimCurve);

    //                    cadexBasisCurve = cadexTrimmed.BasisCurve();
    //                    gimBasisCurve = gimTrimmed.BasisCurve();
    //                }


    //                bool result = CompareCurve(cadexBasisCurve, gimBasisCurve);
    //                if (!result)
    //                {
    //                    string debug = "OFFSET/TRIMMED HAVE A DIFFERENT BASIS";
    //                    Debug(debug);
    //                    DebugMarker();
    //                }
    //                curvesSimilar &= result;
    //            }
    //            break;
    //    }
    //    EndRegion();
    //    return curvesSimilar;
    //}

    //public bool CompareBeziers(ModelData_Curve cadexCurve, ModelData_Curve gimCurve)
    //{

    //    bool beziersSimilar = true;

    //    switch (cadexCurve.Type())
    //    {
    //        case ModelData_CurveType.ModelData_CT_Bezier:
    //            {
    //                StartRegion("Bezier Curve");
    //                var cadexBezier = ModelData_BezierCurve.Cast(cadexCurve);
    //                var gimBezier = ModelData_BezierCurve.Cast(gimCurve);

    //                var cadexPoles = cadexBezier.Poles().ToArray();
    //                var gimPoles = gimBezier.Poles().ToArray();
    //                Debug("Number of Poles: " + cadexPoles.Length + " vs " + gimPoles.Length);

    //                if (!VectorHelpers.CompareDouble(cadexPoles.Length, gimPoles.Length))
    //                {
    //                    string debug = "BEZIERS HAVE DIFFERENT POLE COUNTS";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    beziersSimilar = false;

    //                }

    //                var cadexWeights = cadexBezier.Weights().ToArray();
    //                var gimWeights = gimBezier.Weights().ToArray();

    //                Debug("Number of Weights: " + cadexWeights.Length + " vs " + gimWeights.Length);
    //                if (!VectorHelpers.CompareDouble(cadexWeights.Length, gimWeights.Length))
    //                {
    //                    string debug = "BEZIERS HAVE DIFFERENT WEIGHT COUNTS";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    beziersSimilar = false;
    //                }

    //                if (beziersSimilar)
    //                {
    //                    StartRegion("Bezier Points");
    //                    for(int i =0; i < Math.Min(cadexPoles.Length, gimPoles.Length); i++)
    //                    {
    //                        Vect cadexPoint = new Vect(cadexPoles[i].X(), cadexPoles[i].Y(), cadexPoles[i].Z());
    //                        Vect gimPoint = new Vect(gimPoles[i].X(), gimPoles[i].Y(), gimPoles[i].Z());

    //                        Debug("Point " + i + ": " + cadexPoint + " vs " + gimPoint);
    //                        if(cadexPoint != gimPoint)
    //                        {
    //                            string debug = "BEZIER POINTS " + i + " ARE DIFFERENT";
    //                            Debug(debug);
    //                            DebugMarker();
    //                            beziersSimilar = false;
    //                        }
    //                    }
    //                    EndRegion();

    //                    StartRegion("Bezier Weights");
    //                    for(int i=0; i < Math.Min(cadexWeights.Length, gimWeights.Length); i++)
    //                    {
    //                        double cadexWeight = cadexWeights[i];
    //                        double gimWeight = gimWeights[i];

    //                        Debug("Weight " + i + ": " + cadexWeight + " vs " + gimWeight);
    //                        if (!VectorHelpers.CompareDouble(cadexWeight, gimWeight))
    //                        {
    //                            string debug = "BEZIER WEIGHTS " + i + " ARE DIFFERENT";
    //                            Debug(debug);
    //                            DebugMarker();
    //                            beziersSimilar = false;
    //                        }
    //                    }
    //                    EndRegion();
    //                }


    //            }break;

    //        case ModelData_CurveType.ModelData_CT_BSpline:
    //            {
    //                StartRegion("Bspline Curve");
    //                var cadexBspline = ModelData_BSplineCurve.Cast(cadexCurve);
    //                var gimBspline = ModelData_BSplineCurve.Cast(gimCurve);

    //                var cadexPoles = cadexBspline.Poles().ToArray();
    //                var gimPoles = gimBspline.Poles().ToArray();

    //                var cadexWeights = cadexBspline.Weights().ToArray();
    //                var gimWeights = gimBspline.Weights().ToArray();

    //                int cadexDegree = cadexBspline.Degree();
    //                int gimDegree = gimBspline.Degree();

    //                Debug("Degree: " + cadexDegree + " vs " + gimDegree);


    //                Debug("CadexEquality: " + cadexBspline.IsEqual(gimBspline));
    //                Debug("Periodic: " + cadexBspline.IsPeriodic() + " vs " + gimBspline.IsPeriodic());
    //                Debug("Rational: " + cadexBspline.IsRational() + " vs " + gimBspline.IsRational());
    //                Debug("Continuity: " + cadexBspline.Continuity() + " vs " + gimBspline.Continuity());
    //                if (!VectorHelpers.CompareDouble(cadexDegree, gimDegree))
    //                {
    //                    string debug = "BSPLINES DONT HAVE THE SAME DEGREE";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    beziersSimilar = false;
    //                }

    //                Debug("Number of Poles: " + cadexPoles.Length + " vs " + gimPoles.Length);

    //                if (!VectorHelpers.CompareDouble(cadexPoles.Length, gimPoles.Length))
    //                {
    //                    string debug = "BSPLINES HAVE DIFFERENT POLE COUNTS";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    beziersSimilar = false;
    //                }

    //                var cadexKnots = cadexBspline.Knots().ToArray();
    //                var gimKnots = gimBspline.Knots().ToArray();

    //                Debug("Number of Knots: " + cadexKnots.Length + " vs " + gimKnots.Length);

    //                if (!VectorHelpers.CompareDouble(cadexKnots.Length, gimKnots.Length))
    //                {
    //                    string debug = "BSPLINES HAVE DIFFERENT KNOT COUNT";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    beziersSimilar = false;
    //                }

    //                var cadexMultiplicities = cadexBspline.Multiplicities().ToArray();
    //                var gimMultiplicities = gimBspline.Multiplicities().ToArray();


    //                Debug("Number of Multiplicties: " + cadexMultiplicities.Length + " vs " + gimMultiplicities.Length);

    //                if (!VectorHelpers.CompareDouble(cadexMultiplicities.Length, gimMultiplicities.Length))
    //                {
    //                    string debug = "BSPLINES HAVE DIFFERENT MULTIPLICIES";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    beziersSimilar = false;
    //                }

    //                Debug("Number of Weights: " + cadexWeights.Length + " vs " + gimWeights.Length);

    //                if(!VectorHelpers.CompareDouble(cadexWeights.Length, gimWeights.Length))
    //                {
    //                    string debug = "BSPLINES HAVE DIFFERENT MULTIPLICIES";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    beziersSimilar = false;
    //                }

    //                StartRegion("Bspline Points");
    //                for (int i = 0; i < Math.Min(cadexPoles.Length, gimPoles.Length); i++)
    //                {
    //                    Vect cadexPoint = new Vect(cadexPoles[i].X(), cadexPoles[i].Y(), cadexPoles[i].Z());
    //                    Vect gimPoint = new Vect(gimPoles[i].X(), gimPoles[i].Y(), gimPoles[i].Z());

    //                    Debug("Point " + i + ": " + cadexPoint + " vs " + gimPoint);
    //                    if (cadexPoint != gimPoint)
    //                    {
    //                        string debug = "BSPLINE POINTS " + i + " ARE DIFFERENT";
    //                        Debug(debug);
    //                        DebugMarker();
    //                        beziersSimilar = false;
    //                    }
    //                }
    //                EndRegion();

    //                StartRegion("Bspline Weights");
    //                for (int i = 0; i < Math.Min(cadexWeights.Length, gimWeights.Length); i++)
    //                {
    //                    double cadexWeight = cadexWeights[i];
    //                    double gimWeight = gimWeights[i];

    //                    Debug("Weight " + i + ": " + cadexWeight + " vs " + gimWeight);
    //                    if (!VectorHelpers.CompareDouble(cadexWeight, gimWeight))
    //                    {
    //                        string debug = "BSPLINE WEIGHTS " + i + " ARE DIFFERENT";
    //                        Debug(debug);
    //                        DebugMarker();
    //                        beziersSimilar = false;
    //                    }
    //                }
    //                EndRegion();


    //                StartRegion("Bspline Knots");
    //                for (int i = 0; i < Math.Min(cadexKnots.Length, gimKnots.Length); i++)
    //                {
    //                    double cadexKnot = cadexKnots[i];
    //                    double gimKnot = gimKnots[i];

    //                    Debug("Knot " + i + ": " + cadexKnot + " vs " + gimKnot);
    //                    if (!VectorHelpers.CompareDouble(cadexKnot, gimKnot))
    //                    {
    //                        string debug = "BSPLINE KNOTS " + i + " ARE DIFFERENT";
    //                        Debug(debug);
    //                        DebugMarker();
    //                        beziersSimilar = false;
    //                    }
    //                }
    //                EndRegion();

    //                StartRegion("Bspline Multiplicities");
    //                for(int i = 0; i < Math.Min(cadexMultiplicities.Length, gimMultiplicities.Length); i++)
    //                {
    //                    int cadexMultiplicity = cadexMultiplicities[i];
    //                    int gimMultiplicity = gimMultiplicities[i];

    //                    Debug("Multiplicity " + i + ": " + cadexMultiplicity + " vs " + gimMultiplicity);
    //                    if(!VectorHelpers.CompareDouble(cadexMultiplicity, gimMultiplicity))
    //                    {
    //                        string debug = "BSPLINE MULTIPLICITY " + i + " ARE DIFFERENT";
    //                        Debug(debug);
    //                        DebugMarker();
    //                        beziersSimilar = false;
    //                    }
    //                }
    //                EndRegion();

    //            }
    //            break;
    //        default: return false;
    //    }

    //    EndRegion();

    //    return beziersSimilar;
    //}

    //public bool CompareLines(ModelData_Line cadexLine, ModelData_Line gimLine)
    //{
    //    StartRegion("Line");
    //    bool linesSimilar = true;

    //    Vect cadexVect = new Vect(cadexLine.Direction().X(), cadexLine.Direction().Y(), cadexLine.Direction().Z());
    //    Vect gimVect = new Vect(gimLine.Direction().X(), gimLine.Direction().Y(), gimLine.Direction().Z());

    //    Vect cadexOrigin = new Vect(cadexLine.Location().X(), cadexLine.Location().Y(), cadexLine.Location().Z());
    //    Vect gimOrigin = new Vect(gimLine.Location().X(), gimLine.Location().Y(), gimLine.Location().Z());

    //    Debug("Line Direction: " + cadexVect + " vs " + gimVect);

    //    if (gimVect != cadexVect)
    //    {
    //        linesSimilar = false;
    //        string debug = "LINES HAVE DIFFERENT DIRECTIONS";
    //        Debug(debug);
    //        DebugMarker();
    //    }

    //    Debug("Line Origin: " + cadexOrigin + " vs " + gimOrigin);

    //    if(gimOrigin != cadexOrigin)
    //    {
    //        string debug = "LINES HAVE DIFFERENT ORIGINS";
    //        Debug(debug);
    //    }

    //    double cadexUStart = cadexLine.UMin();
    //    double gimUStart = gimLine.UMin();

    //    Debug("U-Min: " + cadexUStart + " vs " + gimUStart);

    //    double cadexUEnd = cadexLine.UMax();
    //    double gimUEnd = gimLine.UMax();

    //    Debug("U-Max: " + cadexUEnd + " vs " + gimUEnd);


    //    double cadexLength = cadexLine.UMax() - cadexLine.UMin();
    //    double gimLength = gimLine.UMax() - gimLine.UMin();

    //    Debug("Line Length: " + cadexLength + " vs " + gimLength);
    //    if(!VectorHelpers.CompareDouble(cadexLength, gimLength))
    //    {
    //        string debug = "LINES HAVE DIFFERENT LENGTH";
    //        Debug(debug);
    //    }
    //    else
    //    {
    //        string debug = "Lines have same length, ok";
    //        Debug(debug);
    //    }

    //    Vect cadexStart = cadexOrigin + cadexLine.UMin() * cadexVect;
    //    Vect gimStart = gimOrigin + gimLine.UMin() * gimVect;

    //    Vect cadexEnd = cadexOrigin + cadexLine.UMax() * cadexVect;
    //    Vect gimEnd = gimOrigin + gimLine.UMax() * gimVect;

    //    Debug("Line Start: " + cadexStart + " vs " + gimStart);
    //    if(cadexStart != gimStart)
    //    {
    //        string debug = "LINES HAVE DIFFERENT START POINTS";
    //        Debug(debug);
    //    }
    //    else
    //    {
    //        string debug = "Lines have same start, OK";
    //        Debug(debug);
    //    }

    //    Debug("Line End: " + cadexEnd + " vs " + gimEnd);

    //    if(cadexEnd != gimEnd)
    //    {
    //        string debug = "LINES HAVE DIFFERENT END POINTS";
    //        Debug(debug);
    //    }
    //    else
    //    {
    //        string debug = "Lines have same end, OK";
    //        Debug(debug);
    //    }

    //    EndRegion();
    //    return linesSimilar;
    //}

    //public bool CompareConics(ModelData_Curve cadexConicCurve, ModelData_Curve gimConicCurve)
    //{
    //    StartRegion("Conic Curve");
    //    bool conicsSimilar = true;

    //    Debug("Periodic: " + cadexConicCurve.IsPeriodic() + " vs " + gimConicCurve.IsPeriodic());
    //    if ( !(cadexConicCurve.IsPeriodic() == gimConicCurve.IsPeriodic()))
    //    {
    //        string debug = "ONE OF THE CURVES IS PERIODIC AND THE OTHER IS NOT";
    //        Debug(debug);
    //        DebugMarker();
    //        conicsSimilar = false;
    //    }

    //    var cadexConic = ModelData_Conic.Cast(cadexConicCurve);
    //    var gimConic = ModelData_Conic.Cast(gimConicCurve);

    //    switch (cadexConicCurve.Type())
    //    {
    //        case ModelData_CurveType.ModelData_CT_Circle:
    //            {
    //                StartRegion("Circle");
    //                var cadexCircle = ModelData_Circle.Cast(cadexConicCurve);
    //                var gimCircle = ModelData_Circle.Cast(gimConicCurve);

    //                double radiusDiff = cadexCircle.Radius() - gimCircle.Radius();
    //                Debug("Radius: " + cadexCircle.Radius() + " vs " + gimCircle.Radius());
    //                if (radiusDiff > Vect.EQUALITY_MARGIN)
    //                {
    //                    string debug = "CIRCLES ARE DIFFERENT RADII!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    conicsSimilar = false;
    //                }
    //                EndRegion();
    //            }break;

    //        case ModelData_CurveType.ModelData_CT_Parabola:
    //            {
    //                StartRegion("Parabola");
    //                var cadexParabola = ModelData_Parabola.Cast(cadexConicCurve);
    //                var gimParabola = ModelData_Parabola.Cast(gimConicCurve);

    //                double focalDiff = cadexParabola.Focal() - gimParabola.Focal();
    //                Debug("Focal: " + cadexParabola.Focal() + " vs " + gimParabola.Focal());
    //                if (focalDiff > Vect.EQUALITY_MARGIN)
    //                {
    //                    string debug = "PARABOLA FOCII ARE DIFFERENT!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    conicsSimilar = false;
    //                }
    //                EndRegion();
    //            }break;
    //        case ModelData_CurveType.ModelData_CT_Hyperbola:
    //            {
    //                StartRegion("Hyperbola");
    //                var cadexHyperbola = ModelData_Hyperbola.Cast(cadexConicCurve);
    //                var gimHyperbola = ModelData_Hyperbola.Cast(gimConicCurve);

    //                double majorRadiusDiff = cadexHyperbola.MajorRadius() - gimHyperbola.MajorRadius();
    //                double minorRadiusDiff = cadexHyperbola.MinorRadius() - gimHyperbola.MinorRadius();

    //                Debug("Major Radius: " + cadexHyperbola.MajorRadius() + " vs " + gimHyperbola.MajorRadius());

    //                if (majorRadiusDiff > Vect.EQUALITY_MARGIN)
    //                {
    //                    string debug = "HYPERBOLA MAJOR RADII ARE DIFFERENT!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    conicsSimilar = false;
    //                }

    //                Debug("Minor Radius: " + cadexHyperbola.MinorRadius() + " vs " + gimHyperbola.MinorRadius());

    //                if (minorRadiusDiff > Vect.EQUALITY_MARGIN)
    //                {
    //                    string debug = "HYPERBOLA MINOR RADII ARE DIFFERENT";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    conicsSimilar = false;
    //                }
    //                EndRegion();
    //            }break;
    //        case ModelData_CurveType.ModelData_CT_Ellipse:
    //            {
    //                StartRegion("Ellipse");
    //                var cadexEllipse = ModelData_Ellipse.Cast(cadexConicCurve);
    //                var gimEllipse = ModelData_Ellipse.Cast (gimConicCurve);

    //                double majorRadiusDiff = cadexEllipse.MajorRadius() - gimEllipse.MajorRadius();
    //                double minorRadiusDiff = cadexEllipse.MinorRadius() - gimEllipse.MinorRadius();
    //                Debug("Major Radius: " + cadexEllipse.MajorRadius() + " vs " + gimEllipse.MajorRadius());
    //                if (majorRadiusDiff > Vect.EQUALITY_MARGIN)
    //                {
    //                    string debug = "ELLIPSE MAJOR RADII ARE DIFFERENT!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    conicsSimilar = false;
    //                }
    //                Debug("Minor Radius: " + cadexEllipse.MinorRadius() + " vs " + cadexEllipse.MinorRadius());
    //                if (minorRadiusDiff > Vect.EQUALITY_MARGIN)
    //                {
    //                    string debug = "ELLIPSE MINOR RADII ARE DIFFERENT";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    conicsSimilar = false;
    //                }
    //                EndRegion();
    //            }
    //            break;

    //        default: break;
    //    }

    //    var cadexPosition = cadexConic.Position();
    //    var gimPosition = gimConic.Position();

    //    Vect cadexLocation = new Vect(cadexPosition.Location().X(), cadexPosition.Location().Y(), cadexPosition.Location().Z());
    //    Vect gimLocation = new Vect(gimPosition.Location().X(), gimPosition.Location().Y(), gimPosition.Location().Z());

    //    Debug("Location: " + cadexLocation + " vs " + gimLocation);
    //    if (cadexLocation != gimLocation)
    //    {
    //        string debug = "CONICS HAVE DIFFERENT LOCATION";
    //        Debug(debug);
    //        DebugMarker();
    //        conicsSimilar = false;
    //    }

    //    Vect cadexXDirection = new Vect(cadexPosition.XDirection().X(), cadexPosition.XDirection().Y(), cadexPosition.XDirection().Z());
    //    Vect gimXDirection = new Vect(gimPosition.XDirection().X(), gimPosition.XDirection().Y(), gimPosition.XDirection().Z());


    //    Debug("X Direction: " + cadexXDirection + " vs " + gimXDirection);
    //    if (cadexXDirection != gimXDirection)
    //    {
    //        string debug = "CONICS HAVE DIFFERENT X DIRECTION";
    //        Debug(debug);
    //        if (cadexConic.Type() != ModelData_CurveType.ModelData_CT_Circle)
    //        {
    //            DebugMarker();
    //            conicsSimilar = false;
    //        }
    //    }

    //    Vect cadexYDirection = new Vect(cadexPosition.YDirection().X(), cadexPosition.YDirection().Y(), cadexPosition.YDirection().Z());
    //    Vect gimYDirection = new Vect(gimPosition.YDirection().X(), gimPosition.YDirection().Y(), gimPosition.YDirection().Z());


    //    Debug("Y Direction: " + cadexYDirection + " vs " + gimYDirection);
    //    if (cadexYDirection != gimYDirection)
    //    {
    //        string debug = "CONICS HAVE DIFFERENT Y DIRECTION";
    //        Debug(debug);
    //        if (cadexConic.Type() != ModelData_CurveType.ModelData_CT_Circle)
    //        {
    //            DebugMarker();
    //            conicsSimilar = false;
    //        }
    //    }

    //    EndRegion();

    //    return conicsSimilar;
    //}

    //public bool Compare2DCurves(ModelData_Curve2d cadex2DCurve, ModelData_Curve2d gim2DCurve)
    //{
    //    StartRegion("2D Seam Curve");
    //    bool curvesSimilar = true;

    //    if(cadex2DCurve.Type() != gim2DCurve.Type())
    //    {
    //        string debug = "2D SEAM CURVES HAVE DIFFERENT TYPES!";
    //        Debug(debug);
    //        DebugMarker();
    //        EndRegion();
    //        return false;
    //    }

    //    switch (cadex2DCurve.Type())
    //    {
    //        case ModelData_CurveType.ModelData_CT_Line:
    //            {
    //                StartRegion("2D Line");
    //                var cadex2Dline = ModelData_Line2d.Cast(cadex2DCurve);
    //                var gim2Dline = ModelData_Line2d.Cast(gim2DCurve);

    //                Vect cadexLocation = new Vect(cadex2Dline.Location().X(), cadex2Dline.Location().Y());
    //                Vect gimLocation = new Vect(gim2Dline.Location().X(), gim2Dline.Location().Y());

    //                Debug("Location: " + cadexLocation + " vs " + gimLocation);
    //                if(cadexLocation != gimLocation)
    //                {
    //                    string debug = "2D LINES HAVE DIFFERENT LOCATIONS!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    curvesSimilar = false;
    //                }

    //                Vect cadexDirection = new Vect(cadex2Dline.Direction().X(), cadex2Dline.Direction().Y());
    //                Vect gimDirection = new Vect(gim2Dline.Direction().X(), gim2Dline.Direction().Y());

    //                Debug("Direction: " + cadexDirection + " vs " + gimDirection);
    //                if(cadexDirection != gimDirection)
    //                {
    //                    string debug = "2D LINES HAVE DIFFERENT DIRECTIONS!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    curvesSimilar = false;
    //                }
    //                EndRegion();
    //            }
    //            break;
    //        case ModelData_CurveType.ModelData_CT_BSpline:
    //            {
    //                StartRegion("2D Bspline");
    //                var cadex2DBspline = ModelData_BSplineCurve2d.Cast(cadex2DCurve);
    //                var gim2DBspline = ModelData_BSplineCurve2d.Cast(gim2DCurve);

    //                var cadexDegree = cadex2DBspline.Degree();
    //                var gimDegree = gim2DBspline.Degree();

    //                if(cadexDegree != gimDegree)
    //                {
    //                    string debug = "2D BSPLINES HAVE DIFFERENT DEGREES";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    curvesSimilar = false;
    //                }

    //                var cadexKnots = cadex2DBspline.Knots().ToArray();
    //                var gimKnots = gim2DBspline.Knots().ToArray();

    //                if(cadexKnots.Length != gimKnots.Length)
    //                {
    //                    string debug = "2D BSPLINE KNOT VECTORS HAVE DIFFERENT LENGTH!";
    //                    Debug(debug);
    //                    DebugMarker();
    //                    curvesSimilar = false;
    //                }

    //                for(int i=0; i<Math.Min(cadexKnots.Length, gimKnots.Length); i++)
    //                {
    //                    double cadexKnot = cadexKnots[i];
    //                    double gimKnot = gimKnots[i];

    //                    Debug("Knot " + i + ": " + cadexKnot + " vs " + gimKnot);
    //                    if(!VectorHelpers.CompareDouble(cadexKnot, gimKnot))
    //                    {
    //                        string debug = "2D BSPLINE KNOTS " + i + " ARE DIFFERENT!";
    //                        Debug(debug);
    //                        DebugMarker();
    //                        curvesSimilar = false;
    //                    }
    //                }
    //                EndRegion();

    //            }break;
    //    }
    //    EndRegion();
    //    return curvesSimilar;
    //}

    //}


}
