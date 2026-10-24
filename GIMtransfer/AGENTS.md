# AGENTS.md

## Scope
- This file governs the `GIMmos/GIMtransfer` subproject (core conversion engine).
- Responsibilities: read external formats to `GimData`, write `GimData` to external formats, GIM JSON conversion, promotion/demotion, and selected utility transforms.
- This is distinct from repository root orchestration.

## Do / Don't
- Do route format logic through `FileManager` partials before adding new entrypoints.
- Do keep read/write parity in mind when adding format support.
- Do preserve `GimData` invariants during promotion/demotion and transform operations.
- Don't bypass `FileTypes`/support checks when introducing format handling.
- Don't duplicate format tables in docs; point to `FileTypes.cs`.

## Key Entry Files
- `JsonManager/README.md` — canonical GIM JSON reader dispatch, sniff, and schema policy ([`VERSION_HISTORY.md`](JsonManager/VERSION_HISTORY.md) for schema ↔ code history)
- Agent Skill: `.agents/skills/gimtransfer-json-versioning/SKILL.md` — versioning workflow for agents
- `FileManager/FileManager.cs`
- `FileManager/FileManagerInternal.cs`
- `FileManager/FileManagerPrivate.cs`
- `FileManager/FileTypes.cs`
- `read/AssimpReadElements.cs`
- `read/GIMocctRead/GIMocctReadElements.cs`
- `write/AssimpWriteElements.cs`
- `write/GIMocctWrite/GIMocctWriteElements.cs`

## Build / Test Commands
- Build: `dotnet build "GIMtransfer.csproj" -c Debug -p:Platform=x64`
- Preferred full integration build: `dotnet build "..\\TestUI\\TestUI - Dll.sln" -c Debug -p:Platform=x64`
- Run tests against this engine: `dotnet test "..\\TestPlatform\\TestPlatform.csproj" -c Debug -p:Platform=x64`

## Dependencies / Interop Notes
- Uses `GIMCompress` model library and `GimLogger`.
- Uses `GimBin` and `GimCrypt` via project reference (if external repo present) or nuget.org package fallback.
- STEP/IGES and related BRep paths depend on `GIMocctWrapper (DLL)` binaries.
- Additional paths use Assimp and other NuGet-backed readers/writers.
