---
id: BL-2529
title: Port libtest programs 520 to 537 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2528, BL-2595, BL-2596]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2529 — Port libtest programs 520 to 537 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 520 to 537 (batch 3) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 3 of 26: programs 520, 521, 523, 524, 525, 526, 530, 533, 536, 537. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib520, lib521, lib523, lib524, lib525, lib526, lib530, lib533, lib536, lib537 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): six ports written and left uncommitted for the shift to stash: `LibOneVerboseTransfer` (lib520, lib523, lib524 differ only in option lists), `Lib521`, `Lib536`, `Lib537`, registered in `LibTestPrograms.Ported`, tests in `Lib520To537Tests.cs` (LibTest tests pass, 217). Cases 520, 524, 536 pass; 521, 522, 523 fail (differences not read); 537 skips (precheck not interpreted). Left: ratchet 520, 524, 536; file 521, 522, 523, 537; one quality run; fast tests; commit. BL-2601 was a false lead (table edit had missed) and is Deferred.

- 2026-10-17 (lane 5, first turns, before any code): split for size. lib530 needs the multi socket-action API (`CURLMOPT_SOCKETFUNCTION`, `CURLMOPT_TIMERFUNCTION`, `curl_multi_socket_action`) driven by a fake that does not support it yet (`FakeMultiTransfer.SocketActionAsync` throws) and its own `select()` over a socket set, so it is BL-2595. lib525, lib526 and lib533 are multi `select_test` loops with `testnum`-dependent cleanup orders across 12 cases, filed as BL-2596. Both register their ports and add their passing cases, so once they are Done this task ports only the six single-transfer programs lib520, lib521, lib523, lib524, lib536 and lib537 (cases 520, 521, 522, 523, 524, 536, 537) and ticks the first criterion for all ten. All ten numbers exist in tests/libtest 8.22.0. Notes for that run: lib521 parses `libtest_arg2` as a port (`curlx_str_number` max 0xffff, MajorBad on failure); lib537 is the `#else` build without getrlimit/setrlimit, like Lib518 (prints `system lacks necessary system function(s)`, answers MajorBad); lib536 prints `This used the proxy` / `This DID NOT use the proxy` from `CURLINFO_USED_PROXY` to stdout.
- 2026-10-17 (lane 1, finishing run): cases 520, 524 and 536 added to the ratchet. Cases 521, 522 and 523 fail in the engine, not the ports: `CURLOPT_PORT` does not override the URL's port (522: `Host: 127.0.0.1` for `Host: 127.0.0.1:8990`; 523: `GET http://www.example.com:999/523` for `:19999/523`, Host likewise; 521: FTP did not finish within 20 seconds). Filed together as BL-2606. Case 537 is not a failure: it needs the `getrlimit` feature and the harness skips its `<client><precheck>` line, as for 518, so it stays off the ratchet with no task. `LibTestProgramsTests.Ported_*` renamed and extended to the six new program names. Quality run on `Curl.Conformance.LibTest.UnitLibrary`: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: f0df8e2d ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Backlog. Split for size: waits on BL-2595 (lib530) and BL-2596 (lib525, lib526, lib533); the six single-transfer ports follow
- 2026-10-17: Backlog -> Doing. Criteria at claim: f0df8e2d ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Backlog. Waits on BL-2601: the case runner skips cases 520 to 537 as not ported although the six ports are registered
- 2026-10-17: Backlog -> Doing. Criteria at claim: f0df8e2d ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib520, lib521, lib523, lib524, lib536, lib537 ported; cases 520, 524, 536 on the ratchet; 521-523 filed as BL-2606
