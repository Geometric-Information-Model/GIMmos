---
name: gim-autopilot
description: "Run the current step, one step, a step range, or all remaining plan steps with bounded autonomous decisions and one final verification report. Only the user can invoke this skill."
---

# GIM Autopilot

`/gim-autopilot [scope]` runs approved GIM plan work without stopping for
minor reversible choices. It is user-invoked and never auto-activates.

## Scope

Parse scope in this order. Refuse with a one-line message when no approved
plan is active.

| Invocation | Scope | Commit | PR |
|---|---|---|---|
| `/gim-autopilot` | Current step | Off | Off |
| `/gim-autopilot step A` | Step A only | Off | Off |
| `/gim-autopilot steps 1-3` | Steps 1 through 3 | Per prefs | Per prefs |
| `/gim-autopilot all` or `entire plan` | All remaining steps | Per prefs | Per prefs |

Current step means first plan step not yet built, or first built step not yet
committed when commits are enabled. A single-step run never commits or opens a
PR. A range or full-plan run follows configured preferences. Any PR opened by
autopilot is a draft. Multi-step branches follow the configured chained-branch
workflow.

## Run rules

- Keep normal per-step ordering: one builder, applicable test validation,
  critic loop, then configured git/PR actions.
- Defer user verification for every step. Give one consolidated verification
  report at run end, then stop. This is the one sanctioned exception to the
  stern per-step verify gate: the consolidated final report is the gate. The
  override affects only the verify gate; every other gate stays as written.
  Outside this explicit user-invoked mode, the stern verify rule is absolute.
- Log every reversible local choice on the plan as it happens, including the
  alternative not chosen.
- Preserve critic passes and `ContextExhausted` handling.
- Do not skip required phases except the deferred verification gate.

## Blocking rubric

Stop and report when work needs credentials, permissions, environment setup, or
missing secrets; explicit destructive consent; history rewrite; ticket
creation; a plan-breaking or costly-to-reverse choice; a builder reports
blocked or dependent partial work; the critic loop fails to converge; tests
remain failed after bounded retries; or the run hits a terminal child
validation result.

Decide and log local reversible choices: naming, file placement, equivalent
implementation options, small fixes, and critic dispositions. Do not treat
autopilot as consent for destructive actions, ticket creation, or changes that
invalidate the plan.

## Ending

At normal end, report per step: work built, test result, critic rounds,
commit, branch, and draft PR URL when applicable. Include all logged decisions,
rejected critic findings with reasons, and exact verification commands or
click-paths. Then wait for user verification; do not start another step or
mark a PR ready.

At blocked end, report completed work, in-flight step and dirty files, blocker,
required user action, exact resume invocation, and decisions logged so far.
