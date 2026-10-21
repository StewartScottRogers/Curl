---
id: BL-2960
title: Port upstream unit program 3216 (Curl_rlimit token bucket with burst and block)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, Curl.Conformance.UnitProgram.UnitLibrary/Limits/Unit3216, Curl.Conformance.UnitProgram.UnitTests/Limits/Unit3216, Curl.Core.UnitLibrary/TransferRateLimit.cs, Curl.Core.UnitTests/TransferRateLimitTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2960 — Port upstream unit program 3216 (Curl_rlimit token bucket with burst and block)

## Goal

Upstream case 3216 runs as a ported unit program over a Curl rate limit that answers libcurl's `Curl_rlimit_*` functions, ends `Passed`, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2848 (ADR-0711 row 3216). `tests/unit/unit3216.c` of curl 8.22.0 (https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit3216.c) and `lib/ratelimit.c` define a token bucket: `Curl_rlimit_init(rate, burst, now)`, `_avail`, `_drain`, `_wait_ms` and `_block`, with an unlimited rate (`CURL_OFF_T_MAX` available, never waits), a burst cap, tokens going negative when overdrawn, and blocking to 0. The program makes 37 `fail_unless` checks over three limits (unlimited, 10/s, 10/s with burst 15). Curl's `--limit-rate` today is `Curl.Core.UnitLibrary/RateLimitedStream.cs`, which waits until bytes moved would have taken that long at the rate: no burst, no block, no token count. Add a `TransferRateLimit` in `Curl.Core.UnitLibrary` (over `TimeProvider` timestamps) that answers `ratelimit.c` row for row; moving `RateLimitedStream` onto it is a follow-up task to file only if curl's measured `--limit-rate` behaviour differs from Curl's. Port the program over it in `Curl.Conformance.UnitProgram.UnitLibrary/Limits/` (an `IUnitProgram` registered in `UnitPrograms.Limits.cs`, `UnitChecks` from `Hashes`, each check's text as the failure line), as BL-2848 ported 1605 and 1606.

## Acceptance criteria

- [ ] `TransferRateLimit` answers every check of unit3216.c, with focused tests in `Curl.Core.UnitTests`.
- [ ] `Unit3216` makes the C program's 37 checks in order (named in its doc comment), is registered in `UnitPrograms.Limits.cs`, and has tests in `Curl.Conformance.UnitProgram.UnitTests/Limits/`.
- [ ] Case 3216 ends `Passed` with `$env:CURL_UPSTREAM_CASE='3216'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Core.UnitLibrary` and `Curl.Conformance.UnitProgram.UnitLibrary` keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-21: Created.
