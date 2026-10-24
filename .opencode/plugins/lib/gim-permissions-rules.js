// gim-permissions-rules.js — pure decision predicates for the opencode
// permission plugin (plan 0004 B1). Framework-free; unit-testable with
// fixture command trees.
//
// Every predicate consumes the JSON tree produced by the central parser lib
// (.agents/skills/gim-tools/scripts/parse-command-tree.ps1). This module
// NEVER re-implements command parsing and never inspects raw command text:
// decisions are evaluated at command-candidate positions only (command names,
// args, non-string tokens) — strings stay inert.
//
// Deny-only posture (ADR 0007 §1a): these predicates never produce an allow
// decision. The config allowlist is the only whitelist; the plugin may only
// throw (before-stage) or reject (ask-stage) or stay silent.
//
// Message contract (user-locked): decisions carry `rule` + `reason` +
// `alternative`; the WIRING builds the final deny message via
// formatDenyMessage() (`.opencode/plugins/gim-permissions.js` throw text, or
// the Cursor hook agent_message/user_message). Rules never embed the full
// framed text. The framed text always closes with: "This is NOT the command's
// output — this is the permission hook's error message."

export const BLOCKING_CMDLETS = [
  "foreach-object",
  "where-object",
  "group-object",
  "sort-object",
  "select-object",
]

export const CMDLET_ALTERNATIVES = {
  "foreach-object":
    "use gim-report --prefix <label> (or --prefix-each) instead",
  "where-object":
    "use gim-where --<predicate> (e.g. --exists, --matches <re>) instead",
  "group-object":
    "use gim-count --by <field> instead",
  "sort-object":
    "use gim-report --sort <field> instead",
  "select-object":
    "use gim-head -n N (--skip N / --last) or gim-json --path <query> instead",
}

// Command-position pwsh/powershell deny (user-locked): exact command-name
// match only — args/strings/`Get-Command pwsh` are NOT denied. Agents NEVER
// invoke pwsh (the harness shell already runs pwsh); the opencode plugin (JS,
// non-agent) is the only sanctioned spawner.
export const PWSH_COMMANDS = new Set(["pwsh", "powershell"])

export const REFLECTION_MARKERS = [
  "$ExecutionContext",
  ".GetType(",
  ".Assembly",
  "GetMethod",
  "GetField",
  ".Invoke(",
  "GetType",
  "Assembly",
  "Invoke",
]

// Bare member-name markers count ONLY in member-access shape (token preceded
// by a `.` token). The central lib's `memberCalls` envelope + `::`
// staticCalls + `$ExecutionContext` already cover the real .NET surface, so
// bare words containing these names (`git commit -m GetMethod`) stay inert.
export const BARE_REFLECTION_MARKERS = ["GetMethod", "GetField", "GetType", "Assembly", "Invoke"]
const BARE_REFLECTION_SET = new Set(BARE_REFLECTION_MARKERS.map((name) => name.toLowerCase()))

// Direct execution-escape verbs — no sanctioned replacement (harness-config
// deny list; B2 hook-denied).
export const EXECUTION_ESCAPES = ["invoke-expression", "iex", "add-type", "start-process"]

// String-literal token kinds to skip in reflection scans (strings stay
// inert). Actual TokenKind names as emitted by the central lib
// (parse-command-tree.ps1 `tokens[].kind`): single-quoted '...' =
// StringLiteral, expandable "..." = StringExpandable, here-strings =
// HereStringLiteral / HereStringExpandable.
const STRING_TOKEN_KINDS = new Set([
  "StringLiteral",
  "StringExpandable",
  "HereStringLiteral",
  "HereStringExpandable",
])

export const SECRET_FILE_PATTERNS = [
  /\.secret\.json$/,
  /appsettings\..*\.secret\.json$/,
  /(^|\/)\.env$/,
  /\.user$/,
  /(^|\/)id_rsa$/,
  /\.pfx$/,
  /\.snk$/,
]

// Cheap cost gate, evaluated BEFORE any pwsh spawn (lazy invocation). This
// only decides whether the central parser is worth invoking; over-matching
// is harmless (an extra spawn), under-matching would skip a deny. The real
// decision always comes from the parsed tree via denyDecision().
export const PRE_FILTER =
  /(ForEach-Object|Where-Object|Group-Object|Sort-Object|Select-Object|Invoke-Expression|\biex\b|Add-Type|Start-Process|\$\(|::|\$ExecutionContext|\.GetType\(|\.Assembly|GetMethod|GetField|\.Invoke\(|\.secret\.json|\.env|\.user|id_rsa|\.pfx|\.snk|\bgit\b|\bdotnet\b|\bpwsh\b|\bpowershell\b)/i

// Truncate a command for the deny message (~120 chars, append '…' when cut).
export function truncateCommand(text, max = 120) {
  const s = String(text ?? "")
  if (s.length <= max) return s
  return s.slice(0, max) + "…"
}

// Build the user-locked deny message framing (structure per plan; harness
// name per file: "opencode permission plugin" / "cursor permission hook").
export function formatDenyMessage(decision, command, harness) {
  const cmdText = truncateCommand(command)
  return (
    `Your command was not run: command '${cmdText}' was denied by ${harness}. ` +
    `Reason: ${decision.reason}. Please try: ${decision.alternative}. ` +
    `This is NOT the command's output — this is the permission hook's error message.`
  )
}

// Fail-closed parse-failure framing (user-locked 2026-08-20): the hook/plugin
// received a NON-EMPTY payload (Cursor) or a pre-filter-matched command whose
// central-lib tree could not be produced (opencode), so it cannot decide —
// deny with this generic contract message (no rule-specific alternative
// exists for an unreadable payload). The `<log path>` names the file-only log
// so the human/agent can inspect the raw bytes.
export function formatParseFailureMessage(command, harness, logPath) {
  const cmdText = truncateCommand(command)
  return (
    `Your command was not run: command '${cmdText}' was denied by ${harness}. ` +
    `Reason: the permission hook could not interpret the permission payload (parse failed) — check ${logPath}. ` +
    `This is NOT the command's output — this is the permission hook's error message.`
  )
}

// True when a central-lib result is NOT a parseable tree: null (spawn error /
// timeout / non-JSON output from the opencode plugin) or an `ok:false`
// envelope (the parser itself reported the command text as unparseable).
// Wiring must deny with formatParseFailureMessage() in this case; decision
// predicates below degrade to "no commands" and return null.
export function isParseFailure(tree) {
  return tree === null || tree === undefined || (tree && tree.ok === false)
}

function dec(rule, reason, alternative) {
  return { rule, reason, alternative }
}

function commandsOf(tree) {
  if (!tree || tree.ok === false) return []
  return Array.isArray(tree.commands) ? tree.commands : []
}

function nameOf(cmd) {
  return String(cmd?.name ?? "").toLowerCase()
}

function argsOf(cmd) {
  return Array.isArray(cmd?.args) ? cmd.args.map(String) : []
}

// True when arg index `i` is part of a git pathspec list (after a standalone
// `--`) on a TRUSTED-READ subcommand. Pathspec args of git READ commands are
// trusted by convention (harness-config "Git `--` pathspec convention");
// write-side subcommands (add, rm, ...) are NOT exempt — `git add -- .env`
// must still hit the secret-file rule.
function isGitPathspecArg(cmd, argIndex) {
  const dd = cmd?.dashDash
  if (nameOf(cmd) !== "git" || !dd || typeof dd.index !== "number" || argIndex < dd.index) return false
  const sub = String((cmd?.args ?? [])[0] ?? "").toLowerCase()
  return GIT_READ_SUBCOMMANDS.has(sub)
}

export function isSecretFilePath(value) {
  if (typeof value !== "string" || value.length === 0) return false
  const lower = value.replace(/\\/g, "/").toLowerCase()
  return SECRET_FILE_PATTERNS.some((re) => re.test(lower))
}

function secretFileArgHit(cmd) {
  const args = argsOf(cmd)
  for (let i = 0; i < args.length; i++) {
    if (isGitPathspecArg(cmd, i)) continue
    if (isSecretFilePath(args[i])) return args[i]
  }
  return null
}

// .NET/reflection markers inside a command's significant tokens, skipping
// string-literal tokens (strings stay inert). Bare member-name markers
// (GetMethod/GetField/GetType/Assembly/Invoke) count ONLY in member-access
// shape (previous significant token is `.`) — bareword args stay inert; the
// memberCalls envelope and `::` staticCalls cover real member access.
function reflectionHit(cmd) {
  const tokens = Array.isArray(cmd?.tokens) ? cmd.tokens : []
  let prevText = ""
  for (const token of tokens) {
    if (STRING_TOKEN_KINDS.has(token?.kind)) continue
    const text = String(token?.text ?? "")
    for (const marker of REFLECTION_MARKERS) {
      if (text.includes(marker)) {
        if (BARE_REFLECTION_SET.has(marker.toLowerCase())) {
          if (prevText !== ".") continue
        }
        return marker
      }
    }
    prevText = text
  }
  return null
}

// Reflection surface reported by the central lib's `memberCalls` envelope:
// member-access names ({GetType, Assembly, Invoke, GetMethod, GetField},
// case-insensitive) plus `$ExecutionContext` variable references. This is the
// structural channel that catches real parser output — member access
// tokenizes into separate `.` / identifier tokens, so substring markers never
// match it, and bare instance expressions ($x.Invoke(...)) have no CommandAst
// at all.
function memberCallsHit(tree) {
  const members = Array.isArray(tree?.memberCalls) ? tree.memberCalls : []
  for (const m of members) {
    const name = String(m ?? "")
    if (REFLECTION_MEMBER_SET.has(name.toLowerCase())) return name
  }
  for (const m of members) {
    if (String(m ?? "").toLowerCase() === "$executioncontext") return "$ExecutionContext"
  }
  return null
}

// Member-access names reported by the central lib's `memberCalls` envelope.
// These cover the .NET reflection surface that tokenizes apart (`.` then
// identifier), so substring markers never match on real output.
const REFLECTION_MEMBER_NAMES = ["GetType", "Assembly", "Invoke", "GetMethod", "GetField"]
const REFLECTION_MEMBER_SET = new Set(REFLECTION_MEMBER_NAMES.map((name) => name.toLowerCase()))

// Git subcommands whose `--` pathspec args are trusted-read (harness-config
// "Git `--` pathspec convention"). Write-side subcommands (e.g. `git add -- .env`)
// are NOT exempt from the secret-file rule.
const GIT_READ_SUBCOMMANDS = new Set(["status", "log", "diff", "blame", "show"])

function gitDestructiveHit(cmd) {
  const args = argsOf(cmd)
  if (args[0] === "branch") {
    if (args.includes("-D")) return "branch -D"
    if (args.includes("-d")) return "branch -d"
  }
  if (args[0] === "add") {
    if (args.includes("-A") || args.includes("--all")) return "add -A"
    if (args.includes("-f") || args.includes("--force")) return "add -f"
  }
  if (args[0] === "stash") {
    for (const variant of ["drop", "apply", "clear"]) {
      if (args.includes(variant)) return `stash ${variant}`
    }
  }
  return null
}

function gitForcePushHit(cmd) {
  const args = argsOf(cmd)
  // Force-push variants only (ADR 0007 §5 / harness-config): BOTH a `push`
  // subcommand and a force flag in the same git command — any arg position
  // (covers `git push --force`, `git push -f`, `git push push --force`,
  // `git push --force-with-lease`). Non-push git commands carrying -f / --force
  // (`git grep -f`, `git branch -f`, `git rm -f` ...) are NOT force-push; they
  // may still be caught by other rules (e.g. destructive git variants).
  if (!args.includes("push")) return false
  for (const a of args) {
    if (a === "-f" || a.startsWith("--force")) return true
  }
  return false
}

function dotnetExecHit(cmd) {
  const args = argsOf(cmd)
  if (args[0] === "nuget" && args[1] === "push") return "nuget push"
  if (args[0] === "nuget" && args[1] === "add" && args[2] === "source") return "nuget add source"
  if (args[0] === "tool" && args[1] === "install" && (args.includes("-g") || args.includes("--global"))) {
    return "tool install -g"
  }
  if (args[0] === "new" && args[1] === "install") return "new install"
  if (args[0] === "workload" && args[1] === "install") return "workload install"
  if (args[0] === "exec" || (args[0] && args[0].toLowerCase().endsWith(".dll"))) return "<path>.dll / exec"
  if (args[0] === "run") return "run"
  if (args[0] === "fsi" || args[0] === "script") return "fsi / script"
  if (args[0] === "msbuild" && args.some((a) => a.startsWith("-t:"))) return "msbuild -t:"
  if (args[0] === "publish" && args.some((a) => a === "-o" || a === "--output")) return "publish -o"
  return null
}

// Before-stage deny: return { rule, reason, alternative } when the command
// must be blocked, or null when the plugin has nothing to say (the config
// allowlist / ask flow takes over). First matching rule wins.
export function denyDecision(tree) {
  const commands = commandsOf(tree)

  for (const cmd of commands) {
    const name = nameOf(cmd)
    if (BLOCKING_CMDLETS.includes(name)) {
      return dec(`cmdlet.${name}`, `cmdlet ${name} (script-block built-in)`, CMDLET_ALTERNATIVES[name])
    }
    if (EXECUTION_ESCAPES.includes(name)) {
      return dec(
        `executionEscape.${name}`,
        `direct execution escape (${name})`,
        "direct execution escapes have no sanctioned replacement; use gim-* verbs, gim-run (or version-controller)",
      )
    }
    if (PWSH_COMMANDS.has(name)) {
      return dec(
        "pwshInvocation",
        "pwsh/powershell invocation",
        "your shell is already pwsh — run scripts in-shell: `& <path>` / pipeline / import (only the opencode plugin may spawn pwsh -File)",
      )
    }
  }

  if (tree?.hasSubstitution === true) {
    return dec(
      "substitution",
      "command substitution $(",
      "pass values through the gim-tools channel: gim-set / gim-get then --<param>-from-var",
    )
  }

  if (Array.isArray(tree?.staticCalls) && tree.staticCalls.includes("::")) {
    return dec(
      "staticCall",
      ":: static member access",
      "use gim-tools verbs; no script-block carriers",
    )
  }

  for (const cmd of commands) {
    const hit = reflectionHit(cmd)
    if (hit) {
      return dec(`reflection.${hit}`, `reflection access (${hit})`, "use gim-tools verbs (defense in depth)")
    }
  }

  const memberHit = memberCallsHit(tree)
  if (memberHit) {
    return dec(`reflection.${memberHit}`, `reflection access (${memberHit})`, "use gim-tools verbs (defense in depth)")
  }

  for (const cmd of commands) {
    const hit = secretFileArgHit(cmd)
    if (hit) {
      return dec(
        "secretFile",
        "secret-file path",
        "never read or write *.secret.json, appsettings.*.secret.json, .env, *.user, *.pubxml.user, **/id_rsa, **/*.pfx, **/*.snk",
      )
    }
  }

  for (const cmd of commands) {
    if (nameOf(cmd) !== "git") continue
    const hit = gitDestructiveHit(cmd)
    if (hit) {
      return dec(
        `gitDestructive.${hit}`,
        `destructive git variant (git ${hit})`,
        "use the two-turn pattern (read first, then act via gim-apply / version-controller)",
      )
    }
    if (gitForcePushHit(cmd)) {
      return dec(
        "gitForcePush",
        "git force-push variant",
        "force push requires explicit user consent; use version-controller (plain git push is allowed)",
      )
    }
    const args = argsOf(cmd)
    if (args[0] === "-C") {
      return dec("gitC", "git -C invocation", "use working_directory / cd instead")
    }
  }

  for (const cmd of commands) {
    if (nameOf(cmd) !== "dotnet") continue
    const hit = dotnetExecHit(cmd)
    if (hit) {
      return dec(
        `dotnetExec.${hit}`,
        `dotnet execution verb (dotnet ${hit})`,
        "run builds/tests/publish through gim-run (env scrub, timeout)",
      )
    }
  }

  return null
}

// Ask-stage decision for the git `--` pathspec quirk (ADR 0007 §3,
// opencode-only). KEEP as safety net: the before-stage throw (gitPathspecDecision)
// is now the primary mechanism (the reject reply carries no custom text), but
// any ask that still reaches permission.asked is decided here. Returns:
//   "reject" — first file after standalone `--` is NOT `.gitignore` → deny
//   "noop"   — first file IS `.gitignore` (edge: command not fully
//              allowlisted) → do nothing, let the ask continue to the UI
//   null     — not a git pathspec command → nothing to decide
export function gitDashAskDecision(tree) {
  for (const cmd of commandsOf(tree)) {
    if (nameOf(cmd) !== "git") continue
    if (cmd?.dashDash) {
      return cmd.dashDash.firstFileAfter === ".gitignore" ? "noop" : "reject"
    }
  }
  return null
}

// Before-stage decision for the git `--` pathspec convention (escalated,
// user-locked: the ask-stage `reject` reply carries no custom text, so the
// sacrificial-token hint must be a before-stage throw). A git command whose
// dashDash firstFileAfter is NOT exactly `.gitignore` (including null — `--`
// with no file after) denies with the hint. `.gitignore`-first passes
// silently; no dashDash → no deny. `git diff --no-index a b` is untouched
// (no standalone `--`).
export function gitPathspecDecision(tree) {
  for (const cmd of commandsOf(tree)) {
    if (nameOf(cmd) !== "git") continue
    if (cmd?.dashDash && cmd.dashDash.firstFileAfter !== ".gitignore") {
      return dec(
        "gitPathspec",
        "git pathspec without .gitignore first",
        "re-run `git <cmd> -- .gitignore <files>` or route through version-controller",
      )
    }
  }
  return null
}

// File-tool (edit/write) path predicate: deny secret-file paths regardless of
// how the tool names the path argument.
export function toolPathDecision(path) {
  if (isSecretFilePath(path)) {
    return dec(
      "secretFile",
      "secret-file path",
      "never read or write *.secret.json, appsettings.*.secret.json, .env, *.user, *.pubxml.user, **/id_rsa, **/*.pfx, **/*.snk",
    )
  }
  return null
}
