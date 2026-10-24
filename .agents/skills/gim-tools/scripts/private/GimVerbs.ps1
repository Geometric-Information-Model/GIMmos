# GimVerbs.ps1 - the shared verb-definition table (A8): one source of truth
# for the full gim-* vocabulary. gim-verbs --json emits it; gim-verbs
# --gen-refs renders it into references/<group>.md; the SKILL.md one-liners
# mirror it. Deterministic: entries are ordered by group (spine, search,
# inspection, capability, state, mutating, execution, fleet, meta) then by plan
# order, and the reference renderer produces byte-identical output on every
# run (no timestamps, no machine paths).

$script:GimVerbGroups = @('spine', 'search', 'inspection', 'capability', 'state', 'mutating', 'execution', 'fleet', 'meta')

$script:GimVerbDefinitions = @(
    [pscustomobject]@{
        name        = 'gim-list'
        group       = 'spine'
        signature   = 'gim-list <path...> [--from-file <f>] [--glob <p>]... [--under <d>] [--depth N] [--dirs-only] [--allow-missing] [--print0] [--json]'
        description = 'List contained paths; sole path entry point (globs, depth, --allow-missing).'
        params      = @(
            [pscustomobject]@{ flag = '<path...>';        detail = 'Literal paths; each is real-path resolved and asserted contained in roots.json. A missing path errors unless --allow-missing.' }
            [pscustomobject]@{ flag = '--from-file <f>';  detail = 'Read literal paths from a contained file, one per line.' }
            [pscustomobject]@{ flag = '--glob <p>';       detail = 'Glob relative to each configured root; ** crosses directory separators, * does not. Repeatable. A glob that matches nothing errors unless --allow-missing.' }
            [pscustomobject]@{ flag = '--under <d>';      detail = 'Re-root the listing at a contained directory; emits items strictly below it.' }
            [pscustomobject]@{ flag = '--depth N';        detail = 'Recursion depth; 1 = immediate children (relative to the glob anchor directory for globs).' }
            [pscustomobject]@{ flag = '--dirs-only';      detail = 'Keep only directory paths.' }
            [pscustomobject]@{ flag = '--allow-missing';  detail = 'Forgive missing literal paths and empty globs (containment is never forgiven).' }
            [pscustomobject]@{ flag = '--print0';         detail = 'NUL-separated output; the truncation marker goes to stderr so the NUL token stream stays clean.' }
            [pscustomobject]@{ flag = '--json';           detail = 'Emit { count, truncated, items }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-where'
        group       = 'spine'
        signature   = 'gim-where [--exists|--missing|--empty|--non-empty|--matches <re>|--not-matches <re>|--larger-than <n>|--smaller-than <n>|--newer-than <iso>|--older-than <iso>|--ext <e>|--is-file|--is-dir] [--field <name>] [--json]'
        description = 'Filter a pipeline stream; predicates hit the item (path) or a named record field (works downstream of gim-grep).'
        params      = @(
            [pscustomobject]@{ flag = '--exists';            detail = 'Keep items whose path exists.' }
            [pscustomobject]@{ flag = '--missing';           detail = 'Keep items whose path does not exist.' }
            [pscustomobject]@{ flag = '--empty';             detail = 'Keep empty files/directories (or empty strings).' }
            [pscustomobject]@{ flag = '--non-empty';         detail = 'Keep non-empty files/directories (or non-empty strings).' }
            [pscustomobject]@{ flag = '--matches <re>';      detail = 'Keep items whose value matches the regex.' }
            [pscustomobject]@{ flag = '--not-matches <re>';  detail = 'Keep items whose value does not match the regex.' }
            [pscustomobject]@{ flag = '--larger-than <n>';   detail = 'Keep existing files larger than n bytes (or a numeric field value with --field).' }
            [pscustomobject]@{ flag = '--smaller-than <n>';  detail = 'Keep existing files smaller than n bytes (or a numeric field value with --field).' }
            [pscustomobject]@{ flag = '--newer-than <iso>';  detail = 'Keep files last written after the timestamp.' }
            [pscustomobject]@{ flag = '--older-than <iso>';  detail = 'Keep files last written before the timestamp.' }
            [pscustomobject]@{ flag = '--ext <e>';           detail = 'Keep files with the extension (with or without the leading dot).' }
            [pscustomobject]@{ flag = '--is-file';           detail = 'Keep items that are files.' }
            [pscustomobject]@{ flag = '--is-dir';            detail = 'Keep items that are directories.' }
            [pscustomobject]@{ flag = '--field <name>';      detail = 'Test a named record field instead of the item (e.g. gim-grep --json downstream).' }
            [pscustomobject]@{ flag = '--json';              detail = 'Emit { count, truncated, items }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-report'
        group       = 'spine'
        signature   = 'gim-report [--prefix <label>] [--prefix-each] [--sort <field>] [--format lines|table|json] [--only-failures] [--json]'
        description = 'Shape/aggregate a stream; --sort replaces Sort-Object, --prefix replaces label loops.'
        params      = @(
            [pscustomobject]@{ flag = '--prefix <label>';       detail = "Prefix every emitted line with '<label>: ' (lines format; table output needs --prefix-each to carry the label)." }
            [pscustomobject]@{ flag = '--prefix-each';          detail = 'Explicit per-item prefix form; additionally carries the label into table/json structured output.' }
            [pscustomobject]@{ flag = '--sort <field>';         detail = 'Sort strings lexicographically or records by property; bare --sort sorts strings (needs a field for records).' }
            [pscustomobject]@{ flag = '--format lines|table|json'; detail = 'Output shape (default lines).' }
            [pscustomobject]@{ flag = '--only-failures';        detail = 'Keep failing strings/records (fail|error|warn, failed=true, or status not ok/pass/success).' }
            [pscustomobject]@{ flag = '--json';                 detail = 'Same as --format json.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-grep'
        group       = 'search'
        signature   = 'gim-grep --pattern <re> [--capture N] [--stdin] [--fixed] [--ignore-case] [--files-only] [--files-without-match] [--count] [--context N] [--max-hits N] [--json]'
        description = 'Pattern search over paths or a --stdin text stream; default output file:line: text.'
        params      = @(
            [pscustomobject]@{ flag = '--pattern <re>';            detail = 'Required. Regex to match (--fixed escapes it).' }
            [pscustomobject]@{ flag = '--capture N';               detail = 'Emit capture group N instead of the line (feeds path predicates downstream).' }
            [pscustomobject]@{ flag = '--stdin';                   detail = 'Read a piped text stream instead of paths.' }
            [pscustomobject]@{ flag = '--fixed';                   detail = 'Treat the pattern as literal text.' }
            [pscustomobject]@{ flag = '--ignore-case';             detail = 'Match case-insensitively.' }
            [pscustomobject]@{ flag = '--files-only';              detail = 'Emit matching file paths only (no lines).' }
            [pscustomobject]@{ flag = '--files-without-match';     detail = 'Emit files that lack the pattern.' }
            [pscustomobject]@{ flag = '--count';                   detail = 'Emit per-file match counts.' }
            [pscustomobject]@{ flag = '--context N';               detail = 'Include N lines around each match (context records, never matches).' }
            [pscustomobject]@{ flag = '--max-hits N';              detail = 'Cap matching records (default 500).' }
            [pscustomobject]@{ flag = '--json';                    detail = 'Emit { count, truncated, items }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-inspect'
        group       = 'inspection'
        signature   = 'gim-inspect [--eof-newline] [--tabs] [--line-endings] [--lines] [--bytes] [--encoding] [--bom] [--trailing-ws] [--max-line-len] [--binary] [--kind] [--mtime] [--hash] [--json]'
        description = 'Byte-level file checks; missing paths report kind=missing, never error.'
        params      = @(
            [pscustomobject]@{ flag = '<path...>';            detail = 'Paths (positional or pipeline). Missing paths report kind=missing, never error; directories report kind=directory.' }
            [pscustomobject]@{ flag = '--eof-newline';        detail = 'Report whether the file ends with a newline (eofNewline).' }
            [pscustomobject]@{ flag = '--tabs';               detail = 'Report whether the file contains tabs (tabs).' }
            [pscustomobject]@{ flag = '--line-endings';       detail = 'Report the dominant ending: none|crlf|lf|cr|mixed (lineEndings).' }
            [pscustomobject]@{ flag = '--lines';              detail = 'Report the line count (lines).' }
            [pscustomobject]@{ flag = '--bytes';              detail = 'Report the byte length (bytes).' }
            [pscustomobject]@{ flag = '--encoding';           detail = 'Report BOM-derived encoding: utf-8|utf-8-bom|utf-16le|utf-16be (encoding).' }
            [pscustomobject]@{ flag = '--bom';                detail = 'Report whether a BOM is present (bom).' }
            [pscustomobject]@{ flag = '--trailing-ws';        detail = 'Report trailing spaces/tabs on any line (trailingWs).' }
            [pscustomobject]@{ flag = '--max-line-len';       detail = 'Report the longest line length (maxLineLen).' }
            [pscustomobject]@{ flag = '--binary';             detail = 'Report whether NUL bytes are present (binary).' }
            [pscustomobject]@{ flag = '--kind';               detail = 'Report kind=file|directory|missing (always included regardless of flags).' }
            [pscustomobject]@{ flag = '--mtime';              detail = 'Report LastWriteTime (mtime).' }
            [pscustomobject]@{ flag = '--hash';               detail = 'Report the SHA256 hex of the file bytes (hash).' }
            [pscustomobject]@{ flag = '--json';               detail = 'Emit { count, truncated, items }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-head'
        group       = 'inspection'
        signature   = 'gim-head -n N [--skip N] [--last] [--json]'
        description = 'First/last lines of files or a piped text stream; replaces Select-Object -First/-Skip/-Last.'
        params      = @(
            [pscustomobject]@{ flag = '-n N';      detail = 'Number of lines (default 10).' }
            [pscustomobject]@{ flag = '--skip N';  detail = 'Skip the first N lines before the head window (per file, or across a piped stream).' }
            [pscustomobject]@{ flag = '--last';    detail = 'Emit the final N lines instead (tail); --skip does not apply.' }
            [pscustomobject]@{ flag = '--json';    detail = 'Emit { count, truncated, items } of { path, line, text }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-count'
        group       = 'inspection'
        signature   = 'gim-count [--by <field>] [--json]'
        description = 'Count pipeline items; --by groups counts per record field; replaces Group-Object + count loops.'
        params      = @(
            [pscustomobject]@{ flag = '--by <field>';  detail = 'Group counts per record field value (bare strings group by themselves).' }
            [pscustomobject]@{ flag = '--json';        detail = 'Emit { count, truncated, items } of { group, count } (or { count } without --by).' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-diff'
        group       = 'inspection'
        signature   = 'gim-diff --against <glob|path>... | --against-var <name> [--json]'
        description = 'Two-set path comparison; state in only-left|only-right|both; no intermediate variables.'
        params      = @(
            [pscustomobject]@{ flag = '<path...>';            detail = 'Left set paths (positional or pipeline, e.g. gim-list output); accepts the ''path: ...'' record form.' }
            [pscustomobject]@{ flag = '--against <glob|path>'; detail = 'Right set: a glob or contained literal path; repeatable.' }
            [pscustomobject]@{ flag = '--against-var <name>';  detail = 'Right set from a stored newline-separated path list (TTL-enforced, containment-revalidated); accepts the ''path: ...'' record form.' }
            [pscustomobject]@{ flag = '--json';                detail = 'Emit { count, truncated, items } of { path, state }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-json'
        group       = 'inspection'
        signature   = 'gim-json --path <query> [--keys] [--validate] [--json]'
        description = 'Query JSON documents with a dotted/bracket path; replaces Select-Object -ExpandProperty.'
        params      = @(
            [pscustomobject]@{ flag = '<path...>';      detail = 'JSON file paths (positional or pipeline); YAML/TOML inputs error as unsupported format.' }
            [pscustomobject]@{ flag = '--path <query>'; detail = 'Dotted/bracket query (a.b[0].c); without it the whole document is the node.' }
            [pscustomobject]@{ flag = '--keys';         detail = 'List keys (property names or array indices) at the queried node.' }
            [pscustomobject]@{ flag = '--validate';     detail = 'Check the document parses; report { path, valid } instead of querying.' }
            [pscustomobject]@{ flag = '--json';         detail = 'Emit { count, truncated, items } of { path, value } (or key strings with --keys).' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-asm'
        group       = 'capability'
        signature   = 'gim-asm types --package <id>/<ver> | --path <dll> [--matches <re>] [--json]'
        description = 'Enumerate exported type full names via PE metadata; never loads the assembly.'
        params      = @(
            [pscustomobject]@{ flag = 'types';                 detail = 'Mode (required).' }
            [pscustomobject]@{ flag = '--package <id>/<ver>';  detail = 'Resolve the package under the nuget read-extra root and pick the best-TFM lib/ dlls.' }
            [pscustomobject]@{ flag = '--path <dll>';          detail = 'Read one assembly directly (containment includes the nuget read-extra root).' }
            [pscustomobject]@{ flag = '--matches <re>';        detail = 'Filter type full names by regex.' }
            [pscustomobject]@{ flag = '--json';                detail = 'Emit { count, truncated, items } of { path, type }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-set'
        group       = 'state'
        signature   = 'gim-set <name> [--clear <name>] [--clear-all] [--ttl <sec>] [--json]'
        description = 'Tee: store the incoming stream in the variable store and pass it through.'
        params      = @(
            [pscustomobject]@{ flag = '<name>';         detail = 'Variable name matching ^[A-Za-z0-9_-]{1,64}$.' }
            [pscustomobject]@{ flag = '--clear <name>'; detail = 'Remove one variable instead of storing.' }
            [pscustomobject]@{ flag = '--clear-all';    detail = 'Remove every variable in the namespace.' }
            [pscustomobject]@{ flag = '--ttl <sec>';    detail = 'Lifetime from last write (default 1 hour; 0 = never expires; negative rejected).' }
            [pscustomobject]@{ flag = '--json';         detail = 'Store without passing through; emit { name, ttl, stored_bytes }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-get'
        group       = 'state'
        signature   = 'gim-get <name> [--max-age <sec>] [--json]'
        description = 'Read a stored variable (head-only pipeline source); TTL checked on read, never stale.'
        params      = @(
            [pscustomobject]@{ flag = '<name>';          detail = 'Variable name to read.' }
            [pscustomobject]@{ flag = '--max-age <sec>'; detail = 'Demand the value be at most this old.' }
            [pscustomobject]@{ flag = '--json';          detail = 'Emit { name, value, age_seconds, ttl_seconds, expired }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-fix'
        group       = 'mutating'
        signature   = 'gim-fix <eof-newline|line-endings|trailing-ws> [--to lf|crlf] [--dry-run] [--json]'
        description = 'Fix byte-level issues in place; pairs with gim-inspect flags; encoding-preserving.'
        params      = @(
            [pscustomobject]@{ flag = '<mode>';             detail = 'eof-newline | line-endings | trailing-ws (pairs with the gim-inspect flags).' }
            [pscustomobject]@{ flag = '<path...>';          detail = 'Files (positional/pipeline) or the text record lines emitted by gim-inspect through gim-where (path: ... form).' }
            [pscustomobject]@{ flag = '--to lf|crlf';       detail = 'Target line ending for eof-newline/line-endings (default: match the file for eof-newline, lf for line-endings).' }
            [pscustomobject]@{ flag = '--dry-run';          detail = 'Compute the report and write nothing.' }
            [pscustomobject]@{ flag = '--json';             detail = 'Emit { count, truncated, items } of { path, mode, before, after, changed }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-write'
        group       = 'mutating'
        signature   = 'gim-write --path <p> [--from-stdin] [--create-only] [--encoding utf8|utf8bom|utf16] [--out-dir <d>] [--path-from-var <name>] [--dry-run] [--json]'
        description = 'Write a pipeline stream to a contained file; default UTF-8 without BOM.'
        params      = @(
            [pscustomobject]@{ flag = '--path <p>';            detail = 'Target path; relative lands under --out-dir, absolute must resolve inside the current repo.' }
            [pscustomobject]@{ flag = '--from-stdin';          detail = 'Optional when content is piped; required when nothing is piped.' }
            [pscustomobject]@{ flag = '--create-only';         detail = 'Refuse to overwrite an existing target.' }
            [pscustomobject]@{ flag = '--encoding utf8|utf8bom|utf16'; detail = 'Write encoding (default utf8 = UTF-8 without BOM; PS 5.1 Out-File emits UTF-16).' }
            [pscustomobject]@{ flag = '--out-dir <d>';         detail = 'Directory for a relative --path (default .agent-scratch/).' }
            [pscustomobject]@{ flag = '--path-from-var <name>'; detail = 'Read the target path from the variable store (TTL-enforced, containment-revalidated).' }
            [pscustomobject]@{ flag = '--dry-run';             detail = 'Print the report and write nothing (no directory creation either).' }
            [pscustomobject]@{ flag = '--json';                detail = 'Emit { count, truncated, items } of { path, existed, created, bytes, encoding, dry_run }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-apply'
        group       = 'mutating'
        signature   = 'gim-apply --create-empty | --touch | --remove [--dry-run] [--json]'
        description = 'Fixed action vocabulary (--create-empty | --touch | --remove) over a filtered pipeline set; targets may be in the current repo or the OS temp root.'
        params      = @(
            [pscustomobject]@{ flag = '--create-empty';  detail = 'Create an empty file; errors when the target exists.' }
            [pscustomobject]@{ flag = '--touch';         detail = 'Update LastWriteTime, creating the file when missing.' }
            [pscustomobject]@{ flag = '--remove';        detail = 'Delete the file; missing is a no-op (changed=False).' }
            [pscustomobject]@{ flag = '<path...>';       detail = 'Files (positional/pipeline) or the gim-inspect text record lines (path: ... form); directories error for every action. Must resolve inside the current repo or the real OS temp root.' }
            [pscustomobject]@{ flag = '--dry-run';       detail = 'Report what would change and touch nothing.' }
            [pscustomobject]@{ flag = '--json';          detail = 'Emit { count, truncated, items } of { path, action, changed }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-run'
        group       = 'execution'
        signature   = 'gim-run <build|test|run|publish|ef|format> [--project <p>] [--project-from-var <name>] [--timeout <sec>] [--dry-run] [--json] [-- <args>...]'
        description = 'Run a dotnet CLI verb on a contained project; env scrub, hard timeout, audit.'
        params      = @(
            [pscustomobject]@{ flag = '<verb>';                 detail = 'build | test | run | publish | ef | format.' }
            [pscustomobject]@{ flag = '--project <p>';           detail = 'Project path; relative resolves against the repo root; must be contained in the current repo (exec roots).' }
            [pscustomobject]@{ flag = '--project-from-var <name>'; detail = 'Read the project path from the variable store (TTL-enforced).' }
            [pscustomobject]@{ flag = '--timeout <sec>';         detail = 'Hard timeout (default 300s, cap 1800s); on expiry the whole process tree is killed (taskkill /T /F) and the verb exits 124.' }
            [pscustomobject]@{ flag = '--dry-run';               detail = 'Print the argv and the scrubbed credential env names; run nothing.' }
            [pscustomobject]@{ flag = '--json';                  detail = 'Emit { dry_run, verb, project, argv, scrubbed_env, exit_code, timed_out, stdout, stderr, truncated }.' }
            [pscustomobject]@{ flag = '-- <args>...';            detail = 'Verbatim trailing dotnet args. PowerShell consumes the first -- itself, so tokens colliding with gim-run flags need a doubled marker (-- -- --json).' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-probe'
        group       = 'execution'
        signature   = 'gim-probe --project <p> --route <r> [--method <m>] [--body-file <f>] [--timeout <sec>] [--dry-run] [--json]'
        description = 'Launch an app on a random local port, poll readiness, issue one HTTP request; always teardown.'
        params      = @(
            [pscustomobject]@{ flag = '--project <p>';    detail = 'Required; project path contained in the current repo (exec roots).' }
            [pscustomobject]@{ flag = '--route <r>';      detail = 'Required; route to request (e.g. /health). Intended for locally-launched apps on the random port; an absolute http(s) URL is accepted but is outside that local-only design intent.' }
            [pscustomobject]@{ flag = '--method <m>';     detail = 'HTTP method (default GET; GET/POST/PUT/DELETE/PATCH/HEAD/OPTIONS).' }
            [pscustomobject]@{ flag = '--body-file <f>';  detail = 'Request body file (contained, sent as application/json).' }
            [pscustomobject]@{ flag = '--timeout <sec>';  detail = 'Readiness timeout (default 300s, cap 1800s); exit 124 on readiness timeout.' }
            [pscustomobject]@{ flag = '--dry-run';        detail = 'Print the argv and the scrubbed credential env names; launch nothing.' }
            [pscustomobject]@{ flag = '--json';           detail = 'Emit { project, route, method, port, status_code, body, exit_code, timed_out }.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-preflight'
        group       = 'fleet'
        signature   = 'gim-preflight --repo <name|path> [--repo <name|path>]... [--branch <name>] [--skip-gh-auth] [--skip-jira-auth] [--dry-run] [--json]'
        description = 'Fleet preflight: auth status, per-repo branch/clean state, parallel fetch, conditional ff-only pull, and product docs indexes.'
        params      = @(
            [pscustomobject]@{ flag = '--repo <name|path>';  detail = 'Repository name or path (repeatable). An absolute path is used as given, a relative path resolves against the current repo root, and a simple name resolves case-insensitively under the parent of the current repo root (must be a git root, .git file or directory). Missing names list the available directories; case-only ambiguity lists the matches. Records branch plus clean/dirty; untracked .agent-scratch/ evidence is ignored by the dirty check.' }
            [pscustomobject]@{ flag = '--branch <name>';      detail = 'Expected branch (default: dev). Pulls --ff-only only when clean and on this branch; pass the prefs default_dev_branch explicitly.' }
            [pscustomobject]@{ flag = '--skip-gh-auth';       detail = 'Skip gh auth. The docs index then marks each family ok=false with next=null (open-PR source unavailable); the run itself does not fail.' }
            [pscustomobject]@{ flag = '--skip-jira-auth';     detail = 'Skip acli jira auth; marks the check skipped, not failed.' }
            [pscustomobject]@{ flag = '--dry-run';            detail = 'Run no fetch or pull; report the planned actions plus the read-only status/docs index.' }
            [pscustomobject]@{ flag = '--json';               detail = 'Emit { ok, dry_run, gh_auth, jira_auth, repos[] } with per-repo fetch/pull status and docs_indexes.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-pr'
        group       = 'fleet'
        signature   = 'gim-pr --repo <name|path> (--files-file [<path>] | --file <path> [--file <path>]...) --message <subject> --title <title> --target <branch> [--new-branch <branch>] [--pr-body <path>] [--draft] [--delete-pr-body] [--remote <name>] [--assignee <login>] [--dry-run] [--json]'
        description = 'One explicit PR flow: stage the declared file list, guard the index, commit, push -u, create the PR, then run the stack lifecycle. First-PR mode refuses dev, main, and release/* as protected branches. Lifecycle mechanics (config, init/add/link/sync verification, result fields, manual fallback) are owned by github-cli-workflow section 4 + references/gim-stack-lifecycle.md; this verb is the executable path.'
        params      = @(
            [pscustomobject]@{ flag = '--repo <name|path>';  detail = 'Repository name or path. An absolute path is used as given, a relative path resolves against the current repo root, and a simple name resolves case-insensitively under the parent of the current repo root (must be a git root).' }
            [pscustomobject]@{ flag = '--files-file [<path>]'; detail = 'File list source. With no value, derives .agent-scratch/pr-files/<feature>/<step>/files.txt from the head branch (--new-branch when given, else the current branch): known first-PR prefixes (feature, docs, chore, hotfix, release, fix, bugfix, refactor, test, ci, build, perf) map <prefix>/<slug> to <slug>/<prefix>; other <feature>/<step> branches map to <feature>/<step>. More than one slash is rejected.' }
            [pscustomobject]@{ flag = '--file <path>';       detail = 'Repo-relative file path; repeatable. Exactly one file source (--files-file or --file) is required.' }
            [pscustomobject]@{ flag = '--message <subject>'; detail = 'Required. Commit subject (git commit --message).' }
            [pscustomobject]@{ flag = '--title <title>';     detail = 'Required. PR title.' }
            [pscustomobject]@{ flag = '--target <branch>';   detail = 'Required. PR base branch (the previous step branch, or the feature branch for the first PR).' }
            [pscustomobject]@{ flag = '--new-branch <branch>'; detail = 'Step PR: create and check out this branch inside the local stack (gh stack add) before staging. Omit for the first PR, which adopts the current branch with gh stack init.' }
            [pscustomobject]@{ flag = '--pr-body <path>';    detail = 'PR body file. A relative path resolves against the repo root, then must resolve inside the evidence directory (same directory as files.txt/result.json); an absolute path must already be inside it. Required unless --draft; a draft without a body passes --body "".' }
            [pscustomobject]@{ flag = '--draft';             detail = 'Create the PR as a draft.' }
            [pscustomobject]@{ flag = '--delete-pr-body';    detail = 'Delete the PR body file after success. The file is kept on failure.' }
            [pscustomobject]@{ flag = '--remote <name>';     detail = 'Push/stack remote (default origin). git push -u <remote> HEAD; gh stack link and sync get --remote.' }
            [pscustomobject]@{ flag = '--assignee <login>';  detail = 'PR assignee (default @me).' }
            [pscustomobject]@{ flag = '--dry-run';           detail = 'Resolve the repo/branch/file list and print the command plan in execution order (config, init/add, stage, commit, push, PR create, link, view, sync); no mutation.' }
            [pscustomobject]@{ flag = '--json';              detail = 'Emit the result record (also written to result.json): repo, branch, head, new_branch, remote, files, commit, push, pr, stack (method, number, url, pending, verified, link {exit,output}, sync {exit,output,count_line}, view_error, error), body, warnings, error.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-jira-search'
        group       = 'fleet'
        signature   = 'gim-jira-search [--keyword <kw>...] [--jql <jql>] [--project <key>] [--limit <n>] [--current-sprint] [--json]'
        description = 'Read-only Jira search: no input queries the project (default GIM); keyword queries use summary OR description containment, one query per keyword; explicit JQL is used as given. Dedupe by issue key; return key, summary, status, and assignee display name only (never email, account id, or avatar). --current-sprint resolves the single active sprint from the D0 board; zero or many active sprints fail and require an explicit sprint filter in --jql. Default --limit is 50.'
        params      = @(
            [pscustomobject]@{ flag = '--keyword <kw>';       detail = 'Optional keyword query; repeatable. Builds project = <key> AND (summary ~ "<kw>" OR description ~ "<kw>"), one acli query per keyword. Omit with --jql and no-input project scans.' }
            [pscustomobject]@{ flag = '--jql <jql>';          detail = 'Optional explicit JQL query; used as given. No input means project = <key>.' }
            [pscustomobject]@{ flag = '--project <key>';      detail = 'Project key for keyword and no-input queries (default GIM).' }
            [pscustomobject]@{ flag = '--limit <n>';          detail = 'Maximum results per acli query (positive integer; default 50).' }
            [pscustomobject]@{ flag = '--current-sprint';     detail = 'Resolve the active sprint with acli board list-sprints and AND sprint = <id>; fail when zero or many active sprints exist.' }
            [pscustomobject]@{ flag = '--json';               detail = 'Emit { ok, count, truncated, items[] }; each item is { key, summary, status, assignee } with assignee display name only.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-jira-create'
        group       = 'fleet'
        signature   = 'gim-jira-create --summary <s> [--project <key>] [--type <type>] [--assignee <@me|email>] [--unassigned] [--description-file <f>] [--sprint <id>] [--current-sprint] [--parent <key>] [--label <l>...] [--dry-run] [--json]'
        description = 'Create a Jira work item from the observed acli template, then assign and verify. Payload assignee is null; assigns @me/explicit unless --unassigned. Description text becomes plain ADF paragraphs split on blank lines (markdown not interpreted); labels default null and a label-specific rejection retries once with null; sprint is the D0 bare integer under sprint.field_id. Returns key + URL, never email/account id. --dry-run prints payload and commands without calling acli.'
        params      = @(
            [pscustomobject]@{ flag = '--summary <s>';           detail = 'Required work-item title.' }
            [pscustomobject]@{ flag = '--project <key>';          detail = 'Project key (default GIM).' }
            [pscustomobject]@{ flag = '--type <type>';            detail = 'Work-item type (default Task).' }
            [pscustomobject]@{ flag = '--assignee <@me|email>';   detail = 'Assignment target (default @me). Conflicting with --unassigned.' }
            [pscustomobject]@{ flag = '--unassigned';             detail = 'Skip post-create assignment (explicit exception).' }
            [pscustomobject]@{ flag = '--description-file <f>';   detail = 'Markdown file converted to the observed ADF document.' }
            [pscustomobject]@{ flag = '--sprint <id>';            detail = 'Explicit sprint id; D0 config must support create-time sprint assignment.' }
            [pscustomobject]@{ flag = '--current-sprint';         detail = 'Resolve the single active sprint from the D0 board; zero/many fails and requires explicit --sprint.' }
            [pscustomobject]@{ flag = '--parent <key>';           detail = 'Parent work-item key (sub-task).' }
            [pscustomobject]@{ flag = '--label <l>';              detail = 'Label; repeatable. Defaults to null; a label rejection retries once with null.' }
            [pscustomobject]@{ flag = '--dry-run';                detail = 'Print payload and exact create/assign/view commands; no acli call.' }
            [pscustomobject]@{ flag = '--json';                   detail = 'Emit { ok, dry_run, key, url, project, type, summary, sprint_id, assigned, payload, commands, warnings, error } for success, dry-run, and argument failures; never email/account id.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-jira-transition'
        group       = 'fleet'
        signature   = 'gim-jira-transition --key <key> --to <status> [--project <key>] [--dry-run] [--json]'
        description = 'Transition a Jira work item to a configured status. Reads the current status with the positional-key view form; a target equal to current is a no-op. Selects routes[].sequence from the D0 jira-transitions.json config and runs each step with acli workitem transition --status (the CLI has no --to flag). Stops at the first failure and returns the exact command and per-step results. --dry-run makes no acli call and prints the placeholder current status, direct target sequence, and command.'
        params      = @(
            [pscustomobject]@{ flag = '--key <key>';           detail = 'Required work-item key such as GIM-123; must belong to the selected project.' }
            [pscustomobject]@{ flag = '--to <status>';         detail = 'Required target status; validated against the D0 config statuses.' }
            [pscustomobject]@{ flag = '--project <key>';       detail = 'Project key used for key validation (default: the D0 config project).' }
            [pscustomobject]@{ flag = '--dry-run';             detail = 'Print current placeholder, target, sequence, and exact command; no acli call.' }
            [pscustomobject]@{ flag = '--json';                detail = 'Emit { ok, dry_run, key, project, current, target, sequence, commands, results, error } for success, no-op, dry-run, and argument failures.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-wrapup-gather'
        group       = 'fleet'
        signature   = 'gim-wrapup-gather --repo <name|path> (--chain <pr> | --pr <n>... | --branch <branch>...) [--out <path>] [--parallel <n>] [--log-parallel <n>] [--json]'
        description = 'Fetch-only PR gather for gim-wrapup. PowerShell shim + dependency-free Node helper (Node >= 20). --chain is primary: walk head/base down then up from one PR, bottom -> top, stopping on trunk, ambiguity, cycle, or 50 PRs. --pr/--branch are explicit fallbacks; modes are mutually exclusive. Independent PR/log pools (defaults 4/2); threads map reviews[].comments[] with replies[]. Failing-check gh run view logs stream to logs/ (50 MB/file, 200 MB total, gather-dir-relative log_file, log_source job/run). Writes schema v1 gather.json (1-space, nulls omitted, pass/skipping checks reduced to {name,state}) plus the authored responses.json shell (PRs with unresolved threads or top-level comments, plus per-PR comment strings) under .agent-scratch/wrapup/<slug>/ or --out. Never posts, resolves, pushes, or calls gh api.'
        params      = @(
            [pscustomobject]@{ flag = '--repo <name|path>';       detail = 'Repository name (case-insensitive sibling) or path; resolved through Resolve-GimFleetRepo.' }
            [pscustomobject]@{ flag = '--chain <pr>';             detail = 'Primary mode: walk the open-PR stack around this PR. Mutually exclusive with --pr and --branch.' }
            [pscustomobject]@{ flag = '--pr <n>';                 detail = 'Explicit PR number; repeatable. Mutually exclusive with --chain and --branch.' }
            [pscustomobject]@{ flag = '--branch <branch>';        detail = 'Explicit PR head branch; repeatable. Mutually exclusive with --chain and --pr.' }
            [pscustomobject]@{ flag = '--out <path>';             detail = 'Output path for gather.json, repo-relative or absolute inside the resolved repo. Default .agent-scratch/wrapup/<slug>/gather.json; responses.json is its sibling.' }
            [pscustomobject]@{ flag = '--parallel <n>';           detail = 'PR fetch concurrency (default 4). Independent of --log-parallel.' }
            [pscustomobject]@{ flag = '--log-parallel <n>';       detail = 'Failing-log fetch concurrency (default 2). Independent of --parallel.' }
            [pscustomobject]@{ flag = '--json';                   detail = 'Emit { ok, path, repo, summary, warnings, error } only; read the file for the normalized document.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-wrapup-respond'
        group       = 'fleet'
        signature   = 'gim-wrapup-respond --repo <name|path> --file <responses.json> --harness <name> [--dry-run] [--json]'
        description = 'Consume responses.json: inline thread replies via gh-pr-review comments reply (then threads resolve only when resolve=true and remotely open), free-form per-PR comments via gh pr comment in order. Apply-time read-only remote refresh per touched PR; sibling <responses>.result.json receipt records posted_at/resolved_at/remote_state/last_error; reruns skip completed entries and retry failures. Authored responses.json is never rewritten; --dry-run makes zero gh calls; footer appended once; per-entry failures return ok=false with counts.'
        params      = @(
            [pscustomobject]@{ flag = '--repo <name|path>';       detail = 'Repository name or path for gh -R; resolves through Resolve-GimFleetRepo.' }
            [pscustomobject]@{ flag = '--file <path>';           detail = 'Existing responses.json under a workspace root from roots.json; relative paths resolve against the resolved repo root.' }
            [pscustomobject]@{ flag = '--harness <name>';        detail = 'Required footer harness name: letters, digits, dot, underscore, hyphen.' }
            [pscustomobject]@{ flag = '--dry-run';               detail = 'Print planned replies/resolves/comments; no gh call and no receipt write.' }
            [pscustomobject]@{ flag = '--json';                  detail = 'Emit { ok, repo, file, receipt, dry_run, posted, resolved, skipped, failed, warnings[], actions[], error }; ok=false when any entry failed.' }
        )
    }
    [pscustomobject]@{
        name        = 'gim-verbs'
        group       = 'meta'
        signature   = 'gim-verbs --json | --gen-refs [--out-dir <d>]'
        description = 'Meta: emit the full verb vocabulary (--json) or write per-group reference docs (--gen-refs).'
        params      = @(
            [pscustomobject]@{ flag = '--json';         detail = 'Emit the full vocabulary: { count, truncated, items } of { name, group, signature, params, description }.' }
            [pscustomobject]@{ flag = '--gen-refs';     detail = 'Write references/<group>.md per group with per-verb flag detail. Deterministic: re-running produces identical bytes.' }
            [pscustomobject]@{ flag = '--out-dir <d>';  detail = 'Output directory for --gen-refs (default .agents/skills/gim-tools/references; relative resolves against the repo root).' }
        )
    }
)

function Get-GimVerbDefinitions {
    <#
    .SYNOPSIS
        The shared verb-definition table (ordered by group then plan order).
    #>
    return @($script:GimVerbDefinitions)
}

function Get-GimVerbGroups {
    <#
    .SYNOPSIS
        Ordered unique group names of a definition set (group order per plan).
    #>
    param([object[]]$Definitions)
    $groups = New-Object System.Collections.Generic.List[string]
    foreach ($d in $Definitions) {
        if (-not $groups.Contains([string]$d.group)) { $groups.Add([string]$d.group) }
    }
    return @($groups.ToArray())
}

function Format-GimVerbGroupDoc {
    <#
    .SYNOPSIS
        Render one group's reference markdown (references/<group>.md).
    .DESCRIPTION
        Deterministic: fixed group order, fixed verb order, no timestamps or
        machine paths, LF newlines, UTF-8 without BOM at write time. The
        header names the generator so hand-edits are discouraged.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Definitions,
        [Parameter(Mandatory = $true)]
        [string]$Group
    )
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('# gim-tools reference: ' + $Group + "`n")
    [void]$sb.Append("`n")
    [void]$sb.Append('Per-group verb reference for the gim-tools skill.' + "`n")
    [void]$sb.Append('Generated by `gim-verbs --gen-refs`; deterministic, do not hand-edit.' + "`n")
    foreach ($d in $Definitions) {
        if ([string]$d.group -ne $Group) { continue }
        [void]$sb.Append("`n")
        [void]$sb.Append('## ' + $d.name + "`n")
        [void]$sb.Append("`n")
        [void]$sb.Append('Signature: `' + [string]$d.signature + '`' + "`n")
        [void]$sb.Append("`n")
        [void]$sb.Append([string]$d.description + "`n")
        [void]$sb.Append("`n")
        [void]$sb.Append('| Flag | Detail |' + "`n")
        [void]$sb.Append('| --- | --- |' + "`n")
        foreach ($p in $d.params) {
            $flagCell = ([string]$p.flag) -replace '\|', '\|'
            $detailCell = ([string]$p.detail) -replace '\|', '\|'
            [void]$sb.Append('| `' + $flagCell + '` | ' + $detailCell + ' |' + "`n")
        }
    }
    return $sb.ToString()
}

function Write-GimVerbRefDocs {
    <#
    .SYNOPSIS
        Write references/<group>.md for every group into OutDir (created on
        demand). Returns the written paths.
    .DESCRIPTION
        OutDir is write-contained (current repo + .agent-scratch/): relative
        paths resolve against the repo root; escapes, $env:TEMP, UNC, and
        unexpanded %VAR% tokens are rejected. Files are written UTF-8 without
        BOM, LF newlines, so a re-run is byte-identical.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Definitions,
        [Parameter(Mandatory = $true)]
        [string]$OutDir
    )
    $dir = $OutDir
    if (-not [System.IO.Path]::IsPathRooted($dir)) {
        $dir = [System.IO.Path]::GetFullPath((Join-Path (Get-GimRepoRoot) $dir))
    }
    $real = Resolve-GimWritePath -Path $dir
    if (-not (Test-Path -LiteralPath $real -PathType Container)) {
        New-Item -ItemType Directory -Path $real -Force | Out-Null
    }
    $written = New-Object System.Collections.Generic.List[string]
    foreach ($group in (Get-GimVerbGroups -Definitions $Definitions)) {
        $doc = Format-GimVerbGroupDoc -Definitions $Definitions -Group $group
        $path = Join-Path $real ($group + '.md')
        [System.IO.File]::WriteAllText($path, $doc, (New-Object System.Text.UTF8Encoding($false)))
        $written.Add($path)
    }
    return @($written.ToArray())
}
