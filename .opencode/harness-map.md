# GIM harness map (OpenCode)

Spawn/resume mechanics for OpenCode. Cursor column: `.cursor/rules/gim-harness-map.mdc`. Human dual crosswalk: `docs/plans/agents/0002-AGENTS-gim-opencode-fleet.plan.md` § Harness tool map. Term: `.agents/CONTEXT.md` (**HarnessToolMap**).

| Action | OpenCode |
|---|---|
| Invoke orchestrator | Primary agent `gim-orchestrator` (Tab / `default_agent`); `gim-orchestrate` skill still authoritative |
| Spawn subagent | Task `subagent_type: gim-*` (same names as Cursor) |
| Resume builder | Task `task_id: <id from prior task>` |
| Fresh critic | New Task; **never** reuse critic `task_id` |
| Parallel explorers | Multiple Task; TUI child-session nav as needed |
| Load skill | OpenCode `skill` tool + Read; `.agents/skills` discovered natively |
| Prefs / plans / ADRs | Same paths under `.agents/` and `docs/` |

Fleet roster and product loop: `.agents/skills/gim-orchestrate/SKILL.md`.
