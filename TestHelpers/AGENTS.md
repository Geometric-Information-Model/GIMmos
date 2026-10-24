# AGENTS.md

## Scope
- `TestHelpers` provides shared fixtures, sample-data loading, and helper assertions for `TestPlatform`.
- It centralizes reusable test input wiring for CAD/GIM scenarios.

## Do / Don't
- Do add reusable helpers here instead of duplicating setup code in individual test classes.
- Do keep helper APIs deterministic and side-effect minimal.
- Don't hide test failures with broad exception swallowing.
- Don't couple helpers to UI-layer behavior.

## Key Entry Files
- `TestHelpers.csproj`
- `TestGimDatas.cs`
- `CorrectnessCheck.cs`
- `TestComparison.cs`

## Build / Test Commands
- Build helper library: `dotnet build "TestHelpers.csproj" -c Debug -p:Platform=x64`
- Validate via tests: `dotnet test "..\\TestPlatform\\TestPlatform.csproj" -c Debug -p:Platform=x64`

## Dependencies / Interop Notes
- References `GIMCompress` and `GIMtransfer`.
- Uses NUnit and NLog packages plus CAD libraries used by test fixtures.
