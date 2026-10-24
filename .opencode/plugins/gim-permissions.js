// gim-permissions.js — opencode production permission plugin (plan 0004 B1).
//
// Thin wiring only. All decision logic lives in ./lib/gim-permissions-rules.js
// (pure predicates over the central command tree). This module:
//   - spawns the central parser lib (pwsh -NoProfile -File
//     .agents/skills/gim-tools/scripts/parse-command-tree.ps1) — the one
//     sanctioned external pwsh spawn (user-locked)
//   - lazily: a cheap PRE_FILTER regex runs before any spawn; a per-command
//     cache is shared between the before-stage and asked-stage
//   - denies with a message via tool.execute.before throw (incl. the git `--`
//     pathspec hint — the reject reply carries no text, user-locked escalation)
//   - rejects (never allows) the git `--` pathspec ask via permission.asked
//     (safety net: .gitignore-first-but-asks edge)
//
// Deny-only (ADR 0007 §1a): this plugin NEVER replies once/always to
// permission.asked — a reply approves the whole compound command. The config
// allowlist is the whitelist; the plugin only throws (before-stage) or
// rejects (ask-stage, git `--` quirk) or stays silent.
//
// Failure-mode split (user-locked 2026-08-20):
//   - Empty command / pre-filter no-match (no spawn) -> silent (fail-open):
//     nothing was received, nothing to decide.
//   - Pre-filter matched but the central-lib spawn produced NO parseable tree
//     (spawn error / timeout / non-JSON / ok:false envelope) -> generic deny
//     in the deny-message contract framing (parse-failed reason + log path).
//
// Logging is file-only: %TEMP%\opencode\gim-permissions.log. NEVER
// console.log in the TUI.

import { appendFileSync, mkdirSync, statSync, writeFileSync } from "node:fs"
import { join } from "node:path"
import { spawn } from "node:child_process"
import {
  PRE_FILTER,
  denyDecision,
  formatDenyMessage,
  formatParseFailureMessage,
  gitDashAskDecision,
  gitPathspecDecision,
  isParseFailure,
  toolPathDecision,
} from "./lib/gim-permissions-rules.js"

const LOG_DIR = join(process.env.TEMP || "C:\\Windows\\Temp", "opencode")
const LOG_FILE = join(LOG_DIR, "gim-permissions.log")
const LOG_MAX_BYTES = 5 * 1024 * 1024 // truncate the log when it exceeds ~5 MB
const MAX_CACHE = 512
const SPAWN_TIMEOUT_MS = 10_000

// Sentinel for "pre-filter did not match — no spawn was needed". Distinct
// from a parse-failure `null` tree: NO_SPAWN means fail-open (nothing to
// decide), while a failed spawn is fail-closed (generic deny).
const NO_SPAWN = Symbol("no-spawn")

function log(tag, message) {
  const line = `${new Date().toISOString()} [${tag}] ${message}`
  try {
    // Truncate-on-open when oversized so the log stays bounded (it carries
    // raw command text that may embed secrets).
    let size = 0
    try {
      size = statSync(LOG_FILE).size
    } catch {
      // file does not exist yet
    }
    if (size > LOG_MAX_BYTES) writeFileSync(LOG_FILE, "")
    appendFileSync(LOG_FILE, line + "\n")
  } catch {
    // never break the session because of logging
  }
}

// Spawn the central parser lib; resolves the parsed JSON tree, or null when
// the tree could NOT be produced (spawn error / timeout / non-JSON output /
// ok:false envelope). Null here is FAIL-CLOSED in the wiring (generic deny —
// user-locked 2026-08-20); the pre-filter no-match path uses the NO_SPAWN
// sentinel instead and stays silent.
function parseTree(command, scriptPath) {
  return new Promise((resolve) => {
    let settled = false
    const done = (value) => {
      if (settled) return
      settled = true
      resolve(value)
    }
    const child = spawn(
      "pwsh",
      ["-NoProfile", "-File", scriptPath, "-Command", command],
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
    child.on("error", (e) => {
      log("spawn", `spawn error ${e?.message ?? String(e)}`)
      done(null)
    })
    child.on("close", (code) => {
      try {
        const tree = JSON.parse(out.trim())
        done(tree)
      } catch {
        log("parse", `bad JSON (exit=${code}) stderr=${err.slice(0, 300)} cmd=${JSON.stringify(command)}`)
        done(null)
      }
    })
    const timer = setTimeout(() => {
      try {
        child.kill()
      } catch {
        // already gone
      }
      log("spawn", `timeout for ${JSON.stringify(command)}`)
      done(null)
    }, SPAWN_TIMEOUT_MS)
    timer.unref()
  })
}

export const GimPermissions = async ({ project, directory, client }) => {
  const projectDirectory = project?.directory ?? directory ?? process.cwd()
  const scriptPath = join(
    projectDirectory,
    ".agents",
    "skills",
    "gim-tools",
    "scripts",
    "parse-command-tree.ps1",
  )

  try {
    mkdirSync(LOG_DIR, { recursive: true })
  } catch {
    // logging is best-effort
  }
  log("init", `plugin loaded project=${project?.id ?? "?"} directory=${projectDirectory}`)

  // Per-command tree cache shared by before-stage and asked-stage. Values are
  // promises so concurrent lookups share one spawn. NO_SPAWN marks
  // "pre-filter no-match — no spawn, fail-open"; null marks "spawned but no
  // parseable tree — fail-closed (generic deny)".
  const treeCache = new Map()
  function getTree(command) {
    const cached = treeCache.get(command)
    if (cached !== undefined) return cached
    const treePromise = PRE_FILTER.test(command) ? parseTree(command, scriptPath) : Promise.resolve(NO_SPAWN)
    treeCache.set(command, treePromise)
    if (treeCache.size > MAX_CACHE) {
      const oldest = treeCache.keys().next().value
      treeCache.delete(oldest)
    }
    return treePromise
  }

  // Deny an execution with an exact message (opencode shows it verbatim).
  function deny(message) {
    const error = new Error(message)
    error.deny = true
    throw error
  }

  async function reply(sessionID, permissionID, response) {
    try {
      const res = await client.postSessionIdPermissionsPermissionId({
        path: { id: sessionID, permissionID },
        body: { response },
      })
      log("reply", `${response} -> ${JSON.stringify(res)}`)
    } catch (e) {
      log("reply", `FAILED ${response} ${e?.message ?? String(e)}`)
    }
  }

  return {
    // ---- ask-stage: git `--` pathspec quirk (reject-only, ADR 0007 §1a/§3) ----
    event: async ({ event }) => {
      try {
        if (event?.type !== "permission.asked") return
        const p = event.properties ?? {}
        if (p.permission !== "bash") return
        const command = String(p.metadata?.command ?? "")
        if (!command) return
        const tree = await getTree(command)
        const decision = gitDashAskDecision(tree)
        if (decision === "reject") {
          log("ask", `reject ${JSON.stringify(command)}`)
          await reply(p.sessionID, p.id, "reject")
        } else {
          log("ask", `noop/null (${decision}) ${JSON.stringify(command)}`)
        }
      } catch (e) {
        log("event", `error ${e?.message ?? String(e)}`)
      }
    },

    // ---- before-stage: deny-with-message (fires before permission eval) ----
    "tool.execute.before": async (input, output) => {
      try {
        const tool = input?.tool ?? ""
        if (tool === "bash") {
          const command = String(output?.args?.command ?? "")
          if (!command) return
          const tree = await getTree(command)
          // Failure-mode split (user-locked 2026-08-20): pre-filter no-match
          // -> NO_SPAWN -> fail-open (silent — nothing to decide). Spawned
          // but no parseable tree (spawn error / timeout / non-JSON /
          // ok:false) -> generic deny — never silently pass on a command the
          // plugin could not interpret.
          if (tree === NO_SPAWN) return
          if (isParseFailure(tree)) {
            log("deny", `bash parse-failure (generic deny) ${JSON.stringify(command)}`)
            deny(formatParseFailureMessage(command, "opencode permission plugin", LOG_FILE))
          }
          const decision = denyDecision(tree)
          if (decision) {
            log("deny", `bash rule=${decision.rule} ${JSON.stringify(command)}`)
            deny(formatDenyMessage(decision, command, "opencode permission plugin"))
          }
          // git `--` pathspec convention: hint must be a before-stage throw
          // (reject reply carries no custom text, user-locked escalation)
          const pathspecDecision = gitPathspecDecision(tree)
          if (pathspecDecision) {
            log("deny", `bash rule=${pathspecDecision.rule} ${JSON.stringify(command)}`)
            deny(formatDenyMessage(pathspecDecision, command, "opencode permission plugin"))
          }
        } else if (tool === "edit" || tool === "write") {
          const path = String(output?.args?.filePath ?? "")
          const decision = toolPathDecision(path)
          if (decision) {
            log("deny", `${tool} rule=${decision.rule} path=${JSON.stringify(path)}`)
            // file-tool deny: `<cmd>` is `<tool> <path>` (message contract)
            deny(formatDenyMessage(decision, `${tool} ${path}`, "opencode permission plugin"))
          }
        }
      } catch (e) {
        if (e && e.deny === true) throw e
        // plugin bug — never break the session; log and let the normal
        // permission flow take over
        log("tool.before", `error tool=${input?.tool ?? "?"} ${e?.message ?? String(e)}`)
      }
    },
  }
}
