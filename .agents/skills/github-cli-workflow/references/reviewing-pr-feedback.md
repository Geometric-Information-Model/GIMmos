# Reviewing PR Feedback — the "Babysit My PR" Loop

Use this loop when the user says "look at my PR comments", "babysit my PR", "address the review feedback", or any read-then-respond intent. Each step gates on consent for anything that writes.

## 1. Fetch all feedback channels

Run all of these against the PR — they cover non-overlapping channels:

- **Top-level conversation**: `gh pr view <n> --comments`
- **CI status**: `gh pr checks <n>`
- **Reviews summary** (approval state, reviewer list, decisions): `gh pr view <n> --json reviews,reviewDecision`
- **Inline review threads (file/line)**: prefer the [`gh-pr-review` extension](https://github.com/agynio/gh-pr-review) — see `gh-pr-review-extension.md`:

  ```bash
  gh pr-review review view -R <owner/repo> --pr <n>
  ```

  Returns reviews + inline threads + replies pre-joined as structured JSON. Use `--unresolved` and `--not_outdated` to focus only on actionable threads.

  **Fallback** if the extension is unavailable or the user declined install:

  ```bash
  gh api repos/:owner/:repo/pulls/<n>/comments
  ```

  Less ergonomic — you'll have to manually correlate `in_reply_to_id` chains and pull replies separately — but always works.

## 2. Categorize feedback

Bin every comment into one of four buckets:

- **must-fix**: explicit `CHANGES_REQUESTED` review item, broken behavior, or correctness bug.
- **suggestion**: reviewer prefers an alternative; not blocking but worth considering.
- **question**: reviewer asks for clarification or rationale.
- **nit**: stylistic, low-impact (typos, naming preferences, ordering).

Track each item with its location (file/line if inline, or "top-level"), the reviewer login, and the `thread_id`/comment id needed to reply.

## 3. Present the categorized list

Show the user a structured summary with proposed actions per item. Group by bucket. For example:

> **must-fix (2)**
> - `services/auth.py:42` (@reviewer1): null check missing on `user.email`. **Proposed**: add `if user is None or user.email is None: raise ...`.
> - `tests/test_auth.py:18` (@reviewer1): missing assertion on error path. **Proposed**: add `assert response.status_code == 400`.
>
> **suggestion (1)**
> - top-level (@reviewer2): consider extracting the validation into a helper. **Proposed**: defer to follow-up issue (out of scope for this PR).
>
> **question (1)**
> - `services/auth.py:55` (@reviewer3): "why hash twice?" **Proposed**: reply explaining the legacy compatibility reason.

Wait for consent before any code changes or replies.

## 4. Address per `agentic-planning § Authoring a plan`

On consent for code changes:

- Trivial fix (one or two files, obvious change) → direct edit, no plan.
- Non-trivial fix (multiple files, multiple steps, refactor) → author a small plan first per `agentic-planning`.

Validate after each change with the area's tests per `agentic-planning § Validate after every step`.

## 5. Push fix commits

Follow `agentic-planning § Commit message guidance`:

- Lowercase first word.
- Past or present tense.
- 1–3 sentences max — one well-chosen line is usually enough.
- No ceremony ("WIP:", "Final:", "Done:").

Babysit-mode commits are **ad-hoc** — not subject to per-step gating, since babysit happens post-plan. Push as you go.

## 6. Post replies (consent required)

On consent, reply on each addressed thread. **Always append the harness footer** (own line, blank line above):

```
__this action was performed by <harness> automatically__
```

**Inline-thread reply (preferred, via extension)**:

```bash
gh pr-review comments reply \
  --thread-id <PRRT_...> \
  --body "Fixed in <commit-sha>: added null check on user.email.

__this action was performed by <harness> automatically__" \
  -R <owner/repo> <n>
```

The `thread_id` (e.g. `PRRT_kwDOAB...`) comes directly from the `gh pr-review review view` JSON output (Step 1).

**Inline-thread reply (fallback, via `gh api`)**:

```bash
gh api repos/:owner/:repo/pulls/<n>/comments/<comment-id>/replies \
  --method POST \
  -f body="Fixed in <commit-sha>: added null check on user.email.

__this action was performed by <harness> automatically__"
```

**Top-level summary comment** (use after a batch of fixes to summarize what changed):

```bash
gh pr comment <n> --body "Addressed all must-fix feedback in <commit-sha-range>:
- null check on user.email (auth.py)
- assertion on error path (test_auth.py)

Suggestions and questions answered inline.

__this action was performed by <harness> automatically__"
```

## 7. Re-request review (consent required)

Never auto-request review. After all addressed items have replies, ask the user:

> "Re-request review from @reviewer1? `gh pr edit <n> --add-reviewer reviewer1`"

On consent, run the command. Otherwise leave it to the user.

**Never resolve threads** outside gim-wrapup. A gim-wrapup reply pass resolves each thread it names after the reply. Otherwise leave the resolve action to the reviewer (see `gh-pr-review-extension.md`).
