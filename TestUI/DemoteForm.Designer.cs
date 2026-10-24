namespace TestUI
{
    partial class DemoteForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.AutoFindFilesButton = new System.Windows.Forms.Button();
            this.DemoteButton = new System.Windows.Forms.Button();
            this.ProjectIDInput = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.ProjectIDInput)).BeginInit();
            this.SuspendLayout();
            // 
            // AutoFindFilesButton
            // 
            this.AutoFindFilesButton.Location = new System.Drawing.Point(383, 630);
            this.AutoFindFilesButton.Name = "AutoFindFilesButton";
            this.AutoFindFilesButton.Size = new System.Drawing.Size(207, 52);
            this.AutoFindFilesButton.TabIndex = 0;
            this.AutoFindFilesButton.Text = "Automatically find files";
            this.AutoFindFilesButton.UseVisualStyleBackColor = true;
            this.AutoFindFilesButton.Click += new System.EventHandler(this.AutoFindFilesButton_Click);
            // 
            // DemoteButton
            // 
            this.DemoteButton.Location = new System.Drawing.Point(985, 630);
            this.DemoteButton.Name = "DemoteButton";
            this.DemoteButton.Size = new System.Drawing.Size(207, 52);
            this.DemoteButton.TabIndex = 1;
            this.DemoteButton.Text = "Promote/Demote";
            this.DemoteButton.UseVisualStyleBackColor = true;
            this.DemoteButton.Click += new System.EventHandler(this.DemoteButton_Click);
            // 
            // ProjectIDInput
            // 
            this.ProjectIDInput.Location = new System.Drawing.Point(748, 642);
            this.ProjectIDInput.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.ProjectIDInput.Name = "ProjectIDInput";
            this.ProjectIDInput.Size = new System.Drawing.Size(180, 31);
            this.ProjectIDInput.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(653, 644);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 25);
            this.label1.TabIndex = 3;
            this.label1.Text = "Project ID";
            // 
            // DemoteForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1628, 694);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ProjectIDInput);
            this.Controls.Add(this.DemoteButton);
            this.Controls.Add(this.AutoFindFilesButton);
            this.Name = "DemoteForm";
            this.Text = "DemoteForm";
            ((System.ComponentModel.ISupportInitialize)(this.ProjectIDInput)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button AutoFindFilesButton;
        private System.Windows.Forms.Button DemoteButton;
        private System.Windows.Forms.NumericUpDown ProjectIDInput;
        private System.Windows.Forms.Label label1;
    }
}