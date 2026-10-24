# AGENTS.md

## Scope
- `GimLogger` (`GimUtilities.csproj`) provides shared logging and progress utilities used by multiple projects.
- It should remain lightweight and stable.

## Do / Don't
- Do keep logging/progress APIs backward compatible where possible.
- Do avoid introducing heavy dependencies here.
- Don't add conversion/domain logic to this utility project.
- Don't make this project responsible for business rules of `GimData`.

## Key Entry Files
- `GimUtilities.csproj`
- `GimLogger.cs`
- `GimProgressStatus.cs`

## Build / Test Commands
- Build: `dotnet build "GimUtilities.csproj" -c Debug -p:Platform=x64`
- Integration check: build dependent project `dotnet build "..\\GIMtransfer\\GIMtransfer.csproj" -c Debug -p:Platform=x64`

## Dependencies / Interop Notes
- Uses NLog; consumers include `GIMCompress`, `GIMtransfer`, and test/tooling projects.
