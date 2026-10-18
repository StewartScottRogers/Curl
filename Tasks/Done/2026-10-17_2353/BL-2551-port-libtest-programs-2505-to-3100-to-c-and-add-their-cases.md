---
id: BL-2551
title: Port libtest programs 2505 to 3100 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2550]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2551 — Port libtest programs 2505 to 3100 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 2505 to 3100 (batch 25) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 25 of 26: programs 2505, 2506, 2700, 3010, 3025, 3026, 3027, 3033, 3034, 3100. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib2505, lib2506, lib2700, lib3010, lib3025, lib3026, lib3027, lib3033, lib3034, lib3100 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C. Cases served: lib2505 (2505), lib2506 (2506), lib2700 (2700 to 2723), lib3010 (3010), lib3025 (3025), lib3026 (3026), lib3027 (3027), lib3033 (3033), lib3034 (3034), lib3100 (3100). Every Libcurl hook they need already existed (`curl_ws_send` with `CURLWS_OFFSET`, `CURLMOPT_NETWORK_CHANGED`, `CURLINFO_RETRY_AFTER`, `CURL_VERSION_THREADSAFE`, `CURLOPT_HTTP200ALIASES`, the RTSP options). Ports in `Lib2505.cs` to `Lib3100.cs`, with `Lib2700Frames` (lib2700's echo loop); tests in `Lib2505To3100Tests.cs`. `FakeEasyWebSocket` gained `FrameReceives`, `FragmentSizes` and `SendLimit`; `FakeCurlEngine.VersionInfo` became settable.
- Choices (sensible defaults): lib3026's threads are `Task.Run` tasks, each init and cleanup an injected delegate (the default a real `CurlGlobal.Init` and dispose, which never fails); `pthread_create`'s failure is left out, as .NET's threads have no such return; its version check needs a library in Libcurl, so the port runs it after a `curl_global_init` over the case's engine; its stderr line is one text for both platforms. lib3010 and lib3027 ignore the getinfo codes Libcurl always answers `CURLE_OK` on a live handle. lib3027's `start_test_timing` is left out (no `abort_on_test_timeout` follows). Unchecked `easy_setopt` calls are left out of the jumps, as in earlier batches.
- On the ratchet: 2505, 3010, 3027, 3033. Platform check: none writes a path, drive letter, OS error text or certificate (HTTP and FTP exchanges and stdout lines only).
- Not on it: 2506 fails (no `.netrc` `Authorization: Basic YmF0bWFuOnJvYmlu` on the proxied request, line 3) - BL-2701, after BL-2696. 3025 fails (stdout empty, expected `ICY 200 OK`) - BL-2702. 3034 hangs past 20 seconds - BL-2703. 2700 to 2723 skipped (`Curl lacks the feature Debug`) - BL-2698, after BL-2688. 3026 skipped (`Curl lacks the feature threadsafe`) - BL-2700. 3100 skipped (no RTSP server emulation) - BL-2699, after BL-2617.
- Quality: the first `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` found `Lib2700.DescribeFlags` at complexity 12; after making it a table lookup: 100% line, 100% branch, 1612 members, 0 failing, worst CRAP 10. Full build clean (0 warnings); fast tests green in all 42 test projects (LibTest 1051 passed, Conformance 3329 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: abfc69e7 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib2505, lib2506, lib2700, lib3010, lib3025, lib3026, lib3027, lib3033, lib3034 and lib3100 are C# ports; cases 2505, 3010, 3027 and 3033 hold the ratchet
