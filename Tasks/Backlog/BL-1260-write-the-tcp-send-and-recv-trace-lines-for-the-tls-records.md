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
completed:
---
# BL-1260 — Write the [TCP] send and recv trace lines for the TLS records of an https connection

## Goal

Under `--trace-config tcp`, `network`, `all` and `-vvvv`, an `https://` transfer writes curl 8.21.0's `[TCP] send` and `recv` lines for the TLS records below the TLS filter, during the handshake and for the application data, in curl's order beside the `>`/`<` lines.

## Context

- Measured in BL-1253's Notes (Schannel build, `-s -v -k --trace-config tcp https://127.0.0.1:P/`): after `ALPN: curl offers http/1.1`, `[TCP] send(len=429) -> 0, 429`, `[TCP] recv(len=4096) -> 81, 0`, `[TCP] adjust_pollset, !active, POLLIN fd=N`, `[TCP] recv(len=4096) -> 0, 1175`, `[TCP] send(len=158) -> 0, 158`, `[TCP] adjust_pollset, !active, POLLIN fd=N`, `[TCP] recv(len=4096) -> 0, 51`, `ALPN: server did not agree...`; no `[TCP] query ALPN`; then `[TCP] send(len=108) -> 0, 108` before the `>` lines and `recv(len=103424) -> 81, 0`, `recv(len=103424) -> 0, 72` before the `<` lines.
- Decide (ADR) which record sizes can match: the ClientHello and Finished sizes come from the TLS stack (SslStream or the hand-built TLS in `Curl.Networking.UnitLibrary`), and the read lengths 4096 and 103424 are Schannel's buffers. Measure the OpenSSL build's lines on Linux too if reachable. Wrap `dialed.Connection` before `AuthenticateTargetAsync` in `TcpConnector.SecureWhenAskedAsync`.

## Acceptance criteria

- [ ] An ADR amendment (ADR-0357) records which lines match curl exactly and why any cannot.
- [ ] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the handshake and application-data `send`/`recv` lines' order beside `>`/`<`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
