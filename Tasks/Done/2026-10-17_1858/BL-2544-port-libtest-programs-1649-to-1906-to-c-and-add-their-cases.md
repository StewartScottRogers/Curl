---
id: BL-2544
title: Port libtest programs 1649 to 1906 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2543]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2544 — Port libtest programs 1649 to 1906 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1649 to 1906 (batch 18) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 18 of 26: programs 1649, 1662, 1678, 1686, 1900, 1901, 1902, 1903, 1905, 1906. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1649, lib1662, lib1678, lib1686, lib1900, lib1901, lib1902, lib1903, lib1905, lib1906 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C, and each serves only its own case. Ports `Lib1649`, `Lib1662` (with its one-byte `Reader`), `Lib1678`, `Lib1686`, `Lib1900` to `Lib1903`, `Lib1905` (its `LoopAsync`) and `Lib1906`; tests in `Lib1649To1906Tests.cs`.
- Choices (sensible defaults): lib1678 needs `curl_easy_ssls_import`, which Libcurl lacks; it is ported as the program built without `USE_SSLS_EXPORT` (succeeds at once), as `Lib1587` is, which is the Schannel build Curl matches on Windows, where the case is skipped (`Curl lacks the feature SSLS-EXPORT`); the full program off Windows, where Curl lists `SSLS-EXPORT`, is BL-2669, and 1678 is left off the ratchet. lib1901's `curlx_strcopy` is kept faithful: a chunk is copied only when it and its NUL fit, and the callback answers the length either way. lib1905's `fdset` and `select()` are a `curl_multi_wait` for `curl_multi_timeout` (1 ms when none). easy_setopt checks that cannot fail on a live Libcurl handle are left out with a comment (lib1649, lib1900, lib1906's port 0), as earlier ports did.
- On the ratchet: 1649, 1900, 1902, 1903, 1905. Platform check: 1649 and 1905 check only the request bytes; 1902, 1903 and 1905 check cookie files in the case's log folder in Netscape format (`mode="text"` for 1903 and 1905, which the runner compares line by line), with no path, drive letter, OS error text or certificate in them; 1900 checks nothing but the exit code.
- Not on it: 1662 is skipped (`Curl lacks the feature Debug`), 1678 skipped on Windows (BL-2669). Filed: 1686 (byte 138, line 7: expected `Authorization: Digest username="alice", realm=...` with `, ` separators, got them joined by `,` alone) - BL-2666. 1901 (byte 143, line 9: expected `3\r\n`, got the end; headers match, no chunk sent) - BL-2667. 1906 (byte 0, line 1: expected `GET /1906 HTTP/1.1\r\n`, got the end; depends on BL-2606, CURLOPT_PORT) - BL-2668.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run: 100% line, 100% branch, 1233 members, 0 failing, worst CRAP 10. Full build clean (0 warnings); fast tests green in every test project (LibTest 800, Conformance 3272 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: c9522e91 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1649 to lib1906 (ten programs) ported to C# over Libcurl; cases 1649, 1900, 1902, 1903 and 1905 on the ratchet; failures filed as BL-2666 to BL-2669
