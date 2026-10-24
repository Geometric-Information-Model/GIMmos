# setup.ps1 - one-time User-level PSModulePath registration for the gim-tools
# module (plan 0004 S1). PowerShell auto-loads modules by name when the
# containing folder is on PSModulePath; registering this scripts/ folder makes
# every fresh pwsh auto-load GIMTools (folder name == module name), so agents
# stop needing a per-command import line (each harness bash call is a fresh
# pwsh; import state never persisted).
#
# User-scope only (HKCU\Environment, no admin). Run once per machine:
#   & ./.agents/skills/gim-tools/scripts/setup.ps1
# then restart opencode / Cursor so harness pwsh processes inherit the new env.
# Guide: docs/guides/0002-setup-gim-tools-autoload.md
#
# Trust note: this registers the checkout path in YOUR User PSModulePath, so
# GIMTools auto-loads in every pwsh you run. Only run it on machines where you
# trust the checkout; the toolkit dir stays deny-write for agents.
#
# Verify-then-register: the module file is checked BEFORE any registry/env
# write. A missing module file (bad checkout / repo moved) exits with a
# warning and leaves the User PSModulePath and the session untouched.

$ErrorActionPreference = 'Stop'

$ScriptsDir = $PSScriptRoot
$ModuleFile = Join-Path $ScriptsDir 'GIMTools\GIMTools.psd1'

function Get-NonEmptyPathEntries {
    param([string]$PathList)
    if (-not $PathList) { return @() }
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($part in $PathList -split ';') {
        if ($part -ne '') { $out.Add($part) }
    }
    return $out.ToArray()
}

# Read-only: needed to tell a stale entry (already registered) apart from a
# fresh checkout (no entry yet) when the module file is missing.
$entries = @(Get-NonEmptyPathEntries ([Environment]::GetEnvironmentVariable('PSModulePath', 'User')))

# Verify first. Do not register a dangling entry.
if (-not (Test-Path -LiteralPath $ModuleFile)) {
    Write-Warning "Module file not found at expected location: $ModuleFile"
    if ($entries -contains $ScriptsDir) {
        Write-Warning "Stale PSModulePath entry detected: User PSModulePath already contains '$ScriptsDir', but the module file is missing there. The repo may have moved; re-run this script from the new checkout, or remove the stale entry from HKCU\Environment\PSModulePath. Nothing was changed."
    } else {
        Write-Warning "This checkout does not look like a gim-tools repo layout. Nothing was registered."
    }
    exit 1
}

if ($entries -notcontains $ScriptsDir) {
    $newUserPath = (@($entries) + @($ScriptsDir)) -join ';'
    [Environment]::SetEnvironmentVariable('PSModulePath', $newUserPath, 'User')
    Write-Host "[i] Registered '$ScriptsDir' in User PSModulePath (HKCU\Environment)."
} else {
    Write-Host "[i] '$ScriptsDir' is already an exact entry in User PSModulePath."
}

# Update the current session's PSModulePath too, so the module resolves
# immediately in this shell without a restart.
$sessionEntries = @(Get-NonEmptyPathEntries $env:PSModulePath)
if ($sessionEntries -notcontains $ScriptsDir) {
    $env:PSModulePath = (@($sessionEntries) + @($ScriptsDir)) -join ';'
}

Write-Host "[i] Module file present: $ModuleFile"
Write-Host "OK gim-tools auto-load is set up. Restart opencode / Cursor so harness shells inherit the new environment."
