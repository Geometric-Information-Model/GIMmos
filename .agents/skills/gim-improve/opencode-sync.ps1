<#
.SYNOPSIS
    Copy GIM OpenCode agents and skills into the user OpenCode config.

.DESCRIPTION
    Copies configured source folders only. Destination path components and text
    replace the repository's "gim-" prefix with the selected prefix. Existing
    destination model lines are preserved; new files keep the source model line.
    This script never invokes git and never deletes destination content.

.PARAMETER Prefix
    Destination name prefix. A trailing "-" is added when omitted.

.PARAMETER RepoRoot
    Overrides config repoRoot. Without it, repoRoot defaults to the repository
    containing this script.
#>
[CmdletBinding()]
param(
    [string]$Prefix,
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$configPath = Join-Path $PSScriptRoot 'opencode-sync.config.json'

function Get-ConfigValue {
    param($Config, [string]$Name)

    if ($Config.PSObject.Properties.Match($Name).Count -gt 0) {
        return $Config.$Name
    }

    return $null
}

function Resolve-ConfiguredPath {
    param(
        [string]$PathValue,
        [string]$BasePath
    )

    $expanded = [Environment]::ExpandEnvironmentVariables($PathValue)
    if ([System.IO.Path]::IsPathRooted($expanded)) {
        return [System.IO.Path]::GetFullPath($expanded)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $expanded))
}

function Normalize-Prefix {
    param([string]$Value)

    $normalized = if ($null -eq $Value) { '' } else { $Value.Trim() }
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        throw 'opencode-sync: prefix cannot be empty.'
    }

    if ($normalized -notmatch '^[^\\/:*?"<>|]+$') {
        throw "opencode-sync: prefix '$normalized' contains invalid filename characters."
    }

    if (-not $normalized.EndsWith('-')) {
        $normalized += '-'
    }

    return $normalized
}

<#
.SYNOPSIS
    Read `prefix:` under `## opencode-sync` in agent-prefs.md.

    Read only the matching section; never modify the prefs file.
#>
function Get-PrefsPrefix {
    param([string]$PrefsPath)

    if (-not (Test-Path -LiteralPath $PrefsPath -PathType Leaf)) {
        return $null
    }

    $lines = [System.IO.File]::ReadAllLines($PrefsPath)
    $inSection = $false
    foreach ($line in $lines) {
        if ($line -match '^\s*##\s+(.+?)\s*$') {
            $inSection = ($Matches[1].Trim() -ieq 'opencode-sync')
            continue
        }

        if ($inSection -and $line -match '^\s*-\s*prefix\s*:\s*(?<value>[^#]*?)\s*$') {
            $value = $Matches['value'].Trim()
            if ($value) {
                return $value
            }
        }
    }

    return $null
}

<#
.SYNOPSIS
    Resolve prefix from explicit input, prefs, or default.

    Use `-Prefix`, then an existing prefs value, then `gim-`; never prompt or write prefs.
#>
function Resolve-Prefix {
    param(
        [string]$RepoRootPath,
        [bool]$Explicit
    )

    if ($Explicit) {
        return Normalize-Prefix -Value $Prefix
    }

    $prefsPath = Join-Path $RepoRootPath '.agents\agent-prefs.md'
    $prefsPrefix = Get-PrefsPrefix -PrefsPath $prefsPath
    if ($prefsPrefix) {
        return Normalize-Prefix -Value $prefsPrefix
    }

    return Normalize-Prefix -Value 'gim'
}

<#
.SYNOPSIS
    Replace literal `gim-` in destination text only.

    Leave source repository text unchanged.
#>
function Replace-GimPrefix {
    param(
        [string]$Text,
        [string]$DestinationPrefix
    )

    return [regex]::Replace(
        $Text,
        [regex]::Escape('gim-'),
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($Match)
            $DestinationPrefix
        }
    )
}

function Test-Denylisted {
    param(
        [string]$RelativePath,
        [string[]]$Denylist
    )

    $normalized = $RelativePath.Replace('\', '/').TrimStart('/')
    foreach ($entry in $Denylist) {
        $entryNormalized = ([string]$entry).Replace('\', '/').Trim('/')
        if ($normalized -ieq $entryNormalized -or $normalized.StartsWith("$entryNormalized/", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

<#
.SYNOPSIS
    Rewrite each destination path segment without changing repo files.

    Apply prefix substitution to destination path components only.
#>
function Get-RewrittenRelativePath {
    param(
        [string]$RelativePath,
        [string]$DestinationPrefix
    )

    $parts = $RelativePath -split '[\\/]'
    $rewrittenParts = foreach ($part in $parts) {
        Replace-GimPrefix -Text $part -DestinationPrefix $DestinationPrefix
    }

    return [string]::Join([System.IO.Path]::DirectorySeparatorChar, $rewrittenParts)
}

function Test-TextFile {
    param([string]$Path)

    $textExtensions = @(
        '.md', '.txt', '.json', '.jsonc', '.yaml', '.yml', '.ps1', '.psm1',
        '.psd1', '.xml', '.toml', '.ini', '.cfg', '.cs', '.js', '.jsx',
        '.ts', '.tsx', '.py', '.html', '.css'
    )
    return $textExtensions -contains ([System.IO.Path]::GetExtension($Path).ToLowerInvariant())
}

function Get-ModelLine {
    param([string[]]$Lines)

    foreach ($line in $Lines) {
        if ($line -match '^\s*model\s*:') {
            return $line
        }
    }

    return $null
}

<#
.SYNOPSIS
    Rewrite destination text while preserving model-line rules.

    Never rewrite `model:`; keep an existing destination line, or insert it at its original index when source lacks one.
#>
function Merge-SourceText {
    param(
        [string]$SourceText,
        [string]$ExistingText,
        [string]$DestinationPrefix
    )

    $sourceLines = $SourceText -split "`r?`n", -1
    $existingLines = if ($null -eq $ExistingText) { @() } else { $ExistingText -split "`r?`n", -1 }
    $newline = if ($SourceText.Contains("`r`n")) { "`r`n" } elseif ($SourceText.Contains("`n")) { "`n" } else { "`r`n" }
    $existingModel = Get-ModelLine -Lines $existingLines
    $sourceModelIndex = -1
    for ($index = 0; $index -lt $sourceLines.Count; $index++) {
        if ($sourceLines[$index] -match '^\s*model\s*:') {
            $sourceModelIndex = $index
            break
        }
    }

    $outputLines = New-Object System.Collections.Generic.List[string]
    foreach ($line in $sourceLines) {
        if ($line -match '^\s*model\s*:') {
            # Source model line exists; preserve it, or keep existing destination model line.
            $outputLines.Add($(if ($existingModel) { $existingModel } else { $line }))
        } else {
            $outputLines.Add((Replace-GimPrefix -Text $line -DestinationPrefix $DestinationPrefix))
        }
    }

    if ($sourceModelIndex -lt 0 -and $existingModel) {
        # Source lacks model; insert destination model at its original index.
        $existingModelIndex = 0
        for ($index = 0; $index -lt $existingLines.Count; $index++) {
            if ($existingLines[$index] -ceq $existingModel) {
                $existingModelIndex = $index
                break
            }
        }
        $insertAt = [Math]::Min($existingModelIndex, $outputLines.Count)
        $outputLines.Insert($insertAt, $existingModel)
    }

    return [string]::Join($newline, $outputLines)
}

if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "opencode-sync: config not found at '$configPath'."
}

$config = [System.IO.File]::ReadAllText($configPath) | ConvertFrom-Json
$configuredRepoRoot = [string](Get-ConfigValue -Config $config -Name 'repoRoot')
$destinationRootValue = [string](Get-ConfigValue -Config $config -Name 'destinationRoot')
$sourceFolders = @(Get-ConfigValue -Config $config -Name 'sourceFolders')
$denylist = @([string[]](Get-ConfigValue -Config $config -Name 'denylist'))

$repoRootPath = if ($RepoRoot) {
    Resolve-ConfiguredPath -PathValue $RepoRoot -BasePath $scriptRepoRoot
} elseif ($configuredRepoRoot) {
    Resolve-ConfiguredPath -PathValue $configuredRepoRoot -BasePath $scriptRepoRoot
} else {
    $scriptRepoRoot
}

if (-not (Test-Path -LiteralPath $repoRootPath -PathType Container)) {
    throw "opencode-sync: repo root '$repoRootPath' does not exist."
}
if ([string]::IsNullOrWhiteSpace($destinationRootValue)) {
    throw "opencode-sync: config value 'destinationRoot' is missing or empty."
}
if ($sourceFolders.Count -eq 0) {
    throw 'opencode-sync: config value sourceFolders is empty.'
}

$destinationRoot = Resolve-ConfiguredPath -PathValue $destinationRootValue -BasePath $repoRootPath
$destinationPrefix = Resolve-Prefix -RepoRootPath $repoRootPath -Explicit:$($PSBoundParameters.ContainsKey('Prefix'))

Write-Host "[opencode-sync] Repo root: $repoRootPath"
Write-Host "[opencode-sync] Destination root: $destinationRoot"
Write-Host "[opencode-sync] Prefix: $destinationPrefix"

$copiedCount = 0
$skippedCount = 0
# Skip denylist, rewrite destination filenames and text files, copy other files unchanged, never delete destination content or touch repo files.
foreach ($sourceFolder in $sourceFolders) {
    $sourceRelative = [string](Get-ConfigValue -Config $sourceFolder -Name 'source')
    $destinationRelative = [string](Get-ConfigValue -Config $sourceFolder -Name 'destination')
    if ([string]::IsNullOrWhiteSpace($sourceRelative) -or [string]::IsNullOrWhiteSpace($destinationRelative)) {
        throw 'opencode-sync: each sourceFolders entry needs source and destination.'
    }

    $sourcePath = Resolve-ConfiguredPath -PathValue $sourceRelative -BasePath $repoRootPath
    $destinationPath = Join-Path $destinationRoot $destinationRelative
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
        throw "opencode-sync: source folder '$sourcePath' does not exist."
    }

    $sourceFiles = Get-ChildItem -LiteralPath $sourcePath -File -Recurse -Force
    foreach ($sourceFile in $sourceFiles) {
        $repoRelative = $sourceFile.FullName.Substring($repoRootPath.TrimEnd('\', '/').Length).TrimStart('\', '/')
        if (Test-Denylisted -RelativePath $repoRelative -Denylist $denylist) {
            $skippedCount++
            continue
        }

        $insideRelative = $sourceFile.FullName.Substring($sourcePath.TrimEnd('\', '/').Length).TrimStart('\', '/')
        $rewrittenRelative = Get-RewrittenRelativePath -RelativePath $insideRelative -DestinationPrefix $destinationPrefix
        $destinationFile = Join-Path $destinationPath $rewrittenRelative
        $destinationDirectory = Split-Path -Parent $destinationFile
        if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        }

        $existingText = $null
        if ((Test-Path -LiteralPath $destinationFile -PathType Leaf) -and (Test-TextFile -Path $sourceFile.FullName)) {
            $existingText = [System.IO.File]::ReadAllText($destinationFile)
        } elseif (Test-Path -LiteralPath $destinationFile -PathType Container) {
            throw "opencode-sync: destination '$destinationFile' is a directory; cannot copy source file."
        }

        if (Test-TextFile -Path $sourceFile.FullName) {
            $sourceText = [System.IO.File]::ReadAllText($sourceFile.FullName)
            $outputText = Merge-SourceText -SourceText $sourceText -ExistingText $existingText -DestinationPrefix $destinationPrefix
            [System.IO.File]::WriteAllText($destinationFile, $outputText, [System.Text.UTF8Encoding]::new($false))
        } else {
            [System.IO.File]::Copy($sourceFile.FullName, $destinationFile, $true)
        }

        $copiedCount++
        Write-Host "[opencode-sync] Copied '$repoRelative' -> '$destinationFile'."
    }
}

Write-Host "[opencode-sync] Summary: copied $copiedCount file(s); skipped $skippedCount denylisted file(s)."
