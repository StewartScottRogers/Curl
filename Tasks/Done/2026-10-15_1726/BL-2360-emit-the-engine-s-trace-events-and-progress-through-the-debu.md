---
id: BL-2360
title: Emit the engine's trace events and progress through the debug and xferinfo callbacks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2316]
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Output.UnitLibrary/Curl.Output.UnitLibrary.csproj]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2360 — Emit the engine's trace events and progress through the debug and xferinfo callbacks

## Goal

`ManagedEasyTransfer.PerformAsync` reports every trace event the runner's `ITransferEvents` receive today (`-v`, `--trace`, `--trace-ascii` lines, the `[TIMER]`/`[READ]`/`[WRITE]` component lines) through `EasyCallbackOptions.Debug`, and the attempt's progress through `EasyCallbackOptions.TransferInfo` (xferinfo) and `Progress`, so a caller can rebuild the tool's verbose, trace and progress-meter output byte for byte from the callbacks.

## Context

ADR-0506 phase 2; found by BL-2318, which needs it to route `Curl.Console`'s serial transfers through `IEasyTransfer.PerformAsync` without changing a byte of `-v`, `--trace` or `-#` output. Today `ManagedEasyTransfer.CreateContext` passes no events, diagnostic log or progress sink. libcurl's `CURLOPT_DEBUGFUNCTION` carries `curl_infotype` (text, header in/out, data in/out, SSL data in/out); where a runner event carries more than an info type and bytes (a component name, a connection ID for the `[n-x]` prefix), decide in an ADR how it reaches the caller. If `Curl.Api`'s `DebugCallback` or `TransferInfoCallback` signatures must change, change them here (`Curl.Api.UnitLibrary` is in `touches`). Engine settings are BL-2359.

## Acceptance criteria

- [x] `ManagedEasyTransferTests` show a transfer's request and response headers, info lines and body arriving at the debug callback with the right info type, in the order `ITransferEvents` receives them today.
- [x] A test shows the xferinfo callback called with the download and upload totals and counts the runner's progress sink receives today, and that a non-zero return aborts the transfer with `CurlCode.AbortedByCallback`.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).

## Notes

- Decided in ADR-0543: `DebugCallback` and `TransferInfoCallback` keep libcurl's signatures, so `Curl.Api.UnitLibrary` is unchanged. `DebugCallbackTransferEvents` (new, `Curl.Engine`) hands every event over under `CURLOPT_VERBOSE` only, with libcurl's info types; structured events (connection opened/reused, TLS handshake/trust/message) become the `CURLINFO_TEXT` lines `-v` prints, worded by `Curl.Output`'s internal `TransferEventInfoText` for the platform's build; SSL data only in the OpenSSL build, as Schannel's libcurl passes none. `CallbackTransferProgress` (new) calls xferinfo, else progress, unless `CURLOPT_NOPROGRESS` (libcurl's default 1); a non-zero, non-`CURL_PROGRESSFUNC_CONTINUE` return cancels a token linked to the caller's and the perform returns `AbortedByCallback` with libcurl's `Callback aborted`, whether the cancel stopped the attempt or it finished first.
- Touches widened to `Curl.Output.UnitLibrary/Curl.Output.UnitLibrary.csproj` (one `InternalsVisibleTo` for `Curl.Engine.UnitLibrary`, so the debug text reuses `-v`'s wording rather than a copy); no task in Doing on `origin/work/dark-factory` touched it.
- `ScriptedConnector` (Engine tests) gained `ObservesCancellation`, so a test can show an abort stopping the read mid-body.
- Left as follow-ups: the `--trace-config` decorators (`[READ]`/`[WRITE]`/`[TIMER]` lines the runner adds) need an engine trace setting - BL-2363; the `--trace-ids` `[xfer-conn]` IDs need live `CURLINFO_XFER_ID`/`CONN_ID` during a perform - BL-2364. Handler-emitted component lines already pass through as text.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10 (after splitting `FinishPerformed` out of `PerformAsync`, which was at complexity 12). Fast tests: 0 failures across the solution.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. ManagedEasyTransfer hands every trace event to the debug callback under CURLOPT_VERBOSE and its progress to the xferinfo/progress callback, whose non-zero return aborts with AbortedByCallback
