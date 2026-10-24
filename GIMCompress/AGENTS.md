# AGENTS.md

## Scope
- `GIMCompress` defines the `GimData` architecture and core entity behavior.
- This project is the authoritative location for `GimData` model semantics and helper contracts.
- It also contains GIM JSON entity definitions used for transfer/visualization paths.

## Do / Don't
- Do treat `../docs/specs/gimdata/README.md` as the source of truth for `GimData` usage rules.
- Do preserve index integrity when adding/removing/reparenting elements (`Elements`, `Locals.parent`, `Objects.element`).
- Do keep element storage contracts (`int`/`double`/`byte` counts) compatible.
- Don't change existing element type structure contracts for legacy data; introduce a new GIMbin version if structure must evolve.
- Don't create invalid hierarchies (multiple roots, geometry directly under part, invalid object targets).

## Key Entry Files
- `../docs/specs/gimdata/README.md`
- `entities/GimData.cs`
- `entities/GimElement.cs`
- `entities/GimObject.cs`
- `entities/GimLocal.cs`
- `EntityHelpers.cs`
- `entities/ClassesForGimJSON.cs`
- `entities/ElementAdapters/` — adapters for type-safe element access
- `entities/ElementBuilders/` — builders for fluent element construction

## Build / Test Commands
- Build: `dotnet build "GIMCompress.csproj" -c Debug -p:Platform=x64`
- Preferred integration validation: build the main DLL solution `dotnet build "..\\TestUI\\TestUI - Dll.sln" -c Debug -p:Platform=x64`
- Optional focused build: `dotnet build "..\\GIMtransfer\\GIMtransfer.csproj" -c Debug -p:Platform=x64`

## Dependencies / Interop Notes
- `GIMCompress` is consumed by `GIMtransfer`, `TestUI`, `TestPlatform`, `StartCityJson`, and conversion tools.
- Changes here can cascade into binary serialization and downstream format readers/writers.
