---
id: BL-745
title: Hand-build DSA signing and verification to FIPS 186-4 for every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-739]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-745 — Hand-build DSA signing and verification to FIPS 186-4 for every platform

## Goal

`Curl.Cryptography.UnitLibrary` signs and verifies DSA (FIPS 186-4) for 1024-bit keys with SHA-1 and for 2048- and 3072-bit keys with SHA-224 and SHA-256, with the deterministic nonce of RFC 6979 and the constant-time exponentiation of BL-739, identically on every platform.

## Context

- ADR-0118 (BL-669): the BCL's `DSA` on macOS cannot create keys, and keys over 1024 bits "don't behave according to FIPS 186-3" there (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28), so DSA is hand-built and used on every platform. The type is `DsaSignature` so it never collides with `System.Security.Cryptography.DSA`.
- Consumers: SSH `ssh-dss` host and user keys (libssh2 1.11.1 offers `ssh-dss`; BL-560's ADR), and TLS 1.2 `dsa_sha1`/`dsa_sha256` signatures on DSA server and client certificates (BL-703, BL-709).
- Vectors: the NIST CAVP FIPS 186-4 DSA `SigVer` vectors for (L, N) = (1024, 160), (2048, 224), (2048, 256), (3072, 256) and RFC 6979 appendix A.2.1 and A.2.2 (deterministic DSA 1024 and 2048 with SHA-1 and SHA-256).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` reproduce RFC 6979 A.2.1 and A.2.2 signatures and every chosen CAVP `SigVer` vector's pass/fail result.
- [x] Verification rejects r or s of 0 or not below q; signing has no secret-dependent branch or table index, stated in the XML docs; the private key is zeroed on `Dispose`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered in-session (plan, tests, implementation, verify, docs) on `MontgomeryModulus`:
  `DsaSignature` (public), `DsaDomainParameters`, `DeterministicDsaNonce` and `Sha224`
  (internal). Decisions in ADR-0200 (Decided by Claude under Stewart's delegation):
  RFC 6979 nonces always; SHA-1 to SHA-512 for signing (SHA-224 hand-built since the BCL
  lacks it on every platform); OpenSSL's verification limits for domain parameters (q of
  160/224/256 bits, odd p longer than q up to 10,000 bits, 1 < g < p); signatures are
  r || s at q's length (the `ssh-dss` blob; TLS's DER is the caller's).
- Signing's only branch is RFC 6979's candidate rejection; every candidate is computed in
  full and the k, r and s checks combined with non-short-circuit `&`, which also let the
  (probability ~2^-160) r = 0 or s = 0 path share the covered rejection branch.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0200 and its index row; no
  task in Doing named it.
- Vectors: RFC 6979 A.2.1/A.2.2 (all 20 signatures, SHA-1 to SHA-512, plus the k values
  for 1024 bits) and NIST CAVP `186-3dsatestvectors.zip` `SigVer.rsp` groups (1024,160,SHA-1),
  (2048,224,SHA-224), (2048,256,SHA-256), (3072,256,SHA-256), 60 vectors, in
  `KnownAnswers/dsa-sigver-fips-186-4.txt`.
- Measured: Curl.Cryptography.UnitLibrary 100% line, 100% branch, 425 members, 0 failing,
  worst CRAP 10. Fast tests: all green (Curl.Cryptography.UnitTests 933).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. DsaSignature signs with RFC 6979 nonces and verifies FIPS 186-4 DSA (1024 to 3072 bits, SHA-1 to SHA-512) on every platform
