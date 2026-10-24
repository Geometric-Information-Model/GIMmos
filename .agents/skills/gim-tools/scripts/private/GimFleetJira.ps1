# GimFleetJira.ps1 - read-only Jira helpers for gim-jira-search (ADR 0008).
#
# No shell interpolation: acli is launched through Invoke-GimFleetExec with an
# argv array. The parser returns only key, summary, status, and the assignee
# display name. Email addresses, account ids, and avatar URLs are never
# forwarded. The D0 config is authoritative for the board id and sprint
# capability; there is no hardcoded board fallback.

function Get-GimFleetJiraConfigPath {
    <#
    .SYNOPSIS
        Path to the D0-generated Jira config.
    #>
    return (Join-Path $script:GimModuleRoot 'fleet\jira-config.json')
}

function Get-GimFleetJiraCreateTemplatePath {
    <#
    .SYNOPSIS
        Path to the observed acli create payload template.
    #>
    return (Join-Path $script:GimModuleRoot 'fleet\jira-create.template.json')
}

function Get-GimFleetJiraProperty {
    <#
    .SYNOPSIS
        Read a non-empty property from a PSCustomObject or IDictionary.
    #>
    param(
        [AllowNull()]
        $Object,
        [string[]]$Names
    )
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) {
        foreach ($name in $Names) {
            if ($Object.Contains($name) -and $null -ne $Object[$name] -and "$($Object[$name])" -ne '') {
                return $Object[$name]
            }
        }
        return $null
    }
    foreach ($name in $Names) {
        $prop = $Object.PSObject.Properties[$name]
        if ($null -ne $prop -and $null -ne $prop.Value -and "$($prop.Value)" -ne '') {
            return $prop.Value
        }
    }
    return $null
}

function Get-GimFleetJiraItemsArray {
    <#
    .SYNOPSIS
        Normalize a JSON document or one of its known array properties.
    .DESCRIPTION
        Returns an empty array for `{}` or `{total:0}` so callers report
        "found 0" rather than treating the wrapper as one item.
    #>
    param(
        [AllowNull()]
        $Document,
        [string[]]$Names
    )
    if ($null -eq $Document) { return @() }
    if ($Document -is [System.Array]) { return @($Document) }
    foreach ($name in $Names) {
        $prop = $Document.PSObject.Properties[$name]
        if ($null -ne $prop) {
            if ($null -eq $prop.Value) { return @() }
            if ($name -eq 'data' -and -not ($prop.Value -is [System.Array])) {
                return @(Get-GimFleetJiraItemsArray -Document $prop.Value -Names $Names)
            }
            return @($prop.Value)
        }
    }
    foreach ($marker in @('key', 'issueKey', 'id')) {
        if ($null -ne $Document.PSObject.Properties[$marker]) { return @($Document) }
    }
    return @()
}

function Get-GimFleetJiraSettings {
    <#
    .SYNOPSIS
        Read and validate the D0 Jira config.
    .DESCRIPTION
        Returns { ok, path, site, board_id, sprint_field_id,
        sprint_supported, current_sprint_resolvable, sprint_create_supported,
        error }. Missing or unreadable config is ok=false naming the file and
        the required input. The sprint capability flags and field id are
        honored by the caller; no board id or field id is assumed.
    #>
    param(
        [AllowEmptyString()]
        [string]$Path = ''
    )
    if ($Path -eq '') { $Path = Get-GimFleetJiraConfigPath }
    $failure = {
        param($Message)
        [pscustomobject]@{
            ok = $false
            path = $Path
            site = ''
            board_id = $null
            sprint_field_id = ''
            sprint_supported = $null
            current_sprint_resolvable = $null
            sprint_create_supported = $null
            error = $Message
        }
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return (& $failure ("Jira config not found: '{0}'; required input: run the D0 probe to create it" -f $Path))
    }
    try {
        $doc = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    } catch {
        return (& $failure ("Jira config is unparseable: '{0}'" -f $Path))
    }
    if ($null -eq $doc -or $null -eq $doc.board) {
        return (& $failure ("Jira config has no board object: '{0}'" -f $Path))
    }
    $boardId = [string](Get-GimFleetJiraProperty -Object $doc.board -Names @('id'))
    if ($boardId.Trim() -notmatch '^\d+$') {
        return (& $failure ("Jira config has no numeric board.id: '{0}'" -f $Path))
    }
    if ($null -eq $doc.sprint) {
        return (& $failure ("Jira config has no sprint object: '{0}'" -f $Path))
    }
    $supportedRaw = Get-GimFleetJiraProperty -Object $doc.sprint -Names @('supported')
    if ($null -eq $supportedRaw) {
        return (& $failure ("Jira config has no sprint.supported flag: '{0}'" -f $Path))
    }
    $currentSprint = Get-GimFleetJiraProperty -Object $doc.sprint -Names @('current_sprint')
    if ($null -eq $currentSprint) {
        return (& $failure ("Jira config has no sprint.current_sprint object: '{0}'" -f $Path))
    }
    $resolvableRaw = Get-GimFleetJiraProperty -Object $currentSprint -Names @('resolvable')
    if ($null -eq $resolvableRaw) {
        return (& $failure ("Jira config has no sprint.current_sprint.resolvable flag: '{0}'" -f $Path))
    }
    $createSupported = $false
    $setOnCreate = Get-GimFleetJiraProperty -Object $doc.sprint -Names @('set_on_create')
    if ($null -ne $setOnCreate) {
        $createSupportedRaw = Get-GimFleetJiraProperty -Object $setOnCreate -Names @('supported')
        if ($null -ne $createSupportedRaw) { $createSupported = [bool]$createSupportedRaw }
    }
    $site = [string](Get-GimFleetJiraProperty -Object $doc -Names @('site'))
    $fieldId = [string](Get-GimFleetJiraProperty -Object $doc.sprint -Names @('field_id'))
    return [pscustomobject]@{
        ok = $true
        path = $Path
        site = $site
        board_id = [int]$boardId
        sprint_field_id = $fieldId
        sprint_supported = [bool]$supportedRaw
        current_sprint_resolvable = [bool]$resolvableRaw
        sprint_create_supported = $createSupported
        error = $null
    }
}

function ConvertTo-GimFleetJiraAdf {
    <#
    .SYNOPSIS
        Convert markdown description text to the observed ADF document shape.
    .DESCRIPTION
        The installed acli accepts a doc/version=1 document with paragraph
        nodes. Blank-line-separated blocks become plain-text paragraphs;
        markdown syntax (`##`, `-`, `**`, links) is not interpreted and stays
        literal text. This is intentionally minimal, not a markdown renderer.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Markdown = ''
    )
    $normalized = (([string]$Markdown) -replace "`r`n?", "`n").Trim()
    $content = New-Object System.Collections.Generic.List[object]
    if ($normalized -eq '') {
        [void]$content.Add([pscustomobject]@{
            type = 'paragraph'
            content = @([pscustomobject]@{ type = 'text'; text = '' })
        })
    } else {
        foreach ($block in [regex]::Split($normalized, '\n\s*\n')) {
            $text = ([string]$block).Trim()
            if ($text -eq '') { continue }
            [void]$content.Add([pscustomobject]@{
                type = 'paragraph'
                content = @([pscustomobject]@{ type = 'text'; text = $text })
            })
        }
    }
    return [pscustomobject]@{
        type = 'doc'
        version = 1
        content = @($content.ToArray())
    }
}

function New-GimFleetJiraCreatePayload {
    <#
    .SYNOPSIS
        Fill the observed acli create payload template.
    .DESCRIPTION
        The payload always carries a null assignee (assignment happens after
        create). Labels default to null; description/parent/additionalAttributes
        are omitted when not supplied. Sprint is the bare integer id under
        additionalAttributes[<sprint.field_id from the D0 config>].
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Template,
        [Parameter(Mandatory = $true)]
        [string]$Project,
        [Parameter(Mandatory = $true)]
        [string]$Summary,
        [Parameter(Mandatory = $true)]
        [string]$Type,
        [AllowEmptyCollection()]
        [string[]]$Labels = @(),
        [AllowNull()]
        [AllowEmptyString()]
        [string]$DescriptionMarkdown = $null,
        [bool]$HasDescription = $false,
        [AllowEmptyString()]
        [string]$Parent = '',
        [AllowNull()]
        [object]$SprintId = $null,
        [AllowEmptyString()]
        [string]$SprintFieldId = '',
        [bool]$HasSprint = $false
    )
    $payload = $Template
    $payload.projectKey = $Project
    $payload.summary = $Summary
    $payload.type = $Type
    $payload.assignee = $null
    if (@($Labels).Count -gt 0) {
        $payload.labels = @($Labels)
    } else {
        $payload.labels = $null
    }
    if ($HasDescription) {
        $payload.description = ConvertTo-GimFleetJiraAdf -Markdown $DescriptionMarkdown
    } else {
        $payload.PSObject.Properties.Remove('description')
    }
    if ($Parent -ne '') {
        $payload.parent = $Parent
    } else {
        $payload.PSObject.Properties.Remove('parent')
    }
    if ($HasSprint) {
        if ($SprintFieldId -eq '') {
            Write-GimError 'sprint field id is missing from the D0 config; required input: a configured sprint.field_id'
        }
        $payload.additionalAttributes | Add-Member -NotePropertyName $SprintFieldId -NotePropertyValue $SprintId -Force
    } else {
        $payload.PSObject.Properties.Remove('additionalAttributes')
    }
    return $payload
}

function ConvertTo-GimFleetJiraJqlString {
    <#
    .SYNOPSIS
        Escape a value for a double-quoted JQL string.
    .DESCRIPTION
        Backslashes are escaped first, then double quotes, so a keyword ending
        in `\` still closes the JQL string correctly.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )
    return ([string]$Value).Replace('\', '\\').Replace('"', '\"')
}

function New-GimFleetJiraKeywordJql {
    <#
    .SYNOPSIS
        Build the keyword JQL: summary or description contains the keyword.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Keyword,
        [Parameter(Mandatory = $true)]
        [string]$Project
    )
    $projectKey = ([string]$Project).Trim().ToUpperInvariant()
    if ($projectKey -notmatch '^[A-Z][A-Z0-9_]*$') {
        Write-GimError ("--project '{0}' is not a Jira project key; required input: letters, digits, underscore" -f $Project)
    }
    $escaped = ConvertTo-GimFleetJiraJqlString -Value $Keyword
    return ('project = {0} AND (summary ~ "{1}" OR description ~ "{1}")' -f $projectKey, $escaped)
}

function Add-GimFleetJiraSprintFilter {
    <#
    .SYNOPSIS
        AND an explicit sprint id onto a JQL query.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Jql,
        [Parameter(Mandatory = $true)]
        [int]$SprintId
    )
    return ('({0}) AND sprint = {1}' -f $Jql, $SprintId)
}

function Resolve-GimFleetJiraCurrentSprint {
    <#
    .SYNOPSIS
        Resolve the single active sprint from the D0 board.
    .DESCRIPTION
        Runs `acli jira board list-sprints --id <board> --state active --json`.
        Exactly one sprint whose state is `active` is required. Zero or many is
        ok=false with a required-input message; there is no implicit pick.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [int]$BoardId
    )
    $argv = @('acli', 'jira', 'board', 'list-sprints', '--id', [string]$BoardId, '--state', 'active', '--json')
    $run = Invoke-GimFleetExec -Argv $argv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 120
    if ($run.timed_out -or $run.exit_code -ne 0) {
        return [pscustomobject]@{
            ok = $false
            id = $null
            reason = 'command_failed'
            count = $null
            command = $argv
            exit_code = [int]$run.exit_code
            error = ('could not resolve the active sprint: ' + (Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$run.stdout + "`n" + [string]$run.stderr))))
        }
    }
    try {
        $doc = [string]$run.stdout | ConvertFrom-Json
    } catch {
        return [pscustomobject]@{
            ok = $false
            id = $null
            reason = 'unparseable'
            count = $null
            command = $argv
            exit_code = 1
            error = 'acli board list-sprints returned unparseable JSON'
        }
    }
    $sprints = @(Get-GimFleetJiraItemsArray -Document $doc -Names @('sprints', 'values', 'items'))
    $active = New-Object System.Collections.Generic.List[object]
    foreach ($sprint in $sprints) {
        $state = [string](Get-GimFleetJiraProperty -Object $sprint -Names @('state'))
        if ($state -eq 'active') { [void]$active.Add($sprint) }
    }
    if ($active.Count -ne 1) {
        return [pscustomobject]@{
            ok = $false
            id = $null
            reason = $(if ($active.Count -eq 0) { 'none' } else { 'multiple' })
            count = $active.Count
            command = $argv
            exit_code = 1
            error = ('expected exactly one active sprint on board {0}, found {1}' -f $BoardId, $active.Count)
        }
    }
    $id = Get-GimFleetJiraProperty -Object $active[0] -Names @('id', 'sprintId')
    $idText = ([string]$id).Trim()
    if ($idText -notmatch '^\d+$') {
        return [pscustomobject]@{
            ok = $false
            id = $null
            reason = 'no_id'
            count = 1
            command = $argv
            exit_code = 1
            error = 'the active sprint has no numeric id'
        }
    }
    return [pscustomobject]@{ ok = $true; id = [int]$idText; reason = $null; count = 1; command = $argv; exit_code = 0; error = $null }
}

function Format-GimFleetJiraSprintFailure {
    <#
    .SYNOPSIS
        Add the verb-specific required input to a sprint-resolution failure.
    .DESCRIPTION
        The resolver returns a structured reason; search needs a --jql sprint
        filter, create needs --sprint <id>. Command/parse failures keep their
        exact diagnostic.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Failure,
        [Parameter(Mandatory = $true)]
        [ValidateSet('search', 'create')]
        [string]$Verb
    )
    if ($Failure.reason -in @('none', 'multiple', 'no_id')) {
        if ($Verb -eq 'search') {
            return ([string]$Failure.error + '; required input: pass --jql with an explicit sprint filter (sprint = <id>)')
        }
        return ([string]$Failure.error + '; required input: pass --sprint <id>')
    }
    return [string]$Failure.error
}

function Get-GimFleetJiraStatusName {
    <#
    .SYNOPSIS
        Extract a status name from a flat or fields-nested Jira object.
    #>
    param(
        [AllowNull()]
        $Object
    )
    if ($null -eq $Object) { return '' }
    $statusRaw = Get-GimFleetJiraProperty -Object $Object -Names @('status')
    if ($null -eq $statusRaw) {
        $fields = Get-GimFleetJiraProperty -Object $Object -Names @('fields')
        if ($null -ne $fields) {
            $statusRaw = Get-GimFleetJiraProperty -Object $fields -Names @('status')
        }
    }
    if ($statusRaw -is [string]) {
        return ([string]$statusRaw).Trim()
    }
    if ($null -ne $statusRaw) {
        return ([string](Get-GimFleetJiraProperty -Object $statusRaw -Names @('name'))).Trim()
    }
    return ''
}

function ConvertTo-GimFleetJiraSearchItem {
    <#
    .SYNOPSIS
        Normalize one acli search issue.
    .DESCRIPTION
        Accepts flat and `fields`-nested shapes. Returns only key, summary,
        status, and assignee display name. Emails, account ids (including the
        Jira Cloud colon form), and avatar URLs are deliberately discarded.
    #>
    param(
        [AllowNull()]
        $Object
    )
    if ($null -eq $Object) { return $null }
    $fields = Get-GimFleetJiraProperty -Object $Object -Names @('fields')
    $key = [string](Get-GimFleetJiraProperty -Object $Object -Names @('key', 'issueKey'))
    if ($key.Trim() -eq '') { return $null }

    $summaryRaw = Get-GimFleetJiraProperty -Object $Object -Names @('summary')
    if ($null -eq $summaryRaw -and $null -ne $fields) {
        $summaryRaw = Get-GimFleetJiraProperty -Object $fields -Names @('summary')
    }
    $summary = ''
    if ($null -ne $summaryRaw) { $summary = [string]$summaryRaw }

    $status = Get-GimFleetJiraStatusName -Object $Object

    $assigneeRaw = Get-GimFleetJiraProperty -Object $Object -Names @('assignee')
    if ($null -eq $assigneeRaw -and $null -ne $fields) {
        $assigneeRaw = Get-GimFleetJiraProperty -Object $fields -Names @('assignee')
    }
    $assignee = $null
    if ($assigneeRaw -is [string]) {
        $assignee = [string]$assigneeRaw
    } elseif ($null -ne $assigneeRaw) {
        $assignee = [string](Get-GimFleetJiraProperty -Object $assigneeRaw -Names @('displayName'))
    }
    if ($null -ne $assignee) {
        $assignee = $assignee.Trim()
        # Display name only. Drop an email or an account id if acli returns one
        # in the string form; Jira Cloud colon-form ids are included.
        if ($assignee -eq '' -or $assignee -match '@' -or
            $assignee -match '^[0-9a-fA-F]{24}$' -or
            $assignee -match '^[0-9a-fA-F]{6,24}:[0-9a-fA-F-]+$') {
            $assignee = $null
        }
    }

    return [pscustomobject]@{
        key = $key.Trim()
        summary = $summary
        status = $status
        assignee = $assignee
    }
}

function ConvertFrom-GimFleetJiraSearchJson {
    <#
    .SYNOPSIS
        Parse acli workitem search JSON into normalized items.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text -or $Text.Trim() -eq '') {
        return [pscustomobject]@{ ok = $false; items = @(); error = 'acli returned empty JSON' }
    }
    if ($Text -match '# gim: output truncated') {
        return [pscustomobject]@{ ok = $false; items = @(); error = 'acli search output was truncated' }
    }
    try {
        $doc = $Text | ConvertFrom-Json
    } catch {
        return [pscustomobject]@{ ok = $false; items = @(); error = 'acli search returned unparseable JSON' }
    }
    $raw = @(Get-GimFleetJiraItemsArray -Document $doc -Names @('issues', 'results', 'items', 'data'))
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($entry in $raw) {
        $item = ConvertTo-GimFleetJiraSearchItem -Object $entry
        if ($null -ne $item) { [void]$items.Add($item) }
    }
    return [pscustomobject]@{ ok = $true; items = @($items.ToArray()); error = $null }
}
