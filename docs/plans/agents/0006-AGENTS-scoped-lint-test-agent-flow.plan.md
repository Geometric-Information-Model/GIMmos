# Scoped lint and test agent flow

> **Scope:** GIMtransfer fleet contracts only. Do not modify `GIMfront`,
> `CoreWebApi`, or `GIMmanager`; another agent may be changing those
> repositories concurrently.
>
> **Included pre-existing work:** preserve and include the current tracker
> diffs in the final PR, even though they are unrelated to this plan's
> linter/tester changes:
> `.agents/skills/atlassian-jira-cli/references/acli-reference.md`,
> `.cursor/agents/gim-tracker.md`, and `.opencode/agents/gim-tracker.md`.

## Goal

Prevent `gim-linter` and `gim-tester` from spending roundtrips on files or
checks that cannot provide useful signal, while preserving the current agent
identities, responsibilities, mandatory skill loads, and nesting rules.

## Locked decisions

- The builder owns child applicability and passes bounded scope to children.
- The linter handles only eligible source files; the tester handles only
  relevant tests. Neither child widens scope to repository defaults.
- Linter and tester applicability are independent decisions: “is there
  something to lint?” and “is there something to test or re-test?” are asked
  separately. If both apply, linter runs first for ordering, but a missing
  or ordinary failed linter does not suppress an independently relevant
  tester; terminal infrastructure conditions still stop the step.
- Validation retries are bounded per step: at most one repair cycle (no more
  than two linter and two tester invocations). Tool unavailability, setup
  failure, permission denial, timeout, or interruption is terminal and is not
  retried; the agent stops, reports the exact condition, and asks the user for
  a course of action with a recommendation.
- Detailed eligibility, discovery, output, and retry contracts live in the
  specialist agent definitions, not in this overview, glossary, or handoff
  prompt.

## Execution mode

- [x] Verify per step
- [x] Commit per step
- [x] PR per step — plan-local delivery override: do not open intermediate
  PRs; consolidate step commits into one final squashed PR
- [x] Critic per step

Step order is critic → verify with user → commit. The single final PR opens
only after all steps and wrap-up review are complete, with the step commits
squashed into that one PR. This explicitly overrides normal PR-per-step
delivery while keeping all per-step gates active.

## Failure scenarios

- Linter receives files outside builder-provided source scope.
- Tester runs tests outside builder-provided relevant scope.
- Builder spawns either child for a non-code-only step.
- Builder lets linter applicability or result suppress an independently
  relevant tester.
- Tester summarizes or truncates a failed test's framework output.
- Step advances without critic, user verification, or commit.
- An intermediate PR is opened, or the final PR is opened before all steps and
  wrap-up review are complete.
- Cursor and OpenCode behavioral contracts diverge.
- A child widens scope from builder-provided inputs to `git status`, the
  whole-worktree diff, or a repository-wide default.
- A child retries after a terminal infrastructure/setup/timeout/interruption
  result, exceeds the per-step validation budget, or fails to surface the
  condition and recommended next action to the user.
- Any change touches `GIMfront`, `CoreWebApi`, or `GIMmanager`.
- The three declared pre-existing tracker files are overwritten, reverted, or
  omitted from the final PR.

## Steps

### Step 1 — Define shared scope and reporting contract [critic, verify, commit] — completed

- **Files / symbols**:
  `.agents/CONTEXT.md`,
  `.agents/skills/gim-orchestrate/SKILL.md`,
  `.agents/prompts/orchestrator-step-brief.md`
- **Change**: Keep shared guidance at routing level: the builder decides
  independently whether linter and tester children apply, passes bounded
  scope, preserves ordering when both apply, and records explicit skips.
  Remove specialist implementation details from the
  orchestrator skill, glossary, and handoff prompt.
- **Validate**: Confirm shared docs contain only the routing lifecycle and do
  not duplicate specialist eligibility, discovery, or output schemas.

### Step 2 — Tighten Cursor agent contracts [critic, verify, commit] — completed

- **Files / symbols**:
  `.cursor/agents/gim-linter.md`,
  `.cursor/agents/gim-tester.md`,
  `.cursor/agents/gim-builder.md`
- **Change**: Put the eligibility rule, discovery, output, skip, and retry
  contracts in the specialist agent definitions without exhaustive file-type
  catalogs. Use representative source/config examples and let the specialist
  classify other extensions by role. Make builder emit bounded scope, classify
  touched files, and conditionally spawn children.
- **Validate**: Confirm non-code-only steps produce both child fields as
  `not_run`; confirm each applicability decision is independent; confirm linter
  precedes tester only when both apply; confirm no-applicable-lint does not
  suppress a relevant tester; confirm failure blocks are verbatim and
  noteworthy findings are separate.

### Step 3 — Mirror behavioral contracts in OpenCode [critic, verify, commit] — completed

- **Files / symbols**:
  `.cursor/agents/gim-*.md`,
  `.opencode/agents/gim-*.md`,
  `.agents/skills/gim-tools/SKILL.md`,
  `.cursor/agents/AGENTS.md`,
  `.opencode/agents/AGENTS.md`
- **Change**: Mirror Step 2's behavioral rules in OpenCode while retaining
  harness-specific frontmatter and permission settings. Across every fleet
  agent, replace runtime gim-tools import instructions with the autoload rule:
  use available `gim-*` commands directly; consult the skill for semantics when
  needed. Update the shared skill's setup text to make autoload the default.
  Keep test-only module imports in test fixtures where they are required.
- **Validate**: Compare the behavioral sections of Cursor/OpenCode twins;
  permit differences only in frontmatter and harness command wording. Confirm
  no fleet agent instructs runtime `Import-Module` for gim-tools.

### Step 4 — Bound validation retries and normalize historical flow docs [critic, verify, commit] — completed

- **Files / symbols**:
  `.cursor/agents/gim-builder.md`,
  `.cursor/agents/gim-linter.md`,
  `.cursor/agents/gim-tester.md`,
  `.opencode/agents/gim-builder.md`,
  `.opencode/agents/gim-linter.md`,
  `.opencode/agents/gim-tester.md`,
  `.agents/prompts/orchestrator-step-brief.md`,
  `docs/plans/agents/0001-AGENTS-gim-orchestrator-fleet.plan.md`,
  `docs/plans/agents/0002-AGENTS-gim-opencode-fleet.plan.md`
- **Change**: Add bounded retry and terminal infrastructure/setup/timeout/
  interruption outcomes to both specialist twins and the builder handoff
  contract. Include user escalation with a recommendation for terminal
  outcomes. Correct historical workflow diagrams/inventory text so retry
  occurs only while budget remains and linter/tester applicability stays
  independent.
- **Validate**: Review both harness contracts and historical flow docs for
  bounded retries, terminal escalation, ordered gates, explicit skip states,
  scope boundaries, and absence of unconditional child-spawn wording.

## Included pre-existing files

These files are part of the final PR as-is. Review and stage them with the
plan's other GIMtransfer changes; do not broaden their scope or rewrite them:

- `.agents/skills/atlassian-jira-cli/references/acli-reference.md`
- `.cursor/agents/gim-tracker.md`
- `.opencode/agents/gim-tracker.md`

## Review

Run a fresh critic after each implementation step and one final wrap-up critic
after Step 4, Z1, and Z2. Apply agreed findings before each verification gate.
The final delivery remains one squashed PR.

### Step Z1 — Resolve final coherence findings [critic, verify, commit] — completed

- **Files / symbols**:
  `.cursor/agents/gim-orchestrator.md`,
  `.opencode/agents/gim-orchestrator.md`,
  `docs/plans/agents/0001-AGENTS-gim-orchestrator-fleet.plan.md`,
  `docs/plans/agents/0006-AGENTS-scoped-lint-test-agent-flow.plan.md`
- **Change**: Keep both orchestrator twins at routing level and remove
  unconditional child sequencing. Correct historical lint-failure routing to
  distinguish bounded actionable repair from terminal escalation, and remove
  historical guidance that requires tests or smoke checks for every change.
- **Validate**: Confirm orchestrator twins delegate applicable validation to
  builder, historical flow matches bounded retry/terminal escalation, and no
  shared or historical overview requires validation for untestable changes.

### Step Z2 — Separate linter and tester applicability gates [critic, verify, commit]

**Status:** completed after critic review and user verification.

- **Files / symbols**:
  `.cursor/agents/gim-builder.md`,
  `.cursor/agents/gim-linter.md`,
  `.cursor/agents/gim-tester.md`,
  `.opencode/agents/gim-builder.md`,
  `.opencode/agents/gim-linter.md`,
  `.opencode/agents/gim-tester.md`,
  `.agents/prompts/orchestrator-step-brief.md`,
  `docs/plans/agents/0001-AGENTS-gim-orchestrator-fleet.plan.md`,
  `docs/plans/agents/0002-AGENTS-gim-opencode-fleet.plan.md`
- **Change**: Make linter and tester applicability independent and record both
  decisions. A relevant tester may run when no lint applies, and an ordinary
  lint result must not decide whether testing applies; preserve terminal user
  escalation and independent bounded retry rules. Keep linter and tester role
  contracts focused on their own work; cross-role routing belongs to builder.
- **Validate**: Review both harness contracts and historical guidance for
  separate lint/test questions, independent skip states, correct ordering when
  both run, no linter-result gate around tester applicability, and no
  cross-role commentary in the linter/tester descriptions.
