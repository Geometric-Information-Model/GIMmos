using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GIMconvert
{
    public partial class GIMconvert : Form
    {
        string SelectedDirectory = "";
        FolderBrowserDialog FileSelector = new();
        public GIMconvert()
        {
            InitializeComponent();
            string[] currentExtensions = new string[] { ".bin", ".step", ".glb", ".ifc" };
            OutputExtensionSelect.Items.AddRange(currentExtensions);
            OutputExtensionSelect.Text = ".glb";
            InputExtensionSelect.Items.AddRange(currentExtensions);
            InputExtensionSelect.Text = ".bin";
            ConvertRun.Enabled = false;
            AllocConsole();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private void FolderSelect_Click(object sender, EventArgs e)
        {
            if (FileSelector.ShowDialog() == DialogResult.OK)             // open file dialog
            {
                SelectedDirectory = FileSelector.SelectedPath;
                FolderDirectoryText.Text = SelectedDirectory;
                ConvertRun.Enabled = true;
            }
        }

        private void ConvertRun_Click(object sender, EventArgs e)
        {
            string[] files = Directory.GetFiles(SelectedDirectory,"*" + InputExtensionSelect.Text, SearchOption.AllDirectories);
            int index = 0;
            bool verbose = false;
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<string> failedFiles = new();
            foreach (string file in files)
            {
                CurrentFile.Text = file;
                index++;
                try
                {
                    var gimData = GIMtransfer.FileAccess.ReadToGimData(file, file.Split('.')[^1]);

                    string fileOutputName = file.Split("\\")[^1];
                    fileOutputName = fileOutputName.Split(".")[0];

                    int fileFolderNumber = (int)Math.Floor((double)int.Parse((fileOutputName.Split('_')[0])) / 10000);

                    string finalFilePath = fileFolderNumber + "\\" + fileOutputName;

                    GIMtransfer.FileAccess.SharpGLTFWriteGLB(gimData, finalFilePath);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("File " + file + " failed: " + ex.Message);
                    Console.WriteLine("Skipping...");
                    failedFiles.Add(file);
                }


                if(index % 1000 == 1)
                {
                    long milliseconds = stopwatch.ElapsedMilliseconds;

                    Console.WriteLine("Writing file " + file);
                    Console.WriteLine("Finished file " + index + " out of " + files.Length);
                    Console.WriteLine("Total Elapsed time: " + (milliseconds / 1000) + " seconds = "
                        + (milliseconds / 60000) + " minutes = " + (milliseconds / 3600000) + " hours.");

                    long averageTime = milliseconds / index;
                    Console.WriteLine("Average File time: " + (averageTime / 1000) + " seconds = "
                        + (averageTime / 60000) + " minutes = " + (averageTime / 3600000) + " hours.");
                    long timeLeft = averageTime * (files.Length - index);
                    Console.WriteLine("Estimated Time Left: "  + (timeLeft / 60000) + " minutes = " 
                        + (timeLeft / 3600000) + " hours.");
                }

            }

            Console.WriteLine("Finished reading files, " + failedFiles.Count + " files failed. Below are the files that didn't convert");
            foreach(string fileName in failedFiles)
            {
                Console.WriteLine(fileName);
            }
        }
    }
}