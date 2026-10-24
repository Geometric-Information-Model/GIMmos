# gim-asm.ps1 - enumerate exported type full names from assemblies via PE
# metadata (System.Reflection.PortableExecutable.PEReader +
# System.Reflection.Metadata.MetadataReader; both in-box on pwsh 7 /
# .NET 10 shared framework). NEVER loads the target assembly into the
# session (no Assembly.LoadFrom / ReflectionOnlyLoadFrom) - PEReader
# byte-parses the file directly.

# Best-TFM preference for nuget lib/ folder selection (lower rank = better).
# Exact-TFM match first; a platform-suffixed TFM (net8.0-windows) falls back
# to its base TFM rank. Unknown TFMs sort after every known one (rank 1000,
# then alphabetical). net10.0/net9.0 are candidates on this machine's runtime;
# netcoreapp3.0 ranks with the modern net5+ family (above netstandard2.1, well
# above the net4x legacy list); netcoreapp2.x sits with the netstandard2.0 era;
# netcoreapp1.x belongs to the netstandard1.x era; net48x (net481/net48) are the
# .NET Framework 4.8-era successors; modern netstandard ranks before net4x.
$script:GimAsmTfmPreference = @(
    'net10.0', 'net9.0', 'net8.0', 'net7.0', 'net6.0', 'net5.0',
    'netcoreapp3.1', 'netcoreapp3.0',
    'netstandard2.1', 'netstandard2.0',
    'netcoreapp2.2', 'netcoreapp2.1', 'netcoreapp2.0',
    'netstandard1.6',
    'netcoreapp1.1', 'netcoreapp1.0',
    'net481', 'net48', 'net472', 'net471', 'net47', 'net462', 'net461', 'net46',
    'net452', 'net451', 'net45', 'net40', 'net35', 'net20'
)

function Get-GimAsmTfmRank {
    # Lower rank = preferred. Unknown TFMs sort after the known list; a
    # platform-suffixed TFM (e.g. net8.0-windows) matches its base TFM rank.
    param([string]$Tfm)
    $lower = $Tfm.ToLowerInvariant()
    for ($i = 0; $i -lt $script:GimAsmTfmPreference.Count; $i++) {
        if ($lower -eq $script:GimAsmTfmPreference[$i]) { return $i }
    }
    $base = $lower.Split('-')[0]
    if ($base -ne $lower) {
        for ($i = 0; $i -lt $script:GimAsmTfmPreference.Count; $i++) {
            if ($base -eq $script:GimAsmTfmPreference[$i]) { return $i }
        }
    }
    return 1000
}

function Get-GimAsmBestLibDlls {
    # Pick the best-TFM lib/ dll(s) of a package: rank lib subfolders by the
    # preference list (net8+ > netstandard2.0 > net4x), take the first folder
    # that contains dlls.
    param([string]$PackageDir)
    $libDir = Join-Path $PackageDir 'lib'
    if (-not (Test-Path -LiteralPath $libDir -PathType Container)) { return @() }
    $tfms = @(Get-ChildItem -LiteralPath $libDir -Directory -ErrorAction SilentlyContinue)
    $ranked = @($tfms | Sort-Object -Property @{ Expression = { Get-GimAsmTfmRank -Tfm $_.Name } }, @{ Expression = { $_.Name } })
    foreach ($tfmDir in $ranked) {
        $dlls = @(Get-ChildItem -LiteralPath $tfmDir.FullName -Filter '*.dll' -File -ErrorAction SilentlyContinue)
        if ($dlls.Count -gt 0) {
            return @($dlls | ForEach-Object { $_.FullName } | Sort-Object)
        }
    }
    return @()
}

function Get-GimAsmTypesFromDll {
    # Enumerate public, top-level (non-nested) type full names from a dll's PE
    # metadata. Compiler-generated names (containing '<') are skipped.
    # PEReader byte-parses the file; the MetadataReader is created from the
    # metadata blob via MetadataReaderProvider.FromMetadataImage (pointer-free
    # on pwsh 7 / .NET 10 - the byte* MetadataReader ctor is not PowerShell
    # callable, and PEReader.GetMetadata() returns a PEMemoryBlock, not a
    # MetadataReader). The target assembly is never loaded. Stream + PEReader
    # are created inside try so a corrupt/empty file never leaks the stream and
    # surfaces as a clean 'gim:' error instead of a raw BadImageFormatException.
    param([string]$DllPath)
    $types = New-Object System.Collections.Generic.List[string]
    $stream = $null
    $pe = $null
    $provider = $null
    try {
        $stream = [System.IO.File]::OpenRead($DllPath)
        $pe = New-Object System.Reflection.PortableExecutable.PEReader($stream, [System.Reflection.PortableExecutable.PEStreamOptions]::LeaveOpen)
        if (-not $pe.HasMetadata) {
            Write-GimError "no CLI metadata: $DllPath"
        }
        $content = $pe.GetMetadata().GetContent()
        $provider = [System.Reflection.Metadata.MetadataReaderProvider]::FromMetadataImage($content)
        $md = $provider.GetMetadataReader()
        $visibilityPublic = [System.Reflection.TypeAttributes]::Public
        $visibilityMask = [System.Reflection.TypeAttributes]::VisibilityMask
        foreach ($tdHandle in $md.TypeDefinitions) {
            $def = $md.GetTypeDefinition($tdHandle)
            if (-not $def.GetDeclaringType().IsNil) { continue }
            if (($def.Attributes -band $visibilityMask) -ne $visibilityPublic) { continue }
            $name = $md.GetString($def.Name)
            if ($name.Contains('<')) { continue }
            $ns = $md.GetString($def.Namespace)
            if ($ns) { $full = $ns + '.' + $name } else { $full = $name }
            $types.Add($full)
        }
    } catch {
        if ($_.Exception.Message -match '^gim: ') { throw }
        Write-GimError "cannot read PE metadata from ${DllPath}: $($_.Exception.Message)"
    } finally {
        if ($provider) { $provider.Dispose() }
        if ($pe) { $pe.Dispose() }
        if ($stream) { $stream.Dispose() }
    }
    return $types
}

function gim-asm {
    <#
    .SYNOPSIS
        Enumerate exported type full names from assemblies via PE metadata.
    .DESCRIPTION
        Modes: --path <dll> reads one assembly directly (containment asserted
        against the full roots list including the nuget read-extra root);
        --package <id>/<ver> resolves the package under
        %USERPROFILE%\.nuget\packages, picks the best-TFM lib/ dll(s), and
        enumerates each. Types emitted are public, top-level (non-nested),
        non-compiler-generated full names. The target assembly is NEVER loaded
        into the session (PEReader + MetadataReader only). --matches <re>
        filters type names. Text output is one 'path: type' line per record;
        --json emits { count, truncated, items } of { path, type } records.
        Bounded to 500 records / 1 MiB with an explicit # gim: output truncated
        marker.
    .PARAMETER Raw
        Positional mode ('types') plus '--flag value' options (see
        DESCRIPTION). --package <id>/<ver>, --path <dll>, --matches <re>,
        --json.
    .EXAMPLE
        gim-asm types --path obj/Debug/net8.0/MyLib.dll --matches "Options"
    .EXAMPLE
        gim-asm types --package microsoft.entityframeworkcore/7.0.20 --matches "Options|IDbContextFactory"
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
                '--package' = 'Package'
                '--path'    = 'Path'
                '--matches' = 'Matches'
            } `
            -SwitchFlags @{
                '--json' = 'Json'
            }
        $pos = @($parsed.Positional)
        if ($pos.Count -ne 1 -or $pos[0] -ne 'types') {
            if ($pos.Count -eq 0) {
                Write-GimError 'missing mode: expected "gim-asm types"'
            }
            Write-GimError "unknown gim-asm mode: '$($pos -join ' ')' (expected 'types')"
        }
        $jsonMode = $parsed.ContainsKey('Json')
        $package = ''
        if ($parsed.ContainsKey('Package')) { $package = [string]$parsed['Package'] }
        $pathArg = ''
        if ($parsed.ContainsKey('Path')) { $pathArg = [string]$parsed['Path'] }
        $matchesRe = ''
        if ($parsed.ContainsKey('Matches')) { $matchesRe = [string]$parsed['Matches'] }
        if ($package -and $pathArg) {
            Write-GimError 'give either --package or --path, not both'
        }
        if (-not $package -and -not $pathArg) {
            Write-GimError 'give --package <id>/<ver> or --path <dll>'
        }
        $rx = $null
        if ($matchesRe) {
            try {
                $rx = New-Object System.Text.RegularExpressions.Regex $matchesRe
            }
            catch {
                Write-GimError "invalid --matches regex: '$matchesRe'"
            }
        }
        $caps = Get-GimCaps
        $dlls = New-Object System.Collections.Generic.List[string]
    }
    end {
        if ($pathArg) {
            # Containment against the full read roots INCLUDING the nuget
            # read-extra root (gim-asm is its only consumer). Resolve-ContainedPath
            # checks the ordinary read roots only, so assert here.
            $real = Get-GimRealPath -Path $pathArg
            $asmRoots = Get-GimRoots
            $allRoots = @($asmRoots.read) + @($asmRoots.readExtra)
            if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths $allRoots)) {
                Write-GimError "path not contained in configured roots: '$pathArg'"
            }
            if ([System.IO.Directory]::Exists($real)) {
                Write-GimError "path is a directory: $pathArg"
            }
            if (-not [System.IO.File]::Exists($real)) {
                Write-GimError "path does not exist: $pathArg"
            }
            $dlls.Add($real)
        } else {
            $parts = @($package -split '/', 2)
            if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0]) -or [string]::IsNullOrWhiteSpace($parts[1])) {
                Write-GimError "invalid --package value: '$package' (expected <id>/<ver>)"
            }
            $pkgId = $parts[0]
            $pkgVer = $parts[1]
            $nugetRoots = @((Get-GimRoots).readExtra)
            if ($nugetRoots.Count -eq 0) {
                Write-GimError 'nuget package root not configured (roots.json readExtraRoots)'
            }
            $pkgDir = $null
            foreach ($root in $nugetRoots) {
                foreach ($candidateId in @($pkgId, $pkgId.ToLowerInvariant())) {
                    $cand = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $root $candidateId) $pkgVer))
                    if (-not (Test-Path -LiteralPath $cand -PathType Container)) { continue }
                    $real = Get-GimRealPath -Path $cand
                    if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths @($root))) {
                        Write-GimError "package path escapes the nuget root: $cand"
                    }
                    $pkgDir = $real
                    break
                }
                if ($pkgDir) { break }
            }
            if (-not $pkgDir) {
                Write-GimError "package not found: $package (under nuget root)"
            }
            $libDlls = @(Get-GimAsmBestLibDlls -PackageDir $pkgDir)
            if ($libDlls.Count -eq 0) {
                Write-GimError "no lib/ dll found for package: $package"
            }
            foreach ($d in $libDlls) { $dlls.Add($d) }
        }

        $records = New-Object System.Collections.Generic.List[object]
        foreach ($dll in $dlls) {
            foreach ($t in @(Get-GimAsmTypesFromDll -DllPath $dll)) {
                if ($rx -and -not $rx.IsMatch($t)) { continue }
                $records.Add([pscustomobject]@{ path = $dll; type = $t })
            }
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) { $lines.Add($rec.path + ': ' + $rec.type) }
        $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) {
            $markerBytes = 0L
            foreach ($l in $fmt.Lines) { $markerBytes += [System.Text.Encoding]::UTF8.GetByteCount($l) + 2 }
            Write-GimTruncationMarker -Items $records.Count -Bytes $markerBytes
        }
    }
}
