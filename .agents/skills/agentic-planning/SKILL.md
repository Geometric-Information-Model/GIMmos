---
name: agentic-planning
description: Discipline for authoring and executing multi-step coding plans — coherent steps that leave the repo valid after each step, right-sized granularity, self-contained context for any executor, alphanumeric step naming, and four orthogonal opt-in per-step modes (verify with user, commit, PR, critic review). Use for any task that decomposes into 3+ non-trivial steps, multi-file refactors, migrations, scaffolding work, or executing a plan written by someone else. Activation phrases - commit-per-step on "commit" + "step"/"each"; PR-per-step on "PR per step", "PR each step", "one PR per step", "chain PRs"; verify-per-step on "verify" + "step"/"each", "stop and verify", "verify with me", "confirm each step"; critic-per-step via `/review-with-critic granular|step`. If ambiguous, ask via multi-select.
---

# Agentic Planning

Governs how to author and execute multi-step coding plans (3+ non-trivial steps, multi-file refactors, anything you might hand off). Applies whether you author the plan, execute your own, or execute one handed to you.

## Core principles

- **Coherence**: repo must be valid (compiles and tests pass) after every step. Never split mutually dependent changes across steps.
- **Right-sized granularity**: a step is too broad if it touches dozens of unrelated files; too granular if validating it adds no signal. Aim for steps you can validate independently.
- **Self-contained context**: write every step assuming a different agent will execute it cold. Include file paths, symbols, the _why_, and the verification command.
- **Opt-in per-step modes**: verify, commit, PR, critic — all OFF by default. Default behavior is one PR per plan, batched commits, single critic at end (see `github-cli-workflow`, `review-with-critic`).
- **Destructive-command consent**: never run history-rewriting or work-discarding git commands without explicit consent.

---

## Authoring a plan

### Step coherence — repo must be valid after each step

Each step's diff must apply on its own without breaking build, type-check, or tests. If two changes only make sense together, they belong in the same step.

**Bad** (repo broken between steps): (1) rename `getUser` → `fetchUser` in `services/userService.ts`; (2) update the 14 call sites.

**Good** (atomic): (1) rename `getUser` → `fetchUser` in `services/userService.ts` AND update all call sites in `routes/`, `components/`, `hooks/`.

### Right-sized granularity

Steps small enough to validate, large enough that validation is meaningful.

- **Too broad**: "Migrate the auth subsystem to the new session provider" (30+ files).
- **Too granular**: "Add `import {sessionProvider}`" → "Replace the body of `requireAuth`" (between steps, file doesn't compile).
- **Right-sized**: "In `auth/middleware.ts`, replace `verifyJwt` inside `requireAuth` with `sessionProvider.verify`; update `auth/middleware.test.ts` mock".

Rule of thumb: after applying the step, you can run one validation command (build, typecheck, focused test) and get a meaningful pass/fail.

### Self-contained context

Assume the executor has not seen this conversation. Each step states: **files/symbols** touched; **the change** in 1-2 sentences; **why**; **preconditions** (env vars, prior steps); **how to verify**. Restate any decision made earlier in the conversation that the step depends on.

### Alphanumeric step naming

- **Single logical domain** (one feature end-to-end): plain numbers `1, 2, 3, …`.
- **2+ logical groups** (related but distinct topics): per-group letters `A1, A2, …, B1, B2, …`. No mixing (don't have `1, 2, B1, B2`).

**Single-domain example** (build the user API end-to-end):

> 1. Add `User` model and migration.
> 2. Add `UserService` and unit tests.
> 3. Add `/users` routes.
> 4. Add frontend `UsersPage`.

**Multi-group example** (three sibling APIs in one plan):

> A1. Add `User` model. A2. Add `UserService`. A3. Add `/users` routes.
> B1. Add `Group` model. B2. Add `GroupService`. B3. Add `/groups` routes.
> C1. Add `Permission` model. C2. Add `PermissionService`. C3. Add `/permissions` routes.

The step ID is referenced from commit messages and the critic's `Steps implemented` input (see `review-with-critic`).

### Plan sizing nudge

If the plan reaches **5+ steps** OR uses the **letter scheme** (multi-group), the plan-authoring agent must emit a one-line suggestion to enable PR-per-step:

> "This plan has 7 steps across 2 groups. Enable PR-per-step so each chunk gets reviewed independently? (Y/n)"

Single-tap accept. No lecture, no blocking — if the user declines, proceed with the default.

### Execution mode declaration

Every plan declares its active per-step modes near the top, in a `## Execution mode` section:

```markdown
## Execution mode

- [x] Verify per step
- [ ] Commit per step
- [x] PR per step (implies commit)
- [x] Critic per step
```

Mirror the active modes inline in each step's heading as a bracketed tag:

```markdown
### Step A1 — create user API service [critic, verify, PR]
```

If zero modes are checked, omit the `## Execution mode` section entirely (defaults apply).

### Failure scenarios

When any per-step mode is active, add a consolidated `## Failure scenarios` section right after `## Execution mode`. Enumerate only scenarios relevant to active modes (templates):

- (any per-step mode) Agent moves to step N+1 while repo is broken after step N.
- (verify ON) Agent moves to step N+1 without verifying step N with the user.
- (verify ON) Agent commits/PRs step N without verifying first.
- (commit-per-step ON) Agent moves to step N+1 without committing step N.
- (commit-per-step ON) Agent commits step N while repo is broken.
- (PR-per-step ON) Agent moves to step N+1 without opening a PR for step N.
- (critic-per-step ON) Agent moves to step N+1 without running critic on step N.
- Order of operations broken (e.g. commit before verify, PR before critic).

### Recommended step template

```markdown
### Step <id> — <title> [<mode tags>]

- **Files / symbols**: `path/to/file.ts` (`functionName`), `path/to/other.tsx`
- **Change**: <one or two sentences describing the diff and the rationale>
- **Validate**: `<command>`
- **Commit message** (if commit/PR mode opted in): `<stepId>: <description>`
```

Omit fields that genuinely don't apply (e.g. no validation command for docs-only).

---

## Per-step modes

Four orthogonal opt-in modes. PR-per-step implies commit-per-step (the PR contains the commit). All four off by default.

| Mode            | Activation phrases                                                                     | What it gates between every step                                                |
| :-------------- | :------------------------------------------------------------------------------------- | :------------------------------------------------------------------------------ |
| Verify per step | `verify` + `step`/`each`, `stop and verify`, `verify with me`, `confirm each step`     | One-line summary + numbered test recipe; user can reply "looks good"            |
| Commit per step | `commit` + `step`/`each`, `commit after every step`, `commit as you go`, `auto-commit` | One commit per step; local only by default                                      |
| PR per step     | `PR per step`, `PR each step`, `one PR per step`, `chain PRs`                          | One PR per step on a chained branch; see `github-cli-workflow` §4               |
| Critic per step | `/review-with-critic granular`, `/review-with-critic step`                             | **`gim-critic`** (fresh; never resume) reviews each step + one final wrap-up; see `review-with-critic` |

If the user's intent is ambiguous (e.g. says "verify" without "step"), ask via multi-select listing all four modes before proceeding.

### Order of operations (phases 0–4)

Between each step, run phases in this order. **Skip any phase whose mode isn't opted in.** Verify always precedes commit/PR; critic precedes verify.

0. **Verify repo state** — area-scoped checks on touched paths: read each touched repo's **`AGENTS.md`** for build/test commands. Runs between every step when any per-step mode is on. With no per-step modes, run lightweight occasional checks after substantive edits.
1. **Critic review** (critic-per-step ON) — invoke **`gim-critic`** (fresh; never resume) per `review-with-critic`, passing `Steps implemented: <stepId>`.
2. **Verify with user** (verify ON) — post a one/two-line summary ("implemented user API service") + numbered test recipe ("1. open the app; 2. visit `/users`; 3. confirm the list renders"). Accept "looks good" as confirmation.
3. **Commit** (commit/PR mode ON) — see _Commit message guidance_ below.
4. **Open PR** (PR-per-step ON) — chained branch per `github-cli-workflow` §4 and `git-branching-workflow`.

---

## Executing a plan

### Validate after every step

After applying each step, run its validation command(s). Do not advance on a failing validation — fix it first, even if that means amending the step's plan entry.

For repo-specific build/test commands, read each touched repo's **`AGENTS.md`**.

### First-commit / first-PR confirmation gate

Before the **first** commit (or PR) of the plan, confirm once with the user:

- **Branch**: which branch receives the commits/PRs? (See branch guard below.)
- **Push?**: per-step commits stay local by default. PR-per-step pushes each step's branch.
- **Excludes**: any tracked-but-uncommitted files that should never ship (env files, scratch notes).

After confirmation, don't re-prompt for subsequent commits/PRs in the same plan execution. (PR-per-step still triggers the one-time PR-mode confirmation in `github-cli-workflow` §3 once per PR.)

### Branch guard

Refuse to commit if the current branch is `dev`, `main`, or matches `release/*`. Ask the user to switch to or create a task branch. See `git-branching-workflow`.

### Push default

Local commit only for commit-per-step. PR-per-step pushes each step's branch (required to open the PR). No `git push` in any other mode without explicit consent.

### Executing a handed-off plan

If the user hands you a plan file (e.g. `@build-plan-with-steps.md path/to/plan.md`), see [`.agents/prompts/build-plan-with-steps.md`](../../prompts/build-plan-with-steps.md) for the stern execution protocol.

---

## Commit message guidance

**Informative but concise** — 2-3 sentences at most, often one line. State what changed and, when useful, why.

**Per-step modes**: prepend the alphanumeric step ID:

- `A1: added user API service and validators`
- `3: extracted commit-message validation into shared service`

**No per-step mode**: free-form lowercase, no ID prefix:

- `added base template scaffolding for rapid_react tool`
- `fixed timezone drift in report aggregator by normalizing to UTC at ingest`

**Bad** (always): `fix`, `tweak`, `WIP`, `Final`, multi-paragraph wall of text. **Prefer** Lowercase first word, past or present tense, focused on the unit of change.

---

## Destructive-command guardrails

Never run any of the following without explicit consent:

- `git reset --hard` (any form)
- `git push --force`, `git push --force-with-lease`, `git push -f`
- `git rebase` (any flavor)
- `git branch -D`
- `git clean -fd` (or any `git clean` removing untracked files)
- `git checkout -- <file>` against unstaged changes
- `git commit --amend` on a pushed commit

**GitHub CLI** commands requiring the same consent rule (see `github-cli-workflow`):

- `gh pr merge` / `gh pr close` / `gh pr revert`
- `gh pr ready` (draft↔ready flip)
- `gh pr edit --base`
- `gh auth logout` / `gh auth refresh` / `gh auth token`
- `gh extension install` / `upgrade` / `remove`

**Fallback**: if a command may discard uncommitted work, rewrite shared history, or otherwise has destructive blast radius and isn't on the list, ask first.

### Reverting your own commits

If you want to undo commits you made, ask first. When the user consents:

- Prefer `git revert <commit>` — preserves history, safe on shared branches.
- Use `git reset --hard` only with explicit consent _and_ a stated reason (e.g. local-only commits to discard).
- Never `--amend` an already-pushed commit.
