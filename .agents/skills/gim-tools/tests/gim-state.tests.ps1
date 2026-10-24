# gim-state.tests.ps1 - Pester 6 tests for the A5 state verbs (gim-set /
# gim-get): set/get roundtrip incl. multi-line, tee passthrough, TTL expiry,
# --max-age, name regex rejection (+ POSIX -- for leading-dash names),
# atomicity under concurrent writes (no errors, no torn file, single file),
# sweep (incl. legacy .val), per-variable and namespace caps (no stdout on cap
# failure), audit log, --json contracts, GIM_VARS_ROOT validation, and TTL
# enforcement on --from-var consumers (gim-diff --against-var).
# The store lives in a temp dir via GIM_VARS_ROOT (restored in AfterAll), so
# nothing outside temp/scratch mutates.
# Pester 6 syntax: Should -Be / -Throw / -Match / -Contain / -Not /
# -BeGreaterOrEqual / -ExpectedMessage.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

Describe 'gim-set / gim-get' {
    BeforeAll {
        $null = Get-GimTestRepoRoot   # dot-source sanity (global helper)
        $storeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-state-' + [guid]::NewGuid().ToString('N'))
        $oldVarsRoot = $env:GIM_VARS_ROOT
        $env:GIM_VARS_ROOT = $storeRoot
    }
    AfterAll {
        if ($null -eq $oldVarsRoot) { Remove-Item Env:GIM_VARS_ROOT -ErrorAction SilentlyContinue } else { $env:GIM_VARS_ROOT = $oldVarsRoot }
        Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'round-trips a single-line value' {
        'hello world' | gim-set alpha | Out-Null
        (gim-get alpha) | Should -Be 'hello world'
    }
    It 'round-trips a multi-line value' {
        @('line1', 'line2', 'line3') | gim-set multi | Out-Null
        @(gim-get multi) | Should -Be @('line1', 'line2', 'line3')
    }
    It 'tees the stream through mid-pipeline' {
        $out = @(@('a', 'b', 'c') | gim-set tee1)
        $out | Should -Be @('a', 'b', 'c')
        @(gim-get tee1) | Should -Be @('a', 'b', 'c')
    }
    It 'stores an empty value when no stream arrives' {
        gim-set emptyvar | Out-Null
        (gim-get emptyvar) | Should -Be ''
    }
    It 'keeps a fresh value within its ttl' {
        'fresh' | gim-set freshvar --ttl 60 | Out-Null
        (gim-get freshvar) | Should -Be 'fresh'
    }
    It 'treats ttl 0 as never-expiring' {
        'forever' | gim-set forevervar --ttl 0 | Out-Null
        (gim-get forevervar) | Should -Be 'forever'
    }
    It 'expires a value past its ttl and never returns it' {
        'stale' | gim-set stalevar --ttl 1 | Out-Null
        Start-Sleep -Seconds 2
        { gim-get stalevar } | Should -Throw -ExpectedMessage '*expired*'
    }
    It 'enforces --max-age on read' {
        'old' | gim-set agevar | Out-Null
        Start-Sleep -Seconds 2
        { gim-get agevar --max-age 1 } | Should -Throw -ExpectedMessage '*max-age*'
        (gim-get agevar --max-age 10) | Should -Be 'old'
    }
    It 'rejects names that do not match the invariant' {
        { gim-set '../../evil' } | Should -Throw -ExpectedMessage '*invalid variable name*'
        { 'v' | gim-set 'a.b' } | Should -Throw -ExpectedMessage '*invalid variable name*'
        { 'v' | gim-set 'has space' } | Should -Throw -ExpectedMessage '*invalid variable name*'
        { gim-get '../x' } | Should -Throw -ExpectedMessage '*invalid variable name*'
    }
    It 'accepts leading-dash names (POSIX -- end-of-options)' {
        # The PowerShell binder consumes the '--' marker itself, so both the
        # explicit form and the bare form route '-dashname' to the positional
        # name (Convert-GimArgs treats unknown dash tokens as positional).
        'v' | gim-set -- -dashname | Out-Null
        (gim-get -- -dashname) | Should -Be 'v'
        'w' | gim-set -otherdash | Out-Null
        (gim-get -otherdash) | Should -Be 'w'
    }
    It 'errors on a missing variable' {
        { gim-get nosuchvar } | Should -Throw -ExpectedMessage '*variable not set*'
    }
    It 'removes a variable with --clear' {
        'x' | gim-set clrvar | Out-Null
        gim-set --clear clrvar | Out-Null
        { gim-get clrvar } | Should -Throw -ExpectedMessage '*variable not set*'
    }
    It 'removes every variable with --clear-all' {
        'x' | gim-set ca1 | Out-Null
        'y' | gim-set ca2 | Out-Null
        gim-set --clear-all | Out-Null
        { gim-get ca1 } | Should -Throw -ExpectedMessage '*variable not set*'
        { gim-get ca2 } | Should -Throw -ExpectedMessage '*variable not set*'
    }
    It 'stores one raw file per variable and rewrites the ttl suffix in place' {
        'v' | gim-set singlefile | Out-Null
        @(Get-ChildItem -LiteralPath $storeRoot -Filter 'singlefile*' -File).Count | Should -Be 1
        @(Get-ChildItem -LiteralPath $storeRoot -Filter '*.meta' -File).Count | Should -Be 0
        'v2' | gim-set singlefile --ttl 60 | Out-Null
        @(Get-ChildItem -LiteralPath $storeRoot -Filter 'singlefile*' -File).Count | Should -Be 1
        (gim-get singlefile) | Should -Be 'v2'
    }
    It 'emits the set contract with --json' {
        $res = 'x' | gim-set jsvar --json | ConvertFrom-Json
        $res.name | Should -Be 'jsvar'
        $res.ttl | Should -Be 3600
        $res.stored_bytes | Should -Be 1
    }
    It 'emits the get contract with --json' {
        'hello' | gim-set jgvar | Out-Null
        $res = gim-get jgvar --json | ConvertFrom-Json
        $res.name | Should -Be 'jgvar'
        $res.value | Should -Be 'hello'
        $res.ttl_seconds | Should -Be 3600
        $res.expired | Should -Be $false
        $res.age_seconds | Should -BeGreaterOrEqual 0
    }
    It 'leaves no torn file and no job errors under concurrent writes' {
        $mp = Join-Path (Get-GimTestRepoRoot) '.agents\skills\gim-tools\scripts\GIMTools\GIMTools.psd1'
        $jobCount = 8
        $iterations = 20
        $values = @()
        for ($i = 0; $i -lt $jobCount; $i++) { $values += ('JOB' + $i + '-PAD-' + ('x' * 20000)) }
        $jobs = @()
        for ($j = 0; $j -lt $jobCount; $j++) {
            $jobs += Start-Job -ScriptBlock {
                param($mpArg, $rootArg, $valArg, $iterArg)
                $env:GIM_VARS_ROOT = $rootArg
                Import-Module -Force $mpArg -DisableNameChecking
                for ($k = 0; $k -lt $iterArg; $k++) {
                    $valArg | gim-set atomicvar | Out-Null
                }
            } -ArgumentList $mp, $storeRoot, $values[$j], $iterations
        }
        try {
            $null = $jobs | Wait-Job
            $failed = @($jobs | Where-Object { $_.State -ne 'Completed' })
            $failed.Count | Should -Be 0
            $jobErrs = @($jobs | Receive-Job -ErrorAction SilentlyContinue -ErrorVariable jobErrRecords)
            $jobErrRecords.Count | Should -Be 0
            $read = @(gim-get atomicvar)
            $read.Count | Should -Be 1
            $values | Should -Contain $read[0]
            @(Get-ChildItem -LiteralPath $storeRoot -Filter 'atomicvar*.val' -File).Count | Should -Be 1
        } finally {
            $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
        }
    }
    It 'sweeps expired variables on the next set' {
        'x' | gim-set swvar1 --ttl 1 | Out-Null
        Start-Sleep -Seconds 2
        'y' | gim-set swvar2 | Out-Null
        { gim-get swvar1 } | Should -Throw -ExpectedMessage '*variable not set*'
        @(Get-ChildItem -LiteralPath $storeRoot -Filter 'swvar1*' -File).Count | Should -Be 0
    }
    It 'sweeps legacy .val files without a ttl suffix as default-ttl' {
        $legacyFile = Join-Path $storeRoot 'legacyvar.val'
        [System.IO.File]::WriteAllText($legacyFile, 'legacy', (New-Object System.Text.UTF8Encoding($false)))
        (Get-Item -LiteralPath $legacyFile).LastWriteTimeUtc = [datetime]::UtcNow.AddHours(-2)
        'y' | gim-set trigsweep | Out-Null
        (Test-Path -LiteralPath $legacyFile) | Should -Be $false
    }
    It 'emits no stdout when the value exceeds the per-variable cap' {
        $out = @()
        $threw = $false
        try { $out = @('x' * 1048577 | gim-set bigvar) } catch { $threw = $true }
        $threw | Should -Be $true
        $out.Count | Should -Be 0
        (Test-Path -LiteralPath (Join-Path $storeRoot 'bigvar.t3600.val')) | Should -Be $false
    }
    It 'rejects writes that exceed the 10 MiB namespace cap at the boundary' {
        gim-set --clear-all | Out-Null
        for ($i = 0; $i -lt 10; $i++) {
            ('x' * 1048576) | gim-set ("cap$i") | Out-Null
        }
        { 'small' | gim-set cap11 } | Should -Throw -ExpectedMessage '*namespace cap*'
    }
    It 'counts legacy variable files toward the namespace cap' {
        gim-set --clear-all | Out-Null
        $legacy = Join-Path $storeRoot 'legacybig.val'
        [System.IO.File]::WriteAllText($legacy, ('x' * 1048576), (New-Object System.Text.UTF8Encoding($false)))
        for ($i = 0; $i -lt 9; $i++) {
            ('x' * 1048576) | gim-set ("bc$i") | Out-Null
        }
        { 'small' | gim-set bc9 } | Should -Throw -ExpectedMessage '*namespace cap*'
        Remove-Item -LiteralPath $legacy -Force -ErrorAction SilentlyContinue
    }
    It 'enforces expiry on --from-var consumers (gim-diff --against-var)' {
        $repoRoot = Get-GimTestRepoRoot
        $target = Join-Path $repoRoot '.gitignore'
        "$target" | gim-set varfresh | Out-Null
        $out = @(@($target) | gim-diff --against-var varfresh)
        $out.Count | Should -BeGreaterThan 0
        "$target" | gim-set varexpired --ttl 1 | Out-Null
        Start-Sleep -Seconds 2
        { @($target) | gim-diff --against-var varexpired } | Should -Throw -ExpectedMessage '*expired*'
    }
    It 'rejects an invalid GIM_VARS_ROOT' {
        $old = $env:GIM_VARS_ROOT
        try {
            $env:GIM_VARS_ROOT = 'relative\path'
            { 'x' | gim-set rootvar } | Should -Throw -ExpectedMessage '*invalid store root*'
            $env:GIM_VARS_ROOT = '\\server\share'
            { 'x' | gim-set rootvar } | Should -Throw -ExpectedMessage '*invalid store root*'
            $env:GIM_VARS_ROOT = '%TEMP%\gim'
            { 'x' | gim-set rootvar } | Should -Throw -ExpectedMessage '*invalid store root*'
            $env:GIM_VARS_ROOT = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-valid-' + [guid]::NewGuid().ToString('N'))
            'ok' | gim-set rootvar | Out-Null
            (gim-get rootvar) | Should -Be 'ok'
        } finally {
            $env:GIM_VARS_ROOT = $old
        }
    }
    It 'appends to the audit log' {
        gim-set --clear-all | Out-Null
        'a' | gim-set aud1 | Out-Null
        'b' | gim-set aud2 | Out-Null
        $audit = @(Get-Content -LiteralPath (Join-Path $storeRoot '_audit.log') -ErrorAction SilentlyContinue)
        $audit.Count | Should -BeGreaterOrEqual 2
        ($audit -join "`n") | Should -Match 'set aud1'
        ($audit -join "`n") | Should -Match 'set aud2'
    }
    It 'rejects an invalid --ttl value' {
        { 'x' | gim-set badttl --ttl abc } | Should -Throw -ExpectedMessage '*invalid --ttl*'
    }
}
