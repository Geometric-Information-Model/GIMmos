# gim-inspect.ps1 - byte-level file inspection with no built-in equivalent.
# Missing paths report kind=missing (never error); directory paths report
# kind=directory. Records: { path, kind, <requested fields> }. Text renders
# 'path: kind=file field=value ...' so the info is visible in text pipelines.
# Encoding/line-ending facts come from the shared GimEncoding helpers
# (Get-GimTextCodec / Read-GimTextBytes / Get-GimLineEndingsName).

function Get-GimInspectRecord {
    param([string]$Path, [hashtable]$Flags)
    $real = Resolve-ContainedPath -Path $Path -AllowMissing
    $rec = @{ path = $real }
    if (-not (Test-Path -LiteralPath $real)) {
        $rec['kind'] = 'missing'
        return [pscustomobject]$rec
    }
    if ([System.IO.Directory]::Exists($real)) {
        $rec['kind'] = 'directory'
        if ($Flags.ContainsKey('Mtime')) {
            $rec['mtime'] = (Get-Item -LiteralPath $real -Force).LastWriteTime
        }
        return [pscustomobject]$rec
    }
    $rec['kind'] = 'file'
    $bytes = [System.IO.File]::ReadAllBytes($real)
    if ($Flags.ContainsKey('Bytes')) { $rec['bytes'] = $bytes.Length }
    if ($Flags.ContainsKey('Bom')) { $rec['bom'] = (Get-GimTextCodec -Bytes $bytes).Name -ne 'utf-8' }
    if ($Flags.ContainsKey('Encoding')) { $rec['encoding'] = (Get-GimTextCodec -Bytes $bytes).Name }
    if ($Flags.ContainsKey('Binary')) {
        $binary = $false
        foreach ($b in $bytes) { if ($b -eq 0) { $binary = $true; break } }
        $rec['binary'] = $binary
    }
    $text = $null
    if ($Flags.ContainsKey('Lines') -or $Flags.ContainsKey('LineEndings') -or $Flags.ContainsKey('EofNewline') -or
        $Flags.ContainsKey('Tabs') -or $Flags.ContainsKey('TrailingWs') -or $Flags.ContainsKey('MaxLineLen')) {
        $text = Read-GimTextBytes -Bytes $bytes
    }
    if ($Flags.ContainsKey('Tabs')) { $rec['tabs'] = $text.Contains("`t") }
    if ($Flags.ContainsKey('Lines')) {
        if ($text.Length -eq 0) { $rec['lines'] = 0 }
        else {
            $nl = [regex]::Matches($text, '\r\n|\n|\r').Count
            if ($text.EndsWith("`n") -or $text.EndsWith("`r")) { $rec['lines'] = $nl }
            else { $rec['lines'] = $nl + 1 }
        }
    }
    if ($Flags.ContainsKey('LineEndings')) {
        $rec['lineEndings'] = Get-GimLineEndingsName -Text $text
    }
    if ($Flags.ContainsKey('EofNewline')) {
        $rec['eofNewline'] = ($text.EndsWith("`n") -or $text.EndsWith("`r"))
    }
    if ($Flags.ContainsKey('TrailingWs')) {
        $rec['trailingWs'] = [regex]::IsMatch($text, '(?m)[ \t]+\r?$')
    }
    if ($Flags.ContainsKey('MaxLineLen')) {
        $max = 0
        foreach ($line in ($text -split '\r\n|\n|\r')) {
            if ($line.Length -gt $max) { $max = $line.Length }
        }
        $rec['maxLineLen'] = $max
    }
    if ($Flags.ContainsKey('Mtime')) {
        $rec['mtime'] = (Get-Item -LiteralPath $real -Force).LastWriteTime
    }
    if ($Flags.ContainsKey('Hash')) {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $rec['hash'] = [System.BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
        } finally {
            $sha.Dispose()
        }
    }
    return [pscustomobject]$rec
}

function Format-GimInspectLine {
    param($Record, [string[]]$FieldOrder)
    $parts = @()
    foreach ($f in $FieldOrder) {
        $p = $Record.PSObject.Properties[$f]
        if ($null -ne $p) { $parts += ($f + '=' + $p.Value) }
    }
    return ($Record.path + ': ' + ($parts -join ' '))
}

function gim-inspect {
    <#
    .SYNOPSIS
        Byte-level inspection of paths (files, directories, missing).
    .DESCRIPTION
        Input: paths (positional or pipeline). Missing paths report
        kind=missing and never error (invariant 9); directories report
        kind=directory. Check flags add fields to the record; with no check
        flags only kind is reported. --hash emits the SHA256 hex of file bytes.
        --json emits { count, truncated, items } of records. Bounded to
        500 items / 1 MiB with an explicit # gim: output truncated marker.
    .PARAMETER Raw
        Positional literal paths plus '--flag value' options (see DESCRIPTION).
        Checks: --eof-newline, --tabs, --line-endings, --lines, --bytes,
        --encoding, --bom, --trailing-ws, --max-line-len, --binary, --kind,
        --mtime, --hash, --json.
    .EXAMPLE
        gim-inspect .gitignore --bytes --lines --hash
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
            -SwitchFlags @{
                '--eof-newline'  = 'EofNewline'
                '--tabs'         = 'Tabs'
                '--line-endings' = 'LineEndings'
                '--lines'        = 'Lines'
                '--bytes'        = 'Bytes'
                '--encoding'     = 'Encoding'
                '--bom'          = 'Bom'
                '--trailing-ws'  = 'TrailingWs'
                '--max-line-len' = 'MaxLineLen'
                '--binary'       = 'Binary'
                '--kind'         = 'Kind'
                '--mtime'        = 'Mtime'
                '--hash'         = 'Hash'
                '--json'         = 'Json'
            }
        $jsonMode = $parsed.ContainsKey('Json')
        $flags = @{}
        foreach ($k in @('EofNewline','Tabs','LineEndings','Lines','Bytes','Encoding','Bom','TrailingWs','MaxLineLen','Binary','Kind','Mtime','Hash')) {
            if ($parsed.ContainsKey($k)) { $flags[$k] = $true }
        }
        $fieldOrder = @('kind')
        foreach ($f in @('bytes','lines','lineEndings','eofNewline','tabs','trailingWs','maxLineLen','binary','bom','encoding','mtime','hash')) {
            $keyMap = @{
                'bytes' = 'Bytes'; 'lines' = 'Lines'; 'lineEndings' = 'LineEndings'
                'eofNewline' = 'EofNewline'; 'tabs' = 'Tabs'; 'trailingWs' = 'TrailingWs'
                'maxLineLen' = 'MaxLineLen'; 'binary' = 'Binary'; 'bom' = 'Bom'
                'encoding' = 'Encoding'; 'mtime' = 'Mtime'; 'hash' = 'Hash'
            }
            if ($flags.ContainsKey($keyMap[$f])) { $fieldOrder += $f }
        }
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
            Write-GimError 'no input: give paths (positional or pipeline)'
        }
        $records = New-Object System.Collections.Generic.List[object]
        foreach ($p in $pathItems) {
            $records.Add((Get-GimInspectRecord -Path $p -Flags $flags))
        }
        if ($jsonMode) {
            Write-GimJson (New-GimJsonEnvelope -Items @($records.ToArray()) -MaxItems $caps.MaxItems)
            return
        }
        $lines = New-Object System.Collections.Generic.List[string]
        foreach ($rec in $records) {
            $lines.Add((Format-GimInspectLine -Record $rec -FieldOrder $fieldOrder))
        }
        $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
        foreach ($line in $fmt.Lines) { Write-Output $line }
        if ($fmt.Truncated) { Write-GimTruncationMarker -Items $records.Count }
    }
}
