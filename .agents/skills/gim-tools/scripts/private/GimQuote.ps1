# GimQuote.ps1 - argv assembly helper.
# No shell interpolation: agent text becomes a quoted argv token for
# ProcessStartInfo.Arguments (Windows CommandLineToArgvW rules).

function Quote-Arg {
    <#
    .SYNOPSIS
        Quote a single argument for a Windows command line (argv), never a shell.
    .DESCRIPTION
        Returns the argument quoted per CommandLineToArgvW parsing rules:
        - empty string -> "" (two double quotes)
        - no whitespace/quotes -> unchanged
        - otherwise wrapped in double quotes; embedded quotes get backslash-escaped;
          backslashes before a quote or at the end of the argument are doubled.
    .EXAMPLE
        Quote-Arg 'a b'   -> "a b"
        Quote-Arg 'x"y'   -> "x\"y"
    #>
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [AllowEmptyString()]
        [string]$Argument
    )

    if ($Argument -eq '') { return '""' }
    if ($Argument -notmatch '[ \t\n\v"]') { return $Argument }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    $i = 0
    $len = $Argument.Length
    while ($i -lt $len) {
        $bs = 0
        while ($i -lt $len -and $Argument[$i] -eq '\') { $bs++; $i++ }
        if ($i -ge $len) {
            # Trailing backslashes: double them, then close the quote.
            [void]$sb.Append(('\' * (2 * $bs)))
            [void]$sb.Append('"')
            return $sb.ToString()
        }
        if ($Argument[$i] -eq '"') {
            # Backslashes before a quote are doubled, quote is escaped.
            [void]$sb.Append(('\' * (2 * $bs + 1)))
            [void]$sb.Append('"')
            $i++
        } else {
            [void]$sb.Append(('\' * $bs))
            [void]$sb.Append($Argument[$i])
            $i++
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}
