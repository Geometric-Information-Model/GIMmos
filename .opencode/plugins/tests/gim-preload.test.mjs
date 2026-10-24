// gim-preload.test.mjs — offline harness for the per-agent preload plugin.
// Run with plain node:
//
//   node .opencode/plugins/tests/gim-preload.test.mjs
//
// Exercises the hooks DIRECTLY (fake chat.message payloads + a fake client), so
// it proves config resolution, file loading, frontmatter stripping, the
// injection string, agent discrimination, and the synthetic-message fallback.
// It cannot prove that opencode calls the hooks or honours message.system —
// that needs a live turn with a sentinel assertion.
//
// Clean-checkout safe: the gitignored `.agents/agent-prefs.md` is never read;
// hook tests run against a temp fixture root with its own config, prefs file,
// and repo-local skill tree. Tracked repo skills are only resolved by name.

import { after, test } from "node:test"
import assert from "node:assert/strict"
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs"
import { tmpdir } from "node:os"
import { dirname, join, sep } from "node:path"
import { fileURLToPath } from "node:url"
import {
  GimPreload,
  buildBlock,
  hasBlock,
  lastUserAgent,
  mergeSystem,
  readConfig,
  relativeDisplay,
  resolveFile,
  resolveSkill,
  skillRoots,
  stripFrontmatter,
} from "../gim-preload.js"

const here = dirname(fileURLToPath(import.meta.url))
const ROOT = join(here, "..", "..", "..")

// Temp fixture project. Repo-local skills only, so no machine-global skill is
// required and no live prefs file is read.
const FIXTURE = mkdtempSync(join(tmpdir(), "gim-preload-test-"))
after(() => rmSync(FIXTURE, { recursive: true, force: true }))

const FIXTURE_AGENTS = {
  "gim-orchestrator": ["caveman", "agentic-planning"],
  "gim-explorer": ["caveman", "workspace-code-search", "gim-tools"],
  "gim-version-controller": ["caveman", "git-branching-workflow"],
}

function writeFixture() {
  mkdirSync(join(FIXTURE, ".agents"), { recursive: true })
  writeFileSync(
    join(FIXTURE, ".agents", "agent-prefs.md"),
    "# fixture prefs\n\nfixture-prefs-marker\n",
    "utf8",
  )
  for (const skills of Object.values(FIXTURE_AGENTS)) {
    for (const name of skills) {
      const dir = join(FIXTURE, ".agents", "skills", name)
      mkdirSync(dir, { recursive: true })
      writeFileSync(
        join(dir, "SKILL.md"),
        `---\nname: ${name}\ndescription: fixture skill\n---\n# ${name}\n\nfixture skill ${name}\n`,
        "utf8",
      )
    }
  }
  writeFileSync(
    join(FIXTURE, ".agents", "agent-preload.json"),
    JSON.stringify(
      {
        enabled: true,
        header: "fixture header",
        agents: Object.fromEntries(
          Object.entries(FIXTURE_AGENTS).map(([agent, skills]) => [
            agent,
            { skills, files: [".agents/agent-prefs.md"] },
          ]),
        ),
      },
      null,
      2,
    ),
    "utf8",
  )
}

writeFixture()

function fakeOutput(agent, system) {
  return { message: { id: `msg_${agent}`, agent, system }, parts: [] }
}

// Fake session.messages() response, newest last.
function fakeClient(messages) {
  const calls = []
  return {
    calls,
    session: {
      messages: async (options) => {
        calls.push(options)
        return { data: messages }
      },
    },
  }
}

test("fixture frontmatter is stripped and a body horizontal rule survives", () => {
  const text = "---\nname: fixture\ndescription: fixture only\n---\n# Title\n\n---\n\nbody\n"
  assert.equal(stripFrontmatter(text), "# Title\n\n---\n\nbody\n")
  assert.equal(stripFrontmatter("# Already clean\n\n---\n"), "# Already clean\n\n---\n")
})

test("skill roots stay inside the project (repo-local only)", () => {
  const roots = skillRoots(ROOT)
  assert.equal(roots.length, 3)
  for (const path of roots) {
    assert.ok(path.startsWith(ROOT), `${path} must be repo-local`)
  }
})

test("skill names and file paths resolve independently in a temp tree", () => {
  assert.match(resolveSkill("caveman", FIXTURE), /\.agents[\\/]skills[\\/]caveman[\\/]SKILL\.md$/)
  assert.equal(resolveSkill("no-such-skill-xyz", FIXTURE), undefined)

  const tempFile = join(FIXTURE, "notes.md")
  writeFileSync(tempFile, "temp\n", "utf8")
  assert.equal(resolveFile("notes.md", FIXTURE), tempFile, "a temp file resolves as a file path")
  assert.equal(resolveFile("caveman", FIXTURE), undefined, "a skill name is not a file path")
  assert.equal(resolveSkill("notes", FIXTURE), undefined, "a file name is not a skill")
  assert.equal(resolveFile(".agents/agent-prefs.md", FIXTURE), join(FIXTURE, ".agents", "agent-prefs.md"))
})

test("relativeDisplay normalizes trailing separators, mixed separators, and case", () => {
  const prefs = join(FIXTURE, ".agents", "agent-prefs.md")
  assert.equal(relativeDisplay(prefs, FIXTURE), ".agents/agent-prefs.md")
  assert.equal(relativeDisplay(prefs, `${FIXTURE}${sep}`), ".agents/agent-prefs.md", "trailing root separator")
  assert.equal(relativeDisplay(prefs.replace(/\\/g, "/"), FIXTURE), ".agents/agent-prefs.md", "forward slashes")
  assert.equal(relativeDisplay(prefs.toLowerCase(), FIXTURE), ".agents/agent-prefs.md", "case mismatch")
  assert.equal(relativeDisplay("C:\\outside\\x.md", FIXTURE), "C:\\outside\\x.md", "outside root is unchanged")
})

// Role skills per agent. Divergence is deliberate: no agent gets every skill.
const EXPECTED = {
  "gim-orchestrator": ["caveman", "agentic-planning", "grill-with-docs", "review-with-critic", "gim-tools"],
  "gim-explorer": ["caveman", "workspace-code-search", "gim-tools"],
  "gim-builder": ["caveman", "agentic-planning", "fileaccess-routing", "format-adapter-contract", "gim-tools"],
  "gim-version-controller": ["caveman", "git-branching-workflow", "github-cli-workflow", "gh-stack", "gim-tools"],
  "gim-critic": ["caveman", "gim-tools"],
  "gim-tracker": ["caveman", "atlassian-jira-cli", "github-cli-workflow", "gim-tools"],
  "gim-debugger": ["caveman", "diagnosing-bugs", "gim-tools"],
  "gim-tester": ["caveman", "gim-tools"],
}

test("config maps the fleet with both sections", () => {
  const config = readConfig(ROOT)
  assert.equal(config.enabled, true)
  for (const agent of Object.keys(EXPECTED)) {
    const spec = config.agents[agent]
    assert.ok(spec && typeof spec === "object", `${agent} must be mapped`)
    assert.ok(Array.isArray(spec.skills), `${agent}.skills must be an array`)
    assert.ok(Array.isArray(spec.files), `${agent}.files must be an array`)
    assert.ok(spec.skills.length + spec.files.length > 0, `${agent} must map something`)
  }
})

test("shared config is the single source and maps role skills per agent", () => {
  assert.ok(resolveFile(".agents/agent-preload.json", ROOT), "shared config must exist")
  assert.equal(resolveFile(".opencode/agent-preload.json", ROOT), undefined, "no second config")

  const config = readConfig(ROOT)
  assert.deepEqual(Object.keys(config.agents).sort(), Object.keys(EXPECTED).sort())
  const removedAgents = ["gim-linter"]
  for (const agent of removedAgents) {
    assert.equal(config.agents[agent], undefined, `no removed agent entry: ${agent}`)
  }

  for (const [agent, skills] of Object.entries(EXPECTED)) {
    assert.deepEqual(config.agents[agent].skills, skills, `${agent} role skills`)
    assert.deepEqual(config.agents[agent].files, [".agents/agent-prefs.md"], `${agent} gets prefs`)
  }

  // Explorer: three role skills plus the prefs file.
  assert.equal(
    config.agents["gim-explorer"].skills.length + config.agents["gim-explorer"].files.length,
    4,
    "explorer maps four preloaded files",
  )
})

test("every configured skill resolves repo-locally; prefs may be gitignored", () => {
  const config = readConfig(ROOT)
  const missing = []
  for (const [agent, spec] of Object.entries(config.agents)) {
    for (const name of spec.skills ?? []) {
      if (!resolveSkill(name, ROOT)) missing.push(`${agent}.skills: ${name}`)
    }
    for (const entry of spec.files ?? []) {
      assert.equal(entry, ".agents/agent-prefs.md", `${agent} preloads the gitignored prefs file`)
    }
  }
  assert.deepEqual(missing, [], `unresolved skills: ${missing.join(", ")}`)
})

test("mergeSystem is idempotent and preserves foreign system text", () => {
  const block = buildBlock("a", [{ path: ".x.md", body: "hello" }], "hdr")
  const once = mergeSystem("preexisting systems", block)
  assert.equal(mergeSystem(once, block), once)
  assert.ok(once.startsWith("preexisting systems"))
  assert.equal((once.match(/<gim-preload/g) ?? []).length, 1)
})

test("lastUserAgent reads the newest user message", () => {
  const messages = [
    { info: { role: "user", agent: "a" }, parts: [] },
    { info: { role: "assistant", agent: "a" }, parts: [] },
    { info: { role: "user", agent: "b" }, parts: [] },
    { info: { role: "assistant", agent: "b" }, parts: [] },
  ]
  assert.equal(lastUserAgent(messages), "b")
  assert.equal(lastUserAgent([]), undefined)
  assert.equal(lastUserAgent(undefined), undefined)
})

test("hook injects fixture skills and prefs for a mapped agent", async () => {
  const hooks = await GimPreload({ directory: FIXTURE, client: fakeClient([]) })
  const output = fakeOutput("gim-explorer", undefined)
  await hooks["chat.message"]({ sessionID: "ses_test", agent: "gim-explorer" }, output)

  const system = output.message.system
  assert.ok(system, "system must be set")
  assert.match(system, /<gim-preload agent="gim-explorer" files="4">/)
  assert.match(system, /<gim-file path="\.agents\/skills\/caveman\/SKILL\.md">/)
  assert.match(system, /<gim-file path="\.agents\/skills\/workspace-code-search\/SKILL\.md">/)
  assert.match(system, /<gim-file path="\.agents\/skills\/gim-tools\/SKILL\.md">/)
  assert.match(system, /<gim-file path="\.agents\/agent-prefs\.md">/)
  assert.match(system, /fixture-prefs-marker/)
  assert.doesNotMatch(system, /^---$/m, "frontmatter must be stripped")
})

test("hook injects the files section, skills first", async () => {
  const hooks = await GimPreload({ directory: FIXTURE, client: fakeClient([]) })
  const output = fakeOutput("gim-orchestrator", "some env block")
  await hooks["chat.message"]({ sessionID: "ses_test", agent: "gim-orchestrator" }, output)

  const system = output.message.system
  assert.ok(system.startsWith("some env block"))
  assert.match(system, /<gim-file path="\.agents\/agent-prefs\.md">/)
  assert.match(system, /fixture-prefs-marker/)
  // skills first, then files
  assert.ok(
    system.indexOf(".agents/skills/caveman/SKILL.md") < system.indexOf(".agents/agent-prefs.md"),
    "skills must precede files",
  )
})

test("hook is a no-op for unmapped agents", async () => {
  const hooks = await GimPreload({ directory: FIXTURE, client: fakeClient([]) })
  const output = fakeOutput("title", undefined)
  await hooks["chat.message"]({ sessionID: "ses_test", agent: "title" }, output)
  assert.equal(output.message.system, undefined)
})

test("different agents get different file sets", async () => {
  const hooks = await GimPreload({ directory: FIXTURE, client: fakeClient([]) })
  const explorer = fakeOutput("gim-explorer", undefined)
  const vc = fakeOutput("gim-version-controller", undefined)
  await hooks["chat.message"]({ sessionID: "s1", agent: "gim-explorer" }, explorer)
  await hooks["chat.message"]({ sessionID: "s2", agent: "gim-version-controller" }, vc)

  assert.doesNotMatch(explorer.message.system, /git-branching-workflow/)
  assert.match(vc.message.system, /git-branching-workflow/)
  assert.match(explorer.message.system, /workspace-code-search/)
})

test("system.transform is a no-op when the block already travelled via message.system", async () => {
  const client = fakeClient([{ info: { role: "user", agent: "gim-explorer" }, parts: [] }])
  const hooks = await GimPreload({ directory: FIXTURE, client })
  const out = fakeOutput("gim-explorer", undefined)
  await hooks["chat.message"]({ sessionID: "ses_dbl", agent: "gim-explorer" }, out)

  const system = [out.message.system]
  await hooks["experimental.chat.system.transform"]({ sessionID: "ses_dbl" }, { system })
  assert.equal(system.length, 1)
  assert.equal(client.calls.length, 0, "must not hit the SDK when the block is present")
})

test("system.transform re-injects on a synthetic turn (no chat.message)", async () => {
  const client = fakeClient([
    { info: { role: "user", agent: "gim-explorer" }, parts: [] },
    { info: { role: "assistant", agent: "gim-explorer" }, parts: [] },
  ])
  const hooks = await GimPreload({ directory: FIXTURE, client })

  // Post-compaction continue: created by sessions.updateMessage, never chat.message.
  const system = ["provider prompt + env"]
  await hooks["experimental.chat.system.transform"]({ sessionID: "ses_fallback" }, { system })

  assert.equal(system.length, 2)
  assert.match(system[1], /<gim-preload agent="gim-explorer"/)
  assert.equal(client.calls.length, 1)
})

test("system.transform no-ops for an unmapped agent", async () => {
  const client = fakeClient([{ info: { role: "user", agent: "build" }, parts: [] }])
  const hooks = await GimPreload({ directory: FIXTURE, client })
  const system = ["env"]
  await hooks["experimental.chat.system.transform"]({ sessionID: "ses_build" }, { system })
  assert.equal(system.length, 1)
})

test("re-injection is stable across turns (same bytes => cache prefix preserved)", async () => {
  const hooks = await GimPreload({ directory: FIXTURE, client: fakeClient([]) })
  const first = fakeOutput("gim-explorer", "env")
  const second = fakeOutput("gim-explorer", "env")
  await hooks["chat.message"]({ sessionID: "s", agent: "gim-explorer" }, first)
  await hooks["chat.message"]({ sessionID: "s", agent: "gim-explorer" }, second)
  assert.equal(first.message.system, second.message.system)
})

test("hasBlock detects our marker and ignores foreign system text", () => {
  assert.equal(hasBlock(["plain"]), false)
  assert.equal(hasBlock([buildBlock("x", [{ path: ".a.md", body: "b" }], "h")]), true)
  assert.equal(hasBlock(undefined), false)
})

test("hasBlock requires a complete block, not a bare opening tag", () => {
  assert.equal(hasBlock(["mentions <gim-preload in prose"]), false, "bare opening tag is not a block")
  assert.equal(hasBlock(["see <gim-preload agent=\"x\" files=\"1\">"]), false, "opening tag with attrs is not a block")
  assert.equal(
    hasBlock(["prefix text\n\n" + buildBlock("x", [{ path: ".a.md", body: "b" }], "h")]),
    true,
    "a complete block anywhere in the system part counts",
  )
})
