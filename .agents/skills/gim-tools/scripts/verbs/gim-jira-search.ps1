# gim-jira-search.ps1 - fleet tier (ADR 0008): read-only Jira search.
#
# Contract:
#   gim-jira-search [--keyword <kw>...] [--jql <jql>]
#                  [--project <key>] [--limit <n>] [--current-sprint] [--json]
#
# No search input is required: with neither --keyword nor --jql, query the
# project's issues (default project GIM), optionally narrowed by
# --current-sprint. A keyword expands to summary OR description containment,
# one acli query per keyword, results deduped by key. Output is key, summary,
# status, and assignee display name only: no email, account id, or avatar URL.
# --current-sprint resolves the single active sprint from the D0 config; zero
# or many is an error asking for an explicit sprint filter in --jql (this verb
# has no --sprint flag). Default --limit is 50.

function Write-GimJiraSearchFailure {
    <#
    .SYNOPSIS
        Emit a JSON failure envelope or throw the text error.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [bool]$JsonMode = $false
    )
    if ($JsonMode) {
        Write-GimJson ([pscustomobject]@{
            ok = $false
            count = 0
            truncated = $false
            items = @()
            error = $Message
        })
        $global:LASTEXITCODE = 1
        exit 1
    }
    Write-GimError $Message
}

function Write-GimJiraSearchResult {
    <#
    .SYNOPSIS
        Emit the bounded text result or the shared JSON envelope.
    #>
    param(
        [AllowEmptyCollection()]
        [object[]]$Items = @(),
        [bool]$JsonMode = $false
    )
    $caps = Get-GimCaps
    $all = @($Items)
    $envelope = New-GimJsonEnvelope -Items $all -MaxItems $caps.MaxItems
    if ($JsonMode) {
        Write-GimJson ([pscustomobject]@{
            ok = $true
            count = $envelope.count
            truncated = $envelope.truncated
            items = $envelope.items
            error = $null
        })
        return
    }

    # Cap before formatting/byte accounting so a large scan does no wasted
    # rendering work; Format-GimLines still enforces the byte cap.
    $renderItems = $all
    if ($all.Count -gt $caps.MaxItems) { $renderItems = $all[0..($caps.MaxItems - 1)] }
    $lines = New-Object System.Collections.Generic.List[string]
    $totalBytes = 0L
    foreach ($item in $renderItems) {
        $summary = ConvertTo-GimFleetSingleLine -Text ([string]$item.summary) -MaxLength 300
        $line = ('{0} [{1}] {2}' -f [string]$item.key, [string]$item.status, $summary)
        if ($null -ne $item.assignee -and [string]$item.assignee -ne '') {
            $line += (' (' + [string]$item.assignee + ')')
        }
        [void]$lines.Add($line)
        $totalBytes += [System.Text.Encoding]::UTF8.GetByteCount($line) + 2
    }
    $fmt = Format-GimLines -Items @($lines.ToArray()) -MaxItems $caps.MaxItems -MaxBytes $caps.MaxBytes
    foreach ($line in $fmt.Lines) { Write-Output $line }
    if ($fmt.Truncated -or $all.Count -gt $renderItems.Count) {
        Write-GimTruncationMarker -Items $all.Count -Bytes $totalBytes
    }
}

function gim-jira-search {
    <#
    .SYNOPSIS
        Read-only Jira work-item search with optional keyword/JQL filters.
    .DESCRIPTION
        Signature:
          gim-jira-search [--keyword <kw>...] [--jql <jql>]
                         [--project <key>] [--limit <n>] [--current-sprint] [--json]

        No search input is required. With neither --keyword nor --jql, query
        the project's issues (`project = <key>`), optionally narrowed by
        --current-sprint. Each --keyword builds
        `summary ~ "<kw>" OR description ~ "<kw>"` (backslashes and quotes
        escaped, one query per keyword, deduped by key); --jql is used as
        given. --project defaults to GIM and applies to keyword and no-input
        queries. --limit defaults to 50.

        --current-sprint reads the D0 config, requires sprint support and
        current-sprint resolvability, then resolves the single active sprint
        with `acli jira board list-sprints --id <board> --state active --json`
        and ANDs `sprint = <id>` onto every query. Zero or multiple active
        sprints is ok=false; the required input is an explicit sprint filter in
        --jql (there is no --sprint flag here).

        Output items contain key, summary, status, and assignee display name
        only. Email addresses, account ids, and avatar URLs are never returned.
        Text output is bounded with the shared caps and truncation marker;
        --json emits { ok, count, truncated, items[] }.
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION).
    .EXAMPLE
        gim-jira-search --keyword auth --keyword login --project GIM --limit 20 --json
    .EXAMPLE
        gim-jira-search --jql 'project = GIM AND status = "In Progress"' --json
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
                '--keyword' = 'Keyword'
                '--jql'     = 'Jql'
                '--project' = 'Project'
                '--limit'   = 'Limit'
            } `
            -SwitchFlags @{
                '--current-sprint' = 'CurrentSprint'
                '--json'           = 'Json'
            }
        $jsonMode = $parsed.ContainsKey('Json')
        if ($parsed.Positional.Count -gt 0) {
            Write-GimJiraSearchFailure -Message ("unexpected arguments: '{0}' (gim-jira-search takes optional --keyword/--jql filters plus --project, --limit, --current-sprint, --json)" -f ($parsed.Positional -join ' ')) -JsonMode $jsonMode
        }

        $hasKeywords = $parsed.ContainsKey('Keyword')
        $hasJql = $parsed.ContainsKey('Jql')
        if ($hasKeywords -and $hasJql) {
            Write-GimJiraSearchFailure -Message 'two search sources given: use --keyword or --jql, never both' -JsonMode $jsonMode
        }

        $keywords = New-Object System.Collections.Generic.List[string]
        if ($hasKeywords) {
            foreach ($entry in @($parsed['Keyword'])) {
                $text = ([string]$entry).Trim()
                if ($text -eq '') { Write-GimJiraSearchFailure -Message '--keyword must not be empty' -JsonMode $jsonMode }
                [void]$keywords.Add($text)
            }
        }
        $jql = ''
        if ($hasJql) {
            $jql = ([string]$parsed['Jql']).Trim()
            if ($jql -eq '') { Write-GimJiraSearchFailure -Message '--jql must not be empty' -JsonMode $jsonMode }
        }

        $project = 'GIM'
        if ($parsed.ContainsKey('Project')) {
            $project = ([string]$parsed['Project']).Trim()
            if ($project -eq '') { Write-GimJiraSearchFailure -Message '--project must not be empty' -JsonMode $jsonMode }
            if ($project -notmatch '^[A-Za-z][A-Za-z0-9_]*$') {
                Write-GimJiraSearchFailure -Message ("--project '{0}' is not a Jira project key; required input: letters, digits, underscore" -f $project) -JsonMode $jsonMode
            }
        }
        $project = $project.ToUpperInvariant()

        $limit = 50
        if ($parsed.ContainsKey('Limit')) {
            $limitText = ([string]$parsed['Limit']).Trim()
            $limitValue = 0
            if (-not [int]::TryParse($limitText, [ref]$limitValue) -or $limitValue -lt 1) {
                Write-GimJiraSearchFailure -Message ('--limit must be a positive integer; got ''{0}''' -f $limitText) -JsonMode $jsonMode
            }
            $limit = $limitValue
        }

        $currentSprint = $parsed.ContainsKey('CurrentSprint')
    }
    end {
        $sprintId = $null
        if ($currentSprint) {
            $settings = Get-GimFleetJiraSettings
            if (-not $settings.ok) {
                Write-GimJiraSearchFailure -Message ([string]$settings.error) -JsonMode $jsonMode
                return
            }
            if (-not $settings.sprint_supported) {
                Write-GimJiraSearchFailure -Message 'Jira sprint is not supported in the D0 config; required input: use --jql without --current-sprint' -JsonMode $jsonMode
                return
            }
            if (-not $settings.current_sprint_resolvable) {
                Write-GimJiraSearchFailure -Message 'current-sprint resolution is not supported in the D0 config; required input: pass --jql with an explicit sprint filter (sprint = <id>)' -JsonMode $jsonMode
                return
            }
            $sprint = Resolve-GimFleetJiraCurrentSprint -BoardId $settings.board_id
            if (-not $sprint.ok) {
                Write-GimJiraSearchFailure -Message (Format-GimFleetJiraSprintFailure -Failure $sprint -Verb 'search') -JsonMode $jsonMode
                return
            }
            $sprintId = [int]$sprint.id
        }

        $queries = New-Object System.Collections.Generic.List[string]
        if ($hasKeywords) {
            foreach ($keyword in $keywords) {
                $query = New-GimFleetJiraKeywordJql -Keyword $keyword -Project $project
                if ($null -ne $sprintId) { $query = Add-GimFleetJiraSprintFilter -Jql $query -SprintId $sprintId }
                [void]$queries.Add($query)
            }
        } elseif ($hasJql) {
            $query = $jql
            if ($null -ne $sprintId) { $query = Add-GimFleetJiraSprintFilter -Jql $query -SprintId $sprintId }
            [void]$queries.Add($query)
        } else {
            $query = ('project = {0}' -f $project)
            if ($null -ne $sprintId) { $query += (' AND sprint = {0}' -f $sprintId) }
            [void]$queries.Add($query)
        }

        $items = New-Object System.Collections.Generic.List[object]
        $seen = @{}
        foreach ($query in $queries) {
            $argv = @(
                'acli', 'jira', 'workitem', 'search',
                '--jql', $query,
                '--fields', 'key,summary,status,assignee',
                '--json'
            )
            $argv += @('--limit', [string]$limit)
            $run = Invoke-GimFleetExec -Argv $argv -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 180
            if ($run.timed_out -or $run.exit_code -ne 0) {
                $errorText = ('acli jira workitem search failed: ' + (Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$run.stdout + "`n" + [string]$run.stderr))) + '; required input: authenticated acli and a valid JQL')
                Write-GimJiraSearchFailure -Message $errorText -JsonMode $jsonMode
                return
            }
            # Success path parses stdout only; a stderr warning must not break
            # a valid JSON payload.
            $text = [string]$run.stdout
            if ($text -match '# gim: output truncated') {
                Write-GimJiraSearchFailure -Message 'acli jira workitem search output was truncated; required input: narrow the query or lower --limit' -JsonMode $jsonMode
                return
            }
            $parsedResult = ConvertFrom-GimFleetJiraSearchJson -Text $text
            if (-not $parsedResult.ok) {
                Write-GimJiraSearchFailure -Message ([string]$parsedResult.error) -JsonMode $jsonMode
                return
            }
            foreach ($item in @($parsedResult.items)) {
                $key = ([string]$item.key).ToLowerInvariant()
                if ($seen.ContainsKey($key)) { continue }
                $seen[$key] = $true
                [void]$items.Add($item)
            }
        }

        Write-GimJiraSearchResult -Items @($items.ToArray()) -JsonMode $jsonMode
    }
}
