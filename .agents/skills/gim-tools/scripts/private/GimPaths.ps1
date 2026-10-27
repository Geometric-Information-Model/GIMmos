# GimPaths.ps1 - path helpers: repo root discovery, real-path resolution with
# symlink handling, roots.json loading (env expansion), containment checks,
# the gim-apply temp-root safety floor, and Resolve-ContainedPath used by
# every verb that touches paths.
# Separators: Windows '\', Linux '/'. Both are accepted on every platform.

function Split-GimPathSegments {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Relative
    )
    return @($Relative -split '[\\/]' | Where-Object { $_ -ne '' })
}

function Get-GimRepoRoot {
    <#
    .SYNOPSIS
        Locate the GIMmos repo root by walking up from the module dir
        until a .git marker is found. Returns the real path so a symlink
        checkout (GIMmos -> GIMtransfer) compares equal to resolved files.
    #>
    $dir = $script:GimModuleRoot
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir '.git')) {
            return (Get-GimRealPath -Path $dir)
        }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    Write-GimError "could not locate repo root (.git) above module path '$script:GimModuleRoot'"
}

function Get-GimRealPath {
    <#
    .SYNOPSIS
        Resolve a path to its real full path, following reparse points
        (symlinks / junctions) at every component. Missing final components
        are kept lexically so --allow-missing callers still get a stable path.
    #>
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Path,
        [int]$Depth = 0
    )
    if ($Depth -gt 16) { Write-GimError "symlink resolution exceeded depth limit at '$Path'" }

    $full = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetPathRoot($full)
    if ($full -eq $root) { return $full }

    # .NET GetFullPath collapses '..' only for the platform separator. Split
    # both separators and pop '..' here so a Windows-style relative path cannot
    # stay lexically inside the repo on Linux.
    $rel = $full.Substring($root.Length).TrimEnd([char[]]@('\', '/'))
    $parts = @(Split-GimPathSegments -Relative $rel)
    $current = $root
    $sep = [string][System.IO.Path]::DirectorySeparatorChar

    for ($i = 0; $i -lt $parts.Count; $i++) {
        $part = $parts[$i]
        if ($part -eq '.') { continue }
        if ($part -eq '..') {
            $trimmed = $current.TrimEnd([char[]]@('\', '/'))
            $rootTrim = $root.TrimEnd([char[]]@('\', '/'))
            if ($trimmed -eq $rootTrim -or $trimmed -eq '') { continue }
            $parent = [System.IO.Path]::GetDirectoryName($trimmed)
            if ([string]::IsNullOrEmpty($parent)) { $current = $root } else { $current = $parent }
            continue
        }
        $candidate = [System.IO.Path]::Combine($current, $part)
        if (Test-Path -LiteralPath $candidate) {
            $item = Get-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
            if ($item -and ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -and $item.Target) {
                # $item.Target is the .NET Core FileSystemInfo.Target API; it
                # does NOT exist on .NET Framework, so reparse-point (symlink /
                # junction) escape detection is a pwsh 7+ capability. On PS 5.1
                # the branch silently no-ops and containment degrades to
                # lexical resolution - that is why the runtime standard is
                # pwsh 7 (AGENTS ADR 0003; setup guide 0001). PS 5.1 is best-effort
                # for data-only verb code only.
                $target = $item.Target
                if ($target -is [System.Array]) { $target = [string]$target[0] }
                if (-not [System.IO.Path]::IsPathRooted($target)) {
                    $target = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetDirectoryName($candidate)) $target))
                } else {
                    $target = [System.IO.Path]::GetFullPath($target)
                }
                if ($i -lt $parts.Count - 1) {
                    $remaining = $parts[($i + 1)..($parts.Count - 1)]
                    return Get-GimRealPath -Path ([System.IO.Path]::Combine($target, ($remaining -join $sep))) -Depth ($Depth + 1)
                }
                return Get-GimRealPath -Path $target -Depth ($Depth + 1)
            }
        }
        $current = $candidate
    }
    return $current
}

function Get-GimRoots {
    <#
    .SYNOPSIS
        Load roots.json, expand %VAR% entries, resolve relative entries
        against the repo root, and cache the result per module session.
    .DESCRIPTION
        Returns @{ read = <string[]>; readExtra = <string[]> }. 'read' is the
        full roots list for read verbs; 'readExtra' is only for gim-asm
        (nuget cache) and never participates in ordinary containment.
    #>
    param(
        [string]$RootsFile = (Join-Path $script:GimModuleRoot 'roots.json')
    )
    if ($script:GimRootsCache -and $script:GimRootsCache.ContainsKey($RootsFile)) {
        return $script:GimRootsCache[$RootsFile]
    }

    if (-not (Test-Path -LiteralPath $RootsFile)) { Write-GimError "roots file not found: $RootsFile" }
    $repoRoot = Get-GimRepoRoot
    $json = Get-Content -LiteralPath $RootsFile -Raw -Encoding UTF8 | ConvertFrom-Json

    function Resolve-RootEntry {
        param([string]$Entry)
        $expanded = [Environment]::ExpandEnvironmentVariables($Entry)
        if ([System.IO.Path]::IsPathRooted($expanded)) {
            return Get-GimRealPath -Path $expanded
        }
        return Get-GimRealPath -Path ([System.IO.Path]::GetFullPath((Join-Path $repoRoot $expanded)))
    }

    $read = @()
    foreach ($entry in @($json.roots)) { $read += (Resolve-RootEntry -Entry $entry) }
    $readExtra = @()
    foreach ($entry in @($json.readExtraRoots)) { $readExtra += (Resolve-RootEntry -Entry $entry) }

    if (-not $script:GimRootsCache) { $script:GimRootsCache = @{} }
    $script:GimRootsCache[$RootsFile] = @{ read = $read; readExtra = $readExtra }
    return $script:GimRootsCache[$RootsFile]
}

function Test-GimContainedPath {
    <#
    .SYNOPSIS
        True when a real path equals a root or sits under one of them
        (case-insensitive, separator-aware).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RealPath,
        [Parameter(Mandatory = $true)]
        [string[]]$RootRealPaths
    )
    foreach ($root in $RootRealPaths) {
        if ([string]::Equals($RealPath, $root, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        $rootTrim = $root.TrimEnd([char[]]@('\', '/'))
        # A filesystem root trims to empty. Do not treat that as a prefix of
        # every path; only an equal root matches above.
        if ([string]::IsNullOrEmpty($rootTrim)) { continue }
        $realTrim = $RealPath.TrimEnd([char[]]@('\', '/'))
        if ([string]::Equals($realTrim, $rootTrim, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        if ($RealPath.StartsWith($rootTrim + '\', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        if ($RealPath.StartsWith($rootTrim + '/', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Resolve-ContainedPath {
    <#
    .SYNOPSIS
        Resolve a path to its real full path and assert it is contained in the
        configured read roots. Errors unless -AllowMissing forgives a missing
        path (containment is never forgiven).
    #>
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Path,
        [switch]$AllowMissing,
        [string]$RootsFile = (Join-Path $script:GimModuleRoot 'roots.json')
    )
    $roots = Get-GimRoots -RootsFile $RootsFile
    $real = Get-GimRealPath -Path $Path

    if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths $roots.read)) {
        Write-GimError "path not contained in configured roots: '$Path' (resolved '$real')"
    }
    if (-not (Test-Path -LiteralPath $real)) {
        if (-not $AllowMissing) {
            Write-GimError "path does not exist: '$Path' (use --allow-missing to skip missing inputs)"
        }
    }
    return $real
}

function Get-GimWriteRoots {
    <#
    .SYNOPSIS
        Write/exec roots: the current repo root only (A6+). `.agent-scratch/`
        lives inside the repo, so "current repo + .agent-scratch/" is one root
        - narrower than the read roots (sibling repos are readable, never
        writable). gim-apply alone widens this set with the OS temp root; see
        Get-GimApplyWriteRoots.
    #>
    return @(Get-GimRepoRoot)
}

function Resolve-GimWritePath {
    <#
    .SYNOPSIS
        Resolve a write target to its real full path and assert containment in
        the current repo (write roots).
    .DESCRIPTION
        Write verbs (gim-fix/gim-write) may only touch the current repo plus
        its `.agent-scratch/`; gim-apply uses Resolve-GimApplyPath, which also
        admits the OS temp root. Sibling repos, `$env:TEMP`, UNC paths,
        `..\..` escapes, and junction/symlink escapes are rejected
        (real-path resolution collapses `..` and follows reparse points, so the
        containment check sees the final target). Unlike Resolve-ContainedPath
        the target does not need to exist - writing creates it. Paths carrying
        unexpanded %VAR% tokens are rejected up front (same rule as the
        variable store root). -RootLabel only names the allowed root set in the
        rejection message; gim-apply passes its wider label while reusing this
        single containment implementation.
    #>
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Path,
        [string[]]$Roots,
        [string]$RootLabel = 'current repo'
    )
    if ($Path -match '%[^%]+%') {
        Write-GimError "invalid write path '$Path': environment variables are not expanded (give the full path)"
    }
    if (-not $Roots) { $Roots = Get-GimWriteRoots }
    $real = Get-GimRealPath -Path $Path
    if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths $Roots)) {
        Write-GimError "write path not contained in $($RootLabel): '$Path' (resolved '$real')"
    }
    return $real
}

function Resolve-GimSafeTempRoot {
    <#
    .SYNOPSIS
        Resolve the OS temp root and enforce the gim-apply safety floor.
    .DESCRIPTION
        gim-apply is the only write verb admitted outside the repo, so the temp
        root it widens to must itself be safe. Rejected: unexpanded %VAR%
        tokens, non-rooted (relative) paths, UNC paths, drive roots, paths
        shallower than two segments below the drive root, and any path equal to
        or an ancestor of the user profile, local app data, app data, Windows
        directory, system root, or the repo root (a temp root above those would
        turn containment into an escape hatch). The path is real-path resolved
        before the checks, so `..` and reparse points are judged on the final
        target. Returns the resolved real path.
    #>
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Path
    )
    if ($Path -match '%[^%]+%') {
        Write-GimError "invalid temp root '$Path': environment variables are not expanded (give the full path)"
    }
    if ($Path -match '^[\\/]{2}') {
        Write-GimError "invalid temp root '$Path': UNC paths are not allowed"
    }
    if (-not [System.IO.Path]::IsPathRooted($Path)) {
        Write-GimError "invalid temp root '$Path': must be an absolute rooted local path"
    }

    $real = Get-GimRealPath -Path $Path
    $root = [System.IO.Path]::GetPathRoot($real)
    if ($real -eq $root) {
        Write-GimError "invalid temp root '$Path': a drive root is not allowed"
    }
    $rel = $real.Substring($root.Length).TrimEnd([char[]]@('\', '/'))
    $segments = @(Split-GimPathSegments -Relative $rel)
    # Windows temp lives several segments under the drive. The Unix temp
    # directory is /tmp (one segment) and is still a safe leaf.
    $minSegments = 2
    if ([System.IO.Path]::DirectorySeparatorChar -eq [char]'/') { $minSegments = 1 }
    if ($segments.Count -lt $minSegments) {
        Write-GimError "invalid temp root '$Path': must sit at least $minSegments path segment(s) below the drive root"
    }

    $protected = @(
        [Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile)
        [Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)
        [Environment]::GetFolderPath([System.Environment+SpecialFolder]::ApplicationData)
        $env:WINDIR
        $env:SystemRoot
        (Get-GimRepoRoot)
    )
    foreach ($candidate in $protected) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        $protectedReal = Get-GimRealPath -Path $candidate
        if (Test-GimContainedPath -RealPath $protectedReal -RootRealPaths @($real)) {
            Write-GimError "invalid temp root '$Path': protected directory '$protectedReal' is at or below it"
        }
    }
    return $real
}

function Get-GimApplyWriteRoots {
    <#
    .SYNOPSIS
        Write roots for gim-apply only: the current repo root plus the real OS
        temp root, after the temp-root safety floor.
    .DESCRIPTION
        gim-apply may create, touch, and remove scratch files under the OS temp
        root as well as inside the current repo. Every other write verb
        (gim-write, gim-fix) and the execution verbs stay repo-only through
        Get-GimWriteRoots. The temp root is resolved once per module session
        with [System.IO.Path]::GetTempPath() and Resolve-GimSafeTempRoot, so a
        symlinked or junctioned %TEMP% is compared as its real target and a
        temp root that fails the safety floor (drive root, UNC, shallow path,
        profile/system/repo ancestor) is rejected rather than widening
        containment. The candidate path is real-path resolved too, so `..`
        escapes and reparse points are judged on the final target.
    #>
    if ($script:GimApplyWriteRootsCache) { return $script:GimApplyWriteRootsCache }
    $roots = @(Get-GimRepoRoot)
    $roots += Resolve-GimSafeTempRoot -Path ([System.IO.Path]::GetTempPath())
    $script:GimApplyWriteRootsCache = $roots
    return $roots
}

function Resolve-GimApplyPath {
    <#
    .SYNOPSIS
        Resolve a gim-apply target to its real full path and assert containment
        in the current repo or the validated OS temp root.
    .DESCRIPTION
        Thin wrapper around Resolve-GimWritePath (the single containment home)
        with the wider gim-apply root set from Get-GimApplyWriteRoots. The
        target need not exist; unexpanded %VAR% tokens are rejected up front;
        real-path resolution collapses `..` and follows reparse points, so
        containment is judged on the final target. A target outside both roots
        - another drive, a sibling repo, a UNC share, a Windows system
        directory - is rejected. gim-write, gim-fix, gim-run, and gim-probe
        never call this helper and keep their repo-only containment.
    #>
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Path,
        [string[]]$Roots
    )
    if (-not $Roots) { $Roots = Get-GimApplyWriteRoots }
    return Resolve-GimWritePath -Path $Path -Roots $Roots -RootLabel 'current repo or OS temp root'
}

function Get-GimPathFromRecord {
    <#
    .SYNOPSIS
        Extract the path prefix from a 'path: ...' record line - the text form
        gim-inspect emits ('path: kind=file hash=...'). A plain path passes
        through unchanged. Windows paths never contain ': ' in a segment (a
        drive colon is followed by a backslash), so the first ': ' split is
        unambiguous. Shared by gim-fix / gim-apply / gim-diff consumers.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object]$Item
    )
    if (-not ($Item -is [string])) {
        Write-GimError "invalid pipeline item of type '$($Item.GetType().Name)': expected a path string or 'path: ...' record line"
    }
    $t = ([string]$Item).Trim()
    if ($t -eq '') {
        Write-GimError "invalid pipeline item: empty string (expected a path or 'path: ...' record line)"
    }
    $idx = $t.IndexOf(': ')
    if ($idx -gt 0) {
        $candidate = $t.Substring(0, $idx).Trim()
        if ($candidate -eq '') {
            Write-GimError "invalid pipeline item: no path before ': ' ('$([string]$Item)')"
        }
        return $candidate
    }
    return $t
}

function Convert-GimWritePathItem {
    <#
    .SYNOPSIS
        Convert a pipeline item to a write-contained real path.
    .DESCRIPTION
        gim-fix/gim-apply accept either plain path strings or the text record
        lines emitted by gim-inspect and passed through gim-where
        ('path: kind=file trailingWs=True ...'): the path prefix is extracted
        by Get-GimPathFromRecord and then write-contained. Non-string items
        and empty strings are rejected with a shape error naming the expected
        input instead of being silently coerced.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object]$Item
    )
    return Resolve-GimWritePath -Path (Get-GimPathFromRecord -Item $Item)
}

function Convert-GimApplyPathItem {
    <#
    .SYNOPSIS
        Convert a pipeline item to a gim-apply-contained real path.
    .DESCRIPTION
        Same input contract as Convert-GimWritePathItem (plain path strings or
        gim-inspect 'path: ...' record lines passed through gim-where), but
        contained against the wider gim-apply roots (current repo plus the
        validated OS temp root) via Resolve-GimApplyPath. Non-string items and
        empty strings are rejected by Get-GimPathFromRecord with a shape error
        naming the expected input.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object]$Item
    )
    return Resolve-GimApplyPath -Path (Get-GimPathFromRecord -Item $Item)
}
