using GIMcompress;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestUI
{
    public partial class DemoteForm : Form
    {
        Panel Panel;
        Dictionary<int, string> DemoteChildrenPaths = new();
        Dictionary<int, Button> DemoteButtons = new();
        Dictionary<int, TextBox> DemoteTextBoxes = new();

        Dictionary<int, CheckBox> PromoteDemoteCheckBoxes = new();

        Dictionary<int, NumericUpDown> PromoteIDInputs = new();
        Dictionary<int, int> PromoteIDs = new();

        // true for "is promotable/not yet promoted"
        Dictionary<int, bool> ChildIsPromotable = new();

        public Dictionary<int, Tuple<int?, int>> DemoteProjObjIDs = new();
        public Dictionary<int, string> FinalDemotionSelection = new();
        public Dictionary<int, int> FinalPromotionSelection = new();
        public int ProjectID = -1;
        public bool Aborted = true;
        public DemoteForm(JsonGimObject gimObject)
        {
            SuspendLayout();
            Panel = new Panel();
            Controls.Add(Panel);
            Panel.Location = new Point(20, 20);
            Panel.Name = "FlowPanel";
            Panel.Size = new Size(1550, 600);
            Panel.AutoScroll = true;

            {
                Label ChildNameLabel = new();
                Panel.Controls.Add(ChildNameLabel);
                ChildNameLabel.Location = new Point(180, + 10);
                ChildNameLabel.Size = new Size(350, 50);
                ChildNameLabel.Text = "Child Name";

                Label ChildIDOrFileLabel = new();
                Panel.Controls.Add(ChildIDOrFileLabel);
                ChildIDOrFileLabel.Location = new Point(580, 10);
                ChildIDOrFileLabel.Size = new Size(800, 50);
                ChildIDOrFileLabel.Text = "Child promote ID Or Demote Source File";

                Label checkBoxLabel = new();
                Panel.Controls.Add(checkBoxLabel);
                checkBoxLabel.Location = new Point(1400, 10);
                checkBoxLabel.Text = "Is Internal";

                Label findFileButtonLabel = new();

                Panel.Controls.Add(findFileButtonLabel);
                findFileButtonLabel.Location = new Point(12, 0);
                findFileButtonLabel.Size = new Size(150, 50);
                findFileButtonLabel.Text = "Select File";

            }


            int index = 1;
            HashSet<int> checkedElements = new();
            foreach (JsonGimElement child in gimObject.Children)
            {
                if (checkedElements.Contains(child.ID)) continue;
                checkedElements.Add(child.ID);
            }

            checkedElements = new();
            foreach (JsonGimElement child in gimObject.Children)
            {
                if (checkedElements.Contains(child.ID)) continue;
                checkedElements.Add(child.ID);
                int childIndex = child.ID;

                Label label = new();
                Panel.Controls.Add(label);
                label.Location = new Point(180, 70 * index + 10);
                label.Size = new Size(350, 50);
                label.Text = child.Name;
            

                CheckBox checkBox = new();
                Panel.Controls.Add(checkBox);
                checkBox.Location = new Point(1400, 70 * index + 10);
                PromoteDemoteCheckBoxes[childIndex] = checkBox;

                if (child.ObjectID != null)
                {
                    ChildIsPromotable[childIndex] = false;
                    DemoteChildrenPaths[childIndex] = "";
                    Button button = new();
                    TextBox textBox = new();
                    DemoteButtons[childIndex] = button;
                    DemoteTextBoxes[childIndex] = textBox;
                    DemoteProjObjIDs[childIndex] = new(child.ProjectID, (int)child.ObjectID);

                    Panel.Controls.Add(button);
                    button.Location = new Point(12, 70 * index);
                    button.Size = new Size(150, 50);
                    button.Text = "Select";

                    Panel.Controls.Add(textBox);
                    textBox.Location = new Point(580, 70 * index + 10);
                    textBox.Size = new Size(800, 50);
                    textBox.Text = "";

                    checkBox.Checked = false;

                    int ID = child.ID;
                    button.Click += (object sender, EventArgs e) =>
                    {
                        var dialog = new OpenFileDialog();
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            string filepath = dialog.FileName;
                            string extension = filepath.Split('.')[^1];
                            if(extension.ToLower() == "json")
                            {
                                PromoteDemoteCheckBoxes[ID].Checked = true;
                                DemoteTextBoxes[ID].Text = filepath;
                                DemoteChildrenPaths[ID] = filepath;
                            }
                        }
                    };

                }
                else
                {
                    checkBox.Checked = true;
                    ChildIsPromotable[childIndex] = true;
                    NumericUpDown promoteIDInput = new();
                    Panel.Controls.Add(promoteIDInput);
                    promoteIDInput.Maximum = 10000000;
                    promoteIDInput.Minimum = 0;
                    promoteIDInput.Value = childIndex;
                    PromoteIDInputs[childIndex] = promoteIDInput;
                    PromoteIDs[childIndex] = childIndex;
                    promoteIDInput.Location = new Point(580, 70 * index + 10);
                    promoteIDInput.Size = new Size(250, 50);

                    int ind = childIndex;
                    promoteIDInput.ValueChanged += (object sender, EventArgs e) =>
                    {
                        checkBox.Checked = false;

                        int currentValue = (int)promoteIDInput.Value;
                        int increment = (int)currentValue - PromoteIDs[ind];
                        int initialValue = (int)currentValue - increment;
                        if(increment == -1)
                        {
                            bool IDexists = false;

                            do
                            {
                                IDexists = false;
                                foreach (var keyval in PromoteIDs)
                                {
                                    var value = keyval.Value;
                                    var key = keyval.Key;
                                    if (key == ind) continue;
                                    if (value == currentValue)
                                    {
                                        IDexists = true;
                                        break;
                                    }
                                }
                                if (currentValue == 0) break;
                                if (IDexists && currentValue > 0) currentValue--;
                            } while(IDexists && currentValue >= 0);
                            if (IDexists)
                            {
                                currentValue = initialValue;
                            }
                        }

                        else
                        {
                            bool IDexists = false;

                            do
                            {
                                IDexists = false;
                                foreach (var keyval in PromoteIDs)
                                {
                                    var value = keyval.Value;
                                    var key = keyval.Key;
                                    if (key == ind) continue;
                                    if (value == currentValue)
                                    {
                                        IDexists = true;
                                        break;
                                    }
                                }
                                if (IDexists) currentValue++;
                            } while (IDexists);
                            if (IDexists)
                            {
                                currentValue = initialValue;
                            }
                        }

                        PromoteIDs[ind] = (int)currentValue;
                        promoteIDInput.Value = currentValue;

                    };
                }
                index++;
            }

            ResumeLayout(false);
            InitializeComponent();
        }

        private void AutoFindFilesButton_Click(object sender, EventArgs e)
        {
            foreach(var keyval in DemoteProjObjIDs)
            {
                var ID = keyval.Key;
                var val = keyval.Value;
                if( val.Item1 != null)
                {
                    int projectID = (int)val.Item1;
                    if(projectID > 0)
                    {
                        int objectID = val.Item2;
                        string fileName = projectID + "-" + objectID + ".json";
                        string tempPath = GIMtransfer.FileAccess.GetTempFolder();
                        string[] files = Directory.GetFiles(tempPath, fileName, SearchOption.AllDirectories);
                        if(files.Length > 0)
                        {
                            PromoteDemoteCheckBoxes[ID].Checked = true;
                            DemoteTextBoxes[ID].Text = files[0];
                            DemoteChildrenPaths[ID] = files[0];
                        }
                    }
                }
            }
        }

        private void DemoteButton_Click(object sender, EventArgs e)
        {
            Aborted = false;
            foreach(var keyval in DemoteChildrenPaths)
            {
                int ID = keyval.Key;
                string path = keyval.Value;

                if (PromoteDemoteCheckBoxes[ID].Checked)
                    FinalDemotionSelection.Add(ID, path);
            }
            foreach(var keyval in PromoteIDs)
            {
                int ID = keyval.Key;
                int promoteID = keyval.Value;
                if (!PromoteDemoteCheckBoxes[ID].Checked)
                    FinalPromotionSelection.Add(ID, promoteID);
            }
            ProjectID = (int)ProjectIDInput.Value;
            Close();
        }
    }
}
