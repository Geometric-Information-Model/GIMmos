# ADR 0004 — KLIC/network payload import through a dedicated FileAccess entrypoint (no GimData)

Date: 2026-10-17 · Status: Accepted · Deciders: user + fleet grill session

## Context

Users import KLIC deliveries (IMKL 2.0 / WIBON GML, Kadaster-minted) and generic GML/KML/XML
files. The product outcome is 2D network lines persisted in CoreWebApi's PostGIS
`NetworkDbContext` and rendered in GIMpro/GIMmaps — deliberately **not** GimData objects:
no 3D model, no proprietary-binary serialization, no unit/scale transforms. KLIC files
reach ~210 MB (3.4M lines, 161k features) and produce ~20–60 MB JSON payloads. Meanwhile every existing upload rides
`FileAccess.FileToJsonAsync` → `ReadToGimData`, whose read dispatch (`FileManagerInternal`)
hosts the working DWG/OCCT/STEP/JSON paths alongside an intern-era GML/KML family
(`read/GMLRead/`) that is DOM-based (`XDocument.Load`) and largely dead code
(`KlicFileReader`, `KlicElements`, `XMLManager` have zero callers; `GimMapsJsonToGimData`
returns null).

## Decision

1. New entrypoint `FileAccess.FileToNetworkPayloadAsync(source, readStatus, gimOptions)` in
   its own `partial` file, backed by a new streaming backend `read/GMLRead/KlicImkl/`
   (forward-only `XmlReader`). It never routes through `ReadToGimData` and emits
   payload DTOs (`Networks`, `Lines`, `SelectionArea`, `Kind`, `Meldnummer`,
   `Leveringsvolgnummer`) as JSON.
2. Extension (`xml|gml|kml`) only gates which lane GIMmanager calls; content sniff (IMKL
   schemaLocation/namespace in the first KB) decides `Kind: klic | network | notNetwork`;
   `notNetwork` falls back to today's mesh path unchanged.
3. Cheap triage ships with it: delete the dead trio, fix the never-started
   `progressStatus` in the GML dispatch slot, case-insensitive `SupportsType`,
   `Console.WriteLine` → `GimLogger`. Old mesh otherwise frozen.

## Alternatives rejected

- **Piggyback on `FileToJsonAsync` with an internal branch**: couples a brand-new streaming
  parser to the shared dispatch of every mesh upload; one slip regresses all CAD imports.
  Failure isolation wins over one fewer manager branch.
- **Single rewritten streaming path serving both uses**: would fix the DOM path's scaling
  problems too, but nobody reported the CityGML mesh path hitting big files; rewrite risk
  without a paying use case (ponytail).
- **Route KLIC through GimData + PropertySets**: ownership/geometry carry is object-linked
  only, kinds are `{String, Double}` (dates/ints string-encoded), RD coordinates risk
  lookup-table quantization, and the payload's consumer is a DB insert, not a
  proprietary-binary write. See
  plan 0008 for PropertySets' real (deferred) trajectory.

## Consequences

- Two XML stacks coexist by design: DOM for small CityGML meshes, streaming for KLIC
  deliveries. Documented so the next auditor isn't tempted to "unify" blindly.
- GIMmanager learns one new call + route entry; the old call stays byte-identical on
  non-network files.
- PropertySets additionally gets only an optional debug sink (not part of this ADR's
  scope, deferred).
