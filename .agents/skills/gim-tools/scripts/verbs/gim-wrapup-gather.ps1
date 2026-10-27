# gim-wrapup-gather.ps1 - fleet tier (ADR 0008): shim around the stdlib Node
# helper. The PowerShell side owns the allowlisted interface, repo resolution,
# containment, atomic write, log publishing, and the compact JSON envelope.
# The helper owns chain walking, concurrency pools, streaming log capture, and
# normalization.

function New-GimWrapupGatherResultRecord {
    param(
        [bool]$Ok = $false,
        [AllowEmptyString()][string]$Path = '',
        [AllowEmptyString()][string]$Repo = '',
        [AllowNull()]$Summary = $null,
        [AllowEmptyCollection()][string[]]$Warnings = @(),
        [AllowEmptyString()][string]$ErrorMessage = ''
    )
    return [pscustomobject]@{
        ok = $Ok
        path = $Path
        repo = $Repo
        summary = $Summary
        warnings = @($Warnings)
        error = $ErrorMessage
    }
}

function Write-GimWrapupGatherFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [bool]$JsonMode = $false,
        [AllowEmptyString()][string]$Path = '',
        [AllowEmptyString()][string]$Repo = ''
    )
    if ($JsonMode) {
        Write-GimJson (New-GimWrapupGatherResultRecord -Ok $false -Path $Path -Repo $Repo -ErrorMessage $Message)
        $global:LASTEXITCODE = 1
        exit 1
    }
    Write-GimError $Message
}

function Write-GimWrapupGatherResult {
    param(
        [Parameter(Mandatory = $true)]
        $Result,
        [bool]$JsonMode = $false
    )
    if ($JsonMode) {
        Write-GimJson $Result
        return
    }
    $logText = ''
    if ([int]$Result.summary.logs_captured -gt 0) {
        $logText = (', {0} logs' -f [int]$Result.summary.logs_captured)
    }
    Write-Output ('wrote {0} ({1} PRs, {2} failing checks, {3} pending checks, {4} open threads{5})' -f `
        [string]$Result.path, [int]$Result.summary.prs, [int]$Result.summary.checks_failing, `
        [int]$Result.summary.checks_pending, [int]$Result.summary.threads_open, $logText)
    foreach ($warningText in @($Result.warnings)) {
        Write-Output ('warning: ' + [string]$warningText)
    }
}

function Publish-GimWrapupGatherLogs {
    <#
    .SYNOPSIS
        Move helper-produced logs into <gather-dir>/logs after a validated gather.
    .DESCRIPTION
        A marker file identifies logs this verb owns. An owned prior logs dir is
        replaced only at publish time; an unowned dir is merged, never
        recursively deleted. No temp dir means the happy path: remove only an
        owned prior logs dir.
    #>
    param(
        [AllowEmptyString()][string]$TempDir = '',
        [Parameter(Mandatory = $true)][string]$FinalDir,
        [Parameter(Mandatory = $true)][string]$MarkerPath
    )
    $hasTemp = ($TempDir -ne '' -and (Test-Path -LiteralPath $TempDir -PathType Container))
    $hasFinal = (Test-Path -LiteralPath $FinalDir -PathType Container)
    $owned = ($hasFinal -and (Test-Path -LiteralPath $MarkerPath -PathType Leaf))
    if (-not $hasTemp) {
        if ($owned) { [System.IO.Directory]::Delete($FinalDir, $true) }
        return
    }
    $created = $false
    if ($owned) {
        [System.IO.Directory]::Delete($FinalDir, $true)
        $hasFinal = $false
    }
    if (-not $hasFinal) {
        [System.IO.Directory]::Move($TempDir, $FinalDir)
        $created = $true
    } else {
        # Unowned dir: merge files but do not claim ownership.
        foreach ($file in [System.IO.Directory]::GetFiles($TempDir)) {
            $dest = Join-Path $FinalDir ([System.IO.Path]::GetFileName($file))
            if (Test-Path -LiteralPath $dest -PathType Leaf) { [System.IO.File]::Delete($dest) }
            [System.IO.File]::Move($file, $dest)
        }
        [System.IO.Directory]::Delete($TempDir, $true)
    }
    if ($created) {
        [System.IO.File]::WriteAllText($MarkerPath, 'gim-wrapup-gather', (New-Object System.Text.UTF8Encoding($false)))
    }
}

function Merge-GimWrapupResponses {
    param(
        [Parameter(Mandatory = $true)]$New,
        [AllowNull()]$Existing
    )
    if ($null -eq $Existing) { return $New }
    $existingByPr = @{}
    foreach ($pr in @($Existing.prs)) {
        $key = [string]$pr.pr
        if ($key -ne '') { $existingByPr[$key] = $pr }
    }
    $mergedPrs = New-Object System.Collections.Generic.List[object]
    $newPrKeys = @{}
    foreach ($newPr in @($New.prs)) {
        $key = [string]$newPr.pr
        $newPrKeys[$key] = $true
        $oldPr = if ($existingByPr.ContainsKey($key)) { $existingByPr[$key] } else { $null }
        $oldThreads = @{}
        if ($null -ne $oldPr) {
            foreach ($thread in @($oldPr.threads)) {
                $oldThreads[[string]$thread.thread_id] = $thread
            }
        }
        $threads = New-Object System.Collections.Generic.List[object]
        foreach ($newThread in @($newPr.threads)) {
            $id = [string]$newThread.thread_id
            if ($oldThreads.ContainsKey($id)) {
                [void]$threads.Add([pscustomobject]@{
                    thread_id = $id
                    resolve = [bool]$oldThreads[$id].resolve
                    response = [string]$oldThreads[$id].response
                })
            } else {
                [void]$threads.Add([pscustomobject]@{
                    thread_id = $id
                    resolve = [bool]$newThread.resolve
                    response = [string]$newThread.response
                })
            }
        }
        $comments = New-Object System.Collections.Generic.List[string]
        if ($null -ne $oldPr) {
            foreach ($comment in @($oldPr.comments)) {
                if ($null -ne $comment) { [void]$comments.Add([string]$comment) }
            }
        }
        [void]$mergedPrs.Add([pscustomobject]@{
            pr = [int]$newPr.pr
            threads = @($threads.ToArray())
            comments = @($comments.ToArray())
        })
    }
    foreach ($oldPr in @($Existing.prs)) {
        $key = [string]$oldPr.pr
        if ($key -eq '' -or $newPrKeys.ContainsKey($key)) { continue }
        [void]$mergedPrs.Add($oldPr)
    }
    return [pscustomobject]@{
        repo = [string]$New.repo
        generated_at = ConvertTo-GimWrapupIsoTimestamp -Value $New.generated_at
        prs = @($mergedPrs.ToArray())
    }
}

function gim-wrapup-gather {
    <#
    .SYNOPSIS
        Fetch PR checks/comments/threads and write the normalized wrapup gather JSON.
    .DESCRIPTION
        Signature:
          gim-wrapup-gather --repo <name|path>
                            (--chain <pr> | --pr <n>... | --branch <branch>...)
                            [--out <path>] [--parallel <n>] [--log-parallel <n>]
                            [--json]

        --chain is the primary mode; --pr/--branch are explicit fallbacks.
        The PowerShell shim resolves the repo and output containment, then
        spawns `node helpers/gim-wrapup-gather.mjs`. `GIM_WRAPUP_GATHER_HELPER`
        is a test-only override constrained to the module helpers dir. The
        helper owns chain
        walking, independent PR/log concurrency pools (default 4/2), thread
        mapping (`reviews[].comments[]` -> threads with `replies[]`), streaming
        failing-check `gh run view` logs (50 MB/file, 200 MB total), and the
        600 s fetch budget. `log_file` is relative to the gather directory;
        `log_source` is `job` or `run`.

        Writes gather.json (1-space, nulls omitted, pass/skip checks reduced)
        and the authored responses.json shell beside it. Responses merge by
        thread id on re-gather. --json prints { ok, path, repo, summary,
        warnings, error } only; read the files for full content. No `gh api`.
    .PARAMETER Raw
        '--flag value' options (see DESCRIPTION).
    .EXAMPLE
        gim-wrapup-gather --repo GIMmos --chain 40 --json
    .EXAMPLE
        gim-wrapup-gather --repo GIMmos --pr 40 --out .agent-scratch/wrapup/pr40/gather.json --json
    .EXAMPLE
        gim-wrapup-gather --repo GIMmos --branch feature/a --branch feature/b --parallel 6 --log-parallel 3 --json
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
                '--repo'        = 'Repo'
                '--chain'       = 'Chain'
                '--pr'          = 'Pr'
                '--branch'      = 'Branch'
                '--out'         = 'Out'
                '--parallel'    = 'Parallel'
                '--log-parallel' = 'LogParallel'
            } `
            -SwitchFlags @{ '--json' = 'Json' }
        $jsonMode = $parsed.ContainsKey('Json')
        if ($parsed.Positional.Count -gt 0) {
            Write-GimWrapupGatherFailure -Message ("unexpected arguments: '{0}' (gim-wrapup-gather takes --repo, --chain or --pr or --branch, optional --out, --parallel, --log-parallel, --json)" -f ($parsed.Positional -join ' ')) -JsonMode $jsonMode
        }
        if (-not $parsed.ContainsKey('Repo')) {
            Write-GimWrapupGatherFailure -Message 'missing --repo: give a repository name or path' -JsonMode $jsonMode
        }
        $repoArg = ([string]$parsed['Repo']).Trim()
        if ($repoArg -eq '') { Write-GimWrapupGatherFailure -Message '--repo must not be empty' -JsonMode $jsonMode }

        $hasChain = $parsed.ContainsKey('Chain')
        $hasPr = $parsed.ContainsKey('Pr')
        $hasBranch = $parsed.ContainsKey('Branch')
        $modeCount = 0
        if ($hasChain) { $modeCount++ }
        if ($hasPr) { $modeCount++ }
        if ($hasBranch) { $modeCount++ }
        if ($modeCount -gt 1) {
            Write-GimWrapupGatherFailure -Message 'two PR sources given: use exactly one of --chain, --pr, or --branch' -JsonMode $jsonMode
        }
        if ($modeCount -eq 0) {
            Write-GimWrapupGatherFailure -Message 'missing PR source: give --chain <pr>, --pr <n>, or --branch <branch>' -JsonMode $jsonMode
        }
        $chainNumber = 0
        if ($hasChain) {
            $chainText = ([string]$parsed['Chain']).Trim()
            if (-not [int]::TryParse($chainText, [ref]$chainNumber) -or $chainNumber -lt 1) {
                Write-GimWrapupGatherFailure -Message ('--chain must be a positive PR number; got ''{0}''' -f $chainText) -JsonMode $jsonMode
            }
        }
        $prs = New-Object System.Collections.Generic.List[int]
        if ($hasPr) {
            foreach ($entry in @($parsed['Pr'])) {
                $text = ([string]$entry).Trim()
                $number = 0
                if (-not [int]::TryParse($text, [ref]$number) -or $number -lt 1) {
                    Write-GimWrapupGatherFailure -Message ('--pr must be a positive integer; got ''{0}''' -f $text) -JsonMode $jsonMode
                }
                [void]$prs.Add($number)
            }
        }
        $branches = New-Object System.Collections.Generic.List[string]
        if ($hasBranch) {
            foreach ($entry in @($parsed['Branch'])) {
                $text = ([string]$entry).Trim()
                if ($text -eq '') { Write-GimWrapupGatherFailure -Message '--branch must not be empty' -JsonMode $jsonMode }
                if ($text.StartsWith('-')) {
                    Write-GimWrapupGatherFailure -Message ('--branch ''{0}'' starts with ''-''; required input: a branch name' -f $text) -JsonMode $jsonMode
                }
                [void]$branches.Add($text)
            }
        }
        $parallel = 4
        if ($parsed.ContainsKey('Parallel')) {
            $text = ([string]$parsed['Parallel']).Trim()
            if (-not [int]::TryParse($text, [ref]$parallel) -or $parallel -lt 1) {
                Write-GimWrapupGatherFailure -Message ('--parallel must be a positive integer; got ''{0}''' -f $text) -JsonMode $jsonMode
            }
        }
        $logParallel = 2
        if ($parsed.ContainsKey('LogParallel')) {
            $text = ([string]$parsed['LogParallel']).Trim()
            if (-not [int]::TryParse($text, [ref]$logParallel) -or $logParallel -lt 1) {
                Write-GimWrapupGatherFailure -Message ('--log-parallel must be a positive integer; got ''{0}''' -f $text) -JsonMode $jsonMode
            }
        }
        $outArg = ''
        if ($parsed.ContainsKey('Out')) {
            $outArg = ([string]$parsed['Out']).Trim()
            if ($outArg -eq '') { Write-GimWrapupGatherFailure -Message '--out must not be empty' -JsonMode $jsonMode }
        }
    }
    end {
        try {
            $repoRoot = Resolve-GimFleetRepo -Requested $repoArg
        } catch {
            Write-GimWrapupGatherFailure -Message ([string]$_.Exception.Message) -JsonMode $jsonMode
            return
        }
        $repoName = Split-Path -Leaf $repoRoot
        $mode = if ($hasChain) { 'chain' } elseif ($hasPr) { 'pr' } else { 'branch' }
        $slug = if ($hasBranch) {
            (([string]$branches[0]).Trim() -replace '[^A-Za-z0-9._-]+', '-').Trim('-')
        } elseif ($hasChain) { 'chain-' + [string]$chainNumber }
        else { 'pr-' + [string]$prs[0] }
        if ($slug -eq '') { $slug = 'gather' }
        if ($outArg -ne '') {
            if ([System.IO.Path]::IsPathRooted($outArg)) { $outPath = $outArg }
            else { $outPath = Join-Path $repoRoot $outArg }
        } else {
            $outPath = Join-Path $repoRoot (Join-Path '.agent-scratch' (Join-Path 'wrapup' (Join-Path $slug 'gather.json')))
        }
        try {
            $outPath = Resolve-GimWritePath -Path $outPath -Roots @($repoRoot) -RootLabel ("repo '{0}'" -f $repoName)
        } catch {
            Write-GimWrapupGatherFailure -Message ([string]$_.Exception.Message) -JsonMode $jsonMode
            return
        }
        $gatherDir = [System.IO.Path]::GetDirectoryName($outPath)
        $tempLogsDir = Join-Path $gatherDir ('.gather-logs-' + [guid]::NewGuid().ToString('N'))
        $helperPath = [string]$env:GIM_WRAPUP_GATHER_HELPER
        if ($helperPath -eq '') {
            $helperPath = Join-Path $script:GimModuleRoot (Join-Path 'helpers' 'gim-wrapup-gather.mjs')
        }
        if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
            Write-GimWrapupGatherFailure -Message ("gim-wrapup-gather helper not found: '{0}'; required input: restore the Node helper" -f $helperPath) -JsonMode $jsonMode
            return
        }
        # Test-only seam: the override may only point inside the module helpers dir.
        $helperReal = Get-GimRealPath -Path $helperPath
        $moduleHelpers = Get-GimRealPath -Path (Join-Path $script:GimModuleRoot 'helpers')
        if (-not (Test-GimContainedPath -RealPath $helperReal -RootRealPaths @($moduleHelpers))) {
            Write-GimWrapupGatherFailure -Message 'GIM_WRAPUP_GATHER_HELPER must point inside the module helpers dir' -JsonMode $jsonMode
            return
        }
        $helperArgs = @(
            'node', $helperPath,
            '--repo-root', $repoRoot,
            '--mode', $mode,
            '--parallel', [string]$parallel,
            '--log-parallel', [string]$logParallel,
            '--logs-dir', $tempLogsDir
        )
        if ($hasChain) { $helperArgs += @('--chain', [string]$chainNumber) }
        elseif ($hasPr) { foreach ($number in $prs) { $helperArgs += @('--pr', [string]$number) } }
        else { foreach ($branch in $branches) { $helperArgs += @('--branch', $branch) } }

        $run = Invoke-GimFleetExec -Argv $helperArgs -WorkingDirectory $repoRoot -TimeoutSeconds 660 -MaxOutputBytes (50 * 1024 * 1024)
        try {
            if ($run.timed_out -or $run.exit_code -ne 0) {
                $text = ConvertTo-GimFleetSingleLine -Text (Redact-GimFleetText -Text ([string]$run.stderr + "`n" + [string]$run.stdout))
                Write-GimWrapupGatherFailure -Message ('gim-wrapup-gather helper failed: ' + $text) -JsonMode $jsonMode
                return
            }
            try {
                $helperResult = [string]$run.stdout | ConvertFrom-Json
            } catch {
                if (([string]$run.stdout).TrimEnd() -match '(?m)# gim: output truncated[^\r\n]*$') {
                    Write-GimWrapupGatherFailure -Message 'gim-wrapup-gather helper output was truncated; required input: reduce gather size' -JsonMode $jsonMode
                } else {
                    Write-GimWrapupGatherFailure -Message 'gim-wrapup-gather helper returned unparseable JSON' -JsonMode $jsonMode
                }
                return
            }
            $document = $helperResult.document
            $jsonText = [string]$helperResult.documentJson
            if ($null -eq $document -or $jsonText -eq '' -or $null -eq $document.PSObject.Properties['prs'] -or
                $null -eq $document.PSObject.Properties['summary'] -or $null -eq $document.PSObject.Properties['input']) {
                Write-GimWrapupGatherFailure -Message 'gim-wrapup-gather helper returned an invalid document' -JsonMode $jsonMode
                return
            }
            try {
                $roundTrip = $jsonText | ConvertFrom-Json
            } catch {
                Write-GimWrapupGatherFailure -Message 'gim-wrapup-gather document failed JSON round-trip validation' -JsonMode $jsonMode
                return
            }
            if ($null -eq $roundTrip -or $null -eq $roundTrip.PSObject.Properties['prs']) {
                Write-GimWrapupGatherFailure -Message 'gim-wrapup-gather document failed JSON round-trip validation' -JsonMode $jsonMode
                return
            }
            $responsesPath = Join-Path $gatherDir 'responses.json'
            $existingResponses = $null
            if (Test-Path -LiteralPath $responsesPath -PathType Leaf) {
                try {
                    $existingResponses = [System.IO.File]::ReadAllText($responsesPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
                } catch {
                    Write-GimWrapupGatherFailure -Message ("responses.json is unparseable: '{0}'" -f $responsesPath) -JsonMode $jsonMode
                    return
                }
            }
            $responsesJson = $null
            $partialGather = (($document.warnings -join ' ') -match '(?i)partial|fetch budget')
            if (-not ($partialGather -and @($helperResult.responses.prs).Count -eq 0)) {
                try {
                    $mergedResponses = Merge-GimWrapupResponses -New $helperResult.responses -Existing $existingResponses
                    $responsesJson = ConvertTo-Json -InputObject $mergedResponses -Depth 20
                    $responsesJson = $responsesJson -replace "`r`n", "`n"
                    $responsesRoundTrip = $responsesJson | ConvertFrom-Json
                    if ($null -eq $responsesRoundTrip -or $null -eq $responsesRoundTrip.PSObject.Properties['prs']) {
                        throw 'missing prs after round-trip'
                    }
                } catch {
                    Write-GimWrapupGatherFailure -Message ('responses.json failed JSON round-trip validation: ' + [string]$_.Exception.Message) -JsonMode $jsonMode
                    return
                }
            }
            Publish-GimWrapupGatherLogs -TempDir $tempLogsDir -FinalDir (Join-Path $gatherDir 'logs') -MarkerPath (Join-Path (Join-Path $gatherDir 'logs') '.gim-wrapup-gather.logs')
            $writeArtifact = {
                param($Path, $Text)
                $parent = [System.IO.Path]::GetDirectoryName($Path)
                if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
                    [void][System.IO.Directory]::CreateDirectory($parent)
                }
                $tmp = Join-Path $parent ('.' + [guid]::NewGuid().ToString('N') + '.tmp')
                try {
                    [System.IO.File]::WriteAllText($tmp, $Text, (New-Object System.Text.UTF8Encoding($false)))
                    Move-Item -LiteralPath $tmp -Destination $Path -Force
                } catch {
                    if (Test-Path -LiteralPath $tmp -PathType Leaf) {
                        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
                    }
                    throw
                }
            }
            try {
                if ($null -ne $responsesJson) { & $writeArtifact $responsesPath $responsesJson }
                & $writeArtifact $outPath $jsonText
            } catch {
                Write-GimWrapupGatherFailure -Message ('cannot write gather artifacts: ' + [string]$_.Exception.Message) -JsonMode $jsonMode
                return
            }
            $repo = [string]$document.repo
            Write-GimWrapupGatherResult -JsonMode $jsonMode -Result (New-GimWrapupGatherResultRecord -Ok $true -Path $outPath -Repo $repo `
                -Summary $document.summary -Warnings @($document.warnings))
        } finally {
            if (Test-Path -LiteralPath $tempLogsDir -PathType Container) {
                Remove-Item -LiteralPath $tempLogsDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
