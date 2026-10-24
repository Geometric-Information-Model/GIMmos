# Developer setup

This guide covers local .NET development for GIMmos on Windows.

## Prerequisites

- Windows x64.
- .NET 10 SDK.
- Visual Studio with .NET 10 and desktop development support for the UI and test harness.
- Required native or vendor-supplied runtime files for projects that use them.

Build and run x64 configurations. Some projects reference external binary or
package dependencies; their availability can affect the full solution build.

## Core build

Run from the repository root:

```powershell
dotnet restore "GIMtransfer/GIMtransfer.csproj"
dotnet build "GIMtransfer/GIMtransfer.csproj" -c Debug -p:Platform=x64
```

`GIMtransfer` references `GIMCompress`, so this path also builds the model
project and its required project dependencies.

## Full UI and test build

The main entry point is `TestUI.sln`; it currently lives under `TestUI/` and
may move later. This checkout also contains `TestUI - Dll.sln`, which local
guidance identifies as the preferred full-integration solution. Open
`TestUI/TestUI - Dll.sln` for full integration.

1. Open `TestUI/TestUI - Dll.sln`.
2. Select `Debug` and `x64`.
3. Build the solution.
4. Run the automated tests:

```powershell
dotnet test "TestPlatform/TestPlatform.csproj" -c Debug -p:Platform=x64
```

Start the `TestUI` project for manual conversion checks after the solution
build succeeds.
