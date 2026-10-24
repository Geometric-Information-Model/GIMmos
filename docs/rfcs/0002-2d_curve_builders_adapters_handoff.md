# Handoff: 2D Curve Builders & Adapters

**Repo:** `GIMmos`  
**Prerequisite PR:** 2D element type layout refactoring (this doc's companion)  
**Goal:** Create `ElementBuilders/Curves/*2D` and `ElementAdapters/Curves/*2D` mirroring the 3D set. Unblocks TD-005 substep (1).

---

## 1. Context

The 2D element types have been refactored to mirror their 3D counterparts:
- No more 6-double `startX/Y/Z`, `endX/Y/Z` prefix
- Placement via `GimLocal` (same struct, 2D fields only)
- `elementDoubles` hold only geometric params (same indices as 3D)

This PR creates the builders and adapters to work with the new layouts.

---

## 2. GimLocal convention for 2D curves

| Field | 2D meaning |
|-------|-----------|
| `Px, Py` | UV origin (base point in 2D space) |
| `Pz` | 0 (unused) |
| `Xx, Xy` | U-axis direction vector |
| `Xz` | 0 (unused) |
| `Yx, Yy` | V-axis direction vector |
| `Yz` | 0 (unused) |
| `Zx, Zy` | Direction vector (used by `C_line2D`, `C_offset2D`) |
| `Zz` | 0 (unused) |

---

## 3. Type layouts (post-refactoring)

| Type | GetDoubleCount | Layout | Mirrors |
|------|---------------|--------|---------|
| `C_line2D` (45) | 2 | `[0]aParamFirst, [1]aParamLast` | `C_line` |
| `C_circle2D` (42) | 3 | `[0]radius, [1]aParamFirst, [2]aParamLast` | `C_circle` |
| `C_elipse2D` (43) | 4 | `[0]MajorRadius, [1]MinorRadius, [2]aParamFirst, [3]aParamLast` | `C_elipse` |
| `C_hyperbol2D` (44) | 4 | `[0]aParamFirst, [1]aParamLast, [2]MajorRadius, [3]MinorRadius` | `C_hyperbol` |
| `C_parabol2D` (46) | 3 | `[0]aParamFirst, [1]aParamLast, [2]Focal` | `C_parabol` |
| `C_offset2D` (47) | 1 | `[0]offset`; direction = `Zx, Zy` of local | `C_offset` |
| `C_trimmed2D` (48) | 2 | `[0]aParamFirst, [1]aParamLast` | `C_trimmed` |
| `C_bezier2D` (40) | -1 | `[0]startU, [1]startV, [2]endU, [3]endV, [4+]poles(2/pole), [last2]aParamFirst, aParamLast` | `C_bezier` |
| `C_bspline2D` (41) | -1 | `[0]startU, [1]startV, [2]endU, [3]endV, [4+]poles(2/pole), knots, weights` | `C_bspline` |

**Bspline2D note:** Start param = `knot[degree]`, end param = `knot[n-degree-1]` (clamped knot vector). No explicit params stored.

---

## 4. Remaining work from the layout PR

### ScaleChanger: bspline2D / bezier2D

`ScaleChanger.ScaleCurve` currently skips `C_bspline2D` and `C_bezier2D` with a `return` and TODO comment. Once adapters exist:
- Scale all pole coordinates (2 per pole) and start/end UV points ([0..3])
- Do NOT scale knots or weights
- Create a `ScaleBSplineCurve2D` method or generalize the existing one

### IsDoubleDistance: all 2D types

`GimElement.IsDoubleDistance` currently returns `false` for all 2D types with a TODO comment. When adapters are created, update to match 3D counterpart logic:
- `C_line2D`: `true` (params for line = distance)
- `C_circle2D`: `i == 0` (radius)
- `C_elipse2D`: `i < 2` (both radii)
- `C_hyperbol2D`: `i == 2 || i == 3` (both radii)
- `C_parabol2D`: `i == 2` (focal)
- `C_offset2D`: `true` (offset is not a spatial distance from origin)
- `C_trimmed2D`: This one is weird; If the child of the trimmed curve is a curve whose length/size depends on its param (e.g. line), then true. otherwise false. Special care should be taken when dealing with this curve, however for all intents and purposes, it doesn't have to be implemented right off the bat.
- `C_bezier2D`: start/end UV + poles are distances; last 2 (params) are not
- `C_bspline2D`: start/end UV + poles are distances; knots/weights are not

---

## 5. Builders to create

Mirror the 3D builder set. Reference implementations:
- `GIMCompress/entities/ElementBuilders/Curves/LineBuilder.cs`
- `GIMCompress/entities/ElementBuilders/Curves/CircleBuilder.cs`
- `GIMCompress/entities/ElementBuilders/Curves/TrimmedCurveBuilder.cs`

| Builder | Key API |
|---------|---------|
| `Line2DBuilder` | `.From(u, v).To(u2, v2).BuildElement()` or `.From(u, v).WithDirection(du, dv).WithLength(L)` |
| `Circle2DBuilder` | `.FromCenter(u, v).WithRadius(r).WithDirectionU(du, dv).WithDirectionV(du, dv).WithAngleRange(t1, t2)` |
| `Ellipse2DBuilder` | `.FromCenter(u, v).WithMajorRadius(r1).WithMinorRadius(r2).WithAngleRange(t1, t2)` |
| `Hyperbola2DBuilder` | `.FromCenter(u, v).WithMajorRadius(r1).WithMinorRadius(r2).WithParams(t1, t2)` |
| `Parabola2DBuilder` | `.FromCenter(u, v).WithFocal(f).WithParams(t1, t2)` |
| `Offset2DBuilder` | `.WithOffset(d).WithDirection(du, dv)` (child: base curve) |
| `Trimmed2DBuilder` | `.WithParams(t1, t2)` (child: base curve) |
| `BSpline2DBuilder` | `.WithStartEnd(sU, sV, eU, eV).WithPoles(...).WithWeights(...).WithKnots(...).WithMultiplicities(...)` |
| `Bezier2DBuilder` | `.WithStartEnd(sU, sV, eU, eV).WithPoles(...).WithParams(t1, t2)` |

---

## 6. Adapters to create

Mirror the 3D adapter set. Reference implementations:
- `GIMCompress/entities/ElementAdapters/Curves/GimLineCurve.cs`
- `GIMCompress/entities/ElementAdapters/Curves/GimCircleCurve.cs`
- `GIMCompress/entities/ElementAdapters/AbstractCurveAdapter.cs`

| Adapter | Key properties |
|---------|---------------|
| `GimLine2DCurve` | `ParamFirst`, `ParamLast`, `Length`, `GetDirection()` (from local Zx/Zy) |
| `GimCircle2DCurve` | `Radius`, `ParamFirst`, `ParamLast`, `GetCenter()`, `GetDirectionU()`, `GetDirectionV()` |
| `GimEllipse2DCurve` | `MajorRadius`, `MinorRadius`, `ParamFirst`, `ParamLast` |
| `GimHyperbola2DCurve` | `MajorRadius`, `MinorRadius`, `ParamFirst`, `ParamLast` |
| `GimParabola2DCurve` | `Focal`, `ParamFirst`, `ParamLast` |
| `GimOffset2DCurve` | `Offset`, `GetDirection()` |
| `GimTrimmed2DCurve` | `ParamFirst`, `ParamLast` |
| `GimBSpline2DCurve` | `StartPoint` (UV), `EndPoint` (UV), `PointCoords` (ArraySegment), `Knots`, `Weights`, `Degree` |
| `GimBezier2DCurve` | `StartPoint` (UV), `EndPoint` (UV), `PointCoords` (ArraySegment), `ParamFirst`, `ParamLast` |

---

## 7. Tests

Location: `TestPlatform/ElementTests/Curves/`

For each 2D type:
- Builder produces correct `ElementType`, `elementDoubles`, and `Locals`
- Adapter reads back all properties correctly from a built element
- Adapter rejects wrong element types with `InvalidGimElementException`
- Round-trip: build → adapt → verify all values

---

## 8. TD-005 cross-reference

This work completes **TD-005 substep (1)**. After this PR:
- Substep (1): DONE (2D builders/adapters exist)
- Substep (2): DONE (Seam type, prior PR)
- Substeps (3)-(5): DWG modularization, migration to builders/adapters, fix semantics
- Substep (6): Re-enable SvgWriter

---

## 9. Additional notes

- `CurveHelpers.checkSplineIsFlat` is now 3D-only. If needed for 2D in future, implement using the 2D adapter's pole access (2 coords per pole, different offset).
- `CurveHelpers.getStartAndEndPointsOfCurve` does not handle 2D types (returns `null`). If needed in future, implement via adapters.
- The DWG reader (`DWGRead2D.cs`) and SVG writer (`SvgWriter.cs`) still use the OLD layout with hardcoded indices. When re-enabled (substeps 3-5), they must be rewritten to use the new builders/adapters.
