---
id: BL-940
title: Check ML-DSA, Ed448, brainpool TLS 1.3 and SHA-224 signatures in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-940 — Check ML-DSA, Ed448, brainpool TLS 1.3 and SHA-224 signatures in the hand-built TLS client

## Goal

`TlsSignatureScheme` checks the nine signature schemes the OpenSSL ClientHello profile offers that the client cannot check yet, so the hand-built client's hello carries OpenSSL's whole `signature_algorithms` list (ADR-0222 decision 1).

## Context

- ADR-0222 (BL-820): `HandBuiltTlsProvider` offers only the profile schemes `TlsSignatureScheme.IsCertificateVerifyScheme` or `IsTls12Scheme` accepts, so a server never picks one the client cannot check. The OpenSSL profile (`ClientHelloProfile.OpenSsl`) loses `mldsa65` `0x0905`, `mldsa87` `0x0906`, `mldsa44` `0x0904`, `ed448` `0x0808`, `ecdsa_brainpoolP256r1tls13_sha256` `0x081a`, `ecdsa_brainpoolP384r1tls13_sha384` `0x081b`, `ecdsa_brainpoolP512r1tls13_sha512` `0x081c`, `ecdsa_sha224` `0x0303` and `rsa_pkcs1_sha224` `0x0301`.
- ML-DSA (FIPS 204) and Ed448 (RFC 8032) are not in every BCL build: hand-build what the BCL lacks in its own `Curl.<Area>.UnitLibrary` (root `CLAUDE.md`), which may split this task. Brainpool ECDSA is already verified by hand for TLS 1.2 (ADR-0219).
- Once they are accepted, `ClientHelloProfileMapping.CheckableSignatureAlgorithms` passes them with no change in `Curl.Networking`; its test `CheckableSignatureAlgorithms_LeaveOutOnlyTheOpenSslSchemesTheClientCannotCheck` then needs its expected list emptied.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` verify a TLS 1.3 CertificateVerify (and, for the TLS 1.2 schemes, a ServerKeyExchange) signed with each of the nine schemes, and refuse a bad signature for each.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

- Filed by BL-820 (ADR-0222).
- 2026-09-29 (lane 2): added `Curl.Networking.UnitTests` to `touches`. Accepting the nine
  schemes makes `CheckableSignatureAlgorithms` keep them, so
  `HandBuiltTlsProviderTests.CheckableSignatureAlgorithms_LeaveOutOnlyTheOpenSslSchemesTheClientCannotCheck`
  fails until its expected list is emptied; the fast tests cannot go green without that
  edit. BL-847 (in Doing) touches `Curl.Networking.UnitTests`, so the task went back to
  Backlog until they no longer overlap.
- Plan found on the first attempt (no code written yet). Every primitive exists already in
  `Curl.Cryptography`: `MlDsa.VerifyData(set, key, msg, context: [], sig)`, `Ed448.Verify`,
  `BrainpoolEcdsa.VerifyHash`/`SignHash`, and SHA-224 through `DsaSignature.HashData(data, new HashAlgorithmName("SHA224"))`.
  1. `TlsSignatureScheme`: constants `Mldsa44` 0x0904, `Mldsa65` 0x0905, `Mldsa87` 0x0906,
     `Ed448` 0x0808, `EcdsaBrainpoolP256r1Tls13Sha256` 0x081a, `...P384r1Tls13Sha384` 0x081b,
     `...P512r1Tls13Sha512` 0x081c, `EcdsaSha224` 0x0303, `RsaPkcs1Sha224` 0x0301; OIDs
     Ed448 `1.3.101.113`, ML-DSA-44/65/87 `2.16.840.1.101.3.4.3.17/.18/.19` (key bits are the
     raw public key). TLS 1.3 `Rules` gain ML-DSA, Ed448 and the brainpool schemes bound to
     their curve OIDs; `Tls12Rules` gain `EcdsaSha224` and `RsaPkcs1Sha224` and drop ML-DSA
     and the brainpool `tls13` schemes (TLS 1.3 only, RFC 8734 and OpenSSL's min version);
     Ed448 stays valid in TLS 1.2 (RFC 8422).
  2. `TlsSignatureKind`: add `MlDsa`, `Ed448`. `TlsCertificatePublicKey.Verify`: dispatch
     them; a key of the wrong length throws `CryptographicException` (bad_certificate) as
     Ed25519 does, a signature of the wrong length is false (MlDsa/Ed448 throw
     `ArgumentException` on wrong lengths, so check first).
  3. SHA-224: hash with `DsaSignature.HashData` and call `ECDsa.VerifyHash(..., Rfc3279DerSequence)`
     and `BrainpoolEcdsa.VerifyHash`; for `rsa_pkcs1_sha224` the BCL has no SHA-224 PKCS #1,
     so generalise `BuildMd5Sha1Block` into a PKCS #1 type 1 block builder and verify with
     `BigInteger.ModPow` over `00 01 FF.. 00 || DigestInfo(SHA-224)` (prefix
     `30 2d 30 0d 06 09 60 86 48 01 65 03 04 02 04 05 00 04 1c`), reusing `VerifyRsaMd5Sha1`'s
     key checks with a minimum modulus of payload + 11 bytes.
  4. Signing keys must not claim a scheme they cannot sign: `EcdsaTlsSigningKey` signs via
     `SignHash(DsaSignature.HashData(...))` so SHA-224 works; `RsaTlsSigningKey` signs
     `rsa_pkcs1_sha224` like MD5+SHA-1, with `RsaCrtPrivateKey` over the DigestInfo block,
     only when the private parameters export.
  5. Tests in `Curl.Tls.UnitTests` (`TlsSignatureTests`): for each of the nine, a good
     signature verifies and a flipped byte gives `DecryptError`; then empty the expected
     list in the Networking test above.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Networking.UnitTests (its CheckableSignatureAlgorithms test pins the nine schemes), which BL-847 in Doing touches
