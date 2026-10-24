# RFC 0004: 2D Curves — Conics, Splines, and BSpline Param-Domain Fix

**Status:** Completed  
**References:** [RFC 0001](0001-cursor_2d_element_type_refactoring.md), [RFC 0002](0002-2d_curve_builders_adapters_handoff.md), [RFC 0003](0003-2d_curve_builders_pr1_design.md)

This RFC records the **design and delivery** of the second 2D-curve tranche: five remaining curve types (`Ellipse2D`, `Hyperbola2D`, `Parabola2D`, `Bezier2D`, `BSpline2D`), cross-cutting `IsDoubleDistance` / `ScaleChanger` completion for those types, and the 3D BSpline param-domain correction. It builds on RFC 0003, which delivered `Line2D`, `Circle2D`, `Trimmed2D`, and `Offset2D`.

With this work, all nine `C_*2D` curve types have builders, adapters, per-type tests, and `IsDoubleDistance` rules. Spline scaling is implemented via dedicated 2D helpers.

---

## 1. Scope delivered

### New builders and adapters

| Type | Builder | Adapter |
|------|---------|---------|
| `C_elipse2D` | `Ellipse2DBuilder` | `GimEllipse2DCurve` |
| `C_hyperbol2D` | `Hyperbola2DBuilder` | `GimHyperbola2DCurve` |
| `C_parabol2D` | `Parabola2DBuilder` | `GimParabola2DCurve` |
| `C_bezier2D` | `Bezier2DBuilder` | `GimBezier2DCurve` |
| `C_bspline2D` | `BSpline2DBuilder` | `GimBSpline2DCurve` |

Conics follow the `Circle2D` local-frame pattern (`Vect.FromLocal*` reads, `ApplyAsDirectionU/V` writes, explicit axes required). Splines mirror their 3D counterparts with a 4-double UV header instead of 6-double XYZ.

### Cross-cutting updates

- **`GimElement.IsDoubleDistance`** — inline rules for all five types (see §5).
- **`ScaleChanger`** — `ScaleBezierCurve2D` and `ScaleBSplineCurve2D` scale start/end UV and pole coordinates; knots, weights, and Bezier param tail are untouched.
- **`GimBSplineCurve` (3D)** — `ParamFirst` / `ParamLast` corrected to use degree-index knot positions (see §3).
- **RFC patches** — RFC 0002 knot-index typo fixed; RFC 0003 annotated with the 3D bug note.

### Tests

Per-type test files under `TestPlatform/ElementTests/Curves/`, plus extensions to `Curve2DTypeTests` and `BSplineCurveTests` (clamped-domain regression).

---

## 2. Design decisions

| Topic | Decision | Rationale |
|-------|----------|-----------|
| `Ellipse2D.WithAngleRange` | Store params as-given; no U/V rotation | Unlike `Circle2D`, ellipse params are independent of axis orientation; 3D ellipse normalizes end param upward — 2D does not |
| `Ellipse2D` radii | `Math.Max` / `Math.Min` at indices 0/1 | Major always at `[0]`, minor at `[1]` regardless of builder call order |
| Conic axis API | `WithDirectionU` + `WithDirectionV` only | 2D has no 3D cross-product shortcut; no `WithDirectionAndAxis` |
| `Hyperbola2D` radii | Store as-given; no swap | Differs from ellipse; matches hyperbola param layout intent |
| Adapter axis getters | `GetDirectionU()` / `GetDirectionV()` on all conics | Consistent with `GimCircle2DCurve`; avoids 3D `Matrix` row reads in UV space |
| Hyperbola / Parabola params | Store exactly; no wrap | Open curves; param range is author-defined |
| Parabola focal | Reject `== 0` only; allow negative | Matches 3D sign semantics without blocking negative focal |
| Spline `IsClosed` / `IsDegenerate` | Port 3D logic with pole stride 2 | Same coincidence rules as 3D Bezier/BSpline, adapted for 2 coords per pole |
| Spline `ScaleChanger` | Scale `[0..3]` + `PointCoords` | UV header and poles are spatial; knots, weights, and Bezier params are not |
| `BSpline2D PointCount` | `(Length − 4 − KnotCount) / 3` | Poles at index 4, 2 doubles each; knots and weights follow (see RFC 0003 §7) |
| BSpline param domain | `Knots[Degree]` / `Knots[KnotCount − Degree − 1]` | Correct clamped domain on an expanded knot vector; applied to 3D fix and 2D from day one |

---

## 3. BSpline param-domain fix

### Problem

`GimBSplineCurve` used `Knots[0]` and `Knots[^1]` for `ParamFirst` and `ParamLast`. On a non-symmetric clamped knot vector this yields the wrong interval.

Counterexample (degree 2): knots `{0, 0.1, 0.2, 1, 1, 1}` → correct domain `[0.2, 1]`, not `[0, 1]`. Symmetric padding `{0,0,0,1,1,1}` yields `0`/`1` under both formulas, which hid the bug in existing tests.

### Fix

```csharp
ParamFirst => Knots[Degree];
ParamLast  => Knots[KnotCount - Degree - 1];
```

Regression test `Adapter_ParamDomain_UsesClampedKnotIndices` locks this in `BSplineCurveTests`. `GimBSpline2DCurve` was implemented with the corrected formula from the start.

### `GimBSplineFace` audit

No change required. The face adapter has no param-domain getters; `UKnots`/`VKnots` are layout slices, and U/V periodicity is derived from multiplicity sums vs pole counts — not from treating first/last knot values as domain endpoints.

---

## 4. Per-type design

**Canonical 2D pattern:** `Circle2DBuilder` / `GimCircle2DCurve` (RFC 0003).

### `C_elipse2D`

- **Layout:** `[majorR, minorR, paramFirst, paramLast]`
- **Builder:** `FromCenter` → `WithRadii` → `WithDirectionU` → `WithDirectionV` → `WithAngleRange` / `WithTotalAngle`
- **Adapter:** `PointAt(t) = center + major·cos(t)·U + minor·sin(t)·V`; `IsClosed` when span is 2π; `IsDegenerate` when both radii are zero on a closed ellipse

### `C_hyperbol2D`

- **Layout:** `[paramFirst, paramLast, majorR, minorR]`
- **Adapter:** `EvaluateAt(t) = P + major·cosh(t)·U + minor·sinh(t)·V`; never closed

### `C_parabol2D`

- **Layout:** `[paramFirst, paramLast, focal]`
- **Adapter:** `EvaluateAt(t) = P + t²/(4F)·U + t·V`; builder rejects `focal == 0`

### `C_bezier2D`

- **Layout:** `[0..3]` start/end UV, `[4+]` poles (2 per pole), `[^2]`/`[^1]` params; `elementInts[0]` = pole count
- **Local:** `[new GimLocal()]` only; geometry in `elementDoubles`

### `C_bspline2D`

- **Layout:** `[0..3]` start/end UV, `[4+]` poles (2 per pole), knots, weights; `elementInts` same as 3D
- **PointCount:** `(elementDoubles.Length - 4 - KnotCount) / 3`
- **Periodicity:** same detection logic as `GimBSplineCurve`, with 2-double pole stride

---

## 5. `IsDoubleDistance`

Rules added in `GimElement` for the five types delivered in this scope:

| Type | Rule |
|------|------|
| `C_elipse2D` | `i < 2` (both radii) |
| `C_hyperbol2D` | `i == 2 \|\| i == 3` (radii only; params at 0/1 are not distances) |
| `C_parabol2D` | `i == 2` (focal only) |
| `C_bezier2D` | `i < length - 2` (UV header + poles; param tail excluded) |
| `C_bspline2D` | `i < 4 + 2 × ((length - 4 - elementInts[0]) / 3)` (UV header + poles; knots/weights excluded) |

Types from RFC 0003 (`Line2D`, `Circle2D`, `Offset2D`, `Trimmed2D`) were already implemented; see RFC 0003 §9 for their rules.

---

## 6. Out of scope

The following items were explicitly **not** part of this RFC. They remain future work under TD-005 and related tracks.

### DWG / SVG reader and writer migration

`DWGRead2D.cs` and `SvgWriter.cs` still use the **legacy** 2D layout (hardcoded indices, pre-refactor start/end doubles). Re-enabling them requires rewriting against the new builders/adapters (TD-005 substeps 3–6). Touching these files during builder work risks regressions in disabled code paths.

### `C_trimmed2D` child-aware `IsDoubleDistance`

`C_trimmed2D` currently returns `false` for all indices. A correct implementation depends on the child curve type (e.g. distance-like for a trimmed line, not for a trimmed circle arc). Deferred until there is a concrete consumer and a clear child-dispatch contract.

### `CurveHelpers.getStartAndEndPointsOfCurve` 2D dispatch

This helper returns `null` for 2D curve types. Adding 2D support means dispatching through the `Gim*2DCurve` adapters; no caller in this scope required it.

### `CurveHelpers.checkSplineIsFlat` for 2D

Remains 3D-only. A 2D variant would use adapter pole access (2 coords per pole, different header offset) if flatness checks are needed on face-boundary splines.

### OCCT / IFC reader migration

3D readers that still manipulate `elementDoubles` directly are outside the 2D builder/adapters track documented here.

### Broader `IsDoubleDistance` gaps

Some 3D types (e.g. `C_trimmed`, faces) still carry TODO or approximate rules unrelated to this 2D delivery.
