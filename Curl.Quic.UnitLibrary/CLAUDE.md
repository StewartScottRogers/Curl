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

The rest of the transport lands in the tasks ADR-0144 lists.

## Rules

- **Base class library plus the hand-built libraries ADR-0120 and ADR-0144 name, and
  nothing else.** `Curl.Tls.UnitLibrary` (its I/O-free `Tls13ClientHandshake` runs the
  QUIC handshake, ADR-0140), `Curl.Cryptography.UnitLibrary` (the primitives the BCL
  lacks on a CI platform) and `Curl.Protocol.Abstractions.UnitLibrary`. No package, and
  no other project reference. BL-723 added the `Curl.Tls` and `Curl.Cryptography`
  references; `Curl.Protocol.Abstractions` is added by the first task that needs it.
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
