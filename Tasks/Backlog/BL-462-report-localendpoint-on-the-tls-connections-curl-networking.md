---
id: BL-462
title: Report LocalEndPoint on the TLS connections Curl.Networking returns, so -P - works over ftps://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-456]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-462 — Report LocalEndPoint on the TLS connections Curl.Networking returns, so -P - works over ftps://

## Goal

A connection `TcpConnector` returns for a `ConnectTarget` with `UseTls` reports the local address and port of its socket as `IConnection.LocalEndPoint`, so `curl -P - ftps://host/f` announces the control connection's own address.

## Context

- `FtpProtocolHandler` reads `-P -`'s address from `IConnection.LocalEndPoint` of the control connection the connector returned (ADR-0102, BL-437 addendum). Under `ftps://` that connection is the TLS one, whose `LocalEndPoint` is the interface default, `null`; the handler then ends with exit 30, `Failed to do PORT`.
- BL-456 makes the plain TCP connections report it; this task covers the TLS wrapper in `Curl.Networking.UnitLibrary` (find it from `TcpConnector`'s `UseTls` path) and any pooled or tunnelled wrapper that hides it.

## Acceptance criteria

- [ ] A named test in `Curl.Networking.UnitTests` connects with `UseTls` to a loopback TLS server and asserts the returned connection's `LocalEndPoint` equals the socket's local endpoint.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-437 (lane 3, 2026-09-27).

## Log

- 2026-09-27: Created.
