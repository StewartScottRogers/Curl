---
id: BL-453
title: Report closing connection after a failed connect in the HTTP handler
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-408]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-453 — Report closing connection after a failed connect in the HTTP handler

## Goal

`curl -v` on an `http://` URL whose connect fails prints `* closing connection #0` after the connector's failure lines, as curl 8.21.0 does.

## Context

- ADR-0100 (BL-408): `TcpConnector` reports `Trying`, `connect to ... failed: <reason>` and the exit 7 message, and leaves `closing connection #N` to the handler, which gets no connection (and so no number) back from a failed connect.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel): `curl -v -s http://127.0.0.1:1/` printed `*   Trying 127.0.0.1:1...`, `* connect to 127.0.0.1 port 1 from 0.0.0.0 port 56585 failed: Connection refused`, `* Failed to connect to 127.0.0.1:1 after 2025 ms: Could not connect to server` and `* closing connection #0`.
- `HttpProtocolHandler.ReportConnectionEnd` and `HttpConnectionInfoLines.Closing` already word the line for a connection the handler holds. Decide (ADR) which number a failed connect gets: curl numbers every connection it creates, failed ones included.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Http.UnitTests` over a connector returning exit 7 records `closing connection #0` as the last info line of the transfer.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Protocol.Http`.

## Notes

- Filed from BL-408 (2026-09-27).
- Measured 2026-09-27 with `curl -v -s`: curl 8.21.0 (Windows) ends a refused dial (7), a failed resolve (6) and a connect timeout (28) with `* closing connection #0`; curl 8.18.0 (WSL, OpenSSL) does the same for 7 and 28 but says `shutting down connection #0` after a failed resolve.
- Decision (ADR-0105): `HttpProtocolHandler.ConnectAndExchangeAsync` reports `closing connection #N` after every failed connect, `N` = `ConnectResult.ConnectionNumber` (`0` for a failure). Numbering failed connects needs Abstractions and Networking, filed as BL-469.
- Tests: `ExecuteAsync_ConnectRefused_ReportsClosingConnectionLast` and `ExecuteAsync_ResolveFails_ReportsClosingConnection` in `HttpProtocolHandlerTests.ConnectionReuse.cs`. Http tests 1079 pass; `Curl.Protocol.Http.UnitLibrary` 100% line and branch.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0105 and its README row; no task in Doing (BL-400, BL-463) names it.
- `Measure-CodeQuality.ps1` flags two `Curl.Console` members (`DiskWriteOutFileOpener.TryOpen`, `DumpHeaderOutputStream.WriteAsync`); neither is touched here.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -v on an http:// URL whose connect fails ends with 'closing connection #0', as curl 8.21.0 does
