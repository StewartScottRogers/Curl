---
id: BL-4309
title: ObliviousHttp extension: Binary HTTP known-length request encoding and response decoding (RFC 9292)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4311]
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/BinaryHttp, Curl.Extension.ObliviousHttp.UnitTests/BinaryHttp]
requirement: none
created: 2026-10-29
completed:
---
# BL-4309 — ObliviousHttp extension: Binary HTTP known-length request encoding and response decoding (RFC 9292)

## Goal

`Curl.Extension.ObliviousHttp.UnitLibrary` encodes a request as a known-length Binary HTTP message and decodes a known-length or indeterminate-length Binary HTTP response (RFC 9292), which Oblivious HTTP carries inside its encapsulation.

## Context

Split from BL-4137 (Oblivious HTTP relay extension) for size; the project is created by BL-4311. Code goes in `BinaryHttp/`. Request: framing indicator 0, control data (method, scheme, authority, path), header section, content, empty trailer section (RFC 9292 sections 3.2 to 3.6), with QUIC variable-length integers (RFC 9000 section 16) written here, not taken from a protocol library. Response: framing indicators 1 and 3, informational responses (1xx) before the final one, header and trailer sections, and padding (section 3.8). Malformed input gives a typed exception.

## Acceptance criteria

- [ ] Encoding a GET request reproduces the known-length example bytes of RFC 9292 section 5; unit tests.
- [ ] Decoding the RFC 9292 section 5 response examples (known-length and indeterminate-length, with an informational response) gives the status, headers and content they show; unit tests.
- [ ] Truncated input, a bad framing indicator and an over-long length each fail with a typed exception; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
