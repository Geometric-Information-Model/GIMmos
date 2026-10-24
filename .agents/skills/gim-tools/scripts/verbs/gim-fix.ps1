# gim-fix.ps1 - in-place byte-level fixes paired with the gim-inspect flags:
# eof-newline | line-endings | trailing-ws. Fixes are encoding-preserving
# (BOM-detected codec round-trips the file: UTF-8-BOM stays BOM'd, UTF-16
# stays UTF-16). Every fix reports per-file before/after; --dry-run computes
# the report without touching disk. Write verbs confine to the current repo +
# .agent-scratch/ (Resolve-GimWritePath). Line-ending classification comes
# from the shared Get-GimLineEndingsName (same helper gim-inspect uses).

function Get-GimFixPlan {
    <#
    .SYNOPSIS
        Compute the fixed text plus before/after facts for one file without
        touching disk. Returns { path, mode, before, after, changed, fixed,
        codec }.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Mode,
        [AllowEmptyString()]
        [string]$To
    )
    $real = Resolve-GimWritePath -Path $Path
    if (-not (Test-Path -LiteralPath $real)) {
        Write-GimError "path does not exist: '$Path' (gim-fix fixes files only)"
    }
    if ([System.IO.Directory]::Exists($real)) {
        Write-GimError "path is a directory: '$Path' (gim-fix fixes files only)"
    }
    $bytes = [System.IO.File]::ReadAllBytes($real)
    $codec = Get-GimTextCodec -Bytes $bytes
    $text = Read-GimTextBytes -Bytes $bytes

    $before = $null
    $after = $null
    $changed = $false
    $fixed = $text

    switch ($Mode) {
        'eof-newline' {
            $before = ($text.EndsWith("`n") -or $text.EndsWith("`r"))
            $after = $true
            if (-not $before) {
                $nl = $To
                if (-not $nl) {
                    # No explicit --to: match the file's existing ending
                    # (CRLF, then lone CR - gim-inspect treats a lone CR as a
                    # newline terminator - else LF) so the append never
                    # creates mixed endings.
                    if ($text.IndexOf("`r`n") -ge 0) { $nl = 'crlf' }
                    elseif ($text.IndexOf("`r") -ge 0) { $nl = 'cr' }
                    else { $nl = 'lf' }
                }
                if ($nl -eq 'crlf') { $fixed = $text + "`r`n" }
                elseif ($nl -eq 'cr') { $fixed = $text + "`r" }
                else { $fixed = $text + "`n" }
                $changed = $true
            }
        }
        'line-endings' {
            $target = $To
            if (-not $target) { $target = 'lf' }
            $before = Get-GimLineEndingsName -Text $text
            $after = $target
            $normalized = $text -replace '\r\n|\r', "`n"
            if ($target -eq 'crlf') { $normalized = $normalized -replace "`n", "`r`n" }
            if ($normalized -cne $text) {
                $fixed = $normalized
                $changed = $true
            }
        }
        'trailing-ws' {
            $before = [regex]::IsMatch($text, '(?m)[ \t]+\r?$')
            $after = $false
            $stripped = $text -replace '(?m)[ \t]+(?=\r?$)', ''
            if ($stripped -cne $text) {
                $fixed = $stripped
                $changed = $true
            }
        }
    }
    return [pscustomobject]@{
        path = $real
        mode = $Mode
        before = $before
        after = $after
        changed = $changed
        fixed = $fixed
        codec = $codec
    }
}

function gim-fix {
    <#
    .SYNOPSIS
        Fix byte-level issues in place (eof-newline / line-endings /
        trailing-ws), reporting before/after per file.
    .DESCRIPTION
        Modes pair with gim-inspect flags:
          eof-newline  - ensure the file ends with a newline (append the
                         file's own ending - CRLF, lone CR, else LF - or
                         --to lf|crlf)
          line-endings - normalize every line ending to --to lf (default) or
                         crlf
          trailing-ws  - strip trailing spaces/tabs on each line (line
                         endings untouched)
        Input: paths (positional or pipeline) or the text record lines emitted
        by gim-inspect and passed through gim-where ('path: kind=file
        trailingWs=True' - the path is the prefix before the first ': ');
        anything else is rejected with a shape error. Missing paths and
        directories error. Fixes preserve the file's encoding (BOM-detected
        codec round-trips UTF-8-BOM and UTF-16). --dry-run prints the report
        and writes nothing. Write targets confine to the current repo +
        .agent-scratch/ (never sibling repos, $env:TEMP, UNC, or escapes).
        Text output 'path: mode=... before=... after=... changed=...';
        --json emits { count, truncated, items } of { path, mode, before,
        after, changed }. Bounded to 500 items / 1 MiB with an explicit
        # gim: output truncated marker.
    .PARAMETER Raw
        Positional mode ('eof-newline'|'line-endings'|'trailing-ws') then
        paths, plus '--flag value' options (see DESCRIPTION): --to lf|crlf,
        --dry-run, --json.
    .EXAMPLE
        gim-list --glob "docs/**/*.md" | gim-inspect --trailing-ws | gim-where --matches "trailingWs=True" | gim-fix trailing-ws --dry-run
    .EXAMPLE
        gim-fix line-endings .gitignore --to lf
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
                '--to' = 'To'
            } `
            -SwitchFlags @{
                '--dry-run' = 'DryRun'
                '--json'    = 'Json'
            }
        $pos = @($parsed.Positional)
        if ($pos.Count -eq 0) {
            Write-GimError 'missing mode: expected "gim-fix <eof-newline|line-endings|trailing-ws> [paths...]"'
        }
        $mode = [string]$pos[0]
        if ($mode -notin @('eof-newline', 'line-endings', 'trailing-ws')) {
            Write-GimError "invalid mode '$mode' (expected eof-newline, line-endings, or trailing-ws)"
        }
        $to = ''
        if ($parsed.ContainsKey('To')) { $to = [string]$parsed['To'] }
        if ($to) {
            if ($to -notin @('lf', 'crlf')) {
                Write-GimError "invalid --to value: '$to' (expected lf or crlf)"
            }
            if ($mode -eq 'trailing-ws') {
                Write-GimError "invalid --to value: '$to' ('--to' is not valid for mode 'trailing-ws')"
            }
        }
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
        $pathItems = New-Object System.Collections.Generic.List[string]
        for ($i = 1; $i -lt $pos.Count; $i++) {
            [void]$pathItems.Add((Convert-GimWritePathItem -Item $pos[$i]))
        }
        $caps = Get-GimCaps
    }
    process {
        if ($null -ne $Item) {
            [void]$pathItems.Add((Convert-GimWritePathItem -Item $Item))
        }
    }
    end {
        if ($pathItems.Count -eq 0) {
            Write-GimError 'no input: give paths (positional or pipeline)'
        }
        $records = New-Object System.Collections.Generic.List[object]
        foreach ($p in $pathItems) {
            $plan = Get-GimFixPlan -Path $p -Mode $mode -To $to
            if ($plan.changed -and -not $dryRun) {
                [System.IO.File]::WriteAllText($plan.path, $plan.fixed, $plan.codec.Encoding)
            }
            $records.Add([pscustomobject]@{
                path = $plan.path
                mode = $plan.mode
                before = $plan.before
                after = $plan.after
                changed = $plan.changed
            })
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            $lines.Add(('{0}: mode={1} before={2} after={3} changed={4}' -f $rec.path, $rec.mode, $rec.before, $rec.after, $rec.changed))
        }
        $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $records.Count }
    }
}
