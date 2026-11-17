---
id: BL-5377
title: Drop -b and -H Cookie cookies on a cross-origin SignalR negotiate redirect
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Transport, Curl.Extended.Protocol.SignalR.UnitTests, Documentation/Planning/Decisions/ADR-1286-a-signalr-negotiate-redirect-to-another-origin-drops-the-users-authorization.md]
requirement: none
created: 2026-11-17
completed:
---
# BL-5377 — Drop -b and -H Cookie cookies on a cross-origin SignalR negotiate redirect

## Goal

A SignalR negotiate redirect to another origin no longer forwards the user's `-b` or `-H "Cookie:"` cookies, so they cannot leak to a server-chosen origin (High: session-cookie leak, in clear after a `signalrs://` start), unless `--location-trusted` is given.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-073 (High, Confirmed: Windows 2 runs per variant, Linux once; re-reproduced by the orchestrator). Area: signalr / signalrs negotiate, Windows and Linux. Relates to ADR-1286 (a negotiate redirect to another origin drops the user's Authorization), which says the redirect behaves like `-L` without `--location-trusted`; that ADR must be updated to name the cookies too.

**Repro.**
1. An HTTP server on 127.0.0.1:43010 whose `POST /hub/negotiate` answers 200, `Content-Type: application/json`, body `{"url":"http://127.0.0.1:43011/evilhub"}`.
2. A logging HTTP server on 127.0.0.1:43011 that answers 404 and logs request headers.
3. `curl -sS -m 8 -b "sid=BCOOKIE" -d '{"type":1,"target":"Echo","arguments":["x"]}' signalr://127.0.0.1:43010/hub`
4. The same with `-H "Cookie: HDRCOOKIE=1"` instead of `-b`.
5. TLS: serve the negotiate reply over TLS on localhost:43012 with body `{"url":"http://localhost:43011/evilhub"}`; `curl -sS -m 8 --cacert ca.pem --ssl-no-revoke -H "Cookie: HDRCOOKIE=1" -d '{"type":1,"target":"Echo","arguments":["x"]}' signalrs://localhost:43012/hub`

**Expected.** ADR-1286 says a cross-origin negotiate redirect behaves "like `-L` without `--location-trusted`". The port's own `-L` (and curl) send neither a `-b` cookie nor a custom `-H Cookie:` to another host. The request to 43011 carries no `Cookie` unless `--location-trusted` is given.
**Actual (PORT).** 43011 receives `POST /evilhub/negotiate?negotiateVersion=1` with `Cookie: sid=BCOOKIE` (step 3) or `Cookie: HDRCOOKIE=1` (steps 4 and 5; step 5 is cleartext HTTP after `signalrs://`). Exit 8 `SignalR: The negotiate request failed with HTTP status 404.` Authorization is correctly dropped.
**Where to start.** `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRNegotiator.cs` `FieldsForRedirect` (line ~46) removes only `Authorization`; `Transport/HandshakeHeaderFields.cs` adds `Cookie` from `-b` (line ~48) and `-H`. Also remove `Cookie` there and in the WebSocket upgrade after a cross-origin redirect.

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.SignalR.UnitTests` (a new file) prove a cross-origin negotiate redirect sends no `Cookie` header when the cookie came from `-b`, and none when it came from `-H "Cookie: ..."`.
- [ ] A test proves the same for a `signalrs://` to `http://` redirect, and for the WebSocket upgrade that follows a cross-origin redirect.
- [ ] A test proves that with `--location-trusted` the cookie is sent, and that a same-origin reply (`{"url":"http://127.0.0.1:43010/hub2"}`) keeps it.
- [ ] A test proves `Authorization` is still dropped on a cross-origin redirect.
- [ ] ADR-1286 is updated to say cookies from `-b` and `-H Cookie:` are dropped too.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: after steps 3 to 5 the 43011 log shows the `/evilhub/negotiate` request with no `Cookie` header.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: after steps 3-5, the 43011 log shows the `/evilhub/negotiate` request with no `Cookie` header. With `--location-trusted` the cookie is sent; with a same-origin reply (`{"url":"http://127.0.0.1:43010/hub2"}`) it is kept.
- ADR-1286 lives in `Documentation/Planning/Decisions/`; mark a changed decision "Decided by Claude under Stewart's delegation".

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 599214a8 ec394e0f 7caee8f6 02b52215 4b4a463e c1cf21f9 ef72c97d a0b49a29.
