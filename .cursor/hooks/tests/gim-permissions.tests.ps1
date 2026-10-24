# gim-permissions.tests.ps1 — Pester 6 battery for the Cursor permission hook
# (plan 0004 C1). Simulated stdin JSON payloads -> assert decision JSON.
#
# Run: Invoke-Pester .cursor/hooks/tests/gim-permissions.tests.ps1
#
# The hook script is dot-sourced (defines functions only; the stdin entry
# point is guarded by InvocationName), then decisions are evaluated in-process
# with the central command-tree lib. Stdin round-trips use
# [Console]::SetIn with a StringReader (restored after each test).

BeforeAll {
    . "$PSScriptRoot\..\gim-permissions.ps1"

    $script:OriginalIn = [Console]::In

    function Get-DecisionJsonFor {
        param([string]$Command, [string]$ToolName = 'Shell')
        $d = Get-GimDecision -Command $Command -ToolName $ToolName -FilePath ''
        return Get-GimDecisionJson -Decision $d
    }

    function Get-DecisionRuleFor {
        param([string]$Command, [string]$ToolName = 'Shell')
        $d = Get-GimDecision -Command $Command -ToolName $ToolName -FilePath ''
        if ($null -eq $d) { return $null }
        return $d.rule
    }

    function Invoke-GimHookWithStdin {
        param([string]$Json)
        $reader = [System.IO.StringReader]::new($Json)
        [Console]::SetIn($reader)
        try {
            return Invoke-GimHook
        } finally {
            $reader.Dispose()
            [Console]::SetIn($script:OriginalIn)
        }
    }
}

AfterAll {
    [Console]::SetIn($script:OriginalIn)
}

Describe 'five block-accepting cmdlets — deny with replacement message' {
    $cases = @(
        @{ cmd = 'Get-Content foo.txt | ForEach-Object { $_.Length }'; rule = 'cmdlet.foreach-object'; msg = 'gim-report' }
        @{ cmd = 'Get-ChildItem | Where-Object { $_.Name }';            rule = 'cmdlet.where-object';   msg = 'gim-where' }
        @{ cmd = 'Get-ChildItem | Group-Object Name';                   rule = 'cmdlet.group-object';    msg = 'gim-count' }
        @{ cmd = 'Get-ChildItem | Sort-Object Name';                    rule = 'cmdlet.sort-object';     msg = 'gim-report --sort' }
        @{ cmd = 'Get-ChildItem | Select-Object -First 5';              rule = 'cmdlet.select-object';   msg = 'gim-head' }
        @{ cmd = 'Get-ChildItem | Select-Object -ExpandProperty Name';  rule = 'cmdlet.select-object';   msg = 'gim-json' }
    )
    foreach ($case in $cases) {
        It "denies: $($case.cmd)" -TestCases @($case) {
            param($cmd, $rule, $msg)
            $d = Get-GimDecision -Command $cmd -ToolName 'Shell' -FilePath ''
            $d | Should -Not -BeNullOrEmpty
            $d.rule | Should -Be $rule
            $d.reason | Should -Match 'script-block built-in'
            $d.alternative | Should -Match $msg
            $json = Get-GimDecisionJson -Decision $d
            ($json | ConvertFrom-Json).permission | Should -BeExactly 'deny'
        }
    }
}

Describe 'execution escapes — deny, no sanctioned replacement' {
    $cases = @(
        @{ cmd = 'Invoke-Expression "foo"';  rule = 'executionEscape.invoke-expression' }
        @{ cmd = 'iex "foo"';                 rule = 'executionEscape.iex' }
        @{ cmd = 'Add-Type -TypeDefinition "x"'; rule = 'executionEscape.add-type' }
        @{ cmd = 'Start-Process notepad';     rule = 'executionEscape.start-process' }
    )
    foreach ($case in $cases) {
        It "denies: $($case.cmd)" -TestCases @($case) {
            param($cmd, $rule)
            (Get-DecisionRuleFor $cmd) | Should -Be $rule
        }
    }
}

Describe 'substitution / static / reflection denies' {
    It 'denies $() command substitution (unquoted — quoted literals excluded by the central lib)' {
        $d = Get-GimDecision -Command 'Write-Output $(Get-Date)' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Be 'substitution'
        $d.reason | Should -Match 'command substitution'
        $d.alternative | Should -Match 'from-var'
    }
    It 'denies :: static member access' {
        $d = Get-GimDecision -Command "[System.IO.File]::ReadAllText('x')" -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Be 'staticCall'
        $d.reason | Should -Match ':: static member access'
    }
    It 'denies $ExecutionContext variable reference (memberCalls envelope)' {
        $d = Get-GimDecision -Command '$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(".")' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Match '^reflection\.'
        $d.reason | Should -Match 'reflection access'
    }
    It 'denies .GetType() member access (memberCalls envelope)' {
        $d = Get-GimDecision -Command '$x.GetType().FullName' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Match '^reflection\.'
    }
    It 'denies bare $x.Invoke(...) instance expression (memberCalls envelope)' {
        $d = Get-GimDecision -Command '$x.Invoke("hi")' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Match '^reflection\.'
    }
}

Describe 'secret-file paths — deny in bash text and tool payloads' {
    $bashCases = @(
        'Get-Content appsettings.development.secret.json'
        'Get-Content .env'
        'Copy-Item id_rsa backup'
        'Copy-Item cert.pfx backup'
        'Write-Output D:\Work\secrets\.env'
    )
    foreach ($cmd in $bashCases) {
        It "denies bash text: $cmd" -TestCases @{ cmd = $cmd } {
            param($cmd)
            $d = Get-GimDecision -Command $cmd -ToolName 'Shell' -FilePath ''
            $d | Should -Not -BeNullOrEmpty
            $d.rule | Should -Be 'secretFile'
            $d.alternative | Should -Match 'never read or write'
        }
    }

    It 'denies git add -- <secret> (write-side not exempt)' {
        $d = Get-GimDecision -Command 'git add -- .env' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Be 'secretFile'
    }

    It 'skips git diff -- <secret> pathspec (trusted-read exemption)' {
        (Get-DecisionRuleFor 'git diff -- .env A.cs') | Should -BeNullOrEmpty
    }

    It 'denies Edit tool payload secret file path' {
        $d = Get-GimDecision -Command '' -ToolName 'Edit' -FilePath 'C:\repo\.env'
        $d | Should -Not -BeNullOrEmpty
        $d.rule | Should -Be 'secretFile'
    }

    It 'denies Write tool payload secret file path' {
        $d = Get-GimDecision -Command '' -ToolName 'Write' -FilePath 'appsettings.prod.secret.json'
        $d.rule | Should -Be 'secretFile'
    }

    It 'passes Edit tool payload on normal path' {
        (Get-GimDecision -Command '' -ToolName 'Edit' -FilePath 'C:\repo\src\File.cs') | Should -BeNullOrEmpty
    }
}

Describe 'destructive git variants — deny' {
    $cases = @(
        @{ cmd = 'git branch -D foo';    rule = 'gitDestructive.branch -D' }
        @{ cmd = 'git branch -d foo';    rule = 'gitDestructive.branch -d' }
        @{ cmd = 'git add -A';           rule = 'gitDestructive.add -A' }
        @{ cmd = 'git add --all .';      rule = 'gitDestructive.add -A' }
        @{ cmd = 'git add -f x';         rule = 'gitDestructive.add -f' }
        @{ cmd = 'git add --force x';    rule = 'gitDestructive.add -f' }
        @{ cmd = 'git stash drop';       rule = 'gitDestructive.stash drop' }
        @{ cmd = 'git stash apply';      rule = 'gitDestructive.stash apply' }
        @{ cmd = 'git stash clear';      rule = 'gitDestructive.stash clear' }
    )
    foreach ($case in $cases) {
        It "denies: $($case.cmd)" -TestCases @($case) {
            param($cmd, $rule)
            $d = Get-GimDecision -Command $cmd -ToolName 'Shell' -FilePath ''
            $d | Should -Not -BeNullOrEmpty
            $d.rule | Should -Be $rule
            $d.alternative | Should -Match 'two-turn pattern'
        }
    }
}

Describe 'force-push variants only — plain push passes' {
    It 'denies git push --force' {
        (Get-DecisionRuleFor 'git push --force origin main') | Should -Be 'gitForcePush'
    }
    It 'denies git push -f' {
        (Get-DecisionRuleFor 'git push -f origin main') | Should -Be 'gitForcePush'
    }
    It 'denies git push --force-with-lease' {
        (Get-DecisionRuleFor 'git push --force-with-lease origin main') | Should -Be 'gitForcePush'
    }
    It 'denies force push inside a chain (deny beats allowlist: git status is allowlisted)' {
        (Get-DecisionRuleFor 'git status; git push --force origin main') | Should -Be 'gitForcePush'
    }
    It 'passes plain git push' {
        (Get-DecisionRuleFor 'git push origin main') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'git push') | Should -BeNullOrEmpty
    }
    It 'passes non-push git with -f (git grep -f, git branch -f)' {
        (Get-DecisionRuleFor 'git grep -f pattern') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'git branch -f foo bar') | Should -BeNullOrEmpty
    }
}

Describe 'git -C — case-sensitive' {
    It 'denies git -C (uppercase C)' {
        $d = Get-GimDecision -Command 'git -C d:\Work\GIM\GIMmos status -sb' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Be 'gitC'
        $d.alternative | Should -Match 'working_directory'
    }
    It 'passes git -c (lowercase c, config subcommand)' {
        (Get-DecisionRuleFor 'git -c core.autocrlf=true status') | Should -BeNullOrEmpty
    }
}

Describe 'dotnet execution verbs — deny, gim-run is the channel' {
    $cases = @(
        'dotnet run'
        'dotnet nuget push x.nupkg'
        'dotnet nuget add source https://x'
        'dotnet tool install -g dotnet-ef'
        'dotnet new install Template'
        'dotnet workload install maui'
        'dotnet exec foo.dll'
        'dotnet foo.dll'
        'dotnet fsi script.fsx'
        'dotnet script x'
        'dotnet msbuild -t:Clean'
        'dotnet publish -o C:\out'
        'dotnet publish --output C:\out'
    )
    foreach ($cmd in $cases) {
        It "denies: $cmd" -TestCases @{ cmd = $cmd } {
            param($cmd)
            $d = Get-GimDecision -Command $cmd -ToolName 'Shell' -FilePath ''
            $d | Should -Not -BeNullOrEmpty
            $d.rule | Should -Match '^dotnetExec\.'
            $d.alternative | Should -Match 'gim-run'
        }
    }
    It 'passes dotnet build (allowed read/build verb)' {
        (Get-DecisionRuleFor 'dotnet build GIMtransfer/GIMtransfer.csproj -c Debug -p:Platform=x64') | Should -BeNullOrEmpty
    }
}

Describe 'allowlisted commands — deny beats allowlist, strings stay inert' {
    It 'denies a block cmdlet even inside an otherwise-allowlisted command' {
        $d = Get-GimDecision -Command 'Write-Output "x" | ForEach-Object { $_.Length }' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Be 'cmdlet.foreach-object'
    }
    It 'passes strings containing cmdlet names (inert)' {
        (Get-DecisionRuleFor 'Write-Output "ForEach-Object"') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'Write-Output "git -C"') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'Write-Output "git push --force"') | Should -BeNullOrEmpty
    }
    It 'passes allowlisted git reads (no git -- quirk rule on Cursor)' {
        (Get-DecisionRuleFor 'git status') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'git log -1 --oneline') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'git diff -- A.cs B.cs') | Should -BeNullOrEmpty
    }
    It 'passes gim-* verbs and Pester tier' {
        (Get-DecisionRuleFor 'gim-list --glob "**/*.cs"') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'Get-Content foo.cs | gim-grep --stdin --pattern "x"') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'Import-Module Pester -Force') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'Invoke-Pester .cursor/hooks/tests/gim-permissions.tests.ps1') | Should -BeNullOrEmpty
    }
}

Describe 'single-quoted strings stay inert (string-kind regression)' {
    It "passes single-quoted `$ExecutionContext" {
        (Get-DecisionRuleFor 'Write-Output ''$ExecutionContext''') | Should -BeNullOrEmpty
    }
    It 'passes single-quoted GetType' {
        (Get-DecisionRuleFor 'Write-Output ''GetType''') | Should -BeNullOrEmpty
    }
    It 'passes single-quoted ForEach-Object' {
        (Get-DecisionRuleFor 'Write-Output ''ForEach-Object''') | Should -BeNullOrEmpty
    }
    It 'passes a single-quoted here-string containing ::' {
        $here = "@'`n::`n'@"
        (Get-DecisionRuleFor ('Write-Output ' + $here)) | Should -BeNullOrEmpty
    }
    It 'passes double-quoted expandable string containing $ExecutionContext' {
        (Get-DecisionRuleFor 'Write-Output "$ExecutionContext"') | Should -BeNullOrEmpty
    }
}

Describe 'null/empty arg arrays — defensive guards (no catch-all reliance)' {
    It 'passes bare git (empty args)' {
        (Get-DecisionRuleFor 'git') | Should -BeNullOrEmpty
    }
    It 'passes bare dotnet (empty args)' {
        (Get-DecisionRuleFor 'dotnet') | Should -BeNullOrEmpty
    }
    It 'Test-GimGitForcePush handles null args array' {
        $c = [pscustomobject]@{ name = 'git'; args = $null }
        (Test-GimGitForcePush -Cmd $c) | Should -BeFalse
    }
    It 'Test-GimGitForcePush handles args with a null element' {
        $c = [pscustomobject]@{ name = 'git'; args = @($null) }
        (Test-GimGitForcePush -Cmd $c) | Should -BeFalse
    }
    It 'Get-GimDotnetExecHit handles null args array' {
        $c = [pscustomobject]@{ name = 'dotnet'; args = $null }
        (Get-GimDotnetExecHit -Cmd $c) | Should -BeNullOrEmpty
    }
    It 'Get-GimDotnetExecHit handles args with a null element' {
        $c = [pscustomobject]@{ name = 'dotnet'; args = @($null) }
        (Get-GimDotnetExecHit -Cmd $c) | Should -BeNullOrEmpty
    }
}

Describe 'deny message contract — user-locked framing' {
    It 'bash deny message carries the exact framing' {
        $json = Get-DecisionJsonFor 'git push --force origin main'
        $obj = $json | ConvertFrom-Json
        $obj.agent_message | Should -BeExactly $obj.user_message
        $obj.agent_message | Should -Match '^Your command was not run: command ''git push --force origin main'' was denied by cursor permission hook\.'
        $obj.agent_message | Should -Match 'Reason: git force-push variant\.'
        $obj.agent_message | Should -Match 'Please try:'
        $obj.agent_message | Should -Match 'This is NOT the command''s output — this is the permission hook''s error message\.$'
        $obj.agent_message | Should -Match 'version-controller'
    }
    It 'tool-path deny message uses <tool> <path> as the command context' {
        $d = Get-GimDecision -Command '' -ToolName 'Edit' -FilePath 'C:\repo\.env'
        $json = Get-GimDecisionJson -Decision $d
        $obj = $json | ConvertFrom-Json
        $obj.agent_message | Should -Match 'command .* was denied by cursor permission hook'
        $obj.agent_message | Should -Match 'Edit C:\\repo\\.env'
        $obj.agent_message | Should -Match 'Reason: secret-file path\.'
    }
    It 'long command text is truncated to ~120 chars with ellipsis' {
        $long = 'Write-Output "' + ('A' * 150) + '" | Where-Object { $_.Length }'
        $d = Get-GimDecision -Command $long -ToolName 'Shell' -FilePath ''
        $json = Get-GimDecisionJson -Decision $d
        $obj = $json | ConvertFrom-Json
        $obj.agent_message | Should -Match '…'
        $obj.agent_message | Should -Not -Match ('A' * 150)
    }
    It 'deny message explicitly says this is NOT the command output' {
        $json = Get-DecisionJsonFor 'git branch -D old'
        $obj = $json | ConvertFrom-Json
        $obj.agent_message | Should -Match 'This is NOT the command''s output'
        $obj.agent_message | Should -Match 'Reason: destructive git variant'
    }
}

Describe 'pwsh / powershell command-position deny (user-locked)' {
    It 'denies pwsh at command position' {
        $d = Get-GimDecision -Command 'pwsh -NoProfile -File x.ps1' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Be 'pwshInvocation'
        $d.reason | Should -Match 'pwsh/powershell invocation'
        $d.alternative | Should -Match 'run scripts in-shell'
    }
    It 'denies powershell at command position' {
        (Get-DecisionRuleFor 'powershell -Command "x"') | Should -Be 'pwshInvocation'
    }
    It 'denies & pwsh call-operator invocation' {
        (Get-DecisionRuleFor '& pwsh -Command "x"') | Should -Be 'pwshInvocation'
    }
    It 'passes Get-Command pwsh (not a pwsh invocation)' {
        (Get-DecisionRuleFor 'Get-Command pwsh') | Should -BeNullOrEmpty
    }
    It 'passes pwsh/powershell inside strings (inert)' {
        (Get-DecisionRuleFor 'Write-Output ''pwsh''') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'Write-Output ''powershell''') | Should -BeNullOrEmpty
    }
    It 'passes pwsh as an argument (not command position)' {
        (Get-DecisionRuleFor 'git commit -m pwsh') | Should -BeNullOrEmpty
    }
}

Describe 'bare reflection markers — member-access shape only (critic Medium)' {
    It 'passes bareword args containing member names (inert)' {
        (Get-DecisionRuleFor 'git commit -m GetMethod') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'git commit -m GetField') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'git commit -m GetType') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'gim-grep --pattern GetMethod') | Should -BeNullOrEmpty
        (Get-DecisionRuleFor 'Get-Content src/GetField.cs') | Should -BeNullOrEmpty
    }
    It 'denies member access via the memberCalls envelope' {
        $d = Get-GimDecision -Command '$x.GetMethod()' -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Match '^reflection\.'
        $d.rule | Should -Be 'reflection.GetMethod'
        (Get-DecisionRuleFor '$x.GetField()') | Should -Be 'reflection.GetField'
    }
    It 'denies static member access via :: / staticCalls' {
        $d = Get-GimDecision -Command "[Type]::GetMethod('x')" -ToolName 'Shell' -FilePath ''
        $d.rule | Should -Be 'staticCall'
    }
}

Describe 'payload shapes — preToolUse / beforeShellExecution / post events' {
    It 'extracts command from preToolUse Shell tool_input' {
        $p = @{ hook_event_name = 'preToolUse'; tool_name = 'Shell'; tool_use_id = '1'; tool_input = @{ command = 'git status'; cwd = 'D:\repo'; timeout = 120 } }
        (Get-GimCommandText -Payload $p) | Should -Be 'git status'
    }
    It 'extracts command from beforeShellExecution payload.command' {
        $p = @{ hook_event_name = 'beforeShellExecution'; command = 'git -C d:\x status'; cwd = 'D:\repo'; sandbox = $false }
        (Get-GimCommandText -Payload $p) | Should -Be 'git -C d:\x status'
    }
    It 'decides on preToolUse payload (deny)' {
        $json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
        $out = Invoke-GimHookWithStdin $json
        ($out | ConvertFrom-Json).permission | Should -BeExactly 'deny'
        ($out | ConvertFrom-Json).agent_message | Should -Match 'force push'
    }
    It 'decides on beforeShellExecution payload (deny)' {
        $json = '{"hook_event_name":"beforeShellExecution","command":"git -C d:\\x status"}'
        $out = Invoke-GimHookWithStdin $json
        ($out | ConvertFrom-Json).permission | Should -BeExactly 'deny'
    }
    It 'passes on postToolUseFailure payload (post events silent)' {
        $p = @{ hook_event_name = 'postToolUseFailure'; tool_name = 'Shell'; failure_type = 'permission_denied'; tool_input = @{ command = 'ForEach-Object' } }
        $out = Invoke-GimHookWithStdin ($p | ConvertTo-Json -Compress -Depth 5)
        $out | Should -BeExactly '{}'
    }
    It 'denies Edit tool secret path from a full preToolUse payload' {
        $p = @{ hook_event_name = 'preToolUse'; tool_name = 'Edit'; tool_input = @{ file_path = 'C:\repo\.env'; old_string = 'x' } }
        $out = Invoke-GimHookWithStdin ($p | ConvertTo-Json -Compress -Depth 5)
        ($out | ConvertFrom-Json).permission | Should -BeExactly 'deny'
        ($out | ConvertFrom-Json).agent_message | Should -Match 'secret-file path'
    }
    It 'decides on a chained full-string payload as one event' {
        $json = '{"hook_event_name":"beforeShellExecution","command":"git status -sb; git log -1 --oneline"}'
        $out = Invoke-GimHookWithStdin $json
        $out | Should -BeExactly '{}'
    }
}

Describe 'malformed / empty input — fail-closed vs fail-open' {
    It 'invalid JSON -> generic deny (fail-closed, non-empty unparseable)' {
        $out = Invoke-GimHookWithStdin 'not json at all'
        $obj = $out | ConvertFrom-Json
        $obj.permission | Should -BeExactly 'deny'
        $obj.agent_message | Should -Match 'parse failed'
    }
    It 'empty stdin -> {}' {
        (Invoke-GimHookWithStdin '') | Should -BeExactly '{}'
    }
    It 'whitespace stdin -> {}' {
        (Invoke-GimHookWithStdin '   ') | Should -BeExactly '{}'
    }
    It 'null JSON -> {}' {
        (Invoke-GimHookWithStdin 'null') | Should -BeExactly '{}'
    }
    It 'empty command text -> {}' {
        (Get-DecisionJsonFor '') | Should -BeExactly '{}'
    }
    It 'missing payload -> {}' {
        (Get-DecisionJsonFor $null) | Should -BeExactly '{}'
    }
}

Describe 'stdin robustness — BOM, whitespace, parse failures (live-spawn parity)' {
    It 'parses payload with a leading UTF-8 BOM' {
        $json = '{"hook_event_name":"beforeShellExecution","command":"git push --force origin main"}'
        $withBom = [string][char]0xFEFF + $json
        $out = Invoke-GimHookWithStdin $withBom
        $obj = $out | ConvertFrom-Json
        $obj.permission | Should -BeExactly 'deny'
        $obj.agent_message | Should -Match 'git force-push variant'
    }
    It 'parses payload with leading whitespace' {
        $json = '   {"hook_event_name":"beforeShellExecution","command":"git push --force origin main"}'
        $out = Invoke-GimHookWithStdin $json
        ($out | ConvertFrom-Json).permission | Should -BeExactly 'deny'
    }
    It 'parses payload with BOM + leading whitespace' {
        $p = @{ hook_event_name = 'preToolUse'; tool_name = 'Shell'; tool_input = @{ command = 'git push --force' } }
        $json = $p | ConvertTo-Json -Compress -Depth 5
        $withBom = [string][char]0xFEFF + "`n  " + $json
        $out = Invoke-GimHookWithStdin $withBom
        ($out | ConvertFrom-Json).permission | Should -BeExactly 'deny'
    }
    It 'empty stdin -> {} (fail-open, parse=fail path)' {
        (Invoke-GimHookWithStdin '') | Should -BeExactly '{}'
    }
    It 'malformed JSON -> generic deny (fail-closed, non-empty unparseable)' {
        $out = Invoke-GimHookWithStdin 'not json at all'
        ($out | ConvertFrom-Json).permission | Should -BeExactly 'deny'
        ($out | ConvertFrom-Json).agent_message | Should -Match 'parse failed'
    }
}

Describe 'fail-closed: non-empty unparseable payload denies (user-locked 2026-08-20)' {
    It 'unparseable JSON payload denies with the generic contract framing' {
        $out = Invoke-GimHookWithStdin '{not json'
        $obj = $out | ConvertFrom-Json
        $obj.permission | Should -BeExactly 'deny'
        $obj.agent_message | Should -BeExactly $obj.user_message
        $obj.agent_message | Should -Match '^Your command was not run: command '''
        $obj.agent_message | Should -Match 'was denied by cursor permission hook\.'
        $obj.agent_message | Should -Match 'could not interpret the permission payload \(parse failed\)'
        $obj.agent_message | Should -Match 'gim-permissions\.log'
        $obj.agent_message | Should -Match 'This is NOT the command''s output — this is the permission hook''s error message\.$'
    }
    It 'mojibake-with-BOM payload denies (the v2 live-regression shape)' {
        $json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
        $mojibake = '´╗┐' + $json
        $out = Invoke-GimHookWithStdin $mojibake
        $obj = $out | ConvertFrom-Json
        $obj.permission | Should -BeExactly 'deny'
        $obj.agent_message | Should -Match 'parse failed'
    }
    It 'truncates the raw payload to ~120 chars in the deny message' {
        $payload = '{' + ('A' * 200)
        $out = Invoke-GimHookWithStdin $payload
        $obj = $out | ConvertFrom-Json
        $obj.agent_message | Should -Match '…'
        $obj.agent_message | Should -Not -Match ('A' * 121)
    }
    It 'empty stdin stays fail-open (unchanged)' {
        (Invoke-GimHookWithStdin '') | Should -BeExactly '{}'
    }
    It 'whitespace stdin stays fail-open (unchanged)' {
        (Invoke-GimHookWithStdin '   ') | Should -BeExactly '{}'
    }
}

Describe 'SetIn StringReader payloads parse without redirected process stdin (critic fix)' {
    It 'Read-GimStdin reads a [Console]::SetIn StringReader payload directly' {
        $original = [Console]::In
        $reader = [System.IO.StringReader]::new('{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}')
        try {
            [Console]::SetIn($reader)
            $raw = Read-GimStdin
            $raw | Should -Not -BeNullOrEmpty
            $raw | Should -Match 'git push --force origin main'
        } finally {
            $reader.Dispose()
            [Console]::SetIn($original)
        }
    }
    It 'full hook round-trip parses a SetIn StringReader payload (deny decision)' {
        $json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
        $out = Invoke-GimHookWithStdin $json
        $obj = $out | ConvertFrom-Json
        $obj.permission | Should -BeExactly 'deny'
        $obj.agent_message | Should -Match 'force push'
    }
}

Describe 'byte-level UTF-8 BOM decode + defensive U+FEFF strip (live regression 2026-08-20)' {
    It 'decodes raw EF BB BF + UTF-8 payload bytes via the StreamReader decode path' {
        $json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
        $bytes = [byte[]](0xEF, 0xBB, 0xBF) + [System.Text.Encoding]::UTF8.GetBytes($json)
        $decoded = Convert-GimUtf8Payload -Bytes $bytes
        $decoded | Should -Not -BeNullOrEmpty
        ($decoded.Length -gt 0 -and $decoded[0] -eq [char]0xFEFF) | Should -BeFalse
        $decoded | Should -Match 'git push --force origin main'
    }
    It 'byte-level BOM payload parses end-to-end (decoded -> SetIn -> hook deny)' {
        $json = '{"hook_event_name":"beforeShellExecution","command":"git push --force origin main"}'
        $bytes = [byte[]](0xEF, 0xBB, 0xBF) + [System.Text.Encoding]::UTF8.GetBytes($json)
        $decoded = Convert-GimUtf8Payload -Bytes $bytes
        $reader = [System.IO.StringReader]::new($decoded)
        try {
            [Console]::SetIn($reader)
            $out = Invoke-GimHook
            $obj = $out | ConvertFrom-Json
            $obj.permission | Should -BeExactly 'deny'
            $obj.agent_message | Should -Match 'force push'
        } finally {
            $reader.Dispose()
            [Console]::SetIn($script:OriginalIn)
        }
    }
    It 'string U+FEFF prefix payload parses via SetIn (defensive strip)' {
        $json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
        $withPrefix = [string][char]0xFEFF + $json
        $out = Invoke-GimHookWithStdin $withPrefix
        ($out | ConvertFrom-Json).permission | Should -BeExactly 'deny'
    }
    It 'decode path must not produce console-codepage mojibake (the v2 regression)' {
        $json = '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"git push --force origin main"}}'
        $bytes = [byte[]](0xEF, 0xBB, 0xBF) + [System.Text.Encoding]::UTF8.GetBytes($json)
        $decoded = Convert-GimUtf8Payload -Bytes $bytes
        $decoded | Should -Not -Match '´╗┐'
        $decoded | Should -Match '^{'
    }
}

Describe 'output contract — JSON only' {
    It 'pass decision is exactly {}' {
        (Get-DecisionJsonFor 'git status') | Should -BeExactly '{}'
    }
    It 'deny decision JSON carries permission/agent_message/user_message' {
        $d = Get-GimDecision -Command 'git push --force' -ToolName 'Shell' -FilePath ''
        $json = Get-GimDecisionJson -Decision $d
        $obj = $json | ConvertFrom-Json
        $obj.permission | Should -BeExactly 'deny'
        $obj.agent_message | Should -BeExactly $obj.user_message
        $obj.user_message | Should -Match 'force push'
    }
}
