# Format capabilities

`UI` reflects `FileTypes.cs`; `runtime` reflects `FileManagerInternal.cs` and `FileManager.cs`. Runtime entries describe actual dispatch. `OCCT-backed` marks native-backed paths; `fallback` marks conditional or catch-all paths. No separate runtime experimental flag is exposed.

| Format | Read | Write | Preview / fallback | Caveats |
|---|---|---|---|---|
| JSON (`.json`) | UI yes; runtime direct JSON reader | UI yes; runtime direct JSON writer | JSON can produce GLB visualisation bytes | GIM JSON model |
| BIN (proprietary, `.bin`) | UI yes; runtime binary reader | UI yes; runtime binary writer | — | Format internals intentionally unspecified |
| GML / KML (`.gml`, `.kml`) | GML UI yes; runtime reader accepts GML and KML | GML UI yes; runtime writer supports GML only | — | KML has no `FileTypes` entry. Writer emits GML with fixed CityGML/GML structure and limited curve handling |
| DWG / DXF (`.dwg`, `.dxf`) | UI yes; runtime ACadSharp readers support both | DWG UI yes, DXF UI no; no dedicated runtime writer branch | — | Both types fall through to Assimp on write; Assimp export map has no DWG/DXF IDs |
| STEP / STP (`.step`, `.stp`) | UI yes; runtime OCCT-backed reader | STEP UI yes, STP UI no; runtime OCCT-backed writer supports both | — | UI metadata lists AP214; runtime dispatch does not enforce version |
| IGES / IGS (`.iges`, `.igs`) | UI yes; runtime OCCT-backed reader | IGES UI yes, IGS UI no; runtime OCCT-backed writer supports both | — | UI metadata lists 5.3; runtime dispatch does not enforce version |
| IFC (`.ifc`) | UI yes; runtime OCCT-backed reader | UI yes; runtime OCCT-backed writer; mesh-only when input has no B-rep | Preview flag or OCCT failure: installed converter → DAE → Assimp | UI metadata lists IFC 2X3. Fallback requires converter executable |
| GLB (`.glb`) | UI yes; runtime generic Assimp fallback; direct SharpGLTF reader helper also exists | UI yes; runtime Assimp `glb2` export; direct SharpGLTF helpers also exist | JSON/object visualisation helpers produce GLB | Base64 helper's `scale` parameter is currently unused; byte-array helper passes `scale` |
| Assimp fallback (other extensions) | Extensions not caught above fall through to Assimp import | Non-JSON/BIN/OCCT/GML types fall through to Assimp export | Used by IFC fallback and mesh import paths | Export IDs are explicit; an unmapped output type reaches Assimp with an empty export ID |
