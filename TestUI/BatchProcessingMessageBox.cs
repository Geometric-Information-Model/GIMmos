using System;
using System.Windows.Forms;

namespace TestUI
{
	public partial class BatchProcessingMessageBox : Form
    {

        public BatchProcessingMessageBox()
        {
            InitializeComponent();

            //foreach (string error in Form1.errorsEncountered) 
            //{
            //    textBox1.Text += error;
            //}

            this.Text = "Batch Testing Results";

            if (Form1.errorsEncountered.Count.Equals(0))
            {
                textBox1.Text = "Congratulations! There were no errors encountered while running the batch test.";
                label1.Text = Form1.message;
            }
            else 
            {
                textBox1.Text = String.Join(Environment.NewLine, Form1.errorsEncountered);
                label1.Text = Form1.message;
            }

             

        }

        private void BatchProcessingMessageBox_Load(object sender, EventArgs e)
        {
            //textBox1.Text = Form1.errorsEncountered[0];
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            
        }
    }
}
