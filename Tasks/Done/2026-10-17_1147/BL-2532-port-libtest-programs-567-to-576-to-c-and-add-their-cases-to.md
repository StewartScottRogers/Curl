---
id: BL-2532
title: Port libtest programs 567 to 576 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2531]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2532 — Port libtest programs 567 to 576 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 567 to 576 (batch 6) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 6 of 26: programs 567, 568, 569, 570, 571, 572, 573, 574, 575, 576. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib567, lib568, lib569, lib570, lib571, lib572, lib573, lib574, lib575, lib576 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): all ten programs exist and every one runs `lib%TESTNUMBER`; lib567 also serves 577 and 689, lib574 serves 1113, lib576 serves 1114, 1162 and 1163. C sources read from curl-8_22_0's `tests/libtest`. Every Libcurl option, callback and info they need was already there (RTSP options, `CURLINFO_RTSP_SESSION_ID`, interleave, fnmatch and chunk callbacks, `curl_easy_duphandle`, `CURLINFO_CONNECT_TIME`).
- Ports `Lib567` to `Lib576`, tests in `Lib567To576Tests.cs`. New helper `LibTestRtspRequests` (lib568 to lib572: `tutil_suburl` stream URIs, the upload request of 568 and 572, the output files of 569 and 571); `LibTestRun.SingleMultiTransferAsync` gained an optional `afterTransfer` for lib573's connect-time check. Choice: `easy_setopt` checks that cannot fail are left out, as earlier ports do, so no branch is unreachable.
- On the ratchet: 573 (HTTP only, no platform dependence). Not passing, filed: 567 to 572, 577, 689 skipped as no RTSP server emulation (BL-2617); 574, 575, 1113, 1162, 1163 skipped for `CWD fully_simulated` (BL-2618); 576 and 1114 run but print nothing, the chunk begin callback never called (BL-2619).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reported 0 failing members. Full build clean, fast tests green (LibTest 330, Conformance 3203 passed).
## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 117e156f ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib567-lib576 ported to C# with unit tests; case 573 on the ratchet; the rest filed as BL-2617 to BL-2619
