# acli command reference

A concise cheat sheet for the Atlassian CLI (`acli`) Jira commands used by this
skill. This file is **generic** — it contains no repo-specific board IDs, column
names, or JQL. For repo-specific values, see the "Repo configuration" pointer in
[`SKILL.md`](../SKILL.md).

Official docs:

- [`jira`](https://developer.atlassian.com/cloud/acli/reference/commands/jira.md)
- [`jira auth`](https://developer.atlassian.com/cloud/acli/reference/commands/jira-auth.md)
- [`jira workitem`](https://developer.atlassian.com/cloud/acli/reference/commands/jira-workitem.md)
- [`jira sprint`](https://developer.atlassian.com/cloud/acli/reference/commands/jira-sprint.md)
- [`jira sprint list-workitems`](https://developer.atlassian.com/cloud/acli/reference/commands/jira-sprint-list-workitems.md)
- [Agent skills specification](https://agentskills.io/specification)

---

## Read-only vs mutating split

| Read-only (safe to run freely) | Mutating (require user consent first) |
|--------------------------------|---------------------------------------|
| `auth status` | `auth login` / `logout` / `switch` |
| `workitem search` | `workitem transition` |
| `workitem view` | `workitem create` |
| `sprint list-workitems` | `workitem edit` |
| `board view` | `workitem assign` |
| `board search` | `workitem comment` |

All mutating commands always need explicit user consent.

---

## auth

```bash
acli jira auth login --web                 # one-time interactive browser login
acli jira auth login --site <site> --email <email> --token <token>  # token login
acli jira auth status                      # verify you are authenticated
acli jira auth logout
acli jira auth switch                      # switch between authenticated sites
```

No `acli jira ...` command works until you have authenticated once.

---

## workitem search (read-only)

```bash
acli jira workitem search --jql "<JQL>" [flags]
```

| Flag | Purpose |
|------|---------|
| `-j`, `--jql` | JQL query string |
| `-f`, `--fields` | comma-separated fields (default: `issuetype,key,assignee,priority,status,summary`) |
| `--filter` | use a saved filter |
| `--json` | JSON output |
| `--csv` | CSV output |
| `--count` | return only the match count |
| `-l`, `--limit` | max results |
| `--paginate` | page through all results |
| `-w`, `--web` | open the search in a browser |

---

## workitem view (read-only)

```bash
acli jira workitem view <KEY> [--fields ...] --json
```

| Flag | Purpose |
|------|---------|
| `-f`, `--fields` | comma-separated fields (default: `key,issuetype,summary,status,assignee,description`) |
| `--json` | JSON output |
| `-w`, `--web` | open the work item in a browser |

---

## workitem transition (mutating — consent required)

```bash
acli jira workitem transition --key "<KEY>" --status "<status name>" [--yes]
```

| Flag | Purpose |
|------|---------|
| `-k`, `--key` | comma-separated work item keys |
| `-s`, `--status` | **free-text target status name** (the workflow status, not necessarily the board column casing) |
| `--jql` | transition all items matching a JQL query |
| `--filter` | transition all items in a saved filter |
| `--json` | JSON output |
| `--ignore-errors` | continue on per-item errors |
| `-y`, `--yes` | skip the confirmation prompt |

The status value is a workflow status **name**, whose casing can differ from the
board column display. Confirm the exact strings for your repo in its board doc.

---

## workitem create (mutating — consent required)

```bash
acli jira workitem create --summary "<title>" --project "<KEY>" --type "<type>" --assignee "<@me|email|default>" [flags]
```

| Flag | Purpose |
|------|---------|
| `-s`, `--summary` | work item summary/title |
| `-p`, `--project` | project key |
| `-t`, `--type` | issue type (e.g. `Task`, `Bug`, `Story`) |
| `-a`, `--assignee` | `@me`, an email, or `default` |
| `-l`, `--label` | label(s) |
| `-d`, `--description` | description text |
| `--from-file` / `--from-json` | create from a file/JSON payload |
| `--generate-json` | print a JSON template |
| `--parent` | parent issue key (for sub-tasks) |
| `-e`, `--editor` | open an editor for the body |

**Create assigns `@me` by default.** Ask the user whether to self-assign
(`--assignee @me`) or assign a specific email before running create. Leave it
unassigned (`--unassigned`) only when the user explicitly requests that
exception.

On project GIM, `labels` is rejected at create (screen-level). Send it only
optionally and expect the rejection; retry with `"labels": null`.

### PowerShell and project-screen pitfalls

- Use PowerShell single quotes around complete JQL expressions:

  ```powershell
  acli jira workitem search --jql 'project = GIM AND summary ~ "auth"' --fields 'key,summary,status,assignee' --json
  ```

  Do not use Bash-style `\"` escaping inside a PowerShell double-quoted
  argument; those backslashes reach Jira and can make the JQL invalid.
- Quote `@me` in PowerShell: `--assignee '@me'`. An unquoted `@me` can be
  parsed as an invalid email value.
- Do **not** pass `--yes` to `workitem create`. `create --help` does not expose
  that flag. `--yes` is command-specific; it is valid for the verified
  `workitem assign` command and may be valid for transition, but check that
  command's help first.
- Some Jira project screens reject the normal create form because the CLI
  submits unsupported fields such as `labels`, even when no label was
  requested. Do not loop on the same command. Run `acli jira workitem
  create --generate-json`, edit the generated schema, and use
  `--from-json`.
- The generated JSON must use the schema printed by this installed `acli`
  version (including its ADF description shape). Set the assignee to the
  current user's account identifier in the payload and set unsupported
  optional fields such as `labels` to `null` when the generated schema
  requires them. Do not invent a REST payload shape.
- Verify assignment after every create. A payload with a null assignee can
  override a CLI `--assignee '@me'` argument. If needed:

  ```powershell
  acli jira workitem assign --key GIM-<n> --assignee '@me' --yes --json
  acli jira workitem view GIM-<n> --fields 'key,summary,status,assignee' --json
  ```

- `--from-json` expects the installed CLI's generated work-item format, not
  arbitrary Jira REST JSON. If it returns `json: invalid format`, regenerate
  the template and compare the payload shape before trying again.

---

## workitem edit / assign (mutating — consent required)

```bash
acli jira workitem edit --key "<KEY>" [-a|--assignee <@me|email>] [--remove-assignee] [-y]
acli jira workitem assign --key "<KEY>" --assignee "<@me|email>" [--jql ...] [-y]
```

---

## sprint (read-only)

```bash
acli jira sprint list-workitems [flags]
```

| Flag | Purpose |
|------|---------|
| `-b`, `--board` | board ID (see [SKILL.md § Repo configuration](../SKILL.md#repo-configuration)) |
| `-s`, `--sprint <id>` | sprint ID; required together with `--board` (`--board` alone exits 1: `required flag(s) "sprint" not set`) |
| `--json` | JSON output |
| `-w`, `--web` | open in browser |

Default discovery flow: resolve the active sprint with
`acli jira board list-sprints --id 9 --state active --json`, then list its
items with `acli jira sprint list-workitems --sprint <id> --board 9` (both
flags required), then `workitem view GIM-<n>` for detail. Skip sprint listing
when the user supplies a ticket key or asks for backlog/JQL search.

Official docs: [`jira sprint`](https://developer.atlassian.com/cloud/acli/reference/commands/jira-sprint.md),
[`jira sprint list-workitems`](https://developer.atlassian.com/cloud/acli/reference/commands/jira-sprint-list-workitems.md).

---

## board (read-only)

```bash
acli jira board list-sprints --id <id> [--state active|future|closed] [--json]
acli jira board view --id <id> [--json]
acli jira board search          # find boards
```

`board list-sprints` is the only sprint command that takes `--state`; use it to
resolve the active sprint before `sprint list-workitems --sprint <id>`.
