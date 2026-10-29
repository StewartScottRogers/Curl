---
id: BL-4250
title: Extension CoAP: frame DTLS 1.2 records and handshake messages (RFC 6347)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4245]
touches: [Curl.Extension.Coap.UnitLibrary/Curl.Extension.Coap.UnitLibrary.csproj, Curl.Extension.Coap.UnitLibrary/Dtls/Wire, Curl.Extension.Coap.UnitTests/Dtls/Wire, Curl.Extension.Coap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4250 — Extension CoAP: frame DTLS 1.2 records and handshake messages (RFC 6347)

## Goal

`Curl.Extension.Coap.UnitLibrary` frames DTLS 1.2 records (epoch, 48-bit sequence number, replay window) and handshake messages (message_seq, fragment offset and length, reassembly), by RFC 6347.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

DTLS is written in the extension, under `Dtls/`, because no curl protocol shares it (BL-4116's Notes). Reuse `Curl.Tls.UnitLibrary`'s public types where they fit (`TlsContentType`, `HandshakeType`, `TlsReader`/`TlsWriter`, the AEAD record cipher shapes) and write the DTLS-only parts here. RFC 6347 sections 4.1 (record layer), 4.1.2.1 (sequence-number nonce and additional data), 4.1.2.6 (anti-replay window), 4.2.2 (handshake header), 4.2.3 (fragmentation and reassembly), 4.2.1 (HelloVerifyRequest). Namespace `Curl.Extension.Coap.Dtls.Wire`.

## Acceptance criteria

- [x] Unit tests check the 13-octet record header and the AEAD additional data against RFC 6347 sections 4.1 and 4.1.2.1.
- [x] Unit tests check the anti-replay window against section 4.1.2.6 (duplicate, too old, in-window, advancing).
- [x] Unit tests check the 12-octet handshake header, fragmenting a message to a size limit and reassembling fragments out of order and overlapping, against sections 4.2.2 and 4.2.3, and decoding a HelloVerifyRequest against 4.2.1.
- [x] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly (one small library, no seams to plan): `Dtls/Wire` holds `DtlsRecordHeader`, `DtlsReplayWindow`, `DtlsHandshakeHeader`, `DtlsHandshakeFragment`, `DtlsHandshakeReassembly` and `DtlsHelloVerifyRequest`; decisions in ADR-1042 (reference Curl.Tls for `TlsContentType`/`HandshakeType`, FormatException like the CoAP codec, 64-number replay window, byte-level reassembly rejecting disagreeing overlaps).
- Added `Curl.Extension.Coap.UnitLibrary/CLAUDE.md` to touches: it said the library references only Curl.Extension.Abstractions, untrue once it references Curl.Tls. No other Doing task names it (checked origin/work/dark-factory).
- `Measure-CodeQuality.ps1 -Library Curl.Extension.Coap.UnitLibrary`: 0 failing members. Coap tests: 116 passed.
- Tests are pure byte arithmetic, platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 161e98d5 0b901c7e b594308d 54cafd17 3d836d5e.
- 2026-10-29: Doing -> Done. The CoAP extension frames DTLS 1.2 records, replay window, handshake fragments and HelloVerifyRequest (RFC 6347)
