---
id: BL-4308
title: ObliviousHttp extension: scaffold the project and HPKE base mode (RFC 9180)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/Curl.Extension.ObliviousHttp.UnitLibrary.csproj, Curl.Extension.ObliviousHttp.UnitLibrary/Hpke, Curl.Extension.ObliviousHttp.UnitTests/Curl.Extension.ObliviousHttp.UnitTests.csproj, Curl.Extension.ObliviousHttp.UnitTests/Hpke, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4308 — ObliviousHttp extension: scaffold the project and HPKE base mode (RFC 9180)

## Goal

`Curl.Extension.ObliviousHttp.UnitLibrary` exists with an HPKE base-mode sender and receiver (RFC 9180 sections 4, 5.1 and 5.2) that Oblivious HTTP's encapsulation (BL-4310) builds on.

## Context

Split from BL-4137 (Oblivious HTTP relay extension) for size. Create the project pair with the `new-project` skill under `/Extensions/ObliviousHttp/` in `Curl.slnx` (ADR-0999, CLAUDE.md "Extensions"); do not register it in any Composition catalogue yet (BL-4137 does that). Base mode only, with KEM DHKEM(X25519, HKDF-SHA256) (0x0020), KDF HKDF-SHA256 (0x0001) and AEADs AES-128-GCM (0x0001), AES-256-GCM (0x0002) and ChaCha20Poly1305 (0x0003). X25519 is `Curl.Cryptography.X25519` (a shared layer, allowed by `VerifyExtensionIsolation`); HKDF and AES-GCM are `System.Security.Cryptography`; use `Curl.Cryptography`'s ChaCha20-Poly1305 if the BCL one is not supported on every platform. Include `Export` (RFC 9180 section 5.3) since RFC 9458 section 4.4 derives the response key with it. Put the code in `Hpke/`.

## Acceptance criteria

- [ ] `Curl.Extension.ObliviousHttp.UnitLibrary` and `Curl.Extension.ObliviousHttp.UnitTests` exist in `/Extensions/ObliviousHttp/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] Sender setup, seal, open and export reproduce the RFC 9180 Appendix A base-mode test vectors for DHKEM(X25519, HKDF-SHA256) with HKDF-SHA256 and each of AES-128-GCM and ChaCha20Poly1305 (A.1.1 and A.2.1), using the vector's ephemeral key; unit tests.
- [ ] A tampered ciphertext or wrong key makes open fail with a typed exception; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
