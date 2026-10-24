# `gh-pr-review` Extension (optional)

The optional [`agynio/gh-pr-review`](https://github.com/agynio/gh-pr-review) extension is the **preferred** path for reading and replying to inline review threads. Native `gh` does not surface inline review threads cleanly; this extension does, with one structured-JSON command instead of a multi-call `gh api` chain.

The extension is **optional**. If the user declines installation or it's unavailable, **fallback to `gh api`** as documented in `SKILL.md § Reading PR feedback`.

## 1. What it is + why we use it

Native `gh pr view <n> --comments` only returns the top-level conversation timeline. It does **not** show inline (file/line) review threads, which are where most actionable code-review feedback lives.

The `gh-pr-review` extension adds:

- `gh pr-review review view` — one command that returns reviews + inline threads + replies pre-joined as deterministic, compact JSON. LLM-friendly: no manual correlation, no follow-up `gh api` calls per thread.
- `gh pr-review comments reply` — reply to an inline thread by `thread_id` directly.
- `gh pr-review threads list/resolve/unresolve` — thread enumeration and (on explicit user request only) state changes.

Cheaper than the `gh api` chain in token count, latency, and parsing complexity.

## 2. Detection (lazy)

Detection runs **only when inline-thread visibility is needed** — not on every preflight, not on session start. The first time the agent needs threads (user asks about feedback / comments / inline reviews):

```bash
gh extension list | grep gh-pr-review
```

If present → use it. If absent → prompt for install (Section 3).

## 3. Install (consent-gated)

If `gh` itself is missing, offer the repo install script first (with consent):

```bash
python .agents/skills/github-cli-workflow/scripts/install_gh.py
```

This installs `gh` **and** `agynio/gh-pr-review` by default. Pass `--no-extension` to install `gh` only. Use `--dry-run` to preview commands. See `SKILL.md § Preflight + fallbacks`.

If `gh` is already installed but the extension is missing, single Y/N prompt — the agent **never** runs `gh extension install` without explicit user consent. Consent prompt verbatim:

> "For inline review threads, install `gh-pr-review` with: `gh extension install agynio/gh-pr-review`. OK to install now?"

On consent:

```bash
gh extension install agynio/gh-pr-review
```

Listed in the destructive list (`SKILL.md § Destructive guardrails`, `agentic-planning § Destructive-command guardrails`) because it installs software on the user's machine.

On decline → drop to the fallback path (Section 10). Don't re-prompt later in the same session.

## 4. Upgrade (never auto)

```bash
gh extension upgrade agynio/gh-pr-review
```

The agent **never** runs upgrade automatically. On version-mismatch errors from the extension, surface the error and tell the user to run the upgrade command themselves.

## 5. Read commands (preferred over `gh api`)

### `review view` — full snapshot

```bash
gh pr-review review view -R <owner/repo> --pr <n>
```

Returns reviews + inline threads + replies as a single JSON document.

Useful filters:

- `--unresolved` — only threads not resolved.
- `--not_outdated` — only threads still pinned to the current diff (skips threads on lines that have moved).
- `--reviewer <login>` — only threads from a specific reviewer.
- `--states APPROVED,CHANGES_REQUESTED,COMMENTED,DISMISSED` — filter top-level review state.
- `--tail <n>` — only the most recent N threads.
- `--include-comment-node-id` — include the GraphQL node ID per comment (occasionally useful for `gh api` cross-references).

Example (most common — only actionable feedback against the current diff):

```bash
gh pr-review review view -R agynio/example --pr 42 --unresolved --not_outdated
```

### `threads list` — lightweight enumeration

```bash
gh pr-review threads list -R <owner/repo> <n>
```

Useful filters: `--unresolved`, `--mine` (threads where the active user has commented).

Use this when you only need thread IDs and their resolved/unresolved status, not the full content.

## 6. Reply path (preferred over `gh api .../replies`)

```bash
gh pr-review comments reply \
  --thread-id <PRRT_...> \
  --body "<reply text>

__this action was performed by <harness> automatically__" \
  -R <owner/repo> <n>
```

`thread_id` values (e.g. `PRRT_kwDOAB...`) come from the `review view` output. Always append the harness footer per `SKILL.md § Harness footer rules`.

## 7. Resolve / unresolve threads (consent required, never auto)

```bash
gh pr-review threads resolve --thread-id <PRRT_...> -R <owner/repo> <n>
gh pr-review threads unresolve --thread-id <PRRT_...> -R <owner/repo> <n>
```

Per the agent's default, **never auto-resolve** — only on an explicit user request ("resolve thread X", "mark thread X resolved"), or on a gim-wrapup reply pass for the threads that brief names.

## 8. Advanced — agent acting as reviewer (out-of-scope by default)

Documented for completeness; only used when the **user explicitly asks** the agent to act as a reviewer on someone else's PR.

```bash
gh pr-review review --start \
  -R <owner/repo> --pr <n>
```

Opens a pending review and returns a `PRR_...` ID.

```bash
gh pr-review review --add-comment \
  --review-id <PRR_...> \
  --path <file> --line <n> \
  --body "..." \
  -R <owner/repo> --pr <n>
```

Adds an inline comment to a pending review.

```bash
gh pr-review review --submit \
  --review-id <PRR_...> \
  --event APPROVE|REQUEST_CHANGES|COMMENT \
  --body "..." \
  -R <owner/repo> --pr <n>
```

Finalizes the pending review.

## 9. JSON output examples

The shapes below are illustrative (verbatim shapes from the extension's docs); the extension's [`docs/SCHEMAS.md`](https://github.com/agynio/gh-pr-review/blob/main/docs/SCHEMAS.md) is the source of truth.

### Pending review (returned by `review --start`)

```json
{
  "review_id": "PRR_kwDOAExample123",
  "state": "PENDING",
  "pr_number": 42,
  "repository": "agynio/example"
}
```

### `review view` snapshot (reviews + threads + replies pre-joined)

```json
{
  "pr": { "number": 42, "title": "[Feature] ..." },
  "reviews": [
    {
      "review_id": "PR_review_node_id",
      "state": "CHANGES_REQUESTED",
      "author": "reviewer1",
      "body": "see inline"
    }
  ],
  "threads": [
    {
      "thread_id": "PRRT_kwDOAB...",
      "is_resolved": false,
      "is_outdated": false,
      "path": "services/auth.py",
      "line": 42,
      "comments": [
        {
          "comment_id": 999001,
          "node_id": "PRRC_kwDOAB...",
          "author": "reviewer1",
          "body": "null check missing on user.email"
        },
        {
          "comment_id": 999002,
          "in_reply_to_id": 999001,
          "author": "agentcursor",
          "body": "Fixed in <sha>: added null check.\n\n__this action was performed by <harness> automatically__"
        }
      ]
    }
  ]
}
```

### `threads list`

```json
[
  { "thread_id": "PRRT_kwDOAB...", "is_resolved": false, "path": "services/auth.py", "line": 42 },
  { "thread_id": "PRRT_kwDOAC...", "is_resolved": true,  "path": "tests/test_auth.py", "line": 18 }
]
```

### `threads resolve` response

```json
{ "thread_id": "PRRT_kwDOAB...", "is_resolved": true }
```

## 10. Fallback when extension unavailable / declined

If the user declined install or `gh extension list` shows the extension missing and re-prompting is inappropriate, **fall back to `gh api`** for both read and reply paths:

- Read: `gh api repos/:owner/:repo/pulls/<n>/comments` — see `SKILL.md § Reading PR feedback`.
- Reply: `gh api repos/:owner/:repo/pulls/<n>/comments/<comment-id>/replies --method POST -f body="..."` — see `SKILL.md § Replying on the user's behalf`.

The fallback is functionally complete (the extension is a convenience layer over the same API). It's just less ergonomic — no thread joining, no built-in `--unresolved` filter, separate calls to fetch replies.

## 11. Doc links

- Repo: <https://github.com/agynio/gh-pr-review>
- [`docs/USAGE.md`](https://github.com/agynio/gh-pr-review/blob/main/docs/USAGE.md) — full command reference.
- [`docs/SCHEMAS.md`](https://github.com/agynio/gh-pr-review/blob/main/docs/SCHEMAS.md) — JSON output schemas.
- [`docs/AGENTS.md`](https://github.com/agynio/gh-pr-review/blob/main/docs/AGENTS.md) — guidance for agentic consumers.
