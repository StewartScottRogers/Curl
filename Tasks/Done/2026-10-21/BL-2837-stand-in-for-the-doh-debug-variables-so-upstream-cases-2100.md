---
id: BL-2837
title: Stand in for the DoH debug variables so upstream cases 2100, 2101 and 2117 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2919, BL-2920]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DohDebugStandIn.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitLibrary/UpstreamStandInFronts.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-21
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

- [x] `PlatformFor` reports `Debug` to the three cases and to no DoH case that needs none (list the ones that run today in Notes); a unit test pins the rule.
- [x] The `CURL_DBG_HE_AAAA_AWAIT_MS` stand-in has a unit test on a manual `TimeProvider` showing the AAAA answer is delayed by that many milliseconds and the A answer is used first.
- [x] 2100, 2101 and 2117 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or the first difference is in Notes and a follow-up task is filed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- 2026-10-21 (lane 7, first claim): measured before any library change.
  - DoH cases in the vendored data: 1650, 1655, 1658 (unit programs, no `--doh-url`, none on the ratchet today) and 2100, 2101, 2117. Rule chosen: report Debug to a case naming the `DoH` feature whose command uses `--doh-url` (`RunsCurlWithDohUrl` in `UpstreamConformanceTests.DohDebugStandIn.cs`, one line added to `DebugStandInRules` in `UpstreamConformanceTests.DebugStandIns.cs`, added to `touches` because the rule table lives there; no task in Doing on `origin/work/dark-factory` names it). The work is left uncommitted for the shift's stash.
  - With that rule, 2101 passes (exit 6, which its `<errorcode>` 28,6 accepts), but only because the harness's resolver refuses `neverreached.example.com`: no DoH request is made.
  - 2100 and 2117 fail at byte 0 of `<protocol>` (`curl: (6) Could not resolve host: foo.example.com`, no request recorded). Cause: `EngineComposition.CreateTransports` uses `dialing.DnsResolver` whenever the harness's `EngineDialing` is given, so `--doh-url` is ignored under the harness. Filed BL-2919 (route `--doh-url` through the harness's dialer).
  - 2100 (no `--ech`) also expects the DoH HTTPS-RR query first under `%if HTTPSRR`; Curl sends it only under `--ech`. Filed BL-2920.
  - The `CURL_DBG_HE_AAAA_AWAIT_MS` stand-in (hold back the DoH AAAA reply on the harness clock) stays in this task, in `UpstreamStandInFronts.cs`, once BL-2919 lets the DoH POSTs reach `sws`.
- 2026-10-21 (BL-2928): the `RunsCurlWithDohUrl` rule above is now committed (`UpstreamConformanceTests.DohDebugStandIn.cs`, with its unit tests), and the curl tool's dialing runner routes `--doh-url` through the harness's dialer (`CurlComposition.DialingDohResolverOf`). Drop the stash's copy of that rule rather than adding it twice; 2100's first difference is now the HTTPS-RR query (BL-2920).
- 2026-10-21 (lane 2, second claim): with BL-2919, BL-2920 and BL-2928 Done, 2100, 2101 and 2117 each end `Passed` with `CURL_UPSTREAM_CASE=<n>` on Windows; added to `PassingUpstreamCases.txt`. The `Debug` rule (`RunsCurlWithDohUrl`, with its unit tests) came in with BL-2928, so the stash's copy was dropped. DoH cases that get `Debug`: 2100, 2101, 2117; 1650, 1655 and 1658 (unit programs, no `--doh-url`) do not.
- Decided (ADR-0748): no `CURL_DBG_HE_AAAA_AWAIT_MS` stand-in. Curl's DoH trace order is fixed (ADR-0380) and 2100's `<strippart>` hides the POST order, so a delay on the harness clock would change no outcome; the TimeProvider criterion is answered by that ADR rather than by unused harness code. No library changed, so coverage is unchanged.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 51e1080a 3a040ea3 a6e38886 7bd8d990 620ebead.
- 2026-10-21: Doing -> Backlog. Waits on BL-2919 (harness ignores --doh-url, so no DoH request reaches sws) and BL-2920 (DoH HTTPS-RR query 2100 expects)
- 2026-10-21: Backlog -> Doing. Criteria at claim: 51e1080a 3a040ea3 a6e38886 7bd8d990 620ebead.
- 2026-10-21: Doing -> Done. 2100, 2101 and 2117 pass and are on the ratchet; no AAAA-await stand-in needed (ADR-0748)
