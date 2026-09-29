---
id: BL-793
title: Report remote_ip, remote_port and local_port for a Unix socket connection as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-507]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-793 — Report remote_ip, remote_port and local_port for a Unix socket connection as curl 8.21.0 does

## Goal

Through `--unix-socket <path>`, `-w '%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}'` prints curl 8.21.0's `<path cut to 45 characters>|-1||-1`, where it now prints the TCP defaults.

## Context

- Found while doing BL-507 (ADR-0149, Consequences). Measured 2026-09-28 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -UnixSocket`: a successful transfer through `C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock` printed `[C:\Users\Stewart Rogers\AppData\Local\Temp\bl|-1||-1|1]` for `[%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}|%{num_connects}]`; a refused one printed `[|-1||-1|0]`.
- The end points come from `Curl.Console/ConnectionEndPointRecorder.cs` into `TransferReport.RemoteEndPoint`/`LocalEndPoint` (Abstractions, `IPEndPoint`), formatted by `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs`. A Unix socket connection's `IConnection.RemoteEndPoint` is a `UnixDomainSocketEndPoint` and its `ConnectResult.LocalEndPoint` is null; `UnixSocketAddress.RemoteIpText` (Networking) is the text curl shows.
- HTTP keeps its own end points (ADR-0119); check what it reports through a socket.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -UnixSocket` on the reference build: the four variables after a success and after a refused connect, also for `https` if a TLS server over a socket can be arranged; copied into Notes.
- [ ] Tests in `Curl.Console.UnitTests` and `Curl.Output.UnitTests` pin the measured values for a Unix socket transfer, and TCP's unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-28: Created.
