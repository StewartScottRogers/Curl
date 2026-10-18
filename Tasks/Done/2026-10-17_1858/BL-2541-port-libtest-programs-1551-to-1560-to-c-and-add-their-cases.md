---
id: BL-2541
title: Port libtest programs 1551 to 1560 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2540]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2541 — Port libtest programs 1551 to 1560 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1551 to 1560 (batch 15) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 15 of 26: programs 1551, 1552, 1553, 1554, 1555, 1556, 1557, 1558, 1559, 1560. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1551, lib1552, lib1553, lib1554, lib1555, lib1556, lib1557, lib1558, lib1559, lib1560 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten exist as C; each is run only by its own case (`lib%TESTNUMBER`). lib1560 is the 2549-line URL API test (about 480 `curl_url` table rows and 16 check functions): too big for a batch, so split out as BL-2652 before any code, as BL-2538 split lib1521 to BL-2642. Every Libcurl call the other nine need existed (`curl_easy_recv`/`send`, the share lock callbacks, `CURLMOPT_MAX_HOST_CONNECTIONS`, `CURLINFO_PROTOCOL`, `curl_url_set`, MIME, the progress and xferinfo callbacks).
- Ports `Lib1551` to `Lib1559`; tests in `Lib1551To1559Tests.cs`. `Lib1552.WaitLoopAsync` is the counted `curl_multi_wait` loop lib1552 (3 turns) and lib1553 (1 turn) share.
- Choices (sensible defaults, no ADR needed): as in BL-2539 and BL-2540, checks Libcurl cannot fail on a live handle are left out so no untestable branch is added: lib1557's `multi_setopt(CURLMOPT_MAX_HOST_CONNECTIONS, 1)` and lib1558's `curl_easy_getinfo(CURLINFO_PROTOCOL)` error line, and lib1559's `if(u)` after `curl_url()`. lib1551 and lib1554 keep upstream's unchecked `curl_easy_init` (`if(curl)`), and lib1554 its `curl_share_init() failed` line with `CURLE_OK`. lib1556's failure line is upstream's `lib1556.c:41`.
- On the ratchet: 1551, 1552, 1553, 1556, 1557, 1558. Platform check: 1551, 1552, 1553 and 1557 check only exit code and protocol bytes over the in-memory servers; 1556 fixed stdout; 1558 prints `hello` and `Protocol: 400` from a `file://%FILE_PWD/...` URL, whose path never reaches the output (no drive letter or separator in it).
- Not on it, filed: 1554 (stdout byte 88, line 5: expected `-] Mutex lock CONNECT`, got `run 1: foobar and so on fun!`; the engine locks only SHARE) - BL-2653. 1555 (stdout byte 24, line 1: expected `curl_easy_recv returned 93`, got `... returned 1`) - BL-2654. 1559 (stdout byte 34, line 1: expected `CURLOPT_URL 10000000 bytes URL == 43`, got `== 0`; the three `CURLUPART_*` lines answer `0 (No error)`, not `3`) - BL-2655.
- Quality: the first `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 2 failing members (the null `TimeProvider` branch of the `Lib1552` and `Lib1553` constructors); after a test for them, the second run reported 0. Full build clean (0 warnings); fast tests green in every test project (LibTest 680, Conformance 3255 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: c779ac44 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1551 to lib1559 ported to C# over Libcurl; cases 1551-1553 and 1556-1558 on the ratchet; lib1560 split to BL-2652, 1554/1555/1559 filed as BL-2653 to BL-2655
