---
id: BL-4254
title: Pass IDatagramCarryingConnection through SocketCallbackMultiplexedConnection
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4222]
touches: [Curl.Engine.UnitLibrary/SocketCallbackMultiplexedConnection.cs, Curl.Engine.UnitTests/SocketCallbackMultiplexedConnectionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4254 — Pass IDatagramCarryingConnection through SocketCallbackMultiplexedConnection

## Goal

An extension that gets a QUIC connection through Engine's `SocketCallbackConnector` can still send and receive datagrams: `SocketCallbackMultiplexedConnection` implements `IDatagramCarryingConnection` when the connection it wraps does.

## Context

BL-4222 (ADR-1043) added the optional interface `IDatagramCarryingConnection` in `Curl.Protocol.Abstractions.UnitLibrary`, which `QuicConnection` implements. `Curl.Engine.UnitLibrary/SocketCallbackMultiplexedConnection.cs` wraps an `IMultiplexedConnection` and implements only that interface, so a caller testing the wrapper for `IDatagramCarryingConnection` does not find it. `PoolingConnector.ConnectMultiplexedAsync` passes the inner connection straight through and `CertificateCheckingConnector` does not wrap it, so they need no change. Either make the wrapper implement the interface and forward to the inner one (0 and `InvalidOperationException` when the inner one does not carry datagrams), or return a second wrapper type that does only when the inner one does; choose the one a caller cannot tell apart from the bare QUIC connection.

## Acceptance criteria

- [x] A `SocketCallbackMultiplexedConnection` over a connection implementing `IDatagramCarryingConnection` forwards `MaxDatagramPayloadLength`, `SendDatagramAsync` and `ReceiveDatagramAsync` to it (unit test with a fake).
- [x] Over a connection that does not implement it, a caller cannot send a datagram (unit test).
- [x] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4222's lane 7 run, which checked the connector wrappers as its Context asked.
- Chose the wrapper that always implements `IDatagramCarryingConnection` (ADR-1050): every real QUIC connection is a `QuicConnection`, which always implements it, so the wrapper answers as the bare connection does, and `SocketCallbackConnector` needs no change. Over a connection without datagrams it answers 0 and `InvalidOperationException` for send and receive.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. `dotnet build` clean; fast tests green on Windows. Tests use no file system, path or platform-specific text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3d2b7b40 7e5d0c04 54cafd17 3d836d5e.
- 2026-10-29: Doing -> Done. SocketCallbackMultiplexedConnection carries datagrams as IDatagramCarryingConnection when its inner QUIC connection does
