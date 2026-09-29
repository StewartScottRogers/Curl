---
id: BL-686
title: Encrypt, decrypt and checksum with the Kerberos AES and RC4 encryption types
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685, BL-675, BL-676, BL-737]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions/ADR-0161-the-hand-built-kerberos-encrypts-with-aes-sha1-aes-sha2-and-rc4-hmac-from-the-initial-cipher-state.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-686 — Encrypt, decrypt and checksum with the Kerberos AES and RC4 encryption types

## Goal

`Curl.Kerberos.UnitLibrary` implements the Kerberos encryption types `aes256-cts-hmac-sha1-96`, `aes128-cts-hmac-sha1-96` (RFC 3962), `aes128-cts-hmac-sha256-128`, `aes256-cts-hmac-sha384-192` (RFC 8009) and `rc4-hmac` (RFC 4757): string-to-key, key derivation, encrypt, decrypt with integrity check, and their checksums, matching the RFCs' test vectors.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Framework: RFC 3961 (simplified profile, `DK`, `n-fold`). AES in CBC with ciphertext stealing is built on the BCL's `Aes`; `Rfc2898DeriveBytes.Pbkdf2`, `HMACSHA1`, `HMACSHA256`, `HMACSHA384` and `HMACMD5` are BCL; MD4 and RC4 come from `Curl.Cryptography.UnitLibrary` (BL-675, BL-676; add the reference here).
- Vectors: RFC 3961 appendix A.1 (n-fold), RFC 3962 appendix B (string-to-key and CTS), RFC 8009 appendix A (all sample results), RFC 4757 for `rc4-hmac` (and its key usage mapping).
- Randomness (the confounder) is injected so encryption is deterministic in tests.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` pass RFC 3961 A.1 n-fold vectors, RFC 3962 appendix B string-to-key and AES-CTS vectors, every RFC 8009 appendix A vector (key derivation, encryption, checksum), and an `rc4-hmac` round trip with a known key; a tampered ciphertext fails the integrity check with a typed failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- 2026-09-28 (BL-669): ADR-0118 puts AES-CBC-CTS in `Curl.Cryptography.UnitLibrary` as `AesCbcCts` (BL-737); use it rather than building CTS here. The RFC 3962 CTS vectors are pinned there; this task still checks the enctype-level vectors.
- 2026-09-28 (BL-686): Delivered as one abstract `KerberosEncryption` with an internal class per family (`AesSha1KerberosEncryption`, `AesSha2KerberosEncryption`, `Rc4HmacKerberosEncryption`), `KerberosNFold`, `KerberosAesCts` (the zero-IV wrapper over `AesCbcCts`), `IKerberosRandomSource`/`SystemKerberosRandomSource`, and `KerberosCryptographyException` with `KerberosCryptographyError`. Decisions recorded in ADR-0161: initial cipher state only, typed integrity failure, MIT's 2^24 iteration limit, MIT's rc4 usage-to-message-type mapping, PRF included for every type, `rc4-hmac-exp` not built.
- 2026-09-28 (BL-686): Vectors were copied from the RFC texts downloaded from rfc-editor.org, not from memory. RFC 3962 and RFC 4757 publish no whole-message encryption vectors, so those tests rebuild the message layout from BCL primitives and also round-trip and tamper.
- 2026-09-28 (BL-686): `touches` gained the ADR file and `Documentation/Planning/Decisions/README.md` (its index row); no task in Doing names either.
- 2026-09-28 (BL-686): Results - Kerberos tests 278 passed; `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` 100% line, 100% branch, 167 members, 0 failing, worst CRAP 10; solution build clean with `-warnaserror`; every fast test project green.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Kerberos AES-SHA1, AES-SHA2 and rc4-hmac string-to-key, encrypt, decrypt, checksum and PRF pass the RFC 3961/3962/8009 vectors
