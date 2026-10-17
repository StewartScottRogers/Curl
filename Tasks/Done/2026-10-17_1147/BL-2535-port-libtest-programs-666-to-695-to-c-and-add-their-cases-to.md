---
id: BL-2535
title: Port libtest programs 666 to 695 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2534]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2535 — Port libtest programs 666 to 695 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 666 to 695 (batch 9) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 9 of 26: programs 666, 667, 668, 670, 674, 676, 677, 678, 694, 695. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib666, lib667, lib668, lib670, lib674, lib676, lib677, lib678, lib694, lib695 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): all ten programs exist in curl-8_22_0's `tests/libtest` (read from raw.githubusercontent.com). lib670 serves cases 670 to 673; no other number in the batch is served by another tool. Every Libcurl call they need existed (`curl_easy_pause`, `curl_easy_send`/`recv`, `curl_mime_subparts`, `CURLOPT_CAINFO_BLOB`, `CURLINFO_HTTPAUTH_USED`, `CURLINFO_ACTIVESOCKET`, `CURLFORM_STREAM`).
- Ports `Lib666` to `Lib668`, `Lib670`, `Lib674`, `Lib676` to `Lib678`, `Lib694`, `Lib695`, tests in `Lib666To695Tests.cs`. New shared helper, tested: `LibTestSteps.EasyPerformOrWriteAsync` (lib667, lib668, lib676). Test fakes: `FakeCurlEngine.Pauses` records `curl_easy_pause`, `FakeMultiTransfer.ExtraSocketEvents` fills an extra wait socket's `revents`, and a refused add no longer registers the transfer.
- Choices (sensible defaults, no ADR needed): `easy_setopt` and `curl_easy_getinfo` checks that cannot fail are left out, as earlier ports do; lib670's read callback after EOF writes upstream's line and aborts the transfer where upstream calls `exit(1)`; time comes from an injected `TimeProvider` (lib670, lib677); lib678's `loadfile` reads the file only when it exists.
- On the ratchet: 666 and 676 (HTTP bytes and a cookie file's contents; no paths, OS text or certificates in the output). Not passing, filed: 667 and 668 MIME callback chunks (BL-2630); 670 to 673 paused upload never resumes (BL-2631); 674 and 695 behind BL-2628 and BL-2626 (BL-2632); 677 behind BL-2623 (BL-2633); 678 skipped for its `<precheck>` line (BL-2634); 694 NTLM type-1 flags (BL-2635).
- Quality: the first `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 4 members (Lib670 `MultiLoopAsync` 14 and `TestAsync` 12 complexity, `TransferAsync` one line, Lib677 `Exchange.Advance` one branch); after splitting `PerformOnceAsync` and `PostAsync` out and testing a zero-byte send, it reported 0 failing members. Full build clean; fast tests green (LibTest 456, Conformance 3215 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 72e03d62 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib666 to lib695 ported and unit tested; cases 666 and 676 on the ratchet, the rest filed as BL-2630 to BL-2635
