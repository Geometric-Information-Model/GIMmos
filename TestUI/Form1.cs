using GIMcompress;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text.Json;
using GIMcompress.Shrinkwrap;
using cadex;
using GIMtransfer;
using System.Threading.Tasks;
using GimBin;
using GimUtilities;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Geometry;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;
using GIMcompress.Shrinkwrap;
using GIMocctWrapper;
using GIMocctCLI;
using GIMtransfer.read.IFCReader;
using GIMtransfer.write;
using GIMtransfer.read;
using GIMtransfer.KlicImkl;
using static System.Net.Mime.MediaTypeNames;

namespace TestUI
{
    public partial class Form1 : Form
    {
        GimData gimData_currentSelected;

        private delegate void SafeProgressBar1Delegate(int progress);
        private delegate void SafeProgressBar2Delegate(int progress);

        public Form1()
        {
            InitializeComponent();

            for (int i = 0; i < 128; i++)
            {
                GimUnitSize unitSize = new GimUnitSize((byte)i);
                this.binaryUnitDropDown.Items.Add(unitSize.ToString());
            }

            binaryUnitDropDown.SelectedIndex = 0;

            for (int i = 0; i < 128; i++)
            {
                GimUnitSize unitSize = new GimUnitSize((byte)i);
                this.visualisationUnitDropDown.Items.Add(unitSize.ToString());
            }

            visualisationUnitDropDown.SelectedIndex = 0;



            Run.Enabled = false;
            FileToJson.Enabled = false;
            JsonToJson.Enabled = false;
            JsonToFile.Enabled = false;
            //GetGLTF.Enabled = false;
            ReadGimData.Enabled = false;
            //FrontendReadWrite.Enabled = false;
            WriteElemCounts.Enabled = false;
            OutputSelect.Text = ".stp";

            // rounding bar

            RoundPrecisionBar.Value = 5;

            //TestBinary.Enabled = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void FileSelect_Click(object sender, EventArgs e)
        {
            if (FileSelector.ShowDialog() == DialogResult.OK)             // open file dialog
            {
                FileName.Text = FileSelector.FileName;
                ReadGimData.Enabled = true;

                FileToJson.Enabled = true;
                JsonToJson.Enabled = true;
                JsonToFile.Enabled = true;
                //FrontendReadWrite.Enabled = true;
                if (CadexOnly.Checked)
                    Run.Enabled = true;

            }
        }

        private void FileSelector_HelpRequest(object sender, EventArgs e)
        {
        }

        private void FileSelector_FileOk(object sender, CancelEventArgs e)
        {
        }

        private void SetRunButtons(bool value)
        {
            Run.Enabled = value;
            FileSelect.Enabled = value;
            CompressButton.Enabled = value;
            FileToJson.Enabled = value;
            JsonToJson.Enabled = value;
            JsonToFile.Enabled = value;
        }

        private GimOptions GetGimOptions()
        {
            return new GimOptions()
            {
                binaryUnitToBe = binaryUnitDropDown.SelectedIndex,
                visualisationUnitToBe = visualisationUnitDropDown.SelectedIndex,
                scale = (double)ScaleInput.Value,
                isIFCPreview = IfcPreviewCheckBox.Checked,
            };
        }
        private void FileToSvg_Click(object sender, EventArgs e)
        {
            SetRunButtons(false);
            string pathFileName = FileSelector.FileName;
            GimData gimData = GIMtransfer.FileAccess.ReadToGimData(pathFileName, Path.GetExtension(pathFileName)[1..], true);
            GimProgressStatus readGimProgress = new(), writeGimProgress = new();
            Task<string> task = GIMtransfer.FileAccess.WriteToSvgFileAsync(gimData, pathFileName, writeGimProgress);
            _ = MonitorFileProgressTask(task, readGimProgress, writeGimProgress, pathFileName);
        }
        private void FileToJson_Click(object sender, EventArgs e)
        {
            SetRunButtons(false);

            // Read to gimdata
            string filetype = Path.GetExtension(FileSelector.FileName);
            if (filetype != ".bin" || filetype != ".gimzip")
            {
                // deprecated, for cadex
                //if (GIMtransfer.FileAccess.options.TryGetValue(filetype[1..], out List<string> fileTypeOut))
                //{
                //    using FileSelector fileDialog = new FileSelector(fileTypeOut);

                //    string tempPathFileName = FileSelector.FileName;
                //    string firstLine;
                //    using (StreamReader reader = new StreamReader(tempPathFileName))
                //    {
                //        firstLine = reader.ReadLine() ?? "";
                //    }
                //    if (firstLine.ElementAt<char>(0) == '#')
                //    {
                //        filetype += ".CREO";
                //    }
                //    else
                //    {
                //        filetype += ".NX";
                //    }
                //}

                string pathFileName = FileSelector.FileName;
                GimProgressStatus readGimProgress = new(), writeGimProgress = new();
                GimOptions gimOptions = GetGimOptions();

                Task<string> task = GIMtransfer.FileAccess.FileToJsonAsync(pathFileName, filetype[1..],
                    pathFileName, readGimProgress, writeGimProgress, gimOptions);

                _ = MonitorFileProgressTask(task, readGimProgress, writeGimProgress, pathFileName);

            }
            else if (filetype == ".gimzip")
            {
                GimLogger.Warn(".gimzip has not been implemented yet. Use .bin instead.");
                SetRunButtons(true);
            }
            else
            {
                // Is bin or GimZip file.
                GimProgressStatus readGimProgress = new GimProgressStatus();
                readGimProgress.Create(new Dictionary<string, int>() { { "ReadGIMbin", 100 } });
                gimData_currentSelected = GIMtransfer.FileAccess.BinaryToGimData(FileSelector.FileName, readGimProgress);
                SetRunButtons(true);
            }

        }

        private void JsonToJson_Click(object sender, EventArgs e)
        {
            SetRunButtons(false);
            string filetype = Path.GetExtension(FileSelector.FileName);

            string pathFileName = FileSelector.FileName;
            GimProgressStatus readGimProgress = new(), writeGimProgress = new();
            GimOptions gimOptions = GetGimOptions();
            string outputName = Path.GetFileNameWithoutExtension(FileSelector.SafeFileName);

            string jsonContent = File.ReadAllText(pathFileName);

            Task task = Task.Run(() =>
                GIMtransfer.FileAccess.JsonToJson(new string[] { jsonContent }, outputName,
                    readGimProgress, writeGimProgress, gimOptions)
            );
        
            _ = MonitorFileProgressTask(task, readGimProgress, writeGimProgress, pathFileName);
        }

        private void JsonToFile_Click(object sender, EventArgs e)

        {
            SetRunButtons(false);

            string pathFileName = FileSelector.FileName;
            GimProgressStatus readGimProgress = new(), writeGimProgress = new();
            GimOptions gimOptions = GetGimOptions();
            string text = File.ReadAllText(pathFileName);
            string outputName = Path.GetFileNameWithoutExtension(FileSelector.SafeFileName);

            Task<string> task = GIMtransfer.FileAccess.JsonToFileAsync(text, OutputSelect.Text[1..], outputName,
                readGimProgress, writeGimProgress, gimOptions);

            _ = MonitorFileProgressTask(task, readGimProgress, writeGimProgress, pathFileName);

        }

        private async Task MonitorFileProgressTask(Task task, GimProgressStatus readGimProgress,
            GimProgressStatus writeGimProgress, string pathFileName)
        {
            ReadProgressBar.Maximum = 100;
            WriteProgressBar.Maximum = 100;
            await Task.Run(async () =>
            {
                while (!task.IsCompleted)
                {
                    UpdateReadProgressBar((int)readGimProgress.PercentageDone);
                    UpdateWriteProgressBar((int)writeGimProgress.PercentageDone);
                    await Task.Delay(100);
                }

                UpdateReadProgressBar(100);
                UpdateWriteProgressBar(100);

                string message = $"File {pathFileName} " + 
                (task.IsCompletedSuccessfully? "successfully converted to JSON" : 
                $"failed to convert to json, exception: { task.Exception}");

                // Shows A success/error message.
                Invoke(() =>
                {
                    MessageBox.Show(this, message);
                    // resetting progress to zero.
                    UpdateReadProgressBar(0);
                    UpdateWriteProgressBar(0);
                });


                // Resets buttons to enabled.
                Invoke(() => SetRunButtons(true));
            });
        }

        private void Run_Click(object sender, EventArgs e)
        {
            string filetype = Path.GetExtension(FileSelector.FileName);

            if (CompressButton.Checked) // round problematic values
            {
                int digitsToRound = RoundPrecisionBar.Value;
                gimData_currentSelected = ValueRounder.RoundValues(gimData_currentSelected, (short)digitsToRound);
            }

            Run.Enabled = false;
            FileSelect.Enabled = false;
            CompressButton.Enabled = false;
            // test values
            string pathFileName = FileSelector.FileName;
            string filename;
            if (CadexOnly.Checked)
            {
                //filename = "GIM_CADEXONLY_" + FileSelector.SafeFileName.Remove(FileSelector.SafeFileName.Length - Path.GetExtension(FileSelector.FileName).Length);
                //GimLogger.Info(GIMtransfer.FileAccess.ConvertFile(pathFileName, filetype[1..], filename, OutputSelect.Text[1..]));
            }
            else
            {
                WriteTypeCounts();

                filename = "GIM_";
                if (CompressButton.Checked) filename += "COMPRESSED_";
                filename += FileSelector.SafeFileName.Remove(FileSelector.SafeFileName.Length - Path.GetExtension(FileSelector.FileName).Length);
                var convertFormat = OutputSelect.Text[1..];
                if (convertFormat == "glbt")
                {
                    var glbString = GIMtransfer.FileAccess.WriteFromGimData(gimData_currentSelected, OutputSelect.Text[1..], filename, CompressButton.Checked);
                    ModelRoot glb = ModelRoot.ParseGLB(Convert.FromBase64String(glbString));
                    glb.Save(GIMtransfer.FileAccess.GetTempPath("ConvertedGLB.glb"));
                }
                else 
                {
                    GIMtransfer.FileAccess.WriteFromGimData(gimData_currentSelected, OutputSelect.Text[1..], filename, CompressButton.Checked);

                }
            }
            Run.Enabled = true;
            FileSelect.Enabled = true;
            CompressButton.Enabled = true;


        }

        private void WriteTypeCounts()
        {
            Dictionary<GimElementType, int> types = new Dictionary<GimElementType, int>();
            foreach (GimElement element in gimData_currentSelected.Elements)
            {
                if (types.ContainsKey(element.elementType))
                    types[element.elementType]++;
                else types.Add(element.elementType, 1);
            }

            // Print the amount of each type.
            List<KeyValuePair<GimElementType, int>> myList = types.ToList();
            myList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
            foreach (KeyValuePair<GimElementType, int> pair in myList)
                GimLogger.Info(pair.Key + ": " + pair.Value);
            GimLogger.Info("Object Count: " + gimData_currentSelected.Objects.Count);
        }

        private void FileName_TextChanged(object sender, EventArgs e)
        {

        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private void Compress_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void CadexOnly_CheckedChanged(object sender, EventArgs e)
        {
            if (FileSelector.FileName != "SelectedFile" && gimData_currentSelected == null)
                Run.Enabled = CadexOnly.Checked;

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void BinaryNum_TextChanged(object sender, EventArgs e)
        {
            //binaryNum = true;
            //if (binaryNum && folderSelected)
            //TestBinary.Enabled = true;
        }

        private void FolderSelectorButton_Click(object sender, EventArgs e)
        {
            if (FolderSelector.ShowDialog() == DialogResult.OK)             // open file dialog
            {
                GimLogger.Info(FolderSelector.SelectedPath);
            }
        }
        private void RoundValues_Click(object sender, EventArgs e)
        {
            // the rounding precision, n => round off nth digit.
            int digitsToRound = RoundPrecisionBar.Value;

            gimData_currentSelected = ValueRounder.RoundValues(gimData_currentSelected, (short)digitsToRound);
        }

        private void changeRoundPrecision(object sender, EventArgs e)
        {
            RoundValLabel.Text = "Rounding Precision: " + RoundPrecisionBar.Value;
        }

        private void roundLabelClick(object sender, EventArgs e)
        {

        }

        private void TestButton_Click(object sender, EventArgs e)
        {

        }

        private void OpenConsole_Click(object sender, EventArgs e)
        {
            AllocConsole();
        }

        private void GetGLTF_Click(object sender, EventArgs e)
        {
            ModelRoot gltf = GIMtransfer.FileAccess.GetGLTF(gimData_currentSelected, gimData_currentSelected.Objects[(int)ObjectID.Value].element);
            gltf.Save(GIMtransfer.FileAccess.GetTempPath("GLTFTest.glb"));
        }

        private void ReadGimData_Click(object sender, EventArgs e)
        {
            Run.Enabled = false;
            FileSelect.Enabled = false;
            CompressButton.Enabled = false;
            // Read to gimdata
            if (Path.GetExtension(FileSelector.FileName) != ".bin" && Path.GetExtension(FileSelector.FileName) != ".gimzip")
            {
                // Read normal cad file
                string filetype = Path.GetExtension(FileSelector.FileName);
                if (GIMtransfer.FileAccess.options.ContainsKey(filetype[1..]))
                {
                    using (FileSelector fileDialog = new FileSelector(GIMtransfer.FileAccess.options[filetype[1..]]))
                    {
                        string tempPathFileName = FileSelector.FileName;
                        string firstLine;
                        using (StreamReader reader = new StreamReader(tempPathFileName))
                        {
                            firstLine = reader.ReadLine() ?? "";
                        }
                        if (firstLine.ElementAt<char>(0) == '#')
                        {
                            filetype += ".CREO";
                        }
                        else 
                        {
                            filetype += ".NX";
                        }

                        //DialogResult result = fileDialog.ShowDialog();
                        //if (result == DialogResult.OK)
                        //{
                        //    filetype += "." + fileDialog.fileType;
                        //}
                    }
                }
                string pathFileName = FileSelector.FileName;

                GIMtransfer.FileAccess.Init();
                if (filetype != ".json")
                {
                    //ModelData_Model aModel = CadexModel.Read(pathFileName, filetype[1..]); // Read the file based on the fileType
                    
                    gimData_currentSelected = GIMtransfer.FileAccess.ReadToGimData(pathFileName, filetype[1..], IfcPreviewCheckBox.Checked);
                }
                else
                {


                    gimData_currentSelected = GIMtransfer.FileAccess.ReadToGimData(pathFileName, filetype[1..], IfcPreviewCheckBox.Checked);
                }


                //gimData_currentSelected = GIMtransfer.FileAccess.ReadToGimData(pathFileName, filetype[1..], true);

                Run.Enabled = true;
            }
            else if (Path.GetExtension(FileSelector.FileName) == ".gimzip")
            {
                GimLogger.Warn(".gimzip has not been implemented yet. Use .bin instead.");
            }
            else
            {
                // Is bin or GimZip file.
                GimProgressStatus readGimProgress = new GimProgressStatus();
                readGimProgress.Create(new Dictionary<string, int>() { { "ReadGIMbin", 100 } });
                gimData_currentSelected = GIMtransfer.FileAccess.BinaryToGimData(FileSelector.FileName, readGimProgress);
            }
            Run.Enabled = true;
            RoundValues.Enabled = true;
            FileSelect.Enabled = true;
            CompressButton.Enabled = true;
            WriteElemCounts.Enabled = true;
        }


        private void profileButton_Click(object sender, EventArgs e)
        {
            var shellElements = ProfileDetector.GetShellElementsInList(gimData_currentSelected, 0);
            int shellIndex = 0;
            while (shellElements.Count > 0)
            {
                List<Tuple<Vect, List<GimElement>>> tuples =
                    ProfileDetector.DetectParallels(gimData_currentSelected, shellElements);


                List<Tuple<Vect, List<PolygonRepresentation>>> polygons =
                    ProfileDetector.ConstructPolygons(gimData_currentSelected, tuples);

                List<Tuple<Vect, List<PolygonRepresentation>>> polygonTuples =
                    ProfileDetector.LinkPolygons(polygons);

                List<PolygonRepresentation> profileCandidates = ProfileDetector.FilterProfileCandidates(polygonTuples);

                List<PolygonRepresentation> profileCandidatesEliminated = ProfileDetector.EliminateConflictingProfiles(profileCandidates);

                profileCandidatesEliminated = ProfileDetector.EliminateProfilesWithUnmatchingFaces(profileCandidatesEliminated);

                if (profileCandidates.Count > 0)
                {
                    var profileTuples = ProfileDetector.ConvertCandidatesIntoProfiles(profileCandidatesEliminated, gimData_currentSelected);

                    ProfileDetector.RemoveOriginalProfilePieces(profileTuples, gimData_currentSelected);

                    var profiles = ProfileDetector.FindOriginalProfileFaces(profileTuples, gimData_currentSelected, shellIndex);

                    ProfileDetector.SimplifyProfileFaces(profiles, gimData_currentSelected);

                    ProfileDetector.OmitPerpendicularFaces(profiles, gimData_currentSelected);
                }



                shellIndex++;

                // getting the next shell
                shellElements = ProfileDetector.GetShellElementsInList(gimData_currentSelected, shellIndex);
            }

            gimData_currentSelected = ValueRounder.RoundValues(gimData_currentSelected);

            //int profileIndex = 0;
            //for (int i=0; i < gimData_currentSelected.Elements.Count; i++)
            //         {
            //	GimElement element = gimData_currentSelected.Elements[i];
            //	if(profileIndex == 29)
            //             {
            //		string x = "DEBUG"; // TODO REMOVE
            //             }
            //	if(element.ElementType == GimElementType.Gim_ProfileInstance)
            //             {
            //		ProfileDecompressor.BuildProfileWalls((uint)i, gimData_currentSelected);
            //		ProfileDecompressor.RemoveProfilePieces((uint)i, gimData_currentSelected);
            //		i--;
            //		profileIndex++;
            //             }
            //         }

        }

        private void RoundValues_Click_1(object sender, EventArgs e)
        {
            // the rounding precision, n => round off nth digit.
            int digitsToRound = RoundPrecisionBar.Value;


            double t = 0.001;
            PolygonRounder.precision = t;

            GimData newData = new();
            newData.CreateRootIfNotExists();
            newData.Elements.Add(new() { ElementType = GimElementType.D_folder });
            var model = GIMtransfer.FileAccess.GetGLTF(newData, 0);
            var glb = Convert.ToBase64String(model.WriteGLB().Array);

            gimData_currentSelected = ValueRounder.RoundValues(gimData_currentSelected, (short)digitsToRound);

            return;

            for (int i = 0; i < gimData_currentSelected.Elements.Count; i++)
            {
                GimElement element = gimData_currentSelected.Elements[i];
                if (element.ElementType == GimElementType.P_polygon)
                {

                    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();
                    TriangleMesh mesh = PolygonRounder.ConstructTriangles(element);
                    stopwatch.Stop();
                    var constructionTime = stopwatch.ElapsedMilliseconds;

                    stopwatch.Restart();
                    PolygonRounder.RoundPolygons(mesh, t);
                    stopwatch.Stop();
                    var roundingTime = stopwatch.ElapsedMilliseconds;

                    stopwatch.Restart();
                    PolygonRounder.RemoveVertexTriangles(mesh);
                    stopwatch.Stop();
                    var removingVerticiesTime = stopwatch.ElapsedMilliseconds;

                    //stopwatch.Restart();
                    //               PolygonRounder.FixDegenerate(mesh);
                    //stopwatch.Stop();
                    //var removingDegenerateTime = stopwatch.ElapsedMilliseconds;

                    //stopwatch.Restart();
                    //               PolygonRounder.FixFlattened(mesh);
                    //stopwatch.Stop();
                    //var removingFlattenedTime = stopwatch.ElapsedMilliseconds;

                    stopwatch.Restart();
                    PolygonRounder.RemoveFlattenedAndDegenerate(mesh);
                    stopwatch.Stop();
                    var removingUnnecessaryTriangles = stopwatch.ElapsedMilliseconds;
                    //PolygonRounder.GroupPlanarTriangles(mesh);

                    //string freeCad = "import Part\nimport Mesh\ntriangles=[\n";
                    //foreach (Triangle triangle in mesh.Triangles)
                    //               {
                    //	freeCad += triangle.GetFreeCADMeshString() + "\n";
                    //               }

                    //freeCad += "]\nmeshObject = Mesh.Mesh(triangles)\nMesh.show(meshObject)";

                    stopwatch.Restart();
                    mesh.ApplyToGimDataPolygon(element);
                    stopwatch.Stop();

                    var applyingToGimDataTime = stopwatch.ElapsedMilliseconds;

                    //PolygonRounder.ConstructNeighbourhoods(mesh);
                    //var triangleGroups = PolygonRounder.GroupPlanarTriangles(mesh);
                    //  PolygonRounder.ReplaceFlatGroupsWithGimPoly(triangleGroups);



                }


            }



            //    int maxChildren = 1, maxChildrenIndex = 0;
            //    locating the element with the most children, for testing purposes

            //    for (int i = 0; i < gimData_currentSelected.Elements.Count; i++)
            //        {
            //            GimElement element = gimData_currentSelected.Elements[i];
            //            if (element.Elements.Count > maxChildren)

            //                maxChildren = element.Elements.Count;
            //            maxChildrenIndex = i;
            //        }
            //}

            //GimElement mostChildrenElement = gimData_currentSelected.Elements[maxChildrenIndex];

            //var ChildrenAndLocals = mostChildrenElement.GetAllChildrenOrdered(maxChildrenIndex, gimData_currentSelected);





        }

        private void ObjectIDLabel_Click(object sender, EventArgs e)
        {

        }

        private void ObjectID_ValueChanged(object sender, EventArgs e)
        {

        }

        private void Joints_Click(object sender, EventArgs e)
        {
            string filetype = Path.GetExtension(FileSelector.FileName);

           

            Run.Enabled = false;
            FileSelect.Enabled = false;
            CompressButton.Enabled = false;
            // test values
            string pathFileName = FileSelector.FileName;
            string filename;

            string jsonContent = File.ReadAllText(pathFileName);
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            JsonGimObject mainJsonObj = JsonManager.ReadGimJson(jsonContent);

            var model = ModelRoot.CreateModel();
            var rootScene = model.UseScene("default");
            SceneBuilder scenebuilder = new SceneBuilder();
            

            for (int i = 0; i < mainJsonObj.GLBs.Length; i++) 
            {
                byte[] glbz = Convert.FromBase64String(mainJsonObj.GLBs[i]);

                ModelRoot rootObjectf = ModelRoot.ParseGLB(glbz);

                if(i==2 || i==4 || i == 6 || i == 8 || i == 10 || i == 11 || i == 3 || i == 9 || i == 7) 
                {
                    rootObjectf.LogicalNodes[0].WithRotationAnimation("Track0", (0, Quaternion.Identity), (1, Quaternion.CreateFromYawPitchRoll(3.4f, 0f, 0f)), (2, Quaternion.Identity));
                }

                scenebuilder.AddScene(SceneBuilder.CreateFrom(rootObjectf.DefaultScene), Matrix4x4.Identity);

            }

            scenebuilder.ToGltf2().SaveGLB(@"E:\\GIMFiles\\file.glb");


            Dictionary<ModelRoot, int> roots = new Dictionary<ModelRoot, int>(); // Populate with your ModelRoot objects

            for (int i = 0; i < mainJsonObj.GLBs.Length; i++) 
            {
                byte[] glb = Convert.FromBase64String(mainJsonObj.GLBs[i]);

                ModelRoot rootObject = ModelRoot.ParseGLB(glb);

                roots.Add(rootObject, i);
            
            }

            List<JsonGimJoint> joints =mainJsonObj.Joints; // Populate with your GIMJoints objects
            


            var model2 = CreateTentacle1s(roots, joints);

            model.SaveGLB("tentacles.glb");
            model.SaveGLTF("tentacles.gltf");
        }

        private static ModelRoot CreateTentacle1s(Dictionary<ModelRoot, int> roots, List<JsonGimJoint> joints)
        {
            SceneBuilder scenebuilder = new SceneBuilder();

            var model = ModelRoot.CreateModel();
            var rootScene = model.UseScene("default");

            foreach (JsonGimJoint joint in joints) 
            {
                var parent = roots;
            }

            foreach (var root in roots.Keys)
            {
                var mesh = root.LogicalMeshes.FirstOrDefault(); // Assuming only one mesh per ModelRoot

                var skeleton = rootScene.CreateNode();
                var bindings = new List<Node>();

                foreach (var joint in joints)
                {
                    var parent = GetModelRootById(roots, joint.Parent);
                    var child = GetModelRootById(roots, joint.Child);

                    if (parent != null && child != null)
                    {
                        var parentOrigin = GetOriginMatrix(parent);
                        var childOrigin = GetOriginMatrix(child);

                        var offset = CalculateJointOffset(parentOrigin, childOrigin);

                        var jointNode = AddTentacleSkeleton(rootScene, skeleton, mesh, offset, Quaternion.Identity);

                        bindings.Add(jointNode);
                    }
                }

                rootScene
                    .CreateNode()
                    .WithSkinnedMesh(mesh, skeleton.WorldMatrix, bindings.ToArray());
            }

            return model;
        }

        private static Matrix4x4 GetOriginMatrix(ModelRoot modelRoot)
        {
            var origin = modelRoot.LogicalNodes.FirstOrDefault()?.LocalTransform ?? Matrix4x4.Identity;

            var originPosition = origin.Translation;
            var originRotation = Quaternion.CreateFromRotationMatrix(origin.Matrix);

            // Convert to ROS convention: RPY (roll, pitch, yaw)
            var originEulerAngles = GetEulerAnglees(originRotation);

            return Matrix4x4.CreateTranslation(originPosition)
                 * Matrix4x4.CreateFromYawPitchRoll(originEulerAngles.Z, originEulerAngles.Y, originEulerAngles.X);
        }
        private static Vector3 GetEulerAnglees(Quaternion quaternion)
        {
            var sinr_cosp = 2 * (quaternion.W * quaternion.X + quaternion.Y * quaternion.Z);
            var cosr_cosp = 1 - 2 * (quaternion.X * quaternion.X + quaternion.Y * quaternion.Y);
            var roll = Math.Atan2(sinr_cosp, cosr_cosp);

            var sinp = 2 * (quaternion.W * quaternion.Y - quaternion.Z * quaternion.X);
            var pitch = Math.Abs(sinp) >= 1 ? Math.Sign(sinp) * Math.PI / 2 : Math.Asin(sinp);

            var siny_cosp = 2 * (quaternion.W * quaternion.Z + quaternion.X * quaternion.Y);
            var cosy_cosp = 1 - 2 * (quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z);
            var yaw = Math.Atan2(siny_cosp, cosy_cosp);

            return new Vector3((float)pitch, (float)yaw, (float)roll);
        }

       
        private static Matrix4x4 CalculateJointOffset(Matrix4x4 parentOrigin, Matrix4x4 childOrigin)
        {
            // Calculate the 4x4 transformation matrix between parent and child origins
            Matrix4x4.Invert(parentOrigin, out Matrix4x4 invertedParent);
            return invertedParent * childOrigin;
        }
        private static Node AddTentacleSkeleton(Scene scene, Node skeleton, Mesh mesh, Matrix4x4 offset, Quaternion anim)
        {
            var bindings = new List<Node>();

            Node bone = null;

            for (int i = 0; i < 10; ++i)
            {
                if (bone == null)
                {
                    bone = skeleton;
                }
                else
                {
                    bone = bone.CreateNode();
                    bone.LocalTransform = Matrix4x4.CreateTranslation(0, 10, 0);
                }

                bone.WithRotationAnimation("Track0", (0, Quaternion.Identity), (1, anim), (2, Quaternion.Identity));

                bindings.Add(bone);
            }

            scene
                .CreateNode()
                .WithSkinnedMesh(mesh, skeleton.WorldMatrix, bindings.ToArray());

            return bindings.Last();


        }

        private static ModelRoot GetModelRootById(Dictionary<ModelRoot, int> roots, int id)
        {
            foreach (var kvp in roots)
            {
                if (kvp.Value == id)
                {
                    return kvp.Key;
                }
            }

            return null; // If no matching ModelRoot object is found, return null
        }

        // Other helper methods
        // Helper method to calculate Euler angles from a Quaternion

        // Helper method to find a GIMJoints object by child ID
        private static GimJoint FindJointByChildId(List<GimJoint> joints, int childId)
        {
            return joints.FirstOrDefault(j => j.Child == childId);
        }

        // Helper method to create the tentacles
        private static void CreateTentacles(List<ModelRoot> roots, List<GimJoint> joints, Scene scene, Node skeleton, Mesh mesh, Matrix4x4 parentOffset, int parentId, int repeat)
        {
            var parentJoint = FindJointByChildId(joints, parentId);

            if (parentJoint == null)
                return;

            var parentOrigin = GetOriginMatrix(roots[parentJoint.Parent]);
            var childOrigin = GetOriginMatrix(roots[parentJoint.Child]);

            var jointOffset = CalculateJointOffset(parentOrigin, childOrigin);

            var jointNode = AddTentacleSkeleton(scene, skeleton, mesh, parentOffset * jointOffset, Quaternion.Identity);

            if (repeat > 0)
            {
                foreach (var childJoint in joints.Where(j => j.Parent == parentJoint.Child))
                {
                    CreateTentacles(roots, joints, scene, jointNode, mesh, parentOffset * jointOffset, childJoint.Child, repeat - 1);
                }
            }
        }

        // Main method to create the tentacles
        private static ModelRoot CreateTentacles(List<ModelRoot> roots, List<GimJoint> joints)
        {
            var model = ModelRoot.CreateModel();
            var scene = model.UseScene("default");

            var mesh = roots.SelectMany(root => root.CreateMeshes()).FirstOrDefault();

            if (mesh == null)
                return model;

            var skeleton = scene.CreateNode();
            var bindings = new List<Node>();

            foreach (var root in roots)
            {
                foreach (var joint in joints.Where(j => j.Parent == -1))
                {
                    CreateTentacles(roots, joints, scene, skeleton, mesh, Matrix4x4.Identity, joint.Child, 2);
                }
            }

            scene.CreateNode().WithSkinnedMesh(mesh, skeleton.WorldMatrix, bindings.ToArray());

            return model;
        }






        public static List<string> errorsEncountered = new List<string>();
        public static string message;
        private void batchClick(object sender, EventArgs e)
        {
            if (!quickTestCheckbox.Checked)
            {
                Form2 form2 = new Form2();

                form2.Show();
            }
            else
            {
                string prefix = null;
                if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                    prefix = folderBrowserDialog1.SelectedPath;
                }
                else if (folderBrowserDialog1.ShowDialog() == DialogResult.Cancel)
                {
                    return;
                }

                int x = 0;

                string[] acceptedFormats = new string[] { "3dxml", "3mf", "3ds", "sat", "sab", "brep", "catpart",
                    "catproduct", "dae", "prt.creo", "asm.creo", "dxf", "dwg", "fbx", "gltf", "glb", "ifc", "igs",
                    "iges", "jt", "prt.NX", "obj", "x_t", "x_b", "xmt_txt", "xmt_bin", "xmb_txt", "xmb_bin", "3dm",
                    "par", "psm", "asm.SLDE", "sldprt", "sldasm", "ply", "prc", "step", "stp", "stl", "u3d", "wrl",
                    "x3d" , "json" };
                List<string> files = GIMtransfer.FileAccess.GetReadableFilesInDirectory(prefix, acceptedFormats);



                string[] exportFormats = new string[] {"bin","sat", "brep", "dae", "dxf", "fbx", "gltf", "ifc",
                "iges",            "jt",
                "obj",         "x_t",
                "3dm",         "stp",
                "stl",         "usd",
                "wrl",         "x3d",
                "json"};


                int fileCount = files.Count;
                int typeCount = exportFormats.Length;


                Random random = new Random();

                for (int i = 0; i < fileCount; i++)
                {

                    string currentFile = files[i];

                    string exportedNameWithoutExtension = Path.GetFileNameWithoutExtension(currentFile);

                    string extension = Path.GetExtension(currentFile);
                    GimData gimData = null;
                    
                    try
                    {
                        gimData = GIMtransfer.FileAccess.ReadToGimData(currentFile, extension[1..]);
                    }
                    catch (Exception ex)
                    {
                        string errorMessage = "File " + exportedNameWithoutExtension + extension + " could not be read";
                        errorMessage += ", Encountered Exception \n" + ex.StackTrace;
                        errorsEncountered.Add(errorMessage);
                        continue;
                    }


                    for (int j = 0; j < typeCount; j++)
                    {
                        string exportfileType = exportFormats[j];


                        string exportedName = exportedNameWithoutExtension;
                        //exportedName = GIMtransfer.FileAccess.GetTempPath(exportedName);

                        if (exportfileType == "bin" || exportfileType == "json")
                        {
                            try
                            {
                                GIMtransfer.FileAccess.WriteFromGimData(gimData, exportfileType, exportedName);
                            }
                            catch (Exception ex)
                            {
                                string errorMessage = "File " + exportedNameWithoutExtension + extension +
                                    " could not be exported to format " + exportfileType;
                                errorMessage += ", Encountered Exception \n" + ex.StackTrace;
                                errorsEncountered.Add(errorMessage);
                                continue;
                            }
                        }
                        else
                        {
                            try
                            {
                                GIMtransfer.FileAccess.WriteFromGimData(gimData, exportfileType, exportedName);
                            }
                            catch (Exception ex)
                            {
                                string errorMessage = "File " + exportedNameWithoutExtension + extension +
                                    " could not be exported to format " + exportfileType;
                                errorMessage += ", Encountered Exception \n" + ex.StackTrace;
                                errorsEncountered.Add(errorMessage);
                                continue;
                            }
                        }

                    }

                }

                message = errorsEncountered.Count + " out of " + (fileCount * typeCount) + " Failed";


                BatchProcessingMessageBox form = new BatchProcessingMessageBox();


                form.ShowDialog();

                errorsEncountered.Clear();

                fileCount = 0;
            }



        }

        private void quickTestCheckbox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void WriteElemCounts_Click(object sender, EventArgs e)
        {
            gimData_currentSelected.WriteElementTypeCount();
        }

        public GIMocctElement test()
        {
            return new(null);
        }

        // Use this button to test out any feature you have developed on your own branch. After being done, comment out
        // your code or delete it, to allow other people to test their features as well
        private void TestFeature_Click(object sender, EventArgs e)
        {
            //string jsonContent = File.ReadAllText(FileSelector.FileName);
            //GimUnitSize unitSize = new((byte)visualisationUnitDropDown.SelectedIndex);
            //var glbreturn = GIMtransfer.FileAccess.GetObjectVisualisationFromJson(jsonContent, unitSize);
            //return;
            //GimProgressStatus stat = new GimProgressStatus();
            //GIMtransfer.GIMocctReadElements t1 = new(stat);
            //GimProgressStatus stat2 = new GimProgressStatus();
            //GIMtransfer.GIMocctReadElements t2 = new(stat2);
            //GimProgressStatus stat3 = new GimProgressStatus();
            //GimProgressStatus stat4 = new GimProgressStatus();
            //GimProgressStatus stat5 = new GimProgressStatus();
            //GimProgressStatus stat6 = new GimProgressStatus();
            //GIMtransfer.GIMocctWriteElements occt = new(stat3);
            //GIMtransfer.GIMocctWriteElements occt2 = new(stat4);
            ////AssimpWriteElements assimp = new(stat6);
            //GIMtransfer.GIMocctWriteElements occt4 = new(stat6);
            //GIMtransfer.GIMocctReadElements occt3 = new(stat5);
            //string path1 = "replace";
            //GimData gimD1 = t1.ReadToGimData(path1, "step");
            //string path2 = "replace";
            // string filePath1 = "replace";

            //GimData gimD2 = t2.ReadToGimData(path2, "step");
            //var testocct1 = occt.WriteGimDataToOcctIFC(gimD1);
            //var testocct2 = occt2.WriteGimDataToOcctIFC(gimD2);
            //BRepAlgoCutWrapper brepCutter = new(testocct1, testocct2);
            //TestBoolWrapper testBool = new(path1, path2);
            //testBool.PerformCut(filePath1);
            //brepCutter.Build();
            //var resultingOCCTshape = brepCutter.Shape();
            ////BOPAlgo_BOPWrapper bopCutter = new();
            ////var resultingOCCTshape = bopCutter.PrepareCut(testocct1, testocct2);
            //var resultingGIMData = occt3.ReadToGimDataFromIFC(resultingOCCTshape);
            ////string filePath1 = "replace";
            ////foreach (string warning in bopCutter.GetWarnings())
            ////{
            ////    Console.WriteLine("Warning: " + warning);
            ////}
            ////foreach (string error in bopCutter.GetErrors())
            ////{
            ////    Console.WriteLine("Warning: " + error);
            ////}
            ////assimp.WriteGimDataToAssimp(filePath1, "glb", resultingGIMData);
            //occt4.WriteGimDataToOcct(filePath1, "step", resultingGIMData);


            //GimData gd = GIMocctReadElements.Test(FileSelector.FileName);
            //string ifcpath = FileSelector.FileName;

            //IFCReaderCLR ifcreader = new();
            //ifcreader.LoadDoc(ifcpath);
            // var shapes = ifcreader.LoadFile(ifcpath);
            //List<GimData> gimDatas = new();
            //foreach ( var shape in shapes )
            //{
            //    GIMocctReadElements occtReader = new(new());
            //    gimDatas.Add(occtReader.ReadToGimDataFromIFC(shape));
            //}
           // GIMocctReadElements occtReader = new(new());
            //GimData resultingGD = occtReader.ReadToGimDataFromIFC(ifcpath);
            //resultingGD.CreateRootIfNotExists();
            //resultingGD.elements.Add(new()
            //{
            //    ElementType = GimElementType.D_folder

            //});
            //resultingGD.AddChildToParent(1, 0);
            //int count = 0;
            //foreach ( var gimularData in gimDatas )
            //{

            //    GIMtransfer.FileAccess.WriteFromGimData(gimularData, "glb", "IFCparts/part-" + count++);
            //    resultingGD.AddSecondaryGimData(gimularData, 1, 1,0);
            //}

            //gimData_currentSelected = resultingGD;
            SetRunButtons(true);


            //return;

            GimData gd = IFCReadElements.ReadIFCToGimData(FileSelector.FileName);
            return;
            string fileName = Path.GetFileNameWithoutExtension(FileSelector.FileName);
            GimProgressStatus gimProgressStatus = new GimProgressStatus();
            GimProgressStatus gimProgressStatus2 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus3 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus4 = new GimProgressStatus();
            GIMtransfer.GIMocctWriteElements ack = new(gimProgressStatus);
            GIMtransfer.GIMocctWriteElements ack4 = new(gimProgressStatus4);
            GIMtransfer.GIMocctReadElements ack2 = new(gimProgressStatus2);
            AssimpWriteElements ack3 = new(gimProgressStatus3);
            //string filePath = "replace";
            //string filePath = "replace" + fileName;
            string filePath = "replace" + fileName;
            var test = ack.WriteGimDataToOcctIFC(gd);
            var test2 = ack2.ReadToGimDataFromIFC(test);
            ack3.WriteGimDataToAssimp(filePath, "glb", test2, FileSelector.FileName);
            //gimProgressStatus = new GimProgressStatus();
            //ack.WriteGimDataToOcct(filePath, "step", gd);
            //TopoDSAbstractShapeWrapper shraper = null;
            return;
            string[] filesToTest = new string[]
            {
                "replace",
                "replace",
                "replace",
                "replace"
            };

            FileSelector.Multiselect = true;
            var dialogResult = FileSelector.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                FileSelector.Multiselect = false;
                return;
            }


            List<GLBStruct> glbObjects = new();
            foreach (string file in FileSelector.FileNames)
            {
                var glbBytes = File.ReadAllBytes(file);
                glbObjects.Add(new()
                {
                    Bytes = glbBytes,
                    Name = Path.GetFileNameWithoutExtension(file),
                    Xyz = new double[] { 0, 0, 0 },
                    Rpy = new double[] { 0, 0, 0 }
                });
            }

            string exportName = "downloadSceneTest";

            var json = GIMtransfer.FileAccess.GimMapSceneDownload(glbObjects, OutputSelect.Text[1..], exportName, new(), new());
            //string serializedJson = JsonConvert.SerializeObject(json);
            //GIMtransfer.FileAccess.JsonToJson(new[] { serializedJson }, "CombineScene", new(), new(), new());
            FileSelector.Multiselect = false;
            return;

            GimData gimData = 
                GIMtransfer.FileAccess.ReadToGimData(FileSelector.FileName, FileSelector.FileName.Split('.')[^1]);

            List<GimPolygon2d> allPolygons = new();
            List<string> types = new();
            for(int i=0; i < gimData.Objects.Count; i++)
            {
                var obj = gimData.Objects[i];
                uint elIndex = obj.Element;
                var element = gimData.Elements[(int)elIndex];

                Matrix transformation = new Matrix(element.Locals[0]);
                Vect offset = new(
                    element.Locals[0].Px,
                    element.Locals[0].Py,
                    element.Locals[0].Pz
                    );

                uint polyMeshIndex = element.Elements[0];
                var polyMesh = gimData.Elements[(int)polyMeshIndex];
                uint polygonIndex = polyMesh.Elements[0];
                var polygon = gimData.Elements[(int)polygonIndex];
                uint materialIndex = polygon.Elements[0];
                var material = gimData.Elements[(int)materialIndex].AsMaterial();

                var color = material.BaseColour;
                var colorHex = Convert.ToString(color, 16);
                string type = "";
                switch (colorHex)
                {

                    case "AA4A44": type = "bridge"; break;
                    case "c16464": type = "building"; break;
                    case "6464c1": type = "building";break;
                    case "A0A0A0": type =  "genericcityobject"; break;
                    case "69f7a6": type = "landuse"; break;
                    case "228b22": type = "plantcover"; break;
                    case "8c8eab": type = "road"; break;
                    case "1ca3ec": type = "waterbody"; break;
                }

                var triangleMesh = PolygonRounder.ConstructTriangles(polygon);
                List<Triangle2d> triangles2d = new();
                HashSet<Vertex2d> uniquePoints2d = new();
                foreach(var triangle in triangleMesh.Triangles)
                {
                    triangle.Vertex1 = transformation * triangle.Vertex1 + offset;
                    triangle.Vertex2 = transformation * triangle.Vertex2 + offset;
                    triangle.Vertex3 = transformation * triangle.Vertex3 + offset;

                    var v1 = triangle.Vertex1;
                    var v2 = triangle.Vertex2;
                    var v3 = triangle.Vertex3;

                    var v1_2d = new Vertex2d(v1[0], v1[1]);
                    var v2_2d = new Vertex2d(v2[0], v2[1]);
                    var v3_2d = new Vertex2d(v3[0], v3[1]);

                    Vertex2d v1out, v2out, v3out;
                    if (!uniquePoints2d.TryGetValue(v1_2d, out v1out))
                    {
                        v1out = v1_2d;
                        uniquePoints2d.Add(v1out);
                    }

                    if (!uniquePoints2d.TryGetValue(v2_2d, out v2out))
                    {
                        v2out = v2_2d;
                        uniquePoints2d.Add(v2out);
                    }

                    if (!uniquePoints2d.TryGetValue(v3_2d, out v3out))
                    {
                        v3out = v3_2d;
                        uniquePoints2d.Add(v3out);
                    }

                    triangles2d.Add(new(
                        v1out, v2out, v3out 
                        ));
                }

                Triangle2d.ConstructNeighborhouds(triangles2d);
                var boundaryGroups = PolygonRounder.GetTriangleGroupBoundaries2d(triangles2d);
                //string trianglesString = Triangle2d.Triangles2dToString(triangles2d);
                //foreach (var boundary in boundaryGroups)
                //{
                //    var boundaryString = GimPolygon.Vertex2dListToString(boundary);
                //}
                allPolygons.Add(new(boundaryGroups));
                types.Add(type);
            }

            GimPolygon2d.ToDesmosFileWithTypes(allPolygons, types);

            //string path = "replace";
            //var modelroot = GLTFReadElements.ReadGLB(FileSelector.FileName);
            //var gimData = GLTFReadElements.ConvertModelRootToGimData(modelroot);
            //gimData_currentSelected = gimData;
            //PolygonRounder.ClipGimDataByXY(gimData_currentSelected, -50000, -80000, 50000, 80000);
        }

        private void UpdateReadProgressBar(int progress)
        {
            UpdateProgressBar(ReadProgressBar, progress);
        }

        private void UpdateWriteProgressBar(int progress)
        {
            UpdateProgressBar(WriteProgressBar, progress);
        }

        private void UpdateProgressBar(ProgressBar progressBar, int progress)
        {
            if (!progressBar.InvokeRequired)
            {
                progressBar.Value = progress;
                return;
            }

            progressBar.Invoke((MethodInvoker)delegate
            {
                progressBar.Value = progress;
            });
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void VersionLabel_Click(object sender, EventArgs e)
        {

        }
        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void Comparison_Click(object sender, EventArgs e)
        {

#if DEBUG

            string fileName = null;
            var dialogResult = FileSelector.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                fileName = FileSelector.FileName;
            }
            else if (dialogResult == DialogResult.Cancel)
            {
                return;
            }
            else
            {
                return;
            }

            ComparisonVisitor comparer = new(fileName);

            var finalComp = comparer.FinalComparison;
            comparer.ExportShapes();
#endif
        }

        private void DemoteChildrenButton_Click(object sender, EventArgs e)
        {

            string pathFileName = FileSelector.FileName;
            string jsonContent = File.ReadAllText(pathFileName);
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            JsonGimObject jsonObj = JsonManager.ReadGimJson(jsonContent);

            var demoteForm = new DemoteForm(jsonObj);
            demoteForm.ShowDialog();
            if (demoteForm.Aborted) return;
            string[] jsonsToOutput = new string[demoteForm.FinalDemotionSelection.Count + 1];

            int projectID = demoteForm.ProjectID;

            string outputName = pathFileName.Split("\\")[^1].Split(".")[0];

            var objectsToPromote = demoteForm.FinalPromotionSelection;
            var objectsToDemote = demoteForm.FinalDemotionSelection;
            HashSet<int> objectsChecked = new();
            int jsonIndex = 1;
            foreach (var jsonChild in jsonObj.Children)
            {
                bool alreadyChecked = !objectsChecked.Add(jsonChild.ID);
                if (alreadyChecked) continue;
                else if (objectsToDemote.ContainsKey(jsonChild.ID))
                {
                    jsonChild.Crud = Crud.Update;
                    jsonChild.ProjectID = null;
                    jsonChild.ObjectID = null;

                    string childJsonText = File.ReadAllText(objectsToDemote[jsonChild.ID]);
                    JsonGimObject childJsonObject = JsonManager.ReadGimJson(childJsonText);
                    childJsonObject.ProjectID = (demoteForm.DemoteProjObjIDs[jsonChild.ID].Item1).ToString();
                    childJsonObject.ObjectID = (demoteForm.DemoteProjObjIDs[jsonChild.ID].Item2).ToString();
                    string modifiedChildJsonText = JsonConvert.SerializeObject(childJsonObject);
                    jsonsToOutput[jsonIndex] = modifiedChildJsonText;
                    jsonIndex++;
                }
                else if (objectsToPromote.ContainsKey(jsonChild.ID))
                {
                    int promoteID = objectsToPromote[jsonChild.ID];
                    jsonChild.Crud = Crud.Update;
                    if (projectID != 0) jsonChild.ProjectID = projectID;
                    jsonChild.ObjectID = promoteID;
                }
                objectsChecked.Add(jsonChild.ID);
            }
             
            string modifiedJsonText = JsonConvert.SerializeObject(jsonObj);

            jsonsToOutput[0] = modifiedJsonText;
            GIMtransfer.FileAccess.JsonToJson(jsonsToOutput, outputName + " demoted", new(), new(), new());
        }

        private void ScaleInput_ValueChanged(object sender, EventArgs e)
        {

        }

        private void ReadProgressBar_Click(object sender, EventArgs e)
        {

        }

        // A3: manual harness button for the KLIC / IMKL 2.0 network-payload lane. Opens an
        // XML/GML/KML file, routes it through GIMtransfer.FileAccess.FileToNetworkPayloadAsync
        // (fully qualified: it shadows System.IO.FileAccess) and reports Kind + counts + elapsed.
        // On klic/network the payload JSON is exported to %TEMP%\GIM\<file base>.json
        // (collision-safe suffix) and the absolute path is logged; every error surfaces verbatim
        // in the log box. The interactive run is the user's verify-gate; nothing else changes here.
        private async void NetworkPayload_Click(object sender, EventArgs e)
        {
            using OpenFileDialog dialog = new()
            {
                Title = "Select XML / GML / KML file for network payload",
                Filter = "XML-family files (*.xml;*.gml;*.kml)|*.xml;*.gml;*.kml|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            string pathFileName = dialog.FileName;

            // Only this button's own state is touched (the new button owns its file dialog), so no
            // other TestUI behavior changes while the read runs.
            NetworkPayloadButton.Enabled = false;
            NetworkPayloadLog.Clear();
            NetworkPayloadStatus.Text = "Network payload: reading " + pathFileName + " ...";

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                // Mirror the existing FileToJson_Click convention for the status argument:
                // a fresh GimProgressStatus passed straight in (no Create() lifecycle).
                GimProgressStatus readStatus = new();
                GimOptions gimOptions = GetGimOptions();

                NetworkPayloadResult result = await GIMtransfer.FileAccess.FileToNetworkPayloadAsync(
                    pathFileName, readStatus, gimOptions);

                stopwatch.Stop();

                if (result.Kind == KlicKind.NotNetwork)
                {
                    NetworkPayloadStatus.Text =
                        $"Network payload: Kind={result.Kind} (no network lines) | " +
                        $"elapsed {stopwatch.ElapsedMilliseconds} ms";
                    AppendNetworkLog($"Kind={result.Kind}: '{pathFileName}' has no network-line content; " +
                        "use the mesh path (File To Json) instead.");
                    return;
                }

                if (string.IsNullOrEmpty(result.Json))
                {
                    NetworkPayloadStatus.Text =
                        $"Network payload: Kind={result.Kind} | no JSON payload | " +
                        $"elapsed {stopwatch.ElapsedMilliseconds} ms";
                    AppendNetworkLog("engine produced no JSON payload");
                    return;
                }

                string exportPath = ExportNetworkPayload(pathFileName, result);

                NetworkPayloadStatus.Text =
                    $"Network payload: Kind={result.Kind} | Meldnummer={result.Meldnummer ?? "(none)"} | " +
                    $"NetworkCount={result.NetworkCount} | LineCount={result.LineCount} | " +
                    $"elapsed {stopwatch.ElapsedMilliseconds} ms";

                AppendNetworkLog($"Kind={result.Kind}");
                AppendNetworkLog("Reader OutputPath: " + (result.OutputPath ?? "(none)"));
                AppendNetworkLog("Exported payload JSON: " + exportPath);
                AppendNetworkLog(
                    $"NetworkCount={result.NetworkCount}, LineCount={result.LineCount}, " +
                    $"elapsed {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // Errors surface verbatim (no swallowing): full exception text incl. stack trace.
                NetworkPayloadStatus.Text = "Network payload: FAILED - see log.";
                AppendNetworkLog("ERROR: " + ex);
            }
            finally
            {
                NetworkPayloadButton.Enabled = true;
            }
        }

        // Exports the payload JSON to %TEMP%\GIM\<file base>.json with a collision-safe suffix.
        // The engine writes to its own nested temp path; the flat export here is the A3 contract
        // target, so it is always written (never short-circuited on the reader's OutputPath).
        private static string ExportNetworkPayload(string sourcePath, NetworkPayloadResult result)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "GIM");
            Directory.CreateDirectory(tempFolder);

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            if (string.IsNullOrEmpty(baseName))
                baseName = "network-payload";

            string target = Path.Combine(tempFolder, baseName + ".json");

            if (File.Exists(target))
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                target = Path.Combine(tempFolder, baseName + "_" + stamp + ".json");
                int counter = 1;
                while (File.Exists(target))
                    target = Path.Combine(tempFolder, baseName + "_" + stamp + "_" + counter++ + ".json");
            }

            // Debug builds pretty-print the exported file for inspection only; the transport JSON
            // the engine emits stays compact. Any parse error falls back to the raw payload verbatim.
            string jsonToWrite = result.Json;
#if DEBUG
            try
            {
                System.Text.Json.Nodes.JsonNode node = System.Text.Json.Nodes.JsonNode.Parse(result.Json);
                jsonToWrite = node.ToJsonString(
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception)
            {
                jsonToWrite = result.Json;
            }
#endif

            File.WriteAllText(target, jsonToWrite);
            return target;
        }

        private void AppendNetworkLog(string message)
        {
            NetworkPayloadLog.AppendText(
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                "  " + message + Environment.NewLine);
        }
    }
}
