# Architectural Decisions

This index lists the fleet ADRs in this directory. They are grouped by area, and within each area by topic. Product ADRs are listed in [the repository index](../0000-INDEX.md).

## Fleet

### Cursor

- [0001: Cursor orchestrator nesting (skill-on-main)](0001-AGENTS-cursor-orchestrator-nesting.md)

### Homes

- [0002: OpenCode fleet dual-home and local config](0002-AGENTS-opencode-fleet-dual-home.md)

### OpenCode

- [0004: Shared bash allowlist owned by user-local opencode config](0004-AGENTS-shared-bash-allowlist-local-config.md)

### Preload

- [0009: Per-agent context preload on OpenCode and Cursor](0009-AGENTS-context-preload.md)

## Toolkit

### Layout

- [0003: gim-* toolkit home: agent skill layout in GIMmos](0003-AGENTS-gim-toolkit-skill-home.md)

### Arguments

- [0005: gim-* toolkit design invariants: data-only arguments, no script blocks](0005-AGENTS-gim-toolkit-data-only-invariants.md)

### Execution

- [0006: gim-run / gim-probe execution confinement](0006-AGENTS-gim-run-execution-confinement.md)

### Permissions

- [0007: Harness permission wiring — gim-tools allow/deny + hooks](0007-AGENTS-harness-permission-wiring.md)

### Fleet verbs

- [0008: Agent orchestration commands in the toolkit](0008-AGENTS-gim-fleet-verbs.md)
