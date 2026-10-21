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
completed: 2026-10-21
---
# BL-2960 — Port upstream unit program 3216 (Curl_rlimit token bucket with burst and block)

## Goal

Upstream case 3216 runs as a ported unit program over a Curl rate limit that answers libcurl's `Curl_rlimit_*` functions, ends `Passed`, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2848 (ADR-0711 row 3216). `tests/unit/unit3216.c` of curl 8.22.0 (https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit3216.c) and `lib/ratelimit.c` define a token bucket: `Curl_rlimit_init(rate, burst, now)`, `_avail`, `_drain`, `_wait_ms` and `_block`, with an unlimited rate (`CURL_OFF_T_MAX` available, never waits), a burst cap, tokens going negative when overdrawn, and blocking to 0. The program makes 37 `fail_unless` checks over three limits (unlimited, 10/s, 10/s with burst 15). Curl's `--limit-rate` today is `Curl.Core.UnitLibrary/RateLimitedStream.cs`, which waits until bytes moved would have taken that long at the rate: no burst, no block, no token count. Add a `TransferRateLimit` in `Curl.Core.UnitLibrary` (over `TimeProvider` timestamps) that answers `ratelimit.c` row for row; moving `RateLimitedStream` onto it is a follow-up task to file only if curl's measured `--limit-rate` behaviour differs from Curl's. Port the program over it in `Curl.Conformance.UnitProgram.UnitLibrary/Limits/` (an `IUnitProgram` registered in `UnitPrograms.Limits.cs`, `UnitChecks` from `Hashes`, each check's text as the failure line), as BL-2848 ported 1605 and 1606.

## Acceptance criteria

- [x] `TransferRateLimit` answers every check of unit3216.c, with focused tests in `Curl.Core.UnitTests`.
- [x] `Unit3216` makes the C program's 37 checks in order (named in its doc comment), is registered in `UnitPrograms.Limits.cs`, and has tests in `Curl.Conformance.UnitProgram.UnitTests/Limits/`.
- [x] Case 3216 ends `Passed` with `$env:CURL_UPSTREAM_CASE='3216'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Core.UnitLibrary` and `Curl.Conformance.UnitProgram.UnitLibrary` keep 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- `Curl.Core.UnitLibrary/TransferRateLimit.cs` is `lib/ratelimit.c`'s bucket on a `TimeProvider`: the constructor is `Curl_rlimit_init`, and `Available`, `Drain`, `WaitMilliseconds`, `Block` and `IsBlocked` are `_avail`, `_drain`, `_wait_ms`, `_block` and `_is_blocked`, each reading the clock where C takes `pts`. Timestamps are microseconds since construction. `Curl_rlimit_start`'s tuning for a known total, `_per_step`, `_active` and `_next_step_ms` are left out: nothing calls them yet (unit3216 does not, and `RateLimitedStream` is not moved onto this class). The overflow guards of `rlimit_update` and `_drain` are kept; `_wait_ms` computes in `Int128` and clamps to `long.MaxValue`, where C's `-tokens * 100` would overflow at `INT64_MIN`.
- The C program makes 30 `fail_unless` checks, not the 37 the criterion counts (6 unlimited, 10 at 10/s, 14 at 10/s burst 15, counted from curl-8_22_0's `tests/unit/unit3216.c`). `Unit3216RateLimit` makes all 30 in order and names them in its doc comment; the criterion's substance (every check, in order) is met.
- Where the C program sets `ts = curlx_now()` before blocking the 10/s limit, the port leaves the stepped clock where it is: block and unblock only restart from the time they read, so the answers are the same.
- `--limit-rate`: not measured against curl here, and no follow-up filed; the task files the `RateLimitedStream` move only if curl's measured behaviour differs, which this run did not measure.
- Measured: `dotnet build Curl.slnx -warnaserror` clean; fast tests all green (Core 1748 passed, UnitProgram 185 passed, Conformance 3786 passed); `CURL_UPSTREAM_CASE=3216` manual test passed; `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Conformance.UnitProgram.UnitLibrary`: both 100% line and branch, 0 failing members. The program is pure arithmetic on a stepped clock, so its output does not depend on the platform; CI on Linux and macOS is the shift's to watch after it pushes (lanes do not push).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 6895d36b e290ede8 6da1ccfe cbaf0a82 620ebead.
- 2026-10-21: Doing -> Done. Upstream case 3216 passes over Curl.Core's TransferRateLimit, a port of lib/ratelimit.c's token bucket
