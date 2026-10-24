# GIM JSON versioning — plan checklist

Use this when changing `JsonManager`, `JsonVersionPolicy`, sniff behavior, readers, or on-disk schema semantics.

## To execute the plan

- Read `[GIMtransfer/JsonManager/README.md](../../../../GIMtransfer/JsonManager/README.md)` and align edits with the dispatch table and procedures there.
- If the **schema** or **externally visible read behavior** changed, updated or appended `[VERSION_HISTORY.md](../../../../GIMtransfer/JsonManager/VERSION_HISTORY.md)`.
- If dispatch, thresholds, or `LATEST_SUPPORTED_JSON_VERSION` changed, updated **Feature thresholds** and related tables in `README.md`.
- Unsupported future files still throw `**NotImplementedException`** only after **streaming** sniff, not after full parse.
- Sniff still ignores nested `**Version`** keys (`JsonTextReader.Depth` / root-level only).
- Sniff reader uses `**DateParseHandling.None**`.
- Readers stamp `**JsonGimObject.Version**` from the parsed root; dispatcher does not stamp it.
- Downstream use of `**Joints**` / `**Materials**` remains null-safe (`?.` / guards).
- Checked default `**version**` in `[GIMCompress/entities/ClassesForGimJSON.cs](../../../../GIMCompress/entities/ClassesForGimJSON.cs)` if writers or baseline output could drift.
- Ran `dotnet test` on `TestPlatform` per `[GIMtransfer/AGENTS.md](../../../../GIMtransfer/AGENTS.md)`; added tests if dispatch logic changed.
- Do not attempt to resolve tests outside of this scope.

## Doc-only PRs

- `README.md` / `VERSION_HISTORY.md` cross-links stay valid relative to repo layout.

