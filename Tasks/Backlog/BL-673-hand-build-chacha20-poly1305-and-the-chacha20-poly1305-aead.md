---
id: BL-673
title: Hand-build ChaCha20, Poly1305 and the ChaCha20-Poly1305 AEAD to RFC 8439
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-673 — Hand-build ChaCha20, Poly1305 and the ChaCha20-Poly1305 AEAD to RFC 8439

## Goal

`Curl.Cryptography.UnitLibrary` exposes the ChaCha20 block function and stream cipher (with the counter and nonce as parameters), the Poly1305 one-time authenticator, and the ChaCha20-Poly1305 AEAD, each exactly as RFC 8439 specifies, on every platform.

## Context

- Consumers: `chacha20-poly1305@openssh.com` (BL-679; OpenSSH's construction uses raw ChaCha20 with a 64-bit nonce and a separate Poly1305 key, described in OpenSSH's `PROTOCOL.chacha20poly1305`, so the raw pieces must be public), QUIC header protection with ChaCha20 (RFC 9001 section 5.4.4, BL-723), TLS 1.3 `TLS_CHACHA20_POLY1305_SHA256` (BL-699). API and rules: BL-669's ADR.
- The BCL's `System.Security.Cryptography.ChaCha20Poly1305` hides the raw pieces and is not supported on every platform, so it is not used here.
- RFC 8439 section 2 (algorithms) and Appendix A (test vectors A.1 to A.5). The original DJB ChaCha20 with a 64-bit nonce and 64-bit counter (for OpenSSH) is covered by allowing the counter/nonce split the ADR names.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass RFC 8439 sections 2.1.1, 2.2.1, 2.3.2, 2.4.2, 2.5.2, 2.6.2 and 2.8.2 and Appendix A.1 to A.5 vectors, including the AEAD decryption vector with a tag failure case.
- [ ] Tag comparison uses `CryptographicOperations.FixedTimeEquals`; a wrong tag returns the typed failure the ADR names and writes no plaintext.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
