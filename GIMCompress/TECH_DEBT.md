# Technical debt ledger (GIMCompress)

## Purpose

This file is a **living backlog** of known structural issues around raw GimElement manipulation. It is not blame-oriented; it exists so changes can move incrementally toward the Builder/Adapter pattern.

**Policy:** Legacy files may keep existing patterns until refactored. **New** components and features should use **ElementBuilders** and **ElementAdapters** (see `../docs/specs/gimdata/README.md` section 8). Do not copy raw array manipulation patterns into greenfield code.

**Exemption:** GIMbin readers/writers and other binary serialization code that must work with raw arrays are exempt from migration.

## How to use

- When you touch an area (reader, writer, CurveHelpers, etc.), scan the **catalog** for related row IDs (**TD-00n**).
- Prefer **small, reviewable PRs** over big-bang refactors unless a change request explicitly needs a larger slice.
- When you fix or materially improve an item, update the row **Notes** and optionally link the PR or issue.

## Catalog

| ID     | Area           | Problem                                                                                        | Example location(s)                                                                                                                  | Risk / impact                                                                                 | Target direction                                                                                                                                                                                                                                                                                                                             | Notes                                    |
| ------ | -------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| TD-001 | Readers        | Raw element array manipulation in format readers.                                              | `GIMtransfer/read/GIMocctRead/GIMocctCurveReader.cs`, `GIMocctSurfaceReader.cs`, `read/CadexReadElements.cs`, `read/IFCReader/IFCReadElements.cs`, `read/DWGReader/DWGReadElement.cs` | Hard to maintain; index-math duplicated across formats.                                       | Migrate to **ElementBuilders**: readers extract parameters from source format, pass to builder, builder produces GimElement.                                                                                                                                                                                                              | DWG reader is the largest quarantined example; see **TD-005**. |
| TD-002 | Writers        | Raw element array manipulation in format writers.                                              | `GIMtransfer/write/GIMocctWrite/GIMocctCurveWriter.cs`, `GIMocctSurfaceWriter.cs`, `write/CadexWriteElements.cs`                     | Same index-math duplicated; periodic/aperiodic checks repeated.                               | Migrate to **ElementAdapters**: writers wrap element in adapter, read via named properties and ArraySegments.                                                                                                                                                                                                                            |                                          |
| TD-003 | Helpers        | Scattered bspline/curve index-math in helper and analysis files.                               | `GIMCompress/CurveHelpers.cs`, `DetectProfiles.cs`, `BRepSimplifier.cs`, `DecompressProfiles.cs`                                      | Same layout knowledge repeated in N places; type-dispatch switch on elementType.               | Migrate to **ElementAdapters**: use `AbstractCurveAdapter` methods (`GetStartPoint`, `GetEndPoint`, `GetStartAndEndPoints`) and concrete adapter properties.                                                                                                                                                                               |                                          |
| TD-004 | Legacy factory | `GimElement.GetLineFromTwoPoints()` duplicates builder logic.                                  | `GIMCompress/entities/GimElement.cs` lines 282-307                                                                                    | Two ways to construct a line; one bypasses validation present in builder.                      | Replace callers with `LineBuilder.From(start).To(end).BuildElement()`.                                                                                                                                                                                                                                                                  | Do not modify GimElement.cs yet.         |
| TD-005 | Readers / 2D   | Intern-supervised DWG/DXF import and SVG export merged in `c53394f` without builder/adapter patterns; monolithic files; some incorrect element layouts; raw `GimElement` arrays. | `GIMtransfer/read/DWGReader/**`, `GIMtransfer/write/SvgWriter.cs`, `GIMtransfer/GimSpaces/FindSpaceStructure.cs` | Incorrect geometry if enabled; high maintenance cost; anti-pattern copy-paste.                | **(1)** Prerequisite: 2D curve builders/adapters (`ElementBuilders/Curves/*2D`, `ElementAdapters/Curves/*2D`, mirror 3D set; tests in `TestPlatform/ElementTests/Curves/`). ~~**(2)** Prerequisite: dedicated seam-edge `GimElementType`~~ **(2) done:** `Seam = 29`, `GimData.MarkCurveAsSeam`, GIMbin V1 migration + V2 write. **(3)** Modularize DWG reader (split `DWGRead2D`). **(4)** Migrate to ElementBuilders/Adapters. **(5)** Fix element semantics vs `EntityHelpers`. **(6)** Refactor SvgWriter when re-enabled. | **Not in production use** — FileManager DWG import and SVG export quarantined. Safe to break logic; prefer compile. **Preserve** explicit `GimElementType` integer assignments from c53394f. Cross-link TD-001. Commit: `c53394f`. |

## Remediation phases

### Phase A — Stop the bleeding

- [ ] Do not add new raw `elementDoubles`/`elementInts`/`Locals` manipulation for curve or face types. Use builders and adapters.
- [ ] For **new** format support, construct elements via builders.
- [ ] For **new** format writing, read elements via adapters.
- [ ] Do not re-enable DWG/DXF import or SVG export until TD-005 substeps (1) and (3)–(5) are complete; do not copy `DWGRead2D` patterns.
- [ ] New `GimElementType` values must use explicit `= N` (see `../docs/specs/gimdata/README.md` enum integer stability rule).

### Phase B — Lazy migration (when touching files)

- [ ] When modifying a reader, replace raw element construction with builders (TD-001).
- [ ] When modifying a writer, replace raw array access with adapters (TD-002).
- [ ] When modifying CurveHelpers/DetectProfiles/etc., use adapter methods instead of manual index math (TD-003).
- [ ] When touching code that uses `GetLineFromTwoPoints`, switch to `LineBuilder` (TD-004).
- [ ] When touching DWG/SVG code, extract one entity type per PR toward TD-005 substeps (3)–(5).

### Phase C — Structural (optional, larger budget)

- [ ] Remove `GimElement.GetLineFromTwoPoints()` once all callers migrated.
- [ ] Deprecate direct `elementDoubles`/`elementInts` setters for curve/face types.
- [ ] Update enum comments in `GimElementType` to reference adapters as authoritative layout documentation.
- [ ] Re-enable FileManager DWG import and SVG export routes after TD-005 substeps (1)–(5) are materially complete.
