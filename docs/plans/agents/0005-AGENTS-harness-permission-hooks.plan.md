---
name: harness-permission-hooks
overview: "Wire the gim-tools harness-config checklist into opencode AND Cursor: allow/deny rulesets, production opencode plugin (deny-only: deny-with-message + git '--' ask-stage reject, never auto-allows), production Cursor hook (deny-with-message), central AST command parser lib (pwsh ParseInput -> JSON tree; both hooks call it), per-harness hooks/plugins AGENTS.md docs, role-scoped gim-tools agent definitions (dual-home, 18 files), curated findings doc (docs/agents/0001-harness-permission-findings.md), harness-config status update, probe-artifact cleanup, GIM-132 tracker comment at wrap-up. Jira: no new ticket (.agents/.cursor meta; existing GIM-132 acknowledged via comment). Session overrides: online chained PRs (first PR = dev <- feature branch, docs-only); verify gate ON when testable (user tests opencode AND cursor flows separately)."
todos:
  - id: seed
    content: Plan 0004 + ADR 0007 — harness permission wiring decisions; PR dev <- feature/harness-permission-wiring (docs-only)
    status: completed
  - id: A1
    content: Central command-tree parser lib — pwsh System.Management.Automation.Language.Parser, JSON output tree + Pester tests
    status: completed
  - id: B1
    content: OpenCode production plugin — deny-only: deny-with-message (tool.execute.before) + git -- ask-stage reject/do-nothing (no auto-allow) + plugins AGENTS.md + delete perm-debug.js
    status: completed
  - id: B2
    content: OpenCode config rules — allows (gim-* 19, git read + git add* + .gitignore token, gh, Get-Help, Pester tier) + lean deny posture (hooks own message-carrying denies); opencode.local.json + template
    status: completed
  - id: C1
    content: Cursor production hook — deny-with-message via central lib + hooks.json rewiring + hooks AGENTS.md + probe cleanup (perm-debug.ps1, cli.json, debug logs)
    status: completed
  - id: D1
    content: Agent definitions dual-home — gim-tools skill load + role-scoped gim-* verbs (9 opencode + 9 cursor)
    status: completed
  - id: D2
    content: docs/agents/0001 curated findings + harness-config implemented-status update + .gitignore safety net + delete 5 root findings files
    status: completed
  - id: Z1
    content: Wrap-up fresh full-plan critic + fixes; mandatory full battery verify on both harnesses (incl. 0003 Z1 conversions re-run); GIM-132 tracker comment
    status: completed
  - id: S1
    content: gim-tools module auto-load — move module to scripts/GIMTools/ (folder==name), one-time setup.ps1 (User PSModulePath), setup guide, reference updates (SKILL.md import line, config templates, agent files, harness-config, tests)
    status: completed
isProject: false
---

# Plan 0004: Harness permission wiring — gim-tools allow/deny + hooks (opencode plugin, cursor hook)

> **Executor notice:** You did **not** participate in the design conversation. This document is the sole source of truth. Do not re-grill locked decisions. Do not invent new rules or mechanisms not listed here. If something is ambiguous *inside* this plan, ask the user once; otherwise implement as written.
>
> **Jira:** no new ticket — `.agents` / `.cursor` / harness meta per gim-orchestrate thresholds. **GIM-132** (previous toolkit work) is acknowledged with a gim-tracker comment at Z1 wrap-up.
>
> **Repo root:** `D:\Work\GIM\GIMtransfer` (Windows; runtime standard **pwsh 7** — never `powershell`).
>
> **Agent pwsh rule (user-locked):** agents NEVER invoke `pwsh` / `powershell`
> as a command — the harness shell already runs pwsh (opencode bash tool,
> Cursor terminal tool), so scripts run in-shell: `& <path>` / pipeline /
> import. Only the opencode plugin (JS, not an agent) spawns `pwsh -File` —
> the sole sanctioned external spawn. Config must NEVER gain a `pwsh*` /
> `powershell*` allow entry (would grant arbitrary execution).
>
> **Deny message contract (user-locked 2026-08-20):** every deny message must
> open with explicit framing so models never mistake it for command output:
> `Your command was not run: command '<cmd>' was denied by <opencode
> permission plugin | cursor permission hook>. Reason: <reason>. Please try:
> <alternative>. This is NOT the command's output — this is the permission
> hook's error message.` Command text truncated (~120 chars). Both harnesses
> also deny `pwsh` / `powershell` at COMMAND position (the shell already runs
> pwsh; scripts run in-shell) with the same contract.
>
> **Prefs overrides (user-locked for this session):** `verify_per_step` **ON when testable** (user tests the opencode AND cursor flows separately and reports back); `commit_per_step` **ON**; `pr_per_step` **ON — PR per step (online, chained)**; `critic_per_step` **granular**; `critic_reruns_per_step: one`; `architecture: windows`. Z-steps keep mandatory verify.
>
> **Working branch:** `feature/harness-permission-wiring` (from `dev`). The seed commit (plan + ADR 0007) lands directly on the feature branch (docs-only exception) and gets its own PR: **`dev <- feature/harness-permission-wiring`** even though it contains only documentation. Step branches: `harness-permission-wiring/<stepid>-<title>` chained leaf-safe, each PR → previous branch (A1 → `feature/harness-permission-wiring`). Normal merge commits, never squash.
>
> **ADR:** [`docs/adr/agents/0007-AGENTS-harness-permission-wiring.md`](../../adr/agents/0007-AGENTS-harness-permission-wiring.md) — lands with the seed commit.

## Overview

Wire the gim-tools harness-config checklist
(`.agents/skills/gim-tools/references/harness-config.md`) into **opencode** and
**Cursor**: allow/deny rulesets, a production opencode plugin (deny-only:
deny-with-message + git `--` ask-stage reject, never auto-allows), a production
Cursor hook (deny-with-message), per-harness `AGENTS.md` docs, role-scoped
gim-tools agent definitions, curated findings doc, and cleanup of all probe
artifacts.

Goal: agents run sanctioned commands without waiting on the user's allowlist;
denied constructs surface with the replacement channel, not a dead-end prompt.

## Environment facts

- Repo: `GIMtransfer` (primary home — workspace-hub / fleet meta work; single repo).
- Default branch: `dev`. Harness versions probed: opencode 1.18.18 (SDK 1.18.7),
  Cursor 3.16.29.
- Empirical mechanism facts (probe session, verified live — details in
  `docs/agents/0001`):
  - opencode: `tool.execute.before` fires before permission eval (throw =
    exact message, no UI); config last-match-wins; compound commands evaluated
    per segment (an `allow`/`always` reply approves the ENTIRE compound
    command → the plugin never auto-allows, ADR 0007 §1a); `permission.asked`
    + SDK reply is reject-only; `always` replies do NOT persist; plugins load
    at startup only; file-only logging (never `console.log` in TUI); use SDK
    `client`, raw `fetch` fails (Windows IPv6).
  - Cursor: `preToolUse` fires for every tool; `{permission:"deny"}` = silent
    block + custom message, beats allowlist, skips `beforeShellExecution`;
    hook `allow` does NOT hide the ask UI; one event per full command string;
    pwsh per-event spawn (~3 s); stdout must be JSON only; hook deny = only
    silent-deny layer (cli.json unused by IDE agent).
- **Central parser lib decision (user-locked):** command parsing is written
  ONCE as a pwsh lib using `System.Management.Automation.Language.Parser` /
  `ParseInput`, emitting a JSON tree (never comma-separated text — commas
  occur inside commands). Both hooks call it: Cursor hook dot-sources the lib
  in-process; opencode plugin spawns `pwsh -File` (lazy: cheap pre-filter +
  per-command cache, so not every bash call pays the spawn).
- Current state: `opencode.local.json` (gitignored) holds a broad allowlist
  incl. the five block-accepting cmdlets (must be removed — hook owns their
  deny); linter/tester per-agent bash blocks carry gim-* verbs; other agents
  rely on global rules; no agent definition loads gim-tools explicitly; probe
  files exist and are untracked: `.opencode/plugins/perm-debug.js`,
  `.cursor/hooks/perm-debug.ps1`, `.cursor/hooks.json` (probe wiring),
  `.cursor/cli.json`, `.cursor/debug-*.log`, and five findings files at repo root.

## Execution mode

- [x] Verify per step (ON when testable; user tests opencode + cursor flows separately)
- [x] Commit per step
- [x] PR per step (online, chained — leaf-safe branches per git-branching-workflow)
- [x] Critic per step (fresh gim-critic; one rerun)

## Failure scenarios

- Agent moves to step N+1 while a step's tests fail or the repo is broken.
- Agent moves to step N+1 without user verification when the step changed
  harness behavior (B1, B2, C1) — user must report opencode AND cursor results.
- Agent commits/PRs a step without verify first (verify precedes commit).
- Agent commits `opencode.json` or any never-commit path
  (harness-config "Never-commit paths"): `opencode.local.json` (gitignored —
  edit only, never stage), `start-opencode.ps1`, `.pr-body.md`, findings files.
- Agent leaves probe artifacts (`perm-debug.*`, `cli.json`, debug logs) staged.
- Agent enables raw `dotnet` / `powershell` / five cmdlets to "fix" a blocked call.
- Agent re-implements command parsing instead of calling the central lib.
- Dual-home sync missed: a change lands in `.opencode/` without the `.cursor/`
  twin (agent definitions, harness maps) or vice versa.
- PR chain base-branch mistakes (chained PRs target the previous step branch).

## References (attached — executing agents MUST read)

- `.agents/skills/gim-tools/references/harness-config.md` — the checklist this
  plan implements (allow list, deny list, hooks, file-write rules, quirks,
  replacements table, never-commit paths, ADR invariant map)
- `docs/plans/agents/0003-AGENTS-gim-toolkit.plan.md` §Z1 — 16 canonical conversions; re-run
  at Z1; every applicable conversion must complete without a permission prompt
- `docs/adr/agents/0003-AGENTS-gim-toolkit-skill-home.md`, `docs/adr/agents/0005-AGENTS-gim-toolkit-data-only-invariants.md`,
  `docs/adr/agents/0006-AGENTS-gim-run-execution-confinement.md` — source invariants
- `docs/adr/agents/0007-AGENTS-harness-permission-wiring.md` — this plan's decisions
- `.agents/skills/gim-tools/SKILL.md` — 19-verb vocabulary + import line
  (`Import-Module -Force ./.agents/skills/gim-tools/scripts/GIMTools.psd1 -DisableNameChecking`)
- Probe findings (deleted at D2; contents summarized in `docs/agents/0001`):
  `allowlist-permission-findings-cursor.md`, `allowlist-permission-findings-opencode.md`,
  `permission-hook-findings-cursor.md`, `permission-hook-findings-opencode.md`,
  `permission-hook-methodology-cursor.md`
- Cursor docs: configuration <https://cursor.com/docs/cli/reference/configuration.md>,
  permissions <https://cursor.com/docs/cli/reference/permissions.md>,
  hooks <https://cursor.com/docs/hooks.md>,
  terminal tool <https://cursor.com/docs/agent/tools/terminal.md>,
  hooks on Windows <https://forum.cursor.com/t/hooks-in-windows/150377>
- OpenCode docs: config <https://opencode.ai/docs/config/>,
  tools <https://opencode.ai/docs/tools/>,
  permissions <https://opencode.ai/docs/permissions/>,
  plugins <https://opencode.ai/docs/plugins/>
- PowerShell AST API: `System.Management.Automation.Language.Parser::ParseInput`
  (returns Ast + tokens + errors)
- `.opencode/harness-map.md`, `.cursor/rules/gim-harness-map.mdc` — harness
  tool maps (dual-home sync duty)
- `.opencode/agents/AGENTS.md`, `.cursor/agents/AGENTS.md` — dual-home sync contract
- `.agents/agent-prefs.md` — gates (session overrides above)

## Branch plan

- Feature branch: `feature/harness-permission-wiring` (from `dev`).
- Seed commit (plan + ADR 0007) lands on the feature branch; PR:
  **`dev <- feature/harness-permission-wiring`** (docs-only PR, opened even
  though it contains only documentation).
- Step branches (chained PRs, leaf-safe):
  `harness-permission-wiring/a1-command-tree-lib`, `harness-permission-wiring/b1-opencode-plugin`,
  `harness-permission-wiring/b2-opencode-config`, `harness-permission-wiring/c1-cursor-hook`,
  `harness-permission-wiring/d1-agent-defs`, `harness-permission-wiring/d2-docs-cleanup`,
  `harness-permission-wiring/z1-wrapup`.
- PR base: each step PR targets the **previous step's branch** (A1 →
  `feature/harness-permission-wiring`), per 0003 precedent and
  git-branching-workflow chained PRs. Normal merge commits, never squash.

---

### Step seed — plan + ADR 0007 [commit, PR]

- **Files / symbols**: `docs/plans/agents/0005-AGENTS-harness-permission-hooks.plan.md`,
  `docs/adr/agents/0007-AGENTS-harness-permission-wiring.md`
- **Change**: Plan and ADR committed directly on
  `feature/harness-permission-wiring` (docs-only exception); PR opened
  `dev <- feature/harness-permission-wiring`.
- **Validate**: n/a (docs-only).
- **Commit message**: `seed: plan 0004 harness permission wiring + ADR 0007`

---

### Step A1 — central command-tree parser lib [critic, verify, commit, PR]

- **Files / symbols**:
  - `.agents/skills/gim-tools/scripts/parse-command-tree.ps1` (NEW) — central lib
  - `.agents/skills/gim-tools/tests/parse-command-tree.tests.ps1` (NEW, Pester)
- **Change**: One parser, written once — no per-harness twins. pwsh script
  that uses `[System.Management.Automation.Language.Parser]::ParseInput` to
  parse a PowerShell command string and emit a **JSON tree** (never
  comma-separated text — commas occur inside commands, so a delimited format
  would break). Dual use:
  - run standalone (`pwsh -NoProfile -File parse-command-tree.ps1`): reads the
    command from stdin (or `-Command` param), writes the JSON tree to stdout
    (JSON only — callers must not see other output)
  - dot-sourceable: defines `Get-GimCommandTree` for in-process callers
    (cursor hook)
  - JSON tree shape (documented in the script header):
    ```json
    {
      "ok": true,
      "parseErrors": ["..."] ,
      "hasSubstitution": false,
      "substitutions": ["$("],
      "staticCalls": ["::"],
      "commands": [
        {
          "name": "git",
          "args": ["diff", "--", ".gitignore", "A", "B"],
          "tokens": [{"text": "...", "kind": "command|string|number|operator|...", "position": 0}],
          "text": "git diff -- .gitignore A B",
          "depth": 0,
          "pipelineStage": 0,
          "dashDash": {"index": 2, "firstFileAfter": ".gitignore"}
        }
      ]
    }
    ```
    `dashDash` present only for `git` commands with a standalone `--` token
    (exact token `--`, not `--all`/`--oneline`, not inside strings);
    `firstFileAfter` = token after `--` or null. `hasSubstitution` /
    `staticCalls` report `$(` and `::` occurrences outside string literals.
    Structural source: walk the Ast (CommandAst, PipelineAst, StatementBlockAst,
    script blocks, parentheses) — quote handling, `''` / backtick escapes, and
    string-vs-command classification all come from the PowerShell parser
    itself, not hand-rolled regex.
- **Validate**: `Invoke-Pester .agents/skills/gim-tools/tests/parse-command-tree.tests.ps1`
  (pwsh 7, Pester 6). Cases must cover: quoted `--` in messages, `--oneline`
  vs `--`, `git commit -m "added '--' detection"`, chains `;`/`&&`/`||`,
  nested `{ }` scriptblocks, strings containing `ForEach-Object` (classified
  string, not command), `$()` inside vs outside strings, `::` inside vs
  outside strings, pipeline stage numbering, malformed input → `ok:false` +
  parseErrors.
- **Commit message**: `A1: central command-tree parser lib (pwsh AST -> JSON tree)`

---

### Step B1 — opencode production plugin [critic, verify, commit, PR]

- **Files / symbols**:
  - `.opencode/plugins/lib/gim-permissions-rules.js` (NEW) — pure decision
    predicates over the central-lib JSON tree (unit-testable with fixture trees)
  - `.opencode/plugins/gim-permissions.js` (NEW) — thin plugin wiring
  - `.opencode/plugins/AGENTS.md` (NEW) — plugin docs (content contract below)
  - `.opencode/plugins/tests/gim-permissions-rules.test.mjs` (NEW, node:assert)
  - delete `.opencode/plugins/perm-debug.js` (probe artifact)
- **Change**: Production plugin (opencode 1.18.x API, SDK `client` for replies,
  file-only logging to `%TEMP%\opencode\gim-permissions.log`, NEVER
  `console.log` in TUI). Calls the central lib by spawning
  `pwsh -NoProfile -File .agents/skills/gim-tools/scripts/parse-command-tree.ps1`
  (path resolved from `project.directory`). **Lazy invocation**: cheap
  pre-filter regex before any spawn; per-command cache (before-stage and
  asked-stage share the tree). Never re-implement parsing locally.
  - **Deny-with-message** via `tool.execute.before` throw (message names the
    replacement, discovery-at-failure) — evaluated over the central tree,
    command-candidate positions only (strings stay inert):
    - five block-accepting cmdlets: `ForEach-Object` → `gim-report --prefix`,
      `Where-Object` → `gim-where --<predicate>`, `Group-Object` →
      `gim-count --by`, `Sort-Object` → `gim-report --sort`, `Select-Object` →
      `gim-head -n N` / `gim-json --path`
    - `$(` outside strings → `--<param>-from-var` channel
    - `::` / `$ExecutionContext` / `.GetType(` / `.Assembly` / `GetMethod` /
      `GetField` / `.Invoke(` (the .NET hole — opencode AST is blind to it);
      member-access detection works on real parser output (central lib
      `memberCalls` list — member-access identifiers outside strings/comments,
      incl. bare instance invocations that emit no CommandAst)
    - secret-file paths (harness-config hooks §1): `*.secret.json`,
      `appsettings.*.secret.json`, `.env`, `*.user`, `*.pubxml.user`,
      `**/id_rsa`, `**/*.pfx`, `**/*.snk` — in bash command text AND file-tool
      paths (edit/write tools); the trusted-read pathspec exclusion applies to
      git READ subcommands only (`status`/`log`/`diff`/`blame`/`show`) — a
      write-side `git add -- <secret>` is still denied
    - destructive git variants: `git branch -D`, `git branch -d`,
      `git add -A`, `git add -f`, `git add --all`, `git add --force`,
      `git stash drop`, `git stash apply`,
      `git stash clear` (message: two-turn pattern / version-controller)
    - **force-push variants only** (NOT plain `git push` — version-controller
      pushes regularly): `git push --force`, `git push -f`,
      `git push --force-with-lease` (message: force push requires explicit
      user consent; use version-controller)
    - `git -C` (message: use working_directory / cd)
    - dotnet execution verbs (harness-config deny list: nuget push/add source,
      tool install -g, new install, workload install, `<path>.dll`/exec,
      fsi/script, `run`, msbuild `-t:`, publish `-o`) — message: use `gim-run`
    - execution escapes: `Invoke-Expression` / `iex` / `Add-Type` /
      `Start-Process` (no sanctioned replacement — use gim-* verbs / `gim-run`)
  - **git `--` quirk — before-stage throw with hint (user-locked escalation)**:
    - `.gitignore`-first commands never ask (config allowlists `git <safe-cmd>`
      + `.gitignore` token → silent run) → before-stage does NOT throw → the
      plugin has nothing to do.
    - Non-`.gitignore`-first git pathspec commands (first file after
      standalone `--` is NOT `.gitignore`, incl. `--` with no file after) →
      **`tool.execute.before` THROW** with the sacrificial-token hint: "git
      pathspec commands must use `.gitignore` as the first file after `--`;
      re-run `git <cmd> -- .gitignore <files>` or route through
      version-controller". Escalated per user decision (2026-08-20): the SDK
      `reject` reply carries no custom text, and the hint is required for
      discovery-at-failure.
    - `.gitignore`-first-but-still-asks edge (command not fully allowlisted,
      e.g. pre-B2 config) → before-stage does NOT throw; `permission.asked`
      handler does **nothing** (ask continues to the UI — never auto-allow).
      The ask-stage reject path remains as a safety net for any ask that
      slips past before-stage.
    - Documented in `.opencode/plugins/AGENTS.md`; live-verify that the
      message reaches the executor (B1 verify item 7 now expects a message).
  - **No auto-allow anywhere (user-locked, ADR 0007 §1a):** the plugin NEVER
    replies `once`/`always` to `permission.asked`. A reply approves the whole
    compound command while opencode evaluates per segment — `allow; Dangerous`
    would run its dangerous part. Allowing requires every part to be safe
    (hard); denying requires one offending part (easy). The config allowlist
    is the whitelist (compound commands only run when EVERY segment matches);
    the plugin is deny-only. No condition table, no whitelist conditions
    (the earlier Pester `-Path @(...)` idea is dropped — the documented
    fallback `Invoke-Pester -Path '<dir>' -PassThru` stays).
  - **AGENTS.md content contract** (both harness docs, phrased ends-over-means):
    purpose; architecture (config allowlist = the whitelist — compound
    commands run only when every segment matches; plugin = deny-only —
    before-stage throws + ask-stage reject, never auto-allows, ADR 0007 §1a);
    how to add a rule; best practices (structural matching over substring,
    central lib only — never local parsing, file-only logs, JSON-only stdout,
    restart required for opencode plugins, ~3 s pwsh spawn per cursor event);
    **dual-sync rule: mirror intent on the other harness unless a
    harness-specific quirk — document the exception**; the opencode↔cursor
    mechanism differences table; per-harness quirks (git `--`, hook allow
    no-op vs ask UI, always-reply non-persistence); Cursor Auto-run allowlist
    maintenance checklist (see C1).
- **Validate**: `node --check` on all three JS files; `node
  .opencode/plugins/tests/gim-permissions-rules.test.mjs` (rules battery with
  fixture trees + one integration test invoking the real central lib via
  pwsh: denies fire with replacement messages, git -- reject/do-nothing,
  string false positives stay inert, and a compound-command case proving no
  auto-allow path exists). **User verify (opencode TUI)** after restart:
  plugin loads, allowed commands run silent, denied commands surface messages,
  `git diff -- .gitignore <file>` runs silent, `git diff -- <file>` asks then
  is rejected.
- **Commit message**: `B1: opencode permission plugin (deny-only, git -- ask-stage)`

---

### Step B2 — opencode config rules [critic, verify, commit, PR]

- **Files / symbols**:
  - `opencode.local.json` (gitignored — EDIT only, never commit)
  - `opencode.local.template.jsonc` (committed — becomes the team-standard ruleset)
- **Change**: Apply harness-config allow/deny lists (decision: all tiers
  allowed — G3 locked). **Lean deny posture (user-locked): commands with a
  sanctioned alternative are NOT config-denied — the hook denies them with a
  helpful message; config deny list stays free of indiscriminate
  message-less entries** (`"*": "ask"` default remains the catch-all):
  - ALLOW additions/changes: `.gitignore*` (sacrificial token), `git add*`
    (**general form** — a `git add --*` pattern still sees the first file as
    a separate command; general `git add*` works naturally; hook denies
    `git add -A` / `-f`), `git blame*`, `Get-Help*`, `Test-NetConnection*`,
    `gh issue view*`, Pester test tier wildcards (`Import-Module Pester*`,
    `Invoke-Pester*`, `Should*`, `InModuleScope*` — exact-word rules never
    fire; wildcard required); external-dir additions:
    `C:/Users/*/Documents/PowerShell/Modules/Pester/**` (user-scope install)
  - REMOVE from allowlist (hook now owns the deny with message): the five
    block-accepting cmdlets (`ForEach-Object`, `Where-Object`, `Group-Object`,
    `Sort-Object`, `Select-Object`) — they fall back to default ask if the
    hook misses them; `Invoke-Expression`/`iex`/`Add-Type`/`Start-Process`
    are NOT added as allows (never were) and are hook-denied
  - REMOVE deny entries whose message the hook now carries: `git -C*` (hook
    message: use working_directory)
  - **git push: NOT denied** (version-controller pushes regularly) — force
    variants (`git push --force*`, `git push -f*`,
    `git push --force-with-lease*`) are hook-denied with the consent message
  - Existing gim-* 19 verbs, git read forms, acli, gh, dotnet build/test,
    Import-Module gim-tools lines stay as-is (audit them against the checklist)
  - Template gains the full standard ruleset (gim-* tiers, git forms
    incl. `git add*` + `.gitignore*`, gh/acli/dotnet read+test, Pester tier)
    with machine-specific paths marked as comments (`external_directory` stays
    local-only); template comments note "message-carrying denies live in the
    plugin/hooks — do not add them here"
- **Validate**: config parses (opencode loads; JSONC check); rule audit list
  vs harness-config. **User verify (opencode TUI)**: gim-* verbs run silent;
  `git diff -- .gitignore <file>` silent; `git add <file>` silent; five
  cmdlets / `$()` / `::` / force-push / `git -C` denied with plugin message;
  Pester wildcards work.
- **Commit message**: `B2: opencode permission rules (gim-tools tiers, git forms, lean deny posture)`

---

### Step C1 — cursor production hook [critic, verify, commit, PR]

- **Files / symbols**:
  - `.cursor/hooks/gim-permissions.ps1` (NEW) — production hook
  - `.cursor/hooks.json` (rewire to production script; keep preToolUse +
    beforeShellExecution; post-events may log or stay silent)
  - `.cursor/hooks/AGENTS.md` (NEW) — docs (content contract per B1) + Cursor
    Auto-run allowlist checklist (user-maintained UI list: git read forms +
    git add, gh, acli, Get-Content, Write-Output, gim-* 19 verbs, .gitignore,
    Pester verbs, Get-Help, Test-NetConnection… — prefix-model warning: use
    scoped forms; **do NOT add the five block cmdlets** — hook denies them)
  - `.cursor/hooks/tests/gim-permissions.tests.ps1` (NEW — simulated stdin
    JSON payloads → assert decision JSON)
  - delete `.cursor/hooks/perm-debug.ps1`, `.cursor/cli.json`,
    `.cursor/debug-*.log` (probe artifacts)
- **Change**: Production hook: dot-sources the central lib
  (`.agents/skills/gim-tools/scripts/parse-command-tree.ps1` → in-process
  `Get-GimCommandTree` — no second spawn), stdin JSON payload handling
  (payload shapes per findings), output `{}` silent or
  `{permission:deny, agent_message, user_message}`:
  - deny-with-message — same rule set as the opencode plugin's before-stage
    denies (same spirit, same messages): five cmdlets (command-candidate
    positions via central tree), `$()`, `::`/reflection, secret-file paths
    (bash text + Edit/Write tool file paths), destructive git variants,
    **force-push variants only** (plain `git push` NOT denied — allowlist
    covers it), `git -C` (case-sensitive), dotnet execution verbs
  - **NO git `--` rule** — Cursor has no such quirk (`git diff -- file`
    passes fine); this is the documented harness-specific exception
  - **NO whitelist** — Cursor's mean is allowlist + complement deny; the
    Auto-run checklist is the allow side
  - JSON-only stdout; file-only log `%TEMP%\cursor\gim-permissions.log`;
    catch-all keeps hook silent on error
- **Validate**: `Invoke-Pester .cursor/hooks/tests/gim-permissions.tests.ps1`
  (payload battery incl. allowlisted-command deny beats allowlist; strings
  with cmdlet names stay inert). **User verify (Cursor live)**: hook deny
  fires with message on `ForEach-Object` / force-push / `git -C`; Auto-run
  list added per checklist runs silent; `git diff -- file` passes (no quirk
  rule — do NOT add the .gitignore rule).
- **Commit message**: `C1: cursor permission hook (deny-with-message via central lib) + probe cleanup`

---

### Step D1 — agent definitions dual-home [critic, verify, commit, PR]

- **Files / symbols**: `.opencode/agents/{gim-orchestrator,gim-explorer,gim-builder,gim-linter,gim-tester,gim-critic,gim-version-controller,gim-tracker,gim-debugger}.md` + `.cursor/agents/<same 9>.md`
- **Change**: dual-home sync (both harnesses, same PR):
  - Mandatory skill load added to every agent: `.agents/skills/gim-tools/SKILL.md`
    (load the suite, use it — not discover at runtime)
  - OpenCode per-agent bash blocks gain role-scoped gim-* verbs (G6 table
    locked):
    - all: read+state tiers (gim-list, gim-where, gim-grep, gim-inspect,
      gim-count, gim-diff, gim-head, gim-report, gim-json, gim-asm, gim-verbs,
      gim-set, gim-get)
    - orchestrator: + fix, write, apply, commit (may write — behavioral rule:
      must delegate; "can, shouldn't")
    - builder: + fix, write, apply, run, probe (quick smoke)
    - linter: + fix, write, apply
    - tester: + run, probe (full suite)
    - critic: + run, probe (verify code works)
    - version-controller: + commit
    - tracker: (none extra)
    - debugger: + fix, write, apply, commit, run, probe (does it all itself)
  - Cursor agents: skill load only (Auto-run is global; checklist in
    `.cursor/hooks/AGENTS.md`)
- **Validate**: grep audit — every agent file loads gim-tools; verb sets match
  the table. **User verify**: spot-check one agent per harness in the UI.
- **Commit message**: `D1: agent definitions load gim-tools with role-scoped verbs`

---

### Step D2 — curated findings doc + harness-config status + cleanup [critic, verify, commit, PR]

- **Files / symbols**:
  - `docs/agents/0001-harness-permission-findings.md` (NEW — curated,
    version-stamped: harness versions probed, payload shapes (`permission.asked`
    fields, cursor preToolUse/beforeShellExecution/postToolUseFailure shapes),
    hook event ordering, mechanism differences table, ends-over-means framing,
    git `--` quirk + ask-stage design, replacement table,
    re-verify-after-upgrade note)
  - `.agents/skills/gim-tools/references/harness-config.md` (update — mark
    implemented status per harness; where each rule lives; hooks section gains
    the production plugin/hook + central lib; git `--` quirk marked wired
    (ask-stage reject); **git push entry revised** — plain push not denied,
    force variants hook-denied; **git add form revised** — `git add*` general
    allow (not `--` scoped) on opencode; lean-deny-posture note — hooks own
    message-carrying denies)
  - `.gitignore` (add `allowlist-permission-findings-*.md`,
    `permission-hook-findings-*.md`, `permission-hook-methodology-*.md`,
    `.cursor/debug-*.log` — safety net)
  - delete the five root findings files
- **Validate**: link check (docs/agents/0001 referenced from ADR 0007 +
  harness-config); root files gone; gitignore entries present.
- **Commit message**: `D2: curated permission findings doc + harness-config status update`

---

### Step Z1 — wrap-up critic + full verify (mandatory) [critic, verify, commit, PR]

- **Files / symbols**: all files touched by A1–D2 (fixes only)
- **Change**: Fresh full-plan gim-critic (`Steps implemented: A1, B1, B2, C1, D1, D2`).
  Actionable findings → fix in this step. `critic_reruns: one` — one re-critic
  if the user asks. **gim-tracker**: leave a comment on **GIM-132**
  acknowledging the prior toolkit work and noting this follow-up
  (harness permission wiring, plan 0004) landed.
- **Validate** (mandatory verify — user tests BOTH harnesses):
  1. opencode TUI: plugin loaded; full battery — gim-* verbs silent,
     denies message, `git diff -- .gitignore <f>` silent / `git diff -- <f>`
     asks then rejected, `git add <f>` silent, Pester suite runs, five
     cmdlets denied w/ replacements, force-push denied w/ consent message.
  2. Cursor: Auto-run allowlist per checklist; hook denies fire with messages;
     `git diff -- file` passes (no quirk rule); plain `git push` not blocked
     (VC flow).
  3. Re-run 0003 Z1 canonical conversions — every applicable conversion
     completes without a permission prompt.
  4. Unit suites: central lib Pester, plugin rules node tests, hook payload
     tests.
- **Commit message**: `Z1: wrap-up fixes from final critic`

---

### Step S1 — gim-tools module auto-load (PSModulePath setup + guide) [critic, verify, commit, PR]

- **Files / symbols**:
  - `scripts/GIMTools.psd1` + `scripts/GIMTools.psm1` → **MOVE** to `scripts/GIMTools/`
    (PowerShell auto-load requires folder name == module name; audit the psm1
    for `$PSScriptRoot`-relative sibling references — `parse-command-tree.ps1`
    / `roots.json` stay in `scripts/`, so adjust to `..\` where needed)
  - `scripts/setup.ps1` (NEW) — one-time User-level PSModulePath registration:
    `$ScriptsDir = $PSScriptRoot` (the `scripts/` folder — parent of the
    `GIMTools/` module folder); split the User PSModulePath on `;` and add
    `$ScriptsDir` only if not already an exact entry; persist via
    `[Environment]::SetEnvironmentVariable(..., "User")` (HKCU\Environment, no
    admin) + update the session env; then verify
    `Test-Path "$ScriptsDir\GIMTools\GIMTools.psd1"` — if the entry was already
    present but the module file is missing (stale entry after a repo move),
    emit a warning naming the stale path. No admin, user-scope only.
  - `docs/guides/0002-setup-gim-tools-autoload.md` (NEW) — setup guide: run
    `scripts/setup.ps1` once per machine; restart opencode/Cursor so harness
    pwsh processes inherit the new env; **trust note** (User PSModulePath
    auto-loads GIMTools in every pwsh for the user — only run on machines
    where you trust the checkout; the toolkit dir stays agent-deny-write);
    troubleshooting (stale entries, moved repo, module not resolving).
  - Reference updates (all import-path references to the old
    `scripts/GIMTools.psd1` location):
    - `.agents/skills/gim-tools/SKILL.md` import line → new path
    - `opencode.local.json` + `opencode.local.template.jsonc` — import-line
      allows (new path forms; `Import-Module GIMTools*` may replace them)
    - `.opencode/agents/gim-linter.md` — the two import-line allows from D1
    - `.agents/skills/gim-tools/references/harness-config.md` — module layout
      + auto-load section (PSModulePath registration; import no longer
      required per shell)
    - `docs/agents/0001-harness-permission-findings.md` — import-line refs
    - gim-tools Pester test files that import the module by path
- **Change**: With the module at `scripts/GIMTools/GIMTools.psd1` and
  `scripts/` in the User PSModulePath, PowerShell auto-loads gim-* verbs in
  every fresh pwsh — agents stop needing the per-command import line (each
  harness bash call is a fresh pwsh; import state never persisted). The
  allowlist still gates every gim-* verb (auto-load does NOT bypass
  permissions). setup.ps1 is the only machine change; everything else in-repo.
- **Validate**: after user runs `scripts/setup.ps1` once + restarts both
  harnesses: fresh pwsh runs `gim-list .` with NO import line (both
  harnesses); `Get-Module -ListAvailable GIMTools` resolves; Pester suites
  green with updated import paths (hook 115+, lib 52, gim-tools verb suite).
- **Commit message**: `S1: gim-tools module auto-load via PSModulePath setup`

---

## Wrap-up

- After Z1: mark all steps complete; fresh full critic if findings remain
  (one rerun per prefs); PR chain closed per git-branching-workflow
  (merge commits, no squash on chained PRs); GIM-132 comment via gim-tracker.
- Post-merge hygiene: `opencode.local.json` on this machine carries the live
  ruleset; other machines copy `opencode.local.template.jsonc`; Cursor users
  apply the Auto-run checklist from `.cursor/hooks/AGENTS.md`.
