---
id: BL-211
title: Measure connect timings and endpoints in TcpConnector and SslStreamTlsProvider
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-160]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-211 — Measure connect timings and endpoints in TcpConnector and SslStreamTlsProvider

## Goal

`TcpConnector` and `SslStreamTlsProvider` fill `ConnectTimings` (lookup, connect, TLS handshake) from `TimeProvider` and the local and remote endpoints.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `Curl.Networking.UnitLibrary/TcpConnector.cs`, `SslStreamTlsProvider.cs`; `ConnectTimings` from BL-160.

## Acceptance criteria

- [x] Tests on `FakeTimeProvider` with the fake dialer show each timestamp and both endpoints.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered (2026-09-26, dark factory lane 4). Decisions in ADR-0029 (decided by Claude under Stewart's delegation):
  - `ITcpDialer.DialAsync` now returns `DialedTcpConnection` (connection + local end point of the socket); `TcpConnector` puts it in `ConnectResult.LocalEndPoint`. The remote end point stays on `IConnection.RemoteEndPoint` (already set by `TcpDialer`, passed through by `SslStreamConnection`). Through a proxy both are the proxy connection's, as in curl.
  - `TcpConnector` takes `Started`, `NameResolved`, `Connected` (tunnel open, through a proxy); `TlsHandshakeCompleted` is the provider's, or the moment the provider returned when it reports none.
  - `NameResolved` is always set, literal address or not: measured `curl -s -o NUL -w "%{time_namelookup}" http://127.0.0.1:1/` on curl 8.21.0 Schannel prints `0.000065`.
  - `SslStreamTlsProvider` gained a `(TlsClientOptions, TimeProvider)` overload; the one-argument constructor keeps `TimeProvider.System`, so `Curl.Console` compiles unchanged. Wiring the shared clock there is BL-277.
- Touches: added `Documentation/Planning/Decisions` for ADR-0029 and its README row; no task in Doing names it.
- Tests: `TcpConnectorTests.Timings.cs` (5) and `SslStreamTlsProviderTests.Timings.cs` (2) on the new `Fakes/SteppingTimeProvider` (a fake `TimeProvider`; the repo has no `FakeTimeProvider` package, BCL only) and `FakeTcpDialer`. Networking fast tests: 294 total, 288 passed, 6 skipped (pre-existing OpenSSL-on-Linux cipher tests).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` reports every new and changed member at 100% line and branch. The one failing member, `SslStreamTlsProvider.CreateCipherSuitesPolicy()` line 272 (the non-Windows `CipherSuitesPolicy` branch, unreachable on Windows), is pre-existing, untouched here, and owned by BL-268. Without `-IncludeIntegration` `TcpDialer` and `UdpDatagramChannel` show loopback-only lines, also pre-existing.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TcpConnector reports Started, NameResolved, Connected and the TLS handshake's end plus the local end point; SslStreamTlsProvider times its handshake (ADR-0029)
