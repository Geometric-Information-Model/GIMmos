---
name: format-adapter-contract
description: Define and integrate reader/writer format adapters with SupportsType, progress lifecycle, and FileAccess dispatch integration. Use when adding or modifying import/export backends in GIMmos.
---

# Format Adapter Contract

Apply this skill when adding or changing a format adapter in `GIMmos/GIMtransfer`.

## Adapter contract

Every backend adapter should follow this contract:

1. Expose `SupportsType(string fileType)` for routing checks.
2. Accept and use `GimProgressStatus` consistently.
3. Keep conversion responsibilities local to the adapter class.
4. Return/consume `GimData` without bypassing structural invariants.
5. Integrate through `FileAccess` dispatch points, not direct external entrypoints.

## Integration rule

After implementing an adapter, wire it in the correct dispatch path:

- Read-side routing in `FileManagerInternal.cs`
- Write-side routing in `FileManager.cs`

## Backend guardrails

- For OCCT-related operations, preserve lock usage patterns (`OcctLock`, `MeshingLock`).
- Keep backend-specific dependencies isolated to backend files under `read/` or `write/`.

## Do

- Keep `SupportsType` tight and explicit.
- Keep progress task names stable and meaningful.
- Keep unsupported types rejected clearly.

## Do not

- Skip `SupportsType` and rely on exceptions for normal routing.
- Mix unrelated backend concerns in a single adapter class.
- Add adapter paths that bypass `FileAccess`.

## References

- Adapter template: [references/ADAPTER_TEMPLATE.md](references/ADAPTER_TEMPLATE.md)
- Progress and supports-type guidance: [references/PROGRESS_AND_SUPPORTS_TYPE.md](references/PROGRESS_AND_SUPPORTS_TYPE.md)
