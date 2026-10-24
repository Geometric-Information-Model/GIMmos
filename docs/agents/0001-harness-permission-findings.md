# 0001: Harness permission findings — curated probe record (opencode + Cursor)

Provenance: curated record distilled from the five probe findings files
(`allowlist-permission-findings-{cursor,opencode}.md`,
`permission-hook-findings-{cursor,opencode}.md`,
`permission-hook-methodology-cursor.md`), which were deleted after curation
(plan 0004, ADR 0007 §6). This doc is the surviving knowledge: version
stamps, payload shapes, event ordering, mechanism differences, quirks, and the
wired design decisions. It is a **reference**, not a status doc — implementation
status lives in `harness-config.md`.

- Date: 2026-08-20 (probe session 2026-08-19).
- Plan: `docs/plans/agents/0005-AGENTS-harness-permission-hooks.plan.md`; decisions locked in
  `docs/adr/agents/0007-AGENTS-harness-permission-wiring.md`.
- Batteries were run live against the real harnesses on Windows (pwsh).

## 1. Harness versions probed (version stamps)

| Harness | Version | Probed surface |
| --- | --- | --- |
| opencode | **1.18.18** (TUI), SDK **1.18.7** | `bash` tool permission gate + plugin event bus |
| Cursor | **3.16.29** (IDE Agent / `unifiedAgent`, hook runner pwsh Core 7.6.5) | Shell / Auto-run gate + `preToolUse` / `beforeShellExecution` hooks. Allowlist-only session earlier on 3.15.19 stable — treat matching-model verdicts as Cursor 3.x stable-series, re-verify per §15. |

Re-verify after any harness upgrade — payload shapes and matcher behavior drift
(§15).

## 2. Ends over means (ADR 0007 §2)

The two harnesses do **not** need identical mechanisms — only the same spirit.
Each side picks the more convenient mean for the same end. Both share the same
split:

- **allowlist = the only silent-allow layer** (opencode config allow /
  Cursor Auto-run allowlist UI).
- **hook/plugin = the only silent-deny-with-message layer** (opencode
  `tool.execute.before` throw / Cursor `preToolUse` `{permission:"deny"}`).
- Config denies (opencode) remain only as a plugin-less safety net with a
  generic message — never the message-carrying channel (lean deny posture,
  ADR 0007 §5).
- Dual-sync rule: mirror the *intent* on the other harness unless the hook
  exists to handle a single-harness quirk; document the exception (the git `--`
  rule is opencode-only, §10).

## 3. Allowlist matching models

### 3.1 opencode (1.18.18)

- **Membership is everything.** A command token either matches an allowlist
  pattern (runs) or it prompts. No fuzzy matching, no alias resolution, no
  value resolution.
- **Position-independent scanning.** Command invocations are gated anywhere on
  the line: top level, inside script blocks, after assignment, under `&`,
  inside parens, inside function bodies, at pipeline start.
- **Inert content is never scanned** (AST-aware, no substring matching):
  string literals, comments, string arguments, variables/member access/
  literals/operators (`$_`, `$var.Count`, `0`, `-lt`).
- **Keywords are NOT commands** — `if`, `for`, `while`, `switch`,
  `try`/`catch`, `foreach` run ungated.
- **Aliases are NOT resolved** — `gci` / `?` rejected despite allowlisted
  targets; literal-token matching only.
- **Variables / user functions in command position are checked by name.**
- **Allowlisted wrappers grant no pass to their contents** — inner commands
  checked independently.
- **Glued range `1..2` is a false positive** — one token, rejected. Spaced
  `1 .. 2` runs.
- 🔴 **.NET member invocations are NOT gated.** The checker is blind to
  `[Type]::Method()` / `[Type]::Property` — bare
  `[System.Math]::Sqrt(16)` ran with zero allowlisted commands and **no
  prompt** (`Assembly::LoadFrom`, `File::WriteAllText`,
  `Process::Start` all executable). As a safety boundary the allowlist is
  cosmetic; the gap is closed by the plugin's `::` / reflection deny
  (structural, from the central command tree). As a policy boundary it works.

### 3.2 Cursor (3.15.19 → 3.16.29 series)

- **Token membership shown in the UI.** A token is allowlisted (auto-run) or
  named in the approval card. No alias resolution, no value resolution.
- **Prefix + scattered token scan**, not a full AST: the starting prefix
  matters (`git`, `Get-ChildItem`, `Write-Output`); non-listed cmdlets are
  still found inside `{ }`, `( )`, function bodies, `if`/`while` bodies, after
  `&`; non-command tokens are often treated **as** commands (`$var`, `f`,
  `1`, `1..2`, `switch`, `try`, `.Count`, `::Sqrt`, `"a"` in `@()`).
- **`;` is split by the wrapper, not by PowerShell** — `… { Get-ChildItem |
  Out-Null; $_.Extension -eq ".md" }` becomes a second invocation of
  `$_.Extension` → PowerShell "not recognized as cmdlet". opencode keeps one
  script.
- **Keywords are inconsistent** — `if`/`while` auto-ran (body still scanned);
  `switch` and `try` were themselves not allowlisted; `foreach` unusable
  (`$n` / `"a"` flagged).
- **Aliases not resolved** (`gci`, `?` blocked); **variables and user
  functions checked by name**; **allowlisted wrappers grant no pass to inner
  cmdlets**.
- **Member / static-method tokens are command tokens** — `.Count` and
  `::Sqrt` prompt. This accidentally **closes opencode's .NET hole**: a
  `[Type]::Method()` call names `::Method` in the approval card. It is a
  policy prompt, not a kernel boundary — approval still runs it — but it is
  not the "ran with no prompt" failure opencode showed.
- Inert positions: string arguments and comments; `"a"` inside `@("a","b")`
  is scanned (false positive).

### 3.3 Shared practical rules

- Spell cmdlet names; never aliases (`gci`, `?`).
- Do not hide commands behind variables or functions — names checked as-is.
- Use spaced operators / array literals over glued ranges.
- Strings and comments stay inert (except Cursor array-literal false
  positives).

## 4. Hook / plugin event ordering

### 4.1 opencode (verified live via plugin log timestamps)

1. **`tool.execute.before`** — fires for **every** bash call, *before*
   permission evaluation (even for config-denied commands). Can mutate
   `output.args`, or `throw` → tool blocked with your exact message, no UI, no
   events, no execution.
2. **Config ruleset** (`permission.bash`) — **last matching rule wins**;
   `deny` → instant generic `PermissionDeniedError` (ruleset dump, no
   `permission.asked` event, no UI, no reply possible); `ask` → permission
   request → `permission.asked` bus event; `allow` → silent run.
3. **Ask resolution** — UI, or a plugin replying via the SDK endpoint.
4. Execution → `tool.execute.after`.

Evidence timestamps (one test run): `tool.before` → `permission.asked`
(8 ms later) → `permission.replied` → `tool.after`.

The legacy `permission.ask` hook is **declared in the SDK types but dead for
the bash flow** on 1.18.18 (zero invocations while the ask UI was up) — do not
rely on it.

### 4.2 Cursor (verified live, log timestamps)

1. `preToolUse` — fires for **every** tool; returning
   `{permission:"deny", agent_message, user_message}` blocks the call,
   **beats the allowlist**, skips `beforeShellExecution`, and fires
   `postToolUseFailure` (`permission_denied`). No ask UI.
2. `beforeShellExecution` (~3.3 s later) — shell-only gate; skipped when
   `preToolUse` denied.
3. `afterShellExecution` / `postToolUse`.
4. Hook `permission:"allow"` does **NOT** hide the ask UI (findings E1/E4) and
   does not persist — the Auto-run allowlist stays the only silent-allow
   layer.

Skip vs deny: user skip → empty `afterShellExecution` + `postToolUse` exit 0;
hook deny → `postToolUseFailure`, no shell hooks.

## 5. Payload shapes

### 5.1 opencode — `permission.asked` (bash)

```json
{
  "id": "per_...",
  "sessionID": "ses_...",
  "permission": "bash",
  "patterns": ["<segment 1>", "<segment 2>", ...],
  "metadata": { "command": "<full raw command string>" },
  "always": ["New-Item *", "Join-Path *", ...],
  "tool": { "messageID": "msg_...", "callID": "call_..." }
}
```

What the plugin receives: the event payload arrives via the `event` hook
(`event.type === "permission.asked"`, fields on `event.properties`, i.e. the
shape above). The plugin replies through the pre-wired SDK client:

```js
await client.postSessionIdPermissionsPermissionId({
  path: { id: p.sessionID, permissionID: p.id },
  body: { response: "always" },   // "once" | "always" | "reject"
})
```

`{"data":true}` = success; ask resolved ~15 ms later, UI never appears.
`permission.replied` event: `{ sessionID, requestID, reply: "once"|"always"|"reject" }`.

Raw `fetch(serverUrl)` fails in the opencode TUI on Windows — `serverUrl`
(`http://localhost:<port>`) resolves `localhost` to IPv6 `::1` while the
server binds IPv4 — so replies must go through the pre-wired SDK `client`,
and logging must be file-only (never `console.log` in the TUI).

### 5.2 Cursor — hook payloads (trimmed, shapes worth keeping)

`preToolUse` Shell:

```json
{ "tool_name": "Shell", "tool_input": { "command": "...", "cwd": "...", "timeout": 120 },
  "tool_use_id": "...", "hook_event_name": "preToolUse", "cursor_version": "3.16.29",
  "conversation_id": "...", "generation_id": "...", "session_id": "...",
  "workspace_roots": [ "..." ], ... }
```

`beforeShellExecution`:

```json
{ "command": "...", "cwd": "...", "sandbox": false,
  "hook_event_name": "beforeShellExecution", "cursor_version": "3.16.29", ... }
```

No `tool_name` on `beforeShellExecution`. `sandbox: false` on every shell hook
this session.

`postToolUseFailure` (hook deny):

```json
{ "error_message": "<custom deny text — em-dash folded to ASCII hyphen>",
  "failure_type": "permission_denied", "duration": 0, "is_interrupt": false,
  "tool_name": "Shell", "tool_input": { ... }, ... }
```

The hook **reads the payload from stdin** (mechanics lesson — §11) and writes
the decision JSON to stdout: `{}` (silent) or
`{"permission":"deny","agent_message":"...","user_message":"..."}`.

## 6. Mechanism differences — opencode plugin vs Cursor hook vs config allowlist

| Concern | opencode plugin | Cursor hook | Config allowlist (both) |
| --- | --- | --- | --- |
| Silent allow | never allows (deny-only, ADR 0007 §1a) | never returns `allow` (no-op against ask UI) | the ONLY silent-allow layer |
| Deny-with-message | `tool.execute.before` **throw** (exact message, before permission eval, no UI) | `preToolUse` `{permission:"deny", agent_message, user_message}` (beats allowlist, no UI) | — (opencode config `deny` = generic ruleset dump, no custom text) |
| Command scope | before-stage: whole command string; ask-stage: per segment (`patterns[]`) | one event per full command string (chains not split) | opencode: per segment of compound commands; Cursor: prefix on the full string |
| Central lib call | spawns `pwsh -File` (lazy pre-filter + per-command cache) | dot-sources the lib in-process (`Get-GimCommandTree`, no second spawn) | n/a |
| Event cadence | `tool.execute.before` per call; `permission.asked` bus event + SDK reply | `preToolUse` / `beforeShellExecution` stdin JSON per call | evaluated per call |
| Startup | plugins load at startup; restart to apply | hooks re-read from `.cursor/hooks.json` per call | config re-read per call |
| Matching | AST-aware token scan | prefix + scattered token scan | opencode: `*`/`?` globs, last-match-wins, AST-aware; Cursor: prefix-only |
| Persistence | nothing persists; `always` replies don't write rules | hook decisions re-evaluated every call | allowlist entries persist (that's their job) |

## 7. Compound-command hazard — the plugin never auto-allows (ADR 0007 §1a)

opencode evaluates compound commands (`;` / `&&` / `||`) **per segment**
(`patterns[]` in the `permission.asked` payload). A `permission.asked` reply
(`once`/`always`) approves the **ENTIRE** compound command, not the one segment
that asked:

- `someCommandWeAllow; DangerousCommand` produces one ask whose "always"
  reply would approve the dangerous part too.
- Allowing requires every part to be safe (hard); denying requires one
  offending part (easy).

Therefore: the config allowlist is the whitelist (a compound command only runs
when **every** segment matches an allow); the plugin/ask-stage handler **never
replies `allow`/`always`** — it may only `reject` (git `--` safety net, §10) or
do nothing (let the ask flow on). All conditional policy lives in
`tool.execute.before` throws (deny-with-message), which block the whole
command — a deny, hence safe.

Cursor has no analogous auto-allow hazard: chains arrive as one full string
and hook `allow` is a no-op against the ask UI, so there is no reply surface.

## 8. `always`-reply non-persistence

- opencode: a `New-Item` command **re-asked in a later call** despite earlier
  `always` replies; the session ruleset dump showed no `New-Item *` rule. The
  plugin must remain the source of truth — the ask recurs every time.
- Cursor: hook `allow` does not write Auto-run rules; `PERMTEST` re-asked;
  complement-deny does not persist either. Persistence = the hook staying
  loaded.

## 9. Config semantics

- **opencode: last-match-wins** (documented; observed live — a `"git -C*":
  "deny"` placed last in the allowlist-style ruleset denies correctly even
  though earlier broad rules match).
- **`ask` vs `deny`**: deny short-circuits before the event layer — a plugin
  cannot add a custom message to a config-denied command (the hook fires, but
  the denial text is the generic ruleset dump). Layered stance: config =
  static safety net (works plugin-less), hook = dynamic/conditional +
  messaging.
- **Cursor**: project `.cursor/cli.json` deny syntax is unproven for the IDE
  agent and was **unused** (a `Shell(git:-C*)` deny did not block; `git -C`
  auto-ran). The hook is the only silent-deny layer. Cursor's last-match /
  deny-precedence question is **UNKNOWN** (C7) — matching, if any, is hook
  `if` order.

## 10. Git `--` pathspec quirk + ask-stage design (opencode only)

- opencode's AST matcher treats the **first file token after `git <cmd> --`**
  as a command candidate; all subsequent path tokens are unchecked.
- Convention: `.gitignore` is allowlisted as a token, so `.gitignore`-first
  commands (`git diff -- .gitignore A B`) run silently (config allowlist, no
  `permission.asked` event).
- **Wired design (user-locked escalation 2026-08-20):**
  - `.gitignore`-first → no throw → silent run (plugin has nothing to do).
  - Non-`.gitignore`-first (first file after standalone `--` is NOT
    `.gitignore`, incl. `--` with no file after) → **`tool.execute.before`
    THROW** with the sacrificial-token hint: "git pathspec commands must use
    `.gitignore` as the first file after `--`; re-run `git <cmd> -- .gitignore
    <files>` or route through version-controller". Escalated because the SDK
    `reject` reply carries **no custom text** and the hint is required for
    discovery-at-failure.
  - `.gitignore`-first-but-still-asks edge (command not fully allowlisted) →
    before-stage does NOT throw; the `permission.asked` handler does
    **nothing** (ask continues to the UI — never auto-allow).
  - Ask-stage `reject` remains as a safety net for any ask that slips past
    before-stage.
- Security stance: path-scoping grants no real security (an agent can trivially
  drop the sacrificial token) — this is a matcher-quirk workaround, not a
  boundary.
- **Cursor: no quirk** — `git diff -- file` passes fine; the hook has NO git
  `--` rule and NO `.gitignore` rule (documented harness-specific exception).

## 11. Cursor stdin mechanics lesson (live bug, C1 — production port regression)

The Cursor hook payload arrives on **stdin** but **not** on `[Console]::In`.
Under a live Cursor spawn:

- ❌ `[Console]::In.ReadToEnd()` **yields nothing** — that was the production
  port regression.
- ✅ Read via `[Console]::OpenStandardInput()` through a `StreamReader` with an
  explicit UTF-8 encoding:
  ```powershell
  $reader = [System.IO.StreamReader]::new(
      [Console]::OpenStandardInput(),
      [System.Text.UTF8Encoding]::new($false))
  $raw = $reader.ReadToEnd()
  ```
- Guard with `[Console]::IsInputRedirected` before reading (an interactive
  console would block any `ReadToEnd` on EOF); keep a
  `[Console]::In.ReadToEnd()` fallback for in-process test callers
  (`[Console]::SetIn` + `StringReader`).
- **Strip the UTF-8 BOM** (`U+FEFF`) from the raw payload before
  `ConvertFrom-Json`.
- **JSON-only stdout** — `{}` or the deny object; the hook's catch-all
  guarantees ANY error → `{}` (a broken hook must not block the IDE).
- **File-only logs** (`%TEMP%\cursor\gim-permissions.log`, capped) — never
  pollute stdout.
- Cursor spawns `pwsh -File` per event (~3 s); keep `hooks.json` minimal
  (only the two pre-gates that decide).

**Failure-mode split (user-locked 2026-08-20):** empty payload / no command to
evaluate → **fail-open** (`{}` / no throw — nothing was received, nothing to
decide). Non-empty payload that cannot be parsed (Cursor: `ConvertFrom-Json`
fails — malformed JSON, mojibake-with-BOM prefix; opencode: pre-filter matched
but the central-lib spawn produced no parseable tree — spawn error / timeout /
non-JSON / `ok:false` envelope) → **generic deny** in the deny-message contract
framing: reason = "the permission hook could not interpret the permission
payload (parse failed) — check `<log path>`" (hook: `%TEMP%\cursor\
gim-permissions.log`; plugin: `%TEMP%\opencode\gim-permissions.log`). Both
harnesses never silently pass on a payload they could not interpret.

## 12. Deny message contract (user-locked)

Every deny message opens with explicit framing so models never mistake it for
command output:

```
Your command was not run: command '<cmd>' was denied by <opencode permission
plugin | cursor permission hook>. Reason: <reason>. Please try: <alternative>.
This is NOT the command's output — this is the permission hook's error message.
```

- `<cmd>` = attempted command text truncated to ~120 chars with '…' when cut;
  for file-tool denies, `<cmd>` = `<tool> <path>`.
- Rules carry only `rule` + `reason` + `alternative`; the wiring builds the
  framed message.
- Implemented identically in `.opencode/plugins/AGENTS.md` and
  `.cursor/hooks/AGENTS.md`.

## 13. Replacement table

| Denied | Use instead |
| --- | --- |
| `ForEach-Object { <label> }` loops | `gim-report --prefix <label>` / `--prefix-each` |
| `Where-Object { ... }` | `gim-where --<predicate>` (`--exists`, `--matches <re>`, ...) |
| `Group-Object` + count loops | `gim-count --by <field>` |
| `Sort-Object` | `gim-report --sort <field>` |
| `Select-Object -First/-Skip/-Last` | `gim-head -n N --skip N [--last]` |
| `Select-Object -ExpandProperty` | `gim-json --path <query>` |
| `$()` command substitution | `--<param>-from-var` (gim-set / gim-get) |
| `[Type]::Method()` / `::` / reflection | gim-tools verbs (no script-block carriers) |
| raw `dotnet` execution verbs | `gim-run` |
| `Invoke-Expression` / `iex` / `Add-Type` / `Start-Process` | no sanctioned replacement — gim-* verbs / `gim-run` |
| `git push --force` / `-f` / `--force-with-lease` | explicit user consent; version-controller (plain `git push` allowed) |
| `git -C` | `working_directory` / `cd` |
| `pwsh` / `powershell` at command position | run scripts in-shell — `& <path>` / pipeline / import (only the opencode plugin, JS non-agent, spawns `pwsh -File`) |

## 14. Privacy rule (user-locked, B2)

Committed configuration (templates, AGENTS.md, docs) references **only public
sibling repos** (GIMmanager, CoreWebApi, GIMocctWrapper). Closed/private
repos stay **machine-local only**: the gitignored
`GIMmos-private.code-workspace` (and `WORKSPACE-PRIVATE.md`) carries
private workspace wiring; never reference private paths in committed config.

## 15. Re-verify after upgrade

Harness upgrades change payload shapes and matcher behavior (opencode AST
matcher, Cursor hook payload fields, plugin SDK API). After any harness
upgrade, **re-run the batteries**:

- opencode: `tool.execute.before` fires before permission eval; deny throw
  message reaches the agent; `permission.asked` payload fields
  (`id` / `sessionID` / `patterns` / `metadata.command`) still parse; the SDK
  reply endpoint still resolves; `always` still does not persist; config
  last-match-wins still holds.
- Cursor: stdin payload still arrives via `OpenStandardInput()` + UTF-8
  StreamReader (BOM strip still needed); `preToolUse` deny still beats the
  allowlist and skips `beforeShellExecution`; `postToolUseFailure` shape
  unchanged; hook `allow` still a no-op against the ask UI.
- Both: aliases still unresolved, strings/comments still inert, .NET `::`
  surface still gated by the central-lib-driven deny, compound-command hazard
  unchanged (never auto-allow).
- Update this file's version stamps when a new probe confirms behavior.
