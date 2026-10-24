---
name: github-cli-workflow
description: "Use when the user mentions PR or any derivative (pull request, PRs, my PR, this PR, draft PR), gh, github, github cli, code review, reviewer, reviewers, review feedback, PR comments, CI checks, opening/creating/merging/closing a PR, replying on a PR, or pointing at a GitHub issue. PR is always Pull Request unless the user explicitly says otherwise. Covers the full GitHub CLI (gh) workflow: preflight + auth, PR creation (default end-of-plan, opt-in early draft), title format, base-branch selection, reading review feedback (top-level + inline), replying with the harness footer, CI failure handling, and issue-linked branches. Auto-opts into per-step commits via agentic-planning. Targets the chained-PR base branch per git-branching-workflow."
compatibility: "Requires GitHub CLI (gh) on PATH for write paths (PR create/comment/edit/issue develop). Read paths gracefully fall back to web-URL guidance when gh is missing."
---

# GitHub CLI Workflow

Full reference: <https://cli.github.com/manual/>. Before any `gh` write path, run a quick preflight: `gh --version` (binary present) and `gh auth status` (authenticated as the expected account).

Branch topology: [`docs/branching-strategy.md`](../../../docs/branching-strategy.md).

## 1. Preflight + fallbacks

Detect availability and auth state once per session, before the first `gh` call:

- `gh --version` — confirms the binary is on `PATH`.
- `gh auth status` — confirms an authenticated session and shows the active account ([gh_auth_status](https://cli.github.com/manual/gh_auth_status)).

If `gh` is missing, do **not** install it silently. Offer the repo install script (with consent):

```bash
python .agents/skills/github-cli-workflow/scripts/install_gh.py
```

Use `--dry-run` to preview OS-appropriate commands. Pass `--no-extension` to install `gh` only without `gh-pr-review`. Manual fallbacks: Windows `winget install --id GitHub.cli`; macOS `brew install gh`; Linux see <https://cli.github.com/manual/installation>.

If `gh` is unauthenticated, hand off to the auth section (Section 3); never run `gh auth login` automatically.

If `gh` is missing entirely on a **read path** (e.g. "what comments are on my PR"), fall back to the web URL: parse `git remote -v`, derive `https://github.com/<owner>/<repo>/...`, and tell the user to open the PR in a browser instead of failing the request.

The optional [`gh-pr-review` extension](https://github.com/agynio/gh-pr-review) adds inline review-thread visibility that native `gh` lacks. Detection is **lazy** — only run `gh extension list | grep gh-pr-review` ([gh_extension_list](https://cli.github.com/manual/gh_extension_list)) the first time you need inline-thread context, not during every preflight. Full flow in `references/gh-pr-review-extension.md`.

## 2. Auth setup (walk-through)

If `gh auth status` reports an unauthenticated session, walk the user through login. The skill **never runs `gh auth login` itself** — the user runs it so the OAuth handshake happens in their browser, on their account.

Print the exact command and stop:

```bash
gh auth login --git-protocol https --scopes "repo,read:org,workflow" --web
```

After the user runs it, verify with `gh auth status`. The skill trusts whatever account `gh auth status` reports as current — never auto-`gh auth switch` between multiple accounts. Cite [gh_auth_login](https://cli.github.com/manual/gh_auth_login) when explaining the flags.

## 3. PR mode activation rules

PR mode activates **looser**: any mention of `PR` (or derivatives like `pull request`, `PRs`, `draft PR`, `my PR`, `this PR`), `gh`, `github`, `github cli`, `review`, `reviewer(s)`, `code review`, `PR comments`, or `CI checks` in the user's message turns it on for the rest of the session. `PR` is always _Pull Request_ unless the user explicitly says otherwise. Read-intent ("what comments are on my PR") triggers the read path; write-intent ("open the PR") triggers the write path.

PR mode auto-opts into per-step commit mode via `agentic-planning`. There is no second commit-mode gate — if PR mode is on, commit mode is on. PR-per-step (`PR per step`, `chain PRs`, etc.) is a distinct stronger opt-in: it produces one PR per step on chained branches rather than a single PR at end (see Section 4).

**One-time PR-mode confirmation gate** (per-PR, not per-session): before the first `gh pr create` of each PR, confirm with the user in a single prompt:

- **Title preview**: the rendered `[Tag] Title-Cased Feature Name - lowercase description`.
- **Base branch**: per Section 5 / `git-branching-workflow`.
- **Push consent**: confirm `git push -u origin HEAD` will run.
- **Excludes**: any tracked-but-uncommitted file that should not ship.

After confirmation, do not re-prompt for the rest of that PR's lifecycle. A new PR opening starts a fresh confirmation.

**Concurrent PR detection**: when PR mode activates, if `gh pr view` on the current branch reports an existing open PR, warn the user and ask explicitly: update existing / open new / do nothing. "Open new" usually fails (GitHub rejects a second PR from the same `head`+`base`); surface that error if the user picks it anyway. No auto-retry on PR-create errors.

**Multiple-open-PR disambiguation** (read path) when the user says "my PR" without qualifier: try `gh pr view` on the current branch first, then fall back to `gh pr status` and ask which one. External PR URLs are accepted: `gh pr view <url>` and `gh pr view <number>` are interchangeable.

## 4. One PR per plan (default) — or PR-per-step (opt-in)

**Default**: when PR mode is active, the plan resolves to a **single PR** opened at end of execution with a full body filled from the plan. The early-open draft path (Section 8) is opt-in.

End-of-plan PR creation runs after the last step's commit lands and pre-PR validation (Section 7) passes.

**PR-per-step alternative** (opt-in via `agentic-planning` activation phrases): each plan step lands on its own chained task branch and gets its own PR. Branches follow `git-branching-workflow` § Step / Task Branches (Chained, leaf-safe): each step's branch is named `<name>/<stepid>-<title>` (e.g. `seam-edges/a1-mark-curve-seam`; all lowercase; never under `feature/<name>/…`) and is cut from the **previous** step's task branch, not the base feature branch.

**Standard mechanics (`gim-pr`).** The orchestrator writes the evidence files
`.agent-scratch/pr-files/<feature>/<step>/files.txt` and `body.md` in the
target repo and hands the full PR package to the version-controller: repo,
branch, commit message, files file, body file, title, target, and
`--new-branch` when the step needs one. The version-controller invokes
`gim-pr` with exactly those parameters.

`gim-pr` stages the explicit list, guards the index, commits, pushes `-u`,
creates the PR, and runs the stack lifecycle. The lifecycle is owned here; read
[`references/gim-stack-lifecycle.md`](references/gim-stack-lifecycle.md) for
the authoritative init/add/link/sync behavior, config keys, result fields,
recorded sync/link failures, consent gates, and merge strategy. It is not
restated in this section. Execution order is owned by that reference: config ->
init/add -> stage -> commit -> push -> pr create -> link -> view -> sync.

The Section 7 pre-PR build/test validation gate applies to **both** the
`gim-pr` path and the manual fallback.

**Primary flow (default):**

1. Orchestrator writes `files.txt` + `body.md` and hands the full package.
2. Version-controller runs `gim-pr` with the package. The one-time PR-mode
   confirmation gate (Section 3) still fires once per step's PR.
3. If `gim-pr` fails, switch to the manual fallback below with the same
   package and **report the failure plus exactly what was done instead**.
4. Title and body per Sections 6-7.

**Manual fallback** - use only when the brief explicitly opts out (**manual**), `gim-pr` failed as above, or the flow is not the usual per-step chain (ad-hoc, hotfix, release, or manual recovery):

1. Push the step's branch: `git push -u origin HEAD`.
2. Open the PR with `--base <previous-task-branch>` (the base feature branch
   for step 1, then each prior step's branch for step N>1).
3. Title and body per Sections 6-7 (manual body-file flow in Section 7).

Never imply the method: `gim-pr` is the default. The brief opts out only by explicitly saying **manual**.

Use normal merge commits (not squash) on chained PRs per `git-branching-workflow` § Merge Strategy. The plan's `## Execution mode` section declares PR-per-step is on; the corresponding failure scenario ("Agent moves to step N+1 without opening a PR for step N") lives in the plan's `## Failure scenarios` section.

## 5. Choosing the base branch

Defer to `git-branching-workflow` for branch shape rules. Quick decision table:

| Situation                | `--base`                                                     |
| ------------------------ | ------------------------------------------------------------ |
| First PR of a feature    | `dev`, from `feature/<name>`. Open this first. Docs on that branch ride in this PR. A feature branch with no PR to `dev` is a ghost branch. |
| Chained task branch      | previous task branch (e.g. `seam-edges/a1-mark-curve-seam`); the first step uses `feature/<name>` |
| Standalone quick feature | `dev`                                                        |
| `[Hotfix]`               | ask the user (`main` or `dev`) — see Section 9               |
| `[Release]`              | `main` (explicit user invocation only — see Section 10)      |

Never `--base main` or `--base release/*` without explicit user consent.

## 6. Title format

`[Tag] Title-Cased Feature Name - lowercase change description`.

**Derivation rule** for the feature-name segment: humanize the branch name (replace hyphens with spaces, drop the `feature/` prefix or task-branch prefix), then Title Case each word. Example: branch `feature/seam-edges` → `Seam Edges`.

**Trailing description**: lowercase to match the commit-message voice in `agentic-planning § Commit message guidance` ("lowercase first word, past or present tense, focused on the unit of change").

**Worked example**: branch `feature/seam-edges`, change "added MarkCurveAsSeam to GimData" → final title:

```
[Feature] Seam Edges - added MarkCurveAsSeam to GimData
```

**PR-per-step note**: title format is unchanged for chained per-step PRs. PR reviewers don't see the plan file and can't cross-reference step IDs, so step IDs live in commit messages (e.g. `A1: added user model`) where the local critic correlates them against the plan — not in the PR title.

**Tag selection** (verbatim user definitions):

- `[Feature]` — code changes in general.
- `[Hotfix]` — bug fixes directly onto dev or main, intended to fix things quickly for people.
- `[Release]` — release PRs.
- `[Docs]` — documentation.

When a plan spans tags, pick the **dominant** one (largest diff / primary intent) and mention secondary work in the body. Title and commit messages get **no** harness footer — the footer is for PR body and comments only (see Section 11).

## 7. Creating the PR (manual fallback body-file flow)

Use this section only on the **fallback** path: the orchestrator's brief says
manual, `gim-pr` failed with a named precondition, or the flow is ad-hoc,
hotfix, release, or manual recovery. The standard `gim-pr` path writes
`body.md` in the evidence directory; this section documents the repo-root
fallback.

**Pre-PR validation gate**: before `gh pr create`, run build + test commands from each **touched repo's `AGENTS.md`**. Refuse to open the PR on failure unless the user overrides ("open it anyway"). Skipped on the early-open draft path (Section 8).

**Body** is filled from the plan per `references/pr-description-template.md`. On issue-linked PRs, suggest `Closes #N` (not enforced if the relationship is implicit).

Cite [gh_pr_create](https://cli.github.com/manual/gh_pr_create). **Always assign the PR to `@me`.**

**PR body (required):** do **not** embed multi-line bodies in shell (no bash HEREDOC, no PowerShell here-strings). Manually:

- Parallel PRs or issues in another repo need full GitHub URLs; `#N` autolinks only in current repo. See `WORKSPACE.md` `## Cross-repo references`.

1. Write the PR body markdown to a file at the **repo root** (e.g. `.pr-body.md`) — use the editor/Write tool, not a shell script. Substitute `<harness>` before writing (see Section 11).
2. `git push -u origin HEAD` then `gh pr create … --body-file .pr-body.md`
3. Delete `.pr-body.md` immediately after (do not commit it).

```text
# body file contents (.pr-body.md at repo root)
## Summary
<1–3 sentences>

## Changes
- <area 1>: <bullet>
- <area 2>: <bullet>

## Notes (optional)
- <migrations, env vars, follow-ups>

Closes #<issue>

__this action was performed by <harness> automatically__
```

```text
git push -u origin HEAD
gh pr create --base "<chosen-base-branch>" --title "[Feature] Seam Edges - added MarkCurveAsSeam to GimData" --assignee "@me" --body-file .pr-body.md
# then delete .pr-body.md
```

Add `--draft` only on the early-open path (Section 8). Print the resulting PR URL to the user. PR-create errors (network, branch protection, second-PR-from-same-branch rejections) are surfaced verbatim — no auto-retry.

**PR-per-step invocation** (one per step, chained per Section 4): same write → `--body-file` → delete flow; `--base` points at the previous step branch and the body is scoped to that single step:

```text
## Summary
Implements plan step A1: add `MarkCurveAsSeam` to `GimData`.

## Changes
- GIMmos: `MarkCurveAsSeam` on `GimData` + FileAccess dispatch.

Part of plan: `<path/to/plan.md>` (step A1)

__this action was performed by <harness> automatically__
```

```text
git push -u origin HEAD
gh pr create --base "<previous-step-branch>" --title "[Feature] Seam Edges - added MarkCurveAsSeam" --assignee "@me" --body-file .pr-body.md
# then delete .pr-body.md
```

The next step's PR uses this one's branch as its `--base`, forming the chain.

## 8. Early-open draft path (opt-in)

Triggered only by an explicit user phrase like **"open the PR at the start"**. Flow:

1. After the **first** commit lands on the task branch, push and open the PR as a draft with a **blank body**:

   ```text
   git push -u origin HEAD
   gh pr create --draft --base "<chosen-base>" --title "<title>" --assignee "@me" --body ""
   ```

2. Skip the pre-PR validation gate at this point — validation runs at the end before flipping to ready.
3. After the **last** step lands, write repo-root `.pr-body.md` (harness footer included), run `gh pr edit <n> --body-file .pr-body.md` ([gh_pr_edit](https://cli.github.com/manual/gh_pr_edit)), then delete `.pr-body.md`.

4. Ask consent before flipping draft → ready: `gh pr ready <n>` ([gh_pr_ready](https://cli.github.com/manual/gh_pr_ready)). Never auto-flip.

## 9. `[Hotfix]` flow

Always **ask the user** which long-lived branch the hotfix branches from and targets:

- **Production-urgent** (live bug): branch from `main`, target `main`. After merge, offer to open a back-merge PR `hotfix/<name>` → `dev` so the fix doesn't regress on the next release.
- **Pre-release-urgent** (fix needed before next release): branch from `dev`, target `dev`.

Branch name: `hotfix/<short-description>`. Title prefix: `[Hotfix]`. Announce the destination branch out loud before `gh pr create`. Ad-hoc pull requests still do not target `dev` or `main`. The required first pull request is `feature/<name>` → `dev`. Hotfix mode is the other case that may target `dev` or `main`. See `git-branching-workflow § Hotfix Branches`.

## 10. `[Release]` flow

Auto-create `[Release]` PRs **only on explicit user invocation** with the release branch name (`release/YYYY.MMDD`). Never proactively offer to cut a release. Same destination-announce rule as `[Hotfix]`. The release PR targets `main`.

## 11. Harness footer rules

The footer (own line, blank line above). Template:

```
__this action was performed by <harness> automatically__
```

Substitute `<harness>` from **runtime detection** of the agent host (e.g. `cursor`, `opencode`). If the harness cannot be detected, use `opencode`. Do **not** read harness from `agent-prefs` (same person may use multiple harnesses).

**Append** the footer on:

- The **initial** PR body at `gh pr create` / first body fill time.
- Top-level PR comments via `gh pr comment` ([gh_pr_comment](https://cli.github.com/manual/gh_pr_comment)).
- Inline-thread replies (extension or `gh api` fallback — Section 12).
- Re-request-review notes.

**Do not** append the footer on:

- The PR title.
- Commit messages.
- Subsequent `gh pr edit --body` / `--body-file` updates that already contain the footer — **preserve** the existing one. Don't strip, don't duplicate.

## 12. Reading PR feedback

Identify the user's "current PR" via `gh pr view` ([gh_pr_view](https://cli.github.com/manual/gh_pr_view)) on the current branch first; fall back to `gh pr status` and ask. External URL or `<number>` is interchangeable with `gh pr view`.

| Channel                   | Command                                                                         |
| ------------------------- | ------------------------------------------------------------------------------- |
| High-level overview       | `gh pr view <n>`                                                                |
| Top-level conversation    | `gh pr view <n> --comments`                                                     |
| Diff                      | `gh pr diff <n>`                                                                |
| CI status                 | `gh pr checks <n>` ([gh_pr_checks](https://cli.github.com/manual/gh_pr_checks)) |
| Reviews summary           | `gh pr view <n> --json reviews,reviewDecision`                                  |
| **Inline review threads** | **see below**                                                                   |

Native `gh` does not surface inline (file/line) review threads cleanly. **Preferred path** uses the optional `gh-pr-review` extension, which returns pre-joined threads as structured JSON in one command:

```bash
gh pr-review review view -R <owner/repo> --pr <n>
```

On first need for inline-thread context, run lazy detection (`gh extension list | grep gh-pr-review`). If missing, prompt for install consent (single Y/N — see `references/gh-pr-review-extension.md`).

**Fallback** (extension declined or unavailable): use the raw API ([gh_api](https://cli.github.com/manual/gh_api)):

```bash
gh api repos/:owner/:repo/pulls/<n>/comments
```

Less ergonomic — no thread joining, separate calls for replies — but always works.

## 13. Replying on the user's behalf (consent required)

Never post on the user's behalf without explicit consent. Always append the harness footer (Section 11). Never resolve threads, except a gim-wrapup reply pass: reply and then resolve each thread that brief names. Never re-request review without asking.

**Top-level PR comment**:

```bash
gh pr comment <n> --body "...

__this action was performed by <harness> automatically__"
```

**Inline-thread reply (preferred, via extension)** — uses the `thread_id` (e.g. `PRRT_kwDOAB...`) from `gh pr-review review view` output:

```bash
gh pr-review comments reply \
  --thread-id <PRRT_...> \
  --body "...

__this action was performed by <harness> automatically__" \
  -R <owner/repo> <n>
```

**Inline-thread reply (fallback)** — when the extension is unavailable:

```bash
gh api repos/:owner/:repo/pulls/<n>/comments/<comment-id>/replies \
  --method POST \
  -f body="...

__this action was performed by <harness> automatically__"
```

For the full babysit-my-PR loop (fetch → categorize → propose → fix → reply), see `references/reviewing-pr-feedback.md`.

## 14. CI failure handling

When `gh pr checks` reports failures, **auto-report** the failing checks (names + run IDs) to the user. Do not silently retry, do not auto-fix.

A gim-wrapup gather is fetch-only. Return check names, status, and conclusion. Do not offer the babysit fix, and do not push.

Then offer a **single bundled consent prompt** for diagnose+fix:

> "Pull failing logs and propose a fix? On consent I'll run `gh run view <run-id> --log-failed`, propose the fix, and push the fix commit."

On consent, run `gh run view <run-id> --log-failed` ([gh_run_view](https://cli.github.com/manual/gh_run_view)), propose the fix, push the fix commit (commit message per `agentic-planning § Commit message guidance`), and surface a summary comment with the harness footer. Babysit-mode fix commits are **ad-hoc** — not subject to per-step gating, since babysit happens post-plan.

## 15. Issue handling

Pointing the agent at a GitHub issue auto-activates **both** plan mode and PR mode. Flow:

1. Read issue context: `gh issue view <n> --comments` ([gh_issue_view](https://cli.github.com/manual/gh_issue_view)).
2. Create the issue-linked branch with the conventional name passed explicitly (satisfies both the issue link and `git-branching-workflow` naming):

   ```bash
   gh issue develop <n> --name <conventional-branch-name>
   ```

   Cite [gh_issue_develop](https://cli.github.com/manual/gh_issue_develop). Conventional-branch-name follows `git-branching-workflow` (e.g. `feature/<name>` for a base feature, `<feature>/<task>` for a chained task, `hotfix/<desc>` for a hotfix).

3. Plan + execute as normal. The PR body includes `Closes #N` so merging the PR closes the issue.

## 16. Destructive guardrails (cross-references `agentic-planning`)

Per `agentic-planning § Destructive-command guardrails`, the following `gh` commands require explicit user consent before each invocation:

- `gh pr merge` (any flavor — squash, merge, rebase)
- `gh pr close`
- `gh pr revert` (creates a new revert PR; destructive on shared branches)
- `gh pr ready` (draft↔ready flips)
- `gh pr edit --base` (changing base after open rewrites review context)
- `gh auth logout`
- `gh auth refresh`
- `gh auth token` (prints token in plaintext to terminal)
- `gh extension install`, `gh extension upgrade`, `gh extension remove` (modifies installed software on the user's machine)

`gh pr-review threads resolve` is **not destructive**. Default: never auto-resolve — only on explicit user request. A gim-wrapup reply pass is that request for the threads it names.

## 17. Quick-reference

- **Cheat-sheet of every `gh` command**: `references/gh-commands.md`.
- **GIM stack lifecycle (`gim-pr` standard, init/add/link/sync, result fields, consent, merge strategy)**: `references/gim-stack-lifecycle.md`.
- **PR description template + verbatim good/bad examples**: `references/pr-description-template.md`.
- **Babysit-my-PR loop**: `references/reviewing-pr-feedback.md`.
- **Optional `gh-pr-review` extension**: `references/gh-pr-review-extension.md`.

Cross-skills:

- Branch shapes (feature, chained, hotfix, release): `git-branching-workflow`.
- Step coherence, commit messages, destructive git guardrails: `agentic-planning`.
