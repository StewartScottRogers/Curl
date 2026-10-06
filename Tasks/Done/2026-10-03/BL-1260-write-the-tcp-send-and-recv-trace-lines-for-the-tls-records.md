---
id: BL-1260
title: Write the [TCP] send and recv trace lines for the TLS records of an https connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1253]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-03
---
# BL-1260 — Write the [TCP] send and recv trace lines for the TLS records of an https connection

## Goal

Under `--trace-config tcp`, `network`, `all` and `-vvvv`, an `https://` transfer writes curl 8.21.0's `[TCP] send` and `recv` lines for the TLS records below the TLS filter, during the handshake and for the application data, in curl's order beside the `>`/`<` lines.

## Context

- Measured in BL-1253's Notes (Schannel build, `-s -v -k --trace-config tcp https://127.0.0.1:P/`): after `ALPN: curl offers http/1.1`, `[TCP] send(len=429) -> 0, 429`, `[TCP] recv(len=4096) -> 81, 0`, `[TCP] adjust_pollset, !active, POLLIN fd=N`, `[TCP] recv(len=4096) -> 0, 1175`, `[TCP] send(len=158) -> 0, 158`, `[TCP] adjust_pollset, !active, POLLIN fd=N`, `[TCP] recv(len=4096) -> 0, 51`, `ALPN: server did not agree...`; no `[TCP] query ALPN`; then `[TCP] send(len=108) -> 0, 108` before the `>` lines and `recv(len=103424) -> 81, 0`, `recv(len=103424) -> 0, 72` before the `<` lines.
- Decide (ADR) which record sizes can match: the ClientHello and Finished sizes come from the TLS stack (SslStream or the hand-built TLS in `Curl.Networking.UnitLibrary`), and the read lengths 4096 and 103424 are Schannel's buffers. Measure the OpenSSL build's lines on Linux too if reachable. Wrap `dialed.Connection` before `AuthenticateTargetAsync` in `TcpConnector.SecureWhenAskedAsync`.

## Acceptance criteria

- [x] An ADR amendment (ADR-0357) records which lines match curl exactly and why any cannot.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the handshake and application-data `send`/`recv` lines' order beside `>`/`<`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- `TcpConnector.TlsRecordTraceFor` wraps a direct `https` connection (no `Proxy`, not a forward proxy) dialled under `TracesTcpFilter` in `TcpIoTraceConnection` before the handshake; its now settable `Lines` start as `HttpsHandshakeLines` (recv len 4096) and switch to `HttpsApplicationDataLines` (recv len 103424) once the handshake succeeds (`AuthenticateTracingRecordsAsync`, split out to keep `SecureWhenAskedAsync` at complexity 10). No `[TCP] query ALPN` for https.
- ADR-0357 BL-1260 amendment: line order, form, receive lengths and would-block lines match; record sizes depend on the TLS stack and cannot match byte for byte; `adjust_pollset, !active, POLLIN fd=N` lines are not written (no socket number). The OpenSSL build was not reachable from the lane, so the Schannel lengths are used on every platform (default taken).
- Through a proxy or tunnel no record lines are written (TCP filter sits below the proxy filter).
- Tests: `TcpConnectorTests.TlsRecordTrace.cs` (Networking), `CurlCommandRunnerTcpIoTraceTests.RunAsync_HttpsGetUnderTraceConfigTcp_*` (Console, tcp, network, all, -vvvv). Measure-CodeQuality: Networking and Console 100% line and branch, 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. https transfers under --trace-config tcp/network/all and -vvvv write curl's [TCP] send/recv lines for the TLS handshake and application-data records
