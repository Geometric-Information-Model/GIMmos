# ADR 0007: Harness permission wiring — gim-tools allow/deny + hooks

Status: accepted (plan 0004).

## Context

The gim-tools toolkit (AGENTS ADR 0003/0005/0006) ships a harness-config checklist
(`.agents/skills/gim-tools/references/harness-config.md`) but applies nothing.
Empirical probing (opencode 1.18.18, Cursor 3.16.29 — findings deleted after
this work, curated copy in `docs/agents/0001-harness-permission-findings.md`)
established how each harness gates commands. This ADR locks the wiring
architecture so agents run their sanctioned commands without waiting on the
user's allowlist, while the .NET/execution holes stay closed with helpful
messages.

## Decisions

### 1. Allowlist is the only silent-allow layer; hooks carry deny-with-message

- **Cursor**: hook `allow` does not hide the ask UI — the Auto-run allowlist
  is the only silent-allow mechanism. Hooks are the only silent-deny layer
  (`.cursor/cli.json` deny syntax is unproven for the IDE agent; unused).
- **OpenCode**: config `allow` runs silently; config `deny` short-circuits
  with a generic ruleset dump; `tool.execute.before` fires *before* permission
  evaluation and can `throw` an exact message — the only pre-permission hook
  surface. `permission.asked` + SDK reply exists but is **reject-only** (see
  decision 1a).
- Consequence: **allow = config / Auto-run UI**. **Deny with a replacement
  message = hook/plugin** (discovery-at-failure channel per harness-config).
  OpenCode config denies remain as a plugin-less safety net (generic message).

### 1a. The plugin never auto-allows — permission.asked is reject-only

A `permission.asked` reply (`once`/`always`) approves the **entire** compound
command, not the one segment that asked: opencode evaluates compound commands
per segment, so `someCommandWeAllow; DangerousCommand` produces one ask whose
"always" reply would approve the dangerous part too. Allowing requires every
part to be safe (hard); denying requires one offending part (easy). Therefore:

- The config allowlist is the whitelist — safe by construction because a
  compound command only runs when **every** segment matches an allow.
- The plugin/ask-stage handler never replies `allow`/`always`; it may only
  `reject` (git `--` quirk, decision 3) or do nothing (let the ask flow on).
- All conditional policy lives in `tool.execute.before` throws (deny-with-
  message), which block the whole command — a deny, hence safe.

### 2. Ends over means — no forced harmonization

The two harnesses do not need identical mechanisms, only the same spirit.
Each side picks the more convenient mean for the same end — **the end matters
more than the means**:

- Both harnesses share the same split: allowlist = allow; hook/plugin =
  deny-with-message. The differences are in the deny channel mechanics
  (opencode `tool.execute.before` throw vs Cursor `preToolUse`
  `{permission:"deny"}`) and in harness-specific quirks (git `--` ask-stage
  reject — opencode only; Cursor passes `git diff -- file` fine).
- Dual-sync rule: when adding a hook/rule on one harness, mirror the *intent*
  on the other unless the hook exists to handle a single-harness quirk.
  Document the mechanism difference in each harness's `AGENTS.md`.
- **Command parsing is centralized**: one pwsh lib
  (`.agents/skills/gim-tools/scripts/parse-command-tree.ps1`) using
  `System.Management.Automation.Language.Parser::ParseInput` emits a JSON
  command tree; both hooks consume it (Cursor dot-sources in-process,
  OpenCode spawns `pwsh -File` lazily). Never rewritten per harness.

### 3. OpenCode git `--` pathspec quirk — ask-stage handling

OpenCode's AST matcher treats the **first file token after `git <cmd> --`** as
a command candidate. Convention: `.gitignore` is allowlisted as a token and
git pathspec commands list it first — `.gitignore`-first commands run silently
(config allowlist, no `permission.asked` event). When a different first file
triggers an ask, the plugin **rejects** it if the first file after `--` is not
`.gitignore`; if it is `.gitignore` (edge case), the plugin **does nothing**
and lets the ask continue — it never auto-allows git pathspec commands
(decision 1a).
Security stance (harness-config): path-scoping grants no real security; this
is a matcher-quirk workaround, not a boundary.

### 4. gim-* verb tiers are team-standard

All 19 gim-* verbs (read, state, write, execution) are allowlisted team-wide
(harness-config "decide individually" tiers decided: allow — write verbs all
support `--dry-run`, `gim-run` scrubs credential env vars). Per-agent
permission surfaces are role-scoped (plan 0004 G6 table): orchestrator may
write but must delegate; builder/tester/critic get execution; debugger gets
full write + execution.

### 5. Deny posture: hooks own the message, config stays lean

Commands with a sanctioned alternative are **not** config-denied — the
hook/plugin denies them with the replacement message (discovery-at-failure).
Config deny entries (opencode) would only surface an indiscriminate generic
message; the `"*": "ask"` default remains the catch-all when hooks miss.
Plain `git push` is **not** denied (version-controller pushes regularly);
force-push variants (`--force`, `-f`, `--force-with-lease`) are hook-denied
with the explicit-consent message. On Cursor (no config-deny surface for the
IDE agent) the hook is the only deny layer.

### 6. Probe artifacts are deleted; findings are curated

`perm-debug.js`, `perm-debug.ps1`, `.cursor/cli.json`, debug logs, and the five
root findings files are deleted after wiring. Distilled, version-stamped
knowledge lives in `docs/agents/0001-harness-permission-findings.md`.

## Consequences

- Agents use gim-* verbs without per-command user approval on both harnesses.
- Denied constructs surface with the sanctioned replacement (gim-* channel),
  not a generic dump or a dead-end prompt.
- Hook behavior is structural (central AST parser), not substring matching;
  string/comment false positives stay inert.
- All harnesses must be re-verified live after any harness upgrade (payload
  shapes drift; see `docs/agents/0001`).
