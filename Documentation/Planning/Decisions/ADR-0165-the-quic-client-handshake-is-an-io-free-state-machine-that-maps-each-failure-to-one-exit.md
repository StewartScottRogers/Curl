# ADR-0165 — The QUIC client handshake is an I/O-free state machine that maps each failure to one exit

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-724.

## Context

BL-724 completes the QUIC version 1 handshake in `Curl.Quic.UnitLibrary` over the
hand-built TLS 1.3 client (ADR-0140, ADR-0146), with the ClientHello, transport
parameters and first Initial ADR-0144 section 5 measured from curl.se's ngtcp2 build, and
the exit codes of its section 7. Several choices were left open: how the handshake meets
the datagram channel, how QUIC reads the server's transport parameters out of TLS, which
exit a TLS failure the client itself detects gets, what a transport-parameter error is,
and how far the handshake goes before loss recovery (BL-725), streams (BL-726) and
close and idle handling (BL-727) exist.

## Decision

1. **Two layers.** `QuicClientHandshake` has no I/O: `Start()` returns the first
   datagram, `Receive(datagram)` returns the datagrams to send, and `Abandon(error,
   failure)` returns the CONNECTION_CLOSE datagrams. `QuicClientConnector` drives it over
   an `IDatagramChannel` with the injected `TimeProvider`, ignores datagrams from any
   endpoint but the server's, and returns `null` or a `QuicHandshakeFailure`. The same
   split `Tls13ClientHandshake` uses; every rule is tested without a clock or a channel.
2. **TLS reports the server's `quic_transport_parameters`.**
   `Tls13ClientHandshake.ServerQuicTransportParameters` exposes the extension data from
   EncryptedExtensions, and QUIC decodes and checks it (`QuicTransportParameters`).
   QUIC does not re-parse TLS messages itself.
3. **The ClientHello is curl's, less one suite value.**
   `QuicClientSettings.CreateCurlTlsSettings` gives ADR-0144's measured profile and
   extension order. `Tls13ClientSettings` refuses a non-TLS 1.3 suite, so the LibreSSL
   signalling value `00ff` is not offered yet; a follow-up task lets the settings carry
   it. A server cannot tell the difference: the value is a TLS 1.2 renegotiation signal.
4. **Packets.** The ClientHello goes in one CRYPTO frame at offset 0, then PADDING in the
   same Initial packet up to 1200 bytes; every datagram that carries an Initial packet is
   1200 bytes. A packet's CRYPTO bytes get whatever room is left after its longest
   possible header (token included), its ACK and control frames and the tag, so no packet
   exceeds 1200 bytes and a ClientHello that does not fit spills into a second full
   datagram. Packets of different levels are coalesced up to 1200 bytes. A Retry whose
   source connection ID is the one the client chose is ignored (RFC 9000 section
   17.2.5.2), and an ACK of a packet never sent is a `PROTOCOL_VIOLATION` (section 13.1). Initial keys are discarded when the first Handshake packet is sent, and
   Handshake keys at HANDSHAKE_DONE (RFC 9001 section 4.9).
5. **Connection IDs.** 20 random bytes for the first destination and for the source,
   as curl's build. Once complete, the client issues IDs up to the server's
   `active_connection_id_limit`, at most 8 (ngtcp2's pool), replaces each one the server
   retires, keeps the server's IDs up to its own limit of 2, and retires those below a
   Retire Prior To field.
6. **Failures and exits.**

   | Failure | Close sent | Exit |
   | --- | --- | --- |
   | Version Negotiation that lists no version 1 | none | 7 |
   | Server CONNECTION_CLOSE with `CONNECTION_REFUSED` | none | 8 |
   | Server CONNECTION_CLOSE with any other error, a TLS alert (`CRYPTO_ERROR`) included | none | 7 |
   | Server transport parameters that do not decode, or whose connection IDs do not match (RFC 9000 section 7.3) | `TRANSPORT_PARAMETER_ERROR` | 7 |
   | Server `version_information` choosing a version other than 1 (RFC 9368 section 4) | `VERSION_NEGOTIATION_ERROR` | 7 |
   | Any other frame or packet rule the server breaks | its transport error | 7 |
   | The verifier rejects the certificate | `CRYPTO_ERROR` + its alert | 60, with the verifier's message |
   | Any other TLS failure the client detects | `CRYPTO_ERROR` + the alert | 35 |
   | No ALPN protocol, or no transport parameters (RFC 9001 sections 8.1 and 8.2) | `no_application_protocol` or `missing_extension` | 35 |
   | No completion in 10 s without `--connect-timeout` | `INTERNAL_ERROR` | 55, `ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT` |
   | `--connect-timeout` elapses | `NO_ERROR` | 28, `Connection timed out after <n> milliseconds` |

   ADR-0144's row "TLS fails inside QUIC, including certificate verification: 60" is
   narrowed to the certificate; any other TLS failure is 35, as the TCP path maps
   `TlsHandshakeFailure` and as curl maps a TLS handshake failure (`CURLE_SSL_CONNECT_ERROR`).
   The connector's message is the detail; the `Failed to connect to <host> port <port>
   after <n> ms: <text>` line is written by the caller that knows the host (BL-728).
7. **Out of scope here.** No retransmission (a lost packet waits for the timeout until
   BL-725), no stream frames (BL-726), and a receive error on the channel propagates to
   the caller, which maps it to exit 56 (BL-727, BL-728). If the server retires its
   handshake connection ID before HANDSHAKE_DONE, Handshake and 1-RTT packets can go to
   different IDs in one datagram (RFC 9000 section 12.2); BL-725 splits datagrams by
   destination when it reworks sending.

## Consequences

- The first Initial is pinned byte for byte in `Curl.Quic.UnitTests` for fixed
  randomness, and the handshake, Retry, Version Negotiation, transport-parameter errors
  and TLS alerts are tested against an in-memory server built from the library's own
  pieces over a fake channel and a manual clock.
- `Curl.Quic.UnitLibrary` now references `Curl.Protocol.Abstractions.UnitLibrary`, as
  ADR-0144 section 1 allows.
- The `00ff` value is missing from the pinned ClientHello until the follow-up lands.

## Alternatives considered

- **Read EncryptedExtensions in QUIC.** It would parse a TLS message twice and in two
  places. Rejected for one property on the TLS handshake.
- **Map every TLS failure to 60, as ADR-0144 section 7 first read.** A handshake alert
  that is not about the certificate is not a verification failure; curl reports 35 for
  it. Rejected.
- **Return from the connector at HANDSHAKE_DONE.** curl reports the connection when the
  handshake completes, one round trip earlier; HANDSHAKE_DONE is taken whenever it comes.
  Rejected.
