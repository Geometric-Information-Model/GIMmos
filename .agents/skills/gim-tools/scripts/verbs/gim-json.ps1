# gim-json.ps1 - JSON document query (JSON-only build: YAML/TOML inputs are
# rejected with a clear unsupported-format error). Serves as the
# Select-Object -ExpandProperty replacement: --path returns the scalar or
# collection value at the queried node.

function Convert-GimJsonPathTokens {
    # Split a dotted/bracket query ('a.b[0].c') into property-name tokens and
    # integer index tokens.
    param([string]$Query)
    $tokens = @()
    $rx = New-Object System.Text.RegularExpressions.Regex '([^.\[\]]+)|\[(\d+)\]'
    foreach ($m in $rx.Matches($Query)) {
        if ($m.Groups[1].Value -ne '') {
            $tokens += $m.Groups[1].Value
        } else {
            $tokens += [int]$m.Groups[2].Value
        }
    }
    return $tokens
}

function Get-GimJsonNode {
    # Walk a parsed JSON object following the query tokens.
    param($Node, [string]$Query)
    if ([string]::IsNullOrWhiteSpace($Query)) { return $Node }
    $tokens = Convert-GimJsonPathTokens $Query
    if ($tokens.Count -eq 0) {
        Write-GimError "invalid --path query: '$Query'"
    }
    foreach ($t in $tokens) {
        if ($null -eq $Node) { break }
        if ($t -is [int]) {
            $Node = $Node[$t]
        } else {
            $Node = $Node.$t
        }
    }
    return $Node
}

function Get-GimJsonKeys {
    # Keys of an object node (property names) or indices of an array node.
    # Scalar/leaf nodes (string, number, bool, null) have no keys - return
    # empty so --keys on a leaf never leaks .NET instance properties.
    param($Node)
    if ($null -eq $Node) { return @() }
    if ($Node -is [System.Array]) {
        $keys = @()
        for ($i = 0; $i -lt $Node.Count; $i++) { $keys += [string]$i }
        return $keys
    }
    if ($Node -is [string] -or $Node -is [int] -or $Node -is [long] -or $Node -is [double] -or $Node -is [bool]) {
        return @()
    }
    $keys = @()
    foreach ($prop in $Node.PSObject.Properties) { $keys += $prop.Name }
    return $keys
}

function Format-GimJsonValue {
    # Text rendering of a queried value: scalar as-is, array expanded one
    # element per line, object as one-line JSON.
    param($Value)
    if ($null -eq $Value) { return '' }
    if ($Value -is [System.Array]) {
        $lines = @()
        foreach ($element in $Value) {
            if ($element -is [string] -or $element -is [int] -or $element -is [long] -or $element -is [double] -or $element -is [bool]) {
                $lines += [string]$element
            } else {
                $lines += ($element | ConvertTo-Json -Depth 10 -Compress)
            }
        }
        return $lines
    }
    if ($Value -is [string] -or $Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [bool]) {
        return @([string]$Value)
    }
    return @($Value | ConvertTo-Json -Depth 10 -Compress)
}

function gim-json {
    <#
    .SYNOPSIS
        Query JSON documents with a dotted/bracket path.
    .DESCRIPTION
        Input: paths to JSON files (positional or pipeline). A missing path or
        directory is an error (invariant 9; only gim-inspect reports
        kind=missing). YAML (.yaml/.yml) and TOML (.toml) inputs are rejected
        with 'unsupported format: <ext>' (JSON-only build). --path <query>
        selects a node (dotted/bracket, e.g. 'a.b[0].c'); without --path the
        whole document is the node. --keys lists the keys (property names, or
        array indices) at the queried node. --validate checks the document
        parses and reports { path, valid } instead of querying. Text output
        renders the queried value (scalar as-is, arrays expanded one per line,
        objects as one-line JSON); --json emits { count, truncated, items } of
        { path, value } records (or key strings with --keys). Bounded to
        500 records / 1 MiB with an explicit # gim: output truncated marker.
    .PARAMETER Raw
        Positional JSON paths plus '--flag value' options (see DESCRIPTION).
        --path <query>, --keys, --validate, --json.
    .EXAMPLE
        gim-json appsettings.json --path "ConnectionStrings:Default" --keys
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
                '--path' = 'Path'
            } `
            -SwitchFlags @{
                '--keys'     = 'Keys'
                '--validate' = 'Validate'
                '--json'     = 'Json'
            }
        $jsonMode = $parsed.ContainsKey('Json')
        $keysMode = $parsed.ContainsKey('Keys')
        $validateMode = $parsed.ContainsKey('Validate')
        $query = ''
        if ($parsed.ContainsKey('Path')) { $query = [string]$parsed['Path'] }
        $caps = Get-GimCaps
        $pathItems = New-Object System.Collections.Generic.List[string]
        foreach ($p in @($parsed.Positional)) {
            if (-not [string]::IsNullOrWhiteSpace($p)) { [void]$pathItems.Add($p) }
        }
    }
    process {
        if ($null -ne $Item) { [void]$pathItems.Add([string]$Item) }
    }
    end {
        if ($pathItems.Count -eq 0) {
            Write-GimError 'no input: give JSON paths (positional or pipeline)'
        }
        $records = New-Object System.Collections.Generic.List[object]
        foreach ($p in $pathItems) {
            $real = Resolve-ContainedPath -Path $p
            if ([System.IO.Directory]::Exists($real)) {
                Write-GimError "path is a directory: $p"
            }
            $ext = [System.IO.Path]::GetExtension($real).TrimStart('.').ToLowerInvariant()
            if ($ext -in @('yaml', 'yml', 'toml')) {
                Write-GimError "unsupported format: $ext (gim-json is a JSON-only build)"
            }
            $reader = New-Object System.IO.StreamReader($real, [System.Text.Encoding]::UTF8, $true)
            $text = $reader.ReadToEnd()
            $reader.Dispose()
            $doc = $null
            try {
                $doc = ConvertFrom-Json -InputObject $text
            }
            catch {
                if ($validateMode) {
                    $records.Add([pscustomobject]@{ path = $real; valid = $false })
                    continue
                }
                Write-GimError "invalid JSON: $real"
            }
            if ($validateMode) {
                $records.Add([pscustomobject]@{ path = $real; valid = $true })
                continue
            }
            $node = Get-GimJsonNode -Node $doc -Query $query
            if ($keysMode) {
                $keys = @(Get-GimJsonKeys -Node $node)
                foreach ($key in $keys) {
                    $records.Add([pscustomobject]@{ path = $real; key = $key })
                }
                continue
            }
            $records.Add([pscustomobject]@{ path = $real; value = $node })
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $outLines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            if ($rec.PSObject.Properties['valid']) {
                $outLines.Add(($rec.path + ': valid=' + $rec.valid))
            } elseif ($rec.PSObject.Properties['key']) {
                $outLines.Add([string]$rec.key)
            } else {
                foreach ($v in @(Format-GimJsonValue -Value $rec.value)) {
                    $outLines.Add($v)
                }
            }
        }
        $fmt = Format-GimLines -Items @($outLines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $records.Count }
    }
}
