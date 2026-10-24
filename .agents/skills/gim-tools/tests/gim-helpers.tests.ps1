# gim-helpers.tests.ps1 - Pester 6 tests for module private helpers.
# Nothing here mutates outside temp/scratch. Helpers are module-private, so
# every Describe runs inside InModuleScope GIMTools.
# Pester 6 syntax: Should -Be / -Throw / -Contain / -BeNullOrEmpty.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking

InModuleScope GIMTools {
    Describe 'Quote-Arg' {
        It 'leaves a plain token unquoted' {
            Quote-Arg 'foo' | Should -Be 'foo'
        }
        It 'quotes a token with spaces' {
            Quote-Arg 'foo bar' | Should -Be '"foo bar"'
        }
        It 'escapes embedded quotes' {
            Quote-Arg 'a"b' | Should -Be '"a\"b"'
        }
        It 'doubles trailing backslashes before the closing quote' {
            Quote-Arg 'a b\' | Should -Be '"a b\\"'
        }
        It 'doubles backslashes before an embedded quote' {
            Quote-Arg 'a\"' | Should -Be '"a\\\""'
        }
        It 'quotes an empty string' {
            Quote-Arg '' | Should -Be '""'
        }
        It 'leaves a path without spaces unquoted' {
            Quote-Arg 'C:\temp\x' | Should -Be 'C:\temp\x'
        }
    }

    Describe 'Get-GimRepoRoot / Get-GimRealPath' {
        It 'finds the repo root above the module dir' {
            $root = Get-GimRepoRoot
            $root | Should -Not -BeNullOrEmpty
            (Test-Path -LiteralPath (Join-Path $root '.git')) | Should -Be $true
        }
        It 'resolves a relative path to its full real path' {
            $root = Get-GimRepoRoot
            (Get-GimRealPath -Path 'docs') | Should -Be ([System.IO.Path]::GetFullPath((Join-Path $root 'docs')))
        }
    }

    Describe 'Get-GimRoots' {
        It 'expands %VAR% entries in roots' {
            $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-roots-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $tmp -Force | Out-Null
            try {
                $rootsFile = Join-Path $tmp 'roots.json'
                $content = '{"roots": [".", "%TEMP%\\gim-env-root"], "readExtraRoots": []}'
                [System.IO.File]::WriteAllText($rootsFile, $content, (New-Object System.Text.UTF8Encoding($false)))
                $roots = Get-GimRoots -RootsFile $rootsFile
                $envRoot = [System.IO.Path]::GetFullPath((Join-Path $env:TEMP 'gim-env-root'))
                $roots.read | Should -Contain $envRoot
            } finally {
                Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Describe 'Resolve-ContainedPath' {
        BeforeAll {
            $repoRoot = Get-GimRepoRoot
            $scratch = Join-Path $repoRoot ('.agent-scratch\gim-helpers-' + [guid]::NewGuid().ToString('N'))
            $scratchSub = Join-Path $scratch 'sub'
        }

        BeforeEach {
            New-Item -ItemType Directory -Path $scratchSub -Force | Out-Null
        }
        AfterEach {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        }

        It 'accepts a path inside the repo root' {
            $resolved = Resolve-ContainedPath -Path (Join-Path $repoRoot 'docs')
            $resolved | Should -Be ([System.IO.Path]::GetFullPath((Join-Path $repoRoot 'docs')))
        }
        It 'rejects a path outside the roots' {
            { Resolve-ContainedPath -Path 'C:\Windows' } | Should -Throw
        }
        It 'rejects a ..\ escape even when it would resolve to a real path' {
            { Resolve-ContainedPath -Path '..\..\..\Windows' } | Should -Throw
        }
        It 'rejects a ..\ escape from inside a contained dir' {
            { Resolve-ContainedPath -Path 'docs\..\..\..\Windows' } | Should -Throw
        }
        It 'rejects an escape even with -AllowMissing (containment never forgiven)' {
            { Resolve-ContainedPath -Path 'C:\Windows' -AllowMissing } | Should -Throw
        }
        It 'errors on a missing contained path without -AllowMissing' {
            { Resolve-ContainedPath -Path (Join-Path $scratch 'nope') } | Should -Throw
        }
        It 'returns the lexical real path for a missing contained path with -AllowMissing' {
            $resolved = Resolve-ContainedPath -Path (Join-Path $scratch 'nope') -AllowMissing
            $resolved | Should -Be ([System.IO.Path]::GetFullPath((Join-Path $scratch 'nope')))
        }
        It 'resolves a junction and rejects it when the target escapes the roots' {
            $outside = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-outside-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $outside -Force | Out-Null
            try {
                $link = Join-Path $scratch 'link'
                New-Item -ItemType Junction -Path $link -Target $outside | Out-Null
                { Resolve-ContainedPath -Path $link } | Should -Throw
            } finally {
                Remove-Item -LiteralPath $outside -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'resolves a junction to a contained target and returns the target real path' {
            $target = Join-Path $scratch 'sub'
            $link = Join-Path $scratch 'link-in'
            New-Item -ItemType Junction -Path $link -Target $target | Out-Null
            $resolved = Resolve-ContainedPath -Path $link
            $resolved | Should -Be ([System.IO.Path]::GetFullPath($target))
        }
    }

    Describe 'Get-GimGlobMatches containment' {
        BeforeAll {
            $repoRoot = Get-GimRepoRoot
            $scratch = Join-Path $repoRoot ('.agent-scratch\gim-globhelp-' + [guid]::NewGuid().ToString('N'))
            $scratchName = Split-Path -Leaf $scratch
        }

        BeforeEach {
            New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        }
        AfterEach {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        }

        It 'skips glob hits that resolve outside the roots through a junction' {
            $outside = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-glob-out-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $outside -Force | Out-Null
            try {
                [System.IO.File]::WriteAllText((Join-Path $outside 'leak.txt'), 'x', (New-Object System.Text.UTF8Encoding($false)))
                $link = Join-Path $scratch 'jlink'
                New-Item -ItemType Junction -Path $link -Target $outside | Out-Null
                [System.IO.File]::WriteAllText((Join-Path $scratch 'real.txt'), 'x', (New-Object System.Text.UTF8Encoding($false)))
                $hits = @(Get-GimGlobMatches -Glob (".agent-scratch/$scratchName/*.txt") -Roots @($repoRoot) -DepthFilter 0)
                $hits.Count | Should -Be 1
                $hits | Should -Contain (Join-Path $scratch 'real.txt')
                $hits | Should -Not -Contain (Join-Path $outside 'leak.txt')
            } finally {
                Remove-Item -LiteralPath $outside -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        It 'applies --depth relative to the glob anchor' {
            New-Item -ItemType Directory -Path (Join-Path $scratch 'sub') -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $scratch 'sub\a.cs'), 'a', (New-Object System.Text.UTF8Encoding($false)))
            New-Item -ItemType Directory -Path (Join-Path $scratch 'sub\deep') -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $scratch 'sub\deep\b.cs'), 'b', (New-Object System.Text.UTF8Encoding($false)))
            $shallow = @(Get-GimGlobMatches -Glob (".agent-scratch/$scratchName/sub/**/*.cs") -Roots @($repoRoot) -DepthFilter 1)
            $shallow.Count | Should -Be 1
            $shallow | Should -Contain (Join-Path $scratch 'sub\a.cs')
            $deeper = @(Get-GimGlobMatches -Glob (".agent-scratch/$scratchName/sub/**/*.cs") -Roots @($repoRoot) -DepthFilter 2)
            $deeper.Count | Should -Be 2
            $deeper | Should -Contain (Join-Path $scratch 'sub\deep\b.cs')
        }
    }

    Describe 'GimVar minimal store' {
        BeforeAll {
            $storeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-var-' + [guid]::NewGuid().ToString('N'))
        }
        AfterEach {
            Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
        }

        It 'round-trips a value through the store' {
            Set-GimVarValue -Name 'alpha' -Value 'hello world' -StoreRoot $storeRoot
            (Get-GimVarValue -Name 'alpha' -StoreRoot $storeRoot) | Should -Be 'hello world'
        }
        It 'round-trips a multi-line value' {
            Set-GimVarValue -Name 'alpha' -Value "line1`nline2" -StoreRoot $storeRoot
            (Get-GimVarValue -Name 'alpha' -StoreRoot $storeRoot) | Should -Be "line1`nline2"
        }
        It 'returns $null for a missing variable' {
            (Get-GimVarValue -Name 'nope' -StoreRoot $storeRoot) | Should -BeNullOrEmpty
        }
        It 'rejects invalid names on write and read' {
            { Set-GimVarValue -Name '../../evil' -Value 'x' -StoreRoot $storeRoot } | Should -Throw
            { Get-GimVarValue -Name 'has space' -StoreRoot $storeRoot } | Should -Throw
        }
        It 'validates names against the invariant' {
            (Test-GimVarName -Name 'abc_123-DEF') | Should -Be $true
            (Test-GimVarName -Name 'a b') | Should -Be $false
            (Test-GimVarName -Name 'a.b') | Should -Be $false
        }
    }

    Describe 'GimOutput helpers' {
        It 'returns the default caps' {
            $caps = Get-GimCaps
            $caps.MaxItems | Should -Be 500
            $caps.MaxBytes | Should -Be 1048576
        }
        It 'builds a JSON envelope with count/truncated/items' {
            $env = New-GimJsonEnvelope -Items @('a', 'b') -MaxItems 10
            $env.count | Should -Be 2
            $env.truncated | Should -Be $false
            @($env.items) | Should -Be @('a', 'b')
        }
        It 'marks truncation when items exceed MaxItems' {
            $env = New-GimJsonEnvelope -Items @('a', 'b', 'c') -MaxItems 2
            $env.count | Should -Be 3
            $env.truncated | Should -Be $true
            @($env.items).Count | Should -Be 2
        }
        It 'emits JSON that ConvertFrom-Json can parse' {
            $json = Write-GimJson -InputObject @{ x = 1 }
            ($json | ConvertFrom-Json).x | Should -Be 1
        }
        It 'Write-GimError writes to stderr and throws' {
            { Write-GimError -Message 'boom' } | Should -Throw
        }
    }
}
