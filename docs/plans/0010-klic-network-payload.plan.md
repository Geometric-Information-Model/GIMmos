# KLIC / IMKL 2.0 network-payload flow (no GimData)

Users upload KLIC deliveries (IMKL 2.0 GML, official Kadaster output) and generic GML/KML/XML,
and the platform renders them as 2D network lines instead of 3D GimData objects. GIMtransfer
reads the file with a streaming reader and emits a JSON payload; CoreWebApi persists lines into
the PostGIS `NetworkDbContext`; GIMmanager dispatches and ships the payload zipped; the
frontend app asks the publish question (non-KLIC), offers "place on map" (prompt pre-checked
for KLIC), and renders the object in GIMpro as a lines scene plus in GIMmaps through the
existing network-line path. Canonical plan; thin plans point at it from `CoreWebApi`,
`GIMmanager`, and the frontend repo. File-format background for reviewers:
`GIMtransfer/docs/guides/0008-klic-imkl-files.md`.

Ground truth from one real delivery — file
`GI_gebiedsinformatielevering_26O0074042_1_V2.xml`, a 210 MB Klic sample file we used, containing: 3.39M lines, 161k features,
  meldnummer 26O0074042 (facts and a fuller format walkthrough live in
  `GIMtransfer/docs/guides/0008-klic-imkl-files.md`: geometry lives on
`us-net-common:UtilityLink` (31,555) joined to theme features by `net:link`; networks are
`imkl:Utiliteitsnet` (36, each with type/thema/owner-role); owner code is embedded in every
gml:id plus `imkl:Beheerder` org names; one file-level envelope + `imkl:Orientatiepolygoon`
selection polygon; 2D RD (EPSG::28992) only, no Z geometry; `validTo` ≈ nil so date-overlap
rules have nothing to read; `ductWidth`/diameters exist (~8.7k width values). Support features
(Maatvoering/Annotatie/Appurtenance/Kast/TechnischGebouw/ExtraGeometrie/EigenTopografie/
ExtraDetailinfo) are dropped in phase 1.

## Execution mode

- [x] Verify per step (files + disposition + manual check, grill-gated with the user)
- [x] Commit per step
- [x] PR per step (one draft `feature/klic-network-flow` → `dev` at the end of the run)
- [x] Critic per step (granular, one rerun max)

Branch: `feature/klic-network-flow` from `dev` (same in every repo touched), then leaf step
branches `klic-network-flow/<stepid>-<title>`. No Jira ticket (none requested for this run;
add one before build if the user asks).

## Failure scenarios

- Move to the next step while the touched repo does not build.
- Commit while the repo does not build.
- Move on without a fresh critic pass on that step.
- Commit before the critic.
- Let any old GML/mesh read flow regress (the intern-era `XDocument` path keeps working for
  CityGML-style files; `notNetwork` fallback routes to it unchanged).
- Let KLIC detection depend on file extension (extension only gates the lane; content sniff
  decides KLIC vs not).
- Touch `PropertySets`/GimData model internals for this flow (PropertySets stays out of the
  production payload path; clone/demote carry work remains deferred to plan 0008 C3).

## Decisions (grill session 2026-10-17, full rationale in ADRs)

- **Payload, not GimData.** One Line = cable/pipe theme feature + its UtilityLink geometry
  merged; Networks[] are real objects (type, theme, ownerCode, ownerName); SelectionArea =
  envelope + Orientatiepolygoon. See GIMtransfer ADR 0004.
- **Per-line metadata, minimal** (review-refined 2026-10-17): per line `status,
  verticalPosition, validFrom, validTo, sourceGmlId, widthMm (nullable)`, plus
  `objectId` stamped at insert and the delivery keys riding the B1 indexed columns
  (not jsonb). **Network facts are NOT duplicated per line** — `networkType`,
  `theme`, `ownerCode`, `ownerName` were dropped from line metadata (review outcome);
  `NetworkIndex` points at `Networks[]`, which carries them once. Width rides
  metadata only (no top-level DTO property; KLIC files carry real widths for some
  asset classes — ducts, casings — and none for others; both are normal shape).
  Render width rule lives in the Viewer decision.
- **Naming.** Object shell = `KLIC <meldnummer> – <woonplaatsNaam>` (for example
  `KLIC 26O0074042 – Arnhem`; B-case keeps the user's filename). Line display name =
  `<Beheerder org name> · <theme> · <localId tail>` (for example
  `Liander · middenspanning · 12406439`), minted by the reader; full `gml:id` rides
  in metadata as `sourceGmlId`.
- **Detection** = IMKL namespace/schemaLocation sniff in the header; extension (xml/gml/kml)
  only routes to the new entrypoint. Reader returns `Kind`: `klic` | `network` | `notNetwork`;
  `notNetwork` falls back to today's mesh path. `Kind` in one sentence: the single
  classification verdict of what was just parsed, returned by the reader and consumed by
  GIMmanager (C: where to ship) and the frontend app (D: which prompt to show) —
  `klic` = official delivery, auto-publish, no prompt; `network` = generic networked
  GML/KML/XML, publish modal, project-owned lines; `notNetwork` = XML-family file with no
  network-line content (e.g. a CityGML mesh) → existing mesh path, behavior unchanged.
- **Ownership/flags split across two tables** (CoreWebApi ADR 0012 + existing ADR 0006):
  `isKlic`, `isPublished` live on `NetworkLine` (visibility + toggle need them; coverage
  is scope, not visibility). **Ownership goes through `ProjectCoverage` — no new column
  on `NetworkLine`**: B-case = one line-scoped coverage row per line (`ProjectId = P`,
  `LineId = line`, geometry/network blank) — "owns the lines one by one"; KLIC = one
  area-scoped coverage row per delivery (`ProjectId = 0`, polygon = Orientatiepolygoon)
  — project `0` is public read per ADR 0006. `Network` stays a neutral typed container;
  add-lines reuse rule = target networks homogeneous in the incoming ownership class
  (KLIC → ownerless/public ones; user project → that project's coverage-scoped networks),
  else create network + coverage rows as above.
- **Delivery replacement** instead of date overlap (CoreWebApi ADR 0012). Background: a
  meldnummer (see the format guide) is KLIC's delivery report number for one dig request —
  the identifier stamped on every feature of that delivery — and `leveringsvolgnummer` is
  its re-delivery counter; re-requesting the same area after operator corrections produces
  the same meldnummer with a higher counter, and different dig requests legally overlap in
  one area. Because the file carries no usable per-line validity windows (`validTo` ≈
  nil/unknown), replacement keys on the delivery, not dates: same meldnummer + higher
  `leveringsvolgnummer` → replace that meldnummer's lines in the area; stale rejected with
  message; different meldnummern coexist. The delivery keys are **indexed columns** on
  `NetworkLine` (see B1) so replacement/staleness queries never lean on jsonb indexes;
  richer per-line facts stay in jsonb metadata. The columns are **nullable and stay
  null for non-KLIC lines** (general knowledge, not enforced by a DB constraint — review
  note). Phase 1 only: line-interval stitching across deliveries is **deferred** to an
  RFC (see Deferred).
- **Transport**: the uncompressed JSON payload is 20–60 MB (this delivery alone), while the
  API server accepts at most ~30 MB request bodies (Kestrel's global default, which exists
  for good reason and stays untouched) — so this flow **zips the payload** on the manager
  (~5–15 MB over the wire, well under the cap): reuse GIMmanager zip machinery with the
  threshold lowered for this branch, and one atomic insert per delivery; chunking deferred
  behind a size guard for future giant deliveries.
- **Publish UX**: post-sniff modal for non-KLIC network uploads (default unpublished, one
  `UPDATE isPublished` keyed by `metadata.objectId=oid`); KLIC auto-publishes, never prompted.
- **Placement**: `ObjectMapPlacement` EF table in `NetworkDbContext` (`ObjectId`, `ProjectId`,
  `Placement` polygon SRID 28992 + GiST, `IsKlic`, `PlacedOnMaps` bool default false),
  written by the import in the same transaction; center/extents/max-bbox derived BFF-side on
  read (no stored center columns); "place on map" flips the flag (prompt pre-checked for
  KLIC, remains available later); "view on map" gates on it; object delete keeps placement-row
  removal + oid removal from line metadata as doc A6/B6 wrote. Important distinction:
  `PlacedOnMaps` and `IsPublished` are **independent** — publish controls the
  hidden-until-called-for visibility rule with project-visibility rules still applying
  (pressing "show unpublished networks" never grants visibility a user didn't already
  have); placement controls only whether the object is integrated into GIMmaps.
- **Viewer**: GIMpro object page renders a lines-only three.js scene fed by the BFF
  (`{id, networkID, points, metadata}` per line) using **`Line2`** (fat-line width support):
  width comes from each line's `metadata.widthMm` when present, falling back for ALL
  widthless lines to **one global default width constant** (phase-1 rule — goal is
  visualize-not-perfect; no medians, no theme tables, nothing fabricated per line).
  Theme/Type semantics: Network `Theme` = coarse class
  (`electricity`/`telecommunications`/… — the per-cable feature names map 1:1 onto
  these; `utilityNetworkType` is the authoritative source), `Type` = the band
  (`middenspanning`/`laagspanning`/…). Other appearance-affecting metadata keys are
  taken into account where trivially cheap (do not overreach). GIMmaps keeps
  `MapNetworkLines` +
  visibility flags + the existing "show unpublished networks" concept; KLIC lines always
  visible, never deletable as map data.
- **Skills (standing)**: GIMtransfer reader steps follow `fileaccess-routing` +
  `format-adapter-contract`; CoreWebApi steps follow `corewebapi-layered-feature`,
  `dotnet-webapi`, `optimizing-ef-core-queries`.
- **PropertySets / proprietary binary formats**: untouched end to end (payload is plain
  JSON; no round-trip through the proprietary binary layer; the external-package fallback
  pin is irrelevant for this flow).

## Steps

- [ ] **A1** — Streaming IMKL payload reader core. `GIMtransfer/read/GMLRead/KlicImkl/`:
  forward-only `XmlReader` state machine (header Levering/Aanvraag, Utiliteitsnet networks,
  owner map from Beheerder blocks, Orientatiepolygoon + envelope, theme features paired to
  UtilityLinks via `net:link`/`net:inNetwork` resolution, per-line geometry from LineString +
  Curve variants, nil/unknown-safe dates, width/diameter fields when present, CRS normalized
  across 3 spellings). Emit strongly-typed payload DTOs (`Networks`, `Lines`, `SelectionArea`,
  `Kind`, `Meldnummer`, `Leveringsvolgnummer`). Geometry serialized as WKT. `Kind`
  (see Decisions) is the reader's classification verdict — `klic` = official KLIC
  delivery, `network` = generic networked GML/KML/XML, `notNetwork` = XML-family file
  with no usable network-line content.
  Test fixture: handcrafted mini-IMKL (`TestPlatform/TestData/Klic/`), deliberately rich
  and varied (review note) — multiple owners + themes, LineString + Curve + Polygon
  variants, nil dates + sentinel dates, nil width + real width, all three CRS spellings,
  codelist variants, an owner that serializes whole features on one line, inline
  namespace declarations, several support features that must be ignored, and a
  `validTo = 9999-12-31` case. Tests in TestPlatform (`InternalsVisibleTo` already set).
  Progress: task names per `format-adapter-contract`; culture-invariant parsing throughout
  (no `double.Parse` without locale; the intern bugs at `XDocToVectList.cs:30` are the
  precedent to avoid).
  Files: `GIMtransfer/read/GMLRead/KlicImkl/*` (new), `TestPlatform/*` (new tests + fixture).
  Does NOT touch `FileManagerInternal.cs` or the file catalogue yet.
  Validate: `dotnet build "GIMtransfer/GIMtransfer.csproj" -c Debug -p:Platform=x64` +
  `dotnet test "TestPlatform/TestPlatform.csproj" -c Debug -p:Platform=x64`.

- [ ] **A2** — FileAccess entrypoint + payload emit + intern triage.
  New `public static partial class FileAccess` method `FileToNetworkPayloadAsync(source,
  readStatus, gimOptions)` in its own partial file (pattern: `FileManagerAsync.cs:13`), never
  routed through `ReadToGimData`/`FileManagerInternal.cs:89`. New read-only `FileTypes.cs`
  entries for `klic`/`imkl` (and note xml/gml stay read-enabled for both flows). Content sniff
  (first KB: `imkl-wibon` schemaLocation or `imkl:` root presence) → `Kind` (the classification
  verdict explained in Decisions — the value every later consumer branches on); emits the payload
  JSON to the temp output path (same convention as existing reads). Triage in the same step:
  delete dead `KlicFileReader.cs` + `KlicElements.cs` + `XMLManager.cs` (no callers anywhere),
  fix the never-started `progressStatus` in the GML dispatch slot, make `SupportsType`
  case-insensitive (`ReadGMLToGimData.cs:69`), replace `Console.WriteLine` debug spam in
  `read/GMLRead/*` with the `GimLogger`. Old mesh path otherwise frozen. See GIMtransfer
  ADR 0004.
  Files: `GIMtransfer/FileManager/*` (new partial + catalogue), `GIMtransfer/read/GMLRead/*`.
  Validate: build + tests; manual TestUI open of the sample's header-only subset.

- [ ] **B1** — CoreWebApi schema: flags + placement table. EF migration on `NetworkDbContext`:
  `NetworkLine` columns `IsKlic`, `IsPublished` (default false) — **no ownership column**
  (ownership rides `ProjectCoverage` per the Decisions bullet and ADR 0006). New
  delivery-key columns `Meldnummer` (string, indexed, **nullable**) and `Leveringsvolgnummer`
  (int, indexed, **nullable**) on `NetworkLine` — null for non-KLIC lines (no DB
  constraint; review note) — so replacement/staleness queries never depend on jsonb
  indexes. New
  `ObjectMapPlacement` chunk (entity under
  `GIM.Common/Models/Entities/Network/`, registered in `NetworkDbContext`): `ObjectId` (int,
  no cross-DB FK — enforced by flow), `ProjectId`, `Placement` geometry polygon SRID 28992 +
  GiST, `IsKlic`, `PlacedOnMaps` default false. Migration via the established
  `dotnet ef migrations add` on `DesignTimeNetworkDbContextFactory`; startup auto-migrate
  already in place. Horizontal index candidates: `(Meldnummer)`, `(ObjectId)`; coverage
  table keeps its own indexes (line-scoped rows queried by `LineId`/`ProjectId`).
  See CoreWebApi ADR 0012 and ADR 0011 for registration rules.
  Validate: build `GIM.WebAPI` solution; `NetworkDatabaseHost.MigrateNetworkDatabase` applies
  cleanly against LocalDb Postgres; repo tests green.

- [ ] **B2** — CoreWebApi import + replacement. New service/repository pair (layered target
  architecture; `optimizing-ef-core-queries` for the bulk insert: chunked `AddRange` +
  `SaveChanges` batches, single transaction per delivery incl. placement row; Postgres
  `DELETE` before insert for the meldnummer-replacement rule; staleness reject when incoming
  `leveringsvolgnummer` ≤ stored max). Endpoint `POST bff/networks/import` — **BFF route
  per ADR 0008 conventions** (review note: GIMmanager runs on the user's machine, so it
  talks to the BFF; `api/network/*` stays a clean REST surface). Zipped payload,
  `[RequestFormLimits]` raised on the action, body size guard switches to 502-with-message
  when delivery unzipped exceeds the guard; Kestrel global cap untouched). Insert stamps every
  line's metadata with `objectId`, `sourceGmlId` (delivery keys live in the B1 columns);
  coverage rows per the Decisions ownership split — KLIC: one area-scoped row
  (`ProjectId = 0`, polygon = Orientatiepolygoon); B-case: one line-scoped row per line
  (`ProjectId = P`, `LineId`, geometry/network blank); name-minted lines kept exactly as
  the reader produced them.
  Tests: replacement matrix (same meldnummer higher/lower volgnummer, different meldnummer),
  bulk insert happy path, unauthenticated → 401, non-member → 403 via `ProjectAccessGate`.
  Validate: build + tests; LocalDb import of the mini fixture end-to-end.

- [ ] **B3** — CoreWebApi read/publish/delete routes. Publish decision: `PATCH
  bff/networks/{objectId}/publish` flipping `IsPublished` for lines whose metadata `objectId`
  matches (idempotent). Placement: `POST bff/networks/{objectId}/placement` flips
  `PlacedOnMaps`; placement read returns `{center, widthM, heightM, exceedsMaxBbox}`
  derived from the stored polygon at query time (max-bbox constant sourced from the existing
  `LiveMapUpstream.MaxAreaSquareMeters`).   Fetch routes: lines by `objectId` (GIMpro viewer), lines by bbox with visibility rules
  (KLIC always; `isPublished`; owning-project lines resolved through `ProjectCoverage`
  line-scoped rows joined with the caller's project set and `ProjectAccessGate` letters)
  delivered through `bff/maps` per ADR 0008 conventions;
  keep camelCase response shapes the front already consumes (`UpstreamNetworkLineResponse`
  precedent). Delete integration: object-delete flow cleans placement row + removes `oid`
  from line metadata (KLIC keeps lines; owned lines deleted per doc B6) — wired into the
  existing `FileObjectRepository.DeleteAsync` pipeline's cross-DB section.
  Validate: build + tests; manual probe via `gim-probe` against LocalDb for one KLIC + one
  owned object.

- [ ] **C** — GIMmanager dispatch + transport. Extension gate (`xml|gml|kml`) in
  `GIMmanagerLogicObjectUploading` routes to `FileAccess.FileToNetworkPayloadAsync`
  (new branch beside `ConvertFileToJsonAsync`, `GIMmanagerLogic.cs:343-360`); on `Kind =
  notNetwork` → existing `FileToJsonAsync` path (behavior unchanged); on `klic`/`network` →
  zip payload (manager zip machinery, threshold lowered for this branch) → new route to
  CoreWebApi `bff/networks/import` (add URI section + entry in `ConfigurationService` +
  `WebApiService`), report `Kind` back over hub events so the frontend app can prompt.
  Progress events stay on existing `SendToJS_object_upload_progress/_result`. No local DB work.
  GIMmanager thin plan: `GIMmanager/docs/plans/0008-thin-klic-network-flow.plan.md`.
  Validate: build solution; manual local-stack run: upload sample header-subset via TestUI or
  built front, confirm manager zips + posts + object shell row created via `PostObjectData`.

- [ ] **D** — Frontend app upload + affordances. Upload completion handles `Kind`: KLIC → no
  publish prompt, pre-checked "place on map" prompt (decline allowed, first-class state),
  object named `KLIC <meldnummer> – <woonplaats>` (reader/manager-supplied, shell row keeps
  it); `network` (non-KLIC) → publish modal with effective-visibility copy, default
  unpublished; `notNetwork` → current flow untouched. Object list: map-object affordances —
  "see on map 🔗" menu entry + bottom-of-page button (doc A4), "place on map" available
  later, "view on map" once placed. GIMpro object page: lines-only three.js scene with
  `Line2` fat lines (`{id, networkID, points, metadata}` from BFF; width from `widthMm`
  with the small fallback key set per the Viewer decision; colors per the live-sources
  precedent PRs 97/105; camera clamped to derived bbox, max-bbox rule enforced
  server-side + honored client-side; doc A5). GIMmaps: lines ride `MapNetworkLines` with
  visibility per flags; "show unpublished networks" toggle only reveals line the caller may
  see (BFF already enforces; the toggle is a fetch param). Frontend thin plan lives in the
  frontend repo (`docs/plans/0011-thin-klic-network-flow.plan.md` there).
  Klic doc references: A3-A7, B3-B7 covered here; X replaced by the ADR 0012 rule.
  Validate: build front; manual local-stack walkthrough of: KLIC upload → prompt → placed →
  view in GIMpro → view in GIMmaps → delete object → lines/behaviors per A6; B-case upload →
  publish modal → toggle behavior.

- [ ] **E** — E2E + wrapup. Full local stack run against the real 210 MB delivery (zip path,
  import timing, maps render at multiple zooms, viewer scene), plus the mini fixture for
  automation. Record measured payload/wire sizes once. Update this plan's checkboxes as steps
  close; no proprietary-binary/package bumps anywhere; PropertySets debug-sink is explicitly
  deferred (possible follow-up note, not a step).

## Test data

- Automation: `TestPlatform/TestData/Klic/mini-delivery.xml` (handcrafted, few KB).
- Manual: the real 210 MB delivery `GI_gebiedsinformatielevering_26O0074042_1_V2.xml`
  (offline uncommitted fixture; never committed to any repo).

## Deferred

- **Reader strictness tiers + dropped-geometry observability (phase 2, deliberation
  2026-10-17).** RFC: `GIMtransfer/docs/rfcs/0008-strictness-tiers-and-observability.md`.
  Short form: principle "strictness
  equals the strength of the contract" — KLIC = contract tier (objective malformedness,
  per-geometry abort + warn, structural failure fails loudly), generic GML/KML =
  best-effort tier (never judge intent); phase 2 adds stride inference (2 → 3 →
  abort-geometry), KML mixed-tuple spec handling, and `DroppedGeometryCount` on
  `NetworkPayloadResult`. Phase 1 ships none of it: 2D-only reads, no failure
  reporting.
- **Line-interval stitching across deliveries (phase 2, review outcome 2026-10-17).**
  RFC: `GIMtransfer/docs/rfcs/0007-line-interval-stitching.md`. Short form: sentinel end date
  `9999-12-31` for unknown `validTo`, identity = `sourceGmlId` + geometry-hash
  fallback (unmatchable = new line, never guessed), prepend-history for older dig
  requests, close-and-reopen for newer ones, absence-ending scoped to the
  deliveries' polygon intersection (no global inverse search), staleness reject only
  within the same meldnummer. Phase-1 schema (nullable delivery-key columns +
  `sourceGmlId` in metadata) is forward-compatible.
- Support-feature rendering (Annotatie labels etc.) and doc Y's "what GIMpro renders on top".
- Depth/Diepte point features and any elevation story (2D geometry only).
- Chunked import protocol (size guard first, only if giant deliveries appear).
- Authenticity hardening for claimed KLIC files (structural-claim + auto-publish stands).
- PropertySets debug sink in TestUI (separate discussion, plan 0008 C3 deferred work).
- Old GML→GimData mesh-path scaling (DOM-based) unless someone feeds it big files.
- GIMmanager and frontend thin-plan step details live in their thin plans, not here.