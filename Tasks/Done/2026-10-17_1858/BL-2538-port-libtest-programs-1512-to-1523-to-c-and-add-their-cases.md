---
id: BL-2538
title: Port libtest programs 1512 to 1523 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2537]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2538 — Port libtest programs 1512 to 1523 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1512 to 1523 (batch 12) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 12 of 26: programs 1512, 1513, 1514, 1515, 1517, 1518, 1520, 1521, 1522, 1523. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1512, lib1513, lib1514, lib1515, lib1517, lib1518, lib1520, lib1521, lib1522, lib1523 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). lib1512, lib1513, lib1514 (also case 1539), lib1515 (also case 1516), lib1517, lib1518 (also cases 1519 and 1543), lib1520, lib1522 and lib1523 exist as C; lib1516 and lib1519 do not (their cases run lib1515 and lib1518). lib1521 is not C in the tarball: `mk-lib1521.pl` generates it from `curl.h`, calling `curl_easy_setopt` for every option and `curl_easy_getinfo` for every info; too big for a batch, so split out as BL-2642 (sizing split, filed before any code). Every Libcurl call the other nine need existed.
- Ports `Lib1512` to `Lib1515`, `Lib1517`, `Lib1518`, `Lib1520`, `Lib1522`, `Lib1523`, tests in `Lib1512To1523Tests.cs`. New helper `LibTestClock.Wait`: `curlx_wait_ms` blocking inside lib1517's synchronous read callback (a `TimeProvider` timer and a `ManualResetEventSlim`, as upstream's callback blocks), tested in `LibTestClockTests`.
- Choices (sensible defaults, no ADR needed): `easy_setopt` checks whose values Libcurl always takes are left out (lib1512, lib1518, lib1522), as earlier ports do. lib1518 and lib1522 make no `curl_global_init` call and lib1523's is unchecked, so they run as `global_init` (differs only with no engine). lib1522's socket option callback leaves the socket alone, as upstream's `#else` branch does, since the 4 KB send buffer changes no byte the case checks. lib1515 reads `CURLMSG_DONE` without checking the type (the only type). Readdata is set to the reader object where upstream passes its struct.
- On the ratchet: 1513, 1514, 1515, 1516, 1518, 1519, 1520, 1522, 1523 and 1539. Platform check: none prints a path, OS error text or certificate (1515/1516 check only the exit code; 1522 prints only sizes; 1523 checks only exit 0, which holds whether its second run times out or succeeds). Not on it: 1512 is skipped by the runner for the `left intact` `<stripfile>` line, the same gap as BL-2641 (noted there); 1517 is skipped on Windows by its `!win32` feature line; 1543 sends no request (`<verify><protocol> differs at byte 0`, a `CURLU_ALLOW_SPACE` URL handle through `CURLOPT_CURLU`), filed as BL-2643.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 0 failing members. Full build clean (0 warnings); fast tests green in every test project (LibTest 582).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b6221732 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1512 to lib1523 but lib1521 ported and unit tested; cases 1513 to 1516, 1518 to 1520, 1522, 1523 and 1539 on the ratchet; lib1521 split to BL-2642, 1543 filed as BL-2643, 1512 noted under BL-2641
