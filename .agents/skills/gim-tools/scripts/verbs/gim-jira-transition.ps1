# gim-jira-transition.ps1 - fleet tier (ADR 0008): status transition.
#
# Contract:
#   gim-jira-transition --key <key> --to <status> [--project <key>]
#                       [--dry-run] [--json]
#
# The current status is read with the positional-key form
# `acli jira workitem view <KEY> --fields status --json`. A target equal to
# the current status is a no-op. The D0 `jira-transitions.json` config is the
# operational source for statuses and route sequences; each sequence step runs
# `acli jira workitem transition --key <KEY> --status "<step>" --yes --json`.
# Execution stops at the first failure and returns the exact command and
# result. `--dry-run` makes no acli call: it prints the placeholder current
# status, the direct target sequence, and the exact command(s).

function Get-GimJiraTransitionConfigPath {
    <#
    .SYNOPSIS
        Path to the D0-generated transition config.
    #>
    return (Join-Path $script:GimModuleRoot 'fleet\jira-transitions.json')
}

function Get-GimJiraTransitionConfig {
    <#
    .SYNOPSIS
        Read and validate jira-transitions.json.
    .DESCRIPTION
        Returns { ok, path, project, statuses[], routes[], error }. A route is
        { from, to, sequence[] }. `routes[].sequence` is the operational D3
        source; the probe chronology is evidence only and is not read.
    #>
    param(
        [AllowEmptyString()]
        [string]$Path = ''
    )
    if ($Path -eq '') { $Path = Get-GimJiraTransitionConfigPath }
    $failure = {
        param($Message)
        [pscustomobject]@{
            ok = $false
            path = $Path
            project = ''
            statuses = @()
            routes = @()
            error = $Message
        }
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return (& $failure ("transition config not found: '{0}'; required input: run the D0 probe to create it" -f $Path))
    }
    try {
        $doc = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    } catch {
        return (& $failure ("transition config is unparseable: '{0}'" -f $Path))
    }
    if ($null -eq $doc) {
        return (& $failure ("transition config is empty: '{0}'" -f $Path))
    }
    $project = [string](Get-GimFleetJiraProperty -Object $doc -Names @('project'))
    if ($project.Trim() -eq '') {
        return (& $failure ("transition config has no project: '{0}'" -f $Path))
    }
    $project = $project.Trim().ToUpperInvariant()

    $statuses = New-Object System.Collections.Generic.List[string]
    foreach ($entry in @($doc.statuses)) {
        $name = [string](Get-GimFleetJiraProperty -Object $entry -Names @('name'))
        if ($name.Trim() -eq '') {
            return (& $failure ("transition config has a status without a name: '{0}'" -f $Path))
        }
        $name = $name.Trim()
        if ($statuses.Contains($name)) {
            return (& $failure ("transition config repeats status '{0}': '{1}'" -f $name, $Path))
        }
        [void]$statuses.Add($name)
    }
    if ($statuses.Count -eq 0) {
        return (& $failure ("transition config has no statuses: '{0}'" -f $Path))
    }

    $routes = New-Object System.Collections.Generic.List[object]
    foreach ($entry in @($doc.routes)) {
        $from = [string](Get-GimFleetJiraProperty -Object $entry -Names @('from'))
        $to = [string](Get-GimFleetJiraProperty -Object $entry -Names @('to'))
        if ($from.Trim() -eq '' -or $to.Trim() -eq '') {
            return (& $failure ("transition config has a route without from/to: '{0}'" -f $Path))
        }
        $from = $from.Trim()
        $to = $to.Trim()
        if (-not $statuses.Contains($from) -or -not $statuses.Contains($to)) {
            return (& $failure ("transition config route '{0}' -> '{1}' uses an unknown status: '{2}'" -f $from, $to, $Path))
        }
        $sequence = New-Object System.Collections.Generic.List[string]
        foreach ($step in @(Get-GimFleetJiraProperty -Object $entry -Names @('sequence'))) {
            $name = ([string]$step).Trim()
            if ($name -eq '' -or -not $statuses.Contains($name)) {
                return (& $failure ("transition config route '{0}' -> '{1}' has an invalid sequence step: '{2}'" -f $from, $to, $Path))
            }
            [void]$sequence.Add($name)
        }
        if ($sequence.Count -eq 0) {
            return (& $failure ("transition config route '{0}' -> '{1}' has an empty sequence: '{2}'" -f $from, $to, $Path))
        }
        [void]$routes.Add([pscustomobject]@{
            from = $from
            to = $to
            sequence = @($sequence.ToArray())
        })
    }
    if ($routes.Count -eq 0) {
        return (& $failure ("transition config has no routes: '{0}'" -f $Path))
    }
    return [pscustomobject]@{
        ok = $true
        path = $Path
        project = $project
        statuses = @($statuses.ToArray())
        routes = @($routes.ToArray())
        error = $null
    }
}

function ConvertTo-GimJiraTransitionStatusKey {
    <#
    .SYNOPSIS
        Normalize a status for matching: trim outer whitespace, fold case.
    #>
    param(
        [AllowEmptyString()]
        [string]$Value = ''
    )
    return ([string]$Value).Trim().ToLowerInvariant()
}

function Get-GimJiraTransitionRoute {
    <#
    .SYNOPSIS
        Select the configured route for a current target pair.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Config,
        [Parameter(Mandatory = $true)]
        [string]$From,
        [Parameter(Mandatory = $true)]
        [string]$To
    )
    $fromKey = ConvertTo-GimJiraTransitionStatusKey -Value $From
    $toKey = ConvertTo-GimJiraTransitionStatusKey -Value $To
    foreach ($route in @($Config.routes)) {
        if ((ConvertTo-GimJiraTransitionStatusKey -Value ([string]$route.from)) -eq $fromKey -and
            (ConvertTo-GimJiraTransitionStatusKey -Value ([string]$route.to)) -eq $toKey) {
            return $route
        }
    }
    return $null
}

function Format-GimJiraTransitionCommand {
    <#
    .SYNOPSIS
        Render the exact acli transition command for text output.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Key,
        [Parameter(Mandatory = $true)]
        [string]$Status
    )
    return ('acli jira workitem transition --key {0} --status {1} --yes --json' -f $Key, (Quote-Arg -Argument $Status))
}

function ConvertFrom-GimJiraTransitionViewJson {
    <#
    .SYNOPSIS
        Parse `workitem view --fields status --json` stdout.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text -or $Text.Trim() -eq '') {
        return [pscustomobject]@{ ok = $false; current = ''; error = 'acli workitem view returned empty JSON' }
    }
    try {
        $doc = $Text | ConvertFrom-Json
    } catch {
        return [pscustomobject]@{ ok = $false; current = ''; error = 'acli workitem view returned unparseable JSON' }
    }
    if ($doc -is [System.Array]) {
        if ($doc.Count -eq 0) {
            return [pscustomobject]@{ ok = $false; current = ''; error = 'acli workitem view returned no item' }
        }
        $doc = $doc[0]
    }
    $current = Get-GimFleetJiraStatusName -Object $doc
    if ($current -eq '') {
        return [pscustomobject]@{ ok = $false; current = ''; error = 'acli workitem view returned no status name' }
    }
    return [pscustomobject]@{ ok = $true; current = $current; error = $null }
}

function New-GimJiraTransitionResultRecord {
    <#
    .SYNOPSIS
        One stable result shape for success, failure, no-op, and dry-run.
    #>
    param(
        [bool]$Ok = $false,
        [bool]$DryRun = $false,
        [AllowEmptyString()][string]$Key = '',
        [AllowEmptyString()][string]$Project = '',
        [AllowEmptyString()][string]$Current = '',
        [AllowEmptyString()][string]$Target = '',
        [AllowEmptyCollection()][string[]]$Sequence = @(),
        [AllowEmptyCollection()][string[]]$Commands = @(),
        [AllowEmptyCollection()][object[]]$Results = @(),
        [AllowEmptyString()][string]$ErrorMessage = ''
    )
    return [pscustomobject]@{
        ok = $Ok
        dry_run = $DryRun
        key = $Key
        project = $Project
        current = $Current
        target = $Target
        sequence = @($Sequence)
        commands = @($Commands)
        results = @($Results)
        error = $ErrorMessage
    }
}

function Write-GimJiraTransitionFailure {
    <#
    .SYNOPSIS
        Emit a JSON failure envelope or throw the text error.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [bool]$JsonMode = $false,
        [bool]$DryRun = $false,
        [AllowEmptyString()][string]$Key = '',
        [AllowEmptyString()][string]$Project = '',
        [AllowEmptyString()][string]$Current = '',
        [AllowEmptyString()][string]$Target = '',
        [AllowEmptyCollection()][string[]]$Sequence = @(),
        [AllowEmptyCollection()][string[]]$Commands = @(),
        [AllowEmptyCollection()][object[]]$Results = @()
    )
    if ($JsonMode) {
        Write-GimJson (New-GimJiraTransitionResultRecord -Ok $false -DryRun $DryRun -Key $Key -Project $Project `
            -Current $Current -Target $Target -Sequence $Sequence -Commands $Commands -Results $Results `
            -ErrorMessage $Message)
        $global:LASTEXITCODE = 1
        exit 1
    }
    Write-GimError $Message
}

function Write-GimJiraTransitionResult {
    <#
    .SYNOPSIS
        Emit the transition result as text or JSON.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [bool]$JsonMode = $false
    )
    if ($JsonMode) {
        Write-GimJson $Result
        return
    }
    if ($Result.dry_run) {
        Write-Output ('current: {0}' -f [string]$Result.current)
        Write-Output ('target: {0}' -f [string]$Result.target)
        Write-Output ('sequence: {0}' -f (@($Result.sequence) -join ' -> '))
        foreach ($command in @($Result.commands)) { Write-Output ('command: ' + [string]$command) }
        return
    }
    if (@($Result.sequence).Count -eq 0) {
        Write-Output ('{0} already {1} (no-op)' -f [string]$Result.key, [string]$Result.target)
        return
    }
    foreach ($step in @($Result.results)) {
        Write-Output ('transitioned {0} to {1}' -f [string]$Result.key, [string]$step.status)
    }
}

function gim-jira-transition {
    <#
    .SYNOPSIS
        Transition a Jira work item to a configured status.
    .DESCRIPTION
        Signature:
          gim-jira-transition --key <key> --to <status> [--project <key>]
                              [--dry-run] [--json]

        Reads the current status with the positional-key form
        `acli jira workitem view <KEY> --fields status --json`. A target equal
        to the current status is a no-op. Otherwise it selects the configured
        route from `fleet/jira-transitions.json`, executes each sequence step
        with `acli jira workitem transition --key <KEY> --status "<step>"
        --yes --json` (the CLI flag is --status; --to is this verb's name), and
        stops at the first failure returning the exact command and result.
        --dry-run makes no acli call: current is `<current-status>`, sequence
        is the direct target step, and the exact command is printed.
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION).
    .EXAMPLE
        gim-jira-transition --key GIM-159 --to 'In Progress' --dry-run --json
    .EXAMPLE
        gim-jira-transition --key GIM-159 --to 'Done' --json
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
                '--key'     = 'Key'
                '--to'      = 'To'
                '--project' = 'Project'
            } `
            -SwitchFlags @{
                '--dry-run' = 'DryRun'
                '--json'    = 'Json'
            }
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
        if ($parsed.Positional.Count -gt 0) {
            Write-GimJiraTransitionFailure -Message ("unexpected arguments: '{0}' (gim-jira-transition takes --key, --to, optional --project, --dry-run, --json)" -f ($parsed.Positional -join ' ')) -JsonMode $jsonMode -DryRun $dryRun
        }
        if (-not $parsed.ContainsKey('Key')) {
            Write-GimJiraTransitionFailure -Message 'missing --key: give a work-item key such as GIM-123' -JsonMode $jsonMode -DryRun $dryRun
        }
        $key = ([string]$parsed['Key']).Trim().ToUpperInvariant()
        if ($key -notmatch '^[A-Z][A-Z0-9_]*-\d+$') {
            Write-GimJiraTransitionFailure -Message '--key must be a work-item key such as GIM-123' -JsonMode $jsonMode -DryRun $dryRun
        }
        if (-not $parsed.ContainsKey('To')) {
            Write-GimJiraTransitionFailure -Message 'missing --to: give a status name from the D0 config' -JsonMode $jsonMode -DryRun $dryRun
        }
        $target = ([string]$parsed['To']).Trim()
        if ($target -eq '') {
            Write-GimJiraTransitionFailure -Message '--to must not be empty' -JsonMode $jsonMode -DryRun $dryRun
        }
        $projectArg = ''
        if ($parsed.ContainsKey('Project')) {
            $projectArg = ([string]$parsed['Project']).Trim().ToUpperInvariant()
            if ($projectArg -eq '') {
                Write-GimJiraTransitionFailure -Message '--project must not be empty' -JsonMode $jsonMode -DryRun $dryRun
            }
            if ($projectArg -notmatch '^[A-Z][A-Z0-9_]*$') {
                Write-GimJiraTransitionFailure -Message ("--project '{0}' is not a Jira project key; required input: letters, digits, underscore" -f $projectArg) -JsonMode $jsonMode -DryRun $dryRun
            }
        }
    }
    end {
        $config = Get-GimJiraTransitionConfig
        if (-not $config.ok) {
            Write-GimJiraTransitionFailure -Message ([string]$config.error) -JsonMode $jsonMode -DryRun $dryRun -Key $key -Target $target
            return
        }
        $project = $config.project
        if ($projectArg -ne '') { $project = $projectArg }
        $keyPrefix = ($key -split '-')[0]
        if ($keyPrefix -ne $project) {
            Write-GimJiraTransitionFailure -Message ("key '{0}' does not belong to project '{1}'; required input: a {1} work-item key" -f $key, $project) -JsonMode $jsonMode -DryRun $dryRun -Key $key -Project $project -Target $target
            return
        }
        $matchedTarget = $null
        $targetKey = ConvertTo-GimJiraTransitionStatusKey -Value $target
        foreach ($name in @($config.statuses)) {
            if ((ConvertTo-GimJiraTransitionStatusKey -Value ([string]$name)) -eq $targetKey) {
                $matchedTarget = [string]$name
                break
            }
        }
        if ($null -eq $matchedTarget) {
            Write-GimJiraTransitionFailure -Message ("unknown --to '{0}'; valid statuses: {1}" -f $target, (@($config.statuses) -join ', ')) -JsonMode $jsonMode -DryRun $dryRun -Key $key -Project $project -Target $target
            return
        }
        $target = $matchedTarget

        if ($dryRun) {
            $command = Format-GimJiraTransitionCommand -Key $key -Status $target
            Write-GimJiraTransitionResult -JsonMode $jsonMode -Result (New-GimJiraTransitionResultRecord -Ok $true -DryRun $true `
                -Key $key -Project $project -Current '<current-status>' -Target $target -Sequence @($target) `
                -Commands @($command))
            return
        }

        $viewArgv = @('acli', 'jira', 'workitem', 'view', $key, '--fields', 'status', '--json')
        $viewRun = Invoke-GimFleetExec -Argv $viewArgv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 120
        if ($viewRun.timed_out -or $viewRun.exit_code -ne 0) {
            $viewError = ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text ([string]$viewRun.stdout + "`n" + [string]$viewRun.stderr))
            Write-GimJiraTransitionFailure -Message ('acli jira workitem view failed: ' + $viewError + '; required input: authenticated acli and an existing work item') -JsonMode $jsonMode -DryRun $dryRun -Key $key -Project $project -Target $target
            return
        }
        $view = ConvertFrom-GimJiraTransitionViewJson -Text ([string]$viewRun.stdout)
        if (-not $view.ok) {
            Write-GimJiraTransitionFailure -Message ([string]$view.error) -JsonMode $jsonMode -DryRun $dryRun -Key $key -Project $project -Target $target
            return
        }
        $current = [string]$view.current
        if ((ConvertTo-GimJiraTransitionStatusKey -Value $current) -eq (ConvertTo-GimJiraTransitionStatusKey -Value $target)) {
            Write-GimJiraTransitionResult -JsonMode $jsonMode -Result (New-GimJiraTransitionResultRecord -Ok $true -DryRun $false `
                -Key $key -Project $project -Current $current -Target $target)
            return
        }
        $route = Get-GimJiraTransitionRoute -Config $config -From $current -To $target
        if ($null -eq $route) {
            Write-GimJiraTransitionFailure -Message ("no configured route from '{0}' to '{1}'; required input: a route in jira-transitions.json" -f $current, $target) -JsonMode $jsonMode -DryRun $dryRun -Key $key -Project $project -Current $current -Target $target
            return
        }
        $sequence = @($route.sequence)
        $commands = New-Object System.Collections.Generic.List[string]
        $results = New-Object System.Collections.Generic.List[object]
        foreach ($step in $sequence) {
            $command = Format-GimJiraTransitionCommand -Key $key -Status $step
            [void]$commands.Add($command)
            $argv = @('acli', 'jira', 'workitem', 'transition', '--key', $key, '--status', $step, '--yes', '--json')
            $run = Invoke-GimFleetExec -Argv $argv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 180
            $ok = (-not $run.timed_out -and $run.exit_code -eq 0)
            $errorText = ''
            if (-not $ok) {
                if ($run.timed_out) {
                    $errorText = 'command timed out'
                } else {
                    $errorText = ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text ([string]$run.stdout + "`n" + [string]$run.stderr))
                    if ($errorText -eq '') { $errorText = ('command exited with code {0}' -f $run.exit_code) }
                }
            }
            [void]$results.Add([pscustomobject]@{
                status = $step
                ok = $ok
                exit_code = [int]$run.exit_code
                command = $command
                error = $errorText
            })
            if (-not $ok) {
                $message = ("transition failed at '{0}': {1} -> {2}" -f $step, $command, $errorText)
                Write-GimJiraTransitionFailure -Message $message -JsonMode $jsonMode -DryRun $dryRun -Key $key -Project $project `
                    -Current $current -Target $target -Sequence $sequence -Commands @($commands.ToArray()) -Results @($results.ToArray())
                return
            }
        }
        Write-GimJiraTransitionResult -JsonMode $jsonMode -Result (New-GimJiraTransitionResultRecord -Ok $true -DryRun $false `
            -Key $key -Project $project -Current $current -Target $target -Sequence $sequence `
            -Commands @($commands.ToArray()) -Results @($results.ToArray()))
    }
}
