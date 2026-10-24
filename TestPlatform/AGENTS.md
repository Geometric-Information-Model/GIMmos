# AGENTS.md

## Scope
- `TestPlatform` contains NUnit-based automated tests for `GIMtransfer` and `GIMCompress`.
- Current tests are useful but may be partially outdated; preserve deterministic behavior.

## Do / Don't
- Do add deterministic, thread-safe tests (`Parallelizable` is used heavily).
- Do prefer validating `GimData` correctness and roundtrip consistency.
- Don't rely on mutable external state without explicit setup/teardown.
- Don't use flaky timing-based assertions.

## Key Entry Files
- `TestPlatform.csproj`
- `GimDataConversionTests.cs`
- `GimDataGeneralTest.cs`
- `GimDataLocalTests.cs`
- `GimDataJsonTests.cs`

## Build / Test Commands
- Run tests: `dotnet test "TestPlatform.csproj" -c Debug -p:Platform=x64`
- Run from repo root: `dotnet test "TestPlatform/TestPlatform.csproj" -c Debug -p:Platform=x64`

## Dependencies / Interop Notes
- Depends on `TestHelpers`, `GIMtransfer`, and `GIMCompress`.
- Uses large CAD-related references (Cadex + others); ensure local environment has required binaries.
