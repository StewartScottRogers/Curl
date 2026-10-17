---
id: BL-2527
title: Port libtest programs 500 to 509 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2556, BL-2558, BL-2559, BL-2560]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed:
---
# BL-2527 — Port libtest programs 500 to 509 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 500 to 509 (batch 1) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 1 of 26: programs 500, 501, 502, 503, 504, 505, 506, 507, 508, 509. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [ ] Each of lib500, lib501, lib502, lib503, lib504, lib505, lib506, lib507, lib508, lib509 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [ ] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [ ] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-16 (lane 1, first pass, no code written): 17 cases run these programs - test500 to
  test509, plus test538 (lib504), test580 and test581 (lib507), test585, test586, test595 and
  test596 (lib500). Every one of them that talks HTTP has a `<protocol>` with no `User-Agent`,
  which fails until BL-2559 (Doing on another lane), and test500's `<reply><data>` (no
  `<stdout>`, not `nocheck`) is judged against the `--output` file a program never writes, which
  fails until BL-2560 (Backlog). BL-2558's notes already said the port tasks need both. Without
  them the ratchet half of this task would add almost nothing and its cases would have to be
  re-run and re-filed later, so BL-2559 and BL-2560 are added to `depends-on` and the task goes
  back to Backlog. The C sources (curl-8_22_0 tag, raw.githubusercontent.com tests/libtest)
  were read: Libcurl already has open/close socket callbacks, `curl_global_init_mem`, multi and
  share; lib506 (share lock callbacks, COOKIELIST, CURLINFO_COOKIELIST) and lib509
  (`curl_easy_escape` with counting memory callbacks) are the ports most likely to show more
  Libcurl gaps when they run.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: e196db4d ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-16: Doing -> Backlog. Waits on BL-2559 (no User-Agent from a Libcurl handle) and BL-2560 (judge a lib case's reply data): every case of lib500-509 fails without them
- 2026-10-17: Backlog -> Doing. Criteria at claim: e196db4d ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
