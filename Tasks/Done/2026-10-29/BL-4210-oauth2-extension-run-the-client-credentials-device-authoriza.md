---
id: BL-4210
title: OAuth2 extension: run the client-credentials, device authorization and PKCE authorization code flows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4211, BL-4212, BL-4213, BL-4209]
touches: [Curl.Extension.OAuth2.UnitLibrary/Flows, Curl.Extension.OAuth2.UnitTests/Flows, Curl.Extension.OAuth2.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4210 — OAuth2 extension: run the client-credentials, device authorization and PKCE authorization code flows

## Goal

The OAuth2 extension gets an access token by the client-credentials grant (RFC 6749 §4.4), the device authorization grant (RFC 8628) and the authorization code grant with PKCE (RFC 6749 §4.1, RFC 7636) through a loopback redirect (RFC 8252 §7.3), trying the cache and a refresh first.

## Context

Fourth part of BL-4161 (split for size). Builds on BL-4212 (wire format), BL-4213 (HTTP client), BL-4209 (cache) and BL-4211 (the hook's connector and listener). Device flow: print the `verification_uri` and `user_code` (or `verification_uri_complete`) to stderr, poll every `interval` seconds on the injected `TimeProvider`, add 5 seconds on `slow_down`, stop on `expired_token`, `access_denied` or after `expires_in` (RFC 8628 §3.3, §3.5). PKCE: print the authorization URL to stderr, accept one redirect on `http://127.0.0.1:<port>/`, check `state` (RFC 6749 §10.12), exchange the code with the verifier. Failures map to curl exit codes decided by ADR.

## Acceptance criteria

- [x] Unit tests with fake connections, a fake listener and a fake `TimeProvider` check each flow against the named RFC sections, including polling, `slow_down` and a wrong `state`.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the exit codes and what the flows print.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run (split for size).
- 2026-10-29 (lane 9): Built `Flows/` (ADR-1045): `IOAuth2TokenFlow`, `OAuth2FlowRequest`, `OAuth2FlowResult`, `OAuth2TokenEndpoint`, the three flows, `OAuth2RedirectReceiver`/`OAuth2Redirect` and `OAuth2CachedTokenSource` (cache and refresh first, then the flow, caching its token). The library references only `Curl.Protocol.Abstractions`, so the flows take `IConnector` (through `OAuth2HttpClient`) and `IConnectionListener` directly; BL-4161's hook passes `TransferHookServices`' two in.
- Defaults taken: an `interval` below 1 polls every second (a 0 would spin); the state is 16 random octets (128 bits, RFC 6749 §10.10), the verifier 32; the redirect head is read to at most 16 KiB; the first of a repeated redirect parameter wins; a redirect `error` is reported as a message (67) rather than an `OAuth2ErrorResponse`, since the error-code table is private to `Wire/`. A cached token is given back without `expires_in`: its lifetime stays in the cache.
- Touches widened to the library's `CLAUDE.md`, which said the flows arrive in BL-4161; no task in Doing on origin/work/dark-factory names it.
- Measure-CodeQuality.ps1 -Library Curl.Extension.OAuth2.UnitLibrary: 100% line, 100% branch, 141 members, 0 failing, worst CRAP 10 (after splitting `OAuth2Redirect.ParseRequestLine` and `OAuth2TokenEndpoint.PostAsync`). Tests are platform-neutral (fakes only, a temp cache directory); CI on Linux and macOS is left to the shift's watch.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5bc17d39 e1c80d50 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. The OAuth2 extension gets a token by client credentials, device authorization and PKCE authorization code flows, cache and refresh first
