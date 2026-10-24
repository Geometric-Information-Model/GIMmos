using Assimp;
using GIMtransfer.BrepToPoly;
using Microsoft.VisualBasic;
using SharpGLTF.Schema2;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Newtonsoft.Json.Linq;

namespace GIMtransfer
{
    public static partial class FileAccess
    {

        public static readonly Dictionary<string, FileTypeProperties> FileTypes = new Dictionary<string, FileTypeProperties>()
        {
            { "3d",
                new FileTypeProperties(
                        "missing.png",
                        "3D File",
                        "File extension for generic 3D objects.",
						//https://fileinfo.com/extension/3ds#3d_studio_scene
						"All",
                        true, true,
                        new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery},
                        false
                        )
            },
			//{ "3dm",
			//	 new FileTypeProperties(
			//			"RHINO.png",
			//			"Rhino",
			//			"File extension developed by Robert McNeel.",
			//			//https://fileinfo.com/extension/3dm
			//			"v1", "v7",
			//			"v2", "v7",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery},
			//			false
			//			)
			//},
			{ "3ds",
				new FileTypeProperties(
						"A3DS.png",
						"3D Studio Max",
						"File extension developed by Autodesk.",
						//https://fileinfo.com/extension/3ds#3d_studio_scene
						"All",
						true, true,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery},
						false
						)
			},
			//{ "3dxml",
			//	new FileTypeProperties(
			//			"3DXML.png",
			//			"3D XML",
			//			"File extension developed by Dassault Systemes.",
			//			//https://fileinfo.com/extension/3dxml
			//			"4.0",
			//			"4.3",
			//			true, false,
			//			new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			{ "3mf",
				new FileTypeProperties(
						"3MF.png",
						"3D Manufacturing Format",
						"File extension developed by 3MF Consortium.",
						//https://fileinfo.com/extension/3mf
						"All",
						false, true,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},
			{ "ac",
				new FileTypeProperties(
						"missing.png",
						"AC3D",
						"File extension used for AC3D files, which are 3D models created and saved using AC3D software.",
						//https://fileinfo.com/extension/ac
						"All",
						true, false,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},
            { "acc",
                new FileTypeProperties(
                        "missing.png",
                        "ACC",
                        "File extension used for Games and simulation applications.",
						//https://fileinfo.com/extension/ac
						"All",
                        true, false,
                        new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false
                        )
            },
            {"amf",
				new FileTypeProperties(
					"AMC.png",
					"Additive Manufacturing File",
					"File extension used to store 3D printing information including geometry, materials, etc..",
					"All",
					true,false,
					new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery},
					false
					)
			},
			{"ase",
				new FileTypeProperties(
					"ASE.png",
					"Autodesk ASCII Scene Export",
					"Text file containing 2D or 3D scene information; exported by various Autodesk",
					"All",
					true,false,
					new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery},
					false
					)
			},


			//{ "asm.CREO",
			//	new FileTypeProperties(
			//			"CREO.png",
			//			"Creo",
			//			"File extension developed by Parametric Technology.",
			//			//https://fileinfo.com/extension/asm#pro_engineer_assembly_file
			//			"2.0", "6.0",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "asm.SLDE",
			//	 new FileTypeProperties(
			//			"SLDE.png",
			//			"Solid Edge",
			//			"File extension developed by Siemens.",
			//			//https://fileinfo.com/extension/asm#solid_edge_assembly_file
			//			"17", "2021",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},

			{ "b3d",
				  new FileTypeProperties(
						"missing.png",
						"Blitz3D Entity Model File",
						"File extension used o created 3D games; stores model information in chunks, can contain geometrical and animation data",
                        //https://fileinfo.com/extension/b3d,
                        "All",
						true, false,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},

			{ "bin",
				  new FileTypeProperties(
						"GIMZIP.png",
						"GimZip",
						"File extension developed by us, much lighter than any others.",
						"All",
						true, true,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},
			{
				"blend",
				new FileTypeProperties(
					"BLEND.png",
					"Blender 3D Data File",
					"A BLEND file is a 3D image or animation project created with Blender. " +
					"It contains 3D mesh data, lighting information, vertex painting, animation keyframes,etc...",
					//https://fileinfo.com/extension/blend
					"All",
					true, false,
					new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false

					)
			},
			//{
			//	"bvh",
			//	new FileTypeProperties(
			//		"missing.png",
			//		"Biovision Hierarchy Animation File",
			//		"File extension used by 3ds Max's Character Studio and other 3D animation programs to import" +
			//		" rotational joint data",
   //                 //https://fileinfo.com/extension/blend
			//		"All",
			//		true, false,
			//		new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, false

			//		)
			//},
			//{ "brep",
			//	new FileTypeProperties(
			//			"BREP.png",
			//			"BRep",
			//			"File extension developed by Open CASCADE Technology.",
			//			//https://fileinfo.com/extension/brep
			//			"All",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "CATPart",
			//	new FileTypeProperties(
			//			"CATIA.png",
			//			"CATIA",
			//			"File extension developed by Dassault Systemes.",
			//			//https://fileinfo.com/extension/catpart
			//			"V5R16", "V5R30",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "CATProduct",
			//	new FileTypeProperties(
			//			"CATIA.png",
			//			"CATIA",
			//			"File extension developed by Dassault Systemes.",
			//			//https://fileinfo.com/extension/catproduct
			//			"V5R16", "V5R30",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},

			{
				"cob",
				new FileTypeProperties(
					"missing.png",
					"Caligari trueSpace Object",
					"File format is a 3D object format generated by trueSpace, a 3D design package from Caligari Corporation.",

					"All",
					true, false,
					new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false

					)
			},

			//{
			//	"csm",
			//	new FileTypeProperties(
			//		"CSM.png",
			//		"Character Studio Marker File",
			//		"ASCII text file that contains position markers for a 3D character; used by Character Studio" +
			//		" a biped animation tool included with Autodesk's 3ds Max program",

			//		"All",
			//		true, false,
			//		new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, false

			//		)
			//},

			{ "dae",
				new FileTypeProperties(
						"COLLADA.png",
						"Collada",
						"File extension developed by Sony.",
						//https://fileinfo.com/extension/dae
						"All", "Version 1.5",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
			{ "dwg",
				new FileTypeProperties(
						"DWG.png",
						"AutoCAD",
						"File extension developed by Autodesk.",
						//https://fileinfo.com/extension/dwg
						"AutoCAD 2007 (AC1021)",
						"AutoCAD 2018 (AC1032)",
						true, true,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},
			{ "dxf",
				new FileTypeProperties(
						"DXF.png",
						"DXF",
						"File extension developed by Autodesk.",
						//https://fileinfo.com/extension/dxf
						"All",
						true, false,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},
			{
			"enff",
				new FileTypeProperties(
					"missing.png",
                    "Extended Neutral File Format",
                    "ENFF, a type of NFF, contains uncompressed 3D data for scenes or objects, used in rendering and ray-tracing.",
					"All",
					true, false,
					new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false

					)
			},
			{ "fbx",
				new FileTypeProperties(
						"FBX.png",
						"FBX",
						"File extension developed by Autodesk.",
						//https://fileinfo.com/extension/fbx
						"All",
						"All",
						"7.2/7.4/7.5",
						"7.2/7.4/7.5",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
			{ "glb",
				new FileTypeProperties(
						"GLTF.png",
						"glTF",
						"File extension developed by The Khronos Group.",
						//https://fileinfo.com/extension/glb#binary_gl_transmission_format_file
						"glTF 2.0",
						true, true,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
			{ "gltf",
				new FileTypeProperties(
						"GLTF.png",
						"glTF",
						"File extension developed by The Khronos Group.",
						//https://fileinfo.com/extension/gltf
						"glTF 2.0",
						true, true,
						new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
			{
				"gml",
				new FileTypeProperties(
					"GML.png",
					"GML",
                    "Reads 2D and 3D GML, writes 3D GML.", 
					"GML 3.2",
					true, true,
                    new List<Industry>(){ Industry.Cartography },
					false
                    )
			},
            {
                "kml",
                new FileTypeProperties(
                    "GML.png",
                    "KML",
                    "Reads KML linework as a 2D network-line payload.",
                    "KML 2.2",
                    true, false,
                    new List<Industry>(){ Industry.Cartography },
                    false
                    )
            },
            {
                "xml",
                new FileTypeProperties(
                    "XML.png",
                    "XML",
                    "Currently only supports XML Containing GML.", 
					"GML 3.2", 
					true, false,
                    new List<Industry>(){ Industry.Cartography },
                    false
                    )
            },
            {
                "klic",
                new FileTypeProperties(
                    "GML.png",
                    "KLIC",
                    "KLIC/IMKL 2.0 (WIBON) delivery, read as a 2D network-line payload (no GimData).",
                    "IMKL 2.0",
                    true, false,
                    new List<Industry>(){ Industry.Cartography },
                    false
                    )
            },
            {
                "imkl",
                new FileTypeProperties(
                    "GML.png",
                    "IMKL",
                    "IMKL 2.0 GML, read as a 2D network-line payload (no GimData).",
                    "IMKL 2.0",
                    true, false,
                    new List<Industry>(){ Industry.Cartography },
                    false
                    )
            },
            {
			"hmp",
				new FileTypeProperties(
					"missing.png",
                    "Worms Height Map File",
                    "Used to get the height data of maps in a game called \"Worms Mayhem\"",

					"All",
					true, false,
					new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false

					)
			},
			{ "ifc",
				new FileTypeProperties(
						"IFC.png",
						"IFC",
						"File extension developed by BuildingSMART.",
						//https://fileinfo.com/extension/ifc
						"2X3",
						true, true,
						new List<Industry>(){ Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering }
						, false
						)
			},
			//{
			//"irr",
			//	new FileTypeProperties(
			//		"missing.png",
   //                 "Irrlicht 3D Scene",
   //                 "An IRR file from Irrlicht contains an XML scene graph with particle systems, animators, materials, and .IRRMESH links.",

			//		"All",
			//		true, false,
			//		new List<Industry>(){ Industry.Cartography,  Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, false

			//		)
			//},
			{
                "irrmesh",
                new FileTypeProperties(
                "missing.png",
                "Irrlicht Static Mesh File",
                "A mesh file by Irrlicht, an open-source 3D engine in C++, contains XML mesh data linked to .IRR files",


                    "All",
                true, false,
					new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false

					)
			},

			{ "iges",
				new FileTypeProperties(
						"IGES.png",
						"IGES",
						"File extension developed by ICAM.",
						//https://fileinfo.com/extension/iges
						"All", "5.3",
						"5.3", "5.3",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
			{ "igs",
				new FileTypeProperties(
						"IGES.png",
						"IGES",
						"File extension developed by ICAM.",
						//https://fileinfo.com/extension/igs#iges_drawing
						"All", "5.3",
						"5.3", "5.3",
						true, false,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},
			{ "json",
				  new FileTypeProperties(
						"JSON.png",
						"JSON",
						"JSON format developed by GIMtransfer, compact and flexible for different uses.",
						//https://fileinfo.com/extension/json
						//https://en.wikipedia.org/wiki/JSON	<for developer info>
						"N/A",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
					  )
			},
			//{ "jt",
			//	new FileTypeProperties(
			//			"JT.png",
			//			"JT",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/jt
			//			"8.x", "10.x and ISO14306:2012",
			//			"9.5 and ISO14306:2012", "9.5 and ISO14306:2012",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			{
            "LWO",
                new FileTypeProperties(
                    "LWO.png",
                    "LightWave 3D Object",
                    "An LWO file is a 3D object in LightWave 3D format with points, polygons, surfaces, and texture references." +
					" It is used in LightWave 3D, Modo, and Poser.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering}
                        , false

                    )
            },
            //{
            //"LWS",
            //    new FileTypeProperties(
            //        "LWS.png",
            //        "LightWave 3D Scene",
            //        "An LWS file is a scene created with LightWave 3D, storing object positions, movements, cameras, and references to .LWO files.",

            //        "All",
            //        true, false,
            //        new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering}
            //            , false

            //        )
            //},
            {
            "LXO",
                new FileTypeProperties(
                    "LXO.png",
                    "Modo 3D Image",
                    "An LXO file is a 3D image created by Foundry Modo, used for storing scenes with 3D models, animations, and renderings.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering}
                        , false

                    )
            },
            {
            "md2",
                new FileTypeProperties(
                    "missing.png",
                    "Quake 2 3D Model",
                    "An MD2 file is a 3D model used by id Software's Quake 2,",

                    "All",
                    true, false,
                    new List<Industry>(){Industry.CivilEngineering }
                        , false

                    )
            },
            {
            "md3",
                new FileTypeProperties(
                    "missing.png",
                    "Quake 3 3D Model",
                    "A model format used by Quake III Arena, primarily for player and character models",

                    "All",
                    true, false,
                    new List<Industry>(){Industry.CivilEngineering }
                        , false

                    )
            },
            {
            "md5anim",
                new FileTypeProperties(
                    "missing.png",
                    "id Tech 4 Model Animation File",
                    "An animation file used by id Tech 4 engine (Doom 3 and Quake 4), containing sequence data for 3D meshes " +
					"from .MD5MESH files",

                    "All",
                    true, false,
                    new List<Industry>(){Industry.CivilEngineering }
                        , false

                    )
            },
            {
            "md5camera",
                new FileTypeProperties(
                    "missing.png",
                    "id Tech 4 Model Camera File",
                    "A camera file used by id Tech 4, the game engine for Doom 3 and Quake 4; contains 3D camera paths",

                    "All",
                    true, false,
                    new List<Industry>(){Industry.CivilEngineering }
                        , false

                    )
            },
            {
            "md5mesh",
                new FileTypeProperties(
                    "missing.png",
                    "id Tech 4 3D Mesh File",
                    "A mesh file used by id Tech 4, the game engine for Doom 3 and Quake 4; contains polygonal mesh data for models",

                    "All",
                    true, false,
                    new List<Industry>(){Industry.CivilEngineering }
                        , false

                    )
            },
            {
            "mdc",
                new FileTypeProperties(
                    "missing.png",
                    "Wolfenstein Compressed 3D Model",
                    "An MDC file used in games, contains 3D models with mesh, colorization, shading, and compressed animation data.",

                    "All",
                    true, false,
                    new List<Industry>(){Industry.CivilEngineering }
                        , false

                    )
            },
            //{
            //"mdl ",
            //    new FileTypeProperties(
            //        "missing.png",
            //        "Character Studio Marker File",
            //        "An MDL file in Warcraft III stores 3D models for buildings and units, editable in text format before conversion to binary .MDX files.",

            //        "All",
            //        true, false,
            //        new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
            //            , false

            //        )
            //},
            {
            "mesh",
                new FileTypeProperties(
                    "missing.png",
                    "3D Mesh Model",
                    "A 3D model format using \"mesh\" files, representing geometric models with polygons forming object surfaces.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			{
            "mot",
                new FileTypeProperties(
                    "missing.png",
                    "LightWave Motion File",
                    "A MOT file from LightWave 3D stores 3D motion data for objects or cameras, allowing for reuse of movements",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			{
            "ms3d",
                new FileTypeProperties(
                    "missing.png",
                    "MilkShape 3D Model",
                    "A model created with MilkShape 3D, containing skeleton, surface, and texture data, and sometimes animation; " +
					"exports to various 3D formats.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			{
            "ndo",
                new FileTypeProperties(
                    "missing.png",
                    "3D Low-polygon Modeler",
                    "NDO is a low-polygon 3D modeling program for creating objects and scenes with tools for textures, materials, " +
					"and scene construction.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			{
            "nff",
                new FileTypeProperties(
                    "missing.png",
                    "Neutral File Format",
                    "1990s geometric design format using a minimal scene description language for objects, surfaces, lighting, " +
					"and viewing angle.",
                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },

            { "obj",
				 new FileTypeProperties(
						"OBJ.png",
						"OBJ",
						"File extension developed by Wavefront Technologies.",
						//https://fileinfo.com/extension/obj#wavefront_3d_object_file
						"All",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false)
			},
			{ 
            "off",
                new FileTypeProperties(
                    "missing.png",
                    "Object File Format",
                    "ASCII format describing 3D objects with collections of polygons and vertices forming the surface.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
            {
            "ogex",
                new FileTypeProperties(
                    "missing.png",
                    "Open Game Engine Exchange",
                    "OGEX files describe 3D scenes, objects, materials, and animations for cross-application and game engine use.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
            

			//{ "par",
			//	 new FileTypeProperties(
			//			"SLDE.png",
			//			"Solid Edge",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/par#solid_edge_part_file
			//			"17", "2021",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, false //couldnt find the default axis 
			//			)
			//},
			//{ "pdf",
			//	 new FileTypeProperties(
			//			"PDF3D.png",
			//			"3D PDF",
			//			"File extension developed by Adobe Systems.",
			//			//https://fileinfo.com/extension/pdf
			//			"Standard ECMA-363, 1st Edition", "Standard ECMA-363, 1st Edition",
			//			"1.6, based on U3D format", "1.6, based on U3D format",
			//			true, false,
			//			new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true //doesnt matter
			//			)
			//},
			{
            "pk3",
                new FileTypeProperties(
                    "missing.png",
                    "Quake 3 Engine Game Data",
                    "A PK3 file is a game archive used by Quake 3 and other id Tech 3 games, similar to a .ZIP file.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
            { "ply",
				 new FileTypeProperties(
						"PLY.png",
						"PLY",
						"File extension developed by Stanford University.",
						//https://fileinfo.com/extension/ply#polygon_model_file
						"All",
						true, true,
						new List<Industry>(){Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
            {
            "pmx",
                new FileTypeProperties(
                    "missing.png",
                    "MikuMikuDance Model File",
                    "An MMD file is a 3D model used by MikuMikuDance (MMD) for animated characters and accessories with textures.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
            {
            "prj",
                new FileTypeProperties(
                    "missing.png",
                    "Project File",
                    "A PRJ file stores project data and settings, used by programs like Microsoft Visual Basic and Autodesk AutoCAD",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			//{ "prc",
			//	 new FileTypeProperties(
			//			"PRC.png",
			//			"PRC",
			//			"File extension developed by Adobe Systems.",
			//			//https://fileinfo.com/extension/prc#product_representation_compact_file
			//			"All",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "prt",
			//	new FileTypeProperties(
			//			"CREO.png",
			//			"Creo",
			//			"File extension developed by PTC & Siemens.",
			//			//https://fileinfo.com/extension/prt#creo_parametric_part
			//			"2.0", "6.0",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "psm",
			//	 new FileTypeProperties(
			//			"SLDE.png",
			//			"Solid Edge",
			//			"File extension developed by Siemens.",
			//			//https://fileinfo.com/extension/psm#solid_edge_sheet_metal_file
			//			"17", "2021",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true)
			//},
			{
            "q3o",
                new FileTypeProperties(
                "missing.png",
                "Quick3D Model",
                "Q3O files store 3D models with images, textures, and animations for viewing in 3D software or conversion to other formats.",

                    "All",
                true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
            //{
            //"q3s",
            //    new FileTypeProperties(
            //        "missing.png",
            //        "Quick3D Scene",
            //        "Q3S files store 3D scenes created with Quick3D Scene, including geometry, textures, lighting, and animations.",

            //        "All",
            //        true, false,
            //        new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
            //            , false

            //        )
            //},
			{
				"rvt",
				new FileTypeProperties(
					"Revit.png",
					"Revit",
					"Autodesk's proprietary file format for Revit.",
					"2021", "2022",
					true, false,
					new List<Industry>(){ Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering }
					, false)
			},
			//{ "sab",
			//	new FileTypeProperties(
			//			"ACIS.png",
			//			"ACIS-SAB",
			//			"File extension developed by Spatial.",
			//			//https://fileinfo.com/extension/sab#acis_sab_model
			//			"R1.5", "R28.0 (2018 1.0.1)",
			//			"R2.0", "R28.0 (2018 1.0.1)",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "sat",
			//	new FileTypeProperties(
			//			"ACIS.png",
			//			"ACIS-SAT",
			//			"File extension developed by Spatial.",
			//			//https://fileinfo.com/extension/sat
			//			"R1.5", "R28.0 (2018 1.0.1)",
			//			"R2.0", "R28.0 (2018 1.0.1)",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			{
            "sib",
                new FileTypeProperties(
                    "missing.png",
                    "SILO",
                    "SIB files are 3D models from Silo, containing objects, textures, and materials, exportable to various formats.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },

			//{ "sldasm",
			//	 new FileTypeProperties(
			//			"SLD.png",
			//			"Solid Works",
			//			"File extension developed by Dassault Systemes.",
			//			//https://fileinfo.com/extension/sldasm
			//			"2004", "2021",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "sldprt",
			//	 new FileTypeProperties(
			//			"SLD.png",
			//			"Solid Works",
			//			"File extension developed by Dassault Systemes.",
			//			//https://fileinfo.com/extension/sldprt
			//			"2004", "2021",
			//			true, false,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{
            //"smd",
            //    new FileTypeProperties(
            //        "missing.png",
            //        "Valve Studiomdl Data File",
            //        "Source Engine 3D model file for games like Half-Life, compiled from ASCII text using Studiomdl and a .QC script.",

            //        "All",
            //        true, false,
            //        new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
            //            , false


                  //  )
            //},
			{ "step",
				 new FileTypeProperties(
						"STEP.png",
						"Step",
						"File extension developed by International Organization for Standardization.",
						//https://fileinfo.com/extension/step
						"STEP AP214",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, false
						)
			},

			{ "stl",
				 new FileTypeProperties(
						"STL.png",
						"STL",
						"File extension developed by 3D Systems.",
						//https://fileinfo.com/extension/stl#stereolithography_file
						"All",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.SupplierForMachinery}
						, false
						)
			},
			{ "stp",
				 new FileTypeProperties(
						"STEP.png",
						"Step",
						"File extension developed by International Organization for Standardization.",
						//https://fileinfo.com/extension/stp#step_3d_cad_file
						"STEP AP214",
						true, false,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						,false
						)
			},
			{
            "ter",
                new FileTypeProperties(
                    "missing.png",
                    "Terragen Terrain File",
                    "Terrain file created by Terragen, containing altitude data in heightfield format for easy manipulation within the program.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			//{ "u3d",
			//	 new FileTypeProperties(
			//			"U3D.png",
			//			"Universal 3D",
			//			"File extension developed by 3D Industry Forum.",
			//			//https://fileinfo.com/extension/u3d
			//			"(Standard ECMA-363, 1st Edition) in binary format", "(Standard ECMA-363, 1st Edition) in binary format",
			//			"Standard ECMA_363, 4th Edition, June 2007", "Standard ECMA_363, 4th Edition, June 2007",
			//			true, false,
			//			new List<Industry>(){ Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{
   //         "uc",
   //             new FileTypeProperties(
   //                 "missing.png",
   //                 "Character Studio Marker File",
   //                 "ASCII text file that contains position markers for a 3D character; used by Character Studio" +
   //                 " a biped animation tool included with Autodesk's 3ds Max program",

   //                 "All",
   //                 true, false,
   //                 new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
   //                     , false

   //                 )
   //         },
			//{ "usd",
			//	 new FileTypeProperties(
			//			"USD.png",
			//			"USD",
			//			"File extension developed by Pixar Animation Studios.",
			//			//https://fileinfo.com/extension/usd
			//			"All",
			//			false, true,
			//			new List<Industry>(){ Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "usda",
			//	 new FileTypeProperties(
			//			"USD.png",
			//			"USD",
			//			"File extension developed by Pixar Animation Studios.",
			//			//https://fileinfo.com/extension/usd
			//			//Copy of USD
			//			"All",
			//			false, true,
			//			new List<Industry>(){ Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "usdc",
			//	 new FileTypeProperties(
			//			"USD.png",
			//			"USD",
			//			"File extension developed by Pixar Animation Studios.",
			//			//https://fileinfo.com/extension/usd
			//			//Copy of USD
			//			"All",
			//			false, true,
			//			new List<Industry>(){ Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "usdz",
			//	 new FileTypeProperties(
			//			"USD.png",
			//			"USD",
			//			"File extension developed by Apple and Pixar Animation Studios.",
			//			//https://fileinfo.com/extension/usdz
			//			"All",
			//			false, true,
			//			new List<Industry>(){ Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			{
            "vta",
                new FileTypeProperties(
                    "missing.png",
                    "Valve Face Flex ",
                "VTA files in Valve Studio store and edit Valve face textures in a proprietary binary format.",

                    "All",
                true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			//{ "wrl",
			//	 new FileTypeProperties(
			//			"VRML.png",
			//			"VRML",
			//			"File extension developed by Web3D Consortium.",
			//			//https://fileinfo.com/extension/wrl
			//			//https://en.wikipedia.org/wiki/VRML#Emergence,_popularity,_and_rival_technical_upgrade <for developer>
			//			//Original developer may be unknown
			//			"1.0", "VRML97 (version 2.0).",
			//			"VRML97 (version 2.0).", "VRML97 (version 2.0).",
			//			true, true,
			//			new List<Industry>(){ Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},

			 { "x",
				 new FileTypeProperties(
						"X.png",
						"X",
						"File extension associated with DirectX files used for storing 3D models, textures, and animations",
						//https://fileinfo.com/extension/x
						"All",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
			//{ "x_b",
			//	 new FileTypeProperties(
			//			"PARASOLID.png",
			//			"Parasolid XT",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/x_b
			//			"v0.15", "All",
			//			"v12", "v33.1",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "x_t",
			//	 new FileTypeProperties(
			//			"PARASOLID.png",
			//			"Parasolid XT",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/x_t
			//			"v0.15", "All",
			//			"v12", "v33.1",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			{ "x3d",
				 new FileTypeProperties(
						"X3D.png",
						"X3D",
						"File extension developed by MAGIX.",
						//https://fileinfo.com/extension/x3d
						"ISO/IEC 19775",
						true, true,
						new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
						, true
						)
			},
            {
            "xgl",
                new FileTypeProperties(
                    "missing.png",
                    "XGL 3D",
                    "XGL files are CAD models used in engineering and manufacturing design applications like Autodesk Inventor " +
					"and Solidworks.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
			//{ "xmb_bin",
			//	 new FileTypeProperties(
			//			"PARASOLID.png",
			//			"Parasolid XT",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/x_t
			//			//Copy of X_T
			//			"v0.15", "All",
			//			"v12", "v33.1",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "xmb_txt",
			//	 new FileTypeProperties(
			//			"PARASOLID.png",
			//			"Parasolid XT",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/x_t
			//			//Copy of X_T
			//			"v0.15", "All",
			//			"v12", "v33.1",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "xmt_bin",
			//	 new FileTypeProperties(
			//			"PARASOLID.png",
			//			"Parasolid XT",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/x_t
			//			//Copy of X_T
			//			"v0.15", "All",
			//			"v12", "v33.1",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			//{ "xmt_txt",
			//	 new FileTypeProperties(
			//			"PARASOLID.png",
			//			"Parasolid XT",
			//			"File extension developed by Siemens PLM Software.",
			//			//https://fileinfo.com/extension/x_t
			//			//Copy of X_T
			//			"v0.15", "All",
			//			"v12", "v33.1",
			//			true, true,
			//			new List<Industry>(){ Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
			//			, true
			//			)
			//},
			{
            "zae",
                new FileTypeProperties(
                    "missing.png",
                    "Archived Collada Format",
                    "A .ZAE file is a Compressed COLLADA Digital Asset Exchange file.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography, Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
            {
            "zgl",
                new FileTypeProperties(
                    "missing.png",
                    "Compressed XGL 3D CAD Model",
                    "ZGL files are compressed 3D CAD models using the XGL format, enabling smaller file sizes for faster sharing " +
					"and storage.",

                    "All",
                    true, false,
                    new List<Industry>(){ Industry.Cartography ,Industry.CivilEngineering, Industry.ConstructionEngineering, Industry.SupplierForBuildingEngineering, Industry.MarineEngineering, Industry.Automotive, Industry.Machinery, Industry.SupplierForMachinery}
                        , false

                    )
            },
        };

        public static readonly Dictionary<string, List<string>> options = new Dictionary<string, List<string>>() { { "prt", new List<string>() { "CREO", "NX" } }, { "asm", new List<string>() { "CREO", "SLDE" } } };

    }
}
