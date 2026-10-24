# Routing Flow (Current Precedent)

## Read flow

Primary read dispatch lives in `GIMtransfer/FileManager/FileManagerInternal.cs`:

1. Handle core formats early (`json`, `bin`).
2. Call backend-specific `SupportsType(...)` gates (GML, DWG, OCCT).
3. Fall back to Assimp for remaining supported mesh-style formats.
4. Keep special IFC handling consistent with existing flow.

## Write flow

Primary write dispatch lives in `GIMtransfer/FileManager/FileManager.cs`:

1. Handle core formats early (`json`, `bin`).
2. Route OCCT-supported formats through `GIMocctWriteElements`.
3. Route GML through `GimdataToGml`.
4. Route remaining supported exports through `AssimpWriteElements`.

## Placement rules for new routing

- Add new backend capability checks in the matching read/write dispatch point.
- Keep backend implementation details in backend classes under `read/` or `write/`.
- Keep dispatch checks close to existing `SupportsType` pattern.

## Related files

- `GIMtransfer/FileManager/FileManager.cs`
- `GIMtransfer/FileManager/FileManagerInternal.cs`
- `GIMtransfer/read/GIMocctRead/GIMocctReadElements.cs`
- `GIMtransfer/write/GIMocctWrite/GIMocctWriteElements.cs`
