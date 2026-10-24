using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using GIMtransfer;
using GIMcompress;


namespace TestUI
{
    public partial class Form2 : Form
    {
        public static List<string> errorsEncountered = new List<string>();
        //public static string message;
        public Form2()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {



            string prefix = null;
            List<string> excludedFormatList = new List<string>();
            //List<string> exportTo = new List<string>();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                prefix = folderBrowserDialog1.SelectedPath;
            }
            else if (folderBrowserDialog1.ShowDialog() == DialogResult.Cancel)
            {
                return;
            }
            foreach (string s in checkedListBox1.CheckedItems) 
            {
                excludedFormatList.Add(s);
            }

            int x = 0;

            string[] acceptedFormats = new string[] { "3dxml", "3mf", "3ds", "sat", "sab", "brep", "catpart",
                "catproduct", "dae", "prt.creo", "asm.creo", "dxf", "dwg", "fbx", "gltf", "glb", "ifc", "igs",
                "iges", "jt", "prt.NX", "obj", "x_t", "x_b", "xmt_txt", "xmt_bin", "xmb_txt", "xmb_bin", "3dm",
                "par", "psm", "asm.SLDE", "sldprt", "sldasm", "ply", "prc", "step", "stp", "stl", "u3d", "wrl",
                "x3d" , "json" };
            List<string> files = GIMtransfer.FileAccess.GetReadableFilesInDirectory(prefix, acceptedFormats);



            List<string> exportFormats = new List<string>() {"bin","sat", "brep", "dae", "dxf", "fbx", "gltf", "ifc",
            "iges",            "jt",
            "obj",         "x_t",
            "3dm",         "stp",
            "stl",         "usd",
            "wrl",         "x3d",
            "json"};

            foreach (string s in excludedFormatList) 
            {
                for (int i = 0; i < exportFormats.Count; i++) 
                {
                    if (exportFormats[i] == s) 
                    {
                        exportFormats.Remove(s);
                    }
                }
            }
            int fileCount = files.Count;
            int typeCount = exportFormats.Count;

            //Random random = new Random();

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
                    Form1.errorsEncountered.Add(errorMessage);
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
                            GIMtransfer.FileAccess.WriteFromGimData(gimData, exportfileType, exportedName, true);
                        }
                        catch (Exception ex)
                        {
                            string errorMessage = "File " + exportedNameWithoutExtension + extension +
                                " could not be exported to format " + exportfileType;
                            errorMessage += ", Encountered Exception \n" + ex.StackTrace;
                            Form1.errorsEncountered.Add(errorMessage);
                            continue;
                        }
                    }
                    else
                    {
                        try
                        {
                            GIMtransfer.FileAccess.WriteFromGimData(gimData, exportfileType, exportedName, true);
                        }
                        catch (Exception ex)
                        {
                            string errorMessage = "File " + exportedNameWithoutExtension + extension +
                                " could not be exported to format " + exportfileType;
                            errorMessage += ", Encountered Exception \n" + ex.StackTrace;
                            Form1.errorsEncountered.Add(errorMessage);
                            continue;
                        }
                    }

                }

            }
            Form1.message = Form1.errorsEncountered.Count + " out of " + (fileCount * typeCount) + " Failed";


            BatchProcessingMessageBox form = new BatchProcessingMessageBox();


            form.ShowDialog();

            Form1.errorsEncountered.Clear();

            fileCount = 0;

            exportFormats = new List<string>() {"bin","sat", "brep", "dae", "dxf", "fbx", "gltf", "ifc",
            "iges",            "jt",
            "obj",         "x_t",
            "3dm",         "stp",
            "stl",         "usd",
            "wrl",         "x3d",
            "json"};
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }
    }
}
