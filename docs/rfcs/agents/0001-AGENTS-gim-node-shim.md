# RFC 0001 — Node as the execution layer for heavy GIM fleet verbs

Status: Accepted (adopted for gim-wrapup-gather; gim-wrapup-respond remains PowerShell)
Date: 2026-10-21
Owner: orchestrator
Related: plan 0007 (agent orchestration scripts), ADR 0008 (GIMTools fleet tier)

## Context

`gim-tools` is a PowerShell 7 module exposing the `gim-*` vocabulary (25 verbs
today across nine groups). Data verbs (`gim-list`, `gim-grep`, `gim-inspect`, and
similar) are simple, contained, and fast. Fleet verbs orchestrate external
processes (`git`, `gh`, `acli`), normalize hundreds of KB of JSON, and
increasingly need concurrency and streaming.

The `gim-wrapup-gather` work exposed the friction:

- a fragile atomic write (`[System.IO.File]::Replace`) failed at runtime;
- sequential per-PR fetching was slow on chains;
- thread normalization was written against a shape the extension never emits;
- streaming multi-MB CI logs into PowerShell strings fights the module's bounded
  output caps (`Invoke-GimFleetExec` caps captured stdout at 256 KiB).

Each is fixable in PowerShell, but the fixes are hand-rolled: runspace pools,
manual chunked reads, byte-boundary trimming, ad-hoc semaphores. That is
recurring complexity for a class of verbs whose job is exactly process
orchestration.

## Decision

Adopt **Node (stdlib only)** as the execution layer for **heavy orchestration
verbs**, behind an unchanged PowerShell `gim-*` interface.

- The PowerShell verb remains the allowlisted surface: argument parsing, repo
  resolution, containment checks, atomic writes, and the JSON envelope.
- A dependency-free Node helper (`.mjs` under
  `.agents/skills/gim-tools/scripts/helpers/`) owns concurrency, streaming,
  chain/API walking, and normalization.
- Agents never invoke `node` directly; only the verb spawns it. The permission
  allowlist and the `gim-*` vocabulary do not change.
- Data-only verbs stay PowerShell. Migration is per verb and reversible.

### Criteria for moving a verb to Node

Move a verb when two or more hold: concurrent external processes; streaming or
multi-megabyte artifacts; non-trivial JSON/API normalization; more than ~300
lines of PowerShell logic for the verb's core.

### Helper conventions

- One `.mjs` per verb; Node ≥ 20 (already required by CodeGraph).
- Standard library only; no npm packages, no lockfile.
- UTF-8 without BOM; LF; JSON to stdout, diagnostics to stderr, non-zero exit
  on failure.
- A fixture mode for tests; no network in tests.
- Boundedness is the helper's job (caps, budgets, truncation markers).

### Alternatives considered

- **PowerShell-only** (runspace pools, streamed reads): viable but accumulates
  ad-hoc parallel plumbing per verb and fights the module's bounded-capture
  model. Rejected as the default for heavy verbs.
- **.NET helper**: stronger typing and AOT performance, but adds a project,
  build/publish step, and a binary inside `.agents/`; shares no domain types
  with the fleet-glue layer (`GimData` is product-side). Rejected for this
  layer.
- **Full rewrite of gim-tools**: large blast radius, no need. Rejected;
  incremental per-verb adoption instead.

## Consequences

- Node becomes a documented prerequisite for migrated verbs (setup guide).
- Two test layers: Pester covers the shim contract and drives the helper with
  fixtures; optional `node:test` suites are human-run per the existing Node
  test policy.
- The allowlist, agent preloads, and CLI surface stay as they are.
- Debugging crosses one process boundary; the helper must emit actionable
  stderr and structured failure objects.

## Rollout

1. `gim-wrapup-gather` (this plan, E1) — first adopter.
2. Evaluate `gim-pr` and `gim-preflight` after the gather has run through a
   wrapup cycle.
3. Data verbs stay PowerShell unless they meet the criteria.

## Risks

- Node version drift — pin `>=20` and fail clearly.
- Scope creep into npm dependencies — stdlib-only is a hard rule.
- Test-policy drift — helper tests are human-run like the OpenCode plugin tests.
- Encoding regressions — UTF-8 discipline and fixture coverage for non-ASCII.
