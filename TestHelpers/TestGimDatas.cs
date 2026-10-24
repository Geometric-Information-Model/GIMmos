using GIMcompress;
using System;
using System.Collections.Generic;
using GimUtilities;

namespace TestHelpers
{
	/// <summary>
	/// This class is a Helper class to cache all GimDatas so we can quicker test them with our internal functions.
	/// This means that all GimDatas that are read trough this class are cached into memory.
	/// Because of this are testing process is very memory intensive.
	/// </summary>
	public class TestGimDatas
	{
		/// <summary>
		/// The prefix path to find the CAD Files in.
		/// </summary>
		private readonly string prefix;

		/// <summary>
		/// The internal dictionary that stores the GimDatas with their filename as key.
		/// </summary>
		private readonly Dictionary<string, GimData> testGimDatasInternal;

		/// <summary>
		/// Lock objects that makes the TestGimDatas class thread-safe. 
		/// </summary>
		private readonly Dictionary<string, object> testGimDatasInternalLocks;

		/// <summary>
		/// Static hard-coded list of files to look for and test with in the CAD files folder.
		/// </summary>
		public static readonly string[] TestFiles = new string[47]
		{
			"9050 ABB_IRB 6640 180-255.dwg",
			"ROBOT ARM.STEP",
			"wooden watch tower2 (2).fbx",
			"Residential Buildings 001.fbx",
			//"TEST_Case_2.ifc",

			//"geometry (2).3ds", // Has textures
			"geometry.3ds", // Has textures

			"171-4.bin",

			"0000-0000 Master Assembly.stp", 

            "WireSphere.step",
			"HalfSphere.step",
			"Test cube.step",
			"omni_wheel_roller2_2.step",
			"Omni_wheel_without_overlappingObjects.step",
			"omni_wheel.stp",
			"MachineKlein.step",
			"ltc-tr-c10-d1-1_0.stp",
			"Roll Cage.STEP",
			"Short Bushing.step",
			"Short Bushing 2.step",
			"Cymbal.step",
			"Test cube.obj",
			"omni_wheel.obj",
			"kanaalplaat-element-vbi-isolatieplaatvloer-200-rc_6_5.ifc",

			"index_hmd_accessory_inclusion_zone.stp",
			"index_base_quickmount.stp",
			"index_controller.stp",
			"index_basestation.stp",
			"index_controller_example.stp",
			"index_controller_sensor_inclusion.stp",
			"index_hmd_facegasket.stp",

			"index_basestation.stl",
			"index_controller.stl",
			"index_controller_example.stl",
			"index_hmd_assembly.stl",
			"index_hmd_accessory_inclusion_zone.stl",

			"Vebo Staal BWS.ifc",
			"Vebo Staal GD1.ifc",
			"Vebo Staal GD2.ifc",
			"Vebo Staal GD3.ifc",
			"Vebo Staal LA1.ifc",
			"Vebo Staal LA2.ifc",
			"Vebo Staal LA3.ifc",
			"Vebo Beton-voorbeeld klein prefab.ifc",
			"Vebo Beton- voorbeeld trappen en bordessen.ifc",

			"219009 Kerstrooslaan te Eindhoven LOD300.ifc",

			"index_hmd_frunk.stp",
			"index_hmd_assembly.stp",

			"Hidravlicna_stiskalnica_315t oprema.step",
		};

		/// <summary>
		/// Creates the TestGimDatas class.
		/// </summary>
		/// <param name="prefix">The prefix path to the CAD files folder to look for the CAD files in.</param>
		public TestGimDatas(string prefix)
		{
			this.prefix = prefix;
			testGimDatasInternal = new Dictionary<string, GimData>();

			testGimDatasInternalLocks = new Dictionary<string, object>();
			for (int i = 0; i < TestFiles.Length; i++)
			{
				testGimDatasInternalLocks[TestFiles[i]] = new object();
			}
		}

		/// <summary>
		/// Gets the gimdata that has the filename that is given. 
		/// The GimData is a clone of the gimdata in the internal array.
		/// </summary>
		/// <param name="filename">The file name of the gimdata to get</param>
		/// <returns>A clone of the gimdata.</returns>
		public GimData Get(string filename)
		{
			LoadIfNotLoaded(filename);
			return testGimDatasInternal[filename].Clone();
		}

		/// <summary>
		/// ONLY USE THIS WHEN NOT EDITING THE GIMDATA BUT ONLY READING FROM IT
		/// Gets the gimdata that has the filename that is given.
		/// The GimData is a direct refrence to the GimData in the internal array!!!
		/// This means this function is quicker than the regulair get. 
		/// But do NOT edit this gimdata beceause that will ruin the other tests!!!
		/// </summary>
		/// <param name="filename">The file name of the gimdata to get</param>
		/// <returns>A direct refrence to the internally stored gimdata as an IReadOnlyGimData.</returns>
		public IReadOnlyGimData GetQuickAndUnsafe(string filename)
		{
			LoadIfNotLoaded(filename);
			return testGimDatasInternal[filename];//.AsReadOnly();
		}

		/// <summary>
		/// Read the file into the internal dictionary.
		/// </summary>
		/// <param name="filename"> The file to read. </param>
		private void LoadIfNotLoaded(string filename)
		{
			lock (testGimDatasInternalLocks[filename])
			{
				if (!testGimDatasInternal.ContainsKey(filename))
				{
					string filePath = prefix + filename;
					// This wont fail but just return null if we can't read the file so we don't need to
					// try again the next time we want to read the file.
					testGimDatasInternal[filename] = TestReadFunctions.ReadToGimData(filePath, false);
				}

				// Always check if the file is null so we know if the file is just unreadable.
				if (testGimDatasInternal[filename] == null)
				{
					throw new Exception("Unreadable file: " + filename);
				}
			}
		}

		readonly Dictionary<Func<object, object>, Dictionary<object, object>> functionGimdataKeys = new();
		private object GetFunctionGimdataKey(Func<object, object> testFunction, object input)
		{
			lock (functionGimdataKeys)
			{
				if (!functionGimdataKeys.ContainsKey(testFunction))
				{
					functionGimdataKeys[testFunction] = new();
				}

				if (!functionGimdataKeys[testFunction].ContainsKey(input))
				{
					functionGimdataKeys[testFunction][input] = new Tuple<Func<object, object>, object>(testFunction, input);
				}
			}

			return functionGimdataKeys[testFunction][input];
		}

		readonly Dictionary<object, object> testFunctionLocks = new();
		readonly Dictionary<object, object> testResultCache = new();

		public Tout RunTest<T, Tout>(Func<object, object> testFunction, T input)
		{
			//return (Tout)testFunction(input);

			// Create test function key.
			object key = GetFunctionGimdataKey(testFunction, input);

			// Lock all locks to make sure we can lock the lock of the test function key.
			lock (testFunctionLocks)
			{
				// Check if we have the key in the testFunctionsLocks, otherwise add it.
				if (!testFunctionLocks.ContainsKey(key))
					testFunctionLocks[key] = new object();
			}

			// Lock the corresponding lock.
			lock (testFunctionLocks[key])
			{
				// If we don't know the result add it to the cache.
				if (!testResultCache.ContainsKey(key))
					testResultCache[key] = testFunction(input);
				else
					GimUtilities.GimLogger.Info("Used cached result. ");
			}

			// Return the cashed result.
			return (Tout)testResultCache[key];
		}
	}
}
