# AGENTS.md

## Scope
- `GIMtwin (DLL)` is a WIP binary reference area for twin/dedup functionality.
- Intended purpose: reduce `GimData` size by identifying duplicate elements and using instantiation patterns.

## Do / Don't
- Do treat this folder as external-binary integration surface.
- Do keep integration points in `GIMtransfer` clearly feature-flagged or optional if WIP.
- Don't implement source logic directly in this folder.
- Don't assume full production readiness.

## Key Entry Files
- `../TestUI/TestUI - Dll.sln` (optional reference hooks)
- `../GIMtransfer/GIMtransfer.csproj` (consumer project)

## Build / Test Commands
- No direct build in this folder.
- Validate by building consumers and running relevant conversion tests.

## Dependencies / Interop Notes
- Source-of-truth logic is outside this repo; this folder is for binary integration only.
