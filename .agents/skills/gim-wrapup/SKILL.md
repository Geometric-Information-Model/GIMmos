---
name: gim-wrapup
description: >
  Post-plan wrap-up. Use when gim-orchestrate reaches step 16 or the user invokes
  `/gim-wrapup`. Always runs at the end of the plan, after the PR chain.
  Appends to the existing plan file and never creates a new plan file. A manual
  run is only by explicit user request. Gathers CI and PR comments, runs the end
  critic when prefs allow, and waits for an explicit Go before follow-up work.
---

# GIM Wrapup

Wrapup always runs at the **end of the plan**, as `gim-orchestrate` step 16.
It always appends to the existing plan file and never creates a new plan file;
if no plan file exists, stop and ask the user which plan to use. A manual
earlier run is only by explicit user request; it gathers CI and comments but
does not append plan content, run the end critic, or start follow-up work.

`gim-orchestrate` step 16 reads this skill and runs it. The user may also invoke
`/gim-wrapup` manually on an open chain, but only as the explicit exception
above. A manual run in the middle of the chain, before the end, gathers CI and
comments and defers the wrap-up critic. A manual run after the wrap-up critic
has already run gathers CI and comments and skips the critic.

The orchestrator compiles the roadmap and edits the plan. Fetching CI and
comments, committing, pushing, merging, and reply-and-resolve belong to
`gim-version-controller`. The end critic is a fresh `gim-critic`. No follow-up
edit, commit, push, merge, reply, or resolve happens before **Go**.

## Chain

The chain is the open PRs for this plan's step branches, bottom to tip. A
direct invocation with no plan uses the open PR on the current branch and the
open PRs stacked on it; that is a gather-only exception and does not supply a
plan file for step 7. When no open PR chain exists, skip the GitHub gather
and the reply pass, say so, and still run the critic gate and the discussion.

## Steps

1. **CI.** Dispatch `gim-version-controller` to run `gim-wrapup-gather
   --chain <pr>` for the chain — one PR identifies it reproducibly. Use
   `--pr`/`--branch` only when chain discovery fails or the set is known.
   Failing checks (fail/cancel, including cancelled/timed-out states) get
   `gh run view` logs under `<gather-dir>/logs/` by default. Done when each
   check's name, status, and conclusion are back and the normalized gather
   JSON is written. A gather is fetch-only.
2. **Comments.** The same dispatch returns every top-level comment and every
   inline thread from `reviews[].comments[]`, including its `replies[]`
   (`author`, `body`, `created_at`). Done when `comments_verbatim` is back.
   Comment bodies stay verbatim. The gather is fetch-only: it never posts or
   resolves.
3. **Critic.** Spawn a fresh `gim-critic` for the whole built plan
   (`Steps implemented:` every built id) and weight findings toward
   cross-step coherence when `critic_per_step` is `granular` or `end`, this
   run is the end of the chain, and the wrap-up critic has not already run.
   When `critic_per_step` is `off`, skip unless the user asked for a critic.
   A manual run after the wrap-up critic has already run skips the critic.
   A manual run in the middle of the chain, before the end, defers the
   critic; the end run still performs it. Done when the critic has returned,
   or when the skip or deferral is recorded.
4. **Roadmap.** After every agent from that launch has returned, publish one
   roadmap in chat. Quote the comment list verbatim, then give each CI item,
   comment, and critic finding one recommended action. Done when the user has
   that roadmap and no mutation has started. Wait for ALL the sub-agents in
   flight to return before publishing the action-items list/roadmap. Any and
   all output between sub-agents midflight is lost and the user won't see it.
   A short sentence in the form of a mental note is acceptable as output here
   (e.g. "critic returned, no criticals, one high. Waiting for version 
   controller..", "Version controller returned, no CI failures. Waiting
   for critic..")
5. **Discussion.** Take questions, tweaks, and pushback. Update the
   recommendation when the user changes it. Agreement, "sounds good", and a
   reached understanding stay in discussion. Done when the roadmap matches
   the latest user tweak, or the user has not changed it.
6. **Go.** Start work only on an instructive command to start: "you can
   start", "you can go", "lets start", "let's start", or a direct order such
   as "start" or "do it". A Go in the same message as a tweak includes that
   tweak. A start-order spoken before the roadmap exists does not count;
   publish the roadmap, then wait. If the phrase is ambiguous, say work has
   not started and stay in discussion. Done when a Go was given after the
   roadmap, or the skill is still waiting. Starting without an explicit "go"
   instruction is a CRITICAL FAILURE.
7. **Plan.** Append the agreed steps to the existing plan file. The default
   and automatic placement is the end of the plan; this step never creates a
   new plan file. If no plan file exists, stop and ask the user which plan to
   use. Shape the steps so the PR split below is visible in the step list. Do
   not open a second plan. Do not create a ticket unless the user asks. A
   manual earlier run (explicit user request only) does not append plan
   content. If the roadmap has no code or stack work, append nothing. Done
   when the plan matches the agreed work, or there was nothing to append.
8. **Build.** Dispatch product-loop steps 7–15 for each new step, using the
   user's prefs. A manual earlier run (explicit user request only) does not
   start follow-up work. Done when each new step has finished its gates,
   or there were no new steps. If the verify gate is on and you skip the
   user's verification, that counts as a CRITICAL FAILURE: the verify gate is
   the most important gate in the run.
9. **Close the threads.** After the agreed work is committed and any new PRs
   are open, **the orchestrator authors `responses.json`** — the thread
   responses, `resolve` flags, and per-PR comment strings — exactly as it owns
   the PR body. Then dispatch `gim-version-controller` with the Go quoted in the
   brief. It runs `gim-wrapup-respond --repo <name|path> --file
   <responses.json> --harness <name>` (dry-run first, then live): replies,
   resolves only the threads whose `resolve` is true and that are remotely
   open, and posts each non-empty per-PR comment string in order. The
   version-controller applies the authored file verbatim; it never composes or
   alters replies. Empty responses are skipped; untouched threads stay as they
   were. Per-entry failures keep the response, write `last_error`, and return
   `ok=false` with counts; it never resolves a failed reply. Done when every
   thread the responses file names is replied/resolved per its `resolve` flag
   and every non-empty per-PR comment string is posted.

## Responses shell

`gim-wrapup-gather` writes `responses.json` beside `gather.json`. The
**orchestrator authors it** — thread responses, `resolve` flags, and per-PR
comment strings — exactly as it owns the PR body. The version-controller only
applies it; the responder never rewrites it.

```json
{ "repo": "owner/name", "generated_at": "...",
  "prs": [ { "pr": 118,
    "threads": [ { "thread_id": "PRRT_...", "resolve": true, "response": "" } ],
    "comments": [ "free-form comment text" ] } ] }
```

`gather.json` is UTF-8 without BOM with a 1-space indent; null-valued
properties are omitted; pass/skipping checks reduce to `{name,state}` while
failing, cancelled, pending, and log rows stay full. Only PRs with at least
one unresolved thread or top-level comment appear; only unresolved threads are listed; `resolve`
defaults to `true`; `response` starts empty. Each
`threads[]` entry is id-keyed by `thread_id`. `comments` is a free-form string
array per PR, posted in order; empty/whitespace strings are skipped. On a
re-gather, authored `response`/`resolve` and `comments` are preserved for
thread ids still present, new unresolved threads are added, and resolved
threads are dropped from a PR that is present. A previous PR entry absent from
the new gather is retained untouched (with its authored comments and
responses), so a partial gather never loses it. The responder writes
`<responses>.result.json` with per-entry `posted_at`,
`resolved_at`, `remote_state`, and `last_error`; receipt entries are idempotent.

Steps 1 and 2 always launch together. Launch the critic in that same round
only when it is not skipped or deferred. Hold findings until that round is
complete, then publish the roadmap once.

## Comment kinds

- **Suggestion.** Recommend applying it when it is correct, with the reason.
  Recommend declining it when it is wrong, with the reason. The discussion
  can overturn either call.
- **Question.** Recommend a reply. The reply is posted in step 9.
- **Suggestion-like question.** Recommend one course in the roadmap. Leave
  the choice in the discussion.
- **Statement.** Recommend acknowledge, a short reply, or a follow-up. Say
  which and why.

Every comment in the verbatim list gets a reply in step 9, including a
decline.

## CI kinds

- **Process check** (ADR CI and any non-test check) failing on PR #N: PR #N
  fixes it. Then merge that result up the stack through #N+1 to the tip,
  with merge commits. This is the exception that writes to existing PRs.
- **Test check** failing on step N: report a flow defect. Tests for that step
  were not run locally. Leave the fix for discussion. It does not use the
  process-check merge-up. Transient CI failures can also be the cause, though
  rare.
- **Pending check:** report it as pending. It is not a fix item.
- **Stack conflicts with `dev`:** merge `dev` up the stack from the bottom,
  with merge commits. No rebase, no force-push.

Do process-check fixes and the `dev` merge-up before any new PR, so new work
stacks on the updated tip. All git work (pull/merge/etc..) is performed by 
the version controller. Do NOT perform the work directly.

## PR split

When prefs open PRs, follow this split for the new work:

- One new PR when the agreed work is one area and the forecast is within
  about 2k lines.
- Several new PRs when the forecast is over about 2k lines, or the work
  touches several areas of concern (E.g. Extensive data/migration work,
  as well as extensive controller refactor). One PR per area or per 
  chunk per repo.

New follow-up work always opens new PRs. Do not put that work on the old
step PRs. Process-check fixes and the `dev` merge-up stay on the existing
chain. When prefs are not opening PRs, do the local steps and skip step 9
until a chain exists.

## Version-controller brief

Gather brief: fetch only. Return check rows and `comments_verbatim` (author,
PR, thread id or top-level id, path and line when inline, body unchanged).

Reply brief: hand over the authored `responses.json` (and its receipt path). The orchestrator owns the replies; the version-controller applies them verbatim and must not compose or edit them. Do not restate replies in chat.
