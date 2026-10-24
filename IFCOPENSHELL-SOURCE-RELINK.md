# IfcOpenShell corresponding source and relink material

This package contains `GIMocctCLI.dll`, which statically links
IfcOpenShell **0.8.0** at upstream commit
`404c39873d6e590a148445794585e50d8ba75313`. The upstream source is available
at [IfcOpenShell commit 404c398](https://github.com/IfcOpenShell/IfcOpenShell/tree/404c39873d6e590a148445794585e50d8ba75313).
The upstream project is licensed under LGPL-3.0-or-later.

`ifc-sdk-1` is the IfcOpenShell SDK dependency role used by the wrapper build
to provide the IfcParse/IfcGeom headers and static libraries linked into
`GIMocctCLI`; no separate `IfcParse.dll` or `IfcGeom.dll` is distributed.

## Obtaining corresponding source and rebuilding

Recipients can obtain the corresponding IfcOpenShell source from the pinned
commit URL above. The public
[GIMocctWrapper repository](https://github.com/Geometric-Information-Model/GIMocctWrapper)
contains the wrapper source and its
[IfcOpenShell build and relink instructions](https://github.com/Geometric-Information-Model/GIMocctWrapper/blob/dev/IfcOpenShell_INSTALL.md).
Those instructions document the `v0.8.0` Windows build, the required GIM
patches, the `ifc-sdk-1` dependency role, and the steps to rebuild
IfcOpenShell and relink it into a modified `GIMocctCLI`.

For a reproducible relink, clone the pinned IfcOpenShell commit, follow the
wrapper instructions to apply the documented patches and build the x64
static SDK, then rebuild `GIMocctCLI` from the wrapper source. The resulting
wrapper can be relinked with the rebuilt `ifc-sdk-1` libraries without using
any proprietary GIMtransfer, GimBin, or GimCrypt source. This notice does not
grant rights to proprietary components; their status is described in
`legal/PROPRIETARY_COMPONENTS.md`.

