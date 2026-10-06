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
completed: 2026-09-27
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

- [x] A test in `Curl.Networking.UnitTests` shows `SslStreamTlsProvider` reporting a
      `TlsTrustEvent` before the handshake event, for `-k` and for `--cacert`.
- [x] A test shows the handshake event carrying `VerifiedHostName` (and `null` with `-k`),
      and an HTTPS proxy's handshake reported with `IsProxy = true`.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Networking`.

## Notes

- Filed from BL-405 (2026-09-27).
- Decisions (Decided by Claude under Stewart's delegation; for ADR-0085, see BL-451):
  - **Proxy flag rides the internal interface.** `IHandshakeReportingTlsProvider`'s method
    gained `bool isProxy`; `SslStreamTlsProvider` keeps its public four-argument overload
    (`isProxy: false`) and adds the five-argument one. `TcpConnector` reports the HTTPS
    tunnelling proxy's handshake (previously unreported) and a forward proxy's with
    `isProxy: true`, as curl's OpenSSL build says `Proxy certificate:` for both (measurement 8
    in BL-405, a forward proxy). Both land on the target's `Events`.
  - **Trust is reported after the cipher and client-certificate checks, before the
    `--cacert` file is read**, so exit 59 and exit 58 report no trust (curl fails those in
    `ossl_connect_step1`, before the store is populated) while a bad `--cacert` still shows
    the `SSL Trust Anchors:` lines it was loading. The Schannel build reports it too;
    `Curl.Output` writes nothing for it there.
  - **Default bundle name: `/cacert.pem`**, the reference build's (`curlimages/curl:8.21.0`,
    measured in BL-405), held as `SslStreamTlsProvider.OpenSslDefaultCaCertificateFile`. It is
    named, never read; verification without `--cacert` still uses the system store.
    Distribution builds print their own path (e.g. Debian's
    `/etc/ssl/certs/ca-certificates.crt`); matching the measured reference build is the
    standing rule. With only `--capath` the default file is still named, as curl keeps its
    built-in CA bundle unless `--cacert` replaces it.
  - **`VerifiedHostName`** is the host as passed to the handshake, an IPv6 literal without
    brackets, `null` under `-k` (or `--proxy-insecure` for the proxy's provider).
  - **ALPN ordering needs no move here**: the provider offers no ALPN
    (`OfferedApplicationProtocols` is empty), so no `ALPN: curl offers` line is written at
    all. Whoever adds ALPN decides where the offer line goes.
  - **No `TlsMessageEvent`**: `SslStream` exposes no TLS records, so the record lines stay
    unreported.
  - No `ITransferEvents` in `Curl.Networking` forwards to another (`PooledConnection` and
    `PoolingConnector` hold the target's events and call them directly), so nothing needed
    to forward the two new default members.
- Delivered: `SslStreamTlsProvider` (trust event, `VerifiedHostName`, `IsProxy`, five-argument
  overload), `IHandshakeReportingTlsProvider` (`isProxy`), `TcpConnector` (proxy handshakes
  reported), `CLAUDE.md`. Tests: `SslStreamTlsProviderTests.HandshakeEvent` (trust before
  handshake for `-k` and `--cacert`, default bundle, no trust on exit 59, proxy flag,
  `VerifiedHostName` rows), `TcpConnectorTests` (origin, forward proxy, HTTPS proxy flags).
- Quality: Curl.Networking 100% line, 100% branch, 0 failing members (worst CRAP 10), 778
  tests (772 passed, 6 skipped); whole fast suite green; `dotnet build -warnaserror` clean.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. OpenSSL -v now gets SSL Trust lines, the subjectAltName host line and Proxy certificate: from Curl.Networking's events
