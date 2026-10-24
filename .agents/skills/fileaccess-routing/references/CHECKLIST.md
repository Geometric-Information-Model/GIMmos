# Routing Change Checklist

Use this checklist whenever you add or modify format routing.

- Confirm `fileType` normalization is in place (lowercase, leading dot handling).
- Confirm backend gate uses `SupportsType(...)` where applicable.
- Confirm read/write parity expectation is considered for the format.
- Confirm progress reporting behavior remains coherent with existing patterns.
- Confirm temp file/folder path usage follows existing `FileAccess` helpers.
- Confirm unsupported format behavior remains explicit and predictable.
- Confirm backend-specific logic stays in backend adapter classes.
- Confirm no duplicate format matrix docs are introduced (refer to `FileTypes.cs`).
