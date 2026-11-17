---
id: BL-5311
title: Drop credentials from a SignalR negotiate redirect to another origin and refuse a signalrs-to-http downgrade
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRNegotiator.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpRequestWriter.cs, Documentation/Conformance/Extended/SignalR.md, Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRNegotiatorRedirectCredentialTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5311 — Drop credentials from a SignalR negotiate redirect to another origin and refuse a signalrs-to-http downgrade

## Goal

A SignalR negotiate redirect to a different scheme, host or port never carries the user's `-u`, `--oauth2-bearer` or `-H Authorization` credentials (unless `--location-trusted`), a `signalrs://` start never sends them over cleartext `http://`, and a redirect that carries an `accessToken` still replaces them with `Authorization: Bearer <token>`.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-062 (High, Confirmed: Windows 4 variants x 2, Linux once, re-reproduced by the orchestrator). Area: signalr / signalrs negotiate, Windows and Linux. A credential leak to another origin and a TLS-to-cleartext downgrade.

**Repro.**
1. HTTP server on 127.0.0.1:37010: `POST /hub/negotiate` answers `200`, `Content-Type: application/json`, body `{"url":"http://127.0.0.1:37011/evilhub"}`.
2. Logging HTTP server on 127.0.0.1:37011 that answers 404 to everything and logs request headers. (Write both servers in PowerShell or a C# file-based app, never Python.)
3. Run:
   - `curl -sS -u alice:SECRETPW -d '{"type":1,"target":"Echo","arguments":["x"]}' signalr://127.0.0.1:37010/hub`
   - the same with `--oauth2-bearer SECRETBEARER` instead of `-u`
   - the same with `-H "Authorization: SECRETHDR"` instead of `-u`
4. TLS variant: serve the same negotiate reply over TLS on localhost:37012, then `curl -sS --cacert ca.pem --ssl-no-revoke -u alice:SECRETPW -d '{"type":1,"target":"Echo","arguments":["x"]}' signalrs://localhost:37012/hub`.

**Expected.** `Documentation/Conformance/Extended/SignalR.md` rows TP-Negotiate-4/5 cover a redirect only when it carries an `accessToken`, which replaces the user's credentials. The port drops Authorization on an origin change everywhere else (`-L`, `--oauth2-client-credentials`, `--dpop`). The second negotiate, to an origin the server picked, must carry no `-u`, `--oauth2-bearer` or `-H Authorization` credentials (unless `--location-trusted`), and must never send them over cleartext after a `signalrs://` start.

**Actual.** 37011 receives `POST /evilhub/negotiate?negotiateVersion=1` with `Authorization: Basic YWxpY2U6U0VDUkVUUFc=` (alice:SECRETPW), `Bearer SECRETBEARER`, or `SECRETHDR`. In the TLS variant the Basic credentials go over plain HTTP. curl then exits 8 with `SignalR: The negotiate request failed with HTTP status 404.` (With `accessToken` present, Authorization is correctly replaced by `Bearer <token>`.)

**Where to start.** `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRNegotiator.cs`, `NegotiateAsync` (line 32) calls `ExchangeAsync(redirectUrl, response.AccessToken, ...)` with the same `requestFields`; `Transport/HttpRequestWriter.cs` (`Combine`, about line 36) removes Authorization only when `accessToken` is not null. When the redirect URL's scheme, host or port differs, remove Authorization from `requestFields` (and refuse an https-to-http change) unless `--location-trusted` is given. Find how `--location-trusted` reaches the extension (the options it is handed) before deciding; the library's `CLAUDE.md` has its rules. If other files in the library must change, add them to `touches`.

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRNegotiatorRedirectCredentialTests.cs` (scripted connection stand-ins, no network) prove that on a negotiate redirect to a different host, port or scheme with no `accessToken`, the second negotiate request carries no `Authorization` header, for a Basic (`-u`), a Bearer (`--oauth2-bearer`) and a raw `-H Authorization` source.
- [ ] A test proves a same-origin redirect with no `accessToken` keeps the credentials, and one with `accessToken` sends `Authorization: Bearer <token>` (as `{"url":"http://127.0.0.1:37011/evilhub","accessToken":"SRVTOKEN"}` must).
- [ ] A test proves a redirect from `https` to `http` never sends credentials; the Log records whether the redirect is refused or sent without them.
- [ ] A test proves `--location-trusted` keeps the credentials across the origin change.
- [ ] The Log records the review's hand check: after each step 3/4 command the 37011 log shows the `/evilhub/negotiate` request with no `Authorization` header; with the `accessToken` reply it shows `Authorization: Bearer SRVTOKEN`.
- [ ] `Documentation/Conformance/Extended/SignalR.md` gets a row for the new behaviour (add it to `touches`), or the Log says why it needs none.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- An extension is tested against its specification, not curl. Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: d39264c2 18f7902b 70f80408 e4dea110 cc813b22 e9cca56d a0b49a29.
