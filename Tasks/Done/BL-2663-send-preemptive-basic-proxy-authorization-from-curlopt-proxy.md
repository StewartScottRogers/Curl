---
id: BL-2663
title: Send preemptive Basic Proxy-Authorization from CURLOPT_PROXYUSERPWD through Libcurl (case 1648)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2663 — Send preemptive Basic Proxy-Authorization from CURLOPT_PROXYUSERPWD through Libcurl (case 1648)

## Goal

`CURLOPT_PROXYUSERPWD` without `CURLOPT_PROXYAUTH` sends `Proxy-Authorization: Basic` on the first request through an HTTP proxy, and none once it is cleared, so upstream case 1648 (lib1648) passes.

## Context

Found by BL-2543, which ported lib1648. The runner reports `differs at byte 53 (line 3): expected "Proxy-Authorization: Basic dmljdGltOnNlY3JldA==\r\n", got "Accept: */*\r\n"`: the proxy credentials set through Libcurl are not sent. The proxies are `firstproxy:<port>` and `secondproxy:<port>`, resolved by `CURLOPT_RESOLVE`. Compare BL-2660 (Digest, case 1588), which may share the cause.

## Acceptance criteria

- [x] Upstream case 1648 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Case 1648 already passed when claimed: BL-2660 (ADR-0677) made `CURLOPT_PROXYUSERPWD` reach the proxy endpoint's credentials for every handle, and the default proxy scheme is Basic, so the first request sends `Proxy-Authorization: Basic` and the cleared second one sends none. This run lists 1648 on the ratchet and adds `PerformAsync_ProxyUserPasswordSetThenClearedWithNoProxyAuth_PutsTheCredentialsOnTheFirstProxyOnly` in `Curl.Engine.UnitTests` (ADR-0482). No production code changed. Case is HTTP-only with no paths, so it is platform-neutral.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 2ddb26db 48be2d2b 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream case 1648 passes and is on the ratchet; Engine test pins preemptive proxy credentials set then cleared
