---
id: BL-2360
title: Emit the engine's trace events and progress through the debug and xferinfo callbacks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2316]
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2360 — Emit the engine's trace events and progress through the debug and xferinfo callbacks

## Goal

`ManagedEasyTransfer.PerformAsync` reports every trace event the runner's `ITransferEvents` receive today (`-v`, `--trace`, `--trace-ascii` lines, the `[TIMER]`/`[READ]`/`[WRITE]` component lines) through `EasyCallbackOptions.Debug`, and the attempt's progress through `EasyCallbackOptions.TransferInfo` (xferinfo) and `Progress`, so a caller can rebuild the tool's verbose, trace and progress-meter output byte for byte from the callbacks.

## Context

ADR-0506 phase 2; found by BL-2318, which needs it to route `Curl.Console`'s serial transfers through `IEasyTransfer.PerformAsync` without changing a byte of `-v`, `--trace` or `-#` output. Today `ManagedEasyTransfer.CreateContext` passes no events, diagnostic log or progress sink. libcurl's `CURLOPT_DEBUGFUNCTION` carries `curl_infotype` (text, header in/out, data in/out, SSL data in/out); where a runner event carries more than an info type and bytes (a component name, a connection ID for the `[n-x]` prefix), decide in an ADR how it reaches the caller. If `Curl.Api`'s `DebugCallback` or `TransferInfoCallback` signatures must change, change them here (`Curl.Api.UnitLibrary` is in `touches`). Engine settings are BL-2359.

## Acceptance criteria

- [ ] `ManagedEasyTransferTests` show a transfer's request and response headers, info lines and body arriving at the debug callback with the right info type, in the order `ITransferEvents` receives them today.
- [ ] A test shows the xferinfo callback called with the download and upload totals and counts the runner's progress sink receives today, and that a non-zero return aborts the transfer with `CurlCode.AbortedByCallback`.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
