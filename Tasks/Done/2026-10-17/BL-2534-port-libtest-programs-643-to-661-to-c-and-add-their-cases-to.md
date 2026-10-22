---
id: BL-2534
title: Port libtest programs 643 to 661 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2533]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2534 — Port libtest programs 643 to 661 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 643 to 661 (batch 8) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 8 of 26: programs 643, 650, 651, 652, 653, 654, 655, 658, 659, 661. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib643, lib650, lib651, lib652, lib653, lib654, lib655, lib658, lib659, lib661 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): all ten programs exist in curl-8_22_0's `tests/libtest` (read from raw.githubusercontent.com); lib643 also serves case 645, and lib597 serves 660 (ported by BL-2533). Every Libcurl call they need existed (form API, MIME parts, `curl_easy_duphandle`, `CURLOPT_CURLU`, `CURLOPT_RESOLVER_START_FUNCTION`, `CURLOPT_FTP_FILEMETHOD`, `CURLOPT_UPLOAD_BUFFERSIZE`).
- Ports `Lib643`, `Lib650` to `Lib655`, `Lib658`, `Lib659`, `Lib661`, tests in `Lib643To661Tests.cs`. New shared helpers, each tested: `LibTestMimeCalls.FirstFailure` (lib643, lib652) and `LibTestSteps.EasyPerformOrReportAsync` (lib658, lib659); `Lib643.ByteReader` is also lib654's read callback. Test fakes: `FakeMimeBuilder` takes an answer and records each part call, `FakeFormBuilder` gained `FailingCall` and calls `curl_formget`'s callback, `FakeCurlEngine.UrlSetResult`.
- Choices (sensible defaults, no ADR needed): lib654's `freefunc` count and lib655's user-data comparison are left out, since Libcurl already decided `curl_mime_data_cb`'s `freefunc` and a callback's `void *` have no managed counterpart (the closure carries them); `easy_setopt` checks that cannot fail are left out, as earlier ports do.
- On the ratchet: 643, 651, 652 (HTTP and SMTP protocol bytes only; no paths, OS text or certificates). Not passing, filed: 645 chunk size (BL-2624); 650 posts nothing (BL-2625); 653 and 654 MIME taken as a snapshot at setopt (BL-2626); 655 resolver start callback never called (BL-2627); 658 and 659 `CURLOPT_CURLU` ignored (BL-2628); 661 EPSV where curl quits (BL-2629).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` first reported `Lib661.TestAsync` at complexity 14; after moving the loop into `RunTransfersAsync` it reported 0 failing members. Full build clean, fast tests green (LibTest 409, Conformance 3213 passed).
## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: ed8a6d47 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib643, 650-655, 658, 659, 661 ported with unit tests; cases 643, 651, 652 on the ratchet; the rest filed as BL-2624 to BL-2629
