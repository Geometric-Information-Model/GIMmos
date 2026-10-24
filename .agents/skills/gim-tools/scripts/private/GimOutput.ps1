# GimOutput.ps1 - output helpers: JSON emission, bounded-output caps,
# truncation marker, and Write-GimError (stderr + non-zero exit via throw).

$script:GimMaxItems = 500
$script:GimMaxBytes = 1048576   # 1 MiB

function Write-GimError {
    <#
    .SYNOPSIS
        Write an error message to stderr and terminate with a non-zero exit.
    .DESCRIPTION
        Writes 'gim: <message>' to the host error stream and throws a terminating
        error so the calling process exits non-zero (unhandled terminating error
        from powershell -Command yields exit code 1).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    $msg = 'gim: ' + $Message
    $Host.UI.WriteErrorLine($msg)
    throw $msg
}

function Get-GimCaps {
    <#
    .SYNOPSIS
        Default bounded-output caps: 500 items, 1 MiB of text output.
    #>
    param(
        [int]$MaxItems = $script:GimMaxItems,
        [long]$MaxBytes = $script:GimMaxBytes
    )
    return [pscustomobject]@{ MaxItems = $MaxItems; MaxBytes = $MaxBytes }
}

function Format-GimLines {
    <#
    .SYNOPSIS
        Shape a list of items into capped, optionally prefixed text lines.
    .DESCRIPTION
        Returns @{ Lines = <string list>; Truncated = <bool> }. Byte cap is
        approximate UTF-8 bytes per line plus newline. Objects are rendered as
        one-line JSON so structured records survive into text pipelines.
    #>
    param(
        [AllowEmptyCollection()]
        [object[]]$Items,
        [int]$MaxItems = $script:GimMaxItems,
        [long]$MaxBytes = $script:GimMaxBytes,
        [string]$Prefix = ''
    )
    $lines = New-Object System.Collections.Generic.List[string]
    $bytes = 0L
    $truncated = $false
    for ($i = 0; $i -lt $Items.Count; $i++) {
        if ($i -ge $MaxItems) { $truncated = $true; break }
        $item = $Items[$i]
        if ($item -is [string]) { $line = $item } else { $line = ($item | ConvertTo-Json -Depth 6 -Compress) }
        if ($Prefix -ne '') { $line = $Prefix + ': ' + $line }
        $lineBytes = [System.Text.Encoding]::UTF8.GetByteCount($line) + 2
        if (($bytes + $lineBytes) -gt $MaxBytes) { $truncated = $true; break }
        [void]$lines.Add($line)
        $bytes += $lineBytes
    }
    return @{ Lines = $lines; Truncated = $truncated }
}

function Write-GimTruncationMarker {
    <#
    .SYNOPSIS
        Emit the explicit '# gim: output truncated (items N, bytes M)' marker
        line for text output (there is no --truncated flag; the marker is
        automatic when a cap is hit).
    #>
    param(
        [long]$Items = 0,
        [long]$Bytes = 0
    )
    Write-Output ('# gim: output truncated (items {0}, bytes {1})' -f $Items, $Bytes)
}

function New-GimJsonEnvelope {
    <#
    .SYNOPSIS
        Wrap items in a structured JSON envelope with count/truncated.
    .DESCRIPTION
        JSON output shape for gim-* verbs: { count, truncated, items }.
        Items are capped at MaxItems so JSON always stays parseable.
    #>
    param(
        [AllowEmptyCollection()]
        [object[]]$Items,
        [int]$MaxItems = $script:GimMaxItems
    )
    $truncated = $Items.Count -gt $MaxItems
    if ($truncated) { $kept = $Items[0..($MaxItems - 1)] } else { $kept = $Items }
    return [pscustomobject]@{ count = $Items.Count; truncated = $truncated; items = @($kept) }
}

function Write-GimJson {
    <#
    .SYNOPSIS
        Emit an object as JSON text (ConvertFrom-Json round-trippable when not
        byte-truncated).
    .DESCRIPTION
        Enforces the byte cap: a JSON document larger than MaxBytes is cut and
        the truncation marker goes to stderr so the JSON stream stays a single
        document (parseable or clearly truncated), never an extra stdout line.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $InputObject,
        [long]$MaxBytes = $script:GimMaxBytes
    )
    $json = ConvertTo-Json -InputObject $InputObject -Depth 10
    $bytes = [System.Text.Encoding]::UTF8.GetByteCount($json)
    if ($bytes -gt $MaxBytes) {
        [Console]::Error.WriteLine(('gim: truncated: json output byte cap exceeded ({0} bytes, cap {1})' -f $bytes, $MaxBytes))
        return $json.Substring(0, [Math]::Min($MaxBytes, $json.Length))
    }
    return $json
}
