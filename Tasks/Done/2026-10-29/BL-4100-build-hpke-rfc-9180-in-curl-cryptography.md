---
id: BL-4100
title: Build HPKE (RFC 9180) in Curl.Cryptography
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitTests/HpkeContextTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4100 — Build HPKE (RFC 9180) in Curl.Cryptography

## Goal

Curl.Cryptography offers HPKE base mode with DHKEM(X25519, HKDF-SHA256), HKDF-SHA256 and AES-128-GCM / ChaCha20-Poly1305, for Oblivious HTTP and Oblivious DoH.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Shared by two extensions, so it lives in the shared cryptography layer. Reuse the X25519 and ChaCha20-Poly1305 already hand-built for SSH.

## Acceptance criteria

- [x] Every RFC 9180 Appendix A test vector for those suites passes (seal and open, exporter).
- [x] Comparisons of secrets are constant-time (reviewed against `CryptographicOperations.FixedTimeEquals`).
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- HPKE base mode was already built for Encrypted Client Hello by BL-677 (a677034d2): `Curl.Cryptography.UnitLibrary/Hpke*.cs` at the library root, not in an `Hpke/` folder, with DHKEM(X25519, HKDF-SHA256) on the SSH-built `X25519`, HKDF-SHA256, AES-128-GCM and the hand-built `AeadChaCha20Poly1305`. So this task only completes the Appendix A vectors; `touches` was narrowed to the one test file it changed, the folders it first named do not exist.
- Added to `HpkeContextTests.cs`: A.2.1.2's two exports with a non-empty exporter context, and the seal and open of sequence numbers 2, 4, 255 and 256 for A.1.1.1 and A.2.1.1. With the existing tests every A.1.1 and A.2.1 base-mode vector (setup, all six encryptions, all three exports) now passes.
- Constant time: the only secret comparisons are the AEAD tags, `AeadChaCha20Poly1305.TryDecrypt` uses `CryptographicOperations.FixedTimeEquals` and AES-GCM is the BCL `AesGcm`; X25519's all-zero check is `ConstantTime.IsAllZero`. `HpkeDhkem.IsOnP256` compares public data only.
- Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary: 100% line, 100% branch, 0 failing members. Solution build clean, fast tests green. Tests are pure computation, platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 95196793 a1d6e106 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Every RFC 9180 A.1.1 and A.2.1 base-mode vector passes through Curl.Cryptography's HPKE
