---
id: BL-2252
title: Reuse the Digest challenge with an increasing nonce-count on a same-host --location redirect (test1286)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2252 — Reuse the Digest challenge with an increasing nonce-count on a same-host --location redirect (test1286)

## Goal

`curl -u auser:apasswd --location --digest URL`, answered 401 Digest then a 302 to a path on the same host, sends `Authorization: Digest ... nc=00000002 ...` on the redirected request, as curl does, so upstream case 1286 passes.

## Context

Found by BL-2238 (2026-10-14), which made the harness run test1286's Digest-field sorting strip program. With the strip carried out, test1286's first two requests match; the third, `GET /12860001` after the 302, has no `Authorization:` line from Curl, where curl 8.22.0 sends a Digest header reusing the first challenge's nonce with `nc=00000002` (curl's `lib/vauth/digest.c` keeps `digest->nc` and increments it per use; https://github.com/curl/curl/pull/1251). Start with the redirect follow in `Curl.Networking.UnitLibrary` and `DigestAuthenticator` / `DigestChallenge` in `Curl.Authentication.UnitLibrary`. Debug it with `CURL_UPSTREAM_CASE=1286` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [x] A same-host `--location` redirect after a successful Digest exchange sends Digest credentials with the challenge's nonce and the nonce-count increased by one, pinned by a focused unit test in the fixed library's `.UnitTests` project (ADR-0482).
- [x] test1286 passes and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `Measure-CodeQuality.ps1` shows no member outside a gate for each library changed; build and fast tests green.

## Notes

- 2026-10-14 (lane 2): The fix is not in Networking or Authentication: `DigestAuthenticator.RepeatAuthorization` already counts a sent answer's `nc` on (BL-869). What was missing was carrying the answer from hop to hop, the way `AuthSchemePicked` travels (BL-1819). `touches` changed once, before any edit, to Curl.Protocol.Abstractions.UnitLibrary, Curl.Core.UnitLibrary and its UnitTests, Curl.Protocol.Http.UnitLibrary and its UnitTests, and Curl.Conformance.UnitTests. No other task was in Doing on origin/work/dark-factory.
- The design: `TransferReport.DigestAuthorizationSent` is the Digest value the hop's last origin request sent. `RedirectFollower` passes it as `HttpRequestOptions.KeptDigestAuthorization` to a hop that may be sent credentials (same origin, or `--location-trusted`) and clears it otherwise. When no value was made before a challenge, `HttpProtocolHandler` sends the kept value again through `IHttpAuthenticator.RepeatAuthorization`, but only with a credential and with Digest allowed. That gives the same nonce and cnonce, the next `nc` and the new uri. No ADR: this matches curl's kept `digest->nc`, so there was no choice to make.
- Focused tests (ADR-0482): `HttpProtocolHandlerTests.KeptDigestAuthorization.cs` (with the `Fakes/RepeatingAuthenticator` fake) and `RedirectFollowerTests.FollowAsync_HopsDigestAuthorizationSent_IsKeptOnlyForAHopThatMayBeSentCredentials`.
- Measured: test1286 passes and is listed. Fast tests: 37 projects, 0 failed. Measure-CodeQuality for the three libraries: 100% line and 100% branch coverage, 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. A same-host --location redirect after a Digest exchange resends Digest with the kept nonce and nc counted on; test1286 passes
