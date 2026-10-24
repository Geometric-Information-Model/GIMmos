---
name: review-with-critic
description: >
  Post-implementation review hook. Invokes gim-critic to review modified files against the plan and
  the repo's coding-practices docs. Use only when the user explicitly says
  "/review-with-critic", "/gim-critic", "use critic", "run critic", "call the critic",
  "review with critic", "critic-review", or asks to verify completed work
  with the critic. Do NOT auto-activate.
---

# Review with Critic

Invokes **gim-critic** to review completed implementation work. Spawn fresh each time; never resume critic. **Invocation-only** — never activate this skill on your own; only when the user explicitly says one of the trigger phrases in the description.

## Activation

User invocation syntax:

```
/review-with-critic [end|granular|step|repo]
/gim-critic [end|granular|step|repo]
```

Both triggers accept the same optional suffix.

The optional suffix selects granularity:

| Suffix              | Meaning                                                                                                                                                |
| :------------------ | :----------------------------------------------------------------------------------------------------------------------------------------------------- |
| (none) / `end`      | **Default**. One critic invocation at the end of the plan or work.                                                                                     |
| `granular` / `step` | One critic invocation after each step of the plan **plus** one final wrap-up invocation at the end (N+1 total). Per-step passes catch in-step issues; the wrap-up pass catches cross-step coherence issues. |
| `repo`              | One critic invocation per touched repo, run in parallel if the host supports it. Each gets the **full** file list + plan, but coding-practices docs scoped to that repo. |

If the user invokes the skill without a suffix (e.g. "use critic"), default to `end`.

## Doc resolution (per touched repo)

Before invoking the critic, resolve coding-practices references for each repo
touched by modified file paths. Map paths to repo roots per [`WORKSPACE.md`](../../WORKSPACE.md).

For each touched repo (in order):

1. `docs/coding-practices.md`
2. Other `docs/*.md` in that repo (e.g. `architecture.md`, `viewer.md`, …)
3. `AGENTS.md` at repo root
4. Nested `AGENTS.md` if the file is under a documented sub-scope (e.g. `GIMdeploy/backend/`)

Pass the resolved paths in the critic prompt under `Coding-practices references:`.

## Invocation contexts

Three distinct contexts in which this skill is activated. Behavior differs.

### 1. Post-plan execution (canonical)

The actor has just finished executing a plan. Procedure:

1. Identify the plan file path.
2. Identify every file touched across the plan (or per step, for `granular`). Include trivial edits (a single comma counts).
3. Identify any commit hashes made during execution.
4. Resolve doc paths per repo (see above).
5. Invoke **gim-critic** (never resume; never generic `explore` / `generalPurpose`) with the prompt below.

### 2. Ad-hoc post-implementation (no plan)

The user invokes the skill on completed work that was not driven by a plan file. Procedure:

1. Identify recently modified files: `git status` + `git diff` for uncommitted; `git log <base>..HEAD` for recent commits on the current branch.
2. Resolve doc paths per repo.
3. Pass that file list and any commit hashes to the critic.
4. In place of the plan file, pass a 1-2 sentence summary of what was done (the actor wrote the code; the actor knows the intent).

### 3. Plan-mode authoring

The actor is currently **authoring** a plan, not executing one. Do **not** call gim-critic (no code exists yet). For all three granularities, inject a trailing `## Review` section at the end of the plan. The body varies per granularity:

```markdown
## Review

After execution, run `/review-with-critic <suffix>` to review the implementation.
<Granularity-specific body — see below.>
```

- **`end`** — "Invoke `/review-with-critic end` once after the final step. Pass the plan path and the complete list of files modified across all steps, plus any commit hashes."

- **`repo`** — "Invoke `/review-with-critic repo` once after the final step. Split by touched repo and call the critic once per repo (may run in parallel if the host supports it); each call receives the **full** file list and full plan, but coding-practices docs scoped to that repo."

- **`granular` / `step`** — "Invoke `/review-with-critic granular` once more after the last per-step critic pass, with the **full** modified file list and `Steps implemented: <all step IDs>`. This wrap-up pass weights findings toward cross-step coherence (e.g. step A1 added a util that step A4 stopped using)."

For `granular` / `step` **additionally** integrate critic review into the plan's per-step gating per [`agentic-planning`](../agentic-planning/SKILL.md):

1. **Mark mode active** in the plan's `## Execution mode` section:

   ```markdown
   ## Execution mode

   - [x] Critic per step
   ```

2. **Mirror the mode in each step's heading tag**:

   ```markdown
   ### Step A1 — create user API service [critic, …]
   ```

3. **Add the failure scenario** to the plan's `## Failure scenarios` section: "(critic-per-step ON) Agent moves to step N+1 without running gim-critic on step N."

At execution time under granular, the actor invokes **gim-critic** between steps (passing only that step's touched files plus `Steps implemented: <stepId>`) and once more at the end as the wrap-up pass described in `## Review`.

## Prompt template the actor sends to the critic

The actor always sends a prompt with this shape (adjust per context):

```
You are reviewing a completed implementation. Do not modify any files,
do not fix issues, do not run state-changing commands.

Plan file: <path/to/plan.md>
(or, if no plan file: "No plan file; this was ad-hoc work. Summary: <1-2 sentences>")

Steps implemented (optional): <comma-separated step IDs>
- Per-step pass: a single ID, e.g. "A1".
- Wrap-up / partial-plan pass: all IDs built, e.g. "A1, A2, A3, B1".
- Omit (or pass "all") for whole-plan review of every step.

Modified files (read each in full):
- <path/to/file1>
- <path/to/file2>
... (every file, even trivial edits)

Commits (use `git show <hash>`):
- <hash1>
- <hash2>
(or: "All changes are uncommitted; use `git diff` and `git status`")

Repos in scope: <GIMmos, GIMfront, …>
- Full review: list every repo touched by modified paths.
- Repo-scoped pass (`repo` mode): one repo per invocation.

Coding-practices references:
- GIMfront/docs/README.md
- GIMfront/AGENTS.md
- GIMmos/AGENTS.md
... (resolved paths per repo — see Doc resolution above)

Return findings per the gim-critic output format (severity headers,
inline [category] tags, terse, file:line refs).
```

Rules for filling in the template:

- **Always pass every modified file**, even if the modification was a single comma. The critic decides what is significant.
- **Always pass every commit hash** if any commits were made during the work. Partial lists let bugs slip through.
- **Pass `Steps implemented`** whenever the actor built only a subset of the plan — per-step gates (granular mode), partial-plan execution via [`.agents/prompts/build-plan-with-steps.md`](../../prompts/build-plan-with-steps.md), or the wrap-up pass after granular runs. Omitting it tells the critic to compare against the whole plan, which false-positives "missing functionality" on un-built steps.
- **Optional terse mode**: add `/caveman full` only when the user explicitly requests terse critic output.

## After the critic returns — disposition protocol

For each bullet the critic returns, the actor takes one of these actions:

- **Agree** → fix the issue. Applies even to nitpicks (per spec).
- **Unsure** → ask the user, offering a recommendation.
- **Reject** (certain the critic is wrong) → do not fix; inform the user of the rejection with a one-line reason.
- **Partial agreement** (the issue is real but the critic's suggested fix is wrong) → **always ask the user** before fixing your own way.

After processing all bullets, emit a `## Critic disposition` summary ledger:

```markdown
## Critic disposition

Fixed:
- [security] Unescaped user input in `GIMfront/src/foo.jsx:42`.
- [correctness] Index out of range in `GIMmos/GIMtransfer/Bar.cs:88`.

Asked user:
- [performance] N+1 query in `CoreWebApi/GIM.WebAPI/Controllers/Baz.cs:120` — recommend fix; uncertain whether the optimization is worth the readability cost.

Rejected:
- [code-practices] Suggested rename of `getUser` — already canonical per `GIMfront/docs/README.md`.
```

If the critic returned no issues, no ledger is needed.

## Re-run loop

After applying any fixes from the critic, ask the user:

> Re-run the critic to verify the fixes?

Recommend **yes** (catches fix-introduced regressions). Skip this prompt entirely if the critic's first pass returned zero issues.

## What this skill does NOT do

- It does **not** auto-activate. The description enumerates explicit user trigger phrases only.
- It does **not** modify the plan file at execution time. (Plan-mode authoring is the only context where this skill writes to the plan, and only to inject the Review section.)
- It does **not** run the gim-critic agent itself — the actor spawns **gim-critic** (harness-native; never resume).
