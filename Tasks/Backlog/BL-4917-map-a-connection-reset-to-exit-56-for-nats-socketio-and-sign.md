---
id: BL-4917
title: Map a connection reset to exit 56 for nats, socketio and signalr
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs, Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoProtocolHandler.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4917 — Map a connection reset to exit 56 for nats, socketio and signalr

## Goal

A reset connection on nats, socketio or signalr ends with `curl: (56) Recv failure: Connection was reset`, as the other broker schemes and `ws://` do.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-007 (Medium, confirmed: reproduced at least twice).

- Repro: NATS mock on 18604 that sends INFO, reads CONNECT/SUB, sends `MSG greet 1 2\r\nok\r\n`, then closes with SO_LINGER 0 (RST). `curl -sS nats://127.0.0.1:18604/greet`. For socketio/signalr: a TCP proxy that RSTs the client after 1.5 s in front of a working server.
- Expected: Exit 56, `curl: (56) Recv failure: Connection was reset`.
- Actual: `curl: (2) Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..` (raw .NET text, doubled period), exit 2.
- Where to start: `NatsProtocolHandler` catches only `NatsException`, `SocketIoProtocolHandler` only `SocketIoException`, `SignalRProtocolHandler` only transport/wire-format exceptions; reuse the `catch (IOException)` to 56/55 pattern from `RedisClientFlow.cs`.

## Acceptance criteria

- [ ] The nats repro prints `ok` then `curl: (56) Recv failure: Connection was reset` and exits 56.
- [ ] The socketio and signalr reset repros exit 56; unit tests in each library pin it.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
