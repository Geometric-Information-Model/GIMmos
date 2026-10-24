using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace TestUI
{
    public partial class FileSelector : Form
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string fileType { get; set; }

        public FileSelector(List<string> options)
        {
            InitializeComponent();  
            foreach (string s in options)
                FileTypeSelect.Items.Add(s);
            fileType = (string)FileTypeSelect.SelectedItem;
        }

        private void Confirm_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void FileTypeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            fileType = (string)FileTypeSelect.SelectedItem;
        }

        private void FileSelector_Load(object sender, EventArgs e)
        {

        }
    }
}
