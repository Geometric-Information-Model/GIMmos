# gim-inspection.tests.ps1 - Pester 6 tests for the A3 inspection verbs:
# gim-inspect, gim-head, gim-count, gim-diff, gim-json.
# Fixtures live under repo .agent-scratch/ (gitignored, contained in roots);
# nothing else outside temp/scratch is mutated (the gim-diff --against-var
# roundtrip redirects $env:LOCALAPPDATA to a temp store, restored in finally).
# Pester 6 syntax: Should -Be / -Throw / -Match / -Contain.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

Describe 'gim-inspect' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-inspect-' + [guid]::NewGuid().ToString('N'))
        $text = Join-Path $scratch 'text.txt'
        $lf = Join-Path $scratch 'lf.txt'
        $noeof = Join-Path $scratch 'noeof.txt'
        $ws = Join-Path $scratch 'ws.txt'
        $empty = Join-Path $scratch 'empty.txt'
        $utf16 = Join-Path $scratch 'utf16.txt'
        $bom = Join-Path $scratch 'bom.txt'
        $bin = Join-Path $scratch 'bin.dat'
        $enc = New-Object System.Text.UTF8Encoding($false)
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        [System.IO.File]::WriteAllText($text, "line one`r`nline two`r`nline three`r`n", $enc)
        [System.IO.File]::WriteAllText($lf, "a`nb`n", $enc)
        [System.IO.File]::WriteAllText($noeof, 'abc', $enc)
        [System.IO.File]::WriteAllText($ws, "a  `tb  `r`n", $enc)
        [System.IO.File]::WriteAllText($empty, '', $enc)
        [System.IO.File]::WriteAllText($utf16, 'hi', [System.Text.Encoding]::Unicode)
        [System.IO.File]::WriteAllText($bom, 'hi', (New-Object System.Text.UTF8Encoding($true)))
        [System.IO.File]::WriteAllBytes($bin, [byte[]]@(65, 0, 66))
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'reports kind=file and counts bytes and lines' {
        $out = @(gim-inspect $text --bytes --lines)
        $out.Count | Should -Be 1
        $out[0] | Should -Match 'kind=file'
        $out[0] | Should -Match 'bytes='
        $out[0] | Should -Match 'lines=3'
    }
    It 'caps records and marks truncation' {
        $paths = @()
        for ($i = 0; $i -lt 600; $i++) { $paths += (Join-Path $scratch ('nope' + $i)) }
        $env = @($paths | gim-inspect --json) | ConvertFrom-Json
        $env.count | Should -Be 600
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 500
        $out = @($paths | gim-inspect)
        $out.Count | Should -Be 501
        $out[-1] | Should -Match 'output truncated'
    }
    It 'reports kind=missing without erroring' {
        $out = @(gim-inspect (Join-Path $scratch 'nope.txt'))
        $out.Count | Should -Be 1
        $out[0] | Should -Match 'kind=missing'
    }
    It 'reports kind=directory' {
        @(gim-inspect $scratch)[0] | Should -Match 'kind=directory'
    }
    It 'detects line endings and eof newline' {
        @(gim-inspect $text --line-endings --eof-newline)[0] | Should -Match 'lineEndings=crlf'
        @(gim-inspect $text --line-endings --eof-newline)[0] | Should -Match 'eofNewline=True'
        @(gim-inspect $lf --line-endings)[0] | Should -Match 'lineEndings=lf'
        @(gim-inspect $noeof --line-endings --eof-newline)[0] | Should -Match 'lineEndings=none'
        @(gim-inspect $noeof --line-endings --eof-newline)[0] | Should -Match 'eofNewline=False'
    }
    It 'detects tabs and trailing whitespace' {
        @(gim-inspect $ws --tabs --trailing-ws)[0] | Should -Match 'tabs=True'
        @(gim-inspect $ws --tabs --trailing-ws)[0] | Should -Match 'trailingWs=True'
        @(gim-inspect $text --tabs --trailing-ws)[0] | Should -Match 'trailingWs=False'
    }
    It 'reports max line length' {
        @(gim-inspect $text --max-line-len)[0] | Should -Match 'maxLineLen=10'
    }
    It 'detects BOM and encoding' {
        @(gim-inspect $bom --bom --encoding)[0] | Should -Match 'bom=True'
        @(gim-inspect $bom --bom --encoding)[0] | Should -Match 'encoding=utf-8-bom'
        @(gim-inspect $utf16 --encoding)[0] | Should -Match 'encoding=utf-16le'
        @(gim-inspect $text --encoding)[0] | Should -Match 'encoding=utf-8'
    }
    It 'detects binary content' {
        @(gim-inspect $bin --binary)[0] | Should -Match 'binary=True'
    }
    It 'emits a SHA256 hash with --hash' {
        $out = @(gim-inspect $text --hash)
        $out[0] | Should -Match 'hash=[0-9a-f]{64}'
    }
    It 'rejects a path outside the roots' {
        { gim-inspect 'C:\Windows' } | Should -Throw
    }
    It 'emits a JSON envelope with --json' {
        $env = gim-inspect $text --bytes --json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].kind | Should -Be 'file'
        @($env.items)[0].path | Should -Be $text
    }
}

Describe 'gim-head' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-head-' + [guid]::NewGuid().ToString('N'))
        $fileA = Join-Path $scratch 'a.txt'
        $fileB = Join-Path $scratch 'b.txt'
        $enc = New-Object System.Text.UTF8Encoding($false)
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        [System.IO.File]::WriteAllText($fileA, "one`r`ntwo`r`nthree`r`nfour`r`n", $enc)
        [System.IO.File]::WriteAllText($fileB, "x1`r`nx2`r`n", $enc)
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'prints the first N lines' {
        $out = @(gim-head $fileA -n 2)
        $out | Should -Be @('one', 'two')
    }
    It 'applies --skip before the head window' {
        $out = @(gim-head $fileA -n 2 --skip 1)
        $out | Should -Be @('two', 'three')
    }
    It 'prints the last N lines with --last' {
        $out = @(gim-head $fileA -n 2 --last)
        $out | Should -Be @('three', 'four')
    }
    It 'emits nothing for -n 0' {
        @(gim-head $fileA -n 0).Count | Should -Be 0
        @(gim-head $fileA -n 0 --last).Count | Should -Be 0
    }
    It 'caps records and marks truncation' {
        $big = Join-Path $scratch 'big.txt'
        $lines = New-Object System.Collections.Generic.List[string]
        for ($i = 0; $i -lt 600; $i++) { $lines.Add('line' + $i) }
        [System.IO.File]::WriteAllText($big, ($lines.ToArray() -join "`r`n"), $enc)
        $env = gim-head $big -n 600 --json | ConvertFrom-Json
        $env.count | Should -Be 600
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 500
        $out = @(gim-head $big -n 600)
        $out.Count | Should -Be 501
        $out[-1] | Should -Match 'output truncated'
    }
    It 'prefixes lines with the path for multiple files' {
        $out = @(@($fileA, $fileB) | gim-head -n 1)
        $out.Count | Should -Be 2
        $out[0] | Should -Match ('.*a\.txt: one$')
        $out[1] | Should -Match ('.*b\.txt: x1$')
    }
    It 'treats a piped text stream as lines (Get-Content | gim-head)' {
        $out = @(Get-Content $fileA | gim-head -n 2 --skip 1)
        $out | Should -Be @('two', 'three')
    }
    It 'heads a raw string stream' {
        $out = @(@('a', 'b', 'c', 'd') | gim-head -n 2)
        $out | Should -Be @('a', 'b')
    }
    It 'tails a raw string stream with --last' {
        $out = @(@('a', 'b', 'c', 'd') | gim-head -n 2 --last)
        $out | Should -Be @('c', 'd')
    }
    It 'applies the stream window across the whole stream' {
        $out = @(@('a', 'b', 'c', 'd') | gim-head -n 2 --skip 2)
        $out | Should -Be @('c', 'd')
    }
    It 'emits nothing for a stream with -n 0' {
        @(@('a', 'b', 'c') | gim-head -n 0).Count | Should -Be 0
    }
    It 'treats piped existing file paths as files' {
        $out = @(@($fileA) | gim-head -n 2)
        $out | Should -Be @('one', 'two')
    }
    It 'treats non-path piped items as stream lines' {
        $out = @('not-a-path' | gim-head -n 1)
        $out | Should -Be @('not-a-path')
    }
    It 'heads grep output piped as a stream' {
        $grepFile = Join-Path $scratch 'grepme.txt'
        [System.IO.File]::WriteAllText($grepFile, "skip`r`nmatch-one`r`nmatch-two`r`n", $enc)
        $out = @(gim-list --glob ".agent-scratch/gim-head-*/grepme.txt" | gim-grep --pattern "match-" | gim-head -n 1)
        $out.Count | Should -Be 1
        $out[0] | Should -Match 'grepme\.txt:2: match-one$'
    }
    It 'emits (stdin) records with --json for stream lines' {
        $env = @('a', 'b') | gim-head -n 1 --json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].path | Should -Be '(stdin)'
        @($env.items)[0].line | Should -Be 1
        @($env.items)[0].text | Should -Be 'a'
    }
    It 'emits path/line/text records with --json' {
        $env = gim-head $fileA -n 2 --json | ConvertFrom-Json
        $env.count | Should -Be 2
        @($env.items)[0].path | Should -Be $fileA
        @($env.items)[0].line | Should -Be 1
        @($env.items)[0].text | Should -Be 'one'
        @($env.items)[1].line | Should -Be 2
    }
    It 'errors on missing paths' {
        { gim-head (Join-Path $scratch 'nope.txt') -n 1 } | Should -Throw
    }
}

Describe 'gim-count' {
    It 'counts pipeline items' {
        @((@('a', 'b', 'a') | gim-count)) | Should -Be @('3')
    }
    It 'emits a JSON envelope with the scalar count' {
        $env = @('a', 'b', 'a') | gim-count --json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].count | Should -Be 3
    }
    It 'caps groups and marks truncation' {
        $many = @()
        for ($i = 0; $i -lt 600; $i++) { $many += ('g' + $i) }
        $env = @($many | gim-count --by x --json) | ConvertFrom-Json
        $env.count | Should -Be 600
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 500
    }
    It 'groups strings by value with --by' {
        $out = @((@('a', 'b', 'a', 'c') | gim-count --by value))
        $out | Should -Be @('a: 2', 'b: 1', 'c: 1')
    }
    It 'groups records by field with --by' {
        $records = @(
            [pscustomobject]@{ name = 'a'; kind = 'x' },
            [pscustomobject]@{ name = 'b'; kind = 'y' },
            [pscustomobject]@{ name = 'c'; kind = 'x' }
        )
        $out = @($records | gim-count --by kind)
        $out | Should -Be @('x: 2', 'y: 1')
    }
    It 'emits group/count records with --by --json' {
        $env = @((@('a', 'b', 'a') | gim-count --by value --json)) | ConvertFrom-Json
        $env.count | Should -Be 2
        @($env.items)[0].group | Should -Be 'a'
        @($env.items)[0].count | Should -Be 2
    }
    It 'rejects positional arguments' {
        { gim-count 'x' } | Should -Throw
    }
}

Describe 'gim-diff' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-diff-' + [guid]::NewGuid().ToString('N'))
        $scratchName = Split-Path -Leaf $scratch
        $leftA = Join-Path $scratch 'leftA.txt'
        $rightB = Join-Path $scratch 'rightB.txt'
        $rightC = Join-Path $scratch 'rightC.txt'
        $enc = New-Object System.Text.UTF8Encoding($false)
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        foreach ($f in @($leftA, $rightB, $rightC)) {
            [System.IO.File]::WriteAllText($f, 'x', $enc)
        }
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'reports only-left / only-right / both' {
        $out = @(@($leftA, $rightB) | gim-diff --against $rightB --against $rightC)
        $out.Count | Should -Be 3
        $out[0] | Should -Match ('^only-left: .*leftA\.txt$')
        $out[1] | Should -Match ('^only-right: .*rightC\.txt$')
        $out[2] | Should -Match ('^both: .*rightB\.txt$')
    }
    It 'expands a glob --against' {
        $out = @(@($leftA, $rightB) | gim-diff --against (".agent-scratch/$scratchName/right*.txt"))
        $out.Count | Should -Be 3
    }
    It 'reads the right set from the variable store with --against-var' {
        $old = $env:LOCALAPPDATA
        $tmpStore = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-store-' + [guid]::NewGuid().ToString('N'))
        try {
            # Write the store file directly (same layout as the module's
            # Set-GimVarValue: %LOCALAPPDATA%\gim\vars\<sanitized-root>\<name>.val,
            # raw text, no envelope) so the test does not depend on variables
            # crossing into the module scope.
            $env:LOCALAPPDATA = $tmpStore
            $sanitized = ($repoRoot -replace '\\', '_' -replace ':', '')
            $storeDir = Join-Path (Join-Path $tmpStore 'gim\vars') $sanitized
            New-Item -ItemType Directory -Path $storeDir -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $storeDir 'diffright.val'), "$rightB`n$rightC`n", $enc)
            $out = @(@($leftA, $rightB) | gim-diff --against-var diffright)
            $out.Count | Should -Be 3
            $out[0] | Should -Match ('^only-left: .*leftA\.txt$')
            $out[2] | Should -Match ('^both: .*rightB\.txt$')
        } finally {
            $env:LOCALAPPDATA = $old
            Remove-Item -LiteralPath $tmpStore -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    It 'accepts gim-inspect record lines as the left set' {
        $out = @(@("${leftA}: kind=file hash=abc") | gim-diff --against $rightB)
        $out.Count | Should -Be 2
        $out[0] | Should -Match ('^only-left: .*leftA\.txt$')
        $out[1] | Should -Match ('^only-right: .*rightB\.txt$')
    }
    It 'reads record-form lines from --against-var' {
        $old = $env:LOCALAPPDATA
        $tmpStore = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-store-' + [guid]::NewGuid().ToString('N'))
        try {
            $env:LOCALAPPDATA = $tmpStore
            $sanitized = ($repoRoot -replace '\\', '_' -replace ':', '')
            $storeDir = Join-Path (Join-Path $tmpStore 'gim\vars') $sanitized
            New-Item -ItemType Directory -Path $storeDir -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $storeDir 'diffright.val'), "${rightB}: kind=file hash=xyz`n${rightC}: kind=file hash=xyz`n", $enc)
            $out = @(@($leftA, $rightB) | gim-diff --against-var diffright)
            $out.Count | Should -Be 3
            $out[0] | Should -Match ('^only-left: .*leftA\.txt$')
            $out[2] | Should -Match ('^both: .*rightB\.txt$')
        } finally {
            $env:LOCALAPPDATA = $old
            Remove-Item -LiteralPath $tmpStore -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    It 'emits path/state records with --json' {
        $env = @(@($leftA, $rightB) | gim-diff --against $rightB --json) | ConvertFrom-Json
        $env.count | Should -Be 2
        @($env.items)[0].state | Should -Be 'only-left'
        @($env.items)[1].state | Should -Be 'both'
    }
    It 'caps records and marks truncation' {
        $manyDir = Join-Path $scratch 'many'
        New-Item -ItemType Directory -Path $manyDir -Force | Out-Null
        $leftPaths = @()
        for ($i = 0; $i -lt 600; $i++) {
            $f = Join-Path $manyDir ('f' + $i + '.txt')
            [System.IO.File]::WriteAllText($f, 'x', $enc)
            $leftPaths += $f
        }
        $env = @($leftPaths | gim-diff --against ".agent-scratch/nomatch-xyz/*.txt" --json) | ConvertFrom-Json
        $env.count | Should -Be 600
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 500
    }
    It 'errors without a right set' {
        { @($leftA) | gim-diff } | Should -Throw
    }
}

Describe 'gim-json' {
    BeforeAll {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-json-' + [guid]::NewGuid().ToString('N'))
        $data = Join-Path $scratch 'data.json'
        $bad = Join-Path $scratch 'bad.json'
        $doc = Join-Path $scratch 'doc.yaml'
        $enc = New-Object System.Text.UTF8Encoding($false)
    }

    BeforeEach {
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        [System.IO.File]::WriteAllText($data, '{"a":{"b":[{"c":1},{"c":2}]},"name":"x","list":[10,20,30]}', $enc)
        [System.IO.File]::WriteAllText($bad, '{invalid', $enc)
        [System.IO.File]::WriteAllText($doc, "name: yaml`r`n", $enc)
    }
    AfterEach {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'queries a scalar with --path' {
        @(gim-json $data --path name) | Should -Be @('x')
    }
    It 'queries dotted and bracket paths' {
        @(gim-json $data --path 'a.b[0].c') | Should -Be @('1')
        @(gim-json $data --path 'a.b[1].c') | Should -Be @('2')
    }
    It 'expands array values one per line' {
        $out = @(gim-json $data --path list)
        $out | Should -Be @('10', '20', '30')
    }
    It 'lists keys at the queried node with --keys' {
        @(gim-json $data --path 'a.b[0]' --keys) | Should -Be @('c')
        $out = @(gim-json $data --keys)
        $out | Should -Contain 'a'
        $out | Should -Contain 'name'
        $out | Should -Contain 'list'
    }
    It 'emits no keys for scalar nodes with --keys' {
        @(gim-json $data --path name --keys).Count | Should -Be 0
        @(gim-json $data --path 'a.b[0].c' --keys).Count | Should -Be 0
        @(gim-json $data --path missing --keys).Count | Should -Be 0
    }
    It 'lists array indices with --keys on an array node' {
        $out = @(gim-json $data --path list --keys)
        $out | Should -Be @('0', '1', '2')
    }
    It 'validates JSON documents with --validate' {
        @(gim-json $data --validate)[0] | Should -Match 'valid=True'
        @(gim-json $bad --validate)[0] | Should -Match 'valid=False'
    }
    It 'errors on invalid JSON without --validate' {
        { gim-json $bad --path name } | Should -Throw
    }
    It 'rejects YAML input with a clear unsupported-format error' {
        { gim-json $doc --path name } | Should -Throw -ExpectedMessage '*unsupported format*'
    }
    It 'errors on a missing path' {
        { gim-json (Join-Path $scratch 'nope.json') } | Should -Throw
    }
    It 'caps key records and marks truncation' {
        $big = Join-Path $scratch 'big.json'
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.Append('{')
        for ($i = 0; $i -lt 600; $i++) {
            if ($i -gt 0) { [void]$sb.Append(',') }
            [void]$sb.Append(('"k{0}": {0}' -f $i))
        }
        [void]$sb.Append('}')
        [System.IO.File]::WriteAllText($big, $sb.ToString(), $enc)
        $env = gim-json $big --keys --json | ConvertFrom-Json
        $env.count | Should -Be 600
        $env.truncated | Should -Be $true
        @($env.items).Count | Should -Be 500
        $out = @(gim-json $big --keys)
        $out.Count | Should -Be 501
        $out[-1] | Should -Match 'output truncated'
    }
    It 'emits path/value records with --json' {
        $env = gim-json $data --path name --json | ConvertFrom-Json
        $env.count | Should -Be 1
        @($env.items)[0].path | Should -Be $data
        @($env.items)[0].value | Should -Be 'x'
    }
}
