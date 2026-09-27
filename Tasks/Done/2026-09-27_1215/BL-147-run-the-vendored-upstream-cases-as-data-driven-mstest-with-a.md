---
id: BL-147
title: Run the vendored upstream cases as data-driven MSTest with a passing-case ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-143, BL-145, BL-146]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Console/Curl.Console.csproj]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-147 — Run the vendored upstream cases as data-driven MSTest with a passing-case ratchet

## Goal

`dotnet test` runs every vendored upstream case in process, fails on a regression of any case on
the committed passing list, and reports every other case as `Inconclusive` with its reason, so
the upstream pass rate is a number anyone can recompute.

## Context

- ADR-0013, decisions 4, 5 and 6.
- The runner is `CurlComposition.CreateRunner(standardOutput, standardError, standardInput,
  connector, datagramConnector)` in `Curl.Console/CurlComposition.cs`; `Curl.Console` needs
  `<InternalsVisibleTo Include="Curl.Conformance.UnitTests" />` beside the existing one.
- `<client><command>` is split into arguments as upstream's `runtests.pl` does (shell-style
  quoting); `%LOGDIR` is a fresh temporary directory per case (BL-145); `<client><file>` is
  written there before the run; `<verify><file>` is compared after.
- Comparison: received bytes against `<verify><protocol>` after `<strip>` / `<strippart>`;
  standard output against `<stdout>`; standard error against `<stderr>` when present; exit code
  against `<errorcode>` (0 when absent).
- Skipped with a reason, not counted as runnable: `<tool>` cases (libcurl, not the tool),
  a `<server>` the harness does not emulate yet, a `<features>` entry Curl lacks, an unknown
  variable, an unsupported `<servercmd>`.
- `http` is not yet registered in `CurlComposition.CreateProtocolHandlers`, so the first passing
  list may be short (`file://` cases, for example); that is expected.

## Acceptance criteria

- [x] One `[DynamicData]` test method in `Curl.Conformance.UnitTests`, in
      `TestCategory("Conformance")`, yields one row per vendored case, named by its test number.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` lists case numbers; a listed case that
      fails fails the test with its first difference; an unlisted failing case is `Inconclusive`
      with its first difference; an unlisted passing case is `Inconclusive` saying it can be listed.
- [x] Every case that passes on the day the task lands is on the list, and the pass rate (listed
      over runnable) is recorded in `Notes` and in `Curl.Conformance.UnitTests/CLAUDE.md`.
- [x] The whole conformance run takes under 60 seconds on the development machine (time recorded
      in `Notes`) and opens no socket.
- [x] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Emulations for FTP, IMAP, POP3, SMTP, TFTP and the other servers are follow-up tasks, filed
  once this runner exists (ADR-0013, decision 7).
- 2026-09-27 (lane 1): resumed from `factory/BL-147-wip` (cherry-picked cleanly). The earlier
  30-minute hang no longer reproduces: each curl run is bounded by a 10-second limit that
  abandons the sws emulation, and each row by a 30-second `WaitAsync`; the conformance run
  takes about 8 seconds (`dotnet test Curl.Conformance.UnitTests`, 6-8 s over five runs) and
  the whole fast suite about 15 seconds. No socket: connections go to the sws emulation, UDP to
  `UnreachableDatagramConnector`.
- test1117 (`writedelay: 100`) was flaky: `Task.Delay` on the system timer can wake just before
  the connection's timestamp reaches a write's send time, and the read then returned 0, ending
  the reply early. `SwsHttpServerConnection.WaitUntilAsync` now waits until the moment has
  passed; `WriteDelay_WhenTheTimerFiresEarly_WaitsForTheWriteInsteadOfReadingNothing` pins it
  with `ManualTimeProvider.TimersFireEarlyBy`.
- test399 and test2092 had been listed but now exit 52 instead of 3: `17e8318` (CurlUrl in
  place of System.Uri, BL-294) stopped rejecting their URLs. That is a product regression outside
  this task's touches, so they came off the list (the list holds what passes on the day it
  lands) and BL-327 puts them back.
- Pass rate on 2026-09-27: 206 listed / 477 runnable = 43.2% (271 failing, 1536 skipped, of 2013).
  Recorded in `Curl.Conformance.UnitTests/CLAUDE.md`.
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 294
  members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Lane 6 finished it, but a test in Curl.Conformance.UnitTests hung the fast-test run for 30+ minutes during integration (2026-09-26 20:29), stopping every lane. Work on branch factory/BL-147-wip; start with git cherry-pick --no-commit factory/BL-147-wip, find and fix the hang (every test must finish in seconds), then carry on.
- 2026-09-26: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lanes cleaned up and cut from 6 to 3 mid-run; partial work saved on branch factory/BL-147-wip: start with git cherry-pick --no-commit factory/BL-147-wip and carry on from it.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. dotnet test runs all 2013 vendored upstream cases in process as test<N> rows; 206 listed cases hold the ratchet (43.2% of runnable), the rest report Inconclusive with their reason
