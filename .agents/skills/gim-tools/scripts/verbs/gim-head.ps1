# gim-head.ps1 - first/last lines of files or a piped text stream (replaces
# Select-Object -First/-Skip/-Last, denied). Positional inputs are file paths
# and are strict (missing path or directory is an error). Pipeline items are
# auto-detected: an item that resolves to an existing contained file is read
# as a file; any other item is treated as one line of a text stream (so
# 'Get-Content file | gim-head' and 'gim-grep ... | gim-head' work). Text
# output: raw lines for a single file or a stream, 'path: line' for multiple
# files. Records: { path, line, text } (path is '(stdin)' for stream lines).

function Add-GimHeadFileRecords {
    # Append head/tail records for one file. Window applies per file (existing
    # behavior: 'gim-head a.txt b.txt -n 1' emits one line per file).
    param(
        [string]$Real,
        [int]$N,
        [int]$Skip,
        [switch]$Last,
        [System.Collections.Generic.List[object]]$Records
    )
    if (-not $Last -and $N -eq 0) { return }
    # StreamReader with try/finally: ReadLines' lazy enumerator is not
    # disposed by PowerShell on break, which would leak the file handle
    # (flaky lock errors in tests). Explicit dispose avoids that.
    $stream = New-Object System.IO.StreamReader($Real, [System.Text.Encoding]::UTF8, $true)
    try {
        $lineNo = 0
        if ($Last) {
            # Rolling buffer of size n: full scan is inherent for tail,
            # but memory stays O(n), never O(file size).
            $buf = New-Object System.Collections.Generic.List[string]
            $bufLines = New-Object System.Collections.Generic.List[int]
            while (($line = $stream.ReadLine()) -ne $null) {
                $lineNo++
                if ($N -gt 0) {
                    $buf.Add($line)
                    $bufLines.Add($lineNo)
                    if ($buf.Count -gt $N) {
                        $buf.RemoveAt(0)
                        $bufLines.RemoveAt(0)
                    }
                }
            }
            for ($i = 0; $i -lt $buf.Count; $i++) {
                $Records.Add([pscustomobject]@{ path = $Real; line = $bufLines[$i]; text = $buf[$i] })
            }
        } else {
            $skipped = 0
            $emitted = 0
            while (($line = $stream.ReadLine()) -ne $null) {
                $lineNo++
                if ($skipped -lt $Skip) { $skipped++; continue }
                if ($emitted -ge $N) { break }
                $Records.Add([pscustomobject]@{ path = $Real; line = $lineNo; text = $line })
                $emitted++
            }
        }
    } finally {
        $stream.Dispose()
    }
}

function Add-GimHeadStreamRecords {
    # Append head/tail records for a piped text stream. Window applies across
    # the whole stream (Select-Object -First/-Skip/-Last semantics). Stream
    # lines carry path '(stdin)' in records and render as raw text lines.
    param(
        [string[]]$Lines,
        [int]$N,
        [int]$Skip,
        [switch]$Last,
        [System.Collections.Generic.List[object]]$Records
    )
    if ($Lines.Count -eq 0) { return }
    if ($Last) {
        $buf = New-Object System.Collections.Generic.List[string]
        foreach ($line in $Lines) {
            if ($N -gt 0) {
                $buf.Add($line)
                if ($buf.Count -gt $N) { $buf.RemoveAt(0) }
            }
        }
        $startLine = $Lines.Count - $buf.Count + 1
        for ($j = 0; $j -lt $buf.Count; $j++) {
            $Records.Add([pscustomobject]@{ path = '(stdin)'; line = $startLine + $j; text = $buf[$j] })
        }
    } else {
        if ($N -eq 0) { return }
        $lineNo = 0
        $skipped = 0
        $emitted = 0
        foreach ($line in $Lines) {
            $lineNo++
            if ($skipped -lt $Skip) { $skipped++; continue }
            if ($emitted -ge $N) { break }
            $Records.Add([pscustomobject]@{ path = '(stdin)'; line = $lineNo; text = $line })
            $emitted++
        }
    }
}

function gim-head {
    <#
    .SYNOPSIS
        Print the first (or with --last, last) N lines of files or a piped
        text stream.
    .DESCRIPTION
        Input: positional paths are strict file paths (missing path or
        directory is an error). Pipeline items auto-detect: an item that
        resolves to an existing contained file is read as a file; any other
        item is one line of a text stream, so 'Get-Content file | gim-head',
        'gim-grep ... | gim-head', and 'gim-list ... | gim-head' all work.
        -n N defaults to 10; --skip N skips the first N lines before the head
        window (applied per file, or across the whole stream); --last emits
        the final N lines instead (--skip does not apply). Text output is the
        raw lines for a single file or a stream, prefixed 'path: ' when more
        than one file is given. Stream lines are always emitted raw.
        --json emits { count, truncated, items } of { path, line, text }
        records (stream lines carry path '(stdin)'). Bounded to 500 records /
        1 MiB with an explicit # gim: output truncated marker.
    .PARAMETER Raw
        Positional literal paths plus '--flag value' options (see DESCRIPTION).
        -n N, --skip N, --last, --json.
    .EXAMPLE
        gim-list --glob "tests/**/*.cs" | gim-head -n 5
    .EXAMPLE
        Get-Content "GIMtransfer\FileManager\FileManager.cs" | gim-head -n 60 --skip 100
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
                '--n'    = 'N'
                '--skip' = 'Skip'
            } `
            -SwitchFlags @{
                '--last' = 'Last'
                '--json' = 'Json'
            }
        $jsonMode = $parsed.ContainsKey('Json')
        $n = 10
        if ($parsed.ContainsKey('N')) {
            $nv = 0
            if (-not [int]::TryParse([string]$parsed['N'], [ref]$nv) -or $nv -lt 0) {
                Write-GimError "invalid -n value: '$($parsed['N'])' (expected integer >= 0)"
            }
            $n = $nv
        }
        $skip = 0
        if ($parsed.ContainsKey('Skip')) {
            $sv = 0
            if (-not [int]::TryParse([string]$parsed['Skip'], [ref]$sv) -or $sv -lt 0) {
                Write-GimError "invalid --skip value: '$($parsed['Skip'])' (expected integer >= 0)"
            }
            $skip = $sv
        }
        $last = $parsed.ContainsKey('Last')
        $caps = Get-GimCaps
        $roots = Get-GimRoots
        # Ordered item list: positional paths first (strict file mode, resolved
        # in end so missing paths error), then pipeline items in order.
        $orderedItems = New-Object System.Collections.Generic.List[object]
        foreach ($p in @($parsed.Positional)) {
            if ([string]::IsNullOrWhiteSpace($p)) { continue }
            [void]$orderedItems.Add([pscustomobject]@{ kind = 'file'; raw = $p; real = '' })
        }
    }
    process {
        if ($null -eq $Item) { return }
        $s = [string]$Item
        # Auto-detect: a pipeline item that resolves to an existing contained
        # file is read as a file; anything else (missing, directory, outside
        # roots, plain text) is one line of the text stream. Probe is
        # non-throwing and silent: real-path resolution, containment check,
        # then File.Exists.
        $entry = [pscustomobject]@{ kind = 'stream'; raw = $s; real = '' }
        if (-not [string]::IsNullOrWhiteSpace($s)) {
            try {
                $full = Get-GimRealPath -Path $s
                if ((Test-GimContainedPath -RealPath $full -RootRealPaths $roots.read) -and [System.IO.File]::Exists($full)) {
                    $entry.kind = 'file'
                    $entry.real = $full
                }
            } catch {
                # Unresolvable text -> keep the stream entry.
            }
        }
        [void]$orderedItems.Add($entry)
    }
    end {
        if ($orderedItems.Count -eq 0) {
            Write-GimError 'no input: give paths (positional or pipeline)'
        }
        $records = New-Object System.Collections.Generic.List[object]
        $fileSourceCount = 0
        $pendingStream = New-Object System.Collections.Generic.List[string]
        foreach ($entry in $orderedItems) {
            if ($entry.kind -eq 'stream') {
                $pendingStream.Add($entry.raw)
                continue
            }
            # File entry: flush any pending stream segment first so output
            # order matches input order.
            if ($pendingStream.Count -gt 0) {
                Add-GimHeadStreamRecords -Lines @($pendingStream.ToArray()) -N $n -Skip $skip -Last:$last -Records $records
                $pendingStream.Clear()
            }
            $real = $entry.real
            if (-not $real) {
                $real = Resolve-ContainedPath -Path $entry.raw
                if ([System.IO.Directory]::Exists($real)) {
                    Write-GimError "path is a directory: $($entry.raw)"
                }
                if (-not [System.IO.File]::Exists($real)) {
                    Write-GimError "path does not exist: $($entry.raw)"
                }
            }
            $fileSourceCount++
            Add-GimHeadFileRecords -Real $real -N $n -Skip $skip -Last:$last -Records $records
        }
        if ($pendingStream.Count -gt 0) {
            Add-GimHeadStreamRecords -Lines @($pendingStream.ToArray()) -N $n -Skip $skip -Last:$last -Records $records
            $pendingStream.Clear()
        }

        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $multiFile = $fileSourceCount -gt 1
        $outLines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            if ($rec.path -eq '(stdin)') {
                $outLines.Add($rec.text)
            } elseif ($multiFile) {
                $outLines.Add(($rec.path + ': ' + $rec.text))
            } else {
                $outLines.Add($rec.text)
            }
        }
        $fmt = Format-GimLines -Items @($outLines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $records.Count }
    }
}
