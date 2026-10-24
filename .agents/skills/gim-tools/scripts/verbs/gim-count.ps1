# gim-count.ps1 - count pipeline items, optionally grouped by a record field
# (replaces Group-Object { } + count loops, denied). No positional arguments:
# it is a pure pipeline verb.

function gim-count {
    <#
    .SYNOPSIS
        Count pipeline items; with --by <field>, group counts per field value.
    .DESCRIPTION
        Input: pipeline items only (no positional arguments). Without --by the
        text output is the bare count; --json emits the standard
        { count, truncated, items } envelope with a single { count: N } record.
        With --by, items are grouped by the named record field (for bare string
        items the string itself is the group); text output is 'group: N' per
        group sorted by group; --json emits { count, truncated, items } of
        { group, count } records. Bounded to 500 groups with an explicit
        # gim: output truncated marker.
    .PARAMETER Raw
        '--flag value' options only (see DESCRIPTION). --by <field>, --json.
    .EXAMPLE
        gim-list --glob "docs/adr/*.md" | gim-count
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
                '--by' = 'By'
            } `
            -SwitchFlags @{
                '--json' = 'Json'
            }
        if (@($parsed.Positional).Count -gt 0) {
            Write-GimError 'gim-count takes no positional arguments'
        }
        $jsonMode = $parsed.ContainsKey('Json')
        $by = ''
        if ($parsed.ContainsKey('By')) { $by = [string]$parsed['By'] }
        $caps = Get-GimCaps
        $items = New-Object System.Collections.Generic.List[object]
    }
    process {
        if ($null -ne $Item) { [void]$items.Add($Item) }
    }
    end {
        $list = @($items.ToArray())
        if (-not $by) {
            $count = $list.Count
            if ($jsonMode) {
                Write-GimJson (New-GimJsonEnvelope -Items @([pscustomobject]@{ count = $count }) -MaxItems $caps.MaxItems)
            } else {
                Write-Output $count
            }
            return
        }
        $groups = @{}
        foreach ($item in $list) {
            $value = $null
            if ($item -is [string]) {
                $value = $item
            } else {
                $prop = $item.PSObject.Properties[$by]
                if ($null -ne $prop) { $value = $prop.Value } else { $value = '' }
            }
            $key = [string]$value
            if ($groups.ContainsKey($key)) { $groups[$key]++ } else { $groups[$key] = 1 }
        }
        $records = New-Object System.Collections.Generic.List[object]
        foreach ($key in @($groups.Keys | Sort-Object)) {
            $records.Add([pscustomobject]@{ group = $key; count = $groups[$key] })
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            $lines.Add(($rec.group + ': ' + $rec.count))
        }
        $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $records.Count }
    }
}
