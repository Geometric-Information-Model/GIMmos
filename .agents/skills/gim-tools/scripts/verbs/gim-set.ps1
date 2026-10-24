# gim-set.ps1 - store a pipeline stream in the variable store and pass it
# through (tee). Also handles --clear <name> / --clear-all removal. Store:
# %LOCALAPPDATA%\gim\vars\<sanitized-repo-root>\<name>.t<ttl>.val (raw bytes,
# no envelope; ttl in the filename) + append-only _audit.log.
# Invariant 6: stored values are data, never argv; consumers read them with
# --<param>-from-var <name> (gim-get writes stdout only).

function gim-set {
    <#
    .SYNOPSIS
        Store a pipeline stream in the variable store and pass it through.
    .DESCRIPTION
        TEES: the incoming stream is buffered, validated against the 1 MiB
        per-variable cap, stored as one raw file
        %LOCALAPPDATA%\gim\vars\<sanitized-repo-root>\<name>.t<ttl>.val, and
        ONLY then passed through (a cap failure emits nothing and exits
        non-zero - no half-consumed tee). With --json the stream is stored
        but not passed through; the result envelope { name, ttl,
        stored_bytes } is the only output. --clear <name> / --clear-all remove
        variable(s); --ttl <sec> sets the lifetime (default 1 hour from last
        write, never refreshed on read; 0 = never expires, negative values
        rejected). Expired variables in the namespace are swept on every call.
        Caps: 1 MiB per variable, 10 MiB per namespace (enforced on write,
        projected). Names starting with '-' are accepted (the PowerShell
        binder strips the POSIX '--' marker itself): gim-set -- -name and
        gim-set -name both store a leading-dash name.
    .PARAMETER Raw
        Positional name plus '--flag value' options (see DESCRIPTION):
        --clear <name>, --clear-all, --ttl <sec>, --json.
    .EXAMPLE
        gim-list --glob "src/**/*.cs" | gim-inspect --hash | gim-set baseline
    .EXAMPLE
        gim-set --clear baseline
    .EXAMPLE
        "v" | gim-set -- -leading-dash-name
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
                '--clear' = 'Clear'
                '--ttl'   = 'Ttl'
            } `
            -SwitchFlags @{
                '--clear-all' = 'ClearAll'
                '--json'      = 'Json'
            }
        $pos = @($parsed.Positional)
        $name = ''
        if ($pos.Count -gt 0) { $name = [string]$pos[0] }
        if ($pos.Count -gt 1) {
            Write-GimError "too many arguments: '$($pos -join ' ')' (expected one variable name)"
        }
        $clearName = ''
        if ($parsed.ContainsKey('Clear')) { $clearName = [string]$parsed['Clear'] }
        $clearAll = $parsed.ContainsKey('ClearAll')
        $jsonMode = $parsed.ContainsKey('Json')
        if ($name) {
            if ($clearName -or $clearAll) {
                Write-GimError 'give either a name to set or --clear/--clear-all, not both'
            }
        } elseif (-not $clearName -and -not $clearAll) {
            Write-GimError 'give a name to set, or --clear <name> / --clear-all'
        }
        if ($clearName -and $clearAll) {
            Write-GimError 'give either --clear <name> or --clear-all, not both'
        }
        $ttl = $script:GimVarDefaultTtl
        if ($parsed.ContainsKey('Ttl')) {
            $ttlRaw = [string]$parsed['Ttl']
            if (-not [int]::TryParse($ttlRaw, [ref]$ttl)) {
                Write-GimError "invalid --ttl value: '$ttlRaw' (expected whole seconds)"
            }
            if ($ttl -lt 0) {
                Write-GimError "invalid --ttl value: '$ttlRaw' (negative ttl not allowed)"
            }
        }
        if ($name -and -not (Test-GimVarName -Name $name)) {
            Write-GimError "invalid variable name '$name' (must match ^[A-Za-z0-9_-]{1,64}$)"
        }
        if ($clearName -and -not (Test-GimVarName -Name $clearName)) {
            Write-GimError "invalid variable name '$clearName' (must match ^[A-Za-z0-9_-]{1,64}$)"
        }
        $lines = New-Object System.Collections.Generic.List[string]
    }
    process {
        # Buffer only - the cap check and store must succeed before the tee
        # emits anything (bounded by the 1 MiB per-variable cap, so cheap).
        if ($null -ne $Item) {
            [void]$lines.Add([string]$Item)
        }
    }
    end {
        $storeRoot = Get-GimVarStoreRoot
        [void](Remove-GimVarExpired -StoreRoot $storeRoot)
        if ($clearAll) {
            $removed = Remove-GimAllVars -StoreRoot $storeRoot
            if ($jsonMode) { Write-GimJson ([pscustomobject]@{ cleared = $removed }) }
            return
        }
        if ($clearName) {
            $existed = Remove-GimVarValue -Name $clearName -StoreRoot $storeRoot
            if ($jsonMode) { Write-GimJson ([pscustomobject]@{ name = $clearName; cleared = $existed }) }
            return
        }
        $value = [string]::Join("`n", @($lines.ToArray()))
        $storedBytes = Set-GimVarValue -Name $name -Value $value -StoreRoot $storeRoot -Ttl $ttl
        if ($jsonMode) {
            Write-GimJson ([pscustomobject]@{ name = $name; ttl = $ttl; stored_bytes = $storedBytes })
            return
        }
        foreach ($l in $lines) { Write-Output $l }
    }
}
