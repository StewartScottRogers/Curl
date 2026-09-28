---
id: BL-656
title: Encode and decode HTTP/2 header blocks with HPACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-655]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-656 — Encode and decode HTTP/2 header blocks with HPACK

## Goal

An HPACK encoder and decoder in `Curl.Protocol.Http.UnitLibrary` implement RFC 7541 (static and dynamic tables, table size updates, integer and string literals, Huffman coding), passing RFC 7541 Appendix C's examples, with the encoder choosing indexing and Huffman as BL-655's ADR records curl's library does where that affects bytes on the wire.

## Context

- Conformance audit 2026-09-28, row 32. If BL-655's ADR decides not to offer HTTP/2, move this task to `Deferred` with that reason.
- The BCL's HPACK code is internal to `System.Net.Http`; it cannot be referenced. Hand-write it; no package.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pass every RFC 7541 Appendix C example (C.2 to C.6), decoder errors (bad index, oversized table update, bad Huffman padding) are typed failures, and round trips hold for header lists with and without Huffman.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
