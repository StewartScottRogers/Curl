---
id: BL-2542
title: Port libtest programs 1564 to 1588 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2541]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2542 — Port libtest programs 1564 to 1588 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1564 to 1588 (batch 16) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 16 of 26: programs 1564, 1565, 1567, 1568, 1569, 1571, 1576, 1582, 1587, 1588. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1564, lib1565, lib1567, lib1568, lib1569, lib1571, lib1576, lib1582, lib1587, lib1588 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C. lib1569 serves cases 1569 and 1570, lib1571 cases 1571 to 1575 and 1581, lib1576 cases 1576 to 1580; the rest only their own case. Every Libcurl call they need existed (`curl_multi_wakeup`, `curl_multi_poll`, `CURLOPT_CURLU`, `CURLOPT_PORT`, `CURLFOLLOW_*`, read and seek callbacks, `CURLOPT_PROXYAUTH`, `CURLOPT_RESOLVE`).
- Ports `Lib1564`, `Lib1565`, `Lib1567`, `Lib1568`, `Lib1569`, `Lib1571`, `Lib1576`, `Lib1582`, `Lib1587`, `Lib1588`; tests in `Lib1564To1588Tests.cs`. `FakeMultiTransfer` gained `Polling` (a hook in each poll) and `EachAddedDoneResult` (one done message per added transfer).
- Choices (sensible defaults, no ADR needed): lib1565 is ported as built with threads (its second thread a `Task`, its `pthread_mutex` a `Lock`), since the case only tests that build; its `pthread_create` check, the "Not all connections" lines (unreachable once the loop ends), the unexpected-message line (Libcurl has only `CURLMSG_DONE`), the thread's `easy_init`/`easy_setopt` checks (cannot fail on a live library) and the hang-timeout checks after each message and after adding (only list work between them and the one before) are left out. `TIME_BETWEEN_START_SECS` is a constructor parameter (2000 ms by default) so tests run without waiting. lib1587 is the program as built without OpenSSL (succeeds at once), as Lib758 is. lib1567 leaves out `if(u)` after `curl_url()`, as lib1559 did; lib1568's `curlx_str_number` is `Lib1568.TryParsePort`; lib1588's `test_argc < 3` is fewer than two arguments after the URL (`test_argc` is `Arguments.Count + 1`, as lib540's `< 4` is `Count < 3`), and its `CURLOPT_RESOLVE` check is left out (cannot fail). lib1576's unchecked `CURLOPT_HTTPHEADER` is set with its checked options.
- On the ratchet: 1569, 1570, 1571, 1572, 1573, 1581. Platform check: all six check only protocol bytes and exit code over the in-memory HTTP and FTP servers; no path, drive letter, OS error text or certificate reaches their output.
- Not on it: 1564 and 1565 are skipped (`Curl lacks the feature wakeup`) - BL-2661. 1582 and 1587 are skipped (`Curl lacks the feature OpenSSL`), as upstream skips them on the Schannel build; no task. Filed: 1567 (protocol byte 0: expected `GET /1567 HTTP/1.1`, got the end; same cause as BL-2643) - BL-2656. 1568 (protocol byte 35, line 2: expected `Host: 127.0.0.1:8990`, got `Host: 127.0.0.1`; after BL-2606) - BL-2657. 1574 (byte 59, line 5: expected `GET /15740001`, got `IGLOO /15740001`) and 1575 (byte 130, line 7: expected `mooPOST /15750001`, got `mooIGLOO /15750001`) - BL-2658. 1576 to 1580 (`curl did not finish within 20 seconds`: the upload reads past `CURLOPT_INFILESIZE`; like BL-2645) - BL-2659. 1588 (byte 217, line 8: expected the Digest `Proxy-Authorization`, got `Accept: */*`) - BL-2660.
- Quality: the first `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 2 failing members (complexity 14 in `Lib1571.Options` and 12 in `Lib1576.TestAsync`, from `is ... or ...` case patterns); after moving the case numbers into sets and `Lib1576.FollowFor`, the second run reported 0. Full build clean (0 warnings); fast tests green in every test project (LibTest 731, Conformance 3261 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 6a271869 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1564 to lib1588 (ten programs) ported to C# over Libcurl; cases 1569-1573 and 1581 on the ratchet; failures filed as BL-2656 to BL-2661
