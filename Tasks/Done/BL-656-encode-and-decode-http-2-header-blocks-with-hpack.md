---
id: BL-656
title: Encode and decode HTTP/2 header blocks with HPACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-655, BL-715]
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-656 — Encode and decode HTTP/2 header blocks with HPACK

## Goal

An HPACK encoder and decoder in `Curl.Http2.UnitLibrary` implement RFC 7541 (static and dynamic tables, table size updates, integer and string literals, Huffman coding), passing RFC 7541 Appendix C's examples, with the encoder choosing indexing and Huffman as BL-655's ADR records curl's library does where that affects bytes on the wire; the Huffman codec is public so QPACK (BL-729) reuses it.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform, and each hand-built piece the BCL lacks lives in its own library: here `Curl.Http2.UnitLibrary` (BL-715).
- The BCL's HPACK code is internal to `System.Net.Http`; it cannot be referenced. Hand-write it; no package.

## Acceptance criteria

- [x] `Curl.Http2.UnitTests` pass every RFC 7541 Appendix C example (C.2 to C.6), decoder errors (bad index, oversized table update, bad Huffman padding) are typed failures, and round trips hold for header lists with and without Huffman.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built: `HeaderField`, `HpackEncoder`, `HpackDecoder`, public `HpackHuffman`, `HpackDecodingException` with `HpackDecodingError` (truncated block, integer overflow, invalid index, table size update too large / not at start / missing, invalid Huffman padding, EOS in data); internal `HpackPrimitives`, `HpackStaticTable`, `HpackDynamicTable`. 83 tests.
- The Huffman table is stored as appendix B's code lengths only; the code is canonical, so the codes are derived. Tests pin spot codes (symbol 0 `0x1ff8`, EOS `0x3fffffff`, ...) and check the code is complete (Kraft sum 1).
- BL-655's ADR-0141 does not say how the encoder picks representations, so ADR-0147 decides it: follow nghttp2's deflater. Measured with curl.se's Windows build (curl 8.18.0, nghttp2 1.68.0, from WinGet, since the reference Git-for-Windows curl has no HTTP/2) through `Record-CurlExchange.ps1 -Curl <that curl> -HoldOpenMilliseconds 1500` and `--http2-prior-knowledge`. The 90-byte HEADERS block it sent is pinned in `HpackEncoderTests.Encode_CurlsRequestHeaders_GivesTheBlockCurlSent`.
- Names and values are strings with one character per byte (Latin-1), which suits the HTTP/2 layer that sits on top.
- `touches` now includes `Documentation/Planning/Decisions` for ADR-0147 and its index row. No task in Doing names it.
- `Measure-CodeQuality.ps1` throws before measuring because four empty test projects (Kerberos, Ldap, Rtsp, Smb) make `dotnet test` exit non-zero with nothing failed. Measured with `-SkipTestRun -ResultsDirectory` over a Http2-only coverage run instead (100% line, 100% branch, 0 failing members, worst CRAP 10). Filed BL-792 to fix the script.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. HPACK encoder and decoder pass RFC 7541 C.2-C.6 and reproduce curl 8.18.0's h2 header block byte for byte
