# gim-mutating.tests.ps1 - Pester 6 tests for the A6 mutating verbs:
# gim-fix, gim-write, gim-apply.
# Fixtures live under repo .agent-scratch/ (gitignored, contained in write
# roots); GIM_VARS_ROOT points at a temp store for the --path-from-var case.
# gim-apply additionally allows the OS temp root, so its Describe uses a
# unique per-run directory under [System.IO.Path]::GetTempPath() and removes it
# afterwards.
# Pester 6 syntax: Should -Be / -Throw / -Match / -Contain / -Not.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

function global:Get-GimBytes {
    param([string]$Path)
    return [System.IO.File]::ReadAllBytes($Path)
}

Describe 'gim-fix' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-fix-' + [guid]::NewGuid().ToString('N'))
        $noeofLf = Join-Path $scratch 'noeof-lf.txt'
        $noeofCrlf = Join-Path $scratch 'noeof-crlf.txt'
        $crlfFile = Join-Path $scratch 'crlf.txt'
        $lfFile = Join-Path $scratch 'lf.txt'
        $wsFile = Join-Path $scratch 'ws.txt'
        $cleanFile = Join-Path $scratch 'clean.txt'
        $bomFile = Join-Path $scratch 'bom.txt'
        $utf16File = Join-Path $scratch 'utf16.txt'
        $crFile = Join-Path $scratch 'cr.txt'
        $enc = New-Object System.Text.UTF8Encoding($false)
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        [System.IO.File]::WriteAllText($noeofLf, "a`nb", $enc)
        [System.IO.File]::WriteAllText($noeofCrlf, "a`r`nb", $enc)
        [System.IO.File]::WriteAllText($crlfFile, "x`r`ny`r`n", $enc)
        [System.IO.File]::WriteAllText($lfFile, "x`ny`n", $enc)
        [System.IO.File]::WriteAllText($wsFile, "a  b  `r`n", $enc)
        [System.IO.File]::WriteAllText($cleanFile, "a`nb`n", $enc)
        [System.IO.File]::WriteAllText($bomFile, 'hi', (New-Object System.Text.UTF8Encoding($true)))
        [System.IO.File]::WriteAllText($utf16File, 'hi  ', [System.Text.Encoding]::Unicode)
        [System.IO.File]::WriteAllText($crFile, "a`rb", $enc)
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'appends an LF end-of-line with eof-newline' {
        gim-fix eof-newline $noeofLf | Out-Null
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $noeofLf)) | Should -Be "a`nb`n"
    }
    It 'appends a CRLF end-of-line with eof-newline --to crlf' {
        gim-fix eof-newline $noeofLf --to crlf | Out-Null
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $noeofLf)) | Should -Be "a`nb`r`n"
    }
    It 'matches the existing CRLF ending for eof-newline without --to' {
        gim-fix eof-newline $noeofCrlf | Out-Null
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $noeofCrlf)) | Should -Be "a`r`nb`r`n"
    }
    It 'appends a lone CR for CR-only files with eof-newline (no mixed endings)' {
        $out = @(gim-fix eof-newline $crFile)
        $out[0] | Should -Match 'before=False'
        $out[0] | Should -Match 'changed=True'
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $crFile)) | Should -Be "a`rb`r"
    }
    It 'treats a lone CR as a newline terminator for eof-newline' {
        [System.IO.File]::WriteAllText($crFile, "a`rb`r", $enc)
        @(gim-fix eof-newline $crFile)[0] | Should -Match 'changed=False'
    }
    It 'reports before/after for eof-newline' {
        $out = @(gim-fix eof-newline $noeofLf)
        $out[0] | Should -Match 'mode=eof-newline'
        $out[0] | Should -Match 'before=False'
        $out[0] | Should -Match 'after=True'
        $out[0] | Should -Match 'changed=True'
    }
    It 'normalizes CRLF to LF with line-endings --to lf' {
        gim-fix line-endings $crlfFile --to lf | Out-Null
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $crlfFile)) | Should -Be "x`ny`n"
    }
    It 'normalizes LF to CRLF with line-endings --to crlf' {
        gim-fix line-endings $lfFile --to crlf | Out-Null
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $lfFile)) | Should -Be "x`r`ny`r`n"
    }
    It 'defaults line-endings to lf' {
        gim-fix line-endings $crlfFile | Out-Null
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $crlfFile)) | Should -Be "x`ny`n"
    }
    It 'strips trailing whitespace without touching line endings' {
        gim-fix trailing-ws $wsFile | Out-Null
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $wsFile)) | Should -Be "a  b`r`n"
    }
    It 'fixes gim-inspect/gim-where text-record pipeline items end-to-end' {
        $out = @(gim-inspect $wsFile --trailing-ws | gim-where --matches 'trailingWs=True' | gim-fix trailing-ws)
        $out.Count | Should -Be 1
        $out[0] | Should -Match 'changed=True'
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $wsFile)) | Should -Be "a  b`r`n"
    }
    It 'errors with no input when the inspect pipeline filters everything out' {
        { gim-inspect $cleanFile --trailing-ws | gim-where --matches 'trailingWs=True' | gim-fix trailing-ws } | Should -Throw -ExpectedMessage '*no input*'
    }
    It 'reports changed=False for already-clean files' {
        @(gim-fix eof-newline $cleanFile)[0] | Should -Match 'changed=False'
        @(gim-fix line-endings $lfFile --to lf)[0] | Should -Match 'changed=False'
        @(gim-fix trailing-ws $cleanFile)[0] | Should -Match 'changed=False'
    }
    It 'leaves files untouched with --dry-run' {
        $before = Get-GimBytes $noeofLf
        $out = @(gim-fix eof-newline $noeofLf --dry-run)
        $out[0] | Should -Match 'changed=True'
        (Get-GimBytes $noeofLf) | Should -Be $before
        $beforeCrlf = Get-GimBytes $crlfFile
        gim-fix line-endings $crlfFile --dry-run | Out-Null
        (Get-GimBytes $crlfFile) | Should -Be $beforeCrlf
    }
    It 'preserves a UTF-8 BOM and UTF-16 encoding across fixes' {
        gim-fix eof-newline $bomFile | Out-Null
        $b = Get-GimBytes $bomFile
        $b[0] | Should -Be 0xEF
        $b[1] | Should -Be 0xBB
        $b[2] | Should -Be 0xBF
        [System.Text.Encoding]::UTF8.GetString($b, 3, $b.Length - 3) | Should -Be "hi`n"
        gim-fix trailing-ws $utf16File | Out-Null
        $b = Get-GimBytes $utf16File
        $b[0] | Should -Be 0xFF
        $b[1] | Should -Be 0xFE
        [System.Text.Encoding]::Unicode.GetString($b, 2, $b.Length - 2) | Should -Be 'hi'
    }
    It 'emits a JSON envelope with --json' {
        $env = gim-fix eof-newline $noeofLf --json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].mode | Should -Be 'eof-newline'
        @($env.items)[0].changed | Should -Be $true
    }
    It 'errors on a missing path and on a directory' {
        { gim-fix eof-newline (Join-Path $scratch 'nope.txt') } | Should -Throw -ExpectedMessage '*does not exist*'
        { gim-fix eof-newline $scratch } | Should -Throw -ExpectedMessage '*directory*'
    }
    It 'rejects non-path pipeline items with a shape error' {
        { [pscustomobject]@{ path = $cleanFile } | gim-fix trailing-ws } | Should -Throw -ExpectedMessage '*invalid pipeline item*'
        { '' | gim-fix trailing-ws } | Should -Throw -ExpectedMessage '*invalid pipeline item*'
    }
    It 'rejects an invalid mode and an invalid --to' {
        { gim-fix bogus $cleanFile } | Should -Throw -ExpectedMessage '*invalid mode*'
        { gim-fix line-endings $cleanFile --to lfcr } | Should -Throw -ExpectedMessage '*invalid --to*'
        { gim-fix trailing-ws $cleanFile --to lf } | Should -Throw -ExpectedMessage '*invalid --to*'
    }
    It 'rejects paths outside the write roots' {
        { gim-fix eof-newline 'C:\Windows\notepad.exe' } | Should -Throw -ExpectedMessage '*not contained*'
        Push-Location $repoRoot
        try {
            { gim-fix eof-newline '..\..\..\Windows\notepad.exe' } | Should -Throw -ExpectedMessage '*not contained*'
        } finally {
            Pop-Location
        }
    }
}

Describe 'gim-write' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-write-' + [guid]::NewGuid().ToString('N'))
        $storeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-write-store-' + [guid]::NewGuid().ToString('N'))
        $oldVarsRoot = $env:GIM_VARS_ROOT
        $env:GIM_VARS_ROOT = $storeRoot
    }
    AfterAll {
        if ($null -eq $oldVarsRoot) { Remove-Item Env:GIM_VARS_ROOT -ErrorAction SilentlyContinue } else { $env:GIM_VARS_ROOT = $oldVarsRoot }
        Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'writes to .agent-scratch by default with UTF-8 no BOM' {
        $name = 'gim-write-default-' + [guid]::NewGuid().ToString('N') + '.txt'
        $target = Join-Path $repoRoot ('.agent-scratch\' + $name)
        Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        try {
            'hello' | gim-write --path $name --from-stdin | Out-Null
            (Test-Path -LiteralPath $target) | Should -Be $true
            [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $target)) | Should -Be 'hello'
            $b = Get-GimBytes $target
            ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) | Should -Be $false
        } finally {
            Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        }
    }
    It 'writes piped content without --from-stdin' {
        $f = Join-Path $scratch 'nostdin.txt'
        'abc' | gim-write --path $f | Out-Null
        (Test-Path -LiteralPath $f) | Should -Be $true
        [System.Text.Encoding]::UTF8.GetString((Get-GimBytes $f)) | Should -Be 'abc'
    }
    It 'writes UTF-8 with BOM for --encoding utf8bom' {
        $f = Join-Path $scratch 'bom.txt'
        'abc' | gim-write --path $f --from-stdin --encoding utf8bom | Out-Null
        $b = Get-GimBytes $f
        $b[0] | Should -Be 0xEF
        $b[1] | Should -Be 0xBB
        $b[2] | Should -Be 0xBF
        [System.Text.Encoding]::UTF8.GetString($b, 3, 3) | Should -Be 'abc'
    }
    It 'writes UTF-16 LE with BOM for --encoding utf16' {
        $f = Join-Path $scratch 'u16.txt'
        'abc' | gim-write --path $f --from-stdin --encoding utf16 | Out-Null
        $b = Get-GimBytes $f
        $b[0] | Should -Be 0xFF
        $b[1] | Should -Be 0xFE
        $b.Length | Should -Be 8
        [System.Text.Encoding]::Unicode.GetString($b, 2, 6) | Should -Be 'abc'
    }
    It 'honors --out-dir and creates nested directories' {
        $sub = Join-Path $scratch 'sub\deep'
        'x' | gim-write --path 'f.txt' --from-stdin --out-dir $sub | Out-Null
        (Test-Path -LiteralPath (Join-Path $sub 'f.txt')) | Should -Be $true
    }
    It 'refuses to overwrite with --create-only' {
        $f = Join-Path $scratch 'exists.txt'
        [System.IO.File]::WriteAllText($f, 'old', (New-Object System.Text.UTF8Encoding($false)))
        { 'new' | gim-write --path $f --from-stdin --create-only } | Should -Throw -ExpectedMessage '*already exists*'
        [System.IO.File]::ReadAllText($f) | Should -Be 'old'
    }
    It 'reads the target path from the variable store with --path-from-var' {
        $target = Join-Path $scratch 'fromvar.txt'
        "$target" | gim-set fromvarpath | Out-Null
        'content' | gim-write --path-from-var fromvarpath --from-stdin | Out-Null
        [System.IO.File]::ReadAllText($target) | Should -Be 'content'
    }
    It 'creates nothing with --dry-run' {
        $target = Join-Path $scratch 'dry.txt'
        $out = @('x' | gim-write --path $target --from-stdin --dry-run)
        (Test-Path -LiteralPath $target) | Should -Be $false
        $out[0] | Should -Match 'would-write'
        $out[0] | Should -Match '1 bytes'
    }
    It 'reports the on-disk byte count including BOM' {
        $f = Join-Path $scratch 'count.txt'
        'abc' | gim-write --path $f --from-stdin --encoding utf8bom | Out-Null
        (Get-GimBytes $f).Length | Should -Be 6
    }
    It 'rejects escape and env-var targets' {
        { 'x' | gim-write --path 'C:\Windows\evil.txt' --from-stdin } | Should -Throw -ExpectedMessage '*not contained*'
        { 'x' | gim-write --path '..\..\..\Windows\evil.txt' --from-stdin } | Should -Throw -ExpectedMessage '*not contained*'
        { 'x' | gim-write --path '%TEMP%\evil.txt' --from-stdin } | Should -Throw -ExpectedMessage '*environment variables*'
    }
    It 'stays repo-only: rejects an OS temp-root target' {
        $f = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-write-temp-' + [guid]::NewGuid().ToString('N') + '.txt')
        { 'x' | gim-write --path $f --from-stdin } | Should -Throw -ExpectedMessage '*not contained*'
        (Test-Path -LiteralPath $f) | Should -Be $false
    }
    It 'rejects missing flags and bad encodings' {
        { gim-write --path (Join-Path $scratch 'x.txt') } | Should -Throw -ExpectedMessage '*--from-stdin*'
        { 'x' | gim-write } | Should -Throw -ExpectedMessage '*--path*'
        { 'x' | gim-write --path (Join-Path $scratch 'x.txt') --from-stdin --encoding latin1 } | Should -Throw -ExpectedMessage '*invalid --encoding*'
        { 'x' | gim-write --path (Join-Path $scratch 'x.txt') --path-from-var v --from-stdin } | Should -Throw -ExpectedMessage '*not both*'
    }
    It 'emits a JSON envelope with --json' {
        $f = Join-Path $scratch 'json.txt'
        $env = 'ab' | gim-write --path $f --from-stdin --json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].path | Should -Be $f
        @($env.items)[0].created | Should -Be $true
        @($env.items)[0].bytes | Should -Be 2
        @($env.items)[0].encoding | Should -Be 'utf8'
    }
}

Describe 'gim-apply' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-apply-' + [guid]::NewGuid().ToString('N'))
        # Unique per-run directory under the real OS temp root; gim-apply is
        # the only write verb allowed to touch it.
        $tempScratch = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-apply-' + [guid]::NewGuid().ToString('N'))
    }
    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        New-Item -ItemType Directory -Path $tempScratch -Force | Out-Null
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $tempScratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'creates an empty file with --create-empty' {
        $f = Join-Path $scratch 'new.txt'
        gim-apply --create-empty $f | Out-Null
        (Test-Path -LiteralPath $f) | Should -Be $true
        (Get-GimBytes $f).Length | Should -Be 0
    }
    It 'refuses to overwrite with --create-empty' {
        $f = Join-Path $scratch 'exists.txt'
        [System.IO.File]::WriteAllText($f, 'x', (New-Object System.Text.UTF8Encoding($false)))
        { gim-apply --create-empty $f } | Should -Throw -ExpectedMessage '*already exists*'
    }
    It 'touches an existing file and creates a missing one' {
        $f = Join-Path $scratch 'touch.txt'
        [System.IO.File]::WriteAllText($f, 'x', (New-Object System.Text.UTF8Encoding($false)))
        (Get-Item -LiteralPath $f).LastWriteTime = [datetime]'2020-01-01'
        gim-apply --touch $f | Out-Null
        (Get-Item -LiteralPath $f).LastWriteTime.Year | Should -BeGreaterThan 2020
        $g = Join-Path $scratch 'made.txt'
        gim-apply --touch $g | Out-Null
        (Test-Path -LiteralPath $g) | Should -Be $true
    }
    It 'removes a file and treats missing as a no-op' {
        $f = Join-Path $scratch 'gone.txt'
        [System.IO.File]::WriteAllText($f, 'x', (New-Object System.Text.UTF8Encoding($false)))
        gim-apply --remove $f | Out-Null
        (Test-Path -LiteralPath $f) | Should -Be $false
        $m = Join-Path $scratch 'never.txt'
        $out = @(gim-apply --remove $m)
        $out[0] | Should -Match 'changed=False'
    }
    It 'applies an action over a pipeline set' {
        $a = Join-Path $scratch 'a.txt'
        $b = Join-Path $scratch 'b.txt'
        @($a, $b) | gim-apply --create-empty | Out-Null
        (Test-Path -LiteralPath $a) | Should -Be $true
        (Test-Path -LiteralPath $b) | Should -Be $true
    }
    It 'leaves everything untouched with --dry-run' {
        $f = Join-Path $scratch 'keep.txt'
        gim-apply --create-empty $f | Out-Null
        $before = Get-GimBytes $f
        $out = @(gim-apply --remove $f --dry-run)
        $out[0] | Should -Match 'changed=True'
        (Test-Path -LiteralPath $f) | Should -Be $true
        (Get-GimBytes $f) | Should -Be $before
    }
    It 'requires exactly one action' {
        { gim-apply (Join-Path $scratch 'x.txt') } | Should -Throw -ExpectedMessage '*missing action*'
        { gim-apply --create-empty --touch (Join-Path $scratch 'x.txt') } | Should -Throw -ExpectedMessage '*exactly one action*'
    }
    It 'rejects non-path pipeline items with a shape error' {
        { [pscustomobject]@{ path = 'x' } | gim-apply --touch } | Should -Throw -ExpectedMessage '*invalid pipeline item*'
        { '' | gim-apply --touch } | Should -Throw -ExpectedMessage '*invalid pipeline item*'
    }
    It 'rejects directories' {
        { gim-apply --create-empty $scratch } | Should -Throw -ExpectedMessage '*directory*'
    }
    It 'emits a JSON envelope with --json' {
        $f = Join-Path $scratch 'j.txt'
        $env = gim-apply --create-empty $f --json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].action | Should -Be 'create-empty'
        @($env.items)[0].changed | Should -Be $true
    }
    It 'creates an empty file under the OS temp root' {
        $f = Join-Path $tempScratch 'temp-new.txt'
        $out = @(gim-apply --create-empty $f)
        $out[0] | Should -Match 'changed=True'
        (Test-Path -LiteralPath $f) | Should -Be $true
        (Get-GimBytes $f).Length | Should -Be 0
    }
    It 'touches an existing temp file and creates a missing one' {
        $f = Join-Path $tempScratch 'temp-touch.txt'
        [System.IO.File]::WriteAllText($f, 'x', (New-Object System.Text.UTF8Encoding($false)))
        (Get-Item -LiteralPath $f).LastWriteTime = [datetime]'2020-01-01'
        gim-apply --touch $f | Out-Null
        (Get-Item -LiteralPath $f).LastWriteTime.Year | Should -BeGreaterThan 2020
        $g = Join-Path $tempScratch 'temp-made.txt'
        gim-apply --touch $g | Out-Null
        (Test-Path -LiteralPath $g) | Should -Be $true
    }
    It 'removes a file under the OS temp root' {
        $f = Join-Path $tempScratch 'temp-gone.txt'
        [System.IO.File]::WriteAllText($f, 'x', (New-Object System.Text.UTF8Encoding($false)))
        $out = @(gim-apply --remove $f)
        $out[0] | Should -Match 'changed=True'
        (Test-Path -LiteralPath $f) | Should -Be $false
    }
    It 'touches nothing under the OS temp root with --dry-run' {
        $f = Join-Path $tempScratch 'temp-dry.txt'
        $out = @(gim-apply --create-empty $f --dry-run)
        $out[0] | Should -Match 'changed=True'
        (Test-Path -LiteralPath $f) | Should -Be $false
    }
    It 'rejects a path outside both the repo and the OS temp root' {
        { gim-apply --create-empty 'C:\Windows\gim-apply-nope.txt' } | Should -Throw -ExpectedMessage '*not contained*'
        Push-Location $repoRoot
        try {
            { gim-apply --touch '..\..\..\Windows\gim-apply-nope.txt' } | Should -Throw -ExpectedMessage '*not contained*'
        } finally {
            Pop-Location
        }
    }
    It 'rejects unexpanded %VAR% targets' {
        { gim-apply --create-empty '%TEMP%\gim-apply-nope.txt' } | Should -Throw -ExpectedMessage '*environment variables*'
    }
}

Describe 'gim-apply temp-root safety floor (module-internal)' {
    InModuleScope GIMTools {
        It 'accepts the current real OS temp root' {
            $temp = [System.IO.Path]::GetTempPath()
            { Resolve-GimSafeTempRoot -Path $temp } | Should -Not -Throw
        }
        It 'rejects a drive root' {
            $driveRoot = [System.IO.Path]::GetPathRoot((Get-GimRepoRoot))
            { Resolve-GimSafeTempRoot -Path $driveRoot } | Should -Throw -ExpectedMessage '*drive root*'
        }
        It 'rejects the user profile' {
            { Resolve-GimSafeTempRoot -Path $env:USERPROFILE } | Should -Throw -ExpectedMessage '*protected directory*'
        }
        It 'rejects a parent of the repo root' {
            $repoParent = Split-Path -Parent (Get-GimRepoRoot)
            { Resolve-GimSafeTempRoot -Path $repoParent } | Should -Throw -ExpectedMessage '*protected directory*'
        }
        It 'rejects a path shallower than two segments below the drive root' {
            $shallow = Join-Path ([System.IO.Path]::GetPathRoot((Get-GimRepoRoot))) 'gim-shallow-temp'
            { Resolve-GimSafeTempRoot -Path $shallow } | Should -Throw -ExpectedMessage '*at least two*'
        }
        It 'rejects a UNC path' {
            { Resolve-GimSafeTempRoot -Path '\\server\share\gim-temp' } | Should -Throw -ExpectedMessage '*UNC*'
        }
        It 'rejects unexpanded environment tokens' {
            { Resolve-GimSafeTempRoot -Path '%TEMP%\gim-apply' } | Should -Throw -ExpectedMessage '*environment variables*'
        }
    }
}

