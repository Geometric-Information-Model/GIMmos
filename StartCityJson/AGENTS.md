# AGENTS.md

## Scope
- `StartCityJson` is a long-running map-data generation pipeline using CityJSON and `GimData`.
- It is an operational data-production utility, not core `GimData`/`GIMtransfer` logic.

## Do / Don't
- Do isolate pipeline-specific logic here.
- Do assume very large inputs and long runtimes; prefer resumable and observable processing.
- Don't change core model invariants here; implement those in `GIMCompress`/`GIMtransfer`.
- Don't block core library changes on this project's historical quirks.

## Key Entry Files
- `StartCityJson.csproj`
- `StartCityJson.sln`
- `Program.cs`
- `MainScript.cs`

## Build / Test Commands
- Build: `dotnet build "StartCityJson.csproj" -c Debug`
- Run (local only with proper data): `dotnet run --project "StartCityJson.csproj"`

## Dependencies / Interop Notes
- Depends on `GIMtransfer`, `GIMCompress`, and `GimBin` via project reference or nuget.org package fallback (pinned at 1.0.10).
- Includes data-access dependencies (EF Core providers), so environment setup matters.
