---
id: BL-391
title: Carry a refused TCP connect from TcpConnector to the TransferResult
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-391 — Carry a refused TCP connect from TcpConnector to the TransferResult

## Goal

A connect that failed because every address refused it reaches `TransferResult` marked as refused, so `TransferRetrier` can tell it from any other exit 7.

## Context

- Found in BL-317 (2026-09-27). curl's `--retry-connrefused` retries exit 7 only when `CURLINFO_OS_ERRNO` is `ECONNREFUSED` (upstream `src/tool_operate.c`, `retrycheck`). Today `ConnectResult` and `TransferResult` carry only the exit code and `Could not connect to server`, which curl 8.21.0 prints for every connect failure, so the cause is lost.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-27: `curl --retry 1 --retry-connrefused http://127.0.0.1:1/` retries (refused); `curl --retry 1 --retry-connrefused http://0.0.0.0:1/` (`Failed to connect to 0.0.0.0:1 after 0 ms: Could not connect to server`, exit 7) does not.
- Start at `Curl.Networking.UnitLibrary/TcpConnector.cs` (`DialFirstReachableAsync`: keep the last `SocketError`), `Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs` and `TransferResult.cs`, and `HttpProtocolHandler`'s failed-connect path. Dict, Gopher, Mqtt, Telnet, Ftp and Tftp convert a failed `ConnectResult` too; file one follow-up for them rather than widening this task.

## Acceptance criteria

- [x] A `ConnectResult` and the HTTP handler's `TransferResult` for a connect every address refused (`SocketError.ConnectionRefused`) say so, and one that failed for another reason (for example `AddressNotAvailable`) does not; pinned in unit tests over a fake dialer.
- [x] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the change is one flag threaded through three existing types, and the plan fitted in the task's Context.
- `ConnectResult.Refused(message)` is a new factory (exit 7, `IsConnectionRefused` true); `Failed` and `Connected` leave the flag false. `TransferResult.IsConnectionRefused` is an init property set with `with`, like `Report`.
- `TcpConnector.DialFirstReachableAsync` now returns the `SocketError` of the last failed dial; `DialFailure` returns `Refused` when it is `ConnectionRefused`. Direct and proxy dials both use it.
- Choice: "refused" means the *last* address tried refused, not every address. That is what curl reads: `retrycheck` tests `CURLINFO_OS_ERRNO`, which holds the errno of the last failed connect attempt. It satisfies the criterion (every address refused -> the last did). Matching curl directly is not a judgement call, so no ADR was written (the Decisions README reserves ADRs for choices a reasonable person would make differently).
- `HttpProtocolHandler.ConnectAndExchangeAsync` copies the flag onto the failed `TransferResult`. BL-390 consumes it in `TransferRetrier`.
- Filed BL-399 for Dict, Gopher, Mqtt and Telnet. Ftp does not connect through `IConnector` yet and Tftp uses a datagram channel, so neither is in it.
- Quality: `Measure-CodeQuality.ps1 -IncludeIntegration` reports Networking, Abstractions and Http at 100% line and branch with 0 failing members. (Without `-IncludeIntegration`, `TcpDialer` and `UdpDatagramChannel`, unchanged here, are reached only by Integration tests.)

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A refused TCP connect reaches ConnectResult and the HTTP TransferResult as IsConnectionRefused; other exit 7s do not
