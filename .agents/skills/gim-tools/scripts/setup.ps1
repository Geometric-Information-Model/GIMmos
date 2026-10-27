# setup.ps1 - one-time PSModulePath registration for the gim-tools module
# (plan 0004 S1). PowerShell auto-loads modules by name when the containing
# folder is on PSModulePath; registering this scripts/ folder makes every
# fresh pwsh auto-load GIMTools (folder name == module name).
#
# Windows: user-scope HKCU\Environment, no admin. Path separator is ';'.
# Linux and macOS: CurrentUserAllHosts profile imports the module, and a
# marked block in ~/.profile exports PSModulePath for login shells. Path
# separator is ':'. [Environment]::SetEnvironmentVariable(..., 'User') is
# not available there.
#
# Run once per machine:
#   & ./.agents/skills/gim-tools/scripts/setup.ps1
# then restart opencode / Cursor so harness pwsh processes inherit the new env.
# Guide: docs/guides/0002-setup-gim-tools-autoload.md
#
# Trust note: this registers the checkout path for this user, so GIMTools
# auto-loads in every pwsh you run. Only run it on machines where you trust
# the checkout; the toolkit dir stays deny-write for agents.
#
# Verify-then-register: the module file is checked BEFORE any env write. A
# missing module file exits with a warning and leaves the session untouched.

$ErrorActionPreference = 'Stop'

$ScriptsDir = $PSScriptRoot
$ModuleFile = Join-Path $ScriptsDir (Join-Path 'GIMTools' 'GIMTools.psd1')
$PathSep = [string][System.IO.Path]::PathSeparator
$IsUnix = [System.IO.Path]::DirectorySeparatorChar -eq [char]'/'

function Get-NonEmptyPathEntries {
    param([string]$PathList)
    if (-not $PathList) { return @() }
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($part in ($PathList -split [regex]::Escape($PathSep))) {
        if ($part -ne '') { $out.Add($part) }
    }
    return $out.ToArray()
}

function Write-GimMarkedBlock {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Begin,
        [Parameter(Mandatory = $true)][string]$End,
        [Parameter(Mandatory = $true)][string]$Block
    )
    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $text = ''
    if (Test-Path -LiteralPath $Path) {
        $text = [System.IO.File]::ReadAllText($Path)
    }
    # String replace, not regex: the block contains $_ and other tokens that
    # [regex]::Replace would treat as substitution patterns.
    $body = $Block.TrimEnd() + "`n"
    $start = $text.IndexOf($Begin)
    $finish = if ($start -ge 0) { $text.IndexOf($End, $start) } else { -1 }
    if ($start -ge 0 -and $finish -gt $start) {
        $finish = $finish + $End.Length
        if ($finish -lt $text.Length -and $text[$finish] -eq "`n") { $finish++ }
        $text = $text.Substring(0, $start) + $body + $text.Substring($finish)
    } else {
        if ($text -and -not $text.EndsWith("`n")) { $text += "`n" }
        if ($text) { $text += "`n" }
        $text += $body
    }
    if (-not $text.EndsWith("`n")) { $text += "`n" }
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $text, $utf8)
}

# Read-only: needed to tell a stale entry apart from a fresh checkout when
# the module file is missing. On Unix the User target is unsupported.
$entries = @()
if (-not $IsUnix) {
    $entries = @(Get-NonEmptyPathEntries ([Environment]::GetEnvironmentVariable('PSModulePath', 'User')))
}

if (-not (Test-Path -LiteralPath $ModuleFile)) {
    Write-Warning "Module file not found at expected location: $ModuleFile"
    if ($entries -contains $ScriptsDir) {
        Write-Warning "Stale PSModulePath entry detected: User PSModulePath already contains '$ScriptsDir', but the module file is missing there. The repo may have moved; re-run this script from the new checkout, or remove the stale entry from HKCU\Environment\PSModulePath. Nothing was changed."
    } else {
        Write-Warning "This checkout does not look like a gim-tools repo layout. Nothing was registered."
    }
    exit 1
}

if (-not $IsUnix) {
    if ($entries -notcontains $ScriptsDir) {
        $newUserPath = (@($entries) + @($ScriptsDir)) -join $PathSep
        [Environment]::SetEnvironmentVariable('PSModulePath', $newUserPath, 'User')
        Write-Host "[i] Registered '$ScriptsDir' in User PSModulePath (HKCU\Environment)."
    } else {
        Write-Host "[i] '$ScriptsDir' is already an exact entry in User PSModulePath."
    }
} else {
    $quoted = $ScriptsDir.Replace("'", "''")
    $profileBlock = @"
# gim-tools:begin
`$gimToolsScripts = '$quoted'
`$gimModule = Join-Path `$gimToolsScripts (Join-Path 'GIMTools' 'GIMTools.psd1')
if (Test-Path -LiteralPath `$gimModule) {
    `$gimSep = [string][System.IO.Path]::PathSeparator
    `$gimEntries = @()
    if (`$env:PSModulePath) {
        `$gimEntries = @(`$env:PSModulePath -split [regex]::Escape(`$gimSep) | Where-Object { `$_ -ne '' })
    }
    if (`$gimEntries -notcontains `$gimToolsScripts) {
        `$env:PSModulePath = (@(`$gimToolsScripts) + @(`$gimEntries)) -join `$gimSep
    }
    Import-Module `$gimModule -DisableNameChecking
}
# gim-tools:end
"@
    $profilePath = Join-Path (Join-Path $HOME '.config/powershell') 'profile.ps1'
    Write-GimMarkedBlock -Path $profilePath -Begin '# gim-tools:begin' -End '# gim-tools:end' -Block $profileBlock
    Write-Host "[i] Registered '$ScriptsDir' in $profilePath (import on pwsh startup)."

    $profileSh = Join-Path $HOME '.profile'
    $shBlock = @"
# gim-tools:begin
case ":`${PSModulePath:-}:" in
  *":${ScriptsDir}:"*) ;;
  *) export PSModulePath="${ScriptsDir}`${PSModulePath:+:`$PSModulePath}" ;;
esac
# gim-tools:end
"@
    Write-GimMarkedBlock -Path $profileSh -Begin '# gim-tools:begin' -End '# gim-tools:end' -Block $shBlock
    Write-Host "[i] Exported PSModulePath from $profileSh for login shells."
}

$sessionEntries = @(Get-NonEmptyPathEntries $env:PSModulePath)
if ($sessionEntries -notcontains $ScriptsDir) {
    $env:PSModulePath = (@($ScriptsDir) + @($sessionEntries)) -join $PathSep
}
Import-Module $ModuleFile -DisableNameChecking

Write-Host "[i] Module file present: $ModuleFile"
Write-Host "OK gim-tools auto-load is set up. Restart opencode / Cursor so harness shells inherit the new environment."
