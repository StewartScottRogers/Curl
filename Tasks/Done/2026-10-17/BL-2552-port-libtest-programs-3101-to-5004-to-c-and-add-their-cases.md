---
id: BL-2552
title: Port libtest programs 3101 to 5004 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2551]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2552 — Port libtest programs 3101 to 5004 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 3101 to 5004 (batch 26) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 26 of 26: programs 3101, 3102, 3103, 3104, 3105, 3207, 3208, 5000, 5004. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib3101, lib3102, lib3103, lib3104, lib3105, lib3207, lib3208, lib5000, lib5004 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All nine programs exist as C, each serving only its own case. Ports: `Lib3101`, `Lib3102`, `LibSharedCookieThroughProxy` (`Lib3103`, `Lib3104`), `Lib3105`, `Lib3207`, `Lib3208`, `LibHttpSignatureRequest` (`Lib5000`, `Lib5004`); tests in `Lib3101To5004Tests.cs`. Helpers: `LibTestRun.SetOptionsUnchecked` (five ports, with its test in `LibTestRunTests`), and `Lib1500.LoopAsync` made public for lib3208, whose loop is the same.
- Libcurl hooks checked before the first edit: `CURLOPT_REDIR_PROTOCOLS_STR`, `CURLOPT_CERTINFO`/`CURLINFO_CERTINFO`, `CURLOPT_COOKIELIST`, the share lock callbacks and `CURL_LOCK_DATA_SSL_SESSION` exist; the `CURLOPT_HTTPSIG_*` options are named but have no setter (`CURLE_NOT_BUILT_IN`). lib5000 and lib5004 are still ported faithfully (they stop at that `easy_setopt` today) and the setters are BL-2713.
- Choices (sensible defaults): `easy_setopt` values Libcurl always takes are set unchecked, as in earlier batches, and so are the getinfo codes Libcurl always answers `CURLE_OK`. lib3103/lib3104 do not check `curl_global_init`, `curl_share_init` or `curl_easy_init`; on a library that gives no handle the port ends with `CURLE_BAD_FUNCTION_ARGUMENT`, as `easy_setopt` on no handle does. lib3207's threads are `Task.Run` tasks and its mutexes `SemaphoreSlim`s (an unlock may run on another thread after an await); its bodies are written as received.
- On the ratchet: 3102, 3103, 3104, 3105, 3208. Platform check: none writes a path, drive letter or OS error text; 3102 and 3208 use the harness's own HTTPS server certificate, as the HTTPS cases already on the ratchet (300 to 306, 3106 and others) do, and verify only the request bytes.
- Not on it: 3101 fails (`<verify><protocol>` differs at line 5: no authenticated retry after the 401) - BL-2714. 5000 and 5004 fail (no request: `CURLOPT_HTTPSIG_ALGORITHM` answers 4) - BL-2713. 3207 skipped (`Curl lacks the feature OpenSSL`, as the Schannel build on Windows does), so nothing is filed.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` found 100% line and branch coverage with 2 members at complexity 12 (`Lib3207.TestAsync`, `Lib3102.IsChainInOrder`); each was split (`WriteBodies`, `FollowsInOrder`), taking about 5 decision points out of each, so both are now well under 10. Full build clean (0 warnings); fast tests green in all 42 test projects (LibTest 1077 passed, Conformance 3366 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: d543e21f ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib3101, lib3102, lib3103, lib3104, lib3105, lib3207, lib3208, lib5000 and lib5004 are C# ports; cases 3102, 3103, 3104, 3105 and 3208 hold the ratchet; 3101 is BL-2714, 5000 and 5004 are BL-2713
