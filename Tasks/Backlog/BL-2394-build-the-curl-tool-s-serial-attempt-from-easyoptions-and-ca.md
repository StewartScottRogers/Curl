---
id: BL-2394
title: Build the curl tool's serial attempt from EasyOptions and callbacks inside ManagedEasyTransfer, retiring ToolAttempt
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2318, BL-2395, BL-2396, BL-2397, BL-2398]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2394 — Build the curl tool's serial attempt from EasyOptions and callbacks inside ManagedEasyTransfer, retiring ToolAttempt

## Goal

`CurlCommandRunner`'s serial attempts no longer hand `ManagedEasyTransfer` a prepared `ToolAttempt`: the handle builds the attempt's context itself from its `EasyOptions`, `ToolSettings`, `Host` and `TraceComponents`, body and header bytes reach the runner through `EasyCallbackOptions.Write` and `Header`, `-v`/`--trace` through `Debug` and the meter through `TransferInfo`, and the output stays byte for byte the same.

## Context

ADR-0506 phase 2, ADR-0552. BL-2318 routed every serial attempt through `IEasyTransfer.PerformAsync` on one easy handle per transfer, but through the internal `IToolAttemptTransfer.ToolAttempt` hook, carrying the context the runner builds (`CurlCommandRunner.FollowRetryingThroughAsync`; `TransferContextFactory.Create` with the tool's proxy, credentials, SSH options, `If-None-Match` lines, alt-svc cache, `-J` header watcher, rate limit, styled headers, progress recorder and watchdogs on `RunningTransferState`). Moving each of those into what `ManagedEasyTransfer.PerformFromOptionsAsync` builds, then deleting `ToolAttempt`, `IToolAttemptTransfer` and `ToolAttemptResult`, is this task. The runner still needs the whole `TransferResult` for `--retry` and `-w`, so decide in an ADR how it gets it (`Info` alone does not carry the report). Split it with the board script in the first turns if it does not fit one run.

## Acceptance criteria

- [ ] `ToolAttempt` and `IToolAttemptTransfer` are gone and `CurlCommandRunnerEasyTransferTests` still shows one `PerformAsync` per attempt.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- 2026-10-15 (lane 2, first claim): sized before any edit and split. The runner's serial context (`CurlCommandRunner` lines ~3533 and ~4377, `FollowRetryingThroughAsync`) carries about twenty tool-side pieces - the output chain (`--limit-rate`, `-N`, `--use-ascii`, gated standard output), the `-D` tee, the `-J` watcher, styled headers, the progress recorder, the run's `-v`/trace events with their `HoldableStream`, the `-Y`/`-y`/`-m` watchdogs on `RunningTransferState`, the proxy and credential choice, SSH options, `If-None-Match` lines, the alt-svc cache, range, offset, form body and upload - each of which must stay byte for byte the same. That is several runs, so the engine-side capability moves first, one step per task: BL-2395 (the whole-`TransferResult` ADR and the per-transfer inputs), BL-2396 (body and header outputs through the callbacks), BL-2397 (`-v`/trace through the debug path), BL-2398 (progress meter and watchdogs). This task stays the last step: switch `FollowRetryingThroughAsync` to a perform from options with those hooks set, drop the runner's duplicate setup, and delete `ToolAttempt`, `IToolAttemptTransfer` and `ToolAttemptResult` with `ManagedEasyTransferTests.ToolAttempt.cs` folded into the from-options tests. No code was changed in this claim.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Split: too big for one run; waits on BL-2395, BL-2396, BL-2397 and BL-2398, which move the runner's attempt wiring into ManagedEasyTransfer step by step
