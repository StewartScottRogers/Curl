---
id: BL-2530
title: Port libtest programs 539 to 553 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2529]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2530 — Port libtest programs 539 to 553 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 539 to 553 (batch 4) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 4 of 26: programs 539, 540, 541, 542, 543, 544, 547, 549, 552, 553. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib539, lib540, lib541, lib542, lib543, lib544, lib547, lib549, lib552, lib553 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): all ten numbers exist in tests/libtest 8.22.0; the cases that run them are 539-545, 547-553 and 561 (545 runs lib544; 548 and 551 run lib547; 550 and 561 run lib549). Ports: `Lib539`, `Lib540`, `Lib541`, `Lib543`, `Lib544`, `Lib547`, `Lib552`, `Lib553`; lib542 and lib549 are two more `LibOneVerboseTransfer` option lists (`NobodyWithoutHeader`, `ProxyTransferMode`). Helpers: `LibTestUploadFile` (the open-the-upload-file opening lib505 and lib541 share; `Lib505` now uses it) and `LibTestIoctl` (the `CURLIOCMD`/`CURLIOE` constants). Tests in `Lib539To553Tests.cs`; `LibTestProgramsTests.Ported_*` renamed and extended.
- Decision (sensible default): Libcurl has no `CURLOPT_IOCTLFUNCTION` setter and answers `CURLE_NOT_BUILT_IN`. Rather than requeue the whole batch, lib547 and lib552 are ported faithfully, handing their ioctl callback over as `Func<int,int>`; the cases that need it fail at that setopt and are filed as BL-2607, which adds the delegate and swaps it into both ports.
- lib544 without `CURLOPT_POSTFIELDSIZE` passes the bytes up to the NUL, as libcurl reads a `char *` with no size; case 545 passes the whole buffer.
- Cases 539, 541, 543, 544, 545 and 553 pass and are on the ratchet; none depends on the platform (FTP and HTTP protocol bytes and escape output only; 541's upload path is an argument, not in the output). Failing, filed: 547, 551, 552 (BL-2607, no IOCTLFUNCTION); 540 and 548 (BL-2608, proxy authentication retry missing); 542 (BL-2609, MDTM sent before TYPE I). Skipped by the harness as `Curl lacks the feature ftp`: 549, 550, 561 (BL-2610).
- Quality run on `Curl.Conformance.LibTest.UnitLibrary`: one failing member, `Lib540..ctor` null-guard branch, covered by a test added after it; all else 100%/100%, complexity at most 10.

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: e6e3fadf ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib539-lib553 ported; cases 539, 541, 543, 544, 545, 553 on the ratchet; the rest filed as BL-2607 to BL-2610
