# parse-command-tree.ps1 - central PowerShell command-tree parser (plan 0004 A1).
#
# One parser, written once - no per-harness twins. Parses a PowerShell command
# string with [System.Management.Automation.Language.Parser]::ParseInput and
# emits a JSON tree. Never emits comma-separated text: commas occur inside
# commands, so a delimited format would break.
#
# Dual use:
#   - in-shell standalone: & ./.agents/skills/gim-tools/scripts/parse-command-tree.ps1
#       -Command '<cmd>' (or pipe the command via stdin). Reads the command from
#       -Command when given, else from stdin; writes the JSON tree to stdout.
#       stdout is JSON only - callers must not see other output.
#   - dot-sourceable: . parse-command-tree.ps1 defines Get-GimCommandTree for
#       in-process callers (cursor hook).
#
# Invocation rule (user-locked): agents NEVER invoke `pwsh` / `powershell` as a
# command - the harness shell already runs pwsh and scripts run in-shell. Agents
# run this script directly via `& <path> -Command ...` or piped stdin.
# Sole exception: the opencode plugin (JS, non-agent) spawns
# `pwsh -NoProfile -File ...` - the only sanctioned external spawn.
#
# JSON tree shape:
# {
#   "ok": true,
#   "parseErrors": ["..."],
#   "hasSubstitution": false,
#   "substitutions": ["$("],
#   "staticCalls": ["::"],
#   "memberCalls": ["GetType", "Assembly"],
#   "commands": [
#     {
#       "name": "git",
#       "args": ["diff", "--", ".gitignore", "A", "B"],
#       "tokens": [{"text": "...", "kind": "command|string|number|operator|...", "position": 0}],
#       "text": "git diff -- .gitignore A B",
#       "depth": 0,
#       "pipelineStage": 0,
#       "dashDash": {"index": 2, "firstFileAfter": ".gitignore"}
#     }
#   ]
# }
#
# Semantics:
# - commands: every CommandAst in the tree, depth-first source order.
#   Structural facts come from the Ast (CommandAst, PipelineAst,
#   StatementBlockAst, script blocks, parentheses); quote handling, '' /
#   backtick escapes, and string-vs-command classification come from the
#   PowerShell parser itself, never hand-rolled regex.
# - name: command word (bare word, or dequoted quoted command name).
# - args: command elements after the name; string elements are dequoted via
#   SafeGetValue, everything else keeps its raw extent text.
# - tokens: significant tokens of this command (comment / newline / whitespace
#   are excluded); kind is the PowerShell token kind name (Identifier, String,
#   Number, Parameter, Operator, ...); position is the 0-based char offset in
#   the full input.
# - text: the command's extent text (name + args; no pipeline separators).
# - depth: nesting level - 0 at top level; +1 per enclosing script block
#   `{ }`, subexpression `$( )`, parenthesis `( )`, or statement-condition
#   parens (if/while/for/foreach/switch).
# - pipelineStage: 0-based index of the command in its enclosing pipeline
#   (0 when the command is not piped).
# - dashDash: present ONLY for a git command whose arg stream contains a
#   standalone `--` token (exact token; --all / --oneline are NOT matches; a
#   quoted "--" is a string, not a match). index = position in the command
#   element stream (name + args, so `git diff -- x` reports 2);
#   firstFileAfter = the arg after `--`, or null when `--` is the last arg.
# - hasSubstitution / staticCalls: `$(` / `::` occurrences in the raw input
#   OUTSIDE quoted string literals and comments (their extents come from the
#   parser). Unquoted expandable barewords like `pre$(x)post` still count.
# - memberCalls: instance member-access member names outside string literals
#   and comments - AST-based: MemberExpressionAst with Static=false covers
#   `.member` and `.Method(...)` (incl. InvokeMemberExpressionAst), plus
#   `$ExecutionContext` variable references from the token stream. Scanned
#   over the AST, so bare instance-expression statements like
#   `$x.Invoke("hi")` (InvokeMemberExpressionAst - no CommandAst) still
#   report their members. `::` members stay under staticCalls (unchanged).
# - JSON arrays (commands, args, tokens, substitutions, staticCalls,
#   memberCalls, parseErrors) always serialize as arrays, even with one element.
# - On parse failure: ok=false with parseErrors; the envelope is still full
#   JSON (never a partial text dump).
#
# Encoding: UTF-8 without BOM. ASCII only.

[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromPipeline = $true)]
    [AllowEmptyString()]
    [string]$Command
)

function Test-GimPositionInRanges {
    <#
    .SYNOPSIS
        True when a 0-based input position falls inside any of the given ranges.
    .PARAMETER Ranges
        List of objects with Start / End (0-based, end-exclusive).
    .PARAMETER Position
        Character position in the raw input.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object]$Ranges,
        [Parameter(Mandatory = $true)]
        [int]$Position
    )
    foreach ($r in $Ranges) {
        if ($Position -ge $r.Start -and $Position -lt $r.End) { return $true }
    }
    return $false
}

function Get-GimSafeStringValue {
    <#
    .SYNOPSIS
        Dequoted string value of a string AST node, never throwing.
    .DESCRIPTION
        SafeGetValue() throws for dynamic expressions (e.g. unquoted expandable
        barewords like `pre$(Get-Date)post` or interpolated `"a $($x) b"`);
        fall back to the raw extent text so parsing never dies on an arg.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.Language.Ast]$Node
    )
    try {
        $value = $Node.SafeGetValue()
        if ($null -ne $value) { return [string]$value }
    } catch {
        # dynamic expression - cannot evaluate statically
    }
    return $Node.Extent.Text
}

function Get-GimCommandTree {
    <#
    .SYNOPSIS
        Parse a PowerShell command string into the command-tree JSON object.
    .DESCRIPTION
        Returns a PSCustomObject shaped like the documented JSON tree. In-process
        callers dot-source this script and call Get-GimCommandTree directly; the
        standalone entry point serializes the same object to stdout.
    .PARAMETER Command
        The PowerShell command string to parse.
    .EXAMPLE
        Get-GimCommandTree -Command 'git diff -- .gitignore A B'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
        [AllowEmptyString()]
        [string]$Command
    )

    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput(
        $Command, [ref]$tokens, [ref]$errors)
    if ($null -eq $errors) { $errors = @() }
    if ($null -eq $tokens) { $tokens = @() }

    # Ranges the `$(` / `::` scan must skip: comments and QUOTED string
    # literals (single, double, here-strings). An unquoted expandable bareword
    # like `pre$(x)post` is NOT a literal - its `$(` is a real substitution
    # and must still count.
    $excludedRanges = [System.Collections.Generic.List[object]]::new()
    foreach ($t in $tokens) {
        if ($t.Kind -eq [System.Management.Automation.Language.TokenKind]::Comment) {
            $excludedRanges.Add([pscustomobject]@{
                Start = $t.Extent.StartOffset
                End   = $t.Extent.EndOffset
            })
        }
    }
    if ($null -ne $ast) {
        $stringNodes = $ast.FindAll({
            param($x)
            if ($x -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                $x.StringConstantType -ne [System.Management.Automation.Language.StringConstantType]::BareWord
            } else {
                $x -is [System.Management.Automation.Language.ExpandableStringExpressionAst]
            }
        }, $true)
        foreach ($n in $stringNodes) {
            # Quoted literals start with a quote / here-string marker; bare
            # words (incl. expandable barewords) never do.
            $text = $n.Extent.Text
            $isQuotedLiteral = $text.Length -gt 0 -and (
                $text[0] -eq [char]'"' -or
                $text[0] -eq [char]"'" -or
                $text[0] -eq [char]'@')
            if ($isQuotedLiteral) {
                $excludedRanges.Add([pscustomobject]@{
                    Start = $n.Extent.StartOffset
                    End   = $n.Extent.EndOffset
                })
            }
        }
    }

    # `$(` / `::` scan over the raw input, excluding literal/comment positions.
    $substitutions = [System.Collections.Generic.List[string]]::new()
    $staticCalls = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrEmpty($Command)) {
        $pos = 0
        while (($pos = $Command.IndexOf('$(', $pos)) -ge 0) {
            if (-not (Test-GimPositionInRanges -Ranges $excludedRanges -Position $pos)) {
                if (-not $substitutions.Contains('$(')) { $substitutions.Add('$(') }
            }
            $pos += 2
        }
        $pos = 0
        while (($pos = $Command.IndexOf('::', $pos)) -ge 0) {
            if (-not (Test-GimPositionInRanges -Ranges $excludedRanges -Position $pos)) {
                if (-not $staticCalls.Contains('::')) { $staticCalls.Add('::') }
            }
            $pos += 2
        }
    }

    # Collect every CommandAst in the tree (depth-first document order).
    $commandNodes = [System.Collections.Generic.List[object]]::new()
    if ($null -ne $ast) {
        $nodes = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.CommandAst]
        }, $true)
        foreach ($n in $nodes) { $commandNodes.Add($n) }
    }

    # Significant tokens: no comment / newline / whitespace-only tokens.
    $significantTokens = @($tokens | Where-Object {
        $_.Kind -ne [System.Management.Automation.Language.TokenKind]::Comment -and
        $_.Kind -ne [System.Management.Automation.Language.TokenKind]::NewLine -and
        $_.Text.Trim() -ne ''
    })

    # memberCalls: instance member-access member names via the AST
    # (MemberExpressionAst with Static=false covers `.member` and
    # `.Method(...)`, including InvokeMemberExpressionAst). Static `::`
    # members stay under staticCalls (unchanged). AST-based, so bare
    # expressions with no CommandAst are still reported. Member nodes whose
    # extent falls inside a quoted string literal or comment (excludedRanges)
    # are skipped - e.g. "$x.GetType()" must NOT report GetType.
    # `$ExecutionContext` variable references are added from the token stream
    # (outside strings/comments).
    $memberCalls = [System.Collections.Generic.List[string]]::new()
    if ($null -ne $ast) {
        $memberNodes = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.MemberExpressionAst] -and -not $node.Static
        }, $true)
        foreach ($m in $memberNodes) {
            if (Test-GimPositionInRanges -Ranges $excludedRanges -Position $m.Extent.StartOffset) {
                continue
            }
            $member = $m.Member
            if ($null -ne $member) {
                $memberName = Get-GimSafeStringValue -Node $member
                if (-not [string]::IsNullOrEmpty($memberName) -and
                    -not $memberCalls.Contains($memberName)) {
                    $memberCalls.Add($memberName)
                }
            }
        }
    }
    for ($i = 0; $i -lt $significantTokens.Count; $i++) {
        $t = $significantTokens[$i]
        if ($t.Kind -eq [System.Management.Automation.Language.TokenKind]::Variable -and
            $t.Text -eq '$ExecutionContext') {
            if (-not $memberCalls.Contains('$ExecutionContext')) { $memberCalls.Add('$ExecutionContext') }
        }
    }

    # Assign each token to the innermost command whose extent contains it.
    $tokenMap = @{}
    foreach ($t in $significantTokens) {
        $best = $null
        foreach ($c in $commandNodes) {
            if ($t.Extent.StartOffset -ge $c.Extent.StartOffset -and
                $t.Extent.StartOffset -lt $c.Extent.EndOffset) {
                if ($null -eq $best -or $c.Extent.StartOffset -gt $best.Extent.StartOffset) {
                    $best = $c
                }
            }
        }
        if ($null -ne $best) {
            if (-not $tokenMap.ContainsKey($best)) {
                $tokenMap[$best] = [System.Collections.Generic.List[object]]::new()
            }
            $tokenMap[$best].Add($t)
        }
    }

    $commandsOut = [System.Collections.Generic.List[object]]::new()

    foreach ($cmd in $commandNodes) {
        $elements = @($cmd.CommandElements)

        # Command name: dequoted quoted name (single or double), bare word,
        # or raw extent.
        $name = ''
        if ($elements.Count -gt 0) {
            $nameEl = $elements[0]
            if ($nameEl -is [System.Management.Automation.Language.StringConstantExpressionAst] -or
                $nameEl -is [System.Management.Automation.Language.ExpandableStringExpressionAst]) {
                $name = Get-GimSafeStringValue -Node $nameEl
            } else {
                $name = $nameEl.Extent.Text
            }
        }

        # Args + per-arg facts (raw extent text, quoted-string flag).
        $args = [System.Collections.Generic.List[string]]::new()
        $argFacts = [System.Collections.Generic.List[object]]::new()
        for ($i = 1; $i -lt $elements.Count; $i++) {
            $el = $elements[$i]
            $rawText = $el.Extent.Text
            $isQuotedString = $false
            if ($el -is [System.Management.Automation.Language.ExpandableStringExpressionAst]) {
                $isQuotedString = $true
            } elseif ($el -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                if ($el.StringConstantType -ne [System.Management.Automation.Language.StringConstantType]::BareWord) {
                    $isQuotedString = $true
                }
            }
            $argText = if ($el -is [System.Management.Automation.Language.StringConstantExpressionAst] -or
                           $el -is [System.Management.Automation.Language.ExpandableStringExpressionAst]) {
                Get-GimSafeStringValue -Node $el
            } else {
                $rawText
            }
            $args.Add($argText)
            $argFacts.Add([pscustomobject]@{
                RawText  = $rawText
                IsQuoted = $isQuotedString
            })
        }

        # dashDash: git only, standalone `--` only (exact token, not quoted).
        $dashDash = $null
        if ($name -eq 'git') {
            for ($i = 0; $i -lt $args.Count; $i++) {
                if (-not $argFacts[$i].IsQuoted -and $argFacts[$i].RawText -eq '--') {
                    $firstFileAfter = $null
                    if ($i + 1 -lt $args.Count) { $firstFileAfter = $args[$i + 1] }
                    # index counts the command element stream (name + args),
                    # matching the plan example (`git diff -- .gitignore` -> 2).
                    $dashDash = [pscustomobject]@{
                        index          = $i + 1
                        firstFileAfter = $firstFileAfter
                    }
                    break
                }
            }
        }

        # depth: nesting level. Root script body is a NamedBlockAst (never
        # counted); a level comes from each enclosing script block `{ }`
        # (ScriptBlockAst, non-root), subexpression `$( )`, parenthesis `( )`,
        # bare statement body block (if/while/foreach/function), or a command
        # sitting inside statement-condition parens (if/while/for/foreach/switch).
        $depth = 0
        $pipeline = $null
        $node = $cmd.Parent
        while ($null -ne $node) {
            $isNesting = $false
            if ($node -is [System.Management.Automation.Language.SubExpressionAst] -or
                $node -is [System.Management.Automation.Language.ParenExpressionAst]) {
                $isNesting = $true
            } elseif ($node -is [System.Management.Automation.Language.ScriptBlockAst]) {
                $isNesting = ($node -ne $ast)   # exclude the root script block
            } elseif ($node -is [System.Management.Automation.Language.StatementBlockAst]) {
                # A block that is a container's own body (script block,
                # subexpression, parens) is already counted as the container.
                $parent = $node.Parent
                $isNesting = -not (
                    $null -ne $parent -and (
                        $parent -is [System.Management.Automation.Language.SubExpressionAst] -or
                        $parent -is [System.Management.Automation.Language.ScriptBlockAst] -or
                        $parent -is [System.Management.Automation.Language.ParenExpressionAst]))
            } elseif ($node -is [System.Management.Automation.Language.IfStatementAst]) {
                # IfStatementAst has no .Condition property: conditions live in
                # Clauses as Item1 (condition pipeline), Item2 (body block).
                foreach ($clause in $node.Clauses) {
                    $cond = $clause.Item1
                    if ($null -ne $cond -and
                        $cmd.Extent.StartOffset -ge $cond.Extent.StartOffset -and
                        $cmd.Extent.StartOffset -lt $cond.Extent.EndOffset) {
                        $isNesting = $true   # command sits inside an if/elseif condition
                        break
                    }
                }
            } elseif ($node -is [System.Management.Automation.Language.WhileStatementAst] -or
                      $node -is [System.Management.Automation.Language.DoWhileStatementAst] -or
                      $node -is [System.Management.Automation.Language.DoUntilStatementAst] -or
                      $node -is [System.Management.Automation.Language.ForStatementAst] -or
                      $node -is [System.Management.Automation.Language.ForEachStatementAst] -or
                      $node -is [System.Management.Automation.Language.SwitchStatementAst]) {
                $cond = $node.Condition
                if ($null -ne $cond -and
                    $cmd.Extent.StartOffset -ge $cond.Extent.StartOffset -and
                    $cmd.Extent.StartOffset -lt $cond.Extent.EndOffset) {
                    $isNesting = $true   # command sits inside the statement's ( ) condition
                }
            }
            if ($isNesting) { $depth++ }
            if ($null -eq $pipeline -and
                $node -is [System.Management.Automation.Language.PipelineAst]) {
                $pipeline = $node
            }
            $node = $node.Parent
        }

        # pipelineStage: index of the command (or its container) in the pipeline.
        $pipelineStage = 0
        if ($null -ne $pipeline) {
            $pipelineElements = @($pipeline.PipelineElements)
            for ($i = 0; $i -lt $pipelineElements.Count; $i++) {
                if ($pipelineElements[$i] -eq $cmd) { $pipelineStage = $i; break }
                if ($cmd.Extent.StartOffset -ge $pipelineElements[$i].Extent.StartOffset -and
                    $cmd.Extent.StartOffset -lt $pipelineElements[$i].Extent.EndOffset) {
                    $pipelineStage = $i
                    break
                }
            }
        }

        # Tokens of this command (innermost-command assignment).
        $tokensOut = [System.Collections.Generic.List[object]]::new()
        if ($tokenMap.ContainsKey($cmd)) {
            foreach ($t in $tokenMap[$cmd]) {
                $tokensOut.Add([pscustomobject]@{
                    text     = $t.Text
                    kind     = $t.Kind.ToString()
                    position = $t.Extent.StartOffset
                })
            }
        }

        $cmdObj = [ordered]@{
            name          = $name
            args          = [string[]]$args
            tokens        = [object[]]$tokensOut
            text          = $cmd.Extent.Text.Trim()
            depth         = $depth
            pipelineStage = $pipelineStage
        }
        if ($null -ne $dashDash) {
            $cmdObj['dashDash'] = $dashDash
        }
        $commandsOut.Add([pscustomobject]$cmdObj)
    }

    $parseErrors = @($errors | ForEach-Object { $_.Message })

    return [pscustomobject][ordered]@{
        ok              = ($parseErrors.Count -eq 0)
        parseErrors     = [string[]]$parseErrors
        hasSubstitution = ($substitutions.Count -gt 0)
        substitutions   = [string[]]$substitutions
        staticCalls     = [string[]]$staticCalls
        memberCalls     = [string[]]$memberCalls
        commands        = [object[]]$commandsOut
    }
}

# Standalone entry point: run as a script (not dot-sourced).
if ($MyInvocation.InvocationName -ne '.') {
    # -Command (or pipeline input) wins; stdin is only read when neither was
    # provided (external pwsh -File spawn with stdin redirection).
    if ($PSBoundParameters.ContainsKey('Command')) {
        $inputText = $Command
    } else {
        $inputText = [Console]::In.ReadToEnd()
    }
    try {
        $tree = Get-GimCommandTree -Command $inputText
        Write-Output ($tree | ConvertTo-Json -Compress -Depth 20)
    } catch {
        $err = [pscustomobject][ordered]@{
            ok              = $false
            parseErrors     = @($_.Exception.Message)
            hasSubstitution = $false
            substitutions   = @()
            staticCalls     = @()
            memberCalls     = @()
            commands        = @()
        }
        Write-Output ($err | ConvertTo-Json -Compress -Depth 20)
    }
}
