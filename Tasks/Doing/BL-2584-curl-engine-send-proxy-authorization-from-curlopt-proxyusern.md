---
id: BL-2584
title: Curl.Engine: send Proxy-Authorization from CURLOPT_PROXYUSERNAME and PROXYPASSWORD in a CONNECT (test503)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2602]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2584 — Curl.Engine: send Proxy-Authorization from CURLOPT_PROXYUSERNAME and PROXYPASSWORD in a CONNECT (test503)

## Goal

A Libcurl handle with `CURLOPT_PROXYUSERNAME` and `CURLOPT_PROXYPASSWORD` sends `Proxy-Authorization: Basic` in its `CONNECT`, as libcurl 8.22.0 does.

## Context

Found by BL-2527 running test503 through the ported `lib503`: `<verify><proxy>` differs at line 3, expected `Proxy-Authorization: Basic dGVzdCUyMDppbmclNDE=`, got `Proxy-Connection: Keep-Alive`. The program sets `CURLOPT_PROXY`, `CURLOPT_HTTPPROXYTUNNEL`, `CURLOPT_PROXYUSERNAME` `test%20` and `CURLOPT_PROXYPASSWORD` `ing%41` (sent as given, not URL-decoded).

## Acceptance criteria

- [ ] test503 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 2): Added `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` to `touches`, since the first criterion adds test503 to it. BL-2528, in Doing, also touches that file, so the task went back to Backlog before any code edit.
- Likely fix: `ManagedEasyTransfer.TransferSettingsOf` (`Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs`, about line 1414) passes `ProxyCredentials: null`. Build it from `options.Authentication.ProxyUserName` and `ProxyPassword` (and `ProxyUserPassword` split at its first colon), used as given, not URL-decoded.
- 2026-10-17 (lane 1): Fix done and left in the stash: `ManagedEasyTransfer.TransferSettingsOf` now passes `ProxyCredentialsOf(options.Authentication)` (`CURLOPT_PROXYUSERNAME`/`PROXYPASSWORD` win over `CURLOPT_PROXYUSERPWD` split at its first colon, used as given; split in `SplitAtFirstColon` to keep CA1502 under 11), with tests in `Curl.Engine.UnitTests/ManagedEasyTransferTests.ProxyCredentials.cs` (7 pass) and `503` added to `PassingUpstreamCases.txt`. With it test503's `Proxy-Authorization` line matches; it then differs at `<verify><proxy>` line 6, the missing `[DISCONNECT]`. Recording the close in the proxy connection's starting recording only when a tunnel is relayed did not help (the `%HTTPPORT` tunnel is not relayed), so that is BL-2602. Next claim: apply the stash, wait for BL-2602, rerun `CURL_UPSTREAM_CASE=503`, run Measure-CodeQuality for Curl.Engine.UnitLibrary, then the fast tests.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 6c7087d7 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Backlog. touches Curl.Conformance.UnitTests/PassingUpstreamCases.txt, which BL-2528 in Doing also touches
- 2026-10-17: Backlog -> Doing. Criteria at claim: 6c7087d7 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Backlog. Proxy-Authorization fix done (in stash); test503 still lacks the proxy log's [DISCONNECT], filed as BL-2602
- 2026-10-17: Backlog -> Doing. Criteria at claim: 6c7087d7 904b57ec 3d836d5e.
