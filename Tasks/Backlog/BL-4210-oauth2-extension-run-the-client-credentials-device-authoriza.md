---
id: BL-4210
title: OAuth2 extension: run the client-credentials, device authorization and PKCE authorization code flows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4211, BL-4212, BL-4213, BL-4209]
touches: [Curl.Extension.OAuth2.UnitLibrary/Flows, Curl.Extension.OAuth2.UnitTests/Flows]
requirement: none
created: 2026-10-29
completed:
---
# BL-4210 — OAuth2 extension: run the client-credentials, device authorization and PKCE authorization code flows

## Goal

The OAuth2 extension gets an access token by the client-credentials grant (RFC 6749 §4.4), the device authorization grant (RFC 8628) and the authorization code grant with PKCE (RFC 6749 §4.1, RFC 7636) through a loopback redirect (RFC 8252 §7.3), trying the cache and a refresh first.

## Context

Fourth part of BL-4161 (split for size). Builds on BL-4212 (wire format), BL-4213 (HTTP client), BL-4209 (cache) and BL-4211 (the hook's connector and listener). Device flow: print the `verification_uri` and `user_code` (or `verification_uri_complete`) to stderr, poll every `interval` seconds on the injected `TimeProvider`, add 5 seconds on `slow_down`, stop on `expired_token`, `access_denied` or after `expires_in` (RFC 8628 §3.3, §3.5). PKCE: print the authorization URL to stderr, accept one redirect on `http://127.0.0.1:<port>/`, check `state` (RFC 6749 §10.12), exchange the code with the verifier. Failures map to curl exit codes decided by ADR.

## Acceptance criteria

- [ ] Unit tests with fake connections, a fake listener and a fake `TimeProvider` check each flow against the named RFC sections, including polling, `slow_down` and a wrong `state`.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the exit codes and what the flows print.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run (split for size).

## Log

- 2026-10-29: Created.
