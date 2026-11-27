---
id: BL-6066
title: Redial on the hand-built client under -k when SslStream's chain build throws for OpenSSL error 1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-6063, BL-6050]
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/TcpConnectorTests.UnspecifiedChainRedial.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.UnspecifiedChain.cs, Curl.Networking.UnitLibrary/TcpConnector.HandBuiltRedial.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectEntry.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Networking.UnitLibrary/TcpConnector.Multiplexed.cs, Curl.Networking.UnitLibrary/ITlsProviderWithHandBuiltRedial.cs, Curl.Networking.UnitLibrary/HandBuiltRedialRequiredException.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6066 — Redial on the hand-built client under -k when SslStream's chain build throws for OpenSSL error 1

## Goal

In the OpenSSL build, `-k` (or `--proxy-insecure`) against a server whose certificate makes `SslStream`'s own chain build throw for OpenSSL error 1 completes the transfer with verify result 1, as OpenSSL curl does.

## Context

ADR-1452 (BL-6063): `SslStreamTlsProvider` can only report that handshake as exit 35, since it is over when the exception arrives and the provider holds one connection. `TcpConnector` would dial again and run the second handshake on `HandBuiltTlsProvider`, whose chain builds go through `CertificateChainBuild` (ADR-1446). Measure curl's `-v` output for such a chain before pinning any.

## Acceptance criteria

- [x] A unit test over fakes, running on every platform, in which the first handshake fails that way under `-k` ends with a connection from the hand-built route and verify result 1.
- [x] Without `-k` the result stays exit 60 as ADR-1452 pins it, with no second dial.

## Notes

- ADR-1453 records the design: SslStreamTlsProvider marks the failed -k result (ITlsProviderWithHandBuiltRedial), TcpConnector unwinds, dials again and handshakes with that host on HandBuiltTlsProvider.
- touches widened to the other TcpConnector partials, the two new types and the library CLAUDE.md; no task in Doing named them (BL-6039 holds only TcpConnector.ConnectTimeout.cs, which is unchanged). TcpConnector.cs itself needed no change.
- The test over fakes replaces the SslStream handshake step and stands a fake in for the hand-built client, so it runs on every platform; on Linux the real hand-built client reports verify result 1 through CertificateChainBuild (ADR-1446).
- curl -v output was not measured and no new line is pinned. A redialled connect writes the pre-handshake -v lines twice: filed as BL-6067.
- Build clean, all fast tests green; Measure-CodeQuality for Curl.Networking.UnitLibrary: line 100, branch 100, 0 failing.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 0c039f9d 23a17329.
- 2026-11-26: Doing -> Done. -k and --proxy-insecure now complete an unspecified-chain transfer by redialling on the hand-built client
