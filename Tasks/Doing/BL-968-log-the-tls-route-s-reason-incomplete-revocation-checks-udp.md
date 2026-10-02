---
id: BL-968
title: Log the TLS route's reason, incomplete revocation checks, UDP and Unix socket connects to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-920]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-968 — Log the TLS route's reason, incomplete revocation checks, UDP and Unix socket connects to the diagnostic log

## Goal

The diagnostic log (`--log-level`) covers the connect steps BL-920 left out: why `TlsClientRouting` chose the hand-built route, a revocation check that could not complete, `UdpDatagramConnector`'s resolve and channel, and a Unix domain socket dial.

## Context

- BL-920 added `NetworkDiagnosticLog` and wired `TcpConnector` and `PoolingConnector`; see its Notes and `Curl.Networking.UnitLibrary/CLAUDE.md` (the ADR-0222 paragraph).
- The route reason: `TlsClientRouting.Choose` knows it (`MaximumVersion` below TLS 1.2, or `RequireCertificateStatus`); expose it (e.g. `TlsClientRouting.Reason(options)`) and let `HandBuiltTlsProvider` report it through `IHandshakeReportingTlsProvider`, then add it to the `tls` info line.
- Revocation: `SslStreamTlsProvider`'s `--ssl-revoke-best-effort` path accepts an offline or unknown status; that acceptance is a `warning` (ADR-0222 decision 2). The provider has no log today, so pass the result back (e.g. on the handshake event or a new result field) and log it in `TcpConnector`.
- `UdpDatagramConnector` (TFTP, Kerberos KDC over UDP) resolves and opens channels without logging; `ConnectOverUnixSocketAsync` in `TcpConnector` logs nothing.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pin: a hand-built handshake under `--tls-max 1.1` logs its reason at `info` (component `tls`); a best-effort revocation acceptance logs a `warning` (component `tls`); a `UdpDatagramConnector` resolve logs at `info` (component `dns`) and its failure at `error`; a Unix socket dial logs the connection at `info` and a refused one at `error` (component `connect`).
- [ ] With `NoDiagnosticLog.Instance` every existing test passes unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-920 as its follow-up.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
