---
id: BL-465
title: Report LocalEndPoint on the TLS connections Curl.Networking returns, so -P - works over ftps://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-456]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-465 — Report LocalEndPoint on the TLS connections Curl.Networking returns, so -P - works over ftps://

## Goal

A connection `TcpConnector` returns for a `ConnectTarget` with `UseTls` reports the local address and port of its socket as `IConnection.LocalEndPoint`, so `curl -P - ftps://host/f` announces the control connection's own address.

## Context

- `FtpProtocolHandler` reads `-P -`'s address from `IConnection.LocalEndPoint` of the control connection the connector returned (ADR-0102, BL-437 addendum). Under `ftps://` that connection is the TLS one, whose `LocalEndPoint` is the interface default, `null`; the handler then ends with exit 30, `Failed to do PORT`.
- BL-456 makes the plain TCP connections report it; this task covers the TLS wrapper in `Curl.Networking.UnitLibrary` (find it from `TcpConnector`'s `UseTls` path) and any pooled or tunnelled wrapper that hides it.

## Acceptance criteria

- [x] A named test in `Curl.Networking.UnitTests` connects with `UseTls` to a loopback TLS server and asserts the returned connection's `LocalEndPoint` equals the socket's local endpoint.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-437 (lane 3, 2026-09-27).
- The production forwarding already landed with BL-456: `SslStreamConnection.LocalEndPoint` returns the plaintext connection's, and `PooledConnection` forwards its underlying one. No production change was needed; this task adds the loopback TLS test `TcpConnectorTests.ConnectAsync_WithTlsToALoopbackTlsServer_ReturnsAConnectionThatReportsItsSocketsLocalEndPoint` (Integration: real `TcpDialer`, real `SslStreamTlsProvider` with `-k`, a self-signed server certificate made in the test) that pins it end to end.
- Pipeline: with no production code to plan or implement, the feature stages reduced to test, verify and document; the Networking `CLAUDE.md` now names the TLS case among the loopback tests.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A TLS connection TcpConnector returns reports its socket's LocalEndPoint, pinned by a loopback TLS test, so -P - works over ftps://
