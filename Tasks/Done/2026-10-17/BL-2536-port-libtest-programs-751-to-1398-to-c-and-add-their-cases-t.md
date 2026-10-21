---
id: BL-2536
title: Port libtest programs 751 to 1398 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2535]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2536 — Port libtest programs 751 to 1398 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 751 to 1398 (batch 10) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 10 of 26: programs 751, 753, 757, 758, 766, 1156, 1301, 1308, 1396, 1398. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib751, lib753, lib757, lib758, lib766, lib1156, lib1301, lib1308, lib1396, lib1398 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): all ten programs exist in curl-8_22_0's `tests/libtest` (read from raw.githubusercontent.com), each serving only its own case; no number in the batch is served by another tool. Every Libcurl call they need existed (`curl_strequal`/`curl_strnequal`, `curl_easy_escape`/`unescape`, `curl_msnprintf`, `curl_formadd`/`curl_formget`, `curl_mime_data_cb`, `curl_mime_subparts`, `CURLOPT_SOCKOPTFUNCTION`).
- Ports `Lib751`, `Lib753`, `Lib757`, `Lib758`, `Lib766`, `Lib1156`, `Lib1301`, `Lib1308`, `Lib1396`, `Lib1398`, tests in `Lib751To1398Tests.cs`. New shared helper, tested in `LibTestAssertionsTests.cs`: `LibTestAssertions` (`unitcheck.h`'s `fail_unless`, `abort_unless`, `fail`, `unitfail`), used by lib1301, lib1308, lib1396 and lib1398. Test fake: `FakeFormBuilder.SerializedLength`.
- Choices (sensible defaults, no ADR needed): lib758 is ported as upstream builds it without OpenSSL 3 (`T578_ENABLED` undefined, as in the Schannel build): `test()` returns `CURLE_OK`, since its check is an OpenSSL `SSL_CTX` verify callback and Libcurl's TLS is never OpenSSL. The checks of lib1301, lib1396 and lib1398 are tables a test can replace, so the failure branches are covered. lib1396's escape rows go through the raw-byte `CurlStrings.Escape`, since the text form reads UTF-8 and a row holds 0xFF. lib1308's `post == last` check is left out (Libcurl keeps no `last` pointer); lib753 sets no `WRITEDATA`/`HEADERDATA` (its callbacks' closures carry the status). `easy_setopt` checks that cannot fail are left out, as earlier ports do.
- On the ratchet: 751, 758, 1156, 1301, 1308, 1396, 1398. Platform check: none prints a path, OS text or certificate; 1308's only path is the form file, whose name is not in the counted bytes (`custom named file`) and whose content the runner writes; 758 does nothing on any platform. Not passing, filed: 753 skipped for the Debug feature and `CURL_FTP_PWD_STOP` (BL-2636); 757 the callback part is read three times per post (BL-2637); 766 the socket-option callback is not called for the accepted active-FTP connection (BL-2638).
- Quality: the first `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 5 members (Lib1156 `OneTestAsync` complexity 16; Lib753 `TurnAsync`, `StartAndLoopAsync` and the constructor one branch each; Lib1398 `CText` one branch); after splitting `SetOptions` out, dropping an unreachable branch and adding tests it reported 0 failing members. Full build clean (0 warnings); fast tests green (LibTest 494 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: cac5a86d ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib751 to lib1398 ported and unit tested; cases 751, 758, 1156, 1301, 1308, 1396 and 1398 on the ratchet, the rest filed as BL-2636 to BL-2638
