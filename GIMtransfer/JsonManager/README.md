# GIM JSON — versioned readers, dispatch policy, and version handling

**Source of truth:** this file defines dispatch, sniff rules, and how to extend schema support. **Schema release history** (thresholds ↔ readers ↔ notes) lives in [`VERSION_HISTORY.md`](VERSION_HISTORY.md); keep it aligned when the on-disk schema or visible read behavior changes.

This folder owns the reader side of the GIM JSON transfer format: the public `JsonManager` entry point, the abstract [`JsonReader`](JsonReader.cs) base class, the centralized version policy in [`JsonVersionPolicy.cs`](JsonVersionPolicy.cs), and the per-schema readers under [`JsonReaderVersions/`](JsonReaderVersions/).

This document is the authoritative reference for **how GIM JSON schema versions are interpreted and dispatched to readers**. Code comments in `JsonVersionPolicy.cs` and the reader classes deliberately stay short and defer to this file for the full rules.

---

## Schema versioning model

A GIM JSON file exposes a `Version` string at the root. GIM JSON uses **three components only**: `major.minor.build`. There is **no fourth "revision" component**.

When we work with `System.Version` values (for comparison and threshold checks), we **normalize the .NET `Revision` component to `0`**. This makes values coming from different constructor overloads compare consistently, and it makes the "three-part only" rule explicit at the API boundary.

Parsing rules for the JSON `Version` string (see `JsonVersionPolicy.TryParseSchemaVersion`):

- Accepts `"major"`, `"major.minor"`, or `"major.minor.build"`.
- Missing components are treated as `0`.
- Any other shape (extra parts, non-numeric, negative) is rejected.
- `null` / whitespace is rejected.
- A rejected version is treated the same as "legacy / unknown schema" (see the dispatch table below).

---

## Dispatch policy

`LATEST_SUPPORTED_JSON_VERSION` (in [`JsonVersionPolicy.cs`](JsonVersionPolicy.cs)) is the **maximum** schema version this codebase implements. The dispatcher (`JsonManager`) maps a file's parsed effective version to a reader using this table:

| Parsed effective version                              | Action                                                                                                |
| ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| **Strictly `< 1.1.2`**                                | Use `JsonReaderV1_1_2_Older` (legacy baseline).                                                       |
| **Missing / unparsable `Version`**                    | Same as `< 1.1.2` — use `JsonReaderV1_1_2_Older`.                                                     |
| **`1.1.2 ≤ version ≤ LATEST_SUPPORTED_JSON_VERSION`** | Dispatch by feature threshold (see below).                                                            |
| **Strictly `> LATEST_SUPPORTED_JSON_VERSION`**        | Throw `NotImplementedException` **before** any full document parse. Message includes file and latest. |

### Feature thresholds

Within the supported range, the dispatcher selects a reader by comparing the parsed version against feature thresholds defined as constants in `JsonVersionPolicy`:

| Feature threshold | Constant | Reader for `version < threshold` | Reader for `version ≥ threshold` |
| ----------------- | -------- | -------------------------------- | -------------------------------- |
| `1.2.0`           | `V1_2`   | `JsonReaderV1_1_2_Older`         | `JsonReaderV1_2`                 |
| `1.3.0`           | `V1_3`   | `JsonReaderV1_2`                 | `JsonReaderV1_3`                 |

Lower bound for the supported range is the constant `V1_1_2` (`1.1.2`). The latest supported version is `1.3.0`.

The 1.3.0 property section is additive-tolerant: pre-1.3 readers ignore unknown `PropertySets` and `PropertySetIndex` keys, decoding geometry and names while dropping properties. Property sets use plain key-value JSON entries: strings encode string properties and numbers encode double properties. Missing property keys in a 1.3.0 payload also mean legacy-shaped data, not corruption. Present malformed links or non-string/non-number property values still fail loudly.

---

## Reader responsibilities

Each version-specific reader is responsible for materializing `JsonGimObject` from the document string. Notably:

- The public `ReadJson(string)` API is **string-only**. `JsonManager` must not pass a pre-parsed `JObject` into a reader, because doing so would imply a full parse of the document **before** the unsupported-version throw. Any `JObject`-based helpers live strictly as `protected` implementation details inside the reader class hierarchy.
- Readers **stamp `JsonGimObject.Version` with the canonical schema line for that reader** (see `JsonVersionPolicy` thresholds: e.g. `JsonReaderV1_1_2_Older` uses `V1_1_2`, `JsonReaderV1_2` uses `V1_2`). The root JSON `Version` is used only by `JsonManager`’s sniff for dispatch, not copied onto the model. `JsonGimObject.Version` exposes an `internal` setter for reader use; the dispatcher must not stamp it.

## Null-safety contract for downstream consumers

`JsonGimObject.Joints`, `Materials`, and `PropertySets` are **nullable** — readers only assign them when corresponding JSON key is present. Downstream consumers (today, `GimJsonToGimData` in `FileManagerPrivate.cs`) must guard with null-conditional access (`jsonObj.Joints?.Count > 0`, `jsonObj.PropertySets?.Count > 0`) rather than dereferencing directly. `JsonGimObject.Children` is initialized to an empty list by default and stays non-null.

---

## Adding a new schema version

When adding a future schema (for example, `1.3.0` property-set support):

1. Add a new reader class `JsonReaderV1_3` under `JsonReaderVersions/`. Prefer inheriting from the closest older reader and overriding only the methods whose behavior has changed.
2. Add the new feature-threshold constant (e.g. `V1_3`) to `JsonVersionPolicy`.
3. Extend the `JsonManager` dispatch to route `version ≥ V1_3` to the new reader.
4. Bump `LATEST_SUPPORTED_JSON_VERSION` to the new schema.
5. Update the **Feature thresholds** table in this README.
6. Keep readers tolerant of absent additive sections; document how older readers handle new unknown keys.

Bug-fix-only readers that don't change the on-disk schema do **not** bump `LATEST_SUPPORTED_JSON_VERSION`.

---

## Pitfalls

- **Unsupported file:** failure MUST happen after a streaming version sniff only, **not** after a full `JObject.Parse` / `JsonConvert.DeserializeObject<JsonGimObject>` of the whole document.
- **Nested `"Version"` keys:** the root-level sniff must ignore deeper `Version` properties (filter by `JsonTextReader.Depth`).
- **Null collection fields:** see the null-safety contract above — guard `Joints` / `Materials` downstream.
