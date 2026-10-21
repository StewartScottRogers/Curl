---
id: BL-2837
title: Stand in for the DoH debug variables so upstream cases 2100, 2101 and 2117 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.DohDebugStandIn.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitLibrary/UpstreamStandInFronts.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2837 — Stand in for the DoH debug variables so upstream cases 2100, 2101 and 2117 run

## Goal

Upstream cases 2100, 2101 and 2117 (DNS-over-HTTPS) run in the harness with the debug build's DoH and happy-eyeballs variables stood in for, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 has the three `excluded | debug-build-only`; on Windows they are skipped with `Curl lacks the feature Debug`.

- 2100 ("HTTP GET using DoH (with HTTPS RR)", servers `http` and `https`, features `Debug,DoH,IPv6`) sets `CURL_DBG_HE_AAAA_AWAIT_MS=60000`: the debug build holds back the AAAA answer so the A answer wins the race. Its `<strippart>` removes the family byte so the order of the two DoH POSTs does not matter.
- 2101 ("DoH without response", `Debug,DoH`) sets nothing but needs the feature.
- 2117 ("HTTP GET using DoH with mandatory HTTPS-RR response", `Debug,DoH,IPv6,ECH,HTTPSRR`) sets nothing; `HTTPSRR` and `ECH` are in `UpstreamCurlPlatform`'s common features.

Read `lib/doh.c`, `lib/hostip.c` and `lib/happy-eyeballs`-related `DEBUGBUILD` blocks (`CURL_DBG_*`) at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0) to see why 2101 and 2117 need `Debug` (a debug-only trace or deterministic request id). Curl's DoH resolver is `Curl.Networking.UnitLibrary/DohDnsResolver.cs` with `DohResponseReader` and `DohTraceLines`; the delay for the AAAA answer needs the injected `TimeProvider` (never `Thread.Sleep`, root CLAUDE.md). Stand-in in the style of ADR-0687/0678/0698: `PlatformFor` reports `Debug` to a case that names `DoH` and either sets one of the `CURL_DBG_*` variables or needs a debug-only effect, and the harness gives the engine the stand-in; it belongs to the harness. Add the folder of any library you must change to `touches` in the same commit.

## Acceptance criteria

- [ ] `PlatformFor` reports `Debug` to the three cases and to no DoH case that needs none (list the ones that run today in Notes); a unit test pins the rule.
- [ ] The `CURL_DBG_HE_AAAA_AWAIT_MS` stand-in has a unit test on a manual `TimeProvider` showing the AAAA answer is delayed by that many milliseconds and the A answer is used first.
- [ ] 2100, 2101 and 2117 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or the first difference is in Notes and a follow-up task is filed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 51e1080a 3a040ea3 a6e38886 7bd8d990 620ebead.
