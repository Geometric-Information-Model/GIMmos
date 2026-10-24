# RFC 0001 — IFC Georef via IfcMapConversion (GIMMAPS download)

Bake **IfcProjectedCRS** + **IfcMapConversion** into IFC4 exports at write time in `GIMocctWrapper/GIMocctCLI/IFCWriter.cpp`. v1 consumer: **GIMMAPS scene download** when `fileType == "ifc"`. API shaped for future non-map IFC exports.

**Status:** Approved design (grill-me 2026-06-14). Not implemented.

**Related:** Workspace root `GIM-ifc-preview-handoff.md` (IFC *import* preview — separate concern). Future IFC RFCs use `000N-ifc-*` in this folder.

---

## Design decisions (locked)

| # | Topic | Decision |
|---|--------|----------|
| 1 | Scope | **GIMMAPS v1**; `GIMIFCExportOptions` API future-proof for other export paths |
| 2 | Eastings / Northings | **Local origin:** RD of map marker (`originRdX`, `originRdY`). **Global origin:** `0`, `0` |
| 3 | Rotation | Identity: `XAxisAbscissa=1`, `XAxisOrdinate=0` |
| 4 | CRS | String on request; front defaults **`EPSG:28992`** |
| 5 | Vertical | **`OrthogonalHeight = 0`** for v1 |
| 6 | Trigger | Auto for every GIMMAPS IFC download → **upgrade to param-gated (C) before** general IFC export uses same path |
| 7 | RD computation | Front sends `originRdX` / `originRdY` (meters) + `origin`; no duplicate C# conversion |
| 8 | C# carrier | `GLBSceneMergeRequest` → **`GimOptions`** (new fields) → `IFCExportOptionsCLR` |
| 9 | IfcSite WGS84 | **Do not** write `RefLatitude` / `RefLongitude` |
| 10 | API shape | `WriteGimDataToIfc(..., GimOptions gimOptions, ...)`; `WriteFromGimData` passes options on IFC path |
| 11 | Validation | When georef on: assert `IfcProjectedCRS` + `IfcMapConversion` exist before serialize |

---

## Why MapConversion (not IfcSite refs)

- GIMMAPS positions in **RD**; MapConversion maps local IFC (0,0,0) → projected CRS.
- `IfcSite.RefLatitude/Longitude` is WGS84-only, approximate, backward-compat; conflicts with MapConversion if both set.
- Long-term: same path for GIS, Revit, IfcGref-style tooling.

---

## v1 data flow

```
MapsApp downloadScene (fileType === "ifc")
  → POST /DownloadGLBData
      projectedCrs: "EPSG:28992"
      origin: "local" | "global"
      originRdX, originRdY   // meters; marker RD when local
      (+ existing min/max, Children, …)
  → SignalR DownloadGLBData → frmMain.DownloadGLBByteForMap
  → map into GimOptions (georef fields)
  → GimMapSceneDownload → GimMapsSceneDownloadLogic → WriteFromGimData
  → GIMocctWriteElements.WriteGimDataToIfc(path, fileType, gimData, gimOptions, …)
  → OcctWrapper.AdvancedWriteIfcFromMemory(path, ifcExportOptionsClr, …)
  → IFCWriter::WriteDocToIfc
      InitializeIfc4ExportScaffold
      ApplyGeoreferencing (NEW) when gimOptions.projectedCrs set
      EmitSpatialContainers / EmitLeafProducts
      ValidateIfc4Export (+ georef checks when enabled)
      write file
```

### Eastings / Northings rule

| `origin` | `mapEastings` | `mapNorthings` |
|----------|---------------|----------------|
| `local` | `originRdX` | `originRdY` |
| `global` | `0` | `0` |

Geometry in global mode is already absolute RD (meters after manager `/1000`); MapConversion anchor is origin of engineering CRS = RD (0,0) equivalent.

---

## New / extended types

### `GimOptions` (GIMmos — `FileManager/FileManager.cs`)

```csharp
// All optional; unset = no georef (non-IFC / non-GIMMAPS exports unchanged)
public string projectedCrs;           // e.g. "EPSG:28992"
public double mapEastings;
public double mapNorthings;
public double mapOrthogonalHeight;    // v1 caller always 0
public double mapXAxisAbscissa = 1.0;
public double mapXAxisOrdinate = 0.0;
public double mapScale = 1.0;
```

Helper (manager or transfer): set from `GLBSceneMergeRequest` when `fileType` is `ifc`:

```csharp
gimOptions.projectedCrs = request.projectedCrs ?? "EPSG:28992";
if (request.origin == "local") {
    gimOptions.mapEastings = request.originRdX;
    gimOptions.mapNorthings = request.originRdY;
} else {
    gimOptions.mapEastings = 0;
    gimOptions.mapNorthings = 0;
}
gimOptions.mapOrthogonalHeight = 0;
// rotation + scale: defaults above
```

### `GLBSceneMergeRequest` (GIMmanager — `GLBSceneMergeRequest.cs`)

```csharp
public string projectedCrs { get; set; }  // default "EPSG:28992" from front
public double originRdX { get; set; }       // meters
public double originRdY { get; set; }       // meters
// origin already exists
```

### `GIMmapDownloadRequest` (GIMfront — `global.d.ts`)

Mirror the same fields. In `MapsApp/index.jsx` `downloadScene`, when exporting IFC:

- `projectedCrs: "EPSG:28992"`
- `originRdX: X`, `originRdY: Y` from `MapSelectionContext` (already RD meters from `LongLatToRD`)

### `GIMIFCExportOptions` (GIMocctWrapper — `IFCExportTypes.h`)

```cpp
struct GIMIFCGeorefParams {
  bool enabled = false;
  std::string projectedCrs;  // e.g. "EPSG:28992"
  double eastings = 0.0;
  double northings = 0.0;
  double orthogonalHeight = 0.0;
  double xAxisAbscissa = 1.0;
  double xAxisOrdinate = 0.0;
  double scale = 1.0;
};

struct GIMIFCExportOptions {
  // existing: tessellationDeflection, verboseLogging, isMeshOnly
  GIMIFCGeorefParams georef;
};
```

Expose on `IFCExportOptionsCLR`; map in `IFCWriterWrapper.cpp` `MapOptions`. Extend `OcctWrapper::AdvancedWriteIfcFromMemory` to accept options (today hardcodes empty `GIMIFCExportOptions`).

---

## C++ implementation (`IFCWriter.cpp`)

**Hook:** immediately after `InitializeIfc4ExportScaffold(file, scaffold)` (~line 752), before Pass 1.

**`ApplyGeoreferencing`** (inline or `IfcGeoreferencing.cpp`):

1. If `!options.georef.enabled` → return.
2. `SourceCRS` = `scaffold.ModelContext` (Model `IfcGeometricRepresentationContext` — already set in `Ifc4ExportScaffold.cpp`).
3. `file.addEntity(new Ifc4::IfcProjectedCRS(..., Name=projectedCrs))`.
4. `file.addEntity(new Ifc4::IfcMapConversion(SourceCRS, TargetCRS, Eastings, Northings, OrthogonalHeight, XAxisAbscissa, XAxisOrdinate, Scale))`.

Mirror IfcOpenShell Python `add_georeferencing` / `edit_georeferencing` (`IfcOpenshell/.../api/georeference/add_georeferencing.py`). **Do not** set `IfcSite.RefLatitude` / `RefLongitude`.

**Units:** export declares **meters** (`NormalizeProjectLengthUnitToMetre`); MapConversion values in **meters**. Front/manager already use RD in meters for map `X`/`Y`; bbox/child XYZ use mm until `/1000` in manager.

**`ValidateIfc4Export`:** when `options.georef.enabled`, fail if no `IfcProjectedCRS` or no `IfcMapConversion` in file.

---

## Repos & files

| Repo | Files |
|------|--------|
| **GIMocctWrapper** | `IFCExportTypes.h`, `IFCWriter.cpp`, `IFCWriter.h`, `IFCWriterWrapper.h/cpp`, `GIMocctCLI.cpp/h` |
| **GIMmos** | `FileManager/FileManager.cs` (`GimOptions`), `write/GIMocctWrite/GIMocctWriteElements.cs`, `FileManager/FileManager.cs` (`WriteFromGimData`, `GimMapSceneDownload` path) |
| **GIMmanager** | `GLBSceneMergeRequest.cs`, `frmMain.cs` (`DownloadGLBByteForMap`) |
| **GIMfront** | `pages/MapsApp/index.jsx`, `global.d.ts` |

**Out of v1:** CoreWebApi persistence, IFC import georef read-back, TestUI georef UI, `OrthogonalHeight` from map `Z`, MapConversion rotation from scene yaw.

---

## Deferred

| Item | Notes |
|------|--------|
| **Q6 → C** | Apply georef only when `projectedCrs` + coords present; required before non-GIMMAPS IFC export |
| **Elevation** | `OrthogonalHeight` from map `Z` / NAP once vertical semantics defined |
| **Rotation** | Non-identity `XAxis*` when project north vs grid north is modeled |
| **Import** | `IFCReadElements` / OCCT read does not restore georef today |
| **Preview RFC** | Optional `0002-ifc-preview-handoff.md` — migrate from workspace root |
| **IfcSite WGS84** | Revisit only if a downstream tool requires it and ignores MapConversion |

---

## E2E verification (post-impl)

| Scenario | Expected |
|----------|----------|
| GIMMAPS download IFC, **local origin** | `IfcMapConversion` Eastings/Northings ≈ marker RD; CRS `EPSG:28992` |
| GIMMAPS download IFC, **global origin** | Eastings/Northings = 0; geometry absolute RD |
| GIMMAPS download GLB/OBJ/… | No georef entities |
| TestUI / generic `WriteFromGimData` IFC | No georef (`projectedCrs` unset) |
| IfcGref / map viewer | Model aligns with map at marker |

**Static chain:** `downloadScene` → `DownloadGLBData` → `DownloadGLBByteForMap` → `GimOptions` → `WriteGimDataToIfc` → `ApplyGeoreferencing`.

---

## Effort (estimate)

| Part | Days |
|------|------|
| `IFCWriter.cpp` + validation | 2–3 |
| CLR / `GimOptions` / plumbing | 1 |
| GIMMAPS front + manager | 0.5 |
| E2E + DLL rebuild | 1 |
| **Total** | **~4–5** |

GitNexus impact: **LOW** on `IFCWriter`, `WriteGimDataToIfc` (optional fields; existing exports unchanged when georef unset).

---

## PR checklist

- [ ] `GIMocctCLI` rebuilt; DLL copied to GIMmos / GIMmanager
- [ ] `impact` on `IFCWriter`, `WriteGimDataToIfc` before merge
- [ ] `detect_changes()` per touched repo
- [ ] Manual E2E: local + global origin IFC from GIMMAPS
- [ ] Confirm no `IfcSite.RefLatitude` in output (`ifcopenshell` or text grep)
