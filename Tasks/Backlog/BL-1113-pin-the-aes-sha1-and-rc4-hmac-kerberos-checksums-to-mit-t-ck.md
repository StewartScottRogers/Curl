---
id: BL-1113
title: Pin the AES-SHA1 and RC4-HMAC Kerberos checksums to MIT t_cksums.c's cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1112]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1113 — Pin the AES-SHA1 and RC4-HMAC Kerberos checksums to MIT t_cksums.c's cases

## Goal

The keyed checksums of `aes128-cts-hmac-sha1-96`, `aes256-cts-hmac-sha1-96` and `rc4-hmac` give MIT Kerberos's published `t_cksums.c` values, as `hmac-sha1-des3-kd` already does, so a key-derivation or usage-mapping mistake in the checksum (used for the GSS-API MIC and the authenticator checksum) is caught.

## Context

- MIT krb5 `src/lib/crypto/crypto_tests/t_cksums.c` (https://github.com/krb5/krb5/blob/master/src/lib/crypto/crypto_tests/t_cksums.c): `CKSUMTYPE_HMAC_SHA1_96_AES128` of `"eight nine ten eleven twelve thirteen"`, usage 3, key `9062430C8CDA3388922E6D6A509F5B7A`, checksum `01A4B088D45628F6946614E3`; `CKSUMTYPE_HMAC_SHA1_96_AES256` of `"fourteen"`, usage 4, key `B1AE4CD8462AFF1677053CC9279AAC30B796FB81CE21474DD3DDBCFEA4EC76D7`, checksum `E08739E3279E2903EC8E3836`; `CKSUMTYPE_HMAC_MD5_ARCFOUR` of `"seventeen eighteen nineteen twenty"`, usage 6, key `F7D3A155AF5E238A0B7A871A96BA2AB2`, checksum `EB38CC97E2230F59DA4117DC5859D7EC`. Check the hex against the file at a pinned commit and name it in the test comment.
- Curl today: `Curl.Kerberos.UnitTests/Des3CbcSha1KerberosEncryptionTests.ComputeChecksum_MitTCksumsCase_MatchesVectorAndVerifies` pins des3's case; `AesSha1KerberosEncryptionTests.ComputeChecksum_IsHmacSha196UnderTheChecksumKey` and `Rc4HmacKerberosEncryptionTests.ComputeChecksum_IsHmacMd5UnderTheSigningKey` recompute the checksum with the same construction the code uses, so a shared mistake passes both. Code under test: `KerberosEncryption.ComputeChecksum` (and its verify counterpart) in `Curl.Kerberos.UnitLibrary/AesSha1KerberosEncryption.cs` and `Rc4HmacKerberosEncryption.cs`.
- A case that fails is a defect to fix in the library in this task, with a note in Notes saying what was wrong.

## Acceptance criteria

- [ ] `AesSha1KerberosEncryptionTests.cs` gains `ComputeChecksum_MitTCksumsCase_MatchesVectorAndVerifies` for the AES-128 and AES-256 cases above, passing, each also verifying the published checksum and refusing it with one bit flipped.
- [ ] `Rc4HmacKerberosEncryptionTests.cs` gains the same test for the `HMAC_MD5_ARCFOUR` case, passing.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
