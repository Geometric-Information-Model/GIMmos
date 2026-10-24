# Agent orchestration commands in the toolkit

## Context

The fleet repeatedly performed the same deterministic chains in prose: repository preflight, commit and pull request creation, tracker search and creation, tracker transition, and wrapup gathering and replies. Those chains were expensive, inconsistently shaped, and error-prone; the flow ledger recorded a broken commit command and repeated three-repository preflights. The intent was to replace the chains with one command per job.

## Decision

Create a fleet tier inside the existing toolkit: commands for preflight, pull request creation, tracker search, tracker creation, tracker transition, wrapup gathering, and wrapup replies. They share one process runner with a hard timeout, captured output, exact failure reporting, redaction of personal identifiers, and dry-run support on every command that mutates repository state. Preflight also reports the highest document numbers seen locally, on the expected remote branch, and in open pull requests, so plan and ADR numbering does not collide. The data-only commands keep their existing contracts; the fleet tier adds process execution and network access under the same no-shell, argv-based controls. The broken commit command and the linter role are removed.

Basic flow: an agent invokes one fleet command, the command validates its arguments and resolves the repository, runs the underlying git, GitHub, or Jira operations in order, stops on the first failure with the exact command and error, and returns a compact text result plus structured JSON.

**Status:** accepted. Amends ADR 0005 by separating the data-only tier from the fleet tier while keeping the no-shell and no-code-argument controls.

## Considered Options

- Fleet tier inside the existing toolkit (chosen): one module, one auto-load path, one allowlist vocabulary.
- Separate fleet module: rejected; two modules and two allowlists for one vocabulary.
- Raw command chains in agent bodies: rejected; repetitive, inconsistently shaped, and error-prone.
- Keep the old commit command: rejected; it is broken and the new pull request command supersedes it.
- Keep the linter role: rejected; linting is removed from the cycle.
