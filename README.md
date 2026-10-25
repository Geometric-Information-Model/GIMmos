# GIMmos

GIMmos is a .NET Ecosystem for GIM CAD data workflows. It reads, transforms,
and writes model data.

## Repository map

| Path | Purpose |
| --- | --- |
| `GIMtransfer/` | Core read, transform, and write library |
| `GIMCompress/` | `GimData` model and related entities |
| `GimLogger/` | Shared logging utilities |
| `GIMtransfer/JsonManager/` | Versioned GIM JSON readers and dispatch policy |
| `TestHelpers/` | Test support and fixture helpers |
| `TestPlatform/` | Automated conversion tests |
| `TestUI/` | Windows manual harness and solution entry point |

## Choose a path

- **Developers:** follow the [developer setup guide](docs/guides/0004-developer-setup.md).
- **Model maintainers:** read the [GimData structure](docs/specs/gimdata/README.md).
- **JSON consumers:** read the [JSON versioning and dispatch guide](GIMtransfer/JsonManager/README.md) and [schema history](GIMtransfer/JsonManager/VERSION_HISTORY.md).
- **Contributors:** use the developer setup guide for core builds, UI checks, and tests.

## Documentation

- [Package consumer guide](docs/guides/0005-package-consumer.md)
- [Format specification](docs/specs/formats/README.md)
- [SDK workflows](docs/guides/0006-sdk-workflows.md)
- [GimData specification](docs/specs/gimdata/README.md)
- [Test and fixture runbook](docs/guides/0007-test-and-fixture-runbook.md)

Use linked canonical documents for format behavior and model rules.

## Licensing and governance

GIM means Geometric Information Model. This repository is the central repo
for GIM Managed Open Source. GIM-owned source in `GIMtransfer`, `GIMCompress`,
and `GimLogger` is licensed under
[EUPL v1.2](LICENSE), with the independent-module permission in
[`EXCEPTIONS.md`](EXCEPTIONS.md). GimBin and GimCrypt remain
GIM-owned proprietary components; see
[`PROPRIETARY_COMPONENTS.md`](PROPRIETARY_COMPONENTS.md). Third-party
components retain their own licences and are listed in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

GIM is part of the DMI public-private ecosystem. DMI is a collaboration
partner and client/customer. DMI is not this project's IP owner or licensor.
DMI manages GIMmos and sets the roadmap and policy. We welcome DMI members and
outside contributors as contributors. See [`CONTRIBUTING.md`](CONTRIBUTING.md)
and [`CONTRIBUTOR_TERMS.md`](CONTRIBUTOR_TERMS.md); existing
[`CODEOWNERS`](CODEOWNERS) governs required review.

The owner is the project's licensor and copyright holder for the covered GIM
source. Technical maintainers review and merge changes under GIMmos according
to the DMI-stewarded roadmap and policy.

## Component scope

This repository owns CAD-processing logic and GIM data handling. `GIMmanager`
orchestrates local processing, caching, and sync; `CoreWebApi` is the remote
database-facing backend; `GIMfront` is the browser frontend; `GIMocctWrapper`
provides OCCT-based STEP/IGES interop; and `GIMzip` owns proprietary binary
components. See [`WORKSPACE.md`](WORKSPACE.md) for boundaries.

## Upcoming

- NuGet package signing for packages pushed by `release.yml`. Public pull-request CI signs nothing.
- Optional later: DCO or CLA.
- Optional later: issue templates, a pull request template, and detailed contribution rules.

## Ecosystem

For DMI context, see [DMI](https://dmi-ecosysteem.nl/over-dmi/) and the
[GIMtransfer managed-open-source event](https://dmi-ecosysteem.nl/events/kansen-van-gimtransfer-als-managed-open-source-binnen-dmi/).

For cross-project architecture, routing rules, and repository boundaries, start with `WORKSPACE.md`. Use `AGENTS.md` for repo-local agent guidance.
