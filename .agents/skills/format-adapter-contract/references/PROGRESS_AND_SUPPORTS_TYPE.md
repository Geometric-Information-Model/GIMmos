# Progress And SupportsType

## SupportsType rules

- Accept a raw `fileType` input and compare in normalized form (`ToLower()`).
- Keep supported extensions explicit and reviewable.
- Return `false` for unsupported types; avoid throwing in `SupportsType`.

## Progress lifecycle rules

- Define stable task keys as constants (for example `*_DISK_READ`, `*_NODE_WRITE`).
- Call `Create(...)` before long-running work starts.
- Report incremental completion where practical (`EntityCompletedCallback(...)`).
- Force completion on successful exit (`ForceCompleteTask(...)`).

## Failure handling expectations

- On handled fallback paths, reset or reinitialize progress appropriately.
- Do not leave progress tasks half-open when operation is known to be complete.

## Reference precedents

- Read progress pattern: `GIMtransfer/read/AssimpReadElements.cs`
- Write progress pattern: `GIMtransfer/write/AssimpWriteElements.cs`
- OCCT read/write support gates: `GIMtransfer/read/GIMocctRead/GIMocctReadElements.cs`, `GIMtransfer/write/GIMocctWrite/GIMocctWriteElements.cs`
