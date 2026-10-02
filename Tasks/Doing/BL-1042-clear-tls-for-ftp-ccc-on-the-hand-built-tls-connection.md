---
id: BL-1042
title: Clear TLS for FTP CCC on the hand-built TLS connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-636]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1042 — Clear TLS for FTP CCC on the hand-built TLS connection

## Goal

`HandBuiltTlsConnection` implements `IConnection.ClearTlsAsync` as `SslStreamConnection` does (ADR-0280), so `--ftp-ssl-ccc` clears TLS from the FTP control connection under `--tls-max 1.0`/`1.1` or `--cert-status` too, instead of ending with exit 81 off the Schannel build.

## Context

- BL-636 added `IConnection.ClearTlsAsync` (default: `null`, cannot clear) and implemented it on `SslStreamConnection` only (ADR-0280, decision 6). `HandBuiltTlsProvider` is chosen by `TlsClientRouting.Choose` for `--tls-max 1.0`/`1.1` and `--cert-status` (ADR-0140).
- Matching the OpenSSL build (`matchesSchannelBuild: false`): send `close_notify` first only when `sendCloseNotifyFirst` is true, read the server's `close_notify`, hand back the plaintext connection. Matching Schannel: send `close_notify` and return `null`. Data, a bare end or an I/O failure in place of the server's `close_notify` returns `null`.
- `Tls12ClientStream` and `Tls13ClientStream` (Curl.Tls.UnitLibrary) already track `CloseNotifyReceived`; they may need a way to send `close_notify` on request without closing the transport.
- Measured behaviour and the recorder (`Record-CurlExchange.ps1 -Ftp` answers `CCC` and clears TLS): BL-636's Notes.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pin `HandBuiltTlsConnection.ClearTlsAsync` over TLS 1.2 (and TLS 1.3 where the platform's server supports it) against a server-side `SslStream`, as `SslStreamConnectionClearTlsTests` does for `SslStreamConnection`: active sends `close_notify` then plain text, passive sends only plain text, the Schannel match returns `null` after sending `close_notify`, and data or an end in place of `close_notify` returns `null`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
