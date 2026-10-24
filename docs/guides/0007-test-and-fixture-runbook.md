# Test and fixture runbook

Run commands from the GIMmos repository root. Use `Debug|x64` for local
builds and keep CAD fixtures outside the repository.

## Choose a test lane

### Self-contained tests

Self-contained tests use inline values or checked-in test data. Examples include
vector, matrix, rounding, curve, seam, and JSON-version tests under
`TestPlatform/`. JSON reader fixtures live under
`TestPlatform/TestData/JsonManager/`. `TestPlatform.csproj` copies its JSON files
to the test output directory.

These tests do not need a CAD file from the fixture directory. The test project
still references CAD-related assemblies, so restore and build requirements can
be broader than the input-data requirements.

### CAD-fixture tests

`TestPlatform/GimDataConversionTests.cs` creates `TestGimDatas` with this
relative fixture location:

```text
../../../../../../CAD Files/
```

The path is resolved from the test process working directory. The filenames
come from the hard-coded list in `TestHelpers/TestGimDatas.cs`; the list includes
multiple CAD formats and a GIM binary sample. The repository does not contain
that `CAD Files` directory or an acquisition script.

Obtain only approved fixtures through the project’s authorized channels. Keep
them outside the checkout, preserve the expected filenames, and do not commit
or redistribute files subject to licensing or confidentiality restrictions.
Do not replace missing fixtures with guessed files: a different model can change
parser, topology, memory, and runtime behavior.

## PowerShell commands

Restore and build the test project:

```powershell
dotnet restore "TestPlatform/TestPlatform.csproj" -p:Platform=x64
dotnet build "GIMtransfer/GIMtransfer.csproj" -c Debug -p:Platform=x64
dotnet build "TestPlatform/TestPlatform.csproj" -c Debug -p:Platform=x64
```

Run the complete automated test project after a successful build:

```powershell
dotnet test "TestPlatform/TestPlatform.csproj" -c Debug -p:Platform=x64 --no-build
```

The full DLL-oriented solution build is:

```powershell
dotnet build "TestUI/TestUI - Dll.sln" -c Debug -p:Platform=x64
```

For the core model project alone:

```powershell
dotnet build "GIMCompress/GIMCompress.csproj" -c Debug -p:Platform=x64
```

## Manual TestUI checks

Open `TestUI/TestUI - Dll.sln` in Visual Studio, select `Debug` and `x64`, and
build the solution. Alternatively, start the WinForms harness from PowerShell:

```powershell
dotnet run --project "TestUI/TestUI.csproj" -c Debug -p:Platform=x64
```

With an approved input file:

1. Use the file picker to select a supported CAD, JSON, or GIM input.
2. Select **Read GimData**. Confirm the operation completes without an
   exception and that conversion controls become available.
3. Select an output format and use **Run**. Confirm the output is created and
   can be opened by the intended consumer.
4. For JSON checks, use **File To JSON** and **JSON To File** with a copy of
   the input or output. Confirm the resulting file is readable.

Manual TestUI checks complement automated tests. TestUI is not an authoritative
automated test suite.

## Determinism and parallel execution

`TestPlatform` uses NUnit. The CAD conversion test assembly sets its
parallelism level to `3`, and active fixture theories use child-level
parallelization. `TestGimDatas` caches loaded data and uses per-file locks;
`Get` returns a clone, while `GetQuickAndUnsafe` is read-only access to cached
data.

Keep tests deterministic:

- use checked-in self-contained data where possible;
- keep fixture filenames and paths stable;
- do not mutate values returned by `GetQuickAndUnsafe`;
- use clones or independent `GimData` instances for mutation and concurrent
  conversion;
- avoid changing parallelism to work around a failure;
- treat memory-heavy or native-backed conversions as resource-sensitive even
  when NUnit can schedule them in parallel.

## CI status and interpretation

The current `.github/workflows/ci.yml` workflow restores `TestPlatform`, builds
the core library, builds the test project, and runs the same test command shown
above. It runs on pushes to `dev` and `main`, on merged pull requests, and by
manual dispatch.

The test step is currently informational:

```yaml
continue-on-error: true
```

The workflow comment records `143` pre-existing failures. A green workflow does
not therefore prove that all tests pass. Restore and build failures remain
blocking for the job; test failures are reported but do not currently gate it.
Local results should include whether the self-contained lane, CAD-fixture lane,
or manual TestUI checks were run.
