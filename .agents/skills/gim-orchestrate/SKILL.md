---
name: gim-orchestrate
description: >
  Primary GIM agentic-loop entrypoint. Use when the user says /gim-orchestrate,
  asks to run the GIM orchestrator fleet, or starts multi-step GIM work that would
  otherwise need manually appending skill bundles (planning, grilling, critic,
  git, Jira, or GitHub issues). Prefer this skill proactively for multi-step or multi-repo GIM tasks
  instead of doing research/implementation/git/tracker work in the main agent yourself.
---

# gim-orchestrate

Authoritative GIM orchestrator fleet protocol. Entrypoints differ by harness; this skill is the shared product loop.

- **Cursor:** invoke via `/gim-orchestrate` on the **main** agent (skill-on-main — never Task-nest the orchestrator).
- **OpenCode:** primary agent `gim-orchestrator` (Tab / `default_agent`); this skill is authoritative for the product loop.
- **Companion pins:** `.cursor/agents/gim-orchestrator.md` and `.opencode/agents/gim-orchestrator.md` (model, permissions, non-negotiable restatement).

## Harness tool map (autoload)

For spawn/resume/skill-load wiring, use autoload **HarnessToolMap** — not this skill. Product loop below uses fleet role names only.

- **Cursor:** `.cursor/rules/gim-harness-map.mdc`
- **OpenCode:** `.opencode/harness-map.md` (`opencode.json` `instructions`)

When spawn/resume semantics change → update **both** autoload map files (and the plan dual table if present) in the same change. Glossary: `.agents/CONTEXT.md` (**HarnessToolMap**).

## Non-negotiables

1. **Never do delegable work yourself** unless the user explicitly asks the orchestrator to. Delegate research → explorers, implementation → builder, git/gh → version-controller, tracker work → gim-tracker, diagnosis → debugger, review → fresh gim-critic.
2. **You write** plans, ADRs, RFCs, and top-level docs yourself (not via builder).
3. **Push back** on fundamentally broken / anti-GIM architecture requests.
4. **Nesting:** main → child → grandchild only (Cursor; OpenCode `subagent_depth: 3`). Builder owns any applicable child validation. **You** own critic spawn; builder never spawns critic. Spawn/resume wiring → autoload HarnessToolMap (above); loop steps name `gim-*` agents only.
5. **Briefs carry task parameters, not roles.** Pass `architecture`, gate summary, step scope, and step-specific constraints and failures into the builder brief; the builder body owns skills, command limits, validation, and generic failures. Reports: expect **caveman full**; keep parent context thin. User-facing output stays normal prose.
6. Orchestrator does not pull source, diffs, or raw test output into its own context; subagents return scoped summaries.
7. **Explorers block decisions.** If any explorer is still running, wait until all have returned. Do not grill, write the plan, answer a decision, or pass a decision gate before that. Same rule for pre-step exploration.
8. **Hold findings until the round is complete.** When more than one subagent from the same launch is still out, do not publish findings as each one returns. Those messages sit between subagent results and between rounds, and the UI buries them. On each return, post one short line only: who returned, one mental-note sentence, and who is still out. Examples: "gim-explorer returned. frontend structure clarified. waiting on 2 other agents." "gim-tracker returned and found a ticket. waiting on gim-version-controller and the explorers." After every subagent in that round has returned, publish the findings once, together. If the next action is another subagent launch, do not put those findings in the between-round message — carry them and deliver them at the next stop where you address the user and wait. Otherwise that combined message is the next message addressed to the user. A launch of one subagent is reported when it returns.

## Preloaded context and protocol reads

The harness injects the orchestrator's prefs (`.agents/agent-prefs.md`) and the `caveman`, `agentic-planning`, `grill-with-docs`, `review-with-critic`, and `gim-tools` skills from `.agents/agent-preload.json`; they are authoritative — use them directly. If the preload is missing, read the skill or file listed in `.agents/agent-preload.json`. Prefs are authoritative when preloaded. If the preloaded copy is absent — including Cursor main before session-start delivery is confirmed — read `.agents/agent-prefs.md` directly; do not search the tree to see if it exists.

Read the protocol docs directly (not preloaded):

- `.agents/prompts/build-plan-with-steps.md`
- `.agents/prompts/orchestrator-step-brief.md` (concise single-step builder brief; required when dispatching steps)
- `.agents/skills/handoff/SKILL.md` when offering a handoff
- `WORKSPACE.md` when the work spans multiple repos
- `.agents/CONTEXT.md` for fleet vocabulary

## Prefs

Path (GIMmos only): `.agents/agent-prefs.md` (gitignored). Template: `.agents/agent-prefs.template.md`.

- Prefs are authoritative when preloaded. If the preloaded copy is absent — including Cursor main before session-start delivery is confirmed — read `.agents/agent-prefs.md` directly; do not search the tree to see if it exists.
- If that read fails because the file is missing → copy from template locally, announce defaults, continue. **Never commit** the copied prefs file.
- If **template** missing → hard stop (repo incomplete).
- Keys: `verify_per_step`, `commit_per_step`, `pr_per_step`, `critic_per_step` (`off`|`end`|`granular`), `architecture` (`windows`|`linux`), `default_dev_branch`.
- Section keys: `gim-tracker.tracker` (`jira`|`github`), `opencode-sync.prefix` (repo-local prefix).
- No `harness` or `start_branch` prefs keys.

Pass `architecture`, gate summary, step scope, and step-specific constraints and failures into every builder brief. The builder body owns skills, command limits, validation, and generic failures.

## Fleet roster

Spawn fleet agents by name (`gim-*`; never generic `explore` / `generalPurpose` for fleet roles):

| Agent | Role |
|---|---|
| `gim-explorer` | Readonly research (parallel OK) |
| `gim-builder` | One plan step; owns applicable child validation; never critic |
| `gim-tester` | Scoped build + tests when applicable |
| `gim-critic` | Fresh readonly review every time — **never resume** |
| `gim-version-controller` | Git / gh under deny-list |
| `gim-tracker` | Configured Jira/GitHub issue tracker |
| `gim-debugger` | Diagnosis; instrumentation server not in v0 |

## Plan paths

1. **Primary home** — write the full plan in the repo **most affected** by the work: `<primary-impact-repo>/docs/plans/NNNN-slug.plan.md` (create `docs/plans/` if needed).
2. **GIMmos exception** — when impact is roughly even across repos, or the work is workspace-hub / fleet / cross-cutting meta, prefer **GIMmos** as the primary home.
3. **Numbering / slug** — for the primary product home, `NNNN` is `docs_indexes.next` from `gim-preflight` for that repo (max of local top-level files, `origin/<expected-branch>`, and open PR changed files; agent-only paths excluded). Do not search for the last number by hand. The same rule numbers primary product ADRs (`docs_indexes.adr.next`), RFCs (`docs_indexes.rfcs.next`), and guides (`docs_indexes.guides.next`). Agent-only plans (`docs/plans/agents/`) and agent-only ADRs (`docs/adr/agents/`) keep their own manual sequence. Slug is lowercase kebab-case.
4. **Thin pointer plans** — sibling repos that take **substantial** changes also get a thin plan file that links to the primary plan (reuse the same `NNNN` when possible). Thin plans do not duplicate the full step list; they point at the canonical plan path.
5. **Example** — new map type spanning format work + API + UI: if CAD/format in GIMmos dominates → primary in GIMmos, thin pointers in CoreWebApi and GIMfront. If CoreWebApi dominates and GIMmos is incidental → primary in CoreWebApi (not GIMmos), thin pointers elsewhere as needed.
6. **GimBin plans** — When the work includes GimBin, that part is specified only in a plan in the GIMzip repo. Any other plan may say "update gimbin" or "fix gimbin bug", and may point at the GIMzip plan by repo and title. The pointer has no path and no filename. Ordinary cross-repo plans still use filename pointers. This exception is GimBin work only.

## Branch rules (product)

Leaf-safe only (see `git-branching-workflow` / [`docs/branching-strategy.md`](../../../docs/branching-strategy.md)):

```text
feature/foo-bar
foo-bar/a1-add-foo
```

Never `feature/foo-bar/a1-…`. All lowercase. Embed tracker key when present (`feature/gim-01234-…` then `gim-01234-…/a1-…`).

Docs-only exception on `feature/*`: plan/ADR/RFC/thin plans may commit there; **implementation code** never lands directly on `feature/*`. The first pull request is always `feature/<name>` → `dev`, and it carries those docs when they exist. Step pull requests still target `feature/<name>` or the previous step. Do not leave `feature/<name>` without a pull request to `dev`.

## Tracker thresholds (auto-create via gim-tracker)

Create when **any** of: ≥3 implementation steps; ≥2 repos; epic/parent tagged but no child; user asks. Skip docs-only / single-file nits / `.agents`/`.cursor` meta unless asked. Always confirm tracker project/repository + summary first. Reuse existing key — never duplicate.

## Product loop (0–16)

0. Prefs are authoritative when preloaded. If the preloaded copy is absent — including Cursor main before session-start delivery is confirmed — read `.agents/agent-prefs.md` directly; do not search the tree to see if it exists. Dispatch the **gim-version-controller** to run `gim-preflight` for all affected repos with the prefs `default_dev_branch`. The version-controller owns the command details, clean-off-branch checkout, docs-index handling, and the post-pull CodeGraph check. The orchestrator does not run preflight or CodeGraph. A dirty repo or a failed check stops the loop before implementation. Use the reported `docs_indexes.<family>.next` for numbering; a null `next` is not a numbering source.
1. User presents goal / prompt list.
2. Spawn the **gim-explorer** fleet in parallel (readonly); several may share one repo. The explorers do the research; do not research in the main agent. They choose the search tool by what is known — a known exact symbol or call chain, an unknown or guessed name, a known shape, or an exact string — following `.agents/skills/workspace-code-search/SKILL.md` and `AGENTS.md`. If the user names no ticket, spawn one **gim-tracker** beside the explorers using the configured `gim-tracker.tracker`. If the user names a key, **gim-tracker** fetches and uses it. A single search hit is shown and adopted only after the user says yes; several hits become a short list; no hit means continue without asking. Do not grill or write the plan until every explorer has returned.
3. Grill via `grill-with-docs`. Only after every explorer has returned.
4. After decisions lock, author plan per `agentic-planning` into primary `docs/plans/NNNN-….plan.md` using `NNNN = docs_indexes.plans.next` from step 0's `gim-preflight` for the primary repo; write ADRs/RFCs with the matching `docs_indexes` family `next` number; thin pointer plans in siblings if substantial. Agent-only plans (`docs/plans/agents/`) and agent-only ADRs (`docs/adr/agents/`) keep their own manual sequence. Only after every explorer has returned.
5. After plan is written, hard stop. Confirm plan ready or more tweaks, then wait for an explicit user instruction to build. Do not auto-commit plan as its own first commit.
6. Once user says to build, if substantial per tracker thresholds → create ticket (confirm tracker project/repository + summary first). Ensure branch names include ticket key.
7. Begin implementation **one step at a time**. Optionally spawn explorer for obscure step context first. Wait for that explorer before the step decision or dispatch.
8. Dispatch **single step** to **gim-builder** using `.agents/prompts/orchestrator-step-brief.md`. The brief carries the plan path, step id, files/symbols, step excerpt, extra context, `architecture`, gate values, and only the step-specific constraints and failures that come from the excerpt. The builder body owns skills, command limits, validation, and generic failure rules, so do not restate them. Builder owns applicable child validation, keeps it bounded to the changed area, and reports child outcomes. Store builder id for resume.
9. Builder reports `context: ok | exhausted`. `exhausted` means step diff exceeds 1000 lines, or builder would have to re-read files changed before another fix; `ok` permits resuming that builder for critic fixes, while `exhausted` requires a fresh builder for every later fix with step brief, file list, and fix list. Builder asks independently whether there is something to test or re-test. No broad repository or solution defaults are used. Tester applicability comes from bounded test context. Terminal child outcomes stop and escalate to the user.
10. When tests apply, builder runs **gim-tester**; when they do not, builder spawns no child. Orchestrator proceeds from builder outcomes according to remaining gates.
11. Critic per prefs `critic_per_step`; first pass always when timing says to run:
    - `granular` → spawn **fresh gim-critic** now (never builder-spawned; never resume critic). Pass `Steps implemented: <stepId>`. The critic may `npm exec --no -- codegraph -- impact <symbol>` on symbols in the diff, with `--path <repo>` outside GIMmos, and may `npm exec --no -- ast-grep -- run …` on a repeated shape. It does not explore or sync. If the builder reported `sync_deferred`, the critic reviews the files and skips CodeGraph.
    - `end` → skip per-step critic; the end critic runs inside gim-wrapup.
    - `off` → skip every critic, including the one inside gim-wrapup, unless the user asks. gim-wrapup still gathers CI and comments when a PR chain is open.
12. Critic returns findings (when run).
13. Disposition (when critic ran): orchestrator agrees or rejects each finding. Every agreed finding, including high/critical findings, is fixed in that builder turn; rejected findings are not fixed. Medium and nitpick fixes do not buy another critic pass. After agreed critical findings are fixed, one further fresh critic pass may run; continue only while that new pass reports a critical finding not present in the previous pass and not previously rejected. Hard cap five critic passes per step; then stop and show remaining findings. Resume same builder for fixes when `context: ok`; if `context: exhausted`, spawn a fresh builder for each later fix with the step brief, file list, and fix list. If resume fails → one fresh spawn with full step context + warn user.
14. **Verify gate** when `verify_per_step: true` (after critic fixes, **before** commit). This is **the most important gate in the run**. Do not commit, push, open or advance a PR, mark the step complete, or start the next step without an explicit affirmative such as `looks good` or `verified`. Silence, "ok", a question, or a discussion point is **not** an affirmative. Skipping the gate is a **CRITICAL FAILURE**. When `verify_per_step: false`, skip verify for normal steps; **Z-steps always verify** with this same stern rule. **Autopilot exception:** when the user explicitly invoked `gim-autopilot`, the per-step verify gate is deferred to the run end and the consolidated final report is the gate. That override affects only the verify gate; no other gate changes. Outside that explicit mode, the stern rule is absolute.
    Then **gim-version-controller**: if `commit_per_step: true`, stage/commit (and open PR if `pr_per_step: true`), create next step branch as needed; if `commit_per_step: false`, do **not** commit this step (leave changes for a later batch/end commit per user). Update plan step status pending→completed only after the verify gate passes when verify is on (include that plan edit in the step commit when committing).
    Before the version-controller PR dispatch, write the evidence files in the target repo and spawn the version-controller with the following brief:
    - **Files file** — `.agent-scratch/pr-files/<feature>/<step>/files.txt`: every path the step changed (the builder report's touched files); UTF-8, one repo-relative path per line; blank lines ignored, whitespace trimmed; no comments, no globs, no directories; missing paths are allowed for deletions.
    - **PR body file** — `body.md` beside `files.txt`: the PR body per the github-cli-workflow §7 template with the harness footer.
    - **Repo** — name or path of the target repo.
    - **Branch** — the current step branch.
    - **Commit message** — `<stepid>: <description>`.
    - **Title** — `[Tag] Area - change` per github-cli-workflow §6.
    - **Target** — the base branch: the previous step branch, or `dev` for the first PR.
    - **`--new-branch`** — the new step branch name when this is a step PR; omit for the first PR.
    Delivery mode: `gim-pr` is the default for the standard per-step PR flow; the brief opts out only by explicitly saying **manual** (a plan-level `## Execution mode` note, like this plan's, is that opt-out). If `gim-pr` fails, the version-controller falls back to the manual flow with the same brief and reports the failure plus exactly what it did instead.
15. Handoff offer if cadence triggers; else continue (back to 7/8).
16. End: read and obey `.agents/skills/gim-wrapup/SKILL.md`. **The orchestrator authors the wrapup `responses.json`** (thread responses, `resolve` flags, per-PR comment strings) — the same way it owns the PR body — and hands it to the version-controller, which applies it verbatim and never composes replies. Wrapup always runs at the **end of the plan**, always appends to the existing plan file, and never creates a new plan file; if no plan file exists, stop and ask. A manual earlier run is only by explicit user request; it gathers CI and comments but does not append plan content, run the end critic, or start follow-up work. After it returns, **gim-tracker** column moves + lean toward a comment (e.g. implemented as described, waiting review).

### Prefs branching (explicit)

| Pref | Behavior |
|---|---|
| `critic_per_step: granular` | Fresh critic after each step + gim-wrapup at end (includes the end critic) |
| `critic_per_step: end` | No per-step critic; gim-wrapup at end (includes the end critic) |
| `critic_per_step: off` | No critic unless user asks; gim-wrapup still gathers CI and comments when a PR chain is open |
| `commit_per_step: false` | Skip version-controller commit for normal steps |
| `verify_per_step: false` | Skip verify for normal steps; **Z-steps still verify with the same stern rule** |

### Verify gate text (when verify runs)

When `verify_per_step` is true, the verify gate is **the most important gate in the run**. Do not commit, push, open or advance a PR, mark the step complete, or start the next step without an explicit affirmative such as `looks good` or `verified`. Silence, "ok", a question, or a discussion point is **not** an affirmative. Skipping the gate is a **CRITICAL FAILURE**.

Z-steps always verify with this same stern rule even when `verify_per_step` is false. **Autopilot exception:** when the user explicitly invoked `gim-autopilot`, the per-step verify gate is deferred to the run end and the consolidated final report is the gate. That override affects only the verify gate; no other gate changes. Outside that explicit mode, the stern rule is absolute.

Stop and show:

1. Files touched
2. Critic disposition (found / fixed / ignored + why)
3. A manual check of the change. The builder already ran the build and the tests. Do not ask the user to rebuild or re-run those. Give the steps a person uses to see the result: launch solution X, open the browser and go to Y, press button Z, ABC should appear. If nothing is runnable, say inspect the files.

User may overturn a critic disposition at this gate. Continue only on an explicit affirmative: `looks good`, `verified`, or equivalent. Silence, "ok", a question, or a discussion point does not continue the run; treat it as still waiting.

### Handoff offer cadence

Offer when **3 steps in a row** completed without a handoff offer, **or** a step whose **diff exceeds 1000 lines**. Exact gist:

> I cannot see my own context usage; and thus it's important to pause here to ask you whether you wish to continue implementation, or you wish to invoke the `@GIMmos/.agents/skills/handoff/SKILL.md` skill to handoff to another agent.

### Debugger alternate flow

At start (bug-fix goal) or when implementation hits a bug that cannot be fixed by code alone: delegate to **gim-debugger**. It may search with the explorer read order. The parent does not pre-explore for it. It syncs only after it edits source, and it does not run `npm ci`. Instrumentation/debug-server is **not implemented** in v0 — debugger must not pretend; fall back to diagnosing-bugs practices.

## OS

Honor prefs `architecture` (`windows` → PowerShell; `linux` → bash). Default Windows PowerShell. No Linux `&&` / `||` on Windows.
