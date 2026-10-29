# Curl.HttpDatagram.UnitLibrary

A hand-built HTTP Datagrams and Capsule Protocol layer (RFC 9297), shared by the
WebTransport and MASQUE extensions, which may not share code with each other (ADR-0999,
ADR-1004). It sits in `/Wire/HttpDatagram/` in `Curl.slnx` and references nothing.

Namespace `Curl.HttpDatagram`. It works on bytes only; the caller owns the streams and
the QUIC datagrams.

- `HttpDatagramVariableLengthInteger` writes and reads RFC 9000 variable-length integers.
- `Capsule` is one capsule (type and value); `CapsuleCodec` encodes one, and decodes one
  from a buffer that may hold only part of it. `CapsuleType.Datagram` is type 0x00.
- `DatagramCapsuleReader` reassembles a stream's capsules (HTTP/1.1 and HTTP/2) and hands
  over each `DATAGRAM` capsule's payload, silently skipping capsules of other types as
  RFC 9297 section 3.2 requires.
- `Http3Datagram` frames an HTTP/3 datagram: the quarter stream ID prefix, then the payload.
- `H3DatagramSetting` is `SETTINGS_H3_DATAGRAM` (0x33): its wire form and the negotiation
  rule (both endpoints send 1).
- Every RFC 9297 violation is an `HttpDatagramException` carrying its
  `HttpDatagramErrorCode`.
