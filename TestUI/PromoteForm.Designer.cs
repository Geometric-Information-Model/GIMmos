namespace TestUI
{
    partial class PromoteForm
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
            this.ObjectsToPromoteChecklist = new System.Windows.Forms.CheckedListBox();
            this.PromoteButton = new System.Windows.Forms.Button();
            this.CheckAllButton = new System.Windows.Forms.Button();
            this.CancelButton = new System.Windows.Forms.Button();
            this.ProjectIDInput = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.ProjectIDInput)).BeginInit();
            this.SuspendLayout();
            // 
            // ObjectsToPromoteChecklist
            // 
            this.ObjectsToPromoteChecklist.FormattingEnabled = true;
            this.ObjectsToPromoteChecklist.Location = new System.Drawing.Point(12, 20);
            this.ObjectsToPromoteChecklist.Name = "ObjectsToPromoteChecklist";
            this.ObjectsToPromoteChecklist.Size = new System.Drawing.Size(776, 312);
            this.ObjectsToPromoteChecklist.TabIndex = 0;
            this.ObjectsToPromoteChecklist.SelectedIndexChanged += new System.EventHandler(this.checkedListBox1_SelectedIndexChanged);
            // 
            // PromoteButton
            // 
            this.PromoteButton.Location = new System.Drawing.Point(561, 388);
            this.PromoteButton.Name = "PromoteButton";
            this.PromoteButton.Size = new System.Drawing.Size(227, 50);
            this.PromoteButton.TabIndex = 1;
            this.PromoteButton.Text = "Promote";
            this.PromoteButton.UseVisualStyleBackColor = true;
            this.PromoteButton.Click += new System.EventHandler(this.PromoteButton_Click);
            // 
            // CheckAllButton
            // 
            this.CheckAllButton.Location = new System.Drawing.Point(287, 388);
            this.CheckAllButton.Name = "CheckAllButton";
            this.CheckAllButton.Size = new System.Drawing.Size(227, 50);
            this.CheckAllButton.TabIndex = 2;
            this.CheckAllButton.Text = "Check All";
            this.CheckAllButton.UseVisualStyleBackColor = true;
            this.CheckAllButton.Click += new System.EventHandler(this.CheckAllButton_Click);
            // 
            // CancelButton
            // 
            this.CancelButton.Location = new System.Drawing.Point(12, 388);
            this.CancelButton.Name = "CancelButton";
            this.CancelButton.Size = new System.Drawing.Size(227, 50);
            this.CancelButton.TabIndex = 3;
            this.CancelButton.Text = "Cancel";
            this.CancelButton.UseVisualStyleBackColor = true;
            this.CancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // ProjectIDInput
            // 
            this.ProjectIDInput.Location = new System.Drawing.Point(371, 349);
            this.ProjectIDInput.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.ProjectIDInput.Name = "ProjectIDInput";
            this.ProjectIDInput.Size = new System.Drawing.Size(180, 31);
            this.ProjectIDInput.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(276, 351);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 25);
            this.label1.TabIndex = 5;
            this.label1.Text = "Project ID";
            // 
            // PromoteForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ProjectIDInput);
            this.Controls.Add(this.CancelButton);
            this.Controls.Add(this.CheckAllButton);
            this.Controls.Add(this.PromoteButton);
            this.Controls.Add(this.ObjectsToPromoteChecklist);
            this.Name = "PromoteForm";
            this.Text = "PromoteForm";
            ((System.ComponentModel.ISupportInitialize)(this.ProjectIDInput)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckedListBox ObjectsToPromoteChecklist;
        private System.Windows.Forms.Button PromoteButton;
        private System.Windows.Forms.Button CheckAllButton;
        private System.Windows.Forms.Button CancelButton;
        private System.Windows.Forms.NumericUpDown ProjectIDInput;
        private System.Windows.Forms.Label label1;
    }
}