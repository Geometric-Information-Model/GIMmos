# GIM stack lifecycle (gim-pr)

Authoritative GIM guidance for the PR/stack lifecycle. `gh-stack` itself is a
vendor-owned skill; read it for command semantics, not for GIM policy.

## Standard path (default)

`gim-pr` is the **default** mechanics for the standard per-step PR flow. The
brief opts out only by explicitly saying **manual**. The orchestrator writes
the evidence files
(`.agent-scratch/pr-files/<feature>/<step>/files.txt` and `body.md`) in the
target repo and hands the full PR package: repo, branch, commit message, files
file, body file, title, target, and `--new-branch` when the step needs one.
The version-controller invokes `gim-pr` with exactly those parameters.

If the brief explicitly opts out (**manual**), run the manual flow from the
brief's information. If `gim-pr` fails, fall back to the manual flow with the
same package and **report the failure plus exactly what was done instead**.

Manual push + `gh pr create` is the fallback when:

- the orchestrator's brief explicitly opts out (**manual**); or
- `gim-pr` fails, with or without a named precondition; or
- the flow is not the usual per-step chain (ad-hoc work, hotfix, release, or
  manual recovery).

## Git config first

Before any stack command, `gim-pr` sets two keys in the target repo's local
config:

```text
git config rerere.enabled true
git config push.autoSetupRemote true
```

Both are idempotent and transparent. `rerere` removes `gh stack init`'s
interactive prompt (upstream `init` asks under a TTY) and helps cascade
rebases; `autoSetupRemote` makes a plain push set its upstream.

## First PR (no `--new-branch`)

`gh stack init --base <target> <current-branch>` adopts the current feature
branch into a new local stack with trunk `<target>`. The target must exist
locally; a missing base fails with gh's exact error.

Record:

- `stack.method=init`
- `stack.pending=true`
- `stack.number=null`
- `stack.verified=null`
- `stack.link=null`, `stack.sync=null`

No link, no sync. A one-PR stack has no remote stack object yet; pending is
expected, not a failure.

## Step PR (`--new-branch <branch>`)

Uncommitted step work sits on the current stack top. `gh stack add <branch>`
creates and checks out the branch inside the local stack and does **not**
commit; `-A`, `-u`, and `-m` are never passed. Upstream behavior: `add` must run
from the top branch, and uncommitted changes carry over onto the new branch.

Then `gim-pr` stages its explicit file list, commits, pushes, and creates the
PR with `--base <target>` (the previous branch).

## Link: extend the remote stack

After the step PR is created, read the target step's `result.json`:

- `stack.number` present → `gh stack link <N> <branch> --remote <remote>`
  (append).
- `stack.number` null (target is the first PR) →
  `gh stack link --base <target.pr.base> <target-branch> <branch> --remote
  <remote>` (create). The base comes from the target result's `pr.base`, never
  from a `gh pr view` walk.

Failures: non-zero exit, any warning line, or a missing `stack #N` in the link
output is `ok=false`. The last `stack #N` wins, so an output echoing an older
stack number cannot false-fail the sync comparison. Raw output is saved as
`stack.link = { exit, output }`.

Upstream link behavior to respect:

- A numeric first argument is treated as a stack number only when a stack with
  that number exists; the remaining arguments are appended.
- Arguments already in the stack are skipped; arguments belonging to a
  different stack are rejected.
- Stack membership is additive only; `link` never removes a PR.
- `link` may create PRs for branch arguments that lack one, so pass only the
  target and the new branch. Both already have PRs after `gim-pr`'s
  `gh pr create`, so `link` reuses them.
- Multiple remotes: always pass `--remote <name>` (otherwise
  `remote.pushDefault` must be configured).

## View: branch count K (URL may be null)

`gh stack view --json` supplies the local branch count K (`branches` array
length). Installed output has no top-level `url` field, so `stack.url` may be
null; do not promise it. A failed or unusable view is `stack.view_error` plus a
warning, and downgrades the K check to number-only.

Upstream view schema: `trunk`, `currentBranch`, `branches[]` (`name`, `head`,
`base`, `isCurrent`, `isMerged`, `isQueued`, `needsRebase`), and
`branches[].pr` (`number`, `url`, `state`; absent when no PR exists). JSON goes
to stdout; status messages go to stderr. Use `--json`; bare `view` opens a TUI
under a PTY.

## Sync: verification only

`gh stack sync --remote <remote>` is GIM's verification step; it does not create
the stack from a new PR. The command still performs its normal fetch,
reconcile, rebase, and push work; GIM's success criterion is the count line.
Success requires:

- exit 0;
- no warning line (the warning-sign glyph, or its mojibake/`?`-prefix
  fallback);
- a stack-count line `(K PRs...) (stack #N)` in either observed wording:
  - `Stack on GitHub is up to date with K PRs (stack #N)`
  - `Linked to the existing stack on GitHub (K PRs, already up to date)
    (stack #N)`
- last matching line wins;
- N equals the number parsed from the link output;
- K equals the view's branch count when the view succeeded.

`Stack synced` alone never counts. A missing count line, an N mismatch, or a K
mismatch is `ok=false` with the sync output in the error. Raw output and the
count line are saved as `stack.sync = { exit, output, count_line }`.

Recorded sync behavior: sync never adopts a PR into an existing stack. It can
print `⚠ <branch> has no PR`, report the existing stack "up to date", and still
end with `✓ Stack synced`. The link step is mandatory before sync. Non-interactive
divergence can print `Sync aborted` and exit 0 with no count line; that fails
the count-line check.

Treat any non-zero exit as failure. Key upstream codes: 2 = not in a stack,
3 = rebase conflict (sync restores branches), 8 = stack file locked, 9 =
stacked PRs unavailable on the repository.

On full pass: `stack.method=add`, `stack.number=N`, `stack.pending=false`,
`stack.verified=true`. Any failure leaves `stack.verified=false` (and keeps a
parsed link number when available).

## Result-field meanings

- `stack.pending`: true only for the first PR; no remote stack object exists
  yet.
- `stack.number`: remote stack number once linked.
- `stack.verified`: true only when link and sync verification both pass.
- `stack.view_error`: view failure diagnostic; warning, never a failure.
- `stack.error`: first failed stack step's diagnostic.

## Hard rules

- No `gh stack checkout` in `gim-pr`.
- No baseRef walk and no full-chain fallback.
- Never call `gh stack submit` to repair a `gim-pr` result. It is manual
  recovery only.
- Only `gh stack view` is read-only/allowlisted. Direct `gh stack add`,
  `link`, `submit`, `sync`, `push`, `merge`, and `rebase` are consent-gated:
  they create branches/PRs, rewrite bases, rebase, or force-push. `gim-pr` runs
  `init`, `add`, `link`, and `sync` internally.
- Chained PRs use **normal merge commits**. Never squash-merge a stack.
  `gh stack merge` is all-or-nothing and consent-gated; a base-branch merge
  queue overrides the method. `gh pr merge` cannot merge a stack.
