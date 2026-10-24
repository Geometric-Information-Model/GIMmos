# Cursor agent home — sync contract

GIM fleet agent **bodies** live here (`gim-*.md`) and in the OpenCode twin folder [`.opencode/agents/`](../../.opencode/agents/) (sync contract: [`AGENTS.md`](../../.opencode/agents/AGENTS.md)). Decision record: [`docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md`](../../docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md).

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
- **Harness map twins** — spawn/resume semantics → update `.cursor/rules/gim-harness-map.mdc` and `.opencode/harness-map.md` together (+ plan dual table if present; same change).
- **Frontmatter** may differ per harness (Cursor: `model`, `readonly`, etc.; OpenCode: `mode`, `permission`, `model`).
- Skills, prompts, and prefs stay shared under `.agents/` — not duplicated here.

## gim-tools vocabulary

- 25 verbs in nine groups; the seven fleet verbs (`gim-preflight`, `gim-pr`, `gim-jira-search`, `gim-jira-create`, `gim-jira-transition`, `gim-wrapup-gather`, `gim-wrapup-respond`) are role-scoped in agent frontmatter, never global.
