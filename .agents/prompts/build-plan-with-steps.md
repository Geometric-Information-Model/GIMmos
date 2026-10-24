# Build plan with steps

**Usage**: `@build-plan-with-steps.md <path/to/plan.md> [optional: steps to build, e.g. "steps 1-4" or "step A"]`

Implement the attached plan. If the user references specific steps (e.g. "step A1-A3" or "step B"), then **ONLY implement those steps**. Do **NOT** edit the plan file itself other than marking implemented steps complete.

---

## Todos

If the host created tracked todos for the plan, update their status as you work (`in_progress` when starting, `complete` when done). Otherwise track progress in your reply. Do not re-create todos that already exist.

## Read the plan's `## Execution mode` section first

The plan declares which per-step modes are active (verify / commit / PR / critic-step). **Honor them strictly.** If the section is missing or the modes are unclear, ASK the user before proceeding — do not guess.

---

## Phases (skip any phase whose mode isn't opted in)

Between each step (and only between steps), run the applicable phases **in this order**. Verify always precedes commit/PR. Critic precedes verify.

0. **Verify repo state** — area-scoped checks on touched paths: read each touched repo's **`AGENTS.md`** for build/test commands.
   - **Always** run between steps when ANY per-step mode is on.
   - With NO per-step modes active, run occasionally as a lightweight sanity check after substantive edits.

1. **Run critic review** (critic-per-step ON) — invoke **gim-critic** (fresh; never resume) per [`.agents/skills/review-with-critic/SKILL.md`](../skills/review-with-critic/SKILL.md). **Pass `Steps implemented: <stepId>`** (e.g. `A1`) so the critic scopes plan-comparison to that step only and doesn't false-positive on un-built steps.

2. **Verify with user** (verify ON) — when `verify_per_step` is true, this is **the most important gate in the run**: no commit, push, PR, plan status update, or next step before an explicit affirmative such as `looks good` or `verified`. Silence, "ok", a question, or a discussion point is **not** an affirmative. Skipping the gate is a **CRITICAL FAILURE**. Post a brief one/two-line summary ("implemented MarkCurveAsSeam on GimData") + a numbered test recipe ("1. build GIMtransfer; 2. run TestPlatform; 3. confirm seam index updates"). Wait for the explicit affirmative; do not treat the reply as optional. **Z-steps always verify** with this same stern rule even when `verify_per_step` is false. **Autopilot exception:** when the user explicitly invoked `gim-autopilot`, the per-step verify gate is deferred to the run end and the consolidated final report is the gate; the override does not affect other gates. Outside that explicit mode, the stern rule is absolute.

3. **Commit changes** (commit-per-step OR PR-per-step ON) — message format: `<stepId>: <lowercase description>` (e.g. `A1: added MarkCurveAsSeam to GimData`).

4. **Open PR** (PR-per-step ON) — chained leaf-safe branch per [`.agents/skills/git-branching-workflow/SKILL.md`](../skills/git-branching-workflow/SKILL.md) (step's branch = `<name>/<stepid>-<title>`, e.g. `seam-edges/a1-mark-curve-seam`; `--base <previous-task-branch>`). See [`.agents/skills/github-cli-workflow/SKILL.md`](../skills/github-cli-workflow/SKILL.md) § 4 for the chained `gh pr create` invocation.

---

## Critical Failure scenarios (stern)

**ANY** of the following counts as a FULL implementation FAILURE. Fail loudly and stop; do not silently move on.

- Agent moves to step N+1 while the repo is broken after step N (cannot build/test/run documented checks).
- (Verify ON) Agent commits, PRs, marks the step complete, or advances to step N+1 before the explicit affirmative. **Autopilot qualifier:** when the user explicitly invoked `gim-autopilot`, the per-step verify gate defers to the run end and the consolidated final report is the gate; the override affects no other gate.
- (Verify ON) Agent treats silence, "ok", a question, or a discussion point as the affirmative and proceeds.
- (Verify ON) Agent skips the verify gate; when `verify_per_step` is true it is the most important gate in the run, and Z-steps keep it even when the pref is false.
- (Commit-per-step ON) Agent moves to step N+1 without committing the changes from step N.
- (Commit-per-step ON) Agent commits step N while the repo is broken.
- (PR-per-step ON) Agent moves to step N+1 without opening a PR for the changes from step N.
- (Critic-per-step ON) Agent moves to step N+1 without invoking gim-critic on step N.
- (Critic + Commit/PR ON) Agent commits or PRs before running the critic.
- The order of operations (phase 0 → 1 → 2 → 3 → 4) is broken (e.g. commit before verify, PR before critic).
- (Todo Subset specified) Agent implements steps the user did not request.

If ANY of the above happens during execution, the **whole implementation counts as a failure** — surface this to the user immediately and stop.

---

## When the user references only a subset of steps

If invoked as `@build-plan-with-steps.md plan.md steps A1-A3`:

- Implement ONLY A1, A2, A3. Do not implement A4+ or any B/C steps.
- After completing the referenced steps, **invoke gim-critic one final time** (if critic mode is on; fresh instance — never resume) with `Steps implemented: A1, A2, A3` so it scopes plan-comparison to those steps and doesn't flag un-built steps as "missing functionality".
- Mark only A1-A3 as complete in the plan; leave all other steps untouched.
- Do not auto-continue to A4+ — stop and report.
