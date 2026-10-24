# GIM Jira workflow — available transitions (reference)

Raw transitions list for the GIM project workflow (captured 2026-08-12 from the Jira API for GIM-127; board doc: `docs/jira-board-specifications.md`).

```json
{
  "transitions": [
    {
      "id": "2",
      "name": "In Review",
      "to": {
        "self": "https://gim.atlassian.net/rest/api/3/status/10011",
        "description": "",
        "iconUrl": "https://gim.atlassian.net/",
        "name": "In Review",
        "id": "10011",
        "statusCategory": { "id": 4, "key": "indeterminate", "colorName": "yellow", "name": "In Progress" }
      },
      "hasScreen": false, "isGlobal": true, "isInitial": false, "isAvailable": true, "isConditional": false, "isLooped": false
    },
    {
      "id": "11",
      "name": "To Do",
      "to": {
        "self": "https://gim.atlassian.net/rest/api/3/status/10010",
        "description": "",
        "iconUrl": "https://gim.atlassian.net/",
        "name": "To Do",
        "id": "10010",
        "statusCategory": { "id": 2, "key": "new", "colorName": "blue-gray", "name": "To Do" }
      },
      "hasScreen": false, "isGlobal": true, "isInitial": false, "isAvailable": true, "isConditional": false, "isLooped": false
    },
    {
      "id": "21",
      "name": "In Progress",
      "to": {
        "self": "https://gim.atlassian.net/rest/api/3/status/3",
        "description": "This issue is being actively worked on at the moment by the assignee.",
        "iconUrl": "https://gim.atlassian.net/images/icons/statuses/inprogress.png",
        "name": "In Progress",
        "id": "3",
        "statusCategory": { "id": 4, "key": "indeterminate", "colorName": "yellow", "name": "In Progress" }
      },
      "hasScreen": false, "isGlobal": true, "isInitial": false, "isAvailable": true, "isConditional": false, "isLooped": false
    },
    {
      "id": "31",
      "name": "Done",
      "to": {
        "self": "https://gim.atlassian.net/rest/api/3/status/10002",
        "description": "",
        "iconUrl": "https://gim.atlassian.net/",
        "name": "Done",
        "id": "10002",
        "statusCategory": { "id": 3, "key": "done", "colorName": "green", "name": "Done" }
      },
      "hasScreen": false, "isGlobal": true, "isInitial": false, "isAvailable": true, "isConditional": false, "isLooped": false
    },
    {
      "id": "3",
      "name": "Branch created",
      "to": {
        "self": "https://gim.atlassian.net/rest/api/3/status/3",
        "description": "This issue is being actively worked on at the moment by the assignee.",
        "iconUrl": "https://gim.atlassian.net/images/icons/statuses/inprogress.png",
        "name": "In Progress",
        "id": "3",
        "statusCategory": { "id": 4, "key": "indeterminate", "colorName": "yellow", "name": "In Progress" }
      },
      "hasScreen": false, "isGlobal": false, "isInitial": false, "isAvailable": true, "isConditional": false, "isLooped": false
    }
  ]
}
```

## Usage

- `acli jira workitem transition --key GIM-<n> --status "<exact name from the table>" --yes`
- There is no read-only transition-list flag in acli; when the workflow changes, re-capture this list (Jira UI → workflow, or `GET /rest/api/3/issue/<key>/transitions`) and update both this file and `docs/jira-board-specifications.md`.
