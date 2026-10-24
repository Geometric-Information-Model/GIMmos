---
name: atlassian-jira-cli
description: View, search, update, and create Jira tickets / work items via the Atlassian CLI (acli). Use when the user mentions Jira, Atlassian, acli, a ticket or work item, an issue key (e.g. GIM-1234), transitioning/moving a ticket between board columns, creating or assigning a ticket, JQL search, sprints, or updating ticket status as work progresses. Raw acli commands with a read-only-vs-mutating consent policy.
---

# Atlassian Jira CLI (acli)

Standardizes Jira work through the Atlassian CLI (`acli`). This skill folder is
**generic and repo-agnostic** — it can be copied verbatim into any repo. All
repo-specific values live outside it (see Repo configuration below).

## Repo configuration

> **Board/workflow specifics for _this_ repo live at
> [`../../../docs/jira-board-specifications.md`](../../../docs/jira-board-specifications.md)** —
> board IDs, the ticket key prefix, the canonical JQL, board columns, and the
> exact workflow status names to pass `transition`. **Read it** before searching
> or transitioning so you use the correct `--status` / `--jql` strings.
>
> Porting this skill to another repo: copy this folder, create an equivalent
> board doc there, and update the relative path in this pointer line.

## When to use

- Look up a ticket's status, summary, or assignee.
- Search tickets by JQL (e.g. everything in a component, or assigned to you).
- List current-sprint work items on the board.
- Move a ticket between columns as work progresses (start work, PRs opened, merged).
- Create or assign a ticket.
- Jira keys in docs, PR bodies, or comments always include repo name or link. See `WORKSPACE.md` `## Cross-repo references`.

## Prerequisites (one-time per machine)

1. Install acli: `python .agents/skills/atlassian-jira-cli/scripts/install_acli.py`
   (preview with `--dry-run`). It never edits PATH; see
   [`references/installation.md`](references/installation.md).
2. Authenticate once: `acli jira auth login --web`, then verify with
   `acli jira auth status`.

If `acli` is not found on PATH, run the installer (step 1) and/or add the
printed location to PATH manually.

## Consent policy (read this before running anything)

| Action | Commands | Consent |
|--------|----------|---------|
| Read-only | `workitem search`, `workitem view`, `sprint list-workitems`, `board list-sprints`, `board view`, `auth status` | Run freely. |
| Mutating | `workitem transition`, `workitem create`, `workitem edit`, `workitem assign`, `workitem comment` | **Get explicit user consent first.** |

For mutating commands, pass `--yes` only when the caller explicitly requests it.
Without `--yes`, acli prompts. Never auto-confirm a mutating command on the user's
behalf.

**Ticket creation assigns `@me` by default.** Before running
`acli jira workitem create`, ask the user whether to self-assign
(`--assignee @me`) or assign a specific email. Leave it unassigned
(`--unassigned`) only when the user explicitly requests that exception.

## Default ticket discovery flow

1. **Current sprint** — resolve the active sprint with
   `acli jira board list-sprints --id 9 --state active --json`, then list its
   items with `acli jira sprint list-workitems --sprint <id> --board 9`. Both
   flags are required; `--board` alone exits 1 with
   `required flag(s) "sprint" not set`. Board **9** per
   [`../../../docs/jira-board-specifications.md`](../../../docs/jira-board-specifications.md).
   See [`references/acli-reference.md`](references/acli-reference.md) for flags.
2. **One ticket** — `acli jira workitem view GIM-<n>` for detail.
3. **Skip sprint** when the user gives a key directly or wants backlog/JQL search
   (`workitem search`, `workitem view`).

## Common commands

```bash
# read-only: current sprint (board 9)
acli jira board list-sprints --id 9 --state active --json
acli jira sprint list-workitems --sprint <id> --board 9

# read-only: search by JQL
acli jira workitem search --jql "<JQL>" --count
acli jira workitem search --jql "<JQL>" --fields "key,status,summary" --json

# read-only: view one ticket (key is positional, never --key)
acli jira workitem view GIM-<n> --fields "key,summary,status,assignee" --json

# mutating (consent required): transition a ticket
acli jira workitem transition --key GIM-<n> --status "<status name>" --yes
```

For `create`/`edit`/`assign`/`comment`, see
[`references/acli-reference.md`](references/acli-reference.md).

## Lifecycle workflow

As work on a ticket progresses, transition it to the right status. The exact
event → status mapping and the precise `--status` strings (their casing differs
from the board column display) are defined in
[`../../../docs/jira-board-specifications.md`](../../../docs/jira-board-specifications.md).
Read that doc, then call `acli jira workitem transition` with the mapped status.

## References

- [`references/acli-reference.md`](references/acli-reference.md) — command cheat
  sheet, read-only vs mutating split, official doc links.
- [`references/installation.md`](references/installation.md) — per-OS install +
  one-time auth, no-PATH-edit policy.
