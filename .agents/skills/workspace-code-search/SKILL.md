---
name: workspace-code-search
description: Search the GIM mega workspace with CodeGraph, ast-grep, semble, and grep. Use when exploring by behavior, finding code without a known symbol, searching AGENTS.md or config, discovering related implementations, or searching CoreWebApi, GIMfront, GIMmanager, GIMzip, or sibling repos from the workspace. CodeGraph and ast-grep are pinned in GIMmos package.json. Semble requires uv run from GIMmos.
compatibility: Requires Node for the pinned CLIs (npm exec --no from GIMmos) and uv for semble (pyproject.toml). Run commands from the GIMmos repository root.
---

# Workspace code search

Use this skill for **cross-repo and exploratory search** in the GIM workspace. The explorer discovers. The builder and the critic do not use this ladder to find where work lives.

Workspace map: [`WORKSPACE.md`](../../../WORKSPACE.md). Semble is declared in [`pyproject.toml`](../../../pyproject.toml). CodeGraph and ast-grep are declared in [`package.json`](../../../package.json).

## Decision rule

Choose the tool by what you already know:

| You know | Tool |
|----------|------|
| The exact symbol, and want its relations or call chain | **CodeGraph** |
| You do **not** know the symbol and are guessing the name | **semble first** |
| A code shape | **ast-grep** |
| An exact string or path | **grep** |

- **Semble is not optional when the symbol is unknown.** Do not skip it.
- **Do not use CodeGraph to guess a symbol.** CodeGraph answers questions about a symbol you already know. If you are guessing the name, run semble first and use CodeGraph only after a hit names the symbol.

Never invert the first two rows.

## Non-negotiables

1. **Always** invoke semble as `uv run semble …` from the **`GIMmos` repository root** — not a global `semble` on `PATH`.
2. **Always** invoke CodeGraph and ast-grep from that same root as `npm exec --no -- codegraph -- …` and `npm exec --no -- ast-grep -- …`. The `--` right after the binary name is required. Without it, npm keeps later flags and a query aimed at another repo stays in GIMmos. Do not call a global `codegraph` or `sg`. The package binary is `ast-grep`.
3. Pass the **repo to search** as the path argument for semble, grep, and ast-grep. For `codegraph explore` and `codegraph impact`, pass another repo as `--path <repo>`. Omit `--path` when the target is GIMmos. `codegraph sync` takes the repo path positionally, still after `codegraph --`.
4. Keep `--top-k` modest (**5–8**) to limit tokens; semble returns ranked **chunks** (path + line range). Read full files only when the chunk is insufficient.
5. This skill does not sync or init CodeGraph, and it does not run `npm ci`. A missing `.codegraph/` falls through. If the pinned CLI is missing, stop and report that.
6. Indexed repos are GIMmos, GIMmanager, CoreWebApi, GIMfront, and GIMzip. Do not expect an index in GIMocctWrapper, nasFlaskApp, or GIMdeploy.

## When to use which tool

| Situation | Tool |
|-----------|------|
| Symbol or call chain you already know, and that repo is indexed | **`npm exec --no -- codegraph -- explore`** or **`codegraph -- impact`**, with `--path <repo>` when the repo is not GIMmos |
| Unknown symbol, guessed name, or behavior/domain words | **`uv run semble search`** |
| `AGENTS.md`, `TECH_DEBT.md`, ledgers, skills | **`uv run semble search … --content docs`** |
| yaml, toml, env-style config | **`uv run semble search … --content config`** |
| A known code shape | **`npm exec --no -- ast-grep -- run …`**. No rewrite from the explorer or the critic |
| Good hit; want siblings or nearby implementations | **`uv run semble find-related`** |
| Exact string, tests, stored-procedure files, every occurrence | **grep** |

**Grep** when the path or filename already gives you the needle and CodeGraph has no index (e.g. `IFCBooleanReader`, `UserControllerTests`, `sp_GetGimBinData`, `deploy_options`).

**Semble** when you would otherwise run several fuzzy greps (saves tokens).

### Known semble gaps (prefer grep)

- `*Tests.cs` — semble ranks production code above test projects.
- `GIM.Common/StoredProcedures/*.txt` — use grep for the procedure name; semble often surfaces C# call sites instead of the `.txt` export.
- Exhaustive literal counts and “is this string anywhere?”

### GIMzip

Text that reaches the parent, a plan, or a brief is the repo, the file, and the symbol. Leave method bodies in the local tool output. If the GIMzip checkout is absent, say the binary layer is the obfuscated DLL drop and stop. Do not describe on-disk layout.

## Workflow

1. Pick the tool from the Decision rule; the scenario table gives the exact command for each situation.
2. Use chunk `file_path` and line range before opening whole files.
3. Promising semble hit → `uv run semble find-related <file_path> <line> <repo-path>`.
4. Confirm exact strings and full occurrence lists with **grep**.

## Quick examples (from GIMmos root)

```bash
npm exec --no -- codegraph -- explore "jwt authentication login" --path ../CoreWebApi
uv run semble search "signalr desktop hub connection" ../GIMfront --top-k 5
uv run semble search "stored procedure ledger priority" ../CoreWebApi --content docs --top-k 5
npm exec --no -- ast-grep -- run --pattern 'console.log($A)' --lang js ../GIMfront
uv run semble find-related GIMtransfer/FileManager/FileManagerInternal.cs 162 .
```

## Fallback

If the uv project is unavailable: `uvx --from "semble[mcp]" semble search "…" <path>`. Prefer locked **`uv run`** in this workspace. There is no global-install fallback for CodeGraph or ast-grep.

## More reference

- Full command surface and flags: [references/commands.md](references/commands.md)
