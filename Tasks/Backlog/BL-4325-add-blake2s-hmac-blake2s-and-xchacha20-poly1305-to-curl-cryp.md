---
id: BL-4325
title: Add BLAKE2s, HMAC-BLAKE2s and XChaCha20-Poly1305 to Curl.Cryptography
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary/Blake2s.cs, Curl.Cryptography.UnitLibrary/HmacBlake2s.cs, Curl.Cryptography.UnitLibrary/HChaCha20.cs, Curl.Cryptography.UnitLibrary/AeadXChaCha20Poly1305.cs, Curl.Cryptography.UnitTests/Blake2sTests.cs, Curl.Cryptography.UnitTests/HmacBlake2sTests.cs, Curl.Cryptography.UnitTests/HChaCha20Tests.cs, Curl.Cryptography.UnitTests/AeadXChaCha20Poly1305Tests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4325 — Add BLAKE2s, HMAC-BLAKE2s and XChaCha20-Poly1305 to Curl.Cryptography

## Goal

`Curl.Cryptography.UnitLibrary` provides BLAKE2s (keyed and unkeyed, any digest length 1-32), HMAC-BLAKE2s, HChaCha20 and XChaCha20-Poly1305, as the WireGuard extension needs them.

## Context

Split from BL-4147 (WireGuard extension): WireGuard's Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s construction needs BLAKE2s for HASH, MAC and HMAC-based HKDF, and XChaCha20-Poly1305 for cookie replies (WireGuard whitepaper section 5.4.7). `X25519`, `ChaCha20`, `Poly1305` and `AeadChaCha20Poly1305` already exist in the library; follow their style. Specifications: RFC 7693 (BLAKE2), RFC 2104 (HMAC, block size 64 for BLAKE2s), draft-irtf-cfrg-xchacha-03 (HChaCha20, XChaCha20-Poly1305).

## Acceptance criteria

- [ ] `Blake2s` matches RFC 7693 Appendix B ("abc") and the official BLAKE2s keyed test vectors for at least lengths 0, 1, 63, 64, 65 and 255 bytes of input; unit tests.
- [ ] `HmacBlake2s` and `HChaCha20` / `AeadXChaCha20Poly1305` match the test vectors of draft-irtf-cfrg-xchacha-03 sections 2.2.1 and A.3.1, and a tampered tag fails to open; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

BLAKE2s is not anywhere in the repository yet (searched 2026-10-29). Base class library only.

## Log

- 2026-10-29: Created.
