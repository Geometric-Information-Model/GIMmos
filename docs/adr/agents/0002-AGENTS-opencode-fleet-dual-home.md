# OpenCode fleet dual-home and local config

The GIM agent fleet runs on both Cursor and OpenCode. Agent **bodies** are duplicated under `.cursor/agents/` and `.opencode/agents/` (platform frontmatter differs: Cursor model/readonly vs OpenCode `mode`/`permission`/`model`). Both folders carry an `AGENTS.md` that requires editing the parallel twin whenever a behavioral contract changes. Skills, prompts, and prefs stay shared under `.agents/` (not duplicated per harness). Nesting for builder → linter/tester (and headroom) is set with project `subagent_depth: 3` in committed `opencode.json`, with `default_agent: gim-orchestrator`. OpenCode **≥ 1.18.2** is required for `subagent_depth` to apply (older CLIs warn and ignore the key). Machine-specific `external_directory` extras load via env `OPENCODE_CONFIG` pointing at a gitignored local JSON file (OpenCode has no first-class `opencode.local.json` auto-load yet); a committed generic template may be copied or replaced by the user’s own file. Committed project config only allows a small shared sibling set (`GIMmanager`, `CoreWebApi`, `GIMocctWrapper`).

**Status:** accepted

## Considered Options

- **Dual full agent markdown homes + sync AGENTS.md** (chosen) — matches OpenCode’s `.opencode/agents/` loader and Cursor’s `.cursor/agents/`; avoids a brittle codegen step.
- **Single shared markdown home** — rejected; frontmatter and permission schemas are not portable.
- **Official `opencode.local.json` auto-load** — not shipped (upstream #17232); rejected as a dependency.
- **`OPENCODE_CONFIG_CONTENT` inline JSON** — rejected; harder to edit than a file path via `OPENCODE_CONFIG`.
- **`subagent_depth: 2`** — rejected in favor of `3` for headroom beyond builder → linter/tester.
