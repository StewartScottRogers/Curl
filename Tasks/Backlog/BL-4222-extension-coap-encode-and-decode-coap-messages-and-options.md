---
id: BL-4222
title: Extension CoAP: encode and decode CoAP messages and options
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Coap.UnitLibrary/Curl.Extension.Coap.UnitLibrary.csproj, Curl.Extension.Coap.UnitLibrary/CLAUDE.md, Curl.Extension.Coap.UnitLibrary/Wire, Curl.Extension.Coap.UnitTests/Curl.Extension.Coap.UnitTests.csproj, Curl.Extension.Coap.UnitTests/Wire, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4222 — Extension CoAP: encode and decode CoAP messages and options

## Goal

`Curl.Extension.Coap.UnitLibrary` encodes and decodes CoAP messages: header, token, options with delta and length encoding, payload marker and payload, plus the Block1/Block2 and Observe option values.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Creates the project pair (`new-project` skill; `/Extensions/Coap/` folder in `Curl.slnx` under the marker comment, ADR-1003; csproj like `Curl.Extension.GraphQl.UnitLibrary`'s). Specification: RFC 7252 sections 3 (message format), 3.1 (option format), 5.4 and 5.10 (option numbers), 12.1 (codes); RFC 7959 section 2.2 (block option value); RFC 7641 section 2 (Observe option value). Namespace `Curl.Extension.Coap.Wire`. No handler yet and no catalogue line: BL-4223 registers it.

## Acceptance criteria

- [ ] `Curl.Extension.Coap.UnitLibrary` and `Curl.Extension.Coap.UnitTests` exist in `/Extensions/Coap/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] Unit tests in `Curl.Extension.Coap.UnitTests/Wire` round-trip messages against RFC 7252 sections 3 and 3.1 (version, type, token length 0-8, code, message ID, option deltas and lengths with the 13 and 14 extended forms, the 0xFF payload marker) and reject the malformed forms section 3 names (token length 9-15, delta or length nibble 15 outside the marker, a payload marker with no payload).
- [ ] Unit tests check the Block1/Block2 value (NUM, M, SZX) against RFC 7959 section 2.2 and the Observe value against RFC 7641 section 2.
- [ ] A `CLAUDE.md` in the library describes what it holds.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
