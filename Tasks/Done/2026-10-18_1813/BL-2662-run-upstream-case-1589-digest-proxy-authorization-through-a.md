---
id: BL-2662
title: Run upstream case 1589: Digest Proxy-Authorization through a proxy on two ports (after BL-2660)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2660]
touches: [Libcurl, Libcurl.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2662 — Run upstream case 1589: Digest Proxy-Authorization through a proxy on two ports (after BL-2660)

## Goal

Upstream case 1589 (lib1589) passes through the runner and is on the ratchet.

## Context

Found by BL-2543, which ported lib1589. The runner reports `differs at byte 217 (line 8): expected "Proxy-Authorization: Digest username=\"silly\", realm=\"weirdorealm\", nonce=\"12345\", uri=\"/path/1589\", response=\"9a547f8fa81cff330c68095603f3819e\"\r\n", got "Accept: */*\r\n"` - the same missing Digest answer to a proxy's 407 as case 1588 (BL-2660); lib1589 changes `CURLOPT_PROXYPORT` between its two requests where lib1588 changes `CURLOPT_PROXY`. Note: on Windows the case needs `!SSPI`; check how the runner judges that feature before expecting it to run.

## Acceptance criteria

- [x] Upstream case 1589 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the reason it cannot run on Windows is recorded under Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: BL-2660 already fixed the Digest answer; what was left was `CURLOPT_PROXYPORT`, which the engine never read, so the second request went to the first proxy port and the case saw four requests on one server. `ProxyUrlParser.TryParse` and `ProxySelector.TrySelect` take `portWhenTextGivesNone` (a port in the proxy text wins, as curl documents), which `TransferProxySelection` passes from `EasyProxyOptions.ProxyPort`. Added `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests` to `touches`: no other task in Doing touched them.
- Case 1589 runs on Windows: the runner judged `!SSPI` fine. Measure-CodeQuality (Core, Engine): 0 failing members. Fast tests green.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 34787d22 48be2d2b 3d836d5e.
- 2026-10-18: Doing -> Done. Case 1589 passes and joins the ratchet: CURLOPT_PROXYPORT now gives the port of proxy text that names none
