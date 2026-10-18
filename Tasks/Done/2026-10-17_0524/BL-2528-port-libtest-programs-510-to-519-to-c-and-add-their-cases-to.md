---
id: BL-2528
title: Port libtest programs 510 to 519 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2527]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2528 — Port libtest programs 510 to 519 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 510 to 519 (batch 2) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 2 of 26: programs 510, 511, 512, 513, 514, 515, 516, 517, 518, 519. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib510, lib511, lib512, lib513, lib514, lib515, lib516, lib517, lib518, lib519 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 5): Ported lib510 to lib519 (all ten exist in tests/libtest 8.22.0; test565 also runs lib510) as `Lib510` to `Lib519`, registered in `LibTestPrograms.Ported`, tests in `Lib510To519Tests.cs`; the fake engine now duplicates a transfer for lib512. Choices: lib512 cannot answer its 3 (a failing `curl_easy_duphandle`), since `CurlEasy.DupHandle` always gives a handle; lib518 is the `#else` build without getrlimit/setrlimit (none in the BCL), so it prints `system lacks necessary system function(s)` and answers MajorBad, as real curl on Windows does; lib517 holds the 64-bit `time_t` rows. Measured on Windows through CURL_UPSTREAM_CASE: 511, 512 and 519 pass and are on the ratchet; all three are plain HTTP with no path, drive letter, OS error text or certificate. Failing, left off: 510 and 565 POST with read callback sent as GET (BL-2586, already filed); 513 exit 52 not 42 (BL-2590); 514 POST not HEAD (BL-2591); 515 GET not zero-byte POST (BL-2592); 516 skipped, form-api feature (BL-2593); 517 skipped, no <client><command> (BL-2589), and military time zones A-Y have reversed signs in CurlDate (BL-2594); 518 skipped, its precheck is not interpreted and Curl has no getrlimit (as on Windows curl). Measure-CodeQuality: 0 failing members, LibTest unit tests 176.

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 19603123 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib510-lib519 ported to C#; 511, 512 and 519 on the ratchet, the other cases filed as BL-2590 to BL-2594 or under BL-2586 and BL-2589
