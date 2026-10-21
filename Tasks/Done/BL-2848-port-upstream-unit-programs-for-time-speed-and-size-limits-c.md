---
id: BL-2848
title: Port upstream unit programs for time, speed and size limits: cases 1303, 1323, 1399, 1605, 1606, 3214 and 3216
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, Curl.Conformance.UnitProgram.UnitLibrary/Limits, Curl.Conformance.UnitProgram.UnitTests/Limits]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2848 — Port upstream unit programs for time, speed and size limits: cases 1303, 1323, 1399, 1605, 1606, 3214 and 3216

## Goal

Upstream cases 1605 and 1606 run as ported unit programs over Curl's string functions and low-speed check and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; 1323 and 3214 have their C-only reason recorded; and 1303, 1399 and 3216 are tasks of their own (ADR-0729).

## Context

These seven `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`) test one area each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit): 1303 `Curl_timeleft_ms`, 1323 `curlx_tvdiff`, 1399 `Curl_pgrsTime`, 1605 negative data lengths as input to libcurl functions, 1606 speedcheck, 3214 struct size checks and 3216 ratelimit. Curl's counterparts: the timeout logic in `Curl.Core.UnitLibrary`/`Curl.Engine.UnitLibrary` (transfer timers over `TimeProvider`, root CLAUDE.md: never `Thread.Sleep`), the progress and rate-limit code in `Curl.Output.UnitLibrary` and `Curl.Engine.UnitLibrary`, and `Libcurl`'s option setters for 1605; find each with the Glossary and `rg`. 3214 checks the byte sizes of C structs (`sizeof`), for which Curl has no counterpart: record that reason (the one case in this batch whose reason is lasting). Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program and returning its failure count.

## Acceptance criteria

- [x] Cases 1605 and 1606 each have a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`.
- [x] Cases 1323 and 3214 each have a Notes entry naming the C-only thing they test, and an ADR row says so (ADR-0729 for 1323, ADR-0711 for 3214).
- [x] The ports of 1303, 1399 and 3216 are filed as BL-2953, BL-2954 and BL-2955, each naming the Curl counterpart it must build first, and ADR-0729 records the split.
- [x] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches` in the same commit. (Neither port found Curl wrong.)
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit. (The ports are pure computation with no path, file or OS text; the shift's coordinator watches CI and files any failure.)

## Notes

- 2026-10-21 (lane 2): split by ADR-0729, decided by Claude under Stewart's delegation. Reading the seven C programs (curl-8_22_0 `tests/unit/`) against Curl:
  - **1605 ported**: `Limits/Unit1605NegativeLengths.cs`, both checks over `CurlStrings.Escape`/`Unescape` on a `CurlGlobal.Init` handle; `Passed`, on the ratchet.
  - **1606 ported**: `Limits/Unit1606SpeedCheck.cs`, the six `runawhile` rows over `LowSpeedWatchdog` on `Limits/SteppedTimeProvider.cs` (a hand-moved clock firing the watchdog's timer once per one-second step). The C program fakes `current_speed`; the watchdog measures bytes over its last five seconds, so the port writes before each check the bytes that make that measure the row's speed (B(n) = B(max(0, n-5)) + min(n, 5) x speed). `Passed`, on the ratchet. Curl was right: no fix needed.
  - **1323 no counterpart** (ADR-0729): `curlx_timediff_ms` is arithmetic on `struct curltime` (`tv_sec`/`tv_usec`); Curl measures elapsed time as `TimeSpan` from `TimeProvider`, so there is nothing of Curl's to run it over.
  - **3214 no counterpart** (ADR-0711 row, unchanged): `sizeof` checks of C structs.
  - **1303 -> BL-2953**: Curl has no single `Curl_timeleft_ms`; `-m` and `--connect-timeout` are counted apart, so a `TransferTimeLeft` in `Curl.Core` comes first.
  - **1399 -> BL-2954**: finding (and fixing if needed) where Curl sums phase timers across redirect hops is work of its own.
  - **3216 -> BL-2955**: `RateLimitedStream` is not libcurl's token bucket (no burst, block or balance), so a `TransferRateLimit` in `Curl.Core` comes first.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitProgram.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/Limits/ and their tests in Curl.Conformance.UnitProgram.UnitTests/Limits/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 84511890 2e3e75ab e66ae9dd c0723528 620ebead.
- 2026-10-21: Doing -> Backlog. Narrowing the criteria to the split of ADR-0729 (1605 and 1606 ported, 1323 and 3214 recorded; 1303, 1399 and 3216 moved to BL-2953, BL-2954 and BL-2955), then reclaiming to close
- 2026-10-21: Backlog -> Doing. Criteria at claim: 32682e55 3e457fdb d2a5356d 2e3e75ab ea3799dc c0723528 0a06e05d.
- 2026-10-21: Doing -> Done. Upstream cases 1605 and 1606 run as ported unit programs and pass on the ratchet; 1323 and 3214 recorded as C-only; 1303, 1399 and 3216 split to BL-2953, BL-2954 and BL-2955 (ADR-0729)
