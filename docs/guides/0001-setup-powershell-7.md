# Setup: PowerShell 7 runtime (required) + Pester 6 (recommended, optional)

Working on GIMmos (and the GIM workspace fleet) assumes **pwsh 7** as the runtime for agent harness shells, and it is what the `gim-*` toolkit tests run under. This guide is part of initial setup.

Dependency tiers:

- **Required:** pwsh 7 (any recent 7.x; tested against 7.6.5).
- **Recommended but Optional:** Pester 6 — only needed to *run the test suite* for the gim-tools module (`.agents/skills/gim-tools/tests/`). Using the `gim-*` verbs themselves never requires Pester.

---

## Required — install PowerShell 7

Use the **official Microsoft releases on GitHub**: <https://github.com/PowerShell/PowerShell/releases>

Download the `.msi` for the latest stable release (e.g. `PowerShell-7.6.5-win-x64.msi`) and run it. The MSI installer:

- installs to `C:\Program Files\PowerShell\7\pwsh.exe`
- **sets the `PATH` variable** and registers the correct binaries — this is why the MSI is the recommended install method: a `winget`/Store-style App Execution Alias shim (`%LOCALAPPDATA%\Microsoft\WindowsApps\pwsh.exe`) can confuse tools that resolve `pwsh` by path (opencode's shell resolver stat-checks the alias, gets `EACCES`, and silently falls back to Windows PowerShell 5.1 — see below).

Verify:

```powershell
$PSVersionTable.PSVersion   # 7.x.y
where.exe pwsh              # C:\Program Files\PowerShell\7\pwsh.exe (first hit)
```

## Point opencode at pwsh

In `opencode.json` / `opencode.local.json`:

```json
"shell": "pwsh.exe",
```

Notes:

- A bare name (`pwsh`, `pwsh.exe`) is the portable choice; it resolves against each user's `PATH`. Use the absolute path only as a last resort — it is machine-specific.
- If the harness still reports PowerShell 5.1 (`$PSVersionTable.PSVersion` → 5.1.x) after restart, the WindowsApps alias is shadowing the real pwsh on your `PATH` (opencode logs `shell tool using shell=...powershell.EXE` in `~/.local/share/opencode/log/opencode.log`). Re-order `PATH` so `C:\Program Files\PowerShell\7` precedes `%LOCALAPPDATA%\Microsoft\WindowsApps`, then restart opencode (config is read at startup — no hot reload).

## Optional — Pester 6 (test suite only)

You only need this if you run the gim-tools tests:

```powershell
Install-Module Pester -Scope CurrentUser -Force
```

Verify:

```powershell
Get-Module -ListAvailable Pester | Sort-Object Version -Descending | Select-Object -First 1 Version, ModuleBase
# Version 6.x at %USERPROFILE%\Documents\PowerShell\Modules\Pester\6.x.y
```

Notes:

- A legacy Pester 3.4.0 may remain visible from the Windows PowerShell module path (`C:\Program Files\WindowsPowerShell\Modules\Pester\3.4.0`). It is **incompatible with pwsh 7** — ignore it; the highest version (6.x) wins when importing.
- Running the suite: `Invoke-Pester -Path .agents/skills/gim-tools/tests/*.tests.ps1 -PassThru` from the repo root (tests are cwd-sensitive).

---

## Why pwsh 7 is the standard

- `System.Reflection.Metadata` (`PEReader` / `MetadataReader`) is part of the .NET shared framework — `gim-asm` enumerates assembly types **in-box** with no vendored binaries and no `Assembly.LoadFrom` (never loads target assemblies).
- Pester 5/6 is the actively maintained test framework; Pester 3.4 is PS 5.1-era and does not run on pwsh 7.
- The `gim-*` module code itself stays PowerShell 5.1-compatible (portable) — the runtime standard applies to the harness shell and the test suite, not to the module's syntax floor.
