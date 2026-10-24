# GimFleetText.ps1 - redaction and text/report helpers for the fleet tier
# (ADR 0008): email and Atlassian account-id redaction, docs-index parsing
# (pure, fixture-friendly), open-PR file extraction, and the compact auth /
# repo / docs-index summary used by gim-preflight.

$script:GimFleetDocFamilies = @(
    [pscustomobject]@{ name = 'plans';  path = 'docs/plans';  pattern = '^docs/plans/(\d{4})-[^/]+\.plan\.md$' }
    [pscustomobject]@{ name = 'adr';    path = 'docs/adr';    pattern = '^docs/adr/(\d{4})-[^/]+\.md$' }
    [pscustomobject]@{ name = 'rfcs';   path = 'docs/rfcs';   pattern = '^docs/rfcs/(\d{4})-[^/]+\.md$' }
    [pscustomobject]@{ name = 'guides'; path = 'docs/guides'; pattern = '^docs/guides/(\d{4})-[^/]+\.md$' }
)

function Redact-GimFleetText {
    <#
    .SYNOPSIS
        Redact email addresses and Atlassian account ids from text that may be
        forwarded to the orchestrator (auth errors, fetch errors).
    .DESCRIPTION
        Replaces email addresses with <redacted-email>, accountId assignments
        and bare 24-hex account ids / UUID-shaped ids with
        <redacted-account>. Git SHAs (40 hex) and ordinary prose survive.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text) { return '' }
    if ($Text -eq '') { return '' }
    $t = [regex]::Replace($Text, "[A-Za-z0-9._%+'-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", '<redacted-email>')
    $t = [regex]::Replace($t, '(?i)\baccount[ _-]?id\b\s*[:=]\s*"?[A-Za-z0-9:_-]{6,}"?', 'accountId=<redacted-account>')
    $t = [regex]::Replace($t, '\b[0-9a-fA-F]{24}\b', '<redacted-account>')
    $t = [regex]::Replace($t, '\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b', '<redacted-account>')
    return $t
}

function ConvertTo-GimFleetSingleLine {
    <#
    .SYNOPSIS
        Collapse whitespace/newlines into one bounded line for reports.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text,
        [int]$MaxLength = 220
    )
    if ($null -eq $Text) { return '' }
    $line = ($Text -replace '\r?\n', ' ') -replace '\s+', ' '
    $line = $line.Trim()
    if ($line.Length -gt $MaxLength) { $line = $line.Substring(0, $MaxLength) + '...' }
    return $line
}

function ConvertTo-GimWrapupIsoTimestamp {
    <#
    .SYNOPSIS
        Normalize a wrapup timestamp to the millisecond UTC shape written to
        gather.json and responses.result.json.
    #>
    param([AllowNull()]$Value)
    if ($null -eq $Value) { return '' }
    if ($Value -is [DateTime]) { return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ') }
    return [string]$Value
}

function Test-GimFleetExcludedDocPath {
    <#
    .SYNOPSIS
        True when a repo-relative doc path is agent-only (excluded from the
        product docs index): .agents/**, .cursor/**, docs/plans/agents/**,
        docs/adr/agents/**, or any filename containing -AGENTS-.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )
    $p = ($Path -replace '\\', '/').TrimStart('/')
    if ($p -match '(^|/)(\.agents|\.cursor)/') { return $true }
    if ($p -match '^docs/(plans|adr)/agents/') { return $true }
    $leaf = ($p -split '/')[-1]
    if ($leaf -match '-AGENTS-') { return $true }
    return $false
}

function Get-GimFleetDocFamily {
    <#
    .SYNOPSIS
        Look up one fixed docs-index family definition by name.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    foreach ($f in $script:GimFleetDocFamilies) {
        if ($f.name -eq $Name) { return $f }
    }
    Write-GimError "unknown docs family: '$Name'"
}

function Get-GimFleetDocNumber {
    <#
    .SYNOPSIS
        Return the four-digit prefix for a repo-relative path in a family, or
        0 when the path does not match the family (or is agent-only).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Family,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Path
    )
    $f = Get-GimFleetDocFamily -Name $Family
    $p = ($Path -replace '\\', '/')
    if (Test-GimFleetExcludedDocPath -Path $p) { return 0 }
    $m = [regex]::Match($p, $f.pattern)
    if (-not $m.Success) { return 0 }
    return [int]$m.Groups[1].Value
}

function Get-GimFleetDocMax {
    <#
    .SYNOPSIS
        Maximum four-digit prefix across a file list for one family (0 when
        none match).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Family,
        [AllowNull()]
        [AllowEmptyCollection()]
        [string[]]$Files = @()
    )
    $max = 0
    if ($null -eq $Files) { return 0 }
    foreach ($file in $Files) {
        if ($null -eq $file) { continue }
        $n = Get-GimFleetDocNumber -Family $Family -Path ([string]$file)
        if ($n -gt $max) { $max = $n }
    }
    return $max
}

function Get-GimFleetDocsIndex {
    <#
    .SYNOPSIS
        Build one family's { ok, local, remote, open_pr, max, next } record.
    .DESCRIPTION
        Pure and fixture-friendly. LocalFiles / RemoteFiles are repo-relative
        path lists (agent-only paths ignored). An absent family is ok=true,
        max 0, next 1. When -Failed is set (remote ls-tree, open-PR source, or
        the fetch it depends on failed), the record is invalid: ok=false,
        remote/open_pr/max/next are null, and error carries the reason. A
        failed source never leaves a stale next for the orchestrator to use.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Family,
        [AllowNull()]
        [AllowEmptyCollection()]
        [string[]]$LocalFiles = @(),
        [AllowNull()]
        [AllowEmptyCollection()]
        [string[]]$RemoteFiles = @(),
        [AllowNull()]
        [AllowEmptyCollection()]
        [string[]]$OpenPrFiles = $null,
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Error = $null,
        [bool]$Failed = $false
    )
    $localMax = Get-GimFleetDocMax -Family $Family -Files $LocalFiles
    if ($Failed) {
        $err = $Error
        if (-not $err) { $err = 'docs index unavailable' }
        return [pscustomobject]@{
            ok      = $false
            local   = $localMax
            remote  = $null
            open_pr = $null
            max     = $null
            next    = $null
            error   = $err
        }
    }
    $remoteMax = Get-GimFleetDocMax -Family $Family -Files $RemoteFiles
    $openPrMax = $null
    if ($null -ne $OpenPrFiles) {
        $openPrMax = Get-GimFleetDocMax -Family $Family -Files $OpenPrFiles
    }
    $max = $localMax
    if ($remoteMax -gt $max) { $max = $remoteMax }
    if ($null -ne $openPrMax -and $openPrMax -gt $max) { $max = $openPrMax }
    return [pscustomobject]@{
        ok      = $true
        local   = $localMax
        remote  = $remoteMax
        open_pr = $openPrMax
        max     = $max
        next    = $max + 1
        error   = $Error
    }
}

function Get-GimFleetOpenPrFilePaths {
    <#
    .SYNOPSIS
        Parse open-PR changed-file output into repo-relative paths.
    .DESCRIPTION
        The live gh call uses a path-only `--jq '.[].files[].path'` form, so
        the normal input is one path per line. A structured JSON array
        (`[{ number, files: [...] }]`) is also accepted for fixtures, where
        files may be strings or { path = ... } objects. Output carrying the
        runner truncation marker or unparseable JSON throws, so the caller can
        mark the docs index failed instead of returning a silent stale value.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    $paths = New-Object System.Collections.Generic.List[string]
    if ($null -eq $Text) { return @() }
    if ($Text -match '# gim: output truncated') {
        throw 'gh pr list output was truncated'
    }
    $trim = $Text.Trim()
    if ($trim -eq '') { return @() }
    if ($trim.StartsWith('[') -or $trim.StartsWith('{')) {
        try {
            $prs = @($trim | ConvertFrom-Json)
        } catch {
            throw 'gh pr list returned unparseable JSON'
        }
        foreach ($pr in $prs) {
            if ($null -eq $pr) { continue }
            $files = $null
            if ($pr -is [System.Collections.IDictionary]) {
                if ($pr.Contains('files')) { $files = $pr['files'] }
            } else {
                $prop = $pr.PSObject.Properties['files']
                if ($null -ne $prop) { $files = $prop.Value }
            }
            if ($null -eq $files) { continue }
            foreach ($file in @($files)) {
                if ($null -eq $file) { continue }
                if ($file -is [string]) {
                    [void]$paths.Add($file)
                    continue
                }
                $pathProp = $file.PSObject.Properties['path']
                if ($null -ne $pathProp -and $pathProp.Value) { [void]$paths.Add([string]$pathProp.Value) }
            }
        }
        return @($paths.ToArray())
    }
    foreach ($line in ($Text -split "`r?`n")) {
        $p = $line.Trim()
        if ($p -ne '') { [void]$paths.Add($p) }
    }
    return @($paths.ToArray())
}

function Format-GimFleetBool {
    <#
    .SYNOPSIS
        Lowercase true/false for report lines.
    #>
    param([bool]$Value)
    if ($Value) { return 'true' }
    return 'false'
}

function Format-GimFleetAuthLine {
    <#
    .SYNOPSIS
        One auth line: ok (host), skipped, or failed (redacted error).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        $Auth
    )
    if ($Auth.skipped) { return ($Label + ': skipped') }
    if ($Auth.ok) {
        $where = ''
        if ($Auth.host) { $where = ' (' + [string]$Auth.host + ')' }
        elseif ($Auth.site) { $where = ' (' + [string]$Auth.site + ')' }
        return ($Label + ': ok' + $where)
    }
    $err = ConvertTo-GimFleetSingleLine -Text ([string]$Auth.error)
    if ($err -eq '') { $err = 'failed' }
    return ($Label + ': failed (' + $err + ')')
}

function Format-GimFleetSummary {
    <#
    .SYNOPSIS
        Compact text summary for gim-preflight (auth lines, one line per repo,
        fetch/pull state, and one docs line per family).
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Report
    )
    $lines = New-Object System.Collections.Generic.List[string]
    if ($Report.dry_run) { $lines.Add('mode: dry-run (no fetch or pull)') }
    $lines.Add((Format-GimFleetAuthLine -Label 'gh auth' -Auth $Report.gh_auth))
    $lines.Add((Format-GimFleetAuthLine -Label 'jira auth' -Auth $Report.jira_auth))
    foreach ($repo in @($Report.repos)) {
        $lines.Add(('repo: {0} branch={1} expected={2} clean={3} status={4}' -f `
            [string]$repo.path,
            [string]$repo.branch,
            [string]$repo.expected_branch,
            (Format-GimFleetBool -Value ([bool]$repo.clean)),
            [string]$repo.status))
        $fetch = 'fetch: '
        if ($repo.fetch.planned) { $fetch += 'planned' }
        elseif ($repo.fetch.ok) { $fetch += 'ok' }
        elseif ($repo.fetch.skipped) { $fetch += 'skipped' }
        else { $fetch += 'failed (' + (ConvertTo-GimFleetSingleLine -Text ([string]$repo.fetch.error)) + ')' }
        $lines.Add('  ' + $fetch)
        $pull = 'pull: '
        if ($repo.pull.planned) {
            $pull += 'planned'
            if ($repo.pull.reason) { $pull += ' (' + [string]$repo.pull.reason + ')' }
        } elseif ($repo.pull.skipped) {
            $pull += 'skipped'
            if ($repo.pull.reason) { $pull += ' (' + [string]$repo.pull.reason + ')' }
        } elseif ($repo.pull.ok) { $pull += 'ok' }
        else { $pull += 'failed (' + (ConvertTo-GimFleetSingleLine -Text ([string]$repo.pull.error)) + ')' }
        $lines.Add('  ' + $pull)
        foreach ($family in $script:GimFleetDocFamilies) {
            $idx = $null
            $prop = $repo.docs_indexes.PSObject.Properties[$family.name]
            if ($null -ne $prop) { $idx = $prop.Value }
            if ($null -eq $idx) { continue }
            $remote = '-'
            if ($null -ne $idx.remote) { $remote = [string]$idx.remote }
            $open = '-'
            if ($null -ne $idx.open_pr) { $open = [string]$idx.open_pr }
            $next = '-'
            if ($null -ne $idx.next) { $next = [string]$idx.next }
            $line = ('  docs {0} next={1} (local={2} remote={3} open_pr={4})' -f `
                $family.name, $next, $idx.local, $remote, $open)
            if (-not [bool]$idx.ok) { $line += ' ok=false' }
            if ($idx.error) { $line += ' error=' + (ConvertTo-GimFleetSingleLine -Text ([string]$idx.error) -MaxLength 120) }
            $lines.Add($line)
        }
    }
    return @($lines.ToArray())
}
