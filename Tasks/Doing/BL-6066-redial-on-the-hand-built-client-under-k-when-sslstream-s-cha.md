---
id: BL-6066
title: Redial on the hand-built client under -k when SslStream's chain build throws for OpenSSL error 1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-6063, BL-6050]
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/TcpConnectorTests.UnspecifiedChainRedial.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.UnspecifiedChain.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6066 — Redial on the hand-built client under -k when SslStream's chain build throws for OpenSSL error 1

## Goal

In the OpenSSL build, `-k` (or `--proxy-insecure`) against a server whose certificate makes `SslStream`'s own chain build throw for OpenSSL error 1 completes the transfer with verify result 1, as OpenSSL curl does.

## Context

ADR-1452 (BL-6063): `SslStreamTlsProvider` can only report that handshake as exit 35, since it is over when the exception arrives and the provider holds one connection. `TcpConnector` would dial again and run the second handshake on `HandBuiltTlsProvider`, whose chain builds go through `CertificateChainBuild` (ADR-1446). Measure curl's `-v` output for such a chain before pinning any.

## Acceptance criteria

- [ ] A unit test over fakes, running on every platform, in which the first handshake fails that way under `-k` ends with a connection from the hand-built route and verify result 1.
- [ ] Without `-k` the result stays exit 60 as ADR-1452 pins it, with no second dial.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 0c039f9d 23a17329.
