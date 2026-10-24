# gim-grep.ps1 - pattern search over files (paths via pipeline/positional) or a
# piped text stream (--stdin). Default output 'file:line: text'; --capture N
# emits the capture group instead of the line. Records: path/line/text (+capture
# when --capture used, +context flag for --context surroundings).
# Files are scanned lazily via StreamReader (OpenText) - never fully
# materialized - so a multi-GB file cannot OOM the session. Caps are enforced
# DURING accumulation: scanning stops once the item cap (--max-hits, default
# 500) or the 1 MiB byte budget is reached, so whole-repo greps never
# materialize unbounded record sets.

function Get-GimGrepMatchCount {
    # Count matching lines. -StopAfter > 0 breaks after the first StopAfter
    # matches (used by --files-only to early-exit per file).
    param([string[]]$Lines, [System.Text.RegularExpressions.Regex]$Rx, [int]$StopAfter = 0)
    $count = 0
    foreach ($line in $Lines) {
        if ($Rx.IsMatch($line)) {
            $count++
            if ($StopAfter -gt 0 -and $count -ge $StopAfter) { break }
        }
    }
    return $count
}

function Get-GimGrepMatches {
    # Scan one line set and materialize match records up to the caps.
    # Returns @{ MatchLines; Records; Truncated; Bytes }.
    # Records: { path, line, text, capture?, context } where context=true marks
    # --context surroundings (never a match). Without --context the scan stops
    # as soon as the item cap is consumed (no lookahead needed); record
    # materialization is always bounded by the item and byte budgets.
    param([string]$Path, [string[]]$Lines, [System.Text.RegularExpressions.Regex]$Rx, [int]$CaptureIdx, [int]$ContextN, [int]$MaxRecords = 500, [long]$MaxBytes = 1048576)
    $matchSet = New-Object 'System.Collections.Generic.HashSet[int]'
    $captures = @{}
    $truncated = $false
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $m = $Rx.Match($Lines[$i])
        if ($m.Success) {
            $ln = $i + 1
            [void]$matchSet.Add($ln)
            if ($CaptureIdx -ge 0) {
                $g = $m.Groups[$CaptureIdx]
                if ($null -ne $g) { $captures[$ln] = $g.Value } else { $captures[$ln] = '' }
            }
            if ($ContextN -eq 0 -and $matchSet.Count -ge $MaxRecords) { $truncated = $true; break }
        }
    }
    $include = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($ln in $matchSet) {
        [void]$include.Add($ln)
        if ($ContextN -gt 0) {
            for ($c = 1; $c -le $ContextN; $c++) {
                if (($ln - $c) -ge 1) { [void]$include.Add($ln - $c) }
                [void]$include.Add($ln + $c)
            }
        }
    }
    $records = New-Object System.Collections.Generic.List[object]
    $bytes = 0L
    $sorted = @([int[]]$include | Sort-Object)
    foreach ($ln in $sorted) {
        if ($ln -lt 1 -or $ln -gt $Lines.Count) { continue }
        if ($records.Count -ge $MaxRecords) { $truncated = $true; break }
        $recBytes = [System.Text.Encoding]::UTF8.GetByteCount($Lines[$ln - 1]) + $Path.Length + 16
        if ($bytes + $recBytes -gt $MaxBytes) { $truncated = $true; break }
        $isMatch = $matchSet.Contains($ln)
        $base = @{
            path    = $Path
            line    = $ln
            text    = $Lines[$ln - 1]
            context = (-not $isMatch)
        }
        if ($CaptureIdx -ge 0 -and $isMatch) {
            $base['capture'] = $captures[$ln]
        }
        $records.Add([pscustomobject]$base)
        $bytes += $recBytes
    }
    return @{ MatchLines = [int[]]$matchSet; Records = $records; Truncated = $truncated; Bytes = $bytes }
}

function Get-GimGrepFileMatchCount {
    # Streaming per-file match count. -StopAfter > 0 breaks after the first
    # StopAfter matches (used by --files-only to early-exit per file). Reads
    # lines lazily - the file is never materialized. StreamReader is disposed
    # explicitly: PowerShell foreach over ReadLines() does not dispose the
    # lazy enumerator on break, which would leak the file handle (flaky lock
    # errors in tests).
    param([string]$Path, [System.Text.RegularExpressions.Regex]$Rx, [int]$StopAfter = 0)
    $count = 0
    $reader = [System.IO.File]::OpenText($Path)
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ($Rx.IsMatch($line)) {
                $count++
                if ($StopAfter -gt 0 -and $count -ge $StopAfter) { break }
            }
        }
    } finally {
        $reader.Dispose()
    }
    return $count
}

function Get-GimGrepFileMatchesStreaming {
    # Lazy line-by-line scan of one file producing match/context records with
    # identical semantics to Get-GimGrepMatches: matches at their line numbers
    # in natural order, --context surroundings marked context=true, captures
    # attached to match records, records deduped by line number, and the same
    # item/byte caps with a Truncated flag. The file is read line-by-line via
    # a disposed StreamReader (OpenText) so memory stays O(context) per file,
    # never O(file size).
    param([string]$Path, [System.Text.RegularExpressions.Regex]$Rx, [int]$CaptureIdx, [int]$ContextN, [int]$MaxRecords = 500, [long]$MaxBytes = 1048576)
    $records = New-Object System.Collections.Generic.List[object]
    $bytes = 0L
    $truncated = $false
    $emittedLines = New-Object 'System.Collections.Generic.HashSet[int]'
    # Rolling window of up to ContextN non-match lines before the current
    # position, emitted as before-context when the next match arrives.
    $window = New-Object System.Collections.Generic.List[object]
    $afterLine = 0
    $lineNo = 0
    # StreamReader is disposed explicitly (finally): PowerShell foreach over
    # ReadLines() does not dispose the lazy enumerator on break, leaking the
    # file handle (flaky lock errors in tests).
    $reader = [System.IO.File]::OpenText($Path)
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            $lineNo++
            $m = $Rx.Match($line)
            if ($m.Success) {
                foreach ($w in $window) {
                    if ($records.Count -ge $MaxRecords -or $bytes -gt $MaxBytes) { $truncated = $true; break }
                    if ($emittedLines.Contains($w.line)) { continue }
                    [void]$emittedLines.Add($w.line)
                    $recBytes = [System.Text.Encoding]::UTF8.GetByteCount($w.text) + $Path.Length + 16
                    if ($bytes + $recBytes -gt $MaxBytes) { $truncated = $true; break }
                    $records.Add([pscustomobject]@{ path = $Path; line = $w.line; text = $w.text; context = $true })
                    $bytes += $recBytes
                }
                $window.Clear()
                if (-not $emittedLines.Contains($lineNo)) {
                    if ($records.Count -ge $MaxRecords -or $bytes -gt $MaxBytes) { $truncated = $true; break }
                    [void]$emittedLines.Add($lineNo)
                    $recBytes = [System.Text.Encoding]::UTF8.GetByteCount($line) + $Path.Length + 16
                    if ($bytes + $recBytes -gt $MaxBytes) { $truncated = $true; break }
                    $base = @{ path = $Path; line = $lineNo; text = $line; context = $false }
                    if ($CaptureIdx -ge 0) {
                        $g = $m.Groups[$CaptureIdx]
                        $base['capture'] = $(if ($null -ne $g) { $g.Value } else { '' })
                    }
                    $records.Add([pscustomobject]$base)
                    $bytes += $recBytes
                }
                $afterLine = $lineNo
                if ($ContextN -eq 0 -and $records.Count -ge $MaxRecords) { $truncated = $true; break }
            } else {
                if ($ContextN -gt 0 -and $afterLine -gt 0 -and ($lineNo - $afterLine) -le $ContextN) {
                    # After-context of the most recent match.
                    if (-not $emittedLines.Contains($lineNo)) {
                        if ($records.Count -ge $MaxRecords -or $bytes -gt $MaxBytes) { $truncated = $true; break }
                        [void]$emittedLines.Add($lineNo)
                        $recBytes = [System.Text.Encoding]::UTF8.GetByteCount($line) + $Path.Length + 16
                        if ($bytes + $recBytes -gt $MaxBytes) { $truncated = $true; break }
                        $records.Add([pscustomobject]@{ path = $Path; line = $lineNo; text = $line; context = $true })
                        $bytes += $recBytes
                    }
                } else {
                    # Keep the before-context window for a future match (only in
                    # context mode; without --context no window is needed).
                    if ($ContextN -gt 0) {
                        $window.Add(@{ text = $line; line = $lineNo })
                        if ($window.Count -gt $ContextN) { $window.RemoveAt(0) }
                    }
                }
            }
        }
    } finally {
        $reader.Dispose()
    }
    return @{ Records = $records; Truncated = $truncated; Bytes = $bytes }
}

function Format-GimGrepLine {
    # Text rendering for one record: capture value first, then count, then the
    # file:line shape ('file:line: text' for matches, 'file:line- text' for
    # context surroundings, 'line: text' / 'line- text' for --stdin without a
    # file path). File-level records (--files-only / --files-without-match /
    # --count) have no line property and render as the bare path / count.
    param($Record, [switch]$Stdin)
    $capProp = $Record.PSObject.Properties['capture']
    if ($null -ne $capProp) { return [string]$capProp.Value }
    $countProp = $Record.PSObject.Properties['count']
    if ($null -ne $countProp) {
        if ($Record.path) { return ($Record.path + ': ' + $countProp.Value) }
        return [string]$countProp.Value
    }
    $lineProp = $Record.PSObject.Properties['line']
    if ($null -eq $lineProp) {
        return [string]$Record.path
    }
    if ($Stdin) {
        if ($Record.context) { return ($Record.line.ToString() + '- ' + $Record.text) }
        return ($Record.line.ToString() + ': ' + $Record.text)
    }
    if ($Record.context) {
        return ($Record.path + ':' + $Record.line + '- ' + $Record.text)
    }
    return ($Record.path + ':' + $Record.line + ': ' + $Record.text)
}

function gim-grep {
    <#
    .SYNOPSIS
        Pattern search: files by path, or a piped text stream with --stdin.
    .DESCRIPTION
        Input: paths (positional or pipeline, e.g. from gim-list) or, with
        --stdin, a piped text stream. Every path is real-path resolved and
        asserted contained in roots.json (a directory or missing path is an
        error). Regex via [regex]; --fixed escapes the pattern; --ignore-case
        matches case-insensitively. Default text output is 'file:line: text'
        (matches) / 'file:line- text' (--context surroundings) / 'line: text'
        for --stdin. --capture N emits the captured value instead of the line
        (so it can feed path predicates downstream, e.g. gim-where --missing).
        --files-only / --files-without-match / --count are file-level modes and
        mutually exclusive; for --stdin the piped stream is treated as one
        input named '(stdin)'. Bounded by --max-hits (default 500) and the 1 MiB
        byte budget, enforced DURING accumulation (scanning stops once a cap is
        reached), with an explicit # gim: output truncated marker carrying the
        real emitted byte count. --json emits { count, truncated, items } of
        records with path/line/text (+capture when --capture used, +context).
    .PARAMETER Raw
        '--flag value' options only (see DESCRIPTION). --pattern <re>,
        --capture N, --stdin, --fixed, --ignore-case, --files-only,
        --files-without-match, --count, --context N, --max-hits N, --json.
    .EXAMPLE
        gim-list --glob "docs/adr/*.md" | gim-grep --pattern "status"
    .EXAMPLE
        git diff -- A.cs | gim-grep --stdin --pattern "^[+-](.*)$" --capture 1
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
                '--pattern'  = 'Pattern'
                '--capture'  = 'Capture'
                '--context'  = 'Context'
                '--max-hits' = 'MaxHits'
            } `
            -SwitchFlags @{
                '--stdin'               = 'Stdin'
                '--fixed'               = 'Fixed'
                '--ignore-case'         = 'IgnoreCase'
                '--files-only'          = 'FilesOnly'
                '--files-without-match' = 'FilesWithoutMatch'
                '--count'               = 'Count'
                '--json'                = 'Json'
            }

        if (-not $parsed.ContainsKey('Pattern')) {
            Write-GimError 'no --pattern given'
        }
        $pattern = [string]$parsed['Pattern']

        $fileModeCount = 0
        foreach ($k in @('FilesOnly', 'FilesWithoutMatch', 'Count')) {
            if ($parsed.ContainsKey($k)) { $fileModeCount++ }
        }
        if ($fileModeCount -gt 1) {
            Write-GimError '--files-only, --files-without-match, and --count are mutually exclusive'
        }

        $stdinMode = $parsed.ContainsKey('Stdin')
        $filesOnly = $parsed.ContainsKey('FilesOnly')
        $filesWithoutMatch = $parsed.ContainsKey('FilesWithoutMatch')
        $countMode = $parsed.ContainsKey('Count')
        $jsonMode = $parsed.ContainsKey('Json')

        $posPaths = @($parsed.Positional)
        if ($stdinMode -and $posPaths.Count -gt 0) {
            Write-GimError '--stdin cannot be combined with path inputs'
        }

        $rxText = $pattern
        if ($parsed.ContainsKey('Fixed')) { $rxText = [regex]::Escape($rxText) }
        $rxOptions = [System.Text.RegularExpressions.RegexOptions]::None
        if ($parsed.ContainsKey('IgnoreCase')) {
            $rxOptions = $rxOptions -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
        }
        try {
            $rx = New-Object System.Text.RegularExpressions.Regex $rxText, $rxOptions
        }
        catch {
            Write-GimError "invalid --pattern regex: '$pattern'"
        }

        $captureIdx = -1
        if ($parsed.ContainsKey('Capture')) {
            $ci = 0
            if (-not [int]::TryParse([string]$parsed['Capture'], [ref]$ci) -or $ci -lt 0) {
                Write-GimError "invalid --capture value: '$($parsed['Capture'])' (expected integer >= 0)"
            }
            $captureIdx = $ci
            if ($captureIdx -ge $rx.GetGroupNumbers().Count) {
                Write-GimError "capture group $captureIdx does not exist in pattern '$pattern'"
            }
        }

        $maxHits = 500
        if ($parsed.ContainsKey('MaxHits')) {
            $mh = 0
            if (-not [int]::TryParse([string]$parsed['MaxHits'], [ref]$mh) -or $mh -lt 1) {
                Write-GimError "invalid --max-hits value: '$($parsed['MaxHits'])' (expected integer >= 1)"
            }
            $maxHits = $mh
        }

        $contextN = 0
        if ($parsed.ContainsKey('Context')) {
            $cn = 0
            if (-not [int]::TryParse([string]$parsed['Context'], [ref]$cn) -or $cn -lt 0) {
                Write-GimError "invalid --context value: '$($parsed['Context'])' (expected integer >= 0)"
            }
            $contextN = $cn
        }

        $caps = Get-GimCaps -MaxItems $maxHits
        $pathItems = New-Object System.Collections.Generic.List[string]
        $stdinLines = New-Object System.Collections.Generic.List[string]
        if (-not $stdinMode) {
            foreach ($p in $posPaths) {
                if (-not [string]::IsNullOrWhiteSpace($p)) { [void]$pathItems.Add($p) }
            }
        }
    }
    process {
        if ($null -eq $Item) { return }
        if ($stdinMode) {
            [void]$stdinLines.Add([string]$Item)
        } else {
            [void]$pathItems.Add([string]$Item)
        }
    }
    end {
        $records = New-Object System.Collections.Generic.List[object]
        $scanTruncated = $false
        $scanBytes = 0L
        $maxRecords = $caps.MaxItems
        $maxBytes = $caps.MaxBytes

        if ($stdinMode) {
            $lines = @($stdinLines.ToArray())
            if ($countMode) {
                $records.Add([pscustomobject]@{ path = '(stdin)'; count = (Get-GimGrepMatchCount -Lines $lines -Rx $rx) })
            } elseif ($filesOnly) {
                if ((Get-GimGrepMatchCount -Lines $lines -Rx $rx -StopAfter 1) -gt 0) {
                    $records.Add([pscustomobject]@{ path = '(stdin)' })
                }
            } elseif ($filesWithoutMatch) {
                if ((Get-GimGrepMatchCount -Lines $lines -Rx $rx) -eq 0) {
                    $records.Add([pscustomobject]@{ path = '(stdin)' })
                }
            } else {
                $res = Get-GimGrepMatches -Path '' -Lines $lines -Rx $rx -CaptureIdx $captureIdx -ContextN $contextN -MaxRecords $maxRecords -MaxBytes $maxBytes
                $scanTruncated = $res.Truncated
                $scanBytes = $res.Bytes
                foreach ($r in $res.Records) { $records.Add($r) }
            }
        } else {
            if ($pathItems.Count -eq 0) {
                Write-GimError 'no input: give paths (positional or pipeline) or use --stdin'
            }
            $files = New-Object System.Collections.Generic.List[string]
            foreach ($p in $pathItems) {
                if ([string]::IsNullOrWhiteSpace($p)) { continue }
                $real = Resolve-ContainedPath -Path $p
                if ([System.IO.Directory]::Exists($real)) {
                    Write-GimError "path is a directory: $p"
                }
                $files.Add($real)
            }
            foreach ($file in $files) {
                if ($records.Count -ge $maxRecords) { $scanTruncated = $true; break }
                if ($countMode) {
                    $records.Add([pscustomobject]@{ path = $file; count = (Get-GimGrepFileMatchCount -Path $file -Rx $rx) })
                } elseif ($filesOnly) {
                    if ((Get-GimGrepFileMatchCount -Path $file -Rx $rx -StopAfter 1) -gt 0) {
                        $records.Add([pscustomobject]@{ path = $file })
                    }
                } elseif ($filesWithoutMatch) {
                    if ((Get-GimGrepFileMatchCount -Path $file -Rx $rx) -eq 0) {
                        $records.Add([pscustomobject]@{ path = $file })
                    }
                } else {
                    $res = Get-GimGrepFileMatchesStreaming -Path $file -Rx $rx -CaptureIdx $captureIdx -ContextN $contextN -MaxRecords $maxRecords -MaxBytes $maxBytes
                    if ($res.Truncated) { $scanTruncated = $true }
                    $scanBytes += $res.Bytes
                    foreach ($r in $res.Records) { $records.Add($r) }
                }
                if ($records.Count -ge $maxRecords) { $scanTruncated = $true; break }
                if ($scanBytes -gt $maxBytes) { $scanTruncated = $true; break }
            }
        }

        if ($jsonMode) {
            Write-GimJson ([pscustomobject]@{ count = $records.Count; truncated = $scanTruncated; items = @($records.ToArray()) })
            return
        }
        $outLines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            $outLines.Add((Format-GimGrepLine -Record $rec -Stdin:$stdinMode))
        }
        $fmt = Format-GimLines -Items @($outLines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated -or $scanTruncated) {
            $markerBytes = 0L
            foreach ($l in $fmt.Lines) { $markerBytes += [System.Text.Encoding]::UTF8.GetByteCount($l) + 2 }
            Write-GimTruncationMarker -Items $records.Count -Bytes $markerBytes
        }
    }
}
