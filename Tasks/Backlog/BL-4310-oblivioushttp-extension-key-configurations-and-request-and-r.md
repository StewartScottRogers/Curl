---
id: BL-4310
title: ObliviousHttp extension: key configurations and request and response encapsulation (RFC 9458, RFC 9540)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4308]
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/Encapsulation, Curl.Extension.ObliviousHttp.UnitTests/Encapsulation]
requirement: none
created: 2026-10-29
completed:
---
# BL-4310 — ObliviousHttp extension: key configurations and request and response encapsulation (RFC 9458, RFC 9540)

## Goal

`Curl.Extension.ObliviousHttp.UnitLibrary` reads a gateway's key configurations and encapsulates a request to, and decapsulates a response from, an Oblivious HTTP gateway (RFC 9458 sections 3 and 4).

## Context

Split from BL-4137 (Oblivious HTTP relay extension) for size; builds on BL-4308's HPKE in `Hpke/`. Code goes in `Encapsulation/`. Key configuration: one `key_config` (RFC 9458 section 3.1) and the `application/ohttp-keys` list of length-prefixed configurations (section 3.2, as RFC 9540 section 3 serves it), choosing the first configuration whose KEM, KDF and AEAD the library supports. Request encapsulation per section 4.3 (header of key id, kem, kdf, aead; HPKE info "message/bhttp request"), response decapsulation per section 4.4 (export secret "message/bhttp response", response_nonce, HKDF-derived AEAD key and nonce). Media types `message/ohttp-req` and `message/ohttp-res` as constants here.

## Acceptance criteria

- [ ] Parsing the key configuration of RFC 9458 Appendix A gives its key id, KEM, public key and symmetric suites; a list holding it parses the same; unit tests.
- [ ] Encapsulating the Appendix A request with its ephemeral key reproduces its encapsulated request bytes, and decapsulating its encapsulated response with that context gives its response bytes; unit tests.
- [ ] A truncated configuration, a configuration with no supported suite, and a response with a wrong tag each fail with a typed exception; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
