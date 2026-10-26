---
id: BL-3846
title: Expose each connection's operating system socket handle through IConnection
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs, Curl.Networking.UnitLibrary/StreamConnection.cs, Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs, Curl.Networking.UnitLibrary/EarlyDataTlsConnection.cs, Curl.Networking.UnitLibrary/PooledConnection.cs, Curl.Networking.UnitLibrary/TcpIoTraceConnection.cs, Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs, Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Engine.UnitLibrary/SocketCallbackConnection.cs, Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/PrerequestConnector.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3846 — Expose each connection's operating system socket handle through IConnection

## Goal

Every connection the managed engine opens can say which operating system socket it runs on (or that it has none), so the multi handle can hand libcurl applications real sockets they can poll (prerequisite of BL-3563, CGR-034).

## Context

CGR-034 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): `curl_multi_socket_action` gives the socket callback synthetic numbers from 0x40000000 (`ManagedMultiTransfer.FirstTransferSocket`), so an application's poll()/WSAPoll fails on them. Real libcurl gives the TCP (or QUIC UDP) socket's descriptor. Today `Curl.Networking` never hands out its `Socket` (ADR-0599, ADR-0610, `SocketCallbackConnector`'s summary), and `IConnection` (`Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs`) has no member for it.

Add to `IConnection` a read-only member giving the underlying OS socket handle (`nint`, or none for an in-memory or test connection): the raw socket in `TcpDialer`/`StreamConnection` gives `Socket.Handle`; every wrapping connection (TLS over SslStream or hand-built, early data, pooled, trace, HTTP/2 proxy tunnel, the engine's `SocketCallbackConnection` and recording/prerequest wrappers) forwards its inner connection's. Through a proxy tunnel it is the socket to the proxy, as in libcurl. Record the shape in an ADR that amends ADR-0610's "never an operating system socket" for this member only; the CURLOPT_*SOCKETFUNCTION numbers stay as ADR-0610 decided.

## Acceptance criteria

- [ ] `IConnection` has a member giving the connection's operating system socket handle or none, documented, and every production `IConnection` implementation gives the real handle or forwards its inner connection's.
- [ ] Unit tests show a TCP connection's member equals its socket's `Handle` and that each wrapper forwards it.
- [ ] An ADR records the decision, marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Measure-CodeQuality.ps1 for the changed libraries shows no member outside a gate.

## Notes

- Filed by BL-3563's first pass (lane 5): BL-3563 needs real sockets and cannot get them from `ManagedMultiTransfer.cs` alone.

## Log

- 2026-10-26: Created.
