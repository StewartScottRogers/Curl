---
id: BL-478
title: Report the transfer done before the HTTP connection-end -v line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-478 — Report the transfer done before the HTTP connection-end -v line

## Goal

`ITransferProgress` has a `ReportTransferDone()` member, and the HTTP handler calls it once the response body is complete, before it reports the connection-end `-v` line (`left intact`, `closing connection`, `shutting down connection`).

## Context

- curl 8.21.0 draws the progress meter's done status lines and the newline that ends them in `Curl_pgrsDone`, before `multi_done` writes `* Connection #0 to host ... left intact`. Measured 2026-09-27 (BL-411 Context): `{ [6 bytes data]`, then three `\r100 ...` status lines and a line feed, then `* Connection #0 to host 127.0.0.1:18421 left intact`.
- `Curl.Console` draws the done lines only after the handler returns (`CurlCommandRunner.WriteProgressAsync`), and the HTTP handler has already reported the connection end by then (`HttpProtocolHandler.ReportConnectionEnd`), so under `-v` without `-s` the meter lands after that line. `Curl.Console` has no way to tell when the transfer is done without a signal from the handler; BL-411 wires the Console side once this signal exists.
- Add the member to `Curl.Protocol.Abstractions.UnitLibrary/ITransferProgress.cs` with an empty default body (so handlers that never call it and sinks that ignore it need no change), implement it in `NoTransferProgress`, forward it in `Curl.Core.UnitLibrary/LowSpeedWatchdog.cs`'s `UploadWatchingProgress` and in `Curl.Protocol.Http.UnitLibrary/HttpTransferProgress.cs`, and call it in `HttpProtocolHandler.ConnectAndExchangeAsync` before `ReportConnectionEnd` for a final (non-retried) exchange. Record the contract change in an ADR amending ADR-0045.

## Acceptance criteria

- [x] `ITransferProgress.ReportTransferDone()` exists, documented as "the transfer's data is complete and the handler is about to report what became of its connection"; `NoTransferProgress` and `LowSpeedWatchdog`'s wrapper pass it on, each pinned by a unit test.
- [x] An `HttpProtocolHandler` test over a scripted connection records, in one ordered list of progress and event calls, `ReportTransferDone` after the last `ReportDownloaded` and before the `Connection #0 to host ... left intact` info line; a request the handler retries (401 answered, dead reused connection) reports it once, for the final exchange only.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for the three libraries touched.

## Notes

- Filed from BL-411 (2026-09-27), which needs this signal to draw the meter's done lines before the connection-end `-v` line.
- 2026-09-27 (BL-478 run): `Documentation/Planning/Decisions` added to `touches` for ADR-0111, which the Context asks for; no task in Doing names it (BL-413 touches Networking and Console only).
- Delivered in-session rather than through the full `/feature` agent chain: a five-file contract addition whose design the Context already spelled out.
- Decision (ADR-0111): the handler reports done for the final exchange whether it succeeded or failed, as `multi_done` calls `Curl_pgrsDone` for both; a failed connect still reports nothing to the progress sink (ADR-0045). A `-L` redirect inside the handler is still one run, so it reports done once.
- `RecordingTransferProgress` now records `done`, and takes an optional shared log so a test orders progress reports against `RecordingTransferEvents.Events`; existing exact-list tests gained a trailing `done`.
- Tests: `ExecuteAsync_KeptAliveResponse_ReportsDoneAfterTheLastCountAndBeforeLeftIntact`, `ExecuteAsync_ChallengeAnsweredOnTheSameConnection_ReportsDoneOnceForTheFinalExchange`, `ExecuteAsync_ChallengeWithConnectionClose_ReportsStartedOnceAcrossBothConnections` (401 on a closed connection), `ExecuteAsync_ReusedConnectionDiesBeforeTheResponse_ReportsDoneOnlyForTheResentRequest`, `HttpTransferProgressTests.ReportTransferDone_PassesItOn`, `NoTransferProgressTests`, `LowSpeedWatchdogTests.WatchProgress_PassesStartedDownloadedAndDoneReportsThrough`.
- Measure-CodeQuality: Abstractions, Core and Http each 100% line, 100% branch, 0 failing members, worst CRAP 10.
- `dotnet format --verify-no-changes` still reports line-ending errors in `Curl.Output.UnitTests/TraceTransferEventWriterOpenSslTlsTests.cs`, which predate this task and are outside its `touches`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ITransferProgress.ReportTransferDone exists and the HTTP handler reports it once, for the final exchange, before the connection-end -v line
