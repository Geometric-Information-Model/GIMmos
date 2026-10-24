# gim-probe.ps1 - launch an app on a random local port (dotnet run --urls),
# poll HTTP readiness, issue one request, print status + body, and ALWAYS
# teardown (kill the process tree via taskkill). Reduces the agent's reason
# to author throwaway scratch projects.

function Get-GimFreePort {
    # Bind a loopback listener on port 0 to get a free port, then release it.
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    return $port
}

function gim-probe {
    <#
    .SYNOPSIS
        Launch an app on a random local port and issue one HTTP request.
    .DESCRIPTION
        Launches 'dotnet run --project <p> --urls http://127.0.0.1:<random>'
        (WorkingDirectory pinned to the repo root), polls the base URL with
        HTTP GET until any response arrives or the timeout expires, then
        issues the request for --route with --method (default GET) and an
        optional --body-file (contained in the current repo +
        .agent-scratch/, sent as application/json). --project must be
        contained in the current repo (exec roots, same rules as gim-run).
        On success prints 'status: <code>' plus the response body; on
        readiness timeout the app output is surfaced so build/server errors
        are visible. The process tree is ALWAYS killed at the end (taskkill
        /PID <pid> /T /F - never Stop-Process alone). Timeout default 300s,
        cap 1800s; exit code 124 on readiness timeout, 1 on launch/request
        failure, 0 on success (any HTTP status). The child environment is
        scrubbed of credential variables; --dry-run prints the argv and the
        scrubbed names without launching. Every real invocation appends an
        audit line to %LOCALAPPDATA%\gim\run-audit.log (GIM_RUN_AUDIT
        overrides). --json emits { project, route, method, port,
        status_code, body, exit_code, timed_out } (dry run adds argv and
        scrubbed_env).
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION): --project <p> (required),
        --route <r> (required), --method <m>, --body-file <f>,
        --timeout <sec>, --dry-run, --json.
    .EXAMPLE
        gim-probe --project GIM.WebAPI/GIM.WebAPI.csproj --route /api/health
    .EXAMPLE
        gim-probe --project App/App.csproj --route /api/items --method POST --body-file .agent-scratch/body.json
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
                '--project'   = 'Project'
                '--route'     = 'Route'
                '--method'    = 'Method'
                '--body-file' = 'BodyFile'
                '--timeout'   = 'Timeout'
            } `
            -SwitchFlags @{
                '--dry-run' = 'DryRun'
                '--json'    = 'Json'
            }
        if ($parsed.Positional.Count -gt 0) {
            Write-GimError "unexpected arguments: '$($parsed.Positional -join ' ')' (gim-probe takes no positional arguments)"
        }
        if (-not $parsed.ContainsKey('Route')) {
            Write-GimError 'missing route: give --route <r>'
        }
        $route = [string]$parsed['Route']
        if ($route.Trim() -eq '') { Write-GimError 'missing route: --route must not be empty' }
        $method = 'GET'
        if ($parsed.ContainsKey('Method')) { $method = [string]$parsed['Method'] }
        $methodUpper = $method.ToUpperInvariant()
        if ($methodUpper -notin @('GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS')) {
            Write-GimError "invalid --method value: '$method' (expected GET, POST, PUT, DELETE, PATCH, HEAD, or OPTIONS)"
        }
        $bodyFile = ''
        if ($parsed.ContainsKey('BodyFile')) { $bodyFile = [string]$parsed['BodyFile'] }
        $projectFlag = ''
        if ($parsed.ContainsKey('Project')) { $projectFlag = [string]$parsed['Project'] }
        if ($projectFlag.Trim() -eq '') {
            Write-GimError 'missing project: give --project <p>'
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
        $bodyText = ''
    }
    end {
        $project = Resolve-GimExecProject -Path $projectFlag
        if ($bodyFile) {
            $real = Resolve-GimWritePath -Path $bodyFile
            if (-not (Test-Path -LiteralPath $real)) {
                Write-GimError "body file not found: '$bodyFile'"
            }
            $bodyText = [System.IO.File]::ReadAllText($real)
        }
        $port = Get-GimFreePort
        $urls = 'http://127.0.0.1:' + $port
        $argv = @('dotnet', 'run', '--project', $project, '--urls', $urls)
        $scrubNames = @(Get-GimScrubbedEnvList)
        if ($dryRun) {
            if ($jsonMode) {
                Write-GimJson ([pscustomobject]@{
                    dry_run = $true
                    project = $project
                    route = $route
                    method = $methodUpper
                    port = $port
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
        $psi = Get-GimExecStartInfo -Argv $argv -WorkingDirectory (Get-GimRepoRoot)
        $proc = New-Object System.Diagnostics.Process
        $proc.StartInfo = $psi
        try {
            [void]$proc.Start()
        } catch {
            Add-GimRunAudit -Verb 'probe' -Project $project -Cwd (Get-GimRepoRoot) -Argv $argv -ExitCode 1
            try { $proc.Dispose() } catch { }
            Write-GimError "failed to start probe process: '$($argv[0])' (check it is installed and on PATH)"
        }
        $state = New-GimPipeState -Process $proc
        $client = New-Object System.Net.Http.HttpClient
        $client.Timeout = [timespan]::FromSeconds(10)
        $base = 'http://127.0.0.1:' + $port + '/'
        $deadline = [datetime]::UtcNow.AddSeconds($timeout)
        $ready = $false
        $status = 0
        $responseBody = ''
        $timedOut = $false
        $failure = ''
        try {
            while (-not $proc.HasExited -and [datetime]::UtcNow -lt $deadline) {
                Update-GimPipeState -State $state
                if (-not $ready) {
                    try {
                        $r = $client.GetAsync($base).Result
                        $r.Dispose()
                        $ready = $true
                    } catch {
                        # Connection refused while the app starts - keep polling.
                    }
                }
                Start-Sleep -Milliseconds 250
            }
            if (-not $ready) {
                $timedOut = $true
            } else {
                $url = $route
                if ($url -notmatch '^https?://') {
                    $url = $base.TrimEnd('/') + '/' + $url.TrimStart('/')
                }
                $msg = New-Object System.Net.Http.HttpRequestMessage
                $msg.Method = New-Object System.Net.Http.HttpMethod($methodUpper)
                $msg.RequestUri = New-Object System.Uri($url)
                if ($bodyText -ne '') {
                    $msg.Content = New-Object System.Net.Http.StringContent($bodyText, [System.Text.Encoding]::UTF8, 'application/json')
                }
                try {
                    $resp = $client.SendAsync($msg).Result
                    $status = [int]$resp.StatusCode
                    if ($null -ne $resp.Content) {
                        $responseBody = $resp.Content.ReadAsStringAsync().Result
                    }
                    $resp.Dispose()
                } catch {
                    $ex = $_.Exception
                    if ($ex.InnerException) { $ex = $ex.InnerException }
                    $failure = 'request failed: ' + $ex.Message
                }
            }
        } finally {
            $client.Dispose()
            # ALWAYS teardown: kill the process tree if it is still running,
            # drain with a bounded grace, then dispose the process object.
            try {
                if (-not $proc.HasExited) {
                    Stop-GimProcessTree -Process $proc
                    $proc.WaitForExit(10000) | Out-Null
                }
                $drainDeadline = [datetime]::UtcNow.AddSeconds($script:GimExecDrainGraceSeconds)
                while ((-not $state.OutDone -or -not $state.ErrDone) -and [datetime]::UtcNow -lt $drainDeadline) {
                    Update-GimPipeState -State $state
                    Start-Sleep -Milliseconds 10
                }
            } finally {
                try { $proc.Dispose() } catch { }
            }
            $outcome = 0
            if ($timedOut) { $outcome = 124 }
            elseif ($failure) { $outcome = 1 }
            Add-GimRunAudit -Verb 'probe' -Project $project -Cwd (Get-GimRepoRoot) -Argv $argv -ExitCode $outcome
        }
        if ($timedOut) {
            # Surface the app output (build/server errors) before propagating
            # the documented timeout exit code 124. Console writes are used so
            # nothing is lost when the runspace exits.
            foreach ($l in $state.OutLines) { [Console]::WriteLine($l) }
            foreach ($l in $state.ErrLines) { [Console]::Error.WriteLine($l) }
            [Console]::Error.WriteLine(('gim: probe readiness timed out after {0}s against {1}' -f $timeout, $base))
            exit 124
        }
        if ($failure) {
            foreach ($l in $state.OutLines) { [Console]::WriteLine($l) }
            foreach ($l in $state.ErrLines) { [Console]::Error.WriteLine($l) }
            [Console]::Error.WriteLine(('gim: probe {0}' -f $failure))
            exit 1
        }
        if ($jsonMode) {
            Write-GimJson ([pscustomobject]@{
                project = $project
                route = $route
                method = $methodUpper
                port = $port
                status_code = $status
                body = $responseBody
                exit_code = 0
                timed_out = $false
            })
            return
        }
        Write-Output ('status: ' + $status)
        if ($responseBody -ne '') {
            if ([System.Text.Encoding]::UTF8.GetByteCount($responseBody) -gt $script:GimMaxBytes) {
                Write-Output $responseBody.Substring(0, [Math]::Min($responseBody.Length, $script:GimMaxBytes))
                Write-GimTruncationMarker -Items 1
            } else {
                Write-Output $responseBody
            }
        }
    }
}
