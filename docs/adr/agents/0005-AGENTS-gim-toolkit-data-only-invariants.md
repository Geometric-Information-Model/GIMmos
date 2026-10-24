# gim-* toolkit design invariants: data-only arguments, no script blocks

Every `gim-*` verb takes **data-only arguments**; no verb accepts a command, script block, or expression. This is a control, not an ergonomic preference: the harness bash permission checker is blind to .NET member invocation (`[Type]::Method()`) inside script blocks, so any allowlisted cmdlet accepting a `{ }` body is a channel to the whole .NET surface (file I/O, assembly load, process spawn). The block-accepting cmdlets `ForEach-Object`, `Where-Object`, `Group-Object`, `Sort-Object`, `Select-Object` are therefore **denied** wherever the harness is configured (this build ships no config; the deny list is documented in `references/harness-config.md` for the user to apply). Concretely: predicates are named flags (`--missing`, `--matches <re>`) never script blocks; a regex is data, a script block is code. Output is bounded (result count and byte caps with an explicit truncation marker); every verb supports `--json` so the agent branches between turns, not in-shell; values are never evaluated — the variable store holds raw bytes, `gim-get` writes stdout only, and a consuming parameter takes `--<param>-from-var <name>` (the module reads its own store; agent-authored text never reaches a command line by interpolation; `--from-var` values are containment-revalidated at every use). Stored-variable names must match `^[A-Za-z0-9_-]{1,64}$` or are rejected outright. The implementation itself never uses `Invoke-Expression`, `iex`, `Add-Type`, `Start-Process`, or `&$var`.

**Status:** accepted

## Considered Options

- **Data-only verbs + named predicates (chosen)** — the load-bearing security invariant of the read tier; makes each verb allowlistable once, with no execution hole in the argument surface.
- **Guarded-mutation verb (`--require-message-matches <re>`)** — rejected; duplicates judgement the agent already makes; two-turn pattern (read, then act) is the replacement for check-then-act.
- **Format-string output templates (`gim-grep --format <template>`)** — rejected; a format string reaching an evaluator violates the data-only rule.
- **Class B "safe aliases" for mostly-data built-ins** — rejected; any verb wrapping a cmdlet that accepts a script block inherits its execution hole.
