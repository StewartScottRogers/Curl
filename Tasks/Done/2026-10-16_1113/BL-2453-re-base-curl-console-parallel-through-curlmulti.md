---
id: BL-2453
title: Re-base Curl.Console: --parallel through CurlMulti
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2452, BL-2428, BL-2432]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2453 — Re-base Curl.Console: --parallel through CurlMulti

## Goal

`--parallel` runs its transfers through `Libcurl`'s `CurlMulti`, keeping `--parallel-max`, `--parallel-immediate`, progress meter and host queueing behaviour.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Existing parallel tests and the upstream parallel cases on the ratchet pass unchanged.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan (ADR-0628): a `-Z` run holds one `CurlMulti` from `ToolCurlEngine.MultiInit`, wrapped in the new
  `ParallelTransferMulti` (`ParallelRun.Transfers`, cleaned up by `ParallelRun.EndAsync`). Every transfer now sets up
  one easy handle; a `-Z` attempt adds it and calls `curl_multi_perform` in its own flow (so its engine transfer sees
  its own `RunningTransferState`), and one loop polls, performs and reads each `CURLMSG_DONE`, removing the handle and
  handing the attempt its result. One gate serializes every multi call; an adder calls `curl_multi_wakeup` first so the
  loop's poll ends. `PerformDirectlyAsync` is gone.
- Default taken: no `CURLMOPT_MAX_HOST_CONNECTIONS`/`MAX_TOTAL_CONNECTIONS` is set; `ParallelTransferQueue` and
  `ParallelHostQueue` still decide when a transfer starts, because the engine starts a pending transfer from whichever
  flow performs next, which would give it the wrong `AsyncLocal` state. `--parallel-max`, `--parallel-immediate`, the
  meter and host queueing are therefore unchanged.
- `-Z` `-w` values now come from the handle's `curl_easy_getinfo` too (ADR-0625's rule), as `-Z` transfers have a handle.
- `ToolCurlEngine.EasyInit` now pairs handle and engine transfer under a lock (`-Z` flows ask at once).
- `CurlCommandRunnerEasyTransferTests.RunAsync_ParallelTransfer_PerformsWithoutTheEngine` pinned the old behaviour; it
  is replaced by `RunAsync_ParallelTransfersRetried_PerformEachAttemptThroughTheRunsMultiHandle`. New:
  `ParallelTransferMultiTests` (3), `ToolCurlEngineTests.MultiInit_AddsAHandleFromEasyInit_AcceptsIt`.
- Measured: full build clean; every fast test project green (Curl.Console.UnitTests 2795 passed, Curl.Conformance.UnitTests
  3130 passed, `PassingUpstreamCases.txt` unchanged); `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. -Z transfers perform through Libcurl's CurlMulti; --parallel-max, --parallel-immediate, meter and host queueing unchanged
