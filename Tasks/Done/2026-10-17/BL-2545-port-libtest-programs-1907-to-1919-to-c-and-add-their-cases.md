---
id: BL-2545
title: Port libtest programs 1907 to 1919 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2544]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2545 — Port libtest programs 1907 to 1919 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1907 to 1919 (batch 19) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 19 of 26: programs 1907, 1908, 1910, 1911, 1912, 1913, 1915, 1916, 1918, 1919. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1907, lib1908, lib1910, lib1911, lib1912, lib1913, lib1915, lib1916, lib1918, lib1919 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C; lib1913 also serves case 1914 and lib1916 case 1917. Ports `Lib1907`, `Lib1908`, `Lib1910`, `Lib1911` (`CountUnexpectedAnswers`, `IsExempt`, `IsExpectedAnswer`, `ResultFor`), `Lib1912`, `Lib1913`, `Lib1915` (`PreloadReader`, `ReadFail`, `Writer`), `Lib1916`, `Lib1918` (`CheckLookups`) and `Lib1919`; tests in `Lib1907To1919Tests.cs`.
- Choices (sensible defaults): lib1912 is ported as the program built without the GCC type-checking macros (`CURLINC_TYPECHECK_GCC_H` unset), which succeeds at once - C# has no such macros - as `Lib1587` and `Lib1678` are the builds without their feature. lib1915 uses the 64-bit `time_t` table (Curl lists `large-time`). `start_test_timing` is left out of lib1908, lib1910 and lib1913, which never check the test's time. easy_setopt and getinfo checks that cannot fail on a live Libcurl handle are left out with a comment (lib1907's `CURLINFO_EFFECTIVE_URL`, lib1915, lib1919), as earlier ports did. lib1911 and lib1918 keep their loops in public static methods taking the table and the setter or lookups, so the mismatch branches are tested with fakes.
- On the ratchet: 1910, 1912, 1913, 1914, 1918, 1919. Platform check: 1910 and 1919 check only request bytes (Basic credentials, a bearer token); 1913 and 1914 only exit code 78; 1912 nothing but exit 0; 1918 an empty stdout. No path, drive letter, OS error text or certificate in any.
- Not on it, filed: 1907 (stdout byte 15, line 1: expected `Effective URL: http://127.0.0.1:8990/1907`, got `Effective URL: 127.0.0.1:8990/hello/../1907`) - BL-2670. 1908 (alt-svc file byte 119, line 3: expected the `h1 127.0.0.1 8990 h2 ...example 443 TIMESTAMP 1 0` entry, got the end) - BL-2671. 1915 (stdout byte 171, line 6: expected `Second request returned 42`, got `[0/4] 1.example.com 25250320 01:02:03`; the second handle writes the first handle's HSTS entries) - BL-2672. 1916 (protocol byte 84, line 3: expected `client PUBLISH 3 000120`, got `client SUBSCRIBE 6 000100012000`) and 1917 (byte 77, line 3: expected the end, got the same SUBSCRIBE) - BL-2673. 1911 (exit: expected 0, got 44; Libcurl takes strings past `CURL_MAX_INPUT_LENGTH`) is BL-2655's cause; BL-2674 puts it on the ratchet once that lands.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run: 100% line, 100% branch, 1286 members, 0 failing, worst CRAP 10. Full build clean (0 warnings), `dotnet format` clean; fast tests green in every test project (LibTest 835, Conformance 3278 passed, 0 failed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 66916637 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1907 to lib1919 (ten programs) ported to C# over Libcurl; cases 1910, 1912, 1913, 1914, 1918 and 1919 on the ratchet; failures filed as BL-2670 to BL-2674
