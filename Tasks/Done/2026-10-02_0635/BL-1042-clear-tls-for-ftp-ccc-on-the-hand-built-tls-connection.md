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
completed: 2026-10-02
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

- [x] `Curl.Networking.UnitTests` pin `HandBuiltTlsConnection.ClearTlsAsync` over TLS 1.2 (and TLS 1.3 where the platform's server supports it) against a server-side `SslStream`, as `SslStreamConnectionClearTlsTests` does for `SslStreamConnection`: active sends `close_notify` then plain text, passive sends only plain text, the Schannel match returns `null` after sending `close_notify`, and data or an end in place of `close_notify` returns `null`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Plan: `HandBuiltTlsConnection` takes `clearsTls` (`!matchesSchannelBuild`) from `HandBuiltTlsProvider` and implements `ClearTlsAsync` as `SslStreamConnection` does (ADR-0280, decision 6). It sends `close_notify` through the existing `Tls12ClientStream`/`Tls13ClientStream.ShutdownAsync` when asked, or always when matching Schannel. It then reads once, and the read must return 0 with `CloseNotifyReceived` set. Any `IOException`, `TlsAlertException` included, returns `null`. No new ADR: this applies ADR-0280's existing decision to the second provider.
- Curl.Tls.UnitLibrary needed no change. `ShutdownAsync` already sends `close_notify` without closing the transport. Both record layers read the transport one record at a time (`ReadAtLeastAsync` of the header, then of the fragment), so no plaintext after the server's `close_notify` is swallowed into a buffer.
- Tests: `HandBuiltTlsConnectionClearTlsTests` has 24 cases. TLS 1.2 runs everywhere; TLS 1.3 is excluded on macOS, where `SslStream` has no TLS 1.3 server. A corrupt-record answer was added beside data, a bare end and an end inside a record; it covers the `IOException` branch. Under TLS 1.3 the client's `close_notify` goes out as an `application_data` record (0x17), so the TLS 1.3 tests expect that outer type.
- Measure-CodeQuality: Curl.Networking.UnitLibrary 100% line, 100% branch, 0 failing members. Full build clean with `-warnaserror`; all fast tests pass.

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. FTP CCC clears TLS on the hand-built TLS connection (--tls-max 1.0/1.1, --cert-status) as on SslStream
