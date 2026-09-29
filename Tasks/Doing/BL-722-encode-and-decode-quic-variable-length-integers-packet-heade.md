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
completed:
---
# BL-722 — Encode and decode QUIC variable-length integers, packet headers and frames

## Goal

`Curl.Quic.UnitLibrary` encodes and decodes QUIC variable-length integers (RFC 9000 section 16), every long-header packet type (Initial, 0-RTT, Handshake, Retry), the short header, Version Negotiation, packet-number encoding and decoding (Appendix A), and every frame type of RFC 9000 section 19.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Design: BL-718's ADR. Pure code: bytes in, typed records out and back; a malformed packet or frame is a typed `FRAME_ENCODING_ERROR` or `PROTOCOL_VIOLATION`, never an exception from an out-of-range read.
- References: RFC 9000 sections 12 to 19 and Appendix A (sample varint encodings in section 16's table, packet-number decoding example in A.3); RFC 9001 Appendix A.2 to A.5 (complete sample packets whose unprotected headers and frames this task can decode once protection is removed by BL-723; until then decode their plaintext forms given in the RFC).

## Acceptance criteria

- [ ] `Curl.Quic.UnitTests` pass RFC 9000 section 16's varint examples and A.3's packet-number example, round-trip every frame type and packet type, decode the plaintext CRYPTO frame and headers from RFC 9001 A.2, and reject truncated and non-minimal encodings where the RFC requires.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
