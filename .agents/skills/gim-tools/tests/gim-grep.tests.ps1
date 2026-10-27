# gim-grep.tests.ps1 - Pester 6 tests for the A2 search verb: default
# file:line: text shape, --capture, --stdin, file-level modes, --fixed,
# --ignore-case, --context, --max-hits, --json record contract, containment.
# Fixtures live under repo .agent-scratch/ (gitignored, contained in roots).
# Pester 6 syntax: Should -Be / -Throw / -Match / -Contain.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

Describe 'gim-grep' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-grep-' + [guid]::NewGuid().ToString('N'))
        $fileA = Join-Path $scratch 'sample.txt'
        $fileB = Join-Path $scratch 'none.txt'
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        $content = "alpha beta`r`ngamma delta`r`nALPHA omega`r`none two three`r`nalpha again`r`naxb`r`na.b`r`n"
        [System.IO.File]::WriteAllText($fileA, $content, (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText($fileB, 'zzz zzz', (New-Object System.Text.UTF8Encoding($false)))
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'emits file:line: text by default' {
        $out = @($fileA | gim-grep --pattern alpha)
        $out.Count | Should -Be 2
        $out[0] | Should -Match ':1: alpha beta$'
        $out[1] | Should -Match ':5: alpha again$'
    }
    It 'accepts positional paths' {
        $out = @(gim-grep $fileA --pattern alpha)
        $out.Count | Should -Be 2
    }
    It 'emits the capture group with --capture' {
        $out = @($fileA | gim-grep --pattern '(\w+) beta' --capture 1)
        $out | Should -Be @('alpha')
    }
    It 'greps a piped text stream with --stdin' {
        $out = @(Get-Content -LiteralPath $fileA | gim-grep --stdin --pattern alpha)
        $out.Count | Should -Be 2
        $out[0] | Should -Match '^1: alpha beta$'
        $out[1] | Should -Match '^5: alpha again$'
    }
    It 'marks stdin context surroundings with a dash' {
        $out = @(Get-Content -LiteralPath $fileA | gim-grep --stdin --pattern omega --context 1)
        $out.Count | Should -Be 3
        $out[0] | Should -Match '^2- gamma delta$'
        $out[1] | Should -Match '^3: ALPHA omega$'
        $out[2] | Should -Match '^4- one two three$'
    }
    It 'names the stdin stream for file-level modes' {
        @(Get-Content -LiteralPath $fileA | gim-grep --stdin --pattern alpha --files-only) | Should -Be @('(stdin)')
        @(Get-Content -LiteralPath $fileA | gim-grep --stdin --pattern alpha --files-without-match).Count | Should -Be 0
        @(Get-Content -LiteralPath $fileB | gim-grep --stdin --pattern alpha --files-without-match) | Should -Be @('(stdin)')
        @(Get-Content -LiteralPath $fileA | gim-grep --stdin --pattern alpha --count) | Should -Be @('(stdin): 2')
    }
    It 'lists files with matches via --files-only' {
        $out = @(@($fileA, $fileB) | gim-grep --pattern alpha --files-only)
        $out.Count | Should -Be 1
        $out | Should -Contain $fileA
    }
    It 'lists files without matches via --files-without-match' {
        $out = @(@($fileA, $fileB) | gim-grep --pattern alpha --files-without-match)
        $out.Count | Should -Be 1
        $out | Should -Contain $fileB
    }
    It 'counts matches per file with --count' {
        $out = @(@($fileA, $fileB) | gim-grep --pattern alpha --count)
        $out.Count | Should -Be 2
        $out[0] | Should -Match ('^' + [regex]::Escape($fileA) + ': 2$')
        $out[1] | Should -Match ('^' + [regex]::Escape($fileB) + ': 0$')
    }
    It 'emits context surroundings with --context' {
        $out = @($fileA | gim-grep --pattern omega --context 1)
        $out.Count | Should -Be 3
        $out[0] | Should -Match ':2- gamma delta$'
        $out[1] | Should -Match ':3: ALPHA omega$'
        $out[2] | Should -Match ':4- one two three$'
    }
    It 'escapes the pattern with --fixed' {
        $out = @($fileA | gim-grep --pattern 'a.b')
        $out.Count | Should -Be 3
        $fixed = @($fileA | gim-grep --pattern 'a.b' --fixed)
        $fixed.Count | Should -Be 1
        $fixed[0] | Should -Match ':7: a\.b$'
    }
    It 'matches case-insensitively with --ignore-case' {
        $out = @($fileA | gim-grep --pattern ALPHA --ignore-case)
        $out.Count | Should -Be 3
    }
    It 'stops scanning at --max-hits and marks truncation' {
        $env = $fileA | gim-grep --pattern '.' --max-hits 2 --json | ConvertFrom-Json
        $env.count | Should -Be 2
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 2
        $out = @($fileA | gim-grep --pattern '.' --max-hits 2)
        $out.Count | Should -Be 3
        $out[-1] | Should -Match 'output truncated'
        ($out[-1] -match 'bytes 0') | Should -Be $false
    }
    It 'caps oversized output at the byte budget and reports real bytes' {
        $big = Join-Path $scratch 'big.txt'
        $lines = New-Object System.Collections.Generic.List[string]
        for ($i = 0; $i -lt 400; $i++) { $lines.Add(('x' * 3000)) }
        [System.IO.File]::WriteAllText($big, ($lines.ToArray() -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
        $out = @($big | gim-grep --pattern 'x')
        $out.Count | Should -BeGreaterThan 300
        $out[-1] | Should -Match 'output truncated'
        ($out[-1] -match 'bytes 0') | Should -Be $false
    }
    It 'streams large files lazily (100k+ lines) with identical results' {
        $big = Join-Path $scratch 'stream-big.txt'
        $sb = New-Object System.Text.StringBuilder
        for ($i = 0; $i -lt 100000; $i++) { [void]$sb.Append("line${i}`n") }
        [System.IO.File]::WriteAllText($big, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
        $out = @($big | gim-grep --pattern '^line99999$')
        $out.Count | Should -Be 1
        $out[0] | Should -Match ':100000: line99999$'
        $count = @($big | gim-grep --pattern '^line9999[0-9]$' --count)
        $count | Should -Be @("$big" + ': 10')
        $env = $big | gim-grep --pattern '^line' --max-hits 10 --json | ConvertFrom-Json
        $env.count | Should -Be 10
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 10
        $ctx = @($big | gim-grep --pattern '^line99999$' --context 1)
        $ctx.Count | Should -Be 2
        $ctx[0] | Should -Match '- line99998$'
        $ctx[1] | Should -Match ':100000: line99999$'
    }
    It 'emits the record contract with --json' {
        $env = $fileA | gim-grep --pattern alpha --json | ConvertFrom-Json
        $env.count | Should -Be 2
        $env.items[0].path | Should -Be $fileA
        $env.items[0].line | Should -Be 1
        $env.items[0].text | Should -Be 'alpha beta'
        $env.items[0].context | Should -Be $false
        $env.items[0].capture | Should -BeNullOrEmpty
    }
    It 'includes the capture field in JSON records when --capture used' {
        $env = $fileA | gim-grep --pattern '(\w+) beta' --capture 1 --json | ConvertFrom-Json
        @($env.items)[0].capture | Should -Be 'alpha'
    }
    It 'rejects a path outside the roots' {
        { gim-grep (Get-GimTestOutsidePath) --pattern 'x' } | Should -Throw
    }
    It 'errors on a missing path' {
        { gim-grep (Join-Path $scratch 'nope.txt') --pattern 'x' } | Should -Throw
    }
    It 'errors on a directory path' {
        { gim-grep $scratch --pattern 'x' } | Should -Throw
    }
    It 'errors without --pattern' {
        { 'x' | gim-grep --stdin } | Should -Throw
    }
    It 'rejects --stdin combined with path inputs' {
        { gim-grep $fileA --stdin --pattern 'x' } | Should -Throw
    }
    It 'rejects combined file-level modes' {
        { $fileA | gim-grep --pattern alpha --count --files-only } | Should -Throw
    }
    It 'emits nothing for empty stdin' {
        $out = @($null | gim-grep --stdin --pattern alpha)
        $out.Count | Should -Be 0
    }
}
