---
id: BL-920
title: Log DNS, connect, proxy, TLS and QUIC steps to the diagnostic log in Curl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-920 — Log DNS, connect, proxy, TLS and QUIC steps to the diagnostic log in Curl.Networking

## Goal

`Curl.Networking.UnitLibrary` writes the diagnostic log from `ConnectTarget.DiagnosticLog`: name resolution (`dns`), each dial attempt and its outcome (`connect`), proxy tunnels (`proxy`), both TLS paths (`tls`) and QUIC dials (`quic`).

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `TcpConnector.cs` (resolve, dial, tunnel, handshake, connect timeout), `TcpDialer.cs` (each address tried, the happy-eyeballs race), `SystemDnsResolver.cs`, `DnsServerResolver.cs` and `DohDnsResolver.cs` (log their results at the connector, or pass them the target's log), `HttpProxyTunnel.cs`, `SocksProxyTunnel.cs`, `Socks4Handshake.cs`/`Socks5Handshake.cs`, `SslStreamTlsProvider.cs` and `HandBuiltTlsProvider.cs`/`HandBuiltHandshake.cs` (the route `TlsClientRouting` chose, and why), `HandBuiltCertificateVerifier.cs`/`PeerVerification.cs`, `PoolingConnector.cs` (reuse or new connection, and why), `QuicDialer.cs`, `UdpDatagramConnector.cs`. `Curl.Tls` and `Curl.Quic` get no project reference to the abstractions: log their steps here, around the calls.
- What, per level: `error` a connect, tunnel or handshake failure with the `CurlExitCode`, the .NET exception type and its message; `warning` an address that failed before another succeeded, a fallback between TLS routes, a revocation check that could not complete; `info` resolved names with the addresses and elapsed ms, the connection made (local and remote end points), a tunnel established, TLS version, cipher suite, ALPN and route; `verbose` every address tried in order, the happy-eyeballs timer, the pool key and reuse decision, the SOCKS and CONNECT exchange steps, certificate verification steps (subject, issuer, chain status).
- Credential-bearing paths: a proxy user and password (`--proxy-user`, SOCKS5 user and password) and a client key pass phrase (`--pass`).

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pin at least: a resolution line at `info` with component `dns`; a refused first address then a successful second (`warning` then `info`, component `connect`); an HTTP CONNECT tunnel at `info` (component `proxy`); a completed handshake at `info` naming the TLS version and route (component `tls`) on the TLS path each existing test already uses; a connect timeout at `error` naming `OperationTimedOut`; a reused pooled connection at `verbose`.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
