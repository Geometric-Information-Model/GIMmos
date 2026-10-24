// gim-permissions-rules.test.mjs — rules battery + integration test for the
// opencode permission plugin (plan 0004 B1). Run with plain node:
//
//   node .opencode/plugins/tests/gim-permissions-rules.test.mjs
//
// HUMAN-GATED (plan 0004 / prefs): the node suite is NOT run by gim-tester;
// run it manually after touching rules.js / gim-permissions.js wiring.
//
// Fixture trees exercise the pure predicates; one integration test spawns the
// REAL central parser lib (pwsh -NoProfile -File parse-command-tree.ps1)
// through the plugin's spawn path and asserts end-to-end decisions.

import { test } from "node:test"
import assert from "node:assert/strict"
import { spawn } from "node:child_process"
import { fileURLToPath } from "node:url"
import {
  BLOCKING_CMDLETS,
  PRE_FILTER,
  denyDecision,
  formatDenyMessage,
  formatParseFailureMessage,
  gitDashAskDecision,
  gitPathspecDecision,
  isParseFailure,
  isSecretFilePath,
  toolPathDecision,
  truncateCommand,
} from "../lib/gim-permissions-rules.js"

// ---------------------------------------------------------------------------
// fixture helpers
// ---------------------------------------------------------------------------

function cmd(name, args, extra = {}) {
  return { name, args, tokens: [], ...extra }
}

function tree(commands, extra = {}) {
  return {
    ok: true,
    parseErrors: [],
    hasSubstitution: false,
    substitutions: [],
    staticCalls: [],
    commands,
    ...extra,
  }
}

// ---------------------------------------------------------------------------
// five block-accepting cmdlets
// ---------------------------------------------------------------------------

test("denies each of the five block-accepting cmdlets at command position", () => {
  const cases = [
    ["ForEach-Object", "gim-report --prefix"],
    ["Where-Object", "gim-where --"],
    ["Group-Object", "gim-count --by"],
    ["Sort-Object", "gim-report --sort"],
    ["Select-Object", "gim-head"],
  ]
  for (const [name, hint] of cases) {
    const d = denyDecision(tree([cmd(name, [])]))
    assert.ok(d, `${name} must be denied`)
    assert.match(d.rule, /^cmdlet\./)
    assert.ok(d.alternative.includes(hint), `${name} alternative must name replacement: ${d.alternative}`)
  }
})

test("BLOCKING_CMDLETS is lower-case and complete", () => {
  assert.deepEqual(BLOCKING_CMDLETS, [
    "foreach-object",
    "where-object",
    "group-object",
    "sort-object",
    "select-object",
  ])
})

test("cmdlet names inside string arguments stay inert", () => {
  const d = denyDecision(
    tree([
      cmd("git", ["commit", "-m", "ForEach-Object loop"]),
      cmd("Write-Output", ["Where-Object"]),
    ]),
  )
  assert.equal(d, null)
})

test("cmdlet in nested script block still denies (depth > 0)", () => {
  const d = denyDecision(
    tree([cmd("ForEach-Object", [], { depth: 1, pipelineStage: 1 })]),
  )
  assert.ok(d)
  assert.equal(d.rule, "cmdlet.foreach-object")
})

// ---------------------------------------------------------------------------
// execution-escape verbs (no sanctioned replacement)
// ---------------------------------------------------------------------------

test("denies execution-escape verbs at command position", () => {
  for (const name of ["Invoke-Expression", "iex", "Add-Type", "Start-Process"]) {
    const d = denyDecision(tree([cmd(name, ["x"])]))
    assert.ok(d, `${name} must deny`)
    assert.match(d.rule, /^executionEscape\./)
    assert.ok(d.alternative.includes("gim-run"), `alternative names gim-run: ${d.alternative}`)
  }
})

test("execution-escape names inside strings stay inert", () => {
  const d = denyDecision(
    tree([cmd("git", ["commit", "-m", "Invoke-Expression and Start-Process"])]),
  )
  assert.equal(d, null)
})

// ---------------------------------------------------------------------------
// `$( )` command substitution
// ---------------------------------------------------------------------------

test("denies $() outside strings via hasSubstitution", () => {
  const d = denyDecision(tree([cmd("gim-where", ["--matches", "$(Get-Date)"])], { hasSubstitution: true }))
  assert.ok(d)
  assert.equal(d.rule, "substitution")
  assert.ok(d.alternative.includes("--<param>-from-var"))
})

test("does not deny when substitution appears only inside strings", () => {
  const d = denyDecision(tree([cmd("Write-Output", ["a $($x) b"])], { hasSubstitution: false }))
  assert.equal(d, null)
})

// ---------------------------------------------------------------------------
// `::` / reflection
// ---------------------------------------------------------------------------

test("denies :: outside strings via staticCalls", () => {
  const d = denyDecision(tree([cmd("gim-report", ["x"])], { staticCalls: ["::"] }))
  assert.ok(d)
  assert.equal(d.rule, "staticCall")
})

test(":: inside a string stays inert", () => {
  const d = denyDecision(tree([cmd("git", ["commit", "-m", "use :: in text"])], { staticCalls: [] }))
  assert.equal(d, null)
})

test("denies reflection markers on non-string tokens", () => {
  // markers that include the dot / variable prefix deny as single tokens
  for (const marker of ["$ExecutionContext", ".GetType(", ".Assembly", ".Invoke("]) {
    const d = denyDecision(tree([cmd("gim-report", ["x"], { tokens: [{ text: marker, kind: "Identifier" }] })]))
    assert.ok(d, `marker ${marker} must deny`)
    assert.match(d.rule, /^reflection\./)
  }
  // bare member-name markers require member-access shape (preceding `.`)
  for (const marker of ["GetMethod", "GetField", "GetType", "Assembly", "Invoke"]) {
    const d = denyDecision(tree([cmd("gim-report", ["x"], { tokens: [{ text: ".", kind: "Operator" }, { text: marker, kind: "Identifier" }] })]))
    assert.ok(d, `member-shaped ${marker} must deny`)
    assert.match(d.rule, /^reflection\./)
  }
})

test("reflection markers inside string tokens stay inert", () => {
  const d = denyDecision(
    tree([cmd("git", ["commit", "-m", "no"], { tokens: [{ text: '".GetType("', kind: "StringExpandable" }] })]),
  )
  assert.equal(d, null)
})

// NOTE: this suite is human-gated — the user runs
// `node .opencode/plugins/tests/gim-permissions-rules.test.mjs` (tester/CI
// does not run it). Cases below are the string-kind regression for the
// dual-home false-deny gap (single-quoted '...' + here-strings were missing
// from STRING_TOKEN_KINDS).
test("single-quoted string tokens stay inert (string-kind regression)", () => {
  const d = denyDecision(
    tree([cmd("Write-Output", [], { tokens: [
      { text: "'$ExecutionContext'", kind: "StringLiteral" },
      { text: "'ForEach-Object'", kind: "StringLiteral" },
      { text: "'::'", kind: "StringLiteral" },
      { text: "'GetType'", kind: "StringLiteral" },
    ] })]),
  )
  assert.equal(d, null)
})

test("single-quoted here-string tokens stay inert (string-kind regression)", () => {
  const d = denyDecision(
    tree([cmd("Write-Output", [], { tokens: [
      { text: "@'\n::\n'@", kind: "HereStringLiteral" },
    ] })]),
  )
  assert.equal(d, null)
})

// ---------------------------------------------------------------------------
// pwsh / powershell command-position deny (user-locked)
// ---------------------------------------------------------------------------

test("denies pwsh / powershell at command position (user-locked)", () => {
  for (const name of ["pwsh", "powershell"]) {
    const d = denyDecision(tree([cmd(name, ["-NoProfile", "-File", "x.ps1"])]))
    assert.ok(d, `${name} must deny`)
    assert.equal(d.rule, "pwshInvocation")
    assert.ok(d.reason.includes("pwsh/powershell invocation"), `reason: ${d.reason}`)
    assert.ok(d.alternative.includes("run scripts in-shell"), `alternative: ${d.alternative}`)
  }
})

test("pwsh / powershell as arg or string stays inert (command position only)", () => {
  assert.equal(denyDecision(tree([cmd("Get-Command", ["pwsh"])])), null)
  assert.equal(denyDecision(tree([cmd("git", ["commit", "-m", "pwsh"])])), null)
  assert.equal(denyDecision(tree([cmd("Write-Output", ["pwsh"])])), null)
  assert.equal(denyDecision(tree([cmd("Write-Output", ["powershell"])])), null)
})

// ---------------------------------------------------------------------------
// bare reflection markers — member-access shape only (critic Medium)
// ---------------------------------------------------------------------------

test("bare reflection markers in bareword args stay inert", () => {
  for (const word of ["GetMethod", "GetField", "GetType", "Assembly", "Invoke"]) {
    const d = denyDecision(tree([cmd("git", ["commit", "-m", word])]))
    assert.equal(d, null, `bareword ${word} must not deny`)
  }
  assert.equal(denyDecision(tree([cmd("gim-grep", ["--pattern", "GetMethod"])])), null)
  assert.equal(denyDecision(tree([cmd("Get-Content", ["src/GetField.cs"])])), null)
})

test("member-access-shaped bare markers still deny", () => {
  for (const word of ["GetMethod", "GetField", "GetType", "Assembly", "Invoke"]) {
    const d = denyDecision(tree([cmd("gim-report", ["x"], { tokens: [{ text: ".", kind: "Operator" }, { text: word, kind: "Identifier" }] })]))
    assert.ok(d, `member-shaped ${word} must deny`)
    assert.match(d.rule, /^reflection\./)
  }
  // memberCalls envelope still denies real member access
  assert.ok(denyDecision(tree([cmd("git", ["status"])], { memberCalls: ["GetMethod"] })))
})

// ---------------------------------------------------------------------------
// deny message contract (user-locked framing)
// ---------------------------------------------------------------------------

test("formatDenyMessage uses the user-locked framing", () => {
  const d = denyDecision(tree([cmd("git", ["push", "--force"])]))
  const msg = formatDenyMessage(d, "git push --force", "opencode permission plugin")
  assert.ok(
    msg.startsWith("Your command was not run: command 'git push --force' was denied by opencode permission plugin."),
    `prefix: ${msg}`,
  )
  assert.ok(msg.includes("Reason: git force-push variant."), `reason: ${msg}`)
  assert.ok(msg.includes("Please try:"), `alternative: ${msg}`)
  assert.ok(msg.includes("version-controller"), `hint: ${msg}`)
  assert.ok(
    msg.endsWith("This is NOT the command's output — this is the permission hook's error message."),
    `suffix: ${msg}`,
  )
})

test("formatDenyMessage truncates long commands to ~120 chars with ellipsis", () => {
  const long = 'Write-Output "' + "A".repeat(150) + '" | Where-Object { $_.Length }'
  const truncated = truncateCommand(long)
  assert.equal(truncated.length, 121) // 120 + ellipsis
  assert.ok(truncated.endsWith("…"))
  assert.equal(truncateCommand("git status"), "git status")
  const msg = formatDenyMessage({ reason: "x", alternative: "y" }, long, "opencode permission plugin")
  assert.ok(msg.includes("…"))
  assert.ok(!msg.includes("A".repeat(150)))
})

test("tool-path deny message uses <tool> <path> as command context", () => {
  const d = toolPathDecision("C:\\repo\\.env")
  const msg = formatDenyMessage(d, "edit C:\\repo\\.env", "opencode permission plugin")
  assert.ok(msg.includes("command 'edit C:\\repo\\.env' was denied by opencode permission plugin"), msg)
  assert.ok(msg.includes("Reason: secret-file path."), msg)
})

test("denies reflection member calls reported by the central lib", () => {
  for (const member of ["GetType", "Assembly", "Invoke", "GetMethod", "GetField"]) {
    const d = denyDecision(tree([cmd("git", ["status"])], { memberCalls: [member] }))
    assert.ok(d, `memberCalls ${member} must deny`)
    assert.match(d.rule, /^reflection\./)
  }
  // case-insensitive intersection + $ExecutionContext variable reference
  assert.ok(denyDecision(tree([cmd("git", ["status"])], { memberCalls: ["gettype"] })))
  assert.ok(denyDecision(tree([cmd("git", ["status"])], { memberCalls: ["$ExecutionContext"] })))
})

test("memberCalls unrelated members stay inert", () => {
  const d = denyDecision(tree([cmd("git", ["status"])], { memberCalls: ["Name", "Count", "Length"] }))
  assert.equal(d, null)
})

test("bare member expression fixture denies via memberCalls with no commands", () => {
  const t = tree([], { memberCalls: ["Invoke"] })
  const d = denyDecision(t)
  assert.ok(d)
  assert.equal(d.rule, "reflection.Invoke")
})

// ---------------------------------------------------------------------------
// secret-file paths
// ---------------------------------------------------------------------------

test("secret-file arg patterns deny on file-reading commands", () => {
  const cases = [
    ["Get-Content", [".env"]],
    ["Get-Content", ["appsettings.Production.secret.json"]],
    ["gim-write", ["--path", "secrets/appsettings.Private.secret.json"]],
    ["gim-list", ["config/appsettings.Local.user"]],
    ["Get-Content", ["C:/Users/x/.ssh/id_rsa"]],
    ["Get-Content", ["certs/dev.pfx"]],
    ["Get-Content", ["keys/strong-name.snk"]],
  ]
  for (const [name, args] of cases) {
    const d = denyDecision(tree([cmd(name, args)]))
    assert.ok(d, `${name} ${args.join(" ")} must deny`)
    assert.equal(d.rule, "secretFile")
    assert.ok(d.alternative.includes("never read or write"))
  }
})

test("git pathspec args are excluded from the secret-file rule (trusted-read)", () => {
  const d = denyDecision(
    tree([cmd("git", ["diff", "--", ".env"], { dashDash: { index: 2, firstFileAfter: ".env" } })]),
  )
  assert.equal(d, null)
})

test("write-side git pathspec is NOT excluded — git add -- .env denies", () => {
  const d = denyDecision(
    tree([cmd("git", ["add", "--", ".env"], { dashDash: { index: 2, firstFileAfter: ".env" } })]),
  )
  assert.ok(d)
  assert.equal(d.rule, "secretFile")
})

test("secret file outside a git pathspec still denies", () => {
  const d = denyDecision(tree([cmd("Get-Content", [".env"]), cmd("git", ["status"])]))
  assert.ok(d)
  assert.equal(d.rule, "secretFile")
})

test("isSecretFilePath normalizes Windows separators", () => {
  assert.equal(isSecretFilePath(".env"), true)
  assert.equal(isSecretFilePath("D:\\proj\\appsettings.Dev.secret.json"), true)
  assert.equal(isSecretFilePath("C:/Users/sbs/.ssh/id_rsa"), true)
  assert.equal(isSecretFilePath("notes.txt"), false)
  assert.equal(isSecretFilePath(""), false)
  assert.equal(isSecretFilePath("src/.gitignore"), false)
  assert.equal(isSecretFilePath("user.cs"), false)
  assert.equal(isSecretFilePath("App.config.user"), true)
  assert.equal(isSecretFilePath("web.pubxml.user"), true)
})

test("toolPathDecision denies secret-file paths for edit/write tools", () => {
  assert.ok(toolPathDecision("D:\\Work\\GIM\\GIMmos\\appsettings.secret.json"))
  assert.ok(toolPathDecision("C:/proj/.env"))
  assert.equal(toolPathDecision("D:\\Work\\GIM\\GIMmos\\src\\Program.cs"), null)
})

// ---------------------------------------------------------------------------
// destructive git variants
// ---------------------------------------------------------------------------

test("denies destructive git variants with the two-turn/version-controller message", () => {
  const cases = [
    ["branch", ["branch", "-D", "old"]],
    ["branch", ["branch", "-d", "old"]],
    ["add", ["add", "-A"]],
    ["add -A", ["add", "--all"]],
    ["add", ["add", "-f", "x.cs"]],
    ["add -f", ["add", "--force", "x.cs"]],
    ["stash", ["stash", "drop"]],
    ["stash", ["stash", "apply"]],
    ["stash", ["stash", "clear"]],
  ]
  for (const [variant, args] of cases) {
    const d = denyDecision(tree([cmd("git", args)]))
    assert.ok(d, `git ${args.join(" ")} must deny`)
    assert.match(d.rule, /^gitDestructive\./)
    assert.ok(d.alternative.includes("version-controller"), `alternative names version-controller: ${d.alternative}`)
    assert.ok(d.reason.includes(variant), `reason names variant ${variant}`)
  }
})

test("non-destructive git forms pass", () => {
  const safe = [
    ["branch", ["branch", "-l"]],
    ["branch", ["branch", "--list"]],
    ["add", ["add", "src/Program.cs"]],
    ["stash", ["stash", "list"]],
    ["stash", ["stash", "push", "-m", "wip"]],
  ]
  for (const args of safe) {
    const d = denyDecision(tree([cmd("git", args)]))
    assert.equal(d, null, `git ${args.join(" ")} must not deny`)
  }
})

// ---------------------------------------------------------------------------
// force-push variants (only)
// ---------------------------------------------------------------------------

test("denies force-push variants with consent message", () => {
  const cases = [
    ["push", ["push", "--force"]],
    ["push", ["push", "-f"]],
    ["push", ["push", "--force-with-lease"]],
    ["push", ["push", "--force-with-lease=main"]],
    ["push", ["push", "-u", "origin", "main", "--force"]],
    ["push", ["push", "push", "--force"]],
  ]
  for (const [, args] of cases) {
    const d = denyDecision(tree([cmd("git", args)]))
    assert.ok(d, `git ${args.join(" ")} must deny`)
    assert.equal(d.rule, "gitForcePush")
    assert.ok(d.alternative.includes("consent"), `alternative names consent: ${d.alternative}`)
  }
})

test("plain git push is NOT denied", () => {
  const d = denyDecision(tree([cmd("git", ["push"])]))
  assert.equal(d, null)
  const d2 = denyDecision(tree([cmd("git", ["push", "--set-upstream", "origin", "dev"])]))
  assert.equal(d2, null)
})

test("non-push git commands with -f / --force are NOT force-push", () => {
  const safe = [
    ["grep", ["grep", "-f", "patterns.txt", "src"]],
    ["branch", ["branch", "-f", "new"]],
    ["rm", ["rm", "-f", "x.cs"]],
  ]
  for (const args of safe) {
    const d = denyDecision(tree([cmd("git", args)]))
    assert.equal(d, null, `git ${args.join(" ")} must not deny as force-push`)
  }
})

// ---------------------------------------------------------------------------
// git -C
// ---------------------------------------------------------------------------

test("denies git -C with working_directory message", () => {
  const d = denyDecision(tree([cmd("git", ["-C", "..", "status"])]))
  assert.ok(d)
  assert.equal(d.rule, "gitC")
  assert.ok(d.alternative.includes("working_directory"))
})

// ---------------------------------------------------------------------------
// dotnet execution verbs
// ---------------------------------------------------------------------------

test("denies dotnet execution verbs with gim-run message", () => {
  const cases = [
    ["nuget", "push", "x.nupkg"],
    ["nuget", "add", "source", "s"],
    ["tool", "install", "-g", "dotnet-ef"],
    ["tool", "install", "--global", "dotnet-ef"],
    ["new", "install", "pkg"],
    ["workload", "install", "wasm-tools"],
    ["exec", "out.dll"],
    ["App.dll", "arg"],
    ["run", "--project", "src/App"],
    ["fsi", "x.fsx"],
    ["script", "x.fsx"],
    ["msbuild", "-t:", "Build"],
    ["publish", "-o", "out"],
    ["publish", "--output", "out"],
  ]
  for (const args of cases) {
    const d = denyDecision(tree([cmd("dotnet", args)]))
    assert.ok(d, `dotnet ${args.join(" ")} must deny`)
    assert.match(d.rule, /^dotnetExec\./)
    assert.ok(d.alternative.includes("gim-run"), `alternative names gim-run: ${d.alternative}`)
  }
})

test("dotnet build/test stay allowed", () => {
  const d = denyDecision(tree([cmd("dotnet", ["build", "GIMtransfer/GIMtransfer.csproj", "-c", "Debug", "-p:Platform=x64"])]))
  assert.equal(d, null)
  const d2 = denyDecision(tree([cmd("dotnet", ["test", "TestPlatform/TestPlatform.csproj"])]))
  assert.equal(d2, null)
})

// ---------------------------------------------------------------------------
// git `--` ask-stage decisions
// ---------------------------------------------------------------------------

test("rejects git pathspec ask when first file after -- is not .gitignore", () => {
  const t = tree([cmd("git", ["diff", "--", "src/Foo.cs"], { dashDash: { index: 2, firstFileAfter: "src/Foo.cs" } })])
  assert.equal(gitDashAskDecision(t), "reject")
})

test("does nothing when first file after -- is .gitignore", () => {
  const t = tree([cmd("git", ["diff", "--", ".gitignore", "src/Foo.cs"], { dashDash: { index: 2, firstFileAfter: ".gitignore" } })])
  assert.equal(gitDashAskDecision(t), "noop")
})

test("returns null when there is no git dashDash", () => {
  assert.equal(gitDashAskDecision(tree([cmd("git", ["status"])])), null)
  assert.equal(gitDashAskDecision(tree([cmd("git", ["diff", "--stat"])])), null)
  assert.equal(gitDashAskDecision(tree([cmd("ForEach-Object", [])])), null)
  assert.equal(gitDashAskDecision(null), null)
  assert.equal(gitDashAskDecision({ ok: false, parseErrors: ["x"], commands: [] }), null)
})

test("rejects when -- is the last arg (firstFileAfter null)", () => {
  const t = tree([cmd("git", ["diff", "--"], { dashDash: { index: 2, firstFileAfter: null } })])
  assert.equal(gitDashAskDecision(t), "reject")
})

// ---------------------------------------------------------------------------
// git `--` pathspec before-stage decision (user-locked escalation: the reject
// reply carries no text, so the hint must be a before-stage throw)
// ---------------------------------------------------------------------------

test("denies non-.gitignore-first git pathspec with the sacrificial-token hint", () => {
  const d = gitPathspecDecision(
    tree([cmd("git", ["diff", "--", "src/Foo.cs"], { dashDash: { index: 2, firstFileAfter: "src/Foo.cs" } })]),
  )
  assert.ok(d)
  assert.equal(d.rule, "gitPathspec")
  assert.ok(d.alternative.includes(".gitignore"), `alternative names .gitignore: ${d.alternative}`)
})

test("allows .gitignore-first git pathspec", () => {
  const t = tree([cmd("git", ["diff", "--", ".gitignore", "src/Foo.cs"], { dashDash: { index: 2, firstFileAfter: ".gitignore" } })])
  assert.equal(gitPathspecDecision(t), null)
})

test("denies `git diff --` with no file after (null firstFileAfter)", () => {
  const d = gitPathspecDecision(tree([cmd("git", ["diff", "--"], { dashDash: { index: 2, firstFileAfter: null } })]))
  assert.ok(d)
  assert.equal(d.rule, "gitPathspec")
})

test("does not deny non-standalone `--` flags", () => {
  const t = tree([cmd("git", ["diff", "--no-index", "a", "b"])])
  assert.equal(gitPathspecDecision(t), null)
})

test("does not deny git log -m -- .gitignore x", () => {
  const t = tree([cmd("git", ["log", "-m", "--", ".gitignore", "x"], { dashDash: { index: 3, firstFileAfter: ".gitignore" } })])
  assert.equal(gitPathspecDecision(t), null)
})

test("strings containing `-- wrongfile` stay inert", () => {
  const d = gitPathspecDecision(tree([cmd("git", ["commit", "-m", "-- wrongfile"])]))
  assert.equal(d, null)
})

test("no dashDash → no pathspec decision", () => {
  assert.equal(gitPathspecDecision(tree([cmd("git", ["status"])])), null)
  assert.equal(gitPathspecDecision(tree([cmd("ForEach-Object", [])])), null)
  assert.equal(gitPathspecDecision(null), null)
})

// ---------------------------------------------------------------------------
// compound commands — deny-only invariant (ADR 0007 §1a)
// ---------------------------------------------------------------------------

test("compound command with one dangerous part denies the whole", () => {
  const d = denyDecision(
    tree([
      cmd("git", ["push", "--force"]),
      cmd("git", ["status"]),
      cmd("Write-Output", ["ForEach-Object is a string"]),
    ]),
  )
  assert.ok(d)
  assert.equal(d.rule, "gitForcePush")
})

test("compound command of safe parts stays silent", () => {
  const d = denyDecision(
    tree([
      cmd("git", ["status"]),
      cmd("git", ["diff", "--stat"]),
      cmd("gim-list", ["--glob", "src/**/*.cs"]),
    ]),
  )
  assert.equal(d, null)
})

test("no decision predicate ever returns an allow (once/always)", () => {
  const batteries = [
    tree([cmd("ForEach-Object", [])]),
    tree([cmd("git", ["push", "-f"])]),
    tree([cmd("git", ["diff", "--", "foo.cs"], { dashDash: { index: 2, firstFileAfter: "foo.cs" } })]),
    tree([cmd("git", ["diff", "--", ".gitignore"], { dashDash: { index: 2, firstFileAfter: ".gitignore" } })]),
    tree([cmd("git", ["status"])]),
    tree([cmd("Write-Output", ["ForEach-Object"])]),
    tree([cmd("Get-Content", [".env"])]),
    tree([cmd("dotnet", ["publish", "-o", "out"])]),
  ]
  for (const t of batteries) {
    const before = denyDecision(t)
    const ask = gitDashAskDecision(t)
    assert.ok(before === null || (before.rule && typeof before.reason === "string"))
    assert.ok(ask === null || ask === "reject" || ask === "noop")
    assert.ok(ask !== "once" && ask !== "always")
  }
})

// ---------------------------------------------------------------------------
// PRE_FILTER cost gate
// ---------------------------------------------------------------------------

test("pre-filter catches everything the deny rules need", () => {
  const dangerous = [
    "ForEach-Object { $_ }",
    "Where-Object { $_.x }",
    "Group-Object name",
    "Sort-Object x",
    "Select-Object -First 5",
    "Invoke-Expression $x",
    "iex $x",
    "Add-Type -TypeDefinition $x",
    "Start-Process notepad",
    "git commit -m \"$(Get-Date)\"",
    "git push --force",
    "git -C ../x status",
    "git branch -D old",
    "git add -A",
    "dotnet nuget push x",
    "pwsh -NoProfile -File x.ps1",
    "powershell -Command x",
    "Get-Content .env",
    "$ExecutionContext.SessionState",
    "Write-Output $x.GetType()",
  ]
  for (const command of dangerous) {
    assert.equal(PRE_FILTER.test(command), true, `pre-filter must catch: ${command}`)
  }
})

test("pre-filter skips clearly safe commands", () => {
  const safe = ["Write-Output hello", "gim-list --glob x", "ls -la", "Get-Content README.md"]
  for (const command of safe) {
    assert.equal(PRE_FILTER.test(command), false, `pre-filter should skip: ${command}`)
  }
  // git/dotnet over-match by design (any git command may be a denied variant;
  // the tree decides, an extra spawn is harmless)
  assert.equal(PRE_FILTER.test("git status"), true)
  assert.equal(PRE_FILTER.test("dotnet build x"), true)
})

// ---------------------------------------------------------------------------
// integration: real central parser lib through the plugin's spawn path
// ---------------------------------------------------------------------------

const SCRIPT_URL = new URL(
  "../../../.agents/skills/gim-tools/scripts/parse-command-tree.ps1",
  import.meta.url,
)
const SCRIPT_PATH = fileURLToPath(SCRIPT_URL)

function parseCommandTree(command) {
  return new Promise((resolve, reject) => {
    const child = spawn(
      "pwsh",
      ["-NoProfile", "-File", SCRIPT_PATH, "-Command", command],
      { windowsHide: true, stdio: ["ignore", "pipe", "pipe"] },
    )
    let out = ""
    let err = ""
    child.stdout.on("data", (d) => {
      out += d
    })
    child.stderr.on("data", (d) => {
      err += d
    })
    child.on("error", reject)
    child.on("close", (code) => {
      try {
        resolve(JSON.parse(out.trim()))
      } catch {
        reject(new Error(`bad JSON exit=${code} stderr=${err}`))
      }
    })
  })
}

test("integration: git -- ask decisions against the real central lib", async () => {
  const rejectTree = await parseCommandTree("git diff -- src/Foo.cs")
  assert.equal(gitDashAskDecision(rejectTree), "reject")

  const noopTree = await parseCommandTree("git diff -- .gitignore src/Foo.cs")
  assert.equal(gitDashAskDecision(noopTree), "noop")

  const plainTree = await parseCommandTree("git status")
  assert.equal(gitDashAskDecision(plainTree), null)
  assert.equal(denyDecision(plainTree), null)
})

test("integration: denies fire with replacement messages", async () => {
  const cmdletTree = await parseCommandTree("Get-ChildItem | ForEach-Object { $_.Name }")
  const d = denyDecision(cmdletTree)
  assert.ok(d)
  assert.equal(d.rule, "cmdlet.foreach-object")
  assert.ok(d.alternative.includes("gim-report --prefix"))

  const pushTree = await parseCommandTree("git push --force")
  const d2 = denyDecision(pushTree)
  assert.ok(d2)
  assert.equal(d2.rule, "gitForcePush")
  assert.ok(d2.alternative.includes("consent"))

  const envTree = await parseCommandTree("Get-Content .env")
  const d3 = denyDecision(envTree)
  assert.ok(d3)
  assert.equal(d3.rule, "secretFile")

  const pubTree = await parseCommandTree("dotnet publish -o out")
  const d4 = denyDecision(pubTree)
  assert.ok(d4)
  assert.match(d4.rule, /^dotnetExec\./)
  assert.ok(d4.alternative.includes("gim-run"))
})

test("integration: reflection member calls on real parser output", async () => {
  const memberTree = await parseCommandTree("Write-Output $x.GetType().Assembly")
  const d1 = denyDecision(memberTree)
  assert.ok(d1, "member access must deny")
  assert.match(d1.rule, /^reflection\./)

  const bareTree = await parseCommandTree("$x.Invoke(\"hi\")")
  assert.equal(bareTree.commands.length, 0, "bare invocation has no CommandAst")
  const d2 = denyDecision(bareTree)
  assert.ok(d2, "bare Invoke must deny")
  assert.match(d2.rule, /^reflection\./)
})

test("integration: git -- pathspec before-stage decisions", async () => {
  const okTree = await parseCommandTree("git diff -- .gitignore A B")
  assert.equal(gitPathspecDecision(okTree), null)

  const denyTree = await parseCommandTree("git diff -- A B")
  const d = gitPathspecDecision(denyTree)
  assert.ok(d)
  assert.equal(d.rule, "gitPathspec")
  assert.ok(d.alternative.includes(".gitignore"), `alternative names .gitignore: ${d.alternative}`)

  // string content with `-- <file>` is not a pathspec — inert
  const inertTree = await parseCommandTree("git commit -m \"-- wrongfile\"")
  assert.equal(gitPathspecDecision(inertTree), null)
})

test("integration: string false positives stay inert", async () => {
  const t1 = await parseCommandTree("git commit -m \"ForEach-Object loop\"")
  assert.equal(denyDecision(t1), null)

  const t2 = await parseCommandTree("Write-Output \"::\"")
  assert.equal(denyDecision(t2), null)

  const t3 = await parseCommandTree("Write-Output \"$ExecutionContext is text\"")
  assert.equal(denyDecision(t3), null)
})

test("integration: single-quoted strings stay inert (string-kind regression)", async () => {
  const t1 = await parseCommandTree("Write-Output '$ExecutionContext'")
  assert.equal(denyDecision(t1), null)

  const t2 = await parseCommandTree("Write-Output 'GetType'")
  assert.equal(denyDecision(t2), null)

  const t3 = await parseCommandTree("Write-Output 'ForEach-Object'")
  assert.equal(denyDecision(t3), null)

  const t4 = await parseCommandTree("@'\n::\n'@")
  assert.equal(denyDecision(t4), null)
})

test("integration: pwsh / powershell command-position deny via real lib", async () => {
  const d1 = denyDecision(await parseCommandTree("pwsh -NoProfile -File x.ps1"))
  assert.ok(d1, "pwsh must deny")
  assert.equal(d1.rule, "pwshInvocation")

  const d2 = denyDecision(await parseCommandTree("& powershell -Command \"x\""))
  assert.equal(d2.rule, "pwshInvocation")

  assert.equal(denyDecision(await parseCommandTree("Get-Command pwsh")), null)
  assert.equal(denyDecision(await parseCommandTree("git commit -m pwsh")), null)
})

test("integration: bare reflection markers stay inert, member access denies", async () => {
  assert.equal(denyDecision(await parseCommandTree("git commit -m GetMethod")), null)
  assert.equal(denyDecision(await parseCommandTree("gim-grep --pattern GetMethod")), null)

  const member = denyDecision(await parseCommandTree("Write-Output $x.GetMethod()"))
  assert.ok(member, "member access must deny")
  assert.match(member.rule, /^reflection\./)
})

test("integration: compound command has no auto-allow path", async () => {
  const compound = await parseCommandTree("git push --force; git status; Write-Output \"ForEach-Object\"")
  const before = denyDecision(compound)
  assert.ok(before)
  assert.equal(before.rule, "gitForcePush")

  const safeCompound = await parseCommandTree("git status; git diff --stat; Write-Output \"ForEach-Object\"")
  assert.equal(denyDecision(safeCompound), null)
})

// ---------------------------------------------------------------------------
// fail-closed parse failure (user-locked 2026-08-20)
// ---------------------------------------------------------------------------

test("fail-closed: non-parseable tree -> generic parse-failure message (contract framing)", () => {
  const msg = formatParseFailureMessage(
    "git push --force origin main",
    "opencode permission plugin",
    "%TEMP%\\opencode\\gim-permissions.log",
  )
  assert.match(msg, /^Your command was not run: command 'git push --force origin main' was denied by opencode permission plugin\./)
  assert.match(msg, /could not interpret the permission payload \(parse failed\)/)
  assert.match(msg, /gim-permissions\.log/)
  assert.match(msg, /This is NOT the command's output — this is the permission hook's error message\.$/)
})

test("fail-closed: isParseFailure flags null and ok:false trees", () => {
  assert.equal(isParseFailure(null), true)
  assert.equal(isParseFailure(undefined), true)
  assert.equal(isParseFailure({ ok: false, parseErrors: ["boom"] }), true)
  assert.equal(isParseFailure(tree([])), false)
})

test("fail-closed: parse-failure message truncates long commands to ~120 chars", () => {
  const long = "x".repeat(200)
  const msg = formatParseFailureMessage(long, "opencode permission plugin", "log")
  assert.match(msg, /…/)
  assert.ok(!msg.includes("x".repeat(121)))
})

test("fail-closed: denyDecision on ok:false tree returns null (wiring denies via isParseFailure first)", () => {
  assert.equal(denyDecision({ ok: false, parseErrors: ["bad"] }), null)
})

test("integration: unparseable input -> ok:false envelope from the REAL central lib -> isParseFailure", async () => {
  const badTree = await parseCommandTree("'")
  assert.equal(badTree.ok, false)
  assert.ok(badTree.parseErrors.length > 0)
  assert.equal(isParseFailure(badTree), true)
  // the wiring denies via isParseFailure BEFORE denyDecision; denyDecision
  // still degrades cleanly (no crash, no wrong decision)
  assert.equal(denyDecision(badTree), null)
})
