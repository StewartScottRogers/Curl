---
id: BL-686
title: Encrypt, decrypt and checksum with the Kerberos AES and RC4 encryption types
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685, BL-675, BL-676]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-686 — Encrypt, decrypt and checksum with the Kerberos AES and RC4 encryption types

## Goal

`Curl.Kerberos.UnitLibrary` implements the Kerberos encryption types `aes256-cts-hmac-sha1-96`, `aes128-cts-hmac-sha1-96` (RFC 3962), `aes128-cts-hmac-sha256-128`, `aes256-cts-hmac-sha384-192` (RFC 8009) and `rc4-hmac` (RFC 4757): string-to-key, key derivation, encrypt, decrypt with integrity check, and their checksums, matching the RFCs' test vectors.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Framework: RFC 3961 (simplified profile, `DK`, `n-fold`). AES in CBC with ciphertext stealing is built on the BCL's `Aes`; `Rfc2898DeriveBytes.Pbkdf2`, `HMACSHA1`, `HMACSHA256`, `HMACSHA384` and `HMACMD5` are BCL; MD4 and RC4 come from `Curl.Cryptography.UnitLibrary` (BL-675, BL-676; add the reference here).
- Vectors: RFC 3961 appendix A.1 (n-fold), RFC 3962 appendix B (string-to-key and CTS), RFC 8009 appendix A (all sample results), RFC 4757 for `rc4-hmac` (and its key usage mapping).
- Randomness (the confounder) is injected so encryption is deterministic in tests.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` pass RFC 3961 A.1 n-fold vectors, RFC 3962 appendix B string-to-key and AES-CTS vectors, every RFC 8009 appendix A vector (key derivation, encryption, checksum), and an `rc4-hmac` round trip with a known key; a tampered ciphertext fails the integrity check with a typed failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
