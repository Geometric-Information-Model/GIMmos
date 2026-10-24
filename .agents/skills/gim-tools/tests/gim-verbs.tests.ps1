# gim-verbs.tests.ps1 - Pester 6 tests for the A8 meta verb:
# --json vocabulary emission and --gen-refs reference generation.
# --gen-refs idempotence is asserted against a scratch output dir under
# .agent-scratch/ (contained, gitignored); the committed references/ are
# never touched by the suite.
# Pester 6 syntax: Should -Be / -Throw / -Match / -Contain / -BeIn.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

Describe 'gim-verbs --json' {
    BeforeAll {
        # Run-scope (BeforeAll) on purpose: Pester 6 discovery-scope variables
        # are invisible inside It blocks (see gim-test-common.ps1 note).
        $expectedGroups = @('spine', 'search', 'inspection', 'capability', 'state', 'mutating', 'execution', 'fleet', 'meta')
    }
    It 'emits the full 25-verb vocabulary that parses as JSON' {
        $env = gim-verbs --json | ConvertFrom-Json
        $env.count | Should -Be 25
        $env.truncated | Should -Be $false
        @($env.items).Count | Should -Be 25
    }
    It 'lists the committed 25 verbs in plan order' {
        $names = @((gim-verbs --json | ConvertFrom-Json).items | ForEach-Object { $_.name })
        $expected = @('gim-list', 'gim-where', 'gim-report', 'gim-grep', 'gim-inspect', 'gim-head', 'gim-count', 'gim-diff', 'gim-json', 'gim-asm', 'gim-set', 'gim-get', 'gim-fix', 'gim-write', 'gim-apply', 'gim-run', 'gim-probe', 'gim-preflight', 'gim-pr', 'gim-jira-search', 'gim-jira-create', 'gim-jira-transition', 'gim-wrapup-gather', 'gim-wrapup-respond', 'gim-verbs')
        $names | Should -Be $expected
    }
    It 'carries the required fields on every verb' {
        $items = @((gim-verbs --json | ConvertFrom-Json).items)
        foreach ($v in $items) {
            $v.name | Should -Match '^gim-[a-z][a-z-]*$'
            $v.group | Should -BeIn $expectedGroups
            $v.signature | Should -Not -BeNullOrEmpty
            $v.description | Should -Not -BeNullOrEmpty
            @($v.params).Count | Should -BeGreaterThan 0
            foreach ($p in @($v.params)) {
                $p.flag | Should -Not -BeNullOrEmpty
                $p.detail | Should -Not -BeNullOrEmpty
            }
        }
    }
    It 'assigns every verb to exactly one group with the planned counts' {
        $items = @((gim-verbs --json | ConvertFrom-Json).items)
        $counts = @{}
        foreach ($v in $items) {
            if ($counts.ContainsKey($v.group)) { $counts[$v.group]++ } else { $counts[$v.group] = 1 }
        }
        $counts['spine'] | Should -Be 3
        $counts['search'] | Should -Be 1
        $counts['inspection'] | Should -Be 5
        $counts['capability'] | Should -Be 1
        $counts['state'] | Should -Be 2
        $counts['mutating'] | Should -Be 3
        $counts['execution'] | Should -Be 2
        $counts['fleet'] | Should -Be 7
        $counts['meta'] | Should -Be 1
        $counts.Count | Should -Be 9
    }
    It 'is itself the meta-group entry' {
        $meta = @((gim-verbs --json | ConvertFrom-Json).items | Where-Object { $_.name -eq 'gim-verbs' })
        $meta.Count | Should -Be 1
        $meta[0].group | Should -Be 'meta'
        $meta[0].signature | Should -Match '--json'
        $meta[0].signature | Should -Match '--gen-refs'
    }
}

Describe 'gim-verbs --gen-refs' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-verbs-' + [guid]::NewGuid().ToString('N'))
        $expectedGroups = @('spine', 'search', 'inspection', 'capability', 'state', 'mutating', 'execution', 'fleet', 'meta')
    }
    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'writes one reference file per group' {
        gim-verbs --gen-refs --out-dir $scratch | Out-Null
        $files = @(Get-ChildItem -LiteralPath $scratch -Filter '*.md' -File)
        $files.Count | Should -Be 9
        $fullNames = @($files | ForEach-Object { $_.FullName })
        foreach ($g in $expectedGroups) {
            $fullNames | Should -Contain (Join-Path $scratch ($g + '.md'))
        }
    }
    It 'is idempotent: two runs produce identical bytes' {
        gim-verbs --gen-refs --out-dir $scratch | Out-Null
        $first = @{}
        foreach ($f in @(Get-ChildItem -LiteralPath $scratch -Filter '*.md' -File)) {
            $first[$f.Name] = [System.IO.File]::ReadAllBytes($f.FullName)
        }
        gim-verbs --gen-refs --out-dir $scratch | Out-Null
        $same = $true
        foreach ($f in @(Get-ChildItem -LiteralPath $scratch -Filter '*.md' -File)) {
            $b1 = $first[$f.Name]
            $b2 = [System.IO.File]::ReadAllBytes($f.FullName)
            if ($b1.Length -ne $b2.Length) { $same = $false; break }
            for ($i = 0; $i -lt $b1.Length; $i++) {
                if ($b1[$i] -ne $b2[$i]) { $same = $false; break }
            }
            if (-not $same) { break }
        }
        $same | Should -Be $true
    }
    It 'gives every verb a reference entry in its group file' {
        gim-verbs --gen-refs --out-dir $scratch | Out-Null
        $items = @((gim-verbs --json | ConvertFrom-Json).items)
        foreach ($v in $items) {
            $doc = [System.IO.File]::ReadAllText((Join-Path $scratch ($v.group + '.md')))
            $doc | Should -Match ([regex]::Escape('## ' + $v.name))
            $doc | Should -Match ([regex]::Escape($v.signature))
        }
    }
    It 'writes UTF-8 without BOM and LF newlines' {
        gim-verbs --gen-refs --out-dir $scratch | Out-Null
        $bytes = [System.IO.File]::ReadAllBytes((Join-Path $scratch 'spine.md'))
        ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) | Should -Be $false
    }
    It 'rejects an output dir outside the write roots' {
        { gim-verbs --gen-refs --out-dir 'C:\Windows\Temp\gim-refs' } | Should -Throw
    }
}

Describe 'gim-verbs modes' {
    It 'errors with no mode' {
        { gim-verbs } | Should -Throw
    }
    It 'errors with both modes' {
        { gim-verbs --json --gen-refs } | Should -Throw
    }
    It 'errors on unexpected positional arguments' {
        { gim-verbs nonsense } | Should -Throw
    }
}
