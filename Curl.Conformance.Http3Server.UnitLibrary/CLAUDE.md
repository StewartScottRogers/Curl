# Curl.Conformance.Http3Server.UnitLibrary

The upstream case runner's stand-in for nghttpx serving HTTP/3 (`%HTTP3PORT`), built in
pieces: BL-2246 put the TLS 1.3 server handshake here, BL-2247 the QUIC server connection
(`Http3ServerConnection`) on top of it, and BL-2231 the HTTP/3 relay to `sws` and the
datagram connector; BL-2261 puts it on `%HTTP3PORT` in the case runner, once BL-2260 sends
in-process Curl's QUIC through the injected `IDatagramConnector`.

## Rules

- It references `Curl.Tls.UnitLibrary` for the handshake codecs, key schedule, transcript
  hash, key shares and signing keys, and Curl.Quic.UnitLibrary for the QUIC codecs (packets,
  frames, packet protection, Initial secrets, CRYPTO reassembly, transport parameters); never
  copy that code here. It references `Curl.Http3.UnitLibrary` for HTTP/3 frames and QPACK,
  and `Curl.Conformance.Http2Server.UnitLibrary` for the h2c stand-in's HTTP/1.1 reply reader
  (`Http2ServerRequestRelay.ReadReplyAsync`, `Http2ServerBackendReader`, `Http2ServerReply`,
  which that library makes visible to this one and its tests). It may also reference
  `Curl.Cryptography.*`, `Curl.Protocol.Abstractions.UnitLibrary` and
  `Curl.Conformance.UnitLibrary`, and no `Curl.Protocol.*` protocol library.
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
  resent and no congestion control runs. Sending holds to the client's flow control
  (BL-2249): the limits come from its quic_transport_parameters (initial_max_data, and
  initial_max_stream_data_bidi_local for its bidirectional streams or _uni for the server's
  unidirectional ones), MAX_DATA and MAX_STREAM_DATA raise them, and bytes past them wait,
  the FIN after them.
- Http3ServerStream is a client stream as the server reads it, reassembled with
  QuicCryptoReassembler; Http3ServerSendStream (internal) is a stream as the server sends
  on it, its unsent bytes and its limit; Http3ServerPacketSpace (internal) is one packet
  number space.
- `Http3ServerSession` (internal) is HTTP/3 on one connection: after the handshake it opens
  its control stream (type 0x00, an empty SETTINGS frame, so QPACK's dynamic table stays at
  capacity 0), reads and ignores the client's unidirectional streams, and answers each
  request stream once its FIN has arrived: HEADERS decoded with `QpackDecoder`, DATA as the
  body, relayed by `Http3ServerRequestRelay`, the reply sent as HEADERS, DATA when there is
  a body, and the FIN. A request stream without HEADERS, or one that breaks HTTP/3 framing or
  QPACK, closes the connection with `H3_MESSAGE_ERROR` (0x10e). Calls are served one at a time.
- `Http3ServerRequestRelay` (internal) writes the HTTP/1.1 request nghttpx-h3 sends sws, as
  upstream's cases 2500 and 2501 expect: absolute-form request line
  (`GET https://localhost:port/path HTTP/1.1`), `Host` from `:authority`, the other fields
  capitalised, `Transfer-Encoding: chunked` for a body without `content-length`, and
  `Via: 3 nghttpx` (after any incoming `via`); no `X-Forwarded-Proto`. The reply comes back
  with `server: nghttpx` and `via: 1.1 nghttpx`; an unreachable backend reads as `502`.
- `Http3ServerChannel` (internal) is one client's `IDatagramChannel`: sends go to its session,
  answers wait for receives and come from `ServerEndPoint`. `Http3ServerConnector` (internal)
  opens a fresh connection and session per open, whatever the port; `UpstreamHttp3ServerFactory.Create(backend, tlsSettings)`
  is the public way in.
