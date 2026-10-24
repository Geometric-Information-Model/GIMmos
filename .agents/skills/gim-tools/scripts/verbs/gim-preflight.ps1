# gim-preflight.ps1 - fleet tier (ADR 0008): auth status, per-repo
# branch/clean state, parallel fetch, conditional ff-only pull, and the
# product docs index (local / origin/<expected-branch> / open PRs).
#
# Safety: never checks out, stashes, resets, or forces. A pull runs only when
# the repo is clean and on the expected branch. The orchestrator uses the JSON
# report to decide; gim-preflight itself performs only fetch + ff-only pull.
# --dry-run performs neither: it reports the planned actions and the read-only
# status/docs index. Auth output carries host/site only; forwarded error text
# is redacted.

function Invoke-GimPreflightGhAuth {
    <#
    .SYNOPSIS
        `gh auth status` in report form: { ok, skipped, host, error }.
        Skipped checks are marked skipped, never failed.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [bool]$Skip = $false
    )
    if ($Skip) {
        return [pscustomobject]@{ ok = $null; skipped = $true; host = $null; error = $null }
    }
    $r = Invoke-GimFleetExec -Argv @('gh', 'auth', 'status') -WorkingDirectory $WorkingDirectory -TimeoutSeconds 30
    if ($r.timed_out) {
        return [pscustomobject]@{ ok = $false; skipped = $false; host = $null; error = 'gh auth status timed out' }
    }
    $text = ([string]$r.stdout + "`n" + [string]$r.stderr)
    if ($r.exit_code -ne 0) {
        return [pscustomobject]@{
            ok = $false
            skipped = $false
            host = $null
            error = (Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text $text))
        }
    }
    $hostName = $null
    foreach ($line in ($text -split "`r?`n")) {
        $l = $line.Trim()
        if ($l -eq '') { continue }
        $m = [regex]::Match($l, '(?i)Logged in to\s+(\S+)')
        if ($m.Success) { $hostName = $m.Groups[1].Value.TrimEnd('.') ; break }
        if ($l -match '^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$') { $hostName = $l; break }
    }
    return [pscustomobject]@{ ok = $true; skipped = $false; host = $hostName; error = $null }
}

function Invoke-GimPreflightJiraAuth {
    <#
    .SYNOPSIS
        `acli jira auth status` in report form: { ok, skipped, site, error }.
        Only the site is returned; the email / account line is never forwarded.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [bool]$Skip = $false
    )
    if ($Skip) {
        return [pscustomobject]@{ ok = $null; skipped = $true; site = $null; error = $null }
    }
    $r = Invoke-GimFleetExec -Argv @('acli', 'jira', 'auth', 'status') -WorkingDirectory $WorkingDirectory -TimeoutSeconds 30
    if ($r.timed_out) {
        return [pscustomobject]@{ ok = $false; skipped = $false; site = $null; error = 'acli jira auth status timed out' }
    }
    $text = ([string]$r.stdout + "`n" + [string]$r.stderr)
    if ($r.exit_code -ne 0) {
        return [pscustomobject]@{
            ok = $false
            skipped = $false
            site = $null
            error = (Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text $text))
        }
    }
    $site = $null
    $m = [regex]::Match($text, '(?im)^\s*Site:\s*(\S+)')
    if ($m.Success) { $site = $m.Groups[1].Value.TrimEnd('.') }
    if (-not $site) {
        $m = [regex]::Match($text, '(?i)https?://([^/\s]+)')
        if ($m.Success) { $site = $m.Groups[1].Value }
    }
    return [pscustomobject]@{ ok = $true; skipped = $false; site = $site; error = $null }
}

function Resolve-GimPreflightRepo {
    <#
    .SYNOPSIS
        Resolve one --repo name or path through Resolve-GimFleetRepo, then
        resolve the git root and record branch / dirty. Untracked
        `.agent-scratch/` evidence is ignored by the dirty check so a
        cross-repo gim-pr result does not mark a sibling repo dirty.
        Returns { ok, path, root, branch, clean, error }.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Requested
    )
    $result = [pscustomobject]@{
        ok = $false
        path = $Requested
        root = $null
        branch = ''
        clean = $false
        error = $null
    }
    if ($Requested.Trim() -eq '') {
        $result.error = 'empty --repo name/path'
        return $result
    }
    try {
        $full = Resolve-GimFleetRepo -Requested $Requested
    } catch {
        $result.error = [string]$_.Exception.Message
        if ($result.error -match '^gim:\s*') { $result.error = $result.error -replace '^gim:\s*', '' }
        return $result
    }
    $result.path = $full
    if (-not (Test-Path -LiteralPath $full -PathType Container)) {
        $result.error = "path is not a directory: '$Requested'"
        return $result
    }
    $top = Invoke-GimFleetExec -Argv @('git', 'rev-parse', '--show-toplevel') -WorkingDirectory $full -TimeoutSeconds 30
    if ($top.exit_code -ne 0) {
        $result.error = (Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$top.stderr)))
        if (-not $result.error) { $result.error = 'not a git repository' }
        $result.path = $full
        return $result
    }
    $root = ([string]$top.stdout).Trim()
    if ($root -eq '') { $root = $full }
    try { $root = [System.IO.Path]::GetFullPath($root) } catch { }
    $result.root = $root
    $result.path = $root
    $branch = Invoke-GimFleetExec -Argv @('git', 'rev-parse', '--abbrev-ref', 'HEAD') -WorkingDirectory $root -TimeoutSeconds 30
    if ($branch.exit_code -ne 0) {
        $result.error = 'git repository has no resolvable HEAD branch'
        return $result
    }
    $result.branch = ([string]$branch.stdout).Trim()
    $status = Invoke-GimFleetExec -Argv @('git', 'status', '--porcelain') -WorkingDirectory $root -TimeoutSeconds 60
    if ($status.exit_code -ne 0) {
        $result.error = (Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$status.stderr)))
        if (-not $result.error) { $result.error = 'git status failed' }
        return $result
    }
    $result.clean = Test-GimPreflightStatusClean -Text ([string]$status.stdout)
    $result.ok = $true
    return $result
}

function Test-GimPreflightStatusClean {
    <#
    .SYNOPSIS
        True when `git status --porcelain` has no entry that matters.
    .DESCRIPTION
        Untracked `.agent-scratch/` evidence is ignored so a cross-repo
        gim-pr result does not mark a sibling preflight repo dirty. Every
        other tracked/untracked entry keeps the existing dirty semantics.
    #>
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )
    if ($null -eq $Text) { return $true }
    foreach ($line in ($Text -split "`r?`n")) {
        $entry = $line.TrimEnd()
        if ($entry -eq '') { continue }
        if ($entry -match '^\?\? \.agent-scratch($|[/\\])') { continue }
        return $false
    }
    return $true
}

function Get-GimPreflightPullPlan {
    <#
    .SYNOPSIS
        Decide the fetch/pull action for one repo without touching the repo.
        Returns { planned, skipped, reason, status }.
    .DESCRIPTION
        Pure decision shared by the live and --dry-run paths. Dirty, detached
        HEAD, branch mismatch, unresolved repo, and failed fetch all skip the
        pull with a reason; a clean repo on the expected branch plans the
        ff-only pull. In dry-run the plan is reported, never executed.
    #>
    param(
        [bool]$RepoOk = $true,
        [bool]$FetchOk = $true,
        [AllowEmptyString()]
        [string]$Branch = '',
        [AllowEmptyString()]
        [string]$Expected = '',
        [bool]$Clean = $false,
        [bool]$DryRun = $false
    )
    if (-not $RepoOk) {
        return [pscustomobject]@{ planned = $false; skipped = $true; reason = 'repo unresolved'; status = 'error' }
    }
    if ($Expected -ne '' -and $Branch -ne $Expected) {
        return [pscustomobject]@{
            planned = $false
            skipped = $true
            reason = ('on {0}, expected {1}' -f $Branch, $Expected)
            status = 'branch_mismatch'
        }
    }
    if (-not $Clean) {
        return [pscustomobject]@{ planned = $false; skipped = $true; reason = 'dirty worktree'; status = 'dirty' }
    }
    if ($DryRun) {
        return [pscustomobject]@{
            planned = $true
            skipped = $true
            reason = 'dry run: would pull --ff-only'
            status = 'dry_run'
        }
    }
    if (-not $FetchOk) {
        return [pscustomobject]@{ planned = $false; skipped = $true; reason = 'fetch failed'; status = 'fetch_failed' }
    }
    return [pscustomobject]@{ planned = $true; skipped = $false; reason = $null; status = 'ok' }
}

function Get-GimPreflightDocsIndexes {
    <#
    .SYNOPSIS
        Build the four-family docs index for one repo: local top-level files,
        one git ls-tree per family against origin/<expected>, and one
        path-only `gh pr list --jq '.[].files[].path'`.
    .DESCRIPTION
        Local numbers are always read. The remote and open-PR sources are
        all-or-nothing: if either fails (or gh auth was skipped), every family
        is marked ok=false with remote/open_pr/max/next null and the error, so
        the orchestrator never consumes a stale next.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$ExpectedBranch,
        [bool]$SkipGh = $false
    )
    $localFiles = @{}
    $remoteFiles = @{}
    $remoteError = $null
    $rootTrim = $Root.TrimEnd('\')
    foreach ($family in $script:GimFleetDocFamilies) {
        $rel = New-Object System.Collections.Generic.List[string]
        $dir = Join-Path $Root ($family.path -replace '/', '\')
        if (Test-Path -LiteralPath $dir -PathType Container) {
            foreach ($file in [System.IO.Directory]::GetFiles($dir, '*', [System.IO.SearchOption]::TopDirectoryOnly)) {
                if ($file.Length -le $rootTrim.Length) { continue }
                $relPath = $file.Substring($rootTrim.Length).TrimStart('\') -replace '\\', '/'
                [void]$rel.Add($relPath)
            }
        }
        $localFiles[$family.name] = @($rel.ToArray())

        $remote = New-Object System.Collections.Generic.List[string]
        if ($ExpectedBranch -eq '') {
            if (-not $remoteError) { $remoteError = 'remote: no expected branch' }
        } else {
            $r = Invoke-GimFleetExec -Argv @('git', 'ls-tree', '-r', '--name-only', ("origin/" + $ExpectedBranch), '--', $family.path) -WorkingDirectory $Root -TimeoutSeconds 60
            if ($r.exit_code -ne 0) {
                if (-not $remoteError) {
                    $remoteError = Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$r.stderr))
                    if (-not $remoteError) { $remoteError = ("git ls-tree origin/{0} failed" -f $ExpectedBranch) }
                }
            } else {
                foreach ($line in (([string]$r.stdout) -split "`r?`n")) {
                    $p = $line.Trim()
                    if ($p -ne '') { [void]$remote.Add($p) }
                }
            }
        }
        $remoteFiles[$family.name] = @($remote.ToArray())
    }

    $openPrFiles = $null
    $openPrError = $null
    if ($SkipGh) {
        $openPrError = 'skipped'
    } else {
        # Path-only output keeps the structured payload far below the runner's
        # capture cap; a truncated or unparseable result is an index failure.
        $r = Invoke-GimFleetExec -Argv @(
            'gh', 'pr', 'list', '--state', 'open', '--limit', '500',
            '--json', 'files', '--jq', '.[].files[].path'
        ) -WorkingDirectory $Root -TimeoutSeconds 120
        if ($r.timed_out) {
            $openPrError = 'gh pr list timed out'
        } elseif ($r.exit_code -ne 0) {
            $openPrError = Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$r.stderr))
            if (-not $openPrError) { $openPrError = 'gh pr list failed' }
        } else {
            try {
                $openPrFiles = @(Get-GimFleetOpenPrFilePaths -Text ([string]$r.stdout))
            } catch {
                $openPrError = $_.Exception.Message
                if (-not $openPrError) { $openPrError = 'gh pr list output could not be parsed' }
            }
        }
    }

    $indexes = [ordered]@{}
    foreach ($family in $script:GimFleetDocFamilies) {
        $err = $null
        if ($remoteError -and $openPrError) { $err = 'remote: ' + $remoteError + '; open_pr: ' + $openPrError }
        elseif ($remoteError) { $err = 'remote: ' + $remoteError }
        elseif ($openPrError) { $err = 'open_pr: ' + $openPrError }
        $failed = [bool]($remoteError -or $openPrError)
        if ($failed) {
            $indexes[$family.name] = Get-GimFleetDocsIndex `
                -Family $family.name `
                -LocalFiles $localFiles[$family.name] `
                -Error $err `
                -Failed $true
        } else {
            $indexes[$family.name] = Get-GimFleetDocsIndex `
                -Family $family.name `
                -LocalFiles $localFiles[$family.name] `
                -RemoteFiles $remoteFiles[$family.name] `
                -OpenPrFiles $openPrFiles
        }
    }
    return [pscustomobject]$indexes
}

function New-GimPreflightEmptyDocsIndexes {
    <#
    .SYNOPSIS
        Failed docs index (ok=false, next=null) for a repo that could not be
        inspected or whose fetch failed.
    #>
    param([string]$Error = $null)
    $indexes = [ordered]@{}
    foreach ($family in $script:GimFleetDocFamilies) {
        $indexes[$family.name] = Get-GimFleetDocsIndex -Family $family.name -Error $Error -Failed $true
    }
    return [pscustomobject]$indexes
}

function gim-preflight {
    <#
    .SYNOPSIS
        Fleet preflight: auth status, repo state, parallel fetch, conditional
        ff-only pull, and the product docs index.
    .DESCRIPTION
        Signature:
          gim-preflight --repo <name|path> [--repo <name|path>]...
                        [--branch <name>]
                        [--skip-gh-auth] [--skip-jira-auth]
                        [--dry-run] [--json]

        --repo accepts an absolute path, a relative path with separators
        (resolved against this repo root), or a simple repo name. A simple
        name is matched case-insensitively under the parent of this repo root
        and must be a git root; a missing name lists the available directories
        and a case-only ambiguity lists the matches. For each --repo it
        resolves the git root, records the current branch and clean/dirty
        state, fetches all repos in parallel, and pulls --ff-only only when the
        repo is clean and on the expected branch. It never checks out, stashes,
        resets, or forces. The caller's working directory is unchanged.

        --branch defaults to dev (the expected branch); pass the prefs
        default_dev_branch explicitly. --dry-run runs no fetch and no pull; it
        reports the planned actions plus the read-only status/docs index.

        The docs index reports the fixed families plans
        (docs/plans/NNNN-*.plan.md), adr (docs/adr/NNNN-*.md), rfcs
        (docs/rfcs/NNNN-*.md), and guides (docs/guides/NNNN-*.md) with
        { ok, local, remote, open_pr, max, next }. Agent-only paths
        (docs/plans/agents/**, docs/adr/agents/**, .agents/**, .cursor/**,
        *-AGENTS-*) are excluded. Remote numbers come from
        origin/<expected-branch>; open-PR numbers come from one path-only
        `gh pr list --state open --json files --jq '.[].files[].path'` per
        repo. If the remote or open-PR source fails (or gh auth is skipped),
        every family is ok=false with remote/open_pr/max/next null; a failed
        fetch invalidates the remote/open-PR index for that repo.

        Auth checks return host/site only. Forwarded error text is redacted
        (emails, account ids). --skip-gh-auth / --skip-jira-auth mark the check
        skipped, not failed. --json emits the full report; the verb exits 1 if
        any auth or repo check fails (dirty is reported, not a failure).
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION).
    .EXAMPLE
        gim-preflight --repo GIMmos --branch dev --json
    .EXAMPLE
        gim-preflight --repo . --branch dev --dry-run --json
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
                '--repo'   = 'Repo'
                '--branch' = 'Branch'
            } `
            -SwitchFlags @{
                '--skip-gh-auth'   = 'SkipGhAuth'
                '--skip-jira-auth' = 'SkipJiraAuth'
                '--dry-run'        = 'DryRun'
                '--json'           = 'Json'
            }
        if ($parsed.Positional.Count -gt 0) {
            Write-GimError "unexpected arguments: '$($parsed.Positional -join ' ')' (give --repo / --branch / skip flags / --dry-run / --json)"
        }
        if (-not $parsed.ContainsKey('Repo')) {
            Write-GimError 'missing --repo: give at least one repository name or path'
        }
        $repos = @($parsed['Repo'])
        if ($repos.Count -eq 0) {
            Write-GimError 'missing --repo: give at least one repository name or path'
        }
        foreach ($repoPath in $repos) {
            if (([string]$repoPath).Trim() -eq '') { Write-GimError '--repo must not be empty' }
        }
        $branchArg = 'dev'
        if ($parsed.ContainsKey('Branch')) { $branchArg = ([string]$parsed['Branch']).Trim() }
        if ($branchArg -eq '') {
            Write-GimError '--branch must not be empty'
        }
        $skipGh = $parsed.ContainsKey('SkipGhAuth')
        $skipJira = $parsed.ContainsKey('SkipJiraAuth')
        $dryRun = $parsed.ContainsKey('DryRun')
        $jsonMode = $parsed.ContainsKey('Json')
    }
    end {
        $repoRoot = Get-GimRepoRoot
        $ghAuth = Invoke-GimPreflightGhAuth -WorkingDirectory $repoRoot -Skip $skipGh
        $jiraAuth = Invoke-GimPreflightJiraAuth -WorkingDirectory $repoRoot -Skip $skipJira

        $resolved = New-Object System.Collections.Generic.List[object]
        foreach ($repoPath in $repos) {
            $resolved.Add((Resolve-GimPreflightRepo -Requested ([string]$repoPath)))
        }

        # Fetch all resolved repos in parallel (start all, then await each).
        # --dry-run starts none: the report carries the planned fetch instead.
        $fetchCommands = New-Object System.Collections.Generic.List[object]
        $fetchTargets = New-Object System.Collections.Generic.List[int]
        if (-not $dryRun) {
            for ($i = 0; $i -lt $resolved.Count; $i++) {
                if (-not $resolved[$i].ok) { continue }
                $fetchCommands.Add(@{
                    argv = @('git', 'fetch')
                    working_directory = [string]$resolved[$i].root
                    timeout_seconds = 300
                })
                $fetchTargets.Add($i)
            }
        }
        $fetchResults = @()
        if ($fetchCommands.Count -gt 0) {
            $fetchResults = @(Invoke-GimFleetBatch -Commands @($fetchCommands.ToArray()))
        }

        $repoReports = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $resolved.Count; $i++) {
            $repo = $resolved[$i]
            $expected = $branchArg

            $fetch = [pscustomobject]@{ ok = $null; skipped = $false; planned = $false; error = $null }
            if (-not $repo.ok) {
                $fetch.skipped = $true
                $fetch.error = 'repo unresolved'
            } elseif ($dryRun) {
                $fetch.skipped = $true
                $fetch.planned = $true
            } else {
                $fetch.ok = $false
                $fetchPos = [array]::IndexOf($fetchTargets.ToArray(), $i)
                if ($fetchPos -ge 0 -and $fetchPos -lt $fetchResults.Count) {
                    $fr = $fetchResults[$fetchPos]
                    if ($fr.timed_out) {
                        $fetch.error = 'git fetch timed out'
                    } elseif ($fr.exit_code -ne 0) {
                        $fetch.error = Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$fr.stderr))
                        if (-not $fetch.error) { $fetch.error = ('git fetch exited with code {0}' -f $fr.exit_code) }
                    } else {
                        $fetch.ok = $true
                    }
                } else {
                    $fetch.error = 'git fetch was not started'
                }
            }

            $plan = Get-GimPreflightPullPlan `
                -RepoOk ([bool]$repo.ok) `
                -FetchOk ([bool]$fetch.ok) `
                -Branch ([string]$repo.branch) `
                -Expected $expected `
                -Clean ([bool]$repo.clean) `
                -DryRun $dryRun
            $pull = [pscustomobject]@{
                ok = $false
                skipped = [bool]$plan.skipped
                planned = [bool]$plan.planned
                reason = $plan.reason
                error = $null
            }
            $statusText = [string]$plan.status
            if ($plan.planned -and -not $dryRun) {
                $pr = Invoke-GimFleetExec -Argv @('git', 'pull', '--ff-only') -WorkingDirectory ([string]$repo.root) -TimeoutSeconds 180
                $pull.planned = $false
                if ($pr.timed_out) {
                    $statusText = 'pull_failed'
                    $pull.error = 'git pull --ff-only timed out'
                } elseif ($pr.exit_code -ne 0) {
                    $statusText = 'pull_failed'
                    $pull.error = Redact-GimFleetText -Text (ConvertTo-GimFleetSingleLine -Text ([string]$pr.stderr))
                    if (-not $pull.error) { $pull.error = ('git pull --ff-only exited with code {0}' -f $pr.exit_code) }
                } else {
                    $pull.ok = $true
                    $pull.skipped = $false
                    $pull.reason = $null
                    $statusText = 'ok'
                }
            }

            if (-not $repo.ok) {
                $docs = New-GimPreflightEmptyDocsIndexes -Error ([string]$repo.error)
            } elseif (-not $dryRun -and -not $fetch.ok) {
                $fetchError = [string]$fetch.error
                if ($fetchError -eq '') { $fetchError = 'fetch failed' }
                $docs = New-GimPreflightEmptyDocsIndexes -Error ('fetch failed: ' + $fetchError)
            } else {
                $docs = Get-GimPreflightDocsIndexes -Root ([string]$repo.root) -ExpectedBranch $expected -SkipGh $skipGh
            }

            $repoReports.Add([pscustomobject]@{
                path = [string]$repo.path
                branch = [string]$repo.branch
                expected_branch = $expected
                clean = [bool]$repo.clean
                fetch = $fetch
                pull = $pull
                status = $statusText
                docs_indexes = $docs
                error = $repo.error
            })
        }

        $authOk = ([bool]$ghAuth.ok -or [bool]$ghAuth.skipped) -and ([bool]$jiraAuth.ok -or [bool]$jiraAuth.skipped)
        $reposOk = $true
        foreach ($rr in $repoReports) {
            if ($rr.status -in @('error', 'fetch_failed', 'pull_failed', 'branch_mismatch')) { $reposOk = $false }
        }
        $allOk = $authOk -and $reposOk

        $report = [pscustomobject]@{
            ok = $allOk
            dry_run = $dryRun
            gh_auth = $ghAuth
            jira_auth = $jiraAuth
            repos = @($repoReports.ToArray())
        }

        if ($jsonMode) {
            Write-GimJson -InputObject $report
            if (-not $allOk) {
                $global:LASTEXITCODE = 1
                exit 1
            }
            return
        }
        foreach ($line in (Format-GimFleetSummary -Report $report)) { Write-Output $line }
        if (-not $allOk) {
            $global:LASTEXITCODE = 1
            exit 1
        }
    }
}
