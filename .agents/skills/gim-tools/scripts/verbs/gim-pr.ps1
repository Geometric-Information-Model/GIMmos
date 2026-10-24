# gim-pr.ps1 - fleet tier (ADR 0008): one explicit PR flow.
#
# Stages an explicit file list, commits it, pushes with -u, creates the PR,
# then runs the stack lifecycle. The branch is born inside the local stack:
#   - first PR (no --new-branch): `gh stack init --base <target> <current>`
#     adopts the current feature branch into a new local stack; no link/sync;
#   - step PR (--new-branch <branch>): `gh stack add <branch>` creates and
#     checks out the next branch inside the local stack (no commit), then
#     `gh stack link` extends the remote stack;
#   - `gh stack view --json` captures the local branch count (stack.url may
#     be unavailable; installed view output has no url field);
#   - `gh stack sync --remote <remote>` verifies the count line and warnings.
# There is no `gh stack checkout`, no baseRef walk, and no full-chain fallback.
#
# Two git config keys are set in the target repo before any stack command:
# rerere.enabled=true (removes init's interactive prompt and helps cascade
# rebases) and push.autoSetupRemote=true (a plain push sets its upstream).
# Both are documented in github-cli-workflow/references/gim-stack-lifecycle.md
# (config keys) and references/harness-config.md. The index is guarded before
# and after staging so a commit can never carry a file outside the declared list.
#
# --dry-run resolves the repo/branch/file list and prints the command plan
# without staging, committing, pushing, or calling gh.

function New-GimPrResultRecord {
    <#
    .SYNOPSIS
        Build the gim-pr result record (also the result.json shape).
    #>
    param(
        [bool]$Ok = $false,
        [bool]$DryRun = $false,
        [AllowEmptyString()][string]$Repo = '',
        [AllowEmptyString()][string]$Root = '',
        [AllowEmptyString()][string]$Branch = '',
        [AllowEmptyString()][string]$Head = '',
        [AllowEmptyString()][string]$NewBranch = '',
        [AllowEmptyString()][string]$Remote = 'origin',
        [AllowEmptyString()][string]$Target = '',
        [AllowEmptyString()][string]$FilesFile = '',
        [AllowEmptyCollection()][string[]]$Files = @(),
        [AllowEmptyString()][string]$Message = '',
        [AllowEmptyString()][string]$Title = '',
        [AllowEmptyString()][string]$PrBody = '',
        [bool]$Draft = $false,
        [AllowEmptyString()][string]$Assignee = '@me'
    )
    return [pscustomobject]@{
        ok = $Ok
        dry_run = $DryRun
        repo = $Repo
        root = $Root
        branch = $Branch
        head = $Head
        new_branch = $NewBranch
        remote = $Remote
        target = $Target
        files_file = $FilesFile
        file_count = @($Files).Count
        files = @($Files)
        plan = @()
        commit = [pscustomobject]@{
            message = $Message
            sha = $null
            subject = $null
        }
        push = [pscustomobject]@{
            ok = $false
            remote = $Remote
            branch = $Head
            error = $null
        }
        pr = [pscustomobject]@{
            number = $null
            url = $null
            title = $Title
            base = $Target
            head = $Head
            draft = $Draft
            assignee = $Assignee
            error = $null
        }
        stack = [pscustomobject]@{
            method = $null
            number = $null
            url = $null
            pending = $false
            verified = $null
            link = $null
            sync = $null
            view_error = $null
            error = $null
        }
        body = [pscustomobject]@{
            path = $PrBody
            deleted = $false
        }
        warnings = @()
        error = $null
    }
}

function Save-GimPrResult {
    <#
    .SYNOPSIS
        Write result.json UTF-8 without BOM, contained in the target repo.
    .DESCRIPTION
        The only write root for result.json is the target repo root
        (result.root), never the module repo or the OS temp root. The PR body
        is separate: it must live in the evidence directory. A caller that
        writes outside the target repo gets a containment error, which
        Complete-GimPrResult turns into a warning so the command result is
        still printed.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )
    if ([string]$Result.root -eq '') { Write-GimError 'internal error: result root is empty' }
    $roots = @((Get-GimRealPath -Path ([string]$Result.root)))
    $real = Resolve-GimWritePath -Path $Path -Roots $roots -RootLabel 'target repo'
    $dir = Split-Path -Parent $real
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $json = ConvertTo-Json -InputObject $Result -Depth 10
    # Persist atomically: write a sibling temp file, then replace the record so
    # a reader (the next step) never observes a half-written result.json.
    $tmp = $real + '.tmp-' + [guid]::NewGuid().ToString('N')
    $encoding = New-Object System.Text.UTF8Encoding($false)
    try {
        [System.IO.File]::WriteAllText($tmp, $json, $encoding)
        if (Test-Path -LiteralPath $real -PathType Leaf) {
            try {
                [System.IO.File]::Replace($tmp, $real, $null)
            } catch {
                # Filesystems without Replace support still get an atomic move.
                Move-Item -LiteralPath $tmp -Destination $real -Force
            }
        } else {
            [System.IO.File]::Move($tmp, $real)
        }
    } catch {
        if (Test-Path -LiteralPath $tmp -PathType Leaf) {
            try { [System.IO.File]::Delete($tmp) } catch { }
        }
        throw
    }
    return $real
}

function Format-GimPrSummary {
    <#
    .SYNOPSIS
        Compact text summary for gim-pr (live and dry-run).
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [AllowEmptyString()][string]$ResultPath = ''
    )
    $lines = New-Object System.Collections.Generic.List[string]
    if ($Result.dry_run) { $lines.Add('mode: dry-run (no config, stack, stage, commit, push, or PR create)') }
    if ($Result.ok) { $lines.Add('gim-pr: ok') } else { $lines.Add('gim-pr: failed') }
    $lines.Add(('repo: {0}' -f [string]$Result.root))
    $lines.Add(('branch: {0} head={1} remote={2} target={3}' -f `
        [string]$Result.branch, [string]$Result.head, [string]$Result.remote, [string]$Result.target))
    if ([string]$Result.files_file -ne '') {
        $lines.Add(('files: {0} ({1})' -f [string]$Result.file_count, [string]$Result.files_file))
    } else {
        $lines.Add(('files: {0}' -f [string]$Result.file_count))
    }
    # Text output is bounded; --json keeps the full list. A long file list
    # must not flood the parent context.
    $fileList = @($Result.files)
    $maxSummaryFiles = 50
    $shownFiles = 0
    foreach ($file in $fileList) {
        if ($shownFiles -ge $maxSummaryFiles) { break }
        $lines.Add('  ' + [string]$file)
        $shownFiles++
    }
    if ($fileList.Count -gt $maxSummaryFiles) {
        $lines.Add(('  ... {0} more file(s)' -f ($fileList.Count - $maxSummaryFiles)))
    }
    if ($Result.dry_run) {
        foreach ($planLine in @($Result.plan)) { $lines.Add('plan: ' + [string]$planLine) }
    } else {
        if ($Result.commit.sha) {
            $lines.Add(('commit: {0} {1}' -f [string]$Result.commit.sha, [string]$Result.commit.subject))
        }
        if ($Result.push.ok) {
            $lines.Add(('push: ok ({0} {1})' -f [string]$Result.push.remote, [string]$Result.push.branch))
        } elseif ($Result.push.error) {
            $lines.Add('push: failed (' + [string]$Result.push.error + ')')
        }
        if ($Result.pr.url) {
            $draft = 'false'
            if ($Result.pr.draft) { $draft = 'true' }
            $lines.Add(('pr: #{0} {1} ({2} <- {3}, draft={4}, assignee={5})' -f `
                [string]$Result.pr.number,
                [string]$Result.pr.url,
                [string]$Result.pr.base,
                [string]$Result.pr.head,
                $draft,
                [string]$Result.pr.assignee))
        } elseif ($Result.pr.error) {
            $lines.Add('pr: failed (' + [string]$Result.pr.error + ')')
        }
        if ($Result.stack.method) {
            $verified = 'null'
            if ($null -ne $Result.stack.verified) { $verified = Format-GimFleetBool -Value ([bool]$Result.stack.verified) }
            $lines.Add(('stack: method={0} number={1} url={2} pending={3} verified={4}' -f `
                [string]$Result.stack.method,
                [string]$Result.stack.number,
                [string]$Result.stack.url,
                (Format-GimFleetBool -Value ([bool]$Result.stack.pending)),
                $verified))
            if ($Result.stack.link) {
                $lines.Add(('stack link: exit={0}' -f [string]$Result.stack.link.exit))
            }
            if ($Result.stack.sync) {
                $lines.Add(('stack sync: exit={0} count={1}' -f `
                    [string]$Result.stack.sync.exit,
                    [string]$Result.stack.sync.count_line))
            }
        }
        if ($Result.body.path) {
            $deleted = 'false'
            if ($Result.body.deleted) { $deleted = 'true' }
            $lines.Add(('body: {0} deleted={1}' -f [string]$Result.body.path, $deleted))
        }
    }
    foreach ($warning in @($Result.warnings)) { $lines.Add('warning: ' + [string]$warning) }
    if ($Result.error) {
        $commandText = @($Result.error.command) -join ' '
        $lines.Add(('error: step={0} exit={1} command={2}' -f `
            [string]$Result.error.step,
            [string]$Result.error.exit_code,
            $commandText))
        $lines.Add('  ' + [string]$Result.error.message)
    }
    if ($ResultPath -ne '') { $lines.Add('result: ' + $ResultPath) }
    return @($lines.ToArray())
}

function New-GimPrDryRunPlan {
    <#
    .SYNOPSIS
        Build the ordered dry-run command plan. Read-only: nothing is run.
    .DESCRIPTION
        Method 'init' is the first PR (no --new-branch); method 'add' is a step
        PR. Both print the pre-index guard, the two config keys, the stack
        setup, stage, post-index guard, commit, push, and PR create. A step PR
        then prints link, view, and sync verification; the first PR prints no
        link and no sync.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Branch,
        [Parameter(Mandatory = $true)]
        [string]$Head,
        [Parameter(Mandatory = $true)]
        [ValidateSet('init', 'add')]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Remote,
        [Parameter(Mandatory = $true)]
        [string]$Target,
        [Parameter(Mandatory = $true)]
        [string[]]$Files,
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [Parameter(Mandatory = $true)]
        [string]$Title,
        [AllowEmptyString()][string]$PrBody = '',
        [bool]$Draft = $false,
        [AllowEmptyString()][string]$Assignee = '@me',
        [bool]$DeletePrBody = $false,
        [AllowNull()][object]$TargetNumber = $null,
        [AllowEmptyString()][string]$TargetBase = ''
    )
    $plan = New-Object System.Collections.Generic.List[string]
    # Real execution order: the pre-staging index guard runs before the stack
    # setup; the post-staging guard runs after git add.
    $plan.Add('git diff --cached --name-only')
    $plan.Add('git config rerere.enabled true')
    $plan.Add('git config push.autoSetupRemote true')
    if ($Method -eq 'init') {
        $plan.Add(('gh stack init --base {0} {1}' -f $Target, $Branch))
    } else {
        $plan.Add(('gh stack add {0}' -f $Head))
    }
    $fileText = New-Object System.Collections.Generic.List[string]
    foreach ($file in @($Files)) { [void]$fileText.Add([string]$file) }
    $plan.Add('git add -- ' + ($fileText.ToArray() -join ' '))
    $plan.Add('git diff --cached --name-only  # guard: only the listed files')
    $plan.Add('git commit --message ' + $Message)
    $plan.Add('git rev-parse HEAD')
    $plan.Add('git log -1 --pretty=%s')
    $plan.Add(('git push -u {0} HEAD' -f $Remote))
    $create = ('gh pr create --base {0} --title {1} --assignee {2}' -f $Target, $Title, $Assignee)
    if ($Draft) { $create += ' --draft' }
    if ($PrBody -ne '') {
        $create += (' --body-file ' + $PrBody)
    } else {
        $create += ' --body ""'
    }
    $plan.Add($create)
    if ($Method -eq 'init') {
        $plan.Add('stack: first PR -> init only; no link, no sync; stack.pending=true, number=null')
    } else {
        if ($null -ne $TargetNumber -and [string]$TargetNumber -ne '') {
            $plan.Add(('gh stack link {0} {1} --remote {2}' -f [string]$TargetNumber, $Head, $Remote))
        } else {
            $plan.Add(('gh stack link --base {0} {1} {2} --remote {3}' -f $TargetBase, $Target, $Head, $Remote))
        }
        $plan.Add('gh stack view --json')
        $plan.Add(('gh stack sync --remote {0}' -f $Remote))
        $plan.Add('stack: step PR -> link extends the stack; sync verifies a stack-count line (K PRs...) (stack #N) with no warning line')
    }
    if ($DeletePrBody -and $PrBody -ne '') { $plan.Add('delete ' + $PrBody + ' after success') }
    return @($plan.ToArray())
}

function Get-GimPrCommandError {
    <#
    .SYNOPSIS
        One redacted, bounded line for a failed git/gh call.
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

function Get-GimPrExceptionMessage {
    <#
    .SYNOPSIS
        Strip the 'gim: ' prefix from a thrown verb error for result.json.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $ErrorRecord
    )
    $message = [string]$ErrorRecord.Exception.Message
    if ($message -match '^gim:\s*') { $message = $message -replace '^gim:\s*', '' }
    return $message
}

function Write-GimPrOutput {
    <#
    .SYNOPSIS
        Emit the result as JSON or as the text summary.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [AllowEmptyString()][string]$ResultPath = '',
        [bool]$JsonMode = $false
    )
    if ($JsonMode) {
        Write-GimJson $Result
        return
    }
    foreach ($line in (Format-GimPrSummary -Result $Result -ResultPath $ResultPath)) {
        Write-Output $line
    }
}

function Complete-GimPrResult {
    <#
    .SYNOPSIS
        Persist result.json, emit it, and exit non-zero when the run failed.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [Parameter(Mandatory = $true)]
        [string]$ResultPath,
        [bool]$JsonMode = $false,
        [AllowEmptyString()][string]$FailedStep = '',
        [AllowEmptyCollection()][string[]]$Command = @(),
        [int]$ExitCode = 0,
        [AllowEmptyString()][string]$ErrorMessage = ''
    )
    if ($FailedStep -ne '') {
        $Result.ok = $false
        $Result.error = [pscustomobject]@{
            step = $FailedStep
            command = @($Command)
            exit_code = $ExitCode
            message = $ErrorMessage
        }
    }
    $written = ''
    try {
        $written = Save-GimPrResult -Result $Result -Path $ResultPath
    } catch {
        # A result-write failure must never hide the command result.
        $Result.warnings = @($Result.warnings) + ('could not write result.json: ' + (Get-GimPrExceptionMessage -ErrorRecord $_))
    }
    Write-GimPrOutput -Result $Result -ResultPath $written -JsonMode $JsonMode
    if (-not $Result.ok) {
        $global:LASTEXITCODE = 1
        exit 1
    }
}

function Get-GimPrViewRecord {
    <#
    .SYNOPSIS
        Parse `gh pr view --json ...` output into the PR confirmation fields.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$JsonText
    )
    if ($null -eq $JsonText -or $JsonText.Trim() -eq '') { return $null }
    try {
        $doc = $JsonText | ConvertFrom-Json
    } catch {
        return $null
    }
    $assignees = New-Object System.Collections.Generic.List[string]
    $assigneeSource = $doc.assignees
    if ($null -eq $assigneeSource) { $assigneeSource = $doc.assignee }
    foreach ($assignee in @($assigneeSource)) {
        if ($null -eq $assignee) { continue }
        if ($assignee -is [string]) {
            [void]$assignees.Add($assignee)
            continue
        }
        $login = $assignee.PSObject.Properties['login']
        if ($null -ne $login -and $login.Value) { [void]$assignees.Add([string]$login.Value) }
    }
    return [pscustomobject]@{
        number = $doc.number
        url = [string]$doc.url
        title = [string]$doc.title
        base = [string]$doc.baseRefName
        head = [string]$doc.headRefName
        draft = [bool]$doc.isDraft
        assignees = @($assignees.ToArray())
    }
}

function Resolve-GimPrAssignee {
    <#
    .SYNOPSIS
        Prefer the assignees returned by `gh pr view`; fall back to the
        requested login when gh returns none.
    #>
    param(
        [AllowEmptyCollection()][string[]]$Returned = @(),
        [AllowEmptyString()][string]$Requested = '@me'
    )
    $list = @($Returned)
    if ($list.Count -gt 0) { return ($list -join ', ') }
    if ($Requested -ne '') { return $Requested }
    return '@me'
}

function Set-GimPrStackOutcome {
    <#
    .SYNOPSIS
        Apply the stack result to the record.
    .DESCRIPTION
        `method` is init (first PR) or add (step PR). A first PR records
        pending=true, number=null, verified=null and never links or syncs. A
        step PR records the link number and verified=true only after link and
        sync verification both pass. Failure sets pending=false, verified=false
        for a step PR, and keeps any parsed link number for diagnosis.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [Parameter(Mandatory = $true)]
        [ValidateSet('init', 'add')]
        [string]$Method,
        [bool]$Succeeded = $false,
        [AllowNull()][object]$Number = $null,
        [AllowNull()][object]$Verified = $null,
        [AllowEmptyString()][string]$ErrorText = ''
    )
    $Result.stack.method = $Method
    if (-not $Succeeded) {
        $Result.ok = $false
        $Result.stack.error = $ErrorText
        $Result.stack.pending = $false
        if ($Method -eq 'add') { $Result.stack.verified = $false }
        if ($null -ne $Number -and [string]$Number -ne '') {
            $Result.stack.number = [int]$Number
        }
        return $Result
    }
    $Result.ok = $true
    $Result.stack.error = $null
    if ($Method -eq 'init') {
        $Result.stack.pending = $true
        $Result.stack.number = $null
        $Result.stack.verified = $null
    } else {
        $Result.stack.pending = $false
        $Result.stack.verified = $Verified
        if ($null -ne $Number -and [string]$Number -ne '') {
            $Result.stack.number = [int]$Number
        }
    }
    return $Result
}

function Invoke-GimPrStackSetup {
    <#
    .SYNOPSIS
        Set the two documented git config keys, then run the stack setup command.
    .DESCRIPTION
        The config keys are set in the target repo's local config before any
        stack command:
          - rerere.enabled=true removes `gh stack init`'s interactive prompt
            and helps cascade rebases;
          - push.autoSetupRemote=true lets a plain push set its upstream.
        First PR (method init): `gh stack init --base <target> <head>` adopts
        the current branch into a new local stack. A missing local base fails
        with gh's exact error. Step PR (method add): `gh stack add <head>`
        creates and checks out the branch inside the local stack; it does not
        commit, and it is invoked with no -A/-u/-m.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Target,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Head,
        [Parameter(Mandatory = $true)]
        [ValidateSet('init', 'add')]
        [string]$Method
    )
    $configKeys = @(
        @{ key = 'rerere.enabled'; value = 'true' },
        @{ key = 'push.autoSetupRemote'; value = 'true' }
    )
    foreach ($entry in $configKeys) {
        $configArgv = @('git', 'config', $entry.key, $entry.value)
        $configRun = Invoke-GimFleetExec -Argv $configArgv -WorkingDirectory $Root -TimeoutSeconds 60
        if ($configRun.timed_out -or $configRun.exit_code -ne 0) {
            return [pscustomobject]@{
                ok = $false
                step = 'config'
                command = $configArgv
                exit_code = [int]$configRun.exit_code
                error = ('could not set git config {0}: {1}' -f $entry.key, (Get-GimPrCommandError -Run $configRun))
            }
        }
    }

    if ($Method -eq 'init') {
        $argv = @('gh', 'stack', 'init', '--base', $Target, $Head)
    } else {
        $argv = @('gh', 'stack', 'add', $Head)
    }
    $run = Invoke-GimFleetExec -Argv $argv -WorkingDirectory $Root -TimeoutSeconds 180
    if ($run.timed_out -or $run.exit_code -ne 0) {
        return [pscustomobject]@{
            ok = $false
            step = 'stack'
            command = $argv
            exit_code = [int]$run.exit_code
            error = (Get-GimPrCommandError -Run $run)
        }
    }
    return [pscustomobject]@{ ok = $true; step = $null; command = $argv; exit_code = 0; error = $null }
}

function Get-GimPrTargetResultPath {
    <#
    .SYNOPSIS
        result.json path for a target step branch's evidence directory.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Branch
    )
    $dir = Get-GimPrResultDirectory -RepoRoot $RepoRoot -Branch $Branch
    return (Join-Path $dir 'result.json')
}

function New-GimPrTargetResultFailure {
    <#
    .SYNOPSIS
        Failure record for Get-GimPrTargetResult.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    return [pscustomobject]@{ ok = $false; path = $Path; number = $null; base = ''; error = $Message }
}

function Get-GimPrTargetResult {
    <#
    .SYNOPSIS
        Read the target step's result.json for the link decision.
    .DESCRIPTION
        Returns { ok, path, number, base, error }. `number` is stack.number when
        present. `base` is pr.base and is required only for the create path
        (target stack.number null). A missing or malformed result is ok=false
        with a clear error; there is no `gh pr view` baseRef walk.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Branch
    )
    $path = Get-GimPrTargetResultPath -RepoRoot $RepoRoot -Branch $Branch
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return (New-GimPrTargetResultFailure -Path $path -Message ("target result.json not found: '{0}'; required input: run gim-pr for the target branch '{1}' first" -f $path, $Branch))
    }
    try {
        $doc = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    } catch {
        return (New-GimPrTargetResultFailure -Path $path -Message ("target result.json is unparseable: '{0}'" -f $path))
    }
    if ($null -eq $doc) {
        return (New-GimPrTargetResultFailure -Path $path -Message ("target result.json is empty: '{0}'" -f $path))
    }
    $stackProp = $doc.PSObject.Properties['stack']
    if ($null -eq $stackProp -or $null -eq $stackProp.Value) {
        return (New-GimPrTargetResultFailure -Path $path -Message ("target result.json has no stack object: '{0}'" -f $path))
    }
    $number = $null
    $numberProp = $stackProp.Value.PSObject.Properties['number']
    if ($null -ne $numberProp -and $null -ne $numberProp.Value) {
        $numberText = ([string]$numberProp.Value).Trim()
        if ($numberText -notmatch '^\d+$') {
            return (New-GimPrTargetResultFailure -Path $path -Message ("target result.json has an invalid stack.number '{0}': '{1}'" -f $numberText, $path))
        }
        $number = [int]$numberText
    }
    $base = ''
    $prProp = $doc.PSObject.Properties['pr']
    if ($null -ne $prProp -and $null -ne $prProp.Value) {
        $baseProp = $prProp.Value.PSObject.Properties['base']
        if ($null -ne $baseProp -and $null -ne $baseProp.Value) {
            $base = ([string]$baseProp.Value).Trim()
        }
    }
    if ($null -eq $number -and $base -eq '') {
        return (New-GimPrTargetResultFailure -Path $path -Message ("target result.json has no pr.base for the create path: '{0}'" -f $path))
    }
    return [pscustomobject]@{ ok = $true; path = $path; number = $number; base = $base; error = $null }
}

function Get-GimPrStackNumberFromText {
    <#
    .SYNOPSIS
        Parse `stack #N` from gh stack link output.
    .DESCRIPTION
        Permissive on purpose: the create-path link output shape is not
        assumed. The last `stack #N` wins so an echoed older stack number does
        not false-fail the sync N comparison. The sync count line is the
        verification safety net.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text) { return $null }
    $numberMatches = [regex]::Matches($Text, 'stack #(\d+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($numberMatches.Count -eq 0) { return $null }
    # Last match wins, matching the sync count-line parser. A link output that
    # echoes an older stack number must not false-fail the N comparison.
    return [int]$numberMatches[$numberMatches.Count - 1].Groups[1].Value
}

function Test-GimPrWarningOutput {
    <#
    .SYNOPSIS
        True when captured gh output contains a warning line (⚠, or the ASCII
        fallback used in some terminals).
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text) { return $false }
    return [bool]([regex]::IsMatch($Text, '(?m)^\s*(?:\u26A0|\?)\s'))
}

function Invoke-GimPrStackLink {
    <#
    .SYNOPSIS
        Extend the remote stack for a step PR.
    .DESCRIPTION
        Reads the target step's result.json. Target stack.number present ->
        `gh stack link <N> <branch> --remote <remote>`; null ->
        `gh stack link --base <pr.base> <target-branch> <branch> --remote
        <remote>`. A non-zero exit, a warning line, or a missing `stack #N` is
        ok=false. Raw output is stored in stack.link.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Target,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Head,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Remote
    )
    $Result.stack.method = 'add'
    $Result.stack.pending = $false
    $Result.stack.verified = $false
    $targetResult = Get-GimPrTargetResult -RepoRoot $Root -Branch $Target
    if (-not $targetResult.ok) {
        $Result.stack.error = [string]$targetResult.error
        return [pscustomobject]@{ ok = $false; number = $null; command = @(); exit_code = 1; error = [string]$targetResult.error }
    }
    if ($null -ne $targetResult.number) {
        $argv = @('gh', 'stack', 'link', [string]$targetResult.number, $Head, '--remote', $Remote)
    } else {
        $argv = @('gh', 'stack', 'link', '--base', [string]$targetResult.base, $Target, $Head, '--remote', $Remote)
    }
    $run = Invoke-GimFleetExec -Argv $argv -WorkingDirectory $Root -TimeoutSeconds 180
    $output = [string]$run.stdout + "`n" + [string]$run.stderr
    $Result.stack.link = [pscustomobject]@{
        exit = [int]$run.exit_code
        output = $output
    }
    if ($run.timed_out -or $run.exit_code -ne 0) {
        $errorText = ('gh stack link failed: ' + (Get-GimPrCommandError -Run $run))
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; number = $null; command = $argv; exit_code = [int]$run.exit_code; error = $errorText }
    }
    if (Test-GimPrWarningOutput -Text $output) {
        $errorText = ('gh stack link reported a warning: ' + (ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text $output)))
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; number = $null; command = $argv; exit_code = 1; error = $errorText }
    }
    $number = Get-GimPrStackNumberFromText -Text $output
    if ($null -eq $number) {
        $errorText = ('gh stack link output contained no stack #N: ' + (ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text $output)))
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; number = $null; command = $argv; exit_code = 1; error = $errorText }
    }
    $Result.stack.number = $number
    $Result.stack.error = $null
    return [pscustomobject]@{ ok = $true; number = $number; command = $argv; exit_code = 0; error = $null }
}

function Set-GimPrStackViewDetails {
    <#
    .SYNOPSIS
        Capture the local branch count before sync (stack.url when available).
    .DESCRIPTION
        Installed `gh stack view --json` has no url field, so stack.url may
        stay null. A failed or unusable view records stack.view_error and emits
        a warning. The returned branch_count is $null when unknown, which
        downgrades the sync K check to number-only.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [Parameter(Mandatory = $true)]
        [string]$Root
    )
    $argv = @('gh', 'stack', 'view', '--json')
    $view = Invoke-GimFleetExec -Argv $argv -WorkingDirectory $Root -TimeoutSeconds 120
    if ($view.timed_out -or $view.exit_code -ne 0) {
        $Result.stack.view_error = Get-GimPrCommandError -Run $view
        $Result.warnings = @($Result.warnings) + ('gh stack view failed: ' + [string]$Result.stack.view_error)
        return [pscustomobject]@{ branch_count = $null }
    }
    $stackInfo = Get-GimPrStackSummary -Text ([string]$view.stdout)
    if ($null -eq $stackInfo) {
        $Result.stack.view_error = 'gh stack view returned unparseable JSON; local chain length unknown'
        $Result.warnings = @($Result.warnings) + [string]$Result.stack.view_error
        return [pscustomobject]@{ branch_count = $null }
    }
    if ($null -ne $stackInfo.url -and [string]$stackInfo.url -ne '') {
        $Result.stack.url = [string]$stackInfo.url
    }
    if ($null -eq $stackInfo.branch_count) {
        $Result.stack.view_error = 'gh stack view returned no branches array; local chain length unknown'
        $Result.warnings = @($Result.warnings) + [string]$Result.stack.view_error
        return [pscustomobject]@{ branch_count = $null }
    }
    return [pscustomobject]@{ branch_count = [int]$stackInfo.branch_count }
}

function Invoke-GimPrStackSync {
    <#
    .SYNOPSIS
        Verify the remote stack after the link.
    .DESCRIPTION
        Requires exit 0, no warning line, and a stack-count line in either
        observed wording: `(K PRs, ...) (stack #N)` or `K PRs (stack #N)`.
        The last such line wins. N must equal the link number; when the view
        supplied a local branch count, K must equal it. Raw output and the
        count line are stored in stack.sync.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Remote,
        [Parameter(Mandatory = $true)]
        [AllowNull()][object]$ExpectedNumber,
        [AllowNull()][object]$ExpectedCount
    )
    $argv = @('gh', 'stack', 'sync', '--remote', $Remote)
    $run = Invoke-GimFleetExec -Argv $argv -WorkingDirectory $Root -TimeoutSeconds 300
    $output = [string]$run.stdout + "`n" + [string]$run.stderr
    $countLine = $null
    $countK = $null
    $countN = $null
    # Two observed wordings:
    #   (2 PRs, already up to date) (stack #145)
    #   up to date with 2 PRs (stack #145)
    # Capture K from either the parenthesized form or the bare "N PRs" form.
    $countMatches = [regex]::Matches($output, '(?:\((\d+) PRs?[^)]*\)|(\d+) PRs?) \(stack #(\d+)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($countMatches.Count -gt 0) {
        # Last count line wins; gh-stack emits different lead-ins for the
        # create-path sync and later syncs, but the count suffix is shared.
        $countMatch = $countMatches[$countMatches.Count - 1]
        # Keep the whole source line, not just the regex suffix; result.json
        # diagnostics and tests expect the wording that produced the count.
        $lineStart = $output.LastIndexOf("`n", $countMatch.Index)
        if ($lineStart -lt 0) { $lineStart = -1 }
        $lineEnd = $output.IndexOf("`n", $countMatch.Index + $countMatch.Length)
        if ($lineEnd -lt 0) { $lineEnd = $output.Length }
        $countLine = $output.Substring($lineStart + 1, $lineEnd - $lineStart - 1).Trim()
        if ($countMatch.Groups[1].Success) {
            $countK = [int]$countMatch.Groups[1].Value
        } else {
            $countK = [int]$countMatch.Groups[2].Value
        }
        $countN = [int]$countMatch.Groups[3].Value
    }
    $Result.stack.sync = [pscustomobject]@{
        exit = [int]$run.exit_code
        output = $output
        count_line = $countLine
    }
    if ($run.timed_out -or $run.exit_code -ne 0) {
        $errorText = ('gh stack sync failed: ' + (Get-GimPrCommandError -Run $run))
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; command = $argv; exit_code = [int]$run.exit_code; error = $errorText }
    }
    if (Test-GimPrWarningOutput -Text $output) {
        $errorText = ('gh stack sync reported a warning: ' + (ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text $output)))
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; command = $argv; exit_code = 1; error = $errorText }
    }
    if ($null -eq $countLine) {
        $errorText = ('gh stack sync output contained no stack-count line ((K PRs, ...) (stack #N) or K PRs (stack #N)): ' + (ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text $output)))
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; command = $argv; exit_code = 1; error = $errorText }
    }
    if ($null -eq $ExpectedNumber -or $countN -ne [int]$ExpectedNumber) {
        $errorText = ('gh stack sync stack number {0} does not match the link number {1}: {2}' -f [string]$countN, [string]$ExpectedNumber, $countLine)
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; command = $argv; exit_code = 1; error = $errorText }
    }
    if ($null -ne $ExpectedCount -and $countK -ne [int]$ExpectedCount) {
        $errorText = ('gh stack sync PR count {0} does not match the local branch count {1}: {2}' -f [string]$countK, [string]$ExpectedCount, $countLine)
        $Result.stack.error = $errorText
        return [pscustomobject]@{ ok = $false; command = $argv; exit_code = 1; error = $errorText }
    }
    return [pscustomobject]@{ ok = $true; command = $argv; exit_code = 0; error = $null }
}

function Invoke-GimPrStackWorkflow {
    <#
    .SYNOPSIS
        Run the stack lifecycle for the created PR.
    .DESCRIPTION
        First PR: record init/pending and return; no link, no view, no sync.
        Step PR: link the PR into the remote stack, capture the view URL and
        local branch count, then verify with sync. Both must pass before
        stack.verified=true.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Target,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Head,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Remote,
        [Parameter(Mandatory = $true)]
        [ValidateSet('init', 'add')]
        [string]$Method
    )
    if ($Method -eq 'init') {
        [void](Set-GimPrStackOutcome -Result $Result -Method 'init' -Succeeded $true)
        return [pscustomobject]@{ ok = $true; step = $null; command = @(); exit_code = 0; error = $null }
    }
    $link = Invoke-GimPrStackLink -Result $Result -Root $Root -Target $Target -Head $Head -Remote $Remote
    if (-not $link.ok) {
        [void](Set-GimPrStackOutcome -Result $Result -Method 'add' -Succeeded $false -Number $link.number -ErrorText ([string]$link.error))
        return [pscustomobject]@{ ok = $false; step = 'link'; command = @($link.command); exit_code = [int]$link.exit_code; error = [string]$link.error }
    }
    $view = Set-GimPrStackViewDetails -Result $Result -Root $Root
    $sync = Invoke-GimPrStackSync -Result $Result -Root $Root -Remote $Remote -ExpectedNumber $link.number -ExpectedCount $view.branch_count
    if (-not $sync.ok) {
        [void](Set-GimPrStackOutcome -Result $Result -Method 'add' -Succeeded $false -Number $link.number -ErrorText ([string]$sync.error))
        return [pscustomobject]@{ ok = $false; step = 'sync'; command = @($sync.command); exit_code = [int]$sync.exit_code; error = [string]$sync.error }
    }
    [void](Set-GimPrStackOutcome -Result $Result -Method 'add' -Succeeded $true -Number $link.number -Verified $true)
    return [pscustomobject]@{ ok = $true; step = $null; command = @(); exit_code = 0; error = $null }
}

function gim-pr {
    <#
    .SYNOPSIS
        Stage an explicit file list, commit, push, create the PR, and sync the
        stack in one call.
    .DESCRIPTION
        Signature:
          gim-pr --repo <name|path>
                 (--files-file [<path>] | --file <path> [--file <path>]...)
                 --message <subject>
                 --title <title>
                 --target <branch>
                 [--new-branch <branch>]
                 [--pr-body <path>] [--draft]
                 [--delete-pr-body]
                 [--remote <name>]
                 [--assignee <login>]
                 [--dry-run] [--json]

        --repo accepts an absolute path, a relative path with separators
        (resolved against this repo root), or a simple repo name. A simple
        name is matched case-insensitively under the parent of this repo root
        and must be a git root. The caller's working directory is unchanged.

        Exactly one file source is required. --file is repeatable and takes a
        repo-relative path. --files-file with no value derives
        .agent-scratch/pr-files/<feature>/<step>/files.txt from the head
        branch (--new-branch when given, else the current branch): known
        first-PR prefixes (feature, docs, chore, hotfix, release, fix, bugfix,
        refactor, test, ci, build, perf) map <prefix>/<slug> to <slug>/<prefix>;
        any other <feature>/<step> branch maps to <feature>/<step>. More than
        one slash is rejected; pass an explicit --files-file then.

        The list is UTF-8, one repo-relative path per line. Lines are trimmed,
        blanks ignored, globs/directories/escapes rejected, duplicates
        deduped. A missing path is allowed: it goes to git add and git's exact
        error is reported (this is how a tracked deletion is committed). After
        staging, git diff --cached must contain only the listed files,
        otherwise the run stops before committing.

        Before any stack command, two keys are set in the target repo's local
        git config: rerere.enabled=true (removes init's interactive prompt and
        helps cascade rebases) and push.autoSetupRemote=true (a plain push sets
        its upstream). Both are idempotent and documented in
        github-cli-workflow/references/gim-stack-lifecycle.md (config keys) and
        references/harness-config.md.

        First PR (no --new-branch): `gh stack init --base <target>
        <current-branch>` adopts the current branch into a new local stack.
        The target must exist locally; a missing base is ok=false with gh's
        exact error. No link, no sync: stack.method=init, stack.pending=true,
        stack.number=null, stack.verified=null.

        Step PR (--new-branch <branch>): uncommitted step work sits on the
        current stack top. `gh stack add <branch>` creates and checks out the
        branch inside the local stack and does not commit (-A/-u/-m are never
        passed). Then the explicit file list is staged, committed, pushed with
        -u, and the PR is created with --base <target> (the previous branch).

        After a step PR is created, `gim-pr` reads the target step's
        result.json:
          - stack.number present -> `gh stack link <N> <branch> --remote
            <remote>` (append);
          - stack.number null -> `gh stack link --base <target.pr.base>
            <target-branch> <branch> --remote <remote>` (create; no gh pr view
            walk).
        A non-zero exit, a warning line, or a missing `stack #N` in the link
        output is ok=false. Raw output is saved in stack.link.

        `gh stack view --json` then captures the local branch count K
        (branches array length); stack.url may stay null because installed
        view output has no url field. A failed view is stack.view_error plus a
        warning and downgrades the K check to number-only.

        `gh stack sync --remote <remote>` verifies only: exit 0, no warning
        line, and a stack-count line `(K PRs, ...) (stack #N)` or
        `K PRs (stack #N)` (the last such line wins) where N equals the link
        number and K equals the local branch count when known.
        Missing line, N mismatch, or K mismatch is ok=false. Raw output and the
        count line are saved in stack.sync. stack.verified=true only when the
        link and sync verification both pass. There is no `gh stack checkout`,
        no baseRef walk, and no full-chain fallback.

        --pr-body is required unless --draft. Like --files-file it resolves
        repo-relative against the repo root, then must resolve inside the
        evidence directory (the same directory as files.txt/result.json);
        outside it is rejected. A draft without a body passes --body "".
        --delete-pr-body removes the body file after success. The
        result is written to result.json in the file list directory (or the
        derived scratch directory) inside the target repo and printed as a
        text summary or --json. On failure the body file is kept and the
        failed step, command, exit code, and error are recorded.

        Errors name the failed precondition and the required input; they do
        not prescribe a fallback.
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION).
    .EXAMPLE
        gim-pr --repo GIMmos --files-file .agent-scratch/pr-files/feature/step/files.txt --message "step: change" --title "[Feature] Change" --target dev --pr-body .agent-scratch/pr-files/feature/step/body.md --remote origin --assignee '@me' --dry-run --json
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
                '--repo'       = 'Repo'
                '--file'       = 'File'
                '--message'    = 'Message'
                '--title'      = 'Title'
                '--target'     = 'Target'
                '--new-branch' = 'NewBranch'
                '--pr-body'    = 'PrBody'
                '--remote'     = 'Remote'
                '--assignee'   = 'Assignee'
            } `
            -SwitchFlags @{
                '--draft'          = 'Draft'
                '--delete-pr-body' = 'DeletePrBody'
                '--dry-run'        = 'DryRun'
                '--json'           = 'Json'
            } `
            -OptionalValueFlags @{
                '--files-file' = 'FilesFile'
            }
        if ($parsed.Positional.Count -gt 0) {
            Write-GimError "unexpected arguments: '$($parsed.Positional -join ' ')' (gim-pr takes --repo, a file source, --message, --title, --target, and optional flags)"
        }
        if (-not $parsed.ContainsKey('Repo')) {
            Write-GimError 'missing --repo: give a repository name or path'
        }
        $repoArg = ([string]$parsed['Repo']).Trim()
        if ($repoArg -eq '') { Write-GimError '--repo must not be empty' }

        $hasFilesFile = $parsed.ContainsKey('FilesFile')
        $fileArgs = @()
        if ($parsed.ContainsKey('File')) { $fileArgs = @($parsed['File']) }
        if ($hasFilesFile -and $fileArgs.Count -gt 0) {
            Write-GimError 'two file sources given: use --files-file or --file, never both'
        }
        if (-not $hasFilesFile -and $fileArgs.Count -eq 0) {
            Write-GimError 'missing file source: give --files-file [<path>] or --file <path> (repeatable)'
        }

        if (-not $parsed.ContainsKey('Message')) { Write-GimError 'missing --message: give the commit subject' }
        $message = ([string]$parsed['Message']).Trim()
        if ($message -eq '') { Write-GimError '--message must not be empty' }

        if (-not $parsed.ContainsKey('Title')) { Write-GimError 'missing --title: give the PR title' }
        $title = ([string]$parsed['Title']).Trim()
        if ($title -eq '') { Write-GimError '--title must not be empty' }

        if (-not $parsed.ContainsKey('Target')) { Write-GimError 'missing --target: give the PR base branch' }
        $target = ([string]$parsed['Target']).Trim()
        if ($target -eq '') { Write-GimError '--target must not be empty' }
        if ($target -match '^-') {
            Write-GimError ("--target '{0}' starts with '-' and would be parsed as an option by gh; required input: a branch name" -f $target)
        }

        $newBranch = ''
        $hasNewBranch = $parsed.ContainsKey('NewBranch')
        if ($hasNewBranch) {
            $newBranch = ([string]$parsed['NewBranch']).Trim()
            if ($newBranch -eq '') { Write-GimError '--new-branch must not be empty' }
            if ($newBranch -match '^-') {
                Write-GimError ("--new-branch '{0}' starts with '-' and would be parsed as an option by gh; required input: a branch name" -f $newBranch)
            }
        }

        $draft = $parsed.ContainsKey('Draft')
        $deletePrBody = $parsed.ContainsKey('DeletePrBody')
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')

        $bodyArg = ''
        $hasBody = $parsed.ContainsKey('PrBody')
        if ($hasBody) {
            $bodyArg = ([string]$parsed['PrBody']).Trim()
            if ($bodyArg -eq '') { Write-GimError '--pr-body must not be empty' }
        } elseif (-not $draft) {
            Write-GimError 'missing --pr-body: give a PR body file, or pass --draft for an empty draft body'
        }
        if ($deletePrBody -and -not $hasBody) {
            Write-GimError '--delete-pr-body needs --pr-body: there is no body file to delete'
        }

        $remote = 'origin'
        if ($parsed.ContainsKey('Remote')) {
            $remote = ([string]$parsed['Remote']).Trim()
            if ($remote -eq '') { Write-GimError '--remote must not be empty' }
        }
        $assignee = '@me'
        if ($parsed.ContainsKey('Assignee')) {
            $assignee = ([string]$parsed['Assignee']).Trim()
            if ($assignee -eq '') { Write-GimError '--assignee must not be empty' }
        }
    }
    end {
        $root = Resolve-GimFleetRepo -Requested $repoArg
        $top = Invoke-GimFleetExec -Argv @('git', 'rev-parse', '--show-toplevel') -WorkingDirectory $root -TimeoutSeconds 30
        if ($top.exit_code -ne 0) {
            Write-GimError ('--repo is not a git work tree: ' + (Get-GimPrCommandError -Run $top) + '; required input: --repo <git root>')
        }
        $root = ([string]$top.stdout).Trim()
        if ($root -eq '') { $root = Resolve-GimFleetRepo -Requested $repoArg }

        $branchRun = Invoke-GimFleetExec -Argv @('git', 'rev-parse', '--abbrev-ref', 'HEAD') -WorkingDirectory $root -TimeoutSeconds 30
        if ($branchRun.exit_code -ne 0) {
            Write-GimError ('cannot resolve the current branch; required input: a checked-out branch (not a detached HEAD): ' + (Get-GimPrCommandError -Run $branchRun))
        }
        $branch = ([string]$branchRun.stdout).Trim()
        $symbolicRun = Invoke-GimFleetExec -Argv @('git', 'symbolic-ref', '-q', '--short', 'HEAD') -WorkingDirectory $root -TimeoutSeconds 30
        if ($branch -eq 'HEAD' -or $symbolicRun.exit_code -ne 0) {
            Write-GimError 'cannot resolve the current branch: detached HEAD; required input: a checked-out branch (git switch <branch>)'
        }
        if ($branch -eq '') {
            Write-GimError 'cannot resolve the current branch; required input: a checked-out branch (not a detached HEAD)'
        }

        $head = $branch
        $method = 'init'
        if ($hasNewBranch) {
            if ($newBranch -eq $branch) {
                Write-GimError ("--new-branch '{0}' equals the current branch; required input: a new step branch name" -f $newBranch)
            }
            $head = $newBranch
            $method = 'add'
        }
        if ($method -eq 'add' -and $branch -ne $target) {
            Write-GimError ("--new-branch requires the current branch to equal --target (current '{0}', target '{1}'); required input: check out the target branch first" -f $branch, $target)
        }
        if ($method -eq 'init' -and ($branch -eq 'dev' -or $branch -eq 'main' -or $branch -match '^release/')) {
            Write-GimError ("refusing to commit the first PR on protected branch '{0}'; required input: a task branch (dev/main/release/* are protected)" -f $branch)
        }

        $filesPath = $null
        if ($hasFilesFile) {
            $filesValue = ([string]$parsed['FilesFile']).Trim()
            try {
                if ($filesValue -eq '') {
                    $filesPath = Get-GimPrDerivedFilesFile -RepoRoot $root -Branch $head
                } else {
                    $filesPath = Resolve-GimPrRepoPath -RepoRoot $root -Path $filesValue -Label 'file list'
                }
            } catch {
                Write-GimError (Get-GimPrExceptionMessage -ErrorRecord $_)
            }
        }

        $resultDir = $null
        if ($filesPath) {
            $resultDir = Split-Path -Parent $filesPath
        } else {
            # --file mode: derive the evidence directory without the throwing
            # files-file helper, so a successful run never emits a gim: error.
            $resultDir = Get-GimPrResultDirectory -RepoRoot $root -Branch $head
        }
        $resultPath = Join-Path $resultDir 'result.json'

        $bodyPath = ''
        if ($hasBody) {
            try {
                # Resolve like --files-file (repo-relative against the repo
                # root, or an absolute repo-contained path), then enforce the
                # evidence-directory location as a second containment check.
                $bodyRepoPath = Resolve-GimPrRepoPath -RepoRoot $root -Path $bodyArg -Label 'PR body'
                $bodyPath = Resolve-GimPrRepoPath -RepoRoot $resultDir -Path $bodyRepoPath -Label 'PR body' -RootLabel 'evidence directory'
            } catch {
                Write-GimError (Get-GimPrExceptionMessage -ErrorRecord $_)
            }
        }

        $result = New-GimPrResultRecord `
            -Repo $root `
            -Root $root `
            -Branch $branch `
            -Head $head `
            -NewBranch $newBranch `
            -Remote $remote `
            -Target $target `
            -FilesFile $filesPath `
            -Message $message `
            -Title $title `
            -PrBody $bodyPath `
            -Draft $draft `
            -Assignee $assignee

        try {
            if ($hasFilesFile) {
                $files = @(Read-GimPrFileList -RepoRoot $root -Path $filesPath)
            } else {
                $files = @(ConvertTo-GimPrFileList -RepoRoot $root -Entries $fileArgs)
                if ($files.Count -eq 0) {
                    Write-GimError 'no files given: --file needs at least one repo-relative file path'
                }
            }
        } catch {
            $messageText = Get-GimPrExceptionMessage -ErrorRecord $_
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'files' -Command @() -ExitCode 1 -ErrorMessage $messageText
        }

        $result.files = @($files)
        $result.file_count = $files.Count

        if ($dryRun) {
            $result.ok = $true
            $result.dry_run = $true
            $dryTarget = $null
            if ($method -eq 'add') {
                $dryTarget = Get-GimPrTargetResult -RepoRoot $root -Branch $target
                if (-not $dryTarget.ok) { Write-GimError ([string]$dryTarget.error) }
            }
            $planParams = @{
                Branch = $branch
                Head = $head
                Method = $method
                Remote = $remote
                Target = $target
                Files = $files
                Message = $message
                Title = $title
                PrBody = $bodyPath
                Draft = $draft
                Assignee = $assignee
                DeletePrBody = $deletePrBody
            }
            if ($null -ne $dryTarget) {
                $planParams.TargetNumber = $dryTarget.number
                $planParams.TargetBase = [string]$dryTarget.base
            }
            $result.plan = @(New-GimPrDryRunPlan @planParams)
            Write-GimPrOutput -Result $result -ResultPath '' -JsonMode $jsonMode
            return
        }

        $preIndex = Get-GimPrIndexFiles -RepoRoot $root
        if (-not $preIndex.ok) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'index' -Command @('git', 'diff', '--cached', '--name-only') -ExitCode 1 `
                -ErrorMessage ([string]$preIndex.error)
        }
        $preGuard = Test-GimPrStagedSubset -Staged $preIndex.files -Listed $files
        if (-not $preGuard.ok) {
            $extra = (@($preGuard.extra) -join ', ')
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'index' -Command @('git', 'diff', '--cached', '--name-only') -ExitCode 1 `
                -ErrorMessage ('staged files outside the file list: ' + $extra + '; required input: include them in the file list or unstage them')
        }

        # Config + init/add run before staging so the branch is born inside the
        # local stack before the commit exists.
        $setup = Invoke-GimPrStackSetup -Root $root -Target $target -Head $head -Method $method
        if (-not $setup.ok) {
            [void](Set-GimPrStackOutcome -Result $result -Method $method -Succeeded $false -ErrorText ([string]$setup.error))
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep ([string]$setup.step) -Command @($setup.command) -ExitCode ([int]$setup.exit_code) `
                -ErrorMessage ([string]$setup.error)
        }

        $stageArgv = @('git', 'add', '--') + $files
        $stage = Invoke-GimFleetExec -Argv $stageArgv -WorkingDirectory $root -TimeoutSeconds 120
        if ($stage.timed_out -or $stage.exit_code -ne 0) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'stage' -Command $stageArgv -ExitCode $stage.exit_code `
                -ErrorMessage ((Get-GimPrCommandError -Run $stage) + '; required input: a path git can stage (a tracked deletion is allowed)')
        }

        $postIndex = Get-GimPrIndexFiles -RepoRoot $root
        if (-not $postIndex.ok) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'index' -Command @('git', 'diff', '--cached', '--name-only') -ExitCode 1 `
                -ErrorMessage ([string]$postIndex.error)
        }
        $postGuard = Test-GimPrStagedSubset -Staged $postIndex.files -Listed $files
        if (-not $postGuard.ok) {
            $extra = (@($postGuard.extra) -join ', ')
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'index' -Command @('git', 'diff', '--cached', '--name-only') -ExitCode 1 `
                -ErrorMessage ('staged files outside the file list: ' + $extra + '; required input: include them in the file list or unstage them')
        }
        if (@($postIndex.files).Count -eq 0) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'stage' -Command @('git', 'diff', '--cached', '--name-only') -ExitCode 1 `
                -ErrorMessage 'no staged files after git add; required input: a file list whose paths have changes to commit'
        }

        $commitArgv = @('git', 'commit', '--message', $message)
        $commit = Invoke-GimFleetExec -Argv $commitArgv -WorkingDirectory $root -TimeoutSeconds 120
        if ($commit.timed_out -or $commit.exit_code -ne 0) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'commit' -Command $commitArgv -ExitCode $commit.exit_code `
                -ErrorMessage ((Get-GimPrCommandError -Run $commit) + '; required input: the listed files must have committable changes')
        }

        $shaRun = Invoke-GimFleetExec -Argv @('git', 'rev-parse', 'HEAD') -WorkingDirectory $root -TimeoutSeconds 30
        if ($shaRun.exit_code -eq 0) { $result.commit.sha = ([string]$shaRun.stdout).Trim() }
        $subjectRun = Invoke-GimFleetExec -Argv @('git', 'log', '-1', '--pretty=%s') -WorkingDirectory $root -TimeoutSeconds 30
        if ($subjectRun.exit_code -eq 0) { $result.commit.subject = ([string]$subjectRun.stdout).Trim() }

        $pushArgv = @('git', 'push', '-u', $remote, 'HEAD')
        $push = Invoke-GimFleetExec -Argv $pushArgv -WorkingDirectory $root -TimeoutSeconds 300
        if ($push.timed_out -or $push.exit_code -ne 0) {
            $result.push.error = Get-GimPrCommandError -Run $push
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'push' -Command $pushArgv -ExitCode $push.exit_code `
                -ErrorMessage ($result.push.error + ('; required input: a reachable remote ''{0}''' -f $remote))
        }
        $result.push.ok = $true

        $createArgv = @('gh', 'pr', 'create', '--base', $target, '--title', $title, '--assignee', $assignee)
        if ($draft) { $createArgv += '--draft' }
        if ($bodyPath -ne '') {
            $createArgv += @('--body-file', $bodyPath)
        } else {
            $createArgv += @('--body', '')
        }
        $create = Invoke-GimFleetExec -Argv $createArgv -WorkingDirectory $root -TimeoutSeconds 180
        if ($create.timed_out -or $create.exit_code -ne 0) {
            $result.pr.error = Get-GimPrCommandError -Run $create
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'pr-create' -Command $createArgv -ExitCode $create.exit_code `
                -ErrorMessage ($result.pr.error + ('; required input: an authenticated gh and a target branch ''{0}''' -f $target))
        }
        $prUrl = Get-GimPrUrlFromText -Text ([string]$create.stdout + "`n" + [string]$create.stderr)
        if (-not $prUrl) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'pr-create' -Command $createArgv -ExitCode 1 `
                -ErrorMessage ('gh pr create succeeded but returned no pull-request URL; required input: rerun with --json and inspect the captured output: ' + (ConvertTo-GimFleetSingleLine -Text ([string]$create.stdout)))
        }
        $result.pr.url = $prUrl

        $viewArgv = @('gh', 'pr', 'view', $prUrl, '--json', 'number,url,title,baseRefName,headRefName,isDraft,assignees')
        $view = Invoke-GimFleetExec -Argv $viewArgv -WorkingDirectory $root -TimeoutSeconds 120
        if ($view.timed_out -or $view.exit_code -ne 0) {
            $result.pr.error = Get-GimPrCommandError -Run $view
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'pr-view' -Command $viewArgv -ExitCode $view.exit_code `
                -ErrorMessage ($result.pr.error + '; required input: gh must be able to read the created pull request')
        }
        $prView = Get-GimPrViewRecord -JsonText ([string]$view.stdout)
        if ($null -eq $prView) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'pr-view' -Command $viewArgv -ExitCode 1 `
                -ErrorMessage 'gh pr view returned unparseable JSON; required input: a working gh installation'
        }
        if ($null -ne $prView.number) { $result.pr.number = $prView.number }
        if ($prView.url) { $result.pr.url = $prView.url }
        if ($prView.title) { $result.pr.title = $prView.title }
        if ($prView.base) { $result.pr.base = $prView.base }
        if ($prView.head) { $result.pr.head = $prView.head }
        $result.pr.draft = [bool]$prView.draft
        if (@($prView.assignees).Count -eq 0) {
            $result.warnings = @($result.warnings) + ('pull request {0} has no assignee; keeping the requested assignee {1}' -f [string]$result.pr.number, $assignee)
        } else {
            $result.pr.assignee = Resolve-GimPrAssignee -Returned @($prView.assignees) -Requested $assignee
        }

        try {
            $workflow = Invoke-GimPrStackWorkflow -Result $result -Root $root -Target $target -Head $head -Remote $remote -Method $method
        } catch {
            # An unexpected stack error is still a hard failure: never leave a
            # warning-only stack result or an ambiguous pending flag.
            $stackError = Get-GimPrExceptionMessage -ErrorRecord $_
            [void](Set-GimPrStackOutcome -Result $result -Method $method -Succeeded $false -ErrorText $stackError)
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep 'stack' -Command @() -ExitCode 1 -ErrorMessage $stackError
        }
        if (-not $workflow.ok) {
            Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode `
                -FailedStep ([string]$workflow.step) -Command @($workflow.command) -ExitCode ([int]$workflow.exit_code) `
                -ErrorMessage ([string]$workflow.error)
        }

        $result.ok = $true
        Complete-GimPrResult -Result $result -ResultPath $resultPath -JsonMode $jsonMode

        if ($deletePrBody -and $bodyPath -ne '') {
            try {
                [System.IO.File]::Delete($bodyPath)
                $result.body.deleted = $true
            } catch {
                $result.warnings = @($result.warnings) + ('could not delete the PR body file: ' + [string]$_.Exception.Message)
            }
            # Best-effort refresh of the persisted record after the deletion;
            # the earlier save/print already carried the command result.
            try { [void](Save-GimPrResult -Result $result -Path $resultPath) } catch { }
        }
    }
}
