@{
    RootModule           = 'GIMTools.psm1'
    ModuleVersion        = '0.1.0'
    GUID                 = 'cb9f2fce-07d8-4ff8-9430-1f9b75d958ae'
    Author               = 'GIM'
    CompanyName          = 'GIM'
    Copyright            = '(c) GIM. All rights reserved.'
    Description          = 'GIM agent toolkit: data-only gim-* verbs for path listing, filtering, reporting, search, inspection, head/count/diff, JSON querying, assembly type enumeration, a TTL variable store, mutating file verbs, confined dotnet execution/probing, fleet preflight (auth/repo/docs index), explicit PR creation (stage/commit/push/PR/stack), read-only Jira search, Jira create/assign/verify, Jira status transition, read-only PR gather plus authored responses shell for wrapup (PowerShell shim + stdlib Node helper), wrapup reply/resolve with a sibling receipt, and the gim-verbs vocabulary/reference generator; 25 verbs across nine groups, with the seven fleet verbs role-scoped and without script-block built-ins. Phase 1 verbs: gim-list, gim-where, gim-report, gim-grep, gim-inspect, gim-head, gim-count, gim-diff, gim-json, gim-asm, gim-set, gim-get, gim-fix, gim-write, gim-apply, gim-run, gim-probe, gim-preflight, gim-pr, gim-jira-search, gim-jira-create, gim-jira-transition, gim-wrapup-gather, gim-wrapup-respond, gim-verbs.'
    PowerShellVersion    = '5.1'
    FunctionsToExport    = @('gim-list','gim-where','gim-report','gim-grep','gim-inspect','gim-head','gim-count','gim-diff','gim-json','gim-asm','gim-set','gim-get','gim-fix','gim-write','gim-apply','gim-run','gim-probe','gim-preflight','gim-pr','gim-jira-search','gim-jira-create','gim-jira-transition','gim-wrapup-gather','gim-wrapup-respond','gim-verbs')
    CmdletsToExport      = @()
    VariablesToExport    = @()
    AliasesToExport      = @()
}
