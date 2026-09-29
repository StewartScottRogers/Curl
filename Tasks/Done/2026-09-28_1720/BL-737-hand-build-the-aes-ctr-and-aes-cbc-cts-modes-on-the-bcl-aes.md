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
completed: 2026-09-28
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

- [x] `Curl.Cryptography.UnitTests` pass NIST SP 800-38A F.5.1 to F.5.6, a test that splits one F.5 message across several calls and gets the same bytes, and a test where the counter wraps from all-ones to zero.
- [x] `Curl.Cryptography.UnitTests` pass all six RFC 3962 appendix B CTS vectors in both directions, and an input shorter than one block is refused with `ArgumentException`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built directly in the session: the API, constant-time and zeroing rules are all fixed
  by ADR-0118, so no new design decision and no new ADR was needed.
- `AesCtr(key, initialCounter)` with `ApplyKeyStream(source, destination)`, named as
  `ChaCha20.ApplyKeyStream` is. It encrypts sixteen counter blocks at a time with one
  `Aes.EncryptEcb` call and keeps the unused keystream, so a message split anywhere
  across calls gives the one-call bytes. `Increment` carries through all sixteen bytes
  with no early exit.
- `AesCbcCts(key)` with `Encrypt`/`Decrypt(initializationVector, source, destination)`,
  the same shape as `Blowfish.EncryptCbc`. A one-block message is plain CBC, as RFC 3962
  section 5 says; longer ones always swap the last two blocks (CBC-CS3), which the
  32-, 48- and 64-byte appendix B vectors confirm. It takes the IV as a parameter;
  Kerberos passes zeros or the previous cipher state (BL-686's business).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` gives
  100% line, 100% branch, 129 members, 0 failing, worst CRAP 10; 304 Cryptography tests
  pass.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. AesCtr and AesCbcCts pass SP 800-38A F.5 and RFC 3962 appendix B vectors at 100% coverage
