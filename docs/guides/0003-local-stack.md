# Local stack runbook

Plan 0005 wires GIMmanager and CoreWebApi local development without changing shipped server behavior. Run each process independently so Visual Studio F5 / Hot Reload continues to work.

## Prerequisites

1. Install Laragon and run MariaDB 10.5.18.
2. Create the local database expected by your configuration, using `gimtest` as the placeholder database name.
3. In CoreWebApi, copy the committed secret template to its ignored local filename and fill in local database credentials and a signing key.

The signing key must match GIMmanager's `JwtAuthentication:Secret` so desktop requests can authenticate against CoreWebApi. Keep real values in the ignored local file.

## Start CoreWebApi against Laragon

Open the CoreWebApi solution in Visual Studio and choose the `LocalDb` launch profile. It runs in `Development` on `https://localhost:7009` and selects `appsettings.secret.local.json`.

The existing CoreWebApi `https` profile remains the default remote-development path and continues to select `appsettings.secret.json`.

MariaDB must answer during CoreWebApi startup because EF detects the server version at boot. A successful process start does not mean the empty database has the application schema; seed work is deferred.

## Start GIMmanager

Open GIMmanager's solution and debug its WinForms project with the `LocalApi` profile. It sets `GIM_API_TARGET=local`, targets CoreWebApi at `https://localhost:7009/`, and keeps local requests on the test-shaped path.

GIMmanager embeds its SignalR host in the WinForms process. Do not also start `GIM.SignalRHub.API`; its standalone profiles are not the path used by GIMmanager. The local hub remains `http://localhost:4300`.

## Known gaps

- No SQL seed is included. Login and CRUD require the future seed work.
- External file services and other external endpoints retain their existing paths.
- This plan does not change SMTP, SendGrid, LinkedIn, uploads, or deployment pipelines.
