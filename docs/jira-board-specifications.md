# Jira board — GIM

- **Project key / ticket prefix:** GIM-
- **Board name:** GIM bord
- **Board ID:** 9

## Default fetch flow

1. Resolve the active sprint: `acli jira board list-sprints --id 9 --state active --json` (observed 2026-10-20: exactly one active sprint, id 378 "GIM Sprint 8"; if zero or multiple active sprints, stop and require an explicit `--sprint <id>`).
2. List current sprint work items: `acli jira sprint list-workitems --sprint <sprint-id> --board 9`. Both flags are required; `--board` alone exits 1 with `required flag(s) "sprint" not set`.
3. View one ticket: `acli jira workitem view GIM-<n>`

Skip sprint when the user supplies a ticket key or asks for backlog/JQL search.

## Status names

Exact workflow status strings to pass to `acli jira workitem transition --status "<name>"` (names differ from display labels in edge cases — use these):

| Status name (exact) | Status id | Category | Notes |
|---|---|---|---|
| To Do | 10010 | new (To Do) | initial status |
| In Progress | 3 | indeterminate (In Progress) | via transitions "In Progress" or "Branch created" |
| In Review | 10011 | indeterminate (In Progress) | via transition "In Review" |
| Done | 10002 | done (Done) | via transition "Done" |

**Workflow note (observed 2026-10-20):** all four statuses (`To Do`, `In Progress`, `In Review`, `Done`) inter-transition directly on this Task workflow — all 12 ordered pairs succeed with no intermediate hop (14 transitions total, all exit 0). The CLI flag is `--status <target>`, not `--to`.

## Available transitions (GIM-127 sample, 2026-08-12)

From the `transitions` array (see `.agents/skills/atlassian-jira-cli/references/transitions-gim-workflow.md` for the raw JSON):

| Transition name (exact, --status value) | Transition id | Target status |
|---|---|---|
| In Review | 2 | In Review (10011) |
| To Do | 11 | To Do (10010) — loop |
| In Progress | 21 | In Progress (3) |
| Done | 31 | Done (10002) |
| Branch created | 3 | In Progress (3) — isGlobal: false (contextual) |

Notes:
- acli has no read-only transition-list flag; the list lives behind the mutating command. Keep this table + the reference JSON updated whenever the workflow changes (query via Jira UI or the transitions endpoint).
- `--status` takes the exact workflow status name; casing matters.
