// gim-preload.js — per-agent context preloading for opencode.
//
// Every agent body says "read and obey .agents/skills/x". That costs a tool
// call per file per agent. This plugin injects the configured files straight
// into the agent's context instead.
//
// Verified against opencode 1.18.19 source:
//   - There is no agentStart / subagentStart hook in the v1 plugin API.
//   - `chat.message` fires on every prompt(), which covers the primary user
//     turn, every `task` subagent child session, and `task_id` resumes.
//   - `output.message.agent` is the resolved agent name (UserMessage.agent is
//     non-optional), and mutations persist (saved after the hook runs).
//   - `output.message.system` is concatenated into the request's system
//     message — but only for the LATEST user message. It is never written into
//     the transcript, so it must be re-injected on every user message.
//
// The one path that bypasses chat.message is a synthetic user message:
// post-compaction "continue" (compaction.ts) and the task-output summary
// (prompt.ts). Those are covered by `experimental.chat.system.transform`,
// which derives the agent from the session's persisted messages instead of
// keeping any local state.
//
// No disk state, no markers, no in-memory cache: every request either has the
// block from chat.message or re-derives it here. Config is re-read per turn,
// so only this .js file needs a restart.
//
// Logging is file-only: %TEMP%\opencode\gim-preload.log. NEVER console.log in
// the TUI.

import { appendFileSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "node:fs"
import { isAbsolute, join } from "node:path"

const LOG_DIR = join(process.env.TEMP || "C:\\Windows\\Temp", "opencode")
const LOG_FILE = join(LOG_DIR, "gim-preload.log")
const LOG_MAX_BYTES = 5 * 1024 * 1024
const CONFIG_NAME = join(".agents", "agent-preload.json")

const BLOCK_OPEN = "gim-preload"
const BLOCK_CLOSE = "gim-file"
const PRELOAD_RE = /<gim-preload\b[\s\S]*?<\/gim-preload>\s*/g
// Complete block, any position: a bare opening tag is not a preload marker.
const PRELOAD_BLOCK_RE = /<gim-preload\b[^>]*>[\s\S]*?<\/gim-preload>/

function log(tag, message) {
  const line = `${new Date().toISOString()} [${tag}] ${message}`
  try {
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

// SKILL.md files carry YAML frontmatter (name/description). The skill tool
// drops it; we drop it too, since the path already identifies the file.
export function stripFrontmatter(text) {
  if (!text.startsWith("---")) return text
  const match = /^---\r?\n[\s\S]*?\r?\n---[ \t]*\r?\n?/.exec(text)
  return match ? text.slice(match[0].length) : text
}

// Skill roots, in resolution order. Repo-local only: the preload never reads a
// skill from the user's global config, so a clean checkout resolves the same
// set on every machine. Mirrors where the `skill` tool finds project skills.
export function skillRoots(root) {
  return [join(root, ".agents", "skills"), join(root, ".opencode", "skills"), join(root, ".claude", "skills")]
}

// Resolve a bare skill name to its SKILL.md, or undefined.
export function resolveSkill(name, root) {
  if (typeof name !== "string" || name.trim() === "") return undefined
  const value = name.trim()
  for (const skillsRoot of skillRoots(root)) {
    const candidate = join(skillsRoot, value, "SKILL.md")
    if (existsSync(candidate)) return candidate
  }
  return undefined
}

// Resolve a file entry: absolute, or relative to the project root.
export function resolveFile(entry, root) {
  if (typeof entry !== "string" || entry.trim() === "") return undefined
  const value = entry.trim()
  const full = isAbsolute(value) ? value : join(root, value)
  return existsSync(full) ? full : undefined
}

// Path shown inside a <gim-file> tag: project-relative with forward slashes.
// Normalizes both inputs so a trailing separator on the root does not shift the
// slice, and compares case-insensitively so Windows drive-letter or directory
// casing differences still produce a relative path. Paths outside the root are
// shown unchanged.
export function relativeDisplay(path, root) {
  const shown = String(path).replace(/\\/g, "/")
  const base = String(root ?? "")
    .replace(/\\/g, "/")
    .replace(/\/+$/, "")
  if (!base) return shown
  if (shown.toLowerCase() === base.toLowerCase()) return "."
  if (!shown.toLowerCase().startsWith(`${base.toLowerCase()}/`)) return path
  return shown.slice(base.length + 1)
}

export function buildBlock(agent, files, header) {
  if (!files.length) return ""
  const parts = [`<${BLOCK_OPEN} agent="${agent}" files="${files.length}">`, header, ""]
  for (const file of files) {
    parts.push(`<${BLOCK_CLOSE} path="${file.path}">`)
    parts.push(file.body)
    parts.push(`</${BLOCK_CLOSE}>`)
    parts.push("")
  }
  parts.push(`</${BLOCK_OPEN}>`)
  return parts.join("\n")
}

// Append (or replace) our block without touching anything else another plugin
// may have written into the message's system field.
export function mergeSystem(existing, block) {
  const cleaned = String(existing ?? "").replace(PRELOAD_RE, "").trimEnd()
  if (!block) return cleaned
  return cleaned ? `${cleaned}\n\n${block}` : block
}

export function hasBlock(system) {
  return Array.isArray(system) && system.some((part) => typeof part === "string" && PRELOAD_BLOCK_RE.test(part))
}

// Newest user message's agent, from a session.messages() response. Used only
// by the synthetic-message fallback, where the hook input carries no agent.
export function lastUserAgent(messages) {
  if (!Array.isArray(messages)) return undefined
  for (let i = messages.length - 1; i >= 0; i--) {
    const info = messages[i]?.info
    if (info?.role === "user" && typeof info.agent === "string") return info.agent
  }
  return undefined
}

export function readConfig(root) {
  const path = join(root, CONFIG_NAME)
  if (!existsSync(path)) return { enabled: false, agents: {}, header: "" }
  try {
    const config = JSON.parse(readFileSync(path, "utf8"))
    return {
      enabled: config.enabled !== false,
      header:
        typeof config.header === "string" && config.header.trim()
          ? config.header.trim()
          : "Reference material, preloaded. Attached to this agent's context.",
      agents: config.agents && typeof config.agents === "object" ? config.agents : {},
    }
  } catch (e) {
    log("config", `parse failed ${path} ${e?.message ?? String(e)}`)
    return { enabled: false, agents: {}, header: "" }
  }
}

// Resolve an agent's files and build its block. An agent maps to:
//   { "skills": ["caveman", ...], "files": [".agents/output-style.md", ...] }
// Skills are resolved through the skill roots, files relative to the project
// root. Skills come first; duplicates are dropped. Returns "" when the agent has
// no mapping or nothing resolved.
function blockFor(config, agent, root) {
  const spec = config.agents[agent]
  if (!spec || typeof spec !== "object") return { block: "", files: [], missing: [] }
  const skills = Array.isArray(spec.skills) ? spec.skills : []
  const paths = Array.isArray(spec.files) ? spec.files : []
  if (!skills.length && !paths.length) return { block: "", files: [], missing: [] }

  const files = []
  const missing = []
  const seen = new Set()

  const add = (path) => {
    if (seen.has(path)) return
    seen.add(path)
    try {
      const body = stripFrontmatter(readFileSync(path, "utf8")).trimEnd()
      files.push({ path: relativeDisplay(path, root), body })
    } catch (e) {
      log("read", `failed ${path} ${e?.message ?? String(e)}`)
    }
  }

  for (const name of skills) {
    const path = resolveSkill(name, root)
    if (path) add(path)
    else missing.push(name)
  }
  for (const entry of paths) {
    const path = resolveFile(entry, root)
    if (path) add(path)
    else missing.push(entry)
  }
  return { block: buildBlock(agent, files, config.header), files, missing }
}

export const GimPreload = async ({ directory, client }) => {
  const root = directory || process.cwd()

  try {
    mkdirSync(LOG_DIR, { recursive: true })
  } catch {
    // logging is best-effort
  }
  log("init", `plugin loaded root=${root} config=${join(root, CONFIG_NAME)}`)

  function injectable(agent) {
    const config = readConfig(root)
    if (!config.enabled || !agent) return undefined
    const { block, files, missing } = blockFor(config, agent, root)
    if (missing.length) log("miss", `agent=${agent} unresolved=${JSON.stringify(missing)}`)
    if (!block) return undefined
    return { block, files }
  }

  return {
    // Primary path: every real user turn, for every agent (primary or child).
    "chat.message": async (input, output) => {
      try {
        const agent = output?.message?.agent ?? input?.agent
        const found = injectable(agent)
        if (!found) return
        output.message.system = mergeSystem(output.message.system, found.block)
        log(
          "inject",
          `agent=${agent} session=${input?.sessionID ?? "?"} message=${output?.message?.id ?? "?"} ` +
            `files=${found.files.length} bytes=${found.block.length} ` +
            `paths=${JSON.stringify(found.files.map((f) => f.path))}`,
        )
      } catch (e) {
        // A plugin bug must never break the session; the agent can still read
        // the files itself.
        log("error", `chat.message ${e?.message ?? String(e)}`)
      }
    },

    // Fallback for user messages that never pass through createUserMessage:
    // post-compaction "continue" and the task-output summary. Those arrive
    // without our block and without an agent name, so derive the agent from the
    // session's persisted messages. No local state, so this survives restarts.
    "experimental.chat.system.transform": async (input, output) => {
      try {
        if (hasBlock(output?.system)) return
        const sessionID = input?.sessionID
        if (!sessionID || !client) return
        const res = await client.session.messages({ path: { id: sessionID } })
        const agent = lastUserAgent(res?.data)
        const found = injectable(agent)
        if (!found) return
        output.system.push(found.block)
        log("fallback", `agent=${agent} session=${sessionID} files=${found.files.length} bytes=${found.block.length}`)
      } catch (e) {
        log("error", `system.transform ${e?.message ?? String(e)}`)
      }
    },
  }
}
