# gim-report.ps1 - shapes/aggregates a stream: line/table/json output,
# --sort (replaces Sort-Object), --prefix / --prefix-each (replaces label
# loops), --only-failures.

function Test-GimFailure {
    # Failure notion for --only-failures: strings matching fail/error/warn,
    # records with failed=$true or a status not in ok/pass/success.
    param($Item)
    if ($Item -is [string]) { return ($Item -match '(?i)fail|error|warn') }
    $failedProp = $Item.PSObject.Properties['failed']
    if ($failedProp -and ($failedProp.Value -eq $true)) { return $true }
    $statusProp = $Item.PSObject.Properties['status']
    if ($statusProp -and ($statusProp.Value -is [string])) {
        return ($statusProp.Value -notin @('ok', 'pass', 'success'))
    }
    return $false
}

function Write-GimTable {
    # Render items as an aligned table, then enforce the same bounded-output
    # caps as lines/json: item cap (lines incl. header) and byte cap with the
    # explicit truncation marker. --prefix-each prefixes every emitted line.
    param([object[]]$Items, [string]$Prefix, [bool]$PrefixEach, [int]$MaxItems = $script:GimMaxItems, [long]$MaxBytes = $script:GimMaxBytes)
    $prefixCol = ($PrefixEach -and $Prefix)
    $lines = New-Object System.Collections.Generic.List[string]
    if ($Items.Count -eq 0) {
        return
    }
    if ($Items[0] -is [string]) {
        foreach ($s in $Items) {
            if ($prefixCol) { [void]$lines.Add($Prefix + ': ' + $s) } else { [void]$lines.Add($s) }
        }
    } else {
        $names = New-Object System.Collections.Generic.List[string]
        foreach ($item in $Items) {
            foreach ($prop in $item.PSObject.Properties) {
                if (-not $names.Contains($prop.Name)) { [void]$names.Add($prop.Name) }
            }
        }
        $widths = @{}
        foreach ($n in $names) {
            $w = $n.Length
            foreach ($item in $Items) {
                $v = [string]$item.PSObject.Properties[$n].Value
                if ($v.Length -gt $w) { $w = $v.Length }
            }
            $widths[$n] = $w
        }
        $header = @()
        foreach ($n in $names) { $header += $n.PadRight($widths[$n]) }
        [void]$lines.Add(($header -join ' '))
        foreach ($item in $Items) {
            $cells = @()
            foreach ($n in $names) {
                $v = [string]$item.PSObject.Properties[$n].Value
                $cells += $v.PadRight($widths[$n])
            }
            [void]$lines.Add(($cells -join ' '))
        }
        if ($prefixCol) {
            $prefixed = New-Object System.Collections.Generic.List[string]
            foreach ($l in $lines) { [void]$prefixed.Add($Prefix + ' | ' + $l) }
            $lines = $prefixed
        }
    }
    $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $MaxItems -MaxBytes $MaxBytes
    foreach ($line in $fmt.Lines) { Write-Output $line }
    if ($fmt.Truncated) { Write-GimTruncationMarker -Items $Items.Count }
}

function gim-report {
    <#
    .SYNOPSIS
        Shape/aggregate a stream: lines (default), table, or json.
    .DESCRIPTION
        Lines: objects become one-line JSON. --prefix <label> prefixes every
        emitted line with '<label>: ' (the label-loop replacement);
        --prefix-each is the explicit per-item form and additionally carries
        the label into structured output (table label column / json label
        field on records; bare strings stay strings in json). --sort <field>
        sorts strings lexicographically or records by property (bare --sort
        sorts strings; records need a field). --only-failures keeps failing
        strings/records. --json is the same as --format json. All formats
        (lines, table, json) are bounded to 500 items / 1 MiB with an explicit
        # gim: output truncated marker (json/truncated marker goes to stderr
        for the json byte cap so the document stays single).
    .PARAMETER Raw
        '--flag value' options only (see DESCRIPTION). --prefix <label>,
        --prefix-each, --sort <field>, --format lines|table|json,
        --only-failures, --json.
    .EXAMPLE
        gim-list --glob "docs/adr/*.md" | gim-where --is-file | gim-report --prefix ADR
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
                '--prefix' = 'Prefix'
                '--format' = 'Format'
            } `
            -OptionalValueFlags @{
                '--sort' = 'Sort'
            } `
            -SwitchFlags @{
                '--prefix-each'   = 'PrefixEach'
                '--only-failures' = 'OnlyFailures'
                '--json'          = 'Json'
            }
        $Prefix = ''
        if ($parsed.ContainsKey('Prefix')) { $Prefix = [string]$parsed['Prefix'] }
        $SortProvided = $parsed.ContainsKey('Sort')
        $Sort = ''
        if ($SortProvided) { $Sort = [string]$parsed['Sort'] }
        $Format = 'lines'
        if ($parsed.ContainsKey('Format')) {
            $f = [string]$parsed['Format']
            if ($f -notin @('lines', 'table', 'json')) { Write-GimError "invalid --format value: '$f' (expected lines|table|json)" }
            $Format = $f
        }
        $PrefixEach = $parsed.ContainsKey('PrefixEach')
        $OnlyFailures = $parsed.ContainsKey('OnlyFailures')
        $Json = $parsed.ContainsKey('Json')

        $caps = Get-GimCaps
        $items = New-Object System.Collections.Generic.List[object]
    }
    process {
        if ($null -ne $Item) { [void]$items.Add($Item) }
    }
    end {
        $list = @($items.ToArray())
        if ($OnlyFailures) {
            $kept = New-Object System.Collections.Generic.List[object]
            foreach ($i in $list) { if (Test-GimFailure -Item $i) { [void]$kept.Add($i) } }
            $list = @($kept.ToArray())
        }
        if ($SortProvided) {
            if ($list.Count -gt 0 -and $list[0] -is [string]) {
                $strs = [string[]]$list
                [System.Array]::Sort($strs)
                $list = @($strs)
            } else {
                if ($Sort -eq '') { Write-GimError '--sort on records needs a field (e.g. --sort name)' }
                $list = @($list | Sort-Object -Property $Sort)
            }
        }

        $format = $Format
        if ($Json) { $format = 'json' }

        switch ($format) {
            'json' {
                $envItems = $list
                if ($PrefixEach -and $Prefix) {
                    $wrapped = New-Object System.Collections.Generic.List[object]
                    foreach ($i in $list) {
                        if ($i -is [string]) { [void]$wrapped.Add($i) }
                        else {
                            $obj = $i | ConvertTo-Json -Depth 6 | ConvertFrom-Json
                            $obj | Add-Member -NotePropertyName 'label' -NotePropertyValue $Prefix -Force
                            [void]$wrapped.Add($obj)
                        }
                    }
                    $envItems = @($wrapped.ToArray())
                }
                Write-GimJson (New-GimJsonEnvelope -Items $envItems -MaxItems $caps.MaxItems)
                break
            }
            'table' {
                Write-GimTable -Items $list -Prefix $Prefix -PrefixEach $PrefixEach -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
                break
            }
            default {
                $fmt = Format-GimLines -Items $list -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes -Prefix $Prefix
                foreach ($line in $fmt.Lines) { Write-Output $line }
                if ($fmt.Truncated) { Write-GimTruncationMarker -Items $list.Count }
            }
        }
    }
}
