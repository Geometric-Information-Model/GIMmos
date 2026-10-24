# Shared bash allowlist owned by user-local opencode config

The nine `gim-*` OpenCode agent files under `.opencode/agents/` repeated an identical 37-entry bash permission block (`"*": ask` + read-only filesystem/git allows). This bloated every agent file and made the allowlist uneditable without touching committed files. The common block now lives in the **user-owned** local config (`opencode.local.json`, gitignored, loaded via `OPENCODE_CONFIG` per `start-opencode.ps1`; onboarding default in the committed `opencode.local.template.jsonc`). Agent frontmatter holds only agent-specific bash rules; `"*": ask` must remain the global map's first entry and never appear in an agent map (agent rules append after global rules and win by last-match-wins). `git branch*` (matches `-d`/`-D`/`-m`) was narrowed per agent to explicit list/copy varieties; create/delete/rename fall through to `*: ask`. Built-in agents (`build`/`plan`/`general`) are pinned to bash allow-all in committed `opencode.json` (main flow is the gim-* fleet). Grandchild agents (`gim-linter`, `gim-tester`) use **self-contained** `"*": deny` maps instead of inheriting the global list — opencode depth ≥2 "ask" approvals never resurface to the main UI, so ask = deadlock there; denied commands are reported back through the parent builder to the orchestrator, and the allowlist grows iteratively. Supersedes plan 0002 Q4's "encode the full common allow list on every agent".

**Status:** accepted

## Considered Options

- **Common block in user-local `permission.bash` + per-agent deltas** (chosen) — de-bloats agent files; each user owns/edits their allowlist via gitignored local config.
- **Keep common block in every agent file** — rejected; 9x duplication, no user-level customization.
- **Common block in committed `opencode.json`** — rejected; no per-user override without editing committed config.
- **`git branch*` left broad** — rejected; also matches `git branch -d`/`-D`/`-m` (silently allowed today).
- **Linter/tester inherit global `*: ask`** — rejected after the depth ≥2 ask deadlock (approval never resurfaced); `"*": deny` + explicit allowlist + parent-resurface footnote instead.
- **`npm install*` for tester** — rejected; wildcard permits `npm install <package>` (supply-chain surface); exact `npm install` only.
