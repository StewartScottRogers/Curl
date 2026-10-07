---
id: BL-819
title: Fail a TLS read that ends without close_notify with curl's exit 56 text on both TLS paths
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-819 — Fail a TLS read that ends without close_notify with curl's exit 56 text on both TLS paths

## Goal

When an `https://` transfer needs more bytes and the server's TLS connection ends without `close_notify`, Curl fails the transfer with exit 56 and the platform curl's text, on the `SslStream` path and the hand-built path alike, as curl's OpenSSL and Schannel builds do.

## Context

- ADR-0157 decision 1: `Tls13ClientStream` and `Tls12ClientStream` return 0 at a bare transport end and say so with `CloseNotifyReceived`; the caller maps an unfinished transfer to exit 56. ADR-0157 quotes the texts: OpenSSL `OpenSSL SSL_read: error:0A000126:SSL routines::unexpected eof while reading` (curl appends `, errno 0`; measure it), Schannel `schannel: server closed abruptly (missing close_notify)`.
- ADR-0162 decision 7 (BL-708): `HandBuiltTlsConnection` and `SslStreamConnection` both return 0 today, so the two paths agree; the HTTP handler reports a thrown `IOException` as `Recv failure: ...`, not curl's text, so the failure needs a typed exception the handlers map (compare `MultiplexedConnectionFailedException` in `Curl.Protocol.Abstractions.UnitLibrary`).
- `SslStream` exposes no `close_notify` flag: find how to tell a bare end from `close_notify` on that path (or record why it cannot and what Curl does instead) in the ADR this task writes.
- Measure real curl with `Record-CurlExchange.ps1` (a TLS server that sends a read-until-close body and closes without `close_notify`) on the Schannel build and the OpenSSL build under WSL before pinning text.

## Acceptance criteria

- [x] A measured ADR records both builds' exit and text for a read-until-close body ended without `close_notify`, and for one ended with it.
- [x] `Curl.Networking.UnitTests` pin that `HandBuiltTlsConnection` fails such a read with the typed failure and returns 0 after `close_notify`; `Curl.Protocol.Http.UnitTests` pin the handler's exit 56 and message for the typed failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Filed by BL-708 (ADR-0162).
- Added `Record-CurlExchange.ps1` (new `-TlsCloseNotify` switch, needed to measure the with-close_notify case) and `Documentation/Planning/Decisions` (ADR-0221) to `touches`; no task in Doing names either.
- Decisions in ADR-0221: typed `MissingCloseNotifyException` in Abstractions; the `SslStream` path tells a bare end from close_notify by whether the transport under it read to its end; the OpenSSL text pins the reference build's OpenSSL 3.5.7, not the 3.5.5 the WSL build printed.
- Measured 2026-09-29: Schannel 8.21.0 and OpenSSL 8.18.0 (WSL) both exit 56 on a missing close_notify and 0 after one (table in ADR-0221).
- Resumed run fixed two hanging tests (an empty read on `InMemoryDuplexStream` waits for data; disposing the server end does not end the server's own read). `Curl.Quic.UnitTests` `Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout` failed once and passed on rerun: a timing flake outside this task.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Both TLS paths fail a read that ends without close_notify with exit 56 and the build's text; ADR-0221
