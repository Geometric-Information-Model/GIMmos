# GimTransfer package consumer guide

This guide covers consuming `GimTransfer` from a .NET application on Windows.
The package targets `net10.0`, but its native CAD integrations require an x64
Windows process.

## Quick checklist

- Install the .NET 10 SDK for restore and build.
- Run the application with the .NET 10 runtime.
- Set the consumer project and process to `x64`. Do not rely on `Any CPU` for
  native CAD operations.
- Restore the package from the configured feed:

```powershell
dotnet add package GimTransfer
```

- Build with the x64 platform:

```powershell
dotnet build -c Release -p:Platform=x64
```

- Keep the package's native files beside the application output.
- For source builds, make authorized closed-component assemblies available.
  Only those proprietary binary components use conditional source project
  references: when their source projects exist, project references are used;
  otherwise, the nuget.org package fallback is used. The OCCT wrapper remains
  a local DLL reference. These components remain proprietary; do not inspect or
  redistribute them outside their license terms.
- Review [third-party notices](../../THIRD_PARTY_NOTICES.md) before
  redistributing an application that includes the package or its native
  payload.

## What the package supplies

`GimTransfer` packages a build target and the native DLL payload needed by its
OCCT references. When NuGet imports `Gimtransfer.targets`, it:

1. References `GIMocctCLI.dll` and `GIMocctWrapper.dll` from the package
   `native/` directory.
2. Marks those references private so they flow to the consumer output.
3. Copies every DLL in `native/` to the consumer output directory.

The package project explicitly packs the native DLLs and the build target.
It does not make closed source components public. Confirm that your package
distribution includes all authorized managed and native dependencies required
by your selected workflows.

The source project also copies local `CadexLib/vc14.2/**` content when present.
The third-party notice classifies Cadex as vestigial and not required to build.
Do not assume this source-build content is part of the package's `native/`
payload; verify the deployment output if a legacy Cadex-backed flow is used.

## Target and architecture requirements

The project declares:

- Package ID: `GimTransfer`
- Target framework: `net10.0`
- Declared platform: `x64`
- Windows native dependencies: OCCT wrapper binaries and the Assimp
  `win-x64` native package

The target framework alone does not make the native integrations portable.
Use Windows x64 for OCCT, IFC, and Assimp-backed workflows. Set
`<PlatformTarget>x64</PlatformTarget>` in the consuming project, build with
`-p:Platform=x64`, and verify that the launched process is x64.

## Native dependency checklist

### OCCT-backed workflows

STEP, STP, IGES, IGS, and IFC paths use the OCCT wrapper references. The native
payload must include the wrapper DLLs and their supporting DLLs, including the
OCCT `TK*.dll` set and other files shipped with that payload. The current
notices also identify FreeType, .NET `Ijwhost.dll`, and bundled MSVC runtime
files as part of the native runtime context.

The current native wrapper uses Debug MSVC runtime DLLs. Those Debug variants
are an interim distribution condition and are not supplied by the normal
Release VC++ redistributable. Keep the bundled files with the application and
follow the disclosure in `THIRD_PARTY_NOTICES.md`.

### Assimp-backed workflows

Assimp uses the managed `StirlingLabs.Assimp.Net` package and the native
`StirlingLabs.assimp.native.win-x64` package. Restore must resolve both. The
native asset must match the x64 process; copying an x86 Assimp DLL into the
output does not fix an architecture mismatch.

The other direct runtime packages are pinned in the project file. The complete
version and license list is maintained in
[`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md).

## Optional IFC preview conversion

The normal IFC path uses the OCCT-backed reader. A preview or fallback path
can invoke an external `IfcConvert.exe`, write a temporary DAE file, and read
that result through Assimp.

Install the optional executable at:

```text
%LOCALAPPDATA%\Programs\GIM\GIMIfc\IfcConvert.exe
```

The executable is not a NuGet dependency. The path is used when IFC preview is
requested and also when the OCCT IFC reader falls back after an error. Without
the executable, those fallback cases fail. The OCCT wrapper contains the
IfcOpenShell code used by its IFC path; separate `IfcParse.dll` and
`IfcGeom.dll` files are not expected by the current notices.

## Optional GLB compression

GLB generation works without the optional compressor. For larger GLBs, the
package can invoke `gltfpack.exe` from:

```text
%LOCALAPPDATA%\Programs\GIM\GIMMesh\gltfpack.exe
```

The compressor is attempted only for blobs at least 128 KiB. It has a
60-second timeout and keeps the original GLB when the tool is missing, fails,
times out, produces no output or an empty output, or does not make the file
smaller.
Failures are logged as warnings. The resulting file uses
`EXT_meshopt_compression`; only enable this path when every downstream GLB
consumer supports that extension. `gltfpack.exe` is optional and is not packed
by the project file.

## Diagnose startup and missing-DLL failures

- **`.NET runtime not found`:** install the .NET 10 runtime matching the
  application deployment, or publish self-contained.
- **`BadImageFormatException` or Windows error `0x8007000B`:** check the
  process, project platform, and every native dependency. Use x64 throughout;
  do not mix x86 and x64 binaries.
- **`DllNotFoundException` for an OCCT wrapper:** inspect the application
  output for both wrapper DLLs and their supporting native files. Confirm the
  NuGet build target was imported and copied `native/*.dll`.
- **OCCT wrapper loads, then reports a missing dependency:** check the complete
  OCCT support set, FreeType, `Ijwhost.dll`, and the bundled MSVC runtime
  files. Installing only the Release VC++ redistributable does not provide the
  current Debug runtime variants.
- **Assimp load failure:** restore the package again, confirm the
  `win-x64` native asset is present, and verify the process is x64.
- **IFC preview failure:** check the exact `IfcConvert.exe` location and run
  the application under the same Windows user whose `%LOCALAPPDATA%` contains
  the tool.
- **GLB compression warning:** missing or failing `gltfpack.exe` is
  non-fatal; the original, uncompressed GLB is retained. If compression
  succeeds but a viewer cannot load the file, check `EXT_meshopt_compression`
  support before changing the application.

If a clean x64 output still fails, capture the first missing module named by
the loader, the process architecture, build configuration, and whether the
failure occurs during restore, startup, or a specific conversion. That
information distinguishes package-copy problems from native ABI problems.
