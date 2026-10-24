# gim-apply.ps1 - fixed action vocabulary (--create-empty | --touch |
# --remove) over a filtered pipeline set. Actions act on files; directories
# and missing targets error (except --remove, which treats missing as
# already-absent no-op, and --touch, which creates). Targets confine to the
# current repo + .agent-scratch/ or the real OS temp root
# (Resolve-GimApplyPath); every other write verb stays repo-only.

function Invoke-GimApplyOne {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Action,
        [switch]$DryRun
    )
    $real = Resolve-GimApplyPath -Path $Path
    $exists = Test-Path -LiteralPath $real
    if ($exists -and [System.IO.Directory]::Exists($real)) {
        Write-GimError "path is a directory: '$Path' (gim-apply acts on files)"
    }
    $changed = $false
    switch ($Action) {
        'create-empty' {
            if ($exists) {
                Write-GimError "target already exists: '$real' (--create-empty refuses overwrite)"
            }
            $changed = $true
            if (-not $DryRun) {
                $dir = Split-Path -Parent $real
                if (-not (Test-Path -LiteralPath $dir)) {
                    New-Item -ItemType Directory -Path $dir -Force | Out-Null
                }
                [System.IO.File]::WriteAllText($real, '', (New-Object System.Text.UTF8Encoding($false)))
            }
        }
        'touch' {
            $changed = $true
            if (-not $DryRun) {
                if ($exists) {
                    (Get-Item -LiteralPath $real -Force).LastWriteTime = [datetime]::Now
                } else {
                    $dir = Split-Path -Parent $real
                    if (-not (Test-Path -LiteralPath $dir)) {
                        New-Item -ItemType Directory -Path $dir -Force | Out-Null
                    }
                    [System.IO.File]::WriteAllText($real, '', (New-Object System.Text.UTF8Encoding($false)))
                }
            }
        }
        'remove' {
            if ($exists) {
                $changed = $true
                if (-not $DryRun) {
                    [System.IO.File]::Delete($real)
                }
            }
        }
    }
    return [pscustomobject]@{ path = $real; action = $Action; changed = $changed }
}

function gim-apply {
    <#
    .SYNOPSIS
        Apply one fixed action (--create-empty | --touch | --remove) to every
        input path.
    .DESCRIPTION
        Input: paths (positional or pipeline, e.g. filtered gim-list output) or
        the text record lines emitted by gim-inspect / passed through
        gim-where ('path: ...' - the path is the prefix before the first
        ': '); anything else is rejected with a shape error.
        Actions:
          --create-empty - create an empty file; errors when the target exists
          --touch        - update LastWriteTime, creating the file when missing
          --remove       - delete the file; missing is a no-op (changed=False)
        Directories error for every action. --dry-run reports what would
        change and touches nothing. Targets may live inside the current repo
        (+ .agent-scratch/) or under the real OS temp root; paths outside both
        roots, UNC targets, and unexpanded %VAR% tokens are rejected
        (gim-write and gim-fix stay repo-only). Text output
        'path: action=... changed=...'; --json emits
        { count, truncated, items } of { path, action, changed }.
        Bounded to 500 items / 1 MiB with an explicit # gim: output truncated
        marker.
    .PARAMETER Raw
        Positional paths plus '--flag value' options (see DESCRIPTION):
        --create-empty, --touch, --remove (exactly one), --dry-run, --json.
    .EXAMPLE
        gim-list --glob ".agent-scratch/tmp-*" | gim-apply --remove --dry-run
    .EXAMPLE
        gim-apply --create-empty .agent-scratch/empty.txt
    .EXAMPLE
        gim-apply --remove <os-temp-file>   # OS temp root is allowed for gim-apply
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
            -SwitchFlags @{
                '--create-empty' = 'CreateEmpty'
                '--touch'        = 'Touch'
                '--remove'       = 'Remove'
                '--dry-run'      = 'DryRun'
                '--json'         = 'Json'
            }
        $actionKeys = @()
        foreach ($k in @('CreateEmpty', 'Touch', 'Remove')) {
            if ($parsed.ContainsKey($k)) { $actionKeys += $k }
        }
        if ($actionKeys.Count -eq 0) {
            Write-GimError 'missing action: give --create-empty, --touch, or --remove'
        }
        if ($actionKeys.Count -gt 1) {
            Write-GimError "give exactly one action, not: '$($actionKeys -join ', ')'"
        }
        $action = 'create-empty'
        if ($actionKeys[0] -eq 'Touch') { $action = 'touch' }
        elseif ($actionKeys[0] -eq 'Remove') { $action = 'remove' }
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
        $pathItems = New-Object System.Collections.Generic.List[string]
        foreach ($p in @($parsed.Positional)) {
            if (-not [string]::IsNullOrWhiteSpace($p)) {
                [void]$pathItems.Add((Convert-GimApplyPathItem -Item $p))
            }
        }
        $caps = Get-GimCaps
    }
    process {
        if ($null -ne $Item) {
            [void]$pathItems.Add((Convert-GimApplyPathItem -Item $Item))
        }
    }
    end {
        if ($pathItems.Count -eq 0) {
            Write-GimError 'no input: give paths (positional or pipeline)'
        }
        $records = New-Object System.Collections.Generic.List[object]
        foreach ($p in $pathItems) {
            $records.Add((Invoke-GimApplyOne -Path $p -Action $action -DryRun:$dryRun))
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            $lines.Add(('{0}: action={1} changed={2}' -f $rec.path, $rec.action, $rec.changed))
        }
        $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $records.Count }
    }
}
