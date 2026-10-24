using GIMcompress;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestUI
{
    public partial class PromoteForm : Form
    {

        public HashSet<int> SelectedIndices = new();
        public int ProjectId = 0;
        public bool Aborted = true;
        public PromoteForm(JsonGimObject gimObject)
        {
            InitializeComponent();

            HashSet<int> uniqueChildren = new HashSet<int>();
            foreach(var obj in gimObject.Children)
            {
                bool exists = !uniqueChildren.Add(obj.ID);
                if (exists) continue;
                ObjectsToPromoteChecklist.Items.Add(obj.ID + "-" + obj.Name);
            }

        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void PromoteButton_Click(object sender, EventArgs e)
        {
            Aborted = false;
            for (int i = 0; i < ObjectsToPromoteChecklist.Items.Count; i++) 
            {
                bool isChecked = ObjectsToPromoteChecklist.GetItemChecked(i);
                if (isChecked)
                {
                    SelectedIndices.Add(i);
                }
            }
            ProjectId = (int)ProjectIDInput.Value;
            this.Close();
        }

        private void CheckAllButton_Click(object sender, EventArgs e)
        {
            bool allTrue = ObjectsToPromoteChecklist.CheckedItems.Count == ObjectsToPromoteChecklist.Items.Count;

            if (allTrue)
            {
                for (int i = 0; i < ObjectsToPromoteChecklist.Items.Count; i++) 
                {
                    ObjectsToPromoteChecklist.SetItemChecked(i, false);
                }
            }

            else
            {
                for (int i = 0; i < ObjectsToPromoteChecklist.Items.Count; i++)
                {
                    ObjectsToPromoteChecklist.SetItemChecked(i, true);
                }
            }
            
        }
    }
}
