---
id: BL-737
title: Hand-build the AES-CTR and AES-CBC-CTS modes on the BCL AES block cipher
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-737 — Hand-build the AES-CTR and AES-CBC-CTS modes on the BCL AES block cipher

## Goal

`Curl.Cryptography.UnitLibrary` exposes `AesCtr` (NIST SP 800-38A counter mode with a 128-bit big-endian counter, as SSH's `aes128-ctr`, `aes192-ctr` and `aes256-ctr` use it, RFC 4344) and `AesCbcCts` (CBC with ciphertext stealing as RFC 3962 defines it for Kerberos), both built on the BCL's `Aes.EncryptEcb`/`EncryptCbc`, on every platform.

## Context

- ADR-0118 (BL-669): the BCL has AES-ECB, AES-CBC and AES-CFB on every CI platform but neither CTR nor CTS, so both modes are built here on the BCL block cipher. API, namespace, constant-time and zeroing rules: ADR-0118.
- Consumers: SSH `aes*-ctr` packet encryption (BL-680) and the `aes256-ctr` cipher of encrypted `openssh-key-v1` keys (BL-681); Kerberos `aes128-cts-hmac-sha1-96`, `aes256-cts-hmac-sha1-96`, `aes128-cts-hmac-sha256-128` and `aes256-cts-hmac-sha384-192` (BL-686, which uses this `AesCbcCts` rather than a copy of its own).
- `AesCtr` is a keyed stream: it keeps its counter between calls so an SSH session can encrypt packet after packet, and zeroes its key and keystream on `Dispose`.
- Vectors: NIST SP 800-38A appendix F.5.1 to F.5.6 (CTR-AES128/192/256 encrypt and decrypt); RFC 3962 appendix B (the six AES-128 CTS encryption vectors, lengths 17 to 64 bytes).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass NIST SP 800-38A F.5.1 to F.5.6, a test that splits one F.5 message across several calls and gets the same bytes, and a test where the counter wraps from all-ones to zero.
- [ ] `Curl.Cryptography.UnitTests` pass all six RFC 3962 appendix B CTS vectors in both directions, and an input shorter than one block is refused with `ArgumentException`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
