# Plan 0003 — Shared bash allowlist moved to user-owned local opencode config

## Goal

The nine `gim-*` agent files under `.opencode/agents/` repeat an identical 37-entry bash permission block (`"*": ask` + 28 filesystem/read allows + 8 read-only git allows). This plan:

1. Moves the common block into the **user-owned** local opencode config (`opencode.local.json`, loaded via `OPENCODE_CONFIG` per `start-opencode.ps1`) and its onboarding template (`opencode.local.template.jsonc`), so each user owns/edits the allowlist and agent files stop bloating.
2. Hardens the over-broad `git branch*: allow` (present in orchestrator/explorer/critic/debugger/version-controller) by replacing it with explicit safe varieties (list/copy only); delete/rename/create fall through to `*: ask`.
3. Keeps opencode **built-in** agents (`build`, `plan`, `general`) at their current allow-all bash behavior via committed pins in `opencode.json` (main flow is the gim-* fleet; pins keep built-ins untouched).

## Locked decisions (grill Q1–Q4)

- **Q1** — tier-2 git-read block (`git branch*`, `git ls-tree*`, `git ls-remote*`, `git branch -vv*`): stays **per-agent** (only orchestrator/explorer/critic/debugger/version-controller carry it). `git branch*` is narrowed to safe varieties (below).
- **Q2** — narrowed `git branch` set accepted: list/copy auto-allowed; create/delete/rename prompt (matcher supports only `*`/`?` on the full command string — verified `packages/core/src/util/wildcard.ts` v1.18.10 — so "create but not delete" cannot be expressed; unlisted forms hit global `*: ask`).
- **Q3** — built-in agents pinned: `agent.build|plan|general.permission.bash = {"*": "allow"}` added to committed `opencode.json`.
- **Q4** — `opencode.local.json` keeps its existing shape (duplicated top-level keys stay); only `permission.bash` is added.

## Verified merge semantics (opencode 1.18.10 source)

- Final per-agent ruleset = built-in defaults → **global** `permission.bash` → **agent** `permission.bash`; evaluated **last-match-wins** (`Permission.merge` = order-preserving concat; `findLast`).
- `"*": ask` must live **only** in the global map and be its **first** entry. An agent-level `"*": ask` would swallow the global allowlist — agent maps must never contain `"*"`.
- Agent with no `bash:` key inherits the global bash rules unchanged.
- Built-in `explore` unaffected (explicit `bash: allow`); `build`/`plan`/`general` get pins (Q3).

## Common block being extracted (37 entries, global)

```yaml
"*": ask
ls*: allow | dir*: allow | pwd*: allow | Get-Location*: allow | cd*: allow | cat*: allow | Get-Content*: allow | head*: allow | tail*: allow | dirname*: allow | echo*: allow | Write-Output*: allow | Write-Host*: allow | Test-Path*: allow | Get-Item*: allow | Get-ChildItem*: allow | tree*: allow | findstr*: allow | grep*: allow | rg*: allow | Select-String*: allow | find*: allow | fc*: allow | Measure-Object*: allow | Sort-Object*: allow | Select-Object*: allow | ForEach-Object*: allow | Out-Null*: allow | git status*: allow | git diff*: allow | git show*: allow | git log*: allow | git ls-files*: allow | git branch --show-current*: allow | git rev-parse*: allow | git check-ignore*: allow
```

## Safe `git branch` variety set (replaces `git branch*` + `git branch -vv*` in the 5 agents that had them)

```yaml
git branch: allow
git branch -a*: allow
git branch -r*: allow
git branch -l*: allow
git branch -v*: allow
git branch -vv*: allow
git branch --all*: allow
git branch --remotes*: allow
git branch --list*: allow
git branch --verbose*: allow
git branch --merged*: allow
git branch --no-merged*: allow
git branch -c*: allow
git branch --copy*: allow
```

Anything else (`git branch <name>`, `-d`, `-D`, `-m`, `--delete`, …) matches nothing → global `*: ask`.

## Execution mode

**Plan-specific override (user, 2026-07-31):** ONE PR at end; verify with user at end only. Overrides `pr_per_step` and `verify_per_step` prefs **for this plan only** — prefs themselves unchanged.

- [ ] Verify per step — **verify once at END** (after all steps, before the single PR)
- [x] Commit per step
- [ ] PR per step — **ONE PR at end**
- [x] Critic per step (granular)

## Failure scenarios

- (verify override) Agent finishes all steps without the end verify gate with the user.
- (PR override) Agent opens more than one PR, or any per-step PR.
- (commit-per-step ON) Agent moves to step N+1 without committing step N.
- (critic-per-step ON) Agent moves to step N+1 without running gim-critic on step N.
- Any agent file keeps a `"*": ask` (or any `"*"` key) in its `bash:` map, or keeps any entry from the extracted common block.
- Any agent whose final merged ruleset loses an allow it had before the refactor (equivalence break).

## Branches

- Base feature: `feature/shared-bash-allowlist` (from `dev`) — carries this plan file (docs-only exception).
- Single implementation leaf: `shared-bash-allowlist/impl-bash-allowlist` (from the feature branch) — all step commits land here.
- One PR at end: `shared-bash-allowlist/impl-bash-allowlist` → `feature/shared-bash-allowlist`. Feature → `dev` merge stays for later.

Jira: skipped — `.opencode`/config meta (per gim-orchestrate thresholds).

---

### Step 1 — add shared bash allowlist to local config + template; pin built-ins [critic, verify, commit, PR]

- **Files / symbols**: `opencode.local.template.jsonc`, `opencode.local.json`, `opencode.json`
- **Change**:
  - `opencode.local.template.jsonc`: add top-level `permission.bash` with the 37-entry common block (`"*": "ask"` **first**), plus comments: user-owned/editable; last-match-wins; `*` must stay first; never copy `*` into agent frontmatter.
  - `opencode.local.json`: add the same `permission.bash` under the existing `permission` object (keep all existing keys — Q4).
  - `opencode.json`: add `agent` block — `build`, `plan`, `general` each `{"permission": {"bash": {"*": "allow"}}}` (built-in pins).
- **Validate**: `node -e` JSON.parse on `opencode.json` + `opencode.local.json`; JSONC-tolerant parse on template; `opencode debug config` (with `OPENCODE_CONFIG` set) shows the merged `permission.bash` with `*: ask` first.
- **Commit message**: `1: added shared bash allowlist to local opencode config`

### Step 2 — strip common bash block from agent files; narrow `git branch*` [critic, verify, commit, PR]

- **Files / symbols**: all nine `.opencode/agents/gim-*.md` files
- **Change**: remove the 37-entry common block from every agent `permission.bash`. Resulting per-agent bash maps (agent-specific rules only; **no `"*"` key anywhere**):
  - `gim-orchestrator`, `gim-explorer`, `gim-critic`: safe `git branch` set (14) + `git ls-tree*`, `git ls-remote*`.
  - `gim-debugger`: same as above + `New-Item*`, `Copy-Item*`.
  - `gim-builder`: `New-Item*`, `Copy-Item*`.
  - `gim-linter`, `gim-tester`: **self-contained deny maps** (session amendment below): `"*": deny` catch-all + re-listed common read/git-read entries + lint/build/test CLIs; one-line deadlock footnote.
  - `gim-tracker`: the four `acli jira …` read commands only.
  - `gim-version-controller`: `git add*`/`commit*`/`push*`/`fetch*`/`pull*`/`ls-tree*`/`ls-remote*`/`git-grep*`/`checkout*`/`switch*` + safe `git branch` set + `gh …` rules + `New-Item*`, `Copy-Item*` + deny/ask tail (`rm *: deny`, `del *: deny`, `Remove-Item*: deny`, `git push --force*: deny`, `git push --force-with-lease*: ask`, `git reset --hard*: ask`, `git branch -D*: ask`, `git commit --amend*: ask`, `gh pr edit --base*: ask`) — preserve existing relative order.
- **Validate**: YAML-parse each frontmatter (9 files); `opencode debug config` merged view; per-agent equivalence table (old ruleset vs new merged ruleset) reviewed in step summary.
- **Commit message**: `2: moved common bash allowlist out of agent files`

### Step 3 — docs: setup guide, ADR 0004, sync-contract note, glossary [critic, verify, commit, PR]

- **Files / symbols**: `docs/guides/0000-setup-opencode.md`, `docs/adr/agents/0004-AGENTS-shared-bash-allowlist-local-config.md` (new), `.opencode/agents/AGENTS.md`, `.agents/CONTEXT.md`
- **Change**:
  - Guide: steps 4–5 describe the local config now carrying the shared bash allowlist (user-owned, template default), built-in pins in committed `opencode.json`, `opencode debug config` verifying bash too.
  - ADR 0004 (new): decision record — common bash ACL moved from agent frontmatter to user-owned local config; agents carry deltas only; `git branch*` narrowed; built-ins pinned; supersedes plan 0002 Q4's "encode the full common allow list on every agent".
  - `.opencode/agents/AGENTS.md`: sync-contract note — common bash allowlist lives in `opencode.local.template.jsonc`/user `opencode.local.json`; agent frontmatter bash = agent-specific rules only; never add `"*"` to an agent bash map (last-match-wins swallows the global list).
  - `.agents/CONTEXT.md`: add glossary term **SharedBashAllowlist** (user-owned common bash rules; agents hold deltas only).
- **Validate**: markdown renders; referenced paths exist.
- **Commit message**: `3: documented shared bash allowlist ownership`

## Review

Invoke `/review-with-critic granular` after each step and once more after the final step (wrap-up pass, `Steps implemented: 1, 2, 3`).

## Session amendment (2026-07-31) — grandchild deadlock workaround

opencode bug: depth ≥2 approval prompts never resurface to the main UI ("ask" = deadlock for grandchildren). User-directed change, OpenCode scope only:

- `gim-linter` / `gim-tester` no longer rely on the global `*: ask` list. Each gets a SELF-CONTAINED `bash` map: `"*": deny` catch-all (first) + re-listed common read/git-read entries (deny swallows the global list — last-match-wins) + their CLIs (linter: `dotnet format*`, `npm run lint*`, `npm run format*`, `npx prettier*`, `npx eslint*`; tester: `dotnet build*`/`test*`/`restore*`, `npm run build*`/`test*`, `npm test*`, `npm ci*`, and exact `npm install` — no wildcard: blocks `npm install <pkg>` supply-chain surface).
- One-line permissions note added to both files: blocked → try allowed alternatives for the same purpose (~5 tries/purpose); if still blocked, continue without it if possible (report later) or report the exact command to parent (gim-builder) → orchestrator. Bounded retry — no early quit on first block (user revision 2026-07-31).
- No Cursor twin changes (frontmatter differs per harness).

## Post-execution notes

- **Restart required**: opencode loads config at startup (not hot-reloaded) — the running session keeps the old rules until restart.
- Residual risk (pre-existing, out of scope): version-controller `git pull*: allow` also matches `git pull --rebase`, and `git checkout*: allow` also matches `git checkout -- <file>`; both contradict the VC deny-list body. Flagged, not fixed here.
