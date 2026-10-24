# RFC 0006 — Local authentication cookie handling

**Status:** Proposed

**Related:** Plan 0005 local stack process wiring; `CoreWebApi/GIM.WebAPI/GIM.WebAPI/Controllers/LinkedInController.cs`; `GIMmanager` authentication configuration.

## Summary

Local authentication is blocked by a production-scoped cookie domain. CoreWebApi's LinkedIn callback creates `authToken` with `Domain=.gimtransfer.com`, so a browser running on `localhost` rejects the cookie before the browser client can read it or use it for authenticated requests.

The cookie-domain change is small. A complete reliable fix also needs frontend token-handling cleanup because the current flow reads a JavaScript-visible cookie and converts it into an authorization header through several inconsistent paths.

## Problem

CoreWebApi sets the authentication cookie in `LinkedInController.cs` with `Secure=true`, `SameSite=None`, `HttpOnly=false`, and `Domain=.gimtransfer.com`. The `Path` attribute is omitted.

The browser rejects a cookie for `.gimtransfer.com` when the response origin is `localhost`. Setting `Domain=localhost` is not a valid replacement; local development needs a host-only cookie created by omitting the `Domain` attribute.

The local HTTPS topology already satisfies the cookie's secure transport requirement. The local browser client uses `https://localhost:3000`, and CoreWebApi uses `https://localhost:7009`.

## Observed behavior

- A browser client hosted on a GIM domain can receive a cookie scoped to `.gimtransfer.com`.
- A browser client hosted on `localhost` cannot receive that production-scoped cookie, whether it calls local CoreWebApi or a remote API.
- Guest authentication already returns a JWT in JSON and does not depend on this cookie.
- The browser client reads `authToken` with JavaScript in `AuthInitializer` and then sends an authorization header.
- Existing frontend paths contain additional inconsistencies: one listener uses `JWT` instead of the standard `Bearer` scheme, Axios subscribes to a separate Redux store instance, and cookie payload/reducer field names are not consistent.

## Proposal

### Phase 1: make cookie attributes target-aware

Add one CoreWebApi cookie-options helper or equivalent narrow code path.

- In local development, omit `Domain` so the browser creates a host-only cookie.
- In deployed environments, retain `Domain=.gimtransfer.com`.
- Set `Path=/` explicitly so the cookie is available across the application.
- Retain `Secure=true` and `SameSite=None` for the existing HTTPS flow.
- Retain `HttpOnly=false` temporarily because the current frontend handoff reads the cookie from JavaScript.
- Never set `Domain=localhost`.

The local branch should be selected from the existing environment/profile configuration rather than from a new deployment environment name. Unchanged production behavior must remain the default.

### Phase 2: normalize token handoff

Choose one authentication contract and use it consistently for all login paths.

The preferred long-term contract is for the API to return a token response and for the browser client to keep the token in its established auth state, then send `Authorization: Bearer <token>`. This avoids requiring JavaScript access to an authentication cookie, but requires coordinated API and client changes.

If the current cookie handoff remains temporarily, normalize the client to one Redux store, one token field, and the standard `Bearer` scheme before removing the JavaScript-readable cookie.

## Compatibility and security

The Phase 1 change is backward-compatible for deployed environments because the production domain and secure attributes remain unchanged. A host-only cookie is deliberately limited to the local API host and is not shared with unrelated local services.

`HttpOnly=false` exposes the token to JavaScript and should be treated as transitional. Changing it immediately would break the current cookie-to-header handoff, so the secure-cookie migration belongs in Phase 2 rather than being combined silently with the local-targeting fix.

## Scope

In scope: CoreWebApi cookie attribute selection, explicit cookie path, focused tests, and coordinated token-handoff cleanup in the browser client.

Out of scope: database seed, external file services, deployment pipeline changes, OAuth provider changes, password-reset behavior, and a new environment enum.

## Acceptance criteria

1. Local HTTPS login receives a host-only `authToken` cookie without a `Domain` attribute.
2. Deployed login continues to emit the existing `.gimtransfer.com` domain.
3. The cookie has `Path=/` in both modes.
4. Local login can complete without manually copying a cookie into browser storage.
5. Authenticated follow-up requests use the standard `Bearer` scheme.
6. Focused automated tests cover local and deployed cookie attributes and the selected token-handoff contract.
7. Existing remote-development and production defaults remain unchanged when local targeting is not explicitly selected.

## Alternatives considered

- Setting `Domain=localhost`: rejected because browsers do not accept it as a valid domain cookie.
- Always omitting `Domain`: workable for one host, but removes the existing cross-subdomain behavior needed by deployed browser/API hosts.
- Relying on `withCredentials` alone: insufficient while the browser rejects the cookie domain and the API primarily authenticates bearer tokens.
- Making the cookie `HttpOnly` immediately: safer in isolation, but incompatible with the current JavaScript cookie reader until Phase 2 is implemented.
