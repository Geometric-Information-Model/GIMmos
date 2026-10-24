---
name: fileaccess-routing
description: Route new or changed format conversion logic through FileAccess partials and backend SupportsType gates. Use when adding or modifying read/write entrypoints, format dispatch, or conversion flow in GIMmos.
---

# FileAccess Routing

Apply this skill when you touch format routing in `GIMmos/GIMtransfer`.

## Primary rule

Route format logic through `FileAccess` partials, not ad-hoc entrypoints:

- `GIMtransfer/FileManager/FileManager.cs`
- `GIMtransfer/FileManager/FileManagerInternal.cs`
- `GIMtransfer/FileManager/FileManagerPrivate.cs`

## Routing expectations

1. Normalize incoming `fileType` (lowercase, trim leading dot where relevant).
2. Keep routing explicit with backend `SupportsType(...)` checks.
3. Preserve read/write parity mindset for newly introduced formats.
4. Keep unsupported formats failing clearly (no silent fallback behavior changes).
5. Keep backend-specific logic in backend classes, not in public API wrappers.

## Do

- Add backend dispatch in `FileAccess` only after backend adapter contract exists.
- Reuse existing progress patterns (`GimProgressStatus`) and temp path helpers.
- Keep dispatch order intentional and documented in commit/PR notes.

## Do not

- Bypass `FileAccess` and call backend readers/writers directly from unrelated surfaces.
- Duplicate format capability tables in docs; use `FileTypes.cs` as canonical.
- Add format-specific branching in multiple unrelated places.

## References

- Routing details: [references/ROUTING_FLOW.md](references/ROUTING_FLOW.md)
- Change checklist: [references/CHECKLIST.md](references/CHECKLIST.md)
