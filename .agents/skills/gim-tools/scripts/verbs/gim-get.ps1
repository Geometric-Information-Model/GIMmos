# gim-get.ps1 - read a stored variable as a head-only pipeline source.
# TTL is checked ON READ: an expired variable exits non-zero and never emits
# its value; age is always written to stderr. Invariant 6: this verb writes
# stdout only; consuming parameters use --<param>-from-var <name> (the module
# reads its own store and revalidates containment at every use).

function gim-get {
    <#
    .SYNOPSIS
        Read a stored variable value (head-only pipeline source).
    .DESCRIPTION
        Emits the raw stored value as one pipeline item per line (a value with
        embedded newlines flows as separate items; an empty value emits a
        single empty item). Age is always printed to stderr. TTL (default 1
        hour from last write, never refreshed on read) is checked on read:
        expired values exit non-zero with 'expired (age N, ttl N)' on stderr
        and never emit the value. --max-age <sec> demands the value be at most
        that old. --json emits { name, value, age_seconds, ttl_seconds,
        expired }.
    .PARAMETER Raw
        Positional name plus '--flag value' options (see DESCRIPTION):
        --max-age <sec>, --json.
    .EXAMPLE
        gim-list --glob "src/**/*.cs" | gim-inspect --hash | gim-set baseline
        gim-list --glob "src/**/*.cs" | gim-inspect --hash | gim-diff --against-var baseline
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
                '--max-age' = 'MaxAge'
            } `
            -SwitchFlags @{
                '--json' = 'Json'
            }
        $pos = @($parsed.Positional)
        if ($pos.Count -ne 1) {
            if ($pos.Count -eq 0) {
                Write-GimError 'missing variable name: expected "gim-get <name>"'
            }
            Write-GimError "too many arguments: '$($pos -join ' ')' (expected one variable name)"
        }
        $name = [string]$pos[0]
        if (-not (Test-GimVarName -Name $name)) {
            Write-GimError "invalid variable name '$name' (must match ^[A-Za-z0-9_-]{1,64}$)"
        }
        $jsonMode = $parsed.ContainsKey('Json')
        $maxAge = -1
        if ($parsed.ContainsKey('MaxAge')) {
            $maxAgeRaw = [string]$parsed['MaxAge']
            if (-not [int]::TryParse($maxAgeRaw, [ref]$maxAge) -or $maxAge -lt 0) {
                Write-GimError "invalid --max-age value: '$maxAgeRaw' (expected non-negative whole seconds)"
            }
        }
    }
    end {
        $storeRoot = Get-GimVarStoreRoot
        $exp = Get-GimVarExpiry -Name $name -StoreRoot $storeRoot
        if ($null -eq $exp) {
            Write-GimError "variable not set: '$name' (use gim-set to store it)"
        }
        [Console]::Error.WriteLine(('gim: age {0}, ttl {1}' -f (Format-GimAge $exp.AgeSeconds), (Format-GimAge $exp.TtlSeconds)))
        if ($exp.Expired) {
            Write-GimError ('expired (age {0}, ttl {1})' -f (Format-GimAge $exp.AgeSeconds), (Format-GimAge $exp.TtlSeconds))
        }
        if ($maxAge -ge 0 -and $exp.AgeSeconds -gt $maxAge) {
            Write-GimError ('older than --max-age {0}s (age {1})' -f $maxAge, (Format-GimAge $exp.AgeSeconds))
        }
        $value = Get-GimVarValue -Name $name -StoreRoot $storeRoot
        if ($jsonMode) {
            Write-GimJson ([pscustomobject]@{
                name        = $name
                value       = $value
                age_seconds = [int][Math]::Floor($exp.AgeSeconds)
                ttl_seconds = $exp.TtlSeconds
                expired     = $exp.Expired
            })
            return
        }
        foreach ($line in @($value -split '\r?\n')) { Write-Output $line }
    }
}
