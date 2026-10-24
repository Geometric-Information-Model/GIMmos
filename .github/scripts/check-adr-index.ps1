<#
.SYNOPSIS
Checks that repository ADR decision files are indexed.

.DESCRIPTION
Validates the solution-level ADR index without scanning nested directories.
The script resolves paths from its own location, so callers may run it from
any working directory.

.NOTES
Pass/fail rules:
- Pass when docs/adr/ is absent.
- Otherwise, docs/adr/0000-INDEX.md must exist.
- NNNN-*.md decision numbers must be unique, and every other direct ADR
  decision file must link from the index.
- 0000-INDEX.md is the index, not a decision number. docs/adr/agents/ is
  ignored.
#>

$ErrorActionPreference = 'Stop'

# Anchor repository discovery to script location, not caller working directory.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$adrDirectory = Join-Path $repoRoot 'docs\adr'

if (-not (Test-Path -LiteralPath $adrDirectory -PathType Container)) {
    Write-Host 'ADR index check passed: docs/adr is absent.'
    exit 0
}

$indexPath = Join-Path $adrDirectory '0000-INDEX.md'
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw 'ADR index check failed: docs/adr/0000-INDEX.md is missing.'
}

# Get-ChildItem stays non-recursive, so docs/adr/agents/ is ignored.
$indexText = Get-Content -LiteralPath $indexPath -Raw
$decisionFiles = @(Get-ChildItem -LiteralPath $adrDirectory -File -Filter '*.md')
$decisions = @()

foreach ($file in $decisionFiles) {
    # The index is metadata, not an ADR decision.
    if ($file.Name -eq '0000-INDEX.md') {
        continue
    }

    # Only directly stored NNNN-*.md files participate in decision checks.
    $match = [regex]::Match($file.Name, '^(?<number>\d{4})-.+\.md$')
    if ($match.Success) {
        $decisions += [pscustomobject]@{
            FileName = $file.Name
            Number = $match.Groups['number'].Value
        }
    }
}

$seenNumbers = @{}
foreach ($decision in $decisions) {
    # Duplicate numbers make ADR references ambiguous.
    if ($seenNumbers.ContainsKey($decision.Number)) {
        throw "ADR index check failed: duplicate decision number $($decision.Number)."
    }

    $seenNumbers[$decision.Number] = $decision.FileName
    # Require a same-directory markdown link targeting this exact filename.
    $escapedFileName = [regex]::Escape($decision.FileName)
    $linkPattern = '\]\(\s*' + $escapedFileName + '\s*\)'
    if ($indexText -notmatch $linkPattern) {
        throw "ADR index check failed: $($decision.FileName) is not linked from docs/adr/0000-INDEX.md."
    }
}

Write-Host "ADR index check passed: $($decisions.Count) decision file(s)."
