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
completed:
---
# BL-2364 — Give the debug callback the transfer and connection IDs for the trace-ids prefix

## Goal

While a `ManagedEasyTransfer` performs, its debug callback can learn the transfer's `%{xfer_id}` and the current connection's `%{conn_id}` (`x` before it connects), so a caller can write `--trace-ids`' `[<xfer>-<conn>] ` prefix on each line as `TraceIdsTransferEvents` does today.

## Context

Found by BL-2360. ADR-0543 keeps `DebugCallback`'s libcurl signature: curl's tool reads `CURLINFO_XFER_ID` and `CURLINFO_CONN_ID` with `curl_easy_getinfo` inside its debug callback (`src/tool_cb_dbg.c`). `ManagedEasyTransfer.Info` is set only when `PerformAsync` ends, so live values need either `Info` updated as the transfer starts and connects (the transfer ID is known at start; the connection ID comes from `TransferPerformer.TakeConnectionId`, as `ConnectionIdRecordingTransferEvents` in `Curl.Console` takes it), or another way; decide in an ADR. Needed by BL-2318 for byte-identical `--trace-ids` and `-vv` output.

## Acceptance criteria

- [ ] A `ManagedEasyTransferTests` test reads the transfer and connection IDs from the handle inside the debug callback and gets `x` (no connection) before the connection opens and the connection's number after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` passes.

## Notes

## Log

- 2026-10-15: Created.
