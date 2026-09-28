---
id: BL-452
title: Report TLS trust, checked host name and proxy handshakes from Curl.Networking
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-405]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-452 — Report TLS trust, checked host name and proxy handshakes from Curl.Networking

## Goal

With the OpenSSL wording, `curl -v https://...` prints the `SSL Trust` lines, the
`subjectAltName: "host" matches cert's "host"` line and, through an HTTPS proxy,
`Proxy certificate:`, because `Curl.Networking` reports the facts BL-405 taught
`Curl.Output` to word.

## Context

- BL-405 added to `Curl.Protocol.Abstractions`: `ITransferEvents.ReportTlsTrust(TlsTrustEvent)`
  and `ReportTlsMessage(TlsMessageEvent)` (default interface members), and
  `TlsHandshakeEvent.IsProxy` and `VerifiedHostName`. Nothing reports them yet.
- `SslStreamTlsProvider` (BL-404) reports the handshake; it should also set
  `VerifiedHostName` (the URL's host as the user typed it, IP without brackets, `null`
  with `-k`) and report `TlsTrustEvent` from `TlsClientOptions` (`-k`, `--cacert`,
  `--capath`; with no `--cacert` the platform's default bundle path, which the curlimages
  build prints as `CAfile: /cacert.pem`). The HTTPS proxy's handshake is not reported at all
  yet (ADR-0085 amendment for BL-404); report it with `IsProxy = true`.
- Order measured in BL-405's Notes: `ALPN: curl offers`, then the Client hello record, then
  the `SSL Trust` lines, then the handshake. The ALPN offer line is written by the handshake
  event today, after the fact; decide how it moves ahead of the trust lines.
- Any `ITransferEvents` that forwards to another (`PooledConnection`, `PoolingConnector`)
  must forward the two new members too, or the default ones swallow them.
- `SslStream` exposes no TLS records, so `ReportTlsMessage` stays unreported; say so.

## Acceptance criteria

- [ ] A test in `Curl.Networking.UnitTests` shows `SslStreamTlsProvider` reporting a
      `TlsTrustEvent` before the handshake event, for `-k` and for `--cacert`.
- [ ] A test shows the handshake event carrying `VerifiedHostName` (and `null` with `-k`),
      and an HTTPS proxy's handshake reported with `IsProxy = true`.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Networking`.

## Notes

- Filed from BL-405 (2026-09-27).

## Log

- 2026-09-27: Created.
