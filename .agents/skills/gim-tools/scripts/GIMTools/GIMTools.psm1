# GIMTools.psm1 - gim-* agent toolkit module (Phase 1 complete: A1-A9 +
# Z1-Z2 - spine, search, inspection, capability, state, mutating, execution,
# meta, harness checklist, full smoke + wrap-up fixes).
# Data-only verbs. No Invoke-Expression / Add-Type / Start-Process / &$var.
# Process launch (gim-run, gim-probe) goes through
# [System.Diagnostics.Process] only.

# Module layout (plan 0004 S1): the module lives at scripts/GIMTools/ so
# PowerShell auto-loads it by name; roots.json, private/, and verbs/ stay in
# the parent scripts/ folder (module root = the scripts/ dir).
$script:GimModuleRoot = Split-Path -Parent $PSScriptRoot

# Shared private arg parser: canonical gim-* invocations use '--flag value'
# style (e.g. 'gim-list --glob "docs/adr/*.md"'). PowerShell treats '--' as
# end-of-parameters, so every verb collects the raw tail via
# ValueFromRemainingArguments and parses it here.
function Convert-GimArgs {
    <#
    .SYNOPSIS
        Parse '--flag value' / '--switch' arguments into a hashtable.
    .DESCRIPTION
        -ValueFlags maps '--name' to a result key and consumes the next token
        as its value (repeatable flags collect an array).
        -SwitchFlags maps '--name' to a result key and sets it to $true.
        -OptionalValueFlags maps '--name' to a result key and consumes the next
        token only when it is not itself a flag (e.g. '--sort' alone).
        Bare tokens collect under the 'Positional' list. Unknown option-like
        tokens also collect under 'Positional' (leading-dash names stay
        reachable when the binder strips '--'); missing values are errors via
        Write-GimError.
    #>
    param(
        [string[]]$Raw = @(),
        [hashtable]$ValueFlags = @{},
        [hashtable]$SwitchFlags = @{},
        [hashtable]$OptionalValueFlags = @{}
    )
    if ($null -eq $Raw) { $Raw = @() }
    $result = @{ Positional = New-Object System.Collections.Generic.List[string] }
    $i = 0
    while ($i -lt $Raw.Count) {
        $tok = $Raw[$i]
        if ($tok -eq '--') {
            # POSIX end-of-options: everything after '--' is positional.
            # NOTE: through normal verb invocation the PowerShell binder
            # consumes the '--' sentinel itself, so this branch only fires for
            # direct Convert-GimArgs calls; the binder still routes a
            # leading-dash token ('-name') into $Raw, where the unknown-option
            # fallback below treats it as positional.
            $i++
            while ($i -lt $Raw.Count) { $result.Positional.Add($Raw[$i]); $i++ }
            break
        }
        if ($tok -match '^--?[A-Za-z][A-Za-z0-9-]*$') {
            $name = $tok.TrimStart('-')
            $full = '--' + $name
            if ($ValueFlags.ContainsKey($full)) {
                if ($i + 1 -ge $Raw.Count) { Write-GimError "missing value for '$tok'" }
                $val = $Raw[$i + 1]
                $key = $ValueFlags[$full]
                if ($result.ContainsKey($key)) {
                    if ($result[$key] -is [string]) { $result[$key] = @($result[$key]) }
                    $result[$key] = @($result[$key]) + $val
                } else {
                    $result[$key] = $val
                }
                $i += 2
                continue
            }
            if ($OptionalValueFlags.ContainsKey($full)) {
                # Value optional: consume the next token only when it is not
                # itself a flag (e.g. '--sort' alone vs '--sort name').
                $key = $OptionalValueFlags[$full]
                $val = ''
                if ($i + 1 -lt $Raw.Count -and $Raw[$i + 1] -notmatch '^--?[A-Za-z]') {
                    $val = $Raw[$i + 1]
                    $i++
                }
                $result[$key] = $val
                $i++
                continue
            }
            if ($SwitchFlags.ContainsKey($full)) {
                $result[$SwitchFlags[$full]] = $true
                $i++
                continue
            }
            # Unknown option-like token: valid variable names may start with a
            # dash (invariant 7 allows '-'), and the binder has already
            # stripped the '--' marker, so treat it as positional instead of
            # erroring (names that collide with a verb's own flags remain
            # unreachable - inherent CLI ambiguity, same as POSIX).
            $result.Positional.Add($tok)
            $i++
            continue
        }
        $result.Positional.Add($tok)
        $i++
    }
    return $result
}

# Private helpers first (module-private; not exported). The helpers stay in
# the parent scripts/ folder, so dot-source via $script:GimModuleRoot.
. (Join-Path $script:GimModuleRoot 'private\GimQuote.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimOutput.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimPaths.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimVar.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimEncoding.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimExec.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimFleetExec.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimFleetText.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimFleetJira.ps1')
. (Join-Path $script:GimModuleRoot 'private\GimVerbs.ps1')

# Verbs (one file per verb).
. (Join-Path $script:GimModuleRoot 'verbs\gim-list.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-where.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-report.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-grep.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-inspect.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-head.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-count.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-diff.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-json.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-asm.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-set.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-get.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-fix.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-write.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-apply.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-run.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-probe.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-preflight.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-pr.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-jira-search.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-jira-create.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-jira-transition.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-wrapup-gather.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-wrapup-respond.ps1')
. (Join-Path $script:GimModuleRoot 'verbs\gim-verbs.ps1')

Export-ModuleMember -Function @('gim-list','gim-where','gim-report','gim-grep','gim-inspect','gim-head','gim-count','gim-diff','gim-json','gim-asm','gim-set','gim-get','gim-fix','gim-write','gim-apply','gim-run','gim-probe','gim-preflight','gim-pr','gim-jira-search','gim-jira-create','gim-jira-transition','gim-wrapup-gather','gim-wrapup-respond','gim-verbs')
