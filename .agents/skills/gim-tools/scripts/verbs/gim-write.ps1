# gim-write.ps1 - write a stdin/pipeline stream to a contained target.
# Default encoding is UTF-8 WITHOUT BOM (PS 5.1 `>`/`Out-File` emit UTF-16 -
# the --encoding flag exists because of that). --out-dir defaults to
# .agent-scratch/ (gitignored, in-repo). --path-from-var reads the target path
# from the A5 variable store (TTL-enforced like every --from-var consumer).
# Write targets confine to the current repo + .agent-scratch/.

function Resolve-GimWriteTarget {
    # Join --path with --out-dir (default .agent-scratch) and assert write
    # containment. A rooted --path bypasses --out-dir (giving both errors).
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [AllowEmptyString()]
        [string]$OutDir
    )
    if ([System.IO.Path]::IsPathRooted($Path)) {
        if ($OutDir) {
            Write-GimError "invalid arguments: '--path' is absolute, so --out-dir does not apply"
        }
        return Resolve-GimWritePath -Path $Path
    }
    $dir = $null
    if ($OutDir) {
        $dir = $OutDir
        if (-not [System.IO.Path]::IsPathRooted($dir)) {
            $dir = Join-Path (Get-GimRepoRoot) $dir
        }
    } else {
        $dir = Join-Path (Get-GimRepoRoot) '.agent-scratch'
    }
    return Resolve-GimWritePath -Path (Join-Path $dir $Path)
}

function gim-write {
    <#
    .SYNOPSIS
        Write a stdin/pipeline stream to a contained file path.
    .DESCRIPTION
        Buffers the pipeline (items joined with LF, exactly like gim-set) and
        writes one file. Default encoding UTF-8 WITHOUT BOM; --encoding
        utf8bom adds the BOM, utf16 writes UTF-16 LE with BOM. A relative
        --path lands under --out-dir (default .agent-scratch/, created on
        demand); an absolute --path must resolve inside the current repo.
        --create-only refuses to overwrite an existing target. --path-from-var
        <name> reads the target path from the variable store (TTL-enforced,
        expired errors and is never consumed). --dry-run prints the report and
        writes nothing (no directory creation either). Write targets confine to
        the current repo + .agent-scratch/; $env:TEMP, UNC, `..\..` escapes,
        and unexpanded %VAR% tokens are rejected. Text output
        'path: wrote|would-write N bytes'; --json emits { count, truncated,
        items } of { path, existed, created, bytes, encoding, dry_run }.
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION): --path <p>,
        --path-from-var <name>, --from-stdin, --create-only,
        --encoding utf8|utf8bom|utf16, --out-dir <d>, --dry-run, --json.
        Piped content is accepted directly (--from-stdin is optional when
        content is piped; it is still required when nothing is piped).
    .EXAMPLE
        gh pr diff 40 --repo Geometric-Information-Model/GIMfront | gim-write --path gimfront-pr40.diff
    .EXAMPLE
        '{"a":1}' | gim-write --path payload.json --encoding utf8bom
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
                '--path'          = 'Path'
                '--out-dir'       = 'OutDir'
                '--path-from-var' = 'PathFromVar'
                '--encoding'      = 'Encoding'
            } `
            -SwitchFlags @{
                '--from-stdin'  = 'FromStdin'
                '--create-only' = 'CreateOnly'
                '--dry-run'     = 'DryRun'
                '--json'        = 'Json'
            }
        if ($parsed.Positional.Count -gt 0) {
            Write-GimError "unexpected arguments: '$($parsed.Positional -join ' ')' (gim-write takes no positional arguments)"
        }
        $path = ''
        if ($parsed.ContainsKey('Path')) { $path = [string]$parsed['Path'] }
        $fromVar = ''
        if ($parsed.ContainsKey('PathFromVar')) { $fromVar = [string]$parsed['PathFromVar'] }
        if ($path -and $fromVar) {
            Write-GimError 'give either --path or --path-from-var, not both'
        }
        if (-not $path -and -not $fromVar) {
            Write-GimError 'missing target: give --path <p> or --path-from-var <name>'
        }
        $fromStdin = $parsed.ContainsKey('FromStdin')
        $outDir = ''
        if ($parsed.ContainsKey('OutDir')) { $outDir = [string]$parsed['OutDir'] }
        $encodingName = 'utf8'
        if ($parsed.ContainsKey('Encoding')) {
            $encodingName = [string]$parsed['Encoding']
            if ($encodingName -notin @('utf8', 'utf8bom', 'utf16')) {
                Write-GimError "invalid --encoding value: '$encodingName' (expected utf8, utf8bom, or utf16)"
            }
        }
        $createOnly = $parsed.ContainsKey('CreateOnly')
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
        $lines = New-Object System.Collections.Generic.List[string]
    }
    process {
        if ($null -ne $Item) { [void]$lines.Add([string]$Item) }
    }
    end {
        # Content source: piped items are always accepted. --from-stdin is
        # optional when content was piped, but required when nothing arrived
        # (an explicit empty write still needs the flag, so a bare invocation
        # never silently creates a 0-byte file).
        if (-not $fromStdin -and $lines.Count -eq 0) {
            Write-GimError 'missing content source: give --from-stdin (or pipe content)'
        }
        if ($fromVar) {
            $varPath = Get-GimVarValueEnforced -Name $fromVar
            if ($null -eq $varPath) {
                Write-GimError "variable not set: '$fromVar' (use gim-set to store it)"
            }
            $path = $varPath.Trim()
            if ($path -eq '') { Write-GimError "variable '$fromVar' holds an empty path" }
        }
        $target = Resolve-GimWriteTarget -Path $path -OutDir $outDir
        $existed = Test-Path -LiteralPath $target
        if ($createOnly -and $existed) {
            Write-GimError "target already exists: '$target' (--create-only refuses overwrite)"
        }
        $encoding = Get-GimEncodingByName -Name $encodingName
        $content = [string]::Join("`n", @($lines.ToArray()))
        # Reported bytes = on-disk size including BOM (projected for --dry-run).
        $bytes = $encoding.GetByteCount($content) + $encoding.GetPreamble().Length
        if (-not $dryRun) {
            $dir = Split-Path -Parent $target
            if (-not (Test-Path -LiteralPath $dir)) {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
            }
            [System.IO.File]::WriteAllText($target, $content, $encoding)
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @([pscustomobject]@{
                path = $target
                existed = $existed
                created = (-not $existed)
                bytes = $bytes
                encoding = $encodingName
                dry_run = $dryRun
            }) -MaxItems 1)
            return
        }
        $action = 'wrote'
        if ($dryRun) { $action = 'would-write' }
        Write-Output ('{0}: {1} {2} bytes' -f $target, $action, $bytes)
    }
}
