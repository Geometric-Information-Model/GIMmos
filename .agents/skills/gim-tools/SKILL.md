---
name: gim-tools
description: Data-only gim-* command vocabulary for file/path work in the GIM workspace without script-block built-ins (ForEach-Object, Where-Object, Group-Object, Sort-Object, Select-Object) and without Invoke-Expression/Add-Type/Start-Process/&$var. Use when an agent needs to list contained paths, filter a path/record stream, grep files, inspect bytes, count/aggregate, diff path sets, read JSON documents, enumerate assembly types, store values between turns, mutate files, run confined builds, run a fleet preflight, create an explicit PR, or shape output lines. Keywords = gim-list, gim-where, gim-grep, gim-inspect, gim-head, gim-count, gim-diff, gim-report, gim-set, gim-get, gim-run, gim-probe, gim-preflight, gim-pr, gim-jira-search, gim-jira-create, gim-jira-transition, gim-wrapup-gather, gim-wrapup-respond, path containment, roots.json, deny-list replacement.
---

# gim-tools - GIM agent command vocabulary

Data-only toolkit for agent file/path work. Every verb takes data arguments
(paths, globs, regexes, flags); none takes code. Output is bounded, JSON is on
demand, and containment is enforced against `scripts/roots.json`.

## Availability and setup

gim-* commands are installed globally and auto-loaded by default for every
agent. Invoke them directly; consult this skill for command semantics when
needed. The allowlist still gates every gim-* verb (auto-load does NOT bypass
permissions).

**Do not import the module** (`Import-Module GIMTools...`); the verbs are
already available and a per-call import is a wasted step. The only exception:
if a `gim-*` command is not recognized in the current shell, run
`Import-Module GIMTools -DisableNameChecking` as a one-off fallback.

If auto-load is unavailable, one-time setup can be run with
`scripts/setup.ps1` (user-scope, no admin), then restart OpenCode / Cursor so
harness shells inherit the environment. See
`docs/guides/0002-setup-gim-tools-autoload.md`.

## Verb list (25 verbs, nine groups)

- `gim-list <path...> [--from-file <f>] [--glob <p>]... [--under <d>] [--depth N] [--dirs-only] [--allow-missing] [--print0] [--json]` - List contained paths; sole path entry point (globs, depth, --allow-missing).
- `gim-where [--exists|--missing|--empty|--non-empty|--matches <re>|--not-matches <re>|--larger-than <n>|--smaller-than <n>|--newer-than <iso>|--older-than <iso>|--ext <e>|--is-file|--is-dir] [--field <name>] [--json]` - Filter a pipeline stream; predicates hit the item (path) or a named record field (works downstream of gim-grep).
- `gim-report [--prefix <label>] [--prefix-each] [--sort <field>] [--format lines|table|json] [--only-failures] [--json]` - Shape/aggregate a stream; --sort replaces Sort-Object, --prefix replaces label loops.
- `gim-grep --pattern <re> [--capture N] [--stdin] [--fixed] [--ignore-case] [--files-only] [--files-without-match] [--count] [--context N] [--max-hits N] [--json]` - Pattern search over paths or a --stdin text stream; default output file:line: text.
- `gim-inspect [--eof-newline] [--tabs] [--line-endings] [--lines] [--bytes] [--encoding] [--bom] [--trailing-ws] [--max-line-len] [--binary] [--kind] [--mtime] [--hash] [--json]` - Byte-level file checks; missing paths report kind=missing, never error.
- `gim-head -n N [--skip N] [--last] [--json]` - First/last lines of files or a piped text stream; replaces Select-Object -First/-Skip/-Last.
- `gim-count [--by <field>] [--json]` - Count pipeline items; --by groups counts per record field; replaces Group-Object + count loops.
- `gim-diff --against <glob|path>... | --against-var <name> [--json]` - Two-set path comparison; state in only-left|only-right|both; no intermediate variables.
- `gim-json --path <query> [--keys] [--validate] [--json]` - Query JSON documents with a dotted/bracket path; replaces Select-Object -ExpandProperty.
- `gim-asm types --package <id>/<ver> | --path <dll> [--matches <re>] [--json]` - Enumerate exported type full names via PE metadata; never loads the assembly.
- `gim-set <name> [--clear <name>] [--clear-all] [--ttl <sec>] [--json]` - Tee: store the incoming stream in the variable store and pass it through.
- `gim-get <name> [--max-age <sec>] [--json]` - Read a stored variable (head-only pipeline source); TTL checked on read, never stale.
- `gim-fix <eof-newline|line-endings|trailing-ws> [--to lf|crlf] [--dry-run] [--json]` - Fix byte-level issues in place; pairs with gim-inspect flags; encoding-preserving.
- `gim-write --path <p> [--from-stdin] [--create-only] [--encoding utf8|utf8bom|utf16] [--out-dir <d>] [--path-from-var <name>] [--dry-run] [--json]` - Write a pipeline stream to a contained file; default UTF-8 without BOM; piped content is accepted without --from-stdin.
- `gim-apply --create-empty | --touch | --remove [--dry-run] [--json]` - Fixed action vocabulary (--create-empty | --touch | --remove) over a filtered pipeline set; targets may be in the current repo or the OS temp root, and that temp root must pass a safety floor (rooted local path, not a drive root or UNC, at least two segments deep, not a protected profile/system/repo ancestor).
- `gim-run <build|test|run|publish|ef|format> [--project <p>] [--project-from-var <name>] [--timeout <sec>] [--dry-run] [--json] [-- <args>...]` - Run a dotnet CLI verb on a contained project; env scrub, hard timeout, audit.
- `gim-probe --project <p> --route <r> [--method <m>] [--body-file <f>] [--timeout <sec>] [--dry-run] [--json]` - Launch an app on a random local port, poll readiness, issue one HTTP request; always teardown.
- `gim-preflight --repo <name|path> [--repo <name|path>]... [--branch <name>] [--skip-gh-auth] [--skip-jira-auth] [--dry-run] [--json]` - Fleet preflight: auth status, per-repo branch/clean, parallel fetch, conditional ff-only pull (default expected branch `dev`), and docs indexes. `--repo` takes an absolute path, a relative path, or a simple repo name (case-insensitive, under the parent of the current repo root). Untracked `.agent-scratch/` evidence is ignored by the dirty check. `--dry-run` does no fetch/pull; a failed remote/open-PR source returns `ok=false, next=null`.
- `gim-pr --repo <name|path> (--files-file [<path>] | --file <path> [--file <path>]...) --message <subject> --title <title> --target <branch> [--new-branch <branch>] [--pr-body <path>] [--draft] [--delete-pr-body] [--remote <name>] [--assignee <login>] [--dry-run] [--json]` - One explicit PR flow: stage the declared file list, guard the index, commit, push `-u`, create the PR, then run the stack lifecycle. Stack lifecycle mechanics (config keys, init/add/link/sync verification, result fields, manual fallback) are owned by `github-cli-workflow` §4 + `references/gim-stack-lifecycle.md`. `--files-file` with no value derives `.agent-scratch/pr-files/<feature>/<step>/files.txt`; errors name the failed precondition and never prescribe a fallback.
- `gim-jira-search [--keyword <kw>...] [--jql <jql>] [--project <key>] [--limit <n>] [--current-sprint] [--json]` - Read-only Jira search. No input queries the project issues (default `GIM`); keyword queries use `summary` OR `description` containment, one acli query per keyword; explicit JQL is used as given. Dedupe by issue key; return key, summary, status, and assignee display name only. `--current-sprint` resolves the D0 board's single active sprint; zero or many active sprints fail and require an explicit sprint filter in `--jql`. Default `--limit` is 50.
- `gim-jira-create --summary <s> [--project <key>] [--type <type>] [--assignee <@me|email>] [--unassigned] [--description-file <f>] [--sprint <id>] [--current-sprint] [--parent <key>] [--label <l>...] [--dry-run] [--json]` - Create a Jira work item from the observed acli template, then assign (default `@me`; `--unassigned` skips) and verify key/assignee. Payload assignee is null; description text becomes plain ADF paragraphs split on blank lines (markdown not interpreted); labels default null and a label-specific rejection retries once with null; sprint is the D0 bare integer under `sprint.field_id`. Returns key + URL, never email/account id. `--dry-run` prints payload and commands without calling acli.
- `gim-jira-transition --key <key> --to <status> [--project <key>] [--dry-run] [--json]` - Read the current status with the positional-key view form; a target equal to current is a no-op. Selects `routes[].sequence` from the D0 `jira-transitions.json` config and runs each step with `acli jira workitem transition --status` (the CLI has no `--to` flag). Stops at the first failure and returns the exact command and per-step results. `--dry-run` makes no acli call and prints the placeholder current status, direct target sequence, and command.
- `gim-wrapup-gather --repo <name|path> (--chain <pr> | --pr <n>... | --branch <branch>...) [--out <path>] [--parallel <n>] [--log-parallel <n>] [--json]` - Fetch-only PR gather for `gim-wrapup`: PowerShell shim + stdlib Node helper (Node >= 20). `--chain` is primary: walk the head/base stack around one PR, bottom -> top, stopping on trunk, ambiguity, cycle, or 50 PRs. `--pr`/`--branch` are explicit fallbacks; modes are mutually exclusive. Independent PR/log pools (defaults 4/2); threads map `reviews[].comments[]` with `replies[]`. Failing-check `gh run view` logs stream under `logs/` (50 MB/file, 200 MB total, gather-dir-relative `log_file`, `log_source` job/run). Writes schema v1 `gather.json` (1-space, nulls omitted, pass/skipping checks reduced to `{name,state}`) plus the authored `responses.json` shell (PRs with unresolved threads or top-level comments, plus per-PR comment strings). Never posts, resolves, pushes, or calls `gh api`.
- `gim-wrapup-respond --repo <name|path> --file <responses.json> --harness <name> [--dry-run] [--json]` - Consume `responses.json`: `gh-pr-review comments reply` per non-empty thread response, `gh-pr-review threads resolve` only when `resolve=true` and remotely open, and `gh pr comment` per non-empty per-PR comment string. Reads remote thread state per touched PR; writes sibling `<responses>.result.json` receipt (`posted_at`, `resolved_at`, `remote_state`, `last_error`); reruns skip completed entries and retry failures. Authored responses are never rewritten; `--dry-run` makes zero gh calls.
- `gim-verbs --json | --gen-refs [--out-dir <d>]` - Meta: emit the full verb vocabulary (--json) or write per-group reference docs (--gen-refs).

## Canonical pipelines

The 16 live conversions below are illustrative (paths are examples - adjust to
a real tree). Plan 0003 §Z1
(`docs/plans/agents/0003-AGENTS-gim-toolkit.plan.md`) is the historical source;
it still lists the removed commit conversion.

```
1.  git diff -- A.cs B.cs C.cs | gim-grep --stdin --pattern "^[+-](.*Identity comes from the JWT claim.*)$" --capture 1 | gim-count
2.  Write-Output "== .gitignore =="; Get-Content .gitignore; Write-Output "== .git/info/exclude =="; Get-Content .git/info/exclude
3.  gim-list --glob "GIM.WebAPI/**/Controllers/*.cs" | gim-grep --pattern "Route\(|AllowAnonymous|Authorize|EnableRateLimiting"
4.  Get-Content "GIMtransfer\FileManager\FileManager.cs" | gim-head -n 60 --skip 100; gim-list --glob "tests/**/*.cs" | gim-grep --pattern "gim|0003|plan"
5.  gim-list --glob "GIM.Common/Respositories/*.cs" --glob "GIM.Common/DBConnections/*.cs" --glob "GIM.WebAPI/**/Controllers/**/*.cs" --allow-missing | gim-grep --pattern "GetConnectionString|ConnectionStrings:" | gim-head -n 25
6.  gh pr list --repo Geometric-Information-Model/GIMfront --json number,title,state,isDraft,baseRefName,headRefName,author,createdAt,mergedAt,mergeable,mergeStateStatus
7.  gh pr diff 40 --repo Geometric-Information-Model/GIMfront | gim-write --path gimfront-pr40.diff; gim-list --glob ".agent-scratch/gimfront-pr*.diff" | gim-inspect --bytes
8.  git stash list (if index 0 matches) -> git stash drop "stash@{0}"  [read-only step: never drop]
9.  Test-NetConnection -ComputerName 3.121.190.108 -Port 3306 -InformationLevel Quiet  [optional: skip if network policy blocks]
10. gim-asm types --package microsoft.entityframeworkcore/7.0.20 --matches "Options|IDbContextFactory|DbContextOptionsBuilder"
11. gim-list --glob ".cursor/agents/**" | gim-diff --against ".opencode/agents/**"
12. gim-list --glob ".agents/**/*.md" | gim-grep --pattern "\]\(([^)#][^)]*)\)" --capture 1 | gim-where --missing | gim-report --prefix BROKEN_REF
13. gim-list --glob ".agents/skills/*/SKILL.md" | gim-grep --files-without-match --pattern "^name:" | gim-report --prefix MISSING_NAME
14. gim-list --glob "src/**/*.cs" | gim-inspect --hash | gim-set baseline; gim-run build --project <real project>; gim-list --glob "src/**/*.cs" | gim-inspect --hash | gim-diff --against-var baseline
15. gim-run build --project GIMtransfer/GIMtransfer.csproj + gim-probe --project <a runnable project> --route /health
16. gim-preflight --repo GIMmos --branch dev --json | gim-json --path repos[0].docs_indexes.plans.next
```

## Containment and output rules

- Roots come from `scripts/roots.json`: `.`, `../GIMmanager`, `../CoreWebApi`,
  `../GIMocctWrapper`, `../GIMzip`, `../GIMfront`; read-extra root
  `%USERPROFILE%\.nuget\packages` is for `gim-asm` only. Write/execution verbs
  confine to the current repo + `.agent-scratch/`, with two exceptions:
  `gim-wrapup-gather` writes only into the resolved `--repo` root (passed to
  `Resolve-GimWritePath -Roots @($repoRoot)`), which may be a sibling repo;
  `gim-apply` may also target the real OS temp root (`gim-fix`, `gim-write`,
  `gim-run`, and `gim-probe` stay repo-only), but that temp root must pass a
  safety floor: rooted local path (never UNC), not a drive root, at least two
  segments below the drive root, and not equal to or an ancestor of the user
  profile, local app data, app data, Windows directory, system root, or the
  repo root.
- Variable store: `%LOCALAPPDATA%\gim\vars\<sanitized-repo-root>\` (sanitized
  = repo root with `\`->`_`, drive colon stripped; `GIM_VARS_ROOT` env
  overrides, must be a rooted local path). One raw file per variable
  `<name>.t<ttl>.val`; TTL enforced on EVERY read incl. `--from-var`
  consumers (`gim-diff --against-var`) - expired = error, never stale. Stored
  values are data, never argv.
- Output caps: 500 items / 1 MiB. When a cap is hit, text output appends an
  explicit marker line `# gim: output truncated (items N, bytes M)` on
  stdout. Most verbs report only the item count (the byte field prints 0);
  gim-grep and gim-asm also compute and report the emitted byte count.
  `--print0` sends the marker to stderr (the NUL token stream stays clean);
  the JSON envelope carries `truncated: true` and a byte-capped JSON document
  reports the truncation on stderr so stdout stays one parseable document.
  There is no `--truncated` flag - the marker is automatic.
- `--json` emits `{ count, truncated, items }` for the pipeline verbs
  (gim-list, gim-where, gim-report, gim-grep, gim-inspect, gim-head,
  gim-count, gim-diff, gim-json, gim-asm, gim-fix, gim-apply, gim-write);
  gim-set/gim-get/gim-run/gim-probe/gim-preflight/gim-pr carry custom shapes
  (documented in the per-group `references/`).
- UTF-8 without BOM everywhere.

## Design invariants (non-negotiable, every verb)

1. Data-only arguments; predicates are named flags (a regex is data).
2. No `Invoke-Expression`, `iex`, `Add-Type`, `Start-Process`, or `&$var`.
   Process launch via `[System.Diagnostics.Process]` only.
3. Bounded output with an explicit `# gim: output truncated` marker (JSON
   envelope carries `truncated: true`).
4. `--json` on demand on every verb.
5. No shell interpolation; argv assembled via `Quote-Arg`.
6. Stored values are data, never argv (`gim-get` -> `--<param>-from-var`).
7. Variable names match `^[A-Za-z0-9_-]{1,64}$`.
8. Every mutating verb supports `--dry-run`.
9. Missing input paths -> `gim-inspect` reports `kind=missing`; other verbs
   error unless they document `--allow-missing`-style handling.
10. Module code stays PowerShell 5.1-compatible as a best-effort tier for
    data-only code, with one exception: containment-critical reparse-point
    resolution (`Get-GimRealPath`) needs the .NET Core `FileSystemInfo.Target`
    API → pwsh 7+ (the runtime standard; see `docs/guides/0001-setup-powershell-7.md`).
    UTF-8 without BOM.

## References

Per-group reference docs — the 9 group files below are generated by
`gim-verbs --gen-refs` (deterministic, do not hand-edit); the last entry,
`references/harness-config.md`, is hand-maintained and user-editable:

- [`references/spine.md`](references/spine.md) - gim-list, gim-where, gim-report
- [`references/search.md`](references/search.md) - gim-grep
- [`references/inspection.md`](references/inspection.md) - gim-inspect, gim-head, gim-count, gim-diff, gim-json
- [`references/capability.md`](references/capability.md) - gim-asm
- [`references/state.md`](references/state.md) - gim-set, gim-get
- [`references/mutating.md`](references/mutating.md) - gim-fix, gim-write, gim-apply
- [`references/execution.md`](references/execution.md) - gim-run, gim-probe
- [`references/fleet.md`](references/fleet.md) - gim-preflight, gim-pr, gim-jira-search, gim-jira-create, gim-jira-transition, gim-wrapup-gather, gim-wrapup-respond
- [`references/meta.md`](references/meta.md) - gim-verbs
- [`references/harness-config.md`](references/harness-config.md) - allow/deny/hooks checklist (hand-written; apply by hand per harness, this skill ships no config)
