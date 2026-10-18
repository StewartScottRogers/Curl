---
id: BL-2547
title: Port libtest programs 1940 to 1960 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2546]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2547 — Port libtest programs 1940 to 1960 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1940 to 1960 (batch 21) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 21 of 26: programs 1940, 1945, 1947, 1948, 1955, 1956, 1957, 1958, 1959, 1960. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1940, lib1945, lib1947, lib1948, lib1955, lib1956, lib1957, lib1958, lib1959, lib1960 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C. Cases: lib1940 serves 1940 to 1944 and 1946; lib1945, lib1947, lib1948, lib1955 to lib1960 one case each. Ports `Lib1940` (`ShowHeaders`, `CheckBadArguments`), `Lib1945`, `Lib1947` (`CountRequests`), `Lib1948` (`PutBuffer`), `LibAwsSigV4Headers` (lib1955 to lib1959 as `Lib1955` to `Lib1959`, registered one by one, like `LibAwsSigV4Request`) and `Lib1960`; tests in `Lib1940To1960Tests.cs`. The fake engine gained a `Headers` property and `FakeTransferHeaders` so the header API can be driven without a network.
- Choices (sensible defaults): lib1940's `curl_easy_header` with a NULL `hout` cannot be made (a C# `out` is always there) and its `curl_easy_nextheader(NULL, ...)` check cannot fail (Libcurl answers null without the engine), so both are left out with a comment; its three bad-argument calls are made together, none having a side effect. lib1947's `easy_setopt` answers are not checked: they cannot fail on a live Libcurl handle. lib1960 checks `libtest_arg2` (dotted-quad IPv4, `curlx_inet_pton() failed` otherwise) and `libtest_arg3` (digits up to 65535) as upstream does, but hands over the opaque socket number 1960 instead of an operating system socket it connected itself: Libcurl's sockets are opaque numbers over the engine's connections (ADR-0610), so there is no OS socket a program could give it.
- On the ratchet: 1940, 1942, 1943, 1944, 1945, 1946, 1947. Platform check: each verifies only header lines on stdout (`name == value`, counts) and the HTTP protocol; no path, drive letter, OS error text or certificate.
- Not on it, filed: 1941 (`Server == from the connect` expected, `from the\tconnect` got: a folded header's tab not unfolded) - BL-2682. 1948 (first request sends `Transfer-Encoding: chunked` where upstream sends `Content-Length: 22`) - BL-2679. 1955 to 1959 skipped (`Curl lacks the feature Debug`) - BL-2681, after BL-2675. 1960 skipped (the harness does not interpret the `%LIBTESTS lib1960 check` precheck) - BL-2680.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run: 100% line, 100% branch, 1365 members, 0 failing, worst CRAP 10. Full build clean (0 warnings, 0 errors); fast tests green in every test project (LibTest 895, Conformance 3288 passed, 0 failed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 6982eec0 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1940 to lib1960 (ten programs) ported to C# over Libcurl; cases 1940 and 1942 to 1947 on the ratchet; failing and skipped cases filed as BL-2681 to BL-2680
