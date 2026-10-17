---
id: BL-2540
title: Port libtest programs 1535 to 1550 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2539]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2540 — Port libtest programs 1535 to 1550 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1535 to 1550 (batch 14) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 14 of 26: programs 1535, 1536, 1537, 1538, 1540, 1541, 1542, 1545, 1549, 1550. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1535, lib1536, lib1537, lib1538, lib1540, lib1541, lib1542, lib1545, lib1549, lib1550 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten exist as C; each is run only by its own case (`lib%TESTNUMBER`); 1539 and 1543 use lib1514 and lib1518 (ported earlier), 1544 and 1546 to 1548 use other tools. Every Libcurl call they need existed (`CURLINFO_PROTOCOL`, `CURLINFO_SCHEME`, `CurlStrings` escape/unescape, `CurlErrorText`, `CURLOPT_MAXLIFETIME_CONN`, `curl_formadd`, `CURLINFO_COOKIELIST`, the pipelining blocklist multi options).
- Ports `Lib1535` to `Lib1538`, `Lib1540` (with `TransferStatus`), `Lib1541`, `Lib1542`, `Lib1545`, `Lib1549`, `Lib1550`; tests in `Lib1535To1550Tests.cs`.
- Choices (sensible defaults, no ADR needed): as in BL-2539, `curl_easy_getinfo`/`curl_easy_duphandle` failure checks and `easy_setopt` failure jumps are left out where Libcurl cannot fail them on a live handle (URL, callbacks, verbose), so no untestable branch is added. lib1537's escapes of its high bytes use the byte-span `CurlStrings.Escape` (the string forms read UTF-8); its -1-length unescape leaves `outlen` at 2017 as C does, modelled by `UnescapeKeepingLength` since a C# `out` must be assigned. lib1538's `CURL_LAST`, `CURLM_LAST`, `CURLSHE_LAST`, `CURLUE_LAST` are pinned from case 1538's expected output (102, 13, 6, 33). lib1541's error line keeps upstream's doubled `CURLINFO_CURLINFO_` prefix (`KN()` hands the whole name to `"CURLINFO_%s"`).
- On the ratchet: 1537, 1538, 1545, 1550. Platform check: 1537 and 1538 compare only fixed stdout text, 1545 has an empty `<verify>` (exit 0 only; its `missing-file` is never opened successfully on any platform), 1550 checks nothing but exit 0.
- Not on it, filed: 1540 (`curl did not finish within 20 seconds`, unpause from the transfer-info callback) - BL-2651. 1541 (stdout line 1: expected `CURLINFO_CONNECT_TIME_T on 1st header is OK`, got `... is WRONG: 0`) - BL-2648. 1549 (stderr byte 0: expected `URL: http://127.0.0.1:8990/1549`, got `0 cookies`; the runner does not write `first.c`'s own lines) - BL-2649. 1542 (skipped: the runner does not run its `$_ = '' if(...)` stripfile) - BL-2650. 1535 and 1536 (exit 2, `CURLE_FAILED_INIT` from a "init failed" check) have the cause BL-2644 already owns (`ManagedEasyTransfer.Reset()` keeps the last `TransferInfo`; lib1534 fails the same way), so no new task.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 0 failing members. Full build clean (0 warnings); fast tests green in every test project (LibTest 651, Conformance 3249 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: abb25e18 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1535 to lib1550 ported to C# over Libcurl; cases 1537, 1538, 1545, 1550 on the ratchet; 1540-1542, 1549 filed as BL-2651 to BL-2650
