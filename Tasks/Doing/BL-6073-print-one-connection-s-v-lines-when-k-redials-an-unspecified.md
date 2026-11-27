---
id: BL-6073
title: Print one connection's -v lines when -k redials an unspecified-chain handshake on the hand-built client
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6066]
touches: [Curl.Networking.UnitLibrary/TcpConnector.HandBuiltRedial.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Networking.UnitTests/TcpConnectorTests.UnspecifiedChainRedial.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6073 — Print one connection's -v lines when -k redials an unspecified-chain handshake on the hand-built client

## Goal

`-v` for a `-k` transfer redialled by ADR-1459 shows the lines of one connection, as OpenSSL curl writes them.

## Context

ADR-1459 (BL-6066): `TcpConnector` repeats the whole connect, so the lines before the handshake
(`Host ... was resolved`, `Trying`, the TLS trust lines) appear twice. Measure OpenSSL curl's `-v`
output for a chain whose name constraint does not decode (ADR-1446) before pinning any line;
`CURL_SSL_BACKEND=openssl` with a multi-TLS curl may do so on Windows.

## Acceptance criteria

- [ ] The measured curl output, or why it could not be measured, is recorded under Notes.
- [ ] A unit test over fakes, running on every platform, shows each line before the handshake once for a redialled direct `-k` connect.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 2c09db2b 3d555037.
