# Leaf-safe branch topology for feature + step PRs

Git refs cannot be both a leaf and a folder. Feature work therefore uses a base leaf `feature/<name>` (all lowercase) plus sibling step branches `<name>/<stepid>-<title>` — never `feature/<name>/<step>`. Plan/ADR/RFC/thin-plan **FeatureDocuments** may commit on the base `feature/*` branch (docs-only exception); implementation lands only via step branches. The first pull request is always `feature/<name>` → `dev`, and it carries those docs when they exist. A `feature/<name>` branch with no pull request to `dev` is a ghost branch. With Jira: `feature/gim-01234-add-foobar` then `gim-01234-add-foobar/a1-…`.

**Status:** accepted

## Considered Options

- **Leaf-safe siblings** (chosen) — `feature/foo-bar` + `foo-bar/a1-add-foo`; avoids ref/folder conflicts.
- **`feature/<name>/<step>` nesting** — rejected; once `feature/<name>` exists as a branch tip, git cannot create children under that path.
