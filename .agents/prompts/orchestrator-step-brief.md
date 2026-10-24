# Orchestrator → gim-builder step brief

**Usage (orchestrator):** Fill every `{{placeholder}}`, then send this document as the Task prompt to **gim-builder**. The brief carries the task and its parameters only. The builder body owns skills, search and command limits, validation, failure rules, and the output contract.

This is **not** the conversation `/handoff` skill (`.agents/skills/handoff/`) — that compacts a session for a new agent. This file is only the per-step builder brief.

---

## Step

| Field | Value |
|---|---|
| Plan file | `{{plan_path}}` |
| Step id | `{{step_id}}` |
| Architecture | `{{architecture}}` (windows → PowerShell; linux → bash) |
| Gates | verify_per_step=`{{verify_per_step}}`; commit_per_step=`{{commit_per_step}}`; pr_per_step=`{{pr_per_step}}`; critic_per_step=`{{critic_per_step}}` |
| Files / symbols | `{{files_symbols_hint}}` |

### Step excerpt (from the plan)

Read this step from the plan file too; the excerpt below is the scope for this dispatch.

```markdown
{{step_section_excerpt}}
```

### Extra context (optional)

{{extra_context}}

## Step-specific constraints and failures

Only constraints that exist in this step's excerpt and are not already in the builder body. Write `none` when the excerpt adds none.

{{step_specific_constraints}}
