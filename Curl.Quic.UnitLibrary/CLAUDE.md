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
carrying the `QuicTransportErrorCode` to close with, never an out-of-range read. The rest
of the transport lands in the tasks ADR-0144 lists.

## Rules

- **Base class library plus the hand-built libraries ADR-0120 and ADR-0144 name, and
  nothing else.** `Curl.Tls.UnitLibrary` (its I/O-free `Tls13ClientHandshake` runs the
  QUIC handshake, ADR-0140), `Curl.Cryptography.UnitLibrary` (the primitives the BCL
  lacks on a CI platform) and `Curl.Protocol.Abstractions.UnitLibrary`. No package, and
  no other project reference. The references are added by the first task that needs
  them (BL-723), not before.
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
