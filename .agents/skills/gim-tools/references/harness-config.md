# gim-tools reference: harness configuration checklist (allow / deny / hooks)

Hand-written checklist (plan step A9). Every entry below is a *decision*;
**implementation status is marked per harness below** (plan 0004 / AGENTS ADR 0007
wired the opencode plugin + config rules and the Cursor hook). Read tier is
safe by construction; write / execution / test tiers are labeled **decide
individually**.

Source invariants: AGENTS ADR 0003 (skill home), AGENTS ADR 0005 (data-only), AGENTS ADR 0006
(execution confinement), AGENTS ADR 0007 (harness wiring), AGENTS ADR 0008 (fleet
verbs). The ADR invariant map at the bottom cross-checks every invariant against
its config-side counterpart.
Empirical probe record (version stamps, payload shapes, event ordering,
quirks): `docs/agents/0001-harness-permission-findings.md`.

## How to use this checklist

1. Apply per harness — matching power differs per harness (table below);
   verify each rule live on the harness you are wiring.
2. Decide the "decide individually" tiers (write, execution, test) before
   applying; do not blanket-allow them.
3. After wiring, re-run the Z1 canonical conversions
   (`docs/plans/agents/0003-AGENTS-gim-toolkit.plan.md` §Z1); every conversion that applies
   must complete without a permission prompt.

## Module layout and auto-load (plan 0004 S1)

The gim-tools module exports 25 verbs across nine groups and lives at `scripts/GIMTools/GIMTools.psd1` + `.psm1`
(**module folder name == module name** — the PowerShell auto-load requirement).
The psm1 dot-sources `private/` + `verbs/` and reads `roots.json` from the
parent `scripts/` folder (they did not move with the module).

Auto-load: `scripts/setup.ps1` (run once per machine, user-scope, no admin)
registers the `scripts/` folder in the **User PSModulePath**
(HKCU\Environment). After the harnesses restart, every fresh pwsh auto-loads
the gim-* verbs — the per-command import line is **no longer required** (each
harness bash call is a fresh pwsh; import state never persisted). Setup guide:
`docs/guides/0002-setup-gim-tools-autoload.md`.

**Auto-load does NOT bypass permissions.** The config allowlist still gates
every gim-* verb exactly as before; the plugin/hook deny rules are unchanged.
The import-line allows below remain for explicit loads (`Import-Module -Force
./.agents/skills/gim-tools/scripts/GIMTools/GIMTools.psd1 ...`, both slash
forms, plus the `Import-Module GIMTools*` name form).

## Implementation status (plan 0004 / AGENTS ADR 0007 — wired)

| Harness | Status | Where each rule lives |
| --- | --- | --- |
| opencode | **wired** | plugin `.opencode/plugins/gim-permissions.js` + decision predicates in `lib/gim-permissions-rules.js` (deny-with-message, git `--` before-stage throw + ask-stage reject — see Hooks + Git `--` sections); config rules in `opencode.local.json` (machine-local, gitignored) with the team copy in `opencode.local.template.jsonc` — allows live there, **no message-carrying denies in config** (lean deny posture, ADR 0007 §5) |
| Cursor | **wired** | hook `.cursor/hooks/gim-permissions.ps1` (wired via `.cursor/hooks.json` for `preToolUse` + `beforeShellExecution`); silent-allow layer = user Auto-run allowlist per the checklist in `.cursor/hooks/AGENTS.md`; `.cursor/cli.json` unused by the IDE agent |
| Claude Code | **not wired** | apply the allow/deny lists below if used (verify live); no hook/plugin exists there |

## Allow list

Read tier (safe by construction — every verb is data-only, AGENTS ADR 0005):

```
gim-list:*  gim-where:*  gim-grep:*  gim-inspect:*  gim-count:*  gim-diff:*
gim-head:*  gim-report:*  gim-json:*  gim-asm:*  gim-verbs:*  Get-Help:*
```

State tier:

```
gim-set:*  gim-get:*
```

Write tier (**decide individually**; all support `--dry-run`, AGENTS ADR 0005 invariant 8):

```
gim-fix:*  gim-write:*  gim-apply:*
```

`gim-fix` and `gim-write` stay repo-only. `gim-apply` alone may also target
files under the real OS temp root (`%TEMP%`, resolved once with
`[System.IO.Path]::GetTempPath()` and real-path resolution); paths outside the
repo and that temp root are still rejected. The temp root itself must pass a
safety floor: a rooted local path (not UNC), not a drive root, at least two
segments below the drive root, and not equal to or an ancestor of the user
profile, local app data, app data, Windows directory, system root, or the repo
root.

Execution tier (**decide individually**; raw `dotnet` stays denied — only the
contained wrapper verbs are allowed, AGENTS ADR 0006):

```
gim-run:*  gim-probe:*
```

Fleet tier (**seven role-scoped verbs, never global**). The version-controller
frontmatter owns network read plus ff-only pull, explicit
stage/commit/push/PR/stack, and the wrapup fetch/reply pass; the tracker
frontmatter owns Jira reads and mutations:

```
# version-controller frontmatter
gim-preflight:*
gim-pr:*
gim-wrapup-gather:*
gim-wrapup-respond:*

# tracker frontmatter
gim-jira-search:*
gim-jira-create:*
gim-jira-transition:*
```

Keep these entries **only** in the owning agent frontmatter
(`.opencode/agents/gim-version-controller.md` or
`.opencode/agents/gim-tracker.md`), never in the global OpenCode template. The
Cursor Auto-run checklist (`.cursor/hooks/AGENTS.md`) lists five of the seven
fleet verbs (`gim-wrapup-gather`, `gim-wrapup-respond`, `gim-jira-search`,
`gim-jira-create`, `gim-jira-transition`) and `gh stack view`. `gim-preflight`
and `gim-pr` stay off that checklist and may prompt once for approval. Cursor's
list is global, so the checklist carries the consent story rather than a
per-agent OpenCode grant.

`gim-preflight` checks `gh auth status` / `acli jira auth status` (host/site
only), fetches the named repos in parallel, and pulls `--ff-only` only when a
repo is clean and on the expected branch (`--branch` defaults to `dev`; pass
the prefs `default_dev_branch`). Untracked `.agent-scratch/` evidence is
ignored by the dirty check, so a cross-repo `gim-pr` result does not block a
sibling repo's pull. `--dry-run` performs no fetch or pull and
reports the planned actions plus the read-only status/docs index. It returns the
product docs index (`docs/plans`, `docs/adr`, `docs/rfcs`, `docs/guides`) so the
orchestrator never searches for the next number by hand. A failed remote
`git ls-tree`, a failed path-only `gh pr list --jq` source, skipped gh auth, or
a failed fetch sets the family `ok=false` with `remote`/`open_pr`/`max`/`next`
null, so a stale `next` is never returned. Forwarded error text is redacted
(emails, account ids). Unlike `gim-run`, it intentionally does not scrub
`GH_TOKEN` / `ACLI_*`: these commands need the user's own auth.

`gim-pr` is one explicit PR flow: it validates a repo-relative file list
(globs, directories, escapes, and duplicates handled), guards the index before
and after staging, commits with the given message, pushes `-u <remote> HEAD`,
creates the PR with `--body-file`, and runs the stack lifecycle. Before any
stack command it sets `git config rerere.enabled true` and
`git config push.autoSetupRemote true` in the target repo (idempotent), and it
calls `gh stack init`/`add`/`link`/`sync` internally. Lifecycle mechanics,
result fields, and the manual fallback are
owned by `github-cli-workflow` §4 + `references/gim-stack-lifecycle.md`.
`--dry-run` prints the command plan and performs no mutation. `gim-pr` errors
name the failed precondition and the required input; they never prescribe a
manual fallback.

Stacked-PR tier (**version-controller only**, read-only; `gh stack` extension):

```
gh stack view:*
```

`gh stack view` is read-only and runs silent. `gim-pr` runs `gh stack init`,
`add`, `link`, and `sync` internally; those four are therefore not on any agent
allowlist. Lifecycle behavior (first-PR pending, link, sync verification,
recorded failures) lives in `github-cli-workflow` §4 +
`references/gim-stack-lifecycle.md`. `gim-pr` never calls `gh stack checkout`
and never calls `gh stack submit` to repair a result.

`gh stack add`, `link`, `submit`, `sync`, `push`, `merge`, and `rebase` stay
consent-gated for direct agent use and are **not** on this list: they create
branches/PRs, rewrite bases, rebase, or force-push. `gim-pr` runs `init`,
`add`, `link`, and `sync` internally as part of its documented flow. `submit` is a manual recovery command only.

Keep this entry **only** in the version-controller frontmatter
(`.opencode/agents/gim-version-controller.md`), never in the global OpenCode
template (`opencode.local.template.jsonc`). It is on the Cursor Auto-run
checklist with the fleet verbs; Cursor's list is global, so that checklist is
the consent story rather than a per-agent OpenCode grant.

Raw data-only built-ins worth allowlisting (no script-block surface). **Use the
scoped forms — a bare entry expands to destructive forms on prefix-only
harnesses** (see warning below):

```
git status  git log  git diff  git blame  git show
git branch -l  git branch --list        (list-only; never bare `git branch`)
git stash list                          (no args only)
git add -- <path...>                    (explicit path scope; -A / -f are out)
                                        Cursor/prefix harnesses: keep this scoped
                                        form. opencode: the config uses the
                                        general `git add*` allow instead — a
                                        `git add --*` scoped pattern trips the
                                        matcher (the first file after `--` is
                                        seen as a separate command); the
                                        hook/plugin denies the destructive
                                        -A / -f variants.
gh pr view  gh pr diff  gh pr list  gh issue view
Get-Content:*  Test-NetConnection:*  Write-Output:*
```

**Prefix-harness warning**: on prefix-only matchers (Cursor, Claude Code) a
bare entry like `git branch` also matches `git branch -D`/`git branch -d`, and
`git add` matches `git add -A`/`git add -f`. Always use the scoped forms above
on those harnesses, and add explicit denies for the destructive variants where
the harness supports deny rules (`git branch -D`, `git branch -d`, `git add -A`,
`git add -f`, `git stash drop`, `git stash apply`, `git stash clear`). These git
entries are convenience grants for data-only reads; the destructive side is the
two-turn pattern (read, then act via `gim-apply`/version-controller).

Test tier (optional, suite-only — only needed to run the Pester suite, guide 0001):

```
Import-Module Pester*  Invoke-Pester*  Should*  InModuleScope*
```

plus external-dir allow for the Pester install locations:

```
C:/Program Files/PowerShell/Modules/Pester/**
%USERPROFILE%/Documents/PowerShell/Modules/Pester/**
```

(user-scope install per guide 0001: `Install-Module Pester -Scope CurrentUser`).

## Deny list

The five block-accepting cmdlets — script-block bodies are the
`[Type]::Method()` execution hole (AGENTS ADR 0005; the harness permission checker is
blind to .NET member invocation inside `{ }`):

```
ForEach-Object  Where-Object  Group-Object  Sort-Object  Select-Object
```

Direct execution-escape verbs (AGENTS ADR 0005 invariant 2):

```
Invoke-Expression  iex  Add-Type  Start-Process  &$var
pwsh  powershell    (agents NEVER invoke — the harness shell already runs pwsh;
                     scripts run in-shell via `& <path>` / pipeline / import;
                     hook/plugin deny them at COMMAND position with a clear
                     message per the deny-message contract; only the opencode
                     plugin (JS, non-agent) spawns `pwsh -File`. Never
                     allowlisted: a `pwsh*` entry would grant arbitrary
                     execution.)
```

Command substitution into allowlisted verb arguments — the substituted text is
uncontrolled argv; the sanctioned channel is `--<param>-from-var` (AGENTS ADR 0005
invariant 6):

```
$()  (command substitution; also deny the practical forms below)
```

Matcher forms: a literal `$( )` rule never matches `$(Get-Date)` on glob
matchers — the entry above is the concept, not a paste-ready glob. Use
practical per-harness forms, e.g. `*$(*` / `*$(Get-*` on glob harnesses, or
the harness's own command-substitution matcher if one exists; opencode's
AST-aware matcher catches substitution structurally and is preferred. The
denial is a speed bump, not a boundary — the discovery-at-failure hook text
(deny message naming the sanctioned channel `--<param>-from-var`) remains the
main defense.

Execution verbs (AGENTS ADR 0006 — run only through `gim-run`/`gim-probe`, never raw):
Scope note: `gim-probe --route` is designed for locally-launched apps on the
probe's random local port; an absolute external http(s) URL is accepted but is
outside the local-only design intent.

```
dotnet nuget push
dotnet nuget add source
dotnet tool install -g
dotnet new install
dotnet workload install
dotnet <path>.dll        (also: dotnet exec)
dotnet fsi / dotnet script
dotnet run               (raw execution — gim-run is the only channel, AGENTS ADR 0006)
dotnet msbuild -t:<arbitrary>
dotnet publish -o <outside repo>
git push --force / -f / --force-with-lease
                          (force-push variants ONLY — denied by the
                           hook/plugin with the explicit-consent message
                           (AGENTS ADR 0007 §5). Plain `git push` is NOT denied —
                           version-controller pushes regularly; on opencode it
                           falls through to the default ask, on Cursor it is
                           on the Auto-run checklist. Lean deny posture: this
                           deny lives in the hook/plugin, not the config.)
```

## Hooks

**Production wiring (plan 0004 / AGENTS ADR 0007 — implemented):**

- OpenCode plugin: `.opencode/plugins/gim-permissions.js` + decision predicates
  in `lib/gim-permissions-rules.js`. Deny-with-message via
  `tool.execute.before` throw; git `--` pathspec enforcement via before-stage
  throw + ask-stage reject/do-nothing; **never auto-allows** (AGENTS ADR 0007 §1a).
- Cursor hook: `.cursor/hooks/gim-permissions.ps1` (wired via
  `.cursor/hooks.json` for `preToolUse` + `beforeShellExecution`), deny via
  `{permission:"deny", agent_message, user_message}`.
- Central command parser — ONE lib, both harnesses:
  `.agents/skills/gim-tools/scripts/parse-command-tree.ps1` (pwsh
  `ParseInput` → JSON tree). The Cursor hook **dot-sources it in-process**
  (`Get-GimCommandTree`, no second spawn); the opencode plugin **spawns**
  `pwsh -File` lazily with a per-command cache. Never re-implement parsing
  locally.
- **Git `--` quirk is wired** (opencode): before-stage throw when the first
  file after `git <cmd> --` is not `.gitignore` (sacrificial-token hint);
  `.gitignore`-first runs silent; ask-stage reject/do-nothing is the safety
  net (see the Git `--` pathspec section below).
- **Lean deny posture (AGENTS ADR 0007 §5):** the hooks/plugin own every
  message-carrying deny; the config keep lists contain no message-carrying
  deny entries (a config deny would surface a generic ruleset dump, not the
  replacement message). Exception: scoped exact-form destructive `git add`
  denies (`git add -A*`, `git add -f*`, `git add --all*`,
  `git add --force*`) live in config as fail-open backstops — exact
  destructive forms only, not indiscriminate. The config backstops engage
  ONLY on plugin/hook fail-open (load/parse failure); on the happy path the
  plugin/hook message-carrying deny fires first (the before-stage throw
  precedes permission evaluation), so the "no message from config" posture
  stays accurate.

**(1) Secret-file deny** — deny reads (and writes) of these paths, covering
every access path (Get-Content, gim-*, raw file tools), not just gim-*:

```
*.secret.json  appsettings.*.secret.json  .env
*.user  *.pubxml.user
**/id_rsa  **/*.pfx  **/*.snk
```

**(2) `::` / reflection deny** — a speed bump against the .NET hole, not a
boundary (with the five block-accepting cmdlets denied there is no in-shell
carrier for `[Type]::Method()`; this is defense in depth):

```
::  $ExecutionContext  .GetType(  .Assembly  GetMethod  GetField  .Invoke(
```

**(3) Discovery-at-failure hooks** — denial text must name the replacement;
this is the channel that reaches an agent which never read the skill:

- denying `Select-String | ForEach-Object { }` says: use `gim-grep --capture N`
- denying `Select-Object -ExpandProperty` says: use `gim-json --path`
- every deny message carries the replacement verb.

**Deny message contract (user-locked, plan 0004):** every deny message opens
with explicit framing so models never mistake it for command output:

```
Your command was not run: command '<cmd>' was denied by <opencode permission
plugin | cursor permission hook>. Reason: <reason>. Please try: <alternative>.
This is NOT the command's output — this is the permission hook's error message.
```

Command text truncated (~120 chars). Both harnesses also deny `pwsh` /
`powershell` at COMMAND position (the harness shell already runs pwsh; scripts
run in-shell — `& <path>` / pipeline / import) with this same contract.
**Failure-mode split (user-locked 2026-08-20):** empty payload → fail-open
(`{}` / no throw — nothing received, nothing to decide); non-empty unparseable
payload → generic deny with the contract framing (reason = "the permission
hook could not interpret the permission payload (parse failed) — check
<log path>") on both harnesses — never a silent pass on an unreadable payload.

## File-write rules

```
allow <repo>/**
deny  <repo>/.agents/skills/gim-tools/**     (AGENTS ADR 0003 — the toolkit is the one
                                              place a single approved write
                                              converts every allowlisted verb
                                              into arbitrary execution)
ask   everything else
```

**Carved exception (A9, explicit):** `gim-verbs --gen-refs` writes to
`.agents/skills/gim-tools/references/**` — allow that one command's in-place
writes. Rationale: the generator is itself allowlisted and deterministic
(byte-identical re-runs, no timestamps/machine paths), and it writes only the
9 group files - it never writes `harness-config.md` (hand-written). The
`--out-dir <d>` flow remains the scratch/test regen path; the carve-out covers
only the default in-place directory. Anything else writing under
`.agents/skills/gim-tools/**` stays denied.

## Per-harness mapping

| Harness | Config home (user-owned) | Matching model | Status (plan 0004) | Notes |
| --- | --- | --- | --- | --- |
| opencode | `opencode.json` / `opencode.local.json` → `permission.bash.allow` / `.deny` arrays | `*` / `?` globs, **last-match-wins**, AST-aware (matches the parsed command, not the raw string) | **wired** — live rules in `opencode.local.json` (gitignored) + team copy `opencode.local.template.jsonc`; deny-with-message in `.opencode/plugins/gim-permissions.js` | Prefer `"<cmd>*"` over `"<cmd> *"` (trailing-space quirk below). Verify each rule live. |
| Cursor | Cursor settings allow/deny lists (or `.cursor/rules/*.mdc`) | **Prefix-only** — `gim-list:*` matches any command starting with `gim-list` | **wired** — hook `.cursor/hooks/gim-permissions.ps1` (`hooks.json`); Auto-run allowlist checklist in `.cursor/hooks/AGENTS.md` | Longer prefixes disambiguate (e.g. `git status` before any broader `git*`). No glob power beyond the prefix. **Must use the scoped git forms from the allow-list warning** (`git branch -l`, `git add -- <path...>`, ...). |
| Claude Code | `~/.claude/settings.json` → `permissions.allow` / `deny` | Prefix-style | **not wired** | Similar to Cursor; verify the exact pattern forms on the live harness. **Must use the scoped git forms from the allow-list warning.** |

Matching-power notes are a starting point, not ground truth — verify each rule
on the live harness before trusting it.

## Quirks

- **Trailing-space quirk**: `"<cmd>*"` preferred over `"<cmd> *"` — a pattern
  ending in a space is fragile against AST-aware matchers (opencode); the
  glob-suffixed form matches reliably.
- **Glued-range workaround**: use `@(1,2,3)` or spaced `1 .. 2` for range
  syntax; a glued `1..2` can be treated as one token by the matcher.
- **pwsh rule**: agents never invoke `pwsh`/`powershell` as a command; the
  harness shell already runs pwsh, so invoke scripts in-shell (`& <path>` /
  pipeline / import). Never allowlist `pwsh*` / `powershell*` — a `pwsh`
  entry would grant arbitrary execution to any agent.

## Replacements table (the five denied cmdlets)

| Denied | Use instead |
| --- | --- |
| `ForEach-Object { <label> }` loops | `gim-report --prefix <label>` / `--prefix-each` |
| `Where-Object { ... }` | `gim-where --<predicate>` (`--exists`, `--matches <re>`, ...) |
| `Group-Object` + count loops | `gim-count --by <field>` |
| `Sort-Object` | `gim-report --sort <field>` |
| `Select-Object -First/-Skip/-Last` | `gim-head -n N --skip N [--last]` |
| `Select-Object -ExpandProperty` | `gim-json --path <query>` |

## Test-tier observed gaps (decide individually)

From gim-tools session builder/tester footnotes — rules that blocked
runtime probes this session; wire them if you want the tester loop to
run untrammeled:

- `Import-Module*` — exact multi-word allow rules never fire on name tokens
  (`Import-Module Pester` was blocked despite an allow rule); use the
  wildcard form.
- `Get-Module*`
- `Format-Hex*`
- `ConvertTo-Json`
- `Invoke-Pester -Path @(array)` — the array form was blocked; the tester fell
  back to `Invoke-Pester -Path '<dir>' -PassThru`.

## Git `--` pathspec convention (session-recorded, user-approved)

- opencode's bash matcher checks only the **first** filename token after
  `git <cmd> --`; all subsequent path tokens are unchecked.
- **Convention**: use `.gitignore` as the sacrificial first file param in
  multi-file git commands — `git diff -- .gitignore A B C` (the token must be
  a real tracked file).
- **Security stance**: path-scoping grants no real security (an agent can
  trivially drop the sacrificial token). Do NOT claim path-level gating on
  allowlisted git read commands — `git diff/log/show/blame/status` are
  trusted-read. Secret-file deny binds on `Get-Content`/`gim-*` paths and via
  the future censor-file hook, not on git `--` pathspecs.
- **Wired (plan 0004, user-locked 2026-08-20)**: the opencode plugin enforces
  the convention — `tool.execute.before` THROWS with the sacrificial-token
  hint when the first file after a standalone `--` is not `.gitignore` (incl.
  `--` with no file after); `.gitignore`-first commands run silent (config
  allowlist); a `.gitignore`-first command that still asks is left alone; the
  ask-stage `reject` is the safety net (never auto-allows — AGENTS ADR 0007 §1a).
  Cursor has no such quirk — no rule there (documented harness-specific
  exception). Remaining future mitigation: a censor-file deny for non-git
  file-access paths, if needed; optionally report first-token-only matching
  upstream (likely a matcher bug).

## Never-commit paths

Harness config gates which files agents can touch; keep these out of every
agent's git write path (version-controller owns git):

```
.agents/agent-prefs.md
opencode.local.json / opencode.local.jsonc
start-opencode.ps1
.pr-body.md
allowlist-permission-findings-*.md
permission-hook-findings-*.md
permission-hook-methodology-*.md
opencode.json              (user commits, not agents)
anything under D:\Work\GIM\PRs
```

## Env-scrub note (AGENTS ADR 0006)

`gim-run` strips credential env vars (`NUGET_API_KEY`, `GH_TOKEN`,
`GITHUB_TOKEN`, `AWS_*`, `AZURE_*`) before exec, and raw `dotnet` execution is
deny-listed — the scrubbed wrapper is the only execution channel. Do not
re-allow raw `dotnet` to "fix" a blocked call.

## ADR invariant map (validation)

Every ADR invariant has a config-side counterpart in this checklist:

| ADR | Invariant | Config-side counterpart |
| --- | --- | --- |
| [AGENTS ADR 0003](../../../../docs/adr/agents/0003-AGENTS-gim-toolkit-skill-home.md) | toolkit home; skill dir not agent-writable | File-write rule `deny <repo>/.agents/skills/gim-tools/**` + `--gen-refs` carve-out |
| 0005 | data-only arguments; no script blocks | Deny five block-accepting cmdlets; `::`/reflection hook; `$( )` substitution deny; replacements table |
| 0006 | execution confinement; deny-listed dotnet verbs + git push (ADR 0007: plain push allowed, force variants denied); env scrub | Execution-tier deny list; `git push` entry = force-push variants only (hook/plugin deny with consent message; plain `git push` NOT denied); gim-run-only execution + env-scrub note |
| [AGENTS ADR 0008](../../../../docs/adr/agents/0008-AGENTS-gim-fleet-verbs.md) | fleet tier: one argv-based process runner (timeout, captured output, exact failure, redaction, dry-run on mutating commands), preflight network read + ff-only pull, docs index; explicit PR stage/commit/push/PR/stack | Seven fleet verbs (`gim-preflight`, `gim-pr`, `gim-jira-search`, `gim-jira-create`, `gim-jira-transition`, `gim-wrapup-gather`, `gim-wrapup-respond`) role-scoped in the version-controller or tracker frontmatter only; `--dry-run`; `--branch` default `dev`; `ok=false`/`next=null` on a failed index source; `gim-pr` index guard and explicit file list; no global allowlist entry |
