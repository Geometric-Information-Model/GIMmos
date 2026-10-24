# gim-where.ps1 - pipeline filter: predicates apply to a path item or to a
# named field of a record (works downstream of gim-grep / gim-* JSON verbs).

function Convert-GimDateTime {
    param([string]$Text)
    $d = [datetime]::MinValue
    if ([datetime]::TryParse($Text, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$d)) {
        return $d
    }
    return $null
}

function Test-GimEmptyValue {
    param($Value, [string]$Path)
    if ($Path -and (Test-Path -LiteralPath $Path -ErrorAction SilentlyContinue)) {
        if (Test-Path -LiteralPath $Path -PathType Container) {
            return (@(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)).Count -eq 0
        }
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        if ($item) { return $item.Length -eq 0 }
    }
    if ($Value -is [string]) { return $Value.Length -eq 0 }
    return $false
}

function Test-GimWhereItem {
    # Evaluates all provided predicates against one item. Predicates AND.
    param(
        $Item,
        [string]$Field,
        [switch]$Exists,
        [switch]$Missing,
        [switch]$Empty,
        [switch]$NonEmpty,
        [string]$Matches,
        [string]$NotMatches,
        [long]$LargerThan,
        [long]$SmallerThan,
        [string]$NewerThan,
        [string]$OlderThan,
        [string]$Ext,
        [switch]$IsFile,
        [switch]$IsDir
    )
    $isString = ($Item -is [string])
    if ($Field) {
        if ($isString) {
            $value = $Item
        } else {
            $prop = $Item.PSObject.Properties[$Field]
            if ($prop) { $value = $prop.Value } else { $value = '' }
        }
    } else {
        $value = $Item
    }
    if ($isString) { $path = $Item } elseif ($Field) { $path = [string]$value } else { $path = $null }

    $ok = $true
    if ($ok -and $Exists)   { $ok = (Test-Path -LiteralPath $path -ErrorAction SilentlyContinue) }
    if ($ok -and $Missing)  { $ok = (-not (Test-Path -LiteralPath $path -ErrorAction SilentlyContinue)) }
    if ($ok -and $Empty)    { $ok = (Test-GimEmptyValue -Value $value -Path $path) }
    if ($ok -and $NonEmpty) { $ok = (-not (Test-GimEmptyValue -Value $value -Path $path)) }
    if ($ok -and $Matches)    { $ok = [regex]::IsMatch([string]$value, $Matches) }
    if ($ok -and $NotMatches) { $ok = (-not [regex]::IsMatch([string]$value, $NotMatches)) }
    if ($ok -and $PSBoundParameters.ContainsKey('LargerThan')) {
        if ($path -and (Test-Path -LiteralPath $path -PathType Leaf -ErrorAction SilentlyContinue)) {
            $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            $ok = ($item -and $item.Length -gt $LargerThan)
        } else {
            # Not an existing file: only a numeric field value (--field) can
            # satisfy a size predicate; anything else is excluded, never
            # compared by string length.
            $num = 0L
            if ([long]::TryParse([string]$value, [ref]$num)) { $ok = ($num -gt $LargerThan) }
            else { $ok = $false }
        }
    }
    if ($ok -and $PSBoundParameters.ContainsKey('SmallerThan')) {
        if ($path -and (Test-Path -LiteralPath $path -PathType Leaf -ErrorAction SilentlyContinue)) {
            $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            $ok = ($item -and $item.Length -lt $SmallerThan)
        } else {
            $num = 0L
            if ([long]::TryParse([string]$value, [ref]$num)) { $ok = ($num -lt $SmallerThan) }
            else { $ok = $false }
        }
    }
    if ($ok -and $NewerThan) {
        if ($path -and (Test-Path -LiteralPath $path -ErrorAction SilentlyContinue)) {
            $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            $ok = ($item -and $item.LastWriteTime -gt (Convert-GimDateTime -Text $NewerThan))
        } else {
            $d = Convert-GimDateTime -Text ([string]$value)
            $ok = ($null -ne $d -and $d -gt (Convert-GimDateTime -Text $NewerThan))
        }
    }
    if ($ok -and $OlderThan) {
        if ($path -and (Test-Path -LiteralPath $path -ErrorAction SilentlyContinue)) {
            $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            $ok = ($item -and $item.LastWriteTime -lt (Convert-GimDateTime -Text $OlderThan))
        } else {
            $d = Convert-GimDateTime -Text ([string]$value)
            $ok = ($null -ne $d -and $d -lt (Convert-GimDateTime -Text $OlderThan))
        }
    }
    if ($ok -and $Ext) {
        $ext = $Ext.TrimStart('.')
        if ($path -and (Test-Path -LiteralPath $path -ErrorAction SilentlyContinue)) {
            $ok = (([System.IO.Path]::GetExtension($path)).TrimStart('.') -eq $ext)
        } else {
            $ok = $false
        }
    }
    if ($ok -and $IsFile) { $ok = (Test-Path -LiteralPath $path -PathType Leaf -ErrorAction SilentlyContinue) }
    if ($ok -and $IsDir)  { $ok = (Test-Path -LiteralPath $path -PathType Container -ErrorAction SilentlyContinue) }
    return $ok
}

function gim-where {
    <#
    .SYNOPSIS
        Filter a pipeline stream by predicates on the item or a named field.
    .DESCRIPTION
        With a bare string item (e.g. from gim-list), predicates hit the
        filesystem. With records (e.g. gim-grep --json), --field <name> picks
        the value tested; for bare strings --field is ignored. Predicates AND
        together. --larger-than/--smaller-than compare the file length for a
        path item, or the numeric field value with --field; items that are
        neither an existing file nor numeric never satisfy a size predicate
        (missing paths are excluded, not compared by string length).
        Output shape matches input; --json wraps in
        { count, truncated, items }.
    .PARAMETER Raw
        '--flag value' options only (see DESCRIPTION). Predicates:
        --exists, --missing, --empty, --non-empty, --matches <re>,
        --not-matches <re>, --larger-than <n>, --smaller-than <n>,
        --newer-than <iso>, --older-than <iso>, --ext <e>, --is-file,
        --is-dir, --field <name>, --json.
    .EXAMPLE
        gim-list --glob "docs/adr/*.md" | gim-where --is-file
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
                '--matches'     = 'Matches'
                '--not-matches'  = 'NotMatches'
                '--larger-than'  = 'LargerThan'
                '--smaller-than' = 'SmallerThan'
                '--newer-than'  = 'NewerThan'
                '--older-than'  = 'OlderThan'
                '--ext'         = 'Ext'
                '--field'       = 'Field'
            } `
            -SwitchFlags @{
                '--exists'   = 'Exists'
                '--missing'  = 'Missing'
                '--empty'    = 'Empty'
                '--non-empty' = 'NonEmpty'
                '--is-file'  = 'IsFile'
                '--is-dir'   = 'IsDir'
                '--json'     = 'Json'
            }

        $caps = Get-GimCaps
        $found = New-Object System.Collections.Generic.List[object]

        # Validate numeric/date/regex values up front so bad data errors before streaming.
        if ($parsed.ContainsKey('LargerThan')) {
            $n = 0L
            if (-not [long]::TryParse([string]$parsed['LargerThan'], [ref]$n)) {
                Write-GimError "invalid --larger-than value: '$($parsed['LargerThan'])'"
            }
            $parsed['LargerThan'] = $n
        }
        if ($parsed.ContainsKey('SmallerThan')) {
            $n = 0L
            if (-not [long]::TryParse([string]$parsed['SmallerThan'], [ref]$n)) {
                Write-GimError "invalid --smaller-than value: '$($parsed['SmallerThan'])'"
            }
            $parsed['SmallerThan'] = $n
        }
        if ($parsed.ContainsKey('Matches')) {
            try { $null = New-Object System.Text.RegularExpressions.Regex ([string]$parsed['Matches']) }
            catch { Write-GimError "invalid --matches regex: '$($parsed['Matches'])'" }
        }
        if ($parsed.ContainsKey('NotMatches')) {
            try { $null = New-Object System.Text.RegularExpressions.Regex ([string]$parsed['NotMatches']) }
            catch { Write-GimError "invalid --not-matches regex: '$($parsed['NotMatches'])'" }
        }
        if ($parsed.ContainsKey('NewerThan')) {
            if (-not (Convert-GimDateTime -Text ([string]$parsed['NewerThan']))) { Write-GimError "invalid --newer-than timestamp: '$($parsed['NewerThan'])'" }
        }
        if ($parsed.ContainsKey('OlderThan')) {
            if (-not (Convert-GimDateTime -Text ([string]$parsed['OlderThan']))) { Write-GimError "invalid --older-than timestamp: '$($parsed['OlderThan'])'" }
        }
    }
    process {
        if ($null -eq $Item) { return }
        $pred = @{ Item = $Item }
        foreach ($key in @('Exists','Missing','Empty','NonEmpty','IsFile','IsDir')) {
            if ($parsed.ContainsKey($key)) { $pred[$key] = $true }
        }
        foreach ($key in @('Matches','NotMatches','LargerThan','SmallerThan','NewerThan','OlderThan','Ext','Field')) {
            if ($parsed.ContainsKey($key)) { $pred[$key] = $parsed[$key] }
        }
        if (Test-GimWhereItem @pred) {
            [void]$found.Add($Item)
        }
    }
    end {
        $items = @($found.ToArray())
        if ($parsed.ContainsKey('Json')) {
            Write-GimJson (New-GimJsonEnvelope -Items $items -MaxItems $caps.MaxItems)
            return
        }
        $fmt = Format-GimLines -Items $items -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $items.Count }
    }
}
