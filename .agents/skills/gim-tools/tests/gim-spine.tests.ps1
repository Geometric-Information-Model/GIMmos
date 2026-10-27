# gim-spine.tests.ps1 - Pester 6 tests for the A1 spine verbs:
# module import, gim-list, gim-where, gim-report.
# Fixtures live under repo .agent-scratch/ (gitignored, contained in roots);
# nothing else outside temp/scratch is mutated.
# Pester 6 syntax: Should -Be / -Throw / -Match / -Contain / -BeNullOrEmpty.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

Describe 'module import' {
    It 'exports all 25 committed verbs' {
        @(Get-Command gim-list, gim-where, gim-report, gim-grep, gim-inspect, gim-head, gim-count, gim-diff, gim-json, gim-asm, gim-set, gim-get, gim-fix, gim-write, gim-apply, gim-run, gim-probe, gim-preflight, gim-pr, gim-jira-search, gim-jira-create, gim-jira-transition, gim-wrapup-gather, gim-wrapup-respond, gim-verbs -Module GIMTools).Count | Should -Be 25
    }
}

Describe 'gim-list' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-list-' + [guid]::NewGuid().ToString('N'))
        $scratchName = Split-Path -Leaf $scratch
        $sub = Join-Path $scratch 'sub'
        $fileA = Join-Path $sub 'a.cs'
        $fileB = Join-Path $sub 'b.md'
        $fileTop = Join-Path $scratch 'top.md'
        $emptyDir = Join-Path $scratch 'emptydir'
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $sub -Force | Out-Null
        New-Item -ItemType Directory -Path $emptyDir -Force | Out-Null
        [System.IO.File]::WriteAllText($fileA, 'a', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText($fileB, 'b', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText($fileTop, 'top', (New-Object System.Text.UTF8Encoding($false)))
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'lists immediate children of a directory' {
        $out = @(gim-list $scratch)
        $out.Count | Should -Be 3
        $out | Should -Contain $sub
        $out | Should -Contain $emptyDir
        $out | Should -Contain $fileTop
        $out | Should -Not -Contain $fileA
    }
    It 'recurses with --depth' {
        $out = @(gim-list $scratch --depth 2)
        $out | Should -Contain $fileA
        $out | Should -Contain $fileB
    }
    It 'lists a single file path' {
        @(gim-list $fileA) | Should -Be @($fileA)
    }
    It 'filters to directories with --dirs-only' {
        $out = @(gim-list $scratch --dirs-only)
        $out.Count | Should -Be 2
        $out | Should -Contain $sub
        $out | Should -Contain $emptyDir
    }
    It 'filters with --under' {
        $out = @(gim-list $scratch --under $sub)
        $out.Count | Should -Be 2
        $out | Should -Contain $fileA
        $out | Should -Contain $fileB
    }
    It 'lists under a directory standalone with --under' {
        $out = @(gim-list --under $sub)
        $out.Count | Should -Be 2
        $out | Should -Contain $fileA
        $out | Should -Contain $fileB
        $out | Should -Not -Contain $sub
        $out | Should -Not -Contain $fileTop
    }
    It 'errors cleanly on a missing --under directory' {
        { gim-list --under (Join-Path $scratch 'missing') } | Should -Throw
    }
    It 'errors on a missing path without --allow-missing' {
        { gim-list (Join-Path $scratch 'missing') } | Should -Throw
    }
    It 'skips a missing path with --allow-missing' {
        @(gim-list (Join-Path $scratch 'missing') --allow-missing).Count | Should -Be 0
    }
    It 'matches a single-star glob under a directory' {
        $out = @(gim-list --glob (".agent-scratch/$scratchName/sub/*.cs"))
        $out | Should -Contain $fileA
        $out.Count | Should -Be 1
    }
    It 'matches a ** glob across depth' {
        $out = @(gim-list --glob ".agent-scratch/**/a.cs")
        $out | Should -Contain $fileA
    }
    It 'applies --depth relative to the glob anchor' {
        New-Item -ItemType Directory -Path (Join-Path $sub 'deep') -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $sub 'deep\c.cs'), 'c', (New-Object System.Text.UTF8Encoding($false)))
        $shallow = @(gim-list --glob (".agent-scratch/$scratchName/sub/**/*.cs") --depth 1)
        $shallow.Count | Should -Be 1
        $shallow | Should -Contain $fileA
        $deeper = @(gim-list --glob (".agent-scratch/$scratchName/sub/**/*.cs") --depth 2)
        $deeper.Count | Should -Be 2
        $deeper | Should -Contain (Join-Path $sub 'deep\c.cs')
    }
    It 'excludes glob matches that resolve outside the roots through a junction' {
        $outside = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-glob-out-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $outside -Force | Out-Null
        try {
            [System.IO.File]::WriteAllText((Join-Path $outside 'leak.txt'), 'x', (New-Object System.Text.UTF8Encoding($false)))
            $link = Join-Path $scratch 'jlink'
            New-GimTestReparsePoint -Path $link -Target $outside | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $scratch 'real.txt'), 'x', (New-Object System.Text.UTF8Encoding($false)))
            $out = @(gim-list --glob (".agent-scratch/$scratchName/*.txt"))
            $out.Count | Should -Be 1
            $out | Should -Contain (Join-Path $scratch 'real.txt')
            $out | Should -Not -Contain (Join-Path $outside 'leak.txt')
            foreach ($p in $out) {
                ($p.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) | Should -Be $true
            }
        } finally {
            Remove-Item -LiteralPath $outside -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    It 'errors when a glob matches nothing without --allow-missing' {
        { gim-list --glob (".agent-scratch/$scratchName/sub/*.nomatch") } | Should -Throw
    }
    It 'skips an empty glob with --allow-missing' {
        @(gim-list --glob (".agent-scratch/$scratchName/sub/*.nomatch") --allow-missing).Count | Should -Be 0
    }
    It 'reads literal paths from --from-file' {
        $listFile = Join-Path $scratch 'paths.txt'
        [System.IO.File]::WriteAllText($listFile, "$fileA`r`n$fileB`r`n", (New-Object System.Text.UTF8Encoding($false)))
        $out = @(gim-list --from-file $listFile)
        $out.Count | Should -Be 2
        $out | Should -Contain $fileA
        $out | Should -Contain $fileB
    }
    It 'emits NUL-separated output with --print0' {
        $out = @(gim-list $scratch --print0)
        $out.Count | Should -Be 1
        ($out[0] -split [char]0).Count | Should -Be 3
    }
    It 'emits a parseable JSON envelope with --json' {
        $env = gim-list $scratch --json | ConvertFrom-Json
        $env.count | Should -Be 3
        $env.truncated | Should -Be $false
        @($env.items).Count | Should -Be 3
    }
    It 'rejects an absolute path outside the roots' {
        { gim-list (Get-GimTestOutsidePath) } | Should -Throw
        { gim-list (Get-GimTestOutsidePath) --allow-missing } | Should -Throw
    }
    It 'rejects a ..\ escape path' {
        Push-Location $repoRoot
        try {
            { gim-list (Get-GimTestParentEscape) } | Should -Throw
        } finally {
            Pop-Location
        }
    }
    It 'errors with no input' {
        { gim-list } | Should -Throw
    }
    It 'caps output at MaxItems with a truncation marker' {
        $many = Join-Path $scratch 'many'
        New-Item -ItemType Directory -Path $many -Force | Out-Null
        for ($i = 0; $i -lt 505; $i++) {
            [System.IO.File]::WriteAllText((Join-Path $many ("f{0:D3}.txt" -f $i)), 'x', (New-Object System.Text.UTF8Encoding($false)))
        }
        $out = @(gim-list $many)
        $out.Count | Should -Be 501
        $out[-1] | Should -Match 'output truncated'
    }
    It 'caps print0 tokens and keeps the truncation marker out of the NUL stream' {
        $many = Join-Path $scratch 'many'
        New-Item -ItemType Directory -Path $many -Force | Out-Null
        for ($i = 0; $i -lt 505; $i++) {
            [System.IO.File]::WriteAllText((Join-Path $many ("f{0:D3}.txt" -f $i)), 'x', (New-Object System.Text.UTF8Encoding($false)))
        }
        $out = @(gim-list $many --print0)
        $out.Count | Should -Be 1
        ($out[0] -split [char]0).Count | Should -Be 500
        ($out[0] -match 'truncated') | Should -Be $false
    }
}

Describe 'gim-where' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-where-' + [guid]::NewGuid().ToString('N'))
        $fileA = Join-Path $scratch 'a.txt'
        $fileB = Join-Path $scratch 'b.cs'
        $fileC = Join-Path $scratch 'c.md'
        $subdir = Join-Path $scratch 'subdir'
        $oldFile = Join-Path $scratch 'old.txt'
        $newFile = Join-Path $scratch 'new.txt'
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $subdir -Force | Out-Null
        [System.IO.File]::WriteAllText($fileA, '0123456789', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText($fileB, '', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText($fileC, ('x' * 500), (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText($oldFile, 'old', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText($newFile, 'new', (New-Object System.Text.UTF8Encoding($false)))
        (Get-Item $oldFile).LastWriteTime = [datetime]'2020-01-01T00:00:00'
        (Get-Item $newFile).LastWriteTime = [datetime]'2025-01-01T00:00:00'
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'filters to files with --is-file' {
        $out = @(gim-list $scratch | gim-where --is-file)
        $out.Count | Should -Be 5
        $out | Should -Contain $fileA
        $out | Should -Not -Contain $subdir
    }
    It 'filters to directories with --is-dir' {
        @(gim-list $scratch | gim-where --is-dir) | Should -Be @($subdir)
    }
    It 'keeps existing paths with --exists' {
        $out = @((@($fileA, (Join-Path $scratch 'nope')) | gim-where --exists))
        $out.Count | Should -Be 1
        $out | Should -Contain $fileA
    }
    It 'keeps missing paths with --missing' {
        $out = @((@($fileA, (Join-Path $scratch 'nope')) | gim-where --missing))
        $out.Count | Should -Be 1
        $out | Should -Contain (Join-Path $scratch 'nope')
    }
    It 'filters empty vs non-empty' {
        $empty = @(gim-list $scratch | gim-where --empty)
        $empty | Should -Contain $fileB
        $empty | Should -Contain $subdir
        $nonEmpty = @(gim-list $scratch | gim-where --non-empty)
        $nonEmpty | Should -Contain $fileA
        $nonEmpty | Should -Not -Contain $fileB
    }
    It 'matches extension with --ext with or without dot' {
        @(gim-list $scratch | gim-where --ext cs) | Should -Be @($fileB)
        @(gim-list $scratch | gim-where --ext '.cs') | Should -Be @($fileB)
    }
    It 'filters by byte size' {
        $big = @((@($fileA, $fileB, $fileC) | gim-where --larger-than 0))
        $big.Count | Should -Be 2
        $big | Should -Contain $fileA
        $small = @((@($fileA, $fileB, $fileC) | gim-where --smaller-than 100))
        $small.Count | Should -Be 2
        $small | Should -Contain $fileB
    }
    It 'excludes missing paths from size predicates' {
        $missing = Join-Path $scratch 'nope.txt'
        @(@($missing) | gim-where --larger-than 0).Count | Should -Be 0
        @(@($missing) | gim-where --smaller-than 100).Count | Should -Be 0
    }
    It 'filters by last-write time' {
        @((@($oldFile, $newFile) | gim-where --older-than '2021-01-01')) | Should -Be @($oldFile)
        @((@($oldFile, $newFile) | gim-where --newer-than '2021-01-01')) | Should -Be @($newFile)
    }
    It 'filters strings with --matches / --not-matches' {
        @((@('foo', 'bar', 'foobar') | gim-where --matches '^foo')) | Should -Be @('foo', 'foobar')
        @((@('foo', 'bar', 'foobar') | gim-where --not-matches '^foo')) | Should -Be @('bar')
    }
    It 'filters records on a named field with --field' {
        $records = @(
            [pscustomobject]@{ name = 'alpha'; kind = 'txt' },
            [pscustomobject]@{ name = 'beta'; kind = 'cs' }
        )
        $out = @($records | gim-where --field kind --matches '^cs$')
        $out.Count | Should -Be 1
        ($out[0] | ConvertFrom-Json).name | Should -Be 'beta'
    }
    It 'emits a JSON envelope with --json' {
        $env = gim-list $scratch | gim-where --is-file --json | ConvertFrom-Json
        $env.count | Should -Be 5
        @($env.items).Count | Should -Be 5
    }
    It 'rejects an invalid regex up front' {
        { @('x') | gim-where --matches '(' } | Should -Throw
    }
}

Describe 'gim-report' {
    It 'passes lines through in order by default' {
        @((@('b', 'a') | gim-report)) | Should -Be @('b', 'a')
    }
    It 'sorts strings with --sort' {
        @((@('b', 'a', 'c') | gim-report --sort)) | Should -Be @('a', 'b', 'c')
    }
    It 'prefixes each line with --prefix' {
        @((@('x', 'y') | gim-report --prefix P)) | Should -Be @('P: x', 'P: y')
    }
    It 'prefixes each line with --prefix-each' {
        @((@('x', 'y') | gim-report --prefix P --prefix-each)) | Should -Be @('P: x', 'P: y')
    }
    It 'emits a JSON envelope with --format json' {
        $env = @(@('x', 'y') | gim-report --format json) | ConvertFrom-Json
        $env.count | Should -Be 2
        @($env.items) | Should -Be @('x', 'y')
    }
    It 'treats --json as --format json' {
        $env = @(@('x') | gim-report --json) | ConvertFrom-Json
        $env.count | Should -Be 1
    }
    It 'renders records as a table with --format table' {
        $out = @(
            [pscustomobject]@{ name = 'a'; size = 1 },
            [pscustomobject]@{ name = 'b'; size = 2 }
        ) | gim-report --format table
        $out -join "`n" | Should -Match 'name'
        $out -join "`n" | Should -Match 'size'
        (($out -join "`n") -match '(?m)^b\s') | Should -Be $true
    }
    It 'caps table output with a truncation marker' {
        $records = @()
        for ($i = 0; $i -lt 600; $i++) {
            $records += [pscustomobject]@{ name = ('n' + $i); size = $i }
        }
        $out = @($records | gim-report --format table)
        $out.Count | Should -Be 501
        $out[-1] | Should -Match 'output truncated'
    }
    It 'caps oversized JSON output at the byte cap' {
        $records = @()
        for ($i = 0; $i -lt 500; $i++) {
            $records += [pscustomobject]@{ name = ('x' * 3000) }
        }
        $out = @($records | gim-report --format json)
        $out.Count | Should -Be 1
        ([System.Text.Encoding]::UTF8.GetByteCount($out[0])) | Should -BeLessThan 1048577
    }
    It 'sorts records by a field with --sort' {
        $env = @(
            [pscustomobject]@{ name = 'b' },
            [pscustomobject]@{ name = 'a' }
        ) | gim-report --sort name --format json | ConvertFrom-Json
        @($env.items)[0].name | Should -Be 'a'
    }
    It 'keeps failing strings with --only-failures' {
        @((@('ok', 'failed', 'warn') | gim-report --only-failures)) | Should -Be @('failed', 'warn')
    }
    It 'keeps failing records with --only-failures' {
        $env = @(
            [pscustomobject]@{ name = 'x'; status = 'ok' },
            [pscustomobject]@{ name = 'y'; failed = $true }
        ) | gim-report --only-failures --format json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].name | Should -Be 'y'
    }
}
