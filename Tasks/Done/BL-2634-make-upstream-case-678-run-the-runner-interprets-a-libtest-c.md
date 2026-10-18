---
id: BL-2634
title: Make upstream case 678 run: the runner interprets a libtest client precheck line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2634 — Make upstream case 678 run: the runner interprets a libtest client precheck line

## Goal

Upstream case 678 runs through the runner and, when it passes, holds the ratchet.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test678 is skipped: `the harness does not interpret the <client><precheck> line ./libtest/libtests lib678 check`. The runner in `Curl.Conformance.UnitLibrary` should run a `libtests libNNN args` precheck through the ported program (`Lib678` answers the `check` URL) and skip the case only when the precheck prints or fails. 678 uses `CURLOPT_CAINFO_BLOB` with `test-localhost.pem` over HTTPS, so check it is platform-neutral before holding it (Schannel on Windows, OpenSSL elsewhere).

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Project at fault: `Curl.Conformance.UnitLibrary` (screening refused the `%LIBTESTS` precheck line, and the
  runner had no way to run it). Added it and `Curl.Conformance.UnitTests` to `touches` before any edit; no
  other task was in Doing on `origin/work/dark-factory`.
- New `UpstreamLibTestCheckLine.Parse` reads `./libtest/libtests libNNN args`. Screening lets such a precheck
  line through only when `libNNN` is ported; the runner's precheck is now async and runs the ported program
  in process with the words after its name (`check` for lib678), its stdout the line's output, against a fresh
  `sws` emulation and an unreachable datagram connector, under the same time limit. A program that throws or
  times out counts as exit 1, so it skips with `precheck command error`, as a crashing precheck does upstream.
  Decision (default taken): the check gets fresh, empty servers rather than the case's own, since runtests.pl's
  prechecks never feed the case's server logs.
- Result: test678's precheck passes quietly (Curl supports `CURLOPT_CAINFO_BLOB`), so the case now runs, and
  fails: no request reaches the HTTPS stand-in (`<verify><protocol>` differs at byte 0, got the end). Filed
  as BL-2725 with the differing lines; 678 is not added to the ratchet.
- Tests: `UpstreamLibTestCheckLineTests` (new), `UpstreamCaseScreeningTests.FindSkipReason_LibTestsPrecheckLine_RunsOnlyWhenItsProgramIsPorted`,
  and three `UpstreamCaseRunnerTests.RunAsync_LibTestsPrecheck*` tests. Curl.Conformance.UnitTests: 3436
  passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. The runner runs a %LIBTESTS libNNN precheck line through the ported program; test678 now runs and its remaining engine difference is BL-2725
