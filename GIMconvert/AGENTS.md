# AGENTS.md

## Scope
- `GIMconvert` is a developer/test conversion utility app.
- It is not the canonical conversion engine; core logic belongs to `../GIMtransfer`.

## Do / Don't
- Do keep this project as a thin utility layer around shared conversion APIs.
- Do use it for quick exploratory/manual conversion checks.
- Don't duplicate or fork conversion behavior from `GIMtransfer`.
- Don't treat this app as a production runtime dependency.

## Key Entry Files
- `GIMconvert/GIMconvert.csproj`
- `GIMconvert/GIMconvert.sln`
- `GIMconvert/Program.cs`
- `GIMconvert/GIMconvertForm.cs`

## Build / Test Commands
- Build: `dotnet build "GIMconvert/GIMconvert.csproj" -c Debug`
- Run: `dotnet run --project "GIMconvert/GIMconvert.csproj"`

## Dependencies / Interop Notes
- Depends on `GIMtransfer`, `GIMCompress`, `GimLogger`, and external `GimBin` project reference.
- Uses Windows Forms and legacy CAD references.
