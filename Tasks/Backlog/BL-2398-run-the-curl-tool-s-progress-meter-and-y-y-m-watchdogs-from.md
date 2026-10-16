---
id: BL-2398
title: Run the curl tool's progress meter and -Y/-y/-m watchdogs from inside ManagedEasyTransfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2397]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2398 — Run the curl tool's progress meter and -Y/-y/-m watchdogs from inside ManagedEasyTransfer

## Goal

A perform from options can drive the curl tool's progress meter (`TransferProgressRecorder`, `-#` bar) and start the attempt's `-Y`/`-y` and `-m` watchdogs on the runner's `RunningTransferState` with its diagnostic log, so the meter lines and the exit 28 texts stay byte for byte the same.

## Context

Split out of BL-2394 (ADR-0506 phase 2, ADR-0552) by its lane on 2026-10-15: the runner's serial attempt context (`CurlCommandRunner`, the two `transferContextFactory.Create` calls near lines 3533 and 4377, and `FollowRetryingThroughAsync`) carries about twenty tool-side pieces, too many for one run. Each step adds the capability to `ManagedEasyTransfer.PerformFromOptionsAsync` (internal properties the tool sets, as `ToolSettings`, `Host` and `TraceComponents` already are) and tests it in `Curl.Engine.UnitTests`; `Curl.Console` keeps using `IToolAttemptTransfer.ToolAttempt` until BL-2394 switches the runner over and deletes it. `Curl.Api` and `Libcurl` handles, which set none of the new properties, must behave exactly as today.

This step: the runner's `progress`, `lowSpeedWatchdog`, `maxTimeWatchdog` and `abortToken` arguments (`transferPerformer.StartLowSpeedWatchdog` / `StartMaxTimeWatchdog` on `Running`) move into what the handle builds: the meter through `EasyCallbackOptions.TransferInfo` with a tool-side adapter, or an internal progress hook recorded in an ADR if the callback's four counts cannot carry what the recorder needs (transfer started, done); the watchdogs started by the handle on the `EngineTransferState` the tool hands it, each `--retry` attempt getting fresh ones.

## Acceptance criteria

- [ ] Tests show a perform from options with the tool's progress and state set reports the same progress sequence and starts the same watchdogs as the same attempt through `ToolAttempt`, a stalled attempt ending with the same exit 28 text.
- [ ] Each perform starts fresh watchdogs, so a `--retry` attempt gets a fresh `-m`.
- [ ] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
