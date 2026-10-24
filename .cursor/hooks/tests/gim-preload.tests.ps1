# gim-preload.tests.ps1 — Pester battery for the Cursor preload hook (ADR 0009).
#
# Run: Invoke-Pester -Path .cursor/hooks/tests/gim-preload.tests.ps1 -PassThru
#
# The hook is dot-sourced (functions only; the stdin entry point is guarded by
# -NoMain and the dot-source invocation name), then handler functions and the
# in-process entry function are called directly. No pwsh spawn, no live Cursor
# turn: the live subagent_type probe belongs to the operator (see
# .cursor/hooks/AGENTS.md and mark it pending until it passes).
#
# Behavior tests use a temp fixture config/skill tree, so they never assert the
# live `.agents/agent-preload.json` mapping contents (exact skill lists). The
# only live-file test is structural: the shared config parses, has the eight
# fleet agents, has no linter entry, and uses harness-neutral wording.

BeforeAll {
    . "$PSScriptRoot\..\gim-preload.ps1" -NoMain

    $script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
    $script:OriginalIn = [Console]::In

    function Get-HookJsonFor {
        param([hashtable]$Payload, [string]$Root = $script:FixtureRoot)
        $raw = $Payload | ConvertTo-Json -Depth 20 -Compress
        return (Invoke-GimPreloadHook -Raw $raw -Root $Root)
    }

    function Invoke-MainWithStdin {
        param([string]$Json, [string]$Root = $script:FixtureRoot)
        $reader = [System.IO.StringReader]::new($Json)
        [Console]::SetIn($reader)
        try {
            return (Invoke-GimPreloadMain -Root $Root)
        } finally {
            $reader.Dispose()
            [Console]::SetIn($script:OriginalIn)
        }
    }

    # Temp fixture root: two agents, one real skill + one real file each, plus
    # deliberately missing entries.
    $script:FixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-preload-fixture-' + [guid]::NewGuid().ToString('N'))
    $skillsRoot = Join-Path $script:FixtureRoot '.agents\skills'
    foreach ($name in @('fixture-orchestrator-skill', 'fixture-explorer-skill')) {
        $skillDir = Join-Path $skillsRoot $name
        [void][System.IO.Directory]::CreateDirectory($skillDir)
        [System.IO.File]::WriteAllText(
            (Join-Path $skillDir 'SKILL.md'),
            "---`nname: $name`ndescription: fixture skill`n---`n# $name`n`nbody of $name`n"
        )
    }
    [void][System.IO.Directory]::CreateDirectory((Join-Path $script:FixtureRoot 'fixture'))
    [System.IO.File]::WriteAllText((Join-Path $script:FixtureRoot 'fixture\prefs.md'), 'fixture prefs body')

    $fixtureConfig = @{
        enabled = $true
        header  = 'fixture header'
        agents  = @{
            'gim-orchestrator' = @{
                skills = @('fixture-orchestrator-skill')
                files  = @('fixture/prefs.md')
            }
            'gim-explorer' = @{
                skills = @('fixture-explorer-skill', 'missing-skill')
                files  = @('fixture/prefs.md', 'no-such-file.md')
            }
        }
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $script:FixtureRoot '.agents\agent-preload.json'),
        ($fixtureConfig | ConvertTo-Json -Depth 20)
    )
}

AfterAll {
    [Console]::SetIn($script:OriginalIn)
    if (-not [string]::IsNullOrEmpty($script:FixtureRoot)) {
        Remove-Item -LiteralPath $script:FixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Describe 'preToolUse pass-through — every no-op returns {} (never allow)' {
    It 'passes a non-Task tool without a permission field' {
        (Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Read'
            tool_input      = @{ path = 'README.md'; prompt = 'not a task' }
        }) | Should -BeExactly '{}'
    }

    It 'passes a Task with no subagent type' {
        (Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{ prompt = 'no agent named' }
        }) | Should -BeExactly '{}'
    }

    It 'passes an unknown subagent type' {
        (Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{ subagent_type = 'gim-not-a-real-agent'; prompt = 'keep me' }
        }) | Should -BeExactly '{}'
    }

    It 'passes a Task with a non-string prompt' {
        (Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{ subagent_type = 'gim-explorer'; prompt = 42 }
        }) | Should -BeExactly '{}'
    }

    It 'passes a Task with an empty prompt' {
        (Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{ subagent_type = 'gim-explorer'; prompt = '' }
        }) | Should -BeExactly '{}'
    }

    It 'is idempotent when the prompt already carries the marker' {
        (Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = '<gim-preload agent="gim-explorer" files="1">x</gim-preload>' + "`n`nhello"
            }
        }) | Should -BeExactly '{}'
    }
}

Describe 'marker idempotence — complete block only' {
    It 'injects when the prompt only mentions the literal opening tag' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = 'Update the hook so a prompt containing <gim-preload is still injected.'
            }
        }
        $result = $raw | ConvertFrom-Json
        $result.permission | Should -BeExactly 'allow'
        $result.updated_input | Should -Not -BeNullOrEmpty
        $prompt = [string]$result.updated_input.prompt
        $prompt | Should -Match '<gim-preload agent="gim-explorer" files="2">'
        $prompt | Should -Match '</gim-preload>'
        $prompt.TrimEnd().EndsWith('is still injected.') | Should -BeTrue
    }

    It 'injects when the prompt mentions the literal tag with attributes but no closing tag' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = 'Explain `<gim-preload agent="x" files="0">` without a closing tag.'
            }
        }
        $result = $raw | ConvertFrom-Json
        $result.permission | Should -BeExactly 'allow'
        $result.updated_input | Should -Not -BeNullOrEmpty
        ([string]$result.updated_input.prompt) | Should -Match '<gim-preload agent="gim-explorer" files="2">'
    }

    It 'passes through when the prompt already contains a complete block' {
        (Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = '<gim-preload agent="gim-explorer" files="2">' + "`nbody`n" +
                                '</gim-preload>' + "`n`nhello"
            }
        }) | Should -BeExactly '{}'
    }

    It 'injects when a complete block is quoted later in the prompt' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = 'Example of an injected prompt:' + "`n`n" +
                                '<gim-preload agent="x" files="1">quoted body</gim-preload>' + "`n`n" +
                                'Now do the real task.'
            }
        }
        $result = $raw | ConvertFrom-Json
        $result.permission | Should -BeExactly 'allow'
        $result.updated_input | Should -Not -BeNullOrEmpty
        $prompt = [string]$result.updated_input.prompt
        $prompt | Should -Match '<gim-preload agent="gim-explorer" files="2">'
        $prompt | Should -Match 'quoted body'
        $prompt.TrimEnd().EndsWith('Now do the real task.') | Should -BeTrue
    }
}

Describe 'preToolUse injection — fixture mapping' {
    It 'returns allow + updated_input only when it injects' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = 'UNIQUE-PROMPT-TAIL'
                description   = 'probe'
            }
        }
        $result = $raw | ConvertFrom-Json
        $result.permission | Should -BeExactly 'allow'
        $result.updated_input | Should -Not -BeNullOrEmpty

        $prompt = [string]$result.updated_input.prompt
        $prompt | Should -Match '<gim-preload agent="gim-explorer" files="2">'
        $prompt | Should -Match 'fixture header'
        $prompt | Should -Match 'body of fixture-explorer-skill'
        $prompt | Should -Match 'fixture prefs body'
        $prompt | Should -Not -Match 'missing-skill'
        $prompt | Should -Not -Match 'no-such-file'
        $prompt.TrimEnd().EndsWith('UNIQUE-PROMPT-TAIL') | Should -BeTrue
    }

    It 'escapes non-ASCII in the raw JSON' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = ('cafe' + [char]0x00E9)
            }
        }
        foreach ($ch in $raw.ToCharArray()) { ([int]$ch) -le 127 | Should -BeTrue }
        $raw | Should -Match '\\u00e9'
        ([string]($raw | ConvertFrom-Json).updated_input.prompt) | Should -Match ([char]0x00E9)
    }

    It 'preserves every other tool_input field' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{
                subagent_type = 'gim-explorer'
                prompt        = 'original prompt'
                description   = 'field to keep'
                custom_flag   = $true
            }
        }
        $result = $raw | ConvertFrom-Json
        $result.updated_input.subagent_type | Should -BeExactly 'gim-explorer'
        $result.updated_input.description | Should -BeExactly 'field to keep'
        $result.updated_input.custom_flag | Should -BeTrue
        ([string]$result.updated_input.prompt).Contains('original prompt') | Should -BeTrue
    }

    It 'falls back to subagentType when subagent_type is absent' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = @{ subagentType = 'gim-explorer'; prompt = 'fallback prompt' }
        }
        $result = $raw | ConvertFrom-Json
        $result.permission | Should -BeExactly 'allow'
        $result.updated_input.subagentType | Should -BeExactly 'gim-explorer'
        ([string]$result.updated_input.prompt) | Should -Match '<gim-preload agent="gim-explorer"'
    }

    It 'accepts tool_input supplied as a JSON string' {
        $raw = Get-HookJsonFor -Payload @{
            hook_event_name = 'preToolUse'
            tool_name       = 'Task'
            tool_input      = '{"subagent_type":"gim-explorer","prompt":"string input tail","description":"keep me"}'
        }
        $result = $raw | ConvertFrom-Json
        $result.permission | Should -BeExactly 'allow'
        $result.updated_input.subagent_type | Should -BeExactly 'gim-explorer'
        $result.updated_input.description | Should -BeExactly 'keep me'
        ([string]$result.updated_input.prompt).TrimEnd().EndsWith('string input tail') | Should -BeTrue
    }
}

Describe 'sessionStart — fixture orchestrator preload' {
    It 'returns additional_context for the orchestrator' {
        $raw = Get-HookJsonFor -Payload @{ hook_event_name = 'sessionStart' }
        $result = $raw | ConvertFrom-Json
        $context = [string]$result.additional_context
        $context | Should -Match '<gim-preload agent="gim-orchestrator"'
        $context | Should -Match 'fixture header'
        $context | Should -Match 'body of fixture-orchestrator-skill'
        $context | Should -Match 'fixture prefs body'
        $result.additional_context | Should -Not -BeNullOrEmpty
    }
}

Describe 'missing entries — skipped and reported' {
    It 'skips missing skills and files without breaking the block' {
        $found = Get-GimPreloadBlock -Agent 'gim-explorer' -Root $script:FixtureRoot
        $found.Block | Should -Match '<gim-preload agent="gim-explorer"'
        $found.Block | Should -Match 'fixture prefs body'
        $found.Block | Should -Not -Match 'missing-skill'
        $found.Block | Should -Not -Match 'no-such-file'
        @($found.Missing) | Should -Contain 'missing-skill'
        @($found.Missing) | Should -Contain 'no-such-file.md'
        @($found.Files).Count | Should -Be 2
    }
}

Describe 'fail-open behavior — garbage/empty/unknown payloads' {
    It 'returns {} for garbage stdin' {
        (Invoke-GimPreloadHook -Raw 'this is not json' -Root $script:FixtureRoot) |
            Should -BeExactly '{}'
    }

    It 'returns {} for empty stdin' {
        (Invoke-GimPreloadHook -Raw '' -Root $script:FixtureRoot) |
            Should -BeExactly '{}'
    }

    It 'returns {} for whitespace stdin' {
        (Invoke-GimPreloadHook -Raw "  `n`t " -Root $script:FixtureRoot) |
            Should -BeExactly '{}'
    }

    It 'returns {} for a payload with no event or tool name' {
        (Invoke-GimPreloadHook -Raw '{"unexpected":true}' -Root $script:FixtureRoot) |
            Should -BeExactly '{}'
    }
}

Describe 'stdin reader — StringReader parity with the permission hook' {
    It 'reads a [Console]::SetIn StringReader payload directly' {
        $original = [Console]::In
        $reader = [System.IO.StringReader]::new('{"hook_event_name":"sessionStart"}')
        try {
            [Console]::SetIn($reader)
            $raw = Read-GimPreloadStdin
            $raw | Should -Not -BeNullOrEmpty
            $raw | Should -Match 'sessionStart'
        } finally {
            $reader.Dispose()
            [Console]::SetIn($original)
        }
    }

    It 'strips one leading U+FEFF after the read' {
        $original = [Console]::In
        $reader = [System.IO.StringReader]::new([string][char]0xFEFF + '{"a":1}')
        try {
            [Console]::SetIn($reader)
            (Read-GimPreloadStdin) | Should -BeExactly '{"a":1}'
        } finally {
            $reader.Dispose()
            [Console]::SetIn($original)
        }
    }
}

Describe 'script entry point — in-process (no pwsh spawn)' {
    It 'injects through Invoke-GimPreloadMain with StringReader stdin' {
        $payload = '{"hook_event_name":"preToolUse","tool_name":"Task","tool_input":{"subagent_type":"gim-explorer","prompt":"entry tail"}}'
        $raw = Invoke-MainWithStdin -Json $payload
        $result = $raw | ConvertFrom-Json
        $result.permission | Should -BeExactly 'allow'
        $result.updated_input | Should -Not -BeNullOrEmpty
        ([string]$result.updated_input.prompt).TrimEnd().EndsWith('entry tail') | Should -BeTrue
    }

    It 'returns {} for garbage stdin through the entry point' {
        (Invoke-MainWithStdin -Json 'not json') | Should -BeExactly '{}'
    }

    It 'returns {} for an empty stdin through the entry point' {
        (Invoke-MainWithStdin -Json '') | Should -BeExactly '{}'
    }

    It 'returns {} for a non-Task payload through the entry point' {
        $payload = '{"hook_event_name":"preToolUse","tool_name":"Read","tool_input":{"path":"x"}}'
        (Invoke-MainWithStdin -Json $payload) | Should -BeExactly '{}'
    }

    It 'returns additional_context for sessionStart through the entry point' {
        $payload = '{"hook_event_name":"sessionStart"}'
        $raw = Invoke-MainWithStdin -Json $payload
        $result = $raw | ConvertFrom-Json
        $result.additional_context | Should -Match '<gim-preload agent="gim-orchestrator"'
    }
}

Describe 'enabled coercion — only a literal boolean false disables' {
    BeforeAll {
        function New-EnabledFixture {
            param([string]$EnabledLiteral)
            $root = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-preload-enabled-' + [guid]::NewGuid().ToString('N'))
            $skillDir = Join-Path $root '.agents\skills\fixture-explorer-skill'
            [void][System.IO.Directory]::CreateDirectory($skillDir)
            [System.IO.File]::WriteAllText(
                (Join-Path $skillDir 'SKILL.md'),
                "---`nname: fixture-explorer-skill`ndescription: fixture skill`n---`n# fixture`n`nbody`n"
            )
            $configJson = '{"enabled":' + $EnabledLiteral + ',"header":"coercion header",' +
                          '"agents":{"gim-explorer":{"skills":["fixture-explorer-skill"],"files":[]}}}'
            [System.IO.File]::WriteAllText((Join-Path $root '.agents\agent-preload.json'), $configJson)
            return $root
        }
    }

    It 'disables on a literal boolean false' {
        $root = New-EnabledFixture -EnabledLiteral 'false'
        try {
            (Get-GimPreloadBlock -Agent 'gim-explorer' -Root $root).Block | Should -BeExactly ''
            (Invoke-GimPreloadHook -Raw '{"hook_event_name":"sessionStart"}' -Root $root) |
                Should -BeExactly '{}'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'stays enabled for the string "false" and the number 0' {
        $stringRoot = New-EnabledFixture -EnabledLiteral '"false"'
        $zeroRoot = New-EnabledFixture -EnabledLiteral '0'
        try {
            (Get-GimPreloadBlock -Agent 'gim-explorer' -Root $stringRoot).Block |
                Should -Match '<gim-preload agent="gim-explorer"'
            (Get-GimPreloadBlock -Agent 'gim-explorer' -Root $zeroRoot).Block |
                Should -Match '<gim-preload agent="gim-explorer"'
        } finally {
            Remove-Item -LiteralPath $stringRoot -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $zeroRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'diagnostic log — metadata only' {
    It 'never logs the prompt text or user_email' {
        $originalTemp = $env:TEMP
        $logRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('gim-preload-log-' + [guid]::NewGuid().ToString('N'))
        [void][System.IO.Directory]::CreateDirectory($logRoot)
        $env:TEMP = $logRoot
        try {
            $raw = Get-HookJsonFor -Payload @{
                hook_event_name = 'preToolUse'
                tool_name       = 'Task'
                user_email      = 'secret-account@example.com'
                tool_input      = @{
                    subagent_type = 'gim-explorer'
                    prompt        = 'LOG-REDACTION-SENTINEL do the work'
                }
            }
            ([string]($raw | ConvertFrom-Json).updated_input.prompt) |
                Should -Match 'LOG-REDACTION-SENTINEL'

            $logPath = Join-Path (Join-Path $logRoot 'cursor') 'gim-preload.log'
            [System.IO.File]::Exists($logPath) | Should -BeTrue
            $log = [System.IO.File]::ReadAllText($logPath)
            $log | Should -Not -Match 'LOG-REDACTION-SENTINEL'
            $log | Should -Not -Match 'secret-account@example.com'
            $log | Should -Not -Match 'user_email'
        } finally {
            $env:TEMP = $originalTemp
            Remove-Item -LiteralPath $logRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'live config — structural only' {
    BeforeAll {
        $configPath = Join-Path $script:RepoRoot '.agents\agent-preload.json'
        $script:LiveConfig = $null
        if ([System.IO.File]::Exists($configPath)) {
            $script:LiveConfig = [System.IO.File]::ReadAllText(
                $configPath,
                [System.Text.Encoding]::UTF8
            ) | ConvertFrom-Json
        }
    }

    It 'parses and has the eight fleet agents, with no linter entry' {
        $script:LiveConfig | Should -Not -BeNullOrEmpty
        $agents = Get-GimPreloadProp -Obj $script:LiveConfig -Name 'agents'
        $agentNames = @(Get-GimPreloadKeys -Obj $agents)
        $agentNames.Count | Should -Be 8
        foreach ($expected in @(
            'gim-orchestrator', 'gim-explorer', 'gim-builder',
            'gim-version-controller', 'gim-critic', 'gim-tracker',
            'gim-debugger', 'gim-tester'
        )) {
            $agentNames | Should -Contain $expected
        }
        $removedAgents = @('gim-linter')
        foreach ($removed in $removedAgents) {
            $agentNames | Should -Not -Contain $removed
        }
    }

    It 'uses harness-neutral comment and header wording' {
        $comment = [string](Get-GimPreloadProp -Obj $script:LiveConfig -Name '$comment')
        $header = [string](Get-GimPreloadProp -Obj $script:LiveConfig -Name 'header')
        $comment | Should -Not -Match 'system prompt'
        $comment | Should -Not -Match 'every user turn'
        $header | Should -Not -Match 'later turn'
        $header | Should -Not -Match 'system prompt'
        $header | Should -Match 'Attached to this agent'
    }
}
