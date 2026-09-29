---
id: BL-743
title: Hand-build ML-KEM to FIPS 203
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-743 — Hand-build ML-KEM to FIPS 203

## Goal

`Curl.Cryptography.UnitLibrary` generates ML-KEM-512, ML-KEM-768 and ML-KEM-1024 key pairs, encapsulates and decapsulates (FIPS 203, with implicit rejection) in constant time, and exposes the SHA-3 family it needs as public types of its own (`Sha3` for SHA3-256 and SHA3-512, `Shake` for SHAKE128 and SHAKE256, FIPS 202), because the BCL's SHA-3 family is not supported on macOS; Ed448 (BL-741) and ML-DSA (BL-744) reuse them.

## Context

- ADR-0118 (BL-669): the BCL's `MLKem` works only on Linux with OpenSSL 3.5+ and on Windows Insider builds, and SHA-3 is missing on macOS (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28). The type is `MlKem`, taking the parameter set.
- Consumers: the TLS 1.3 hybrid groups `X25519MLKEM768` (OpenSSL 3.5's first default key share), `SecP256r1MLKEM768` and `SecP384r1MLKEM1024` and the pure `MLKEM512/768/1024` groups (draft-ietf-tls-ecdhe-mlkem, draft-ietf-tls-mlkem), listed by `openssl list -tls-groups` (OpenSSL 3.5.7, 2026-09-28), in the hand-built TLS client (BL-699, BL-709).
- Vectors: the NIST ACVP ML-KEM `keyGen` and `encapDecap` known-answer vectors (FIPS 203 final), including the decapsulation implicit-rejection cases; FIPS 202 vectors for the internal SHA-3 and SHAKE functions.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` reproduce a set of ACVP `keyGen`, `encapsulation` and `decapsulation` vectors for each parameter set, including one modified ciphertext that yields the implicit-rejection key.
- [ ] `Curl.Cryptography.UnitTests` pass the FIPS 202 example values (NIST "SHA-3 Examples" and the CAVP byte-oriented SHA3-256, SHA3-512, SHAKE128 and SHAKE256 short-message vectors) for `Sha3` and `Shake`.
- [ ] Decapsulation compares the re-encrypted ciphertext without a secret-dependent branch (a masked select), stated in the XML docs; the decapsulation key is zeroed on `Dispose`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
