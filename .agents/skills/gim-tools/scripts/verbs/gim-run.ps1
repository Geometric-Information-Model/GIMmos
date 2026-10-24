# gim-run.ps1 - execution tier: run dotnet build/test/run/publish/ef/format
# on a contained project. argv built with Quote-Arg, launched via
# [System.Diagnostics.Process] (no shell), environment scrubbed of
# credential variables, hard timeout with taskkill tree kill, audit line
# appended, exit code propagated.
#
# Trailing child args ('-- <args>...'): the PowerShell binder consumes the
# FIRST '--' token before a function ever sees it, so colliding child flags
# (--json/--dry-run/--project/--project-from-var/--timeout) after a SINGLE
# '--' would be re-interpreted as gim-run's own flags. Non-colliding trailing
# args work with a single '--' (unknown flags fall through as positional).
# For colliding tokens the marker must be doubled: '-- -- <args>' - the
# second '--' reaches Convert-GimArgs, whose end-of-options branch routes
# everything after it VERBATIM into the child argv (never re-parsed as gim
# flags). This is the reliable contract: PowerShell itself consumes the first
# marker, so the boundary is expressed by the second.

function Get-GimRunArgv {
    # argv = dotnet <verb> [--project <p>] <p> [-- <args>...]
    # 'run' inserts '--' before trailing args (dotnet run forwards them to the
    # app); 'ef' takes --project as an option; the rest take the project
    # positionally.
    param(
        [Parameter(Mandatory = $true)]
        [string]$Verb,
        [Parameter(Mandatory = $true)]
        [string]$Project,
        [AllowEmptyCollection()]
        [string[]]$Args
    )
    $a = New-Object System.Collections.Generic.List[string]
    $a.Add('dotnet')
    $a.Add($Verb)
    switch ($Verb) {
        'run' {
            $a.Add('--project'); $a.Add($Project)
            if ($Args.Count -gt 0) {
                $a.Add('--')
                foreach ($arg in $Args) { $a.Add($arg) }
            }
        }
        'ef' {
            $a.Add('--project'); $a.Add($Project)
            foreach ($arg in $Args) { $a.Add($arg) }
        }
        default {
            $a.Add($Project)
            foreach ($arg in $Args) { $a.Add($arg) }
        }
    }
    return @($a.ToArray())
}

function gim-run {
    <#
    .SYNOPSIS
        Run a dotnet CLI verb on a contained project with a hard timeout.
    .DESCRIPTION
        Verbs: build, test, run, publish, ef, format. The project resolves
        from --project (relative paths resolve against the repo root), from
        --project-from-var <name> (A5 variable store, TTL-enforced), or
        defaults to the current directory - then is made absolute and must be
        contained in the current repo + .agent-scratch/ (exec roots, narrower
        than the read roots; $env:TEMP, UNC paths, `..\..` escapes, and
        junction/symlink escapes are rejected).

        Trailing child args come after a '--' token. PowerShell itself
        consumes the FIRST '--', so trailing tokens that collide with
        gim-run's own flags (--json, --dry-run, --project,
        --project-from-var, --timeout) must follow a DOUBLED marker:
        'gim-run <verb> <flags> -- -- --json' passes '--json' verbatim to the
        child. Non-colliding trailing args work with a single '--'
        (e.g. '-- -c Release'). For the 'run' verb the trailing args follow a
        '--' so dotnet forwards them to the app.

        The child environment is scrubbed of credential variables
        (NUGET_API_KEY, GH_TOKEN, GITHUB_TOKEN, AWS_*, AZURE_*); --dry-run
        prints the argv plus the scrubbed names without executing. Timeout
        default 300s, cap 1800s; on expiry the whole process tree is killed
        (taskkill /PID <pid> /T /F) and the verb exits 124. In text mode
        stdout/stderr are streamed to the console and the verb exits with the
        child's exit code on failure (124 on timeout); in --json mode the
        process exits 0 and the envelope carries { verb, project, argv,
        exit_code, timed_out, stdout, stderr, truncated }. Every real
        invocation appends an audit line to %LOCALAPPDATA%\gim\run-audit.log
        (GIM_RUN_AUDIT overrides).
    .PARAMETER Raw
        Positional verb plus '--flag value' options (see DESCRIPTION):
        --project <p>, --project-from-var <name>, --timeout <sec>,
        --dry-run, --json, then '--' (doubled for colliding tokens) for
        verbatim trailing dotnet args.
    .EXAMPLE
        gim-run test --project TestPlatform/TestPlatform.csproj --dry-run
    .EXAMPLE
        gim-run run --project GIMtransfer/GIMtransfer.csproj --dry-run -- --urls http://127.0.0.1:5000
    .EXAMPLE
        gim-run ef --project Main/Main.csproj --dry-run -- -- --project Other/Other.csproj
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
                '--project'          = 'Project'
                '--project-from-var' = 'ProjectFromVar'
                '--timeout'          = 'Timeout'
            } `
            -SwitchFlags @{
                '--dry-run' = 'DryRun'
                '--json'    = 'Json'
            }
        $pos = @($parsed.Positional)
        if ($pos.Count -eq 0) {
            Write-GimError 'missing verb: give build, test, run, publish, ef, or format'
        }
        $verb = [string]$pos[0]
        if ($verb -notin @('build', 'test', 'run', 'publish', 'ef', 'format')) {
            Write-GimError "invalid verb: '$verb' (expected build, test, run, publish, ef, or format)"
        }
        $trailing = @()
        if ($pos.Count -gt 1) { $trailing = @($pos[1..($pos.Count - 1)]) }
        $projectFlag = ''
        if ($parsed.ContainsKey('Project')) { $projectFlag = [string]$parsed['Project'] }
        $projectVar = ''
        if ($parsed.ContainsKey('ProjectFromVar')) { $projectVar = [string]$parsed['ProjectFromVar'] }
        if ($projectFlag -and $projectVar) {
            Write-GimError 'give either --project or --project-from-var, not both'
        }
        $timeout = $script:GimRunDefaultTimeout
        if ($parsed.ContainsKey('Timeout')) {
            $timeoutRaw = [string]$parsed['Timeout']
            if (-not [int]::TryParse($timeoutRaw, [ref]$timeout)) {
                Write-GimError "invalid --timeout value: '$timeoutRaw' (expected whole seconds)"
            }
            if ($timeout -lt 1) { Write-GimError "invalid --timeout value: '$timeoutRaw' (minimum 1 second)" }
            if ($timeout -gt $script:GimRunMaxTimeout) { Write-GimError "invalid --timeout value: '$timeoutRaw' (cap $($script:GimRunMaxTimeout)s)" }
        }
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
    }
    end {
        $project = ''
        if ($projectVar) {
            $project = Get-GimVarValueEnforced -Name $projectVar
            if ($null -eq $project) {
                Write-GimError "variable not set: '$projectVar' (use gim-set to store it)"
            }
            $project = $project.Trim()
            if ($project -eq '') { Write-GimError "variable '$projectVar' holds an empty project path" }
        } elseif ($projectFlag) {
            $project = $projectFlag
            if ($project.Trim() -eq '') { Write-GimError 'missing project: --project must not be empty' }
        } else {
            $project = (Get-Location).Path
        }
        $project = Resolve-GimExecProject -Path $project
        $argv = @(Get-GimRunArgv -Verb $verb -Project $project -Args $trailing)
        $scrubNames = @(Get-GimScrubbedEnvList)
        if ($dryRun) {
            if ($jsonMode) {
                Write-GimJson ([pscustomobject]@{
                    dry_run = $true
                    verb = $verb
                    project = $project
                    argv = $argv
                    scrubbed_env = $scrubNames
                })
                return
            }
            foreach ($a in $argv) { Write-Output $a }
            if ($scrubNames.Count -gt 0) { Write-Output ('scrubbed: ' + ($scrubNames -join ', ')) }
            else { Write-Output 'scrubbed: (none)' }
            return
        }
        $result = Invoke-GimExec -Argv $argv -Verb 'run' -Project $project -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds $timeout -Stream:(-not $jsonMode)
        $global:LASTEXITCODE = $result.ExitCode
        if ($jsonMode) {
            Write-GimJson ([pscustomobject]@{
                dry_run = $false
                verb = $verb
                project = $project
                argv = $argv
                exit_code = $result.ExitCode
                timed_out = $result.TimedOut
                stdout = [string]::Join("`n", $result.Stdout)
                stderr = [string]::Join("`n", $result.Stderr)
                truncated = $result.Truncated
            })
            return
        }
        if ($result.Truncated -and $result.ExitCode -eq 0) { Write-GimTruncationMarker -Items $result.Stdout.Count }
        if ($result.TimedOut) {
            [Console]::Error.WriteLine(('gim: timed out after {0}s, process tree killed' -f $timeout))
            exit 124
        }
        if ($result.ExitCode -ne 0) {
            [Console]::Error.WriteLine(('gim: {0} exited with code {1}' -f $verb, $result.ExitCode))
            exit $result.ExitCode
        }
    }
}
