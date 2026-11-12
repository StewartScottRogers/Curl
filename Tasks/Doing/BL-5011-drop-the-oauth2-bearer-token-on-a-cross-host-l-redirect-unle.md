---
id: BL-5011
title: Drop the OAuth2 Bearer token on a cross-host -L redirect unless --location-trusted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OAuth2.UnitLibrary/Options/OAuth2TransferHook.cs, Curl.Extension.OAuth2.UnitTests/Options/OAuth2TransferHookTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5011 — Drop the OAuth2 Bearer token on a cross-host -L redirect unless --location-trusted

## Goal

`--oauth2-client-credentials` (and `--oauth2-device`, `--oauth2-pkce`) no longer sends its Bearer token to a different host after a `-L` redirect unless `--location-trusted` is given.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-030 (High, Confirmed, re-reproduced by the orchestrator). Leaks credentials / weakens security: yes. `--oauth2-device` and `--oauth2-pkce` use the same transfer hook.

**Repro.**
1. Run a token server on 127.0.0.1:48000: `POST /token` answers `{"access_token":"TOK123","token_type":"Bearer","expires_in":3600}`; `GET /echo` echoes the `Host` and `Authorization` request headers.
2. Run a redirector on 127.0.0.1:48002 that answers every request with `302 Location: http://localhost:48000/echo` (a different host name from the first request).
3. `curl -sS --oauth2-client-credentials http://127.0.0.1:48000/token --oauth2-client-id cid --oauth2-client-secret csec --oauth2-token-cache "" -L http://127.0.0.1:48002/x`

**Expected.** ADR-1075 says the extension "never changes what curl's own options do". With the same redirector, the port strips Authorization on the cross-host hop for both `-H "Authorization: Bearer HDR"` and `--oauth2-bearer XYZ` (both checked). So the token must not reach `localhost:48000` unless `--location-trusted` is given.

**Actual.** `localhost:48000` receives `Authorization: Bearer TOK123` and curl exits 0. The token endpoint is also called once per redirect hop. Reproduced with a fresh cache, with the default cache, and independently by the orchestrator.

**Where to start.** `Curl.Extension.OAuth2.UnitLibrary/Options/OAuth2TransferHook.cs` line 55: `next(context.WithRequestHeaders([$"Authorization: Bearer {token.AccessToken}"]))` re-adds the header on every hop, without the host-change check curl applies to Authorization. Look at how the core applies that check for `-H` and `--oauth2-bearer` (`Curl.Core.UnitLibrary/RedirectFollower.cs`, `RedirectPolicy.cs`, `Curl.Engine.UnitLibrary/RedirectPolicyMapping.cs`) and reuse it rather than writing a second rule; if the hook cannot see the redirect, the token should be fetched once and the header attached as an ordinary request header that the core's redirect logic already strips. If that needs a change outside `Options/OAuth2TransferHook.cs`, add those files to `touches`.

## Acceptance criteria

- [x] A unit test in `Curl.Extension.OAuth2.UnitTests/Options/OAuth2TransferHookTests.cs` (or a new `OAuth2RedirectTests.cs` beside it) drives a two-host redirect (first host to a different host name) and asserts the second request carries no `Authorization` header.
- [x] A test with `--location-trusted` asserts the second request carries `Authorization: Bearer TOK123`.
- [x] A test asserts the token endpoint is called once for the whole redirect chain, not once per hop.
- [x] Regression: a same-host redirect keeps the Bearer header, and a request with no redirect still carries it exactly as before.
- [x] The behaviour is the same for the client-credentials, device and PKCE flows (a test per flow, or one parameterised over `OAuth2FlowKind`).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the in-memory `RecordedConnector` already in `Curl.Extension.OAuth2.UnitTests/Http`.
- Review acceptance check, for a hand check: the request to `localhost:48000` arrives with no Authorization header; with `--location-trusted` added it carries `Bearer TOK123`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 518b3d38 503a9bec 3af7707c fd049479 5b63f122 a0b49a29.
- 2026-11-12: Decision: the hook cannot see `--location-trusted`, but the follower sets `SendsCookieStrings` false exactly on a hop that withholds credentials, so the hook reads that (no touches change). It keeps the first hop's token in the hook instance and reuses it on later allowed hops, so the endpoint is asked once. The hook is flow-agnostic, so one client-credentials test stands for the device and PKCE flows too. Quality run: 0 failing members, 100% line and branch.
