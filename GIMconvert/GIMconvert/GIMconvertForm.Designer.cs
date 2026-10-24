namespace GIMconvert
{
    partial class GIMconvert
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
            this.FolderSelect = new System.Windows.Forms.Button();
            this.FolderDirectoryText = new System.Windows.Forms.TextBox();
            this.ConvertRun = new System.Windows.Forms.Button();
            this.InputExtensionSelect = new System.Windows.Forms.ComboBox();
            this.FileInputLabel = new System.Windows.Forms.Label();
            this.FileOutputText = new System.Windows.Forms.Label();
            this.OutputExtensionSelect = new System.Windows.Forms.ComboBox();
            this.TextArea = new System.Windows.Forms.RichTextBox();
            this.CurrentFile = new System.Windows.Forms.TextBox();
            this.ConvertAllExtensions = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // FolderSelect
            // 
            this.FolderSelect.Location = new System.Drawing.Point(12, 12);
            this.FolderSelect.Name = "FolderSelect";
            this.FolderSelect.Size = new System.Drawing.Size(153, 34);
            this.FolderSelect.TabIndex = 0;
            this.FolderSelect.Text = "Select Folder";
            this.FolderSelect.UseVisualStyleBackColor = true;
            this.FolderSelect.Click += new System.EventHandler(this.FolderSelect_Click);
            // 
            // FolderDirectoryText
            // 
            this.FolderDirectoryText.Location = new System.Drawing.Point(12, 52);
            this.FolderDirectoryText.Name = "FolderDirectoryText";
            this.FolderDirectoryText.Size = new System.Drawing.Size(880, 31);
            this.FolderDirectoryText.TabIndex = 1;
            // 
            // ConvertRun
            // 
            this.ConvertRun.Location = new System.Drawing.Point(780, 12);
            this.ConvertRun.Name = "ConvertRun";
            this.ConvertRun.Size = new System.Drawing.Size(112, 34);
            this.ConvertRun.TabIndex = 2;
            this.ConvertRun.Text = "Convert";
            this.ConvertRun.UseVisualStyleBackColor = true;
            this.ConvertRun.Click += new System.EventHandler(this.ConvertRun_Click);
            // 
            // InputExtensionSelect
            // 
            this.InputExtensionSelect.FormattingEnabled = true;
            this.InputExtensionSelect.Location = new System.Drawing.Point(12, 114);
            this.InputExtensionSelect.Name = "InputExtensionSelect";
            this.InputExtensionSelect.Size = new System.Drawing.Size(182, 33);
            this.InputExtensionSelect.TabIndex = 3;
            // 
            // FileInputLabel
            // 
            this.FileInputLabel.AutoSize = true;
            this.FileInputLabel.Location = new System.Drawing.Point(12, 86);
            this.FileInputLabel.Name = "FileInputLabel";
            this.FileInputLabel.Size = new System.Drawing.Size(165, 25);
            this.FileInputLabel.TabIndex = 4;
            this.FileInputLabel.Text = "File Input Extension";
            // 
            // FileOutputText
            // 
            this.FileOutputText.AutoSize = true;
            this.FileOutputText.Location = new System.Drawing.Point(712, 86);
            this.FileOutputText.Name = "FileOutputText";
            this.FileOutputText.Size = new System.Drawing.Size(180, 25);
            this.FileOutputText.TabIndex = 5;
            this.FileOutputText.Text = "File Output Extension";
            // 
            // OutputExtensionSelect
            // 
            this.OutputExtensionSelect.FormattingEnabled = true;
            this.OutputExtensionSelect.Location = new System.Drawing.Point(710, 114);
            this.OutputExtensionSelect.Name = "OutputExtensionSelect";
            this.OutputExtensionSelect.Size = new System.Drawing.Size(182, 33);
            this.OutputExtensionSelect.TabIndex = 6;
            // 
            // TextArea
            // 
            this.TextArea.Location = new System.Drawing.Point(12, 153);
            this.TextArea.Name = "TextArea";
            this.TextArea.Size = new System.Drawing.Size(880, 327);
            this.TextArea.TabIndex = 7;
            this.TextArea.Text = "";
            // 
            // CurrentFile
            // 
            this.CurrentFile.Location = new System.Drawing.Point(12, 486);
            this.CurrentFile.Name = "CurrentFile";
            this.CurrentFile.Size = new System.Drawing.Size(880, 31);
            this.CurrentFile.TabIndex = 8;
            // 
            // ConvertAllExtensions
            // 
            this.ConvertAllExtensions.AutoSize = true;
            this.ConvertAllExtensions.Location = new System.Drawing.Point(200, 114);
            this.ConvertAllExtensions.Name = "ConvertAllExtensions";
            this.ConvertAllExtensions.Size = new System.Drawing.Size(213, 29);
            this.ConvertAllExtensions.TabIndex = 9;
            this.ConvertAllExtensions.Text = "Convert All Extensions";
            this.ConvertAllExtensions.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(904, 529);
            this.Controls.Add(this.ConvertAllExtensions);
            this.Controls.Add(this.CurrentFile);
            this.Controls.Add(this.TextArea);
            this.Controls.Add(this.OutputExtensionSelect);
            this.Controls.Add(this.FileOutputText);
            this.Controls.Add(this.FileInputLabel);
            this.Controls.Add(this.InputExtensionSelect);
            this.Controls.Add(this.ConvertRun);
            this.Controls.Add(this.FolderDirectoryText);
            this.Controls.Add(this.FolderSelect);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Button FolderSelect;
        private TextBox FolderDirectoryText;
        private Button ConvertRun;
        private ComboBox InputExtensionSelect;
        private Label FileInputLabel;
        private Label FileOutputText;
        private ComboBox OutputExtensionSelect;
        private RichTextBox TextArea;
        private TextBox CurrentFile;
        private CheckBox ConvertAllExtensions;
    }
}