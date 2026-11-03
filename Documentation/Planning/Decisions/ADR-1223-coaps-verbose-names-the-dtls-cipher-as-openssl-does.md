# ADR-1223: `-v` on coaps names the DTLS cipher suite as OpenSSL does, after a connection line from the UDP channel

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4667

Decided by Claude under Stewart's delegation.

## Context

ADR-1222 decided that `-v` on a `coaps` transfer writes a line starting `* SSL connection using DTLSv1.2`, and that
every CoAP transfer writes the `* Established connection to ...` line every curl transfer writes. curl 8.22.0 has no
CoAP, so there is nothing to measure for the rest of the DTLS line. The CoAP handler opens its UDP channel itself
through `IDatagramConnector`, so nothing reported the connection.

## Decision

- `CoapProtocolHandler` reports the opened UDP channel through `ITransferEvents.ReportConnectionOpened`, naming the
  URL's host, the server's address, the channel's local address (`0.0.0.0` port `0` when the channel has none) and
  the connector's connection number, so the host writes the same `Established connection` line a TCP transfer gets.
  A channel whose server end point is not an IP address reports nothing, as no such channel exists today.
- After the DTLS handshake it writes `* SSL connection using DTLSv1.2 / <cipher>`, with OpenSSL's name for the suite
  the server chose (`ECDHE-ECDSA-AES128-GCM-SHA256`, `ECDHE-RSA-AES128-GCM-SHA256` or `ECDHE-ECDSA-AES128-CCM8`):
  the version and cipher fields curl's OpenSSL build writes from `SSL_get_version` and `SSL_get_cipher`. The group
  and signature fields newer curl appends are left out: the line stays the simplest that starts as ADR-1222 pins.

## Consequences

`CurlSurface:Verbose-Coap-1` and `CurlSurface:Verbose-Coaps-1` pass and leave `KnownFailures.txt`.
`DtlsClientSession.CipherSuite` carries the chosen suite from the handshake to `DtlsDatagramChannel.CipherName`.
