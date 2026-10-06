---
id: BL-722
title: Encode and decode QUIC variable-length integers, packet headers and frames
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-719]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-722 — Encode and decode QUIC variable-length integers, packet headers and frames

## Goal

`Curl.Quic.UnitLibrary` encodes and decodes QUIC variable-length integers (RFC 9000 section 16), every long-header packet type (Initial, 0-RTT, Handshake, Retry), the short header, Version Negotiation, packet-number encoding and decoding (Appendix A), and every frame type of RFC 9000 section 19.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Design: BL-718's ADR. Pure code: bytes in, typed records out and back; a malformed packet or frame is a typed `FRAME_ENCODING_ERROR` or `PROTOCOL_VIOLATION`, never an exception from an out-of-range read.
- References: RFC 9000 sections 12 to 19 and Appendix A (sample varint encodings in section 16's table, packet-number decoding example in A.3); RFC 9001 Appendix A.2 to A.5 (complete sample packets whose unprotected headers and frames this task can decode once protection is removed by BL-723; until then decode their plaintext forms given in the RFC).

## Acceptance criteria

- [x] `Curl.Quic.UnitTests` pass RFC 9000 section 16's varint examples and A.3's packet-number example, round-trip every frame type and packet type, decode the plaintext CRYPTO frame and headers from RFC 9001 A.2, and reject truncated and non-minimal encodings where the RFC requires.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered in the session rather than through the full `/feature` agent chain: the
  design is fixed by RFC 9000 and ADR-0144, and the work stays inside
  `Curl.Quic.UnitLibrary` and its tests.
- Shape: `QuicVariableLengthInteger` (section 16), `QuicPacketNumber` (Appendix A.2 length
  choice, A.3 decoding), `QuicPacketCodec` over four packet records (long header with
  packet number, Retry, Version Negotiation, short header) returning a `QuicDecodedPacket`
  with `HeaderLength` (the AEAD associated data BL-723 needs) and `Length` (for coalesced
  packets), and `QuicFrameCodec` over one `QuicFrame` record per section 19 frame type.
- Defaults taken (implementation choices, no user-visible behaviour, so Notes rather
  than an ADR):
  - Errors are a typed `QuicTransportException` with `FrameEncodingError` for a
    malformed frame (unknown type, truncated field, empty NEW_TOKEN, ACK below packet 0,
    stream count above 2^60, offset + length above 2^62 - 1, connection ID not 1 to 20
    bytes, Retire Prior To above Sequence Number) and `ProtocolViolation` for a malformed
    packet, an empty payload, a frame type not in its shortest encoding (section 12.4's
    MAY), and a frame the packet type may not carry (table 3).
  - A long header's Length field is always written in at least two bytes, as RFC 9001
    A.2 and A.3 write it, so padding a packet never moves its header; decoding then
    re-encoding the RFC's sample headers is byte-identical.
  - A run of PADDING bytes decodes as one `QuicPaddingFrame(Length)`.
  - The decoder reads version 1 and Version Negotiation only; any other version is a
    `ProtocolViolation`, since its layout past the invariant header is unknown.
  - Payloads are "what follows the packet number" (ciphertext plus tag once protected),
    so the RFC 9001 A.2 and A.3 tests append 16 placeholder tag bytes to the plaintext.
  - Reserved bits are exposed, not checked: RFC 9000 17.2 requires the check only after
    packet protection is removed, which is BL-723's job.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Quic encodes and decodes varints, packet numbers, every QUIC v1 packet header and every RFC 9000 frame, pinned to RFC 9000/9001 samples, 100% covered
