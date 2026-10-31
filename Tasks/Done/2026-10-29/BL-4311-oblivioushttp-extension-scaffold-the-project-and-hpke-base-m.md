---
id: BL-4311
title: ObliviousHttp extension: scaffold the project and HPKE base mode (RFC 9180)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/Curl.Extension.ObliviousHttp.UnitLibrary.csproj, Curl.Extension.ObliviousHttp.UnitLibrary/CLAUDE.md, Curl.Extension.ObliviousHttp.UnitLibrary/Hpke, Curl.Extension.ObliviousHttp.UnitTests/Curl.Extension.ObliviousHttp.UnitTests.csproj, Curl.Extension.ObliviousHttp.UnitTests/Hpke, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4311 — ObliviousHttp extension: scaffold the project and HPKE base mode (RFC 9180)

## Goal

`Curl.Extension.ObliviousHttp.UnitLibrary` exists with an HPKE base-mode sender and receiver (RFC 9180 sections 4, 5.1 and 5.2) that Oblivious HTTP's encapsulation (BL-4310) builds on.

## Context

Split from BL-4137 (Oblivious HTTP relay extension) for size. Create the project pair with the `new-project` skill under `/Extensions/ObliviousHttp/` in `Curl.slnx` (ADR-0999, CLAUDE.md "Extensions"); do not register it in any Composition catalogue yet (BL-4137 does that). Base mode only, with KEM DHKEM(X25519, HKDF-SHA256) (0x0020), KDF HKDF-SHA256 (0x0001) and AEADs AES-128-GCM (0x0001), AES-256-GCM (0x0002) and ChaCha20Poly1305 (0x0003). X25519 is `Curl.Cryptography.X25519` (a shared layer, allowed by `VerifyExtensionIsolation`); HKDF and AES-GCM are `System.Security.Cryptography`; use `Curl.Cryptography`'s ChaCha20-Poly1305 if the BCL one is not supported on every platform. Include `Export` (RFC 9180 section 5.3) since RFC 9458 section 4.4 derives the response key with it. Put the code in `Hpke/`.

## Acceptance criteria

- [x] `Curl.Extension.ObliviousHttp.UnitLibrary` and `Curl.Extension.ObliviousHttp.UnitTests` exist in `/Extensions/ObliviousHttp/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [x] Sender setup, seal, open and export reproduce the RFC 9180 Appendix A base-mode test vectors for DHKEM(X25519, HKDF-SHA256) with HKDF-SHA256 and each of AES-128-GCM and ChaCha20Poly1305 (A.1.1 and A.2.1), using the vector's ephemeral key; unit tests.
- [x] A tampered ciphertext or wrong key makes open fail with a typed exception; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `Curl.Cryptography.UnitLibrary` already holds a complete, vector-tested HPKE base mode (`Hpke`, `HpkeContext`, for ECH) with exactly these suites, so `Hpke/` wraps it rather than building a second one: `HpkeSuite`, `HpkeSender` (`SetUp` fresh or with a given skE, `EncapsulatedKey`, `Seal`, `Export`), `HpkeReceiver` (`SetUp`, `Open`, `Export`), `HpkeExport`, and the typed `HpkeSetupException` and `HpkeOpenException` (both `CryptographicException`) in place of `Curl.Cryptography`'s `false`. ADR-1104 records it. The library references only `Curl.Cryptography.UnitLibrary`.
- Added `Curl.Extension.ObliviousHttp.UnitLibrary/CLAUDE.md` to `touches`: the new project's own rules file, which no other task can hold.
- Tests: 25 in `Curl.Extension.ObliviousHttp.UnitTests` (A.1.1 and A.2.1 enc, both ciphertexts in sequence, 32-byte export from both sides; tampered ciphertext, wrong skR, wrong aad and a short ciphertext throw `HpkeOpenException`; short keys throw `HpkeSetupException`).
- Measure-CodeQuality.ps1 -Library Curl.Extension.ObliviousHttp.UnitLibrary: line 100%, branch 100%, 16 members, 0 failing, worst CRAP 2.
- `dotnet build` clean (VerifyExtensionIsolation passes); the full fast run passed except `Curl.Extension.Coap.UnitTests`' `LostServerHelloFlight` DTLS timer test, which passed alone straight afterwards: filed as BL-4314. The change is platform-neutral (no paths, OS text or certificates), which covers CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 50429c5b efc415fa a9f33b76 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.ObliviousHttp has HPKE base-mode HpkeSender and HpkeReceiver over Curl.Cryptography's Hpke, reproducing RFC 9180 A.1.1 and A.2.1, with typed setup and open exceptions
