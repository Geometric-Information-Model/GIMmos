# Cursor hooks — harness docs

Two hooks live in this directory, both wired from `.cursor/hooks.json` and both
spawned as `pwsh -NoProfile -ExecutionPolicy Bypass -File <script>`:

- `gim-permissions.ps1` — the production deny-only permission hook (plan 0004
  C1, ADR 0007); the bulk of this document.
- `gim-preload.ps1` — the per-agent context preload hook (ADR 0009); see
  § [gim-preload](#gim-preload-context-preload) below.

Cursor re-reads hooks from `hooks.json` per call — no restart needed to apply
hook changes.

## Purpose

Agents run sanctioned commands without waiting on the user's Auto-run
allowlist, while denied constructs surface with the **replacement channel** (a
message naming the gim-tools verb to use) instead of a dead-end prompt. The
hook is the "discovery-at-failure" layer of
`.agents/skills/gim-tools/references/harness-config.md` on the Cursor side.

## Architecture (ends over means, ADR 0007)

- **The Auto-run allowlist is the only silent-allow layer.** The hook can
  never add allowlist entries, and a hook `allow` does **not** hide the ask UI
  (findings E1/E4). Happy path returns `{}` (silent) — never `allow`.
- **The hook is deny-only.** `preToolUse` returning
  `{permission:"deny", agent_message, user_message}` blocks the call *before*
  permission evaluation, skips `beforeShellExecution`, fires
  `postToolUseFailure` (`permission_denied`), and **beats the allowlist**
  (findings C1/E2). This is the only silent-deny layer.
- **No whitelist in the hook.** Cursor's mean is Auto-run allowlist +
  complement deny (findings C3): allowlist the family, and let the hook deny
  anything the allowlist would otherwise auto-run but the policy forbids.
- **Full string, one event.** Cursor sends one Shell call per event; chains
  (`;` / `&&` / `||`) are not split. A deny anywhere in the chain denies the
  whole call.
- **Decision logic is structural** over the central command tree, produced by
  the ONE parser lib (`.agents/skills/gim-tools/scripts/parse-command-tree.ps1`)
  — **dot-sourced in-process** (`Get-GimCommandTree`, no second pwsh spawn;
  the hook already runs under pwsh). Decisions are evaluated at
  command-candidate positions (command names, args, non-string tokens,
  envelope flags) — strings stay inert.

## How to add a rule

1. Add the decision predicate in `gim-permissions.ps1` (mirroring
   `.opencode/plugins/lib/gim-permissions-rules.js` — dual-sync rule).
   Decide structural facts from the tree: command names (`commands[].name`),
   args (`commands[].args`), non-string tokens (`commands[].tokens`), envelope
   flags (`hasSubstitution`, `staticCalls`), or the `memberCalls` envelope —
   never raw command text, so strings stay inert.
2. Wire it into `Get-GimDenyForTree` (rule order = first matching rule wins;
   decisions return `{ rule, reason, alternative }` per the message contract
   above — never a full framed message).
3. Add cases to `tests/gim-permissions.tests.ps1`.
4. Update this document.
5. Live-verify in Cursor (hook re-reads per call; no restart needed).

## Best practices

- **Structural matching over substring.** A string argument containing
  `ForEach-Object` must not deny. Check command names / non-string tokens.
- **Central lib only — never local parsing.** The hook dot-sources
  `.agents/skills/gim-tools/scripts/parse-command-tree.ps1` in-process. No
  second pwsh spawn, ever.
- **File-only logs.** `%TEMP%\cursor\gim-permissions.log` (capped ~5 MB,
  truncate-on-open when oversized). NEVER write anything but JSON to stdout.
- **JSON-only stdout.** `{}` (silent/pass) or
  `{"permission":"deny","agent_message":"...","user_message":"..."}`. A
  catch-all around the whole handler guarantees ANY error → `{}` — a broken
  hook must not block the IDE.
- **Per-event spawn cost.** Cursor spawns `pwsh -File` per event (~3 s). Keep
  `hooks.json` minimal: only the two pre-gates that decide (`preToolUse` +
  `beforeShellExecution`); post-events are not wired (they only log).
- **Fail-silent on parser failure.** If the central lib can't load or the tree
  can't be produced, the hook says `{}` and the Auto-run/ask flow takes over.
- **Failure-mode split (user-locked 2026-08-20).** Empty stdin payload →
  `{}` (fail-open — nothing received, nothing to decide). Non-empty
  unparseable payload (`ConvertFrom-Json` fails, e.g. malformed JSON or a
  mojibake-with-BOM prefix) → **generic deny** in the deny-message contract
  framing: "the permission hook could not interpret the permission payload
  (parse failed) — check `%TEMP%\cursor\gim-permissions.log`". The hook never
  silently passes on a payload it could not interpret.

## Deny message contract (user-locked)

Every deny (`agent_message` / `user_message`, incl. file-tool path denies)
uses the exact framing below so models never mistake a deny message for
command output. Harness name for this file: **cursor permission hook**.

```
Your command was not run: command '<cmd>' was denied by cursor permission hook. Reason: <reason>. Please try: <alternative>. This is NOT the command's output — this is the permission hook's error message.
```

- `<cmd>` = the attempted command text, truncated to ~120 chars with '…' when
  cut; for file-tool denies `<cmd>` = `<tool> <path>` (e.g. `Edit C:\repo\.env`).
- `<reason>` = short rule category (e.g. `cmdlet foreach-object (script-block
  built-in)`, `git force-push variant`, `secret-file path`, `pwsh/powershell
  invocation`).
- `<alternative>` = the replacement hint per rule.
- Rules (`Get-GimDenyForTree` decision objects) carry ONLY `rule` + `reason` +
  `alternative`; the wiring (`Format-GimDenyMessage` in this hook) builds the
  framed message. Never embed the full framed text in a rule.

## Deny rules — user-locked notes

- **pwsh / powershell command-position deny.** A command whose name is
  exactly `pwsh` or `powershell` is denied (`pwshInvocation`). Command
  position only: `Get-Command pwsh`, `git commit -m pwsh`, and
  `'pwsh'` strings are NOT denied. Agents never invoke pwsh — the harness
  shell already runs pwsh and scripts run in-shell (`& <path>` / pipeline /
  import); only the opencode plugin (JS, non-agent) may spawn `pwsh -File`.
- **Bare reflection markers are member-access-shape only.** Bare
  `GetMethod` / `GetField` / `GetType` / `Assembly` / `Invoke` match ONLY when
  the token is preceded by a `.` member-access token; barewords like
  `git commit -m GetMethod` stay inert. The real .NET surface is covered by
  the central lib `memberCalls` envelope + `::` staticCalls +
  `$ExecutionContext`.

## Dual-sync rule

Mirror the **intent** on the other harness (`.opencode/plugins/gim-permissions.js`
+ `lib/gim-permissions-rules.js` + `.opencode/plugins/AGENTS.md`) unless the
hook exists to handle a single-harness quirk. Document the exception in both
harness docs. Current exception: **the git `--` pathspec rule is
opencode-only** — Cursor has no such quirk (`git diff -- file` passes fine);
this hook has NO git `--` rule and NO `.gitignore` rule (do not add them).

## Cursor ↔ OpenCode mechanism differences

| Concern | OpenCode plugin | Cursor hook |
| --- | --- | --- |
| Deny-with-message | `tool.execute.before` **throw** (exact message, before permission eval) | `preToolUse` `{ permission: "deny", agent_message, user_message }` |
| Silent allow | config allowlist only | Auto-run allowlist (UI) only — hook `allow` does NOT hide the ask UI |
| Silent deny | `tool.execute.before` throw beats config | hook deny beats allowlist, skips `beforeShellExecution` |
| Command scope | per command string (before-stage); per segment (ask eval) | one event per full command string |
| Central lib call | spawns `pwsh -File` (lazy, cached) | dot-sources the lib in-process (no second spawn) |
| Parser output | JSON line on stdout | JSON line on stdout |
| Event cadence | `permission.asked` bus event + SDK reply | `preToolUse` / `beforeShellExecution` stdin JSON |
| Startup | plugins load at startup; restart to apply | hooks re-read from `hooks.json` per call |
| Git `--` quirk | before-stage throw + ask-stage reject | no quirk — no rule (documented exception) |

## Per-harness quirks (Cursor)

- **Hook allow no-op vs ask UI.** A `preToolUse` `permission:"allow"` does NOT
  suppress the ask UI and does NOT persist (findings E1/E4). The permission
  hook never returns `allow` — its pass is `{}` and silence relies on the
  Auto-run allowlist. The preload hook returns `allow` only alongside
  `updated_input` on an actual rewrite (see § gim-preload); that is a rewrite
  acknowledgment, not a permission decision.
- **Deny beats allowlist.** `preToolUse` deny stops allowlisted cmdlets
  (`Write-Output`, `git`, ...) — proven (findings E2).
- **Persistence is the hook staying loaded.** Hook decisions are re-evaluated
  every call; nothing is remembered (findings C5).
- **`.cursor/cli.json` is the Agent CLI surface**, not this IDE session; its
  deny syntax is unproven for the IDE agent and it is unused — the hook owns
  the deny layer. Do not reintroduce it.
- **`git -C` is case-sensitive** — `git -c` (config) must not be caught.

## Cursor Auto-run allowlist maintenance checklist

The Cursor Auto-run allowlist (Settings → Auto-run) is the Cursor silent-allow
layer — keep it in sync with the opencode allowlist intent
(`.agents/skills/gim-tools/references/harness-config.md` is the source of
truth). **Prefix-model warning:** Cursor matches by prefix — a bare entry like
`git branch` also matches `git branch -D`, and `git add` matches `git add -A`.
Always use the scoped forms below.

**Do NOT add the five block cmdlets** (`ForEach-Object`, `Where-Object`,
`Group-Object`, `Sort-Object`, `Select-Object`) — the hook denies them with
the replacement message (that deny must beat the allowlist).

User-maintained list:

- git read forms (scoped): `git status`, `git log`, `git diff`, `git blame`,
  `git show`, `git branch -l`, `git branch --list` (list-only; never bare
  `git branch`), `git stash list` (no args only)
- git write form (scoped): `git add -- <path...>` (explicit path scope;
  `-A` / `-f` are out — hook denies the destructive variants)
- git write form (scoped): `git commit -m <message>` (explicit message form;
  version-controller commits). This is a user-maintained entry — the user
  must add it to Cursor settings → Auto-run for silent commit; this checklist
  alone does not apply it.
- `git push` (plain — version-controller pushes; force-push variants are
  hook-denied)
- `gh pr view`, `gh pr diff`, `gh pr list`, `gh run view`, `gh issue view`
- `gh stack view` (read-only; version-controller stacked-PR inspection).
  **Only this stack command belongs here.** `gim-pr` calls
  `init`/`add`/`link`/`sync` internally. Direct `add`, `link`, `submit`,
  `sync`, `push`, `merge`, and `rebase` are consent-gated because they create
  branches/PRs, rewrite bases, rebase, or force-push.
- acli read forms
- `Get-Content`, `Write-Output`, `Get-Help`, `Test-NetConnection`
- the 18 globally allowlisted `gim-*` verbs (`gim-list`, `gim-where`,
  `gim-grep`, `gim-inspect`, `gim-count`, `gim-diff`, `gim-head`,
  `gim-report`, `gim-json`, `gim-asm`, `gim-verbs`, `gim-set`, `gim-get`,
  `gim-fix`, `gim-write`, `gim-apply`, `gim-run`, `gim-probe`). The toolkit
  totals 25 verbs across nine groups; the seven fleet verbs below are
  role-scoped, not global. Five are on this Cursor checklist; `gim-preflight`
  and `gim-pr` are not and may prompt once.
  - **`gim-preflight` is intentionally absent.** It is a version-controller
    verb kept only in that agent's OpenCode frontmatter (ADR 0008). Cursor's
    Auto-run list is global, so Cursor may ask once when the version-controller
    runs it; approve that one prompt (the verb never checks out, stashes,
    resets, or forces).
  - **`gim-pr` is intentionally absent.** It is a version-controller verb kept
    only in that agent's OpenCode frontmatter (ADR 0008). Its explicit file
    list, index guard, commit, push, PR create, and stack init/add/link/sync are owned by
    that role. Cursor's Auto-run list is global, so Cursor may ask once when the
    version-controller runs it; approve that one prompt (errors name the failed
    precondition and required input; the manual flow belongs to the agent body).
  - **`gim-wrapup-gather` is version-controller-scoped.** It is a fetch-only
    PR gather kept in that agent's OpenCode frontmatter and this checklist,
    never in the global OpenCode template. It writes the gather JSON, optional
    `logs/` files, and an owned `logs/` marker; it never posts or resolves.
  - **`gim-wrapup-respond` is version-controller-scoped.** It is the reply/
    resolve pass kept in that agent's OpenCode frontmatter and this checklist,
    never in the global OpenCode template. It posts per-entry responses,
    resolves only when requested, and records `last_error` on failures.
  - **`gim-jira-search`, `gim-jira-create`, and `gim-jira-transition` are
    tracker-scoped.** They are kept in the tracker's OpenCode frontmatter and
    this checklist, never in the global OpenCode template. Search is read-only
    (key, summary, status, assignee display name only); create is mutating and
    returns key + URL; transition is mutating, stops on the first failure, and
    treats an already-at-target status as a no-op.
  - **`gh stack view` is version-controller-scoped.** It is kept only in that
    agent's OpenCode frontmatter and this checklist, never in the global
    OpenCode template. The mutating stack commands (`add`, `link`, `submit`, `sync`,
    `push`, `merge`, `rebase`) stay consent-gated and are never added here.
- `.gitignore` (pathspec token)
- Pester test tier: `Import-Module Pester*`, `Invoke-Pester*`, `Should*`,
  `InModuleScope*` (exact multi-word rules never fire on name tokens — use
  wildcard forms) plus external-dir allow for the Pester install locations
  (`C:/Program Files/PowerShell/Modules/Pester/**`,
  `%USERPROFILE%/Documents/PowerShell/Modules/Pester/**`)

Never allowlist: `pwsh` / `powershell` (a `pwsh*` entry grants arbitrary
execution), `dotnet` raw execution verbs (gim-run is the only channel),
`Invoke-Expression` / `iex` / `Add-Type` / `Start-Process`, the five block
cmdlets.

## gim-preload (context preload)

Per-agent context preload for Cursor (decision record:
`docs/adr/agents/0009-AGENTS-context-preload.md`). One script,
`.cursor/hooks/gim-preload.ps1`, serves two hook events; both are wired in
`.cursor/hooks.json` beside the permission hook.

- **`sessionStart`** returns `{"additional_context":"<gim-preload ...>"}` with
  the `gim-orchestrator` preload (prefs plus `caveman`,
  `agentic-planning`, `grill-with-docs`, `review-with-critic`, `gim-tools`).
  The default Cursor chat is **implicitly the orchestrator**, so this hook
  never detects an agent name or type. The handler and its output are tested
  in-process; live **session-start delivery remains pending** — the Task path
  below is the verified one.
- **`preToolUse` with `"matcher": "Task"`** reads `tool_input.prompt` and
  `tool_input.subagent_type` (fallback `subagentType`), resolves that agent's
  entry in the shared config, and, only on a real injection, returns
  `{"permission":"allow","updated_input":{...}}` with the preload block and a
  separator prepended to `prompt`. Every other `tool_input` field is copied
  through unchanged; only `prompt` is replaced. The live probe passed: all
  eight agents returned their mapped block, no skill or read calls were made,
  and the log shows the subagent type and the expected file count (see the
  probe bullet below).
- **Pass-through vs injection (security).** Every no-op path — non-Task tool,
  unknown or missing agent type, missing/non-string/empty prompt, complete
  preload block already anchored at the prompt start, missing/unreadable
  config, unparseable stdin, or any internal error — returns `{}` (a silent
  pass-through with no rewrite), never `{"permission":"allow"}`. The hook
  returns `{"permission":"allow","updated_input":{...}}` only when it actually
  injects. That is a rewrite acknowledgment, **not a permission grant**: the
  preload hook never decides allow/deny, a hook `allow` does not hide the ask
  UI, and the permission hook remains the deny owner. This reconciles with the
  permission hook's "never return allow" rule: that rule governs the
  permission *decision*; the preload hook's `allow` exists only to carry
  `updated_input`, and its own pass is also `{}`.
- **Shared config.** `.agents/agent-preload.json` is the one map for both
  harnesses — the OpenCode plugin `.opencode/plugins/gim-preload.js` reads the
  same file. There is no Cursor-local copy. Skills resolve under
  `.agents/skills/<name>/SKILL.md`, then `.opencode/skills` and
  `.claude/skills`; `files` are relative to the repo root (or absolute).
  Skills come first, then files. Missing entries are skipped and logged by
  both handlers (`sessionStart` and `preToolUse`).
- **Block shape.** Matches the OpenCode plugin:
  `<gim-preload agent="<name>" files="<n>">`, the config header, one
  `<gim-file path="<project-relative>">` per file, `</gim-preload>`.
- **Marker idempotence.** If `prompt` already starts with a complete
  `<gim-preload ...>...</gim-preload>` block (leading whitespace allowed), the
  hook returns `{}` with no `updated_input`, so a retried or resumed prompt is
  never double-injected. A prompt that merely mentions the literal opening tag
  (no closing tag) is not treated as preloaded and still gets the block
  prepended, and neither is a prompt that quotes a complete block later in its
  text — the marker is anchored at the prompt start.
- **Fail-open, valid JSON only.** Missing/unreadable config, unknown agent,
  non-string prompt, unparseable stdin, or any internal error returns `{}` (no
  rewrite) — `sessionStart` also degrades to `{}`. A missing skill or file
  entry is not a no-op: it is logged and skipped, and the rest of the block
  still injects. A broken preload must never block a subagent or emit invalid
  JSON.
- **Diagnostic log.** `%TEMP%\cursor\gim-preload.log`, capped ~5 MB: event,
  tool name, subagent type, prompt length, `tool_input` key names, file
  count/bytes, and missing entry names (both handlers). Never the prompt text,
  `user_email`, or account ids.
- **`subagent_type` field-discovery probe (passed).** Cursor does not
  document the Task tool's input shape. The hook reads `subagent_type` first
  and `subagentType` second, and logs the observed key names. The live probe
  passed: all eight agents returned their mapped block, with no skill or read
  calls, and the log shows the subagent type and the expected file count. So
  `updated_input` does reach the spawned subagent.
- **Per-event spawn cost.** Cursor spawns `pwsh -File` per hook event (~3 s).
  Keep the preload wiring minimal: `sessionStart` plus `preToolUse` with the
  `Task` matcher only. The hook is registered with a `Task` matcher; if Cursor
  invokes it for another tool anyway, the handler returns `{}`.
- **Windows/PS compatibility.** The script is PowerShell 5.1- and
  7-compatible. Stdin mirrors the permission hook: StringReader-backed
  `[Console]::In` for in-process tests, otherwise `OpenStandardInput` +
  UTF-8 `StreamReader` when stdin is redirected (the live spawn), otherwise
  `''` — it never blocks on an interactive console. One leading U+FEFF is
  stripped defensively.
- **Tests.** `.cursor/hooks/tests/gim-preload.tests.ps1` (Pester) dot-sources
  the script with `-NoMain` and calls the handlers plus the in-process
  `Invoke-GimPreloadMain` entry function directly. Behavior tests use a temp
  fixture config/skill tree; a separate structural test only checks that the
  live `.agents/agent-preload.json` parses, has the eight agents, has no
  linter entry, and uses harness-neutral wording. Coverage: pass-through,
  injection, field preservation, `subagentType` fallback, session-start,
  garbage input, anchored complete-block marker idempotence (a bare opening
  tag, and a complete block quoted later in the prompt, still inject),
  `tool_input` supplied as a JSON string, `enabled` coercion (only a literal
  boolean `false` disables), missing entries, log redaction, and the stdin
  reader. It never spawns `pwsh`.
