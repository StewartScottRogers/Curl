---
id: BL-729
title: Encode and decode HTTP/3 header blocks with QPACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-720, BL-656]
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-729 — Encode and decode HTTP/3 header blocks with QPACK

## Goal

`Curl.Http3.UnitLibrary` encodes and decodes QPACK field sections (RFC 9204): the static table, the dynamic table with the capacity and blocked-streams limits BL-718's ADR records curl's build advertising, the encoder and decoder stream instructions, required insert count and base, and Huffman string literals through `Curl.Http2.UnitLibrary`'s Huffman codec.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Design: BL-718's ADR. Huffman: the codec BL-656 built for HPACK (RFC 7541 Appendix B is the same code); add the `Curl.Http2.UnitLibrary` reference here and make that codec public there if it is not.
- References: RFC 9204 sections 3 (dynamic table), 4 (wire format), Appendix A (static table), Appendix B (encoding and decoding examples B.1 to B.5).

## Acceptance criteria

- [ ] `Curl.Http3.UnitTests` reproduce every RFC 9204 Appendix B example (field sections and encoder/decoder stream bytes), round-trip header lists with and without the dynamic table, and reject an invalid static index, a required insert count beyond the table and an oversized capacity instruction with `QPACK_DECOMPRESSION_FAILED`/`QPACK_ENCODER_STREAM_ERROR`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
