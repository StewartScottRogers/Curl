---
id: BL-968
title: Log the TLS route's reason, incomplete revocation checks, UDP and Unix socket connects to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-920]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console]
requirement: none
created: 2026-09-29
completed: 2026-10-02
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

- [x] `Curl.Networking.UnitTests` pin: a hand-built handshake under `--tls-max 1.1` logs its reason at `info` (component `tls`); a best-effort revocation acceptance logs a `warning` (component `tls`); a `UdpDatagramConnector` resolve logs at `info` (component `dns`) and its failure at `error`; a Unix socket dial logs the connection at `info` and a refused one at `error` (component `connect`).
- [x] With `NoDiagnosticLog.Instance` every existing test passes unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-920 as its follow-up.
- Route reason: `TlsClientRouting` is now a table of `(Holds, Reason)` rows; `Choose` is
  `Reason(options) is null ? SslStream : HandBuilt`, so the two can never disagree. The first row
  that holds names the reason (e.g. `--tls-max caps the versions below TLS 1.2`).
  `IHandshakeReportingTlsProvider.RouteReason` carries it (null for `SslStream`), and the `tls`
  info line ends `route HandBuilt (<reason>)`.
- Revocation: `ServerCertificateVerification.Judge` sets `PeerVerification.RevocationCheckIncomplete`
  when best effort tolerated the chain (not under `-k`); `SslStreamTlsProvider` passes it to
  `HandshakeCapturingTransferEvents.ReportRevocationCheckIncomplete` - not a new `ITransferEvents`
  member, since `Curl.Protocol.Abstractions` is outside `touches` and curl prints nothing for it.
  `TcpConnector` now wraps the events when `warning` (not `info`) is on, so a `--log-level warning`
  run sees it. The hand-built path never tolerates revocation (best effort is Schannel-only), so it
  needs no report.
- Unix socket refusal: the `connect` `error` line already comes from `ConnectWithinAsync`'s
  `Failed`; only the success `info` line (`connected to Unix socket <path>`) is new.
- UDP: `UdpDatagramConnector` takes an optional `IDiagnosticLog` (default none). A `--resolve`
  entry is logged as "from the DNS cache", as `TcpConnector` words it. Wiring it in
  `CurlComposition.CreateUdpDatagramConnector` needed `Curl.Console`; no task in Doing on
  `origin/work/dark-factory` touches it (only BL-981, Http), so it was added to `touches`.
- `Measure-CodeQuality.ps1`'s one failing member, `UdpChannelOpener.OpenFrom` line 53, is the
  intermittent gap BL-1154 already tracks; untouched here.
- No ADR: every choice sits inside ADR-0222's levels and components.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The diagnostic log names the hand-built TLS route's reason, warns on a best-effort revocation acceptance, and logs UDP resolves/channels and Unix socket connects
