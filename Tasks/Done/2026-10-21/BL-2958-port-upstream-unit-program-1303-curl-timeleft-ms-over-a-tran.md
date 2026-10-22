---
id: BL-2958
title: Port upstream unit program 1303 (Curl_timeleft_ms) over a transfer time-left function
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, Curl.Conformance.UnitProgram.UnitLibrary/Limits/Unit1303, Curl.Conformance.UnitProgram.UnitTests/Limits/Unit1303, Curl.Core.UnitLibrary/TransferTimeLeft.cs, Curl.Core.UnitTests/TransferTimeLeftTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2958 — Port upstream unit program 1303 (Curl_timeleft_ms) over a transfer time-left function

## Goal

Upstream case 1303 runs as a ported unit program over a Curl function that answers libcurl's `Curl_timeleft_ms`, ends `Passed`, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2848 (ADR-0711 row 1303). `tests/unit/unit1303.c` of curl 8.22.0 (https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1303.c) checks 33 rows of time left, in milliseconds, given `-m` (`timeout`), `--connect-timeout` (`connecttimeout`), whether the transfer is still connecting, and the time since the start: the smaller of the two while connecting, the connect timeout defaulting to 300 s (`DEFAULT_CONNECT_TIMEOUT`) while connecting with none set, 0 for "no timeout active", -1 for exactly expired and negative when overdue. Curl has no such function today: `-m` is `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs` and the connect timeout lives in the connectors, each counting its own limit. Add one small `TransferTimeLeft` in `Curl.Core.UnitLibrary` (over `TimeSpan`s) that answers the C function, and port the program over it in `Curl.Conformance.UnitProgram.UnitLibrary/Limits/` (an `IUnitProgram` registered in `UnitPrograms.Limits.cs`, `UnitChecks` from `Hashes`, each row's comment as the failure line), as BL-2848 ported 1605 and 1606.

## Acceptance criteria

- [x] `TransferTimeLeft` answers every row of unit1303.c, with focused tests in `Curl.Core.UnitTests`.
- [x] `Unit1303` makes the C program's 33 checks row for row (named in its doc comment), is registered in `UnitPrograms.Limits.cs`, and has tests in `Curl.Conformance.UnitProgram.UnitTests/Limits/`.
- [x] Case 1303 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1303'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Core.UnitLibrary` and `Curl.Conformance.UnitProgram.UnitLibrary` keep 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- `TransferTimeLeft.Milliseconds(maxTime, connectTimeout, connecting, elapsed)` in `Curl.Core` answers `Curl_timeleft_ms` as curl 8.22.0 does: `-m` left (0 = none set; exactly 0 left becomes -1); once connected that is the answer; while connecting the connect timeout (300 s default) left, and the smaller of the two when `-m` is set. A static function over `TimeSpan`s, no clock: the C function reads `now` from its caller too. Not wired into `MaxTimeWatchdog` or the connectors; this task only adds the function and the port.
- The port is `Limits/Unit1303TimeLeft.cs` (the `touches` entry `Limits/Unit1303` named the file prefix). Its 33 rows are the C table verbatim, elapsed = now - 1000000 s.
- Measured: `Curl.Core.UnitLibrary` and `Curl.Conformance.UnitProgram.UnitLibrary` 100% line and branch, 0 failing members, worst CRAP 10. Case 1303 passes with `CURL_UPSTREAM_CASE=1303`. Fast suite green on Windows.
- Pure arithmetic over `TimeSpan`, nothing platform-dependent. The CI criterion is handed to the shift (a lane may not push): the coordinator's CI watch (BL-987) files a High task if case 1303 or the new tests fail on Linux or macOS.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 4f2fd27b baef796f a33759f5 cbaf0a82 620ebead.
- 2026-10-21: Doing -> Done. Case 1303 passes: unit1303 ported over Curl.Core's TransferTimeLeft, which answers Curl_timeleft_ms
