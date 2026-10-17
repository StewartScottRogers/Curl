---
id: BL-2533
title: Port libtest programs 578 to 599 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2532]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2533 — Port libtest programs 578 to 599 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 578 to 599 (batch 7) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 7 of 26: programs 578, 579, 582, 583, 589, 590, 591, 597, 598, 599. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib578, lib579, lib582, lib583, lib589, lib590, lib591, lib597, lib598, lib599 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): all ten programs exist and run `lib%TESTNUMBER`; lib589 also serves 584, lib591 serves 592 to 594, lib597 serves 660. C sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). Every Libcurl option, callback and info they need already existed (progress callback, MIME, socket and timer callbacks, `curl_easy_reset`, `CURLINFO_PROXYAUTH_USED`, `CURLINFO_CONTENT_LENGTH_DOWNLOAD`, `CURLOPT_ACCEPTTIMEOUT_MS`).
- Ports `Lib578` to `Lib599` (tests in `Lib578To599Tests.cs`). New helpers, each used by two or more ports and tested: `LibTestArgumentFile` (open-write-close of `libtest_arg2`'s file, lib578/579/599) and `LibTestSteps.PerformWithMultiTimeoutUntilDoneAsync` (lib591/597's `multi_timeout` + `select` loop). `Lib582Run` follows `Lib530Run`'s socket-action loop. Choices: `easy_setopt` checks that cannot fail are left out, as earlier ports do; errno text in `fopen() failed` lines is the exception message, as `LibTestUploadFile` does; the test fake engine gained `CreatingMime` and a `FakeMimeBuilder`.
- On the ratchet: 578, 582, 583, 591, 592, 598, 599 (protocol bytes, error codes or the client's own file; no paths, OS text or certificates in what is compared). Not passing, filed: 584 and 589 send GET after `CURLOPT_MIMEPOST` NULL (BL-2620); 579 writes no stdout and 590 never sends the NTLM follow-up (BL-2621); 593 and 594 time out after 20 s (BL-2622); 597 and 660 send nothing through the multi connect-only path (BL-2623).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reported 0 failing members. Full build clean, fast tests green (LibTest 372, Conformance 3210 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 38948fb2 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib578, 579, 582, 583, 589-591, 597-599 ported with unit tests; cases 578, 582, 583, 591, 592, 598, 599 on the ratchet; the rest filed as BL-2620 to BL-2623
