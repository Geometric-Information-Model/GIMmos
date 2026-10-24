# OpenCode agent home — sync contract

GIM fleet agent **bodies** live here (`gim-*.md`) and in the Cursor twin folder [`.cursor/agents/`](../../.cursor/agents/) (sync contract: [`AGENTS.md`](../../.cursor/agents/AGENTS.md)). Decision record: [`docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md`](../../docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md).

## Eight agent stems

| Stem | File |
|---|---|
| `gim-orchestrator` | `gim-orchestrator.md` |
| `gim-explorer` | `gim-explorer.md` |
| `gim-builder` | `gim-builder.md` |
| `gim-tester` | `gim-tester.md` |
| `gim-critic` | `gim-critic.md` |
| `gim-version-controller` | `gim-version-controller.md` |
| `gim-tracker` | `gim-tracker.md` |
| `gim-debugger` | `gim-debugger.md` |

## Sync duty

- **Behavioral twin duty:** behavioral changes (body text, spawn rules, output contracts, mandatory skills) → update the matching `gim-*.md` in **both** `.cursor/agents/` and `.opencode/agents/` in the **same** change/PR; behavioral twins must remain synchronized.
- **Harness map twins** — spawn/resume semantics → update `.opencode/harness-map.md` and `.cursor/rules/gim-harness-map.mdc` together (+ plan dual table if present; same change).
- **Frontmatter** may differ per harness (OpenCode: `mode`, `permission`, `model`; Cursor: `model`, `readonly`, etc.).
- Skills, prompts, and prefs stay shared under `.agents/` — not duplicated here.

## Bash permissions

- The **common bash allowlist** lives in the user-owned local config (`opencode.local.template.jsonc` → user `opencode.local.json` via `OPENCODE_CONFIG`); agent frontmatter `bash` holds **agent-specific rules only**.
- Never add a `"*"` key to an agent `bash` map (agent rules append after global; last-match-wins — a `"*"` would swallow the global list). Exception: `gim-tester` uses a self-contained `"*": deny` map (grandchild "ask" never resurfaced — see plan 0003 session amendment).
- `gim-tools` exposes 25 verbs in nine groups. The seven fleet verbs (`gim-preflight`, `gim-pr`, `gim-jira-search`, `gim-jira-create`, `gim-jira-transition`, `gim-wrapup-gather`, `gim-wrapup-respond`) are role-scoped in the owning agent frontmatter, never in the shared global allowlist.
