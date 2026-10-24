# RFC 0008 — Reader strictness tiers + dropped-geometry observability (phase 2)

**Status:** Draft. Companion docs:
`docs/plans/0010-klic-network-payload.plan.md` (phase 1), `docs/guides/0008-klic-imkl-files.md`,
`docs/rfcs/0007-line-interval-stitching.md` (independent phase-2 track).

## Context

The KLIC reader family (A1 IMKL sink, A4 generic GML/KML sink) must treat file
families differently: one is a published government standard, the other is arbitrary
user-drawn linework. Phase 1 deliberately ships the narrow version: 2D-only reads,
per-geometry abort on malformed tokens with a lightweight `GimLogger.Warn`,
no failure-reporting machinery. This RFC holds the deferred design so it's not lost.

## Principle (the part worth promoting to an ADR)

**Strictness equals the strength of the contract.**

- **KLIC / IMKL = contract tier.** Published schema, fixed feature names, fixed date
  formats, exact theme↔geometry pairing. "Malformed" is objective there; a corrupted
  record is not "creative input". Behavior: the affected *geometry* aborts (returns
  null) with one warn; the rest of the delivery continues; structural XML failure
  (rank-1 well-formedness) fails loudly rather than importing half a delivery
  silently.
- **Generic GML / KML = best-effort tier.** No standard to violate; the reader never
  judges intent — extract what parses, skip what doesn't, and never torch the whole
  file over one bad geometry.

## Deferred design items

1. **Stride inference (generic tier).** Dimensionless posList: try stride 2; if the
   token count does not divide by 2 but divides by 3 → assume 3-tuples (log the
   inference once per geometry); neither divides → abort that geometry. Removes the
   "user draws mixed 2D/3D tuples" trap and the silent lon/lat mispairing class.
2. **KML mixed-tuple handling.** The KML OGC spec allows mixing 2-tuples and
   3-tuples within one coordinate list; inference must be per-tuple-run, not
   per-file. Includes a spec test.
3. **Observability: `DroppedGeometryCount`.** A field on `NetworkPayloadResult`
   (count of geometries skipped for malformed content). TestUI prints it; GIMmanager
   logs it on the hub events; candidate to persist as a small number on the import
   (B2 side). Answers observationally "how would we know a file half-parsed" without
   heuristics about intent.
4. **Scope of the warn-on-drop.** Phase 1 keeps the single lightweight warn per
   dropped geometry as the only noise; part of this RFC is to decide whether the
   contract tier should escalate repeated malformedness to a louder signal
   (e.g. delivery-level flag) once observability lands.

## Interaction with other deferred work

Independent of RFC 0007 (delivery stitching): stitching is a persistence-layer
upgrade; this RFC is a reader-behavior upgrade. They may land in either order; both
assume the phase-1 payload shape stays stable (`SourceCrs`, lean metadata,
metadata-only width).

## Rejected alternatives

- Treat both tiers with the same strictness (fails user sketches on pedantic
  conformance tests that don't matter in a best-effort context).
- Treat both best-effort (would silently accept corrupted official deliveries; a
  half-imported KLIC delivery that displays "complete" is worse than a loud failure).
