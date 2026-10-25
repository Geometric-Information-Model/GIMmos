// gim-permissions.mjs — Cursor deny-only permission hook.
//
// Same contract as the previous pwsh hook: JSON on stdin, JSON on stdout,
// `{}` for pass, deny object for block. Decisions come from the shared rules
// module (one rule table with the OpenCode plugin). Command trees are built
// here so a hook event does not pay for a pwsh process.
//
// Cursor does not apply the OpenCode git `--` pathspec rule. Strings stay
// inert. Any internal error returns `{}`.

import fs from "node:fs"
import path from "node:path"
import os from "node:os"
import { fileURLToPath } from "node:url"
import {
  denyDecision,
  formatDenyMessage,
  formatParseFailureMessage,
  toolPathDecision,
} from "../../.opencode/plugins/lib/gim-permissions-rules.js"

const HARNESS = "cursor permission hook"
const LOG_LIMIT = 5 * 1024 * 1024

function logPath() {
  const temp = process.env.TEMP || process.env.TMP || os.tmpdir()
  return path.join(temp, "cursor", "gim-permissions.log")
}

function log(line) {
  try {
    const file = logPath()
    fs.mkdirSync(path.dirname(file), { recursive: true })
    if (fs.existsSync(file) && fs.statSync(file).size > LOG_LIMIT) fs.writeFileSync(file, "")
    fs.appendFileSync(file, `${new Date().toISOString()} ${line}\n`)
  } catch {
    // logging is best-effort
  }
}

export function decodeUtf8Payload(bytes) {
  const buf = Buffer.isBuffer(bytes) ? bytes : Buffer.from(bytes)
  let start = 0
  if (buf.length >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) start = 3
  let text = buf.subarray(start).toString("utf8")
  if (text.charCodeAt(0) === 0xfeff) text = text.slice(1)
  return text
}

export function normalizeStdinText(raw) {
  let text = String(raw ?? "")
  if (text.charCodeAt(0) === 0xfeff) text = text.slice(1)
  return text
}

function collapseSnippet(raw) {
  let s = String(raw ?? "")
  if (s.length > 200) s = s.slice(0, 200) + "\u2026"
  let out = ""
  for (const ch of s) {
    const code = ch.codePointAt(0)
    out += code < 32 || code === 127 ? " " : ch
  }
  return out
}

function isWordBoundary(input, i) {
  const c = input[i]
  if (c === " " || c === "\t" || c === "\f" || c === "\v" || c === "\r" || c === "\n") return true
  if ("|;{}()\"'#,".includes(c)) return true
  if (c === "$" && input[i + 1] === "(") return true
  if (input.startsWith("&&", i) || input.startsWith("||", i)) return true
  if (c === "<" && input.startsWith("<#", i)) return true
  return false
}

function scanSingle(input, i) {
  const start = i
  let value = ""
  i++
  while (i < input.length) {
    if (input[i] === "'" && input[i + 1] === "'") {
      value += "'"
      i += 2
      continue
    }
    if (input[i] === "'") {
      i++
      break
    }
    value += input[i]
    i++
  }
  const raw = input.slice(start, i)
  return {
    type: "string",
    start,
    end: i,
    raw,
    text: raw,
    value,
    quoted: true,
    kind: "StringLiteral",
  }
}

function scanDouble(input, i) {
  const start = i
  let value = ""
  i++
  while (i < input.length) {
    if (input[i] === "`" && i + 1 < input.length) {
      value += input[i + 1]
      i += 2
      continue
    }
    if (input[i] === '"') {
      i++
      break
    }
    value += input[i]
    i++
  }
  const raw = input.slice(start, i)
  return {
    type: "expandable",
    start,
    end: i,
    raw,
    text: raw,
    value,
    quoted: true,
    kind: "StringExpandable",
  }
}

function scanHereString(input, i) {
  const quote = input[i + 1]
  let j = i + 2
  if (input[j] === "\r") j++
  if (input[j] !== "\n") return null
  const contentStart = j + 1
  let lineStart = contentStart
  let end = input.length
  for (let p = contentStart; p <= input.length; p++) {
    if (p === input.length || input[p] === "\n") {
      let lineEnd = p
      if (lineEnd > lineStart && input[lineEnd - 1] === "\r") lineEnd--
      if (input.slice(lineStart, lineEnd) === quote + "@") {
        end = lineStart + 2
        break
      }
      lineStart = p + 1
    }
  }
  const raw = input.slice(i, end)
  const kind = quote === "'" ? "HereStringLiteral" : "HereStringExpandable"
  return {
    type: quote === "'" ? "string" : "expandable",
    start: i,
    end,
    raw,
    text: raw,
    value: input.slice(contentStart, Math.max(contentStart, end - 2)),
    quoted: true,
    kind,
  }
}

function tokenize(input) {
  const tokens = []
  const excluded = []
  const n = input.length
  let i = 0
  while (i < n) {
    const c = input[i]
    if (c === " " || c === "\t" || c === "\f" || c === "\v") {
      i++
      continue
    }
    if (c === "\r") {
      i++
      continue
    }
    if (c === "\n") {
      tokens.push({ type: "newline", start: i, end: i + 1, raw: "\n", text: "\n", op: "\n", kind: "NewLine" })
      i++
      continue
    }
    if (c === "#" || (c === "<" && input.startsWith("<#", i))) {
      const start = i
      if (c === "#") {
        while (i < n && input[i] !== "\n") i++
      } else {
        i += 2
        const close = input.indexOf("#>", i)
        i = close < 0 ? n : close + 2
      }
      if (i > start) excluded.push({ start, end: i })
      continue
    }
    if (c === "@" && (input[i + 1] === "'" || input[i + 1] === '"')) {
      const here = scanHereString(input, i)
      if (here) {
        tokens.push(here)
        excluded.push({ start: here.start, end: here.end })
        i = here.end
        continue
      }
    }
    if (c === "'") {
      const tok = scanSingle(input, i)
      tokens.push(tok)
      excluded.push({ start: tok.start, end: tok.end })
      i = tok.end
      continue
    }
    if (c === '"') {
      const tok = scanDouble(input, i)
      tokens.push(tok)
      excluded.push({ start: tok.start, end: tok.end })
      i = tok.end
      continue
    }
    if (input.startsWith("&&", i) || input.startsWith("||", i)) {
      const raw = input.slice(i, i + 2)
      tokens.push({ type: "op", start: i, end: i + 2, raw, text: raw, op: raw, kind: "Operator" })
      i += 2
      continue
    }
    if (c === "|" || c === ";") {
      tokens.push({ type: "op", start: i, end: i + 1, raw: c, text: c, op: c, kind: "Operator" })
      i++
      continue
    }
    if (c === "{" || c === "}" || c === "(" || c === ")") {
      tokens.push({ type: "group", start: i, end: i + 1, raw: c, text: c, op: c, kind: "Group" })
      i++
      continue
    }
    if (c === "$" && input[i + 1] === "(") {
      tokens.push({ type: "group", start: i, end: i + 2, raw: "$(", text: "$(", op: "$(", kind: "Operator" })
      i += 2
      continue
    }
    if (c === "&") {
      tokens.push({ type: "op", start: i, end: i + 1, raw: "&", text: "&", op: "&", kind: "Operator" })
      i++
      continue
    }
    const start = i
    i++
    while (i < n && !isWordBoundary(input, i)) i++
    const raw = input.slice(start, i)
    tokens.push({
      type: "word",
      start,
      end: i,
      raw,
      text: raw,
      value: raw,
      quoted: false,
      kind: "Generic",
    })
  }
  return { tokens, excluded }
}

function maskExcluded(input, ranges) {
  const chars = input.split("")
  for (const range of ranges) {
    for (let i = range.start; i < range.end && i < chars.length; i++) chars[i] = " "
  }
  return chars.join("")
}

function addUnique(list, value) {
  if (value && !list.includes(value)) list.push(value)
}

function collectMembers(masked) {
  const memberCalls = []
  const chain = /\$[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+/g
  for (const match of masked.matchAll(chain)) {
    for (const part of match[0].split(".").slice(1)) addUnique(memberCalls, part)
  }
  const afterCall = /\)(?:\.[A-Za-z_][A-Za-z0-9_]*)+/g
  for (const match of masked.matchAll(afterCall)) {
    for (const part of match[0].split(".").slice(1)) addUnique(memberCalls, part)
  }
  const vars = /\$([A-Za-z_][A-Za-z0-9_]*)/g
  for (const match of masked.matchAll(vars)) {
    if (match[1] === "ExecutionContext") addUnique(memberCalls, "$ExecutionContext")
  }
  return memberCalls
}

function isOpen(token) {
  return token.type === "group" && (token.op === "{" || token.op === "(" || token.op === "$(")
}

function isClose(token) {
  return token.type === "group" && (token.op === "}" || token.op === ")")
}

function isStatementSep(token) {
  if (token.type === "newline") return true
  return token.type === "op" && (token.op === ";" || token.op === "&&" || token.op === "||")
}

function splitTop(tokens, sep) {
  const parts = []
  let depth = 0
  let current = []
  for (const token of tokens) {
    if (isOpen(token)) depth++
    if (depth === 0 && sep(token)) {
      parts.push(current)
      current = []
    } else {
      current.push(token)
    }
    if (isClose(token)) depth = Math.max(0, depth - 1)
  }
  parts.push(current)
  return parts
}

function matchingInner(tokens, openIndex) {
  const open = tokens[openIndex]
  const close = open.op === "{" ? "}" : ")"
  let depth = 1
  const inner = []
  let j = openIndex + 1
  for (; j < tokens.length; j++) {
    const token = tokens[j]
    if (isOpen(token)) {
      depth++
      inner.push(token)
      continue
    }
    if (token.type === "group" && token.op === close) {
      depth--
      if (depth === 0) return { inner, next: j + 1 }
      inner.push(token)
      continue
    }
    inner.push(token)
  }
  return { inner, next: tokens.length }
}

function walkGroups(tokens, visit) {
  for (let i = 0; i < tokens.length; i++) {
    if (!isOpen(tokens[i])) continue
    const found = matchingInner(tokens, i)
    visit(found.inner)
    i = found.next - 1
  }
}

function groupRaw(input, tokens, openIndex, next) {
  const start = tokens[openIndex].start
  const endToken = tokens[Math.min(next, tokens.length) - 1]
  return input.slice(start, endToken ? endToken.end : start)
}

function findName(tokens) {
  let i = 0
  while (i < tokens.length && tokens[i].type === "op" && tokens[i].op === "&") i++
  for (let k = i; k < tokens.length; k++) {
    const token = tokens[k]
    if (token.type === "word" || token.type === "string" || token.type === "expandable") return k
    if (token.type === "group") return -1
  }
  return -1
}

function collectArgs(input, tokens) {
  const args = []
  const facts = []
  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i]
    if (token.type === "newline" || token.type === "op") continue
    if (isOpen(token)) {
      const found = matchingInner(tokens, i)
      const raw = groupRaw(input, tokens, i, found.next)
      args.push(raw)
      facts.push({ raw, quoted: false })
      i = found.next - 1
      continue
    }
    if (token.type === "word" || token.type === "string" || token.type === "expandable") {
      args.push(token.value ?? token.raw)
      facts.push({ raw: token.raw, quoted: !!token.quoted })
    }
  }
  return { args, facts }
}

function absorbSegment(input, tokens, depth, stage, commands) {
  const nameIdx = findName(tokens)
  if (nameIdx >= 0) {
    const nameTok = tokens[nameIdx]
    const name = nameTok.value ?? nameTok.raw
    const { args, facts } = collectArgs(input, tokens.slice(nameIdx + 1))
    let dashDash
    if (String(name).toLowerCase() === "git") {
      for (let a = 0; a < args.length; a++) {
        if (!facts[a].quoted && facts[a].raw === "--") {
          dashDash = { index: a + 1, firstFileAfter: a + 1 < args.length ? args[a + 1] : null }
          break
        }
      }
    }
    const significant = tokens.filter((token) => token.type !== "newline")
    const text = significant.length
      ? input.slice(significant[0].start, significant[significant.length - 1].end).trim()
      : String(name)
    const cmd = {
      name: String(name),
      args,
      tokens: significant.map((token) => ({
        text: token.text ?? token.raw,
        kind: token.kind || "Generic",
        position: token.start,
      })),
      text,
      depth,
      pipelineStage: stage,
    }
    if (dashDash) cmd.dashDash = dashDash
    commands.push(cmd)
  }
  walkGroups(tokens, (inner) => {
    for (const nested of parseCommands(input, inner, depth + 1)) commands.push(nested)
  })
}

function parseCommands(input, tokens, depth) {
  const commands = []
  for (const statement of splitTop(tokens, isStatementSep)) {
    const stages = splitTop(statement, (token) => token.type === "op" && token.op === "|")
    stages.forEach((stageTokens, stage) => {
      absorbSegment(input, stageTokens, depth, stage, commands)
    })
  }
  return commands
}

export function parseCommandTree(command) {
  const input = String(command ?? "")
  const { tokens, excluded } = tokenize(input)
  const masked = maskExcluded(input, excluded)
  const hasSubstitution = masked.includes("$(")
  const hasStatic = masked.includes("::")
  return {
    ok: true,
    parseErrors: [],
    hasSubstitution,
    substitutions: hasSubstitution ? ["$("] : [],
    staticCalls: hasStatic ? ["::"] : [],
    memberCalls: collectMembers(masked),
    commands: parseCommands(input, tokens, 0),
  }
}

export function extractCommandText(payload) {
  if (payload == null || typeof payload !== "object") return ""
  if (typeof payload.command === "string" && payload.command.length > 0) return payload.command
  let toolInput = payload.tool_input
  if (typeof toolInput === "string") {
    try {
      toolInput = JSON.parse(toolInput)
    } catch {
      return toolInput
    }
  }
  if (toolInput && typeof toolInput === "object" && typeof toolInput.command === "string") return toolInput.command
  return ""
}

export function extractFilePath(payload) {
  if (payload == null || typeof payload !== "object") return ""
  let toolInput = payload.tool_input
  if (typeof toolInput === "string") {
    try {
      toolInput = JSON.parse(toolInput)
    } catch {
      return ""
    }
  }
  if (!toolInput || typeof toolInput !== "object") return ""
  for (const key of ["file_path", "filePath", "path"]) {
    if (typeof toolInput[key] === "string" && toolInput[key].length > 0) return toolInput[key]
  }
  return ""
}

export function decide(command, toolName = "", filePath = "") {
  const tool = String(toolName ?? "")
  const lower = tool.toLowerCase()
  if ((lower === "edit" || lower === "write") && filePath) {
    const decision = toolPathDecision(String(filePath))
    if (!decision) return null
    return { ...decision, commandText: `${tool} ${filePath}` }
  }
  if (command == null || String(command).length === 0) return null
  const decision = denyDecision(parseCommandTree(String(command)))
  if (!decision) return null
  return { ...decision, commandText: String(command) }
}

export function decisionJson(decision) {
  if (!decision) return "{}"
  const msg = formatDenyMessage(decision, decision.commandText ?? "", HARNESS)
  return JSON.stringify({ permission: "deny", agent_message: msg, user_message: msg })
}

export function handleHook(raw) {
  try {
    const original = String(raw ?? "")
    const text = normalizeStdinText(original).trim()
    if (!text) {
      log(`stdin_len=${original.length} parse=fail raw=`)
      return "{}"
    }
    let payload
    try {
      payload = JSON.parse(text)
    } catch {
      const snip = collapseSnippet(text)
      log(`stdin_len=${original.length} parse=fail raw=${snip}`)
      const msg = formatParseFailureMessage(snip, HARNESS, logPath())
      return JSON.stringify({ permission: "deny", agent_message: msg, user_message: msg })
    }
    if (payload == null || typeof payload !== "object" || Array.isArray(payload)) {
      log(`stdin_len=${original.length} parse=ok rule=pass`)
      return "{}"
    }
    const event = String(payload.hook_event_name ?? "")
    const tool = String(payload.tool_name ?? "")
    if (event === "preToolUse" || event === "beforeShellExecution") {
      const command = extractCommandText(payload)
      const filePath = extractFilePath(payload)
      const decision = decide(command, tool, filePath)
      log(
        `event=${event} tool=${tool} rule=${decision ? decision.rule : "pass"} cmd=${command.replace(/\s+/g, " ")} stdin_len=${original.length} parse=ok`,
      )
      return decisionJson(decision)
    }
    log(`event=${event} tool=${tool} rule=pass stdin_len=${original.length} parse=ok`)
    return "{}"
  } catch (err) {
    log(`ERROR ${err && err.message ? err.message : err}`)
    return "{}"
  }
}

function runningAsCli() {
  const entry = process.argv[1]
  if (!entry) return false
  try {
    return fs.realpathSync(entry) === fs.realpathSync(fileURLToPath(import.meta.url))
  } catch {
    return false
  }
}

async function main() {
  try {
    if (process.stdin.isTTY) {
      process.stdout.write("{}\n")
      return
    }
    const chunks = []
    for await (const chunk of process.stdin) chunks.push(chunk)
    const text = decodeUtf8Payload(Buffer.concat(chunks))
    process.stdout.write(`${handleHook(text)}\n`)
  } catch {
    process.stdout.write("{}\n")
  }
}

if (runningAsCli()) main()
