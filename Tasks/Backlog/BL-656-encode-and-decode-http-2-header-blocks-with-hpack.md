---
id: BL-656
title: Encode and decode HTTP/2 header blocks with HPACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-655, BL-715]
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-656 — Encode and decode HTTP/2 header blocks with HPACK

## Goal

An HPACK encoder and decoder in `Curl.Http2.UnitLibrary` implement RFC 7541 (static and dynamic tables, table size updates, integer and string literals, Huffman coding), passing RFC 7541 Appendix C's examples, with the encoder choosing indexing and Huffman as BL-655's ADR records curl's library does where that affects bytes on the wire; the Huffman codec is public so QPACK (BL-729) reuses it.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform, and each hand-built piece the BCL lacks lives in its own library: here `Curl.Http2.UnitLibrary` (BL-715).
- The BCL's HPACK code is internal to `System.Net.Http`; it cannot be referenced. Hand-write it; no package.

## Acceptance criteria

- [ ] `Curl.Http2.UnitTests` pass every RFC 7541 Appendix C example (C.2 to C.6), decoder errors (bad index, oversized table update, bad Huffman padding) are typed failures, and round trips hold for header lists with and without Huffman.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
