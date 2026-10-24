# GIM orchestration scripts and context preload

Turn the repetitive fleet loops into deterministic GIMTools commands, preload each agent's context on both harnesses, and remove the obsolete linter and commit paths. This is fleet work only: agents, skills, harness configuration, and GIMTools. No product code. All edits stay in `GIMtransfer`.

## Decisions

- **Command home:** `GIMTools`, not a new module. Add a `fleet` verb group. Existing data verbs keep their data-only invariants; fleet verbs add process execution, network reads, and git/gh/acli mutations under the same argv/no-shell rules.
- **New verbs (7):** `gim-preflight`, `gim-pr`, `gim-jira-search`, `gim-jira-create`, `gim-jira-transition`, `gim-wrapup-gather`, `gim-wrapup-respond`.
- **Doc index preflight:** `gim-preflight` also returns `docs_indexes` per repo for product plans, ADRs, RFCs, and guides. It reads local top-level files, `origin/<expected-branch>`, and currently open PR changed files. It excludes `docs/plans/agents/**`, `docs/adr/agents/**`, `.agents/**`, `.cursor/**`, and `*-AGENTS-*` filenames. The orchestrator uses `next`; it never searches for the last number by hand. Agent-only ADRs/docs keep their own manual sequence and get no remote/open-PR due diligence.
- **Removed verbs:** `gim-commit` is deleted with every live reference. `gim-linter` is deleted with every live reference; there is no linting in the cycle. Future linting returns as hooks, not as an agent.
- **Context preload:** one shared config at `.agents/agent-preload.json`.
  - OpenCode: `chat.message` + `experimental.chat.system.transform` inject the agent's prefs and skills on every turn.
  - Cursor: `sessionStart.additional_context` injects the orchestrator preload; the default Cursor chat is implicitly the orchestrator, so this hook needs no agent name or type. `preToolUse` with matcher `Task` rewrites `tool_input.prompt` via `updated_input` for each subagent. `subagentStart` is allow/deny only and is not used for injection.
  - `tool_input.subagent_type` is not documented for Task; the Cursor hook accepts `subagent_type` and `subagentType`, logs the raw payload, and a live probe confirms the field on Cursor 3.17.8.
- **`gim-pr` contract:** required `--repo`; exactly one file source (`--files-file` or repeatable `--file`); required `--message`, `--title`, `--target`; optional `--new-branch <branch>`; `--pr-body` required unless `--draft`; `--delete-pr-body`, `--remote`, `--assignee`, `--dry-run`, `--json`. Stack handling is mandatory and automatic, amended after the smoke-4 and smoke-5 live tests (2026-10-20): the first PR runs `gh stack init --base <target> <branch>` and records `stack.pending=true`; step PRs pass `--new-branch <branch>` and gim-pr runs `gh stack add <branch>` so the branch is born inside the local stack, then `gh stack link` extends the remote stack (append by stack number, or create with `--base` from the target step's `result.json`), and `gh stack sync` runs as verification only — its `✓ Stack synced` verdict alone never counts as success; a stack-count line `(K PRs…) (stack #N)` in either observed wording (`up to date with …` or `Linked to the existing stack on GitHub …`) plus no `⚠` lines is what passes. `gh stack checkout` is not used. No `--stack` flag.
- **PR evidence path:** `.agent-scratch/pr-files/<feature>/<step>/` contains `files.txt`, `body.md`, and `result.json`. `.agent-scratch/` is the canonical repo scratch root.
- **File list:** UTF-8, one repo-relative path per line; blank lines ignored; whitespace trimmed; no comments, globs, or directories. Missing paths go to `git add` and its exact error is reported.
- **First PR detection:** known prefixes `feature`, `docs`, `chore`, `hotfix`, `release`, `fix`, `bugfix`, `refactor`, `test`, `ci`, `build`, `perf` map `<prefix>/<slug>` to `<slug>/<prefix>`. Other shapes are step branches: `<feature>/<step>`.
- **CodeGraph:** sync once at the end of the builder's flow, after tests pass. No sync between edits. Debugger syncs after it edits.
- **Verify gate:** when `verify_per_step` is true, it is the most important gate in the run. No commit, push, PR, plan status update, or next step before an explicit affirmative.
- **Wrapup:** always at the end of the plan, always appends to the existing plan file, never creates a new plan file. A manual earlier run is only by explicit user request and does not append plan content or start follow-up work.
- **`gim-apply`:** all actions may target the repo or the OS temp root. `gim-write` and `gim-fix` stay repo-only.
- **Explorer search:** known symbol + relations -> CodeGraph; unknown/guessed symbol -> semble first, and semble is not optional; known shape -> ast-grep; exact string -> grep. CodeGraph is never used to guess a symbol.
- **Jira assignment:** self-assignment always uses `'@me'`; no account or email lookup. `gim-jira-create` defaults to `@me`; `--unassigned` is the explicit exception.
- **Jira sprint:** the tracker probe determines the sprint field and whether `--current-sprint` is resolvable. If it is not, `--sprint <id>` is explicit and the default is no sprint; the limitation is reported.
- **gh-stack:** the `gh-stack` skill is a vendor-owned copy (upstream `github/gh-stack`, provenance note intact). The fleet never edits it. All GIM stack-flow guidance (init/add/link/sync-verify, sync-never-adopts, consent gates, observed output fixtures) lives in `github-cli-workflow` — one source of truth.
- **One source of truth:** the same fact lives in exactly one live doc, especially where files intersect (an agent body plus a skill that agent always loads). Intersecting docs point to the owning doc instead of restating it. Applied in C3: `github-cli-workflow` owns the PR/stack mechanics; the version-controller bodies keep pointers and result-field meanings only; `gim-tools` owns the verb contract.
- **Fleet usage:** `gim-pr` ships in C3 (with its Pester suite and the smoke-6/7 live evidence) and is the default PR path going forward — a brief opts out only by explicitly saying **manual**. This plan opts out: its `## Execution mode` keeps the fleet on the manual git/gh flow for the remaining steps. The orchestrator writes the evidence files and hands the full PR package each time.
- **Delivery:** the user switched this run to the `gim-orchestrate` flow. Commit and PR per step are active after the verify gate. The first PR is `feature/gim-fleet-scripts` -> `dev`, carrying the plan and ADRs; step PRs chain leaf-safe. Commit subjects use `<stepid>: <description>` per `agentic-planning`. No ticket unless the user asks.
- **Repo scope:** `GIMtransfer` only. Historical plans and ADRs keep their old linter/commit references; only live guidance changes.

## Execution mode

- [x] Verify per step
- [x] Commit per step
- [x] PR per step
- [x] Critic per step

Step order is critic -> verify with user -> plan status update -> commit -> PR. The first PR is `feature/gim-fleet-scripts` -> `dev`; step PRs chain leaf-safe. Commit subjects use `<stepid>: <description>` per `agentic-planning`. The descriptive `gim-improve(gim-fleet): ...` messages in the steps are retained as summaries; prefix the step id when committing.

The fleet runs the manual git/gh flow for the remaining steps of this plan (the C3 commit included) — an explicit plan-level opt-out from the `gim-pr` default: the orchestrator writes `.agent-scratch/pr-files/<feature>/<step>/files.txt` and `body.md`, hands the full PR package (repo, branch, commit message, files file, body file, title, target) to the version-controller, and the version-controller stages/commits/pushes/opens the PR with explicit commands — no `gim-pr`, no `gh stack` commands. If `gim-pr` fails on any later run, the version-controller reports the failure and exactly what it did instead.

## Failure scenarios

- Agent skips the verify gate when `verify_per_step` is true, or treats silence, "ok", or a question as affirmative.
- Agent commits, pushes, opens a PR, or starts step N+1 before the verify gate.
- Agent moves to step N+1 without committing step N after verification.
- Agent moves to step N+1 without opening the step PR after the commit.
- Agent creates a new plan file during wrapup, or appends wrapup content to a different plan.
- Agent leaves the repo broken after a step (Pester, module import, or documented check fails).
- Agent reintroduces `gim-linter`, lint applicability, or lint budgets.
- Agent leaves a live `gim-commit` reference or re-adds the verb.
- Agent syncs CodeGraph before tests pass, or after every edit.
- Agent ships the Cursor preload hook without the live field probe, or emits invalid JSON from the hook (this blocks subagents).
- Agent injects PII (email addresses, account ids) into command output, JSON evidence, or chat.
- Agent edits `GIMfront`, `CoreWebApi`, `GIMmanager`, `GIMzip`, or another sibling repo.
- Agent changes product code in `GIMtransfer` source projects.
- Agent calls `gh stack submit` from `gim-pr`.
- Agent reintroduces `gh stack checkout` into `gim-pr`, treats `✓ Stack synced` alone as success, or re-adds a baseRef walk / full-chain fallback.
- Agent creates a Jira ticket or transitions a ticket without explicit user consent.
- Agent widens `gim-apply` temp access to `gim-write` or `gim-fix`.
- Agent uses CodeGraph to guess an unknown symbol, or skips semble when the symbol is unknown.

## Branch and preconditions

- Current checkout: `gim-158-gimxml/a1-xml-writer`. The worktree already has the OpenCode preload work and the `deepseek-flash` model pins. Preserve them; do not discard, stash, or reset.
- Commit and PR per step are active. Cut `feature/gim-fleet-scripts` from `dev`, carrying the current uncommitted fleet work; commit the plan and ADRs there, then open `feature/gim-fleet-scripts` -> `dev`. Step branches `gim-fleet-scripts/<stepid>-<slug>` chain from the previous branch.
- The current uncommitted preload/model-pin work rides on the feature branch. Step A0 stages only the plan, ADRs, and ADR index. Any step that edits a pre-pinned agent file carries that file's deepseek-flash pin; Step B1 commits the remaining preload/model-pin work in files no earlier step edits.
- Required tools: PowerShell 7, Pester 6, OpenCode >= 1.18.19, Cursor 3.17.8, `gh` with `gh stack` and `gh-pr-review`, `acli` authenticated, `gh` authenticated.
- Node tests are human-run: `node` is intentionally not agent-allowlisted. The user runs the OpenCode plugin tests at the verify gate.
- Tracker probes (D0) are mutating and need explicit user consent. The user may create the work ticket for this plan at D0.
- The `.agents/skills/gim-tools/**` write policy normally denies ordinary agents. This plan is an approved fleet-maintenance exception for the builder; runtime policy for ordinary agents is unchanged.

## Steps

### Step A0 — record fleet-tier and preload ADRs [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Owner:** orchestrator (ADRs are not delegated to the builder).
- **Files / symbols:** `docs/adr/agents/0008-AGENTS-gim-fleet-verbs.md`, `docs/adr/agents/0009-AGENTS-context-preload.md`, `docs/adr/agents/0000-AGENTS-INDEX.md`.
- **Change:** ADR 0008 records the GIMTools fleet tier: why the verbs live in GIMTools, which invariants still bind (argv, no shell, no code arguments, bounded output, redaction), and how the fleet tier differs from data verbs. ADR 0009 records the intended preload mechanism on both harnesses; Step B2 updates it with the live probe outcome. Add both to the ADR index.
- **Validate:** inspect links and status lines; confirm both ADRs name the code paths they govern.
- **Commit message:** `A0: record fleet-tier and preload decisions`

### Step A1 — remove gim-linter end to end [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** delete `.opencode/agents/gim-linter.md`, `.cursor/agents/gim-linter.md`, `.agents/agents/gim-linter.md`. Update `.opencode/agents/AGENTS.md`, `.cursor/agents/AGENTS.md`, `.opencode/agents/gim-builder.md`, `.cursor/agents/gim-builder.md`, `.agents/CONTEXT.md`, `.agents/skills/gim-orchestrate/SKILL.md`, `.agents/prompts/orchestrator-step-brief.md`, `.agents/prompts/build-plan-with-steps.md`, `.agents/skills/agentic-planning/SKILL.md`, `.agents/skills/gim-autopilot/SKILL.md`, `.agents/skills/github-cli-workflow/SKILL.md`, `.agents/skills/gim-improve/SKILL.md`, `.agents/agent-prefs.template.md`, `.opencode/agent-preload.json` or the shared config after B1, `.opencode/agents/gim-orchestrator.md`, `.cursor/agents/gim-orchestrator.md`, and any live harness guide that names `gim-linter`.
- **Change:** remove the linter role, its preload entry, its spawn permission, its prefs key, its applicability and retry rules, and every instruction that asks the builder to lint. The builder's only child is `gim-tester`. No linting happens in the cycle. Remove `gim-linter.enabled` from the prefs keys. The builder records only `test_applicable` and tester outcomes. Step A1 also carries the two pre-existing deepseek-flash pin hunks in the builder and orchestrator agent files it edits. Leave historical plans and ADRs untouched.
- **Validate:** `gim-grep --pattern "gim-linter|gim_linter|eligible_lint|lint_applicable|linter_attempts"` over live paths returns no hits outside `docs/plans/` and `docs/adr/`. Confirm the eight remaining agent files exist in both homes. Confirm no live cycle doc still tells the builder to lint: `.agents/prompts/build-plan-with-steps.md`, `.agents/skills/agentic-planning/SKILL.md`, `.agents/skills/gim-autopilot/SKILL.md`, `.agents/skills/github-cli-workflow/SKILL.md`.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): remove the linter role and lint cycle`

### Step A2 — remove gim-commit end to end [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** delete `.agents/skills/gim-tools/scripts/verbs/gim-commit.ps1`. Update `.agents/skills/gim-tools/scripts/GIMTools/GIMTools.psm1`, `GIMTools.psd1`, `.agents/skills/gim-tools/scripts/private/GimVerbs.ps1`, `.agents/skills/gim-tools/tests/gim-mutating.tests.ps1`, `gim-spine.tests.ps1`, `gim-verbs.tests.ps1`, `.agents/skills/gim-tools/SKILL.md`, `.agents/skills/gim-tools/references/mutating.md`, `.agents/skills/gim-tools/references/harness-config.md`, both version-controller and orchestrator twins, `.opencode/agents/gim-debugger.md`, `.cursor/agents/gim-debugger.md`, `.cursor/hooks/AGENTS.md`, `.opencode/plugins/AGENTS.md`, `opencode.local.template.jsonc`, and the live `opencode.local.json`.
- **Change:** remove the verb, its dot-source, its export, its vocabulary entry, its tests, its allowlist entries, its canonical conversion, and every live instruction that names it. The version controller uses `git commit -m`; later it uses `gim-pr` for the standard flow. Leave historical plans and ADRs untouched.
- **Validate:** `Get-Command gim-commit -ErrorAction SilentlyContinue` returns nothing; `gim-verbs --json` has no `gim-commit`; Pester `gim-mutating.tests.ps1`, `gim-spine.tests.ps1`, `gim-verbs.tests.ps1` pass with the new counts.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): delete the broken gim-commit verb`

### Step A3 — allow gim-apply on the OS temp root [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/gim-tools/scripts/private/GimPaths.ps1` (`Get-GimApplyWriteRoots`, `Resolve-GimApplyPath`, temp-root floor), `.agents/skills/gim-tools/scripts/verbs/gim-apply.ps1`, `.agents/skills/gim-tools/tests/gim-mutating.tests.ps1`, `SKILL.md`, `references/mutating.md`, `references/harness-config.md`, `docs/adr/agents/0006-AGENTS-gim-run-execution-confinement.md`.
- **Change:** `gim-apply --create-empty`, `--touch`, and `--remove` may target the current repo or the real OS temp root. Resolve the temp root once with `[System.IO.Path]::GetTempPath()` and real-path resolution. Apply a safety floor to the temp root: it must be a rooted local path, not a drive root or UNC path, at least two segments deep, and not equal to or an ancestor of the user profile, local app data, app data, Windows directory, system root, or the repo root. Keep `gim-write`, `gim-fix`, `gim-run`, and `gim-probe` repo-only. Update help text and the write-root table. Add tests for temp create, touch, remove, dry-run, rejection of a path outside both roots, and the unsafe-temp floor. Amend ADR 0006 for the gim-apply exception.
- **Validate:** `Invoke-Pester -Path .agents/skills/gim-tools/tests/gim-mutating.tests.ps1 -PassThru`.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): let gim-apply manage temp files`

### Step A4 — sync CodeGraph only after tests pass [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.opencode/agents/gim-builder.md`, `.cursor/agents/gim-builder.md`, `.agents/prompts/orchestrator-step-brief.md`, `.agents/skills/gim-orchestrate/SKILL.md` (steps 8-9), `.opencode/agents/gim-debugger.md`, `.cursor/agents/gim-debugger.md`.
- **Change:** the builder runs the tester first, and only after the tester passes does it sync CodeGraph once per indexed repo touched by the step. No sync after individual edits. If tests fail, no sync. The step brief no longer tells the builder to sync after edits; the builder body owns the end-of-flow rule. The debugger syncs after its edits, since it has no tester child. Add `codegraph_sync` (`ok | sync_deferred | skipped_tests_failed | not_applicable`) and `index_stale` to the builder output contract, so the critic can distinguish a deferred sync from a skipped one. Move the sync rule out of the Code search section into an end-of-flow section.
- **Validate:** inspect both builder twins and the step brief for the order `tests -> sync`; confirm no "sync after edits" instruction remains for the builder; confirm the output contract carries `codegraph_sync` and `index_stale`.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): sync CodeGraph after validation`

### Step A5 — stern verify gate and wrapup placement [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.opencode/agents/gim-orchestrator.md`, `.cursor/agents/gim-orchestrator.md`, `.agents/skills/gim-orchestrate/SKILL.md`, `.agents/prompts/build-plan-with-steps.md`, `.agents/skills/gim-autopilot/SKILL.md`, `.agents/skills/gim-wrapup/SKILL.md`.
- **Change:** state that when `verify_per_step` is true, the verify gate is the most important gate in the run. Do not commit, push, open or advance a PR, mark the step complete, or start the next step without an explicit affirmative such as `looks good` or `verified`. Silence, "ok", a question, or a discussion point is not affirmative. Skipping the gate is a CRITICAL FAILURE. Reconcile this with the autopilot's deferred verification: when the user explicitly invoked `gim-autopilot`, the per-step gate is deferred to the run end and the consolidated final report is the gate; the autopilot override does not affect other gates. Z-steps always verify with the same stern rule even when `verify_per_step` is false. Wrapup always runs at the end of the plan, always appends to the existing plan file, and never creates a new plan file. A manual earlier run is only by explicit user request; it gathers CI and comments but does not append plan content, run the end critic, or start follow-up work.
- **Validate:** inspect all six files; confirm the words "most important gate", "CRITICAL FAILURE", "never creates a new plan file", and "end of the plan" appear in the right places; confirm the autopilot carve-out and the Z-step rule are present.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): make verify and wrapup gates stern`

### Step A6 — concise subagent briefs [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/prompts/orchestrator-step-brief.md`, `.opencode/agents/gim-orchestrator.md`, `.cursor/agents/gim-orchestrator.md`, `.agents/skills/gim-orchestrate/SKILL.md` (step 8 and non-negotiable 5), `.opencode/agents/gim-builder.md`, `.cursor/agents/gim-builder.md`.
- **Change:** the orchestrator sends the task and its parameters, not a role preamble, not a skill-read list, and not a command list. The step brief becomes: plan path, step id, files/symbols, step excerpt, extra context, architecture, gate values, and only step-specific constraints and failures. The builder already knows its skills, search limits, validation rules, and output contract from its own body. Keep the plan path and step excerpt so the builder can read exactly one step. Remove the duplicated "read caveman" and "run codegraph like this" blocks from the brief. The builder bodies keep the doc-only test carve-out (no test source for docs), the plan-file edit guard (only the excerpt may authorize a status/notes edit), the generic failure rules (including a knowingly broken touched area), and the no commit/push/PR rule.
- **Validate:** inspect the new brief; confirm it still carries plan path, step id, files/symbols, gates, and extra context; confirm no role preamble, skill-read list, or command catalog; confirm the builder bodies own the doc-only test carve-out, plan-file guard, generic failure rules, and commit/push prohibition.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): send task parameters, not role essays`

### Step A7 — explorer search order wording [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/workspace-code-search/SKILL.md`, `.opencode/agents/gim-explorer.md`, `.cursor/agents/gim-explorer.md`, `.opencode/agents/gim-debugger.md`, `.cursor/agents/gim-debugger.md`, `.agents/skills/gim-orchestrate/SKILL.md` (step 2), `AGENTS.md` (Code search section).
- **Change:** state the rule plainly in the explorer and debugger bodies and in `workspace-code-search`: if you know the exact symbol and want its relations or call chain, use CodeGraph; if you do not know the symbol and are guessing the name, use semble first, and semble is not optional; do not use CodeGraph to guess a symbol; do not skip semble when the symbol is unknown. ast-grep is for a known shape; grep is for an exact string. The orchestrator step 2 only names the spawn/wait/tracker behavior and points at `workspace-code-search` and `AGENTS.md`; it does not repeat the tool mapping or command forms. `AGENTS.md` keeps the compact rule. Collapse duplicate decision-rule copies: keep one authoritative rule near the top, the scenario table, and a workflow that references it.
- **Validate:** `workspace-code-search`, the explorer twins, and the debugger twins carry the full unknown-symbol/semble-not-optional rule; `gim-orchestrate` step 2 contains no `npm exec`, `--path`, `--` rule, or "do not sync" and still contains the tracker/wait rules; `AGENTS.md` carries the compact rule; the decision rule is stated once in the search skill, not three times.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): make semble mandatory for unknown symbols`

### Step B1 — promote OpenCode preload and share its config [critic, verify, commit, PR]

Status: completed. Verified by the user; node suite 18/18 and the live OpenCode probe passed.

- **Files / symbols:** `.opencode/plugins/gim-preload.js`, `.opencode/plugins/tests/gim-preload.test.mjs`, `.agents/agent-preload.json` (new shared config), `.opencode/plugins/AGENTS.md`, `.opencode/agents/gim-*.md` (model pins already present), `docs/adr/agents/0009-AGENTS-context-preload.md` (citation only; B2 owns the hook update).
- **Change:** promote the untracked OpenCode plugin and tests. Move the config to `.agents/agent-preload.json` and update the plugin constant. The config maps the eight remaining fleet agents. Every agent gets `.agents/agent-prefs.md`. Add the role skills: orchestrator `caveman`, `agentic-planning`, `grill-with-docs`, `review-with-critic`, `gim-tools`; explorer `caveman`, `workspace-code-search`, `gim-tools`; builder `caveman`, `agentic-planning`, `fileaccess-routing`, `format-adapter-contract`, `gim-tools`; version-controller `caveman`, `git-branching-workflow`, `github-cli-workflow`, `gim-tools`; critic `caveman`, `gim-tools`; tracker `caveman`, `atlassian-jira-cli`, `github-cli-workflow`, `gim-tools`; debugger `caveman`, `diagnosing-bugs`, `gim-tools`; tester `caveman`, `gim-tools`. The critic and debugger sets match their bodies; do not preload `review-with-critic` for the critic or user-global `debug-instrumentation` for the debugger. No linter entry. Keep the `deepseek/deepseek-flash` pins.
- **Validate:** user runs `node .opencode/plugins/tests/gim-preload.test.mjs`; all tests pass on a clean checkout. The test must use repo-local skills and a temp fixture for the prefs file; it must not require the gitignored `.agents/agent-prefs.md` or any user-global skill, and it must not assert live prefs values. Use fixture strings for frontmatter/horizontal-rule tests and a temp file for the file-vs-skill resolution test. Live OpenCode probe: spawn `gim-explorer` and confirm the `<gim-preload agent="gim-explorer" files="4">` block (three skills plus prefs), `inject` in `%TEMP%\opencode\gim-preload.log`, and no `fallback` line on a normal turn. The no-`skill`/no-`read` criterion belongs to B3, not B1. Normalize trailing separators and case in the plugin's relative-path display, and cite ADR 0009 rather than plan steps in the config and plugin docs.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): promote the OpenCode preload plugin`

### Step B2 — add the Cursor preload hook [critic, verify, commit, PR]

Status: completed. Verified by the user; Pester 33/33; Cursor Task probe all eight agents.

- **Files / symbols:** `.cursor/hooks/gim-preload.ps1`, `.cursor/hooks/tests/gim-preload.tests.ps1`, `.cursor/hooks.json`, `.cursor/hooks/AGENTS.md`, `.agents/agent-preload.json`, `.opencode/plugins/AGENTS.md`, `.opencode/plugins/gim-preload.js`, `.opencode/plugins/tests/gim-preload.test.mjs`, `docs/adr/agents/0009-AGENTS-context-preload.md` (orchestrator-authored).
- **Change:** add a PowerShell hook with two roles. `sessionStart` returns `additional_context` with the orchestrator preload (prefs plus its skills); in Cursor the default chat is implicitly the orchestrator, so this hook does not try to detect an agent name or type. Update ADR 0009 to record that implicit role. `preToolUse` with matcher `Task` reads `tool_input.prompt` and `subagent_type` (fallback `subagentType`), resolves the agent's shared config, and prepends a `<gim-preload>` block to the prompt using `updated_input`. Preserve every other `tool_input` field. Idempotence matches a complete block **anchored at the prompt start**, so a prompt that quotes the block later still injects; align the OpenCode `hasBlock` check with the same complete-block rule. The hook is a pass-through/rewrite, not a permission grant: no-op, error, and unknown-type paths return `{}`; return `{"permission":"allow","updated_input":...}` only when injecting. Use the sibling permission hook's stdin pattern, never `[Console]::In` when stdin is not redirected. Emit ASCII-only JSON by escaping every code unit above 127 as `\uXXXX`; Windows Cursor decodes hook stdout with the console code page, and a non-ASCII block otherwise makes `preToolUse` fail closed as invalid JSON. Log sanitized fields (event, tool, subagent type, prompt length, keys, file count/bytes; no PII) to `%TEMP%\cursor\gim-preload.log`. Work on PowerShell 5.1 and 7. Wire both hooks in `hooks.json` beside the existing permission hook. Add one-line comments to the hook helpers and match the OpenCode `enabled` coercion. Update ADR 0009 with the probe outcome and the confirmed field name.
- **Validate:** `Invoke-Pester -Path .cursor/hooks/tests/gim-preload.tests.ps1 -PassThru`; tests use temp fixtures, not live mappings, and cover pass-through `{}`, injection `allow` + `updated_input`, field preservation, the stdin reader and script entry point, garbage input, unknown type, complete-block idempotence, a quoted block later in the prompt still injecting, literal-tag-without-block injection, non-ASCII JSON escaping, log redaction (no prompt text or identity), JSON-string `tool_input`, `enabled: false`, and `subagentType` fallback. Live Cursor probe: all eight fleet agents must return their mapped block, the log must show the real `subagent_type` field and the expected file count, and the subagent must not read or call the skill tool. `sessionStart` delivery is a separate main-chat probe; the ADR status stays scoped to the verified Task path until that probe passes. If the probe fails, do not proceed to B3; report the exact payload and keep manual skill reads.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): preload Cursor subagent context`

### Step B3 — rewrite agent skill-load sections [critic, verify, commit, PR; conditional on B2 probe]

Status: completed. Verified by the user; Task preload probes passed.

- **Files / symbols:** `.opencode/agents/gim-*.md` (8), `.cursor/agents/gim-*.md` (8), `.agents/skills/gim-orchestrate/SKILL.md` (mandatory load), `.agents/CONTEXT.md` (add **Preload**), `docs/adr/agents/0009-AGENTS-context-preload.md`.
- **Change:** replace the "read and obey" instructions for preloaded skills with a statement that the preload is injected and authoritative. Keep the area-skill matrix and each touched repo's `AGENTS.md` reads. Do not write "do not read this"; just stop instructing the redundant read. Do not remove the fallback knowledge that the agent can read a skill if the preload is missing. Skip this step entirely if the B2 live probe failed. Amend ADR 0009: subagent bodies rely on the Task preload; the orchestrator keeps a prefs read fallback until the session-start probe passes.
- **Validate:** inspect both twins; confirm the preloaded names are no longer framed as mandatory reads, while area skills and repo guidance remain. Confirm ADR 0009 matches the rewrite, `CONTEXT.md` does not assert unverified session-start delivery, tester/tracker direct reads are outside the preloaded list, and the prefs fallback wording is identical in the orchestrator bodies and the skill. Live probes: one OpenCode subagent and one Cursor subagent answer a skill-content question without a `read`/`skill` call.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): rely on preloaded agent context`

### Step C1 — add the fleet runner and gim-preflight [critic, verify, commit, PR]

Status: completed. Verified by the user; Pester 98/98; repo-name resolution covered.

- **Files / symbols:** `.agents/skills/gim-tools/scripts/private/GimFleetExec.ps1`, `GimFleetText.ps1`, `.agents/skills/gim-tools/scripts/verbs/gim-preflight.ps1`, `GIMTools.psm1`, `GIMTools.psd1`, `GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `tests/gim-verbs.tests.ps1`, `tests/gim-spine.tests.ps1`, `SKILL.md`, `references/harness-config.md`, `.opencode/agents/gim-version-controller.md`, `.cursor/agents/gim-version-controller.md`, `.opencode/agents/gim-orchestrator.md`, `.cursor/agents/gim-orchestrator.md`, `.agents/skills/gim-orchestrate/SKILL.md` (Plan paths and step 0/4), `opencode.local.template.jsonc`, `.cursor/hooks/AGENTS.md`.
- **Change:** add a fleet process runner: argv array, working directory, hard timeout, captured stdout/stderr, exit code, no shell, `Quote-Arg`, and a redaction helper for emails and account ids. Add the `fleet` group and `gim-preflight`. Signature: `gim-preflight --repo <name|path> [--repo <name|path>]... [--branch <name>] [--skip-gh-auth] [--skip-jira-auth] [--dry-run] [--json]`. It checks `gh auth status` and `acli jira auth status` (host/site only; no email or account id), then for each repo resolves the git root, records branch and clean/dirty, fetches all repos in parallel, and pulls `--ff-only` only when the repo is clean and on the expected branch. It never checks out, stashes, resets, or forces. It reports per-repo `{path, branch, expected_branch, clean, fetch, pull, status}` and exits 1 if any check fails.
  - **Repo resolution:** add `Resolve-GimFleetRepo` in the fleet helper. Accept a full path, a relative path with separators, or a simple repo name. A simple name resolves under the parent of the current repo root, case-insensitive, and must be a git root; missing names error with the available directory names, and case-only ambiguity errors with the matches. The resolved path is the working directory for child `git`/`gh` calls; the caller's working directory is unchanged.
  - **Step 0:** the orchestrator dispatches the version-controller to preflight the affected repos. The version-controller body owns the `gim-preflight` command example, repo-name resolution, clean-off-branch checkout, and docs-index handling. The orchestrator uses the reported `docs_indexes.next` for numbering and stops on a dirty repo or a failed check. The script itself never checks out.
  - **Dry run:** `--dry-run` runs no fetch or pull; it reports the planned fetch/pull and the read-only status/docs index.
  - **Doc index:** for each repo, also return `docs_indexes` for the fixed families `plans` (`docs/plans/NNNN-*.plan.md`), `adr` (`docs/adr/NNNN-*.md`), `rfcs` (`docs/rfcs/NNNN-*.md`), and `guides` (`docs/guides/NNNN-*.md`). For each family, read the maximum four-digit prefix from (a) local top-level files, (b) one `git ls-tree -r --name-only origin/<expected-branch> -- <family-path>`, and (c) open PR changed files from one `gh pr list --state open --json number,files` (or an equivalent path-only jq form with a structured-output size that cannot truncate JSON). Exclude `docs/plans/agents/**`, `docs/adr/agents/**`, `.agents/**`, `.cursor/**`, and any `*-AGENTS-*` filename. Return `{ local, remote, open_pr, max, next }` per family; an absent family is `max: 0, next: 1`. If the remote or open-PR source fails, set `ok=false`, `remote`/`open_pr=null`, `max=null`, `next=null`, and the error; never return a stale next. A failed fetch invalidates the remote/open-PR index for that repo. No config file, no caching, no remote-branch sweep, no per-file detail. The text summary prints one line per family, e.g. `docs plans next=11 (local=10 remote=10 open_pr=10)`.
  - **Orchestrator use:** update the skill's Plan paths rule to choose the primary product plan/ADR/RFC number from `docs_indexes.next`; the orchestrator does not search for the last number by hand. Agent-only plans (`docs/plans/agents/`) and agent-only ADRs (`docs/adr/agents/`) keep their own manual sequence.
  - **Allowlist:** add `gim-preflight` to the version-controller frontmatter only. Do not add it to the global OpenCode template or the generic Cursor Auto-run list; Cursor may ask once when the version-controller uses it.
  - **Harness docs:** add the fleet tier and ADR 0008 to `references/harness-config.md`. Comments in the new code cite ADR 0008, not plan numbers.
- **Validate:** `Invoke-Pester -Path .agents/skills/gim-tools/tests/gim-fleet.tests.ps1 -PassThru`; `gim-verbs --json` shows the `fleet` group; `gim-preflight --dry-run --json --repo GIMtransfer` resolves the current repo by name and returns the expected dirty/branch report without mutating anything; tests cover repo-name resolution (`GIMtransfer`, `gimtransfer`, a sibling name, a missing name, a case-only ambiguity), the dry-run, the pull decision, failed remote/open-PR index (ok=false, next=null), JSON parse/truncation failure, and the absence of a global allowlist entry. Confirm the orchestrator step 0 is a short dispatch to the version-controller, both orchestrator twins mirror that step, and the version-controller body carries the live `gim-preflight --repo GIMtransfer --branch dev --json` example and the command details. Live `gim-preflight --json --repo GIMtransfer` is user-run. Current evidence: open PR 132 carries `docs/plans/0010-klic-network-payload.plan.md` and `docs/rfcs/0008-strictness-tiers-and-observability.md`, so `plans.next` must be at least 0011 and `rfcs.next` at least 0009 even though this checkout is on an older branch.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add gim-preflight`

### Step C2 — add the gh-stack skill and allowlists [critic, verify, commit, PR]

Status: completed. Verified by the user; allowlists scoped to `gh stack view`.

- **Files / symbols:** `.agents/skills/gh-stack/SKILL.md`, `references/commands.md`, `references/stack-design.md`, `references/troubleshooting.md`; `.agents/agent-preload.json`; `.opencode/agents/gim-version-controller.md`; `.cursor/agents/gim-version-controller.md`; `.agents/skills/git-branching-workflow/SKILL.md`; `.agents/skills/gim-tools/references/harness-config.md`; `.cursor/hooks/AGENTS.md`; `.opencode/plugins/AGENTS.md`.
- **Change:** add a maintained gh-stack skill based on the upstream sources, with a provenance note, and verify its commands against `gh stack --help`. Register `gh-stack` in the version-controller preload. Add only `gh stack view` (read-only) to the version-controller frontmatter and the Cursor Auto-run checklist; do not add it to the global OpenCode template. `init`/`link` are called inside `gim-pr`; `add`, `submit`, `sync`, `push`, `merge`, and `rebase` stay consent-gated because they create branches/PRs, rewrite bases, rebase, or force-push. State that `gim-pr` uses `init`/`link` automatically, and `submit` is a manual recovery command.
- **Validate:** inspect the skill's command shapes against `gh stack --help`; confirm the version-controller body names `gim-pr` as the standard path and `gh stack submit` only as manual recovery; confirm only `gh stack view` is allowlisted and the mutating stack commands are consent-gated; confirm the skill has the provenance note, no "without a stack target" mode, and no wrong `--base feature/<name>` advice.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add the gh-stack skill`

### Step C3 — add gim-pr [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/gim-tools/scripts/verbs/gim-pr.ps1`, `scripts/private/GimFleetExec.ps1`, `GIMTools.psm1`, `GIMTools.psd1`, `GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `SKILL.md`, `references/harness-config.md`, `.opencode/agents/gim-version-controller.md`, `.cursor/agents/gim-version-controller.md`, `.agents/skills/gh-stack/SKILL.md` + `references/commands.md`/`stack-design.md`/`troubleshooting.md` (user-restored vendor state, shipped as-is with no fleet edits), `.agents/skills/git-branching-workflow/SKILL.md`, `.agents/skills/github-cli-workflow/SKILL.md` (+ its references), `opencode.local.template.jsonc`, `.cursor/hooks/AGENTS.md`.
- **Change:** implement the contract. `--repo` accepts a name or path through `Resolve-GimFleetRepo`; a simple name resolves under the parent of the current repo root, case-insensitive.
  ```text
  gim-pr --repo <name|path>
         (--files-file [<path>] | --file <path> [--file <path>]...)
         --message <subject>
         --title <title>
         --target <branch>
         [--new-branch <branch>]
         [--pr-body <path>] [--draft]
         [--delete-pr-body]
         [--remote <name>]
         [--assignee <login>]
         [--dry-run] [--json]
  ```
  Exactly one file source; `--files-file` with no value derives `.agent-scratch/pr-files/<feature>/<step>/files.txt`. Parse UTF-8 lines, trim, ignore blanks, reject globs/directories/escapes, dedupe. Missing paths are allowed and go to `git add`, whose exact error is reported; this supports deletions. Error if the index has staged files outside the list. Stage, commit, `push -u`, create the PR, then:
  - **Git config (documented upfront, not silent):** before any stack command, gim-pr sets two keys in the target repo's local git config: `git config rerere.enabled true` and `git config push.autoSetupRemote true`. Both are idempotent. rerere removes `gh stack init`'s interactive prompt and helps cascade rebases; autoSetupRemote makes a plain push set its upstream. The github-cli-workflow skill, the version-controller twins, and `harness-config.md` name these keys so the behavior is transparent.
  - **First PR — no `--new-branch`:** the current branch is the feature branch. Run `gh stack init --base <target> <current-branch>`; init adopts the existing branch into a new local stack with trunk `<target>`. Precondition: `<target>` must exist locally; a missing base is `ok=false` with the exact error. Then `gh pr create --base <target>`.
  - **Step PR — `--new-branch <branch>`:** uncommitted step work sits on the current stack top. Run `gh stack add <branch>` (no `-A`/`-u`/`-m`; it creates and checks out the branch inside the local stack and does not commit). The file-list guard stays with gim-pr's own `git add` of the explicit list. Then `gh pr create --base <target>`, where `<target>` is the previous branch.
  - **Stack link — step PRs only, after the PR is created:** extend the remote stack. Read the target step's `result.json`:
    - `stack.number` present → `gh stack link <N> <branch> --remote <remote>` (append path).
    - `stack.number` null (target is the first PR) → `gh stack link --base <target.pr.base> <target-branch> <branch> --remote <remote>` (create path; the base comes from the target step's `result.json` `pr.base`, never from a `gh pr view` walk).
    A non-zero exit, any `⚠` line, or a missing `stack #N` in the link output is `ok=false`. No fallback, no retry, no `gh stack checkout`. The raw link output is saved in `result.json`.
  - **Stack sync — verification only, step PRs, after the link:** run `gh stack sync --remote <remote>`. The verdict line alone is never success. Require exit 0, no `⚠` lines, and a stack-count line matching `\((\d+) PRs?[^)]*\) \(stack #(\d+)\)` — gh-stack v0.1.0 emits two observed wordings: `up to date with K PRs (stack #N)` and, on the first sync right after a create-path link, `Linked to the existing stack on GitHub (K PRs, already up to date) (stack #N)`; the parser accepts the general shape (last match wins). `N` must equal the number parsed from the link output and `K` must equal the expected chain length — the `branches` array length of `gh stack view --json` run just before the sync (that view also supplies `stack.url`; a failed view is `stack.view_error`, a warning, and downgrades the `K` check to number-only). A missing count line, an `N` mismatch, or a `K` mismatch is `ok=false` with the sync output in the error. The raw sync output is saved in `result.json`. First PR: no link and no sync; `stack.pending=true`, `stack.number=null`.
  - **No `gh stack checkout`, no baseRef walk, no full-chain fallback.** `gh stack link` extends the remote stack; `gh stack sync` only verifies. Known gh-stack behavior (recorded in troubleshooting): sync never adopts a PR into an existing stack — the link step is mandatory before it.
  - **Import guidance (amended at C3):** the gim-tools skill tells agents not to import the module — the `gim-*` verbs are installed globally and auto-loaded; `Import-Module GIMTools -DisableNameChecking` is a one-off fallback only when a command is not recognized in the shell.
  - **Result and guards:** write `result.json` and the PR body inside the target repo's `.agent-scratch/pr-files/...` using the target repo root as the write root, not the module repo; `--pr-body` must resolve inside the evidence directory. Save the result and print the output before deleting the PR body; a result-write failure is a warning, not a lost command result. In `--file` mode, derive the evidence directory without calling the auto files-file helper; if the branch shape is invalid, use `.agent-scratch/pr-files/<sanitized-branch>/`. Reject detached HEAD before commit. `stack.method` is `init` (first PR) or `add` (step PR); step PRs record `stack.number`, `stack.link` (exit + raw output), `stack.sync` (exit + raw output + observed count line), and `stack.verified` (`true` only when the link and the sync verification both pass); `stack.pending` is true only for the first PR. `pr.assignee` uses the returned assignees when available. The dry run prints the exact would-be commands (config, init/add, stage, commit, push, pr create, link, view, sync) and performs no mutation.
- **Validate:** Pester covers argument errors, file-list parsing, path derivation, missing-path allowance, index guard, detached HEAD, `--dry-run` action selection, result-write roots and warning behavior, assignee output, and the result JSON shape. The stack section covers: the two git-config keys are set in the target repo before any stack command; first PR runs `gh stack init --base <target> <current-branch>`, fails `ok=false` on a missing local base, and records `pending=true` with no link and no sync; step PR runs `gh stack add <branch>` with no `-A`/`-u`/`-m`; the append path runs `gh stack link <N> <branch>` and the create path runs `gh stack link --base <base> <target-branch> <branch>` with the base read from the target step's `result.json` (a missing target `result.json` is `ok=false`; there is no `gh pr view` walk); a non-zero link exit is `ok=false`; any `⚠` line in link or sync output is `ok=false`; a missing `stack #N` in link output is `ok=false`; the sync verification requires a stack-count line `(K PRs…) (stack #N)` in either observed wording (`up to date with K PRs (stack #N)` or `Linked to the existing stack on GitHub (K PRs, already up to date) (stack #N)`) with `N` matching the link number and `K` matching the local branch count, and a mismatch is `ok=false`; a failed `gh stack view` is a warning and downgrades the `K` check to number-only; the raw link and sync outputs are saved in `result.json`; no code path emits `gh stack checkout`. User runs `gim-pr --dry-run` with a sample list on a scratch branch; no live commit or PR without explicit consent. **Before the commit** (untested code is never committed), the live smoke test runs a fresh 3-branch chain in CoreWebApi — `feature/gim-pr-smoke-6`, `gim-pr-smoke-6/a1-content`, `gim-pr-smoke-6/a2-final` (2/3/1 files, drafts, full `files.txt` and `body.md`, targets `dev` / `feature/gim-pr-smoke-6` / `gim-pr-smoke-6/a1-content`): the first PR reports no stack and `pending=true`; the second PR exercises the create path (`link --base` from the target step's `result.json`) and captures the create-path link output shape plus the 2-PR sync count line, with local `gh stack view --json` showing two branches and trunk `dev`; the third PR exercises the append path (`✓ Added 1 PR to stack #N`) and captures the 3-PR sync count line, with local `gh stack view --json` showing three branches, trunk `dev`, and a `pr` entry on the top branch; the remote stack is confirmed to contain all three PRs (the sync count line is the evidence — `gh api` is not permitted); optionally repeat the final link once to capture the already-linked skip output; no local sync failure occurs at any point. The smoke-6 outputs become the parser fixtures for the create path.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add gim-pr`

### Step D0 — probe Jira sprint and transitions [critic, verify, commit, PR; user consent]

Status: completed. Verified by the user.

- **Owner:** orchestrator + `gim-tracker` (mutating tracker work; not a builder step).
- **Files / symbols:** `.agents/skills/gim-tools/scripts/fleet/jira-config.json`, `.agents/skills/gim-tools/scripts/fleet/jira-transitions.json`, `docs/jira-board-specifications.md` if the workflow table changes.
- **Change:** with explicit user consent, create the work ticket for this plan or use a disposable ticket. Probe:
  1. Sprint assignment: try `additionalAttributes` with the sprint custom field. Record the field name/id and whether `--current-sprint` can be resolved without guessing. If it cannot, record `sprint_supported: false` and require explicit `--sprint <id>`.
  2. Transition series: from the ticket's current status, try each target status; record the exact `acli jira workitem transition` commands, exit codes, and whether direct transitions work or an intermediate status is required.
  Write the two JSON config files. Never guess a field or sequence; record only observed behavior.
- **Validate:** `gim-json --validate` both files; tracker returns the exact commands and results; no ticket is left unassigned unless the user chose `--unassigned`.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): record Jira sprint and transition probes`

### Step D1 — add gim-jira-search [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/gim-tools/scripts/private/GimFleetJira.ps1`, `.agents/skills/gim-tools/scripts/verbs/gim-jira-search.ps1`, `GIMTools.psm1`, `GIMTools.psd1`, `GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `SKILL.md`, `.opencode/agents/gim-tracker.md`, `.cursor/agents/gim-tracker.md`, `opencode.local.template.jsonc`, `.cursor/hooks/AGENTS.md`.
- **Change:** signature `gim-jira-search [--keyword <kw>...] [--jql <jql>] [--project <key>] [--limit <n>] [--current-sprint] [--json]`. No search input is required: with neither `--keyword` nor `--jql`, return the issues in the project (default `GIM`), optionally narrowed by `--current-sprint`. Default project `GIM`. A keyword builds `summary ~ "kw" OR description ~ "kw"` (escape backslashes first, then quotes), one query per keyword, dedupe by key. `--current-sprint` resolves the single active sprint from the D0 config and adds `sprint = <id>`; zero or many active sprints is `ok=false` naming the required `--jql` input. Return key, summary, status, and assignee display name only; never email or account id. Add it to the tracker allowlist.
- **Validate:** Pester covers JQL escaping, keyword merging, dedupe, redaction, the no-input query (all issues in the project), the sprint-only query, and the zero/many-active-sprint failure. User runs one read-only live search (keyword or sprint-only) for a harmless query.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add gim-jira-search`

### Step D2 — add gim-jira-create [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/gim-tools/scripts/verbs/gim-jira-create.ps1`, `.agents/skills/gim-tools/scripts/fleet/jira-create.template.json`, `.agents/skills/gim-tools/scripts/fleet/jira-config.json`, `GIMTools.psm1`, `GIMTools.psd1`, `GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `SKILL.md`, `.opencode/agents/gim-tracker.md`, `.cursor/agents/gim-tracker.md`, `opencode.local.template.jsonc`, `.cursor/hooks/AGENTS.md`.
- **Change:** signature `gim-jira-create --summary <s> [--project <key>] [--type <type>] [--assignee <@me|email>] [--unassigned] [--description-file <f>] [--sprint <id>] [--current-sprint] [--parent <key>] [--label <l>...] [--dry-run] [--json]`. Default project `GIM`, type `Task`, assignee `@me`. Build the payload from the generated template; set `labels` to `null` when the project screen rejects the optional field; set the payload assignee to `null`; create with `--from-json`; assign `'@me'` unless `--unassigned`; verify the key/assignee; return key and URL. Sprint uses the D0 config; if unsupported, `--sprint` errors clearly and `--current-sprint` is not offered. The tracker twins carry 2-3 complete execution examples for `gim-jira-create` (minimal defaults, description + current sprint, unassigned + explicit sprint) and for `gim-jira-search` (keyword, sprint-only, JQL), matching the version-controller's complete-invocation style.
- **Validate:** Pester covers template filling, ADF description, label/null behavior, assignee choice, and `--unassigned`. User creates one ticket with `--dry-run` first, then a live create only with consent.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add gim-jira-create`

### Step D3 — add gim-jira-transition [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/gim-tools/scripts/verbs/gim-jira-transition.ps1`, `.agents/skills/gim-tools/scripts/fleet/jira-transitions.json`, `GIMTools.psm1`, `GIMTools.psd1`, `GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `SKILL.md`, `.opencode/agents/gim-tracker.md`, `.cursor/agents/gim-tracker.md`, `opencode.local.template.jsonc`, `.cursor/hooks/AGENTS.md`.
- **Change:** signature `gim-jira-transition --key <key> --to <status> [--project <key>] [--dry-run] [--json]`. Read the current status; if it already matches, return a no-op. Read the observed sequence from `jira-transitions.json`; execute each step as `acli jira workitem transition --key <key> --status <target> --yes --json` (the CLI flag is `--status`, not `--to`; our verb's `--to` maps to it, per the D0 probe); stop on the first failure and return the exact command and result. Return current, target, sequence, and per-step results. The tracker twins carry 2-3 complete execution examples for `gim-jira-transition` (single transition, no-op when already at the target, dry-run), matching the other Jira verbs' example style.
- **Validate:** Pester covers no-op, sequence selection, failure stop, and dry-run. User transitions the work/dummy ticket through one path and confirms the final status.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add gim-jira-transition`

### Step E1 — add gim-wrapup-gather [critic, verify, commit, PR]

Status: completed. Verified by the user (live: `--pr` double-run; CoreWebApi PR 118 thread capture with replies; GIMtransfer PR chain from 151).

- **Files / symbols:** `.agents/skills/gim-tools/scripts/verbs/gim-wrapup-gather.ps1`, `.agents/skills/gim-tools/scripts/helpers/gim-wrapup-gather.mjs`, `GIMTools.psm1`, `GIMTools.psd1`, `GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `SKILL.md`, `.opencode/agents/gim-version-controller.md`, `.cursor/agents/gim-version-controller.md`, `opencode.local.template.jsonc`, `.cursor/hooks/AGENTS.md`, `.agents/skills/gim-wrapup/SKILL.md`.
- **Change:** signature `gim-wrapup-gather --repo <name|path> (--chain <pr> | --pr <n>... | --branch <branch>...) [--out <path>] [--parallel <n>] [--log-parallel <n>] [--json]`. `--repo` resolves through `Resolve-GimFleetRepo`; a simple name resolves under the parent of the current repo root, case-insensitive.
  - **Shim architecture:** the PowerShell verb is the allowlisted interface (argument parsing, repo resolution and containment, atomic write, compact envelope). A dependency-free Node helper (`scripts/helpers/gim-wrapup-gather.mjs`, stdlib only) owns chain walking, the concurrency pools, streaming log capture, and normalization; the verb spawns it and `node` is never invoked by agents directly. Node is a documented prerequisite (already required by CodeGraph).
  - **Two independent concurrency pools:** PR fetch (default 4, `--parallel`) and failing-log fetch (default 2, `--log-parallel`). Separate semaphores so log fetching never starves PR fetching. Results reassemble in chain order.
  - **`--chain <pr>` is the primary input mode** — reproducible by construction: one PR identifies the chain. Walk down via `baseRefName` while the base matches an open PR's head, then up by listing open PRs whose `baseRefName` equals the current head; bottom→top order, cycle guard, maximum 50 PRs, same-repo enforcement. `--pr`/`--branch` are the explicit fallback when chain discovery fails or the set is known. The three modes are mutually exclusive.
  - Fetch each PR with `gh pr view`; fetch checks with `gh pr checks --json`; fetch inline threads with `gh-pr-review review view --include-comment-node-id`; include top-level comments and reviews.
  - **Thread mapping (live shape):** `gh-pr-review review view` returns `reviews[].comments[]` (each with `thread_id`, `path`, `line`, `author_login`, `body`, `is_resolved`, `is_outdated`, and `thread_comments[]` replies). The gather maps each comment to one thread and each `thread_comments[]` entry to that thread's `replies[]` (`author`, `body`, `created_at`). There is no top-level `threads` key.
  - **Two artifacts:** `gather.json` (full evidence) and `responses.json` beside it — a bare work shell the agent edits: `{ repo, generated_at, prs: [ { pr, threads: [ { thread_id, resolve: true, response: "" } ], comments: [] } ] }`. Only PRs with at least one unresolved thread appear; only unresolved threads are listed; `comments` is a plain string array the agent fills with what it wants posted per PR. Re-gather merges by `thread_id`: authored `response`/`resolve` and `comments` are preserved, new unresolved threads added, resolved ones dropped; the write is atomic. The `gim-wrapup` skill documents this shape.
  - **Failing-check logs are captured by default.** If any check has a failing bucket, resolve the run/job id from the check `link` (`/actions/runs/<run>/job/<job>`) and fetch `gh run view --job <job> --log` (fallback `gh run view <run> --log-failed`), writing one file per failing action to `<gather-dir>/logs/<action-id>.log` (e.g. `run-<id>.log`, `job-<id>.log`). Each failing check records `log_file` (relative path), `log_truncated`, and `log_warning`; a happy path writes no `logs/` directory. Caps: 50 MB per file, 200 MB total; on exceed, truncate with a marker and set `log_truncated: true`. A missing/unparseable run id in a `link` is a per-check warning, not a gather failure. `gh api` stays forbidden; `gh run view` is the only log path.
  - Normalize to a pretty-printed, validated JSON document:
  ```json
  {
    "schema_version": 1,
    "generated_at": "...",
    "repo": "owner/name",
    "input": { "mode": "chain|pr|branch", "from": "...", "discovered": [1, 2] },
    "prs": [
      {
        "number": 1, "url": "...", "title": "...", "state": "OPEN",
        "is_draft": false, "base": "...", "head": "...", "author": "...",
        "review_decision": "...", "mergeable": "...", "merge_state_status": "...",
        "checks": [
          { "name": "...", "state": "...", "bucket": "...", "link": "...",
            "description": "...", "started_at": "...", "completed_at": "...",
            "log_file": null, "log_truncated": false, "log_warning": null,
            "log_source": null }
        ],
        "reviews": [ { "author": "...", "state": "...", "body": "...", "submitted_at": "..." } ],
        "top_level_comments": [
          { "id": "...", "author": "...", "body": "..." }
        ],
        "threads": [
          { "thread_id": "PRRT_...", "path": "...", "line": 1, "author": "...",
            "body": "...", "is_resolved": false, "is_outdated": false,
            "replies": [ { "author": "...", "body": "...", "created_at": "..." } ] }
        ]
      }
    ],
    "warnings": [],
    "summary": { "prs": 1, "checks_failing": 0, "checks_pending": 0, "threads_open": 0, "logs_captured": 0 }
  }
  ```
  Default output `.agent-scratch/wrapup/<slug>/gather.json` (slug from the first branch or `--out`) plus `responses.json` beside it; the single-PR version is `--pr <n>` with an `--out` path under the PR's evidence folder. Serialization: omit null-valued properties; `pass`/`skipping` checks carry only `name` and `state` (full rows for fail/cancel/pending and any check with log fields); 1-space indentation; UTF-8 without BOM; LF; validate by round-trip before writing.
- **Validate:** Pester covers the shim contract (argument parsing, repo/out containment, atomic double-write, compact envelope), fixture-driven normalization (including the live `reviews[].comments[]`/`thread_comments[]` mapping with `replies[]`), null omission, success-check reduction, 1-space serialization, the `responses.json` shell and its merge rule, JSON validation, missing extension error, single-PR shape, `--chain` walking (down and up, cycle guard, max depth, cross-repo rejection), mutually exclusive modes, two independent concurrency pools (`--parallel`/`--log-parallel`, no shared semaphore), default failing-log capture (one file per failing action, `log_file`/`log_truncated`, 50 MB per-file and 200 MB total caps, no `logs/` on the happy path), and a missing run id in a `link` degrading to a per-check warning. User runs one live chain gather against an open PR chain and reviews the JSON, `responses.json`, and any captured logs.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add gim-wrapup-gather`

### Step E2 — add gim-wrapup-respond [critic, verify, commit, PR]

Status: completed. Verified by the user (gather → responses shell → dry-run → live reply/resolve → rerun no-op).

- **Files / symbols:** `.agents/skills/gim-tools/scripts/verbs/gim-wrapup-respond.ps1`, `GIMTools.psm1`, `GIMTools.psd1`, `GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `SKILL.md`, `.opencode/agents/gim-version-controller.md`, `.cursor/agents/gim-version-controller.md`, `opencode.local.template.jsonc`, `.cursor/hooks/AGENTS.md`, `.agents/skills/gim-wrapup/SKILL.md`.
- **Change:** signature `gim-wrapup-respond --repo <name|path> --file <responses.json> --harness <name> [--dry-run] [--json]`. The input is the gather's `responses.json` shell (per PR: `threads[]` with `thread_id`/`resolve`/`response`, and `comments[]` as a plain string array). For each PR: post `gh-pr-review comments reply --thread-id <id> --body "<response>\n\n__this action was performed by <harness> automatically__"` for each non-empty thread response, then resolve with `gh-pr-review threads resolve --thread-id <id>` only when `resolve` is true and the reply succeeded; post each non-empty `comments[]` string with `gh pr comment <pr> --body "<string>\n\n__this action was performed by <harness> automatically__"`. Never post an empty response/comment. Apply-time remote refresh per touched PR (read-only `gh-pr-review review view`): a thread resolved remotely is skipped and marked resolved without a call; resolve only runs when the thread is remotely open; a locally resolved entry that is remotely open is retried when `resolve` is true. Status is recorded in a sibling receipt `<responses>.result.json` (per entry: `posted_at`, `resolved_at`, `remote_state`, `last_error`); reruns skip completed entries and retry failures. The authored `responses.json` is never rewritten by this verb. Per-entry failures keep the response/comment, write `last_error`, and continue; the verb returns `ok=false` with counts if any entry failed. `--dry-run` prints the planned actions and makes zero gh calls.
- **Validate:** Pester covers dry-run zero calls, footer exactly once, resolve-only-when-true, reply-then-resolve ordering, empty response/comment skip, the free-form per-PR `comments` string array, remote-state refresh (already resolved → skip/mark; reopened → re-resolve), receipt idempotence (no repost, retry failed, no re-resolve), per-entry failure isolation with `last_error`, redaction, and atomic receipt write. User runs one live reply on a test thread and confirms the thread resolves.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): add gim-wrapup-respond`

### Step F1 — regenerate references and reconcile all counts [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/gim-tools/references/fleet.md` (new), `references/mutating.md`, `references/harness-config.md`, `SKILL.md`, `GIMTools.psd1` description, `tests/gim-verbs.tests.ps1`, `tests/gim-spine.tests.ps1`, `.opencode/agents/AGENTS.md`, `.cursor/agents/AGENTS.md`, `.agents/CONTEXT.md`, `.agents/skills/gim-improve/SKILL.md`, `.agents/agent-prefs.template.md`, `opencode.local.template.jsonc`, live `opencode.local.json` (not committed), `.cursor/hooks/AGENTS.md`, `.opencode/plugins/AGENTS.md`, `docs/guides/0002-setup-gim-tools-autoload.md`.
- **Change:** run `gim-verbs --gen-refs` after all seven fleet verbs exist. Update every count to 25 verbs and nine groups; mutating has three verbs (`gim-fix`, `gim-write`, `gim-apply`); fleet has seven. Update the skill verb list, the group reference links, the module description, the allowlist docs, the CONTEXT terms (`FleetVerb`, `Preload`, `GatherFile`, `PRFileList`, `StackInit`), and the prefs template. Remove every live `gim-linter`, `gim-commit`, and "19 verbs" reference. Update the live OpenCode config and tell the user it is machine-local.
- **Validate:** `gim-verbs --gen-refs` is idempotent; Pester full suite passes; `gim-grep --pattern "gim-linter|gim-commit|19 verbs|19-verb"` over live paths returns nothing outside historical plans/ADRs.
- **Commit message (only if later authorized):** `gim-improve(gim-fleet): reconcile verb references and allowlists`

### Step F2 — full verification [verify]

Status: completed. Verified by the user. 596/596 tests (GIMTools 563 + Cursor hook 33); 25 verbs / 9 groups.

- **Files / symbols:** none (verification and report).
- **Change:** run the full Pester suite for GIMTools and the Cursor hook; have the user run the OpenCode Node preload tests; run the OpenCode and Cursor live probes; run `gim-preflight --json --repo .` (expect the current dirty branch report, no mutation); run `gim-pr --dry-run` with a sample file list; run one read-only `gim-jira-search`; run one `gim-wrapup-gather` against an open PR if available; inspect `git status` and confirm no commits, pushes, or PRs happened. Report exact commands, pass/fail counts, and residual risks.
- **Validate:** `Invoke-Pester -Path .agents/skills/gim-tools/tests -PassThru`; `Invoke-Pester -Path .cursor/hooks/tests/gim-preload.tests.ps1 -PassThru`; the live probes; `gim-verbs --json`.
- **Commit message:** none.

## Follow-up step (wrapup, 2026-10-23)

### Step G1 — resolve wrapup findings [critic, verify, commit, PR]

Status: completed. Verified by the user.

- **Files / symbols:** `.agents/skills/gim-tools/scripts/verbs/gim-pr.ps1`, `.agents/skills/gim-tools/scripts/helpers/gim-wrapup-gather.mjs`, `.agents/skills/gim-tools/scripts/verbs/gim-wrapup-gather.ps1`, `.agents/skills/gim-tools/scripts/verbs/gim-wrapup-respond.ps1`, `.agents/skills/gim-tools/scripts/private/GimVerbs.ps1`, `tests/gim-fleet.tests.ps1`, `SKILL.md`, `references/fleet.md`, `references/harness-config.md`, `.agents/skills/gim-wrapup/SKILL.md`, `.agents/skills/atlassian-jira-cli/SKILL.md` + `references/acli-reference.md`, `.agents/skills/github-cli-workflow/references/reviewing-pr-feedback.md`, `.opencode/agents/gim-version-controller.md`, `.cursor/agents/gim-version-controller.md`, `.opencode/agents/gim-tracker.md`, `.cursor/agents/gim-tracker.md`, `docs/rfcs/agents/0001-AGENTS-gim-node-shim.md`, `docs/plans/agents/0007-AGENTS-gim-orchestration-scripts.plan.md`.
- **Change (one PR for all 18 wrapup findings):**
  1. HIGH: `gim-pr --pr-body` resolves against the repo root like `--files-file`, then asserts evidence-dir containment; add the missing test.
  2. HIGH: `responses.json` shell includes any PR with top-level comments, not only PRs with unresolved threads.
  3. Remove the dead `top_level_comments[].response`/`posted_at` fields from `gather.json`.
  4. Tracker docs allow the documented `--unassigned` exception (drop the absolute "never create unassigned" rule).
  5. `harness-config.md`: one consent story for the Cursor Auto-run list (the fleet verbs and `gh stack view` are listed).
  6. Trim the VC/tracker twins' restated rules (PR body, stack consent, Jira flags) to pointers — one source of truth.
  7. `--remote` is documented for `gh stack link` as well as `sync` (GimVerbs metadata, `fleet.md`, plan C3 text).
  8. Local scratch cleanup: delete the stale `.agent-scratch/pr-files/gim-fleet-scripts/c3-gim-pr/result.json` (gitignored; not part of the PR).
  9. Record the residual: this chain stays manual for `gim-pr` (no `result.json`, not in a local stack).
  10. Flip the draft chain PR (#156) to ready (version-controller action; consent given).
  11. `gim-pr` refuses first-PR commits on `dev`/`main`/`release/*`; add a test.
  12. Share one `ConvertTo-GimWrapupIsoTimestamp` helper between gather and respond.
  13. RFC 0001: 25 verbs today; status update.
  14. Tracker examples use `--current-sprint` instead of the ended `--sprint 378`.
  15. Gather docstring matches the helper-override rule (module helpers dir only).
  16. VC twins' respond example path matches the gather example (`chain-40` vs `pr40`).
  17. `reviewing-pr-feedback.md`: drop the stale lint step.
  18. Plan E1 status wording: clarify 118/151 are PR numbers.
- **Validate:** full Pester suite (GIMTools + Cursor hook); targeted tests for items 1, 2, 11, 12; `gim-verbs --gen-refs` idempotent; grep for stale counts/roles; no vendor (`gh-stack`) edits.
- **Commit message:** `G1: resolve wrapup findings`

## Manual checklist (human)

1. **OpenCode preload.** After restart, spawn `gim-explorer` with the handoff probe. Confirm the `<gim-preload agent="gim-explorer" files="3">` block, the three paths, and no `skill`/`read` calls for preloaded files. Check `%TEMP%\opencode\gim-preload.log`.
2. **Cursor preload.** Add a unique marker to a mapped test skill, spawn `gim-explorer`, and ask it to quote the marker without reading files. Confirm `updated_input` reached the subagent and the log shows the real `tool_input` field names. Remove the test marker.
3. **Preflight.** Run `gim-preflight --json --repo .` on a clean checkout and on this dirty checkout; confirm the report is accurate and nothing was checked out, stashed, or reset. Confirm `docs_indexes.plans.next >= 0011` and `docs_indexes.rfcs.next >= 0009` because open PR 132 carries plan 0010 and RFC 0008.
4. **PR dry-run.** Run `gim-pr --dry-run` with a sample `.agent-scratch/pr-files/.../files.txt`; confirm the plan shows add, commit, push, PR create, and the correct `gh stack init` or `link`.
5. **Jira.** Run one read-only search. With consent, create one ticket and transition it through the observed sequence; confirm assignment and final status.
6. **Wrapup.** Run one gather against an open PR; edit one `response` and `resolve`; run respond; confirm the reply, the footer, and the resolved thread.

## Autopilot log (2026-10-21, overnight run)

- **Scope:** finish F1 (built, uncommitted) and F2; push step PRs as **draft**; no per-step verification pause and no handoff pause; stop only on blocking/critical issues.
- **Verify gate:** deferred for F1/F2 per the autopilot exception — the consolidated end report is the gate.
- **Autopilot turned off by the user before F1 verification; normal per-step verify gates resumed from F1 onward.**
- **Decisions:**
  - F1 commits on its own branch and opens a draft PR chained on `e2-wrapup-respond`.
  - F2 is verification-only: run the Pester suites and read-only probes; report the user-run items (OpenCode Node preload tests, live preload probes) instead of blocking.
  - The plan's F2 "confirm no commits/pushes/PRs happened" is read as "F2 itself performs no mutations"; the F1 commit/PR are expected from this run.

## Review

After execution, run `/review-with-critic granular` once more after the last per-step critic pass, with the full modified file list and `Steps implemented: A0, A1, A2, A3, A4, A5, A6, A7, B1, B2, B3, C1, C2, C3, D0, D1, D2, D3, E1, E2, F1, F2`. That wrap-up pass weights findings toward cross-step coherence: duplicate rules between the new verbs and the agent bodies, stale allowlist entries, and any place where a removed linter/commit reference survived.
