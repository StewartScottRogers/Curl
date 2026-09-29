---
id: BL-729
title: Encode and decode HTTP/3 header blocks with QPACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-720, BL-656]
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-729 — Encode and decode HTTP/3 header blocks with QPACK

## Goal

`Curl.Http3.UnitLibrary` encodes and decodes QPACK field sections (RFC 9204): the static table, the dynamic table with the capacity and blocked-streams limits BL-718's ADR records curl's build advertising, the encoder and decoder stream instructions, required insert count and base, and Huffman string literals through `Curl.Http2.UnitLibrary`'s Huffman codec.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Design: BL-718's ADR. Huffman: the codec BL-656 built for HPACK (RFC 7541 Appendix B is the same code); add the `Curl.Http2.UnitLibrary` reference here and make that codec public there if it is not.
- References: RFC 9204 sections 3 (dynamic table), 4 (wire format), Appendix A (static table), Appendix B (encoding and decoding examples B.1 to B.5).

## Acceptance criteria

- [x] `Curl.Http3.UnitTests` reproduce every RFC 9204 Appendix B example (field sections and encoder/decoder stream bytes), round-trip header lists with and without the dynamic table, and reject an invalid static index, a required insert count beyond the table and an oversized capacity instruction with `QPACK_DECOMPRESSION_FAILED`/`QPACK_ENCODER_STREAM_ERROR`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: RFC 9204 fetched from rfc-editor.org (appendix A static table, appendix B examples). `QpackPrimitives` (62-bit prefixed integers, string literals with the Huffman flag above any prefix, Huffman through `Curl.Http2`'s public `HpackHuffman`), `QpackStaticTable` (99 entries), `QpackDynamicTable` (absolute indexes, eviction arithmetic), `QpackRequiredInsertCount` (section 4.5.1.1), `QpackFieldLineReader` (the five field line forms), `QpackDecoder` and `QpackEncoder`. `Curl.Http2.HeaderField` is reused as the field type; `Curl.Http2.UnitLibrary` itself is unchanged (`HpackHuffman` was already public), so it is not in `touches`.
- Encoder and decoder behaviour decided in ADR-0163 (Decided by Claude under Stewart's delegation). Added `Documentation/Planning/Decisions` to `touches` for that ADR and its README row; no task in Doing (BL-687 Kerberos, BL-724 QUIC) names it.
- Appendix B: `QpackAppendixBTests` has the encoder produce every published field section and encoder stream instruction (B.1 to B.5, Huffman off as the RFC's literals are raw) and the decoder decode them and produce the decoder stream bytes `84`, `01`, `48`, with the RFC's table sizes (106, 160, 217, 215). B.4's section blocks at the decoder until the Duplicate arrives, as the RFC describes.
- Errors: invalid static index, a Required Insert Count beyond the table (unreachable encoding, or blocking with no blocked streams allowed) and a too-large capacity instruction are pinned in `QpackDecoderTests` with `QpackErrorCode.DecompressionFailed` and `EncoderStreamError`; bad decoder stream instructions with `DecoderStreamError` in `QpackEncoderTests`.
- Verified 2026-09-28: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Http3.UnitTests 94 passed); `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary`: 100% line, 100% branch, 85 members, 0 failing, worst CRAP 10.
- Follow-up filed: BL-822 (bound the bytes buffered for an incomplete encoder stream instruction).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Http3 encodes and decodes QPACK field sections and encoder/decoder stream instructions, replaying RFC 9204 appendix B byte for byte
