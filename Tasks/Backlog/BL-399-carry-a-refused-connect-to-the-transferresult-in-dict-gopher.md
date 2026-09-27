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
completed:
---
# BL-399 — Carry a refused connect to the TransferResult in Dict, Gopher, Mqtt and Telnet

## Goal

A `dict://`, `gopher://`, `mqtt://` or `telnet://` transfer whose connect the peer refused returns a `TransferResult` with `IsConnectionRefused` set, as the HTTP handler does since BL-391, so `--retry-connrefused` can retry it.

## Context

- Found in BL-391 (2026-09-27). BL-391 added `ConnectResult.IsConnectionRefused` (set by `ConnectResult.Refused`, which `TcpConnector` returns when the last address dialled refused) and `TransferResult.IsConnectionRefused`, and made `HttpProtocolHandler` copy one to the other (`ConnectAndExchangeAsync`).
- These four handlers turn a failed `ConnectResult` into `TransferResult.Failure(exitCode, message)` and drop the flag: `DictProtocolHandler.cs`, `GopherProtocolHandler.cs`, `MqttProtocolHandler.cs`, `TelnetProtocolHandler.cs`. Each needs `with { IsConnectionRefused = connect.IsConnectionRefused }`.
- Out of scope: Ftp does not open a connection through `IConnector` yet; Tftp opens a datagram channel (`DatagramOpenResult`), where a refusal is not a connect failure.

## Acceptance criteria

- [ ] For each of Dict, Gopher, Mqtt and Telnet, a unit test over a fake connector returning `ConnectResult.Refused(...)` gets exit 7 with `IsConnectionRefused` true, and one returning `ConnectResult.Failed(CurlExitCode.CouldntConnect, ...)` gets it false.
- [ ] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

## Log

- 2026-09-27: Created.
