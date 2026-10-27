# Setup: gim-tools module auto-load (PSModulePath)

One-time per-machine setup so the `gim-*` toolkit verbs load automatically in
every fresh pwsh — agents stop needing a per-command import line.

## Prerequisites

- PowerShell 7+ (module runtime standard).
- Node.js >= 20 on `PATH`. `gim-wrapup-gather` is a PowerShell shim around the
  dependency-free helper `scripts/helpers/gim-wrapup-gather.mjs`; Node is
  already required by the pinned CodeGraph CLI. Agents never call `node`
  directly. No npm packages are installed for this verb.

## Why

Each harness bash call is a **fresh pwsh process**; import state never
persists across calls. Before this setup, every agent command that used a
`gim-*` verb had to carry the import line
(`Import-Module -Force ./.agents/skills/gim-tools/scripts/GIMTools/GIMTools.psd1
-DisableNameChecking`) — noisy and easy to forget.

The module now lives at `.agents/skills/gim-tools/scripts/GIMTools/`
(**folder name == module name** - PowerShell auto-load requires that), and the
parent `scripts/` folder is registered in the **User PSModulePath**. When a
pwsh starts and a `gim-*` command is invoked, PowerShell finds and imports the
module automatically. It exports 25 verbs in nine groups; the seven fleet verbs
are role-scoped in agent frontmatter and remain outside the shared allowlist.

> Permission note: auto-load does **NOT** bypass harness permissions. The
> config allowlist still gates every `gim-*` verb exactly as before; the
> plugin/hook deny rules are unchanged. Auto-load only removes the import
> step.

## How — run once per machine

From the repo root:

```powershell
pwsh -NoProfile -File ./.agents/skills/gim-tools/scripts/setup.ps1
```

What the script does (user-scope, no admin — **verify-then-register** —
nothing is written when the module file is missing):

1. Takes its own folder (`.agents/skills/gim-tools/scripts/`) as the folder to
   register.
2. Verifies `GIMTools/GIMTools.psd1` under that folder **first**. If the
   module file is missing, it warns and exits — the saved path and the
   current session are left untouched (distinguishes a stale existing entry
   from a checkout that does not look like the gim-tools repo layout).
3. Splits `PSModulePath` on the platform separator (`;` on Windows, `:` on
   Linux and macOS) and adds the folder **only if it is not already an exact
   entry** (no duplicates).
4. Persists, then updates the current session and imports the module.
   Windows writes User `PSModulePath` with
   `[Environment]::SetEnvironmentVariable(..., "User")` (`HKCU\Environment`).
   Linux and macOS write a marked block in the CurrentUserAllHosts profile
   (`~/.config/powershell/profile.ps1`) that prepends the folder and imports
   `GIMTools`, plus a marked `PSModulePath` export in `~/.profile` for login
   shells. The User environment-variable target is not available there.

Then **restart opencode and Cursor** so harness pwsh processes inherit the new
environment (environment changes are read at process start).

## What changed after setup

- Fresh pwsh resolves the module: `Get-Module -ListAvailable GIMTools`.
- `gim-list .` (and every other `gim-*` verb) runs with **no import line**.
- The import-line allow entries remain in the harness configs for explicit
  loads, but are no longer required per shell.

## Trust note

Registering the folder in the **User** PSModulePath means GIMTools auto-loads
in **every pwsh you run** as this user — not just agent harness shells. Only
run `setup.ps1` on machines where you **trust the checkout** (the module's
verbs are the same data-only verbs already allowlisted in harness configs, but
auto-load removes the per-shell import gate). The toolkit directory
(`.agents/skills/gim-tools/**`) stays **deny-write for agents** — this setup
only changes module resolution, not agent file permissions.

## Harness permission note

Cursor's Auto-run allowlist is global. The first time the version-controller
runs a fleet verb (`gim-preflight`, `gim-pr`, `gim-wrapup-gather`,
`gim-wrapup-respond`) or the tracker runs `gim-jira-search`,
`gim-jira-create`, or `gim-jira-transition`, Cursor may ask once; approve that
one prompt for the role. `gh stack view` is read-only and may also ask once.

## Troubleshooting

- **Stale entry after a repo move.** The saved path contains the old
  checkout, but the module file is missing there. On Windows, `setup.ps1`
  detects the entry already present + missing module file and emits a warning
  naming the stale path. Fix: run `setup.ps1` from the new checkout, or remove
  the stale entry from `HKCU\Environment\PSModulePath` (Settings → Environment
  Variables → User variables → `PSModulePath`). On Linux and macOS, re-run
  `setup.ps1` from the new checkout; it replaces the marked profile block.
- **Module not resolving.** On Windows, check the User PSModulePath entry
  (`[Environment]::GetEnvironmentVariable('PSModulePath','User')`). On Linux
  and macOS, check the marked block in `~/.config/powershell/profile.ps1`.
  The checkout must still contain `scripts/GIMTools/GIMTools.psd1`, and the
  harness must be restarted after setup. A `pwsh -NoProfile` process does not
  read that profile; login shells pick up `~/.profile` instead.
- **Unregister.** On Windows, remove the entry from User `PSModulePath` in
  `HKCU\Environment` (or Settings → Environment Variables), then restart the
  harnesses. On Linux and macOS, delete the `# gim-tools:begin` /
  `# gim-tools:end` blocks from `~/.config/powershell/profile.ps1` and
  `~/.profile`. Delete only that block; keep the rest.
