---
id: BL-1109
title: Pin GaloisCounterMode to the GCM specification's 96-bit-IV known answers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1109 — Pin GaloisCounterMode to the GCM specification's 96-bit-IV known answers

## Goal

The hand-built `GaloisCounterMode` is checked against published known answers, not only against the BCL: every 96-bit-IV test case of the GCM specification passes over AES-128, AES-192 and AES-256, and each case's tag, ciphertext and associated data, changed by one bit, is refused.

## Context

- `Curl.Cryptography.UnitTests/GaloisCounterModeTests.cs` tests `Curl.Cryptography.UnitLibrary/GaloisCounterMode.cs` only by matching `System.Security.Cryptography.AesGcm` on seeded random inputs (`EncryptAndTryDecrypt_OverAes_MatchAesGcmOnRandomInputs`), plus an in-place round trip and `Multiply_ByOne`. No published vector is pinned, and no test changes a bit of a tag, ciphertext or associated data and expects `TryDecrypt` to return `false` at this level (only `AeadAriaGcmTests` does, for ARIA). Every other cipher in the library is pinned to its RFC or NIST vectors (see `AeadAesCcmTests`, `AeadChaCha20Poly1305Tests`, `AesCtrTests`).
- Source of the vectors: McGrew and Viega, "The Galois/Counter Mode of Operation (GCM)", revised submission to NIST, Appendix B, Test Cases 1-4 (AES-128), 7-10 (AES-192) and 13-16 (AES-256) - the cases with a 96-bit IV, the only nonce length `GaloisCounterMode.NonceSize` supports. The same vectors are reproduced in NIST's CAVP `gcmEncryptExtIV*.rsp` and in the Go and OpenSSL test suites. Anchors to check the copy: Test Case 1 (key and IV all zero, no plaintext) has tag `58e2fccefa7e3061367f1d57a4e7455a`; Test Case 2 (16 zero plaintext bytes) has ciphertext `0388dace60b6a392f328c2b971b2fe78` and tag `ab6e47d42cec13bdf53a67b21257bddf`.
- Run the mode over the test file's existing `AesBlockCipher` helper (the BCL's AES in ECB, one block at a time), so the test needs nothing new from the library.
- Cases 5, 6, 11, 12, 17 and 18 use 64-bit and 480-bit IVs; leave them out and say so in the test class's summary.

## Acceptance criteria

- [x] A data-driven test in `GaloisCounterModeTests.cs`, for example `EncryptAndTryDecrypt_GcmSpecificationTestCase_GiveThePublishedCiphertextAndTag`, pins all twelve 96-bit-IV cases: `Encrypt` gives the published ciphertext and tag and `TryDecrypt` gives the plaintext back.
- [x] A test flips one bit of the tag, of the ciphertext and of the associated data of Test Case 4 in turn and asserts `TryDecrypt` returns `false`.
- [x] The tests pass on Windows, Linux and macOS (they use only `Aes` in ECB, which all three support).
- [x] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Vectors typed from the GCM specification's Appendix B and cross-checked before pinning: a throwaway PowerShell run of the BCL's `AesGcm` over all twelve cases reproduced every published ciphertext and tag (Test Case 15's tag is `b094dac5d93471bdec1a502270e3cc6c`).
- Bit-flip test flips the first and last bit of each of Test Case 4's tag, ciphertext and associated data (six cases).
- Fast tests green. `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line, 100% branch, 0 failing members (read with `-SkipTestRun`: the full coverage run hit one `Curl.Conformance.UnitTests` failure under load that passed on rerun, unrelated to this test-only change).

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. GaloisCounterMode pinned to all twelve 96-bit-IV GCM specification known answers, with one-bit tamper refusal over Test Case 4
