# GIM Agent Fleet

Ubiquitous language for the GIM orchestrator fleet across harnesses (Cursor and OpenCode): skills, subagents, prefs, and plan/ADR workflow. This is **not** the CAD `GimData` domain language.

## Language

**HarnessToolMap**:
Per-harness mapping of fleet spawn/resume wiring to each harness's tools. Details in autoload map files.
_Avoid_: harness notes (when meaning the table), adapter table

**Orchestrator**:
Main-loop agent/skill that sequences the fleet and never performs research, implementation, git, or Jira work itself unless the user explicitly asks.
_Avoid_: coordinator, manager agent

**Explorer**:
Readonly research subagent that searches the workspace or web and returns findings; parallel spawns OK.
_Avoid_: searcher

**Builder**:
Subagent that implements exactly one plan step and coordinates applicable child
validation.
_Avoid_: implementer, coder

**ContextExhausted**:
Builder report that this instance has spent most of its context. Later fix dispatches for that step go to a fresh builder.
_Avoid_: substantial

**Tester**:
Subagent that runs applicable tests in a bounded area and reports outcomes.
_Avoid_: QA agent

**ScopeManifest**:
Bounded child scope supplied by builder for applicable validation. Children use
that scope and do not widen it to repository-wide defaults.

**NearbyTestBoundary**:
Relevant local test area for changed source. Tester stays within builder-supplied
context instead of using a repository-wide default.

**TesterReport**:
Structured summary of test outcomes and noteworthy observations. Builder uses
it to report applicable child validation results.

**Critic** (`gim-critic`):
Readonly post-step reviewer; always spawned fresh; never modifies the repo.
_Avoid_: reviewer, critic (alone, in new docs)

**CriticDisposition**:
The orchestrator's agree-or-reject decision on a critic finding, made inside the critic loop.
_Avoid_: user approval (during the loop)

**VersionController**:
Subagent that owns git and GitHub CLI mutations under a destructive deny-list.
_Avoid_: git agent

**Tracker**:
Subagent that owns the configured issue tracker, either Jira via acli or GitHub issues via gh.
_Avoid_: jira agent

**Deliberate**:
User-invoked discussion checkpoint. Numbered points stay open to pushback until explicit consent to continue.
_Avoid_: pause, checkpoint

**Improve**:
User-invoked skill that edits the fleet itself when the exact file and the exact edit are known.
_Avoid_: weevil-improve

**Autopilot**:
User-invoked run of one or more plan steps that decides reversible local choices itself and defers verification to the end of the run.
_Avoid_: night shift, unattended mode

**Wrapup** (`gim-wrapup`):
Post-plan skill. Gathers open-PR CI and verbatim comments, runs the end critic when prefs say so, and waits for an explicit Go before follow-up work.
_Avoid_: wrap-up critic (when meaning the whole end step)

**AgentPrefix**:
Name prefix written only into the OpenCode copies of fleet agents. The repo names stay `gim-`.
_Avoid_: gim- prefix (when meaning the copy)

**OpenCodeSync**:
A copy of OpenCode fleet agents and skills into the user OpenCode config. It does not commit. An existing copy keeps its model; a new copy takes the repo's model.

**Debugger**:
Subagent for hypothesis-driven bug diagnosis; instrumentation server deferred.
_Avoid_: fixer

**Prefs**:
Local gitignored `agent-prefs.md` controlling gates, architecture, per-agent instruction sections, and the OpenCode copy prefix.
_Avoid_: settings.json

**Preload**:
Per-agent context injected by the harness from `.agents/agent-preload.json`: prefs for every agent plus role skills. OpenCode injects every user turn, and the Cursor Task-spawn path is verified. Cursor session-start delivery is pending confirmation, so the orchestrator keeps a prefs fallback until then. Agents use preloaded material directly; the body keeps only non-preloaded reads and a fallback path.
_Avoid_: auto-attach

**FleetVerb**:
One of the seven network/remote verbs in the `fleet` group: `gim-preflight`, `gim-pr`, `gim-jira-search`, `gim-jira-create`, `gim-jira-transition`, `gim-wrapup-gather`, `gim-wrapup-respond`. Role-scoped in agent frontmatter; never part of the shared global allowlist. The toolkit total is 25 verbs in nine groups.
_Avoid_: global verb, fleet command

**GatherFile** (`gather.json`):
Schema-v1 read-only PR gather written by `gim-wrapup-gather` under `.agent-scratch/wrapup/<slug>/` or `--out`. UTF-8 without BOM, 1-space indent, null-valued properties omitted; pass/skipping checks reduce to `{name,state}` while failing/cancelled/pending/log rows stay full.
_Avoid_: report.json, gather output

**ResponseFile** (`responses.json`):
The authored work shell beside `gather.json` (`{repo, generated_at, prs[{pr, threads[{thread_id, resolve, response}], comments[]}]}`). Re-gather merges by thread id and preserves authored fields; `gim-wrapup-respond` never rewrites it.
_Avoid_: reply file, response payload

**RespondReceipt** (`responses.result.json`):
Sibling receipt written by `gim-wrapup-respond`: per entry `posted_at`, `resolved_at`, `remote_state`, `last_error`, plus comment keys derived from SHA-256 content. Reruns skip completed entries and retry failures.
_Avoid_: result.json (unqualified), respond log

**PRFileList**:
The explicit repo-relative `files.txt` evidence file consumed by `gim-pr` (`--files-file`), one path per line; globs, directories, and escapes are rejected; duplicates are deduped. Derived under `.agent-scratch/pr-files/<feature>/<step>/` when `--files-file` has no value.
_Avoid_: changed-files list, glob

**StackInit**:
The first-PR `gh stack init --base <target> <current-branch>` step run inside `gim-pr`; it records `pending=true` and never links or syncs. Later step PRs use `gh stack add` plus `link`/`sync` verification.
_Avoid_: stack setup, bootstrap PR

**VerifyGate**:
Prefs-gated user confirmation of a finished step: files touched, critic disposition, and a test recipe. It happens only after the critic loop, and it is where the user may overturn a critic disposition.
_Avoid_: approval

**ThinPlan**:
Pointer plan file in a sibling repo that references the primary plan.
_Avoid_: stub plan

**FeatureDocuments**:
Plan, ADR, RFC, or thin-plan docs allowed on `feature/*` under the docs-only exception.
_Avoid_: docs (alone)

**LeafSafeBranch**:
Branch naming that never creates `feature/<name>/<step>` under an existing `feature/<name>` leaf.
_Avoid_: nested feature step branch

**SharedBashAllowlist**:
User-owned common bash rules in the local opencode config (`opencode.local.json` via `OPENCODE_CONFIG`; template default). Agent frontmatter holds agent-specific rules only; `gim-tester` uses a self-contained `"*": deny` map.
_Avoid_: per-agent allowlist, common ACL block
## Local stack vocabulary

**LocalDb**:
CoreWebApi Visual Studio launch profile that runs the API against Laragon MariaDB through `appsettings.secret.local.json`.

**RemoteDb**:
CoreWebApi's existing HTTPS development profile and secret overlay path for the remote development database.

**Secret overlay file**:
Optional JSON configuration file layered over `appsettings.json` during CoreWebApi startup. Local development selects `appsettings.secret.local.json`; existing default behavior selects `appsettings.secret.json`.

**Local API target**:
GIMmanager opt-in mode selected by `GIM_API_TARGET=local`. It keeps `isProduction=false` and routes the test-shaped API URL to local CoreWebApi.

**Local stack**:
Independent local development processes: browser client at `https://localhost:3000`, CoreWebApi at `https://localhost:7009`, GIMmanager's embedded hub at `http://localhost:4300`, and Laragon MariaDB at `127.0.0.1:3306`.
