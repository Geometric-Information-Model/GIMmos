---
name: gim-improve
description: "Improve the GIM fleet workflow itself through exact-file, exact-edit triage; workflow-only, not product-code work. Only the user can invoke this skill."
---

# GIM Improve

`/gim-improve <issue and/or fix>` changes the GIM fleet itself: agents,
skills, harness guidance, or related workflow configuration. It does not
change product code.

## Triage

Ask one question first: can the request identify the exact file and exact edit?

- **Yes:** make the requested edit directly.
- **No:** do not edit. Propose a short candidate list with a recommendation and
  wait for the user's choice.

Vague requests such as “fix the skill” fail this test. A concrete request such
as “add Task access to `gim-builder`” passes it.

## Coupled-file check

Before any edit, enumerate every implicated file. For fleet changes, check this
coupled set first:

- `.cursor/agents/` and `.opencode/agents/` (both agent homes)
- `.cursor/rules/gim-harness-map.mdc` and `.opencode/harness-map.md` (both
  harness maps)
- `.agents/skills/gim-orchestrate/SKILL.md`
- `.agents/CONTEXT.md`
- `.agents/skills/gim-tools/` (25 verbs, nine groups; regenerate references with
  `gim-verbs --gen-refs`; the seven fleet verbs stay role-scoped)

Edit only files required by the requested diff. Do not rewrite coupled files
merely for symmetry when the request does not require it. Use GIM fleet names:
`gim-orchestrator`, `gim-builder`, `gim-explorer`, `gim-critic`,
`gim-version-controller`, `gim-tester`, `gim-tracker`, and `gim-debugger`.

## Commit boundary

Do not commit automatically. Commit only when the user explicitly asks, by
telling `gim-orchestrator` to delegate the mutation to
`gim-version-controller`. The commit subject must be:

`gim-improve(<target>): <summary>`

The commit body must include a `Trigger:` line quoting the user's request.
Do not create tickets, push, or open a PR unless separately authorized by the
workflow and user.
