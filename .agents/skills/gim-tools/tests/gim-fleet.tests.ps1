# gim-fleet.tests.ps1 - Pester 6 tests for the fleet tier (ADR 0008):
# redaction, docs-index parsing (fixture file lists), the pull decision,
# repo-name resolution (fixture git roots), argument errors, the JSON report
# shape, dry-run behavior, index failures, open-PR parse/truncation failures,
# and allowlist placement. No live fetch/pull against this repository:
# integration tests use --dry-run on a throwaway git repo under
# .agent-scratch/.
# Pester 6 syntax: Should -Be / -Throw / -Match / -BeTrue / -BeFalse.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

Describe 'fleet redaction' {
    InModuleScope GIMTools {
        It 'redacts email addresses' {
            $out = Redact-GimFleetText 'contact med.anis.messaoudi@gmail.com for access'
            $out | Should -Match '<redacted-email>'
            $out | Should -Not -Match 'gmail\.com'
            $out | Should -Not -Match 'messaoudi'
        }
        It 'redacts accountId assignments and bare account ids' {
            $out = Redact-GimFleetText 'accountId=5b10ac8d82e05b22cc7d4ef5 and 5b10ac8d82e05b22cc7d4ef5'
            $out | Should -Not -Match '5b10ac8d82e05b22cc7d4ef5'
            $out | Should -Match '<redacted-account>'
        }
        It 'keeps ordinary git shas' {
            $sha = 'a1868820123456789abcdef0123456789abcdef0'
            $out = Redact-GimFleetText ('commit ' + $sha)
            $out | Should -Match $sha
        }
        It 'collapses multi-line failure text to one bounded line' {
            $out = ConvertTo-GimFleetSingleLine -Text "line one`r`nline two" -MaxLength 200
            $out | Should -Be 'line one line two'
        }
    }
}

Describe 'fleet exec argv tokens' {
    InModuleScope GIMTools {
        It 'quotes an empty argv token as two double quotes' {
            Quote-Arg -Argument '' | Should -Be '""'
        }
        It 'passes an empty argv token through to the child process' {
            # `gh pr create --body ''` is a real empty token; the mandatory
            # string[] binder used to reject the whole array. git
            # rev-parse --sq-quote echoes each argv token, so an empty token
            # shows up as '' in the child's stdout.
            $root = Get-GimTestRepoRoot
            $run = Invoke-GimFleetExec -Argv @('git', 'rev-parse', '--sq-quote', 'a', '', 'b') -WorkingDirectory $root -TimeoutSeconds 60
            $run.exit_code | Should -Be 0
            ([string]$run.stdout).Trim() | Should -Be "'a' '' 'b'"
        }
        It 'passes an empty argv token through the batch runner' {
            $root = Get-GimTestRepoRoot
            $results = @(Invoke-GimFleetBatch -Commands @(
                @{ argv = @('git', 'rev-parse', '--sq-quote', ''); working_directory = $root; timeout_seconds = 60 }
            ))
            $results.Count | Should -Be 1
            $results[0].exit_code | Should -Be 0
            ([string]$results[0].stdout).Trim() | Should -Be "''"
        }
        It 'decodes child stdout and stderr as UTF-8' {
            $root = Get-GimTestRepoRoot
            $psi = Get-GimFleetStartInfo -Argv @('git', '--version') -WorkingDirectory $root
            $psi.StandardOutputEncoding.WebName | Should -Be 'utf-8'
            $psi.StandardErrorEncoding.WebName | Should -Be 'utf-8'
        }
        It 'raises the captured output cap when requested' {
            $root = Get-GimTestRepoRoot
            $pwshPath = (Get-Process -Id $PID).Path
            $scriptText = "[Console]::Out.Write(('x' * 300000))"
            $run = Invoke-GimFleetExec -Argv @($pwshPath, '-NoProfile', '-Command', $scriptText) -WorkingDirectory $root -TimeoutSeconds 60 -MaxOutputBytes 400000
            $run.exit_code | Should -Be 0
            ([string]$run.stdout).Length | Should -Be 300000
        }
    }
}

Describe 'fleet docs index' {
    InModuleScope GIMTools {
        It 'takes the max across local, remote, and open PR files' {
            $idx = Get-GimFleetDocsIndex -Family 'plans' `
                -LocalFiles @('docs/plans/0003-a.plan.md', 'docs/plans/0010-b.plan.md', 'docs/plans/agents/0099-AGENTS-x.plan.md') `
                -RemoteFiles @('docs/plans/0011-c.plan.md') `
                -OpenPrFiles @('docs/plans/0012-d.plan.md', 'docs/plans/agents/0100-AGENTS-y.plan.md', '.agents/ignored.md')
            $idx.ok | Should -BeTrue
            $idx.local | Should -Be 10
            $idx.remote | Should -Be 11
            $idx.open_pr | Should -Be 12
            $idx.max | Should -Be 12
            $idx.next | Should -Be 13
            $idx.error | Should -BeNullOrEmpty
        }
        It 'returns ok, max 0, and next 1 for an absent family' {
            $idx = Get-GimFleetDocsIndex -Family 'guides' -LocalFiles @('docs/plans/0010-x.plan.md') -RemoteFiles @() -OpenPrFiles @()
            $idx.ok | Should -BeTrue
            $idx.local | Should -Be 0
            $idx.remote | Should -Be 0
            $idx.open_pr | Should -Be 0
            $idx.max | Should -Be 0
            $idx.next | Should -Be 1
            $idx.error | Should -BeNullOrEmpty
        }
        It 'invalidates remote, open_pr, max, and next when a source fails' {
            $idx = Get-GimFleetDocsIndex -Family 'rfcs' `
                -LocalFiles @('docs/rfcs/0006-a.md') `
                -RemoteFiles @('docs/rfcs/0008-b.md') `
                -OpenPrFiles @('docs/rfcs/0010-c.md') `
                -Error 'remote: git ls-tree failed' `
                -Failed $true
            $idx.ok | Should -BeFalse
            $idx.local | Should -Be 6
            $idx.remote | Should -BeNullOrEmpty
            $idx.open_pr | Should -BeNullOrEmpty
            $idx.max | Should -BeNullOrEmpty
            $idx.next | Should -BeNullOrEmpty
            $idx.error | Should -Be 'remote: git ls-tree failed'
        }
        It 'excludes agent-only paths' {
            Test-GimFleetExcludedDocPath -Path '.agents/skills/gim-tools/SKILL.md' | Should -BeTrue
            Test-GimFleetExcludedDocPath -Path '.cursor/agents/gim-builder.md' | Should -BeTrue
            Test-GimFleetExcludedDocPath -Path 'docs/plans/agents/0007-AGENTS-x.plan.md' | Should -BeTrue
            Test-GimFleetExcludedDocPath -Path 'docs/adr/agents/0009-AGENTS-x.md' | Should -BeTrue
            Test-GimFleetExcludedDocPath -Path 'docs/plans/0010-AGENTS-x.plan.md' | Should -BeTrue
            Test-GimFleetExcludedDocPath -Path 'docs/plans/0010-product.plan.md' | Should -BeFalse
        }
        It 'parses the path-only gh pr list output' {
            $paths = @(Get-GimFleetOpenPrFilePaths -Text "docs/plans/0010-klic-network-payload.plan.md`ndocs/rfcs/0008-strictness-tiers-and-observability.md`n")
            $paths | Should -Contain 'docs/plans/0010-klic-network-payload.plan.md'
            $paths | Should -Contain 'docs/rfcs/0008-strictness-tiers-and-observability.md'
            $paths.Count | Should -Be 2
        }
        It 'still flattens structured JSON file objects' {
            $prs = @(
                [pscustomobject]@{
                    number = 132
                    files = @(
                        [pscustomobject]@{ path = 'docs/plans/0010-klic-network-payload.plan.md'; additions = 1 },
                        'docs/rfcs/0008-strictness-tiers-and-observability.md'
                    )
                }
            ) | ConvertTo-Json -Depth 6
            $paths = @(Get-GimFleetOpenPrFilePaths -Text $prs)
            $paths | Should -Contain 'docs/plans/0010-klic-network-payload.plan.md'
            $paths | Should -Contain 'docs/rfcs/0008-strictness-tiers-and-observability.md'
            $paths.Count | Should -Be 2
        }
        It 'throws on unparseable JSON so the caller can fail the index' {
            { Get-GimFleetOpenPrFilePaths -Text '[{"files": ' } | Should -Throw '*unparseable JSON*'
        }
        It 'throws on truncated runner output so the caller can fail the index' {
            { Get-GimFleetOpenPrFilePaths -Text "docs/plans/0001-a.plan.md`n# gim: output truncated (bytes 262144)" } |
                Should -Throw '*truncated*'
        }
    }
}

Describe 'gim-preflight pull decision' {
    InModuleScope GIMTools {
        It 'plans the ff-only pull when clean and on the expected branch' {
            $plan = Get-GimPreflightPullPlan -RepoOk $true -FetchOk $true -Branch 'dev' -Expected 'dev' -Clean $true
            $plan.planned | Should -BeTrue
            $plan.skipped | Should -BeFalse
            $plan.status | Should -Be 'ok'
        }
        It 'skips a dirty worktree and never pulls' {
            $plan = Get-GimPreflightPullPlan -RepoOk $true -FetchOk $true -Branch 'dev' -Expected 'dev' -Clean $false
            $plan.planned | Should -BeFalse
            $plan.skipped | Should -BeTrue
            $plan.reason | Should -Be 'dirty worktree'
            $plan.status | Should -Be 'dirty'
        }
        It 'skips an off-branch repo and reports the mismatch' {
            $plan = Get-GimPreflightPullPlan -RepoOk $true -FetchOk $true -Branch 'main' -Expected 'dev' -Clean $true
            $plan.planned | Should -BeFalse
            $plan.status | Should -Be 'branch_mismatch'
            $plan.reason | Should -Be 'on main, expected dev'
        }
        It 'skips the pull when the fetch failed' {
            $plan = Get-GimPreflightPullPlan -RepoOk $true -FetchOk $false -Branch 'dev' -Expected 'dev' -Clean $true
            $plan.planned | Should -BeFalse
            $plan.status | Should -Be 'fetch_failed'
        }
        It 'plans without executing in dry-run mode' {
            $plan = Get-GimPreflightPullPlan -RepoOk $true -FetchOk $false -Branch 'dev' -Expected 'dev' -Clean $true -DryRun $true
            $plan.planned | Should -BeTrue
            $plan.skipped | Should -BeTrue
            $plan.status | Should -Be 'dry_run'
            $plan.reason | Should -Match 'would pull'
        }
    }
}

Describe 'gim-preflight status cleanliness' {
    InModuleScope GIMTools {
        It 'ignores untracked .agent-scratch evidence' {
            (Test-GimPreflightStatusClean -Text "?? .agent-scratch/`n?? .agent-scratch/pr-files/x/files.txt") |
                Should -BeTrue
        }
        It 'keeps other tracked and untracked changes dirty' {
            (Test-GimPreflightStatusClean -Text '?? notes.txt') | Should -BeFalse
            (Test-GimPreflightStatusClean -Text ' M src/a.cs') | Should -BeFalse
            (Test-GimPreflightStatusClean -Text '?? .agent-scratch-other/x') | Should -BeFalse
        }
    }
}

Describe 'gim-preflight arguments' {
    It 'errors without --repo' {
        { gim-preflight } | Should -Throw
    }
    It 'errors when --repo has no value' {
        { gim-preflight --repo } | Should -Throw
    }
    It 'errors on an empty --branch' {
        { gim-preflight --repo . --branch '' } | Should -Throw
    }
    It 'errors on unexpected positional arguments' {
        { gim-preflight --repo . nonsense } | Should -Throw
    }
}

Describe 'fleet repo name resolution' {
    InModuleScope GIMTools {
        BeforeAll {
            $repoRoot = Get-GimTestRepoRoot
            $scratch = Join-Path $repoRoot ('.agent-scratch\gim-fleet-repos-' + [guid]::NewGuid().ToString('N'))
            $fixture = Join-Path $scratch 'workspace'
            $baseRoot = Join-Path $fixture 'GIMmos'
            New-Item -ItemType Directory -Path (Join-Path $baseRoot '.git') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $fixture 'CoreWebApi') -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $fixture 'CoreWebApi\.git'), 'gitdir: ../.fake/gitdir', (New-Object System.Text.UTF8Encoding($false)))
            New-Item -ItemType Directory -Path (Join-Path $fixture 'MiXeDCase\.git') -Force | Out-Null
        }
        AfterAll {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        }

        It 'resolves a simple repo name under the parent of the base root' {
            $resolved = Resolve-GimFleetRepo -Requested 'GIMmos' -BaseRoot $baseRoot
            $resolved | Should -Be (Get-GimRealPath -Path $baseRoot)
        }
        It 'resolves a simple repo name case-insensitively' {
            (Resolve-GimFleetRepo -Requested 'gimmos' -BaseRoot $baseRoot) | Should -Be (Get-GimRealPath -Path $baseRoot)
        }
        It 'resolves a sibling repo name' {
            (Resolve-GimFleetRepo -Requested 'CoreWebApi' -BaseRoot $baseRoot) |
                Should -Be (Get-GimRealPath -Path (Join-Path $fixture 'CoreWebApi'))
        }
        It 'resolves a case-mismatched directory name' {
            (Resolve-GimFleetRepo -Requested 'mixedcase' -BaseRoot $baseRoot) |
                Should -Be (Get-GimRealPath -Path (Join-Path $fixture 'MiXeDCase'))
        }
        It 'uses an absolute path as given' {
            $absolute = Join-Path $fixture 'CoreWebApi'
            (Resolve-GimFleetRepo -Requested $absolute -BaseRoot $baseRoot) |
                Should -Be (Get-GimRealPath -Path $absolute)
        }
        It 'resolves a relative path with separators against the base root' {
            (Resolve-GimFleetRepo -Requested '..\CoreWebApi' -BaseRoot $baseRoot) |
                Should -Be (Get-GimRealPath -Path (Join-Path $fixture 'CoreWebApi'))
        }
        It 'errors for a missing name and lists the available directories' {
            { Resolve-GimFleetRepo -Requested 'NoSuchRepo' -BaseRoot $baseRoot } |
                Should -Throw '*available directories*CoreWebApi, GIMmos*'
        }
        It 'errors on a case-only ambiguous match and lists the matches' {
            Mock Get-GimFleetSiblingDirectoryNames { @('GIMmos', 'gimmos') }
            Mock Test-GimFleetGitRoot { $true }
            { Resolve-GimFleetRepo -Requested 'GIMMOS' -BaseRoot $baseRoot } |
                Should -Throw '*ambiguous*GIMmos, gimmos*'
        }
        It 'leaves the caller working directory unchanged' {
            $before = (Get-Location).Path
            [void](Resolve-GimFleetRepo -Requested 'GIMmos' -BaseRoot $baseRoot)
            (Get-Location).Path | Should -Be $before
        }
    }
}

Describe 'gim-preflight report' {
    InModuleScope GIMTools {
        BeforeAll {
            $repoRoot = Get-GimTestRepoRoot
            $scratch = Join-Path $repoRoot ('.agent-scratch\gim-fleet-' + [guid]::NewGuid().ToString('N'))
        }
        BeforeEach {
            New-Item -ItemType Directory -Path $scratch -Force | Out-Null
            $init = Invoke-GimFleetExec -Argv @('git', 'init') -WorkingDirectory $scratch -TimeoutSeconds 60
            if ($init.exit_code -ne 0) { throw ('git init failed: ' + $init.stderr) }
            $commit = Invoke-GimFleetExec -Argv @(
                'git', '-c', 'user.name=GIM Test', '-c', 'user.email=gim-test@example.invalid',
                'commit', '--allow-empty', '-m', 'init'
            ) -WorkingDirectory $scratch -TimeoutSeconds 60
            if ($commit.exit_code -ne 0) { throw ('git commit failed: ' + $commit.stderr) }
            [System.IO.File]::WriteAllText((Join-Path $scratch 'dirty.txt'), 'x', (New-Object System.Text.UTF8Encoding($false)))
            $branchResult = Invoke-GimFleetExec -Argv @('git', 'rev-parse', '--abbrev-ref', 'HEAD') -WorkingDirectory $scratch -TimeoutSeconds 30
            if ($branchResult.exit_code -ne 0) { throw ('git branch lookup failed: ' + $branchResult.stderr) }
            $script:expectedBranch = ([string]$branchResult.stdout).Trim()
        }
        AfterEach {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        }

        It 'emits the dry-run JSON report shape with skipped auth and a dirty repo' {
            $raw = gim-preflight --repo $scratch --branch $script:expectedBranch --dry-run --skip-gh-auth --skip-jira-auth --json
            $report = $raw | ConvertFrom-Json
            $report.PSObject.Properties.Name | Should -Contain 'ok'
            $report.PSObject.Properties.Name | Should -Contain 'dry_run'
            $report.PSObject.Properties.Name | Should -Contain 'gh_auth'
            $report.PSObject.Properties.Name | Should -Contain 'jira_auth'
            $report.PSObject.Properties.Name | Should -Contain 'repos'
            $report.ok | Should -BeTrue
            $report.dry_run | Should -BeTrue
            $report.gh_auth.skipped | Should -BeTrue
            $report.jira_auth.skipped | Should -BeTrue
            $report.gh_auth.ok | Should -BeNullOrEmpty
            $report.jira_auth.ok | Should -BeNullOrEmpty
            @($report.repos).Count | Should -Be 1
        }
        It 'reports branch, expected branch, clean=false, planned fetch, and a skipped pull' {
            $raw = gim-preflight --repo $scratch --branch $script:expectedBranch --dry-run --skip-gh-auth --skip-jira-auth --json
            $repo = @(($raw | ConvertFrom-Json).repos)[0]
            $repo.branch | Should -Be $script:expectedBranch
            $repo.expected_branch | Should -Be $script:expectedBranch
            $repo.clean | Should -BeFalse
            $repo.fetch.planned | Should -BeTrue
            $repo.fetch.skipped | Should -BeTrue
            $repo.fetch.ok | Should -BeNullOrEmpty
            $repo.pull.planned | Should -BeFalse
            $repo.pull.skipped | Should -BeTrue
            $repo.pull.reason | Should -Be 'dirty worktree'
            $repo.status | Should -Be 'dirty'
            Test-Path -LiteralPath (Join-Path $scratch 'dirty.txt') | Should -BeTrue
        }
        It 'marks every docs family failed with next=null when the remote/open-PR sources fail' {
            $raw = gim-preflight --repo $scratch --branch $script:expectedBranch --dry-run --skip-gh-auth --skip-jira-auth --json
            $repo = @(($raw | ConvertFrom-Json).repos)[0]
            foreach ($family in @('plans', 'adr', 'rfcs', 'guides')) {
                $idx = $repo.docs_indexes.PSObject.Properties[$family].Value
                $idx.PSObject.Properties.Name | Should -Contain 'ok'
                $idx.PSObject.Properties.Name | Should -Contain 'local'
                $idx.PSObject.Properties.Name | Should -Contain 'remote'
                $idx.PSObject.Properties.Name | Should -Contain 'open_pr'
                $idx.PSObject.Properties.Name | Should -Contain 'max'
                $idx.PSObject.Properties.Name | Should -Contain 'next'
                $idx.ok | Should -BeFalse
                $idx.remote | Should -BeNullOrEmpty
                $idx.open_pr | Should -BeNullOrEmpty
                $idx.max | Should -BeNullOrEmpty
                $idx.next | Should -BeNullOrEmpty
                $idx.error | Should -Not -BeNullOrEmpty
            }
        }
    }
}

Describe 'gim-pr argument errors' {
    It 'errors without arguments' {
        { gim-pr } | Should -Throw
    }
    It 'errors without --repo' {
        { gim-pr --files-file x --message m --title t --target dev --pr-body b --dry-run } | Should -Throw
    }
    It 'errors when both file sources are given' {
        { gim-pr --repo . --files-file x --file a --message m --title t --target dev --pr-body b --dry-run } | Should -Throw
    }
    It 'errors when no file source is given' {
        { gim-pr --repo . --message m --title t --target dev --pr-body b --dry-run } | Should -Throw
    }
    It 'errors without --message' {
        { gim-pr --repo . --file a --title t --target dev --pr-body b --dry-run } | Should -Throw
    }
    It 'errors without --title' {
        { gim-pr --repo . --file a --message m --target dev --pr-body b --dry-run } | Should -Throw
    }
    It 'errors without --target' {
        { gim-pr --repo . --file a --message m --title t --pr-body b --dry-run } | Should -Throw
    }
    It 'errors without --pr-body unless --draft' {
        { gim-pr --repo . --file a --message m --title t --target dev --dry-run } | Should -Throw
    }
    It 'errors when --delete-pr-body has no --pr-body' {
        { gim-pr --repo . --file a --message m --title t --target dev --delete-pr-body --dry-run } | Should -Throw
    }
    It 'errors when --file has no value' {
        { gim-pr --repo . --file } | Should -Throw
    }
    It 'rejects a --target value that starts with a dash' {
        { gim-pr --repo . --file a --message m --title t --target -1bad --draft --dry-run } |
            Should -Throw '*--target*branch name*'
    }
    It 'rejects a --new-branch value that starts with a dash' {
        { gim-pr --repo . --file a --message m --title t --target dev --new-branch -1bad --draft --dry-run } |
            Should -Throw '*--new-branch*branch name*'
    }
}

Describe 'gim-pr path derivation' {
    InModuleScope GIMTools {
        It 'maps a known first-PR prefix to <slug>/<prefix>' {
            (Get-GimPrFilesRelativeDirectory -Branch 'feature/c3-gim-pr') | Should -Be 'c3-gim-pr/feature'
            (Get-GimPrFilesRelativeDirectory -Branch 'docs/setup-guide') | Should -Be 'setup-guide/docs'
            (Get-GimPrFilesRelativeDirectory -Branch 'hotfix/login') | Should -Be 'login/hotfix'
        }
        It 'maps any other <feature>/<step> branch to <feature>/<step>' {
            (Get-GimPrFilesRelativeDirectory -Branch 'gim-fleet-scripts/c3-gim-pr') |
                Should -Be 'gim-fleet-scripts/c3-gim-pr'
        }
        It 'strips refs/heads/' {
            (Get-GimPrFilesRelativeDirectory -Branch 'refs/heads/feature/x') | Should -Be 'x/feature'
        }
        It 'rejects a branch with more than one slash' {
            { Get-GimPrFilesRelativeDirectory -Branch 'feature/a/b' } |
                Should -Throw '*exactly one*required input*--files-file*'
        }
        It 'rejects an empty branch and detached HEAD' {
            { Get-GimPrFilesRelativeDirectory -Branch '' } | Should -Throw '*required input*--files-file*'
            { Get-GimPrFilesRelativeDirectory -Branch 'HEAD' } | Should -Throw '*detached HEAD*'
        }
        It 'derives the scratch files.txt path' {
            $root = Get-GimTestRepoRoot
            $path = Get-GimPrDerivedFilesFile -RepoRoot $root -Branch 'gim-fleet-scripts/c3-gim-pr'
            $path | Should -Match '\.agent-scratch[/\\]pr-files[/\\]gim-fleet-scripts[/\\]c3-gim-pr[/\\]files\.txt$'
        }
        It 'derives the result directory without error for a mapped branch' {
            $root = Get-GimTestRepoRoot
            (Get-GimPrResultDirectory -RepoRoot $root -Branch 'gim-fleet-scripts/c3-gim-pr') |
                Should -Match '\.agent-scratch[/\\]pr-files[/\\]gim-fleet-scripts[/\\]c3-gim-pr$'
            (Get-GimPrResultDirectory -RepoRoot $root -Branch 'feature/c3-gim-pr') |
                Should -Match '\.agent-scratch[/\\]pr-files[/\\]c3-gim-pr[/\\]feature$'
        }
        It 'sanitizes an invalid branch shape instead of throwing' {
            $root = Get-GimTestRepoRoot
            (Get-GimPrResultDirectory -RepoRoot $root -Branch 'feature/a/b') |
                Should -Match '\.agent-scratch[/\\]pr-files[/\\]feature-a-b$'
            (Get-GimPrResultDirectory -RepoRoot $root -Branch '') |
                Should -Match '\.agent-scratch[/\\]pr-files[/\\]unknown$'
        }
    }
}

Describe 'gim-pr file list parsing' {
    InModuleScope GIMTools {
        BeforeAll {
            $repoRoot = Get-GimTestRepoRoot
            $repoReal = Get-GimRealPath -Path $repoRoot
            $scratch = Join-Path $repoReal ('.agent-scratch\gim-pr-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path (Join-Path $scratch 'sub') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $scratch 'folder') -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $scratch 'a.txt'), 'a', (New-Object System.Text.UTF8Encoding($false)))
            [System.IO.File]::WriteAllText((Join-Path $scratch 'sub\b.txt'), 'b', (New-Object System.Text.UTF8Encoding($false)))
            $rootPrefix = $repoReal.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
            $script:relA = ((Join-Path $scratch 'a.txt').Substring($rootPrefix.Length) -replace '\\', '/')
            $script:relB = ((Join-Path $scratch 'sub\b.txt').Substring($rootPrefix.Length) -replace '\\', '/')
            $script:relDir = ((Join-Path $scratch 'folder').Substring($rootPrefix.Length) -replace '\\', '/')
            $script:scratchRel = ($scratch.Substring($rootPrefix.Length) -replace '\\', '/')
        }
        AfterAll {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        }

        It 'trims, ignores blanks, normalizes separators, and dedupes' {
            $files = @(ConvertTo-GimPrFileList -RepoRoot $repoReal -Entries @(
                ('  ' + $script:relA + '  '),
                '',
                ($script:relB -replace '/', '\'),
                $script:relA
            ))
            ($files -join '|') | Should -Be ($script:relA + '|' + $script:relB)
        }
        It 'rejects globs' {
            { ConvertTo-GimPrFileList -RepoRoot $repoReal -Entries @('src/*.cs') } |
                Should -Throw '*globs are not allowed*'
        }
        It 'rejects directories' {
            { ConvertTo-GimPrFileList -RepoRoot $repoReal -Entries @($script:relDir) } |
                Should -Throw '*directories are not allowed*'
        }
        It 'rejects escapes' {
            { ConvertTo-GimPrFileList -RepoRoot $repoReal -Entries @('..\outside.txt') } |
                Should -Throw '*escapes the repository*'
        }
        It 'rejects absolute paths' {
            { ConvertTo-GimPrFileList -RepoRoot $repoReal -Entries @((Join-Path $repoReal 'a.txt')) } |
                Should -Throw '*absolute paths are not allowed*'
        }
        It 'allows a missing path and returns it for git add' {
            $files = @(ConvertTo-GimPrFileList -RepoRoot $repoReal -Entries @('docs/no-such-file-xyz.txt'))
            ($files -join '|') | Should -Be 'docs/no-such-file-xyz.txt'
        }
        It 'reads a UTF-8 file list and derives its entries' {
            $listPath = Join-Path $scratch 'files.txt'
            [System.IO.File]::WriteAllText($listPath, ($script:relA + "`r`n`r`n" + $script:relB + "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
            $files = @(Read-GimPrFileList -RepoRoot $repoReal -Path $listPath)
            ($files -join '|') | Should -Be ($script:relA + '|' + $script:relB)
        }
        It 'rejects an empty file list' {
            $listPath = Join-Path $scratch 'empty.txt'
            [System.IO.File]::WriteAllText($listPath, "`r`n`r`n", (New-Object System.Text.UTF8Encoding($false)))
            { Read-GimPrFileList -RepoRoot $repoReal -Path $listPath } | Should -Throw '*file list is empty*'
        }
        It 'rejects a PR body outside the evidence directory' {
            $evidence = Join-Path $repoReal ('docs\gim-pr-evidence-' + [guid]::NewGuid().ToString('N'))
            { Resolve-GimPrRepoPath -RepoRoot $evidence -Path (Join-Path $repoReal 'no-such-body.md') -Label 'PR body' -RootLabel 'evidence directory' } |
                Should -Throw '*outside the evidence directory*'
        }
    }
}

Describe 'gim-pr index guard' {
    InModuleScope GIMTools {
        It 'accepts staged paths that are all in the list' {
            $guard = Test-GimPrStagedSubset -Staged @('b.txt', 'a.txt') -Listed @('A.TXT', 'b.txt')
            $guard.ok | Should -BeTrue
            @($guard.extra).Count | Should -Be 0
        }
        It 'reports staged paths outside the list' {
            $guard = Test-GimPrStagedSubset -Staged @('a.txt', 'sneaky.txt') -Listed @('a.txt')
            $guard.ok | Should -BeFalse
            ($guard.extra -join ',') | Should -Be 'sneaky.txt'
        }
        It 'accepts an empty index' {
            (Test-GimPrStagedSubset -Staged @() -Listed @('a.txt')).ok | Should -BeTrue
        }
    }
}

Describe 'gim-pr command plan and result shape' {
    InModuleScope GIMTools {
        It 'plans the first PR with init and no link or sync' {
            $plan = @(New-GimPrDryRunPlan -Branch 'feature/x' -Head 'feature/x' -Method 'init' -Remote 'origin' `
                -Target 'dev' -Files @('a.txt', 'b.txt') -Message 'C3: add gim-pr' `
                -Title '[Feature] Fleet - added gim-pr' -PrBody '.pr-body.md' `
                -Assignee '@me' -DeletePrBody $true)
            $plan[0] | Should -Be 'git diff --cached --name-only'
            $plan[1] | Should -Be 'git config rerere.enabled true'
            $plan[2] | Should -Be 'git config push.autoSetupRemote true'
            $plan[3] | Should -Be 'gh stack init --base dev feature/x'
            $plan | Should -Contain 'git diff --cached --name-only  # guard: only the listed files'
            $plan | Should -Contain 'git add -- a.txt b.txt'
            $plan | Should -Contain 'git commit --message C3: add gim-pr'
            $plan | Should -Contain 'git push -u origin HEAD'
            ($plan -join "`n") | Should -Match 'gh pr create --base dev --title \[Feature\] Fleet - added gim-pr --assignee @me --body-file \.pr-body\.md'
            ($plan -join "`n") | Should -Not -Match 'gh stack link'
            ($plan -join "`n") | Should -Not -Match 'gh stack sync'
            ($plan -join "`n") | Should -Not -Match 'gh stack view'
            ($plan -join "`n") | Should -Not -Match 'gh stack checkout'
            $plan | Should -Contain 'delete .pr-body.md after success'
        }
        It 'plans a step PR as add, link, view, and sync' {
            $plan = @(New-GimPrDryRunPlan -Branch 'feature/x' -Head 'feature/x/a1' -Method 'add' -Remote 'origin' `
                -Target 'feature/x' -Files @('a.txt') -Message 'm' -Title 't' -Draft $true -Assignee '@me' `
                -TargetNumber 141)
            $plan | Should -Contain 'gh stack add feature/x/a1'
            ($plan -join "`n") | Should -Not -Match 'gh stack add .*\s-[Aum]\b'
            $plan | Should -Contain 'gh stack link 141 feature/x/a1 --remote origin'
            $plan | Should -Contain 'gh stack view --json'
            $plan | Should -Contain 'gh stack sync --remote origin'
            ($plan -join "`n") | Should -Not -Match 'gh stack init'
            ($plan -join "`n") | Should -Match 'stack-count line'
        }
        It 'plans the step-PR create link with the target result base' {
            $plan = @(New-GimPrDryRunPlan -Branch 'feature/gim-pr-smoke-6' -Head 'gim-pr-smoke-6/a1-content' -Method 'add' -Remote 'origin' `
                -Target 'feature/gim-pr-smoke-6' -Files @('a.txt') -Message 'm' -Title 't' -Draft $true -Assignee '@me' `
                -TargetBase 'dev')
            $plan | Should -Contain 'gh stack link --base dev feature/gim-pr-smoke-6 gim-pr-smoke-6/a1-content --remote origin'
        }
        It 'uses --body "" for a draft without a body' {
            $plan = @(New-GimPrDryRunPlan -Branch 'feature/x' -Head 'feature/x/a1' -Method 'add' -Remote 'origin' `
                -Target 'dev' -Files @('a.txt') -Message 'm' -Title 't' -Draft $true -Assignee '@me')
            ($plan -join "`n") | Should -Match 'gh pr create --base dev --title t --assignee @me --draft --body ""'
        }
        It 'records the first-PR and step-PR stack outcomes' {
            $first = New-GimPrResultRecord -Repo 'D:\repo' -Root 'D:\repo' -Branch 'feature/x' -Head 'feature/x' -Target 'dev'
            [void](Set-GimPrStackOutcome -Result $first -Method 'init' -Succeeded $true)
            $first.ok | Should -BeTrue
            $first.stack.method | Should -Be 'init'
            $first.stack.pending | Should -BeTrue
            $first.stack.number | Should -BeNullOrEmpty
            $first.stack.verified | Should -BeNullOrEmpty
            $first.stack.link | Should -BeNullOrEmpty
            $first.stack.sync | Should -BeNullOrEmpty

            $stack = New-GimPrResultRecord -Repo 'D:\repo' -Root 'D:\repo' -Branch 'feature/x' -Head 'feature/x/a1' -Target 'feature/x'
            [void](Set-GimPrStackOutcome -Result $stack -Method 'add' -Succeeded $true -Number 125 -Verified $true)
            $stack.ok | Should -BeTrue
            $stack.stack.number | Should -Be 125
            $stack.stack.pending | Should -BeFalse
            $stack.stack.verified | Should -BeTrue

            $failed = New-GimPrResultRecord -Repo 'D:\repo' -Root 'D:\repo' -Branch 'feature/x' -Target 'dev'
            [void](Set-GimPrStackOutcome -Result $failed -Method 'add' -Succeeded $false -Number 141 -ErrorText 'sync refused')
            $failed.ok | Should -BeFalse
            $failed.stack.error | Should -Be 'sync refused'
            $failed.stack.pending | Should -BeFalse
            $failed.stack.verified | Should -BeFalse
            $failed.stack.number | Should -Be 141
        }
        It 'prefers returned assignees and falls back to the requested login' {
            (Resolve-GimPrAssignee -Returned @('octocat') -Requested '@me') | Should -Be 'octocat'
            (Resolve-GimPrAssignee -Returned @() -Requested '@me') | Should -Be '@me'
            $view = Get-GimPrViewRecord -JsonText '{"number":7,"url":"u","title":"t","baseRefName":"dev","headRefName":"feature/x","isDraft":false,"assignees":[{"login":"octocat"}]}'
            (Resolve-GimPrAssignee -Returned @($view.assignees) -Requested '@me') | Should -Be 'octocat'
        }
        It 'creates the result record with the documented keys' {
            $result = New-GimPrResultRecord -Repo 'D:\repo' -Root 'D:\repo' -Branch 'feature/x' `
                -Head 'feature/x/a1' -NewBranch 'feature/x/a1' -Remote 'origin' -Target 'dev' `
                -FilesFile '.agent-scratch/pr-files/x/feature/files.txt' `
                -Files @('a.txt') -Message 'm' -Title 't' -PrBody '.pr-body.md' -Draft $false -Assignee '@me'
            $keys = @($result.PSObject.Properties.Name)
            foreach ($key in @('ok', 'dry_run', 'repo', 'branch', 'head', 'new_branch', 'remote', 'target', 'files_file', 'file_count', 'files', 'commit', 'push', 'pr', 'stack', 'body', 'warnings', 'error')) {
                $keys | Should -Contain $key
            }
            $result.ok | Should -BeFalse
            $result.file_count | Should -Be 1
            $result.head | Should -Be 'feature/x/a1'
            $result.new_branch | Should -Be 'feature/x/a1'
            ((@($result.commit.PSObject.Properties.Name)) -join ',') | Should -Be 'message,sha,subject'
            foreach ($name in @('number', 'url', 'title', 'base', 'head', 'draft', 'assignee', 'error')) {
                @($result.pr.PSObject.Properties.Name) | Should -Contain $name
            }
            foreach ($name in @('method', 'number', 'url', 'pending', 'verified', 'link', 'sync', 'view_error', 'error')) {
                @($result.stack.PSObject.Properties.Name) | Should -Contain $name
            }
            @($result.stack.PSObject.Properties.Name) | Should -Not -Contain 'action'
            @($result.stack.PSObject.Properties.Name) | Should -Not -Contain 'status'
            @($result.stack.PSObject.Properties.Name) | Should -Not -Contain 'local_sync'
        }
        It 'bounds the text file summary but keeps the full record' {
            $files = New-Object System.Collections.Generic.List[string]
            for ($i = 1; $i -le 60; $i++) { [void]$files.Add(('src/file-{0}.cs' -f $i)) }
            $result = New-GimPrResultRecord -Repo 'D:\repo' -Root 'D:\repo' -Branch 'feature/x' -Head 'feature/x' -Target 'dev' -Files @($files.ToArray())
            $result.file_count | Should -Be 60
            @($result.files).Count | Should -Be 60
            $summary = (Format-GimPrSummary -Result $result) -join "`n"
            $summary | Should -Match '\.\.\. 10 more file\(s\)'
            $summary | Should -Not -Match 'src/file-60\.cs'
        }
        It 'parses the last PR URL from gh output' {
            (Get-GimPrUrlFromText -Text "creating`nhttps://github.com/o/r/pull/12`n") | Should -Be 'https://github.com/o/r/pull/12'
            (Get-GimPrUrlFromText -Text 'no url here') | Should -BeNullOrEmpty
        }
    }
}

Describe 'gim-pr stack parsers' {
    InModuleScope GIMTools {
        It 'parses stack #N permissively from link output' {
            (Get-GimPrStackNumberFromText -Text '✓ Added 1 PR to stack #141') | Should -Be 141
            (Get-GimPrStackNumberFromText -Text 'Created stack #7') | Should -Be 7
            (Get-GimPrStackNumberFromText -Text 'V Created stack with 2 PRs (stack #145)') | Should -Be 145
            (Get-GimPrStackNumberFromText -Text "stack #100`nV Added 1 PR to stack #141") | Should -Be 141
            (Get-GimPrStackNumberFromText -Text 'linked without a number') | Should -BeNullOrEmpty
        }
        It 'detects warning lines in gh output' {
            (Test-GimPrWarningOutput -Text "✓ Added 1 PR to stack #141") | Should -BeFalse
            (Test-GimPrWarningOutput -Text "⚠ step/a has no PR") | Should -BeTrue
            (Test-GimPrWarningOutput -Text "? step/a has no PR") | Should -BeTrue
        }
        It 'reads the documented view url and branch count without walking nested PR urls' {
            $top = Get-GimPrStackSummary -Text '{"url":"https://github.com/o/r/stack/1","branches":[{"name":"a"},{"name":"b"}]}'
            $top.url | Should -Be 'https://github.com/o/r/stack/1'
            $top.branch_count | Should -Be 2
            $nested = Get-GimPrStackSummary -Text '{"stack":{"url":"https://github.com/o/r/stack/2"},"branches":[{"name":"a"}]}'
            $nested.url | Should -Be 'https://github.com/o/r/stack/2'
            $nested.branch_count | Should -Be 1
            $prOnly = Get-GimPrStackSummary -Text '{"branches":[{"url":"https://github.com/o/r/pull/3"}]}'
            $prOnly.url | Should -BeNullOrEmpty
            $prOnly.branch_count | Should -Be 1
        }
        It 'reads the target result stack number and pr.base' {
            $repo = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-pr-target-' + [guid]::NewGuid().ToString('N'))
            try {
                $dir = Get-GimPrResultDirectory -RepoRoot $repo -Branch 'feature/target'
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                [System.IO.File]::WriteAllText((Join-Path $dir 'result.json'), '{"stack":{"number":141},"pr":{"base":"dev"}}', (New-Object System.Text.UTF8Encoding($false)))
                $append = Get-GimPrTargetResult -RepoRoot $repo -Branch 'feature/target'
                $append.ok | Should -BeTrue
                $append.number | Should -Be 141
                $append.base | Should -Be 'dev'

                [System.IO.File]::WriteAllText((Join-Path $dir 'result.json'), '{"stack":{"number":null},"pr":{"base":"dev"}}', (New-Object System.Text.UTF8Encoding($false)))
                $create = Get-GimPrTargetResult -RepoRoot $repo -Branch 'feature/target'
                $create.ok | Should -BeTrue
                $create.number | Should -BeNullOrEmpty
                $create.base | Should -Be 'dev'

                [System.IO.File]::WriteAllText((Join-Path $dir 'result.json'), '{"stack":{}}', (New-Object System.Text.UTF8Encoding($false)))
                $noBase = Get-GimPrTargetResult -RepoRoot $repo -Branch 'feature/target'
                $noBase.ok | Should -BeFalse
                $noBase.error | Should -Match 'no pr.base'
            } finally {
                Remove-Item -LiteralPath $repo -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'fails clearly when the target result is missing' {
            $repo = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-pr-target-' + [guid]::NewGuid().ToString('N'))
            try {
                New-Item -ItemType Directory -Path $repo -Force | Out-Null
                $missing = Get-GimPrTargetResult -RepoRoot $repo -Branch 'feature/target'
                $missing.ok | Should -BeFalse
                $missing.error | Should -Match 'target result.json not found'
            } finally {
                Remove-Item -LiteralPath $repo -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Describe 'gim-pr stack setup' {
    InModuleScope GIMTools {
        BeforeEach {
            $script:setupRepo = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-pr-setup-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $script:setupRepo -Force | Out-Null
            $script:setupCalls = New-Object System.Collections.Generic.List[string]
            $script:setupRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ''; stderr = '' }
            Mock Invoke-GimFleetExec {
                $script:setupCalls.Add(($Argv -join ' '))
                return $script:setupRun
            }
        }
        AfterEach {
            Remove-Item -LiteralPath $script:setupRepo -Recurse -Force -ErrorAction SilentlyContinue
        }
        It 'sets both config keys before gh stack init' {
            $setup = Invoke-GimPrStackSetup -Root $script:setupRepo -Target 'dev' -Head 'feature/x' -Method 'init'
            $setup.ok | Should -BeTrue
            $script:setupCalls[0] | Should -Be 'git config rerere.enabled true'
            $script:setupCalls[1] | Should -Be 'git config push.autoSetupRemote true'
            $script:setupCalls[2] | Should -Be 'gh stack init --base dev feature/x'
            $script:setupCalls.Count | Should -Be 3
        }
        It 'runs gh stack add with the new branch and no commit flags' {
            $setup = Invoke-GimPrStackSetup -Root $script:setupRepo -Target 'feature/x' -Head 'feature/x/a1' -Method 'add'
            $setup.ok | Should -BeTrue
            $script:setupCalls[2] | Should -Be 'gh stack add feature/x/a1'
            $script:setupCalls[2] | Should -Not -Match '\s-[Aum]\b'
            $script:setupCalls.Count | Should -Be 3
        }
        It 'fails when a config key cannot be set' {
            $script:setupRun = [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'config refused' }
            $setup = Invoke-GimPrStackSetup -Root $script:setupRepo -Target 'dev' -Head 'feature/x' -Method 'init'
            $setup.ok | Should -BeFalse
            $setup.step | Should -Be 'config'
            $setup.error | Should -Match 'config refused'
            $script:setupCalls.Count | Should -Be 1
        }
        It 'reports the exact gh stack init error for a missing local base' {
            Mock Invoke-GimFleetExec {
                $script:setupCalls.Add(($Argv -join ' '))
                if ($Argv[0] -eq 'gh') {
                    return [pscustomobject]@{ exit_code = 128; timed_out = $false; stdout = ''; stderr = "fatal: not a valid object name: 'dev'" }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ''; stderr = '' }
            }
            $setup = Invoke-GimPrStackSetup -Root $script:setupRepo -Target 'dev' -Head 'feature/x' -Method 'init'
            $setup.ok | Should -BeFalse
            $setup.step | Should -Be 'stack'
            $setup.error | Should -Match "fatal: not a valid object name: 'dev'"
            ($setup.command -join ' ') | Should -Be 'gh stack init --base dev feature/x'
        }
    }
}

Describe 'gim-pr stack workflow' {
    InModuleScope GIMTools {
        BeforeEach {
            $script:stackRepo = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-pr-workflow-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $script:stackRepo -Force | Out-Null
            $script:targetBranch = 'feature/target'
            $script:targetDir = Get-GimPrResultDirectory -RepoRoot $script:stackRepo -Branch $script:targetBranch
            $script:workflowCalls = New-Object System.Collections.Generic.List[string]
            $script:linkRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '✓ Added 1 PR to stack #141'; stderr = '' }
            $script:viewRun = [pscustomobject]@{
                exit_code = 0; timed_out = $false
                stdout = '{"url":"https://github.com/o/r/stack/141","branches":[{"name":"feature/target"},{"name":"step/a"}]}'
                stderr = ''
            }
            $script:syncRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '✓ Stack on GitHub is up to date with 2 PRs (stack #141)'; stderr = '' }
            New-Item -ItemType Directory -Path $script:targetDir -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $script:targetDir 'result.json'), '{"stack":{"number":141},"pr":{"base":"feature/target"}}', (New-Object System.Text.UTF8Encoding($false)))
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:workflowCalls.Add($joined)
                if ($joined -like 'gh stack link*') { return $script:linkRun }
                if ($joined -eq 'gh stack view --json') { return $script:viewRun }
                if ($joined -eq 'gh stack sync --remote origin') { return $script:syncRun }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ''; stderr = '' }
            }
        }
        AfterEach {
            Remove-Item -LiteralPath $script:stackRepo -Recurse -Force -ErrorAction SilentlyContinue
        }
        It 'first PR records init/pending with no link, view, or sync calls' {
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'feature/x' -Target 'dev' -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target 'dev' -Head 'feature/x' -Remote 'origin' -Method 'init'
            $flow.ok | Should -BeTrue
            $script:workflowCalls.Count | Should -Be 0
            $result.ok | Should -BeTrue
            $result.stack.method | Should -Be 'init'
            $result.stack.pending | Should -BeTrue
            $result.stack.number | Should -BeNullOrEmpty
            $result.stack.verified | Should -BeNullOrEmpty
            $result.stack.link | Should -BeNullOrEmpty
            $result.stack.sync | Should -BeNullOrEmpty
        }
        It 'appends with gh stack link <N> <branch> and verifies the count line' {
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeTrue
            $script:workflowCalls[0] | Should -Be 'gh stack link 141 step/a --remote origin'
            $script:workflowCalls[1] | Should -Be 'gh stack view --json'
            $script:workflowCalls[2] | Should -Be 'gh stack sync --remote origin'
            $result.ok | Should -BeTrue
            $result.stack.method | Should -Be 'add'
            $result.stack.number | Should -Be 141
            $result.stack.pending | Should -BeFalse
            $result.stack.verified | Should -BeTrue
            $result.stack.link.exit | Should -Be 0
            $result.stack.link.output | Should -Match 'Added 1 PR to stack #141'
            $result.stack.sync.exit | Should -Be 0
            $result.stack.sync.count_line | Should -Match 'up to date with 2 PRs \(stack #141\)'
            $result.stack.sync.output | Should -Match 'up to date with 2 PRs'
            $result.stack.url | Should -Be 'https://github.com/o/r/stack/141'
            ($script:workflowCalls -join "`n") | Should -Not -Match 'gh stack checkout'
        }
        It 'uses the last stack number when link output echoes an older one' {
            $script:linkRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = "stack #100`nV Added 1 PR to stack #141"; stderr = '' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeTrue
            $result.stack.number | Should -Be 141
            $result.stack.verified | Should -BeTrue
        }
        It 'creates with gh stack link --base from the target result pr.base' {
            [System.IO.File]::WriteAllText((Join-Path $script:targetDir 'result.json'), '{"stack":{"number":null},"pr":{"base":"dev"}}', (New-Object System.Text.UTF8Encoding($false)))
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeTrue
            $script:workflowCalls[0] | Should -Be 'gh stack link --base dev feature/target step/a --remote origin'
            $result.stack.number | Should -Be 141
            $result.stack.verified | Should -BeTrue
            ($script:workflowCalls -join "`n") | Should -Not -Match 'gh pr view.*baseRefName'
        }
        It 'accepts the create-path sync wording (Linked ... already up to date)' {
            [System.IO.File]::WriteAllText((Join-Path $script:targetDir 'result.json'), '{"stack":{"number":145},"pr":{"base":"feature/target"}}', (New-Object System.Text.UTF8Encoding($false)))
            $script:linkRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = 'V Created stack with 2 PRs (stack #145)'; stderr = '' }
            $script:syncRun = [pscustomobject]@{
                exit_code = 0; timed_out = $false
                stdout = 'V Linked to the existing stack on GitHub (2 PRs, already up to date) (stack #145)'; stderr = ''
            }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeTrue
            $result.stack.number | Should -Be 145
            $result.stack.verified | Should -BeTrue
            $result.stack.sync.count_line | Should -Match 'Linked to the existing stack on GitHub \(2 PRs, already up to date\) \(stack #145\)'
        }
        It 'uses the last matching stack-count line' {
            [System.IO.File]::WriteAllText((Join-Path $script:targetDir 'result.json'), '{"stack":{"number":145},"pr":{"base":"feature/target"}}', (New-Object System.Text.UTF8Encoding($false)))
            $script:linkRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = 'V Created stack with 2 PRs (stack #145)'; stderr = '' }
            $script:syncRun = [pscustomobject]@{
                exit_code = 0; timed_out = $false
                stdout = "V Linked to the existing stack on GitHub (2 PRs, already up to date) (stack #145)`nV Stack on GitHub is up to date with 3 PRs (stack #145)"; stderr = ''
            }
            $script:viewRun = [pscustomobject]@{
                exit_code = 0; timed_out = $false
                stdout = '{"url":"https://github.com/o/r/stack/145","branches":[{"name":"a"},{"name":"b"},{"name":"c"}]}'
                stderr = ''
            }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeTrue
            $result.stack.verified | Should -BeTrue
            $result.stack.sync.count_line | Should -Match 'up to date with 3 PRs \(stack #145\)'
        }
        It 'fails when a stack-count line has no stack #N' {
            $script:syncRun = [pscustomobject]@{
                exit_code = 0; timed_out = $false
                stdout = 'V Linked to the existing stack on GitHub (2 PRs, already up to date)'; stderr = ''
            }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'sync'
            $flow.error | Should -Match 'stack-count line'
            $result.stack.sync.count_line | Should -BeNullOrEmpty
        }
        It 'fails clearly when the target result.json is missing' {
            Remove-Item -LiteralPath (Join-Path $script:targetDir 'result.json') -Force
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'link'
            $flow.error | Should -Match 'target result.json not found'
            $script:workflowCalls.Count | Should -Be 0
            $result.ok | Should -BeFalse
            $result.stack.link | Should -BeNullOrEmpty
            $result.stack.verified | Should -BeFalse
        }
        It 'fails when gh stack link exits non-zero' {
            $script:linkRun = [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'link refused' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'link'
            $flow.error | Should -Match 'link refused'
            $result.stack.link.exit | Should -Be 1
            $result.stack.verified | Should -BeFalse
        }
        It 'fails when gh stack link prints a warning line' {
            $script:linkRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = "⚠ step/a has no PR`n✓ Added 1 PR to stack #141"; stderr = '' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'link'
            $flow.error | Should -Match 'warning'
            $result.stack.link.output | Should -Match 'has no PR'
        }
        It 'fails when gh stack link output has no stack number' {
            $script:linkRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '✓ linked without a number'; stderr = '' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'link'
            $flow.error | Should -Match 'no stack #N'
            $result.stack.link.exit | Should -Be 0
        }
        It 'fails when gh stack sync exits non-zero' {
            $script:syncRun = [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'sync refused' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'sync'
            $flow.error | Should -Match 'sync refused'
            $result.stack.link.output | Should -Match 'Added 1 PR to stack #141'
            $result.stack.sync.exit | Should -Be 1
            $result.stack.verified | Should -BeFalse
        }
        It 'fails when gh stack sync prints a warning line even with a count line' {
            $script:syncRun = [pscustomobject]@{
                exit_code = 0; timed_out = $false
                stdout = "⚠ step/a has no PR`n✓ Stack on GitHub is up to date with 2 PRs (stack #141)"; stderr = ''
            }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'sync'
            $flow.error | Should -Match 'warning'
            $result.stack.sync.count_line | Should -Match 'up to date with 2 PRs \(stack #141\)'
            $result.stack.verified | Should -BeFalse
        }
        It 'fails when the sync count line is missing' {
            $script:syncRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '✓ Stack synced'; stderr = '' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.step | Should -Be 'sync'
            $flow.error | Should -Match 'count line'
            $result.stack.sync.count_line | Should -BeNullOrEmpty
        }
        It 'fails when the sync count line stack number does not match the link number' {
            $script:syncRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '✓ Stack on GitHub is up to date with 2 PRs (stack #999)'; stderr = '' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.error | Should -Match 'does not match the link number'
            $result.stack.number | Should -Be 141
        }
        It 'fails when the sync count line PR count does not match the local branch count' {
            $script:viewRun = [pscustomobject]@{
                exit_code = 0; timed_out = $false
                stdout = '{"url":"https://github.com/o/r/stack/141","branches":[{"name":"a"},{"name":"b"},{"name":"c"}]}'
                stderr = ''
            }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeFalse
            $flow.error | Should -Match 'local branch count'
        }
        It 'preserves UTF-8 glyphs in stored link and sync output' {
            $check = [char]0x2713
            $dash = [char]0x2014
            $script:linkRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ('{0} Added 1 PR to stack #141 {1} ok' -f $check, $dash); stderr = '' }
            $script:syncRun = [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ('{0} Stack on GitHub is up to date with 2 PRs (stack #141) {1} done' -f $check, $dash); stderr = '' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeTrue
            $result.stack.link.output.Contains([string]$check) | Should -BeTrue
            $result.stack.link.output.Contains([string]$dash) | Should -BeTrue
            $result.stack.sync.output.Contains([string]$check) | Should -BeTrue
            $result.stack.sync.output.Contains([string]$dash) | Should -BeTrue
            $written = Save-GimPrResult -Result $result -Path (Join-Path $script:stackRepo '.agent-scratch\pr-files\utf8\result.json')
            $doc = [System.IO.File]::ReadAllText($written, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
            $doc.stack.link.output.Contains([string]$check) | Should -BeTrue
            $doc.stack.sync.output.Contains([string]$dash) | Should -BeTrue
        }
        It 'downgrades the K check to number-only when the view fails' {
            $script:viewRun = [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'not in a stack' }
            $result = New-GimPrResultRecord -Repo $script:stackRepo -Root $script:stackRepo -Branch 'feature/x' -Head 'step/a' -Target $script:targetBranch -Remote 'origin'
            $flow = Invoke-GimPrStackWorkflow -Result $result -Root $script:stackRepo -Target $script:targetBranch -Head 'step/a' -Remote 'origin' -Method 'add'
            $flow.ok | Should -BeTrue
            $result.stack.verified | Should -BeTrue
            $result.stack.number | Should -Be 141
            $result.stack.view_error | Should -Match 'not in a stack'
            @($result.warnings).Count | Should -Be 1
        }
    }
}

Describe 'gim-jira-search' {
    InModuleScope GIMTools {
        It 'escapes keyword quotes in JQL' {
            (New-GimFleetJiraKeywordJql -Keyword 'auth "login"' -Project 'gim') |
                Should -Be 'project = GIM AND (summary ~ "auth \"login\"" OR description ~ "auth \"login\"")'
        }
        It 'escapes backslashes before quotes in JQL' {
            (New-GimFleetJiraKeywordJql -Keyword 'C:\temp\' -Project 'GIM') |
                Should -Be 'project = GIM AND (summary ~ "C:\\temp\\" OR description ~ "C:\\temp\\")'
        }
        It 'normalizes flat and nested search JSON and strips PII' {
            $json = @'
[
  {"key":"GIM-1","fields":{"summary":"Auth bug","status":{"name":"To Do"},"assignee":{"displayName":"Alice","emailAddress":"alice@example.com","accountId":"5b10ac8d82e05b22cc7d4ef5","avatarUrls":{"48x48":"https://x"}}}},
  {"key":"GIM-2","summary":"Auth docs","status":"Done","assignee":null}
]
'@
            $parsed = ConvertFrom-GimFleetJiraSearchJson -Text $json
            $parsed.ok | Should -BeTrue
            @($parsed.items).Count | Should -Be 2
            $parsed.items[0].key | Should -Be 'GIM-1'
            $parsed.items[0].summary | Should -Be 'Auth bug'
            $parsed.items[0].status | Should -Be 'To Do'
            $parsed.items[0].assignee | Should -Be 'Alice'
            $parsed.items[1].status | Should -Be 'Done'
            $serialized = $parsed | ConvertTo-Json -Depth 6
            $serialized | Should -Not -Match 'alice@example\.com'
            $serialized | Should -Not -Match '5b10ac8d82e05b22cc7d4ef5'
            $serialized | Should -Not -Match 'avatar'
        }
        It 'drops Jira Cloud colon-form account ids in flat assignee strings' {
            $parsed = ConvertFrom-GimFleetJiraSearchJson -Text '[{"key":"GIM-9","summary":"x","status":"To Do","assignee":"557058:1a2b3c4d5e6f7a8b9c0d1e2f"}]'
            $parsed.items[0].assignee | Should -BeNullOrEmpty
        }
        It 'treats an empty wrapper as zero items' {
            $parsed = ConvertFrom-GimFleetJiraSearchJson -Text '{"total":0}'
            $parsed.ok | Should -BeTrue
            @($parsed.items).Count | Should -Be 0
            @(Get-GimFleetJiraItemsArray -Document ([pscustomobject]@{ total = 0 }) -Names @('sprints', 'values', 'items')).Count | Should -Be 0
        }
        It 'runs one query per keyword and dedupes by key' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                if ($joined -match 'one') {
                    return [pscustomobject]@{
                        exit_code = 0; timed_out = $false; stderr = ''
                        stdout = '[{"key":"GIM-1","summary":"One","status":"To Do","assignee":{"displayName":"Alice","emailAddress":"alice@example.com","accountId":"5b10ac8d82e05b22cc7d4ef5"}},{"key":"GIM-2","summary":"Two","status":"Done","assignee":null}]'
                    }
                }
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"GIM-2","summary":"Two","status":"Done","assignee":null},{"key":"GIM-3","summary":"Three","status":"In Progress","assignee":null}]'
                }
            }
            $raw = gim-jira-search --keyword one --keyword two --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.count | Should -Be 3
            @($doc.items).Count | Should -Be 3
            ((@($doc.items | ForEach-Object { $_.key })) -join '|') | Should -Be 'GIM-1|GIM-2|GIM-3'
            $script:calls.Count | Should -Be 2
            ($script:calls[0]) | Should -Match 'summary ~ "one"'
            ($script:calls[1]) | Should -Match 'summary ~ "two"'
            ([string]$raw) | Should -Not -Match 'alice@example\.com'
            ([string]$raw) | Should -Not -Match '5b10ac8d82e05b22cc7d4ef5'
            $names = @($doc.items[0].PSObject.Properties.Name)
            $names.Count | Should -Be 4
            foreach ($name in @('key', 'summary', 'status', 'assignee')) { $names | Should -Contain $name }
        }
        It 'applies the active sprint filter for --current-sprint' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                if ($joined -match 'board list-sprints') {
                    return [pscustomobject]@{
                        exit_code = 0; timed_out = $false; stderr = ''
                        stdout = '[{"id":378,"name":"GIM Sprint 8","state":"active"}]'
                    }
                }
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"GIM-4","summary":"Sprint item","status":"To Do","assignee":null}]'
                }
            }
            $raw = gim-jira-search --keyword auth --current-sprint --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.count | Should -Be 1
            $script:calls.Count | Should -Be 2
            ($script:calls[1]) | Should -Match 'sprint = 378'
        }
        It 'queries the project with --current-sprint only' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                if ($joined -match 'board list-sprints') {
                    return [pscustomobject]@{
                        exit_code = 0; timed_out = $false; stderr = ''
                        stdout = '[{"id":378,"name":"GIM Sprint 8","state":"active"}]'
                    }
                }
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"GIM-4","summary":"Sprint item","status":"To Do","assignee":null}]'
                }
            }
            $raw = gim-jira-search --current-sprint --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $script:calls.Count | Should -Be 2
            ($script:calls[1]) | Should -Match '--jql project = GIM AND sprint = 378'
            ($script:calls[1]) | Should -Match '--limit 50'
        }
        It 'uses --jql as given' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"GIM-5","summary":"JQL item","status":"Done","assignee":null}]'
                }
            }
            $raw = gim-jira-search --jql 'project = GIM AND status = "Done"' --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            ($script:calls[0]) | Should -Match '--jql project = GIM AND status = "Done"'
        }
        It 'returns ok=false when active sprint resolution is ambiguous' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '[]'; stderr = '' }
            }
            $sprint = Resolve-GimFleetJiraCurrentSprint -BoardId 9
            $sprint.ok | Should -BeFalse
            $sprint.reason | Should -Be 'none'
            $sprint.error | Should -Match 'exactly one active sprint'
            $sprint.error | Should -Not -Match '--jql'
            (Format-GimFleetJiraSprintFailure -Failure $sprint -Verb 'search') | Should -Match 'sprint = <id>'
            $script:calls.Count | Should -Be 1
            ($script:calls[0]) | Should -Match 'board list-sprints'
        }
        It 'returns ok=false when many active sprints are returned' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"id":1,"state":"active"},{"id":2,"state":"active"}]'
                }
            }
            $sprint = Resolve-GimFleetJiraCurrentSprint -BoardId 9
            $sprint.ok | Should -BeFalse
            $sprint.reason | Should -Be 'multiple'
            $sprint.count | Should -Be 2
            $sprint.error | Should -Match 'found 2'
            $createMessage = Format-GimFleetJiraSprintFailure -Failure $sprint -Verb 'create'
            $createMessage | Should -Match '--sprint <id>'
            $createMessage | Should -Not -Match '--jql'
            $searchMessage = Format-GimFleetJiraSprintFailure -Failure $sprint -Verb 'search'
            $searchMessage | Should -Match '--jql'
            $script:calls.Count | Should -Be 1
        }
        It 'reads board id and sprint capability from the D0 config without fallback' {
            $path = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-config-' + [guid]::NewGuid().ToString('N') + '.json')
            try {
                [System.IO.File]::WriteAllText($path, '{"board":{"id":12},"sprint":{"supported":true,"field_id":"customfield_99999","current_sprint":{"resolvable":true}}}', (New-Object System.Text.UTF8Encoding($false)))
                $settings = Get-GimFleetJiraSettings -Path $path
                $settings.ok | Should -BeTrue
                $settings.board_id | Should -Be 12
                $settings.sprint_field_id | Should -Be 'customfield_99999'
                $settings.sprint_supported | Should -BeTrue
                $settings.current_sprint_resolvable | Should -BeTrue

                [System.IO.File]::WriteAllText($path, '{"board":{"id":12},"sprint":{"supported":false,"current_sprint":{"resolvable":false}}}', (New-Object System.Text.UTF8Encoding($false)))
                $disabled = Get-GimFleetJiraSettings -Path $path
                $disabled.sprint_supported | Should -BeFalse
                $disabled.current_sprint_resolvable | Should -BeFalse

                $missing = Get-GimFleetJiraSettings -Path (Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-missing-' + [guid]::NewGuid().ToString('N') + '.json'))
                $missing.ok | Should -BeFalse
                $missing.error | Should -Match 'Jira config not found'
            } finally {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
        It 'honors the config sprint-supported flag' {
            $path = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-config-' + [guid]::NewGuid().ToString('N') + '.json')
            try {
                [System.IO.File]::WriteAllText($path, '{"board":{"id":9},"sprint":{"supported":false,"current_sprint":{"resolvable":true}}}', (New-Object System.Text.UTF8Encoding($false)))
                $settings = Get-GimFleetJiraSettings -Path $path
                $settings.ok | Should -BeTrue
                $settings.sprint_supported | Should -BeFalse
            } finally {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
        It 'forwards --limit to acli' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"GIM-1","summary":"One","status":"To Do","assignee":null}]'
                }
            }
            $raw = gim-jira-search --keyword one --limit 7 --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            ($script:calls[0]) | Should -Match '--limit 7'
        }
        It 'redacts email and account id from failure text' {
            $text = Redact-GimFleetText -Text 'denied for alice@example.com account 5b10ac8d82e05b22cc7d4ef5'
            $text | Should -Not -Match 'alice@example\.com'
            $text | Should -Not -Match '5b10ac8d82e05b22cc7d4ef5'
        }
        It 'ignores a stderr warning when stdout JSON is valid' {
            Mock Invoke-GimFleetExec {
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false
                    stdout = '[{"key":"GIM-1","summary":"One","status":"To Do","assignee":null}]'
                    stderr = 'warning: harmless'
                }
            }
            $raw = gim-jira-search --keyword one --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.count | Should -Be 1
        }
        It 'exits 1 for a JSON failure' {
            $modulePath = Join-Path $script:GimModuleRoot 'GIMTools\GIMTools.psd1'
            $pwshPath = (Get-Process -Id $PID).Path
            $scriptText = "Import-Module '$modulePath' -Force -DisableNameChecking; & (Get-Module GIMTools) { Write-GimJiraSearchFailure -Message 'boom' -JsonMode `$true }"
            $run = Invoke-GimFleetExec -Argv @($pwshPath, '-NoProfile', '-Command', $scriptText) -WorkingDirectory (Get-GimTestRepoRoot) -TimeoutSeconds 60
            $run.exit_code | Should -Be 1
            $doc = $run.stdout | ConvertFrom-Json
            $doc.ok | Should -BeFalse
            $doc.error | Should -Be 'boom'
        }
        It 'emits a JSON envelope for an argument error' {
            $modulePath = Join-Path $script:GimModuleRoot 'GIMTools\GIMTools.psd1'
            $pwshPath = (Get-Process -Id $PID).Path
            $scriptText = "Import-Module '$modulePath' -Force -DisableNameChecking; gim-jira-search --keyword one --jql 'project = GIM' --json"
            $run = Invoke-GimFleetExec -Argv @($pwshPath, '-NoProfile', '-Command', $scriptText) -WorkingDirectory (Get-GimTestRepoRoot) -TimeoutSeconds 60
            $run.exit_code | Should -Be 1
            $doc = $run.stdout | ConvertFrom-Json
            $doc.ok | Should -BeFalse
            $doc.error | Should -Match 'never both'
            $doc.count | Should -Be 0
            @($doc.PSObject.Properties.Name) | Should -Contain 'items'
        }
        It 'caps output with the shared items cap and standard marker' {
            $items = New-Object System.Collections.Generic.List[string]
            for ($i = 1; $i -le 501; $i++) {
                [void]$items.Add('{"key":"GIM-' + $i + '","summary":"s","status":"To Do","assignee":null}')
            }
            $script:jiraJson = '[' + ($items.ToArray() -join ',') + ']'
            Mock Invoke-GimFleetExec {
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = $script:jiraJson; stderr = '' }
            }
            $lines = @(gim-jira-search --keyword one)
            ($lines -join "`n") | Should -Match '# gim: output truncated \(items 501, bytes \d+\)'
            $raw = gim-jira-search --keyword one --json
            $doc = $raw | ConvertFrom-Json
            $doc.count | Should -Be 501
            $doc.truncated | Should -BeTrue
            @($doc.items).Count | Should -Be 500
        }
        It 'rejects conflicting search sources' {
            { gim-jira-search --keyword one --jql 'project = GIM' } | Should -Throw '*never both*'
        }
        It 'queries the project with no search input' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"GIM-1","summary":"One","status":"To Do","assignee":null}]'
                }
            }
            $raw = gim-jira-search --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.count | Should -Be 1
            ($script:calls[0]) | Should -Match '--jql project = GIM'
            ($script:calls[0]) | Should -Match '--limit 50'
        }
        It 'queries the project with --project only' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"ABC-1","summary":"One","status":"To Do","assignee":null}]'
                }
            }
            $raw = gim-jira-search --project ABC --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            ($script:calls[0]) | Should -Match '--jql project = ABC'
            ($script:calls[0]) | Should -Match '--limit 50'
        }
        It 'queries the project with --limit only and forwards it' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                return [pscustomobject]@{
                    exit_code = 0; timed_out = $false; stderr = ''
                    stdout = '[{"key":"GIM-1","summary":"One","status":"To Do","assignee":null}]'
                }
            }
            $raw = gim-jira-search --limit 7 --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            ($script:calls[0]) | Should -Match '--jql project = GIM'
            ($script:calls[0]) | Should -Match '--limit 7'
        }
        It 'rejects a non-positive --limit' {
            { gim-jira-search --keyword one --limit 0 } | Should -Throw '*positive integer*'
        }
    }
}

Describe 'gim-pr step branch precondition' {
    InModuleScope GIMTools {
        BeforeAll {
            $script:preconditionRepo = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-pr-precondition-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $script:preconditionRepo -Force | Out-Null
        }
        AfterAll {
            Remove-Item -LiteralPath $script:preconditionRepo -Recurse -Force -ErrorAction SilentlyContinue
        }
        It 'requires the current branch to equal --target for a step PR' {
            Mock Resolve-GimFleetRepo { $script:preconditionRepo }
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                if ($joined -eq 'git rev-parse --show-toplevel') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = $script:preconditionRepo; stderr = '' }
                }
                if ($joined -eq 'git rev-parse --abbrev-ref HEAD') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = 'step/a'; stderr = '' }
                }
                if ($joined -eq 'git symbolic-ref -q --short HEAD') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = 'step/a'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ''; stderr = '' }
            }
            { gim-pr --repo fake --file a.txt --message 'm' --title 't' --target previous/branch --new-branch step/b --draft --dry-run } |
                Should -Throw '*requires the current branch to equal --target*'
        }
        It 'refuses a first PR on a protected branch' {
            Mock Resolve-GimFleetRepo { $script:preconditionRepo }
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                if ($joined -eq 'git rev-parse --show-toplevel') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = $script:preconditionRepo; stderr = '' }
                }
                if ($joined -eq 'git rev-parse --abbrev-ref HEAD') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = 'dev'; stderr = '' }
                }
                if ($joined -eq 'git symbolic-ref -q --short HEAD') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = 'dev'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ''; stderr = '' }
            }
            { gim-pr --repo fake --file a.txt --message 'm' --title 't' --target dev --draft --dry-run } |
                Should -Throw '*protected branch*'
        }
        It 'resolves a repo-relative --pr-body inside the evidence directory' {
            $branch = 'feature/x'
            $evidence = Join-Path $script:preconditionRepo '.agent-scratch\pr-files\x\feature'
            New-Item -ItemType Directory -Path $evidence -Force | Out-Null
            $body = Join-Path $evidence 'body.md'
            [System.IO.File]::WriteAllText($body, 'body', (New-Object System.Text.UTF8Encoding($false)))
            Mock Resolve-GimFleetRepo { $script:preconditionRepo }
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                if ($joined -eq 'git rev-parse --show-toplevel') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = $script:preconditionRepo; stderr = '' }
                }
                if ($joined -eq 'git rev-parse --abbrev-ref HEAD') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = $branch; stderr = '' }
                }
                if ($joined -eq 'git symbolic-ref -q --short HEAD') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = $branch; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = ''; stderr = '' }
            }
            $raw = gim-pr --repo fake --file a.txt --message 'm' --title 't' --target dev --pr-body '.agent-scratch/pr-files/x/feature/body.md' --dry-run --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.body.path | Should -Be (Get-GimRealPath -Path $body)
        }
    }
}

Describe 'gim-pr result write roots' {
    InModuleScope GIMTools {
        BeforeAll {
            $script:tempTarget = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-pr-result-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $script:tempTarget -Force | Out-Null
        }
        AfterAll {
            Remove-Item -LiteralPath $script:tempTarget -Recurse -Force -ErrorAction SilentlyContinue
        }
        It 'writes result.json under the temp target repo, not the module repo' {
            $result = New-GimPrResultRecord -Repo $script:tempTarget -Root $script:tempTarget -Branch 'feature/x' -Target 'dev'
            $path = Join-Path $script:tempTarget '.agent-scratch\pr-files\x\feature\result.json'
            $written = Save-GimPrResult -Result $result -Path $path
            $written | Should -Be (Get-GimRealPath -Path $path)
            Test-Path -LiteralPath $path -PathType Leaf | Should -BeTrue
        }
        It 'rejects a result path outside the target repo' {
            $result = New-GimPrResultRecord -Repo $script:tempTarget -Root $script:tempTarget -Branch 'feature/x' -Target 'dev'
            $outside = Join-Path ([System.IO.Path]::GetPathRoot($script:tempTarget)) ('gim-pr-reject-' + [guid]::NewGuid().ToString('N') + '\result.json')
            { Save-GimPrResult -Result $result -Path $outside } | Should -Throw '*write path not contained*'
        }
        It 'rejects an OS-temp path outside the target repo' {
            $result = New-GimPrResultRecord -Repo $script:tempTarget -Root $script:tempTarget -Branch 'feature/x' -Target 'dev'
            $outside = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-pr-temp-reject-' + [guid]::NewGuid().ToString('N') + '\result.json')
            { Save-GimPrResult -Result $result -Path $outside } | Should -Throw '*write path not contained*'
        }
        It 'turns a result-write failure into a warning and still prints the result' {
            $result = New-GimPrResultRecord -Repo $script:tempTarget -Root $script:tempTarget -Branch 'feature/x' -Target 'dev'
            $result.ok = $true
            $outside = Join-Path ([System.IO.Path]::GetPathRoot($script:tempTarget)) ('gim-pr-reject-' + [guid]::NewGuid().ToString('N') + '\result.json')
            $output = @(Complete-GimPrResult -Result $result -ResultPath $outside -JsonMode $false)
            $result.ok | Should -BeTrue
            (@($result.warnings) -join "`n") | Should -Match 'could not write result.json'
            ($output -join "`n") | Should -Match 'warning: could not write result.json'
            ($output -join "`n") | Should -Match 'gim-pr: ok'
        }
    }
}

Describe 'gim-pr detached HEAD guard' {
    InModuleScope GIMTools {
        BeforeAll {
            $repoRoot = Get-GimTestRepoRoot
            $scratch = Join-Path $repoRoot ('.agent-scratch\gim-pr-detached-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $scratch -Force | Out-Null
            $init = Invoke-GimFleetExec -Argv @('git', 'init') -WorkingDirectory $scratch -TimeoutSeconds 60
            if ($init.exit_code -ne 0) { throw ('git init failed: ' + $init.stderr) }
            $commit = Invoke-GimFleetExec -Argv @(
                'git', '-c', 'user.name=GIM Test', '-c', 'user.email=gim-test@example.invalid',
                'commit', '--allow-empty', '-m', 'init'
            ) -WorkingDirectory $scratch -TimeoutSeconds 60
            if ($commit.exit_code -ne 0) { throw ('git commit failed: ' + $commit.stderr) }
            $detach = Invoke-GimFleetExec -Argv @('git', 'checkout', '--detach') -WorkingDirectory $scratch -TimeoutSeconds 60
            if ($detach.exit_code -ne 0) { throw ('git detach failed: ' + $detach.stderr) }
            $script:detachedRepo = $scratch
        }
        AfterAll {
            Remove-Item -LiteralPath $script:detachedRepo -Recurse -Force -ErrorAction SilentlyContinue
        }
        It 'rejects a detached HEAD before staging or committing' {
            { gim-pr --repo $script:detachedRepo --file a.txt --message 'm' --title 't' --target dev --draft --dry-run } |
                Should -Throw '*detached HEAD*'
        }
    }
}

Describe 'gim-jira-create' {
    InModuleScope GIMTools {
        It 'builds the observed ADF document from markdown' {
            $adf = ConvertTo-GimFleetJiraAdf -Markdown "line one`nline two"
            $adf.type | Should -Be 'doc'
            $adf.version | Should -Be 1
            @($adf.content).Count | Should -Be 1
            $adf.content[0].type | Should -Be 'paragraph'
            $adf.content[0].content[0].type | Should -Be 'text'
            $adf.content[0].content[0].text | Should -Be "line one`nline two"
        }
        It 'splits ADF paragraphs on blank lines without interpreting markdown' {
            $adf = ConvertTo-GimFleetJiraAdf -Markdown "## Heading`n`n- bullet one`n- bullet two"
            @($adf.content).Count | Should -Be 2
            $adf.content[0].content[0].text | Should -Be '## Heading'
            $adf.content[1].content[0].text | Should -Be "- bullet one`n- bullet two"
        }
        It 'uses the configured sprint field id instead of a hardcoded custom field' {
            $template = [System.IO.File]::ReadAllText((Get-GimFleetJiraCreateTemplatePath), [System.Text.Encoding]::UTF8) | ConvertFrom-Json
            $payload = New-GimFleetJiraCreatePayload -Template $template -Project 'GIM' -Summary 's' -Type 'Task' -SprintId 42 -SprintFieldId 'customfield_99999' -HasSprint $true
            $payload.additionalAttributes.customfield_99999 | Should -Be 42
            @($payload.additionalAttributes.PSObject.Properties.Name) | Should -Not -Contain 'customfield_10020'
        }
        It 'fills the template payload on dry-run without calling acli' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec { $script:calls.Add(($Argv -join ' ')); return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' } }
            $desc = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-desc-' + [guid]::NewGuid().ToString('N') + '.md')
            try {
                [System.IO.File]::WriteAllText($desc, "Fix the auth flow.`n", (New-Object System.Text.UTF8Encoding($false)))
                $raw = gim-jira-create --summary 'Fix auth' --project gim --type Task --description-file $desc --sprint 378 --parent GIM-1 --label bug --label auth --dry-run --json
                $doc = $raw | ConvertFrom-Json
                $doc.ok | Should -BeTrue
                $doc.dry_run | Should -BeTrue
                $doc.payload.projectKey | Should -Be 'GIM'
                $doc.payload.summary | Should -Be 'Fix auth'
                $doc.payload.type | Should -Be 'Task'
                $doc.payload.assignee | Should -BeNullOrEmpty
                ($doc.payload.labels -join ',') | Should -Be 'bug,auth'
                $doc.payload.description.type | Should -Be 'doc'
                $doc.payload.description.version | Should -Be 1
                $doc.payload.description.content[0].content[0].text | Should -Be 'Fix the auth flow.'
                $doc.payload.parent | Should -Be 'GIM-1'
                $doc.payload.additionalAttributes.customfield_10020 | Should -Be 378
                @($doc.commands).Count | Should -Be 3
                $doc.commands[0] | Should -Match 'acli jira workitem create --from-json'
                $doc.commands[1] | Should -Match "assign --key <new-key> --assignee @me --yes --json"
                $doc.commands[2] | Should -Match 'view <new-key>'
                $script:calls.Count | Should -Be 0
            } finally {
                Remove-Item -LiteralPath $desc -Force -ErrorAction SilentlyContinue
            }
        }
        It 'defaults labels to null and supports --unassigned' {
            $raw = gim-jira-create --summary 'No labels' --unassigned --dry-run --json
            $doc = $raw | ConvertFrom-Json
            $doc.payload.labels | Should -BeNullOrEmpty
            $doc.assigned | Should -BeFalse
            @($doc.commands).Count | Should -Be 2
            ($doc.commands -join "`n") | Should -Not -Match ' workitem assign '
        }
        It 'uses the current-sprint placeholder on dry-run without acli' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec { $script:calls.Add(($Argv -join ' ')); return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' } }
            $raw = gim-jira-create --summary 'Sprint dry run' --current-sprint --dry-run --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.payload.additionalAttributes.customfield_10020 | Should -Be '<current-sprint-id>'
            ($doc.warnings -join "`n") | Should -Match 'current-sprint'
            $script:calls.Count | Should -Be 0
        }
        It 'redacts an explicit assignee email from dry-run commands' {
            $raw = gim-jira-create --summary 'Assign item' --assignee 'alice@example.com' --dry-run --json
            $doc = $raw | ConvertFrom-Json
            $doc.commands[1] | Should -Match '--assignee <redacted-email>'
            ([string]$raw) | Should -Not -Match 'alice@example\.com'
        }
        It 'does not echo a rejected assignee value in the error' {
            $caught = $null
            try {
                gim-jira-create --summary 'Bad assignee' --assignee 'alice@example.com extra' --dry-run
            } catch {
                $caught = [string]$_.Exception.Message
            }
            $caught | Should -Match "--assignee must be '@me' or an email address"
            $caught | Should -Not -Match 'alice@example\.com'
        }
        It 'requires sprint resolution support for --current-sprint' {
            Mock Get-GimFleetJiraSettings {
                return [pscustomobject]@{
                    ok = $true; path = 'config'; site = 'gim.atlassian.net'; board_id = 9
                    sprint_field_id = 'customfield_10020'; sprint_supported = $true
                    current_sprint_resolvable = $false; sprint_create_supported = $true; error = $null
                }
            }
            { gim-jira-create --summary 'Sprint item' --current-sprint } | Should -Throw '*current-sprint resolution is not supported*'
        }
        It 'emits a JSON envelope for an argument error' {
            $modulePath = Join-Path $script:GimModuleRoot 'GIMTools\GIMTools.psd1'
            $pwshPath = (Get-Process -Id $PID).Path
            $scriptText = "Import-Module '$modulePath' -Force -DisableNameChecking; gim-jira-create --summary '' --json"
            $run = Invoke-GimFleetExec -Argv @($pwshPath, '-NoProfile', '-Command', $scriptText) -WorkingDirectory (Get-GimTestRepoRoot) -TimeoutSeconds 60
            $run.exit_code | Should -Be 1
            $doc = $run.stdout | ConvertFrom-Json
            $doc.ok | Should -BeFalse
            $doc.error | Should -Match '--summary'
            foreach ($name in @('ok', 'dry_run', 'key', 'url', 'project', 'type', 'summary', 'sprint_id', 'assigned', 'payload', 'commands', 'warnings', 'error')) {
                @($doc.PSObject.Properties.Name) | Should -Contain $name
            }
        }
        It 'creates, assigns @me, and verifies without echoing identity' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            $script:createPayloads = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                if ($Argv -contains 'create') {
                    $idx = [Array]::IndexOf($Argv, '--from-json')
                    $script:createPayloadPath = [string]$Argv[$idx + 1]
                    $script:createPayloads.Add([System.IO.File]::ReadAllText($script:createPayloadPath, [System.Text.Encoding]::UTF8))
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-123","url":"https://gim.atlassian.net/browse/GIM-123"}'; stderr = '' }
                }
                if ($Argv -contains 'assign') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"ok":true}'; stderr = '' }
                }
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-123","fields":{"assignee":{"displayName":"Alice","emailAddress":"alice@example.com","accountId":"557058:1a2b3c4d5e6f7a8b9c0d1e2f"}}}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $raw = gim-jira-create --summary 'Fix auth' --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.key | Should -Be 'GIM-123'
            $doc.url | Should -Be 'https://gim.atlassian.net/browse/GIM-123'
            $doc.assigned | Should -BeTrue
            $script:calls.Count | Should -Be 3
            ($script:calls[0]) | Should -Match 'workitem create'
            ($script:calls[1]) | Should -Match "assign --key GIM-123 --assignee @me --yes --json"
            ($script:calls[2]) | Should -Match 'view GIM-123'
            $payload = $script:createPayloads[0] | ConvertFrom-Json
            $payload.assignee | Should -BeNullOrEmpty
            $payload.labels | Should -BeNullOrEmpty
            (Test-Path -LiteralPath $script:createPayloadPath) | Should -BeFalse
            ([string]$raw) | Should -Not -Match 'alice@example\.com'
            ([string]$raw) | Should -Not -Match '557058:'
        }
        It 'skips assignment with --unassigned' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains 'create') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-124"}'; stderr = '' }
                }
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-124","fields":{"assignee":null}}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $raw = gim-jira-create --summary 'Unassigned item' --unassigned --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.assigned | Should -BeFalse
            $script:calls.Count | Should -Be 2
            ($script:calls -join "`n") | Should -Not -Match ' workitem assign '
        }
        It 'retries with labels null after a label rejection' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            $script:createPayloads = New-Object System.Collections.Generic.List[string]
            $script:createCount = 0
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                if ($Argv -contains 'create') {
                    $script:createCount++
                    $idx = [Array]::IndexOf($Argv, '--from-json')
                    $script:createPayloads.Add([System.IO.File]::ReadAllText([string]$Argv[$idx + 1], [System.Text.Encoding]::UTF8))
                    if ($script:createCount -eq 1) {
                        return [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'unsupported field labels' }
                    }
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-125"}'; stderr = '' }
                }
                if ($Argv -contains 'assign') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"ok":true}'; stderr = '' }
                }
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-125","fields":{"assignee":{"displayName":"Alice"}}}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $raw = gim-jira-create --summary 'Labels item' --label bug --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $script:createCount | Should -Be 2
            $first = $script:createPayloads[0] | ConvertFrom-Json
            $second = $script:createPayloads[1] | ConvertFrom-Json
            ($first.labels -join ',') | Should -Be 'bug'
            $second.labels | Should -BeNullOrEmpty
            ($doc.warnings -join "`n") | Should -Match 'labels null'
        }
        It 'resolves --current-sprint live and stores the bare integer' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            $script:createPayloads = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $joined = $Argv -join ' '
                $script:calls.Add($joined)
                if ($Argv -contains 'board') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '[{"id":378,"state":"active"}]'; stderr = '' }
                }
                if ($Argv -contains 'create') {
                    $idx = [Array]::IndexOf($Argv, '--from-json')
                    $script:createPayloads.Add([System.IO.File]::ReadAllText([string]$Argv[$idx + 1], [System.Text.Encoding]::UTF8))
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-126"}'; stderr = '' }
                }
                if ($Argv -contains 'assign') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"ok":true}'; stderr = '' }
                }
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-126","fields":{"assignee":{"displayName":"Alice"}}}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $raw = gim-jira-create --summary 'Sprint item' --current-sprint --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $payload = $script:createPayloads[0] | ConvertFrom-Json
            $payload.additionalAttributes.customfield_10020 | Should -Be 378
            ($script:calls[0]) | Should -Match 'board list-sprints'
        }
    }
}

Describe 'gim-jira-transition' {
    InModuleScope GIMTools {
        It 'reads the D0 config statuses and routes' {
            $config = Get-GimJiraTransitionConfig
            $config.ok | Should -BeTrue
            $config.project | Should -Be 'GIM'
            @($config.statuses) | Should -Contain 'To Do'
            @($config.statuses) | Should -Contain 'Done'
            @($config.routes).Count | Should -BeGreaterThan 0
            $route = Get-GimJiraTransitionRoute -Config $config -From 'To Do' -To 'Done'
            $route | Should -Not -BeNullOrEmpty
            ($route.sequence -join ',') | Should -Be 'Done'
        }
        It 'returns a no-op when the current status already matches' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-1","fields":{"status":{"name":"Done"}}}'; stderr = '' }
                }
                throw 'transition must not run on a no-op'
            }
            $raw = gim-jira-transition --key gim-1 --to 'Done' --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.key | Should -Be 'GIM-1'
            $doc.current | Should -Be 'Done'
            $doc.target | Should -Be 'Done'
            @($doc.sequence).Count | Should -Be 0
            @($doc.commands).Count | Should -Be 0
            @($doc.results).Count | Should -Be 0
            $script:calls.Count | Should -Be 1
            $script:calls[0] | Should -Match 'view GIM-1 --fields status --json'
        }
        It 'selects the configured route and runs one transition step' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-1","fields":{"status":{"name":"To Do"}}}'; stderr = '' }
                }
                if ($Argv -contains 'transition') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"successCount":1}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $raw = gim-jira-transition --key GIM-1 --to 'done' --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.dry_run | Should -BeFalse
            $doc.current | Should -Be 'To Do'
            $doc.target | Should -Be 'Done'
            ($doc.sequence -join ',') | Should -Be 'Done'
            ($doc.commands -join "`n") | Should -Match 'workitem transition --key GIM-1 --status Done --yes --json'
            @($doc.results).Count | Should -Be 1
            $doc.results[0].status | Should -Be 'Done'
            $doc.results[0].ok | Should -BeTrue
            $script:calls.Count | Should -Be 2
            $script:calls[1] | Should -Match 'transition --key GIM-1 --status Done --yes --json'
        }
        It 'stops at the first transition failure and reports the exact command' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"key":"GIM-1","status":"In Progress"}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'transition refused' }
            }
            $caught = $null
            try {
                gim-jira-transition --key GIM-1 --to 'Done'
            } catch {
                $caught = [string]$_.Exception.Message
            }
            $caught | Should -Match "transition failed at 'Done'"
            $caught | Should -Match 'workitem transition --key GIM-1 --status Done --yes --json'
            $caught | Should -Match 'transition refused'
            $script:calls.Count | Should -Be 2
        }
        It 'stops after the first failure in a multi-step fixture route' {
            $path = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-transitions-' + [guid]::NewGuid().ToString('N') + '.json')
            try {
                $json = '{"project":"GIM","statuses":[{"name":"A"},{"name":"B"},{"name":"C"}],"routes":[{"from":"A","to":"C","sequence":["B","C"]}]}'
                [System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))
                $script:transitionConfig = Get-GimJiraTransitionConfig -Path $path
                $script:transitionConfig.ok | Should -BeTrue
                ($script:transitionConfig.routes[0].sequence -join ',') | Should -Be 'B,C'
                Mock Get-GimJiraTransitionConfig { $script:transitionConfig }
                $script:calls = New-Object System.Collections.Generic.List[string]
                $script:transitionCalls = 0
                Mock Invoke-GimFleetExec {
                    $script:calls.Add(($Argv -join ' '))
                    if ($Argv -contains 'view') {
                        return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"status":"A"}'; stderr = '' }
                    }
                    $script:transitionCalls++
                    return [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'first step refused for alice@example.com' }
                }
                $caught = $null
                try {
                    gim-jira-transition --key GIM-1 --to 'C'
                } catch {
                    $caught = [string]$_.Exception.Message
                }
                $caught | Should -Match "transition failed at 'B'"
                $caught | Should -Match '<redacted-email>'
                $caught | Should -Not -Match 'alice@example\.com'
                $script:transitionCalls | Should -Be 1
                $joined = $script:calls -join "`n"
                $joined | Should -Match 'transition --key GIM-1 --status B --yes --json'
                $joined | Should -Not -Match 'transition --key GIM-1 --status C --yes --json'
            } finally {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
        It 'reports a missing configured route without transitioning' {
            $path = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-transitions-' + [guid]::NewGuid().ToString('N') + '.json')
            try {
                $json = '{"project":"GIM","statuses":[{"name":"A"},{"name":"B"},{"name":"C"}],"routes":[{"from":"A","to":"B","sequence":["B"]}]}'
                [System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))
                $script:transitionConfig = Get-GimJiraTransitionConfig -Path $path
                Mock Get-GimJiraTransitionConfig { $script:transitionConfig }
                $script:transitionCalls = 0
                Mock Invoke-GimFleetExec {
                    if ($Argv -contains 'view') {
                        return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"status":"A"}'; stderr = '' }
                    }
                    $script:transitionCalls++
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
                }
                { gim-jira-transition --key GIM-1 --to 'C' } | Should -Throw "*no configured route from 'A' to 'C'*"
                $script:transitionCalls | Should -Be 0
            } finally {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
        It 'redacts an email from a current-status view failure' {
            Mock Invoke-GimFleetExec {
                return [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'denied for alice@example.com account 5b10ac8d82e05b22cc7d4ef5' }
            }
            $caught = $null
            try {
                gim-jira-transition --key GIM-1 --to 'Done'
            } catch {
                $caught = [string]$_.Exception.Message
            }
            $caught | Should -Match 'workitem view failed'
            $caught | Should -Match '<redacted-email>'
            $caught | Should -Not -Match 'alice@example\.com'
            $caught | Should -Not -Match '5b10ac8d82e05b22cc7d4ef5'
        }
        It 'allows a project override for a matching key prefix' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"status":"To Do"}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"successCount":1}'; stderr = '' }
            }
            $raw = gim-jira-transition --key ABC-1 --project ABC --to 'Done' --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.project | Should -Be 'ABC'
            $script:calls.Count | Should -Be 2
        }
        It 'no-ops across casing and outer whitespace in the current status' {
            Mock Invoke-GimFleetExec {
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"status":"  dOnE  "}'; stderr = '' }
            }
            $raw = gim-jira-transition --key GIM-1 --to ' done ' --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            @($doc.sequence).Count | Should -Be 0
            @($doc.results).Count | Should -Be 0
        }
        It 'matches a route across casing and outer whitespace in the current status' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains 'view') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"status":"  to do  "}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"successCount":1}'; stderr = '' }
            }
            $raw = gim-jira-transition --key GIM-1 --to 'Done' --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            ($doc.sequence -join ',') | Should -Be 'Done'
            $script:calls.Count | Should -Be 2
        }
        It 'fails clearly for a missing or unparseable transition config' {
            $missingPath = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-missing-' + [guid]::NewGuid().ToString('N') + '.json')
            $missing = Get-GimJiraTransitionConfig -Path $missingPath
            $missing.ok | Should -BeFalse
            $missing.error | Should -Match 'transition config not found'

            $badPath = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-jira-bad-' + [guid]::NewGuid().ToString('N') + '.json')
            try {
                [System.IO.File]::WriteAllText($badPath, '{', (New-Object System.Text.UTF8Encoding($false)))
                $bad = Get-GimJiraTransitionConfig -Path $badPath
                $bad.ok | Should -BeFalse
                $bad.error | Should -Match 'transition config is unparseable'
            } finally {
                Remove-Item -LiteralPath $badPath -Force -ErrorAction SilentlyContinue
            }
        }
        It 'previews a dry-run without any acli call' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec { $script:calls.Add(($Argv -join ' ')); return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' } }
            $raw = gim-jira-transition --key GIM-1 --to 'Done' --dry-run --json
            $doc = $raw | ConvertFrom-Json
            $doc.ok | Should -BeTrue
            $doc.dry_run | Should -BeTrue
            $doc.current | Should -Be '<current-status>'
            ($doc.sequence -join ',') | Should -Be 'Done'
            $doc.commands[0] | Should -Match 'workitem transition --key GIM-1 --status Done --yes --json'
            @($doc.results).Count | Should -Be 0
            $script:calls.Count | Should -Be 0
        }
        It 'rejects an unknown target and lists valid statuses' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec { $script:calls.Add(($Argv -join ' ')); return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' } }
            { gim-jira-transition --key GIM-1 --to 'Blocked' } | Should -Throw '*unknown --to*valid statuses*To Do*In Progress*In Review*Done*'
            $script:calls.Count | Should -Be 0
        }
        It 'rejects a key from another project' {
            { gim-jira-transition --key ABC-1 --to 'Done' } | Should -Throw "*does not belong to project 'GIM'*"
        }
        It 'emits a JSON envelope for an argument error' {
            $modulePath = Join-Path $script:GimModuleRoot 'GIMTools\GIMTools.psd1'
            $pwshPath = (Get-Process -Id $PID).Path
            $scriptText = "Import-Module '$modulePath' -Force -DisableNameChecking; gim-jira-transition --to Done --json"
            $run = Invoke-GimFleetExec -Argv @($pwshPath, '-NoProfile', '-Command', $scriptText) -WorkingDirectory (Get-GimTestRepoRoot) -TimeoutSeconds 60
            $run.exit_code | Should -Be 1
            $doc = $run.stdout | ConvertFrom-Json
            $doc.ok | Should -BeFalse
            $doc.error | Should -Match '--key'
            foreach ($name in @('ok', 'dry_run', 'key', 'project', 'current', 'target', 'sequence', 'commands', 'results', 'error')) {
                @($doc.PSObject.Properties.Name) | Should -Contain $name
            }
        }
    }
}

function global:New-GimWrapupFixture {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowEmptyCollection()][object[]]$Checks1 = @(),
        [AllowEmptyCollection()][object[]]$Checks2 = @(),
        [AllowEmptyCollection()][object[]]$TopLevelComments1 = @(),
        [string]$LogText = "job log`n",
        [AllowEmptyString()][string]$ReviewError2 = ''
    )
    $view = {
        param($number, $head, $base)
        [ordered]@{
            number = $number; url = ('https://github.com/owner/repo/pull/' + $number)
            title = ('PR ' + $number); state = 'OPEN'; isDraft = $false
            baseRefName = $base; headRefName = $head
            author = [ordered]@{ login = 'octocat' }
            reviewDecision = ''; mergeable = 'MERGEABLE'; mergeStateStatus = 'CLEAN'
            comments = @(); reviews = @()
        }
    }
    $review = [ordered]@{
        reviews = @(
            [ordered]@{
                comments = @(
                    [ordered]@{
                        thread_id = 'PRRT_1'; path = 'a.cs'; line = 42
                        author_login = 'reviewer'; body = 'inline body'
                        is_resolved = $false; is_outdated = $false
                        thread_comments = @(
                            [ordered]@{ author = 'agent'; body = 'reply body'; created_at = '2026-01-01T00:00:00Z' }
                        )
                    }
                )
            }
        )
    }
    $v1 = & $view 1 'feature/a' 'dev'
    $v1.comments = @($TopLevelComments1)
    $rec1 = [ordered]@{ view = $v1; checks = @($Checks1); review = [ordered]@{ reviews = @() } }
    $rec2 = [ordered]@{ view = (& $view 2 'feature/b' 'feature/a'); checks = @($Checks2); review = $review }
    if ($ReviewError2 -ne '') { $rec2.reviewError = $ReviewError2 }
    $rec3 = [ordered]@{ view = (& $view 3 'feature/c' 'feature/b'); checks = @(); review = [ordered]@{ reviews = @() } }
    $fixture = [ordered]@{
        openPrs = @(
            [ordered]@{ number = 1; headRefName = 'feature/a'; baseRefName = 'dev'; url = 'https://github.com/owner/repo/pull/1' }
            [ordered]@{ number = 2; headRefName = 'feature/b'; baseRefName = 'feature/a'; url = 'https://github.com/owner/repo/pull/2' }
            [ordered]@{ number = 3; headRefName = 'feature/c'; baseRefName = 'feature/b'; url = 'https://github.com/owner/repo/pull/3' }
        )
        prs = [ordered]@{ '1' = $rec1; '2' = $rec2; '3' = $rec3 }
        logs = [ordered]@{ 'job-300' = [ordered]@{ ok = $true; text = $LogText }; 'job-1' = [ordered]@{ ok = $true; text = $LogText }; 'job-2' = [ordered]@{ ok = $true; text = $LogText } }
    }
    [System.IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $fixture -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
}

Describe 'gim-wrapup shared helpers' {
    InModuleScope GIMTools {
        It 'normalizes timestamps through one shared helper' {
            (ConvertTo-GimWrapupIsoTimestamp -Value ([datetime]::SpecifyKind([datetime]'2026-01-01T05:06:07.089', [System.DateTimeKind]::Utc))) |
                Should -Be '2026-01-01T05:06:07.089Z'
            (ConvertTo-GimWrapupIsoTimestamp -Value $null) | Should -Be ''
            (ConvertTo-GimWrapupIsoTimestamp -Value '2026-01-01T05:06:07Z') | Should -Be '2026-01-01T05:06:07Z'
        }
    }
}

Describe 'gim-wrapup-gather shim' {
    InModuleScope GIMTools {
        It 'forwards flags to the helper and writes the compact envelope and file' {
            $dir = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-wrapup-stub-' + [guid]::NewGuid().ToString('N'))
            $outDir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-stub-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            [void][System.IO.Directory]::CreateDirectory($outDir)
            $stub = Join-Path $script:GimModuleRoot ('helpers\test-stub-' + [guid]::NewGuid().ToString('N') + '.mjs')
            $argsFile = Join-Path $dir 'args.json'
            $out = Join-Path $outDir 'gather.json'
            $stubText = @'
import fs from 'node:fs';
const args = process.argv.slice(2);
fs.writeFileSync(process.env.GIM_WRAPUP_TEST_ARGS_FILE, JSON.stringify(args));
const doc = {schema_version:1,generated_at:'2026-01-01T00:00:00Z',repo:'owner/repo',input:{mode:'pr',from:'40',discovered:[40]},prs:[],warnings:[],summary:{prs:0,checks_failing:0,checks_pending:0,threads_open:0,logs_captured:0}};
const responses = {repo:'owner/repo',generated_at:'2026-01-01T00:00:00Z',prs:[]};
process.stdout.write(JSON.stringify({document:doc,documentJson:JSON.stringify(doc,null,1),responses,warnings:[],logManifest:[]}));
'@
            [System.IO.File]::WriteAllText($stub, $stubText, (New-Object System.Text.UTF8Encoding($false)))
            $oldHelper = $env:GIM_WRAPUP_GATHER_HELPER
            $oldArgs = $env:GIM_WRAPUP_TEST_ARGS_FILE
            try {
                $env:GIM_WRAPUP_GATHER_HELPER = $stub
                $env:GIM_WRAPUP_TEST_ARGS_FILE = $argsFile
                $raw = gim-wrapup-gather --repo GIMmos --pr 40 --out $out --parallel 3 --log-parallel 2 --json
                $result = $raw | ConvertFrom-Json
                $result.ok | Should -BeTrue
                $result.path | Should -Be $out
                $result.repo | Should -Be 'owner/repo'
                foreach ($name in @('ok', 'path', 'repo', 'summary', 'warnings', 'error')) {
                    @($result.PSObject.Properties.Name) | Should -Contain $name
                }
                $rawFile = [System.IO.File]::ReadAllText($out, [System.Text.Encoding]::UTF8)
                ($rawFile) | Should -Match "`n `"schema_version`":"
                $file = $rawFile | ConvertFrom-Json
                $file.schema_version | Should -Be 1
                (Test-Path -LiteralPath (Join-Path $outDir 'responses.json')) | Should -BeTrue
                $forwarded = [System.IO.File]::ReadAllText($argsFile) | ConvertFrom-Json
                ($forwarded -join ' ') | Should -Match '--mode pr'
                ($forwarded -join ' ') | Should -Match '--pr 40'
                ($forwarded -join ' ') | Should -Match '--parallel 3'
                ($forwarded -join ' ') | Should -Match '--log-parallel 2'
                $raw2 = gim-wrapup-gather --repo GIMmos --pr 40 --out $out --json
                $result2 = $raw2 | ConvertFrom-Json
                $result2.ok | Should -BeTrue
                @([System.IO.Directory]::GetFiles($outDir, '*.tmp')).Count | Should -Be 0
            } finally {
                $env:GIM_WRAPUP_GATHER_HELPER = $oldHelper
                $env:GIM_WRAPUP_TEST_ARGS_FILE = $oldArgs
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $stub -Force -ErrorAction SilentlyContinue
            }
        }
        It 'removes a prior owned logs dir on a successful no-log rerun' {
            $dir = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-wrapup-stub-' + [guid]::NewGuid().ToString('N'))
            $outDir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-stub-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            [void][System.IO.Directory]::CreateDirectory((Join-Path $outDir 'logs'))
            $stub = Join-Path $script:GimModuleRoot ('helpers\test-stub-' + [guid]::NewGuid().ToString('N') + '.mjs')
            $out = Join-Path $outDir 'gather.json'
            $stubText = @'
const doc = {schema_version:1,generated_at:'2026-01-01T00:00:00Z',repo:'owner/repo',input:{mode:'pr',from:'40',discovered:[40]},prs:[],warnings:[],summary:{prs:0,checks_failing:0,checks_pending:0,threads_open:0,logs_captured:0}};
const responses = {repo:'owner/repo',generated_at:'2026-01-01T00:00:00Z',prs:[]};
process.stdout.write(JSON.stringify({document:doc,documentJson:JSON.stringify(doc,null,1),responses,warnings:[],logManifest:[]}));
'@
            [System.IO.File]::WriteAllText($stub, $stubText, (New-Object System.Text.UTF8Encoding($false)))
            [System.IO.File]::WriteAllText((Join-Path $outDir 'logs\.gim-wrapup-gather.logs'), 'gim-wrapup-gather', (New-Object System.Text.UTF8Encoding($false)))
            [System.IO.File]::WriteAllText((Join-Path $outDir 'logs\old.log'), 'old', (New-Object System.Text.UTF8Encoding($false)))
            $oldHelper = $env:GIM_WRAPUP_GATHER_HELPER
            try {
                $env:GIM_WRAPUP_GATHER_HELPER = $stub
                gim-wrapup-gather --repo GIMmos --pr 40 --out $out --json | Out-Null
                (Test-Path -LiteralPath (Join-Path $outDir 'logs')) | Should -BeFalse
            } finally {
                $env:GIM_WRAPUP_GATHER_HELPER = $oldHelper
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $stub -Force -ErrorAction SilentlyContinue
            }
        }
        It 'leaves responses.json untouched on a zero-PR partial gather' {
            $outDir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-stub-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($outDir)
            $stub = Join-Path $script:GimModuleRoot ('helpers\test-stub-' + [guid]::NewGuid().ToString('N') + '.mjs')
            $out = Join-Path $outDir 'gather.json'
            $responsesPath = Join-Path $outDir 'responses.json'
            $before = '{"repo":"owner/repo","generated_at":"2026-01-01T00:00:00.000Z","prs":[{"pr":50,"threads":[],"comments":["keep me"]}]}'
            [System.IO.File]::WriteAllText($responsesPath, $before, (New-Object System.Text.UTF8Encoding($false)))
            $stubText = @'
const doc = {schema_version:1,generated_at:'2026-01-01T00:00:00.000Z',repo:'owner/repo',input:{mode:'chain',from:'40',discovered:[40,41]},prs:[],warnings:['fetch budget of 600s reached; gather is partial'],summary:{prs:0,checks_failing:0,checks_pending:0,threads_open:0,logs_captured:0}};
const responses = {repo:'owner/repo',generated_at:'2026-01-01T00:00:00.000Z',prs:[]};
process.stdout.write(JSON.stringify({document:doc,documentJson:JSON.stringify(doc,null,1),responses,warnings:doc.warnings,logManifest:[]}));
'@
            [System.IO.File]::WriteAllText($stub, $stubText, (New-Object System.Text.UTF8Encoding($false)))
            $oldHelper = $env:GIM_WRAPUP_GATHER_HELPER
            try {
                $env:GIM_WRAPUP_GATHER_HELPER = $stub
                gim-wrapup-gather --repo GIMmos --chain 40 --out $out --json | Out-Null
                ([System.IO.File]::ReadAllText($responsesPath)) | Should -Be $before
                (Test-Path -LiteralPath $out) | Should -BeTrue
            } finally {
                $env:GIM_WRAPUP_GATHER_HELPER = $oldHelper
                Remove-Item -LiteralPath $stub -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'rejects mixed modes and an out-path escape before spawning a helper' {
            { gim-wrapup-gather --repo GIMmos --chain 1 --pr 40 } | Should -Throw '*exactly one of --chain, --pr, or --branch*'
            $repoLeaf = Split-Path -Leaf (Resolve-GimFleetRepo -Requested 'GIMmos')
            { gim-wrapup-gather --repo GIMmos --pr 40 --out ..\gim-wrapup-escape.json } | Should -Throw "*write path not contained in repo '$repoLeaf'*"
        }
    }
}

Describe 'gim-wrapup-gather helper' {
    InModuleScope GIMTools {

        It 'maps live reviews[].comments[] plus replies and captures a job log' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            $logsDir = Join-Path $dir 'temp-logs'
            try {
                $checks = @([ordered]@{ name = 'ci'; state = 'FAILURE'; bucket = 'fail'; link = 'https://github.com/owner/repo/actions/runs/100/job/300'; description = ''; startedAt = ''; completedAt = '' })
                New-GimWrapupFixture -Path $fixturePath -Checks2 $checks -LogText ("log line`n" * 20)
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'chain', '--chain', '2', '--parallel', '2', '--log-parallel', '1', '--logs-dir', $logsDir, '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 0
                $out = $run.stdout | ConvertFrom-Json
                $doc = $out.document
                ($doc.input.discovered -join ',') | Should -Be '1,2,3'
                (($doc.prs | ForEach-Object { $_.number }) -join ',') | Should -Be '1,2,3'
                $doc.prs[1].threads[0].thread_id | Should -Be 'PRRT_1'
                $doc.prs[1].threads[0].replies[0].author | Should -Be 'agent'
                $doc.prs[1].threads[0].replies[0].body | Should -Be 'reply body'
                $doc.prs[1].checks[0].log_file | Should -Be 'logs/job-300-ci-2-0.log'
                $doc.prs[1].checks[0].log_source | Should -Be 'job'
                $doc.prs[1].checks[0].log_truncated | Should -BeFalse
                $doc.summary.logs_captured | Should -Be 1
                (Test-Path -LiteralPath (Join-Path $logsDir 'job-300-ci-2-0.log')) | Should -BeTrue
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'guards chain cycles, ambiguity, and the 50 PR cap' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                New-GimWrapupFixture -Path $fixturePath
                $cycle = Get-Content $fixturePath -Raw | ConvertFrom-Json
                $cycle.openPrs[0].baseRefName = 'feature/b'
                $cycle.openPrs[1].baseRefName = 'feature/a'
                [System.IO.File]::WriteAllText($fixturePath, ($cycle | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $cycleRun = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'chain', '--chain', '1', '--logs-dir', (Join-Path $dir 'l1'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $cycleDoc = ($cycleRun.stdout | ConvertFrom-Json).document
                ($cycleDoc.warnings -join ' ') | Should -Match 'cycle detected'

                New-GimWrapupFixture -Path $fixturePath
                $ambiguous = Get-Content $fixturePath -Raw | ConvertFrom-Json
                $ambiguous.openPrs[2].baseRefName = 'feature/a'
                [System.IO.File]::WriteAllText($fixturePath, ($ambiguous | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $ambiguousRun = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'chain', '--chain', '1', '--logs-dir', (Join-Path $dir 'l2'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $ambiguousDoc = ($ambiguousRun.stdout | ConvertFrom-Json).document
                ($ambiguousDoc.warnings -join ' ') | Should -Match 'multiple open PRs have base'

                $open = New-Object System.Collections.Generic.List[object]
                $prs = [ordered]@{}
                for ($i = 1; $i -le 51; $i++) {
                    $base = if ($i -eq 1) { 'dev' } else { 'feature/' + ($i - 1) }
                    [void]$open.Add([ordered]@{ number = $i; headRefName = ('feature/' + $i); baseRefName = $base; url = ('https://github.com/owner/repo/pull/' + $i) })
                    $prs[[string]$i] = [ordered]@{
                        view = [ordered]@{ number = $i; url = ('https://github.com/owner/repo/pull/' + $i); title = 't'; state = 'OPEN'; isDraft = $false; baseRefName = $base; headRefName = ('feature/' + $i); author = [ordered]@{ login = 'octocat' }; reviewDecision = ''; mergeable = 'MERGEABLE'; mergeStateStatus = 'CLEAN'; comments = @(); reviews = @() }
                        checks = @(); review = [ordered]@{ reviews = @() }
                    }
                }
                $bigFixture = [ordered]@{ openPrs = @($open.ToArray()); prs = $prs; logs = [ordered]@{} }
                [System.IO.File]::WriteAllText($fixturePath, ($bigFixture | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $bigRun = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'chain', '--chain', '25', '--parallel', '8', '--logs-dir', (Join-Path $dir 'l3'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 120
                $bigDoc = ($bigRun.stdout | ConvertFrom-Json).document
                @($bigDoc.prs).Count | Should -Be 50
                ($bigDoc.warnings -join ' ') | Should -Match '50 PR cap'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'warns instead of failing when a successful log fetch is empty' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                $checks = @([ordered]@{ name = 'ci'; state = 'FAILURE'; bucket = 'fail'; link = 'https://github.com/owner/repo/actions/runs/100/job/300'; description = ''; startedAt = ''; completedAt = '' })
                New-GimWrapupFixture -Path $fixturePath -Checks2 $checks
                $fixture = Get-Content $fixturePath -Raw | ConvertFrom-Json
                $fixture.logs.'job-300'.text = ''
                Add-Member -InputObject $fixture.logs -NotePropertyName 'run-100' -NotePropertyValue ([pscustomobject]@{ ok = $true; text = '' }) -Force
                [System.IO.File]::WriteAllText($fixturePath, ($fixture | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', (Join-Path $dir 'logs'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 0
                $doc = ($run.stdout | ConvertFrom-Json).document
                $doc.prs[0].checks[0].log_file | Should -BeNullOrEmpty
                $doc.prs[0].checks[0].log_warning | Should -Match 'no log output'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'falls back to the run log when the job fetch fails' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                $checks = @([ordered]@{ name = 'ci'; state = 'FAILURE'; bucket = 'fail'; link = 'https://github.com/owner/repo/actions/runs/100/job/300'; description = ''; startedAt = ''; completedAt = '' })
                New-GimWrapupFixture -Path $fixturePath -Checks2 $checks
                $fixture = Get-Content $fixturePath -Raw | ConvertFrom-Json
                $fixture.logs.'job-300'.ok = $false
                $fixture.logs.'job-300'.text = 'partial job log'
                Add-Member -InputObject $fixture.logs -NotePropertyName 'run-100' -NotePropertyValue ([pscustomobject]@{ ok = $true; text = 'fallback log' }) -Force
                [System.IO.File]::WriteAllText($fixturePath, ($fixture | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', (Join-Path $dir 'logs'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 0
                $doc = ($run.stdout | ConvertFrom-Json).document
                $doc.prs[0].checks[0].log_file | Should -Be 'logs/run-100-ci-2-0.log'
                $doc.prs[0].checks[0].log_source | Should -Be 'run'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'serializes 1-space JSON, omits nulls, and reduces passing checks' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                $checks = @([ordered]@{ name = 'ok'; state = 'SUCCESS'; bucket = 'pass'; link = ''; description = ''; startedAt = ''; completedAt = '' })
                New-GimWrapupFixture -Path $fixturePath -Checks2 $checks
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', (Join-Path $dir 'logs'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $out = $run.stdout | ConvertFrom-Json
                $out.documentJson | Should -Match "`n `"schema_version`":"
                $out.documentJson | Should -Not -Match 'log_warning"\s*:\s*null'
                $out.documentJson | Should -Not -Match '"response"'
                $check = $out.document.prs[0].checks[0]
                $check.name | Should -Be 'ok'
                $check.state | Should -Be 'SUCCESS'
                $check.bucket | Should -BeNullOrEmpty
                @($out.responses.prs[0].threads).Count | Should -Be 1
                $out.responses.prs[0].threads[0].thread_id | Should -Be 'PRRT_1'
                $out.responses.prs[0].threads[0].resolve | Should -BeTrue
                $out.responses.prs[0].threads[0].response | Should -Be ''
                @($out.responses.prs[0].comments).Count | Should -Be 0
                $out.document.prs[0].threads[0].PSObject.Properties.Name | Should -Not -Contain 'response'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'includes a top-level-comment-only PR in the responses shell' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                $topComments = @([ordered]@{ id = 'IC_1'; author = [ordered]@{ login = 'octocat' }; body = 'top-level body' })
                New-GimWrapupFixture -Path $fixturePath -TopLevelComments1 $topComments
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'chain', '--chain', '1', '--logs-dir', (Join-Path $dir 'logs'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $out = $run.stdout | ConvertFrom-Json
                @($out.responses.prs).Count | Should -Be 2
                $out.responses.prs[0].pr | Should -Be 1
                @($out.responses.prs[0].threads).Count | Should -Be 0
                @($out.responses.prs[0].comments).Count | Should -Be 0
                $out.responses.prs[1].pr | Should -Be 2
                @($out.responses.prs[1].threads).Count | Should -Be 1
                $top = $out.document.prs[0].top_level_comments[0]
                $top.id | Should -Be 'IC_1'
                $top.body | Should -Be 'top-level body'
                $top.PSObject.Properties.Name | Should -Not -Contain 'response'
                $top.PSObject.Properties.Name | Should -Not -Contain 'posted_at'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'merges authored responses by thread id and drops resolved entries' {
            $new = [pscustomobject]@{
                repo = 'owner/repo'; generated_at = '2026-01-01T00:00:00.000Z'
                prs = @([pscustomobject]@{
                    pr = 40
                    threads = @(
                        [pscustomobject]@{ thread_id = 'PRRT_1'; resolve = $true; response = '' }
                        [pscustomobject]@{ thread_id = 'PRRT_2'; resolve = $true; response = '' }
                    )
                    comments = @()
                })
            }
            $existing = [pscustomobject]@{
                repo = 'owner/repo'; generated_at = '2025-01-01T00:00:00.000Z'
                prs = @(
                    [pscustomobject]@{
                        pr = 40
                        threads = @(
                            [pscustomobject]@{ thread_id = 'PRRT_1'; resolve = $false; response = 'authored' }
                            [pscustomobject]@{ thread_id = 'PRRT_9'; resolve = $true; response = 'resolved remotely' }
                        )
                        comments = @('old comment')
                    }
                    [pscustomobject]@{
                        pr = 50; threads = @(); comments = @('keep absent PR comment')
                    }
                )
            }
            $merged = Merge-GimWrapupResponses -New $new -Existing $existing
            @($merged.prs).Count | Should -Be 2
            @($merged.prs[0].threads).Count | Should -Be 2
            $merged.prs[0].threads[0].response | Should -Be 'authored'
            $merged.prs[0].threads[0].resolve | Should -BeFalse
            $merged.prs[0].threads[1].response | Should -Be ''
            $merged.prs[0].threads[1].resolve | Should -BeTrue
            ($merged.prs[0].comments -join ',') | Should -Be 'old comment'
            $merged.prs[1].pr | Should -Be 50
            ($merged.prs[1].comments -join ',') | Should -Be 'keep absent PR comment'
        }
        It 'creates no logs dir on the happy path' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            $logsDir = Join-Path $dir 'temp-logs'
            try {
                New-GimWrapupFixture -Path $fixturePath
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '1', '--logs-dir', $logsDir, '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 0
                $doc = ($run.stdout | ConvertFrom-Json).document
                @($doc.prs[0].checks).Count | Should -Be 0
                (Test-Path -LiteralPath $logsDir) | Should -BeFalse
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'truncates logs at the configured caps and warns on missing ids' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            $logsDir = Join-Path $dir 'temp-logs'
            try {
                $checks = @([ordered]@{ name = 'ci'; state = 'FAILURE'; bucket = 'fail'; link = 'https://github.com/owner/repo/actions/runs/100/job/300'; description = ''; startedAt = ''; completedAt = '' })
                New-GimWrapupFixture -Path $fixturePath -Checks2 $checks -LogText ('z' * 400)
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', $logsDir, '--fixture', $fixturePath, '--log-file-cap-bytes', '100', '--log-total-cap-bytes', '150') -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 0
                $doc = ($run.stdout | ConvertFrom-Json).document
                $doc.prs[0].checks[0].log_truncated | Should -BeTrue
                ([System.IO.File]::ReadAllText((Join-Path $logsDir 'job-300-ci-2-0.log'))) | Should -Match 'gim: log truncated'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'stops log capture at the total cap' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            $logsDir = Join-Path $dir 'temp-logs'
            try {
                $checks = @(
                    [ordered]@{ name = 'a'; state = 'FAILURE'; bucket = 'fail'; link = 'https://github.com/owner/repo/actions/runs/1/job/1'; description = ''; startedAt = ''; completedAt = '' }
                    [ordered]@{ name = 'b'; state = 'FAILURE'; bucket = 'fail'; link = 'https://github.com/owner/repo/actions/runs/2/job/2'; description = ''; startedAt = ''; completedAt = '' }
                )
                New-GimWrapupFixture -Path $fixturePath -Checks2 $checks -LogText ('z' * 400)
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', $logsDir, '--fixture', $fixturePath, '--log-file-cap-bytes', '100', '--log-total-cap-bytes', '100') -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 0
                $doc = ($run.stdout | ConvertFrom-Json).document
                $doc.prs[0].checks[0].log_truncated | Should -BeTrue
                $doc.prs[0].checks[1].log_file | Should -BeNullOrEmpty
                $doc.prs[0].checks[1].log_warning | Should -Match 'total gather cap'
                $doc.summary.logs_captured | Should -Be 1
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'warns and skips a log when the check link has no run id' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                $checks = @([ordered]@{ name = 'ci'; state = 'FAILURE'; bucket = 'fail'; link = 'https://github.com/owner/repo/checks'; description = ''; startedAt = ''; completedAt = '' })
                New-GimWrapupFixture -Path $fixturePath -Checks2 $checks
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', (Join-Path $dir 'logs'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $doc = ($run.stdout | ConvertFrom-Json).document
                $doc.prs[0].checks[0].log_file | Should -BeNullOrEmpty
                $doc.prs[0].checks[0].log_source | Should -BeNullOrEmpty
                $doc.prs[0].checks[0].log_warning | Should -Match 'no parseable Actions run id'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'rejects PRs from different repositories' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                New-GimWrapupFixture -Path $fixturePath
                $fixture = Get-Content $fixturePath -Raw | ConvertFrom-Json
                $fixture.prs.'2'.view.url = 'https://github.com/other/repo/pull/2'
                [System.IO.File]::WriteAllText($fixturePath, ($fixture | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '1', '--pr', '2', '--logs-dir', (Join-Path $dir 'logs'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 1
                $run.stderr | Should -Match 'PRs span different repositories'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'fails fatally on checks failure and missing extension' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                New-GimWrapupFixture -Path $fixturePath
                $checksFixture = Get-Content $fixturePath -Raw | ConvertFrom-Json
                Add-Member -InputObject $checksFixture.prs.'2' -NotePropertyName checksError -NotePropertyValue 'gh auth failed' -Force
                [System.IO.File]::WriteAllText($fixturePath, ($checksFixture | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $badChecks = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', (Join-Path $dir 'l1'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $badChecks.exit_code | Should -Be 1
                $badChecks.stderr | Should -Match 'gh pr checks failed.*gh auth failed'

                New-GimWrapupFixture -Path $fixturePath -ReviewError2 'unknown command pr-review'
                $badReview = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'pr', '--pr', '2', '--logs-dir', (Join-Path $dir 'l2'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $badReview.exit_code | Should -Be 1
                $badReview.stderr | Should -Match 'gh-pr-review extension is required'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'warns and writes a partial document when the fetch budget is zero' {
            $repo = Get-GimTestRepoRoot
            $helper = Join-Path $script:GimModuleRoot 'helpers\gim-wrapup-gather.mjs'
            $dir = Join-Path $repo ('.agent-scratch\gim-wrapup-helper-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $fixturePath = Join-Path $dir 'fixture.json'
            try {
                New-GimWrapupFixture -Path $fixturePath
                $run = Invoke-GimFleetExec -Argv @('node', $helper, '--repo-root', $repo, '--mode', 'chain', '--chain', '2', '--budget-seconds', '0', '--logs-dir', (Join-Path $dir 'logs'), '--fixture', $fixturePath) -WorkingDirectory $repo -TimeoutSeconds 60
                $run.exit_code | Should -Be 0
                $doc = ($run.stdout | ConvertFrom-Json).document
                @($doc.prs).Count | Should -Be 0
                ($doc.warnings -join ' ') | Should -Match 'fetch budget.*partial'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function global:New-GimWrapupRespondFixture {
    param([Parameter(Mandatory = $true)][string]$Path)
    $doc = [ordered]@{
        repo = 'owner/repo'
        generated_at = '2026-01-01T00:00:00.000Z'
        prs = @(
            [ordered]@{
                pr = 40
                threads = @(
                    [ordered]@{ thread_id = 'PRRT_1'; resolve = $true; response = 'Thanks' }
                    [ordered]@{ thread_id = 'PRRT_2'; resolve = $false; response = 'No resolve' }
                    [ordered]@{ thread_id = 'PRRT_3'; resolve = $true; response = '' }
                )
                comments = @('Top comment', '   ', 'Second comment')
            }
        )
    }
    [System.IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $doc -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
}

Describe 'gim-wrapup-respond' {
    InModuleScope GIMTools {
        It 'plans dry-run actions with zero gh calls and no receipt write' {
            $dir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-respond-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $file = Join-Path $dir 'responses.json'
            try {
                New-GimWrapupRespondFixture -Path $file
                $before = [System.IO.File]::ReadAllText($file)
                Mock Invoke-GimFleetExec { throw 'gh must not run in dry-run' }
                $raw = gim-wrapup-respond --repo GIMmos --file $file --harness test-harness --dry-run --json
                $result = $raw | ConvertFrom-Json
                $result.ok | Should -BeTrue
                $result.dry_run | Should -BeTrue
                $result.posted | Should -Be 4
                $result.resolved | Should -Be 1
                $result.skipped | Should -Be 2
                @($result.actions).Count | Should -Be 5
                ([System.IO.File]::ReadAllText($file)) | Should -Be $before
                (Test-Path -LiteralPath (Join-Path $dir 'responses.result.json')) | Should -BeFalse
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'replies before resolving, posts comments in order, and writes a receipt' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            $script:bodies = @{}
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains '--include-comment-node-id') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"reviews":[{"comments":[{"thread_id":"PRRT_1","is_resolved":false},{"thread_id":"PRRT_2","is_resolved":false}]}]}'; stderr = '' }
                }
                if ($Argv -contains 'reply') {
                    $idx = [Array]::IndexOf($Argv, '--thread-id')
                    $bodyIdx = [Array]::IndexOf($Argv, '--body')
                    $script:bodies[[string]$Argv[$idx + 1]] = [string]$Argv[$bodyIdx + 1]
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $dir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-respond-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $file = Join-Path $dir 'responses.json'
            try {
                New-GimWrapupRespondFixture -Path $file
                $raw = gim-wrapup-respond --repo GIMmos --file $file --harness test-harness --json
                $result = $raw | ConvertFrom-Json
                $result.error | Should -BeNullOrEmpty
                $result.ok | Should -BeTrue
                $result.posted | Should -Be 4
                $result.resolved | Should -Be 1
                $result.skipped | Should -Be 2
                $footer = '__this action was performed by test-harness automatically__'
                ([regex]::Matches($script:bodies['PRRT_1'], [regex]::Escape($footer))).Count | Should -Be 1
                $replyIndex = -1
                $resolveIndex = -1
                for ($i = 0; $i -lt $script:calls.Count; $i++) {
                    if ($script:calls[$i] -match 'comments reply --thread-id PRRT_1') { $replyIndex = $i }
                    if ($script:calls[$i] -match 'threads resolve --thread-id PRRT_1') { $resolveIndex = $i }
                }
                $replyIndex | Should -BeGreaterThan -1
                $resolveIndex | Should -BeGreaterThan -1
                $replyIndex | Should -BeLessThan $resolveIndex
                ($script:calls -join "`n") | Should -Not -Match 'threads resolve --thread-id PRRT_2'
                $commentCalls = @($script:calls | Where-Object { $_ -match '^gh pr comment 40 ' })
                $commentCalls.Count | Should -Be 2
                $commentCalls[0] | Should -Match 'Top comment'
                $commentCalls[1] | Should -Match 'Second comment'
                $receipt = [System.IO.File]::ReadAllText((Join-Path $dir 'responses.result.json')) | ConvertFrom-Json
                $receipt.prs[0].threads[0].posted_at.ToString('yyyy-MM-ddTHH:mm:ss.fffZ') | Should -Match '\.\d{3}Z$'
                $receipt.prs[0].threads[0].resolved_at.ToString('yyyy-MM-ddTHH:mm:ss.fffZ') | Should -Match '\.\d{3}Z$'
                $receipt.prs[0].threads[0].remote_state | Should -Be 'open'
                $receipt.prs[0].comments[0].posted_at | Should -Not -BeNullOrEmpty
                @([System.IO.Directory]::GetFiles($dir, '.respond-receipt-*.tmp')).Count | Should -Be 0
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'skips a remotely resolved thread without posting and marks it resolved' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains '--include-comment-node-id') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"reviews":[{"comments":[{"thread_id":"PRRT_1","is_resolved":true},{"thread_id":"PRRT_2","is_resolved":true}]}]}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $dir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-respond-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $file = Join-Path $dir 'responses.json'
            try {
                New-GimWrapupRespondFixture -Path $file
                $remoteProbe = '{"reviews":[{"comments":[{"thread_id":"PRRT_1","is_resolved":true}]}]}' | ConvertFrom-Json
                $remoteMapProbe = ConvertFrom-GimWrapupRemoteThreads -Document $remoteProbe
                $remoteMapProbe['PRRT_1'].remote_state | Should -Be 'resolved'
                $raw = gim-wrapup-respond --repo GIMmos --file $file --harness test-harness --json
                $result = $raw | ConvertFrom-Json
                $result.ok | Should -BeTrue
                ($script:calls -join "`n") | Should -Match 'include-comment-node-id'
                ($result.warnings -join ' ') | Should -Match 'already resolved remotely'
                $result.posted | Should -Be 2
                $result.resolved | Should -Be 0
                ($script:calls -join "`n") | Should -Not -Match 'comments reply'
                ($script:calls -join "`n") | Should -Not -Match 'threads resolve'
                ($result.warnings -join ' ') | Should -Match 'already resolved remotely'
                $receipt = [System.IO.File]::ReadAllText((Join-Path $dir 'responses.result.json')) | ConvertFrom-Json
                $receipt.prs[0].threads[0].remote_state | Should -Be 'resolved'
                $receipt.prs[0].threads[0].resolved_at | Should -Not -BeNullOrEmpty
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'retries resolve for a locally resolved entry that is remotely open' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains '--include-comment-node-id') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"reviews":[{"comments":[{"thread_id":"PRRT_1","is_resolved":false},{"thread_id":"PRRT_2","is_resolved":false}]}]}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $dir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-respond-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $file = Join-Path $dir 'responses.json'
            $receiptPath = Join-Path $dir 'responses.result.json'
            try {
                New-GimWrapupRespondFixture -Path $file
                $receipt = [ordered]@{ schema_version = 1; repo = 'owner/repo'; generated_at = '2026-01-01T00:00:00.000Z'; prs = @(
                    [ordered]@{ pr = 40; threads = @([ordered]@{ thread_id = 'PRRT_1'; posted_at = '2026-01-01T00:00:00.000Z'; resolved_at = '2026-01-01T00:00:00.000Z'; remote_state = 'resolved'; last_error = $null }); comments = @() })
                }
                [System.IO.File]::WriteAllText($receiptPath, (ConvertTo-Json -InputObject $receipt -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $raw = gim-wrapup-respond --repo GIMmos --file $file --harness test-harness --json
                $result = $raw | ConvertFrom-Json
                $result.resolved | Should -Be 1
                ($script:calls -join "`n") | Should -Match 'threads resolve --thread-id PRRT_1'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'skips completed receipt entries and retries failures' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains '--include-comment-node-id') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"reviews":[{"comments":[{"thread_id":"PRRT_1","is_resolved":true},{"thread_id":"PRRT_2","is_resolved":false}]}]}'; stderr = '' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $dir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-respond-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $file = Join-Path $dir 'responses.json'
            $receiptPath = Join-Path $dir 'responses.result.json'
            try {
                New-GimWrapupRespondFixture -Path $file
                $receipt = [ordered]@{ schema_version = 1; repo = 'owner/repo'; generated_at = '2026-01-01T00:00:00.000Z'; prs = @(
                    [ordered]@{ pr = 40; threads = @(
                        [ordered]@{ thread_id = 'PRRT_1'; posted_at = '2026-01-01T00:00:00.000Z'; resolved_at = '2026-01-01T00:00:01.000Z'; remote_state = 'resolved'; last_error = $null }
                        [ordered]@{ thread_id = 'PRRT_2'; posted_at = $null; resolved_at = $null; remote_state = 'open'; last_error = 'previous failure' }
                    ); comments = @(
                        [ordered]@{ key = (Get-GimWrapupCommentKey -Text 'Top comment'); posted_at = '2026-01-01T00:00:00.000Z'; last_error = $null }
                        [ordered]@{ key = (Get-GimWrapupCommentKey -Text 'Second comment'); posted_at = '2026-01-01T00:00:00.000Z'; last_error = $null }
                    ) })
                }
                [System.IO.File]::WriteAllText($receiptPath, (ConvertTo-Json -InputObject $receipt -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                $raw = gim-wrapup-respond --repo GIMmos --file $file --harness test-harness --json
                $result = $raw | ConvertFrom-Json
                $result.ok | Should -BeTrue
                $result.posted | Should -Be 1
                $result.resolved | Should -Be 0
                ($script:calls -join "`n") | Should -Not -Match 'comments reply --thread-id PRRT_1'
                ($script:calls -join "`n") | Should -Not -Match 'pr comment'
                ($script:calls -join "`n") | Should -Not -Match 'threads resolve --thread-id PRRT_1'
                ($script:calls -join "`n") | Should -Match 'comments reply --thread-id PRRT_2'
                $receipt = [System.IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json
                $receipt.prs[0].threads[1].last_error | Should -BeNullOrEmpty
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'isolates per-entry failures and keeps posted_at on resolve failure' {
            $script:calls = New-Object System.Collections.Generic.List[string]
            Mock Invoke-GimFleetExec {
                $script:calls.Add(($Argv -join ' '))
                if ($Argv -contains '--include-comment-node-id') {
                    return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{"reviews":[{"comments":[{"thread_id":"PRRT_1","is_resolved":false},{"thread_id":"PRRT_2","is_resolved":false}]}]}'; stderr = '' }
                }
                if ($Argv -contains 'reply' -and ($Argv -join ' ') -match 'PRRT_1') {
                    return [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'reply denied for alice@example.com' }
                }
                if ($Argv -contains 'threads' -and ($Argv -join ' ') -match 'PRRT_2') {
                    return [pscustomobject]@{ exit_code = 1; timed_out = $false; stdout = ''; stderr = 'resolve denied' }
                }
                return [pscustomobject]@{ exit_code = 0; timed_out = $false; stdout = '{}'; stderr = '' }
            }
            $dir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-respond-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $file = Join-Path $dir 'responses.json'
            try {
                New-GimWrapupRespondFixture -Path $file
                $raw = gim-wrapup-respond --repo GIMmos --file $file --harness test-harness --json
                $result = $raw | ConvertFrom-Json
                $result.ok | Should -BeFalse
                $result.failed | Should -BeGreaterThan 0
                ($script:calls -join "`n") | Should -Not -Match 'threads resolve --thread-id PRRT_1'
                $receipt = [System.IO.File]::ReadAllText((Join-Path $dir 'responses.result.json')) | ConvertFrom-Json
                $receipt.prs[0].threads[0].last_error | Should -Match 'reply denied'
                $receipt.prs[0].threads[0].last_error | Should -Match '<redacted-email>'
                $receipt.prs[0].threads[0].last_error | Should -Not -Match 'alice@example\.com'
                $receipt.prs[0].threads[1].posted_at | Should -Not -BeNullOrEmpty
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'rejects gather-shaped input and validates every PR before gh calls' {
            $dir = Join-Path (Get-GimTestRepoRoot) ('.agent-scratch\gim-wrapup-respond-' + [guid]::NewGuid().ToString('N'))
            [void][System.IO.Directory]::CreateDirectory($dir)
            $file = Join-Path $dir 'responses.json'
            Mock Invoke-GimFleetExec { throw 'gh must not run before validation' }
            try {
                $gather = [ordered]@{ schema_version = 1; repo = 'owner/repo'; prs = @() }
                [System.IO.File]::WriteAllText($file, (ConvertTo-Json -InputObject $gather -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
                { gim-wrapup-respond --repo GIMmos --file $file --harness test-harness } | Should -Throw '*gather-shaped*'

                New-GimWrapupRespondFixture -Path $file
                $doc = Get-Content $file -Raw | ConvertFrom-Json
                $doc.prs += [pscustomobject]@{ pr = 'bad'; threads = @(); comments = @() }
                [System.IO.File]::WriteAllText($file, (ConvertTo-Json -InputObject $doc -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
                { gim-wrapup-respond --repo GIMmos --file $file --harness test-harness } | Should -Throw '*without a valid number*'
            } finally {
                Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'rejects missing flags and a file outside the repo roots' {
            { gim-wrapup-respond --harness x --file responses.json } | Should -Throw '*missing --repo*'
            { gim-wrapup-respond --repo GIMmos --harness x } | Should -Throw '*missing --file*'
            { gim-wrapup-respond --repo GIMmos --file responses.json } | Should -Throw '*missing --harness*'
            { gim-wrapup-respond --repo GIMmos --file (Get-GimTestOutsidePath 'responses.json') --harness x } | Should -Throw '*outside the configured roots*roots.json*'
        }
        It 'emits a JSON argument-error envelope' {
            $modulePath = Join-Path $script:GimModuleRoot 'GIMTools\GIMTools.psd1'
            $pwshPath = (Get-Process -Id $PID).Path
            $scriptText = "Import-Module '$modulePath' -Force -DisableNameChecking; gim-wrapup-respond --repo GIMmos --harness x --json"
            $run = Invoke-GimFleetExec -Argv @($pwshPath, '-NoProfile', '-Command', $scriptText) -WorkingDirectory (Get-GimTestRepoRoot) -TimeoutSeconds 60
            $run.exit_code | Should -Be 1
            $doc = $run.stdout | ConvertFrom-Json
            $doc.ok | Should -BeFalse
            $doc.error | Should -Match '--file'
            foreach ($name in @('ok', 'repo', 'file', 'receipt', 'dry_run', 'posted', 'resolved', 'skipped', 'failed', 'warnings', 'actions', 'error')) {
                @($doc.PSObject.Properties.Name) | Should -Contain $name
            }
        }
    }
}

Describe 'fleet allowlist placement' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $template = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'opencode.local.template.jsonc'))
        $cursorDoc = [System.IO.File]::ReadAllText((Join-Path $repoRoot '.cursor\hooks\AGENTS.md'))
        $versionController = [System.IO.File]::ReadAllText((Join-Path $repoRoot '.opencode\agents\gim-version-controller.md'))
        $cursorVersionController = [System.IO.File]::ReadAllText((Join-Path $repoRoot '.cursor\agents\gim-version-controller.md'))
        $tracker = [System.IO.File]::ReadAllText((Join-Path $repoRoot '.opencode\agents\gim-tracker.md'))
    }
    It 'does not add gim-jira-search to the global OpenCode allowlist' {
        $template | Should -Not -Match '"gim-jira-search\*"'
    }
    It 'adds gim-jira-search to the tracker allowlist and Cursor checklist note' {
        $tracker | Should -Match 'gim-jira-search\*: allow'
        $cursorDoc | Should -Match 'gim-jira-search'
    }
    It 'does not add gim-jira-create to the global OpenCode allowlist' {
        $template | Should -Not -Match '"gim-jira-create\*"'
    }
    It 'adds gim-jira-create to the tracker allowlist and Cursor checklist note' {
        $tracker | Should -Match 'gim-jira-create\*: allow'
        $cursorDoc | Should -Match 'gim-jira-create'
    }
    It 'does not add gim-jira-transition to the global OpenCode allowlist' {
        $template | Should -Not -Match '"gim-jira-transition\*"'
    }
    It 'adds gim-jira-transition to the tracker allowlist and Cursor checklist note' {
        $tracker | Should -Match 'gim-jira-transition\*: allow'
        $cursorDoc | Should -Match 'gim-jira-transition'
    }
    It 'does not add gim-wrapup-gather to the global OpenCode allowlist' {
        $template | Should -Not -Match '"gim-wrapup-gather\*"'
    }
    It 'adds gim-wrapup-gather to the version-controller allowlist and Cursor checklist note' {
        $versionController | Should -Match 'gim-wrapup-gather\*: allow'
        $cursorDoc | Should -Match 'gim-wrapup-gather'
    }
    It 'allows gh run view for version-controller log capture' {
        $versionController | Should -Match 'gh run view\*: allow'
        $cursorDoc | Should -Match 'gh run view'
    }
    It 'does not add gim-wrapup-respond to the global OpenCode allowlist' {
        $template | Should -Not -Match '"gim-wrapup-respond\*"'
    }
    It 'adds gim-wrapup-respond to the version-controller allowlist and Cursor checklist note' {
        $versionController | Should -Match 'gim-wrapup-respond\*: allow'
        $cursorDoc | Should -Match 'gim-wrapup-respond'
    }
    It 'does not add gim-preflight to the global OpenCode allowlist' {
        $template | Should -Not -Match '"gim-preflight\*"'
    }
    It 'does not add gim-pr to the global OpenCode allowlist' {
        $template | Should -Not -Match '"gim-pr\*"'
    }
    It 'does not add gim-preflight to the generic Cursor Auto-run list' {
        $cursorDoc | Should -Not -Match '`gim-preflight`,'
        $cursorDoc | Should -Not -Match '`gim-preflight`\)'
        $cursorDoc | Should -Match 'intentionally absent'
    }
    It 'does not add gim-pr to the generic Cursor Auto-run list' {
        $cursorDoc | Should -Not -Match '`gim-pr`,'
        $cursorDoc | Should -Not -Match '`gim-pr`\)'
        $cursorDoc | Should -Match 'gim-pr'
    }
    It 'keeps gim-preflight and gim-pr in the OpenCode version-controller frontmatter only' {
        $versionController | Should -Match 'gim-preflight\*: allow'
        $versionController | Should -Match 'gim-pr\*: allow'
    }
    It 'keeps gim-pr out of the Cursor version-controller frontmatter' {
        $cursorVersionController | Should -Not -Match 'gim-pr\*: allow'
        $cursorVersionController | Should -Match '## gim-pr'
    }
}
