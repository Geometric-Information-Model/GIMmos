# `gh` Command Cheat-Sheet

Every `gh` command the skill uses, with its doc link. Native `gh` commands link to <https://cli.github.com/manual/>; extension commands link to <https://github.com/agynio/gh-pr-review>.

Commands flagged **(destructive)** require explicit user consent each time per `agentic-planning § Destructive-command guardrails` and `SKILL.md § Destructive guardrails`.

## Auth

- [`gh auth login`](https://cli.github.com/manual/gh_auth_login) — start the OAuth flow. The skill **never runs this itself**; it prints the exact command for the user.
- [`gh auth status`](https://cli.github.com/manual/gh_auth_status) — show authenticated accounts. Used to verify after the user runs `gh auth login`.
- [`gh auth logout`](https://cli.github.com/manual/gh_auth_logout) **(destructive)** — log the active account out.
- [`gh auth refresh`](https://cli.github.com/manual/gh_auth_refresh) **(destructive)** — refresh stored OAuth scopes / token.
- [`gh auth token`](https://cli.github.com/manual/gh_auth_token) **(destructive)** — prints the active OAuth token in plaintext to the terminal. Treat as a secret-disclosure command.

## PR

- [`gh pr create`](https://cli.github.com/manual/gh_pr_create) — open a PR. The skill's main write command. Always `--assignee @me`. Write body to a repo-root file (e.g. `.pr-body.md`), pass `--body-file`, then delete the file — no bash/PowerShell here-strings for the body.
- [`gh pr view`](https://cli.github.com/manual/gh_pr_view) — view PR (current branch by default; accepts `<number>`, `<url>`, or branch name). Add `--comments` for top-level conversation, `--json reviews,reviewDecision` for review summary.
- [`gh pr diff`](https://cli.github.com/manual/gh_pr_diff) — view the PR's diff (read path).
- [`gh pr status`](https://cli.github.com/manual/gh_pr_status) — list PRs relevant to the user (created, mentioned, requesting review). Used as fallback for "my PR" disambiguation.
- [`gh pr checks`](https://cli.github.com/manual/gh_pr_checks) — list CI check runs and their status for a PR. First read after a push.
- [`gh pr comment`](https://cli.github.com/manual/gh_pr_comment) — post a top-level conversation comment on a PR. Always append the harness footer.
- [`gh pr edit`](https://cli.github.com/manual/gh_pr_edit) — edit an open PR's title, body, base, labels, reviewers. Used for body fill on the early-open path (`--body-file` from a repo-root file, then delete). `--base` flag is **(destructive)** — it rewrites review context; consent required.
- [`gh pr ready`](https://cli.github.com/manual/gh_pr_ready) **(destructive)** — flip a PR between draft and ready-for-review. Consent required each time.
- [`gh pr merge`](https://cli.github.com/manual/gh_pr_merge) **(destructive)** — merge a PR (any flavor: squash, merge commit, rebase). Consent required.
- [`gh pr close`](https://cli.github.com/manual/gh_pr_close) **(destructive)** — close a PR without merging. Consent required.
- [`gh pr revert`](https://cli.github.com/manual/gh_pr_revert) **(destructive)** — open a revert PR for an already-merged PR. Destructive on shared branches; consent required.

## Issue

- [`gh issue view`](https://cli.github.com/manual/gh_issue_view) — view issue details. Use `--comments` for conversation.
- [`gh issue develop`](https://cli.github.com/manual/gh_issue_develop) — create an issue-linked branch. Pass `--name <conventional-branch>` so the branch satisfies both the issue link and `git-branching-workflow` naming.

## Run (CI / Actions)

- [`gh run view`](https://cli.github.com/manual/gh_run_view) — view a workflow run. Use `--log-failed <run-id>` to pull only the failing-step logs (consent-gated CI diagnose+fix bundle).

## API (raw + fallback paths)

- [`gh api`](https://cli.github.com/manual/gh_api) — raw REST/GraphQL access. Used as the **fallback** when the `gh-pr-review` extension is unavailable:
  - `gh api repos/:owner/:repo/pulls/<n>/comments` — list inline review comments (no thread joining).
  - `gh api repos/:owner/:repo/pulls/<n>/comments/<comment-id>/replies --method POST -f body="..."` — reply to an inline comment.

## Extension management

- [`gh extension list`](https://cli.github.com/manual/gh_extension_list) — list installed extensions. Used for **lazy** detection of `gh-pr-review` (only when inline-thread visibility is needed).
- [`gh extension install`](https://cli.github.com/manual/gh_extension_install) **(destructive: installs software on the user's machine)** — install an extension. Consent required.
- [`gh extension upgrade`](https://cli.github.com/manual/gh_extension_upgrade) **(destructive)** — upgrade an extension. Never auto; surface version-mismatch errors and tell the user to run themselves.
- [`gh extension remove`](https://cli.github.com/manual/gh_extension_remove) **(destructive)** — remove an extension. Consent required.

## Extension: `gh-pr-review`

Optional but **preferred** for inline-thread visibility. See `gh-pr-review-extension.md` for the install flow, JSON examples, and policy. Repo: <https://github.com/agynio/gh-pr-review>. Docs: [`docs/USAGE.md`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md), [`docs/SCHEMAS.md`](https://github.com/agynio/gh-pr-review/blob/main/docs/SCHEMAS.md), [`docs/AGENTS.md`](https://github.com/agynio/gh-pr-review/blob/main/docs/AGENTS.md).

- [`gh pr-review review view`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — full snapshot of reviews + inline threads + replies as JSON. Filters: `--unresolved`, `--not_outdated`, `--reviewer <login>`, `--states APPROVED,CHANGES_REQUESTED,COMMENTED,DISMISSED`, `--tail <n>`, `--include-comment-node-id`.
- [`gh pr-review comments reply`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — reply to an inline thread by `thread_id`. Always append harness footer.
- [`gh pr-review threads list`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — lighter-weight thread enumeration. Filters include `--unresolved` and `--mine`.
- [`gh pr-review threads resolve`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — resolve a thread by `thread_id`. **Never auto** — only on explicit user request, or a gim-wrapup reply pass.
- [`gh pr-review threads unresolve`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — un-resolve a thread by `thread_id`. Same policy.
- **Advanced — agent acting as reviewer** (out-of-scope by default; only on explicit user invocation):
  - [`gh pr-review review --start`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — open a pending review, returns `PRR_...` ID.
  - [`gh pr-review review --add-comment`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — add an inline comment to a pending review.
  - [`gh pr-review review --submit`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — finalize the pending review with `--event APPROVE|REQUEST_CHANGES|COMMENT`.

## See also

- `SKILL.md` — full activation, consent, and footer policy.
- `pr-description-template.md` — body template + verbosity examples.
- `reviewing-pr-feedback.md` — babysit-my-PR loop.
- `gh-pr-review-extension.md` — extension install / fallback policy.
- `agentic-planning` — destructive-command guardrails (cross-skill source of truth).
- `git-branching-workflow` — base-branch rules for `gh pr create --base`.
