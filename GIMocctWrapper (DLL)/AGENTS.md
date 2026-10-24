# AGENTS.md

## Scope
- `GIMocctWrapper (DLL)` contains prebuilt OCCT wrapper binaries used by `GIMtransfer`.
- Folder root is the **RelWithDebInfo ship set** (git + NuGet `native/`).
- `Debug/` is a local-only overlay from a GIMocctCLI Debug|x64 build (gitignored).

## Do / Don't
- Do treat the folder root as binary runtime dependency storage for the product.
- Do update all related wrapper binaries together when upgrading the ship set.
- Do build `GIMocctCLI.sln` **Release|x64** to refresh the folder root; **Debug|x64** writes `Debug/` only.
- Don't hand-edit, patch, or partially replace individual DLLs without compatibility validation.
- Don't implement OCCT wrapper source changes here.

## Key Entry Files
- `GIMocctCLI.dll`
- `GIMocctWrapper.dll`
- `../GIMtransfer/GIMtransfer.csproj`
- `../TestUI/Release - Dll.sln`

## Build / Test Commands
- No direct build in this folder.
- Validate with consumer builds:
  - `dotnet build "..\\GIMtransfer\\GIMtransfer.csproj" -c Debug -p:Platform=x64`
  - `dotnet build "..\\TestUI\\Release - Dll.sln" -c Release -p:Platform=x64`

## Dependencies / Interop Notes
- Enables OCCT-backed read/write functionality (notably STEP/IGES/IFC flows).
- OCCT upstream context: https://dev.opencascade.org
