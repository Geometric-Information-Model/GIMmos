---
name: git-branching-workflow
description: Branching, chained PR, and merge strategy for this repo — long-lived branches dev/main/release/*, base feature branches feature/<name> from dev, leaf-safe step branches <name>/<stepid>-<title>, docs-only exception on feature/*, normal merge commits (no squash on chained PRs), release/YYYY.MMDD release branches. Use when creating branches, opening PRs, planning a release, or merging into dev/main.
---

# Branching and Git Workflow

Canonical leaf-safe topology: [`docs/branching-strategy.md`](../../../docs/branching-strategy.md).

## Branches and Release Flow

- **Long-lived branches**: `dev`, `main`, and `release/*`.
- **Feature development** starts from `dev`.
- **Release branches** are cut from `dev` using the pattern `release/YYYY.MMDD` (example: `release/2026.0118`).
- **Production** is `main` after a release branch is QA'd and merged.

For repos whose default branch is not yet `dev`/`main`, see [references/branch-debt.md](references/branch-debt.md).

## Naming rules (always)

- **All branch names are lowercase** (including Jira keys in the name: `gim-01234`, not `GIM-01234`).
- **Leaf-safe topology**: a git ref cannot be both a branch tip (leaf) and a folder. Never create step/task branches *under* an existing `feature/<name>` leaf.

```text
CORRECT:
  feature/foo-bar                 ← base feature branch (leaf)
  foo-bar/a1-add-foo              ← step branch (sibling path, NOT under feature/)
  foo-bar/a2-add-bar
  foo-bar/b1-combine-foo-bar

WRONG (will break):
  feature/foo-bar/a1-add-foo      ← treats feature/foo-bar as a folder after it already exists as a leaf
```

## Feature Branch Hierarchy

### Base Feature Branch

- When starting a new feature, create a base feature branch from `dev`:
  - Example: `feature/seam-edges`
  - With Jira: `feature/gim-01234-add-foobar`
- **Never commit implementation code** directly to the base feature branch.
- All **implementation** changes to the base feature branch must come through PRs from step/task branches.

### Docs-only exception on `feature/*`

- **Exception:** you should commit **FeatureDocuments** directly on the base `feature/<name>` branch: plan, ADR, RFC, thin pointer plans, and similar feature docs only.
- Implementation, tests, and non-doc skill/agent code still go through step/task branches (or prefs-driven step commits), never straight onto `feature/*`.

### Step / Task Branches (Chained, leaf-safe)

- Create step/task branches off the most recent step branch (or from the base feature branch for the first step), **never** as `feature/<name>/…`.
- Pattern: `<name>/<stepid>-<title>` where `<name>` matches the feature slug **without** the `feature/` prefix, `<stepid>` is lowercase alphanumeric (e.g. `a1`), and `<title>` is a short lowercase slug.

```text
feature/seam-edges
  → seam-edges/a1-mark-curve-seam
  → seam-edges/a2-export-seam-metadata

feature/gim-01234-add-foobar
  → gim-01234-add-foobar/a1-add-model
  → gim-01234-add-foobar/a2-add-service
```

- Correct: `seam-edges/a1-mark-curve-seam`
- Incorrect: `feature/seam-edges/a1-mark-curve-seam`
- Incorrect: `feature/seam-edges/mark-curve-seam`
- Incorrect: `Seam-Edges/A1-Mark-Curve` (must be lowercase)

### PR Flow (Chained)

The first pull request is always `feature/<name>` → `dev`. Open it when the feature branch exists. If the feature has docs, they are committed on `feature/<name>` and that pull request carries them. A `feature/<name>` branch with no pull request to `dev` is a ghost branch. Do not open step pull requests and leave the feature branch unlinked.

Stacked PR mechanics are owned by `github-cli-workflow` §4 + `references/gim-stack-lifecycle.md`. Keep the branch shapes below leaf-safe: the first PR is `feature/<name>` -> `dev`; each step branch is cut from the previous step branch. `gim-pr` runs `init`/`add`/`link`/`sync` internally; direct `gh stack` mutations are consent-gated. The merge strategy below still applies (normal merge commits, never squash).

1. Open PR: `feature/seam-edges` → `dev`. Docs on `feature/seam-edges` are in this pull request.
2. Create `seam-edges/a1-mark-curve-seam` from `feature/seam-edges`.
3. Open PR: `seam-edges/a1-mark-curve-seam` → `feature/seam-edges`.
4. If more work is needed, branch from the latest step branch:
   - Create `seam-edges/a2-export-seam-metadata` from `seam-edges/a1-mark-curve-seam`.
5. Open PR: `seam-edges/a2-export-seam-metadata` → `seam-edges/a1-mark-curve-seam`.

## Hotfix Branches

- Hotfixes patch urgent issues. Branch name: `hotfix/<short-description>` (e.g. `hotfix/null-pointer-in-billing`).
- Base/target depends on urgency — always confirm with the user:
  - **Production-urgent** (live bug): branch from `main`, target `main`. After merge, open a back-merge PR `hotfix/<name>` → `dev` so the fix doesn't regress on the next release cut.
  - **Pre-release-urgent** (fix needed before next release): branch from `dev`, target `dev`.
- Title prefix is `[Hotfix]`. Ad-hoc pull requests still do not target `dev` or `main`. The required first pull request is `feature/<name>` → `dev`. Hotfix mode is the other case that may target `dev` or `main`; announce that destination before creating the pull request.

## Merge Strategy

- For PRs into `feature/*` and for chained PRs, **use normal merge commits**.
- **Do not use squash merges** in chained PRs to avoid conflict amplification.

## Opening PRs with gh

When `gh` is available, open PRs from the CLI using the base branch rules above:

- First pull request of a feature → `--base dev` from `feature/<name>`. Open this before any step pull request. Docs on `feature/<name>` ride in it.
- Chained step/task branch → `--base <previous-step-branch>` (the first step uses `--base feature/<name>`)
- Quick feature branch → `--base dev`
- `[Hotfix]` → ask the user (main or dev)
- Never `--base main` or `--base release/*` without explicit consent.

For the `gh pr create` invocation, PR description format, and how to read review feedback, see `github-cli-workflow`. If unsure which branch is the previous one in the chained PR case, ask the user (and provide options, optionally)

## Final Integration

- The `feature/<name>` → `dev` pull request is already open. Once the feature is complete, a senior developer merges that pull request and resolves any conflicts at that time.

For per-step commit discipline within a step branch, see `agentic-planning`.
