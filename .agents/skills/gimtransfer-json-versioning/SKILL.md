---

## name: gimtransfer-json-versioning

description: >-
  Guide for GIM JSON schema versioning in GIMtransfer—JsonManager streaming sniff, JsonVersionPolicy thresholds
  (x.y.z, major not fixed), version-specific JsonReader classes, LATEST_SUPPORTED_JSON_VERSION,
  NotImplementedException for future schemas, JsonGimObject.Version stamping, nullable Properties/Joints/Materials and
  FileManagerPrivate consumers, and TestPlatform coverage. Use when changing JsonManager dispatch, readers, or
  on-disk schema behavior.
metadata:
  ecosystem: GIM
  repo: GIMmos

# GIMtransfer JSON versioning

## When to activate

Use this skill when you touch any of the following:

- Anything under `[GIMtransfer/JsonManager/](../../../GIMtransfer/JsonManager/README.md)` (dispatch, sniff, policy, readers).
- `LATEST_SUPPORTED_JSON_VERSION`, feature-threshold constants, or reader selection logic.
- A new or changed `JsonReader*` class under `JsonReaderVersions/`.
- Downstream assumptions about `JsonGimObject` (e.g. `Joints`, `Materials`, `Version`) in `FileManagerPrivate` or similar.

## Read first

The canonical technical reference is `**[GIMtransfer/JsonManager/README.md](../../../GIMtransfer/JsonManager/README.md)**`. Read it before changing behavior. For a compact history of schema releases and code pointers, see `**[VERSION_HISTORY.md](../../../GIMtransfer/JsonManager/VERSION_HISTORY.md)**`.

## Mandatory maintenance rule

If you change **any** of: dispatch table, thresholds, sniff rules, supported version range, reader inheritance, or the contract for how `JsonGimObject.Version` is set from the file:

1. Update the matching sections in `**README.md` in the same plan** (it is the source of truth for dispatch and procedures).
2. **Append or revise** `[VERSION_HISTORY.md](../../../GIMtransfer/JsonManager/VERSION_HISTORY.md)` when the **on-disk schema** or **externally visible read behavior** changes.

Bug-fix-only reader changes that do **not** change persisted JSON do **not** require a new schema row (and do not bump `LATEST_SUPPORTED_JSON_VERSION` per README).

## Pitfall reminders (details in README)

- Reject unsupported future versions **after** streaming sniff only—never after a full-document parse.
- Use `**JsonTextReader.Depth`** so nested `"Version"` keys are ignored; only the root property counts.
- Set `**DateParseHandling.None`** on the sniff reader so version-like strings are not parsed as dates.
- `**JsonGimObject.Version`** is stamped by the reader from the file’s JSON—**not** by the dispatcher.
- Some properties (such as `**Joints`** and `**Materials`**) are nullable; downstream code must use null-conditional access.

## Cross-cutting touchpoints

- **Serialization default:** `[GIMCompress/entities/ClassesForGimJSON.cs](../../../GIMCompress/entities/ClassesForGimJSON.cs)` holds the default `version` string for new objects. When bumping the supported schema or writer output, reconcile this default with `[JsonVersionPolicy](../../../GIMtransfer/JsonManager/JsonVersionPolicy.cs)` and `LATEST_SUPPORTED_JSON_VERSION` to avoid drift.
- Schema bumps may require changes **outside** `JsonManager/` (writers, defaults, tests).

## Version numbering

On-disk `Version` strings are **three-part** `major.minor.build`. **No** component is fixed—future supported schemas could be `**2.0.0`**, `**10.4.3`**, etc. Use neutral placeholders (`x.y.z`, “next schema”) in generic guidance; use ** literals** only when describing **current** policy constants and readers.

When adding types, **follow existing reader naming** in the repo; do not infer major/minor semantics from class names alone (e.g. future `JsonReaderV2_0` is possible).

## Testing

Run tests as in `[GIMtransfer/AGENTS.md](../../../GIMtransfer/AGENTS.md)`:

`dotnet test "..\\TestPlatform\\TestPlatform.csproj" -c Debug -p:Platform=x64`

Add or adjust tests when you change version dispatch; prefer golden JSON fixtures per supported schema if coverage is thin.

## Spec and validation

This skill follows the [Agent Skills specification](https://agentskills.io/specification). When the CLI is available, validate the skill directory:

`skills-ref validate .agents/skills/gimtransfer-json-versioning`

## Progressive disclosure

- Step-by-step plan checklist: [references/CHECKLIST.md](references/CHECKLIST.md)

## Optional: ecosystem drift

If product docs or other repos (e.g. GIMfront) mention supported JSON features, consider a quick consistency check after schema changes.