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
completed: 2026-09-29
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

- [x] `Curl.Networking.UnitTests` pin at least: a resolution line at `info` with component `dns`; a refused first address then a successful second (`warning` then `info`, component `connect`); an HTTP CONNECT tunnel at `info` (component `proxy`); a completed handshake at `info` naming the TLS version and route (component `tls`) on the TLS path each existing test already uses; a connect timeout at `error` naming `OperationTimedOut`; a reused pooled connection at `verbose`.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- Delivered: `NetworkDiagnosticLog` (every message built only after `IsEnabled`) wired into
  `TcpConnector` (resolve, dials, CONNECT and SOCKS tunnels, both TLS paths, connect failures and
  escaping exceptions, QUIC around `QuicDialer`) and `PoolingConnector` (reuse decision). The exact
  lines and levels are in `Curl.Networking.UnitLibrary/CLAUDE.md`'s ADR-0222 paragraph. Tests:
  `TcpConnectorTests.DiagnosticLog`, `TcpConnectorQuicTests.DiagnosticLog`,
  `PoolingConnectorDiagnosticLogTests`, `NetworkDiagnosticLogTests`,
  `HandshakeCapturingTransferEventsTests`; `Curl.Networking.UnitTests` 1628 passed, 11 skipped.
  Coverage: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Choice (default taken): the TLS version, cipher suite and certificates come from the
  `TlsHandshakeEvent` the provider already reports; `TcpConnector` wraps the target's events in
  `HandshakeCapturingTransferEvents` only when `info` is on, so with the log off the provider still
  receives the target's own events and nothing changes.
- Choice: the route is `IHandshakeReportingTlsProvider.Route` (explicitly implemented by both real
  providers), not a type check; a provider that is not handshake-reporting logs `route unreported`.
- Choice: every failed dial is a `warning` (the next address is tried) and the final failure is
  one `error` line under `connect`; a failed handshake is also an `error` under `tls`, so the
  `tls` component alone shows handshake failures.
- Choice: a connect failure and an escaping exception are logged once in `ConnectAsync`, which
  catches, logs and rethrows with `throw;` so the stack is kept.
- The resolve's elapsed time reads the clock only when `info` is on, so tests pinning timestamps
  through `SteppingTimeProvider` see no extra reads.
- Left for BL-968 (filed): the reason `TlsClientRouting` chose the route, a revocation check that
  could not complete (`warning`), `UdpDatagramConnector`, and Unix socket dials. None is in the
  acceptance criteria.
- No ADR: ADR-0222 already decides levels, components and the never-logged values; the choices
  above are implementation defaults recorded here.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Curl.Networking writes dns, connect, proxy, tls and quic steps to the --log-level diagnostic log, never a credential
