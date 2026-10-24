# parse-command-tree.tests.ps1 - Pester 6 tests for the A1 central command-tree
# parser lib (.agents/skills/gim-tools/scripts/parse-command-tree.ps1).
#
# Self-contained: no network, no external tools; invokes the script through the
# standalone entry point (`& <script> -Command ...`) and the dot-source path.
# The suite never runs tests itself - the gim-tester agent owns execution.
#
# Pester 6 syntax: Should -Be / -BeEmpty / -BeNullOrEmpty / -BeGreaterThan /
# -Contain / -Not.
#
# Paths are resolved in BeforeAll (run scope): Pester 6 discovery-scope
# variables are invisible inside It blocks (see gim-test-common.ps1 note).

BeforeAll {
    $script:scriptPath = Join-Path $PSScriptRoot '..\scripts\parse-command-tree.ps1'
}

Describe 'envelope and standalone entry' {
    It 'emits exactly one JSON-only line for a valid command' {
        $out = @(& $scriptPath -Command 'git diff -- .gitignore A B')
        $out.Count | Should -Be 1
        $tree = $out[0] | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.parseErrors).Count | Should -Be 0
        $tree.hasSubstitution | Should -Be $false
        $tree.commands.Count | Should -Be 1
    }
    It 'reads the command from the pipeline when -Command is omitted' {
        $tree = ('git log --oneline' | & $scriptPath) | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.commands[0].name | Should -Be 'git'
    }
    It 'emits an empty commands list for an empty input' {
        $tree = (& $scriptPath -Command '') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.commands.Count | Should -Be 0
    }
    It 'survives commas inside commands (JSON, not a delimited format)' {
        $tree = (& $scriptPath -Command 'git commit -m "a, b"') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.commands[0].args[2] | Should -Be 'a, b'
    }
    It 'always serializes collections as arrays (single-element cases)' {
        $tree = (& $scriptPath -Command 'git') | ConvertFrom-Json -AsHashtable
        $tree['commands'] -is [array] | Should -Be $true
        $tree['commands'][0]['args'] -is [array] | Should -Be $true
        $tree['commands'][0]['tokens'] -is [array] | Should -Be $true
        $tree['commands'][0]['tokens'].Count | Should -Be 1
        $tree['substitutions'] -is [array] | Should -Be $true
        $tree['staticCalls'] -is [array] | Should -Be $true
        $tree['parseErrors'] -is [array] | Should -Be $true
    }
    It 'writes commands as a JSON array in the raw output' {
        $raw = (& $scriptPath -Command 'git status') -join ''
        $raw | Should -Match '"commands"\s*:\s*\['
    }
}

Describe 'command name and args' {
    It 'reports name, args, and text for a plain git command' {
        $tree = (& $scriptPath -Command 'git diff -- .gitignore A B') | ConvertFrom-Json
        $cmd = $tree.commands[0]
        $cmd.name | Should -Be 'git'
        $cmd.args.Count | Should -Be 5
        @($cmd.args) -join ' ' | Should -Be 'diff -- .gitignore A B'
        $cmd.text | Should -Be 'git diff -- .gitignore A B'
    }
    It 'dequotes double-quoted message arguments' {
        $tree = (& $scriptPath -Command 'git commit -m "added ''--'' detection"') | ConvertFrom-Json
        $tree.commands[0].name | Should -Be 'git'
        $tree.commands[0].args[2] | Should -Be "added '--' detection"
    }
    It 'handles a quoted command name' {
        $tree = (& $scriptPath -Command "& 'git' log --oneline") | ConvertFrom-Json
        $tree.commands[0].name | Should -Be 'git'
    }
    It 'dequotes double-quoted command names' {
        $tree = (& $scriptPath -Command '& "git" log -- .gitignore X') | ConvertFrom-Json
        $tree.commands.Count | Should -Be 1
        $tree.commands[0].name | Should -Be 'git'
        $tree.commands[0].dashDash.index | Should -Be 2
        $tree.commands[0].dashDash.firstFileAfter | Should -Be '.gitignore'
    }
    It 'classifies strings containing cmdlet names as strings, not commands' {
        $tree = (& $scriptPath -Command 'echo "ForEach-Object and Where-Object are text"') | ConvertFrom-Json
        $tree.commands.Count | Should -Be 1
        $tree.commands[0].name | Should -Be 'echo'
        $names = @($tree.commands | ForEach-Object { $_.name })
        $names | Should -Not -Contain 'ForEach-Object'
        $names | Should -Not -Contain 'Where-Object'
    }
}

Describe 'dashDash (git --)' {
    It 'finds a standalone -- and the first file after it' {
        $tree = (& $scriptPath -Command 'git diff -- .gitignore A B') | ConvertFrom-Json
        $dd = $tree.commands[0].dashDash
        $dd.index | Should -Be 2
        $dd.firstFileAfter | Should -Be '.gitignore'
    }
    It 'reports null firstFileAfter when -- is the last arg' {
        $tree = (& $scriptPath -Command 'git diff --') | ConvertFrom-Json
        $dd = $tree.commands[0].dashDash
        $dd.index | Should -Be 2
        $dd.firstFileAfter | Should -BeNullOrEmpty
    }
    It 'does not match --oneline or other dashed flags' {
        $tree = (& $scriptPath -Command 'git log --oneline') | ConvertFrom-Json
        $tree.commands[0].dashDash | Should -BeNullOrEmpty
    }
    It 'does not treat quoted -- text as a standalone token' {
        $tree = (& $scriptPath -Command 'git commit -m "added ''--'' detection"') | ConvertFrom-Json
        $tree.commands[0].dashDash | Should -BeNullOrEmpty
        $tree2 = (& $scriptPath -Command "git commit -m 'added ''--'' detection'") | ConvertFrom-Json
        $tree2.commands[0].dashDash | Should -BeNullOrEmpty
    }
    It 'keeps the first standalone -- and treats a later quoted -- as a value' {
        $tree = (& $scriptPath -Command 'git diff -- "--"') | ConvertFrom-Json
        $dd = $tree.commands[0].dashDash
        $dd.index | Should -Be 2
        $dd.firstFileAfter | Should -Be '--'
    }
    It 'does not attach dashDash to non-git commands' {
        $tree = (& $scriptPath -Command 'echo --') | ConvertFrom-Json
        $tree.commands[0].dashDash | Should -BeNullOrEmpty
    }
    It 'counts -- across the element stream including the command name' {
        $tree = (& $scriptPath -Command 'git commit -m x -- .gitignore') | ConvertFrom-Json
        $dd = $tree.commands[0].dashDash
        $dd.index | Should -Be 4
        $dd.firstFileAfter | Should -Be '.gitignore'
    }
}

Describe 'substitutions and static calls' {
    It 'flags $() outside strings' {
        $tree = (& $scriptPath -Command 'echo $(Get-Date)') | ConvertFrom-Json
        $tree.hasSubstitution | Should -Be $true
        @($tree.substitutions) -join ',' | Should -Be '$('
    }
    It 'does not flag $() inside double-quoted strings' {
        $tree = (& $scriptPath -Command 'echo "$(Get-Date)"') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $false
        @($tree.substitutions).Count | Should -Be 0
    }
    It 'does not flag $() nested inside double-quoted strings' {
        $tree = (& $scriptPath -Command 'echo "a $($x) b"') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $false
    }
    It 'does not flag $() inside single-quoted strings' {
        $tree = (& $scriptPath -Command 'echo ''$()''') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $false
    }
    It 'flags :: outside strings' {
        $tree = (& $scriptPath -Command 'echo [DateTime]::Now') | ConvertFrom-Json
        @($tree.staticCalls) | Should -Contain '::'
    }
    It 'does not flag :: inside double-quoted strings' {
        $tree = (& $scriptPath -Command 'echo "a::b"') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.staticCalls).Count | Should -Be 0
    }
    It 'does not flag :: inside single-quoted strings' {
        $tree = (& $scriptPath -Command "echo 'a::b'") | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.staticCalls).Count | Should -Be 0
    }
    It 'flags $() embedded in unquoted expandable barewords' {
        $tree = (& $scriptPath -Command 'echo pre$(Get-Date)post') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $true
        $tree = (& $scriptPath -Command 'git commit -m pre$(whoami)post') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $true
    }
    It 'does not flag $() embedded in double-quoted strings' {
        $tree = (& $scriptPath -Command 'echo "pre$(Get-Date)post"') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $false
    }
    It 'does not flag $() inside comments' {
        $tree = (& $scriptPath -Command 'git status # $(Get-Date)') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $false
    }
    It 'does not flag :: inside comments' {
        $tree = (& $scriptPath -Command 'git status # [DateTime]::Now') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.staticCalls).Count | Should -Be 0
    }
    It 'does not flag $() or :: inside here-strings' {
        $cmd = "echo @'`nhere `$(Get-Date) a::b`n'@"
        $tree = (& $scriptPath -Command $cmd) | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $false
        @($tree.staticCalls).Count | Should -Be 0
    }
    It 'still flags $() outside here-strings' {
        $cmd = "echo `$(Get-Date) @'`nx`n'@"
        $tree = (& $scriptPath -Command $cmd) | ConvertFrom-Json
        $tree.ok | Should -Be $true
        $tree.hasSubstitution | Should -Be $true
    }
}

Describe 'memberCalls (member access)' {
    It 'reports member-access identifiers after a dot' {
        $tree = (& $scriptPath -Command '$x.GetType().Assembly') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.memberCalls) | Should -Contain 'GetType'
        @($tree.memberCalls) | Should -Contain 'Assembly'
    }
    It 'reports member calls with no enclosing CommandAst and keeps commands empty' {
        $tree = (& $scriptPath -Command '$x.Invoke("hi")') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.memberCalls) | Should -Contain 'Invoke'
        @($tree.commands).Count | Should -Be 0
    }
    It 'does not report member access inside quoted strings' {
        $tree = (& $scriptPath -Command 'echo ".GetType("') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.memberCalls) | Should -Not -Contain 'GetType'
    }
    It 'does not report member access inside expandable strings' {
        $tree = (& $scriptPath -Command 'echo "$x.GetType()"') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.memberCalls) | Should -Not -Contain 'GetType'
    }
    It 'does not report member access inside comments' {
        $tree = (& $scriptPath -Command 'git status # .Assembly') | ConvertFrom-Json
        $tree.ok | Should -Be $true
        @($tree.memberCalls) | Should -Not -Contain 'Assembly'
    }
    It 'reports $ExecutionContext variable references' {
        $tree = (& $scriptPath -Command '$ExecutionContext.SessionState') | ConvertFrom-Json
        @($tree.memberCalls) | Should -Contain '$ExecutionContext'
    }
    It 'still reports :: via staticCalls (unchanged)' {
        $tree = (& $scriptPath -Command 'echo [DateTime]::Now') | ConvertFrom-Json
        @($tree.staticCalls) | Should -Contain '::'
    }
}

Describe 'depth and pipeline stage' {
    It 'assigns depth 0 and stage 0 to a top-level command' {
        $tree = (& $scriptPath -Command 'git status') | ConvertFrom-Json
        $tree.commands[0].depth | Should -Be 0
        $tree.commands[0].pipelineStage | Should -Be 0
    }
    It 'splits ; && || chains into separate top-level commands' {
        $tree = (& $scriptPath -Command 'git status; git log --oneline && echo done || echo fail') | ConvertFrom-Json
        $tree.commands.Count | Should -Be 4
        @($tree.commands | ForEach-Object { $_.name }) -join ' ' | Should -Be 'git git echo echo'
        foreach ($c in $tree.commands) {
            $c.depth | Should -Be 0
            $c.pipelineStage | Should -Be 0
        }
    }
    It 'numbers pipeline stages 0-based' {
        $tree = (& $scriptPath -Command 'git status | Select-Object -First 1') | ConvertFrom-Json
        $tree.commands.Count | Should -Be 2
        $tree.commands[0].name | Should -Be 'git'
        $tree.commands[0].pipelineStage | Should -Be 0
        $tree.commands[1].name | Should -Be 'Select-Object'
        $tree.commands[1].pipelineStage | Should -Be 1
    }
    It 'gives each chain element its own stage 0 unless piped' {
        $tree = (& $scriptPath -Command 'echo a; echo b | echo c') | ConvertFrom-Json
        $tree.commands.Count | Should -Be 3
        $tree.commands[0].pipelineStage | Should -Be 0
        $tree.commands[1].pipelineStage | Should -Be 0
        $tree.commands[2].pipelineStage | Should -Be 1
    }
    It 'measures depth through script blocks and subexpressions' {
        $tree = (& $scriptPath -Command '& { git log --oneline }') | ConvertFrom-Json
        $git = @($tree.commands | Where-Object { $_.name -eq 'git' })
        $git.Count | Should -Be 1
        $git[0].depth | Should -Be 1
        $tree = (& $scriptPath -Command '$(git status)') | ConvertFrom-Json
        $tree.commands[0].depth | Should -Be 1
    }
    It 'measures depth through parentheses and nested script blocks' {
        $tree = (& $scriptPath -Command 'if (git status) { git log }') | ConvertFrom-Json
        $tree.commands.Count | Should -Be 2
        $tree.commands[0].depth | Should -Be 1
        $tree.commands[1].depth | Should -Be 1
    }
}

Describe 'tokens' {
    It 'lists significant tokens with text, kind, and 0-based position' {
        $tree = (& $scriptPath -Command 'git diff -- .gitignore') | ConvertFrom-Json
        $tokens = @($tree.commands[0].tokens)
        $tokens[0].text | Should -Be 'git'
        $tokens[0].kind | Should -Be 'Identifier'
        $tokens[0].position | Should -Be 0
        @($tokens | ForEach-Object { $_.text }) -join ' ' | Should -Be 'git diff -- .gitignore'
        foreach ($t in $tokens) { $t.kind | Should -Not -BeNullOrEmpty }
    }
    It 'classifies quoted message tokens as StringExpandable' {
        $tree = (& $scriptPath -Command 'git commit -m "msg"') | ConvertFrom-Json
        $strTokens = @($tree.commands[0].tokens | Where-Object { $_.text -eq '"msg"' })
        $strTokens.Count | Should -Be 1
        $strTokens[0].kind | Should -Be 'StringExpandable'
    }
    It 'excludes comments and whitespace from the token list' {
        $tree = (& $scriptPath -Command 'git status # note') | ConvertFrom-Json
        $tokens = @($tree.commands[0].tokens)
        @($tokens | Where-Object { $_.text -match 'note' }).Count | Should -Be 0
        foreach ($t in $tokens) { $t.text.Trim() | Should -Not -Be '' }
    }
    It 'attaches tokens to the innermost command only' {
        $tree = (& $scriptPath -Command '$(git status)') | ConvertFrom-Json
        $tree.commands.Count | Should -Be 1
        @($tree.commands[0].tokens | ForEach-Object { $_.text }) -join ' ' | Should -Be 'git status'
    }
}

Describe 'dot-source contract' {
    It 'defines Get-GimCommandTree usable in-process' {
        $result = & {
            . $scriptPath
            Get-GimCommandTree -Command 'git status'
        }
        $result.ok | Should -Be $true
        $result.commands[0].name | Should -Be 'git'
    }
}

Describe 'malformed input' {
    It 'returns ok:false with parseErrors for an unterminated string' {
        $tree = (& $scriptPath -Command 'git commit -m "oops') | ConvertFrom-Json
        $tree.ok | Should -Be $false
        @($tree.parseErrors).Count | Should -BeGreaterThan 0
    }
    It 'returns ok:false with parseErrors for a stray opening paren' {
        $tree = (& $scriptPath -Command 'if (') | ConvertFrom-Json
        $tree.ok | Should -Be $false
        @($tree.parseErrors).Count | Should -BeGreaterThan 0
    }
    It 'keeps the envelope JSON-only on parse failure' {
        $out = @(& $scriptPath -Command 'if (')
        $out.Count | Should -Be 1
        ($out[0] | ConvertFrom-Json).ok | Should -Be $false
    }
}
