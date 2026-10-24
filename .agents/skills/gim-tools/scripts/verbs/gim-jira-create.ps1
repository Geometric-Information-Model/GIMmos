# gim-jira-create.ps1 - fleet tier (ADR 0008): create, assign, verify.
#
# Contract:
#   gim-jira-create --summary <s> [--project <key>] [--type <type>]
#                   [--assignee <@me|email>] [--unassigned]
#                   [--description-file <f>] [--sprint <id>] [--current-sprint]
#                   [--parent <key>] [--label <l>...] [--dry-run] [--json]
#
# The payload comes from the observed acli template. The payload assignee is
# always null; the verb assigns '@me' (or the explicit login) after create
# unless --unassigned, then verifies key/assignee. Sprint is a bare integer
# under additionalAttributes[<sprint.field_id>] from the D0 config. Output is
# key + URL; never email or account id. Dry-run prints the payload and the
# exact create/assign/view commands without calling acli.

function Get-GimJiraCreateCommandError {
    <#
    .SYNOPSIS
        One redacted, bounded line for a failed acli call.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Run
    )
    if ($Run.timed_out) { return 'command timed out' }
    $text = [string]$Run.stderr
    if ($text.Trim() -eq '') { $text = [string]$Run.stdout }
    $line = ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text $text)
    if ($line -eq '') { $line = ('command exited with code {0}' -f $Run.exit_code) }
    return $line
}

function New-GimJiraCreateResultRecord {
    <#
    .SYNOPSIS
        One stable result shape for success, failure, and dry-run.
    #>
    param(
        [bool]$Ok = $false,
        [bool]$DryRun = $false,
        [AllowEmptyString()][string]$Project = '',
        [AllowEmptyString()][string]$Type = '',
        [AllowEmptyString()][string]$Summary = '',
        [AllowNull()][object]$SprintId = $null,
        [AllowNull()][object]$Assigned = $null,
        [AllowEmptyString()][string]$Key = '',
        [AllowEmptyString()][string]$Url = '',
        [AllowNull()]$Payload = $null,
        [AllowEmptyCollection()][string[]]$Commands = @(),
        [AllowEmptyCollection()][string[]]$Warnings = @(),
        [AllowEmptyString()][string]$ErrorMessage = ''
    )
    return [pscustomobject]@{
        ok = $Ok
        dry_run = $DryRun
        key = $Key
        url = $Url
        project = $Project
        type = $Type
        summary = $Summary
        sprint_id = $SprintId
        assigned = $Assigned
        payload = $Payload
        commands = @($Commands)
        warnings = @($Warnings)
        error = $ErrorMessage
    }
}

function Write-GimJiraCreateFailure {
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
        [AllowEmptyString()][string]$Url = '',
        [AllowEmptyString()][string]$Project = '',
        [AllowEmptyString()][string]$Type = '',
        [AllowEmptyString()][string]$Summary = '',
        [AllowNull()][object]$SprintId = $null,
        [AllowNull()][object]$Assigned = $null,
        [AllowEmptyCollection()][string[]]$Warnings = @()
    )
    if ($JsonMode) {
        Write-GimJson (New-GimJiraCreateResultRecord -Ok $false -DryRun $DryRun -Project $Project -Type $Type `
            -Summary $Summary -SprintId $SprintId -Assigned $Assigned -Key $Key -Url $Url `
            -Warnings $Warnings -ErrorMessage $Message)
        $global:LASTEXITCODE = 1
        exit 1
    }
    Write-GimError $Message
}

function Write-GimJiraCreateResult {
    <#
    .SYNOPSIS
        Emit the create result as text or JSON.
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
        Write-Output 'payload:'
        Write-Output (ConvertTo-Json -InputObject $Result.payload -Depth 20)
        foreach ($command in @($Result.commands)) { Write-Output ('command: ' + [string]$command) }
    } else {
        Write-Output ('created {0} {1}' -f [string]$Result.key, [string]$Result.url)
    }
    foreach ($warning in @($Result.warnings)) { Write-Output ('warning: ' + [string]$warning) }
}

function gim-jira-create {
    <#
    .SYNOPSIS
        Create a Jira work item, assign it, and verify it.
    .DESCRIPTION
        Signature:
          gim-jira-create --summary <s> [--project <key>] [--type <type>]
                          [--assignee <@me|email>] [--unassigned]
                          [--description-file <f>] [--sprint <id>] [--current-sprint]
                          [--parent <key>] [--label <l>...] [--dry-run] [--json]

        Default project GIM, type Task, assignee @me. The payload is the
        observed acli template with a null assignee; after create the verb
        assigns @me (or the explicit email) and verifies key/assignee unless
        --unassigned. Labels are null unless --label is given; a create
        rejection mentioning labels is retried once with labels null.
        --description-file text becomes plain ADF paragraphs split on blank
        lines; markdown syntax is not interpreted.
        Sprint is the D0 config's bare integer under sprint.field_id
        (customfield_10020 in the checked-in config); --current-sprint resolves
        the single active sprint first. Zero/many active sprints fails
        and requires an explicit --sprint <id>. --dry-run prints the payload
        and exact commands without calling acli.
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION).
    .EXAMPLE
        gim-jira-create --summary 'Fix auth' --description-file .agent-scratch/ticket.md --current-sprint --dry-run --json
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
                '--summary'          = 'Summary'
                '--project'          = 'Project'
                '--type'             = 'Type'
                '--assignee'         = 'Assignee'
                '--description-file' = 'DescriptionFile'
                '--sprint'           = 'Sprint'
                '--parent'           = 'Parent'
                '--label'            = 'Label'
            } `
            -SwitchFlags @{
                '--unassigned'    = 'Unassigned'
                '--current-sprint' = 'CurrentSprint'
                '--dry-run'       = 'DryRun'
                '--json'          = 'Json'
            }
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
        if ($parsed.Positional.Count -gt 0) {
            Write-GimJiraCreateFailure -Message ("unexpected arguments: '{0}' (gim-jira-create takes --summary and optional create flags)" -f ($parsed.Positional -join ' ')) -JsonMode $jsonMode -DryRun $dryRun
        }
        if (-not $parsed.ContainsKey('Summary')) { Write-GimJiraCreateFailure -Message 'missing --summary: give the work-item title' -JsonMode $jsonMode -DryRun $dryRun }
        $summary = ([string]$parsed['Summary']).Trim()
        if ($summary -eq '') { Write-GimJiraCreateFailure -Message '--summary must not be empty' -JsonMode $jsonMode -DryRun $dryRun }

        $project = 'GIM'
        if ($parsed.ContainsKey('Project')) {
            $project = ([string]$parsed['Project']).Trim()
            if ($project -eq '') { Write-GimJiraCreateFailure -Message '--project must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
            if ($project -notmatch '^[A-Za-z][A-Za-z0-9_]*$') {
                Write-GimJiraCreateFailure -Message ("--project '{0}' is not a Jira project key; required input: letters, digits, underscore" -f $project) -JsonMode $jsonMode -DryRun $dryRun
            }
        }
        $project = $project.ToUpperInvariant()

        $type = 'Task'
        if ($parsed.ContainsKey('Type')) {
            $type = ([string]$parsed['Type']).Trim()
            if ($type -eq '') { Write-GimJiraCreateFailure -Message '--type must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
        }

        $unassigned = $parsed.ContainsKey('Unassigned')
        $hasAssignee = $parsed.ContainsKey('Assignee')
        if ($unassigned -and $hasAssignee) {
            Write-GimJiraCreateFailure -Message 'conflicting assignee flags: use --assignee or --unassigned, never both' -JsonMode $jsonMode -DryRun $dryRun
        }
        $assignee = '@me'
        if ($hasAssignee) {
            $assignee = ([string]$parsed['Assignee']).Trim()
            if ($assignee -eq '') { Write-GimJiraCreateFailure -Message '--assignee must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
            if ($assignee -ne '@me' -and $assignee -notmatch '^\S+@\S+$') {
                Write-GimJiraCreateFailure -Message "--assignee must be '@me' or an email address" -JsonMode $jsonMode -DryRun $dryRun
            }
        }

        $descriptionFile = ''
        $hasDescription = $parsed.ContainsKey('DescriptionFile')
        if ($hasDescription) {
            $descriptionFile = ([string]$parsed['DescriptionFile']).Trim()
            if ($descriptionFile -eq '') { Write-GimJiraCreateFailure -Message '--description-file must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
        }

        $sprintArg = $null
        $hasSprint = $parsed.ContainsKey('Sprint')
        if ($hasSprint) {
            $sprintText = ([string]$parsed['Sprint']).Trim()
            $sprintValue = 0
            if (-not [int]::TryParse($sprintText, [ref]$sprintValue) -or $sprintValue -lt 1) {
                Write-GimJiraCreateFailure -Message ('--sprint must be a positive integer; got ''{0}''' -f $sprintText) -JsonMode $jsonMode -DryRun $dryRun
            }
            $sprintArg = $sprintValue
        }
        $currentSprint = $parsed.ContainsKey('CurrentSprint')
        if ($hasSprint -and $currentSprint) {
            Write-GimJiraCreateFailure -Message 'conflicting sprint flags: use --sprint or --current-sprint, never both' -JsonMode $jsonMode -DryRun $dryRun
        }

        $parent = ''
        if ($parsed.ContainsKey('Parent')) {
            $parent = ([string]$parsed['Parent']).Trim()
            if ($parent -eq '') { Write-GimJiraCreateFailure -Message '--parent must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
            if ($parent -notmatch '^[A-Za-z][A-Za-z0-9_]*-\d+$') {
                Write-GimJiraCreateFailure -Message ("--parent '{0}' must be a work-item key such as GIM-123" -f $parent) -JsonMode $jsonMode -DryRun $dryRun
            }
        }

        $labels = New-Object System.Collections.Generic.List[string]
        if ($parsed.ContainsKey('Label')) {
            foreach ($label in @($parsed['Label'])) {
                $text = ([string]$label).Trim()
                if ($text -eq '') { Write-GimJiraCreateFailure -Message '--label must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
                [void]$labels.Add($text)
            }
        }
    }
    end {
        $settings = Get-GimFleetJiraSettings
        if (-not $settings.ok) {
            Write-GimJiraCreateFailure -Message ([string]$settings.error) -JsonMode $jsonMode -DryRun $dryRun
            return
        }
        if (($hasSprint -or $currentSprint) -and -not $settings.sprint_supported) {
            Write-GimJiraCreateFailure -Message 'Jira sprint is not supported in the D0 config; required input: omit --sprint/--current-sprint' -JsonMode $jsonMode -DryRun $dryRun
            return
        }
        if (($hasSprint -or $currentSprint) -and -not $settings.sprint_create_supported) {
            Write-GimJiraCreateFailure -Message 'sprint assignment at create time is not supported in the D0 config; required input: omit --sprint/--current-sprint' -JsonMode $jsonMode -DryRun $dryRun
            return
        }
        if ($currentSprint -and -not $settings.current_sprint_resolvable) {
            Write-GimJiraCreateFailure -Message 'current-sprint resolution is not supported in the D0 config; required input: pass --sprint <id>' -JsonMode $jsonMode -DryRun $dryRun
            return
        }
        if (($hasSprint -or $currentSprint) -and $settings.sprint_field_id -eq '') {
            Write-GimJiraCreateFailure -Message 'sprint field id is missing from the D0 config; required input: a configured sprint.field_id' -JsonMode $jsonMode -DryRun $dryRun
            return
        }

        $sprintId = $null
        $warnings = New-Object System.Collections.Generic.List[string]
        if ($hasSprint) {
            $sprintId = $sprintArg
        } elseif ($currentSprint) {
            if ($dryRun) {
                $sprintId = '<current-sprint-id>'
                [void]$warnings.Add('dry run: --current-sprint is unresolved; the live run resolves it with acli board list-sprints')
            } else {
                $sprint = Resolve-GimFleetJiraCurrentSprint -BoardId $settings.board_id
                if (-not $sprint.ok) {
                    Write-GimJiraCreateFailure -Message (Format-GimFleetJiraSprintFailure -Failure $sprint -Verb 'create') -JsonMode $jsonMode -DryRun $dryRun
                    return
                }
                $sprintId = [int]$sprint.id
            }
        }

        $templatePath = Get-GimFleetJiraCreateTemplatePath
        if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
            Write-GimJiraCreateFailure -Message ("create payload template not found: '{0}'; required input: restore the D2 template" -f $templatePath) -JsonMode $jsonMode -DryRun $dryRun
            return
        }
        try {
            $template = [System.IO.File]::ReadAllText($templatePath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
        } catch {
            Write-GimJiraCreateFailure -Message ("create payload template is unparseable: '{0}'" -f $templatePath) -JsonMode $jsonMode -DryRun $dryRun
            return
        }

        $descriptionText = $null
        if ($hasDescription) {
            try {
                $descriptionPath = $descriptionFile
                if (-not [System.IO.Path]::IsPathRooted($descriptionPath)) {
                    $descriptionPath = Join-Path (Get-Location).Path $descriptionPath
                }
                $descriptionPath = [System.IO.Path]::GetFullPath($descriptionPath)
                if (-not (Test-Path -LiteralPath $descriptionPath -PathType Leaf)) {
                    Write-GimJiraCreateFailure -Message ("description file not found: '{0}'" -f $descriptionPath) -JsonMode $jsonMode -DryRun $dryRun
                    return
                }
                $descriptionText = [System.IO.File]::ReadAllText($descriptionPath, [System.Text.Encoding]::UTF8)
            } catch {
                Write-GimJiraCreateFailure -Message ('cannot read description file: ' + [string]$_.Exception.Message) -JsonMode $jsonMode -DryRun $dryRun
                return
            }
        }

        $payload = New-GimFleetJiraCreatePayload -Template $template -Project $project -Summary $summary -Type $type `
            -Labels @($labels.ToArray()) -DescriptionMarkdown $descriptionText -HasDescription $hasDescription `
            -Parent $parent -SprintId $sprintId -SprintFieldId $settings.sprint_field_id -HasSprint ($hasSprint -or $currentSprint)
        $payloadJson = ConvertTo-Json -InputObject $payload -Depth 20 -Compress
        $payloadPath = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-create-' + [guid]::NewGuid().ToString('N') + '.json')

        if ($dryRun) {
            $commands = New-Object System.Collections.Generic.List[string]
            [void]$commands.Add('acli jira workitem create --from-json <temp-payload.json> --json')
            if (-not $unassigned) {
                $assignPrinted = $assignee
                if ($assignee -ne '@me') { $assignPrinted = '<redacted-email>' }
                [void]$commands.Add(('acli jira workitem assign --key <new-key> --assignee {0} --yes --json' -f $assignPrinted))
            }
            [void]$commands.Add('acli jira workitem view <new-key> --fields key,summary,status,assignee --json')
            Write-GimJiraCreateResult -JsonMode $jsonMode -Result (New-GimJiraCreateResultRecord -Ok $true -DryRun $true `
                -Project $project -Type $type -Summary $summary -SprintId $sprintId -Assigned (-not $unassigned) `
                -Payload $payload -Commands @($commands.ToArray()) -Warnings @($warnings.ToArray()))
            return
        }

        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($payloadPath, $payloadJson, $utf8)
        try {
        $createArgv = @('acli', 'jira', 'workitem', 'create', '--from-json', $payloadPath, '--json')
        $createRun = Invoke-GimFleetExec -Argv $createArgv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 180
        if ($createRun.timed_out -or $createRun.exit_code -ne 0) {
            $createError = Get-GimJiraCreateCommandError -Run $createRun
            if (@($labels).Count -gt 0 -and $createError -match '(?i)\blabels?\b') {
                $payload.labels = $null
                $payloadJson = ConvertTo-Json -InputObject $payload -Depth 20 -Compress
                [System.IO.File]::WriteAllText($payloadPath, $payloadJson, $utf8)
                [void]$warnings.Add('create rejected the optional labels; retried with labels null')
                $createRun = Invoke-GimFleetExec -Argv $createArgv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 180
            }
            if ($createRun.timed_out -or $createRun.exit_code -ne 0) {
                Write-GimJiraCreateFailure -Message ('acli jira workitem create failed: ' + (Get-GimJiraCreateCommandError -Run $createRun)) -JsonMode $jsonMode -Warnings @($warnings.ToArray())
                return
            }
        }

        $createText = [string]$createRun.stdout
        try {
            $createDoc = $createText | ConvertFrom-Json
        } catch {
            Write-GimJiraCreateFailure -Message 'acli create returned unparseable JSON; required input: a working acli create --json' -JsonMode $jsonMode -Warnings @($warnings.ToArray())
            return
        }
        $key = [string](Get-GimFleetJiraProperty -Object $createDoc -Names @('key', 'issueKey'))
        if ($key.Trim() -eq '') {
            Write-GimJiraCreateFailure -Message 'acli create returned no work-item key; required input: a working acli create --json' -JsonMode $jsonMode -Warnings @($warnings.ToArray())
            return
        }
        $key = $key.Trim()
        $url = [string](Get-GimFleetJiraProperty -Object $createDoc -Names @('url', 'selfUrl'))
        if ($url -eq '' -and $settings.site -ne '') {
            $url = 'https://{0}/browse/{1}' -f $settings.site.TrimEnd('/'), $key
        }

        if (-not $unassigned) {
            $assignArgv = @('acli', 'jira', 'workitem', 'assign', '--key', $key, '--assignee', $assignee, '--yes', '--json')
            $assignRun = Invoke-GimFleetExec -Argv $assignArgv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 120
            if ($assignRun.timed_out -or $assignRun.exit_code -ne 0) {
                Write-GimJiraCreateFailure -Message ('acli jira workitem assign failed: ' + (Get-GimJiraCreateCommandError -Run $assignRun)) -JsonMode $jsonMode -Key $key -Url $url -Warnings @($warnings.ToArray())
                return
            }
        }

        $viewArgv = @('acli', 'jira', 'workitem', 'view', $key, '--fields', 'key,summary,status,assignee', '--json')
        $viewRun = Invoke-GimFleetExec -Argv $viewArgv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 120
        if ($viewRun.timed_out -or $viewRun.exit_code -ne 0) {
            Write-GimJiraCreateFailure -Message ('acli jira workitem view failed: ' + (Get-GimJiraCreateCommandError -Run $viewRun)) -JsonMode $jsonMode -Key $key -Url $url -Warnings @($warnings.ToArray())
            return
        }
        $viewText = [string]$viewRun.stdout
        try {
            $viewDoc = $viewText | ConvertFrom-Json
        } catch {
            Write-GimJiraCreateFailure -Message 'acli view returned unparseable JSON; required input: a working acli view --json' -JsonMode $jsonMode -Key $key -Url $url -Warnings @($warnings.ToArray())
            return
        }
        $viewKey = [string](Get-GimFleetJiraProperty -Object $viewDoc -Names @('key', 'issueKey'))
        if ($viewKey.Trim() -ne '' -and $viewKey.Trim() -ne $key) {
            Write-GimJiraCreateFailure -Message ('created key {0} but view returned {1}' -f $key, $viewKey.Trim()) -JsonMode $jsonMode -Key $key -Url $url -Warnings @($warnings.ToArray())
            return
        }
        if (-not $unassigned) {
            $viewAssignee = Get-GimFleetJiraProperty -Object $viewDoc -Names @('assignee')
            if ($null -eq $viewAssignee) {
                $viewFields = Get-GimFleetJiraProperty -Object $viewDoc -Names @('fields')
                if ($null -ne $viewFields) {
                    $viewAssignee = Get-GimFleetJiraProperty -Object $viewFields -Names @('assignee')
                }
            }
            if ($null -eq $viewAssignee) {
                Write-GimJiraCreateFailure -Message 'assignee verification failed: the created work item has no assignee; required input: assignment access for the requested user' -JsonMode $jsonMode -Key $key -Url $url -Warnings @($warnings.ToArray())
                return
            }
        }

        Write-GimJiraCreateResult -JsonMode $jsonMode -Result (New-GimJiraCreateResultRecord -Ok $true -DryRun $false `
            -Project $project -Type $type -Summary $summary -SprintId $sprintId -Assigned (-not $unassigned) `
            -Key $key -Url $url -Warnings @($warnings.ToArray()))
        } finally {
            if (Test-Path -LiteralPath $payloadPath -PathType Leaf) {
                Remove-Item -LiteralPath $payloadPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
