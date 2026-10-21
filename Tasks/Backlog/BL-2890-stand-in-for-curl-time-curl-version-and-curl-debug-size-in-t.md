---
id: BL-2890
title: Stand in for CURL_TIME, CURL_VERSION and CURL_DEBUG_SIZE in the curl tool's --write-out so upstream cases 970, 972 and 1981 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.WriteOutClockStandIn.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2890 — Stand in for CURL_TIME, CURL_VERSION and CURL_DEBUG_SIZE in the curl tool's --write-out so upstream cases 970, 972 and 1981 run

## Goal

Upstream cases 970, 972 (`--write-out '%{json}'`) and 1981 (`%time{...}`) pass in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.

## Context

Split from BL-2835. 970 and 972 set `CURL_TIME=13`, `CURL_DEBUG_SIZE=4019` and `CURL_VERSION=curl-unit-test-fake-version`; 1981 sets `CURL_TIME=1754037103` and `LC_TIME=C` and expects `Time: 01/Aug/2025 08:31:43.037103 +0000 UTC%s 1754037103`: the debug build's fixed clock carries `CURL_TIME % 1000000` as its microseconds (read upstream's `lib/curl_trc.c`/`lib/timeval.c` and `src/tool_writeout.c` at tag `curl-8_22_0` for the exact rule, and how `CURL_DEBUG_SIZE` and `CURL_VERSION` reach the JSON). The runner's `-w` clock is `CurlCommandRunner`'s `timeProvider`; the dialing `CurlComposition.CreateRunner` overload passes none today. Check `%Z` under the Windows C runtime dialect: if Windows' answer differs, pin each platform in its own test or keep 1981 off the ratchet on that platform. Report `Debug` to such a case with one rule in `DebugStandInRules`, its predicate in its own partial file.

## Acceptance criteria

- [ ] `PlatformFor` reports `Debug` to a case setting `CURL_TIME` without the HSTS feature, `CURL_VERSION` or `CURL_DEBUG_SIZE`, and a unit test pins it.
- [ ] A unit test pins each stand-in: the fixed `-w` clock with its microseconds, the version string and the debug size in `%{json}`.
- [ ] Cases 970, 972 and 1981 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-21: Created.
