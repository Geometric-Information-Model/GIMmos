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
