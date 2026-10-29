---
id: BL-3918
title: Share one surrogate-escape UTF-8 implementation instead of four copies
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/InvalidUtf8ByteEscape.cs, Curl.Authentication.UnitLibrary/InvalidUtf8ByteEscapingEncoding.cs, Curl.Protocol.Abstractions.UnitLibrary/RawBytePath.cs, Curl.Cli.UnitLibrary/HeaderFileText.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3918 — Share one surrogate-escape UTF-8 implementation instead of four copies

## Goal

One surrogate-escape UTF-8 implementation (each byte outside a valid UTF-8 sequence becomes the lone surrogate U+DC00 + byte, and back) lives in `Curl.Protocol.Abstractions.UnitLibrary`, and Cli, Authentication and Abstractions all use it instead of their own copies.

## Context

Found by the shift-end review (ADR-0710) of `b0d27d68e..56c134b89`, type-design-analyzer, and checked against the code. The shift added four copies of the same algorithm:

- `Curl.Cli.UnitLibrary/InvalidUtf8ByteEscape.cs:31-77` (`Decode`, `Encode` and a nested `EscapingUtf8Encoding`)
- `Curl.Authentication.UnitLibrary/InvalidUtf8ByteEscapingEncoding.cs` (a near line-for-line copy)
- `Curl.Protocol.Abstractions.UnitLibrary/RawBytePath.cs:16-85` (`FromBytes`/`ToBytes`, the same loop and constants)
- `Curl.Cli.UnitLibrary/HeaderFileText.cs:36-58` (a fourth decoder)

The copies have already drifted. `InvalidUtf8ByteEscapingEncoding` passes code page 65001 to the base `Encoding`, but Cli's `EscapingUtf8Encoding` uses the parameterless constructor and reports code page 0. The bytes must round-trip between libraries. For example, `Curl.Console/RawByteFileName.cs` encodes with Cli's encoding the paths that `RawBytePath` decodes, so a fix to one copy that misses the others breaks that silently. Both Cli and Authentication already reference `Curl.Protocol.Abstractions.UnitLibrary`, so no new project reference is needed.

Neither `Encoding` subclass overrides `GetDecoder()`. A `StreamReader` or chunked `Decoder` over them would escape a multi-byte sequence split at a buffer boundary. Current callers decode whole buffers only, so nothing breaks today. The shared type should document that limit or keep the `Encoding` internal.

## Acceptance criteria

- [x] The escape and unescape loop exists once, in `Curl.Protocol.Abstractions.UnitLibrary`. `git grep -n "0xDC00\|SurrogateOffset" -- "*.cs" ":!*UnitTests*"` finds it only there.
- [x] `InvalidUtf8ByteEscape`, `InvalidUtf8ByteEscapingEncoding` (or their replacement) and `HeaderFileText` call the shared implementation, and every such `Encoding` reports code page 65001.
- [x] The shared type's doc comment states that it decodes whole buffers only, not chunked streams.
- [x] The existing tests of all four call sites still pass. The shared implementation has its own tests in `Curl.Protocol.Abstractions.UnitTests` and holds 100% line and branch coverage.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Added SurrogateEscapeUtf8 (public, Curl.Protocol.Abstractions.UnitLibrary) with Decode, Encode, HoldsEscape and a code-page-65001 Encoding. The Cli and Authentication names stay as thin forwarders so callers and tests are unchanged; RawBytePath and HeaderFileText call it. Other files with DC00 (UrlByteText, CookieEngine and so on) are different algorithms, outside touches.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1289ed73 b93f505f 4d002e90 dc720966 44447f62.
- 2026-10-28: Doing -> Done. One SurrogateEscapeUtf8 in Abstractions; Cli, Authentication, RawBytePath, HeaderFileText use it
