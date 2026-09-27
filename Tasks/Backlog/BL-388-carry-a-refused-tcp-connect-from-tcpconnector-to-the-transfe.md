---
id: BL-388
title: Carry a refused TCP connect from TcpConnector to the TransferResult
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-388 — Carry a refused TCP connect from TcpConnector to the TransferResult

## Goal

A connect that failed because every address refused it reaches `TransferResult` marked as refused, so `TransferRetrier` can tell it from any other exit 7.

## Context

- Found in BL-317 (2026-09-27). curl's `--retry-connrefused` retries exit 7 only when `CURLINFO_OS_ERRNO` is `ECONNREFUSED` (upstream `src/tool_operate.c`, `retrycheck`). Today `ConnectResult` and `TransferResult` carry only the exit code and `Could not connect to server`, which curl 8.21.0 prints for every connect failure, so the cause is lost.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-27: `curl --retry 1 --retry-connrefused http://127.0.0.1:1/` retries (refused); `curl --retry 1 --retry-connrefused http://0.0.0.0:1/` (`Failed to connect to 0.0.0.0:1 after 0 ms: Could not connect to server`, exit 7) does not.
- Start at `Curl.Networking.UnitLibrary/TcpConnector.cs` (`DialFirstReachableAsync`: keep the last `SocketError`), `Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs` and `TransferResult.cs`, and `HttpProtocolHandler`'s failed-connect path. Dict, Gopher, Mqtt, Telnet, Ftp and Tftp convert a failed `ConnectResult` too; file one follow-up for them rather than widening this task.

## Acceptance criteria

- [ ] A `ConnectResult` and the HTTP handler's `TransferResult` for a connect every address refused (`SocketError.ConnectionRefused`) say so, and one that failed for another reason (for example `AddressNotAvailable`) does not; pinned in unit tests over a fake dialer.
- [ ] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

## Log

- 2026-09-27: Created.
