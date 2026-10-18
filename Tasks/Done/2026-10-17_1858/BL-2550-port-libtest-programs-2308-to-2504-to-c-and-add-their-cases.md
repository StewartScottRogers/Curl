---
id: BL-2550
title: Port libtest programs 2308 to 2504 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2549]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2550 — Port libtest programs 2308 to 2504 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 2308 to 2504 (batch 24) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 24 of 26: programs 2308, 2309, 2397, 2402, 2404, 2405, 2412, 2414, 2502, 2504. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib2308, lib2309, lib2397, lib2402, lib2404, lib2405, lib2412, lib2414, lib2502, lib2504 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C. Cases served: lib2308 (2308), lib2309 (2309), lib2397 (2397), lib2402 (2402), lib2404 (2404), lib2405 (2405, 2407), lib2412 (2412), lib2414 (2414), lib2502 (2502), lib2504 (2504). Ports `Lib2308` (its `FailWrite` and `WriteReturnedAsync` shared with lib2309), `Lib2309`, `Lib2397`, `LibPipeWaitHandles` (the shared body of lib2402, lib2404 and lib2502) with `Lib2402`, `Lib2404` and `Lib2502`, `Lib2405` with `Lib2405Run`, `Lib2412`, `Lib2414` and `Lib2504`; tests in `Lib2308To2504Tests.cs`. New harness helper `LibTestSteps.MultiInitOrMajorBad` (lib2412 and lib2414), tested in `LibTestStepsTests.Plain.cs`. The fake multi handle gained `AddResults`, `Adding` and `WaitSocketsAnswering`.
- Choices (sensible defaults): lib2405's `curl_multi_waitfds` calls with a NULL `ufds` and a size, or a NULL `fd_count`, are left out (Libcurl's span and `out` count have no NULL). Its NULL-`ufds` count query is an empty span, which Libcurl (like the engine) answers with `CURLM_OUT_OF_MEMORY` and the full count when a descriptor is waited on, so the port accepts either code there; its zero-size `ufds` calls are the too-short call. Unchecked `easy_setopt` and `multi_setopt` calls are left out, as in earlier batches, since Libcurl takes these values without fail. lib2309 checks `curl_easy_init` by hand and ends `CURLE_OK` on failure, as upstream's `if(curl)` does; lib2308 uses the `EasyInit` step, as earlier unchecked ports did.
- On the ratchet: 2308, 2397, 2504. Platform check: none writes a path, drive letter, OS error text or certificate.
- Not on it: 2309 fails (no `.netrc` `Authorization: Basic ZGFuaWVs...` on the proxied request from the duplicated handle; `<protocol>` differs at line 3) - BL-2696. 2402, 2404 and 2407 skipped on Windows (`Curl lacks the feature http/2`; the runner lists `http/2` off Windows only) - BL-2697 measures them on Linux and macOS. 2405, 2412 and 2414 skipped (`Curl lacks the feature wakeup`) - left to BL-2661, which decides the `wakeup` feature for every case that needs it. 2502 skipped (`Curl lacks the feature http/3`, listed on no platform by design).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` first found `Lib2405Run.LoopAsync` and `Lib2405Run.RunAsync` (complexity 12), an unreachable line in `Lib2405.EmptyMultiTestAsync` and an uncovered branch in `Lib2405.TestAsync`; after splitting them: 100% line, 100% branch, 1544 members, 0 failing, worst CRAP 10. Full build clean (0 warnings); fast tests green in every test project (LibTest 1007 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 1054b341 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib2308 to lib2504 (ten programs) ported to C# over Libcurl; cases 2308, 2397 and 2504 on the ratchet; the rest filed as BL-2696 and BL-2697 or left to BL-2661
