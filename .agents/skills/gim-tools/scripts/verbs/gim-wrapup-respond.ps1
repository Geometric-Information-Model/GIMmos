# gim-wrapup-respond.ps1 - fleet tier (ADR 0008): consume responses.json.
#
# The gather's authored work shell is responses.json. This verb posts inline
# thread replies, resolves when requested, posts free-form per-PR comments, and
# writes a sibling receipt. The authored responses.json is never rewritten.

function New-GimWrapupRespondResultRecord {
    param(
        [bool]$Ok = $false,
        [AllowEmptyString()][string]$Repo = '',
        [AllowEmptyString()][string]$File = '',
        [AllowEmptyString()][string]$Receipt = '',
        [bool]$DryRun = $false,
        [int]$Posted = 0,
        [int]$Resolved = 0,
        [int]$Skipped = 0,
        [int]$Failed = 0,
        [AllowEmptyCollection()][string[]]$Warnings = @(),
        [AllowEmptyCollection()][object[]]$Actions = @(),
        [AllowEmptyString()][string]$ErrorMessage = ''
    )
    return [pscustomobject]@{
        ok = $Ok
        repo = $Repo
        file = $File
        receipt = $Receipt
        dry_run = $DryRun
        posted = $Posted
        resolved = $Resolved
        skipped = $Skipped
        failed = $Failed
        warnings = @($Warnings)
        actions = @($Actions)
        error = $ErrorMessage
    }
}

function Write-GimWrapupRespondFailure {
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [bool]$JsonMode = $false,
        [AllowEmptyString()][string]$Repo = '',
        [AllowEmptyString()][string]$File = '',
        [AllowEmptyString()][string]$Receipt = '',
        [bool]$DryRun = $false,
        [int]$Posted = 0,
        [int]$Resolved = 0,
        [int]$Skipped = 0,
        [int]$Failed = 0,
        [AllowEmptyCollection()][string[]]$Warnings = @(),
        [AllowEmptyCollection()][object[]]$Actions = @()
    )
    if ($JsonMode) {
        Write-GimJson (New-GimWrapupRespondResultRecord -Ok $false -Repo $Repo -File $File -Receipt $Receipt `
            -DryRun $DryRun -Posted $Posted -Resolved $Resolved -Skipped $Skipped -Failed $Failed `
            -Warnings $Warnings -Actions $Actions -ErrorMessage $Message)
        $global:LASTEXITCODE = 1
        exit 1
    }
    Write-GimError $Message
}

function Add-GimWrapupFooter {
    param(
        [AllowEmptyString()][string]$Response = '',
        [Parameter(Mandatory = $true)][string]$Footer
    )
    $text = ([string]$Response).TrimEnd()
    if ($text -match [regex]::Escape($Footer)) { return $text }
    return ($text + "`n`n" + $Footer)
}

function Get-GimWrapupCommentKey {
    param([AllowEmptyString()][string]$Text = '')
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes([string]$Text))
        return ([System.BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function ConvertTo-GimWrapupActionExcerpt {
    param([AllowNull()][AllowEmptyString()][string]$Text = '')
    $line = ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text ([string]$Text))
    if ($line.Length -gt 160) { $line = $line.Substring(0, 160) + '...' }
    return $line
}

function Write-GimWrapupRespondResult {
    param(
        [Parameter(Mandatory = $true)]$Result,
        [bool]$JsonMode = $false
    )
    if ($JsonMode) {
        Write-GimJson $Result
        return
    }
    if ($Result.dry_run) {
        foreach ($action in @($Result.actions)) {
            if ($action.type -eq 'thread_resolve') {
                Write-Output ('thread_resolve PR#{0} {1}' -f $action.pr, $action.id)
            } else {
                Write-Output ('{0} PR#{1} {2} resolve={3} response={4}' -f `
                    $action.type, $action.pr, $action.id, [bool]$action.resolve, [string]$action.response_excerpt)
            }
        }
    }
    foreach ($warningText in @($Result.warnings)) {
        Write-Output ('warning: ' + [string]$warningText)
    }
    foreach ($action in @($Result.actions)) {
        if (-not [bool]$action.ok) {
            Write-Output ('failed {0} PR#{1} {2}: {3}' -f $action.type, $action.pr, $action.id, [string]$action.error)
        }
    }
    Write-Output ('posted={0} resolved={1} skipped={2} failed={3}' -f `
        [int]$Result.posted, [int]$Result.resolved, [int]$Result.skipped, [int]$Result.failed)
}

function ConvertFrom-GimWrapupRemoteThreads {
    param([AllowNull()]$Document)
    $map = @{}
    if ($null -eq $Document) { return $map }
    foreach ($review in @($Document.reviews)) {
        foreach ($comment in @($review.comments)) {
            $id = [string]$comment.thread_id
            if ($id -ne '') {
                $map[$id] = [pscustomobject]@{
                    remote_state = $(if ([bool]$comment.is_resolved) { 'resolved' } else { 'open' })
                }
            }
        }
    }
    foreach ($thread in @($Document.threads)) {
        $id = [string]$thread.thread_id
        if ($id -ne '') {
            $map[$id] = [pscustomobject]@{
                remote_state = $(if ([bool]$thread.is_resolved) { 'resolved' } else { 'open' })
            }
        }
    }
    return $map
}

function Save-GimWrapupRespondReceipt {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Repo,
        [AllowEmptyCollection()][object[]]$Prs = @()
    )
    $doc = [pscustomobject]@{
        schema_version = 1
        repo = $Repo
        generated_at = [string]([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fff') + 'Z')
        prs = @($Prs)
    }
    $json = ConvertTo-Json -InputObject $doc -Depth 20
    $json = $json -replace "`r`n", "`n"
    $roundTrip = $json | ConvertFrom-Json
    if ($null -eq $roundTrip -or $null -eq $roundTrip.PSObject.Properties['prs']) {
        throw 'missing prs after round-trip'
    }
    $tmp = Join-Path ([System.IO.Path]::GetDirectoryName($Path)) ('.respond-receipt-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $tmp -Destination $Path -Force
    } catch {
        if (Test-Path -LiteralPath $tmp -PathType Leaf) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
        throw
    }
}

function gim-wrapup-respond {
    <#
    .SYNOPSIS
        Consume the gather responses shell and write a receipt.
    .DESCRIPTION
        Signature:
          gim-wrapup-respond --repo <name|path> --file <responses.json>
                             --harness <name> [--dry-run] [--json]

        responses.json is the authored work shell: per PR, threads with
        thread_id/resolve/response and comments as a free-form string array.
        Replies use gh-pr-review comments reply; resolves use gh-pr-review
        threads resolve only when resolve=true and the reply succeeded. Comments
        use gh pr comment in order. Footer is appended once, after a blank line.

        Apply-time remote refresh reads gh-pr-review review view per touched PR.
        Remotely resolved threads are marked resolved without a reply; resolves
        run only when remotely open; a locally resolved thread that is remotely
        open is retried when resolve=true. The sibling receipt
        <responses>.result.json records posted_at/resolved_at/remote_state/
        last_error; reruns skip completed entries and retry failures. The
        authored responses.json is never rewritten. --dry-run makes zero gh
        calls and prints planned actions.
    .PARAMETER Raw
        '--flag value' options.
    .EXAMPLE
        gim-wrapup-respond --repo CoreWebApi --file .agent-scratch/wrapup/chain-118/responses.json --harness gim-version-controller --dry-run --json
    .EXAMPLE
        gim-wrapup-respond --repo CoreWebApi --file .agent-scratch/wrapup/chain-118/responses.json --harness gim-version-controller --json
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
                '--repo'    = 'Repo'
                '--file'    = 'File'
                '--harness' = 'Harness'
            } `
            -SwitchFlags @{
                '--dry-run' = 'DryRun'
                '--json'    = 'Json'
            }
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
        if ($parsed.Positional.Count -gt 0) {
            Write-GimWrapupRespondFailure -Message ("unexpected arguments: '{0}' (gim-wrapup-respond takes --repo, --file, --harness, --dry-run, --json)" -f ($parsed.Positional -join ' ')) -JsonMode $jsonMode -DryRun $dryRun
        }
        if (-not $parsed.ContainsKey('Repo')) { Write-GimWrapupRespondFailure -Message 'missing --repo: give a repository name or path' -JsonMode $jsonMode -DryRun $dryRun }
        $repoArg = ([string]$parsed['Repo']).Trim()
        if ($repoArg -eq '') { Write-GimWrapupRespondFailure -Message '--repo must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
        if (-not $parsed.ContainsKey('File')) { Write-GimWrapupRespondFailure -Message 'missing --file: give the responses.json path' -JsonMode $jsonMode -DryRun $dryRun }
        $fileArg = ([string]$parsed['File']).Trim()
        if ($fileArg -eq '') { Write-GimWrapupRespondFailure -Message '--file must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
        if (-not $parsed.ContainsKey('Harness')) { Write-GimWrapupRespondFailure -Message 'missing --harness: give the harness name for the footer' -JsonMode $jsonMode -DryRun $dryRun }
        $harness = ([string]$parsed['Harness']).Trim()
        if ($harness -eq '') { Write-GimWrapupRespondFailure -Message '--harness must not be empty' -JsonMode $jsonMode -DryRun $dryRun }
        if ($harness -notmatch '^[A-Za-z0-9._-]+$') { Write-GimWrapupRespondFailure -Message '--harness may contain only letters, digits, dot, underscore, or hyphen' -JsonMode $jsonMode -DryRun $dryRun }
    }
    end {
        try {
            $repoRoot = Resolve-GimFleetRepo -Requested $repoArg
            $roots = Get-GimRoots
        } catch {
            Write-GimWrapupRespondFailure -Message ([string]$_.Exception.Message) -JsonMode $jsonMode -DryRun $dryRun
            return
        }
        $fileCandidate = $fileArg
        if (-not [System.IO.Path]::IsPathRooted($fileCandidate)) { $fileCandidate = Join-Path $repoRoot $fileCandidate }
        try { $filePath = Get-GimRealPath -Path $fileCandidate } catch {
            Write-GimWrapupRespondFailure -Message ([string]$_.Exception.Message) -JsonMode $jsonMode -DryRun $dryRun -File $fileArg
            return
        }
        if (-not (Test-GimContainedPath -RealPath $filePath -RootRealPaths @($roots.read))) {
            Write-GimWrapupRespondFailure -Message ("responses file is outside the configured roots: '{0}'; required input: the repo must be listed in roots.json" -f $filePath) -JsonMode $jsonMode -DryRun $dryRun -File $filePath
            return
        }
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            Write-GimWrapupRespondFailure -Message ("responses file not found: '{0}'" -f $filePath) -JsonMode $jsonMode -DryRun $dryRun -File $filePath
            return
        }
        $receiptPath = Join-Path ([System.IO.Path]::GetDirectoryName($filePath)) ([System.IO.Path]::GetFileNameWithoutExtension($filePath) + '.result.json')
        try {
            $doc = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
        } catch {
            Write-GimWrapupRespondFailure -Message ("responses file is unparseable: '{0}'" -f $filePath) -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
            return
        }
        if ($null -eq $doc -or $null -eq $doc.PSObject.Properties['repo'] -or [string]$doc.repo -notmatch '^[^/]+/[^/]+$') {
            Write-GimWrapupRespondFailure -Message 'responses file has no repo owner/name' -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
            return
        }
        $repo = [string]$doc.repo
        $prs = @($doc.prs)
        if ($null -eq $doc.PSObject.Properties['prs']) {
            Write-GimWrapupRespondFailure -Message 'responses file has no prs array' -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
            return
        }
        if ($null -ne $doc.PSObject.Properties['schema_version']) {
            Write-GimWrapupRespondFailure -Message 'responses file is gather-shaped; required input: the responses.json work shell (no schema_version)' -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
            return
        }
        foreach ($pr in $prs) {
            $number = 0
            if (-not [int]::TryParse([string]$pr.pr, [ref]$number) -or $number -lt 1) {
                Write-GimWrapupRespondFailure -Message 'responses file has a PR without a valid number' -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
                return
            }
            if ($null -eq $pr.PSObject.Properties['threads'] -or $null -eq $pr.PSObject.Properties['comments']) {
                Write-GimWrapupRespondFailure -Message ('responses PR {0} must have threads and comments arrays' -f $number) -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
                return
            }
            foreach ($thread in @($pr.threads)) {
                if ([string]$thread.thread_id -eq '' -or $null -eq $thread.PSObject.Properties['response'] -or $null -eq $thread.PSObject.Properties['resolve']) {
                    Write-GimWrapupRespondFailure -Message ('responses PR {0} has a thread without thread_id/response/resolve' -f $number) -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
                    return
                }
            }
            foreach ($comment in @($pr.comments)) {
                if ($null -ne $comment -and -not ($comment -is [string])) {
                    Write-GimWrapupRespondFailure -Message ('responses PR {0} comments must be strings' -f $number) -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
                    return
                }
            }
        }

        $receipt = $null
        if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
            try {
                $receipt = [System.IO.File]::ReadAllText($receiptPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
            } catch {
                Write-GimWrapupRespondFailure -Message ("receipt is unparseable: '{0}'" -f $receiptPath) -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
                return
            }
        }
        $receiptPrByNumber = @{}
        if ($null -ne $receipt) {
            foreach ($rp in @($receipt.prs)) { $receiptPrByNumber[[string]$rp.pr] = $rp }
        }

        $footer = ('__this action was performed by {0} automatically__' -f $harness)
        $posted = 0
        $resolved = 0
        $skipped = 0
        $failed = 0
        $warnings = New-Object System.Collections.Generic.List[string]
        $actions = New-Object System.Collections.Generic.List[object]
        $receiptPrs = New-Object System.Collections.Generic.List[object]
        $now = { [string]([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fff') + 'Z') }

        $runAction = {
            param($Argv, $Action)
            $run = Invoke-GimFleetExec -Argv $Argv -WorkingDirectory $repoRoot -TimeoutSeconds 120
            if ($run.timed_out -or $run.exit_code -ne 0) {
                $message = ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text ([string]$run.stdout + "`n" + [string]$run.stderr))
                if ($message -eq '') { $message = ('command exited with code {0}' -f $run.exit_code) }
                $Action.ok = $false
                $Action.error = $message
                return $false
            }
            $Action.ok = $true
            $Action.error = $null
            return $true
        }

        foreach ($pr in $prs) {
            $number = 0
            if (-not [int]::TryParse([string]$pr.pr, [ref]$number) -or $number -lt 1) {
                Write-GimWrapupRespondFailure -Message 'responses file has a PR without a valid number' -JsonMode $jsonMode -DryRun $dryRun -File $filePath -Receipt $receiptPath
                return
            }
            $oldPr = if ($receiptPrByNumber.ContainsKey([string]$number)) { $receiptPrByNumber[[string]$number] } else { $null }
            $oldThreads = @{}
            $oldComments = @{}
            if ($null -ne $oldPr) {
                foreach ($rt in @($oldPr.threads)) { $oldThreads[[string]$rt.thread_id] = $rt }
                foreach ($rc in @($oldPr.comments)) { $oldComments[[string]$rc.key] = $rc }
            }

            $remoteMap = @{}
            $remoteStateDefault = 'unknown'
            $threads = @($pr.threads)
            $commentsForPr = @($pr.comments)
            if (-not $dryRun -and ($threads.Count -gt 0 -or $commentsForPr.Count -gt 0)) {
                $argv = @('gh', 'pr-review', 'review', 'view', '-R', $repo, '--pr', [string]$number, '--include-comment-node-id')
                $run = Invoke-GimFleetExec -Argv $argv -WorkingDirectory $repoRoot -TimeoutSeconds 120
                if ($run.timed_out -or $run.exit_code -ne 0) {
                    $text = ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text ([string]$run.stdout + "`n" + [string]$run.stderr))
                    [void]$warnings.Add(('remote refresh failed for PR {0}: {1}' -f $number, $text))
                } else {
                    try {
                        $remoteMap = ConvertFrom-GimWrapupRemoteThreads -Document ([string]$run.stdout | ConvertFrom-Json)
                        $remoteStateDefault = 'missing'
                    } catch {
                        [void]$warnings.Add(('remote refresh returned unparseable JSON for PR {0}' -f $number))
                    }
                }
            }

            $receiptThreads = New-Object System.Collections.Generic.List[object]
            foreach ($thread in $threads) {
                $threadId = [string]$thread.thread_id
                $response = [string]$thread.response
                $resolve = [bool]$thread.resolve
                $old = if ($oldThreads.ContainsKey($threadId)) { $oldThreads[$threadId] } else { $null }
                $postedAt = if ($null -ne $old -and $null -ne $old.posted_at) { ConvertTo-GimWrapupIsoTimestamp -Value $old.posted_at } else { '' }
                $resolvedAt = if ($null -ne $old -and $null -ne $old.resolved_at) { ConvertTo-GimWrapupIsoTimestamp -Value $old.resolved_at } else { '' }
                $lastError = ''
                $remoteState = if ($remoteMap.ContainsKey($threadId)) { [string]$remoteMap[$threadId].remote_state } else { $remoteStateDefault }
                if ($dryRun) { $remoteState = 'unknown' }

                if ($dryRun) {
                    $replyReady = ($postedAt -ne '')
                    if ([string]::IsNullOrWhiteSpace($response) -and $postedAt -eq '') {
                        $skipped++
                    } elseif ($postedAt -eq '') {
                        [void]$actions.Add([pscustomobject]@{ pr = $number; type = 'thread_reply'; id = $threadId; response_excerpt = ConvertTo-GimWrapupActionExcerpt -Text $response; resolve = $resolve; ok = $true; error = $null })
                        $posted++
                        $replyReady = $true
                    }
                    if ($replyReady -and $resolve -and $resolvedAt -eq '') {
                        [void]$actions.Add([pscustomobject]@{ pr = $number; type = 'thread_resolve'; id = $threadId; response_excerpt = ''; resolve = $true; ok = $true; error = $null })
                        $resolved++
                    }
                } else {
                    if ($remoteState -eq 'resolved') {
                        if ($postedAt -eq '') {
                            [void]$warnings.Add(('thread {0} is already resolved remotely; skipped reply' -f $threadId))
                            $skipped++
                        }
                        if ($resolvedAt -eq '') { $resolvedAt = (& $now) }
                    } elseif ([string]::IsNullOrWhiteSpace($response) -and $postedAt -eq '') {
                        $skipped++
                    } elseif ($remoteState -eq 'missing') {
                        $lastError = 'thread not found remotely'
                        $failed++
                        [void]$actions.Add([pscustomobject]@{ pr = $number; type = 'thread_missing'; id = $threadId; response_excerpt = ''; resolve = $resolve; ok = $false; error = $lastError })
                    } else {
                        $replyReady = ($postedAt -ne '')
                        if (-not $replyReady) {
                            if ([string]::IsNullOrWhiteSpace($response)) {
                                $skipped++
                            } else {
                                $action = [pscustomobject]@{ pr = $number; type = 'thread_reply'; id = $threadId; response_excerpt = ConvertTo-GimWrapupActionExcerpt -Text $response; resolve = $resolve; ok = $true; error = $null }
                                $body = Add-GimWrapupFooter -Response $response -Footer $footer
                                $argv = @('gh', 'pr-review', 'comments', 'reply', '--thread-id', $threadId, '--body', $body, '-R', $repo, '--pr', [string]$number)
                                if (& $runAction $argv $action) { $postedAt = (& $now); $posted++ } else { $lastError = $action.error; $failed++ }
                                [void]$actions.Add($action)
                            }
                        }
                        $replyReady = ($postedAt -ne '')
                        if ($replyReady -and $resolve -and $remoteState -eq 'open') {
                            $action = [pscustomobject]@{ pr = $number; type = 'thread_resolve'; id = $threadId; response_excerpt = ''; resolve = $true; ok = $true; error = $null }
                            $argv = @('gh', 'pr-review', 'threads', 'resolve', '--thread-id', $threadId, '-R', $repo, '--pr', [string]$number)
                            if (& $runAction $argv $action) { $resolvedAt = (& $now); $resolved++ } else { $lastError = $action.error; $failed++ }
                            [void]$actions.Add($action)
                        }
                    }
                }
                [void]$receiptThreads.Add([pscustomobject]@{
                    thread_id = $threadId
                    posted_at = $(if ($postedAt -eq '') { $null } else { $postedAt })
                    resolved_at = $(if ($resolvedAt -eq '') { $null } else { $resolvedAt })
                    remote_state = $remoteState
                    last_error = $(if ($lastError -eq '') { $null } else { $lastError })
                })
            }

            $receiptComments = New-Object System.Collections.Generic.List[object]
            foreach ($comment in @($pr.comments)) {
                $text = [string]$comment
                $commentKey = Get-GimWrapupCommentKey -Text $text
                $old = if ($oldComments.ContainsKey($commentKey)) { $oldComments[$commentKey] } else { $null }
                $postedAt = if ($null -ne $old -and $null -ne $old.posted_at) { ConvertTo-GimWrapupIsoTimestamp -Value $old.posted_at } else { '' }
                $lastError = ''
                if ([string]::IsNullOrWhiteSpace($text)) {
                    $skipped++
                } elseif ($dryRun) {
                    if ($postedAt -eq '') {
                        [void]$actions.Add([pscustomobject]@{ pr = $number; type = 'pr_comment'; id = $commentKey; response_excerpt = ConvertTo-GimWrapupActionExcerpt -Text $text; resolve = $false; ok = $true; error = $null })
                        $posted++
                    }
                } elseif ($postedAt -eq '') {
                    $action = [pscustomobject]@{ pr = $number; type = 'pr_comment'; id = $commentKey; response_excerpt = ConvertTo-GimWrapupActionExcerpt -Text $text; resolve = $false; ok = $true; error = $null }
                    $body = Add-GimWrapupFooter -Response $text -Footer $footer
                    $argv = @('gh', 'pr', 'comment', [string]$number, '--body', $body, '-R', $repo)
                    if (& $runAction $argv $action) { $postedAt = (& $now); $posted++ } else { $lastError = $action.error; $failed++ }
                    [void]$actions.Add($action)
                }
                [void]$receiptComments.Add([pscustomobject]@{
                    key = $commentKey
                    posted_at = $(if ($postedAt -eq '') { $null } else { $postedAt })
                    last_error = $(if ($lastError -eq '') { $null } else { $lastError })
                })
            }

            [void]$receiptPrs.Add([pscustomobject]@{
                pr = $number
                threads = @($receiptThreads.ToArray())
                comments = @($receiptComments.ToArray())
            })
            if (-not $dryRun) {
                try {
                    Save-GimWrapupRespondReceipt -Path $receiptPath -Repo $repo -Prs @($receiptPrs.ToArray())
                } catch {
                    Write-GimWrapupRespondFailure -Message ('cannot write receipt: ' + [string]$_.Exception.Message) -JsonMode $jsonMode -DryRun $dryRun `
                        -Repo $repo -File $filePath -Receipt $receiptPath -Posted $posted -Resolved $resolved -Skipped $skipped -Failed $failed -Warnings @($warnings.ToArray()) -Actions @($actions.ToArray())
                    return
                }
            }
        }

        if (-not $dryRun) {
            $receiptDoc = [pscustomobject]@{
                schema_version = 1
                repo = $repo
                generated_at = (& $now)
                prs = @($receiptPrs.ToArray())
            }
            try {
                $receiptJson = ConvertTo-Json -InputObject $receiptDoc -Depth 20
                $receiptJson = $receiptJson -replace "`r`n", "`n"
                $receiptRoundTrip = $receiptJson | ConvertFrom-Json
                if ($null -eq $receiptRoundTrip -or $null -eq $receiptRoundTrip.PSObject.Properties['prs']) { throw 'missing prs after round-trip' }
                $tmp = Join-Path ([System.IO.Path]::GetDirectoryName($receiptPath)) ('.respond-receipt-' + [guid]::NewGuid().ToString('N') + '.tmp')
                [System.IO.File]::WriteAllText($tmp, $receiptJson, (New-Object System.Text.UTF8Encoding($false)))
                Move-Item -LiteralPath $tmp -Destination $receiptPath -Force
            } catch {
                Write-GimWrapupRespondFailure -Message ('cannot write receipt: ' + [string]$_.Exception.Message) -JsonMode $jsonMode -DryRun $dryRun `
                    -Repo $repo -File $filePath -Receipt $receiptPath -Posted $posted -Resolved $resolved -Skipped $skipped -Failed $failed -Warnings @($warnings.ToArray()) -Actions @($actions.ToArray())
                return
            }
        }

        $result = New-GimWrapupRespondResultRecord -Ok ($failed -eq 0) -Repo $repo -File $filePath -Receipt $receiptPath -DryRun $dryRun `
            -Posted $posted -Resolved $resolved -Skipped $skipped -Failed $failed -Warnings @($warnings.ToArray()) -Actions @($actions.ToArray())
        Write-GimWrapupRespondResult -JsonMode $jsonMode -Result $result
        if ($failed -gt 0) { $global:LASTEXITCODE = 1 }
    }
}
