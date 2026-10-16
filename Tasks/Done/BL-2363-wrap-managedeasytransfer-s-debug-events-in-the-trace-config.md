---
id: BL-2363
title: Wrap ManagedEasyTransfer's debug events in the --trace-config component decorators
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2360]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2363 — Wrap ManagedEasyTransfer's debug events in the --trace-config component decorators

## Goal

Under `--trace-config read`, `write` and `timer`, the debug callback of a `ManagedEasyTransfer` receives the `[READ]`, `[WRITE]` and `[TIMER]` lines that `CurlCommandRunner.WithTraceLineEvents` adds today through `ClientReaderResetTraceEvents`, `ClientWriterTraceEvents` and `ResponseWaitTimerTraceEvents`, in the same places.

## Context

Found by BL-2360 (ADR-0543): `DebugCallbackTransferEvents` passes on every event the handlers and connectors report, but the decorators that add the component lines are applied by the runner from `EngineTraceComponents`, which the engine has no setting for. libcurl takes them from `curl_global_trace`; add an internal `EngineTraceComponents?` hook on `ManagedEasyTransfer` (like `ToolSettings`, ADR-0542) or an engine-wide setting, decide which in an ADR, and wrap the debug events with the decorators the runner uses (the `[MULTI]` lines' `MultiStateTraceEvents` stays in `Curl.Console`; leave those to BL-2318 or file them). Needed by BL-2318 for byte-identical `--trace-config` output.

## Acceptance criteria

- [x] A `ManagedEasyTransferTests` test shows the debug callback receiving `[READ] client_reset, clear readers` and the `[WRITE]` lines under `read` and `write` trace components, in the order the runner's events receive them.
- [x] A test shows the response wait's `[TIMER]` lines after `Request completely sent off` under the `timer` component.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` passes.

## Notes

- Decided (ADR-0545): an internal `ManagedEasyTransfer.TraceComponents` hook (default `EngineTraceComponents.None`, copied by `Duplicate`, kept by `Reset` as a global setting) rather than an engine-wide setting, beside `ToolSettings` and `Host`; no public API change. `WithTraceComponentEvents` wraps the debug events as the runner does: timer innermost, read, write outermost.
- The `[TIMER]` lines are the runner's multi-not-traced form (`WaitLines(tracesExpiry: false)`): the engine has no multi; `MultiStateTraceEvents` and `AsyncResolveTeardownTraceEvents` stay in `Curl.Console` for BL-2318.
- Measured: Curl.Engine.UnitLibrary 0 failing members; Curl.Engine.UnitTests 971 passed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. ManagedEasyTransfer's debug callback receives the --trace-config read, write and timer lines through its TraceComponents hook
