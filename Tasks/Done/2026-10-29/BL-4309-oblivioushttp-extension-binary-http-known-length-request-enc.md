---
id: BL-4309
title: ObliviousHttp extension: Binary HTTP known-length request encoding and response decoding (RFC 9292)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4311]
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/BinaryHttp, Curl.Extension.ObliviousHttp.UnitTests/BinaryHttp, Curl.Extension.ObliviousHttp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4309 — ObliviousHttp extension: Binary HTTP known-length request encoding and response decoding (RFC 9292)

## Goal

`Curl.Extension.ObliviousHttp.UnitLibrary` encodes a request as a known-length Binary HTTP message and decodes a known-length or indeterminate-length Binary HTTP response (RFC 9292), which Oblivious HTTP carries inside its encapsulation.

## Context

Split from BL-4137 (Oblivious HTTP relay extension) for size; the project is created by BL-4311. Code goes in `BinaryHttp/`. Request: framing indicator 0, control data (method, scheme, authority, path), header section, content, empty trailer section (RFC 9292 sections 3.2 to 3.6), with QUIC variable-length integers (RFC 9000 section 16) written here, not taken from a protocol library. Response: framing indicators 1 and 3, informational responses (1xx) before the final one, header and trailer sections, and padding (section 3.8). Malformed input gives a typed exception.

## Acceptance criteria

- [x] Encoding a GET request reproduces the known-length example bytes of RFC 9292 section 5; unit tests.
- [x] Decoding the RFC 9292 section 5 response examples (known-length and indeterminate-length, with an informational response) gives the status, headers and content they show; unit tests.
- [x] Truncated input, a bad framing indicator and an over-long length each fail with a typed exception; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly rather than through the full `/feature` subagent stages: one folder, a specification with
  exact test vectors, and the cost cap favoured doing it in-thread.
- `BinaryHttp/`: `QuicVariableLengthInteger` (write), `BinaryHttpReader` (read varints, length-prefixed bytes,
  padding), `BinaryHttpRequestEncoder.EncodeKnownLength`, `BinaryHttpResponseDecoder.Decode`, records
  `BinaryHttpField`, `BinaryHttpRequest`, `BinaryHttpInformationalResponse`, `BinaryHttpResponse`, and
  `BinaryHttpFormatException : FormatException`.
- Defaults taken: field names and values are Latin-1 strings, so every byte round-trips; the encoder lower-cases
  field names (RFC 9292 section 3.6 follows HTTP/2's lower-case rule) and rejects an empty one; the decoder validates
  padding (section 3.8 allows skipping that check, but validating is stricter and costs nothing). It does not
  reject pseudo-field names or field bytes HTTP forbids; BL-4137's relay can add that if it needs it.
- Test vectors: RFC 9292 Figure 8 (encoder), Figures 11 and 13 (decoder), plus RFC 9000 section 16 / Appendix A.1
  integer examples. Taken from the RFC text at rfc-editor.org.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.ObliviousHttp.UnitLibrary` gave 100% line, 100% branch,
  0 failing members, highest complexity 8.
- Added `Curl.Extension.ObliviousHttp.UnitLibrary/CLAUDE.md` to touches to document `BinaryHttp/`; no task in Doing
  on origin/work/dark-factory held it (BL-4310 holds only `Encapsulation/`).
- Platform: the tests are pure byte computation with no paths or OS text, so they are platform-neutral; fast tests
  green on Windows (90 test projects).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 505ca4e0 38c0c021 65982c12 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Binary HTTP known-length request encoding and known- and indeterminate-length response decoding (RFC 9292) work, checked against the RFC section 5 examples
