# RFC 0003: 2D Curve Builders & Adapters — Design

**Status:** Active  
**References:** [RFC 0001](0001-cursor_2d_element_type_refactoring.md), [RFC 0002](0002-2d_curve_builders_adapters_handoff.md)

This document records the **design choices** for implementing 2D curve builders and adapters on top of the post-refactor element layouts.

---

## 1. Context

2D curve types (`C_*2D`) mirror their 3D counterparts:

- No 6-double `startX/Y/Z`, `endX/Y/Z` prefix on analytic types
- Placement via `GimLocal` (2D fields only; see §3)
- `elementDoubles` hold only geometric params (same indices as 3D where applicable)

New code must use **ElementBuilders** / **ElementAdapters** per `docs/specs/gimdata/README.md` §8 and TD-005 substep (1).

---

## 2. Architectural decisions (grill-me)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Base adapter | New `AbstractCurve2DAdapter : AbstractElementAdapter` | Clean 2D layer; do **not** extend `AbstractCurveAdapter` (3D-only contract) |
| UV points | `Vect` with `Length == 2`; use `.X`/`.Y` or `[0]`/`[1]` | Variable-length `Vect` already used for 2D in `DetectProfiles`, `Shrinkwrap`; no new `Uv` struct |
| `Vect` origin write | `SetLocalBasis(local)` when `Length == 2` → `Px, Py`, `Pz = 0` | Lets line/offset builders mirror 3D fluency |
| `Vect` direction write | `ApplyToLocal(local)` when `Length == 2` → `Zx, Zy`, `Zz = 0` | Used by `C_line2D`, `C_offset2D` |
| `Vect` U/V axis write | `ApplyAsDirectionU(local)`, `ApplyAsDirectionV(local)` | Sets `Xx/Xy`, `Yx/Yy`; row-local Z components are 0 and omitted Z row is `int.MinValue` |
| `Vect` local read | `FromLocalOrigin2D`, `FromLocalDirectionU/V`, `FromLocalDirection` | Symmetric with writes; adapters avoid `new Vect(local, index)` (always 3D) |
| `Vect` guards | Throw `InvalidGimOperation` on `.Z`, `^`, `GetNormalPlane` when `Length < 3` | Fail fast; do **not** silent-default `.Z` to 0 |
| Builder entry API | Both `(double u, double v)` and `Vect` overloads on every builder | Ergonomic literals + type-safe `Vect`; validate `uv.Length == 2` |
| Conic U/V axes | Required explicit directions (no identity defaults) | Local frame **is** the geometry; silent `(1,0)/(0,1)` is wrong on rotated faces |
| Tests | One test file per type; `VectorTests` for `Vect` infra; `Curve2DTypeTests` for cross-cutting layout/scaling/`IsDoubleDistance` | Mirrors 3D `*CurveTests.cs` pattern |
| Namespace | `GIMcompress.ElementHelpers` | Same as 3D builders/adapters |

---

## 3. `GimLocal` convention for 2D curves

| Field | 2D meaning |
|-------|-----------|
| `Px, Py` | UV origin (base point in 2D space) |
| `Pz` | 0 (unused) |
| `Xx, Xy` | U-axis direction vector, or `int.MinValue` when the X row is omitted |
| `Xz` | 0 when X row is present; `int.MinValue` when omitted |
| `Yx, Yy` | V-axis direction vector, or `int.MinValue` when the Y row is omitted |
| `Yz` | 0 when Y row is present; `int.MinValue` when omitted |
| `Zx, Zy` | Direction vector (`C_line2D`, `C_offset2D`), or `int.MinValue` when the Z row is omitted |
| `Zz` | 0 when Z row is present as a 2D direction; `int.MinValue` when omitted |

**Population rules:**

- **Origin:** `_center.SetLocalBasis(local)` or `Vect.FromLocalOrigin2D(local)` to read
- **Line / offset direction:** `direction.ApplyToLocal(local)` or `Vect.FromLocalDirection(local)`; X/Y rows are omitted with `int.MinValue`
- **Circle / ellipse / hyperbola / parabola axes:** `dirU.ApplyAsDirectionU(local)`, `dirV.ApplyAsDirectionV(local)`; read via `FromLocalDirectionU/V`; Z row is omitted with `int.MinValue`
- **Splines (bezier/bspline 2D):** `Locals = [new GimLocal()]` only; geometry lives in `elementDoubles`

Do not use `Matrix.ApplyToLocal` for 2D conics — there is no 3D cross product in UV space. `Circle2DBuilder` uses 2D perpendicular math for `WithAngleRange` rotation.

---

## 4. `Vect` 2D infrastructure

`Vect` (`GIMCompress/MatrixCalculus.cs`) is the shared 2D/3D vector type.

### What works at `Length == 2`

`Norm`, `Direction`, `+`, `-`, `*`, `/`, `==`, dot product, `IsCollinearTo` (non-3D branch), indexing, `.X`/`.Y`.

### What is guarded at `Length < 3`

`.Z`, `operator ^`, `GetNormalPlane`, and any API that assumes three components.

### 2D-aware write/read API

| Method | `Length == 2` behavior |
|--------|------------------------|
| `SetLocalBasis` | `Px=[0], Py=[1], Pz=0` |
| `ApplyToLocal` | Omit X/Y rows with `int.MinValue`, `Zx=[0], Zy=[1], Zz=0` |
| `ApplyAsDirectionU` | `Xx=[0], Xy=[1], Xz=0`; omit Z row with `int.MinValue` |
| `ApplyAsDirectionV` | `Yx=[0], Yy=[1], Yz=0`; omit Z row with `int.MinValue` |
| `FromLocalOrigin2D` | `new Vect(Px, Py)` |
| `FromLocalDirectionU` | `new Vect(Xx, Xy)` |
| `FromLocalDirectionV` | `new Vect(Yx, Yy)` |
| `FromLocalDirection` | `new Vect(Zx, Zy)` |

Mixing 2D and 3D `Vect` in arithmetic throws (length mismatch) — intentional.

---

## 5. `AbstractCurve2DAdapter`

Intermediate base for all 2D curve adapters.

| Member | Semantics |
|--------|-----------|
| `ParamFirst`, `ParamLast` | Parametric domain (meaning varies by type; splines derive from knots or stored values) |
| `GetStartPoint`, `GetEndPoint` | Return `Vect` with `Length == 2` |
| `GetStartAndEndPoints` | Tuple helper |
| `IsClosed` | Type-specific |
| `IsDegenerate` | Default: open curve with coincident start/end; conics override |

Concrete adapters set `ExpectedType` and expose type-specific properties (`Radius`, `GetDirectionU`, `PointCoords`, etc.).

---

## 6. Shared builder conventions

Every 2D builder:

- Implements `IElementBuilder` and exposes `BuildAdapter()` returning the matching `Gim*2DCurve`
- Offers `(double, double)` overloads delegating to `Vect` overloads
- Validates `Vect.Length == 2` with `InvalidGimElementException`
- Uses `InvalidGimElementException` for geometric validation (null direction, collinear axes, bad params)
- Mirrors the **3D builder** of the same family for fluent interface shape and validation rules

---

## 7. Type layouts

| Type | ID | `GetDoubleCount` | `elementDoubles` layout | 3D mirror |
|------|----|------------------|-------------------------|-----------|
| `C_bezier2D` | 40 | -1 | `[0]startU, [1]startV, [2]endU, [3]endV, [4+]poles(2/pole), [last2]aParamFirst, aParamLast` | `C_bezier` |
| `C_bspline2D` | 41 | -1 | `[0..3]start/end UV, [4+]poles(2/pole), knots, weights` | `C_bspline` |
| `C_circle2D` | 42 | 3 | `[0]radius, [1]aParamFirst, [2]aParamLast` | `C_circle` |
| `C_elipse2D` | 43 | 4 | `[0]MajorRadius, [1]MinorRadius, [2]aParamFirst, [3]aParamLast` | `C_elipse` |
| `C_hyperbol2D` | 44 | 4 | `[0]aParamFirst, [1]aParamLast, [2]MajorRadius, [3]MinorRadius` | `C_hyperbol` |
| `C_line2D` | 45 | 2 | `[0]aParamFirst, [1]aParamLast` | `C_line` |
| `C_parabol2D` | 46 | 3 | `[0]aParamFirst, [1]aParamLast, [2]Focal` | `C_parabol` |
| `C_offset2D` | 47 | 1 | `[0]offset` | `C_offset` |
| `C_trimmed2D` | 48 | 2 | `[0]aParamFirst, [1]aParamLast` | `C_trimmed` |

**Bspline2D params:** Not stored explicitly. `ParamFirst = knot[degree]`, `ParamLast = knot[n - degree - 1]` (clamped knot vector).

> **Note:** `GimBSplineCurve` (3D) previously used `Knots[0]` / `Knots[^1]` for `ParamFirst`/`ParamLast` — a bug for non-symmetric clamped vectors. Fixed in RFC 0004 Phase 1. `GimBSpline2DCurve` must use the degree-index formula from day one.

**Bspline2D `elementInts`:** Same as 3D — `[0] knot count, [1..] multiplicities, [last] degree`.

**Bezier2D `elementInts`:** `[0] pole count`.

---

## 8. Per-type design

### `C_line2D`

- **Params:** Normalized to `[0, length]` like 3D `LineBuilder` (param shifts move origin along direction; inverted params flip direction).
- **Local:** `_start.SetLocalBasis(local)`; `direction.ApplyToLocal(local)`.
- **Adapter:** `Length = |paramLast - paramFirst|`; `GetDirection()` via `FromLocalDirection`; start = origin, end = origin + length × direction.
- **3D reference:** `LineBuilder.cs`, `GimLineCurve.cs`.

### `C_circle2D`

- **Params:** `[radius, 0, |angle|]` after `WithAngleRange` normalization.
- **Local:** center `SetLocalBasis`; `ApplyAsDirectionU/V` for axes; `WithAngleRange` rotates U/V in 2D (handedness sign, not `Vect ^`).
- **Adapter:** `PointAt(angle)` = center + r·(cos·U + sin·V); `IsClosed` when range covers 2π; `GetCenter`, `GetDirectionU/V`.
- **Axes:** Required explicit U and V — no default frame.
- **3D reference:** `CircleBuilder.cs`, `GimCircleCurve.cs`.

### `C_trimmed2D`

- **Params only** in `elementDoubles`; child curve is a separate element in the tree.
- **Local:** `[new GimLocal()]`.
- **Adapter:** `ParamFirst`/`ParamLast` only; `GetStartPoint`/`GetEndPoint` throw `InvalidGimOperation`.
- **3D reference:** `TrimmedCurveBuilder.cs`, `GimTrimmedCurve.cs`.

### `C_offset2D`

- **Params:** `[offset]`; direction in local `Zx/Zy`.
- **Local:** `direction.ApplyToLocal(local)`.
- **Adapter:** `Offset`; `GetDirection()`; end = origin + offset × direction; `ParamFirst`/`ParamLast` return 0.
- **3D reference:** `OffsetCurveBuilder.cs`, `GimOffsetCurve.cs`.

### `C_elipse2D`

- **Params:** `[majorR, minorR, paramFirst, paramLast]` — stored as-given.
- **Local:** `SetLocalBasis` + `ApplyAsDirectionU/V`; required explicit axes.
- **Adapter:** `PointAt(t)` = center + major·cos(t)·U + minor·sin(t)·V; `IsClosed` at 2π span.
- **3D reference:** `EllipseBuilder.cs`, `GimEllipseCurve.cs`.

### `C_hyperbol2D`

- **Params:** `[paramFirst, paramLast, majorR, minorR]`.
- **Adapter:** `EvaluateAt(t)` = P + major·cosh(t)·U + minor·sinh(t)·V; never closed.
- **3D reference:** `HyperbolaBuilder.cs`, `GimHyperbolaCurve.cs`.

### `C_parabol2D`

- **Params:** `[paramFirst, paramLast, focal]`.
- **Adapter:** `EvaluateAt(t)` = P + t²/(4F)·U + t·V; focal ≠ 0.
- **3D reference:** `ParabolaBuilder.cs`, `GimParabolaCurve.cs`.

### `C_bezier2D`

- **Poles:** `Vect[]` with `Length == 2` each; interleaved UV from index 4.
- **Adapter:** `StartPoint`/`EndPoint` as `ArraySegment<double>` length 2; `PoleCoords` at 4; params at `[^2]`/`[^1]`.
- **3D reference:** `BezierBuilder.cs`, `GimBezierCurve.cs`.

### `C_bspline2D`

- **Adapter `PointCount`:** `(elementDoubles.Length - 4 - KnotCount) / 3`
- **Segments:** header at 0–3; poles at 4; knots and weights after (mirror 3D with 4-header instead of 6).
- **3D reference:** `BSplineBuilder.cs`, `GimBSplineCurve.cs`.

---

## 9. `IsDoubleDistance` semantics

| Type | Rule | vs 3D |
|------|------|-------|
| `C_line2D` | `true` for all indices | 3D `C_line` is `false` |
| `C_circle2D` | `i == 0` | same |
| `C_elipse2D` | `i < 2` | same |
| `C_hyperbol2D` | `i == 2 \|\| i == 3` | same |
| `C_parabol2D` | `i == 2` | same |
| `C_offset2D` | `true` | 3D `C_offset` is `false` |
| `C_trimmed2D` | `false` (deferred) | child-dependent logic |
| `C_bezier2D` | `i < length - 2` | UV + poles scale; params do not |
| `C_bspline2D` | `i < 4 + 2 × PointCount` | UV + poles scale; knots/weights do not |

---

## 10. `ScaleChanger` semantics (2D splines)

Fixed-layout 2D types are handled in `ScaleChanger.ScaleCurve`. For splines:

- Scale start/end UV (`[0..3]`) and pole coordinates via adapter `PointCoords`
- Do **not** scale knots, weights, or bezier param tail

---

## 11. Out of scope for this design

- DWG/DXF reader migration (`DWGRead2D.cs`) — TD-005 substeps 3–5
- `CurveHelpers.getStartAndEndPointsOfCurve` 2D dispatch
- OCCT/IFC reader migration to builders
- `C_trimmed2D` child-aware `IsDoubleDistance`
