# RFC 0007 — Line-interval stitching across KLIC deliveries (phase 2)

**Status:** Draft. Companion docs: `docs/plans/0010-klic-network-payload.plan.md`
(phase 1 = delivery-keyed replacement), `docs/guides/0008-klic-imkl-files.md`
(KLIC background), CoreWebApi `docs/adr/0012-klic-network-flows-persistence.md`
(phase-1 schema).

## Context

Phase 1 treats a KLIC delivery as a replaceable unit (same meldnummer + higher
leveringsvolgnummer → replace; different meldnummern coexist). That is simple and
correct, but it double-draws the same physical cable when two dig requests overlap in
one area, and it keeps no time dimension. This RFC designs the phase-2 upgrade: a
temporal line model where one physical line can have multiple validity-interval
records stitched across deliveries.

The schema is already forward-compatible: phase 1 adds indexed `Meldnummer` /
`Leveringsvolgnummer` columns (nullable, null for non-KLIC lines) and stores
`sourceGmlId` in per-line metadata.

## Proposed model

- **Sentinel end date.** `validTo` unknown/nil → `9999-12-31` (map DB convention:
  "always displayed unless known to end"; end date exclusive).
- **Line identity.** `sourceGmlId` primary; geometry-hash fallback. Identity
  resolution failure is always the conservative outcome: the line is new, never
  guessed. (Answers review Q3: identity instability across deliveries is accepted
  risk; the fallback exists, matching failure never merges.)
- **Older dig request (same line seen again).** Prepend a history record: from
  `new.validTo` until the existing record's (or the minimum of the same line's
  records') `startDate`.
- **Newer dig request (same line seen again).** Close the current record at
  `new.validTo` (exclusive) and open a new record `[validTo, 9999-12-31]`.
- **Absence-ending.** When delivery M′ supersedes delivery M, end M's records that
  are *not* present in M′, scoped to the **polygon intersection of the two
  deliveries** (absence in a non-overlapping delivery is not removal). One indexed
  query per import via the delivery-key columns — never a global inverse search.
  (Answers review Q1.)
- **Staleness reject stays within the same meldnummer only.** An older *different*
  meldnummer is legitimate history, not staleness.
- **Scope.** Phase 2 applies to KLIC deliveries first; extending it to non-KLIC user
  lines (which have null delivery keys) is an open question for the honing stage.

## Open questions (to close before ADR promotion)

1. Identity: how stable is `sourceGmlId` across operators/re-deliveries in practice?
   What granularity for the geometry-hash fallback (exact vs tolerance)?
2. Absence-ending semantics when two deliveries partially overlap and the newer one
   is *smaller*: is ending scoped records correct, or should only full-coverage
   supersession end anything?
3. Does the prepend-history rule apply when an older delivery arrives *after* a
   newer one (out-of-order imports), or only in chronological order?
4. Does stitching extend to non-KLIC user-uploaded network lines, and if so, what is
   their identity (they have no meldnummer)?
5. UI/render behavior for historical intervals (display only the current interval?
   toggle for history?).

## Rejected alternatives

- Keep meldnummer-replace forever: double-draws overlapping-delivery lines and
  discards history; rejected as the end state, kept as phase 1 for shipping speed.
- Per-line content-identity diffing without time dimension: changes data in place,
  loses the temporal record; the interval model supersedes it.

## Forward compatibility

Phase-1 import code needs no changes when phase 2 lands: the stitching pass is an
additional post-import step keyed on the same columns; phase-1 records read as
`[validFrom, 9999-12-31]`-style intervals once defaults are applied.
