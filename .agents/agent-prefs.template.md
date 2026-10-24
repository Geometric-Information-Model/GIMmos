# GIM agent prefs (template)
# Copy to agent-prefs.md (gitignored) and edit.

## Gates
- verify_per_step: true
  # When false, orchestrator may continue without a per-step user verify gate.
- commit_per_step: true
- pr_per_step: true
- critic_per_step: granular   # off | end | granular

## Runtime
- architecture: windows      # windows => PowerShell; linux => bash
- default_dev_branch: dev

## gim-tools
# 25 verbs across nine groups. The seven fleet verbs (gim-preflight, gim-pr,
# gim-jira-search, gim-jira-create, gim-jira-transition, gim-wrapup-gather,
# gim-wrapup-respond) are role-scoped in agent frontmatter; never add them to
# the shared global allowlist. Group references are generated.

## gim-tracker
- tracker: jira

## opencode-sync
- prefix: gim
