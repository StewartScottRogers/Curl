---
id: BL-1712
title: Refuse an empty ALPN protocol list or name in Curl.Tls ApplicationLayerProtocolNegotiationExtension
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1712 — Refuse an empty ALPN protocol list or name in Curl.Tls ApplicationLayerProtocolNegotiationExtension

## Goal

`ApplicationLayerProtocolNegotiationExtension` refuses what RFC 7301 section 3.1 forbids: `Decode` answers an empty protocol list and an empty protocol name with `decode_error`, and `Encode`'s contract covers an empty list and a name over 255 bytes.

## Context

- Found by BL-1523's adversarial tests: `Decode([0x00, 0x00])` (empty `ProtocolNameList`, which is `<2..2^16-1>`) and `Decode([0x00, 0x01, 0x00])` (a zero-length `ProtocolName`, which is `<1..2^8-1>`) both succeed today.
- `Encode([])` returns an extension with an empty list, which a server must reject; `Encode([new string('a', 256)])` throws `ArgumentException` from `TlsWriter.WriteVector`, which `Encode`'s doc comment does not promise.
- Decide whether `Encode` throws `ArgumentException` for both (and document it) or callers are trusted; record the choice in Notes.

## Acceptance criteria

- [ ] `ApplicationLayerProtocolNegotiationExtension.Decode` answers `00 00` and `00 01 00` with `TlsAlertDescription.DecodeError`, each pinned by a test in `Curl.Tls.UnitTests`.
- [ ] `Encode` on an empty list and on a 256-byte name behaves as its doc comment states, each pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass; Curl.Tls.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
