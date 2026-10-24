# AGENTS.md

## Scope
- This file governs the `GIMmos` repository root (multi-project CAD data orchestration).
- Important naming:
  - `GIMmos` (repo root): solution-level orchestration and shared dependencies.
  - `GIMmos/GIMtransfer` (subproject): core read/write/conversion engine.
- The canonical CAD data model is `GimData` (defined in `GIMCompress`).

## Do / Don't
- Do keep `GimData` structure valid and index-safe; use `docs/specs/gimdata/README.md` as the authoritative rule set.
- Do prefer changes in source projects over binary drop folders.
- Do keep guidance concise and local: add nested `AGENTS.md` only where local rules differ.
- Don't modify shipped binaries in wrapper folders (`GIMocctWrapper (DLL)`, `GIMtwin (DLL)`).
- Don't confuse repo-level paths with the core subproject paths.

## Key Entry Files
- `README.md`
- `docs/specs/gimdata/README.md`
- `GIMCompress/entities/GimData.cs`
- `GIMtransfer/FileManager/FileManager.cs`
- `GIMtransfer/FileManager/FileManagerInternal.cs`
- `GIMtransfer/FileManager/FileManagerPrivate.cs`
- `GIMtransfer/FileManager/FileTypes.cs`

## Build / Test Commands
- Repo restore: `dotnet restore`
- Core build: `dotnet build "GIMtransfer/GIMtransfer.csproj" -c Debug -p:Platform=x64`
- Core model build: `dotnet build "GIMCompress/GIMCompress.csproj" -c Debug -p:Platform=x64`
- Tests: `dotnet test "TestPlatform/TestPlatform.csproj" -c Debug -p:Platform=x64`
- UI harness (manual): open `TestUI/TestUI - Dll.sln` in Visual Studio, build `Debug|x64`.

## Dependencies / Interop Notes
- Runtime is .NET (`net10.0` / `net10.0-windows` projects).
- `GIMtransfer` prefers project references to external proprietary repos when present:
  - `..\..\GIMzip\GimBin\GimBin.csproj`
  - `..\..\GIMzip\GimCrypt\GimCrypt.csproj`
  otherwise falls back to nuget.org packages.
- OCCT-based STEP/IGES flows rely on `GIMocctWrapper (DLL)` binaries.

## Workspace Context
- Cross-repository architecture for the mega workspace lives in `WORKSPACE.md`.
- The multi-root workspace file is `GIMmos.code-workspace`.
- Use `WORKSPACE.md` when work spans `GIMmos`, `GIMzip`, `CoreWebApi`, `GIMfront`, `GIMmanager`, or `nasFlaskApp`.

## GIM agentic loop setup

Shared by Cursor and OpenCode:

- Open the multi-root workspace: [`GIMmos.code-workspace`](GIMmos.code-workspace) (not a single-repo folder alone).
- Copy [`.agents/agent-prefs.template.md`](.agents/agent-prefs.template.md) → `.agents/agent-prefs.md` (gitignored). Never commit the copied prefs file.
- Install GitHub CLI: [`.agents/skills/github-cli-workflow/scripts/install_gh.py`](.agents/skills/github-cli-workflow/scripts/install_gh.py) and [`.agents/skills/github-cli-workflow/SKILL.md`](.agents/skills/github-cli-workflow/SKILL.md).
- Install Atlassian acli: [`.agents/skills/atlassian-jira-cli/SKILL.md`](.agents/skills/atlassian-jira-cli/SKILL.md).
- Prefer [`/gim-orchestrate`](.agents/skills/gim-orchestrate/SKILL.md) for multi-step GIM work instead of manually appending skill bundles.
- Roster and docs:
  - Agent fleet glossary: [`.agents/CONTEXT.md`](.agents/CONTEXT.md) (map: [`CONTEXT-MAP.md`](CONTEXT-MAP.md))
  - ADRs: [`docs/adr/`](docs/adr/)
  - Plans: [`docs/plans/`](docs/plans/)
  - Guides: [`docs/guides/`](docs/guides/)
  - Canonical agents (dual-home): [`.cursor/agents/`](.cursor/agents/) (Cursor) and [`.opencode/agents/`](.opencode/agents/) (OpenCode); one-line pointers under [`.agents/agents/`](.agents/agents/); keep behavioral contracts in sync per each folder's `AGENTS.md`
  - Step brief prompt: [`.agents/prompts/orchestrator-step-brief.md`](.agents/prompts/orchestrator-step-brief.md) (not the session [`handoff`](.agents/skills/handoff/) skill)
- OpenCode first-time setup (humans / install): [`docs/guides/0000-setup-opencode.md`](docs/guides/0000-setup-opencode.md) — OpenCode ≥ 1.18.2, DeepSeek `/connect`, `OPENCODE_CONFIG`.

## Code search
- Pick the tool by what you know: CodeGraph for a known exact symbol (relations or call chain), **semble** for an unknown or guessed name, ast-grep for a known shape, **grep** for an exact string. **Semble is not optional when the symbol is unknown; do not use CodeGraph to guess a symbol.** Activate [`.agents/skills/workspace-code-search/SKILL.md`](.agents/skills/workspace-code-search/SKILL.md) when exploring by intent or searching sibling repos.
- From this repo root: `npm exec --no -- codegraph -- explore "<query>" --path <repo>` and `npm exec --no -- ast-grep -- run …`. The `--` right after the binary name is required. Without it, npm keeps the flags and the query stays in GIMmos. `uv run semble search "…" <target-repo-path>` is the first move for an unknown or guessed symbol.

