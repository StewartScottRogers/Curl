---
id: BL-677
title: Hand-build HPKE base mode to RFC 9180 for Encrypted Client Hello
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-671, BL-673]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-677 — Hand-build HPKE base mode to RFC 9180 for Encrypted Client Hello

## Goal

`Curl.Cryptography.UnitLibrary` seals messages with HPKE base mode (RFC 9180) for the suites Encrypted Client Hello uses: DHKEM(X25519, HKDF-SHA256) and DHKEM(P-256, HKDF-SHA256), HKDF-SHA256, and AES-128-GCM, AES-256-GCM or ChaCha20-Poly1305.

## Context

- Consumer: ECH in the hand-built TLS 1.3 client (BL-706) for `--ech`. Builds on X25519 (BL-671) and ChaCha20-Poly1305 (BL-673); P-256 via the BCL's `ECDiffieHellman`, HKDF via `System.Security.Cryptography.HKDF`, AES-GCM via `AesGcm` or the hand-built fallback BL-669's ADR names. API: BL-669's ADR.
- RFC 9180 sections 4 (DHKEM), 5.1 (key schedule), 5.2 (encryption and nonce sequence), Appendix A (test vectors A.1.1, A.2.1, A.3.1 and the ChaCha20-Poly1305 variants: base mode only).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` reproduce the base-mode Appendix A vectors for each supported suite: `enc`, shared secret, key, base nonce, and the first sealed ciphertexts, with a deterministic ephemeral key injected as the RFC's vectors require.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
