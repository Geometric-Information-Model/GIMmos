# GimVar.ps1 - full variable store (A5): storage-path convention, name
# validation (invariant 7), atomic temp+rename writes, TTL encoded in the
# FILENAME, expiry checked on every read, per-variable and namespace caps,
# opportunistic sweep, and an append-only audit log.
#
# Store layout (one file per variable - plan A5 "raw bytes, no envelope"):
#   <name>.t<ttl>.val   value bytes raw; ttl seconds in the filename suffix
#   <name>.val          legacy layout (no ttl suffix) - read/written by older
#                       store consumers and by the A3 gim-inspection fixture;
#                       treated as default-TTL for expiry and swept like any
#                       other var
#   _audit.log          append-only namespace audit (never counted in caps)
# TTL is derived from the filename, age from the file's last-write time, so a
# single temp+rename write is fully atomic (no val/meta pair to tear).
#
# Store root: %LOCALAPPDATA%\gim\vars\<sanitized-repo-root> where the repo
# root is encoded with '\' -> '_' and the drive colon stripped (e.g.
# D:\Work\GIM\GIMtransfer -> D__Work_GIM_GIMtransfer). GIM_VARS_ROOT env var
# overrides the root (must be a rooted local path; tests point it at a temp
# dir); -StoreRoot param wins over both.
#
# TTL policy (global, enforced on EVERY read incl. --from-var consumers):
# default 1 hour from last write, never refreshed on read; expired -> clear
# error, never a stale value; age always printed to stderr.

$script:GimVarDefaultTtl = 3600            # 1 hour from last write
$script:GimVarMaxValueBytes = 1048576      # 1 MiB per variable
$script:GimVarMaxNamespaceBytes = 10485760 # 10 MiB per namespace dir

function Test-GimStoreRoot {
    <#
    .SYNOPSIS
        Validate a store root: must be a rooted local path (drive letter).
        Relative, UNC, and env-var-containing values are rejected so the
        store never silently relocates.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )
    if ($Root -match '%[^%]+%') {
        Write-GimError "invalid store root '$Root': environment variables are not expanded (give the full path)"
    }
    if (-not [System.IO.Path]::IsPathRooted($Root)) {
        Write-GimError "invalid store root '$Root': must be an absolute path"
    }
    $full = [System.IO.Path]::GetFullPath($Root)
    $pathRoot = [System.IO.Path]::GetPathRoot($full)
    if ($null -eq $pathRoot -or $pathRoot.Length -ne 3 -or $pathRoot[1] -ne ':') {
        Write-GimError "invalid store root '$Root': must be a rooted local path (drive letter), not UNC"
    }
    return $full
}

function Get-GimVarStoreRoot {
    <#
    .SYNOPSIS
        Storage directory for gim variables.
    .DESCRIPTION
        Default: %LOCALAPPDATA%\gim\vars\<sanitized-repo-root> where the repo
        root is encoded with '\' -> '_' and the drive colon stripped. -Root
        overrides; otherwise the GIM_VARS_ROOT environment variable overrides
        (must be a rooted local path - validated; tests use a temp dir so
        nothing outside temp/scratch changes).
    #>
    param(
        [string]$Root
    )
    if ($Root) { return (Test-GimStoreRoot -Root $Root) }
    if ($env:GIM_VARS_ROOT) { return (Test-GimStoreRoot -Root $env:GIM_VARS_ROOT) }
    $repo = Get-GimRepoRoot
    $sanitized = ($repo -replace '\\', '_' -replace ':', '')
    return Join-Path (Join-Path $env:LOCALAPPDATA 'gim\vars') $sanitized
}

function Test-GimVarName {
    <#
    .SYNOPSIS
        True when the variable name matches ^[A-Za-z0-9_-]{1,64}$ (invariant 7).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    return ($Name -match '^[A-Za-z0-9_-]{1,64}$')
}

function Get-GimVarNameFromFile {
    <#
    .SYNOPSIS
        Parse a variable file name into @{ Name; Ttl; HasTtl } or $null.
    .DESCRIPTION
        <name>.t<ttl>.val carries its ttl; <name>.val is the legacy layout
        (Ttl $null, caller applies the default). Files that do not match
        either shape (audit log, stray temps) return $null.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )
    if ($FileName -match '^([A-Za-z0-9_-]{1,64})\.t(\d+)\.val$') {
        return [pscustomobject]@{ Name = $Matches[1]; Ttl = [int]$Matches[2]; HasTtl = $true }
    }
    if ($FileName -match '^([A-Za-z0-9_-]{1,64})\.val$') {
        return [pscustomobject]@{ Name = $Matches[1]; Ttl = $null; HasTtl = $false }
    }
    return $null
}

function Find-GimVarFile {
    <#
    .SYNOPSIS
        Path of a variable's current file (newest ttl variant, else legacy
        <name>.val), or $null when it does not exist.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$StoreRoot
    )
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    $variants = @()
    try {
        $variants = @(Get-ChildItem -LiteralPath $dir -Filter ($Name + '.t*.val') -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending)
    } catch {
        $variants = @()
    }
    if ($variants.Count -gt 0) { return $variants[0].FullName }
    $legacy = Join-Path $dir ($Name + '.val')
    if (Test-Path -LiteralPath $legacy) { return $legacy }
    return $null
}

function Write-GimVarAtomic {
    <#
    .SYNOPSIS
        Write a text file atomically: unique temp file + rename (Replace when
        the destination exists). A concurrent reader sees either the complete
        old value or the complete new value, never a torn file. The
        check-then-move race on concurrent first writes is tolerated by
        catching IOException and retrying against the current destination.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )
    $tmp = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    $backup = $Path + '.' + [guid]::NewGuid().ToString('N') + '.bak'
    [System.IO.File]::WriteAllText($tmp, $Content, (New-Object System.Text.UTF8Encoding($false)))
    $last = $null
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            if (Test-Path -LiteralPath $Path) {
                # Replace(source, dest, backup) is the atomic overwrite; the
                # backup must be a real path - PowerShell binds $null to '' and
                # .NET 10 rejects an empty backup ("path is empty").
                [System.IO.File]::Replace($tmp, $Path, $backup)
            } else {
                [System.IO.File]::Move($tmp, $Path)
            }
            $last = $null
            break
        } catch [System.IO.IOException] {
            # Concurrent writer created/removed the destination between the
            # check and the move, or briefly holds it open; back off
            # exponentially and retry (5 attempts).
            $last = $_
            if ($attempt -lt 5) { Start-Sleep -Milliseconds (25 * $attempt) }
        }
    }
    if ($last) {
        try { [System.IO.File]::Delete($tmp) } catch [System.IO.IOException] { }
        throw $last
    }
    if (Test-Path -LiteralPath $backup) {
        try { [System.IO.File]::Delete($backup) } catch [System.IO.IOException] { }
    }
}

function Add-GimVarAudit {
    <#
    .SYNOPSIS
        Append one line to the namespace's append-only _audit.log.
    #>
    param(
        [string]$StoreRoot,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $line = ([datetime]::UtcNow.ToString('o') + ' ' + $Message + "`n")
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            [System.IO.File]::AppendAllText((Join-Path $dir '_audit.log'), $line, (New-Object System.Text.UTF8Encoding($false)))
            return
        } catch [System.IO.IOException] {
            # Concurrent writers contend for the shared audit log; back off
            # briefly and retry. Audit is best-effort bookkeeping - after 5
            # attempts the store operation proceeds without the line rather
            # than failing the write.
            if ($attempt -lt 5) { Start-Sleep -Milliseconds (25 * $attempt) }
        }
    }
}

function Get-GimVarNamespaceBytes {
    <#
    .SYNOPSIS
        Total bytes of all variable files (ttl-suffixed + legacy .val) in the
        namespace - the 10 MiB cap input. _audit.log and stray temp files are
        not counted.
    #>
    param([string]$StoreRoot)
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { return 0L }
    $total = 0L
    foreach ($f in @(Get-ChildItem -LiteralPath $dir -Filter '*.val' -File -ErrorAction SilentlyContinue)) {
        $total += $f.Length
    }
    return $total
}

function Get-GimVarExpiry {
    <#
    .SYNOPSIS
        Expiry facts for a variable, or $null when it does not exist.
    .DESCRIPTION
        Returns @{ Name; TtlSeconds; Written; AgeSeconds; Expired }. TTL comes
        from the filename suffix; legacy <name>.val files use the default TTL.
        ttl <= 0 means never expires. Age is measured against UtcNow (never
        refreshed on read).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$StoreRoot
    )
    if (-not (Test-GimVarName -Name $Name)) {
        Write-GimError "invalid variable name '$Name' (must match ^[A-Za-z0-9_-]{1,64}$)"
    }
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    $file = Find-GimVarFile -Name $Name -StoreRoot $dir
    if ($null -eq $file) { return $null }
    $parsed = Get-GimVarNameFromFile -FileName (Split-Path -Leaf $file)
    $ttl = $script:GimVarDefaultTtl
    if ($parsed -and $parsed.HasTtl) { $ttl = $parsed.Ttl }
    try {
        $item = Get-Item -LiteralPath $file -Force -ErrorAction Stop
    } catch {
        # A concurrent writer removed the file between Find and read -
        # treat as not set rather than surfacing a raw error.
        return $null
    }
    $written = $item.LastWriteTimeUtc
    $age = [Math]::Max(0.0, ([datetime]::UtcNow - $written).TotalSeconds)
    $expired = ($ttl -gt 0 -and $age -gt $ttl)
    return [pscustomobject]@{ Name = $Name; TtlSeconds = $ttl; Written = $written; AgeSeconds = $age; Expired = $expired }
}

function Format-GimAge {
    <#
    .SYNOPSIS
        Human-readable duration: whole seconds under 60, minutes above.
    #>
    param([double]$Seconds)
    if ($Seconds -lt 60) { return ('{0}s' -f [int][Math]::Floor($Seconds)) }
    return ('{0}m' -f [int][Math]::Floor($Seconds / 60))
}

function Set-GimVarValue {
    <#
    .SYNOPSIS
        Write a variable value atomically (temp file + rename) as a single
        <name>.t<ttl>.val file, with caps (1 MiB per variable, 10 MiB
        namespace) and an audit line. Names are validated; anything else is
        rejected. Stale variants of the same name (other ttl suffixes, legacy
        <name>.val) are removed after the write. Returns stored bytes.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value,
        [string]$StoreRoot,
        [int]$Ttl = $script:GimVarDefaultTtl
    )
    if (-not (Test-GimVarName -Name $Name)) {
        Write-GimError "invalid variable name '$Name' (must match ^[A-Za-z0-9_-]{1,64}$)"
    }
    $bytes = [long][System.Text.Encoding]::UTF8.GetByteCount($Value)
    if ($bytes -gt $script:GimVarMaxValueBytes) {
        Write-GimError "value too large for '$Name' ($bytes bytes, cap 1 MiB)"
    }
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $existing = 0L
    $oldFile = Find-GimVarFile -Name $Name -StoreRoot $dir
    if ($oldFile) {
        try { $existing = (Get-Item -LiteralPath $oldFile -Force -ErrorAction Stop).Length } catch { $existing = 0L }
    }
    $projected = (Get-GimVarNamespaceBytes -StoreRoot $dir) - $existing + $bytes
    if ($projected -gt $script:GimVarMaxNamespaceBytes) {
        Write-GimError "variable namespace cap exceeded ($projected bytes projected, cap 10 MiB)"
    }
    $final = Join-Path $dir ($Name + '.t' + $Ttl + '.val')
    Write-GimVarAtomic -Path $final -Content $Value
    foreach ($f in @(Get-ChildItem -LiteralPath $dir -Filter ($Name + '.*.val') -File -ErrorAction SilentlyContinue)) {
        if ($f.FullName -ne $final) {
            try { [System.IO.File]::Delete($f.FullName) } catch [System.IO.IOException] { }
        }
    }
    $legacy = Join-Path $dir ($Name + '.val')
    if ((Test-Path -LiteralPath $legacy) -and ($legacy -ne $final)) {
        try { [System.IO.File]::Delete($legacy) } catch [System.IO.IOException] { }
    }
    Add-GimVarAudit -StoreRoot $dir -Message ("set $Name $bytes B ttl=$Ttl")
    return $bytes
}

function Get-GimVarValue {
    <#
    .SYNOPSIS
        Read a variable value, or $null when the variable does not exist.
        Raw value, no TTL check (consumers use Get-GimVarValueEnforced; this
        stays raw for internal callers that already checked expiry).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$StoreRoot
    )
    if (-not (Test-GimVarName -Name $Name)) {
        Write-GimError "invalid variable name '$Name' (must match ^[A-Za-z0-9_-]{1,64}$)"
    }
    $file = Find-GimVarFile -Name $Name -StoreRoot $StoreRoot
    if ($null -eq $file) { return $null }
    try {
        return [System.IO.File]::ReadAllText($file)
    } catch [System.IO.IOException] {
        # Concurrent writer removed the file between Find and read.
        return $null
    }
}

function Get-GimVarValueEnforced {
    <#
    .SYNOPSIS
        Shared consumer read (gim-get and every --from-var consumer): enforces
        expiry identically everywhere. TTL checked on read; expired values
        error with 'expired (age N, ttl N)' and are never returned; age always
        printed to stderr. Returns $null when the variable is missing (callers
        decide the missing-variable message), the raw value otherwise.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$StoreRoot
    )
    $exp = Get-GimVarExpiry -Name $Name -StoreRoot $StoreRoot
    if ($null -eq $exp) { return $null }
    [Console]::Error.WriteLine(('gim: age {0}, ttl {1}' -f (Format-GimAge $exp.AgeSeconds), (Format-GimAge $exp.TtlSeconds)))
    if ($exp.Expired) {
        Write-GimError ('expired (age {0}, ttl {1})' -f (Format-GimAge $exp.AgeSeconds), (Format-GimAge $exp.TtlSeconds))
    }
    return Get-GimVarValue -Name $Name -StoreRoot $StoreRoot
}

function Remove-GimVarFiles {
    <#
    .SYNOPSIS
        Delete every file belonging to a variable (ttl variants + legacy).
        No audit (callers audit).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$StoreRoot
    )
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    foreach ($f in @(Get-ChildItem -LiteralPath $dir -Filter ($Name + '.*.val') -File -ErrorAction SilentlyContinue)) {
        try { [System.IO.File]::Delete($f.FullName) } catch [System.IO.IOException] { }
    }
    $legacy = Join-Path $dir ($Name + '.val')
    if (Test-Path -LiteralPath $legacy) {
        try { [System.IO.File]::Delete($legacy) } catch [System.IO.IOException] { }
    }
}

function Remove-GimVarValue {
    <#
    .SYNOPSIS
        Remove one variable (files + audit). Returns $true when it existed.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [string]$StoreRoot
    )
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    $existed = $null -ne (Find-GimVarFile -Name $Name -StoreRoot $dir)
    Remove-GimVarFiles -Name $Name -StoreRoot $dir
    Add-GimVarAudit -StoreRoot $dir -Message ('clear ' + $Name)
    return $existed
}

function Remove-GimVarExpired {
    <#
    .SYNOPSIS
        Opportunistic sweep: delete every expired variable in the namespace
        (ttl-suffixed files and legacy .val files, which use the default TTL).
        Returns the number removed. Never touches non-expired vars.
    #>
    param([string]$StoreRoot)
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { return 0 }
    $removed = 0
    foreach ($valFile in @(Get-ChildItem -LiteralPath $dir -Filter '*.val' -File -ErrorAction SilentlyContinue)) {
        $parsed = Get-GimVarNameFromFile -FileName $valFile.Name
        if ($null -eq $parsed -or -not (Test-GimVarName -Name $parsed.Name)) { continue }
        $exp = Get-GimVarExpiry -Name $parsed.Name -StoreRoot $dir
        if ($exp -and $exp.Expired) {
            Remove-GimVarFiles -Name $parsed.Name -StoreRoot $dir
            Add-GimVarAudit -StoreRoot $dir -Message ('swept ' + $parsed.Name)
            $removed++
        }
    }
    return $removed
}

function Remove-GimAllVars {
    <#
    .SYNOPSIS
        Remove every variable in the namespace (files + one audit line).
        Returns the number removed. Leaves _audit.log and stray files alone.
    #>
    param([string]$StoreRoot)
    $dir = Get-GimVarStoreRoot -Root $StoreRoot
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { return 0 }
    $removed = 0
    $seen = @{}
    foreach ($valFile in @(Get-ChildItem -LiteralPath $dir -Filter '*.val' -File -ErrorAction SilentlyContinue)) {
        $parsed = Get-GimVarNameFromFile -FileName $valFile.Name
        if ($null -eq $parsed -or -not (Test-GimVarName -Name $parsed.Name)) { continue }
        if ($seen.ContainsKey($parsed.Name)) { continue }
        $seen[$parsed.Name] = $true
        Remove-GimVarFiles -Name $parsed.Name -StoreRoot $dir
        $removed++
    }
    Add-GimVarAudit -StoreRoot $dir -Message ('clear-all ' + $removed)
    return $removed
}
