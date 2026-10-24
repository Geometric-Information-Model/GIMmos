using GIMcompress;

namespace TestUI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.FileSelect = new System.Windows.Forms.Button();
            this.FileSelector = new System.Windows.Forms.OpenFileDialog();
            this.Run = new System.Windows.Forms.Button();
            this.FileName = new System.Windows.Forms.TextBox();
            this.CompressButton = new System.Windows.Forms.CheckBox();
            this.CadexOnly = new System.Windows.Forms.CheckBox();
            this.OutputSelect = new System.Windows.Forms.ComboBox();
            this.FolderSelector = new System.Windows.Forms.FolderBrowserDialog();
            this.OpenConsole = new System.Windows.Forms.Button();
            this.ReadGimData = new System.Windows.Forms.Button();
            this.RoundPrecisionBar = new System.Windows.Forms.TrackBar();
            this.RoundValLabel = new System.Windows.Forms.Label();
            this.profileButton = new System.Windows.Forms.Button();
            this.RoundValues = new System.Windows.Forms.Button();
            this.ObjectIDLabel = new System.Windows.Forms.Label();
            this.ObjectID = new System.Windows.Forms.NumericUpDown();
            this.Joints = new System.Windows.Forms.Button();
            this.BatchTest = new System.Windows.Forms.Button();
            this.quickTestCheckbox = new System.Windows.Forms.CheckBox();
            this.WriteElemCounts = new System.Windows.Forms.Button();
            this.TestFeature = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.VersionLabel = new System.Windows.Forms.Label();
            this.quickTestCheckbox1 = new System.Windows.Forms.CheckBox();
            this.ReadProgressBar = new System.Windows.Forms.ProgressBar();
            this.WriteProgressBar = new System.Windows.Forms.ProgressBar();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.FileToJson = new System.Windows.Forms.Button();
            this.JsonToFile = new System.Windows.Forms.Button();
            this.JsonToJson = new System.Windows.Forms.Button();
            this.binaryUnitDropDown = new System.Windows.Forms.ComboBox();
            this.visualisationUnitDropDown = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.Comparison = new System.Windows.Forms.Button();
            this.DemoteChildrenButton = new System.Windows.Forms.Button();
            this.ScaleInput = new System.Windows.Forms.NumericUpDown();
            this.ScaleInputLabel = new System.Windows.Forms.Label();
            this.FileToSvg = new System.Windows.Forms.Button();
            this.NetworkPayloadButton = new System.Windows.Forms.Button();
            this.NetworkPayloadStatus = new System.Windows.Forms.Label();
            this.NetworkPayloadLog = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.RoundPrecisionBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ObjectID)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ScaleInput)).BeginInit();
            this.SuspendLayout();
            // 
            // FileSelect
            // 
            this.FileSelect.Location = new System.Drawing.Point(17, 17);
            this.FileSelect.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.FileSelect.Name = "FileSelect";
            this.FileSelect.Size = new System.Drawing.Size(139, 38);
            this.FileSelect.TabIndex = 0;
            this.FileSelect.Text = "Select File";
            this.FileSelect.UseVisualStyleBackColor = true;
            this.FileSelect.Click += new System.EventHandler(this.FileSelect_Click);
            // 
            // FileSelector
            // 
            this.FileSelector.FileName = "SelectedFile";
            this.FileSelector.FileOk += new System.ComponentModel.CancelEventHandler(this.FileSelector_FileOk);
            // 
            // Run
            // 
            this.Run.Location = new System.Drawing.Point(731, 17);
            this.Run.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Run.Name = "Run";
            this.Run.Size = new System.Drawing.Size(213, 38);
            this.Run.TabIndex = 1;
            this.Run.Text = "Run";
            this.Run.UseVisualStyleBackColor = true;
            this.Run.Click += new System.EventHandler(this.Run_Click);
            // 
            // FileName
            // 
            this.FileName.Location = new System.Drawing.Point(17, 68);
            this.FileName.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.FileName.Name = "FileName";
            this.FileName.Size = new System.Drawing.Size(1107, 31);
            this.FileName.TabIndex = 2;
            this.FileName.TextChanged += new System.EventHandler(this.FileName_TextChanged);
            // 
            // CompressButton
            // 
            this.CompressButton.AutoSize = true;
            this.CompressButton.Checked = true;
            this.CompressButton.CheckState = System.Windows.Forms.CheckState.Checked;
            this.CompressButton.Location = new System.Drawing.Point(461, 20);
            this.CompressButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.CompressButton.Name = "CompressButton";
            this.CompressButton.Size = new System.Drawing.Size(118, 29);
            this.CompressButton.TabIndex = 3;
            this.CompressButton.Text = "Compress";
            this.CompressButton.UseVisualStyleBackColor = true;
            this.CompressButton.CheckedChanged += new System.EventHandler(this.Compress_CheckedChanged);
            // 
            // CadexOnly
            // 
            this.CadexOnly.AutoSize = true;
            this.CadexOnly.Location = new System.Drawing.Point(583, 20);
            this.CadexOnly.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.CadexOnly.Name = "CadexOnly";
            this.CadexOnly.Size = new System.Drawing.Size(128, 29);
            this.CadexOnly.TabIndex = 4;
            this.CadexOnly.Text = "Cadex Only";
            this.CadexOnly.UseVisualStyleBackColor = true;
            this.CadexOnly.CheckedChanged += new System.EventHandler(this.CadexOnly_CheckedChanged);
            // 
            // OutputSelect
            // 
            this.OutputSelect.AllowDrop = true;
            this.OutputSelect.FormattingEnabled = true;
            this.OutputSelect.Items.AddRange(new object[] {
            ".bin",
            ".sat",
            ".brep",
            ".dae",
            ".dxf",
            ".fbx",
            ".gltf",
            ".ifc",
            ".iges",
            ".jt",
            ".obj",
            ".x_t",
            ".3dm",
            ".stp",
            ".stl",
            ".usd",
            ".wrl",
            ".x3d",
            ".json",
            ".glb",
            ".gml"
            });
            this.OutputSelect.Location = new System.Drawing.Point(953, 18);
            this.OutputSelect.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.OutputSelect.Name = "OutputSelect";
            this.OutputSelect.Size = new System.Drawing.Size(171, 33);
            this.OutputSelect.TabIndex = 5;
            this.OutputSelect.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // OpenConsole
            // 
            this.OpenConsole.Location = new System.Drawing.Point(649, 544);
            this.OpenConsole.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.OpenConsole.Name = "OpenConsole";
            this.OpenConsole.Size = new System.Drawing.Size(477, 90);
            this.OpenConsole.TabIndex = 12;
            this.OpenConsole.Text = "Open Console";
            this.OpenConsole.UseVisualStyleBackColor = true;
            this.OpenConsole.Click += new System.EventHandler(this.OpenConsole_Click);
            // 
            // ReadGimData
            // 
            this.ReadGimData.Location = new System.Drawing.Point(164, 17);
            this.ReadGimData.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ReadGimData.Name = "ReadGimData";
            this.ReadGimData.Size = new System.Drawing.Size(146, 38);
            this.ReadGimData.TabIndex = 17;
            this.ReadGimData.Text = "Read GimData";
            this.ReadGimData.UseVisualStyleBackColor = true;
            this.ReadGimData.Click += new System.EventHandler(this.ReadGimData_Click);
            // 
            // RoundPrecisionBar
            // 
            this.RoundPrecisionBar.Location = new System.Drawing.Point(81, 237);
            this.RoundPrecisionBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.RoundPrecisionBar.Name = "RoundPrecisionBar";
            this.RoundPrecisionBar.Size = new System.Drawing.Size(477, 69);
            this.RoundPrecisionBar.TabIndex = 19;
            // 
            // RoundValLabel
            // 
            this.RoundValLabel.AutoSize = true;
            this.RoundValLabel.Location = new System.Drawing.Point(223, 193);
            this.RoundValLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.RoundValLabel.Name = "RoundValLabel";
            this.RoundValLabel.Size = new System.Drawing.Size(173, 25);
            this.RoundValLabel.TabIndex = 20;
            this.RoundValLabel.Text = "Rounding Precision: ";
            // 
            // profileButton
            // 
            this.profileButton.Location = new System.Drawing.Point(319, 17);
            this.profileButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.profileButton.Name = "profileButton";
            this.profileButton.Size = new System.Drawing.Size(119, 38);
            this.profileButton.TabIndex = 21;
            this.profileButton.Text = "Profile";
            this.profileButton.UseVisualStyleBackColor = true;
            this.profileButton.Click += new System.EventHandler(this.profileButton_Click);
            // 
            // RoundValues
            // 
            this.RoundValues.Enabled = false;
            this.RoundValues.Location = new System.Drawing.Point(39, 435);
            this.RoundValues.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.RoundValues.Name = "RoundValues";
            this.RoundValues.Size = new System.Drawing.Size(477, 90);
            this.RoundValues.TabIndex = 22;
            this.RoundValues.Text = "Round Values";
            this.RoundValues.UseVisualStyleBackColor = true;
            this.RoundValues.Click += new System.EventHandler(this.RoundValues_Click_1);
            // 
            // ObjectIDLabel
            // 
            this.ObjectIDLabel.AutoSize = true;
            this.ObjectIDLabel.Location = new System.Drawing.Point(929, 124);
            this.ObjectIDLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ObjectIDLabel.Name = "ObjectIDLabel";
            this.ObjectIDLabel.Size = new System.Drawing.Size(131, 25);
            this.ObjectIDLabel.TabIndex = 23;
            this.ObjectIDLabel.Text = "Json Object ID:";
            this.ObjectIDLabel.Click += new System.EventHandler(this.ObjectIDLabel_Click);
            // 
            // ObjectID
            // 
            this.ObjectID.Location = new System.Drawing.Point(920, 154);
            this.ObjectID.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ObjectID.Name = "ObjectID";
            this.ObjectID.Size = new System.Drawing.Size(171, 31);
            this.ObjectID.TabIndex = 24;
            this.ObjectID.ValueChanged += new System.EventHandler(this.ObjectID_ValueChanged);
            // 
            // Joints
            // 
            this.Joints.Location = new System.Drawing.Point(648, 662);
            this.Joints.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Joints.Name = "Joints";
            this.Joints.Size = new System.Drawing.Size(476, 87);
            this.Joints.TabIndex = 25;
            this.Joints.Text = "Joints";
            this.Joints.UseVisualStyleBackColor = true;
            this.Joints.Click += new System.EventHandler(this.Joints_Click);
            // 
            // BatchTest
            // 
            this.BatchTest.Location = new System.Drawing.Point(39, 303);
            this.BatchTest.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.BatchTest.Name = "BatchTest";
            this.BatchTest.Size = new System.Drawing.Size(346, 87);
            this.BatchTest.TabIndex = 26;
            this.BatchTest.Text = "batch test read/write with properties";
            this.BatchTest.UseVisualStyleBackColor = true;
            this.BatchTest.Click += new System.EventHandler(this.batchClick);
            // 
            // quickTestCheckbox
            // 
            this.quickTestCheckbox.AutoSize = true;
            this.quickTestCheckbox.Location = new System.Drawing.Point(39, 397);
            this.quickTestCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.quickTestCheckbox.Name = "quickTestCheckbox";
            this.quickTestCheckbox.Size = new System.Drawing.Size(372, 29);
            this.quickTestCheckbox.TabIndex = 27;
            this.quickTestCheckbox.Text = "Check to run batch test without properties";
            this.quickTestCheckbox.UseVisualStyleBackColor = true;
            this.quickTestCheckbox.CheckedChanged += new System.EventHandler(this.quickTestCheckbox_CheckedChanged);
            // 
            // WriteElemCounts
            // 
            this.WriteElemCounts.Location = new System.Drawing.Point(454, 165);
            this.WriteElemCounts.Name = "WriteElemCounts";
            this.WriteElemCounts.Size = new System.Drawing.Size(334, 53);
            this.WriteElemCounts.TabIndex = 28;
            this.WriteElemCounts.Text = "Write element counts";
            this.WriteElemCounts.UseVisualStyleBackColor = true;
            this.WriteElemCounts.Click += new System.EventHandler(this.WriteElemCounts_Click);
            // 
            // TestFeature
            // 
            this.TestFeature.Location = new System.Drawing.Point(39, 547);
            this.TestFeature.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.TestFeature.Name = "TestFeature";
            this.TestFeature.Size = new System.Drawing.Size(476, 87);
            this.TestFeature.TabIndex = 28;
            this.TestFeature.Text = "Test Feature";
            this.TestFeature.UseVisualStyleBackColor = true;
            this.TestFeature.Click += new System.EventHandler(this.TestFeature_Click);
            // 
            // VersionLabel
            // 
            this.VersionLabel.AutoSize = true;
            this.VersionLabel.Location = new System.Drawing.Point(39, 787);
            this.VersionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.VersionLabel.Name = "VersionLabel";
            this.VersionLabel.Size = new System.Drawing.Size(117, 25);
            this.VersionLabel.TabIndex = 29;
            this.VersionLabel.Text = "Version: 1.1.0\r\n";
            this.VersionLabel.Click += new System.EventHandler(this.VersionLabel_Click);
            // 
            // quickTestCheckbox1
            // 
            this.quickTestCheckbox1.AutoSize = true;
            this.quickTestCheckbox1.Location = new System.Drawing.Point(394, 350);
            this.quickTestCheckbox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.quickTestCheckbox1.Name = "quickTestCheckbox1";
            this.quickTestCheckbox1.Size = new System.Drawing.Size(118, 29);
            this.quickTestCheckbox1.TabIndex = 30;
            this.quickTestCheckbox1.Text = "Quick Test";
            this.quickTestCheckbox1.UseVisualStyleBackColor = true;
            // 
            // ReadProgressBar
            // 
            this.ReadProgressBar.Location = new System.Drawing.Point(995, 792);
            this.ReadProgressBar.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ReadProgressBar.Name = "ReadProgressBar";
            this.ReadProgressBar.Size = new System.Drawing.Size(143, 40);
            this.ReadProgressBar.TabIndex = 31;
            this.ReadProgressBar.Click += new System.EventHandler(this.ReadProgressBar_Click);
            // 
            // WriteProgressBar
            // 
            this.WriteProgressBar.Location = new System.Drawing.Point(649, 792);
            this.WriteProgressBar.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.WriteProgressBar.Name = "WriteProgressBar";
            this.WriteProgressBar.Size = new System.Drawing.Size(143, 40);
            this.WriteProgressBar.TabIndex = 32;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(988, 753);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(150, 25);
            this.label2.TabIndex = 33;
            this.label2.Text = "Reading Progress";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(649, 753);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(144, 25);
            this.label3.TabIndex = 34;
            this.label3.Text = "Writing Progress";
 
            // 
            // FileToJson
            // 
            this.FileToJson.Location = new System.Drawing.Point(241, 112);
            this.FileToJson.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.FileToJson.Name = "FileToJson";
            this.FileToJson.Size = new System.Drawing.Size(196, 48);
            this.FileToJson.TabIndex = 35;
            this.FileToJson.Text = "File To Json";
            this.FileToJson.UseVisualStyleBackColor = true;
            this.FileToJson.Click += new System.EventHandler(this.FileToJson_Click);
            // 
            // JsonToFile
            // 
            this.JsonToFile.Location = new System.Drawing.Point(716, 112);
            this.JsonToFile.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.JsonToFile.Name = "JsonToFile";
            this.JsonToFile.Size = new System.Drawing.Size(196, 48);
            this.JsonToFile.TabIndex = 36;
            this.JsonToFile.Text = "Json To File";
            this.JsonToFile.UseVisualStyleBackColor = true;
            this.JsonToFile.Click += new System.EventHandler(this.JsonToFile_Click);
            // 
            // JsonToJson
            // 
            this.JsonToJson.Location = new System.Drawing.Point(483, 112);
            this.JsonToJson.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.JsonToJson.Name = "JsonToJson";
            this.JsonToJson.Size = new System.Drawing.Size(196, 48);
            this.JsonToJson.TabIndex = 37;
            this.JsonToJson.Text = "Json To Json";
            this.JsonToJson.UseVisualStyleBackColor = true;
            this.JsonToJson.Click += new System.EventHandler(this.JsonToJson_Click);
            //
            // FileToSvg
            //
            this.FileToSvg.Location = new System.Drawing.Point(39, 112);
            this.FileToSvg.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.FileToSvg.Name = "FileToSvg";
            this.FileToSvg.Size = new System.Drawing.Size(196, 48);
            this.FileToSvg.TabIndex = 34;
            this.FileToSvg.Text = "File To Svg";
            this.FileToSvg.UseVisualStyleBackColor = true;
            this.FileToSvg.Click += new System.EventHandler(this.FileToSvg_Click);
            // 
            // binaryUnitDropDown
            // 
            this.binaryUnitDropDown.FormattingEnabled = true;
            this.binaryUnitDropDown.Location = new System.Drawing.Point(683, 270);
            this.binaryUnitDropDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.binaryUnitDropDown.Name = "binaryUnitDropDown";
            this.binaryUnitDropDown.Size = new System.Drawing.Size(188, 33);
            this.binaryUnitDropDown.TabIndex = 38;
            // 
            // visualisationUnitDropDown
            // 
            this.visualisationUnitDropDown.FormattingEnabled = true;
            this.visualisationUnitDropDown.Location = new System.Drawing.Point(907, 270);
            this.visualisationUnitDropDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.visualisationUnitDropDown.Name = "visualisationUnitDropDown";
            this.visualisationUnitDropDown.Size = new System.Drawing.Size(188, 33);
            this.visualisationUnitDropDown.TabIndex = 39;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(727, 240);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(97, 25);
            this.label4.TabIndex = 40;
            this.label4.Text = "Binary Unit";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(929, 240);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(147, 25);
            this.label5.TabIndex = 41;
            this.label5.Text = "Visualisation Unit";
            this.label5.Click += new System.EventHandler(this.label5_Click);
            // 
            // Comparison
            // 
            this.Comparison.Location = new System.Drawing.Point(39, 662);
            this.Comparison.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Comparison.Name = "Comparison";
            this.Comparison.Size = new System.Drawing.Size(476, 87);
            this.Comparison.TabIndex = 42;
            this.Comparison.Text = "Compare GimData and ModelData";
            this.Comparison.UseVisualStyleBackColor = true;
            this.Comparison.Click += new System.EventHandler(this.Comparison_Click);
            // 
            // DemoteChildrenButton
            // 
            this.DemoteChildrenButton.Location = new System.Drawing.Point(649, 435);
            this.DemoteChildrenButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.DemoteChildrenButton.Name = "DemoteChildrenButton";
            this.DemoteChildrenButton.Size = new System.Drawing.Size(477, 90);
            this.DemoteChildrenButton.TabIndex = 44;
            this.DemoteChildrenButton.Text = "Promote ↑/Demote ↓";
            this.DemoteChildrenButton.UseVisualStyleBackColor = true;
            this.DemoteChildrenButton.Click += new System.EventHandler(this.DemoteChildrenButton_Click);
            // 
            // ScaleInput
            // 
            this.ScaleInput.DecimalPlaces = 10;
            this.ScaleInput.Location = new System.Drawing.Point(799, 349);
            this.ScaleInput.Maximum = new decimal(new int[] {
            1410065408,
            2,
            0,
            0});
            this.ScaleInput.Name = "ScaleInput";
            this.ScaleInput.Size = new System.Drawing.Size(180, 31);
            this.ScaleInput.TabIndex = 45;
            this.ScaleInput.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.ScaleInput.ValueChanged += new System.EventHandler(this.ScaleInput_ValueChanged);
            // 
            // ScaleInputLabel
            // 
            this.ScaleInputLabel.AutoSize = true;
            this.ScaleInputLabel.Location = new System.Drawing.Point(853, 321);
            this.ScaleInputLabel.Name = "ScaleInputLabel";
            this.ScaleInputLabel.Size = new System.Drawing.Size(52, 25);
            this.ScaleInputLabel.TabIndex = 46;
            this.ScaleInputLabel.Text = "Scale";
            // 
            // IfcPreviewCheckBox
            // 
            this.IfcPreviewCheckBox = new System.Windows.Forms.CheckBox();
            this.IfcPreviewCheckBox.AutoSize = true;
            this.IfcPreviewCheckBox.Location = new System.Drawing.Point(799, 390);
            this.IfcPreviewCheckBox.Name = "IfcPreviewCheckBox";
            this.IfcPreviewCheckBox.Size = new System.Drawing.Size(120, 25);
            this.IfcPreviewCheckBox.TabIndex = 47;
            this.IfcPreviewCheckBox.Text = "IFC Preview";
            this.IfcPreviewCheckBox.UseVisualStyleBackColor = true;
            // 
            // NetworkPayloadButton
            // 
            this.NetworkPayloadButton.Location = new System.Drawing.Point(39, 852);
            this.NetworkPayloadButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.NetworkPayloadButton.Name = "NetworkPayloadButton";
            this.NetworkPayloadButton.Size = new System.Drawing.Size(300, 48);
            this.NetworkPayloadButton.TabIndex = 48;
            this.NetworkPayloadButton.Text = "File To Network Payload";
            this.NetworkPayloadButton.UseVisualStyleBackColor = true;
            this.NetworkPayloadButton.Click += new System.EventHandler(this.NetworkPayload_Click);
            // 
            // NetworkPayloadStatus
            // 
            this.NetworkPayloadStatus.AutoSize = true;
            this.NetworkPayloadStatus.Location = new System.Drawing.Point(39, 910);
            this.NetworkPayloadStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.NetworkPayloadStatus.Name = "NetworkPayloadStatus";
            this.NetworkPayloadStatus.Size = new System.Drawing.Size(180, 25);
            this.NetworkPayloadStatus.TabIndex = 49;
            this.NetworkPayloadStatus.Text = "Network payload: idle";
            // 
            // NetworkPayloadLog
            // 
            this.NetworkPayloadLog.Location = new System.Drawing.Point(39, 940);
            this.NetworkPayloadLog.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.NetworkPayloadLog.Multiline = true;
            this.NetworkPayloadLog.Name = "NetworkPayloadLog";
            this.NetworkPayloadLog.ReadOnly = true;
            this.NetworkPayloadLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.NetworkPayloadLog.Size = new System.Drawing.Size(1086, 125);
            this.NetworkPayloadLog.TabIndex = 50;
            this.NetworkPayloadLog.WordWrap = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1151, 1080);
            this.Controls.Add(this.NetworkPayloadLog);
            this.Controls.Add(this.NetworkPayloadStatus);
            this.Controls.Add(this.NetworkPayloadButton);
            this.Controls.Add(this.ScaleInputLabel);
            this.Controls.Add(this.ScaleInput);
            this.Controls.Add(this.IfcPreviewCheckBox);
            this.Controls.Add(this.DemoteChildrenButton);
            this.Controls.Add(this.Comparison);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.visualisationUnitDropDown);
            this.Controls.Add(this.binaryUnitDropDown);
            this.Controls.Add(this.FileToSvg);
            this.Controls.Add(this.JsonToJson);
            this.Controls.Add(this.JsonToFile);
            this.Controls.Add(this.FileToJson);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.WriteProgressBar);
            this.Controls.Add(this.ReadProgressBar);
            this.Controls.Add(this.quickTestCheckbox1);
            this.Controls.Add(this.VersionLabel);
            this.Controls.Add(this.TestFeature);
            this.Controls.Add(this.quickTestCheckbox);
            this.Controls.Add(this.BatchTest);
            this.Controls.Add(this.Joints);
            this.Controls.Add(this.ObjectID);
            this.Controls.Add(this.ObjectIDLabel);
            this.Controls.Add(this.RoundValues);
            this.Controls.Add(this.profileButton);
            this.Controls.Add(this.RoundValLabel);
            this.Controls.Add(this.RoundPrecisionBar);
            this.Controls.Add(this.ReadGimData);
            this.Controls.Add(this.OpenConsole);
            this.Controls.Add(this.OutputSelect);
            this.Controls.Add(this.CadexOnly);
            this.Controls.Add(this.CompressButton);
            this.Controls.Add(this.FileName);
            this.Controls.Add(this.Run);
            this.Controls.Add(this.FileSelect);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "Form1";
            this.Text = "Gim Test";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.RoundPrecisionBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ObjectID)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ScaleInput)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button FileSelect;
        private System.Windows.Forms.OpenFileDialog FileSelector;
        private System.Windows.Forms.Button Run;
        private System.Windows.Forms.TextBox FileName;
        private System.Windows.Forms.CheckBox CompressButton;
        private System.Windows.Forms.CheckBox CadexOnly;
        private System.Windows.Forms.ComboBox OutputSelect;
        private System.Windows.Forms.FolderBrowserDialog FolderSelector;
        private System.Windows.Forms.Button OpenConsole;
        private System.Windows.Forms.Button ReadGimData;
        private System.Windows.Forms.TrackBar RoundPrecisionBar;
        private System.Windows.Forms.Label RoundValLabel;
        private System.Windows.Forms.Button profileButton;
        private System.Windows.Forms.Button RoundValues;
        private System.Windows.Forms.Label ObjectIDLabel;
        private System.Windows.Forms.NumericUpDown ObjectID;
        private System.Windows.Forms.Button Joints;
        private System.Windows.Forms.Button BatchTest;
        public System.Windows.Forms.CheckBox quickTestCheckbox;
        private System.Windows.Forms.Button WriteElemCounts;
        private System.Windows.Forms.Button TestFeature;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Label VersionLabel;
        private System.Windows.Forms.CheckBox quickTestCheckbox1;
        private System.Windows.Forms.ProgressBar ReadProgressBar;
        private System.Windows.Forms.ProgressBar WriteProgressBar;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button FileToSvg;
        private System.Windows.Forms.Button FileToJson;
        private System.Windows.Forms.Button JsonToFile;
        private System.Windows.Forms.Button JsonToJson;
        private System.Windows.Forms.ComboBox binaryUnitDropDown;
        private System.Windows.Forms.ComboBox visualisationUnitDropDown;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button Comparison;
        private System.Windows.Forms.Button DemoteChildrenButton;
        private System.Windows.Forms.NumericUpDown ScaleInput;
        private System.Windows.Forms.Label ScaleInputLabel;
        private System.Windows.Forms.CheckBox IfcPreviewCheckBox;
        private System.Windows.Forms.Button NetworkPayloadButton;
        private System.Windows.Forms.Label NetworkPayloadStatus;
        private System.Windows.Forms.TextBox NetworkPayloadLog;
    }
}

