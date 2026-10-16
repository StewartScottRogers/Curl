---
id: BL-2364
title: Give the debug callback the transfer and connection IDs for the trace-ids prefix
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2360]
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2364 — Give the debug callback the transfer and connection IDs for the trace-ids prefix

## Goal

While a `ManagedEasyTransfer` performs, its debug callback can learn the transfer's `%{xfer_id}` and the current connection's `%{conn_id}` (`x` before it connects), so a caller can write `--trace-ids`' `[<xfer>-<conn>] ` prefix on each line as `TraceIdsTransferEvents` does today.

## Context

Found by BL-2360. ADR-0543 keeps `DebugCallback`'s libcurl signature: curl's tool reads `CURLINFO_XFER_ID` and `CURLINFO_CONN_ID` with `curl_easy_getinfo` inside its debug callback (`src/tool_cb_dbg.c`). `ManagedEasyTransfer.Info` is set only when `PerformAsync` ends, so live values need either `Info` updated as the transfer starts and connects (the transfer ID is known at start; the connection ID comes from `TransferPerformer.TakeConnectionId`, as `ConnectionIdRecordingTransferEvents` in `Curl.Console` takes it), or another way; decide in an ADR. Needed by BL-2318 for byte-identical `--trace-ids` and `-vv` output.

## Acceptance criteria

- [x] A `ManagedEasyTransferTests` test reads the transfer and connection IDs from the handle inside the debug callback and gets `x` (no connection) before the connection opens and the connection's number after it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` passes.

## Notes

- Design (ADR-0546): `ManagedEasyTransfer.Info` is updated while the transfer performs, as `curl_easy_getinfo` reads it: a fresh `TransferInfo` with `TransferId` and `ConnectionId` -1 once the transfer ID is taken, and `EngineConnectionIdTransferEvents` gets a `connectionIdTaken` action it calls with each `%{conn_id}` it takes (opened or reused) before passing the event on, so the `Connected to` line already reads the number. No `Curl.Api` change was needed.
- Test: `PerformAsync_DebugCallbackReadsTheHandlesIds_GetsNoConnectionBeforeItOpensAndItsNumberAfter`; `ScriptedConnector` gained `ReportsTheConnectionOpened` (a `Trying` info line, then connection 1 opened) since the fake reported no connection before.
- Fast run: all green except one unrelated `Curl.Networking.UnitTests` TLS client-certificate test that passed alone; filed as BL-2372.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. The debug callback reads the running transfer's TransferId and ConnectionId (-1 before connecting) from the handle's Info
