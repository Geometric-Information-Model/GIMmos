# OpenCode plugins

Two plugins live in `.opencode/plugins/`: `gim-permissions.js` (deny-only
permission hook, the bulk of this document) and `gim-preload.js` (per-agent
context preload, see the section below). Both load at OpenCode startup; a
restart is required after any `.js` change.

## gim-preload

Per-agent context preload (decision record:
`docs/adr/agents/0009-AGENTS-context-preload.md`). Injects each fleet agent's
reference material into its system prompt, so the agent body does not spend a
tool call reading it.

- **Shared config:** `.agents/agent-preload.json` — same file the Cursor
  preload hook reads. There is exactly one config; do not add a harness-local
  copy. The plugin defines `CONFIG_NAME = .agents/agent-preload.json` relative
  to the project root.
- **Dual-sync (ADR 0009):** the Cursor adapter
  (`.cursor/hooks/gim-preload.ps1`, documented in `.cursor/hooks/AGENTS.md`)
  reads this same map. An agent-mapping change applies to both harnesses
  without a second edit; a change to one adapter's mechanics must keep the
  other adapter's documented behavior in step. Record such decisions against
  ADR 0009.
- **Shape:** `enabled`, `header`, and `agents.{name}.{skills,files}`.
  `skills` are bare skill names resolved through the repo-local roots
  `.agents/skills`, then `.opencode/skills` and `.claude/skills`; there are no
  user-global skill roots. `files` are paths relative to the project root.
  Skills are injected first, then files.
- **Every agent gets `.agents/agent-prefs.md`.** Role skills diverge per
  agent (orchestrator planning/tracking, explorer search, builder area
  routing, version-controller git/GitHub, critic `caveman` + `gim-tools`,
  tracker Jira/GitHub, debugger `caveman` + `diagnosing-bugs` + `gim-tools`,
  tester tools). There is no linter agent or entry.
- **Hooks:** `chat.message` covers every real user turn, including `task`
  subagent children and `task_id` resumes (the agent name is on the message).
  `experimental.chat.system.transform` is the fallback for synthetic user
  messages (post-compaction "continue", task-output summary), deriving the
  agent from the session's persisted messages. No marker or in-memory state;
  the config is re-read every turn.
- **Path display:** `<gim-file path="...">` is project-relative with forward
  slashes. A trailing separator on the project root and Windows path-case
  differences are normalized before display; a path outside the root is shown
  unchanged.
- **Restart rule:** the config is re-read per turn, so config and skill edits
  apply on the next turn. Only a change to `gim-preload.js` needs an OpenCode
  restart.
- **Log:** file-only at `%TEMP%\opencode\gim-preload.log` (tags `init`,
  `inject`, `fallback`, `miss`, `config`, `read`, `error`). Never
  `console.log` in the TUI.

## gim-permissions

The production deny-only permission plugin for the OpenCode TUI (ADR 0007).

### Purpose

Agents run sanctioned commands without waiting on the user's allowlist, while
denied constructs surface with the **replacement channel** (a message naming
the gim-tools verb to use) instead of a dead-end prompt or a generic ruleset
dump. The plugin is the "discovery-at-failure" layer of
`.agents/skills/gim-tools/references/harness-config.md`.

### Architecture (ends over means)

- **The config allowlist is the whitelist.** A compound bash command only runs
  when **every** segment matches an allow rule. This is the only silent-allow
  layer (opencode `permission.bash.allow`).
- **The plugin is deny-only.** It never allows anything:
  - *before-stage*: `tool.execute.before` fires before permission evaluation;
    throwing blocks the whole command with an exact message (the replacement
    hint). This is the primary deny channel, including the git `--` pathspec
    hint (escalated — see quirks).
  - *ask-stage*: `permission.asked` + SDK reply is **reject-only** for the git
    `--` pathspec quirk (safety net, see below). The plugin NEVER replies
    `once`/`always` (ADR 0007 §1a) — a reply approves the **entire** compound
    command, so `allow; Dangerous` would run its dangerous part.
- **All decision logic is pure predicates** over the central command tree in
  `lib/gim-permissions-rules.js`; `gim-permissions.js` is thin wiring (spawn +
  cache + events + logging). The central tree comes from the ONE parser lib
  (`.agents/skills/gim-tools/scripts/parse-command-tree.ps1`) — command parsing
  is never re-implemented locally.
- Command text is parsed lazily: a cheap `PRE_FILTER` regex runs first; the
  parser spawn only happens when the regex matches. Trees are cached per
  command and shared between the before-stage and the ask-stage.

### Deny message contract (user-locked)

Every deny (before-stage throw, incl. file-tool path denies) uses the exact
framing below so models never mistake a deny message for command output.
Harness name for this file: **opencode permission plugin**.

```
Your command was not run: command '<cmd>' was denied by opencode permission plugin. Reason: <reason>. Please try: <alternative>. This is NOT the command's output — this is the permission hook's error message.
```

- `<cmd>` = the attempted command text, truncated to ~120 chars with '…' when
  cut; for file-tool denies `<cmd>` = `<tool> <path>` (e.g. `edit C:\repo\.env`).
- `<reason>` = short rule category (e.g. `cmdlet foreach-object (script-block
  built-in)`, `git force-push variant`, `secret-file path`, `pwsh/powershell
  invocation`, `git pathspec without .gitignore first`).
- `<alternative>` = the replacement hint per rule.
- Rules (`lib/gim-permissions-rules.js`) carry ONLY `rule` + `reason` +
  `alternative`; the wiring (`gim-permissions.js`) builds the framed message
  via `formatDenyMessage()` (exported from the rules module, unit-testable).
  Never embed the full framed text in a rule.

### How to add a rule

1. Add the decision predicate to `lib/gim-permissions-rules.js`
   (framework-free, fixture-testable). Decide structural facts from the tree:
   command names (`commands[].name`), args (`commands[].args`), non-string
   tokens (`commands[].tokens`), envelope flags (`hasSubstitution`,
   `staticCalls`), or the `memberCalls` envelope (member-access names reported
   by the central lib — the only channel that sees bare `$x.Invoke(...)`
   expressions) — never raw command text, so strings stay inert. Return
   `{ rule, reason, alternative }` (message contract above).
2. Wire it in `denyDecision()` / `gitPathspecDecision()` (before-stage throw)
   or `gitDashAskDecision()` (ask-stage reject/noop).
3. Add fixture-tree cases to `tests/gim-permissions-rules.test.mjs` (and an
   integration case when it touches the real parser).
4. Update this document.
5. Restart OpenCode and verify live (plugins load at startup only).

### Best practices

- **Structural matching over substring.** A string argument containing
  `ForEach-Object` must not deny. Check command names / non-string tokens.
- **Central lib only — never local parsing.** The plugin spawns
  `pwsh -NoProfile -File .agents/skills/gim-tools/scripts/parse-command-tree.ps1`
  (path resolved from `project.directory`) and consumes its JSON tree. The
  pwsh spawn is the one sanctioned external spawn (user-locked): agents never
  invoke `pwsh`; the plugin (JS, non-agent) does.
- **File-only logs.** `%TEMP%\opencode\gim-permissions.log`. NEVER
  `console.log` in the TUI (corrupts the interface).
- **JSON-only stdout** from the parser — the plugin treats stdout as one JSON
  document; the parser never emits other output.
- **Restart required** for OpenCode plugins — they load at startup only.
- **Spawn cost.** A pwsh spawn costs ~3–5 s per unique command (cached
  thereafter; allowlisted commands still pay it once per unique command). The
  pre-filter + per-command cache keep most commands on the fast path. Keep
  `PRE_FILTER` broad (over-matching costs one extra spawn; under-matching
  skips a deny).
- **Failure-mode split (user-locked 2026-08-20).** Empty command / pre-filter
  no-match (no spawn) → silent (fail-open): nothing was received, nothing to
  decide. Pre-filter matched but the central-lib spawn produced **no parseable
  tree** (spawn error / timeout / non-JSON output / `ok:false` envelope) →
  **generic deny** in the deny-message contract framing: "the permission hook
  could not interpret the permission payload (parse failed) — check
  `%TEMP%\opencode\gim-permissions.log`". The plugin never silently passes on
  a command it could not interpret. (Separate config safety net: the scoped
  destructive-form `git add` config denies still block on total plugin
  failure, so `git add -A` stays denied even if the plugin never loads.)
- **Deny messages name the replacement** (discovery-at-failure): every rule
  carries the gim-tools verb to use instead, framed per the message contract
  above.

### Deny rules — user-locked notes

- **pwsh / powershell command-position deny.** A command whose name is
  exactly `pwsh` or `powershell` is denied (`pwshInvocation`). Command
  position only: `Get-Command pwsh`, `git commit -m pwsh`, and
  `'pwsh'` strings are NOT denied. Agents never invoke pwsh — the harness
  shell already runs pwsh and scripts run in-shell; the plugin (JS, non-agent)
  is the only sanctioned spawner.
- **Bare reflection markers are member-access-shape only.** Bare
  `GetMethod` / `GetField` / `GetType` / `Assembly` / `Invoke` match ONLY when
  the token is preceded by a `.` member-access token; barewords like
  `git commit -m GetMethod` stay inert. The real .NET surface is covered by
  the central lib `memberCalls` envelope + `::` staticCalls +
  `$ExecutionContext`.

### Dual-sync rule

Mirror the **intent** on the other harness (`.cursor/hooks/gim-permissions.ps1`
+ `.cursor/hooks/AGENTS.md`) unless the hook exists to handle a
single-harness quirk. Document the exception in both harness docs. Current
exception: the git `--` pathspec rule is opencode-only (Cursor passes
`git diff -- file` fine and has no such quirk).

### OpenCode ↔ Cursor mechanism differences

| Concern | OpenCode plugin | Cursor hook |
| --- | --- | --- |
| Deny-with-message | `tool.execute.before` **throw** (exact message, before permission eval) | `preToolUse` `{ permission: "deny", agent_message, user_message }` |
| Silent allow | config allowlist only | Auto-run allowlist (UI) only — hook `allow` does NOT hide the ask UI |
| Silent deny | `tool.execute.before` throw beats config | hook deny beats allowlist, skips `beforeShellExecution` |
| Command scope | per command string (before-stage); per segment (ask eval) | one event per full command string |
| Central lib call | spawns `pwsh -File` (lazy, cached) | dot-sources the lib in-process (no second spawn) |
| Parser output | JSON line on stdout | JSON line on stdout |
| Event cadence | `permission.asked` bus event + SDK reply | `preToolUse` / `beforeShellExecution` stdin JSON |
| Startup | plugins load at startup; restart to apply | hooks re-read from `hooks.json` |

### Per-harness quirks

- **Git `--` pathspec quirk (opencode only).** OpenCode's AST matcher treats
  the first file token after `git <cmd> --` as a command candidate. The
  convention: list `.gitignore` first — `git diff -- .gitignore A B` runs
  silent (allowlist). **Primary mechanism (user-locked escalation): the
  before-stage throw.** Any git command whose first file after standalone `--`
  is NOT exactly `.gitignore` (including `--` with no file after) is denied at
  `tool.execute.before` with the sacrificial-token hint
  (`git <cmd> -- .gitignore <files>` or version-controller). `.gitignore`-first
  passes silently; `git diff --no-index a b` (no standalone `--`) is untouched.
  **Safety net (retained): the ask-stage reject/do-nothing handler.** A
  `.gitignore`-first command that still asks (not fully allowlisted) is left
  alone (`do nothing` — never allowed); any non-`.gitignore`-first ask that
  reaches `permission.asked` is rejected. The `reject` reply carries no custom
  text — that is why the hint lives in the before-stage throw.
- **Hook allow no-op vs ask UI.** On Cursor, a hook `allow` does not suppress
  the ask UI; on OpenCode, only config allows are silent.
- **Always-reply non-persistence.** OpenCode `always` replies do not persist
  across sessions; rely on the allowlist, not on remembered approvals.

### Cursor Auto-run allowlist maintenance checklist

The Cursor Auto-run allowlist is the Cursor silent-allow layer — keep it in
sync with the opencode allowlist intent (see `.cursor/hooks/AGENTS.md` for the
full checklist; `.agents/skills/gim-tools/references/harness-config.md` is the
source of truth). Essentials: git read forms (`git status`, `git log`,
`git diff`, `git show`, `git blame`, `git branch -l`, `git stash list`,
`git add -- <path...>`, `git commit -m <message>`), gh / acli read forms,
`Get-Content`, `Write-Output`,
the 18 global `gim-*` verbs (25 total across nine groups; the seven fleet
verbs are role-scoped), `.gitignore` token, Pester test tier, `Get-Help`,
`Test-NetConnection`. **Do NOT add the five block cmdlets** (`ForEach-Object`,
`Where-Object`, `Group-Object`, `Sort-Object`, `Select-Object`) — the hook
denies them with the replacement message.

Stacked-PR commands are version-controller-scoped: only `gh stack view`
(read-only) is kept in that agent's OpenCode frontmatter and the Cursor
checklist, never in the global OpenCode template. `gim-pr` calls
`init`/`add`/`link`/`sync` internally; those four are not on any agent
allowlist. Direct `add`, `link`, `submit`, `sync`, `push`, `merge`, and
`rebase` are consent-gated because they create branches/PRs, rewrite bases,
rebase, or force-push. `gh stack submit` is manual recovery only. Cursor's
Auto-run list is global, so Cursor may ask once the first time the
version-controller runs `gh stack view`; approve that one prompt.
