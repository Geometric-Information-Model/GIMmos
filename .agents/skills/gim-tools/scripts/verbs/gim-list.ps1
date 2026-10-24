# gim-list.ps1 - sole path entry point: list contained paths with globs,
# depth control, and bounded output. Canonical invocation uses '--flag' style
# (PowerShell treats '--' as end-of-parameters, so args are parsed manually).

function Convert-GimGlobToRegex {
    param([string]$Glob)
    # Canonical globs may use forward slashes (e.g. "docs/adr/*.md"); normalize
    # to backslash before building the regex so Windows real paths match.
    # '**' crosses separators; a trailing '**\' (zero or more directory
    # segments) is optional so 'sub/**/*.cs' also matches 'sub\a.cs'.
    $normalized = $Glob -replace '/', '\'
    $sb = New-Object System.Text.StringBuilder
    $i = 0
    while ($i -lt $normalized.Length) {
        $ch = $normalized[$i]
        if ($ch -eq '*') {
            if (($i + 1) -lt $normalized.Length -and $normalized[$i + 1] -eq '*') {
                if (($i + 2) -lt $normalized.Length -and $normalized[$i + 2] -eq '\') {
                    [void]$sb.Append('(?:.*\\)?')
                    $i += 3
                } else {
                    [void]$sb.Append('.*')
                    $i += 2
                }
            } else {
                [void]$sb.Append('[^\\]*')
                $i++
            }
        } elseif ($ch -eq '?') {
            [void]$sb.Append('[^\\]')
            $i++
        } else {
            [void]$sb.Append([System.Text.RegularExpressions.Regex]::Escape([string]$ch))
            $i++
        }
    }
    return '^' + $sb.ToString() + '$'
}

function Get-GimGlobAnchor {
    # Longest non-wildcard directory prefix of a glob, used to bound recursion.
    param([string]$Glob)
    $normalized = $Glob -replace '/', '\'
    $idx = $normalized.IndexOfAny([char[]]@('*', '?'))
    if ($idx -lt 0) { return $normalized }
    $before = $normalized.Substring(0, $idx)
    $lastSep = $before.LastIndexOf('\')
    if ($lastSep -lt 0) { return '' }
    return $before.Substring(0, $lastSep)
}

function Get-GimGlobMatches {
    # Expand a glob under each root. --depth counts components relative to the
    # glob anchor directory (the non-wildcard prefix), matching how literal
    # directory paths count depth relative to the input directory. Every hit is
    # real-path resolved AND re-asserted contained in the roots: a symlink or
    # junction inside a root that points outside is skipped, never emitted
    # (containment is never forgiven, same as literal/--from-file paths).
    param([string]$Glob, [string[]]$Roots, [int]$DepthFilter)
    $regex = Convert-GimGlobToRegex -Glob $Glob
    $rx = New-Object System.Text.RegularExpressions.Regex $regex
    $anchor = Get-GimGlobAnchor -Glob $Glob
    $anchorRel = ''
    if ($anchor -ne '') { $anchorRel = $anchor.Trim('\') }
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($root in $Roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $start = $root
        if ($anchor -ne '') { $start = [System.IO.Path]::GetFullPath((Join-Path $root $anchor)) }
        if (-not (Test-Path -LiteralPath $start)) { continue }
        $items = Get-ChildItem -LiteralPath $start -Recurse -Force -ErrorAction SilentlyContinue
        foreach ($item in $items) {
            $rel = $item.FullName.Substring($root.Length).TrimStart('\')
            if ($rx.IsMatch($rel)) {
                if ($DepthFilter -gt 0) {
                    $depthRel = $rel
                    if ($anchorRel -ne '') {
                        $prefix = $anchorRel + '\'
                        if ($depthRel.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                            $depthRel = $depthRel.Substring($prefix.Length)
                        }
                    }
                    if (@($depthRel -split '\\').Count -gt $DepthFilter) { continue }
                }
                $real = Get-GimRealPath -Path $item.FullName
                if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths $Roots)) { continue }
                $hits.Add($real)
            }
        }
    }
    return @($hits)
}

function Get-GimListUnder {
    # List entries under a directory, recursing Depth levels (1 = immediate children).
    param([string]$Dir, [int]$Depth)
    if ($Depth -le 1) {
        return @(Get-ChildItem -LiteralPath $Dir -Force -ErrorAction SilentlyContinue)
    }
    $all = @(Get-ChildItem -LiteralPath $Dir -Recurse -Force -ErrorAction SilentlyContinue)
    return @($all | Where-Object { @($_.FullName.Substring($Dir.Length).TrimStart('\') -split '\\').Count -le $Depth })
}

function gim-list {
    <#
    .SYNOPSIS
        List contained paths. Sole path entry point for the toolkit.
    .DESCRIPTION
        Inputs: positional literal paths, --from-file lines, or repeatable
        --glob patterns (relative to each configured root; ** crosses directory
        separators, * does not). Every emitted path is real-path resolved and
        asserted contained in roots.json -- glob matches included, so a symlink
        or junction inside a root that resolves outside is never emitted. A
        literal path that does not exist is an error unless --allow-missing. A
        glob that matches nothing is an error unless --allow-missing. --depth N
        limits recursion: relative to an input directory for literal paths,
        relative to the glob anchor directory for globs (1 = matches directly
        under the anchor).
        Output: full paths, one per line, sorted; --print0 NUL-separated
        (truncation marker goes to stderr to keep the NUL stream clean);
        --json emits { count, truncated, items }. Bounded to 500 items / 1 MiB
        with an explicit # gim: output truncated marker (stderr for --print0).
    .PARAMETER Raw
        Positional literal paths plus '--flag value' options (see DESCRIPTION).
        Use --from-file <f> for paths from a contained file, --glob <p> (repeat),
        --under <d>, --depth N, --dirs-only, --allow-missing, --print0, --json.
    .EXAMPLE
        gim-list --glob "docs/adr/*.md" | gim-where --is-file | gim-report --prefix ADR
    #>
    [CmdletBinding(PositionalBinding = $false)]
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]]$Raw
    )
    begin {
        if ($null -eq $Raw) { $Raw = @() }
        $parsed = Convert-GimArgs -Raw $Raw `
            -ValueFlags @{
                '--from-file' = 'FromFile'
                '--glob'      = 'Glob'
                '--under'     = 'Under'
                '--depth'     = 'Depth'
            } `
            -SwitchFlags @{
                '--dirs-only'     = 'DirsOnly'
                '--allow-missing' = 'AllowMissing'
                '--print0'        = 'Print0'
                '--json'          = 'Json'
            }
        $Path = @($parsed.Positional)
        $FromFile = $null
        if ($parsed.ContainsKey('FromFile')) { $FromFile = [string]$parsed['FromFile'] }
        $Glob = @()
        if ($parsed.ContainsKey('Glob')) { $Glob = @($parsed['Glob']) }
        $Under = $null
        if ($parsed.ContainsKey('Under')) { $Under = [string]$parsed['Under'] }
        $Depth = 1
        if ($parsed.ContainsKey('Depth')) {
            $depthNum = 0
            if (-not [int]::TryParse([string]$parsed['Depth'], [ref]$depthNum) -or $depthNum -lt 1) {
                Write-GimError "invalid --depth value: '$($parsed['Depth'])' (expected integer >= 1)"
            }
            $Depth = $depthNum
        }
        $DirsOnly = $parsed.ContainsKey('DirsOnly')
        $AllowMissing = $parsed.ContainsKey('AllowMissing')
        $Print0 = $parsed.ContainsKey('Print0')
        $Json = $parsed.ContainsKey('Json')
        $depthExplicit = $parsed.ContainsKey('Depth')
        $depthFilter = 0
        if ($depthExplicit) { $depthFilter = $Depth }

        $caps = Get-GimCaps
        $all = New-Object System.Collections.Generic.List[string]
        $roots = Get-GimRoots
    }
    end {
        $underReal = $null
        if ($Under) {
            $underReal = Resolve-ContainedPath -Path $Under -AllowMissing:$false
        }
        $anyInput = $false

        function Add-GimPathItem {
            param([string]$TargetReal)
            if (Test-Path -LiteralPath $TargetReal -PathType Container) {
                foreach ($item in Get-GimListUnder -Dir $TargetReal -Depth $Depth) {
                    $all.Add((Get-GimRealPath -Path $item.FullName))
                }
            } else {
                $all.Add($TargetReal)
            }
        }

        # --under re-roots the listing at the under directory so callers get
        # that subtree's contents (the filter below keeps items strictly below
        # it, excluding the under directory itself). --under alone is a valid
        # input source, so it counts toward $anyInput.
        if ($underReal) {
            $anyInput = $true
            Add-GimPathItem -TargetReal $underReal
        }

        foreach ($p in $Path) {
            if ($null -eq $p) { continue }
            $anyInput = $true
            $real = Resolve-ContainedPath -Path $p -AllowMissing:$AllowMissing
            if (-not (Test-Path -LiteralPath $real)) { continue }
            Add-GimPathItem -TargetReal $real
        }

        if ($FromFile) {
            $anyInput = $true
            $ffReal = Resolve-ContainedPath -Path $FromFile -AllowMissing:$false
            foreach ($line in @(Get-Content -LiteralPath $ffReal -Encoding UTF8 -ErrorAction Stop)) {
                $t = $line.Trim()
                if ($t -eq '') { continue }
                $real = Resolve-ContainedPath -Path $t -AllowMissing:$AllowMissing
                if (-not (Test-Path -LiteralPath $real)) { continue }
                Add-GimPathItem -TargetReal $real
            }
        }

        foreach ($g in $Glob) {
            if ($null -eq $g) { continue }
            $anyInput = $true
            if ($g -notmatch '[*?]') {
                $real = Resolve-ContainedPath -Path $g -AllowMissing:$AllowMissing
                if (-not (Test-Path -LiteralPath $real)) { continue }
                Add-GimPathItem -TargetReal $real
            } else {
                $matches = Get-GimGlobMatches -Glob $g -Roots $roots.read -DepthFilter $depthFilter
                if ($matches.Count -eq 0 -and -not $AllowMissing) {
                    Write-GimError "glob matched nothing: '$g' (use --allow-missing to skip)"
                }
                foreach ($m in $matches) { [void]$all.Add($m) }
            }
        }

        if (-not $anyInput) {
            Write-GimError 'no input: give paths, --from-file, or --glob'
        }

        $items = @($all | Sort-Object -Unique)

        if ($DirsOnly) {
            $dirs = New-Object System.Collections.Generic.List[string]
            foreach ($p in $items) {
                if (Test-Path -LiteralPath $p -PathType Container) { [void]$dirs.Add($p) }
            }
            $items = @($dirs)
        }

        if ($underReal) {
            $kept = New-Object System.Collections.Generic.List[string]
            foreach ($p in $items) {
                if ($p -ne $underReal -and (Test-GimContainedPath -RealPath $p -RootRealPaths @($underReal))) { [void]$kept.Add($p) }
            }
            $items = @($kept)
        }

        if ($Json) {
            Write-GimJson (New-GimJsonEnvelope -Items $items -MaxItems $caps.MaxItems)
            return
        }

        if ($Print0) {
            if ($items.Count -eq 0) { return }
            $kept = $items
            $truncated = $false
            if ($kept.Count -gt $caps.MaxItems) {
                $kept = $kept[0..($caps.MaxItems - 1)]
                $truncated = $true
            }
            $stream = [string]::Join([char]0, $kept)
            if ([System.Text.Encoding]::UTF8.GetByteCount($stream) -gt $caps.MaxBytes) {
                $fit = New-Object System.Collections.Generic.List[string]
                $bytes = 0L
                foreach ($t in $kept) {
                    $tb = [System.Text.Encoding]::UTF8.GetByteCount($t) + 1
                    if ($bytes + $tb -gt $caps.MaxBytes) { $truncated = $true; break }
                    [void]$fit.Add($t)
                    $bytes += $tb
                }
                $stream = [string]::Join([char]0, $fit)
            }
            Write-Output $stream
            if ($truncated) {
                # Marker goes to stderr: a stdout line would corrupt the NUL
                # token stream for downstream NUL consumers (e.g. xargs -0).
                [Console]::Error.WriteLine(('gim: truncated: print0 output capped at {0} items / {1} bytes' -f $caps.MaxItems, $caps.MaxBytes))
            }
            return
        }

        $fmt = Format-GimLines -Items $items -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $items.Count }
    }
}
