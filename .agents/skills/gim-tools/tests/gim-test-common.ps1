# gim-test-common.ps1 - shared Pester 6 test helpers for the gim-tools suite.
# Dot-source from each suite file: . (Join-Path $here 'gim-test-common.ps1')
# ASCII only; UTF-8 without BOM.
#
# Get-GimTestRepoRoot is defined in GLOBAL scope on purpose. Pester 6.1.0 on
# pwsh 7.6.5 throws a flow-control escape (Pester #2669) when a function
# defined in test-file (script) scope is invoked from It/BeforeAll, and
# discovery-scope variables are invisible inside run blocks. Functions defined
# outside the test-file scope (module/global) are invoked fine.

function global:Get-GimTestRepoRoot {
    # GIMmos repo root from this file's directory:
    # tests -> gim-tools -> skills -> .agents -> GIMmos (4 parents up).
    # $PSScriptRoot is unambiguous: this helper and every suite that
    # dot-sources it live in the same tests directory.
    return Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
}

function global:New-GimTestReparsePoint {
    # Junction on Windows, symlink elsewhere. Both are reparse points that
    # Get-GimRealPath follows.
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Target
    )
    if ([System.IO.Path]::DirectorySeparatorChar -eq [char]'/') {
        New-Item -ItemType SymbolicLink -Path $Path -Target $Target | Out-Null
        return
    }
    New-Item -ItemType Junction -Path $Path -Target $Target | Out-Null
}

function global:Get-GimTestOutsidePath {
    # A rooted path outside the repo. On Windows this stays under C:\Windows
    # so existing assertions keep the same path. On Unix, C:\... is not rooted
    # and would land inside the working directory.
    param([string]$Child = '')
    if ([System.IO.Path]::DirectorySeparatorChar -eq '\') {
        if ([string]::IsNullOrEmpty($Child)) { return 'C:\Windows' }
        return (Join-Path 'C:\Windows' $Child)
    }
    $root = [System.IO.Path]::GetTempPath().TrimEnd([char[]]@('\', '/'))
    if ([string]::IsNullOrEmpty($Child)) { return $root }
    return (Join-Path $root $Child)
}

function global:Get-GimTestNonTempOutsidePath {
    # Outside the repo and outside the OS temp root. gim-apply admits the
    # temp root, so C:\Windows (Windows) or /var/lib (Unix) is the reject path.
    param([string]$Child = 'gim-forbidden')
    if ([System.IO.Path]::DirectorySeparatorChar -eq '\') {
        return (Join-Path 'C:\Windows' $Child)
    }
    return (Join-Path '/var/lib' $Child)
}

function global:Get-GimTestParentEscape {
    # '..\..\..\leaf' on Windows and '../../../leaf' on Unix, so GetFullPath
    # actually leaves the repo on both.
    param([string]$Leaf = 'Windows')
    $sep = [System.IO.Path]::DirectorySeparatorChar
    return ('..' + $sep + '..' + $sep + '..' + $sep + $Leaf)
}
