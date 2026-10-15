# Curl.Conformance.Http3Server.UnitLibrary

The upstream case runner's stand-in for nghttpx serving HTTP/3 (`%HTTP3PORT`), built in
pieces: BL-2246 put the TLS 1.3 server handshake here, BL-2247 the QUIC server connection
(`Http3ServerConnection`) on top of it; the HTTP/3 relay to `sws` comes next.

## Rules

- It references `Curl.Tls.UnitLibrary` for the handshake codecs, key schedule, transcript
  hash, key shares and signing keys, and Curl.Quic.UnitLibrary for the QUIC codecs (packets,
  frames, packet protection, Initial secrets, CRYPTO reassembly, transport parameters); never
  copy that code here. It may also reference
  `Curl.Cryptography.*` and `Curl.Conformance.UnitLibrary`, and no `Curl.Protocol.*` library.
- BCL only, no socket, no `SslStream`: bytes in, bytes out, per encryption level, as RFC 9001
  section 4 hands TLS to QUIC.
- Held to the `*.UnitLibrary` gates: 100% line and branch coverage, complexity at most 10.

## Shape

- `Http3ServerTlsHandshake` is the I/O-free TLS 1.3 server, at the message level (no TLS
  records: QUIC carries the messages in CRYPTO frames). `Receive(level, bytes)` takes the
  ClientHello at Initial and the client Finished at Handshake, whole or in parts, and returns
  a `Curl.Tls` `Tls13HandshakeOutput`: ServerHello (or a HelloRetryRequest) to send at Initial,
  EncryptedExtensions, Certificate, CertificateVerify and Finished at Handshake, and the
  `Tls13TrafficSecret`s per level - Handshake read and write and Application write after the
  ClientHello, Application read once the client Finished checks out. A failure carries the
  alert: `handshake_failure` with no common suite, group or signature scheme,
  `illegal_parameter` for a second ClientHello still without the requested share or a bad
  share, `decrypt_error` for a bad client Finished, `no_application_protocol` without ALPN
  `h3` (RFC 9001 section 8.1), `missing_extension` without groups, key shares, signature
  schemes or `quic_transport_parameters`, `protocol_version` without TLS 1.3, and
  `unexpected_message` for a message out of order or at the wrong level.
- `Http3ServerTlsSettings` holds the certificate chain, signing key and transport parameters
  to send, and the preference orders: TLS_AES_128_GCM_SHA256 first, X25519 then secp256r1,
  rsa_pss_rsae_sha256 first. `FromRsaCertificate` takes upstream's `test-localhost.pem` as
  `HttpsServerConnector.LoadCertificate` loads it.
- `Http3ServerClientOffer` reads the ClientHello's extensions and refuses one QUIC cannot use.
- `Http3ServerTlsOutput` collects one `Receive` call's bytes and secrets.
- Http3ServerConnection is the I/O-free QUIC version 1 server (BL-2247). Receive(datagram)
  returns the datagrams to send: it accepts the client's first Initial (DCID of 8 bytes or
  more), derives the Initial keys from it, chooses an 8-byte connection ID, feeds CRYPTO frames
  to Http3ServerTlsHandshake and installs its secrets, sends DefaultTransportParameters
  (limits large enough that the client never waits for MAX_DATA, MAX_STREAM_DATA or
  MAX_STREAMS) with original_destination_connection_id and initial_source_connection_id,
  acknowledges every ack-eliciting packet, drops packets that fail protection, repeat or name
  another connection, sends HANDSHAKE_DONE, and closes with CONNECTION_CLOSE 0x1c (a frame
  error, or 0x100 plus the TLS alert) at every level it has keys for. An unknown version
  in a datagram of 1200 bytes or more gets Version Negotiation. Until a client Handshake
  packet arrives it sends at most 3x the bytes received and holds the rest. AcceptStream,
  OpenUnidirectionalStream (3, 7, ...), Write and TakeDatagramsToSend carry stream
  data; Close sends the application close; PeerClose keeps the client's. Nothing is
  resent and no congestion control runs; it does not yet hold its sending to the client's
  flow control limits (BL-2249 covers that).
- Http3ServerStream is a client stream as the server reads it, reassembled with
  QuicCryptoReassembler; Http3ServerPacketSpace (internal) is one packet number space.
