---
name: GIM OpenCode Fleet
overview: "Port the GIM orchestrator fleet to OpenCode: dual-home agents under .opencode/agents, project opencode.json (default_agent, subagent_depth 3, shared external_directory), gitignored local config via OPENCODE_CONFIG, harness tool map in gim-orchestrate, sync AGENTS.md in both agent folders. Linked to GIM-121."
todos:
  - id: A1
    content: AGENTS ADR 0002 + CONTEXT HarnessToolMap + ensure this plan is tracked
    status: completed
  - id: B1
    content: opencode.json + opencode.local.template.jsonc + gitignore
    status: completed
  - id: C1
    content: Dual agents/AGENTS.md sync contracts (.cursor + .opencode)
    status: completed
  - id: D1
    content: Port nine gim-* agents to .opencode/agents (models, permissions, bash allowlists)
    status: completed
  - id: E1
    content: Harness tool map in gim-orchestrate skill + OpenCode orchestrator agent body
    status: completed
  - id: F1
    content: AGENTS.md / WORKSPACE.md OpenCode setup; dual pointers; clear out-of-scope notes
    status: completed
  - id: G1
    content: Inventory consistency pass (names, depth, default_agent, sync reminders)
    status: completed
  - id: Z1
    content: Per-harness autoload HarnessToolMap (Cursor rules + OpenCode instructions) + wrap-up coherence fixes
    status: completed
  - id: Z2
    content: Wrap-up critic agrees — roster/name-only + harness-neutral critic spawn wording
    status: completed
isProject: false
---

# GIM OpenCode Fleet — Full Handoff Plan

> **Executor notice:** You did **not** participate in the design conversation. Treat this document as the sole source of truth. Do not re-grill locked decisions. Do not invent conflicting permission or config rules. If something is ambiguous *inside* this plan, ask the user once; otherwise implement as written.
>
> **Jira:** [GIM-121](https://gim.atlassian.net/browse/GIM-121) — *Create full Agentic workflow for GIM ecosystem* (reuse; do not create a duplicate ticket). Description/AC in Jira are empty — this plan is the spec.
>
> **Repo root:** `d:\Work\GIM\GIMtransfer` (Windows / PowerShell). Prefs: `.agents/agent-prefs.md` (`architecture: windows`, `verify_per_step`/`commit_per_step`/`pr_per_step` true, `critic_per_step: granular`, `critic_reruns_per_step: one`). **This plan overrides** `pr_per_step` → one end PR only (prefs stay true for other plans).
>
> **Working branch:** `feature/gim-121-opencode-fleet` (created from `feature/gim-orchestrator-fleet`). May be local-only until first push.
>
> **Cursor fleet reference:** [`docs/plans/agents/0001-AGENTS-gim-orchestrator-fleet.plan.md`](0001-AGENTS-gim-orchestrator-fleet.plan.md) — product loop, agent body contracts, deny-lists, prefs. This plan ports that fleet to OpenCode; do not re-implement Cursor agents from scratch.
>
> **Official OpenCode refs:** [agents](https://opencode.ai/docs/agents/) · [config](https://opencode.ai/docs/config/) · [permissions](https://opencode.ai/docs/permissions/) · [providers](https://opencode.ai/docs/providers/) (DeepSeek `/connect`)
>
> **ADR:** [`docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md`](../../adr/agents/0002-AGENTS-opencode-fleet-dual-home.md) (landed in A1)

---

## Executor handoff (read before step A1)

### Session state at handoff

> **At handoff (pre-A1):** no OpenCode config; pointers Cursor-only. **Current (post-G1):** inventory landed — rows below reflect present repo state.

| Item | State |
|---|---|
| Branch | `feature/gim-121-opencode-fleet` (from Cursor fleet branch) |
| A1 artifacts | this plan; `docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md`; `.agents/CONTEXT.md` (HarnessToolMap + dual-harness blurb) — committed in A1 |
| Must **never** stage/commit | `GIMtransfer-private.code-workspace`, `WORKSPACE-PRIVATE.md`, `.agents/agent-prefs.md`, `opencode.local.json` / `.jsonc` |
| OpenCode config in repo | `opencode.json`, `opencode.local.template.jsonc` (gitignored `opencode.local.json` via `OPENCODE_CONFIG`); `.opencode/agents/` — nine `gim-*.md` + sync `AGENTS.md` |
| Cursor agents | complete under `.cursor/agents/gim-*.md` + sync `AGENTS.md` (dual-home with OpenCode twins) |
| Pointers | `.agents/agents/gim-*.md` → dual-home (`.cursor/agents/` + `.opencode/agents/`); F1 landed |

**A1 job:** verify those three docs match locked decisions below, fix gaps if any, then critic → verify → commit. Do not rewrite locked Qs.

### How to run this plan

1. Confirm `git branch --show-current` is `feature/gim-121-opencode-fleet` (or recreate from `feature/gim-orchestrator-fleet` if missing).
2. Prefer `/gim-orchestrate` or execute via [`.agents/prompts/build-plan-with-steps.md`](../../../.agents/prompts/build-plan-with-steps.md) against **this** plan path.
3. Honor Execution mode strictly (critic → verify → commit per step; **one PR at end**).
4. Work on leaf-safe step branches `gim-121-opencode-fleet/<stepid>-<title>` (or directly on the feature branch when consolidating). Merge completed step work into `feature/gim-121-opencode-fleet` as you go or at convenient checkpoints.
5. **Single PR** when the plan is done (or when the user asks): `feature/gim-121-opencode-fleet` → `feature/gim-orchestrator-fleet`. Title per `github-cli-workflow` (include GIM-121 context). Assign `@me`. Body via repo-root `.pr-body.md` then delete. Do **not** open per-step PRs.
6. After G1 wrap-up critic; comment on GIM-121 via tracker (lean yes).

### PR policy change (mid-execution)

Early A1/B1 PRs (#38, #39) were closed; all step commits land on / merge into `feature/gim-121-opencode-fleet`. One PR targets `feature/gim-orchestrator-fleet` at the end.

### Suggested skills (invoke)

1. [`agentic-planning`](../../../.agents/skills/agentic-planning/SKILL.md) — gates / step coherence
2. [`build-plan-with-steps`](../../../.agents/prompts/build-plan-with-steps.md) — stern failure protocol
3. [`review-with-critic`](../../../.agents/skills/review-with-critic/SKILL.md) — granular + wrap-up
4. [`git-branching-workflow`](../../../.agents/skills/git-branching-workflow/SKILL.md) — leaf-safe names
5. [`github-cli-workflow`](../../../.agents/skills/github-cli-workflow/SKILL.md) — PRs, harness footer (default `opencode` if undetectable)
6. [`gim-orchestrate`](../../../.agents/skills/gim-orchestrate/SKILL.md) — if running as fleet orchestrator
7. [`caveman`](../../../.agents/skills/caveman/SKILL.md) — subagent reports
8. Port sources: [`.cursor/agents/gim-*.md`](../../../.cursor/agents/) + product contracts in [0001 plan](0001-AGENTS-gim-orchestrator-fleet.plan.md)

### Copy-paste skeletons (implementers)

**`opencode.json` (committed) — shape:**

```json
{
  "$schema": "https://opencode.ai/config.json",
  "default_agent": "gim-orchestrator",
  "subagent_depth": 3,
  "shell": "pwsh",
  "permission": {
    "external_directory": {
      "../GIMmanager/**": "allow",
      "../CoreWebApi/**": "allow",
      "../GIMocctWrapper/**": "allow"
    }
  }
}
```

**`opencode.local.template.jsonc` — generic only** (JSONC OK if you prefer comments; if pure JSON, use a short `_comment` string field or README line in AGENTS setup — prefer JSONC template named `opencode.local.template.jsonc` with `$schema` + example `../OtherProject/**` allow, **no** GIM optional sibling names):

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  // Copy to opencode.local.json (gitignored) OR point OPENCODE_CONFIG at your own file.
  // Add machine-local overrides below (example path is fictional).
  "permission": {
    "external_directory": {
      "../OtherProject/**": "allow"
    }
  }
}
```

**Env setup (document in AGENTS.md; help user if asked):**

```powershell
$env:OPENCODE_CONFIG = "D:\Work\GIM\GIMtransfer\opencode.local.json"
# Persist via user/env or profile as the user prefers — do not commit secrets.
```

Note: `OPENCODE_CONFIG` merges **below** project for conflicting keys but **additive** `external_directory` patterns stack (deep merge). Do **not** use `OPENCODE_CONFIG_CONTENT` for this project.

**OpenCode agent markdown — frontmatter pattern:**

```yaml
---
description: <one line; required — used for Task/@ discovery>
mode: primary   # or subagent
model: deepseek/deepseek-v4-pro   # or deepseek-v4-flash per Q2
permission:
  edit: allow   # or deny
  webfetch: deny
  websearch: deny
  task:
    "*": deny
    gim-linter: allow    # example — builder only
    gim-tester: allow
  bash:
    "*": ask
    ls*: allow
    # ... full common list from Q4, then agent extras ...
---
```

**Bash object rule:** put `"*": ask` **first**, then specific allow (and deny) globs (last match wins). Encode the full common allow list from Q4 on every agent; append extras per agent table. Default wildcard is **ask** (not deny) so non-conventional / expression-shaped commands can still proceed with user approval when the allowlist misses them.

**Readonly agents** (`gim-explorer`, `gim-critic`): `edit: deny`; keep `read`/`glob`/`grep`/`list` allowed (default allow is fine unless tightening). Critic bash = common + git-read extras only.

**read/skill/question:** leave default allow unless a step finds a reason to deny; do not over-constrain v0.

**`.agents/agents/` pointer format after F1** (example):

```text
Canonical: `.cursor/agents/gim-orchestrator.md` (Cursor) and `.opencode/agents/gim-orchestrator.md` (OpenCode). Keep behavioral contracts in sync per those folders' AGENTS.md.
```

**Sync `AGENTS.md` (both agent folders) must say:**

- Nine stems: orchestrator, explorer, builder, linter, tester, critic, version-controller, tracker, debugger
- Behavioral body/protocol changes require updating the twin in the other harness folder in the **same** change/PR
- Frontmatter may differ (Cursor model/readonly vs OpenCode mode/permission/model)
- Link AGENTS ADR 0002

### Permission encoding checklist (D1)

| Agent | mode | model | edit | task allows | bash |
|---|---|---|---|---|---|
| gim-orchestrator | primary | pro | allow | explorer, builder, critic, version-controller, tracker, debugger | common + git-read extras |
| gim-explorer | subagent | flash | deny | none | common + git-read extras |
| gim-builder | subagent | pro | allow | conditional linter/tester | common + New-Item, Copy-Item |
| gim-linter | subagent | flash | allow | none | common only |
| gim-tester | subagent | flash | deny | none | common only |
| gim-critic | subagent | flash | deny | none | common + git-read extras |
| gim-version-controller | subagent | flash | allow | none | common + git write + gh + New-Item/Copy-Item; soft-deny force/reset patterns |
| gim-tracker | subagent | flash | temp-only (`~/AppData/Local/Temp/**`; via edit + external_directory allow-globs, `*` deny elsewhere) | none | common + acli allows |
| gim-debugger | subagent | pro | allow | none | common + git-read extras + New-Item/Copy-Item |

Also set `webfetch`/`websearch` per Q4 edit/web table. Orchestrator **must** be able to task-spawn `gim-critic`. Builder must **not**.

### Model id verification

Once during D1 (or B1 if convenient): run `opencode models` after DeepSeek `/connect`. If official ids differ from `deepseek/deepseek-v4-pro` / `deepseek/deepseek-v4-flash`, keep provider `deepseek/` and fix stems; note the actual ids in the step verify recipe. Do not switch to `opencode/` or `opencode-go/` Zen/Go catalogs.

### Docs hygiene

- Never mention private/internal optional sibling repos in committed files.
- Committed `external_directory`: **only** GIMmanager, CoreWebApi, GIMocctWrapper.
- Do not disable built-in `build`/`plan`.
- Do not change Cursor model pins in this plan.
- `subagent_depth` must be **3**; `default_agent` must be **`gim-orchestrator`**.

---

## Execution mode (THIS meta-plan)

Mirrors agent prefs for this work **except** PR-per-step (plan-local override — do **not** change `.agents/agent-prefs.md`; other plans keep prefs `pr_per_step: true`):

- [x] Verify per step
- [x] Commit per step
- [ ] PR per step — **off for this plan only**; single PR at end: `feature/gim-121-opencode-fleet` → `feature/gim-orchestrator-fleet`
- [x] Critic per step (`granular`); `critic_reruns_per_step: one`

Order between steps: **repo-state checks → critic → verify with user → commit** (no per-step PR).

This plan has 7 steps across docs/config/agents groups. One end PR only.

### Failure scenarios

- Move to step N+1 while docs/config/agents are inconsistent after step N.
- Skip verify, critic, or commit when that mode is on.
- Commit before verify, or open the end PR before critic/verify on the final step / wrap-up.
- Commit `.agents/agent-prefs.md`, `opencode.local.json`, or private workspace files.
- Mention or document private/sibling repos that belong only in the user’s local OpenCode config (beyond the three committed `external_directory` paths).
- Use Linux `&&` / `||` on Windows.
- Create invalid refs like `feature/gim-121-opencode-fleet/a1-…`.
- Change Cursor agent **behavioral** contract without updating the OpenCode twin (or vice versa) after sync AGENTS.md exists.
- Set `subagent_depth` to less than 3, or `default_agent` to anything other than `gim-orchestrator`.
- Open additional per-step PRs after the one-PR policy change.

### First-commit confirmation (once)

- Branch: `feature/gim-121-opencode-fleet` (docs-first OK on feature branch).
- Step branches: optional. With **one end PR**, prefer committing directly on `feature/gim-121-opencode-fleet`. Leaf branches `gim-121-opencode-fleet/<stepid>-<title>` only if you want isolation or parallel work; otherwise they add merge overhead without a chained PR.
- Push: feature branch as needed; **one** PR at end (not per step).
- Excludes: `opencode.local.json`, `.agents/agent-prefs.md`, `GIMtransfer-private.code-workspace`, `WORKSPACE-PRIVATE.md`.

### After final step

1. Wrap-up critic: `Steps implemented: A1, B1, C1, D1, E1, F1, G1` (done mid-execution).
2. **Z1** — patchup from wrap-up + harness-map autoload design (see step Z1 below). Verify mandatory for Z-steps.
3. Open **one** PR: `feature/gim-121-opencode-fleet` → `feature/gim-orchestrator-fleet`.
4. Tracker: lean toward a comment on GIM-121 (OpenCode port implemented as described; waiting review).

---

## Why this exists

Plan 0001 delivered the Cursor-side fleet and explicitly left **OpenCode agent port** out of scope. Contributors using OpenCode need the same roster, loop, and prefs, with OpenCode-native agent files, permissions, models, and project config (`default_agent`, `subagent_depth`).

---

## Locked design decisions (DO NOT REOPEN)

### Q1 — Dual agent homes + sync duty

- Full bodies in **both** `.cursor/agents/gim-*.md` and `.opencode/agents/gim-*.md` (near-identical contracts).
- Sync instructions in **both** `.cursor/agents/AGENTS.md` and `.opencode/agents/AGENTS.md`: when modifying one agent’s behavioral contract, update the parallel file in the other folder in the same change.
- Frontmatter may differ by platform (Cursor `model`/`readonly` vs OpenCode `mode`/`permission`/`model`).
- Skills, prompts, prefs stay shared under `.agents/`.

### Q2 — Models (OpenCode agents only)

| Agents | Model |
|---|---|
| `gim-orchestrator`, `gim-builder`, `gim-debugger` | `deepseek/deepseek-v4-pro` |
| All other `gim-*` | `deepseek/deepseek-v4-flash` |

- Official DeepSeek provider via OpenCode `/connect` → DeepSeek.
- Implementation must run `opencode models` (or equivalent) once and adjust stems if provider lists differ; keep `deepseek/` provider prefix.

### Q3 — Nesting

- Project `opencode.json`: `"subagent_depth": 3`.
- Builder conditionally spawns linter/tester for eligible, bounded work and records their applicability independently; orchestrator owns critic; same product rules as Cursor AGENTS ADR 0001, with depth headroom. If both apply, linter runs first, but no linter result gates tester applicability. Validation allows one repair cycle only for actionable deterministic failures, with independent linter/tester retry budgets; terminal outcomes stop retries and escalate exact result plus recommendation to the user.

### Q4 — Permissions + bash allowlists

Permission values: `allow` | `ask` | `deny`. Prefer OpenCode `permission` (not deprecated `tools`).

**Task spawn ACL (illustrative — encode in agent frontmatter):**

| Agent | `permission.task` |
|---|---|
| `gim-orchestrator` | `"*": ask`, then allow: `gim-explorer`, `gim-builder`, `gim-critic`, `gim-version-controller`, `gim-tracker`, `gim-debugger` |
| `gim-builder` | `"*": deny`, then allow: `gim-linter`, `gim-tester` |
| All other fleet agents | `"task": "deny"` (or `{"*": "deny"}`) |

**edit / web (summary):**

| Agent | edit | webfetch/websearch |
|---|---|---|
| orchestrator | allow | deny |
| explorer | deny | allow |
| builder | allow | deny |
| linter | allow | deny |
| tester | deny | deny |
| critic | deny | deny |
| version-controller | allow | deny |
| tracker | deny | deny |
| debugger | allow | allow |

**Bash — common allow on EVERY agent** (glob form; `"*": ask` first, then allows; last match wins):

`ls*`, `dir*`, `pwd*`, `Get-Location*`, `cd*`, `cat*`, `Get-Content*`, `head*`, `tail*`, `dirname*`, `echo*`, `Write-Output*`, `Write-Host*`, `Test-Path*`, `Get-Item*`, `Get-ChildItem*`, `tree*`, `findstr*`, `grep*`, `rg*`, `Select-String*`, `find*`, `fc*`, `Measure-Object*`, `Sort-Object*`, `Select-Object*`, `ForEach-Object*`, `Out-Null*`, `git status*`, `git diff*`, `git show*`, `git log*`, `git ls-files*`, `git branch --show-current*`, `git rev-parse*`, `git check-ignore*`

**Bash — extras:**

| Agent | Extra allows |
|---|---|
| orchestrator, explorer, critic | `git branch*`, `git ls-tree*`, `git ls-remote*`, `git branch -vv*` |
| builder | `New-Item*`, `Copy-Item*` |
| linter, tester | (common only for now) |
| version-controller | `git add*`, `git commit*`, `git push*`, `git fetch*`, `git pull*`, `git ls-tree*`, `git ls-remote*`, `git branch*`, `git-grep*`, `git checkout*`, `git switch*`, `gh auth status*`, `gh --version*`, `gh pr list*`, `gh pr create*`, `gh pr view*`, `gh pr edit*`, `gh pr comment*`, `gh pr-review*`, `New-Item*`, `Copy-Item*` |
| tracker | `acli jira workitem view*`, `acli jira auth status*`, `acli jira workitem search*`, `acli jira sprint list-workitems*` |
| debugger | git-read extras + `New-Item*`, `Copy-Item*` |

**Deferred (grow later):** `dotnet*`, `npm*`, `uv run semble*`, ESLint/format CLIs. Prompt bodies may still instruct those tools; expand allowlists when needed. Unlisted bash still hits `"*": ask`.

**Version-controller destructive patterns** (after broader allows; last match wins) — align with prompt consent, not a hard paradox:

| Pattern | permission |
|---|---|
| `rm *`, `del *`, `Remove-Item*`, `git push --force*` | `deny` (hard stop) |
| `git push --force-with-lease*`, `git reset --hard*`, `git branch -D*`, `git commit --amend*`, `gh pr edit --base*` | `ask` (user consent gate; body still requires explicit consent) |

Other agents: prefer not listing those destructive globs unless needed; default `"*": ask` already prompts.

### Q5 — Project vs local OpenCode config

**Committed** `opencode.json` (repo root):

- `$schema`: `https://opencode.ai/config.json`
- `default_agent`: `gim-orchestrator`
- `subagent_depth`: `3`
- `shell`: `pwsh` (Windows team default)
- `permission.external_directory` **only**:
  - `../GIMmanager/**`: allow
  - `../CoreWebApi/**`: allow
  - `../GIMocctWrapper/**`: allow

**Gitignored** local file (name convention `opencode.local.json`):

- Loaded with **`OPENCODE_CONFIG=<absolute-or-stable-path-to-file>`** (file path — not `OPENCODE_CONFIG_CONTENT`).
- Holds additional `external_directory` (and any other machine overrides) for paths **not** in the committed set.
- **Do not** document private/internal repo names in committed templates, AGENTS.md, or this plan’s setup prose beyond the three committed paths above.

**Committed** `opencode.local.template.jsonc`:

- Generic placeholders only (e.g. comments/examples like `../OtherProject/**`) so another user can add **their own** paths.
- No private or optional GIM sibling names.

**Setup (AGENTS.md):** copy template to local file **or** provide the user’s own file; set `OPENCODE_CONFIG` to that path. Orchestrator may help with this setup step; never lecture about private repos.

### Q6 — Built-ins

- Leave OpenCode built-in `build` and `plan` **enabled**.
- `default_agent` is `gim-orchestrator`.

### Q7 — Visibility

- All `gim-*` agents remain `@`-visible (`hidden` omitted or false).

### Q8 — Branch / Jira

- Feature branch: `feature/gim-121-opencode-fleet` from `feature/gim-orchestrator-fleet`.
- Step branches: `gim-121-opencode-fleet/<stepid>-<title>`.
- Ticket: GIM-121 (reuse).
- Never commit private workspace files if present untracked.

### Q9 — Shared skill + HarnessToolMap

- Keep **one** `.agents/skills/gim-orchestrate/SKILL.md` for the product loop (harness-neutral).
- **E1 (landed):** dual-column Harness tool map lived in the skill.
- **Z1 (patchup):** move **per-harness spawn/resume mechanics** into autoload surfaces so agents need not Read the skill just for Task/resume wiring:
  - Cursor: `.cursor/rules/gim-harness-map.mdc` ([Cursor rules](https://cursor.com/docs/rules)) — Cursor column / Cursor-native bullets only; prefer `alwaysApply: true` (or intelligent attach if token pressure).
  - OpenCode: `.opencode/harness-map.md` + `opencode.json` `"instructions": [".opencode/harness-map.md"]` ([OpenCode instructions](https://opencode.ai/docs/rules/)) — OpenCode column only.
- Human dual-column crosswalk lives in this plan (see **Harness tool map** below); `gim-orchestrate` skill = pointer to autoload only — **do not** duplicate the full dual table into both always-on autoload files.
- Skill loop 0–16 stays harness-neutral; spawn/resume wiring → autoload HarnessToolMap.
- Sync duty: changing spawn/resume semantics updates **both** harness map autoload files (+ plan dual table if present) in the same change.

### Q10 — Template wording

- Template + setup as in Q5; “copy template **or** user’s own file”.

### Q11 — ADR + glossary

- ADR `docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md`.
- CONTEXT: harness-agnostic fleet blurb + term **HarnessToolMap**.

### Additional product rules (inherit from 0001)

- Same product loop 0–16, prefs, verify gate, handoff cadence, Jira thresholds, leaf-safe branches, critic freshness, builder resume, debugger v0 skeleton.
- Orchestrator is OpenCode **`mode: primary`**; other fleet agents **`mode: subagent`**.
- Filename stem = agent name (`gim-orchestrator.md` → `gim-orchestrator`).

---

## Harness tool map (E1 skill table → Z1 autoload)

Canonical action rows (dual-column crosswalk for humans; lives here — not in the skill):

| Action | Cursor | OpenCode |
|---|---|---|
| Invoke orchestrator | `/gim-orchestrate` skill on main (not Task-nested) | Primary agent `gim-orchestrator` (Tab / `default_agent`); skill still authoritative |
| Spawn subagent | Task `subagent_type: gim-*` | Task `subagent_type: gim-*` (same names) |
| Resume builder | Task `resume: <builder_agent_id>` | Task `task_id: <id from prior task>` |
| Fresh critic | New Task; never resume critic | New Task; never reuse critic `task_id` |
| Parallel explorers | Multiple Task; `run_in_background` OK | Multiple Task; document TUI child-session nav if useful |
| Load skill | Read path / skill attach | OpenCode `skill` tool + Read; `.agents/skills` discovered natively |
| Prefs / plans / ADRs | Same paths under `.agents/` and `docs/` | Same |

**Z1 runtime source of truth for spawn/resume:** per-harness autoload files (Cursor rule + OpenCode `instructions`), each containing **only that harness’s column**. Dual table above remains the crosswalk; do not paste it wholesale into both always-on files.

---

## End-state inventory

### OpenCode agents (`.opencode/agents/`)

| File | mode | model | Notes |
|---|---|---|---|
| `gim-orchestrator.md` | primary | `deepseek/deepseek-v4-pro` | default_agent; task allowlist |
| `gim-explorer.md` | subagent | `deepseek/deepseek-v4-flash` | edit deny |
| `gim-builder.md` | subagent | `deepseek/deepseek-v4-pro` | conditional task → bounded linter/tester |
| `gim-linter.md` | subagent | flash | |
| `gim-tester.md` | subagent | flash | |
| `gim-critic.md` | subagent | flash | edit deny |
| `gim-version-controller.md` | subagent | flash | git/gh allowlist |
| `gim-tracker.md` | subagent | flash | acli allowlist; temp-scratch writes only |
| `gim-debugger.md` | subagent | `deepseek/deepseek-v4-pro` | |
| `AGENTS.md` | n/a | n/a | sync duty with `.cursor/agents/AGENTS.md` |

### Config / template

| Path | Role |
|---|---|
| `opencode.json` | committed project config |
| `opencode.local.template.jsonc` | generic template |
| `opencode.local.json` | gitignored; user-provided or copied |

### Shared updates

| Path | Role |
|---|---|
| `.agents/skills/gim-orchestrate/SKILL.md` | Product loop; points at autoload HarnessToolMap |
| `.cursor/rules/gim-harness-map.mdc` | Cursor-only spawn/resume (autoload) |
| `.opencode/harness-map.md` | OpenCode-only spawn/resume (`instructions`) |
| `.cursor/agents/AGENTS.md` | sync duty (new) |
| `AGENTS.md` | Thin dual-home pointers + link to OpenCode setup guide |
| `docs/guides/0000-setup-opencode.md` | Full OpenCode install / `OPENCODE_CONFIG` walkthrough |
| `WORKSPACE.md` | OpenCode no longer “out of scope” |
| `.agents/agents/gim-*.md` | dual-home pointers (Cursor + OpenCode) |
| `docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md` | ADR |
| `.agents/CONTEXT.md` | HarnessToolMap + dual-harness blurb |

---

## Implementation steps

### Step A1 — ADR + CONTEXT + plan tracked [critic, verify, commit]

- **Files**: `docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md`; `.agents/CONTEXT.md`; `docs/plans/agents/0002-AGENTS-gim-opencode-fleet.plan.md` (this file).
- **Change**: Ensure AGENTS ADR 0002 and CONTEXT updates exist and match locked decisions; plan file present for tracking.
- **Validate**: ADR number unique; CONTEXT defines HarnessToolMap; no private-repo names in these docs.
- **Commit message**: `A1: add OpenCode fleet ADR, CONTEXT term, and plan 0002`

### Step B1 — Project config + local template + gitignore [critic, verify, commit]

- **Files**: `opencode.json`; `opencode.local.template.jsonc`; `.gitignore`
- **Change**: Committed config per Q5 (`default_agent`, `subagent_depth: 3`, `shell: pwsh`, three `external_directory` allows). Generic local template. Gitignore `opencode.local.json` and `opencode.local.jsonc`.
- **Validate**: JSON schema URL present; only the three sibling globs in committed ext-dir; template has no private/optional GIM sibling names.
- **Commit message**: `B1: add opencode.json, local template, and gitignore`

### Step C1 — Dual agents/AGENTS.md sync contracts [critic, verify, commit]

- **Files**: `.cursor/agents/AGENTS.md`; `.opencode/agents/AGENTS.md`
- **Change**: Document sync duty (behavioral parity; platform frontmatter may differ); list the nine agent stems; point at AGENTS ADR 0002.
- **Validate**: Both files exist and cross-reference each other.
- **Commit message**: `C1: add dual-home agent sync AGENTS.md files`

### Step D1 — Port nine OpenCode agents [critic, verify, commit]

- **Files**: `.opencode/agents/gim-*.md` (nine files)
- **Change**: Port contracts from `.cursor/agents/gim-*.md` into OpenCode markdown frontmatter (`description`, `mode`, `model`, `permission` including bash allowlists and `task` ACL). Bodies: same non-negotiables, skills to Read, output contracts; replace Cursor-specific spawn wording with OpenCode Task/`task_id` where needed (or “see HarnessToolMap”).
- **Validate**: Models match Q2 (`opencode models` verified: `deepseek/deepseek-v4-pro`, `deepseek/deepseek-v4-flash`); orchestrator `permission.task` includes `gim-critic` (and the other five spawn targets); builder task allowlist is **only** `gim-linter` + `gim-tester`; filename stems match `default_agent`; bash common list present on every agent.
- **Commit message**: `D1: add OpenCode gim-* agent definitions`

### Step E1 — HarnessToolMap in gim-orchestrate [critic, verify, commit]

- **Files**: `.agents/skills/gim-orchestrate/SKILL.md`; optionally tighten `.opencode/agents/gim-orchestrator.md` / `.cursor/agents/gim-orchestrator.md` cross-links
- **Change**: Insert top-level harness tool map; keep loop harness-neutral; note OpenCode primary vs Cursor skill-on-main.
- **Validate**: Map includes spawn + resume at minimum; no duplicated conflicting spawn instructions later in the skill.
- **Commit message**: `E1: add harness tool map to gim-orchestrate skill`

### Step F1 — Setup docs + pointers + out-of-scope cleanup [critic, verify, commit]

- **Files**: `AGENTS.md` (thin pointers + link); `docs/guides/0000-setup-opencode.md` (full OpenCode setup); `WORKSPACE.md`; `.agents/agents/gim-*.md` pointers; any “OpenCode out of scope” lines in 0001 plan footer notes only if needed for consistency (prefer AGENTS/WORKSPACE; do not rewrite all of 0001 history)
- **Change**: OpenCode setup guide (install/connect DeepSeek; copy template **or** own file; `OPENCODE_CONFIG`; multi-root workspace). Root `AGENTS.md` keeps a 1–2 line pointer only (every agent loads it). Pointers mention both canonical homes. Remove or replace “OpenCode port out of scope” in AGENTS/WORKSPACE.
- **Validate**: Setup guide does not name private/optional siblings beyond committed three; `OPENCODE_CONFIG` documented (not `OPENCODE_CONFIG_CONTENT`); `AGENTS.md` roster lines are folder pointers without enumerating every ADR/plan/agent example.
- **Commit message**: `F1: document OpenCode fleet setup and dual pointers`

### Step G1 — Inventory consistency [critic, verify, commit]

- **Files**: any stragglers from D1–F1
- **Change**: Confirm nine OpenCode agents + sync AGENTS.md pair; `opencode debug config` documented as optional verify; rg for stale “OpenCode out of scope” in active AGENTS/WORKSPACE.
- **Validate**: Checklist in this plan’s end-state inventory satisfied.
- **Commit message**: `G1: finalize OpenCode fleet inventory`

### Step Z1 — Autoload HarnessToolMap + wrap-up coherence [critic, verify, commit]

Wrap-up critic + mid-flight design: per-harness autoload for spawn/resume (agents should not need to Read `gim-orchestrate` only for the map).

- **Files** (expected):
  - `.cursor/rules/gim-harness-map.mdc` (new)
  - `.opencode/harness-map.md` (new)
  - `opencode.json` — add `instructions` entry for the OpenCode map
  - `.agents/skills/gim-orchestrate/SKILL.md` — pointer to autoload maps only (no skill crosswalk table)
  - `.agents/CONTEXT.md` — HarnessToolMap term notes autoload locations
  - Dual agent-folder `AGENTS.md` sync note for map twins
  - Wrap-up coherence (same step unless deferred by user): `.agents/skills/review-with-critic/SKILL.md` harness-neutral; `.agents/prompts/orchestrator-step-brief.md` dual builder contract paths; stale “E1” cruft in OpenCode builder body; plan Q4 `task "*"` text matches shipped orchestrator `ask`; CONTEXT explorer gloss mentions OpenCode parallel explorers
  - **VC bash vs body deny-list:** decision **B** (resolved Z1) — prompt-only + document; hard ACL deny for `rm`/`del`/`Remove-Item`/`git push --force*`; `ask` for consentable destructives; body deny-list still requires explicit user consent. Do not expand VC bash globs to mirror body deny-list.
- **Change**: Autoload Cursor column via rules; OpenCode column via `instructions`. Product loop stays in shared skill. Fix wrap-up coherence items agreed above.
- **Validate**: Cursor rule + OpenCode instructions wired; each always-on file is single-harness (no full dual table duplicated); skill does not leave conflicting spawn/resume as sole Cursor-only truth; smoke that OpenCode `opencode debug config` / session still healthy with `instructions` path.
- **Commit message**: `Z1: autoload per-harness tool maps and wrap-up coherence fixes`

---

## Expected end-state tree (additions)

```text
opencode.json                 # includes instructions → .opencode/harness-map.md
opencode.local.template.jsonc
opencode.local.json          # gitignored; not in repo
.opencode/
  harness-map.md             # OpenCode-only spawn/resume (autoload)
  agents/
    AGENTS.md
    gim-*.md
.cursor/
  rules/
    gim-harness-map.mdc      # Cursor-only spawn/resume (autoload)
  agents/
    AGENTS.md
    gim-*.md
docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md
docs/guides/0000-setup-opencode.md
docs/plans/agents/0002-AGENTS-gim-opencode-fleet.plan.md
```

---

## Out of scope

- Expanding bash allowlists for `dotnet` / `npm` / `semble` / ESLint (defer).
- Debugger instrumentation server.
- Disabling built-in OpenCode `build`/`plan`.
- Committing machine-local OpenCode config or documenting private repo paths.
- Changing Cursor model pins (remain grok/composer until a separate change).
- Replacing the shared prefs system.
- Putting the full dual-column HarnessToolMap into root `AGENTS.md` or other always-on mega-dumps.

---

### Step Z2 — Wrap-up critic agrees (spawn wording) [critic, verify, commit]

User disposition after wrap-up critic (post-Z1):

- **Agree:** `gim-orchestrate` fleet roster still leaks `subagent_type`; strip to role names only.
- **Agree:** `build-plan-with-steps` + `agentic-planning` still say Task `subagent_type: gim-critic` — make harness-neutral (`gim-critic` by name; never resume).
- **Reject:** add private workspace files to `.gitignore` — handled on a separate branch/PR; do not commit them here.
- **Leave:** root `AGENTS.md` thickness (was Ask, not Agree).
- **Reject (already):** CONTEXT path dump / explorer verbose gloss.

- **Files**: `.agents/skills/gim-orchestrate/SKILL.md`; `.agents/prompts/build-plan-with-steps.md`; `.agents/skills/agentic-planning/SKILL.md`; this plan (Z2 todo → completed).
- **Change**: Light wording only — no ACL, no CONTEXT fattening, no private gitignore.
- **Validate**: No leftover `subagent_type` in orchestrate roster; critic invoke lines name `gim-critic` without Cursor-only Task syntax.
- **Commit message**: `Z2: harness-neutral critic spawn wording after wrap-up`

---

## Review

After Z2, if needed re-check wrap-up weight: autoload maps vs skill loop; no conflicting spawn docs; sync duty for map twins. Final wrap-up already ran for A1–Z1; Z2 is narrow wording — per-step critic on Z2 only, then end PR.

---

## Conversation provenance

- Designed via `/gim-orchestrate` + grill-with-docs on branch off Cursor fleet.
- Locked Q1–Q11 as summarized above.
- Explorers: OpenCode agents/config/permissions; Cursor fleet inventory; local-config DIY (`OPENCODE_CONFIG` — no first-class `opencode.local.json` auto-load; upstream #17232).
- Tracker: GIM-121 fetched and reused.
- Handoff section added in-plan so a cold executor need not read the design chat.
