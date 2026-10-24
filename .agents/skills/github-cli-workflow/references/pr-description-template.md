# PR Description Template

The body conveys **intent and scope**. The diff conveys the line-by-line truth. Aim for "informative but not bloated" — enough that a reviewer or future maintainer can understand the unit of change without paging through the diff, no more.

Cross-link: voice matches `agentic-planning § Commit message guidance` — lowercase first word in headings/bullets where natural, past or present tense, no ceremony.

## Template (copy verbatim)

```markdown
## Summary
<1–3 sentences: what this PR does, and why, in plain language.>

## Changes
- <area 1>: <bullet>
- <area 2>: <bullet>

## Notes (optional)
- <migrations, env vars, follow-ups, intentional gaps>

Closes #<issue> <!-- when applicable -->

__this action was performed by <harness> automatically__
```

The `__this action was performed by <harness> automatically__` footer is required on the **initial** body at `gh pr create` time — substitute `<harness>` via runtime detection (default `opencode` if unknown). On subsequent body updates, preserve the existing footer — don't strip, don't duplicate. Write the body to a repo-root file (e.g. `.pr-body.md`), pass `--body-file`, then delete the file — do not embed PowerShell/bash here-strings for the body.

## Verbosity bracketing — three examples

The three examples below bracket the band of acceptable verbosity. The "Bad — too short" and "Good" examples are **verbatim from the spec** and should not be paraphrased. The "Bad — too verbose" example is illustrative.

### Bad — too short (verbatim)

> this pr adds user functionality

Useless to a reviewer. No scope, no rationale, nothing to anchor a review on.

### Good — informative but not bloated (verbatim)

> this PR adds user and login functionalities to the code. changes include ... (e.g. we can enumerate migrations, db models, schemas, views, services etc...)

This is the canonical "right amount of detail": one or two sentences of summary, then a list grouped by area (migrations / models / schemas / views / services / frontend). A reviewer can read the body in 30 seconds and know which files matter.

Concretely, applying that to a real PR body:

```markdown
## Summary
This PR adds seam-edge marking to the GIM CAD pipeline and wires it through the viewer.

## Changes
- GIMmos: `MarkCurveAsSeam` on `GimData` and FileAccess dispatch for seam metadata
- GIMfront: route + service wiring for seam-edge UI actions
- tests: unit coverage for `MarkCurveAsSeam` and seam index safety

Closes #123

__this action was performed by <harness> automatically__
```

### Bad — too verbose (illustrative)

> This monumental PR introduces a brand new seam-edge system to our application. As we all know, seams are the backbone of any modern CAD product, and marking curves as seams is a critical entry point for any application that needs to differentiate between boundary and interior geometry. We have spent considerable time evaluating various approaches before settling on the implementation you see here, which carefully balances performance, usability, and maintainability. The `GimData` changes introduce new seam indices, which have carefully been designed with the following rules... (paragraphs continue restating the same points).

Restates the same points with ceremony. The reviewer is now skimming for the actual changes instead of reading them.

## Soft length guideline

If the body would exceed **~150 lines**, group bullets more aggressively by area instead of listing every file. The diff is the source of truth for line-by-line detail; the body is for orientation.

For example, instead of:

```markdown
## Changes
- GIMmos/GIMCompress/entities/GimData.cs: added MarkCurveAsSeam
- GIMmos/GIMtransfer/FileManager/FileAccess.cs: dispatch seam write
- GIMfront/src/services/seamService.js: API call wrapper
- GIMfront/src/components/viewer-components/Viewer/index.jsx: seam toggle UI
- ... (40 more bullets)
```

prefer:

```markdown
## Changes
- GIMmos: `MarkCurveAsSeam` on `GimData` + FileAccess dispatch
- GIMfront: seam-edge route, service, and viewer toggle
- tests: `MarkCurveAsSeam` unit tests + index-safety checks
```

## When to break the template

The template is a starting point, not a cage. A trivial doc-only PR can be:

```markdown
## Summary
Fixed a typo in the README install instructions.

__this action was performed by <harness> automatically__
```

A migration-only PR may not need a `Changes` section at all if the summary already names the migration.
