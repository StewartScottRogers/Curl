---
id: BL-3987
title: The hand-built TLS client sends close_notify when it closes an https connection, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs, Curl.Networking.UnitTests/HandBuiltTlsConnectionCloseNotifyTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3987 — The hand-built TLS client sends close_notify when it closes an https connection, as curl does

## Goal

When Curl's hand-built TLS client (`HandBuiltTlsConnection`, used for `--tls-max 1.1`, `--cert-status`, `--curves`, `--ech` and the other ADR-0140 rows) closes an https connection, the server sees the TLS `close_notify` alert real curl sends, in both builds.

## Context

Filed by BL-3984 (ADR-0959), which made `SslStreamConnection.DisposeAsync` send `close_notify` first: curl 8.18.0 (OpenSSL 3.5.5) and curl 8.22.0 (Schannel) both send `TLS 1.3, Alert [length 0002], warning close_notify` after the body against `openssl s_server -tls1_3 -ign_eof -msg` answering `HTTP/1.0 200 OK` with `Content-Length: 5`. `HandBuiltTlsConnection.DisposeAsync` disposes its `tlsStream` and the plaintext connection; whether that sends the alert was not checked. Check first (a server-side `SslStream` over `Fakes/InMemoryDuplexStream`, reading the raw bytes after the handshake, as `SslStreamConnectionCloseNotifyTests` does), and change only if it sends none; keep BL-3984's exceptions (TLS already ended by `ShutDownSendingAsync` or `ClearTlsAsync`, transport already ended, a send failure ignored).

## Acceptance criteria

- [x] A focused unit test in `Curl.Networking.UnitTests` pins that disposing a `HandBuiltTlsConnection` after a transfer sends a `close_notify` alert record, and none after TLS was cleared.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Checked first: `Tls12ClientStream` and `Tls13ClientStream` dispose without sending close_notify (their `Dispose` docs say so), so `HandBuiltTlsConnection.DisposeAsync` sent none. It now sends it through `ShutdownAsync` first, as `SslStreamConnection` does under ADR-0959. It skips the alert after `ClearTlsAsync` or `ShutDownSendingAsync` (`_tlsEnded`), and ignores an `IOException` (a TLS failure is a `TlsAlertException`) or an `InvalidOperationException` (the transport was disposed). The decision is ADR-0959's, so no new ADR.
- `ShutdownAsync` now treats a stream that is neither hand-built client as having no TLS to end. `HandBuiltTlsProviderTests.SessionTickets`' `ReadAsync_OverAStreamThatIsNotTls13_ReportsNoTicket` builds the connection over a `MemoryStream`, and the old cast threw on it at dispose. Chosen over editing that test, which is outside `touches`.
- `HandBuiltTlsConnectionCloseNotifyTests` pins both builds over TLS 1.2 and TLS 1.3 (TLS 1.3 off macOS, as the ClearTls tests do), no alert after TLS was cleared, and no throw with the transport gone or after a corrupt record. The tests are platform-neutral: no path, OS error text or platform certificate.
- Build clean, fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 97262355 779790c0.
- 2026-10-28: Doing -> Done. Closing a hand-built TLS connection sends close_notify, as curl does
