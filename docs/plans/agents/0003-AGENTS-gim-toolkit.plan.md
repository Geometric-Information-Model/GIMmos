---
name: gim-toolkit
overview: "Build the Phase 1 gim-* PowerShell toolkit (19 data-only verbs) as an agent skill at .agents/skills/gim-tools/ in GIMtransfer: spine (gim-list/gim-where/gim-report), search (gim-grep), inspection (gim-inspect/gim-head/gim-count/gim-diff/gim-json), capability (gim-asm), state (gim-set/gim-get), mutating (gim-fix/gim-write/gim-apply/gim-commit), execution (gim-run/gim-probe), meta (gim-verbs + docs), config checklist (allow/deny/hooks recipe). Pester 6 tests (optional dep; setup guide docs/guides/0001-setup-powershell-7.md). No harness config changes (user wires allowlist later; checklist produced in A9 at references/harness-config.md). Jira GIM-132."
todos:
  - id: A1
    content: Spine — skill scaffold (module, helpers, roots.json, Pester harness, .gitignore) + gim-list + gim-where + gim-report
    status: completed
  - id: A2
    content: Search — gim-grep (--capture, --stdin, file:line default)
    status: completed
  - id: A3
    content: Inspection — gim-inspect, gim-head, gim-count, gim-diff, gim-json
    status: completed
  - id: P1
    content: Runtime pivot — pwsh 7 + Pester 6: convert A1-A3 test suites to Pester 6 syntax, amend AGENTS ADR 0003, add setup guide (docs/guides/0001), update plan env facts. No module code changes.
    status: completed
  - id: A4
    content: Capability — gim-asm (System.Reflection.Metadata)
    status: completed
  - id: A5
    content: State — gim-set/gim-get + variable store + --from-var convention
    status: completed
  - id: A6
    content: Mutating — gim-fix, gim-write, gim-apply, gim-commit
    status: completed
  - id: A7
    content: Execution — gim-run (roots confinement, timeout, env scrub, audit) + gim-probe
    status: completed
  - id: A8
    content: Meta — gim-verbs (--json/--gen-refs) + final SKILL.md + references/ docs
    status: completed
  - id: A9
    content: Harness configuration checklist — allow list, deny list, required hooks (incl. "X denied, use gim-* instead" discovery hooks), file-write rules, per-harness mapping. Not applied; user wires it later.
    status: completed
  - id: Z1
    content: Full smoke — 16 canonical conversions run as written (mandatory verify report)
    status: completed
  - id: Z2
    content: Wrap-up fresh full-plan critic + fixes if any
    status: completed
isProject: false
---

# gim-* Toolkit — Agent Command Vocabulary (Phase 1)

> **Executor notice:** You did **not** participate in the design conversation. This document is the sole source of truth. Do not re-grill locked decisions. Do not invent new verbs or flags not listed here. If something is ambiguous *inside* this plan, ask the user once; otherwise implement as written.
>
> **Jira:** [GIM-132](https://gim.atlassian.net/browse/GIM-132) — created before step A1; branch names embed the key.
>
> **Repo root:** `D:\Work\GIM\GIMtransfer` (Windows; runtime standard **pwsh 7.6.5+** — Core/.NET 10; the harness shell and the test suite run on pwsh 7). Module code stays **PowerShell 5.1-compatible** (portable — no pwsh-only features in `scripts/`). Tests run under **Pester 6.1.0** (optional dependency, see setup guide [`docs/guides/0001-setup-powershell-7.md`](../../guides/0001-setup-powershell-7.md)).
>
> **Prefs overrides (user-locked):** `verify_per_step` **OFF** for A- and P-steps (push through without user verification); `commit_per_step` **ON**; `pr_per_step` **ON — PR per step (chained)**; `critic_per_step` **granular**; `critic_reruns_per_step: one`; `architecture: windows`. Z-steps keep mandatory verify per orchestration protocol — executed as a non-blocking summary report (user override: continue unless stopped).
>
> **Working branch:** `feature/gim-132-gim-toolkit` (from `dev`). Step branches: `gim-132-gim-toolkit/<stepid>-<title>` chained leaf-safe, each PR → previous branch (A1 → `feature/gim-132-gim-toolkit`). Normal merge commits, never squash. Plan/ADRs commit on the feature branch (docs-only exception); implementation only via step branches.
>
> **ADRs:** [`docs/adr/agents/0003-AGENTS-gim-toolkit-skill-home.md`](../../adr/agents/0003-AGENTS-gim-toolkit-skill-home.md) · [`0005-AGENTS-gim-toolkit-data-only-invariants.md`](../../adr/agents/0005-AGENTS-gim-toolkit-data-only-invariants.md) · [`0006-AGENTS-gim-run-execution-confinement.md`](../../adr/agents/0006-AGENTS-gim-run-execution-confinement.md) — landed with A1.
>
> **Official agent skills spec (reference):** <https://agentskills.io/specification.md> — the skill at `.agents/skills/gim-tools/` must conform: frontmatter `name` matches directory name (`gim-tools`), `description` what+when+keywords, sanctioned subdirs (`scripts/`, `references/`), SKILL.md body ≤500 lines with progressive disclosure into `references/` files, relative-path links from the skill root.

---

## Layout (locked)

```
.agents/skills/gim-tools/
  SKILL.md                     frontmatter name: gim-tools (must match dir); body ≤500 lines:
                               verb list one-liners + canonical pipelines + references pointers
  scripts/
    GIMTools.psd1              module manifest (module version 0.1.0)
    GIMTools.psm1              imports helpers, dot-sources verbs/, exports gim-* functions
    roots.json                 roots list for containment (see below)
    private/                   shared helpers (no exports)
      GimPaths.ps1             Resolve-ContainedPath (real path, symlink resolution, roots check),
                               env-var expansion for roots entries
      GimOutput.ps1            JSON emission, bounded-output caps + truncation marker,
                               Write-GimError (stderr, non-zero exit)
      GimVar.ps1               variable-store read/write/expiry (A5; minimal read helper usable earlier)
      GimQuote.ps1             Quote-Arg for argv assembly (no shell interpolation)
    verbs/                     one file per verb, dot-sourced by psm1
      gim-list.ps1 gim-where.ps1 gim-report.ps1 gim-grep.ps1 gim-inspect.ps1
      gim-head.ps1 gim-count.ps1 gim-diff.ps1 gim-json.ps1 gim-asm.ps1
      gim-set.ps1 gim-get.ps1 gim-fix.ps1 gim-write.ps1 gim-apply.ps1
      gim-commit.ps1 gim-run.ps1 gim-probe.ps1 gim-verbs.ps1
  references/                   per-group docs (generated by gim-verbs --gen-refs, hand-tuned):
                               spine.md search.md inspection.md capability.md state.md
                               mutating.md execution.md meta.md harness-config.md
  tests/                       Pester 6 tests (Invoke-Pester -Path <file> -PassThru; optional dep)
```

`roots.json` defaults (relative entries resolved against GIMtransfer root; `%VAR%` env expansion allowed): `.`, `../GIMmanager`, `../CoreWebApi`, `../GIMocctWrapper`, `../GIMzip`, `../GIMfront`, plus read-extra root `%USERPROFILE%\.nuget\packages` (for `gim-asm` only). **Read verbs** confine to the full roots list; **write and execution verbs** confine to current repo + `.agent-scratch/` only. `.agent-scratch/` is created in-repo and gitignored (add to `.gitignore` in A1).

## Design invariants (non-negotiable, every verb)

1. **Data-only arguments.** No parameter accepts code, a script block, a command name, or a format string reaching an evaluator. Predicates are named flags (`--missing`, `--empty`, `--matches <re>` — a regex is data).
2. **No `Invoke-Expression`, `iex`, `Add-Type`, `Start-Process`, or `&$var`** anywhere in the implementation. Process launch via `[System.Diagnostics.Process]` only.
3. **Bounded output.** Caps on result count (default 500) and bytes (default 1 MiB) with an explicit `--truncated` marker; `--max-hits`/`-n` style flags where the spec names them.
4. **`--json` on demand** on every verb (structured output for between-turn branching).
5. **No shell interpolation.** Agent-supplied text becomes argv via `Quote-Arg` (double-quote + escape embedded quotes/backslashes) into `ProcessStartInfo.Arguments`; never `cmd /c`, never string concatenation into a shell.
6. **Stored values are data, never argv.** `gim-get` writes stdout only; consuming parameters use `--<param>-from-var <name>`; every `--from-var` value is containment-revalidated at use.
7. **Variable names:** `^[A-Za-z0-9_-]{1,64}$`, reject anything else — no normalisation.
8. **Every mutating verb supports `--dry-run`** (and `SupportsShouldProcess` where natural).
9. **Missing input paths** → `gim-inspect` reports `kind=missing` (no error); other verbs error unless they document `--allow-missing`-style handling.
10. **PowerShell 5.1 compatible** throughout; UTF-8 without BOM is the default write encoding (`[System.IO.File]::WriteAllText` with `New-Object System.Text.UTF8Encoding($false)`).

## Execution mode

- [ ] Verify per step (user override OFF; Z-steps report-only)
- [x] Commit per step
- [x] PR per step (chained)
- [x] Critic per step (granular: per-step + wrap-up)

## Failure scenarios

- (commit-per-step ON) Agent moves to step N+1 without committing step N.
- (commit-per-step ON) Agent commits step N while the touched area is broken for its documented checks.
- (PR-per-step ON) Agent moves to step N+1 without opening the PR for step N.
- (critic-per-step ON) Agent moves to step N+1 without running a fresh gim-critic on step N.
- (critic-per-step ON) Agent reuses a critic session (critic is always fresh).
- Order of operations broken: critic before tester, PR before critic, commit before linter/tester pass.
- Agent implements verbs/flags not listed in this plan, or renames a locked verb.
- Agent touches harness config files (`.opencode/`, `.cursor/`, `.claude/`, `opencode*.json*`) — out of scope, user does this later.

---

### Step A1 — spine: scaffold + gim-list/gim-where/gim-report [critic, commit, PR]

- **Files / symbols**: `.agents/skills/gim-tools/` scaffold per Layout (SKILL.md skeleton with frontmatter `name: gim-tools` + description + placeholder verb list), `scripts/GIMTools.psd1`, `scripts/GIMTools.psm1`, `scripts/roots.json`, `scripts/private/{GimPaths,GimOutput,GimQuote}.ps1` (+ minimal `GimVar.ps1` read/write helper for later use), `scripts/verbs/{gim-list,gim-where,gim-report}.ps1`, `tests/gim-spine.tests.ps1` + `tests/gim-helpers.tests.ps1`, `.gitignore` (add `.agent-scratch/`), plus ADRs 0004/0005/0006 and this plan file (docs land on the feature branch; code lands on the step branch).
- **Change**: Module imports cleanly with `-DisableNameChecking`. Helpers: `Resolve-ContainedPath` (real-path resolve, symlink resolution, roots assertion, env expansion), bounded-output caps, `Quote-Arg`. Verbs:
  - `gim-list <path...> [--from-file <f>] [--glob <p>]… [--under <d>] [--depth N] [--dirs-only] [--allow-missing] [--print0] [--json]` — sole path entry point; containment against roots list; globs repeat; unresolvable path errors unless `--allow-missing`.
  - `gim-where [--exists|--missing|--empty|--non-empty|--matches <re>|--not-matches <re>|--larger-than <n>|--smaller-than <n>|--newer-than <iso>|--older-than <iso>|--ext <e>|--is-file|--is-dir] [--field <name>] [--json]` — pipeline filter; predicates apply to the item (path) or a named record field (works downstream of `gim-grep`).
  - `gim-report [--prefix <label>] [--prefix-each] [--sort <field>] [--format lines|table|json] [--only-failures] [--json]` — shapes/aggregates a stream; `--sort` replaces `Sort-Object` (denied), `--prefix` replaces label loops.
- **Validate**: `Import-Module -Force .agents/skills/gim-tools/scripts/GIMTools.psd1 -DisableNameChecking`; `Invoke-Pester -Script .agents/skills/gim-tools/tests/gim-spine.tests.ps1 -PassThru` (containment rejections incl. `..\` escapes, glob, `--allow-missing`, each `gim-where` predicate, report formats/sort); manual smoke: `gim-list --glob "docs/adr/*.md" | gim-where --is-file | gim-report --prefix ADR`.
- **Commit message**: `A1: spine verbs (gim-list/gim-where/gim-report) and module scaffold`

### Step A2 — search: gim-grep [critic, commit, PR]

- **Files / symbols**: `scripts/verbs/gim-grep.ps1`, `tests/gim-grep.tests.ps1`, SKILL.md skeleton verb list gains gim-grep.
- **Change**: `gim-grep --pattern <re> [--capture N] [--stdin] [--fixed] [--ignore-case] [--files-only] [--files-without-match] [--count] [--context N] [--max-hits N] [--json]`. Input = paths (pipeline or positional) or, with `--stdin`, a piped text stream. **Default output `file:line: text`** (kills the format-loop idiom). `--capture N` emits the capture group instead of the line (derived key, still data). `--files-without-match` = "which files lack the pattern". Regex via `[regex]`; `--fixed` escapes. Bounded by `--max-hits`.
- **Validate**: Pester (default shape, capture, stdin over `Get-Content` pipe, files-without-match, count, context, fixed, ignore-case, max-hits, json); smoke conversions: `git diff -- A.cs B.cs C.cs | gim-grep --stdin --pattern "^[+-](.*Identity comes from the JWT claim.*)$" --capture 1 | gim-count` (needs A1 verbs), `gim-list --glob "GIM.WebAPI/**/Controllers/*.cs" | gim-grep --pattern "Route\(|AllowAnonymous|Authorize"`, `gim-list --glob ".agents/**/*.md" | gim-grep --pattern "\]\(([^)#][^)]*)\)" --capture 1 | gim-where --missing | gim-report --prefix BROKEN_REF`.
- **Commit message**: `A2: gim-grep with --capture/--stdin and file:line: text default`

### Step A3 — inspection: gim-inspect/gim-head/gim-count/gim-diff/gim-json [critic, commit, PR]

- **Files / symbols**: `scripts/verbs/{gim-inspect,gim-head,gim-count,gim-diff,gim-json}.ps1`, `tests/gim-inspection.tests.ps1`.
- **Change**:
  - `gim-inspect [--eof-newline] [--tabs] [--line-endings] [--lines] [--bytes] [--encoding] [--bom] [--trailing-ws] [--max-line-len] [--binary] [--kind] [--mtime] [--hash] [--json]` — byte-level checks with no built-in equivalent; missing path reports `kind=missing`, never errors. `--hash` emits file hashes (SHA256).
  - `gim-head -n N [--skip N] [--last] [--json]` — replaces `Select-Object -First/-Skip/-Last` (denied); `--last` subsumes tail.
  - `gim-count [--by <field>] [--json]` — replaces `Group-Object { }` + count loops; `--by` groups on a record field.
  - `gim-diff --against <path-list|glob> | --against-var <name> [--json]` — two-set comparison with no intermediate variables; `--against-var` reads the variable store (helper from A1; store convention from ADR 0005).
  - `gim-json --path <query> [--validate] [--keys] [--json]` — JSON documents only in this build (native `ConvertFrom-Json`); YAML/TOML input yields a clear `unsupported format: <ext>` error naming the limitation; `--path` is a dotted/bracket query (`a.b[0].c`), `--keys` lists keys at the queried node.
- **Validate**: Pester per verb (inspect flags on crafted temp files incl. BOM/CRLF/trailing-ws/missing; head skip/last; count by field; diff against glob + against-var roundtrip; json path/keys/validate); smoke conversion 11: `gim-list --glob ".cursor/agents/**" | gim-diff --against ".opencode/agents/**"`.
- **Commit message**: `A3: inspection verbs (gim-inspect/gim-head/gim-count/gim-diff/gim-json)`

### Step P1 — runtime pivot: pwsh 7 + Pester 6 (amend A1–A3 artifacts) [critic, commit, PR]

- **Files / symbols**: `tests/gim-spine.tests.ps1`, `tests/gim-helpers.tests.ps1`, `tests/gim-grep.tests.ps1`, `tests/gim-inspection.tests.ps1` (+ `tests/gim-test-common.ps1` shared repo-root helper, Pester 3.4 → 6 syntax conversion); `docs/adr/agents/0003-AGENTS-gim-toolkit-skill-home.md` (amended — runtime pwsh 7, tests Pester 6 optional); `docs/guides/0001-setup-powershell-7.md` (new — pwsh 7 required via official GitHub MSI, Pester 6 recommended-but-optional); this plan file's env-facts/overview/layout/A4/A9/Z1 wording (orchestrator-authored, rides in this commit). Note: the user's `.opencode/agents/gim-tester.md` allowlist cleanup rides along in this commit per the user's standing instruction (tester modifications → next available PR).
- **Change**: Runtime standard is **pwsh 7.6.5+** (harness + test suite; `System.Reflection.Metadata` in-box → A4 unchanged in mechanism). Pester **6.1.0** installed on the dev machine (optional dependency for users — only needed to run the suite; documented in guide 0001). Convert all existing tests (145 in 4 suites as of A3) from Pester 3.4 positional syntax (`Should Be`) to Pester 6 syntax (`Should -Be`, `-Not` switch, `Should -Contain` for collections, `Should -Throw`, `-ExpectedMessage` semantics per installed 6.x). Export assertions must be robust to the working tree's A4-WIP module state (assert the 9 committed A1-A3 verbs exported + the not-yet-built set not exported; leave `gim-asm` unasserted — A4 owns that assertion). **No module/verb code changes** — `scripts/` stays PowerShell 5.1-compatible and untouched. Amend AGENTS ADR 0003 (runtime + optional Pester). Add setup guide. Fix plan env facts (already applied by orchestrator; keep in sync).
- **Validate**: `Import-Module -Force .agents/skills/gim-tools/scripts/GIMTools.psd1 -DisableNameChecking`; `Invoke-Pester -Path @('.agents/skills/gim-tools/tests/gim-spine.tests.ps1','.agents/skills/gim-tools/tests/gim-helpers.tests.ps1','.agents/skills/gim-tools/tests/gim-grep.tests.ps1','.agents/skills/gim-tools/tests/gim-inspection.tests.ps1') -PassThru` → all green (expect ≥145) under Pester 6; linter on touched files; `Get-Module -ListAvailable Pester | Select -First 1 Version` → 6.x.
- **Commit message**: `P1: pwsh 7 runtime pivot — Pester 6 suite, AGENTS ADR 0003, setup guide`

### Step A4 — capability: gim-asm [critic, commit, PR]

- **Files / symbols**: `scripts/verbs/gim-asm.ps1`, `tests/gim-asm.tests.ps1`.
- **Change**: `gim-asm types --package <id>/<ver> [--matches <re>] | --path <dll> [--json]`. Enumerate exported type full names via **`System.Reflection.PortableExecutable.PEReader` + `System.Reflection.Metadata.MetadataReader`** (in-box on pwsh 7 / .NET 10 shared framework — `System.Reflection.Metadata` ships with the .NET runtime; PEReader byte-parses the target file and never loads it into the session). **Never** `Assembly.LoadFrom`/`ReflectionOnlyLoadFrom` of the target. `--package` resolves under `%USERPROFILE%\.nuget\packages` (read-extra root in roots.json), picks best-TFM `lib/` DLL(s); `--matches` filters.
- **Validate**: Pester — `--path` against any built/available DLL (e.g. an existing `obj`/`bin` output or a NuGet-cached assembly present on the machine; if none, test the PE-read path on `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll`); assert type list non-empty and `--matches` filters; assert no load happens (module stays loaded in session). Manual: `gim-asm types --package microsoft.entityframeworkcore/7.0.20 --matches "Options|IDbContextFactory"` (if that package is cached; else any cached package).
- **Commit message**: `A4: gim-asm metadata type enumeration`

### Step A5 — state: gim-set/gim-get [critic, commit, PR]

- **Files / symbols**: `scripts/verbs/{gim-set,gim-get}.ps1`, `scripts/private/GimVar.ps1` (full implementation), `tests/gim-state.tests.ps1`.
- **Change**: Store at `%LOCALAPPDATA%\gim\vars\<sanitized-repo-root>\<name>.val` (sanitized root = repo root path, `\`→`_`, drive colon stripped). One file per variable, raw bytes (no envelope), temp-write + rename for atomicity; append-only `_audit.log`. Name regex `^[A-Za-z0-9_-]{1,64}$`, reject otherwise. `gim-set <name> [--clear <name>] [--clear-all] [--ttl <sec>] [--json]` **tees** (stores stream AND passes through); opportunistically sweeps expired vars in the namespace. `gim-get <name> [--max-age <sec>] [--json]` — head-only pipeline source; TTL (default 1h from last write, never refreshed on read) checked **on read**: expired → non-zero exit, `expired (age Nm, ttl Nm)` to stderr, never returns stale value; age always printed to stderr. Caps: 1 MiB per variable, 10 MiB namespace. `--from-var` consumption convention lands as documented in ADR 0005 (existing consuming flags from A3; A6/A7 add theirs).
- **Validate**: Pester — set/get roundtrip incl. multi-line, tee passthrough mid-pipeline, TTL expiry via `--ttl 1` + sleep, `--max-age` demand, name regex rejection (`../../evil`), atomicity (concurrent writes leave no torn file), sweep, caps.
- **Commit message**: `A5: gim-set/gim-get variable store with TTL`

### Step A6 — mutating: gim-fix/gim-write/gim-apply/gim-commit [critic, commit, PR]

- **Files / symbols**: `scripts/verbs/{gim-fix,gim-write,gim-apply,gim-commit}.ps1`, `tests/gim-mutating.tests.ps1`.
- **Change**: Write verbs confine to current repo + `.agent-scratch/`. All support `--dry-run`.
  - `gim-fix <eof-newline|line-endings|trailing-ws> [--to lf|crlf] [--dry-run] [--json]` — pairs with `gim-inspect` flags; fixes in place, reports per-file before/after.
  - `gim-write --path <p> --from-stdin [--create-only] [--encoding utf8|utf8bom|utf16] [--out-dir <d>] [--path-from-var <name>] [--dry-run]` — default encoding **UTF-8 without BOM** (PS 5.1 `>`/`Out-File` emit UTF-16 — this flag exists because of that); `--out-dir` defaults to `.agent-scratch/`; `--create-only` refuses overwrite.
  - `gim-apply --create-empty | --touch | --remove [--dry-run] [--json]` — fixed action vocabulary over a filtered pipeline set.
  - `gim-commit --subject <s> [--body <b>]… [--trailer <t>]… [--body-file <f>] [--dry-run]` — builds argv via `Quote-Arg` and invokes `git` directly (no shell, no here-strings); `--dry-run` prints the argv (test hook); repeat `--body` for paragraphs.
- **Validate**: Pester on temp fixtures inside `.agent-scratch/` — fix eof-newline/line-endings/trailing-ws byte-verified, dry-run leaves files untouched, write encodings byte-checked (no BOM default), create-only refusal, apply create/touch/remove, commit `--dry-run` argv contains subject/body/trailer unquoted-correctly (no shell interpretation). Never run a real commit in tests.
- **Commit message**: `A6: mutating verbs (gim-fix/gim-write/gim-apply/gim-commit)`

### Step A7 — execution: gim-run/gim-probe [critic, commit, PR]

- **Files / symbols**: `scripts/verbs/{gim-run,gim-probe}.ps1`, `scripts/private/GimExec.ps1` (shared process launch: argv, scrub, timeout, kill, audit), `tests/gim-execution.tests.ps1`.
- **Change**:
  - `gim-run <build|test|run|publish|ef|format> [--project <p>] [--project-from-var <name>] [--timeout <sec>] [--dry-run] [-- <args>...]` — resolve `--project` (or cwd) → absolute → assert containment under **current repo + `.agent-scratch/`** (exec roots, narrower than read roots); reject `$env:TEMP`, UNC, junction/symlink escapes; argv via `Quote-Arg`; env scrub (strip `NUGET_API_KEY`, `GH_TOKEN`, `GITHUB_TOKEN`, `AWS_*`, `AZURE_*`); hard timeout default 300s, cap 1800s, kill process tree on expiry (`WaitForExit(ms)`, then `taskkill /PID <pid> /T /F` — never `Stop-Process` on the tree alone); append audit line (timestamp, cwd, verb, project, args, exit code) to `%LOCALAPPDATA%\gim\run-audit.log`; stream stdout/stderr; exit code propagated.
  - `gim-probe --project <p> --route <r> [--method <m>] [--body-file <f>] [--timeout <sec>]` — launch app on a random local port (`--urls http://127.0.0.1:<port>` appended for dotnet run), poll readiness (HTTP GET until non-error or timeout), issue request, print status + body, always teardown (kill tree). Reduces the agent's reason to author scratch projects.
- **Validate**: Pester — argv dry-run for each verb; confinement rejections (`..\..\`, `%TEMP%`, UNC-ish); scrub list applied in dry-run output; timeout path with a sleep command. Smoke in Z1 with real build/probe; here only `gim-run test --project TestPlatform/TestPlatform.csproj --dry-run` (no execution in unit tests).
- **Commit message**: `A7: gim-run and gim-probe execution tier`

### Step A8 — meta: gim-verbs + final docs [critic, commit, PR]

- **Files / symbols**: `scripts/verbs/gim-verbs.ps1`, `tests/gim-verbs.tests.ps1`, final `SKILL.md`, `references/*.md` (the 8 group files: spine/search/inspection/capability/state/mutating/execution/meta; `harness-config.md` lands in A9).
- **Change**: `gim-verbs --json` emits the full vocabulary (name, group, signature, params, one-line description) from the verb-definition table; `gim-verbs --gen-refs` writes `references/<group>.md` per group with per-verb flag detail. Final `SKILL.md` (≤500 lines): frontmatter `name: gim-tools` + description, import line (`Import-Module -Force .agents/skills/gim-tools/scripts/GIMTools.psd1 -DisableNameChecking`), verb list one-liners, canonical pipelines (§Z1's conversion list), pointers to `references/`. **Do not apply any harness config.**
- **Validate**: `gim-verbs --json | ConvertFrom-Json` parses and lists 19 verbs; `--gen-refs` idempotent (run twice, diff clean); SKILL.md < 500 lines; frontmatter name matches dir; every verb has a reference entry.
- **Commit message**: `A8: gim-verbs generator and final skill documentation`

### Step A9 — harness configuration checklist (allow / deny / hooks) [critic, commit, PR]

- **Files / symbols**: `references/harness-config.md` (hand-written, the checklist), one-line pointer in `SKILL.md` (references section).
- **Change**: Produce the complete configuration recipe the user applies later (this step **ships no config and applies nothing** — it is the checklist). Contents:
  - **Allow list**: read tier `gim-list:* gim-where:* gim-grep:* gim-inspect:* gim-count:* gim-diff:* gim-head:* gim-report:* gim-json:* gim-asm:* gim-verbs:* Get-Help:*`; state tier `gim-set:* gim-get:*`; write tier (decide individually) `gim-fix:* gim-write:* gim-apply:* gim-commit:*`; execution tier (decide individually) `gim-run:* gim-probe:*`; raw data-only built-ins worth allowlisting (`git status/log/diff/blame/show/branch/stash list/add`, `gh pr view/diff/list`, `gh issue view`, `Get-Content:*`, `Test-NetConnection:*`, `Write-Output:*`); **test tier (optional, suite-only)** — Pester 6: `Import-Module Pester*`, `Invoke-Pester*`, `Should*`, `InModuleScope*`, plus external-dir allow for `C:/Program Files/PowerShell/Modules/Pester/**` and `%USERPROFILE%/Documents/PowerShell/Modules/Pester/**` (user-scope install per guide 0001).
  - **Deny list**: the five block-accepting cmdlets (`ForEach-Object`, `Where-Object`, `Group-Object`, `Sort-Object`, `Select-Object`) — script-block bodies are the `[Type]::Method()` execution hole (ADR 0005); `Invoke-Expression`, `iex`, `Add-Type`, `Start-Process`, `&$var`; `$( )` command substitution into allowlisted verb arguments; execution verbs: `dotnet nuget push`, `dotnet nuget add source`, `dotnet tool install -g`, `dotnet new install`, `dotnet workload install`, `dotnet <path>.dll` / `dotnet exec`, `dotnet fsi` / `dotnet script`, `dotnet msbuild -t:<arbitrary>`, `dotnet publish -o <outside repo>`, `git push` (agent-side; version-controller owns pushes).
  - **Hooks**: (1) **secret-file deny** — deny reads of `*.secret.json`, `appsettings.*.secret.json`, `.env`, `*.user`, `*.pubxml.user`, `**/id_rsa`, `**/*.pfx`, `**/*.snk` (covers every access path, not just gim-*); (2) **`::` / reflection deny** — deny `::` bare, `$ExecutionContext`, `.GetType(`, `.Assembly`, `GetMethod`, `GetField`, `.Invoke(` (a speed bump against the .NET hole, not a boundary); (3) **discovery-at-failure hooks** — denial text must name the replacement, e.g. denying `Select-String | ForEach-Object { }` says *"use `gim-grep --capture N`"*; denying `Select-Object -ExpandProperty` says *"use `gim-json --path`"*; this is the channel that reaches an agent which never read the skill. File-write rules: `allow <repo>/**`, `deny <repo>/.agents/skills/gim-tools/**` (AGENTS ADR 0003 / `docs/adr/agents/0003-AGENTS-gim-toolkit-skill-home.md` — the toolkit is the one place a single approved write converts every allowlisted verb into arbitrary execution), `ask` everything else. Per-harness mapping table (opencode / Cursor / Claude Code) with matching-power notes (`*`/`?` last-match-wins AST-aware on opencode, prefix-only on Cursor, prefix-style on Claude Code — verify each on the live harness).
  - Also record: trailing-space quirk (`"<cmd>*"` preferred over `"<cmd> *"`), glued-range workaround (`@(1,2,3)` / spaced `1 .. 2`), the five denied cmdlets' replacements table (`gim-where`, `gim-count`, `gim-head`, `gim-report --sort`, `gim-json`).
- **Validate**: manual review of the checklist against ADRs 0004–0006 (every invariant has a config-side counterpart); SKILL.md pointer present.
- **Commit message**: `A9: harness configuration checklist (allow/deny/hooks)`

### Step Z1 — full smoke: canonical conversions [critic, commit, PR]

- **Files / symbols**: none new (fixes only if a conversion fails); `tests/` may gain regression tests for anything broken.
- **Change**: Run every canonical conversion below as written; failures = bugs, fix in this step. Conversions:
  1. `git diff -- A.cs B.cs C.cs | gim-grep --stdin --pattern "^[+-](.*Identity comes from the JWT claim.*)$" --capture 1 | gim-count` (paths illustrative — run against any three tracked files with a shared phrase)
  2. `Write-Output "== .gitignore =="; Get-Content .gitignore; Write-Output "== .git/info/exclude =="; Get-Content .git/info/exclude`
  3. `gim-list --glob "GIM.WebAPI/**/Controllers/*.cs" | gim-grep --pattern "Route\(|AllowAnonymous|Authorize|EnableRateLimiting"` (adjust glob to a real tree)
  4. `Get-Content "GIMtransfer\FileManager\FileManager.cs" | gim-head -n 60 --skip 100`; then `gim-list --glob "tests/**/*.cs" | gim-grep --pattern "gim|0003|plan"`
  5. `gim-list --glob "GIM.Common/Respositories/*.cs" --glob "GIM.Common/DBConnections/*.cs" --glob "GIM.WebAPI/**/Controllers/**/*.cs" --allow-missing | gim-grep --pattern "GetConnectionString|ConnectionStrings:" | gim-head -n 25` (misspelled globs prove `--allow-missing`)
  6. `gh pr list --repo Geometric-Information-Model/GIMfront --json number,title,state,isDraft,baseRefName,headRefName,author,createdAt,mergedAt,mergeable,mergeStateStatus`
  7. `gh pr diff 40 --repo Geometric-Information-Model/GIMfront | gim-write --path gimfront-pr40.diff` (a few PRs; then `gim-list --glob ".agent-scratch/gimfront-pr*.diff" | gim-inspect --bytes`)
  8. Two-turn stash: `git stash list` → (if index 0 matches) `git stash drop "stash@{0}"` — read-only here; do not actually drop anything.
  9. `Test-NetConnection -ComputerName 3.121.190.108 -Port 3306 -InformationLevel Quiet` (external host; optional — skip if network policy blocks)
  10. `gim-asm types --package microsoft.entityframeworkcore/7.0.20 --matches "Options|IDbContextFactory|DbContextOptionsBuilder"` (or any cached package)
  11. `gim-list --glob ".cursor/agents/**" | gim-diff --against ".opencode/agents/**"`
  12. `gim-list --glob ".agents/**/*.md" | gim-grep --pattern "\]\(([^)#][^)]*)\)" --capture 1 | gim-where --missing | gim-report --prefix BROKEN_REF`
  13. `gim-list --glob ".agents/skills/*/SKILL.md" | gim-grep --files-without-match --pattern "^name:" | gim-report --prefix MISSING_NAME`
  14. `gim-list --glob "src/**/*.cs" | gim-inspect --hash | gim-set baseline; gim-run build --project <real project>; gim-list --glob "src/**/*.cs" | gim-inspect --hash | gim-diff --against-var baseline` (build may be slow — use the smallest real project)
  15. `gim-commit --dry-run --subject "Z1: sample" --body "First paragraph." --body "Second paragraph." --trailer "Co-authored-by: Cursor <cursoragent@cursor.com>"` (dry-run only)
  16. `gim-run build --project GIMtransfer/GIMtransfer.csproj` + `gim-probe --project <a runnable project> --route /health` (if no runnable HTTP project exists, document the skip; probe against any route that exists)
- **Validate**: all conversions that apply complete without a permission prompt and without manual intervention; full Pester suite green under **Pester 6 on pwsh 7** (runs after P1). **Mandatory verify (Z-step)**: present the smoke summary (passed/skipped/failed) — user override: continue unless stopped.
- **Commit message**: `Z1: full smoke of canonical conversions`

**Z1 results (recorded at wrap-up):** 16 conversions run — **14 PASS / 2 SKIP** (conv 9 external host unreachable — environment; conv 16 probe no HTTP project in repo — documented per plan; conv 16 build failed on **pre-existing** DWGReader source errors, out of scope). Bugs found by smoke + fixed with regression tests: gim-head gained text-stream mode for piped content (conv 4/5), gim-write `--from-stdin` optional when content is piped (conv 7), gim-diff accepts `path: ...` record-form lines incl. `--against-var` (conv 14). Suite: 292 passed / 0 failed (9 suites, Pester 6.1.0, pwsh 7.6.5). Verify gate skipped by user override.

### Step Z2 — wrap-up critic [critic, commit, PR]

- **Files / symbols**: any touched by fixes.
- **Change**: Fresh full-plan gim-critic (all steps A1–Z1, all files, plan path). Actionable findings → fix in this step; `until_clean` not active (reruns: one) — apply fixes, one re-critic only if the user asks. Report disposition ledger. **Mandatory verify (Z-step)**: present disposition summary — continue unless stopped.
- **Commit message**: `Z2: wrap-up critic fixes`

---

## Review

Invoke `/review-with-critic granular` per the plan's gating: fresh gim-critic after **every** step (passing `Steps implemented: <stepId>` and that step's files), plus one final wrap-up pass after Z1 with the **full** modified file list and `Steps implemented: A1, A2, A3, P1, A4..Z1`. Wrap-up pass weights findings toward cross-step coherence.

## Canonical outputs / acceptance

- 19 verbs exportable from `GIMTools.psd1` (`Get-Command gim-* -Module GIMTools` → 19).
- Pester suite green on pwsh 7 (Pester 6 syntax; Pester is an optional dependency per guide 0001).
- No allowlisted-command transcript contains `{`, `if`, `foreach`, or `$var =` for toolkit work.
- The variable store never feeds argv: grep the module for `$(gim-get` and interpolation of stored values into command lines — both must return nothing.
- 16 conversions in Z1 run as written (minus documented skips).

## Never stage/commit

`.agents/agent-prefs.md`, `opencode.local.json`/`.jsonc`, `start-opencode.ps1`, `.pr-body.md`, `allowlist-permission-findings.md`, `allowlist-permission-findings-cursor.md` (user keeps untracked; do not touch, do not reference), anything under `D:\Work\GIM\PRs`.
