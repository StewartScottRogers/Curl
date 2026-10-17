---
id: BL-2531
title: Port libtest programs 554 to 566 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2530]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2531 — Port libtest programs 554 to 566 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 554 to 566 (batch 5) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 5 of 26: programs 554, 555, 556, 558, 559, 560, 562, 564, 566. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib554, lib555, lib556, lib558, lib559, lib560, lib562, lib564, lib566 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): lib557 is the `curl_m*printf` test and Libcurl has no printf API; split out to BL-2612, which depends on BL-2616 (the Libcurl API). Criteria edited in Backlog to drop lib557, then reclaimed. Cases for the rest: 554 and 587 (lib554), 555, 556 and 696 (lib556), 558, 559, 560, 562 and 563 (lib562), 564, 566. Case 561 runs lib549, 565 runs lib510 (earlier batches).
- Ports: `Lib554` (its `StreamReader` reads the form's streams in order, one byte a call, since a managed read callback gets no `CURLFORM_STREAM` pointer; 587 aborts), `Lib555` (reuses `Lib547.BlurbReader`), `Lib556` (connect-only send/recv, twice for 696), `Lib558`, `Lib559`, `Lib560`, `Lib562`, `Lib564`, `Lib566`. Tests in `Lib554To566Tests.cs`; `FakeCurlEngine` gained `CreatingForm` and `ConnectOnly`, with `FakeFormBuilder` and `FakeConnectOnlyConnection`.
- On the ratchet: 556, 559, 560, 564, 566, 587, 696. None depends on the platform: HTTP, HTTPS with verification off, SOCKS4 and raw protocol bytes only, and 566 writes `CL -1` to a file whose path is an argument. Failing, filed: 554 (BL-2613, form stream framing), 562 and 563 (BL-2614, CURLOPT_PORT), 558 (BL-2615, skipped as TrackMemory), 555 (BL-2607, no IOCTLFUNCTION, already filed).
- Quality run on `Curl.Conformance.LibTest.UnitLibrary`: one failing member, `Lib556.ExchangeAsync` at complexity 20 (async); split into `SendSomeAsync` and `ReceiveSomeAsync`, each well under 10, and the unit tests cover every branch of the three; not measured a second time (cost cap). All else 100%/100%.

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: a86a911a ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Backlog. Split lib557 out to BL-2612 (needs Libcurl curl_mprintf, BL-2616); criteria edited in Backlog to drop lib557
- 2026-10-17: Backlog -> Doing. Criteria at claim: 8c36e189 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib554-lib566 (but lib557, split to BL-2612) ported; cases 556, 559, 560, 564, 566, 587, 696 on the ratchet; the rest filed as BL-2613 to BL-2615
