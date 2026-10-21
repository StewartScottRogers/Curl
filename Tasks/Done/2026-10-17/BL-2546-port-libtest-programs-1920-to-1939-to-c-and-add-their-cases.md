---
id: BL-2546
title: Port libtest programs 1920 to 1939 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2545]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2546 — Port libtest programs 1920 to 1939 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1920 to 1939 (batch 20) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 20 of 26: programs 1920, 1921, 1922, 1933, 1934, 1935, 1936, 1937, 1938, 1939. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1920, lib1921, lib1922, lib1933, lib1934, lib1935, lib1936, lib1937, lib1938, lib1939 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C, one case each. Ports `Lib1920`, `Lib1921` (`RunChain`, `WriteUrl`), `Lib1922`, `LibAwsSigV4Request` (lib1933 to lib1938 as `Lib1933` to `Lib1938`, registered one by one in `LibTestPrograms.Ported`, as `LibProxyTunnelUpload` serves lib1525 to lib1527) and `Lib1939` (`StartAsync`); tests in `Lib1920To1939Tests.cs`.
- Choices (sensible defaults): lib1933 to lib1938 differ only in their provider string, credentials and `POST` body, so they are one class with two option lists each (the options before `CURLOPT_HEADER` and the body after `CURLOPT_HTTPHEADER`, keeping upstream's order). lib1921 makes no `curl_global_init`; the port takes its library from `LibTestRun.WithLibraryAsync`, and a library already cleaned up stands for `curl_url()` failing (`CURLE_FAILED_INIT`). lib1922's argument check runs before `global_init`, as upstream's does. easy_setopt, `curl_slist_append` and `curl_easy_duphandle` checks that cannot fail on a live Libcurl handle are left out with a comment, as earlier ports did.
- On the ratchet: 1920, 1921, 1939. Platform check: 1920 verifies only the cookie jar's text (host `%HOSTIP` and `example.com` lines, no path); 1921 a fixed `URL: https://example.com/newpath` stdout; 1939 nothing but the exit code. No path, drive letter, OS error text or certificate in any.
- Not on it, filed: 1933 to 1938 are skipped (`Curl lacks the feature Debug`; they fix the SigV4 time with the Debug build) - BL-2675. 1922 is skipped for the same reason (it needs `CURL_HSTS_HTTP`) - BL-2676. Both need the harness's Debug stand-in in `UpstreamConformanceTests.cs`, outside this task's `touches`.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run: 100% line, 100% branch, 1325 members, 0 failing, worst CRAP 10. Full build clean (0 warnings); fast tests green in every test project (LibTest 860, Conformance 3281 passed, 0 failed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: ec047ec5 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1920 to lib1939 (ten programs) ported to C# over Libcurl; cases 1920, 1921 and 1939 on the ratchet; skipped Debug cases filed as BL-2675 and BL-2676
