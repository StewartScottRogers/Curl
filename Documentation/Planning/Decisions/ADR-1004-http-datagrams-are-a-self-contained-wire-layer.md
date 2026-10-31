# ADR-1004: HTTP Datagrams and the Capsule Protocol are a self-contained Wire layer

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4101
- Decided by Claude under Stewart's delegation.

## Context

WebTransport and MASQUE both need HTTP Datagrams and the Capsule Protocol (RFC 9297).
Extensions may not share code with each other (ADR-0999), so the framing needs a home that
both can reference. The variable-length integer RFC 9297 uses already exists in
`Curl.Http3.UnitLibrary` (`Http3VariableLengthInteger`), but the Capsule Protocol also runs
on HTTP/1.1 and HTTP/2 streams, where HTTP/3 has no part.

## Decision

`Curl.HttpDatagram.UnitLibrary` sits in `/Wire/HttpDatagram/` and references nothing: it
carries its own RFC 9000 variable-length integer (`HttpDatagramVariableLengthInteger`)
rather than referencing `Curl.Http3.UnitLibrary`, so an HTTP/1.1 or HTTP/2 user does not
pull in QPACK and HTTP/3 framing. It works on bytes only:

- `CapsuleCodec` encodes and decodes one capsule (type, length, value);
  `DatagramCapsuleReader` reassembles a stream's capsules, hands over each `DATAGRAM`
  capsule's payload and silently drops every other type (section 3.2), and its `Complete`
  fails a stream that ends inside a capsule (section 3.3).
- `Http3Datagram` writes and reads the quarter stream ID prefix (section 2.1), failing a
  cut-short prefix or one above 2^60 - 1 with H3_DATAGRAM_ERROR (0x33).
- `H3DatagramSetting` writes the `SETTINGS_H3_DATAGRAM` (0x33) entry, treats an absent
  setting as 0, fails any value other than 0 or 1 with H3_SETTINGS_ERROR, and enables
  datagrams only when both endpoints sent 1 (section 2.1.1).

It leaves wiring into a transfer, the `Capsule-Protocol` header and QUIC DATAGRAM frames
to the extensions that use it.

## Consequences

Two small copies of the variable-length integer exist (here and in HTTP/3); each is held
to the quality gates on its own. Upstream curl has no HTTP Datagram support, so the layer
is tested against RFC 9297's wire formats, not against curl.
