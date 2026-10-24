# GIM JSON — schema version history

Human-maintained log of **schema releases** and **externally visible read behavior** tied to version thresholds. **Not** every code change: bug-fix-only reader work that does not alter on-disk JSON does **not** add a row (see [`README.md`](README.md) — bug fixes do not bump `LATEST_SUPPORTED_JSON_VERSION`).

Schema strings use **three-part** `x.y.z` (`major.minor.build`). Future rows may use any major (e.g. `2.0.0`); **`1.*` is not assumed permanent.**

| Schema version | Introduced (date / PR) | User-visible / on-disk change | Reader / code | Notes |
| ---------------- | ---------------------- | ------------------------------ | ------------- | ----- |
| *Strictly below* `1.1.2`, or root `Version` missing / unparsable | — | Legacy baseline; treated as pre–`1.1.2` | [`JsonReaderV1_1_2_Older`](JsonReaderVersions/JsonReaderV1_1_2_Older.cs) | See `V1_1_2` in [`JsonVersionPolicy.cs`](JsonVersionPolicy.cs) |
| From `1.1.2` up to (but not including) `1.2.0` | — | Supported range; pre–Materials threshold | [`JsonReaderV1_1_2_Older`](JsonReaderVersions/JsonReaderV1_1_2_Older.cs) | Dispatch: `SelectReader` in [`JsonManager.cs`](JsonManager.cs) |
| `1.2.0` (through versions before `1.3.0`) | — | Materials and per-child `MaterialID` (see policy comments) | [`JsonReaderV1_2`](JsonReaderVersions/JsonReaderV1_2.cs) | Threshold `V1_2` in [`JsonVersionPolicy.cs`](JsonVersionPolicy.cs) |
| `1.3.0` … `LATEST_SUPPORTED_JSON_VERSION` (currently **`1.3.0`**) | — | Optional root property sets with nullable root/child links; property values use plain JSON strings or numbers; invalid links and non-primitive values fail loudly | [`JsonReaderV1_3`](JsonReaderVersions/JsonReaderV1_3.cs) | Threshold `V1_3`; older readers ignore unknown property keys and keep geometry + names; missing property section remains tolerated |
| *Strictly greater than* `LATEST_SUPPORTED_JSON_VERSION` | — | Not supported in this build | *N/A* — throws `NotImplementedException` after sniff | [`JsonVersionPolicy.IsUnsupportedFuture`](JsonVersionPolicy.cs) |

When you add a new supported schema `x.y.z`, add a row and update [`README.md`](README.md) dispatch and threshold tables in the same change.
