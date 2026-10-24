# gim-execution.tests.ps1 - Pester 6 tests for the A7 execution verbs:
# gim-run, gim-probe, and the shared GimExec process launcher.
# Real execution is limited to trivial child processes (pwsh echo/sleep
# commands, plus a grandchild-holds-the-pipe drain-bound check) through the
# module-internal Invoke-GimExec - never a real dotnet build/test. --dry-run
# covers argv, verbatim '--' trailing args, and env-scrub; GIM_RUN_AUDIT and
# the -AuditPath parameter keep audit lines hermetic (nothing touches the
# real %LOCALAPPDATA%\gim\run-audit.log).
# Pester 6 syntax: Should -Be / -Throw / -Match / -Contain / -Not.

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $here '..\scripts\GIMTools\GIMTools.psd1'
Import-Module $ModulePath -Force -DisableNameChecking
. (Join-Path $here 'gim-test-common.ps1')

function global:Get-GimExecProj {
    return Join-Path (Get-GimTestRepoRoot) 'TestPlatform\TestPlatform.csproj'
}

Describe 'gim-run argv dry-run' {
    It 'builds dotnet build argv with an absolute contained project' {
        $out = @(gim-run build --project TestPlatform/TestPlatform.csproj --dry-run)
        $out[0] | Should -Be 'dotnet'
        $out[1] | Should -Be 'build'
        $out[2] | Should -Be (Get-GimExecProj)
    }
    It 'builds dotnet test argv' {
        $out = @(gim-run test --project TestPlatform/TestPlatform.csproj --dry-run)
        $out[0] | Should -Be 'dotnet'
        $out[1] | Should -Be 'test'
        $out[2] | Should -Be (Get-GimExecProj)
    }
    It 'builds dotnet publish argv' {
        $out = @(gim-run publish --project TestPlatform/TestPlatform.csproj --dry-run)
        $out[0] | Should -Be 'dotnet'
        $out[1] | Should -Be 'publish'
        $out[2] | Should -Be (Get-GimExecProj)
    }
    It 'builds dotnet format argv' {
        $out = @(gim-run format --project TestPlatform/TestPlatform.csproj --dry-run)
        $out[0] | Should -Be 'dotnet'
        $out[1] | Should -Be 'format'
        $out[2] | Should -Be (Get-GimExecProj)
    }
    It 'builds dotnet run argv with -- before trailing args' {
        $out = @(gim-run run --project TestPlatform/TestPlatform.csproj --dry-run -- --urls http://127.0.0.1:5000)
        $out | Should -Contain 'dotnet'
        $out | Should -Contain 'run'
        $out | Should -Contain '--project'
        $out | Should -Contain '--'
        $out | Should -Contain '--urls'
    }
    It 'builds dotnet ef argv with --project' {
        $out = @(gim-run ef --project TestPlatform/TestPlatform.csproj --dry-run -- migrations add Init)
        $out[0] | Should -Be 'dotnet'
        $out[1] | Should -Be 'ef'
        $out | Should -Contain '--project'
        $out | Should -Contain 'migrations'
    }
    It 'appends trailing args for build after --' {
        $out = @(gim-run build --project TestPlatform/TestPlatform.csproj --dry-run -- -c Release)
        $out | Should -Contain '-c'
        $out | Should -Contain 'Release'
    }
    It 'defaults the project to the current directory' {
        Push-Location (Get-GimTestRepoRoot)
        try {
            $out = @(gim-run build --dry-run)
            $out[2] | Should -Be (Get-GimTestRepoRoot)
        } finally {
            Pop-Location
        }
    }
    It 'reads the project from the variable store with --project-from-var' {
        $storeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-run-store-' + [guid]::NewGuid().ToString('N'))
        $oldVarsRoot = $env:GIM_VARS_ROOT
        $env:GIM_VARS_ROOT = $storeRoot
        try {
            $proj = Get-GimExecProj
            "$proj" | gim-set runproj | Out-Null
            $out = @(gim-run build --project-from-var runproj --dry-run)
            $out[2] | Should -Be $proj
        } finally {
            if ($null -eq $oldVarsRoot) { Remove-Item Env:GIM_VARS_ROOT -ErrorAction SilentlyContinue } else { $env:GIM_VARS_ROOT = $oldVarsRoot }
            Remove-Item -LiteralPath $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    It 'emits the argv with --json' {
        $env = gim-run test --project TestPlatform/TestPlatform.csproj --dry-run --json | ConvertFrom-Json
        $env.dry_run | Should -Be $true
        $env.verb | Should -Be 'test'
        @($env.argv)[0] | Should -Be 'dotnet'
        @($env.argv)[2] | Should -Be (Get-GimExecProj)
    }
    It 'requires a verb and rejects unknown verbs' {
        { gim-run --dry-run } | Should -Throw -ExpectedMessage '*missing verb*'
        { gim-run bogus --project TestPlatform/TestPlatform.csproj --dry-run } | Should -Throw -ExpectedMessage '*invalid verb*'
    }
    It 'rejects --project and --project-from-var together' {
        { gim-run build --project x --project-from-var v --dry-run } | Should -Throw -ExpectedMessage '*not both*'
    }
    It 'validates --timeout range' {
        { gim-run build --project TestPlatform/TestPlatform.csproj --timeout 0 --dry-run } | Should -Throw -ExpectedMessage '*minimum 1*'
        { gim-run build --project TestPlatform/TestPlatform.csproj --timeout 1801 --dry-run } | Should -Throw -ExpectedMessage '*cap*'
        { gim-run build --project TestPlatform/TestPlatform.csproj --timeout abc --dry-run } | Should -Throw -ExpectedMessage '*invalid --timeout*'
    }
}

Describe 'gim-run confinement' {
    It 'rejects a ..\ escape project' {
        Push-Location (Get-GimTestRepoRoot)
        try {
            { gim-run build --project '..\..\..\Windows\System32' --dry-run } | Should -Throw -ExpectedMessage '*not contained*'
        } finally {
            Pop-Location
        }
    }
    It 'rejects an unexpanded %TEMP% project' {
        { gim-run build --project '%TEMP%\evil.csproj' --dry-run } | Should -Throw -ExpectedMessage '*environment variables*'
    }
    It 'rejects an absolute project outside the repo' {
        { gim-run build --project 'C:\Windows\notepad.exe' --dry-run } | Should -Throw -ExpectedMessage '*not contained*'
    }
    It 'rejects a project escaping through a junction' {
        $repoRoot = Get-GimTestRepoRoot
        $outside = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-run-out-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $outside -Force | Out-Null
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-run-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        try {
            $link = Join-Path $scratch 'jlink'
            New-Item -ItemType Junction -Path $link -Target $outside | Out-Null
            { gim-run build --project (Join-Path $link 'x.csproj') --dry-run } | Should -Throw -ExpectedMessage '*not contained*'
        } finally {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $outside -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'gim-run verbatim trailing args after --' {
    # PowerShell consumes the FIRST '--' before the function sees it, so
    # colliding child tokens must follow a doubled marker '-- --'; everything
    # after the second '--' is routed verbatim into the child argv.
    It 'passes -- -- --json to the child argv (not gim json mode)' {
        $out = @(gim-run run --project TestPlatform/TestPlatform.csproj --dry-run -- -- --json)
        $out | Should -Contain '--json'
        ($out -join "`n") | Should -Not -Match '^\{'
    }
    It 'passes -- -- --dry-run to the child argv (not the verb dry-run switch)' {
        $out = @(gim-run run --project TestPlatform/TestPlatform.csproj --dry-run -- -- --dry-run)
        @($out | Where-Object { $_ -eq '--dry-run' }).Count | Should -Be 1
    }
    It 'passes -- -- --project foo to the child argv (not re-bound as gim project)' {
        $out = @(gim-run run --project TestPlatform/TestPlatform.csproj --dry-run -- -- --project foo)
        $out | Should -Contain 'foo'
        @($out | Where-Object { $_ -eq '--project' }).Count | Should -Be 2
    }
    It 'passes -- -- --timeout and --project-from-var to the child argv verbatim' {
        $out = @(gim-run run --project TestPlatform/TestPlatform.csproj --dry-run -- -- --timeout 5 --project-from-var v)
        $out | Should -Contain '--timeout'
        $out | Should -Contain '--project-from-var'
        $out | Should -Contain 'v'
    }
}

Describe 'gim-run env scrub' {
    It 'lists credential variables to scrub in dry-run output' {
        $oldToken = $env:GH_TOKEN
        $oldNuget = $env:NUGET_API_KEY
        $oldAws = $env:AWS_SECRET_ACCESS_KEY
        $oldAzure = $env:AZURE_SUBSCRIPTION_ID
        $oldBenign = $env:GIM_RUN_TEST_BENIGN
        $env:GH_TOKEN = 'secret'
        $env:NUGET_API_KEY = 'secret'
        $env:AWS_SECRET_ACCESS_KEY = 'secret'
        $env:AZURE_SUBSCRIPTION_ID = 'secret'
        $env:GIM_RUN_TEST_BENIGN = 'benign'
        try {
            $out = @(gim-run build --project TestPlatform/TestPlatform.csproj --dry-run)
            ($out -join "`n") | Should -Match 'GH_TOKEN'
            ($out -join "`n") | Should -Match 'NUGET_API_KEY'
            ($out -join "`n") | Should -Match 'AWS_SECRET_ACCESS_KEY'
            ($out -join "`n") | Should -Match 'AZURE_SUBSCRIPTION_ID'
            ($out -join "`n") | Should -Not -Match 'GIM_RUN_TEST_BENIGN'
        } finally {
            if ($null -eq $oldToken) { Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue } else { $env:GH_TOKEN = $oldToken }
            if ($null -eq $oldNuget) { Remove-Item Env:NUGET_API_KEY -ErrorAction SilentlyContinue } else { $env:NUGET_API_KEY = $oldNuget }
            if ($null -eq $oldAws) { Remove-Item Env:AWS_SECRET_ACCESS_KEY -ErrorAction SilentlyContinue } else { $env:AWS_SECRET_ACCESS_KEY = $oldAws }
            if ($null -eq $oldAzure) { Remove-Item Env:AZURE_SUBSCRIPTION_ID -ErrorAction SilentlyContinue } else { $env:AZURE_SUBSCRIPTION_ID = $oldAzure }
            if ($null -eq $oldBenign) { Remove-Item Env:GIM_RUN_TEST_BENIGN -ErrorAction SilentlyContinue } else { $env:GIM_RUN_TEST_BENIGN = $oldBenign }
        }
    }
    InModuleScope GIMTools {
        It 'scrubs credential variables from the real child environment' {
            $oldToken = $env:GH_TOKEN
            $env:GH_TOKEN = 'secret'
            try {
                $res = Invoke-GimExec -Argv @('pwsh', '-NoProfile', '-Command', 'if ($env:GH_TOKEN) { exit 7 } else { exit 0 }') -Verb 'run' -Project '' -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 30
                $res.ExitCode | Should -Be 0
            } finally {
                if ($null -eq $oldToken) { Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue } else { $env:GH_TOKEN = $oldToken }
            }
        }
    }
}

Describe 'gim-run real execution via shared launcher (module-internal)' {
    InModuleScope GIMTools {
        It 'streams output, propagates the exit code, and appends an audit line' {
            $audit = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-audit-' + [guid]::NewGuid().ToString('N') + '.log')
            try {
                $res = Invoke-GimExec -Argv @('pwsh', '-NoProfile', '-Command', 'Write-Output hello; Write-Error warn; exit 3') -Verb 'run' -Project 'TestPlatform/TestPlatform.csproj' -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 30 -AuditPath $audit
                $res.TimedOut | Should -Be $false
                $res.ExitCode | Should -Be 3
                $res.Stdout | Should -Contain 'hello'
                ($res.Stderr -join "`n") | Should -Match 'warn'
                (Test-Path -LiteralPath $audit) | Should -Be $true
                $lines = @(Get-Content -LiteralPath $audit)
                $lines.Count | Should -Be 1
                $lines[0] | Should -Match ' run '
                $lines[0] | Should -Match 'TestPlatform/TestPlatform.csproj'
                $lines[0] | Should -Match ' 3$'
            } finally {
                Remove-Item -LiteralPath $audit -Force -ErrorAction SilentlyContinue
            }
        }
        It 'streams child stdout to the console in stream mode without polluting the result' {
            $out = @(Invoke-GimExec -Argv @('pwsh', '-NoProfile', '-Command', 'Write-Output hello; Write-Output world; exit 0') -Verb 'run' -Project '' -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 30 -Stream)
            # Only the structured result is emitted to the pipeline - streamed
            # lines go to the console and must NOT be captured into the array
            # (previously Write-Output captured them, so text-mode gim-run
            # printed nothing).
            $out.Count | Should -Be 1
            $out[0].ExitCode | Should -Be 0
            # ...and the streamed lines are still captured for the verb result.
            $out[0].Stdout | Should -Contain 'hello'
            $out[0].Stdout | Should -Contain 'world'
        }
        It 'does not stall on a grandchild holding the stdout pipe after exit' {
            $cmd = '$psi = New-Object System.Diagnostics.ProcessStartInfo; $psi.FileName = ''pwsh''; $psi.Arguments = ''-NoProfile -Command Start-Sleep -Seconds 8''; $psi.UseShellExecute = $false; $p = [System.Diagnostics.Process]::Start($psi); $p.Dispose(); exit 0'
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $res = Invoke-GimExec -Argv @('pwsh', '-NoProfile', '-Command', $cmd) -Verb 'run' -Project '' -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 30
            $sw.Stop()
            $res.ExitCode | Should -Be 0
            # Drain grace is 3s; waiting for the 8s grandchild would trip this.
            $sw.Elapsed.TotalSeconds | Should -BeLessThan 6.5
        }
        It 'kills the process tree on timeout with exit code 124' {
            $audit = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-audit-' + [guid]::NewGuid().ToString('N') + '.log')
            try {
                $res = Invoke-GimExec -Argv @('pwsh', '-NoProfile', '-Command', 'Start-Sleep -Seconds 30') -Verb 'run' -Project '' -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 1 -AuditPath $audit
                $res.TimedOut | Should -Be $true
                $res.ExitCode | Should -Be 124
                @(Get-Content -LiteralPath $audit)[0] | Should -Match ' 124$'
            } finally {
                Remove-Item -LiteralPath $audit -Force -ErrorAction SilentlyContinue
            }
        }
        It 'honors GIM_RUN_AUDIT for the default audit path' {
            $audit = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-audit-' + [guid]::NewGuid().ToString('N') + '.log')
            $old = $env:GIM_RUN_AUDIT
            $env:GIM_RUN_AUDIT = $audit
            try {
                (Get-GimRunAuditPath) | Should -Be $audit
                $res = Invoke-GimExec -Argv @('pwsh', '-NoProfile', '-Command', 'exit 0') -Verb 'probe' -Project '' -WorkingDirectory (Get-GimRepoRoot) -TimeoutSeconds 30
                $res.ExitCode | Should -Be 0
                (Test-Path -LiteralPath $audit) | Should -Be $true
            } finally {
                if ($null -eq $old) { Remove-Item Env:GIM_RUN_AUDIT -ErrorAction SilentlyContinue } else { $env:GIM_RUN_AUDIT = $old }
                Remove-Item -LiteralPath $audit -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Describe 'gim-probe argv dry-run' {
    It 'builds dotnet run argv with --urls on a random local port' {
        $out = @(gim-probe --project TestPlatform/TestPlatform.csproj --route /api/health --dry-run)
        $out[0] | Should -Be 'dotnet'
        $out[1] | Should -Be 'run'
        $out | Should -Contain '--project'
        $out | Should -Contain '--urls'
        ($out -join "`n") | Should -Match 'http://127\.0\.0\.1:\d+'
    }
    It 'honors --method and --body-file' {
        $repoRoot = Get-GimTestRepoRoot
        $scratch = Join-Path $repoRoot ('.agent-scratch\gim-probe-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        try {
            $bf = Join-Path $scratch 'body.json'
            [System.IO.File]::WriteAllText($bf, '{"a":1}', (New-Object System.Text.UTF8Encoding($false)))
            $env = gim-probe --project TestPlatform/TestPlatform.csproj --route /api --method POST --body-file $bf --dry-run --json | ConvertFrom-Json
            $env.dry_run | Should -Be $true
            $env.method | Should -Be 'POST'
            @($env.argv)[0] | Should -Be 'dotnet'
        } finally {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    It 'requires --route and --project, and rejects invalid --method' {
        { gim-probe --project TestPlatform/TestPlatform.csproj --dry-run } | Should -Throw -ExpectedMessage '*route*'
        { gim-probe --route /api/health --dry-run } | Should -Throw -ExpectedMessage '*project*'
        { gim-probe --project TestPlatform/TestPlatform.csproj --route / --method FOO --dry-run } | Should -Throw -ExpectedMessage '*invalid --method*'
    }
    It 'rejects a --body-file outside the repo' {
        { gim-probe --project TestPlatform/TestPlatform.csproj --route / --body-file 'C:\Windows\notepad.exe' --dry-run } | Should -Throw -ExpectedMessage '*not contained*'
    }
    It 'rejects a project escaping the repo' {
        { gim-probe --project '..\..\..\Windows\System32' --route / --dry-run } | Should -Throw -ExpectedMessage '*not contained*'
    }
    It 'validates --timeout range' {
        { gim-probe --project TestPlatform/TestPlatform.csproj --route / --timeout 1801 --dry-run } | Should -Throw -ExpectedMessage '*cap*'
    }
}
