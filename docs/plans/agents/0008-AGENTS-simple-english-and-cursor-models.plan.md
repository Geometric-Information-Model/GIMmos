# Simple English and Cursor subagent models

Fleet workflow only. Two changes: pin the seven Cursor Task subagents to one model, and add a simple-English skill that the orchestrator must follow when it writes a human doc. No product code. No hook changes. No ADR.

## Decisions

- **Cursor models.** These seven files change their `model:` line to `deepseek-v41-flash-1017-high`:
  - `.cursor/agents/gim-builder.md` (now `gemini-3.8-flash[]`)
  - `.cursor/agents/gim-explorer.md` (now `gemini-3.8-flash[]`)
  - `.cursor/agents/gim-tester.md` (now `gemini-3.8-flash[]`)
  - `.cursor/agents/gim-tracker.md` (now `gemini-3.8-flash[]`)
  - `.cursor/agents/gim-critic.md` (now `grok-4.7[context=256k,reasoning_effort=high,fast=false]`)
  - `.cursor/agents/gim-debugger.md` (now `grok-4.7[context=256k,reasoning_effort=high,fast=false]`)
  - `.cursor/agents/gim-version-controller.md` (now `gpt-5.6-luna[context=272k,reasoning=high,fast=false]`)
- **Orchestrator model.** `.cursor/agents/gim-orchestrator.md` stays `grok-4.7[context=256k,reasoning_effort=high,fast=false]`. It is the main-agent pin, not a Task subagent.
- **OpenCode models.** Every `.opencode/agents/*.md` model line stays `deepseek/deepseek-flash`. Frontmatter may differ per harness. This change does not sync model pins.
- **Human doc.** An ADR, an RFC, a guide, or README or index prose written for a person. Apply the writing rule when one of these is created or edited. Do not edit an existing human doc only to apply the rule.
- **Not a human doc.** A plan, a skill, an agent prompt, code, chat, and a caveman report. Plans stay fat. They hold the detail an agent needs to execute the work.
- **Skill.** New model-invoked skill `.agents/skills/gim-simple-english/SKILL.md`. Do not set `disable-model-invocation`. Do not add it to `.agents/agent-preload.json`. The description is the trigger. The skill holds the grammar rules and nothing else.
- **Process home.** `gim-orchestrate` non-negotiable 2 is the only place that states when to read the skill, when to run unslop, how thin an ADR is, and how `## Considered Options` looks. Both orchestrator agent files add one pointer sentence. They do not copy the rules.
- **Unslop.** After a human-doc draft follows `gim-simple-english`, run unslop `rewrite` with the `crisp` preset. Do not edit `.agents/skills/unslop/`.
- **Considered Options.** The heading stays `## Considered Options`. It stays optional. Each rejected option is one bullet: a bold title, one or two sentences only when the title is unclear, then why it was refused. The chosen decision stays in the opening.
- **Out of scope.** Do not edit `.agents/skills/domain-modeling/` (third-party). Do not edit `.agents/skills/agentic-planning/` (it governs plan shape only). Do not edit `.cursor/hooks/`, `.cursor/hooks.json`, or any OpenCode plugin. Do not port `gim-preload.ps1`. The user withdrew the hook port. Do not write an ADR. The rule is easy to edit, so it fails the "hard to reverse" test. Do not create a Jira ticket. Search returned no hit, and this is `.agents` / `.cursor` meta.
- **Numbering.** This file is agent-plan `0008`. Product `docs_indexes` next numbers (plan 11, ADR 5, RFC 9, guide 9) are not used.
- **Delivery.** One PR per step, opened with `gim-pr`. The user withdrew the one-PR override. The branch base is `chore/simple-english`, cut from `dev`, because this is a chore and not a `feature/*` branch. Step 2 branch is `simple-english/2-cursor-model-pins`. Step 3 branch is `simple-english/3-simple-english-skill`. Each step's files are still uncommitted when `gim-pr` starts. `gim-pr` commits them, pushes, and opens that step's PR. `gim-pr` stores the subject only, so each step's `Trigger:` line goes in that step's PR body. Do not open a PR with `gh pr create`. No ticket key in the branch name.

## Execution mode

- [x] Verify per step
- [x] Commit per step
- [x] PR per step
- [x] Critic per step

Prefs set these four. The one-PR override is withdrawn. Step order is critic, then verify with the user, then plan status update, then `gim-pr`. `gim-pr` commits, pushes, and opens the step PR. The commit subject is `<stepid>: gim-improve(<target>): <summary>`. The `Trigger:` line is in the PR body, because `gim-pr` does not take a commit body. No harness footer on the commit subject or the PR title.

Branch: cut `chore/simple-english` from `dev`. Step PRs chain leaf-safe. Step 2 is cut from `chore/simple-english`. Step 3 is cut from `simple-english/2-cursor-model-pins`.

PR titles:

- Step 1: `[Docs] Simple English - add the fleet plan`. Target `dev`. No `--new-branch`.
- Step 2: `[Docs] Simple English - pin Cursor Task subagents to deepseek flash`. Target `chore/simple-english`. `--new-branch simple-english/2-cursor-model-pins`.
- Step 3: `[Docs] Simple English - add the simple English skill`. Target `simple-english/2-cursor-model-pins`. `--new-branch simple-english/3-simple-english-skill`.

## Failure scenarios

- Agent moves to step N+1 while a check in step N failed.
- Agent moves to step N+1 without a fresh gim-critic pass on step N.
- Agent moves to step N+1 without an explicit user affirmative (`looks good` or `verified`). Silence, "ok", a question, or a discussion point is not an affirmative.
- Agent commits or opens a PR for step N before that affirmative.
- Agent moves to step N+1 without a PR for step N.
- Agent opens a PR with `gh pr create` instead of `gim-pr`.
- Agent runs `gim-pr` after that step's files are already committed.
- Agent commits step N without the step id prefix, or omits the `Trigger:` line from the PR body.
- Agent commits on `dev`, `main`, or `release/*`.
- Agent changes the Cursor orchestrator model, or any OpenCode model line.
- Agent edits `domain-modeling`, `agentic-planning`, `unslop`, hooks, or hook config.
- Agent writes an ADR, creates a Jira ticket, or preloads `gim-simple-english` in `.agents/agent-preload.json`.
- Agent copies the grammar rules into the orchestrator agent files instead of pointing at `gim-orchestrate`.
- Agent edits a sibling repo.

## Branch and preconditions

- Preflight on 2026-11-15: `dev` clean, `origin/dev` fetched and pulled, `ok: true`. This plan file is the only intended dirty path before step 1.
- Architecture: `windows`. PowerShell. Do not use Linux `&&` or `||`. Do not prefix git with `git -C`.
- No build and no test project. Each step's check is a file read.
- Step 1 has no builder. The orchestrator writes this file. Steps 2 and 3 go to gim-builder.

## Steps

### Step 1 — publish this plan [critic, verify, commit, PR]

Status: completed.

- **Files / symbols**: `docs/plans/agents/0008-AGENTS-simple-english-and-cursor-models.plan.md` (this file).
- **Change**: The file is already written. Do not edit it in this step except to set this step's status to completed after the verify gate, and include that status edit in the commit.
- **Why**: The first PR is `chore/simple-english` → `dev` and carries this plan.
- **Owner**: no builder. Fresh gim-critic reviews this file. Then the user verifies. Then set this step's status to completed. Then gim-version-controller runs `gim-pr`. The plan file must still be uncommitted when `gim-pr` starts. Evidence files: `.agent-scratch/pr-files/simple-english/chore/files.txt` and `body.md` beside it. The body contains the `Trigger:` line below. Message: `1: gim-improve(fleet): add the simple English and Cursor model plan`. Title: `[Docs] Simple English - add the fleet plan`. Target: `dev`. Do not pass `--new-branch`.
- **Validate**: The file is the only change. `git status` shows this path and no other intended edit.
- **Commit message**: `1: gim-improve(fleet): add the simple English and Cursor model plan`
- **Trigger**: `update the models of the subagents (in cursor only) to use deepseek-v41-flash-1017-high` and `update the rules around ADR/RFC/human-doc creation and language` using loose Simplified Technical English, with unslop on human docs, thin ADRs, and fat plans. Hooks are out: `we won't move any hooks`.

### Step 2 — pin Cursor Task subagent models [critic, verify, commit, PR]

Status: completed.

- **Files / symbols**: the seven `.cursor/agents/gim-*.md` files listed under Decisions. Change only the `model:` frontmatter line.
- **Change**: Set each of the seven lines to `model: deepseek-v41-flash-1017-high`. Do not change any other line in those files. Do not change `.cursor/agents/gim-orchestrator.md`. Do not change any file under `.opencode/agents/`.
- **Why**: The user asked for Cursor subagents only. The orchestrator stays on grok. OpenCode already has its own model pin.
- **Validate**: Read the `model:` line of all eight Cursor agent files and all eight OpenCode agent files. The seven Task agents show `deepseek-v41-flash-1017-high`. The Cursor orchestrator still shows `grok-4.7[context=256k,reasoning_effort=high,fast=false]`. Every OpenCode agent still shows `deepseek/deepseek-flash`.
- **Commit message**: `2: gim-improve(cursor-agents): pin Task subagents to deepseek-v41-flash`
- **Trigger**: `all of the subagents, but not the orchestrator` and `update the models of the subagents (in cursor only) to use deepseek-v41-flash-1017-high`.
- **PR**: after verify, gim-version-controller runs `gim-pr`. Evidence files: `.agent-scratch/pr-files/simple-english/2-cursor-model-pins/files.txt` and `body.md`. The body contains the `Trigger:` line. Message: the commit message above. Title: `[Docs] Simple English - pin Cursor Task subagents to deepseek flash`. Target: `chore/simple-english`. `--new-branch simple-english/2-cursor-model-pins`. The seven model files must still be uncommitted when `gim-pr` starts.

### Step 3 — add gim-simple-english and point the orchestrator at it [critic, verify, commit, PR]

Status: pending.

- **Files / symbols**:
  - Create `.agents/skills/gim-simple-english/SKILL.md`.
  - Edit `.agents/skills/gim-orchestrate/SKILL.md` non-negotiable 2, and add one sentence to product-loop step 4.
  - Edit `.cursor/agents/gim-orchestrator.md` non-negotiable 2, and add one sentence to the product-loop mirror step 4.
  - Edit `.opencode/agents/gim-orchestrator.md` the same way. Do not change its `model:` line.
- **Change**: Write the skill below as the file contents. Then add the process sentence to the three protocol files. Do not copy the grammar list into those files.
- **Why**: The grammar list is long, so it lives in a skill the description can trigger. The orchestrator is the writer, so the duty to use the skill lives on non-negotiable 2. One source of truth for the process. One source of truth for the grammar.
- **Skill file contents**:

````markdown
---
name: gim-simple-english
description: >
  Simple English for human-facing docs: common words, short sentences, active voice.
  Use when writing or editing an ADR, RFC, guide, or README, when the user asks
  for simple English, or when gim-orchestrate writes a human doc.
---

# Simple English

Write a human doc in simple English. A human doc is an ADR, an RFC, a guide, or README or index prose for a person.

## When to use

Use this skill when you write or edit a human doc, or when the user asks for simple English.

`gim-orchestrate` non-negotiable 2 says when to run this skill and when to run unslop. Do not restate that process here.

## When not to use

Do not use this skill for a plan, a skill, an agent prompt, code, chat, or a caveman report.

Do not edit a human doc only to apply these rules. Apply the rules when you already have another reason to edit that doc.

## Text types

A procedure tells a person to do something. A guide can be a procedure. A description tells a person what is true. An ADR and an RFC are descriptions.

## Rules

1. Use a common English word. If a common word and a longer synonym both fit, use the common word. Keep a technical name when it is the name of a thing in this system.
2. Make each instruction clear and specific.
3. Do not write a noun phrase with more than three words.
4. Use a verb only in these forms: infinitive, imperative, simple present, simple past, simple future, or past participle as an adjective.
5. Do not use an auxiliary verb to build a complex verb form.
6. Use the "-ing" form of a verb only as a technical noun, or as a modifier in a technical noun.
7. Use the active voice. In a description, use the passive voice only when the actor is unknown.
8. Write a short sentence. Use at most 20 words in a procedure. Use at most 25 words in a description.
9. Do not drop a subject, a verb, or an article to make a sentence shorter.
10. Use a vertical list when the text is complex.
11. Write one instruction in each sentence.
12. Write one topic in each paragraph.
13. Do not put more than six sentences in a paragraph.
14. Start a safety instruction with a clear command or a clear condition.

## Done when

Every rule above holds for the human doc you are writing. A procedure sentence has at most 20 words. A description sentence has at most 25 words. A technical name is still the name used in this system.
````

- **Orchestrator skill, non-negotiable 2.** Replace the current item 2 with this text. The fence is the whole item, including the leading `2.`.

~~~text
2. **You write** plans, ADRs, RFCs, and top-level docs yourself (not via builder). A human doc is an ADR, an RFC, a guide, or README or index prose for a person. When you write or edit a human doc, read and follow `gim-simple-english`, then run unslop `rewrite` with the `crisp` preset. Keep the ADR thin. Keep the plan fat for the agent who will execute it. `## Considered Options` is optional. Each rejected option is one bullet: a bold title, one or two sentences only when the title is unclear, then why you refused it. The chosen decision stays in the opening. Do not edit an existing human doc only to apply this rule. A plan, a skill, a prompt, code, chat, and a caveman report are not human docs.
~~~

- **Orchestrator skill, product-loop step 4.** After the existing step 4 sentence, add: `Write each human doc per non-negotiable 2.`
- **Both agent files, non-negotiable 2.** Replace:

  `2. **You write** plans/ADRs/RFCs/top-level docs (not builder).`

  with:

  `2. **You write** plans/ADRs/RFCs/top-level docs (not builder). When you write or edit a human doc, follow the human-doc rule in `.agents/skills/gim-orchestrate/SKILL.md` (non-negotiable 2).`

- **Both agent files, product-loop mirror step 4.** After the existing step 4 sentence, add: `Write each human doc per non-negotiable 2.`
- **Validate**:
  - The skill name is `gim-simple-english`. The description matches the block above. `disable-model-invocation` is absent.
  - `gim-orchestrate` non-negotiable 2 names `gim-simple-english`, unslop `rewrite`, the `crisp` preset, thin ADRs, fat plans, and `## Considered Options`.
  - Both agent files point at that rule and do not list the 14 grammar rules.
  - `.opencode/agents/gim-orchestrator.md` still has `model: deepseek/deepseek-flash`.
  - `.cursor/agents/gim-orchestrator.md` still has `model: grok-4.7[context=256k,reasoning_effort=high,fast=false]`.
  - `domain-modeling`, `agentic-planning`, `unslop`, and `.cursor/hooks/` have no edits.
  - `.agents/agent-preload.json` has no new entry.
- **PR**: after verify, gim-version-controller runs `gim-pr`. Evidence files: `.agent-scratch/pr-files/simple-english/3-simple-english-skill/files.txt` and `body.md`. The body includes this `Trigger:` line: `We'll call the skill gim-simple-english` and `these should go to agentic planning OR orchestration skill and agent`, resolved to the orchestration skill and both orchestrator agents. Vocabulary rule: `use simple english words and prefer them over complex synonyms`, tightened to the common-word rule in the skill. Message: `3: gim-improve(gim-simple-english): add the skill and orchestrator pointers`. Title: `[Docs] Simple English - add the simple English skill`. Target: `simple-english/2-cursor-model-pins`. `--new-branch simple-english/3-simple-english-skill`. The step 3 files must still be uncommitted when `gim-pr` starts.

## Review

After execution, run `/review-with-critic granular` once more after the last per-step critic pass, with the full modified file list and `Steps implemented: 1, 2, 3`. This wrap-up pass weights findings toward cross-step coherence.

Per-step critic passes run between steps. Each pass gets that step's files and `Steps implemented: <stepId>`.
