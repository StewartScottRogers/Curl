---
id: BL-2543
title: Port libtest programs 1589 to 1648 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2542]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2543 — Port libtest programs 1589 to 1648 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1589 to 1648 (batch 17) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 17 of 26: programs 1589, 1591, 1592, 1593, 1594, 1597, 1598, 1599, 1647, 1648. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1589, lib1591, lib1592, lib1593, lib1594, lib1597, lib1598, lib1599, lib1647, lib1648 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C. lib1594 serves cases 1594 to 1596; the rest only their own case. Every Libcurl call they need existed (`CURLOPT_TRAILERFUNCTION`, `CURLOPT_DNS_SERVERS`, `CURLOPT_TIMECONDITION`, `CURLINFO_CONDITION_UNMET`, `CURLINFO_RETRY_AFTER`, `CURLOPT_PROTOCOLS_STR`, `curl_version_info`, `CURLOPT_NETRC_FILE`, `CURLOPT_PROXYPORT`, `CURLOPT_PROXY_SSL_VERIFY*`).
- Ports `Lib1589`, `Lib1591`, `Lib1592`, `Lib1593`, `Lib1594`, `Lib1597` (with `Lib1597Expectation`), `Lib1598`, `Lib1599`, `Lib1647`, `Lib1648`; tests in `Lib1589To1648Tests.cs`.
- Choices (sensible defaults, no ADR needed): the trailer callback lib1591 and lib1598 both define is `Lib1591.AddTrailers`, its OOM abort left out (a managed append cannot fail). lib1591's read callback copies from the body's start, as upstream's `memcpy(ptr, testdata, amount)` does. lib1589's `libtest_arg5` is `Arguments[4]` and its `test_argc < 5` is fewer than four arguments; its `atoi` is `Lib1589.ParseInteger`. lib1592's timeout comes from `Lib1592.TimeoutFor` over the `CURLOPT_DNS_SERVERS` answer (Libcurl accepts the option, so the port takes the c-ares path). lib1597's loop is `Lib1597.TestStrings` over an injected setter; its "protolist buffer too small" check is left out (no fixed buffer). easy_setopt and getinfo checks that cannot fail on a live Libcurl handle are left out with a comment (lib1592, lib1593, lib1594, lib1648), as earlier ports did.
- On the ratchet: 1591, 1593, 1594, 1595, 1596, 1599. Platform check: all six check only protocol bytes, stdout `Retry-After N` and the exit code over the in-memory servers; 1599's netrc path is never printed. No path, drive letter, OS error text or certificate reaches their output.
- Not on it: 1592 is skipped (reaches `a-site-never-accessed.example.org` on the internet) and 1647 is skipped (`Curl lacks the feature Debug`); no task. Filed: 1589 (byte 217, line 8: expected the Digest `Proxy-Authorization`, got `Accept: */*`; same cause as BL-2660) - BL-2662. 1648 (byte 53, line 3: expected `Proxy-Authorization: Basic dmljdGltOnNlY3JldA==`, got `Accept: */*`) - BL-2663. 1597 (stdout byte 0: expected `Tested 15 strings`, got `unexpectedly 'goobar' returned 43`) - BL-2664. 1598 (byte 226, line 11: expected `my-super-awesome-trailer: trail1`, got the empty closing line) - BL-2665.
- Quality: the first `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 3 failing members (complexity 12 in `Lib1589.ParseInteger`, one uncovered branch each in `Lib1647.TestAsync` and `Lib1597.CodeFor`); after a regular expression in `ParseInteger` and two more tests the second run reported 0 (100% line and branch). Full build clean (0 warnings); fast tests green in every test project (LibTest 774, Conformance 3267 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 50ea1f91 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1589 to lib1648 (ten programs) ported to C# over Libcurl; cases 1591, 1593-1596 and 1599 on the ratchet; failures filed as BL-2662 to BL-2665
