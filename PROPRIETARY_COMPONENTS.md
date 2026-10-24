# Proprietary components

This inventory identifies GIM-owned components used by this repository whose
source and implementation are closed and outside the EUPL-covered GIM source.

## GimBin and GimCrypt

`GimBin` and `GimCrypt` provide internal binary serialization and cryptographic
support for GIM data flows. They remain GIM-owned proprietary components.
Public builds restore `GimBin` and `GimCrypt` from [nuget.org](https://www.nuget.org/)
when the `GIMzip` projects are absent. This repository pins version 1.0.10. A
checkout with those projects uses the project references.

These components are not licensed under the repository's EUPL terms.

## Boundary and third-party components

Independent modules may retain their chosen licence when they use documented
public GIM APIs, assembly boundaries, or process APIs, as described in
[`EXCEPTIONS.md`](EXCEPTIONS.md). This generic permission does
not limit itself to components listed here.

External vendor plugins and SDK adapters are not GIM-owned inventory and need
not be listed here. Their own licence terms continue to apply. Third-party
components redistributed by this repository are documented in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

## Reporting issues

File bugs or feature requests involving GimBin or GimCrypt in this repository
and apply label `component:gimbin`. Maintainers triage them to the closed
implementation.
