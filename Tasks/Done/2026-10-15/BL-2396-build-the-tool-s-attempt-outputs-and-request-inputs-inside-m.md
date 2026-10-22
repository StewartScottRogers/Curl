---
id: BL-2396
title: Build the tool's attempt body and header outputs inside ManagedEasyTransfer from EasyOptions and callbacks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2395]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2396 — Build the tool's attempt body and header outputs inside ManagedEasyTransfer from EasyOptions and callbacks

## Goal

A perform from options can write the curl tool's body and header bytes through the handle's callbacks with the runner's stream chain intact: `--limit-rate`, `-N` flushing, `--use-ascii` text mode, the `-D` tee (`HeaderLineTeeStream`), the `-J` header watcher (`RemoteHeaderNameStream`) and `--styled-output` header styles.

## Context

Split out of BL-2394 (ADR-0506 phase 2, ADR-0552) by its lane on 2026-10-15: the runner's serial attempt context (`CurlCommandRunner`, the two `transferContextFactory.Create` calls near lines 3533 and 4377, and `FollowRetryingThroughAsync`) carries about twenty tool-side pieces, too many for one run. Each step adds the capability to `ManagedEasyTransfer.PerformFromOptionsAsync` (internal properties the tool sets, as `ToolSettings`, `Host` and `TraceComponents` already are) and tests it in `Curl.Engine.UnitTests`; `Curl.Console` keeps using `IToolAttemptTransfer.ToolAttempt` until BL-2394 switches the runner over and deletes it. `Curl.Api` and `Libcurl` handles, which set none of the new properties, must behave exactly as today.

This step: the runner's `output`, `headerOutput`, `watchHeaderOutput` and `bodyHeaderStyles` arguments to `TransferContextFactory.Create` reach the context through `EasyCallbackOptions.Write` and `Header` (or an internal output hook where a callback cannot carry the semantics, e.g. a stream the factory must wrap with the low-speed watchdog), with the same bytes and the same write order, including the write-failure behaviour `StandardOutputFailureDeferringStream` and `DeferredOutputFileStream` rely on (exit 23 texts).

## Acceptance criteria

- [x] Tests in `Curl.Engine.UnitTests` show a perform from options with the tool's output and header hooks set writes the same bytes, in the same order, as the same attempt run through `ToolAttempt`, for a body, `-i`, `-D` and `-J`.
- [x] A write callback that fails still ends the transfer with the runner's exit 23 text.
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes
- 2026-10-15 (lane 3): Added the internal `ManagedEasyTransfer.ToolOutputs` (`EngineToolTransferOutputs`: `Output`, `HeaderOutput`, `WatchHeaderOutput`, `BodyHeaderStyles`) passed to `TransferContextFactory.Create` in place of the write and header callbacks; without it the handle builds the same record from the callbacks (`OwnOutputsOf`), so `Curl.Api` and `Libcurl` handles are unchanged. Chose a stream hook over the callbacks because the factory must wrap the body in the low-speed watchdog, tee `-i` lines with `-D` and hand the header output to the `-J` watcher, which a callback cannot stand in for (ADR-0558).
- `CallbackWriteStream` now fails a short write with `OutputWriteFailedException` and `passed N returned M` (the return as given, `BytesAccepted` clamped to zero or more), so a failing write callback ends with the runner's exit 23 text; before, the HTTP handler said `returned 0` whatever the callback returned.
- Tests: `ManagedEasyTransferTests.ToolOutputs.cs` runs body, `-i`, `-D`, `-J` and `--styled-output` through `ToolOutputs` and through a `ToolAttempt` built with the same outputs and compares the ordered writes; exit 23 for a failing tool output and a failing write callback. Curl.Engine.UnitTests 1032 passed; full fast run 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. A perform from options writes the runner's body, -i, -D, -J and styled header bytes through ToolOutputs, and a failing write callback ends with the exit 23 text
