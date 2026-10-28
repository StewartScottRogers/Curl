---
id: BL-3985
title: The hand-built TLS client sends close_notify when it closes an https connection, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs, Curl.Networking.UnitTests/HandBuiltTlsConnectionCloseNotifyTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3985 — The hand-built TLS client sends close_notify when it closes an https connection, as curl does

## Goal

When Curl's hand-built TLS client (`HandBuiltTlsConnection`, used for `--tls-max 1.1`, `--cert-status`, `--curves`, `--ech` and the other ADR-0140 rows) closes an https connection, the server sees the TLS `close_notify` alert real curl sends, in both builds.

## Context

Filed by BL-3984 (ADR-0957), which made `SslStreamConnection.DisposeAsync` send `close_notify` first: curl 8.18.0 (OpenSSL 3.5.5) and curl 8.22.0 (Schannel) both send `TLS 1.3, Alert [length 0002], warning close_notify` after the body against `openssl s_server -tls1_3 -ign_eof -msg` answering `HTTP/1.0 200 OK` with `Content-Length: 5`. `HandBuiltTlsConnection.DisposeAsync` disposes its `tlsStream` and the plaintext connection; whether that sends the alert was not checked. Check first (a server-side `SslStream` over `Fakes/InMemoryDuplexStream`, reading the raw bytes after the handshake, as `SslStreamConnectionCloseNotifyTests` does), and change only if it sends none; keep BL-3984's exceptions (TLS already ended by `ShutDownSendingAsync` or `ClearTlsAsync`, transport already ended, a send failure ignored).

## Acceptance criteria

- [ ] A focused unit test in `Curl.Networking.UnitTests` pins that disposing a `HandBuiltTlsConnection` after a transfer sends a `close_notify` alert record, and none after TLS was cleared.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
