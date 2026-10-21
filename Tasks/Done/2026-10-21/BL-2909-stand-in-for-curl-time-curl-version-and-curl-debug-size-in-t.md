---
id: BL-2909
title: Stand in for CURL_TIME, CURL_VERSION and CURL_DEBUG_SIZE in the curl tool's --write-out so upstream cases 970, 972 and 1981 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/DebugWriteOutStandIn.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.WriteOutClockStandIn.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2909 — Stand in for CURL_TIME, CURL_VERSION and CURL_DEBUG_SIZE in the curl tool's --write-out so upstream cases 970, 972 and 1981 run

## Goal

Upstream cases 970, 972 (`--write-out '%{json}'`) and 1981 (`%time{...}`) pass in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.

## Context

Split from BL-2835. 970 and 972 set `CURL_TIME=13`, `CURL_DEBUG_SIZE=4019` and `CURL_VERSION=curl-unit-test-fake-version`; 1981 sets `CURL_TIME=1754037103` and `LC_TIME=C` and expects `Time: 01/Aug/2025 08:31:43.037103 +0000 UTC%s 1754037103`: the debug build's fixed clock carries `CURL_TIME % 1000000` as its microseconds (read upstream's `lib/curl_trc.c`/`lib/timeval.c` and `src/tool_writeout.c` at tag `curl-8_22_0` for the exact rule, and how `CURL_DEBUG_SIZE` and `CURL_VERSION` reach the JSON). The runner's `-w` clock is `CurlCommandRunner`'s `timeProvider`; the dialing `CurlComposition.CreateRunner` overload passes none today. Check `%Z` under the Windows C runtime dialect: if Windows' answer differs, pin each platform in its own test or keep 1981 off the ratchet on that platform. Report `Debug` to such a case with one rule in `DebugStandInRules`, its predicate in its own partial file.

## Acceptance criteria

- [x] `PlatformFor` reports `Debug` to a case setting `CURL_TIME` without the HSTS feature, `CURL_VERSION` or `CURL_DEBUG_SIZE`, and a unit test pins it.
- [x] A unit test pins each stand-in: the fixed `-w` clock with its microseconds, the version string and the debug size in `%{json}`.
- [x] Cases 970, 972 and 1981 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage.

## Notes

- Upstream's rules (curl-8_22_0): `src/tool_writeout.c` `outtime` fixes `%time{}` at `CURL_TIME` seconds with `CURL_TIME % 1000000` microseconds and rewrites `%Z`/`%z` to `UTC`/`+0000` itself, so 1981's line is the same on every platform (no OSCondition needed). `lib/getinfo.c` answers `CURL_TIME` for the local port, every `*_TIME_T` (microseconds) and both speeds, and `CURL_DEBUG_SIZE` for the header and request sizes; `lib/version.c` returns `CURL_VERSION` from `curl_version()`. That is all 970 and 972 needed.
- Design (ADR-0732): `Curl.Console/DebugWriteOutStandIn.cs` holds the three values; `CurlCommandRunner` takes it as `debugWriteOut` (the `%time{}` clock is a `FixedWallClock`, so timings and waits keep the system clock); the dialing `CreateRunner(honoursDebugWriteOutVariables: true)`, set only by the harness, builds it. The rule is `SetsWriteOutDebugVariable` in `UpstreamConformanceTests.WriteOutClockStandIn.cs`, with the stand-in's unit tests beside it.
- Touches widened: the work needed `Curl.Console/CurlCommandRunner.cs` (the `-w` values are built there), the new `Curl.Console/DebugWriteOutStandIn.cs`, `UpstreamConformanceTests.cs` (RunCurlAsync's flag) and `UpstreamConformanceTests.DebugStandIns.cs` (the rule table line); no task in Doing on origin/work/dark-factory named any of them.
- Measured: cases 1981, 970 and 972 each `Passed` with `CURL_UPSTREAM_CASE`; Curl.Console 100% line, 100% branch, 0 members past a gate (`Measure-CodeQuality.ps1 -Library Curl.Console`). The first measurement found the runner constructor and the dialing `CreateRunner` at 14 and 12; the branches moved into `DebugWriteOutStandIn.OrNone` and `OfHonoured`.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: c37c0770 b6940992 786b670f 9495a595.
- 2026-10-21: Doing -> Done. Curl's -w honours the debug build's CURL_TIME, CURL_DEBUG_SIZE and CURL_VERSION in the harness; upstream cases 970, 972 and 1981 pass and are on the ratchet
