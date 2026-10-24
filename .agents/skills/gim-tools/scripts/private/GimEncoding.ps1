# GimEncoding.ps1 - encoding helpers for the mutating verbs (A6): the
# gim-write --encoding vocabulary (utf8 / utf8bom / utf16) and a BOM-detecting
# text codec for gim-fix (rewrite in place while preserving the file's
# existing encoding). Default is UTF-8 without BOM (invariant 10; PS 5.1
# `>`/`Out-File` emit UTF-16 - the gim-write --encoding flag exists because of
# that).

function Get-GimEncodingByName {
    <#
    .SYNOPSIS
        Encoding object for the gim-write --encoding vocabulary.
    .DESCRIPTION
        utf8    -> UTF-8 without BOM (default; invariant 10)
        utf8bom -> UTF-8 with BOM
        utf16   -> UTF-16 LE with BOM
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    switch ($Name) {
        'utf8'    { return (New-Object System.Text.UTF8Encoding($false)) }
        'utf8bom' { return (New-Object System.Text.UTF8Encoding($true)) }
        'utf16'   { return [System.Text.Encoding]::Unicode }
    }
    Write-GimError "invalid encoding name '$Name' (expected utf8, utf8bom, or utf16)"
}

function Get-GimTextCodec {
    <#
    .SYNOPSIS
        BOM-detecting text codec for a byte buffer.
    .DESCRIPTION
        Returns @{ Encoding; HasBom; Name } where Name mirrors gim-inspect
        --encoding output (utf-8-bom / utf-16le / utf-16be / utf-8). Decoding
        with the codec and re-encoding with the same codec round-trips the
        original BOM.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes
    )
    if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
        return @{ Encoding = (New-Object System.Text.UTF8Encoding($true)); HasBom = $true; Name = 'utf-8-bom' }
    }
    if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) {
        return @{ Encoding = [System.Text.Encoding]::Unicode; HasBom = $true; Name = 'utf-16le' }
    }
    if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) {
        return @{ Encoding = [System.Text.Encoding]::BigEndianUnicode; HasBom = $true; Name = 'utf-16be' }
    }
    return @{ Encoding = (New-Object System.Text.UTF8Encoding($false)); HasBom = $false; Name = 'utf-8' }
}

function Read-GimTextBytes {
    <#
    .SYNOPSIS
        Decode a byte buffer to text with BOM detection (same semantics as
        gim-inspect's text reader, kept local so mutating verbs do not depend
        on another verb file's private functions).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes
    )
    $stream = New-Object System.IO.MemoryStream(, $Bytes)
    $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true)
    try {
        return $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
    }
}

function Get-GimLineEndingsName {
    <#
    .SYNOPSIS
        Line-ending classification shared by gim-inspect --line-endings and
        gim-fix: crlf / lf / cr / mixed / none.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )
    $crlf = [regex]::Matches($Text, '\r\n').Count
    $lf = [regex]::Matches($Text, '(?<!\r)\n').Count
    $cr = [regex]::Matches($Text, '\r(?!\n)').Count
    if ($crlf -eq 0 -and $lf -eq 0 -and $cr -eq 0) { return 'none' }
    if ($lf -eq 0 -and $cr -eq 0) { return 'crlf' }
    if ($crlf -eq 0 -and $cr -eq 0) { return 'lf' }
    if ($crlf -eq 0 -and $lf -eq 0) { return 'cr' }
    return 'mixed'
}
