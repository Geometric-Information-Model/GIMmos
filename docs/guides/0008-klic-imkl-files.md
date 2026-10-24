# KLIC / IMKL 2.0 delivery files — format guide

What a KLIC file is, how one is produced, what is inside it, and the domain vocabulary
used by the network-payload flow (`docs/plans/0010-klic-network-payload.plan.md`).
Written so a developer who has never seen a KLIC delivery can review changes in this
area without guessing.

## 1. What KLIC is and who can produce a file

KLIC ("Kabel- en Leiding informatie Centrum") is the bureau operated by Kadaster (the
Dutch land registry) on behalf of all network operators in the Netherlands. Dutch law
first via WION (2010), then broadened via WIBON (2024, the version that standardizes
the IMKL 2.0 delivery format) requires anyone planning excavation to request
**gebiedsinformatie** — area information — about every underground cable/pipeline
network inside their dig area before digging. KLIC is the only system that mints these
deliveries: it composes each file from every participating operator's source data
inside the requested polygon. Textually the files are XML/GML; functionally they are
official government output. Anyone can craft an XML in the same schema, but a real
delivery is always minted by the KLIC system; the platform therefore treats "this is
KLIC" as a structural claim of the file (header layout, meldnummer block, owner
registries), not as verified authenticity.

## 2. Request-to-download lifecycle

1. An excavator/planner (engineering bureau, municipality, DMI/ministry — exactly the
   target GIM users) files a dig request at the KLIC portal, digitizing a **dig-area
   polygon** and stating the activity type.
2. The system assigns a **meldnummer** (delivery report number, e.g. `26O0074042`
   — it is stamped in the filename too) plus an order number and a position number.
3. KLIC composes the delivery for that meldnummer: the main XML/GML file plus a
   `bronnen/` folder with a background raster and per-owner PDF attachments
   (profile sketches, general annexes).
4. The requester downloads the package. The XML is what the GIM reader ingests.

A delivery is versioned: re-requesting the same meldnummer after operator grid
corrections yields the same meldnummer with a higher **leveringsvolgnummer**
(re-delivery counter, e.g. the `_V2` suffix in the filename). Different dig requests
can legally overlap in one area and are separate deliveries.

## 3. What a delivery covers and how big it gets

- Coverage = the digitized dig-area polygon (`Orientatiepolygoon` feature) plus
  everything that crosses it. The file's one top-level envelope is simply that
  polygon's bounding box. Not a whole network, not a whole region.
- One file = one meldnummer delivery, self-contained: header (delivery + request
  blocks), then per-owner blocks (administrator/interested-party registries,
  attachments, per-network declarations), then the heavy per-owner feature blocks.
- Size varies with urban density: the reviewed sample (file
  `GI_gebiedsinformatielevering_26O0074042_1_V2.xml`) is 210 MB, 3.4M lines,
  161k features, 25 owner codes; rural areas are far smaller.

## 4. Inside the file — the structural facts that shape the GIM reader

- **Geometry lives on `UtilityLink`, not on the cable.** Theme features such as
  `Elektriciteitskabel`, `Telecommunicatiekabel`, `Mantelbuis`, `Rioolleiding` carry
  only semantics (theme, voltages, status, position category, accuracy, dates,
  widths/diameters) and link to their geometry with `net:link` and their network with
  `net:inNetwork`. The geometry-bearing feature is `us-net-common:UtilityLink`
  (LineString or Curve). A reader must join the two — this is why one GIM "line" is a
  merged object: UtilityLink geometry + the theme feature's attributes.
- **Networks are first-class features** (`imkl:Utiliteitsnet`): network type
  (electricity, telecommunications, oilGasChemical, sewer, …), theme label
  (middenspanning, laagspanning, …), and owner/authority roles. The **owner code is
  embedded in every feature id** (e.g. `nl.imkl-KL1040.…`, owner 1040) and real
  organization names come from the `Beheerder` blocks; a reader builds one small
  ownercode→organization-name map per file.
- **Selection area**: `Orientatiepolygoon` (the dig-request polygon); the top-level
  envelope is its bounding box.
- **Dates**: `validFrom`/`validTo` are **per line/feature values** (each cable, duct or
  pipe feature carries its own; the file as a whole only carries request/delivery
  timestamps). Per-line `validFrom` is historical/every-old; `validTo` is sparse and
  mostly nil/"unknown". There are no per-line validity windows usable for
  date-overlap logic — hence the delivery-keyed replacement rule. Both values are
  carried per line in the GIM payload's metadata.
- **Width/diameter**: real values exist per feature (for example duct widths in mm);
  frequently nil. Carried into line metadata when present.
- **Depth**: strictly 2D RD coordinates (EPSG 28992, meters; CRS spelled three
  different ways across files). Depth exists only as sparse depth-point features and
  text annotations — never as line elevation.
- **Display-support layer** (annotations, dimensioning, fittings, buildings, extra
  geometries): many tens of thousands of small features that make the interactive
  viewer readable, but which the GIM flow drops in phase 1; only the network lines
  and their attached semantics persist.
- **Reader-hostile trivia**: some owners serialize whole features on one line,
  namespaces are declared inline mid-file, codelist properties may be self-closing,
  geometry containers vary (LineString, Curve/segments, Polygon, MultiCurve),
  sentinel dates (1900/1968) mean "unknown", and peer-feature ids repeat in two
  styles (dotted vs dashed). Parsers must be streaming (XML pull), locale-invariant,
  and tolerant of all three CRS spellings.

## 5. Vocabulary quick reference

| Term | Meaning |
|---|---|
| KLIC / KLIC-WIN | The Kadaster-run underground networks information system |
| Gebiedsinformatie | Area information delivery — what "a KLIC file" is |
| IMKL | The information model standard for deliveries (1.x under WION; 2.0 under WIBON) |
| Meldnummer | The dig-request/delivery report number stamped on the whole delivery |
| Leveringsvolgnummer | Re-delivery version counter per meldnummer |
| Orientatiepolygoon | The dig-request polygon (the delivery's coverage) |
| Beheerder | Per-operator administrator block: real organization names per owner code |
| Owner code | The provenance suffix embedded in every feature id (KL1040, GM0202, …) |
| Utiliteitsnet | A utility-network declaration (type + theme + roles) |
| UtilityLink | The geometry-bearing line feature joined from a theme feature |
| validFrom / validTo | Per-line validity dates (usable only for history, not for replacement) |
