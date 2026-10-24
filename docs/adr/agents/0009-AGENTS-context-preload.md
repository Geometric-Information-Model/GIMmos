# Per-agent context preload

## Context

Agents manually read their skills and preferences at the start of every session and again on resume. Loading was non-deterministic and repeated work. The intent was to place the same material into each agent's context automatically from one shared map of agents to preference files and skills.

## Decision

Use one shared preload map for both harnesses. Every agent receives the preference file and its role skills.

OpenCode injects the mapped material into the agent's system context on every user message, with a fallback for synthetic turns that do not pass through the normal message path.

Cursor uses two mechanisms. Session start adds the main orchestrator preload to the initial context; the default Cursor chat is implicitly the orchestrator, so no agent type is needed. Before a Task subagent is spawned, a pre-tool hook rewrites the task prompt to prepend that subagent's preload. Cursor's subagent-start hook is only an allow-or-deny gate and is not used for injection.

Both hooks preserve unrelated context, avoid duplicate injection, fail open with valid output, and write a diagnostic log. Idempotence matches a complete preload block anchored at the prompt start, not a raw opening tag. On Windows, Cursor decodes hook stdout with the console code page, so the Cursor hook emits ASCII-only JSON and escapes non-ASCII code units. A live probe on Cursor 3.17.8 confirmed the Task path: the subagent type field, the prompt rewrite, and all eight fleet agents receiving their mapped block with no skill or read call. Session-start delivery is a separate pending probe.

**Status:** accepted; the Cursor Task path is live-verified on Cursor 3.17.8. Session-start delivery remains pending. The subagent bodies rely on the Task preload; the orchestrator keeps a prefs read fallback until the session-start probe passes.

## Considered Options

- Shared preload map with harness-specific injection (chosen): one source of truth, two thin adapters.
- Inject through Cursor's subagent-start hook: rejected; its output is only allow or deny, and an invalid response blocks the subagent.
- Use Cursor session start only: rejected; it has no subagent type, so it cannot choose role skills.
- Generate agent bodies with embedded skills: rejected; duplication and drift.
- Keep manual reads: rejected; non-deterministic and repeated.
- One map per harness: rejected; the maps drift.
