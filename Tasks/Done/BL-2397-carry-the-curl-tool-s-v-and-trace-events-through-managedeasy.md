---
id: BL-2397
title: Carry the curl tool's -v and --trace events through ManagedEasyTransfer's debug path byte for byte
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2396]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2397 — Carry the curl tool's -v and --trace events through ManagedEasyTransfer's debug path byte for byte

## Goal

A perform from options can report the curl tool's `-v`, `--trace`, `--trace-ascii`, `--trace-ids` and `--trace-config` events to the runner's `ITransferEvents` (its `HoldableStream`-backed writer, `TraceIdsTransferEvents`, `MultiStateTraceEvents` and `ConnectionIdRecordingTransferEvents`) so every byte on standard error stays the same.

## Context

Split out of BL-2394 (ADR-0506 phase 2, ADR-0552) by its lane on 2026-10-15: the runner's serial attempt context (`CurlCommandRunner`, the two `transferContextFactory.Create` calls near lines 3533 and 4377, and `FollowRetryingThroughAsync`) carries about twenty tool-side pieces, too many for one run. Each step adds the capability to `ManagedEasyTransfer.PerformFromOptionsAsync` (internal properties the tool sets, as `ToolSettings`, `Host` and `TraceComponents` already are) and tests it in `Curl.Engine.UnitTests`; `Curl.Console` keeps using `IToolAttemptTransfer.ToolAttempt` until BL-2394 switches the runner over and deletes it. `Curl.Api` and `Libcurl` handles, which set none of the new properties, must behave exactly as today.

This step: either carry the runner's events through `EasyCallbackOptions.Debug` with a tool-side adapter that rebuilds every line byte for byte, or, if the debug callback cannot carry them (ADR-0543 keeps libcurl's signature), record in an ADR the internal events hook the tool uses instead. Keep the handle's own `EngineConnectionIdTransferEvents` and `TlsVerifyResultRecordingTransferEvents` from numbering `%{conn_id}` twice when the runner's recorder is present.

## Acceptance criteria

- [x] Tests show a perform from options with the tool's events set reports the same event sequence the same attempt reports through `ToolAttempt`, `%{conn_id}` numbered once.
- [x] An ADR records the route chosen (debug callback or internal hook) and why.
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- Route: an internal hook, `ManagedEasyTransfer.ToolEvents` (`EngineToolTransferEvents`: the runner's events chain and the running transfer it keeps), not the debug callback: libcurl's `DebugCallback` signature (ADR-0543) carries only rendered bytes, so an adapter would re-render every `-v` line and guess event kinds back from text (ADR-0561).
- While set, the handle reports to it alone (no debug callback, no `TraceComponents` decorators), runs on its `State` and builds no `EngineConnectionIdTransferEvents`, so `%{conn_id}` is numbered once, by the runner's recorder; `TlsVerifyResultRecordingTransferEvents` still wraps it, as it numbers nothing.
- Tests: `ManagedEasyTransferTests.ToolEvents.cs` (3). The scripted test connector reports no connection-opened event, so the fake recorder numbers on the first request header. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. A perform from options reports the runner's -v and --trace events through the internal ToolEvents hook, conn_id numbered once (ADR-0561)
