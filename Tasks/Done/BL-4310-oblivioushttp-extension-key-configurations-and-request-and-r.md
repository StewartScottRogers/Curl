---
id: BL-4310
title: ObliviousHttp extension: key configurations and request and response encapsulation (RFC 9458, RFC 9540)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4311]
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/Encapsulation, Curl.Extension.ObliviousHttp.UnitTests/Encapsulation, Curl.Extension.ObliviousHttp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4310 — ObliviousHttp extension: key configurations and request and response encapsulation (RFC 9458, RFC 9540)

## Goal

`Curl.Extension.ObliviousHttp.UnitLibrary` reads a gateway's key configurations and encapsulates a request to, and decapsulates a response from, an Oblivious HTTP gateway (RFC 9458 sections 3 and 4).

## Context

Split from BL-4137 (Oblivious HTTP relay extension) for size; builds on BL-4311's HPKE in `Hpke/`. Code goes in `Encapsulation/`. Key configuration: one `key_config` (RFC 9458 section 3.1) and the `application/ohttp-keys` list of length-prefixed configurations (section 3.2, as RFC 9540 section 3 serves it), choosing the first configuration whose KEM, KDF and AEAD the library supports. Request encapsulation per section 4.3 (header of key id, kem, kdf, aead; HPKE info "message/bhttp request"), response decapsulation per section 4.4 (export secret "message/bhttp response", response_nonce, HKDF-derived AEAD key and nonce). Media types `message/ohttp-req` and `message/ohttp-res` as constants here.

## Acceptance criteria

- [x] Parsing the key configuration of RFC 9458 Appendix A gives its key id, KEM, public key and symmetric suites; a list holding it parses the same; unit tests.
- [x] Encapsulating the Appendix A request with its ephemeral key reproduces its encapsulated request bytes, and decapsulating its encapsulated response with that context gives its response bytes; unit tests.
- [x] A truncated configuration, a configuration with no supported suite, and a response with a wrong tag each fail with a typed exception; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: `Encapsulation/` holds `KeyConfiguration` (one RFC 9458 section 3.1 configuration), `KeyConfigurationList`
  (the section 3.2 `application/ohttp-keys` list and `SelectFirstSupported`, giving a `KeyConfigurationChoice`),
  `ObliviousRequest` (section 4.3 encapsulation, keeping the HPKE sender context for section 4.4 response
  decapsulation; the response AEAD is in `ResponseAead`) and `ObliviousHttpMediaTypes`. Decisions are in ADR-1111: a list
  leaves out entries whose KEM is unknown, because their length prefix lets it skip them, while a single configuration
  with an unknown KEM throws, because its public key cannot be delimited. The first supported KDF and AEAD pair in the
  gateway's order is chosen. The three exceptions are `MalformedKeyConfigurationException` (FormatException),
  `UnsupportedKeyConfigurationException` (NotSupportedException) and `EncapsulatedResponseException`
  (CryptographicException).
- Appendix A hex values were taken from the RFC text (rfc-editor.org/rfc/rfc9458.txt). The encapsulated request and
  response reproduce byte for byte. AES-256-GCM and ChaCha20Poly1305 responses are checked against a gateway side
  written in the test from the RFC.
- Added `Curl.Extension.ObliviousHttp.UnitLibrary/CLAUDE.md` to touches so it can describe `Encapsulation/`. No task
  in Doing on origin/work/dark-factory names it (BL-4309 holds only `BinaryHttp/`).
- `Measure-CodeQuality.ps1 -Library Curl.Extension.ObliviousHttp.UnitLibrary`: 100% line, 100% branch, 40 members,
  0 failing, worst CRAP 8. The first run found `ParseIfKemSupported` at complexity 12, so the suite check moved into
  `ReadSuites`. `dotnet build` is clean and the fast tests are green. The tests are platform-neutral (bytes only, no
  paths or OS text).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: fc74dce3 a0eee265 73ea1b26 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Key configurations parse, the first supported suite is chosen, and requests encapsulate and responses decapsulate per RFC 9458 Appendix A
