---
id: BL-4245
title: Extension CoAP: encode and decode CoAP messages and options
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Coap.UnitLibrary/Curl.Extension.Coap.UnitLibrary.csproj, Curl.Extension.Coap.UnitLibrary/CLAUDE.md, Curl.Extension.Coap.UnitLibrary/Wire, Curl.Extension.Coap.UnitTests/Curl.Extension.Coap.UnitTests.csproj, Curl.Extension.Coap.UnitTests/Wire, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4245 — Extension CoAP: encode and decode CoAP messages and options

## Goal

`Curl.Extension.Coap.UnitLibrary` encodes and decodes CoAP messages: header, token, options with delta and length encoding, payload marker and payload, plus the Block1/Block2 and Observe option values.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Creates the project pair (`new-project` skill; `/Extensions/Coap/` folder in `Curl.slnx` under the marker comment, ADR-1003; csproj like `Curl.Extension.GraphQl.UnitLibrary`'s). Specification: RFC 7252 sections 3 (message format), 3.1 (option format), 5.4 and 5.10 (option numbers), 12.1 (codes); RFC 7959 section 2.2 (block option value); RFC 7641 section 2 (Observe option value). Namespace `Curl.Extension.Coap.Wire`. No handler yet and no catalogue line: BL-4246 registers it.

## Acceptance criteria

- [x] `Curl.Extension.Coap.UnitLibrary` and `Curl.Extension.Coap.UnitTests` exist in `/Extensions/Coap/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [x] Unit tests in `Curl.Extension.Coap.UnitTests/Wire` round-trip messages against RFC 7252 sections 3 and 3.1 (version, type, token length 0-8, code, message ID, option deltas and lengths with the 13 and 14 extended forms, the 0xFF payload marker) and reject the malformed forms section 3 names (token length 9-15, delta or length nibble 15 outside the marker, a payload marker with no payload).
- [x] Unit tests check the Block1/Block2 value (NUM, M, SZX) against RFC 7959 section 2.2 and the Observe value against RFC 7641 section 2.
- [x] A `CLAUDE.md` in the library describes what it holds.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: a `Wire` namespace of small types: `CoapMessage`/`CoapOption`/`CoapCode`/`CoapMessageType`, `CoapMessageEncoding` (Encode/Decode), `CoapOptionNumber`, and the option value formats `CoapUnsignedInteger`, `CoapBlockValue`, `CoapObserveValue`. Decided by Claude (sensible default): types are `internal` (BL-4246's handler lives in the same library); malformed input throws `FormatException`, bad arguments to Encode throw `ArgumentException`; `Encode` sorts options by number (stable, so repeated options keep their order); `Decode` also rejects an Empty (0.00) message with bytes after its header (RFC 7252 section 4.1) and a block value with the reserved SZX 7.
- Verified: `dotnet build` clean (so `VerifyExtensionIsolation` passed), 68 tests in Curl.Extension.Coap.UnitTests pass, the fast tests pass (all 63 test assemblies), `Measure-CodeQuality.ps1 -Library Curl.Extension.Coap.UnitLibrary`: 100% line, 100% branch, max complexity 10, 0 failing members.
- Platform: pure byte computation, no file system, path, OS error or certificate, so CI on Linux and macOS is unaffected.

- Plan: a `Wire` namespace of small types: `CoapMessage`/`CoapOption`/`CoapCode`/`CoapMessageType`, `CoapMessageEncoding` (Encode/Decode), `CoapOptionNumber`, and the option value formats `CoapUnsignedInteger`, `CoapBlockValue`, `CoapObserveValue`. Decided by Claude (sensible default): types are `internal` (BL-4246's handler lives in the same library); malformed input throws `FormatException`, bad arguments to Encode throw `ArgumentException`; `Encode` sorts options by number (stable, so repeated options keep their order); `Decode` also rejects an Empty (0.00) message with bytes after its header (RFC 7252 section 4.1) and a block value with the reserved SZX 7.
- Verified: `dotnet build` clean (so `VerifyExtensionIsolation` passed), 68 tests in Curl.Extension.Coap.UnitTests pass, the fast tests pass (all 63 test assemblies), `Measure-CodeQuality.ps1 -Library Curl.Extension.Coap.UnitLibrary`: 100% line, 100% branch, max complexity 10, 0 failing members.
- Platform: pure byte computation, no file system, path, OS error or certificate, so CI on Linux and macOS is unaffected.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 954eeb9c 9fd73c22 d877c528 a4a68863 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.Coap.UnitLibrary encodes and decodes CoAP messages, options, Block1/Block2 and Observe values per RFC 7252, 7959 and 7641
