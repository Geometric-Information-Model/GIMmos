# gim-verbs.ps1 - meta verb (A8): emits the full gim-* vocabulary as JSON
# (--json) or renders the per-group reference docs into references/ (--gen-refs)
# from the shared definition table in private/GimVerbs.ps1. Data-only, like
# every verb; --gen-refs writes files only inside the current repo.

function gim-verbs {
    <#
    .SYNOPSIS
        Emit the full verb vocabulary (--json) or write per-group reference
        docs (--gen-refs).
    .DESCRIPTION
        Modes:
          --json     - emit the vocabulary from the shared definition table:
                       { count, truncated, items } of { name, group,
                       signature, params, description } for all 20 verbs.
          --gen-refs - write references/<group>.md per group (spine, search,
                       inspection, capability, state, mutating, execution,
                       fleet, meta) with per-verb flag detail. The renderer is
                       deterministic: re-running produces byte-identical
                       files (no timestamps, no machine paths; UTF-8 without
                       BOM, LF newlines). --out-dir <d> redirects the output
                       (default .agents/skills/gim-tools/references) for
                       scratch/test runs; the directory must resolve inside
                       the current repo (write containment).
        Exactly one mode is required. Text output for --gen-refs is one
        'wrote <path>' line per group file; --json output is the standard
        envelope. Bounded to 500 items / 1 MiB like every verb.
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION): --json, --gen-refs,
        --out-dir <d>.
    .EXAMPLE
        gim-verbs --json | ConvertFrom-Json
    .EXAMPLE
        gim-verbs --gen-refs
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
                '--out-dir' = 'OutDir'
            } `
            -SwitchFlags @{
                '--json'    = 'Json'
                '--gen-refs' = 'GenRefs'
            }
        if ($parsed.Positional.Count -gt 0) {
            Write-GimError "unexpected arguments: '$($parsed.Positional -join ' ')' (gim-verbs takes --json or --gen-refs)"
        }
        $jsonMode = $parsed.ContainsKey('Json')
        $genRefs = $parsed.ContainsKey('GenRefs')
        if ($jsonMode -and $genRefs) {
            Write-GimError 'give either --json or --gen-refs, not both'
        }
        if (-not $jsonMode -and -not $genRefs) {
            Write-GimError 'missing mode: give --json or --gen-refs'
        }
        $outDir = ''
        if ($parsed.ContainsKey('OutDir')) { $outDir = [string]$parsed['OutDir'] }
        $caps = Get-GimCaps
    }
    end {
        $defs = Get-GimVerbDefinitions
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items $defs -MaxItems $caps.MaxItems)
            return
        }
        $dir = $outDir
        if (-not $dir) {
            $dir = Join-Path (Split-Path -Parent $script:GimModuleRoot) 'references'
        }
        $written = @(Write-GimVerbRefDocs -Definitions $defs -OutDir $dir)
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($p in $written) { $lines.Add('wrote ' + $p) }
        $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $written.Count }
    }
}
