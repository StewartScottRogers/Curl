---
id: BL-1112
title: Pin AES-SHA1 and RC4-HMAC Kerberos decryption to MIT t_decrypt.c's cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1112 — Pin AES-SHA1 and RC4-HMAC Kerberos decryption to MIT t_decrypt.c's cases

## Goal

The hand-built `aes128-cts-hmac-sha1-96` (17), `aes256-cts-hmac-sha1-96` (18) and `rc4-hmac` (23) encryptions decrypt every one of MIT Kerberos's published `t_decrypt.c` ciphertexts to its plaintext, as `des3-cbc-sha1` already does, so a confounder, key-usage or MAC-placement mistake that a round trip cannot see is caught.

## Context

- MIT krb5 `src/lib/crypto/crypto_tests/t_decrypt.c` (https://github.com/krb5/krb5/blob/master/src/lib/crypto/crypto_tests/t_decrypt.c) lists, for each enctype, five cases - plaintext `""`, `"1"`, `"9 bytesss"`, `"13 bytes byte"` and `"30 bytes bytes bytes bytes byt"` under key usages 0 to 4 - each with its key and ciphertext. For example the first `ENCTYPE_ARCFOUR_HMAC` case: usage 0, key `F81FEC39255F5784E850C4377C88BD85`, ciphertext `02C1EB15586144122EC717763DD348BF00434DDC6585954C`.
- Curl today: `Curl.Kerberos.UnitTests/Des3CbcSha1KerberosEncryptionTests.cs` `Decrypt_MitTDecryptCase_GivesThePlaintextAndItsZeroPadding` pins des3's five cases, and `CamelliaCmacKerberosEncryptionTests.cs` uses t_decrypt for Camellia. But `AesSha1KerberosEncryptionTests.cs` checks AES encryption only structurally (`Encrypt_Aes256_IsCtsOfConfounderAndPlaintextThenHmacSha196`, round trips, tampering) and `Rc4HmacKerberosEncryptionTests.cs` only by round trip with RFC 4757's `foo` key (`Decrypt_RoundTripWithFooKey_ReturnsPlaintext`). No known ciphertext is decrypted for 17, 18 or 23.
- Code under test: `Curl.Kerberos.UnitLibrary/AesSha1KerberosEncryption.cs` and `Rc4HmacKerberosEncryption.cs` through `KerberosEncryption.Decrypt(key, usage, ciphertext)`. A case that fails is a defect to fix in the library in this task, with a note in Notes saying what was wrong.
- Copy the hex from the file at a pinned commit and name the commit in the test's comment.

## Acceptance criteria

- [ ] `AesSha1KerberosEncryptionTests.cs` gains `Decrypt_MitTDecryptCase_GivesThePlaintext` with all five `ENCTYPE_AES128_CTS_HMAC_SHA1_96` and all five `ENCTYPE_AES256_CTS_HMAC_SHA1_96` cases, passing.
- [ ] `Rc4HmacKerberosEncryptionTests.cs` gains the same test with all five `ENCTYPE_ARCFOUR_HMAC` cases, passing.
- [ ] Each test also decrypts one case with one ciphertext bit flipped and asserts `KerberosCryptographyError.IntegrityCheckFailed`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
