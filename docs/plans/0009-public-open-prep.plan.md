# Public open prep (Phase 0 and Phase 1)

Finish the GIMtransfer public-open checklist on this tree. Commits and ready pull requests go to `origin`, which is the private archive `GIMtransferArchive`. The new public repository stays empty until the human push at the end.

This plan does not edit `OPEN_SOURCE_ROADMAP.md` in place. Step E1 copies the leftover items into `README.md` and deletes the roadmap.

## Decisions

- Public builds take `GimBin` 1.0.10 and `GimCrypt` 1.0.10 from nuget.org when the GIMzip projects are absent. A machine with those projects keeps the project reference.
- `TestUI`, `StartCityJson`, and `GIMconvert` use that same pattern. `StartCityJson` and `GIMconvert` reference GimBin only. Do not add GimCrypt to them.
- Delete the in-repo DLL drops: `GimBin/GimBin.dll`, `GimBin/GimBin-old.dll`, `GimCrypt/GimCrypt.dll`, and the `AGENTS.md` files in those two folders.
- Obfuscation of the published packages is ConfuserEx. Do not write BitMono or ConfuserEx into public docs. The roadmap file is deleted, not corrected.
- `ci.yml` runs on pull request `opened`, `synchronize`, and `reopened`, on push to `dev` and `main`, and on `workflow_dispatch`. Remove the `closed` / merged-only trigger.
- NuGet signing stays out. `README.md` records it under Upcoming.
- Azure Artifacts feeds, `dotnet-nuget-setting.yml`, and other Azure package-feed references leave GIMtransfer only. Do not change GIMmanager installer signing.
- CityJson private paths:
  - Delete a file only when the whole file is commented out and that commented code contains a private path.
  - Inventory says that applies to `StartCityJson/ModifyOrUpdateDB.cs` and `StartCityJson/Old/ReadWriteDatabase.cs`. Re-check before deleting. If a file has live code, keep it and scrub it.
  - In files that stay, replace live private paths with the placeholder `replace`, and delete commented-out archived blocks that contain private paths.
  - Known files that stay: `StartCityJson/Program.cs`, `StartCityJson/Old/ProgramOld.cs`, `StartCityJson/ReadCityJson.cs`, `StartCityJson/Classes/GimProperties.cs`.
- Outside CityJson, scrub private paths in comments and in code. Do not delete those files and do not delete the surrounding comment. Known files: `TestUI/Form1.cs`, `GIMtransfer/FileManager/FileManagerPrivate.cs`, `GIMtransfer/read/GIMocctRead/GIMocctReadElements.cs`, `GIMtransfer/read/IFCReader/GimBooleanConvertion.cs`.
- Leave fleet docs, hook tests, historical plans, `.cursor`, `.opencode`, and `.agents` path examples unchanged.
- `pyproject.toml` is local search config. Remove the `authors` name and email. Do not add a personal name or the holding’s legal name.
- Public wording stays “the owner.”
- Push ready pull requests to `origin` (`GIMtransferArchive`). One ready pull request per step, plus the feature-branch pull request into `dev`. Do not open a draft. Do not push any other remote. The human still publishes the new public repository, as new history, after this plan.

## Execution mode

- [x] Verify per step
- [x] Commit per step
- [x] PR per step
- [x] Critic per step

PR-per-step is on. Each step opens a **ready** pull request on `origin` (`GIMtransferArchive`). The feature branch also gets a ready pull request into `dev` before step A1. Do not pass `--draft`. Prefs `pr_per_step: true` applies.

Overnight autopilot (human, 2026-10-16): defer every verify gate and give one report after E1. Do not stop between steps for a human check, and do not offer a handoff. No Jira ticket: the human did not confirm a project or summary, so branch names stay as listed below. Alternative not chosen: stop and ask. A blocker stops the run only when no later step can proceed.

Logged choices:

- A1 build failures on StartCityJson (NU1605), GIMconvert (NU1201), and TestPlatform fixture misses stay unfixed. The critic found they sit on unchanged project references, not on the new package fallback. Alternative not chosen: bump ACadSharp or retarget GIMconvert inside A1.
- D1 StartCityJson build still fails only on that same NU1605. The scrub did not add a compile error. Alternative not chosen: bump ACadSharp inside D1.
- E1 wrap-up found four more living docs that still described in-repo GimBin or GimCrypt DLL drops. Agreed and fixed in E1: `WORKSPACE.md`, `THIRD_PARTY_NOTICES.md`, `GIMtransfer/AGENTS.md`, `StartCityJson/AGENTS.md`. Alternative not chosen: leave those sentences because they were not in the original E1 file list.

## Failure scenarios

- Agent moves to step N+1 while the repo is broken after step N.
- (verify ON, autopilot off) Agent moves to step N+1 without verifying step N with the user.
- This run is overnight autopilot: defer every verify gate. One report after E1.
- (commit-per-step ON) Agent moves to step N+1 without committing step N.
- (commit-per-step ON) Agent commits step N while the repo is broken.
- (PR-per-step ON) Agent moves to step N+1 without a ready pull request for step N on `origin`.
- (critic-per-step ON) Agent moves to step N+1 without running gim-critic on step N.
- Order of operations broken (commit before critic, or a draft pull request, or a pull request before the step commit).
- Agent opens a pull request as a draft, or pushes to a remote other than `origin` (`GIMtransferArchive`).
- Agent commits on `dev`, `main`, or `release/*`.
- Agent edits `OPEN_SOURCE_ROADMAP.md` before step E1, or deletes it before the Upcoming section exists.
- Agent applies the CityJson whole-file delete rule outside `StartCityJson/`.
- Agent scrubs fleet docs, hook tests, or historical plans.
- Agent names ConfuserEx or BitMono in a public doc.

## Branch

Create `feature/public-open-prep` from the current `dev`, push it to `origin` (`GIMtransferArchive`), and open a ready pull request into `dev`. Step branches, each with a ready pull request into the previous branch:

- `public-open-prep/a1-nuget-fallback`
- `public-open-prep/b1-retire-azure`
- `public-open-prep/c1-ci-on-pr-open`
- `public-open-prep/d1-scrub-private-paths`
- `public-open-prep/e1-readme-and-delete-roadmap`

Neighbors GIM-136, GIM-131, GIM-130, GIM-108, and GIM-101 are not this work. Do not reuse them. This run has no Jira key. Do not create a ticket. Pull-request titles (ready, not draft):

- `feature/public-open-prep` → `dev`: `[Docs] Public Open Prep - record the public-open checklist`
- `public-open-prep/a1-nuget-fallback` → `feature/public-open-prep`: `[Feature] Public Open Prep - restore GimBin and GimCrypt from nuget.org when source is absent`
- `public-open-prep/b1-retire-azure` → `public-open-prep/a1-nuget-fallback`: `[Feature] Public Open Prep - remove the Azure Artifacts workflow`
- `public-open-prep/c1-ci-on-pr-open` → `public-open-prep/b1-retire-azure`: `[Feature] Public Open Prep - run CI on open pull requests`
- `public-open-prep/d1-scrub-private-paths` → `public-open-prep/c1-ci-on-pr-open`: `[Feature] Public Open Prep - scrub private paths and the local search author field`
- `public-open-prep/e1-readme-and-delete-roadmap` → `public-open-prep/d1-scrub-private-paths`: `[Docs] Public Open Prep - point public docs at nuget.org and remove the open-source roadmap`

## Manual checklist (human)

Already done:

- [x] Private archive repository is `GIMtransferArchive`. Local `origin` points at it.
- [x] The new `GIMtransfer` GitHub repository exists and has not been pushed.

Before you publish:

- [ ] Confirm the holding’s registered name internally. Leave the public text as “the owner.”
- [ ] Create the GitHub label `component:gimbin` on the new public repository.
- [ ] Confirm `GimBin` 1.0.10 and `GimCrypt` 1.0.10 are still listed on nuget.org.

When this plan is finished and you accept the tree:

- [ ] The ready pull-request chain on `GIMtransferArchive` is this plan’s publish path. Separately, push this tree to the new `GIMtransfer` repository yourself, as new history. Do not point `origin` at that new repository during this plan.

Later, not this plan:

- [ ] NuGet package signing.
- [ ] Credential rotation for anything the archive history still contains.
- [ ] DCO or CLA, issue templates, and detailed contribution rules. Those stay as Upcoming lines until you want them.

## Steps

### Step A1 — nuget fallback and remove DLL drops [critic, verify, commit, pr]

- **Status**: completed
- **Files / symbols**: `TestUI/TestUI.csproj`, `StartCityJson/StartCityJson.csproj`, `GIMconvert/GIMconvert/GIMconvert.csproj`, `TestUI/nuget.config`, `GimBin/` (`GimBin.dll`, `GimBin-old.dll`, `AGENTS.md`), `GimCrypt/` (`GimCrypt.dll`, `AGENTS.md`). `GIMtransfer/GIMtransfer.csproj` already has the package fallback; leave that block as it is.
- **Change**: When the GIMzip project file is absent, reference the nuget.org package instead of a HintPath DLL. TestUI: `GimBin` 1.0.10 and `GimCrypt` 1.0.10. StartCityJson and GIMconvert: `GimBin` 1.0.10 only. Delete `TestUI/nuget.config` so restore uses the repo-root `nuget.config` (nuget.org only). Delete the two DLL drop folders, including `GimBin-old.dll`.
- **Why**: The core project and GIMmanager already restore these packages. The three side projects are the reason the DLLs are still in the tree. TestUI’s own config still points at Azure Artifacts, so the package switch and that config go together.
- **Validate**: This workstation has GIMzip, so `dotnet build` exercises the project-reference branch. Confirm the `!Exists` groups contain `PackageReference` and no `HintPath`. Then build:
  - `dotnet build "GIMtransfer/GIMtransfer.csproj" -c Debug -p:Platform=x64`
  - `dotnet build "TestUI/TestUI.csproj" -c Debug -p:Platform=x64`
  - `dotnet build "StartCityJson/StartCityJson.csproj" -c Debug -p:Platform=x64`
  - `dotnet build "GIMconvert/GIMconvert/GIMconvert.csproj" -c Debug -p:Platform=x64`
  - `dotnet test "TestPlatform/TestPlatform.csproj" -c Debug -p:Platform=x64`
- **Commit message**: `A1: restore GimBin and GimCrypt from nuget.org when source is absent`

### Step B1 — retire Azure Artifacts [critic, verify, commit, pr]

- **Status**: completed
- **Files / symbols**: `.github/workflows/dotnet-nuget-setting.yml`, `AGENTS.md` (the key-entry bullet for that workflow). Any other GIMtransfer hit of `pkgs.dev.azure.com`, `AZURE_ARTIFACTS`, or `AzureFeed`, except `OPEN_SOURCE_ROADMAP.md`.
- **Change**: Delete the Azure Artifacts workflow. Remove references to it. Leave the roadmap file for step E1. Do not change GIMmanager.
- **Why**: The public story is nuget.org. The workflow still publishes with `AZURE_ARTIFACTS_PAT`.
- **Validate**: Search GIMtransfer for `pkgs.dev.azure.com`, `AZURE_ARTIFACTS`, and `AzureFeed`. The only remaining hits are in `OPEN_SOURCE_ROADMAP.md`. `dotnet build "GIMtransfer/GIMtransfer.csproj" -c Debug -p:Platform=x64` still succeeds.
- **Commit message**: `B1: remove the Azure Artifacts workflow from GIMtransfer`

### Step C1 — run CI when a pull request opens [critic, verify, commit, pr]

- **Status**: completed
- **Files / symbols**: `.github/workflows/ci.yml` (`on`, and the job-level merged condition).
- **Change**: Trigger on push to `dev` and `main`, on `workflow_dispatch`, and on pull request `opened`, `synchronize`, and `reopened`. Remove `pull_request` type `closed` and the `merged == true` job condition. Keep the existing restore, build, and test steps. Do not add signing or Azure secrets.
- **Why**: Tests need to run on the open pull request and on each new commit pushed to it. A run that starts only after merge does not gate review.
- **Validate**: Read `.github/workflows/ci.yml` and confirm the trigger block and that the job is not gated on `merged`.
- **Commit message**: `C1: run CI on open pull requests`

### Step D1 — scrub private paths [critic, verify, commit, pr]

- **Status**: completed
- **Files / symbols**: `StartCityJson/ModifyOrUpdateDB.cs`, `StartCityJson/Old/ReadWriteDatabase.cs`, `StartCityJson/Program.cs`, `StartCityJson/Old/ProgramOld.cs`, `StartCityJson/ReadCityJson.cs`, `StartCityJson/Classes/GimProperties.cs`, `TestUI/Form1.cs`, `GIMtransfer/FileManager/FileManagerPrivate.cs`, `GIMtransfer/read/GIMocctRead/GIMocctReadElements.cs`, `GIMtransfer/read/IFCReader/GimBooleanConvertion.cs`, `pyproject.toml` (`authors`).
- **Change**: In `StartCityJson/`, delete a file only when every code line is a comment and a private path is in that commented file. Otherwise keep the file, replace live private path strings with `replace`, and delete commented-out archived blocks that contain a private path. Outside `StartCityJson/`, replace the private path text in comments and in string literals with `replace`. Keep the file and the rest of the comment. Remove the `authors` key from `pyproject.toml`. Do not add a replacement identity.
- **Why**: The public tree must not ship personal paths or the local search config’s author identity. CityJson archived files are the only place a whole file goes away.
- **Validate**: Search the touched files for `C:\Users\`. No hits. `pyproject.toml` has no `authors` key. Build the projects this step still leaves in place:
  - `dotnet build "StartCityJson/StartCityJson.csproj" -c Debug -p:Platform=x64`
  - `dotnet build "TestUI/TestUI.csproj" -c Debug -p:Platform=x64`
  - `dotnet build "GIMtransfer/GIMtransfer.csproj" -c Debug -p:Platform=x64`
- **Commit message**: `D1: scrub private paths and the local search author field`

### Step E1 — public docs and delete the roadmap [critic, verify, commit, pr]

- **Status**: completed
- **Files / symbols**: `README.md`, `PROPRIETARY_COMPONENTS.md`, `AGENTS.md`, `docs/guides/0005-package-consumer.md`, `OPEN_SOURCE_ROADMAP.md`.
- **Change**: Add `## Upcoming` to `README.md` immediately before `## Ecosystem`, with these lines only:
  - NuGet package signing for packages pushed by `release.yml`. Public pull-request CI signs nothing.
  - Optional later: DCO or CLA.
  - Optional later: issue templates, a pull request template, and detailed contribution rules.
  - In `PROPRIETARY_COMPONENTS.md`, replace the in-repo DLL “near term / later nuget.org” lines. State that public builds restore GimBin and GimCrypt from nuget.org when the GIMzip projects are absent, that this repo pins 1.0.10, and that a checkout with those projects uses the project references. Keep the `component:gimbin` issue instruction.
  - In `AGENTS.md`, stop describing local DLL fallbacks for GimBin and GimCrypt. Describe the project-reference or nuget.org package fallback. Remove `GimBin` and `GimCrypt` from the “don’t modify shipped binaries in wrapper folders” line. Leave `GIMocctWrapper (DLL)` and `GIMtwin (DLL)` on that line. Point the GimZip path spelling at `GIMzip`, matching the csproj condition.
  - In `docs/guides/0005-package-consumer.md`, replace “repository-local fallback DLLs” for those proprietary components with the nuget.org package fallback. Leave the OCCT local-DLL sentence as it is.
  - Delete `OPEN_SOURCE_ROADMAP.md` in this same step, after the Upcoming section is in the tree.
- **Why**: After the DLL drops are gone, the public docs must describe nuget.org. The roadmap’s remaining items live in README, then the roadmap file goes away.
- **Validate**: `OPEN_SOURCE_ROADMAP.md` is absent. `README.md` contains `## Upcoming` and the three leftover items. `PROPRIETARY_COMPONENTS.md` does not tell the reader to use an in-repo GimBin or GimCrypt DLL. Search for `BitMono` and `dotnet-nuget-setting.yml` and confirm no hits.
- **Commit message**: `E1: point public docs at nuget.org and remove the open-source roadmap`

## Out of scope

- GIMzip, GIMmanager, CoreWebApi, GIMfront, and nasFlaskApp.
- Implementing NuGet signing, or editing `release.yml` beyond leaving it unsigned.
- `Obfuscate.xml` (old Dotfuscator config). Leave it.
- Fleet docs, hook tests, and historical plans.
- Pushing to the new public `GIMtransfer` repository, creating the GitHub label, and writing the holding’s legal name into the tree. Ready pull requests to `origin` (`GIMtransferArchive`) are in scope.

## Review

After execution, run `/review-with-critic granular` once more after the last per-step critic pass, with the full modified file list and `Steps implemented: A1, B1, C1, D1, E1`. This wrap-up pass weights findings toward cross-step coherence.
