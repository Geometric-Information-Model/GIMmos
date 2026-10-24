# AGENTS.md

## Scope
- `TestUI` is the manual UI harness for exercising `GIMtransfer` conversion features.
- It is also used for DLL-oriented build packaging via solution variants.

## Do / Don't
- Do use this project for manual verification of read/write/promote/demote workflows.
- Do keep UI test actions aligned with core engine behavior in `../GIMtransfer`.
- Don't implement core conversion logic in UI forms when it belongs in `GIMtransfer` or `GIMCompress`.
- Don't treat this as authoritative automated test coverage.

## Key Entry Files
- `TestUI - Dll.sln`
- `Release - Dll.sln`
- `TestUI.sln`
- `TestUI.csproj`
- `Form1.cs`
- `Program.cs`

## Build / Test Commands
- Prefer full solution build (DLL packaging path): `dotnet build "Release - Dll.sln" -c Release -p:Platform=x64`
- Debug UI against the RelWithDebInfo ship drop: `dotnet build "Release - Dll.sln" -c Debug -p:Platform=x64`
- `TestUI - Dll.sln` is the older Debug packaging path (GimLogger/Gmsh project refs); prefer `Release - Dll.sln`.
- Visual Studio workflow: open `Release - Dll.sln` and build `Debug|x64` or `Release|x64`.

## Dependencies / Interop Notes
- References `GIMtransfer`, `GIMCompress`, `GimBin`, and OCCT wrapper DLLs.
- Post-build copies `GIMocctWrapper (DLL)` binaries into output.
