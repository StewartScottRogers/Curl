---
id: BL-2928
title: Run upstream case 2100 through the named-case test and record whether the DoH POSTs now reach sws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.NamedCase.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DohDebugStandIn.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/CurlCommandRunnerDialingDohTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2928 — Run upstream case 2100 through the named-case test and record whether the DoH POSTs now reach sws

## Goal

`CURL_UPSTREAM_CASE=2100` runs through the named-case test instead of ending `skipped: Curl lacks the feature Debug`, and the first difference (if any) is recorded.

## Context

BL-2919 routed `--doh-url` through the harness's dialer, but its criterion 3 could not be checked: the named-case test skips case 2100 for the `Debug` feature (`UpstreamCaseScreening.UnsupportedFeature`), which the ratchet's harness stands in for (ADR-0497). Find why the named-case run lacks that stand-in and give it the same one.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=2100` is no longer skipped for `Debug`; the first difference, if any, is recorded in Notes and is no longer `Could not resolve host`.

## Notes

- The Context's premise was wrong: the ratchet skipped 2100 for `Debug` too, since both runs take the platform from `PlatformFor`, and no `DebugStandInRules` entry matched it. Added `RunsCurlWithDohUrl` (`UpstreamConformanceTests.DohDebugStandIn.cs`, one line in `DebugStandIns.cs`): Debug for a case naming the `DoH` feature whose command runs `--doh-url` - 2100, 2101 and 2117, not the DoH unit programs 1650, 1655 and 1658. This is the rule BL-2837's first claim chose; BL-2837 could not be the blocker, as it waits on BL-2919, which waits on this task. Unit tests pin the rule and the three vendored cases.
- With the skip lifted, 2100 still failed `curl: (6) Could not resolve host: foo.example.com` with no request at sws: BL-2919 routed `--doh-url` through the dialer only for the libtest engine (`EngineComposition.CreateTransports`); the curl tool's dialing runner (`CurlComposition.CreateRunner`, which `RunCurlAsync` uses) still handed every option group the given resolver. Added `CurlComposition.DialingDohResolverOf`: under `--doh-url` a DoH resolver whose connector dials the runner's `ITcpDialer` and resolves the DoH host through the runner's resolver (after the `CURL_DNS_SERVER` choice). Tests: `CurlCommandRunnerDialingDohTests`. Added `Curl.Console/CurlComposition.cs` and that test file to `touches`; no task in Doing on `origin/work/dark-factory` (BL-2912, BL-2926) names either.
- First difference now (Windows, 2026-10-21): `<verify><protocol> differs at byte 115 (line 5): expected "Content-Length: 47", got "Content-Length: 33"` - the DoH POSTs reach sws and the GET completes (6-byte body), but Curl's first DoH POST is the A/AAAA query, not the HTTPS-RR query `%if HTTPSRR` expects first. That is BL-2920's work.
- `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 17acd8f2.
- 2026-10-21: Doing -> Done. Case 2100 runs under the named-case test (DoH Debug stand-in), and the curl tool's dialing runner sends --doh-url POSTs to sws; first difference is the HTTPS-RR query (BL-2920)
