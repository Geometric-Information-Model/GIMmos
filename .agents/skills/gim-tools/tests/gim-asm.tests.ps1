# gim-asm.tests.ps1 - Pester 6 tests for the A4 capability verb: PE-metadata
# type enumeration (never loads the target assembly), --path containment,
# --matches filtering, --package resolution + not-found error, JSON envelope.
# The target dll is a COPY of the in-box .NET Framework mscorlib placed under
# .agent-scratch/ (contained in roots; nothing outside temp/scratch mutates).
# Pester 6 syntax: Should -Be / -BeGreaterThan / -Contain / -Not / -Match /
# -Throw -ExpectedMessage (wildcard -like) / -BeNullOrEmpty.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

Describe 'gim-asm' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-asm-' + [guid]::NewGuid().ToString('N'))
        $frameworkDir = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
        if (-not (Test-Path -LiteralPath $frameworkDir)) { $frameworkDir = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319' }
        $source = Join-Path $frameworkDir 'mscorlib.dll'
        $target = Join-Path $scratch 'mscorlib.dll'
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        [System.IO.File]::Copy($source, $target, $true)
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'enumerates exported types from a dll via --path' {
        $out = @(gim-asm types --path $target)
        $out.Count | Should -BeGreaterThan 500
        $out | Should -Contain "${target}: System.Object"
        $out | Should -Contain "${target}: System.Int32"
        $out | Should -Contain "${target}: Microsoft.Win32.Registry"
    }
    It 'caps text output at 500 items with a truncation marker' {
        $out = @(gim-asm types --path $target)
        $out.Count | Should -Be 501
        $out[-1] | Should -Match 'output truncated'
    }
    It 'skips nested and compiler-generated types' {
        $out = @(gim-asm types --path $target)
        @($out | Where-Object { $_ -match '<' }).Count | Should -Be 0
        @($out | Where-Object { $_ -match '\+' }).Count | Should -Be 0
    }
    It 'filters type names with --matches' {
        $out = @(gim-asm types --path $target --matches '^System\.Text\.')
        $out.Count | Should -BeGreaterThan 0
        @($out | Where-Object { $_ -notmatch ': System\.Text\.' }).Count | Should -Be 0
    }
    It 'never loads the target assembly into the session' {
        $before = @([System.AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetName().Name })
        $null = gim-asm types --path $target | Out-Null
        $after = @([System.AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetName().Name })
        $new = @($after | Where-Object {
            $before -notcontains $_ -and
            $_ -ne 'System.Reflection.Metadata' -and
            $_ -ne 'System.Collections.Immutable'
        })
        $new.Count | Should -Be 0
        # Direct proof: no assembly was loaded from the scratch copy.
        $loadedFromScratch = @([System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object {
            $_.Location -and $_.Location.StartsWith($scratch, [System.StringComparison]::OrdinalIgnoreCase)
        })
        $loadedFromScratch.Count | Should -Be 0
    }
    It 'emits path/type records with --json and truncates the envelope' {
        $env = gim-asm types --path $target --json | ConvertFrom-Json
        $env.count | Should -BeGreaterThan 500
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 500
        @($env.items)[0].path | Should -Be $target
        @($env.items)[0].type | Should -Not -BeNullOrEmpty
    }
    It 'rejects a missing --path' {
        { gim-asm types --path (Join-Path $scratch 'nope.dll') } | Should -Throw -ExpectedMessage '*path does not exist*'
    }
    It 'rejects a path outside the roots' {
        { gim-asm types --path 'C:\Windows\System32\kernel32.dll' } | Should -Throw -ExpectedMessage '*not contained*'
    }
    It 'errors without --package or --path' {
        { gim-asm types } | Should -Throw -ExpectedMessage '*give --package <id>/<ver> or --path <dll>*'
    }
    It 'rejects an unknown mode' {
        { gim-asm bogus --path $target } | Should -Throw -ExpectedMessage '*unknown gim-asm mode*'
    }
    It 'errors cleanly when --package is not cached' {
        { gim-asm types --package 'gim-no-such-package/0.0.0' } | Should -Throw -ExpectedMessage '*package not found*'
    }
    It 'resolves and enumerates a cached nuget package when one exists' {
        # Picker mirrors the verb's own TFM ranking (Get-GimAsmTfmRank) and
        # requires a lib/<tfm>/ containing at least one *.dll - a lib dir with
        # no dlls (e.g. bouncycastle) is skipped, so the pick is stable across
        # caches and never triggers "no lib/ dll found".
        $pkg = InModuleScope GIMTools {
            $root = @((Get-GimRoots).readExtra)[0]
            if (-not $root -or -not (Test-Path -LiteralPath $root)) { return $null }
            $found = $null
            foreach ($dir in @(Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue)) {
                if ($found) { break }
                foreach ($vd in @(Get-ChildItem -LiteralPath $dir.FullName -Directory -ErrorAction SilentlyContinue)) {
                    if ($found) { break }
                    $libDir = Join-Path $vd.FullName 'lib'
                    if (-not (Test-Path -LiteralPath $libDir -PathType Container)) { continue }
                    $ranked = @(Get-ChildItem -LiteralPath $libDir -Directory -ErrorAction SilentlyContinue |
                        Sort-Object -Property @{ Expression = { Get-GimAsmTfmRank -Tfm $_.Name } }, @{ Expression = { $_.Name } })
                    foreach ($tfmDir in $ranked) {
                        $dlls = @(Get-ChildItem -LiteralPath $tfmDir.FullName -Filter '*.dll' -File -ErrorAction SilentlyContinue)
                        if ($dlls.Count -gt 0) { $found = $dir.Name + '/' + $vd.Name; break }
                    }
                }
            }
            return $found
        }
        if (-not $pkg) {
            ($null -eq $pkg) | Should -Be $true
            return
        }
        $all = @(gim-asm types --package $pkg)
        $all.Count | Should -BeGreaterThan 0
        # --matches is positively exercised: filter on the first emitted type's
        # exact full name (provably hits on any package) and assert it selects
        # exactly that one type (strict subset of the unfiltered list).
        $firstType = $all[0] -replace '^.*: ', ''
        $matched = @(gim-asm types --package $pkg --matches ('^' + [regex]::Escape($firstType) + '$'))
        $matched.Count | Should -Be 1
        if ($all.Count -gt 1) {
            $matched.Count | Should -BeLessThan $all.Count
        }
        @($matched | Where-Object { $_ -notlike ('*' + $firstType) }).Count | Should -Be 0
    }
}
