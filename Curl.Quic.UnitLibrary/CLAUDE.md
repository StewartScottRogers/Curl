# Curl.Quic.UnitLibrary

Hand-built QUIC version 1 client transport, per RFC 9000 (the transport: variable-length
integers, packet headers, frames, streams and flow control, connection close, idle
timeout and stateless reset), RFC 9001 (TLS for QUIC: Initial secrets, packet and header
protection, the handshake at each encryption level) and RFC 9002 (loss detection and
congestion control). It carries `--http3` and `--http3-only` under ADR-0144;
`Curl.Http3.UnitLibrary` reads and writes its streams as byte streams and never
references it. `System.Net.Quic` is not used (ADR-0144).

Namespace `Curl.Quic`. So far it holds the wire codec (BL-722): `QuicVariableLengthInteger`,
`QuicPacketNumber` (Appendix A encoding and decoding), `QuicPacketCodec` with one record per
header form (`QuicLongHeaderPacket`, `QuicRetryPacket`, `QuicVersionNegotiationPacket`,
`QuicShortHeaderPacket`), and `QuicFrameCodec` with one `QuicFrame` record per frame type of
RFC 9000 section 19. Bytes the peer sent that break a rule are a `QuicTransportException`
carrying the `QuicTransportErrorCode` to close with, never an out-of-range read.

Packet protection (BL-723, RFC 9001 sections 5 and 6):

- `QuicInitialSecrets`: the version 1 salt, `initial_secret`, `client in` and `server in`.
- `QuicPacketKeys`: `quic key`, `quic iv` and `quic hp` of a secret, and `quic ku`
  (`DeriveNextSecret`), with HKDF-Expand-Label from `Curl.Tls` (`Tls13KeySchedule`).
- `QuicHeaderProtection`: the five-byte mask of a 16-byte sample, AES-ECB for the AES
  suites and the hand-built ChaCha20 for ChaCha20-Poly1305; applies and removes it in place.
- `QuicPacketProtection`: one direction at one encryption level. `Protect(packet,
  packetNumber)` encrypts a `QuicLongHeaderPacket` or `QuicShortHeaderPacket` whose
  payload is the plaintext frames (the BCL's `AesGcm`, or `AeadChaCha20Poly1305` from
  `Curl.Cryptography`) and applies header protection. `Unprotect` returns a
  `QuicUnprotectResult`: a packet too short for the sample or failing the AEAD is dropped
  with its `QuicUnprotectStatus` and its length, never thrown; a Reserved Bit set on an
  authentic packet is a `ProtocolViolation`. Key update: `UpdateKeys` moves to the next
  phase and keeps the previous keys; a received short header of the other phase opens with
  the previous keys when no packet of the current phase has arrived yet or its number is
  lower than the first that did, otherwise with the next keys, and success moves the
  receiver to that phase (`KeyPhaseChanged`). `DiscardPreviousKeys` drops the old ones.
  Suites: `0x1301`, `0x1302`, `0x1303`; the AES-CCM suites wait for the hand-built AES-CCM.
- `QuicRetryIntegrity`: the Retry Integrity Tag (section 5.8) and its fixed-time check.

The handshake (BL-724, RFC 9000 sections 5 to 8, 17 and 18, RFC 9001 section 4, ADR-0165):

- `QuicClientHandshake`: I/O-free. `Start()` returns the first Initial (curl's ClientHello
  in one CRYPTO frame, padded to 1200 bytes), `Receive(datagram)` returns what to send,
  `Abandon` returns the CONNECTION_CLOSE datagrams. It drives `Tls13ClientHandshake` at the
  Initial, Handshake and 1-RTT levels, follows one Retry, fails on Version Negotiation
  without version 1, checks the server's transport parameters, discards Initial keys at the
  first Handshake packet and Handshake keys at HANDSHAKE_DONE, and keeps NEW_TOKEN tokens.
  `QuicClientSettings` (with `CreateCurlTlsSettings`), `QuicHandshakeFailure` and the
  internal `QuicHandshakeFailures` hold the settings and the exit mapping.
- `QuicClientConnector`: runs a handshake over `IDatagramChannel` with the injected
  `TimeProvider`; 10 s without `--connect-timeout` is exit 55, the timeout exit 28.
- `QuicTransportParameters` and `QuicVersionInformation`: RFC 9000 section 18 and
  RFC 9368, encoded in curl's order, only the values that differ from the defaults.
- `QuicCryptoReassembler`, `QuicPeerConnectionIds`, `QuicLocalConnectionIds`, and the
  internal `QuicPacketNumberSpace`, `QuicDatagramAssembler` and `QuicPacketAddress`.

Loss detection and congestion control (BL-725, RFC 9002, RFC 9438):

- `QuicRttEstimator`: latest, minimum and smoothed RTT and its variation (section 5),
  the probe timeout and the time-threshold loss delay.
- `QuicLossRecovery`: Appendix A with no clock of its own. Remembers every `QuicSentPacket`
  per `QuicPacketNumberSpaceId`, takes RTT samples from ACK frames (the ACK Delay counts
  only in application data), declares loss by packet threshold (3) and time threshold
  (9/8 RTT), runs the probe timeout with exponential backoff and the client's
  anti-deadlock probe, arms application data only once the handshake is confirmed, and
  detects persistent congestion within the losses one call declares. It hands lost
  packets back; it never resends one.
- `QuicCongestionController` (bytes in flight, recovery period, persistent congestion) with
  `QuicCubicCongestionController`, the default as in curl's build, and
  `QuicNewRenoCongestionController` (Appendix B); `QuicClientSettings.CongestionControl`
  chooses. `QuicPacer` is RFC 9002 section 7.7's token bucket.
- `QuicClientHandshake` records each packet the assembler builds, resends the data of lost
  packets in new packets (`QuicPacketNumberSpace.RequeueLost`: CRYPTO by range, other
  frames as they were, never ACK, PADDING, PING, CONNECTION_CLOSE or path frames), sends
  one probe per probe timeout (the oldest unacknowledged data, else a PING) and exposes
  `TimeUntilLossDetectionTimeout`, `OnLossDetectionTimeout`, `TimeUntilSend` and
  `OnDatagramSent`, which `QuicClientConnector` runs. It takes the `TimeProvider`.
- `QuicDatagramAssembler` stops at the congestion window (acknowledgements, probes and
  CONNECTION_CLOSE go regardless) and starts a new datagram when the destination
  connection ID changes (RFC 9000 section 12.2).

Streams and flow control (BL-726, RFC 9000 sections 2 to 4, ADR-0174):

- `QuicStreamSet` (`QuicClientHandshake.Streams`): opens client bidirectional and
  unidirectional streams up to the server's MAX_STREAMS (STREAMS_BLOCKED once per limit),
  creates the server's streams with every lower-numbered one of their type, routes the
  stream frames of sections 19.4 to 19.14, holds the server's MAX_DATA and MAX_STREAM_DATA
  (DATA_BLOCKED and STREAM_DATA_BLOCKED once per limit), and raises the client's limits by
  fixed windows at half. Violations throw `FLOW_CONTROL_ERROR`, `STREAM_LIMIT_ERROR`,
  `STREAM_STATE_ERROR` or `FINAL_SIZE_ERROR`, which close the connection.
- `QuicStream`: reassembles reordered and repeated STREAM frames, checks the final size,
  keeps the peer's RESET_STREAM and STOP_SENDING (answered with RESET_STREAM), and queues
  writes until flow control lets them go. `Abort` sends RESET_STREAM and STOP_SENDING.
- The internal `QuicSendCredit` and `QuicReceiveCredit` hold one limit each way.
  `QuicPacketNumberSpace.Streams` puts the streams' frames in 1-RTT packets; a lost STREAM
  frame goes again unless its stream was reset. A short header packet ends its datagram.
- `QuicClientHandshake.TakeDatagramsToSend` and `CloseWithApplicationError` send what the
  streams queued and an application CONNECTION_CLOSE.
- `QuicConnection` implements `IMultiplexedConnection` over a completed handshake and its
  `IDatagramChannel`: one loop sends, receives and runs the loss detection timer; streams
  (the internal `QuicMultiplexedStream`) change state under its lock and wake it. A lost
  channel is exit 56.

Close, idle timeout and stateless reset (BL-727, RFC 9000 section 10, ADR-0177):

- After the handshake, the server's CONNECTION_CLOSE (a TLS alert included), a transport
  error the client detects and a stateless reset are exit 56, `Failure when receiving data
  from the peer`, as in curl's ngtcp2 build; `QuicHandshakeFailure.ServerClose` keeps the
  server's frame. During the handshake the mapping of ADR-0165 stands.
- The client sends CONNECTION_CLOSE once, with no closing period, and after the server's
  close or a stateless reset drains: it sends nothing more.
- `QuicClientHandshake.TimeUntilIdleTimer` and `OnIdleTimer`: once complete, a PING at half
  the idle timeout (the smaller non-zero `max_idle_timeout`, at least three probe timeouts),
  and at the timeout a silent close, exit 55 `ngtcp2_conn_handle_expiry returned error:
  ERR_IDLE_CLOSE`. `QuicConnection`'s loop runs it beside the loss detection timer.
- `QuicPeerConnectionIds.IsStatelessReset`: the token of the connection ID in use, at the
  end of a short-header-shaped datagram of 21 bytes or more, compared in fixed time.

## Rules

- **Base class library plus the hand-built libraries ADR-0120 and ADR-0144 name, and
  nothing else.** `Curl.Tls.UnitLibrary` (its I/O-free `Tls13ClientHandshake` runs the
  QUIC handshake, ADR-0140), `Curl.Cryptography.UnitLibrary` (the primitives the BCL
  lacks on a CI platform) and `Curl.Protocol.Abstractions.UnitLibrary`. No package, and
  no other project reference. BL-723 added the `Curl.Tls` and `Curl.Cryptography`
  references, BL-724 `Curl.Protocol.Abstractions` (for `IDatagramChannel` and `CurlExitCode`).
- **Never a `Socket`.** Datagrams go in and out through a seam the caller implements;
  the library turns datagrams into datagrams and never opens a socket or any stream.
  This is what keeps its tests off the network.
- **Time through `TimeProvider`.** Loss detection, probe timeouts, pacing and the idle
  timeout take the injected `TimeProvider`; never `DateTime.Now`, `Stopwatch` or
  `Thread.Sleep`.
- **Randomness injected.** Connection IDs, packet number starts and the TLS client
  random come from an injected source, so published vectors reproduce exactly.
- **RFC 9001 Appendix A is the reference.** Its Initial secrets, keys, IVs, header
  protection keys and protected packets are pinned byte for byte in
  `Curl.Quic.UnitTests`, with the appendix section cited beside each.
- Tests in `Curl.Quic.UnitTests` are platform-neutral.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
