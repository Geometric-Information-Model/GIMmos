# Cursor orchestrator nesting (skill-on-main)

Cursor allows only two levels of subagent nesting (main → child → grandchild). The GIM orchestrator therefore runs as a **skill on the main agent** (`/gim-orchestrate`), with a companion `.cursor/agents/gim-orchestrator.md` for form/model pin only — not as a Task-spawned nested orchestrator. The builder (depth 1) may spawn linter/tester (depth 2); the orchestrator alone owns critic spawn — the builder never spawns critic.

**Status:** accepted

## Considered Options

- **Skill-on-main + agent def for form** (chosen) — preserves nesting budget for builder → linter/tester and keeps the main context as the sequencer.
- **Fully flat spawn-only-from-main** — rejected; builder could not run lint/test without burning the only nesting hop on the orchestrator.
- **Nested orchestrator subagent** — rejected; would consume a nesting level and leave grandchildren unable to spawn further agents.
