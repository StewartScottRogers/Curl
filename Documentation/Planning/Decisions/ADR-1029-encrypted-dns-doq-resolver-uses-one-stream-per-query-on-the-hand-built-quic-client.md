# ADR-1029: The DNS-over-QUIC resolver uses one stream per query on the hand-built QUIC client

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4208)

## Context

BL-4208, split from BL-4148 beside BL-4207's DNS-over-TLS resolver (ADR-1024), adds an `IDnsResolver` over DNS
over QUIC (RFC 9250) to `Curl.Extension.EncryptedDns.UnitLibrary`. Upstream curl has no DNS-over-QUIC option, so
there is nothing to measure; the code is tested against RFC 9250. `System.Net.Quic` is not used (ADR-0144), and an
extension may reference the shared `Curl.Quic.UnitLibrary`.

## Decision

- `DnsOverQuicResolver` takes an `IDatagramConnector`, an `IServerCertificateVerifier`, a `DnsOverQuicServer`
  (port 853 by default, section 4.1.1, with an optional TLS server name) and a `TimeProvider`. It runs
  `QuicClientConnector.RunHandshakeAsync` on a `QuicClientConnectionState` and carries the lookup on a
  `QuicConnection`.
- The TLS hello is `QuicClientSettings.CreateOpenSslTlsSettings` with `ApplicationProtocols` set to `["doq"]` only
  (section 4.1.1), on every platform: the hello profile is not curl behaviour here, and one profile keeps the tests
  the same on Windows, Linux and macOS.
- Each lookup opens one connection and sends the AAAA query, then the A query, each on its own client bidirectional
  stream with Message ID 0, the two-octet length prefix and FIN after the query (sections 4.2 and 4.2.1). Answers
  are read in query order and returned AAAA first, then A, as ADR-1024 does.
- An answer stream that does not hold exactly one length-prefixed message with Message ID 0 closes the connection
  with DOQ_PROTOCOL_ERROR (sections 4.3 and 4.5) and gives no addresses; otherwise the connection closes with
  DOQ_NO_ERROR. A failed open or handshake, a reset stream or a failed connection gives no addresses.
- Its tests drive the real QUIC client against a copy of `Curl.Quic.UnitTests`' in-memory `QuicTestServer`, as
  `Curl.Networking.UnitTests` does, so `Curl.Quic.UnitLibrary` grants `InternalsVisibleTo` to
  `Curl.Extension.EncryptedDns.UnitTests`.
- 0-RTT, connection reuse across lookups and the options, hook and catalogue lines are left to BL-4148.

## Consequences

- The extension's lookups carry no curl-visible behaviour change until BL-4148 registers them.
