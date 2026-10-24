# GimFleetExec.ps1 - fleet process runner for git / gh / acli (ADR 0008), plus
# the gim-pr helpers (file-list validation, derived scratch paths, index guard,
# and stack-view parsing).
# argv array, explicit working directory, hard timeout with tree kill, captured
# stdout/stderr, exit code. No shell, no Start-Process, no &$var; argv is
# assembled with Quote-Arg and launched through [System.Diagnostics.Process]
# only. Unlike GimExec this runner does NOT scrub GH_TOKEN / ACLI credentials:
# the fleet commands need the user's own auth. It never prints secrets; callers
# redact any forwarded error text through Redact-GimFleetText.

$script:GimFleetDefaultTimeout = 120
$script:GimFleetMaxTimeout = 900
$script:GimFleetMaxOutputBytes = 262144

function Test-GimFleetGitRoot {
    <#
    .SYNOPSIS
        True when a directory is a git root: it holds a .git directory (main
        checkout) or a .git file (linked worktree / submodule).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
    $marker = Join-Path $Path '.git'
    if (Test-Path -LiteralPath $marker -PathType Leaf) { return $true }
    if (Test-Path -LiteralPath $marker -PathType Container) { return $true }
    return $false
}

function Get-GimFleetSiblingDirectoryNames {
    <#
    .SYNOPSIS
        Sorted directory names directly under a parent root. Sorted
        case-insensitively so missing-name and ambiguity errors are stable.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentRoot
    )
    $names = New-Object System.Collections.Generic.List[string]
    if (Test-Path -LiteralPath $ParentRoot -PathType Container) {
        foreach ($dir in [System.IO.Directory]::GetDirectories($ParentRoot)) {
            [void]$names.Add([System.IO.Path]::GetFileName($dir))
        }
    }
    $sorted = $names.ToArray()
    [System.Array]::Sort($sorted, [System.StringComparer]::OrdinalIgnoreCase)
    return @($sorted)
}

function Resolve-GimFleetRepo {
    <#
    .SYNOPSIS
        Resolve a gim-preflight / gim-pr --repo value to an absolute path.
    .DESCRIPTION
        Accepts three shapes:
          - absolute path: resolved, and it must be a git root;
          - relative path with separators (or '.' / '..'): resolved against
            the base root, and it must be a git root;
          - simple repo name: matched case-insensitively against the directory
            names under the parent of the base root, and must be a git root
            (.git directory or file). A missing name lists the available
            directory names; a case-only ambiguity lists the matches.
        The base root defaults to Get-GimRepoRoot; tests pass -BaseRoot. The
        caller's current directory is never changed: the result is the working
        directory for child git/gh calls. Returns the real path.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Requested,
        [AllowEmptyString()]
        [string]$BaseRoot = ''
    )
    $name = $Requested.Trim()
    if ($name -eq '') { Write-GimError '--repo must not be empty' }
    if ($BaseRoot -eq '') { $BaseRoot = Get-GimRepoRoot }
    $base = [System.IO.Path]::GetFullPath($BaseRoot)

    if ([System.IO.Path]::IsPathRooted($name)) {
        $real = Get-GimRealPath -Path $name
        if (-not (Test-GimFleetGitRoot -Path $real)) {
            Write-GimError ("--repo path '{0}' is not a git root (expected .git file or directory)" -f $name)
        }
        return $real
    }
    if ($name -match '[\\/]' -or $name -eq '.' -or $name -eq '..') {
        $real = Get-GimRealPath -Path (Join-Path $base $name)
        if (-not (Test-GimFleetGitRoot -Path $real)) {
            Write-GimError ("--repo path '{0}' is not a git root (expected .git file or directory)" -f $name)
        }
        return $real
    }

    $parent = Split-Path -Parent $base
    if (-not $parent) { $parent = $base }
    $nameMatches = New-Object System.Collections.Generic.List[string]
    foreach ($candidate in (Get-GimFleetSiblingDirectoryNames -ParentRoot $parent)) {
        if ([string]::Equals($candidate, $name, [System.StringComparison]::OrdinalIgnoreCase)) {
            [void]$nameMatches.Add($candidate)
        }
    }
    if ($nameMatches.Count -eq 0) {
        $available = @(Get-GimFleetSiblingDirectoryNames -ParentRoot $parent)
        $list = 'none'
        if ($available.Count -gt 0) { $list = ($available -join ', ') }
        Write-GimError ("unknown repo name '{0}': available directories under '{1}': {2}" -f $name, $parent, $list)
    }

    $roots = New-Object System.Collections.Generic.List[string]
    foreach ($candidate in $nameMatches) {
        $path = Join-Path $parent $candidate
        if (Test-GimFleetGitRoot -Path $path) { [void]$roots.Add($path) }
    }
    if ($roots.Count -eq 0) {
        $available = @(Get-GimFleetSiblingDirectoryNames -ParentRoot $parent)
        $list = 'none'
        if ($available.Count -gt 0) { $list = ($available -join ', ') }
        Write-GimError ("unknown repo name '{0}': no git root under '{1}'; available directories: {2}" -f $name, $parent, $list)
    }
    if ($roots.Count -gt 1) {
        $leafNames = New-Object System.Collections.Generic.List[string]
        foreach ($root in $roots) { [void]$leafNames.Add([System.IO.Path]::GetFileName($root)) }
        $sorted = $leafNames.ToArray()
        [System.Array]::Sort($sorted, [System.StringComparer]::OrdinalIgnoreCase)
        Write-GimError ("ambiguous repo name '{0}' under '{1}': matches {2}" -f $name, $parent, ($sorted -join ', '))
    }
    return (Get-GimRealPath -Path $roots[0])
}

$script:GimPrFirstBranchPrefixes = @(
    'feature', 'docs', 'chore', 'hotfix', 'release', 'fix', 'bugfix',
    'refactor', 'test', 'ci', 'build', 'perf'
)

function Resolve-GimPrRepoPath {
    <#
    .SYNOPSIS
        Resolve a gim-pr input path (file list, PR body) as a repo-relative or
        repo-contained path. Escapes, UNC paths, unexpanded %VAR% tokens, and
        paths outside the repository are rejected.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [AllowEmptyString()]
        [string]$RootLabel = 'repository',
        [switch]$AllowMissing
    )
    $trim = $Path.Trim()
    if ($trim -eq '') { Write-GimError ("{0} path must not be empty; required input: a repo-relative path" -f $Label) }
    if ($trim -match '%[^%]+%') {
        Write-GimError ("{0} path '{1}' contains an unexpanded environment variable; required input: the full path" -f $Label, $trim)
    }
    if ($trim -match '^[\\/]{2}') {
        Write-GimError ("{0} path '{1}' is a UNC path; required input: a path inside the repository" -f $Label, $trim)
    }
    if ([System.IO.Path]::IsPathRooted($trim)) {
        $full = [System.IO.Path]::GetFullPath($trim)
    } else {
        $full = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $trim))
    }
    $real = Get-GimRealPath -Path $full
    $rootReal = Get-GimRealPath -Path $RepoRoot
    if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths @($rootReal))) {
        Write-GimError ("{0} path '{1}' resolves outside the {2} '{3}'; required input: a path inside the {2}" -f $Label, $trim, $RootLabel, $rootReal)
    }
    if (-not $AllowMissing) {
        if (-not (Test-Path -LiteralPath $real -PathType Leaf)) {
            Write-GimError ("{0} file not found: '{1}'; required input: an existing file inside the {2}" -f $Label, $trim, $RootLabel)
        }
    }
    return $real
}

function ConvertTo-GimPrRepoRelativeFile {
    <#
    .SYNOPSIS
        Validate one file-list entry: repo-relative, no globs, no directories,
        no escapes. Returns the normalized repo-relative path ('/'-separated).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$Entry
    )
    $trim = $Entry.Trim()
    if ($trim -eq '') {
        Write-GimError 'invalid file list entry: blank line; required input: one repo-relative file path per line'
    }
    if ($trim -match '[*?\[\]]') {
        Write-GimError ("invalid file list entry '{0}': globs are not allowed; required input: one explicit repo-relative file path per line" -f $trim)
    }
    if ($trim -match '^[\\/]{2}') {
        Write-GimError ("invalid file list entry '{0}': UNC paths are not allowed; required input: a repo-relative path" -f $trim)
    }
    if ([System.IO.Path]::IsPathRooted($trim)) {
        Write-GimError ("invalid file list entry '{0}': absolute paths are not allowed; required input: a repo-relative path" -f $trim)
    }
    $normalized = $trim -replace '\\', '/'
    if ($normalized -match '(^|/)\.\.(/|$)') {
        Write-GimError ("invalid file list entry '{0}': the path escapes the repository; required input: a repo-relative path" -f $trim)
    }
    $full = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot ($normalized -replace '/', '\')))
    $real = Get-GimRealPath -Path $full
    $rootReal = Get-GimRealPath -Path $RepoRoot
    if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths @($rootReal))) {
        Write-GimError ("invalid file list entry '{0}': the path escapes the repository; required input: a repo-relative path" -f $trim)
    }
    if (Test-Path -LiteralPath $real -PathType Container) {
        Write-GimError ("invalid file list entry '{0}': directories are not allowed; required input: a file path" -f $trim)
    }
    # A missing path is allowed on purpose: it goes to `git add`, whose exact
    # error is reported, and it is the only way to commit a tracked deletion.
    $rootPrefix = $rootReal.TrimEnd('\') + '\'
    return ($real.Substring($rootPrefix.Length) -replace '\\', '/')
}

function ConvertTo-GimPrFileList {
    <#
    .SYNOPSIS
        Validate file-list entries, trim, drop blanks, and dedupe
        case-insensitively. Returns normalized repo-relative paths.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [AllowEmptyCollection()]
        [string[]]$Entries = @()
    )
    $seen = @{}
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($entry in @($Entries)) {
        if ($null -eq $entry) { continue }
        $trim = ([string]$entry).Trim()
        if ($trim -eq '') { continue }
        $rel = ConvertTo-GimPrRepoRelativeFile -RepoRoot $RepoRoot -Entry $trim
        $key = $rel.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        [void]$list.Add($rel)
    }
    return @($list.ToArray())
}

function Read-GimPrFileList {
    <#
    .SYNOPSIS
        Read a UTF-8 file list (one repo-relative path per line), trim lines,
        ignore blanks, reject globs/directories/escapes, and dedupe.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )
    $full = Resolve-GimPrRepoPath -RepoRoot $RepoRoot -Path $Path -Label 'file list'
    $lines = @([System.IO.File]::ReadAllLines($full, [System.Text.Encoding]::UTF8))
    $files = @(ConvertTo-GimPrFileList -RepoRoot $RepoRoot -Entries $lines)
    if ($files.Count -eq 0) {
        Write-GimError ("file list is empty: '{0}'; required input: one repo-relative file path per line" -f $Path)
    }
    return $files
}

function Get-GimPrFilesRelativeDirectory {
    <#
    .SYNOPSIS
        Derive the scratch file-list directory from a branch name.
    .DESCRIPTION
        Known first-PR prefixes map <prefix>/<slug> to <slug>/<prefix>; any
        other <feature>/<step> branch maps to <feature>/<step>. Exactly one
        slash is required; anything else errors and asks for an explicit
        --files-file.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Branch
    )
    $b = $Branch.Trim()
    if ($b -eq '') {
        Write-GimError 'cannot derive --files-file: the current branch name is empty; required input: --files-file <path>'
    }
    if ($b.StartsWith('refs/heads/', [System.StringComparison]::OrdinalIgnoreCase)) {
        $b = $b.Substring('refs/heads/'.Length)
    }
    if ($b -eq 'HEAD') {
        Write-GimError 'cannot derive --files-file: detached HEAD; required input: --files-file <path>'
    }
    $slashes = ([regex]::Matches($b, '/')).Count
    if ($slashes -ne 1) {
        Write-GimError ("cannot derive --files-file from branch '{0}': expected exactly one '/', found {1}; required input: --files-file <path>" -f $Branch, $slashes)
    }
    $parts = $b.Split('/')
    $first = $parts[0]
    $second = $parts[1]
    if ($script:GimPrFirstBranchPrefixes -contains $first) {
        return ($second + '/' + $first)
    }
    return ($first + '/' + $second)
}

function Get-GimPrResultDirectory {
    <#
    .SYNOPSIS
        Evidence directory for result.json, derived without throwing.
    .DESCRIPTION
        Uses the same <feature>/<step> mapping as the derived files.txt when
        the branch has exactly one '/'; any other branch shape (including a
        detached-HEAD name if a caller reaches this helper) falls back to
        .agent-scratch/pr-files/<sanitized-branch>/.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Branch
    )
    $rel = $null
    try {
        $rel = Get-GimPrFilesRelativeDirectory -Branch $Branch
    } catch {
        $rel = $null
    }
    if (-not $rel) {
        $sanitized = ([string]$Branch).Trim() -replace '[^A-Za-z0-9._-]', '-'
        $sanitized = $sanitized.Trim('-', '.')
        if ($sanitized -eq '') { $sanitized = 'unknown' }
        $rel = $sanitized
    }
    $dir = $rel -replace '/', '\'
    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot ('.agent-scratch\pr-files\' + $dir)))
}

function Get-GimPrDerivedFilesFile {
    <#
    .SYNOPSIS
        Absolute derived files.txt path:
        <repo>/.agent-scratch/pr-files/<feature>/<step>/files.txt
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Branch
    )
    $rel = Get-GimPrFilesRelativeDirectory -Branch $Branch
    $dir = $rel -replace '/', '\'
    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot ('.agent-scratch\pr-files\' + $dir + '\files.txt')))
}

function Get-GimPrIndexFiles {
    <#
    .SYNOPSIS
        List staged (index) paths. Returns { ok, files, error }.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )
    $r = Invoke-GimFleetExec -Argv @('git', 'diff', '--cached', '--name-only', '-z') -WorkingDirectory $RepoRoot -TimeoutSeconds 60
    if ($r.timed_out) {
        return [pscustomobject]@{ ok = $false; files = @(); error = 'git diff --cached timed out' }
    }
    if ($r.exit_code -ne 0) {
        $err = Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$r.stderr))
        if (-not $err) { $err = ('git diff --cached exited with code {0}' -f $r.exit_code) }
        return [pscustomobject]@{ ok = $false; files = @(); error = $err }
    }
    $files = New-Object System.Collections.Generic.List[string]
    foreach ($item in (([string]$r.stdout) -split "`0")) {
        $p = $item.Trim()
        if ($p -eq '') { continue }
        [void]$files.Add(($p -replace '\\', '/'))
    }
    return [pscustomobject]@{ ok = $true; files = @($files.ToArray()); error = $null }
}

function Test-GimPrStagedSubset {
    <#
    .SYNOPSIS
        True when every staged path is in the declared file list. Returns
        { ok, extra } where extra names staged paths outside the list.
    #>
    param(
        [AllowEmptyCollection()]
        [string[]]$Staged = @(),
        [AllowEmptyCollection()]
        [string[]]$Listed = @()
    )
    $allowed = @{}
    foreach ($p in @($Listed)) {
        if ($null -ne $p) { $allowed[([string]$p).ToLowerInvariant()] = $true }
    }
    $extra = New-Object System.Collections.Generic.List[string]
    foreach ($p in @($Staged)) {
        if ($null -eq $p) { continue }
        if (-not $allowed.ContainsKey(([string]$p).ToLowerInvariant())) {
            [void]$extra.Add([string]$p)
        }
    }
    return [pscustomobject]@{ ok = ($extra.Count -eq 0); extra = @($extra.ToArray()) }
}

function Get-GimPrUrlFromText {
    <#
    .SYNOPSIS
        Extract the last pull-request URL from gh output.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text) { return $null }
    $url = $null
    foreach ($m in [regex]::Matches($Text, 'https?://\S+/pull/\d+')) { $url = $m.Value.TrimEnd('.', ',', ')') }
    return $url
}

function Get-GimPrStackSummary {
    <#
    .SYNOPSIS
        Parse `gh stack view --json` into { url, branch_count }.
    .DESCRIPTION
        Read the documented shape directly: top-level `url` and `branches`, or
        the same fields inside the top-level `stack` object. No recursive field
        walk, so a nested pull-request URL can never be mistaken for the stack
        URL. `branch_count` is $null when the branches array is absent; the
        caller then downgrades the sync K check to number-only.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text) { return $null }
    $trim = $Text.Trim()
    if ($trim -eq '') { return $null }
    if ($trim -match '# gim: output truncated') { return $null }
    try {
        $doc = $trim | ConvertFrom-Json
    } catch {
        return $null
    }
    if ($null -eq $doc) { return $null }
    $url = $null
    $urlProp = $doc.PSObject.Properties['url']
    if ($null -ne $urlProp -and $urlProp.Value) { $url = [string]$urlProp.Value }
    $branches = $null
    $branchesProp = $doc.PSObject.Properties['branches']
    if ($null -ne $branchesProp) { $branches = $branchesProp.Value }
    $stackProp = $doc.PSObject.Properties['stack']
    if ($null -ne $stackProp -and $null -ne $stackProp.Value) {
        if ($null -eq $url) {
            $nestedUrl = $stackProp.Value.PSObject.Properties['url']
            if ($null -ne $nestedUrl -and $nestedUrl.Value) { $url = [string]$nestedUrl.Value }
        }
        if ($null -eq $branches) {
            $nestedBranches = $stackProp.Value.PSObject.Properties['branches']
            if ($null -ne $nestedBranches) { $branches = $nestedBranches.Value }
        }
    }
    $branchCount = $null
    if ($null -ne $branches) { $branchCount = @($branches).Count }
    return [pscustomobject]@{ url = $url; branch_count = $branchCount }
}

function Limit-GimFleetOutput {
    <#
    .SYNOPSIS
        Bound one captured stream to MaxOutputBytes (UTF-8 approximated as
        characters) so a noisy child cannot exhaust the parent context.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text,
        [int]$MaxBytes = $script:GimFleetMaxOutputBytes
    )
    if ($null -eq $Text) { return '' }
    if ($Text.Length -le $MaxBytes) { return $Text }
    return $Text.Substring(0, $MaxBytes) + "`n# gim: output truncated (bytes $MaxBytes)"
}

function Get-GimFleetStartInfo {
    <#
    .SYNOPSIS
        Build a ProcessStartInfo from an argv array: argv[0] is the FileName,
        the rest are Quote-Arg-joined (CommandLineToArgvW rules, no shell),
        WorkingDirectory is explicit, and both pipes are redirected.
    #>
    param(
        # Empty tokens are legal argv values (e.g. `gh pr create --body ''`).
        # Without AllowEmptyString the mandatory binder rejects the whole
        # array when any element is an empty string.
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$Argv,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )
    if ($Argv.Count -eq 0) { Write-GimError 'internal error: empty fleet argv' }
    $quoted = New-Object System.Collections.Generic.List[string]
    for ($i = 1; $i -lt $Argv.Count; $i++) { $quoted.Add((Quote-Arg -Argument $Argv[$i])) }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Argv[0]
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    # Decode child output as UTF-8. Without this, .NET uses the Windows
    # console code page and gh-stack glyphs (checkmarks, em dashes) arrive as
    # mojibake before result.json is written.
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $psi.StandardOutputEncoding = $utf8
    $psi.StandardErrorEncoding = $utf8
    $psi.CreateNoWindow = $true
    $psi.Arguments = [string]::Join(' ', $quoted.ToArray())
    return $psi
}

function Start-GimFleetProcess {
    <#
    .SYNOPSIS
        Start an argv process and begin draining both pipes. Returns a state
        record (started or start error) for Complete-GimFleetProcess.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$Argv,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [int]$TimeoutSeconds = $script:GimFleetDefaultTimeout,
        [int]$MaxOutputBytes = $script:GimFleetMaxOutputBytes
    )
    $state = [pscustomobject]@{
        Process = $null
        OutTask = $null
        ErrTask = $null
        TimeoutSeconds = $TimeoutSeconds
        MaxOutputBytes = $MaxOutputBytes
        StartError = $null
    }
    $psi = Get-GimFleetStartInfo -Argv $Argv -WorkingDirectory $WorkingDirectory
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    try {
        [void]$proc.Start()
    } catch {
        $state.StartError = ("failed to start '{0}': {1}" -f $Argv[0], $_.Exception.Message)
        try { $proc.Dispose() } catch { }
        return $state
    }
    $state.Process = $proc
    try {
        $state.OutTask = $proc.StandardOutput.ReadToEndAsync()
        $state.ErrTask = $proc.StandardError.ReadToEndAsync()
    } catch {
        $state.StartError = ("failed to read process pipes: {0}" -f $_.Exception.Message)
    }
    return $state
}

function Complete-GimFleetProcess {
    <#
    .SYNOPSIS
        Wait for a started process (hard timeout, tree kill on expiry), drain
        both captured streams, and return
        @{ exit_code; timed_out; stdout; stderr }.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $State
    )
    if ($State.StartError) {
        return [pscustomobject]@{
            exit_code = 127
            timed_out = $false
            stdout    = ''
            stderr    = (Limit-GimFleetOutput -Text $State.StartError -MaxBytes $State.MaxOutputBytes)
        }
    }
    $proc = $State.Process
    $timedOut = $false
    $exitCode = 1
    try {
        $deadline = [datetime]::UtcNow.AddSeconds($State.TimeoutSeconds)
        while (-not $proc.HasExited -and [datetime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 25
        }
        $timedOut = -not $proc.HasExited
        if ($timedOut) {
            Stop-GimProcessTree -Process $proc
            $proc.WaitForExit(10000) | Out-Null
        } else {
            $proc.WaitForExit() | Out-Null
        }
        try { $exitCode = $proc.ExitCode } catch { $exitCode = 1 }
        if ($timedOut) { $exitCode = 124 }
        $stdout = ''
        $stderr = ''
        if ($null -ne $State.OutTask) {
            try { if ($State.OutTask.Wait(3000)) { $stdout = $State.OutTask.Result } } catch { $stdout = '' }
        }
        if ($null -ne $State.ErrTask) {
            try { if ($State.ErrTask.Wait(3000)) { $stderr = $State.ErrTask.Result } } catch { $stderr = '' }
        }
        return [pscustomobject]@{
            exit_code = $exitCode
            timed_out = $timedOut
            stdout    = (Limit-GimFleetOutput -Text $stdout -MaxBytes $State.MaxOutputBytes)
            stderr    = (Limit-GimFleetOutput -Text $stderr -MaxBytes $State.MaxOutputBytes)
        }
    } finally {
        try { $proc.Dispose() } catch { }
    }
}

function Invoke-GimFleetExec {
    <#
    .SYNOPSIS
        Run one argv process to completion (or hard timeout) and return
        @{ exit_code; timed_out; stdout; stderr }.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$Argv,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [int]$TimeoutSeconds = $script:GimFleetDefaultTimeout,
        [int]$MaxOutputBytes = $script:GimFleetMaxOutputBytes
    )
    if ($TimeoutSeconds -lt 1) { $TimeoutSeconds = 1 }
    if ($TimeoutSeconds -gt $script:GimFleetMaxTimeout) { $TimeoutSeconds = $script:GimFleetMaxTimeout }
    $state = Start-GimFleetProcess -Argv $Argv -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds -MaxOutputBytes $MaxOutputBytes
    return (Complete-GimFleetProcess -State $state)
}

function Invoke-GimFleetBatch {
    <#
    .SYNOPSIS
        Run several argv processes in parallel: every process is started before
        the first one is awaited, then results are returned in input order.
    .DESCRIPTION
        Each command is @{ argv = <string[]>; working_directory = <string>;
        timeout_seconds = <int> }. Parallelism is bounded by the caller's
        command count; this function does not spawn a shell or a job.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Commands
    )
    $states = New-Object System.Collections.Generic.List[object]
    foreach ($cmd in $Commands) {
        $argv = @($cmd.argv)
        $wd = [string]$cmd.working_directory
        $timeout = $script:GimFleetDefaultTimeout
        if ($cmd.ContainsKey('timeout_seconds')) { $timeout = [int]$cmd.timeout_seconds }
        $states.Add((Start-GimFleetProcess -Argv $argv -WorkingDirectory $wd -TimeoutSeconds $timeout))
    }
    $results = New-Object System.Collections.Generic.List[object]
    foreach ($state in $states) {
        $results.Add((Complete-GimFleetProcess -State $state))
    }
    return @($results.ToArray())
}
