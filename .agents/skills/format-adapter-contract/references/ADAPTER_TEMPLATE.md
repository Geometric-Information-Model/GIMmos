# Adapter Template

Use this template shape when introducing a new backend adapter.

## Reader adapter shape

- Class under `GIMtransfer/read/...`
- Constructor accepts optional/required `GimProgressStatus`
- `public static bool SupportsType(string fileType)`
- `public GimData Read...(...)`

## Writer adapter shape

- Class under `GIMtransfer/write/...`
- Constructor accepts optional/required `GimProgressStatus`
- `public static bool SupportsType(string fileType)`
- `public string Write...(...)` (or equivalent backend output)

## Required integration points

1. Add read dispatch hook in `GIMtransfer/FileManager/FileManagerInternal.cs`.
2. Add write dispatch hook in `GIMtransfer/FileManager/FileManager.cs` (if writer exists).
3. Keep helper logic inside adapter or backend-specific subfiles.

## Existing examples

- `GIMtransfer/read/GIMocctRead/GIMocctReadElements.cs`
- `GIMtransfer/write/GIMocctWrite/GIMocctWriteElements.cs`
- `GIMtransfer/read/DWGReader/DWGReadElement.cs`
- `GIMtransfer/read/AssimpReadElements.cs`
- `GIMtransfer/write/AssimpWriteElements.cs`
