# gim-permissions.ps1 — Cursor production permission hook (plan 0004 C1).
#
# Deny-only hook for the Cursor IDE agent (ADR 0007). The Auto-run allowlist
# (user-maintained UI list, see AGENTS.md) is the ONLY silent-allow layer; this
# hook is the ONLY silent-deny layer. `preToolUse` returning
# `{permission:"deny", agent_message, user_message}` blocks the call before
# permission evaluation and beats the allowlist (findings C1/E2). A hook
# `allow` is a no-op against the ask UI (findings E1) — this hook NEVER returns
# allow: pass is `{}` (silent), deny carries the replacement message
# (discovery-at-failure, harness-config).
#
# Decision logic mirrors the opencode plugin's before-stage denies
# (.opencode/plugins/lib/gim-permissions-rules.js) — same rule set, same
# messages. Commands are parsed with the ONE central lib
# (.agents/skills/gim-tools/scripts/parse-command-tree.ps1), dot-sourced
# in-process (Get-GimCommandTree — NO second pwsh spawn; the hook already runs
# under pwsh). All decisions are evaluated at command-candidate positions from
# the AST tree (command names, args, non-string tokens, envelope flags), so
# strings/comment text stay inert.
#
# Harness-specific exceptions (documented, do NOT add):
#   - NO git `--` rule — Cursor has no such quirk (`git diff -- file` passes
#     fine); the git `--` pathspec rule is opencode-only.
#   - NO whitelist — Cursor's mean is Auto-run allowlist + complement deny;
#     the Auto-run checklist (AGENTS.md) is the allow side.
#   - Force-push variants only: plain `git push` is NOT denied.
#   - git -C is case-sensitive (`git -c` config is not caught).
#
# Output contract: JSON-only stdout — `{}` (silent/pass) or
# `{"permission":"deny","agent_message":"...","user_message":"..."}`. A
# catch-all around the whole handler ensures ANY error -> `{}` (a broken hook
# must not block the IDE). Logging is file-only:
# %TEMP%\cursor\gim-permissions.log (capped ~5 MB).

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
param()

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ---------------------------------------------------------------------------
# Logging (file-only, capped ~5 MB)
# ---------------------------------------------------------------------------
function Write-GimLog {
    param([string]$Line)
    try {
        $logDir = Join-Path $env:TEMP 'cursor'
        $logFile = Join-Path $logDir 'gim-permissions.log'
        [void][System.IO.Directory]::CreateDirectory($logDir)
        if ([System.IO.File]::Exists($logFile)) {
            try {
                if ((Get-Item -LiteralPath $logFile).Length -gt (5MB)) {
                    [System.IO.File]::WriteAllText($logFile, '')
                }
            } catch { }
        }
        $iso = [DateTimeOffset]::UtcNow.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
        [System.IO.File]::AppendAllText(
            $logFile,
            ("{0} {1}{2}" -f $iso, $Line, [Environment]::NewLine)
        )
    } catch {
        # logging is best-effort; never break the hook over a log failure
    }
}

# ---------------------------------------------------------------------------
# Central lib (dot-sourced in-process, no second spawn)
# ---------------------------------------------------------------------------
$script:CentralLibLoaded = $false
try {
    $centralLib = Join-Path $PSScriptRoot '..\..\.agents\skills\gim-tools\scripts\parse-command-tree.ps1'
    . $centralLib
    $script:CentralLibLoaded = (Get-Command Get-GimCommandTree -ErrorAction SilentlyContinue) -ne $null
    if (-not $script:CentralLibLoaded) {
        Write-GimLog ('central-lib load failed: Get-GimCommandTree not defined after dot-source: {0}' -f $centralLib)
    }
} catch {
    $script:CentralLibLoaded = $false
    Write-GimLog ('central-lib load failed: {0}' -f $_.Exception.Message)
}

# ---------------------------------------------------------------------------
# Rule tables (mirror .opencode/plugins/lib/gim-permissions-rules.js)
# ---------------------------------------------------------------------------
$script:BlockingCmdlets = @(
    'foreach-object', 'where-object', 'group-object', 'sort-object', 'select-object'
)

# Replacement-only hint per blocked cmdlet (message contract: rules carry
# reason + alternative; the wiring frames the final message).
$script:CmdletAlternatives = @{
    'foreach-object' = 'use gim-report --prefix <label> (or --prefix-each) instead'
    'where-object'   = 'use gim-where --<predicate> (e.g. --exists, --matches <re>) instead'
    'group-object'   = 'use gim-count --by <field> instead'
    'sort-object'    = 'use gim-report --sort <field> instead'
    'select-object'  = 'use gim-head -n N (--skip N / --last) or gim-json --path <query> instead'
}

# Command-position pwsh/powershell deny (user-locked): exact command-name
# match only — args/strings/`Get-Command pwsh` are NOT denied. Agents NEVER
# invoke pwsh (the harness shell already runs pwsh; scripts run in-shell).
$script:PwshCommands = @('pwsh', 'powershell')

$script:ExecutionEscapes = @(
    'invoke-expression', 'iex', 'add-type', 'start-process'
)

$script:ReflectionMarkers = @(
    '$ExecutionContext', '.GetType(', '.Assembly', 'GetMethod', 'GetField', '.Invoke(', 'GetType', 'Assembly', 'Invoke'
)

# Bare member-name markers count ONLY in member-access shape (token preceded
# by a `.` token). The central lib's memberCalls envelope + `::` staticCalls
# + `$ExecutionContext` already cover the real .NET surface, so bare words
# containing these names (`git commit -m GetMethod`) stay inert.
$script:BareReflectionMarkers = @('GetMethod', 'GetField', 'GetType', 'Assembly', 'Invoke')

$script:ReflectionMemberNames = @('GetType', 'Assembly', 'Invoke', 'GetMethod', 'GetField')

# String-literal token kinds to skip in reflection scans (strings stay
# inert). Actual TokenKind names verified against Get-GimCommandTree output:
# single-quoted '...' = StringLiteral, expandable "..." = StringExpandable,
# here-strings = HereStringLiteral / HereStringExpandable.
$script:StringTokenKinds = @(
    'StringLiteral', 'StringExpandable', 'HereStringLiteral', 'HereStringExpandable'
)

$script:GitReadSubcommands = @('status', 'log', 'diff', 'blame', 'show')

# ---------------------------------------------------------------------------
# Payload handling (payload shapes per docs/agents/0001-harness-permission-findings.md)
# ---------------------------------------------------------------------------
function Get-GimProp {
    <#
    .SYNOPSIS
        Property read that tolerates both PSCustomObject (JSON stdin,
        ConvertFrom-Json) and IDictionary (hashtable) payloads.
    #>
    param($Obj, [string]$Name)
    if ($null -eq $Obj) { return $null }
    if ($Obj -is [System.Collections.IDictionary]) {
        if ($Obj.ContainsKey($Name)) { return $Obj[$Name] }
        return $null
    }
    if ($Obj.PSObject.Properties[$Name]) { return $Obj.$Name }
    return $null
}

function Get-GimCommandText {
    <#
    .SYNOPSIS
        Extract the command text from a Cursor hook stdin payload.
    .DESCRIPTION
        preToolUse (Shell): tool_input.command. beforeShellExecution:
        payload.command directly. Tolerant of tool_input being a string that
        itself is JSON, and of hashtable payloads (in-memory test callers).
    #>
    param($Payload)
    if ($null -eq $Payload) { return '' }
    $cmdProp = Get-GimProp -Obj $Payload -Name 'command'
    if ($null -ne $cmdProp -and -not [string]::IsNullOrEmpty([string]$cmdProp)) {
        return [string]$cmdProp
    }
    $ti = Get-GimProp -Obj $Payload -Name 'tool_input'
    if ($null -eq $ti) { return '' }
    if ($ti -is [string]) {
        try {
            $parsed = $ti | ConvertFrom-Json -Depth 8
            $inner = Get-GimProp -Obj $parsed -Name 'command'
            if ($null -ne $inner) { return [string]$inner }
        } catch { }
        return [string]$ti
    }
    $inner = Get-GimProp -Obj $ti -Name 'command'
    if ($null -ne $inner) { return [string]$inner }
    return ''
}

function Get-GimToolFilePath {
    <#
    .SYNOPSIS
        Extract the target file path from a non-shell tool payload (Edit/Write).
    .DESCRIPTION
        Cursor edit/write tools carry tool_input.file_path (snake_case);
        filePath / path tolerated for payload drift.
    #>
    param($Payload)
    if ($null -eq $Payload) { return '' }
    $ti = Get-GimProp -Obj $Payload -Name 'tool_input'
    if ($null -eq $ti) { return '' }
    foreach ($prop in @('file_path', 'filePath', 'path')) {
        $value = Get-GimProp -Obj $ti -Name $prop
        if ($null -ne $value -and -not [string]::IsNullOrEmpty([string]$value)) {
            return [string]$value
        }
    }
    return ''
}

# ---------------------------------------------------------------------------
# Decision predicates (mirror gim-permissions-rules.js; structural only)
# ---------------------------------------------------------------------------
function Test-GimSecretFilePath {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) { return $false }
    $lower = $Value.Replace('\', '/').ToLowerInvariant()
    if ($lower.EndsWith('.secret.json')) { return $true }
    if ($lower -match 'appsettings\..*\.secret\.json$') { return $true }
    if ($lower -match '(^|/)\.env$') { return $true }
    if ($lower.EndsWith('.user')) { return $true }
    if ($lower -match '(^|/)id_rsa$') { return $true }
    if ($lower.EndsWith('.pfx')) { return $true }
    if ($lower.EndsWith('.snk')) { return $true }
    return $false
}

function Get-GimSecretFileHit {
    <#
    .SUMMARY
        First secret-file path in a command's args, skipping git pathspec args
        of trusted-read subcommands (harness-config git `--` convention).
    #>
    param($Cmd)
    $args = @($Cmd.args)
    $sub = if ($args.Count -gt 0) { [string]$args[0] } else { '' }
    $dd = $Cmd.dashDash
    for ($i = 0; $i -lt $args.Count; $i++) {
        if ($null -ne $dd -and $null -ne $dd.index -and $i -ge [int]$dd.index -and
            $sub.ToLowerInvariant() -in $script:GitReadSubcommands) {
            continue
        }
        if (Test-GimSecretFilePath -Value ([string]$args[$i])) {
            return [string]$args[$i]
        }
    }
    return $null
}

function Get-GimReflectionHit {
    <#
    .SYNOPSIS
        Reflection markers inside a command's significant tokens, skipping
        string-literal tokens (strings stay inert). Bare member-name markers
        (GetMethod/GetField/GetType/Assembly/Invoke) count ONLY in
        member-access shape (previous significant token is `.`) — bareword
        args stay inert; the memberCalls envelope and `::` staticCalls cover
        real member access.
    #>
    param($Cmd)
    $tokens = @($Cmd.tokens)
    $prevText = ''
    foreach ($t in $tokens) {
        if ($t.kind -in $script:StringTokenKinds) { continue }
        $text = [string]$t.text
        foreach ($marker in $script:ReflectionMarkers) {
            if ($text.Contains($marker)) {
                if ($marker -in $script:BareReflectionMarkers) {
                    if ($prevText -ne '.') { continue }
                }
                return $marker
            }
        }
        $prevText = $text
    }
    return $null
}

function Get-GimMemberCallsHit {
    <#
    .SUMMARY
        Reflection surface from the central lib's memberCalls envelope
        (member-access identifiers + $ExecutionContext variable references).
    #>
    param($Tree)
    $members = @($Tree.memberCalls)
    $lowerNames = $script:ReflectionMemberNames | ForEach-Object { $_.ToLowerInvariant() }
    foreach ($m in $members) {
        if (([string]$m).ToLowerInvariant() -in $lowerNames) { return [string]$m }
    }
    foreach ($m in $members) {
        if (([string]$m).ToLowerInvariant() -eq '$executioncontext') { return '$ExecutionContext' }
    }
    return $null
}

function Get-GimGitDestructiveHit {
    param($Cmd)
    $args = @($Cmd.args)
    if ($args.Count -gt 0 -and $args[0] -eq 'branch') {
        if ($args -contains '-D') { return 'branch -D' }
        if ($args -contains '-d') { return 'branch -d' }
    }
    if ($args.Count -gt 0 -and $args[0] -eq 'add') {
        if ($args -contains '-A' -or $args -contains '--all') { return 'add -A' }
        if ($args -contains '-f' -or $args -contains '--force') { return 'add -f' }
    }
    if ($args.Count -gt 0 -and $args[0] -eq 'stash') {
        foreach ($variant in @('drop', 'apply', 'clear')) {
            if ($args -contains $variant) { return "stash $variant" }
        }
    }
    return $null
}

function Test-GimGitForcePush {
    <#
    .SUMMARY
        Force-push variants ONLY (ADR 0007 §5): push subcommand + a force flag
        (-f / --force*) in the same git command. Plain git push is NOT denied;
        non-push git with -f (git grep -f, git branch -f, ...) is NOT denied.
    #>
    param($Cmd)
    $args = @($Cmd.args)
    if ($args -notcontains 'push') { return $false }
    foreach ($a in $args) {
        if ($null -ne $a -and ($a -eq '-f' -or $a.StartsWith('--force'))) { return $true }
    }
    return $false
}

function Get-GimDotnetExecHit {
    param($Cmd)
    $args = @($Cmd.args)
    if ($args.Count -ge 2 -and $args[0] -eq 'nuget' -and $args[1] -eq 'push') { return 'nuget push' }
    if ($args.Count -ge 3 -and $args[0] -eq 'nuget' -and $args[1] -eq 'add' -and $args[2] -eq 'source') { return 'nuget add source' }
    if ($args.Count -ge 2 -and $args[0] -eq 'tool' -and $args[1] -eq 'install' -and
        ($args -contains '-g' -or $args -contains '--global')) { return 'tool install -g' }
    if ($args.Count -ge 2 -and $args[0] -eq 'new' -and $args[1] -eq 'install') { return 'new install' }
    if ($args.Count -ge 2 -and $args[0] -eq 'workload' -and $args[1] -eq 'install') { return 'workload install' }
    if ($args.Count -gt 0 -and $args[0] -eq 'exec') { return '<path>.dll / exec' }
    if ($args.Count -gt 0 -and -not [string]::IsNullOrEmpty([string]$args[0]) -and
        ([string]$args[0]).ToLowerInvariant().EndsWith('.dll')) { return '<path>.dll / exec' }
    if ($args.Count -gt 0 -and $args[0] -eq 'run') { return 'run' }
    if ($args.Count -gt 0 -and $args[0] -in @('fsi', 'script')) { return 'fsi / script' }
    if ($args.Count -gt 0 -and $args[0] -eq 'msbuild' -and ($args | Where-Object { $_ -like '-t:*' })) { return 'msbuild -t:' }
    if ($args.Count -gt 0 -and $args[0] -eq 'publish' -and ($args -contains '-o' -or $args -contains '--output')) { return 'publish -o' }
    return $null
}

function Get-GimDenyForTree {
    <#
    .SYNOPSIS
        Deny decision over a central-lib command tree. Returns
        [pscustomobject]@{ rule; reason; alternative } or $null (nothing to
        say -> `{}`). Message contract (user-locked): rules carry reason +
        alternative; Format-GimDenyMessage builds the final framed text.
    .DESCRIPTION
        Mirrors denyDecision() in .opencode/plugins/lib/gim-permissions-rules.js
        — same rule order, same reasons, same alternatives. First matching
        rule wins.
    #>
    param($Tree)

    $commands = @()
    if ($null -ne $Tree -and $Tree.ok) { $commands = @($Tree.commands) }

    foreach ($cmd in $commands) {
        $name = ([string]$cmd.name).ToLowerInvariant()
        if ($name -in $script:BlockingCmdlets) {
            return [pscustomobject]@{
                rule        = "cmdlet.$name"
                reason      = "cmdlet $name (script-block built-in)"
                alternative = $script:CmdletAlternatives[$name]
            }
        }
        if ($name -in $script:ExecutionEscapes) {
            return [pscustomobject]@{
                rule        = "executionEscape.$name"
                reason      = "direct execution escape ($name)"
                alternative = 'direct execution escapes have no sanctioned replacement; use gim-* verbs, gim-run (or version-controller)'
            }
        }
        if ($name -in $script:PwshCommands) {
            return [pscustomobject]@{
                rule        = 'pwshInvocation'
                reason      = 'pwsh/powershell invocation'
                alternative = 'your shell is already pwsh — run scripts in-shell: `& <path>` / pipeline / import (only the opencode plugin may spawn pwsh -File)'
            }
        }
    }

    if ($Tree.hasSubstitution) {
        return [pscustomobject]@{
            rule        = 'substitution'
            reason      = 'command substitution $('
            alternative = 'pass values through the gim-tools channel: gim-set / gim-get then --<param>-from-var'
        }
    }

    if (@($Tree.staticCalls) -contains '::') {
        return [pscustomobject]@{
            rule        = 'staticCall'
            reason      = ':: static member access'
            alternative = 'use gim-tools verbs; no script-block carriers'
        }
    }

    foreach ($cmd in $commands) {
        $hit = Get-GimReflectionHit -Cmd $cmd
        if ($null -ne $hit) {
            return [pscustomobject]@{
                rule        = "reflection.$hit"
                reason      = "reflection access ($hit)"
                alternative = 'use gim-tools verbs (defense in depth)'
            }
        }
    }

    $memberHit = Get-GimMemberCallsHit -Tree $Tree
    if ($null -ne $memberHit) {
        return [pscustomobject]@{
            rule        = "reflection.$memberHit"
            reason      = "reflection access ($memberHit)"
            alternative = 'use gim-tools verbs (defense in depth)'
        }
    }

    foreach ($cmd in $commands) {
        $hit = Get-GimSecretFileHit -Cmd $cmd
        if ($null -ne $hit) {
            return [pscustomobject]@{
                rule        = 'secretFile'
                reason      = 'secret-file path'
                alternative = 'never read or write *.secret.json, appsettings.*.secret.json, .env, *.user, *.pubxml.user, **/id_rsa, **/*.pfx, **/*.snk'
            }
        }
    }

    foreach ($cmd in $commands) {
        if (([string]$cmd.name).ToLowerInvariant() -ne 'git') { continue }
        $hit = Get-GimGitDestructiveHit -Cmd $cmd
        if ($null -ne $hit) {
            return [pscustomobject]@{
                rule        = "gitDestructive.$hit"
                reason      = "destructive git variant (git $hit)"
                alternative = 'use the two-turn pattern (read first, then act via gim-apply / version-controller)'
            }
        }
        if (Test-GimGitForcePush -Cmd $cmd) {
            return [pscustomobject]@{
                rule        = 'gitForcePush'
                reason      = 'git force-push variant'
                alternative = 'force push requires explicit user consent; use version-controller (plain git push is allowed)'
            }
        }
        $args = @($cmd.args)
        if ($args.Count -gt 0 -and $args[0] -ceq '-C') {
            return [pscustomobject]@{
                rule        = 'gitC'
                reason      = 'git -C invocation'
                alternative = 'use working_directory / cd instead'
            }
        }
    }

    foreach ($cmd in $commands) {
        if (([string]$cmd.name).ToLowerInvariant() -ne 'dotnet') { continue }
        $hit = Get-GimDotnetExecHit -Cmd $cmd
        if ($null -ne $hit) {
            return [pscustomobject]@{
                rule        = "dotnetExec.$hit"
                reason      = "dotnet execution verb (dotnet $hit)"
                alternative = 'run builds/tests/publish through gim-run (env scrub, timeout)'
            }
        }
    }

    return $null
}

function Get-GimDecision {
    <#
    .SYNOPSIS
        Top-level decision for one hook event: tool file path first (Edit/Write),
        then command text via the central tree.
    .RETURNS
        Decision object { rule; reason; alternative; commandText } or $null
        (pass / nothing to say). commandText is the framed `<cmd>` context:
        the full command for bash, `<tool> <path>` for file tools.
    #>
    param(
        [string]$Command,
        [string]$ToolName,
        [string]$FilePath
    )

    # Non-shell tools (Edit/Write): deny secret-file paths in the tool payload.
    if ($ToolName -in @('edit', 'write') -and -not [string]::IsNullOrEmpty($FilePath)) {
        if (Test-GimSecretFilePath -Value $FilePath) {
            return [pscustomobject]@{
                rule        = 'secretFile'
                reason      = 'secret-file path'
                alternative = 'never read or write *.secret.json, appsettings.*.secret.json, .env, *.user, *.pubxml.user, **/id_rsa, **/*.pfx, **/*.snk'
                commandText = ("{0} {1}" -f $ToolName, $FilePath)
            }
        }
        return $null
    }

    if ([string]::IsNullOrEmpty($Command)) { return $null }
    if (-not $script:CentralLibLoaded) { return $null }

    $tree = Get-GimCommandTree -Command $Command
    $decision = Get-GimDenyForTree -Tree $tree
    if ($null -ne $decision) {
        $decision | Add-Member -NotePropertyName commandText -NotePropertyValue $Command
    }
    return $decision
}

function Format-GimDenyMessage {
    <#
    .SYNOPSIS
        Build the user-locked deny message framing (message contract): the
        command was NOT run; this is the hook's error, not command output.
    .PARAMETER Decision
        Decision object with reason + alternative.
    .PARAMETER CommandText
        The attempted command text (`<cmd>`), truncated to ~120 chars with
        '…' when cut.
    .PARAMETER Harness
        Harness display name — "cursor permission hook" for this file.
    #>
    param($Decision, [string]$CommandText, [string]$Harness)
    $cmd = $CommandText
    if ($cmd.Length -gt 120) { $cmd = $cmd.Substring(0, 120) + '…' }
    return (
        "Your command was not run: command '{0}' was denied by {1}. Reason: {2}. Please try: {3}. This is NOT the command's output — this is the permission hook's error message." -f
        $cmd, $Harness, $Decision.reason, $Decision.alternative
    )
}

function Get-GimDecisionJson {
    <#
    .SYNOPSIS
        Serialize a decision to the hook output contract: `{}` for pass or a
        deny object with permission / agent_message / user_message. Deny
        messages use the user-locked framing (Format-GimDenyMessage).
    #>
    param($Decision)
    if ($null -eq $Decision) { return '{}' }
    $msg = Format-GimDenyMessage -Decision $Decision -CommandText ([string]$Decision.commandText) -Harness 'cursor permission hook'
    $out = [ordered]@{
        permission    = 'deny'
        agent_message = $msg
        user_message  = $msg
    }
    return ($out | ConvertTo-Json -Compress)
}

function Write-GimJsonOut {
    param([string]$Json)
    [Console]::Out.Write($Json)
    if (-not $Json.EndsWith("`n")) { [Console]::Out.WriteLine() }
}

function Get-GimLogSnippet {
    <#
    .SYNOPSIS
        Sanitize a raw stdin string for the file-only log: truncate to ~200
        chars and collapse control characters so the log line stays one line.
    #>
    param([string]$Raw)
    $s = $Raw
    if ($s.Length -gt 200) { $s = $s.Substring(0, 200) + '…' }
    $sb = [System.Text.StringBuilder]::new($s.Length)
    foreach ($ch in $s.ToCharArray()) {
        if ([int]$ch -lt 32 -or [int]$ch -eq 127) { [void]$sb.Append(' ') } else { [void]$sb.Append($ch) }
    }
    return $sb.ToString()
}

function Test-GimIsStringReader {
    <#
    .SYNOPSIS
        True when [Console]::In is (or wraps) a StringReader — the in-process
        Pester SetIn marker. [Console]::In is surfaced as SyncTextReader (a
        TextReader wrapper), so a bare `-is [System.IO.StringReader]` never
        matches; unwrap the private `_in` field and test the underlying
        reader. Fail-safe: ANY error -> $false (a live/interactive reader is
        never treated as a test reader).
    #>
    param($Reader)
    try {
        if ($null -eq $Reader) { return $false }
        if ($Reader -is [System.IO.StringReader]) { return $true }
        $field = $Reader.GetType().GetField('_in', [System.Reflection.BindingFlags]'Instance,NonPublic')
        if ($null -eq $field) { return $false }
        $inner = $field.GetValue($Reader)
        return ($inner -is [System.IO.StringReader])
    } catch {
        return $false
    }
}

function Convert-GimUtf8Payload {
    <#
    .SYNOPSIS
        Decode raw stdin bytes as UTF-8 with StreamReader BOM sniffing —
        identical semantics to the live-spawn pipe read
        (OpenStandardInput + UTF-8 StreamReader). Strips a leading UTF-8 BOM
        (EF BB BF) at byte level and decodes UTF-8 regardless of the console
        codepage. Used by the byte-level BOM test to exercise the decode path
        in-process; the live path applies the same reader construction over
        the stdin pipe.
    #>
    param([byte[]]$Bytes)
    try {
        if ($null -eq $Bytes -or $Bytes.Length -eq 0) { return '' }
        $stream = [System.IO.MemoryStream]::new($Bytes)
        try {
            $reader = [System.IO.StreamReader]::new(
                $stream,
                [System.Text.UTF8Encoding]::new($false)
            )
            $text = $reader.ReadToEnd()
            $reader.Dispose()
            return $text
        } finally {
            $stream.Dispose()
        }
    } catch {
        return ''
    }
}

function Read-GimStdin {
    <#
    .SYNOPSIS
        Read the hook's stdin payload. Live Cursor spawn: the payload arrives
        on the standard-input pipe with a raw UTF-8 BOM (bytes EF BB BF) —
        read via OpenStandardInput + an explicit UTF-8 StreamReader, whose BOM
        sniffing strips the BOM at byte level and decodes UTF-8 regardless of
        the console codepage. In-process test callers ([Console]::SetIn with a
        StringReader) are read via [Console]::In. Fail-open: any read error
        returns ''.
    .DESCRIPTION
        Two live bugs recorded in docs/agents/0001-harness-permission-findings.md
        §11 (v2 regression, 2026-08-20):
        - [Console]::In.ReadToEnd() is EMPTY under the live Cursor spawn —
          the payload is on the standard-input pipe, not [Console]::In.
        - Reading [Console]::In on the live path decodes through the console
          codepage, so a raw UTF-8 BOM arrives as mojibake (e.g. '´╗┐'), not
          U+FEFF; the U+FEFF strip then misses and ConvertFrom-Json fails.
        Order: (1) StringReader-backed [Console]::In (in-process Pester SetIn)
        -> read it (works regardless of redirect state); (2) redirected stdin
        -> OpenStandardInput + UTF-8 StreamReader (the ONLY live path; NEVER
        [Console]::In); (3) otherwise ''. Defensively strip a single leading
        U+FEFF after any read (belt+braces — covers any path where the BOM
        survived decode). JSON-only stdout + fail-open catch-all
        (Invoke-GimHook).
    #>
    $raw = ''

    # Path 1 — in-process test callers: [Console]::SetIn + StringReader
    # (surfaced as SyncTextReader). Detected by unwrapping the reader (never
    # by a bare -is, which misses the wrapper). Works with redirected OR
    # console stdin, so the Pester SetIn round-trips pass under any runner.
    if (Test-GimIsStringReader -Reader ([Console]::In)) {
        try {
            $raw = [Console]::In.ReadToEnd()
        } catch {
            $raw = ''
        }
    } elseif ([Console]::IsInputRedirected) {
        # Path 2 — LIVE Cursor spawn: payload piped on standard input with a
        # raw UTF-8 BOM. NEVER read [Console]::In here (empty under spawn;
        # mojibake on BOM). StreamReader's BOM sniffing strips EF BB BF at
        # byte level and the explicit UTF-8 encoding decodes the payload
        # regardless of the console codepage.
        try {
            $reader = [System.IO.StreamReader]::new(
                [Console]::OpenStandardInput(),
                [System.Text.UTF8Encoding]::new($false)
            )
            $raw = $reader.ReadToEnd()
            $reader.Dispose()
        } catch {
            $raw = ''
        }
    }
    # else — interactive console (no StringReader, no redirect): '' — a read
    # would block on EOF. This is never the hook's execution context.

    # Defensive — strip a single leading U+FEFF if the BOM survived decode
    # (covers any path/encoding edge).
    if (-not [string]::IsNullOrEmpty($raw) -and $raw[0] -eq [char]0xFEFF) {
        $raw = $raw.Substring(1)
    }
    return $raw
}

function Invoke-GimHook {
    <#
    .SYNOPSIS
        Read the stdin JSON payload, evaluate the decision, return the output
        JSON string. Failure-mode split (user-locked 2026-08-20):
          - EMPTY payload (nothing received) -> '{}' (fail-open — nothing to
            decide).
          - Non-empty payload that cannot be parsed -> generic deny in the
            deny-message contract framing ("the permission hook could not
            interpret the permission payload (parse failed) — check
            %TEMP%\cursor\gim-permissions.log") — the hook never silently
            passes on a payload it could not interpret.
        Catch-all: ANY internal error -> '{}' (a broken hook must not block
        the IDE). Diagnostics: every event logs stdin_len=<n> parse=ok|fail
        (file-only log); unparseable stdin logs a sanitized raw snippet
        <= 200 chars. Read bugs (live Cursor spawn): see Read-GimStdin —
        [Console]::In is empty under the live spawn and mojibakes a raw
        UTF-8 BOM (docs/agents/0001-harness-permission-findings.md §11).
    #>
    try {
        $raw = Read-GimStdin
        $rawLen = if ($null -eq $raw) { 0 } else { $raw.Length }
        $rawText = if ($null -eq $raw) { '' } else { [string]$raw }

        $parseOk = $false
        $payload = $null
        if (-not [string]::IsNullOrWhiteSpace($rawText)) {
            # strip a leading UTF-8 BOM char (U+FEFF), then surrounding whitespace
            if ($rawText[0] -eq [char]0xFEFF) { $rawText = $rawText.Substring(1) }
            $rawText = $rawText.Trim()
        }
        if (-not [string]::IsNullOrEmpty($rawText)) {
            try {
                $payload = $rawText | ConvertFrom-Json -Depth 8
                $parseOk = $true
            } catch {
                $payload = $null
            }
        }

        if (-not $parseOk) {
            Write-GimLog ("stdin_len={0} parse=fail raw={1}" -f $rawLen, (Get-GimLogSnippet -Raw $rawText))
            if ([string]::IsNullOrEmpty($rawText)) {
                # Empty payload — nothing received, nothing to decide: fail-open.
                return '{}'
            }
            # Non-empty payload that cannot be parsed: fail-closed — generic
            # deny in the deny-message contract framing (no rule-specific
            # alternative exists for an unreadable payload).
            $snippet = Get-GimLogSnippet -Raw $rawText
            if ($snippet.Length -gt 120) { $snippet = $snippet.Substring(0, 120) + '…' }
            $logPath = Join-Path $env:TEMP 'cursor\gim-permissions.log'
            $msg = ("Your command was not run: command '{0}' was denied by cursor permission hook. Reason: the permission hook could not interpret the permission payload (parse failed) — check {1}. This is NOT the command's output — this is the permission hook's error message." -f
                $snippet, $logPath)
            $out = [ordered]@{
                permission    = 'deny'
                agent_message = $msg
                user_message  = $msg
            }
            return ($out | ConvertTo-Json -Compress)
        }

        $event = [string](Get-GimProp -Obj $payload -Name 'hook_event_name')
        $tool = [string](Get-GimProp -Obj $payload -Name 'tool_name')

        $command = Get-GimCommandText -Payload $payload
        $decision = $null
        if ($event -eq 'preToolUse' -or $event -eq 'beforeShellExecution') {
            $filePath = Get-GimToolFilePath -Payload $payload
            $decision = Get-GimDecision -Command $command -ToolName $tool -FilePath $filePath
        }

        $rule = if ($null -ne $decision) { $decision.rule } else { 'pass' }
        Write-GimLog ("event={0} tool={1} rule={2} cmd={3} stdin_len={4} parse={5}" -f
            $event, $tool, $rule, ($command -replace '\s+', ' '), $rawLen, $(if ($parseOk) { 'ok' } else { 'fail' }))

        return (Get-GimDecisionJson -Decision $decision)
    } catch {
        Write-GimLog ("ERROR {0}" -f $_.Exception.Message)
        return '{}'
    }
}

# ---------------------------------------------------------------------------
# Entry point — only when run as a script (not dot-sourced by tests).
# ---------------------------------------------------------------------------
if ($MyInvocation.InvocationName -ne '.') {
    try {
        $out = Invoke-GimHook
        Write-GimJsonOut -Json $out
    } catch {
        Write-GimJsonOut -Json '{}'
    }
    exit 0
}
