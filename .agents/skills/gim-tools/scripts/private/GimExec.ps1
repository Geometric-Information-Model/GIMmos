# GimExec.ps1 - shared process launch for the execution tier (A7): argv
# assembly (Quote-Arg), environment scrubbing of credential variables, hard
# timeout with taskkill tree kill, stream pumping with output caps, exec-root
# containment, and an append-only run audit log.
#
# Design invariants: no shell (UseShellExecute=false), no Start-Process, no
# &$var; the child WorkingDirectory is always set explicitly (never the
# harness cwd); kill is taskkill /PID <pid> /T /F - never Stop-Process alone;
# the audit path is overridable via GIM_RUN_AUDIT so tests stay hermetic.

$script:GimRunDefaultTimeout = 300
$script:GimRunMaxTimeout = 1800
$script:GimExecDrainGraceSeconds = 3

function Test-GimScrubbedVarName {
    <#
    .SYNOPSIS
        True when an environment variable name matches the credential scrub
        list: exact NUGET_API_KEY / GH_TOKEN / GITHUB_TOKEN, or any AWS_* /
        AZURE_* prefix (case-insensitive on Windows).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    foreach ($exact in @('NUGET_API_KEY', 'GH_TOKEN', 'GITHUB_TOKEN')) {
        if ($Name -eq $exact) { return $true }
    }
    foreach ($prefix in @('AWS_', 'AZURE_')) {
        if ($Name.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Get-GimScrubbedEnvList {
    <#
    .SYNOPSIS
        Names of credential variables currently present in the process
        environment that the scrub will strip from a child. Exposed in
        --dry-run output so tests can verify without executing.
    #>
    $names = New-Object System.Collections.Generic.List[string]
    foreach ($key in @([System.Environment]::GetEnvironmentVariables().Keys)) {
        $name = [string]$key
        if (Test-GimScrubbedVarName -Name $name) { $names.Add($name) }
    }
    return @($names.ToArray() | Sort-Object)
}

function Get-GimRunAuditPath {
    <#
    .SYNOPSIS
        Path of the run audit log: GIM_RUN_AUDIT env override (validated
        rooted local path - tests point it at a temp file) or
        %LOCALAPPDATA%\gim\run-audit.log.
    #>
    param([string]$Path)
    if ($Path) { return $Path }
    if ($env:GIM_RUN_AUDIT) {
        $p = $env:GIM_RUN_AUDIT
        if ($p -match '%[^%]+%') {
            Write-GimError "invalid GIM_RUN_AUDIT '$p': environment variables are not expanded (give the full path)"
        }
        if (-not [System.IO.Path]::IsPathRooted($p)) {
            Write-GimError "invalid GIM_RUN_AUDIT '$p': must be an absolute path"
        }
        return $p
    }
    return Join-Path (Get-GimLocalDataRoot) 'gim\run-audit.log'
}

function Add-GimRunAudit {
    <#
    .SYNOPSIS
        Append one audit line (timestamp, cwd, verb, project, args, exit
        code) to the run audit log. Best-effort with retry, mirroring the
        variable-store audit.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Verb,
        [AllowEmptyString()]
        [string]$Project,
        [Parameter(Mandatory = $true)]
        [string]$Cwd,
        [Parameter(Mandatory = $true)]
        [string[]]$Argv,
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,
        [string]$Path = ''
    )
    $path = Get-GimRunAuditPath -Path $Path
    $quoted = New-Object System.Collections.Generic.List[string]
    foreach ($a in $Argv) { $quoted.Add((Quote-Arg -Argument $a)) }
    $argsText = ([string]::Join(' ', $quoted.ToArray()) -replace '\r?\n', ' ')
    $cwdClean = ($Cwd -replace '\r?\n', ' ')
    $projClean = ($Project -replace '\r?\n', ' ')
    if ($projClean -eq '') { $projClean = '-' }
    $line = ([datetime]::UtcNow.ToString('o') + ' ' + $cwdClean + ' ' + $Verb + ' ' + $projClean + ' ' + $argsText + ' ' + $ExitCode + "`n")
    $dir = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            [System.IO.File]::AppendAllText($path, $line, (New-Object System.Text.UTF8Encoding($false)))
            return
        } catch [System.IO.IOException] {
            # Concurrent writers contend for the shared audit log; back off
            # briefly and retry. Audit is best-effort bookkeeping.
            if ($attempt -lt 5) { Start-Sleep -Milliseconds (25 * $attempt) }
        }
    }
}

function Resolve-GimExecProject {
    <#
    .SYNOPSIS
        Resolve a gim-run/gim-probe project path to a real absolute path and
        assert containment in the current repo (exec roots = current repo +
        .agent-scratch/ - narrower than the read roots).
    .DESCRIPTION
        Relative paths resolve against the repo root (never the harness cwd).
        Unexpanded %VAR% tokens, UNC paths, `..\..` escapes, and
        junction/symlink escapes are rejected exactly like write paths:
        real-path resolution collapses `..` and follows reparse points, so
        the containment check sees the final target.
    #>
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Path
    )
    if ($Path -match '%[^%]+%') {
        Write-GimError "invalid exec path '$Path': environment variables are not expanded (give the full path)"
    }
    $repoRoot = Get-GimRepoRoot
    $full = $Path
    if (-not [System.IO.Path]::IsPathRooted($full)) {
        $full = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $full))
    }
    $real = Get-GimRealPath -Path $full
    if (-not (Test-GimContainedPath -RealPath $real -RootRealPaths (Get-GimWriteRoots))) {
        Write-GimError "exec path not contained in current repo: '$Path' (resolved '$real')"
    }
    return $real
}

function Get-GimExecStartInfo {
    <#
    .SYNOPSIS
        Build a ProcessStartInfo from an argv array: argv[0] is the FileName,
        the rest are Quote-Arg-joined (Windows CommandLineToArgvW rules, no
        shell), WorkingDirectory is explicit, UseShellExecute=false, and the
        child environment is scrubbed of credential variables.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Argv,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )
    if ($Argv.Count -eq 0) { Write-GimError 'internal error: empty argv' }
    $quoted = New-Object System.Collections.Generic.List[string]
    for ($i = 1; $i -lt $Argv.Count; $i++) { $quoted.Add((Quote-Arg -Argument $Argv[$i])) }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Argv[0]
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Arguments = [string]::Join(' ', $quoted.ToArray())
    foreach ($key in @($psi.EnvironmentVariables.Keys)) {
        if (Test-GimScrubbedVarName -Name ([string]$key)) {
            [void]$psi.EnvironmentVariables.Remove($key)
        }
    }
    return $psi
}

function Stop-GimProcessTree {
    <#
    .SYNOPSIS
        Kill a process and its whole tree via taskkill /PID <pid> /T /F.
        Never Stop-Process alone (it cannot reach the tree). Best-effort:
        a process that already exited is a no-op.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Process
    )
    $killPsi = New-Object System.Diagnostics.ProcessStartInfo
    $killPsi.FileName = 'taskkill'
    $killPsi.Arguments = ('/PID {0} /T /F' -f $Process.Id)
    $killPsi.UseShellExecute = $false
    $killPsi.CreateNoWindow = $true
    $kill = New-Object System.Diagnostics.Process
    $kill.StartInfo = $killPsi
    try {
        [void]$kill.Start()
        $kill.WaitForExit(10000) | Out-Null
        $kill.Dispose()
    } catch {
        # Already exited or taskkill unavailable - teardown must not throw.
    }
}

function New-GimPipeState {
    <#
    .SYNOPSIS
        Pending-read state for streaming both redirected pipes: two
        ReadLineAsync tasks plus captured (capped) line buffers.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Process
    )
    $state = [pscustomobject]@{
        OutReader = $Process.StandardOutput
        ErrReader = $Process.StandardError
        OutTask = $null
        ErrTask = $null
        OutDone = $false
        ErrDone = $false
        OutLines = New-Object System.Collections.Generic.List[string]
        ErrLines = New-Object System.Collections.Generic.List[string]
        OutBytes = 0L
        ErrBytes = 0L
        Truncated = $false
    }
    $state.OutTask = $state.OutReader.ReadLineAsync()
    $state.ErrTask = $state.ErrReader.ReadLineAsync()
    return $state
}

function Update-GimPipeState {
    <#
    .SYNOPSIS
        One non-blocking pump pass over the pending pipe tasks: completed
        lines are captured (capped at MaxItems/MaxBytes) and optionally
        streamed - stdout lines to the console ([Console]::WriteLine, so they
        are never captured into a caller's result array), stderr lines to the
        console error stream. EOF and faulted tasks mark the pipe done.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$State,
        [switch]$Stream,
        [int]$MaxItems = $script:GimMaxItems,
        [long]$MaxBytes = $script:GimMaxBytes
    )
    if (-not $State.OutDone -and $State.OutTask.IsCompleted) {
        $line = $null
        try {
            $line = $State.OutTask.Result
        } catch {
            # Pipe faulted (e.g. stream closed after a tree kill) - treat as EOF.
            $State.OutDone = $true
        }
        if (-not $State.OutDone) {
            if ($null -ne $line) {
                $lineBytes = [System.Text.Encoding]::UTF8.GetByteCount($line) + 2
                if (-not $State.Truncated -and $State.OutLines.Count -lt $MaxItems -and ($State.OutBytes + $lineBytes) -le $MaxBytes) {
                    [void]$State.OutLines.Add($line)
                    $State.OutBytes += $lineBytes
                    if ($Stream) { [Console]::WriteLine($line) }
                } else {
                    $State.Truncated = $true
                }
                $State.OutTask = $State.OutReader.ReadLineAsync()
            } else {
                $State.OutDone = $true
            }
        }
    }
    if (-not $State.ErrDone -and $State.ErrTask.IsCompleted) {
        $line = $null
        try {
            $line = $State.ErrTask.Result
        } catch {
            $State.ErrDone = $true
        }
        if (-not $State.ErrDone) {
            if ($null -ne $line) {
                $lineBytes = [System.Text.Encoding]::UTF8.GetByteCount($line) + 2
                if (-not $State.Truncated -and $State.ErrLines.Count -lt $MaxItems -and ($State.ErrBytes + $lineBytes) -le $MaxBytes) {
                    [void]$State.ErrLines.Add($line)
                    $State.ErrBytes += $lineBytes
                    if ($Stream) { [Console]::Error.WriteLine($line) }
                } else {
                    $State.Truncated = $true
                }
                $State.ErrTask = $State.ErrReader.ReadLineAsync()
            } else {
                $State.ErrDone = $true
            }
        }
    }
}

function Invoke-GimExec {
    <#
    .SYNOPSIS
        Launch an argv process to completion (or hard timeout): stream output
        with caps, kill the tree via taskkill on expiry, drain, and append an
        audit line. Returns @{ TimedOut; ExitCode; Stdout; Stderr;
        Truncated }.
    .DESCRIPTION
        Shared launcher for gim-run (and the building blocks gim-probe
        composes). Waits up to TimeoutSeconds (default 300, cap enforced by
        the verb), pumping both pipes live so no pipe-buffer deadlock. On
        expiry the process tree is killed with taskkill /PID <pid> /T /F
        (never Stop-Process alone) and the reported exit code is 124. In
        stream mode (gim-run text output) child stdout/stderr lines go
        directly to the console so they are never captured into the caller's
        result; the returned object is the ONLY pipeline output. After the
        process exits, the pipe drain is bounded by
        GimExecDrainGraceSeconds (3s) so a grandchild holding the redirected
        pipe cannot stall the run. The process object is always disposed. The
        audit line is 'timestamp cwd verb project args exit_code' appended to
        Get-GimRunAuditPath (-AuditPath overrides for hermetic tests).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Argv,
        [Parameter(Mandatory = $true)]
        [string]$Verb,
        [AllowEmptyString()]
        [string]$Project = '',
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [int]$TimeoutSeconds = $script:GimRunDefaultTimeout,
        [string]$AuditPath = '',
        [switch]$Stream
    )
    $psi = Get-GimExecStartInfo -Argv $Argv -WorkingDirectory $WorkingDirectory
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $timedOut = $false
    $exitCode = 1
    $result = $null
    try {
        try {
            [void]$proc.Start()
        } catch {
            Add-GimRunAudit -Verb $Verb -Project $Project -Cwd $WorkingDirectory -Argv $Argv -ExitCode 1 -Path $AuditPath
            Write-GimError "failed to start process: '$($Argv[0])' (check it is installed and on PATH)"
        }
        $state = New-GimPipeState -Process $proc
        $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not $proc.HasExited -and [datetime]::UtcNow -lt $deadline) {
            Update-GimPipeState -State $state -Stream:$Stream
            Start-Sleep -Milliseconds 20
        }
        $timedOut = -not $proc.HasExited
        if ($timedOut) {
            Stop-GimProcessTree -Process $proc
            $proc.WaitForExit(10000) | Out-Null
        } else {
            $proc.WaitForExit() | Out-Null
        }
        # Bounded drain: a grandchild may hold the redirected pipe open after
        # the child exits; stop after a short grace so the run never stalls.
        $drainDeadline = [datetime]::UtcNow.AddSeconds($script:GimExecDrainGraceSeconds)
        while ((-not $state.OutDone -or -not $state.ErrDone) -and [datetime]::UtcNow -lt $drainDeadline) {
            Update-GimPipeState -State $state -Stream:$Stream
            Start-Sleep -Milliseconds 10
        }
        try { $exitCode = $proc.ExitCode } catch { $exitCode = 1 }
        if ($timedOut) { $exitCode = 124 }
        $result = [pscustomobject]@{
            TimedOut = $timedOut
            ExitCode = $exitCode
            Stdout = @($state.OutLines.ToArray())
            Stderr = @($state.ErrLines.ToArray())
            Truncated = $state.Truncated
        }
    } finally {
        try { $proc.Dispose() } catch { }
    }
    Add-GimRunAudit -Verb $Verb -Project $Project -Cwd $WorkingDirectory -Argv $Argv -ExitCode $exitCode -Path $AuditPath
    return $result
}
