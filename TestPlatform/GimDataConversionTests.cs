using cadex;
using GIMcompress;
using GIMtransfer;
using GIMtransfer.BrepToPoly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestHelpers;
using GimUtilities;
using NUnit.Framework.Legacy;

// This should be set to the amount you want for your proccessor.
// I have set this to 3 so my pc is still useable when i'm running tests.
[assembly: LevelOfParallelism(3)]

namespace TestPlatform
{

	public class CadexTests
	{
		[SetUpAttribute]
		public void Setup()
		{
		}

		private readonly TestGimDatas testGimDatas = new TestGimDatas("../../../../../../CAD Files/");

		[DatapointSource]
		private readonly string[] testFileNames = TestGimDatas.TestFiles;


//		[Test, Theory, Parallelizable(ParallelScope.Children)]
//		public void CompleteFileDoubleCadexTest(string filePath)
//		{
//#if DEBUG
//			IReadOnlyGimData gd = testGimDatas.GetQuickAndUnsafe(filePath);
//			GimLogger.Info("Write from gimdata.");
//			ModelData_Model model = testGimDatas.RunTest<IReadOnlyGimData, ModelData_Model>(GIMtransfer.FileAccess.ReadFromGimData, gd);
//			GimLogger.Info("Read to gimdata. (gdb)");
//			IReadOnlyGimData gdb = testGimDatas.RunTest<ModelData_Model, IReadOnlyGimData>(GIMtransfer.FileAccess.ReadToGimData, model);

//			GimLogger.Info("Assert correctness gd");
//			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gd);
//			GimLogger.Info("Assert correctness gdb");
//			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gdb);
//			GimLogger.Info("Compare gd and gdb");
//			ClassicAssert.IsTrue(TestComparison.CompareGimDatas(gd, gdb));
//#endif
//		}

//		[Test, Theory, Parallelizable(ParallelScope.All)]
//		public void ProgressStatusTest(string filePath)
//		{
//#if DEBUG
//			IReadOnlyGimData gd = testGimDatas.GetQuickAndUnsafe(filePath);
//			GimProgressStatus progressStatus = new GimProgressStatus();

//			Task<ModelData_Model> modelTask = new Task<ModelData_Model>(() => (GIMtransfer.FileAccess.ReadFromGimData(gd, GIMtransfer.FileAccess.GetTempFolder(), progressStatus)));
//			modelTask.Start();

//			// Wait to make sure the progress is started;
//			while (!progressStatus.IsStarted)
//			{
//				// Do nothing
//			}

//			int i = 0;
//			// When it's started start spamming time esitmates
//			while (!progressStatus.ThinksItsCompleted && !modelTask.IsCompleted)
//			{
//				GimLogger.Info(progressStatus.PercentageDone.ToString() + "% done!");
//				GimLogger.Info(progressStatus.GuessedTimeLeft.ToString() + " time left.");

//				if (i < 14)
//					Thread.Sleep(500);
//				else
//					Thread.Sleep(5000);

//				i++;
//			}

//			modelTask.Wait();
//			gd.WriteElementTypeCount();
//			ClassicAssert.AreEqual(100.0f, progressStatus.PercentageDone);
//			ClassicAssert.IsTrue(progressStatus.ThinksItsCompleted);
//#endif
//		}

//		[Test, Theory]
//		public void CompleteFileDoubleCadexTestMultithreaded(string filePath)
//		{
//#if DEBUG
//			IReadOnlyGimData gd = testGimDatas.GetQuickAndUnsafe(filePath);
//			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gd);

//			GimLogger.Info("Write from gimdata.");
//			ModelData_Model model = testGimDatas.RunTest<IReadOnlyGimData, ModelData_Model>(GIMtransfer.FileAccess.ReadFromGimData, gd);
//			GimLogger.Info("Read to gimdata. (gdb)");
//			GimData gdb = GIMtransfer.FileAccess.ReadToGimData(model, false, true);

//			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gdb);
//#endif
//		}

//		[Test, Theory, Parallelizable(ParallelScope.Children)]
//		public void CompleteFileTripleCadexTest(string filePath)
//		{
//#if DEBUG
//			IReadOnlyGimData gda = testGimDatas.GetQuickAndUnsafe(filePath);
//			GimLogger.Info("Write from gimdata.");
//			ModelData_Model modela = testGimDatas.RunTest<IReadOnlyGimData, ModelData_Model>(GIMtransfer.FileAccess.ReadFromGimData, gda);
//			GimLogger.Info("Read to gimdata. (gdb)");

//			IReadOnlyGimData gdb = testGimDatas.RunTest<ModelData_Model, IReadOnlyGimData>(GIMtransfer.FileAccess.ReadToGimData, modela);

//			GimLogger.Info("Write from gimdata.");
//			ModelData_Model modelb = testGimDatas.RunTest<IReadOnlyGimData, ModelData_Model>(GIMtransfer.FileAccess.ReadFromGimData, gdb);
//			GimLogger.Info("Read to gimdata. (gdc)");

//			IReadOnlyGimData gdc = testGimDatas.RunTest<ModelData_Model, IReadOnlyGimData>(GIMtransfer.FileAccess.ReadToGimData, modelb);

//			GimLogger.Info("Assert correctness gdb");
//			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gdb);
//			GimLogger.Info("Assert correctness gdc");
//			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gdc);
//			GimLogger.Info("Compare gda and gdc");
//			ClassicAssert.IsTrue(TestComparison.CompareGimDatas(gdb, gdc));
//#endif
//		}

		//[Test, Theory, Parallelizable(ParallelScope.Children), CancelAfter(1000 * 60 * 5)]
		//public void ConvertToPolyTroughGimData(string filePath)
		//{
		//	IReadOnlyGimData gda = testGimDatas.GetQuickAndUnsafe(filePath);
		//	gda.WriteElementTypeCount();
		//	GimLogger.Info("Read to Poly GimData. (gda)");
		//	GimData gimDataPoly = BrepToPoly.BrepGimDataToPoly(gda);
		//	gimDataPoly.WriteElementTypeCount();
		//	testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gimDataPoly);
		//}

//		[Test, Theory, CancelAfter(1000 * 60 * 5)]
//		public void ConvertToPoly(string filePath)
//		{
//#if DEBUG
//			// Error for files that crash verry badly
//			// Roll cage kills my pc when converting it to a polygon
//			if (filePath == "Roll Cage.STEP"
//				|| filePath == "index_controller.stp")
//			{
//				GimLogger.Error(filePath + " kills my pc when converting it to a polygon.");
//				ClassicAssert.Fail();
//			}
//			// Files that are to heavy to test for converting to a polygon.
//			if (filePath == "Hidravlicna_stiskalnica_315t oprema.step"
//				|| filePath == "kanaalplaat-element-vbi-isolatieplaatvloer-200-rc_6_5.ifc"
//				|| filePath == "219009 Kerstrooslaan te Eindhoven LOD300.ifc")
//			{
//				GimLogger.Warn(filePath + "is to heavy to test for converting to a polygon.");
//				ClassicAssert.Fail();
//			}
//			GimData gd_brep = TestReadFunctions.ReadToGimData("../../../../../../CAD Files/" + filePath, true);
//			gd_brep.WriteElementTypeCount();
//			GimData gd_poly = GIMtransfer.FileAccess.GimDataToPoly(gd_brep);
//			ClassicAssert.IsNotNull(gd_poly);
//#endif
//		}

//		[Test, Theory, CancelAfter(1000 * 60 * 5)]
//		public void ConvertToPolyCadexOnly(string filePath)
//		{
//			string filetype = Path.GetExtension(filePath)[1..];
//			string pathFileName = Path.GetFullPath("../../../../../../CAD Files/" + filePath, Directory.GetCurrentDirectory());
//			GIMtransfer.FileAccess.Start();
//			ModelData_Model model = CadexModel.Read(pathFileName, filetype);
//			model = CadexModel.CadexModelToPoly(model);
//		}

//		[Test, Theory, Parallelizable(ParallelScope.Children)]
//		public void CompleteFileDoubleCadexTestWithCompress(string filePath)
//		{
//#if DEBUG
//			GimData gd = testGimDatas.Get(filePath);
//			gd = Twins.LinkTwins(gd);
//			ModelData_Model model = GIMtransfer.FileAccess.ReadFromGimData(gd);

//			gd = GIMtransfer.FileAccess.ReadToGimData(model, false);
//			gd = Twins.LinkTwins(gd);

//			GimData gdb = GIMtransfer.FileAccess.ReadToGimData(model, false);
//			gdb = Twins.LinkTwins(gdb);

//			GimLogger.Info("Compare gd and gdb");
//			ClassicAssert.IsTrue(TestComparison.CompareGimDatas(gd, gdb));
//#endif
//		}

//		[Test, Theory, Parallelizable(ParallelScope.Children)]
//		public void CompareLinkTwinsOutputAndInput(string filePath)
//		{
//#if DEBUG
//			IReadOnlyGimData gd = testGimDatas.GetQuickAndUnsafe(filePath);
//			GimData gdb = Twins.LinkTwins(gd.Clone());
//			ModelData_Model model = testGimDatas.RunTest<IReadOnlyGimData, ModelData_Model>(GIMtransfer.FileAccess.ReadFromGimData, gdb);

//			gdb = GIMtransfer.FileAccess.ReadToGimData(model, false);

//			GimLogger.Info("Compare gd and gdc");
//			ClassicAssert.IsTrue(TestComparison.CompareGimDatas(gd, gdb));
//#endif
//		}

		[Test, Theory, Parallelizable(ParallelScope.Children)]
		public void GimDataIsCorrect(string filePath)
		{
			IReadOnlyGimData gd = testGimDatas.GetQuickAndUnsafe(filePath);

			GimLogger.Info("Assert correctness (gd)");
            GimUtilities.GimLogger.Info("Assert correctness (gd)");
			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gd);
			gd.WriteElementTypeCount();
		}

		[Test, Theory, Parallelizable(ParallelScope.Children)]
		public void GimDataIsCorrectAfterLinkingTwins(string filePath)
		{
			GimData gd = testGimDatas.Get(filePath);
			gd = Twins.LinkTwins(gd);

            GimUtilities.GimLogger.Info("Assert correctness (gd)");
			testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gd);
		}

		[Test, Theory, Parallelizable(ParallelScope.Children)]
		public void LinkLocalsTest(string filePath)
		{
			GimData gdb = testGimDatas.Get(filePath);
			ClassicAssert.IsNotNull(gdb);

			// For now only links twins B
			gdb = Twins.LinkTwins(gdb);

			// Read second time A
			GimData gd = testGimDatas.Get(filePath);
			ClassicAssert.IsNotNull(gd);

			// Links twins A
			gd = Twins.LinkTwins(gd);

			// Compare
			ClassicAssert.IsTrue(TestComparison.CompareGimDatas(gd, gdb));
		}

		//// CancelAfter after 5 minutes.
		//[Test, Theory, CancelAfter(1000 * 60 * 5)]
		//public void GimDataToGmshTest(string filePath)
		//{
		//	GimData gd = testGimDatas.Get(filePath);
		//	Assert.IsNotNull(gd);

		//	GimData gdb = GimGMSH.GimDataAddPolygonRepresentation(gd);

		//	Assert.IsNotNull(gdb);
		//	testGimDatas.RunTest<IReadOnlyGimData, bool>(CorrectnessCheck.CheckCorrectnessGimData, gd);

		//	// Uncomment this to see the polygon in action!
		//	//GIMtransfer.FileAccess.WriteFromGimData(gdb, "obj", filePath, false);
		//}

		[Test, Theory]
		public void FileTypeTest(Industry industry)
		{
			// All filetypes for a industry.
			IEnumerable<KeyValuePair<string, FileTypeProperties>> allFileTypesForIndustry = GIMtransfer.FileAccess.FileTypes.Where(pair => pair.Value.industries.Contains(industry));
			foreach (KeyValuePair<string, FileTypeProperties> filetype in allFileTypesForIndustry)
			{
				GimUtilities.GimLogger.Info(filetype.Key);
			}

			// Get all filetypes we can write for a certain industry
			IEnumerable<KeyValuePair<string, FileTypeProperties>> write = GIMtransfer.FileAccess.FileTypes.Where(pair => pair.Value.industries.Contains(industry) && pair.Value.output);
			foreach (KeyValuePair<string, FileTypeProperties> filetype in write)
			{
                GimUtilities.GimLogger.Info(filetype.Key);
			}

			// Get all filetypes we can read for a certain industry
			IEnumerable<KeyValuePair<string, FileTypeProperties>> read = GIMtransfer.FileAccess.FileTypes.Where(pair => pair.Value.industries.Contains(industry) && pair.Value.input);
			foreach (KeyValuePair<string, FileTypeProperties> filetype in read)
			{
                GimUtilities.GimLogger.Info(filetype.Key);
			}

		}
	}
}