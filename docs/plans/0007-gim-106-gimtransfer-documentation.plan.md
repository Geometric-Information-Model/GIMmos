---
name: GIM-106 GIMtransfer documentation
overview: Document GIMtransfer around its highest-value user and SDK workflows. Prioritize a truthful Windows/package onboarding path, capability boundaries, and strategy-level developer guidance while keeping public documentation dense and protecting proprietary implementation details.
todos:
  - id: A1
    content: Add the local unslop skill and establish the public README and developer quickstart
    status: completed
  - id: A2
    content: Document package consumption and native runtime prerequisites
    status: completed
  - id: A3
    content: Publish a compact, truthful format capability specification
    status: completed
  - id: A4
    content: Document public SDK workflows and conversion semantics
    status: completed
  - id: A5
    content: Move and promote the GimData specification, then document the test and fixture workflow
    status: completed
  - id: Z1
    content: Resolve final documentation navigation and error-contract findings
    status: completed
isProject: false
---

# GIM-106 GIMtransfer documentation plan

## Scope

- Repository: `D:/Work/GIM/GIMtransfer` only.
- Ticket: GIM-106, “Document GIMtransfer Repo”; Jira has no description or acceptance criteria, so this plan defines the proposed deliverables.
- Audience: developers evaluating or consuming `GimTransfer` as an SDK and contributors working on this repository.
- Documentation goal: explain mental models, workflows, boundaries, prerequisites, and failure modes. Do not add extensive inline comments or reproduce source code in prose.
- Information density takes priority over file length. Prefer short sections, compact tables, and links to canonical material.
- Public documentation must never name, link to, or provide paths into private sibling repositories. Describe external/private dependencies generically where necessary.
- Explicitly out of scope: source-code fixes, proprietary binary layout/compression/cryptography details, broad Doxygen generation, adapter/release/contribution governance docs, and documentation for sibling repositories.

## Content production rule

Every builder writing documentation must use the available `/unslop` skill for the content, with the `crisp` preset where applicable. Treat that skill as pre-existing external input: use it as supplied, do not edit or clean it, and do not claim ownership of its contents. Read and follow its core contract; preserve verified facts, code, quantities, uncertainty, and technical scope. Use it as an editorial pass for dense, human technical prose, not as permission to invent claims or inflate the documents. Keep examples and tables factual and concise.

The currently present `.agents/skills/unslop/` skill package and its corresponding `skills-lock.json` change are pre-existing input for this work. Preserve them exactly as supplied; do not edit, clean, replace, or partially copy the package.

## Research findings and priority

- **P0:** human quickstart is missing; package consumption, Windows x64/native runtime setup, and the distinction between core build and full UI/test build are unclear.
- **P0:** format metadata is broader than actual routing; users need a truthful capability reference distinguishing read/write/preview, native-backed paths, fallbacks, and known limitations.
- **P0:** test/fixture guidance is not trustworthy enough for onboarding; CAD fixtures, external paths, and non-gating CI behavior need explicit treatment.
- **P1:** public SDK entrypoints, output/temp-path behavior, progress, errors, and copyable external examples are undocumented.
- **P1:** GimData’s graph/index invariants and builder/adapter strategy need an SDK-facing specification, while an existing canonical document should be moved to a more discoverable public location.
- **P1:** existing JSON documentation should be promoted through navigation and connected to consumer workflows without duplicating its schema rules.
- **Safety:** treat proprietary binary data and cryptographic helpers as opaque dependencies. Do not document their layout, algorithms, or reverse-engineering details.

## Execution mode

- [x] Verify per step
- [x] Commit per step
- [ ] PR per step
- [x] Critic per step

Step order is critic → verify with user → commit. No intermediate PRs are opened. After all steps and the wrap-up review, consolidate the step commits into one final squashed PR.

## Failure scenarios

- Agent advances while a documentation step is incomplete, internally contradictory, or contains unverified package/runtime claims.
- Agent commits or opens the final PR before the critic and user verification gates complete.
- Public documentation names or links a private sibling repository.
- README or a guide presents broad format metadata as guaranteed runtime support without reconciling actual dispatch behavior.
- Examples use internal/friend-only APIs as if they were available to external package consumers.
- Documentation exposes proprietary binary implementation details or implies unsupported redistribution.
- A step expands into source-code fixes, sibling-repository edits, or deferred governance work.
- Agent produces verbose AI-shaped prose instead of using `/unslop` to keep content concise and information-dense.

## Documentation structure

- Keep `README.md` a short public landing page and navigation surface.
- Put procedural onboarding and workflows under `docs/guides/`:
  - `docs/guides/0004-developer-setup.md`
  - `docs/guides/0005-package-consumer.md`
  - `docs/guides/0006-sdk-workflows.md`
  - `docs/guides/0007-test-and-fixture-runbook.md`
- Put stable capability and model contracts under `docs/specs/`:
  - `docs/specs/formats/README.md`
  - `docs/specs/gimdata/README.md`
- Do not use numbered filenames for specifications. Numbering remains for plans, ADRs, and RFCs.
- Link existing JSON documentation and other canonical local references instead of copying their contents.

## Steps

### Step A1 — add the local writing skill and establish the public landing page [critic, verify, commit] — completed

- **Files / symbols**:
  - `.agents/skills/unslop/**`
  - `skills-lock.json`
  - `README.md`
  - new `docs/guides/0004-developer-setup.md`
- **Change**: Preserve and include the complete currently present `/unslop` skill package and lock entry. Reshape the public README around repository purpose, project map, audience paths, and links to the focused docs. Add a concise developer quickstart with .NET 10, Windows/x64 prerequisites, core build versus full UI/test build, and the main entry point `TestUI.sln` (currently under the TestUI area; this location may change in the future). Remove or rewrite any public README wording that names or links private sibling repositories.
- **Content**: Use `/unslop` with the crisp preset for every drafted or rewritten prose section. Keep the README short; use the setup guide for procedure.
- **Why**: Current README is ecosystem-oriented and mixes public onboarding with private-repository references. New users need one safe, stable starting point.
- **Validate**: Verify commands and paths against local project/solution files. Check that public README and new guide contain no private-repository names, links, or paths. Confirm the full skill package remains self-contained and its lock entry matches.

### Step A2 — document package consumption and native runtime prerequisites [critic, verify, commit] — completed

- **Files / symbols**:
  - new `docs/guides/0005-package-consumer.md`
  - `GIMtransfer/GIMtransfer.csproj`
  - `GIMtransfer/Gimtransfer.targets`
  - local proprietary-component and third-party policy references
- **Change**: Explain `GimTransfer` package consumption, target/runtime requirements, x64 constraints, project-reference versus fallback-binary behavior, native payload copying, OCCT/Assimp prerequisites, optional IFC conversion tooling, optional GLB compression tooling, and common missing-DLL/architecture failure diagnosis. Describe private/proprietary dependencies generically and do not name or link their repositories.
- **Content**: Use `/unslop` with the crisp preset. Prefer a dependency checklist and short troubleshooting bullets over a long dependency inventory.
- **Why**: Package and runtime behavior is currently discoverable only by reading project files and implementation details.
- **Validate**: Verify package ID, target framework, architecture, native paths, optional-tool paths, and policy statements against current local project/target files. Confirm no private-repository references enter the public guide.

### Step A3 — publish a compact, truthful format capability specification [critic, verify, commit] — completed

- **Files / symbols**:
  - new `docs/specs/formats/README.md`
  - `GIMtransfer/FileManager/FileTypes.cs`
  - `GIMtransfer/FileManager/FileManagerInternal.cs`
  - `GIMtransfer/FileManager/FileManager.cs`
  - relevant local format adapter implementations
- **Change**: Create one minimal capability table covering JSON, proprietary BIN, GML/KML, DWG/DXF, STEP/STP, IGES/IGS, IFC, GLB, and Assimp fallback. Use concise columns for format, read, write, preview/fallback, and caveats where useful. Distinguish broad UI metadata from actual runtime dispatch, identify native-backed and experimental/fallback paths, and record verified limitations without inventing support.
- **Content**: Use `/unslop` with the crisp preset. Keep table entries terse; do not surround the table with generic framing or per-format essays.
- **Why**: A full support map is difficult to maintain, but a small factual table gives SDK users a usable contract without duplicating the implementation.
- **Validate**: Cross-check every table entry and caveat against current routing and adapter gates. Confirm the table is minimal, readable, and contains no private-repository references.

### Step A4 — document public SDK workflows and conversion semantics [critic, verify, commit] — completed

- **Files / symbols**:
  - new `docs/guides/0006-sdk-workflows.md`
  - public `GIMtransfer.FileAccess` methods
  - `JsonManager.ReadGimJson`
  - `GimProgressStatus`
  - local temp-output helpers and public JSON/GLB entrypoints
- **Change**: Provide minimal external-consumer workflows for reading/converting data, JSON/GLB operations, output handling, progress callbacks, errors, and lifecycle/concurrency expectations where verified. Clearly separate public SDK APIs from internal/friend-assembly methods. Call out observed behavior or limitations, such as temp-path semantics or scale behavior, rather than presenting assumptions as guarantees.
- **Content**: Use `/unslop` with the crisp preset. Include only examples that use public APIs; label any repository-test or UI-only material as non-SDK usage.
- **Why**: Existing examples primarily target TestUI/tests and may call APIs unavailable to package consumers.
- **Validate**: Confirm every example uses only public APIs and every return/output/error statement matches current signatures and implementation. Check links and code snippets for copy/paste correctness.

### Step A5 — move the GimData specification and document the test/fixture workflow [critic, verify, commit] — completed

- **Files / symbols**:
  - moved specification: `docs/specs/gimdata/README.md`
  - update local references to the moved specification
  - new `docs/guides/0007-test-and-fixture-runbook.md`
  - `GIMCompress/entities/GimData.cs`
  - `TestPlatform`, `TestHelpers`, `TestUI`, and CI configuration
- **Change**: Promote the existing GimData document to the stable `docs/specs/gimdata/` location without expanding it into a source mirror. Retain its useful rules and clarify, where needed, the indexed graph, root/object/element relationships, locals and transforms, builder/adapter usage, units, and mutation safety. Update local links rather than maintaining two copies. Separately document self-contained versus CAD-fixture tests, fixture location/acquisition constraints, manual TestUI checks, exact PowerShell commands, deterministic/parallel expectations, and the current CI non-gating/known-failure caveat.
- **Content**: Use `/unslop` with the crisp preset for edits to the moved specification and for the test runbook. Preserve technical rules and existing evidence; cut repetition instead of adding explanatory bulk.
- **Why**: GimData guidance already exists but is hidden beside implementation. Contributors also need an honest test path that does not imply unavailable fixtures or green CI guarantees.
- **Validate**: Confirm the move leaves no stale local links, commands and fixture paths match current files, and test/CI caveats are accurately stated. Confirm no private-repository names or proprietary binary details are introduced.

## Deferred follow-up candidates

- Public-only Doxygen configuration and XML API reference.
- Adapter extension specification covering support gates, progress ownership, dispatch seams, and native locking.
- Contribution guide, release/package publishing runbook, changelog, and security surface.
- Automated generation/checking of the format capability specification.

## Included pre-existing work

These files are intentional current work and must remain in the final PR:

- `.agents/skills/unslop/**`
- `skills-lock.json`

Do not revert, replace with a partial implementation, or omit them while executing this plan.

## Review

Run a fresh critic after each implementation step and one final wrap-up critic after A5 with the full modified file list and `Steps implemented: A1, A2, A3, A4, A5`. The wrap-up must check cross-document consistency, public/private boundary compliance, package/runtime claims, public API examples, format support, and proprietary-boundary compliance. If it finds actionable issues, add a corrective Z-step and run its full critic/verification/commit cycle. Final delivery remains one squashed PR.

### Step Z1 — resolve final documentation navigation, error-contract, and skill-package findings [critic, verify, commit] — completed

- **Files / symbols**:
  - `README.md`
  - `docs/guides/0006-sdk-workflows.md`
  - `docs/plans/0007-gim-106-gimtransfer-documentation.plan.md`
- **Change**: Add navigation links from the public README to the package, format, SDK, GimData, and test documentation. Qualify the `ReadGimDataFromFile` error guidance so it does not claim every invalid read throws when some binary-read paths return `null`. Leave the pre-existing unslop skill package untouched.
- **Content**: Use `/unslop` with the crisp preset for revised documentation prose. Keep all repairs minimal and factual; do not edit the externally supplied skill.
- **Why**: The final review found discoverability, error-contract, and package cleanliness issues that would otherwise leave the documentation set internally inconsistent or harder to use.
- **Validate**: Check every README link and re-check the error behavior against local implementation, then run the fresh critic and verification gate before committing.
