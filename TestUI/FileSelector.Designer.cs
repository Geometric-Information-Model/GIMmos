
namespace TestUI
{
    partial class FileSelector
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
            FileTypeSelect = new System.Windows.Forms.ComboBox();
            Confirm = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // FileTypeSelect
            // 
            FileTypeSelect.FormattingEnabled = true;
            FileTypeSelect.Location = new System.Drawing.Point(74, 55);
            FileTypeSelect.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            FileTypeSelect.Name = "FileTypeSelect";
            FileTypeSelect.Size = new System.Drawing.Size(171, 33);
            FileTypeSelect.TabIndex = 0;
            FileTypeSelect.SelectedIndexChanged += FileTypeSelect_SelectedIndexChanged;
            // 
            // Confirm
            // 
            Confirm.Location = new System.Drawing.Point(74, 135);
            Confirm.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            Confirm.Name = "Confirm";
            Confirm.Size = new System.Drawing.Size(173, 42);
            Confirm.TabIndex = 1;
            Confirm.Text = "Confirm";
            Confirm.UseVisualStyleBackColor = true;
            Confirm.Click += Confirm_Click;
            // 
            // FileSelector
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(326, 242);
            Controls.Add(Confirm);
            Controls.Add(FileTypeSelect);
            Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            Name = "FileSelector";
            Text = "FileSelector";
            Load += FileSelector_Load;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ComboBox FileTypeSelect;
        private System.Windows.Forms.Button Confirm;
    }
}