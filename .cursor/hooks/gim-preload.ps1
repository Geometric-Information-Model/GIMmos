# gim-preload.ps1 — Cursor per-agent context preload hook (ADR 0009).
#
# Two roles, one script, wired in `.cursor/hooks.json`:
#
#   sessionStart  -> {"additional_context":"<gim-preload ...>"} with the
#                    orchestrator preload (prefs + role skills). The default
#                    Cursor chat is implicitly the orchestrator, so this hook
#                    never tries to detect an agent name or type.
#   preToolUse    -> matcher "Task": reads tool_input.prompt and
#   (Task only)      tool_input.subagent_type (fallback subagentType), resolves
#                    that agent's shared config, and, ONLY when it actually
#                    injects, returns {"permission":"allow","updated_input":{...}}
#                    with the preload block prepended to the prompt. Every
#                    other tool_input field is copied through unchanged.
#
# Pass-through vs rewrite: this hook is not a permission grant. Every no-op
# path — non-Task, unknown/missing agent, missing/non-string prompt, marker
# block already at the prompt start, unreadable config, or ANY internal
# error — returns `{}`, a silent pass-through with no rewrite. `allow` appears
# ONLY alongside
# `updated_input` on a real injection. The permission hook owns deny; this
# hook never decides allow/deny.
#
# Fail-open contract: all of the above degrade to valid `{}` JSON — never
# invalid JSON, never an allow-only object, and never a blocked subagent.
# The sessionStart side also degrades to `{}`.
#
# Shared config: `.agents/agent-preload.json` (the same file the OpenCode
# plugin reads). Skills resolve under `.agents/skills`, then `.opencode/skills`
# and `.claude/skills`; `files` are relative to the repo root (or absolute).
# Skills come first, then files; missing entries are skipped.
#
# Logging is file-only: %TEMP%\cursor\gim-preload.log (capped ~5 MB). Field
# discovery needs the observed tool_input key names, not the user's prompt:
# never log prompt text, user_email, or account ids.
#
# JSON-only stdout. PowerShell 5.1 and 7 compatible: no `??`, `?.`, ternary,
# or `-AsHashtable`.

param(
    [switch]$NoMain
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:GimPreloadHeaderDefault = 'Reference material, preloaded. Attached to this agent''s context.'
$script:GimPreloadSkillRoots = @('.agents\skills', '.opencode\skills', '.claude\skills')
$script:GimPreloadPassJson = '{}'
# Idempotence marker: a COMPLETE block ANCHORED at the prompt start, so the
# marker is only the block this hook prepends. A prompt that merely mentions
# the literal `<gim-preload` (e.g. docs or a task description) still gets
# injected, and so does a prompt that quotes a complete block later in its
# text — only an already-injected block suppresses the rewrite.
$script:GimPreloadBlockPattern = '^\s*<gim-preload\b[^>]*>[\s\S]*?</gim-preload>'

# Repo root captured at load time. Tests pass -Root explicitly; the live hook
# uses this default (the script lives at <repo>\.cursor\hooks\).
$script:GimPreloadRepoRootDefault = $null
try {
    if (-not [string]::IsNullOrEmpty($PSCommandPath)) {
        $hookDir = Split-Path -Parent $PSCommandPath
        $script:GimPreloadRepoRootDefault = [System.IO.Path]::GetFullPath(
            (Join-Path (Join-Path $hookDir '..') '..')
        )
    }
} catch {
    $script:GimPreloadRepoRootDefault = $null
}

# ---------------------------------------------------------------------------
# Logging (file-only, capped ~5 MB)
# ---------------------------------------------------------------------------
# Append one metadata line to the capped file log; logging never fails the hook.
function Write-GimPreloadLog {
    param([string]$Line)
    try {
        $logDir = Join-Path $env:TEMP 'cursor'
        $logFile = Join-Path $logDir 'gim-preload.log'
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
# Payload helpers (PSCustomObject from JSON, IDictionary from tests)
# ---------------------------------------------------------------------------
# Read one property from either a PSCustomObject payload or an IDictionary.
function Get-GimPreloadProp {
    param($Obj, [string]$Name)
    if ($null -eq $Obj) { return $null }
    if ($Obj -is [System.Collections.IDictionary]) {
        if ($Obj.Contains($Name)) { return $Obj[$Name] }
        return $null
    }
    if ($Obj.PSObject.Properties[$Name]) { return $Obj.$Name }
    return $null
}

# List property names for log field-discovery, from an object or a dictionary.
function Get-GimPreloadKeys {
    param($Obj)
    if ($null -eq $Obj) { return @() }
    if ($Obj -is [System.Collections.IDictionary]) { return @($Obj.Keys) }
    $names = New-Object System.Collections.ArrayList
    foreach ($p in $Obj.PSObject.Properties) { [void]$names.Add([string]$p.Name) }
    return @($names)
}

# ---------------------------------------------------------------------------
# Config + resolution
# ---------------------------------------------------------------------------
# Resolve the repo root: explicit argument, else script location, else cwd.
function Get-GimPreloadRoot {
    param([string]$Root)
    if (-not [string]::IsNullOrWhiteSpace($Root)) {
        return [System.IO.Path]::GetFullPath($Root)
    }
    if (-not [string]::IsNullOrEmpty($script:GimPreloadRepoRootDefault)) {
        return $script:GimPreloadRepoRootDefault
    }
    return [System.IO.Path]::GetFullPath((Get-Location).Path)
}

function Read-GimPreloadConfig {
    <#
    .SYNOPSIS
        Read the shared preload map. Returns $null when absent/unreadable, so
        every caller fails open.
    #>
    param([string]$Root)
    $path = Join-Path $Root '.agents\agent-preload.json'
    if (-not [System.IO.File]::Exists($path)) { return $null }
    try {
        $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        $config = $text | ConvertFrom-Json
        if ($null -eq $config) { return $null }

        # Parity with the OpenCode adapter: ONLY the JSON literal `false`
        # disables. The strings "false"/"0" and the number 0 stay enabled, so a
        # hand-edited config cannot disable the hook by accident.
        $enabled = $true
        $enabledValue = Get-GimPreloadProp -Obj $config -Name 'enabled'
        if ($enabledValue -is [bool] -and -not $enabledValue) { $enabled = $false }

        $header = [string](Get-GimPreloadProp -Obj $config -Name 'header')
        if ([string]::IsNullOrWhiteSpace($header)) { $header = $script:GimPreloadHeaderDefault }

        $agents = Get-GimPreloadProp -Obj $config -Name 'agents'
        if ($null -eq $agents) { $agents = [pscustomobject]@{} }

        return [pscustomobject]@{
            Enabled = $enabled
            Header  = $header
            Agents  = $agents
        }
    } catch {
        Write-GimPreloadLog ('config-error path={0} error={1}' -f $path, $_.Exception.Message)
        return $null
    }
}

# Resolve a bare skill name to its SKILL.md across the repo-local skill roots.
function Resolve-GimPreloadSkill {
    param([string]$Name, [string]$Root)
    if ([string]::IsNullOrWhiteSpace($Name)) { return $null }
    $value = $Name.Trim()
    foreach ($relative in $script:GimPreloadSkillRoots) {
        $candidate = Join-Path (Join-Path $Root $relative) (Join-Path $value 'SKILL.md')
        if ([System.IO.File]::Exists($candidate)) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }
    return $null
}

# Resolve a file entry: absolute, or relative to the repo root; missing -> $null.
function Resolve-GimPreloadFile {
    param([string]$Entry, [string]$Root)
    if ([string]::IsNullOrWhiteSpace($Entry)) { return $null }
    $value = $Entry.Trim()
    $full = $value
    if (-not [System.IO.Path]::IsPathRooted($value)) {
        $full = Join-Path $Root $value
    }
    if ([System.IO.File]::Exists($full)) {
        return [System.IO.Path]::GetFullPath($full)
    }
    return $null
}

# Drop a leading YAML frontmatter block; a body horizontal rule survives.
function Strip-GimPreloadFrontmatter {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    if (-not $Text.StartsWith('---')) { return $Text }
    $match = [regex]::Match($Text, '^---\r?\n[\s\S]*?\r?\n---[ \t]*\r?\n?')
    if ($match.Success) { return $Text.Substring($match.Length) }
    return $Text
}

# Show a path project-relative with forward slashes; outside the root unchanged.
function Get-GimPreloadRelativePath {
    param([string]$Path, [string]$Root)
    $shown = ([string]$Path).Replace('\', '/')
    $base = ([string]$Root).Replace('\', '/').TrimEnd('/')
    if ([string]::IsNullOrEmpty($base)) { return $shown }
    if ($shown.ToLowerInvariant() -eq $base.ToLowerInvariant()) { return '.' }
    $prefix = $base.ToLowerInvariant() + '/'
    if (-not $shown.ToLowerInvariant().StartsWith($prefix)) { return $Path }
    return $shown.Substring($base.Length + 1)
}

# Read one preload file as UTF-8, strip frontmatter, trim trailing whitespace.
function Read-GimPreloadBody {
    param([string]$Path)
    try {
        $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
        return (Strip-GimPreloadFrontmatter -Text $text).TrimEnd()
    } catch {
        Write-GimPreloadLog ('read-error path={0} error={1}' -f $Path, $_.Exception.Message)
        return $null
    }
}

function Build-GimPreloadBlock {
    <#
    .SYNOPSIS
        Build the same `<gim-preload>` shape the OpenCode plugin injects.
    #>
    param([string]$Agent, $Files, [string]$Header)
    if ($null -eq $Files) { return '' }
    $items = @($Files)
    if ($items.Count -eq 0) { return '' }
    if ([string]::IsNullOrWhiteSpace($Header)) { $Header = $script:GimPreloadHeaderDefault }

    $parts = New-Object System.Collections.ArrayList
    [void]$parts.Add(('<gim-preload agent="{0}" files="{1}">' -f $Agent, $items.Count))
    [void]$parts.Add($Header)
    [void]$parts.Add('')
    foreach ($file in $items) {
        [void]$parts.Add(('<gim-file path="{0}">' -f $file.Path))
        [void]$parts.Add([string]$file.Body)
        [void]$parts.Add('</gim-file>')
        [void]$parts.Add('')
    }
    [void]$parts.Add('</gim-preload>')
    return ($parts -join "`n")
}

function Get-GimPreloadBlock {
    <#
    .SYNOPSIS
        Resolve one agent's skills + files and build its block. Returns
        { Block; Files; Missing }. Block is '' when the agent is unknown,
        disabled, or nothing resolved.
    #>
    param([string]$Agent, [string]$Root)

    $result = [pscustomobject]@{
        Block   = ''
        Files   = @()
        Missing = @()
    }
    if ([string]::IsNullOrWhiteSpace($Agent)) { return $result }

    $config = Read-GimPreloadConfig -Root $Root
    if ($null -eq $config -or -not $config.Enabled) { return $result }

    $spec = Get-GimPreloadProp -Obj $config.Agents -Name $Agent
    if ($null -eq $spec) { return $result }

    $skills = @()
    $skillValue = Get-GimPreloadProp -Obj $spec -Name 'skills'
    if ($null -ne $skillValue) { $skills = @($skillValue) }

    $entries = @()
    $fileValue = Get-GimPreloadProp -Obj $spec -Name 'files'
    if ($null -ne $fileValue) { $entries = @($fileValue) }

    if ($skills.Count -eq 0 -and $entries.Count -eq 0) { return $result }

    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    $files = New-Object System.Collections.ArrayList
    $missing = New-Object System.Collections.ArrayList

    $add = {
        param([string]$Path)
        if (-not $seen.Add($Path)) { return }
        $body = Read-GimPreloadBody -Path $Path
        if ($null -eq $body) { return }
        [void]$files.Add([pscustomobject]@{
            Path = (Get-GimPreloadRelativePath -Path $Path -Root $Root)
            Body = $body
        })
    }

    foreach ($name in $skills) {
        if (-not ($name -is [string])) { continue }
        $path = Resolve-GimPreloadSkill -Name ([string]$name) -Root $Root
        if ($null -eq $path) {
            [void]$missing.Add([string]$name)
            continue
        }
        & $add $path
    }
    foreach ($entry in $entries) {
        if (-not ($entry -is [string])) { continue }
        $path = Resolve-GimPreloadFile -Entry ([string]$entry) -Root $Root
        if ($null -eq $path) {
            [void]$missing.Add([string]$entry)
            continue
        }
        & $add $path
    }

    $result.Files = @($files)
    $result.Missing = @($missing)
    $result.Block = Build-GimPreloadBlock -Agent $Agent -Files $result.Files -Header $config.Header
    return $result
}

function ConvertTo-GimPreloadJson {
    <#
    .SYNOPSIS
        Compact JSON with every non-ASCII code unit written as \uXXXX.
        Cursor's Windows runner decodes native hook stdout with the console
        code page, so a raw UTF-8 byte in that stream is not preserved.
    #>
    param($Value)
    $json = $Value | ConvertTo-Json -Depth 50 -Compress
    $sb = New-Object System.Text.StringBuilder $json.Length
    foreach ($ch in $json.ToCharArray()) {
        $n = [int]$ch
        if ($n -gt 127) { [void]$sb.Append(('\u{0:x4}' -f $n)) }
        else { [void]$sb.Append($ch) }
    }
    return $sb.ToString()
}

# ---------------------------------------------------------------------------
# Hook handlers
# ---------------------------------------------------------------------------
function Invoke-GimPreloadSessionStart {
    <#
    .SYNOPSIS
        sessionStart: always the orchestrator preload (the default Cursor chat
        is implicitly the orchestrator). Any failure -> `{}`.
    #>
    param($Payload, [string]$Root)
    try {
        $found = Get-GimPreloadBlock -Agent 'gim-orchestrator' -Root $Root
        $keys = @(Get-GimPreloadKeys -Obj $Payload)
        Write-GimPreloadLog ('event=sessionStart agent=gim-orchestrator payload_keys={0} files={1} bytes={2} missing={3}' -f
            ($keys -join ','), @($found.Files).Count, ([string]$found.Block).Length, (@($found.Missing) -join ','))
        if ([string]::IsNullOrEmpty([string]$found.Block)) { return $script:GimPreloadPassJson }
        $out = [ordered]@{
            additional_context = [string]$found.Block
        }
        return (ConvertTo-GimPreloadJson $out)
    } catch {
        Write-GimPreloadLog ('event=sessionStart error={0}' -f $_.Exception.Message)
        return $script:GimPreloadPassJson
    }
}

function Invoke-GimPreloadPreToolUse {
    <#
    .SYNOPSIS
        preToolUse (Task only): prepend the subagent's preload to
        tool_input.prompt via updated_input. Every other tool_input field is
        copied through. Any no-op/failure -> `{}` (pass-through, no rewrite).
        `{"permission":"allow","updated_input":...}` is returned only when a
        block is actually injected; this is not a permission grant.
    #>
    param($Payload, [string]$Root)
    $tool = [string](Get-GimPreloadProp -Obj $Payload -Name 'tool_name')
    $subagent = ''

    try {
        if ($tool -ne 'Task') {
            Write-GimPreloadLog ('event=preToolUse tool={0} skip=not-task' -f $tool)
            return $script:GimPreloadPassJson
        }

        $toolInput = Get-GimPreloadProp -Obj $Payload -Name 'tool_input'
        if ($toolInput -is [string]) {
            try { $toolInput = $toolInput | ConvertFrom-Json } catch { $toolInput = $null }
        }
        $keys = @(Get-GimPreloadKeys -Obj $toolInput)

        $subagentValue = Get-GimPreloadProp -Obj $toolInput -Name 'subagent_type'
        if ($null -eq $subagentValue) {
            $subagentValue = Get-GimPreloadProp -Obj $toolInput -Name 'subagentType'
        }
        if ($subagentValue -is [string]) { $subagent = [string]$subagentValue }

        $promptValue = Get-GimPreloadProp -Obj $toolInput -Name 'prompt'
        $promptLength = 0
        if ($promptValue -is [string]) { $promptLength = ([string]$promptValue).Length }

        if ([string]::IsNullOrWhiteSpace($subagent)) {
            Write-GimPreloadLog ('event=preToolUse tool=Task subagent= prompt_len={0} keys={1} skip=no-subagent' -f
                $promptLength, ($keys -join ','))
            return $script:GimPreloadPassJson
        }
        if (-not ($promptValue -is [string]) -or [string]::IsNullOrEmpty([string]$promptValue)) {
            Write-GimPreloadLog ('event=preToolUse tool=Task subagent={0} prompt_len={1} keys={2} skip=no-prompt' -f
                $subagent, $promptLength, ($keys -join ','))
            return $script:GimPreloadPassJson
        }

        $prompt = [string]$promptValue
        if ([regex]::IsMatch($prompt, $script:GimPreloadBlockPattern)) {
            Write-GimPreloadLog ('event=preToolUse tool=Task subagent={0} prompt_len={1} keys={2} skip=already-preloaded' -f
                $subagent, $promptLength, ($keys -join ','))
            return $script:GimPreloadPassJson
        }

        $found = Get-GimPreloadBlock -Agent $subagent -Root $Root
        $block = [string]$found.Block
        Write-GimPreloadLog ('event=preToolUse tool=Task subagent={0} prompt_len={1} keys={2} files={3} bytes={4} missing={5}' -f
            $subagent, $promptLength, ($keys -join ','), @($found.Files).Count, $block.Length, (@($found.Missing) -join ','))
        if ([string]::IsNullOrEmpty($block)) { return $script:GimPreloadPassJson }

        # Copy every tool_input property, replace only prompt.
        $updated = [ordered]@{}
        if ($toolInput -is [System.Collections.IDictionary]) {
            foreach ($key in $toolInput.Keys) { $updated[[string]$key] = $toolInput[$key] }
        } else {
            foreach ($property in $toolInput.PSObject.Properties) {
                $updated[[string]$property.Name] = $property.Value
            }
        }
        $updated['prompt'] = $block + "`n`n" + $prompt

        $out = [ordered]@{
            permission    = 'allow'
            updated_input = $updated
        }
        return (ConvertTo-GimPreloadJson $out)
    } catch {
        Write-GimPreloadLog ('event=preToolUse tool={0} subagent={1} error={2}' -f
            $tool, $subagent, $_.Exception.Message)
        return $script:GimPreloadPassJson
    }
}

# Classify a payload as sessionStart, preToolUse, or '' (unknown/ignored).
function Get-GimPreloadEventKind {
    param($Payload)
    $event = [string](Get-GimPreloadProp -Obj $Payload -Name 'hook_event_name')
    if ($event -eq 'sessionStart') { return 'sessionStart' }
    if ($event -eq 'preToolUse' -or $event -eq 'beforeShellExecution') { return 'preToolUse' }
    if ($event -eq 'subagentStart') { return '' }
    $tool = Get-GimPreloadProp -Obj $Payload -Name 'tool_name'
    if ($null -ne $tool) { return 'preToolUse' }
    return ''
}

function Invoke-GimPreloadHook {
    <#
    .SYNOPSIS
        Parse one stdin payload and return the one JSON document Cursor expects.
        Garbage/empty/unknown payloads fail open as `{}` (pass-through).
    #>
    param([string]$Raw, [string]$Root)
    if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Get-GimPreloadRoot }

    $payload = $null
    try {
        $text = [string]$Raw
        if (-not [string]::IsNullOrEmpty($text)) {
            if ($text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
            $text = $text.Trim()
        }
        if ([string]::IsNullOrWhiteSpace($text)) { return $script:GimPreloadPassJson }
        $payload = $text | ConvertFrom-Json
    } catch {
        Write-GimPreloadLog ('event=? error=parse input_len={0}' -f ([string]$Raw).Length)
        return $script:GimPreloadPassJson
    }

    $kind = Get-GimPreloadEventKind -Payload $payload
    if ($kind -eq 'sessionStart') {
        return (Invoke-GimPreloadSessionStart -Payload $payload -Root $Root)
    }
    if ($kind -eq 'preToolUse') {
        return (Invoke-GimPreloadPreToolUse -Payload $payload -Root $Root)
    }
    Write-GimPreloadLog ('event=? payload_keys={0} skip=unknown-event' -f
        (@(Get-GimPreloadKeys -Obj $payload) -join ','))
    return $script:GimPreloadPassJson
}

function Test-GimPreloadIsStringReader {
    <#
    .SYNOPSIS
        True when [Console]::In is (or wraps) a StringReader — the in-process
        Pester SetIn marker. [Console]::In is surfaced as SyncTextReader (a
        TextReader wrapper), so a bare `-is [System.IO.StringReader]` never
        matches; unwrap the private `_in` field and test the underlying
        reader. Fail-safe: ANY error -> $false (a live/interactive reader is
        never treated as a test reader). Same pattern as the permission hook.
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

function Read-GimPreloadStdin {
    <#
    .SYNOPSIS
        Read the hook payload as UTF-8.
    .DESCRIPTION
        Mirrors Read-GimStdin in the sibling permission hook:
        1. StringReader-backed [Console]::In (in-process Pester SetIn) -> read
           it directly; works regardless of redirect state.
        2. [Console]::IsInputRedirected -> OpenStandardInput + an explicit
           UTF-8 StreamReader (the live Cursor spawn path; BOM sniffing strips
           EF BB BF at byte level). NEVER read [Console]::In here.
        3. Otherwise '' — an interactive console read would block on EOF, and
           this is never the hook's execution context.
        A single leading U+FEFF is stripped defensively after any read.
    #>
    $raw = ''

    if (Test-GimPreloadIsStringReader -Reader ([Console]::In)) {
        try {
            $raw = [Console]::In.ReadToEnd()
        } catch {
            $raw = ''
        }
    } elseif ([Console]::IsInputRedirected) {
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

    if (-not [string]::IsNullOrEmpty($raw) -and $raw[0] -eq [char]0xFEFF) {
        $raw = $raw.Substring(1)
    }
    return $raw
}

function Invoke-GimPreloadMain {
    <#
    .SYNOPSIS
        Read one stdin payload and return the one JSON document Cursor expects.
        Kept as a function so in-process tests can exercise the full entry
        path with [Console]::SetIn (no pwsh spawn).
    #>
    param([string]$Root)
    $json = $script:GimPreloadPassJson
    try {
        $stdin = Read-GimPreloadStdin
        $json = Invoke-GimPreloadHook -Raw $stdin -Root (Get-GimPreloadRoot -Root $Root)
    } catch {
        Write-GimPreloadLog ('event=? fatal={0}' -f $_.Exception.Message)
        $json = $script:GimPreloadPassJson
    }
    if ([string]::IsNullOrWhiteSpace($json)) { $json = $script:GimPreloadPassJson }
    return $json
}

function Write-GimPreloadJsonOut {
    <#
    .SYNOPSIS
        Write the one JSON document to stdout with a trailing newline and an
        explicit flush (parity with the sibling permission hook).
    #>
    param([string]$Json)
    if ([string]::IsNullOrWhiteSpace($Json)) { $Json = $script:GimPreloadPassJson }
    [Console]::Out.WriteLine($Json)
    [Console]::Out.Flush()
}

# ---------------------------------------------------------------------------
# Entry point — only when run as a script (not dot-sourced by tests, and not
# with -NoMain).
# ---------------------------------------------------------------------------
if (-not $NoMain -and $MyInvocation.InvocationName -ne '.') {
    $json = Invoke-GimPreloadMain
    Write-GimPreloadJsonOut -Json $json
    exit 0
}
