# gim-diff.ps1 - two-set path comparison with no intermediate variables.
# Left set: pipeline/positional paths. Right set: --against <glob|path>
# (repeatable) or --against-var <name> (a stored newline-separated path list).
# Records: { path, state } with state in only-left|only-right|both.
# Pipeline/var items accept the 'path: ...' record form (e.g. gim-inspect
# output: 'path: kind=file hash=...'); the path prefix is extracted by the
# shared Get-GimPathFromRecord (same ': ' convention as gim-fix/gim-apply).

function Get-GimDiffRightSet {
    # Expand --against entries (globs or literal contained paths) plus an
    # optional --against-var list into a set of real paths.
    param([string[]]$Against, [string]$AgainstVar)
    $set = New-Object 'System.Collections.Generic.HashSet[string]'
    $roots = Get-GimRoots
    foreach ($entry in $Against) {
        if ([string]::IsNullOrWhiteSpace($entry)) { continue }
        if ($entry -match '[*?]') {
            $hits = @(Get-GimGlobMatches -Glob $entry -Roots $roots.read -DepthFilter 0)
            foreach ($h in $hits) { [void]$set.Add($h) }
        } else {
            $real = Resolve-ContainedPath -Path $entry
            [void]$set.Add($real)
        }
    }
    if ($AgainstVar) {
        # Consumer read via the shared enforcing helper: TTL is checked
        # identically to gim-get (expired -> clear error, never a stale value;
        # age always on stderr) - plan A5 "never returns stale value" is
        # global across every --<param>-from-var consumer.
        $value = Get-GimVarValueEnforced -Name $AgainstVar
        if ($null -eq $value) {
            Write-GimError "variable not set: '$AgainstVar' (use gim-set to store it)"
        }
        foreach ($line in @($value -split '\r?\n')) {
            $t = $line.Trim()
            if ($t -eq '') { continue }
            $real = Resolve-ContainedPath -Path (Get-GimPathFromRecord -Item $t)
            [void]$set.Add($real)
        }
    }
    return [string[]]$set
}

function gim-diff {
    <#
    .SYNOPSIS
        Compare two path sets and report only-left / only-right / both.
    .DESCRIPTION
        Left set: paths from the pipeline (e.g. gim-list) or positional.
        Items accept the 'path: ...' record form (e.g. gim-inspect output
        'path: kind=file hash=...'); the path is the prefix before the first
        ': '.
        Right set: --against <glob-or-path> (repeatable) and/or
        --against-var <name> (a stored newline-separated path list, read from
        the variable store; every path is containment-revalidated). Stored
        values are TTL-enforced on read like gim-get (an expired variable
        errors with 'expired (age N, ttl N)' and is never consumed).
        Comparison is on real paths. Text output is 'state: path' grouped by
        state; --json emits { count, truncated, items } of { path, state }
        records. Bounded to 500 records / 1 MiB with an explicit
        # gim: output truncated marker.
    .PARAMETER Raw
        Positional left paths plus '--flag value' options (see DESCRIPTION).
        --against <glob|path>, --against-var <name>, --json.
    .EXAMPLE
        gim-list --glob ".cursor/agents/**" | gim-diff --against ".opencode/agents/**"
    #>
    [CmdletBinding(PositionalBinding = $false)]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [object]$Item,
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]]$Raw
    )
    begin {
        if ($null -eq $Raw) { $Raw = @() }
        $parsed = Convert-GimArgs -Raw $Raw `
            -ValueFlags @{
                '--against'      = 'Against'
                '--against-var'  = 'AgainstVar'
            } `
            -SwitchFlags @{
                '--json' = 'Json'
            }
        $jsonMode = $parsed.ContainsKey('Json')
        $against = @()
        if ($parsed.ContainsKey('Against')) { $against = @($parsed['Against']) }
        $againstVar = ''
        if ($parsed.ContainsKey('AgainstVar')) { $againstVar = [string]$parsed['AgainstVar'] }
        if ($against.Count -eq 0 -and -not $againstVar) {
            Write-GimError 'no right set: give --against <glob|path> or --against-var <name>'
        }
        $caps = Get-GimCaps
        $pathItems = New-Object System.Collections.Generic.List[string]
        foreach ($p in @($parsed.Positional)) {
            if (-not [string]::IsNullOrWhiteSpace($p)) { [void]$pathItems.Add($p) }
        }
    }
    process {
        if ($null -ne $Item) { [void]$pathItems.Add([string]$Item) }
    }
    end {
        if ($pathItems.Count -eq 0) {
            Write-GimError 'no left set: give paths (positional or pipeline)'
        }
        $right = @(Get-GimDiffRightSet -Against $against -AgainstVar $againstVar)
        $left = New-Object 'System.Collections.Generic.HashSet[string]'
        foreach ($p in $pathItems) {
            if ([string]::IsNullOrWhiteSpace($p)) { continue }
            $real = Resolve-ContainedPath -Path (Get-GimPathFromRecord -Item $p)
            [void]$left.Add($real)
        }
        $records = New-Object System.Collections.Generic.List[object]
        foreach ($state in @('only-left', 'only-right', 'both')) {
            if ($state -eq 'only-left') {
                $paths = @($left | Where-Object { -not $right.Contains($_) })
            } elseif ($state -eq 'only-right') {
                $paths = @($right | Where-Object { -not $left.Contains($_) })
            } else {
                $paths = @($left | Where-Object { $right.Contains($_) })
            }
            foreach ($path in @($paths | Sort-Object)) {
                $records.Add([pscustomobject]@{ path = $path; state = $state })
            }
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            $lines.Add(($rec.state + ': ' + $rec.path))
        }
        $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $records.Count }
    }
}
