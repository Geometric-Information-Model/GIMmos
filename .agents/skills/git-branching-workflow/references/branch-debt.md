# Branch debt — current vs target

Agents should use **this repo's actual default** when opening PRs until debt is cleared. The target model is always: `dev` ← features, `main` ← releases.

Verified from `git pull` of `dev` on 2026-09-28 for GIMmos, GIMfront, CoreWebApi, GIMmanager, and GIMzip. nasFlaskApp, GIMdeploy, and GIMocctWrapper were not pulled in that pass.

| Repo | Integration (today) | Production/main (today) | Target |
|------|---------------------|-------------------------|--------|
| GIMmos | `dev` | `main` | `dev` + `main` |
| GIMfront | `dev` | `main` | `dev` + `main` |
| CoreWebApi | `dev` | `main` | `dev` + `main` |
| GIMmanager | `dev` | `main` | `dev` + `main` |
| GIMzip | `dev` | `master` | `dev` + `main` |
| nasFlaskApp | `main` | `main` | `dev` + `main` |
| GIMdeploy | `main` | `main` | `dev` + `main` |
