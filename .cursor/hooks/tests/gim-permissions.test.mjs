// Port of gim-permissions.tests.ps1. Same cases, node:test.
// Run: node --test .cursor/hooks/tests/gim-permissions.test.mjs

import { describe, test } from "node:test"
import assert from "node:assert/strict"
import { spawn } from "node:child_process"
import { fileURLToPath } from "node:url"
import path from "node:path"
import { denyDecision } from "../../../.opencode/plugins/lib/gim-permissions-rules.js"
import {
  decide,
  decisionJson,
  decodeUtf8Payload,
  extractCommandText,
  handleHook,
  normalizeStdinText,
} from "../gim-permissions.mjs"

const hookPath = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../gim-permissions.mjs")

function ruleFor(command, toolName = "Shell", filePath = "") {
  const decision = decide(command, toolName, filePath)
  return decision ? decision.rule : null
}

function jsonFor(command, toolName = "Shell", filePath = "") {
  return decisionJson(decide(command, toolName, filePath))
}

describe("five block-accepting cmdlets — deny with replacement message", () => {
  const cases = [
    ["Get-Content foo.txt | ForEach-Object { $_.Length }", "cmdlet.foreach-object", "gim-report"],
    ["Get-ChildItem | Where-Object { $_.Name }", "cmdlet.where-object", "gim-where"],
    ["Get-ChildItem | Group-Object Name", "cmdlet.group-object", "gim-count"],
    ["Get-ChildItem | Sort-Object Name", "cmdlet.sort-object", "gim-report --sort"],
    ["Get-ChildItem | Select-Object -First 5", "cmdlet.select-object", "gim-head"],
    ["Get-ChildItem | Select-Object -ExpandProperty Name", "cmdlet.select-object", "gim-json"],
  ]
  for (const [cmd, rule, msg] of cases) {
    test(`denies: ${cmd}`, () => {
      const decision = decide(cmd, "Shell", "")
      assert.ok(decision)
      assert.equal(decision.rule, rule)
      assert.match(decision.reason, /script-block built-in/)
      assert.match(decision.alternative, new RegExp(msg.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")))
      assert.equal(JSON.parse(decisionJson(decision)).permission, "deny")
    })
  }
})

describe("execution escapes — deny, no sanctioned replacement", () => {
  const cases = [
    ['Invoke-Expression "foo"', "executionEscape.invoke-expression"],
    ['iex "foo"', "executionEscape.iex"],
    ['Add-Type -TypeDefinition "x"', "executionEscape.add-type"],
    ["Start-Process notepad", "executionEscape.start-process"],
  ]
  for (const [cmd, rule] of cases) {
    test(`denies: ${cmd}`, () => {
      assert.equal(ruleFor(cmd), rule)
    })
  }
})

describe("substitution / static / reflection denies", () => {
  test("denies $() command substitution (unquoted — quoted literals excluded by the central lib)", () => {
    const decision = decide("Write-Output $(Get-Date)", "Shell", "")
    assert.equal(decision.rule, "substitution")
    assert.match(decision.reason, /command substitution/)
    assert.match(decision.alternative, /from-var/)
  })
  test("denies :: static member access", () => {
    const decision = decide("[System.IO.File]::ReadAllText('x')", "Shell", "")
    assert.equal(decision.rule, "staticCall")
    assert.match(decision.reason, /:: static member access/)
  })
  test("denies $ExecutionContext variable reference (memberCalls envelope)", () => {
    const decision = decide('$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(".")', "Shell", "")
    assert.match(decision.rule, /^reflection\./)
    assert.match(decision.reason, /reflection access/)
  })
  test("denies .GetType() member access (memberCalls envelope)", () => {
    const decision = decide("$x.GetType().FullName", "Shell", "")
    assert.match(decision.rule, /^reflection\./)
  })
  test("denies bare $x.Invoke(...) instance expression (memberCalls envelope)", () => {
    const decision = decide('$x.Invoke("hi")', "Shell", "")
    assert.match(decision.rule, /^reflection\./)
  })
})

describe("secret-file paths — deny in bash text and tool payloads", () => {
  const bashCases = [
    "Get-Content appsettings.development.secret.json",
    "Get-Content .env",
    "Copy-Item id_rsa backup",
    "Copy-Item cert.pfx backup",
    "Write-Output D:\\Work\\secrets\\.env",
  ]
  for (const cmd of bashCases) {
    test(`denies bash text: ${cmd}`, () => {
      const decision = decide(cmd, "Shell", "")
      assert.ok(decision)
      assert.equal(decision.rule, "secretFile")
      assert.match(decision.alternative, /never read or write/)
    })
  }
  test("denies git add -- <secret> (write-side not exempt)", () => {
    assert.equal(ruleFor("git add -- .env"), "secretFile")
  })
  test("skips git diff -- <secret> pathspec (trusted-read exemption)", () => {
    assert.equal(ruleFor("git diff -- .env A.cs"), null)
  })
  test("denies Edit tool payload secret file path", () => {
    const decision = decide("", "Edit", "C:\\repo\\.env")
    assert.ok(decision)
    assert.equal(decision.rule, "secretFile")
  })
  test("denies Write tool payload secret file path", () => {
    assert.equal(ruleFor("", "Write", "appsettings.prod.secret.json"), "secretFile")
  })
  test("passes Edit tool payload on normal path", () => {
    assert.equal(ruleFor("", "Edit", "C:\\repo\\src\\File.cs"), null)
  })
})

describe("destructive git variants — deny", () => {
  const cases = [
    ["git branch -D foo", "gitDestructive.branch -D"],
    ["git branch -d foo", "gitDestructive.branch -d"],
    ["git add -A", "gitDestructive.add -A"],
    ["git add --all .", "gitDestructive.add -A"],
    ["git add -f x", "gitDestructive.add -f"],
    ["git add --force x", "gitDestructive.add -f"],
    ["git stash drop", "gitDestructive.stash drop"],
    ["git stash apply", "gitDestructive.stash apply"],
    ["git stash clear", "gitDestructive.stash clear"],
  ]
  for (const [cmd, rule] of cases) {
    test(`denies: ${cmd}`, () => {
      const decision = decide(cmd, "Shell", "")
      assert.ok(decision)
      assert.equal(decision.rule, rule)
      assert.match(decision.alternative, /two-turn pattern/)
    })
  }
})

describe("force-push variants only — plain push passes", () => {
  test("denies git push --force", () => {
    assert.equal(ruleFor("git push --force origin main"), "gitForcePush")
  })
  test("denies git push -f", () => {
    assert.equal(ruleFor("git push -f origin main"), "gitForcePush")
  })
  test("denies git push --force-with-lease", () => {
    assert.equal(ruleFor("git push --force-with-lease origin main"), "gitForcePush")
  })
  test("denies force push inside a chain (deny beats allowlist: git status is allowlisted)", () => {
    assert.equal(ruleFor("git status; git push --force origin main"), "gitForcePush")
  })
  test("passes plain git push", () => {
    assert.equal(ruleFor("git push origin main"), null)
    assert.equal(ruleFor("git push"), null)
  })
  test("passes non-push git with -f (git grep -f, git branch -f)", () => {
    assert.equal(ruleFor("git grep -f pattern"), null)
    assert.equal(ruleFor("git branch -f foo bar"), null)
  })
})

describe("git -C — case-sensitive", () => {
  test("denies git -C (uppercase C)", () => {
    const decision = decide("git -C d:\\Work\\GIM\\GIMmos status -sb", "Shell", "")
    assert.equal(decision.rule, "gitC")
    assert.match(decision.alternative, /working_directory/)
  })
  test("passes git -c (lowercase c, config subcommand)", () => {
    assert.equal(ruleFor("git -c core.autocrlf=true status"), null)
  })
})

describe("dotnet execution verbs — deny, gim-run is the channel", () => {
  const cases = [
    "dotnet run",
    "dotnet nuget push x.nupkg",
    "dotnet nuget add source https://x",
    "dotnet tool install -g dotnet-ef",
    "dotnet new install Template",
    "dotnet workload install maui",
    "dotnet exec foo.dll",
    "dotnet foo.dll",
    "dotnet fsi script.fsx",
    "dotnet script x",
    "dotnet msbuild -t:Clean",
    "dotnet publish -o C:\\out",
    "dotnet publish --output C:\\out",
  ]
  for (const cmd of cases) {
    test(`denies: ${cmd}`, () => {
      const decision = decide(cmd, "Shell", "")
      assert.ok(decision)
      assert.match(decision.rule, /^dotnetExec\./)
      assert.match(decision.alternative, /gim-run/)
    })
  }
  test("passes dotnet build (allowed read/build verb)", () => {
    assert.equal(ruleFor("dotnet build GIMtransfer/GIMtransfer.csproj -c Debug -p:Platform=x64"), null)
  })
})

describe("allowlisted commands — deny beats allowlist, strings stay inert", () => {
  test("denies a block cmdlet even inside an otherwise-allowlisted command", () => {
    assert.equal(ruleFor('Write-Output "x" | ForEach-Object { $_.Length }'), "cmdlet.foreach-object")
  })
  test("passes strings containing cmdlet names (inert)", () => {
    assert.equal(ruleFor('Write-Output "ForEach-Object"'), null)
    assert.equal(ruleFor('Write-Output "git -C"'), null)
    assert.equal(ruleFor('Write-Output "git push --force"'), null)
  })
  test("passes allowlisted git reads (no git -- quirk rule on Cursor)", () => {
    assert.equal(ruleFor("git status"), null)
    assert.equal(ruleFor("git log -1 --oneline"), null)
    assert.equal(ruleFor("git diff -- A.cs B.cs"), null)
  })
  test("passes gim-* verbs and Pester tier", () => {
    assert.equal(ruleFor('gim-list --glob "**/*.cs"'), null)
    assert.equal(ruleFor('Get-Content foo.cs | gim-grep --stdin --pattern "x"'), null)
    assert.equal(ruleFor("Import-Module Pester -Force"), null)
    assert.equal(ruleFor("Invoke-Pester .cursor/hooks/tests/gim-permissions.tests.ps1"), null)
  })
})

describe("single-quoted strings stay inert (string-kind regression)", () => {
  test("passes single-quoted $ExecutionContext", () => {
    assert.equal(ruleFor("Write-Output '$ExecutionContext'"), null)
  })
  test("passes single-quoted GetType", () => {
    assert.equal(ruleFor("Write-Output 'GetType'"), null)
  })
  test("passes single-quoted ForEach-Object", () => {
    assert.equal(ruleFor("Write-Output 'ForEach-Object'"), null)
  })
  test("passes a single-quoted here-string containing ::", () => {
    const here = "@'\n::\n'@"
    assert.equal(ruleFor("Write-Output " + here), null)
  })
  test("passes double-quoted expandable string containing $ExecutionContext", () => {
    assert.equal(ruleFor('Write-Output "$ExecutionContext"'), null)
  })
})

describe("null/empty arg arrays — defensive guards (no catch-all reliance)", () => {
  test("passes bare git (empty args)", () => {
    assert.equal(ruleFor("git"), null)
  })
  test("passes bare dotnet (empty args)", () => {
    assert.equal(ruleFor("dotnet"), null)
  })
  test("Test-GimGitForcePush handles null args array", () => {
    const decision = denyDecision({ ok: true, commands: [{ name: "git", args: null }] })
    assert.equal(decision, null)
  })
  test("Test-GimGitForcePush handles args with a null element", () => {
    const decision = denyDecision({ ok: true, commands: [{ name: "git", args: [null] }] })
    assert.equal(decision, null)
  })
  test("Get-GimDotnetExecHit handles null args array", () => {
    const decision = denyDecision({ ok: true, commands: [{ name: "dotnet", args: null }] })
    assert.equal(decision, null)
  })
  test("Get-GimDotnetExecHit handles args with a null element", () => {
    const decision = denyDecision({ ok: true, commands: [{ name: "dotnet", args: [null] }] })
    assert.equal(decision, null)
  })
})

describe("deny message contract — user-locked framing", () => {
  test("bash deny message carries the exact framing", () => {
    const obj = JSON.parse(jsonFor("git push --force origin main"))
    assert.equal(obj.agent_message, obj.user_message)
    assert.match(obj.agent_message, /^Your command was not run: command 'git push --force origin main' was denied by cursor permission hook\./)
    assert.match(obj.agent_message, /Reason: git force-push variant\./)
    assert.match(obj.agent_message, /Please try:/)
    assert.match(obj.agent_message, /This is NOT the command's output \u2014 this is the permission hook's error message\.$/)
    assert.match(obj.agent_message, /version-controller/)
  })
  test("tool-path deny message uses <tool> <path> as the command context", () => {
    const obj = JSON.parse(jsonFor("", "Edit", "C:\\repo\\.env"))
    assert.match(obj.agent_message, /command .* was denied by cursor permission hook/)
    assert.match(obj.agent_message, /Edit C:\\repo\\.env/)
    assert.match(obj.agent_message, /Reason: secret-file path\./)
  })
  test("long command text is truncated to ~120 chars with ellipsis", () => {
    const long = `Write-Output "${"A".repeat(150)}" | Where-Object { $_.Length }`
    const obj = JSON.parse(jsonFor(long))
    assert.match(obj.agent_message, /\u2026/)
    assert.doesNotMatch(obj.agent_message, new RegExp("A".repeat(150)))
  })
  test("deny message explicitly says this is NOT the command output", () => {
    const obj = JSON.parse(jsonFor("git branch -D old"))
    assert.match(obj.agent_message, /This is NOT the command's output/)
    assert.match(obj.agent_message, /Reason: destructive git variant/)
  })
})

describe("pwsh / powershell command-position deny (user-locked)", () => {
  test("denies pwsh at command position", () => {
    const decision = decide("pwsh -NoProfile -File x.ps1", "Shell", "")
    assert.equal(decision.rule, "pwshInvocation")
    assert.match(decision.reason, /pwsh\/powershell invocation/)
    assert.match(decision.alternative, /run scripts in-shell/)
  })
  test("denies powershell at command position", () => {
    assert.equal(ruleFor('powershell -Command "x"'), "pwshInvocation")
  })
  test("denies & pwsh call-operator invocation", () => {
    assert.equal(ruleFor('& pwsh -Command "x"'), "pwshInvocation")
  })
  test("passes Get-Command pwsh (not a pwsh invocation)", () => {
    assert.equal(ruleFor("Get-Command pwsh"), null)
  })
  test("passes pwsh/powershell inside strings (inert)", () => {
    assert.equal(ruleFor("Write-Output 'pwsh'"), null)
    assert.equal(ruleFor("Write-Output 'powershell'"), null)
  })
  test("passes pwsh as an argument (not command position)", () => {
    assert.equal(ruleFor("git commit -m pwsh"), null)
  })
})

describe("bare reflection markers — member-access shape only (critic Medium)", () => {
  test("passes bareword args containing member names (inert)", () => {
    assert.equal(ruleFor("git commit -m GetMethod"), null)
    assert.equal(ruleFor("git commit -m GetField"), null)
    assert.equal(ruleFor("git commit -m GetType"), null)
    assert.equal(ruleFor("gim-grep --pattern GetMethod"), null)
    assert.equal(ruleFor("Get-Content src/GetField.cs"), null)
  })
  test("denies member access via the memberCalls envelope", () => {
    const decision = decide("$x.GetMethod()", "Shell", "")
    assert.match(decision.rule, /^reflection\./)
    assert.equal(decision.rule, "reflection.GetMethod")
    assert.equal(ruleFor("$x.GetField()"), "reflection.GetField")
  })
  test("denies static member access via :: / staticCalls", () => {
    assert.equal(ruleFor("[Type]::GetMethod('x')"), "staticCall")
  })
})

describe("payload shapes — preToolUse / beforeShellExecution / post events", () => {
  test("extracts command from preToolUse Shell tool_input", () => {
    const payload = {
      hook_event_name: "preToolUse",
      tool_name: "Shell",
      tool_use_id: "1",
      tool_input: { command: "git status", cwd: "D:\\repo", timeout: 120 },
    }
    assert.equal(extractCommandText(payload), "git status")
  })
  test("extracts command from beforeShellExecution payload.command", () => {
    const payload = {
      hook_event_name: "beforeShellExecution",
      command: "git -C d:\\x status",
      cwd: "D:\\repo",
      sandbox: false,
    }
    assert.equal(extractCommandText(payload), "git -C d:\\x status")
  })
  test("decides on preToolUse payload (deny)", () => {
    const out = handleHook('{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}')
    const obj = JSON.parse(out)
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /force push/)
  })
  test("decides on beforeShellExecution payload (deny)", () => {
    const out = handleHook('{"hook_event_name":"beforeShellExecution","command":"git -C d:\\\\x status"}')
    assert.equal(JSON.parse(out).permission, "deny")
  })
  test("passes on postToolUseFailure payload (post events silent)", () => {
    const payload = {
      hook_event_name: "postToolUseFailure",
      tool_name: "Shell",
      failure_type: "permission_denied",
      tool_input: { command: "ForEach-Object" },
    }
    assert.equal(handleHook(JSON.stringify(payload)), "{}")
  })
  test("denies Edit tool secret path from a full preToolUse payload", () => {
    const payload = {
      hook_event_name: "preToolUse",
      tool_name: "Edit",
      tool_input: { file_path: "C:\\repo\\.env", old_string: "x" },
    }
    const obj = JSON.parse(handleHook(JSON.stringify(payload)))
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /secret-file path/)
  })
  test("decides on a chained full-string payload as one event", () => {
    const out = handleHook('{"hook_event_name":"beforeShellExecution","command":"git status -sb; git log -1 --oneline"}')
    assert.equal(out, "{}")
  })
})

describe("malformed / empty input — fail-closed vs fail-open", () => {
  test("invalid JSON -> generic deny (fail-closed, non-empty unparseable)", () => {
    const obj = JSON.parse(handleHook("not json at all"))
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /parse failed/)
  })
  test("empty stdin -> {}", () => {
    assert.equal(handleHook(""), "{}")
  })
  test("whitespace stdin -> {}", () => {
    assert.equal(handleHook("   "), "{}")
  })
  test("null JSON -> {}", () => {
    assert.equal(handleHook("null"), "{}")
  })
  test("empty command text -> {}", () => {
    assert.equal(jsonFor(""), "{}")
  })
  test("missing payload -> {}", () => {
    assert.equal(jsonFor(null), "{}")
  })
})

describe("stdin robustness — BOM, whitespace, parse failures (live-spawn parity)", () => {
  test("parses payload with a leading UTF-8 BOM", () => {
    const json = '{"hook_event_name":"beforeShellExecution","command":"git push --force origin main"}'
    const obj = JSON.parse(handleHook(`\uFEFF${json}`))
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /git force-push variant/)
  })
  test("parses payload with leading whitespace", () => {
    const json = '   {"hook_event_name":"beforeShellExecution","command":"git push --force origin main"}'
    assert.equal(JSON.parse(handleHook(json)).permission, "deny")
  })
  test("parses payload with BOM + leading whitespace", () => {
    const json = JSON.stringify({
      hook_event_name: "preToolUse",
      tool_name: "Shell",
      tool_input: { command: "git push --force" },
    })
    assert.equal(JSON.parse(handleHook(`\uFEFF\n  ${json}`)).permission, "deny")
  })
  test("empty stdin -> {} (fail-open, parse=fail path)", () => {
    assert.equal(handleHook(""), "{}")
  })
  test("malformed JSON -> generic deny (fail-closed, non-empty unparseable)", () => {
    const obj = JSON.parse(handleHook("not json at all"))
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /parse failed/)
  })
})

describe("fail-closed: non-empty unparseable payload denies (user-locked 2026-08-20)", () => {
  test("unparseable JSON payload denies with the generic contract framing", () => {
    const obj = JSON.parse(handleHook("{not json"))
    assert.equal(obj.permission, "deny")
    assert.equal(obj.agent_message, obj.user_message)
    assert.match(obj.agent_message, /^Your command was not run: command '/)
    assert.match(obj.agent_message, /was denied by cursor permission hook\./)
    assert.match(obj.agent_message, /could not interpret the permission payload \(parse failed\)/)
    assert.match(obj.agent_message, /gim-permissions\.log/)
    assert.match(obj.agent_message, /This is NOT the command's output \u2014 this is the permission hook's error message\.$/)
  })
  test("mojibake-with-BOM payload denies (the v2 live-regression shape)", () => {
    const json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
    const obj = JSON.parse(handleHook(`\u00B4\u2557\u2510${json}`))
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /parse failed/)
  })
  test("truncates the raw payload to ~120 chars in the deny message", () => {
    const obj = JSON.parse(handleHook("{" + "A".repeat(200)))
    assert.match(obj.agent_message, /\u2026/)
    assert.doesNotMatch(obj.agent_message, new RegExp("A".repeat(121)))
  })
  test("empty stdin stays fail-open (unchanged)", () => {
    assert.equal(handleHook(""), "{}")
  })
  test("whitespace stdin stays fail-open (unchanged)", () => {
    assert.equal(handleHook("   "), "{}")
  })
})

describe("SetIn StringReader payloads parse without redirected process stdin (critic fix)", () => {
  test("normalizeStdinText keeps a direct in-process payload", () => {
    const raw = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
    const text = normalizeStdinText(raw)
    assert.ok(text)
    assert.match(text, /git push --force origin main/)
  })
  test("full hook round-trip parses a direct payload (deny decision)", () => {
    const json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
    const obj = JSON.parse(handleHook(json))
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /force push/)
  })
})

describe("byte-level UTF-8 BOM decode + defensive U+FEFF strip (live regression 2026-08-20)", () => {
  test("decodes raw EF BB BF + UTF-8 payload bytes via the decode path", () => {
    const json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
    const decoded = decodeUtf8Payload(Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), Buffer.from(json)]))
    assert.ok(decoded)
    assert.notEqual(decoded.charCodeAt(0), 0xfeff)
    assert.match(decoded, /git push --force origin main/)
  })
  test("byte-level BOM payload parses end-to-end (decoded -> hook deny)", () => {
    const json = '{"hook_event_name":"beforeShellExecution","command":"git push --force origin main"}'
    const decoded = decodeUtf8Payload(Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), Buffer.from(json)]))
    const obj = JSON.parse(handleHook(decoded))
    assert.equal(obj.permission, "deny")
    assert.match(obj.agent_message, /force push/)
  })
  test("string U+FEFF prefix payload parses (defensive strip)", () => {
    const json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
    assert.equal(JSON.parse(handleHook(`\uFEFF${json}`)).permission, "deny")
  })
  test("decode path must not produce console-codepage mojibake (the v2 regression)", () => {
    const json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
    const decoded = decodeUtf8Payload(Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), Buffer.from(json)]))
    assert.doesNotMatch(decoded, /\u00B4\u2557\u2510/)
    assert.match(decoded, /^\{/)
  })
})

describe("output contract — JSON only", () => {
  test("pass decision is exactly {}", () => {
    assert.equal(jsonFor("git status"), "{}")
  })
  test("deny decision JSON carries permission/agent_message/user_message", () => {
    const obj = JSON.parse(jsonFor("git push --force"))
    assert.equal(obj.permission, "deny")
    assert.equal(obj.agent_message, obj.user_message)
    assert.match(obj.user_message, /force push/)
  })
  test("CLI stdout is one JSON document and stderr is empty", async () => {
    const proc = spawn(process.execPath, ["--disable-warning=MODULE_TYPELESS_PACKAGE_JSON", hookPath], { stdio: ["pipe", "pipe", "pipe"] })
    const stdout = []
    const stderr = []
    proc.stdout.on("data", (chunk) => stdout.push(chunk))
    proc.stderr.on("data", (chunk) => stderr.push(chunk))
    proc.stdin.end('{"hook_event_name":"beforeShellExecution","command":"git status"}')
    const code = await new Promise((resolve) => proc.on("close", resolve))
    assert.equal(code, 0)
    assert.equal(Buffer.concat(stderr).toString("utf8"), "")
    assert.equal(Buffer.concat(stdout).toString("utf8"), "{}\n")
  })
})
