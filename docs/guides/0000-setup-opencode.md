# OpenCode fleet setup

Human / first-time OpenCode setup for the GIM agent fleet in `GIMmos`. Prefer this guide over loading the full walkthrough into every agent context via root `AGENTS.md`.

## Steps

1. Open [`GIMmos.code-workspace`](../../GIMmos.code-workspace) (same multi-root workspace as Cursor).
2. Install **OpenCode ≥ 1.18.2** (required for project `subagent_depth` in [`opencode.json`](../../opencode.json)).
3. Connect **DeepSeek** via OpenCode `/connect` (official provider). Models: `deepseek/deepseek-v4-pro` and `deepseek/deepseek-v4-flash`.
4. Copy [`opencode.local.template.jsonc`](../../opencode.local.template.jsonc) → `opencode.local.json` (gitignored) **or** use your own local JSON file. Point OpenCode at it with the **`OPENCODE_CONFIG`** environment variable (file path — not `OPENCODE_CONFIG_CONTENT`). PowerShell example:

   ```powershell
   Copy-Item opencode.local.template.jsonc opencode.local.json
   $env:OPENCODE_CONFIG = (Resolve-Path opencode.local.json).Path
   ```

   The template carries the **shared bash allowlist** (read-only commands + `"*": ask` catch-all) — user-owned: widen/narrow it freely; it is the default for all agents. Never add a `"*"` key to an agent frontmatter `bash` map (it would swallow this global list).

5. Committed [`opencode.json`](../../opencode.json) already sets `default_agent: gim-orchestrator`, `subagent_depth: 3`, three shared `external_directory` allows (`../GIMmanager/**`, `../CoreWebApi/**`, `../GIMocctWrapper/**`), and pins built-in agents `build`/`plan`/`general` to bash allow-all. Your local file via `OPENCODE_CONFIG` overrides/extends permissions only (bash allowlist + extra paths) — never commit machine-specific siblings. Agent files under `.opencode/agents/` carry only agent-specific bash rules (`gim-tester` uses a self-contained `"*": deny` map).
6. Committed setup docs name only those three sibling repos for `external_directory`; do not add private or optional workspace folders to committed config or docs.
7. Optional verify: `opencode debug config` — confirms merged project `opencode.json` + `OPENCODE_CONFIG` local overlay (bash allowlist + external_directory included).

## Related

- Dual-home ADR: [`docs/adr/agents/0002-AGENTS-opencode-fleet-dual-home.md`](../adr/agents/0002-AGENTS-opencode-fleet-dual-home.md)
- Agent sync: [`.cursor/agents/AGENTS.md`](../../.cursor/agents/AGENTS.md) · [`.opencode/agents/AGENTS.md`](../../.opencode/agents/AGENTS.md)
- Shared skill: [`.agents/skills/gim-orchestrate/SKILL.md`](../../.agents/skills/gim-orchestrate/SKILL.md)
