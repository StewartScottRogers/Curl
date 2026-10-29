---
id: BL-4224
title: Pass IDatagramCarryingConnection through SocketCallbackMultiplexedConnection
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4222]
touches: [Curl.Engine.UnitLibrary/SocketCallbackMultiplexedConnection.cs, Curl.Engine.UnitTests/SocketCallbackMultiplexedConnectionTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4224 — Pass IDatagramCarryingConnection through SocketCallbackMultiplexedConnection

## Goal

An extension that gets a QUIC connection through Engine's `SocketCallbackConnector` can still send and receive datagrams: `SocketCallbackMultiplexedConnection` implements `IDatagramCarryingConnection` when the connection it wraps does.

## Context

BL-4222 (ADR-1041) added the optional interface `IDatagramCarryingConnection` in `Curl.Protocol.Abstractions.UnitLibrary`, which `QuicConnection` implements. `Curl.Engine.UnitLibrary/SocketCallbackMultiplexedConnection.cs` wraps an `IMultiplexedConnection` and implements only that interface, so a caller testing the wrapper for `IDatagramCarryingConnection` does not find it. `PoolingConnector.ConnectMultiplexedAsync` passes the inner connection straight through and `CertificateCheckingConnector` does not wrap it, so they need no change. Either make the wrapper implement the interface and forward to the inner one (0 and `InvalidOperationException` when the inner one does not carry datagrams), or return a second wrapper type that does only when the inner one does; choose the one a caller cannot tell apart from the bare QUIC connection.

## Acceptance criteria

- [ ] A `SocketCallbackMultiplexedConnection` over a connection implementing `IDatagramCarryingConnection` forwards `MaxDatagramPayloadLength`, `SendDatagramAsync` and `ReceiveDatagramAsync` to it (unit test with a fake).
- [ ] Over a connection that does not implement it, a caller cannot send a datagram (unit test).
- [ ] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4222's lane 7 run, which checked the connector wrappers as its Context asked.

## Log

- 2026-10-29: Created.
