---
id: BL-399
title: Carry a refused connect to the TransferResult in Dict, Gopher, Mqtt and Telnet
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-391]
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-399 — Carry a refused connect to the TransferResult in Dict, Gopher, Mqtt and Telnet

## Goal

A `dict://`, `gopher://`, `mqtt://` or `telnet://` transfer whose connect the peer refused returns a `TransferResult` with `IsConnectionRefused` set, as the HTTP handler does since BL-391, so `--retry-connrefused` can retry it.

## Context

- Found in BL-391 (2026-09-27). BL-391 added `ConnectResult.IsConnectionRefused` (set by `ConnectResult.Refused`, which `TcpConnector` returns when the last address dialled refused) and `TransferResult.IsConnectionRefused`, and made `HttpProtocolHandler` copy one to the other (`ConnectAndExchangeAsync`).
- These four handlers turn a failed `ConnectResult` into `TransferResult.Failure(exitCode, message)` and drop the flag: `DictProtocolHandler.cs`, `GopherProtocolHandler.cs`, `MqttProtocolHandler.cs`, `TelnetProtocolHandler.cs`. Each needs `with { IsConnectionRefused = connect.IsConnectionRefused }`.
- Out of scope: Ftp does not open a connection through `IConnector` yet; Tftp opens a datagram channel (`DatagramOpenResult`), where a refusal is not a connect failure.

## Acceptance criteria

- [x] For each of Dict, Gopher, Mqtt and Telnet, a unit test over a fake connector returning `ConnectResult.Refused(...)` gets exit 7 with `IsConnectionRefused` true, and one returning `ConnectResult.Failed(CurlExitCode.CouldntConnect, ...)` gets it false.
- [x] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

- Delivered directly rather than through the full /feature agent stages: a one-line change per handler with the design already fixed by BL-391 (copy `ConnectResult.IsConnectionRefused` into the failure `TransferResult`). Each handler now sets `IsConnectionRefused` in an object initializer on its existing `new TransferResult(...)`.
- Tests: a new `ExecuteAsync_ConnectRefused_ReturnsExit7MarkedConnectionRefused` in each of the four test projects, and the existing connect-failure test in each now asserts the flag is false for `ConnectResult.Failed(CouldntConnect, ...)`.
- Measure-CodeQuality: Dict, Gopher, Mqtt, Telnet at 100% line and branch, 0 failing members. `dotnet format --verify-no-changes` is clean for the eight touched projects; the solution-wide run reports pre-existing LF endings in Curl.Protocol.Abstractions.UnitLibrary (outside this task).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Dict, Gopher, Mqtt and Telnet carry a refused connect to TransferResult.IsConnectionRefused, so --retry-connrefused retries them
