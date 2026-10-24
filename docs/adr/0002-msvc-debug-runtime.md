# MSVC Debug runtime redistribution (interim)

This project previously shipped **Debug** Microsoft C/C++ runtime DLLs
(`msvcp140d.dll`, `ucrtbased.dll`, `vcruntime140d.dll`, `vcruntime140_1d.dll`)
because `GIMocctCLI.dll` was built in Debug. Microsoft's redistributable
license does **not** cover those Debug binaries. That was a **disclosed
interim** until a RelWithDebInfo / Release native stack shipped.

**Status:** superseded (2026-08-20). `GIMmos/GIMocctWrapper (DLL)/`
now contains RelWithDebInfo `GIMocctCLI.dll` plus 27 Authenticode-signed
Release OCCT `TK*.dll`s. Debug CRT DLLs are gone from that folder. The
NuGet `native/` payload packs `*.dll` from the same folder, so they no
longer enter the package either. Release MSVC/UCRT come from `VC_redist.x64`
/ Windows.

## Considered Options

- **Keep Debug, disclose** (chosen as interim) — keep the Debug wrapper for
  development, bundle Debug CRTs app-local, disclose in
  [`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md). Risk: license
  scanners and due-diligence reviews flag it once we go public.
- **Release build of the native stack** (the fix; now landed in the drop) —
  rebuild OCCT, IfcOpenShell, and GIMocctCLI in Release / RelWithDebInfo |
  x64. Debug runtimes drop out; payload shrinks; CAD paths stop paying
  Debug CRT / unoptimized OCCT cost.
- **Strip Debug CRTs from NuGet only** (interim hygiene) — exclude them from
  package payloads while the installer still bundles them so the product runs.
  Does not fix the installer/distribution issue.

## How they used to ship

1. `GIMmanager/GIMmanager.csproj` — post-build copy of `vcruntime*.dll`,
   `msvcp140d.dll`, `ucrtbased.dll` (and OCCT/wrapper DLLs) into output.
   That target is now commented out; GIMmanager consumes natives via the
   GimTransfer NuGet package (`Gimtransfer.targets` copies `native\*.dll`).
2. `GIMmanager/GIMmanagerSetup.iss` — installer ships that build output
   app-local and already downloads `VC_redist.x64` (Release CRT).
3. GIMtransfer NuGet `native/` payload — packs `GIMocctWrapper (DLL)/`.
   After the RelWithDebInfo drop, that folder has no Debug CRTs.

## License analysis

| DLL | Source | Redistributable? |
|---|---|---|
| `msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`, `concrt140.dll` (Release) | VC++ redistributable | **Yes** |
| `ucrtbase.dll` (Release UCRT) | Windows 10/11 | **Yes** — OS component |
| `msvcp140d.dll`, `vcruntime140d.dll`, `vcruntime140_1d.dll`, `ucrtbased.dll` (Debug) | Visual Studio | **No** — development machines only |

## After the RelWithDebInfo drop landed

Done in the GIMmos drop:

1. RelWithDebInfo `GIMocctCLI.dll` (no debug CRT dependents).
2. Debug runtime DLLs removed from `GIMocctWrapper (DLL)/`.
3. NuGet `native/` no longer packs those filenames (wildcard over the cleaned folder).

The commented Debug-runtime `xcopy` in `GIMmanager.csproj` is leftover
hygiene, not a ship path. `GIMocctCLI.sln` Release|x64 PostBuild refreshes
this drop; Debug|x64 writes `GIMocctWrapper (DLL)/Debug/` only.

IfcOpenShell and GIMocctCLI must share configuration (`LNK2038` — see
`GIMocctWrapper/IfcOpenShell_INSTALL.md`).

## Related

- [`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md)
- `GIMmanager/GIMmanager.csproj`
- `GIMmanager/GIMmanager/GIMmanagerSetup.iss`
